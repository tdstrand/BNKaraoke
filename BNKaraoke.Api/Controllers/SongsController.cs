using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using BNKaraoke.Api.Data;
using BNKaraoke.Api.Models;
using System.Net.Http;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Security.Claims;

namespace BNKaraoke.Controllers
{
    [Route("api/songs")]
    [ApiController]
    public class SongsController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly IConfiguration _configuration;

        public SongsController(ApplicationDbContext context, IHttpClientFactory httpClientFactory, IConfiguration configuration)
        {
            _context = context;
            _httpClientFactory = httpClientFactory;
            _configuration = configuration;
        }

        [HttpGet("search")]
        [Authorize(Policy = "Singer")]
        public async Task<IActionResult> Search(string query = "", int page = 1, int pageSize = 50)
        {
            var songsQuery = _context.Songs.Where(s => s.Status == "active");
            if (!string.IsNullOrEmpty(query) && query.ToLower() != "all")
            {
                songsQuery = songsQuery.Where(s => s.Title.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                                                  s.Artist.Contains(query, StringComparison.OrdinalIgnoreCase));
            }

            var totalSongs = await songsQuery.CountAsync();
            var songs = await songsQuery
                .OrderBy(s => s.Title)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            return Ok(new
            {
                TotalSongs = totalSongs,
                Songs = songs,
                CurrentPage = page,
                PageSize = pageSize,
                TotalPages = (int)Math.Ceiling(totalSongs / (double)pageSize)
            });
        }

        [HttpGet("user-requests")]
        [Authorize(Policy = "Singer")]
        public async Task<IActionResult> GetUserRequests()
        {
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            Console.WriteLine($"GetUserRequests: UserId from token: {userId}");
            if (string.IsNullOrEmpty(userId))
            {
                return BadRequest("User identity not found in token");
            }
            var songs = await _context.Songs
                .Where(s => s.RequestedBy == userId && s.Status == "pending")
                .ToListAsync();
            Console.WriteLine($"GetUserRequests: Found {songs.Count} pending songs for {userId}");
            return Ok(songs);
        }

        [HttpGet("pending")]
        [Authorize(Policy = "SongManager")]
        public async Task<IActionResult> GetPending()
        {
            Console.WriteLine("GetPending: Endpoint called - Starting execution");
            try
            {
                Console.WriteLine("GetPending: Querying database for pending songs");
                var songs = await _context.Songs
                    .Where(s => s.Status == "pending")
                    .ToListAsync();
                Console.WriteLine($"GetPending: Found {songs.Count} pending songs");
                return Ok(songs);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"GetPending: Exception occurred - {ex.Message}");
                throw;
            }
        }

        [HttpGet("youtube-search")]
        [Authorize(Policy = "SongManager")]
        public async Task<IActionResult> YouTubeSearch(string query)
        {
            try
            {
                var client = _httpClientFactory.CreateClient();
                var apiKey = _configuration["YouTube:ApiKey"];
                if (string.IsNullOrEmpty(apiKey))
                {
                    throw new InvalidOperationException("YouTube API key is missing in configuration.");
                }
                var response = await client.GetAsync(
                    $"https://www.googleapis.com/youtube/v3/search?part=snippet&q={Uri.EscapeDataString(query)}&type=video&key={apiKey}&maxResults=10"
                );
                Console.WriteLine($"YouTubeSearch: Status for query '{query}': {response.StatusCode}");
                if (!response.IsSuccessStatusCode)
                {
                    var errorText = await response.Content.ReadAsStringAsync();
                    Console.WriteLine($"YouTubeSearch: Error response: {errorText}");
                    return BadRequest($"YouTube search failed: {errorText}");
                }

                var json = await response.Content.ReadAsStringAsync();
                Console.WriteLine($"YouTubeSearch JSON: {json}");
                var data = JsonSerializer.Deserialize<YouTubeSearchResponse>(json);
                Console.WriteLine($"YouTubeSearch Deserialized Data: {JsonSerializer.Serialize(data)}");
                if (data == null)
                {
                    Console.WriteLine("YouTubeSearch: No valid response found");
                    return Ok(new List<object>());
                }

                var videos = (data.Items ?? new List<YouTubeItem>()).Select(v => new
                {
                    videoId = v.Id?.VideoId ?? "unknown",
                    title = v.Snippet?.Title ?? "Untitled",
                    url = v.Id?.VideoId != null ? $"https://www.youtube.com/watch?v={v.Id.VideoId}" : "unknown"
                }).ToList();
                Console.WriteLine($"YouTubeSearch: Found {videos.Count} videos");
                return Ok(videos);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"YouTubeSearch: Exception: {ex.Message}");
                return StatusCode(500, $"Internal server error: {ex.Message}");
            }
        }

        [HttpGet("spotify-search")]
        [Authorize(Policy = "Singer")]
        public async Task<IActionResult> SpotifySearch(string query)
        {
            try
            {
                if (string.IsNullOrEmpty(query))
                {
                    return BadRequest("Query parameter is required.");
                }

                Console.WriteLine($"SpotifySearch: Starting with query: {query}");
                var client = _httpClientFactory.CreateClient();
                var token = await GetSpotifyToken(client);
                Console.WriteLine($"SpotifySearch: Token retrieved: {token}");
                client.DefaultRequestHeaders.Clear();
                client.DefaultRequestHeaders.Add("Authorization", $"Bearer {token}");

                var response = await client.GetAsync($"https://api.spotify.com/v1/search?q={Uri.EscapeDataString(query)}&type=track&limit=10");
                Console.WriteLine($"SpotifySearch: Search status: {response.StatusCode}");
                if (!response.IsSuccessStatusCode)
                {
                    var errorText = await response.Content.ReadAsStringAsync();
                    Console.WriteLine($"SpotifySearch: Search error response: {errorText}");
                    return BadRequest($"Spotify search failed: {errorText}");
                }

                var json = await response.Content.ReadAsStringAsync();
                Console.WriteLine($"SpotifySearch: Search JSON: {json}");
                var data = JsonSerializer.Deserialize<SpotifySearchResponse>(json);
                if (data?.Tracks == null)
                {
                    Console.WriteLine("SpotifySearch: Tracks object is null");
                    return BadRequest("No tracks data in Spotify response");
                }

                var songs = new List<Song>();
                foreach (var track in data.Tracks.Items ?? new List<SpotifyTrack>())
                {
                    var song = new Song
                    {
                        Title = track.Name,
                        Artist = string.Join(", ", track.Artists.Select(a => a.Name)),
                        SpotifyId = track.Id,
                        Status = "pending",
                        RequestDate = DateTime.UtcNow,
                        RequestedBy = User.Identity.Name ?? User.FindFirst(ClaimTypes.NameIdentifier)?.Value,
                        Bpm = 0,
                        Danceability = 0,
                        Energy = 0
                    };

                    var trackResponse = await client.GetAsync($"https://api.spotify.com/v1/tracks/{track.Id}");
                    if (trackResponse.IsSuccessStatusCode)
                    {
                        var trackJson = await trackResponse.Content.ReadAsStringAsync();
                        var trackDetails = JsonSerializer.Deserialize<SpotifyTrackDetails>(trackJson);
                        if (trackDetails != null)
                        {
                            song.Popularity = trackDetails.Popularity;
                        }
                    }

                    if (track.Artists.Any())
                    {
                        var artistId = track.Artists[0].Id;
                        var artistResponse = await client.GetAsync($"https://api.spotify.com/v1/artists/{artistId}");
                        if (artistResponse.IsSuccessStatusCode)
                        {
                            var artistJson = await artistResponse.Content.ReadAsStringAsync();
                            var artistDetails = JsonSerializer.Deserialize<SpotifyArtistDetails>(artistJson);
                            song.Genre = artistDetails?.Genres.FirstOrDefault() ?? "Unknown";
                        }
                        else
                        {
                            song.Genre = "Unknown";
                        }
                    }
                    else
                    {
                        song.Genre = "Unknown";
                    }

                    songs.Add(song);
                }

                Console.WriteLine($"SpotifySearch: Found {songs.Count} songs");
                return Ok(songs);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"SpotifySearch: Exception: {ex.Message}");
                return StatusCode(500, $"Internal server error: {ex.Message}");
            }
        }

        [HttpPost("approve")]
        [Authorize(Policy = "SongManager")]
        public async Task<IActionResult> ApproveSong([FromBody] ApproveSongRequest request)
        {
            try
            {
                var song = await _context.Songs.FindAsync(request.Id);
                if (song == null) return NotFound("Song not found");
                song.YouTubeUrl = request.YouTubeUrl;
                song.Status = "active";
                song.ApprovedBy = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(song.ApprovedBy))
                {
                    Console.WriteLine("ApprovedBy is null in ApproveSong. Claims: " +
                        string.Join(", ", User.Claims.Select(c => $"{c.Type}: {c.Value}")));
                }
                await _context.SaveChangesAsync();
                Console.WriteLine($"ApproveSong: Song '{song.Title}' approved by {song.ApprovedBy}");
                return Ok(new { message = "Party hit approved!" });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"ApproveSong: Exception: {ex.Message}");
                return StatusCode(500, $"Failed to approve song: {ex.Message}");
            }
        }

        [HttpPost("reject")]
        [Authorize(Policy = "SongManager")]
        public async Task<IActionResult> RejectSong([FromBody] RejectSongRequest request)
        {
            try
            {
                var song = await _context.Songs.FindAsync(request.Id);
                if (song == null) return NotFound("Song not found");
                song.Status = "unavailable";
                await _context.SaveChangesAsync();
                Console.WriteLine($"RejectSong: Song '{song.Title}' rejected");
                return Ok(new { message = "Song sidelined for now!" });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"RejectSong: Exception: {ex.Message}");
                return StatusCode(500, $"Failed to reject song: {ex.Message}");
            }
        }

        [HttpPost("request")]
        [Authorize(Policy = "Singer")]
        public async Task<IActionResult> RequestSong([FromBody] Song song)
        {
            try
            {
                song.Status = "pending";
                song.RequestDate = DateTime.UtcNow;
                song.RequestedBy = User.Identity.Name ?? User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(song.RequestedBy))
                {
                    Console.WriteLine("RequestedBy is null in RequestSong. Claims: " +
                        string.Join(", ", User.Claims.Select(c => $"{c.Type}: {c.Value}")));
                    return BadRequest("User identity not found in token");
                }

                _context.Songs.Add(song);
                await _context.SaveChangesAsync();
                Console.WriteLine($"RequestSong: Song '{song.Title}' added by {song.RequestedBy}");
                return Ok(new { message = "Song added to the party queue!" });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"RequestSong: Exception: {ex.Message}");
                return StatusCode(500, $"Failed to add song request: {ex.Message}");
            }
        }

        private async Task<string> GetSpotifyToken(HttpClient client)
        {
            try
            {
                var clientId = _configuration["Spotify:ClientId"];
                var clientSecret = _configuration["Spotify:ClientSecret"];
                if (string.IsNullOrEmpty(clientId) || string.IsNullOrEmpty(clientSecret))
                {
                    throw new InvalidOperationException("Spotify ClientId or ClientSecret is missing in configuration.");
                }

                var request = new HttpRequestMessage(HttpMethod.Post, "https://accounts.spotify.com/api/token")
                {
                    Content = new FormUrlEncodedContent(new Dictionary<string, string>
                    {
                        { "grant_type", "client_credentials" },
                        { "client_id", clientId },
                        { "client_secret", clientSecret }
                    })
                };

                var response = await client.SendAsync(request);
                Console.WriteLine($"GetSpotifyToken: Response status: {response.StatusCode}");
                if (!response.IsSuccessStatusCode)
                {
                    var errorText = await response.Content.ReadAsStringAsync();
                    Console.WriteLine($"GetSpotifyToken: Error response: {errorText}");
                    throw new InvalidOperationException($"Failed to get Spotify token: {errorText}");
                }

                var json = await response.Content.ReadAsStringAsync();
                Console.WriteLine($"GetSpotifyToken: Raw JSON: {json}");
                var tokenData = JsonSerializer.Deserialize<SpotifyTokenResponse>(json);
                if (string.IsNullOrEmpty(tokenData?.AccessToken))
                {
                    throw new InvalidOperationException("Spotify token response missing access_token");
                }

                return tokenData.AccessToken;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"GetSpotifyToken: Exception: {ex.Message}");
                throw;
            }
        }

        public class SpotifySearchResponse
        {
            [JsonPropertyName("tracks")]
            public SpotifyTracks Tracks { get; set; }
        }

        public class SpotifyTracks
        {
            [JsonPropertyName("items")]
            public List<SpotifyTrack> Items { get; set; }
        }

        public class SpotifyTrack
        {
            [JsonPropertyName("id")]
            public string Id { get; set; }
            [JsonPropertyName("name")]
            public string Name { get; set; }
            [JsonPropertyName("artists")]
            public List<SpotifyArtist> Artists { get; set; }
        }

        public class SpotifyArtist
        {
            [JsonPropertyName("id")]
            public string Id { get; set; }
            [JsonPropertyName("name")]
            public string Name { get; set; }
        }

        public class SpotifyTokenResponse
        {
            [JsonPropertyName("access_token")]
            public string AccessToken { get; set; }
        }

        public class SpotifyTrackDetails
        {
            [JsonPropertyName("popularity")]
            public int Popularity { get; set; }
        }

        public class SpotifyArtistDetails
        {
            [JsonPropertyName("genres")]
            public List<string> Genres { get; set; }
        }

        public class YouTubeSearchResponse
        {
            [JsonPropertyName("items")]
            public List<YouTubeItem> Items { get; set; }
        }

        public class YouTubeItem
        {
            [JsonPropertyName("id")]
            public YouTubeId Id { get; set; }
            [JsonPropertyName("snippet")]
            public YouTubeSnippet Snippet { get; set; }
        }

        public class YouTubeId
        {
            [JsonPropertyName("videoId")]
            public string VideoId { get; set; }
        }

        public class YouTubeSnippet
        {
            [JsonPropertyName("title")]
            public string Title { get; set; }
        }

        public class ApproveSongRequest
        {
            public int Id { get; set; }
            public string YouTubeUrl { get; set; }
        }

        public class RejectSongRequest
        {
            public int Id { get; set; }
        }
    }
}
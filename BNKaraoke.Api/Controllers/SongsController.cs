using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using BNKaraoke.Api.Data;
using BNKaraoke.Api.Models;
using System.Net.Http;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Security.Claims;
using Microsoft.Extensions.Logging;

namespace BNKaraoke.Controllers
{
    [Route("api/songs")]
    [ApiController]
    public class SongsController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly IConfiguration _configuration;
        private readonly ILogger<SongsController> _logger;

        public SongsController(ApplicationDbContext context, IHttpClientFactory httpClientFactory, IConfiguration configuration, ILogger<SongsController> logger)
        {
            _context = context;
            _httpClientFactory = httpClientFactory;
            _configuration = configuration;
            _logger = logger;
        }

        [HttpGet("search")]
        [Authorize(Policy = "Singer")]
        public async Task<IActionResult> Search(string query = "", int page = 1, int pageSize = 50)
        {
            _logger.LogInformation("Search: Query={Query}, Page={Page}, PageSize={PageSize}", query, page, pageSize);

            if (pageSize > 100)
            {
                _logger.LogWarning("Search: PageSize {PageSize} exceeds maximum limit of 100", pageSize);
                return BadRequest(new { error = "PageSize cannot exceed 100" });
            }

            var songsQuery = _context.Songs.Where(s => s.Status == "active");
            if (!string.IsNullOrEmpty(query) && query.ToLower() != "all")
            {
                songsQuery = songsQuery.Where(s => EF.Functions.ILike(s.Title, $"%{query}%") ||
                                                  EF.Functions.ILike(s.Artist, $"%{query}%"));
            }

            var totalSongs = await songsQuery.CountAsync();
            var songs = await songsQuery
                .OrderBy(s => s.Title)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            _logger.LogInformation("Search: Found {TotalSongs} songs, returning {SongCount} for page {Page}", totalSongs, songs.Count, page);

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
            _logger.LogInformation("GetUserRequests: UserId={UserId}", userId);

            if (string.IsNullOrEmpty(userId))
            {
                _logger.LogWarning("GetUserRequests: User identity not found in token");
                return BadRequest(new { error = "User identity not found in token" });
            }

            var songs = await _context.Songs
                .Where(s => s.RequestedBy == userId && s.Status == "pending")
                .ToListAsync();

            _logger.LogInformation("GetUserRequests: Found {SongCount} pending songs for UserId={UserId}", songs.Count, userId);
            return Ok(songs);
        }

        [HttpGet("pending")]
        [Authorize(Policy = "SongManager")]
        public async Task<IActionResult> GetPending()
        {
            _logger.LogInformation("GetPending: Querying database for pending songs");

            try
            {
                var songs = await _context.Songs
                    .Where(s => s.Status == "pending")
                    .ToListAsync();

                _logger.LogInformation("GetPending: Found {SongCount} pending songs", songs.Count);
                return Ok(songs);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "GetPending: Exception occurred");
                return StatusCode(500, new { error = "Failed to retrieve pending songs" });
            }
        }

        [HttpGet("youtube-search")]
        [Authorize(Policy = "SongManager")]
        public async Task<IActionResult> YouTubeSearch(string query)
        {
            _logger.LogInformation("YouTubeSearch: Query={Query}", query);

            try
            {
                var client = _httpClientFactory.CreateClient();
                var apiKey = _configuration["YouTube:ApiKey"];
                if (string.IsNullOrEmpty(apiKey))
                {
                    _logger.LogError("YouTubeSearch: YouTube API key is missing in configuration");
                    return BadRequest(new { error = "YouTube API key is missing" });
                }

                var response = await client.GetAsync(
                    $"https://www.googleapis.com/youtube/v3/search?part=snippet&q={Uri.EscapeDataString(query)}&type=video&key={apiKey}&maxResults=10"
                );

                _logger.LogInformation("YouTubeSearch: Status for query '{Query}': {StatusCode}", query, response.StatusCode);

                if (!response.IsSuccessStatusCode)
                {
                    var errorText = await response.Content.ReadAsStringAsync();
                    _logger.LogWarning("YouTubeSearch: Error response: {ErrorText}", errorText);
                    return BadRequest(new { error = $"YouTube search failed: {errorText}" });
                }

                var json = await response.Content.ReadAsStringAsync();
                var data = JsonSerializer.Deserialize<YouTubeSearchResponse>(json);

                if (data?.Items == null)
                {
                    _logger.LogWarning("YouTubeSearch: No valid response found for query '{Query}'", query);
                    return Ok(new List<object>());
                }

                var videos = data.Items
                    .Where(v => v != null)
                    .Select(v => new
                    {
                        videoId = v.Id?.VideoId ?? "unknown",
                        title = v.Snippet?.Title ?? "Untitled",
                        url = v.Id?.VideoId != null ? $"https://www.youtube.com/watch?v={v.Id.VideoId}" : "unknown"
                    }).ToList();

                _logger.LogInformation("YouTubeSearch: Found {VideoCount} videos for query '{Query}'", videos.Count, query);
                return Ok(videos);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "YouTubeSearch: Exception for query '{Query}'", query);
                return StatusCode(500, new { error = "Failed to search YouTube" });
            }
        }

        [HttpGet("spotify-search")]
        [Authorize(Policy = "Singer")]
        public async Task<IActionResult> SpotifySearch(string query)
        {
            _logger.LogInformation("SpotifySearch: Query={Query}", query);

            try
            {
                if (string.IsNullOrEmpty(query))
                {
                    _logger.LogWarning("SpotifySearch: Query parameter is missing");
                    return BadRequest(new { error = "Query parameter is required" });
                }

                var client = _httpClientFactory.CreateClient();
                var token = await GetSpotifyToken(client);
                client.DefaultRequestHeaders.Clear();
                client.DefaultRequestHeaders.Add("Authorization", $"Bearer {token}");

                var response = await client.GetAsync($"https://api.spotify.com/v1/search?q={Uri.EscapeDataString(query)}&type=track&limit=10");
                _logger.LogInformation("SpotifySearch: Search status: {StatusCode}", response.StatusCode);

                if (!response.IsSuccessStatusCode)
                {
                    var errorText = await response.Content.ReadAsStringAsync();
                    _logger.LogWarning("SpotifySearch: Search error response: {ErrorText}", errorText);
                    return BadRequest(new { error = $"Spotify search failed: {errorText}" });
                }

                var json = await response.Content.ReadAsStringAsync();
                var data = JsonSerializer.Deserialize<SpotifySearchResponse>(json);

                if (data?.Tracks == null)
                {
                    _logger.LogWarning("SpotifySearch: Tracks object is null for query '{Query}'", query);
                    return BadRequest(new { error = "No tracks found in Spotify response" });
                }

                var songs = new List<Song>();
                foreach (var track in data.Tracks.Items)
                {
                    var song = new Song
                    {
                        Title = track.Name,
                        Artist = string.Join(", ", track.Artists.Select(a => a.Name)),
                        SpotifyId = track.Id,
                        Status = "pending",
                        RequestDate = DateTime.UtcNow,
                        RequestedBy = User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? string.Empty,
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

                _logger.LogInformation("SpotifySearch: Found {SongCount} songs for query '{Query}'", songs.Count, query);
                return Ok(songs);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "SpotifySearch: Exception for query '{Query}'", query);
                return StatusCode(500, new { error = "Failed to search Spotify" });
            }
        }

        [HttpPost("approve")]
        [Authorize(Policy = "SongManager")]
        public async Task<IActionResult> ApproveSong([FromBody] ApproveSongRequest request)
        {
            _logger.LogInformation("ApproveSong: SongId={SongId}", request.Id);

            try
            {
                var song = await _context.Songs.FindAsync(request.Id);
                if (song == null)
                {
                    _logger.LogWarning("ApproveSong: Song not found - SongId={SongId}", request.Id);
                    return NotFound(new { error = "Song not found" });
                }

                song.YouTubeUrl = request.YouTubeUrl;
                song.Status = "active";
                song.ApprovedBy = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

                if (string.IsNullOrEmpty(song.ApprovedBy))
                {
                    _logger.LogWarning("ApproveSong: ApprovedBy is null. Claims: {Claims}", string.Join(", ", User.Claims.Select(c => $"{c.Type}: {c.Value}")));
                }

                await _context.SaveChangesAsync();
                _logger.LogInformation("ApproveSong: Song '{Title}' approved by {ApprovedBy}", song.Title, song.ApprovedBy);
                return Ok(new { message = "Party hit approved!" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "ApproveSong: Exception for SongId={SongId}", request.Id);
                return StatusCode(500, new { error = "Failed to approve song" });
            }
        }

        [HttpPost("reject")]
        [Authorize(Policy = "SongManager")]
        public async Task<IActionResult> RejectSong([FromBody] RejectSongRequest request)
        {
            _logger.LogInformation("RejectSong: SongId={SongId}", request.Id);

            try
            {
                var song = await _context.Songs.FindAsync(request.Id);
                if (song == null)
                {
                    _logger.LogWarning("RejectSong: Song not found - SongId={SongId}", request.Id);
                    return NotFound(new { error = "Song not found" });
                }

                song.Status = "unavailable";
                await _context.SaveChangesAsync();
                _logger.LogInformation("RejectSong: Song '{Title}' rejected", song.Title);
                return Ok(new { message = "Song sidelined for now!" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "RejectSong: Exception for SongId={SongId}", request.Id);
                return StatusCode(500, new { error = "Failed to reject song" });
            }
        }

        [HttpPost("request")]
        [Authorize(Policy = "Singer")]
        public async Task<IActionResult> RequestSong([FromBody] Song song)
        {
            _logger.LogInformation("RequestSong: Title={Title}, Artist={Artist}", song.Title, song.Artist);

            try
            {
                song.Status = "pending";
                song.RequestDate = DateTime.UtcNow;
                song.RequestedBy = User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? string.Empty;

                if (string.IsNullOrEmpty(song.RequestedBy))
                {
                    _logger.LogWarning("RequestSong: RequestedBy is null. Claims: {Claims}", string.Join(", ", User.Claims.Select(c => $"{c.Type}: {c.Value}")));
                    return BadRequest(new { error = "User identity not found in token" });
                }

                _context.Songs.Add(song);
                await _context.SaveChangesAsync();
                _logger.LogInformation("RequestSong: Song '{Title}' added by {RequestedBy}", song.Title, song.RequestedBy);
                return Ok(new { message = "Song added to the party queue!" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "RequestSong: Exception for Title={Title}", song.Title);
                return StatusCode(500, new { error = "Failed to add song request" });
            }
        }

        private async Task<string> GetSpotifyToken(HttpClient client)
        {
            _logger.LogInformation("GetSpotifyToken: Requesting token");

            try
            {
                var clientId = _configuration["Spotify:ClientId"];
                var clientSecret = _configuration["Spotify:ClientSecret"];
                if (string.IsNullOrEmpty(clientId) || string.IsNullOrEmpty(clientSecret))
                {
                    _logger.LogError("GetSpotifyToken: Spotify ClientId or ClientSecret is missing in configuration");
                    throw new InvalidOperationException("Spotify ClientId or ClientSecret is missing.");
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
                _logger.LogInformation("GetSpotifyToken: Response status: {StatusCode}", response.StatusCode);

                if (!response.IsSuccessStatusCode)
                {
                    var errorText = await response.Content.ReadAsStringAsync();
                    _logger.LogWarning("GetSpotifyToken: Error response: {ErrorText}", errorText);
                    throw new InvalidOperationException($"Failed to get Spotify token: {errorText}");
                }

                var json = await response.Content.ReadAsStringAsync();
                var tokenData = JsonSerializer.Deserialize<SpotifyTokenResponse>(json);

                if (string.IsNullOrEmpty(tokenData?.AccessToken))
                {
                    _logger.LogError("GetSpotifyToken: Spotify token response missing access_token");
                    throw new InvalidOperationException("Spotify token response missing access_token");
                }

                _logger.LogInformation("GetSpotifyToken: Token retrieved successfully");
                return tokenData.AccessToken;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "GetSpotifyToken: Exception occurred");
                throw;
            }
        }

        public class SpotifySearchResponse
        {
            [JsonPropertyName("tracks")]
            public required SpotifyTracks Tracks { get; set; }
        }

        public class SpotifyTracks
        {
            [JsonPropertyName("items")]
            public required List<SpotifyTrack> Items { get; set; }
        }

        public class SpotifyTrack
        {
            [JsonPropertyName("id")]
            public required string Id { get; set; }
            [JsonPropertyName("name")]
            public required string Name { get; set; }
            [JsonPropertyName("artists")]
            public required List<SpotifyArtist> Artists { get; set; }
        }

        public class SpotifyArtist
        {
            [JsonPropertyName("id")]
            public required string Id { get; set; }
            [JsonPropertyName("name")]
            public required string Name { get; set; }
        }

        public class SpotifyTokenResponse
        {
            [JsonPropertyName("access_token")]
            public required string AccessToken { get; set; }
        }

        public class SpotifyTrackDetails
        {
            [JsonPropertyName("popularity")]
            public int Popularity { get; set; }
        }

        public class SpotifyArtistDetails
        {
            [JsonPropertyName("genres")]
            public required List<string> Genres { get; set; }
        }

        public class YouTubeSearchResponse
        {
            [JsonPropertyName("items")]
            public required List<YouTubeItem> Items { get; set; }
        }

        public class YouTubeItem
        {
            [JsonPropertyName("id")]
            public YouTubeId? Id { get; set; }
            [JsonPropertyName("snippet")]
            public YouTubeSnippet? Snippet { get; set; }
        }

        public class YouTubeId
        {
            [JsonPropertyName("videoId")]
            public string? VideoId { get; set; }
        }

        public class YouTubeSnippet
        {
            [JsonPropertyName("title")]
            public string? Title { get; set; }
        }

        public class ApproveSongRequest
        {
            public int Id { get; set; }
            public string? YouTubeUrl { get; set; }
        }

        public class RejectSongRequest
        {
            public int Id { get; set; }
        }
    }
}
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using BNKaraoke.Api.Data;
using BNKaraoke.Api.Models;
using System.Security.Claims;
using System.Net.Http;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using System.Globalization;

namespace BNKaraoke.Api.Controllers
{
    [Route("api/songs")]
    [ApiController]
    public class SongController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly IConfiguration _configuration;
        private readonly ILogger<SongController> _logger;

        public SongController(ApplicationDbContext context, IHttpClientFactory httpClientFactory, IConfiguration configuration, ILogger<SongController> logger)
        {
            _context = context;
            _httpClientFactory = httpClientFactory;
            _configuration = configuration;
            _logger = logger;
        }

        [HttpGet("{songId}")]
        [Authorize(Policy = "Singer")]
        public async Task<IActionResult> GetSongById(int songId)
        {
            _logger.LogInformation("Fetching song with SongId: {SongId}", songId);

            try
            {
                var song = await _context.Songs.FindAsync(songId);
                if (song == null)
                {
                    _logger.LogWarning("Song not found with SongId: {SongId}", songId);
                    return NotFound("Song not found");
                }

                _logger.LogInformation("Successfully fetched song with SongId: {SongId}", songId);
                return Ok(song);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching song with SongId {SongId}: {Message}", songId, ex.Message);
                return StatusCode(500, new { message = "An error occurred while fetching the song", details = ex.Message });
            }
        }

        [HttpGet("users")]
        [Authorize(Policy = "Singer")]
        public async Task<IActionResult> GetUsers()
        {
            _logger.LogInformation("Fetching list of users");

            try
            {
                var users = await _context.Users
                    .Select(u => new
                    {
                        Id = u.Id,
                        UserName = u.UserName,
                        FirstName = u.FirstName,
                        LastName = u.LastName
                    })
                    .OrderBy(u => u.FirstName)
                    .ThenBy(u => u.LastName)
                    .ToListAsync();

                _logger.LogInformation("Successfully fetched {Count} users", users.Count);
                return Ok(users);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching users: {Message}", ex.Message);
                return StatusCode(500, new { message = "An error occurred while fetching users", details = ex.Message });
            }
        }

        [HttpGet("search")]
        [Authorize(Policy = "Singer")]
        public async Task<IActionResult> Search(
            string? query = "",
            string? artist = "",
            string? decade = "",
            string? genre = "",
            string? popularity = "",
            int page = 1,
            int pageSize = 50)
        {
            _logger.LogInformation("Search: Query={Query}, Artist={Artist}, Decade={Decade}, Genre={Genre}, Popularity={Popularity}, Page={Page}, PageSize={PageSize}",
                query, artist, decade, genre, popularity, page, pageSize);

            if (pageSize > 100)
            {
                _logger.LogWarning("Search: PageSize {PageSize} exceeds maximum limit of 100", pageSize);
                return BadRequest(new { error = "PageSize cannot exceed 100" });
            }

            var songsQuery = _context.Songs.Where(s => s.Status == "active");
            _logger.LogDebug("Initial song count after status filter: {Count}", await songsQuery.CountAsync());

            // Apply filters
            if (!string.IsNullOrEmpty(artist))
            {
                songsQuery = songsQuery.Where(s => EF.Functions.ILike(s.Artist, artist));
                _logger.LogDebug("Song count after artist filter ({Artist}): {Count}", artist, await songsQuery.CountAsync());
            }
            else if (!string.IsNullOrEmpty(query) && query.ToLower() != "all")
            {
                songsQuery = songsQuery.Where(s => EF.Functions.ILike(s.Title, $"%{query}%") ||
                                                  EF.Functions.ILike(s.Artist, $"%{query}%"));
                _logger.LogDebug("Song count after query filter ({Query}): {Count}", query, await songsQuery.CountAsync());
            }

            if (!string.IsNullOrEmpty(decade))
            {
                songsQuery = songsQuery.Where(s => s.Decade != null && EF.Functions.ILike(s.Decade, decade));
                _logger.LogDebug("Song count after decade filter ({Decade}): {Count}", decade, await songsQuery.CountAsync());
            }

            if (!string.IsNullOrEmpty(genre))
            {
                songsQuery = songsQuery.Where(s => s.Genre != null && EF.Functions.ILike(s.Genre, genre));
                _logger.LogDebug("Song count after genre filter ({Genre}): {Count}", genre, await songsQuery.CountAsync());
            }

            if (!string.IsNullOrEmpty(popularity) && popularity != "popularity")
            {
                var range = popularity.Split('=');
                if (range.Length == 2)
                {
                    var bounds = range[1].Split('-');
                    if (bounds.Length == 2 && int.TryParse(bounds[0], out int min) && int.TryParse(bounds[1], out int max))
                    {
                        songsQuery = songsQuery.Where(s => s.Popularity.HasValue && s.Popularity.Value >= min && s.Popularity.Value <= max);
                        _logger.LogDebug("Song count after popularity filter ({Min}-{Max}): {Count}", min, max, await songsQuery.CountAsync());
                    }
                }
            }

            // Apply sorting for popularity if specified
            if (popularity == "popularity")
            {
                songsQuery = songsQuery.OrderByDescending(s => s.Popularity.GetValueOrDefault(0));
            }
            else
            {
                songsQuery = songsQuery.OrderBy(s => s.Title);
            }

            var totalSongs = await songsQuery.CountAsync();
            var songs = await songsQuery
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            _logger.LogInformation("Search: Found {TotalSongs} songs, returning {SongCount} for page {Page}", totalSongs, songs.Count, page);

            return Ok(new
            {
                totalSongs,
                songs,
                currentPage = page,
                pageSize,
                totalPages = (int)Math.Ceiling(totalSongs / (double)pageSize)
            });
        }

        [HttpGet("artists")]
        [Authorize(Policy = "Singer")]
        public async Task<IActionResult> GetArtists()
        {
            _logger.LogInformation("GetArtists: Received request to fetch unique artists");

            try
            {
                _logger.LogDebug("GetArtists: Verifying database context");
                if (_context == null)
                {
                    _logger.LogError("GetArtists: Database context is null");
                    return StatusCode(500, new { error = "Database context is not initialized" });
                }

                _logger.LogDebug("GetArtists: Querying Songs table for active songs");
                var songsQuery = _context.Songs.Where(s => s.Status == "active");
                _logger.LogDebug("GetArtists: Selecting distinct artists");
                var artists = await songsQuery
                    .Select(s => s.Artist)
                    .Distinct()
                    .OrderBy(a => a)
                    .ToListAsync();

                _logger.LogInformation("GetArtists: Successfully fetched {ArtistCount} unique artists", artists.Count);
                return Ok(artists);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "GetArtists: Exception occurred while fetching artists");
                return StatusCode(500, new { error = "Failed to retrieve artists: " + ex.Message });
            }
        }

        [HttpGet("genres")]
        [Authorize(Policy = "Singer")]
        public async Task<IActionResult> GetGenres()
        {
            _logger.LogInformation("GetGenres: Received request to fetch unique genres");

            try
            {
                _logger.LogDebug("GetGenres: Verifying database context");
                if (_context == null)
                {
                    _logger.LogError("GetGenres: Database context is null");
                    return StatusCode(500, new { error = "Database context is not initialized" });
                }

                _logger.LogDebug("GetGenres: Querying Songs table for active songs with non-null genres");
                var songsQuery = _context.Songs.Where(s => s.Status == "active" && s.Genre != null);
                _logger.LogDebug("GetGenres: Selecting distinct genres");
                var genres = await songsQuery
                    .Select(s => s.Genre)
                    .Distinct()
                    .OrderBy(g => g)
                    .ToListAsync();

                _logger.LogInformation("GetGenres: Successfully fetched {GenreCount} unique genres", genres.Count);
                return Ok(genres);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "GetGenres: Exception occurred while fetching genres");
                return StatusCode(500, new { error = "Failed to retrieve genres: " + ex.Message });
            }
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

                var songs = new List<object>();
                foreach (var track in data.Tracks.Items)
                {
                    var song = new
                    {
                        id = track.Id,
                        title = track.Name,
                        artist = string.Join(", ", track.Artists.Select(a => a.Name)),
                        popularity = 0,
                        genre = "Unknown",
                        bpm = 0,
                        danceability = 0,
                        energy = 0,
                        decade = "Unknown" // Default value, updated later
                    };

                    string decade = "Unknown";
                    string genre = "Unknown";
                    var trackResponse = await client.GetAsync($"https://api.spotify.com/v1/tracks/{track.Id}");
                    if (trackResponse.IsSuccessStatusCode)
                    {
                        var trackJson = await trackResponse.Content.ReadAsStringAsync();
                        var trackDetails = JsonSerializer.Deserialize<SpotifyTrackDetails>(trackJson);
                        if (trackDetails != null)
                        {
                            // Try track-level release_date first
                            string? releaseDate = null;
                            if (!string.IsNullOrEmpty(trackDetails.ReleaseDate))
                            {
                                releaseDate = trackDetails.ReleaseDate;
                            }
                            // Fall back to album-level release_date
                            else if (trackDetails.Album != null && !string.IsNullOrEmpty(trackDetails.Album.ReleaseDate))
                            {
                                releaseDate = trackDetails.Album.ReleaseDate;
                            }

                            if (!string.IsNullOrEmpty(releaseDate))
                            {
                                var yearStr = releaseDate.Split('-')[0];
                                if (int.TryParse(yearStr, out int year))
                                {
                                    decade = $"{year - (year % 10)}s";
                                }
                            }

                            // Update song with decade and track-level data
                            song = new
                            {
                                id = song.id,
                                title = song.title,
                                artist = song.artist,
                                popularity = trackDetails.Popularity,
                                genre = song.genre,
                                bpm = song.bpm,
                                danceability = song.danceability,
                                energy = song.energy,
                                decade = decade
                            };

                            // Fetch genre from track's primary artist
                            if (trackDetails.Artists != null && trackDetails.Artists.Any() == true)
                            {
#pragma warning disable CS8602 // Suppress false positive warning
                                var artistId = trackDetails.Artists[0].Id;
#pragma warning restore CS8602
                                var artistResponse = await client.GetAsync($"https://api.spotify.com/v1/artists/{artistId}");
                                if (artistResponse.IsSuccessStatusCode)
                                {
                                    var artistJson = await artistResponse.Content.ReadAsStringAsync();
                                    var artistDetails = JsonSerializer.Deserialize<SpotifyArtistDetails>(artistJson);
                                    if (artistDetails?.Genres.Any() == true)
                                    {
                                        genre = CapitalizeGenre(artistDetails.Genres.First());
                                        _logger.LogDebug("SpotifySearch: Found genre '{Genre}' from track's primary artist for track '{TrackId}'", genre, track.Id);
                                    }
                                    else
                                    {
                                        _logger.LogDebug("SpotifySearch: No genres found for track's primary artist for track '{TrackId}'", track.Id);
                                    }
                                }
                            }

                            // If genre is still "Unknown", try the album's artists
                            if (genre == "Unknown" && trackDetails.Album != null && trackDetails.Album.Id != null)
                            {
                                var albumResponse = await client.GetAsync($"https://api.spotify.com/v1/albums/{trackDetails.Album.Id}");
                                if (albumResponse.IsSuccessStatusCode)
                                {
                                    var albumJson = await albumResponse.Content.ReadAsStringAsync();
                                    var albumDetails = JsonSerializer.Deserialize<SpotifyAlbumDetails>(albumJson);
                                    if (albumDetails != null && albumDetails.Artists != null && albumDetails.Artists.Any() == true)
                                    {
#pragma warning disable CS8602 // Suppress false positive warning
                                        foreach (var albumArtist in albumDetails.Artists)
#pragma warning restore CS8602
                                        {
                                            var artistResponse = await client.GetAsync($"https://api.spotify.com/v1/artists/{albumArtist.Id}");
                                            if (artistResponse.IsSuccessStatusCode)
                                            {
                                                var artistJson = await artistResponse.Content.ReadAsStringAsync();
                                                var artistDetails = JsonSerializer.Deserialize<SpotifyArtistDetails>(artistJson);
                                                if (artistDetails?.Genres.Any() == true)
                                                {
                                                    genre = CapitalizeGenre(artistDetails.Genres.First());
                                                    _logger.LogDebug("SpotifySearch: Found genre '{Genre}' from album's artist '{ArtistId}' for track '{TrackId}'", genre, albumArtist.Id, track.Id);
                                                    break; // Use the first genre found
                                                }
                                            }
                                        }
                                    }
                                    else
                                    {
                                        _logger.LogDebug("SpotifySearch: No artists found for album '{AlbumId}' for track '{TrackId}'", trackDetails.Album.Id, track.Id);
                                    }
                                }
                            }
                        }
                    }

                    // Update song with the final genre
                    song = new
                    {
                        id = song.id,
                        title = song.title,
                        artist = song.artist,
                        popularity = song.popularity,
                        genre = genre,
                        bpm = song.bpm,
                        danceability = song.danceability,
                        energy = song.energy,
                        decade = song.decade
                    };
                    songs.Add(song);
                }

                _logger.LogInformation("SpotifySearch: Found {SongCount} songs for query '{Query}'", songs.Count, query);
                return Ok(new { songs });
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

                // If SpotifyId is provided, fetch the decade and genre from Spotify
                if (!string.IsNullOrEmpty(song.SpotifyId))
                {
                    var client = _httpClientFactory.CreateClient();
                    var token = await GetSpotifyToken(client);
                    client.DefaultRequestHeaders.Clear();
                    client.DefaultRequestHeaders.Add("Authorization", $"Bearer {token}");

                    var trackResponse = await client.GetAsync($"https://api.spotify.com/v1/tracks/{song.SpotifyId}");
                    if (trackResponse.IsSuccessStatusCode)
                    {
                        var trackJson = await trackResponse.Content.ReadAsStringAsync();
                        var trackDetails = JsonSerializer.Deserialize<SpotifyTrackDetails>(trackJson);
                        if (trackDetails != null)
                        {
                            // Try track-level release_date first
                            string? releaseDate = null;
                            if (!string.IsNullOrEmpty(trackDetails.ReleaseDate))
                            {
                                releaseDate = trackDetails.ReleaseDate;
                            }
                            // Fall back to album-level release_date
                            else if (trackDetails.Album != null && !string.IsNullOrEmpty(trackDetails.Album.ReleaseDate))
                            {
                                releaseDate = trackDetails.Album.ReleaseDate;
                            }

                            if (!string.IsNullOrEmpty(releaseDate))
                            {
                                var yearStr = releaseDate.Split('-')[0];
                                if (int.TryParse(yearStr, out int year))
                                {
                                    song.Decade = $"{year - (year % 10)}s";
                                }
                            }

                            // Fetch genre, starting with the track's primary artist
                            string genre = "Unknown";
                            if (trackDetails.Artists != null && trackDetails.Artists.Any() == true)
                            {
#pragma warning disable CS8602
                                var artistId = trackDetails.Artists[0].Id;
#pragma warning restore CS8602
                                var artistResponse = await client.GetAsync($"https://api.spotify.com/v1/artists/{artistId}");
                                if (artistResponse.IsSuccessStatusCode)
                                {
                                    var artistJson = await artistResponse.Content.ReadAsStringAsync();
                                    var artistDetails = JsonSerializer.Deserialize<SpotifyArtistDetails>(artistJson);
                                    if (artistDetails?.Genres.Any() == true)
                                    {
                                        genre = CapitalizeGenre(artistDetails.Genres.First());
                                        _logger.LogDebug("RequestSong: Found genre '{Genre}' from track's primary artist for track '{TrackId}'", genre, song.SpotifyId);
                                    }
                                }
                            }

                            // If genre is still "Unknown", try the album's artists
                            if (genre == "Unknown" && trackDetails.Album != null && trackDetails.Album.Id != null)
                            {
                                var albumResponse = await client.GetAsync($"https://api.spotify.com/v1/albums/{trackDetails.Album.Id}");
                                if (albumResponse.IsSuccessStatusCode)
                                {
                                    var albumJson = await albumResponse.Content.ReadAsStringAsync();
                                    var albumDetails = JsonSerializer.Deserialize<SpotifyAlbumDetails>(albumJson);
                                    if (albumDetails != null && albumDetails.Artists != null && albumDetails.Artists.Any() == true)
                                    {
#pragma warning disable CS8602 // Suppress false positive warning
                                        foreach (var albumArtist in albumDetails.Artists)
#pragma warning restore CS8602
                                        {
                                            var artistResponse = await client.GetAsync($"https://api.spotify.com/v1/artists/{albumArtist.Id}");
                                            if (artistResponse.IsSuccessStatusCode)
                                            {
                                                var artistJson = await artistResponse.Content.ReadAsStringAsync();
                                                var artistDetails = JsonSerializer.Deserialize<SpotifyArtistDetails>(artistJson);
                                                if (artistDetails?.Genres.Any() == true)
                                                {
                                                    genre = CapitalizeGenre(artistDetails.Genres.First());
                                                    _logger.LogDebug("RequestSong: Found genre '{Genre}' from album's artist '{ArtistId}' for track '{TrackId}'", genre, albumArtist.Id, song.SpotifyId);
                                                    break; // Use the first genre found
                                                }
                                            }
                                        }
                                    }
                                }
                            }

                            song.Genre = genre;
                        }
                    }
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

        [HttpGet("favorites")]
        [Authorize(Policy = "Singer")]
        public async Task<IActionResult> GetFavorites()
        {
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userId))
            {
                _logger.LogWarning("GetFavorites: User identity not found in token");
                return Unauthorized();
            }
            var favoriteSongIds = await _context.FavoriteSongs
                .Where(fs => fs.SingerId == userId)
                .Select(fs => fs.SongId)
                .ToListAsync();
            var songs = await _context.Songs
                .Where(s => favoriteSongIds.Contains(s.Id))
                .ToListAsync();
            _logger.LogInformation("GetFavorites: Found {SongCount} favorite songs for UserId={UserId}", songs.Count, userId);
            return Ok(songs);
        }

        [HttpPost("favorites")]
        [Authorize(Policy = "Singer")]
        public async Task<IActionResult> AddFavorite([FromBody] AddFavoriteRequest request)
        {
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userId))
            {
                _logger.LogWarning("AddFavorite: User identity not found in token");
                return Unauthorized();
            }
            var song = await _context.Songs.FindAsync(request.SongId);
            if (song == null)
            {
                _logger.LogWarning("AddFavorite: Song not found - SongId={SongId}", request.SongId);
                return NotFound("Song not found");
            }
            var existingFavorite = await _context.FavoriteSongs
                .FirstOrDefaultAsync(fs => fs.SingerId == userId && fs.SongId == request.SongId);
            if (existingFavorite != null)
            {
                _logger.LogWarning("AddFavorite: Song already in favorites - SongId={SongId}, UserId={UserId}", request.SongId, userId);
                return BadRequest("Song already in favorites");
            }
            var favorite = new FavoriteSong
            {
                SingerId = userId,
                SongId = request.SongId
            };
            _context.FavoriteSongs.Add(favorite);
            await _context.SaveChangesAsync();
            _logger.LogInformation("AddFavorite: Added song to favorites - SongId={SongId}, UserId={UserId}", request.SongId, userId);
            return Ok(new { success = true });
        }

        [HttpDelete("favorites/{songId}")]
        [Authorize(Policy = "Singer")]
        public async Task<IActionResult> RemoveFavorite(int songId)
        {
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userId))
            {
                _logger.LogWarning("RemoveFavorite: User identity not found in token");
                return Unauthorized();
            }
            var favorite = await _context.FavoriteSongs
                .FirstOrDefaultAsync(fs => fs.SingerId == userId && fs.SongId == songId);
            if (favorite == null)
            {
                _logger.LogWarning("RemoveFavorite: Favorite not found - SongId={SongId}, UserId={UserId}", songId, userId);
                return NotFound("Favorite not found");
            }
            _context.FavoriteSongs.Remove(favorite);
            await _context.SaveChangesAsync();
            _logger.LogInformation("RemoveFavorite: Removed song from favorites - SongId={SongId}, UserId={UserId}", songId, userId);
            return Ok(new { success = true });
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

        // Helper method to capitalize the first letter of each word in a genre
        private string CapitalizeGenre(string genre)
        {
            if (string.IsNullOrEmpty(genre) || genre == "Unknown")
            {
                return genre;
            }

            // Use TextInfo.ToTitleCase to capitalize the first letter of each word
            TextInfo textInfo = CultureInfo.InvariantCulture.TextInfo;
            return textInfo.ToTitleCase(genre.ToLower());
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
            [JsonPropertyName("album")]
            public SpotifyAlbum? Album { get; set; }
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

            [JsonPropertyName("release_date")]
            public string? ReleaseDate { get; set; }

            [JsonPropertyName("album")]
            public SpotifyAlbum? Album { get; set; }

            [JsonPropertyName("artists")]
            public List<SpotifyArtist>? Artists { get; set; }
        }

        public class SpotifyAlbum
        {
            [JsonPropertyName("id")]
            public string? Id { get; set; }

            [JsonPropertyName("release_date")]
            public string? ReleaseDate { get; set; }
        }

        public class SpotifyAlbumDetails
        {
            [JsonPropertyName("artists")]
            public List<SpotifyArtist>? Artists { get; set; }
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

        public class AddFavoriteRequest
        {
            public int SongId { get; set; }
        }
    }
}
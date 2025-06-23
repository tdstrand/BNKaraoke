using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System.Text.Json;
using System.Threading.Tasks;
using BNKaraoke.Api.Data;
using BNKaraoke.Api.Dtos;
using BNKaraoke.Api.Models;
using System.Linq;
using System.Collections.Generic;
using System.Security.Claims;

namespace BNKaraoke.Api.Controllers
{
    [Route("api/eventqueue")]
    [ApiController]
    public class EventQueueController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<EventQueueController> _logger;

        public EventQueueController(ApplicationDbContext context, ILogger<EventQueueController> logger)
        {
            _context = context;
            _logger = logger;
            _logger.LogInformation("EventQueueController instantiated");
        }

        [HttpGet("{eventId}")]
        [Authorize]
        public async Task<IActionResult> GetQueue(int eventId)
        {
            try
            {
                _logger.LogInformation("Fetching event queue for EventId: {EventId}", eventId);
                var eventEntity = await _context.Events.FirstOrDefaultAsync(e => e.EventId == eventId);
                if (eventEntity == null)
                {
                    _logger.LogWarning("Event not found with EventId: {EventId}", eventId);
                    return NotFound("Event not found");
                }

                var queueEntries = await _context.EventQueues
                    .Where(eq => eq.EventId == eventId)
                    .Include(eq => eq.Song) // Include Song navigation property
                    .ToListAsync();

                var requestorUserNames = queueEntries
                    .Select(eq => eq.RequestorUserName)
                    .Where(userName => userName != null)
                    .Distinct()
                    .ToList();
                var allUsers = await _context.Users
                    .OfType<ApplicationUser>()
                    .Where(u => requestorUserNames.Contains(u.UserName!))
                    .ToListAsync();
                var users = allUsers.ToDictionary(u => u.UserName!, u => u);

                var singerUserNames = new HashSet<string>();
                foreach (var eq in queueEntries)
                {
                    try
                    {
                        var singers = JsonSerializer.Deserialize<string[]>(eq.Singers) ?? Array.Empty<string>();
                        foreach (var singer in singers)
                        {
                            if (singer != "AllSing" && singer != "TheBoys" && singer != "TheGirls" && singer != null)
                            {
                                singerUserNames.Add(singer);
                            }
                        }
                    }
                    catch (JsonException ex)
                    {
                        _logger.LogWarning("Failed to deserialize Singers for QueueId {QueueId}: {Message}", eq.QueueId, ex.Message);
                    }
                }

                var singerUserNamesList = singerUserNames.ToList();
                var singerUsersList = allUsers
                    .Where(u => u.UserName != null && singerUserNamesList.Contains(u.UserName))
                    .ToList();
                var singerUsers = singerUsersList.ToDictionary(u => u.UserName!, u => u);

                var userIds = users.Values.Select(u => u.Id)
                    .Concat(singerUsers.Values.Select(u => u.Id))
                    .Distinct()
                    .ToList();
                var allAttendances = await _context.EventAttendances
                    .Where(ea => ea.EventId == eventId)
                    .ToListAsync();
                var attendancesList = allAttendances
                    .Where(ea => userIds.Contains(ea.RequestorId))
                    .ToList();
                var attendances = attendancesList.ToDictionary(ea => ea.RequestorId, ea => ea);

                var queueDtos = new List<EventQueueDto>();
                foreach (var eq in queueEntries)
                {
                    if (string.IsNullOrEmpty(eq.RequestorUserName) || !users.TryGetValue(eq.RequestorUserName, out var requestor))
                    {
                        _logger.LogWarning("Requestor not found with UserName: {UserName} for QueueId {QueueId}", eq.RequestorUserName, eq.QueueId);
                        continue;
                    }

                    attendances.TryGetValue(requestor.Id, out var attendance);

                    var singersList = new List<string>();
                    try
                    {
                        var singersArray = JsonSerializer.Deserialize<string[]>(eq.Singers);
                        if (singersArray != null)
                        {
                            singersList.AddRange(singersArray);
                        }
                    }
                    catch (JsonException ex)
                    {
                        _logger.LogWarning("Failed to deserialize Singers for QueueId {QueueId}: {Message}", eq.QueueId, ex.Message);
                    }

                    bool anySingerOnBreak = false;
                    foreach (var singer in singersList)
                    {
                        if (singer == "AllSing" || singer == "TheBoys" || singer == "TheGirls")
                        {
                            continue;
                        }

                        if (singer != null && singerUsers.TryGetValue(singer, out var singerUser) &&
                            attendances.TryGetValue(singerUser.Id, out var singerAttendance) &&
                            singerAttendance.IsOnBreak)
                        {
                            anySingerOnBreak = true;
                            break;
                        }
                    }

                    var queueDto = new EventQueueDto
                    {
                        QueueId = eq.QueueId,
                        EventId = eq.EventId,
                        SongId = eq.SongId,
                        Song = new BNKaraoke.Api.Dtos.Song
                        {
                            Id = eq.SongId,
                            Title = eq.Song?.Title ?? "Unknown Song",
                            Artist = eq.Song?.Artist ?? "Unknown Artist"
                        },
                        RequestorUserName = eq.RequestorUserName,
                        RequestorDisplayName = requestor != null ? $"{requestor.FirstName} {requestor.LastName}".Trim() : eq.RequestorUserName ?? "Unknown Requestor",
                        Singers = singersList,
                        Position = eq.Position,
                        Status = ComputeSongStatus(eq, anySingerOnBreak),
                        IsActive = eq.IsActive,
                        WasSkipped = eq.WasSkipped,
                        IsCurrentlyPlaying = eq.IsCurrentlyPlaying,
                        SungAt = eq.SungAt,
                        IsOnBreak = attendance != null ? attendance.IsOnBreak : false
                    };

                    queueDtos.Add(queueDto);
                }

                var sortedQueueDtos = queueDtos.OrderBy(eq => eq.Position).ToList();

                _logger.LogInformation("Successfully fetched {Count} queue entries for EventId: {EventId}", sortedQueueDtos.Count, eventId);
                return Ok(sortedQueueDtos);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching event queue for EventId: {EventId}: {Message}", eventId, ex.Message);
                return StatusCode(500, new { message = "An error occurred while fetching the event queue", details = ex.Message });
            }
        }

        [HttpPost("{eventId}")]
        [Authorize]
        public async Task<IActionResult> AddToQueue(int eventId, [FromBody] EventQueueCreateDto queueDto)
        {
            try
            {
                _logger.LogInformation("Adding song to queue for EventId: {EventId}, SongId: {SongId}, RequestorUserName: {RequestorUserName}", eventId, queueDto.SongId, queueDto.RequestorUserName);
                if (!ModelState.IsValid)
                {
                    _logger.LogWarning("Invalid model state for AddToQueue: {Errors}", string.Join("; ", ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage)));
                    return BadRequest(ModelState);
                }

                var eventEntity = await _context.Events.FindAsync(eventId);
                if (eventEntity == null)
                {
                    _logger.LogWarning("Event not found with EventId: {EventId}", eventId);
                    return NotFound("Event not found");
                }

                if (eventEntity.IsCanceled || eventEntity.Visibility != "Visible")
                {
                    _logger.LogWarning("Cannot add to queue for EventId {EventId}: Event is canceled or hidden", eventId);
                    return BadRequest("Cannot add to queue for a canceled or hidden event");
                }

                var song = await _context.Songs.FindAsync(queueDto.SongId);
                if (song == null)
                {
                    _logger.LogWarning("Song not found with SongId: {SongId}", queueDto.SongId);
                    return BadRequest("Song not found");
                }

                if (string.IsNullOrEmpty(queueDto.RequestorUserName))
                {
                    _logger.LogWarning("RequestorUserName is null or empty");
                    return BadRequest("RequestorUserName cannot be null or empty");
                }

                var requestor = await _context.Users
                    .OfType<ApplicationUser>()
                    .FirstOrDefaultAsync(u => u.UserName == queueDto.RequestorUserName);
                if (requestor == null)
                {
                    _logger.LogWarning("Requestor not found with UserName: {UserName}", queueDto.RequestorUserName);
                    return BadRequest("Requestor not found with UserName: " + queueDto.RequestorUserName);
                }

                var exists = await _context.EventQueues.AnyAsync(q =>
                    q.EventId == eventId &&
                    q.RequestorUserName == queueDto.RequestorUserName &&
                    q.SongId == queueDto.SongId);
                if (exists)
                {
                    _logger.LogWarning("Song {SongId} already in queue for EventId {EventId} by RequestorUserName {UserName}", queueDto.SongId, eventId, queueDto.RequestorUserName);
                    return BadRequest("Song already in queue.");
                }

                var requestedCount = await _context.EventQueues
                    .CountAsync(eq => eq.EventId == eventId && eq.RequestorUserName == queueDto.RequestorUserName);
                if (requestedCount >= eventEntity.RequestLimit)
                {
                    _logger.LogWarning("Requestor with UserName {UserName} has reached the request limit of {RequestLimit} for EventId {EventId}", queueDto.RequestorUserName, eventEntity.RequestLimit, eventId);
                    return BadRequest($"You have reached the event's request limit of {eventEntity.RequestLimit} songs.");
                }

                if (eventEntity.Status != "Upcoming")
                {
                    var attendance = await _context.EventAttendances
                        .FirstOrDefaultAsync(ea => ea.EventId == eventId && ea.RequestorId == requestor.Id);
                    if (attendance == null || !attendance.IsCheckedIn)
                    {
                        _logger.LogWarning("Requestor with UserName {UserName} must be checked in to add to queue for EventId {EventId} with status {Status}", queueDto.RequestorUserName, eventId, eventEntity.Status);
                        return BadRequest("Requestor must be checked in to add to the queue for a non-upcoming event");
                    }
                }

                var maxPosition = await _context.EventQueues
                    .Where(eq => eq.EventId == eventId)
                    .MaxAsync(eq => (int?)eq.Position) ?? 0;

                var userName = requestor.UserName ?? string.Empty; // Ensure non-null
                var newQueueEntry = new EventQueue
                {
                    EventId = eventId,
                    SongId = queueDto.SongId,
                    RequestorUserName = userName,
                    Singers = JsonSerializer.Serialize(new[] { userName }),
                    Position = maxPosition + 1,
                    Status = eventEntity.Status,
                    IsActive = eventEntity.Status == "Live",
                    WasSkipped = false,
                    IsCurrentlyPlaying = false,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow,
                    IsOnBreak = false
                };

                _context.EventQueues.Add(newQueueEntry);
                await _context.SaveChangesAsync();

                var singersList = new List<string>();
                try
                {
                    var singersArray = JsonSerializer.Deserialize<string[]>(newQueueEntry.Singers) ?? Array.Empty<string>();
                    singersList.AddRange(singersArray);
                }
                catch (JsonException ex)
                {
                    _logger.LogWarning("Failed to deserialize Singers for QueueId {QueueId}: {Message}", newQueueEntry.QueueId, ex.Message);
                }

                var queueEntryDto = new EventQueueDto
                {
                    QueueId = newQueueEntry.QueueId,
                    EventId = newQueueEntry.EventId,
                    SongId = newQueueEntry.SongId,
                    Song = new BNKaraoke.Api.Dtos.Song
                    {
                        Id = song.Id,
                        Title = song.Title ?? "Unknown Song",
                        Artist = song.Artist ?? "Unknown Artist"
                    },
                    RequestorUserName = newQueueEntry.RequestorUserName,
                    RequestorDisplayName = $"{requestor.FirstName} {requestor.LastName}".Trim() ?? newQueueEntry.RequestorUserName,
                    Singers = singersList,
                    Position = newQueueEntry.Position,
                    Status = ComputeSongStatus(newQueueEntry, false),
                    IsActive = newQueueEntry.IsActive,
                    WasSkipped = newQueueEntry.WasSkipped,
                    IsCurrentlyPlaying = newQueueEntry.IsCurrentlyPlaying,
                    SungAt = newQueueEntry.SungAt,
                    IsOnBreak = false
                };

                _logger.LogInformation("Successfully added song to queue for EventId {EventId}, QueueId: {QueueId}", eventId, newQueueEntry.QueueId);
                return CreatedAtAction(nameof(GetQueue), new { eventId, queueId = newQueueEntry.QueueId }, queueEntryDto);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error adding song to queue for EventId {EventId}: {Message}", eventId, ex.Message);
                return StatusCode(500, new { message = "An error occurred while adding to the queue", details = ex.Message });
            }
        }

        [HttpPost("{eventId}/{queueId}/skip")]
        [Authorize(Roles = "DJ")]
        public async Task<IActionResult> SkipSong(int eventId, int queueId)
        {
            try
            {
                _logger.LogInformation("Skipping song with QueueId {QueueId} for EventId: {EventId}", queueId, eventId);
                var eventEntity = await _context.Events.FindAsync(eventId);
                if (eventEntity == null)
                {
                    _logger.LogWarning("Event not found with EventId: {EventId}", eventId);
                    return NotFound("Event not found");
                }

                var queueEntry = await _context.EventQueues
                    .FirstOrDefaultAsync(eq => eq.EventId == eventId && eq.QueueId == queueId);
                if (queueEntry == null)
                {
                    _logger.LogWarning("Queue entry not found with QueueId {QueueId} for EventId {EventId}", queueId, eventId);
                    return NotFound("Queue entry not found");
                }

                var requestor = await _context.Users
                    .OfType<ApplicationUser>()
                    .FirstOrDefaultAsync(u => u.UserName == queueEntry.RequestorUserName);
                if (requestor == null)
                {
                    _logger.LogWarning("Requestor not found with UserName: {UserName}", queueEntry.RequestorUserName);
                    return BadRequest("Requestor not found");
                }

                var attendance = await _context.EventAttendances
                    .FirstOrDefaultAsync(ea => ea.EventId == eventId && ea.RequestorId == requestor.Id);
                if (attendance == null || !attendance.IsOnBreak)
                {
                    _logger.LogWarning("Requestor with UserName {UserName} must be on break to skip song for EventId {EventId}", queueEntry.RequestorUserName, eventId);
                    return BadRequest("Requestor must be on break to skip their song");
                }

                queueEntry.WasSkipped = true;
                queueEntry.UpdatedAt = DateTime.UtcNow;

                await _context.SaveChangesAsync();
                _logger.LogInformation("Successfully skipped song with QueueId {QueueId} for EventId {EventId}", queueId, eventId);
                return Ok();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error skipping song with QueueId {QueueId} for EventId {EventId}: {Message}", queueId, eventId, ex.Message);
                return StatusCode(500, new { message = "An error occurred while skipping the song", details = ex.Message });
            }
        }

        [HttpPut("{eventId}/reorder")]
        [Authorize]
        public async Task<IActionResult> ReorderQueue(int eventId, [FromBody] ReorderQueueRequest request)
        {
            try
            {
                _logger.LogInformation("Reordering queue for EventId: {EventId}", eventId);
                if (!ModelState.IsValid)
                {
                    _logger.LogWarning("Invalid model state for ReorderQueue: {Errors}", string.Join("; ", ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage)));
                    return BadRequest(ModelState);
                }

                var eventEntity = await _context.Events.FindAsync(eventId);
                if (eventEntity == null)
                {
                    _logger.LogWarning("Event not found with EventId: {EventId}", eventId);
                    return NotFound("Event not found");
                }

                var userName = User.FindFirst(ClaimTypes.Name)?.Value;
                if (string.IsNullOrEmpty(userName))
                {
                    _logger.LogWarning("User identity not found in token");
                    return Unauthorized("User identity not found in token");
                }

                var userQueueEntries = await _context.EventQueues
                    .FromSqlRaw(
                        @"SELECT * FROM public.""EventQueues""
                          WHERE ""EventId"" = {0} AND (""RequestorUserName"" = {1} OR ""Singers"" @> {2}::jsonb)",
                        eventId, userName, $"[{userName}]"
                    )
                    .ToListAsync();

                var requestQueueIds = request.NewOrder.Select(o => o.QueueId).ToList();
                var userQueueIds = userQueueEntries.Select(eq => eq.QueueId).ToList();

                if (requestQueueIds.Count != userQueueIds.Count || !requestQueueIds.All(qid => userQueueIds.Contains(qid)))
                {
                    _logger.LogWarning("Invalid reorder request: Queue IDs do not match user's queue entries for EventId {EventId}", eventId);
                    return BadRequest("Invalid reorder request: Queue IDs do not match user's queue entries");
                }

                var allQueueEntries = await _context.EventQueues
                    .Where(eq => eq.EventId == eventId)
                    .OrderBy(eq => eq.Position)
                    .ToListAsync();

                var positionMapping = allQueueEntries.ToDictionary(eq => eq.QueueId, eq => eq.Position);

                for (int i = 0; i < request.NewOrder.Count; i++)
                {
                    var queueId = request.NewOrder[i].QueueId;
                    var newPosition = request.NewOrder[i].Position;
                    positionMapping[queueId] = newPosition;
                }

                var sortedPositions = positionMapping.OrderBy(kv => kv.Value).ToList();
                for (int i = 0; i < sortedPositions.Count; i++)
                {
                    var queueId = sortedPositions[i].Key;
                    var queueEntry = allQueueEntries.First(eq => eq.QueueId == queueId);
                    queueEntry.Position = i + 1;
                    queueEntry.UpdatedAt = DateTime.UtcNow;
                }

                await _context.SaveChangesAsync();
                _logger.LogInformation("Successfully reordered queue for EventId {EventId}", eventId);
                return Ok();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error reordering queue for EventId {EventId}: {Message}", eventId, ex.Message);
                return StatusCode(500, new { message = "An error occurred while reordering the queue", details = ex.Message });
            }
        }

        [HttpPost("{eventId}/{queueId}/singers")]
        [Authorize]
        public async Task<IActionResult> UpdateSingers(int eventId, int queueId, [FromBody] UpdateSingersRequest request)
        {
            try
            {
                _logger.LogInformation("Updating singers for QueueId {QueueId} in EventId: {EventId}", queueId, eventId);
                if (!ModelState.IsValid)
                {
                    _logger.LogWarning("Invalid model state for UpdateSingers: {Errors}", string.Join("; ", ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage)));
                    return BadRequest(ModelState);
                }

                var eventEntity = await _context.Events.FindAsync(eventId);
                if (eventEntity == null)
                {
                    _logger.LogWarning("Event not found with EventId: {EventId}", eventId);
                    return NotFound("Event not found");
                }

                var queueEntry = await _context.EventQueues
                    .FirstOrDefaultAsync(eq => eq.EventId == eventId && eq.QueueId == queueId);
                if (queueEntry == null)
                {
                    _logger.LogWarning("Queue entry not found with QueueId {QueueId} for EventId {EventId}", queueId, eventId);
                    return NotFound("Queue entry not found");
                }

                if (request.Singers == null || !request.Singers.Any())
                {
                    _logger.LogWarning("Singers list cannot be empty for QueueId {QueueId}", queueId);
                    return BadRequest("At least one singer or special group is required");
                }

                var specialGroups = new List<string> { "AllSing", "TheBoys", "TheGirls" };
                var hasSpecialGroup = request.Singers.Any(s => specialGroups.Contains(s));
                var individualSingers = request.Singers.Where(s => !specialGroups.Contains(s)).ToList();

                if (hasSpecialGroup)
                {
                    if (request.Singers.Count > 1)
                    {
                        _logger.LogWarning("Special group selected with additional singers for QueueId {QueueId}", queueId);
                        return BadRequest("Special groups cannot be combined with individual singers");
                    }
                }
                else
                {
                    if (individualSingers.Count > 7)
                    {
                        _logger.LogWarning("Too many individual singers ({Count}) for QueueId {QueueId}", individualSingers.Count, queueId);
                        return BadRequest("Cannot have more than 7 individual singers");
                    }

                    foreach (var singer in individualSingers)
                    {
                        if (string.IsNullOrEmpty(singer))
                        {
                            _logger.LogWarning("Singer UserName is null or empty for QueueId {QueueId}", queueId);
                            return BadRequest("Singer UserName cannot be null or empty");
                        }

                        var singerUser = await _context.Users
                            .OfType<ApplicationUser>()
                            .FirstOrDefaultAsync(u => u.UserName == singer);
                        if (singerUser == null)
                        {
                            _logger.LogWarning("Singer not found with UserName: {UserName} for QueueId: {QueueId}", singer, queueId);
                            return BadRequest($"Singer not found: {singer}");
                        }
                    }
                }

                queueEntry.Singers = JsonSerializer.Serialize(request.Singers);
                queueEntry.UpdatedAt = DateTime.UtcNow;

                await _context.SaveChangesAsync();
                _logger.LogInformation("Successfully updated singers for QueueId {QueueId} in EventId {EventId}", queueId, eventId);
                return Ok();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating singers for QueueId {QueueId} in EventId {EventId}: {Message}", queueId, eventId, ex.Message);
                return StatusCode(500, new { message = "An error occurred while updating singers", details = ex.Message });
            }
        }

        [HttpDelete("clear")]
        [Authorize]
        public async Task<IActionResult> ClearUserQueue()
        {
            try
            {
                var userName = User.FindFirst(ClaimTypes.Name)?.Value;
                if (string.IsNullOrEmpty(userName))
                {
                    _logger.LogWarning("User identity not found in token");
                    return Unauthorized("User identity not found in token");
                }

                _logger.LogInformation("Clearing queue for user with UserName: {UserName}", userName);
                var userQueueItems = await _context.EventQueues
                    .Where(eq => eq.RequestorUserName == userName)
                    .ToListAsync();

                _context.EventQueues.RemoveRange(userQueueItems);
                await _context.SaveChangesAsync();

                _logger.LogInformation("Successfully cleared queue for user with UserName: {UserName}", userName);
                return Ok(new { success = true, message = "User queue cleared successfully." });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error clearing user queue for UserName {UserName}: {Message}", User.FindFirst(ClaimTypes.Name)?.Value ?? "Unknown", ex.Message);
                return StatusCode(500, new { success = false, message = "An error occurred while clearing the queue.", details = ex.Message });
            }
        }

        private string ComputeSongStatus(EventQueue queueEntry, bool anySingerOnBreak)
        {
            if (queueEntry.SungAt != null)
            {
                return "Completed";
            }
            if (queueEntry.IsCurrentlyPlaying)
            {
                return "Now Singing";
            }
            if (anySingerOnBreak)
            {
                return "Waiting on singers";
            }
            return "Pending";
        }
    }
}
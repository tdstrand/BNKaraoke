using BNKaraoke.Api.Data;
using BNKaraoke.Api.Dtos;
using BNKaraoke.Api.Hubs;
using BNKaraoke.Api.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Security.Claims;
using System.Text.Json;
using System.Threading.Tasks;
using System.Transactions;

namespace BNKaraoke.Api.Controllers
{
    [Route("api/dj")]
    [ApiController]
    [Authorize]
    public class DJController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<DJController> _logger;
        private readonly IHubContext<KaraokeDJHub> _hubContext;
        private readonly IHttpClientFactory _httpClientFactory;

        public DJController(
            ApplicationDbContext context,
            ILogger<DJController> logger,
            IHubContext<KaraokeDJHub> hubContext,
            IHttpClientFactory httpClientFactory)
        {
            _context = context;
            _logger = logger;
            _hubContext = hubContext;
            _httpClientFactory = httpClientFactory;
        }

        [HttpGet("events/{eventId}/queue")]
        public async Task<IActionResult> GetEventQueue(int eventId)
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
                    .Include(eq => eq.Song)
                    .ToListAsync();

                var requestorUserNames = queueEntries
                    .Select(eq => eq.RequestorUserName)
                    .Where(userName => userName != null)
                    .Distinct()
                    .ToList();
                var allUsers = await _context.Users
                    .Where(u => u.UserName != null && requestorUserNames.Contains(u.UserName!))
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

                var singerUsers = await _context.Users
                    .Where(u => u.UserName != null && singerUserNames.Contains(u.UserName!))
                    .ToDictionaryAsync(u => u.UserName!, u => u);

                var userIds = users.Values.Select(u => u.Id)
                    .Concat(singerUsers.Values.Select(u => u.Id))
                    .Distinct()
                    .ToList();
                var attendances = await _context.EventAttendances
                    .Where(ea => ea.EventId == eventId && userIds.Contains(ea.RequestorId))
                    .ToDictionaryAsync(ea => ea.RequestorId, ea => ea);

                var queueDtos = new List<DJQueueEntryDto>();
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
                        singersList.AddRange(JsonSerializer.Deserialize<string[]>(eq.Singers) ?? Array.Empty<string>());
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

                    var queueDto = new DJQueueEntryDto
                    {
                        QueueId = eq.QueueId,
                        EventId = eq.EventId,
                        SongId = eq.SongId,
                        SongTitle = eq.Song?.Title,
                        SongArtist = eq.Song?.Artist,
                        RequestorDisplayName = $"{requestor.FirstName} {requestor.LastName}".Trim(),
                        VideoLength = "0:00",
                        Position = eq.Position,
                        Status = ComputeSongStatus(eq, anySingerOnBreak),
                        RequestorUserName = eq.RequestorUserName,
                        Singers = singersList,
                        IsActive = eq.IsActive,
                        WasSkipped = eq.WasSkipped,
                        IsCurrentlyPlaying = eq.IsCurrentlyPlaying,
                        SungAt = eq.SungAt,
                        Genre = eq.Song?.Genre,
                        Decade = eq.Song?.Decade,
                        YouTubeUrl = eq.Song?.YouTubeUrl,
                        IsVideoCached = false,
                        IsOnBreak = attendance?.IsOnBreak ?? false
                    };

                    queueDtos.Add(queueDto);
                }

                var sortedQueueDtos = queueDtos.OrderBy(eq => eq.Position).ToList();

                _logger.LogInformation("Successfully fetched {Count} queue entries for EventId: {EventId}", eventId, sortedQueueDtos.Count);
                return Ok(sortedQueueDtos);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching event queue for EventId: {EventId}: {Message}", eventId);
                return StatusCode(500, new { message = "An error occurred while retrieving the event queue", details = ex.Message });
            }
        }

        [HttpGet("events/{eventId}/singers")]
        public async Task<IActionResult> GetSingers(int eventId)
        {
            try
            {
                _logger.LogInformation("Fetching singers for event {EventId}", eventId);
                var eventEntity = await _context.Events.FindAsync(eventId);
                if (eventEntity == null)
                {
                    _logger.LogWarning("Event not found with EventId: {EventId}", eventId);
                    return NotFound("Event not found");
                }

                var timeoutSetting = await _context.ApiSettings
                    .FirstOrDefaultAsync(s => s.SettingKey == "ActivityTimeoutMinutes");
                int timeoutMinutes = 30; // Default
                if (timeoutSetting != null && int.TryParse(timeoutSetting.SettingValue, out var parsedTimeout))
                {
                    timeoutMinutes = parsedTimeout;
                }

                var attendances = await _context.EventAttendances
                    .Where(ea => ea.EventId == eventId)
                    .Include(ea => ea.Requestor)
                    .ToListAsync();

                var singerDtos = new List<DJSingerDto>();
                foreach (var attendance in attendances)
                {
                    if (attendance.Requestor == null)
                    {
                        _logger.LogWarning("Requestor not found for AttendanceId: {AttendanceId}", attendance.AttendanceId);
                        continue;
                    }

                    bool isLoggedIn = attendance.IsCheckedIn &&
                        (attendance.Requestor.LastActivity == null ||
                         attendance.Requestor.LastActivity >= DateTime.UtcNow.AddMinutes(-timeoutMinutes));

                    var singerDto = new DJSingerDto
                    {
                        UserId = attendance.Requestor.UserName,
                        DisplayName = $"{attendance.Requestor.FirstName} {attendance.Requestor.LastName}".Trim(),
                        IsLoggedIn = isLoggedIn,
                        IsJoined = attendance.IsCheckedIn,
                        IsOnBreak = attendance.IsOnBreak
                    };

                    singerDtos.Add(singerDto);
                }

                var sortedSingerDtos = singerDtos
                    .OrderByDescending(s => s.IsJoined)
                    .ThenBy(s => s.IsOnBreak)
                    .ToList();

                _logger.LogInformation("Successfully fetched {Count} singers for EventId: {EventId}", sortedSingerDtos.Count, eventId);
                return Ok(sortedSingerDtos);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching singers for EventId: {EventId}: {Message}", eventId);
                return StatusCode(500, new { message = "An error occurred while retrieving singers", details = ex.Message });
            }
        }

        [HttpPut("{eventId}/queue/reorder")]
        [Authorize(Roles = "Karaoke DJ")]
        public async Task<IActionResult> ReorderDjQueue(int eventId, [FromBody] List<int> queueIds)
        {
            try
            {
                _logger.LogInformation("Reordering DJ queue for EventId: {EventId} with QueueIds: {QueueIds}", eventId, string.Join(",", queueIds));
                var eventEntity = await _context.Events.FindAsync(eventId);
                if (eventEntity == null)
                {
                    _logger.LogWarning("Event not found with EventId: {EventId}", eventId);
                    return NotFound("Event not found");
                }

                var queueEntries = await _context.EventQueues
                    .Where(eq => eq.EventId == eventId && eq.SungAt == null && !eq.IsCurrentlyPlaying && eq.Status != "Archived")
                    .AsNoTracking()
                    .ToListAsync();

                if (!queueEntries.Any())
                {
                    _logger.LogInformation("No upcoming queue entries for EventId: {EventId}", eventId);
                    return Ok();
                }

                var queueEntryIds = queueEntries.Select(eq => eq.QueueId).ToHashSet();
                var invalidIds = queueIds.Where(id => !queueEntryIds.Contains(id)).ToList();
                if (invalidIds.Any())
                {
                    _logger.LogWarning("Invalid QueueIds provided for EventId: {EventId}: {InvalidIds}", eventId, string.Join(", ", invalidIds));
                    return BadRequest($"Invalid QueueIds: [{string.Join(", ", invalidIds)}]");
                }

                if (queueIds.Count != queueEntries.Count || queueIds.Distinct().Count() != queueIds.Count)
                {
                    _logger.LogWarning("Invalid reorder request: Queue IDs do not match upcoming queue or contain duplicates for EventId {EventId}", eventId);
                    return BadRequest("Invalid reorder request: Queue IDs must include all upcoming songs without duplicates");
                }

                _logger.LogInformation("Positions before reorder for EventId {EventId}: {Positions}", eventId, string.Join(", ", queueEntries.OrderBy(e => e.Position).Select(e => $"{e.QueueId}:{e.Position}")));
                var originalTimestamps = queueEntries.ToDictionary(eq => eq.QueueId, eq => eq.UpdatedAt);

                using (var scope = new TransactionScope(TransactionScopeOption.Required, new TransactionOptions { IsolationLevel = IsolationLevel.Serializable }, TransactionScopeAsyncFlowOption.Enabled))
                {
                    await _context.Database.ExecuteSqlRawAsync(
                        "UPDATE public.\"EventQueues\" SET \"Position\" = 0 WHERE \"EventId\" = {0} AND \"SungAt\" IS NULL AND \"IsCurrentlyPlaying\" = FALSE AND \"Status\" != 'Archived'",
                        eventId);

                    foreach (var queueId in queueIds)
                    {
                        var dbEntry = await _context.EventQueues
                            .FirstOrDefaultAsync(eq => eq.QueueId == queueId && eq.EventId == eventId);
                        if (dbEntry == null)
                        {
                            _logger.LogWarning("Queue entry not found for QueueId: {QueueId} during reorder for EventId: {EventId}", queueId, eventId);
                            throw new InvalidOperationException($"Queue entry not found for QueueId: {queueId}");
                        }
                        dbEntry.Position = queueIds.IndexOf(queueId) + 1;
                        dbEntry.UpdatedAt = DateTime.UtcNow;
                    }

                    using (var checkContext = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>()
                        .UseNpgsql(_context.Database.GetConnectionString()).Options))
                    {
                        var currentEntries = await checkContext.EventQueues
                            .Where(eq => eq.EventId == eventId && queueIds.Contains(eq.QueueId))
                            .ToListAsync();
                        if (currentEntries.Any(eq => originalTimestamps[eq.QueueId] != eq.UpdatedAt))
                        {
                            _logger.LogWarning("Concurrency conflict detected for EventId: {EventId}", eventId);
                            throw new InvalidOperationException("Queue modified by another user");
                        }
                    }

                    await _context.SaveChangesAsync();
                    scope.Complete();
                }

                var updatedEntries = await _context.EventQueues
                    .Where(eq => eq.EventId == eventId && queueIds.Contains(eq.QueueId))
                    .OrderBy(eq => eq.Position)
                    .ToListAsync();
                _logger.LogInformation("Positions after reorder for EventId {EventId}: {Positions}", eventId, string.Join(", ", updatedEntries.Select(e => $"{e.QueueId}:{e.Position}")));

                await _hubContext.Clients.Group($"Event_{eventId}").SendAsync("QueueUpdated", 0, "Reordered");

                _logger.LogInformation("Successfully reordered DJ queue for EventId: {EventId}", eventId);
                return Ok();
            }
            catch (InvalidOperationException ex) when (ex.Message.Contains("Queue modified"))
            {
                _logger.LogWarning("Concurrency conflict during reorder for EventId: {EventId}: {Message}", eventId, ex.Message);
                return StatusCode(409, "Queue modified by another user, please retry");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error reordering DJ queue for EventId {EventId}: {Message}", eventId);
                return StatusCode(500, new { message = "An error occurred while reordering the queue", details = ex.Message });
            }
        }

        private string ComputeSongStatus(EventQueue queueEntry, bool anySingerOnBreak)
        {
            if (queueEntry.WasSkipped)
                return "Skipped";
            if (queueEntry.IsCurrentlyPlaying)
                return "Playing";
            if (queueEntry.SungAt != null)
                return "Sung";
            if (anySingerOnBreak)
                return "OnBreak";
            return queueEntry.Status;
        }
    }
}
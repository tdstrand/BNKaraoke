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
        private static readonly Dictionary<int, string> _holdReasons = new();

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

        [HttpPost("{eventId}/attendance/check-in")]
        [Authorize(Roles = "Karaoke DJ,Singer")]
        public async Task<IActionResult> CheckIn(int eventId, [FromBody] CheckInDto request)
        {
            try
            {
                if (string.IsNullOrEmpty(request.RequestorUserName))
                {
                    _logger.LogWarning("RequestorUserName is null or empty for EventId: {EventId}", eventId);
                    return BadRequest("RequestorUserName cannot be null or empty");
                }

                _logger.LogInformation("Checking in for EventId: {EventId}, RequestorUserName: {RequestorUserName}", eventId, request.RequestorUserName);
                var eventEntity = await _context.Events.FindAsync(eventId);
                if (eventEntity == null)
                {
                    _logger.LogWarning("Event not found with EventId: {EventId}", eventId);
                    return NotFound("Event not found");
                }

                var user = await _context.Users
                    .FirstOrDefaultAsync(u => u.UserName == request.RequestorUserName);
                if (user == null)
                {
                    _logger.LogWarning("User not found with UserName: {UserName}", request.RequestorUserName);
                    return NotFound("User not found");
                }

                var singerStatus = await _context.SingerStatus
                    .FirstOrDefaultAsync(ss => ss.EventId == eventId && ss.RequestorId == user.Id);
                if (singerStatus == null)
                {
                    singerStatus = new SingerStatus
                    {
                        EventId = eventId,
                        RequestorId = user.Id,
                        IsLoggedIn = true,
                        IsJoined = true,
                        IsOnBreak = false,
                        UpdatedAt = DateTime.UtcNow
                    };
                    _context.SingerStatus.Add(singerStatus);
                }
                else
                {
                    singerStatus.IsLoggedIn = true;
                    singerStatus.IsJoined = true;
                    singerStatus.IsOnBreak = false;
                    singerStatus.UpdatedAt = DateTime.UtcNow;
                }

                var attendance = await _context.EventAttendances
                    .FirstOrDefaultAsync(ea => ea.EventId == eventId && ea.RequestorId == user.Id);
                if (attendance == null)
                {
                    attendance = new EventAttendance
                    {
                        EventId = eventId,
                        RequestorId = user.Id,
                        IsCheckedIn = true,
                        IsOnBreak = false
                    };
                    _context.EventAttendances.Add(attendance);
                }
                else
                {
                    attendance.IsCheckedIn = true;
                    attendance.IsOnBreak = false;
                    attendance.BreakStartAt = null;
                    attendance.BreakEndAt = null;
                }

                await _context.SaveChangesAsync();

                var queueEntries = await _context.EventQueues
                    .Where(eq => eq.EventId == eventId && eq.RequestorUserName == request.RequestorUserName && eq.SungAt == null)
                    .ToListAsync();
                foreach (var entry in queueEntries)
                {
                    _holdReasons.Remove(entry.QueueId);
                }

                await _hubContext.Clients.Group($"Event_{eventId}")
                    .SendAsync("SingerStatusUpdated", new
                    {
                        UserId = user.UserName,
                        EventId = eventId,
                        DisplayName = $"{user.FirstName} {user.LastName}".Trim(),
                        IsLoggedIn = true,
                        IsJoined = true,
                        IsOnBreak = false
                    });

                _logger.LogInformation("Checked in successfully for EventId: {EventId}, UserId: {UserId}", eventId, user.Id);
                return Ok(new
                {
                    message = "Checked in successfully",
                    singerStatus = new
                    {
                        UserId = user.UserName,
                        DisplayName = $"{user.FirstName} {user.LastName}".Trim(),
                        IsLoggedIn = true,
                        IsJoined = true,
                        IsOnBreak = false
                    }
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking in for EventId: {EventId}, RequestorUserName: {RequestorUserName}", eventId, request.RequestorUserName);
                return StatusCode(500, new { message = "Error checking in", details = ex.Message });
            }
        }

        [HttpPost("{eventId}/attendance/check-out")]
        [Authorize(Roles = "Karaoke DJ,Singer")]
        public async Task<IActionResult> CheckOut(int eventId, [FromBody] CheckInDto request)
        {
            try
            {
                if (string.IsNullOrEmpty(request.RequestorUserName))
                {
                    _logger.LogWarning("RequestorUserName is null or empty for EventId: {EventId}", eventId);
                    return BadRequest("RequestorUserName cannot be null or empty");
                }

                _logger.LogInformation("Checking out for EventId: {EventId}, RequestorUserName: {RequestorUserName}", eventId, request.RequestorUserName);
                var eventEntity = await _context.Events.FindAsync(eventId);
                if (eventEntity == null)
                {
                    _logger.LogWarning("Event not found with EventId: {EventId}", eventId);
                    return NotFound("Event not found");
                }

                var user = await _context.Users
                    .FirstOrDefaultAsync(u => u.UserName == request.RequestorUserName);
                if (user == null)
                {
                    _logger.LogWarning("User not found with UserName: {UserName}", request.RequestorUserName);
                    return NotFound("User not found");
                }

                var singerStatus = await _context.SingerStatus
                    .FirstOrDefaultAsync(ss => ss.EventId == eventId && ss.RequestorId == user.Id);
                if (singerStatus != null)
                {
                    singerStatus.IsLoggedIn = false;
                    singerStatus.IsJoined = false;
                    singerStatus.IsOnBreak = false;
                    singerStatus.UpdatedAt = DateTime.UtcNow;
                }

                var attendance = await _context.EventAttendances
                    .FirstOrDefaultAsync(ea => ea.EventId == eventId && ea.RequestorId == user.Id);
                if (attendance != null)
                {
                    attendance.IsCheckedIn = false;
                    attendance.IsOnBreak = false;
                    attendance.BreakStartAt = null;
                    attendance.BreakEndAt = null;
                }

                var queueEntries = await _context.EventQueues
                    .Where(eq => eq.EventId == eventId && eq.RequestorUserName == request.RequestorUserName && eq.SungAt == null)
                    .ToListAsync();
                foreach (var entry in queueEntries)
                {
                    _holdReasons[entry.QueueId] = "NotJoined";
                }

                await _context.SaveChangesAsync();

                await _hubContext.Clients.Group($"Event_{eventId}")
                    .SendAsync("SingerStatusUpdated", new
                    {
                        UserId = user.UserName,
                        EventId = eventId,
                        DisplayName = $"{user.FirstName} {user.LastName}".Trim(),
                        IsLoggedIn = false,
                        IsJoined = false,
                        IsOnBreak = false
                    });

                _logger.LogInformation("Checked out successfully for EventId: {EventId}, UserId: {UserId}", eventId, user.Id);
                return Ok(new { message = "Checked out successfully" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking out for EventId: {EventId}, RequestorUserName: {RequestorUserName}", eventId, request.RequestorUserName);
                return StatusCode(500, new { message = "Error checking out", details = ex.Message });
            }
        }

        [HttpPost("{eventId}/queue/{queueId}/play")]
        [Authorize(Roles = "Karaoke DJ")]
        public async Task<IActionResult> PlaySong(int eventId, int queueId)
        {
            try
            {
                _logger.LogInformation("Playing song for EventId: {EventId}, QueueId: {QueueId}", eventId, queueId);
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
                    _logger.LogWarning("Queue entry not found with QueueId: {QueueId} for EventId: {EventId}", queueId, eventId);
                    return NotFound("Queue entry not found");
                }

                using (var scope = new TransactionScope(TransactionScopeOption.Required, TransactionScopeAsyncFlowOption.Enabled))
                {
                    await _context.Database.ExecuteSqlRawAsync(
                        "UPDATE public.\"EventQueues\" SET \"IsCurrentlyPlaying\" = FALSE, \"UpdatedAt\" = @p0 WHERE \"EventId\" = @p1 AND \"IsCurrentlyPlaying\" = TRUE",
                        DateTime.UtcNow, eventId);

                    queueEntry.IsCurrentlyPlaying = true;
                    _holdReasons.Remove(queueId);
                    queueEntry.UpdatedAt = DateTime.UtcNow;

                    await _context.SaveChangesAsync();
                    scope.Complete();
                }

                await _hubContext.Clients.Group($"Event_{eventId}")
                    .SendAsync("QueueUpdated", queueId, "Playing", null, null);

                _logger.LogInformation("Started play for QueueId: {QueueId} for EventId: {EventId}", queueId, eventId);
                return Ok(new { message = "Song play started", QueueId = queueId });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error starting play for EventId: {EventId}, QueueId: {QueueId}", eventId, queueId);
                return StatusCode(500, new { message = "Error starting song play", details = ex.Message });
            }
        }

        [HttpPost("{eventId}/queue/{queueId}/skipped")]
        [Authorize(Roles = "Karaoke DJ")]
        public async Task<IActionResult> SkipSong(int eventId, int queueId)
        {
            try
            {
                _logger.LogInformation("Skipping song for EventId: {EventId}, QueueId: {QueueId}", eventId, queueId);
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
                    _logger.LogWarning("Queue entry not found with QueueId: {QueueId} for EventId: {EventId}", queueId, eventId);
                    return NotFound("Queue entry not found");
                }

                queueEntry.WasSkipped = true;
                queueEntry.IsCurrentlyPlaying = false;
                queueEntry.SungAt = DateTime.UtcNow;
                queueEntry.UpdatedAt = DateTime.UtcNow;

                await _context.SaveChangesAsync();

                await _hubContext.Clients.Group($"Event_{eventId}")
                    .SendAsync("QueueUpdated", queueId, "Skipped", null, null);

                _logger.LogInformation("Skipped song with QueueId: {QueueId} for EventId: {EventId}", queueId, eventId);
                return Ok(new { message = "Song skipped" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error skipping song for EventId: {EventId}, QueueId: {QueueId}", eventId, queueId);
                return StatusCode(500, new { message = "Error skipping song", details = ex.Message });
            }
        }

        [HttpPost("queue/now-playing")]
        [Authorize(Roles = "Karaoke DJ")]
        public async Task<IActionResult> SetNowPlaying([FromBody] NowPlayingDto request)
        {
            try
            {
                _logger.LogInformation("Setting now playing for QueueId: {QueueId}", request.QueueId);
                var queueEntry = await _context.EventQueues
                    .FirstOrDefaultAsync(eq => eq.QueueId == request.QueueId);
                if (queueEntry == null)
                {
                    _logger.LogWarning("Queue entry not found with QueueId: {QueueId}", request.QueueId);
                    return NotFound("Queue entry not found");
                }

                var eventEntity = await _context.Events.FindAsync(queueEntry.EventId);
                if (eventEntity == null)
                {
                    _logger.LogWarning("Event not found with EventId: {EventId}", queueEntry.EventId);
                    return NotFound("Event not found");
                }

                using (var scope = new TransactionScope(TransactionScopeOption.Required, TransactionScopeAsyncFlowOption.Enabled))
                {
                    await _context.Database.ExecuteSqlRawAsync(
                        "UPDATE public.\"EventQueues\" SET \"IsCurrentlyPlaying\" = FALSE, \"UpdatedAt\" = @p0 WHERE \"EventId\" = @p1 AND \"IsCurrentlyPlaying\" = TRUE",
                        DateTime.UtcNow, queueEntry.EventId);

                    queueEntry.IsCurrentlyPlaying = true;
                    _holdReasons.Remove(request.QueueId);
                    queueEntry.UpdatedAt = DateTime.UtcNow;

                    await _context.SaveChangesAsync();
                    scope.Complete();
                }

                await _hubContext.Clients.Group($"Event_{queueEntry.EventId}")
                    .SendAsync("QueueUpdated", queueEntry.QueueId, "Playing", null, null);

                _logger.LogInformation("Set now playing for QueueId: {QueueId}, EventId: {EventId}", request.QueueId, queueEntry.EventId);
                return Ok(new { message = "Song set as now playing", QueueId = request.QueueId });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error setting now playing for QueueId: {QueueId}", request.QueueId);
                return StatusCode(500, new { message = "Error setting now playing", details = ex.Message });
            }
        }

        [HttpPost("{eventId}/autoplay/next")]
        [Authorize(Roles = "Karaoke DJ")]
        public async Task<IActionResult> AutoplayNext(int eventId)
        {
            try
            {
                _logger.LogInformation("Selecting next song for autoplay in EventId: {EventId}", eventId);
                var eventEntity = await _context.Events.FindAsync(eventId);
                if (eventEntity == null)
                {
                    _logger.LogWarning("Event not found with EventId: {EventId}", eventId);
                    return NotFound("Event not found");
                }

                var queueEntries = await _context.EventQueues
                    .Where(eq => eq.EventId == eventId && eq.Status == "Live" && eq.SungAt == null && !eq.WasSkipped && !eq.IsCurrentlyPlaying)
                    .Include(eq => eq.Song)
                    .OrderBy(eq => eq.Position)
                    .ToListAsync();

                EventQueue? nextEntry = null;
                foreach (var entry in queueEntries)
                {
                    var singers = JsonSerializer.Deserialize<string[]>(entry.Singers) ?? Array.Empty<string>();
                    bool allSingersAvailable = true;
                    string holdReason = "None";

                    foreach (var singer in singers)
                    {
                        if (singer == "AllSing" || singer == "TheBoys" || singer == "TheGirls")
                            continue;

                        var singerUser = await _context.Users.FirstOrDefaultAsync(u => u.UserName == singer);
                        if (singerUser == null)
                        {
                            holdReason = "NotJoined";
                            allSingersAvailable = false;
                            break;
                        }

                        var singerStatus = await _context.SingerStatus
                            .FirstOrDefaultAsync(ss => ss.EventId == eventId && ss.RequestorId == singerUser.Id);
                        if (singerStatus == null || !singerStatus.IsLoggedIn || !singerStatus.IsJoined)
                        {
                            holdReason = singerStatus == null ? "NotJoined" : "NotLoggedIn";
                            allSingersAvailable = false;
                            break;
                        }
                        else if (singerStatus.IsOnBreak)
                        {
                            holdReason = "OnBreak";
                            allSingersAvailable = false;
                            break;
                        }
                    }

                    if (allSingersAvailable)
                    {
                        nextEntry = entry;
                        break;
                    }
                    else
                    {
                        entry.IsOnBreak = true;
                        entry.UpdatedAt = DateTime.UtcNow;
                        _holdReasons[entry.QueueId] = holdReason;
                        await _hubContext.Clients.Group($"Event_{eventId}")
                            .SendAsync("QueueUpdated", entry.QueueId, "OnHold", null, holdReason);
                    }
                }

                if (nextEntry == null)
                {
                    _logger.LogInformation("No eligible songs for autoplay in EventId: {EventId}", eventId);
                    return NotFound(new { message = "No eligible songs" });
                }

                using (var scope = new TransactionScope(TransactionScopeOption.Required, TransactionScopeAsyncFlowOption.Enabled))
                {
                    await _context.Database.ExecuteSqlRawAsync(
                        "UPDATE public.\"EventQueues\" SET \"IsCurrentlyPlaying\" = FALSE, \"UpdatedAt\" = @p0 WHERE \"EventId\" = @p1 AND \"IsCurrentlyPlaying\" = TRUE",
                        DateTime.UtcNow, eventId);

                    nextEntry.IsCurrentlyPlaying = true;
                    _holdReasons.Remove(nextEntry.QueueId);
                    nextEntry.UpdatedAt = DateTime.UtcNow;

                    await _context.SaveChangesAsync();
                    scope.Complete();
                }

                await _hubContext.Clients.Group($"Event_{eventId}")
                    .SendAsync("QueueUpdated", nextEntry.QueueId, "Playing", null, null);

                _logger.LogInformation("Selected next song for autoplay: QueueId: {QueueId}, EventId: {EventId}", nextEntry.QueueId, eventId);
                return Ok(new
                {
                    QueueId = nextEntry.QueueId,
                    SongId = nextEntry.SongId,
                    SongTitle = nextEntry.Song?.Title ?? string.Empty,
                    message = "Next song selected"
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error selecting next song for autoplay in EventId: {EventId}", eventId);
                return StatusCode(500, new { message = "Error selecting next song", details = ex.Message });
            }
        }

        [HttpGet("queue/unplayed")]
        public async Task<IActionResult> GetUnplayedQueue(int eventId)
        {
            try
            {
                _logger.LogInformation("Fetching unplayed queue for EventId: {EventId}", eventId);
                var eventEntity = await _context.Events.FindAsync(eventId);
                if (eventEntity == null)
                {
                    _logger.LogWarning("Event not found with EventId: {EventId}", eventId);
                    return NotFound("Event not found");
                }

                var queueEntries = await _context.EventQueues
                    .Where(eq => eq.EventId == eventId && eq.Status == "Live" && eq.SungAt == null && !eq.WasSkipped && !eq.IsCurrentlyPlaying && !eq.IsOnBreak)
                    .Include(eq => eq.Song)
                    .OrderBy(eq => eq.Position)
                    .ToListAsync();

                queueEntries = queueEntries.Where(eq => !_holdReasons.ContainsKey(eq.QueueId)).ToList();

                var queueDtos = await BuildQueueDtos(queueEntries, eventId);
                if (queueDtos.Any())
                {
                    queueDtos.First().IsUpNext = true;
                }

                _logger.LogInformation("Fetched {Count} unplayed queue entries for EventId: {EventId}", queueDtos.Count, eventId);
                return Ok(queueDtos);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching unplayed queue for EventId: {EventId}", eventId);
                return StatusCode(500, new { message = "Error retrieving unplayed queue", details = ex.Message });
            }
        }

        [HttpGet("queue/now-playing")]
        public async Task<IActionResult> GetNowPlayingQueue(int eventId)
        {
            try
            {
                _logger.LogInformation("Fetching now playing queue for EventId: {EventId}", eventId);
                var eventEntity = await _context.Events.FindAsync(eventId);
                if (eventEntity == null)
                {
                    _logger.LogWarning("Event not found with EventId: {EventId}", eventId);
                    return NotFound("Event not found");
                }

                var queueEntry = await _context.EventQueues
                    .Where(eq => eq.EventId == eventId && eq.IsCurrentlyPlaying)
                    .Include(eq => eq.Song)
                    .FirstOrDefaultAsync();

                if (queueEntry == null)
                {
                    _logger.LogInformation("No currently playing song for EventId: {EventId}", eventId);
                    return Ok(null);
                }

                var queueDtos = await BuildQueueDtos(new[] { queueEntry }, eventId);
                var queueDto = queueDtos.FirstOrDefault();

                _logger.LogInformation("Fetched now playing queue entry for EventId: {EventId}", eventId);
                return Ok(queueDto);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching now playing queue for EventId: {EventId}", eventId);
                return StatusCode(500, new { message = "Error retrieving now playing queue", details = ex.Message });
            }
        }

        [HttpGet("queue/completed")]
        public async Task<IActionResult> GetCompletedQueue(int eventId)
        {
            try
            {
                _logger.LogInformation("Fetching completed queue for EventId: {EventId}", eventId);
                var eventEntity = await _context.Events.FindAsync(eventId);
                if (eventEntity == null)
                {
                    _logger.LogWarning("Event not found with EventId: {EventId}", eventId);
                    return NotFound("Event not found");
                }

                var queueEntries = await _context.EventQueues
                    .Where(eq => eq.EventId == eventId && (eq.SungAt != null || eq.WasSkipped))
                    .Include(eq => eq.Song)
                    .OrderByDescending(eq => eq.SungAt ?? DateTime.UtcNow)
                    .ToListAsync();

                var queueDtos = await BuildQueueDtos(queueEntries, eventId);

                _logger.LogInformation("Fetched {Count} completed queue entries for EventId: {EventId}", queueDtos.Count, eventId);
                return Ok(queueDtos);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching completed queue for EventId: {EventId}", eventId);
                return StatusCode(500, new { message = "Error retrieving completed queue", details = ex.Message });
            }
        }

        [HttpGet("queue/all")]
        public async Task<IActionResult> GetAllQueue(int eventId)
        {
            try
            {
                _logger.LogInformation("Fetching all queue for EventId: {EventId}", eventId);
                var eventEntity = await _context.Events.FirstOrDefaultAsync(e => e.EventId == eventId);
                if (eventEntity == null)
                {
                    _logger.LogWarning("Event not found with EventId: {EventId}", eventId);
                    return NotFound("Event not found");
                }

                var queueEntries = await _context.EventQueues
                    .Where(eq => eq.EventId == eventId)
                    .Include(eq => eq.Song)
                    .OrderBy(eq => eq.Position)
                    .ToListAsync();

                var queueDtos = await BuildQueueDtos(queueEntries, eventId);
                if (queueDtos.Any(q => q.Status == "Unplayed"))
                {
                    queueDtos.First(q => q.Status == "Unplayed").IsUpNext = true;
                }

                _logger.LogInformation("Fetched {Count} queue entries for EventId: {EventId}", queueDtos.Count, eventId);
                return Ok(queueDtos);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching all queue for EventId: {EventId}", eventId);
                return StatusCode(500, new { message = "Error retrieving all queue", details = ex.Message });
            }
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

                var queueDtos = await BuildQueueDtos(queueEntries, eventId);
                if (queueDtos.Any(q => q.Status == "Unplayed"))
                {
                    queueDtos.First(q => q.Status == "Unplayed").IsUpNext = true;
                }

                _logger.LogInformation("Fetched {Count} queue entries for EventId: {EventId}", queueDtos.Count, eventId);
                return Ok(new { QueueEntries = queueDtos, SongsCompleted = eventEntity.SongsCompleted });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching event queue for EventId: {EventId}", eventId);
                return StatusCode(500, new { message = "Error retrieving event queue", details = ex.Message });
            }
        }

        [HttpGet("events/{eventId}/singers")]
        public async Task<IActionResult> GetSingers(int eventId, int page = 1, int size = 100)
        {
            try
            {
                _logger.LogInformation("Fetching singers for EventId: {EventId}, Page: {Page}, Size: {Size}", eventId, page, size);
                var eventEntity = await _context.Events.FindAsync(eventId);
                if (eventEntity == null)
                {
                    _logger.LogWarning("Event not found with EventId: {EventId}", eventId);
                    return NotFound("Event not found");
                }

                var timeoutSetting = await _context.ApiSettings
                    .FirstOrDefaultAsync(s => s.SettingKey == "ActivityTimeoutMinutes");
                int timeoutMinutes = timeoutSetting != null && int.TryParse(timeoutSetting.SettingValue, out var parsedTimeout) ? parsedTimeout : 30;

                var singers = await _context.SingerStatus
                    .Where(ss => ss.EventId == eventId)
                    .Join(_context.Users,
                        ss => ss.RequestorId,
                        u => u.Id,
                        (ss, u) => new
                        {
                            SingerStatus = ss,
                            User = u
                        })
                    .Where(x => x.SingerStatus.IsJoined || x.SingerStatus.IsLoggedIn)
                    .Select(x => new DJSingerDto
                    {
                        UserId = x.User.UserName ?? string.Empty,
                        DisplayName = (x.User.FirstName + " " + x.User.LastName).Trim(),
                        IsLoggedIn = x.SingerStatus.IsLoggedIn && (x.User.LastActivity == null || x.User.LastActivity >= DateTime.UtcNow.AddMinutes(-timeoutMinutes)),
                        IsJoined = x.SingerStatus.IsJoined,
                        IsOnBreak = x.SingerStatus.IsOnBreak
                    })
                    .ToListAsync();

                var totalSingers = singers.Count;
                var pagedSingers = singers
                    .OrderBy(s => s.DisplayName)
                    .Skip((page - 1) * size)
                    .Take(size)
                    .ToList();

                _logger.LogInformation("Fetched {Count} singers for EventId: {EventId}", pagedSingers.Count, eventId);
                return Ok(new { Singers = pagedSingers, Total = totalSingers, Page = page, Size = size });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching singers for EventId: {EventId}", eventId);
                return StatusCode(500, new { message = "Error retrieving singers", details = ex.Message });
            }
        }

        [HttpPut("{eventId}/queue/reorder")]
        [Authorize(Roles = "Karaoke DJ")]
        public async Task<IActionResult> ReorderDjQueue(int eventId, [FromBody] List<int> queueIds)
        {
            try
            {
                _logger.LogInformation("Reordering DJ queue for EventId: {EventId}", eventId);
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
                    return Ok(new { message = "No upcoming queue entries to reorder" });
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
                    _logger.LogWarning("Invalid reorder request for EventId: {EventId}", eventId);
                    return BadRequest("Queue IDs must include all upcoming songs without duplicates");
                }

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

                await _hubContext.Clients.Group($"Event_{eventId}").SendAsync("QueueUpdated", 0, "Reordered");

                _logger.LogInformation("Reordered DJ queue for EventId: {EventId}", eventId);
                return Ok(new { message = "Queue reordered" });
            }
            catch (InvalidOperationException ex) when (ex.Message.Contains("Queue modified"))
            {
                _logger.LogWarning("Concurrency conflict during reorder for EventId: {EventId}: {Message}", eventId, ex.Message);
                return StatusCode(409, "Queue modified by another user, please retry");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error reordering DJ queue for EventId: {EventId}", ex.Message);
                return StatusCode(500, new { message = "Error reordering queue", details = ex.Message });
            }
        }

        [HttpPost("complete")]
        [Authorize(Roles = "Karaoke DJ")]
        public async Task<IActionResult> CompleteSong([FromBody] CompleteSongDto request)
        {
            try
            {
                _logger.LogInformation("Completing song for EventId: {EventId}, QueueId: {QueueId}", request.EventId, request.QueueId);
                var eventEntity = await _context.Events.FindAsync(request.EventId);
                if (eventEntity == null)
                {
                    _logger.LogWarning("Event not found with EventId: {EventId}", request.EventId);
                    return NotFound("Event not found");
                }

                var queueEntry = await _context.EventQueues
                    .FirstOrDefaultAsync(eq => eq.EventId == request.EventId && eq.QueueId == request.QueueId);
                if (queueEntry == null)
                {
                    _logger.LogWarning("Queue entry not found with QueueId: {QueueId} for EventId: {EventId}", request.QueueId, request.EventId);
                    return NotFound("Queue entry not found");
                }

                queueEntry.IsCurrentlyPlaying = false;
                queueEntry.SungAt = DateTime.UtcNow;
                queueEntry.Status = "Live";
                queueEntry.UpdatedAt = DateTime.UtcNow;

                eventEntity.SongsCompleted++;

                await _context.SaveChangesAsync();

                await _hubContext.Clients.Group($"Event_{request.EventId}")
                    .SendAsync("QueueUpdated", request.QueueId, "Sung", null, null);

                _logger.LogInformation("Completed song with QueueId: {QueueId} for EventId: {EventId}", request.QueueId, request.EventId);
                return Ok(new { message = "Song completed" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error completing song for EventId: {EventId}, QueueId: {QueueId}", request.EventId, request.QueueId);
                return StatusCode(500, new { message = "Error completing song", details = ex.Message });
            }
        }

        [HttpPost("break")]
        [Authorize(Roles = "Karaoke DJ")]
        public async Task<IActionResult> ToggleBreak([FromBody] ToggleBreakDto request)
        {
            try
            {
                _logger.LogInformation("Toggling break for EventId: {EventId}, QueueId: {QueueId}, IsOnBreak: {IsOnBreak}", request.EventId, request.QueueId, request.IsOnBreak);
                var queueEntry = await _context.EventQueues
                    .FirstOrDefaultAsync(eq => eq.EventId == request.EventId && eq.QueueId == request.QueueId);
                if (queueEntry == null)
                {
                    _logger.LogWarning("Queue entry not found with QueueId: {QueueId} for EventId: {EventId}", request.QueueId, request.EventId);
                    return NotFound("Queue entry not found");
                }

                queueEntry.IsOnBreak = request.IsOnBreak;
                if (request.IsOnBreak)
                    _holdReasons[queueEntry.QueueId] = "OnHold";
                else
                    _holdReasons.Remove(queueEntry.QueueId);
                queueEntry.UpdatedAt = DateTime.UtcNow;

                await _context.SaveChangesAsync();

                await _hubContext.Clients.Group($"Event_{request.EventId}")
                    .SendAsync("QueueUpdated", queueEntry.QueueId, request.IsOnBreak ? "OnHold" : "Eligible", null, request.IsOnBreak ? "OnHold" : "None");

                _logger.LogInformation("Toggled break for QueueId: {QueueId} to IsOnBreak: {IsOnBreak} for EventId: {EventId}", request.QueueId, request.IsOnBreak, request.EventId);
                return Ok(new { message = "Break status updated" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error toggling break for EventId: {EventId}, QueueId: {QueueId}", request.EventId, request.QueueId);
                return StatusCode(500, new { message = "Error updating break status", details = ex.Message });
            }
        }

        [HttpPost("singer/update")]
        [Authorize(Roles = "Karaoke DJ")]
        public async Task<IActionResult> UpdateSingerStatus([FromBody] UpdateSingerStatusDto request)
        {
            try
            {
                _logger.LogInformation("Updating singer status for EventId: {EventId}, RequestorUserName: {RequestorUserName}", request.EventId, request.RequestorUserName);
                var eventEntity = await _context.Events.FindAsync(request.EventId);
                if (eventEntity == null)
                {
                    _logger.LogWarning("Event not found with EventId: {EventId}", request.EventId);
                    return NotFound("Event not found");
                }

                var user = await _context.Users
                    .FirstOrDefaultAsync(u => u.UserName == request.RequestorUserName);
                if (user == null)
                {
                    _logger.LogWarning("User not found with UserName: {UserName}", request.RequestorUserName);
                    return NotFound("User not found");
                }

                using (var scope = new TransactionScope(TransactionScopeOption.Required, TransactionScopeAsyncFlowOption.Enabled))
                {
                    var singerStatus = await _context.SingerStatus
                        .FirstOrDefaultAsync(ss => ss.EventId == request.EventId && ss.RequestorId == user.Id);
                    if (singerStatus == null)
                    {
                        singerStatus = new SingerStatus
                        {
                            EventId = request.EventId,
                            RequestorId = user.Id,
                            IsLoggedIn = request.IsLoggedIn,
                            IsJoined = request.IsJoined,
                            IsOnBreak = request.IsOnBreak,
                            UpdatedAt = DateTime.UtcNow
                        };
                        _context.SingerStatus.Add(singerStatus);
                    }
                    else
                    {
                        singerStatus.IsLoggedIn = request.IsLoggedIn;
                        singerStatus.IsJoined = request.IsJoined;
                        singerStatus.IsOnBreak = request.IsOnBreak;
                        singerStatus.UpdatedAt = DateTime.UtcNow;
                    }

                    var holdReason = "None";
                    if (!request.IsLoggedIn)
                        holdReason = "NotLoggedIn";
                    else if (!request.IsJoined)
                        holdReason = "NotJoined";
                    else if (request.IsOnBreak)
                        holdReason = "OnBreak";

                    var queueEntries = await _context.EventQueues
                        .Where(eq => eq.EventId == request.EventId && eq.RequestorUserName == request.RequestorUserName && eq.SungAt == null && eq.Status == "Live")
                        .ToListAsync();
                    foreach (var entry in queueEntries)
                    {
                        entry.IsOnBreak = request.IsOnBreak;
                        entry.UpdatedAt = DateTime.UtcNow;
                        if (holdReason != "None")
                            _holdReasons[entry.QueueId] = holdReason;
                        else
                            _holdReasons.Remove(entry.QueueId);
                    }

                    await _context.SaveChangesAsync();
                    scope.Complete();
                }

                var response = new DJSingerDto
                {
                    UserId = user.UserName ?? string.Empty,
                    DisplayName = $"{user.FirstName} {user.LastName}".Trim(),
                    IsLoggedIn = request.IsLoggedIn,
                    IsJoined = request.IsJoined,
                    IsOnBreak = request.IsOnBreak
                };

                await _hubContext.Clients.Group($"Event_{request.EventId}")
                    .SendAsync("SingerStatusUpdated", response);

                _logger.LogInformation("Updated singer status for RequestorUserName: {RequestorUserName} for EventId: {EventId}", request.RequestorUserName, request.EventId);
                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating singer status for EventId: {EventId}, RequestorUserName: {RequestorUserName}", request.EventId, request.RequestorUserName);
                return StatusCode(500, new { message = "Error updating singer status", details = ex.Message });
            }
        }

        private async Task<List<EventQueueDto>> BuildQueueDtos(IEnumerable<EventQueue> queueEntries, int eventId)
        {
            var requestorUserNames = queueEntries
                .Select(eq => eq.RequestorUserName)
                .Where(userName => !string.IsNullOrEmpty(userName))
                .Distinct()
                .ToList();
            var allUsers = await _context.Users
                .Where(u => u.UserName != null && requestorUserNames.Contains(u.UserName))
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
                        if (singer != "AllSing" && singer != "TheBoys" && singer != "TheGirls" && !string.IsNullOrEmpty(singer))
                        {
                            singerUserNames.Add(singer);
                        }
                    }
                }
                catch (JsonException ex)
                {
                    _logger.LogWarning("Failed to deserialize Singers for QueueId: {QueueId}, Error: {Message}", eq.QueueId, ex.Message);
                }
            }

            var singerUsers = await _context.Users
                .Where(u => u.UserName != null && singerUserNames.Contains(u.UserName))
                .ToDictionaryAsync(u => u.UserName!, u => u);

            var userIds = users.Values.Select(u => u.Id)
                .Concat(singerUsers.Values.Select(u => u.Id))
                .Distinct()
                .ToList();
            var attendances = await _context.EventAttendances
                .Where(ea => ea.EventId == eventId && userIds.Contains(ea.RequestorId))
                .ToDictionaryAsync(ea => ea.RequestorId, ea => ea);

            var queueDtos = new List<EventQueueDto>();
            foreach (var eq in queueEntries)
            {
                if (string.IsNullOrEmpty(eq.RequestorUserName) || !users.TryGetValue(eq.RequestorUserName, out var requestor))
                {
                    _logger.LogWarning("Requestor not found with UserName: {UserName} for QueueId: {QueueId}", eq.RequestorUserName, eq.QueueId);
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
                    _logger.LogWarning("Failed to deserialize Singers for QueueId: {QueueId}, Error: {Message}", eq.QueueId, ex.Message);
                }

                bool anySingerOnBreak = false;
                string holdReason = _holdReasons.TryGetValue(eq.QueueId, out var reason) ? reason : "None";
                foreach (var singer in singersList)
                {
                    if (singer == "AllSing" || singer == "TheBoys" || singer == "TheGirls")
                        continue;

                    if (singer != null && singerUsers.TryGetValue(singer, out var singerUser) &&
                        attendances.TryGetValue(singerUser.Id, out var singerAttendance) &&
                        singerAttendance.IsOnBreak)
                    {
                        anySingerOnBreak = true;
                        if (holdReason == "None")
                            holdReason = "OnBreak";
                        break;
                    }
                }

                var status = ComputeSongStatus(eq, anySingerOnBreak, holdReason);
                var queueDto = new EventQueueDto
                {
                    QueueId = eq.QueueId,
                    EventId = eq.EventId,
                    SongId = eq.SongId,
                    SongTitle = eq.Song?.Title ?? string.Empty,
                    SongArtist = eq.Song?.Artist ?? string.Empty,
                    RequestorUserName = eq.RequestorUserName,
                    RequestorDisplayName = $"{requestor.FirstName} {requestor.LastName}".Trim(), // Added display name
                    Singers = singersList,
                    Position = eq.Position,
                    Status = status,
                    IsActive = eq.IsActive,
                    WasSkipped = eq.WasSkipped,
                    IsCurrentlyPlaying = eq.IsCurrentlyPlaying,
                    SungAt = eq.SungAt,
                    IsOnBreak = eq.IsOnBreak,
                    HoldReason = holdReason,
                    IsUpNext = false
                };

                queueDtos.Add(queueDto);
            }

            return queueDtos.OrderBy(q => q.Position).ToList();
        }

        private string ComputeSongStatus(EventQueue queueEntry, bool anySingerOnBreak, string holdReason)
        {
            if (queueEntry.WasSkipped)
                return "Skipped";
            if (queueEntry.IsCurrentlyPlaying)
                return "Playing";
            if (queueEntry.SungAt != null)
                return "Sung";
            if (anySingerOnBreak || queueEntry.IsOnBreak || holdReason != "None")
                return "Held";
            return "Unplayed";
        }
    }

    public class CheckInDto
    {
        public string RequestorUserName { get; set; } = string.Empty;
    }

    public class CompleteSongDto
    {
        public int EventId { get; set; }
        public int QueueId { get; set; }
    }

    public class NowPlayingDto
    {
        public int QueueId { get; set; }
    }

    public class ToggleBreakDto
    {
        public int EventId { get; set; }
        public int QueueId { get; set; }
        public bool IsOnBreak { get; set; }
    }

    public class UpdateSingerStatusDto
    {
        public int EventId { get; set; }
        public string RequestorUserName { get; set; } = string.Empty;
        public bool IsLoggedIn { get; set; }
        public bool IsJoined { get; set; }
        public bool IsOnBreak { get; set; }
    }

    public class DJSingerDto
    {
        public string UserId { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        public bool IsLoggedIn { get; set; }
        public bool IsJoined { get; set; }
        public bool IsOnBreak { get; set; }
    }

    public class EventQueueDto
    {
        public int QueueId { get; set; }
        public int EventId { get; set; }
        public int SongId { get; set; }
        public string SongTitle { get; set; } = string.Empty;
        public string SongArtist { get; set; } = string.Empty;
        public string RequestorUserName { get; set; } = string.Empty;
        public string RequestorDisplayName { get; set; } = string.Empty; // Added for UI
        public List<string> Singers { get; set; } = new List<string>();
        public int Position { get; set; }
        public string Status { get; set; } = string.Empty;
        public bool IsActive { get; set; }
        public bool WasSkipped { get; set; }
        public bool IsCurrentlyPlaying { get; set; }
        public DateTime? SungAt { get; set; }
        public bool IsOnBreak { get; set; }
        public string HoldReason { get; set; } = string.Empty;
        public bool IsUpNext { get; set; }
    }
}
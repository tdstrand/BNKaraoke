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
using System.Security.Claims;
using System.Collections.Generic;
using Microsoft.AspNetCore.SignalR;
using BNKaraoke.Api.Hubs;

namespace BNKaraoke.Api.Controllers
{
    [Route("api/events")]
    [ApiController]
    public class EventController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<EventController> _logger;
        private readonly IHubContext<SingersHub> _hubContext;

        public EventController(ApplicationDbContext context, ILogger<EventController> logger, IHubContext<SingersHub> hubContext)
        {
            _context = context;
            _logger = logger;
            _hubContext = hubContext;
            _logger.LogInformation("EventController instantiated");
        }

        [HttpGet("health")]
        [AllowAnonymous]
        public IActionResult HealthCheck()
        {
            _logger.LogInformation("Health check endpoint called");
            return Ok(new { status = "healthy", message = "EventController is running" });
        }

        [HttpGet("minimal")]
        [AllowAnonymous]
        public IActionResult MinimalTest()
        {
            _logger.LogInformation("Minimal test endpoint called");
            return Ok(new { message = "Minimal test endpoint reached" });
        }

        [HttpGet]
        [Authorize]
        public async Task<IActionResult> GetEvents()
        {
            try
            {
                _logger.LogInformation("Fetching visible events from the database");
                var events = await _context.Events
                    .Where(e => e.Visibility == "Visible" && !e.IsCanceled)
                    .Select(e => new EventDto
                    {
                        EventId = e.EventId,
                        EventCode = e.EventCode,
                        Description = e.Description,
                        Status = e.Status,
                        Visibility = e.Visibility,
                        Location = e.Location,
                        ScheduledDate = e.ScheduledDate,
                        ScheduledStartTime = e.ScheduledStartTime,
                        ScheduledEndTime = e.ScheduledEndTime,
                        KaraokeDJName = e.KaraokeDJName,
                        IsCanceled = e.IsCanceled,
                        RequestLimit = e.RequestLimit,
                        QueueCount = e.EventQueues.Count
                    })
                    .OrderBy(e => e.ScheduledDate)
                    .ToListAsync();

                _logger.LogInformation("Successfully fetched {Count} events", events.Count);
                return Ok(events);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching events: {Message}", ex.Message);
                return StatusCode(500, new { message = "An error occurred while fetching events", details = ex.Message });
            }
        }

        [HttpPost]
        [Authorize(Roles = "EventManager")]
        public async Task<IActionResult> CreateEvent([FromBody] EventCreateDto eventDto)
        {
            try
            {
                _logger.LogInformation("Creating a new event with EventCode: {EventCode}", eventDto.EventCode);
                if (!ModelState.IsValid)
                {
                    _logger.LogWarning("Invalid model state for CreateEvent: {Errors}", string.Join("; ", ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage)));
                    return BadRequest(ModelState);
                }

                if (string.IsNullOrEmpty(eventDto.EventCode))
                {
                    _logger.LogWarning("EventCode is null or empty");
                    return BadRequest("EventCode cannot be null or empty");
                }

                var newEvent = new Event
                {
                    EventCode = eventDto.EventCode,
                    Description = eventDto.Description ?? string.Empty,
                    Status = eventDto.Status ?? "Upcoming",
                    Visibility = eventDto.Visibility ?? "Visible",
                    Location = eventDto.Location ?? string.Empty,
                    ScheduledDate = eventDto.ScheduledDate, // Non-nullable, validated by ModelState
                    ScheduledStartTime = eventDto.ScheduledStartTime,
                    ScheduledEndTime = eventDto.ScheduledEndTime,
                    KaraokeDJName = eventDto.KaraokeDJName ?? string.Empty,
                    IsCanceled = eventDto.IsCanceled ?? false,
                    RequestLimit = eventDto.RequestLimit,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                };

                _context.Events.Add(newEvent);
                await _context.SaveChangesAsync();

                var eventResponse = new EventDto
                {
                    EventId = newEvent.EventId,
                    EventCode = newEvent.EventCode,
                    Description = newEvent.Description,
                    Status = newEvent.Status,
                    Visibility = newEvent.Visibility,
                    Location = newEvent.Location,
                    ScheduledDate = newEvent.ScheduledDate,
                    ScheduledStartTime = newEvent.ScheduledStartTime,
                    ScheduledEndTime = newEvent.ScheduledEndTime,
                    KaraokeDJName = newEvent.KaraokeDJName,
                    IsCanceled = newEvent.IsCanceled,
                    RequestLimit = newEvent.RequestLimit,
                    QueueCount = 0
                };

                _logger.LogInformation("Successfully created event with EventId: {EventId}", newEvent.EventId);
                return CreatedAtAction(nameof(GetEvent), new { eventId = newEvent.EventId }, eventResponse);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating event: {Message}", ex.Message);
                return StatusCode(500, new { message = "An error occurred while creating the event", details = ex.Message });
            }
        }

        [HttpPut("{eventId}")]
        [Authorize(Roles = "EventManager")]
        public async Task<IActionResult> UpdateEvent(int eventId, [FromBody] EventUpdateDto eventDto)
        {
            try
            {
                _logger.LogInformation("Updating event with EventId: {EventId}", eventId);
                if (!ModelState.IsValid)
                {
                    _logger.LogWarning("Invalid model state for UpdateEvent: {Errors}", string.Join("; ", ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage)));
                    return BadRequest(ModelState);
                }

                var existingEvent = await _context.Events.FindAsync(eventId);
                if (existingEvent == null)
                {
                    _logger.LogWarning("Event not found with EventId: {EventId}", eventId);
                    return NotFound("Event not found");
                }

                if (string.IsNullOrEmpty(eventDto.EventCode))
                {
                    _logger.LogWarning("EventCode is null or empty for EventId: {EventId}", eventId);
                    return BadRequest("EventCode cannot be null or empty");
                }

                var oldStatus = existingEvent.Status;
                existingEvent.EventCode = eventDto.EventCode;
                existingEvent.Description = eventDto.Description ?? existingEvent.Description;
                existingEvent.Status = eventDto.Status ?? existingEvent.Status;
                existingEvent.Visibility = eventDto.Visibility ?? existingEvent.Visibility;
                existingEvent.Location = eventDto.Location ?? existingEvent.Location;
                existingEvent.ScheduledDate = eventDto.ScheduledDate; // Non-nullable, validated by ModelState
                existingEvent.ScheduledStartTime = eventDto.ScheduledStartTime ?? existingEvent.ScheduledStartTime;
                existingEvent.ScheduledEndTime = eventDto.ScheduledEndTime ?? existingEvent.ScheduledEndTime;
                existingEvent.KaraokeDJName = eventDto.KaraokeDJName ?? existingEvent.KaraokeDJName;
                existingEvent.IsCanceled = eventDto.IsCanceled ?? existingEvent.IsCanceled;
                existingEvent.RequestLimit = eventDto.RequestLimit;
                existingEvent.UpdatedAt = DateTime.UtcNow;

                if (oldStatus != existingEvent.Status)
                {
                    _logger.LogInformation("Event status changed from {OldStatus} to {NewStatus}, updating queue entries for EventId: {EventId}", oldStatus, existingEvent.Status, eventId);
                    var queueEntries = await _context.EventQueues
                        .Where(eq => eq.EventId == eventId)
                        .ToListAsync();

                    foreach (var entry in queueEntries)
                    {
                        entry.Status = existingEvent.Status;
                        entry.IsActive = existingEvent.Status == "Live";
                        entry.UpdatedAt = DateTime.UtcNow;
                    }
                }

                await _context.SaveChangesAsync();

                var eventResponse = new EventDto
                {
                    EventId = existingEvent.EventId,
                    EventCode = existingEvent.EventCode,
                    Description = existingEvent.Description,
                    Status = existingEvent.Status,
                    Visibility = existingEvent.Visibility,
                    Location = existingEvent.Location,
                    ScheduledDate = existingEvent.ScheduledDate,
                    ScheduledStartTime = existingEvent.ScheduledStartTime,
                    ScheduledEndTime = existingEvent.ScheduledEndTime,
                    KaraokeDJName = existingEvent.KaraokeDJName,
                    IsCanceled = existingEvent.IsCanceled,
                    RequestLimit = existingEvent.RequestLimit,
                    QueueCount = await _context.EventQueues
                        .CountAsync(eq => eq.EventId == eventId)
                };

                _logger.LogInformation("Successfully updated event with EventId: {EventId}", eventId);
                return Ok(eventResponse);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating event with EventId {EventId}: {Message}", eventId, ex.Message);
                return StatusCode(500, new { message = "An error occurred while updating the event", details = ex.Message });
            }
        }

        [HttpGet("{eventId}")]
        [Authorize]
        public async Task<IActionResult> GetEvent(int eventId)
        {
            try
            {
                _logger.LogInformation("Fetching event with EventId: {EventId}", eventId);
                var eventEntity = await _context.Events.FindAsync(eventId);
                if (eventEntity == null)
                {
                    _logger.LogWarning("Event not found with EventId: {EventId}", eventId);
                    return NotFound("Event not found");
                }

                var eventResponse = new EventDto
                {
                    EventId = eventEntity.EventId,
                    EventCode = eventEntity.EventCode,
                    Description = eventEntity.Description,
                    Status = eventEntity.Status,
                    Visibility = eventEntity.Visibility,
                    Location = eventEntity.Location,
                    ScheduledDate = eventEntity.ScheduledDate,
                    ScheduledStartTime = eventEntity.ScheduledStartTime,
                    ScheduledEndTime = eventEntity.ScheduledEndTime,
                    KaraokeDJName = eventEntity.KaraokeDJName,
                    IsCanceled = eventEntity.IsCanceled,
                    RequestLimit = eventEntity.RequestLimit,
                    QueueCount = await _context.EventQueues
                        .CountAsync(eq => eq.EventId == eventId)
                };

                _logger.LogInformation("Successfully fetched event with EventId: {EventId}", eventId);
                return Ok(eventResponse);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching event with EventId {EventId}: {Message}", eventId, ex.Message);
                return StatusCode(500, new { message = "An error occurred while fetching the event", details = ex.Message });
            }
        }

        [HttpPost("{eventId}/queue")]
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

                var newQueueEntry = new EventQueue
                {
                    EventId = eventId,
                    SongId = queueDto.SongId,
                    RequestorUserName = requestor.UserName,
                    Singers = JsonSerializer.Serialize(new[] { requestor.UserName }),
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
                    var singersArray = JsonSerializer.Deserialize<string[]>(newQueueEntry.Singers);
                    if (singersArray != null)
                    {
                        singersList.AddRange(singersArray);
                    }
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
                    RequestorUserName = newQueueEntry.RequestorUserName,
                    Singers = singersList,
                    Position = newQueueEntry.Position,
                    Status = ComputeSongStatus(newQueueEntry, false),
                    IsActive = newQueueEntry.IsActive,
                    WasSkipped = newQueueEntry.WasSkipped,
                    IsCurrentlyPlaying = newQueueEntry.IsCurrentlyPlaying,
                    SungAt = newQueueEntry.SungAt,
                    IsOnBreak = newQueueEntry.IsOnBreak
                };

                await _hubContext.Clients.Group($"Event_{eventId}").SendAsync("QueueUpdated", newQueueEntry.QueueId, "Added");
                _logger.LogInformation("Successfully added song to queue for EventId {EventId}, QueueId: {QueueId}", eventId, newQueueEntry.QueueId);
                return CreatedAtAction(nameof(GetEventQueue), new { eventId, queueId = newQueueEntry.QueueId }, queueEntryDto);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error adding song to queue for EventId {EventId}: {Message}", eventId, ex.Message);
                return StatusCode(500, new { message = "An error occurred while adding to the queue", details = ex.Message });
            }
        }

        [HttpGet("{eventId}/queue")]
        [Authorize]
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
                    .ToListAsync();

                var requestorUserNames = queueEntries
                    .Select(eq => eq.RequestorUserName)
                    .Where(userName => userName != null)
                    .Distinct()
                    .ToList();
                var allUsers = await _context.Users
                    .OfType<ApplicationUser>()
                    .ToListAsync();
                var usersList = allUsers
                    .Where(u => u.UserName != null && requestorUserNames.Contains(u.UserName))
                    .ToList();
                var users = usersList.ToDictionary(u => u.UserName!, u => u);

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

                    var queueDto = new EventQueueDto
                    {
                        QueueId = eq.QueueId,
                        EventId = eq.EventId,
                        SongId = eq.SongId,
                        RequestorUserName = eq.RequestorUserName,
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

        [HttpGet("{eventId}/attendance/status")]
        [Authorize]
        public async Task<IActionResult> GetAttendanceStatus(int eventId)
        {
            try
            {
                _logger.LogInformation("Fetching attendance status for EventId: {EventId}", eventId);

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

                var requestor = await _context.Users
                    .OfType<ApplicationUser>()
                    .FirstOrDefaultAsync(u => u.UserName == userName);
                if (requestor == null)
                {
                    _logger.LogWarning("Requestor not found with UserName: {UserName}", userName);
                    return BadRequest("Requestor not found");
                }

                var attendance = await _context.EventAttendances
                    .FirstOrDefaultAsync(ea => ea.EventId == eventId && ea.RequestorId == requestor.Id);

                if (attendance == null)
                {
                    return Ok(new { isCheckedIn = false, isOnBreak = false });
                }

                return Ok(new
                {
                    isCheckedIn = attendance.IsCheckedIn,
                    isOnBreak = attendance.IsOnBreak,
                    breakStartAt = attendance.BreakStartAt,
                    breakEndAt = attendance.BreakEndAt
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching attendance status for EventId {EventId}: {Message}", eventId, ex.Message);
                return StatusCode(500, new { message = "An error occurred while fetching attendance status", details = ex.Message });
            }
        }

        [HttpPost("{eventId}/attendance/check-in")]
        [Authorize]
        public async Task<IActionResult> CheckIn(int eventId, [FromBody] AttendanceActionDto actionDto)
        {
            try
            {
                _logger.LogInformation("Checking in requestor with UserName {UserName} for EventId: {EventId}", actionDto.RequestorId, eventId);
                if (!ModelState.IsValid)
                {
                    _logger.LogWarning("Invalid model state for CheckIn: {Errors}", string.Join("; ", ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage)));
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
                    _logger.LogWarning("Cannot check in to EventId {EventId}: Event is canceled or hidden", eventId);
                    return BadRequest("Cannot check in to a canceled or hidden event");
                }

                if (eventEntity.Status != "Live")
                {
                    _logger.LogWarning("Cannot check in to EventId {EventId}: Event is not live (Status: {Status})", eventId, eventEntity.Status);
                    return BadRequest("Can only check in to live events");
                }

                if (string.IsNullOrEmpty(actionDto.RequestorId))
                {
                    _logger.LogWarning("RequestorId is null or empty");
                    return BadRequest("RequestorId cannot be null or empty");
                }

                var requestor = await _context.Users
                    .OfType<ApplicationUser>()
                    .FirstOrDefaultAsync(u => u.UserName == actionDto.RequestorId);
                if (requestor == null)
                {
                    _logger.LogWarning("Requestor not found with UserName: {UserName}", actionDto.RequestorId);
                    return BadRequest("Requestor not found");
                }

                var attendance = await _context.EventAttendances
                    .FirstOrDefaultAsync(ea => ea.EventId == eventId && ea.RequestorId == requestor.Id);

                if (attendance == null)
                {
                    attendance = new EventAttendance
                    {
                        EventId = eventId,
                        RequestorId = requestor.Id,
                        IsCheckedIn = true,
                        IsOnBreak = false
                    };
                    _context.EventAttendances.Add(attendance);
                }
                else
                {
                    if (attendance.IsCheckedIn)
                    {
                        _logger.LogWarning("Requestor with UserName {UserName} is already checked in for EventId {EventId}", actionDto.RequestorId, eventId);
                        return BadRequest("Requestor is already checked in");
                    }

                    attendance.IsCheckedIn = true;
                    attendance.IsOnBreak = false;
                    attendance.BreakStartAt = null;
                    attendance.BreakEndAt = null;
                }

                await _context.SaveChangesAsync();

                var attendanceHistory = new EventAttendanceHistory
                {
                    EventId = eventId,
                    RequestorId = requestor.Id,
                    Action = "CheckIn",
                    ActionTimestamp = DateTime.UtcNow,
                    AttendanceId = attendance.AttendanceId
                };
                _context.EventAttendanceHistories.Add(attendanceHistory);

                var queueEntries = await _context.EventQueues
                    .Where(eq => eq.EventId == eventId && eq.RequestorUserName == requestor.UserName)
                    .ToListAsync();

                foreach (var entry in queueEntries)
                {
                    entry.IsActive = true;
                    entry.Status = "Live";
                    entry.UpdatedAt = DateTime.UtcNow;
                }

                await _context.SaveChangesAsync();
                await _hubContext.Clients.Group($"Event_{eventId}").SendAsync("UpdateSingerStatus", requestor.Id, eventId, $"{requestor.FirstName} {requestor.LastName}", true, true, false);

                _logger.LogInformation("Successfully checked in requestor with UserName {UserName} for EventId {EventId}", actionDto.RequestorId, eventId);
                return Ok(new { message = "Check-in successful" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking in requestor with UserName {UserName} for EventId {EventId}: {Message}", actionDto.RequestorId, eventId, ex.Message);
                return StatusCode(500, new { message = "An error occurred while checking in", details = ex.Message });
            }
        }

        [HttpPost("{eventId}/attendance/check-out")]
        [Authorize]
        public async Task<IActionResult> CheckOut(int eventId, [FromBody] AttendanceActionDto actionDto)
        {
            try
            {
                _logger.LogInformation("Checking out requestor with UserName {UserName} for EventId: {EventId}", actionDto.RequestorId, eventId);
                if (!ModelState.IsValid)
                {
                    _logger.LogWarning("Invalid model state for CheckOut: {Errors}", string.Join("; ", ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage)));
                    return BadRequest(ModelState);
                }

                var eventEntity = await _context.Events.FindAsync(eventId);
                if (eventEntity == null)
                {
                    _logger.LogWarning("Event not found with EventId: {EventId}", eventId);
                    return NotFound("Event not found");
                }

                if (string.IsNullOrEmpty(actionDto.RequestorId))
                {
                    _logger.LogWarning("RequestorId is null or empty");
                    return BadRequest("RequestorId cannot be null or empty");
                }

                var requestor = await _context.Users
                    .OfType<ApplicationUser>()
                    .FirstOrDefaultAsync(u => u.UserName == actionDto.RequestorId);
                if (requestor == null)
                {
                    _logger.LogWarning("Requestor not found with UserName: {UserName}", actionDto.RequestorId);
                    return BadRequest("Requestor not found");
                }

                var attendance = await _context.EventAttendances
                    .FirstOrDefaultAsync(ea => ea.EventId == eventId && ea.RequestorId == requestor.Id);
                if (attendance == null || !attendance.IsCheckedIn)
                {
                    _logger.LogWarning("Requestor with UserName {UserName} is not checked in for EventId {EventId}", actionDto.RequestorId, eventId);
                    return BadRequest("Requestor is not checked in");
                }

                attendance.IsCheckedIn = false;
                attendance.IsOnBreak = false;
                attendance.BreakStartAt = null;
                attendance.BreakEndAt = null;

                await _context.SaveChangesAsync();

                var attendanceHistory = new EventAttendanceHistory
                {
                    EventId = eventId,
                    RequestorId = requestor.Id,
                    Action = "CheckOut",
                    ActionTimestamp = DateTime.UtcNow,
                    AttendanceId = attendance.AttendanceId
                };
                _context.EventAttendanceHistories.Add(attendanceHistory);

                var queueEntries = await _context.EventQueues
                    .Where(eq => eq.EventId == eventId && eq.RequestorUserName == requestor.UserName)
                    .ToListAsync();

                foreach (var entry in queueEntries)
                {
                    entry.IsActive = false;
                    entry.UpdatedAt = DateTime.UtcNow;
                }

                await _context.SaveChangesAsync();
                await _hubContext.Clients.Group($"Event_{eventId}").SendAsync("UpdateSingerStatus", requestor.Id, eventId, $"{requestor.FirstName} {requestor.LastName}", true, false, false);

                _logger.LogInformation("Successfully checked out requestor with UserName {UserName} for EventId {EventId}", actionDto.RequestorId, eventId);
                return Ok(new { message = "Check-out successful" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking out requestor with UserName {UserName} for EventId {EventId}: {Message}", actionDto.RequestorId, eventId, ex.Message);
                return StatusCode(500, new { message = "An error occurred while checking out", details = ex.Message });
            }
        }

        [HttpPost("{eventId}/attendance/break/start")]
        [Authorize]
        public async Task<IActionResult> StartBreak(int eventId, [FromBody] AttendanceActionDto actionDto)
        {
            try
            {
                _logger.LogInformation("Starting break for requestor with UserName {UserName} for EventId: {EventId}", actionDto.RequestorId, eventId);
                if (!ModelState.IsValid)
                {
                    _logger.LogWarning("Invalid model state for StartBreak: {Errors}", string.Join("; ", ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage)));
                    return BadRequest(ModelState);
                }

                var eventEntity = await _context.Events.FindAsync(eventId);
                if (eventEntity == null)
                {
                    _logger.LogWarning("Event not found with EventId: {EventId}", eventId);
                    return NotFound("Event not found");
                }

                if (string.IsNullOrEmpty(actionDto.RequestorId))
                {
                    _logger.LogWarning("RequestorId is null or empty");
                    return BadRequest("RequestorId cannot be null or empty");
                }

                var requestor = await _context.Users
                    .OfType<ApplicationUser>()
                    .FirstOrDefaultAsync(u => u.UserName == actionDto.RequestorId);
                if (requestor == null)
                {
                    _logger.LogWarning("Requestor not found with UserName: {UserName}", actionDto.RequestorId);
                    return BadRequest("Requestor not found");
                }

                var attendance = await _context.EventAttendances
                    .FirstOrDefaultAsync(ea => ea.EventId == eventId && ea.RequestorId == requestor.Id);
                if (attendance == null || !attendance.IsCheckedIn)
                {
                    _logger.LogWarning("Requestor with UserName {UserName} must be checked in to take a break for EventId {EventId}", actionDto.RequestorId, eventId);
                    return BadRequest("Requestor must be checked in to take a break");
                }

                if (attendance.IsOnBreak)
                {
                    _logger.LogWarning("Requestor with UserName {UserName} is already on break for EventId {EventId}", actionDto.RequestorId, eventId);
                    return BadRequest("Requestor is already on break");
                }

                attendance.IsOnBreak = true;
                attendance.BreakStartAt = DateTime.UtcNow;
                attendance.BreakEndAt = null;

                await _context.SaveChangesAsync();
                await _hubContext.Clients.Group($"Event_{eventId}").SendAsync("UpdateSingerStatus", requestor.Id, eventId, $"{requestor.FirstName} {requestor.LastName}", true, true, true);

                _logger.LogInformation("Successfully started break for requestor with UserName {UserName} for EventId {EventId}", actionDto.RequestorId, eventId);
                return Ok();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error starting break for requestor with UserName {UserName} for EventId {EventId}: {Message}", actionDto.RequestorId, eventId, ex.Message);
                return StatusCode(500, new { message = "An error occurred while starting the break", details = ex.Message });
            }
        }

        [HttpPost("{eventId}/attendance/break/end")]
        [Authorize]
        public async Task<IActionResult> EndBreak(int eventId, [FromBody] AttendanceActionDto actionDto)
        {
            try
            {
                _logger.LogInformation("Ending break for requestor with UserName {UserName} for EventId: {EventId}", actionDto.RequestorId, eventId);
                if (!ModelState.IsValid)
                {
                    _logger.LogWarning("Invalid model state for EndBreak: {Errors}", string.Join("; ", ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage)));
                    return BadRequest(ModelState);
                }

                var eventEntity = await _context.Events.FindAsync(eventId);
                if (eventEntity == null)
                {
                    _logger.LogWarning("Event not found with EventId: {EventId}", eventId);
                    return NotFound("Event not found");
                }

                if (string.IsNullOrEmpty(actionDto.RequestorId))
                {
                    _logger.LogWarning("RequestorId is null or empty");
                    return BadRequest("RequestorId cannot be null or empty");
                }

                var requestor = await _context.Users
                    .OfType<ApplicationUser>()
                    .FirstOrDefaultAsync(u => u.UserName == actionDto.RequestorId);
                if (requestor == null)
                {
                    _logger.LogWarning("Requestor not found with UserName: {UserName}", actionDto.RequestorId);
                    return BadRequest("Requestor not found");
                }

                var attendance = await _context.EventAttendances
                    .FirstOrDefaultAsync(ea => ea.EventId == eventId && ea.RequestorId == requestor.Id);
                if (attendance == null || !attendance.IsCheckedIn || !attendance.IsOnBreak)
                {
                    _logger.LogWarning("Requestor with UserName {UserName} must be checked in and on break to end break for EventId {EventId}", actionDto.RequestorId, eventId);
                    return BadRequest("Requestor must be checked in and on break to end a break");
                }

                attendance.IsOnBreak = false;
                attendance.BreakEndAt = DateTime.UtcNow;

                var minPosition = await _context.EventQueues
                    .Where(eq => eq.EventId == eventId)
                    .MinAsync(eq => (int?)eq.Position) ?? 1;

                var skippedEntries = await _context.EventQueues
                    .Where(eq => eq.EventId == eventId && eq.RequestorUserName == requestor.UserName && eq.WasSkipped)
                    .OrderBy(eq => eq.QueueId)
                    .ToListAsync();

                for (int i = 0; i < skippedEntries.Count; i++)
                {
                    skippedEntries[i].Position = minPosition - (i + 1);
                    skippedEntries[i].WasSkipped = false;
                    skippedEntries[i].UpdatedAt = DateTime.UtcNow;
                }

                var otherEntries = await _context.EventQueues
                    .Where(eq => eq.EventId == eventId && (eq.RequestorUserName != requestor.UserName || !eq.WasSkipped))
                    .OrderBy(eq => eq.Position)
                    .ToListAsync();

                for (int i = 0; i < otherEntries.Count; i++)
                {
                    otherEntries[i].Position = minPosition + i;
                    otherEntries[i].UpdatedAt = DateTime.UtcNow;
                }

                await _context.SaveChangesAsync();
                await _hubContext.Clients.Group($"Event_{eventId}").SendAsync("UpdateSingerStatus", requestor.Id, eventId, $"{requestor.FirstName} {requestor.LastName}", true, true, false);

                _logger.LogInformation("Successfully ended break for requestor with UserName {UserName} for EventId {EventId}", actionDto.RequestorId, eventId);
                return Ok();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error ending break for requestor with UserName {UserName} for EventId {EventId}: {Message}", actionDto.RequestorId, eventId, ex.Message);
                return StatusCode(500, new { message = "An error occurred while ending the break", details = ex.Message });
            }
        }

        [HttpPost("{eventId}/queue/{queueId}/skip")]
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
                await _hubContext.Clients.Group($"Event_{eventId}").SendAsync("QueueUpdated", queueId, "Skipped");

                _logger.LogInformation("Successfully skipped song with QueueId {QueueId} for EventId {EventId}", queueId, eventId);
                return Ok();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error skipping song with QueueId {QueueId} for EventId {EventId}: {Message}", queueId, ex.Message);
                return StatusCode(500, new { message = "An error occurred while skipping the song", details = ex.Message });
            }
        }

        [HttpPut("{eventId}/queue/reorder")]
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

                foreach (var order in request.NewOrder)
                {
                    var queueEntry = allQueueEntries.FirstOrDefault(eq => eq.QueueId == order.QueueId);
                    if (queueEntry != null)
                    {
                        positionMapping[queueEntry.QueueId] = order.Position;
                    }
                }

                var otherQueueEntries = allQueueEntries
                    .Where(eq => !userQueueIds.Contains(eq.QueueId))
                    .OrderBy(eq => eq.Position)
                    .ToList();

                var sortedUserEntries = userQueueEntries
                    .OrderBy(eq => request.NewOrder.FirstOrDefault(o => o.QueueId == eq.QueueId)?.Position ?? int.MaxValue)
                    .ToList();

                int position = 1;
                var reorderedEntries = new List<EventQueue>();
                foreach (var userEntry in sortedUserEntries)
                {
                    userEntry.Position = position++;
                    userEntry.UpdatedAt = DateTime.UtcNow;
                    reorderedEntries.Add(userEntry);
                }

                foreach (var otherEntry in otherQueueEntries)
                {
                    otherEntry.Position = position++;
                    otherEntry.UpdatedAt = DateTime.UtcNow;
                    reorderedEntries.Add(otherEntry);
                }

                await _context.SaveChangesAsync();
                await _hubContext.Clients.Group($"Event_{eventId}").SendAsync("QueueUpdated", 0, "Reordered");

                _logger.LogInformation("Successfully reordered queue for EventId: {EventId}", eventId);
                return Ok();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error reordering queue for EventId {EventId}: {Message}", eventId, ex.Message);
                return StatusCode(500, new { message = "An error occurred while reordering the queue", details = ex.Message });
            }
        }

        [HttpPost("{eventId}/queue/{queueId}/singers")]
        [Authorize]
        public async Task<IActionResult> UpdateQueueSingers(int eventId, int queueId, [FromBody] UpdateQueueSingersDto singersDto)
        {
            try
            {
                _logger.LogInformation("Updating singers for QueueId {QueueId} in EventId: {EventId}", queueId, eventId);
                if (!ModelState.IsValid)
                {
                    _logger.LogWarning("Invalid model state for UpdateQueueSingers: {Errors}", string.Join("; ", ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage)));
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

                var userName = User.FindFirst(ClaimTypes.Name)?.Value;
                if (string.IsNullOrEmpty(userName))
                {
                    _logger.LogWarning("User identity not found in token");
                    return Unauthorized("User identity not found in token");
                }

                var requestor = await _context.Users
                    .OfType<ApplicationUser>()
                    .FirstOrDefaultAsync(u => u.UserName == queueEntry.RequestorUserName);
                if (requestor == null)
                {
                    _logger.LogWarning("Requestor not found with UserName: {UserName}", queueEntry.RequestorUserName);
                    return BadRequest("Requestor not found");
                }

                if (queueEntry.RequestorUserName != userName)
                {
                    try
                    {
                        var currentSingers = JsonSerializer.Deserialize<string[]>(queueEntry.Singers) ?? Array.Empty<string>();
                        if (!currentSingers.Contains(userName))
                        {
                            _logger.LogWarning("User with UserName {UserName} is not authorized to update singers for QueueId {QueueId}", userName, queueId);
                            return Forbid("Only the requestor or a singer in the queue entry can update singers");
                        }
                    }
                    catch (JsonException ex)
                    {
                        _logger.LogWarning("Failed to deserialize Singers for QueueId {QueueId}: {Message}", queueId, ex.Message);
                        return StatusCode(500, new { message = "Error processing singers data", details = ex.Message });
                    }
                }

                var newSingers = singersDto.Singers ?? Array.Empty<string>();
                foreach (var singer in newSingers)
                {
                    if (singer != "AllSing" && singer != "TheBoys" && singer != "TheGirls")
                    {
                        var singerUser = await _context.Users
                            .OfType<ApplicationUser>()
                            .FirstOrDefaultAsync(u => u.UserName == singer);
                        if (singerUser == null)
                        {
                            _logger.LogWarning("Singer not found with UserName: {UserName}", singer);
                            return BadRequest($"Singer not found: {singer}");
                        }

                        var attendance = await _context.EventAttendances
                            .FirstOrDefaultAsync(ea => ea.EventId == eventId && ea.RequestorId == singerUser.Id);
                        if (eventEntity.Status != "Upcoming" && (attendance == null || !attendance.IsCheckedIn))
                        {
                            _logger.LogWarning("Singer with UserName {UserName} must be checked in to be added to queue for EventId {EventId}", singer, eventId);
                            return BadRequest($"Singer must be checked in: {singer}");
                        }
                    }
                }

                if (!newSingers.Any(s => s == queueEntry.RequestorUserName || s == "AllSing" || s == "TheBoys" || s == "TheGirls"))
                {
                    _logger.LogWarning("RequestorUserName {UserName} must be included in singers list for QueueId {QueueId}", queueEntry.RequestorUserName, queueId);
                    return BadRequest("Requestor must be included in the singers list");
                }

                var serializedSingers = JsonSerializer.Serialize(newSingers);
                queueEntry.Singers = serializedSingers;
                queueEntry.UpdatedAt = DateTime.UtcNow;

                await _context.SaveChangesAsync();

                var singersList = new List<string>();
                try
                {
                    singersList.AddRange(JsonSerializer.Deserialize<string[]>(queueEntry.Singers) ?? Array.Empty<string>());
                }
                catch (JsonException ex)
                {
                    _logger.LogWarning("Failed to deserialize Singers for QueueId {QueueId}: {Message}", queueId, ex.Message);
                }

                var queueEntryDto = new EventQueueDto
                {
                    QueueId = queueEntry.QueueId,
                    EventId = queueEntry.EventId,
                    SongId = queueEntry.SongId,
                    RequestorUserName = queueEntry.RequestorUserName,
                    Singers = singersList,
                    Position = queueEntry.Position,
                    Status = ComputeSongStatus(queueEntry, false),
                    IsActive = queueEntry.IsActive,
                    WasSkipped = queueEntry.WasSkipped,
                    IsCurrentlyPlaying = queueEntry.IsCurrentlyPlaying,
                    SungAt = queueEntry.SungAt,
                    IsOnBreak = queueEntry.IsOnBreak
                };

                _logger.LogInformation("Successfully updated singers for QueueId {QueueId} in EventId {EventId}", queueId, eventId);
                return Ok(queueEntryDto);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating singers for QueueId {QueueId} in EventId {EventId}: {Message}", queueId, eventId, ex.Message);
                return StatusCode(500, new { message = "An error occurred while updating singers", details = ex.Message });
            }
        }

        [HttpPost("{eventId}/queue/{queueId}/play")]
        [Authorize(Roles = "Karaoke DJ")]
        public async Task<IActionResult> PlaySong(int eventId, int queueId)
        {
            try
            {
                _logger.LogInformation("Playing song with QueueId {QueueId} for EventId: {EventId}", queueId, eventId);
                var queueEntry = await _context.EventQueues
                    .FirstOrDefaultAsync(eq => eq.EventId == eventId && eq.QueueId == queueId);
                if (queueEntry == null)
                {
                    _logger.LogWarning("Queue entry not found with QueueId {QueueId} for EventId {EventId}", queueId, eventId);
                    return NotFound("Queue entry not found");
                }
                queueEntry.IsCurrentlyPlaying = true;
                queueEntry.Status = "Live";
                queueEntry.UpdatedAt = DateTime.UtcNow;
                await _context.SaveChangesAsync();
                await _hubContext.Clients.Group($"Event_{eventId}").SendAsync("QueueUpdated", queueId, "Playing");
                _logger.LogInformation("Successfully played song with QueueId {QueueId} for EventId {EventId}", queueId, eventId);
                return Ok();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error playing song with QueueId {QueueId} for EventId {EventId}: {Message}", queueId, eventId, ex.Message);
                return StatusCode(500, new { message = "Error playing song", details = ex.Message });
            }
        }

        [HttpPost("{eventId}/queue/{queueId}/pause")]
        [Authorize(Roles = "Karaoke DJ")]
        public async Task<IActionResult> PauseSong(int eventId, int queueId)
        {
            try
            {
                _logger.LogInformation("Pausing song with QueueId {QueueId} for EventId: {EventId}", queueId, eventId);
                var queueEntry = await _context.EventQueues
                    .FirstOrDefaultAsync(eq => eq.EventId == eventId && eq.QueueId == queueId);
                if (queueEntry == null)
                {
                    _logger.LogWarning("Queue entry not found with QueueId {QueueId} for EventId {EventId}", queueId, eventId);
                    return NotFound("Queue entry not found");
                }
                queueEntry.IsCurrentlyPlaying = false;
                queueEntry.UpdatedAt = DateTime.UtcNow;
                await _context.SaveChangesAsync();
                await _hubContext.Clients.Group($"Event_{eventId}").SendAsync("QueueUpdated", queueId, "Paused");
                _logger.LogInformation("Successfully paused song with QueueId {QueueId} for EventId {EventId}", queueId, eventId);
                return Ok();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error pausing song with QueueId {QueueId} for EventId {EventId}: {Message}", queueId, eventId, ex.Message);
                return StatusCode(500, new { message = "Error pausing song", details = ex.Message });
            }
        }

        [HttpPost("{eventId}/queue/{queueId}/stop")]
        [Authorize(Roles = "Karaoke DJ")]
        public async Task<IActionResult> StopSong(int eventId, int queueId)
        {
            try
            {
                _logger.LogInformation("Stopping song with QueueId {QueueId} for EventId: {EventId}", queueId, eventId);
                var queueEntry = await _context.EventQueues
                    .FirstOrDefaultAsync(eq => eq.EventId == eventId && eq.QueueId == queueId);
                if (queueEntry == null)
                {
                    _logger.LogWarning("Queue entry not found with QueueId {QueueId} for EventId {EventId}", queueId, eventId);
                    return NotFound("Queue entry not found");
                }
                queueEntry.IsCurrentlyPlaying = false;
                queueEntry.SungAt = DateTime.UtcNow;
                queueEntry.Status = "Archived";
                queueEntry.UpdatedAt = DateTime.UtcNow;
                await _context.SaveChangesAsync();
                await _hubContext.Clients.Group($"Event_{eventId}").SendAsync("QueueUpdated", queueId, "Stopped");
                _logger.LogInformation("Successfully stopped song with QueueId {QueueId} for EventId {EventId}", queueId, eventId);
                return Ok();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error stopping song with QueueId {QueueId} for EventId {EventId}: {Message}", queueId, eventId, ex.Message);
                return StatusCode(500, new { message = "Error stopping song", details = ex.Message });
            }
        }

        [HttpPost("{eventId}/queue/{queueId}/launch")]
        [Authorize(Roles = "Karaoke DJ")]
        public async Task<IActionResult> LaunchVideo(int eventId, int queueId)
        {
            try
            {
                _logger.LogInformation("Launching video for QueueId {QueueId} in EventId: {EventId}", queueId, eventId);
                var queueEntry = await _context.EventQueues
                    .Include(eq => eq.Song)
                    .FirstOrDefaultAsync(eq => eq.EventId == eventId && eq.QueueId == queueId);
                if (queueEntry == null)
                {
                    _logger.LogWarning("Queue entry not found with QueueId {QueueId} for EventId {EventId}", queueId, eventId);
                    return NotFound("Queue entry not found");
                }
                if (string.IsNullOrEmpty(queueEntry.Song?.YouTubeUrl))
                {
                    _logger.LogWarning("No YouTube URL for QueueId {QueueId} in EventId {EventId}", queueId, eventId);
                    return BadRequest("No YouTube URL available for this song");
                }
                queueEntry.IsCurrentlyPlaying = true;
                queueEntry.Status = "Live";
                queueEntry.UpdatedAt = DateTime.UtcNow;
                await _context.SaveChangesAsync();
                await _hubContext.Clients.Group($"Event_{eventId}").SendAsync("QueueUpdated", queueId, "Playing", queueEntry.Song.YouTubeUrl);
                _logger.LogInformation("Successfully launched video for QueueId {QueueId} in EventId {EventId}", queueId, eventId);
                return Ok(new { youTubeUrl = queueEntry.Song.YouTubeUrl });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error launching video for QueueId {QueueId} in EventId {EventId}: {Message}", queueId, ex.Message);
                return StatusCode(500, new { message = "Error launching video", details = ex.Message });
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
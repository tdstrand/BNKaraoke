namespace BNKaraoke.Api.Controllers
{
    using Microsoft.AspNetCore.Authorization;
    using Microsoft.AspNetCore.Mvc;
    using Microsoft.EntityFrameworkCore;
    using Microsoft.Extensions.Logging;
    using System.Threading.Tasks;
    using BNKaraoke.Api.Data;
    using BNKaraoke.Api.Models;
    using BNKaraoke.Api.Dtos;
    using System.Linq;
    using System.Security.Claims;

    [Route("api/events")]
    [ApiController]
    public class EventController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<EventController> _logger;

        public EventController(ApplicationDbContext context, ILogger<EventController> logger)
        {
            _context = context;
            _logger = logger;
            _logger.LogInformation("EventController instantiated");
        }

        // GET /api/events/health: Health check endpoint (no dependencies)
        [HttpGet("health")]
        [AllowAnonymous]
        public IActionResult HealthCheck()
        {
            _logger.LogInformation("Health check endpoint called");
            return Ok(new { status = "healthy", message = "EventController is running" });
        }

        // GET /api/events/minimal: Minimal test endpoint (no dependencies)
        [HttpGet("minimal")]
        [AllowAnonymous]
        public IActionResult MinimalTest()
        {
            _logger.LogInformation("Minimal test endpoint called");
            return Ok(new { message = "Minimal test endpoint reached" });
        }

        // GET /api/events: Get all events
        [HttpGet]
        [Authorize]
        public async Task<IActionResult> GetEvents()
        {
            try
            {
                _logger.LogInformation("Fetching all events from the database");
                var events = await _context.Events
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

        // POST /api/events: Create a new event
        [HttpPost]
        [Authorize(Roles = "EventManager")]
        public async Task<IActionResult> CreateEvent([FromBody] EventCreateDto eventDto)
        {
            try
            {
                _logger.LogInformation("Creating a new event with EventCode: {EventCode}", eventDto.EventCode);
                if (!ModelState.IsValid)
                {
                    _logger.LogWarning("Invalid model state for CreateEvent: {Errors}", ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage));
                    return BadRequest(ModelState);
                }

                var newEvent = new Event
                {
                    EventCode = eventDto.EventCode,
                    Description = eventDto.Description,
                    Status = eventDto.Status ?? "Upcoming",
                    Visibility = eventDto.Visibility ?? "Visible",
                    Location = eventDto.Location,
                    ScheduledDate = eventDto.ScheduledDate,
                    ScheduledStartTime = eventDto.ScheduledStartTime,
                    ScheduledEndTime = eventDto.ScheduledEndTime,
                    KaraokeDJName = eventDto.KaraokeDJName,
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
                    QueueCount = 0 // New event, no queue entries yet
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

        // PUT /api/events/{eventId}: Update an event
        [HttpPut("{eventId}")]
        [Authorize(Roles = "EventManager")]
        public async Task<IActionResult> UpdateEvent(int eventId, [FromBody] EventUpdateDto eventDto)
        {
            try
            {
                _logger.LogInformation("Updating event with EventId: {EventId}", eventId);
                if (!ModelState.IsValid)
                {
                    _logger.LogWarning("Invalid model state for UpdateEvent: {Errors}", ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage));
                    return BadRequest(ModelState);
                }

                var existingEvent = await _context.Events.FindAsync(eventId);
                if (existingEvent == null)
                {
                    _logger.LogWarning("Event not found with EventId: {EventId}", eventId);
                    return NotFound("Event not found");
                }

                existingEvent.EventCode = eventDto.EventCode;
                existingEvent.Description = eventDto.Description;
                existingEvent.Status = eventDto.Status ?? existingEvent.Status;
                existingEvent.Visibility = eventDto.Visibility ?? existingEvent.Visibility;
                existingEvent.Location = eventDto.Location;
                existingEvent.ScheduledDate = eventDto.ScheduledDate;
                existingEvent.ScheduledStartTime = eventDto.ScheduledStartTime ?? existingEvent.ScheduledStartTime;
                existingEvent.ScheduledEndTime = eventDto.ScheduledEndTime ?? existingEvent.ScheduledEndTime;
                existingEvent.KaraokeDJName = eventDto.KaraokeDJName ?? existingEvent.KaraokeDJName;
                existingEvent.IsCanceled = eventDto.IsCanceled ?? existingEvent.IsCanceled;
                existingEvent.RequestLimit = eventDto.RequestLimit;
                existingEvent.UpdatedAt = DateTime.UtcNow;

                // If the status changes, update the status of associated queue entries
                if (existingEvent.Status != eventDto.Status)
                {
                    _logger.LogInformation("Event status changed from {OldStatus} to {NewStatus}, updating queue entries", existingEvent.Status, eventDto.Status);
                    var queueEntries = await _context.EventQueues
                        .Where(eq => eq.EventId == eventId)
                        .ToListAsync();

                    foreach (var entry in queueEntries)
                    {
                        entry.Status = existingEvent.Status;
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
                    QueueCount = await _context.EventQueues.CountAsync(eq => eq.EventId == eventId)
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

        // GET /api/events/{eventId}: Get an event by ID
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
                    QueueCount = await _context.EventQueues.CountAsync(eq => eq.EventId == eventId)
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

        // POST /api/events/{eventId}/queue: Add a song to the event queue
        [HttpPost("{eventId}/queue")]
        [Authorize]
        public async Task<IActionResult> AddToQueue(int eventId, [FromBody] EventQueueCreateDto queueDto)
        {
            try
            {
                _logger.LogInformation("Adding song to queue for EventId: {EventId}, SongId: {SongId}, RequestorId: {RequestorId}", eventId, queueDto.SongId, queueDto.RequestorId);
                if (!ModelState.IsValid)
                {
                    _logger.LogWarning("Invalid model state for AddToQueue: {Errors}", ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage));
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

                // Verify the song exists
                var song = await _context.Songs.FindAsync(queueDto.SongId);
                if (song == null)
                {
                    _logger.LogWarning("Song not found with SongId: {SongId}", queueDto.SongId);
                    return BadRequest("Song not found");
                }

                // Verify the requestor exists by UserName instead of Id
                var requestor = await _context.Users
                    .FirstOrDefaultAsync(u => u.UserName == queueDto.RequestorId);
                if (requestor == null)
                {
                    _logger.LogWarning("Requestor not found with UserName: {RequestorId}", queueDto.RequestorId);
                    return BadRequest("Requestor not found");
                }

                // Check the event's request limit (excluding co-sung songs)
                var requestedCount = await _context.EventQueues
                    .CountAsync(eq => eq.EventId == eventId && eq.RequestorId == requestor.Id);
                if (requestedCount >= eventEntity.RequestLimit)
                {
                    _logger.LogWarning("Requestor with UserName {UserName} has reached the request limit of {RequestLimit} for EventId {EventId}", queueDto.RequestorId, eventEntity.RequestLimit, eventId);
                    return BadRequest($"You have reached the event's request limit of {eventEntity.RequestLimit} songs.");
                }

                // Check if the requestor is checked in, but only for non-Upcoming events
                if (eventEntity.Status != "Upcoming")
                {
                    var attendance = await _context.EventAttendances
                        .FirstOrDefaultAsync(ea => ea.EventId == eventId && ea.RequestorId == requestor.Id);
                    if (attendance == null || !attendance.IsCheckedIn)
                    {
                        _logger.LogWarning("Requestor with UserName {UserName} must be checked in to add to queue for EventId {EventId} with status {Status}", queueDto.RequestorId, eventId, eventEntity.Status);
                        return BadRequest("Requestor must be checked in to add to the queue for a non-upcoming event");
                    }
                }

                // Get the next position in the queue
                var maxPosition = await _context.EventQueues
                    .Where(eq => eq.EventId == eventId)
                    .MaxAsync(eq => (int?)eq.Position) ?? 0;

                var newQueueEntry = new EventQueue
                {
                    EventId = eventId,
                    SongId = queueDto.SongId,
                    RequestorId = requestor.Id,
                    Singers = new List<string> { requestor.Id }, // Initialize with the requestor
                    Position = maxPosition + 1,
                    Status = eventEntity.Status,
                    IsActive = eventEntity.Status != "Upcoming", // Active only if the event is not Upcoming
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                };

                _context.EventQueues.Add(newQueueEntry);
                await _context.SaveChangesAsync();

                // Map the new queue entry to a DTO to avoid circular references
                var queueEntryDto = new EventQueueDto
                {
                    QueueId = newQueueEntry.QueueId,
                    EventId = newQueueEntry.EventId,
                    SongId = newQueueEntry.SongId,
                    RequestorId = newQueueEntry.RequestorId,
                    Singers = newQueueEntry.Singers,
                    Position = newQueueEntry.Position,
                    Status = ComputeSongStatus(newQueueEntry), // Compute status
                    IsActive = newQueueEntry.IsActive,
                    WasSkipped = newQueueEntry.WasSkipped,
                    IsCurrentlyPlaying = newQueueEntry.IsCurrentlyPlaying,
                    SungAt = newQueueEntry.SungAt,
                    IsOnBreak = false // Will be computed in GetEventQueue
                };

                _logger.LogInformation("Successfully added song to queue for EventId {EventId}, QueueId: {QueueId}", eventId, newQueueEntry.QueueId);
                return CreatedAtAction(nameof(GetEventQueue), new { eventId, queueId = newQueueEntry.QueueId }, queueEntryDto);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error adding song to queue for EventId {EventId}: {Message}", eventId, ex.Message);
                return StatusCode(500, new { message = "An error occurred while adding to the queue", details = ex.Message });
            }
        }

        // GET /api/events/{eventId}/queue: Retrieve the event queue (with break status and computed status)
        [HttpGet("{eventId}/queue")]
        [Authorize]
        public IActionResult GetEventQueue(int eventId)
        {
            try
            {
                _logger.LogInformation("Fetching event queue for EventId: {EventId}", eventId);
                var eventEntity = _context.Events.FirstOrDefault(e => e.EventId == eventId);
                if (eventEntity == null)
                {
                    _logger.LogWarning("Event not found with EventId: {EventId}", eventId);
                    return NotFound("Event not found");
                }

                var queueEntries = (from queue in _context.EventQueues
                                    join attendance in _context.EventAttendances
                                    on new { queue.EventId, queue.RequestorId } equals new { attendance.EventId, attendance.RequestorId }
                                    where queue.EventId == eventId
                                    select new { QueueEntry = queue, Attendance = attendance }).ToList();

                var queueDtos = new List<EventQueueDto>();
                foreach (var entry in queueEntries)
                {
                    var eq = entry.QueueEntry;
                    var ea = entry.Attendance;

                    // Check if any singer is on break
                    bool anySingerOnBreak = false;
                    foreach (var singerId in eq.Singers)
                    {
                        if (singerId == "AllSing" || singerId == "TheBoys" || singerId == "TheGirls")
                        {
                            continue; // Special groups don't have break status
                        }

                        var singerAttendance = _context.EventAttendances
                            .FirstOrDefault(ea => ea.EventId == eventId && ea.RequestorId == singerId);
                        if (singerAttendance != null && singerAttendance.IsOnBreak)
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
                        RequestorId = eq.RequestorId,
                        Singers = eq.Singers,
                        Position = eq.Position,
                        Status = ComputeSongStatus(eq, anySingerOnBreak),
                        IsActive = eq.IsActive,
                        WasSkipped = eq.WasSkipped,
                        IsCurrentlyPlaying = eq.IsCurrentlyPlaying,
                        SungAt = eq.SungAt,
                        IsOnBreak = ea.IsOnBreak // This reflects the requestor's break status
                    };

                    queueDtos.Add(queueDto);
                }

                // Sort by position
                queueDtos = queueDtos.OrderBy(eq => eq.Position).ToList();

                _logger.LogInformation("Successfully fetched {Count} queue entries for EventId: {EventId}", queueDtos.Count, eventId);
                return Ok(queueDtos);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching event queue for EventId {EventId}: {Message}", eventId, ex.Message);
                return StatusCode(500, new { message = "An error occurred while fetching the event queue", details = ex.Message });
            }
        }

        // Helper method to compute song status
        private string ComputeSongStatus(EventQueue queueEntry, bool anySingerOnBreak = false)
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

        // POST /api/events/{eventId}/attendance/check-in: Requestor checks in
        [HttpPost("{eventId}/attendance/check-in")]
        [Authorize]
        public async Task<IActionResult> CheckIn(int eventId, [FromBody] AttendanceActionDto actionDto)
        {
            try
            {
                _logger.LogInformation("Checking in requestor with UserName {UserName} for EventId: {EventId}", actionDto.RequestorId, eventId);
                if (!ModelState.IsValid)
                {
                    _logger.LogWarning("Invalid model state for CheckIn: {Errors}", ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage));
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

                // Look up requestor by UserName instead of Id
                var requestor = await _context.Users
                    .FirstOrDefaultAsync(u => u.UserName == actionDto.RequestorId);
                if (requestor == null)
                {
                    _logger.LogWarning("Requestor not found with UserName: {UserName}", actionDto.RequestorId);
                    return BadRequest("Requestor not found with UserName: " + actionDto.RequestorId);
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

                // Save the EventAttendance record first to generate the AttendanceId
                await _context.SaveChangesAsync();

                // Log the check-in action with the generated AttendanceId
                var attendanceHistory = new EventAttendanceHistory
                {
                    EventId = eventId,
                    RequestorId = requestor.Id,
                    Action = "CheckIn",
                    ActionTimestamp = DateTime.UtcNow,
                    AttendanceId = attendance.AttendanceId // Now guaranteed to have a valid value
                };
                _context.EventAttendanceHistories.Add(attendanceHistory);

                // Activate the requestor's queue entries
                var queueEntries = await _context.EventQueues
                    .Where(eq => eq.EventId == eventId && eq.RequestorId == requestor.Id)
                    .ToListAsync();

                foreach (var entry in queueEntries)
                {
                    entry.IsActive = true;
                    entry.UpdatedAt = DateTime.UtcNow;
                }

                // Save the EventAttendanceHistory and queue updates
                await _context.SaveChangesAsync();

                _logger.LogInformation("Successfully checked in requestor with UserName {UserName} for EventId {EventId}", actionDto.RequestorId, eventId);
                return Ok(new { message = "Check-in successful" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking in requestor with UserName {UserName} for EventId {EventId}: {Message}", actionDto.RequestorId, eventId, ex.Message);
                return StatusCode(500, new { message = "An error occurred while checking in", details = ex.Message });
            }
        }

        // POST /api/events/{eventId}/attendance/check-out: Requestor checks out
        [HttpPost("{eventId}/attendance/check-out")]
        [Authorize]
        public async Task<IActionResult> CheckOut(int eventId, [FromBody] AttendanceActionDto actionDto)
        {
            try
            {
                _logger.LogInformation("Checking out requestor with UserName {UserName} for EventId: {EventId}", actionDto.RequestorId, eventId);
                if (!ModelState.IsValid)
                {
                    _logger.LogWarning("Invalid model state for CheckOut: {Errors}", ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage));
                    return BadRequest(ModelState);
                }

                var eventEntity = await _context.Events.FindAsync(eventId);
                if (eventEntity == null)
                {
                    _logger.LogWarning("Event not found with EventId: {EventId}", eventId);
                    return NotFound("Event not found");
                }

                // Look up requestor by UserName instead of Id
                var requestor = await _context.Users
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

                // Save the updated EventAttendance record first
                await _context.SaveChangesAsync();

                // Log the check-out action with the AttendanceId
                var attendanceHistory = new EventAttendanceHistory
                {
                    EventId = eventId,
                    RequestorId = requestor.Id,
                    Action = "CheckOut",
                    ActionTimestamp = DateTime.UtcNow,
                    AttendanceId = attendance.AttendanceId
                };
                _context.EventAttendanceHistories.Add(attendanceHistory);

                // Deactivate the requestor's queue entries
                var queueEntries = await _context.EventQueues
                    .Where(eq => eq.EventId == eventId && eq.RequestorId == requestor.Id)
                    .ToListAsync();

                foreach (var entry in queueEntries)
                {
                    entry.IsActive = false;
                    entry.UpdatedAt = DateTime.UtcNow;
                }

                // Save the EventAttendanceHistory and queue updates
                await _context.SaveChangesAsync();

                _logger.LogInformation("Successfully checked out requestor with UserName {UserName} for EventId {EventId}", actionDto.RequestorId, eventId);
                return Ok(new { message = "Check-out successful" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking out requestor with UserName {UserName} for EventId {EventId}: {Message}", actionDto.RequestorId, eventId, ex.Message);
                return StatusCode(500, new { message = "An error occurred while checking out", details = ex.Message });
            }
        }

        // POST /api/events/{eventId}/attendance/break/start: Requestor takes a break
        [HttpPost("{eventId}/attendance/break/start")]
        [Authorize]
        public async Task<IActionResult> StartBreak(int eventId, [FromBody] AttendanceActionDto actionDto)
        {
            try
            {
                _logger.LogInformation("Starting break for requestor with UserName {UserName} for EventId: {EventId}", actionDto.RequestorId, eventId);
                if (!ModelState.IsValid)
                {
                    _logger.LogWarning("Invalid model state for StartBreak: {Errors}", ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage));
                    return BadRequest(ModelState);
                }

                var eventEntity = await _context.Events.FindAsync(eventId);
                if (eventEntity == null)
                {
                    _logger.LogWarning("Event not found with EventId: {EventId}", eventId);
                    return NotFound("Event not found");
                }

                var attendance = await _context.EventAttendances
                    .FirstOrDefaultAsync(ea => ea.EventId == eventId && ea.RequestorId == actionDto.RequestorId);
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
                _logger.LogInformation("Successfully started break for requestor with UserName {UserName} for EventId {EventId}", actionDto.RequestorId, eventId);
                return Ok();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error starting break for requestor with UserName {UserName} for EventId {EventId}: {Message}", actionDto.RequestorId, eventId, ex.Message);
                return StatusCode(500, new { message = "An error occurred while starting the break", details = ex.Message });
            }
        }

        // POST /api/events/{eventId}/attendance/break/end: Requestor returns from break
        [HttpPost("{eventId}/attendance/break/end")]
        [Authorize]
        public async Task<IActionResult> EndBreak(int eventId, [FromBody] AttendanceActionDto actionDto)
        {
            try
            {
                _logger.LogInformation("Ending break for requestor with UserName {UserName} for EventId: {EventId}", actionDto.RequestorId, eventId);
                if (!ModelState.IsValid)
                {
                    _logger.LogWarning("Invalid model state for EndBreak: {Errors}", ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage));
                    return BadRequest(ModelState);
                }

                var eventEntity = await _context.Events.FindAsync(eventId);
                if (eventEntity == null)
                {
                    _logger.LogWarning("Event not found with EventId: {EventId}", eventId);
                    return NotFound("Event not found");
                }

                var attendance = await _context.EventAttendances
                    .FirstOrDefaultAsync(ea => ea.EventId == eventId && ea.RequestorId == actionDto.RequestorId);
                if (attendance == null || !attendance.IsCheckedIn || !attendance.IsOnBreak)
                {
                    _logger.LogWarning("Requestor with UserName {UserName} must be checked in and on break to end break for EventId {EventId}", actionDto.RequestorId, eventId);
                    return BadRequest("Requestor must be checked in and on break to end a break");
                }

                attendance.IsOnBreak = false;
                attendance.BreakEndAt = DateTime.UtcNow;

                // Move skipped songs to the top of the queue
                var minPosition = await _context.EventQueues
                    .Where(eq => eq.EventId == eventId)
                    .MinAsync(eq => (int?)eq.Position) ?? 1;

                var skippedEntries = await _context.EventQueues
                    .Where(eq => eq.EventId == eventId && eq.RequestorId == actionDto.RequestorId && eq.WasSkipped)
                    .OrderBy(eq => eq.QueueId)
                    .ToListAsync();

                for (int i = 0; i < skippedEntries.Count; i++)
                {
                    skippedEntries[i].Position = minPosition - (i + 1);
                    skippedEntries[i].WasSkipped = false;
                    skippedEntries[i].UpdatedAt = DateTime.UtcNow;
                }

                // Reorder the rest of the queue
                var otherEntries = await _context.EventQueues
                    .Where(eq => eq.EventId == eventId && (eq.RequestorId != actionDto.RequestorId || !eq.WasSkipped))
                    .OrderBy(eq => eq.Position)
                    .ToListAsync();

                for (int i = 0; i < otherEntries.Count; i++)
                {
                    otherEntries[i].Position = minPosition + i;
                    otherEntries[i].UpdatedAt = DateTime.UtcNow;
                }

                await _context.SaveChangesAsync();
                _logger.LogInformation("Successfully ended break for requestor with UserName {UserName} for EventId {EventId}", actionDto.RequestorId, eventId);
                return Ok();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error ending break for requestor with UserName {UserName} for EventId {EventId}: {Message}", actionDto.RequestorId, eventId, ex.Message);
                return StatusCode(500, new { message = "An error occurred while ending the break", details = ex.Message });
            }
        }

        // POST /api/events/{eventId}/queue/{queueId}/skip: DJ skips a song
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

                var attendance = await _context.EventAttendances
                    .FirstOrDefaultAsync(ea => ea.EventId == eventId && ea.RequestorId == queueEntry.RequestorId);
                if (attendance == null || !attendance.IsOnBreak)
                {
                    _logger.LogWarning("Requestor with RequestorId {RequestorId} must be on break to skip song for EventId {EventId}", queueEntry.RequestorId, eventId);
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

        // PUT /api/events/{eventId}/queue/reorder: Reorder the user's queue entries
        [HttpPut("{eventId}/queue/reorder")]
        [Authorize]
        public async Task<IActionResult> ReorderQueue(int eventId, [FromBody] ReorderQueueRequest request)
        {
            try
            {
                _logger.LogInformation("Reordering queue for EventId: {EventId}", eventId);
                if (!ModelState.IsValid)
                {
                    _logger.LogWarning("Invalid model state for ReorderQueue: {Errors}", ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage));
                    return BadRequest(ModelState);
                }

                var eventEntity = await _context.Events.FindAsync(eventId);
                if (eventEntity == null)
                {
                    _logger.LogWarning("Event not found with EventId: {EventId}", eventId);
                    return NotFound("Event not found");
                }

                // Get the user's ID from the token
                var userId = User.FindFirst(ClaimTypes.Name)?.Value;
                if (string.IsNullOrEmpty(userId))
                {
                    _logger.LogWarning("User identity not found in token");
                    return Unauthorized("User identity not found in token");
                }

                // Fetch the user's queue playlist entries for this event
                var userQueueEntries = await _context.EventQueues
                    .Where(eq => eq.EventId == eventId && (eq.RequestorId == userId || eq.Singers.Contains(userId)))
                    .ToListAsync();

                // Validate the request
                var requestQueueIds = request.NewOrder.Select(o => o.QueueId).ToList();
                var userQueueIds = userQueueEntries.Select(eq => eq.QueueId).ToList();

                if (requestQueueIds.Count != userQueueIds.Count || !requestQueueIds.All(qid => userQueueIds.Contains(qid)))
                {
                    _logger.LogWarning("Invalid reorder request: Queue IDs do not match user's queue entries for EventId {EventId}", eventId);
                    return BadRequest("Invalid reorder request: Queue IDs do not match user's queue entries");
                }

                // Get all queue entries for the event to determine position mapping
                var allQueueEntries = await _context.EventQueues
                    .Where(eq => eq.EventId == eventId)
                    .OrderBy(eq => eq.Position)
                    .ToListAsync();

                // Map current positions to queue entries
                var positionMapping = allQueueEntries.ToDictionary(eq => eq.QueueId, eq => eq.Position);

                // Update positions for the user's queue entries based on the new order
                for (int i = 0; i < request.NewOrder.Count; i++)
                {
                    var queueId = request.NewOrder[i].QueueId;
                    var newPosition = request.NewOrder[i].Position;

                    var queueEntry = userQueueEntries.First(eq => eq.QueueId == queueId);
                    var oldPosition = positionMapping[queueId];

                    // Update the position in the mapping
                    positionMapping[queueId] = newPosition;
                }

                // Assign new positions to all queue entries
                var sortedPositions = positionMapping.OrderBy(kv => kv.Value).ToList();
                for (int i = 0; i < sortedPositions.Count; i++)
                {
                    var queueId = sortedPositions[i].Key;
                    var queueEntry = allQueueEntries.First(eq => eq.QueueId == queueId);
                    queueEntry.Position = i + 1; // Positions start at 1
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

        // POST /api/events/{eventId}/queue/{queueId}/singers: Update singers for a queue entry
        [HttpPost("{eventId}/queue/{queueId}/singers")]
        [Authorize]
        public async Task<IActionResult> UpdateSingers(int eventId, int queueId, [FromBody] UpdateSingersRequest request)
        {
            try
            {
                _logger.LogInformation("Updating singers for QueueId {QueueId} in EventId: {EventId}", queueId, eventId);
                if (!ModelState.IsValid)
                {
                    _logger.LogWarning("Invalid model state for UpdateSingers: {Errors}", ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage));
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

                // Validate the new singers list
                if (request.Singers == null || !request.Singers.Any())
                {
                    _logger.LogWarning("Singers list cannot be empty for QueueId {QueueId}", queueId);
                    return BadRequest("At least one singer or special group is required");
                }

                // Check for special groups
                var specialGroups = new List<string> { "AllSing", "TheBoys", "TheGirls" };
                var hasSpecialGroup = request.Singers.Any(s => specialGroups.Contains(s));
                var individualSingers = request.Singers.Where(s => !specialGroups.Contains(s)).ToList();

                if (hasSpecialGroup)
                {
                    // If a special group is selected, it must be the only entry
                    if (request.Singers.Count > 1)
                    {
                        _logger.LogWarning("Special group selected with additional singers for QueueId {QueueId}", queueId);
                        return BadRequest("Special groups cannot be combined with individual singers");
                    }
                }
                else
                {
                    // If no special group, enforce the limit of 7 individual singers
                    if (individualSingers.Count > 7)
                    {
                        _logger.LogWarning("Too many individual singers ({Count}) for QueueId {QueueId}", individualSingers.Count, queueId);
                        return BadRequest("Cannot have more than 7 individual singers");
                    }

                    // Validate that all individual singers exist
                    foreach (var singerId in individualSingers)
                    {
                        var singer = await _context.Users.FindAsync(singerId);
                        if (singer == null)
                        {
                            _logger.LogWarning("Singer not found with SingerId: {SingerId} for QueueId {QueueId}", singerId, queueId);
                            return BadRequest($"Singer not found: {singerId}");
                        }
                    }
                }

                queueEntry.Singers = request.Singers;
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
    }

    public class AttendanceActionDto
    {
        public required string RequestorId { get; set; }
    }
}
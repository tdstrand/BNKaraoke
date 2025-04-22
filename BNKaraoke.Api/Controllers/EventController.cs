using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using BNKaraoke.Api.Data;
using BNKaraoke.Api.Models;
using BNKaraoke.Api.Dtos;

namespace BNKaraoke.Api.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class EventController : ControllerBase
    {
        private readonly ApplicationDbContext _context;

        public EventController(ApplicationDbContext context)
        {
            _context = context;
        }

        // POST /api/events: Create a new event
        [HttpPost]
        public async Task<IActionResult> CreateEvent([FromBody] EventCreateDto eventDto)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

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
                QueueCount = 0 // New event, no queue entries yet
            };

            return CreatedAtAction(nameof(GetEvent), new { id = newEvent.EventId }, eventResponse);
        }

        // PUT /api/events/{eventId}: Update an event
        [HttpPut("{eventId}")]
        public async Task<IActionResult> UpdateEvent(int eventId, [FromBody] EventUpdateDto eventDto)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var existingEvent = await _context.Events.FindAsync(eventId);
            if (existingEvent == null)
                return NotFound();

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
            existingEvent.UpdatedAt = DateTime.UtcNow;

            // If the status changes, update the status of associated queue entries
            if (existingEvent.Status != eventDto.Status)
            {
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
                QueueCount = await _context.EventQueues.CountAsync(eq => eq.EventId == eventId)
            };

            return Ok(eventResponse);
        }

        // GET /api/events/{eventId}: Get an event by ID
        [HttpGet("{eventId}")]
        public async Task<IActionResult> GetEvent(int eventId)
        {
            var eventEntity = await _context.Events.FindAsync(eventId);
            if (eventEntity == null)
                return NotFound("Event not found");

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
                QueueCount = await _context.EventQueues.CountAsync(eq => eq.EventId == eventId)
            };

            return Ok(eventResponse);
        }

        // POST /api/events/{eventId}/queue: Add a song to the event queue
        [HttpPost("{eventId}/queue")]
        public async Task<IActionResult> AddToQueue(int eventId, [FromBody] EventQueueCreateDto queueDto)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var eventEntity = await _context.Events.FindAsync(eventId);
            if (eventEntity == null)
                return NotFound("Event not found");

            if (eventEntity.IsCanceled || eventEntity.Visibility != "Visible")
                return BadRequest("Cannot add to queue for a canceled or hidden event");

            // Verify the song exists
            var song = await _context.Songs.FindAsync(queueDto.SongId);
            if (song == null)
                return BadRequest("Song not found");

            // Verify the singer exists
            var singer = await _context.Users.FindAsync(queueDto.SingerId);
            if (singer == null)
                return BadRequest("Singer not found");

            // Check if the singer is checked in
            var attendance = await _context.EventAttendances
                .FirstOrDefaultAsync(ea => ea.EventId == eventId && ea.SingerId == queueDto.SingerId);
            if (attendance == null || !attendance.IsCheckedIn)
                return BadRequest("Singer must be checked in to add to the queue");

            // Get the next position in the queue
            var maxPosition = await _context.EventQueues
                .Where(eq => eq.EventId == eventId)
                .MaxAsync(eq => (int?)eq.Position) ?? 0;

            var newQueueEntry = new EventQueue
            {
                EventId = eventId,
                SongId = queueDto.SongId,
                SingerId = queueDto.SingerId,
                Position = maxPosition + 1,
                Status = eventEntity.Status,
                IsActive = true, // Active since the singer is checked in
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            _context.EventQueues.Add(newQueueEntry);
            await _context.SaveChangesAsync();

            return CreatedAtAction(nameof(GetEventQueue), new { eventId, queueId = newQueueEntry.QueueId }, newQueueEntry);
        }

        // GET /api/events/{eventId}/queue: Retrieve the event queue (with break status)
        [HttpGet("{eventId}/queue")]
        public async Task<IActionResult> GetEventQueue(int eventId)
        {
            var eventEntity = await _context.Events.FindAsync(eventId);
            if (eventEntity == null)
                return NotFound("Event not found");

            var queueEntries = await _context.EventQueues
                .Where(eq => eq.EventId == eventId)
                .Join(
                    _context.EventAttendances,
                    eq => new { eq.EventId, eq.SingerId },
                    ea => new { ea.EventId, ea.SingerId },
                    (eq, ea) => new EventQueueDto
                    {
                        QueueId = eq.QueueId,
                        EventId = eq.EventId,
                        SongId = eq.SongId,
                        SingerId = eq.SingerId,
                        Position = eq.Position,
                        Status = eq.Status,
                        IsActive = eq.IsActive,
                        WasSkipped = eq.WasSkipped,
                        IsCurrentlyPlaying = eq.IsCurrentlyPlaying,
                        SungAt = eq.SungAt,
                        IsOnBreak = ea.IsOnBreak
                    })
                .OrderBy(eq => eq.Position)
                .ToListAsync();

            return Ok(queueEntries);
        }

        // POST /api/events/{eventId}/attendance/check-in: Singer checks in
        [HttpPost("{eventId}/attendance/check-in")]
        public async Task<IActionResult> CheckIn(int eventId, [FromBody] AttendanceActionDto actionDto)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var eventEntity = await _context.Events.FindAsync(eventId);
            if (eventEntity == null)
                return NotFound("Event not found");

            if (eventEntity.IsCanceled || eventEntity.Visibility != "Visible")
                return BadRequest("Cannot check in to a canceled or hidden event");

            var singer = await _context.Users.FindAsync(actionDto.SingerId);
            if (singer == null)
                return BadRequest("Singer not found");

            var attendance = await _context.EventAttendances
                .FirstOrDefaultAsync(ea => ea.EventId == eventId && ea.SingerId == actionDto.SingerId);

            if (attendance == null)
            {
                attendance = new EventAttendance
                {
                    EventId = eventId,
                    SingerId = actionDto.SingerId,
                    IsCheckedIn = true,
                    IsOnBreak = false
                };
                _context.EventAttendances.Add(attendance);
            }
            else
            {
                if (attendance.IsCheckedIn)
                    return BadRequest("Singer is already checked in");

                attendance.IsCheckedIn = true;
                attendance.IsOnBreak = false;
                attendance.BreakStartAt = null;
                attendance.BreakEndAt = null;
            }

            // Log the check-in action
            var attendanceHistory = new EventAttendanceHistory
            {
                EventId = eventId,
                SingerId = actionDto.SingerId,
                Action = "CheckIn",
                ActionTimestamp = DateTime.UtcNow,
                AttendanceId = attendance.AttendanceId
            };
            _context.EventAttendanceHistories.Add(attendanceHistory);

            // Activate the singer's queue entries
            var queueEntries = await _context.EventQueues
                .Where(eq => eq.EventId == eventId && eq.SingerId == actionDto.SingerId)
                .ToListAsync();

            foreach (var entry in queueEntries)
            {
                entry.IsActive = true;
                entry.UpdatedAt = DateTime.UtcNow;
            }

            await _context.SaveChangesAsync();
            return Ok();
        }

        // POST /api/events/{eventId}/attendance/check-out: Singer checks out
        [HttpPost("{eventId}/attendance/check-out")]
        public async Task<IActionResult> CheckOut(int eventId, [FromBody] AttendanceActionDto actionDto)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var eventEntity = await _context.Events.FindAsync(eventId);
            if (eventEntity == null)
                return NotFound("Event not found");

            var attendance = await _context.EventAttendances
                .FirstOrDefaultAsync(ea => ea.EventId == eventId && ea.SingerId == actionDto.SingerId);
            if (attendance == null || !attendance.IsCheckedIn)
                return BadRequest("Singer is not checked in");

            attendance.IsCheckedIn = false;
            attendance.IsOnBreak = false;
            attendance.BreakStartAt = null;
            attendance.BreakEndAt = null;

            // Log the check-out action
            var attendanceHistory = new EventAttendanceHistory
            {
                EventId = eventId,
                SingerId = actionDto.SingerId,
                Action = "CheckOut",
                ActionTimestamp = DateTime.UtcNow,
                AttendanceId = attendance.AttendanceId
            };
            _context.EventAttendanceHistories.Add(attendanceHistory);

            // Deactivate the singer's queue entries
            var queueEntries = await _context.EventQueues
                .Where(eq => eq.EventId == eventId && eq.SingerId == actionDto.SingerId)
                .ToListAsync();

            foreach (var entry in queueEntries)
            {
                entry.IsActive = false;
                entry.UpdatedAt = DateTime.UtcNow;
            }

            await _context.SaveChangesAsync();
            return Ok();
        }

        // POST /api/events/{eventId}/attendance/break/start: Singer takes a break
        [HttpPost("{eventId}/attendance/break/start")]
        public async Task<IActionResult> StartBreak(int eventId, [FromBody] AttendanceActionDto actionDto)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var eventEntity = await _context.Events.FindAsync(eventId);
            if (eventEntity == null)
                return NotFound("Event not found");

            var attendance = await _context.EventAttendances
                .FirstOrDefaultAsync(ea => ea.EventId == eventId && ea.SingerId == actionDto.SingerId);
            if (attendance == null || !attendance.IsCheckedIn)
                return BadRequest("Singer must be checked in to take a break");

            if (attendance.IsOnBreak)
                return BadRequest("Singer is already on break");

            attendance.IsOnBreak = true;
            attendance.BreakStartAt = DateTime.UtcNow;
            attendance.BreakEndAt = null;

            await _context.SaveChangesAsync();
            return Ok();
        }

        // POST /api/events/{eventId}/attendance/break/end: Singer returns from break
        [HttpPost("{eventId}/attendance/break/end")]
        public async Task<IActionResult> EndBreak(int eventId, [FromBody] AttendanceActionDto actionDto)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var eventEntity = await _context.Events.FindAsync(eventId);
            if (eventEntity == null)
                return NotFound("Event not found");

            var attendance = await _context.EventAttendances
                .FirstOrDefaultAsync(ea => ea.EventId == eventId && ea.SingerId == actionDto.SingerId);
            if (attendance == null || !attendance.IsCheckedIn || !attendance.IsOnBreak)
                return BadRequest("Singer must be checked in and on break to end a break");

            attendance.IsOnBreak = false;
            attendance.BreakEndAt = DateTime.UtcNow;

            // Move skipped songs to the top of the queue
            var minPosition = await _context.EventQueues
                .Where(eq => eq.EventId == eventId)
                .MinAsync(eq => (int?)eq.Position) ?? 1;

            var skippedEntries = await _context.EventQueues
                .Where(eq => eq.EventId == eventId && eq.SingerId == actionDto.SingerId && eq.WasSkipped)
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
                .Where(eq => eq.EventId == eventId && (eq.SingerId != actionDto.SingerId || !eq.WasSkipped))
                .OrderBy(eq => eq.Position)
                .ToListAsync();

            for (int i = 0; i < otherEntries.Count; i++)
            {
                otherEntries[i].Position = minPosition + i;
                otherEntries[i].UpdatedAt = DateTime.UtcNow;
            }

            await _context.SaveChangesAsync();
            return Ok();
        }

        // POST /api/events/{eventId}/queue/{queueId}/skip: DJ skips a song
        [HttpPost("{eventId}/queue/{queueId}/skip")]
        public async Task<IActionResult> SkipSong(int eventId, int queueId)
        {
            var eventEntity = await _context.Events.FindAsync(eventId);
            if (eventEntity == null)
                return NotFound("Event not found");

            var queueEntry = await _context.EventQueues
                .FirstOrDefaultAsync(eq => eq.EventId == eventId && eq.QueueId == queueId);
            if (queueEntry == null)
                return NotFound("Queue entry not found");

            var attendance = await _context.EventAttendances
                .FirstOrDefaultAsync(ea => ea.EventId == eventId && ea.SingerId == queueEntry.SingerId);
            if (attendance == null || !attendance.IsOnBreak)
                return BadRequest("Singer must be on break to skip their song");

            queueEntry.WasSkipped = true;
            queueEntry.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();
            return Ok();
        }
    }
}
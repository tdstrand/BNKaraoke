using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using BNKaraoke.Api.Data;
using Microsoft.EntityFrameworkCore;
using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging; // Added for logging

namespace BNKaraoke.Api.Hubs
{
    [Authorize]
    public class KaraokeDJHub : Hub
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<KaraokeDJHub> _logger; // Added for logging

        public KaraokeDJHub(ApplicationDbContext context, ILogger<KaraokeDJHub> logger)
        {
            _context = context;
            _logger = logger;
        }

        public async Task UpdateSingerStatus(string userId, int eventId, string displayName, bool isLoggedIn, bool isJoined, bool isOnBreak)
        {
            _logger.LogInformation("Updating singer status for UserId: {UserId}, EventId: {EventId}", userId, eventId);
            var attendance = await _context.EventAttendances
                .FirstOrDefaultAsync(ea => ea.EventId == eventId && ea.RequestorId == userId);
            if (attendance != null)
            {
                attendance.IsCheckedIn = isJoined;
                attendance.IsOnBreak = isOnBreak;
                attendance.BreakStartAt = isOnBreak ? DateTime.UtcNow : null;
                attendance.BreakEndAt = !isOnBreak && attendance.IsOnBreak ? DateTime.UtcNow : null;
                await _context.SaveChangesAsync();
            }

            await Clients.Group($"Event_{eventId}").SendAsync("SingerStatusUpdated", new
            {
                UserId = userId,
                EventId = eventId,
                DisplayName = displayName,
                IsLoggedIn = isLoggedIn,
                IsJoined = isJoined,
                IsOnBreak = isOnBreak
            });
            _logger.LogInformation("Sent SingerStatusUpdated for UserId: {UserId}, EventId: {EventId}", userId, eventId);
        }

        public async Task UpdateQueue(int queueId, int eventId, string action, string? youTubeUrl = null, string? holdReason = null)
        {
            _logger.LogInformation("Updating queue for QueueId: {QueueId}, EventId: {EventId}, Action: {Action}", queueId, eventId, action);
            await Clients.Group($"Event_{eventId}").SendAsync("QueueUpdated", new
            {
                QueueId = queueId,
                EventId = eventId,
                Action = action,
                YouTubeUrl = youTubeUrl,
                HoldReason = holdReason // Added for hold status
            });
            _logger.LogInformation("Sent QueueUpdated for QueueId: {QueueId}, EventId: {EventId}", queueId, eventId);
        }

        public async Task QueuePlaying(int queueId, int eventId, string? youTubeUrl = null)
        {
            _logger.LogInformation("Notifying queue playing for QueueId: {QueueId}, EventId: {EventId}", queueId, eventId);
            await Clients.Group($"Event_{eventId}").SendAsync("QueuePlaying", new
            {
                QueueId = queueId,
                EventId = eventId,
                YouTubeUrl = youTubeUrl
            });
            _logger.LogInformation("Sent QueuePlaying for QueueId: {QueueId}, EventId: {EventId}", queueId, eventId);
        }

        public async Task JoinEventGroup(int eventId)
        {
            const int maxRetries = 3;
            int retryCount = 0;
            bool joined = false;

            while (retryCount < maxRetries && !joined)
            {
                try
                {
                    _logger.LogInformation("Attempting to join group Event_{EventId} for ConnectionId: {ConnectionId}, Attempt: {Attempt}", eventId, Context.ConnectionId, retryCount + 1);
                    await Groups.AddToGroupAsync(Context.ConnectionId, $"Event_{eventId}");
                    joined = true;
                    _logger.LogInformation("Successfully joined group Event_{EventId} for ConnectionId: {ConnectionId}", eventId, Context.ConnectionId);
                }
                catch (Exception ex)
                {
                    retryCount++;
                    _logger.LogWarning(ex, "Failed to join group Event_{EventId} for ConnectionId: {ConnectionId}, Attempt: {Attempt}", eventId, Context.ConnectionId, retryCount);
                    if (retryCount >= maxRetries)
                    {
                        _logger.LogError(ex, "Max retries reached for joining group Event_{EventId} for ConnectionId: {ConnectionId}", eventId, Context.ConnectionId);
                        throw;
                    }
                    await Task.Delay(5000); // 5-second delay between retries
                }
            }
        }

        public override async Task OnConnectedAsync()
        {
            _logger.LogInformation("Client connected with ConnectionId: {ConnectionId}", Context.ConnectionId);
            await base.OnConnectedAsync();
        }

        public override async Task OnDisconnectedAsync(Exception? exception)
        {
            if (exception != null)
            {
                _logger.LogWarning(exception, "Client disconnected with ConnectionId: {ConnectionId}", Context.ConnectionId);
            }
            else
            {
                _logger.LogInformation("Client disconnected with ConnectionId: {ConnectionId}", Context.ConnectionId);
            }
            await base.OnDisconnectedAsync(exception);
        }
    }
}
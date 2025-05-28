using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using BNKaraoke.Api.Data;
using Microsoft.EntityFrameworkCore;
using System;
using System.Threading.Tasks;

namespace BNKaraoke.Api.Hubs
{
    [Authorize]
    public class KaraokeDJHub : Hub
    {
        private readonly ApplicationDbContext _context;

        public KaraokeDJHub(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task UpdateSingerStatus(string userId, int eventId, string displayName, bool isLoggedIn, bool isJoined, bool isOnBreak)
        {
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

            await Clients.Group($"Event_{eventId}").SendAsync("UpdateSingerStatus", userId, eventId, displayName, isLoggedIn, isJoined, isOnBreak);
        }

        public async Task UpdateQueue(int eventId, int queueId, string status, string? youTubeUrl = null)
        {
            await Clients.Group($"Event_{eventId}").SendAsync("QueueUpdated", queueId, status, youTubeUrl);
        }

        public async Task JoinEventGroup(int eventId)
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, $"Event_{eventId}");
        }
    }
}
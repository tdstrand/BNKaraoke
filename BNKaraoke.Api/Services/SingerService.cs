using BNKaraoke.Api.Data;
using BNKaraoke.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace BNKaraoke.Api.Services;
public class SingerService
{
    private readonly ApplicationDbContext _context;

    public SingerService(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<List<Singer>> GetSingersAsync(int eventId)
    {
        return await _context.Singers
            .Where(s => s.EventId == eventId)
            .ToListAsync();
    }

    public async Task UpdateSingerAsync(string userId, int eventId, string displayName, bool isLoggedIn, bool isJoined, bool isOnBreak)
    {
        var singer = await _context.Singers
            .FirstOrDefaultAsync(s => s.UserId == userId && s.EventId == eventId);
        if (singer == null)
        {
            singer = new Singer { UserId = userId, EventId = eventId, DisplayName = displayName, IsLoggedIn = isLoggedIn, IsJoined = isJoined, IsOnBreak = isOnBreak };
            _context.Singers.Add(singer);
        }
        else
        {
            singer.DisplayName = displayName;
            singer.IsLoggedIn = isLoggedIn;
            singer.IsJoined = isJoined;
            singer.IsOnBreak = isOnBreak;
        }

        var attendance = await _context.EventAttendances
            .FirstOrDefaultAsync(ea => ea.EventId == eventId && ea.RequestorId == userId);
        if (attendance != null)
        {
            attendance.IsCheckedIn = isJoined;
            attendance.IsOnBreak = isOnBreak;
            attendance.BreakStartAt = isOnBreak ? DateTime.UtcNow : null;
            attendance.BreakEndAt = !isOnBreak && attendance.IsOnBreak ? DateTime.UtcNow : null;
        }

        await _context.SaveChangesAsync();
    }
}
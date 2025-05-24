using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using BNKaraoke.Api.Services;

namespace BNKaraoke.Api.Hubs;
[Authorize]
public class SingersHub : Hub
{
    private readonly SingerService _singerService;

    public SingersHub(SingerService singerService)
    {
        _singerService = singerService;
    }

    public async Task UpdateSingerStatus(string userId, int eventId, string displayName, bool isLoggedIn, bool isJoined, bool isOnBreak)
    {
        await _singerService.UpdateSingerAsync(userId, eventId, displayName, isLoggedIn, isJoined, isOnBreak);
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
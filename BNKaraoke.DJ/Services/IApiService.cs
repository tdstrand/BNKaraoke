using System.Collections.Generic;
using System.Threading.Tasks;
using BNKaraoke.DJ.Models;

namespace BNKaraoke.DJ.Services
{
    public interface IApiService
    {
        Task<List<EventDto>> GetLiveEventsAsync();
        Task JoinEventAsync(string eventId, string phoneNumber);
        Task LeaveEventAsync(string eventId, string phoneNumber);
        Task<string> GetDiagnosticAsync();
        Task<LoginResult> LoginAsync(string phoneNumber, string password);
        Task PlayAsync(string eventId, string queueId);
        Task PauseAsync(string eventId, string queueId);
        Task StopAsync(string eventId, string queueId);
        Task SkipAsync(string eventId, string queueId);
        Task LaunchVideoAsync(string eventId, string queueId);
        Task<List<Singer>> GetSingersAsync(string eventId);
    }
}
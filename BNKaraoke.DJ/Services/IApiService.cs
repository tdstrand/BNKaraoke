using System.Collections.Generic;
using System.Threading.Tasks;
using BNKaraoke.DJ.Models;

namespace BNKaraoke.DJ.Services;

public interface IApiService
{
    Task<LoginResult> LoginAsync(string username, string password);
    Task<string> GetDiagnosticAsync();
    Task<List<EventDto>> GetLiveEventsAsync();
    Task JoinEventAsync(string eventId, string userName);
    Task LeaveEventAsync(string eventId, string userName);
}
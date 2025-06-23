using System.Collections.Generic;
using System.Threading.Tasks;
using BNKaraoke.DJ.Models;

// Aliasing our DJ-specific types to avoid ambiguity.
using DjEvent = BNKaraoke.DJ.Models.Event;
using DjEventQueue = BNKaraoke.DJ.Models.EventQueue;

namespace BNKaraoke.DJ.Services
{
    /// <summary>
    /// Provides all API interactions with the BNKaraoke.API.
    /// This interface uses the DJ‑specific models rather than the API's models.
    /// </summary>
    public interface IApiService
    {
        Task<LoginResult> LoginAsync(string username, string password);
        Task<IEnumerable<DjEvent>> GetActiveEventsAsync();
        Task CheckInAsync(int eventId);
        Task CheckOutAsync();
        Task<DjEventQueue> GetEventQueueAsync();
        Task AdvanceSongAsync();
        Task ReorderQueueAsync(IEnumerable<int> newOrder);
        Task RemoveSongAsync(int songId);
        Task PauseQueueAsync(bool pause);
    }
}
using System;
using System.Threading.Tasks;

namespace BNKaraoke.DJ.Services
{
    /// <summary>
    /// Provides real-time communication via SignalR.
    /// </summary>
    public interface ISignalRService
    {
        event Action OnQueueUpdated;
        event Action OnEventStatusChanged;
        // Add more events here as needed (e.g., NewSongAdded, etc.)

        Task ConnectAsync();
        Task DisconnectAsync();
    }
}
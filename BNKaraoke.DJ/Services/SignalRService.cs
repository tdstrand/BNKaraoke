using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.SignalR.Client;

namespace BNKaraoke.DJ.Services
{
    public class SignalRService : ISignalRService
    {
        private HubConnection _hubConnection;
        private readonly string _hubUrl;

        // Mark events as nullable:
        public event Action? OnQueueUpdated;
        public event Action? OnEventStatusChanged;

        public SignalRService()
        {
            _hubUrl = "http://localhost:7290/hub/queue";
            _hubConnection = new HubConnectionBuilder()
                .WithUrl(_hubUrl)
                .WithAutomaticReconnect()
                .Build();

            RegisterHubEvents();
        }

        private void RegisterHubEvents()
        {
            _hubConnection.On("QueueUpdated", () => OnQueueUpdated?.Invoke());
            _hubConnection.On("EventStatusChanged", () => OnEventStatusChanged?.Invoke());
        }

        public async Task ConnectAsync()
        {
            if (_hubConnection.State == HubConnectionState.Disconnected)
            {
                await _hubConnection.StartAsync();
            }
        }

        public async Task DisconnectAsync()
        {
            if (_hubConnection.State != HubConnectionState.Disconnected)
            {
                await _hubConnection.StopAsync();
            }
        }
    }
}
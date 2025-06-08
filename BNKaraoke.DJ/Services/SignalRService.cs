using Microsoft.AspNetCore.SignalR.Client;
using Serilog;
using System;
using System.Threading.Tasks;

namespace BNKaraoke.DJ.Services
{
    public class SignalRService
    {
        private readonly HubConnection _connection;
        private readonly Action<int, string, int?, bool?> _queueUpdated;
        private readonly Action<string, bool, bool, bool> _singerStatusUpdated;
        private readonly IUserSessionService _userSessionService;
        private const int MaxRetries = 3;
        private const int RetryDelayMs = 5000;

        public SignalRService(
            IUserSessionService userSessionService,
            Action<int, string, int?, bool?> queueUpdated,
            Action<string, bool, bool, bool> singerStatusUpdated)
        {
            _userSessionService = userSessionService;
            _queueUpdated = queueUpdated;
            _singerStatusUpdated = singerStatusUpdated;

            _connection = new HubConnectionBuilder()
                .WithUrl("https://api.bnkaraoke.com/hubs/karaoke-dj", options =>
                {
#pragma warning disable CS1998 // Suppress async method lacks await warning
                    options.AccessTokenProvider = () => Task.FromResult(_userSessionService.Token);
#pragma warning restore CS1998
                })
                .WithAutomaticReconnect()
                .Build();

            _connection.On<int, string, int?, bool?>(
                "QueueUpdated", async (queueId, action, position, isOnBreak) =>
                {
                    try
                    {
                        Log.Information("[SIGNALR] QueueUpdated: QueueId={QueueId}, Action={Action}, Position={Position}, IsOnBreak={IsOnBreak}", queueId, action, position, isOnBreak);
                        _queueUpdated(queueId, action, position, isOnBreak);
                    }
                    catch (Exception ex)
                    {
                        Log.Error("[SIGNALR] Failed to process QueueUpdated for QueueId={QueueId}: {Message}", queueId, ex.Message);
                    }
                });

            _connection.On<string, bool, bool, bool>(
                "SingerStatusUpdated", (requestorUserName, isLoggedIn, isJoined, isOnBreak) =>
                {
                    try
                    {
                        Log.Information("[SIGNALR] SingerStatusUpdated: RequestorUserName={RequestorUserName}, IsLoggedIn={IsLoggedIn}, IsJoined={IsJoined}, IsOnBreak={IsOnBreak}",
                            requestorUserName, isLoggedIn, isJoined, isOnBreak);
                        _singerStatusUpdated(requestorUserName, isLoggedIn, isJoined, isOnBreak);
                    }
                    catch (Exception ex)
                    {
                        Log.Error("[SIGNALR] Failed to process SingerStatusUpdated for RequestorUserName={RequestorUserName}: {Message}", requestorUserName, ex.Message);
                    }
                });

            _connection.On<int, int>("QueuePlaying", (queueId, eventId) =>
                Log.Information("[SIGNALR] QueuePlaying ignored: QueueId={QueueId}, EventId={EventId}", queueId, eventId));
        }

        public async Task StartAsync(int eventId)
        {
            try
            {
                Log.Information("[SIGNALR] Starting connection for EventId={EventId}", eventId);
                for (int attempt = 1; attempt <= MaxRetries; attempt++)
                {
                    try
                    {
                        await _connection.StartAsync();
                        await _connection.InvokeAsync("AddToGroup", $"Event_{eventId}");
                        Log.Information("[SIGNALR] Joined group Event_{EventId} on attempt {Attempt}", eventId, attempt);
                        return;
                    }
                    catch (Exception ex)
                    {
                        Log.Error("[SIGNALR] Failed to start connection for EventId={EventId} on attempt {Attempt}: {Message}", eventId, attempt, ex.Message);
                        if (attempt == MaxRetries) throw;
                        await Task.Delay(RetryDelayMs);
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Error("[SIGNALR] Failed to start connection after {MaxRetries} attempts for EventId={EventId}: {Message}", MaxRetries, eventId, ex.Message);
                throw;
            }
        }

        public async Task StopAsync(int eventId)
        {
            try
            {
                Log.Information("[SIGNALR] Stopping connection for EventId={EventId}", eventId);
                for (int attempt = 1; attempt <= MaxRetries; attempt++)
                {
                    try
                    {
                        await _connection.InvokeAsync("RemoveFromGroup", $"Event_{eventId}");
                        await _connection.StopAsync();
                        Log.Information("[SIGNALR] Stopped connection for EventId={EventId} on attempt {Attempt}", eventId, attempt);
                        return;
                    }
                    catch (Exception ex)
                    {
                        Log.Error("[SIGNALR] Failed to stop connection for EventId={EventId} on attempt {Attempt}: {Message}", eventId, attempt, ex.Message);
                        if (attempt == MaxRetries) throw;
                        await Task.Delay(RetryDelayMs);
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Error("[SIGNALR] Failed to stop connection after {MaxRetries} attempts for EventId={EventId}: {Message}", MaxRetries, eventId, ex.Message);
                throw;
            }
        }
    }
}
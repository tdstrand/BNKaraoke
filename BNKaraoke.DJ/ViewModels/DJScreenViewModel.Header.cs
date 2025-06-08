using BNKaraoke.DJ.Views;
using CommunityToolkit.Mvvm.Input;
using Microsoft.AspNetCore.SignalR.Client;
using Serilog;
using System;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;

namespace BNKaraoke.DJ.ViewModels
{
    public partial class DJScreenViewModel
    {
        private async Task InitializeSignalRAsync(string eventId)
        {
            try
            {
                Log.Information("[DJSCREEN SIGNALR] Initializing SignalR connection for EventId={EventId}", eventId);
                _signalRConnection = new HubConnectionBuilder()
                    .WithUrl($"{_settingsService.Settings.ApiUrl}/hubs/karaoke-dj", options =>
                    {
                        options.AccessTokenProvider = () => Task.FromResult(_userSessionService.Token);
                    })
                    .WithAutomaticReconnect()
                    .Build();

                _signalRConnection.On<string, string, string>("QueueUpdated", async (queueId, status, youTubeUrl) =>
                {
                    Log.Information("[DJSCREEN SIGNALR] Received QueueUpdated: QueueId={QueueId}, Status={Status}, YouTubeUrl={YouTubeUrl}", queueId, status, youTubeUrl);
                    await Application.Current.Dispatcher.InvokeAsync(async () =>
                    {
                        try
                        {
                            if (int.TryParse(queueId, out int parsedQueueId))
                            {
                                if (parsedQueueId == 0)
                                {
                                    await LoadQueueData();
                                    await LoadSungCountAsync();
                                    Log.Information("[DJSCREEN SIGNALR] Triggered full queue reload for EventId={EventId}", _currentEventId);
                                }
                                else
                                {
                                    var queueEntry = QueueEntries.FirstOrDefault(q => q.QueueId == parsedQueueId);
                                    if (queueEntry != null)
                                    {
                                        queueEntry.Status = status;
                                        queueEntry.YouTubeUrl = youTubeUrl;
                                        if (!string.IsNullOrEmpty(youTubeUrl) && _videoCacheService != null)
                                        {
                                            queueEntry.IsVideoCached = _videoCacheService.IsVideoCached(queueEntry.SongId);
                                            Log.Information("[DJSCREEN SIGNALR] Checked cache for SongId={SongId}, IsCached={IsCached}, CachePath={CachePath}",
                                                queueEntry.SongId, queueEntry.IsVideoCached, System.IO.Path.Combine(_settingsService.Settings.VideoCachePath, $"{queueEntry.SongId}.mp4"));
                                            if (!queueEntry.IsVideoCached)
                                            {
                                                await _videoCacheService.CacheVideoAsync(youTubeUrl, queueEntry.SongId);
                                                queueEntry.IsVideoCached = _videoCacheService.IsVideoCached(queueEntry.SongId);
                                                Log.Information("[DJSCREEN SIGNALR] Cached video for SongId={SongId}, IsCached={IsCached}", queueEntry.SongId, queueEntry.IsVideoCached);
                                            }
                                        }
                                        Log.Information("[DJSCREEN SIGNALR] Updated queue entry: QueueId={QueueId}, IsVideoCached={IsCached}", queueId, queueEntry.IsVideoCached);
                                    }
                                    else
                                    {
                                        Log.Warning("[DJSCREEN SIGNALR] Queue entry not found: QueueId={QueueId}", queueId);
                                    }
                                }
                            }
                            else
                            {
                                Log.Error("[DJSCREEN SIGNALR] Invalid QueueId format: {QueueId}", queueId);
                            }
                        }
                        catch (Exception ex)
                        {
                            Log.Error("[DJSCREEN SIGNALR] Failed to process QueueUpdated: {Message}", ex.Message);
                            WarningMessage = $"Failed to update queue: {ex.Message}";
                        }
                    });
                });

                _signalRConnection.On<string, bool, bool, bool>("SingerStatusUpdated", async (requestorUserName, isLoggedIn, isJoined, isOnBreak) =>
                {
                    Log.Information("[DJSCREEN SIGNALR] Received SingerStatusUpdated: RequestorUserName={RequestorUserName}", requestorUserName);
                    await Application.Current.Dispatcher.InvokeAsync(async () =>
                    {
                        try
                        {
                            await LoadSingerData();
                            Log.Information("[DJSCREEN SIGNALR] Triggered singer data reload for EventId={EventId}", _currentEventId);
                        }
                        catch (Exception ex)
                        {
                            Log.Error("[DJSCREEN SIGNALR] Failed to process SingerStatusUpdated: {Message}", ex.Message);
                            WarningMessage = $"Failed to update singers: {ex.Message}";
                        }
                    });
                });

                _signalRConnection.Closed += async error =>
                {
                    Log.Error("[DJSCREEN SIGNALR] Connection closed: {Error}", error?.Message);
                    await Task.Delay(5000);
                    await StartSignalRConnectionAsync(eventId);
                };

                await StartSignalRConnectionAsync(eventId);
            }
            catch (Exception ex)
            {
                Log.Error("[DJSCREEN SIGNALR] Failed to initialize SignalR for EventId={EventId}: {Message}", eventId, ex.Message);
                WarningMessage = "Failed to connect to real-time updates.";
            }
        }

        private async Task StartSignalRConnectionAsync(string eventId)
        {
            const int maxRetries = 3;
            int retryCount = 0;

            while (retryCount < maxRetries)
            {
                try
                {
                    if (_signalRConnection != null)
                    {
                        if (_signalRConnection.State != HubConnectionState.Disconnected)
                        {
                            await _signalRConnection.StopAsync();
                            Log.Information("[DJSCREEN SIGNALR] Stopped existing SignalR connection for EventId={EventId}", eventId);
                        }
                        Log.Information("[DJSCREEN SIGNALR] Attempting to connect to KaraokeDJHub for EventId={EventId}, Attempt={Attempt}", eventId, retryCount + 1);
                        using var cts = new System.Threading.CancellationTokenSource(TimeSpan.FromSeconds(10));
                        await _signalRConnection.StartAsync(cts.Token);
                        if (!int.TryParse(eventId, out int eventIdInt))
                        {
                            Log.Error("[DJSCREEN SIGNALR] Invalid EventId format: {EventId}", eventId);
                            break;
                        }
                        await _signalRConnection.InvokeAsync("JoinEventGroup", $"Event_{eventIdInt}", cts.Token);
                        Log.Information("[DJSCREEN SIGNALR] Connected to KaraokeDJHub and joined group for EventId={EventId}", eventId);
                        return;
                    }
                }
                catch (OperationCanceledException)
                {
                    Log.Error("[DJSCREEN SIGNALR] SignalR connection timed out for EventId={EventId}, Attempt={Attempt}", eventId, retryCount + 1);
                }
                catch (Exception ex)
                {
                    Log.Error("[DJSCREEN SIGNALR] Failed to connect to KaraokeDJHub for EventId={EventId}, Attempt={Attempt}: {Message}", eventId, retryCount + 1, ex.Message);
                }

                retryCount++;
                if (retryCount < maxRetries)
                {
                    await Task.Delay(5000 * retryCount);
                }
            }

            Log.Error("[DJSCREEN SIGNALR] Failed to connect to KaraokeDJHub for EventId={EventId} after {MaxRetries} attempts", eventId, maxRetries);
            WarningMessage = "Failed to connect to real-time updates after multiple attempts.";
        }

        private async void UserSessionService_SessionChanged(object? sender, EventArgs e)
        {
            try
            {
                Log.Information("[DJSCREEN] Session changed event received");
                if (!_isLoginWindowOpen)
                {
                    await UpdateAuthenticationState();
                }
                else
                {
                    Log.Information("[DJSCREEN] Skipped UpdateAuthenticationState due to open LoginWindow");
                }
            }
            catch (Exception ex)
            {
                Log.Error("[DJSCREEN] Failed to handle session changed event: {Message}", ex.Message);
                WarningMessage = $"Failed to handle session change: {ex.Message}";
            }
        }

        [RelayCommand]
        private async Task LoginLogout()
        {
            Log.Information("[DJSCREEN] LoginLogout command invoked");
            if (IsAuthenticated)
            {
                Log.Information("[DJSCREEN] Showing logout confirmation");
                var result = MessageBox.Show("Are you sure you want to logout?", "Confirm Logout", MessageBoxButton.YesNo, MessageBoxImage.Question);
                if (result == MessageBoxResult.Yes)
                {
                    Log.Information("[DJSCREEN] Logging out");
                    if (!string.IsNullOrEmpty(_currentEventId))
                    {
                        try
                        {
                            await _apiService.LeaveEventAsync(_currentEventId, _userSessionService.UserName ?? string.Empty);
                            Log.Information("[DJSCREEN] Left event: {EventId}", _currentEventId);
                        }
                        catch (Exception ex)
                        {
                            Log.Error("[DJSCREEN] Failed to leave event: {EventId}: {Message}", _currentEventId, ex.Message);
                            WarningMessage = $"Failed to leave event: {ex.Message}";
                        }
                        _currentEventId = null;
                    }
                    _userSessionService.ClearSession();
                    if (_videoPlayerWindow != null)
                    {
                        _videoPlayerWindow.Close();
                        _videoPlayerWindow = null;
                        IsShowActive = false;
                        ShowButtonText = "Start Show";
                        ShowButtonColor = "#22d3ee";
                    }
                    await UpdateAuthenticationState();
                    Log.Information("[DJSCREEN] Logout complete: IsAuthenticated={IsAuthenticated}, WelcomeMessage={WelcomeMessage}, LoginLogoutButtonText={LoginLogoutButtonText}",
                        IsAuthenticated, WelcomeMessage, LoginLogoutButtonText);
                }
            }
            else
            {
                Log.Information("[DJSCREEN] Showing LoginWindow");
                _isLoginWindowOpen = true;
                try
                {
                    var loginWindow = new LoginWindow { WindowStartupLocation = WindowStartupLocation.CenterScreen };
                    var result = loginWindow.ShowDialog();
                    _isLoginWindowOpen = false;
                    if (result == true)
                    {
                        await UpdateAuthenticationState();
                        Log.Information("[DJSCREEN] LoginWindow closed with successful login: IsAuthenticated={IsAuthenticated}, WelcomeMessage={WelcomeMessage}, LoginLogoutButtonText={LoginLogoutButtonText}",
                            IsAuthenticated, WelcomeMessage, LoginLogoutButtonText);
                    }
                    else
                    {
                        Log.Information("[DJSCREEN] LoginWindow closed without login");
                    }
                }
                catch (Exception ex)
                {
                    Log.Error("[DJSCREEN] Failed to show LoginWindow: {Message}", ex.Message);
                    WarningMessage = $"Failed to show login: {ex.Message}";
                }
                finally
                {
                    _isLoginWindowOpen = false;
                }
            }
        }

        [RelayCommand]
        private async Task JoinLiveEvent()
        {
            Log.Information("[DJSCREEN] JoinLiveEvent command invoked");
            if (string.IsNullOrEmpty(_currentEventId))
            {
                try
                {
                    var events = await _apiService.GetLiveEventsAsync();
                    if (events.Count == 1)
                    {
                        var eventDto = events[0];
                        if (string.IsNullOrEmpty(_userSessionService.UserName))
                        {
                            Log.Error("[DJSCREEN] Cannot join event: UserName is empty");
                            WarningMessage = "Cannot join event: User username is not set.";
                            return;
                        }
                        await _apiService.JoinEventAsync(eventDto.EventId.ToString(), _userSessionService.UserName);
                        _currentEventId = eventDto.EventId.ToString();
                        CurrentEvent = eventDto;
                        JoinEventButtonText = $"Leave {eventDto.EventCode}";
                        JoinEventButtonColor = "#FF0000"; // Red
                        Log.Information("[DJSCREEN] Joined event: {EventId}, {EventCode}", _currentEventId, eventDto.EventCode);

                        await LoadSingerData();
                        await LoadQueueData();
                        await LoadSungCountAsync();
                        await InitializeSignalRAsync(_currentEventId);
                    }
                    else if (events.Count > 1)
                    {
                        Log.Information("[DJSCREEN] Multiple live events; showing dropdown (placeholder)");
                        WarningMessage = "Multiple live events; dropdown not implemented";
                    }
                    else
                    {
                        Log.Information("[DJSCREEN] No live events available");
                        WarningMessage = "No live events are currently available.";
                    }
                }
                catch (Exception ex)
                {
                    Log.Error("[DJSCREEN] Failed to join event: {Message}", ex.Message);
                    WarningMessage = $"Failed to join event: {ex.Message}";
                }
            }
            else
            {
                try
                {
                    if (string.IsNullOrEmpty(_userSessionService.UserName))
                    {
                        Log.Error("[DJSCREEN] Cannot leave event: UserName is empty");
                        WarningMessage = "Cannot leave event: User username is not set.";
                        return;
                    }
                    await _apiService.LeaveEventAsync(_currentEventId, _userSessionService.UserName);
                    Log.Information("[DJSCREEN] Left event: {EventId}", _currentEventId);
                    _currentEventId = null;
                    CurrentEvent = null;
                    QueueEntries.Clear();
                    Singers.Clear();
                    GreenSingers.Clear();
                    YellowSingers.Clear();
                    OrangeSingers.Clear();
                    RedSingers.Clear();
                    NonDummySingersCount = 0;
                    SungCount = 0;
                    if (_videoPlayerWindow != null)
                    {
                        _videoPlayerWindow.Close();
                        _videoPlayerWindow = null;
                        IsShowActive = false;
                        ShowButtonText = "Start Show";
                        ShowButtonColor = "#22d3ee";
                    }
                    PlayingQueueEntry = null;
                    OnPropertyChanged(nameof(NonDummySingersCount));
                    OnPropertyChanged(nameof(SungCount));
                    if (_signalRConnection != null)
                    {
                        await _signalRConnection.StopAsync();
                        _signalRConnection = null;
                        Log.Information("[DJSCREEN SIGNALR] Disconnected from KaraokeDJHub");
                    }
                    await UpdateAuthenticationState();
                }
                catch (Exception ex)
                {
                    Log.Error("[DJSCREEN] Failed to leave event: {EventId}: {Message}", _currentEventId, ex.Message);
                    WarningMessage = $"Failed to leave event: {ex.Message}";
                }
            }
        }

        [RelayCommand]
        private void OpenSettings()
        {
            Log.Information("[DJSCREEN] Settings button clicked");
            try
            {
                var settingsWindow = new SettingsWindow { WindowStartupLocation = WindowStartupLocation.CenterScreen };
                settingsWindow.ShowDialog();
                Log.Information("[DJSCREEN] SettingsWindow closed");
            }
            catch (Exception ex)
            {
                Log.Error("[DJSCREEN] Failed to open SettingsWindow: {Message}", ex.Message);
                WarningMessage = $"Failed to open settings: {ex.Message}";
            }
        }

        public async Task UpdateAuthenticationState()
        {
            try
            {
                Log.Information("[DJSCREEN] Updating authentication state");
                bool newIsAuthenticated = _userSessionService.IsAuthenticated;
                string newWelcomeMessage = newIsAuthenticated ? $"Welcome, {_userSessionService.FirstName ?? "User"}" : "Not logged in";
                string newLoginLogoutButtonText = newIsAuthenticated ? "Logout" : "Login";
                string newLoginLogoutButtonColor = newIsAuthenticated ? "#FF0000" : "#3B82F6"; // Red for logout, Blue for login
                bool newIsJoinEventButtonVisible = newIsAuthenticated;

                IsAuthenticated = newIsAuthenticated;
                WelcomeMessage = newWelcomeMessage;
                LoginLogoutButtonText = newLoginLogoutButtonText;
                LoginLogoutButtonColor = newLoginLogoutButtonColor;
                IsJoinEventButtonVisible = newIsJoinEventButtonVisible;

                if (!newIsAuthenticated)
                {
                    JoinEventButtonText = "No Live Events";
                    JoinEventButtonColor = "Gray";
                    _currentEventId = null;
                    CurrentEvent = null;
                    QueueEntries.Clear();
                    Singers.Clear();
                    GreenSingers.Clear();
                    YellowSingers.Clear();
                    OrangeSingers.Clear();
                    RedSingers.Clear();
                    NonDummySingersCount = 0;
                    SungCount = 0;
                    if (_videoPlayerWindow != null)
                    {
                        _videoPlayerWindow.Close();
                        _videoPlayerWindow = null;
                        IsShowActive = false;
                        ShowButtonText = "Start Show";
                        ShowButtonColor = "#22d3ee";
                    }
                    PlayingQueueEntry = null;
                    if (_signalRConnection != null)
                    {
                        await _signalRConnection.StopAsync();
                        _signalRConnection = null;
                        Log.Information("[DJSCREEN SIGNALR] Disconnected from KaraokeDJHub on logout");
                    }
                }
                else if (string.IsNullOrEmpty(_currentEventId) && !_isLoginWindowOpen)
                {
                    await UpdateJoinEventButtonState();
                }

                Application.Current.Dispatcher.Invoke(() =>
                {
                    OnPropertyChanged(nameof(IsAuthenticated));
                    OnPropertyChanged(nameof(WelcomeMessage));
                    OnPropertyChanged(nameof(LoginLogoutButtonText));
                    OnPropertyChanged(nameof(LoginLogoutButtonColor));
                    OnPropertyChanged(nameof(IsJoinEventButtonVisible));
                    OnPropertyChanged(nameof(JoinEventButtonText));
                    OnPropertyChanged(nameof(JoinEventButtonColor));
                    OnPropertyChanged(nameof(CurrentEvent));
                    OnPropertyChanged(nameof(QueueEntries));
                    OnPropertyChanged(nameof(Singers));
                    OnPropertyChanged(nameof(GreenSingers));
                    OnPropertyChanged(nameof(YellowSingers));
                    OnPropertyChanged(nameof(OrangeSingers));
                    OnPropertyChanged(nameof(RedSingers));
                    OnPropertyChanged(nameof(NonDummySingersCount));
                    OnPropertyChanged(nameof(ShowButtonText));
                    OnPropertyChanged(nameof(ShowButtonColor));
                    OnPropertyChanged(nameof(IsShowActive));
                    OnPropertyChanged(nameof(PlayingQueueEntry));
                    OnPropertyChanged(nameof(SungCount));
                });

                Log.Information("[DJSCREEN] Authentication state updated: IsAuthenticated={IsAuthenticated}, WelcomeMessage={WelcomeMessage}, LoginLogoutButtonText={LoginLogoutButtonText}, IsJoinEventButtonVisible={IsJoinEventButtonVisible}",
                    IsAuthenticated, WelcomeMessage, LoginLogoutButtonText, IsJoinEventButtonVisible);
            }
            catch (Exception ex)
            {
                Log.Error("[DJSCREEN] Failed to update authentication state: {Message}", ex.Message);
                WarningMessage = $"Failed to update authentication: {ex.Message}";
            }
        }

        private async Task UpdateJoinEventButtonState()
        {
            try
            {
                Log.Information("[DJSCREEN] Updating join event button state");
                if (!IsAuthenticated)
                {
                    JoinEventButtonText = "No Live Events";
                    JoinEventButtonColor = "Gray";
                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        OnPropertyChanged(nameof(JoinEventButtonText));
                        OnPropertyChanged(nameof(JoinEventButtonColor));
                    });
                    return;
                }

                var events = await _apiService.GetLiveEventsAsync();
                Application.Current.Dispatcher.Invoke(() =>
                {
                    if (events.Count == 0)
                    {
                        JoinEventButtonText = "No Live Events";
                        JoinEventButtonColor = "Gray";
                    }
                    else if (events.Count == 1)
                    {
                        JoinEventButtonText = string.IsNullOrEmpty(_currentEventId) ? $"Join {events[0].EventCode}" : $"Leave {events[0].EventCode}";
                        JoinEventButtonColor = string.IsNullOrEmpty(_currentEventId) ? "#3B82F6" : "#FF0000";
                    }
                    else
                    {
                        JoinEventButtonText = "Join Live Event";
                        JoinEventButtonColor = "#3B82F6";
                    }
                    Log.Information("[DJSCREEN] Join event button updated: JoinEventButtonText={JoinEventButtonText}, JoinEventButtonColor={JoinEventButtonColor}, EventCount={EventCount}",
                        JoinEventButtonText, JoinEventButtonColor, events.Count);

                    OnPropertyChanged(nameof(JoinEventButtonText));
                    OnPropertyChanged(nameof(JoinEventButtonColor));
                });
            }
            catch (Exception ex)
            {
                Log.Error("[DJSCREEN] Failed to update join event button state: {Message}", ex.Message);
                WarningMessage = $"Failed to update event button: {ex.Message}";
            }
        }

        private async Task LoadSungCountAsync()
        {
            try
            {
                if (!string.IsNullOrEmpty(_currentEventId))
                {
                    SungCount = await _apiService.GetSungCountAsync(_currentEventId);
                    Log.Information("[DJSCREEN] Loaded sung count {Count} for EventId={EventId}", SungCount, _currentEventId);
                }
            }
            catch (Exception ex)
            {
                Log.Error("[DJSCREEN] Failed to load sung count for EventId={EventId}: {Message}", _currentEventId, ex.Message);
                WarningMessage = $"Failed to load sung count: {ex.Message}";
            }
        }
    }
}
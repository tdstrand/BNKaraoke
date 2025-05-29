using BNKaraoke.DJ.Models;
using BNKaraoke.DJ.Services;
using BNKaraoke.DJ.Views;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.AspNetCore.WebUtilities;
using Serilog;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace BNKaraoke.DJ.ViewModels;

#pragma warning disable CS8622 // Suppress for event handlers
public partial class DJScreenViewModel : ObservableObject
{
    private readonly IUserSessionService _userSessionService = UserSessionService.Instance;
    private readonly IApiService _apiService = new ApiService(UserSessionService.Instance, SettingsService.Instance);
    private readonly SettingsService _settingsService = SettingsService.Instance;
    private readonly VideoCacheService? _videoCacheService;
    private string? _currentEventId;
    private HubConnection? _signalRConnection;

    [ObservableProperty]
    private bool _isAuthenticated;

    [ObservableProperty]
    private string _welcomeMessage = "Not logged in";

    [ObservableProperty]
    private string _loginLogoutButtonText = "Login";

    [ObservableProperty]
    private string _loginLogoutButtonColor = "#3B82F6"; // Blue

    [ObservableProperty]
    private string _joinEventButtonText = "No Live Events";

    [ObservableProperty]
    private string _joinEventButtonColor = "Gray"; // Disabled

    [ObservableProperty]
    private bool _isJoinEventButtonVisible;

    [ObservableProperty]
    private EventDto? _currentEvent;

    [ObservableProperty]
    private ObservableCollection<QueueEntry> _queueEntries = new();

    [ObservableProperty]
    private QueueEntry? _selectedQueueEntry;

    [ObservableProperty]
    private bool _isPlaying;

    [ObservableProperty]
    private ObservableCollection<Singer> _singers = new();

    [ObservableProperty]
    private int _nonDummySingersCount;

    [ObservableProperty]
    private ObservableCollection<Singer> _greenSingers = new();

    [ObservableProperty]
    private ObservableCollection<Singer> _yellowSingers = new();

    [ObservableProperty]
    private ObservableCollection<Singer> _orangeSingers = new();

    [ObservableProperty]
    private ObservableCollection<Singer> _redSingers = new();

    public DJScreenViewModel(VideoCacheService? videoCacheService = null)
    {
        try
        {
            Log.Information("[DJSCREEN VM] Starting ViewModel constructor");
            _videoCacheService = videoCacheService;
            _userSessionService.SessionChanged += UserSessionService_SessionChanged;
            Log.Information("[DJSCREEN VM] Subscribed to SessionChanged event");
            UpdateAuthenticationState();
            Log.Information("[DJSCREEN VM] Called UpdateAuthenticationState in constructor");
            Log.Information("[DJSCREEN VM] ViewModel instance created: {InstanceId}", GetHashCode());
        }
        catch (Exception ex)
        {
            Log.Error("[DJSCREEN VM] Failed to initialize ViewModel: {Message}", ex.Message);
            MessageBox.Show($"Failed to initialize DJScreen: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private async Task InitializeSignalRAsync(string eventId)
    {
        try
        {
            Log.Information("[DJSCREEN SIGNALR] Initializing SignalR connection for EventId={EventId}", eventId);
            _signalRConnection = new HubConnectionBuilder()
                .WithUrl("http://localhost:7290/hubs/karaoke-DJ", options =>
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
                                // Full queue reload
                                await LoadQueueData();
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
                                        if (!queueEntry.IsVideoCached)
                                        {
                                            await _videoCacheService.CacheVideoAsync(youTubeUrl, queueEntry.SongId);
                                            queueEntry.IsVideoCached = _videoCacheService.IsVideoCached(queueEntry.SongId);
                                        }
                                    }
                                    Log.Information("[DJSCREEN SIGNALR] Updated queue entry: QueueId={QueueId}", queueId);
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
            await Application.Current.Dispatcher.InvokeAsync(() =>
            {
                MessageBox.Show("Failed to connect to real-time updates.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            });
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
                    Log.Information("[DJSCREEN SIGNALR] Attempting to connect to KaraokeDJHub for EventId={EventId}, Attempt={Attempt}", eventId, retryCount + 1);
                    using var cts = new System.Threading.CancellationTokenSource(TimeSpan.FromSeconds(10));
                    await _signalRConnection.StartAsync(cts.Token);
                    if (!int.TryParse(eventId, out int eventIdInt))
                    {
                        Log.Error("[DJSCREEN SIGNALR] Invalid EventId format: {EventId}", eventId);
                        break;
                    }
                    await _signalRConnection.InvokeAsync("JoinEventGroup", eventIdInt, cts.Token);
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
                await Task.Delay(5000 * retryCount); // Exponential backoff
            }
        }

        Log.Error("[DJSCREEN SIGNALR] Failed to connect to KaraokeDJHub for EventId={EventId} after {MaxRetries} attempts", eventId, maxRetries);
    }

    private void SortSingers()
    {
        try
        {
            Log.Information("[DJSCREEN] Sorting singers");
            var sortedSingers = Singers.OrderBy(singer =>
            {
                if (singer.IsLoggedIn && singer.IsJoined && !singer.IsOnBreak) return 1; // Green
                if (singer.IsLoggedIn && singer.IsJoined && singer.IsOnBreak) return 2; // Yellow
                if (singer.IsLoggedIn && !singer.IsJoined) return 3; // Orange
                return 4; // Red
            }).ThenBy(singer => singer.DisplayName).ToList();

            Application.Current.Dispatcher.Invoke(() =>
            {
                Singers.Clear();
                foreach (var singer in sortedSingers)
                {
                    Singers.Add(singer);
                }

                GreenSingers.Clear();
                YellowSingers.Clear();
                OrangeSingers.Clear();
                RedSingers.Clear();

                foreach (var singer in Singers)
                {
                    if (singer.IsLoggedIn && singer.IsJoined && !singer.IsOnBreak)
                        GreenSingers.Add(singer);
                    else if (singer.IsLoggedIn && singer.IsJoined && singer.IsOnBreak)
                        YellowSingers.Add(singer);
                    else if (singer.IsLoggedIn && !singer.IsJoined)
                        OrangeSingers.Add(singer);
                    else
                        RedSingers.Add(singer);
                }

                NonDummySingersCount = Singers.Count;
                OnPropertyChanged(nameof(NonDummySingersCount));
                OnPropertyChanged(nameof(GreenSingers));
                OnPropertyChanged(nameof(YellowSingers));
                OnPropertyChanged(nameof(OrangeSingers));
                OnPropertyChanged(nameof(RedSingers));
                Log.Information("[DJSCREEN] Sorted singers: {Count} singers, Names={Names}",
                    NonDummySingersCount, string.Join(", ", Singers.Select(s => s.DisplayName)));
            });
        }
        catch (Exception ex)
        {
            Log.Error("[DJSCREEN] Failed to sort singers: {Message}", ex.Message);
        }
    }

    private void UserSessionService_SessionChanged(object? sender, EventArgs e)
    {
        try
        {
            Log.Information("[DJSCREEN] Session changed event received");
            UpdateAuthenticationState();
        }
        catch (Exception ex)
        {
            Log.Error("[DJSCREEN] Failed to handle session changed event: {Message}", ex.Message);
        }
    }

    [RelayCommand]
    private void LoginLogout()
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
                        _apiService.LeaveEventAsync(_currentEventId, _userSessionService.UserName ?? string.Empty).GetAwaiter().GetResult();
                        Log.Information("[DJSCREEN] Left event: {EventId}", _currentEventId);
                    }
                    catch (Exception ex)
                    {
                        Log.Error("[DJSCREEN] Failed to leave event: {EventId}: {Message}", _currentEventId, ex.Message);
                    }
                    _currentEventId = null;
                }
                _userSessionService.ClearSession();
                UpdateAuthenticationState();
                Log.Information("[DJSCREEN] Logout complete: IsAuthenticated={IsAuthenticated}, WelcomeMessage={WelcomeMessage}, LoginLogoutButtonText={LoginLogoutButtonText}",
                    IsAuthenticated, WelcomeMessage, LoginLogoutButtonText);
            }
        }
        else
        {
            Log.Information("[DJSCREEN] Showing LoginWindow");
            var loginWindow = new LoginWindow { WindowStartupLocation = WindowStartupLocation.CenterScreen };
            loginWindow.ShowDialog();
            UpdateAuthenticationState();
            Log.Information("[DJSCREEN] LoginWindow closed: IsAuthenticated={IsAuthenticated}, WelcomeMessage={WelcomeMessage}, LoginLogoutButtonText={LoginLogoutButtonText}",
                IsAuthenticated, WelcomeMessage, LoginLogoutButtonText);
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
                        MessageBox.Show("Cannot join event: User username is not set.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
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
                    await InitializeSignalRAsync(_currentEventId);
                }
                else if (events.Count > 1)
                {
                    Log.Information("[DJSCREEN] Multiple live events; showing dropdown (placeholder)");
                    MessageBox.Show("Multiple live events; dropdown not implemented", "Join Event", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                else
                {
                    Log.Information("[DJSCREEN] No live events available");
                    MessageBox.Show("No live events are currently available.", "No Events", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            catch (HttpRequestException ex)
            {
                Log.Error("[DJSCREEN] Failed to join event: Status={StatusCode}, Message={Message}", ex.StatusCode, ex.Message);
                MessageBox.Show($"Failed to join event: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            catch (Exception ex)
            {
                Log.Error("[DJSCREEN] Failed to join event: {Message}", ex.Message);
                MessageBox.Show($"Failed to join event: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        else
        {
            try
            {
                if (string.IsNullOrEmpty(_userSessionService.UserName))
                {
                    Log.Error("[DJSCREEN] Cannot leave event: UserName is empty");
                    MessageBox.Show("Cannot leave event: User username is not set.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
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
                OnPropertyChanged(nameof(NonDummySingersCount));
                if (_signalRConnection != null)
                {
                    await _signalRConnection.StopAsync();
                    _signalRConnection = null;
                    Log.Information("[DJSCREEN SIGNALR] Disconnected from KaraokeDJHub");
                }
                await UpdateJoinEventButtonState();
            }
            catch (Exception ex)
            {
                Log.Error("[DJSCREEN] Failed to leave event: {EventId}: {Message}", _currentEventId, ex.Message);
                MessageBox.Show($"Failed to leave event: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }

    [RelayCommand]
    private void OpenSettings()
    {
        Log.Information("[DJSCREEN] Settings button clicked");
        var settingsWindow = new SettingsWindow { WindowStartupLocation = WindowStartupLocation.CenterScreen };
        settingsWindow.ShowDialog();
        Log.Information("[DJSCREEN] SettingsWindow closed");
    }

    [RelayCommand]
    private void ShowSongDetails()
    {
        Log.Information("[DJSCREEN] ShowSongDetails command invoked");
        if (SelectedQueueEntry != null)
        {
            var songDetailsWindow = new SongDetailsWindow
            {
                WindowStartupLocation = WindowStartupLocation.CenterScreen,
                DataContext = new SongDetailsViewModel { SelectedQueueEntry = SelectedQueueEntry }
            };
            songDetailsWindow.ShowDialog();
            Log.Information("[DJSCREEN] SongDetailsWindow closed");
        }
        else
        {
            Log.Information("[DJSCREEN] No queue entry selected for song details");
            MessageBox.Show("Please select a song to view details.", "No Selection", MessageBoxButton.OK, MessageBoxImage.Information);
        }
    }

    [RelayCommand]
    private async Task Play()
    {
        Log.Information("[DJSCREEN] Play/Pause command invoked");
        if (SelectedQueueEntry != null && !string.IsNullOrEmpty(_currentEventId))
        {
            try
            {
                if (IsPlaying)
                {
                    await _apiService.PauseAsync(_currentEventId, SelectedQueueEntry.QueueId.ToString());
                    Log.Information("[DJSCREEN] Pause request sent for event {EventId}, queue {QueueId}: {SongTitle}", _currentEventId, SelectedQueueEntry.QueueId, SelectedQueueEntry.SongTitle);
                    MessageBox.Show($"Paused {SelectedQueueEntry.SongTitle}", "Pause", MessageBoxButton.OK, MessageBoxImage.Information);
                    IsPlaying = false;
                }
                else
                {
                    await _apiService.PlayAsync(_currentEventId, SelectedQueueEntry.QueueId.ToString());
                    Log.Information("[DJSCREEN] Play request sent for event {EventId}, queue {QueueId}: {SongTitle}", _currentEventId, SelectedQueueEntry.QueueId, SelectedQueueEntry.SongTitle);
                    MessageBox.Show($"Playing {SelectedQueueEntry.SongTitle}", "Play", MessageBoxButton.OK, MessageBoxImage.Information);
                    IsPlaying = true;
                }
            }
            catch (Exception ex)
            {
                Log.Error("[DJSCREEN] Failed to {Action} queue {QueueId}: {Message}", IsPlaying ? "pause" : "play", SelectedQueueEntry.QueueId, ex.Message);
                MessageBox.Show($"Failed to {(IsPlaying ? "pause" : "play")}: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        else
        {
            Log.Information("[DJSCREEN] Play/Pause failed: No queue entry selected or no event joined");
            MessageBox.Show("Please select a song and join an event.", "No Selection", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    [RelayCommand]
    private async Task Stop()
    {
        Log.Information("[DJSCREEN] Stop command invoked");
        if (SelectedQueueEntry != null && !string.IsNullOrEmpty(_currentEventId))
        {
            try
            {
                await _apiService.StopAsync(_currentEventId, SelectedQueueEntry.QueueId.ToString());
                Log.Information("[DJSCREEN] Stop request sent for event {EventId}, queue {QueueId}: {SongTitle}", _currentEventId, SelectedQueueEntry.QueueId, SelectedQueueEntry.SongTitle);
                MessageBox.Show($"Stopped {SelectedQueueEntry.SongTitle}", "Stop", MessageBoxButton.OK, MessageBoxImage.Information);
                IsPlaying = false;
            }
            catch (Exception ex)
            {
                Log.Error("[DJSCREEN] Failed to stop queue {QueueId}: {Message}", SelectedQueueEntry.QueueId, ex.Message);
                MessageBox.Show($"Failed to stop: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        else
        {
            Log.Information("[DJSCREEN] Stop failed: No queue entry selected or no event joined");
            MessageBox.Show("Please select a song and join an event.", "No Selection", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    [RelayCommand]
    private async Task Skip()
    {
        Log.Information("[DJSCREEN] Skip command invoked");
        if (SelectedQueueEntry != null && !string.IsNullOrEmpty(_currentEventId))
        {
            try
            {
                await _apiService.SkipAsync(_currentEventId, SelectedQueueEntry.QueueId.ToString());
                Log.Information("[DJSCREEN] Skip request sent for event {EventId}, queue {QueueId}: {SongTitle}", _currentEventId, SelectedQueueEntry.QueueId, SelectedQueueEntry.SongTitle);
                MessageBox.Show($"Skipped {SelectedQueueEntry.SongTitle}", "Skip", MessageBoxButton.OK, MessageBoxImage.Information);
                IsPlaying = false;
                await LoadQueueData();
            }
            catch (Exception ex)
            {
                Log.Error("[DJSCREEN] Failed to skip queue {QueueId}: {Message}", SelectedQueueEntry.QueueId, ex.Message);
                MessageBox.Show($"Failed to skip: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        else
        {
            Log.Information("[DJSCREEN] Skip failed: No queue entry selected or no event joined");
            MessageBox.Show("Please select a song and join an event.", "No Selection", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    [RelayCommand]
    private async Task LaunchVideo()
    {
        Log.Information("[DJSCREEN] LaunchVideo command invoked");
        if (SelectedQueueEntry != null && !string.IsNullOrEmpty(_currentEventId))
        {
            try
            {
                await _apiService.LaunchVideoAsync(_currentEventId, SelectedQueueEntry.QueueId.ToString());
                Log.Information("[DJSCREEN] Launch video request sent for event {EventId}, queue {QueueId}: {SongTitle}", _currentEventId, SelectedQueueEntry.QueueId, SelectedQueueEntry.SongTitle);
                MessageBox.Show($"Launched video for {SelectedQueueEntry.SongTitle}", "Launch Video", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                Log.Error("[DJSCREEN] Failed to launch video for queue {QueueId}: {Message}", SelectedQueueEntry.QueueId, ex.Message);
                MessageBox.Show($"Failed to launch video: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        else
        {
            Log.Information("[DJSCREEN] Launch video failed: No queue entry selected or no event joined");
            MessageBox.Show("Please select a song and join an event.", "No Selection", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    [RelayCommand]
    private void StartDrag(QueueEntry draggedItem)
    {
        try
        {
            Log.Information("[DJSCREEN] StartDrag command invoked for QueueId={QueueId}", draggedItem?.QueueId ?? -1);
            if (draggedItem == null)
            {
                Log.Error("[DJSCREEN] Drag failed: Dragged item is null");
                return;
            }

            var listView = Application.Current.Windows.OfType<DJScreen>()
                .Select(w => w.FindName("QueueListView") as ListView)
                .FirstOrDefault(lv => lv != null);

            if (listView == null)
            {
                Log.Error("[DJSCREEN] Drag failed: QueueListView not found");
                return;
            }

            Log.Information("[DJSCREEN] Initiating DragDrop for queue {QueueId}", draggedItem.QueueId);
            var data = new DataObject(typeof(QueueEntry), draggedItem);
            DragDrop.DoDragDrop(listView, data, DragDropEffects.Move);
            Log.Information("[DJSCREEN] Completed drag for queue {QueueId}", draggedItem.QueueId);
        }
        catch (Exception ex)
        {
            Log.Error("[DJSCREEN] Drag failed: {Message}, StackTrace={StackTrace}", ex.Message, ex.StackTrace);
            Application.Current.Dispatcher.Invoke(() =>
            {
                MessageBox.Show($"Drag failed: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            });
        }
    }

    [RelayCommand]
    private async Task Drop(DragEventArgs e)
    {
        try
        {
            Log.Information("[DJSCREEN] Drop command invoked: SourceType={SourceType}, Handled={Handled}", e?.Source?.GetType().Name ?? "null", e?.Handled ?? false);
            if (string.IsNullOrEmpty(_currentEventId))
            {
                Log.Warning("[DJSCREEN] Drop failed: No event joined");
                return;
            }

            if (e == null)
            {
                Log.Error("[DJSCREEN] Drop failed: DragEventArgs is null");
                return;
            }

            Log.Information("[DJSCREEN] Accessing dragged data");
            var draggedItem = e.Data.GetData(typeof(QueueEntry)) as QueueEntry;
            if (draggedItem == null)
            {
                Log.Warning("[DJSCREEN] Drop failed: Dragged item is null or not a QueueEntry");
                return;
            }

            Log.Information("[DJSCREEN] Accessing target element");
            var target = e.OriginalSource as FrameworkElement;
            var targetItem = target?.DataContext as QueueEntry;

            if (targetItem == null)
            {
                Log.Warning("[DJSCREEN] Drop failed: Target item is null or not a QueueEntry, OriginalSourceType={OriginalSourceType}", e.OriginalSource?.GetType().Name);
                return;
            }

            if (draggedItem == targetItem)
            {
                Log.Information("[DJSCREEN] Drop ignored: Dragged item is the same as target");
                return;
            }

            Log.Information("[DJSCREEN] Calculating indices for queue {QueueId}", draggedItem.QueueId);
            int sourceIndex = QueueEntries.IndexOf(draggedItem);
            int targetIndex = QueueEntries.IndexOf(targetItem);

            if (sourceIndex < 0 || targetIndex < 0)
            {
                Log.Warning("[DJSCREEN] Drop failed: Invalid source or target index, SourceIndex={SourceIndex}, TargetIndex={TargetIndex}", sourceIndex, targetIndex);
                return;
            }

            Log.Information("[DJSCREEN] Reordering queue locally");
            Application.Current.Dispatcher.Invoke(() =>
            {
                QueueEntries.Move(sourceIndex, targetIndex);
                for (int i = 0; i < QueueEntries.Count; i++)
                {
                    QueueEntries[i].Position = i + 1;
                }
            });

            var queueIds = QueueEntries.Select(q => q.QueueId.ToString()).ToList();
            Log.Information("[DJSCREEN] Reorder payload: EventId={EventId}, QueueIds={QueueIds}", _currentEventId, string.Join(",", queueIds));

            try
            {
                await _apiService.ReorderQueueAsync(_currentEventId, queueIds);
                Log.Information("[DJSCREEN] Queue reordered for event {EventId}, dropped {SourceQueueId} to position {TargetIndex}",
                    _currentEventId, draggedItem.QueueId, targetIndex + 1);

                // Refresh queue from backend to ensure positions are correct
                await LoadQueueData();
                Log.Information("[DJSCREEN] Refreshed queue data after reorder for event {EventId}", _currentEventId);
            }
            catch (HttpRequestException ex)
            {
                Log.Error("[DJSCREEN] Failed to persist queue order: Status={StatusCode}, Error={Error}", ex.StatusCode, ex.Message);
                Application.Current.Dispatcher.Invoke(() =>
                {
                    MessageBox.Show($"Failed to reorder queue: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                });
                await LoadQueueData(); // Refresh to revert to server state
            }
        }
        catch (Exception ex)
        {
            Log.Error("[DJSCREEN] Drop failed: {Message}, StackTrace={StackTrace}", ex.Message, ex.StackTrace);
            Application.Current.Dispatcher.Invoke(() =>
            {
                MessageBox.Show($"Failed to reorder queue: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            });
        }
    }

    public async void UpdateAuthenticationState()
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
                if (_signalRConnection != null)
                {
                    await _signalRConnection.StopAsync();
                    _signalRConnection = null;
                    Log.Information("[DJSCREEN SIGNALR] Disconnected from KaraokeDJHub on logout");
                }
            }
            else if (string.IsNullOrEmpty(_currentEventId))
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
            });

            Log.Information("[DJSCREEN] Authentication state updated: IsAuthenticated={IsAuthenticated}, WelcomeMessage={WelcomeMessage}, LoginLogoutButtonText={LoginLogoutButtonText}, IsJoinEventButtonVisible={IsJoinEventButtonVisible}",
                IsAuthenticated, WelcomeMessage, LoginLogoutButtonText, IsJoinEventButtonVisible);
        }
        catch (Exception ex)
        {
            Log.Error("[DJSCREEN] Failed to update authentication state: {Message}", ex.Message);
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
        catch (UnauthorizedAccessException ex)
        {
            Log.Error("[DJSCREEN] Authentication error updating join event button state: {Message}", ex.Message);
            Application.Current.Dispatcher.Invoke(() =>
            {
                MessageBox.Show("Authentication failed. Please re-login.", "Authentication Error", MessageBoxButton.OK, MessageBoxImage.Error);
                _userSessionService.ClearSession();
                UpdateAuthenticationState();
            });
        }
        catch (Exception ex)
        {
            Log.Error("[DJSCREEN] Failed to update join event button state: {Message}", ex.Message);
            Application.Current.Dispatcher.Invoke(() =>
            {
                JoinEventButtonText = "No Live Events";
                JoinEventButtonColor = "Gray";
                OnPropertyChanged(nameof(JoinEventButtonText));
                OnPropertyChanged(nameof(JoinEventButtonColor));
            });
        }
    }

    private async Task LoadQueueData()
    {
        try
        {
            Log.Information("[DJSCREEN] Loading queue data for event: {EventId}", _currentEventId);
            if (string.IsNullOrEmpty(_currentEventId))
            {
                Log.Information("[DJSCREEN] No event joined, skipping queue data load");
                QueueEntries.Clear();
                return;
            }

            var queueEntries = await _apiService.GetQueueAsync(_currentEventId);
            await Application.Current.Dispatcher.InvokeAsync(async () =>
            {
                QueueEntries.Clear();
                foreach (var entry in queueEntries.OrderBy(q => q.Position))
                {
                    entry.IsVideoCached = _videoCacheService?.IsVideoCached(entry.SongId) ?? false;
                    QueueEntries.Add(entry);
                    if (!entry.IsVideoCached && !string.IsNullOrEmpty(entry.YouTubeUrl) && _videoCacheService != null)
                    {
                        // Trigger caching asynchronously in background
                        _ = Task.Run(async () =>
                        {
                            try
                            {
                                await _videoCacheService.CacheVideoAsync(entry.YouTubeUrl, entry.SongId);
                                await Application.Current.Dispatcher.InvokeAsync(() =>
                                {
                                    entry.IsVideoCached = _videoCacheService.IsVideoCached(entry.SongId);
                                    Log.Information("[DJSCREEN] Updated IsVideoCached for SongId={SongId}: {IsCached}", entry.SongId, entry.IsVideoCached);
                                });
                            }
                            catch (Exception ex)
                            {
                                Log.Error("[DJSCREEN] Failed to cache video for SongId={SongId}: {Message}", entry.SongId, ex.Message);
                            }
                        });
                    }
                }
                Log.Information("[DJSCREEN] Loaded {Count} queue entries for event {EventId}", QueueEntries.Count, _currentEventId);
            });
        }
        catch (Exception ex)
        {
            Log.Error("[DJSCREEN] Failed to load queue data for event: {EventId}: {Message}", _currentEventId, ex.Message);
            await Application.Current.Dispatcher.InvokeAsync(() =>
            {
                MessageBox.Show($"Failed to load queue data: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            });
        }
    }

    private async Task LoadSingerData()
    {
        try
        {
            Log.Information("[DJSCREEN] Loading singer data for event: {EventId}", _currentEventId);
            if (string.IsNullOrEmpty(_currentEventId))
            {
                Log.Information("[DJSCREEN] No event joined, skipping singer data load");
                Singers.Clear();
                GreenSingers.Clear();
                YellowSingers.Clear();
                OrangeSingers.Clear();
                RedSingers.Clear();
                NonDummySingersCount = 0;
                OnPropertyChanged(nameof(NonDummySingersCount));
                return;
            }

            var singers = await _apiService.GetSingersAsync(_currentEventId);
            Application.Current.Dispatcher.Invoke(() =>
            {
                Singers.Clear();
                foreach (var singer in singers)
                {
                    Singers.Add(singer);
                }
                SortSingers();
                NonDummySingersCount = Singers.Count;
                Log.Information("[DJSCREEN] Loaded {Count} singers for event {EventId}, Names={Names}",
                    NonDummySingersCount, _currentEventId, string.Join(", ", Singers.Select(s => s.DisplayName)));
            });
        }
        catch (Exception ex)
        {
            Log.Error("[DJSCREEN] Failed to load singers for event: {EventId}: {Message}", _currentEventId, ex.Message);
            Application.Current.Dispatcher.Invoke(() =>
            {
                MessageBox.Show($"Failed to load singers: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            });
        }
    }
}
#pragma warning restore CS8622
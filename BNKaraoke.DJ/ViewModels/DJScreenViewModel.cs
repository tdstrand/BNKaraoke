using BNKaraoke.DJ.Models;
using BNKaraoke.DJ.Services;
using BNKaraoke.DJ.Views;
using CommunityToolkit.Mvvm.ComponentModel;
using Serilog;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Timers;
using System.Windows;

namespace BNKaraoke.DJ.ViewModels
{
    public partial class DJScreenViewModel : ObservableObject
    {
        private readonly IUserSessionService _userSessionService = UserSessionService.Instance;
        private readonly IApiService _apiService = new ApiService(UserSessionService.Instance, SettingsService.Instance);
        private readonly SettingsService _settingsService = SettingsService.Instance;
        private readonly VideoCacheService? _videoCacheService;
        private readonly SignalRService _signalRService;
        private string? _currentEventId;
        private VideoPlayerWindow? _videoPlayerWindow;
        private bool _isLoginWindowOpen;
        private Timer? _singerPollingTimer;

        [ObservableProperty]
        private bool _isAuthenticated;

        [ObservableProperty]
        private string _welcomeMessage = "Not logged in";

        [ObservableProperty]
        private string _loginLogoutButtonText = "Login";

        [ObservableProperty]
        private string _loginLogoutButtonColor = "#3B82F6";

        [ObservableProperty]
        private string _joinEventButtonText = "No Live Events";

        [ObservableProperty]
        private string _joinEventButtonColor = "Gray";

        [ObservableProperty]
        private bool _isJoinEventButtonVisible;

        [ObservableProperty]
        private EventDto? _currentEvent;

        [ObservableProperty]
        private ObservableCollection<QueueEntry> _queueEntries = [];

        [ObservableProperty]
        private QueueEntry? _selectedQueueEntry;

        [ObservableProperty]
        private bool _isPlaying;

        [ObservableProperty]
        private ObservableCollection<Singer> _singers = [];

        [ObservableProperty]
        private string _showButtonText = "Start Show";

        [ObservableProperty]
        private string _showButtonColor = "#22d3ee";

        [ObservableProperty]
        private bool _isShowActive;

        [ObservableProperty]
        private QueueEntry? _playingQueueEntry;

        [ObservableProperty]
        private int _totalSongsPlayed;

        [ObservableProperty]
        private bool _isAutoPlayEnabled = true;

        [ObservableProperty]
        private string _autoPlayButtonText = "Auto Play: On";

        [ObservableProperty]
        private string _currentVideoPosition = "--:--";

        [ObservableProperty]
        private string _timeRemaining = "0:00";

        [ObservableProperty]
        private int _timeRemainingSeconds;

        [ObservableProperty]
        private DateTime? _warningExpirationTime;

        [ObservableProperty]
        private bool _isVideoPaused;

        [ObservableProperty]
        private string _warningMessage = "";

        [ObservableProperty]
        private int _sungCount;

        [ObservableProperty]
        private TimeSpan _songDuration = TimeSpan.FromMinutes(4);

        [ObservableProperty]
        private string _stopRestartButtonColor = "#22d3ee";

        public DJScreenViewModel(VideoCacheService? videoCacheService = null)
        {
            try
            {
                Log.Information("[DJSCREEN VM] Starting ViewModel constructor");
                _videoCacheService = videoCacheService ?? new VideoCacheService(_settingsService);
                Log.Information("[DJSCREEN VM] VideoCacheService initialized, CachePath={CachePath}", _settingsService.Settings.VideoCachePath);

                _signalRService = new SignalRService(
                    _userSessionService,
                    (queueId, action, position, isOnBreak) => HandleQueueUpdated(queueId, action, position, isOnBreak),
                    (requestorUserName, isLoggedIn, isJoined, isOnBreak) => HandleSingerStatusUpdated(requestorUserName, isLoggedIn, isJoined, isOnBreak)
                );

                _userSessionService.SessionChanged += UserSessionService_SessionChanged;
                Log.Information("[DJSCREEN VM] Subscribed to SessionChanged event");

                _singerPollingTimer = new Timer(180000); // 180 seconds
                _singerPollingTimer.Elapsed += async (s, e) => await LoadSingersAsync(_currentEventId);
                _singerPollingTimer.AutoReset = true;

                UpdateAuthenticationState();
                Log.Information("[DJSCREEN VM] Initialized UI state in constructor");
                Log.Information("[DJSCREEN VM] ViewModel instance created: {InstanceId}", GetHashCode());
            }
            catch (Exception ex)
            {
                Log.Error("[DJSCREEN VM] Failed to initialize ViewModel: {Message}", ex.Message);
                MessageBox.Show($"Failed to initialize DJScreen: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
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

        public async Task UpdateAuthenticationState()
        {
            try
            {
                Log.Information("[DJSCREEN] Updating authentication state");
                bool newIsAuthenticated = _userSessionService.IsAuthenticated;
                string newWelcomeMessage = newIsAuthenticated ? $"Welcome, {_userSessionService.FirstName ?? "User"}" : "Not logged in";
                string newLoginLogoutButtonText = newIsAuthenticated ? "Logout" : "Login";
                string newLoginLogoutButtonColor = newIsAuthenticated ? "#FF0000" : "#3B82F6";
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
                    if (_singerPollingTimer != null)
                        _singerPollingTimer.Stop();
                    await _signalRService.StopAsync(0);
                    Log.Information("[DJSCREEN SIGNALR] Disconnected from KaraokeDJHub on logout");
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

        private void HandleQueueUpdated(int queueId, string action, int? position, bool? isOnBreak)
        {
            Application.Current.Dispatcher.Invoke(async () =>
            {
                try
                {
                    Log.Information("[DJSCREEN SIGNALR] Handling QueueUpdated: QueueId={QueueId}, Action={Action}, Position={Position}, IsOnBreak={IsOnBreak}", queueId, action, position, isOnBreak);
                    await LoadQueueData();
                    await LoadSingersAsync(_currentEventId);
                }
                catch (Exception ex)
                {
                    Log.Error("[DJSCREEN SIGNALR] Failed to handle QueueUpdated for QueueId={QueueId}: {Message}, StackTrace={StackTrace}", queueId, ex.Message, ex.StackTrace);
                    WarningMessage = $"Failed to update queue: {ex.Message}";
                }
            });
        }

        private void HandleSingerStatusUpdated(string requestorUserName, bool isLoggedIn, bool isJoined, bool isOnBreak)
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                try
                {
                    Log.Information("[DJSCREEN SIGNALR] Handling SingerStatusUpdated: RequestorUserName={RequestorUserName}, IsLoggedIn={IsLoggedIn}, IsJoined={IsJoined}, IsOnBreak={IsOnBreak}",
                        requestorUserName, isLoggedIn, isJoined, isOnBreak);
                    var singer = Singers.FirstOrDefault(s => s.UserId == requestorUserName);
                    if (singer != null)
                    {
                        Log.Information("[DJSCREEN SIGNALR] Before update: UserId={UserId}, IsLoggedIn={IsLoggedIn}, IsJoined={IsJoined}, IsOnBreak={IsOnBreak}",
                            singer.UserId, singer.IsLoggedIn, singer.IsJoined, singer.IsOnBreak);

                        singer.IsLoggedIn = isLoggedIn;
                        Log.Information("[DJSCREEN SIGNALR] Updated IsLoggedIn to {IsLoggedIn} for UserId={UserId}", isLoggedIn, singer.UserId);
                        singer.IsJoined = isJoined;
                        Log.Information("[DJSCREEN SIGNALR] Updated IsJoined to {IsJoined} for UserId={UserId}", isJoined, singer.UserId);
                        singer.IsOnBreak = isOnBreak;
                        Log.Information("[DJSCREEN SIGNALR] Updated IsOnBreak to {IsOnBreak} for UserId={UserId}", isOnBreak, singer.UserId);

                        SortSingers();
                        OnPropertyChanged(nameof(Singers)); // Ensure UI refresh
                        Log.Information("[DJSCREEN SIGNALR] Locally updated singer status for UserId={UserId}, IsLoggedIn={IsLoggedIn}, IsJoined={IsJoined}, IsOnBreak={IsOnBreak}",
                            requestorUserName, isLoggedIn, isJoined, isOnBreak);
                    }
                    else
                    {
                        Log.Warning("[DJSCREEN SIGNALR] Singer not found locally for UserId={UserId}", requestorUserName);
                        Task.Run(() => LoadSingersAsync(_currentEventId, requestorUserName));
                    }
                }
                catch (Exception ex)
                {
                    Log.Error("[DJSCREEN SIGNALR] Failed to handle SingerStatusUpdated for RequestorUserName={RequestorUserName}: {Message}, StackTrace={StackTrace}", requestorUserName, ex.Message, ex.StackTrace);
                    WarningMessage = $"Failed to update singers: {ex.Message}";
                }
            });
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
                    OnPropertyChanged(nameof(Singers));
                    Log.Information("[DJSCREEN] Sorted singers: Count={Count}, Names={Names}",
                        Singers.Count, string.Join(", ", Singers.Select(s => s.DisplayName)));
                });
            }
            catch (Exception ex)
            {
                Log.Error("[DJSCREEN] Failed to sort singers: {Message}", ex.Message);
                WarningMessage = $"Failed to sort singers: {ex.Message}";
            }
        }
    }

    public class SingersResponse
    {
        public List<DJSingerDto> Singers { get; set; } = [];
        public int Total { get; set; }
        public int Page { get; set; }
        public int Size { get; set; }
    }
}
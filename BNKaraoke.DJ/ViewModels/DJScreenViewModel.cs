using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using BNKaraoke.DJ.Services;
using BNKaraoke.DJ.Views;
using BNKaraoke.DJ.Models;
using Serilog;
using Microsoft.AspNetCore.SignalR.Client;

namespace BNKaraoke.DJ.ViewModels;

#pragma warning disable CS8622 // Suppress nullability warning for event handlers
public partial class DJScreenViewModel : ObservableObject
{
    private readonly IUserSessionService _userSessionService = UserSessionService.Instance;
    private readonly IApiService _apiService = new ApiService(UserSessionService.Instance, SettingsService.Instance);
    private readonly SettingsService _settingsService = SettingsService.Instance;
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
    private ObservableCollection<QueueEntry> _queueEntries = new ObservableCollection<QueueEntry>();

    [ObservableProperty]
    private QueueEntry? _selectedQueueEntry;

    [ObservableProperty]
    private bool _isPlaying;

    [ObservableProperty]
    private ObservableCollection<Singer> _singers = new ObservableCollection<Singer>();

    [ObservableProperty]
    private int _nonDummySingersCount;

    [ObservableProperty]
    private ObservableCollection<Singer> _greenSingers = new ObservableCollection<Singer>();

    [ObservableProperty]
    private ObservableCollection<Singer> _yellowSingers = new ObservableCollection<Singer>();

    [ObservableProperty]
    private ObservableCollection<Singer> _orangeSingers = new ObservableCollection<Singer>();

    [ObservableProperty]
    private ObservableCollection<Singer> _redSingers = new ObservableCollection<Singer>();

    public DJScreenViewModel()
    {
        try
        {
            Log.Information("[DJSCREEN VM] Starting ViewModel constructor");
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

    private void InitializeSignalR()
    {
        try
        {
            Log.Information("[DJSCREEN SIGNALR] Initializing SignalR connection");
            _signalRConnection = new HubConnectionBuilder()
                .WithUrl("http://localhost:7290/hubs/singers")
                .Build();

            _signalRConnection.On<string, string, bool, bool, bool>("UpdateSingerStatus", (userId, displayName, isLoggedIn, isJoined, isOnBreak) =>
            {
                App.Current.Dispatcher.Invoke(() =>
                {
                    var singer = Singers.FirstOrDefault(s => s.UserId == userId);
                    if (singer != null)
                    {
                        singer.IsLoggedIn = isLoggedIn;
                        singer.IsJoined = isJoined;
                        singer.IsOnBreak = isOnBreak;
                    }
                    else
                    {
                        singer = new Singer { UserId = userId, DisplayName = displayName, IsLoggedIn = isLoggedIn, IsJoined = isJoined, IsOnBreak = isOnBreak };
                        Singers.Add(singer);
                    }
                    SortSingers();
                    Log.Information("[DJSCREEN SIGNALR] Singer status updated: {UserId}, {DisplayName}, LoggedIn={IsLoggedIn}, Joined={IsJoined}, OnBreak={IsOnBreak}",
                        userId, displayName, isLoggedIn, isJoined, isOnBreak);
                });
            });

            _signalRConnection.Closed += async (error) =>
            {
                Log.Error("[DJSCREEN SIGNALR] Connection closed: {Error}", error?.Message);
                await Task.Delay(5000);
                await StartSignalRConnection();
            };

            StartSignalRConnection().GetAwaiter().GetResult();
        }
        catch (Exception ex)
        {
            Log.Error("[DJSCREEN SIGNALR] Failed to initialize SignalR: {Message}", ex.Message);
        }
    }

    private async Task StartSignalRConnection()
    {
        try
        {
            if (_signalRConnection != null)
            {
                await _signalRConnection.StartAsync();
                Log.Information("[DJSCREEN SIGNALR] Connected to singers hub");
            }
        }
        catch (Exception ex)
        {
            Log.Error("[DJSCREEN SIGNALR] Failed to connect to singers hub: {Message}", ex.Message);
        }
    }

    private void LoadMockSingerData()
    {
        try
        {
            Log.Information("[DJSCREEN] Loading mock singer data");
            Singers.Clear();

            // Green (LoggedIn, Joined, Not OnBreak)
            Singers.Add(new Singer { UserId = "7275651909", DisplayName = "Ted Strand", IsLoggedIn = true, IsJoined = true, IsOnBreak = false });
            Singers.Add(new Singer { UserId = "5556667777", DisplayName = "Chris Matl", IsLoggedIn = true, IsJoined = true, IsOnBreak = false });
            Singers.Add(new Singer { UserId = "6667778888", DisplayName = "Drew Wirstrom", IsLoggedIn = true, IsJoined = true, IsOnBreak = false });

            // Yellow (LoggedIn, Joined, OnBreak)
            Singers.Add(new Singer { UserId = "9876543210", DisplayName = "Jessica Gann", IsLoggedIn = true, IsJoined = true, IsOnBreak = true });
            Singers.Add(new Singer { UserId = "7778889999", DisplayName = "Ann Marie Dixon", IsLoggedIn = true, IsJoined = true, IsOnBreak = true });
            Singers.Add(new Singer { UserId = "8889990000", DisplayName = "Tina Wirstrom", IsLoggedIn = true, IsJoined = true, IsOnBreak = true });

            // Orange (LoggedIn, Not Joined, Not OnBreak)
            Singers.Add(new Singer { UserId = "1112223333", DisplayName = "John Doe", IsLoggedIn = true, IsJoined = false, IsOnBreak = false });
            Singers.Add(new Singer { UserId = "9990001111", DisplayName = "Mark Dixon", IsLoggedIn = true, IsJoined = false, IsOnBreak = false });
            Singers.Add(new Singer { UserId = "0001112222", DisplayName = "Sherrie Matl", IsLoggedIn = true, IsJoined = false, IsOnBreak = false });

            // Red (Not LoggedIn, Not Joined, Not OnBreak)
            Singers.Add(new Singer { UserId = "4445556666", DisplayName = "Jane Roe", IsLoggedIn = false, IsJoined = false, IsOnBreak = false });
            Singers.Add(new Singer { UserId = "1112224444", DisplayName = "Terry Gann", IsLoggedIn = false, IsJoined = false, IsOnBreak = false });
            Singers.Add(new Singer { UserId = "2223335555", DisplayName = "Tricia Strand", IsLoggedIn = false, IsJoined = false, IsOnBreak = false });

            SortSingers();
            NonDummySingersCount = Singers.Count;
            Log.Information("[DJSCREEN] Loaded mock singer data: {Count} singers, Names={Names}",
                NonDummySingersCount, string.Join(", ", Singers.Select(s => s.DisplayName)));
        }
        catch (Exception ex)
        {
            Log.Error("[DJSCREEN] Failed to load mock singer data: {Message}", ex.Message);
        }
    }

    private void SortSingers()
    {
        try
        {
            Log.Information("[DJSCREEN] Sorting singers");
            var sortedSingers = Singers.OrderBy(s =>
            {
                if (s.IsLoggedIn && s.IsJoined && !s.IsOnBreak) return 1; // Green
                if (s.IsLoggedIn && s.IsJoined && s.IsOnBreak) return 2; // Yellow
                if (s.IsLoggedIn && !s.IsJoined) return 3; // Orange
                return 4; // Red
            }).ThenBy(s => s.DisplayName).ToList();

            Singers.Clear();
            foreach (var singer in sortedSingers)
            {
                Singers.Add(singer);
            }

            // Update filtered collections
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
        }
        catch (Exception ex)
        {
            Log.Error("[DJSCREEN] Failed to sort singers: {Message}", ex.Message);
        }
    }

    private void LoadMockQueueData()
    {
        try
        {
            Log.Information("[DJSCREEN] Loading mock queue data");
            QueueEntries.Clear();
            CurrentEvent = new EventDto { EventId = 3, Description = "Live Event Test 1" };
            QueueEntries.Add(new QueueEntry { QueueId = "27", SongId = 12, SongTitle = "Come Sail Away", SongArtist = "Styx", RequestorDisplayName = "Ted Strand", VideoLength = "5:14", Position = 1, Status = "Live", RequestorUserName = "7275651909", SungAt = null, Genre = "Rock", Decade = "1970s", YouTubeUrl = "https://youtube.com/watch?v=a1", IsVideoCached = false });
            QueueEntries.Add(new QueueEntry { QueueId = "28", SongId = 21, SongTitle = "All Out of Love", SongArtist = "Air Supply", RequestorDisplayName = "Ted Strand", VideoLength = "4:01", Position = 2, Status = "Live", RequestorUserName = "7275651909", SungAt = null, Genre = "Soft Rock", Decade = "1980s", YouTubeUrl = "https://youtube.com/watch?v=b2", IsVideoCached = false });
            QueueEntries.Add(new QueueEntry { QueueId = "29", SongId = 32, SongTitle = "Give It Up", SongArtist = "KC and The Sunshine Band", RequestorDisplayName = "Ted Strand", VideoLength = "4:13", Position = 3, Status = "Live", RequestorUserName = "7275651909", SungAt = null, Genre = "Disco", Decade = "1980s", YouTubeUrl = "https://youtube.com/watch?v=c3", IsVideoCached = false });
            QueueEntries.Add(new QueueEntry { QueueId = "30", SongId = 28, SongTitle = "Lovin', Touchin', Squeezin'", SongArtist = "Journey", RequestorDisplayName = "Ted Strand", VideoLength = "3:54", Position = 4, Status = "Live", RequestorUserName = "7275651909", SungAt = null, Genre = "Rock", Decade = "1970s", YouTubeUrl = "https://youtube.com/watch?v=d4", IsVideoCached = false });
            QueueEntries.Add(new QueueEntry { QueueId = "31", SongId = 27, SongTitle = "Lights", SongArtist = "Journey", RequestorDisplayName = "Ted Strand", VideoLength = "3:10", Position = 5, Status = "Live", RequestorUserName = "7275651909", SungAt = null, Genre = "Rock", Decade = "1970s", YouTubeUrl = "https://youtube.com/watch?v=e5", IsVideoCached = false });
            QueueEntries.Add(new QueueEntry { QueueId = "32", SongId = 35, SongTitle = "Will You Love Me Tomorrow", SongArtist = "The Shirelles", RequestorDisplayName = "Ted Strand", VideoLength = "2:41", Position = 6, Status = "Live", RequestorUserName = "7275651909", SungAt = null, Genre = "Pop", Decade = "1960s", YouTubeUrl = "https://youtube.com/watch?v=f6", IsVideoCached = false });
            QueueEntries.Add(new QueueEntry { QueueId = "33", SongId = 15, SongTitle = "Crazy - Single Version", SongArtist = "Patsy Cline", RequestorDisplayName = "Jessica Gann", VideoLength = "2:44", Position = 7, Status = "Live", RequestorUserName = "9876543210", SungAt = null, Genre = "Country", Decade = "1960s", YouTubeUrl = "https://youtube.com/watch?v=g7", IsVideoCached = false });
            QueueEntries.Add(new QueueEntry { QueueId = "34", SongId = 6, SongTitle = "Gentle River", SongArtist = "Alison Krauss", RequestorDisplayName = "Jessica Gann", VideoLength = "4:27", Position = 8, Status = "Live", RequestorUserName = "9876543210", SungAt = null, Genre = "Bluegrass", Decade = "1990s", YouTubeUrl = "https://youtube.com/watch?v=h8", IsVideoCached = false });
            QueueEntries.Add(new QueueEntry { QueueId = "35", SongId = 48, SongTitle = "At Last", SongArtist = "Etta James", RequestorDisplayName = "Jessica Gann", VideoLength = "3:00", Position = 9, Status = "Live", RequestorUserName = "9876543210", SungAt = null, Genre = "Soul", Decade = "1960s", YouTubeUrl = "https://youtube.com/watch?v=i9", IsVideoCached = false });
            QueueEntries.Add(new QueueEntry { QueueId = "36", SongId = 18, SongTitle = "Don't Mind If I Do", SongArtist = "Riley Green", RequestorDisplayName = "Jessica Gann", VideoLength = "3:34", Position = 10, Status = "Live", RequestorUserName = "9876543210", SungAt = null, Genre = "Country", Decade = "2010s", YouTubeUrl = "https://youtube.com/watch?v=j0", IsVideoCached = false });
            Log.Information("[DJSCREEN] Loaded mock queue data: {Count} entries", QueueEntries.Count);
        }
        catch (Exception ex)
        {
            Log.Error("[DJSCREEN] Failed to load mock queue data: {Message}", ex.Message);
        }
    }

    private void UserSessionService_SessionChanged(object sender, EventArgs e)
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
                        _apiService.LeaveEventAsync(_currentEventId, _userSessionService.PhoneNumber ?? string.Empty).GetAwaiter().GetResult();
                        Log.Information("[DJSCREEN] Left event: {EventId}", _currentEventId);
                    }
                    catch (Exception ex)
                    {
                        Log.Error("[DJSCREEN] Failed to leave event {EventId}: {Message}", _currentEventId, ex.Message);
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
                    await _apiService.JoinEventAsync(events[0].EventId.ToString(), _userSessionService.PhoneNumber ?? string.Empty);
                    _currentEventId = events[0].EventId.ToString();
                    CurrentEvent = events[0];
                    JoinEventButtonText = $"Leave {events[0].EventCode}";
                    JoinEventButtonColor = "#FF0000"; // Red
                    Log.Information("[DJSCREEN] Joined event: {EventId}, {EventCode}", _currentEventId, events[0].EventCode);
                    // Load mock data after joining event
                    LoadMockQueueData();
                    LoadMockSingerData();
                    await LoadQueueData();
                    await LoadSingerData();
                }
                else if (events.Count > 1)
                {
                    Log.Information("[DJSCREEN] Multiple live events; showing dropdown (placeholder)");
                    MessageBox.Show("Multiple live events; dropdown not implemented", "Join Event", MessageBoxButton.OK, MessageBoxImage.Information);
                }
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
                await _apiService.LeaveEventAsync(_currentEventId, _userSessionService.PhoneNumber ?? string.Empty);
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
                await UpdateJoinEventButtonState();
            }
            catch (Exception ex)
            {
                Log.Error("[DJSCREEN] Failed to leave event {EventId}: {Message}", _currentEventId, ex.Message);
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
    private void ReorderQueue()
    {
        Log.Information("[DJSCREEN] ReorderQueue command invoked");
        // Placeholder: Log reordering; will integrate with ApiService.ReorderQueueAsync
    }

    [RelayCommand]
    private async Task Play()
    {
        Log.Information("[DJSCREEN] Play/Pause command invoked");
        if (SelectedQueueEntry != null && !string.IsNullOrEmpty(_currentEventId) && SelectedQueueEntry.QueueId != null)
        {
            try
            {
                if (IsPlaying)
                {
                    await _apiService.PauseAsync(_currentEventId, SelectedQueueEntry.QueueId);
                    Log.Information("[DJSCREEN] Pause request sent for event {EventId}, queue {QueueId}: {SongTitle}", _currentEventId, SelectedQueueEntry.QueueId, SelectedQueueEntry.SongTitle);
                    MessageBox.Show($"Paused {SelectedQueueEntry.SongTitle}", "Pause", MessageBoxButton.OK, MessageBoxImage.Information);
                    IsPlaying = false;
                }
                else
                {
                    await _apiService.PlayAsync(_currentEventId, SelectedQueueEntry.QueueId);
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
        if (SelectedQueueEntry != null && !string.IsNullOrEmpty(_currentEventId) && SelectedQueueEntry.QueueId != null)
        {
            try
            {
                await _apiService.StopAsync(_currentEventId, SelectedQueueEntry.QueueId);
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
        if (SelectedQueueEntry != null && !string.IsNullOrEmpty(_currentEventId) && SelectedQueueEntry.QueueId != null)
        {
            try
            {
                await _apiService.SkipAsync(_currentEventId, SelectedQueueEntry.QueueId);
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
        if (SelectedQueueEntry != null && !string.IsNullOrEmpty(_currentEventId) && SelectedQueueEntry.QueueId != null)
        {
            try
            {
                await _apiService.LaunchVideoAsync(_currentEventId, SelectedQueueEntry.QueueId);
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
    private void StartDrag(QueueEntry entry)
    {
        Log.Information("[DJSCREEN] StartDrag command invoked for queue {QueueId}", entry?.QueueId);
        // Placeholder for drag start logic
    }

    [RelayCommand]
    private void Drop(QueueEntry entry)
    {
        Log.Information("[DJSCREEN] Drop command invoked for queue {QueueId}", entry?.QueueId);
        // Placeholder for drop logic
    }

    public void UpdateAuthenticationState()
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
            }
            else if (string.IsNullOrEmpty(_currentEventId))
            {
                Task.Run(UpdateJoinEventButtonState).GetAwaiter().GetResult();
            }

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
                OnPropertyChanged(nameof(JoinEventButtonText));
                OnPropertyChanged(nameof(JoinEventButtonColor));
                return;
            }

            var events = await _apiService.GetLiveEventsAsync();
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
        }
        catch (Exception ex)
        {
            Log.Error("[DJSCREEN] Failed to update join event button state: {Message}", ex.Message);
            JoinEventButtonText = "No Live Events";
            JoinEventButtonColor = "Gray";
            OnPropertyChanged(nameof(JoinEventButtonText));
            OnPropertyChanged(nameof(JoinEventButtonColor));
        }
    }

    private async Task LoadQueueData()
    {
        try
        {
            Log.Information("[DJSCREEN] Loading queue data for event: {EventId}", _currentEventId);
            // Placeholder for ApiService.GetQueueAsync
            await Task.CompletedTask;
        }
        catch (Exception ex)
        {
            Log.Error("[DJSCREEN] Failed to load queue data: {Message}", ex.Message);
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
                return;
            }

            // Skip ApiService.GetSingersAsync to retain mock singers from LoadMockSingerData
            SortSingers();
            NonDummySingersCount = Singers.Count;
            Log.Information("[DJSCREEN] Loaded {Count} singers for event {EventId}, Names={Names}",
                NonDummySingersCount, _currentEventId, string.Join(", ", Singers.Select(s => s.DisplayName)));
        }
        catch (Exception ex)
        {
            Log.Error("[DJSCREEN] Failed to load singers for event {EventId}: {Message}", _currentEventId, ex.Message);
            MessageBox.Show($"Failed to load singers: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }
}
#pragma warning restore CS8622
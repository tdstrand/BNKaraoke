using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using System.Windows;
using BNKaraoke.DJ.Services;
using BNKaraoke.DJ.Views;
using BNKaraoke.DJ.Models;
using Serilog;

namespace BNKaraoke.DJ.ViewModels;

#pragma warning disable CS8622 // Suppress nullability warning for event handlers
public partial class DJScreenViewModel : ObservableObject
{
    private readonly IUserSessionService _userSessionService;
    private readonly IApiService _apiService;
    private readonly SettingsService _settingsService;
    private string? _currentEventId;

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

    public DJScreenViewModel()
    {
        _userSessionService = UserSessionService.Instance;
        _settingsService = SettingsService.Instance;
        _apiService = new ApiService(_userSessionService, _settingsService);
        Log.Information("[DJSCREEN VM] ViewModel instance created: {InstanceId}", GetHashCode());
        _userSessionService.SessionChanged += UserSessionService_SessionChanged;
        UpdateAuthenticationState();
        LoadMockQueueData(); // Temporary mock data for testing
        Log.Information("[DJSCREEN INIT] Initial state: IsAuthenticated={IsAuthenticated}, WelcomeMessage={WelcomeMessage}, LoginLogoutButtonText={LoginLogoutButtonText}, JoinEventButtonText={JoinEventButtonText}",
            IsAuthenticated, WelcomeMessage, LoginLogoutButtonText, JoinEventButtonText);
    }

    private void UserSessionService_SessionChanged(object sender, EventArgs e)
    {
        Log.Information("[DJSCREEN] Session changed event received");
        UpdateAuthenticationState();
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
                    // Load queue data for the event
                    await LoadQueueData();
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
        var songDetailsWindow = new SongDetailsWindow
        {
            WindowStartupLocation = WindowStartupLocation.CenterScreen,
            DataContext = new SongDetailsViewModel { SelectedQueueEntry = QueueEntries.Count > 0 ? QueueEntries[0] : null } // Placeholder: select first item
        };
        songDetailsWindow.ShowDialog();
        Log.Information("[DJSCREEN] SongDetailsWindow closed");
    }

    [RelayCommand]
    private void ReorderQueue()
    {
        Log.Information("[DJSCREEN] ReorderQueue command invoked");
        // Placeholder: Log reordering; will integrate with ApiService.ReorderQueueAsync
    }

    public void UpdateAuthenticationState()
    {
        Log.Information("[DJSCREEN] Updating authentication state");
        bool newIsAuthenticated = _userSessionService.IsAuthenticated;
        string newWelcomeMessage = newIsAuthenticated ? $"Welcome, {_userSessionService.FirstName}" : "Not logged in";
        string newLoginLogoutButtonText = newIsAuthenticated ? "Logout" : "Login";
        string newLoginLogoutButtonColor = newIsAuthenticated ? "#FF0000" : "#3B82F6"; // Red for logout, Blue for login
        bool newIsJoinEventButtonVisible = newIsAuthenticated;

        IsAuthenticated = newIsAuthenticated;
        WelcomeMessage = newWelcomeMessage;
        LoginLogoutButtonText = newLoginLogoutButtonText;
        LoginLogoutButtonColor = newLoginLogoutButtonColor;
        IsJoinEventButtonVisible = newIsJoinEventButtonVisible;

        if (IsAuthenticated)
        {
            Task.Run(UpdateJoinEventButtonState).GetAwaiter().GetResult();
        }
        else
        {
            JoinEventButtonText = "No Live Events";
            JoinEventButtonColor = "Gray";
            _currentEventId = null;
            CurrentEvent = null;
            QueueEntries.Clear();
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

        Log.Information("[DJSCREEN] State updated: IsAuthenticated={IsAuthenticated}, WelcomeMessage={WelcomeMessage}, LoginLogoutButtonText={LoginLogoutButtonText}, LoginLogoutButtonColor={LoginLogoutButtonColor}, IsJoinEventButtonVisible={IsJoinEventButtonVisible}, JoinEventButtonText={JoinEventButtonText}, JoinEventButtonColor={JoinEventButtonColor}",
            IsAuthenticated, WelcomeMessage, LoginLogoutButtonText, LoginLogoutButtonColor, IsJoinEventButtonVisible, JoinEventButtonText, JoinEventButtonColor);
    }

    private async Task UpdateJoinEventButtonState()
    {
        if (!IsAuthenticated)
        {
            JoinEventButtonText = "No Live Events";
            JoinEventButtonColor = "Gray";
            return;
        }

        try
        {
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
        }
        catch (Exception ex)
        {
            Log.Error("[DJSCREEN] Failed to fetch live events: {Message}", ex.Message);
            JoinEventButtonText = "No Live Events";
            JoinEventButtonColor = "Gray";
        }

        OnPropertyChanged(nameof(JoinEventButtonText));
        OnPropertyChanged(nameof(JoinEventButtonColor));
    }

    private void LoadMockQueueData()
    {
        // Mock data for LVTEST (ID 3)
        QueueEntries.Clear();
        CurrentEvent = new EventDto { EventId = 3, Description = "Live Event Test 1" };
        QueueEntries.Add(new QueueEntry { QueueId = "27", SongId = 12, SongTitle = "Come Sail Away", SongArtist = "Styx", RequestorDisplayName = "Ted Strand", VideoLength = "5:14", Position = 1, Status = "Live", RequestorUserName = "7275651909", SungAt = null, Genre = "Rock", Decade = "1970s", YouTubeUrl = "https://youtube.com/watch?v=a1", IsVideoCached = false });
        QueueEntries.Add(new QueueEntry { QueueId = "28", SongId = 21, SongTitle = "All Out of Love", SongArtist = "Air Supply", RequestorDisplayName = "Ted Strand", VideoLength = "4:01", Position = 2, Status = "Live", RequestorUserName = "7275651909", SungAt = null, Genre = "Soft Rock", Decade = "1980s", YouTubeUrl = "https://youtube.com/watch?v=b2", IsVideoCached = false });
        QueueEntries.Add(new QueueEntry { QueueId = "29", SongId = 32, SongTitle = "Give It Up", SongArtist = "KC and The Sunshine Band", RequestorDisplayName = "Ted Strand", VideoLength = "4:13", Position = 3, Status = "Live", RequestorUserName = "7275651909", SungAt = null, Genre = "Disco", Decade = "1980s", YouTubeUrl = "https://youtube.com/watch?v=c3", IsVideoCached = false });
        QueueEntries.Add(new QueueEntry { QueueId = "30", SongId = 28, SongTitle = "Lovin', Touchin', Squeezin'", SongArtist = "Journey", RequestorDisplayName = "Ted Strand", VideoLength = "3:54", Position = 4, Status = "Live", RequestorUserName = "7275651909", SungAt = null, Genre = "Rock", Decade = "1970s", YouTubeUrl = "https://youtube.com/watch?v=d4", IsVideoCached = false });
        QueueEntries.Add(new QueueEntry { QueueId = "31", SongId = 27, SongTitle = "Lights", SongArtist = "Journey", RequestorDisplayName = "Ted Strand", VideoLength = "3:10", Position = 5, Status = "Live", RequestorUserName = "7275651909", SungAt = null, Genre = "Rock", Decade = "1970s", YouTubeUrl = "https://youtube.com/watch?v=e5", IsVideoCached = false });
        QueueEntries.Add(new QueueEntry { QueueId = "32", SongId = 35, SongTitle = "Will You Love Me Tomorrow", SongArtist = "The Shirelles", RequestorDisplayName = "Ted Strand", VideoLength = "2:41", Position = 6, Status = "Live", RequestorUserName = "7275651909", SungAt = null, Genre = "Pop", Decade = "1960s", YouTubeUrl = "https://youtube.com/watch?v=f6", IsVideoCached = false });
        QueueEntries.Add(new QueueEntry { QueueId = "33", SongId = 15, SongTitle = "Crazy - Single Version", SongArtist = "Patsy Cline", RequestorDisplayName = "Alice Smith", VideoLength = "2:44", Position = 7, Status = "Live", RequestorUserName = "1234567891", SungAt = null, Genre = "Country", Decade = "1960s", YouTubeUrl = "https://youtube.com/watch?v=g7", IsVideoCached = false });
        QueueEntries.Add(new QueueEntry { QueueId = "34", SongId = 6, SongTitle = "Gentle River", SongArtist = "Alison Krauss", RequestorDisplayName = "Alice Smith", VideoLength = "4:27", Position = 8, Status = "Live", RequestorUserName = "1234567891", SungAt = null, Genre = "Bluegrass", Decade = "1990s", YouTubeUrl = "https://youtube.com/watch?v=h8", IsVideoCached = false });
        QueueEntries.Add(new QueueEntry { QueueId = "35", SongId = 48, SongTitle = "At Last", SongArtist = "Etta James", RequestorDisplayName = "Alice Smith", VideoLength = "3:00", Position = 9, Status = "Live", RequestorUserName = "1234567891", SungAt = null, Genre = "Soul", Decade = "1960s", YouTubeUrl = "https://youtube.com/watch?v=i9", IsVideoCached = false });
        QueueEntries.Add(new QueueEntry { QueueId = "36", SongId = 18, SongTitle = "Don't Mind If I Do", SongArtist = "Riley Green", RequestorDisplayName = "Alice Smith", VideoLength = "3:34", Position = 10, Status = "Live", RequestorUserName = "1234567891", SungAt = null, Genre = "Country", Decade = "2010s", YouTubeUrl = "https://youtube.com/watch?v=j0", IsVideoCached = false });
        Log.Information("[DJSCREEN] Loaded mock queue data: {Count} entries", QueueEntries.Count);
    }

#pragma warning disable CS1998
    private async Task LoadQueueData()
    {
        Log.Information("[DJSCREEN] Loading queue data for event: {EventId}", _currentEventId);
        // Placeholder for ApiService.GetQueueAsync
    }
#pragma warning restore CS1998
}
#pragma warning restore CS8622
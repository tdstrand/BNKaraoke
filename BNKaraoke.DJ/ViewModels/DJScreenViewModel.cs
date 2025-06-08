using BNKaraoke.DJ.Models;
using BNKaraoke.DJ.Services;
using BNKaraoke.DJ.Views;
using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.AspNetCore.SignalR.Client;
using Serilog;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;

namespace BNKaraoke.DJ.ViewModels
{
    public partial class DJScreenViewModel : ObservableObject
    {
        private readonly IUserSessionService _userSessionService = UserSessionService.Instance;
        private readonly IApiService _apiService = new ApiService(UserSessionService.Instance, SettingsService.Instance);
        private readonly SettingsService _settingsService = SettingsService.Instance;
        private readonly VideoCacheService? _videoCacheService;
        private string? _currentEventId;
        private HubConnection? _signalRConnection;
        private VideoPlayerWindow? _videoPlayerWindow;
        private bool _isLoginWindowOpen;

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
        private ObservableCollection<QueueEntry> _queueEntries = [];

        [ObservableProperty]
        private QueueEntry? _selectedQueueEntry;

        [ObservableProperty]
        private bool _isPlaying;

        [ObservableProperty]
        private ObservableCollection<Singer> _singers = [];

        [ObservableProperty]
        private int _nonDummySingersCount;

        [ObservableProperty]
        private ObservableCollection<Singer> _greenSingers = [];

        [ObservableProperty]
        private ObservableCollection<Singer> _yellowSingers = [];

        [ObservableProperty]
        private ObservableCollection<Singer> _orangeSingers = [];

        [ObservableProperty]
        private ObservableCollection<Singer> _redSingers = [];

        [ObservableProperty]
        private string _showButtonText = "Start Show";

        [ObservableProperty]
        private string _showButtonColor = "#22d3ee"; // Cyan

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
        private double _songPosition;

        [ObservableProperty]
        private TimeSpan _songDuration = TimeSpan.FromMinutes(4);

        [ObservableProperty]
        private string _stopRestartButtonColor = "#22d3ee"; // Default cyan

        public DJScreenViewModel(VideoCacheService? videoCacheService = null)
        {
            try
            {
                Log.Information("[DJSCREEN VM] Starting ViewModel constructor");
                _videoCacheService = videoCacheService ?? new VideoCacheService(_settingsService);
                Log.Information("[DJSCREEN VM] VideoCacheService initialized, CachePath={CachePath}", _settingsService.Settings.VideoCachePath);
                _userSessionService.SessionChanged += UserSessionService_SessionChanged;
                Log.Information("[DJSCREEN VM] Subscribed to SessionChanged event");
                UpdateAuthenticationState().GetAwaiter().GetResult();
                Log.Information("[DJSCREEN VM] Called UpdateAuthenticationState in constructor");
                Log.Information("[DJSCREEN VM] ViewModel instance created: {InstanceId}", GetHashCode());
            }
            catch (Exception ex)
            {
                Log.Error("[DJSCREEN VM] Failed to initialize ViewModel: {Message}", ex.Message);
                MessageBox.Show($"Failed to initialize DJScreen: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}
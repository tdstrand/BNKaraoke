using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Windows;
using BNKaraoke.DJ.Services;
using BNKaraoke.DJ.Views;
using Serilog;

namespace BNKaraoke.DJ.ViewModels;

#pragma warning disable CS8622 // Suppress nullability warning for event handlers
public partial class DJScreenViewModel : ObservableObject
{
    private readonly IUserSessionService _userSessionService;
    private readonly IApiService _apiService;
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

    public DJScreenViewModel()
    {
        _userSessionService = UserSessionService.Instance;
        _apiService = new ApiService(_userSessionService);
        Log.Information("[DJSCREEN VM] ViewModel instance created: {InstanceId}", GetHashCode());
        _userSessionService.SessionChanged += UserSessionService_SessionChanged;
        UpdateAuthenticationState();
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
                    JoinEventButtonText = $"Leave {events[0].EventCode}";
                    JoinEventButtonColor = "#FF0000"; // Red
                    Log.Information("[DJSCREEN] Joined event: {EventId}, {EventCode}", _currentEventId, events[0].EventCode);
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
        }

        OnPropertyChanged(nameof(IsAuthenticated));
        OnPropertyChanged(nameof(WelcomeMessage));
        OnPropertyChanged(nameof(LoginLogoutButtonText));
        OnPropertyChanged(nameof(LoginLogoutButtonColor));
        OnPropertyChanged(nameof(IsJoinEventButtonVisible));
        OnPropertyChanged(nameof(JoinEventButtonText));
        OnPropertyChanged(nameof(JoinEventButtonColor));

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
}
#pragma warning restore CS8622
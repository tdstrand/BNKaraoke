using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Windows;
using BNKaraoke.DJ.Services;
using BNKaraoke.DJ.Views;
using Serilog;

namespace BNKaraoke.DJ.ViewModels;

public partial class DJScreenViewModel : ObservableObject
{
    private readonly IUserSessionService _userSessionService = UserSessionService.Instance;

    [ObservableProperty]
    private bool _isAuthenticated;

    [ObservableProperty]
    private string _welcomeMessage = "Not logged in";

    [ObservableProperty]
    private string _loginLogoutButtonText = "Login";

    [ObservableProperty]
    private string _loginLogoutButtonColor = "#3B82F6"; // Blue

    [ObservableProperty]
    private string _joinEventButtonColor = "Gray"; // Placeholder

    public DJScreenViewModel()
    {
        Log.Information("[DJSCREEN VM] ViewModel instance created: {InstanceId}", GetHashCode());
        _userSessionService.SessionChanged += UserSessionService_SessionChanged;
        UpdateAuthenticationState();
        Log.Information("[DJSCREEN INIT] Initial state: IsAuthenticated={IsAuthenticated}, WelcomeMessage={WelcomeMessage}, LoginLogoutButtonText={LoginLogoutButtonText}, LoginLogoutButtonColor={LoginLogoutButtonColor}, JoinEventButtonColor={JoinEventButtonColor}",
            IsAuthenticated, WelcomeMessage, LoginLogoutButtonText, LoginLogoutButtonColor, JoinEventButtonColor);
    }

    private void UserSessionService_SessionChanged(object sender, System.EventArgs e)
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
                _userSessionService.ClearSession();
                UpdateAuthenticationState();
                Log.Information("[DJSCREEN] Logout complete: IsAuthenticated={IsAuthenticated}, WelcomeMessage={WelcomeMessage}, LoginLogoutButtonText={LoginLogoutButtonText}, LoginLogoutButtonColor={LoginLogoutButtonColor}",
                    IsAuthenticated, WelcomeMessage, LoginLogoutButtonText, LoginLogoutButtonColor);
            }
        }
        else
        {
            Log.Information("[DJSCREEN] Showing LoginWindow");
            var loginWindow = new LoginWindow { WindowStartupLocation = WindowStartupLocation.CenterScreen };
            loginWindow.ShowDialog();
            UpdateAuthenticationState();
            Log.Information("[DJSCREEN] LoginWindow closed: IsAuthenticated={IsAuthenticated}, WelcomeMessage={WelcomeMessage}, LoginLogoutButtonText={LoginLogoutButtonText}, LoginLogoutButtonColor={LoginLogoutButtonColor}",
                IsAuthenticated, WelcomeMessage, LoginLogoutButtonText, LoginLogoutButtonColor);
        }
    }

    [RelayCommand]
    private void OpenSettings()
    {
        Log.Information("[DJSCREEN] Settings button clicked - placeholder");
        MessageBox.Show("SettingsWindow not implemented yet", "Settings", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    [RelayCommand]
    private void JoinLiveEvent()
    {
        Log.Information("[DJSCREEN] Join Live Event button clicked - placeholder");
        MessageBox.Show("Join Live Event not implemented yet", "Join Event", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    public void UpdateAuthenticationState()
    {
        Log.Information("[DJSCREEN] Updating authentication state");
        bool newIsAuthenticated = _userSessionService.IsAuthenticated;
        string newWelcomeMessage = newIsAuthenticated ? $"Welcome, {_userSessionService.FirstName}" : "Not logged in";
        string newLoginLogoutButtonText = newIsAuthenticated ? "Logout" : "Login";
        string newLoginLogoutButtonColor = newIsAuthenticated ? "#FF0000" : "#3B82F6"; // Red for logout, Blue for login
        string newJoinEventButtonColor = newIsAuthenticated ? "#3B82F6" : "Gray"; // Placeholder

        IsAuthenticated = newIsAuthenticated;
        WelcomeMessage = newWelcomeMessage;
        LoginLogoutButtonText = newLoginLogoutButtonText;
        LoginLogoutButtonColor = newLoginLogoutButtonColor;
        JoinEventButtonColor = newJoinEventButtonColor;

        OnPropertyChanged(nameof(IsAuthenticated));
        OnPropertyChanged(nameof(WelcomeMessage));
        OnPropertyChanged(nameof(LoginLogoutButtonText));
        OnPropertyChanged(nameof(LoginLogoutButtonColor));
        OnPropertyChanged(nameof(JoinEventButtonColor));

        Log.Information("[DJSCREEN] State updated: IsAuthenticated={IsAuthenticated}, WelcomeMessage={WelcomeMessage}, LoginLogoutButtonText={LoginLogoutButtonText}, LoginLogoutButtonColor={LoginLogoutButtonColor}, JoinEventButtonColor={JoinEventButtonColor}",
            IsAuthenticated, WelcomeMessage, LoginLogoutButtonText, LoginLogoutButtonColor, JoinEventButtonColor);
    }
}
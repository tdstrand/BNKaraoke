using BNKaraoke.DJ.Services;
using BNKaraoke.DJ.Views;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Serilog;
using System;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;

namespace BNKaraoke.DJ.ViewModels;

public partial class LoginWindowViewModel : ObservableObject
{
    private readonly IAuthService _authService;
    private readonly IUserSessionService _userSessionService;

    [ObservableProperty]
    private string _username = string.Empty;

    [ObservableProperty]
    private string _password = string.Empty;

    [ObservableProperty]
    private string? _errorMessage;

    public LoginWindowViewModel()
    {
        _authService = new AuthService();
        _userSessionService = UserSessionService.Instance;
        Log.Information("[LOGIN INIT] ViewModel initialized: {InstanceId}", GetHashCode());
    }

    [RelayCommand]
    private async Task Login()
    {
        try
        {
            ErrorMessage = null;
            var phone = Regex.Replace(Username, "[^0-9]", "");
            Log.Information("[LOGIN DEBUG] Sending login for: {Phone}", phone);
            var loginResult = await _authService.LoginAsync(phone, Password);
            Log.Information("[LOGIN DEBUG] Login result: Token={Token}, FirstName={FirstName}, UserId={UserId}, PhoneNumber={PhoneNumber}, Roles={Roles}, IsAuthenticated={IsAuthenticated}",
                loginResult.Token?.Substring(0, 10) ?? "null", loginResult.FirstName, loginResult.UserId, loginResult.PhoneNumber,
                loginResult.Roles != null ? string.Join(",", loginResult.Roles) : "null", _userSessionService.IsAuthenticated);
            _userSessionService.SetSession(loginResult);
            Log.Information("[LOGIN DEBUG] Session set: IsAuthenticated={IsAuthenticated}", _userSessionService.IsAuthenticated);
            Application.Current.Windows.OfType<Window>().FirstOrDefault(w => w is LoginWindow)?.Close();
        }
        catch (Exception ex)
        {
            Log.Warning("[LOGIN DEBUG] Login failed: {Message}", ex.Message);
            ErrorMessage = $"Login failed: {ex.Message}";
        }
    }
}
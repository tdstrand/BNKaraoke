using BNKaraoke.DJ.Models;
using BNKaraoke.DJ.Services;
using BNKaraoke.DJ.Views;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Serilog;
using System;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;

namespace BNKaraoke.DJ.ViewModels
{
    public partial class LoginWindowViewModel : ObservableObject
    {
        private readonly IApiService _apiService = new ApiService(UserSessionService.Instance, SettingsService.Instance);
        private readonly IUserSessionService _userSessionService = UserSessionService.Instance;

        [ObservableProperty]
        private string _phoneNumber = string.Empty;

        [ObservableProperty]
        private string _password = string.Empty;

        [ObservableProperty]
        private bool _isBusy;

        public LoginWindowViewModel()
        {
            Log.Information("[LOGIN INIT] ViewModel initialized: {InstanceId}", GetHashCode());
        }

        [RelayCommand]
        private async Task LoginAsync()
        {
            try
            {
                Log.Information("[LOGIN DEBUG] Sending login for: {PhoneNumber}", PhoneNumber);
                IsBusy = true;

                var loginResult = await _apiService.LoginAsync(PhoneNumber, Password);
                Log.Information("[LOGIN DEBUG] Login result: Token={Token}, FirstName={FirstName}, PhoneNumber={PhoneNumber}",
                    loginResult.Token, loginResult.FirstName, loginResult.PhoneNumber);

                if (!string.IsNullOrEmpty(loginResult.Token))
                {
                    _userSessionService.SetSession(loginResult);
                    Log.Information("[LOGIN DEBUG] Session set: IsAuthenticated=True");

                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        if (Application.Current.Windows.OfType<LoginWindow>().FirstOrDefault() is LoginWindow loginWindow)
                        {
                            loginWindow.DialogResult = true;
                            loginWindow.Close();
                            Log.Information("[LOGIN DEBUG] LoginWindow closed after successful login");
                        }
                    });
                }
                else
                {
                    Log.Error("[LOGIN DEBUG] Login failed: Invalid credentials");
                    MessageBox.Show("Invalid phone number or password.", "Login Failed", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            catch (Exception ex)
            {
                Log.Error("[LOGIN DEBUG] Login failed: {Message}", ex.Message);
                MessageBox.Show($"Login failed: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                IsBusy = false;
            }
        }
    }
}
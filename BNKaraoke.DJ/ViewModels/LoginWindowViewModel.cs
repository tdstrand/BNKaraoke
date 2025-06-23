// File: ViewModels/LoginWindowViewModel.cs
using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows.Input;
using System.Diagnostics;
using BNKaraoke.DJ.Services;
using BNKaraoke.DJ.Models;

namespace BNKaraoke.DJ.ViewModels
{
    public class LoginWindowViewModel : INotifyPropertyChanged
    {
        private string _displayPhone = "";
        private string _password = "";
        private string _errorMessage = "";
        private bool _isBusy = false;

        private readonly IApiServices _api;
        private readonly IUserSessionService _session;
        private readonly Action _onLoginSuccess;

        public event PropertyChangedEventHandler? PropertyChanged;

        public LoginWindowViewModel(IApiServices api, IUserSessionService session, Action onLoginSuccess)
        {
            _api = api;
            _session = session;
            _onLoginSuccess = onLoginSuccess;

            LoginCommand = new RelayCommand(
                async (_) => await ExecuteLoginAsync(),
                (_) => CanLogin()
            );
        }

        public string DisplayPhone
        {
            get => _displayPhone;
            set
            {
                if (_displayPhone != value)
                {
                    _displayPhone = FormatPhone(value);
                    OnPropertyChanged();
                    RaiseCanExecuteChanged();
                }
            }
        }

        public string Password
        {
            get => _password;
            set
            {
                _password = value;
                OnPropertyChanged();
                RaiseCanExecuteChanged();
            }
        }

        public string ErrorMessage
        {
            get => _errorMessage;
            set { _errorMessage = value; OnPropertyChanged(); OnPropertyChanged(nameof(HasError)); }
        }

        public bool HasError => !string.IsNullOrWhiteSpace(ErrorMessage);

        public bool IsBusy
        {
            get => _isBusy;
            set { _isBusy = value; OnPropertyChanged(); RaiseCanExecuteChanged(); }
        }

        public ICommand LoginCommand { get; }

        private async Task ExecuteLoginAsync()
        {
            ErrorMessage = "";

            var rawPhone = StripPhone(DisplayPhone);
            if (rawPhone.Length != 10)
            {
                ErrorMessage = "Enter a valid 10-digit phone number.";
                return;
            }

            IsBusy = true;

            try
            {
                Debug.WriteLine($"🔐 Attempting login for phone: {rawPhone}");

                var token = await _api.LoginAsync(rawPhone, Password);
                var userInfo = await _api.GetUserInfoAsync(token);

                if (!userInfo.Roles.Contains("Karaoke DJ"))
                {
                    ErrorMessage = "You must have the 'Karaoke DJ' role to use this console.";
                    return;
                }

                _session.SetSession(new UserSession
                {
                    Token = token,
                    UserId = userInfo.UserName,
                    FirstName = userInfo.FirstName,
                    LastName = userInfo.LastName,
                    Roles = userInfo.Roles
                });

                Debug.WriteLine("✅ Login successful. Closing window...");
                _onLoginSuccess.Invoke();
            }
            catch (Exception ex)
            {
                Debug.WriteLine("❌ Login error: " + ex);
                ErrorMessage = "Login failed: " + ex.Message;
            }
            finally
            {
                IsBusy = false;
            }
        }

        private string StripPhone(string phone)
        {
            return Regex.Replace(phone, @"\D", "");
        }

        private string FormatPhone(string raw)
        {
            string digits = StripPhone(raw);
            if (digits.Length > 10)
                digits = digits.Substring(0, 10);

            return digits.Length switch
            {
                <= 3 => digits,
                <= 6 => $"({digits.Substring(0, 3)}) {digits.Substring(3)}",
                _ => $"({digits.Substring(0, 3)}) {digits.Substring(3, 3)}-{digits.Substring(6)}"
            };
        }

        private bool CanLogin()
        {
            var phone = StripPhone(DisplayPhone);
            return !IsBusy && phone.Length == 10 && !string.IsNullOrWhiteSpace(Password);
        }

        private void RaiseCanExecuteChanged()
        {
            if (LoginCommand is RelayCommand cmd)
                cmd.RaiseCanExecuteChanged();
        }

        private void OnPropertyChanged([CallerMemberName] string? name = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }
    }
}

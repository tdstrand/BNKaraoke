using System.Linq;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using BNKaraoke.DJ.Services;
using BNKaraoke.DJ.Models;

namespace BNKaraoke.DJ.Views;

public partial class LoginWindow : Window
{
    private readonly AuthService _authService;
    private readonly IUserSessionService _session;

    public LoginWindow()
    {
        InitializeComponent();
        _authService = new AuthService(new SettingsService().LoadSettings().ApiBaseUrl);
        _session = new UserSessionService();
        Loaded += (s, e) => PhoneNumberBox.Focus();
        PhoneNumberBox.PreviewTextInput += FormatPhoneInput;
        PhoneNumberBox.TextChanged += PhoneNumberBox_TextChanged;
        PasswordBox.KeyDown += PasswordBox_KeyDown;
    }

    private void PhoneNumberBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        var digits = new string(Regex.Replace(PhoneNumberBox.Text, "[^0-9]", "").Take(10).ToArray());
        PhoneNumberBox.TextChanged -= PhoneNumberBox_TextChanged;

        if (digits.Length <= 3)
            PhoneNumberBox.Text = $"({digits}";
        else if (digits.Length <= 6)
            PhoneNumberBox.Text = $"({digits.Substring(0, 3)}) {digits.Substring(3)}";
        else
            PhoneNumberBox.Text = $"({digits.Substring(0, 3)}) {digits.Substring(3, 3)}-{digits.Substring(6)}";

        PhoneNumberBox.CaretIndex = PhoneNumberBox.Text.Length;
        PhoneNumberBox.TextChanged += PhoneNumberBox_TextChanged;
    }

    private void FormatPhoneInput(object sender, TextCompositionEventArgs e)
    {
        e.Handled = !Regex.IsMatch(e.Text, "^[0-9]$");
    }

    private void PasswordBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
            TryLogin();
    }

    private void LoginButton_Click(object sender, RoutedEventArgs e)
    {
        TryLogin();
    }

    private async void TryLogin()
    {
        string rawPhone = Regex.Replace(PhoneNumberBox.Text, "[^0-9]", "");
        string password = PasswordBox.Password;

        if (rawPhone.Length != 10 || string.IsNullOrEmpty(password))
        {
            MessageBox.Show("Enter a valid phone number and password.");
            return;
        }

        var result = await _authService.LoginAsync(rawPhone, password);
        if (result != null)
        {
            _session.SetSession(result.Token, result.FirstName, result.LastName, result.PhoneNumber, result.Roles);
            DialogResult = true;
            Close();
        }
        else
        {
            MessageBox.Show("Login failed. Please check your credentials.", "Login Error", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }
}

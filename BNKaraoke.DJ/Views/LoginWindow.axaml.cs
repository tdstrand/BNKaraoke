// File: C:\Users\tstra\source\repos\BNKaraoke\BNKaraoke.DJ\Views\LoginWindow.axaml.cs
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Markup.Xaml;
using BNKaraoke.DJ.ViewModels;
using System.Diagnostics;
using Avalonia.Interactivity;
using System.Linq;

namespace BNKaraoke.DJ.Views
{
    public partial class LoginWindow : Window
    {
        public LoginWindow()
        {
            Debug.WriteLine("LoginWindow constructor called");
            InitializeComponent();
            DataContext = new LoginWindowViewModel(this);

            // Attach LostFocus event handler
            var phoneNumberBox = this.FindControl<TextBox>("PhoneNumberBox");
            if (phoneNumberBox != null)
            {
                Debug.WriteLine("Attaching LostFocus event handler to PhoneNumberBox");
                phoneNumberBox.LostFocus += PhoneNumber_LostFocus;
            }
            else
            {
                Debug.WriteLine("PhoneNumberBox not found during initialization");
            }

            Debug.WriteLine("LoginWindow initialized");
        }

        private void InitializeComponent()
        {
            Debug.WriteLine("InitializeComponent called");
            AvaloniaXamlLoader.Load(this);
            Debug.WriteLine("AvaloniaXamlLoader.Load completed");
        }

        private void PhoneNumber_KeyDown(object? sender, KeyEventArgs e)
        {
            Debug.WriteLine("PhoneNumber_KeyDown event triggered");
            if (e.Key == Key.Enter && sender is TextBox textBox)
            {
                // Move focus to the Password field
                var passwordBox = this.FindControl<TextBox>("PasswordBox");
                if (passwordBox != null)
                {
                    Debug.WriteLine("Moving focus to PasswordBox");
                    passwordBox.Focus();
                    e.Handled = true;
                }
                else
                {
                    Debug.WriteLine("PasswordBox not found during PhoneNumber_KeyDown");
                }
            }
        }

        private void PhoneNumber_GotFocus(object? sender, GotFocusEventArgs e)
        {
            Debug.WriteLine("PhoneNumber_GotFocus event triggered");
            if (sender is TextBox textBox)
            {
#pragma warning disable CS8602 // Dereference of a possibly null reference.
                if (textBox.Text == string.Empty)
#pragma warning restore CS8602
                {
                    Debug.WriteLine("PhoneNumber updated: ");
                    textBox.Text = string.Empty;
                }
            }
        }

        private void PhoneNumber_LostFocus(object? sender, RoutedEventArgs e)
        {
            Debug.WriteLine("PhoneNumber_LostFocus event triggered");
            if (sender is TextBox textBox)
            {
#pragma warning disable CS8602 // Dereference of a possibly null reference.
                if (!string.IsNullOrEmpty(textBox.Text))
#pragma warning restore CS8602
                {
                    Debug.WriteLine("FormatPhoneNumber called");
                    string digits = new string(textBox.Text.Where(char.IsDigit).ToArray());
                    if (digits.Length == 10)
                    {
                        textBox.Text = $"({digits.Substring(0, 3)}) {digits.Substring(3, 3)}-{digits.Substring(6, 4)}";
                        Debug.WriteLine($"Phone number formatted: {textBox.Text}");
                    }
                }
            }
        }

        private void Password_KeyDown(object? sender, KeyEventArgs e)
        {
            Debug.WriteLine("Password_KeyDown event triggered");
            if (e.Key == Key.Enter && DataContext is LoginWindowViewModel viewModel)
            {
                Debug.WriteLine("Login command executed from Password Enter key...");
                viewModel.LoginCommand.Execute(null);
                e.Handled = true;
            }
        }
    }
}
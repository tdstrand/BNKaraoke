// File: C:\Users\tstra\source\repos\BNKaraoke\BNKaraoke.DJ\ViewModels\LoginWindowViewModel.cs
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Avalonia.Controls;

namespace BNKaraoke.DJ.ViewModels
{
    public partial class LoginWindowViewModel : ObservableObject
    {
        private readonly Window _window;

        [ObservableProperty]
        private string _phoneNumber = string.Empty;

        [ObservableProperty]
        private string _password = string.Empty;

        [ObservableProperty]
        private bool _isLoginSuccessful;

        public LoginWindowViewModel(Window window)
        {
            _window = window;
        }

        [RelayCommand]
        private void Login()
        {
            // Basic validation or logic can be added here
            if (!string.IsNullOrEmpty(PhoneNumber) && !string.IsNullOrEmpty(Password))
            {
                IsLoginSuccessful = true;
                // Close the window
                _window.Close();
            }
        }

        [RelayCommand]
        private void Cancel()
        {
            IsLoginSuccessful = false;
            // Close the window
            _window.Close();
        }
    }
}
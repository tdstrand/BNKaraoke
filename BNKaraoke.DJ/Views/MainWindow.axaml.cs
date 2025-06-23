// File: MainWindow.axaml.cs
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using BNKaraoke.DJ.Services;
using System.Threading.Tasks;

namespace BNKaraoke.DJ.Views
{
    public partial class MainWindow : Window
    {
        private readonly IApiServices _apiServices;
        private readonly IUserSessionService _session;

        public MainWindow()
        {
            InitializeComponent();

            _apiServices = DependencyLocator.ApiService;
            _session = DependencyLocator.UserSessionService;

            UpdateLoginButtonState();
        }

        private async void OnLoginClick(object? sender, RoutedEventArgs e)
        {
            if (_session.IsAuthenticated)
            {
                var result = await ShowLogoutConfirmation();

                if (result == true)
                {
                    _session.ClearSession();
                    UpdateLoginButtonState();
                }
            }
            else
            {
                var loginWindow = new LoginWindow();
                await loginWindow.ShowDialog(this);

                if (_session.IsAuthenticated)
                {
                    UpdateLoginButtonState();
                }
            }
        }

        private async Task<bool> ShowLogoutConfirmation()
        {
            var dialog = new Window
            {
                Title = "Logout Confirmation",
                Width = 300,
                Height = 140,
                WindowStartupLocation = WindowStartupLocation.CenterOwner
            };

            var tcs = new TaskCompletionSource<bool>();

            var yesButton = new Button { Content = "Yes", Width = 80, Background = Brushes.Red, Foreground = Brushes.White };
            var noButton = new Button { Content = "No", Width = 80, Background = Brushes.Gray, Foreground = Brushes.White };

            yesButton.Click += (_, _) => { tcs.SetResult(true); dialog.Close(); };
            noButton.Click += (_, _) => { tcs.SetResult(false); dialog.Close(); };

            dialog.Content = new StackPanel
            {
                Margin = new Thickness(20),
                Children =
                {
                    new TextBlock { Text = "Are you sure you want to logout?", FontSize = 16, Margin = new Thickness(0, 0, 0, 20) },
                    new StackPanel
                    {
                        Orientation = Orientation.Horizontal,
                        HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center,
                        Spacing = 10,
                        Children = { yesButton, noButton }
                    }
                }
            };

            await dialog.ShowDialog(this);
            return await tcs.Task;
        }

        private void UpdateLoginButtonState()
        {
            if (_session.IsAuthenticated)
            {
                LoginButton.Content = "Logout";
                LoginButton.Background = Brushes.Red;
                LoginButton.Foreground = Brushes.White;

                WelcomeText.Text = $"Welcome, DJ {_session.FirstName}";
            }
            else
            {
                LoginButton.Content = "Login";
                LoginButton.Background = Brushes.Blue;
                LoginButton.Foreground = Brushes.White;

                WelcomeText.Text = "Welcome, Guest";
                JoinLiveEventButton.IsVisible = false;
            }
        }

        private void OnSettingsClick(object? sender, PointerPressedEventArgs e)
        {
            // TODO: Open settings window
        }
    }
}

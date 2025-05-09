// File: C:\Users\tstra\source\repos\BNKaraoke\BNKaraoke.DJ\Views\MainWindow.axaml.cs
using Avalonia.Controls;
using BNKaraoke.DJ.ViewModels;
using System.Threading.Tasks;
using Avalonia.Controls.Shapes;
using Avalonia.Media;
using Avalonia.Layout;
using Avalonia.Threading;
using System.Diagnostics;
using Avalonia;
using System;

namespace BNKaraoke.DJ.Views
{
    public partial class MainWindow : Window
    {
        private readonly MainWindowViewModel _viewModel;

        public MainWindow()
        {
            Debug.WriteLine("MainWindow constructor called");
            _viewModel = new MainWindowViewModel(this);
            DataContext = _viewModel;
            Debug.WriteLine($"MainWindow DataContext set to new MainWindowViewModel instance, InstanceId: {_viewModel.InstanceId}");
            InitializeComponent();
        }

        protected override void OnInitialized()
        {
            base.OnInitialized();
            Debug.WriteLine($"MainWindow OnInitialized called, DataContext InstanceId: {(DataContext as MainWindowViewModel)?.InstanceId}");
        }

        public async Task ShowMessageDialog(string title, string message)
        {
            Debug.WriteLine($"Showing message dialog: {title} - {message}");
            var dialog = new Window
            {
                Title = title,
                Width = 300,
                Height = 150,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                CanResize = false,
                Background = new SolidColorBrush(Color.Parse("#1e3a8a")),
            };

            var stackPanel = new StackPanel
            {
                Margin = new Avalonia.Thickness(10),
                Spacing = 10,
            };

            stackPanel.Children.Add(new TextBlock
            {
                Text = message,
                Foreground = new SolidColorBrush(Color.Parse("#FFFFFF")),
                TextWrapping = Avalonia.Media.TextWrapping.Wrap,
            });

            var okButton = new Button
            {
                Content = "OK",
                HorizontalAlignment = HorizontalAlignment.Center,
                Background = new SolidColorBrush(Color.Parse("#22d3ee")),
                Foreground = new SolidColorBrush(Color.Parse("Black")),
                CornerRadius = new CornerRadius(8),
            };
            okButton.Click += (s, e) =>
            {
                Debug.WriteLine("OK button clicked in message dialog");
                dialog.Close();
            };
            stackPanel.Children.Add(okButton);

            dialog.Content = stackPanel;
            try
            {
                Debug.WriteLine("Displaying message dialog");
                await dialog.ShowDialog(this);
                Debug.WriteLine("Message dialog closed");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error displaying message dialog: {ex.Message}");
            }
        }

        public async Task<bool> RequestConfirmationDialog(string title, string message)
        {
            Debug.WriteLine($"Showing confirmation dialog: {title} - {message}");
            var tcs = new TaskCompletionSource<bool>();

            var dialog = new Window
            {
                Title = title,
                Width = 300,
                Height = 150,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                CanResize = false,
                Background = new SolidColorBrush(Color.Parse("#1e3a8a")),
            };

            var stackPanel = new StackPanel
            {
                Margin = new Avalonia.Thickness(10),
                Spacing = 10,
            };

            stackPanel.Children.Add(new TextBlock
            {
                Text = message,
                Foreground = new SolidColorBrush(Color.Parse("#FFFFFF")),
                TextWrapping = Avalonia.Media.TextWrapping.Wrap,
            });

            var buttonPanel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Center,
                Spacing = 10,
            };

            var yesButton = new Button
            {
                Content = "Yes",
                Background = new SolidColorBrush(Color.Parse("#22d3ee")),
                Foreground = new SolidColorBrush(Color.Parse("Black")),
                CornerRadius = new CornerRadius(8),
            };
            yesButton.Click += (s, e) =>
            {
                Debug.WriteLine("Yes button clicked in confirmation dialog");
                tcs.SetResult(true);
                dialog.Close();
            };

            var noButton = new Button
            {
                Content = "No",
                Background = new SolidColorBrush(Color.Parse("#22d3ee")),
                Foreground = new SolidColorBrush(Color.Parse("Black")),
                CornerRadius = new CornerRadius(8),
            };
            noButton.Click += (s, e) =>
            {
                Debug.WriteLine("No button clicked in confirmation dialog");
                tcs.SetResult(false);
                dialog.Close();
            };

            buttonPanel.Children.Add(yesButton);
            buttonPanel.Children.Add(noButton);
            stackPanel.Children.Add(buttonPanel);

            dialog.Content = stackPanel;
            try
            {
                Debug.WriteLine("Displaying confirmation dialog");
                await dialog.ShowDialog(this);
                Debug.WriteLine("Confirmation dialog closed");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error displaying confirmation dialog: {ex.Message}");
                tcs.SetResult(false);
            }
            return tcs.Task.Result;
        }

        public async Task<(string? phoneNumber, string? password)> ShowLoginDialogAsync()
        {
            Debug.WriteLine("ShowLoginDialogAsync called");
            try
            {
                // Ensure dialog creation and display happen on the UI thread
#pragma warning disable CS8619 // Nullability of reference types in value doesn't match target type
                var result = await Dispatcher.UIThread.InvokeAsync(async () =>
                {
                    Debug.WriteLine("Creating LoginWindow instance on UI thread");
                    var loginDialog = new LoginWindow();
                    Debug.WriteLine("LoginWindow instance created");
                    await loginDialog.ShowDialog(this);
                    Debug.WriteLine("Login dialog closed");
                    if (loginDialog.DataContext is LoginWindowViewModel viewModel && viewModel.IsLoginSuccessful)
                    {
                        var phoneNumber = viewModel.PhoneNumber.Replace("(", "").Replace(")", "").Replace(" ", "").Replace("-", "");
                        Debug.WriteLine($"Login dialog successful: phoneNumber={phoneNumber}");
                        return (phoneNumber, viewModel.Password);
                    }
                    Debug.WriteLine("Login dialog cancelled or failed");
                    return (null, null);
                });
#pragma warning restore CS8619
                return result;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error in ShowLoginDialogAsync: {ex.Message}");
                return (null, null);
            }
        }

        protected override async void OnClosed(EventArgs e)
        {
            Debug.WriteLine("MainWindow closing, logging out user");
            if (DataContext is MainWindowViewModel viewModel)
            {
                // Perform logout if the user is logged in
                if (viewModel.IsLoggedIn)
                {
                    Debug.WriteLine("User is logged in, performing logout on app exit");
                    // Use the command to logout, bypassing confirmation dialog
                    await Task.Run(() => viewModel.ToggleLoginCommand.Execute(true));
                }
                else
                {
                    Debug.WriteLine("User is not logged in, no logout needed");
                }

                // Ensure SignalR connection and HTTP client are disposed
                await viewModel.DisposeAsync();
            }
            base.OnClosed(e);
        }
    }
}
using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using BNKaraoke.DJ.ViewModels;
using System.Diagnostics;
using System.Threading.Tasks;
using Avalonia.Threading;
using System.Reactive.Linq; // For Subscribe overload
using System; // Added for Exception and EventArgs

namespace BNKaraoke.DJ.Views;

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

        // Attach Click event handler to the Join/Check-In button
        var joinButton = this.FindControl<Button>("JoinButton");
        if (joinButton != null)
        {
            Debug.WriteLine("JoinButton found, attaching event handlers");
            joinButton.Click += async (sender, e) =>
            {
                Debug.WriteLine($"JoinButton Click event triggered, CanJoinLiveEvent={_viewModel.CanJoinLiveEvent}");
                if (_viewModel.ToggleCheckInCommand.CanExecute(null))
                {
                    await (Task)_viewModel.ToggleCheckInCommand.ExecuteAsync(null);
                }
                else
                {
                    Debug.WriteLine("JoinButton Click: Command cannot execute.");
                }
            };

            // Add a PropertyChangedCallback to log IsEnabled changes
            joinButton.GetObservable(Button.IsEnabledProperty).Subscribe(
                isEnabled => Debug.WriteLine($"JoinButton IsEnabled changed to: {isEnabled}")
            );
        }
        else
        {
            Debug.WriteLine("JoinButton not found in MainWindow.");
        }
    }

    protected override void OnInitialized()
    {
        base.OnInitialized();
        Debug.WriteLine($"MainWindow OnInitialized called, DataContext InstanceId: {_viewModel.InstanceId}");
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

    public async Task ShowMessageDialog(string title, string message)
    {
        await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(async () =>
        {
            var dialog = new MessageDialog(title, message);
            await dialog.ShowDialog(this);
        });
    }

    public async Task<bool> RequestConfirmationDialog(string title, string message)
    {
        return await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(async () =>
        {
            var dialog = new ConfirmationDialog(title, message);
            return await dialog.ShowDialog<bool>(this);
        });
    }

    protected override async void OnClosed(EventArgs e)
    {
        Debug.WriteLine("MainWindow closing, logging out user");
        // Perform logout if the user is logged in
        if (_viewModel.IsLoggedIn)
        {
            Debug.WriteLine("User is logged in, performing logout on app exit");
            // Use the command to logout, bypassing confirmation dialog
            await Task.Run(() => _viewModel.ToggleLoginCommand.Execute(true));
        }
        else
        {
            Debug.WriteLine("User is not logged in, no logout needed");
        }

        // Ensure SignalR connection and HTTP client are disposed
        await _viewModel.DisposeAsync();
        base.OnClosed(e);
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
        Debug.WriteLine("AvaloniaXamlLoader.Load completed");
    }
}
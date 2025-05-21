using BNKaraoke.DJ.Services;
using BNKaraoke.DJ.ViewModels;
using BNKaraoke.DJ.Views;
using Serilog;
using Serilog.Debugging;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;

namespace BNKaraoke.DJ;

public partial class App : Application
{
    [DllImport("kernel32.dll")]
    private static extern bool AllocConsole();

    protected override void OnStartup(StartupEventArgs e)
    {
        // Enable Serilog self-logging for errors
        SelfLog.Enable(msg => System.Diagnostics.Debug.WriteLine($"[Serilog Error] {msg}"));

        // Allocate console for dotnet run
        AllocConsole();

        // Use writable user directory for log file
        var logPath = @"C:\Users\tstra\Documents\BNKaraoke\log.txt";
        if (!string.IsNullOrEmpty(Path.GetDirectoryName(logPath)))
        {
            Directory.CreateDirectory(Path.GetDirectoryName(logPath)); // Ensure directory exists
        }
        Log.Logger = new LoggerConfiguration()
            .WriteTo.Console()
            .WriteTo.Debug()
            .WriteTo.File(logPath, rollingInterval: RollingInterval.Day)
            .CreateLogger();

        Log.Information("[APP START] Serilog initialized, log file: {LogPath}", logPath);

        base.OnStartup(e);

        var userSessionService = UserSessionService.Instance;
        var mainWindow = new DJScreen { WindowState = WindowState.Maximized };

        Log.Information("[APP START] Checking session: IsAuthenticated={IsAuthenticated}", userSessionService.IsAuthenticated);

        mainWindow.Show();

        if (!userSessionService.IsAuthenticated)
        {
            var loginWindow = new LoginWindow { Owner = mainWindow, WindowStartupLocation = WindowStartupLocation.CenterOwner };
            Log.Information("[APP START] Showing LoginWindow as dialog");
            loginWindow.ShowDialog();
            if (!userSessionService.IsAuthenticated)
            {
                Log.Information("[APP START] Login canceled, shutting down");
                Shutdown();
                return;
            }
            Log.Information("[APP START] Login succeeded");
            // Trigger DJScreenViewModel refresh
            if (mainWindow.DataContext is DJScreenViewModel viewModel)
            {
                viewModel.UpdateAuthenticationState();
                Log.Information("[APP START] Triggered DJScreenViewModel refresh post-login");
            }
        }

        Log.Information("[APP START] Activating DJScreen");
        mainWindow.Activate();
    }
}
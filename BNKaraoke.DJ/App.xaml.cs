using System;
using System.Runtime.InteropServices;
using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Serilog;
using Serilog.Extensions.Logging;
using BNKaraoke.DJ.Views;
using BNKaraoke.DJ.Services;
using BNKaraoke.DJ.Models;

namespace BNKaraoke.DJ;

public partial class App : Application
{
    [DllImport("kernel32.dll")]
    internal static extern bool AllocConsole();

    private ILogger<App> _logger;
    private AppSettings _settings;
    private SettingsService _settingsService;

    protected override async void OnStartup(StartupEventArgs e)
    {
        AllocConsole();
        Console.WriteLine("[DEBUG] Console attached.");
        Application.Current.ShutdownMode = ShutdownMode.OnExplicitShutdown;

        var logFilePath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "bnkdj-log.txt");

        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Debug()
            .WriteTo.Console()
            .WriteTo.File(logFilePath, rollingInterval: RollingInterval.Day)
            .CreateLogger();

        var services = new ServiceCollection();
        services.AddLogging(config =>
        {
            config.ClearProviders();
            config.AddProvider(new SerilogLoggerProvider(Log.Logger, dispose: true));
        });

        var provider = services.BuildServiceProvider();
        _logger = provider.GetRequiredService<ILogger<App>>();
        _settingsService = new SettingsService();

        _logger.LogInformation("Application starting up...");

        base.OnStartup(e);

        _logger.LogInformation("[TRACE] Loading settings...");
        _settings = _settingsService.LoadSettings();
        _logger.LogInformation("[TRACE] Settings loaded: {BaseUrl}", _settings.ApiBaseUrl);

        _logger.LogInformation("[TRACE] Testing API base URL...");
        if (await _settingsService.TestApiAsync(_settings.ApiBaseUrl))
        {
            LaunchLoginAndDJScreen();
        }
        else
        {
            _logger.LogWarning("API unreachable at {Url}. Launching mini settings.", _settings.ApiBaseUrl);
            var settingsWindow = new MiniSettingsWindow(_settings);
            var result = settingsWindow.ShowDialog();

            if (result == true)
            {
                _settings = _settingsService.LoadSettings();
                if (await _settingsService.TestApiAsync(_settings.ApiBaseUrl))
                {
                    LaunchLoginAndDJScreen();
                    Application.Current.Shutdown();
                    return;
                }
            }

            MessageBox.Show("Unable to connect to API after settings update. Application will exit.",
                "Connection Error", MessageBoxButton.OK, MessageBoxImage.Error);
            Shutdown();
        }

        Application.Current.Shutdown();
    }

    private void LaunchLoginAndDJScreen()
    {
        var loginWindow = new LoginWindow();
        if (loginWindow.ShowDialog() == true)
        {
            _logger.LogInformation("[TRACE] LOGIN SUCCESSFUL. LAUNCHING DJSCREEN...");
            Console.WriteLine("LOGIN SUCCESSFUL. LAUNCHING DJSCREEN...");
            var djScreen = new DJScreen();
            _logger.LogInformation("[TRACE] DJScreen object created...");
            Console.WriteLine("DJScreen object created...");
            Application.Current.MainWindow = djScreen;
            djScreen.ShowDialog();
            _logger.LogInformation("[TRACE] DJScreen.ShowDialog completed.");
        }
    }
}

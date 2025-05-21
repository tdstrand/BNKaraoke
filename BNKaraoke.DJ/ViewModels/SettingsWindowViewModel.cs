using BNKaraoke.DJ.Services;
using BNKaraoke.DJ.Views;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Serilog;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using System.Windows; // For WPF MessageBox and Application
using System.Windows.Forms; // For FolderBrowserDialog

namespace BNKaraoke.DJ.ViewModels;

public partial class SettingsWindowViewModel : ObservableObject
{
    private readonly SettingsService _settingsService;
    private readonly IUserSessionService _userSessionService;
    private readonly List<string> _availableApiUrls = new List<string>
    {
        "http://localhost:7290",
        "https://bnkaraoke.com:7290"
    };

    [ObservableProperty] private string _apiUrl;
    [ObservableProperty] private string _defaultDJName;
    [ObservableProperty] private string _preferredAudioDevice;
    [ObservableProperty] private string _karaokeVideoDevice;
    [ObservableProperty] private bool _enableVideoCaching;
    [ObservableProperty] private string _videoCachePath;
    [ObservableProperty] private double _cacheSizeGB;
    [ObservableProperty] private bool _enableSignalRSync;
    [ObservableProperty] private string _signalRHubUrl;
    [ObservableProperty] private int _reconnectIntervalMs;
    [ObservableProperty] private string _theme;
    [ObservableProperty] private bool _showDebugConsole;
    [ObservableProperty] private bool _maximizedOnStart;
    [ObservableProperty] private string _logFilePath;
    [ObservableProperty] private bool _enableVerboseLogging;

    public IReadOnlyList<string> AvailableApiUrls => _availableApiUrls;

    public SettingsWindowViewModel()
    {
        _settingsService = SettingsService.Instance;
        _userSessionService = UserSessionService.Instance;

        // Initialize from current settings
        _apiUrl = _settingsService.Settings.ApiUrl;
        _defaultDJName = _settingsService.Settings.DefaultDJName;
        _preferredAudioDevice = _settingsService.Settings.PreferredAudioDevice;
        _karaokeVideoDevice = _settingsService.Settings.KaraokeVideoDevice;
        _enableVideoCaching = _settingsService.Settings.EnableVideoCaching;
        _videoCachePath = _settingsService.Settings.VideoCachePath;
        _cacheSizeGB = _settingsService.Settings.CacheSizeGB;
        _enableSignalRSync = _settingsService.Settings.EnableSignalRSync;
        _signalRHubUrl = _settingsService.Settings.SignalRHubUrl;
        _reconnectIntervalMs = _settingsService.Settings.ReconnectIntervalMs;
        _theme = _settingsService.Settings.Theme;
        _showDebugConsole = _settingsService.Settings.ShowDebugConsole;
        _maximizedOnStart = _settingsService.Settings.MaximizedOnStart;
        _logFilePath = _settingsService.Settings.LogFilePath;
        _enableVerboseLogging = _settingsService.Settings.EnableVerboseLogging;

        Log.Information("[SETTINGS VM] Initialized: ApiUrl={ApiUrl}, DefaultDJName={DefaultDJName}, PreferredAudioDevice={PreferredAudioDevice}, EnableSignalRSync={EnableSignalRSync}, CacheSizeGB={CacheSizeGB}",
            _apiUrl, _defaultDJName, _preferredAudioDevice, _enableSignalRSync, _cacheSizeGB);
    }

    [RelayCommand]
    private async Task SaveSettings()
    {
        try
        {
            // Validate ApiUrl
            if (!_availableApiUrls.Contains(ApiUrl))
            {
                System.Windows.MessageBox.Show($"API URL {ApiUrl} is not in the allowed list. Please select a valid URL.", "Invalid API URL", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }
            using (var client = new HttpClient())
            {
                client.Timeout = TimeSpan.FromSeconds(5);
                var response = await client.GetAsync($"{ApiUrl}/api/Auth/test");
                if (!response.IsSuccessStatusCode)
                {
                    System.Windows.MessageBox.Show($"API URL {ApiUrl} is not reachable. Please try again.", "API Unreachable", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }
            }

            // Validate ReconnectIntervalMs
            if (ReconnectIntervalMs < 1000)
            {
                System.Windows.MessageBox.Show("Reconnect Interval must be at least 1000 ms.", "Invalid Input", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            // Validate VideoCachePath
            if (!string.IsNullOrEmpty(VideoCachePath) && !Directory.Exists(VideoCachePath))
            {
                try
                {
                    Directory.CreateDirectory(VideoCachePath);
                }
                catch
                {
                    System.Windows.MessageBox.Show($"Video Cache Path {VideoCachePath} is invalid or inaccessible.", "Invalid Path", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }
            }

            // Validate LogFilePath
            if (!string.IsNullOrEmpty(LogFilePath))
            {
                try
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(LogFilePath)!);
                }
                catch
                {
                    System.Windows.MessageBox.Show($"Log File Path {LogFilePath} is invalid or inaccessible.", "Invalid Path", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }
            }

            // Validate CacheSizeGB
            if (CacheSizeGB < 0 || CacheSizeGB > 100)
            {
                System.Windows.MessageBox.Show("Cache Size must be between 0 and 100 GB.", "Invalid Input", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            bool apiUrlChanged = _settingsService.Settings.ApiUrl != ApiUrl;
            if (apiUrlChanged)
            {
                var result = System.Windows.MessageBox.Show("Changing the API URL will log you out. Proceed?", "Confirm API URL Change", MessageBoxButton.YesNo, MessageBoxImage.Warning);
                if (result != MessageBoxResult.Yes)
                {
                    return;
                }
            }

            // Update settings
            _settingsService.Settings.ApiUrl = ApiUrl;
            _settingsService.Settings.DefaultDJName = DefaultDJName;
            _settingsService.Settings.PreferredAudioDevice = PreferredAudioDevice;
            _settingsService.Settings.KaraokeVideoDevice = KaraokeVideoDevice;
            _settingsService.Settings.EnableVideoCaching = EnableVideoCaching;
            _settingsService.Settings.VideoCachePath = VideoCachePath;
            _settingsService.Settings.CacheSizeGB = CacheSizeGB;
            _settingsService.Settings.EnableSignalRSync = EnableSignalRSync;
            _settingsService.Settings.SignalRHubUrl = SignalRHubUrl;
            _settingsService.Settings.ReconnectIntervalMs = ReconnectIntervalMs;
            _settingsService.Settings.Theme = Theme;
            _settingsService.Settings.ShowDebugConsole = ShowDebugConsole;
            _settingsService.Settings.MaximizedOnStart = MaximizedOnStart;
            _settingsService.Settings.LogFilePath = LogFilePath;
            _settingsService.Settings.EnableVerboseLogging = EnableVerboseLogging;

            // Save to settings.json
            await _settingsService.SaveSettingsAsync();

            if (apiUrlChanged)
            {
                _userSessionService.ClearSession();
                Log.Information("[SETTINGS VM] API URL changed to {ApiUrl}, session cleared", ApiUrl);

                var loginWindow = new LoginWindow { WindowStartupLocation = WindowStartupLocation.CenterScreen };
                loginWindow.Show();
                System.Windows.Application.Current.Windows.OfType<SettingsWindow>().First()?.Close();
                System.Windows.Application.Current.Windows.OfType<DJScreen>().First()?.Close();
            }
            else
            {
                Log.Information("[SETTINGS VM] Settings saved successfully");
                System.Windows.Application.Current.Windows.OfType<SettingsWindow>().First()?.Close();
            }
        }
        catch (Exception ex)
        {
            Log.Error("[SETTINGS VM] Failed to save settings: {Message}", ex.Message);
            System.Windows.MessageBox.Show($"Failed to save settings: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    [RelayCommand]
    private void BrowseVideoCachePath()
    {
        using (var dialog = new FolderBrowserDialog())
        {
            dialog.Description = "Select Video Cache Path";
            dialog.SelectedPath = string.IsNullOrEmpty(VideoCachePath) ? Environment.GetFolderPath(Environment.SpecialFolder.MyVideos) : VideoCachePath;
            if (dialog.ShowDialog() == DialogResult.OK)
            {
                VideoCachePath = dialog.SelectedPath;
                Log.Information("[SETTINGS VM] Selected Video Cache Path: {VideoCachePath}", VideoCachePath);
            }
        }
    }

    [RelayCommand]
    private void BrowseLogFilePath()
    {
        using (var dialog = new FolderBrowserDialog())
        {
            dialog.Description = "Select Log File Path";
            var initialPath = string.IsNullOrEmpty(LogFilePath) ? Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments) : Path.GetDirectoryName(LogFilePath);
            dialog.SelectedPath = initialPath ?? Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            if (dialog.ShowDialog() == DialogResult.OK)
            {
                LogFilePath = Path.Combine(dialog.SelectedPath, "DJ.log");
                Log.Information("[SETTINGS VM] Selected Log File Path: {LogFilePath}", LogFilePath);
            }
        }
    }

    [RelayCommand]
    private void Cancel()
    {
        Log.Information("[SETTINGS VM] Settings dialog canceled");
        System.Windows.Application.Current.Windows.OfType<SettingsWindow>().First()?.Close();
    }
}
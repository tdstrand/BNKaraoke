using BNKaraoke.DJ.Models;
using Serilog;
using System;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;

namespace BNKaraoke.DJ.Services;

public class SettingsService
{
    private static readonly Lazy<SettingsService> _instance = new Lazy<SettingsService>(() => new SettingsService());
    public static SettingsService Instance => _instance.Value;

    private readonly string _settingsPath;

    public DjSettings Settings { get; private set; }

    private SettingsService()
    {
        _settingsPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "BNKaraoke", "settings.json");
        Settings = new DjSettings();
    }

    public async Task LoadSettingsAsync()
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_settingsPath)!);
            if (File.Exists(_settingsPath))
            {
                var json = await File.ReadAllTextAsync(_settingsPath);
                Settings = JsonSerializer.Deserialize<DjSettings>(json) ?? new DjSettings();
                Log.Information("[SETTINGS SERVICE] Loaded settings from {SettingsPath}", _settingsPath);
            }
            else
            {
                Settings = new DjSettings();
                await SaveSettingsAsync();
                Log.Information("[SETTINGS SERVICE] Created new settings file with defaults at {SettingsPath}", _settingsPath);
            }
        }
        catch (Exception ex)
        {
            Settings = new DjSettings();
            Log.Error("[SETTINGS SERVICE] Failed to load settings from {SettingsPath}: {Message}", _settingsPath, ex.Message);
            System.Diagnostics.Debug.WriteLine($"[SETTINGS SERVICE] Failed to load settings from {_settingsPath}: {ex.Message}");
            MessageBox.Show($"Settings file {_settingsPath} is corrupt or inaccessible. Default settings applied. Please review Settings.",
                "Settings Load Error", MessageBoxButton.OK, MessageBoxImage.Error);
            try
            {
                await SaveSettingsAsync();
            }
            catch (Exception saveEx)
            {
                Log.Error("[SETTINGS SERVICE] Failed to create default settings file: {Message}", saveEx.Message);
                System.Diagnostics.Debug.WriteLine($"[SETTINGS SERVICE] Failed to create default settings file: {saveEx.Message}");
            }
        }
    }

    public async Task SaveSettingsAsync()
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_settingsPath)!);
            var json = JsonSerializer.Serialize(Settings, new JsonSerializerOptions { WriteIndented = true });
            await File.WriteAllTextAsync(_settingsPath, json);
            Log.Information("[SETTINGS SERVICE] Saved settings to {SettingsPath}", _settingsPath);
        }
        catch (Exception ex)
        {
            Log.Error("[SETTINGS SERVICE] Failed to save settings to {SettingsPath}: {Message}", _settingsPath, ex.Message);
            System.Diagnostics.Debug.WriteLine($"[SETTINGS SERVICE] Failed to save settings to {_settingsPath}: {ex.Message}");
            MessageBox.Show($"Failed to save settings to {_settingsPath}. Changes may not persist.",
                "Settings Save Error", MessageBoxButton.OK, MessageBoxImage.Error);
            throw;
        }
    }
}
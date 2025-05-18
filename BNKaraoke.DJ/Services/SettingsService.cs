using System;
using System.IO;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;

namespace BNKaraoke.DJ.Services;

public class SettingsService
{
    private const string SettingsFile = "appsettings.json";

    public AppSettings LoadSettings()
    {
        if (!File.Exists(SettingsFile))
        {
            return new AppSettings { ApiBaseUrl = "http://localhost:7290" };
        }

        var json = File.ReadAllText(SettingsFile);
        return JsonSerializer.Deserialize<AppSettings>(json) ?? new AppSettings();
    }

    public void SaveSettings(AppSettings settings)
    {
        var json = JsonSerializer.Serialize(settings, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(SettingsFile, json);
    }

    public async Task<bool> TestApiAsync(string apiBaseUrl)
    {
        try
        {
            using var client = new HttpClient { BaseAddress = new Uri(apiBaseUrl) };
            var response = await client.GetAsync("api/diagnostic/test");
            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }
}
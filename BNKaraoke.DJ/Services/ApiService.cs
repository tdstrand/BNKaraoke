using BNKaraoke.DJ.Models;
using Serilog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;

namespace BNKaraoke.DJ.Services;

public class ApiService : IApiService
{
    private readonly HttpClient _httpClient;
    private readonly IUserSessionService _userSessionService;
    private readonly SettingsService _settingsService;

    public ApiService(IUserSessionService userSessionService, SettingsService settingsService)
    {
        _httpClient = new HttpClient { BaseAddress = new Uri(settingsService.Settings.ApiUrl) };
        _userSessionService = userSessionService;
        _settingsService = settingsService;
    }

    public async Task<LoginResult> LoginAsync(string username, string password)
    {
        var response = await _httpClient.PostAsJsonAsync("/api/auth/login", new { UserName = username, Password = password });
        response.EnsureSuccessStatusCode();
        var loginResult = await response.Content.ReadFromJsonAsync<LoginResult>();
        Log.Information("[API] Login response: Token={Token}, FirstName={FirstName}, UserId={UserId}, PhoneNumber={PhoneNumber}, Roles={Roles}",
            loginResult?.Token?.Substring(0, 10) ?? "null", loginResult?.FirstName, loginResult?.UserId, loginResult?.PhoneNumber,
            loginResult?.Roles != null ? string.Join(",", loginResult.Roles) : "null");
        return loginResult!;
    }

    public async Task<List<EventDto>> GetLiveEventsAsync()
    {
        try
        {
            if (!string.IsNullOrEmpty(_userSessionService.Token))
            {
                _httpClient.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _userSessionService.Token);
            }
            var response = await _httpClient.GetFromJsonAsync<List<EventDto>>("/api/events");
            var liveEvents = response?.Where(e => e.Status == "Live" && e.Visibility == "Visible" && !e.IsCanceled).ToList() ?? new List<EventDto>();
            Log.Information("[API] Live events fetched: Count={Count}", liveEvents.Count);
            return liveEvents;
        }
        catch (Exception ex)
        {
            Log.Error("[API] Failed to fetch live events: {Message}", ex.Message);
            return new List<EventDto>();
        }
    }

    public async Task JoinEventAsync(string eventId, string phoneNumber)
    {
        try
        {
            if (!string.IsNullOrEmpty(_userSessionService.Token))
            {
                _httpClient.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _userSessionService.Token);
            }
            var response = await _httpClient.PostAsJsonAsync($"/api/events/{eventId}/attendance/check-in", new AttendanceActionDto { RequestorId = phoneNumber });
            response.EnsureSuccessStatusCode();
            Log.Information("[API] Joined event: EventId={EventId}, PhoneNumber={PhoneNumber}", eventId, phoneNumber);
        }
        catch (Exception ex)
        {
            Log.Error("[API] Failed to join event {EventId} for PhoneNumber {PhoneNumber}: {Message}", eventId, phoneNumber, ex.Message);
            throw;
        }
    }

    public async Task LeaveEventAsync(string eventId, string phoneNumber)
    {
        try
        {
            if (!string.IsNullOrEmpty(_userSessionService.Token))
            {
                _httpClient.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _userSessionService.Token);
            }
            var response = await _httpClient.PostAsJsonAsync($"/api/events/{eventId}/attendance/check-out", new AttendanceActionDto { RequestorId = phoneNumber });
            response.EnsureSuccessStatusCode();
            Log.Information("[API] Left event: EventId={EventId}, PhoneNumber={PhoneNumber}", eventId, phoneNumber);
        }
        catch (Exception ex)
        {
            Log.Error("[API] Failed to leave event {EventId} for PhoneNumber {PhoneNumber}: {Message}", eventId, phoneNumber, ex.Message);
            throw;
        }
    }

    public async Task<string> GetDiagnosticAsync()
    {
        try
        {
            var response = await _httpClient.GetStringAsync("/api/diagnostic");
            Log.Information("[API] Diagnostic response: {Response}", response);
            return response;
        }
        catch (Exception ex)
        {
            Log.Error("[API] Failed to get diagnostic: {Message}", ex.Message);
            return $"Diagnostic failed: {ex.Message}";
        }
    }
}
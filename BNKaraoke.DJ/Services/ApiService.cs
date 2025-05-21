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

    public ApiService(IUserSessionService userSessionService)
    {
        _httpClient = new HttpClient { BaseAddress = new Uri("http://localhost:7290") };
        _userSessionService = userSessionService;
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

    public async Task JoinEventAsync(string eventId, string userId)
    {
        try
        {
            if (!string.IsNullOrEmpty(_userSessionService.Token))
            {
                _httpClient.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _userSessionService.Token);
            }
            var response = await _httpClient.PostAsJsonAsync($"/api/events/{eventId}/attendance/check-in", new AttendanceActionDto { RequestorId = userId });
            response.EnsureSuccessStatusCode();
            Log.Information("[API] Joined event: EventId={EventId}, UserId={UserId}", eventId, userId);
        }
        catch (Exception ex)
        {
            Log.Error("[API] Failed to join event {EventId} for UserId {UserId}: {Message}", eventId, userId, ex.Message);
            throw;
        }
    }

    public async Task LeaveEventAsync(string eventId, string userId)
    {
        try
        {
            if (!string.IsNullOrEmpty(_userSessionService.Token))
            {
                _httpClient.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _userSessionService.Token);
            }
            var response = await _httpClient.PostAsJsonAsync($"/api/events/{eventId}/attendance/check-out", new AttendanceActionDto { RequestorId = userId });
            response.EnsureSuccessStatusCode();
            Log.Information("[API] Left event: EventId={EventId}, UserId={UserId}", eventId, userId);
        }
        catch (Exception ex)
        {
            Log.Error("[API] Failed to leave event {EventId} for UserId {UserId}: {Message}", eventId, userId, ex.Message);
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
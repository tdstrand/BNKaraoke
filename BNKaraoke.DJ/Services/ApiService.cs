using BNKaraoke.DJ.Models;
using Serilog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Tasks;

namespace BNKaraoke.DJ.Services;

public class ApiService : IApiService
{
    private readonly HttpClient _httpClient;
    private readonly IUserSessionService _userSessionService;
    private readonly SettingsService _settingsService;

    public ApiService(IUserSessionService userSessionService, SettingsService settingsService)
    {
        _userSessionService = userSessionService;
        _settingsService = settingsService;
        _httpClient = new HttpClient { BaseAddress = new Uri("http://localhost:7290") };
        ConfigureAuthorizationHeader();
    }

    private void ConfigureAuthorizationHeader()
    {
        _httpClient.DefaultRequestHeaders.Authorization = null;
        if (!string.IsNullOrEmpty(_userSessionService.Token))
        {
            _httpClient.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _userSessionService.Token);
            Log.Information("[APISERVICE] Authorization header set with Bearer token");
        }
        else
        {
            Log.Warning("[APISERVICE] No token available for Authorization header");
        }
    }

    public async Task<List<EventDto>> GetLiveEventsAsync()
    {
        try
        {
            ConfigureAuthorizationHeader();
            Log.Information("[APISERVICE] Attempting to fetch live events");
            var response = await _httpClient.GetAsync("/api/events?status=active");
            response.EnsureSuccessStatusCode();
            var events = await response.Content.ReadFromJsonAsync<List<EventDto>>();
            Log.Information("[APISERVICE] Fetched {Count} live events", events?.Count ?? 0);
            return events ?? new List<EventDto>();
        }
        catch (HttpRequestException ex) when (ex.StatusCode == System.Net.HttpStatusCode.Unauthorized)
        {
            Log.Error("[APISERVICE] Unauthorized access when fetching live events: {Message}", ex.Message);
            throw new UnauthorizedAccessException("Authentication failed. Please re-login.", ex);
        }
        catch (HttpRequestException ex)
        {
            Log.Error("[APISERVICE] Failed to fetch live events: Status={StatusCode}, Message={Message}", ex.StatusCode, ex.Message);
            throw;
        }
        catch (Exception ex)
        {
            Log.Error("[APISERVICE] Failed to fetch live events: {Message}", ex.Message);
            throw;
        }
    }

    public async Task JoinEventAsync(string eventId, string requestorId)
    {
        try
        {
            ConfigureAuthorizationHeader();
            var request = new { RequestorId = requestorId };
            Log.Information("[APISERVICE] Sending join event request for EventId={EventId}, RequestorId={RequestorId}, Payload={Payload}", eventId, requestorId, JsonSerializer.Serialize(request));
            var response = await _httpClient.PostAsJsonAsync($"/api/events/{eventId}/attendance/check-in", request);
            if (response.StatusCode == System.Net.HttpStatusCode.Conflict || response.StatusCode == System.Net.HttpStatusCode.BadRequest)
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                if (errorContent.Contains("already checked in", StringComparison.OrdinalIgnoreCase))
                {
                    Log.Information("[APISERVICE] User {RequestorId} is already checked in for EventId={EventId}", requestorId, eventId);
                    return;
                }
                Log.Error("[APISERVICE] Failed to join event {EventId}: Status={StatusCode}, Error={Error}", eventId, response.StatusCode, errorContent);
                throw new HttpRequestException($"Failed to join event: {response.StatusCode} - {errorContent}");
            }
            response.EnsureSuccessStatusCode();
            Log.Information("[APISERVICE] Successfully joined event: EventId={EventId}", eventId);
        }
        catch (HttpRequestException ex)
        {
            Log.Error("[APISERVICE] Failed to join event {EventId}: Status={StatusCode}, Message={Message}", eventId, ex.StatusCode, ex.Message);
            throw;
        }
    }

    public async Task LeaveEventAsync(string eventId, string requestorId)
    {
        try
        {
            ConfigureAuthorizationHeader();
            var request = new { RequestorId = requestorId };
            Log.Information("[APISERVICE] Sending leave event request for EventId={EventId}, RequestorId={RequestorId}, Payload={Payload}", eventId, requestorId, JsonSerializer.Serialize(request));
            var response = await _httpClient.PostAsJsonAsync($"/api/events/{eventId}/attendance/check-out", request);
            response.EnsureSuccessStatusCode();
            var content = await response.Content.ReadAsStringAsync();
            Log.Information("[APISERVICE] Successfully left event: EventId={EventId}, Response={Response}", eventId, content);
        }
        catch (HttpRequestException ex)
        {
            Log.Error("[APISERVICE] Failed to leave event {EventId}: Status={StatusCode}, Message={Message}", eventId, ex.StatusCode, ex.Message);
            throw new HttpRequestException($"Failed to leave event: {ex.StatusCode} - {ex.Message}", ex);
        }
    }

    public async Task<string> GetDiagnosticAsync()
    {
        try
        {
            ConfigureAuthorizationHeader();
            var response = await _httpClient.GetAsync("/api/diagnostic/test");
            response.EnsureSuccessStatusCode();
            var result = await response.Content.ReadAsStringAsync();
            Log.Information("[APISERVICE] Fetched diagnostic data");
            return result;
        }
        catch (HttpRequestException ex)
        {
            Log.Error("[APISERVICE] Failed to fetch diagnostic data: {Message}", ex.Message);
            throw;
        }
    }

    public async Task<LoginResult> LoginAsync(string userName, string password)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(userName))
            {
                Log.Error("[APISERVICE] Login attempt with empty UserName");
                throw new ArgumentException("UserName cannot be empty", nameof(userName));
            }
            if (string.IsNullOrWhiteSpace(password))
            {
                Log.Error("[APISERVICE] Login attempt with empty Password");
                throw new ArgumentException("Password cannot be empty", nameof(password));
            }

            var request = new { UserName = userName, Password = password };
            var requestJson = JsonSerializer.Serialize(request);
            Log.Information("[APISERVICE] Sending login request for UserName={UserName}, Payload={Payload}", userName, requestJson);
            var response = await _httpClient.PostAsJsonAsync("/api/auth/login", request);
            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                Log.Error("[APISERVICE] Login failed for UserName={UserName}: Status={StatusCode}, Error={Error}", userName, response.StatusCode, errorContent);
                throw new HttpRequestException($"Login failed: {response.StatusCode} - {errorContent}");
            }
            var result = await response.Content.ReadFromJsonAsync<LoginResult>();
            if (result == null)
            {
                Log.Error("[APISERVICE] Login response is null for UserName={UserName}", userName);
                throw new InvalidOperationException("Login response is null");
            }
            Log.Information("[APISERVICE] Login successful for UserName={UserName}", userName);
            return result;
        }
        catch (ArgumentException ex)
        {
            Log.Error("[APISERVICE] Invalid login parameters: {Message}", ex.Message);
            throw;
        }
        catch (HttpRequestException ex)
        {
            Log.Error("[APISERVICE] Login failed for UserName={UserName}: {Message}", userName ?? "null", ex.Message);
            throw;
        }
        catch (Exception ex)
        {
            Log.Error("[APISERVICE] Unexpected error during login for UserName={UserName}: {Message}", userName ?? "null", ex.Message);
            throw;
        }
    }

    public async Task<List<Singer>> GetSingersAsync(string eventId)
    {
        try
        {
            ConfigureAuthorizationHeader();
            Log.Information("[APISERVICE] Attempting to fetch singers for event {EventId}", eventId);
            var response = await _httpClient.GetAsync($"/api/dj/events/{eventId}/singers");
            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                Log.Error("[APISERVICE] Failed to fetch singers for event {EventId}: Status={StatusCode}, Message={Message}", eventId, response.StatusCode, errorContent);
                return new List<Singer>();
            }
            var singers = await response.Content.ReadFromJsonAsync<List<Singer>>();
            Log.Information("[APISERVICE] Fetched {Count} singers for event {EventId}", singers?.Count ?? 0, eventId);
            return singers ?? new List<Singer>();
        }
        catch (Exception ex)
        {
            Log.Error("[APISERVICE] Failed to fetch singers for event {EventId}: {Message}", eventId, ex.Message);
            return new List<Singer>();
        }
    }

    public async Task<List<QueueEntry>> GetQueueAsync(string eventId)
    {
        try
        {
            ConfigureAuthorizationHeader();
            Log.Information("[APISERVICE] Attempting to fetch queue entries for event {EventId}", eventId);
            var response = await _httpClient.GetAsync($"/api/dj/events/{eventId}/queue");
            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                Log.Error("[APISERVICE] Failed to fetch queue entries for event {EventId}: Status={StatusCode}, Message={Message}", eventId, response.StatusCode, errorContent);
                throw new HttpRequestException($"Failed to fetch queue: {errorContent}");
            }
            var queueEntries = await response.Content.ReadFromJsonAsync<List<QueueEntry>>();
            Log.Information("[APISERVICE] Fetched {Count} queue entries for event {EventId}", queueEntries?.Count ?? 0, eventId);
            return queueEntries ?? new List<QueueEntry>();
        }
        catch (JsonException ex)
        {
            Log.Error("[APISERVICE] Failed to deserialize queue entries for event {EventId}: {Message}", eventId, ex.Message);
            throw;
        }
        catch (HttpRequestException ex)
        {
            Log.Error("[APISERVICE] Failed to fetch queue entries for event {EventId}: {Message}", eventId, ex.Message);
            throw;
        }
    }

    public async Task ReorderQueueAsync(string eventId, List<string> queueIds)
    {
        try
        {
            ConfigureAuthorizationHeader();
            Log.Information("[APISERVICE] Attempting to reorder queue for EventId={EventId}", eventId);
            var payload = queueIds.Select(int.Parse).ToList();
            var response = await _httpClient.PutAsJsonAsync($"/api/dj/{eventId}/queue/reorder", payload);
            response.EnsureSuccessStatusCode();
            Log.Information("[APISERVICE] Successfully reordered queue for EventId={EventId}", eventId);
        }
        catch (HttpRequestException ex)
        {
            Log.Error("[APISERVICE] Failed to reorder queue for EventId={EventId}: Status={StatusCode}, Message={Message}", eventId, ex.StatusCode, ex.Message);
            throw;
        }
        catch (Exception ex)
        {
            Log.Error("[APISERVICE] Failed to reorder queue for EventId={EventId}: {Message}", eventId, ex.Message);
            throw;
        }
    }

    public async Task PlayAsync(string eventId, string queueId)
    {
        try
        {
            ConfigureAuthorizationHeader();
            Log.Information("[APISERVICE] Sending play request for event {EventId}, queue {QueueId}", eventId, queueId);
            var response = await _httpClient.PostAsync($"/api/events/{eventId}/queue/{queueId}/play", null);
            response.EnsureSuccessStatusCode();
            Log.Information("[APISERVICE] Successfully sent play request for event {EventId}, queue {QueueId}", eventId, queueId);
        }
        catch (HttpRequestException ex)
        {
            Log.Error("[APISERVICE] Failed to play queue {QueueId} for event {EventId}: {Message}", queueId, ex.Message);
            throw;
        }
    }

    public async Task PauseAsync(string eventId, string queueId)
    {
        try
        {
            ConfigureAuthorizationHeader();
            Log.Information("[APISERVICE] Sending pause request for event {EventId}, queue {QueueId}", eventId, queueId);
            var response = await _httpClient.PostAsync($"/api/events/{eventId}/queue/{queueId}/pause", null);
            response.EnsureSuccessStatusCode();
            Log.Information("[APISERVICE] Successfully sent pause request for event {EventId}, queue {QueueId}", eventId, queueId);
        }
        catch (HttpRequestException ex)
        {
            Log.Error("[APISERVICE] Failed to pause queue {QueueId} for event {EventId}: {Message}", queueId, ex.Message);
            throw;
        }
    }

    public async Task StopAsync(string eventId, string queueId)
    {
        try
        {
            ConfigureAuthorizationHeader();
            Log.Information("[APISERVICE] Sending stop request for event {EventId}, queue {QueueId}", eventId, queueId);
            var response = await _httpClient.PostAsync($"/api/events/{eventId}/queue/{queueId}/stop", null);
            response.EnsureSuccessStatusCode();
            Log.Information("[APISERVICE] Successfully sent stop request for event {EventId}, queue {QueueId}", eventId, queueId);
        }
        catch (HttpRequestException ex)
        {
            Log.Error("[APISERVICE] Failed to stop queue {QueueId} for event {EventId}: {Message}", queueId, ex.Message);
            throw;
        }
    }

    public async Task SkipAsync(string eventId, string queueId)
    {
        try
        {
            ConfigureAuthorizationHeader();
            Log.Information("[APISERVICE] Sending skip request for event {EventId}, queue {QueueId}", eventId, queueId);
            var response = await _httpClient.PostAsync($"/api/events/{eventId}/queue/{queueId}/skip", null);
            response.EnsureSuccessStatusCode();
            Log.Information("[APISERVICE] Successfully sent skip request for event {EventId}, queue {QueueId}", eventId, queueId);
        }
        catch (HttpRequestException ex)
        {
            Log.Error("[APISERVICE] Failed to skip queue {QueueId} for event {EventId}: {Message}", queueId, ex.Message);
            throw;
        }
    }

    public async Task LaunchVideoAsync(string eventId, string queueId)
    {
        try
        {
            ConfigureAuthorizationHeader();
            Log.Information("[APISERVICE] Sending launch video request for event {EventId}, queue {QueueId}", eventId, queueId);
            var response = await _httpClient.PostAsync($"/api/events/{eventId}/queue/{queueId}/launch", null);
            response.EnsureSuccessStatusCode();
            Log.Information("[APISERVICE] Successfully sent launch video request for event {EventId}, queue {QueueId}", eventId, queueId);
        }
        catch (HttpRequestException ex)
        {
            Log.Error("[APISERVICE] Failed to launch video for queue {QueueId} for event {EventId}: {Message}", queueId, ex.Message);
            throw;
        }
    }
}
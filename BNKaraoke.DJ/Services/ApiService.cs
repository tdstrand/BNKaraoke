using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading.Tasks;
using BNKaraoke.DJ.Models;

namespace BNKaraoke.DJ.Services
{
    public class ApiService : IApiService
    {
        private readonly IUserSessionService _userSessionService;
        private readonly SettingsService _settingsService;
        private readonly HttpClient _httpClient;

        public ApiService(IUserSessionService userSessionService, SettingsService settingsService)
        {
            _userSessionService = userSessionService;
            _settingsService = settingsService;
            _httpClient = new HttpClient();
            _httpClient.BaseAddress = new Uri("http://localhost:7290"); // Temporary hardcoded URL
        }

        public async Task<List<EventDto>> GetLiveEventsAsync()
        {
            await Task.CompletedTask; // Suppress CS1998
            return new List<EventDto>
            {
                new EventDto { EventId = 3, Description = "Live Event Test 1", EventCode = "TEST1" }
            };
        }

        public async Task JoinEventAsync(string eventId, string phoneNumber)
        {
            await Task.CompletedTask; // Suppress CS1998
        }

        public async Task LeaveEventAsync(string eventId, string phoneNumber)
        {
            await Task.CompletedTask; // Suppress CS1998
        }

        public async Task<string> GetDiagnosticAsync()
        {
            await Task.CompletedTask; // Suppress CS1998
            return "Diagnostic data";
        }

        public async Task<LoginResult> LoginAsync(string phoneNumber, string password)
        {
            await Task.CompletedTask; // Suppress CS1998
            return new LoginResult
            {
                Token = "mock-token",
                UserId = "mock-user-id",
                FirstName = "Mock",
                LastName = "User",
                PhoneNumber = phoneNumber,
                Roles = new List<string> { "Singer" }
            };
        }

        public async Task PlayAsync(string eventId, string queueId)
        {
            try
            {
                var response = await _httpClient.PostAsync($"/api/eventqueue/{eventId}/play?queueId={queueId}", null);
                response.EnsureSuccessStatusCode();
                Serilog.Log.Information("[APISERVICE] Play request sent for event {EventId}, queue {QueueId}", eventId, queueId);
            }
            catch (HttpRequestException ex)
            {
                Serilog.Log.Error("[APISERVICE] Failed to send play request for event {EventId}, queue {QueueId}: {Message}", eventId, queueId, ex.Message);
                throw;
            }
        }

        public async Task PauseAsync(string eventId, string queueId)
        {
            try
            {
                var response = await _httpClient.PostAsync($"/api/eventqueue/{eventId}/pause?queueId={queueId}", null);
                response.EnsureSuccessStatusCode();
                Serilog.Log.Information("[APISERVICE] Pause request sent for event {EventId}, queue {QueueId}", eventId, queueId);
            }
            catch (HttpRequestException ex)
            {
                Serilog.Log.Error("[APISERVICE] Failed to send pause request for event {EventId}, queue {QueueId}: {Message}", eventId, queueId, ex.Message);
                throw;
            }
        }

        public async Task StopAsync(string eventId, string queueId)
        {
            try
            {
                var response = await _httpClient.PostAsync($"/api/eventqueue/{eventId}/stop?queueId={queueId}", null);
                response.EnsureSuccessStatusCode();
                Serilog.Log.Information("[APISERVICE] Stop request sent for event {EventId}, queue {QueueId}", eventId, queueId);
            }
            catch (HttpRequestException ex)
            {
                Serilog.Log.Error("[APISERVICE] Failed to send stop request for event {EventId}, queue {QueueId}: {Message}", eventId, queueId, ex.Message);
                throw;
            }
        }

        public async Task SkipAsync(string eventId, string queueId)
        {
            try
            {
                var response = await _httpClient.PostAsync($"/api/eventqueue/{eventId}/skip?queueId={queueId}", null);
                response.EnsureSuccessStatusCode();
                Serilog.Log.Information("[APISERVICE] Skip request sent for event {EventId}, queue {QueueId}", eventId, queueId);
            }
            catch (HttpRequestException ex)
            {
                Serilog.Log.Error("[APISERVICE] Failed to send skip request for event {EventId}, queue {QueueId}: {Message}", eventId, queueId, ex.Message);
                throw;
            }
        }

        public async Task LaunchVideoAsync(string eventId, string queueId)
        {
            try
            {
                var response = await _httpClient.PostAsync($"/api/eventqueue/{eventId}/launch-video?queueId={queueId}", null);
                response.EnsureSuccessStatusCode();
                Serilog.Log.Information("[APISERVICE] Launch video request sent for event {EventId}, queue {QueueId}", eventId, queueId);
            }
            catch (HttpRequestException ex)
            {
                Serilog.Log.Error("[APISERVICE] Failed to send launch video request for event {EventId}, queue {QueueId}: {Message}", eventId, queueId, ex.Message);
                throw;
            }
        }
    }
}
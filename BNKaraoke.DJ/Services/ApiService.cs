using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading.Tasks;
using BNKaraoke.DJ.Models;
using Serilog;

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
            _httpClient.BaseAddress = new Uri("http://localhost:7290"); // Hardcoded pending SettingsService.cs
        }

        public async Task<List<EventDto>> GetLiveEventsAsync()
        {
            await Task.CompletedTask;
            return new List<EventDto>
            {
                new EventDto { EventId = 3, Description = "Live Event Test 1", EventCode = "TEST1" }
            };
        }

        public async Task JoinEventAsync(string eventId, string phoneNumber)
        {
            await Task.CompletedTask;
        }

        public async Task LeaveEventAsync(string eventId, string phoneNumber)
        {
            await Task.CompletedTask;
        }

        public async Task<string> GetDiagnosticAsync()
        {
            await Task.CompletedTask;
            return "Diagnostic data";
        }

        public async Task<LoginResult> LoginAsync(string phoneNumber, string password)
        {
            await Task.CompletedTask;
            return new LoginResult
            {
                Token = "mock-token",
                FirstName = "Mock",
                PhoneNumber = phoneNumber
            };
        }

        public async Task PlayAsync(string eventId, string queueId)
        {
            try
            {
                var response = await _httpClient.PostAsync($"/api/eventqueue/{eventId}/play?queueId={queueId}", null);
                response.EnsureSuccessStatusCode();
                Log.Information("[APISERVICE] Play request sent for event {EventId}, queue {QueueId}", eventId, queueId);
            }
            catch (HttpRequestException ex)
            {
                Log.Error("[APISERVICE] Failed to send play request for event {EventId}, queue {QueueId}: {Message}", eventId, queueId, ex.Message);
                throw;
            }
        }

        public async Task PauseAsync(string eventId, string queueId)
        {
            try
            {
                var response = await _httpClient.PostAsync($"/api/eventqueue/{eventId}/pause?queueId={queueId}", null);
                response.EnsureSuccessStatusCode();
                Log.Information("[APISERVICE] Pause request sent for event {EventId}, queue {QueueId}", eventId, queueId);
            }
            catch (HttpRequestException ex)
            {
                Log.Error("[APISERVICE] Failed to send pause request for event {EventId}, queue {QueueId}: {Message}", eventId, queueId, ex.Message);
                throw;
            }
        }

        public async Task StopAsync(string eventId, string queueId)
        {
            try
            {
                var response = await _httpClient.PostAsync($"/api/eventqueue/{eventId}/stop?queueId={queueId}", null);
                response.EnsureSuccessStatusCode();
                Log.Information("[APISERVICE] Stop request sent for event {EventId}, queue {QueueId}", eventId, queueId);
            }
            catch (HttpRequestException ex)
            {
                Log.Error("[APISERVICE] Failed to send stop request for event {EventId}, queue {QueueId}: {Message}", eventId, queueId, ex.Message);
                throw;
            }
        }

        public async Task SkipAsync(string eventId, string queueId)
        {
            try
            {
                var response = await _httpClient.PostAsync($"/api/eventqueue/{eventId}/skip?queueId={queueId}", null);
                response.EnsureSuccessStatusCode();
                Log.Information("[APISERVICE] Skip request sent for event {EventId}, queue {QueueId}", eventId, queueId);
            }
            catch (HttpRequestException ex)
            {
                Log.Error("[APISERVICE] Failed to send skip request for event {EventId}, queue {QueueId}: {Message}", eventId, queueId, ex.Message);
                throw;
            }
        }

        public async Task LaunchVideoAsync(string eventId, string queueId)
        {
            try
            {
                var response = await _httpClient.PostAsync($"/api/eventqueue/{eventId}/launch-video?queueId={queueId}", null);
                response.EnsureSuccessStatusCode();
                Log.Information("[APISERVICE] Launch video request sent for event {EventId}, queue {QueueId}", eventId, queueId);
            }
            catch (HttpRequestException ex)
            {
                Log.Error("[APISERVICE] Failed to send launch video request for event {EventId}, queue {QueueId}: {Message}", eventId, queueId, ex.Message);
                throw;
            }
        }

        public async Task<List<Singer>> GetSingersAsync(string eventId)
        {
            try
            {
                await Task.CompletedTask;
                Log.Information("[APISERVICE] Fetching mock singers for event {EventId}", eventId);
                return new List<Singer>
                {
                    new Singer { UserId = "7275651909", DisplayName = "Ted Strand", IsLoggedIn = true, IsJoined = true, IsOnBreak = false }, // Green
                    new Singer { UserId = "9876543210", DisplayName = "Jessica Gann", IsLoggedIn = true, IsJoined = true, IsOnBreak = true }, // Yellow
                    new Singer { UserId = "1112223333", DisplayName = "John Doe", IsLoggedIn = true, IsJoined = false, IsOnBreak = false }, // Orange
                    new Singer { UserId = "4445556666", DisplayName = "Jane Roe", IsLoggedIn = false, IsJoined = false, IsOnBreak = false } // Red
                };
            }
            catch (Exception ex)
            {
                Log.Error("[APISERVICE] Failed to fetch singers for event {EventId}: {Message}", eventId, ex.Message);
                throw;
            }
        }
    }
}
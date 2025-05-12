using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using BNKaraoke.DJ.Models;

// Aliasing our DJ-specific types to avoid conflicts.
using DjEvent = BNKaraoke.DJ.Models.Event;
using DjEventQueue = BNKaraoke.DJ.Models.EventQueue;

namespace BNKaraoke.DJ.Services
{
    /// <summary>
    /// Implements the IApiService interface using HttpClient to call the BNKaraoke.API.
    /// Responses are mapped to DJ‑specific models.
    /// </summary>
    public class ApiService : IApiService
    {
        private readonly HttpClient _httpClient;
        private readonly string _apiBaseUrl;

        public ApiService(HttpClient httpClient)
        {
            _httpClient = httpClient;
            // Typically load this from configuration (appsettings.json) if needed.
            _apiBaseUrl = "http://localhost:7290";
        }

        public async Task<LoginResult> LoginAsync(string username, string password)
        {
            var loginData = new { username, password };
            var response = await _httpClient.PostAsJsonAsync($"{_apiBaseUrl}/api/auth/login", loginData);
            response.EnsureSuccessStatusCode();
            // Ensure a non-null result is returned (adjust as needed).
            return await response.Content.ReadFromJsonAsync<LoginResult>() ?? new LoginResult();
        }

        public async Task<IEnumerable<DjEvent>> GetActiveEventsAsync()
        {
            var response = await _httpClient.GetAsync($"{_apiBaseUrl}/api/event");
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadFromJsonAsync<IEnumerable<DjEvent>>() ?? new List<DjEvent>();
        }

        public async Task CheckInAsync(int eventId)
        {
            var data = new { eventId };
            var response = await _httpClient.PostAsJsonAsync($"{_apiBaseUrl}/api/dj/checkin", data);
            response.EnsureSuccessStatusCode();
        }

        public async Task CheckOutAsync()
        {
            var response = await _httpClient.PostAsync($"{_apiBaseUrl}/api/dj/checkout", null);
            response.EnsureSuccessStatusCode();
        }

        public async Task<DjEventQueue> GetEventQueueAsync()
        {
            var response = await _httpClient.GetAsync($"{_apiBaseUrl}/api/song/queue");
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadFromJsonAsync<DjEventQueue>() ?? new DjEventQueue();
        }

        public async Task AdvanceSongAsync()
        {
            var response = await _httpClient.PutAsync($"{_apiBaseUrl}/api/song/queue/advance", null);
            response.EnsureSuccessStatusCode();
        }

        public async Task ReorderQueueAsync(IEnumerable<int> newOrder)
        {
            var response = await _httpClient.PutAsJsonAsync($"{_apiBaseUrl}/api/song/queue/reorder", newOrder);
            response.EnsureSuccessStatusCode();
        }

        public async Task RemoveSongAsync(int songId)
        {
            var response = await _httpClient.DeleteAsync($"{_apiBaseUrl}/api/song/queue/{songId}");
            response.EnsureSuccessStatusCode();
        }

        public async Task PauseQueueAsync(bool pause)
        {
            var data = new { pause };
            var response = await _httpClient.PutAsJsonAsync($"{_apiBaseUrl}/api/song/queue/pause", data);
            response.EnsureSuccessStatusCode();
        }
    }
}
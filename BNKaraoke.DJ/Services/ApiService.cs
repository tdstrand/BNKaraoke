// File: ApiService.cs
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Tasks;
using BNKaraoke.DJ.Models;

namespace BNKaraoke.DJ.Services
{
    public class ApiService : IApiServices
    {
        private readonly HttpClient _httpClient;

        public ApiService(HttpClient httpClient)
        {
            _httpClient = httpClient;
        }

        public async Task<string> LoginAsync(string phoneNumber, string password)
        {
            var response = await _httpClient.PostAsJsonAsync("/api/auth/login", new
            {
                userName = phoneNumber,
                password = password
            });

            response.EnsureSuccessStatusCode();

            var json = await response.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(json);
            return doc.RootElement.GetProperty("token").GetString()!;
        }

        public async Task<UserInfo> GetUserInfoAsync(string token)
        {
            _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

            var response = await _httpClient.GetAsync("/api/users/me");
            response.EnsureSuccessStatusCode();

            var user = await response.Content.ReadFromJsonAsync<UserInfo>();
            return user!;
        }
    }

    public interface IApiServices
    {
        Task<string> LoginAsync(string phoneNumber, string password);
        Task<UserInfo> GetUserInfoAsync(string token);
    }
}

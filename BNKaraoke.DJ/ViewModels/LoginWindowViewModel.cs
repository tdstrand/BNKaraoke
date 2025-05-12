using Avalonia.Controls;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System;
using System.Diagnostics;
using System.Net.Http;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading.Tasks;

namespace BNKaraoke.DJ.ViewModels
{
    public partial class LoginWindowViewModel : ObservableObject
    {
        private readonly Window _window;
        private readonly HttpClient _httpClient;
        private readonly string _apiUrl = "http://localhost:7290"; // Temporary until SettingsService is implemented

        [ObservableProperty]
        private string? _phoneNumber;

        [ObservableProperty]
        private string? _password;

        [ObservableProperty]
        private string? _errorMessage;

        public LoginWindowViewModel(Window window, HttpClient httpClient)
        {
            _window = window ?? throw new ArgumentNullException(nameof(window));
            _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        }

        [RelayCommand]
        private async Task Login()
        {
            ErrorMessage = null;

            if (string.IsNullOrWhiteSpace(PhoneNumber) || PhoneNumber.Length != 10)
            {
                ErrorMessage = "Please enter a valid 10-digit phone number.";
                return;
            }

            if (string.IsNullOrWhiteSpace(Password))
            {
                ErrorMessage = "Please enter a password.";
                return;
            }

            try
            {
                var request = new { userName = PhoneNumber, Password };
                var requestJson = JsonSerializer.Serialize(request);
                Debug.WriteLine($"Sending login request to {_apiUrl}/api/Auth/login: {requestJson}");
                var content = new StringContent(requestJson, System.Text.Encoding.UTF8, "application/json");
                var response = await _httpClient.PostAsync($"{_apiUrl}/api/Auth/login", content);

                Debug.WriteLine($"Login response: StatusCode={response.StatusCode}");
                if (!response.IsSuccessStatusCode)
                {
                    var responseContent = await response.Content.ReadAsStringAsync();
                    Debug.WriteLine($"Login failed with response: {responseContent}");
                    ErrorMessage = "Invalid credentials.";
                    return;
                }

                var responseContentSuccess = await response.Content.ReadAsStringAsync();
                Debug.WriteLine($"Login response content: {responseContentSuccess}");
                var json = JsonNode.Parse(responseContentSuccess);
                var token = json?["token"]?.ToString();

                if (string.IsNullOrEmpty(token))
                {
                    ErrorMessage = "Authentication failed: No token received.";
                    return;
                }

                // Verify "Karaoke DJ" role in array
                var payload = System.Text.Json.JsonSerializer.Deserialize<JsonNode>(System.Convert.FromBase64String(token.Split('.')[1].PadRight((token.Split('.')[1].Length + 3) & ~3, '=')));
                var roles = payload?["http://schemas.microsoft.com/ws/2008/06/identity/claims/role"]?.AsArray();
                bool hasKaraokeDjRole = false;

                if (roles != null)
                {
                    foreach (var roleNode in roles)
                    {
                        if (roleNode?.ToString() == "Karaoke DJ")
                        {
                            hasKaraokeDjRole = true;
                            break;
                        }
                    }
                }

                if (!hasKaraokeDjRole)
                {
                    ErrorMessage = "Access denied: User does not have Karaoke DJ role.";
                    return;
                }

                // Store token (e.g., in a static class or service)
                // Example: App.Token = token;
                Debug.WriteLine("Login successful, closing LoginWindow.");
                _window.Close(true);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Login exception: {ex.Message}");
                ErrorMessage = $"Login failed: {ex.Message}";
            }
        }
    }
}
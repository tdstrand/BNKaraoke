// File: C:\Users\tstra\source\repos\BNKaraoke\BNKaraoke.DJ\ViewModels\MainWindowViewModel.cs
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using BNKaraoke.DJ.Models;
using System.Collections.ObjectModel;
using Microsoft.AspNetCore.SignalR.Client;
using System.Threading.Tasks;
using System.Net.Http;
using System.Text.Json;
using System.Text;
using System.IdentityModel.Tokens.Jwt;
using BNKaraoke.DJ.Views;
using System.Diagnostics;
using System;
using System.Collections.Generic;
using System.Linq;

namespace BNKaraoke.DJ.ViewModels
{
    public partial class MainWindowViewModel : ObservableObject
    {
        private readonly HubConnection _hubConnection;
        private readonly HttpClient _httpClient;
        private string _eventId = "1"; // Default eventId, adjust as needed
        private string? _jwtToken = string.Empty;
        private string? _userId;
        private string[]? _roles;
        private string? _firstName;
        private string? _lastName;
        private string? _userName;
        private bool _mustChangePassword;
        private readonly Guid _instanceId;
        private readonly MainWindow? _mainWindow;

        public Guid InstanceId { get => _instanceId; private set { } }

        [ObservableProperty]
        private ObservableCollection<EventQueueDto> _queue = new ObservableCollection<EventQueueDto>();

        [ObservableProperty]
        private double _cacheSize = 5.0; // Default 5 GB

        [ObservableProperty]
        private bool _isLoggedIn;

        [ObservableProperty]
        private bool _isCheckedIn;

        [ObservableProperty]
        private string _loginButtonText = "Login";

        [ObservableProperty]
        private string _checkInButtonText = "Join Live Event";

        public MainWindowViewModel(MainWindow? mainWindow = null)
        {
            _instanceId = Guid.NewGuid();
            Debug.WriteLine($"MainWindowViewModel constructor called, InstanceId: {_instanceId}");
            _mainWindow = mainWindow;
            _httpClient = new HttpClient();
            _httpClient.BaseAddress = new Uri("http://localhost:7290/");

            _hubConnection = new HubConnectionBuilder()
                .WithUrl("http://localhost:7290/hub/queue")
                .Build();

            _hubConnection.On<string>("QueueUpdated", async (eventId) =>
            {
                Debug.WriteLine($"[InstanceId: {_instanceId}] QueueUpdated received for EventId: {eventId}");
                _eventId = eventId;
                await FetchQueueAsync();
            });
        }

        [RelayCommand]
        private async Task ToggleLogin(bool? confirmLogout = null)
        {
            Debug.WriteLine($"[InstanceId: {_instanceId}] ToggleLogin command triggered");
            try
            {
                if (IsLoggedIn)
                {
                    Debug.WriteLine($"[InstanceId: {_instanceId}] User is logged in, attempting to logout");
                    if (confirmLogout != true)
                    {
                        if (_mainWindow != null)
                        {
                            Debug.WriteLine($"[InstanceId: {_instanceId}] Requesting logout confirmation");
                            var confirm = await _mainWindow.RequestConfirmationDialog("Logout Confirmation", "Are you sure you want to logout?");
                            Debug.WriteLine($"[InstanceId: {_instanceId}] Logout confirmation result: {confirm}");
                            if (!confirm) return;
                        }
                        else
                        {
                            Debug.WriteLine($"[InstanceId: {_instanceId}] MainWindow is null, cannot request logout confirmation");
                            return;
                        }
                    }

                    _jwtToken = null;
                    _userId = null;
                    _roles = null;
                    _firstName = null;
                    _lastName = null;
                    _userName = null;
                    _mustChangePassword = false;
                    IsLoggedIn = false;
                    IsCheckedIn = false;
                    LoginButtonText = "Login";
                    CheckInButtonText = "Join Live Event";
                    Queue.Clear();

                    if (_hubConnection.State == HubConnectionState.Connected)
                    {
                        Debug.WriteLine($"[InstanceId: {_instanceId}] Stopping SignalR connection");
                        await _hubConnection.StopAsync();
                    }
                }
                else
                {
                    Debug.WriteLine($"[InstanceId: {_instanceId}] User is not logged in, attempting to show login dialog");
                    if (_mainWindow != null)
                    {
                        Debug.WriteLine($"[InstanceId: {_instanceId}] MainWindow reference is set, showing login dialog directly");
                        var (phoneNumber, password) = await _mainWindow.ShowLoginDialogAsync();
                        Debug.WriteLine($"[InstanceId: {_instanceId}] Login dialog returned: phoneNumber={phoneNumber}, password={(password != null ? "[REDACTED]" : "null")}");
                        if (!string.IsNullOrEmpty(phoneNumber) && !string.IsNullOrEmpty(password))
                        {
                            var loginRequest = new { UserName = phoneNumber, Password = password };
                            var content = new StringContent(JsonSerializer.Serialize(loginRequest), Encoding.UTF8, "application/json");
                            try
                            {
                                Debug.WriteLine($"[InstanceId: {_instanceId}] Attempting to login...");
                                var response = await _httpClient.PostAsync("api/auth/login", content);
                                Debug.WriteLine($"[InstanceId: {_instanceId}] API response received: StatusCode={response.StatusCode}");
                                if (response.IsSuccessStatusCode)
                                {
                                    var json = await response.Content.ReadAsStringAsync();
                                    Debug.WriteLine($"[InstanceId: {_instanceId}] API response JSON: {json}");
                                    var loginResponse = JsonSerializer.Deserialize<Dictionary<string, object>>(json);
                                    if (loginResponse != null && loginResponse.TryGetValue("token", out var tokenObj))
                                    {
                                        _jwtToken = tokenObj?.ToString();
                                        Debug.WriteLine($"[InstanceId: {_instanceId}] Login successful, token received.");

                                        // Store additional user data
                                        loginResponse.TryGetValue("userId", out var userIdObj);
                                        _userId = userIdObj?.ToString();
                                        Debug.WriteLine($"[InstanceId: {_instanceId}] UserId: {_userId}");
                                        loginResponse.TryGetValue("roles", out var rolesObj);
                                        _roles = rolesObj is JsonElement rolesElement && rolesElement.ValueKind == JsonValueKind.Array
                                            ? rolesElement.EnumerateArray().Select(e => e.GetString()).Where(s => s != null).Cast<string>().ToArray()
                                            : null;
                                        Debug.WriteLine($"[InstanceId: {_instanceId}] Roles: {string.Join(", ", _roles ?? Array.Empty<string>())}");
                                        loginResponse.TryGetValue("firstName", out var firstNameObj);
                                        _firstName = firstNameObj?.ToString();
                                        Debug.WriteLine($"[InstanceId: {_instanceId}] FirstName: {_firstName}");
                                        loginResponse.TryGetValue("lastName", out var lastNameObj);
                                        _lastName = lastNameObj?.ToString();
                                        Debug.WriteLine($"[InstanceId: {_instanceId}] LastName: {_lastName}");
                                        loginResponse.TryGetValue("userName", out var userNameObj);
                                        _userName = userNameObj?.ToString();
                                        Debug.WriteLine($"[InstanceId: {_instanceId}] UserName: {_userName}");
                                        loginResponse.TryGetValue("mustChangePassword", out var mustChangePasswordObj);
                                        _mustChangePassword = mustChangePasswordObj?.ToString() == "true";
                                        Debug.WriteLine($"[InstanceId: {_instanceId}] MustChangePassword: {_mustChangePassword}");

                                        if (await HasKaraokeDJRole())
                                        {
                                            IsLoggedIn = true;
                                            LoginButtonText = "Logout";
                                            CheckInButtonText = "Join Live Event";
                                            Debug.WriteLine($"[InstanceId: {_instanceId}] User has Karaoke DJ role, login completed.");

                                            if (_mustChangePassword)
                                            {
                                                Debug.WriteLine($"[InstanceId: {_instanceId}] Must change password, showing dialog...");
                                                await _mainWindow.ShowMessageDialog("Password Change Required", "You must change your password before proceeding.");
                                                // In a real app, we'd show a password change dialog here
                                            }

                                            Debug.WriteLine($"[InstanceId: {_instanceId}] Starting SignalR connection...");
                                            await StartSignalRConnection();
                                            Debug.WriteLine($"[InstanceId: {_instanceId}] Fetching queue...");
                                            await FetchQueueAsync();
                                        }
                                        else
                                        {
                                            Debug.WriteLine($"[InstanceId: {_instanceId}] User does not have 'Karaoke DJ' role.");
                                            await _mainWindow.ShowMessageDialog("Access Denied", "You must have the 'Karaoke DJ' role to access this application.");
                                            _jwtToken = null;
                                            _userId = null;
                                            _roles = null;
                                            _firstName = null;
                                            _lastName = null;
                                            _userName = null;
                                            _mustChangePassword = false;
                                        }
                                    }
                                    else
                                    {
                                        Debug.WriteLine($"[InstanceId: {_instanceId}] Login failed: No token in response.");
                                        await _mainWindow.ShowMessageDialog("Login Failed", "Invalid login response.");
                                    }
                                }
                                else
                                {
                                    Debug.WriteLine($"[InstanceId: {_instanceId}] Login failed: StatusCode={response.StatusCode}");
                                    var errorText = await response.Content.ReadAsStringAsync();
                                    Debug.WriteLine($"[InstanceId: {_instanceId}] Error response: {errorText}");
                                    var errorData = JsonSerializer.Deserialize<Dictionary<string, string>>(errorText);
                                    var errorMessage = errorData?.TryGetValue("message", out var msg) == true ? msg : "Invalid phone number or password.";
                                    await _mainWindow.ShowMessageDialog("Login Failed", errorMessage);
                                }
                            }
                            catch (Exception ex)
                            {
                                Debug.WriteLine($"[InstanceId: {_instanceId}] Login error: {ex.Message}");
                                await _mainWindow.ShowMessageDialog("Login Error", $"An error occurred during login: {ex.Message}");
                            }
                        }
                        else
                        {
                            Debug.WriteLine($"[InstanceId: {_instanceId}] Login dialog returned null values");
                        }
                    }
                    else
                    {
                        Debug.WriteLine($"[InstanceId: {_instanceId}] MainWindow reference is null, cannot show login dialog");
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[InstanceId: {_instanceId}] ToggleLogin exception: {ex.Message}");
                if (_mainWindow != null)
                {
                    await _mainWindow.ShowMessageDialog("Error", $"An error occurred: {ex.Message}");
                }
            }
        }

        [RelayCommand]
        private async Task ToggleCheckIn(bool? confirmExit = null)
        {
            if (!IsLoggedIn) return;

            if (IsCheckedIn)
            {
                if (confirmExit != true)
                {
                    if (_mainWindow != null)
                    {
                        var confirm = await _mainWindow.RequestConfirmationDialog("Exit Event Confirmation", "Are you sure you want to exit the live event?");
                        if (!confirm) return;
                    }
                    else
                    {
                        Debug.WriteLine($"[InstanceId: {_instanceId}] MainWindow is null, cannot request confirmation");
                        return;
                    }
                }

                try
                {
                    Debug.WriteLine($"[InstanceId: {_instanceId}] Checking out from EventId: {_eventId}");
                    _httpClient.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _jwtToken);
                    var request = new { RequestorId = _userId }; // Use stored user ID
                    var content = new StringContent(JsonSerializer.Serialize(request), Encoding.UTF8, "application/json");
                    var response = await _httpClient.PostAsync($"api/events/{_eventId}/attendance/check-out", content);
                    if (response.IsSuccessStatusCode)
                    {
                        IsCheckedIn = false;
                        CheckInButtonText = "Join Live Event";
                        Queue.Clear();
                        Debug.WriteLine($"[InstanceId: {_instanceId}] Successfully checked out from the event.");
                    }
                    else
                    {
                        Debug.WriteLine($"[InstanceId: {_instanceId}] Check-out failed: StatusCode={response.StatusCode}");
                        if (_mainWindow != null)
                        {
                            await _mainWindow.ShowMessageDialog("Check-Out Failed", "Failed to exit the live event.");
                        }
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[InstanceId: {_instanceId}] Check-out error: {ex.Message}");
                    if (_mainWindow != null)
                    {
                        await _mainWindow.ShowMessageDialog("Check-Out Error", $"An error occurred while exiting the event: {ex.Message}");
                    }
                }
            }
            else
            {
                try
                {
                    Debug.WriteLine($"[InstanceId: {_instanceId}] Checking in to EventId: {_eventId}");
                    _httpClient.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _jwtToken);
                    var request = new { RequestorId = _userId }; // Use stored user ID
                    var content = new StringContent(JsonSerializer.Serialize(request), Encoding.UTF8, "application/json");
                    var response = await _httpClient.PostAsync($"api/events/{_eventId}/attendance/check-in", content);
                    if (response.IsSuccessStatusCode)
                    {
                        IsCheckedIn = true;
                        CheckInButtonText = "Exit Live Event";
                        await FetchQueueAsync();
                        Debug.WriteLine($"[InstanceId: {_instanceId}] Successfully checked in to the event.");
                    }
                    else
                    {
                        Debug.WriteLine($"[InstanceId: {_instanceId}] Check-in failed: StatusCode={response.StatusCode}");
                        if (_mainWindow != null)
                        {
                            await _mainWindow.ShowMessageDialog("Check-In Failed", "Failed to join the live event. Ensure the event is live.");
                        }
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[InstanceId: {_instanceId}] Check-in error: {ex.Message}");
                    if (_mainWindow != null)
                    {
                        await _mainWindow.ShowMessageDialog("Check-In Error", $"An error occurred while joining the event: {ex.Message}");
                    }
                }
            }
        }

        private async Task<bool> HasKaraokeDJRole()
        {
            try
            {
                var handler = new JwtSecurityTokenHandler();
                var token = handler.ReadJwtToken(_jwtToken);
                // Check all role claims, not just the first one
                var roleClaims = token.Claims.Where(c => c.Type == "role" || c.Type == "http://schemas.microsoft.com/ws/2008/06/identity/claims/role").ToList();
                var hasRole = roleClaims.Any(c => c.Value == "Karaoke DJ");
                Debug.WriteLine($"[InstanceId: {_instanceId}] Role claims: {string.Join(", ", roleClaims.Select(c => c.Value))}, HasRole={hasRole}");
                return hasRole;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[InstanceId: {_instanceId}] Role check error: {ex.Message}");
                return false;
            }
        }

        private async Task StartSignalRConnection()
        {
            try
            {
                Debug.WriteLine($"[InstanceId: {_instanceId}] Attempting to start SignalR connection...");
                await _hubConnection.StartAsync();
                Debug.WriteLine($"[InstanceId: {_instanceId}] SignalR connection established successfully.");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[InstanceId: {_instanceId}] SignalR connection failed: {ex.Message}");
                if (_mainWindow != null)
                {
                    await _mainWindow.ShowMessageDialog("SignalR Error", $"Failed to connect to real-time updates: {ex.Message}");
                }
            }
        }

        // Suppress CS1998 warning: Method does use await for HTTP request, but Dispatcher.UIThread.Post is synchronous
        private async Task FetchQueueAsync()
        {
            try
            {
                Debug.WriteLine($"[InstanceId: {_instanceId}] Fetching queue for EventId: {_eventId}");
                _httpClient.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _jwtToken);
                var response = await _httpClient.GetAsync($"api/events/{_eventId}/queue");
                if (response.IsSuccessStatusCode)
                {
                    var json = await response.Content.ReadAsStringAsync();
                    Debug.WriteLine($"[InstanceId: {_instanceId}] Queue response JSON: {json}");
                    var queueItems = JsonSerializer.Deserialize<List<EventQueueDto>>(json, new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true
                    });

                    if (queueItems != null && queueItems.Any())
                    {
                        Debug.WriteLine($"[InstanceId: {_instanceId}] Fetched {queueItems.Count} queue items.");
                        Avalonia.Threading.Dispatcher.UIThread.Post(() =>
                        {
                            Queue.Clear();
                            foreach (var item in queueItems)
                            {
                                Queue.Add(item);
                            }
                        });
                    }
                    else
                    {
                        Debug.WriteLine($"[InstanceId: {_instanceId}] No queue items found in the response.");
                    }
                }
                else
                {
                    Debug.WriteLine($"[InstanceId: {_instanceId}] Failed to fetch queue: StatusCode={response.StatusCode}, Reason={response.ReasonPhrase}");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[InstanceId: {_instanceId}] Error fetching queue: {ex.Message}");
            }
        }

        [RelayCommand]
        private void SaveSettings()
        {
            Debug.WriteLine($"[InstanceId: {_instanceId}] Cache size saved: {CacheSize} GB");
        }

        public async Task DisposeAsync()
        {
            if (_hubConnection != null)
            {
                Debug.WriteLine($"[InstanceId: {_instanceId}] Disposing SignalR connection");
                await _hubConnection.StopAsync().ConfigureAwait(false);
                await _hubConnection.DisposeAsync().ConfigureAwait(false);
            }
            Debug.WriteLine($"[InstanceId: {_instanceId}] Disposing HttpClient");
            _httpClient?.Dispose();
        }
    }
}
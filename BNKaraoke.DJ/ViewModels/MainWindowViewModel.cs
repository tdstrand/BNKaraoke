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
using System.Linq;
using System.Collections.Generic;

namespace BNKaraoke.DJ.ViewModels
{
    public partial class MainWindowViewModel : ObservableObject
    {
        private readonly HubConnection _hubConnection;
        private readonly HttpClient _httpClient;
        private string? _jwtToken = string.Empty;
        private string? _userId;
        private string[]? _roles;
        private string? _firstName;
        private string? _lastName;
        private string? _userName;
        private bool _mustChangePassword;
        private readonly Guid _instanceId;
        private readonly MainWindow? _mainWindow;
        private readonly Dictionary<string, string> _userNameCache = new Dictionary<string, string>();
        private readonly Dictionary<int, Song> _songCache = new Dictionary<int, Song>();

        public Guid InstanceId { get => _instanceId; private set { } }

        [ObservableProperty]
        private ObservableCollection<EventQueueDto> _queue = new ObservableCollection<EventQueueDto>();

        [ObservableProperty]
        private ObservableCollection<EventDto> _liveEvents = new ObservableCollection<EventDto>();

        [ObservableProperty]
        private EventDto? _selectedEvent;

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

        [ObservableProperty]
        private bool _isLoadingEvents = false;

        [ObservableProperty]
        private bool _canJoinLiveEvent;

        public bool ShouldShowEventDropdown => IsLoggedIn && LiveEvents.Count > 1;

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

            _hubConnection.On<string>("QueueUpdated", (eventId) =>
            {
                Debug.WriteLine($"[InstanceId: {_instanceId}] QueueUpdated received for EventId: {eventId}");
                if (IsCheckedIn && SelectedEvent?.EventId.ToString() == eventId)
                {
                    FetchQueueAsync().GetAwaiter().GetResult(); // Synchronous call for simplicity
                }
            });

            // Subscribe to property changes to update CanJoinLiveEvent
            PropertyChanged += async (sender, e) =>
            {
                if (e.PropertyName is nameof(IsLoggedIn) or nameof(LiveEvents) or nameof(IsLoadingEvents))
                {
                    Debug.WriteLine($"[InstanceId: {_instanceId}] PropertyChanged: {e.PropertyName}, IsLoggedIn={IsLoggedIn}, LiveEvents.Count={LiveEvents.Count}, IsLoadingEvents={IsLoadingEvents}");
                    await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() =>
                    {
                        CanJoinLiveEvent = IsLoggedIn && LiveEvents.Count > 0 && !IsLoadingEvents;
                        Debug.WriteLine($"[InstanceId: {_instanceId}] CanJoinLiveEvent updated: {CanJoinLiveEvent}");
                    });
                }
            };

            // Subscribe to LiveEvents collection changes
            LiveEvents.CollectionChanged += async (sender, e) =>
            {
                await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() =>
                {
                    OnPropertyChanged(nameof(LiveEvents));
                    Debug.WriteLine($"[InstanceId: {_instanceId}] LiveEvents collection changed, Count: {LiveEvents.Count}");
                });
            };
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
                    LiveEvents.Clear();
                    SelectedEvent = null;
                    Queue.Clear();
                    _userNameCache.Clear();
                    _songCache.Clear();

                    if (_hubConnection.State == HubConnectionState.Connected)
                    {
                        Debug.WriteLine($"[InstanceId: {_instanceId}] Stopping SignalR connection");
                        await _hubConnection.StopAsync();
                    }

                    // Explicitly update CanJoinLiveEvent after logout
                    await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() =>
                    {
                        CanJoinLiveEvent = IsLoggedIn && LiveEvents.Count > 0 && !IsLoadingEvents;
                        Debug.WriteLine($"[InstanceId: {_instanceId}] CanJoinLiveEvent after logout: {CanJoinLiveEvent}");
                    });
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
                                        _userName = userNameObj?.ToString() ?? phoneNumber; // Fallback to phoneNumber if userName is empty
                                        Debug.WriteLine($"[InstanceId: {_instanceId}] UserName: {_userName}");
                                        loginResponse.TryGetValue("mustChangePassword", out var mustChangePasswordObj);
                                        _mustChangePassword = mustChangePasswordObj?.ToString() == "true";
                                        Debug.WriteLine($"[InstanceId: {_instanceId}] MustChangePassword: {_mustChangePassword}");

                                        if (HasKaraokeDJRole())
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
                                            Debug.WriteLine($"[InstanceId: {_instanceId}] Fetching live events...");
                                            IsLoadingEvents = true;
                                            await FetchLiveEventsAsync();
                                            IsLoadingEvents = false;

                                            // Explicitly update CanJoinLiveEvent after login
                                            await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() =>
                                            {
                                                CanJoinLiveEvent = IsLoggedIn && LiveEvents.Count > 0 && !IsLoadingEvents;
                                                Debug.WriteLine($"[InstanceId: {_instanceId}] CanJoinLiveEvent after login: {CanJoinLiveEvent}");
                                            });
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

                                            // Explicitly update CanJoinLiveEvent after failed role check
                                            await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() =>
                                            {
                                                CanJoinLiveEvent = IsLoggedIn && LiveEvents.Count > 0 && !IsLoadingEvents;
                                                Debug.WriteLine($"[InstanceId: {_instanceId}] CanJoinLiveEvent after role check failure: {CanJoinLiveEvent}");
                                            });
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
            Debug.WriteLine($"[InstanceId: {_instanceId}] ToggleCheckIn command triggered, IsLoggedIn={IsLoggedIn}, IsCheckedIn={IsCheckedIn}");
            if (!IsLoggedIn)
            {
                Debug.WriteLine($"[InstanceId: {_instanceId}] ToggleCheckIn: Not logged in, returning early.");
                return;
            }

            try
            {
                if (IsCheckedIn)
                {
                    Debug.WriteLine($"[InstanceId: {_instanceId}] ToggleCheckIn: Already checked in, attempting to check out.");
                    if (confirmExit != true)
                    {
                        if (_mainWindow != null)
                        {
                            Debug.WriteLine($"[InstanceId: {_instanceId}] ToggleCheckIn: Requesting check-out confirmation.");
                            var confirm = await _mainWindow.RequestConfirmationDialog("Exit Event Confirmation", $"Are you sure you want to exit {SelectedEvent?.Description}?");
                            Debug.WriteLine($"[InstanceId: {_instanceId}] ToggleCheckIn: Check-out confirmation result: {confirm}");
                            if (!confirm) return;
                        }
                        else
                        {
                            Debug.WriteLine($"[InstanceId: {_instanceId}] ToggleCheckIn: MainWindow is null, cannot request confirmation");
                            return;
                        }
                    }

                    try
                    {
                        Debug.WriteLine($"[InstanceId: {_instanceId}] Checking out from EventId: {SelectedEvent?.EventId}");
                        _httpClient.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _jwtToken);
                        var request = new { RequestorId = _userName };
                        var content = new StringContent(JsonSerializer.Serialize(request), Encoding.UTF8, "application/json");
                        Debug.WriteLine($"[InstanceId: {_instanceId}] Check-out request payload: {JsonSerializer.Serialize(request)}");
                        var response = await _httpClient.PostAsync($"api/events/{SelectedEvent?.EventId}/attendance/check-out", content);
                        if (response.IsSuccessStatusCode)
                        {
                            IsCheckedIn = false;
                            CheckInButtonText = "Join Live Event";
                            await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() =>
                            {
                                OnPropertyChanged(nameof(CheckInButtonText)); // Ensure UI updates
                                Debug.WriteLine($"[InstanceId: {_instanceId}] Successfully checked out from the event.");

                                // Update CanJoinLiveEvent after check-out
                                CanJoinLiveEvent = IsLoggedIn && LiveEvents.Count > 0 && !IsLoadingEvents;
                                Debug.WriteLine($"[InstanceId: {_instanceId}] CanJoinLiveEvent after check-out: {CanJoinLiveEvent}");
                            });
                        }
                        else
                        {
                            Debug.WriteLine($"[InstanceId: {_instanceId}] Check-out failed: StatusCode={response.StatusCode}");
                            var errorText = await response.Content.ReadAsStringAsync();
                            Debug.WriteLine($"[InstanceId: {_instanceId}] Check-out error response: {errorText}");
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
                    Debug.WriteLine($"[InstanceId: {_instanceId}] ToggleCheckIn: Not checked in, attempting to check in.");
                    // If there's exactly one live event, auto-join it
                    if (LiveEvents.Count == 1)
                    {
                        SelectedEvent = LiveEvents.First();
                        Debug.WriteLine($"[InstanceId: {_instanceId}] Auto-joining the only live event: {SelectedEvent.Description}");

                        Debug.WriteLine($"[InstanceId: {_instanceId}] Checking attendance status for EventId: {SelectedEvent.EventId}");
                        _httpClient.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _jwtToken);
                        var statusResponse = await _httpClient.GetAsync($"api/events/{SelectedEvent.EventId}/attendance/status");
                        if (statusResponse.IsSuccessStatusCode)
                        {
                            var statusJson = await statusResponse.Content.ReadAsStringAsync();
                            Debug.WriteLine($"[InstanceId: {_instanceId}] Attendance status response: {statusJson}");
                            var statusData = JsonSerializer.Deserialize<Dictionary<string, object>>(statusJson);
                            if (statusData != null && statusData.TryGetValue("isCheckedIn", out var isCheckedInObj) && bool.TryParse(isCheckedInObj?.ToString(), out var isCheckedIn) && isCheckedIn)
                            {
                                Debug.WriteLine($"[InstanceId: {_instanceId}] User is already checked in to EventId: {SelectedEvent.EventId}");
                                IsCheckedIn = true;
                                CheckInButtonText = $"Leave {SelectedEvent.Description}";
                                await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() =>
                                {
                                    OnPropertyChanged(nameof(CheckInButtonText)); // Ensure UI updates
                                });
                                await FetchQueueAsync();
                                return;
                            }
                        }
                        else
                        {
                            Debug.WriteLine($"[InstanceId: {_instanceId}] Failed to fetch attendance status: StatusCode={statusResponse.StatusCode}");
                            var errorText = await statusResponse.Content.ReadAsStringAsync();
                            Debug.WriteLine($"[InstanceId: {_instanceId}] Attendance status error response: {errorText}");
                        }

                        Debug.WriteLine($"[InstanceId: {_instanceId}] Checking in to EventId: {SelectedEvent.EventId}");
                        var request = new { RequestorId = _userName };
                        var content = new StringContent(JsonSerializer.Serialize(request), Encoding.UTF8, "application/json");
                        Debug.WriteLine($"[InstanceId: {_instanceId}] Check-in request payload: {JsonSerializer.Serialize(request)}");
                        var response = await _httpClient.PostAsync($"api/events/{SelectedEvent.EventId}/attendance/check-in", content);
                        if (response.IsSuccessStatusCode)
                        {
                            IsCheckedIn = true;
                            CheckInButtonText = $"Leave {SelectedEvent.Description}";
                            await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() =>
                            {
                                OnPropertyChanged(nameof(CheckInButtonText)); // Ensure UI updates
                                Debug.WriteLine($"[InstanceId: {_instanceId}] CheckInButtonText updated to: {CheckInButtonText}");
                                Debug.WriteLine($"[InstanceId: {_instanceId}] Successfully checked in to the event.");

                                // Update CanJoinLiveEvent after check-in
                                CanJoinLiveEvent = IsLoggedIn && LiveEvents.Count > 0 && !IsLoadingEvents;
                                Debug.WriteLine($"[InstanceId: {_instanceId}] CanJoinLiveEvent after check-in: {CanJoinLiveEvent}");
                            });
                            await FetchQueueAsync();
                        }
                        else
                        {
                            Debug.WriteLine($"[InstanceId: {_instanceId}] Check-in failed: StatusCode={response.StatusCode}");
                            var errorText = await response.Content.ReadAsStringAsync();
                            Debug.WriteLine($"[InstanceId: {_instanceId}] Check-in error response: {errorText}");
                            var errorData = JsonSerializer.Deserialize<Dictionary<string, string>>(errorText);
                            var errorMessage = errorData?.TryGetValue("message", out var msg) == true ? msg : "Failed to join the live event. Ensure the event is live.";
                            if (_mainWindow != null)
                            {
                                await _mainWindow.ShowMessageDialog("Check-In Failed", errorMessage);
                            }
                        }
                    }
                    else if (LiveEvents.Count > 1)
                    {
                        Debug.WriteLine($"[InstanceId: {_instanceId}] ToggleCheckIn: Multiple live events, checking SelectedEvent.");
                        if (SelectedEvent == null)
                        {
                            if (_mainWindow != null)
                            {
                                Debug.WriteLine($"[InstanceId: {_instanceId}] ToggleCheckIn: No event selected, showing dialog.");
                                await _mainWindow.ShowMessageDialog("Select Event", "Please select an event to join from the dropdown.");
                            }
                            return;
                        }

                        Debug.WriteLine($"[InstanceId: {_instanceId}] Checking attendance status for EventId: {SelectedEvent.EventId}");
                        _httpClient.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _jwtToken);
                        var statusResponse = await _httpClient.GetAsync($"api/events/{SelectedEvent.EventId}/attendance/status");
                        if (statusResponse.IsSuccessStatusCode)
                        {
                            var statusJson = await statusResponse.Content.ReadAsStringAsync();
                            Debug.WriteLine($"[InstanceId: {_instanceId}] Attendance status response: {statusJson}");
                            var statusData = JsonSerializer.Deserialize<Dictionary<string, object>>(statusJson);
                            if (statusData != null && statusData.TryGetValue("isCheckedIn", out var isCheckedInObj) && bool.TryParse(isCheckedInObj?.ToString(), out var isCheckedIn) && isCheckedIn)
                            {
                                Debug.WriteLine($"[InstanceId: {_instanceId}] User is already checked in to EventId: {SelectedEvent.EventId}");
                                IsCheckedIn = true;
                                CheckInButtonText = $"Leave {SelectedEvent.Description}";
                                await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() =>
                                {
                                    OnPropertyChanged(nameof(CheckInButtonText)); // Ensure UI updates
                                });
                                await FetchQueueAsync();
                                return;
                            }
                        }
                        else
                        {
                            Debug.WriteLine($"[InstanceId: {_instanceId}] Failed to fetch attendance status: StatusCode={statusResponse.StatusCode}");
                            var errorText = await statusResponse.Content.ReadAsStringAsync();
                            Debug.WriteLine($"[InstanceId: {_instanceId}] Attendance status error response: {errorText}");
                        }

                        Debug.WriteLine($"[InstanceId: {_instanceId}] Checking in to EventId: {SelectedEvent.EventId}");
                        var request = new { RequestorId = _userName };
                        var content = new StringContent(JsonSerializer.Serialize(request), Encoding.UTF8, "application/json");
                        Debug.WriteLine($"[InstanceId: {_instanceId}] Check-in request payload: {JsonSerializer.Serialize(request)}");
                        var response = await _httpClient.PostAsync($"api/events/{SelectedEvent.EventId}/attendance/check-in", content);
                        if (response.IsSuccessStatusCode)
                        {
                            IsCheckedIn = true;
                            CheckInButtonText = $"Leave {SelectedEvent.Description}";
                            await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() =>
                            {
                                OnPropertyChanged(nameof(CheckInButtonText)); // Ensure UI updates
                                Debug.WriteLine($"[InstanceId: {_instanceId}] CheckInButtonText updated to: {CheckInButtonText}");
                                Debug.WriteLine($"[InstanceId: {_instanceId}] Successfully checked in to the event.");

                                // Update CanJoinLiveEvent after check-in
                                CanJoinLiveEvent = IsLoggedIn && LiveEvents.Count > 0 && !IsLoadingEvents;
                                Debug.WriteLine($"[InstanceId: {_instanceId}] CanJoinLiveEvent after check-in: {CanJoinLiveEvent}");
                            });
                            await FetchQueueAsync();
                        }
                        else
                        {
                            Debug.WriteLine($"[InstanceId: {_instanceId}] Check-in failed: StatusCode={response.StatusCode}");
                            var errorText = await response.Content.ReadAsStringAsync();
                            Debug.WriteLine($"[InstanceId: {_instanceId}] Check-in error response: {errorText}");
                            var errorData = JsonSerializer.Deserialize<Dictionary<string, string>>(errorText);
                            var errorMessage = errorData?.TryGetValue("message", out var msg) == true ? msg : "Failed to join the live event. Ensure the event is live.";
                            if (_mainWindow != null)
                            {
                                await _mainWindow.ShowMessageDialog("Check-In Failed", errorMessage);
                            }
                        }
                    }
                    else
                    {
                        Debug.WriteLine($"[InstanceId: {_instanceId}] ToggleCheckIn: No live events available.");
                        if (_mainWindow != null)
                        {
                            await _mainWindow.ShowMessageDialog("No Live Events", "There are no live events available to join.");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[InstanceId: {_instanceId}] ToggleCheckIn exception: {ex.Message}");
                if (_mainWindow != null)
                {
                    await _mainWindow.ShowMessageDialog("Error", $"An error occurred: {ex.Message}");
                }
            }
        }

        private bool HasKaraokeDJRole()
        {
            try
            {
                var handler = new JwtSecurityTokenHandler();
                var token = handler.ReadJwtToken(_jwtToken);
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

        private async Task FetchLiveEventsAsync()
        {
            try
            {
                Debug.WriteLine($"[InstanceId: {_instanceId}] Fetching live and visible events...");
                _httpClient.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _jwtToken);
                var response = await _httpClient.GetAsync("api/events");
                if (response.IsSuccessStatusCode)
                {
                    var json = await response.Content.ReadAsStringAsync();
                    Debug.WriteLine($"[InstanceId: {_instanceId}] Events response JSON: {json}");
                    var events = JsonSerializer.Deserialize<List<EventDto>>(json, new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true
                    });

                    if (events != null && events.Any())
                    {
                        Debug.WriteLine($"[InstanceId: {_instanceId}] Fetched {events.Count} live events.");
                        await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() =>
                        {
                            LiveEvents.Clear();
                            foreach (var evt in events)
                            {
                                LiveEvents.Add(evt);
                            }
                            // If there's only one event, auto-select it
                            if (LiveEvents.Count == 1)
                            {
                                SelectedEvent = LiveEvents.First();
                            }
                            else
                            {
                                SelectedEvent = null; // Require user to select if multiple events
                            }
                            Debug.WriteLine($"[InstanceId: {_instanceId}] LiveEvents updated with {LiveEvents.Count} events, SelectedEvent: {SelectedEvent?.Description}");

                            // Explicitly update CanJoinLiveEvent after fetching events
                            CanJoinLiveEvent = IsLoggedIn && LiveEvents.Count > 0 && !IsLoadingEvents;
                            Debug.WriteLine($"[InstanceId: {_instanceId}] CanJoinLiveEvent after FetchLiveEventsAsync: {CanJoinLiveEvent}");
                        });
                    }
                    else
                    {
                        Debug.WriteLine($"[InstanceId: {_instanceId}] No live events found in the response.");
                        await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() =>
                        {
                            LiveEvents.Clear();
                            SelectedEvent = null;
                            Debug.WriteLine($"[InstanceId: {_instanceId}] LiveEvents cleared, SelectedEvent set to null");

                            // Explicitly update CanJoinLiveEvent after clearing events
                            CanJoinLiveEvent = IsLoggedIn && LiveEvents.Count > 0 && !IsLoadingEvents;
                            Debug.WriteLine($"[InstanceId: {_instanceId}] CanJoinLiveEvent after clearing events: {CanJoinLiveEvent}");
                        });
                    }
                }
                else
                {
                    Debug.WriteLine($"[InstanceId: {_instanceId}] Failed to fetch live events: StatusCode={response.StatusCode}, Reason={response.ReasonPhrase}");
                    var errorText = await response.Content.ReadAsStringAsync();
                    Debug.WriteLine($"[InstanceId: {_instanceId}] Fetch live events error response: {errorText}");
                    await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() =>
                    {
                        LiveEvents.Clear();
                        SelectedEvent = null;
                        Debug.WriteLine($"[InstanceId: {_instanceId}] LiveEvents cleared due to fetch failure, SelectedEvent set to null");

                        // Explicitly update CanJoinLiveEvent after fetch failure
                        CanJoinLiveEvent = IsLoggedIn && LiveEvents.Count > 0 && !IsLoadingEvents;
                        Debug.WriteLine($"[InstanceId: {_instanceId}] CanJoinLiveEvent after fetch failure: {CanJoinLiveEvent}");
                    });
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[InstanceId: {_instanceId}] Error fetching live events: {ex.Message}");
                await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() =>
                {
                    LiveEvents.Clear();
                    SelectedEvent = null;
                    Debug.WriteLine($"[InstanceId: {_instanceId}] LiveEvents cleared due to exception, SelectedEvent set to null");

                    // Explicitly update CanJoinLiveEvent after exception
                    CanJoinLiveEvent = IsLoggedIn && LiveEvents.Count > 0 && !IsLoadingEvents;
                    Debug.WriteLine($"[InstanceId: {_instanceId}] CanJoinLiveEvent after exception: {CanJoinLiveEvent}");
                });
            }
        }

        // Suppress CS1998 warning: Method does use await for HTTP request, but Dispatcher.UIThread.InvokeAsync is synchronous in some paths
        private async Task FetchQueueAsync()
        {
            try
            {
                if (!IsCheckedIn || SelectedEvent == null)
                {
                    Debug.WriteLine($"[InstanceId: {_instanceId}] Not checked in or no event selected, skipping queue fetch.");
                    await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() =>
                    {
                        Queue.Clear();
                    });
                    return;
                }

                Debug.WriteLine($"[InstanceId: {_instanceId}] Fetching queue for EventId: {SelectedEvent.EventId}");
                _httpClient.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _jwtToken);
                var response = await _httpClient.GetAsync($"api/events/{SelectedEvent.EventId}/queue");
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
                        // Fetch additional data for each queue item
                        foreach (var item in queueItems)
                        {
                            // Fetch requestor display name
                            if (!string.IsNullOrEmpty(item.RequestorUserName))
                            {
                                item.RequestorDisplayName = await GetUserDisplayNameAsync(item.RequestorUserName);
                            }

                            // Fetch song details if Song is incomplete
                            if (item.Song != null && item.Song.Id > 0 && (string.IsNullOrEmpty(item.Song.Title) || string.IsNullOrEmpty(item.Song.Artist)))
                            {
                                item.Song = await GetSongDetailsAsync(item.Song.Id);
                            }
                        }

                        await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() =>
                        {
                            Queue.Clear();
                            foreach (var item in queueItems)
                            {
                                Debug.WriteLine($"[InstanceId: {_instanceId}] Queue item: SongId={item.SongId}, Title={item.Song?.Title}, Artist={item.Song?.Artist}, Requestor={item.RequestorDisplayName}, Singers={(item.Singers != null ? string.Join(", ", item.Singers) : "None")}");
                                Queue.Add(item);
                            }
                        });
                    }
                    else
                    {
                        Debug.WriteLine($"[InstanceId: {_instanceId}] No queue items found in the response.");
                        await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() =>
                        {
                            Queue.Clear();
                        });
                    }
                }
                else
                {
                    Debug.WriteLine($"[InstanceId: {_instanceId}] Failed to fetch queue: StatusCode={response.StatusCode}, Reason={response.ReasonPhrase}");
                    var errorText = await response.Content.ReadAsStringAsync();
                    Debug.WriteLine($"[InstanceId: {_instanceId}] Queue fetch error response: {errorText}");
                    await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() =>
                    {
                        Queue.Clear();
                    });
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[InstanceId: {_instanceId}] Error fetching queue: {ex.Message}");
                await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() =>
                {
                    Queue.Clear();
                });
            }
        }

        // Helper method to fetch the user's display name from their username (phone number)
        private async Task<string> GetUserDisplayNameAsync(string username)
        {
            if (string.IsNullOrEmpty(username)) return username;

            // Check cache first
            if (_userNameCache.TryGetValue(username, out var displayName))
            {
                return displayName;
            }

            try
            {
                Debug.WriteLine($"[InstanceId: {_instanceId}] Fetching user display name for username: {username}");
                _httpClient.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _jwtToken);
                var response = await _httpClient.GetAsync($"api/users/{username}");
                if (response.IsSuccessStatusCode)
                {
                    var json = await response.Content.ReadAsStringAsync();
                    Debug.WriteLine($"[InstanceId: {_instanceId}] User response JSON: {json}");
                    var userData = JsonSerializer.Deserialize<Dictionary<string, object>>(json, new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true
                    });

                    if (userData != null)
                    {
                        var firstName = userData.GetValueOrDefault("firstName")?.ToString() ?? string.Empty;
                        var lastName = userData.GetValueOrDefault("lastName")?.ToString() ?? string.Empty;
                        displayName = $"{firstName} {lastName}".Trim();
                        if (string.IsNullOrEmpty(displayName))
                        {
                            displayName = username; // Fallback to username if no name is available
                        }
                        _userNameCache[username] = displayName;
                        return displayName;
                    }
                }
                else
                {
                    Debug.WriteLine($"[InstanceId: {_instanceId}] Failed to fetch user display name: StatusCode={response.StatusCode}");
                    var errorText = await response.Content.ReadAsStringAsync();
                    Debug.WriteLine($"[InstanceId: {_instanceId}] User fetch error response: {errorText}");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[InstanceId: {_instanceId}] Error fetching user display name: {ex.Message}");
            }

            // Fallback to username if the API call fails
            _userNameCache[username] = username;
            return username;
        }

        // Helper method to fetch song details if not provided in the queue response
        private async Task<Song> GetSongDetailsAsync(int songId)
        {
            if (songId <= 0) return new Song();

            // Check cache first
            if (_songCache.TryGetValue(songId, out var song))
            {
                return song;
            }

            try
            {
                Debug.WriteLine($"[InstanceId: {_instanceId}] Fetching song details for SongId: {songId}");
                _httpClient.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _jwtToken);
                var response = await _httpClient.GetAsync($"api/songs/{songId}");
                if (response.IsSuccessStatusCode)
                {
                    var json = await response.Content.ReadAsStringAsync();
                    Debug.WriteLine($"[InstanceId: {_instanceId}] Song response JSON: {json}");
                    song = JsonSerializer.Deserialize<Song>(json, new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true
                    });

                    if (song != null)
                    {
                        _songCache[songId] = song;
                        return song;
                    }
                }
                else
                {
                    Debug.WriteLine($"[InstanceId: {_instanceId}] Failed to fetch song details: StatusCode={response.StatusCode}");
                    var errorText = await response.Content.ReadAsStringAsync();
                    Debug.WriteLine($"[InstanceId: {_instanceId}] Song fetch error response: {errorText}");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[InstanceId: {_instanceId}] Error fetching song details: {ex.Message}");
            }

            // Fallback to an empty song if the API call fails
            song = new Song { Id = songId, Title = "Unknown Song", Artist = "Unknown Artist" };
            _songCache[songId] = song;
            return song;
        }

        // Helper method to format singers based on the rules (currently not used, kept for future implementation)
        public string FormatSingers(List<string> singers)
        {
            if (singers == null || !singers.Any()) return string.Empty;

            // Parse each singer's name into first and last names
            var parsedSingers = singers.Select(s =>
            {
                var parts = s?.Split(' ', StringSplitOptions.RemoveEmptyEntries) ?? Array.Empty<string>();
                return new
                {
                    FullName = s ?? string.Empty,
                    FirstName = parts.Length > 0 ? parts[0] : string.Empty,
                    LastName = parts.Length > 1 ? parts[1] : string.Empty
                };
            }).ToList();

            // Group singers by FirstName to detect duplicates
            var groupedByFirstName = parsedSingers.GroupBy(s => s.FirstName)
                                                  .ToDictionary(g => g.Key, g => g.ToList());

            var formattedNames = new List<string>();
            foreach (var singer in parsedSingers)
            {
                var sameFirstNameSingers = groupedByFirstName[singer.FirstName];
                if (sameFirstNameSingers.Count == 1)
                {
                    // Only one singer with this first name, use FirstName only
                    formattedNames.Add(singer.FirstName);
                }
                else
                {
                    // Multiple singers with the same first name, check last initial
                    var sameLastInitial = sameFirstNameSingers
                        .Where(s => s.LastName.Length > 0 && singer.LastName.Length > 0 && s.LastName[0] == singer.LastName[0])
                        .ToList();

                    if (sameLastInitial.Count == 1)
                    {
                        // Only one singer with this first name and last initial, use FirstName + LastInitial
                        formattedNames.Add(singer.LastName.Length > 0 ? $"{singer.FirstName} {singer.LastName[0]}" : singer.FirstName);
                    }
                    else
                    {
                        // Multiple singers with the same first name and last initial, use FirstName + LastName
                        formattedNames.Add(singer.FullName);
                    }
                }
            }

            return string.Join(", ", formattedNames);
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
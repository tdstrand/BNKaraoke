using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using BNKaraoke.DJ.Services;
using BNKaraoke.DJ.ViewModels;
using CommunityToolkit.Mvvm.Input;
using Microsoft.AspNetCore.SignalR.Client;
using Serilog;

namespace BNKaraoke.DJ.Views;

#pragma warning disable CS8622 // Suppress nullability warning for event handlers
public partial class DJScreen : Window
{
    private readonly IUserSessionService _userSessionService;
    private readonly IApiService _apiService;
    private string? _currentEventId;
    private HubConnection? _queueHubConnection;
    private HubConnection? _singersHubConnection;

    public DJScreen()
    {
        _userSessionService = UserSessionService.Instance;
        _apiService = new ApiService(_userSessionService);
        _currentEventId = null;
        InitializeComponent();
        DataContext = new DJScreenViewModel();
        Loaded += DJScreen_Loaded;
        Closing += DJScreen_Closing;
    }

    private async void DJScreen_Loaded(object sender, RoutedEventArgs e)
    {
        Log.Information("[DJSCREEN] Window loaded");
        await InitializeSignalR();
    }

    private async void DJScreen_Closing(object sender, System.ComponentModel.CancelEventArgs e)
    {
        Log.Information("[DJSCREEN] Window closing");
        if (_userSessionService.IsAuthenticated)
        {
            if (!string.IsNullOrEmpty(_currentEventId))
            {
                try
                {
                    await _apiService.LeaveEventAsync(_currentEventId, _userSessionService.PhoneNumber ?? string.Empty);
                    Log.Information("[DJSCREEN] Left event on close: {EventId}", _currentEventId);
                }
                catch (Exception ex)
                {
                    Log.Error("[DJSCREEN] Failed to leave event on close {EventId}: {Message}", _currentEventId, ex.Message);
                }
            }
            _userSessionService.ClearSession();
            Log.Information("[DJSCREEN] Cleared session on close");
        }
        await DisconnectSignalR();
    }

    private async Task InitializeSignalR()
    {
        try
        {
            _queueHubConnection = new HubConnectionBuilder()
                .WithUrl("http://localhost:7290/hubs/queue")
                .Build();
            _singersHubConnection = new HubConnectionBuilder()
                .WithUrl("http://localhost:7290/hubs/singers")
                .Build();

            _queueHubConnection.On<string>("QueueUpdated", (message) =>
            {
                Log.Information("[DJSCREEN] Queue updated via SignalR: {Message}", message);
                // Placeholder: Update DJQueue
            });

            _singersHubConnection.On<string>("SingersUpdated", (message) =>
            {
                Log.Information("[DJSCREEN] Singers updated via SignalR: {Message}", message);
                // Placeholder: Update Singers Detail
            });

            await _queueHubConnection.StartAsync();
            await _singersHubConnection.StartAsync();
            Log.Information("[DJSCREEN] SignalR connections established");
        }
        catch (Exception ex)
        {
            Log.Error("[DJSCREEN] Failed to initialize SignalR: {Message}", ex.Message);
        }
    }

    private async Task DisconnectSignalR()
    {
        try
        {
            if (_queueHubConnection != null)
            {
                await _queueHubConnection.StopAsync();
                Log.Information("[DJSCREEN] Queue SignalR connection stopped");
            }
            if (_singersHubConnection != null)
            {
                await _singersHubConnection.StopAsync();
                Log.Information("[DJSCREEN] Singers SignalR connection stopped");
            }
        }
        catch (Exception ex)
        {
            Log.Error("[DJSCREEN] Failed to disconnect SignalR: {Message}", ex.Message);
        }
    }
}
#pragma warning restore CS8622
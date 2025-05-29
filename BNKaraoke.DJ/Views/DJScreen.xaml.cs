using BNKaraoke.DJ.Models;
using BNKaraoke.DJ.Services;
using BNKaraoke.DJ.ViewModels;
using Serilog;
using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace BNKaraoke.DJ.Views;

public partial class DJScreen : Window
{
    public DJScreen()
    {
        InitializeComponent();
        DataContext = new DJScreenViewModel(new VideoCacheService(SettingsService.Instance));
        Log.Information("[DJSCREEN] Initializing window");
    }

    private void Window_Loaded(object sender, RoutedEventArgs e)
    {
        try
        {
            Log.Information("[DJSCREEN] Window loaded");
            if (DataContext is DJScreenViewModel viewModel)
            {
                viewModel.UpdateAuthenticationState();
                Log.Information("[DJSCREEN] Forced UpdateAuthenticationState on load");
                InvalidateVisual();
                Log.Information("[DJSCREEN] Forced UI refresh with InvalidateVisual");
            }
        }
        catch (Exception ex)
        {
            Log.Error("[DJSCREEN] Window load failed: {Message}, StackTrace={StackTrace}", ex.Message, ex.StackTrace);
        }
    }

    private void ListViewItem_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        try
        {
            Log.Information("[DJSCREEN] PreviewMouseLeftButtonDown triggered");
            if (sender is ListViewItem item && item.DataContext is QueueEntry queueEntry)
            {
                if (DataContext is DJScreenViewModel viewModel)
                {
                    viewModel.StartDragCommand.Execute(queueEntry);
                }
                else
                {
                    Log.Error("[DJSCREEN] Drag failed: DataContext is not DJScreenViewModel");
                }
            }
            else
            {
                Log.Error("[DJSCREEN] Drag failed: Invalid sender or DataContext, SenderType={SenderType}", sender?.GetType().Name);
            }
        }
        catch (Exception ex)
        {
            Log.Error("[DJSCREEN] PreviewMouseLeftButtonDown failed: {Message}, StackTrace={StackTrace}", ex.Message, ex.StackTrace);
        }
    }

    protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
    {
        try
        {
            Log.Information("[DJSCREEN] Window closing");
            if (DataContext is DJScreenViewModel viewModel && viewModel.IsAuthenticated)
            {
                Log.Information("[DJSCREEN] User is authenticated, initiating logout");
                // Execute logout command to leave event and clear session
                viewModel.LoginLogoutCommand.Execute(null);
                Log.Information("[DJSCREEN] Logout command executed");
            }
            base.OnClosing(e);
        }
        catch (Exception ex)
        {
            Log.Error("[DJSCREEN] Window closing failed: {Message}, StackTrace={StackTrace}", ex.Message, ex.StackTrace);
        }
    }
}
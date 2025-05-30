using BNKaraoke.DJ.Models;
using BNKaraoke.DJ.ViewModels;
using CommunityToolkit.Mvvm.Input;
using Serilog;
using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace BNKaraoke.DJ.Views;

public partial class DJScreen : Window
{
    public DJScreen()
    {
        InitializeComponent();
        try
        {
            DataContext = new DJScreenViewModel();
        }
        catch (Exception ex)
        {
            Log.Error("[DJSCREEN] Failed to initialize DJScreen: {Message}", ex.Message);
            MessageBox.Show($"Failed to initialize DJScreen: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            Close();
        }
    }

    private void Window_Loaded(object sender, RoutedEventArgs e)
    {
        try
        {
            var viewModel = DataContext as DJScreenViewModel;
            if (viewModel == null)
            {
                Log.Error("[DJSCREEN] Failed to load ViewModel");
                MessageBox.Show("Failed to load ViewModel.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                Close();
                return;
            }
        }
        catch (Exception ex)
        {
            Log.Error("[DJSCREEN] Failed to load DJScreen: {Message}", ex.Message);
            MessageBox.Show($"Failed to load DJScreen: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            Close();
        }
    }

    private void ListViewItem_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        try
        {
            if (sender is ListViewItem item && item.IsSelected)
            {
                var queueEntry = item.DataContext as QueueEntry;
                if (queueEntry != null)
                {
                    var viewModel = DataContext as DJScreenViewModel;
                    if (viewModel != null)
                    {
                        viewModel.StartDragCommand.Execute(queueEntry);
                        Log.Information("[DJSCREEN] Drag initiated for QueueId={QueueId}", queueEntry.QueueId);
                    }
                    else
                    {
                        Log.Warning("[DJSCREEN] ViewModel is null in drag handler");
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Log.Error("[DJSCREEN] Failed to initiate drag: {Message}", ex.Message);
            MessageBox.Show($"Failed to initiate drag: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void ListViewItem_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        try
        {
            Log.Information("[DJSCREEN] Double-click event triggered");
            if (sender is ListViewItem item && item.IsSelected)
            {
                var queueEntry = item.DataContext as QueueEntry;
                if (queueEntry == null)
                {
                    Log.Warning("[DJSCREEN] Double-click ignored: QueueEntry is null");
                    return;
                }
                var viewModel = DataContext as DJScreenViewModel;
                if (viewModel == null)
                {
                    Log.Warning("[DJSCREEN] Double-click ignored: ViewModel is null");
                    return;
                }
                viewModel.SelectedQueueEntry = queueEntry; // Ensure selection
                Log.Information("[DJSCREEN] Selected QueueId={QueueId}", queueEntry.QueueId);
                var command = viewModel.PlayQueueItemCommand as IRelayCommand;
                if (command != null && command.CanExecute(null))
                {
                    Log.Information("[DJSCREEN] Executing PlayQueueItemCommand for QueueId={QueueId}", queueEntry.QueueId);
                    Application.Current.Dispatcher.Invoke(() => command.Execute(null));
                }
                else
                {
                    Log.Warning("[DJSCREEN] PlayQueueItemCommand not executable: CommandExists={CommandExists}, CanExecute={CanExecute}, IsShowActive={IsShowActive}",
                        command != null, command?.CanExecute(null) ?? false, viewModel.IsShowActive);
                }
            }
            else
            {
                Log.Information("[DJSCREEN] Double-click ignored: SenderType={SenderType}, IsSelected={IsSelected}",
                    sender?.GetType().Name, (sender as ListViewItem)?.IsSelected ?? false);
            }
        }
        catch (Exception ex)
        {
            Log.Error("[DJSCREEN] Failed to handle double-click: {Message}, StackTrace={StackTrace}", ex.Message, ex.StackTrace);
            MessageBox.Show($"Failed to handle double-click: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }
}
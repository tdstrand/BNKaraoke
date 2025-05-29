using BNKaraoke.DJ.Models;
using BNKaraoke.DJ.ViewModels;
using CommunityToolkit.Mvvm.Input;
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
                MessageBox.Show("Failed to load ViewModel.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                Close();
                return;
            }
        }
        catch (Exception ex)
        {
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
                    viewModel?.StartDragCommand.Execute(queueEntry);
                }
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Failed to initiate drag: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void ListViewItem_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        try
        {
            if (sender is ListViewItem item && item.IsSelected)
            {
                var viewModel = DataContext as DJScreenViewModel;
                if (viewModel?.PlayQueueItemCommand is IRelayCommand command && command.CanExecute(null))
                {
                    command.Execute(null);
                }
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Failed to handle double-click: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }
}
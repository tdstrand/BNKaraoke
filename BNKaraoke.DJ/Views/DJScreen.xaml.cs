using BNKaraoke.DJ.Models;
using BNKaraoke.DJ.ViewModels;
using CommunityToolkit.Mvvm.Input;
using Serilog;
using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using BNKaraoke.DJ.Converters;

namespace BNKaraoke.DJ.Views
{
    public partial class DJScreen : Window
    {
        private readonly SingerStatusToColorConverter _colorConverter = new SingerStatusToColorConverter();

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
                    viewModel.SelectedQueueEntry = queueEntry;
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

        private void SingersContextMenu_Opening(object sender, ContextMenuEventArgs e)
        {
            try
            {
                if (sender is ListView listView && listView.SelectedItem is Singer selectedSinger)
                {
                    var viewModel = DataContext as DJScreenViewModel;
                    if (viewModel == null)
                    {
                        Log.Warning("[DJSCREEN] Singers ContextMenu: ViewModel is null");
                        e.Handled = true;
                        return;
                    }

                    var contextMenu = listView.ContextMenu;
                    if (contextMenu == null)
                    {
                        Log.Warning("[DJSCREEN] Singers ContextMenu: ContextMenu is null");
                        e.Handled = true;
                        return;
                    }

                    // Ensure UI thread and refresh bindings
                    Application.Current.Dispatcher.InvokeAsync(() =>
                    {
                        listView.Items.Refresh();
                        listView.UpdateLayout();
                        listView.ItemsSource = null; // Clear ItemsSource
                        listView.ItemsSource = viewModel.Singers; // Rebind
                        Log.Information("[DJSCREEN] ListView ItemsSource rebound: Count={Count}", viewModel.Singers.Count);
                    }).Wait();

                    // Clone Singer to ensure fresh data
                    var singer = viewModel.Singers.FirstOrDefault(s => s.UserId == selectedSinger.UserId);
                    if (singer == null)
                    {
                        Log.Warning("[DJSCREEN] Singers ContextMenu: Singer not found in viewModel.Singers for UserId={UserId}", selectedSinger.UserId);
                        e.Handled = true;
                        return;
                    }
                    var clonedSinger = new Singer
                    {
                        UserId = singer.UserId,
                        DisplayName = singer.DisplayName,
                        IsLoggedIn = singer.IsLoggedIn,
                        IsJoined = singer.IsJoined,
                        IsOnBreak = singer.IsOnBreak
                    };

                    Log.Information("[DJSCREEN] Singer state before color conversion: UserId={UserId}, DisplayName={DisplayName}, IsLoggedIn={IsLoggedIn}, IsJoined={IsJoined}, IsOnBreak={IsOnBreak}",
                        clonedSinger.UserId, clonedSinger.DisplayName, clonedSinger.IsLoggedIn, clonedSinger.IsJoined, clonedSinger.IsOnBreak);
                    Log.Information("[DJSCREEN] Singers collection state: Count={Count}, Names={Names}",
                        viewModel.Singers.Count, string.Join(", ", viewModel.Singers.Select(s => s.DisplayName)));

                    var previousColor = _colorConverter.Convert(clonedSinger, typeof(Brush), null, System.Globalization.CultureInfo.InvariantCulture) as SolidColorBrush;
                    var previousColorHex = previousColor != null ? $"#{previousColor.Color.R:X2}{previousColor.Color.G:X2}{previousColor.Color.B:X2}" : "Unknown";

                    foreach (var item in contextMenu.Items)
                    {
                        if (item is MenuItem menuItem)
                        {
                            menuItem.Click -= (s, args) => { };
                            menuItem.Click += (s, args) =>
                            {
                                try
                                {
                                    Log.Information("[DJSCREEN] MenuItem clicked: Name={Name}", menuItem.Name);
                                    string status;
                                    switch (menuItem.Name)
                                    {
                                        case "SetAvailableMenuItem":
                                            status = "Active";
                                            break;
                                        case "SetOnBreakMenuItem":
                                            status = "OnBreak";
                                            break;
                                        case "SetNotJoinedMenuItem":
                                            status = "NotJoined";
                                            break;
                                        case "SetLoggedOutMenuItem":
                                            status = "LoggedOut";
                                            break;
                                        default:
                                            status = string.Empty;
                                            Log.Warning("[DJSCREEN] Unknown MenuItem: {Name}", menuItem.Name);
                                            break;
                                    }

                                    if (!string.IsNullOrEmpty(status))
                                    {
                                        var parameter = $"{status}|{singer.UserId}";
                                        Log.Information("[DJSCREEN] Right-click operation: User={DisplayName}, UserId={UserId}, PreviousColor={PreviousColor}, NewStatus={Status}",
                                            singer.DisplayName, singer.UserId, previousColorHex, status);
                                        viewModel.UpdateSingerStatusCommand.Execute(parameter);
                                        Application.Current.Dispatcher.InvokeAsync(() =>
                                        {
                                            listView.Items.Refresh();
                                            listView.UpdateLayout();
                                            listView.ItemsSource = null;
                                            listView.ItemsSource = viewModel.Singers;
                                            Log.Information("[DJSCREEN] ListView refreshed after status update: UserId={UserId}, Status={Status}", singer.UserId, status);
                                        });
                                    }
                                }
                                catch (Exception ex)
                                {
                                    Log.Error("[DJSCREEN] Failed to handle MenuItem click: {Message}", ex.Message);
                                    MessageBox.Show($"Failed to update singer status: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                                }
                            };
                        }
                    }
                }
                else
                {
                    Log.Information("[DJSCREEN] Singers ContextMenu: No singer selected or invalid sender");
                    e.Handled = true;
                }
            }
            catch (Exception ex)
            {
                Log.Error("[DJSCREEN] Failed to open Singers ContextMenu: {Message}", ex.Message);
                MessageBox.Show($"Failed to open context menu: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                e.Handled = true;
            }
        }
    }
}
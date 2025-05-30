using BNKaraoke.DJ.Models;
using BNKaraoke.DJ.Views;
using CommunityToolkit.Mvvm.Input;
using LibVLCSharp.Shared;
using Serilog;
using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;

namespace BNKaraoke.DJ.ViewModels;

public partial class DJScreenViewModel
{
    [RelayCommand]
    private void ShowSongDetails()
    {
        Log.Information("[DJSCREEN] ShowSongDetails command invoked");
        if (SelectedQueueEntry != null)
        {
            try
            {
                var songDetailsWindow = new SongDetailsWindow
                {
                    WindowStartupLocation = WindowStartupLocation.CenterScreen,
                    DataContext = new SongDetailsViewModel { SelectedQueueEntry = SelectedQueueEntry }
                };
                songDetailsWindow.ShowDialog();
                Log.Information("[DJSCREEN] SongDetailsWindow closed");
            }
            catch (Exception ex)
            {
                Log.Error("[DJSCREEN] Failed to show SongDetailsWindow: {Message}", ex.Message);
                WarningMessage = $"Failed to show song details: {ex.Message}";
            }
        }
        else
        {
            Log.Information("[DJSCREEN] No queue entry selected for song details");
            WarningMessage = "Please select a song to view details.";
        }
    }

    [RelayCommand]
    private void StartDrag(QueueEntry draggedItem)
    {
        try
        {
            Log.Information("[DJSCREEN] StartDrag command invoked for QueueId={QueueId}", draggedItem?.QueueId ?? -1);
            if (draggedItem == null)
            {
                Log.Error("[DJSCREEN] Drag failed: Dragged item is null");
                WarningMessage = "Drag failed: No item selected.";
                return;
            }

            var listView = Application.Current.Windows.OfType<DJScreen>()
                .Select(w => w.FindName("QueueListView") as ListView)
                .FirstOrDefault(lv => lv != null);

            if (listView == null)
            {
                Log.Error("[DJSCREEN] Drag failed: QueueListView not found");
                WarningMessage = "Drag failed: Queue not found.";
                return;
            }

            Log.Information("[DJSCREEN] Initiating DragDrop for queue {QueueId}", draggedItem.QueueId);
            var data = new DataObject(typeof(QueueEntry), draggedItem);
            DragDrop.DoDragDrop(listView, data, DragDropEffects.Move);
            Log.Information("[DJSCREEN] Completed drag for queue {QueueId}", draggedItem.QueueId);
        }
        catch (Exception ex)
        {
            Log.Error("[DJSCREEN] Drag failed: {Message}", ex.Message);
            WarningMessage = $"Failed to drag: {ex.Message}";
        }
    }

    [RelayCommand]
    private async Task Drop(DragEventArgs e)
    {
        try
        {
            Log.Information("[DJSCREEN] Drop command invoked: SourceType={SourceType}, Handled={Handled}", e?.Source?.GetType().Name ?? "null", e?.Handled ?? false);
            if (string.IsNullOrEmpty(_currentEventId))
            {
                Log.Warning("[DJSCREEN] Drop failed: No event joined");
                WarningMessage = "Drop failed: No event joined.";
                return;
            }

            if (e == null)
            {
                Log.Error("[DJSCREEN] Drop failed: DragEventArgs is null");
                WarningMessage = "Drop failed: Invalid drag data.";
                return;
            }

            Log.Information("[DJSCREEN] Accessing dragged data");
            var draggedItem = e.Data.GetData(typeof(QueueEntry)) as QueueEntry;
            if (draggedItem == null)
            {
                Log.Warning("[DJSCREEN] Drop failed: Dragged item is null or not a QueueEntry");
                WarningMessage = "Drop failed: Invalid dragged item.";
                return;
            }

            Log.Information("[DJSCREEN] Accessing target element");
            var target = e.OriginalSource as FrameworkElement;
            var targetItem = target?.DataContext as QueueEntry;

            if (targetItem == null)
            {
                Log.Warning("[DJSCREEN] Drop failed: Target item is null or not a QueueEntry, OriginalSourceType={OriginalSourceType}", e.OriginalSource?.GetType().Name);
                WarningMessage = "Drop failed: Invalid target item.";
                return;
            }

            if (draggedItem == targetItem)
            {
                Log.Information("[DJSCREEN] Drop ignored: Dragged item is the same as target");
                return;
            }

            if (IsPlaying && PlayingQueueEntry != null &&
                (draggedItem.QueueId == PlayingQueueEntry.QueueId || targetItem.QueueId == PlayingQueueEntry.QueueId))
            {
                Log.Information("[DJSCREEN] Drop failed: Cannot reorder playing song, QueueId={QueueId}", draggedItem.QueueId);
                WarningMessage = "Cannot reorder the playing song.";
                return;
            }

            Log.Information("[DJSCREEN] Calculating indices for queue {QueueId}", draggedItem.QueueId);
            int sourceIndex = QueueEntries.IndexOf(draggedItem);
            int targetIndex = QueueEntries.IndexOf(targetItem);

            if (sourceIndex < 0 || targetIndex < 0)
            {
                Log.Warning("[DJSCREEN] Drop failed: Invalid source or target index, SourceIndex={SourceIndex}, TargetIndex={TargetIndex}", sourceIndex, targetIndex);
                WarningMessage = "Drop failed: Invalid queue indices.";
                return;
            }

            Log.Information("[DJSCREEN] Reordering queue locally");
            Application.Current.Dispatcher.Invoke(() =>
            {
                QueueEntries.Move(sourceIndex, targetIndex);
                for (int i = 0; i < QueueEntries.Count; i++)
                {
                    QueueEntries[i].Position = i + 1;
                }
            });

            var queueIds = QueueEntries.Select(q => q.QueueId.ToString()).ToList();
            Log.Information("[DJSCREEN] Reorder payload: EventId={EventId}, QueueIds={QueueIds}", _currentEventId, string.Join(",", queueIds));

            try
            {
                await _apiService.ReorderQueueAsync(_currentEventId, queueIds);
                Log.Information("[DJSCREEN] Queue reordered for event {EventId}, dropped {SourceQueueId} to position {TargetIndex}",
                    _currentEventId, draggedItem.QueueId, targetIndex + 1);

                await LoadQueueData();
                Log.Information("[DJSCREEN] Refreshed queue data after reorder for event {EventId}", _currentEventId);
            }
            catch (Exception ex)
            {
                Log.Error("[DJSCREEN] Failed to persist queue order: {Message}", ex.Message);
                WarningMessage = $"Failed to reorder queue: {ex.Message}";
                await LoadQueueData(); // Revert to server state
            }
        }
        catch (Exception ex)
        {
            Log.Error("[DJSCREEN] Drop failed: {Message}", ex.Message);
            WarningMessage = $"Failed to reorder queue: {ex.Message}";
        }
    }

    private async Task LoadQueueData()
    {
        try
        {
            Log.Information("[DJSCREEN] Loading queue data for event: {EventId}", _currentEventId);
            if (string.IsNullOrEmpty(_currentEventId))
            {
                Log.Information("[DJSCREEN] No event joined, skipping queue data load");
                QueueEntries.Clear();
                return;
            }

            var queueEntries = await _apiService.GetQueueAsync(_currentEventId);
            await Application.Current.Dispatcher.InvokeAsync(() =>
            {
                QueueEntries.Clear();
                foreach (var entry in queueEntries.OrderBy(q => q.Position))
                {
                    if (_videoCacheService != null)
                    {
                        entry.IsVideoCached = _videoCacheService.IsVideoCached(entry.SongId);
                        if (entry.IsVideoCached)
                        {
                            string videoPath = Path.Combine(_settingsService.Settings.VideoCachePath, $"{entry.SongId}.mp4");
                            try
                            {
                                using var libVLC = new LibVLC();
                                using var media = new Media(libVLC, new Uri(videoPath));
                                media.Parse().GetAwaiter().GetResult();
                                entry.VideoLength = TimeSpan.FromMilliseconds(media.Duration).ToString(@"m\:ss");
                                Log.Information("[DJSCREEN] Set VideoLength for SongId={SongId}: {VideoLength}", entry.SongId, entry.VideoLength);
                            }
                            catch (Exception ex)
                            {
                                Log.Error("[DJSCREEN] Failed to get duration for SongId={SongId}: {Message}", entry.SongId, ex.Message);
                                entry.VideoLength = "";
                            }
                        }
                        Log.Information("[DJSCREEN] Checked cache for SongId={SongId}, IsCached={IsCached}, CachePath={CachePath}, VideoLength={VideoLength}",
                            entry.SongId, entry.IsVideoCached, Path.Combine(_settingsService.Settings.VideoCachePath, $"{entry.SongId}.mp4"), entry.VideoLength);
                    }
                    QueueEntries.Add(entry);
                    if (!entry.IsVideoCached && !string.IsNullOrEmpty(entry.YouTubeUrl) && _videoCacheService != null)
                    {
                        _ = Task.Run(async () =>
                        {
                            try
                            {
                                await _videoCacheService.CacheVideoAsync(entry.YouTubeUrl, entry.SongId);
                                await Application.Current.Dispatcher.InvokeAsync(() =>
                                {
                                    entry.IsVideoCached = _videoCacheService.IsVideoCached(entry.SongId);
                                    if (entry.IsVideoCached)
                                    {
                                        try
                                        {
                                            using var libVLC = new LibVLC();
                                            using var media = new Media(libVLC, new Uri(Path.Combine(_settingsService.Settings.VideoCachePath, $"{entry.SongId}.mp4")));
                                            media.Parse().GetAwaiter().GetResult();
                                            entry.VideoLength = TimeSpan.FromMilliseconds(media.Duration).ToString(@"m\:ss");
                                            Log.Information("[DJSCREEN] Set VideoLength for cached SongId={SongId}: {VideoLength}", entry.SongId, entry.VideoLength);
                                        }
                                        catch (Exception ex)
                                        {
                                            Log.Error("[DJSCREEN] Failed to get duration for cached SongId={SongId}: {Message}", entry.SongId, ex.Message);
                                            entry.VideoLength = "";
                                        }
                                    }
                                    Log.Information("[DJSCREEN] Cached video for SongId={SongId}, IsCached={IsCached}", entry.SongId, entry.IsVideoCached);
                                });
                            }
                            catch (Exception ex)
                            {
                                Log.Error("[DJSCREEN] Failed to cache video for SongId={SongId}: {Message}", entry.SongId, ex.Message);
                            }
                        });
                    }
                }
                if (SelectedQueueEntry == null && QueueEntries.Any())
                {
                    SelectedQueueEntry = QueueEntries.First();
                }
                Log.Information("[DJSCREEN] Loaded {Count} queue entries for event {EventId}", QueueEntries.Count, _currentEventId);
            });
        }
        catch (Exception ex)
        {
            Log.Error("[DJSCREEN] Failed to load queue data for event: {EventId}: {Message}", _currentEventId, ex.Message);
            WarningMessage = $"Failed to load queue data: {ex.Message}";
        }
    }
}
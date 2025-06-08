using BNKaraoke.DJ.Models;
using BNKaraoke.DJ.Views;
using CommunityToolkit.Mvvm.Input;
using LibVLCSharp.Shared;
using Serilog;
using System;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using System.Timers;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;

namespace BNKaraoke.DJ.ViewModels
{
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
                    SetWarningMessage($"Failed to show song details: {ex.Message}");
                }
            }
            else
            {
                Log.Information("[DJSCREEN] No queue entry selected for song details");
                SetWarningMessage("Please select a song to view details.");
            }
        }

        [RelayCommand]
        private void StartDrag(object parameter)
        {
            try
            {
                var draggedItem = parameter as QueueEntry;
                Log.Information("[DJSCREEN] StartDrag command invoked for QueueId={QueueId}", draggedItem?.QueueId ?? -1);
                if (draggedItem == null)
                {
                    Log.Error("[DJSCREEN] Drag failed: Dragged item is null");
                    SetWarningMessage("Drag failed: No item selected.");
                    return;
                }

                var listView = Application.Current.Windows.OfType<DJScreen>()
                    .Select(w => w.FindName("QueueListView") as ListView)
                    .FirstOrDefault(lv => lv != null);

                if (listView == null)
                {
                    Log.Error("[DJSCREEN] Drag failed: QueueListView not found");
                    SetWarningMessage("Drag failed: Queue not found.");
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
                SetWarningMessage($"Failed to drag: {ex.Message}");
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
                    SetWarningMessage("Drop failed: No event joined.");
                    return;
                }

                if (e == null)
                {
                    Log.Error("[DJSCREEN] Drop failed: DragEventArgs is null");
                    SetWarningMessage("Drop failed: Invalid drag data.");
                    return;
                }

                Log.Information("[DJSCREEN] Accessing dragged data");
                var draggedItem = e.Data.GetData(typeof(QueueEntry)) as QueueEntry;
                if (draggedItem == null)
                {
                    Log.Warning("[DJSCREEN] Drop failed: Dragged item is null or not a QueueEntry");
                    SetWarningMessage("Drop failed: Invalid dragged item.");
                    return;
                }

                Log.Information("[DJSCREEN] Accessing target element");
                var target = e.OriginalSource as FrameworkElement;
                var targetItem = target?.DataContext as QueueEntry;

                if (targetItem == null)
                {
                    Log.Warning("[DJSCREEN] Drop failed: Target item is null or not a QueueEntry, OriginalSourceType={OriginalSourceType}", e.OriginalSource?.GetType().Name);
                    SetWarningMessage("Drop failed: Invalid target item.");
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
                    SetWarningMessage("Cannot reorder the playing song.");
                    return;
                }

                Log.Information("[DJSCREEN] Calculating indices for queue {QueueId}", draggedItem.QueueId);
                int sourceIndex = QueueEntries.IndexOf(draggedItem);
                int targetIndex = QueueEntries.IndexOf(targetItem);

                if (sourceIndex < 0 || targetIndex < 0)
                {
                    Log.Warning("[DJSCREEN] Drop failed: Invalid source or target index, SourceIndex={SourceIndex}, TargetIndex={TargetIndex}", sourceIndex, targetIndex);
                    SetWarningMessage("Drop failed: Invalid queue indices.");
                    return;
                }

                Log.Information("[DJSCREEN] Reordering queue locally");
                Application.Current.Dispatcher.Invoke(() =>
                {
                    QueueEntries.Move(sourceIndex, targetIndex);
                    for (int i = 0; i < QueueEntries.Count; i++)
                    {
                        QueueEntries[i].Position = i + 1;
                        QueueEntries[i].IsUpNext = i == 0; // Set IsUpNext for first entry
                    }
                    OnPropertyChanged(nameof(QueueEntries));
                });

                var queueIds = QueueEntries.Select(q => q.QueueId.ToString()).ToList();
                Log.Information("[DJSCREEN] Reorder payload: EventId={EventId}, QueueIds={QueueIds}", _currentEventId, string.Join(",", queueIds));

                try
                {
                    await _apiService.ReorderQueueAsync(_currentEventId, queueIds);
                    Log.Information("[DJSCREEN] Queue reordered for event {EventId}, dropped {SourceQueueId} to position {TargetIndex}",
                        _currentEventId, draggedItem.QueueId, targetIndex + 1);

                    await LoadQueueData();
                    await LoadSungCountAsync();
                    Log.Information("[DJSCREEN] Refreshed queue data after reorder for event {EventId}", _currentEventId);
                }
                catch (Exception ex)
                {
                    Log.Error("[DJSCREEN] Failed to persist queue order: {Message}", ex.Message);
                    SetWarningMessage($"Failed to reorder queue: {ex.Message}");
                    await LoadQueueData(); // Revert to server state
                }
            }
            catch (Exception ex)
            {
                Log.Error("[DJSCREEN] Drop failed: {Message}", ex.Message);
                SetWarningMessage($"Failed to reorder queue: {ex.Message}");
            }
        }

        [RelayCommand]
        private void ToggleAutoPlay()
        {
            Log.Information("[DJSCREEN] ToggleAutoPlay command invoked");
            if (_isDisposing) return;
            IsAutoPlayEnabled = !IsAutoPlayEnabled;
            AutoPlayButtonText = IsAutoPlayEnabled ? "Auto Play: ON" : "Auto Play: OFF";
            Log.Information("[DJSCREEN] AutoPlay set to: {State}", IsAutoPlayEnabled);
        }

        [RelayCommand]
        private async Task Skip()
        {
            Log.Information("[DJSCREEN] Skip command invoked");
            if (_isDisposing) return;

            var targetEntry = PlayingQueueEntry ?? SelectedQueueEntry;
            if (targetEntry == null || string.IsNullOrEmpty(_currentEventId))
            {
                Log.Information("[DJSCREEN] Skip failed: No queue entry playing/selected or no event joined, PlayingQueueEntry={Playing}, SelectedQueueEntry={Selected}, EventId={EventId}",
                    PlayingQueueEntry?.QueueId ?? -1, SelectedQueueEntry?.QueueId ?? -1, _currentEventId ?? "null");
                SetWarningMessage("Please select a song and join an event.");
                if (_videoPlayerWindow != null)
                {
                    _videoPlayerWindow.StopVideo();
                    Log.Information("[DJSCREEN] Video playback stopped due to no valid queue entry");
                    IsPlaying = false;
                    IsVideoPaused = false;
                    SliderPosition = 0;
                    CurrentVideoPosition = "--:--";
                    TimeRemainingSeconds = 0;
                    TimeRemaining = "0:00";
                    OnPropertyChanged(nameof(SliderPosition));
                    OnPropertyChanged(nameof(CurrentVideoPosition));
                    OnPropertyChanged(nameof(TimeRemaining));
                    OnPropertyChanged(nameof(TimeRemainingSeconds));
                    if (_updateTimer != null)
                    {
                        _updateTimer.Stop();
                        Log.Information("[DJSCREEN] Stopped update timer due to no valid queue entry");
                    }
                }
                return;
            }

            try
            {
                if (_videoPlayerWindow != null)
                {
                    _videoPlayerWindow.StopVideo();
                    Log.Information("[DJSCREEN] Video playback stopped for QueueId={QueueId}", targetEntry.QueueId);
                }
                targetEntry.WasSkipped = true;
                IsPlaying = false;
                IsVideoPaused = false;
                SliderPosition = 0;
                CurrentVideoPosition = "--:--";
                TimeRemainingSeconds = 0;
                TimeRemaining = "0:00";
                OnPropertyChanged(nameof(SliderPosition));
                OnPropertyChanged(nameof(CurrentVideoPosition));
                OnPropertyChanged(nameof(TimeRemaining));
                OnPropertyChanged(nameof(TimeRemainingSeconds));
                if (_updateTimer != null)
                {
                    _updateTimer.Stop();
                    Log.Information("[DJSCREEN] Stopped update timer for QueueId={QueueId}", targetEntry.QueueId);
                }

                await _apiService.CompleteSongAsync(_currentEventId, targetEntry.QueueId);
                Log.Information("[DJSCREEN] Skip request sent for event {EventId}, queue {QueueId}: {SongTitle}", _currentEventId, targetEntry.QueueId, targetEntry.SongTitle);

                if (PlayingQueueEntry != null)
                {
                    var entry = QueueEntries.FirstOrDefault(q => q.QueueId == PlayingQueueEntry.QueueId);
                    if (entry != null)
                    {
                        int sourceIndex = QueueEntries.IndexOf(entry);
                        if (sourceIndex >= 0)
                        {
                            QueueEntries.Move(sourceIndex, QueueEntries.Count - 1);
                            for (int i = 0; i < QueueEntries.Count; i++)
                            {
                                QueueEntries[i].Position = i + 1;
                                QueueEntries[i].IsUpNext = i == 0; // Set IsUpNext for first entry
                            }
                            var queueIds = QueueEntries.Select(q => q.QueueId.ToString()).ToList();
                            Log.Information("[DJSCREEN] Reordering queue for event {EventId}, QueueIds={QueueIds}", _currentEventId, string.Join(",", queueIds));
                            try
                            {
                                await _apiService.ReorderQueueAsync(_currentEventId, queueIds);
                                Log.Information("[DJSCREEN] Moved skipped song to end: QueueId={QueueId}", entry.QueueId);
                            }
                            catch (Exception reorderEx)
                            {
                                Log.Error("[DJSCREEN] Failed to reorder queue for QueueId={QueueId}: {Message}", entry.QueueId, reorderEx.Message);
                            }
                        }
                    }
                    TotalSongsPlayed++;
                    SungCount++;
                    OnPropertyChanged(nameof(TotalSongsPlayed));
                    OnPropertyChanged(nameof(SungCount));
                    Log.Information("[DJSCREEN] Incremented TotalSongsPlayed: {Count}, SungCount: {SungCount}", TotalSongsPlayed, SungCount);
                    QueueEntries.Remove(PlayingQueueEntry); // Remove from queue
                    PlayingQueueEntry = null;
                    OnPropertyChanged(nameof(PlayingQueueEntry));
                    OnPropertyChanged(nameof(QueueEntries));
                }
                await LoadQueueData();
                await LoadSungCountAsync();
            }
            catch (HttpRequestException ex)
            {
                Log.Error("[DJSCREEN] Failed to skip queue {QueueId}: StatusCode={StatusCode}, Message={Message}", targetEntry.QueueId, ex.StatusCode, ex.Message);
                SetWarningMessage($"Failed to skip: {ex.Message}");
                if (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
                {
                    await LoadQueueData();
                }
            }
            catch (Exception ex)
            {
                Log.Error("[DJSCREEN] Failed to skip queue {QueueId}: {Message}", targetEntry.QueueId, ex.Message);
                SetWarningMessage($"Failed to skip: {ex.Message}");
            }
        }

        [RelayCommand]
        private async Task PlayQueueItem()
        {
            Log.Information("[DJSCREEN] PlayQueueItem command invoked");
            if (_isDisposing) return;
            if (!IsShowActive)
            {
                Log.Information("[DJSCREEN] Play failed: Show not started");
                SetWarningMessage("Please start the show first.");
                return;
            }

            if (QueueEntries.Count == 0)
            {
                Log.Information("[DJSCREEN] Play failed: Queue is empty");
                SetWarningMessage("No songs in the queue.");
                return;
            }

            if (SelectedQueueEntry == null)
            {
                Log.Information("[DJSCREEN] Play failed: No queue entry selected");
                SetWarningMessage("Please select a song to play.");
                return;
            }

            if (!SelectedQueueEntry.IsVideoCached)
            {
                Log.Information("[DJSCREEN] Play failed: Video not cached for SongId={SongId}", SelectedQueueEntry.SongId);
                SetWarningMessage("Video not cached. Please wait for caching to complete.");
                return;
            }

            if (SelectedQueueEntry.IsOnHold && !string.IsNullOrEmpty(SelectedQueueEntry.HoldReason))
            {
                var result = MessageBox.Show($"Singer is on hold ({SelectedQueueEntry.HoldReason}). Play anyway?", "Confirm Play", MessageBoxButton.YesNo, MessageBoxImage.Question);
                if (result == MessageBoxResult.No)
                {
                    Log.Information("[DJSCREEN] PlayQueueItem cancelled by user for held song: QueueId={QueueId}, HoldReason={HoldReason}", SelectedQueueEntry.QueueId, SelectedQueueEntry.HoldReason);
                    return;
                }
                Log.Information("[DJSCREEN] Playing held song: QueueId={QueueId}, HoldReason={HoldReason}", SelectedQueueEntry.QueueId, SelectedQueueEntry.HoldReason);
            }

            if (IsPlaying || (IsVideoPaused && PlayingQueueEntry != null))
            {
                var result = MessageBox.Show($"Stop current song '{PlayingQueueEntry?.SongTitle}' and play '{SelectedQueueEntry.SongTitle}'?", "Confirm Song Change", MessageBoxButton.YesNo, MessageBoxImage.Question);
                if (result == MessageBoxResult.No)
                {
                    Log.Information("[DJSCREEN] PlayQueueItem cancelled by user");
                    return;
                }

                try
                {
                    if (_currentEventId != null && PlayingQueueEntry != null)
                    {
                        if (_videoPlayerWindow != null)
                        {
                            _videoPlayerWindow.StopVideo();
                            _videoPlayerWindow.MediaPlayer?.Stop(); // Ensure video is fully stopped
                            Log.Information("[DJSCREEN] Stopped video for QueueId={QueueId}", PlayingQueueEntry.QueueId);
                        }
                        await _apiService.CompleteSongAsync(_currentEventId, PlayingQueueEntry.QueueId);
                        Log.Information("[DJSCREEN] Stop request sent for event {EventId}, queue {QueueId}: {SongTitle}", _currentEventId, PlayingQueueEntry.QueueId, PlayingQueueEntry.SongTitle);
                        IsPlaying = false;
                        IsVideoPaused = false;
                        SliderPosition = 0;
                        CurrentVideoPosition = "--:--";
                        TimeRemainingSeconds = 0;
                        TimeRemaining = "0:00";
                        OnPropertyChanged(nameof(SliderPosition));
                        OnPropertyChanged(nameof(CurrentVideoPosition));
                        OnPropertyChanged(nameof(TimeRemaining));
                        OnPropertyChanged(nameof(TimeRemainingSeconds));
                        if (_updateTimer != null)
                        {
                            _updateTimer.Stop();
                            Log.Information("[DJSCREEN] Stopped update timer for QueueId={QueueId}", PlayingQueueEntry.QueueId);
                        }
                        TotalSongsPlayed++;
                        SungCount++;
                        OnPropertyChanged(nameof(TotalSongsPlayed));
                        OnPropertyChanged(nameof(SungCount));
                        Log.Information("[DJSCREEN] Incremented TotalSongsPlayed: {Count}, SungCount: {SungCount}", TotalSongsPlayed, SungCount);
                        QueueEntries.Remove(PlayingQueueEntry); // Remove from queue
                        PlayingQueueEntry = null;
                        OnPropertyChanged(nameof(PlayingQueueEntry));
                        OnPropertyChanged(nameof(QueueEntries));
                    }
                }
                catch (Exception ex)
                {
                    Log.Error("[DJSCREEN] Failed to stop queue {QueueId}: {Message}", PlayingQueueEntry?.QueueId ?? -1, ex.Message);
                    SetWarningMessage($"Failed to stop current song: {ex.Message}");
                    return;
                }
            }

            if (_currentEventId != null)
            {
                try
                {
                    if (_videoPlayerWindow == null)
                    {
                        _videoPlayerWindow = new VideoPlayerWindow();
                        _videoPlayerWindow.SongEnded += VideoPlayerWindow_SongEnded;
                        _videoPlayerWindow.Closed += VideoPlayerWindow_SongEnded;
                        Log.Information("[DJSCREEN] Subscribed to SongEnded and Closed events for new VideoPlayerWindow");
                    }
                    string videoPath = System.IO.Path.Combine(_settingsService.Settings.VideoCachePath, $"{SelectedQueueEntry.SongId}.mp4");
                    _isInitialPlayback = true;
                    _videoPlayerWindow.PlayVideo(videoPath);
                    _videoPlayerWindow.Show();
                    IsPlaying = true;
                    IsVideoPaused = false;
                    PlayingQueueEntry = SelectedQueueEntry;
                    OnPropertyChanged(nameof(PlayingQueueEntry));
                    try
                    {
                        await _apiService.PlayAsync(_currentEventId, SelectedQueueEntry.QueueId.ToString());
                        Log.Information("[DJSCREEN] Play request sent for event {EventId}, queue {QueueId}: {SongTitle}", _currentEventId, SelectedQueueEntry.QueueId, SelectedQueueEntry.SongTitle);
                    }
                    catch (Exception apiEx)
                    {
                        Log.Error("[DJSCREEN] Failed to send play request for queue {QueueId}: {Message}", SelectedQueueEntry.QueueId, apiEx.Message);
                        SetWarningMessage($"Failed to play API: {apiEx.Message}");
                    }
                    if (TimeSpan.TryParseExact(SelectedQueueEntry.VideoLength, @"m\:ss", null, out var duration))
                    {
                        _totalDuration = duration;
                        SongDuration = duration;
                        OnPropertyChanged(nameof(SongDuration));
                        Log.Information("[DJSCREEN] Set total duration: {Duration}", duration);
                    }
                    if (_countdownTimer == null)
                    {
                        _countdownTimer = new Timer(1000);
                        _countdownTimer.Elapsed += CountdownTimer_Elapsed;
                        _countdownTimer.Start();
                    }
                    if (_updateTimer == null)
                    {
                        _updateTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(1000) }; // Increased interval
                        _updateTimer.Tick += UpdateTimer_Tick;
                        _updateTimer.Start();
                    }
                    QueueEntries.Remove(SelectedQueueEntry); // Remove from queue
                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        for (int i = 0; i < QueueEntries.Count; i++)
                        {
                            QueueEntries[i].IsUpNext = i == 0; // Set IsUpNext for first entry
                        }
                        OnPropertyChanged(nameof(QueueEntries));
                    });
                    await Task.Delay(1000); // Reduced delay
                    _isInitialPlayback = false;
                    await LoadQueueData(); // Refresh queue to update Up Next
                }
                catch (Exception ex)
                {
                    Log.Error("[DJSCREEN] Failed to play queue {QueueId}: {Message}", SelectedQueueEntry.QueueId, ex.Message);
                    SetWarningMessage($"Failed to play: {ex.Message}");
                    _isInitialPlayback = false;
                }
            }
            else
            {
                Log.Information("[DJSCREEN] Play failed: No event joined");
                SetWarningMessage("Please join an event.");
            }
        }

        public async Task PlayNextAutoPlaySong()
        {
            Log.Information("[DJSCREEN] AutoPlay enabled, checking for next song");
            var nextEntry = QueueEntries.OrderBy(q => q.Position)
                .FirstOrDefault(q => q.IsActive && !q.IsOnHold && q.IsVideoCached);
            if (nextEntry != null)
            {
                SelectedQueueEntry = nextEntry;
                OnPropertyChanged(nameof(SelectedQueueEntry));
                try
                {
                    if (_currentEventId != null)
                    {
                        await _apiService.PlayAsync(_currentEventId, nextEntry.QueueId.ToString());
                        Log.Information("[DJSCREEN] Auto-playing next song for event {EventId}, queue {QueueId}: {SongTitle}", _currentEventId, nextEntry.QueueId, nextEntry.SongTitle);
                    }
                }
                catch (Exception apiEx)
                {
                    Log.Error("[DJSCREEN] Failed to send play request for auto-play queue {QueueId}: {Message}", nextEntry.QueueId, apiEx.Message);
                    SetWarningMessage($"Failed to auto-play API: {apiEx.Message}");
                }
                string videoPath = System.IO.Path.Combine(_settingsService.Settings.VideoCachePath, $"{nextEntry.SongId}.mp4");
                if (_videoPlayerWindow == null)
                {
                    _videoPlayerWindow = new VideoPlayerWindow();
                    _videoPlayerWindow.SongEnded += VideoPlayerWindow_SongEnded;
                    _videoPlayerWindow.Closed += VideoPlayerWindow_Closed;
                    Log.Information("[DJSCREEN] Subscribed to SongEnded and Closed events for auto-play");
                }
                _isInitialPlayback = true;
                _videoPlayerWindow.PlayVideo(videoPath);
                _videoPlayerWindow.Show();
                IsPlaying = true;
                PlayingQueueEntry = nextEntry;
                OnPropertyChanged(nameof(PlayingQueueEntry));
                if (TimeSpan.TryParseExact(nextEntry.VideoLength, @"m\:ss", null, out var duration))
                {
                    _totalDuration = duration;
                    SongDuration = duration;
                    OnPropertyChanged(nameof(SongDuration));
                    Log.Information("[DJSCREEN] Set total duration: {Duration}", duration);
                }
                if (_countdownTimer == null)
                {
                    _countdownTimer = new Timer(1000);
                    _countdownTimer.Elapsed += CountdownTimer_Elapsed;
                    _countdownTimer.Start();
                }
                if (_updateTimer == null)
                {
                    _updateTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(1000) }; // Increased interval
                    _updateTimer.Tick += UpdateTimer_Tick;
                    _updateTimer.Start();
                }
                QueueEntries.Remove(nextEntry); // Remove from queue
                Application.Current.Dispatcher.Invoke(() =>
                {
                    for (int i = 0; i < QueueEntries.Count; i++)
                    {
                        QueueEntries[i].IsUpNext = i == 0; // Set IsUpNext for first entry
                    }
                    OnPropertyChanged(nameof(QueueEntries));
                });
                await Task.Delay(1000); // Reduced delay
                _isInitialPlayback = false;
                await LoadQueueData(); // Refresh queue to update Up Next
            }
            else
            {
                Log.Information("[DJSCREEN] No valid next song to auto-play");
                IsPlaying = false;
                PlayingQueueEntry = null;
                OnPropertyChanged(nameof(PlayingQueueEntry));
                await LoadQueueData();
                await LoadSungCountAsync();
            }
        }

        public async Task LoadQueueData()
        {
            if (string.IsNullOrEmpty(_currentEventId)) return;
            try
            {
                Log.Information("[DJSCREEN] Loading queue data for event: {EventId}", _currentEventId);
                var queueEntries = await _apiService.GetQueueAsync(_currentEventId);
                Log.Information("[DJSCREEN] API returned {Count} queue entries for event {EventId}, RawJSON={RawJSON}",
                    queueEntries.Count, _currentEventId, System.Text.Json.JsonSerializer.Serialize(queueEntries));
                await Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    QueueEntries.Clear();
                    for (int i = 0; i < queueEntries.Count; i++)
                    {
                        var entry = queueEntries.OrderBy(q => q.Position).ElementAt(i);
                        entry.IsUpNext = i == 0; // Set IsUpNext for first entry
                        // Map API fields to QueueEntry
                        entry.RequestorDisplayName = entry.RequestorDisplayName; // Use API-provided name
                        if (entry.Singers != null && entry.Singers.Any())
                        {
                            entry.Singers = entry.Singers.ToList(); // Map Singers array
                        }
                        if (_videoCacheService != null)
                        {
                            entry.IsVideoCached = _videoCacheService.IsVideoCached(entry.SongId);
                            if (entry.IsVideoCached)
                            {
                                string videoPath = System.IO.Path.Combine(_settingsService.Settings.VideoCachePath, $"{entry.SongId}.mp4");
                                _ = Task.Run(async () =>
                                {
                                    try
                                    {
                                        using var libVLC = new LibVLC();
                                        using var media = new Media(libVLC, new Uri(videoPath));
                                        await media.Parse();
                                        await Application.Current.Dispatcher.InvokeAsync(() =>
                                        {
                                            entry.VideoLength = TimeSpan.FromMilliseconds(media.Duration).ToString(@"m\:ss");
                                            Log.Information("[DJSCREEN] Set VideoLength for SongId={SongId}: {VideoLength}", entry.SongId, entry.VideoLength);
                                        });
                                    }
                                    catch (Exception ex)
                                    {
                                        Log.Error("[DJSCREEN] Failed to get duration for SongId={SongId}: {Message}", entry.SongId, ex.Message);
                                        Application.Current.Dispatcher.Invoke(() => entry.VideoLength = "");
                                    }
                                });
                            }
                            Log.Information("[DJSCREEN] Queue entry: SongId={SongId}, IsCached={IsCached}, CachePath={CachePath}, SongTitle={SongTitle}, RequestorDisplayName={RequestorDisplayName}, Singers={Singers}, VideoLength={VideoLength}, IsUpNext={IsUpNext}, IsOnHold={IsOnHold}, HoldReason={HoldReason}",
                                entry.SongId, entry.IsVideoCached, System.IO.Path.Combine(_settingsService.Settings.VideoCachePath, $"{entry.SongId}.mp4"),
                                entry.SongTitle ?? "null", entry.RequestorDisplayName ?? "null",
                                entry.Singers != null ? string.Join(",", entry.Singers) : "null",
                                entry.VideoLength, entry.IsUpNext, entry.IsOnHold, entry.HoldReason ?? "null");
                        }
                        QueueEntries.Add(entry);
                    }
                    if (SelectedQueueEntry == null && QueueEntries.Any())
                    {
                        SelectedQueueEntry = QueueEntries.First();
                        OnPropertyChanged(nameof(SelectedQueueEntry));
                    }
                    OnPropertyChanged(nameof(QueueEntries));
                    Log.Information("[DJSCREEN] Loaded {Count} queue entries for event {EventId}", QueueEntries.Count, _currentEventId);
                });
            }
            catch (Exception ex)
            {
                Log.Error("[DJSCREEN] Failed to load queue for EventId={EventId}: {Message}", _currentEventId, ex.Message);
                SetWarningMessage($"Failed to load queue: {ex.Message}");
            }
        }
    }
}
using BNKaraoke.DJ.Views;
using CommunityToolkit.Mvvm.Input;
using LibVLCSharp.Shared;
using Serilog;
using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Timers;
using System.Windows;

namespace BNKaraoke.DJ.ViewModels;

public partial class DJScreenViewModel
{
    private Timer? _warningTimer;
    private Timer? _countdownTimer;
    private bool _isDisposing;
    private TimeSpan? _totalDuration;
    private bool _countdownStarted;

    public void SetWarningMessage(string message)
    {
        if (_isDisposing) return;
        try
        {
            WarningMessage = message;
            WarningExpirationTime = DateTime.Now.AddSeconds(30);
            if (_warningTimer == null)
            {
                _warningTimer = new Timer(1000);
                _warningTimer.Elapsed += WarningTimer_Elapsed;
                _warningTimer.Start();
            }
        }
        catch (Exception ex)
        {
            Log.Error("[DJSCREEN] Failed to set warning message: {Message}", ex.Message);
        }
    }

    private void WarningTimer_Elapsed(object? sender, ElapsedEventArgs e)
    {
        if (_isDisposing) return;
        try
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                if (WarningExpirationTime == null || DateTime.Now >= WarningExpirationTime)
                {
                    WarningMessage = "";
                    WarningExpirationTime = null;
                    if (_warningTimer != null)
                    {
                        _warningTimer.Stop();
                        _warningTimer.Dispose();
                        _warningTimer = null;
                    }
                }
                OnPropertyChanged(nameof(WarningExpirationTime));
            });
        }
        catch (Exception ex)
        {
            Log.Error("[DJSCREEN] Failed to process warning timer: {Message}", ex.Message);
        }
    }

    private void CountdownTimer_Elapsed(object? sender, ElapsedEventArgs e)
    {
        if (_isDisposing) return;
        try
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                if (IsPlaying && _totalDuration.HasValue && _videoPlayerWindow?.MediaPlayer != null)
                {
                    var currentTime = TimeSpan.FromMilliseconds(_videoPlayerWindow.MediaPlayer.Time);
                    var remaining = _totalDuration.Value - currentTime;
                    var seconds = (int)Math.Max(0, remaining.TotalSeconds);
                    TimeRemainingSeconds = seconds;
                    TimeRemaining = TimeSpan.FromSeconds(seconds).ToString(@"m\:ss");
                    if (!_countdownStarted)
                    {
                        Log.Information("[DJSCREEN] Countdown started: {TimeRemaining}", TimeRemaining);
                        _countdownStarted = true;
                    }
                    CurrentVideoPosition = currentTime.ToString(@"m\:ss");
                    OnPropertyChanged(nameof(CurrentVideoPosition));
                    Log.Information("[DJSCREEN] Updated CurrentVideoPosition: {Position}", CurrentVideoPosition);
                    if (seconds == 0)
                    {
                        Log.Information("[DJSCREEN] Countdown ended");
                        _countdownStarted = false;
                    }
                    OnPropertyChanged(nameof(TimeRemaining));
                    OnPropertyChanged(nameof(TimeRemainingSeconds));
                }
                else
                {
                    TimeRemainingSeconds = 0;
                    TimeRemaining = "0:00";
                    CurrentVideoPosition = "--:--";
                    OnPropertyChanged(nameof(CurrentVideoPosition));
                    _countdownStarted = false;
                    OnPropertyChanged(nameof(TimeRemaining));
                    OnPropertyChanged(nameof(TimeRemainingSeconds));
                }
            });
        }
        catch (Exception ex)
        {
            Log.Error("[DJSCREEN] Failed to process countdown timer: {Message}", ex.Message);
        }
    }

    [RelayCommand]
    private async Task Play()
    {
        Log.Information("[DJSCREEN] Play/Pause command invoked");
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

        var targetEntry = SelectedQueueEntry ?? QueueEntries.FirstOrDefault();
        if (targetEntry == null)
        {
            Log.Information("[DJSCREEN] Play failed: No queue entry selected");
            SetWarningMessage("Please select a song to play.");
            return;
        }

        if (!targetEntry.IsVideoCached)
        {
            Log.Information("[DJSCREEN] Play failed: Video not cached for SongId={SongId}", targetEntry.SongId);
            SetWarningMessage("Video not cached. Please wait for caching to complete.");
            return;
        }

        if (!string.IsNullOrEmpty(_currentEventId))
        {
            try
            {
                if (IsPlaying && _videoPlayerWindow?.MediaPlayer != null)
                {
                    _videoPlayerWindow.MediaPlayer.Pause();
                    IsVideoPaused = true;
                    IsPlaying = false;
                    await _apiService.PauseAsync(_currentEventId, targetEntry.QueueId.ToString());
                    Log.Information("[DJSCREEN] Pause request sent for event {EventId}, queue {QueueId}: {SongTitle}", _currentEventId, targetEntry.QueueId, targetEntry.SongTitle);
                }
                else
                {
                    if (_videoPlayerWindow == null)
                    {
                        _videoPlayerWindow = new VideoPlayerWindow();
                        _videoPlayerWindow.SongEnded += VideoPlayerWindow_SongEnded;
                        _videoPlayerWindow.TimeChanged += VideoPlayerWindow_TimeChanged;
                        _videoPlayerWindow.Closed += VideoPlayerWindow_Closed;
                        Log.Information("[DJSCREEN] Created new VideoPlayerWindow for playback");
                    }
                    string videoPath = Path.Combine(_settingsService.Settings.VideoCachePath, $"{targetEntry.SongId}.mp4");
                    if (IsVideoPaused && _videoPlayerWindow.MediaPlayer != null)
                    {
                        _videoPlayerWindow.MediaPlayer.Play();
                        IsVideoPaused = false;
                    }
                    else
                    {
                        _videoPlayerWindow.PlayVideo(videoPath);
                        if (TimeSpan.TryParseExact(targetEntry.VideoLength, @"m\:ss", null, out var duration))
                        {
                            _totalDuration = duration;
                            Log.Information("[DJSCREEN] Set total duration: {Duration}", duration);
                        }
                    }
                    _videoPlayerWindow.Show();
                    IsPlaying = true;
                    PlayingQueueEntry = targetEntry;
                    SelectedQueueEntry = targetEntry;
                    await _apiService.PlayAsync(_currentEventId, targetEntry.QueueId.ToString());
                    Log.Information("[DJSCREEN] Play request sent for event {EventId}, queue {QueueId}: {SongTitle}", _currentEventId, targetEntry.QueueId, targetEntry.SongTitle);
                    if (_countdownTimer == null)
                    {
                        _countdownTimer = new Timer(1000);
                        _countdownTimer.Elapsed += CountdownTimer_Elapsed;
                        _countdownTimer.Start();
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Error("[DJSCREEN] Failed to {Action} queue {QueueId}: {Message}", IsPlaying ? "pause" : "play", targetEntry.QueueId, ex.Message);
                SetWarningMessage($"Failed to {(IsPlaying ? "pause" : "play")}: {ex.Message}");
            }
        }
        else
        {
            Log.Information("[DJSCREEN] Play/Pause failed: No event joined");
            SetWarningMessage("Please join an event.");
        }
    }

    [RelayCommand]
    private async Task Stop()
    {
        Log.Information("[DJSCREEN] Stop command invoked");
        if (_isDisposing) return;
        if (SelectedQueueEntry != null && !string.IsNullOrEmpty(_currentEventId))
        {
            try
            {
                await _apiService.StopAsync(_currentEventId, SelectedQueueEntry.QueueId.ToString());
                Log.Information("[DJSCREEN] Stop request sent for event {EventId}, queue {QueueId}: {SongTitle}", _currentEventId, SelectedQueueEntry.QueueId, SelectedQueueEntry.SongTitle);
                if (_videoPlayerWindow != null)
                {
                    _videoPlayerWindow.StopVideo();
                    _videoPlayerWindow.Close();
                    _videoPlayerWindow = null;
                }
                IsPlaying = false;
                IsVideoPaused = false;
                CurrentVideoPosition = "--:--";
                TimeRemainingSeconds = 0;
                TimeRemaining = "0:00";

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
                            }
                            var queueIds = QueueEntries.Select(q => q.QueueId.ToString()).ToList();
                            Log.Information("[DJSCREEN] Reordering queue for event {EventId}, QueueIds={QueueIds}", _currentEventId, string.Join(",", queueIds));
                            try
                            {
                                await _apiService.ReorderQueueAsync(_currentEventId, queueIds);
                                Log.Information("[DJSCREEN] Moved stopped song to end: QueueId={QueueId}", entry.QueueId);
                            }
                            catch (Exception reorderEx)
                            {
                                Log.Error("[DJSCREEN] Failed to reorder queue for QueueId={QueueId}: {Message}", entry.QueueId, reorderEx.Message);
                            }
                        }
                    }
                    TotalSongsPlayed++;
                    OnPropertyChanged(nameof(TotalSongsPlayed));
                    Log.Information("[DJSCREEN] Incremented TotalSongsPlayed: {Count}", TotalSongsPlayed);
                    PlayingQueueEntry = null;
                }
            }
            catch (Exception ex)
            {
                Log.Error("[DJSCREEN] Failed to stop queue {QueueId}: {Message}", SelectedQueueEntry.QueueId, ex.Message);
                SetWarningMessage($"Failed to stop: {ex.Message}");
            }
        }
        else
        {
            Log.Information("[DJSCREEN] Stop failed: No queue entry selected or no event joined");
            SetWarningMessage("Please select a song and join an event.");
        }
    }

    [RelayCommand]
    private async Task Skip()
    {
        Log.Information("[DJSCREEN] Skip command invoked");
        if (_isDisposing) return;
        if (SelectedQueueEntry != null && !string.IsNullOrEmpty(_currentEventId))
        {
            try
            {
                await _apiService.SkipAsync(_currentEventId, SelectedQueueEntry.QueueId.ToString());
                Log.Information("[DJSCREEN] Skip request sent for event {EventId}, queue {QueueId}: {SongTitle}", _currentEventId, SelectedQueueEntry.QueueId, SelectedQueueEntry.SongTitle);
                if (_videoPlayerWindow != null)
                {
                    _videoPlayerWindow.StopVideo();
                    _videoPlayerWindow.Close();
                    _videoPlayerWindow = null;
                }
                IsPlaying = false;
                IsVideoPaused = false;
                CurrentVideoPosition = "--:--";
                TimeRemainingSeconds = 0;
                TimeRemaining = "0:00";

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
                    OnPropertyChanged(nameof(TotalSongsPlayed));
                    Log.Information("[DJSCREEN] Incremented TotalSongsPlayed: {Count}", TotalSongsPlayed);
                    PlayingQueueEntry = null;
                }
                await LoadQueueData();
            }
            catch (Exception ex)
            {
                Log.Error("[DJSCREEN] Failed to skip queue {QueueId}: {Message}", SelectedQueueEntry.QueueId, ex.Message);
                SetWarningMessage($"Failed to skip: {ex.Message}");
            }
        }
        else
        {
            Log.Information("[DJSCREEN] Skip failed: No queue entry selected or no event joined");
            SetWarningMessage("Please select a song and join an event.");
        }
    }

    [RelayCommand]
    private void ToggleShow()
    {
        Log.Information("[DJSCREEN] ToggleShow command invoked");
        if (_isDisposing) return;
        if (!IsShowActive)
        {
            try
            {
                Log.Information("[DJSCREEN] Starting show");
                _videoPlayerWindow = new VideoPlayerWindow();
                _videoPlayerWindow.SongEnded += VideoPlayerWindow_SongEnded;
                _videoPlayerWindow.TimeChanged += VideoPlayerWindow_TimeChanged;
                _videoPlayerWindow.Closed += VideoPlayerWindow_Closed;
                Log.Information("[DJSCREEN] Subscribed to SongEnded, TimeChanged, and Closed events for VideoPlayerWindow");
                _videoPlayerWindow.Show();
                IsShowActive = true;
                ShowButtonText = "End Show";
                ShowButtonColor = "#FF0000";
                Log.Information("[DJSCREEN] Show started, VideoPlayerWindow shown with idle title");
            }
            catch (Exception ex)
            {
                Log.Error("[DJSCREEN] Failed to start show: {Message}", ex.Message);
                SetWarningMessage($"Failed to start show: {ex.Message}");
                if (_videoPlayerWindow != null)
                {
                    _videoPlayerWindow.Close();
                    _videoPlayerWindow = null;
                }
            }
        }
        else
        {
            var result = MessageBox.Show("Are you sure you want to end the show?", "Confirm End Show", MessageBoxButton.YesNo, MessageBoxImage.Question);
            if (result == MessageBoxResult.Yes)
            {
                try
                {
                    Log.Information("[DJSCREEN] Ending show");
                    if (_videoPlayerWindow != null)
                    {
                        _videoPlayerWindow.Close();
                        _videoPlayerWindow = null;
                    }
                    IsShowActive = false;
                    ShowButtonText = "Start Show";
                    ShowButtonColor = "#22d3ee";
                    if (IsPlaying)
                    {
                        IsPlaying = false;
                        IsVideoPaused = false;
                        CurrentVideoPosition = "--:--";
                        TimeRemainingSeconds = 0;
                        TimeRemaining = "0:00";
                        Log.Information("[DJSCREEN] Playback stopped due to show ending");
                        PlayingQueueEntry = null;
                    }
                    Log.Information("[DJSCREEN] Show ended, VideoPlayerWindow closed");
                }
                catch (Exception ex)
                {
                    Log.Error("[DJSCREEN] Failed to end show: {Message}", ex.Message);
                    SetWarningMessage($"Failed to end show: {ex.Message}");
                }
            }
            else
            {
                Log.Information("[DJSCREEN] End show cancelled by user");
            }
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

        if (IsPlaying && PlayingQueueEntry != SelectedQueueEntry)
        {
            var result = MessageBox.Show($"Stop current song '{PlayingQueueEntry?.SongTitle}' and play '{SelectedQueueEntry.SongTitle}'?", "Confirm Song Change", MessageBoxButton.YesNo, MessageBoxImage.Question);
            if (result != MessageBoxResult.Yes)
            {
                Log.Information("[DJSCREEN] PlayQueueItem cancelled by user");
                return;
            }

            try
            {
                if (_currentEventId != null && PlayingQueueEntry != null)
                {
                    await _apiService.StopAsync(_currentEventId, PlayingQueueEntry.QueueId.ToString());
                    Log.Information("[DJSCREEN] Stop request sent for event {EventId}, queue {QueueId}: {SongTitle}", _currentEventId, PlayingQueueEntry.QueueId, PlayingQueueEntry.SongTitle);
                    if (_videoPlayerWindow != null)
                    {
                        _videoPlayerWindow.StopVideo();
                        _videoPlayerWindow.Close();
                        _videoPlayerWindow = null;
                    }
                    IsPlaying = false;
                    IsVideoPaused = false;
                    CurrentVideoPosition = "--:--";
                    TimeRemainingSeconds = 0;
                    TimeRemaining = "0:00";
                    TotalSongsPlayed++;
                    OnPropertyChanged(nameof(TotalSongsPlayed));
                    Log.Information("[DJSCREEN] Incremented TotalSongsPlayed: {Count}", TotalSongsPlayed);
                    PlayingQueueEntry = null;
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
                    _videoPlayerWindow.TimeChanged += VideoPlayerWindow_TimeChanged;
                    _videoPlayerWindow.Closed += VideoPlayerWindow_Closed;
                    Log.Information("[DJSCREEN] Subscribed to SongEnded, TimeChanged, and Closed events for new VideoPlayerWindow");
                }
                string videoPath = Path.Combine(_settingsService.Settings.VideoCachePath, $"{SelectedQueueEntry.SongId}.mp4");
                _videoPlayerWindow.PlayVideo(videoPath);
                _videoPlayerWindow.Show();
                IsPlaying = true;
                IsVideoPaused = false;
                PlayingQueueEntry = SelectedQueueEntry;
                await _apiService.PlayAsync(_currentEventId, SelectedQueueEntry.QueueId.ToString());
                Log.Information("[DJSCREEN] Play request sent for event {EventId}, queue {QueueId}: {SongTitle}", _currentEventId, SelectedQueueEntry.QueueId, SelectedQueueEntry.SongTitle);
                if (TimeSpan.TryParseExact(SelectedQueueEntry.VideoLength, @"m\:ss", null, out var duration))
                {
                    _totalDuration = duration;
                    Log.Information("[DJSCREEN] Set total duration: {Duration}", duration);
                }
                if (_countdownTimer == null)
                {
                    _countdownTimer = new Timer(1000);
                    _countdownTimer.Elapsed += CountdownTimer_Elapsed;
                    _countdownTimer.Start();
                }
            }
            catch (Exception ex)
            {
                Log.Error("[DJSCREEN] Failed to play queue {QueueId}: {Message}", SelectedQueueEntry.QueueId, ex.Message);
                SetWarningMessage($"Failed to play: {ex.Message}");
            }
        }
        else
        {
            Log.Information("[DJSCREEN] Play failed: No event joined");
            SetWarningMessage("Please join an event.");
        }
    }

    [RelayCommand]
    private void ToggleAutoPlay()
    {
        Log.Information("[DJSCREEN] ToggleAutoPlay command invoked");
        if (_isDisposing) return;
        IsAutoPlayEnabled = !IsAutoPlayEnabled;
        AutoPlayButtonText = IsAutoPlayEnabled ? "Auto Play is ON" : "Auto Play is OFF";
        Log.Information("[DJSCREEN] AutoPlay set to: {State}", IsAutoPlayEnabled);
    }

    public async Task HandleSongEnded()
    {
        Log.Information("[DJSCREEN] Handling song ended");
        if (_isDisposing) return;
        try
        {
            if (PlayingQueueEntry != null)
            {
                IsPlaying = false;
                IsVideoPaused = false;
                CurrentVideoPosition = "--:--";
                TimeRemainingSeconds = 0;
                TimeRemaining = "0:00";
                TotalSongsPlayed++;
                OnPropertyChanged(nameof(TotalSongsPlayed));
                Log.Information("[DJSCREEN] Incremented TotalSongsPlayed: {Count}", TotalSongsPlayed);
                PlayingQueueEntry = null;
            }

            if (_videoPlayerWindow != null)
            {
                _videoPlayerWindow.StopVideo();
                _videoPlayerWindow.Close();
                _videoPlayerWindow = null;
            }

            if (IsAutoPlayEnabled && !string.IsNullOrEmpty(_currentEventId))
            {
                var nextEntry = QueueEntries.OrderBy(q => q.Position).FirstOrDefault();
                if (nextEntry != null && nextEntry.IsVideoCached)
                {
                    SelectedQueueEntry = nextEntry;
                    await _apiService.PlayAsync(_currentEventId, nextEntry.QueueId.ToString());
                    Log.Information("[DJSCREEN] Auto-playing next song for event {EventId}, queue {QueueId}: {SongTitle}", _currentEventId, nextEntry.QueueId, nextEntry.SongTitle);
                    string videoPath = Path.Combine(_settingsService.Settings.VideoCachePath, $"{nextEntry.SongId}.mp4");
                    if (_videoPlayerWindow == null)
                    {
                        _videoPlayerWindow = new VideoPlayerWindow();
                        _videoPlayerWindow.SongEnded += VideoPlayerWindow_SongEnded;
                        _videoPlayerWindow.TimeChanged += VideoPlayerWindow_TimeChanged;
                        _videoPlayerWindow.Closed += VideoPlayerWindow_Closed;
                        Log.Information("[DJSCREEN] Subscribed to SongEnded, TimeChanged, and Closed events for auto-play");
                    }
                    _videoPlayerWindow.PlayVideo(videoPath);
                    _videoPlayerWindow.Show();
                    IsPlaying = true;
                    PlayingQueueEntry = nextEntry;
                    if (TimeSpan.TryParseExact(nextEntry.VideoLength, @"m\:ss", null, out var duration))
                    {
                        _totalDuration = duration;
                        Log.Information("[DJSCREEN] Set total duration: {Duration}", duration);
                    }
                    if (_countdownTimer == null)
                    {
                        _countdownTimer = new Timer(1000);
                        _countdownTimer.Elapsed += CountdownTimer_Elapsed;
                        _countdownTimer.Start();
                    }
                }
                else
                {
                    Log.Information("[DJSCREEN] No valid next song to auto-play");
                    if (_videoPlayerWindow == null && IsShowActive)
                    {
                        _videoPlayerWindow = new VideoPlayerWindow();
                        _videoPlayerWindow.SongEnded += VideoPlayerWindow_SongEnded;
                        _videoPlayerWindow.TimeChanged += VideoPlayerWindow_TimeChanged;
                        _videoPlayerWindow.Closed += VideoPlayerWindow_Closed;
                        _videoPlayerWindow.Show();
                    }
                }
            }
            else
            {
                Log.Information("[DJSCREEN] AutoPlay is disabled or no event joined");
                if (IsShowActive && _videoPlayerWindow == null)
                {
                    _videoPlayerWindow = new VideoPlayerWindow();
                    _videoPlayerWindow.SongEnded += VideoPlayerWindow_SongEnded;
                    _videoPlayerWindow.TimeChanged += VideoPlayerWindow_TimeChanged;
                    _videoPlayerWindow.Closed += VideoPlayerWindow_Closed;
                    _videoPlayerWindow.Show();
                }
            }
        }
        catch (Exception ex)
        {
            Log.Error("[DJSCREEN] Failed to handle song ended: {Message}", ex.Message);
            SetWarningMessage($"Failed to handle song end: {ex.Message}");
        }
    }

    private void VideoPlayerWindow_SongEnded(object? sender, EventArgs e)
    {
        Log.Information("[DJSCREEN] SongEnded event received");
        if (_isDisposing) return;
        try
        {
            Application.Current.Dispatcher.InvokeAsync(async () =>
            {
                await HandleSongEnded();
            }).Wait();
        }
        catch (Exception ex)
        {
            Log.Error("[DJSCREEN] Failed to process SongEnded event: {Message}, StackTrace={StackTrace}", ex.Message, ex.StackTrace);
        }
    }

    private void VideoPlayerWindow_TimeChanged(object? sender, MediaPlayerTimeChangedEventArgs e)
    {
        if (_isDisposing) return;
        try
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                CurrentVideoPosition = TimeSpan.FromMilliseconds(e.Time).ToString(@"m\:ss");
                OnPropertyChanged(nameof(CurrentVideoPosition));
            });
        }
        catch (Exception ex)
        {
            Log.Error("[DJSCREEN] Failed to process TimeChanged event: {Message}", ex.Message);
        }
    }

    private void VideoPlayerWindow_Closed(object? sender, EventArgs e)
    {
        Log.Information("[DJSCREEN] VideoPlayerWindow closed");
        if (_isDisposing) return;
        try
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                if (_videoPlayerWindow != null)
                {
                    _videoPlayerWindow.SongEnded -= VideoPlayerWindow_SongEnded;
                    _videoPlayerWindow.TimeChanged -= VideoPlayerWindow_TimeChanged;
                    _videoPlayerWindow.Closed -= VideoPlayerWindow_Closed;
                    _videoPlayerWindow = null;
                }
                IsShowActive = false;
                ShowButtonText = "Start Show";
                ShowButtonColor = "#22d3ee";
                if (IsPlaying)
                {
                    IsPlaying = false;
                    IsVideoPaused = false;
                    CurrentVideoPosition = "--:--";
                    TimeRemainingSeconds = 0;
                    TimeRemaining = "0:00";
                    PlayingQueueEntry = null;
                }
                Log.Information("[DJSCREEN] Show state reset due to VideoPlayerWindow close");
            });
        }
        catch (Exception ex)
        {
            Log.Error("[DJSCREEN] Failed to process VideoPlayerWindow close: {Message}", ex.Message);
        }
    }

    public void Dispose()
    {
        _isDisposing = true;
        try
        {
            if (_warningTimer != null)
            {
                _warningTimer.Stop();
                _warningTimer.Dispose();
                _warningTimer = null;
            }
            if (_countdownTimer != null)
            {
                _countdownTimer.Stop();
                _countdownTimer.Dispose();
                _countdownTimer = null;
            }
            if (_videoPlayerWindow != null)
            {
                _videoPlayerWindow.SongEnded -= VideoPlayerWindow_SongEnded;
                _videoPlayerWindow.TimeChanged -= VideoPlayerWindow_TimeChanged;
                _videoPlayerWindow.Closed -= VideoPlayerWindow_Closed;
                _videoPlayerWindow.Close();
                _videoPlayerWindow = null;
            }
        }
        catch (Exception ex)
        {
            Log.Error("[DJSCREEN] Failed to dispose resources: {Message}", ex.Message);
        }
    }
}
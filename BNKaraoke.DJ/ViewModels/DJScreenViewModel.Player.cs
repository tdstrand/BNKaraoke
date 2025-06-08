using BNKaraoke.DJ.Models;
using BNKaraoke.DJ.Views;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LibVLCSharp.Shared;
using Serilog;
using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Timers;
using System.Windows;
using System.Windows.Threading;

namespace BNKaraoke.DJ.ViewModels
{
    public partial class DJScreenViewModel
    {
        private Timer? _warningTimer;
        private Timer? _countdownTimer;
        private DispatcherTimer? _updateTimer;
        private bool _isDisposing;
        private TimeSpan? _totalDuration;
        private bool _countdownStarted;
        private bool _isSeeking;
        private bool _isInitialPlayback;
        private bool _wasPlaying;

        [ObservableProperty]
        private double _sliderPosition;

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
                    if ((IsPlaying || IsVideoPaused) && _totalDuration.HasValue && _videoPlayerWindow?.MediaPlayer != null)
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
                        OnPropertyChanged(nameof(TimeRemaining));
                        OnPropertyChanged(nameof(TimeRemainingSeconds));
                        if (seconds == 0)
                        {
                            Log.Information("[DJSCREEN] Countdown ended");
                            _countdownStarted = false;
                        }
                    }
                    else
                    {
                        TimeRemainingSeconds = 0;
                        TimeRemaining = "0:00";
                        CurrentVideoPosition = "--:--";
                        SliderPosition = 0;
                        OnPropertyChanged(nameof(SliderPosition));
                        OnPropertyChanged(nameof(CurrentVideoPosition));
                        OnPropertyChanged(nameof(TimeRemaining));
                        OnPropertyChanged(nameof(TimeRemainingSeconds));
                        _countdownStarted = false;
                    }
                });
            }
            catch (Exception ex)
            {
                Log.Error("[DJSCREEN] Failed to process countdown timer: {Message}", ex.Message);
            }
        }

        private void UpdateTimer_Tick(object? sender, EventArgs e)
        {
            if (_isDisposing || _isSeeking || _isInitialPlayback || _videoPlayerWindow?.MediaPlayer == null || !IsPlaying || _videoPlayerWindow.MediaPlayer.State == VLCState.Stopped) return;
            try
            {
                Application.Current.Dispatcher.Invoke(() =>
                {
                    if (_videoPlayerWindow.MediaPlayer != null)
                    {
                        var currentTime = TimeSpan.FromMilliseconds(_videoPlayerWindow.MediaPlayer.Time);
                        CurrentVideoPosition = currentTime.ToString(@"m\:ss");
                        if (!_isSeeking)
                        {
                            SliderPosition = currentTime.TotalSeconds;
                            OnPropertyChanged(nameof(SliderPosition));
                        }
                        OnPropertyChanged(nameof(CurrentVideoPosition));
                    }
                });
            }
            catch (Exception ex)
            {
                Log.Error("[DJSCREEN] Failed to update position: {Message}", ex.Message);
            }
        }

        [RelayCommand]
        private void StartSeeking()
        {
            if (_isDisposing || _videoPlayerWindow?.MediaPlayer == null) return;
            try
            {
                _isSeeking = true;
                _wasPlaying = _videoPlayerWindow.MediaPlayer.IsPlaying;
                if (_wasPlaying)
                {
                    _videoPlayerWindow.MediaPlayer.Pause();
                    Log.Information("[DJSCREEN] Paused video for seeking");
                }
                Log.Information("[DJSCREEN] Started seeking");
            }
            catch (Exception ex)
            {
                Log.Error("[DJSCREEN] Failed to start seeking: {Message}", ex.Message);
            }
        }

        [RelayCommand]
        private void StopSeeking()
        {
            if (_isDisposing || _videoPlayerWindow?.MediaPlayer == null) return;
            try
            {
                _isSeeking = false;
                if (_wasPlaying)
                {
                    _videoPlayerWindow.MediaPlayer.Play();
                    Log.Information("[DJSCREEN] Resumed video after seeking");
                }
                Log.Information("[DJSCREEN] Stopped seeking");
            }
            catch (Exception ex)
            {
                Log.Error("[DJSCREEN] Failed to stop seeking: {Message}", ex.Message);
            }
        }

        [RelayCommand]
        private void SeekSong(double position)
        {
            if (_isDisposing || _videoPlayerWindow?.MediaPlayer == null || _isInitialPlayback || _videoPlayerWindow.MediaPlayer.State == VLCState.Stopped)
            {
                Log.Information("[DJSCREEN] SeekSong skipped: Disposing={Disposing}, MediaPlayer={MediaPlayer}, InitialPlayback={InitialPlayback}, State={State}",
                    _isDisposing, _videoPlayerWindow?.MediaPlayer != null, _isInitialPlayback, _videoPlayerWindow?.MediaPlayer?.State);
                return;
            }
            try
            {
                var currentTime = _videoPlayerWindow.MediaPlayer.Time / 1000.0;
                if (!_isSeeking && Math.Abs(position - currentTime) < 2.0) // Increased threshold
                {
                    Log.Information("[DJSCREEN] SeekSong skipped: Position={Position} too close to current time={CurrentTime}", position, currentTime);
                    return;
                }
                Log.Information("[DJSCREEN] SeekSong invoked with position: {Position}, IsSeeking={IsSeeking}, MediaState={State}",
                    position, _isSeeking, _videoPlayerWindow.MediaPlayer.State);
                _isSeeking = true;
                _videoPlayerWindow.MediaPlayer.Pause();
                SliderPosition = position;
                CurrentVideoPosition = TimeSpan.FromSeconds(position).ToString(@"m\:ss");
                OnPropertyChanged(nameof(SliderPosition));
                OnPropertyChanged(nameof(CurrentVideoPosition));
                _videoPlayerWindow.MediaPlayer.Time = (long)(position * 1000);
                if (_wasPlaying)
                {
                    _videoPlayerWindow.MediaPlayer.Play();
                }
                _isSeeking = false;
                Log.Information("[DJSCREEN] Seeked to position: {Position}", position);
            }
            catch (Exception ex)
            {
                Log.Error("[DJSCREEN] Failed to seek song: {Message}", ex.Message);
                SetWarningMessage($"Failed to seek song: {ex.Message}");
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

            var targetEntry = PlayingQueueEntry ?? SelectedQueueEntry ?? QueueEntries.FirstOrDefault();
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
                    if (_videoPlayerWindow == null)
                    {
                        _videoPlayerWindow = new VideoPlayerWindow();
                        _videoPlayerWindow.SongEnded += VideoPlayerWindow_SongEnded;
                        _videoPlayerWindow.Closed += VideoPlayerWindow_Closed;
                        Log.Information("[DJSCREEN] Created new VideoPlayerWindow for playback");
                    }

                    // Resume paused song
                    if (IsVideoPaused && PlayingQueueEntry != null)
                    {
                        _videoPlayerWindow.MediaPlayer.Play();
                        IsVideoPaused = false;
                        IsPlaying = true;
                        StopRestartButtonColor = "#22d3ee";
                        OnPropertyChanged(nameof(StopRestartButtonColor));
                        Log.Information("[DJSCREEN] Resumed video for event {EventId}, queue {QueueId}: {SongTitle}", _currentEventId, PlayingQueueEntry.QueueId, PlayingQueueEntry.SongTitle);
                        try
                        {
                            await _apiService.PlayAsync(_currentEventId, PlayingQueueEntry.QueueId.ToString());
                            Log.Information("[DJSCREEN] Play request sent for event {EventId}, queue {QueueId}: {SongTitle}", _currentEventId, PlayingQueueEntry.QueueId, PlayingQueueEntry.SongTitle);
                        }
                        catch (Exception apiEx)
                        {
                            Log.Error("[DJSCREEN] Failed to send play request for queue {QueueId}: {Message}", PlayingQueueEntry.QueueId, apiEx.Message);
                            SetWarningMessage($"Failed to play API: {apiEx.Message}");
                        }
                        if (_updateTimer == null)
                        {
                            _updateTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(1000) };
                            _updateTimer.Tick += UpdateTimer_Tick;
                            _updateTimer.Start();
                        }
                        return;
                    }

                    // Pause playing song
                    if (IsPlaying && _videoPlayerWindow.MediaPlayer != null)
                    {
                        _videoPlayerWindow.PauseVideo();
                        IsVideoPaused = true;
                        IsPlaying = false;
                        StopRestartButtonColor = "#FF0000";
                        OnPropertyChanged(nameof(StopRestartButtonColor));
                        try
                        {
                            await _apiService.PauseAsync(_currentEventId, targetEntry.QueueId.ToString());
                            Log.Information("[DJSCREEN] Pause request sent for event {EventId}, queue {QueueId}: {SongTitle}", _currentEventId, targetEntry.QueueId, targetEntry.SongTitle);
                        }
                        catch (Exception apiEx)
                        {
                            Log.Error("[DJSCREEN] Failed to send pause request for queue {QueueId}: {Message}", targetEntry.QueueId, apiEx.Message);
                            SetWarningMessage($"Failed to pause API: {apiEx.Message}");
                        }
                        return;
                    }

                    // Play new song
                    string videoPath = Path.Combine(_settingsService.Settings.VideoCachePath, $"{targetEntry.SongId}.mp4");
                    _isInitialPlayback = true;
                    _videoPlayerWindow.PlayVideo(videoPath);
                    if (TimeSpan.TryParseExact(targetEntry.VideoLength, @"m\:ss", null, out var duration))
                    {
                        _totalDuration = duration;
                        SongDuration = duration;
                        OnPropertyChanged(nameof(SongDuration));
                        Log.Information("[DJSCREEN] Set total duration: {Duration}", duration);
                    }
                    PlayingQueueEntry = targetEntry;
                    SelectedQueueEntry = targetEntry;
                    OnPropertyChanged(nameof(PlayingQueueEntry));
                    OnPropertyChanged(nameof(SelectedQueueEntry));
                    IsPlaying = true;
                    IsVideoPaused = false;
                    StopRestartButtonColor = "#22d3ee";
                    OnPropertyChanged(nameof(StopRestartButtonColor));
                    try
                    {
                        await _apiService.PlayAsync(_currentEventId, targetEntry.QueueId.ToString());
                        Log.Information("[DJSCREEN] Play request sent for event {EventId}, queue {QueueId}: {SongTitle}", _currentEventId, targetEntry.QueueId, targetEntry.SongTitle);
                    }
                    catch (Exception apiEx)
                    {
                        Log.Error("[DJSCREEN] Failed to send play request for queue {QueueId}: {Message}", targetEntry.QueueId, apiEx.Message);
                        SetWarningMessage($"Failed to play API: {apiEx.Message}");
                    }
                    QueueEntries.Remove(targetEntry);
                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        for (int i = 0; i < QueueEntries.Count; i++)
                        {
                            QueueEntries[i].IsUpNext = i == 0;
                        }
                        OnPropertyChanged(nameof(QueueEntries));
                    });

                    _videoPlayerWindow.Show();
                    if (_countdownTimer == null)
                    {
                        _countdownTimer = new Timer(1000);
                        _countdownTimer.Elapsed += CountdownTimer_Elapsed;
                        _countdownTimer.Start();
                    }
                    if (_updateTimer == null)
                    {
                        _updateTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(1000) };
                        _updateTimer.Tick += UpdateTimer_Tick;
                        _updateTimer.Start();
                    }
                    await Task.Delay(1000); // Reduced delay
                    _isInitialPlayback = false;
                }
                catch (Exception ex)
                {
                    Log.Error("[DJSCREEN] Failed to {Action} queue {QueueId}: {Message}", IsPlaying ? "pause" : "play", targetEntry.QueueId, ex.Message);
                    SetWarningMessage($"Failed to {(IsPlaying ? "pause" : "play")}: {ex.Message}");
                    _isInitialPlayback = false;
                }
            }
            else
            {
                Log.Information("[DJSCREEN] Play/Pause failed: No event joined");
                SetWarningMessage("Please join an event.");
            }
        }

        [RelayCommand]
        private async Task StopRestart()
        {
            Log.Information("[DJSCREEN] StopRestart command invoked");
            if (_isDisposing) return;

            var targetEntry = PlayingQueueEntry ?? SelectedQueueEntry;
            if (targetEntry == null || string.IsNullOrEmpty(_currentEventId))
            {
                Log.Information("[DJSCREEN] StopRestart failed: No queue entry playing/selected or no event joined, PlayingQueueEntry={Playing}, SelectedQueueEntry={Selected}, EventId={EventId}",
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
                    StopRestartButtonColor = "#22d3ee";
                    OnPropertyChanged(nameof(SliderPosition));
                    OnPropertyChanged(nameof(CurrentVideoPosition));
                    OnPropertyChanged(nameof(TimeRemaining));
                    OnPropertyChanged(nameof(TimeRemainingSeconds));
                    OnPropertyChanged(nameof(StopRestartButtonColor));
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
                if (IsVideoPaused && _videoPlayerWindow?.MediaPlayer != null && PlayingQueueEntry != null)
                {
                    // Restart: Replay the same song from the beginning
                    string videoPath = Path.Combine(_settingsService.Settings.VideoCachePath, $"{PlayingQueueEntry.SongId}.mp4");
                    _videoPlayerWindow.PlayVideo(videoPath);
                    SliderPosition = 0;
                    CurrentVideoPosition = "0:00";
                    IsPlaying = true;
                    IsVideoPaused = false;
                    StopRestartButtonColor = "#22d3ee";
                    OnPropertyChanged(nameof(SliderPosition));
                    OnPropertyChanged(nameof(CurrentVideoPosition));
                    OnPropertyChanged(nameof(StopRestartButtonColor));
                    if (_updateTimer == null)
                    {
                        _updateTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(1000) };
                        _updateTimer.Tick += UpdateTimer_Tick;
                        _updateTimer.Start();
                    }
                    Log.Information("[DJSCREEN] Restarted video for QueueId={QueueId}: {SongTitle}", PlayingQueueEntry.QueueId, PlayingQueueEntry.SongTitle);
                }
                else
                {
                    // Stop: Pause playback, keep song in Now Playing
                    if (_videoPlayerWindow != null)
                    {
                        _videoPlayerWindow.StopVideo();
                        Log.Information("[DJSCREEN] Video playback stopped for QueueId={QueueId}", targetEntry.QueueId);
                    }
                    IsPlaying = false;
                    IsVideoPaused = true;
                    SliderPosition = 0;
                    CurrentVideoPosition = "--:--";
                    TimeRemainingSeconds = 0;
                    TimeRemaining = "0:00";
                    StopRestartButtonColor = "#FF0000";
                    OnPropertyChanged(nameof(SliderPosition));
                    OnPropertyChanged(nameof(CurrentVideoPosition));
                    OnPropertyChanged(nameof(TimeRemaining));
                    OnPropertyChanged(nameof(TimeRemainingSeconds));
                    OnPropertyChanged(nameof(StopRestartButtonColor));
                    if (_updateTimer != null)
                    {
                        _updateTimer.Stop();
                        Log.Information("[DJSCREEN] Stopped update timer for QueueId={QueueId}", targetEntry.QueueId);
                    }
                    Log.Information("[DJSCREEN] Stopped video, retained in Now Playing: QueueId={QueueId}: {SongTitle}", targetEntry.QueueId, targetEntry.SongTitle);
                }
            }
            catch (Exception ex)
            {
                Log.Error("[DJSCREEN] Failed to stop/restart queue {QueueId}: {Message}", targetEntry.QueueId, ex.Message);
                SetWarningMessage($"Failed to stop/restart: {ex.Message}");
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
                    if (_videoPlayerWindow == null)
                    {
                        _videoPlayerWindow = new VideoPlayerWindow();
                        _videoPlayerWindow.SongEnded += VideoPlayerWindow_SongEnded;
                        _videoPlayerWindow.Closed += VideoPlayerWindow_Closed;
                        Log.Information("[DJSCREEN] Subscribed to SongEnded and Closed events for VideoPlayerWindow");
                    }
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
                        _videoPlayerWindow.EndShow();
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
                            _videoPlayerWindow.EndShow();
                            _videoPlayerWindow = null;
                        }
                        IsShowActive = false;
                        ShowButtonText = "Start Show";
                        ShowButtonColor = "#22d3ee";
                        if (IsPlaying || IsVideoPaused)
                        {
                            IsPlaying = false;
                            IsVideoPaused = false;
                            SliderPosition = 0;
                            CurrentVideoPosition = "--:--";
                            TimeRemainingSeconds = 0;
                            TimeRemaining = "0:00";
                            StopRestartButtonColor = "#22d3ee";
                            OnPropertyChanged(nameof(SliderPosition));
                            OnPropertyChanged(nameof(CurrentVideoPosition));
                            OnPropertyChanged(nameof(TimeRemaining));
                            OnPropertyChanged(nameof(TimeRemainingSeconds));
                            OnPropertyChanged(nameof(StopRestartButtonColor));
                            PlayingQueueEntry = null;
                            OnPropertyChanged(nameof(PlayingQueueEntry));
                            if (_updateTimer != null)
                            {
                                _updateTimer.Stop();
                                Log.Information("[DJSCREEN] Stopped update timer due to show ending");
                            }
                            Log.Information("[DJSCREEN] Playback stopped due to show ending");
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
        private async Task ViewSungSongs()
        {
            Log.Information("[DJSCREEN] ViewSungSongs command invoked");
            if (_isDisposing) return;
            try
            {
                var sungWindow = new SungSongsView { DataContext = new SungSongsViewModel(_apiService, _currentEventId ?? "3") };
                sungWindow.ShowDialog();
                Log.Information("[DJSCREEN] SungSongsView shown for EventId={EventId}", _currentEventId);
                await Task.CompletedTask;
            }
            catch (Exception ex)
            {
                Log.Error("[DJSCREEN] Failed to show SungSongsView: {Message}", ex.Message);
                SetWarningMessage($"Failed to view sung songs: {ex.Message}");
            }
        }

        public async Task HandleSongEnded()
        {
            Log.Information("[DJSCREEN] Handling song ended");
            if (_isDisposing) return;
            try
            {
                if (_updateTimer != null)
                {
                    _updateTimer.Stop();
                    Log.Information("[DJSCREEN] Stopped update timer on song end");
                }

                if (PlayingQueueEntry != null && !string.IsNullOrEmpty(_currentEventId))
                {
                    await _apiService.CompleteSongAsync(_currentEventId, PlayingQueueEntry.QueueId);
                    Log.Information("[DJSCREEN] Completed song for event {EventId}, queue {QueueId}: {SongTitle}", _currentEventId, PlayingQueueEntry.QueueId, PlayingQueueEntry.SongTitle);
                    QueueEntries.Remove(PlayingQueueEntry);
                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        for (int i = 0; i < QueueEntries.Count; i++)
                        {
                            QueueEntries[i].IsUpNext = i == 0;
                        }
                        OnPropertyChanged(nameof(QueueEntries));
                    });
                }

                IsPlaying = false;
                IsVideoPaused = false;
                SliderPosition = 0;
                CurrentVideoPosition = "--:--";
                TimeRemainingSeconds = 0;
                TimeRemaining = "0:00";
                StopRestartButtonColor = "#22d3ee";
                OnPropertyChanged(nameof(SliderPosition));
                OnPropertyChanged(nameof(CurrentVideoPosition));
                OnPropertyChanged(nameof(TimeRemaining));
                OnPropertyChanged(nameof(TimeRemainingSeconds));
                OnPropertyChanged(nameof(StopRestartButtonColor));
                TotalSongsPlayed++;
                SungCount++;
                OnPropertyChanged(nameof(TotalSongsPlayed));
                OnPropertyChanged(nameof(SungCount));
                Log.Information("[DJSCREEN] Incremented TotalSongsPlayed: {Count}, SungCount: {SungCount}", TotalSongsPlayed, SungCount);

                if (_videoPlayerWindow != null)
                {
                    _videoPlayerWindow.StopVideo();
                }

                PlayingQueueEntry = null;
                OnPropertyChanged(nameof(PlayingQueueEntry));

                if (IsAutoPlayEnabled && !string.IsNullOrEmpty(_currentEventId))
                {
                    await PlayNextAutoPlaySong();
                }
                else
                {
                    Log.Information("[DJSCREEN] AutoPlay is disabled or no event joined, IsAutoPlayEnabled={State}", IsAutoPlayEnabled);
                    await LoadQueueData();
                    await LoadSungCountAsync();
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
                        _videoPlayerWindow.Closed -= VideoPlayerWindow_Closed;
                        _videoPlayerWindow = null;
                    }
                    IsShowActive = false;
                    ShowButtonText = "Start Show";
                    ShowButtonColor = "#22d3ee";
                    if (IsPlaying || IsVideoPaused)
                    {
                        IsPlaying = false;
                        IsVideoPaused = false;
                        SliderPosition = 0;
                        CurrentVideoPosition = "--:--";
                        TimeRemainingSeconds = 0;
                        TimeRemaining = "0:00";
                        StopRestartButtonColor = "#22d3ee";
                        OnPropertyChanged(nameof(SliderPosition));
                        OnPropertyChanged(nameof(CurrentVideoPosition));
                        OnPropertyChanged(nameof(TimeRemaining));
                        OnPropertyChanged(nameof(TimeRemainingSeconds));
                        OnPropertyChanged(nameof(StopRestartButtonColor));
                        PlayingQueueEntry = null;
                        OnPropertyChanged(nameof(PlayingQueueEntry));
                        if (_updateTimer != null)
                        {
                            _updateTimer.Stop();
                            Log.Information("[DJSCREEN] Stopped update timer due to VideoPlayerWindow close");
                        }
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
                if (_updateTimer != null)
                {
                    _updateTimer.Stop();
                    _updateTimer = null;
                }
                if (_videoPlayerWindow != null)
                {
                    _videoPlayerWindow.SongEnded -= VideoPlayerWindow_SongEnded;
                    _videoPlayerWindow.Closed -= VideoPlayerWindow_Closed;
                    _videoPlayerWindow.EndShow();
                    _videoPlayerWindow = null;
                }
            }
            catch (Exception ex)
            {
                Log.Error("[DJSCREEN] Failed to dispose resources: {Message}", ex.Message);
            }
        }
    }
}
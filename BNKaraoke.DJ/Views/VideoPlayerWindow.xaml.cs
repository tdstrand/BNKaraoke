using BNKaraoke.DJ.Services;
using LibVLCSharp.Shared;
using Serilog;
using System;
using System.Linq;
using System.Windows;
using System.Windows.Media;

namespace BNKaraoke.DJ.Views;

public partial class VideoPlayerWindow : Window
{
    private readonly SettingsService _settingsService = SettingsService.Instance;
    private readonly LibVLC? _libVLC;
    public LibVLCSharp.Shared.MediaPlayer? MediaPlayer { get; private set; }

    public event EventHandler? SongEnded;

    public VideoPlayerWindow()
    {
        InitializeComponent();
        try
        {
            Log.Information("[VIDEO PLAYER] Initializing VideoPlayerWindow");
            _libVLC = new LibVLC("--fullscreen", "--no-video-title-show", "--no-osd", "--no-video-deco");
            MediaPlayer = new LibVLCSharp.Shared.MediaPlayer(_libVLC);
            VideoPlayer.MediaPlayer = MediaPlayer;
            MediaPlayer.EndReached += MediaPlayerEnded;
            Loaded += VideoPlayerWindow_Loaded;
        }
        catch (Exception ex)
        {
            Log.Error("[VIDEO PLAYER] Failed to initialize VideoPlayerWindow: {Message}", ex.Message);
            MessageBox.Show($"Failed to initialize video player: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            Close();
        }
    }

    private void VideoPlayerWindow_Loaded(object sender, RoutedEventArgs e)
    {
        try
        {
            Log.Information("[VIDEO PLAYER] VideoPlayerWindow loaded, setting display");
            WindowStyle = WindowStyle.None;
            ResizeMode = ResizeMode.NoResize;
            WindowState = WindowState.Normal;
            SetDisplayDevice();
            WindowState = WindowState.Maximized;
            ShowActivated = true;

            // Log window and VideoView bounds
            var dpi = VisualTreeHelper.GetDpi(this);
            Log.Information("[VIDEO PLAYER] Window bounds: Left={Left}, Top={Top}, Width={Width}, Height={Height}, DpiScale={DpiScaleX}x{DpiScaleY}",
                Left, Top, Width, Height, dpi.DpiScaleX, dpi.DpiScaleY);
            Log.Information("[VIDEO PLAYER] VideoView bounds: Width={Width}, Height={Height}, ActualWidth={ActualWidth}, ActualHeight={ActualHeight}",
                VideoPlayer.Width, VideoPlayer.Height, VideoPlayer.ActualWidth, VideoPlayer.ActualHeight);
        }
        catch (Exception ex)
        {
            Log.Error("[VIDEO PLAYER] Failed to set display on load: {Message}", ex.Message);
        }
    }

    public void PlayVideo(string videoPath)
    {
        try
        {
            Log.Information("[VIDEO PLAYER] Playing video: {VideoPath}", videoPath);
#pragma warning disable CS8604 // Suppress null reference warning as _libVLC is initialized
            using var media = new Media(_libVLC, new Uri(videoPath), "--fullscreen", "--no-video-title-show", "--no-osd", "--no-video-deco");
#pragma warning restore CS8604
            MediaPlayer?.Play(media);
            // Force fullscreen
            Application.Current.Dispatcher.Invoke(() =>
            {
                WindowState = WindowState.Maximized;
                WindowStyle = WindowStyle.None;
            });
            // Log VLC state
            Log.Information("[VIDEO PLAYER] VLC state: IsPlaying={IsPlaying}, Fullscreen={Fullscreen}",
                MediaPlayer?.IsPlaying ?? false, MediaPlayer?.Fullscreen ?? false);
        }
        catch (Exception ex)
        {
            Log.Error("[VIDEO PLAYER] Failed to play video: {VideoPath}, Message: {Message}", videoPath, ex.Message);
            MessageBox.Show($"Failed to play video: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    public void StopVideo()
    {
        try
        {
            Log.Information("[VIDEO PLAYER] Stopping video");
            if (MediaPlayer?.IsPlaying == true)
            {
                MediaPlayer.Stop();
            }
        }
        catch (Exception ex)
        {
            Log.Error("[VIDEO PLAYER] Failed to stop video: {Message}", ex.Message);
        }
    }

    private void SetDisplayDevice()
    {
        try
        {
            string targetDevice = _settingsService.Settings.KaraokeVideoDevice;
            Log.Information("[VIDEO PLAYER] Setting display to: {Device}", targetDevice);

            // Log all available screens
            foreach (var screen in System.Windows.Forms.Screen.AllScreens)
            {
                Log.Information("[VIDEO PLAYER] Detected screen: {DeviceName}, Bounds: {Left}x{Top} {Width}x{Height}, Primary: {Primary}",
                    screen.DeviceName, screen.Bounds.Left, screen.Bounds.Top, screen.Bounds.Width, screen.Bounds.Height, screen.Primary);
            }

            var targetScreen = System.Windows.Forms.Screen.AllScreens.FirstOrDefault(
                screen => screen.DeviceName.Equals(targetDevice, StringComparison.OrdinalIgnoreCase));

            if (targetScreen != null)
            {
                var bounds = targetScreen.Bounds;
                // Use PresentationSource to get accurate DPI
                var source = PresentationSource.FromVisual(this);
                double dpiX = source?.CompositionTarget?.TransformToDevice.M11 ?? 1.0;
                double dpiY = source?.CompositionTarget?.TransformToDevice.M22 ?? 1.0;
                Left = bounds.Left / dpiX;
                Top = bounds.Top / dpiY;
                Width = bounds.Width / dpiX;
                Height = bounds.Height / dpiY;

                Log.Information("[VIDEO PLAYER] Set display to: {Device}, Adjusted Bounds: {Left}x{Top} {Width}x{Height}, DPI: {DpiX}x{DpiY}",
                    targetDevice, Left, Top, Width, Height, dpiX, dpiY);
            }
            else
            {
                Log.Warning("[VIDEO PLAYER] Target device not found: {Device}, falling back to primary monitor", targetDevice);
                var primaryScreen = System.Windows.Forms.Screen.PrimaryScreen;
                var source = PresentationSource.FromVisual(this);
                double dpiX = source?.CompositionTarget?.TransformToDevice.M11 ?? 1.0;
                double dpiY = source?.CompositionTarget?.TransformToDevice.M22 ?? 1.0;
                if (primaryScreen != null)
                {
                    var bounds = primaryScreen.Bounds;
                    Left = bounds.Left / dpiX;
                    Top = bounds.Top / dpiY;
                    Width = bounds.Width / dpiX;
                    Height = bounds.Height / dpiY;
                }
                else
                {
                    Left = SystemParameters.WorkArea.Left;
                    Top = SystemParameters.WorkArea.Top;
                    Width = SystemParameters.WorkArea.Width;
                    Height = SystemParameters.WorkArea.Height;
                }
                Log.Information("[VIDEO PLAYER] Fallback to primary, Adjusted Bounds: {Left}x{Top} {Width}x{Height}, DPI: {DpiX}x{DpiY}",
                    Left, Top, Width, Height, dpiX, dpiY);
            }
        }
        catch (Exception ex)
        {
            Log.Error("[VIDEO PLAYER] Failed to set display device: {Message}", ex.Message);
            Application.Current.Dispatcher.Invoke(() =>
            {
                MessageBox.Show($"Failed to set display device: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            });
        }
    }

    private void MediaPlayerEnded(object? sender, EventArgs e)
    {
        Application.Current.Dispatcher.Invoke(() =>
        {
            Log.Information("[VIDEO PLAYER] Media ended");
            MediaPlayer?.Stop();
            SongEnded?.Invoke(this, EventArgs.Empty);
        });
    }

    protected override void OnClosed(EventArgs e)
    {
        try
        {
            Log.Information("[VIDEO PLAYER] Closing VideoPlayerWindow");
            MediaPlayer?.Stop();
            MediaPlayer?.Dispose();
            _libVLC?.Dispose();
        }
        catch (Exception ex)
        {
            Log.Error("[VIDEO PLAYER] Error during cleanup: {Message}", ex.Message);
        }
        base.OnClosed(e);
    }
}
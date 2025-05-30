using BNKaraoke.DJ.Services;
using LibVLCSharp.Shared;
using Serilog;
using System;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;

namespace BNKaraoke.DJ.Views;

public partial class VideoPlayerWindow : Window
{
    private readonly SettingsService _settingsService = SettingsService.Instance;
    private readonly LibVLC? _libVLC;
    public LibVLCSharp.Shared.MediaPlayer? MediaPlayer { get; private set; }
    private bool _isDisposing;

    public event EventHandler? SongEnded;
    public event EventHandler<MediaPlayerTimeChangedEventArgs>? TimeChanged;

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags);

    [Flags]
    private enum SetWindowPosFlags : uint
    {
        SWP_NOSIZE = 0x0001,
        SWP_NOMOVE = 0x0002,
        SWP_NOZORDER = 0x0004,
        SWP_NOREDRAW = 0x0008,
        SWP_NOACTIVATE = 0x0010,
        SWP_FRAMECHANGED = 0x0020,
        SWP_SHOWWINDOW = 0x0040,
        SWP_HIDEWINDOW = 0x0080,
        SWP_NOCOPYBITS = 0x0100,
        SWP_NOOWNERZORDER = 0x0200,
        SWP_NOSENDCHANGING = 0x0400
    }

    public VideoPlayerWindow()
    {
        try
        {
            Log.Information("[VIDEO PLAYER] Initializing video player window");
            _libVLC = new LibVLC("--no-video-title-show", "--no-osd", "--no-video-deco");
            MediaPlayer = new LibVLCSharp.Shared.MediaPlayer(_libVLC);
            ShowInTaskbar = true;
            Owner = null; // Prevent parenting to primary monitor
            WindowStartupLocation = WindowStartupLocation.Manual;
            InitializeComponent();
            VideoPlayer.MediaPlayer = MediaPlayer;
            SourceInitialized += VideoPlayerWindow_SourceInitialized;
            Loaded += VideoPlayerWindow_Loaded;
            Log.Information("[VIDEO PLAYER] Video player window initialized successfully");
        }
        catch (Exception ex)
        {
            Log.Error("[VIDEO PLAYER] Failed to initialize video player window: {Message}", ex.Message);
            MessageBox.Show($"Failed to initialize video player: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            Close();
        }
    }

    private void VideoPlayerWindow_SourceInitialized(object? sender, EventArgs e)
    {
        try
        {
            Log.Information("[VIDEO PLAYER] Source initialized, setting display");
            SetDisplayDevice();
            Show();
            Activate(); // Ensure visibility and focus
            Log.Information("[VIDEO PLAYER] Window visibility after SourceInitialized: {Visibility}, ShowInTaskbar: {ShowInTaskbar}", Visibility, ShowInTaskbar);
        }
        catch (Exception ex)
        {
            Log.Error("[VIDEO PLAYER] Failed to set display on source initialized: {Message}", ex.Message);
        }
    }

    private void VideoPlayerWindow_Loaded(object sender, RoutedEventArgs e)
    {
        try
        {
            Log.Information("[VIDEO PLAYER] Video player window loaded, finalizing display");
            WindowStyle = WindowStyle.None;
            ResizeMode = ResizeMode.NoResize;
            WindowState = WindowState.Maximized;
            ShowActivated = true;

            // Verify bounds and reposition if incorrect
            var hwnd = new WindowInteropHelper(this).Handle;
            var currentScreen = System.Windows.Forms.Screen.FromHandle(hwnd);
            if (Left < 0 || Top < 0 || currentScreen.DeviceName != _settingsService.Settings.KaraokeVideoDevice)
            {
                Log.Warning("[VIDEO PLAYER] Incorrect bounds or screen: Left={Left}, Top={Top}, Screen={Screen}, repositioning", Left, Top, currentScreen.DeviceName);
                SetDisplayDevice();
            }

            if (MediaPlayer != null)
            {
                MediaPlayer.EndReached += MediaPlayerEnded;
                MediaPlayer.TimeChanged += MediaPlayer_TimeChanged;
            }

            Visibility = Visibility.Visible;
            Show();
            Activate(); // Ensure window is active

            Log.Information("[VIDEO PLAYER] Current screen: {DeviceName}, Bounds: {Left}x{Top} {Width}x{Height}, Primary: {Primary}",
                currentScreen.DeviceName, currentScreen.Bounds.Left, currentScreen.Bounds.Top, currentScreen.Bounds.Width, currentScreen.Bounds.Height, currentScreen.Primary);
            Log.Information("[VIDEO PLAYER] Final window bounds: Left={Left}, Top={Top}, Width={Width}, Height={Height}",
                Left, Top, Width, Height);
            Log.Information("[VIDEO PLAYER] VideoView bounds: Width={Width}, Height={Height}, ActualWidth={ActualWidth}, ActualHeight={ActualHeight}",
                VideoPlayer.Width, VideoPlayer.Height, VideoPlayer.ActualWidth, VideoPlayer.ActualHeight);
            Log.Information("[VIDEO PLAYER] Window visibility: {Visibility}, ShowInTaskbar: {ShowInTaskbar}", Visibility, ShowInTaskbar);
        }
        catch (Exception ex)
        {
            Log.Error("[VIDEO PLAYER] Failed to set display on load: {Message}", ex.Message);
            MessageBox.Show($"Failed to set display: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    public void PlayVideo(string videoPath)
    {
        try
        {
            Log.Information("[VIDEO PLAYER] Attempting to play video: {VideoPath}", videoPath);
            if (_libVLC == null || MediaPlayer == null) throw new InvalidOperationException("Media player not initialized");
            string device = _settingsService.Settings.KaraokeVideoDevice; // \\.\DISPLAY4
            using var media = new Media(_libVLC, new Uri(videoPath), $"--directx-device={device}", "--no-video-title-show", "--no-osd", "--no-video-deco");
            MediaPlayer.Play(media);
            Visibility = Visibility.Visible;
            Show();
            Activate(); // Ensure visibility during playback
            Application.Current.Dispatcher.Invoke(() =>
            {
                WindowStyle = WindowStyle.None;
                WindowState = WindowState.Maximized;
            });
            Log.Information("[VIDEO PLAYER] VLC state: IsPlaying={IsPlaying}, Fullscreen={Fullscreen}",
                MediaPlayer.IsPlaying, MediaPlayer.Fullscreen);
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
            Visibility = Visibility.Visible;
            Show();
            Activate();
        }
        catch (Exception ex)
        {
            Log.Error("[VIDEO PLAYER] Failed to stop video: {Message}", ex.Message);
        }
    }

    internal void SetDisplayDevice()
    {
        try
        {
            string targetDevice = _settingsService.Settings.KaraokeVideoDevice;
            Log.Information("[VIDEO PLAYER] Setting display to: {Device}", targetDevice);

            var targetScreen = System.Windows.Forms.Screen.AllScreens.FirstOrDefault(
                screen => screen.DeviceName.Equals(targetDevice, StringComparison.OrdinalIgnoreCase));

            foreach (var screen in System.Windows.Forms.Screen.AllScreens)
            {
                Log.Information("[VIDEO PLAYER] Detected screen: {DeviceName}, Bounds: {Left}x{Top} {Width}x{Height}, Primary: {Primary}",
                    screen.DeviceName, screen.Bounds.Left, screen.Bounds.Top, screen.Bounds.Width, screen.Bounds.Height, screen.Primary);
            }

            if (targetScreen != null)
            {
                var bounds = targetScreen.Bounds;
                IntPtr hwnd = new WindowInteropHelper(this).Handle;
                bool result = SetWindowPos(hwnd, IntPtr.Zero, bounds.Left, bounds.Top, bounds.Width, bounds.Height, (uint)(SetWindowPosFlags.SWP_NOZORDER | SetWindowPosFlags.SWP_NOACTIVATE));
                Log.Information("[VIDEO PLAYER] SetWindowPos to {Device}, Position: {Left}x{Top}, Size: {Width}x{Height}, Success: {Result}, Flags: {Flags}",
                    targetDevice, bounds.Left, bounds.Top, bounds.Width, bounds.Height, result, (uint)(SetWindowPosFlags.SWP_NOZORDER | SetWindowPosFlags.SWP_NOACTIVATE));
                var currentScreen = System.Windows.Forms.Screen.FromHandle(hwnd);
                Log.Information("[VIDEO PLAYER] Current screen after SetWindowPos: {DeviceName}", currentScreen.DeviceName);
            }
            else
            {
                Log.Warning("[VIDEO PLAYER] Target device not found: {Device}, falling back to primary monitor", targetDevice);
                var primaryScreen = System.Windows.Forms.Screen.PrimaryScreen;
                if (primaryScreen != null)
                {
                    var bounds = primaryScreen.Bounds;
                    IntPtr hwnd = new WindowInteropHelper(this).Handle;
                    bool result = SetWindowPos(hwnd, IntPtr.Zero, bounds.Left, bounds.Top, bounds.Width, bounds.Height, (uint)(SetWindowPosFlags.SWP_NOZORDER | SetWindowPosFlags.SWP_NOACTIVATE));
                    Log.Information("[VIDEO PLAYER] Fallback to primary, Position: {Left}x{Top}, Size: {Width}x{Height}, Success: {Result}, Flags: {Flags}",
                        bounds.Left, bounds.Top, bounds.Width, bounds.Height, result, (uint)(SetWindowPosFlags.SWP_NOZORDER | SetWindowPosFlags.SWP_NOACTIVATE));
                }
                else
                {
                    Log.Error("[VIDEO PLAYER] No primary screen found");
                }
            }
        }
        catch (Exception ex)
        {
            Log.Error("[VIDEO PLAYER] Failed to set display device: {Message}", ex.Message);
            MessageBox.Show($"Failed to set display device: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void MediaPlayerEnded(object? sender, EventArgs e)
    {
        if (_isDisposing) return;
        try
        {
            Application.Current.Dispatcher.InvokeAsync(() =>
            {
                Log.Information("[VIDEO PLAYER] Media ended");
                if (MediaPlayer != null)
                {
                    MediaPlayer.Stop();
                }
                Visibility = Visibility.Visible;
                Show();
                Activate();
                Log.Information("[VIDEO PLAYER] Invoking SongEnded event");
                SongEnded?.Invoke(this, EventArgs.Empty);
            });
        }
        catch (Exception ex)
        {
            Log.Error("[VIDEO PLAYER] Failed to process MediaEnded event: {Message}, StackTrace={StackTrace}", ex.Message, ex.StackTrace);
        }
    }

    private void MediaPlayer_TimeChanged(object? sender, MediaPlayerTimeChangedEventArgs e)
    {
        if (_isDisposing) return;
        try
        {
            Application.Current.Dispatcher.InvokeAsync(() =>
            {
                TimeChanged?.Invoke(this, e);
            });
        }
        catch (Exception ex)
        {
            Log.Error("[VIDEO PLAYER] Failed to process TimeChanged event: {Message}", ex.Message);
        }
    }

    protected override void OnClosed(EventArgs e)
    {
        _isDisposing = true;
        try
        {
            Log.Information("[VIDEO PLAYER] Closing video player window");
            if (MediaPlayer != null)
            {
                MediaPlayer.Stop();
                MediaPlayer.TimeChanged -= MediaPlayer_TimeChanged;
                MediaPlayer.EndReached -= MediaPlayerEnded;
                MediaPlayer.Dispose();
                MediaPlayer = null;
            }
            _libVLC?.Dispose();
        }
        catch (Exception ex)
        {
            Log.Error("[VIDEO PLAYER] Error during cleanup: {Message}", ex.Message);
        }
        base.OnClosed(e);
    }
}
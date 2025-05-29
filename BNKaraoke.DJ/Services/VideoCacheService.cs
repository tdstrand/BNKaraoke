using Serilog;
using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace BNKaraoke.DJ.Services;

public class VideoCacheService
{
    private readonly SettingsService _settingsService;
    private readonly string _ytDlpPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Tools", "yt-dlp.exe");
    private readonly string _ffmpegPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Tools", "ffmpeg.exe");

    public VideoCacheService(SettingsService settingsService)
    {
        _settingsService = settingsService;
    }

    public async Task CacheVideoAsync(string youTubeUrl, int songId)
    {
        if (!_settingsService.Settings.EnableVideoCaching)
        {
            Log.Information("[CACHE SERVICE] Video caching disabled, skipping: SongId={SongId}", songId);
            return;
        }

        string cachePath = Path.Combine(_settingsService.Settings.VideoCachePath, $"{songId}.mp4");
        if (File.Exists(cachePath))
        {
            Log.Information("[CACHE SERVICE] Video already cached: SongId={SongId}", songId);
            return;
        }

        try
        {
            EnsureCacheDirectory();
            EnsureCacheSize();

            string args = $"--output \"{cachePath}\" --format mp4 --merge-output-format mp4 \"{youTubeUrl}\"";
            var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = _ytDlpPath,
                    Arguments = args,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                }
            };

            process.Start();
            string output = await process.StandardOutput.ReadToEndAsync();
            string error = await process.StandardError.ReadToEndAsync();
            await process.WaitForExitAsync();

            if (process.ExitCode == 0)
            {
                Log.Information("[CACHE SERVICE] Cached video: SongId={SongId}, Output: {Output}", songId, output);
            }
            else
            {
                Log.Error("[CACHE SERVICE] Failed to cache video: SongId={SongId}, Error: {Error}", songId, error);
            }
        }
        catch (Exception ex)
        {
            Log.Error("[CACHE SERVICE] Error caching video: SongId={SongId}, Message: {Message}", songId, ex.Message);
        }
    }

    public bool IsVideoCached(int songId)
    {
        string cachePath = Path.Combine(_settingsService.Settings.VideoCachePath, $"{songId}.mp4");
        return File.Exists(cachePath);
    }

    private void EnsureCacheDirectory()
    {
        string cacheDir = _settingsService.Settings.VideoCachePath;
        if (!Directory.Exists(cacheDir))
        {
            Directory.CreateDirectory(cacheDir);
            Log.Information("[CACHE SERVICE] Created cache directory: {CacheDir}", cacheDir);
        }
    }

    private void EnsureCacheSize()
    {
        try
        {
            long maxSizeBytes = (long)(_settingsService.Settings.CacheSizeGB * 1024 * 1024 * 1024);
            var cacheDir = new DirectoryInfo(_settingsService.Settings.VideoCachePath);
            var files = cacheDir.GetFiles("*.mp4").OrderBy(f => f.LastWriteTime).ToList();
            long totalSize = files.Sum(f => f.Length);

            while (totalSize > maxSizeBytes && files.Any())
            {
                var oldestFile = files.First();
                long fileSize = oldestFile.Length;
                oldestFile.Delete();
                totalSize -= fileSize;
                files.RemoveAt(0);
                Log.Information("[CACHE SERVICE] Deleted oldest cached file: {FileName}, Size: {Size} bytes", oldestFile.Name, fileSize);
            }
        }
        catch (Exception ex)
        {
            Log.Error("[CACHE SERVICE] Error managing cache size: {Message}", ex.Message);
        }
    }
}
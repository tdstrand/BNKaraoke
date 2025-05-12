using Avalonia;
using System;
using System.Diagnostics;

namespace BNKaraoke.DJ;

sealed class Program
{
    [STAThread]
    public static void Main(string[] args)
    {
        Debug.WriteLine("Program.Main: Starting application...");
        try
        {
            BuildAvaloniaApp()
                .StartWithClassicDesktopLifetime(args);
            Debug.WriteLine("Program.Main: Application started successfully.");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Program.Main: Exception during startup: {ex}");
            throw;
        }
    }

    public static AppBuilder BuildAvaloniaApp()
    {
        Debug.WriteLine("Program.BuildAvaloniaApp: Configuring AppBuilder...");
        var builder = AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .LogToTrace();
        Debug.WriteLine("Program.BuildAvaloniaApp: AppBuilder configured.");
        return builder;
    }
}
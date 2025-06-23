using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using System.Diagnostics;

namespace BNKaraoke.DJ
{
    public partial class App : Application
    {
        public override void Initialize()
        {
            Debug.WriteLine("App.Initialize: Starting XAML loading...");
            AvaloniaXamlLoader.Load(this);
            Debug.WriteLine("App.Initialize: XAML loaded successfully.");
        }

        public override void OnFrameworkInitializationCompleted()
        {
            Debug.WriteLine("App.OnFrameworkInitializationCompleted: Checking application lifetime...");
            if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            {
                Debug.WriteLine("App.OnFrameworkInitializationCompleted: Creating MainWindow...");
                desktop.MainWindow = new Views.MainWindow();
                Debug.WriteLine("App.OnFrameworkInitializationCompleted: MainWindow created.");
            }
            else
            {
                Debug.WriteLine("App.OnFrameworkInitializationCompleted: ApplicationLifetime is not IClassicDesktopStyleApplicationLifetime.");
            }

            base.OnFrameworkInitializationCompleted();
            Debug.WriteLine("App.OnFrameworkInitializationCompleted: Completed.");
        }
    }
}
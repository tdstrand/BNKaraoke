using System;
using System.Windows;
using Serilog;

namespace BNKaraoke.DJ.Views;

public partial class DJScreen : Window
{
    public DJScreen()
    {
        try
        {
            InitializeComponent();
            Log.Information("[TRACE] DJScreen InitializeComponent completed.");
        }
        catch (Exception ex)
        {
            Log.Error(ex, "[ERROR] Exception during InitializeComponent in DJScreen.");
            MessageBox.Show("An error occurred while initializing DJScreen. See log for details.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            Close();
        }

        Loaded += DJScreen_Loaded;
        Closing += DJScreen_Closing;
    }

    private void DJScreen_Loaded(object sender, RoutedEventArgs e)
    {
        Log.Information("[TRACE] DJScreen Loaded event fired.");
        MessageBox.Show("DJScreen loaded. Close this window manually to exit.", "DJScreen", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private void DJScreen_Closing(object sender, System.ComponentModel.CancelEventArgs e)
    {
        Log.Information("[TRACE] DJScreen Closing event fired.");
    }
}

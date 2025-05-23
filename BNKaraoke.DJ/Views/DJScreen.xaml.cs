using System;
using System.Windows;
using Serilog;
using BNKaraoke.DJ.ViewModels;

namespace BNKaraoke.DJ.Views
{
    public partial class DJScreen : Window
    {
        private readonly DJScreenViewModel _viewModel = new DJScreenViewModel();

        public DJScreen()
        {
            try
            {
                Log.Information("[DJSCREEN] Initializing window");
                InitializeComponent();
                DataContext = _viewModel;
                Log.Information("[DJSCREEN] DataContext set to new DJScreenViewModel: {InstanceId}", _viewModel.GetHashCode());
                _viewModel.UpdateAuthenticationState();
                Log.Information("[DJSCREEN] Called UpdateAuthenticationState in constructor");
            }
            catch (Exception ex)
            {
                Log.Error("[DJSCREEN] Failed to initialize window: {Message}", ex.Message);
                MessageBox.Show($"Failed to initialize DJScreen: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            try
            {
                Log.Information("[DJSCREEN] Window loaded");
                if (DataContext is DJScreenViewModel viewModel)
                {
                    viewModel.UpdateAuthenticationState();
                    Log.Information("[DJSCREEN] Forced UpdateAuthenticationState on load");
                    InvalidateVisual();
                    Log.Information("[DJSCREEN] Forced UI refresh with InvalidateVisual");
                }
                else
                {
                    Log.Error("[DJSCREEN] DataContext is not DJScreenViewModel");
                    DataContext = _viewModel;
                    _viewModel.UpdateAuthenticationState();
                    InvalidateVisual();
                    Log.Information("[DJSCREEN] Reset DataContext and called UpdateAuthenticationState with UI refresh");
                }
            }
            catch (Exception ex)
            {
                Log.Error("[DJSCREEN] Failed to load window: {Message}", ex.Message);
                MessageBox.Show($"Failed to load DJScreen: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}
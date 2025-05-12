using Avalonia.Controls;
using System.Net.Http;
using BNKaraoke.DJ.Services;
using BNKaraoke.DJ.ViewModels;

namespace BNKaraoke.DJ.Views
{
    public partial class VideoPlayerWindow : Window
    {
        public VideoPlayerWindow()
        {
            InitializeComponent();

            // Supply the required dependencies.
            IApiService apiService = new ApiService(new HttpClient());
            ISignalRService signalRService = new SignalRService();

            // For now, use MainWindowViewModel as the DataContext.
            DataContext = new MainWindowViewModel(apiService, signalRService);
        }
    }
}
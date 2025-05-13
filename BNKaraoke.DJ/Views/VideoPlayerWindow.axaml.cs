// File: VideoPlayerWindow.axaml.cs
using Avalonia.Controls;
using BNKaraoke.DJ.Services;

namespace BNKaraoke.DJ.Views
{
    public partial class VideoPlayerWindow : Window
    {
        public VideoPlayerWindow()
        {
            InitializeComponent();
            var api = DependencyLocator.ApiService; // IApiServices type
        }
    }
}

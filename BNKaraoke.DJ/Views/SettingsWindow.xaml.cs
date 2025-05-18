using System.Windows;
using BNKaraoke.DJ.Models;
using BNKaraoke.DJ.Services;

namespace BNKaraoke.DJ.Views
{
    public partial class SettingsWindow : Window
    {
        public SettingsWindow(AppSettings settings, SettingsService settingsService)
        {
            InitializeComponent();
            ApiBaseUrlBox.Text = settings.ApiBaseUrl;
            _settings = settings;
            _settingsService = settingsService;
        }

        private readonly AppSettings _settings;
        private readonly SettingsService _settingsService;

        private void Save_Click(object sender, RoutedEventArgs e)
        {
            _settings.ApiBaseUrl = ApiBaseUrlBox.Text;
            _settingsService.SaveSettings(_settings);
            DialogResult = true;
            Close();
        }
    }
}
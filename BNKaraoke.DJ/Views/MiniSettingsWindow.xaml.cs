using System.Windows;
using BNKaraoke.DJ.Models;
using BNKaraoke.DJ.Services;

namespace BNKaraoke.DJ.Views;

public partial class MiniSettingsWindow : Window
{
    private readonly AppSettings _settings;
    private readonly SettingsService _settingsService;

    public MiniSettingsWindow(AppSettings settings)
    {
        InitializeComponent();
        _settings = settings;
        _settingsService = new SettingsService();
        ApiBaseUrlTextBox.Text = _settings.ApiBaseUrl;
    }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        _settings.ApiBaseUrl = ApiBaseUrlTextBox.Text.Trim();
        _settingsService.SaveSettings(_settings);
        DialogResult = true;
        Close();
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
}

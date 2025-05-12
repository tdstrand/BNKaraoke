using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows.Input;

namespace BNKaraoke.DJ.ViewModels
{
    public class SettingsDialogViewModel : INotifyPropertyChanged
    {
        private string _customMessage;
        public string CustomMessage
        {
            get => _customMessage;
            set
            {
                _customMessage = value;
                OnPropertyChanged();
            }
        }

        private string _apiUrl;
        public string ApiUrl
        {
            get => _apiUrl;
            set
            {
                _apiUrl = value;
                OnPropertyChanged();
            }
        }

        public ObservableCollection<string> AvailableApiUrls { get; }

        public ICommand SaveCommand { get; }
        public ICommand CancelCommand { get; }

        public SettingsDialogViewModel()
        {
            _customMessage = "Default Custom Message";
            _apiUrl = "http://localhost:7290";
            AvailableApiUrls = new ObservableCollection<string>
            {
                "http://localhost:7290",
                "https://api.bnkaraoke.com"
            };

            SaveCommand = new RelayCommand(async (param) => await SaveAsync(param));
            CancelCommand = new RelayCommand(async (param) => await CancelAsync(param));
        }

        private async Task SaveAsync(object? param)
        {
            // Implement your save logic here.
            await Task.CompletedTask;
        }

        private async Task CancelAsync(object? param)
        {
            // Implement your cancel logic here.
            await Task.CompletedTask;
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
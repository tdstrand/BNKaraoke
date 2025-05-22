using System.Windows.Input;
using CommunityToolkit.Mvvm.Input;
using BNKaraoke.DJ.Models;

namespace BNKaraoke.DJ.ViewModels
{
    public class SongDetailsViewModel : ViewModelBase
    {
        private QueueEntry? _selectedQueueEntry;
        public QueueEntry? SelectedQueueEntry
        {
            get => _selectedQueueEntry;
            set
            {
                _selectedQueueEntry = value;
                OnPropertyChanged(nameof(SelectedQueueEntry));
            }
        }

        public ICommand CloseCommand { get; }

        public SongDetailsViewModel()
        {
            CloseCommand = new RelayCommand<object>(Close);
        }

        private void Close(object? parameter)
        {
            // Placeholder for window close logic
        }
    }
}
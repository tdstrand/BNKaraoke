using Avalonia.Controls;
using CommunityToolkit.Mvvm.ComponentModel;

using CommunityToolkit.Mvvm.Input;

namespace BNKaraoke.DJ.ViewModels
{
    public partial class ConfirmationDialogViewModel : ObservableObject
    {
        private readonly Window _window;

        [ObservableProperty]
        private string _title;

        [ObservableProperty]
        private string _message;

        public ConfirmationDialogViewModel(Window window, string title, string message)
        {
            _window = window;
            _title = title;
            _message = message;
        }

        [RelayCommand]
        private void Confirm()
        {
            _window.Close(true);
        }

        [RelayCommand]
        private void Cancel()
        {
            _window.Close(false);
        }
    }
}
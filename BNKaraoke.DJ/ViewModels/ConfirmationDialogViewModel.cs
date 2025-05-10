using Avalonia.Controls;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace BNKaraoke.DJ.ViewModels
{
    public partial class ConfirmationDialogViewModel : ObservableObject
    {
        private readonly Window _window;

        public ConfirmationDialogViewModel(Window window)
        {
            _window = window;
        }

        [ObservableProperty]
        private string _title = "Confirmation";

        [ObservableProperty]
        private string _message = string.Empty;

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
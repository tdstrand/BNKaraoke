using Avalonia.Controls;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace BNKaraoke.DJ.ViewModels
{
    public partial class MessageDialogViewModel : ObservableObject
    {
        private readonly Window _window;

        public MessageDialogViewModel(Window window)
        {
            _window = window;
        }

        [ObservableProperty]
        private string _title = "Message";

        [ObservableProperty]
        private string _message = string.Empty;

        [RelayCommand]
        private void Close()
        {
            _window.Close();
        }
    }
}
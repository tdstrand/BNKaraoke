using Avalonia.Controls;
using CommunityToolkit.Mvvm.ComponentModel;

using CommunityToolkit.Mvvm.Input;

namespace BNKaraoke.DJ.ViewModels
{
    public partial class MessageDialogViewModel : ObservableObject
    {
        private readonly Window _window;

        [ObservableProperty]
        private string _title;

        [ObservableProperty]
        private string _message;

        public MessageDialogViewModel(Window window, string title, string message)
        {
            _window = window;
            _title = title;
            _message = message;
        }

        [RelayCommand]
        private void OK()
        {
            _window.Close();
        }
    }
}
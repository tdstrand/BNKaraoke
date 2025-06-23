using Avalonia.Controls;
using Avalonia.Markup.Xaml;

using BNKaraoke.DJ.ViewModels;

namespace BNKaraoke.DJ.Views
{
    public partial class MessageDialog : Window
    {
        public MessageDialog(string title, string message)
        {
            InitializeComponent();
            DataContext = new MessageDialogViewModel(this, title, message);
        }

        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);
        }
    }
}
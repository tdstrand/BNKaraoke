using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using BNKaraoke.DJ.ViewModels;

namespace BNKaraoke.DJ.Views
{
    public partial class MessageDialog : Window
    {
        public MessageDialog()
        {
            InitializeComponent();
        }

        public MessageDialog(string title, string message) : this()
        {
            DataContext = new MessageDialogViewModel(this)
            {
                Title = title,
                Message = message
            };
        }

        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);
        }
    }
}
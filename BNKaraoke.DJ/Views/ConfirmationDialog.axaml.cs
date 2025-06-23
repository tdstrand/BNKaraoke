using Avalonia.Controls;
using Avalonia.Markup.Xaml;

using BNKaraoke.DJ.ViewModels;
namespace BNKaraoke.DJ.Views
{
    public partial class ConfirmationDialog : Window
    {
        public ConfirmationDialog(string title, string message)
        {
            InitializeComponent();
            DataContext = new ConfirmationDialogViewModel(this, title, message);
        }

        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);
        }
    }
}
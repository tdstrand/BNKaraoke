using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using BNKaraoke.DJ.ViewModels;

namespace BNKaraoke.DJ.Views
{
    public partial class ConfirmationDialog : Window
    {
        public ConfirmationDialog()
        {
            InitializeComponent();
        }

        public ConfirmationDialog(string title, string message) : this()
        {
            DataContext = new ConfirmationDialogViewModel(this)
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
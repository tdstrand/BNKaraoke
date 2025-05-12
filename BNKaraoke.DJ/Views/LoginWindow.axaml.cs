using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using BNKaraoke.DJ.ViewModels;
using System.Net.Http;

namespace BNKaraoke.DJ.Views
{
    public partial class LoginWindow : Window
    {
        public LoginWindow()
        {
            InitializeComponent();
            DataContext = new LoginWindowViewModel(this, new HttpClient());
        }

        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);
        }

        private void PhoneNumber_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                var passwordBox = this.FindControl<TextBox>("PasswordBox");
                if (passwordBox != null)
                {
                    passwordBox.Focus();
                }
            }
        }

        private void Password_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                var viewModel = DataContext as LoginWindowViewModel;
                if (viewModel != null)
                {
                    viewModel.LoginCommand.Execute(null);
                }
            }
        }
    }
}
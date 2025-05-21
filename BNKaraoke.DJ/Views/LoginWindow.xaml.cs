// Views/LoginWindow.xaml.cs
using BNKaraoke.DJ.ViewModels;
using System;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace BNKaraoke.DJ.Views;

public partial class LoginWindow : Window
{
    private readonly LoginWindowViewModel _viewModel;

    public LoginWindow()
    {
        InitializeComponent();
        _viewModel = new LoginWindowViewModel();
        DataContext = _viewModel;
        PhoneNumberBox.TextChanged += PhoneNumberTextBox_TextChanged;
        PhoneNumberBox.PreviewTextInput += PhoneNumberTextBox_PreviewTextInput;
        PasswordBox.PasswordChanged += (s, e) => _viewModel.Password = PasswordBox.Password;
        KeyDown += Window_KeyDown;
        PhoneNumberBox.Focus();
    }

    private void PhoneNumberTextBox_PreviewTextInput(object sender, TextCompositionEventArgs e)
    {
        e.Handled = !Regex.IsMatch(e.Text, "[0-9]");
    }

    private void PhoneNumberTextBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        var digits = Regex.Replace(PhoneNumberBox.Text, "[^0-9]", "");
        if (digits.Length > 10) digits = digits.Substring(0, 10);

        string formatted = digits;
        if (digits.Length >= 7)
            formatted = $"({digits.Substring(0, 3)}) {digits.Substring(3, 3)}-{digits.Substring(6)}";
        else if (digits.Length >= 4)
            formatted = $"({digits.Substring(0, 3)}) {digits.Substring(3)}";
        else if (digits.Length >= 1)
            formatted = $"({digits}";

        PhoneNumberBox.Text = formatted;
        PhoneNumberBox.CaretIndex = PhoneNumberBox.Text.Length;
    }

    private void Window_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter && PhoneNumberBox.Text.Length >= 10 && PasswordBox.Password.Length > 0)
        {
            _viewModel.LoginCommand.Execute(null);
        }
    }

    protected override void OnClosed(EventArgs e)
    {
        PhoneNumberBox.Text = string.Empty;
        PasswordBox.Clear();
        base.OnClosed(e);
    }
}
// Views/DJScreen.xaml.cs
using System.Windows;
using BNKaraoke.DJ.ViewModels;

namespace BNKaraoke.DJ.Views;

public partial class DJScreen : Window
{
    public DJScreen()
    {
        InitializeComponent();
        DataContext = new DJScreenViewModel();
    }
}
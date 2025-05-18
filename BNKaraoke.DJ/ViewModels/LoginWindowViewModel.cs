namespace BNKaraoke.DJ.ViewModels;

public class LoginWindowViewModel
{
    public string Phone { get; set; } = "";
    public string Password { get; set; } = "";

    public bool CanLogin => !string.IsNullOrWhiteSpace(Phone) && !string.IsNullOrWhiteSpace(Password);
}

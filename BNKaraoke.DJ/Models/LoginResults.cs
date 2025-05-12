namespace BNKaraoke.DJ.Models
{
    public class LoginResult
    {
        public string Token { get; set; } = string.Empty;
        public string? RefreshToken { get; set; }
    }
}
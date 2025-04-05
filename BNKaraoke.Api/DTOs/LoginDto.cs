using System.ComponentModel.DataAnnotations;

namespace BNKaraoke.Api.DTOs
{
    public class LoginDto
    {
        [Required]
        public string UserName { get; set; } = string.Empty;

        [Required]
        [DataType(DataType.Password)]
        public string Password { get; set; } = string.Empty;
    }

    // ✅ Add UserResponseDto to return additional info on login
    public class UserResponseDto
    {
        public string Token { get; set; } = string.Empty;
        public string FirstName { get; set; } = string.Empty;
        public string LastName { get; set; } = string.Empty;
        public List<string> Roles { get; set; } = new List<string>();
    }
}

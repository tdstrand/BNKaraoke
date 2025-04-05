using System.ComponentModel.DataAnnotations;

namespace BNKaraoke.Api.DTOs
{
    public class RegisterDto
    {
        [Required]
        public string PhoneNumber { get; set; } = string.Empty;

        [Required]
        [DataType(DataType.Password)]
        public string Password { get; set; } = string.Empty;

        [Required] // ✅ Add First Name field
        public string FirstName { get; set; } = string.Empty;

        [Required] // ✅ Add Last Name field
        public string LastName { get; set; } = string.Empty;

        [Required]
        public List<string> Roles { get; set; } = new List<string>();
    }
}

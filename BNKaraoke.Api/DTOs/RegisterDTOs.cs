using System.ComponentModel.DataAnnotations;

namespace BNKaraoke.Api.DTOs
{
    public class RegisterDto
    {
        [Required]
        public required string PhoneNumber { get; set; }

        [Required]
        [DataType(DataType.Password)]
        public required string Password { get; set; }

        [Required]
        public required string FirstName { get; set; }

        [Required]
        public required string LastName { get; set; }

        [Required]
        public required List<string> Roles { get; set; }
    }
}
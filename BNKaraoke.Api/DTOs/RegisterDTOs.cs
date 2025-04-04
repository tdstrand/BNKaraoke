using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace BNKaraoke.Web.DTOs
{
    public class RegisterDto
    {
        [Required]
        public string PhoneNumber { get; set; }

        [Required]
        public string Password { get; set; }

        public List<string> Roles { get; set; }
    }
}

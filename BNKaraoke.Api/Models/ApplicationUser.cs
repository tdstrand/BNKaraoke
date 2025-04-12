using Microsoft.AspNetCore.Identity;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace BNKaraoke.Api.Models;

public class ApplicationUser : IdentityUser
{
    [Required]
    [Phone]
    public override string? PhoneNumber { get; set; } = string.Empty;

    [Required]
    [Column(TypeName = "nvarchar(100)")]
    public string FirstName { get; set; } = string.Empty;

    [Required]
    [Column(TypeName = "nvarchar(100)")]
    public string LastName { get; set; } = string.Empty;
}

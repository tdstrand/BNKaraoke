using Microsoft.AspNetCore.Identity;
using System.ComponentModel.DataAnnotations;

namespace BNKaraoke.Api.Models;

public class ApplicationUser : IdentityUser
{
    [Required]
    [Phone]
    public override string? UserName
    {
        get => PhoneNumber;
        set => PhoneNumber = value;
    }

    [Required]
    [Phone]
    public override string? PhoneNumber { get; set; } = string.Empty;

    // Additional properties
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
}
using System.ComponentModel.DataAnnotations;

namespace BNKaraoke.Api.Models;

public class UserViewModel
{
    public string Id { get; set; } = string.Empty;

    [Required]
    [Display(Name = "Username")]
    public string UserName { get; set; } = string.Empty;

    [Required]
    [EmailAddress]
    [Display(Name = "Email")]
    public string Email { get; set; } = string.Empty;

    [Display(Name = "First Name")]
    public string FirstName { get; set; } = string.Empty;

    [Display(Name = "Last Name")]
    public string LastName { get; set; } = string.Empty;

    public bool EmailConfirmed { get; set; }
}
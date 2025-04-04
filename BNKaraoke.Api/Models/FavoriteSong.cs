using System.ComponentModel.DataAnnotations;

namespace BNKaraoke.Api.Models;

public class FavoriteSong
{
    public int Id { get; set; }

    [Required]
    public string SingerId { get; set; } = string.Empty;

    public int SongId { get; set; }
}
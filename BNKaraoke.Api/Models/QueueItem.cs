using System.ComponentModel.DataAnnotations;

namespace BNKaraoke.Api.Models;

public class QueueItem
{
    public int Id { get; set; }

    [Required]
    public string SingerId { get; set; } = string.Empty;

    public int SongId { get; set; }
    public DateTime RequestTime { get; set; }
}
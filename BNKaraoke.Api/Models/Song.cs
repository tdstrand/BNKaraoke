namespace BNKaraoke.Api.Models;

public class Song
{
    public int Id { get; set; }
    public required string Title { get; set; }
    public required string Artist { get; set; }
    public required string SpotifyId { get; set; }
    public string? YouTubeUrl { get; set; }
    public required string Status { get; set; } = "pending";
    public string? Genre { get; set; }
    public float Bpm { get; set; }
    public float Energy { get; set; }
    public float Valence { get; set; }
    public float Danceability { get; set; }
    public int Popularity { get; set; }
    public DateTime RequestDate { get; set; }
    public required string RequestedBy { get; set; }
    public string? ApprovedBy { get; set; }
}
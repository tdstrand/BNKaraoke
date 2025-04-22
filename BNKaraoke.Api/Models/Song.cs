namespace BNKaraoke.Api.Models
{
    public class Song
    {
        public int Id { get; set; }
        public string Title { get; set; } = string.Empty;
        public string Artist { get; set; } = string.Empty;
        public string? SpotifyId { get; set; }
        public string? MusicBrainzId { get; set; } // New field for MusicBrainz ID
        public string? Decade { get; set; }
        public string? Genre { get; set; }
        public int? Popularity { get; set; }
        public float? Bpm { get; set; } // New field for BPM
        public string? Danceability { get; set; } // New field for danceability (e.g., "danceable")
        public string? Energy { get; set; } // New field for energy (e.g., "aggressive")
        public string? Mood { get; set; } // New field for mood (e.g., "happy")
        public int? LastFmPlaycount { get; set; } // New field for Last.fm playcount
        public string? YouTubeUrl { get; set; }
        public string Status { get; set; } = "pending";
        public DateTime? RequestDate { get; set; }
        public string RequestedBy { get; set; } = string.Empty;
        public string? ApprovedBy { get; set; }
        public int? Valence { get; set; } // Integer to match database field
    }
}
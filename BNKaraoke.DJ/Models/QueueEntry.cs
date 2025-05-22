using System;

namespace BNKaraoke.DJ.Models
{
    public class QueueEntry
    {
        public string? QueueId { get; set; }
        public int SongId { get; set; }
        public string? SongTitle { get; set; }
        public string? SongArtist { get; set; }
        public string? RequestorDisplayName { get; set; }
        public string? VideoLength { get; set; }
        public int Position { get; set; }
        public string? Status { get; set; }
        public string? RequestorUserName { get; set; }
        public DateTime? SungAt { get; set; }
        public string? Genre { get; set; }
        public string? Decade { get; set; }
        public string? YouTubeUrl { get; set; }
        public bool IsVideoCached { get; set; }
    }
}
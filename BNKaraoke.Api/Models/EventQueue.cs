namespace BNKaraoke.Api.Models
{
    public class EventQueue
    {
        public int QueueId { get; set; }
        public int EventId { get; set; }
        public int SongId { get; set; }
        public required string RequestorUserName { get; set; } // Username, required
        public required string Singers { get; set; } // JSON-serialized list of singers, required
        public int Position { get; set; }
        public required string Status { get; set; } // Status, required
        public bool IsActive { get; set; }
        public bool WasSkipped { get; set; }
        public bool IsCurrentlyPlaying { get; set; }
        public DateTime? SungAt { get; set; } // Changed to DateTime? to match database schema
        public bool IsOnBreak { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }

        // Navigation properties (nullable, as they are optional)
        public Event? Event { get; set; }
        public Song? Song { get; set; }
        public ApplicationUser? Requestor { get; set; }
    }
}
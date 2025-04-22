namespace BNKaraoke.Api.Models
{
    public class EventQueue
    {
        public int QueueId { get; set; }
        public int EventId { get; set; }
        public int SongId { get; set; }
        public string SingerId { get; set; } = string.Empty; // Matches AspNetUsers.Id (text)
        public int Position { get; set; }
        public string Status { get; set; } = "Upcoming"; // Upcoming, Live, Archived
        public bool IsActive { get; set; } = false;
        public bool WasSkipped { get; set; } = false;
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
        public bool IsCurrentlyPlaying { get; set; } = false;
        public DateTime? SungAt { get; set; }

        // Navigation properties
        public Event Event { get; set; } = null!;
        public Song Song { get; set; } = null!;
        public ApplicationUser Singer { get; set; } = null!;
    }
}
namespace BNKaraoke.Api.Dtos
{
    public class EventQueueDto
    {
        public int QueueId { get; set; }
        public int EventId { get; set; }
        public int SongId { get; set; }
        public Song Song { get; set; } = new Song();
        public string RequestorUserName { get; set; } = string.Empty;
        public string RequestorDisplayName { get; set; } = string.Empty;
        public List<string> Singers { get; set; } = new List<string>();
        public int Position { get; set; }
        public string Status { get; set; } = string.Empty;
        public bool IsActive { get; set; }
        public bool WasSkipped { get; set; }
        public bool IsCurrentlyPlaying { get; set; }
        public DateTime? SungAt { get; set; }
        public bool IsOnBreak { get; set; }
    }

    public class Song
    {
        public int Id { get; set; }
        public string Title { get; set; } = "Unknown Song";
        public string Artist { get; set; } = "Unknown Artist";
    }
}
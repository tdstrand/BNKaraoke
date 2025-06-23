using System.Collections.Generic;
using System;

namespace BNKaraoke.DJ.Models
{
    public class EventQueueDto
    {
        public int QueueId { get; set; }
        public int EventId { get; set; }
        public int SongId { get; set; }
        public string RequestorUserName { get; set; } = string.Empty;
        public string RequestorDisplayName { get; set; } = string.Empty; // Added to store the fetched display name
        public List<string> Singers { get; set; } = new List<string>();
        public int Position { get; set; }
        public string Status { get; set; } = string.Empty;
        public bool IsActive { get; set; }
        public bool WasSkipped { get; set; }
        public bool IsCurrentlyPlaying { get; set; }
        public DateTime? SungAt { get; set; }
        public bool IsOnBreak { get; set; }
        public Song Song { get; set; } = new Song(); // Nested Song object
    }

    
}
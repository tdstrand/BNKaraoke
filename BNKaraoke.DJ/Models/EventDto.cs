namespace BNKaraoke.DJ.Models
{
    public class EventDto
    {
        public int EventId { get; set; }
        public string? EventCode { get; set; } // Made nullable
        public string? Description { get; set; } // Made nullable
        public string? Status { get; set; } // Made nullable
        public string? Visibility { get; set; } // Made nullable
        public string? Location { get; set; } // Made nullable
        public string? ScheduledDate { get; set; } // Made nullable
        public string? ScheduledStartTime { get; set; } // Made nullable
        public string? ScheduledEndTime { get; set; } // Made nullable
        public string? KaraokeDJName { get; set; } // Made nullable
        public bool IsCanceled { get; set; }
        public int RequestLimit { get; set; }
        public int QueueCount { get; set; }
    }
}
namespace BNKaraoke.Api.Dtos
{
    public class EventDto
    {
        public int EventId { get; set; }
        public string EventCode { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public string Visibility { get; set; } = string.Empty;
        public string Location { get; set; } = string.Empty;
        public DateTime ScheduledDate { get; set; }
        public TimeSpan? ScheduledStartTime { get; set; }
        public TimeSpan? ScheduledEndTime { get; set; }
        public string? KaraokeDJName { get; set; }
        public bool IsCanceled { get; set; }
        public int RequestLimit { get; set; }
        public int QueueCount { get; set; }
    }

    public class EventQueueCreateDto
    {
        public int SongId { get; set; }
        public string RequestorUserName { get; set; } = string.Empty;
    }

    public class AttendanceActionDto
    {
        public string RequestorId { get; set; } = string.Empty;
    }

    public class ReorderQueueRequest
    {
        public List<QueuePosition> NewOrder { get; set; } = new List<QueuePosition>();
    }

    public class QueuePosition
    {
        public int QueueId { get; set; }
        public int Position { get; set; }
    }

    public class UpdateSingersRequest
    {
        public List<string> Singers { get; set; } = new List<string>();
    }

    public class EventCreateDto
    {
        public string EventCode { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string? Status { get; set; }
        public string? Visibility { get; set; }
        public string Location { get; set; } = string.Empty;
        public DateTime ScheduledDate { get; set; }
        public TimeSpan? ScheduledStartTime { get; set; }
        public TimeSpan? ScheduledEndTime { get; set; }
        public string? KaraokeDJName { get; set; }
        public bool? IsCanceled { get; set; }
        public int RequestLimit { get; set; } = 15;
    }

    public class EventUpdateDto
    {
        public string EventCode { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string? Status { get; set; }
        public string? Visibility { get; set; }
        public string Location { get; set; } = string.Empty;
        public DateTime ScheduledDate { get; set; }
        public TimeSpan? ScheduledStartTime { get; set; }
        public TimeSpan? ScheduledEndTime { get; set; }
        public string? KaraokeDJName { get; set; }
        public bool? IsCanceled { get; set; }
        public int RequestLimit { get; set; } = 15;
    }
}
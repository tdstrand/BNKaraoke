namespace BNKaraoke.Api.Dtos
{
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
    }
}
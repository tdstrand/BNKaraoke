namespace BNKaraoke.Api.Models
{
    public class EventAttendance
    {
        public int AttendanceId { get; set; }
        public int EventId { get; set; }
        public string SingerId { get; set; } = string.Empty; // Matches AspNetUsers.Id (text)
        public bool IsCheckedIn { get; set; } = false;
        public bool IsOnBreak { get; set; } = false;
        public DateTime? BreakStartAt { get; set; }
        public DateTime? BreakEndAt { get; set; }

        // Navigation properties
        public Event Event { get; set; } = null!;
        public ApplicationUser Singer { get; set; } = null!;
        public List<EventAttendanceHistory> AttendanceHistories { get; set; } = new List<EventAttendanceHistory>();
    }
}
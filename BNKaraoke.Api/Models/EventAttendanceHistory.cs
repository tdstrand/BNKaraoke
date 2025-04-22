namespace BNKaraoke.Api.Models
{
    public class EventAttendanceHistory
    {
        public int HistoryId { get; set; }
        public int EventId { get; set; }
        public string SingerId { get; set; } = string.Empty; // Matches AspNetUsers.Id (text)
        public string Action { get; set; } = string.Empty; // CheckIn, CheckOut
        public DateTime ActionTimestamp { get; set; }
        public int? AttendanceId { get; set; }

        // Navigation properties
        public Event Event { get; set; } = null!;
        public ApplicationUser Singer { get; set; } = null!;
        public EventAttendance? Attendance { get; set; }
    }
}
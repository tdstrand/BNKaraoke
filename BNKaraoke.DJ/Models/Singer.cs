namespace BNKaraoke.DJ.Models
{
    public class Singer
    {
        public string? UserId { get; set; } // Nullable to fix CS8618
        public string? DisplayName { get; set; } // Nullable to fix CS8618
        public bool IsLoggedIn { get; set; }
        public bool IsJoined { get; set; }
        public bool IsOnBreak { get; set; }
    }
}
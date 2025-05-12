namespace BNKaraoke.DJ.Models
{
    public class Song
    {
        public int Id { get; set; }
        public string Title { get; set; } = string.Empty;
        public string Artist { get; set; } = string.Empty;
        // Add additional properties (e.g., PerformerName) as required.
    }
}
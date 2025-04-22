namespace BNKaraoke.Api.Dtos
{
    public class EventQueueCreateDto
    {
        public int SongId { get; set; }
        public string SingerId { get; set; } = string.Empty;
    }
}
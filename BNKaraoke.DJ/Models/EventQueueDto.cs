// File: C:\Users\tstra\source\repos\BNKaraoke\BNKaraoke.DJ\Models\EventQueueDto.cs
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

    public class Song
    {
        public int Id { get; set; }
        public string Title { get; set; } = string.Empty;
        public string Artist { get; set; } = string.Empty;
        public string? Genre { get; set; }
        public string? Decade { get; set; }
        public string Status { get; set; } = string.Empty;
        public float? Bpm { get; set; }
        public float? Danceability { get; set; }
        public float? Energy { get; set; }
        public string? Mood { get; set; }
        public int? Popularity { get; set; }
        public string? SpotifyId { get; set; }
        public string? YouTubeUrl { get; set; }
        public string? MusicBrainzId { get; set; }
        public int? LastFmPlaycount { get; set; }
        public int? Valence { get; set; }
    }
}
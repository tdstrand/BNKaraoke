using Microsoft.AspNetCore.Mvc;
using BNKaraoke.Api.Data; // Ensure this matches your DbContext namespace
using BNKaraoke.Api.Models;

namespace BNKaraoke.Api.Controllers
{
    [Route("api/songs")]
    [ApiController]
    public class SongsController : ControllerBase
    {
        private readonly ApplicationDbContext _context; // Changed to ApplicationDbContext
        public SongsController(ApplicationDbContext context) => _context = context;

        [HttpGet("search")]
        public IActionResult Search(string query)
        {
            var songs = _context.Songs
                .Where(s => s.Status == "active" && (s.Title.Contains(query) || s.Artist.Contains(query)))
                .ToList();
            return Ok(songs);
        }

        [HttpPost("request")]
        public IActionResult RequestSong([FromBody] Song song)
        {
            song.Status = "pending";
            song.RequestDate = DateTime.UtcNow;
            song.RequestedBy = "user1"; // Replace with auth later
            _context.Songs.Add(song);
            _context.SaveChanges();
            return Ok(new { message = "Song added to the party queue!" });
        }

        [HttpGet("pending")]
        public IActionResult GetPending()
        {
            var pending = _context.Songs.Where(s => s.Status == "pending").ToList();
            return Ok(pending);
        }

        [HttpPost("approve")]
        public IActionResult ApproveSong(int id, [FromBody] string youtubeUrl)
        {
            var song = _context.Songs.Find(id);
            if (song == null) return NotFound();
            song.YouTubeUrl = youtubeUrl;
            song.Status = "active";
            song.ApprovedBy = "admin1"; // Replace with auth
            _context.SaveChanges();
            return Ok(new { message = "Party hit approved!" });
        }
    }
}
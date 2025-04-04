using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using BNKaraoke.Api.Models;

namespace BNKaraoke.Api.Data;

public class ApplicationDbContext : IdentityDbContext<ApplicationUser>
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
        : base(options)
    {
    }

    public DbSet<Song> Songs { get; set; }
    public DbSet<Event> Events { get; set; }
    public DbSet<QueueItem> QueueItems { get; set; }
    public DbSet<FavoriteSong> FavoriteSongs { get; set; }
}
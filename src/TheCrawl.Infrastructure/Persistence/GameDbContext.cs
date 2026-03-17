using Microsoft.EntityFrameworkCore;
using TheCrawl.Domain.Entities;

namespace TheCrawl.Infrastructure.Persistence;

public class GameDbContext(DbContextOptions<GameDbContext> options) : DbContext(options)
{
    public DbSet<GameSession> GameSessions => Set<GameSession>();
    public DbSet<Player> Players => Set<Player>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Player>(b =>
        {
            b.HasKey(p => p.Id);
            b.Property(p => p.Class).HasConversion<string>();
            b.OwnsOne(p => p.BaseStats);
            b.OwnsOne(p => p.Position);
        });

        modelBuilder.Entity<GameSession>(b =>
        {
            b.HasKey(s => s.Id);
            b.HasOne(s => s.Player).WithMany().HasForeignKey("PlayerId");
            b.Property(s => s.Status).HasConversion<string>();
            b.Ignore(s => s.CurrentFloor); // Floor is complex; lives in memory only during active sessions
            b.Ignore(s => s.EventLog);    // In-memory audit trail; persisted separately as SessionEvent if needed later
        });
    }
}

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
            b.OwnsOne(p => p.BaseStats);
            b.OwnsOne(p => p.Position);
        });

        modelBuilder.Entity<GameSession>(b =>
        {
            b.HasKey(s => s.Id);
            b.HasOne(s => s.Player).WithMany().HasForeignKey("PlayerId");
            b.Property(s => s.Status).HasConversion<string>();
            b.Property(s => s.EventLog)
                .HasConversion(
                    v => string.Join('\n', v),
                    v => v.Split('\n', StringSplitOptions.RemoveEmptyEntries).ToList());
        });
    }
}

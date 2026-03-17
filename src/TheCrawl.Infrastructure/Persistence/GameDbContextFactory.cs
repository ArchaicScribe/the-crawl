using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace TheCrawl.Infrastructure.Persistence;

/// <summary>
/// Provides a DbContext instance for EF Core design-time tooling (migrations, scaffolding).
/// Not used at runtime — only invoked by dotnet-ef CLI commands.
/// </summary>
public class GameDbContextFactory : IDesignTimeDbContextFactory<GameDbContext>
{
    public GameDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<GameDbContext>()
            .UseNpgsql("Host=localhost;Port=5432;Database=thecrawl;Username=crawl;Password=crawl")
            .Options;

        return new GameDbContext(options);
    }
}

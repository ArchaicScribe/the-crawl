using Microsoft.EntityFrameworkCore;
using TheCrawl.Domain.Entities;
using TheCrawl.Domain.Enums;
using TheCrawl.Domain.Interfaces;
using TheCrawl.Infrastructure.Persistence;

namespace TheCrawl.Infrastructure.Repositories;

public class GameSessionRepository(GameDbContext db) : IGameSessionRepository
{
    public async Task<GameSession?> GetByIdAsync(Guid sessionId, CancellationToken ct = default) =>
        await db.GameSessions
            .Include(s => s.Player)
            .FirstOrDefaultAsync(s => s.Id == sessionId, ct);

    public async Task<IEnumerable<GameSession>> GetLeaderboardAsync(int top = 10, CancellationToken ct = default) =>
        await db.GameSessions
            .Include(s => s.Player)
            .Where(s => s.Status == GameStatus.Dead || s.Status == GameStatus.Escaped)
            .OrderByDescending(s => s.Player.BroadcastScore)
            .Take(top)
            .ToListAsync(ct);

    public async Task SaveAsync(GameSession session, CancellationToken ct = default)
    {
        var existing = await db.GameSessions.FindAsync([session.Id], ct);
        if (existing is null)
            db.GameSessions.Add(session);
        else
            db.Entry(existing).CurrentValues.SetValues(session);

        await db.SaveChangesAsync(ct);
    }
}

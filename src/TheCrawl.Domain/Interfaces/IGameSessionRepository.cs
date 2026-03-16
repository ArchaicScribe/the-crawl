using TheCrawl.Domain.Entities;

namespace TheCrawl.Domain.Interfaces;

public interface IGameSessionRepository
{
    Task<GameSession?> GetByIdAsync(Guid sessionId, CancellationToken ct = default);
    Task<IEnumerable<GameSession>> GetLeaderboardAsync(int top = 10, CancellationToken ct = default);
    Task SaveAsync(GameSession session, CancellationToken ct = default);
}

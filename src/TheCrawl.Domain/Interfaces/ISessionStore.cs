using TheCrawl.Domain.Entities;

namespace TheCrawl.Domain.Interfaces;

public interface ISessionStore
{
    Task<GameSession?> GetAsync(Guid sessionId, CancellationToken ct = default);
    Task SaveAsync(GameSession session, CancellationToken ct = default);
    Task RemoveAsync(Guid sessionId, CancellationToken ct = default);
}

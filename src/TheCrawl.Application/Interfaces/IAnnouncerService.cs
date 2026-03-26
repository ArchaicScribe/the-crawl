namespace TheCrawl.Application.Interfaces;

public interface IAnnouncerService
{
    Task<string> OnSessionStartAsync(Guid sessionId, string playerName, string playerClass, CancellationToken ct = default);
    Task<string> OnKillAsync(Guid sessionId, string playerName, string enemyName, int killCount, CancellationToken ct = default);
    Task<string> OnPlayerDamagedAsync(Guid sessionId, string playerName, int damage, int remainingHp, CancellationToken ct = default);
    Task<string> OnDeathAsync(Guid sessionId, string playerName, int floorsCleared, int killCount, CancellationToken ct = default);
    Task<string> OnFloorDescendAsync(Guid sessionId, string playerName, int newFloor, CancellationToken ct = default);
    Task<string> OnItemPickupAsync(Guid sessionId, string playerName, string itemName, CancellationToken ct = default);
    Task<string> OnObjectionSucceedsAsync(Guid sessionId, string enemyName, CancellationToken ct = default);
    Task<string> OnLevelUpAsync(Guid sessionId, string playerName, int newLevel, CancellationToken ct = default);
    Task<string> OnAbilityUsedAsync(Guid sessionId, string playerName, string abilityName, string? targetName, CancellationToken ct = default);
}

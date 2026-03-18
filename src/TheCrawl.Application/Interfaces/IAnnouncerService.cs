namespace TheCrawl.Application.Interfaces;

public interface IAnnouncerService
{
    Task<string> OnSessionStartAsync(string playerName, string playerClass, CancellationToken ct = default);
    Task<string> OnKillAsync(string playerName, string enemyName, int killCount, CancellationToken ct = default);
    Task<string> OnPlayerDamagedAsync(string playerName, int damage, int remainingHp, CancellationToken ct = default);
    Task<string> OnDeathAsync(string playerName, int floorsCleared, int killCount, CancellationToken ct = default);
    Task<string> OnFloorDescendAsync(string playerName, int newFloor, CancellationToken ct = default);
    Task<string> OnItemPickupAsync(string playerName, string itemName, CancellationToken ct = default);
    Task<string> OnObjectionSucceedsAsync(string enemyName, CancellationToken ct = default);
}

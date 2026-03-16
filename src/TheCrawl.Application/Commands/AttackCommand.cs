namespace TheCrawl.Application.Commands;

public record AttackCommand(Guid SessionId);
public record AttackResult(bool Hit, int Damage, string Message, bool EnemyDied, string? AnnouncerMessage = null);

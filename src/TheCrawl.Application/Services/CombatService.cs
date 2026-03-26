using TheCrawl.Application.Commands;
using TheCrawl.Application.Interfaces;
using TheCrawl.Domain.Entities;
using TheCrawl.Domain.Enums;

namespace TheCrawl.Application.Services;

public class CombatService(IAnnouncerService announcer)
{
    private readonly Random _rng = new();

    public async Task<AttackResult> ResolvePlayerAttackAsync(GameSession session, CancellationToken ct = default)
    {
        var player = session.Player;
        var floor = session.CurrentFloor;

        var enemy = floor.Enemies
            .Where(e => e.IsAlive)
            .OrderBy(e => e.Position.DistanceTo(player.Position))
            .FirstOrDefault();

        if (enemy is null)
            return new AttackResult(false, 0, "No enemies in range.", false);

        var dodgeRoll = _rng.Next(0, 100);
        var effectiveDodge = Math.Max(0, enemy.DodgeChance - player.BaseStats.Wit);
        if (dodgeRoll < effectiveDodge)
            return new AttackResult(false, 0, $"{enemy.Name} sidesteps your attack.", false);

        var damage = player.RollWeaponDamage(_rng) + _rng.Next(-1, 3);
        var dealt = enemy.TakeDamage(Math.Max(1, damage));

        if (!enemy.IsAlive)
        {
            player.RegisterKill();
            player.AddRatings(enemy.RatingsOnKill);
            var levelsGained = player.AwardXp(enemy.XpOnKill);
            session.LogEvent($"Killed {enemy.Name}. +{enemy.RatingsOnKill} RATINGS. +{enemy.XpOnKill} XP.");
            if (levelsGained > 0) session.LogEvent($"Level up! Now level {player.Level}.");
            var killMessage = levelsGained > 0
                ? await announcer.OnLevelUpAsync(session.Id, player.Name, player.Level, ct)
                : await announcer.OnKillAsync(session.Id, player.Name, enemy.Name, player.KillCount, ct);
            return new AttackResult(true, dealt, $"{enemy.Name} is down.", true, killMessage);
        }

        var enemyDamage = await ResolveEnemyAttackAsync(session, enemy, ct);
        session.LogEvent($"Hit {enemy.Name} for {dealt}. Took {enemyDamage} in return.");

        string? deathMessage = null;
        if (!player.IsAlive)
        {
            session.EndSession(GameStatus.Dead);
            deathMessage = await announcer.OnDeathAsync(session.Id, player.Name, player.FloorsCleared, player.KillCount, ct);
        }

        return new AttackResult(true, dealt,
            $"Hit {enemy.Name} for {dealt}. {enemy.Name} hits back for {enemyDamage}.",
            false, deathMessage);
    }

    private async Task<int> ResolveEnemyAttackAsync(GameSession session, Enemy enemy, CancellationToken ct)
    {
        var player = session.Player;
        if (_rng.Next(0, 100) < player.BaseStats.Nerve)
            return 0;

        var damage = enemy.Damage + _rng.Next(-1, 2);
        var actual = player.TakeDamage(Math.Max(1, damage));
        if (actual > 0)
        {
            var msg = await announcer.OnPlayerDamagedAsync(session.Id, player.Name, actual, player.CurrentHp, ct);
            session.LogEvent(msg);
        }
        return actual;
    }
}

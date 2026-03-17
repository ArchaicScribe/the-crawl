using TheCrawl.Application.Commands;
using TheCrawl.Application.Interfaces;
using TheCrawl.Domain.Entities;

namespace TheCrawl.Application.Services;

public class CombatService(IAnnouncerService announcer)
{
    private readonly Random _rng = new();

    public async Task<AttackResult> AttackAsync(AttackCommand command, CancellationToken ct = default)
    {
        // Locate session from repository for persistence — active sessions handled by GameService
        // This operates on a passed-in session for testability
        throw new NotImplementedException("Wire session lookup via GameService.");
    }

    public AttackResult ResolvePlayerAttack(GameSession session)
    {
        var player = session.Player;
        var floor = session.CurrentFloor;

        var enemy = floor.Enemies
            .Where(e => e.IsAlive)
            .OrderBy(e => e.Position.DistanceTo(player.Position))
            .FirstOrDefault();

        if (enemy is null)
            return new AttackResult(false, 0, "No enemies in range.", false);

        // Dodge check: enemy's dodge chance vs player WIT modifier
        var dodgeRoll = _rng.Next(0, 100);
        var effectiveDodge = Math.Max(0, enemy.DodgeChance - player.BaseStats.Wit);
        if (dodgeRoll < effectiveDodge)
            return new AttackResult(false, 0, $"{enemy.Name} sidesteps your attack.", false);

        // Damage: MUSCLE + variance
        var baseDamage = player.BaseStats.Muscle;
        var damage = baseDamage + _rng.Next(-1, 3);
        var dealt = enemy.TakeDamage(Math.Max(1, damage));

        if (!enemy.IsAlive)
        {
            player.RegisterKill();
            var announcerMessage = announcer.OnKill(player.Name, enemy.Name, player.KillCount);
            session.LogEvent($"Killed {enemy.Name}. +{enemy.RatingsOnKill} RATINGS.");
            return new AttackResult(true, dealt, $"{enemy.Name} is down.", true, announcerMessage);
        }

        // Enemy counter-attack
        var enemyDamage = ResolveEnemyAttack(session, enemy);
        session.LogEvent($"Hit {enemy.Name} for {dealt}. Took {enemyDamage} in return.");

        string? deathMessage = null;
        if (!player.IsAlive)
        {
            session.EndSession(Domain.Enums.GameStatus.Dead);
            deathMessage = announcer.OnDeath(player.Name, player.FloorsCleared, player.KillCount);
        }

        return new AttackResult(true, dealt,
            $"Hit {enemy.Name} for {dealt}. {enemy.Name} hits back for {enemyDamage}.",
            false, deathMessage);
    }

    private int ResolveEnemyAttack(GameSession session, Enemy enemy)
    {
        var player = session.Player;
        var dodgeRoll = _rng.Next(0, 100);
        var playerDodge = player.BaseStats.Nerve;
        if (dodgeRoll < playerDodge)
            return 0;

        var damage = enemy.Damage + _rng.Next(-1, 2);
        var actual = player.TakeDamage(Math.Max(1, damage));
        if (actual > 0)
            session.LogEvent(announcer.OnPlayerDamaged(player.Name, actual, player.CurrentHp));
        return actual;
    }
}

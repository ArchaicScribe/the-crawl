using TheCrawl.Application.Interfaces;
using TheCrawl.Domain.Entities;
using TheCrawl.Domain.Enums;
using TheCrawl.Domain.Interfaces;
using TheCrawl.Domain.ValueObjects;

namespace TheCrawl.Application.Services;

/// <summary>
/// Processes one turn for every living enemy on the current floor.
/// Called after every successful player action (move, attack).
///
/// Behavior:
///   Chase  — enemy spotted the player (or remembers seeing them).
///            A* toward last known position. Attacks if adjacent.
///   Wander — random walkable adjacent tile. Ignores player.
///
/// Awareness: enemies enter Chase when the player is in their FOV tile set
/// (proxy: player's VisibleTiles contains enemy position — mutual sight).
/// They chase for AwarenessMemory turns after losing sight before forgetting.
/// </summary>
public class EnemyTurnService(IPathfinder pathfinder, IAnnouncerService announcer)
{
    private static readonly Random Rng = new();

    /// <summary>
    /// Runs all enemy turns. Returns event strings to be logged, plus optional
    /// announcer commentary if the player was hit.
    /// </summary>
    public async Task<EnemyTurnResult> ProcessTurnsAsync(GameSession session, CancellationToken ct = default)
    {
        var floor = session.CurrentFloor;
        var player = session.Player;
        var events = new List<string>();
        string? announcerMessage = null;

        // Snapshot occupied positions to avoid enemies stacking
        var occupiedByEnemies = floor.Enemies
            .Where(e => e.IsAlive)
            .Select(e => e.Position)
            .ToHashSet();

        foreach (var enemy in floor.Enemies.Where(e => e.IsAlive))
        {
            var playerVisible = floor.IsVisible(enemy.Position);

            // Update awareness
            if (playerVisible)
                enemy.SpotPlayer(player.Position);
            else
                enemy.DecrementAwareness();

            if (enemy.Behavior == EnemyBehavior.Chase && enemy.LastKnownPlayerPos is not null)
            {
                var target = enemy.LastKnownPlayerPos;

                // Attack if already adjacent
                if (IsAdjacent(enemy.Position, player.Position))
                {
                    var (dmg, msg, announcerMsg) = await ResolveEnemyAttackAsync(session, enemy, ct);
                    events.Add(msg);
                    if (announcerMsg is not null) announcerMessage = announcerMsg;
                    continue;
                }

                // Pathfind toward last known player pos (or current pos if visible)
                occupiedByEnemies.Remove(enemy.Position);
                var step = pathfinder.NextStep(enemy.Position, target, floor, occupiedByEnemies);
                if (step is not null && step != player.Position)
                {
                    enemy.MoveTo(step);
                    occupiedByEnemies.Add(step);
                }
                else
                {
                    occupiedByEnemies.Add(enemy.Position);
                }

                // Attack after moving if now adjacent
                if (IsAdjacent(enemy.Position, player.Position))
                {
                    var (dmg, msg, announcerMsg) = await ResolveEnemyAttackAsync(session, enemy, ct);
                    events.Add(msg);
                    if (announcerMsg is not null) announcerMessage = announcerMsg;
                }
            }
            else
            {
                // Wander: pick a random walkable neighbour that isn't already occupied
                occupiedByEnemies.Remove(enemy.Position);
                var options = enemy.Position.CardinalNeighbors()
                    .Where(p => floor.IsWalkable(p) && !occupiedByEnemies.Contains(p) && p != player.Position)
                    .ToList();

                if (options.Count > 0)
                {
                    var dest = options[Rng.Next(options.Count)];
                    enemy.MoveTo(dest);
                    occupiedByEnemies.Add(dest);
                }
                else
                {
                    occupiedByEnemies.Add(enemy.Position);
                }
            }
        }

        return new EnemyTurnResult(events, announcerMessage, !player.IsAlive);
    }

    private async Task<(int Damage, string LogMessage, string? AnnouncerMessage)> ResolveEnemyAttackAsync(
        GameSession session, Enemy enemy, CancellationToken ct)
    {
        var player = session.Player;

        // NERVE stat gives dodge chance vs enemy attacks
        if (Rng.Next(0, 100) < player.BaseStats.Nerve)
            return (0, $"{enemy.Name} lunges — you sidestep it.", null);

        var damage = Math.Max(1, enemy.Damage + Rng.Next(-1, 2));
        player.TakeDamage(damage);

        string? announcerMsg = null;
        string logMsg;

        if (!player.IsAlive)
        {
            session.EndSession(GameStatus.Dead);
            announcerMsg = await announcer.OnDeathAsync(player.Name, player.FloorsCleared, player.KillCount, ct);
            logMsg = $"{enemy.Name} kills {player.Name}. The broadcast has its moment.";
        }
        else
        {
            announcerMsg = await announcer.OnPlayerDamagedAsync(player.Name, damage, player.CurrentHp, ct);
            logMsg = $"{enemy.Name} hits for {damage}. HP: {player.CurrentHp}/{player.MaxHp}.";
        }

        return (damage, logMsg, announcerMsg);
    }

    private static bool IsAdjacent(Position a, Position b) =>
        Math.Abs(a.X - b.X) + Math.Abs(a.Y - b.Y) == 1;
}

public record EnemyTurnResult(List<string> Events, string? AnnouncerMessage, bool PlayerDied);

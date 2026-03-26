using TheCrawl.Application.Commands;
using TheCrawl.Application.Interfaces;
using TheCrawl.Domain.Entities;
using TheCrawl.Domain.Enums;
using TheCrawl.Domain.Interfaces;

namespace TheCrawl.Application.Services;

/// <summary>
/// Resolves class-specific ability use for the current player.
///
/// One ability per class, gated by AbilityCooldown on the Player.
/// Cooldown ticks down once per enemy turn cycle (i.e., once per player action).
///
/// Objection  (Lawyer)       — Wit check vs enemy dodge → forces Wander. 5-turn CD.
/// Charm      (Influencer)   — Always succeeds → forces nearest enemy to Wander. 4-turn CD.
/// Audit      (Accountant)   — Wit × 2 forced damage, no dodge roll. 3-turn CD.
/// Field Repair (Electrician)— Restores weapon durability by Nerve × 3. 5-turn CD.
/// Suppress   (Veteran)      — Forces Wander + Muscle/2 damage. 3-turn CD.
/// Fumigate   (Exterminator) — Muscle/2 damage to every alive enemy. 6-turn CD.
/// </summary>
public class AbilityService(ISessionStore sessionStore, IAnnouncerService announcer)
{
    private static readonly Random Rng = new();

    public async Task<UseAbilityResult> UseAbilityAsync(UseAbilityCommand command, CancellationToken ct = default)
    {
        var session = await sessionStore.GetAsync(command.SessionId, ct);
        if (session is null || !session.IsActive)
            return new UseAbilityResult(false, "Session not found or already ended.");

        var player = session.Player;
        if (!player.CanUseAbility)
            return new UseAbilityResult(false, $"Ability on cooldown — {player.AbilityCooldown} turns remaining.");

        var (success, message, announcerMsg) = player.Class switch
        {
            PlayerClass.Lawyer       => await UseObjectionAsync(session, ct),
            PlayerClass.Influencer   => await UseCharmAsync(session, ct),
            PlayerClass.Accountant   => await UseAuditAsync(session, ct),
            PlayerClass.Electrician  => await UseFieldRepairAsync(session, ct),
            PlayerClass.Veteran      => await UseSuppressAsync(session, ct),
            PlayerClass.Exterminator => await UseFumigateAsync(session, ct),
            _ => (false, "No ability defined for this class.", (string?)null)
        };

        if (success)
        {
            session.LogEvent(message);
            await sessionStore.SaveAsync(session, ct);
        }

        return new UseAbilityResult(success, message, announcerMsg);
    }

    // -------------------------------------------------------------------------
    // Per-class ability implementations
    // -------------------------------------------------------------------------

    /// <summary>
    /// Lawyer — Objection: Wit check vs nearest enemy dodge chance.
    /// Success: enemy forced to Wander, memory cleared.
    /// Failure: enemy is legally unimpressed.
    /// </summary>
    private async Task<(bool, string, string?)> UseObjectionAsync(GameSession session, CancellationToken ct)
    {
        var player = session.Player;
        var enemy  = NearestEnemy(session);
        if (enemy is null) return (false, "No enemies in range to file an Objection against.", null);

        var witBonus      = player.BaseStats.Wit * 3;
        var effectiveDodge = Math.Max(0, enemy.DodgeChance - witBonus);
        var roll           = Rng.Next(0, 100);

        if (roll < effectiveDodge)
        {
            var failMsg = $"Objection filed against {enemy.Name}. {enemy.Name} is not a party to this proceeding and does not care.";
            return (true, failMsg, await announcer.OnAbilityUsedAsync(session.Id, player.Name, "Objection", enemy.Name, ct));
        }

        enemy.SpotPlayer(player.Position); // keep them aware but…
        ForceWander(enemy);                // …immediately override to Wander
        player.StartAbilityCooldown(5);

        var msg = $"OBJECTION SUSTAINED. {enemy.Name} is legally compelled to stand down. Cooldown: 5 turns.";
        var announcerMsg = await announcer.OnObjectionSucceedsAsync(session.Id, enemy.Name, ct);
        return (true, msg, announcerMsg);
    }

    /// <summary>
    /// Influencer — Charm: always forces nearest enemy to Wander.
    /// No skill check. The parasocial contract is binding.
    /// </summary>
    private async Task<(bool, string, string?)> UseCharmAsync(GameSession session, CancellationToken ct)
    {
        var player = session.Player;
        var enemy  = NearestEnemy(session);
        if (enemy is null) return (false, "No enemies nearby to Charm.", null);

        ForceWander(enemy);
        player.StartAbilityCooldown(4);

        var msg = $"{enemy.Name} is momentarily charmed. They'll remember they hate you in 4 turns.";
        var announcerMsg = await announcer.OnAbilityUsedAsync(session.Id, player.Name, "Charm", enemy.Name, ct);
        return (true, msg, announcerMsg);
    }

    /// <summary>
    /// Accountant — Audit: deals Wit × 2 forced damage to nearest enemy.
    /// No dodge roll — audits are unavoidable.
    /// </summary>
    private async Task<(bool, string, string?)> UseAuditAsync(GameSession session, CancellationToken ct)
    {
        var player = session.Player;
        var enemy  = NearestEnemy(session);
        if (enemy is null) return (false, "No enemies to audit.", null);

        var damage = Math.Max(1, player.BaseStats.Wit * 2);
        enemy.TakeDamage(damage);
        player.StartAbilityCooldown(3);

        var deathNote = enemy.IsAlive ? string.Empty : $" {enemy.Name} could not withstand the scrutiny.";
        var msg = $"Audit complete. {enemy.Name} assessed for {damage} damage — no deductions available.{deathNote} Cooldown: 3 turns.";
        var announcerMsg = await announcer.OnAbilityUsedAsync(session.Id, player.Name, "Audit", enemy.Name, ct);
        return (true, msg, announcerMsg);
    }

    /// <summary>
    /// Electrician — Field Repair: restores equipped weapon durability by Nerve × 3.
    /// No target. Works in the field with available materials and electrical tape.
    /// </summary>
    private async Task<(bool, string, string?)> UseFieldRepairAsync(GameSession session, CancellationToken ct)
    {
        var player = session.Player;
        if (player.EquippedWeapon is null)
            return (false, "No weapon equipped to repair.", null);
        if (player.EquippedWeapon.CurrentDurability >= player.EquippedWeapon.MaxDurability)
            return (false, $"{player.EquippedWeapon.DisplayName} is already at full durability.", null);

        var repairAmount = Math.Max(1, player.BaseStats.Nerve * 3);
        player.EquippedWeapon.Repair(repairAmount);
        player.StartAbilityCooldown(5);

        var msg = $"Field Repair: {player.EquippedWeapon.DisplayName} restored by {repairAmount} durability " +
                  $"({player.EquippedWeapon.CurrentDurability}/{player.EquippedWeapon.MaxDurability}). Cooldown: 5 turns.";
        var announcerMsg = await announcer.OnAbilityUsedAsync(session.Id, player.Name, "Field Repair", null, ct);
        return (true, msg, announcerMsg);
    }

    /// <summary>
    /// Veteran — Suppress: forces nearest enemy to Wander AND deals Muscle/2 damage.
    /// Military discipline applied to pest management.
    /// </summary>
    private async Task<(bool, string, string?)> UseSuppressAsync(GameSession session, CancellationToken ct)
    {
        var player = session.Player;
        var enemy  = NearestEnemy(session);
        if (enemy is null) return (false, "No enemies to suppress.", null);

        var damage = Math.Max(1, player.BaseStats.Muscle / 2);
        enemy.TakeDamage(damage);
        if (enemy.IsAlive) ForceWander(enemy);
        player.StartAbilityCooldown(3);

        var deathNote = enemy.IsAlive ? string.Empty : $" {enemy.Name} did not survive the suppression.";
        var msg = $"Suppressed {enemy.Name} for {damage} damage.{deathNote} Cooldown: 3 turns.";
        var announcerMsg = await announcer.OnAbilityUsedAsync(session.Id, player.Name, "Suppress", enemy.Name, ct);
        return (true, msg, announcerMsg);
    }

    /// <summary>
    /// Exterminator — Fumigate: deals Muscle/2 damage to every alive enemy on the floor.
    /// Thirty years in pest control. This isn't that different.
    /// </summary>
    private async Task<(bool, string, string?)> UseFumigateAsync(GameSession session, CancellationToken ct)
    {
        var player  = session.Player;
        var targets = session.CurrentFloor.Enemies.Where(e => e.IsAlive).ToList();
        if (targets.Count == 0) return (false, "No enemies on the floor to fumigate.", null);

        var damage = Math.Max(1, player.BaseStats.Muscle / 2);
        var killed  = 0;
        foreach (var e in targets)
        {
            e.TakeDamage(damage);
            if (!e.IsAlive) killed++;
        }

        player.StartAbilityCooldown(6);

        var killNote = killed > 0 ? $" {killed} eliminated outright." : string.Empty;
        var msg = $"Fumigate: {damage} damage to all {targets.Count} enemies.{killNote} Cooldown: 6 turns.";
        var announcerMsg = await announcer.OnAbilityUsedAsync(session.Id, player.Name, "Fumigate", null, ct);
        return (true, msg, announcerMsg);
    }

    // -------------------------------------------------------------------------
    // Helpers
    // -------------------------------------------------------------------------

    /// <summary>Returns the nearest alive enemy by Chebyshev distance, or null.</summary>
    private static Enemy? NearestEnemy(GameSession session)
    {
        var pos = session.Player.Position;
        return session.CurrentFloor.Enemies
            .Where(e => e.IsAlive)
            .OrderBy(e => Math.Max(Math.Abs(e.Position.X - pos.X), Math.Abs(e.Position.Y - pos.Y)))
            .FirstOrDefault();
    }

    /// <summary>Immediately switches an enemy to Wander and clears its last known player position.</summary>
    private static void ForceWander(Enemy enemy) => enemy.ForceWander();
}

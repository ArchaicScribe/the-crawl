using TheCrawl.Application.Interfaces;

namespace TheCrawl.Infrastructure.Generators;

/// <summary>
/// Static fallback announcer used when VERA (Claude API) is unavailable.
/// Session ID is accepted but not used — responses are stateless.
/// </summary>
public class AnnouncerService : IAnnouncerService
{
    private static readonly Random _rng = new();

    public Task<string> OnSessionStartAsync(Guid sessionId, string playerName, string playerClass, CancellationToken ct = default) =>
        Task.FromResult(Pick([
            $"WELCOME TO THE CRAWL! Tonight's contestant: {playerName}, a {playerClass}. The odds are not in their favor. That's why we watch.",
            $"Broadcast starting NOW. {playerName} — classified as '{playerClass}' — enters the facility. Sponsors, your packages are standing by.",
            $"Ladies, gentlemen, and beings without a concept of either: {playerName} ({playerClass}) has entered the facility. Let's see how long this one lasts.",
        ]));

    public Task<string> OnKillAsync(Guid sessionId, string playerName, string enemyName, int killCount, CancellationToken ct = default) =>
        Task.FromResult(Pick([
            $"{enemyName} eliminated. Kill #{killCount} for {playerName}. The audience rating just ticked up.",
            $"DOWN GOES {enemyName.ToUpper()}! {playerName} with kill number {killCount}. Sponsors are VERY interested.",
            $"Ooh. {enemyName} did not survive that encounter. {playerName} at {killCount} kills. Commentary incoming from our alien desk.",
        ]));

    public Task<string> OnPlayerDamagedAsync(Guid sessionId, string playerName, int damage, int remainingHp, CancellationToken ct = default) =>
        Task.FromResult(Pick([
            $"{playerName} takes {damage} damage. {remainingHp} HP remaining. Keep it together.",
            $"Ouch. {damage} points of damage for {playerName}. The crowd winces. Or they cheer. Hard to tell.",
            $"{playerName} absorbs {damage} damage. {remainingHp} HP left. We've seen worse. We've also seen much worse outcomes from worse.",
        ]));

    public Task<string> OnDeathAsync(Guid sessionId, string playerName, int floorsCleared, int killCount, CancellationToken ct = default) =>
        Task.FromResult(Pick([
            $"{playerName} is ELIMINATED. {floorsCleared} floors. {killCount} kills. A solid run. The facility thanks you for your participation.",
            $"And that's a wrap on {playerName}. Floors cleared: {floorsCleared}. Kills: {killCount}. We'll see the highlight reel in next week's broadcast.",
            $"CONTESTANT DOWN. {playerName}, floor {floorsCleared}, {killCount} confirmed kills. The leaderboard has been updated. Donations welcome.",
        ]));

    public Task<string> OnFloorDescendAsync(Guid sessionId, string playerName, int newFloor, CancellationToken ct = default) =>
        Task.FromResult(Pick([
            $"{playerName} descends to floor {newFloor}. The facility adjusts. Something has noticed.",
            $"Floor {newFloor}. New zone. New threats. {playerName} keeps going. Brave or stupid — the audience votes either way.",
            $"DOWN TO FLOOR {newFloor}. {playerName} continues. Sponsor packages have been recalibrated for this depth.",
        ]));

    public Task<string> OnItemPickupAsync(Guid sessionId, string playerName, string itemName, CancellationToken ct = default) =>
        Task.FromResult(Pick([
            $"{playerName} picks up {itemName}. A sponsor brand is visible. Ratings bump incoming.",
            $"{itemName} acquired by {playerName}. The product placement algorithm is satisfied.",
            $"Oh, {playerName} found a {itemName}. Our analytics team is very excited about this.",
        ]));

    public Task<string> OnObjectionSucceedsAsync(Guid sessionId, string enemyName, CancellationToken ct = default) =>
        Task.FromResult(Pick([
            $"OBJECTION SUSTAINED. {enemyName} is legally compelled to stand down. The facility's legal team is reportedly furious.",
            $"Incredible. The Lawyer has once again exploited a procedural loophole. {enemyName} cannot legally proceed with this attack.",
            $"The facility's terms of service do not actually permit {enemyName} to engage at this time. Objection upheld. Remarkable.",
        ]));

    public Task<string> OnLevelUpAsync(Guid sessionId, string playerName, int newLevel, CancellationToken ct = default) =>
        Task.FromResult(Pick([
            $"{playerName} has reached level {newLevel}. The facility has noted this development with moderate concern.",
            $"LEVEL {newLevel}. {playerName} continues to improve. Sponsors are adjusting their risk assessments accordingly.",
            $"{playerName} levels up to {newLevel}. Statistically, this makes them more expensive to kill. The audience approves.",
        ]));

    public Task<string> OnAbilityUsedAsync(Guid sessionId, string playerName, string abilityName, string? targetName, CancellationToken ct = default) =>
        Task.FromResult(Pick(targetName is not null
            ? [
                $"{playerName} deploys {abilityName} against {targetName}. Unorthodox. Effective. Good television.",
                $"{abilityName} activated. {targetName} did not see that coming. Neither did our legal team.",
                $"Class ability: {abilityName}. {playerName} reminds {targetName} — and this broadcast — why they were hired.",
            ]
            : [
                $"{playerName} uses {abilityName}. The facility logs this under 'contestant resourcefulness.'",
                $"{abilityName} deployed. {playerName} is either very clever or very desperate. Ratings suggest both.",
                $"Ability used: {abilityName}. The audience appreciates a professional at work.",
            ]));

    private static string Pick(string[] options) => options[_rng.Next(options.Length)];
}

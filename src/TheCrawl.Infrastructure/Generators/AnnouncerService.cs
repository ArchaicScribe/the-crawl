using TheCrawl.Application.Interfaces;

namespace TheCrawl.Infrastructure.Generators;

public class AnnouncerService : IAnnouncerService
{
    private static readonly Random _rng = new();

    public string OnSessionStart(string playerName, string playerClass) =>
        Pick([
            $"WELCOME TO THE CRAWL! Tonight's contestant: {playerName}, a {playerClass}. The odds are not in their favor. That's why we watch.",
            $"Broadcast starting NOW. {playerName} — classified as '{playerClass}' — enters the facility. Sponsors, your packages are standing by.",
            $"Ladies, gentlemen, and beings without a concept of either: {playerName} ({playerClass}) has entered the facility. Let's see how long this one lasts.",
        ]);

    public string OnKill(string playerName, string enemyName, int killCount) =>
        Pick([
            $"{enemyName} eliminated. Kill #{killCount} for {playerName}. The audience rating just ticked up.",
            $"DOWN GOES {enemyName.ToUpper()}! {playerName} with kill number {killCount}. Sponsors are VERY interested.",
            $"Ooh. {enemyName} did not survive that encounter. {playerName} at {killCount} kills. Commentary incoming from our alien desk.",
        ]);

    public string OnPlayerDamaged(string playerName, int damage, int remainingHp) =>
        Pick([
            $"{playerName} takes {damage} damage. {remainingHp} HP remaining. Keep it together.",
            $"Ouch. {damage} points of damage for {playerName}. The crowd winces. Or they cheer. Hard to tell.",
            $"{playerName} absorbs {damage} damage. {remainingHp} HP left. We've seen worse. We've also seen much worse outcomes from worse.",
        ]);

    public string OnDeath(string playerName, int floorsCleared, int killCount) =>
        Pick([
            $"{playerName} is ELIMINATED. {floorsCleared} floors. {killCount} kills. A solid run. The facility thanks you for your participation.",
            $"And that's a wrap on {playerName}. Floors cleared: {floorsCleared}. Kills: {killCount}. We'll see the highlight reel in next week's broadcast.",
            $"CONTESTANT DOWN. {playerName}, floor {floorsCleared}, {killCount} confirmed kills. The leaderboard has been updated. Donations welcome.",
        ]);

    public string OnFloorDescend(string playerName, int newFloor) =>
        Pick([
            $"{playerName} descends to floor {newFloor}. The facility adjusts. Something has noticed.",
            $"Floor {newFloor}. New zone. New threats. {playerName} keeps going. Brave or stupid — the audience votes either way.",
            $"DOWN TO FLOOR {newFloor}. {playerName} continues. Sponsor packages have been recalibrated for this depth.",
        ]);

    public string OnItemPickup(string playerName, string itemName) =>
        Pick([
            $"{playerName} picks up {itemName}. A sponsor brand is visible. Ratings bump incoming.",
            $"{itemName} acquired by {playerName}. The product placement algorithm is satisfied.",
            $"Oh, {playerName} found a {itemName}. Our analytics team is very excited about this.",
        ]);

    public string OnObjectionSucceeds(string enemyName) =>
        Pick([
            $"OBJECTION SUSTAINED. {enemyName} is legally compelled to stand down. The facility's legal team is reportedly furious.",
            $"Incredible. The Lawyer has once again exploited a procedural loophole. {enemyName} cannot legally proceed with this attack.",
            $"The facility's terms of service do not actually permit {enemyName} to engage at this time. Objection upheld. Remarkable.",
        ]);

    private static string Pick(string[] options) => options[_rng.Next(options.Length)];
}

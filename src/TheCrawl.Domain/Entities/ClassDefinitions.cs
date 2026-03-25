using TheCrawl.Domain.Enums;
using TheCrawl.Domain.ValueObjects;

namespace TheCrawl.Domain.Entities;

public static class ClassDefinitions
{
    public static Stats GetBaseStats(PlayerClass playerClass) => playerClass switch
    {
        PlayerClass.Exterminator => new Stats(Muscle: 8, Nerve: 4, Grit: 7, Wit: 2, Ratings: 4),
        PlayerClass.Influencer   => new Stats(Muscle: 2, Nerve: 5, Grit: 3, Wit: 4, Ratings: 10),
        PlayerClass.Accountant   => new Stats(Muscle: 2, Nerve: 4, Grit: 3, Wit: 9, Ratings: 3),
        PlayerClass.Electrician  => new Stats(Muscle: 4, Nerve: 8, Grit: 4, Wit: 6, Ratings: 4),
        PlayerClass.Veteran      => new Stats(Muscle: 5, Nerve: 5, Grit: 6, Wit: 5, Ratings: 5),
        PlayerClass.Lawyer       => new Stats(Muscle: 2, Nerve: 3, Grit: 3, Wit: 8, Ratings: 7),
        _ => throw new ArgumentOutOfRangeException(nameof(playerClass))
    };

    public static string GetFlavorQuote(PlayerClass playerClass) => playerClass switch
    {
        PlayerClass.Exterminator => "Thirty years in pest control. This isn't that different.",
        PlayerClass.Influencer   => "I have 4.2 million followers. This is just content.",
        PlayerClass.Accountant   => "I've reviewed the dungeon's resource allocation. It's inefficient. So am I.",
        PlayerClass.Electrician  => "I'm not supposed to be here. I was just fixing the wiring.",
        PlayerClass.Veteran      => "Third tour. At least this one has loot.",
        PlayerClass.Lawyer       => "My client disputes the validity of this encounter.",
        _ => throw new ArgumentOutOfRangeException(nameof(playerClass))
    };

    /// <summary>
    /// XP required to advance FROM the given level to the next.
    /// Uses a 1.5-power curve: 50 * level^1.5 (rounded).
    /// Level 1→2: 50, Level 5→6: 559, Level 10→11: 1581.
    /// </summary>
    public static int XpToNextLevel(int level) =>
        (int)(50.0 * Math.Pow(level, 1.5));

    /// <summary>Stat bonuses awarded on level-up, tuned per class identity.</summary>
    public static Stats GetLevelUpBonus(PlayerClass playerClass) => playerClass switch
    {
        PlayerClass.Exterminator => new Stats(Muscle: 2, Nerve: 0, Grit: 1, Wit: 0, Ratings: 0),
        PlayerClass.Influencer   => new Stats(Muscle: 0, Nerve: 1, Grit: 0, Wit: 1, Ratings: 1),
        PlayerClass.Accountant   => new Stats(Muscle: 0, Nerve: 0, Grit: 1, Wit: 2, Ratings: 0),
        PlayerClass.Electrician  => new Stats(Muscle: 0, Nerve: 2, Grit: 0, Wit: 1, Ratings: 0),
        PlayerClass.Veteran      => new Stats(Muscle: 1, Nerve: 1, Grit: 1, Wit: 0, Ratings: 0),
        PlayerClass.Lawyer       => new Stats(Muscle: 0, Nerve: 1, Grit: 0, Wit: 1, Ratings: 1),
        _ => throw new ArgumentOutOfRangeException(nameof(playerClass))
    };
}

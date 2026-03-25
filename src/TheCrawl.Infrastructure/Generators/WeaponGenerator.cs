using TheCrawl.Domain.Entities;
using TheCrawl.Domain.Enums;
using TheCrawl.Domain.Interfaces;

namespace TheCrawl.Infrastructure.Generators;

/// <summary>
/// Procedural weapon generator using zone-specific component pools.
/// Each zone has its own vocabulary of prefixes, bases, and suffixes.
/// Compatibility filtering prevents tonally incoherent combinations.
/// Unique weapons are hand-authored and bypass the procedural pipeline.
/// </summary>
public class WeaponGenerator : IWeaponGenerator
{
    private static readonly Random Rng = new();

    // -------------------------------------------------------------------------
    // Rarity weights per zone — higher zones skew toward better rarity
    // -------------------------------------------------------------------------

    private static readonly Dictionary<ZoneType, int[]> RarityWeights = new()
    {
        // indices: Junk, Standard, Quality, Prototype, AlienTech
        [ZoneType.SurfaceFringe]    = [50, 35, 12, 3,  0],
        [ZoneType.CorporateSector]  = [25, 40, 25, 9,  1],
        [ZoneType.IndustrialSector] = [10, 30, 35, 20, 5],
        [ZoneType.TheDeep]          = [0,  15, 35, 35, 15],
    };

    // Unique weapon chance by rarity (out of 100) — elevated for testing
    private static readonly Dictionary<WeaponRarity, int> UniqueChance = new()
    {
        [WeaponRarity.Junk]      = 0,
        [WeaponRarity.Standard]  = 3,
        [WeaponRarity.Quality]   = 8,
        [WeaponRarity.Prototype] = 15,
        [WeaponRarity.AlienTech] = 30,
    };

    // -------------------------------------------------------------------------
    // Component pools — each zone has its own vocabulary
    // -------------------------------------------------------------------------

    private record WeaponBase(string Name, WeaponWeight Weight, int DmgMin, int DmgMax);
    private record WeaponAffix(string Text, string[] IncompatibleWith);

    private static readonly Dictionary<ZoneType, WeaponBase[]> BasePools = new()
    {
        [ZoneType.SurfaceFringe] =
        [
            new("Stapler",           WeaponWeight.Light,  1, 3),
            new("Box Cutter",        WeaponWeight.Light,  2, 4),
            new("Letter Opener",     WeaponWeight.Light,  1, 3),
            new("Coffee Pot",        WeaponWeight.Light,  2, 5),
            new("Tape Dispenser",    WeaponWeight.Light,  1, 3),
            new("Fire Extinguisher", WeaponWeight.Medium, 4, 8),
            new("Folding Chair",     WeaponWeight.Medium, 3, 7),
        ],
        [ZoneType.CorporateSector] =
        [
            new("Briefcase",         WeaponWeight.Medium, 4, 8),
            new("Security Baton",    WeaponWeight.Medium, 5, 9),
            new("Industrial Stapler",WeaponWeight.Medium, 4, 7),
            new("Laser Pointer",     WeaponWeight.Light,  3, 6),
            new("Cable Bundle",      WeaponWeight.Light,  2, 5),
            new("Corporate Seal",    WeaponWeight.Medium, 5, 9),
            new("Toner Cartridge",   WeaponWeight.Light,  3, 6),
        ],
        [ZoneType.IndustrialSector] =
        [
            new("Pipe Wrench",       WeaponWeight.Heavy,  8, 14),
            new("Rivet Gun",         WeaponWeight.Medium, 6, 11),
            new("Plasma Cutter",     WeaponWeight.Heavy,  10, 16),
            new("Angle Grinder",     WeaponWeight.Medium, 7, 12),
            new("Industrial Drill",  WeaponWeight.Heavy,  9, 15),
            new("Welding Torch",     WeaponWeight.Medium, 6, 11),
            new("Pneumatic Press",   WeaponWeight.Heavy,  11, 18),
        ],
        [ZoneType.TheDeep] =
        [
            new("Void Emitter",      WeaponWeight.Light,  10, 18),
            new("Probability Lance", WeaponWeight.Medium, 12, 20),
            new("Resonance Hammer",  WeaponWeight.Heavy,  15, 24),
            new("Entropy Blade",     WeaponWeight.Light,  11, 19),
            new("Signal Disruptor",  WeaponWeight.Medium, 10, 17),
            new("Quantum Stapler",   WeaponWeight.Light,  9,  16),
        ],
    };

    private static readonly Dictionary<ZoneType, WeaponAffix[]> PrefixPools = new()
    {
        [ZoneType.SurfaceFringe] =
        [
            new("Recalled",          []),
            new("Ergonomic",         ["Recalled"]),
            new("Award-Winning",     ["Recalled", "Off-Brand"]),
            new("Off-Brand",         ["Award-Winning", "Ergonomic"]),
            new("OSHA-Cited",        []),
            new("Biodegradable",     []),
        ],
        [ZoneType.CorporateSector] =
        [
            new("Proprietary",       []),
            new("Restructured",      ["Synergistic"]),
            new("Synergistic",       ["Restructured"]),
            new("Redacted",          []),
            new("Compliant",         ["Redacted"]),
            new("Leveraged",         []),
        ],
        [ZoneType.IndustrialSector] =
        [
            new("Overclocked",       ["Decommissioned"]),
            new("Decommissioned",    ["Overclocked", "Reinforced"]),
            new("Pressurized",       []),
            new("Condemned",         ["Reinforced"]),
            new("Reinforced",        ["Condemned", "Decommissioned"]),
            new("Retrofitted",       []),
        ],
        [ZoneType.TheDeep] =
        [
            new("Non-Euclidean",     []),
            new("Sentient (Allegedly)", ["Discontinued"]),
            new("Discontinued",      ["Sentient (Allegedly)"]),
            new("Unclassified",      []),
            new("Legally Distinct",  []),
            new("Recalled (Universal)", ["Legally Distinct"]),
        ],
    };

    private static readonly Dictionary<ZoneType, WeaponAffix[]> SuffixPools = new()
    {
        [ZoneType.SurfaceFringe] =
        [
            new("of the Break Room",      []),
            new("of Aisle 7",             []),
            new("(Batteries Not Included)", ["of Aisle 7"]),
            new("of the Lost and Found",  []),
            new("of Uncertain Provenance",[]),
            new("of the Supply Closet",   []),
        ],
        [ZoneType.CorporateSector] =
        [
            new("of the Boardroom",       []),
            new("of Q3 Projections",      []),
            new("(Patent Pending)",       ["of Q3 Projections"]),
            new("of the Hostile Takeover",[]),
            new("of the Non-Disclosure Agreement", []),
            new("of the Exit Interview",  []),
        ],
        [ZoneType.IndustrialSector] =
        [
            new("of the Night Shift",     []),
            new("of the Union",           []),
            new("of Structural Concern",  ["of the Union"]),
            new("(Do Not Operate)",       []),
            new("of the Safety Violation",[]),
            new("of the Final Inspection",[]),
        ],
        [ZoneType.TheDeep] =
        [
            new("of the Previous Contestant", []),
            new("of Uncertain Physics",   []),
            new("(Translation Pending)",  []),
            new("of the Broadcast",       []),
            new("of Sector 7-G",          []),
            new("of Non-Local Origin",    ["of Sector 7-G"]),
        ],
    };

    // -------------------------------------------------------------------------
    // Unidentified names per zone
    // -------------------------------------------------------------------------

    private static readonly Dictionary<ZoneType, string[]> UnidentifiedNames = new()
    {
        [ZoneType.SurfaceFringe]    = ["a vibrating piece of office equipment", "something from the supply closet", "an item of unclear purpose", "a repurposed consumer product"],
        [ZoneType.CorporateSector]  = ["a proprietary device (unlicensed)", "a redacted asset", "a classified implement", "a restructured tool"],
        [ZoneType.IndustrialSector] = ["an unsanctioned tool", "a decommissioned something", "a load-bearing implement", "a device marked Do Not Operate"],
        [ZoneType.TheDeep]          = ["an unclassified signal emitter", "a device of non-local origin", "an item of uncertain physics", "(translation pending)"],
    };

    // -------------------------------------------------------------------------
    // Hand-authored unique weapons — one table, any zone can roll them
    // -------------------------------------------------------------------------

    private static readonly Weapon[] UniqueWeapons =
    [
        // SurfaceFringe unique
        Weapon.Create(
            baseName: "Resignation Letter",
            prefix: "The",
            suffix: string.Empty,
            unidentifiedName: "a crumpled document",
            rarity: WeaponRarity.Quality,
            weight: WeaponWeight.Light,
            originZone: ZoneType.SurfaceFringe,
            tierLevel: 1,
            damageMin: 4, damageMax: 8,
            maxDurability: 20,
            statModifierStat: "WIT", statModifierValue: 2,
            isUnique: true),

        // CorporateSector unique — Donut's Tiara (easter egg)
        Weapon.Create(
            baseName: "Tiara",
            prefix: "Donut's",
            suffix: string.Empty,
            unidentifiedName: "an accessory that seems to be watching you",
            rarity: WeaponRarity.Prototype,
            weight: WeaponWeight.Light,
            originZone: ZoneType.CorporateSector,
            tierLevel: 2,
            damageMin: 2, damageMax: 5,
            maxDurability: 999,
            statModifierStat: "RATINGS", statModifierValue: 5,
            isUnique: true),

        // IndustrialSector unique
        Weapon.Create(
            baseName: "Final Inspection",
            prefix: "The",
            suffix: string.Empty,
            unidentifiedName: "a tool with a checklist attached",
            rarity: WeaponRarity.Prototype,
            weight: WeaponWeight.Heavy,
            originZone: ZoneType.IndustrialSector,
            tierLevel: 3,
            damageMin: 14, damageMax: 22,
            maxDurability: 1,  // breaks permanently after one use
            isUnique: true),

        // TheDeep unique
        Weapon.Create(
            baseName: "Broadcast Signal",
            prefix: "The",
            suffix: string.Empty,
            unidentifiedName: "a device emitting something uncomfortably familiar",
            rarity: WeaponRarity.AlienTech,
            weight: WeaponWeight.Medium,
            originZone: ZoneType.TheDeep,
            tierLevel: 4,
            damageMin: 18, damageMax: 28,
            maxDurability: 10,
            isUnique: true),
    ];

    // -------------------------------------------------------------------------
    // Durability bands by rarity
    // -------------------------------------------------------------------------

    private static readonly Dictionary<WeaponRarity, (int Min, int Max)> DurabilityBands = new()
    {
        [WeaponRarity.Junk]      = (5,  10),
        [WeaponRarity.Standard]  = (12, 20),
        [WeaponRarity.Quality]   = (18, 28),
        [WeaponRarity.Prototype] = (25, 40),
        [WeaponRarity.AlienTech] = (30, 50),
    };

    // -------------------------------------------------------------------------
    // Rarity damage multipliers
    // -------------------------------------------------------------------------

    private static readonly Dictionary<WeaponRarity, float> DamageMultiplier = new()
    {
        [WeaponRarity.Junk]      = 0.7f,
        [WeaponRarity.Standard]  = 1.0f,
        [WeaponRarity.Quality]   = 1.3f,
        [WeaponRarity.Prototype] = 1.7f,
        [WeaponRarity.AlienTech] = 2.2f,
    };

    // -------------------------------------------------------------------------
    // Public API
    // -------------------------------------------------------------------------

    public WeaponRarity RollRarity(ZoneType zone, Random rng)
    {
        var weights = RarityWeights[zone];
        var total = weights.Sum();
        var roll = rng.Next(0, total);
        var cumulative = 0;
        for (var i = 0; i < weights.Length; i++)
        {
            cumulative += weights[i];
            if (roll < cumulative)
                return (WeaponRarity)i;
        }
        return WeaponRarity.Standard;
    }

    public Weapon Generate(ZoneType zone, WeaponRarity rarity, bool forceUnique = false)
    {
        // Roll for unique weapon first
        var uniqueRoll = Rng.Next(0, 100);
        if (forceUnique || uniqueRoll < UniqueChance[rarity])
        {
            var eligible = UniqueWeapons
                .Where(u => u.Rarity <= rarity + 1)
                .ToArray();
            if (eligible.Length > 0)
                return eligible[Rng.Next(eligible.Length)];
        }

        // Procedural generation
        var bases    = BasePools[zone];
        var prefixes = PrefixPools[zone];
        var suffixes = SuffixPools[zone];

        var weaponBase = bases[Rng.Next(bases.Length)];

        // Pick prefix, then filter suffixes for compatibility
        var prefix = prefixes[Rng.Next(prefixes.Length)];
        var compatibleSuffixes = suffixes
            .Where(s => !s.IncompatibleWith.Contains(prefix.Text))
            .ToArray();
        var suffix = compatibleSuffixes.Length > 0
            ? compatibleSuffixes[Rng.Next(compatibleSuffixes.Length)]
            : suffixes[Rng.Next(suffixes.Length)];

        var unidentifiedNames = UnidentifiedNames[zone];
        var unidentifiedName  = unidentifiedNames[Rng.Next(unidentifiedNames.Length)];

        var mult     = DamageMultiplier[rarity];
        var dmgMin   = Math.Max(1, (int)(weaponBase.DmgMin * mult));
        var dmgMax   = Math.Max(dmgMin + 1, (int)(weaponBase.DmgMax * mult));

        var durBand  = DurabilityBands[rarity];
        var durability = Rng.Next(durBand.Min, durBand.Max + 1);

        var isCursed = rarity == WeaponRarity.Junk && Rng.Next(0, 100) < 20;

        return Weapon.Create(
            baseName: weaponBase.Name,
            prefix: prefix.Text,
            suffix: suffix.Text,
            unidentifiedName: unidentifiedName,
            rarity: rarity,
            weight: weaponBase.Weight,
            originZone: zone,
            tierLevel: ZoneToTier(zone),
            damageMin: dmgMin,
            damageMax: dmgMax,
            maxDurability: durability,
            isCursed: isCursed);
    }

    private static int ZoneToTier(ZoneType zone) => zone switch
    {
        ZoneType.SurfaceFringe    => 1,
        ZoneType.CorporateSector  => 2,
        ZoneType.IndustrialSector => 3,
        ZoneType.TheDeep          => 4,
        _                         => 1
    };
}

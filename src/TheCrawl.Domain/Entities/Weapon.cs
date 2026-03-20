using TheCrawl.Domain.Enums;
using TheCrawl.Domain.ValueObjects;

namespace TheCrawl.Domain.Entities;

public class Weapon
{
    public Guid Id { get; private set; }

    // Identity
    public string BaseName { get; private set; }      // e.g. "Stapler"
    public string Prefix { get; private set; }        // e.g. "Recalled"
    public string Suffix { get; private set; }        // e.g. "of the Break Room"
    public string UnidentifiedName { get; private set; } // zone-appropriate mystery name

    // Classification
    public WeaponRarity Rarity { get; private set; }
    public WeaponWeight Weight { get; private set; }
    public ZoneType OriginZone { get; private set; }
    public int TierLevel { get; private set; }        // 1–4, matches zone tier

    // Combat
    public int DamageMin { get; private set; }
    public int DamageMax { get; private set; }

    /// <summary>Stat boosted by this weapon (null = damage only).</summary>
    public string? StatModifierStat { get; private set; }
    public int StatModifierValue { get; private set; }

    // Condition
    public int MaxDurability { get; private set; }
    public int CurrentDurability { get; private set; }
    public bool IsBroken => CurrentDurability <= 0;

    // Identification
    public bool IsIdentified { get; private set; }

    /// <summary>True for hand-authored uniques — never procedurally generated.</summary>
    public bool IsUnique { get; private set; }

    /// <summary>Cursed weapons cannot be unequipped until cleansed or floor descent.</summary>
    public bool IsCursed { get; private set; }

    private Weapon() { }

    /// <summary>Full display name — only available after identification.</summary>
    public string DisplayName => IsIdentified
        ? $"{Prefix} {BaseName} {Suffix}".Trim()
        : UnidentifiedName;

    public int RollDamage(Random rng) =>
        IsBroken ? 0 : rng.Next(DamageMin, DamageMax + 1);

    /// <summary>Degrades weapon by one point. Returns true if weapon just broke.</summary>
    public bool Degrade()
    {
        if (IsBroken) return false;
        CurrentDurability--;
        return IsBroken;
    }

    public void Repair(int amount) =>
        CurrentDurability = Math.Min(MaxDurability, CurrentDurability + amount);

    public void Identify() => IsIdentified = true;

    public void Cleanse() => IsCursed = false;

    public static Weapon Create(
        string baseName, string prefix, string suffix, string unidentifiedName,
        WeaponRarity rarity, WeaponWeight weight, ZoneType originZone, int tierLevel,
        int damageMin, int damageMax, int maxDurability,
        string? statModifierStat = null, int statModifierValue = 0,
        bool isCursed = false, bool isUnique = false) => new()
    {
        Id = Guid.NewGuid(),
        BaseName = baseName,
        Prefix = prefix,
        Suffix = suffix,
        UnidentifiedName = unidentifiedName,
        Rarity = rarity,
        Weight = weight,
        OriginZone = originZone,
        TierLevel = tierLevel,
        DamageMin = damageMin,
        DamageMax = damageMax,
        MaxDurability = maxDurability,
        CurrentDurability = maxDurability,
        StatModifierStat = statModifierStat,
        StatModifierValue = statModifierValue,
        IsCursed = isCursed,
        IsUnique = isUnique,
        IsIdentified = isUnique // uniques self-identify; procedural weapons do not
    };

    public static Weapon Restore(
        Guid id, string baseName, string prefix, string suffix, string unidentifiedName,
        WeaponRarity rarity, WeaponWeight weight, ZoneType originZone, int tierLevel,
        int damageMin, int damageMax, int maxDurability, int currentDurability,
        bool isIdentified, bool isUnique, bool isCursed,
        string? statModifierStat = null, int statModifierValue = 0) => new()
    {
        Id = id,
        BaseName = baseName,
        Prefix = prefix,
        Suffix = suffix,
        UnidentifiedName = unidentifiedName,
        Rarity = rarity,
        Weight = weight,
        OriginZone = originZone,
        TierLevel = tierLevel,
        DamageMin = damageMin,
        DamageMax = damageMax,
        MaxDurability = maxDurability,
        CurrentDurability = currentDurability,
        IsIdentified = isIdentified,
        IsUnique = isUnique,
        IsCursed = isCursed,
        StatModifierStat = statModifierStat,
        StatModifierValue = statModifierValue
    };
}

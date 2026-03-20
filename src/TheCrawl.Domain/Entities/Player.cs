using TheCrawl.Domain.Enums;
using TheCrawl.Domain.ValueObjects;

namespace TheCrawl.Domain.Entities;

public class Player
{
    private const int BaseBackpackCapacity = 6;

    public Guid Id { get; private set; }
    public string Name { get; private set; }
    public PlayerClass Class { get; private set; }
    public Stats BaseStats { get; private set; }
    public int CurrentHp { get; private set; }
    public int MaxHp => BaseStats.MaxHp;
    public int Level { get; private set; }
    public int KillCount { get; private set; }
    public int FloorsCleared { get; private set; }
    public Position Position { get; private set; }
    public bool IsAlive => CurrentHp > 0;

    // -------------------------------------------------------------------------
    // Equipment slots
    // -------------------------------------------------------------------------
    public Weapon? EquippedWeapon   { get; private set; }
    public Weapon? EquippedOffhand  { get; private set; }

    // Armor slots
    public Weapon? SlotHead         { get; private set; }
    public Weapon? SlotFace         { get; private set; }
    public Weapon? SlotNeck         { get; private set; }
    public Weapon? SlotChest        { get; private set; }
    public Weapon? SlotArms         { get; private set; }
    public Weapon? SlotBracers      { get; private set; }
    public Weapon? SlotGloves       { get; private set; }
    public Weapon? SlotBelt         { get; private set; }
    public Weapon? SlotLegs         { get; private set; }
    public Weapon? SlotBoots        { get; private set; }

    // Jewelry / accessory
    public Weapon? SlotRing1        { get; private set; }
    public Weapon? SlotRing2        { get; private set; }
    public Weapon? SlotAccessory    { get; private set; }

    // Backpack — capacity expands via sponsorship tiers
    public List<Item> Backpack      { get; private set; } = [];
    public int BackpackCapacity     { get; private set; } = BaseBackpackCapacity;
    public bool BackpackFull        => Backpack.Count >= BackpackCapacity;

    private Player() { } // EF Core

    public Player(string name, PlayerClass playerClass, Stats baseStats, Position startPosition)
    {
        Id = Guid.NewGuid();
        Name = name;
        Class = playerClass;
        BaseStats = baseStats;
        CurrentHp = baseStats.MaxHp;
        Level = 1;
        Position = startPosition;
    }

    public void MoveTo(Position position) => Position = position;

    public int TakeDamage(int amount)
    {
        var actual = Math.Max(0, amount);
        CurrentHp = Math.Max(0, CurrentHp - actual);
        return actual;
    }

    public void Heal(int amount) =>
        CurrentHp = Math.Min(MaxHp, CurrentHp + amount);

    public void RegisterKill() => KillCount++;

    public void ClearFloor() => FloorsCleared++;

    public void LevelUp(Stats bonus)
    {
        Level++;
        BaseStats = BaseStats.Add(bonus).Clamp();
        CurrentHp = Math.Min(CurrentHp, MaxHp);
    }

    public void ExpandBackpack(int slots) =>
        BackpackCapacity += slots;

    // -------------------------------------------------------------------------
    // Weapon equip / unequip
    // -------------------------------------------------------------------------

    /// <summary>
    /// Equips a weapon to the main hand. Identifies it on equip.
    /// Returns the previously equipped weapon (if any) for the caller to handle.
    /// </summary>
    public Weapon? EquipWeapon(Weapon weapon)
    {
        weapon.Identify();
        var previous = EquippedWeapon;
        EquippedWeapon = weapon;
        return previous;
    }

    /// <summary>
    /// Equips a weapon to the offhand. Only valid for dual-wield classes.
    /// Returns the previously equipped offhand (if any).
    /// </summary>
    public Weapon? EquipOffhand(Weapon weapon)
    {
        weapon.Identify();
        var previous = EquippedOffhand;
        EquippedOffhand = weapon;
        return previous;
    }

    public bool CanDualWield => Class is PlayerClass.Exterminator or PlayerClass.Veteran;

    public bool CanEquipHeavy => Class is PlayerClass.Exterminator or PlayerClass.Veteran;

    // -------------------------------------------------------------------------
    // Combat helpers
    // -------------------------------------------------------------------------

    /// <summary>
    /// Base weapon damage for this turn. Falls back to fists (MUSCLE-based) if unarmed.
    /// Degrades the equipped weapon on use.
    /// </summary>
    public int RollWeaponDamage(Random rng)
    {
        if (EquippedWeapon is null || EquippedWeapon.IsBroken)
            return Math.Max(1, BaseStats.Muscle + rng.Next(-1, 2)); // fists

        var damage = EquippedWeapon.RollDamage(rng) + BaseStats.Muscle;
        EquippedWeapon.Degrade();
        return Math.Max(1, damage);
    }

    public long BroadcastScore =>
        (long)BaseStats.Ratings * FloorsCleared * Math.Max(1, KillCount);

    public static Player Restore(
        Guid id, string name, PlayerClass playerClass, Stats stats,
        int currentHp, int level, int killCount, int floorsCleared, Position position,
        int backpackCapacity = BaseBackpackCapacity,
        Weapon? equippedWeapon = null, Weapon? equippedOffhand = null,
        List<Item>? backpack = null) => new()
    {
        Id = id,
        Name = name,
        Class = playerClass,
        BaseStats = stats,
        CurrentHp = currentHp,
        Level = level,
        KillCount = killCount,
        FloorsCleared = floorsCleared,
        Position = position,
        BackpackCapacity = backpackCapacity,
        EquippedWeapon = equippedWeapon,
        EquippedOffhand = equippedOffhand,
        Backpack = backpack ?? [],
    };
}

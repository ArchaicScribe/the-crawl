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
    public int Xp { get; private set; }
    public int XpToNextLevel => ClassDefinitions.XpToNextLevel(Level);
    public int KillCount { get; private set; }
    public int FloorsCleared { get; private set; }
    public int TotalRatings { get; private set; }
    public int AbilityCooldown { get; private set; }
    public bool CanUseAbility => AbilityCooldown <= 0;
    public Position Position { get; private set; }
    public bool IsAlive => CurrentHp > 0;

    // -------------------------------------------------------------------------
    // Equipment slots
    // -------------------------------------------------------------------------
    public Weapon? EquippedWeapon  { get; private set; }
    public Weapon? EquippedOffhand { get; private set; }

    // Armor slots — typed as Weapon for now; armor items will share the entity
    public Weapon? SlotHead        { get; private set; }
    public Weapon? SlotFace        { get; private set; }
    public Weapon? SlotNeck        { get; private set; }
    public Weapon? SlotChest       { get; private set; }
    public Weapon? SlotArms        { get; private set; }
    public Weapon? SlotBracers     { get; private set; }
    public Weapon? SlotGloves      { get; private set; }
    public Weapon? SlotBelt        { get; private set; }
    public Weapon? SlotLegs        { get; private set; }
    public Weapon? SlotBoots       { get; private set; }

    // Jewelry / accessory — base slots; Influencer gets extras
    public Weapon? SlotRing1       { get; private set; }
    public Weapon? SlotRing2       { get; private set; }
    public Weapon? SlotAccessory   { get; private set; }

    // Influencer-only bonus jewelry slots
    public Weapon? SlotRing3       { get; private set; }
    public Weapon? SlotEarring     { get; private set; }
    public Weapon? SlotAnklet      { get; private set; }
    public Weapon? SlotWristband   { get; private set; }

    // -------------------------------------------------------------------------
    // Backpack — separate lists; unified capacity keeps slot count clean
    // -------------------------------------------------------------------------
    public List<Item>   BackpackItems   { get; private set; } = [];
    public List<Weapon> BackpackWeapons { get; private set; } = [];
    public int BackpackCapacity         { get; private set; } = BaseBackpackCapacity;
    public int BackpackUsed             => BackpackItems.Count + BackpackWeapons.Count;
    public bool BackpackFull            => BackpackUsed >= BackpackCapacity;

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

    // -------------------------------------------------------------------------
    // Movement / stats
    // -------------------------------------------------------------------------

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
    public void ClearFloor()   => FloorsCleared++;
    public void AddRatings(int amount) => TotalRatings += amount;
    public void StartAbilityCooldown(int turns) => AbilityCooldown = turns;
    public void TickAbilityCooldown() { if (AbilityCooldown > 0) AbilityCooldown--; }

    /// <summary>
    /// Awards XP. Loops until XP is below the next threshold to handle multi-level gains.
    /// Returns the number of levels gained (0 if none).
    /// </summary>
    public int AwardXp(int amount)
    {
        Xp += amount;
        int levelsGained = 0;
        while (Xp >= XpToNextLevel)
        {
            Xp -= XpToNextLevel;
            LevelUp(ClassDefinitions.GetLevelUpBonus(Class));
            levelsGained++;
        }
        return levelsGained;
    }

    public void LevelUp(Stats bonus)
    {
        Level++;
        BaseStats = BaseStats.Add(bonus).Clamp();
        // Partial heal on level-up — reward without fully restoring HP
        CurrentHp = Math.Min(MaxHp, CurrentHp + MaxHp / 4);
    }

    public void ExpandBackpack(int slots) => BackpackCapacity += slots;

    // -------------------------------------------------------------------------
    // Class-specific slot availability
    // -------------------------------------------------------------------------

    /// <summary>Influencers get 3 ring slots; all other classes get 2.</summary>
    public int RingSlotCount => Class == PlayerClass.Influencer ? 3 : 2;

    /// <summary>Bonus jewelry slots are Influencer-exclusive.</summary>
    public bool HasBonusJewelrySlots => Class == PlayerClass.Influencer;

    // -------------------------------------------------------------------------
    // Equip checks
    // -------------------------------------------------------------------------

    public bool CanDualWield => Class is PlayerClass.Exterminator or PlayerClass.Veteran;

    /// <summary>
    /// Returns null if the weapon can be equipped, or a reason string if not.
    /// Enforces weight class level gates per class.
    /// </summary>
    public string? CanEquip(Weapon weapon, bool offhand = false)
    {
        if (offhand && !CanDualWield)
            return "Your class cannot dual wield.";

        if (weapon.Weight == WeaponWeight.Heavy)
        {
            if (Class == PlayerClass.Veteran && Level < 10)
                return $"Veterans can equip Heavy weapons at level 10. You are level {Level}.";
            if (Class == PlayerClass.Exterminator && Level < 8)
                return $"Exterminators can equip Heavy weapons at level 8. You are level {Level}.";
            if (Class is not (PlayerClass.Veteran or PlayerClass.Exterminator))
                return "Only Veterans and Exterminators can wield Heavy weapons.";
        }

        if (offhand && weapon.Weight == WeaponWeight.Medium
            && Class == PlayerClass.Veteran && Level < 5)
            return $"Veterans can dual wield Medium weapons at level 5. You are level {Level}.";

        return null; // clear to equip
    }

    // -------------------------------------------------------------------------
    // Equip / unequip
    // -------------------------------------------------------------------------

    /// <summary>
    /// Equips weapon to main hand. Identifies on equip (risk of curse reveal).
    /// Returns the displaced weapon so the caller can push it to backpack or floor.
    /// </summary>
    public Weapon? EquipWeapon(Weapon weapon)
    {
        weapon.Identify();
        var displaced = EquippedWeapon;
        EquippedWeapon = weapon;
        return displaced;
    }

    /// <summary>
    /// Equips weapon to offhand. Caller must verify CanEquip(weapon, offhand: true) first.
    /// </summary>
    public Weapon? EquipOffhand(Weapon weapon)
    {
        weapon.Identify();
        var displaced = EquippedOffhand;
        EquippedOffhand = weapon;
        return displaced;
    }

    public void UnequipWeapon()  => EquippedWeapon  = null;
    public void UnequipOffhand() => EquippedOffhand = null;

    // -------------------------------------------------------------------------
    // Backpack operations
    // -------------------------------------------------------------------------

    public bool AddToBackpack(Weapon weapon)
    {
        if (BackpackFull) return false;
        BackpackWeapons.Add(weapon);
        return true;
    }

    public bool AddToBackpack(Item item)
    {
        if (BackpackFull) return false;
        BackpackItems.Add(item);
        return true;
    }

    public bool RemoveFromBackpack(Guid weaponId)
    {
        var weapon = BackpackWeapons.FirstOrDefault(w => w.Id == weaponId);
        if (weapon is null) return false;
        BackpackWeapons.Remove(weapon);
        return true;
    }

    public bool RemoveFromBackpack(Guid itemId, out Item? item)
    {
        item = BackpackItems.FirstOrDefault(i => i.Id == itemId);
        if (item is null) return false;
        BackpackItems.Remove(item);
        return true;
    }

    public Weapon? FindBackpackWeapon(Guid weaponId) =>
        BackpackWeapons.FirstOrDefault(w => w.Id == weaponId);

    public Item? FindBackpackItem(Guid itemId) =>
        BackpackItems.FirstOrDefault(i => i.Id == itemId);

    // -------------------------------------------------------------------------
    // Combat
    // -------------------------------------------------------------------------

    /// <summary>
    /// Rolls damage for this turn. Degrades equipped weapon on use.
    /// Falls back to fists when unarmed or broken.
    /// </summary>
    public int RollWeaponDamage(Random rng)
    {
        if (EquippedWeapon is null || EquippedWeapon.IsBroken)
            return Math.Max(1, BaseStats.Muscle + rng.Next(-1, 2));

        var damage = EquippedWeapon.RollDamage(rng) + BaseStats.Muscle;
        EquippedWeapon.Degrade();
        return Math.Max(1, damage);
    }

    public long BroadcastScore =>
        (long)(BaseStats.Ratings + TotalRatings) * Math.Max(1, FloorsCleared) * Math.Max(1, KillCount);

    // -------------------------------------------------------------------------
    // Restore
    // -------------------------------------------------------------------------

    public static Player Restore(
        Guid id, string name, PlayerClass playerClass, Stats stats,
        int currentHp, int level, int xp, int killCount, int floorsCleared,
        int totalRatings, int abilityCooldown, Position position,
        int backpackCapacity = BaseBackpackCapacity,
        Weapon? equippedWeapon  = null,
        Weapon? equippedOffhand = null,
        List<Item>?   backpackItems   = null,
        List<Weapon>? backpackWeapons = null) => new()
    {
        Id               = id,
        Name             = name,
        Class            = playerClass,
        BaseStats        = stats,
        CurrentHp        = currentHp,
        Level            = level,
        Xp               = xp,
        KillCount        = killCount,
        FloorsCleared    = floorsCleared,
        TotalRatings     = totalRatings,
        AbilityCooldown  = abilityCooldown,
        Position         = position,
        BackpackCapacity  = backpackCapacity,
        EquippedWeapon    = equippedWeapon,
        EquippedOffhand   = equippedOffhand,
        BackpackItems     = backpackItems   ?? [],
        BackpackWeapons   = backpackWeapons ?? [],
    };
}

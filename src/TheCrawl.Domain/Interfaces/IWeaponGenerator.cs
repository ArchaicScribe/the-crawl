using TheCrawl.Domain.Entities;
using TheCrawl.Domain.Enums;

namespace TheCrawl.Domain.Interfaces;

public interface IWeaponGenerator
{
    /// <summary>
    /// Generates a weapon appropriate for the given zone and rarity.
    /// Pass forceUnique = true during testing to guarantee a unique weapon.
    /// </summary>
    Weapon Generate(ZoneType zone, WeaponRarity rarity, bool forceUnique = false);

    /// <summary>
    /// Rolls a rarity for the given zone using weighted loot table sampling.
    /// Higher floors skew toward better rarity.
    /// </summary>
    WeaponRarity RollRarity(ZoneType zone, Random rng);
}

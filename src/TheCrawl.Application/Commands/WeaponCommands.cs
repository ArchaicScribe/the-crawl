using TheCrawl.Domain.Enums;

namespace TheCrawl.Application.Commands;

public record PickupCommand(Guid SessionId);
public record PickupResult(bool Success, string Message, string? ItemName = null, bool IsWeapon = false);

/// <summary>
/// Equip a weapon from the backpack.
/// Offhand: dual wield to the offhand slot.
/// JewelrySlot: route to a named jewelry/accessory slot (ignores Offhand when set).
/// </summary>
public record EquipCommand(Guid SessionId, Guid WeaponId, bool Offhand = false, JewelrySlot? JewelrySlot = null);
public record EquipResult(bool Success, string Message, string? WeaponName = null);

public record DropCommand(Guid SessionId, Guid ItemId, bool IsWeapon = false);
public record DropResult(bool Success, string Message);

public record UseItemCommand(Guid SessionId, Guid ItemId);
public record UseItemResult(bool Success, string Message, string? AnnouncerMessage = null);

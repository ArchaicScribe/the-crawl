namespace TheCrawl.Application.Commands;

public record PickupCommand(Guid SessionId);
public record PickupResult(bool Success, string Message, string? ItemName = null, bool IsWeapon = false);

public record EquipCommand(Guid SessionId, Guid WeaponId, bool Offhand = false);
public record EquipResult(bool Success, string Message, string? WeaponName = null);

public record DropCommand(Guid SessionId, Guid ItemId, bool IsWeapon = false);
public record DropResult(bool Success, string Message);

public record UseItemCommand(Guid SessionId, Guid ItemId);
public record UseItemResult(bool Success, string Message, string? AnnouncerMessage = null);

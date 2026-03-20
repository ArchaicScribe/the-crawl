namespace TheCrawl.Application.Commands;

public record PickupWeaponCommand(Guid SessionId);
public record PickupWeaponResult(bool Success, string Message, string? WeaponName = null);

public record EquipWeaponCommand(Guid SessionId, Guid WeaponId, bool Offhand = false);
public record EquipWeaponResult(bool Success, string Message, string? WeaponName = null, string? AnnouncerMessage = null);

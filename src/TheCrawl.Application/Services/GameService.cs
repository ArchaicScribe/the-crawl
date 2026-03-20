using TheCrawl.Application.Commands;
using TheCrawl.Application.Interfaces;
using TheCrawl.Domain.Entities;
using TheCrawl.Domain.Enums;
using TheCrawl.Domain.Interfaces;
using TheCrawl.Domain.ValueObjects;

namespace TheCrawl.Application.Services;

public class GameService(
    IDungeonGenerator dungeonGenerator,
    IGameSessionRepository sessionRepository,
    ISessionStore sessionStore,
    IAnnouncerService announcer,
    IFovCalculator fov,
    EnemyTurnService enemyTurns)
{
    // Sight radius in tiles. Could become a per-class stat or item modifier later.
    private const int SightRadius = 8;

    public async Task<StartGameResult> StartGameAsync(StartGameCommand command, CancellationToken ct = default)
    {
        var stats = ClassDefinitions.GetBaseStats(command.PlayerClass);
        var floor = dungeonGenerator.GenerateFloor(1, ZoneType.SurfaceFringe);
        var startPos = floor.Rooms.First().Center;
        var player = new Player(command.PlayerName, command.PlayerClass, stats, startPos);
        var session = new GameSession(player, floor);

        // Compute initial FOV from the starting position
        floor.UpdateVisibility(fov.Calculate(startPos, SightRadius, floor));

        await sessionStore.SaveAsync(session, ct);
        await sessionRepository.SaveAsync(session, ct);

        var message = await announcer.OnSessionStartAsync(session.Id, command.PlayerName, command.PlayerClass.ToString(), ct);
        return new StartGameResult(session.Id, message);
    }

    public async Task<MoveResult> MoveAsync(MoveCommand command, CancellationToken ct = default)
    {
        var session = await sessionStore.GetAsync(command.SessionId, ct);
        if (session is null || !session.IsActive)
            return new MoveResult(false, "Session not found or already ended.");

        var player = session.Player;
        var floor = session.CurrentFloor;

        var delta = command.Direction switch
        {
            Direction.North     => new Position(0, -1),
            Direction.South     => new Position(0, 1),
            Direction.West      => new Position(-1, 0),
            Direction.East      => new Position(1, 0),
            Direction.NorthEast => new Position(1, -1),
            Direction.NorthWest => new Position(-1, -1),
            Direction.SouthEast => new Position(1, 1),
            Direction.SouthWest => new Position(-1, 1),
            _ => new Position(0, 0)
        };

        var target = new Position(player.Position.X + delta.X, player.Position.Y + delta.Y);

        if (!floor.IsWalkable(target))
            return new MoveResult(false, "Something solid stops you. Probably a wall.");

        var enemy = floor.EnemyAt(target);
        if (enemy is not null)
            return new MoveResult(false, $"A {enemy.Name} blocks the way. Attack to proceed.");

        player.MoveTo(target);

        // Recompute FOV — replaces VisibleTiles, accumulates ExploredTiles
        floor.UpdateVisibility(fov.Calculate(target, SightRadius, floor));

        string? announcerMessage = null;

        if (target == floor.StairsPosition)
        {
            var nextFloorNumber = floor.FloorNumber + 1;
            var nextZone = nextFloorNumber switch
            {
                <= 5  => ZoneType.SurfaceFringe,
                <= 15 => ZoneType.CorporateSector,
                <= 25 => ZoneType.IndustrialSector,
                _     => ZoneType.TheDeep
            };
            var nextFloor = dungeonGenerator.GenerateFloor(nextFloorNumber, nextZone);
            session.DescendToFloor(nextFloor);
            // FOV from spawn position on the new floor
            nextFloor.UpdateVisibility(fov.Calculate(player.Position, SightRadius, nextFloor));
            announcerMessage = await announcer.OnFloorDescendAsync(session.Id, player.Name, nextFloorNumber, ct);
        }

        // Enemy turns run after every successful player move
        var enemyResult = await enemyTurns.ProcessTurnsAsync(session, ct);
        foreach (var ev in enemyResult.Events)
            session.LogEvent(ev);

        // Enemy announcer message takes priority over floor-descent message
        var finalAnnouncer = enemyResult.AnnouncerMessage ?? announcerMessage;

        await sessionStore.SaveAsync(session, ct);
        return new MoveResult(true, $"Moved to ({target.X},{target.Y}).", finalAnnouncer);
    }

    public async Task<PickupResult> PickupAsync(PickupCommand command, CancellationToken ct = default)
    {
        var session = await sessionStore.GetAsync(command.SessionId, ct);
        if (session is null || !session.IsActive)
            return new PickupResult(false, "Session not found or already ended.");

        var floor  = session.CurrentFloor;
        var player = session.Player;

        // Weapon takes priority over item when both occupy the same tile
        var weapon = floor.WeaponAt(player.Position);
        if (weapon is not null)
        {
            if (player.BackpackFull && player.EquippedWeapon is not null)
                return new PickupResult(false, "Backpack is full and main hand is occupied.");

            floor.RemoveWeapon(weapon);
            weapon.PickUp();

            string message;
            if (player.EquippedWeapon is null)
            {
                player.EquipWeapon(weapon);
                message = $"Equipped {weapon.DisplayName}.";
            }
            else
            {
                player.AddToBackpack(weapon);
                message = $"Added {weapon.DisplayName} to backpack.";
            }

            session.LogEvent(message);
            await sessionStore.SaveAsync(session, ct);
            return new PickupResult(true, message, weapon.DisplayName, IsWeapon: true);
        }

        var item = floor.ItemAt(player.Position);
        if (item is not null)
        {
            if (player.BackpackFull)
                return new PickupResult(false, "Backpack is full.");

            floor.RemoveItem(item);
            player.AddToBackpack(item);
            var message = $"Picked up {item.Name}.";
            session.LogEvent(message);
            await sessionStore.SaveAsync(session, ct);
            return new PickupResult(true, message, item.Name, IsWeapon: false);
        }

        return new PickupResult(false, "Nothing here to pick up.");
    }

    public async Task<EquipResult> EquipAsync(EquipCommand command, CancellationToken ct = default)
    {
        var session = await sessionStore.GetAsync(command.SessionId, ct);
        if (session is null || !session.IsActive)
            return new EquipResult(false, "Session not found or already ended.");

        var player = session.Player;
        var weapon = player.FindBackpackWeapon(command.WeaponId);
        if (weapon is null)
            return new EquipResult(false, "Weapon not found in backpack.");

        var restriction = player.CanEquip(weapon, command.Offhand);
        if (restriction is not null)
            return new EquipResult(false, restriction);

        player.RemoveFromBackpack(command.WeaponId);

        Weapon? displaced = command.Offhand
            ? player.EquipOffhand(weapon)
            : player.EquipWeapon(weapon);

        // Displaced weapon goes back into backpack if there's room, otherwise dropped
        if (displaced is not null)
        {
            if (!player.AddToBackpack(displaced))
            {
                displaced.PlaceAt(player.Position);
                session.CurrentFloor.AddWeapon(displaced);
                session.LogEvent($"{displaced.DisplayName} dropped — backpack full.");
            }
        }

        var slot = command.Offhand ? "offhand" : "main hand";
        var message = $"Equipped {weapon.DisplayName} to {slot}.";
        session.LogEvent(message);
        await sessionStore.SaveAsync(session, ct);
        return new EquipResult(true, message, weapon.DisplayName);
    }

    public async Task<DropResult> DropAsync(DropCommand command, CancellationToken ct = default)
    {
        var session = await sessionStore.GetAsync(command.SessionId, ct);
        if (session is null || !session.IsActive)
            return new DropResult(false, "Session not found or already ended.");

        var player = session.Player;
        var floor  = session.CurrentFloor;

        if (command.IsWeapon)
        {
            var weapon = player.FindBackpackWeapon(command.ItemId);
            if (weapon is null)
                return new DropResult(false, "Weapon not found in backpack.");

            if (weapon.IsCursed)
                return new DropResult(false, $"{weapon.DisplayName} is cursed and won't leave your possession.");

            player.RemoveFromBackpack(command.ItemId);
            weapon.PlaceAt(player.Position);
            floor.AddWeapon(weapon);
            session.LogEvent($"Dropped {weapon.DisplayName}.");
            await sessionStore.SaveAsync(session, ct);
            return new DropResult(true, $"Dropped {weapon.DisplayName}.");
        }
        else
        {
            if (!player.RemoveFromBackpack(command.ItemId, out var item) || item is null)
                return new DropResult(false, "Item not found in backpack.");

            item.MoveTo(player.Position);
            floor.AddItem(item);
            session.LogEvent($"Dropped {item.Name}.");
            await sessionStore.SaveAsync(session, ct);
            return new DropResult(true, $"Dropped {item.Name}.");
        }
    }

    public async Task<UseItemResult> UseItemAsync(UseItemCommand command, CancellationToken ct = default)
    {
        var session = await sessionStore.GetAsync(command.SessionId, ct);
        if (session is null || !session.IsActive)
            return new UseItemResult(false, "Session not found or already ended.");

        var player = session.Player;
        if (!player.RemoveFromBackpack(command.ItemId, out var item) || item is null)
            return new UseItemResult(false, "Item not found in backpack.");

        string message;
        string? announcerMsg = null;

        switch (item.Type)
        {
            case Domain.Entities.ItemType.Consumable:
                player.Heal(item.EffectValue);
                message = $"Used {item.Name}. Restored {item.EffectValue} HP. ({player.CurrentHp}/{player.MaxHp})";
                announcerMsg = await announcer.OnItemPickupAsync(session.Id, player.Name, item.Name, ct);
                break;
            default:
                message = $"{item.Name} can't be used directly.";
                player.AddToBackpack(item); // put it back
                await sessionStore.SaveAsync(session, ct);
                return new UseItemResult(false, message);
        }

        session.LogEvent(message);
        await sessionStore.SaveAsync(session, ct);
        return new UseItemResult(true, message, announcerMsg);
    }

    public async Task<GameSession?> GetSessionAsync(Guid sessionId, CancellationToken ct = default) =>
        await sessionStore.GetAsync(sessionId, ct);
}

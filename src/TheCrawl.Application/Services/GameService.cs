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

        var message = await announcer.OnSessionStartAsync(command.PlayerName, command.PlayerClass.ToString(), ct);
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
            announcerMessage = await announcer.OnFloorDescendAsync(player.Name, nextFloorNumber, ct);
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

    public async Task<PickupWeaponResult> PickupWeaponAsync(PickupWeaponCommand command, CancellationToken ct = default)
    {
        var session = await sessionStore.GetAsync(command.SessionId, ct);
        if (session is null || !session.IsActive)
            return new PickupWeaponResult(false, "Session not found or already ended.");

        var floor  = session.CurrentFloor;
        var weapon = floor.WeaponAt(session.Player.Position);
        if (weapon is null)
            return new PickupWeaponResult(false, "Nothing to pick up here.");

        floor.RemoveWeapon(weapon);
        weapon.PickUp();

        // Auto-equip to main hand if empty; otherwise prompt player to equip manually
        string message;
        if (session.Player.EquippedWeapon is null)
        {
            session.Player.EquipWeapon(weapon);
            message = $"Equipped {weapon.DisplayName}.";
        }
        else
        {
            message = $"Picked up {weapon.DisplayName}. Use /equip to swap it in.";
        }

        session.LogEvent(message);
        await sessionStore.SaveAsync(session, ct);
        return new PickupWeaponResult(true, message, weapon.DisplayName);
    }

    public async Task<GameSession?> GetSessionAsync(Guid sessionId, CancellationToken ct = default) =>
        await sessionStore.GetAsync(sessionId, ct);
}

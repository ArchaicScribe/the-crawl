using TheCrawl.Domain.Entities;
using TheCrawl.Domain.Enums;
using TheCrawl.Domain.Interfaces;
using TheCrawl.Domain.ValueObjects;

namespace TheCrawl.Infrastructure.Generators;

public class DungeonGenerator : IDungeonGenerator
{
    private const int MapWidth = 60;
    private const int MapHeight = 40;
    private const int MaxRooms = 12;
    private const int MinRoomSize = 4;
    private const int MaxRoomSize = 10;

    private readonly Random _rng = new();

    public Floor GenerateFloor(int floorNumber, ZoneType zone)
    {
        var tiles = InitWalls();
        var rooms = new List<Room>();

        for (int i = 0; i < MaxRooms; i++)
        {
            var w = _rng.Next(MinRoomSize, MaxRoomSize + 1);
            var h = _rng.Next(MinRoomSize, MaxRoomSize + 1);
            var x = _rng.Next(1, MapWidth - w - 1);
            var y = _rng.Next(1, MapHeight - h - 1);
            var candidate = new Room(x, y, w, h);

            if (rooms.Any(r => r.Overlaps(candidate)))
                continue;

            CarveRoom(tiles, candidate);

            if (rooms.Count > 0)
                CarveCorridors(tiles, rooms.Last().Center, candidate.Center);

            rooms.Add(candidate);
        }

        var enemies = SpawnEnemies(rooms, floorNumber, zone);
        var items = SpawnItems(rooms, floorNumber);
        var stairs = rooms.Last().Center;
        tiles[stairs.X, stairs.Y] = TileType.StairsDown;

        return new Floor(floorNumber, zone, MapWidth, MapHeight, tiles, rooms, enemies, items, stairs);
    }

    private static TileType[,] InitWalls()
    {
        var tiles = new TileType[MapWidth, MapHeight];
        for (int x = 0; x < MapWidth; x++)
            for (int y = 0; y < MapHeight; y++)
                tiles[x, y] = TileType.Wall;
        return tiles;
    }

    private static void CarveRoom(TileType[,] tiles, Room room)
    {
        for (int x = room.X; x < room.X + room.Width; x++)
            for (int y = room.Y; y < room.Y + room.Height; y++)
                tiles[x, y] = TileType.Floor;
    }

    private static void CarveCorridors(TileType[,] tiles, Position a, Position b)
    {
        int x = a.X, y = a.Y;
        while (x != b.X)
        {
            tiles[x, y] = TileType.Floor;
            x += x < b.X ? 1 : -1;
        }
        while (y != b.Y)
        {
            tiles[x, y] = TileType.Floor;
            y += y < b.Y ? 1 : -1;
        }
    }

    private List<Enemy> SpawnEnemies(List<Room> rooms, int floorNumber, ZoneType zone)
    {
        var enemies = new List<Enemy>();
        var pool = GetEnemyPool(zone, floorNumber);

        foreach (var room in rooms.Skip(1))
        {
            var count = _rng.Next(1, 4);
            for (int i = 0; i < count; i++)
            {
                var template = pool[_rng.Next(pool.Count)];
                var pos = RandomFloorPosition(room);
                enemies.Add(new Enemy(
                    template.Name,
                    template.FlavorTitle,
                    template.MaxHp + floorNumber * 2,
                    template.Damage + floorNumber / 3,
                    template.DodgeChance,
                    template.RatingsOnKill,
                    pos));
            }
        }

        return enemies;
    }

    private List<Item> SpawnItems(List<Room> rooms, int floorNumber)
    {
        var items = new List<Item>();
        foreach (var room in rooms.Skip(1).Where(_ => _rng.Next(3) == 0))
        {
            var pos = RandomFloorPosition(room);
            items.Add(new Item("Med Kit", "Restores 20 HP. Expired two years ago.", ItemType.Consumable, 20, pos));
        }
        return items;
    }

    private Position RandomFloorPosition(Room room) => new(
        _rng.Next(room.X + 1, room.X + room.Width - 1),
        _rng.Next(room.Y + 1, room.Y + room.Height - 1));

    private static List<EnemyTemplate> GetEnemyPool(ZoneType zone, int floorNumber) => zone switch
    {
        ZoneType.SurfaceFringe => [
            new("Sewer Rat", "Critically Enlarged", 8, 2, 15, 2),
            new("Feral Cat", "Previously Domesticated", 12, 3, 25, 3),
            new("Security Guard", "Very Underpaid", 15, 4, 10, 4),
        ],
        ZoneType.CorporateSector => [
            new("Security Drone", "Model HR-7", 20, 5, 20, 5),
            new("Middle Manager", "Fully Autonomous", 18, 4, 5, 6),
            new("Automated HR System", "Recruiting Version", 25, 6, 0, 8),
        ],
        ZoneType.IndustrialSector => [
            new("Maintenance Bot", "Overdue for Servicing", 30, 7, 10, 7),
            new("Arc Welder Drone", "Safety Mode Disabled", 35, 9, 15, 9),
            new("Coolant Leak", "Sentient", 20, 5, 0, 5),
        ],
        ZoneType.TheDeep => [
            new("Dungeon Architect", "Do Not Engage", 50, 12, 20, 15),
            new("Xal'Veth Scout", "Rating This 5 Stars", 40, 10, 30, 12),
            new("Broadcast Moderator", "Content Policy Enforcer", 45, 11, 15, 14),
        ],
        _ => [new("Error", "undefined behavior", 10, 3, 0, 1)]
    };

    private record EnemyTemplate(string Name, string FlavorTitle, int MaxHp, int Damage, int DodgeChance, int RatingsOnKill);
}

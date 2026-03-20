using TheCrawl.Domain.Enums;
using TheCrawl.Domain.ValueObjects;

namespace TheCrawl.Domain.Entities;

public class Floor
{
    public Guid Id { get; private set; }
    public int FloorNumber { get; private set; }
    public ZoneType Zone { get; private set; }
    public int Width { get; private set; }
    public int Height { get; private set; }
    public TileType[,] Tiles { get; private set; }
    public List<Room> Rooms { get; private set; } = [];
    public List<Enemy> Enemies { get; private set; } = [];
    public List<Item> Items { get; private set; } = [];
    public List<Weapon> Weapons { get; private set; } = [];
    public Position StairsPosition { get; private set; }
    public bool IsCleared => !Enemies.Any(e => e.IsAlive);

    /// <summary>Tiles the player can see right now. Recalculated every turn.</summary>
    public HashSet<Position> VisibleTiles { get; private set; } = [];

    /// <summary>Tiles the player has seen at any point. Accumulates over the run.</summary>
    public HashSet<Position> ExploredTiles { get; private set; } = [];

    private Floor() { }

    public Floor(int floorNumber, ZoneType zone, int width, int height, TileType[,] tiles,
        List<Room> rooms, List<Enemy> enemies, List<Item> items, List<Weapon> weapons,
        Position stairsPosition)
    {
        Id = Guid.NewGuid();
        FloorNumber = floorNumber;
        Zone = zone;
        Width = width;
        Height = height;
        Tiles = tiles;
        Rooms = rooms;
        Enemies = enemies;
        Items = items;
        Weapons = weapons;
        StairsPosition = stairsPosition;
    }

    public TileType GetTile(Position pos) => Tiles[pos.X, pos.Y];

    public bool IsWalkable(Position pos) =>
        pos.X >= 0 && pos.X < Width &&
        pos.Y >= 0 && pos.Y < Height &&
        Tiles[pos.X, pos.Y] != TileType.Wall;

    public Enemy? EnemyAt(Position pos) =>
        Enemies.FirstOrDefault(e => e.IsAlive && e.Position == pos);

    public Item? ItemAt(Position pos) =>
        Items.FirstOrDefault(i => i.Position == pos);

    public Weapon? WeaponAt(Position pos) =>
        Weapons.FirstOrDefault(w => w.Position == pos);

    public void RemoveWeapon(Weapon weapon) =>
        Weapons.Remove(weapon);

    /// <summary>
    /// Replaces the current visible set and merges it into explored.
    /// Called after every player move.
    /// </summary>
    public void UpdateVisibility(HashSet<Position> visibleTiles)
    {
        VisibleTiles = visibleTiles;
        foreach (var pos in visibleTiles)
            ExploredTiles.Add(pos);
    }

    public bool IsVisible(Position pos)  => VisibleTiles.Contains(pos);
    public bool IsExplored(Position pos) => ExploredTiles.Contains(pos);

    public static Floor Restore(
        Guid id, int floorNumber, ZoneType zone, int width, int height,
        TileType[,] tiles, List<Room> rooms, List<Enemy> enemies,
        List<Item> items, List<Weapon> weapons, Position stairsPosition,
        HashSet<Position>? visibleTiles = null,
        HashSet<Position>? exploredTiles = null) => new()
    {
        Id = id,
        FloorNumber = floorNumber,
        Zone = zone,
        Width = width,
        Height = height,
        Tiles = tiles,
        Rooms = rooms,
        Enemies = enemies,
        Items = items,
        Weapons = weapons,
        StairsPosition = stairsPosition,
        VisibleTiles  = visibleTiles  ?? [],
        ExploredTiles = exploredTiles ?? []
    };
}

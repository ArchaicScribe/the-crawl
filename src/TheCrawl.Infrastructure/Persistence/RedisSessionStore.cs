using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Caching.Distributed;
using TheCrawl.Domain.Entities;
using TheCrawl.Domain.Enums;
using TheCrawl.Domain.Interfaces;
using TheCrawl.Domain.ValueObjects;

namespace TheCrawl.Infrastructure.Persistence;

public class RedisSessionStore(IDistributedCache cache) : ISessionStore
{
    private static readonly TimeSpan SessionTtl = TimeSpan.FromHours(24);

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter() }
    };

    public async Task<GameSession?> GetAsync(Guid sessionId, CancellationToken ct = default)
    {
        var json = await cache.GetStringAsync(Key(sessionId), ct);
        if (json is null) return null;

        var snapshot = JsonSerializer.Deserialize<SessionSnapshot>(json, JsonOptions);
        return snapshot?.ToSession();
    }

    public async Task SaveAsync(GameSession session, CancellationToken ct = default)
    {
        var snapshot = SessionSnapshot.FromSession(session);
        var json = JsonSerializer.Serialize(snapshot, JsonOptions);
        await cache.SetStringAsync(Key(session.Id), json, new DistributedCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = SessionTtl
        }, ct);
    }

    public async Task RemoveAsync(Guid sessionId, CancellationToken ct = default) =>
        await cache.RemoveAsync(Key(sessionId), ct);

    private static string Key(Guid sessionId) => $"session:{sessionId}";

    // -------------------------------------------------------------------------
    // Snapshot DTOs — flat, serializable representations of domain objects
    // -------------------------------------------------------------------------

    private record SessionSnapshot(
        Guid Id,
        PlayerSnapshot Player,
        FloorSnapshot Floor,
        GameStatus Status,
        DateTime StartedAt,
        DateTime? EndedAt,
        List<string> EventLog)
    {
        public static SessionSnapshot FromSession(GameSession s) => new(
            s.Id,
            PlayerSnapshot.From(s.Player),
            FloorSnapshot.From(s.CurrentFloor),
            s.Status,
            s.StartedAt,
            s.EndedAt,
            [.. s.EventLog]);

        public GameSession ToSession()
        {
            var player = Player.ToDomain();
            var floor = Floor.ToDomain();
            return GameSession.Restore(Id, player, floor, Status, StartedAt, EndedAt, EventLog);
        }
    }

    private record PlayerSnapshot(
        Guid Id, string Name, PlayerClass Class,
        int Muscle, int Nerve, int Grit, int Wit, int Ratings,
        int CurrentHp, int Level, int KillCount, int FloorsCleared,
        int X, int Y)
    {
        public static PlayerSnapshot From(Player p) => new(
            p.Id, p.Name, p.Class,
            p.BaseStats.Muscle, p.BaseStats.Nerve, p.BaseStats.Grit,
            p.BaseStats.Wit, p.BaseStats.Ratings,
            p.CurrentHp, p.Level, p.KillCount, p.FloorsCleared,
            p.Position.X, p.Position.Y);

        public Player ToDomain() => Player.Restore(
            Id, Name, Class,
            new Stats(Muscle, Nerve, Grit, Wit, Ratings),
            CurrentHp, Level, KillCount, FloorsCleared,
            new Position(X, Y));
    }

    private record FloorSnapshot(
        Guid Id, int FloorNumber, ZoneType Zone,
        int Width, int Height,
        int[] Tiles,
        List<RoomSnapshot> Rooms,
        List<EnemySnapshot> Enemies,
        List<ItemSnapshot> Items,
        int StairsX, int StairsY,
        List<int[]> VisibleTiles,
        List<int[]> ExploredTiles)
    {
        public static FloorSnapshot From(Floor f) => new(
            f.Id, f.FloorNumber, f.Zone,
            f.Width, f.Height,
            Flatten(f.Tiles, f.Width, f.Height),
            f.Rooms.Select(RoomSnapshot.From).ToList(),
            f.Enemies.Select(EnemySnapshot.From).ToList(),
            f.Items.Select(ItemSnapshot.From).ToList(),
            f.StairsPosition.X, f.StairsPosition.Y,
            f.VisibleTiles.Select(p  => new[] { p.X, p.Y }).ToList(),
            f.ExploredTiles.Select(p => new[] { p.X, p.Y }).ToList());

        public Floor ToDomain()
        {
            var tiles    = Unflatten(Tiles, Width, Height);
            var visible  = VisibleTiles.Select(p  => new Position(p[0], p[1])).ToHashSet();
            var explored = ExploredTiles.Select(p => new Position(p[0], p[1])).ToHashSet();
            return Floor.Restore(
                Id, FloorNumber, Zone, Width, Height, tiles,
                Rooms.Select(r => r.ToDomain()).ToList(),
                Enemies.Select(e => e.ToDomain()).ToList(),
                Items.Select(i => i.ToDomain()).ToList(),
                new Position(StairsX, StairsY),
                visible, explored);
        }

        private static int[] Flatten(TileType[,] tiles, int w, int h)
        {
            var flat = new int[w * h];
            for (int x = 0; x < w; x++)
                for (int y = 0; y < h; y++)
                    flat[x * h + y] = (int)tiles[x, y];
            return flat;
        }

        private static TileType[,] Unflatten(int[] flat, int w, int h)
        {
            var tiles = new TileType[w, h];
            for (int x = 0; x < w; x++)
                for (int y = 0; y < h; y++)
                    tiles[x, y] = (TileType)flat[x * h + y];
            return tiles;
        }
    }

    private record RoomSnapshot(int X, int Y, int Width, int Height)
    {
        public static RoomSnapshot From(Room r) => new(r.X, r.Y, r.Width, r.Height);
        public Room ToDomain() => new(X, Y, Width, Height);
    }

    private record EnemySnapshot(
        Guid Id, string Name, string FlavorTitle,
        int MaxHp, int CurrentHp, int Damage,
        int DodgeChance, int RatingsOnKill, int X, int Y,
        EnemyBehavior Behavior, int AwarenessMemory,
        int? LastKnownX, int? LastKnownY)
    {
        public static EnemySnapshot From(Enemy e) => new(
            e.Id, e.Name, e.FlavorTitle,
            e.MaxHp, e.CurrentHp, e.Damage,
            e.DodgeChance, e.RatingsOnKill,
            e.Position.X, e.Position.Y,
            e.Behavior, e.AwarenessMemory,
            e.LastKnownPlayerPos?.X, e.LastKnownPlayerPos?.Y);

        public Enemy ToDomain() => Enemy.Restore(
            Id, Name, FlavorTitle,
            MaxHp, CurrentHp, Damage,
            DodgeChance, RatingsOnKill,
            new Position(X, Y),
            Behavior, AwarenessMemory,
            LastKnownX.HasValue && LastKnownY.HasValue
                ? new Position(LastKnownX.Value, LastKnownY.Value)
                : null);
    }

    private record ItemSnapshot(
        Guid Id, string Name, string Description,
        ItemType Type, int EffectValue, int X, int Y)
    {
        public static ItemSnapshot From(Item i) => new(
            i.Id, i.Name, i.Description,
            i.Type, i.EffectValue, i.Position.X, i.Position.Y);

        public Item ToDomain() => Item.Restore(
            Id, Name, Description, Type, EffectValue, new Position(X, Y));
    }
}

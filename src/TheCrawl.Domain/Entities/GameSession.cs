using System.ComponentModel.DataAnnotations.Schema;
using TheCrawl.Domain.Enums;

namespace TheCrawl.Domain.Entities;

public class GameSession
{
    public Guid Id { get; private set; }
    public Player Player { get; private set; }
    [NotMapped] public Floor CurrentFloor { get; private set; }
    public GameStatus Status { get; private set; }
    public DateTime StartedAt { get; private set; }
    public DateTime? EndedAt { get; private set; }
    [NotMapped] public List<string> EventLog { get; private set; } = [];

    private GameSession() { }

    public GameSession(Player player, Floor startingFloor)
    {
        Id = Guid.NewGuid();
        Player = player;
        CurrentFloor = startingFloor;
        Status = GameStatus.Active;
        StartedAt = DateTime.UtcNow;
    }

    public void DescendToFloor(Floor floor)
    {
        Player.ClearFloor();
        CurrentFloor = floor;
        LogEvent($"Floor {floor.FloorNumber}. Zone: {floor.Zone}. The broadcast continues.");
    }

    public void EndSession(GameStatus outcome)
    {
        Status = outcome;
        EndedAt = DateTime.UtcNow;
    }

    public void LogEvent(string message)
    {
        EventLog.Add($"[{DateTime.UtcNow:HH:mm:ss}] {message}");
        if (EventLog.Count > 200)
            EventLog.RemoveAt(0);
    }

    public bool IsActive => Status == GameStatus.Active;

    public static GameSession Restore(
        Guid id, Player player, Floor floor, GameStatus status,
        DateTime startedAt, DateTime? endedAt, List<string> eventLog) => new()
    {
        Id = id,
        Player = player,
        CurrentFloor = floor,
        Status = status,
        StartedAt = startedAt,
        EndedAt = endedAt,
        EventLog = eventLog
    };
}

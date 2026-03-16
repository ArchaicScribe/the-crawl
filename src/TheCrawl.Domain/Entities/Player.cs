using TheCrawl.Domain.Enums;
using TheCrawl.Domain.ValueObjects;

namespace TheCrawl.Domain.Entities;

public class Player
{
    public Guid Id { get; private set; }
    public string Name { get; private set; }
    public PlayerClass Class { get; private set; }
    public Stats BaseStats { get; private set; }
    public int CurrentHp { get; private set; }
    public int MaxHp => BaseStats.MaxHp;
    public int Level { get; private set; }
    public int KillCount { get; private set; }
    public int FloorsCleared { get; private set; }
    public Position Position { get; private set; }
    public bool IsAlive => CurrentHp > 0;

    private Player() { } // EF Core

    public Player(string name, PlayerClass playerClass, Stats baseStats, Position startPosition)
    {
        Id = Guid.NewGuid();
        Name = name;
        Class = playerClass;
        BaseStats = baseStats;
        CurrentHp = baseStats.MaxHp;
        Level = 1;
        Position = startPosition;
    }

    public void MoveTo(Position position) => Position = position;

    public int TakeDamage(int amount)
    {
        var actual = Math.Max(0, amount);
        CurrentHp = Math.Max(0, CurrentHp - actual);
        return actual;
    }

    public void Heal(int amount) =>
        CurrentHp = Math.Min(MaxHp, CurrentHp + amount);

    public void RegisterKill() => KillCount++;

    public void ClearFloor() => FloorsCleared++;

    public void LevelUp(Stats bonus)
    {
        Level++;
        BaseStats = BaseStats.Add(bonus).Clamp();
        CurrentHp = Math.Min(CurrentHp, MaxHp);
    }

    public long BroadcastScore =>
        (long)BaseStats.Ratings * FloorsCleared * Math.Max(1, KillCount);
}

using TheCrawl.Domain.ValueObjects;

namespace TheCrawl.Domain.Entities;

public class Enemy
{
    public Guid Id { get; private set; }
    public string Name { get; private set; }
    public string FlavorTitle { get; private set; }
    public int MaxHp { get; private set; }
    public int CurrentHp { get; private set; }
    public int Damage { get; private set; }
    public int DodgeChance { get; private set; } // 0–100
    public int RatingsOnKill { get; private set; }
    public Position Position { get; private set; }
    public bool IsAlive => CurrentHp > 0;

    private Enemy() { }

    public Enemy(string name, string flavorTitle, int maxHp, int damage, int dodgeChance, int ratingsOnKill, Position position)
    {
        Id = Guid.NewGuid();
        Name = name;
        FlavorTitle = flavorTitle;
        MaxHp = maxHp;
        CurrentHp = maxHp;
        Damage = damage;
        DodgeChance = dodgeChance;
        RatingsOnKill = ratingsOnKill;
        Position = position;
    }

    public int TakeDamage(int amount)
    {
        var actual = Math.Max(0, amount);
        CurrentHp = Math.Max(0, CurrentHp - actual);
        return actual;
    }

    public void MoveTo(Position position) => Position = position;
}

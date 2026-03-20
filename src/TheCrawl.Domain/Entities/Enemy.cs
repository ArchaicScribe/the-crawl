using TheCrawl.Domain.Enums;
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

    // Awareness state — persisted in Redis so enemies remember across turns
    public EnemyBehavior Behavior { get; private set; } = EnemyBehavior.Wander;

    /// <summary>Turns remaining in Chase mode after losing sight of the player.</summary>
    public int AwarenessMemory { get; private set; }

    /// <summary>Last position the enemy saw the player at. Used while chasing.</summary>
    public Position? LastKnownPlayerPos { get; private set; }

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

    /// <summary>Called when this enemy spots the player.</summary>
    public void SpotPlayer(Position playerPos)
    {
        Behavior = EnemyBehavior.Chase;
        AwarenessMemory = 6;
        LastKnownPlayerPos = playerPos;
    }

    /// <summary>Called each turn the enemy is out of sight. Returns true when awareness expires.</summary>
    public bool DecrementAwareness()
    {
        if (Behavior != EnemyBehavior.Chase) return false;
        AwarenessMemory--;
        if (AwarenessMemory <= 0)
        {
            Behavior = EnemyBehavior.Wander;
            LastKnownPlayerPos = null;
            return true;
        }
        return false;
    }

    public static Enemy Restore(
        Guid id, string name, string flavorTitle,
        int maxHp, int currentHp, int damage,
        int dodgeChance, int ratingsOnKill, Position position,
        EnemyBehavior behavior = EnemyBehavior.Wander,
        int awarenessMemory = 0,
        Position? lastKnownPlayerPos = null) => new()
    {
        Id = id,
        Name = name,
        FlavorTitle = flavorTitle,
        MaxHp = maxHp,
        CurrentHp = currentHp,
        Damage = damage,
        DodgeChance = dodgeChance,
        RatingsOnKill = ratingsOnKill,
        Position = position,
        Behavior = behavior,
        AwarenessMemory = awarenessMemory,
        LastKnownPlayerPos = lastKnownPlayerPos
    };
}

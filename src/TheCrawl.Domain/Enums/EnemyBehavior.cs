namespace TheCrawl.Domain.Enums;

public enum EnemyBehavior
{
    Wander,  // No awareness of player — moves randomly
    Chase    // Player spotted — pursuing last known position
}

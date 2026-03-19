using TheCrawl.Domain.Entities;
using TheCrawl.Domain.ValueObjects;

namespace TheCrawl.Domain.Interfaces;

public interface IFovCalculator
{
    /// <summary>
    /// Computes the set of tiles visible from <paramref name="origin"/> within
    /// <paramref name="radius"/> tiles, blocked by walls in <paramref name="floor"/>.
    /// </summary>
    HashSet<Position> Calculate(Position origin, int radius, Floor floor);
}

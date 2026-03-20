using TheCrawl.Domain.Entities;
using TheCrawl.Domain.ValueObjects;

namespace TheCrawl.Domain.Interfaces;

public interface IPathfinder
{
    /// <summary>
    /// Returns the next step an entity at <paramref name="start"/> should take to reach
    /// <paramref name="goal"/> on the given floor, or null if no path exists.
    /// </summary>
    Position? NextStep(Position start, Position goal, Floor floor, IEnumerable<Position> occupied);
}

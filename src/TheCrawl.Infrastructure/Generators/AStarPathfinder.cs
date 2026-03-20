using TheCrawl.Domain.Entities;
using TheCrawl.Domain.Interfaces;
using TheCrawl.Domain.ValueObjects;

namespace TheCrawl.Infrastructure.Generators;

/// <summary>
/// A* pathfinder using Manhattan heuristic and cardinal-only movement.
/// Returns only the next step — callers re-invoke each turn.
/// </summary>
public class AStarPathfinder : IPathfinder
{
    public Position? NextStep(Position start, Position goal, Floor floor, IEnumerable<Position> occupied)
    {
        if (start == goal) return null;

        var blockedSet = new HashSet<Position>(occupied);

        // f = g + h; track via priority queue keyed on f-cost
        var open = new PriorityQueue<Position, int>();
        var cameFrom = new Dictionary<Position, Position>();
        var gCost = new Dictionary<Position, int> { [start] = 0 };

        open.Enqueue(start, Heuristic(start, goal));

        while (open.Count > 0)
        {
            var current = open.Dequeue();

            if (current == goal)
                return ReconstructFirstStep(cameFrom, start, goal);

            foreach (var neighbor in current.CardinalNeighbors())
            {
                // Must be walkable. Goal tile may be occupied by the player — allow it.
                if (!floor.IsWalkable(neighbor)) continue;
                if (blockedSet.Contains(neighbor) && neighbor != goal) continue;

                var tentativeG = gCost[current] + 1;
                if (gCost.TryGetValue(neighbor, out var existingG) && tentativeG >= existingG)
                    continue;

                cameFrom[neighbor] = current;
                gCost[neighbor] = tentativeG;
                open.Enqueue(neighbor, tentativeG + Heuristic(neighbor, goal));
            }
        }

        return null; // No path found
    }

    private static int Heuristic(Position a, Position b) =>
        Math.Abs(a.X - b.X) + Math.Abs(a.Y - b.Y);

    private static Position ReconstructFirstStep(Dictionary<Position, Position> cameFrom, Position start, Position goal)
    {
        var current = goal;
        while (cameFrom.TryGetValue(current, out var prev) && prev != start)
            current = prev;
        return current;
    }
}

namespace TheCrawl.Domain.ValueObjects;

public record Position(int X, int Y)
{
    public double DistanceTo(Position other) =>
        Math.Sqrt(Math.Pow(other.X - X, 2) + Math.Pow(other.Y - Y, 2));

    public IEnumerable<Position> CardinalNeighbors() =>
    [
        new(X, Y - 1),
        new(X, Y + 1),
        new(X - 1, Y),
        new(X + 1, Y)
    ];

    public IEnumerable<Position> AllNeighbors()
    {
        for (var dx = -1; dx <= 1; dx++)
        for (var dy = -1; dy <= 1; dy++)
        {
            if (dx == 0 && dy == 0) continue;
            yield return new Position(X + dx, Y + dy);
        }
    }
}

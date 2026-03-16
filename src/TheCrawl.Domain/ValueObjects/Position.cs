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
}

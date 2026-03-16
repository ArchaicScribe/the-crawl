using TheCrawl.Domain.ValueObjects;

namespace TheCrawl.Domain.Entities;

public class Room
{
    public Guid Id { get; private set; }
    public int X { get; private set; }
    public int Y { get; private set; }
    public int Width { get; private set; }
    public int Height { get; private set; }

    private Room() { }

    public Room(int x, int y, int width, int height)
    {
        Id = Guid.NewGuid();
        X = x;
        Y = y;
        Width = width;
        Height = height;
    }

    public Position Center => new(X + Width / 2, Y + Height / 2);

    public bool Overlaps(Room other) =>
        X < other.X + other.Width &&
        X + Width > other.X &&
        Y < other.Y + other.Height &&
        Y + Height > other.Y;

    public bool Contains(Position pos) =>
        pos.X >= X && pos.X < X + Width &&
        pos.Y >= Y && pos.Y < Y + Height;
}

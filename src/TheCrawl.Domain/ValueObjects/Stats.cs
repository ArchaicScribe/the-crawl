namespace TheCrawl.Domain.ValueObjects;

public record Stats(int Muscle, int Nerve, int Grit, int Wit, int Ratings)
{
    public int MaxHp => Grit * 10;

    public Stats Add(Stats other) => new(
        Muscle + other.Muscle,
        Nerve + other.Nerve,
        Grit + other.Grit,
        Wit + other.Wit,
        Ratings + other.Ratings
    );

    public Stats Clamp(int min = 1, int max = 99) => new(
        Math.Clamp(Muscle, min, max),
        Math.Clamp(Nerve, min, max),
        Math.Clamp(Grit, min, max),
        Math.Clamp(Wit, min, max),
        Math.Clamp(Ratings, min, max)
    );
}

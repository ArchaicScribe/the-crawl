using TheCrawl.Domain.Entities;
using TheCrawl.Domain.Enums;
using TheCrawl.Domain.Interfaces;
using TheCrawl.Domain.ValueObjects;

namespace TheCrawl.Infrastructure.Generators;

/// <summary>
/// Recursive shadowcasting FOV algorithm (Björn Björnsson).
/// Divides the map into 8 octants and scans outward from the origin,
/// tracking shadow intervals cast by walls. Any tile not covered by a
/// shadow is visible. O(visible tiles) time complexity.
/// </summary>
public class FovCalculator : IFovCalculator
{
    public const int DefaultRadius = 8;

    // Each octant maps local (col, row) coordinates to global map coordinates.
    // col=0 is the octant axis (straight ahead), col=row is the 45° boundary.
    // The eight transforms rotate/reflect a single scan pattern across all directions.
    private static readonly Func<Position, int, int, Position>[] OctantTransforms =
    [
        (o, col, r) => new Position(o.X + r,   o.Y - col), // E → NE
        (o, col, r) => new Position(o.X + col, o.Y - r  ), // N → NE
        (o, col, r) => new Position(o.X - col, o.Y - r  ), // N → NW
        (o, col, r) => new Position(o.X - r,   o.Y - col), // W → NW
        (o, col, r) => new Position(o.X - r,   o.Y + col), // W → SW
        (o, col, r) => new Position(o.X - col, o.Y + r  ), // S → SW
        (o, col, r) => new Position(o.X + col, o.Y + r  ), // S → SE
        (o, col, r) => new Position(o.X + r,   o.Y + col), // E → SE
    ];

    public HashSet<Position> Calculate(Position origin, int radius, Floor floor)
    {
        var visible = new HashSet<Position> { origin }; // origin is always visible

        foreach (var transform in OctantTransforms)
            ScanOctant(visible, floor, origin, radius, transform, row: 1, lo: 0f, hi: 1f);

        return visible;
    }

    /// <summary>
    /// Scans one octant outward from <paramref name="row"/> to <paramref name="radius"/>.
    /// <paramref name="lo"/> and <paramref name="hi"/> define the visible slope interval
    /// within this octant (0.0 = axis, 1.0 = 45° boundary). Walls narrow the interval
    /// and trigger recursive sub-scans for the obstructed region.
    /// </summary>
    private static void ScanOctant(
        HashSet<Position> visible,
        Floor floor,
        Position origin,
        int radius,
        Func<Position, int, int, Position> transform,
        int row,
        float lo,
        float hi)
    {
        if (lo >= hi || row > radius) return;

        float nextLo = lo;
        bool prevWasWall = false;

        for (int col = 0; col <= row; col++)
        {
            // Slope interval of this cell within the octant.
            // Using (row + 0.5) as denominator places the sample at the cell's far edge,
            // which avoids missing thin diagonal corridors.
            float cellLo = (col - 0.5f) / (row + 0.5f);
            float cellHi = (col + 0.5f) / (row + 0.5f);

            // Cell is entirely above the visible cone — no more cells in this row matter.
            if (cellLo >= hi) break;

            // Cell is entirely below the visible cone — not yet in range.
            if (cellHi <= lo) continue;

            var pos = transform(origin, col, row);
            bool inBounds = pos.X >= 0 && pos.X < floor.Width
                         && pos.Y >= 0 && pos.Y < floor.Height;

            // Circular clamp: keeps FOV round instead of diamond-shaped.
            if (inBounds && col * col + row * row <= radius * radius)
                visible.Add(pos);

            bool isWall = !inBounds || floor.GetTile(pos) == TileType.Wall;

            if (isWall)
            {
                if (!prevWasWall)
                {
                    // Floor-to-wall transition: recurse for the slice below this wall
                    // (from nextLo up to where this wall starts).
                    if (nextLo < cellLo)
                        ScanOctant(visible, floor, origin, radius, transform, row + 1, nextLo, cellLo);
                }
                // Track where this shadow block ends so we can resume after it.
                nextLo = cellHi;
                prevWasWall = true;
            }
            else
            {
                if (prevWasWall)
                {
                    // Wall-to-floor transition: resume the main scan from after the shadow.
                    lo = nextLo;
                }
                prevWasWall = false;
            }
        }

        // If the last cell in the row was floor, continue scanning the next row
        // with the remaining visible interval.
        if (!prevWasWall)
            ScanOctant(visible, floor, origin, radius, transform, row + 1, nextLo, hi);
    }
}

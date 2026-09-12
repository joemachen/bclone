using Bclone.Sim.Core;

namespace Bclone.Sim.World;

/// <summary>
/// ⭐⭐ The tiles a straight segment touches, and whether they are all passable — <b>the
/// question string-pulling asks</b> (gridless slice 4, D356).
/// </summary>
/// <remarks>
/// <para>
/// A villager's route is still the tile cost field's staircase; this is what lets them walk the
/// straight line across it instead. A leg may cut from here to a route tile only if every tile
/// under the segment is passable — otherwise the string bends there.
/// </para>
/// <para>
/// <b>⛔ EXACT, AND CONSERVATIVE AT CORNERS.</b> Exact because a leg is planned from the answer,
/// so it is sim state and two machines must agree on it: the walk is Amanatides–Woo over the
/// grid with the crossing parameters kept as <b>rationals in fixed-point raw bits</b>, compared by
/// cross-multiplication in <see cref="Int128"/> — no float, no <see cref="Fixed"/> division, no
/// square root (D2). Conservative because a segment that passes exactly through the corner where
/// two water tiles meet visits <em>both</em> tiles beside the corner, so a villager cannot squeeze
/// between two ponds. Over-counting only bends the string a tile early; under-counting would let
/// somebody wade.
/// </para>
/// <para>
/// ⚠️ <b>Endpoints may be anywhere</b>, not only tile centres: a villager who changes their mind
/// mid-leg re-plans from an off-centre point (`VillagerPointTests.ALegIsDroppedWhenTheTargetChanges`).
/// </para>
/// </remarks>
public static class LineOfSight
{
    /// <summary>Whether every tile under the segment from <paramref name="from"/> to <paramref name="to"/> can be walked.</summary>
    public static bool Clear(GeneratedMap map, Point from, Point to)
    {
        ArgumentNullException.ThrowIfNull(map);

        bool clear = true;
        Walk(from, to, tile =>
        {
            if (!map.Contains(tile) || !TerrainRules.IsPassable(map.TerrainAt(tile)))
            {
                clear = false;
                return false;
            }

            return true;
        });

        return clear;
    }

    /// <summary>Every tile the segment touches, in the order it touches them. For the guards.</summary>
    public static List<GridPos> TilesCrossed(Point from, Point to)
    {
        var tiles = new List<GridPos>();
        Walk(from, to, tile =>
        {
            tiles.Add(tile);
            return true;
        });

        return tiles;
    }

    /// <summary>
    /// The grid walk. <paramref name="visit"/> returns false to stop early.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The parameter <c>t</c> runs from 0 at <paramref name="from"/> to 1 at <paramref name="to"/>.
    /// The next vertical grid line is crossed at <c>t = (nextX − x0) / dx</c>, the next horizontal
    /// at <c>t = (nextY − y0) / dy</c>; whichever is smaller is crossed first, and equal means a
    /// corner. Every quantity is a raw fixed-point <c>long</c>, so <c>a/b &lt; c/d</c> is
    /// <c>a·d &lt; c·b</c> with the signs of the denominators made positive first — in
    /// <see cref="Int128"/>, because two 64-bit raws multiplied do not fit a <c>long</c>.
    /// </para>
    /// <para>
    /// The walk ends when the current tile is <paramref name="to"/>'s tile, and is bounded by the
    /// Manhattan distance between the two tiles plus the corner visits, so it cannot spin.
    /// </para>
    /// </remarks>
    private static void Walk(Point from, Point to, Func<GridPos, bool> visit)
    {
        GridPos tile = from.ToTile();
        GridPos last = to.ToTile();

        if (!visit(tile) || tile == last)
        {
            return;
        }

        long x0 = from.X.RawBits;
        long y0 = from.Y.RawBits;
        long dx = to.X.RawBits - x0;
        long dy = to.Y.RawBits - y0;
        int stepX = dx > 0 ? 1 : dx < 0 ? -1 : 0;
        int stepY = dy > 0 ? 1 : dy < 0 ? -1 : 0;

        // Denominators made positive so the cross-multiplied comparison keeps its direction;
        // an axis that does not move never crosses a line on that axis.
        long denX = dx == 0 ? 0 : Math.Abs(dx);
        long denY = dy == 0 ? 0 : Math.Abs(dy);

        int budget = (Math.Abs(last.X - tile.X) + Math.Abs(last.Y - tile.Y)) * 2 + 2;

        while (budget-- > 0)
        {
            // Distance along t to the next grid line on each axis, as numerator over denominator.
            long numX = denX == 0 ? 0 : Math.Abs(NextLine(tile.X, stepX) - x0);
            long numY = denY == 0 ? 0 : Math.Abs(NextLine(tile.Y, stepY) - y0);

            int order = denX == 0 ? 1 : denY == 0 ? -1 : Compare(numX, denX, numY, denY);

            if (order == 0)
            {
                // ⛔ A corner: both tiles beside it are touched, then the diagonal one.
                var beside = new GridPos(tile.X + stepX, tile.Y);
                var above = new GridPos(tile.X, tile.Y + stepY);
                if (!visit(beside) || !visit(above))
                {
                    return;
                }

                tile = new GridPos(tile.X + stepX, tile.Y + stepY);
            }
            else if (order < 0)
            {
                tile = new GridPos(tile.X + stepX, tile.Y);
            }
            else
            {
                tile = new GridPos(tile.X, tile.Y + stepY);
            }

            if (!visit(tile) || tile == last)
            {
                return;
            }
        }
    }

    /// <summary>The raw coordinate of the next grid line in the direction of travel from inside <paramref name="tile"/>.</summary>
    private static long NextLine(int tile, int step) =>
        Fixed.FromInt(step > 0 ? tile + 1 : tile).RawBits;

    /// <summary><c>a/b</c> against <c>c/d</c>, both denominators positive, exactly.</summary>
    private static int Compare(long a, long b, long c, long d)
    {
        Int128 left = (Int128)a * d;
        Int128 right = (Int128)c * b;
        return left.CompareTo(right);
    }
}

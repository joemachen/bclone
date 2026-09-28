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
    /// <summary>
    /// Clear of water AND of buildings — save the two footprints a leg may cross: the one it
    /// leaves and the one it arrives at (D383).
    /// </summary>
    public static bool Clear(
        GeneratedMap map, IObstacles obstacles, Point from, Point to,
        IReadOnlyList<GridPos> leaving, IReadOnlyList<GridPos> arriving)
    {
        ArgumentNullException.ThrowIfNull(map);
        ArgumentNullException.ThrowIfNull(obstacles);
        bool clear = true;
        Walk(from, to, (was, tile) =>
        {
            if (!map.Contains(tile) || !TerrainRules.IsPassable(map.TerrainAt(tile)))
            {
                clear = false;
                return false;
            }

            if (obstacles.StandsOn(tile) && !Among(leaving, tile) && !Among(arriving, tile))
            {
                clear = false;
                return false;
            }

            // ⭐⭐ AND IT MAY NOT CROSS A FENCE (D404, `fences-as-walls.md §3.4`). Without this a
            // villager routes round a yard — the cost field refuses the edge — and then *draws*
            // straight through it, because a leg is a straight line to the furthest visible route
            // tile (D356). The routing and the drawing have to read the same wall.
            if (AWallBetween(obstacles, was, tile))
            {
                clear = false;
                return false;
            }

            return true;
        },
        (above, diagonal) =>
        {
            // ⭐ The fourth edge at a corner (D404) — see Walk.
            if (AWallBetween(obstacles, above, diagonal))
            {
                clear = false;
                return false;
            }

            return true;
        });
        return clear;
    }

    /// <summary>
    /// Whether a move from one point to another goes through no wall (D404) — the physical
    /// question, where <see cref="Clear(GeneratedMap, IObstacles, Point, Point, IReadOnlyList{GridPos}, IReadOnlyList{GridPos})"/>
    /// is the stricter one a planned leg must answer.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A move through a grid corner touches the four edges that meet there at a single point, so
    /// it goes <em>through</em> a wall only if <b>both</b> ways round the corner are walled — which
    /// is exactly the move that slips diagonally into a yard between two of its fences. A leg is
    /// held to more (it may not graze a post at all), because it is planned and can simply go
    /// round; this is asked of what actually happened, including a villager set down on a
    /// building's standing place at the end of a walk (<c>BehaviorSystem.Arrive</c>).
    /// </para>
    /// <para>Asked of every villager's every move by <c>NoStepEverCrossesAWall</c>.</para>
    /// </remarks>
    public static bool ClearOfWalls(IObstacles obstacles, Point from, Point to)
    {
        ArgumentNullException.ThrowIfNull(obstacles);
        List<GridPos> tiles = TilesCrossed(from, to);
        for (int i = 1; i < tiles.Count; i++)
        {
            // A corner is listed as the tile, the two beside the corner, then the diagonal.
            if (i + 1 < tiles.Count && tiles[i].ManhattanDistanceTo(tiles[i + 1]) == 2)
            {
                if (i + 2 < tiles.Count)
                {
                    GridPos corner = tiles[i - 1];
                    GridPos beside = tiles[i];
                    GridPos above = tiles[i + 1];
                    GridPos diagonal = tiles[i + 2];
                    bool besideShut = AWallBetween(obstacles, corner, beside) || AWallBetween(obstacles, beside, diagonal);
                    bool aboveShut = AWallBetween(obstacles, corner, above) || AWallBetween(obstacles, above, diagonal);
                    if (besideShut && aboveShut)
                    {
                        return false;
                    }
                }

                // A move that ends ON the corner touches it and goes no further.
                i += 2;
                continue;
            }

            if (AWallBetween(obstacles, tiles[i - 1], tiles[i]))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>Whether a wall stands on the edge a unit step crosses — false for no step at all.</summary>
    private static bool AWallBetween(IObstacles obstacles, GridPos from, GridPos to)
    {
        byte crossing = ZoneMap.EdgeBit(to.X - from.X, to.Y - from.Y);
        return crossing != 0 && (obstacles.WallsOn(from) & crossing) != 0;
    }

    private static bool Among(IReadOnlyList<GridPos> tiles, GridPos tile)
    {
        for (int i = 0; i < tiles.Count; i++)
        {
            if (tiles[i] == tile)
            {
                return true;
            }
        }

        return false;
    }

    public static bool Clear(GeneratedMap map, Point from, Point to)
    {
        ArgumentNullException.ThrowIfNull(map);

        bool clear = true;
        Walk(from, to, (_, tile) =>
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
        Walk(from, to, (_, tile) =>
        {
            tiles.Add(tile);
            return true;
        });

        return tiles;
    }

    /// <summary>
    /// ⭐ Every tile a footstep from <paramref name="from"/> to <paramref name="to"/> ENTERS, in order
    /// — never <paramref name="from"/>'s own tile, and through a grid corner only the diagonal tile
    /// (D424).
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Joe: *"what's going on with these pathway segments? can they be a smooth path?"*</b> A step
    /// trod only the tile it landed on, and a step on a worn lane is longer than a tile (clock B:
    /// packed ground costs 8 of 10, so a step is 1.25 tiles). Every walker between the same two doors
    /// walks the same leg in the same steps, so they all skipped <em>the same tiles</em>, every trip —
    /// a straight packed lane with a tile nobody ever stood on (`o.## ###o###`, shipped 12345 at year
    /// 30), drawn as dashes. A footstep marks the ground it passes over.
    /// </para>
    /// <para>
    /// ⚠️ <b>Not <see cref="TilesCrossed"/>'s corners.</b> That walk is conservative — a line through a
    /// corner touches both tiles beside it, which is right for a wall and wrong for a foot: a 45° walk
    /// would tread three tiles a step and wear a thick staircase (D414 names the same trap).
    /// </para>
    /// </remarks>
    public static void Footprints(Point from, Point to, Action<GridPos> tread)
    {
        ArgumentNullException.ThrowIfNull(tread);
        Walk(
            from,
            to,
            (was, tile) =>
            {
                if (was != tile)
                {
                    tread(tile);
                }

                return true;
            },
            cornersTouchBoth: false);
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
    private static void Walk(
        Point from,
        Point to,
        Func<GridPos, GridPos, bool> visit,
        Func<GridPos, GridPos, bool>? alsoCrossed = null,
        bool cornersTouchBoth = true)
    {
        GridPos tile = from.ToTile();
        GridPos last = to.ToTile();

        if (!visit(tile, tile) || tile == last)
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
            GridPos was = tile;
            // Distance along t to the next grid line on each axis, as numerator over denominator.
            long numX = denX == 0 ? 0 : Math.Abs(NextLine(tile.X, stepX) - x0);
            long numY = denY == 0 ? 0 : Math.Abs(NextLine(tile.Y, stepY) - y0);

            int order = denX == 0 ? 1 : denY == 0 ? -1 : Compare(numX, denX, numY, denY);

            if (order == 0 && !cornersTouchBoth)
            {
                // A FOOTSTEP THROUGH A CORNER STEPS DIAGONALLY (D424) — it touches neither tile
                // beside it. A step that ENDS on the corner ends in the tile the point floors to.
                var besideTile = new GridPos(tile.X + stepX, tile.Y);
                var aboveTile = new GridPos(tile.X, tile.Y + stepY);
                if (besideTile == last || aboveTile == last)
                {
                    visit(tile, last);
                    return;
                }

                tile = new GridPos(tile.X + stepX, tile.Y + stepY);
                if (!visit(was, tile) || tile == last)
                {
                    return;
                }

                continue;
            }

            if (order == 0)
            {
                // ⛔ A corner: both tiles beside it are touched, then the diagonal one.
                var beside = new GridPos(tile.X + stepX, tile.Y);
                var above = new GridPos(tile.X, tile.Y + stepY);

                // ⭐ BOTH ARE ENTERED FROM THE CORNER TILE (D404), not from each other — they do
                // not touch. A line through a corner crosses both of those edges, so a fence on
                // either is a fence this leg may not cross.
                if (!visit(tile, beside) || !visit(tile, above))
                {
                    return;
                }

                // ⛔⛔ A LINE THAT ENDS ON THE CORNER ENDS IN ONE OF THE TWO (D404). A point on a grid
                // corner is filed under the tile it floors to, which is `beside` or `above` — never
                // the diagonal — so the old test (`tile == last` after the diagonal) never matched
                // and the walk ran on past the end until its budget ran out. A villager stepping
                // (−2.5, −1.7) → (−2, −2) was walked through seven tiles, one of them across the
                // Fletchers' fence. Every leg `Clear` judged that way was judged by ground it
                // never touches.
                if (beside == last || above == last)
                {
                    return;
                }

                tile = new GridPos(tile.X + stepX, tile.Y + stepY);
                if (!visit(beside, tile))
                {
                    return;
                }

                // ⭐ AND THE FOURTH EDGE (D404): a line through a corner touches all four edges that
                // meet there, and a fence on any of them is a post the leg would graze. The tile is
                // entered once (from `beside`), so the edge from `above` is asked on its own —
                // otherwise `TilesCrossed` would list the diagonal twice. Asked before the arrival
                // test, so a leg that ENDS past a corner is held to it too.
                if ((alsoCrossed is not null && !alsoCrossed(above, tile)) || tile == last)
                {
                    return;
                }

                continue;
            }
            else if (order < 0)
            {
                tile = new GridPos(tile.X + stepX, tile.Y);
            }
            else
            {
                tile = new GridPos(tile.X, tile.Y + stepY);
            }

            if (!visit(was, tile) || tile == last)
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

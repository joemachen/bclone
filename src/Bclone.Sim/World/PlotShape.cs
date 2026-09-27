using Bclone.Sim.Core;

namespace Bclone.Sim.World;

/// <summary>
/// A household's plot, derived from its house (D386, D411, <c>specs/organic-housing.md §3, §9</c>):
/// the tiles the house stands on, the yard behind and beside it, and the band across its front that
/// is its lane.
/// </summary>
/// <remarks>
/// <para>
/// ⭐ <b>Derived, never stored.</b> The front tile the chooser pointed at, which way the house faces
/// and the household's id make a plot, and everything below is arithmetic on them. The zone map keeps
/// the result as an index (<c>ZoneMap.ClaimPlot</c>), maintained where a home changes; this is the one
/// place the arithmetic lives, so the index, the chooser and the view cannot disagree about where a
/// fence runs.
/// </para>
/// <para>
/// ⭐⭐ <b>THE YARD FOLLOWS THE HOUSE, AT ANY ANGLE (D411).</b> Until D411 a plot was one of four
/// rectangles, the house flush on its front row and the lane the row beyond — and a street of them
/// read as suburbia (Joe, three times; §9.2). Now a house faces its path at whatever angle the path
/// asks, and the yard is a rectangle in the <em>house's</em> frame — past each end by a hashed reach,
/// behind by a hashed depth, nothing in front — whose tile centres are the plot (the centre rule,
/// D319). The fence is still walls on those tiles' edges (D404, Joe's call: *"walls on tile edges"*),
/// so a yard turned 30° is a stepped outline round a turned house, which he accepted.
/// </para>
/// <para>
/// ⛔ <b>The same rotation as <see cref="Footprint.Covers(Point)"/>, in the same order.</b>
/// <see cref="Fixed"/> multiplication is not associative; <see cref="Point.RotatedBy"/> is the one copy.
/// </para>
/// </remarks>
public readonly record struct PlotShape
{
    /// <summary>The tile the chooser pointed at — the house's own tile, one of its two.</summary>
    public required GridPos Front { get; init; }

    public required Angle Facing { get; init; }

    /// <summary>The tiles the house stands on (its footprint's, by the centre rule).</summary>
    public required IReadOnlyList<GridPos> House { get; init; }

    /// <summary>Every tile of the plot, house tiles included, nearest the door first.</summary>
    /// <remarks>
    /// ⭐ The order is the gate's: <c>ZoneMap.FenceEdges</c> opens the first yard edge onto the lane
    /// in this order, so nearest the door first is a gate beside the door.
    /// </remarks>
    public required IReadOnlyList<GridPos> Tiles { get; init; }

    /// <summary>The band across the front — the lane no plot may claim (§3.2), nearest the door first.</summary>
    public required IReadOnlyList<GridPos> Lane { get; init; }

    /// <summary>The lane tile in front of the house's door — where the household steps out.</summary>
    public required GridPos Door { get; init; }

    /// <summary>
    /// Yard tiles the rectangle covered that no gate could reach (<see cref="OneWayIn"/>) — ground
    /// the family does not get, priced as a clipped tile is (D411).
    /// </summary>
    public int Unreached { get; init; }

    /// <summary>
    /// The quarter a facing is nearest — north for <see cref="Angle.Zero"/>, then east, south, west.
    /// </summary>
    /// <remarks>
    /// Rounded, not floored (D411): a house turned 350° is a north-facing house for the anchor rule
    /// and the compass word, not a west-facing one. Identical to the floor at the four quarters.
    /// </remarks>
    public static GridPos LaneDirection(Angle facing) => (((facing.Raw + 0x2000) & 0xFFFF) >> 14) switch
    {
        0 => new GridPos(0, -1),
        1 => new GridPos(1, 0),
        2 => new GridPos(0, 1),
        _ => new GridPos(-1, 0),
    };

    /// <summary>The four square facings — north, east, south, west — for posing a square house.</summary>
    /// <remarks>Since D411 a house faces its path at any 1/64 turn; these are four of them.</remarks>
    public static readonly IReadOnlyList<Angle> Quarters = new[]
    {
        Angle.Zero, Angle.Right, Angle.Right + Angle.Right, Angle.Right + Angle.Right + Angle.Right,
    };

    /// <summary>One of a config's hashed values for this household — ⛔ never an <c>Rng</c> draw.</summary>
    /// <remarks>
    /// A different salt per question, so a household's gap and its yard's depth are not the same
    /// choice read twice. The same valley twice gets the same village.
    /// </remarks>
    public static int ByHash(int householdId, int salt, IReadOnlyList<int> values)
    {
        ArgumentNullException.ThrowIfNull(values);
        uint mix = unchecked(((uint)householdId * 2654435761u) ^ ((uint)salt * 2246822519u));
        mix ^= mix >> 15;
        mix = unchecked(mix * 3266489917u);
        mix ^= mix >> 13;
        return values[(int)(mix % (uint)values.Count)];
    }

    /// <summary>The salts <see cref="ByHash"/> is asked with — one per question, stated.</summary>
    public const int SideSalt = 1;

    /// <inheritdoc cref="SideSalt"/>
    public const int BackSalt = 2;

    /// <inheritdoc cref="SideSalt"/>
    public const int OtherSideSalt = 5;

    /// <inheritdoc cref="SideSalt"/>
    public const int WhichSideSalt = 6;

    /// <inheritdoc cref="SideSalt"/>
    public const int SetbackSalt = 3;

    /// <inheritdoc cref="SideSalt"/>
    public const int GapSalt = 4;

    /// <summary>
    /// The plot of a house standing at <paramref name="centre"/>, facing <paramref name="facing"/>,
    /// on <paramref name="house"/>, with a yard reaching <paramref name="left"/> past one end,
    /// <paramref name="right"/> past the other and <paramref name="back"/> behind (§9.5 P4).
    /// </summary>
    /// <remarks>
    /// <para>
    /// The house is two tiles wide and one deep: in its own frame it spans x ∈ [−1, 1] and
    /// y ∈ [−½, ½], the door on the −y edge (the frame <see cref="Footprint"/> turns and the view
    /// draws). The yard is x ∈ [−(1 + left), 1 + right], y ∈ [−½, ½ + back]; the lane is the band in
    /// front, y ∈ [−1½, −½), as wide as the yard. A tile belongs to whichever its centre falls in.
    /// </para>
    /// <para>
    /// ⛔ <b>One end always reaches past the house</b> — found on the first picture: a yard exactly
    /// as wide as its house sits wholly behind it, touches the lane nowhere, and has no gate, so
    /// <see cref="OneWayIn"/> kept none of it and a house stood with no yard at all. D386's side yard
    /// on the front row was the gate's tile, and the long end is that tile.
    /// </para>
    /// </remarks>
    public static PlotShape Of(
        GridPos front, Angle facing, Point centre, IReadOnlyList<GridPos> house, Fixed left, Fixed right, Fixed back)
    {
        ArgumentNullException.ThrowIfNull(house);
        Fixed half = Fixed.FromRatio(1, 2);
        Fixed toLeft = -(Fixed.FromInt(1) + left);
        Fixed toRight = Fixed.FromInt(1) + right;
        // ⭐ The lane runs a tile past the yard at each end (D411): a turned yard's gate tile meets
        // the band in front of it edge to edge far more often, and the lane is the street, not a
        // strip exactly the plot's width.
        Fixed one = Fixed.FromInt(1);
        Fixed reach = (toRight > -toLeft ? toRight : -toLeft) + one;
        Fixed laneFront = -Fixed.FromRatio(3, 2);
        Fixed behind = half + back;

        // The door's point: a tile in front of the house's centre.
        Point door = centre + new Point(Fixed.Zero, -Fixed.FromInt(1)).RotatedBy(facing);

        // Every tile the yard or its lane could reach, row by row: in the house's frame the farthest
        // corner is (reach, max(behind, 1½)) from the centre, whichever way it is turned, so a square
        // of that corner's distance and a tile more holds every centre that could be in.
        Fixed deepest = behind > -laneFront ? behind : -laneFront;
        int bound = ((reach * reach) + (deepest * deepest)).Sqrt().ToInt() + 1;
        GridPos at = centre.ToTile();
        var yard = new List<GridPos>();
        var lane = new List<GridPos>();
        var onTheHouse = new HashSet<GridPos>(house);
        for (int dy = -bound; dy <= bound; dy++)
        {
            for (int dx = -bound; dx <= bound; dx++)
            {
                var tile = new GridPos(at.X + dx, at.Y + dy);
                if (onTheHouse.Contains(tile))
                {
                    continue;
                }

                Point local = (Point.CentreOf(tile) - centre).RotatedBy(-facing);
                if (local.Y >= -half && local.Y <= behind)
                {
                    if (local.X >= toLeft && local.X <= toRight)
                    {
                        yard.Add(tile);
                    }

                    continue;
                }
                else if (local.Y >= laneFront && local.Y < -half && local.X >= toLeft - one && local.X <= toRight + one)
                {
                    lane.Add(tile);
                }
            }
        }

        int rasterised = yard.Count;
        Bridge(yard, lane, onTheHouse, centre + new Point(Fixed.Zero, back / Fixed.FromInt(2)).RotatedBy(facing));
        List<GridPos> laneInOrder = NearestFirst(lane, door);
        List<GridPos> yardInOrder = OneWayIn(NearestFirst(yard, door), lane);
        var tiles = new List<GridPos>(house.Count + yardInOrder.Count);
        tiles.AddRange(house);
        tiles.AddRange(yardInOrder);

        GridPos doorTile = door.ToTile();
        if (!lane.Contains(doorTile) && laneInOrder.Count > 0)
        {
            doorTile = laneInOrder[0];
        }

        return new PlotShape
        {
            Front = front,
            Facing = facing,
            House = house,
            Tiles = tiles,
            Lane = laneInOrder,
            Door = doorTile,
            Unreached = System.Math.Max(0, rasterised - yardInOrder.Count),
        };
    }

    /// <summary>
    /// ⭐ Join every two yard tiles that meet only at a corner with the tile between them nearer the
    /// yard's middle (D411), so a turned yard is one piece a gate can open onto.
    /// </summary>
    /// <remarks>
    /// ⛔ Found by <c>AHouseFacesThePathInFrontOfIt</c>: a yard turned 45° has tile centres that meet
    /// only corner to corner, a villager steps edge to edge (D404), so most of it was no gate's and
    /// the chooser — rightly — preferred the square facing that kept its yard. The worst angle a
    /// path can ask for was the one a house could not take. Never onto the house or the lane; the
    /// fence goes round what is joined.
    /// </remarks>
    private static void Bridge(List<GridPos> yard, List<GridPos> lane, HashSet<GridPos> house, Point middle)
    {
        var inYard = new HashSet<GridPos>(yard);
        var onTheLane = new HashSet<GridPos>(lane);
        for (int i = 0; i < yard.Count; i++)
        {
            GridPos t = yard[i];
            foreach ((int dx, int dy) in Corners)
            {
                var diagonal = new GridPos(t.X + dx, t.Y + dy);
                var across = new GridPos(t.X + dx, t.Y);
                var down = new GridPos(t.X, t.Y + dy);
                if (!inYard.Contains(diagonal) || inYard.Contains(across) || inYard.Contains(down))
                {
                    continue;
                }

                bool acrossFree = !house.Contains(across) && !onTheLane.Contains(across);
                bool downFree = !house.Contains(down) && !onTheLane.Contains(down);
                if (!acrossFree && !downFree)
                {
                    continue;
                }

                GridPos join = !downFree || (acrossFree && Nearer(across, down, middle)) ? across : down;
                inYard.Add(join);
                yard.Add(join);
            }
        }
    }

    /// <summary>The four diagonal steps, in a stated order.</summary>
    private static readonly (int Dx, int Dy)[] Corners = { (1, -1), (1, 1), (-1, 1), (-1, -1) };

    /// <summary>Whether a's centre is nearer the point than b's — row order on a tie, stated.</summary>
    private static bool Nearer(GridPos a, GridPos b, Point to)
    {
        Point da = Point.CentreOf(a) - to;
        Point db = Point.CentreOf(b) - to;
        Fixed ra = (da.X * da.X) + (da.Y * da.Y);
        Fixed rb = (db.X * db.X) + (db.Y * db.Y);
        return ra != rb ? ra < rb : (a.Y != b.Y ? a.Y < b.Y : a.X < b.X);
    }

    /// <summary>
    /// ⭐ The yard the gate can reach: the tiles joined edge to edge to the first yard tile beside
    /// the lane (the gate's, <c>ZoneMap.FenceEdges</c>), in the order given (D411).
    /// </summary>
    /// <remarks>
    /// ⛔ Found by counting: a turned rectangle's tile centres can meet only at a corner, and a yard
    /// in two pieces has a half no gate reaches — <c>SimWorld.GateOpensAt</c> refused about half of
    /// every angled plot tried, and the chooser fell back to a square one. A villager steps edge to
    /// edge (D404), so the yard is what one gate opens onto; a corner-only piece is not fenced in.
    /// </remarks>
    private static List<GridPos> OneWayIn(List<GridPos> yard, List<GridPos> lane)
    {
        var onTheLane = new HashSet<GridPos>(lane);
        var inYard = new HashSet<GridPos>(yard);
        GridPos? gate = null;
        for (int i = 0; i < yard.Count && gate is null; i++)
        {
            GridPos t = yard[i];
            if (onTheLane.Contains(new GridPos(t.X, t.Y - 1)) || onTheLane.Contains(new GridPos(t.X + 1, t.Y))
                || onTheLane.Contains(new GridPos(t.X, t.Y + 1)) || onTheLane.Contains(new GridPos(t.X - 1, t.Y)))
            {
                gate = t;
            }
        }

        if (gate is not GridPos start)
        {
            return new List<GridPos>();
        }

        var reached = new HashSet<GridPos> { start };
        var queue = new Queue<GridPos>();
        queue.Enqueue(start);
        while (queue.Count > 0)
        {
            GridPos at = queue.Dequeue();
            foreach (GridPos next in new[]
            {
                new GridPos(at.X, at.Y - 1), new GridPos(at.X + 1, at.Y),
                new GridPos(at.X, at.Y + 1), new GridPos(at.X - 1, at.Y),
            })
            {
                if (inYard.Contains(next) && reached.Add(next))
                {
                    queue.Enqueue(next);
                }
            }
        }

        var kept = new List<GridPos>(reached.Count);
        for (int i = 0; i < yard.Count; i++)
        {
            if (reached.Contains(yard[i]))
            {
                kept.Add(yard[i]);
            }
        }

        return kept;
    }

    /// <summary>Tiles sorted by their centre's distance from a point, then row order — stated, so the gate is the same edge every run.</summary>
    private static List<GridPos> NearestFirst(List<GridPos> tiles, Point from)
    {
        // Squared: the order is all that is wanted, and a square root per tile is not.
        var keyed = new List<(Fixed Distance, GridPos Tile)>(tiles.Count);
        for (int i = 0; i < tiles.Count; i++)
        {
            Point apart = Point.CentreOf(tiles[i]) - from;
            keyed.Add(((apart.X * apart.X) + (apart.Y * apart.Y), tiles[i]));
        }

        keyed.Sort(static (a, b) =>
            a.Distance != b.Distance ? a.Distance.CompareTo(b.Distance)
            : a.Tile.Y != b.Tile.Y ? a.Tile.Y.CompareTo(b.Tile.Y)
            : a.Tile.X.CompareTo(b.Tile.X));

        var sorted = new List<GridPos>(keyed.Count);
        for (int i = 0; i < keyed.Count; i++)
        {
            sorted.Add(keyed[i].Tile);
        }

        return sorted;
    }
}

/// <summary>
/// What <c>Household.ChooseSite</c> answers (D386): the front tile the house is filed under, the way
/// it faces, and the chooser's own sentence for why.
/// </summary>
public readonly record struct HomeSite(GridPos Front, Angle Facing, string WhyHere);

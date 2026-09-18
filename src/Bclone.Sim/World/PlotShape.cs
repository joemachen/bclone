using Bclone.Sim.Core;

namespace Bclone.Sim.World;

/// <summary>
/// A household's plot, derived from its house (D386, <c>specs/organic-housing.md §3</c>): the two
/// tiles the house stands on, the rectangle of ground behind and beside it, and the row across
/// its front that is the lane.
/// </summary>
/// <remarks>
/// <para>
/// ⭐ <b>Derived, never stored.</b> Three facts make a plot — the front tile the chooser pointed
/// at, which way the house faces, and the household's id — and everything below is arithmetic on
/// them. The zone map keeps the result as an index (<c>ZoneMap.ClaimPlot</c>), maintained where a
/// home changes; this is the one place the arithmetic lives, so the index, the chooser and the
/// view cannot disagree about where a fence runs.
/// </para>
/// <para>
/// <b>The house pair is the footprint's pair.</b> A 2×1 anchored west (facing north or south) covers
/// the pointed tile and the one west of it; anchored north (facing east or west) the pointed tile
/// and the one north of it (<c>SimWorld.HomeAnchorOn</c>, D382's anchor rule applied to the turned
/// extent). The front row is those two plus a third along the lane — on whichever side a hash of
/// the household says — and the yard is <c>depth − 1</c> rows behind. ⛔ Never an <c>Rng</c> draw.
/// </para>
/// </remarks>
public readonly record struct PlotShape
{
    /// <summary>The tile the chooser pointed at — the house's own tile, one of its two.</summary>
    public required GridPos Front { get; init; }

    public required Angle Facing { get; init; }

    /// <summary>The two tiles the house stands on.</summary>
    public required IReadOnlyList<GridPos> House { get; init; }

    /// <summary>Every tile of the plot, house tiles included, row by row from the front.</summary>
    public required IReadOnlyList<GridPos> Tiles { get; init; }

    /// <summary>The row across the front — the lane no plot may claim (§3.2).</summary>
    public required IReadOnlyList<GridPos> Lane { get; init; }

    /// <summary>The lane tile in front of the house's door — where the household steps out.</summary>
    public required GridPos Door { get; init; }

    /// <summary>
    /// The tiles just outside each of the plot's two sides — the ground a neighbour's plot would
    /// hold. Two lists, one per side, for the chooser's <i>apart</i> term (§3.3).
    /// </summary>
    public required IReadOnlyList<IReadOnlyList<GridPos>> Beside { get; init; }

    /// <summary>Which way the lane lies from the house: north for <see cref="Angle.Zero"/>, then east, south, west by quarter turns.</summary>
    public static GridPos LaneDirection(Angle facing) => (facing.Raw >> 14) switch
    {
        0 => new GridPos(0, -1),
        1 => new GridPos(1, 0),
        2 => new GridPos(0, 1),
        _ => new GridPos(-1, 0),
    };

    /// <summary>The four facings a house can have, in the order the chooser tries them.</summary>
    public static readonly IReadOnlyList<Angle> Facings = new[]
    {
        Angle.Zero, Angle.Right, Angle.Right + Angle.Right, Angle.Right + Angle.Right + Angle.Right,
    };

    /// <summary>Whether the house sits at the near end of its front row (the pointed tile's side) or the far end.</summary>
    /// <remarks>A hash of the id, so a row of houses is not a row of identical boxes and the same valley twice gets the same row.</remarks>
    public static bool HouseOnTheNearSide(int householdId)
    {
        uint mix = unchecked((uint)householdId * 2654435761u);
        mix ^= mix >> 15;
        mix = unchecked(mix * 2246822519u);
        mix ^= mix >> 13;
        return (mix & 1) == 0;
    }

    /// <summary>
    /// Where a household's facings are tried from when no lane decides it (D388): an index into
    /// <see cref="Facings"/> from a hash of the id, so the founders' houses and a plot with no lane
    /// nearby face four ways between them rather than all north. ⛔ Never an <c>Rng</c> draw.
    /// </summary>
    public static int FacingByHash(int householdId)
    {
        uint mix = unchecked((uint)householdId * 2246822519u);
        mix ^= mix >> 13;
        mix = unchecked(mix * 3266489917u);
        mix ^= mix >> 16;
        return (int)(mix % (uint)Facings.Count);
    }

    public static PlotShape Of(GridPos front, Angle facing, int householdId, int width, int depth)
    {
        if (width < 2 || depth < 1)
        {
            throw new System.ArgumentOutOfRangeException(nameof(width), "A plot is at least a house wide and a row deep.");
        }

        GridPos toLane = LaneDirection(facing);

        // Along the lane, toward the second house tile: west for a house anchored west, north
        // for one anchored north. The pair is (front + along, front).
        var along = toLane.Y != 0 ? new GridPos(-1, 0) : new GridPos(0, -1);
        var back = new GridPos(-toLane.X, -toLane.Y);

        // The front row runs from `start` for `width` tiles in the `along` direction, the house
        // pair at one end or the other of it.
        bool near = HouseOnTheNearSide(householdId);
        GridPos start = near
            ? front
            : new GridPos(front.X - (along.X * (width - 2)), front.Y - (along.Y * (width - 2)));

        var house = new GridPos[] { new(front.X + along.X, front.Y + along.Y), front };
        var tiles = new List<GridPos>(width * depth);
        var lane = new List<GridPos>(width);
        for (int row = 0; row < depth; row++)
        {
            for (int i = 0; i < width; i++)
            {
                tiles.Add(new GridPos(
                    start.X + (along.X * i) + (back.X * row),
                    start.Y + (along.Y * i) + (back.Y * row)));
            }
        }

        for (int i = 0; i < width; i++)
        {
            lane.Add(new GridPos(start.X + (along.X * i) + toLane.X, start.Y + (along.Y * i) + toLane.Y));
        }

        var sideA = new List<GridPos>(depth);
        var sideB = new List<GridPos>(depth);
        for (int row = 0; row < depth; row++)
        {
            sideA.Add(new GridPos(start.X - along.X + (back.X * row), start.Y - along.Y + (back.Y * row)));
            sideB.Add(new GridPos(
                start.X + (along.X * width) + (back.X * row),
                start.Y + (along.Y * width) + (back.Y * row)));
        }

        return new PlotShape
        {
            Front = front,
            Facing = facing,
            House = house,
            Tiles = tiles,
            Lane = lane,
            Door = new GridPos(front.X + toLane.X, front.Y + toLane.Y),
            Beside = new IReadOnlyList<GridPos>[] { sideA, sideB },
        };
    }
}

/// <summary>
/// What <c>Household.ChooseSite</c> answers (D386): the front tile the house is filed under, the way
/// it faces, and the chooser's own sentence for why.
/// </summary>
public readonly record struct HomeSite(GridPos Front, Angle Facing, string WhyHere);

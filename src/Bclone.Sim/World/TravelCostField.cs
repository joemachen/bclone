namespace Bclone.Sim.World;

/// <summary>
/// The single source of truth for "how far is that, really".
/// </summary>
/// <remarks>
/// <para>
/// <b>There must only ever be one of these.</b> <c>DESIGN.md §2.6</c> calls this out
/// explicitly: labour catchment (§2.2) and desire-path roads (§2.6) have to read the
/// same cost field or they will fight. A warehouse beside a well-worn trail
/// effectively has a larger catchment — that is a feature, and it only works if both
/// systems agree on what "cost" means.
/// </para>
/// <para>
/// Phase 1 ships the uniform version: every tile costs the same. The structure is
/// what matters — a per-tile multiplier that Phase 3 lowers where feet have worn a
/// path, without catchment needing to know anything changed.
/// </para>
/// <para>
/// Integer costs only, per decision D2. This feeds job assignment, which decides who
/// eats.
/// </para>
/// </remarks>
public sealed class TravelCostField
{
    /// <summary>Cost of crossing one tile of unmodified ground.</summary>
    public const int BaseTileCost = 10;

    private readonly int _ticksPerBaseTile;

    public TravelCostField(int ticksPerBaseTile = 1)
    {
        if (ticksPerBaseTile < 1)
        {
            throw new ArgumentOutOfRangeException(
                nameof(ticksPerBaseTile), $"Must be at least one tick per tile (got {ticksPerBaseTile}).");
        }

        _ticksPerBaseTile = ticksPerBaseTile;
    }

    /// <summary>
    /// Travel cost between two points, in cost units.
    /// </summary>
    /// <remarks>
    /// Manhattan distance rather than straight-line: a square root would put a float
    /// in the middle of arithmetic that decides who takes which job.
    /// </remarks>
    public int Cost(GridPos from, GridPos to) => from.ManhattanDistanceTo(to) * BaseTileCost;

    /// <summary>Ticks it takes to travel a given cost.</summary>
    public int TicksForCost(int cost)
    {
        if (cost < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(cost), $"Cost cannot be negative (got {cost}).");
        }

        // Integer division truncates, so a sub-tile remainder is free. Rounding up
        // instead would make every short hop cost a full tile.
        return cost * _ticksPerBaseTile / BaseTileCost;
    }

    /// <summary>Ticks to travel between two points. The form callers actually want.</summary>
    public int TicksBetween(GridPos from, GridPos to) => TicksForCost(Cost(from, to));

    /// <summary>
    /// Whether <paramref name="to"/> is inside a catchment of
    /// <paramref name="radiusInCost"/> centred on <paramref name="from"/>.
    /// </summary>
    /// <remarks>
    /// Catchment is measured in <em>cost</em>, not tiles — which is the whole point.
    /// Once roads exist, a workplace at the end of a good trail reaches further than
    /// one the same number of tiles away across rough ground, and neither system
    /// needs special-casing for that to be true.
    /// </remarks>
    public bool IsWithinCatchment(GridPos from, GridPos to, int radiusInCost) =>
        Cost(from, to) <= radiusInCost;

    /// <summary>Convert a radius expressed in tiles into cost units.</summary>
    public static int TilesToCost(int tiles) => tiles * BaseTileCost;
}

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
    private readonly GeneratedMap? _map;

    /// <summary>
    /// One flow field per destination, built on first ask and kept forever.
    /// </summary>
    /// <remarks>
    /// <b>Cached without any invalidation protocol, because terrain never changes.</b>
    /// <see cref="GeneratedMap"/> is immutable once generated, so a field computed for
    /// a destination is correct for the rest of the run — there is no stale-cache case
    /// to get wrong, which removes the most likely home for the "code reading state
    /// from where it used to live" bug this project keeps meeting. If terrain ever
    /// becomes mutable — a felled stand, a paved road, a bridge (D40) — this comment
    /// stops being true and the cache needs a way to be dropped.
    /// </remarks>
    private readonly Dictionary<GridPos, TerrainCostField> _fields = new();

    public TravelCostField(int ticksPerBaseTile = 1, GeneratedMap? map = null)
    {
        if (ticksPerBaseTile < 1)
        {
            throw new ArgumentOutOfRangeException(
                nameof(ticksPerBaseTile), $"Must be at least one tick per tile (got {ticksPerBaseTile}).");
        }

        _ticksPerBaseTile = ticksPerBaseTile;
        _map = map;
    }

    /// <summary>
    /// Travel cost between two points, in cost units — round the water, not through it.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Manhattan distance rather than straight-line: a square root would put a float in
    /// the middle of arithmetic that decides who takes which job. With a map, the walk
    /// is a real path over the terrain and water is impassable (D40); without one it
    /// falls back to plain distance, which is what the pure unit tests measure and what
    /// a valley with no river amounts to anyway.
    /// </para>
    /// <para>
    /// <b>Unreachable comes back as <see cref="Unreachable"/>, not as a big number.</b>
    /// A sentinel that takes part in arithmetic silently wins nearest-thing searches
    /// and sends villagers on errands they can never finish.
    /// </para>
    /// </remarks>
    public int Cost(GridPos from, GridPos to) =>
        _map is null
            ? from.ManhattanDistanceTo(to) * BaseTileCost
            : FieldTo(to).CostFrom(from);

    /// <summary>No walk gets there — across the river, or off the map.</summary>
    public const int Unreachable = TerrainCostField.Unreachable;

    /// <summary>Whether any route exists at all.</summary>
    public bool CanReach(GridPos from, GridPos to) => Cost(from, to) != Unreachable;

    /// <summary>
    /// One step from <paramref name="from"/> toward <paramref name="to"/>.
    /// </summary>
    /// <remarks>
    /// The movement half of the same field, and it must come from the same place as
    /// the cost or the two will disagree — a villager walking a straight line while
    /// the economy budgets for a path round the water is the worst of both.
    /// </remarks>
    public GridPos StepToward(GridPos from, GridPos to) =>
        _map is null ? from.StepToward(to) : FieldTo(to).StepFrom(from);

    private TerrainCostField FieldTo(GridPos destination)
    {
        if (!_fields.TryGetValue(destination, out TerrainCostField? field))
        {
            field = TerrainCostField.Build(_map!, destination, BaseTileCost);
            _fields[destination] = field;
        }

        return field;
    }

    /// <summary>Ticks it takes to travel a given cost.</summary>
    public int TicksForCost(int cost)
    {
        if (cost < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(cost), $"Cost cannot be negative (got {cost}).");
        }

        // Kept whole rather than scaled into a number of ticks. Multiplying
        // int.MaxValue by anything is the overflow this sentinel exists to avoid.
        if (cost == Unreachable)
        {
            return Unreachable;
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

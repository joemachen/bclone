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
    /// <para>
    /// <b>Terrain can change now, so this has an invalidation path</b> — see
    /// <see cref="Forget"/>. It said *"cached without any invalidation protocol, because
    /// terrain never changes"* for two phases and named the day it would stop being true
    /// (D41); a felled stand is that day.
    /// </para>
    /// <para>
    /// <b>Dropped on a change of passability, not on a change of terrain</b>, and the
    /// distinction is the whole performance argument: each field is a full Dijkstra over the
    /// valley, one per destination, and felling happens several times a year per logger.
    /// Grass and forest cost the same to cross, so felling moves no route
    /// (`specs/mutable-terrain.md §4.2`) — but that is now a <em>stated rule</em> that a
    /// test holds down, rather than a coincidence the cache was relying on.
    /// </para>
    /// </remarks>
    private readonly Dictionary<GridPos, TerrainCostField> _fields = new();

    // ---------------------------------------------------------------
    //  Worn ground (§2.6, D358)
    // ---------------------------------------------------------------

    private PathWear? _wear;
    private int _wornAt;
    private int _packedAt;
    private int _wornCost;
    private int _packedCost;
    private int _builtAtWearGeneration = -1;

    /// <summary>Whether any tile was worn enough to cost less than grass when the fields were last rebuilt — if not, the sweep is exact. Derived once per generation, never per field.</summary>
    private bool _anythingWorn;

    /// <summary>The price of stepping onto each tile, by map-order index, as of the last hand-over.</summary>
    private byte[]? _entryCost;

    /// <summary>Reused rebuild buffers — one per world, never shared (determinism).</summary>
    private TerrainCostField.Scratch? _scratch;

    /// <summary>
    /// ⭐ Let the field read the desire paths: a trodden tile is cheaper to cross, and every cached
    /// route is forgotten when the season hands new wear over.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>This is §2.6's "critical integration" in one method</b>: movement and labour catchment
    /// read the SAME field, so a warehouse beside a worn lane has a bigger reach — a feature,
    /// because both systems agree it does. Nothing else in the sim learns about paths.
    /// </para>
    /// <para>
    /// ⛔ Costs are read from the wear <em>as of the last season turn</em>: <see cref="FieldTo"/>
    /// drops every cached field when <see cref="PathWear.Generation"/> has moved, and
    /// <c>Generation</c> moves only in <see cref="PathWear.Decay"/>. A footstep never rebuilds a
    /// field. Uniform ground (nothing worn yet) still takes D179's breadth-first sweep.
    /// </para>
    /// </remarks>
    public void ReadWearFrom(PathWear wear, int wornAt, int packedAt, int wornCost, int packedCost)
    {
        ArgumentNullException.ThrowIfNull(wear);
        if (wornCost < 1 || packedCost < 1 || packedCost > wornCost || wornCost > BaseTileCost)
        {
            throw new ArgumentOutOfRangeException(
                nameof(wornCost),
                $"Path costs must run packed ≤ worn ≤ {BaseTileCost} and stay positive (got worn {wornCost}, packed {packedCost}).");
        }

        if (wornAt < 1 || packedAt < wornAt)
        {
            throw new ArgumentOutOfRangeException(
                nameof(wornAt), $"Path thresholds must run 1 ≤ worn ≤ packed (got {wornAt}, {packedAt}).");
        }

        _wear = wear;
        _wornAt = wornAt;
        _packedAt = packedAt;
        _wornCost = wornCost;
        _packedCost = packedCost;
        wear.PriceAt(wornAt, packedAt);
        Forget();
    }

    /// <summary>What it costs to step onto this tile — grass, a worn path, or a packed one.</summary>
    public int CostToEnter(GridPos tile)
    {
        if (_wear is null)
        {
            return BaseTileCost;
        }

        int wear = _wear.At(tile);
        return wear >= _packedAt ? _packedCost : wear >= _wornAt ? _wornCost : BaseTileCost;
    }



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

    /// <summary>
    /// ⭐ The whole route from <paramref name="from"/> to <paramref name="to"/> — the tiles
    /// <see cref="StepToward"/> would visit, in order, ending on <paramref name="to"/>; empty if
    /// there is no way, or nowhere to go (gridless slice 4, D356).
    /// </summary>
    /// <remarks>
    /// <b>Nothing new is computed.</b> It is <see cref="StepToward"/> repeated over the cached flow
    /// field, so a pulled string is pulled over exactly the staircase a villager would have walked,
    /// and the two cost systems `CLAUDE.md` forbids never come into being. O(route length), each
    /// step one array read once the field is cached.
    /// </remarks>
    public List<GridPos> RouteFrom(GridPos from, GridPos to)
    {
        var route = new List<GridPos>();
        GridPos here = from;

        // Bounded by the field's own answer: a step that does not move is "no way through", and
        // a route longer than the valley is a field that has gone wrong, not a walk.
        int budget = (_map?.Width ?? 512) * (_map?.Height ?? 512);
        while (here != to && budget-- > 0)
        {
            GridPos next = StepToward(here, to);
            if (next == here)
            {
                route.Clear();
                return route;
            }

            route.Add(next);
            here = next;
        }

        return route;
    }

    /// <summary>
    /// Throw away every cached route, because the ground they were computed over has
    /// changed shape.
    /// </summary>
    /// <remarks>
    /// <b>All of them, not the ones near the change.</b> A flow field spans the whole valley,
    /// so a single tile becoming impassable can lengthen a route that starts nowhere near it —
    /// working out which fields are affected is the same Dijkstra as rebuilding them, and
    /// getting it subtly wrong would leave exactly the stale route this exists to prevent. It
    /// is cheap because it is rare: only a change of <em>passability</em> gets here.
    /// </remarks>
    public void Forget()
    {
        _fields.Clear();
        _scratch = null;
    }

    /// <summary>How many routes are currently cached. For tests and diagnostics.</summary>
    internal int CachedFields => _fields.Count;

    private TerrainCostField FieldTo(GridPos destination)
    {
        // ⭐ Wear reaches the routes at a season turn, here, and nowhere else (D358) — and only when
        // some tile's PRICE changed (`RoutesGeneration`): a rebuild every season took the suite past
        // ten minutes. The fields are KEPT and refilled in place on the next ask, with a price table
        // computed once for the generation — the first draft threw them away and re-allocated, and a
        // fifty-year run went 1.6 s → 3.4 s.
        if (_wear is not null && _wear.RoutesGeneration != _builtAtWearGeneration)
        {
            _builtAtWearGeneration = _wear.RoutesGeneration;
            _anythingWorn = _wear.TilesAtLeast(_wornAt) > 0;
            if (_anythingWorn)
            {
                _entryCost ??= new byte[_map!.Width * _map.Height];
                for (int i = 0; i < _entryCost.Length; i++)
                {
                    _entryCost[i] = (byte)CostToEnter(_wear.PositionOf(i));
                }
            }
        }

        if (!_fields.TryGetValue(destination, out TerrainCostField? field))
        {
            // A fresh field is the sweep, which is already right when nothing is worn — only a
            // priced valley needs it refilled straight away.
            field = TerrainCostField.Build(_map!, destination, BaseTileCost);
            field.BuiltAtGeneration = _anythingWorn ? -1 : _builtAtWearGeneration;
            _fields[destination] = field;
        }

        if (field.BuiltAtGeneration != _builtAtWearGeneration)
        {
            _scratch ??= new TerrainCostField.Scratch(_map!, BaseTileCost);
            field.Refill(_map!, BaseTileCost, _anythingWorn ? _entryCost : null, _scratch);
            field.BuiltAtGeneration = _builtAtWearGeneration;
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

    // ⛔ `IsWithinCatchment` IS DELETED with the catchment it was named for
    // (`forests-and-gathering.md §3`). Nothing in the sim asked it any more: the allocator's
    // only question is `CanReach`, and a workplace's reach is not a thing the village
    // enforces. Deleted rather than renamed to something like `IsWithin`, on D98's rule —
    // a general-purpose radius test with no caller is an invitation to reintroduce the fence
    // by accident.
    //
    // The property it was protecting survives: distance is measured in *cost*, so a road can
    // extend a workplace's reach later without either system knowing about the other. That is
    // still true of `Cost` itself, and still tested.
    //
    // `TilesToCost` went the same way in D159, and for the same reason it should have gone
    // with the fence: converting a radius in tiles into cost units is a question only the
    // catchment ever asked, and it had sat here with no caller since D120.
}

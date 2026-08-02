namespace Bclone.Sim.World;

/// <summary>
/// The cost of walking to one destination, from every tile in the valley.
/// </summary>
/// <remarks>
/// <para>
/// A Dijkstra flow field, computed once per building over the static terrain. It
/// answers two questions at once, which is why it is worth the memory: <b>how far is
/// it from here</b> (an array lookup) and <b>which way is the next step</b> (the
/// cheapest neighbour). Between them those are every travel question the sim asks.
/// </para>
/// <para>
/// <b>One field per building rather than a search per call, because of a property of
/// this game: every travel query has a building at one end.</b> Labour catchment asks
/// home-to-workplace, a fetch asks position-to-store, a marketer asks position-to-home.
/// Nothing ever asks the distance between two arbitrary tiles. So the destinations are
/// few and fixed, and the expensive half of the problem can be done once instead of
/// tens of thousands of times a year in the labour pass.
/// </para>
/// <para>
/// <b>Walking the field cannot get a villager stuck</b>, which straight-line stepping
/// into a river bank absolutely could. Any tile with a finite cost has a neighbour with
/// a lower one — that is what Dijkstra guarantees — so "step to the cheapest neighbour"
/// always makes progress. The only case needing care is a destination that is not
/// reachable at all, and that is reported rather than walked into.
/// </para>
/// <para>
/// Integer costs throughout, per D2. This feeds job assignment, which decides who eats.
/// </para>
/// </remarks>
public sealed class TerrainCostField
{
    /// <summary>Returned for a tile no walk can reach — across the river, or off the map.</summary>
    /// <remarks>
    /// <b>A distinct answer, not a very large number.</b> A sentinel that takes part in
    /// arithmetic is a bug waiting to be written: "unreachable plus one" is a number,
    /// compares as a number, and silently wins a nearest-thing search. Callers ask
    /// <see cref="IsReachable"/> rather than comparing against it.
    /// </remarks>
    public const int Unreachable = int.MaxValue;

    private readonly int[] _cost;
    private readonly int _width;
    private readonly int _height;
    private readonly int _minX;
    private readonly int _minY;

    private TerrainCostField(
        GridPos destination, int[] cost, int width, int height, int minX, int minY)
    {
        Destination = destination;
        _cost = cost;
        _width = width;
        _height = height;
        _minX = minX;
        _minY = minY;
    }

    /// <summary>Where every path in this field is going.</summary>
    public GridPos Destination { get; }

    /// <summary>
    /// Build the field for one destination.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Plain Dijkstra over four-connected tiles, with a simple scan for the next
    /// frontier tile rather than a priority queue. <b>The scan is deliberate.</b> A
    /// binary heap would be asymptotically better and would break the determinism
    /// contract in a way that is very hard to see: equal-cost tiles come out in an
    /// order that depends on the heap's internal shuffling, and two runs that pop them
    /// differently produce different — equally short — paths, so villagers walk
    /// different routes on the same seed. A scan in tile order breaks every tie the
    /// same way, forever (D5's rule about ordering being part of the value).
    /// </para>
    /// <para>
    /// At 9,600 tiles this is fast enough to be irrelevant, and it runs when a building
    /// is founded rather than in the tick loop.
    /// </para>
    /// </remarks>
    public static TerrainCostField Build(GeneratedMap map, GridPos destination, int baseTileCost)
    {
        ArgumentNullException.ThrowIfNull(map);

        if (baseTileCost < 1)
        {
            throw new ArgumentOutOfRangeException(
                nameof(baseTileCost), $"A tile must cost at least one (got {baseTileCost}).");
        }

        int width = map.Width;
        int height = map.Height;
        var cost = new int[width * height];
        var settled = new bool[width * height];

        for (int i = 0; i < cost.Length; i++)
        {
            cost[i] = Unreachable;
        }

        var field = new TerrainCostField(destination, cost, width, height, map.MinX, map.MinY);

        // A destination in the water is a destination nobody can stand on. Left
        // entirely unreachable rather than quietly nudged to the bank: a building in
        // the river is a bug in whatever placed it, and hiding it here would make that
        // bug appear somewhere else entirely.
        int start = field.IndexOf(destination);
        if (start < 0 || !TerrainRules.IsPassable(map.TerrainAt(destination)))
        {
            return field;
        }

        cost[start] = 0;

        while (true)
        {
            // Cheapest unsettled tile, ties broken by tile order — see the remarks.
            int current = -1;
            int currentCost = Unreachable;
            for (int i = 0; i < cost.Length; i++)
            {
                if (!settled[i] && cost[i] < currentCost)
                {
                    current = i;
                    currentCost = cost[i];
                }
            }

            if (current < 0)
            {
                break;
            }

            settled[current] = true;

            int x = current % width;
            int y = current / width;

            Relax(x + 1, y);
            Relax(x - 1, y);
            Relax(x, y + 1);
            Relax(x, y - 1);

            void Relax(int nx, int ny)
            {
                if (nx < 0 || nx >= width || ny < 0 || ny >= height)
                {
                    return;
                }

                var neighbour = new GridPos(nx + map.MinX, ny + map.MinY);
                if (!TerrainRules.IsPassable(map.TerrainAt(neighbour)))
                {
                    // The whole point (D40). Water is not expensive, it is impossible,
                    // until the village learns to bridge it.
                    return;
                }

                int index = (ny * width) + nx;
                int candidate = currentCost + baseTileCost;
                if (candidate < cost[index])
                {
                    cost[index] = candidate;
                }
            }
        }

        return field;
    }

    /// <summary>Cost of walking from here to the destination, or <see cref="Unreachable"/>.</summary>
    public int CostFrom(GridPos from)
    {
        int index = IndexOf(from);
        return index < 0 ? Unreachable : _cost[index];
    }

    /// <summary>Whether any walk gets there from here.</summary>
    public bool IsReachable(GridPos from) => CostFrom(from) != Unreachable;

    /// <summary>
    /// One step from <paramref name="from"/> toward the destination.
    /// </summary>
    /// <remarks>
    /// Downhill on the field, so it goes round water without anybody storing a route.
    /// Neighbours are tried in a fixed order — east, west, south, north — so that two
    /// equally good ways round an obstacle always resolve the same way; without that,
    /// a villager could take either side of the river on different runs of the same
    /// seed and the state hash would diverge on a journey nobody chose differently.
    /// </remarks>
    public GridPos StepFrom(GridPos from)
    {
        if (from == Destination)
        {
            return from;
        }

        int here = CostFrom(from);
        if (here == Unreachable || here == 0)
        {
            return from;
        }

        GridPos best = from;
        int bestCost = here;

        Consider(new GridPos(from.X + 1, from.Y));
        Consider(new GridPos(from.X - 1, from.Y));
        Consider(new GridPos(from.X, from.Y + 1));
        Consider(new GridPos(from.X, from.Y - 1));

        return best;

        void Consider(GridPos neighbour)
        {
            int candidate = CostFrom(neighbour);
            if (candidate < bestCost)
            {
                bestCost = candidate;
                best = neighbour;
            }
        }
    }

    private int IndexOf(GridPos position)
    {
        int x = position.X - _minX;
        int y = position.Y - _minY;

        return x < 0 || x >= _width || y < 0 || y >= _height ? -1 : (y * _width) + x;
    }
}

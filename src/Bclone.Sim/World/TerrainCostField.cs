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

        // ⭐⭐ A BREADTH-FIRST SWEEP, BECAUSE EVERY EDGE COSTS THE SAME (D179).
        //
        // This was a textbook Dijkstra: for each of the valley's tiles, scan ALL of them for
        // the cheapest unsettled one. That is O(n²) — **about 92 million iterations for a
        // 120×80 valley, per field** — and it was measured at **99 ms a field, 41 fields per
        // founding, four seconds to build a world**, which is very nearly the entire test
        // suite (`DESIGN.md` D179).
        //
        // ⭐ THE PRIORITY QUEUE IS NOT NEEDED, WHICH IS THE WHOLE TRICK. Dijkstra earns its
        // keep when edges have *different* weights; here every step costs `baseTileCost` and
        // movement is four-way, so **the cheapest unsettled tile is always simply the next one
        // out of a FIFO queue.** With uniform weights, Dijkstra *is* breadth-first search.
        //
        // ⚠️ AND THE RESULT IS PROVABLY IDENTICAL, WHICH IS WHY THIS IS SAFE TO DO TO A P0.
        // Shortest-path distance is a property of the graph, not of the order it is explored
        // in — so `cost[]` comes out byte-for-byte the same, and `StepFrom` reads nothing but
        // `cost[]` in a fixed neighbour order. **The old comment about ties being broken by
        // tile order was describing something that could not affect the answer**: two tiles at
        // equal cost settle in either order and both get the same number either way.
        //
        // A tile enters the queue exactly once — the first relaxation is always the cheapest,
        // because BFS reaches tiles in non-decreasing cost — so the queue never needs to hold
        // more than one entry per tile and no tile is ever re-examined.
        //
        // ⛔⛔ AND HERE IS THE TRAP, FOR WHOEVER BUILDS DESIRE-PATH ROADS (§2.6).
        //
        // **This is correct ONLY while every passable tile costs the same to cross.** §2.6 is
        // a planned pillar and it says, in as many words, that crossing thresholds *"shifts
        // the tile visually and **lowers pathfinding cost**, creating a reinforcement loop"*.
        // **The day a worn path is cheaper than grass, breadth-first search silently returns
        // wrong answers** — it will keep the first route it finds rather than the cheapest,
        // and nothing here will throw.
        //
        // ✅ **THAT DAY CAME (D358), AND THE PARAGRAPH WAS READ.** The overload below takes a per-tile
        // entry cost and settles by cost — Dial's bucket queue rather than the priority queue this
        // paragraph suggested, because every edge is a small integer and the buckets are O(n + cost)
        // where a heap is O(E log V); a heap was measured first and cost the suite a minute. **The
        // sweep is still used whenever nothing is worn**, and `DesirePathTests` proves the two agree
        // byte for byte on uniform ground. **Do not go back to the scan.**
        //
        // ⚠️ It will not announce itself. Every guard in the suite would still pass on the day
        // roads land, because they all describe a valley where the rule still holds — and the
        // symptom would be villagers taking scenic routes for a phase before anybody noticed.
        // *That is why this paragraph is here rather than in a decision nobody greps for.*
        var queue = new int[cost.Length];
        int head = 0;
        int tail = 0;
        queue[tail++] = start;

        while (head < tail)
        {
            int current = queue[head++];
            int currentCost = cost[current];

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

                int index = (ny * width) + nx;
                if (cost[index] != Unreachable)
                {
                    // Already reached, and by a route that cannot be beaten: BFS arrives in
                    // non-decreasing cost, so the first arrival is the cheapest one.
                    return;
                }

                var neighbour = new GridPos(nx + map.MinX, ny + map.MinY);
                if (!TerrainRules.IsPassable(map.TerrainAt(neighbour)))
                {
                    // The whole point (D40). Water is not expensive, it is impossible,
                    // until the village learns to bridge it.
                    return;
                }

                cost[index] = currentCost + baseTileCost;
                queue[tail++] = index;
            }
        }

        return field;
    }

    /// <summary>
    /// ⭐⭐ The same field over ground that is NOT uniform — <b>worn paths are cheaper</b>
    /// (§2.6, D358).
    /// </summary>
    /// <remarks>
    /// <para>
    /// D179's breadth-first sweep is correct only because every edge costs the same; the moment a
    /// trodden tile costs 9 and grass 10, the FIFO no longer settles the cheapest tile first and
    /// the field would be wrong on exactly the tiles this feature is about. So this is Dijkstra as
    /// a BUCKET queue (Dial's algorithm) — <b>O(n + total cost)</b>, a whisker slower than the
    /// sweep — and it is used <em>only when something is worn</em>: the caller passes a
    /// <c>null</c> price table for a valley nobody has walked and gets the sweep.
    /// </para>
    /// <para>
    /// ⚠️ <b>Deterministic in its answer, not in its order.</b> Shortest-path cost is a property of
    /// the graph; ties settle in either order and <c>cost[]</c> comes out the same (D179's own
    /// argument). Buckets are walked in insertion order, so two machines do the same work in the
    /// same order — belt and braces, and it costs nothing.
    /// </para>
    /// <para>
    /// <paramref name="entryCost"/> is the cost of stepping ONTO each tile by map-order index —
    /// a table, not a delegate, because it is asked four times a tile and the first draft's delegate
    /// (position → wear → threshold) was a measurable share of a rebuild. Never less than one,
    /// never more than <paramref name="baseTileCost"/>; passability is still the terrain's.
    /// </para>
    /// </remarks>
    public static TerrainCostField Build(
        GeneratedMap map, GridPos destination, int baseTileCost, byte[]? entryCost)
    {
        if (entryCost is null)
        {
            return Build(map, destination, baseTileCost);
        }

        TerrainCostField field = Build(map, destination, baseTileCost);
        field.Refill(map, baseTileCost, entryCost, new Scratch(map, baseTileCost));
        return field;
    }

    /// <summary>The buffers a rebuild reuses, so a season's rebuilds allocate nothing but the answer.</summary>
    /// <remarks>
    /// ⛔ <b>One per <c>TravelCostField</c>, never shared across worlds</b> — a buffer shared
    /// between two simulations is a determinism hazard far worse than the allocation it saves
    /// (`CLAUDE.md`), and the test suite runs worlds in parallel.
    /// </remarks>
    internal sealed class Scratch
    {
        public readonly int[] Queue;
        public readonly List<int>[] Ring;

        /// <summary>Whether each tile can be walked, by map-order index — asked four times a tile in every refill, so it is a table, not a call.</summary>
        public readonly bool[] Passable;

        public Scratch(GeneratedMap map, int baseTileCost)
        {
            int tiles = map.Width * map.Height;
            Queue = new int[tiles];
            Ring = new List<int>[baseTileCost + 1];
            for (int i = 0; i < Ring.Length; i++)
            {
                Ring[i] = new List<int>();
            }

            Passable = new bool[tiles];
            for (int i = 0; i < tiles; i++)
            {
                var at = new GridPos((i % map.Width) + map.MinX, (i / map.Width) + map.MinY);
                Passable[i] = TerrainRules.IsPassable(map.TerrainAt(at));
            }
        }
    }

    /// <summary>Which hand-over of the paths this field was last computed against — the cache's key.</summary>
    internal int BuiltAtGeneration { get; set; } = -1;

    /// <summary>
    /// Recompute this field IN PLACE for the current prices — the whole reason a season's turn
    /// does not allocate ninety fresh arrays.
    /// </summary>
    /// <remarks>
    /// ⛔⛔ <b>THE FIRST DRAFT REBUILT BY ALLOCATING, AND A FIFTY-YEAR RUN WENT 1.6 s → 3.4 s</b>
    /// (D358): every price change threw away ~90 fields and built ~90 more, 38 KB each, and under
    /// the suite's parallel load the collector turned that into a suite three times slower. The
    /// fields are kept and refilled: same arrays, same answer.
    /// </remarks>
    internal void Refill(GeneratedMap map, int baseTileCost, byte[]? entryCost, Scratch scratch)
    {
        int[] cost = _cost;
        for (int i = 0; i < cost.Length; i++)
        {
            cost[i] = Unreachable;
        }

        int start = IndexOf(Destination);
        bool[] passable = scratch.Passable;
        if (start < 0 || !TerrainRules.IsPassable(map.TerrainAt(Destination)))
        {
            return;
        }

        cost[start] = 0;
        int width = _width;
        int height = _height;

        if (entryCost is null)
        {
            // Uniform ground: D179's sweep, over the reused queue.
            int[] queue = scratch.Queue;
            int head = 0;
            int tail = 0;
            queue[tail++] = start;
            while (head < tail)
            {
                int current = queue[head++];
                int currentCost = cost[current];
                int x = current % width;
                int y = current / width;
                Sweep(x + 1, y, currentCost);
                Sweep(x - 1, y, currentCost);
                Sweep(x, y + 1, currentCost);
                Sweep(x, y - 1, currentCost);
            }

            return;

            void Sweep(int nx, int ny, int currentCost)
            {
                if (nx < 0 || nx >= width || ny < 0 || ny >= height)
                {
                    return;
                }

                int index = (ny * width) + nx;
                if (cost[index] != Unreachable)
                {
                    return;
                }

                if (!passable[index])
                {
                    return;
                }

                cost[index] = currentCost + baseTileCost;
                queue[tail++] = index;
            }
        }

        // ⭐⭐ DIAL'S ALGORITHM, NOT A HEAP — because every edge costs a small integer no larger
        // than `baseTileCost`, the frontier fits in `baseTileCost + 1` buckets indexed by cost,
        // and settling in cost order is a walk round that ring: O(n + total cost), which is
        // within a whisker of D179's sweep and provably the same answer as Dijkstra. A heap
        // version was measured first and cost the suite a minute.
        // ⚠️ A tile pulled from a bucket whose recorded cost has since improved is stale and skipped
        // — the improved entry sits in an earlier bucket and was, or will be, settled first.
        // Red-checked (D358): removing the skip changes NO answer — a stale settle cannot improve a
        // neighbour — so this is a work-saver, kept, and the zero is written down.
        List<int>[] ring = scratch.Ring;
        int ringSize = ring.Length;
        for (int i = 0; i < ringSize; i++)
        {
            ring[i].Clear();
        }

        ring[0].Add(start);
        int pending = 1;
        int settling = 0;

        while (pending > 0)
        {
            List<int> bucket = ring[settling % ringSize];
            for (int b = 0; b < bucket.Count; b++)
            {
                int current = bucket[b];
                pending--;
                if (cost[current] != settling)
                {
                    continue;
                }

                int x = current % width;
                int y = current / width;
                Relax(x + 1, y);
                Relax(x - 1, y);
                Relax(x, y + 1);
                Relax(x, y - 1);
            }

            bucket.Clear();
            settling++;

            void Relax(int nx, int ny)
            {
                if (nx < 0 || nx >= width || ny < 0 || ny >= height)
                {
                    return;
                }

                int index = (ny * width) + nx;
                if (!passable[index])
                {
                    return;
                }

                int step = entryCost[index];
                if (step < 1 || step > baseTileCost)
                {
                    throw new InvalidOperationException(
                        $"A tile's entry cost must be between 1 and {baseTileCost} for the bucket queue (got {step}).");
                }

                int candidate = settling + step;
                if (candidate < cost[index])
                {
                    cost[index] = candidate;
                    ring[candidate % ringSize].Add(index);
                    pending++;
                }
            }
        }
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

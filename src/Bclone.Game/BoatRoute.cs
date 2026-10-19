using Godot;

namespace Bclone.Game;

/// <summary>
/// ⭐ The water a trading boat sails (`specs/trade.md §4.4`, commit 7 — View II): from the edge the river enters by, past the
/// trading post, to the edge it leaves by — <b>found once per post</b>, because the river never changes (CLAUDE.md: nothing
/// derivable is rebuilt per frame).
/// </summary>
/// <remarks>
/// <para>
/// <b>Pure</b>, as <see cref="TrailNetwork"/> is: a grid of water in, one line out — so the probe hands it a river by hand.
/// A boat is sim state with three ticks (<c>TradeBoat</c>: it appears, docks, leaves); <b>where it is on the water is
/// derived</b> from those ticks and this line (<see cref="Where"/>), never stepped and never saved (`trade.md §6`).
/// </para>
/// <para>
/// The line keeps to the middle of the river: a step costs more the nearer it runs to a bank, and a diagonal step is taken
/// only where both of its sides are water — a boat never cuts a corner of land. Every tie goes to the lower tile index.
/// </para>
/// </remarks>
internal sealed class BoatRoute
{
    /// <summary>Smoothing passes over each half (Chaikin), as the lanes get.</summary>
    private const int SmoothingPasses = 2;

    /// <summary>What hugging a bank costs a step, on top of the step: divided by one more than the tiles to the land.</summary>
    private const int BankCost = 60;

    /// <summary>How far from the post its mooring may be looked for, in tiles.</summary>
    private const int MooringReach = 5;

    private BoatRoute(Vector2[] points, float[] along, float dock)
    {
        Points = points;
        Along = along;
        DockAt = dock;
    }

    /// <summary>The line in tile coordinates, from the upriver edge to the downriver edge.</summary>
    public Vector2[] Points { get; }

    /// <summary>How far along the line each point lies, in tiles.</summary>
    public float[] Along { get; }

    /// <summary>How far along the line the mooring lies.</summary>
    public float DockAt { get; }

    /// <summary>The line's whole length.</summary>
    public float Length => Along[^1];

    /// <summary>
    /// The boat's line, or null if the river cannot be sailed from either edge to the post — no water near it, or an
    /// edge the course names that no water reaches.
    /// </summary>
    /// <param name="originX">The tile x of index 0.</param>
    /// <param name="originY">The tile y of index 0.</param>
    /// <param name="width">The window's width in tiles.</param>
    /// <param name="height">The window's height in tiles.</param>
    /// <param name="water">Row-major: whether each tile is water a boat floats on.</param>
    /// <param name="post">The post's centre, in tile coordinates.</param>
    /// <param name="course">The river's course — <c>we</c>, <c>ns</c>, <c>nwse</c> or <c>swne</c> (<c>MapGenerator.CourseOf</c>).</param>
    public static BoatRoute? Find(int originX, int originY, int width, int height, bool[] water, Vector2 post, string course)
    {
        int n = width * height;
        if (water.Length != n)
        {
            throw new ArgumentException($"A {width}×{height} river wants {n} tiles, not {water.Length}.");
        }

        // Upriver is where the course starts, downriver where it ends; a diagonal course enters by either of two edges.
        (bool West, bool East, bool North, bool South) up = course switch
        {
            "ns" => (false, false, true, false),
            "nwse" => (true, false, true, false),
            "swne" => (true, false, false, true),
            _ => (true, false, false, false),
        };
        (bool West, bool East, bool North, bool South) down = course switch
        {
            "ns" => (false, false, false, true),
            "nwse" => (false, true, false, true),
            "swne" => (false, true, true, false),
            _ => (false, true, false, false),
        };

        int mooring = Mooring();
        if (mooring < 0)
        {
            return null;
        }

        int[] toLand = TilesToTheLand();
        List<int>? inbound = Cheapest(OnTheEdge(up), mooring);
        List<int>? outbound = Cheapest(new List<int> { mooring }, -1, OnTheEdge(down));
        if (inbound is null || outbound is null)
        {
            return null;
        }

        Vector2[] first = Smoothed(inbound);
        Vector2[] second = Smoothed(outbound);
        var points = new Vector2[first.Length + second.Length - 1];
        first.CopyTo(points, 0);
        Array.Copy(second, 1, points, first.Length, second.Length - 1);
        var along = new float[points.Length];
        for (int k = 1; k < points.Length; k++)
        {
            along[k] = along[k - 1] + points[k - 1].DistanceTo(points[k]);
        }

        return new BoatRoute(points, along, along[first.Length - 1]);

        // The water tile nearest the post's centre, within reach — where the boat ties up.
        int Mooring()
        {
            int best = -1;
            float bestDistance = MooringReach * MooringReach + 0.01f;
            for (int i = 0; i < n; i++)
            {
                if (!water[i])
                {
                    continue;
                }

                float d = new Vector2(originX + (i % width), originY + (i / width)).DistanceSquaredTo(post);
                if (d < bestDistance - 1e-4f)
                {
                    bestDistance = d;
                    best = i;
                }
            }

            return best;
        }

        // Every water tile's distance to the nearest land, in steps (four ways).
        int[] TilesToTheLand()
        {
            var distance = new int[n];
            Array.Fill(distance, int.MaxValue);
            var queue = new Queue<int>();
            for (int i = 0; i < n; i++)
            {
                if (!water[i])
                {
                    distance[i] = 0;
                    queue.Enqueue(i);
                }
            }

            while (queue.Count > 0)
            {
                int here = queue.Dequeue();
                foreach (int there in Fours(here))
                {
                    if (distance[there] == int.MaxValue)
                    {
                        distance[there] = distance[here] + 1;
                        queue.Enqueue(there);
                    }
                }
            }

            return distance;
        }

        List<int> OnTheEdge((bool West, bool East, bool North, bool South) side)
        {
            var edge = new List<int>();
            for (int i = 0; i < n; i++)
            {
                int x = i % width, y = i / width;
                if (water[i] && ((side.West && x == 0) || (side.East && x == width - 1) || (side.North && y == 0) || (side.South && y == height - 1)))
                {
                    edge.Add(i);
                }
            }

            return edge;
        }

        // Dijkstra over the water from every start at once to the goal — or, with no goal, to the first of the ends
        // reached; the tiles in order, start first.
        List<int>? Cheapest(List<int> starts, int goal, List<int>? ends = null)
        {
            if (starts.Count == 0 || (goal < 0 && (ends is null || ends.Count == 0)))
            {
                return null;
            }

            var finish = ends is null ? null : new HashSet<int>(ends);
            var cost = new Dictionary<int, long>();
            var back = new Dictionary<int, int>();
            var open = new PriorityQueue<int, long>();
            foreach (int start in starts)
            {
                cost[start] = 0;
                back[start] = -1;
                open.Enqueue(start, start);
            }

            int reached = -1;
            while (open.TryDequeue(out int here, out long priority))
            {
                if (priority / n != cost[here])
                {
                    continue;
                }

                if (here == goal || (finish is not null && finish.Contains(here)))
                {
                    reached = here;
                    break;
                }

                int hx = here % width, hy = here / width;
                for (int dy = -1; dy <= 1; dy++)
                {
                    for (int dx = -1; dx <= 1; dx++)
                    {
                        int nx = hx + dx, ny = hy + dy;
                        if ((dx == 0 && dy == 0) || nx < 0 || ny < 0 || nx >= width || ny >= height)
                        {
                            continue;
                        }

                        int there = (ny * width) + nx;
                        if (!water[there] || (dx != 0 && dy != 0 && (!water[(hy * width) + nx] || !water[(ny * width) + hx])))
                        {
                            continue;
                        }

                        long step = cost[here] + (dx != 0 && dy != 0 ? 14 : 10) + (BankCost / (1 + toLand[there]));
                        if (!cost.TryGetValue(there, out long known) || step < known)
                        {
                            cost[there] = step;
                            back[there] = here;
                            open.Enqueue(there, (step * n) + there);
                        }
                    }
                }
            }

            if (reached < 0)
            {
                return null;
            }

            var tiles = new List<int> { reached };
            while (back[tiles[^1]] >= 0)
            {
                tiles.Add(back[tiles[^1]]);
            }

            tiles.Reverse();
            return tiles;
        }

        Vector2[] Smoothed(List<int> tiles)
        {
            var points = tiles.Select(t => new Vector2(originX + (t % width), originY + (t / width))).ToArray();
            for (int pass = 0; pass < SmoothingPasses && points.Length > 2; pass++)
            {
                var next = new List<Vector2>(points.Length * 2) { points[0] };
                for (int s = 0; s + 1 < points.Length; s++)
                {
                    next.Add(points[s].Lerp(points[s + 1], 0.25f));
                    next.Add(points[s].Lerp(points[s + 1], 0.75f));
                }

                next.Add(points[^1]);
                points = next.ToArray();
            }

            return points;
        }

        IEnumerable<int> Fours(int i)
        {
            int x = i % width, y = i / width;
            if (x > 0)
            {
                yield return i - 1;
            }

            if (x < width - 1)
            {
                yield return i + 1;
            }

            if (y > 0)
            {
                yield return i - width;
            }

            if (y < height - 1)
            {
                yield return i + width;
            }
        }
    }

    /// <summary>
    /// Where a boat is, at a moment between ticks — derived from its three ticks, never stepped (`trade.md §4.4`): sailing
    /// in from its edge until it docks, tied up at the mooring until it leaves, then on to the far edge, gone once it has
    /// sailed off. A boat from upriver sails down the line; one from downriver sails up it.
    /// </summary>
    /// <returns>Whether it is on the water, where, and the way it faces (a unit vector).</returns>
    public (bool Shown, Vector2 At, Vector2 Heading) Where(double now, ulong appears, ulong docks, ulong leaves, ulong sail, bool fromUpriver)
    {
        if (now < appears || now >= leaves + sail)
        {
            return (false, Vector2.Zero, Vector2.Right);
        }

        // How far along the line, measured from the edge the boat came in by.
        float travelled;
        if (now < docks)
        {
            float share = docks > appears ? (float)((now - appears) / (docks - appears)) : 1f;
            travelled = (fromUpriver ? DockAt : Length - DockAt) * share;
        }
        else if (now <= leaves)
        {
            travelled = fromUpriver ? DockAt : Length - DockAt;
        }
        else
        {
            float share = sail > 0 ? (float)((now - leaves) / sail) : 1f;
            float home = fromUpriver ? DockAt : Length - DockAt;
            travelled = home + ((Length - home) * share);
        }

        float along = fromUpriver ? travelled : Length - travelled;
        (Vector2 at, Vector2 heading) = PointAt(along);
        return (true, at, fromUpriver ? heading : -heading);
    }

    /// <summary>The point a distance along the line, and the line's direction there (downriver).</summary>
    private (Vector2 At, Vector2 Heading) PointAt(float along)
    {
        along = Mathf.Clamp(along, 0f, Length);
        int k = 1;
        while (k < Along.Length - 1 && Along[k] < along)
        {
            k++;
        }

        Vector2 a = Points[k - 1], b = Points[k];
        float span = Along[k] - Along[k - 1];
        float share = span > 1e-6f ? (along - Along[k - 1]) / span : 0f;
        Vector2 heading = (b - a).LengthSquared() > 1e-8f ? (b - a).Normalized() : Vector2.Right;
        return (a.Lerp(b, share), heading);
    }
}

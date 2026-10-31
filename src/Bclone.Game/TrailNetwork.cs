using Godot;

namespace Bclone.Game;

/// <summary>
/// ⭐ The village's worn ground read as a NETWORK of lanes — the centre lines of the shape the paths make — rather than
/// one ribbon per tile (D581, Joe's note 1, option E of <c>specs/path-shapes.md</c>).
/// </summary>
/// <remarks>
/// <para>
/// <b>Why:</b> drawn tile by tile, two lanes a tile apart were joined by every walk that crossed between them (a
/// ladder), a lane round a hut's outline was a square ring (Joe's <c>#</c> knots), and a busy hub was a filled block.
/// The shape the village wore is a few lanes; this finds them: <b>bridge</b> a one-tile gap between two worn tiles that
/// face each other across it, <b>thin</b> the shape to its centre line (Zhang–Suen, which keeps every connection and
/// every hole — a lane round a building stays a loop), <b>trace</b> the line into lanes between its ends and junctions,
/// <b>place</b> each point at the middle of the worn ground round it, add the valley's wander (D580) and <b>smooth</b>.
/// </para>
/// <para>
/// <b>Pure</b>: a grid in, lanes out — no node, no world, no clock — so the probe hands it shapes by hand. Built once a
/// paths generation (<c>VillageMap.CollectTheTrailsIfTheyMoved</c>), never per frame. Deterministic: every sweep is in
/// index order and every tie goes to the lower index.
/// </para>
/// <para>
/// ⛔ <b>What it never does:</b> draw on a blocked tile (a building's footprint, or water nobody can walk) — the caller
/// zeroes the grade there and a gap is never bridged across one; or bridge a gap two tiles wide, which is two lanes.
/// ⛔ <b>Nor cross a fence</b> (D605): a wall separates two tiles in the bridging, the thinning, the tracing, the
/// placing and every step drawn (<see cref="Linkage"/>).
/// </para>
/// <para>
/// ⭐ <b>And since D605</b> (Joe's star, his doorsteps — <c>specs/path-shapes.md §4.F</c>): a clump of junction pixels
/// wider than 3 × 3 is a <b>plaza</b>, its arrivals joined by a tree on its own worn skeleton rather than all meeting at
/// one point; and a building with worn ground beside it that no lane is drawn against gets its <b>doorstep</b> — a short
/// lane along the worn ground from the nearest lane to its wall.
/// </para>
/// </remarks>
internal sealed class TrailNetwork
{
    /// <summary>One lane: its points in tile coordinates, a grade per point, and how its ends finish.</summary>
    internal sealed record Lane(Vector2[] Points, byte[] Grades, bool Closed, bool StartsAtADeadEnd, bool EndsAtADeadEnd);

    /// <summary>Smoothing passes over each lane (Chaikin): enough to round a corner, few enough to keep its place.</summary>
    private const int SmoothingPasses = 2;

    /// <summary>How far round a building a detour may look, in tiles beyond either end of the step (D583).</summary>
    private const int DetourReach = 4;

    /// <summary>A detour's step on unworn ground against 1 on worn: it keeps to where people walked (D583).</summary>
    private const int DetourBareGroundCost = 4;

    /// <summary>How far a doorstep lane may run along the worn ground to meet the nearest lane, in tiles (D605).</summary>
    private const int DoorstepReach = 6;

    /// <summary>
    /// How near a building's tile a lane must be drawn to touch its wall, in tiles (D607): the wall is half a tile out and
    /// a lane's ribbon a little more — a lane down the next tile's middle, at 1, passes by and gets its spur.
    /// </summary>
    private const float DoorstepTouches = 0.75f;

    /// <summary>The longest ring round one building that keeps only its busier side, in pixels round (D607).</summary>
    private const int RingAtMost = 20;

    /// <summary>How far past the doorstep's centre toward the wall a spur runs — to the wall, a hair short of its tile (D607).</summary>
    private const float DoorstepToTheWall = 0.45f;

    /// <summary>The detour's four ways, in a fixed order.</summary>
    private static readonly (int, int)[] FourWays = { (1, 0), (-1, 0), (0, 1), (0, -1) };

    private TrailNetwork(List<Lane> lanes, List<(Vector2 At, byte Grade)> junctions, int bridged, int cuts, int ringsOpened)
    {
        Lanes = lanes;
        Junctions = junctions;
        BridgedTiles = bridged;
        CutSteps = cuts;
        RingsOpened = ringsOpened;
    }

    /// <summary>The lanes, in the order traced.</summary>
    public IReadOnlyList<Lane> Lanes { get; }

    /// <summary>Where three or more lanes meet, and the best grade among them.</summary>
    public IReadOnlyList<(Vector2 At, byte Grade)> Junctions { get; }

    /// <summary>How many one-tile gaps were bridged — for the probe.</summary>
    public int BridgedTiles { get; }

    /// <summary>How many steps had no way round a building and were cut at its walls (D583: the last resort) — for the probe.</summary>
    public int CutSteps { get; }

    /// <summary>How many rings round one building were opened to their busier side (D607) — for the probe.</summary>
    public int RingsOpened { get; }

    /// <summary>A network with nothing in it.</summary>
    public static TrailNetwork Empty { get; } = new(new List<Lane>(), new List<(Vector2, byte)>(), 0, 0, 0);

    /// <summary>
    /// The network of the worn ground in a window of the valley.
    /// </summary>
    /// <param name="originX">The tile x of index 0.</param>
    /// <param name="originY">The tile y of index 0.</param>
    /// <param name="width">The window's width in tiles.</param>
    /// <param name="height">The window's height in tiles.</param>
    /// <param name="grade">0 unworn, 1 worn, 2 packed — row-major; already 0 on every blocked tile.</param>
    /// <param name="blocked">Tiles no lane may be drawn on or bridged across.</param>
    /// <param name="wander">The offset a point is moved by (the valley's seeded wander), or null for none.</param>
    /// <param name="walls">
    /// Each tile's walls as <c>ZoneMap</c>'s bits (1 north, 2 east, 4 south, 8 west), or null for none (D605): a wall
    /// separates two tiles everywhere the network looks — no bridge, no touching in the thinning or the trace, no point
    /// pulled across, no step drawn through.
    /// </param>
    /// <param name="buildingOf">
    /// Which building stands on each tile (0 for none), or null to draw no doorsteps (D605): a building with worn ground
    /// beside it that no lane comes within a tile of gets a short lane to its doorstep along the worn ground.
    /// </param>
    /// <param name="doors">
    /// ⭐ Each doored building's door, by its number in <paramref name="buildingOf"/>: the index of its door tile and of its
    /// doorstep (`doors.md §12.5`). A building listed is reached, and spurred, only through its door; one not listed — a
    /// well, a pile, a farm — by any side, as before.
    /// </param>
    public static TrailNetwork Build(
        int originX, int originY, int width, int height, byte[] grade, bool[] blocked, Func<Vector2, Vector2>? wander,
        byte[]? walls = null, int[]? buildingOf = null, IReadOnlyDictionary<int, (int Door, int Step)>? doors = null)
    {
        int n = width * height;
        if (grade.Length != n || blocked.Length != n || (walls is not null && walls.Length != n) || (buildingOf is not null && buildingOf.Length != n))
        {
            throw new ArgumentException($"A {width}×{height} window wants {n} grades, blocked flags, walls and buildings, not {grade.Length}, {blocked.Length}, {walls?.Length} and {buildingOf?.Length}.");
        }

        var linked = new Linkage(width, height, walls);

        // 1. The shape: worn ground, and every one-tile gap with worn ground facing across it — measured on the worn
        // ground alone, so a bridged tile never bridges the next (a two-tile gap is two lanes). ⛔ Never across a wall.
        var shape = new byte[n];
        int bridged = 0;
        for (int i = 0; i < n; i++)
        {
            if (grade[i] > 0)
            {
                shape[i] = grade[i];
                continue;
            }

            if (blocked[i])
            {
                continue;
            }

            int x = i % width;
            int y = i / width;
            byte across = Math.Max(Facing(x, y, 0, 1), Facing(x, y, 1, 0));
            if (across > 0)
            {
                shape[i] = across;
                bridged++;
            }
        }

        // 2. Thin it to its centre line, keeping a pixel for any piece the thinning would have erased whole.
        bool[] line = Thin(shape, width, height, linked);

        // 3. A staircase on the line keeps a pixel at each corner it does not need; drop them, so a diagonal is a
        // diagonal and not a run of tiny junctions.
        Dictionary<(int, int), int> wraps = DropCornerPixels(line, blocked, width, height, linked);

        // 4. Trace, place and smooth; then the doorsteps.
        return Trace(line, shape, blocked, wraps, originX, originY, width, height, wander, bridged, linked, buildingOf, doors);

        byte Facing(int x, int y, int dx, int dy)
        {
            byte a = GradeAt(x - dx, y - dy);
            byte b = GradeAt(x + dx, y + dy);
            int gap = (y * width) + x;
            return a > 0 && b > 0 && linked.Step(gap, -dx, -dy) && linked.Step(gap, dx, dy) ? Math.Min(a, b) : (byte)0;
        }

        byte GradeAt(int x, int y) => x < 0 || y < 0 || x >= width || y >= height ? (byte)0 : grade[(y * width) + x];
    }

    /// <summary>
    /// ⛔ Which tiles touch, with the fences in (D605): two tiles side by side touch unless a wall runs between them;
    /// two corner to corner touch if either way round the corner is open. Without walls, every neighbour touches.
    /// </summary>
    private sealed class Linkage
    {
        private readonly int _width;
        private readonly int _height;
        private readonly byte[]? _walls;

        public Linkage(int width, int height, byte[]? walls)
        {
            _width = width;
            _height = height;
            _walls = walls;
        }

        /// <summary>Whether a step from tile <paramref name="i"/> one tile across or down crosses no wall.</summary>
        public bool Step(int i, int dx, int dy)
        {
            if (_walls is null)
            {
                return true;
            }

            int x = (i % _width) + dx, y = (i / _width) + dy;
            if (x < 0 || y < 0 || x >= _width || y >= _height)
            {
                return true;
            }

            int j = (y * _width) + x;
            return (_walls[i] & Bit(dx, dy)) == 0 && (_walls[j] & Bit(-dx, -dy)) == 0;
        }

        /// <summary>Whether two of the eight neighbours touch.</summary>
        public bool Touch(int i, int j)
        {
            if (_walls is null)
            {
                return true;
            }

            int dx = (j % _width) - (i % _width), dy = (j / _width) - (i / _width);
            if (dx == 0 || dy == 0)
            {
                return Step(i, dx, dy);
            }

            return (Step(i, dx, 0) && Step(i + dx, 0, dy)) || (Step(i, 0, dy) && Step(i + (dy * _width), dx, 0));
        }

        private static byte Bit(int dx, int dy) => (dx, dy) switch { (0, -1) => 1, (1, 0) => 2, (0, 1) => 4, (-1, 0) => 8, _ => 0 };
    }

    /// <summary>Zhang–Suen thinning of the shape; a piece it would erase whole keeps the pixel nearest its middle.</summary>
    private static bool[] Thin(byte[] shape, int width, int height, Linkage linked)
    {
        int n = width * height;
        var on = new bool[n];
        for (int i = 0; i < n; i++)
        {
            on[i] = shape[i] > 0;
        }

        var doomed = new List<int>();
        bool changed = true;
        while (changed)
        {
            changed = false;
            for (int step = 0; step < 2; step++)
            {
                doomed.Clear();
                for (int i = 0; i < n; i++)
                {
                    if (!on[i])
                    {
                        continue;
                    }

                    int x = i % width;
                    int y = i / width;

                    // P2..P9 clockwise from north (screen y runs down) — ⛔ a neighbour across a fence is not on (D605).
                    bool p2 = From(i, x, y - 1), p3 = From(i, x + 1, y - 1), p4 = From(i, x + 1, y), p5 = From(i, x + 1, y + 1);
                    bool p6 = From(i, x, y + 1), p7 = From(i, x - 1, y + 1), p8 = From(i, x - 1, y), p9 = From(i, x - 1, y - 1);
                    int b = (p2 ? 1 : 0) + (p3 ? 1 : 0) + (p4 ? 1 : 0) + (p5 ? 1 : 0) + (p6 ? 1 : 0) + (p7 ? 1 : 0) + (p8 ? 1 : 0) + (p9 ? 1 : 0);
                    int a = Rise(p2, p3) + Rise(p3, p4) + Rise(p4, p5) + Rise(p5, p6) + Rise(p6, p7) + Rise(p7, p8) + Rise(p8, p9) + Rise(p9, p2);
                    bool outer = step == 0 ? !(p2 && p4 && p6) && !(p4 && p6 && p8) : !(p2 && p4 && p8) && !(p2 && p6 && p8);
                    if (b >= 2 && b <= 6 && a == 1 && outer)
                    {
                        doomed.Add(i);
                    }
                }

                foreach (int i in doomed)
                {
                    on[i] = false;
                }

                changed |= doomed.Count > 0;
            }
        }

        // ⚠️ Zhang–Suen erases a 2×2 whole. Every piece of the shape keeps at least the pixel nearest its middle.
        var seen = new bool[n];
        var piece = new List<int>();
        var queue = new Queue<int>();
        for (int start = 0; start < n; start++)
        {
            if (shape[start] == 0 || seen[start])
            {
                continue;
            }

            piece.Clear();
            seen[start] = true;
            queue.Enqueue(start);
            bool kept = false;
            float sx = 0f, sy = 0f;
            while (queue.Count > 0)
            {
                int i = queue.Dequeue();
                piece.Add(i);
                kept |= on[i];
                sx += i % width;
                sy += i / width;
                for (int dy = -1; dy <= 1; dy++)
                {
                    for (int dx = -1; dx <= 1; dx++)
                    {
                        int x = (i % width) + dx;
                        int y = (i / width) + dy;
                        if (x >= 0 && y >= 0 && x < width && y < height && shape[(y * width) + x] > 0 && !seen[(y * width) + x]
                            && linked.Touch(i, (y * width) + x))
                        {
                            seen[(y * width) + x] = true;
                            queue.Enqueue((y * width) + x);
                        }
                    }
                }
            }

            if (!kept)
            {
                float mx = sx / piece.Count, my = sy / piece.Count;
                int best = piece[0];
                float bestDistance = float.MaxValue;
                foreach (int i in piece)
                {
                    float d = ((i % width) - mx) * ((i % width) - mx) + ((i / width) - my) * ((i / width) - my);
                    if (d < bestDistance - 1e-6f || (Math.Abs(d - bestDistance) <= 1e-6f && i < best))
                    {
                        bestDistance = d;
                        best = i;
                    }
                }

                on[best] = true;
            }
        }

        return on;

        bool From(int i, int x, int y) => x >= 0 && y >= 0 && x < width && y < height && on[(y * width) + x] && linked.Touch(i, (y * width) + x);

        static int Rise(bool from, bool to) => !from && to ? 1 : 0;
    }

    /// <summary>
    /// A pixel whose only two neighbours already touch each other joins nothing — a staircase's corner, or a nub — and goes.
    /// ⛔ <b>But a dropped corner that the line between its neighbours would cut a building's corner without is
    /// remembered</b>, by that pair (D582): the line's shape loses it, the drawing goes round it. Kept as a pixel, it
    /// gave every pixel of a ring three neighbours and the whole ring became one junction at the building's middle.
    /// </summary>
    private static Dictionary<(int, int), int> DropCornerPixels(bool[] line, bool[] blocked, int width, int height, Linkage linked)
    {
        var wraps = new Dictionary<(int, int), int>();
        for (int i = 0; i < line.Length; i++)
        {
            if (!line[i] || Degree(line, i, width, height, linked) != 2)
            {
                continue;
            }

            int x = i % width;
            int y = i / width;
            int first = -1, second = -1;
            for (int dy = -1; dy <= 1; dy++)
            {
                for (int dx = -1; dx <= 1; dx++)
                {
                    int nx = x + dx, ny = y + dy;
                    if ((dx != 0 || dy != 0) && nx >= 0 && ny >= 0 && nx < width && ny < height && line[(ny * width) + nx]
                        && linked.Touch(i, (ny * width) + nx))
                    {
                        if (first < 0)
                        {
                            first = (ny * width) + nx;
                        }
                        else
                        {
                            second = (ny * width) + nx;
                        }
                    }
                }
            }

            // ⛔ Only when the two neighbours touch each other without it (D605): a corner pixel that is the way round a
            // fence's end is a link, not a nub.
            int fx = first % width, fy = first / width, sx = second % width, sy = second / width;
            if (Math.Abs(fx - sx) > 1 || Math.Abs(fy - sy) > 1 || !linked.Touch(first, second))
            {
                continue;
            }

            // Diagonal to each other, the two neighbours and this pixel are three of a square; the fourth is the tile the
            // line between them would cut the corner of.
            int ox = fx + sx - x, oy = fy + sy - y;
            bool wrapsABuilding = Math.Abs(fx - sx) == 1 && Math.Abs(fy - sy) == 1
                && ox >= 0 && oy >= 0 && ox < width && oy < height && blocked[(oy * width) + ox];
            line[i] = false;
            if (wrapsABuilding)
            {
                wraps[first < second ? (first, second) : (second, first)] = i;
            }
        }

        return wraps;
    }

    private static int Degree(bool[] line, int i, int width, int height, Linkage linked)
    {
        int x = i % width;
        int y = i / width;
        int degree = 0;
        for (int dy = -1; dy <= 1; dy++)
        {
            for (int dx = -1; dx <= 1; dx++)
            {
                int nx = x + dx, ny = y + dy;
                if ((dx != 0 || dy != 0) && nx >= 0 && ny >= 0 && nx < width && ny < height && line[(ny * width) + nx]
                    && linked.Touch(i, (ny * width) + nx))
                {
                    degree++;
                }
            }
        }

        return degree;
    }

    /// <summary>The line traced into lanes between its ends and junctions, every point placed and smoothed.</summary>
    private static TrailNetwork Trace(
        bool[] line, byte[] shape, bool[] blocked, Dictionary<(int, int), int> wraps, int originX, int originY, int width, int height,
        Func<Vector2, Vector2>? wander, int bridged, Linkage linked, int[]? buildingOf, IReadOnlyDictionary<int, (int Door, int Step)>? doors)
    {
        int n = line.Length;
        var paths = new List<List<int>>();
        var closed = new List<bool>();
        var walked = new HashSet<(int, int)>();
        var ends = new int[n];

        // ⭐ A CLUMP OF TOUCHING JUNCTION PIXELS IS ONE JUNCTION: the middle of a thinned cross is several pixels each with
        // three neighbours, and walked pixel by pixel it drew a knot of tiny lanes between them. Each pixel of a clump
        // carries the clump's lowest pixel; no lane runs inside a clump, and every lane meets it at its middle.
        var hub = new int[n];

        // ⛔ NO STUB PRUNING (D581, measured): cutting short dead ends off a junction is the textbook skeleton clean-up, and
        // on Joe's own village it cut the three-tile lane up to a store's door — a lane, not a stub — while changing nothing
        // in any shape the probe draws. The clumps above are what keep a hub tidy.
        Walk();
        int ringsOpened = 0;
        if (buildingOf is not null)
        {
            KeepTheBusierSides(buildingOf);
        }

        // ⭐⭐ BUT A CLUMP WIDER THAN 3 × 3 IS A PLAZA, NOT A CROSSING (D605, Joe's star: "doesn't look organic - how would
        // real humans behave on those paths?"). A packed square thins to a mesh whose junction pixels all touch, so it was one
        // junction — fifteen and seventeen lanes meeting at one point on his villages, half of them loops out of it and back.
        // Capped at 3 × 3 the mesh drew as forty junctions. Neither is how people cross a square: each lane stops where it
        // arrives, and the arrivals are joined by a TREE on the plaza's own worn skeleton — the nearest arrival added one
        // at a time by its pixel path — so nothing loops, branches part where the ground does, and every step is worn.
        var plaza = new bool[n];
        var extent = new Dictionary<int, (int MinX, int MinY, int MaxX, int MaxY)>();
        for (int i = 0; i < n; i++)
        {
            if (hub[i] < 0)
            {
                continue;
            }

            int x = i % width, y = i / width;
            extent[hub[i]] = extent.TryGetValue(hub[i], out var e)
                ? (Math.Min(e.MinX, x), Math.Min(e.MinY, y), Math.Max(e.MaxX, x), Math.Max(e.MaxY, y))
                : (x, y, x, y);
        }

        for (int i = 0; i < n; i++)
        {
            plaza[i] = hub[i] >= 0 && extent[hub[i]] is var e && (e.MaxX - e.MinX > 2 || e.MaxY - e.MinY > 2);
        }

        var hubPoints = new Dictionary<int, Vector2>();
        int cuts = 0;
        var lanes = new List<Lane>();
        for (int p = 0; p < paths.Count; p++)
        {
            List<int> path = paths[p];
            lanes.AddRange(Placed(path, closed[p], !closed[p] && path.Count > 1 && End(path[0]) == 1, !closed[p] && path.Count > 1 && End(path[^1]) == 1));
        }

        // The plazas' trees, and how many lanes and branches meet at each of their pixels.
        var meetAt = new Dictionary<int, int>();
        foreach (List<int> path in paths)
        {
            if (path.Count > 1)
            {
                foreach (int end in new[] { path[0], path[^1] })
                {
                    if (plaza[end])
                    {
                        meetAt[end] = meetAt.GetValueOrDefault(end) + 1;
                    }
                }
            }
        }

        foreach (int rep in extent.Keys.Order())
        {
            if (!plaza[rep])
            {
                continue;
            }

            foreach (List<int> branch in PlazaTree(rep, meetAt.Keys.Where(a => hub[a] == rep).Order().ToList()))
            {
                meetAt[branch[0]] = meetAt.GetValueOrDefault(branch[0]) + 1;
                meetAt[branch[^1]] = meetAt.GetValueOrDefault(branch[^1]) + 1;
                lanes.AddRange(Placed(branch, false, false, false));
            }
        }

        var junctions = new List<(Vector2, byte)>();
        for (int i = 0; i < n; i++)
        {
            if (hub[i] == i && !plaza[i] && ends[i] >= 3)
            {
                byte best = 0;
                for (int k = 0; k < n; k++)
                {
                    best = hub[k] == i ? Math.Max(best, shape[k]) : best;
                }

                junctions.Add((HubPoint(i), best));
            }
            else if (plaza[i] && meetAt.GetValueOrDefault(i) >= 3)
            {
                junctions.Add((PointOf(i), shape[i]));
            }
        }

        if (buildingOf is not null)
        {
            lanes.AddRange(Doorsteps(buildingOf));
        }

        return new TrailNetwork(lanes, junctions, bridged, cuts, ringsOpened);

        // ⭐ A RING ROUND ONE SMALL BUILDING KEEPS ITS BUSIER SIDE (Joe, D607: "weird looking paths" — rings and hooks round
        // single stores; his call, "Busier side only"). People do wear both sides of a store they pass from both ways, but a
        // lane circling it reads as nobody going in. Two lanes between the same two junctions that together ring exactly one
        // building — at most `RingAtMost` pixels round — lose the less worn (by mean grade; then the longer; then the later);
        // a lane that leaves a junction, rings one building and comes back is cut at its far point, and its busier half kept
        // as a dead end. The ground stays worn; the drawing says which way it is walked.
        void KeepTheBusierSides(int[] buildings)
        {
            var dropped = new HashSet<int>();
            for (int p = 0; p < paths.Count; p++)
            {
                List<int> path = paths[p];
                if (closed[p] || path.Count < 2 || dropped.Contains(p))
                {
                    continue;
                }

                int a = Key(path[0]), b = Key(path[^1]);
                if (SamePlace(path[0], path[^1]))
                {
                    if (path.Count <= RingAtMost && RingsOneBuilding(path, buildings))
                    {
                        int middle = path.Count / 2;
                        List<int> first = path.GetRange(0, middle + 1);
                        List<int> second = path.GetRange(middle, path.Count - middle);
                        second.Reverse();
                        List<int> kept = MeanGrade(second) > MeanGrade(first) ? second : first;
                        ends[ReferenceEquals(kept, first) ? b : a]--;
                        ends[Key(kept[^1])]++;
                        paths[p] = kept;
                        ringsOpened++;
                    }

                    continue;
                }

                for (int q = p + 1; q < paths.Count; q++)
                {
                    List<int> other = paths[q];
                    if (closed[q] || other.Count < 2 || dropped.Contains(q)
                        || !((SamePlace(other[0], path[0]) && SamePlace(other[^1], path[^1])) || (SamePlace(other[0], path[^1]) && SamePlace(other[^1], path[0])))
                        || path.Count + other.Count > RingAtMost)
                    {
                        continue;
                    }

                    var ring = new List<int>(path);
                    var back = new List<int>(other);
                    if (SamePlace(back[0], path[0]))
                    {
                        back.Reverse();
                    }

                    ring.AddRange(back.Skip(1).Take(back.Count - 2));
                    if (!RingsOneBuilding(ring, buildings))
                    {
                        continue;
                    }

                    float mp = MeanGrade(path), mq = MeanGrade(other);
                    int loser = mp < mq ? p : mq < mp ? q : other.Count > path.Count ? q : path.Count > other.Count ? p : q;
                    dropped.Add(loser);
                    ringsOpened++;
                    ends[Key(paths[loser][0])]--;
                    ends[Key(paths[loser][^1])]--;
                    break;
                }
            }

            foreach (int p in dropped.OrderDescending())
            {
                paths.RemoveAt(p);
                closed.RemoveAt(p);
            }
        }

        float MeanGrade(List<int> path) => (float)path.Average(i => shape[i]);

        // Two lane ends are one place when they share a junction or lie within two pixels — a junction the thinning split in
        // two, joined by a one-pixel stub, is still one crossing (D607: the ring in the probe's shape ended at two such).
        bool SamePlace(int i, int j) =>
            Key(i) == Key(j) || (Math.Abs((i % width) - (j % width)) <= 2 && Math.Abs((i / width) - (j / width)) <= 2);

        // Whether a ring of pixels encloses tiles of exactly one building and nothing worn of its own inside.
        bool RingsOneBuilding(List<int> ring, int[] buildings)
        {
            int x0 = ring.Min(i => i % width), x1 = ring.Max(i => i % width);
            int y0 = ring.Min(i => i / width), y1 = ring.Max(i => i / width);
            var onRing = new HashSet<int>(ring);
            int building = 0;
            for (int y = y0; y <= y1; y++)
            {
                for (int x = x0; x <= x1; x++)
                {
                    int i = (y * width) + x;
                    if (onRing.Contains(i) || !Inside(x, y))
                    {
                        continue;
                    }

                    if (buildings[i] == 0)
                    {
                        if (line[i])
                        {
                            return false;
                        }

                        continue;
                    }

                    if (building != 0 && buildings[i] != building)
                    {
                        return false;
                    }

                    building = buildings[i];
                }
            }

            return building != 0;

            // Ray to the east, crossing the ring's edges between pixel centres.
            bool Inside(int x, int y)
            {
                bool inside = false;
                for (int k = 0; k < ring.Count; k++)
                {
                    int p = ring[k], q = ring[(k + 1) % ring.Count];
                    float px = p % width, py = p / width, qx = q % width, qy = q / width;
                    if ((py > y) != (qy > y) && x < px + ((y - py) * (qx - px) / (qy - py)))
                    {
                        inside = !inside;
                    }
                }

                return inside;
            }
        }

        // The tree joining a plaza's arrivals along its own pixels (D605): from the first arrival, the nearest one not yet
        // joined is reached from the whole tree at once, lowest pixel first, and its path becomes part of the tree; then the
        // tree is walked into branches between its forks and its arrivals.
        List<List<int>> PlazaTree(int rep, List<int> arrivals)
        {
            var branches = new List<List<int>>();
            if (arrivals.Count < 2)
            {
                return branches;
            }

            var isArrival = new HashSet<int>(arrivals);
            var inTree = new SortedSet<int> { arrivals[0] };
            var adjacency = new SortedDictionary<int, SortedSet<int>>();
            var waiting = new SortedSet<int>(arrivals.Skip(1));
            while (waiting.Count > 0)
            {
                var back = new Dictionary<int, int>();
                var queue = new Queue<int>();
                foreach (int t in inTree)
                {
                    back[t] = -1;
                    queue.Enqueue(t);
                }

                int found = -1;
                while (queue.Count > 0)
                {
                    int here = queue.Dequeue();
                    if (waiting.Contains(here))
                    {
                        found = here;
                        break;
                    }

                    foreach (int next in NeighboursOf(here))
                    {
                        if (hub[next] == rep && !back.ContainsKey(next))
                        {
                            back[next] = here;
                            queue.Enqueue(next);
                        }
                    }
                }

                if (found < 0)
                {
                    break;
                }

                for (int at = found; back[at] >= 0; at = back[at])
                {
                    Link(at, back[at]);
                    inTree.Add(at);
                }

                waiting.Remove(found);
            }

            var walkedTree = new HashSet<(int, int)>();
            foreach (int start in adjacency.Keys)
            {
                if (adjacency[start].Count == 2 && !isArrival.Contains(start))
                {
                    continue;
                }

                foreach (int first in adjacency[start])
                {
                    if (!walkedTree.Add(Edge(start, first)))
                    {
                        continue;
                    }

                    var branch = new List<int> { start };
                    int previous = start, here = first;
                    while (true)
                    {
                        branch.Add(here);
                        if (adjacency[here].Count != 2 || isArrival.Contains(here))
                        {
                            break;
                        }

                        int onward = adjacency[here].First(c => c != previous);
                        walkedTree.Add(Edge(here, onward));
                        previous = here;
                        here = onward;
                    }

                    branches.Add(branch);
                }
            }

            return branches;

            void Link(int a, int b)
            {
                Add(a, b);
                Add(b, a);
            }

            void Add(int a, int b)
            {
                if (!adjacency.TryGetValue(a, out SortedSet<int>? set))
                {
                    adjacency[a] = set = new SortedSet<int>();
                }

                set.Add(b);
            }
        }

        // ⭐ THE DOORSTEPS (D605, Joe: "we're missing some of the shorter sections of pathways to-and-from entrances ...
        // nothing shows, even after decades"). The ground in front of a building is worn — 48 of 54 buildings on his
        // Year-58 village — but a lane passing a tile or two off thins its doorstep into itself. A building with worn
        // ground beside it and no line pixel within a tile of it gets a short lane: from the nearest line pixel, along the
        // worn ground (four ways, through no wall, at most `DoorstepReach` steps), to the worn tile against its wall, and on
        // to the wall. Never over unworn ground (§1.1), nothing pruned (D581) — only what the thinning absorbed, drawn.
        List<Lane> Doorsteps(int[] buildings)
        {
            var tilesOf = new SortedDictionary<int, List<int>>();
            for (int i = 0; i < n; i++)
            {
                if (buildings[i] > 0)
                {
                    if (!tilesOf.TryGetValue(buildings[i], out List<int>? tiles))
                    {
                        tilesOf[buildings[i]] = tiles = new List<int>();
                    }

                    tiles.Add(i);
                }
            }

            var added = new List<Lane>();
            foreach ((int id, List<int> all) in tilesOf)
            {
                bool reached = false;
                var steps = new SortedDictionary<int, int>();

                // ⭐ A DOORED BUILDING IS REACHED THROUGH ITS DOOR (`doors.md §12.5`): only its door's wall is asked, and only
                // its doorstep is spurred to — the walks have converged there since D622, so that is where the ground wears.
                (int Door, int Step)? door = doors is not null && doors.TryGetValue(id, out (int Door, int Step) d) ? d : null;
                IReadOnlyList<int> tiles = door is { } at ? new[] { at.Door } : all;

                // ⭐ REACHED ONLY WHEN A LANE IS DRAWN TOUCHING ITS WALL (Joe, D607: "paths not connecting to building
                // entrances" — his red marks in D600's screenshot are spurs from the passing lane to each wall). A lane
                // passing a tile off, drawn on its own tile's middle, goes BY the building; it gets its spur.
                foreach (int t in tiles)
                {
                    int tx = t % width, ty = t / width;
                    var wallTile = new Vector2(originX + tx, originY + ty);
                    foreach ((int dx, int dy) in FourWays)
                    {
                        int nx = tx + dx, ny = ty + dy;
                        int step = (ny * width) + nx;
                        if (door is { } through && step != through.Step)
                        {
                            continue;
                        }

                        reached |= nx >= 0 && ny >= 0 && nx < width && ny < height && line[step] && linked.Step(t, dx, dy)
                            && PointOf(step).DistanceTo(wallTile) <= DoorstepTouches;
                        if (nx >= 0 && ny >= 0 && nx < width && ny < height && shape[step] > 0 && !blocked[step]
                            && linked.Step(t, dx, dy) && !steps.ContainsKey(step))
                        {
                            steps[step] = t;
                        }
                    }
                }

                if (reached || steps.Count == 0)
                {
                    continue;
                }

                // From every doorstep at once, lowest first, to the nearest line pixel.
                var back = new Dictionary<int, int>();
                var depth = new Dictionary<int, int>();
                var queue = new Queue<int>();
                foreach (int step in steps.Keys)
                {
                    depth[step] = 0;
                    queue.Enqueue(step);
                }

                int joined = -1;
                while (queue.Count > 0 && joined < 0)
                {
                    int here = queue.Dequeue();
                    if (line[here])
                    {
                        joined = here;
                        break;
                    }

                    if (depth[here] >= DoorstepReach)
                    {
                        continue;
                    }

                    foreach ((int dx, int dy) in FourWays)
                    {
                        int nx = (here % width) + dx, ny = (here / width) + dy;
                        int there = (ny * width) + nx;
                        if (nx >= 0 && ny >= 0 && nx < width && ny < height && shape[there] > 0 && !blocked[there]
                            && linked.Step(here, dx, dy) && !depth.ContainsKey(there))
                        {
                            depth[there] = depth[here] + 1;
                            back[there] = here;
                            queue.Enqueue(there);
                        }
                    }
                }

                if (joined < 0)
                {
                    continue;
                }

                // The line pixel first, as the lane through it is placed; then tile centres to the doorstep, and on to the wall.
                var route = new List<int> { joined };
                while (back.TryGetValue(route[^1], out int previous))
                {
                    route.Add(previous);
                }

                int doorstep = route[^1];
                int wall = steps[doorstep];
                var points = new List<Vector2> { PointOf(joined) };
                var grades = new List<byte> { shape[joined] };
                for (int k = 1; k < route.Count; k++)
                {
                    points.Add(new Vector2(originX + (route[k] % width), originY + (route[k] / width)));
                    grades.Add(shape[route[k]]);
                }

                if (route.Count == 1)
                {
                    points.Add(new Vector2(originX + (doorstep % width), originY + (doorstep / width)));
                    grades.Add(shape[doorstep]);
                }

                Vector2 step0 = new(originX + (doorstep % width), originY + (doorstep / width));
                Vector2 against = new(originX + (wall % width), originY + (wall / width));
                points.Add(step0.Lerp(against, DoorstepToTheWall));
                grades.Add(shape[doorstep]);

                Vector2[] drawn = points.ToArray();
                byte[] graded = grades.ToArray();
                for (int pass = 0; pass < SmoothingPasses && drawn.Length > 2; pass++)
                {
                    (drawn, graded) = Chaikin(drawn, graded, false, Crosses);
                }

                // ⚠️ No taper at the wall (D607): a spur meets the building at full width, or it reads as stopping short.
                added.Add(new Lane(drawn, graded, false, false, false));
            }

            return added;
        }

        int Key(int i) => hub[i] >= 0 ? hub[i] : i;

        int End(int i) => ends[Key(i)];

        Vector2 HubPoint(int rep)
        {
            if (hubPoints.TryGetValue(rep, out Vector2 known))
            {
                return known;
            }

            Vector2 sum = Vector2.Zero;
            int count = 0;
            for (int k = 0; k < n; k++)
            {
                if (hub[k] == rep)
                {
                    sum += PointOf(k);
                    count++;
                }
            }

            Vector2 middle = sum / count;
            if (OnABuilding(middle))
            {
                // A clump round a building has its middle on it; the clump's own pixel nearest that middle instead.
                float nearest = float.MaxValue;
                for (int k = 0; k < n; k++)
                {
                    if (hub[k] == rep && PointOf(k).DistanceSquaredTo(middle) < nearest)
                    {
                        nearest = PointOf(k).DistanceSquaredTo(middle);
                        sum = PointOf(k);
                    }
                }

                middle = sum;
            }

            hubPoints[rep] = middle;
            return middle;
        }

        void Walk()
        {
            paths.Clear();
            closed.Clear();
            walked.Clear();
            Array.Clear(ends);

            // The clumps, lowest pixel first.
            Array.Fill(hub, -1);
            var clump = new Queue<int>();
            for (int i = 0; i < n; i++)
            {
                if (!line[i] || hub[i] >= 0 || Degree(line, i, width, height, linked) < 3)
                {
                    continue;
                }

                // Grown whole: a clump wider than 3 × 3 is a plaza, joined by its tree (D605, above).
                hub[i] = i;
                clump.Enqueue(i);
                while (clump.Count > 0)
                {
                    foreach (int other in NeighboursOf(clump.Dequeue()))
                    {
                        if (hub[other] < 0 && Degree(line, other, width, height, linked) >= 3)
                        {
                            hub[other] = i;
                            clump.Enqueue(other);
                        }
                    }
                }
            }

            // From every pixel that is not a plain link (an end, a junction, or alone), along each way out.
            for (int i = 0; i < n; i++)
            {
                if (!line[i])
                {
                    continue;
                }

                int degree = Degree(line, i, width, height, linked);
                if (degree == 0)
                {
                    paths.Add(new List<int> { i });
                    closed.Add(false);
                    continue;
                }

                if (degree == 2)
                {
                    continue;
                }

                foreach (int next in NeighboursOf(i))
                {
                    if (walked.Contains(Edge(i, next)))
                    {
                        continue;
                    }

                    if (hub[i] >= 0 && hub[next] == hub[i])
                    {
                        walked.Add(Edge(i, next));
                        continue;
                    }

                    var path = new List<int> { i };
                    int previous = i, here = next;
                    walked.Add(Edge(previous, here));
                    while (true)
                    {
                        path.Add(here);
                        if (Degree(line, here, width, height, linked) != 2)
                        {
                            break;
                        }

                        int onward = -1;
                        foreach (int candidate in NeighboursOf(here))
                        {
                            if (candidate != previous && !walked.Contains(Edge(here, candidate)))
                            {
                                onward = candidate;
                                break;
                            }
                        }

                        if (onward < 0)
                        {
                            break;
                        }

                        walked.Add(Edge(here, onward));
                        previous = here;
                        here = onward;
                    }

                    paths.Add(path);
                    closed.Add(false);
                }
            }

            // Whatever is left is loops of plain links — each started at its lowest pixel.
            for (int i = 0; i < n; i++)
            {
                if (!line[i] || Degree(line, i, width, height, linked) != 2 || AnyWalked(i))
                {
                    continue;
                }

                var loop = new List<int> { i };
                int previous = i;
                int here = NeighboursOf(i)[0];
                walked.Add(Edge(previous, here));
                while (here != i)
                {
                    loop.Add(here);
                    int onward = -1;
                    foreach (int candidate in NeighboursOf(here))
                    {
                        if (candidate != previous && !walked.Contains(Edge(here, candidate)))
                        {
                            onward = candidate;
                            break;
                        }
                    }

                    if (onward < 0)
                    {
                        break;
                    }

                    walked.Add(Edge(here, onward));
                    previous = here;
                    here = onward;
                }

                paths.Add(loop);
                closed.Add(true);
            }

            foreach (List<int> path in paths)
            {
                if (path.Count > 1)
                {
                    ends[Key(path[0])]++;
                    ends[Key(path[^1])]++;
                }
            }
        }

        List<Lane> Placed(List<int> path, bool loop, bool startsDead, bool endsDead)
        {
            // Each point, and the pixel it came from (−1 for a junction's middle, which stays put).
            var placed = new List<Vector2>(path.Count + 4);
            var from = new List<int>(path.Count + 4);
            var graded = new List<byte>(path.Count + 4);
            int last = loop ? path.Count : path.Count - 1;
            for (int k = 0; k < path.Count; k++)
            {
                bool atAHub = (k == 0 || k == path.Count - 1) && hub[path[k]] >= 0 && !plaza[path[k]];
                placed.Add(atAHub ? HubPoint(hub[path[k]]) : PointOf(path[k]));
                from.Add(atAHub ? -1 : path[k]);
                graded.Add(shape[path[k]]);
                int next = path[(k + 1) % path.Count];
                if (k < last && wraps.TryGetValue(path[k] < next ? (path[k], next) : (next, path[k]), out int corner))
                {
                    // ⛔ Round the building's corner, not across it (D582).
                    placed.Add(Unwandered(corner));
                    from.Add(corner);
                    graded.Add(Math.Min(shape[path[k]], shape[next]));
                }
            }

            // ⛔ A wander that would carry the line across a building is not taken (D582): both ends of that step go back
            // to where they were placed.
            int steps = loop ? placed.Count : placed.Count - 1;
            for (int k = 0; k < steps; k++)
            {
                int next = (k + 1) % placed.Count;
                if (Crosses(placed[k], placed[next]))
                {
                    placed[k] = from[k] < 0 ? placed[k] : Unwandered(from[k]);
                    placed[next] = from[next] < 0 ? placed[next] : Unwandered(from[next]);
                }
            }

            // ⭐⭐ ROUND THE BUILDING, NOT ACROSS IT, AND NOT CUT AT ITS WALLS (D583, Joe: "the path should go around the
            // building"). A step that would still cross one is replaced by the ground round it — the worn ground first —
            // from tile centre to tile centre, four ways, so no step of the detour clips a corner.
            var routed = new List<Vector2>(placed.Count + 8);
            var routedGrades = new List<byte>(placed.Count + 8);
            steps = loop ? placed.Count : placed.Count - 1;
            for (int k = 0; k < placed.Count; k++)
            {
                routed.Add(placed[k]);
                routedGrades.Add(graded[k]);
                int next = (k + 1) % placed.Count;
                if (k < steps && Crosses(placed[k], placed[next]) && Detour(placed[k], placed[next]) is List<Vector2> round)
                {
                    byte g = Math.Min(graded[k], graded[next]);
                    foreach (Vector2 point in round)
                    {
                        routed.Add(point);
                        routedGrades.Add(g);
                    }
                }
            }

            Vector2[] points = routed.ToArray();
            byte[] grades = routedGrades.ToArray();

            for (int pass = 0; pass < SmoothingPasses && points.Length > 2; pass++)
            {
                (points, grades) = Chaikin(points, grades, loop, Crosses);
            }

            return CutAtBuildings(points, grades, loop, startsDead, endsDead);
        }

        // ⛔ THE LAST RESORT (D582; since D583 only when there is no way round — a building walled in on every side, which
        // placement refuses): a step that would still cross a building is not drawn, and the lane stops at its walls.
        // A line over a building is a path nobody could walk.

        // The tiles round a building from a's tile to b's: Dijkstra, four ways, over unblocked tiles within four of
        // either end — a worn tile costs 1, bare ground 4, so the detour keeps to where people walked. Tile centres, both
        // ends included; null if nothing goes round.
        List<Vector2>? Detour(Vector2 a, Vector2 b)
        {
            int ax = Mathf.RoundToInt(a.X) - originX, ay = Mathf.RoundToInt(a.Y) - originY;
            int bx = Mathf.RoundToInt(b.X) - originX, by = Mathf.RoundToInt(b.Y) - originY;
            int x0 = Math.Max(0, Math.Min(ax, bx) - DetourReach), x1 = Math.Min(width - 1, Math.Max(ax, bx) + DetourReach);
            int y0 = Math.Max(0, Math.Min(ay, by) - DetourReach), y1 = Math.Min(height - 1, Math.Max(ay, by) + DetourReach);
            if (!Inside(ax, ay) || !Inside(bx, by) || blocked[(ay * width) + ax] || blocked[(by * width) + bx])
            {
                return null;
            }

            int start = (ay * width) + ax, goal = (by * width) + bx;
            var cost = new Dictionary<int, int> { [start] = 0 };
            var back = new Dictionary<int, int>();
            var open = new PriorityQueue<int, long>();
            open.Enqueue(start, start);
            while (open.TryDequeue(out int here, out long priority))
            {
                int settled = (int)(priority / n);
                if (settled != cost[here])
                {
                    continue;
                }

                if (here == goal)
                {
                    break;
                }

                int hx = here % width, hy = here / width;
                foreach ((int dx, int dy) in FourWays)
                {
                    int nx = hx + dx, ny = hy + dy;
                    if (!Inside(nx, ny) || nx < x0 || nx > x1 || ny < y0 || ny > y1 || blocked[(ny * width) + nx] || !linked.Step(here, dx, dy))
                    {
                        continue;
                    }

                    int there = (ny * width) + nx;
                    int step = settled + (shape[there] > 0 ? 1 : DetourBareGroundCost);
                    if (!cost.TryGetValue(there, out int known) || step < known || (step == known && here < back[there]))
                    {
                        cost[there] = step;
                        back[there] = here;
                        open.Enqueue(there, ((long)step * n) + there);
                    }
                }
            }

            if (!back.ContainsKey(goal) && goal != start)
            {
                return null;
            }

            var tiles = new List<int> { goal };
            while (tiles[^1] != start)
            {
                tiles.Add(back[tiles[^1]]);
            }

            tiles.Reverse();
            var round = new List<Vector2>(tiles.Count);
            foreach (int t in tiles)
            {
                round.Add(new Vector2(originX + (t % width), originY + (t / width)));
            }

            return round;

            bool Inside(int x, int y) => x >= 0 && y >= 0 && x < width && y < height;
        }
        List<Lane> CutAtBuildings(Vector2[] points, byte[] grades, bool loop, bool startsDead, bool endsDead)
        {
            int count = points.Length;
            int segments = loop ? count : count - 1;
            int firstCut = -1;
            for (int k = 0; k < segments && firstCut < 0; k++)
            {
                firstCut = Crosses(points[k], points[(k + 1) % count]) ? k : -1;
            }

            if (firstCut < 0)
            {
                return new List<Lane> { new(points, grades, loop, startsDead, endsDead) };
            }

            for (int k = 0; k < segments; k++)
            {
                cuts += Crosses(points[k], points[(k + 1) % count]) ? 1 : 0;
            }

            // Walk once round (a loop starts just past its first cut), starting a new piece after every cut.
            var pieces = new List<Lane>();
            var piecePoints = new List<Vector2>();
            var pieceGrades = new List<byte>();
            int start = loop ? (firstCut + 1) % count : 0;
            bool atTheStart = !loop;
            for (int step = 0; step < count; step++)
            {
                int k = (start + step) % count;
                piecePoints.Add(points[k]);
                pieceGrades.Add(grades[k]);
                bool lastPoint = step == count - 1;
                bool cutAfter = !lastPoint && Crosses(points[k], points[(k + 1) % count]);
                if (cutAfter || lastPoint)
                {
                    if (piecePoints.Count > 1)
                    {
                        pieces.Add(new Lane(piecePoints.ToArray(), pieceGrades.ToArray(), false,
                            atTheStart && startsDead, lastPoint && !loop && endsDead));
                    }

                    piecePoints.Clear();
                    pieceGrades.Clear();
                    atTheStart = false;
                }
            }

            return pieces;
        }

        // ⭐ The middle of the worn ground round the pixel, so a two-wide corridor lands on its midline and a lane beside a
        // building is pulled off it; then the valley's wander.
        Vector2 PointOf(int i)
        {
            int x = i % width;
            int y = i / width;
            float sx = 0f, sy = 0f;
            int count = 0;
            for (int dy = -1; dy <= 1; dy++)
            {
                for (int dx = -1; dx <= 1; dx++)
                {
                    int nx = x + dx, ny = y + dy;
                    if (nx >= 0 && ny >= 0 && nx < width && ny < height && shape[(ny * width) + nx] > 0 && linked.Touch(i, (ny * width) + nx))
                    {
                        sx += nx;
                        sy += ny;
                        count++;
                    }
                }
            }

            Vector2 at = Unwandered(i);
            if (wander is null)
            {
                return at;
            }

            Vector2 wandered = at + wander(at);
            return OnABuilding(wandered) ? at : wandered;
        }

        // The middle of the worn ground round the pixel — or, should that middle fall on a building, the pixel's own centre.
        Vector2 Unwandered(int i)
        {
            int x = i % width;
            int y = i / width;
            float sx = 0f, sy = 0f;
            int count = 0;
            for (int dy = -1; dy <= 1; dy++)
            {
                for (int dx = -1; dx <= 1; dx++)
                {
                    int nx = x + dx, ny = y + dy;
                    if (nx >= 0 && ny >= 0 && nx < width && ny < height && shape[(ny * width) + nx] > 0 && linked.Touch(i, (ny * width) + nx))
                    {
                        sx += nx;
                        sy += ny;
                        count++;
                    }
                }
            }

            var middle = new Vector2(originX + (sx / count), originY + (sy / count));
            return OnABuilding(middle) ? new Vector2(originX + x, originY + y) : middle;
        }

        bool OnABuilding(Vector2 point)
        {
            int x = Mathf.RoundToInt(point.X) - originX;
            int y = Mathf.RoundToInt(point.Y) - originY;
            return x >= 0 && y >= 0 && x < width && y < height && blocked[(y * width) + x];
        }

        // Whether the line from a to b passes over a building — every tile it enters, walked exactly (a tile is the unit
        // square round its integer centre). ⚠️ Not sampled: five samples along a long diagonal slipped past a building's
        // corner on Joe's village, and the smoothing then cut the shorter step it made (D583).
        bool Crosses(Vector2 a, Vector2 b)
        {
            int x = Mathf.RoundToInt(a.X), y = Mathf.RoundToInt(a.Y);
            int endX = Mathf.RoundToInt(b.X), endY = Mathf.RoundToInt(b.Y);
            if (TileBlocked(x, y))
            {
                return true;
            }

            float dx = b.X - a.X, dy = b.Y - a.Y;
            int stepX = Math.Sign(dx), stepY = Math.Sign(dy);
            float tMaxX = stepX == 0 ? float.MaxValue : (x + (stepX * 0.5f) - a.X) / dx;
            float tMaxY = stepY == 0 ? float.MaxValue : (y + (stepY * 0.5f) - a.Y) / dy;
            float tDeltaX = stepX == 0 ? float.MaxValue : stepX / dx;
            float tDeltaY = stepY == 0 ? float.MaxValue : stepY / dy;
            for (int guard = 0; (x != endX || y != endY) && guard < 64; guard++)
            {
                int fromX = x, fromY = y;
                if (tMaxX < tMaxY)
                {
                    x += stepX;
                    tMaxX += tDeltaX;
                }
                else
                {
                    y += stepY;
                    tMaxY += tDeltaY;
                }

                // ⛔ A fence crossed is a building crossed (D605): the line must go round it, through the gate.
                if (TileBlocked(x, y) || Walled(fromX, fromY, x - fromX, y - fromY))
                {
                    return true;
                }
            }

            return false;
        }

        bool TileBlocked(int x, int y)
        {
            int wx = x - originX, wy = y - originY;
            return wx >= 0 && wy >= 0 && wx < width && wy < height && blocked[(wy * width) + wx];
        }

        bool Walled(int x, int y, int dx, int dy)
        {
            int wx = x - originX, wy = y - originY;
            return wx >= 0 && wy >= 0 && wx < width && wy < height && !linked.Step((wy * width) + wx, dx, dy);
        }

        List<int> NeighboursOf(int i)
        {
            var found = new List<int>(8);
            int x = i % width;
            int y = i / width;
            for (int dy = -1; dy <= 1; dy++)
            {
                for (int dx = -1; dx <= 1; dx++)
                {
                    int nx = x + dx, ny = y + dy;
                    if ((dx != 0 || dy != 0) && nx >= 0 && ny >= 0 && nx < width && ny < height && line[(ny * width) + nx]
                        && linked.Touch(i, (ny * width) + nx))
                    {
                        found.Add((ny * width) + nx);
                    }
                }
            }

            return found;
        }

        bool AnyWalked(int i)
        {
            foreach (int neighbour in NeighboursOf(i))
            {
                if (walked.Contains(Edge(i, neighbour)))
                {
                    return true;
                }
            }

            return false;
        }

        static (int, int) Edge(int a, int b) => a < b ? (a, b) : (b, a);
    }

    /// <summary>
    /// One pass of corner cutting: each segment's quarter and three-quarter points; an open lane keeps its ends; ⛔ and a
    /// corner whose cut would cross a building is kept, not cut (D582).
    /// </summary>
    private static (Vector2[], byte[]) Chaikin(Vector2[] points, byte[] grades, bool loop, Func<Vector2, Vector2, bool> crosses)
    {
        var outPoints = new List<Vector2>(points.Length * 2);
        var outGrades = new List<byte>(points.Length * 2);
        int segments = loop ? points.Length : points.Length - 1;
        if (!loop)
        {
            outPoints.Add(points[0]);
            outGrades.Add(grades[0]);
        }

        for (int s = 0; s < segments; s++)
        {
            Vector2 a = points[s];
            Vector2 b = points[(s + 1) % points.Length];
            byte g = Math.Min(grades[s], grades[(s + 1) % points.Length]);
            outPoints.Add(a.Lerp(b, 0.25f));
            outGrades.Add(g);
            outPoints.Add(a.Lerp(b, 0.75f));
            outGrades.Add(g);

            // The corner at b, cut from here to the next segment's quarter point — unless that cut crosses a building.
            bool cornerAhead = loop || s + 1 < segments;
            if (cornerAhead)
            {
                Vector2 c = points[(s + 2) % points.Length];
                if (crosses(a.Lerp(b, 0.75f), b.Lerp(c, 0.25f)))
                {
                    outPoints.Add(b);
                    outGrades.Add(grades[(s + 1) % points.Length]);
                }
            }
        }

        if (!loop)
        {
            outPoints.Add(points[^1]);
            outGrades.Add(grades[^1]);
        }

        return (outPoints.ToArray(), outGrades.ToArray());
    }
}

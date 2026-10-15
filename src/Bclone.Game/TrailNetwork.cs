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
/// </para>
/// </remarks>
internal sealed class TrailNetwork
{
    /// <summary>One lane: its points in tile coordinates, a grade per point, and how its ends finish.</summary>
    internal sealed record Lane(Vector2[] Points, byte[] Grades, bool Closed, bool StartsAtADeadEnd, bool EndsAtADeadEnd);

    /// <summary>Smoothing passes over each lane (Chaikin): enough to round a corner, few enough to keep its place.</summary>
    private const int SmoothingPasses = 2;

    private TrailNetwork(List<Lane> lanes, List<(Vector2 At, byte Grade)> junctions, int bridged)
    {
        Lanes = lanes;
        Junctions = junctions;
        BridgedTiles = bridged;
    }

    /// <summary>The lanes, in the order traced.</summary>
    public IReadOnlyList<Lane> Lanes { get; }

    /// <summary>Where three or more lanes meet, and the best grade among them.</summary>
    public IReadOnlyList<(Vector2 At, byte Grade)> Junctions { get; }

    /// <summary>How many one-tile gaps were bridged — for the probe.</summary>
    public int BridgedTiles { get; }

    /// <summary>A network with nothing in it.</summary>
    public static TrailNetwork Empty { get; } = new(new List<Lane>(), new List<(Vector2, byte)>(), 0);

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
    public static TrailNetwork Build(int originX, int originY, int width, int height, byte[] grade, bool[] blocked, Func<Vector2, Vector2>? wander)
    {
        int n = width * height;
        if (grade.Length != n || blocked.Length != n)
        {
            throw new ArgumentException($"A {width}×{height} window wants {n} grades and {n} blocked flags, not {grade.Length} and {blocked.Length}.");
        }

        // 1. The shape: worn ground, and every one-tile gap with worn ground facing across it — measured on the worn
        // ground alone, so a bridged tile never bridges the next (a two-tile gap is two lanes).
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
        bool[] line = Thin(shape, width, height);

        // 3. A staircase on the line keeps a pixel at each corner it does not need; drop them, so a diagonal is a
        // diagonal and not a run of tiny junctions.
        Dictionary<(int, int), int> wraps = DropCornerPixels(line, blocked, width, height);

        // 4. Trace, prune one-pixel spurs, place and smooth.
        return Trace(line, shape, blocked, wraps, originX, originY, width, height, wander, bridged);

        byte Facing(int x, int y, int dx, int dy)
        {
            byte a = GradeAt(x - dx, y - dy);
            byte b = GradeAt(x + dx, y + dy);
            return a > 0 && b > 0 ? Math.Min(a, b) : (byte)0;
        }

        byte GradeAt(int x, int y) => x < 0 || y < 0 || x >= width || y >= height ? (byte)0 : grade[(y * width) + x];
    }

    /// <summary>Zhang–Suen thinning of the shape; a piece it would erase whole keeps the pixel nearest its middle.</summary>
    private static bool[] Thin(byte[] shape, int width, int height)
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

                    // P2..P9 clockwise from north (screen y runs down).
                    bool p2 = At(x, y - 1), p3 = At(x + 1, y - 1), p4 = At(x + 1, y), p5 = At(x + 1, y + 1);
                    bool p6 = At(x, y + 1), p7 = At(x - 1, y + 1), p8 = At(x - 1, y), p9 = At(x - 1, y - 1);
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
                        if (x >= 0 && y >= 0 && x < width && y < height && shape[(y * width) + x] > 0 && !seen[(y * width) + x])
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

        bool At(int x, int y) => x >= 0 && y >= 0 && x < width && y < height && on[(y * width) + x];

        static int Rise(bool from, bool to) => !from && to ? 1 : 0;
    }

    /// <summary>
    /// A pixel whose only two neighbours already touch each other joins nothing — a staircase's corner, or a nub — and goes.
    /// ⛔ <b>But a dropped corner that the line between its neighbours would cut a building's corner without is
    /// remembered</b>, by that pair (D582): the line's shape loses it, the drawing goes round it. Kept as a pixel, it
    /// gave every pixel of a ring three neighbours and the whole ring became one junction at the building's middle.
    /// </summary>
    private static Dictionary<(int, int), int> DropCornerPixels(bool[] line, bool[] blocked, int width, int height)
    {
        var wraps = new Dictionary<(int, int), int>();
        for (int i = 0; i < line.Length; i++)
        {
            if (!line[i] || Degree(line, i, width, height) != 2)
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
                    if ((dx != 0 || dy != 0) && nx >= 0 && ny >= 0 && nx < width && ny < height && line[(ny * width) + nx])
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

            int fx = first % width, fy = first / width, sx = second % width, sy = second / width;
            if (Math.Abs(fx - sx) > 1 || Math.Abs(fy - sy) > 1)
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

    private static int Degree(bool[] line, int i, int width, int height)
    {
        int x = i % width;
        int y = i / width;
        int degree = 0;
        for (int dy = -1; dy <= 1; dy++)
        {
            for (int dx = -1; dx <= 1; dx++)
            {
                int nx = x + dx, ny = y + dy;
                if ((dx != 0 || dy != 0) && nx >= 0 && ny >= 0 && nx < width && ny < height && line[(ny * width) + nx])
                {
                    degree++;
                }
            }
        }

        return degree;
    }

    /// <summary>The line traced into lanes between its ends and junctions, every point placed and smoothed.</summary>
    private static TrailNetwork Trace(bool[] line, byte[] shape, bool[] blocked, Dictionary<(int, int), int> wraps, int originX, int originY, int width, int height, Func<Vector2, Vector2>? wander, int bridged)
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

        var hubPoints = new Dictionary<int, Vector2>();
        var lanes = new List<Lane>();
        for (int p = 0; p < paths.Count; p++)
        {
            List<int> path = paths[p];
            lanes.AddRange(Placed(path, closed[p], !closed[p] && path.Count > 1 && End(path[0]) == 1, !closed[p] && path.Count > 1 && End(path[^1]) == 1));
        }

        var junctions = new List<(Vector2, byte)>();
        for (int i = 0; i < n; i++)
        {
            if (hub[i] == i && ends[i] >= 3)
            {
                byte best = 0;
                for (int k = 0; k < n; k++)
                {
                    best = hub[k] == i ? Math.Max(best, shape[k]) : best;
                }

                junctions.Add((HubPoint(i), best));
            }
        }

        return new TrailNetwork(lanes, junctions, bridged);

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
                if (!line[i] || hub[i] >= 0 || Degree(line, i, width, height) < 3)
                {
                    continue;
                }

                hub[i] = i;
                clump.Enqueue(i);
                while (clump.Count > 0)
                {
                    foreach (int other in NeighboursOf(clump.Dequeue()))
                    {
                        if (hub[other] < 0 && Degree(line, other, width, height) >= 3)
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

                int degree = Degree(line, i, width, height);
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
                        if (Degree(line, here, width, height) != 2)
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
                if (!line[i] || Degree(line, i, width, height) != 2 || AnyWalked(i))
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
                bool atAHub = (k == 0 || k == path.Count - 1) && hub[path[k]] >= 0;
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

            Vector2[] points = placed.ToArray();
            byte[] grades = graded.ToArray();

            for (int pass = 0; pass < SmoothingPasses && points.Length > 2; pass++)
            {
                (points, grades) = Chaikin(points, grades, loop, Crosses);
            }

            return CutAtBuildings(points, grades, loop, startsDead, endsDead);
        }

        // ⛔ THE INVARIANT, LAST (D582): a step that would still cross a building — a lane leaving a junction that rings a
        // one-tile store, say — is not drawn. The lane stops at the wall on each side, which is what a path to a door
        // looks like; a line over a building is a path nobody could walk.
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
                    if (nx >= 0 && ny >= 0 && nx < width && ny < height && shape[(ny * width) + nx] > 0)
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
                    if (nx >= 0 && ny >= 0 && nx < width && ny < height && shape[(ny * width) + nx] > 0)
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

        // Whether the line from a to b passes over a building — sampled at quarter steps, as the probe asks.
        bool Crosses(Vector2 a, Vector2 b)
        {
            for (int q = 0; q <= 4; q++)
            {
                if (OnABuilding(a.Lerp(b, q / 4f)))
                {
                    return true;
                }
            }

            return false;
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
                    if ((dx != 0 || dy != 0) && nx >= 0 && ny >= 0 && nx < width && ny < height && line[(ny * width) + nx])
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

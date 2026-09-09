using Godot;

namespace Bclone.Game;

/// <summary>
/// ⭐⭐ The border of a painted region, as a smooth closed shape rather than a staircase (D332).
/// </summary>
/// <remarks>
/// <para>
/// <b>Joe, playing the free-placement build:</b> *"if the game is gridless, then why is everything
/// still in a grid? why isnt the paint brush a smooth circle?"* ⛔ **Two different things were being
/// called the grid.** The sim being tile-indexed is his own closed decision (`gridless.md §10.2` —
/// terrain and spatial partitioning stay gridded, *"not to be re-litigated"*), and the brush paints
/// tiles because zones are three tile layers. **What nobody had ever revisited is that the tiles are
/// DRAWN as tiles**: three per-tile rect passes with hard staircase borders.
/// </para>
/// <para>
/// ⭐ <b>Nothing here changes what is painted.</b> The zone is the same set of tiles, the brush lays
/// down the same ground, and no sim state moves. This is a picture of a set of tiles — which is why
/// it lives in the view and why no golden can move for it.
/// </para>
/// <para>
/// <b>⛔ THE THREE STEPS, AND THE MIDDLE ONE IS THE ONE THAT MAKES IT LOOK RIGHT.</b>
/// <list type="number">
/// <item><b>Trace</b> — every edge of a painted tile whose neighbour is unpainted is a boundary
/// segment; chain them into closed loops.</item>
/// <item><b>Straighten</b> — merge runs of segments that point the same way. **Without this the
/// corner-cutting rounds every half-tile step and a straight fence comes out wobbly**; with it, long
/// runs stay straight and only the corners soften, which is what "smooth" should mean here.</item>
/// <item><b>Round</b> — Chaikin corner-cutting, twice.</item>
/// </list>
/// </para>
/// <para>
/// ⚠️ <b>Worked in DOUBLED tile coordinates so every corner is an integer.</b> A tile's corners sit
/// at half-tiles, and chaining segments means comparing endpoints for equality — which is exactly
/// the thing not to do in floating point. *Halve once, at the end, when it becomes a picture.*
/// </para>
/// </remarks>
internal static class ZoneOutline
{
    /// <summary>
    /// ⭐⭐ Trace a few known shapes and say whether they came out closed — <b>for the probe</b>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// ⛔ <b>This lives here because the view has no test project at all</b> (D11, D160) and this is
    /// pure geometry that can be wrong in ways no screenshot would show — a loop that did not close,
    /// a hole traced as an outer edge, a diagonal pinch that spun the walk. **The probe is the only
    /// instrument the view has**, so the check goes where the instrument can reach it.
    /// </para>
    /// <para>
    /// ⚠️ It asserts the <em>shape of the answer</em>, not its coordinates: how many loops, and that
    /// each one returns to where it started. *Pinning the points would pin the corner-cutting
    /// constant rather than the tracer.*
    /// </para>
    /// </remarks>
    internal static string SelfCheck()
    {
        var complaints = new List<string>();
        string sizes = string.Empty;

        CheckTiles("one tile", new[] { (0, 0) }, 1);
        CheckTiles("a 3x3 block", Block(3, 3), 1);
        CheckTiles("an L", new[] { (0, 0), (1, 0), (0, 1), (0, 2) }, 1);
        CheckTiles("two apart", new[] { (0, 0), (5, 5) }, 2);

        // ⚠️ A RING HAS TWO BORDERS — the outside and the hole. A tracer that returned one has
        // silently swallowed the hole, which is the failure that looks most like success.
        var ring = new HashSet<Vector2I>();
        foreach ((int x, int y) in Block(3, 3))
        {
            if (x != 1 || y != 1)
            {
                ring.Add(new Vector2I(x, y));
            }
        }

        Check("a ring round a hole", ring, 2);

        // ⚠️ A diagonal pinch: two tiles meeting at one corner. Either way of resolving it is a
        // valid picture; what must not happen is a walk that never terminates.
        CheckTiles("a diagonal pinch", new[] { (0, 0), (1, 1) }, 2);

        // ⛔⛔ AND THE SHAPE SURVIVES THE SMOOTHING, WHICH IS THE CHECK THAT WAS MISSING (D333).
        // Closure said the tracer worked; nothing said the ROUNDING left a square square. It did
        // not: proportional corner-cutting turned a 5×5 block into a sixteen-sided figure of about
        // 20 square tiles, and Joe read it off the screen as *"that 'square' brush is the round
        // brush"*. **Area is what tells them apart** — a circle inscribed in a 5×5 square is 19.6
        // against 25, a fifth of the shape gone.
        Area("a 5x5 square", Block(5, 5), 25f);
        Area("a radius-3 round", RoundBrush(3), 37f);

        return complaints.Count == 0
            ? $"[widths] zone outlines: ✅ every shape closed and kept its area{sizes}"
            : "[widths] zone outlines: ⛔ " + string.Join("; ", complaints);

        void Area(string what, IEnumerable<(int X, int Y)> tiles, float wanted)
        {
            var set = new HashSet<Vector2I>();
            foreach ((int x, int y) in tiles)
            {
                set.Add(new Vector2I(x, y));
            }

            List<Vector2[]> loops = Trace(set);
            if (loops.Count != 1)
            {
                complaints.Add($"{what}: {loops.Count} loops, wanted 1");
                return;
            }

            float area = Mathf.Abs(SignedArea(loops[0]));
            sizes += $" · {what} {area:F1} of {wanted:F0}";

            // ⚠️ A tenth, not an exact match: rounding the corners is SUPPOSED to remove a little.
            // What it must not do is remove a fifth, which is what a circle costs a square.
            if (Mathf.Abs(area - wanted) > wanted * 0.1f)
            {
                complaints.Add($"{what}: area {area:F1}, wanted about {wanted:F0}");
            }
        }

        static IEnumerable<(int, int)> RoundBrush(int radius)
        {
            long limit = (long)radius * (radius + 1);
            for (int dy = -radius; dy <= radius; dy++)
            {
                for (int dx = -radius; dx <= radius; dx++)
                {
                    if (((long)dx * dx) + ((long)dy * dy) <= limit)
                    {
                        yield return (dx, dy);
                    }
                }
            }
        }

        static IEnumerable<(int, int)> Block(int wide, int tall)
        {
            for (int y = 0; y < tall; y++)
            {
                for (int x = 0; x < wide; x++)
                {
                    yield return (x, y);
                }
            }
        }

        void CheckTiles(string what, IEnumerable<(int X, int Y)> tiles, int expected)
        {
            var set = new HashSet<Vector2I>();
            foreach ((int x, int y) in tiles)
            {
                set.Add(new Vector2I(x, y));
            }

            Check(what, set, expected);
        }

        void Check(string what, HashSet<Vector2I> tiles, int expected)
        {
            List<Vector2[]> loops = Trace(tiles);

            if (loops.Count != expected)
            {
                complaints.Add($"{what}: {loops.Count} loops, wanted {expected}");
                return;
            }

            for (int i = 0; i < loops.Count; i++)
            {
                Vector2[] loop = loops[i];
                if (loop.Length < 4 || loop[0].DistanceTo(loop[^1]) > 0.001f)
                {
                    complaints.Add($"{what}: loop {i} did not close");
                }
            }
        }
    }

    /// <summary>Twice the area of a closed loop, signed — the shoelace sum.</summary>
    private static float SignedArea(Vector2[] loop)
    {
        float twice = 0f;
        for (int i = 0; i < loop.Length - 1; i++)
        {
            twice += (loop[i].X * loop[i + 1].Y) - (loop[i + 1].X * loop[i].Y);
        }

        return twice / 2f;
    }

    /// <summary>How many times to cut the corners.</summary>
    private const int Roundings = 2;

    /// <summary>
    /// ⛔⛔ How much of a corner to cut, <b>in tiles</b> — and this number is why a square looked
    /// like a circle (D333).
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Joe:</b> *"that 'square' brush is the round brush and the round brush looks like weird
    /// diamond."* **He was reading the shapes correctly and they were both wrong.** Plain Chaikin
    /// cuts each corner at a QUARTER OF THE SIDE — so a 5×5 square, which straightens to exactly
    /// **four** vertices, becomes an octagon after one pass and a sixteen-sided figure after two.
    /// **A square with four corners, rounded proportionally, IS a circle.** And a stepped disc,
    /// whose sides are one tile each, had its every step cut a quarter of a tile from both ends
    /// until the whole thing pulled inward into a blobby cross.
    /// </para>
    /// <para>
    /// ⭐ <b>The cut is capped by an absolute distance instead.</b> A long fence loses a third of a
    /// tile at each corner and stays a fence; a one-tile step is softened and stays a step. *The
    /// shape survives the smoothing, which is the whole point of smoothing it.*
    /// </para>
    /// <para>
    /// ⚠️ <b>A proportional rule cannot tell a big shape from a small one</b>, and that is the
    /// general form of this bug: the same fraction is a rounded corner on a hundred-tile zone and a
    /// total rewrite of a five-tile one.
    /// </para>
    /// </remarks>
    private const float CornerCutTiles = 0.35f;

    /// <summary>
    /// The smoothed closed loops around a set of tiles, in tile coordinates.
    /// </summary>
    /// <remarks>
    /// ⚠️ <b>Deterministic in its output order</b> even though it uses a dictionary: the walk is
    /// driven by a <em>list</em> of segments in insertion order and the dictionary is only ever
    /// asked "which segments start here?". *Iteration order of a hash table is not a thing to draw
    /// from, for the same reason the sim bans it outright.*
    /// </remarks>
    internal static List<Vector2[]> Trace(HashSet<Vector2I> tiles)
    {
        var loops = new List<Vector2[]>();
        if (tiles.Count == 0)
        {
            return loops;
        }

        List<(Vector2I From, Vector2I To)> edges = BoundaryEdges(tiles);
        foreach (List<Vector2I> loop in ChainIntoLoops(edges))
        {
            List<Vector2> straightened = Straighten(loop);
            if (straightened.Count < 3)
            {
                continue;
            }

            loops.Add(Round(straightened));
        }

        return loops;
    }

    /// <summary>
    /// Every tile edge with unpainted ground on the far side, wound the same way round.
    /// </summary>
    /// <remarks>
    /// ⭐ <b>Consistent winding is what makes chaining possible at all</b> — each segment's end is
    /// the next one's start, so the walk never has to ask which direction it is going. Clockwise on
    /// screen, with the painted ground on the left.
    /// </remarks>
    private static List<(Vector2I From, Vector2I To)> BoundaryEdges(HashSet<Vector2I> tiles)
    {
        var edges = new List<(Vector2I, Vector2I)>();

        foreach (Vector2I tile in tiles)
        {
            int left = (tile.X * 2) - 1;
            int right = (tile.X * 2) + 1;
            int top = (tile.Y * 2) - 1;
            int bottom = (tile.Y * 2) + 1;

            if (!tiles.Contains(new Vector2I(tile.X, tile.Y - 1)))
            {
                edges.Add((new Vector2I(left, top), new Vector2I(right, top)));
            }

            if (!tiles.Contains(new Vector2I(tile.X + 1, tile.Y)))
            {
                edges.Add((new Vector2I(right, top), new Vector2I(right, bottom)));
            }

            if (!tiles.Contains(new Vector2I(tile.X, tile.Y + 1)))
            {
                edges.Add((new Vector2I(right, bottom), new Vector2I(left, bottom)));
            }

            if (!tiles.Contains(new Vector2I(tile.X - 1, tile.Y)))
            {
                edges.Add((new Vector2I(left, bottom), new Vector2I(left, top)));
            }
        }

        return edges;
    }

    /// <summary>
    /// Follow the segments end to start until each loop closes.
    /// </summary>
    /// <remarks>
    /// ⚠️ <b>A point can start two segments</b> where a region pinches to a diagonal touch. Either
    /// choice closes a valid loop and the two differ only in how the pinch is drawn, so the walk
    /// takes whichever is still unused — recorded because it looks like an oversight and is not.
    /// </remarks>
    private static List<List<Vector2I>> ChainIntoLoops(List<(Vector2I From, Vector2I To)> edges)
    {
        var startingAt = new Dictionary<Vector2I, List<int>>();
        for (int i = 0; i < edges.Count; i++)
        {
            if (!startingAt.TryGetValue(edges[i].From, out List<int>? here))
            {
                here = new List<int>();
                startingAt[edges[i].From] = here;
            }

            here.Add(i);
        }

        var used = new bool[edges.Count];
        var loops = new List<List<Vector2I>>();

        for (int i = 0; i < edges.Count; i++)
        {
            if (used[i])
            {
                continue;
            }

            var loop = new List<Vector2I> { edges[i].From };
            int at = i;

            // Bounded by the segment count: every step consumes one, so it cannot spin.
            while (at >= 0 && !used[at])
            {
                used[at] = true;
                loop.Add(edges[at].To);
                at = NextFrom(edges[at].To, startingAt, used);
            }

            // A loop that closed on itself; the repeated last point is dropped.
            if (loop.Count > 3)
            {
                loop.RemoveAt(loop.Count - 1);
                loops.Add(loop);
            }
        }

        return loops;
    }

    private static int NextFrom(
        Vector2I point, Dictionary<Vector2I, List<int>> startingAt, bool[] used)
    {
        if (!startingAt.TryGetValue(point, out List<int>? here))
        {
            return -1;
        }

        for (int i = 0; i < here.Count; i++)
        {
            if (!used[here[i]])
            {
                return here[i];
            }
        }

        return -1;
    }

    /// <summary>
    /// Merge runs that point the same way, and halve back into tile coordinates.
    /// </summary>
    /// <remarks>
    /// ⛔ <b>This is the step that decides whether the result looks smooth or looks shaky.</b>
    /// Corner-cutting a polygon whose every side is half a tile long rounds the staircase into a
    /// ripple; corner-cutting one whose sides are the real straight runs rounds only the corners.
    /// </remarks>
    private static List<Vector2> Straighten(List<Vector2I> loop)
    {
        var kept = new List<Vector2>();

        for (int i = 0; i < loop.Count; i++)
        {
            Vector2I before = loop[(i - 1 + loop.Count) % loop.Count];
            Vector2I here = loop[i];
            Vector2I after = loop[(i + 1) % loop.Count];

            Vector2I incoming = here - before;
            Vector2I outgoing = after - here;

            if (incoming != outgoing)
            {
                kept.Add(new Vector2(here.X / 2f, here.Y / 2f));
            }
        }

        return kept;
    }

    /// <summary>
    /// Chaikin corner-cutting on a closed loop — each pass replaces a corner with two points a
    /// quarter of the way along each of its sides.
    /// </summary>
    private static Vector2[] Round(List<Vector2> loop)
    {
        List<Vector2> points = loop;

        for (int pass = 0; pass < Roundings; pass++)
        {
            var cut = new List<Vector2>(points.Count * 2);

            for (int i = 0; i < points.Count; i++)
            {
                Vector2 here = points[i];
                Vector2 next = points[(i + 1) % points.Count];

                // ⛔ A quarter of the side, or the cap — whichever is SMALLER. Plain Chaikin takes
                // the quarter unconditionally, which is what turned a four-cornered square into a
                // circle. The quarter is still the ceiling, so a side shorter than twice the cap
                // never has its two cuts cross.
                float side = here.DistanceTo(next);
                float share = side <= 0.0001f
                    ? 0.25f
                    : Mathf.Min(0.25f, CornerCutTiles / side);

                cut.Add(here.Lerp(next, share));
                cut.Add(here.Lerp(next, 1f - share));
            }

            points = cut;
        }

        // Closed: the first point repeated, so a polyline draws the last side too.
        var closed = new Vector2[points.Count + 1];
        points.CopyTo(closed);
        closed[^1] = points[0];
        return closed;
    }
}

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
        // ⚠️ Stated here rather than reaching into `SubTile`, so the tracer keeps
        // knowing nothing about what it is tracing.
        const int SubTilesPerTile = 4;

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
        // ⛔⛔ THE SQUARE IS EXACT, NOT APPROXIMATE, AND THAT IS THE WHOLE OF D334. Its four corners
        // are each between two five-tile runs, so all four are left alone and **not one square tile
        // of it is lost.** A tolerance here would let the corners creep back.
        // ⚠️ HALF A PERCENT, AND THE FIRST TRY AT 2% SCORED ZERO ON ITS RED CHECK. Disabling the
        // sharp-corner rule costs the square exactly 0.5 of its 25 — which is 2% on the nose, so a
        // 2% band let the bug through with nothing to say. **The square is exactly 25 by
        // construction when its corners are kept**, so the only tolerance it needs is float noise.
        Area("a 5x5 square", Block(5, 5), 25f, within: 0.005f);

        // ⚠️ The disc is allowed to lose a little: every one of its corners IS a staircase step,
        // which is the thing rounding exists for.
        Area("a radius-3 round", RoundBrush(3), 37f, within: 0.1f);

        // ⭐⭐ AND THE THING THE WHOLE SLICE WAS FOR: a small round brush is now ACTUALLY ROUND
        // (D336). At five tiles across in whole tiles a disc has no choice but to be a bitten
        // square — 21 of 25, **84% of its box**. In quarter-tiles the same brush is twenty cells
        // wide and fills **79%**, which is π/4. *That five points is the difference between "a
        // circle" and "haha this is a circle????"*
        Roundness("a 5-tile round brush, in quarter-tiles", 10, 0.79f);

        // ⛔⛔ AND WHETHER THE OUTLINE IS ACTUALLY SMOOTH, WHICH NOTHING HERE ASKED
        // (D343). Every check above is about **area**, and area was green for the whole time the
        // smoothing was being applied at a quarter of its stated strength — a staircase
        // encloses the same area as the curve through it. Joe read the difference straight off
        // the screen: *"the selected area for harvest still looks jagged/square."*
        // ⭐ **Perimeter is what tells them apart**, scale-free: a shape's perimeter over
        // that of the circle with its area. A disc drawn in cells and left stepped comes to about
        // 1.27 — the bounding square's perimeter over the circle's — and a properly
        // smoothed one approaches 1.
        // ⚠️ MEASURED IN BOTH STATES BEFORE THE BAND WAS CHOSEN, because the
        // first threshold was picked by eye at 1.10 and **the bug measures 1.07, so it scored
        // zero.** *That is D334's trap exactly — a tolerance chosen by eye can be wider than
        // the defect it is watching for.* Smoothed: 1.00. Applied in cells, as Joe photographed
        // it: 1.06–1.07. The band sits between them.
        Smoothness("a 10-tile round", RoundBrush(20), SubTilesPerTile, 1.03f);
        Smoothness("a 5-tile round", RoundBrush(10), SubTilesPerTile, 1.03f);

        // ⭐⭐ THE SQUARE IS THE CONTROL, AND IT IS WHY THIS METRIC CAN BE TRUSTED. A
        // square's perimeter is 4/√π ≈ 1.13 of its circle's, and D334 says its
        // four corners are the player's and must be **left exactly alone** — so it measures
        // 1.13 whether the smoothing is right or wrong. **A guard that only ever said "smoother
        // is better" would happily pass a rounding rule that dissolved every square in the
        // game.** Two-sided, so it catches that too.
        Smoothness("a 6-tile square", Block(24, 24), SubTilesPerTile, 1.16f, least: 1.10f);

        // ⛔⛔ AND WHETHER THE CURVE HAS CORNERS, WHICH PERIMETER CANNOT SEE EITHER
        // (D345). A twenty-sided polygon is 1.004 of a circle's perimeter and looks like a
        // twenty-sided polygon — Joe: *"look at how jagged/square the brush is."* **The
        // sharpest turn between consecutive segments is what a facet IS**, so that is what this
        // measures. The square stays as the control the other way: its corners must remain
        // corners.
        // ⚠️ MEASURED BEFORE THE BAND WAS SET, on both sides of the fix: the single-stage
        // smoothing read 18° (Joe's twenty-sided brush); chords then three quarter passes
        // read 4°. A whole-tile stroke is posed too, because its steps are four cells and the
        // chord stage behaves differently on it.
        Facets("a 10-tile round", RoundBrush(20), SubTilesPerTile, 8f);
        Facets("a 5-tile round", RoundBrush(10), SubTilesPerTile, 8f);
        // ⭐ The founding zone is the one whole-tile shape a player meets without painting
        // it: a Manhattan diamond of tiles, whose 45° edges are four-cell steps.
        Facets("the founding diamond, in whole tiles", WholeTiles(Diamond(6)), SubTilesPerTile, 10f);
        Facets("a 6-tile square", Block(24, 24), SubTilesPerTile, 999f, least: 80f);

        // ⛔⛔ AND THE FILL COVERS THE REGION AND NOTHING ELSE (D345). A square fills to
        // its own area; **a ring round a hole fills to the ring and not the hole** — which is
        // the case that made "fill the smoothed contour" a deferred problem in the first place.
        // The red check is the centroid filter switched off: the ring then fills its hole too.
        Filled("a 5x5 square", Block(5, 5), 25f, within: 0.02f);
        Filled("a ring round a hole", RingOfTiles(), 8f, within: 0.06f);

        // ⛔⛔ AND THE TRIANGLES DO NOT OVERLAP (D352). Joe: *"sometimes the paintbrush (all
        // types) has weird… triangle artifacting"* — a translucent fill drawn twice where two
        // triangles overlap is a darker facet, and once where a gap is left is a lighter one.
        // `Filled` above cannot see it: a double-covered patch beside a gap sums to the right area.
        // **The triangles' area against the loops' own area is what tells them apart** — a fill
        // with no overlap and no gap is the polygon, exactly.
        Tiled("a 5-tile round brush", RoundBrush(10), SubTilesPerTile);
        Tiled("a 7-tile round brush", RoundBrush(14), SubTilesPerTile);
        Tiled("a 10-tile round brush", RoundBrush(20), SubTilesPerTile);
        Tiled("a 6-tile square", Block(24, 24), SubTilesPerTile);
        Tiled("a 13-tile round brush", RoundBrush(26), SubTilesPerTile);

        return complaints.Count == 0
            ? $"[widths] zone outlines: ✅ every shape closed and kept its area{sizes}"
            : "[widths] zone outlines: ⛔ " + string.Join("; ", complaints);

        void Roundness(string what, int radius, float wanted)
        {
            int cells = 0;
            long limit = (long)radius * (radius + 1);
            for (int dy = -radius; dy <= radius; dy++)
            {
                for (int dx = -radius; dx <= radius; dx++)
                {
                    if (((long)dx * dx) + ((long)dy * dy) <= limit)
                    {
                        cells++;
                    }
                }
            }

            int box = ((radius * 2) + 1) * ((radius * 2) + 1);
            float fill = cells / (float)box;
            sizes += $" · {what} fills {fill * 100f:F0}% of its box";

            // ⚠️ A disc fills π/4 of its bounding square. Much above that is a square with its
            // corners nibbled — which is exactly what five WHOLE tiles gave, at 84%.
            if (fill > wanted + 0.04f)
            {
                complaints.Add($"{what}: fills {fill * 100f:F0}%, a disc fills {wanted * 100f:F0}%");
            }
        }

        void Tiled(string what, IEnumerable<(int X, int Y)> cells, int cellsPerTile)
        {
            var set = new HashSet<Vector2I>();
            foreach ((int x, int y) in cells)
            {
                set.Add(new Vector2I(x, y));
            }

            List<Vector2[]> loops = Trace(set, cellsPerTile);
            float polygon = 0f;
            foreach (Vector2[] loop in loops)
            {
                // Outer loops and holes wind opposite ways, so the signed sum is the region.
                polygon += SignedArea(loop);
            }

            polygon = Mathf.Abs(polygon);
            float triangles = TriangleArea(Fill(loops, set));
            float ratio = polygon > 0f ? triangles / polygon : 0f;
            sizes += $" · {what} is tiled to {ratio:F3} of its own area";

            // Half a percent: the centroid filter can drop a sliver at a concavity and that is
            // a gap of a triangle's width, not a facet.
            if (Mathf.Abs(ratio - 1f) > 0.005f)
            {
                complaints.Add(
                    $"{what}: the triangles cover {ratio:F3} of the polygon — "
                    + (ratio > 1f ? "overlapping, which draws darker facets" : "with gaps, which draw lighter ones"));
            }
        }

        void Filled(string what, IEnumerable<(int X, int Y)> tiles, float wanted, float within)
        {
            var set = new HashSet<Vector2I>();
            foreach ((int x, int y) in tiles)
            {
                set.Add(new Vector2I(x, y));
            }

            float area = TriangleArea(Fill(Trace(set), set));
            sizes += $" · {what} fills {area:F1} of {wanted:F0}";

            if (Mathf.Abs(area - wanted) > wanted * within)
            {
                complaints.Add(
                    $"{what}: the fill covers {area:F1}, wanted {wanted:F0} within "
                    + $"{within * 100f:F0}% — the triangles do not follow the paint");
            }
        }

        static IEnumerable<(int, int)> RingOfTiles()
        {
            foreach ((int x, int y) in Block(3, 3))
            {
                if (x != 1 || y != 1)
                {
                    yield return (x, y);
                }
            }
        }

        void Facets(
            string what,
            IEnumerable<(int X, int Y)> cells,
            int cellsPerTile,
            float worstDegrees,
            float least = 0f)
        {
            var set = new HashSet<Vector2I>();
            foreach ((int x, int y) in cells)
            {
                set.Add(new Vector2I(x, y));
            }

            List<Vector2[]> loops = Trace(set, cellsPerTile);
            if (loops.Count != 1)
            {
                complaints.Add($"{what}: {loops.Count} loops, wanted 1");
                return;
            }

            // ⛔⛔ DUPLICATES REMOVED BEFORE MEASURING, BECAUSE THE FIRST VERSION SKIPPED
            // EVERY CORNER THAT MATTERED. Two cuts meeting at a side's midpoint produce the same
            // point twice; skipping the zero-length segment ALSO skipped the turn on either side
            // of it — which is exactly where the chords met. **It reported 7° on a
            // polygon Joe could count the sides of.**
            var loop = new List<Vector2>();
            for (int i = 0; i + 1 < loops[0].Length; i++)
            {
                if (loop.Count == 0 || loop[^1].DistanceSquaredTo(loops[0][i]) > 1e-8f)
                {
                    loop.Add(loops[0][i]);
                }
            }

            int count = loop.Count;
            float sharpest = 0f;

            for (int i = 0; i < count; i++)
            {
                Vector2 incoming = loop[i] - loop[(i - 1 + count) % count];
                Vector2 outgoing = loop[(i + 1) % count] - loop[i];

                float turn = Mathf.RadToDeg(Mathf.Abs(incoming.AngleTo(outgoing)));
                sharpest = Mathf.Max(sharpest, turn);
            }

            sizes += $" · {what} turns at most {sharpest:F0}° over {count} points";

            if (sharpest > worstDegrees)
            {
                complaints.Add(
                    $"{what}: sharpest turn {sharpest:F0}°, wanted under {worstDegrees:F0}° "
                    + "— the border is a polygon with visible corners");
            }
            else if (sharpest < least)
            {
                complaints.Add(
                    $"{what}: sharpest turn only {sharpest:F0}°, wanted at least {least:F0}° "
                    + "— the corners the player painted have been rounded off (D334)");
            }
        }

        void Smoothness(
            string what,
            IEnumerable<(int X, int Y)> cells,
            int cellsPerTile,
            float worst,
            float least = 0f)
        {
            var set = new HashSet<Vector2I>();
            foreach ((int x, int y) in cells)
            {
                set.Add(new Vector2I(x, y));
            }

            List<Vector2[]> loops = Trace(set, cellsPerTile);
            if (loops.Count != 1)
            {
                complaints.Add($"{what}: {loops.Count} loops, wanted 1");
                return;
            }

            Vector2[] loop = loops[0];
            float perimeter = 0f;
            for (int i = 0; i + 1 < loop.Length; i++)
            {
                perimeter += loop[i].DistanceTo(loop[i + 1]);
            }

            float area = Mathf.Abs(SignedArea(loop));
            if (area <= 0f)
            {
                complaints.Add($"{what}: no area to measure");
                return;
            }

            // 1 for a circle; a stepped disc is about 1.27, which is 4/π ÷ (4/π).
            float ragged = perimeter / (2f * Mathf.Sqrt(Mathf.Pi * area));
            sizes += $" · {what} is {ragged:F2} of a circle's perimeter";

            if (ragged > worst)
            {
                complaints.Add(
                    $"{what}: perimeter is {ragged:F2}× a circle's, wanted under "
                    + $"{worst:F2} — the outline is still a staircase");
            }
            else if (ragged < least)
            {
                complaints.Add(
                    $"{what}: perimeter is only {ragged:F2}× a circle's, wanted at "
                    + $"least {least:F2} — the smoothing has rounded off corners the "
                    + "player painted (D334)");
            }
        }

        void Area(string what, IEnumerable<(int X, int Y)> tiles, float wanted, float within)
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

            if (Mathf.Abs(area - wanted) > wanted * within)
            {
                complaints.Add(
                    $"{what}: area {area:F1}, wanted {wanted:F0} within {within * 100f:F1}%");
            }
        }

        static IEnumerable<(int, int)> WholeTiles(IEnumerable<(int X, int Y)> tiles)
        {
            foreach ((int x, int y) in tiles)
            {
                for (int sy = 0; sy < SubTilesPerTile; sy++)
                {
                    for (int sx = 0; sx < SubTilesPerTile; sx++)
                    {
                        yield return ((x * SubTilesPerTile) + sx, (y * SubTilesPerTile) + sy);
                    }
                }
            }
        }

        static IEnumerable<(int, int)> Diamond(int radius)
        {
            for (int dy = -radius; dy <= radius; dy++)
            {
                for (int dx = -radius; dx <= radius; dx++)
                {
                    if (Math.Abs(dx) + Math.Abs(dy) <= radius)
                    {
                        yield return (dx, dy);
                    }
                }
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

    /// <summary>
    /// How many times the staircase is cut at HALF a step — <b>the stage that turns steps
    /// into chords</b> (D345).
    /// </summary>
    /// <remarks>
    /// ⛔⛔ <b>THIS STAGE MAKES CHORDS, NOT CURVES, AND FOR A WHOLE SLICE NOBODY NOTICED
    /// BECAUSE THE GUARD COULD NOT SEE IT.</b> Cutting a one-cell step at exactly its midpoint
    /// puts the cut on the midpoint of the step, and **the midpoints of a regular staircase are
    /// collinear** — so a digital circle, whose steps run 2:1 then 1:1 then 1:2 round each
    /// octant, comes out as about twenty straight chords meeting at corners of 18°. Joe:
    /// *"look at how jagged/square the brush is."* The perimeter guard read 1.00, because a
    /// chord polygon has a circle's perimeter; the turning-angle guard read 7°, because it
    /// skipped every corner that sat beside one of the duplicate midpoints. *Two instruments,
    /// both blind to the same thing, for different reasons.*
    /// </remarks>
    private const int Straightenings = 2;

    /// <summary>
    /// How many times the chords are then rounded at a QUARTER — <b>the stage that makes the
    /// curve</b> (D345).
    /// </summary>
    /// <remarks>
    /// ⭐ Classic Chaikin, which converges to a smooth spline of the chord polygon. It cannot
    /// be run on the staircase directly — measured: a quarter cut on one-cell steps only
    /// softens the wiggle, and the perimeter comes out 1.10 of a circle's however many passes
    /// are run — which is why the chord stage comes first. ⛔ **The sharp-corner rule
    /// still applies here**, so a square the player painted keeps its four corners through both
    /// stages.
    /// </remarks>
    private const int Roundings = 3;

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
    /// ⛔⛔ How long both sides of a corner must be for it to count as a <b>real corner</b> and be
    /// left sharp (D334).
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Joe:</b> *"the square brush looks okay — but why are the corners rounded? id prefer them
    /// square."* **He is right, and the distinction is one the smoothing was not making at all.**
    /// A border has two completely different kinds of corner in it, and rounding both is what made
    /// a square look apologetic:
    /// </para>
    /// <list type="bullet">
    /// <item><b>A real corner</b> — two long straight runs meeting at a right angle. **The player
    /// painted that**, and it should stay exactly as painted.</item>
    /// <item><b>A staircase step</b> — a one-tile jog where a straight line has been approximated by
    /// squares. **Nobody chose that**; it is an artefact of the ground being tiled, and it is the
    /// only thing worth softening.</item>
    /// </list>
    /// <para>
    /// ⭐ <b>The length of the two adjacent runs tells them apart</b>, and nothing else has to. A
    /// corner between two runs of at least this many tiles is deliberate and is left alone; anything
    /// shorter is a step and is rounded. *So a square stays square and a disc stops looking like a
    /// pile of bricks, from one rule.*
    /// </para>
    /// </remarks>
    private const float SharpCornerTiles = 2f;

    /// <summary>
    /// The smoothed closed loops around a set of tiles, in tile coordinates.
    /// </summary>
    /// <remarks>
    /// <para>
    /// ⚠️ <b>Deterministic in its output order</b> even though it uses a dictionary: the
    /// walk is driven by a <em>list</em> of segments in insertion order and the dictionary is only
    /// ever asked "which segments start here?". *Iteration order of a hash table is not a thing to
    /// draw from, for the same reason the sim bans it outright.*
    /// </para>
    /// <para>
    /// ⛔⛔ <b><paramref name="cellsPerTile"/> IS NOT OPTIONAL DECORATION — THE
    /// SMOOTHING IS STATED IN TILES AND THIS IS THE ONLY THING THAT KNOWS WHAT A TILE IS</b>
    /// (D343). The tracer works in whatever grid it is handed; <see cref="CornerCutTiles"/> and
    /// <see cref="SharpCornerTiles"/> are stated in tiles because that is what the design
    /// arguments for them are about. **Without this they were applied in CELLS**, and since D336
    /// a zone cell is a quarter-tile — so the corner cut was a quarter of its stated size and
    /// *every run longer than half a tile was being preserved as a corner the player had
    /// deliberately painted.* Joe: *"the selected area for harvest still looks jagged/square."*
    /// </para>
    /// </remarks>
    internal static List<Vector2[]> Trace(HashSet<Vector2I> tiles, int cellsPerTile = 1)
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

            loops.Add(Round(straightened, cellsPerTile));
        }

        return loops;
    }

    /// <summary>
    /// ⭐⭐ The inside of the traced loops as triangles — <b>so the fill follows the curve
    /// instead of the cells</b> (D345).
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Joe: *"why am i seeing that instead of smooth contoured lines?"*</b> The border was a
    /// curve and the fill under it was still one rectangle per quarter-tile, so every painted area
    /// had a staircase showing through its own smooth edge. *`the-valley-in-view.md §4` deferred
    /// "filling the smoothed contour" as a real problem — a concave polygon with holes.*
    /// </para>
    /// <para>
    /// ⭐⭐ <b>HOLES AND CONCAVITY COME FOR FREE, BY NOT TRIANGULATING THE POLYGON AT
    /// ALL.</b> Delaunay over every loop point covers the convex hull; **keeping only the triangles
    /// whose centroid lands on a painted cell** carves the hull back to the region — a ring
    /// round a hut loses the triangles inside the hole, a bay loses the ones across its mouth. No
    /// key-holing, no winding logic, and the same one rule for every shape. *The centroid test
    /// is one array read, and it asks the same cells the loops were traced from.*
    /// </para>
    /// <para>
    /// ⚠️ <b>A gap narrower than a triangle can keep a sliver of fill across it</b> —
    /// a Delaunay triangle spanning the gap has its centroid on painted ground either side. Named
    /// rather than fixed: the border still draws correctly there, and the cases are rare.
    /// </para>
    /// <para>
    /// ⚠️ <b>Computed when the paint moves, never per frame.</b> The caller caches the
    /// triangles beside the loops and transforms them to the screen each frame —
    /// `CLAUDE.md`'s standing rule.
    /// </para>
    /// </remarks>
    internal static Vector2[] Fill(List<Vector2[]> loops, HashSet<Vector2I> cells)
    {
        // ⛔⛔ EAR CLIPPING, NOT DELAUNAY (D352). D345 triangulated the loop points with
        // `Geometry2D.TriangulateDelaunay` and kept the triangles whose centroid sat on paint. Joe:
        // *"sometimes the paintbrush (all types) has weird… triangle artifacting."* Measured with
        // `Tiled` in the self-check: the Delaunay triangles cover **1.62× a 7-tile round's area
        // and 2.30× a 13-tile round's** — they overlap, and a translucent fill drawn twice is a
        // darker facet. A smoothed round is hundreds of points lying almost on one circle, the
        // one input Delaunay is not unique for, and nudging the points off it only moved which
        // brush sizes broke. **A polygon's own ear clipping cannot overlap and cannot gap**: it
        // is the polygon, exactly. Holes are bridged into their outer loop first — the classic
        // cut from the hole's rightmost point to a visible outer vertex — so a ring round a hut
        // is one weakly simple polygon and fills as a ring.
        var outers = new List<List<Vector2>>();
        var holes = new List<List<Vector2>>();
        float outerSign = 0f;
        float largest = 0f;

        foreach (Vector2[] loop in loops)
        {
            float signed = Mathf.Abs(SignedArea(loop));
            if (signed > largest)
            {
                largest = signed;
                outerSign = Mathf.Sign(SignedArea(loop));
            }
        }

        foreach (Vector2[] loop in loops)
        {
            List<Vector2> open = WithoutTheClosingRepeat(loop);
            if (open.Count < 3)
            {
                continue;
            }

            (Mathf.Sign(SignedArea(loop)) == outerSign ? outers : holes).Add(open);
        }

        var kept = new List<Vector2>();
        foreach (List<Vector2> outer in outers)
        {
            List<Vector2> polygon = outer;

            // Every hole inside this outer loop, rightmost first, bridged in one at a time.
            var inside = new List<List<Vector2>>();
            Vector2[] fence = outer.ToArray();
            foreach (List<Vector2> hole in holes)
            {
                if (Geometry2D.IsPointInPolygon(hole[0], fence))
                {
                    inside.Add(hole);
                }
            }

            inside.Sort((a, b) => Rightmost(b).X.CompareTo(Rightmost(a).X));
            foreach (List<Vector2> hole in inside)
            {
                polygon = Bridge(polygon, hole);
            }

            Vector2[] corners = polygon.ToArray();
            int[] indices = Geometry2D.TriangulatePolygon(corners);

            if (indices.Length == 0)
            {
                // ⚠️ The clipper refused it — a self-touching bridge it could not see past. The
                // old path is kept as the fallback so the region still fills, facets and all,
                // rather than vanishing; the self-check's `Tiled` line is what says how often.
                indices = Geometry2D.TriangulateDelaunay(corners);
                for (int t = 0; t + 2 < indices.Length; t += 3)
                {
                    Vector2 centroid = (corners[indices[t]] + corners[indices[t + 1]] + corners[indices[t + 2]]) / 3f;
                    var cell = new Vector2I(Mathf.RoundToInt(centroid.X), Mathf.RoundToInt(centroid.Y));
                    if (cells.Contains(cell))
                    {
                        kept.Add(corners[indices[t]]);
                        kept.Add(corners[indices[t + 1]]);
                        kept.Add(corners[indices[t + 2]]);
                    }
                }

                continue;
            }

            for (int t = 0; t + 2 < indices.Length; t += 3)
            {
                kept.Add(corners[indices[t]]);
                kept.Add(corners[indices[t + 1]]);
                kept.Add(corners[indices[t + 2]]);
            }
        }

        return kept.ToArray();
    }

    /// <summary>A loop's points without the repeated first point, and without coincident neighbours.</summary>
    private static List<Vector2> WithoutTheClosingRepeat(Vector2[] loop)
    {
        var points = new List<Vector2>(loop.Length);
        for (int i = 0; i + 1 < loop.Length; i++)
        {
            if (points.Count == 0 || points[^1].DistanceSquaredTo(loop[i]) > 1e-8f)
            {
                points.Add(loop[i]);
            }
        }

        if (points.Count > 1 && points[^1].DistanceSquaredTo(points[0]) <= 1e-8f)
        {
            points.RemoveAt(points.Count - 1);
        }

        return points;
    }

    private static Vector2 Rightmost(List<Vector2> loop)
    {
        Vector2 best = loop[0];
        foreach (Vector2 point in loop)
        {
            if (point.X > best.X)
            {
                best = point;
            }
        }

        return best;
    }

    /// <summary>
    /// Splice a hole into its outer polygon along a zero-width cut, so one ear-clipping pass
    /// fills the ring.
    /// </summary>
    /// <remarks>
    /// From the hole's rightmost vertex to the nearest outer vertex the cut can reach without
    /// crossing an edge of either — checked against every edge, because an outer loop can be as
    /// concave as the player paints it. ⚠️ If no vertex is visible (it should always be — the
    /// rightmost point of a hole can always see the outer boundary somewhere) the nearest one is
    /// taken anyway and the clipper's fallback in <see cref="Fill"/> catches the result.
    /// </remarks>
    private static List<Vector2> Bridge(List<Vector2> outer, List<Vector2> hole)
    {
        int start = 0;
        for (int i = 1; i < hole.Count; i++)
        {
            if (hole[i].X > hole[start].X)
            {
                start = i;
            }
        }

        Vector2 from = hole[start];

        int best = -1;
        float bestDistance = float.MaxValue;
        for (int i = 0; i < outer.Count; i++)
        {
            float distance = from.DistanceSquaredTo(outer[i]);
            if (distance >= bestDistance
                || Crosses(from, outer[i], outer, i)
                || Crosses(from, outer[i], hole, start))
            {
                continue;
            }

            best = i;
            bestDistance = distance;
        }

        if (best < 0)
        {
            for (int i = 0; i < outer.Count; i++)
            {
                float distance = from.DistanceSquaredTo(outer[i]);
                if (distance < bestDistance)
                {
                    best = i;
                    bestDistance = distance;
                }
            }
        }

        // outer[0..best], then the hole once round from its rightmost point and back to it,
        // then outer[best] again and the rest of the outer loop.
        var spliced = new List<Vector2>(outer.Count + hole.Count + 2);
        for (int i = 0; i <= best; i++)
        {
            spliced.Add(outer[i]);
        }

        for (int k = 0; k <= hole.Count; k++)
        {
            spliced.Add(hole[(start + k) % hole.Count]);
        }

        for (int i = best; i < outer.Count; i++)
        {
            spliced.Add(outer[i]);
        }

        return spliced;
    }

    /// <summary>
    /// Whether the cut from <paramref name="a"/> to <paramref name="b"/> crosses an edge of the
    /// loop, ignoring the two edges that meet at vertex <paramref name="at"/>.
    /// </summary>
    private static bool Crosses(Vector2 a, Vector2 b, List<Vector2> loop, int at)
    {
        for (int i = 0; i < loop.Count; i++)
        {
            int next = (i + 1) % loop.Count;
            if (i == at || next == at)
            {
                continue;
            }

            if (Geometry2D.SegmentIntersectsSegment(a, b, loop[i], loop[next]).VariantType != Variant.Type.Nil)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>The area the triangles cover, for the self-check.</summary>
    private static float TriangleArea(Vector2[] triangles)
    {
        float twice = 0f;
        for (int t = 0; t + 2 < triangles.Length; t += 3)
        {
            Vector2 a = triangles[t];
            Vector2 b = triangles[t + 1];
            Vector2 c = triangles[t + 2];
            twice += Mathf.Abs(((b.X - a.X) * (c.Y - a.Y)) - ((c.X - a.X) * (b.Y - a.Y)));
        }

        return twice / 2f;
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
    private static Vector2[] Round(List<Vector2> loop, int cellsPerTile)
    {
        // ⛔ The two rules below are stated in TILES and the loop is in CELLS (D343).
        float cornerCut = CornerCutTiles * cellsPerTile;
        float sharpCorner = SharpCornerTiles * cellsPerTile;

        List<Vector2> points = loop;

        // ---- Stage 1: steps into chords ----
        for (int pass = 0; pass < Straightenings; pass++)
        {
            points = Cut(points, sharpCorner, cornerCut, 2f);
        }

        // ⚠️ Two cuts meeting at a step's midpoint leave the same point twice; drawn,
        // that is a zero-length segment the polyline pays for, and measured, it hid every corner.
        points = WithoutRepeats(points);

        // ---- Stage 2: chords into a curve ----
        for (int pass = 0; pass < Roundings; pass++)
        {
            points = Cut(points, sharpCorner, float.PositiveInfinity, 4f);
        }

        // Closed: the first point repeated, so a polyline draws the last side too.
        var closed = new Vector2[points.Count + 1];
        points.CopyTo(closed);
        closed[^1] = points[0];
        return closed;
    }

    /// <summary>Consecutive duplicates dropped; the loop's shape is unchanged.</summary>
    private static List<Vector2> WithoutRepeats(List<Vector2> points)
    {
        var kept = new List<Vector2>(points.Count);
        for (int i = 0; i < points.Count; i++)
        {
            if (kept.Count == 0 || kept[^1].DistanceSquaredTo(points[i]) > 1e-8f)
            {
                kept.Add(points[i]);
            }
        }

        if (kept.Count > 1 && kept[0].DistanceSquaredTo(kept[^1]) <= 1e-8f)
        {
            kept.RemoveAt(kept.Count - 1);
        }

        return kept;
    }

    /// <summary>
    /// One corner-cutting pass: every corner between two runs shorter than
    /// <paramref name="sharpCorner"/> is replaced by two points, each at most
    /// <paramref name="cap"/> and at most a <paramref name="fraction"/> of its run from the corner.
    /// </summary>
    private static List<Vector2> Cut(
        List<Vector2> points, float sharpCorner, float cap, float fraction)
    {
        {
            var cut = new List<Vector2>(points.Count * 2);

            // ⛔⛔ WALKS THE CORNERS, NOT THE SIDES, BECAUSE THE DECISION IS PER CORNER (D334).
            // Chaikin cuts every side and therefore every corner; what is wanted is to cut the
            // corners that are STAIRCASE STEPS and leave the ones the player actually painted.
            for (int i = 0; i < points.Count; i++)
            {
                Vector2 before = points[(i - 1 + points.Count) % points.Count];
                Vector2 here = points[i];
                Vector2 next = points[(i + 1) % points.Count];

                float back = before.DistanceTo(here);
                float forward = here.DistanceTo(next);

                // Two long runs meeting: a corner somebody chose. Kept exactly.
                if (back >= sharpCorner && forward >= sharpCorner)
                {
                    cut.Add(here);
                    continue;
                }

                // ⚠️ Never more than HALF a run, or the cuts from the two ends of a short side
                // cross each other and the outline turns inside out.
                float cutBack = Mathf.Min(cap, back / fraction);
                float cutOn = Mathf.Min(cap, forward / fraction);

                cut.Add(here + ((before - here).Normalized() * cutBack));
                cut.Add(here + ((next - here).Normalized() * cutOn));
            }

            return cut;
        }
    }
}

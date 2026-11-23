using Godot;

namespace Kith.Game;

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

        // ⛔⛔ AND A STAGE CLIPPED TO ITS CELLS IS THE FIELD WHERE IT TOUCHES THE FENCE (D362).
        // Clipped to every cell, the fill is ITSELF — the first draft lost 1% to the curve's bulge
        // past its own cells, which would have been a sliver of bare earth along every fence;
        // clipped to the lower half of the cells it is about half, and nothing of it lies inside a
        // cell it was not kept for. The red check is the clip switched off: the "half" comes back whole.
        // ⛔ And nothing of the half bulges into the void beside the OTHER half's rim (D366, Joe's
        // yellow edges) — red-checked with the rim extension off.
        Clipped("a 7-tile round, clipped to all of it", RoundBrush(14), all: true);
        Clipped("a 7-tile round, clipped to its lower half", RoundBrush(14), all: false);

        // ⛔⛔ AND A SHAPE KEEPS CLEAR OF THE GROUND BESIDE IT (D857, Joe's Millbrook: a field in a quarry's notch). A 3×3-tile
        // block with its top-left tile cut out, in quarter-tiles, the notch tile held by somebody else: rounded freely its
        // inward corner bulges INTO the notch (the bug, measured here so the check cannot pass on a shape that never bulged);
        // kept clear of the notch, no point of the outline enters it.
        KeptClear();

        return complaints.Count == 0
            ? $"[widths] zone outlines: ✅ every shape closed and kept its area{sizes}"
            : "[widths] zone outlines: ⛔ " + string.Join("; ", complaints) + sizes;

        void KeptClear()
        {
            var quarry = new HashSet<Vector2I>();
            foreach ((int x, int y) in Block(12, 12))
            {
                if (x >= 4 || y >= 4)
                {
                    quarry.Add(new Vector2I(x, y));
                }
            }

            static bool InTheNotch(Vector2I cell) => cell.X >= 0 && cell.X < 4 && cell.Y >= 0 && cell.Y < 4;

            // How far into the notch tile's square (cells −0.5 … 3.5 each way) the outline reaches, in cells — walked along
            // every side, not only at its points: a side between two points on the notch's edges cuts straight across it
            // (D857's red check scored zero on the points alone).
            static float Into(List<Vector2[]> loops)
            {
                float deepest = 0f;
                foreach (Vector2[] loop in loops)
                {
                    for (int i = 0; i + 1 < loop.Length; i++)
                    {
                        for (int step = 0; step <= 16; step++)
                        {
                            Vector2 p = loop[i].Lerp(loop[i + 1], step / 16f);
                            deepest = Mathf.Max(deepest, Mathf.Min(3.5f - p.X, 3.5f - p.Y));
                        }
                    }
                }

                return deepest;
            }

            float free = Into(Trace(quarry, SubTilesPerTile));
            float kept = Into(Trace(quarry, SubTilesPerTile, keepClearOf: InTheNotch));
            if (free < 0.1f)
            {
                complaints.Add($"keep clear: rounded freely the notch was never entered ({free:F2} cells), so the check measures nothing");
            }

            if (kept > 1e-4f)
            {
                complaints.Add($"keep clear: the outline reaches {kept:F2} cells into ground it was told to keep clear of");
            }

            sizes += $"; kept clear of a neighbour's notch ({free:F2} cells in when rounded freely, {kept:F2} kept)";
        }

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
            float triangles = TriangleArea(Fill(loops));
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

        void Clipped(string what, IEnumerable<(int X, int Y)> cells, bool all)
        {
            var set = new HashSet<Vector2I>();
            foreach ((int x, int y) in cells)
            {
                set.Add(new Vector2I(x, y));
            }

            Vector2[] whole = Fill(Trace(set, SubTilesPerTile));
            float wholeArea = TriangleArea(whole);

            var keep = new HashSet<Vector2I>();
            int midY = 0;
            foreach (Vector2I cell in set)
            {
                midY += cell.Y;
            }

            midY /= set.Count;
            foreach (Vector2I cell in set)
            {
                if (all || cell.Y < midY)
                {
                    keep.Add(cell);
                }
            }

            Vector2[] clipped = ClipToCells(whole, keep, set, SubTilesPerTile);
            float area = TriangleArea(clipped);
            sizes += $" · {what} clips to {area:F1} of {wholeArea:F1}";

            if (all)
            {
                // Itself, to float noise: every cut would be on an edge between two kept cells.
                if (Mathf.Abs(area - wholeArea) > 0.05f)
                {
                    complaints.Add($"{what}: the clip covers {area:F1} of its own {wholeArea:F1} — the fill bulges past the cells it is clipped to");
                }
            }
            else
            {
                // About half: the cut runs through the curve's own triangles.
                float wanted = wholeArea * keep.Count / set.Count;
                if (Mathf.Abs(area - wanted) > Mathf.Max(0.5f, wanted * 0.1f))
                {
                    complaints.Add($"{what}: the clip covers {area:F1}, wanted about {wanted:F1}");
                }
            }

            // Nothing of it lies INSIDE a field cell it was not kept for.
            int trespassing = 0;
            for (int i = 0; i + 2 < clipped.Length; i += 3)
            {
                Vector2 centroid = (clipped[i] + clipped[i + 1] + clipped[i + 2]) / 3f;
                var cell = new Vector2I(Mathf.RoundToInt(centroid.X), Mathf.RoundToInt(centroid.Y));
                if (set.Contains(cell) && !keep.Contains(cell))
                {
                    trespassing++;
                }
            }

            if (trespassing > 0)
            {
                complaints.Add($"{what}: {trespassing} clipped triangles sit on field cells that were not kept");
            }

            // ⛔ And nothing of it lies OUTSIDE the field beside a cell that was not kept (D366): the
            // bulge past an unsown rim cell goes with the cell. A triangle in the void is fine only
            // if the field cell nearest its centroid is a kept one — there the bulge is the curve.
            int wedges = 0;
            for (int i = 0; i + 2 < clipped.Length; i += 3)
            {
                Vector2 centroid = (clipped[i] + clipped[i + 1] + clipped[i + 2]) / 3f;
                var on = new Vector2I(Mathf.RoundToInt(centroid.X), Mathf.RoundToInt(centroid.Y));
                if (set.Contains(on))
                {
                    continue;
                }

                float nearest = float.MaxValue;
                bool nearestKept = false;
                foreach (Vector2I cell in set)
                {
                    float distance = centroid.DistanceSquaredTo(new Vector2(cell.X, cell.Y));
                    if (distance < nearest - 1e-4f)
                    {
                        nearest = distance;
                        nearestKept = keep.Contains(cell);
                    }
                    else if (Mathf.Abs(distance - nearest) <= 1e-4f)
                    {
                        nearestKept |= keep.Contains(cell);
                    }
                }

                if (!nearestKept)
                {
                    wedges++;
                }
            }

            if (wedges > 0)
            {
                complaints.Add($"{what}: {wedges} clipped triangles bulge into the void beside a cell that was not kept — the rim would show the wrong colour");
            }
        }

        void Filled(string what, IEnumerable<(int X, int Y)> tiles, float wanted, float within)
        {
            var set = new HashSet<Vector2I>();
            foreach ((int x, int y) in tiles)
            {
                set.Add(new Vector2I(x, y));
            }

            float area = TriangleArea(Fill(Trace(set)));
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
    /// <param name="tiles">The shape, as the tiles (or cells) it covers.</param>
    /// <param name="cellsPerTile">How many cells a tile is — 1 for a tile shape, <c>SubTile.PerTile</c> for paint.</param>
    /// <param name="sharpCornerTiles">
    /// A side at least this long, in tiles, keeps its corner — the player drew it (D343). A
    /// <b>yard</b> passes <see cref="float.PositiveInfinity"/>: nobody drew a yard, so no corner of
    /// one is ever kept (D374, Joe: *"what is this big square that showed up in the middle?"* — a
    /// 3×3 block of worn tiles whose every side was two tiles, the sharp-corner length exactly).
    /// </param>
    /// <param name="keepClearOf">Cells somebody else holds: a corner within the rounding's reach of one is kept where it was painted (D857).</param>
    internal static List<Vector2[]> Trace(
        HashSet<Vector2I> tiles, int cellsPerTile = 1, float sharpCornerTiles = SharpCornerTiles,
        Func<Vector2I, bool>? keepClearOf = null)
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

            loops.Add(Round(straightened, cellsPerTile, sharpCornerTiles, Pinned(straightened, cellsPerTile, keepClearOf)));
        }

        return loops;
    }

    /// <summary>
    /// ⭐⭐ THE CORNERS THAT MUST NOT BE ROUNDED, BECAUSE ROUNDING WOULD CROSS SOMEBODY ELSE'S GROUND (D857). Joe, Millbrook:
    /// <i>"i painted the farm field on the existing quarry and it overlapped."</i> The sim had kept them apart — the field held
    /// the grass tile in the quarry's top-left notch, the quarry the stone around it — but rounding cuts a shape's INWARD
    /// corners outward (a convex corner's cut lies inside, a concave one's in the empty quarter beyond it), so the quarry's
    /// floor and outline curved into the field's tile. So a corner with a <paramref name="keepClearOf"/> cell within
    /// <see cref="RimReach"/> — as far as the rounding can carry a curve past its cells, the reach <see cref="ClipToCells"/>
    /// already uses — is kept exactly where it was painted, D334's sharp-corner rule for one more reason. Everywhere else the
    /// rounding is untouched, so a round brush in open ground stays round.
    /// </summary>
    private static HashSet<Vector2>? Pinned(List<Vector2> corners, int cellsPerTile, Func<Vector2I, bool>? keepClearOf)
    {
        if (keepClearOf is null)
        {
            return null;
        }

        int reach = RimReach(cellsPerTile);
        HashSet<Vector2>? pinned = null;
        foreach (Vector2 corner in corners)
        {
            // A corner sits on cell edges (half-integers); the cells whose square comes within the reach of it.
            int fromX = Mathf.FloorToInt(corner.X - reach);
            int toX = Mathf.CeilToInt(corner.X + reach);
            int fromY = Mathf.FloorToInt(corner.Y - reach);
            int toY = Mathf.CeilToInt(corner.Y + reach);
            for (int y = fromY; y <= toY && !(pinned?.Contains(corner) ?? false); y++)
            {
                for (int x = fromX; x <= toX; x++)
                {
                    if (Mathf.Abs(x - corner.X) <= reach + 0.5f && Mathf.Abs(y - corner.Y) <= reach + 0.5f && keepClearOf(new Vector2I(x, y)))
                    {
                        (pinned ??= new HashSet<Vector2>()).Add(corner);
                        break;
                    }
                }
            }
        }

        return pinned;
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
    /// <summary>
    /// ⭐⭐ A fill cut down to some of its own cells — <b>how a half-sown field follows the paint's
    /// curve at the fence and the tile's edge inside it</b> (D362).
    /// </summary>
    /// <remarks>
    /// <para>
    /// Joe, on a round field with a stage tint that stair-stepped where it met the circle: *"conform
    /// to the edge of the paintbrush, no matter how small … a perfect circle."* D360 drew a partial
    /// stage as its painted quarters, square, which is right where sown meets bare inside the field
    /// and wrong where sown meets the paint's edge. So the stage is the FIELD's own smooth fill
    /// <b>with the cells that are not the stage taken away</b>: every triangle of the fill has the
    /// non-stage cells' squares (as horizontal runs) subtracted from it, and each subtraction leaves
    /// up to four disjoint convex pieces — left of the run, right of it, below, above. Nothing is
    /// ever cut along the fence, because the fence is not a field cell's edge; inside the field the
    /// cuts are the tile edges. Disjoint by construction, so no facet and no gap (D352's argument).
    /// </para>
    /// <para>
    /// ⛔ The first draft clipped TO the kept cells instead, and lost 1% of a round to the curve's
    /// own bulge past its cells — the smoothing cuts a staircase's concave corners outward — which
    /// would have been a sliver of bare earth along every fence. The self-check's first line is
    /// that a fill with nothing taken away is itself. Cached with the trace, never per frame.
    /// </para>
    /// <para>
    /// ⛔⛔ <b>AND THE BULGE BESIDE AN UNSOWN RIM CELL IS CUT WITH THE CELL (D366).</b> Joe: *"look
    /// at the yellow edges of the round farming area — that looks so cheap and gross."* The
    /// subtraction cuts the fill by the non-stage cells' SQUARES, and the fill bulges past its rim
    /// cells into the void beyond any cell — where no square ever reached it — so a ripe tint kept
    /// wedges outside an unsown rim. So each non-stage cell's square is extended outward, on every
    /// side that faces no field cell, by <see cref="RimReach"/> cells: beside an unsown rim cell the
    /// bulge goes with the cell; beside a sown one it stays, because there it IS the field's curve.
    /// ⚠️ At a concave corner between a sown and an unsown rim cell the whole notch is cut, and the
    /// bare field shows in it — a notch of the field's own colour, never a wedge of the wrong one.
    /// </para>
    /// </remarks>
    internal static Vector2[] ClipToCells(Vector2[] triangles, HashSet<Vector2I> keep, HashSet<Vector2I> field, int cellsPerTile)
    {
        var away = new HashSet<Vector2I>();
        foreach (Vector2I cell in field)
        {
            if (!keep.Contains(cell))
            {
                away.Add(cell);
            }
        }

        // The void beside an unsown rim cell, out to where the smoothing can have reached.
        int reach = RimReach(cellsPerTile);
        var rim = new HashSet<Vector2I>();
        foreach (Vector2I cell in away)
        {
            foreach (Vector2I step in new[] { new Vector2I(1, 0), new Vector2I(-1, 0), new Vector2I(0, 1), new Vector2I(0, -1) })
            {
                for (int k = 1; k <= reach; k++)
                {
                    Vector2I beyond = cell + (step * k);
                    if (field.Contains(beyond))
                    {
                        break;
                    }

                    rim.Add(beyond);
                }
            }
        }

        away.UnionWith(rim);

        List<(Vector2I From, int Length)> runs = RunsOf(away);
        var outcome = new List<Vector2>();
        var pieces = new List<List<Vector2>>();
        var next = new List<List<Vector2>>();
        var scratch = new List<Vector2>(8);

        for (int t = 0; t + 2 < triangles.Length; t += 3)
        {
            pieces.Clear();
            pieces.Add(new List<Vector2> { triangles[t], triangles[t + 1], triangles[t + 2] });

            foreach ((Vector2I from, int length) in runs)
            {
                float left = from.X - 0.5f;
                float right = from.X + length - 0.5f;
                float bottom = from.Y - 0.5f;
                float top = from.Y + 0.5f;

                next.Clear();
                foreach (List<Vector2> piece in pieces)
                {
                    if (!Meets(piece, left, right, bottom, top))
                    {
                        next.Add(piece);
                        continue;
                    }

                    // The complement of a rectangle, as four disjoint convex regions.
                    Keep(Cut(piece, scratch, p => left - p.X, (p, q) => Lerp(p, q, (left - p.X) / (q.X - p.X))));
                    Keep(Cut(piece, scratch, p => p.X - right, (p, q) => Lerp(p, q, (right - p.X) / (q.X - p.X))));

                    List<Vector2> middle = Cut(piece, scratch, p => p.X - left, (p, q) => Lerp(p, q, (left - p.X) / (q.X - p.X)));
                    middle = Cut(middle, scratch, p => right - p.X, (p, q) => Lerp(p, q, (right - p.X) / (q.X - p.X)));
                    Keep(Cut(middle, scratch, p => bottom - p.Y, (p, q) => Lerp(p, q, (bottom - p.Y) / (q.Y - p.Y))));
                    Keep(Cut(middle, scratch, p => p.Y - top, (p, q) => Lerp(p, q, (top - p.Y) / (q.Y - p.Y))));

                    // ⚠️ A cut that only grazes a piece leaves a zero-area sliver of collinear
                    // points; kept, the first draft drew three thousand of them for a half field.
                    void Keep(List<Vector2> part)
                    {
                        if (part.Count >= 3 && Mathf.Abs(AreaOf(part)) > 1e-6f)
                        {
                            next.Add(part);
                        }
                    }
                }

                (pieces, next) = (next, pieces);
            }

            foreach (List<Vector2> piece in pieces)
            {
                for (int i = 1; i + 1 < piece.Count; i++)
                {
                    outcome.Add(piece[0]);
                    outcome.Add(piece[i]);
                    outcome.Add(piece[i + 1]);
                }
            }
        }

        return outcome.ToArray();

        static Vector2 Lerp(Vector2 p, Vector2 q, float t) => p + ((q - p) * t);

        static float AreaOf(List<Vector2> polygon)
        {
            float twice = 0f;
            for (int i = 0; i < polygon.Count; i++)
            {
                Vector2 p = polygon[i];
                Vector2 q = polygon[(i + 1) % polygon.Count];
                twice += (p.X * q.Y) - (q.X * p.Y);
            }

            return twice / 2f;
        }

        static bool Meets(List<Vector2> piece, float left, float right, float bottom, float top)
        {
            float minX = float.MaxValue, maxX = float.MinValue, minY = float.MaxValue, maxY = float.MinValue;
            foreach (Vector2 p in piece)
            {
                minX = Mathf.Min(minX, p.X);
                maxX = Mathf.Max(maxX, p.X);
                minY = Mathf.Min(minY, p.Y);
                maxY = Mathf.Max(maxY, p.Y);
            }

            return !(maxX <= left || minX >= right || maxY <= bottom || minY >= top);
        }

        static List<Vector2> Cut(
            List<Vector2> piece, List<Vector2> scratch, Func<Vector2, float> inside, Func<Vector2, Vector2, Vector2> crossing)
        {
            var copy = new List<Vector2>(piece);
            ClipAgainst(copy, scratch, inside, crossing);
            return copy;
        }
    }

    /// <summary>
    /// How far past its cells a smoothed fill can bulge, in cells — <b>the straightening's own
    /// cap</b>. A chord across a concave step reaches at most <see cref="CornerCutTiles"/> from the
    /// corner, and every later pass stays inside the hull of the last; two cells at four a tile.
    /// </summary>
    internal static int RimReach(int cellsPerTile) => Mathf.CeilToInt(CornerCutTiles * cellsPerTile);

    /// <summary>Cells as horizontal runs — (leftmost cell, length) — in a stated order.</summary>
    private static List<(Vector2I From, int Length)> RunsOf(HashSet<Vector2I> cells)
    {
        var runs = new List<(Vector2I, int)>();
        var sorted = new List<Vector2I>(cells);
        sorted.Sort((p, q) => p.Y != q.Y ? p.Y.CompareTo(q.Y) : p.X.CompareTo(q.X));

        int i = 0;
        while (i < sorted.Count)
        {
            Vector2I start = sorted[i];
            int length = 1;
            while (i + length < sorted.Count && sorted[i + length].Y == start.Y && sorted[i + length].X == start.X + length)
            {
                length++;
            }

            runs.Add((start, length));
            i += length;
        }

        return runs;
    }

    /// <summary>One Sutherland–Hodgman pass: keep the side where <paramref name="inside"/> is not negative.</summary>
    private static void ClipAgainst(
        List<Vector2> polygon, List<Vector2> scratch, Func<Vector2, float> inside, Func<Vector2, Vector2, Vector2> crossing)
    {
        scratch.Clear();
        int count = polygon.Count;
        for (int i = 0; i < count; i++)
        {
            Vector2 current = polygon[i];
            Vector2 previous = polygon[(i + count - 1) % count];
            bool currentIn = inside(current) >= 0f;
            bool previousIn = inside(previous) >= 0f;

            if (currentIn)
            {
                if (!previousIn)
                {
                    scratch.Add(crossing(previous, current));
                }

                scratch.Add(current);
            }
            else if (previousIn)
            {
                scratch.Add(crossing(previous, current));
            }
        }

        polygon.Clear();
        polygon.AddRange(scratch);
    }

    /// <summary>
    /// ⛔⛔ AND A VILLAGE-SIZED FILL IS QUICK (Joe, 2026-11-14: <i>"painting areas is very slow/choppy"</i>). The fill re-runs
    /// on every brushful, and a brushful comes every frame while the mouse is held — so a residential area of ~2,200 outline
    /// points (sub-tile steps, D336, rounded twice) cost 125–312 ms a stroke on his save (the art lane measured it). Posed
    /// here at that size: rounds overlapping along a wave with rounds cut out of it, so it has the curves, the length and
    /// the holes; timed (best of three, wall-clock — the view's, never the sim's) and its tiling asked as <c>Tiled</c> does.
    /// </summary>
    internal static string AVillageSizedFillIsQuick()
    {
        var cells = new HashSet<Vector2I>();
        for (int i = 0; i < 4; i++)
        {
            int cx = i * 22;
            int cy = (int)(Mathf.Sin(i * 0.9f) * 30f);
            int radius = 16 + (i * 7 % 9);
            Paint(cells, cx, cy, radius, add: true);
        }

        for (int i = 1; i < 4; i += 2)
        {
            Paint(cells, (i * 22) + 4, (int)(Mathf.Sin(i * 0.9f) * 30f) + 3, 5, add: false);
        }

        List<Vector2[]> loops = Trace(cells, 4); // quarter-tiles, as the map's (SubTile.PerTile, a sim type)
        int points = 0;
        float polygon = 0f;
        foreach (Vector2[] loop in loops)
        {
            points += loop.Length;
            polygon += SignedArea(loop);
        }

        polygon = Mathf.Abs(polygon);
        double best = double.MaxValue;
        Vector2[] triangles = System.Array.Empty<Vector2>();
        for (int run = 0; run < 3; run++)
        {
            var clock = System.Diagnostics.Stopwatch.StartNew();
            triangles = Fill(loops);
            best = System.Math.Min(best, clock.Elapsed.TotalMilliseconds);
        }

        float ratio = polygon > 0f ? TriangleArea(triangles) / polygon : 0f;
        string sizes = $"{points} outline points in {loops.Count} loops fill in {best:F1} ms as {triangles.Length / 3} triangles, "
            + $"tiled to {ratio:F3} of its own area";

        // ⚠️ Twenty milliseconds is a stroke inside a frame and a bit with the rest of the trace (the art lane's 7.5 ms
        // sub-tile scan); measured before the fix, so the band is known to see the defect.
        var complaints = new List<string>();
        if (best > 20.0)
        {
            complaints.Add($"the fill took {best:F1} ms — a brushful a frame would be choppy");
        }

        if (Mathf.Abs(ratio - 1f) > 0.005f)
        {
            complaints.Add($"the triangles cover {ratio:F3} of the polygon");
        }

        return complaints.Count == 0
            ? $"[widths] zone fill: ✅ {sizes}"
            : $"[widths] zone fill: ⛔ {string.Join("; ", complaints)} — {sizes}";

        static void Paint(HashSet<Vector2I> into, int cx, int cy, int radius, bool add)
        {
            long limit = (long)radius * (radius + 1);
            for (int dy = -radius; dy <= radius; dy++)
            {
                for (int dx = -radius; dx <= radius; dx++)
                {
                    if (((long)dx * dx) + ((long)dy * dy) <= limit)
                    {
                        var at = new Vector2I(cx + dx, cy + dy);
                        if (add)
                        {
                            into.Add(at);
                        }
                        else
                        {
                            into.Remove(at);
                        }
                    }
                }
            }
        }
    }

    /// <summary>
    /// The region the loops enclose — outer loops less their holes — as triangles that cover it exactly once.
    /// </summary>
    /// <remarks>
    /// <para>
    /// ⭐⭐ IN LEVEL STRIPS, NOT BY EAR CLIPPING (Joe, 2026-11-14: <i>"painting areas is very slow/choppy"</i>). D352's ear
    /// clipping was exact — it fixed D345's overlapping Delaunay facets — but Godot's clipper costs about the square of the
    /// points, and the fill re-runs on every brushful: a residential area of 2,200 outline points took 125–312 ms a stroke
    /// on his save, and the probe's 4,363-point pose 792 ms. <b>This is exact in the same sense and close to linear.</b>
    /// </para>
    /// <para>
    /// Every edge that is not level, swept downwards from one vertex height to the next: in each strip the edges crossing
    /// it, ordered by where they cross its middle, pair off outside-inside — even-odd, so a hole is outside without being
    /// bridged in (an outer loop and its holes are disjoint loops) — and each pair bounds a trapezoid, two triangles. A
    /// trapezoid runs on down while both its edges do, so there are about as many as edges, not strips.
    /// </para>
    /// <para>
    /// ⚠️ <b>Why it cannot crack or draw darker:</b> trapezoids in one strip are separated by outside; the only shared
    /// boundaries are LEVEL lines at a vertex's height, where both sides use that same y exactly, and the map is drawn
    /// unrotated (<c>ToScreen</c> is a scale and an offset), so no pixel can fall between or under both. A sloped side is
    /// always a polygon edge with outside beyond it. The self-check's <c>Tiled</c> lines measure it: triangles against the
    /// loops' own area, 1.000.
    /// </para>
    /// </remarks>
    internal static Vector2[] Fill(List<Vector2[]> loops)
    {
        var edges = new List<(Vector2 Top, Vector2 Bottom)>();
        var heights = new List<float>();
        foreach (Vector2[] loop in loops)
        {
            for (int i = 0; i < loop.Length; i++)
            {
                Vector2 a = loop[i];
                Vector2 b = loop[(i + 1) % loop.Length];
                if (a.Y == b.Y)
                {
                    continue; // level — or the loop's closing repeat — crosses no strip
                }

                edges.Add(a.Y < b.Y ? (a, b) : (b, a));
                heights.Add(a.Y);
                heights.Add(b.Y);
            }
        }

        heights.Sort();
        var levels = new List<float>(heights.Count);
        foreach (float y in heights)
        {
            if (levels.Count == 0 || levels[^1] != y)
            {
                levels.Add(y);
            }
        }

        edges.Sort((p, q) => p.Top.Y.CompareTo(q.Top.Y));

        var kept = new List<Vector2>();
        var active = new List<int>();
        var running = new List<(int Left, int Right, float From)>();
        var pairs = new HashSet<(int, int)>();
        int nextEdge = 0;
        for (int s = 0; s + 1 < levels.Count; s++)
        {
            float top = levels[s];
            float middle = (top + levels[s + 1]) / 2f;

            active.RemoveAll(e => edges[e].Bottom.Y <= top);
            while (nextEdge < edges.Count && edges[nextEdge].Top.Y <= top)
            {
                active.Add(nextEdge);
                nextEdge++;
            }

            active.Sort((p, q) => XAt(edges[p], middle).CompareTo(XAt(edges[q], middle)));
            pairs.Clear();
            for (int k = 0; k + 1 < active.Count; k += 2)
            {
                pairs.Add((active[k], active[k + 1]));
            }

            // A trapezoid whose two edges no longer pair ends at this strip's top; one that still does runs on.
            for (int r = running.Count - 1; r >= 0; r--)
            {
                (int left, int right, float from) = running[r];
                if (!pairs.Remove((left, right)))
                {
                    Trapezoid(left, right, from, top);
                    running.RemoveAt(r);
                }
            }

            foreach ((int left, int right) in pairs)
            {
                running.Add((left, right, top));
            }
        }

        float bottom = levels.Count > 0 ? levels[^1] : 0f;
        foreach ((int left, int right, float from) in running)
        {
            Trapezoid(left, right, from, bottom);
        }

        return kept.ToArray();

        void Trapezoid(int left, int right, float from, float to)
        {
            var a = new Vector2(XAt(edges[left], from), from);
            var b = new Vector2(XAt(edges[right], from), from);
            var c = new Vector2(XAt(edges[right], to), to);
            var d = new Vector2(XAt(edges[left], to), to);
            kept.Add(a);
            kept.Add(b);
            kept.Add(c);
            kept.Add(a);
            kept.Add(c);
            kept.Add(d);
        }

        // At a vertex's own height the vertex itself, exactly — so a trapezoid's corner is the outline's point.
        static float XAt((Vector2 Top, Vector2 Bottom) edge, float y) =>
            y <= edge.Top.Y ? edge.Top.X
            : y >= edge.Bottom.Y ? edge.Bottom.X
            : edge.Top.X + ((y - edge.Top.Y) * (edge.Bottom.X - edge.Top.X) / (edge.Bottom.Y - edge.Top.Y));
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
    private static Vector2[] Round(List<Vector2> loop, int cellsPerTile, float sharpCornerTiles, HashSet<Vector2>? pinned = null)
    {
        // ⛔ The two rules below are stated in TILES and the loop is in CELLS (D343).
        float cornerCut = CornerCutTiles * cellsPerTile;
        float sharpCorner = sharpCornerTiles * cellsPerTile;

        List<Vector2> points = loop;

        // ---- Stage 1: steps into chords ----
        for (int pass = 0; pass < Straightenings; pass++)
        {
            points = Cut(points, sharpCorner, cornerCut, 2f, pinned);
        }

        // ⚠️ Two cuts meeting at a step's midpoint leave the same point twice; drawn,
        // that is a zero-length segment the polyline pays for, and measured, it hid every corner.
        points = WithoutRepeats(points);

        // ---- Stage 2: chords into a curve ----
        for (int pass = 0; pass < Roundings; pass++)
        {
            points = Cut(points, sharpCorner, float.PositiveInfinity, 4f, pinned);
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
        List<Vector2> points, float sharpCorner, float cap, float fraction, HashSet<Vector2>? pinned = null)
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

                // Two long runs meeting: a corner somebody chose. Kept exactly. ⭐ And a corner beside somebody else's
                // ground (D857, `Pinned`): kept exactly, so the curve never crosses into it. The kept point is the very
                // same value every pass, so the pin holds through all of them.
                if ((back >= sharpCorner && forward >= sharpCorner) || (pinned?.Contains(here) ?? false))
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

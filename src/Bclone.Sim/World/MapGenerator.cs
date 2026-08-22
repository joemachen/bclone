using Bclone.Sim.Config;
using Bclone.Sim.Determinism;

namespace Bclone.Sim.World;

/// <summary>
/// Generates the valley from the run's seed (D18).
/// </summary>
/// <remarks>
/// <para>
/// <b>Draw order is the contract.</b> Every value comes from the run's one
/// <see cref="DeterministicRandom"/>, in the order written here, and inserting a draw
/// in the middle shifts every subsequent value — silently invalidating every seed
/// anybody has written down and every golden test. It is the same hazard D5 names for
/// system execution order, and it deserves the same treatment: reordering is a
/// behavioural change, never a tidy-up.
/// </para>
/// <para>
/// <b>The generator is bounded rather than checked.</b> The economy is derived from how
/// far the worst-placed home is from its nearest forage site
/// (<see cref="VillageEconomy.RoundTripTicks"/>), so a generator free to put sites
/// anywhere would make the food economy a property of the seed. Instead it draws
/// <em>within</em> radii the economy already reads, so the distance budget holds by
/// construction — no reject-and-redraw loop, and no seed that is quietly unsurvivable.
/// That is the answer to the spec's §3, and it is what turns "is this valley fair?"
/// from a hope into a property the type system nearly enforces.
/// </para>
/// </remarks>
public static class MapGenerator
{
    /// <summary>Build the valley. Same seed and config ⇒ byte-identical map.</summary>
    public static GeneratedMap Generate(SimConfig config, DeterministicRandom rng)
    {
        ArgumentNullException.ThrowIfNull(config);
        ArgumentNullException.ThrowIfNull(rng);

        int width = config.MapWidth;
        int height = config.MapHeight;
        int minX = config.MapMinX;
        int minY = config.MapMinY;

        var terrain = new Terrain[width * height];
        var soil = new byte[width * height];

        // ---- 1. The river ------------------------------------------
        CarveRiver(config, rng, terrain, width, height);

        // ---- 2 and 3. THE STANDS AND THE BERRY PATCHES ARE GONE ------
        //
        // ⭐ Two ring-drawn tree stands and six ring-drawn forage sites used to be laid here,
        // and with them went the last placeholder in the economy: food was a **fact of the
        // map** rather than a decision (`forests-and-gathering.md`, Joe). The valley is
        // wooded across its whole area now (step 7), the player sites a gatherer's hut in it,
        // and *the trees in that hut's ring decide what a trip is worth.* **Timber and food
        // compete for the same trees**, which is the whole point.
        //
        // ⚠️ THIS MOVES EVERY SEED AND THAT IS UNAVOIDABLE. Those two loops consumed random
        // draws, so deleting them shifts every subsequent value: the founding site, the soil,
        // both seams and the woodland are all different now for every seed ever written down.
        // Draw order is the seed contract (§1) and this is the one kind of change that is
        // allowed to break it — a slice that removes generated content rather than adding it.
        // All three goldens are re-taken here, once, with the old values kept beside them.
        //
        // The nudge-out-of-water pass went with them. It existed because berries do not grow
        // in the river; woodland is painted tile by tile over open grass and never needs it.

        // ---- 4. The founding site ----------------------------------
        // Where the first homes and the village's buildings go. Kept near the middle
        // of the ring of sites, because the economy's distance budget is derived from
        // a village that sits inside that ring rather than off to one side.
        GridPos wanted = ClampInside(
            new GridPos(
                DrawJitter(rng, config.FoundingJitterTiles),
                DrawJitter(rng, config.FoundingJitterTiles)),
            config);

        // AND ON THE SAME SIDE OF THE RIVER AS ITS WORK.
        //
        // Not being IN the water is not enough, which is what the first version
        // assumed. Water is impassable (D40), so a settlement founded on the far bank
        // from every berry patch is a village that starves in its first year without
        // anybody having made a decision — measured, on seed 1, where the river runs
        // straight through where the village wanted to be: peak population zero.
        //
        // Until bridges exist the generator owes the village a valley it can live in
        // (spec §6), so the founding site moves to the reachable side rather than the
        // map being redrawn. Costs no random draws, so the seed contract is untouched.
        GridPos founding = ChooseFoundingSite(
            terrain, wanted, width, height, minX, minY);

        // ---- 5. Soil ------------------------------------------------
        // Generated, hashed, and read by nothing yet — it is here so that when §2.3's
        // soil depletion lands it does not have to change the DRAW ORDER, which would
        // invalidate every seed already written down.
        for (int i = 0; i < soil.Length; i++)
        {
            soil[i] = (byte)rng.NextInt(config.SoilQualityMin, config.SoilQualityMax + 1);
        }

        // ---- 6. Stone and iron --------------------------------------
        // ⭐ APPENDED AFTER EVERY EXISTING DRAW, AND THAT IS THE WHOLE OF THE CARE HERE.
        // Draw order is the contract (§1 of this file): inserting these anywhere earlier
        // would shift every subsequent value, so the river, the stands, the forage sites,
        // the founding site and the soil would all move for every seed ever written down.
        // Added at the end, all of those are byte-identical and only the new tiles differ.
        //
        // SEAMS, NOT SCATTER — the same argument the forest stands are built on, and D67's
        // reason for refusing a percentage roll: you can see a seam, so going after it is a
        // decision rather than a lottery. Scattered ore would be texture.
        //
        // STONE NEAR, IRON FAR. That is the design rather than flavour: reaching the iron
        // is a thing the player chooses to do, and a valley whose ore sits in the far woods
        // plays differently from one where it is on the doorstep (§2.5's argument for
        // seeded maps).
        PaintSeams(
            config, rng, terrain, Terrain.Rock,
            config.StoneSeamCount, config.StoneSeamRingTiles, config.StoneSeamRadiusTiles,
            width, height, minX, minY);

        PaintSeams(
            config, rng, terrain, Terrain.IronDeposit,
            config.IronSeamCount, config.IronSeamRingTiles, config.IronSeamRadiusTiles,
            width, height, minX, minY);

        // ---- 7. Woodland across the whole valley ---------------------
        // ⭐ THE VALLEY IS WOODED NOW, NOT DOTTED WITH TWO STANDS (Joe,
        // `specs/forests-and-gathering.md`). "There should be generated forests on the map
        // naturally, just like stone, iron, water — lots of them, actually", so that a
        // gatherer's hut can be sited in woodland from the first year.
        //
        // ⚠️ APPENDED AFTER EVERY EXISTING DRAW, for the reason §1 of this file gives and
        // D91 already had to take care over: inserting these draws anywhere earlier would
        // shift every subsequent value, and the river, the stands, the forage sites, the
        // founding site, the soil and both seams would move for every seed ever written
        // down. Added last, all of those are byte-identical and only trees differ.
        //
        // OVER OPEN GRASS ONLY, which is the same rule `PaintSeams` follows and here it
        // matters in the other direction: woodland drawn over the seams would quietly take
        // the stone and iron back out of the valley a slice after they were put in.
        PaintWoodland(config, rng, terrain, founding, width, height, minX, minY);

        return new GeneratedMap(width, height, minX, minY, terrain, soil, founding);
    }

    /// <summary>
    /// A position on a ring around the origin, one slot per site, plus a little jitter.
    /// </summary>
    /// <remarks>
    /// Evenly spaced slots rather than free angles, because "spread" is a requirement
    /// (D24) and not an average. Drawing angles at random would sometimes put four
    /// sites in one quadrant, which is the layout that starved the village once
    /// already. Jitter makes each valley different; the slots make every valley
    /// habitable.
    /// </remarks>
    private static GridPos DrawRingPosition(
        DeterministicRandom rng, int radius, int jitter, int index, int count)
    {
        GridPos slot = RingSlot(index, radius);
        return new GridPos(slot.X + DrawJitter(rng, jitter), slot.Y + DrawJitter(rng, jitter));
    }

    /// <summary>
    /// Where the nth site sits before any jitter — the <b>canonical</b> valley.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Public and RNG-free on purpose. <see cref="VillageEconomy"/> derives the food
    /// economy from how far the worst-placed home is from its nearest site, and it has
    /// to be able to ask that question <em>without</em> generating a world — otherwise
    /// the economy becomes a property of the seed and two runs have different physics.
    /// So the economy budgets against this layout plus the worst jitter the generator
    /// may add, and every seed lands inside that budget by construction.
    /// </para>
    /// <para>
    /// Evenly spaced slots rather than free angles, because "spread" is a requirement
    /// (D24) and not an average. Drawing angles at random would sometimes put four
    /// sites in one quadrant, which is precisely the layout that starved the village
    /// once already — central homes idle beside a full thicket while the outskirts had
    /// nothing in reach.
    /// </para>
    /// </remarks>
    public static GridPos RingSlot(int index, int radius)
    {
        // Eight compass slots walked in order, so the arithmetic stays integer — a
        // trigonometric ring would put floats in worldgen, against D2.
        (int X, int Y)[] directions =
        {
            (1, 0), (-1, 0), (0, 1), (0, -1),
            (1, 1), (-1, -1), (1, -1), (-1, 1),
        };

        (int X, int Y) direction = directions[index % directions.Length];

        // Diagonals are longer in Manhattan terms, so halve them — otherwise the
        // corner sites sit twice as far out as the cardinal ones and the ring is a
        // star.
        bool diagonal = direction.X != 0 && direction.Y != 0;
        int reach = diagonal ? (radius + 1) / 2 : radius;

        // Later rings step outward, so more sites than slots still spreads.
        int ringsOut = index / directions.Length;
        reach += ringsOut * radius / 2;

        return new GridPos(direction.X * reach, direction.Y * reach);
    }

    // `CanonicalForageSites` and `CanonicalTreeStands` are deleted with the things they
    // described (slice 5). They gave the economy a jitter-free layout to budget against, so
    // that one derivation held for every seed rather than each valley having its own
    // physics. **The bound is the gatherer hut's ring now** — a number, not a layout — which
    // does the same job without needing a canonical map to consult.

    private static int DrawJitter(DeterministicRandom rng, int jitter) =>
        jitter <= 0 ? 0 : rng.NextInt(-jitter, jitter + 1);

    /// <summary>
    /// Cut a river along the valley's long axis, wandering as it goes.
    /// </summary>
    /// <remarks>
    /// Along rather than across, per D26: the valley is wide because §2.5 describes a
    /// river valley, and a river runs down one. It wanders by a step at a time so the
    /// shape is a watercourse rather than a canal.
    /// </remarks>
    private static void CarveRiver(
        SimConfig config, DeterministicRandom rng, Terrain[] terrain, int width, int height)
    {
        if (config.RiverWidthTiles <= 0)
        {
            return;
        }

        // Start somewhere in the middle band, so the river never hugs an edge and
        // cuts a thin strip of valley off from everything.
        int band = height / 4;
        int y = rng.NextInt(band, height - band);

        for (int x = 0; x < width; x++)
        {
            for (int w = 0; w < config.RiverWidthTiles; w++)
            {
                int row = y + w;
                if (row >= 0 && row < height)
                {
                    terrain[(row * width) + x] = Terrain.Water;
                }
            }

            // Wander: -1, 0 or +1 each column.
            y += rng.NextInt(-1, 2);
            y = Math.Clamp(y, 1, height - config.RiverWidthTiles - 1);
        }
    }

    /// <summary>
    /// Lay seams of one kind of deposit around a ring, clumped rather than scattered.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Only ever over open grass.</b> Not water, for the obvious reason, and <b>not
    /// forest</b> — because overwriting trees would quietly take timber out of the valley
    /// and the whole food-and-fuel economy is derived against how much wood a village can
    /// reach. A seam that costs the village a stand is a balance change hiding inside a
    /// worldgen change.
    /// </para>
    /// <para>
    /// <b>The ring-and-jitter shape is copied from the forage sites deliberately</b>
    /// (D24): drawing angles at random clusters things, and a valley whose four stone
    /// seams all landed in one corner is a valley where the resource may as well not
    /// exist for half the village.
    /// </para>
    /// </remarks>
    private static void PaintSeams(
        SimConfig config,
        DeterministicRandom rng,
        Terrain[] terrain,
        Terrain kind,
        int count,
        int ringTiles,
        int radius,
        int width,
        int height,
        int minX,
        int minY)
    {
        for (int i = 0; i < count; i++)
        {
            GridPos centre = ClampInside(
                DrawRingPosition(rng, ringTiles, config.SiteJitterTiles, i, count), config);

            for (int dy = -radius; dy <= radius; dy++)
            {
                for (int dx = -radius; dx <= radius; dx++)
                {
                    if (Math.Abs(dx) + Math.Abs(dy) > radius)
                    {
                        continue;
                    }

                    int x = centre.X + dx - minX;
                    int row = centre.Y + dy - minY;
                    if (x < 0 || x >= width || row < 0 || row >= height)
                    {
                        continue;
                    }

                    int index = (row * width) + x;
                    if (terrain[index] == Terrain.Grass)
                    {
                        terrain[index] = kind;
                    }
                }
            }
        }
    }

    /// <summary>
    /// How many woodland clumps this valley gets — <b>derived from a stated coverage</b>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>The target is content; the count is a consequence</b> (D16). *"About this much of the
    /// valley is wooded"* is a statement about what kind of place this is and a modder may
    /// change it freely; how many clumps that takes is arithmetic, and typing it would mean a
    /// bigger map quietly got a barer valley.
    /// </para>
    /// <para>
    /// ⚠️ <b>Clumps overlap, so the coverage actually achieved is lower than the target</b> —
    /// they are dropped independently, and none may fall on water or a seam. That is why the
    /// number is a *target* rather than a promise, and why what the valley really ends up with
    /// is asserted by a measurement rather than by this arithmetic
    /// (<c>MapGenerationTests</c>). Solving the overlap exactly wants logarithms, and floats
    /// are banned from sim-critical paths (D2).
    /// </para>
    /// </remarks>
    public static int ForestClumpCount(SimConfig config)
    {
        ArgumentNullException.ThrowIfNull(config);

        int tiles = config.MapWidth * config.MapHeight;
        int wanted = tiles * config.ForestCoveragePercent / 100;
        int perClump = ClumpArea(config.ForestClumpRadiusTiles);

        return perClump <= 0 ? 0 : VillageEconomy.CeilingDivide(wanted, perClump);
    }

    /// <summary>Tiles in a diamond of this radius — the shape <c>PaintForest</c> paints.</summary>
    private static int ClumpArea(int radius) =>
        radius < 0 ? 0 : (2 * radius * radius) + (2 * radius) + 1;

    /// <summary>Scatter woodland clumps over the whole valley.</summary>
    /// <remarks>
    /// <b>Anywhere, unlike everything else in this file, and that is the point.</b> The stands,
    /// the forage sites and the seams are all drawn on rings, because each is a small number of
    /// places that had to be *spread* (D24) — four seams in one corner is a resource half the
    /// village cannot reach. Woodland is the opposite problem: there is a lot of it, so
    /// independent placement gives a valley with thick parts and thin parts, which is what
    /// makes siting a gatherer's hut a decision.
    /// </remarks>
    private static void PaintWoodland(
        SimConfig config,
        DeterministicRandom rng,
        Terrain[] terrain,
        GridPos founding,
        int width,
        int height,
        int minX,
        int minY)
    {
        int count = ForestClumpCount(config);

        for (int i = 0; i < count; i++)
        {
            // Two draws per clump, always — never a redraw. A rejection loop would make the
            // number of random values consumed depend on the terrain, which is precisely the
            // hidden coupling that stops a seed reproducing its world (see the forage sites).
            var centre = new GridPos(
                rng.NextInt(minX, minX + width),
                rng.NextInt(minY, minY + height));

            PaintForest(
                terrain, centre, config.ForestClumpRadiusTiles, width, height, minX, minY,
                onlyOverGrass: true,
                keepClear: founding,
                keepClearRadius: config.FoundingClearingRadiusTiles);
        }
    }

    /// <summary>Paint a diamond of woodland.</summary>
    /// <remarks>
    /// <para>
    /// <b><c>onlyOverGrass</c> is true for the scattered woodland</b>, which is drawn
    /// <em>after</em> the seams and must not take them back out of the valley; false for the
    /// tree stands, which are drawn before anything else and may cover bare ground freely.
    /// </para>
    /// <para>
    /// <b>⭐ <c>keepClear</c> is the founding glade, and it exists because the alternative was
    /// measured as fatal.</b> With the valley wooded, 40 of the 81 tiles within four of the
    /// founding site were forest — so the pile, the builder's hut and the woodcutter's hut all
    /// waited on a clearing before anything could begin. Measured on the shipped opening:
    /// <b>the pile stood at t67 instead of t1, the hut never stood at all, and all four
    /// founders froze</b>, against 4 alive and 2 roofed in the same opening on bare ground.
    /// That is D93's finding — <em>any inserted hop kills winter 1</em> — arriving from
    /// worldgen rather than from labour.
    /// </para>
    /// <para>
    /// <b>It is a skip during the woodland pass, not a clearing afterwards</b>, and the
    /// difference matters: clearing after the fact would strip the tree stands drawn in step 2
    /// as well, quietly taking timber out of the valley. Skipping only ever declines to add
    /// trees, so the stands, the seams and the river are untouched by construction.
    /// </para>
    /// <para>
    /// It is also the true picture: exiles arriving in a river valley settle a glade. The woods
    /// begin a few tiles out, which is close enough for a gatherer's hut and far enough that
    /// the opening is not a clearing puzzle.
    /// </para>
    /// </remarks>
    private static void PaintForest(
        Terrain[] terrain,
        GridPos centre,
        int radius,
        int width,
        int height,
        int minX,
        int minY,
        bool onlyOverGrass = false,
        GridPos? keepClear = null,
        int keepClearRadius = 0)
    {
        for (int dy = -radius; dy <= radius; dy++)
        {
            for (int dx = -radius; dx <= radius; dx++)
            {
                if (Math.Abs(dx) + Math.Abs(dy) > radius)
                {
                    continue;
                }

                int x = centre.X + dx - minX;
                int row = centre.Y + dy - minY;
                if (x < 0 || x >= width || row < 0 || row >= height)
                {
                    continue;
                }

                int index = (row * width) + x;

                // Trees do not grow in the river. Water wins, which also stops a stand
                // from quietly bridging it.
                if (terrain[index] == Terrain.Water)
                {
                    continue;
                }

                if (onlyOverGrass && terrain[index] != Terrain.Grass)
                {
                    continue;
                }

                if (keepClear is GridPos glade
                    && glade.ManhattanDistanceTo(new GridPos(centre.X + dx, centre.Y + dy))
                        <= keepClearRadius)
                {
                    continue;
                }

                terrain[index] = Terrain.Forest;
            }
        }
    }

    /// <summary>
    /// A spot to found the village that can actually reach its work.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Walkable tiles are labelled into connected components — one sweep — and the
    /// village goes into the component holding the most forage sites, as near to where
    /// it wanted to be as that allows. A village with berries it cannot walk to is not
    /// a hard start, it is an unplayable one, and non-negotiable 1 says a death has to
    /// be traceable to a decision. Nobody decided the river was there.
    /// </para>
    /// <para>
    /// Ties go to the tile nearest the wanted spot, then to the lower y and then the
    /// lower x — a total order, because "whichever the scan found first" would make the
    /// whole world depend on iteration order.
    /// </para>
    /// </remarks>
    private static GridPos ChooseFoundingSite(
        Terrain[] terrain,
        GridPos wanted,
        int width,
        int height,
        int minX,
        int minY)
    {
        int[] component = LabelComponents(terrain, width, height);

        // ⭐ THE BIGGEST PIECE OF WALKABLE GROUND, and it had to stop being "the most work"
        // because there is no longer any work on the map to count (`forests-and-gathering.md`
        // slice 5). This ranked land masses by the forage sites and tree stands they could
        // reach; both are retired, and the thing that replaced them — woodland — **cannot be
        // used here**, because `PaintWoodland` is drawn at step 7 and this is step 4. Asking
        // about trees that do not exist yet would mean moving the woodland draw earlier, and
        // draw order is the seed contract.
        //
        // **Size is the honest successor, and it is close to what the old rule measured
        // anyway:** the sites were spread across the whole valley, so "the land mass with the
        // most of them" was very nearly "the biggest land mass" already. It costs no draws,
        // needs nothing that has not been generated yet, and states the thing the rule was
        // always for — *the founders settle the largest ground they can walk across, and are
        // never stranded on an island by a river they cannot cross* (D40, spec §6).
        var tilesPerComponent = new Dictionary<int, int>();
        for (int i = 0; i < component.Length; i++)
        {
            if (component[i] >= 0)
            {
                tilesPerComponent[component[i]] = tilesPerComponent.GetValueOrDefault(component[i]) + 1;
            }
        }

        GridPos best = wanted;
        int bestRoom = -1;
        int bestDistance = int.MaxValue;

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                int index = (y * width) + x;
                if (terrain[index] == Terrain.Water)
                {
                    continue;
                }

                int room = tilesPerComponent.GetValueOrDefault(component[index]);
                var here = new GridPos(x + minX, y + minY);
                int distance = here.ManhattanDistanceTo(wanted);

                // Ties to the tile nearest the spot the generator wanted, then by scan
                // order — a total order, as before, so no two runs can disagree.
                bool better = room > bestRoom
                    || (room == bestRoom && distance < bestDistance);

                if (better)
                {
                    best = here;
                    bestRoom = room;
                    bestDistance = distance;
                }
            }
        }

        return best;
    }

    /// <summary>Label each walkable tile with the land mass it belongs to. -1 is water.</summary>
    private static int[] LabelComponents(Terrain[] terrain, int width, int height)
    {
        var label = new int[width * height];
        for (int i = 0; i < label.Length; i++)
        {
            label[i] = -1;
        }

        var queue = new Queue<int>();
        int next = 0;

        for (int start = 0; start < label.Length; start++)
        {
            if (terrain[start] == Terrain.Water || label[start] >= 0)
            {
                continue;
            }

            int current = next++;
            label[start] = current;
            queue.Enqueue(start);

            while (queue.Count > 0)
            {
                int index = queue.Dequeue();
                int x = index % width;
                int y = index / width;

                Visit(x + 1, y);
                Visit(x - 1, y);
                Visit(x, y + 1);
                Visit(x, y - 1);

                void Visit(int nx, int ny)
                {
                    if (nx < 0 || nx >= width || ny < 0 || ny >= height)
                    {
                        return;
                    }

                    int neighbour = (ny * width) + nx;
                    if (terrain[neighbour] == Terrain.Water || label[neighbour] >= 0)
                    {
                        return;
                    }

                    label[neighbour] = current;
                    queue.Enqueue(neighbour);
                }
            }
        }

        return label;
    }

    private static int ComponentAt(
        int[] component, GridPos position, int width, int height, int minX, int minY)
    {
        int x = position.X - minX;
        int y = position.Y - minY;

        return x < 0 || x >= width || y < 0 || y >= height ? -1 : component[(y * width) + x];
    }

    /// <summary>Walk outward until the tile is not water. Consumes no random draws.</summary>
    private static GridPos NudgeOutOfWater(
        Terrain[] terrain, GridPos from, int width, int height, int minX, int minY)
    {
        for (int radius = 0; radius < height; radius++)
        {
            for (int dy = -radius; dy <= radius; dy++)
            {
                for (int dx = -radius; dx <= radius; dx++)
                {
                    if (Math.Abs(dx) + Math.Abs(dy) != radius)
                    {
                        continue;
                    }

                    int x = from.X + dx - minX;
                    int row = from.Y + dy - minY;
                    if (x < 0 || x >= width || row < 0 || row >= height)
                    {
                        continue;
                    }

                    if (terrain[(row * width) + x] != Terrain.Water)
                    {
                        return new GridPos(from.X + dx, from.Y + dy);
                    }
                }
            }
        }

        throw new InvalidOperationException(
            "The whole valley is under water; no village could be founded. Check river_width_tiles.");
    }

    private static GridPos ClampInside(GridPos position, SimConfig config) =>
        new(
            Math.Clamp(position.X, config.MapMinX + 1, config.MapMaxX - 1),
            Math.Clamp(position.Y, config.MapMinY + 1, config.MapMaxY - 1));
}

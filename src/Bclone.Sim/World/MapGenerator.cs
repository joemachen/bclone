using Bclone.Sim.Config;
using Bclone.Sim.Determinism;

namespace Bclone.Sim.World;

/// <summary>
/// Generates the valley from the run's seed (D18).
/// </summary>
/// <remarks>
/// <para>
/// <b>⭐ Each stage draws on a stream of its own (D473, `seeded-map-generation.md §13`).</b> The
/// valley is generated in <see cref="Stage"/>s — the river, the founding site, the stone seams,
/// the iron seams, the woodland — and each stage's <see cref="DeterministicRandom"/> is seeded
/// from the run's seed and the stage's stated id through <see cref="SplitMix64"/>, then handed to
/// its helpers <b>by <c>ref</c></b>. So a draw added inside a stage moves that stage's numbers
/// and no other's, and a new stage — a lake, an island, a cliff from the new-game screen —
/// appends an id and moves no other stage's draws. (A later stage that paints only over grass
/// still sees an earlier stage's ground: a new river shape moves trees. What it never moves is
/// another stage's <em>numbers</em>.)
/// </para>
/// <para>
/// ⛔ <b>What this replaced (D435):</b> one stream, passed to every helper <em>by value</em>, so
/// each helper drew on a copy and the caller's stream never moved — every drawn seam shared one
/// offset, the founding jitter's x and y were one draw, the river and the soil began on the same
/// numbers. "Draw order is the contract" was the design's sentence; the code never kept it.
/// ⚠️ <c>DeterministicRandom</c> is a <c>struct</c>: a helper that takes one by value draws on
/// a copy. Pass it by <c>ref</c>.
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
    /// <summary>
    /// The stages of the valley, each with a stated id that is <b>never renumbered</b> (D473).
    /// </summary>
    /// <remarks>
    /// The id is the stage's seed: renumbering one reshuffles that stage for every seed anyone
    /// has written down. A new stage takes the next id.
    /// </remarks>
    internal enum Stage
    {
        River = 1,
        Founding = 2,
        StoneSeams = 3,
        IronSeams = 4,
        Woodland = 5,
    }

    /// <summary>A stage's seed: the run's seed and the stage's id through splitmix64 (D473).</summary>
    /// <remarks>
    /// ⛔ Never <see cref="DeterministicRandom"/>'s <c>stream</c> parameter: D344 measured small
    /// adjacent stream ids six times deadlier (6 dead valleys of 24 against 1).
    /// </remarks>
    internal static ulong StageSeed(ulong seed, Stage stage) => SplitMix64.Fold(seed, (ulong)stage);

    /// <summary>Build the valley. Same seed and config ⇒ byte-identical map.</summary>
    public static GeneratedMap Generate(SimConfig config, ulong seed)
    {
        ArgumentNullException.ThrowIfNull(config);

        int width = config.MapWidth;
        int height = config.MapHeight;
        int minX = config.MapMinX;
        int minY = config.MapMinY;

        var terrain = new Terrain[width * height];

        // ---- 1. The river ------------------------------------------
        var riverRng = new DeterministicRandom(StageSeed(seed, Stage.River));
        CarveRiver(config, ref riverRng, terrain, width, height);

        // ⭐ The two ring-drawn tree stands and six ring-drawn forage sites that once followed
        // are gone (`forests-and-gathering.md` slice 5): the valley is wooded across its whole
        // area (step 5), the player sites a gatherer's hut in it, and *the trees in that hut's
        // ring decide what a trip is worth.* **Timber and food compete for the same trees.**

        // ---- 2. The founding site ----------------------------------
        // Where the first homes and the village's buildings go. Kept near the middle
        // of the ring of sites, because the economy's distance budget is derived from
        // a village that sits inside that ring rather than off to one side. x is drawn, then y.
        var foundingRng = new DeterministicRandom(StageSeed(seed, Stage.Founding));
        int jitterX = DrawJitter(ref foundingRng, config.FoundingJitterTiles);
        int jitterY = DrawJitter(ref foundingRng, config.FoundingJitterTiles);
        GridPos wanted = ClampInside(new GridPos(jitterX, jitterY), config);

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
        // map being redrawn. It takes no draws.
        GridPos founding = ChooseFoundingSite(
            terrain, wanted, config.StartingResidentialRadius, width, height, minX, minY);

        // ⛔ GROUND QUALITY IS GONE (D395, built D470; its draw deleted in D473). Joe: *"remove
        // the 'ground' quality functionality from the game entirely."* If it ever returns, it is a
        // new stage, which moves no other stage's draws.

        // ---- 3 and 4. Stone and iron --------------------------------
        // SEAMS, NOT SCATTER — D67's reason for refusing a percentage roll: you can see a seam,
        // so going after it is a decision rather than a lottery. Scattered ore would be texture.
        //
        // STONE NEAR, IRON FAR. That is the design rather than flavour: reaching the iron
        // is a thing the player chooses to do, and a valley whose ore sits in the far woods
        // plays differently from one where it is on the doorstep (§2.5's argument for
        // seeded maps).
        //
        // ⭐ THE QUARRY'S SEAMS ARE HASHED, NEVER DRAWN (`quarry.md §3.1`, D434). Past the drawn
        // four and two, a seam's offset hashes its own stage's seed with its kind and its index,
        // so the extra rock costs no draws.
        int ironTiles = TilesToHold(config, Goods.Iron, config.IronSeamMinIron);

        ulong stoneSeed = StageSeed(seed, Stage.StoneSeams);
        var stoneRng = new DeterministicRandom(stoneSeed);
        PaintSeams(
            config, ref stoneRng, terrain, Terrain.Rock,
            config.StoneSeamCount, config.ExtraStoneSeams, config.StoneSeamRingTiles,
            config.StoneSeamRadiusTiles, 0, stoneSeed,
            width, height, minX, minY);

        ulong ironSeed = StageSeed(seed, Stage.IronSeams);
        var ironRng = new DeterministicRandom(ironSeed);
        PaintSeams(
            config, ref ironRng, terrain, Terrain.IronDeposit,
            config.IronSeamCount, config.ExtraIronSeams, config.IronSeamRingTiles,
            config.IronSeamRadiusTiles, ironTiles, ironSeed,
            width, height, minX, minY);

        // ---- 5. Woodland across the whole valley ---------------------
        // ⭐ THE VALLEY IS WOODED, NOT DOTTED WITH TWO STANDS (Joe,
        // `specs/forests-and-gathering.md`). "There should be generated forests on the map
        // naturally, just like stone, iron, water — lots of them, actually", so that a
        // gatherer's hut can be sited in woodland from the first year.
        //
        // OVER OPEN GRASS ONLY, which is the same rule `PaintSeams` follows and here it
        // matters in the other direction: woodland drawn over the seams would quietly take
        // the stone and iron back out of the valley a slice after they were put in. So it is
        // painted last, and a change to any stage before it can move trees.
        var woodlandRng = new DeterministicRandom(StageSeed(seed, Stage.Woodland));
        PaintWoodland(config, ref woodlandRng, terrain, founding, width, height, minX, minY);

        return new GeneratedMap(width, height, minX, minY, terrain, founding);
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
        ref DeterministicRandom rng, int radius, int jitter, int index)
    {
        GridPos slot = RingSlot(index, radius);
        int x = DrawJitter(ref rng, jitter);
        int y = DrawJitter(ref rng, jitter);
        return new GridPos(slot.X + x, slot.Y + y);
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

    private static int DrawJitter(ref DeterministicRandom rng, int jitter) =>
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
        SimConfig config, ref DeterministicRandom rng, Terrain[] terrain, int width, int height)
    {
        if (config.RiverWidthTiles <= 0)
        {
            return;
        }

        // Start somewhere in the middle band, so the river never hugs an edge and
        // cuts a thin strip of valley off from everything.
        int band = height / 4;
        int y = rng.NextInt(band, height - band);

        // ⭐⭐ THE WIDTH WANDERS AS WELL AS THE COURSE (D344, Joe: *"let's widen it by
        // ~50% with some variation"*).
        // ⛔ **HASHED FROM THE COLUMN, NOT DRAWN.** Under one shared stream (before D473) one extra
        // `rng` call per column would have shifted every value after it — and a first attempt at
        // this did exactly that, which put the SHIPPED valley (seed 12345) on ground where the
        // village stores no food in ten years. The river has its own stream now, so a draw here
        // would move only the river (and the trees that grow round it); the hash still keeps its
        // course where it is.
        // ⚠️ **A width drawn fresh each column would be noise, not variation** —
        // the banks would fray a tile in and out every step and read as a ragged hose. Hashing
        // the column in BLOCKS holds a width for several columns, so a reach reads as a pool or
        // a narrows.
        int widest = config.RiverWidthTiles + config.RiverWidthWanderTiles;
        int start = y;

        for (int x = 0; x < width; x++)
        {
            int wide = config.RiverWidthTiles;
            if (config.RiverWidthWanderTiles > 0)
            {
                uint spin = Scramble(x / 5, start);
                wide += (int)(spin % (uint)(config.RiverWidthWanderTiles + 1));
            }

            for (int w = 0; w < wide; w++)
            {
                int row = y + w;
                if (row >= 0 && row < height)
                {
                    terrain[(row * width) + x] = Terrain.Water;
                }
            }

            // Wander: -1, 0 or +1 each column.
            y += rng.NextInt(-1, 2);

            // ⚠️ Clamped against the WIDEST it may become, not against its width today
            // — or a river that swells while hugging the edge would run off the map.
            y = Math.Clamp(y, 1, height - widest - 1);
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
        ref DeterministicRandom rng,
        Terrain[] terrain,
        Terrain kind,
        int drawn,
        int hashed,
        int ringTiles,
        int radius,
        int leastTiles,
        ulong valley,
        int width,
        int height,
        int minX,
        int minY)
    {
        // ⭐ A seam grows no further than this, so a seam the river has nearly drowned cannot
        // spread across the valley looking for dry ground. Three rings is a radius-1 iron seam
        // grown to 25 tiles — measured enough for every iron seam in 64 valleys (`quarry.md §6.1`).
        const int MostGrowth = 3;

        IReadOnlyList<int> slots = SeamSlots(drawn, hashed, ringTiles);
        for (int n = 0; n < slots.Count; n++)
        {
            int i = slots[n];
            GridPos slot = RingSlot(i, ringTiles);
            GridPos centre = ClampInside(
                n < drawn
                    ? DrawRingPosition(ref rng, ringTiles, config.SiteJitterTiles, i)
                    : new GridPos(
                        slot.X + HashJitter(valley, kind, i, 0, config.SiteJitterTiles),
                        slot.Y + HashJitter(valley, kind, i, 1, config.SiteJitterTiles)),
                config);

            int r = radius;
            int held = PaintDiamond(terrain, kind, centre, r, width, height, minX, minY);
            while (held < leastTiles && r < radius + MostGrowth)
            {
                r++;
                held = PaintDiamond(terrain, kind, centre, r, width, height, minX, minY);
            }
        }
    }

    /// <summary>
    /// Which <see cref="RingSlot"/>s a kind of seam is laid at: the drawn ones first, in order,
    /// then the hashed ones — <b>never nearer the village than the ring itself</b>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// ⛔ <b>Found by the suite, not reasoned (D434):</b> <see cref="RingSlot"/> halves a diagonal
    /// so the ring is Manhattan-round, which puts the first ring's diagonals at (7, 7) — inside
    /// the founding's house plots. Laid there, four seams took the ground eight housing guards
    /// needed and put rock under the first building sites. So a hashed seam skips any slot whose
    /// larger coordinate is short of the ring: the first ring's diagonals are passed over and the
    /// second ring's cardinals (21) and diagonals (14, 14) are used instead.
    /// </para>
    /// <para>
    /// Public and draw-free, so a guard asks the generator's own rule rather than restating it.
    /// </para>
    /// </remarks>
    public static IReadOnlyList<int> SeamSlots(int drawn, int hashed, int ringTiles)
    {
        var slots = new List<int>(drawn + hashed);
        for (int i = 0; i < drawn; i++)
        {
            slots.Add(i);
        }

        for (int i = drawn; slots.Count < drawn + hashed; i++)
        {
            GridPos at = RingSlot(i, ringTiles);
            if (Math.Max(Math.Abs(at.X), Math.Abs(at.Y)) >= ringTiles)
            {
                slots.Add(i);
            }
        }

        return slots;
    }

    /// <summary>
    /// Paint a Manhattan diamond of <paramref name="kind"/> over open grass, and say how many of
    /// its tiles are that kind afterwards.
    /// </summary>
    private static int PaintDiamond(
        Terrain[] terrain, Terrain kind, GridPos centre, int radius, int width, int height, int minX, int minY)
    {
        int held = 0;
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

                if (terrain[index] == kind)
                {
                    held++;
                }
            }
        }

        return held;
    }

    /// <summary>
    /// A seam's offset from its slot, from a hash — the draw-free twin of <see cref="DrawJitter"/>.
    /// </summary>
    /// <remarks>
    /// splitmix64 over the seam kind's own stage seed, its kind, its index and the axis — well
    /// spread even for adjacent indices, which is the property D344 measured
    /// <c>DeterministicRandom</c>'s stream parameter lacking.
    /// </remarks>
    private static int HashJitter(ulong valley, Terrain kind, int index, int axis, int jitter)
    {
        if (jitter <= 0)
        {
            return 0;
        }

        ulong z = SplitMix64.Fold(valley, (ulong)((((int)kind * 64) + index) * 2 + axis));
        return (int)(z % (ulong)((2 * jitter) + 1)) - jitter;
    }

    /// <summary>Tiles of a seam it takes to hold <paramref name="least"/> of a good — its row's yield a tile.</summary>
    private static int TilesToHold(SimConfig config, Goods goods, int least)
    {
        if (least <= 0)
        {
            return 0;
        }

        int perTile = 0;
        foreach (GoodRow row in config.GoodsCatalog)
        {
            if (row.Id == (int)goods)
            {
                perTile = row.YieldPerTile;
            }
        }

        return perTile <= 0 ? 0 : (least + perTile - 1) / perTile;
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

    /// <summary>
    /// ⭐ Whether an offset falls inside a clump — <b>a circle with a wobbling edge</b>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The radius is modulated by a hash of the clump's centre and the direction, so each wood
    /// has its own lopsided outline and the same wood is the same shape for ever. **Nothing is
    /// drawn from the generator's randomness**, which is what lets the shape change without
    /// moving a single other thing in the valley.
    /// </para>
    /// <para>
    /// ⚠️ <b>Compared on squared distance</b>, because a square root here would be a
    /// float in a sim-critical path and D2 bans that outright. The wobble is applied to the
    /// squared radius instead.
    /// </para>
    /// </remarks>
    private static bool InsideTheClump(GridPos centre, int dx, int dy, int radius)
    {
        int away = (dx * dx) + (dy * dy);

        // Eight sectors round the clump, each with its own reach. The joins are left unblended
        // on purpose — the renderer's field smooths them far better than arithmetic would.
        int sector = ((dx >= 0 ? 1 : 0) * 4)
            + ((dy >= 0 ? 1 : 0) * 2)
            + (Math.Abs(dx) > Math.Abs(dy) ? 1 : 0);

        uint spin = Scramble(centre.X + (sector * 7919), centre.Y - (sector * 104729));

        int reach = radius * radius;
        int wobble = (int)(spin % 41) - 20;

        return away <= reach + (reach * wobble / 50);
    }

    /// <summary>A stateless hash of two coordinates — shape, and never a draw.</summary>
    private static uint Scramble(int x, int y)
    {
        unchecked
        {
            uint h = (uint)((x * 73856093) ^ (y * 19349663));
            h ^= h >> 13;
            h *= 0x85EBCA6Bu;
            h ^= h >> 16;
            return h;
        }
    }

    /// <summary>Tiles in a clump of this radius — the shape <c>PaintForest</c> paints.</summary>
    /// <remarks>
    /// ⛔⛔ <b>THIS HAD TO MOVE WITH THE SHAPE, AND FORGETTING IT WOULD HAVE BEEN A
    /// BALANCE CHANGE HIDING INSIDE A WORLDGEN CHANGE</b> (D344) — which is the exact thing
    /// <see cref="PaintSeams"/>'s own remarks warn about. It returned the area of a Manhattan
    /// diamond, <c>2r² + 2r + 1</c> = **41 tiles at radius 4**;
    /// <see cref="InsideTheClump"/> now paints a wobbling circle, which averages
    /// <c>πr²</c> ≈ **50**. Left alone, the generator would have dropped the same
    /// number of clumps and quietly put **a fifth more woodland** in every valley — more
    /// timber, more forage, and the whole food economy derived against the wrong valley.
    /// <para>
    /// ⚠️ <c>π</c> as <c>314/100</c>, because a float here is D2's ban. The
    /// truth is measured rather than asserted by <c>MapGenerationTests</c>, so this only has to
    /// be close.
    /// </para>
    /// </remarks>
    private static int ClumpArea(int radius) =>
        radius < 0 ? 0 : (314 * radius * radius / 100) + 1;

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
        ref DeterministicRandom rng,
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
        // ⭐⭐ A WOOD IS A BLOB, NOT A DIAMOND (D344). `Math.Abs(dx) + Math.Abs(dy)`
        // is a Manhattan ball and it looked like one: every clump in the valley was a lozenge
        // with its points on the compass. **D342 proved the renderer cannot hide it** — the
        // field's edge jitter moves a boundary about a tile, which on a nine-tile diamond is a
        // nibble, *while the same machinery transforms a two-tile river.* The difference is
        // entirely the ratio of the jitter to the feature, so the shape is the generator's.
        // ⭐ **Hashed from the clump's own centre and consuming no draws**, for the same
        // reason the river's width is.
        for (int dy = -radius; dy <= radius; dy++)
        {
            for (int dx = -radius; dx <= radius; dx++)
            {
                if (!InsideTheClump(centre, dx, dy, radius))
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
    /// village goes into the <b>largest</b> one, as near to where it wanted to be as that
    /// allows. A village on ground it cannot walk out of is not a hard start, it is an
    /// unplayable one, and non-negotiable 1 says a death has to be traceable to a decision.
    /// Nobody decided the river was there.
    /// <para>
    /// ⚠️ <b>~~the component holding the most forage sites~~ — corrected 2026-08-28.</b> The body
    /// of this method retired that rule and says so in an inline comment: <em>"it had to stop
    /// being 'the most work' because there is no longer any work on the map to count… size is the
    /// honest successor."</em> Forage sites were removed from the map in step C. <b>The rewrite
    /// updated the code and the inline comment and left the XML doc — the one a caller reads on
    /// hover — describing the old rule.</b>
    /// </para>
    /// </para>
    /// <para>
    /// ⭐ <b>And on dry ground, not on the bank (D472).</b> Within the largest piece, a tile with
    /// no water within <paramref name="dryRadius"/> — the starter zone's own diamond
    /// (<c>starting_residential_radius</c>) — beats one on the river's edge. Found by the
    /// per-stage reshuffle: on seed 99 the river ran a tile from the founding, half the starter
    /// zone lay across water nobody can cross, and the warm start had nowhere to put a house; on
    /// seed 24 the founding's work sat across it. Spec §10.1: *until bridges exist the generator
    /// must not cut the village off from its work.* A valley with no dry tile anywhere falls
    /// back to the old rule.
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
        int dryRadius,
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

        int[] toWater = StepsToWater(terrain, width, height);

        GridPos best = wanted;
        int bestRoom = -1;
        bool bestDry = false;
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
                bool dry = toWater[index] > dryRadius;

                // The biggest ground, then dry ground, then the tile nearest the spot the
                // generator wanted, then scan order — a total order, so no two runs can disagree.
                bool better = room > bestRoom
                    || (room == bestRoom && dry && !bestDry)
                    || (room == bestRoom && dry == bestDry && distance < bestDistance);

                if (better)
                {
                    best = here;
                    bestRoom = room;
                    bestDry = dry;
                    bestDistance = distance;
                }
            }
        }

        return best;
    }

    /// <summary>
    /// How many steps each tile is from the nearest water — Manhattan, through anything — or
    /// <see cref="int.MaxValue"/> in a valley with none (D472).
    /// </summary>
    /// <remarks>
    /// One breadth-first sweep out from every water tile at once, so the whole valley costs one
    /// pass rather than a search per candidate. Four neighbours, the cost field's own steps.
    /// </remarks>
    private static int[] StepsToWater(Terrain[] terrain, int width, int height)
    {
        var steps = new int[terrain.Length];
        var frontier = new Queue<int>();
        for (int i = 0; i < terrain.Length; i++)
        {
            if (terrain[i] == Terrain.Water)
            {
                steps[i] = 0;
                frontier.Enqueue(i);
            }
            else
            {
                steps[i] = int.MaxValue;
            }
        }

        while (frontier.Count > 0)
        {
            int i = frontier.Dequeue();
            int x = i % width;
            int y = i / width;
            int next = steps[i] + 1;

            if (x + 1 < width && steps[i + 1] > next) { steps[i + 1] = next; frontier.Enqueue(i + 1); }
            if (x > 0 && steps[i - 1] > next) { steps[i - 1] = next; frontier.Enqueue(i - 1); }
            if (y + 1 < height && steps[i + width] > next) { steps[i + width] = next; frontier.Enqueue(i + width); }
            if (y > 0 && steps[i - width] > next) { steps[i - width] = next; frontier.Enqueue(i - width); }
        }

        return steps;
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

    private static GridPos ClampInside(GridPos position, SimConfig config) =>
        new(
            Math.Clamp(position.X, config.MapMinX + 1, config.MapMaxX - 1),
            Math.Clamp(position.Y, config.MapMinY + 1, config.MapMaxY - 1));
}

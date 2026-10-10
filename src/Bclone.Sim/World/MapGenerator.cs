using Bclone.Sim.Config;
using Bclone.Sim.Core;
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
        CarveRiver(config, CourseOf(config, seed), ref riverRng, terrain, width, height);

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
        // ⭐ FOUND, NOT PLACED (D475, Joe: *"stone and iron nodes look planned and symmetrical"*).
        // Each seam takes a sector of its ring, a phase the ring draws, an angle and a reach inside
        // it, and an outcrop's wobbling outline — see `SeamsOf`. Stone first, so iron never lands
        // on rock.
        int ironTiles = TilesToHold(config, Goods.Iron, config.IronSeamMinIron);

        foreach (Seam seam in SeamsOf(config, seed, Terrain.Rock))
        {
            PaintOutcrop(terrain, Terrain.Rock, seam, 0, width, height, minX, minY);
        }

        foreach (Seam seam in SeamsOf(config, seed, Terrain.IronDeposit))
        {
            PaintOutcrop(terrain, Terrain.IronDeposit, seam, ironTiles, width, height, minX, minY);
        }

        // ⭐ AND THEN ACROSS THE WHOLE VALLEY (D543, Joe: *"more variety, more frequency across the whole valley"*;
        // `specs/seams-revisited.md`). The near seams above keep their guarantees and go down first, so they have the
        // ground; the valley-wide ones fill in after, in every shape and size, and only the near iron grows to 50.
        foreach (Seam seam in ScatteredSeamsOf(config, seed, Terrain.Rock))
        {
            PaintOutcrop(terrain, Terrain.Rock, seam, 0, width, height, minX, minY);
        }

        foreach (Seam seam in ScatteredSeamsOf(config, seed, Terrain.IronDeposit))
        {
            PaintOutcrop(terrain, Terrain.IronDeposit, seam, 0, width, height, minX, minY);
        }

        // ---- 5. Woodland across the whole valley ---------------------
        // ⭐ THE VALLEY IS WOODED, NOT DOTTED WITH TWO STANDS (Joe,
        // `specs/forests-and-gathering.md`). "There should be generated forests on the map
        // naturally, just like stone, iron, water — lots of them, actually", so that a
        // gatherer's hut can be sited in woodland from the first year.
        //
        // OVER OPEN GRASS ONLY, which is the same rule `PaintOutcrop` follows and here it
        // matters in the other direction: woodland drawn over the seams would quietly take
        // the stone and iron back out of the valley a slice after they were put in. So it is
        // painted last, and a change to any stage before it can move trees.
        var woodlandRng = new DeterministicRandom(StageSeed(seed, Stage.Woodland));
        PaintWoodland(config, ref woodlandRng, terrain, founding, width, height, minX, minY);

        return new GeneratedMap(width, height, minX, minY, terrain, founding);
    }

    // `CanonicalForageSites` and `CanonicalTreeStands` are deleted with the things they
    // described (slice 5). They gave the economy a jitter-free layout to budget against, so
    // that one derivation held for every seed rather than each valley having its own
    // physics. **The bound is the gatherer hut's ring now** — a number, not a layout — which
    // does the same job without needing a canonical map to consult.

    private static int DrawJitter(ref DeterministicRandom rng, int jitter) =>
        jitter <= 0 ? 0 : rng.NextInt(-jitter, jitter + 1);

    /// <summary>The values <c>river_course</c> may take, in the new-game screen's order (D477).</summary>
    public static IReadOnlyList<string> RiverCourses { get; } = new[] { "we", "ns", "nwse", "swne", "any" };

    /// <summary>Whether <paramref name="course"/> is one of <see cref="RiverCourses"/>.</summary>
    public static bool IsRiverCourse(string? course) => course is not null && RiverCourses.Contains(course);

    /// <summary>
    /// The way this valley's river runs: <c>river_course</c>, or under <c>any</c> one of the four
    /// <b>by hash of the River stage's seed</b> — never a draw, so <c>any</c> moves no draw (D477).
    /// </summary>
    public static string CourseOf(SimConfig config, ulong seed)
    {
        ArgumentNullException.ThrowIfNull(config);
        if (config.RiverCourse != "any")
        {
            return config.RiverCourse;
        }

        ulong pick = SplitMix64.Fold(StageSeed(seed, Stage.River), AnyCourseSalt);
        return RiverCourses[(int)(pick % 4)];
    }

    /// <summary>Folded into the River stage's seed to choose a course under <c>any</c>.</summary>
    private const ulong AnyCourseSalt = 0x52495645; // "RIVE"

    /// <summary>
    /// Cut the river the way <paramref name="course"/> says, wandering as it goes.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>West to east is the river every valley had before D477</b> — the same loop, the same draws
    /// in the same order — so the default moves nothing. North to south is that loop with the axes
    /// swapped. The diagonals are carved as a strip (<see cref="CarveDiagonalRiver"/>).
    /// </para>
    /// <para>
    /// Along rather than across, per D26: the valley is wide because §2.5 describes a river valley,
    /// and a river runs down one. It wanders by a step at a time so the shape is a watercourse
    /// rather than a canal.
    /// </para>
    /// </remarks>
    private static void CarveRiver(
        SimConfig config, string course, ref DeterministicRandom rng, Terrain[] terrain, int width, int height)
    {
        if (config.RiverWidthTiles <= 0)
        {
            return;
        }

        switch (course)
        {
            case "ns":
                CarveStraightRiver(config, ref rng, terrain, width, height, alongX: false);
                break;
            case "nwse":
                CarveDiagonalRiver(config, ref rng, terrain, width, height, falling: true);
                break;
            case "swne":
                CarveDiagonalRiver(config, ref rng, terrain, width, height, falling: false);
                break;
            default:
                CarveStraightRiver(config, ref rng, terrain, width, height, alongX: true);
                break;
        }
    }

    /// <summary>
    /// A river down one axis of the valley: west to east when <paramref name="alongX"/>, else north
    /// to south — the same carve with the axes swapped.
    /// </summary>
    private static void CarveStraightRiver(
        SimConfig config, ref DeterministicRandom rng, Terrain[] terrain, int width, int height, bool alongX)
    {
        int length = alongX ? width : height;
        int across = alongX ? height : width;

        // Start somewhere in the middle band, so the river never hugs an edge and
        // cuts a thin strip of valley off from everything.
        int band = across / 4;
        int y = rng.NextInt(band, across - band);

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

        for (int x = 0; x < length; x++)
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
                if (row >= 0 && row < across)
                {
                    int tile = alongX ? (row * width) + x : (x * width) + row;
                    terrain[tile] = Terrain.Water;
                }
            }

            // Wander: -1, 0 or +1 each column.
            y += rng.NextInt(-1, 2);

            // ⚠️ Clamped against the WIDEST it may become, not against its width today
            // — or a river that swells while hugging the edge would run off the map.
            y = Math.Clamp(y, 1, across - widest - 1);
        }
    }

    /// <summary>
    /// A river corner to corner: north-west to south-east when <paramref name="falling"/>, else
    /// south-west to north-east (D477).
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Carved as a strip, not a staircase.</b> Along the course a tile's position is
    /// <c>x + y</c> (falling) or <c>x − y</c> (rising); across it, the other. The river is every
    /// tile whose across-coordinate lies in <c>[d, d + span)</c>, where <c>d</c> wanders −1/0/+1 a
    /// step along the course just as a west–east river's row does. A step a walker takes changes
    /// both coordinates by exactly one, so a strip at least 2 wide whose edge moves at most one a
    /// step cannot be crossed without entering it — no ford (`new-game-screen.md §8.4` asks the real
    /// cost field rather than this sentence).
    /// </para>
    /// <para>
    /// <b>The width is measured across the course.</b> A strip <c>span</c> wide in <c>x ± y</c> is
    /// <c>span / √2</c> wide on the ground, so <c>span</c> is the width × √2, rounded — a diagonal 3
    /// is as wide to the eye as a west–east 3. Integer throughout (D2).
    /// </para>
    /// </remarks>
    private static void CarveDiagonalRiver(
        SimConfig config, ref DeterministicRandom rng, Terrain[] terrain, int width, int height, bool falling)
    {
        // The across-coordinate of a line through the middle of the valley, and the band the
        // start is drawn in — a quarter of the short side either way, as west–east's is.
        int centre = falling ? (width - height) / 2 : ((width + height) / 2) - 1;
        int band = Math.Min(width, height) / 4;
        int d = rng.NextInt(centre - band, centre + band);
        int start = d;

        int widest = SpanAcross(config.RiverWidthTiles + config.RiverWidthWanderTiles);
        int steps = width + height - 1;

        for (int s = 0; s < steps; s++)
        {
            int wide = config.RiverWidthTiles;
            if (config.RiverWidthWanderTiles > 0)
            {
                uint spin = Scramble(s / 7, start);
                wide += (int)(spin % (uint)(config.RiverWidthWanderTiles + 1));
            }

            int span = SpanAcross(wide);
            for (int a = d; a < d + span; a++)
            {
                // falling: s = x + y and a = x − y.  rising: s = x − y + (height − 1) and a = x + y.
                int twiceX = falling ? s + a : s - (height - 1) + a;
                if ((twiceX & 1) != 0)
                {
                    continue;
                }

                int x = twiceX / 2;
                int y = falling ? s - x : a - x;
                if (x >= 0 && x < width && y >= 0 && y < height)
                {
                    terrain[(y * width) + x] = Terrain.Water;
                }
            }

            d += rng.NextInt(-1, 2);
            d = Math.Clamp(d, centre - (2 * band), centre + (2 * band) - widest);
        }
    }

    /// <summary>A diagonal strip's span in <c>x ± y</c> for a river this many tiles wide: × √2, rounded, at least 2.</summary>
    private static int SpanAcross(int tilesWide) => Math.Max(2, ((tilesWide * 1414) + 500) / 1000);

    /// <summary>The shapes a seam takes (D543, Joe: *"all of the above, more variety"*).</summary>
    public enum SeamShape
    {
        /// <summary>A rounded outcrop with a wobbling edge — every seam before D543.</summary>
        Blob = 0,

        /// <summary>A long stripe a tile or two wide, bending as it runs.</summary>
        Vein = 1,

        /// <summary>Three to five small outcrops close together.</summary>
        Cluster = 2,

        /// <summary>A small outcrop with two or three veins running out of it.</summary>
        Arms = 3,
    }

    /// <summary>
    /// One seam: where it is centred, how big it is (in hundredths of a tile², a disc's r² × 100), its shape, and which
    /// way a vein or the first arm or part runs (a raw <see cref="Angle"/>).
    /// </summary>
    public readonly record struct Seam(GridPos Centre, int ReachHundredths, SeamShape Shape = SeamShape.Blob, int Turn = 0);

    /// <summary>
    /// Where a kind's seams lie and how big each is — <b>drawn from that kind's own stage</b>, and the
    /// one place the answer is decided (D475).
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Spread, then scattered.</b> D24's guarantee stands: a ring's seams take one equal sector
    /// each, so four never land in one quadrant — the layout that once starved a village. But the
    /// ring draws a phase, so its sectors do not sit on the compass, and each seam draws an angle
    /// inside its sector (<c>seam_angle_scatter_percent</c> of it) and a reach out from the ring
    /// (up to <c>seam_reach_scatter_percent</c> further, never nearer — D434 found seams nearer than
    /// the ring take the founders' house plots). It was eight compass slots with a tile of jitter,
    /// and Joe saw a cross: <em>"they do not look organically placed."</em>
    /// </para>
    /// <para>
    /// <b>Rings fill four, then eight.</b> Ring <c>k</c> holds up to <c>4k</c> seams at
    /// <c>1 + (k − 1)/2</c> times the kind's ring — the shipped layout to the seam (stone four at
    /// 14 and eight at 21, iron four at 26), so the economy measured against it stands.
    /// <c>…_seam_count</c> and <c>extra_…_seams</c> are summed; the split was the hash's, gone with it.
    /// </para>
    /// <para>
    /// <b>Public and pure</b>, so a guard asks the generator rather than restating it. Integer
    /// throughout: positions turn through <see cref="Angle"/>'s table (D2, D318).
    /// </para>
    /// </remarks>
    public static IReadOnlyList<Seam> SeamsOf(SimConfig config, ulong seed, Terrain kind) => Lay(config, seed, kind).Near;

    /// <summary>
    /// ⭐ The valley-wide seams (D543): one candidate per cell of a grid over the whole map, a valley's own richness
    /// deciding how many, each a size and a shape of its own. <b>Drawn after the near seams, on the same stage</b>, so
    /// the rings lie where they did and no other stage draws a number differently.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Across the whole valley, the far bank included</b> (Joe: *"keep them. we'll eventually add bridges"*), but
    /// never within <c>stone_seam_clear_of_founding_tiles</c> (iron: <c>iron_…</c>, further) of the founding — the house
    /// plots D434 kept the rings off, and for iron the doorstep.
    /// </para>
    /// <para>
    /// <b>How many is the valley's</b>: one draw of ±<c>seam_count_variety_percent</c> around
    /// <c>scattered_stone_seams</c> / <c>scattered_iron_seams</c>, so one valley is rich and the next is lean.
    /// <b>How big is skewed small</b>: a uniform draw squared, between <c>scattered_seam_tiles_min</c> and
    /// <c>…_max</c>, so pebbles are common and big outcrops rare. No iron minimum (Joe: only the near iron holds 50).
    /// </para>
    /// </remarks>
    public static IReadOnlyList<Seam> ScatteredSeamsOf(SimConfig config, ulong seed, Terrain kind) => Lay(config, seed, kind).Scattered;

    private static (List<Seam> Near, List<Seam> Scattered) Lay(SimConfig config, ulong seed, Terrain kind)
    {
        ArgumentNullException.ThrowIfNull(config);

        bool stone = kind == Terrain.Rock;
        int total = stone
            ? config.StoneSeamCount + config.ExtraStoneSeams
            : config.IronSeamCount + config.ExtraIronSeams;
        int ring = stone ? config.StoneSeamRingTiles : config.IronSeamRingTiles;
        int radius = stone ? config.StoneSeamRadiusTiles : config.IronSeamRadiusTiles;

        var rng = new DeterministicRandom(StageSeed(seed, stone ? Stage.StoneSeams : Stage.IronSeams));
        var seams = new List<Seam>(total);
        for (int k = 1; seams.Count < total; k++)
        {
            int here = Math.Min(4 * k, total - seams.Count);
            int reach = ring + ((k - 1) * ring / 2);
            int sector = 65536 / here;
            int swing = sector * config.SeamAngleScatterPercent / 200;
            int phase = rng.NextInt(0, 65536);

            for (int i = 0; i < here; i++)
            {
                int turn = phase + (i * sector) + (swing <= 0 ? 0 : rng.NextInt(-swing, swing + 1));
                int further = reach * config.SeamReachScatterPercent / 100;
                int far = reach + (further <= 0 ? 0 : rng.NextInt(0, further + 1));

                Point at = new Point(Fixed.FromInt(far), Fixed.Zero)
                    .RotatedBy(Angle.FromRaw(unchecked((ushort)turn)));
                GridPos centre = ClampInside(at.ToTile(), config);

                int size = 100 * radius * radius;
                int vary = size * config.SeamSizeScatterPercent / 100;
                if (vary > 0)
                {
                    size += rng.NextInt(-vary, vary + 1);
                }

                // ⭐ Its shape with it (D543), so a ring's seams are the same whatever rings come after — drawn after
                // all of them, the inner four took different shapes the moment the outer eight were added, and woods
                // moved (`TheQuarrysSeamsMovedNoForest` caught it). Solid shapes only, because the near seams'
                // guarantees count what lies round the centre: stone a blob or a blob with arms (three in reach), iron
                // a blob — arms grown to 50 ran their iron past what a player reads as one seam (a wide river left one
                // holding 24 within eight tiles of its centre).
                SeamShape shape = PickShape(config, stone, ref rng, nearOnly: true);
                seams.Add(new Seam(centre, size < 50 ? 50 : size, shape, rng.NextInt(0, 65536)));
            }
        }

        var scattered = new List<Seam>();
        int target = stone ? config.ScatteredStoneSeams : config.ScatteredIronSeams;
        if (target <= 0)
        {
            return (seams, scattered);
        }

        int variety = config.SeamCountVarietyPercent;
        int richness = 100 + (variety > 0 ? rng.NextInt(-variety, variety + 1) : 0);
        int wanted = ((target * richness) + 50) / 100;

        int cell = config.SeamScatterCellTiles;
        int clear = stone ? config.StoneSeamClearOfFoundingTiles : config.IronSeamClearOfFoundingTiles;
        var cells = new List<GridPos>();
        for (int y = config.MapMinY; y < config.MapMinY + config.MapHeight; y += cell)
        {
            for (int x = config.MapMinX; x < config.MapMinX + config.MapWidth; x += cell)
            {
                int cx = x + (cell / 2);
                int cy = y + (cell / 2);
                if ((cx * cx) + (cy * cy) >= clear * clear)
                {
                    cells.Add(new GridPos(x, y));
                }
            }
        }

        int least = config.ScatteredSeamTilesMin;
        int spread = config.ScatteredSeamTilesMax - least;
        for (int i = 0; i < wanted && i < cells.Count; i++)
        {
            // A cell of its own (a partial shuffle), a place inside it, a size, a shape and a heading — every draw made
            // whether or not the seam is kept, so one valley's skip never shifts the next seam's numbers.
            int j = rng.NextInt(i, cells.Count);
            (cells[i], cells[j]) = (cells[j], cells[i]);
            GridPos corner = cells[i];
            GridPos at = ClampInside(new GridPos(corner.X + rng.NextInt(0, cell), corner.Y + rng.NextInt(0, cell)), config);
            int u = rng.NextInt(0, 1001);
            int tiles = least + (spread * u * u / 1_000_000);
            SeamShape shape = PickShape(config, stone, ref rng, nearOnly: false);
            int turn = rng.NextInt(0, 65536);

            if ((at.X * at.X) + (at.Y * at.Y) < clear * clear)
            {
                continue;
            }

            scattered.Add(new Seam(at, Math.Max(50, tiles * 700 / 22), shape, turn));
        }

        return (seams, scattered);
    }

    /// <summary>A shape by this kind's weights (`stone_seam_shapes`, `iron_seam_shapes`) — the near seams only solid ones.</summary>
    private static SeamShape PickShape(SimConfig config, bool stone, ref DeterministicRandom rng, bool nearOnly)
    {
        IReadOnlyDictionary<string, int> weights = stone ? config.StoneSeamShapes : config.IronSeamShapes;
        int total = 0;
        foreach (SeamShape shape in Shapes)
        {
            total += Allowed(shape) ? weights.GetValueOrDefault(NameOf(shape)) : 0;
        }

        if (total <= 0)
        {
            return SeamShape.Blob;
        }

        int roll = rng.NextInt(0, total);
        foreach (SeamShape shape in Shapes)
        {
            roll -= Allowed(shape) ? weights.GetValueOrDefault(NameOf(shape)) : 0;
            if (roll < 0)
            {
                return shape;
            }
        }

        return SeamShape.Blob;

        bool Allowed(SeamShape shape) => !nearOnly || shape == SeamShape.Blob || (stone && shape == SeamShape.Arms);
    }

    /// <summary>The shapes in id order — the order their weights are rolled against.</summary>
    private static readonly SeamShape[] Shapes = { SeamShape.Blob, SeamShape.Vein, SeamShape.Cluster, SeamShape.Arms };

    /// <summary>A shape's name in the data file: <c>blob</c>, <c>vein</c>, <c>cluster</c>, <c>arms</c>.</summary>
    public static string NameOf(SeamShape shape) => shape switch
    {
        SeamShape.Vein => "vein",
        SeamShape.Cluster => "cluster",
        SeamShape.Arms => "arms",
        _ => "blob",
    };

    /// <summary>
    /// The tiles this seam covers painted alone on open grass — what its shape and size come to, asked of the painter
    /// itself rather than restated (D543's shape and size guards).
    /// </summary>
    public static IReadOnlyList<GridPos> FootprintOf(Seam seam, Terrain kind)
    {
        const int Half = 60;
        int side = (2 * Half) + 1;
        var scratch = new Terrain[side * side];
        int minX = seam.Centre.X - Half;
        int minY = seam.Centre.Y - Half;
        PaintSeamAt(scratch, kind, seam, seam.ReachHundredths, side, side, minX, minY);

        var tiles = new List<GridPos>();
        for (int i = 0; i < scratch.Length; i++)
        {
            if (scratch[i] == kind)
            {
                tiles.Add(new GridPos(minX + (i % side), minY + (i / side)));
            }
        }

        return tiles;
    }

    /// <summary>
    /// Paint one outcrop of <paramref name="kind"/> over open grass — growing it until it holds
    /// <paramref name="leastTiles"/> — and say how many of its tiles are that kind.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Only ever over open grass.</b> Not water, for the obvious reason, and never over another
    /// seam; the woodland is painted after, round it.
    /// </para>
    /// <para>
    /// ⭐ <b>An outcrop, not a stamp</b> (D475): the forests' wobbling outline (<see cref="Wobble"/>)
    /// on a size the seam drew, where it was a Manhattan diamond of one size for every seam — the
    /// shape D347 left "until the economy is in view". A seam grows a size step at a time, at most
    /// three, so one the river has nearly drowned cannot spread across the valley looking for dry
    /// ground — enough for every iron seam in 64 valleys to hold its 50 (`quarry.md §6.1`).
    /// </para>
    /// </remarks>
    private static int PaintOutcrop(
        Terrain[] terrain, Terrain kind, Seam seam, int leastTiles, int width, int height, int minX, int minY)
    {
        // ⚠️ Three steps were enough for every iron seam in 64 shipped valleys; a new-game screen's
        // wide river or diagonal drowns more of a seam (D480: a 6-wide river left one holding 8), so a
        // seam still short after three may take up to three more. A seam that held within three grows
        // exactly as it did — no shipped valley moves.
        const int MostGrowth = 6;

        int reach = seam.ReachHundredths;
        int held = PaintSeamAt(terrain, kind, seam, reach, width, height, minX, minY);
        for (int grown = 0; held < leastTiles && grown < MostGrowth; grown++)
        {
            // A ring a step: the radius one tile wider, as the diamond grew. Integer (D2).
            int radius = 0;
            while ((radius + 1) * (radius + 1) * 100 <= reach)
            {
                radius++;
            }

            reach += 100 * ((2 * radius) + 1);
            held = PaintSeamAt(terrain, kind, seam, reach, width, height, minX, minY);
        }

        return held;
    }

    /// <summary>
    /// Paint one seam in its shape at this size, over open grass only, and say how many tiles of its kind it covers —
    /// each tile counted once, whatever parts of the shape overlap (D543).
    /// </summary>
    private static int PaintSeamAt(
        Terrain[] terrain, Terrain kind, Seam seam, int reachHundredths, int width, int height, int minX, int minY)
    {
        var held = new HashSet<int>();
        var ground = new Ground(terrain, kind, width, height, minX, minY, held);
        switch (seam.Shape)
        {
            case SeamShape.Vein:
                PaintVein(ground, seam.Centre, seam.Turn, AreaOf(reachHundredths), wide: 0, fromTheCentre: false);
                break;

            case SeamShape.Cluster:
                {
                    // Three to five small outcrops round the centre, each a part of the size; where they sit is hashed
                    // from the centre, never drawn, so the shape changes nothing else in the valley.
                    int parts = 3 + (int)(Scramble(seam.Centre.X, seam.Centre.Y + 17) % 3);
                    int each = Math.Max(50, reachHundredths / parts);
                    int apart = 2 + RadiusOf(each);
                    for (int j = 0; j < parts; j++)
                    {
                        int turn = seam.Turn + (j * 65536 / parts) + (int)(Scramble(seam.Centre.X + j, seam.Centre.Y - j) % 8001) - 4000;
                        int far = apart + (int)(Scramble(seam.Centre.Y + j, seam.Centre.X) % 3);
                        PaintDisc(ground, Off(seam.Centre, far, 0, turn), each);
                    }

                    break;
                }

            case SeamShape.Arms:
                {
                    // A small outcrop and two or three veins one tile wide running out of it.
                    int core = Math.Max(50, reachHundredths * 45 / 100);
                    PaintDisc(ground, seam.Centre, core);
                    int arms = 2 + (int)(Scramble(seam.Centre.X - 31, seam.Centre.Y + 31) % 2);
                    int armTiles = Math.Max(2, AreaOf(reachHundredths) * 55 / 100 / arms) + RadiusOf(core);
                    for (int j = 0; j < arms; j++)
                    {
                        int turn = seam.Turn + (j * 65536 / arms) + (int)(Scramble(seam.Centre.X + (j * 13), seam.Centre.Y) % 10001) - 5000;
                        PaintVein(ground, seam.Centre, turn, armTiles, wide: 1, fromTheCentre: true);
                    }

                    break;
                }

            default:
                PaintDisc(ground, seam.Centre, reachHundredths);
                break;
        }

        return held.Count;
    }

    /// <summary>Where one seam's paint goes, and the tiles of its kind it has covered so far.</summary>
    private readonly record struct Ground(
        Terrain[] Terrain, Terrain Kind, int Width, int Height, int MinX, int MinY, HashSet<int> Held);

    /// <summary>One tile: painted if it is open grass, counted if it is this kind (painted now or already).</summary>
    private static void PaintTile(Ground ground, GridPos at)
    {
        int x = at.X - ground.MinX;
        int row = at.Y - ground.MinY;
        if (x < 0 || x >= ground.Width || row < 0 || row >= ground.Height)
        {
            return;
        }

        int index = (row * ground.Width) + x;
        if (ground.Terrain[index] == Terrain.Grass)
        {
            ground.Terrain[index] = ground.Kind;
        }

        if (ground.Terrain[index] == ground.Kind)
        {
            ground.Held.Add(index);
        }
    }

    /// <summary>A rounded outcrop with the forests' wobbling edge — every seam's shape before D543 (D475).</summary>
    private static void PaintDisc(Ground ground, GridPos centre, int reachHundredths)
    {
        int bound = 2;
        while (bound * bound * 100 < reachHundredths * 7 / 5)
        {
            bound++;
        }

        for (int dy = -bound; dy <= bound; dy++)
        {
            for (int dx = -bound; dx <= bound; dx++)
            {
                int away = 100 * ((dx * dx) + (dy * dy));
                if (away <= reachHundredths + (reachHundredths * Wobble(centre, dx, dy) / 50))
                {
                    PaintTile(ground, new GridPos(centre.X + dx, centre.Y + dy));
                }
            }
        }
    }

    /// <summary>
    /// A stripe of about this many tiles, a tile or two wide (<paramref name="wide"/> 0 lets the centre's hash choose),
    /// along a heading, bending a tile sideways now and then by hash. Through <see cref="Angle"/>'s table: integers (D2).
    /// </summary>
    private static void PaintVein(Ground ground, GridPos centre, int turn, int tiles, int wide, bool fromTheCentre)
    {
        if (wide <= 0)
        {
            wide = 1 + (int)(Scramble((centre.X * 3) + turn, centre.Y * 5) % 2);
        }

        int length = Math.Max(2, tiles / wide);
        int start = fromTheCentre ? 0 : -(length / 2);
        int bend = 0;
        for (int i = 0; i < length; i++)
        {
            int step = start + i;
            if (i % 4 == 3)
            {
                bend = Math.Clamp(bend + (int)(Scramble(centre.X + (step * 7919), centre.Y + turn) % 3) - 1, -2, 2);
            }

            for (int w = 0; w < wide; w++)
            {
                PaintTile(ground, Off(centre, step, bend + w, turn));
            }
        }
    }

    /// <summary>The tile <paramref name="along"/> tiles out and <paramref name="across"/> to the side of a heading from a centre.</summary>
    private static GridPos Off(GridPos centre, int along, int across, int turn)
    {
        GridPos off = new Point(Fixed.FromInt(along), Fixed.FromInt(across))
            .RotatedBy(Angle.FromRaw(unchecked((ushort)turn))).ToTile();
        return new GridPos(centre.X + off.X, centre.Y + off.Y);
    }

    /// <summary>About how many tiles a size covers: a disc's r² × 100 ÷ 100 × π, with π as 22/7 (D2).</summary>
    private static int AreaOf(int reachHundredths) => Math.Max(1, reachHundredths * 22 / 700);

    /// <summary>The whole-tile radius of a size: the largest r with r² × 100 within it.</summary>
    private static int RadiusOf(int reachHundredths)
    {
        int radius = 0;
        while ((radius + 1) * (radius + 1) * 100 <= reachHundredths)
        {
            radius++;
        }

        return radius;
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
        int reach = radius * radius;
        return away <= reach + (reach * Wobble(centre, dx, dy) / 50);
    }

    /// <summary>
    /// How much this direction's reach swells or shrinks, −20 to +20 fiftieths — the wobble a
    /// forest clump and a seam's outcrop share (D344, D475).
    /// </summary>
    /// <remarks>
    /// Eight sectors round the centre, each with its own reach, hashed from the centre — so each has
    /// its own lopsided outline and the same one is the same shape for ever. The joins are left
    /// unblended on purpose: the renderer's field smooths them far better than arithmetic would.
    /// </remarks>
    private static int Wobble(GridPos centre, int dx, int dy)
    {
        int sector = ((dx >= 0 ? 1 : 0) * 4)
            + ((dy >= 0 ? 1 : 0) * 2)
            + (Math.Abs(dx) > Math.Abs(dy) ? 1 : 0);

        uint spin = Scramble(centre.X + (sector * 7919), centre.Y - (sector * 104729));
        return (int)(spin % 41) - 20;
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
    /// the seams' painting (then `PaintSeams`) warned about. It returned the area of a Manhattan
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

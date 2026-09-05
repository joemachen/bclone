using System.Text;
using Bclone.Sim.Config;
using Bclone.Sim.Core;
using Bclone.Sim.Determinism;
using Bclone.Sim.Logging;
using Bclone.Sim.World;
using Xunit;
using Xunit.Abstractions;

namespace Bclone.Sim.Tests;

/// <summary>
/// The valley is generated from the seed — <c>specs/seeded-map-generation.md</c> (D18).
/// </summary>
public sealed class MapGenerationTests
{
    private readonly ITestOutputHelper _output;

    public MapGenerationTests(ITestOutputHelper output) => _output = output;

    private static SimConfig Config => VillageFixtures.Village;

    private static GeneratedMap Generate(SimConfig config, ulong seed) =>
        MapGenerator.Generate(config, new DeterministicRandom(seed));

    [Fact]
    public void SameSeedGivesTheSameValley()
    {
        ulong hashA = StateHash.MixMap(0UL, Generate(Config, 12345UL));
        ulong hashB = StateHash.MixMap(0UL, Generate(Config, 12345UL));

        Assert.Equal(hashA, hashB);
    }

    [Fact]
    public void DifferentSeedsGiveDifferentValleys()
    {
        // Anti-vacuity (D7). Without this the test above passes just as happily over a
        // generator that ignores its seed and returns the same valley every time —
        // which would be the most expensive kind of green.
        ulong hashA = StateHash.MixMap(0UL, Generate(Config, 1UL));
        ulong hashB = StateHash.MixMap(0UL, Generate(Config, 2UL));

        Assert.NotEqual(hashA, hashB);
    }

    [Fact]
    public void TheWorldIsGeneratedFromTheRunsOwnSeed()
    {
        // The point of D18: quoting one number reproduces an entire run, world
        // included. Two worlds built from the same seed must agree about the valley
        // before anybody has taken a step.
        SimWorld a = SimFactory.CreatePhase0(Config, new InMemoryLogSink(), seedOverride: 777UL).World;
        SimWorld b = SimFactory.CreatePhase0(Config, new InMemoryLogSink(), seedOverride: 777UL).World;
        SimWorld c = SimFactory.CreatePhase0(Config, new InMemoryLogSink(), seedOverride: 778UL).World;

        Assert.Equal(StateHash.MixMap(0UL, a.Map), StateHash.MixMap(0UL, b.Map));
        Assert.NotEqual(StateHash.MixMap(0UL, a.Map), StateHash.MixMap(0UL, c.Map));
    }

    [Fact]
    public void DrawOrderIsTheContract()
    {
        // A golden hash, and the failure it exists for is silent: reordering the
        // generator's draws shifts every subsequent value, so every seed anybody has
        // written down stops reproducing its world. That is a save-breaking change
        // wearing the clothes of a tidy-up, so it should fail the build.
        //
        // If this breaks and the change was DELIBERATE, update the constant and say so
        // in the commit. If it broke by accident, that is the bug.
        ulong actual = StateHash.MixMap(0UL, Generate(Config, 12345UL));
        _output.WriteLine($"golden map hash for seed 12345: {actual}UL");

        Assert.Equal(GoldenMapHash, actual);
    }

    /// <summary>Seed 12345 on the village config. See <see cref="DrawOrderIsTheContract"/>.</summary>
    // RE-TAKEN ONCE, DELIBERATELY (D91): the valley has stone and iron in it now, so its
    // fingerprint changes. That is the golden working, not breaking.
    //
    // What it does NOT mean is that the draw order moved — the seams are APPENDED after
    // every existing draw, and SeamsTests.TheSeamsWereAppendedToTheDrawOrder proves it by
    // generating the same seed with the seam counts set to zero and asserting the founding
    // site, the forage sites, the tree stands and the soil are all identical. Only the new
    // tiles differ.
    //
    // RE-TAKEN AGAIN, DELIBERATELY (`forests-and-gathering.md` slice 1): the valley is wooded
    // now — about 28% of it, against the two stands it had before — so its fingerprint changes.
    //
    // And again this does NOT mean the draw order moved. The woodland is APPENDED after every
    // existing draw, exactly as the seams were, and TheWoodlandWasAppendedToTheDrawOrder proves
    // it by generating the same seed with the coverage set to zero and asserting the founding
    // site, the forage sites, the tree stands and the soil are all identical. Only trees differ.
    //
    // RE-TAKEN A THIRD TIME, DELIBERATELY (D152) — and this one is the kind of change the
    // guard exists to make somebody say out loud. **The two ring-drawn tree stands and the six
    // ring-drawn forage sites are deleted** (step C, D124–D130): food stopped being a fact of
    // the map and became a decision. Those two loops consumed random draws, so removing them
    // shifts every subsequent value — the founding site, the soil, both seams and the woodland
    // are all different now for every seed ever written down.
    //
    // ⚠️ UNLIKE THE PREVIOUS TWO RE-TAKES, THE DRAW ORDER GENUINELY MOVED. The seams (D91) and
    // the woodland were APPENDED, and their own guards prove it. This is deletion from the
    // middle, which is save-breaking by construction — taken on purpose because the sites it
    // removes are gone from the game, and recorded here so that no later reader mistakes it
    // for the appended kind. `MapGenerator` says the same thing at the site of the deletion.
    //
    //   before the seams (D91):       2208871881858546589
    //   before the valley was wooded: 7476686338440514564
    //   before the sites retired:     15355449050208049248
    //
    // ⭐ RE-TAKEN FOR SOIL BECOMING REGIONAL (D178), and this one is the APPENDED kind rather
    // than the deletion above — in fact it is gentler still. **Not one tile of terrain moved.**
    // Soil is drawn at step 5 and the reshaping into regions consumes NO draws, so the river,
    // the woodland, both seams and the founding site are byte-identical for every seed ever
    // written down; only the soil bytes differ, and `MixMap` hashes them.
    //
    // ⚠️ THAT CLAIM IS GUARDED RATHER THAN ASSERTED —
    // `PerSiteYieldTests.MakingSoilRegionalMovedNoOtherTileInTheValley` pins terrain
    // fingerprints taken from `main` BEFORE the change, across three seeds. **That guard is
    // what licenses this hash to move alone.**
    //
    //   before ground was worth going to: 3589830841205379371
    private const ulong GoldenMapHash = 11099415282837858114UL;

    // ---------------------------------------------------------------
    //  Woodland — `specs/forests-and-gathering.md`
    // ---------------------------------------------------------------

    /// <summary>
    /// ⭐ The valley is actually wooded, and the coverage is measured rather than assumed.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>The arithmetic cannot tell you this.</b> `ForestClumpCount` derives how many clumps a
    /// stated coverage takes, but they are dropped independently and overlap, and none may fall
    /// on water or a seam — so the target is a target and the achieved coverage is lower. The
    /// config comment says so; this is what makes that claim checkable.
    /// </para>
    /// <para>
    /// <b>A band, not a number</b>, and deliberately wide. The point of the guard is that the
    /// valley is neither bare (which is the game before this change — two stands, about fifty
    /// tiles) nor solid forest (which leaves nowhere to build). Tightening it to a single
    /// figure would make it a golden by the back door, and it would fail for a jitter change
    /// nobody cares about.
    /// </para>
    /// </remarks>
    [Theory]
    [InlineData(1UL)]
    [InlineData(7UL)]
    [InlineData(12345UL)]
    public void TheValleyIsActuallyWooded(ulong seed)
    {
        SimConfig config = Config;
        SimWorld world = SimFactory.CreatePhase0(config, new InMemoryLogSink(), seed).World;
        GeneratedMap map = world.Map;

        int forest = 0;
        for (int y = 0; y < map.Height; y++)
        {
            for (int x = 0; x < map.Width; x++)
            {
                if (map.TerrainAt(new GridPos(map.MinX + x, map.MinY + y)) == Terrain.Forest)
                {
                    forest++;
                }
            }
        }

        int percent = forest * 100 / (map.Width * map.Height);
        _output.WriteLine($"seed {seed}: {forest} wooded tiles, {percent}% of the valley, "
            + $"from {MapGenerator.ForestClumpCount(config)} clumps targeting "
            + $"{config.ForestCoveragePercent}%");

        Assert.True(percent >= 15, $"The valley is barely wooded ({percent}%) — a gatherer's "
            + "hut would have nowhere to stand.");
        Assert.True(percent <= 50, $"The valley is {percent}% forest — there is nowhere to "
            + "build and every site would wait on a clearing.");
    }

    /// <summary>
    /// ⭐ The woodland was appended to the draw order, so no seed's valley moved for it.
    /// </summary>
    /// <remarks>
    /// <b>The same guard the seams got (D91), and for the same reason.</b> Draw order is the
    /// seed contract: a draw inserted in the middle shifts every subsequent value, so the
    /// river, the sites, the stands, the founding site and the soil would all move for every
    /// seed anybody has written down. Proved by generating the same seed with the coverage set
    /// to zero and asserting everything except the trees is identical.
    /// </remarks>
    [Theory]
    [InlineData(1UL)]
    [InlineData(12345UL)]
    public void TheWoodlandWasAppendedToTheDrawOrder(ulong seed)
    {
        SimConfig config = Config;

        SimWorld wooded = SimFactory.CreatePhase0(config, new InMemoryLogSink(), seed).World;
        SimWorld bare = SimFactory.CreatePhase0(
            config with { ForestCoveragePercent = 0 }, new InMemoryLogSink(), seed).World;

        Assert.Equal(bare.Map.FoundingSite, wooded.Map.FoundingSite);
        Assert.Equal(bare.Map.Soil, wooded.Map.Soil);
    }

    /// <summary>Woodland never takes the stone and iron back out of the valley.</summary>
    /// <remarks>
    /// It is drawn <em>after</em> the seams — it had to be, to keep the draw order — so unlike
    /// the tree stands it paints over open grass only. Without that rule this slice would have
    /// been a balance change hiding inside a worldgen change, which is the trap `PaintSeams`
    /// already documents from the other direction.
    /// </remarks>
    [Fact]
    public void WoodlandDoesNotSwallowTheSeams()
    {
        SimConfig config = Config;

        int withWoods = CountSeams(SimFactory.CreatePhase0(config, new InMemoryLogSink(), 1UL).World);
        int without = CountSeams(SimFactory.CreatePhase0(
            config with { ForestCoveragePercent = 0 }, new InMemoryLogSink(), 1UL).World);

        _output.WriteLine($"seam tiles: {without} bare, {withWoods} wooded");

        Assert.True(without > 0, "This valley has no seams at all, so nothing was tested.");
        Assert.Equal(without, withWoods);
    }

    private static int CountSeams(SimWorld world)
    {
        GeneratedMap map = world.Map;
        int seams = 0;
        for (int y = 0; y < map.Height; y++)
        {
            for (int x = 0; x < map.Width; x++)
            {
                Terrain terrain = map.TerrainAt(new GridPos(map.MinX + x, map.MinY + y));
                if (terrain is Terrain.Rock or Terrain.IronDeposit)
                {
                    seams++;
                }
            }
        }

        return seams;
    }

    [Fact]
    public void NoTwoPlacesInTheValleyShareAName()
    {
        // Joe read the original bug off his own screen: "Nobody is working the berry patch,
        // the southern western thicket, the southern eastern thicket, the southern eastern
        // thicket" — the same phrase twice, because a bearing has eight values and that
        // village had six forage sites. Every one of these names ends up inside a sentence
        // about why somebody walks the way they do, and a name that points at two places
        // answers nothing. That is §1.1, not tidiness.
        //
        // ⭐ THE PROPERTY SURVIVED ITS MECHANISM. Bearings are gone with the thickets, and
        // D124 replaced them with a counter — *gatherer's hut 1, gatherer's hut 2* — until
        // the player can rename them. So the collision this guards can no longer be produced
        // by a seed, and the fifty-seed sweep it used to do proved nothing: with generated
        // sites retired, a founding valley has almost no names in it to collide.
        //
        // It watches the counter instead. **The player is the one who makes duplicates now**
        // — marking the same kind of building over and over is the ordinary way to play —
        // so that is what the fixture does.
        //
        // Still stated as ONE NAME, ONE PLACE rather than one name per building, because of
        // D36's seam: the market is deliberately both a store and a workplace at a single
        // position, so those two sharing "the market" is the model being right rather than a
        // collision. Two names at two different tiles is the thing that cannot happen.
        SimWorld world = SimFactory.CreatePhase0(Config, new InMemoryLogSink(), 12345UL).World;

        int marked = 0;
        foreach (BuildingKind kind in new[]
                 {
                     BuildingKind.GathererHut, BuildingKind.GathererHut, BuildingKind.GathererHut,
                     BuildingKind.WoodcutterHut, BuildingKind.WoodcutterHut,
                     BuildingKind.Granary, BuildingKind.Granary,
                 })
        {
            if (world.Mark(kind, SomewhereFree(world, kind)).Allowed)
            {
                marked++;
            }
        }

        var places = new Dictionary<string, GridPos>();

        foreach (Workplace workplace in world.Workplaces)
        {
            Check(places, workplace.Name, workplace.Position);
        }

        foreach (StoreBuilding store in world.StoreBuildings)
        {
            Check(places, store.Name, store.Position);
        }

        _output.WriteLine($"{marked} marked, {places.Count} distinct names: "
            + string.Join(", ", places.Keys));

        // Anti-vacuity (D7): if the marking did not take, this checks a valley with one
        // building in it and the counter is never asked to disambiguate anything.
        Assert.True(marked >= 5,
            $"Only {marked} of seven buildings could be marked, so nothing here has a "
            + "twin and the numbering is never exercised.");

        static void Check(Dictionary<string, GridPos> places, string name, GridPos at)
        {
            if (places.TryGetValue(name, out GridPos already))
            {
                Assert.True(already == at,
                    $"\"{name}\" names both {already} and {at}. Nothing the village "
                    + "says about either place can be acted on.");
                return;
            }

            places[name] = at;
        }
    }

    /// <summary>Somewhere clear of what is already down to put something else.</summary>
    private static GridPos SomewhereFree(SimWorld world, BuildingKind kind)
    {
        GridPos site = world.Map.FoundingSite;
        for (int radius = 1; radius < 20; radius++)
        {
            for (int dy = -radius; dy <= radius; dy++)
            {
                for (int dx = -radius; dx <= radius; dx++)
                {
                    var at = new GridPos(site.X + dx, site.Y + dy);
                    if (world.CanBuildAt(kind, at).Allowed)
                    {
                        return at;
                    }
                }
            }
        }

        throw new Xunit.Sdk.XunitException($"Nowhere to put a {kind}.");
    }

    [Fact]
    public void NothingIsGeneratedOutsideTheValley()
    {
        for (ulong seed = 1; seed <= 50; seed++)
        {
            GeneratedMap map = Generate(Config, seed);

            // The forage sites and the tree stands were checked here too. They are retired,
            // and the founding site is the one generated position left — which makes this
            // guard narrower and no less load-bearing: a founding site off the map is a
            // valley nobody can live in.
            Assert.True(map.Contains(map.FoundingSite), $"Seed {seed}: founding site is off the map.");
        }
    }

    [Fact]
    public void TheVillageIsNeverFoundedInTheRiver()
    {
        for (ulong seed = 1; seed <= 50; seed++)
        {
            GeneratedMap map = Generate(Config, seed);
            Assert.NotEqual(Terrain.Water, map.TerrainAt(map.FoundingSite));
        }
    }

    /// <summary>
    /// ⭐ Woodland reaches every side of the village — <b>D24's guarantee, re-pointed</b>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This was <c>ForageSitesStaySpread</c>, and D24 is a record of what happens when food
    /// is not spread: the extra patches all went out to the map edges, every home near the
    /// middle competed for the one original patch, and the outskirts had nothing in reach.
    /// </para>
    /// <para>
    /// <b>The patches are retired and the guarantee still matters</b>, because food still
    /// comes from trees — it just comes from a hut the player sites in them. A valley whose
    /// woodland is all on one side is a valley with one place worth living, which is D24's
    /// failure wearing the new system's clothes. So the question moves from *where are the
    /// six sites* to *is there wood on every side of the founding site*.
    /// </para>
    /// </remarks>
    [Fact]
    public void WoodlandReachesEverySideOfTheVillage()
    {
        SimConfig config = Config;

        for (ulong seed = 1; seed <= 50; seed++)
        {
            GeneratedMap map = Generate(config, seed);

            int east = 0, west = 0, north = 0, south = 0;
            for (int y = map.MinY; y < map.MinY + map.Height; y++)
            {
                for (int x = map.MinX; x < map.MinX + map.Width; x++)
                {
                    if (map.TerrainAt(new GridPos(x, y)) != Terrain.Forest)
                    {
                        continue;
                    }

                    if (x > map.FoundingSite.X) east++;
                    if (x < map.FoundingSite.X) west++;
                    if (y > map.FoundingSite.Y) south++;
                    if (y < map.FoundingSite.Y) north++;
                }
            }

            Assert.True(east > 0 && west > 0, $"Seed {seed}: wood is all on one side ({west}W/{east}E).");
            Assert.True(north > 0 && south > 0, $"Seed {seed}: wood is all on one side ({north}N/{south}S).");
        }
    }

    /// <summary>
    /// Every valley leaves the village inside the walk its economy budgets for.
    /// </summary>
    /// <remarks>
    /// <b>The subject changed and the guarantee did not.</b> It measured each home against
    /// the nearest generated forage site; there are none, so it measures against the nearest
    /// place anyone actually gathers — which in a warm start is the gatherer's hut. Same
    /// question, asked of the thing that answers it now.
    /// <para>
    /// ⚠️ The budget is a budget rather than a fence since `forests-and-gathering.md §3.2`,
    /// so this is no longer enforced by `ChooseSite` refusing. **That makes it worth
    /// asserting more, not less**: nothing else would notice a valley where the founding
    /// layout quietly puts every family beyond what the food economy pays for.
    /// </para>
    /// </remarks>
    [Fact]
    public void EveryValleyMeetsTheEconomysDistanceBudget()
    {
        SimConfig config = Config;
        int budget = VillageEconomy.MaxHomeToWorkTiles(config);

        int seedsBeyond = 0;
        int seedsWithin = 0;
        int worstAnywhere = 0;
        const int Seeds = 30;

        for (ulong seed = 1; seed <= Seeds; seed++)
        {
            SimWorld world = SimFactory.CreatePhase0(config, new InMemoryLogSink(), seed).World;

            var walks = new List<int>();
            foreach (Household household in world.Households)
            {
                int nearest = int.MaxValue;
                foreach (Workplace workplace in world.Workplaces)
                {
                    if (workplace.Kind != JobKind.Forager || workplace.IsSite)
                    {
                        continue;
                    }

                    // ⭐ BY WALKING, NOT WITH A RULER, AND THIS IS THE THIRD SITE OF THAT BUG.
                    // D111 found `ChooseSite` scoring candidate tiles on `ManhattanDistanceTo`
                    // while every real journey goes round the river, and D121 fixed it there —
                    // and this guard, which exists to check that method's output, went on
                    // measuring with the ruler D121 deleted. **A home and the guard on it were
                    // grading different worlds**, which is the failure `Household` warns about
                    // in as many words: *"or a home's two scores measure different worlds."*
                    int cost = world.TravelCost.Cost(household.Home(), workplace.Position);
                    if (cost != TravelCostField.Unreachable)
                    {
                        nearest = Math.Min(nearest, cost / TravelCostField.BaseTileCost);
                    }
                }

                Assert.True(nearest != int.MaxValue,
                    $"Seed {seed}: {household.Name} cannot walk to any gatherer's hut at all.");

                walks.Add(nearest);
                worstAnywhere = Math.Max(worstAnywhere, nearest);
            }

            Assert.NotEmpty(walks);
            walks.Sort();
            int typical = walks[walks.Count / 2];

            if (walks[^1] > budget)
            {
                seedsBeyond++;
            }

            // ⭐ THE TYPICAL FAMILY, NOT EVERY FAMILY — because the fence is gone (D120) and a
            // budget that refused would be the fence back. `forests-and-gathering.md §3.2`:
            // *"Building beyond the budget is allowed, warned about, and genuinely costs
            // food."* One household at nine tiles against a budget of eight is that rule
            // working, and this guard failed seed 9 for it.
            //
            // **What is still worth asserting is the remark this test already carried:**
            // *"nothing else would notice a valley where the founding layout quietly puts
            // every family beyond what the food economy pays for."* That is a claim about the
            // village, so it is measured on the village — the median walk — and the outliers
            // are the design rather than the defect.
            _output.WriteLine($"seed {seed,2}: typical {typical,2}, worst {walks[^1],2}, "
                + $"{walks.Count} households");

            if (typical <= budget)
            {
                seedsWithin++;
            }

            // ⭐ STRANDED, NOT ONE TILE OVER — because the fence is gone (D120) and a budget
            // that refuses would be the fence back. `forests-and-gathering.md §3.2`:
            // *"Building beyond the budget is allowed, warned about, and genuinely costs
            // food."* This guard failed seed 9 for a village nine tiles from its hut against a
            // budget of eight, which is that rule working exactly as written.
            //
            // **Measured before choosing the bar:** across 30 seeds the typical walk runs 2 to
            // 9 tiles and only three seeds are over 8 — so what wants catching is a valley that
            // put the village *miles* from its food, not one that cost it a tile.
            Assert.True(typical <= budget * 2,
                $"Seed {seed}: the typical family walks {typical} tiles to work against a "
                + $"budget of {budget}. That is not a long walk, it is a village the generator "
                + "sited away from its own food.");

            // And an outlier is an outlier rather than a different kind of valley.
            Assert.True(walks[^1] <= budget * 3,
                $"Seed {seed}: {walks[^1]} tiles to work against a budget of {budget} is not a "
                + "long walk, it is a family the generator stranded.");
        }

        _output.WriteLine(
            $"{Seeds} seeds, budget {budget}: {seedsWithin} sit within it, {seedsBeyond} had at "
            + $"least one family beyond it; the longest walk anywhere was {worstAnywhere} tiles.");

        // ⭐ AND THE GENERATOR AS A WHOLE STILL AIMS INSIDE THE BUDGET. One valley a tile over
        // is the design; most valleys a tile over would mean the economy is derived against a
        // distance the generator no longer produces, and nothing else in the suite would say
        // so. Measured at 27 of 30 — a three-quarters bar leaves real headroom and still fires
        // long before the derivation and the map have drifted apart.
        Assert.True(seedsWithin * 4 >= Seeds * 3,
            $"Only {seedsWithin} of {Seeds} valleys put the typical family inside the "
            + $"{budget}-tile budget the food economy is derived against.");
    }

    /// <summary>How long each valley is watched for. See the note in the body — this is a trade.</summary>
    /// <remarks>
    /// <para>
    /// <b>⏱️ SHORTENED FROM 200 ON JOE'S CALL, AND IT IS A REAL TRADE RATHER THAN A TIDY-UP.</b>
    /// Twelve valleys × 200 years was <b>five of the suite's eleven minutes on its own</b> — the
    /// single most expensive thing in the project — and the suite is the tax on every change.
    /// All twelve seeds are kept, because <em>which</em> valleys are watched is what this guard
    /// is for; what shortens is how long each is watched.
    /// </para>
    /// <para>
    /// <b>⚠️ What it costs, said plainly:</b> this guard catches changes that help eleven
    /// valleys and kill one, and it has done so twice — D103's seed 11 aged out to nothing
    /// <em>by year 160</em>, and D110's died by year 106. <b>The second would still be caught at
    /// 120 years and the first might not.</b> A slow ageing-out is exactly the shape that needs
    /// the longest horizon, so if a change is ever suspected of causing one, <b>this number is
    /// the first thing to raise</b> — temporarily and deliberately.
    /// </para>
    /// </remarks>
    private const int SeedWatchYears = 120;

    [Fact]
    public void EverySeedProducesAValleyAVillageSurvivesIn()
    {
        // THE property test, and the thing a generated world needs that a hand-placed
        // one never did: hand-placement was checked once by a human, and generation has
        // to be right for valleys nobody has ever looked at.
        SimConfig config = Config;
        var results = new List<string>();

        for (ulong seed = 1; seed <= 12; seed++)
        {
            var sink = new InMemoryLogSink();
            SimLoop loop = SimFactory.CreatePhase0(config, sink, seed);

            int lowest = int.MaxValue;
            int peak = 0;
            for (int year = 1; year <= SeedWatchYears; year++)
            {
                loop.Step(config.TicksPerYear);
                peak = Math.Max(peak, loop.World.Population);
                if (year >= 40)
                {
                    lowest = Math.Min(lowest, loop.World.Population);
                }
            }

            results.Add($"seed {seed,2}: peak {peak,3}, low {lowest,3}, final {loop.World.Population,3}");

            // ⭐ D111's PROMISE, GUARDED AT LAST. `MarkHome` skips `CanBuildAt` on the
            // written grounds that `ChooseSite` has already found reachable ground — and in
            // seed 11 it demonstrably had not, siting a house on the far bank that no builder
            // could ever walk to and freezing that village's whole future (D110). `MarkHome`
            // logs a warning when the promise breaks; **nothing was reading it.**
            //
            // Free to check here, since these are the twelve valleys the promise has to hold
            // in and they are already being run.
            foreach (LogEntry entry in sink.Entries)
            {
                Assert.False(
                    entry.Level >= LogLevel.Warn
                        && entry.Message.Contains("no route to it", StringComparison.Ordinal),
                    $"Seed {seed} sited a house nobody can walk to: {entry.Message}");
            }

            // ⭐ THE VALLEY MUST NOT KILL THE VILLAGE. Every one of the twelve is still
            // standing at 120 years — finals run 6 to 49 — and a generated valley that wiped
            // one out would be the generator's fault rather than the player's, which is the
            // whole reason this property test exists.
            Assert.True(loop.World.Population >= config.StartingPopulation,
                $"Seed {seed} died out — finished at {loop.World.Population}. " +
                $"({string.Join("; ", results)})");

            // ⭐ AND IT MUST BE ABLE TO GROW ONE. Peak rather than final, and D143 is why.
            //
            // ⛔ THIS USED TO ASSERT THE SLOPE — `Population * 2 >= peak`, *"it must not be
            // halfway out the door"* — on the reasoning that a village which peaks and then
            // dwindles with nobody starved is a village that is finished. **Joe's ruling
            // retires that claim outright:** *"an unattended village should die out. The user
            // needs to play the game at some point."* Nobody sites a building or paints a tile
            // in any of these twelve runs, so dwindling is the game working, and the guard was
            // measuring how long a valley coasts rather than how good a valley it is.
            //
            // **It was also not one bad seed.** Measured across the twelve, SIX fail the slope
            // — 49→15, 49→13, 37→8, 40→6, 37→6, 49→17 — which is what settles it as the wrong
            // claim rather than a seed to investigate.
            //
            // What survives is the question a MAP-generation guard should be asking: *can a
            // village live here at all?* Peaks run 30 to 49, so twenty has real headroom and
            // still fires on a valley too poor, too wooded or too cut-up to support a
            // settlement — which is the defect this arm has actually caught twice (D103, D110).
            // ⛔⛔ TWENTY → TWELVE (D262, Joe): *"the user must build more forests and huts to grow."*
            // **A gathering hut seats two now, and an UNATTENDED village never builds a second
            // one** — so this guard no longer asks "can a village thrive here by itself", which is
            // a promise the game has deliberately withdrawn. It asks the question a
            // map-generation guard should: **can a village live here at all?**
            //
            // ⭐ Measured after the cap: peaks of 26, 32 and 18 where they used to run 30 to 49.
            // **Twelve keeps real headroom under the poorest valley measured** and still fires on
            // ground too thin, too wooded or too cut-up to settle — the defect this arm has
            // actually caught twice (D103, D110), where peaks sit barely above the founding four.
            Assert.True(peak >= 12,
                $"Seed {seed} never grew a village — it peaked at {peak} from "
                + $"{config.StartingPopulation} founders, so this valley cannot support one. "
                + $"({string.Join("; ", results)})");
        }

        foreach (string line in results)
        {
            _output.WriteLine(line);
        }
    }

    // ---------------------------------------------------------------
    //  Water you have to go round — specs/pathfinding-and-water.md (D40)
    // ---------------------------------------------------------------

    [Fact]
    public void NobodyEverStandsOnWater()
    {
        // The regression this whole slice exists to prevent, asserted every tick
        // rather than inferred from the village surviving.
        SimConfig config = Config;
        SimLoop loop = SimFactory.CreatePhase0(config, new InMemoryLogSink(), seedOverride: 1UL);

        for (int i = 0; i < config.TicksPerYear * 60; i++)
        {
            loop.StepOnce();

            foreach (Villager villager in loop.World.Villagers)
            {
                if (!villager.Alive)
                {
                    continue;
                }

                Assert.NotEqual(Terrain.Water, loop.World.Map.TerrainAt(villager.Position));
            }
        }
    }

    [Fact]
    public void NothingIsEverBuiltOnWater()
    {
        // Homes, workplaces and stores alike. A building in the river is unreachable
        // by construction (TerrainCostField refuses to serve one), so this failing
        // would show up later as a village that mysteriously cannot make firewood —
        // which is exactly how it did show up on seed 1.
        SimConfig config = Config;

        for (ulong seed = 1; seed <= 20; seed++)
        {
            SimLoop loop = SimFactory.CreatePhase0(config, new InMemoryLogSink(), seed);
            loop.Step(config.TicksPerYear * 60);
            SimWorld world = loop.World;

            foreach (Workplace workplace in world.Workplaces)
            {
                Assert.NotEqual(Terrain.Water, world.Map.TerrainAt(workplace.Position));
            }

            foreach (StoreBuilding store in world.StoreBuildings)
            {
                Assert.NotEqual(Terrain.Water, world.Map.TerrainAt(store.Position));
            }

            foreach (Household household in world.Households)
            {
                // Only houses that stand. One being built is a workplace, and the loop
                // above has already checked that none of those is in the river (D102).
                if (household.HasHome)
                {
                    Assert.NotEqual(Terrain.Water, world.Map.TerrainAt(household.Home()));
                }
            }
        }
    }

    [Fact]
    public void EveryVillageCanReachItsOwnBuildings()
    {
        // §6 of the pathfinding spec: until bridges exist (D40) the generator owes the
        // village a valley it can live in. A warehouse on the far bank is as useless as one
        // under water, and the difference is invisible on the map.
        SimConfig config = Config;

        for (ulong seed = 1; seed <= 20; seed++)
        {
            SimWorld world = SimFactory.CreatePhase0(config, new InMemoryLogSink(), seed).World;
            GridPos home = world.Households[0].Home();

            foreach (StoreBuilding store in world.StoreBuildings)
            {
                Assert.True(world.TravelCost.CanReach(home, store.Position),
                    $"Seed {seed}: {store.Name} is cut off from the village.");
            }

            int reachableForage = 0;
            foreach (Workplace workplace in world.Workplaces)
            {
                if (workplace.Kind == JobKind.Forager
                    && world.TravelCost.CanReach(home, workplace.Position))
                {
                    reachableForage++;
                }
            }

            Assert.True(reachableForage > 0, $"Seed {seed}: no forage site is reachable at all.");
        }
    }

    /// <summary>⭐ Crossing the river costs more than the crow flies.</summary>
    /// <remarks>
    /// <para>
    /// <b>⚠️ IT USED TO PIN SEED 1 AND HOPE, AND THE HOPE RAN OUT.</b> It measured every
    /// workplace from one home and demanded that at least one route detour — which was true of
    /// seed 1 while the generator happened to put a building across the water, and stopped
    /// being true when the thickets retired and every workplace became something the player
    /// sites. The guard then reported *"either the terrain is not being read, or this seed has
    /// no water in the way"*, and it was the second — **a true statement about seed 1's layout
    /// wearing the costume of a broken cost field.**
    /// </para>
    /// <para>
    /// <b>So it constructs the crossing instead of shopping for one.</b> Find a run of water
    /// with dry land on both banks and walk between those two tiles: that is the claim — *water
    /// is impassable and every journey goes round it* (D40) — asked directly of the cost field,
    /// with nothing riding on where a building happened to land. Same lesson as this session's
    /// work-ground fixture: a guard that searches for its own precondition is a guard that
    /// reports the search.
    /// </para>
    /// <para>
    /// Its anti-vacuity twin is <see cref="WithNoRiverEveryCostIsTheStraightLine"/> below —
    /// without that, a field quietly returning Manhattan distance everywhere would pass this.
    /// </para>
    /// </remarks>
    [Fact]
    public void AWalkAcrossTheRiverIsLongerThanTheStraightLine()
    {
        SimConfig config = Config;
        SimWorld world = SimFactory.CreatePhase0(config, new InMemoryLogSink(), seedOverride: 1UL).World;

        Assert.True(
            OppositeBanks(world, out GridPos west, out GridPos east),
            "This valley has no river with dry land on both banks, so there is nothing to cross.");

        int path = world.TravelCost.Cost(west, east);
        int straight = west.ManhattanDistanceTo(east) * TravelCostField.BaseTileCost;

        _output.WriteLine(
            $"{west} to {east} across the water: "
            + (path == TravelCostField.Unreachable ? "no way round at all" : $"costs {path}")
            + $", against {straight} as the crow flies.");

        // Unreachable counts: a river with no way round is the strongest form of the claim.
        Assert.True(path == TravelCostField.Unreachable || path > straight,
            $"Walking from {west} to {east} straight through the river cost {path}, the same as "
            + "the crow flies — the terrain is not being read.");
    }

    /// <summary>Two dry tiles with a run of water between them, on one row.</summary>
    private static bool OppositeBanks(SimWorld world, out GridPos west, out GridPos east)
    {
        for (int y = world.Map.MinY; y < world.Map.MinY + world.Map.Height; y++)
        {
            for (int x = world.Map.MinX + 1; x < world.Map.MinX + world.Map.Width - 1; x++)
            {
                if (world.Map.TerrainAt(new GridPos(x, y)) != Terrain.Water
                    || world.Map.TerrainAt(new GridPos(x - 1, y)) == Terrain.Water)
                {
                    continue;
                }

                int end = x;
                while (end < world.Map.MinX + world.Map.Width - 1
                    && world.Map.TerrainAt(new GridPos(end, y)) == Terrain.Water)
                {
                    end++;
                }

                if (world.Map.TerrainAt(new GridPos(end, y)) == Terrain.Water)
                {
                    continue;
                }

                west = new GridPos(x - 1, y);
                east = new GridPos(end, y);
                return true;
            }
        }

        west = default;
        east = default;
        return false;
    }

    [Fact]
    public void WithNoRiverEveryCostIsTheStraightLine()
    {
        // The anti-vacuity twin (D7). If the field were broken in the other direction —
        // always going the long way round, or always reporting unreachable — the test
        // above would still pass. This pins the other end: with nothing in the way, a
        // real path and plain distance must agree exactly.
        SimConfig config = Config with { RiverWidthTiles = 0 };
        SimWorld world = SimFactory.CreatePhase0(config, new InMemoryLogSink(), seedOverride: 1UL).World;

        GridPos from = world.Households[0].Home();

        foreach (Workplace workplace in world.Workplaces)
        {
            int path = world.TravelCost.Cost(from, workplace.Position);
            int straight = from.ManhattanDistanceTo(workplace.Position) * TravelCostField.BaseTileCost;

            Assert.Equal(straight, path);
        }
    }

    [Fact]
    public void AreTheseValleysWorthPlayingTwice()
    {
        // Not an assertion — a contact sheet. The one question about generated worlds
        // that no test can answer is whether they are interesting, and the honest
        // thing is to put them where a human will look rather than declare it done on
        // the strength of the property tests passing (spec §9).
        SimConfig config = Config;

        for (ulong seed = 1; seed <= 3; seed++)
        {
            SimWorld world = SimFactory.CreatePhase0(config, new InMemoryLogSink(), seed).World;
            _output.WriteLine($"---- seed {seed} ----");
            _output.WriteLine(Draw(world, radius: 14));
            _output.WriteLine(string.Empty);
        }
    }

    /// <summary>A small ASCII window on the valley around the settlement.</summary>
    private static string Draw(SimWorld world, int radius)
    {
        GridPos centre = world.Map.FoundingSite;
        var text = new StringBuilder();

        for (int y = centre.Y - radius; y <= centre.Y + radius; y++)
        {
            for (int x = centre.X - radius * 2; x <= centre.X + radius * 2; x++)
            {
                var here = new GridPos(x, y);
                text.Append(SymbolAt(world, here));
            }

            text.AppendLine();
        }

        text.AppendLine("  . grass   ~ water   \" forest   F forage   T stand   h home   G granary   S warehouse   M market");
        return text.ToString();
    }

    private static char SymbolAt(SimWorld world, GridPos here)
    {
        foreach (StoreBuilding building in world.StoreBuildings)
        {
            if (building.Position == here)
            {
                return building.Kind switch
                {
                    StoreKind.Granary => 'G',
                    StoreKind.Warehouse => 'S',
                    _ => 'M',
                };
            }
        }

        foreach (Workplace workplace in world.Workplaces)
        {
            if (workplace.Position == here)
            {
                return workplace.Kind switch
                {
                    JobKind.Forager => 'F',
                    JobKind.Forester => 'T',
                    JobKind.Woodcutter => 'W',
                    _ => 'M',
                };
            }
        }

        foreach (Household household in world.Households)
        {
            if (household.Home() == here)
            {
                return 'h';
            }
        }

        return world.Map.TerrainAt(here) switch
        {
            Terrain.Water => '~',
            Terrain.Forest => '"',
            _ => '.',
        };
    }
}

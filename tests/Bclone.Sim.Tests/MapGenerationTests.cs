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
    //   before the seams (D91):     2208871881858546589
    //   before the valley was wooded: 7476686338440514564
    private const ulong GoldenMapHash = 15355449050208049248UL;

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
        Assert.Equal(bare.Map.ForageSites, wooded.Map.ForageSites);
        Assert.Equal(bare.Map.TreeStands, wooded.Map.TreeStands);
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
        // Joe read this off his own screen: "Nobody is working the berry patch, the
        // southern western thicket, the southern eastern thicket, the southern eastern
        // thicket" — the same phrase twice, because a bearing has eight values and this
        // village has six forage sites. Every one of these names ends up inside a
        // sentence about why somebody walks the way they do, and a name that points at
        // two places answers nothing. That is §1.1, not tidiness.
        //
        // Stated as ONE NAME, ONE PLACE rather than as one name per building, and the
        // difference is D36's seam: the market is deliberately both a store and a
        // workplace at a single position, so those two sharing "the market" is the
        // model being right rather than a collision. Two names at two different tiles
        // is the thing that cannot happen.
        //
        // Fifty seeds rather than the usual twelve: this is a collision test, and
        // collisions are exactly the case a small sample misses.
        int seedsThatNeededTheTieBreak = 0;

        for (ulong seed = 1; seed <= 50; seed++)
        {
            SimWorld world = SimFactory.CreatePhase0(Config, new InMemoryLogSink(), seed).World;

            var places = new Dictionary<string, GridPos>();
            bool tieBroken = false;

            foreach (Workplace workplace in world.Workplaces)
            {
                Check(seed, places, workplace.Name, workplace.Position);
                tieBroken |= workplace.Name.Contains("far", System.StringComparison.Ordinal);
            }

            foreach (StoreBuilding store in world.StoreBuildings)
            {
                Check(seed, places, store.Name, store.Position);
            }

            if (tieBroken)
            {
                seedsThatNeededTheTieBreak++;
            }
        }

        // Anti-vacuity (D7), and it is the whole point here: if no seed ever puts two
        // sites on the same bearing then this test passes over a generator that has no
        // collisions to resolve, and the code that resolves them is dead. The bug Joe
        // found was on seed 12345, so at least some seeds must still need the tie-break.
        _output.WriteLine(
            $"{seedsThatNeededTheTieBreak} of 50 valleys put two places on one bearing.");

        Assert.True(seedsThatNeededTheTieBreak > 0,
            "No valley in fifty needed a name disambiguated, so this guard is watching a " +
            "collision that never happens and the code that resolves them is never run.");

        static void Check(ulong seed, Dictionary<string, GridPos> places, string name, GridPos at)
        {
            if (places.TryGetValue(name, out GridPos already))
            {
                Assert.True(already == at,
                    $"Seed {seed}: \"{name}\" names both {already} and {at}. Nothing the village " +
                    "says about either place can be acted on.");
                return;
            }

            places[name] = at;
        }
    }

    [Fact]
    public void ForagingAndFellingAreNamedAsDifferentKindsOfPlace()
    {
        // The half of the bug that was worse than the repeat. Every site past the first
        // was called a *thicket* whatever it was, so a tree stand and a berry patch were
        // named alike — and a player told nobody was working "the southern eastern
        // thicket" could not tell whether the village was short of food or of timber.
        SimWorld world = SimFactory.CreatePhase0(Config, new InMemoryLogSink(), 12345UL).World;

        int thickets = 0;
        int woods = 0;

        foreach (Workplace workplace in world.Workplaces)
        {
            if (workplace.Kind == JobKind.Forager)
            {
                Assert.DoesNotContain("wood", workplace.Name, System.StringComparison.Ordinal);
                thickets++;
            }

            if (workplace.Kind == JobKind.Forester)
            {
                Assert.DoesNotContain("thicket", workplace.Name, System.StringComparison.Ordinal);
                woods++;
            }
        }

        var said = new List<string>();
        foreach (Workplace workplace in world.Workplaces)
        {
            said.Add(workplace.Name);
        }

        _output.WriteLine(
            $"{thickets} places to forage and {woods} to fell, none of them named alike:");
        _output.WriteLine("  " + string.Join(", ", said));

        // Anti-vacuity (D7): a village with one of each proves nothing about the naming.
        Assert.True(thickets > 1 && woods > 1,
            "The fixture has too few sites for this to be testing anything.");
    }

    [Fact]
    public void NothingIsGeneratedOutsideTheValley()
    {
        for (ulong seed = 1; seed <= 50; seed++)
        {
            GeneratedMap map = Generate(Config, seed);

            foreach (GridPos site in map.ForageSites)
            {
                Assert.True(map.Contains(site), $"Seed {seed}: forage site {site} is off the map.");
            }

            foreach (GridPos stand in map.TreeStands)
            {
                Assert.True(map.Contains(stand), $"Seed {seed}: tree stand {stand} is off the map.");
            }

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

    [Fact]
    public void ForageSitesStaySpread()
    {
        // D24, which is a record of what happens when they do not: the extra sites all
        // went out to the map edges, every home near the middle competed for the one
        // original patch, and tightening catchment left central families idle beside a
        // full thicket while the outskirts had nothing in reach.
        //
        // The generator guarantees this by construction — evenly spaced slots with
        // jitter, rather than free angles — so this asserts the guarantee rather than
        // hoping the draw was kind.
        SimConfig config = Config;

        for (ulong seed = 1; seed <= 50; seed++)
        {
            GeneratedMap map = Generate(config, seed);

            int east = 0, west = 0, north = 0, south = 0;
            foreach (GridPos site in map.ForageSites)
            {
                if (site.X > map.FoundingSite.X) east++;
                if (site.X < map.FoundingSite.X) west++;
                if (site.Y > map.FoundingSite.Y) south++;
                if (site.Y < map.FoundingSite.Y) north++;
            }

            Assert.True(east > 0 && west > 0, $"Seed {seed}: sites are all on one side ({west}W/{east}E).");
            Assert.True(north > 0 && south > 0, $"Seed {seed}: sites are all on one side ({north}N/{south}S).");
        }
    }

    [Fact]
    public void EveryValleyMeetsTheEconomysDistanceBudget()
    {
        // The guarantee that replaces hand-placement's implicit one, asserted directly
        // rather than through survival — so a failure says WHICH constraint broke
        // rather than "the village died, good luck".
        SimConfig config = Config;
        int budget = VillageEconomy.MaxHomeToWorkTiles(config);

        for (ulong seed = 1; seed <= 30; seed++)
        {
            SimWorld world = SimFactory.CreatePhase0(config, new InMemoryLogSink(), seed).World;

            foreach (Household household in world.Households)
            {
                int nearest = int.MaxValue;
                foreach (GridPos site in world.Map.ForageSites)
                {
                    nearest = Math.Min(nearest, household.Home().ManhattanDistanceTo(site));
                }

                Assert.True(nearest <= budget,
                    $"Seed {seed}: {household.Name} is {nearest} tiles from work, budget {budget}.");
            }
        }
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

            Assert.True(loop.World.Population >= config.StartingPopulation,
                $"Seed {seed} died out — finished at {loop.World.Population}. " +
                $"({string.Join("; ", results)})");

            // ⭐ AND IT MUST NOT BE HALFWAY OUT THE DOOR EITHER — which is what compensates for
            // the shorter horizon above.
            //
            // The failure this guard exists for is a village that AGES OUT: it peaks, then
            // dwindles with nobody starved and nobody frozen, and is simply gone. D103's seed 11
            // peaked at 32 and reached nothing by year 160; watching only 120 years, "is anybody
            // left?" could still answer yes while the village was plainly finished. **So notice
            // the slope rather than waiting for the end.**
            //
            // Half of peak is deliberately generous: every healthy seed today ends within a
            // couple of its own peak (48 → 47), so this has better than two-to-one headroom and
            // will not go off because a village had a hard decade.
            Assert.True(loop.World.Population * 2 >= peak,
                $"Seed {seed} is dwindling — it peaked at {peak} and finished at "
                + $"{loop.World.Population}, so it is on its way out rather than living. "
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
        // village a valley it can live in. A shed on the far bank is as useless as one
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

    [Fact]
    public void AWalkAcrossTheRiverIsLongerThanTheStraightLine()
    {
        // And the anti-vacuity twin below it — without that, a field that quietly
        // returned Manhattan distance everywhere would pass this happily.
        SimConfig config = Config;
        SimWorld world = SimFactory.CreatePhase0(config, new InMemoryLogSink(), seedOverride: 1UL).World;

        GridPos from = world.Households[0].Home();
        int detoured = 0;

        foreach (Workplace workplace in world.Workplaces)
        {
            int path = world.TravelCost.Cost(from, workplace.Position);
            if (path == TravelCostField.Unreachable)
            {
                detoured++;
                continue;
            }

            int straight = from.ManhattanDistanceTo(workplace.Position) * TravelCostField.BaseTileCost;
            Assert.True(path >= straight, "A path cannot be shorter than the straight line.");

            if (path > straight)
            {
                detoured++;
            }
        }

        _output.WriteLine($"{detoured} of {world.Workplaces.Count} places cost more than the crow flies.");
        Assert.True(detoured > 0,
            "Every route on this seed was a straight line, so the river is costing nothing — " +
            "either the terrain is not being read, or this seed has no water in the way.");
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

        text.AppendLine("  . grass   ~ water   \" forest   F forage   T stand   h home   G granary   S shed   M market");
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
                    StoreKind.Shed => 'S',
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

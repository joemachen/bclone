using System.Text.Json.Nodes;
using Bclone.Sim.Config;
using Bclone.Sim.Core;
using Bclone.Sim.Determinism;
using Bclone.Sim.Logging;
using Bclone.Sim.Persistence;
using Bclone.Sim.Systems;
using Bclone.Sim.World;
using Xunit;
using Xunit.Abstractions;

namespace Bclone.Sim.Tests;

/// <summary>
/// House tiers (`specs/house-tiers.md`, D716–D718). <b>Piece 1, the generation</b> (§14.1): the family tree that gates
/// both rungs (§4.4 idea A, Joe: <i>"I love the family tree unlock idea!"</i>) needs a villager to know which generation
/// in the valley they are — founders and arriving nomads the first, a child one past the elder of its two parents.
/// </summary>
public sealed class HouseTierTests
{
    private readonly ITestOutputHelper _output;

    public HouseTierTests(ITestOutputHelper output) => _output = output;

    private static SimConfig Village => VillageFixtures.Village;

    /// <summary>Everybody the founding puts in the valley is its first generation.</summary>
    [Fact]
    public void TheFoundersAreTheFirstGeneration()
    {
        foreach (SimConfig config in new[] { Village, ShippedConfig.Load() })
        {
            SimWorld world = SimFactory.CreatePhase0(config, new InMemoryLogSink()).World;
            Assert.NotEmpty(world.Villagers);
            Assert.All(world.Villagers, v => Assert.Equal(1, v.Generation));
        }
    }

    /// <summary>
    /// ⭐ Every child born in sixty fixture years is one generation past the elder of the couple it was born to, read off the
    /// household the tick before — and the run reaches the founders' great-grandchildren, so a rule that only ever wrote 2
    /// (a child of founders) cannot pass.
    /// </summary>
    [Fact]
    public void AChildIsOneGenerationPastTheElderOfItsParents()
    {
        SimLoop loop = SimFactory.CreatePhase0(Village with { StartingHouseholds = 3 }, new InMemoryLogSink());
        SimWorld world = loop.World;
        int births = 0;
        int deepest = 1;
        int ticks = 60 * world.Config.TicksPerYear;
        for (int t = 0; t < ticks; t++)
        {
            Dictionary<int, int> eldest = ElderParentGenerationByHousehold(world);
            int before = world.Villagers.Count;
            loop.StepOnce();
            for (int i = before; i < world.Villagers.Count; i++)
            {
                Villager child = world.Villagers[i];
                if (child.ArrivedAtTick != 0UL || child.Founder)
                {
                    Assert.Equal(1, child.Generation);
                    continue;
                }

                Assert.True(eldest.TryGetValue(child.HouseholdId, out int parents),
                    $"{child.FullName} was born to a household with no couple the tick before");
                Assert.Equal(parents + 1, child.Generation);
                births++;
                deepest = Math.Max(deepest, child.Generation);
            }
        }

        _output.WriteLine($"{births} births in sixty years; the deepest generation born is {deepest}");
        Assert.True(births > 0, "the pose needs births");
        Assert.True(deepest >= 4, $"the pose needs the founders' great-grandchildren (deepest was {deepest})");
    }

    /// <summary>
    /// Saved and loaded with the villager; ⛔ not hashed — a statistic the gates count, D676's shape, so no golden moves for
    /// it. The gap is asserted here so nobody "fixes" it unread: two villages differing only in a generation hash alike.
    /// </summary>
    [Fact]
    public void TheGenerationIsSavedAndNotHashed()
    {
        SimLoop loop = SimFactory.CreatePhase0(Village with { StartingHouseholds = 3 }, new InMemoryLogSink());
        loop.Step(30 * loop.World.Config.TicksPerYear);
        Villager child = loop.World.Villagers.First(v => v.Generation >= 2);

        JsonObject document = SaveGame.Capture(loop, "test", "2026-11-08 12:00", "test");
        SimLoop loaded = SaveGame.Load(JsonNode.Parse(SaveFile.TextOf(document))!.AsObject(), loop.World.Config, null);
        Assert.Equal(
            loop.World.Villagers.Select(v => v.Generation),
            loaded.World.Villagers.Select(v => v.Generation));

        ulong before = StateHash.Compute(loop.World);
        child.Generation += 7;
        Assert.Equal(before, StateHash.Compute(loop.World));
    }

    /// <summary>
    /// Format 13 since the generation (`house-tiers.md §7`): a format-12 save — Joe's Elderwood and Stonebridge, 2026-11-05 to
    /// 11-08 — is refused naming its build, never half-loaded with every villager the first generation.
    /// </summary>
    [Fact]
    public void AFormatTwelveSaveIsRefused()
    {
        SimConfig config = Village;
        SimLoop loop = SimFactory.CreatePhase0(config, new InMemoryLogSink());
        Assert.Equal(13, SaveGame.Format);

        string folder = Path.Combine(Path.GetTempPath(), "bclone-house-tier-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);
        try
        {
            JsonObject document = SaveGame.Capture(loop, "0.0.1", "2026-11-08 12:00", NewGame.ShareCode(
                config, NewGame.Defaults(config, loop.World.Seed.ToString(System.Globalization.CultureInfo.InvariantCulture))));
            document["format"] = 12;
            string path = Path.Combine(folder, $"format-twelve{SaveFile.Extension}");
            SaveFile.Write(path, document);
            SaveOpened opened = SaveFile.Open(path, config);
            Assert.Null(opened.Loop);
            Assert.Equal("Saved by build 0.0.1, whose saves this build cannot read.", opened.Refusal);
        }
        finally
        {
            Directory.Delete(folder, recursive: true);
        }
    }

    // ---------------------------------------------------------------
    // Piece 2 — the rows and the burn (§14.2): three dwelling rows, one door (`SimWorld.HomeRowOf`).
    // ---------------------------------------------------------------

    private static List<Household> Housed(SimWorld world) =>
        world.Households.Where(h => h.HasHome && world.LivingMembersOf(h) > 0).ToList();

    /// <summary>
    /// ⭐ The identity that keeps every golden honest: the cabin's winter is the two old keys, and the spread burns exactly
    /// <c>firewood_per_winter_day</c> on every beat — on the shipped config and the fixture.
    /// </summary>
    [Fact]
    public void TheCabinBurnsTodaysFirewoodOnEveryBeat()
    {
        foreach (SimConfig config in new[] { Village, ShippedConfig.Load() })
        {
            BuildingRow cabin = config.BuildingRows.Single(r => r.Id == (int)BuildingKind.Home);
            Assert.Equal(VillageEconomy.FirewoodPerHouseholdPerWinter(config), cabin.WinterFirewood);
            int beats = (config.DaysPerSeason + config.FirewoodBurnIntervalDays - 1) / config.FirewoodBurnIntervalDays;
            for (int k = 0; k < beats; k++)
            {
                Assert.Equal(config.FirewoodPerWinterDay, HearthSystem.BurnOnBeat(cabin.WinterFirewood, k, beats));
            }
        }

        Assert.Equal(24, ShippedConfig.Load().BuildingRows.Single(r => r.Id == (int)BuildingKind.Home).WinterFirewood);
    }

    /// <summary>Any winter is reachable now (D189's grid gone): every one sums to itself, no beat over ⌈W/B⌉.</summary>
    [Fact]
    public void AnyWinterIsBurnedEvenlyAndWhole()
    {
        SimConfig shipped = ShippedConfig.Load();
        Assert.Equal(12, shipped.BuildingRows.Single(r => r.Id == (int)BuildingKind.Cottage).WinterFirewood);
        Assert.Equal(5, shipped.BuildingRows.Single(r => r.Id == (int)BuildingKind.Manor).WinterFirewood);
        foreach (int winter in new[] { 0, 1, 5, 12, 24, 64, 72, 80 })
        {
            const int Beats = 8;
            int sum = 0;
            for (int k = 0; k < Beats; k++)
            {
                int burn = HearthSystem.BurnOnBeat(winter, k, Beats);
                Assert.InRange(burn, 0, (winter + Beats - 1) / Beats);
                sum += burn;
            }

            Assert.Equal(winter, sum);
        }
    }

    /// <summary>
    /// Over a real winter each house burns its own row: a cabin 24's worth, a cottage 12, a manor 5 — read as the falls
    /// in each larder's firewood from the first day of autumn to the last of winter (fetches only ever add).
    /// </summary>
    [Fact]
    public void OverAWinterEachHouseBurnsItsOwnRow()
    {
        SimLoop loop = SimFactory.CreatePhase0(Village with { StartingHouseholds = 3 }, new InMemoryLogSink());
        SimWorld world = loop.World;
        while (world.Clock.Season != Season.Fall)
        {
            loop.StepOnce();
        }

        List<Household> homes = Housed(world);
        Assert.True(homes.Count >= 3, $"the pose needs three housed families (has {homes.Count})");
        BuildingKind[] kinds = { BuildingKind.Home, BuildingKind.Cottage, BuildingKind.Manor };
        var burned = new int[3];
        for (int i = 0; i < 3; i++)
        {
            world.SetHomeKind(homes[i], kinds[i]);
            homes[i].Stockpile.Add(Goods.Firewood, Math.Max(0, 40 - homes[i].Stockpile.Firewood));
            Assert.True(homes[i].Stockpile.Firewood >= 24, "the pose needs a winter's firewood in every larder");
        }

        for (int t = 0; t < 2 * world.Config.TicksPerSeason; t++)
        {
            int[] before = homes.Take(3).Select(h => h.Stockpile.Firewood).ToArray();
            loop.StepOnce();
            for (int i = 0; i < 3; i++)
            {
                burned[i] += Math.Max(0, before[i] - homes[i].Stockpile.Firewood);
            }
        }

        _output.WriteLine($"cabin {burned[0]}, cottage {burned[1]}, manor {burned[2]}");
        for (int i = 0; i < 3; i++)
        {
            Assert.Equal(world.BuildingsCatalog[kinds[i]]!.WinterFirewood, burned[i]);
        }
    }

    /// <summary>⭐ <i>Keeps its warmth</i> (§4.8): a fireless cottage chills slower than a cabin, a manor slower still.</summary>
    [Fact]
    public void AFirelessStoneHouseChillsSlowerThanACabin()
    {
        SimWorld world = SimFactory.CreatePhase0(Village with { StartingHouseholds = 3 }, new InMemoryLogSink()).World;
        SimConfig config = world.Config;
        List<Household> homes = Housed(world);
        Assert.True(homes.Count >= 3, $"the pose needs three housed families (has {homes.Count})");

        Assert.Equal(config.ExposurePerTickSheltered, HearthSystem.RoofChillAt(world, new GridPos(-500, -500)));
        int[] days = { config.ExposureDaysSheltered, world.BuildingsCatalog[BuildingKind.Cottage]!.ExposureDaysSheltered,
            world.BuildingsCatalog[BuildingKind.Manor]!.ExposureDaysSheltered };
        Assert.True(days[0] < days[1] && days[1] < days[2], "each house keeps its warmth longer than the last");
        BuildingKind[] kinds = { BuildingKind.Home, BuildingKind.Cottage, BuildingKind.Manor };
        for (int i = 0; i < 3; i++)
        {
            world.SetHomeKind(homes[i], kinds[i]);
            int expected = config.ExposureThreshold / (days[i] * config.TicksPerDay);
            Assert.Equal(expected, HearthSystem.RoofChillAt(world, homes[i].HomeTile!.Value));
        }

        SimConfig shipped = ShippedConfig.Load();
        Assert.Equal(30, shipped.BuildingRows.Single(r => r.Id == (int)BuildingKind.Cottage).ExposureDaysSheltered);
        Assert.Equal(40, shipped.BuildingRows.Single(r => r.Id == (int)BuildingKind.Manor).ExposureDaysSheltered);
    }

    /// <summary>
    /// And the hearth reads it: in winter, a villager standing in a fireless cottage gets cold at the cottage's rate, one in
    /// a fireless cabin at the cabin's — the hearth driven directly so nobody walks off the tile (`ShelterAndExposureTests`).
    /// </summary>
    [Fact]
    public void TheHearthChillsAFirelessHouseAtItsOwnRate()
    {
        SimLoop loop = SimFactory.CreatePhase0(Village with { StartingHouseholds = 3 }, new InMemoryLogSink());
        SimWorld world = loop.World;
        while (world.Clock.Season != Season.Winter)
        {
            loop.StepOnce();
        }

        List<Household> homes = Housed(world);
        Assert.True(homes.Count >= 2, "the pose needs two housed families");
        world.SetHomeKind(homes[1], BuildingKind.Cottage);
        var people = new List<Villager>();
        foreach (Household h in homes.Take(2))
        {
            h.Stockpile.TryTake(Goods.Firewood, h.Stockpile.Firewood);
            Villager v = world.FindVillager(h.MemberIds.First(id => world.FindVillager(id)!.Alive))!;
            v.Position = h.HomePosition!.Value;
            v.Cold = 0;
            people.Add(v);
        }

        var hearth = new HearthSystem();
        const int Ticks = 10;
        for (int i = 0; i < Ticks; i++)
        {
            hearth.Execute(world);
        }

        SimConfig config = world.Config;
        int cottage = config.ExposureThreshold / (world.BuildingsCatalog[BuildingKind.Cottage]!.ExposureDaysSheltered * config.TicksPerDay);
        Assert.Equal(Ticks * config.ExposurePerTickSheltered, people[0].Cold);
        Assert.Equal(Ticks * cottage, people[1].Cold);
        Assert.True(cottage < config.ExposurePerTickSheltered, "a cottage keeps its warmth longer");
    }

    /// <summary>⭐ <i>Lasts</i> (§4.8): ten years on, a cabin is worn and a cottage is sound; the rows' years are doubled.</summary>
    [Fact]
    public void AStoneHouseLastsTwiceAsLong()
    {
        SimConfig shipped = ShippedConfig.Load();
        BuildingRow cabin = shipped.BuildingRows.Single(r => r.Id == (int)BuildingKind.Home);
        foreach (BuildingKind stone in new[] { BuildingKind.Cottage, BuildingKind.Manor })
        {
            BuildingRow row = shipped.BuildingRows.Single(r => r.Id == (int)stone);
            Assert.Equal(cabin.WearsInYears * 2, row.WearsInYears);
            Assert.Equal(cabin.FailsInYears * 2, row.FailsInYears);
            Assert.Equal(cabin.RuinedInYears * 2, row.RuinedInYears);
        }

        SimLoop loop = SimFactory.CreatePhase0(Village with { StartingHouseholds = 2 }, new InMemoryLogSink());
        SimWorld world = loop.World;
        loop.Step(10 * world.Config.TicksPerYear);
        List<Household> homes = Housed(world);
        Assert.True(homes.Count >= 2, "the pose needs two housed families");
        foreach (Household h in homes.Take(2))
        {
            h.HomeRaisedAtTick = 1;
            h.HomeRepairedAtTick = 0;
        }

        world.SetHomeKind(homes[1], BuildingKind.Cottage);
        Assert.True(world.ConditionOf(homes[0]) >= Condition.Worn, $"the cabin is {world.ConditionOf(homes[0])}");
        Assert.Equal(Condition.Sound, world.ConditionOf(homes[1]));
    }

    /// <summary>The larder is the house's (Joe, D717): its walls and its food target follow the row, and go back with it.</summary>
    [Fact]
    public void TheLarderIsTheHouses()
    {
        // A family that wants far more than any larder holds, so the walls — not the family's size — set the target.
        SimWorld world = SimFactory.CreatePhase0(Village with { StartingHouseholds = 2, HomeStoreCap = 400, StockpileTarget = 5000 },
            new InMemoryLogSink()).World;
        Household family = Housed(world).First();
        Assert.Equal(400, family.Stockpile.Capacity);
        foreach ((BuildingKind kind, int walls) in new[] { (BuildingKind.Cottage, 600), (BuildingKind.Manor, 800), (BuildingKind.Home, 400) })
        {
            world.SetHomeKind(family, kind);
            Assert.Equal(walls, family.Stockpile.Capacity);
            Assert.Equal(world.BuildingsCatalog[kind]!.WinterFirewood * world.Config.WinterBufferPercent / 100, world.FirewoodWantedFor(family));
            int wanted = world.Config.StockpileTarget * Math.Max(1, world.LivingMembersOf(family));
            Assert.Equal(Math.Min(wanted, walls - world.FirewoodWantedFor(family)), world.TargetFoodFor(family));
        }
    }

    /// <summary>The woodcutters want each house's own winter: a village of cottages wants twelve less a house.</summary>
    [Fact]
    public void TheWoodcuttersWantEachHousesOwnWinter()
    {
        SimWorld world = SimFactory.CreatePhase0(Village with { StartingHouseholds = 3 }, new InMemoryLogSink()).World;
        List<Household> homes = Housed(world);
        Assert.NotEmpty(homes);
        foreach (StoreBuilding store in world.StoreBuildings)
        {
            store.Store.TryTake(Goods.Firewood, store.Store.Firewood);
        }

        // Every larder at its cabin's want, so nobody is "missing" any in either arm and the difference is the burn alone.
        foreach (Household h in world.Households)
        {
            int want = world.FirewoodWantedFor(h);
            h.Stockpile.Add(Goods.Firewood, Math.Max(0, want - h.Stockpile.Firewood));
            Assert.True(h.Stockpile.Firewood >= want, "the pose needs every larder at its want");
        }

        int cabins = LabourQuota.FirewoodShortfall(world);
        foreach (Household h in homes)
        {
            world.SetHomeKind(h, BuildingKind.Cottage);
        }

        int cottages = LabourQuota.FirewoodShortfall(world);
        int saved = world.BuildingsCatalog[BuildingKind.Home]!.WinterFirewood - world.BuildingsCatalog[BuildingKind.Cottage]!.WinterFirewood;
        _output.WriteLine($"shortfall: cabins {cabins}, cottages {cottages}, {homes.Count} homes × {saved}");
        Assert.True(saved > 0 && cottages > 0, "the pose needs a shortfall the cottages do not erase");
        Assert.Equal(homes.Count * saved, cabins - cottages);
    }

    /// <summary>Saved with the house; hashed only off the cabin, so a village of cabins hashes as it always did.</summary>
    [Fact]
    public void TheHousesKindIsSavedAndHashedOnlyOffTheCabin()
    {
        SimLoop loop = SimFactory.CreatePhase0(Village with { StartingHouseholds = 2 }, new InMemoryLogSink());
        SimWorld world = loop.World;
        Household family = Housed(world).First();
        ulong cabins = StateHash.Compute(world);

        world.SetHomeKind(family, BuildingKind.Manor);
        Assert.NotEqual(cabins, StateHash.Compute(world));
        JsonObject document = SaveGame.Capture(loop, "test", "2026-11-08 12:00", "test");
        SimLoop loaded = SaveGame.Load(JsonNode.Parse(SaveFile.TextOf(document))!.AsObject(), world.Config, null);
        Household back = loaded.World.Households.Single(h => h.Id == family.Id);
        Assert.Equal(BuildingKind.Manor, back.HomeKind);
        Assert.Equal(800, back.Stockpile.Capacity);
        Assert.Equal(StateHash.Compute(world), StateHash.Compute(loaded.World));

        world.SetHomeKind(family, BuildingKind.Home);
        Assert.Equal(cabins, StateHash.Compute(world));
    }

    /// <summary>A dwelling stands on the house's ground and keeps a larder — the longhouse's lesson (D571), refused by name.</summary>
    [Fact]
    public void ADwellingOffTheHousesGroundIsRefusedAtLoad()
    {
        SimConfig config = Village;
        var rows = config.BuildingRows.Select(r => r.Id == (int)BuildingKind.Manor ? r with { ExtentWidth = 3 } : r).ToList();
        SimConfigException blew = Assert.Throws<SimConfigException>(() => (config with { Buildings = rows }).Validate());
        _output.WriteLine(blew.Message);
        Assert.Contains("insulated manor", blew.Message, StringComparison.Ordinal);

        var larderless = config.BuildingRows.Select(r => r.Id == (int)BuildingKind.Cottage ? r with { LocalStoreCap = 0 } : r).ToList();
        Assert.Throws<SimConfigException>(() => (config with { Buildings = larderless }).Validate());
        (config with { Buildings = config.BuildingRows.ToList() }).Validate();
    }

    // ---------------------------------------------------------------
    // Piece 3 — the gates and the choice (§14.3): the family tree latches each house; Build homes as says what is built.
    // ---------------------------------------------------------------

    /// <summary>A three-family fixture village with the family tree's four numbers set for the pose.</summary>
    private static SimConfig Tree(int cottageGeneration, int cottagePeople, int manorGeneration, int manorPeople) =>
        Village with
        {
            StartingHouseholds = 3,
            CottageUnlockGeneration = cottageGeneration,
            CottageUnlockPopulation = cottagePeople,
            ManorUnlockGeneration = manorGeneration,
            ManorUnlockPopulation = manorPeople,
        };

    /// <summary>
    /// ⭐ §9.4–§9.5: each latch holds exactly when both its factors do, not a tick before either — posed twice over sixty
    /// fixture years, once with the generation the binding factor (the people already there) and once with the people
    /// binding (the founders already the generation); and the manor, whose own two factors are met long before the cottage
    /// is, waits for it.
    /// </summary>
    [Fact]
    public void EachHouseIsLearnedTheTickBothItsFactorsHoldAndTheManorWaitsForTheCottage()
    {
        SimLoop probe = SimFactory.CreatePhase0(Tree(3, 1, 2, 1), new InMemoryLogSink());
        int founders = probe.World.Villagers.Count;
        foreach ((int gen, int people, string binds) in new[] { (3, 1, "the generation"), (1, founders + 3, "the people") })
        {
            SimLoop loop = SimFactory.CreatePhase0(Tree(gen, people, 2, 1), new InMemoryLogSink());
            SimWorld world = loop.World;
            Assert.False(world.CottageLearned);
            string why = world.WhyNotYet(BuildingKind.Cottage) ?? string.Empty;
            Assert.Contains($"{people} people ({world.MostEverAlive} so far)", why, StringComparison.Ordinal);
            Assert.Contains($"the {SimWorld.GenerationWord(gen)} generation", why, StringComparison.Ordinal);

            int learnedAt = -1;
            int manorAt = -1;
            int manorHeldWithoutTheCottage = 0;
            int ticks = 60 * world.Config.TicksPerYear;
            for (int t = 0; t < ticks && manorAt < 0; t++)
            {
                loop.StepOnce();
                bool cottageHolds = world.DeepestGenerationBorn >= gen && world.MostEverAlive >= people;
                Assert.Equal(cottageHolds, world.CottageLearned);
                bool manorsOwn = world.DeepestGenerationBorn >= 2 && world.MostEverAlive >= 1;
                Assert.Equal(manorsOwn && world.CottageLearned, world.ManorLearned);
                if (manorsOwn && !world.CottageLearned)
                {
                    manorHeldWithoutTheCottage++;
                }

                learnedAt = learnedAt < 0 && world.CottageLearned ? t : learnedAt;
                manorAt = manorAt < 0 && world.ManorLearned ? t : manorAt;
            }

            _output.WriteLine($"{binds} binding: cottage at tick {learnedAt}, manor at {manorAt} (its own factors held "
                + $"{manorHeldWithoutTheCottage} ticks before); deepest {world.DeepestGenerationBorn}, most alive {world.MostEverAlive}");
            Assert.True(learnedAt >= 0, $"the pose needs the cottage learned ({binds} binding)");
            Assert.True(manorAt >= learnedAt, "the manor is never learned before the cottage");
            Assert.True(gen == 1 || manorHeldWithoutTheCottage > 0, "the pose needs the manor's own factors met before the cottage");
            Assert.True(world.IsUnlocked(BuildingKind.Cottage) && world.IsUnlocked(BuildingKind.Manor));
            Assert.Contains(world.Moments, m => m.Body.Contains("build in stone", StringComparison.Ordinal));
        }
    }

    /// <summary>
    /// A band that carries the village over the cottage's number latches it the tick it settles, and says so in the nomads'
    /// own moment — one popup (D576) — without introducing the tech-tree map: it is the village's size, not its hands.
    /// </summary>
    [Fact]
    public void ABandThatCarriesTheVillageOverTheLineSaysSoInItsOwnMoment()
    {
        // A scout of the same valley, its cottage out of reach, finds the first band's tick and the peak just before it.
        SimLoop scout = SimFactory.CreatePhase0(Tree(1, 9999, 99, 0), new InMemoryLogSink());
        int peakBefore = 0;
        ulong settledAt = 0;
        for (int t = 0; t < 20 * scout.World.Config.TicksPerYear && settledAt == 0; t++)
        {
            int peak = scout.World.MostEverAlive;
            scout.StepOnce();
            if (scout.World.Villagers.Any(v => v.ArrivedAtTick != 0UL))
            {
                settledAt = scout.World.Tick;
                peakBefore = peak;
            }
        }

        Assert.True(settledAt > 0, "the pose needs a band to settle in twenty years");

        // The same valley with the line one past that peak: no birth before the band can reach it, and the band does.
        SimLoop loop = SimFactory.CreatePhase0(Tree(1, peakBefore + 1, 99, 0), new InMemoryLogSink());
        SimWorld world = loop.World;
        while (world.Tick < settledAt - 1)
        {
            loop.StepOnce();
            Assert.False(world.CottageLearned, $"learned at tick {world.Tick}, before the band");
        }

        bool shown = world.ShownTheTechTree;
        int moments = world.Moments.Count;
        loop.StepOnce();
        _output.WriteLine($"a band settled at tick {settledAt}; the peak before it {peakBefore}");
        Assert.True(world.CottageLearned);
        Moment said = Assert.Single(world.Moments.Skip(moments));
        Assert.Contains("build in stone", said.Body, StringComparison.Ordinal);
        Assert.Equal(shown, world.ShownTheTechTree);
    }

    /// <summary>
    /// The map reads the gate (§6): the cottage's node unlocks the cottage on the family tree with both numbers, in sight
    /// from the founding; the manor's waits in the fog behind it; known is the latch, never the counters.
    /// </summary>
    [Fact]
    public void TheMapAndTheGateReadTheFamilyTree()
    {
        SimWorld world = SimFactory.CreatePhase0(Village, new InMemoryLogSink()).World;
        TechNodeRow cottage = world.Config.TechNodeRows.Single(n => n.Id == "cottage");
        TechNodeRow manor = world.Config.TechNodeRows.Single(n => n.Id == "manor");
        Assert.Equal((BuildingKind.Cottage, TechCondition.CottageFamilyTree), (cottage.Unlocks!.Value, cottage.Condition));
        Assert.Equal((BuildingKind.Manor, TechCondition.ManorFamilyTree), (manor.Unlocks!.Value, manor.Condition));
        Assert.Equal(new[] { "cottage" }, manor.Requires);

        Assert.Equal((1, 3, world.MostEverAlive, 20), TechTree.FamilyTreeOf(world, TechCondition.CottageFamilyTree));
        Assert.Equal((1, 5, world.MostEverAlive, 45), TechTree.FamilyTreeOf(world, TechCondition.ManorFamilyTree));
        Assert.Equal(TechState.InSight, TechTree.StateOf(world, cottage).State);
        Assert.Equal(TechState.Fogged, TechTree.StateOf(world, manor).State);
        Assert.Equal((0, 100), TechTree.ProgressOf(world, TechCondition.ManorFamilyTree));
        Assert.False(world.IsUnlocked(BuildingKind.Cottage));
        Assert.Contains("build in stone", world.WhyNotYet(BuildingKind.Manor)!, StringComparison.Ordinal);

        world.CottageLearned = true;
        Assert.Equal(TechState.Known, TechTree.StateOf(world, cottage).State);
        Assert.Equal(TechState.InSight, TechTree.StateOf(world, manor).State);
        Assert.True(world.IsUnlocked(BuildingKind.Cottage));
        Assert.Contains($"45 people ({world.MostEverAlive} so far)", world.WhyNotYet(BuildingKind.Manor)!, StringComparison.Ordinal);

        world.ManorLearned = true;
        Assert.Equal(TechState.Known, TechTree.StateOf(world, manor).State);
        Assert.Null(world.WhyNotYet(BuildingKind.Manor));
        Assert.True(TechTree.IntroducesTheMap(TechCondition.CottageFamilyTree));
    }

    /// <summary>
    /// §7 / §9.16: the two latches and the choice are saved, and hashed only when set — so a village of cabins that has
    /// learned nothing hashes byte for byte as before. (The factors' gap — the generation unhashed — is
    /// <see cref="TheGenerationIsSavedAndNotHashed"/>'s.)
    /// </summary>
    [Fact]
    public void TheLatchesAndTheChoiceAreSavedAndHashedOnlyWhenSet()
    {
        SimLoop loop = SimFactory.CreatePhase0(Village, new InMemoryLogSink());
        SimWorld world = loop.World;
        ulong nothing = StateHash.Compute(world);
        world.CottageLearned = true;
        ulong cottage = StateHash.Compute(world);
        world.ManorLearned = true;
        ulong manor = StateHash.Compute(world);
        world.HomesBuiltAs = BuildingKind.Manor;
        ulong chosen = StateHash.Compute(world);
        Assert.Equal(4, new[] { nothing, cottage, manor, chosen }.Distinct().Count());

        JsonObject document = SaveGame.Capture(loop, "test", "2026-11-08 12:00", "test");
        SimWorld back = SaveGame.Load(JsonNode.Parse(SaveFile.TextOf(document))!.AsObject(), world.Config, null).World;
        Assert.True(back.CottageLearned && back.ManorLearned);
        Assert.Equal(BuildingKind.Manor, back.HomesBuiltAs);
        Assert.Equal(chosen, StateHash.Compute(back));

        world.CottageLearned = false;
        world.ManorLearned = false;
        world.HomesBuiltAs = BuildingKind.Home;
        Assert.Equal(nothing, StateHash.Compute(world));
    }

    /// <summary>§4.5: the choice is Cabin, refused in words above what is learned, and refused for anything not a house.</summary>
    [Fact]
    public void TheChoiceIsCabinAndRefusedAboveWhatIsLearned()
    {
        SimWorld world = SimFactory.CreatePhase0(Village, new InMemoryLogSink()).World;
        Assert.Equal(BuildingKind.Home, world.HomesBuiltAs);
        Assert.Equal(world.WhyNotYet(BuildingKind.Cottage), world.SetHomesBuiltAs(BuildingKind.Cottage));
        Assert.NotNull(world.SetHomesBuiltAs(BuildingKind.Granary));
        Assert.Equal(BuildingKind.Home, world.HomesBuiltAs);

        world.CottageLearned = true;
        Assert.Null(world.SetHomesBuiltAs(BuildingKind.Cottage));
        Assert.Equal(BuildingKind.Cottage, world.HomesBuiltAs);
        Assert.NotNull(world.SetHomesBuiltAs(BuildingKind.Manor));
        Assert.Equal(BuildingKind.Cottage, world.HomesBuiltAs);

        world.ManorLearned = true;
        Assert.Null(world.SetHomesBuiltAs(BuildingKind.Manor));
        Assert.Equal(BuildingKind.Manor, world.HomesBuiltAs);
    }

    /// <summary>A fixture village with stone and logs in store and two hands kept on building.</summary>
    private static SimLoop AVillageToRebuild(SimConfig config)
    {
        SimLoop loop = SimFactory.CreatePhase0(config, new InMemoryLogSink());
        SimWorld world = loop.World;
        StoreBuilding store = world.StoreBuildings.First(s => s.Gives(Goods.Logs) && s.Gives(Goods.Stone));
        store.Store.Receive(Goods.Logs, 600);
        store.Store.Receive(Goods.Stone, 600);
        foreach (Villager builder in world.Villagers.Where(v => v.Alive && v.CanWork).Take(2))
        {
            world.SetPinnedTrade(builder, JobKind.Builder);
        }

        return loop;
    }

    private static GridPos TileOf(SimWorld world, Household household) => world.HomeFootprintOf(household)!.Value.Origin.ToTile();

    private static bool StandsOn(Workplace? site, SimWorld world, Household household) =>
        site is not null && site.Footprint.Covers(TileOf(world, household));

    /// <summary>
    /// ⭐ §9.6, the unattended guard: a village that has learned both houses and was not told builds and rebuilds nothing
    /// in stone — fifteen years, stone in store.
    /// </summary>
    [Fact]
    public void ALearnedVillageThatWasNotToldRebuildsNothing()
    {
        SimLoop loop = AVillageToRebuild(Village with { StartingHouseholds = 3 });
        SimWorld world = loop.World;
        world.CottageLearned = true;
        world.ManorLearned = true;
        for (int year = 0; year < 15; year++)
        {
            loop.Step(world.Config.TicksPerYear);
            Assert.All(world.Households, h => Assert.Equal(BuildingKind.Home, h.HomeKind));
            Assert.DoesNotContain(world.Workplaces, w => w.Construction is { } site && BuildingKinds.TierOf(site.Kind) > 0);
        }

        Assert.Equal(BuildingKind.Home, world.HomesBuiltAs);
    }

    /// <summary>
    /// ⭐ §9.7: under <i>Cottage</i> the standing cabins are rebuilt where they stand, never two at once; every family is
    /// housed the whole time; the fence is the one it had; the kind, raised-at and repaired-at are set at the finish — and a
    /// new couple's house is marked a cottage.
    /// </summary>
    [Fact]
    public void UnderCottageTheCabinsAreRebuiltOneAtATimeWithTheFamiliesAtHome()
    {
        SimLoop loop = AVillageToRebuild(Village with { StartingHouseholds = 3 });
        SimWorld world = loop.World;
        world.CottageLearned = true;
        List<Household> housed = Housed(world);
        Assert.True(housed.Count >= 3, "the pose needs three standing cabins");
        var fences = housed.ToDictionary(h => h.Id, h => h.FencedTiles.ToList());
        Assert.Null(world.SetHomesBuiltAs(BuildingKind.Cottage));
        Assert.NotNull(world.StandingRebuild());

        int rebuilt = 0;
        int newHomes = 0;
        var marked = new HashSet<int>();
        var kinds = housed.ToDictionary(h => h.Id, h => h.HomeKind);
        int ticks = 25 * world.Config.TicksPerYear;
        for (int t = 0; t < ticks && (rebuilt < housed.Count || newHomes == 0); t++)
        {
            ulong before = world.Tick;
            loop.StepOnce();
            Assert.True(world.Workplaces.Count(w => w.Construction is { RebuildsHome: true }) <= 1, "two rebuilds at once");
            var sitesNow = new HashSet<int>();
            foreach (Workplace site in world.Workplaces)
            {
                if (site.Construction is { RebuildsHome: false, Repairing: false, Demolishing: false } plan
                    && BuildingKinds.IsDwelling(plan.Kind) && plan.ForHouseholdId != 0)
                {
                    // A new couple's house: a cottage, and one a family (the roofless queue asks `HomeSiteFor` of any row).
                    Assert.Equal(BuildingKind.Cottage, plan.Kind);
                    Assert.True(sitesNow.Add(plan.ForHouseholdId), "two house sites for one family");
                    marked.Add(plan.ForHouseholdId);
                }
            }

            // ⭐ And raised: the family moves into a cottage, not a cabin with a cottage's price.
            foreach (int id in marked.Where(id => !sitesNow.Contains(id)).ToList())
            {
                marked.Remove(id);
                if (world.FindHousehold(id) is { HasHome: true } moved)
                {
                    Assert.Equal(BuildingKind.Cottage, moved.HomeKind);
                    newHomes++;
                }
            }

            foreach (Household h in housed)
            {
                Assert.True(h.HasHome, $"the {h.Name} household lost its roof while its house was rebuilt");
                Assert.Equal(fences[h.Id], h.FencedTiles);
                if (h.HomeKind != kinds[h.Id])
                {
                    Assert.Equal(BuildingKind.Cottage, h.HomeKind);
                    Assert.InRange(h.HomeRaisedAtTick, before, world.Tick);
                    Assert.Equal(0UL, h.HomeRepairedAtTick);
                    Assert.Equal(600, h.Stockpile.Capacity);
                    kinds[h.Id] = h.HomeKind;
                    rebuilt++;
                }
            }
        }

        _output.WriteLine($"{rebuilt} of {housed.Count} rebuilt by Year {world.Clock.Year}; {newHomes} new homes raised as cottages");
        Assert.Equal(housed.Count, rebuilt);
        Assert.True(newHomes > 0, "the pose needs a new couple's house raised under Cottage");
    }

    /// <summary>A repair already under way is let finish (§4.5): the rebuild goes to another house, and the mending stays.</summary>
    [Fact]
    public void AHalfMendedHouseIsMendedBeforeItIsRebuilt()
    {
        SimLoop loop = AVillageToRebuild(Village with { StartingHouseholds = 3 });
        SimWorld world = loop.World;
        loop.Step(10 * world.Config.TicksPerYear);
        foreach (Workplace mending in world.Workplaces.Where(w => w.Construction is { Repairing: true }).ToList())
        {
            world.CancelConstruction(mending);
        }

        List<Household> homes = Housed(world);
        foreach (Household h in homes)
        {
            h.HomeRepairedAtTick = world.Tick;
        }

        Household worn = homes[0];
        worn.HomeRaisedAtTick = 1;
        worn.HomeRepairedAtTick = 0;
        world.TendWhatStands();
        Workplace repair = world.RepairSiteAt(TileOf(world, worn))!;
        Assert.Equal(1, repair.Construction!.Deliver(Goods.Logs, 1));

        world.CottageLearned = true;
        Assert.Null(world.SetHomesBuiltAs(BuildingKind.Cottage));
        Assert.NotNull(world.StandingRebuild());
        Assert.False(StandsOn(world.StandingRebuild(), world, worn), "a half-mended house was taken for the rebuild");
        Assert.Same(repair, world.RepairSiteAt(TileOf(world, worn)));
    }

    /// <summary>⭐ §9.8: a rebuild is behind every other site — a granary marked after it is ahead of it, and neither can be moved past the other.</summary>
    [Fact]
    public void ARebuildWaitsBehindEveryOtherSite()
    {
        SimLoop loop = AVillageToRebuild(Village with { StartingHouseholds = 3 });
        SimWorld world = loop.World;
        world.CottageLearned = true;
        Assert.Null(world.SetHomesBuiltAs(BuildingKind.Cottage));
        Workplace rebuild = world.StandingRebuild()!;
        Assert.True(world.Mark(BuildingKind.Granary, ClearGroundNear(world, BuildingKind.Granary)).Allowed);
        Workplace granary = world.Workplaces.Single(w => w.Construction is { Kind: BuildingKind.Granary });
        Assert.True(granary.Id > rebuild.Id, "the pose needs the granary marked after the rebuild");

        List<Workplace> queue = world.BuildQueue();
        Assert.Same(rebuild, queue[^1]);
        Assert.Same(granary, world.NextToBuild());
        Assert.False(world.MoveInBuildQueue(rebuild, -1));
        Assert.False(world.MoveInBuildQueue(queue[^2], +1));
    }

    /// <summary>
    /// ⭐ §9.9: a worn cabin under <i>Cottage</i> is rebuilt before a sound one nearer the hut, its untouched repair becomes
    /// the rebuild, and a ruin comes back an open cottage; the walk never raises a repair on a house whose rebuild stands.
    /// </summary>
    [Fact]
    public void AWornCabinIsRebuiltNotRepairedAndARuinComesBackACottage()
    {
        SimLoop loop = AVillageToRebuild(Village with { StartingHouseholds = 3 });
        SimWorld world = loop.World;
        loop.Step(25 * world.Config.TicksPerYear);
        foreach (Workplace mending in world.Workplaces.Where(w => w.Construction is { Repairing: true }).ToList())
        {
            world.CancelConstruction(mending);
        }

        Point hut = world.Workplaces.First(w => w.Kind == JobKind.Builder && !w.IsSite).Position;
        List<Household> byDistance = Housed(world)
            .OrderBy(h => world.HomeFootprintOf(h)!.Value.Origin.DistanceTo(hut)).ThenBy(h => h.Id).ToList();
        Assert.True(byDistance.Count >= 3, "the pose needs three standing cabins");
        foreach (Household h in byDistance)
        {
            h.HomeRepairedAtTick = world.Tick;
            h.HomeClosed = false;
        }

        // The farthest two: one worn, one a ruin (twenty-five years since raised, never mended).
        Household worn = byDistance[^2];
        Household ruin = byDistance[^1];
        worn.HomeRaisedAtTick = 1;
        worn.HomeRepairedAtTick = world.Tick - (9UL * (ulong)world.Config.TicksPerYear);
        ruin.HomeRaisedAtTick = 1;
        ruin.HomeRepairedAtTick = 0;
        Assert.Equal(Condition.Worn, world.ConditionOf(worn));
        Assert.Equal(Condition.Ruin, world.ConditionOf(ruin));

        // Under Cabin the walk mends them, and closes the ruin.
        world.TendWhatStands();
        Assert.NotNull(world.RepairSiteAt(TileOf(world, worn)));
        Assert.NotNull(world.RepairSiteAt(TileOf(world, ruin)));
        Assert.True(ruin.HomeClosed);

        // Under Cottage the worn are first, nearest of them first — and the repair becomes the rebuild.
        world.CottageLearned = true;
        Assert.Null(world.SetHomesBuiltAs(BuildingKind.Cottage));
        Household first = StandsOn(world.StandingRebuild(), world, worn) ? worn : ruin;
        Assert.True(StandsOn(world.StandingRebuild(), world, first), "the rebuild went to a sound cabin before the worn ones");
        Assert.Null(world.RepairSiteAt(TileOf(world, first)));

        // The walk does not stack a repair on a house whose rebuild stands.
        world.TendWhatStands();
        Assert.Null(world.RepairSiteAt(TileOf(world, first)));

        world.Complete(world.StandingRebuild()!);
        Assert.Equal(BuildingKind.Cottage, first.HomeKind);
        Household second = first == worn ? ruin : worn;
        Assert.True(StandsOn(world.StandingRebuild(), world, second), "the next rebuild went past the other worn cabin");
        world.Complete(world.StandingRebuild()!);
        foreach (Household h in new[] { worn, ruin })
        {
            Assert.Equal(BuildingKind.Cottage, h.HomeKind);
            Assert.Equal(Condition.Sound, world.ConditionOf(h));
            Assert.False(h.HomeClosed);
            Assert.Null(world.RepairSiteAt(TileOf(world, h)));
        }
    }

    /// <summary>
    /// §9.10 and §4.5's last two rules: <i>Manor</i> over a cabin goes straight to a manor; a fresh choice re-aims a rebuild
    /// nobody has started; lowering it calls an untouched one off and raises no more; one already under way is finished.
    /// </summary>
    [Fact]
    public void ManorOverACabinGoesStraightToManorAndLoweringStopsTheRebuilds()
    {
        SimLoop loop = AVillageToRebuild(Village with { StartingHouseholds = 3 });
        SimWorld world = loop.World;
        world.CottageLearned = true;
        world.ManorLearned = true;

        Assert.Null(world.SetHomesBuiltAs(BuildingKind.Cottage));
        Assert.Equal(BuildingKind.Cottage, world.StandingRebuild()!.Construction!.Kind);
        Assert.Null(world.SetHomesBuiltAs(BuildingKind.Manor));
        Workplace manor = world.StandingRebuild()!;
        Assert.Equal(BuildingKind.Manor, manor.Construction!.Kind);
        Household cabin = world.HouseholdAt(manor.Tile)!;
        Assert.Equal(BuildingKind.Home, cabin.HomeKind);
        world.Complete(manor);
        Assert.Equal(BuildingKind.Manor, cabin.HomeKind);
        Assert.Equal(800, cabin.Stockpile.Capacity);

        // Lowered: the next, untouched, is called off, and the season's walk raises none.
        Assert.NotNull(world.StandingRebuild());
        Assert.Null(world.SetHomesBuiltAs(BuildingKind.Home));
        Assert.Null(world.StandingRebuild());
        world.TendWhatStands();
        Assert.Null(world.StandingRebuild());

        // Started, it is finished.
        Assert.Null(world.SetHomesBuiltAs(BuildingKind.Cottage));
        Workplace started = world.StandingRebuild()!;
        Assert.Equal(1, started.Construction!.Deliver(Goods.Logs, 1));
        Assert.Null(world.SetHomesBuiltAs(BuildingKind.Home));
        Assert.Same(started, world.StandingRebuild());
    }

    /// <summary>§8: a house pulled down takes its rebuild with it, and what was delivered goes back.</summary>
    [Fact]
    public void AHouseComingDownTakesItsRebuildWithIt()
    {
        SimLoop loop = AVillageToRebuild(Village with { StartingHouseholds = 3 });
        SimWorld world = loop.World;
        world.CottageLearned = true;
        Assert.Null(world.SetHomesBuiltAs(BuildingKind.Cottage));
        Workplace rebuild = world.StandingRebuild()!;
        Household family = world.HouseholdAt(rebuild.Tile)!;
        Assert.Equal(1, rebuild.Construction!.Deliver(Goods.Stone, 1));
        int stoneBefore = world.StoreBuildings.Sum(s => s.Store[Goods.Stone]);

        Assert.True(world.MarkDemolition(TileOf(world, family)).Allowed);
        Assert.DoesNotContain(rebuild, world.Workplaces);
        Assert.False(StandsOn(world.StandingRebuild(), world, family));
        Assert.Equal(stoneBefore + 1, world.StoreBuildings.Sum(s => s.Store[Goods.Stone]));
    }

    /// <summary>§9.15: a village mid-rebuild, saved and loaded, hashes as it did live, the site still a rebuild of its row.</summary>
    [Fact]
    public void ARebuildIsSavedAndHashed()
    {
        SimLoop loop = AVillageToRebuild(Village with { StartingHouseholds = 3 });
        SimWorld world = loop.World;
        world.CottageLearned = true;
        Assert.Null(world.SetHomesBuiltAs(BuildingKind.Cottage));
        loop.Step(world.Config.TicksPerDay * 3);
        Workplace rebuild = world.StandingRebuild()!;

        JsonObject document = SaveGame.Capture(loop, "test", "2026-11-08 12:00", "test");
        SimWorld back = SaveGame.Load(JsonNode.Parse(SaveFile.TextOf(document))!.AsObject(), world.Config, null).World;
        Workplace loaded = back.StandingRebuild()!;
        Assert.Equal((rebuild.Id, BuildingKind.Cottage), (loaded.Id, loaded.Construction!.Kind));
        Assert.Equal(StateHash.Compute(world), StateHash.Compute(back));
    }

    // ---------------------------------------------------------------
    // Piece 4 — the view's facts (§14.4): the family's name on a stone house, a house's own rebuild, the log's two lines.
    // ---------------------------------------------------------------

    /// <summary>
    /// ⭐ §4.8, <i>bears the family's name</i> (Joe, D718: <i>"go with your 3 perks!"</i>): a cottage is <i>"the Ashford
    /// cottage"</i>, a manor <i>"Ashford Manor"</i> — derived from the household, never stored — and a cabin, or a family with
    /// no house, has none.
    /// </summary>
    [Fact]
    public void AStoneHouseBearsTheFamilysName()
    {
        SimWorld world = SimFactory.CreatePhase0(Village with { StartingHouseholds = 2 }, new InMemoryLogSink()).World;
        Household family = Housed(world)[0];
        Assert.Null(world.HouseNameOf(family));

        world.SetHomeKind(family, BuildingKind.Cottage);
        Assert.Equal($"the {family.Name} cottage", world.HouseNameOf(family));

        world.SetHomeKind(family, BuildingKind.Manor);
        Assert.Equal($"{family.Name} Manor", world.HouseNameOf(family));

        family.Rename("Thornwood");
        Assert.Equal("Thornwood Manor", world.HouseNameOf(family));

        Point? home = family.HomePosition;
        family.HomePosition = null;
        Assert.Null(world.HouseNameOf(family));
        family.HomePosition = home;
    }

    /// <summary>A name is a dwelling's, and names the family — refused at load otherwise, by the row's name.</summary>
    [Fact]
    public void AHouseNameIsADwellingsAndNamesTheFamily()
    {
        SimConfig config = Village;
        var nameless = config.BuildingRows
            .Select(r => r.Id == (int)BuildingKind.Cottage ? r with { HouseName = "the cottage" } : r).ToList();
        SimConfigException blew = Assert.Throws<SimConfigException>(() => (config with { Buildings = nameless }).Validate());
        _output.WriteLine(blew.Message);
        Assert.Contains("stone cottage", blew.Message, StringComparison.Ordinal);

        var namedGranary = config.BuildingRows
            .Select(r => r.Id == (int)BuildingKind.Granary ? r with { HouseName = "the {family} granary" } : r).ToList();
        blew = Assert.Throws<SimConfigException>(() => (config with { Buildings = namedGranary }).Validate());
        _output.WriteLine(blew.Message);
        Assert.Contains("granary", blew.Message, StringComparison.Ordinal);
    }

    /// <summary>
    /// The house knows its own rebuild — what the home card reads its progress from (§6) — and the log says the rebuild's
    /// start and its finish (§7), the finished house by its new name, with its article right (<i>"an insulated manor"</i>).
    /// </summary>
    [Fact]
    public void AHouseKnowsItsRebuildAndTheLogSaysItsStartAndFinish()
    {
        SimLoop loop = AVillageToRebuild(Village with { StartingHouseholds = 3 });
        SimWorld world = loop.World;
        var log = (InMemoryLogSink)world.Logger;
        world.CottageLearned = true;
        world.ManorLearned = true;
        Assert.All(Housed(world), h => Assert.Null(world.RebuildOf(h)));

        Assert.Null(world.SetHomesBuiltAs(BuildingKind.Cottage));
        Workplace rebuild = world.StandingRebuild()!;
        Household family = world.HouseholdAt(rebuild.Tile)!;
        Assert.Same(rebuild, world.RebuildOf(family));
        Assert.All(Housed(world).Where(h => h.Id != family.Id), h => Assert.Null(world.RebuildOf(h)));
        string raised = $"The {family.Name} household's house is to be rebuilt as a stone cottage";
        Assert.Contains(log.Entries, e => e.Level == LogLevel.Info && e.Message.StartsWith(raised, StringComparison.Ordinal));

        Assert.Null(world.SetHomesBuiltAs(BuildingKind.Manor));
        Workplace manor = world.StandingRebuild()!;
        Assert.Same(manor, world.RebuildOf(family));
        string reaimed = $"The {family.Name} household's house is to be rebuilt as an insulated manor";
        Assert.Contains(log.Entries, e => e.Message.StartsWith(reaimed, StringComparison.Ordinal));

        world.Complete(manor);
        Assert.False(StandsOn(world.RebuildOf(family), world, family));
        LogEntry finished = Assert.Single(log.Entries, e => e.Message.Contains($"is rebuilt as {family.Name} Manor", StringComparison.Ordinal));
        _output.WriteLine(finished.Message);
        Assert.Equal(LogLevel.Info, finished.Level);
        Assert.StartsWith($"The {family.Name} household's house is rebuilt", finished.Message, StringComparison.Ordinal);
        Assert.Contains("5 firewood a winter where the old one burned 24", finished.Message, StringComparison.Ordinal);
        Assert.DoesNotContain(log.Entries, e => e.Message.Contains(" a insulated", StringComparison.Ordinal));
    }

    private static GridPos ClearGroundNear(SimWorld world, BuildingKind kind)
    {
        GridPos site = world.Map.FoundingSite;
        for (int radius = 1; radius < 14; radius++)
        {
            for (int dy = -radius; dy <= radius; dy++)
            {
                for (int dx = -radius; dx <= radius; dx++)
                {
                    var at = new GridPos(site.X + dx, site.Y + dy);
                    if (!world.HasSomethingToHarvest(at) && world.CanBuildAt(kind, at).Allowed)
                    {
                        return at;
                    }
                }
            }
        }

        throw new Xunit.Sdk.XunitException("No clear ground near the founding site.");
    }

    /// <summary>The elder generation of each household's living, paired, fertile-age couple — the parents of a birth.</summary>
    private static Dictionary<int, int> ElderParentGenerationByHousehold(SimWorld world)
    {
        var eldest = new Dictionary<int, int>();
        for (int i = 0; i < world.Villagers.Count; i++)
        {
            Villager v = world.Villagers[i];
            if (!v.Alive || !v.IsPaired)
            {
                continue;
            }

            Villager? partner = world.FindVillager(v.PartnerId);
            if (partner is null || !partner.Alive || partner.HouseholdId != v.HouseholdId)
            {
                continue;
            }

            int pair = Math.Max(v.Generation, partner.Generation);
            eldest[v.HouseholdId] = eldest.TryGetValue(v.HouseholdId, out int had) ? Math.Max(had, pair) : pair;
        }

        return eldest;
    }
}

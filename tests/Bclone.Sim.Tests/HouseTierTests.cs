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

using Bclone.Sim.Config;
using Bclone.Sim.Core;
using Bclone.Sim.Determinism;
using Bclone.Sim.Logging;
using Bclone.Sim.Systems;
using Bclone.Sim.World;
using Xunit;
using Xunit.Abstractions;

namespace Bclone.Sim.Tests;

/// <summary>
/// `specs/clothing.md §3` — the coat on the villager (C1, D659): a roof you wear, worn a day every winter day,
/// fetched from a store by any adult. The tailor who sews it is C2's (`TailorTests`).
/// </summary>
public sealed class ClothingTests
{
    private readonly ITestOutputHelper _output;

    public ClothingTests(ITestOutputHelper output) => _output = output;

    private static SimConfig Config => VillageFixtures.Village;

    /// <summary>A loop stepped to the first winter, so the hearth system is live.</summary>
    private static SimLoop InWinter(SimConfig config)
    {
        SimLoop loop = SimFactory.CreatePhase0(config, new InMemoryLogSink());
        while (loop.World.Clock.Season != Season.Winter)
        {
            loop.StepOnce();
        }

        return loop;
    }

    /// <summary>Hold everyone still and let only the cold happen (`ShelterAndExposureTests.Chill`).</summary>
    private static void Chill(SimWorld world, int ticks)
    {
        var hearth = new HearthSystem();
        for (int i = 0; i < ticks; i++)
        {
            foreach (Household household in world.Households)
            {
                household.Stockpile.Add(Goods.Firewood, 10);
            }

            hearth.Execute(world);
        }
    }

    private static GridPos FarFromAnyBuilding(SimWorld world)
    {
        for (int x = 0; x < world.Map.Width; x++)
        {
            for (int y = 0; y < world.Map.Height; y++)
            {
                var candidate = new GridPos(x, y);
                if (world.ShelterAt(candidate) == Shelter.Outdoors)
                {
                    return candidate;
                }
            }
        }

        throw new Xunit.Sdk.XunitException("The whole valley is under cover, which cannot be right.");
    }

    // ---------------------------------------------------------------
    //  §3.1 — a roof you wear
    // ---------------------------------------------------------------

    /// <summary>Joe, D659: *"a roof you wear"* — open ground chills a clothed villager exactly as a fireless roof does.</summary>
    [Fact]
    public void ACoatMakesOpenGroundAsKindAsARoof()
    {
        SimWorld world = InWinter(Config).World;

        Villager clothed = world.Villagers[0];
        Villager bare = world.Villagers[1];
        Villager underARoof = world.Villagers[2];

        Point open = Point.CentreOf(FarFromAnyBuilding(world));
        clothed.Position = open;
        bare.Position = open;
        underARoof.Position = world.AnyStoreOf(StoreKind.Warehouse).Position;
        clothed.CoatDays = 1_000;
        clothed.Cold = bare.Cold = underARoof.Cold = 0;

        Chill(world, 20);

        _output.WriteLine($"clothed outdoors {clothed.Cold}, bare outdoors {bare.Cold}, bare under a roof {underARoof.Cold}.");

        Assert.True(clothed.Cold > 0, "A coat stopped the cold entirely — D45's literal reading, which Joe did not take.");
        Assert.Equal(underARoof.Cold, clothed.Cold);
        Assert.True(clothed.Cold < bare.Cold, "A coat was worth nothing against open ground.");
    }

    /// <summary>The roof's arm does not read the coat: a household out of firewood still freezes (fuel stays the axis).</summary>
    [Fact]
    public void ACoatNeverHelpsUnderAFirelessRoof()
    {
        SimWorld world = InWinter(Config).World;

        Villager clothed = world.Villagers[0];
        Villager bare = world.Villagers[1];
        Point roof = world.AnyStoreOf(StoreKind.Warehouse).Position;
        clothed.Position = roof;
        bare.Position = roof;
        clothed.CoatDays = 1_000;
        clothed.Cold = bare.Cold = 0;

        Chill(world, 20);

        Assert.True(bare.Cold > 0, "A fireless roof stopped the cold, which is not the model.");
        Assert.Equal(bare.Cold, clothed.Cold);
    }

    // ---------------------------------------------------------------
    //  §3.3 — wear by the winter day
    // ---------------------------------------------------------------

    [Fact]
    public void ACoatWearsADayEveryWinterDayAndNoneOutsideWinter()
    {
        SimConfig config = Config;
        SimLoop loop = InWinter(config);
        Villager villager = loop.World.Villagers[0];
        villager.CoatDays = 10;

        loop.Step(config.TicksPerDay * 3);

        _output.WriteLine($"three winter days: 10 → {villager.CoatDays}.");
        Assert.Equal(3, 10 - villager.CoatDays);

        SimLoop spring = SimFactory.CreatePhase0(config, new InMemoryLogSink());
        Assert.NotEqual(Season.Winter, spring.World.Clock.Season);
        Villager other = spring.World.Villagers[0];
        other.CoatDays = 10;

        spring.Step(config.TicksPerDay * 3);

        Assert.Equal(10, other.CoatDays);
    }

    [Fact]
    public void AWornThroughCoatLeavesThemBareAndTheLogSaysSo()
    {
        SimConfig config = Config;
        var sink = new InMemoryLogSink();
        SimLoop loop = SimFactory.CreatePhase0(config, sink);
        while (loop.World.Clock.Season != Season.Winter)
        {
            loop.StepOnce();
        }

        Villager villager = loop.World.Villagers[0];
        villager.CoatDays = 1;

        loop.Step(config.TicksPerDay);

        Assert.Equal(0, villager.CoatDays);
        LogEntry? said = sink.Entries.FirstOrDefault(e => e.Message.Contains("coat is worn through", StringComparison.Ordinal));
        Assert.NotNull(said);
        _output.WriteLine(said!.Message);
        Assert.Contains(villager.Name, said.Message, StringComparison.Ordinal);
    }

    // ---------------------------------------------------------------
    //  §3.3 — fetched from a store, by any adult
    // ---------------------------------------------------------------

    [Fact]
    public void AnAdultFetchesACoatFromAStoreAndAChildNever()
    {
        SimConfig config = Config;
        var sink = new InMemoryLogSink();
        SimLoop loop = SimFactory.CreatePhase0(config, sink);
        SimWorld world = loop.World;
        StoreBuilding warehouse = world.AnyStoreOf(StoreKind.Warehouse);
        warehouse.Store.Add(Goods.Clothing, 3);

        // ⚠️ The founders stand beside the shelves at tick 0, so the first coats are taken on the spot and nobody is
        // mid-walk on a sampled tick — the walk is printed, not asserted. The hand-over is: every coat that left the
        // shelf was taken by somebody at the store, and the log says so (nothing conjured).
        int fetches = 0;
        for (int tick = 0; tick < config.TicksPerSeason; tick++)
        {
            loop.StepOnce();
            fetches += world.Villagers.Count(v => v.State == VillagerState.FetchingACoat);
        }

        int coated = world.Villagers.Count(v => v.Alive && v.CoatDays > 0);
        int onShelves = world.InStores(Goods.Clothing);
        int handedOver = sink.Entries.Count(e => e.Message.Contains("took clothing from", StringComparison.Ordinal));
        _output.WriteLine($"{coated} coated + {onShelves} on the shelves = {coated + onShelves}; {handedOver} handed over at a store; {fetches} villager-ticks spent walking for one.");

        Assert.True(coated > 0, "Nobody fetched a coat in a season.");
        Assert.Equal(3, coated + onShelves);
        Assert.Equal(coated, handedOver);
        Assert.All(world.Villagers.Where(v => v.CoatDays > 0), v => Assert.True(v.CanWork, $"{v.Name}, a child, wears a coat."));
        Assert.All(world.Villagers.Where(v => v.CoatDays > 0),
            v => Assert.Equal(world.GoodsCatalog[Goods.Clothing].CoatWinters * config.DaysPerSeason, v.CoatDays));
    }

    /// <summary>Joe, D659: children do not count — a child beside a shelf of coats takes none, and wants none of the tailor.</summary>
    [Fact]
    public void AChildTakesNoCoatAndCountsForNone()
    {
        SimConfig config = Config;
        SimLoop loop = SimFactory.CreatePhase0(config, new InMemoryLogSink());
        SimWorld world = loop.World;
        Villager child = world.Villagers[0];
        child.AgeYears = 3;
        child.LifeStage = LifeStage.Child;
        int adultsBefore = world.Villagers.Count(v => v.Alive && v.CanWork);
        world.AnyStoreOf(StoreKind.Warehouse).Store.Add(Goods.Clothing, 100);

        loop.Step(config.TicksPerSeason);

        Assert.False(child.CanWork);
        Assert.Equal(0, child.CoatDays);
        Assert.True(world.Villagers.Any(v => v.CoatDays > 0), "No adult took a coat either, so the child's bare back proves nothing.");
        Assert.True(adultsBefore > 0);
    }

    [Fact]
    public void NoCoatShipsInTheCartAndNobodyWearsOneAtTheFounding()
    {
        SimWorld world = SimFactory.CreatePhase0(ShippedConfig.Load(), new InMemoryLogSink()).World;

        Assert.Equal(0, world.InStores(Goods.Clothing));
        Assert.All(world.Villagers, v => Assert.Equal(0, v.CoatDays));
        Assert.Null(world.StockLimits.For(Goods.Clothing));
    }

    // ---------------------------------------------------------------
    //  §3.4 — the good's row
    // ---------------------------------------------------------------

    [Fact]
    public void ClothingIsAMaterialTheWarehouseKeepsAndItsWintersAreItsRows()
    {
        SimConfig shipped = ShippedConfig.Load();
        var catalogue = new GoodsCatalog(shipped.GoodsCatalog);
        GoodRow row = catalogue[Goods.Clothing];

        Assert.Equal(GoodCategory.Materials, row.Category);
        Assert.Contains(StoreKind.Warehouse, row.StoredBy);
        Assert.DoesNotContain(StoreKind.Market, row.StoredBy);
        Assert.DoesNotContain(StoreKind.Granary, row.StoredBy);
        Assert.True(row.IsCoat);
        Assert.Equal(shipped.CoatWinters, row.CoatWinters);
        Assert.Equal(3, row.CoatWinters);
        Assert.Equal(new[] { Goods.Clothing }, catalogue.CoatsBestFirst);
        Assert.False(catalogue[Goods.Leather].IsCoat);
    }

    // ---------------------------------------------------------------
    //  §3.3 — hashed, saved
    // ---------------------------------------------------------------

    [Fact]
    public void TheHashCoversTheCoat()
    {
        SimWorld world = SimFactory.CreatePhase0(Config, new InMemoryLogSink()).World;
        ulong bare = StateHash.Compute(world);

        world.Villagers[0].CoatDays = 5;

        Assert.NotEqual(bare, StateHash.Compute(world));
    }

    // ---------------------------------------------------------------
    //  §3.5 — the shortfall the tailor is hired against
    // ---------------------------------------------------------------

    [Fact]
    public void ClothingShortfallCountsTheUncoatedAYearsWearAndTheShelves()
    {
        SimWorld world = SimFactory.CreatePhase0(Config, new InMemoryLogSink()).World;
        int winters = world.GoodsCatalog[Goods.Clothing].CoatWinters;
        int adults = world.Villagers.Count(v => v.Alive && v.CanWork);
        int aYearsWear = (adults + winters - 1) / winters;
        _output.WriteLine($"{adults} adults, a coat lasts {winters} winters, a year's wear {aYearsWear}.");

        Assert.True(adults > 1, "The fixture has too few adults to count.");
        Assert.Equal(adults + aYearsWear, LabourQuota.ClothingWanted(world));
        Assert.Equal(adults + aYearsWear, LabourQuota.ClothingShortfall(world));

        world.Villagers.First(v => v.CanWork).CoatDays = 10;
        Assert.Equal(adults - 1 + aYearsWear, LabourQuota.ClothingShortfall(world));

        world.AnyStoreOf(StoreKind.Warehouse).Store.Add(Goods.Clothing, 2);
        Assert.Equal(adults - 1 + aYearsWear - 2, LabourQuota.ClothingShortfall(world));

        world.AnyStoreOf(StoreKind.Warehouse).Store.Add(Goods.Clothing, 1_000);
        Assert.Equal(0, LabourQuota.ClothingShortfall(world));
    }
}

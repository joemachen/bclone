using Bclone.Sim.Config;
using Bclone.Sim.Core;
using Bclone.Sim.Determinism;
using Bclone.Sim.Logging;
using Bclone.Sim.World;
using Xunit;
using Xunit.Abstractions;

namespace Bclone.Sim.Tests;

/// <summary>
/// `specs/clothing.md §3.5–3.6` — the tailor's shop (C2, D659): leather sewn into coats in the mill's batch shape,
/// learned at fifty leather handled, wanted against the clothing shortfall. The coat itself is `ClothingTests`.
/// </summary>
public sealed class TailorTests
{
    private readonly ITestOutputHelper _output;

    public TailorTests(ITestOutputHelper output) => _output = output;

    private static SimConfig Config => VillageFixtures.Village;

    private static StoreBuilding TheWarehouse(SimWorld world) => world.AnyStoreOf(StoreKind.Warehouse);

    /// <summary>A standing tailor's shop, the unlock met first (the mill's `RaiseA` sets the wheat; this sets the leather).</summary>
    internal static Workplace RaiseAShop(SimWorld world)
    {
        world.LeatherEverHeld = Math.Max(world.LeatherEverHeld, world.Config.TailorUnlockLeather);
        return FoodChainTests.RaiseA(world, BuildingKind.TailorShop);
    }

    // ---------------------------------------------------------------
    //  §3.5 — the batch
    // ---------------------------------------------------------------

    [Fact]
    public void ATailorSewsLeatherIntoCoats()
    {
        SimConfig config = Config;
        SimLoop loop = SimFactory.CreatePhase0(config, new InMemoryLogSink());
        SimWorld world = loop.World;

        Workplace shop = RaiseAShop(world);
        ToolsTests.KeepTheVillageWarmAndFed(world);
        FoodChainTests.OnlyOneTradeWorks(world, JobKind.Tailor);
        TheWarehouse(world).Store.Add(Goods.Leather, 100);
        int leatherBefore = world.InStores(Goods.Leather);

        int sewingTicks = 0;
        for (int tick = 0; tick < config.TicksPerSeason && world.CoatsEverSewn < 4; tick++)
        {
            loop.StepOnce();
            sewingTicks += world.Villagers.Count(v => v.State == VillagerState.Sewing && v.Tile == shop.Tile);
        }

        int leatherSpent = leatherBefore - world.InStores(Goods.Leather);
        int coated = world.Villagers.Count(v => v.Alive && v.CoatDays > 0);
        int onShelves = world.InStores(Goods.Clothing);
        _output.WriteLine($"{world.CoatsEverSewn} coats sewn from {leatherSpent} leather; {coated} on backs + {onShelves} on the shelves; {sewingTicks} villager-ticks at the bench.");

        Assert.True(world.CoatsEverSewn > 0, "A staffed shop with leather in the warehouse sewed nothing.");
        Assert.Equal(world.CoatsEverSewn * config.LeatherPerCoat, leatherSpent);

        // ⛔ The recipe, to the unit: a tool quickens a sew and never makes a coat from nothing. Every coat sewn is on a
        // back or a shelf — the adults fetch them as they are made (`ClothingTests`).
        Assert.Equal(world.CoatsEverSewn, coated + onShelves);
        Assert.True(sewingTicks >= world.CoatsEverSewn, "The coats came without the tailor being seen at the bench.");
    }

    /// <summary>With no clothing limit set, the shop sews toward what the shelves should hold and stops there (§3.5); a set limit is the ceiling.</summary>
    [Fact]
    public void TheShopStopsWhenEveryAdultHasACoatAndAYearsWearIsOnTheShelf()
    {
        SimWorld world = SimFactory.CreatePhase0(Config, new InMemoryLogSink()).World;
        Workplace shop = RaiseAShop(world);
        ToolsTests.KeepTheVillageWarmAndFed(world);
        TheWarehouse(world).Store.Add(Goods.Leather, 100);

        Assert.Null(world.StockLimits.For(Goods.Clothing));
        Assert.Null(world.WhyTheBatchWaits(shop));

        int wanted = LabourQuota.ClothingWanted(world);
        TheWarehouse(world).Store.Add(Goods.Clothing, wanted);
        string? waits = world.WhyTheBatchWaits(shop);
        _output.WriteLine($"wanted {wanted}: {waits}");
        Assert.NotNull(waits);
        Assert.Contains("every adult has a coat", waits, StringComparison.Ordinal);

        // The player's ceiling outranks the derived want (D62): three asked for, three held, and the needle stills.
        TheWarehouse(world).Store.TakeAll(Goods.Clothing);
        world.SetStockLimit(Goods.Clothing, 3);
        Assert.Null(world.WhyTheBatchWaits(shop));
        TheWarehouse(world).Store.Add(Goods.Clothing, 3);
        Assert.Contains("asked the village to keep", world.WhyTheBatchWaits(shop), StringComparison.Ordinal);
    }

    /// <summary>A shop with no leather in reach says so, in the batch's words.</summary>
    [Fact]
    public void AShopWithNoLeatherSaysWhatItIsShortOf()
    {
        SimWorld world = SimFactory.CreatePhase0(Config, new InMemoryLogSink()).World;
        Workplace shop = RaiseAShop(world);
        ToolsTests.KeepTheVillageWarmAndFed(world);
        foreach (StoreBuilding store in world.StoreBuildings)
        {
            store.Store.TakeAll(Goods.Leather);
        }

        string? waits = world.WhyTheBatchWaits(shop);
        _output.WriteLine(waits);
        Assert.NotNull(waits);
        Assert.Contains("leather", waits, StringComparison.Ordinal);
        Assert.Contains($"{world.Config.LeatherPerCoat} ", waits, StringComparison.Ordinal);
    }

    // ---------------------------------------------------------------
    //  §3.6 — the unlock
    // ---------------------------------------------------------------

    [Fact]
    public void TheShopIsLearnedAtFiftyLeatherAndItIsAStopNotAGift()
    {
        SimWorld world = SimFactory.CreatePhase0(Config, new InMemoryLogSink()).World;
        string? before = world.WhyNotYet(BuildingKind.TailorShop);
        _output.WriteLine(before);
        Assert.Contains("leather", before, StringComparison.Ordinal);
        Assert.Equal(50, world.Config.TailorUnlockLeather);

        world.LeatherEverHeld = world.Config.TailorUnlockLeather - 1;
        Assert.False(world.IsUnlocked(BuildingKind.TailorShop), "One hide short and it was known.");

        world.Moments.Clear();
        world.HeldLeather(1);

        Assert.True(world.IsUnlocked(BuildingKind.TailorShop));
        Moment learned = Assert.Single(world.Moments);
        _output.WriteLine($"{learned.Title}: {learned.Body}");
        Assert.Contains("sew", learned.Title, StringComparison.Ordinal);
        Assert.True(learned.WaitsToBeDismissed);
        Assert.False(world.AFreeSmithyIsOwed);

        // Once learned, learned: the next hide raises no second moment.
        world.HeldLeather(10);
        Assert.Single(world.Moments);
    }

    /// <summary>The hunt's hide counts toward the shop — counted where it comes in, like the reap.</summary>
    [Fact]
    public void AHuntsHideCountsTowardTheShop()
    {
        SimConfig config = Config;
        SimLoop loop = SimFactory.CreatePhase0(config, new InMemoryLogSink());
        SimWorld world = loop.World;
        HuntingTests.RaiseALodgeFor(world);
        Assert.Equal(0, world.LeatherEverHeld);

        for (int tick = 0; tick < config.TicksPerYear && world.LeatherEverHeld == 0; tick++)
        {
            loop.StepOnce();
        }

        _output.WriteLine($"leather handled {world.LeatherEverHeld} after the first hunt.");
        Assert.True(world.LeatherEverHeld > 0, "A year of hunting handled no leather.");
    }

    /// <summary>Leather handled is the village's identity, sparsely: it decides what may be built. Coats sewn is a statistic and is not.</summary>
    [Fact]
    public void TheLeatherCounterIsInTheFingerprintAndTheCoatCountIsNot()
    {
        SimWorld world = SimFactory.CreatePhase0(Config, new InMemoryLogSink()).World;
        ulong before = StateHash.Compute(world);

        world.CoatsEverSewn = 7;
        Assert.Equal(before, StateHash.Compute(world));

        world.LeatherEverHeld = 1;
        Assert.NotEqual(before, StateHash.Compute(world));
    }

    // ---------------------------------------------------------------
    //  §3.5 — the quota
    // ---------------------------------------------------------------

    [Fact]
    public void TailorsAreWantedOnlyWhileAdultsGoUncoatedAndTheShopCanSew()
    {
        SimWorld world = SimFactory.CreatePhase0(Config, new InMemoryLogSink()).World;
        ToolsTests.KeepTheVillageWarmAndFed(world);

        // No shop: nobody, however short the village is of coats.
        Assert.True(LabourQuota.ClothingShortfall(world) > 0);
        Assert.Equal(0, LabourQuota.TailorsWanted(world));

        Workplace shop = RaiseAShop(world);
        foreach (StoreBuilding store in world.StoreBuildings)
        {
            store.Store.TakeAll(Goods.Leather);
        }

        // A shop with nothing to sew wants nobody — the batch waits, and a seat there would sit idle.
        Assert.Equal(0, LabourQuota.TailorsWanted(world));

        TheWarehouse(world).Store.Add(Goods.Leather, 100);
        int wanted = LabourQuota.TailorsWanted(world);
        _output.WriteLine($"shortfall {LabourQuota.ClothingShortfall(world)}, a tailor sews {VillageEconomy.CoatsSewnPerYearAtWorst(world.Config)} a year: {wanted} wanted of {shop.Capacity} seats.");
        Assert.True(wanted > 0, "Adults without coats, leather on the shelf, and no tailor wanted.");
        Assert.True(wanted <= shop.Capacity);

        // Every adult coated and a year's wear on the shelf: nobody.
        foreach (Villager villager in world.Villagers.Where(v => v.CanWork))
        {
            villager.CoatDays = 10;
        }

        TheWarehouse(world).Store.Add(Goods.Clothing, 1_000);
        Assert.Equal(0, LabourQuota.ClothingShortfall(world));
        Assert.Equal(0, LabourQuota.TailorsWanted(world));
    }

    // ---------------------------------------------------------------
    //  § Rows
    // ---------------------------------------------------------------

    [Fact]
    public void TheTailorsRowsAreAsShipped()
    {
        SimConfig shipped = ShippedConfig.Load();
        SimWorld world = SimFactory.CreatePhase0(shipped, new InMemoryLogSink()).World;

        Assert.Equal(BuildingKind.TailorShop, world.JobsCatalog.WorksAt(JobKind.Tailor));
        Assert.Equal(JobKind.Tailor, world.BuildingsCatalog.EmployedBy(BuildingKind.TailorShop));
        Assert.True(world.JobsCatalog.UsesTool(JobKind.Tailor));
        Assert.Equal(Goods.Clothing, world.JobsCatalog.LimitedBy(JobKind.Tailor));

        Assert.NotNull(world.BatchFor(JobKind.Tailor));
        Batch batch = world.BatchFor(JobKind.Tailor)!.Value;
        Assert.Equal(Goods.Leather, batch.Input);
        Assert.Equal(Goods.Clothing, batch.Output);
        Assert.Equal(1, batch.OutputPerBatch);
        Assert.Equal(0, batch.Firing);
        Assert.Equal(shipped.LeatherPerCoat, batch.InputPerBatch);
        Assert.Equal(2, shipped.LeatherPerCoat); // 5 until D666.

        Assert.Contains(shipped.Skills, s => s.GrownBy == JobKind.Tailor && s.Name == "tailoring");
        Assert.Contains(shipped.TechNodeRows, n => n.Unlocks == BuildingKind.TailorShop && n.Condition == TechCondition.LeatherHeld);
    }
}

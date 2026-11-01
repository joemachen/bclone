using Bclone.Sim.Config;
using Bclone.Sim.Core;
using Bclone.Sim.Logging;
using Bclone.Sim.World;
using Xunit;
using Xunit.Abstractions;

namespace Bclone.Sim.Tests;

/// <summary>
/// `specs/clothing.md §3.5`, D664 — how hides leave the lodge. Joe's Year-54 village held 50 hides in the lodge and
/// none in any store for two years, with a tailor who had nothing to sew: the carrier the test had sent for the hides
/// took the meat scraps instead. Two rules: the armful is the good that sent the carrier (A), and a coat's worth of
/// hides is worth the walk when a standing shop is short of them (B).
/// </summary>
public sealed class LeatherOutOfTheLodgeTests
{
    private readonly ITestOutputHelper _output;

    public LeatherOutOfTheLodgeTests(ITestOutputHelper output) => _output = output;

    private static SimConfig Config => VillageFixtures.Village;

    private static StoreBuilding TheWarehouse(SimWorld world) => world.AnyStoreOf(StoreKind.Warehouse);

    /// <summary>
    /// Joe's lodge, posed the way the fisher's clearing guards pose a hut: the village settled a year, one marketer and no
    /// hunter (so no hunt refills the lodge and the marketer's leg is the only carrier, as in his Year-54 village), every
    /// larder full, half the shelves cleared so storage has room (D370's premise, as the fisher's guard makes it).
    /// </summary>
    private static (SimLoop Loop, Workplace Lodge) JoesLodge(int meat, int leather)
    {
        SimConfig config = Config;
        SimLoop loop = SimFactory.CreatePhase0(config, new InMemoryLogSink());
        SimWorld world = loop.World;
        Workplace lodge = HuntingTests.RaiseALodgeFor(world);
        loop.Step(config.TicksPerYear + 1);

        foreach (JobKind kind in JobLimits.Kinds)
        {
            world.SetJobLimit(kind, kind == JobKind.Marketer ? 1 : 0);
        }

        foreach (Household household in world.Households)
        {
            int wanted = world.TargetFoodFor(household);
            if (world.FoodIn(household.Stockpile) < wanted)
            {
                household.Stockpile.Add(Goods.Produce, wanted);
            }
        }

        foreach (StoreBuilding store in world.StoreBuildings)
        {
            foreach (Goods goods in new[] { Goods.Produce, Goods.Logs, Goods.Firewood, Goods.Meat, Goods.Leather })
            {
                store.Store.TryTake(goods, store.Store[goods] / 2);
            }
        }

        lodge.Store.TakeAll(Goods.Meat);
        lodge.Store.TakeAll(Goods.Leather);
        lodge.Store.Add(Goods.Meat, meat);
        lodge.Store.Add(Goods.Leather, leather);
        Assert.Equal(meat, lodge.Store[Goods.Meat]);
        Assert.Equal(leather, lodge.Store[Goods.Leather]);
        Assert.True(world.SomewhereToPut(Goods.Leather) && world.SomewhereToPut(Goods.Meat), "the premise: storage with room for both");
        return (loop, lodge);
    }

    private static void NoLeatherOnAnyShelf(SimWorld world)
    {
        foreach (StoreBuilding store in world.StoreBuildings)
        {
            store.Store.TakeAll(Goods.Leather);
        }
    }

    // ---------------------------------------------------------------
    //  Rule A — the armful is the good that sent the carrier
    // ---------------------------------------------------------------

    /// <summary>Joe's lodge: 29 meat and 50 hides. The test names the hides; the carrier takes the hides and leaves the scraps.</summary>
    [Fact]
    public void TheCarrierTakesTheHidesTheTestNamedAndLeavesTheMeatScraps()
    {
        // Meat scraps below an armful, hides above it (the fixture's armful is 80; Joe's shipped one is 40).
        int hides = Config.CarryCapacity + 10;
        (SimLoop loop, Workplace lodge) = JoesLodge(meat: 29, leather: hides);
        SimWorld world = loop.World;
        int storesBefore = world.InStores(Goods.Leather);
        Assert.Equal(Goods.Leather, world.GoodWorthClearing(lodge));

        int tick = 0;
        for (; tick < world.Config.TicksPerSeason * 2 && world.InStores(Goods.Leather) == storesBefore; tick++)
        {
            loop.StepOnce();
        }

        _output.WriteLine($"tick {tick}: stores gained {world.InStores(Goods.Leather) - storesBefore} leather; the lodge holds {lodge.Store[Goods.Leather]} leather and {lodge.Store[Goods.Meat]} meat.");
        Assert.Equal(storesBefore + world.Config.CarryCapacity, world.InStores(Goods.Leather));
        Assert.Equal(29, lodge.Store[Goods.Meat]);
        Assert.Equal(10, lodge.Store[Goods.Leather]);
    }

    /// <summary>A food buffer clears as it always did (D362): the test names the meat and the carrier takes it.</summary>
    [Fact]
    public void AFoodBufferClearsAsItAlwaysDid()
    {
        int meat = Config.CarryCapacity + 20;
        (SimLoop loop, Workplace lodge) = JoesLodge(meat: meat, leather: 50);
        SimWorld world = loop.World;
        int meatBefore = world.InStores(Goods.Meat);
        Assert.Equal(Goods.Meat, world.GoodWorthClearing(lodge));

        int tick = 0;
        for (; tick < world.Config.TicksPerSeason * 2 && world.InStores(Goods.Meat) <= meatBefore; tick++)
        {
            loop.StepOnce();
        }

        _output.WriteLine($"tick {tick}: stores gained {world.InStores(Goods.Meat) - meatBefore} meat; the lodge holds {lodge.Store[Goods.Meat]} meat and {lodge.Store[Goods.Leather]} leather.");
        Assert.True(world.InStores(Goods.Meat) > meatBefore, "The meat the test named did not reach a store in two seasons.");
        Assert.Equal(50, lodge.Store[Goods.Leather]);
    }

    // ---------------------------------------------------------------
    //  Rule B — a coat's worth is worth the walk when the shop is short
    // ---------------------------------------------------------------

    [Fact]
    public void AHideIsWorthTheWalkAtACoatsWorthOnceAShopStandsAndTheShelvesAreBare()
    {
        SimWorld world = SimFactory.CreatePhase0(Config, new InMemoryLogSink()).World;
        Workplace lodge = HuntingTests.RaiseALodgeFor(world);
        NoLeatherOnAnyShelf(world);
        lodge.Store.Add(Goods.Leather, 10);
        int aCoat = world.Config.LeatherPerCoat;
        Assert.True(10 < world.Config.CarryCapacity && 10 >= aCoat, "The pose needs one hunt's hide: short of an armful, a coat or more.");

        // No shop: a hide short of an armful is nobody's walk (D370).
        Assert.Null(world.GoodWorthClearing(lodge));

        // A shop stands and the shelves are bare: the hides are worth the walk.
        TailorTests.RaiseAShop(world);
        NoLeatherOnAnyShelf(world);
        Assert.Equal(Goods.Leather, world.GoodWorthClearing(lodge));

        // A coat's worth on a shelf, and the shop is not short.
        TheWarehouse(world).Store.Add(Goods.Leather, aCoat);
        Assert.Null(world.GoodWorthClearing(lodge));

        // Less than a coat's worth in the lodge is not worth the walk either.
        NoLeatherOnAnyShelf(world);
        lodge.Store.TakeAll(Goods.Leather);
        lodge.Store.Add(Goods.Leather, aCoat - 1);
        Assert.Null(world.GoodWorthClearing(lodge));

        // An armful is worth the walk as ever, shop or none.
        lodge.Store.Add(Goods.Leather, world.Config.CarryCapacity);
        TheWarehouse(world).Store.Add(Goods.Leather, 1_000);
        Assert.Equal(Goods.Leather, world.GoodWorthClearing(lodge));
    }

    /// <summary>
    /// Joe's Greyford, Year 26: 1,800 meat in the lodge all year with the hunters at work, so "food first" named the meat on
    /// every trip and 16 hides waited. A shop starving for its input gets one armful of hides, then the meat again (D665).
    /// </summary>
    [Fact]
    public void AStarvingShopOutranksARoutineFoodArmfulOnce()
    {
        SimWorld world = SimFactory.CreatePhase0(Config, new InMemoryLogSink()).World;
        Workplace lodge = HuntingTests.RaiseALodgeFor(world);
        TailorTests.RaiseAShop(world);
        NoLeatherOnAnyShelf(world);
        lodge.Store.Add(Goods.Meat, Config.CarryCapacity * 4);
        lodge.Store.Add(Goods.Leather, 10);
        Assert.True(world.SomewhereToPut(Goods.Meat) && world.SomewhereToPut(Goods.Leather), "the premise: room for both");

        Assert.Equal(Goods.Leather, world.GoodWorthClearing(lodge));

        // A coat's worth on the shelf, and the meat is the walk again.
        TheWarehouse(world).Store.Add(Goods.Leather, world.Config.LeatherPerCoat);
        Assert.Equal(Goods.Meat, world.GoodWorthClearing(lodge));
    }

    /// <summary>The marketer's errands count the lodge once the shop stands (D370's leg, `LabourQuota.MarketersWanted`).</summary>
    [Fact]
    public void MarketersAreWantedForTheHidesOnceAShopStands()
    {
        SimWorld world = SimFactory.CreatePhase0(Config, new InMemoryLogSink()).World;
        ToolsTests.KeepTheVillageWarmAndFed(world);
        Workplace lodge = HuntingTests.RaiseALodgeFor(world);
        NoLeatherOnAnyShelf(world);
        lodge.Store.Add(Goods.Leather, 10);
        int before = LabourQuota.MarketersWanted(world);

        TailorTests.RaiseAShop(world);
        NoLeatherOnAnyShelf(world);

        int after = LabourQuota.MarketersWanted(world);
        _output.WriteLine($"marketers wanted: {before} before the shop, {after} after.");
        Assert.True(after > before, "A lodge the shop is short of hides from asked for no marketer.");
    }

    /// <summary>Food is never a shop's shortage: a hungry village is the floor's business (D362), and a meat buffer below an armful stays nobody's walk.</summary>
    [Fact]
    public void FoodIsNeverAShopsShortage()
    {
        SimWorld world = SimFactory.CreatePhase0(Config, new InMemoryLogSink()).World;
        Workplace lodge = HuntingTests.RaiseALodgeFor(world);
        TailorTests.RaiseAShop(world);
        lodge.Store.Add(Goods.Meat, 10);

        Assert.False(world.AStandingShopIsShortOf(Goods.Meat, 10));
        Assert.Null(world.GoodWorthClearing(lodge));
    }

    // ---------------------------------------------------------------
    //  D665 — a scrap of another food does not keep the meat in
    // ---------------------------------------------------------------

    /// <summary>
    /// Joe's Greyford lodge, Year 25: 11 forage and 5 fish a hunter had put down with his catch, 2,622 meat, an empty
    /// granary set to meat — and nobody came, because the test asked only the first food it found. Every food is asked.
    /// </summary>
    [Fact]
    public void AScrapOfAnotherFoodDoesNotKeepTheMeatIn()
    {
        int meat = Config.CarryCapacity * 3;
        (SimLoop loop, Workplace lodge) = JoesLodge(meat: meat, leather: 0);
        SimWorld world = loop.World;
        lodge.Store.Add(Goods.Produce, 11);
        lodge.Store.Add(Goods.Fish, 5);
        int meatBefore = world.InStores(Goods.Meat);

        Assert.Equal(Goods.Meat, world.GoodWorthClearing(lodge));

        int tick = 0;
        for (; tick < world.Config.TicksPerSeason * 2 && world.InStores(Goods.Meat) <= meatBefore; tick++)
        {
            loop.StepOnce();
        }

        _output.WriteLine($"tick {tick}: stores gained {world.InStores(Goods.Meat) - meatBefore} meat; the lodge holds {lodge.Store[Goods.Meat]} meat, {lodge.Store[Goods.Produce]} forage, {lodge.Store[Goods.Fish]} fish.");
        Assert.True(world.InStores(Goods.Meat) > meatBefore, "Eleven forage in the lodge kept the meat in for two seasons.");
    }
}

using Kith.Sim.Config;
using Kith.Sim.Core;
using Kith.Sim.Logging;
using Kith.Sim.World;
using Xunit;
using Xunit.Abstractions;

namespace Kith.Sim.Tests;

/// <summary>
/// W3 step 2 (`specs/tavern.md §5.1–§5.3`, §5.5, D834–D836): ale a good, the brewery W1's fifth batch row, learned with
/// barley, brewing toward the taverns' season — and the brewer <b>hired by the village</b> from shipped rows, the finding
/// <see cref="FifthBatchTradeTests"/> left for W3 (a modded trade is never seated; a shipped one must be).
/// </summary>
public sealed class BreweryTests
{
    private readonly ITestOutputHelper _output;

    public BreweryTests(ITestOutputHelper output) => _output = output;

    private static SimConfig Config => VillageFixtures.Village;

    /// <summary>
    /// Ale is not food, and is kept in the granary beside its barley (Joe, D838) — never the market's counter — and a granary
    /// holding it counts no more food for it.
    /// </summary>
    [Fact]
    public void AleIsNotFoodAndIsKeptInTheGranary()
    {
        var goods = new GoodsCatalog(Config.GoodsCatalog);
        Assert.False(goods.Edible(Goods.Ale));
        Assert.Equal(0, goods[Goods.Ale].Nutrition);
        Assert.Contains(StoreKind.Granary, goods[Goods.Ale].StoredBy);
        Assert.DoesNotContain(StoreKind.Warehouse, goods[Goods.Ale].StoredBy);

        SimWorld world = SimFactory.CreatePhase0(Config, new InMemoryLogSink()).World;
        int food = world.FoodInGranaries();
        TheGranary(world).Store.Add(Goods.Ale, 20);
        Assert.Equal(food, world.FoodInGranaries());
        Assert.DoesNotContain(StoreKind.Market, goods[Goods.Ale].StoredBy);
        Assert.Equal("ale", goods.NameOf(Goods.Ale));
    }

    /// <summary>
    /// ⛔ A granary that may hold ale keeps ALL its room for food (Joe, D838: <i>"counts on the food side of that split"</i>) — ale
    /// is <c>kept_with_food</c>, so the granary is no shared roof, and food fills it to the brim with ale in it too. Red with the
    /// flag off: the D361/D409 split keeps half of every granary for everything else, in every village (nine goldens moved).
    /// </summary>
    [Fact]
    public void AGranaryThatMayHoldAleKeepsAllItsRoomForFood()
    {
        SimWorld world = SimFactory.CreatePhase0(Config, new InMemoryLogSink()).World;
        StoreBuilding granary = TheGranary(world);
        Assert.True(granary.Accepts(Goods.Ale), "the premise: a granary takes ale");
        granary.Store.Add(Goods.Ale, 20);
        Assert.Equal(granary.Store.FreeSpace, granary.RoomFor(Goods.Produce));
        Assert.Equal(granary.Store.FreeSpace, granary.RoomFor(Goods.Ale));
        int put = granary.Put(Goods.Produce, granary.Store.Capacity);
        _output.WriteLine($"{put} forage went into a granary holding 20 ale; {granary.Store.FreeSpace} left of {granary.Store.Capacity}");
        Assert.True(granary.Store.IsFull, "Food could not fill a granary that holds ale.");
    }

    /// <summary>The brewer's batch, read off the shipped row: twenty barley to ten ale, no firing (D836).</summary>
    [Fact]
    public void TheBrewersBatchIsReadOffItsRow()
    {
        SimWorld world = SimFactory.CreatePhase0(Config, new InMemoryLogSink()).World;
        Assert.Equal(
            new Batch(JobKind.Brewer, Goods.Barley, 20, Goods.Ale, 10, 0, 4, 4, "brew", "a brew"),
            world.BatchFor(JobKind.Brewer));
        Assert.Equal(BuildingKind.Brewery, world.JobsCatalog.WorksAt(JobKind.Brewer));
        Assert.Equal(BuildingKind.Tavern, world.JobsCatalog.WorksAt(JobKind.TavernKeeper));
    }

    /// <summary>
    /// ⭐ Learned with barley, at the forager's find (Joe's call 2A) — the brewery and the tavern together, as the bakery comes
    /// with the mill; the nodes in Lore known with them. Red with the gate removed (unlocked from the founding).
    /// </summary>
    [Fact]
    public void TheBreweryAndTheTavernAreLearnedWithBarley()
    {
        SimWorld world = SimFactory.CreatePhase0(Config, new InMemoryLogSink()).World;
        Assert.False(world.IsUnlocked(BuildingKind.Brewery));
        Assert.False(world.IsUnlocked(BuildingKind.Tavern));
        Assert.Contains("Nobody here brews yet", world.WhyNotYet(BuildingKind.Brewery), StringComparison.Ordinal);
        Assert.False(world.KnowsOf(Goods.Ale));

        world.BarleyLearned = true;
        Assert.True(world.IsUnlocked(BuildingKind.Brewery));
        Assert.True(world.IsUnlocked(BuildingKind.Tavern));
        Assert.True(world.KnowsOf(Goods.Ale));
        foreach (string id in new[] { "brewery", "tavern" })
        {
            TechNodeRow node = world.Config.TechNodeRows.Single(n => n.Id == id);
            Assert.Equal(TechState.Known, TechTree.StateOf(world, node).State);
        }
    }

    /// <summary>
    /// ⭐ Brewed to the player's stock limit, tavern or not (Joe, D838: <i>"i want ale to have a stock limit - not be determined
    /// by the tavern"</i>): with no tavern standing the copper runs; with the limit held it stops, and says the limit. Red with
    /// the old tavern rule back (no tavern, nothing to brew).
    /// </summary>
    [Fact]
    public void TheBreweryBrewsToTheLimitTavernOrNot()
    {
        SimConfig config = Config;
        SimWorld world = SimFactory.CreatePhase0(config, new InMemoryLogSink()).World;
        world.BarleyLearned = true;
        Workplace brewery = FoodChainTests.RaiseA(world, BuildingKind.Brewery);
        ToolsTests.KeepTheVillageWarmAndFed(world);
        TheGranary(world).Store.Add(Goods.Barley, 200);
        Assert.True(world.SetStockLimit(Goods.Ale, 30).Allowed);
        Assert.DoesNotContain(world.Workplaces, w => w.Kind == JobKind.TavernKeeper);

        Assert.Null(world.WhyTheBatchWaits(brewery));
        Assert.True(LabourQuota.BatchHandsWanted(world, world.Batches.For(JobKind.Brewer)!) > 0, "No brewer wanted below the limit.");

        TheGranary(world).Store.Add(Goods.Ale, 30);
        string? met = world.WhyTheBatchWaits(brewery);
        _output.WriteLine($"the limit held: {met}");
        Assert.Contains("you asked the village to keep 30", met, StringComparison.Ordinal);
        Assert.Equal(0, LabourQuota.BatchHandsWanted(world, world.Batches.For(JobKind.Brewer)!));
    }

    /// <summary>⛔ `FedFirst`: barley is food, so the copper is cold while the village is short of it (the miller's rule).</summary>
    [Fact]
    public void TheBreweryWaitsWhileTheVillageIsHungry()
    {
        SimConfig config = Config;
        SimWorld world = SimFactory.CreatePhase0(config, new InMemoryLogSink()).World;
        world.BarleyLearned = true;
        Workplace brewery = FoodChainTests.RaiseA(world, BuildingKind.Brewery);
        FoodChainTests.RaiseA(world, BuildingKind.Tavern);
        foreach (Goods good in world.GoodsCatalog.EdibleGoods)
        {
            foreach (Household household in world.Households)
            {
                household.Stockpile.TakeAll(good);
            }

            foreach (StoreBuilding store in world.StoreBuildings)
            {
                store.Store.TakeAll(good);
            }
        }

        TheGranary(world).Store.Add(Goods.Barley, config.BarleyPerBrew);
        Assert.True(LabourQuota.VillageIsShortOfFood(world), "The premise: the village is hungry.");
        Assert.Contains("needs its grain to eat", world.WhyTheBatchWaits(brewery), StringComparison.Ordinal);
        Assert.Equal(0, LabourQuota.BatchHandsWanted(world, world.Batches.For(JobKind.Brewer)!));
    }

    /// <summary>
    /// ⭐ <b>The brewer hired by the village from shipped rows</b> — no hand seated by the test, no job limit typed: a brewery and a
    /// tavern standing, barley in the granary, and within a season somebody walks to the copper and the warehouse holds ale.
    /// Red with the brewer out of <c>LabourAllocator.KindsInOrder</c> (D795's finding).
    /// </summary>
    [Fact]
    public void TheVillageHiresABrewerAndBrewsAle()
    {
        SimConfig config = Config;
        SimLoop loop = SimFactory.CreatePhase0(config, new InMemoryLogSink());
        SimWorld world = loop.World;
        world.BarleyLearned = true;
        Workplace brewery = FoodChainTests.RaiseA(world, BuildingKind.Brewery);
        FoodChainTests.RaiseA(world, BuildingKind.Tavern);
        ToolsTests.KeepTheVillageWarmAndFed(world);
        TheGranary(world).Store.Add(Goods.Barley, 400);
        Assert.True(LabourQuota.BatchHandsWanted(world, world.Batches.For(JobKind.Brewer)!) > 0, "No brewer wanted.");

        int aleBefore = world.HeldAgainstItsLimit(Goods.Ale);
        bool seated = false;
        bool brewed = false;
        for (int tick = 0; tick < 2 * config.TicksPerSeason && !(seated && brewed); tick++)
        {
            loop.StepOnce();
            seated |= brewery.WorkerIds.Count > 0;
            brewed |= world.Villagers.Any(v => v.State == VillagerState.WorkingABatch && v.BatchTrade == JobKind.Brewer);
        }

        loop.Step(config.TicksPerDay * 4);
        int made = world.HeldAgainstItsLimit(Goods.Ale) - aleBefore;
        _output.WriteLine($"seated {seated}, brewed {brewed}, ale made {made}, the brewery: {world.WhyTheBatchWaits(brewery) ?? "running"}");
        Assert.True(seated, "The village never hired a brewer.");
        Assert.True(brewed, "Nobody brewed at the brewery.");
        Assert.True(made > 0, "The brewery made no ale.");
        Assert.Equal(0, made % config.AlePerBrew);
    }

    private static StoreBuilding TheGranary(SimWorld world) =>
        world.StoreBuildings.First(s => s.Kind == StoreKind.Granary);
}

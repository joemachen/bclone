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

    /// <summary>Ale is not food, and is kept dry where flour is — never a granary (the birth gate) or the market's counter.</summary>
    [Fact]
    public void AleIsNotFoodAndIsKeptInTheWarehouse()
    {
        var goods = new GoodsCatalog(Config.GoodsCatalog);
        Assert.False(goods.Edible(Goods.Ale));
        Assert.Equal(0, goods[Goods.Ale].Nutrition);
        Assert.Contains(StoreKind.Warehouse, goods[Goods.Ale].StoredBy);
        Assert.DoesNotContain(StoreKind.Granary, goods[Goods.Ale].StoredBy);
        Assert.DoesNotContain(StoreKind.Market, goods[Goods.Ale].StoredBy);
        Assert.Equal("ale", goods.NameOf(Goods.Ale));
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
    /// ⭐ `TavernsSupplied` (§5.3), its three arms: with no tavern standing there is nothing to brew for; with one and the
    /// homes near it, the copper runs; with a season's drinking held, it stops — each said in a sentence. Under the
    /// shipped starting limit, which is the ceiling over it (D836).
    /// </summary>
    [Fact]
    public void TheBreweryBrewsForTheTavernsAndNoMore()
    {
        SimConfig config = Config;
        SimWorld world = SimFactory.CreatePhase0(config, new InMemoryLogSink()).World;
        world.BarleyLearned = true;
        Workplace brewery = FoodChainTests.RaiseA(world, BuildingKind.Brewery);
        ToolsTests.KeepTheVillageWarmAndFed(world);
        TheGranary(world).Store.Add(Goods.Barley, 200);

        string? noTavern = world.WhyTheBatchWaits(brewery);
        _output.WriteLine($"no tavern: {noTavern}");
        Assert.Contains("no tavern stands", noTavern, StringComparison.Ordinal);
        Assert.Equal(0, LabourQuota.AleWanted(world));

        FoodChainTests.RaiseA(world, BuildingKind.Tavern);
        int adults = world.Villagers.Count(v => v.CanWork && world.TavernWithinReachOf(v) is not null);
        Assert.True(adults > 0, "The premise: homes near the founding are within reach of a tavern beside it.");
        Assert.Equal(adults * LabourQuota.EveningsASeason(config), LabourQuota.AleWanted(world));
        Assert.Null(world.WhyTheBatchWaits(brewery));
        Assert.True(LabourQuota.AleShortfall(world) > 0);

        TheWarehouse(world).Store.Add(Goods.Ale, LabourQuota.AleWanted(world));
        string? enough = world.WhyTheBatchWaits(brewery);
        _output.WriteLine($"a season held: {enough}");
        Assert.Contains("enough for the season", enough, StringComparison.Ordinal);
        Assert.Equal(0, LabourQuota.AleShortfall(world));
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

    private static StoreBuilding TheWarehouse(SimWorld world) =>
        world.StoreBuildings.First(s => s.Kind == StoreKind.Warehouse);

    private static StoreBuilding TheGranary(SimWorld world) =>
        world.StoreBuildings.First(s => s.Kind == StoreKind.Granary);
}

using Kith.Sim.Config;
using Kith.Sim.Core;
using System.Text.Json.Nodes;
using Kith.Sim.Logging;
using Kith.Sim.Persistence;
using Kith.Sim.Systems;
using Kith.Sim.World;
using Xunit;
using Xunit.Abstractions;

namespace Kith.Sim.Tests;

/// <summary>
/// W14, the market stand — <c>specs/market-stand.md</c> (D851–D852).
/// </summary>
/// <remarks>
/// §5.2, call 1: <b>a marketer stocks the counter of the market they work at first.</b> Measured before it was written
/// (§4.3): a marketer's load went to the counter nearest the storehouse it came from, so a stall out among the far houses
/// stood with its seat filled and its shelf empty, and Oakhaven's far families walked <i>more</i> than with no stall.
/// The rule is the plan alone: the load already goes to the marketer's own counter (<c>StockingTheMarket</c> walks to their
/// workplace), which a real stall's keeper filling it over a season pins end to end.
/// </remarks>
public sealed class MarketStandTests
{
    private readonly ITestOutputHelper _output;

    public MarketStandTests(ITestOutputHelper output) => _output = output;

    private static SimConfig Config => ShippedConfig.Established();

    /// <summary>The established village's market, a second market raised beside it, and a marketer seated at the second.</summary>
    private static (SimWorld world, StoreBuilding first, StoreBuilding second, Villager marketer) TwoMarkets()
    {
        SimWorld world = SimFactory.CreatePhase0(Config, new InMemoryLogSink()).World;
        StoreBuilding first = world.StoreBuildings.Single(s => s.Kind == StoreKind.Market);
        Workplace secondWorks = FoodChainTests.RaiseA(world, BuildingKind.Market);
        StoreBuilding second = world.StoreAt(secondWorks.Tile)!;
        Assert.NotSame(first, second);
        Assert.Equal(StoreKind.Market, second.Kind);

        Villager marketer = world.Villagers.First(v => v.Alive && v.CanWork);
        marketer.WorkplaceId = secondWorks.Id;
        secondWorks.WorkerIds.Add(marketer.Id);
        return (world, first, second, marketer);
    }

    /// <summary>Every shelf of <paramref name="market"/> at a limit of nought but <paramref name="but"/>'s.</summary>
    private static void OnlyShortOf(SimWorld world, StoreBuilding market, Goods but, int limit, int holding)
    {
        for (int id = 0; id < world.GoodsCatalog.Count; id++)
        {
            var goods = (Goods)id;
            if (goods != but && world.GoodsCatalog.StoredBy(goods, StoreKind.Market))
            {
                world.SetMarketLimit(market, goods, 0);
            }
        }

        world.SetMarketLimit(market, but, limit);
        Assert.Equal(holding, market.Put(but, holding));
    }

    /// <summary>
    /// ⭐ The plan: the other market's firewood shelf is bare and this marketer's own meat shelf half full — today's
    /// emptiest-shelf rule (D601) took the bare one; their own counter comes first, so they go for meat.
    /// </summary>
    [Fact]
    public void AMarketerPlansForTheirOwnCounterFirst()
    {
        (SimWorld world, StoreBuilding first, StoreBuilding second, Villager marketer) = TwoMarkets();
        world.StoreBuildings.Single(s => s.Kind == StoreKind.Granary).Put(Goods.Meat, 400);
        world.StoreBuildings.Single(s => s.Kind == StoreKind.Warehouse).Put(Goods.Firewood, 400);
        OnlyShortOf(world, first, Goods.Firewood, 1000, 0);
        OnlyShortOf(world, second, Goods.Meat, 1000, 500);

        BehaviorSystem.MarketErrand? next = BehaviorSystem.PlanMarketErrand(world, marketer);
        _output.WriteLine($"{marketer.Name} plans {next?.Goods} from ({next?.Source.X},{next?.Source.Y})");

        Assert.Equal(Goods.Meat, next?.Goods);
    }

    /// <summary>With their own counter at its limits, a marketer stocks any market's emptiest shelf, as before (D601).</summary>
    [Fact]
    public void WithTheirOwnCounterFullAMarketerStocksAnyMarket()
    {
        (SimWorld world, StoreBuilding first, StoreBuilding second, Villager marketer) = TwoMarkets();
        world.StoreBuildings.Single(s => s.Kind == StoreKind.Granary).Put(Goods.Meat, 400);
        world.StoreBuildings.Single(s => s.Kind == StoreKind.Warehouse).Put(Goods.Firewood, 400);
        OnlyShortOf(world, first, Goods.Firewood, 1000, 0);
        OnlyShortOf(world, second, Goods.Meat, 500, 500);
        Assert.False(world.CounterWantsStocking(second));

        BehaviorSystem.MarketErrand? next = BehaviorSystem.PlanMarketErrand(world, marketer);
        _output.WriteLine($"{marketer.Name} plans {next?.Goods} from ({next?.Source.X},{next?.Source.Y})");

        Assert.Equal(Goods.Firewood, next?.Goods);
    }

    // ---------------------------------------------------------------
    //  §5.1 and §5.3 — the stall: the market's row at half its size, and its own marketer (D852)
    // ---------------------------------------------------------------

    /// <summary>The established village with a market stall raised near the founding, and the stall's workplace and counter.</summary>
    private static (SimLoop loop, SimWorld world, Workplace stall, StoreBuilding counter) AStall()
    {
        SimLoop loop = SimFactory.CreatePhase0(Config, new InMemoryLogSink());
        SimWorld world = loop.World;
        world.MarketStallLearned = true; // the long walk, taken as made (§5.4's own guards walk it)
        Workplace stall = FoodChainTests.RaiseA(world, BuildingKind.MarketStall);
        StoreBuilding counter = world.StoreAt(stall.Tile)!;
        return (loop, world, stall, counter);
    }

    /// <summary>
    /// ⭐ §5.1: a stall stores as a market — every rule reads <c>StoreKind.Market</c> — at half the market's room and half its
    /// starting limit a good, with one seat, a 2×1 footprint, a cart to own, and its workplace knowing it is a stall.
    /// </summary>
    [Fact]
    public void AStallIsAMarketAtHalfItsSize()
    {
        (_, SimWorld world, Workplace stall, StoreBuilding counter) = AStall();
        SimConfig c = world.Config;

        Assert.Equal(StoreKind.Market, counter.Kind);
        Assert.Equal(BuildingKind.MarketStall, counter.RaisedAs);
        Assert.Equal(c.MarketStallStoreCapacity, counter.Store.Capacity);
        Assert.Equal(c.MarketStallStartingLimit, counter.KeepsUpTo(Goods.Produce));
        Assert.Equal(JobKind.Marketer, stall.Kind);
        Assert.Equal(BuildingKind.MarketStall, world.BuildingKindOf(stall));
        Assert.Equal(c.MarketStallCapacity, stall.Places);
        Assert.Equal((2, 1), (stall.ExtentWidth, stall.ExtentHeight));
        Assert.True(stall.IsACartOwner);

        // And the market is still a market: its workplace says nothing, its counter keeps the market's limit.
        Workplace market = world.Workplaces.First(w => w.Kind == JobKind.Marketer && !w.IsSite && w.Id != stall.Id);
        Assert.Null(market.RaisedAs);
        Assert.Equal(BuildingKind.Market, world.BuildingKindOf(market));
        Assert.Equal(c.MarketStartingLimit, world.StoreAt(market.Tile)!.KeepsUpTo(Goods.Produce));
    }

    /// <summary>§5.1: a household beside a stocked stall shops there — D372's nearest stocked market, unchanged.</summary>
    [Fact]
    public void AShopperBesideAStockedStallShopsThere()
    {
        (_, SimWorld world, _, StoreBuilding counter) = AStall();
        Assert.Equal(world.Config.CarryCapacity, counter.Put(Goods.Produce, world.Config.CarryCapacity));

        Assert.Same(counter, BehaviorSystem.FoodShopFrom(world, counter.Tile, world.Config.CarryCapacity));
    }

    /// <summary>
    /// ⭐ §5.3, Joe's call (D852): <i>"a market stand should have its own marketer - only 1 though"</i>. The professions number
    /// typed at the market's seats (and at nought) leaves the stall its one keeper on top, seated at the stall.
    /// </summary>
    [Theory]
    [InlineData(0)]
    [InlineData(2)]
    public void AStallHasItsOwnMarketerOutsideTheProfessionsNumber(int typed)
    {
        (_, SimWorld world, Workplace stall, StoreBuilding counter) = AStall();
        world.StoreBuildings.Single(s => s.Kind == StoreKind.Granary).Put(Goods.Produce, 2000);
        Assert.True(world.CounterWantsStocking(counter), "the stall's counter wants nothing, so it asks for nobody");
        world.SetJobLimit(JobKind.Marketer, typed);

        LabourQuota quota = LabourQuota.For(world);
        LabourAllocator.Reshuffle(world);

        int atTheStall = world.Villagers.Count(v => v.Alive && v.HasJob && v.WorkplaceId == stall.Id);
        int atTheMarket = world.Villagers.Count(v => v.Alive && v.HasJob && v.WorkplaceId != stall.Id
            && world.FindWorkplace(v.WorkplaceId)?.Kind == JobKind.Marketer);
        _output.WriteLine($"typed {typed}: quota {quota.Marketers} of whom {quota.StallKeepers} at stalls; seated {atTheStall} at the stall, {atTheMarket} at the market");
        Assert.Equal(1, quota.StallKeepers);
        Assert.Equal(1, atTheStall);
        Assert.True(atTheMarket <= typed, $"{atTheMarket} at the market against the {typed} typed");
    }

    /// <summary>A stall with every shelf at its limit asks for nobody (D36: bounded by errands, never by spare hands).</summary>
    [Fact]
    public void AStallWithNothingToStockAsksForNobody()
    {
        (_, SimWorld world, _, StoreBuilding counter) = AStall();
        for (int id = 0; id < world.GoodsCatalog.Count; id++)
        {
            if (world.GoodsCatalog.StoredBy((Goods)id, StoreKind.Market))
            {
                world.SetMarketLimit(counter, (Goods)id, 0);
            }
        }

        Assert.False(world.CounterWantsStocking(counter));
        Assert.Equal(0, LabourQuota.StallKeepersWanted(world));
    }

    /// <summary>
    /// ⭐ End to end (§5.2 and §5.3 together): a stall raised in the established village, the professions number typed, a
    /// granary full — a season on, its own keeper has filled its counter with food.
    /// </summary>
    [Fact]
    public void AStallsOwnKeeperFillsItsCounter()
    {
        (SimLoop loop, SimWorld world, Workplace stall, StoreBuilding counter) = AStall();
        world.StoreBuildings.Single(s => s.Kind == StoreKind.Granary).Put(Goods.Produce, 2000);
        world.SetJobLimit(JobKind.Marketer, 1);

        loop.Step(world.Config.TicksPerSeason);

        int food = world.GoodsCatalog.EdibleGoods.Sum(g => counter.Store[g]);
        _output.WriteLine($"a season on: {food} food on the stall's counter, its keeper {world.Villagers.FirstOrDefault(v => v.WorkplaceId == stall.Id)?.Name ?? "nobody"}");
        Assert.True(food >= world.Config.CarryCapacity * world.Config.CartArmfuls,
            $"the stall's counter holds {food} food a season on — less than one cart-load");
    }

    /// <summary>§6: a stall's workplace comes back from a save still a stall, and the hash says so.</summary>
    [Fact]
    public void AStallIsSavedAsAStall()
    {
        (SimLoop loop, SimWorld world, Workplace stall, _) = AStall();
        ulong before = Determinism.StateHash.Compute(world);

        SimLoop again = Reload(loop);
        Workplace back = again.World.FindWorkplace(stall.Id)!;

        Assert.Equal(BuildingKind.MarketStall, back.RaisedAs);
        Assert.Equal(world.Config.MarketStallStartingLimit, again.World.StoreAt(back.Tile)!.KeepsUpTo(Goods.Produce));
        Assert.Equal(before, Determinism.StateHash.Compute(again.World));
    }

    /// <summary>§6: whether a workplace stands in a stall is in the fingerprint — the same village with the mark cleared hashes apart.</summary>
    [Fact]
    public void AStallIsInTheFingerprint()
    {
        (_, SimWorld world, Workplace stall, _) = AStall();
        ulong asAStall = Determinism.StateHash.Compute(world);
        stall.RaisedAs = null;

        Assert.NotEqual(asAStall, Determinism.StateHash.Compute(world));
    }

    // ---------------------------------------------------------------
    //  §5.4 — the long walk (D839, D852): five households, each once, a round trip of ten days
    // ---------------------------------------------------------------

    /// <summary>The established village's housed households and the store furthest from each — the long walk posed.</summary>
    private static (SimLoop loop, SimWorld world, List<Household> homes, StoreBuilding far) TheLongWalkPosed(SimConfig config)
    {
        SimLoop loop = SimFactory.CreatePhase0(config, new InMemoryLogSink());
        SimWorld world = loop.World;
        List<Household> homes = world.Households.Where(h => h.HomeTile is not null && world.LivingMembersOf(h) > 0).ToList();
        StoreBuilding far = world.StoreBuildings.OrderByDescending(s => world.TravelCost.TicksBetween(homes[0].HomeTile!.Value, s.Tile)).First();
        return (loop, world, homes, far);
    }

    /// <summary>A walker of the household with an armful of food in hand — what the collection put there.</summary>
    private static Villager WalkerOf(SimWorld world, Household household)
    {
        Villager walker = world.Villagers.First(v => v.Alive && v.HouseholdId == household.Id);
        walker.Carried.Receive(Goods.Produce, 10);
        return walker;
    }

    /// <summary>⭐ A household's long walk marks it once, and counts once however often it walks; a short one counts nothing.</summary>
    [Fact]
    public void ALongWalkMarksTheHouseholdOnce()
    {
        (_, SimWorld world, List<Household> homes, StoreBuilding far) = TheLongWalkPosed(Config with { MarketStallWalkTicks = 0 });
        Household home = homes[0];
        Villager walker = WalkerOf(world, home);

        world.WalkedToShop(home, walker, far);
        world.WalkedToShop(home, walker, far);

        Assert.True(home.WalkedFar);
        Assert.Equal(1, world.HouseholdsThatWalkedFar);
        Assert.False(world.MarketStallLearned);
    }

    /// <summary>A walk shorter than the knob marks nobody: the round trip is twice the walk from the home (§5.4, the planned walk).</summary>
    [Fact]
    public void AWalkShorterThanTheLongWalkCountsNothing()
    {
        (_, SimWorld world, List<Household> homes, StoreBuilding far) = TheLongWalkPosed(Config);
        Household home = homes[0];
        int roundTrip = 2 * world.TravelCost.TicksBetween(home.HomeTile!.Value, far.Tile);
        (_, world, homes, far) = TheLongWalkPosed(Config with { MarketStallWalkTicks = roundTrip + 1 });
        home = homes[0];

        world.WalkedToShop(home, WalkerOf(world, home), far);

        Assert.False(home.WalkedFar);
        Assert.Equal(0, world.HouseholdsThatWalkedFar);

        // ⭐ And at the knob exactly, it counts — the edge is "at least".
        (_, world, homes, far) = TheLongWalkPosed(Config with { MarketStallWalkTicks = roundTrip });
        world.WalkedToShop(homes[0], WalkerOf(world, homes[0]), far);
        Assert.Equal(1, world.HouseholdsThatWalkedFar);
    }

    /// <summary>
    /// ⭐ The fifth household teaches the stall: the build bar's refusal says how far before, the gate opens after, the Lore node
    /// is known, and the village is told who walked how far for what — Joe's sentence (D839).
    /// </summary>
    [Fact]
    public void TheFifthHouseholdTeachesTheStall()
    {
        (_, SimWorld world, List<Household> homes, StoreBuilding far) =
            TheLongWalkPosed(Config with { MarketStallWalkTicks = 0, MarketStallHouseholds = 2 });
        Assert.True(homes.Count >= 2, "the pose needs two households");
        TechNodeRow node = world.Config.TechNodeRows.Single(n => n.Unlocks == BuildingKind.MarketStall);

        world.WalkedToShop(homes[0], WalkerOf(world, homes[0]), far);
        Assert.False(world.IsUnlocked(BuildingKind.MarketStall));
        string? why = world.WhyNotYet(BuildingKind.MarketStall);
        _output.WriteLine(why);
        Assert.Contains("1 of the 2 households", why, StringComparison.Ordinal);
        Assert.NotEqual(TechState.Known, TechTree.StateOf(world, node).State);

        Villager walker = WalkerOf(world, homes[1]);
        world.WalkedToShop(homes[1], walker, far);

        Assert.True(world.MarketStallLearned);
        Assert.True(world.IsUnlocked(BuildingKind.MarketStall));
        Assert.Equal(TechState.Known, TechTree.StateOf(world, node).State);
        string said = string.Join(" | ", world.Moments.Select(m => m.Title + ": " + m.Body));
        _output.WriteLine(said);
        Assert.Contains($"{walker.Name} walked", said, StringComparison.Ordinal);
        Assert.Contains("the far houses ask for a stall", said, StringComparison.Ordinal);
    }

    /// <summary>⭐ End to end, through the counter: with the long walk at nought and one household, the first shopping trip teaches it.</summary>
    [Fact]
    public void TheFirstShoppingTripCountsAtTheCounter()
    {
        SimConfig config = Config with { MarketStallWalkTicks = 0, MarketStallHouseholds = 1 };
        SimLoop loop = SimFactory.CreatePhase0(config, new InMemoryLogSink());

        // ⚠️ No firewood in any store, so the trip that counts is a food trip: the counter is asked at both collections, and a
        // guard that either one satisfies scored zero against the food call site (D854's red-check).
        foreach (StoreBuilding store in loop.World.StoreBuildings)
        {
            store.Store.TakeAll(Goods.Firewood);
        }

        for (int tick = 0; tick < config.TicksPerYear && !loop.World.MarketStallLearned; tick++)
        {
            loop.StepOnce();
        }

        _output.WriteLine($"learned {loop.World.MarketStallLearned} by tick {loop.World.Tick}, {loop.World.HouseholdsThatWalkedFar} household(s)");
        Assert.True(loop.World.MarketStallLearned, "a year of shopping never reached the counter's count");
        Assert.Equal(1, loop.World.HouseholdsThatWalkedFar);

        // ⭐ And it was a food trip that counted — the moment names what was collected. With the stores' firewood taken, the
        // first collection is food; were the food call site gone, the first to count would be a firewood trip (M14).
        string said = string.Join(" | ", loop.World.Moments.Select(m => m.Body));
        _output.WriteLine(said);
        Assert.DoesNotContain("for firewood", said, StringComparison.Ordinal);
    }

    /// <summary>
    /// The Lore node reads the latch, not the count: a village that learned the stall and was then loaded with a higher knob
    /// (or whose count stopped at learning) still knows it — the workshop's rule, the latch is the state.
    /// </summary>
    [Fact]
    public void TheLoreNodeReadsTheLatch()
    {
        (_, SimWorld world, _, _) = TheLongWalkPosed(Config);
        TechNodeRow node = world.Config.TechNodeRows.Single(n => n.Unlocks == BuildingKind.MarketStall);
        world.MarketStallLearned = true;

        Assert.Equal(0, world.HouseholdsThatWalkedFar);
        Assert.Equal(TechState.Known, TechTree.StateOf(world, node).State);
    }

    /// <summary>
    /// §6: the latch is in the fingerprint; the count and the household's mark are not — the gap asserted on purpose (D676's
    /// shape: two villages at different counts below the gate hash alike).
    /// </summary>
    [Fact]
    public void TheLongWalksCountIsNotInTheFingerprint()
    {
        (_, SimWorld world, List<Household> homes, _) = TheLongWalkPosed(Config);
        ulong before = Determinism.StateHash.Compute(world);
        homes[0].WalkedFar = true;
        world.HouseholdsThatWalkedFar = 3;
        Assert.Equal(before, Determinism.StateHash.Compute(world));

        world.MarketStallLearned = true;
        Assert.NotEqual(before, Determinism.StateHash.Compute(world));
    }

    /// <summary>§6: the latch, the count and the household's mark come back from a save; a save without them reads nothing learned.</summary>
    [Fact]
    public void TheLongWalkIsSaved()
    {
        (SimLoop loop, SimWorld world, List<Household> homes, _) = TheLongWalkPosed(Config);
        SimLoop fresh = Reload(loop);
        Assert.False(fresh.World.MarketStallLearned);
        Assert.Equal(0, fresh.World.HouseholdsThatWalkedFar);

        homes[0].WalkedFar = true;
        world.HouseholdsThatWalkedFar = 4;
        world.MarketStallLearned = true;
        SimLoop again = Reload(loop);

        Assert.True(again.World.MarketStallLearned);
        Assert.Equal(4, again.World.HouseholdsThatWalkedFar);
        Assert.True(again.World.FindHousehold(homes[0].Id)!.WalkedFar);
    }

    private static SimLoop Reload(SimLoop loop)
    {
        JsonObject document = SaveGame.Capture(loop, "test", "2026-11-22 12:00", "test");
        return SaveGame.Load(JsonNode.Parse(SaveFile.TextOf(document))!.AsObject(), loop.World.Config, null);
    }
}

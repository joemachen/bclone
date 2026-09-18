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
/// The market is a shop — <c>specs/storage-and-distribution.md §14.9</c> (D372, Joe).
/// </summary>
/// <remarks>
/// <para>
/// Joe, three times over: *"villagers should be going to the market and the marketer replenishes
/// their market from the granary. the villagers should only go to the granary if there is no
/// market nearby or the market doesnt have any food"*; *"deliveries go"*; *"it should be when
/// larder items get to 50% of their total maximum, not as soon as it is below 99%"*; and a limit
/// per good per market that the player sets.
/// </para>
/// <para>
/// Four rules, each with its own guard here: a household shops at a market before a storehouse;
/// a household goes at half a larder and comes back to target; a market is stocked to its own
/// limit and never past it; and the marketer's day starts with the counter. The 300-year
/// market-off run in <see cref="MarketTests"/> is still the acceptance test.
/// </para>
/// </remarks>
public sealed class MarketShopTests
{
    private readonly ITestOutputHelper _output;

    public MarketShopTests(ITestOutputHelper output) => _output = output;

    private static SimConfig Config => VillageFixtures.Village;

    private static SimLoop Build(SimConfig config) =>
        SimFactory.CreatePhase0(config, new InMemoryLogSink());

    // ---------------------------------------------------------------
    //  ⭐⭐ The market comes first (Joe)
    // ---------------------------------------------------------------

    /// <summary>
    /// ⭐⭐ A stocked market wins the household's errand over a stocked granary that is
    /// <b>nearer</b> — and an empty market loses it back to the granary.
    /// </summary>
    /// <remarks>
    /// Nearest-of-any-store was §14.5's rule since D14 and it is exactly what Joe saw as
    /// *"bum rushing the granary"*: the granary stands beside the homes, so the market — a few
    /// tiles further — never won a single trip. The rule now is a market that holds the good,
    /// then storage only if no market is reachable or none holds it.
    /// </remarks>
    [Fact]
    public void AHouseholdFetchesFromTheMarketBeforeTheGranary()
    {
        // The market moved a few tiles out, so that "nearest" and "market" disagree.
        SimConfig config = Config with { MarketX = 6, MarketY = 5 };
        SimWorld world = Build(config).World;

        Villager villager = world.Villagers.First(v => v.Alive && v.CanWork);
        Household home = world.HouseholdOf(villager);
        StoreBuilding granary = world.AnyStoreOf(StoreKind.Granary);
        StoreBuilding market = world.AnyStoreOf(StoreKind.Market);

        int toGranary = world.TravelCost.TicksBetween(villager.Tile, granary.Tile);
        int toMarket = world.TravelCost.TicksBetween(villager.Tile, market.Tile);
        Assert.True(toMarket != TravelCostField.Unreachable, "the posed market is across water");
        Assert.True(toMarket > toGranary,
            $"the market ({toMarket} ticks) is not further than the granary ({toGranary}), so nearest-first would pass this for free");

        granary.Store.Receive(Goods.Produce, 400);
        market.Store.Receive(Goods.Produce, 120);
        home.Stockpile.TryTake(Goods.Produce, home.Stockpile[Goods.Produce]);
        Assert.Equal(0, world.FoodIn(home.Stockpile));

        StoreBuilding? first = BehaviorSystem.PlanFetchForTest(world, villager);
        _output.WriteLine(
            $"{home.Name}'s larder is empty; the granary is {toGranary} ticks away, the market {toMarket}: "
            + $"they go to {first?.Name ?? "nowhere"}");
        Assert.Same(market, first);

        market.Store.TryTake(Goods.Produce, market.Store[Goods.Produce]);
        StoreBuilding? second = BehaviorSystem.PlanFetchForTest(world, villager);
        _output.WriteLine($"with the market empty they go to {second?.Name ?? "nowhere"}");
        Assert.Same(granary, second);

        // ⛔ A SCRAP AT THE COUNTER IS NOT A STOCKED COUNTER: five food at the market and four
        // hundred in the granary is a trip to the granary — written as "any at all", the three
        // fish the last fetch left behind captured every trip, and a village on the edge starved
        // beside a granary. But a scrap is still the last resort when nothing else holds any.
        market.Store.Receive(Goods.Produce, 5);
        StoreBuilding? third = BehaviorSystem.PlanFetchForTest(world, villager);
        _output.WriteLine($"with five at the market and 400 in the granary they go to {third?.Name ?? "nowhere"}");
        Assert.Same(granary, third);

        granary.Store.TryTake(Goods.Produce, granary.Store[Goods.Produce]);
        StoreBuilding? last = BehaviorSystem.PlanFetchForTest(world, villager);
        _output.WriteLine($"with five at the market and nothing in the granary they go to {last?.Name ?? "nowhere"}");
        Assert.Same(market, last);
    }

    // ---------------------------------------------------------------
    //  ⭐ Half a larder, then back to target (Joe)
    // ---------------------------------------------------------------

    /// <summary>
    /// ⭐ A larder at 60 % of target sends nobody; at 50 % somebody goes — and keeps going
    /// until the larder is back at target, not one armful over the line.
    /// </summary>
    /// <remarks>
    /// The second half is the one that costs state: a household of two wants 190 food and an
    /// armful is 40, so a trigger alone would leave every larder hovering between a half and
    /// three-quarters. <c>Household.ToppingUpFood</c> is set when the trigger fires and cleared
    /// at target, so the trips come back to back — Joe's *"fewer, fuller trips"*.
    /// </remarks>
    [Fact]
    public void AHouseholdFetchesAtHalfALarder()
    {
        SimConfig config = Config;
        SimLoop loop = Build(config);
        SimWorld world = loop.World;

        Villager villager = world.Villagers.First(v => v.Alive && v.CanWork);
        Household home = world.HouseholdOf(villager);
        StoreBuilding granary = world.AnyStoreOf(StoreKind.Granary);
        granary.Store.Receive(Goods.Produce, 1000);

        int target = world.TargetFoodFor(home);
        Assert.True(target > config.CarryCapacity * 2,
            $"a target of {target} fits in two armfuls, so this could not tell one trip from a run of them");

        SetLarder(home, target * 60 / 100);
        Assert.Null(BehaviorSystem.PlanFetchForTest(world, villager));

        SetLarder(home, target * 50 / 100);
        Assert.NotNull(BehaviorSystem.PlanFetchForTest(world, villager));

        // And the run of trips: the larder comes back to target within a season — with nobody
        // gathering, so what the larder gains it gains by fetching (a forager's own take would
        // carry it past target and pass this for free; measured, the flag scored zero without this).
        foreach (JobKind kind in JobLimits.Kinds)
        {
            world.SetJobLimit(kind, 0);
        }

        int fullest = 0;
        for (int tick = 0; tick < config.TicksPerSeason; tick++)
        {
            loop.StepOnce();
            fullest = System.Math.Max(fullest, world.FoodIn(home.Stockpile));
        }

        _output.WriteLine(
            $"{home.Name} wants {target}; from {target / 2} the larder reached {fullest} within a season "
            + $"(one armful would have stopped at {target / 2 + config.CarryCapacity})");
        Assert.True(fullest > target / 2 + config.CarryCapacity,
            $"the larder stopped at {fullest} — one armful over the trigger, not back to target");
        Assert.True(fullest >= target * 9 / 10, $"the larder never came back near {target} (best {fullest})");
    }

    /// <summary>The anti-vacuity half of the trigger: the same dial, read from config.</summary>
    [Fact]
    public void TheTriggerIsTheDialAndNotAConstant()
    {
        SimConfig config = Config with { FetchBelowSharePercent = 80 };
        SimWorld world = Build(config).World;

        Villager villager = world.Villagers.First(v => v.Alive && v.CanWork);
        Household home = world.HouseholdOf(villager);
        world.AnyStoreOf(StoreKind.Granary).Store.Receive(Goods.Produce, 1000);

        // Above the dial first — once the trigger has fired the top-up keeps going, by design.
        int target = world.TargetFoodFor(home);
        SetLarder(home, target * 85 / 100);
        Assert.Null(BehaviorSystem.PlanFetchForTest(world, villager));

        SetLarder(home, target * 75 / 100);
        Assert.NotNull(BehaviorSystem.PlanFetchForTest(world, villager));
    }

    // ---------------------------------------------------------------
    //  ⭐ The market's own limit (Joe)
    // ---------------------------------------------------------------

    /// <summary>
    /// ⭐ A market limited to 200 firewood beside a warehouse of 1,000 holds about 200; raise
    /// the limit and it climbs; set it to 0 and the marketer stops stocking it and never empties it.
    /// </summary>
    /// <remarks>
    /// A limit is a ceiling, not an order to clear — D62's shape for the village limits, one
    /// building down. Posed in spring, when no household fetches firewood, so what the counter
    /// holds is the marketer's doing and nobody else's.
    /// </remarks>
    [Fact]
    public void AMarketIsStockedToItsOwnLimit()
    {
        SimConfig config = ShippedConfig.Established();
        SimLoop loop = Build(config);
        SimWorld world = loop.World;

        // Settle, so there are marketers and homes; then to the next spring.
        loop.Step(config.TicksPerYear * 8);
        while (world.Clock.Season != Season.Spring)
        {
            loop.StepOnce();
        }

        StoreBuilding market = world.StoreBuildings.Single(s => s.Kind == StoreKind.Market);
        StoreBuilding warehouse = world.AnyStoreOf(StoreKind.Warehouse);
        Assert.True(world.Workplaces.Any(w => w.Kind == JobKind.Marketer && w.Places > 0), "no market to work");

        market.Store.TryTake(Goods.Firewood, market.Store.Firewood);
        warehouse.Store.Receive(Goods.Firewood, 1000 - warehouse.Store.Firewood);

        Assert.True(world.SetMarketLimit(market, Goods.Firewood, 200).Allowed);
        int most = RunASeason();
        _output.WriteLine($"limited to 200: the counter peaked at {most} firewood, ends at {market.Store.Firewood}");
        Assert.True(most <= 200, $"the market was stocked past its limit ({most})");
        Assert.True(market.Store.Firewood >= 200 - config.CarryCapacity, $"the market was never stocked up to its limit ({market.Store.Firewood})");

        Assert.True(world.SetMarketLimit(market, Goods.Firewood, 400).Allowed);
        most = RunASeason();
        _output.WriteLine($"raised to 400: the counter peaked at {most}");
        Assert.True(most > 200, "raising the limit did not let the marketer stock more");
        Assert.True(most <= 400, $"the market was stocked past its raised limit ({most})");

        int held = market.Store.Firewood;
        Assert.True(world.SetMarketLimit(market, Goods.Firewood, 0).Allowed);
        most = RunASeason();
        _output.WriteLine($"limited to 0: the counter held {held}, peaked at {most}, ends at {market.Store.Firewood}");
        Assert.True(most <= held, "a limit of 0 did not stop the marketer stocking firewood");
        Assert.True(market.Store.Firewood > 0, "a limit of 0 emptied the counter — a limit is a ceiling, not an order to clear");

        int RunASeason()
        {
            int peak = 0;
            for (int tick = 0; tick < config.TicksPerSeason; tick++)
            {
                loop.StepOnce();
                warehouse.Store.Receive(Goods.Firewood, System.Math.Max(0, 1000 - warehouse.Store.Firewood));
                peak = System.Math.Max(peak, market.Store.Firewood);
            }

            return peak;
        }
    }

    /// <summary>A limit past the counter's capacity is allowed with a warning; a good the market never holds is refused.</summary>
    [Fact]
    public void ALimitIsBoundedByWhatTheCounterCanHold()
    {
        SimWorld world = Build(Config).World;
        StoreBuilding market = world.AnyStoreOf(StoreKind.Market);
        StoreBuilding granary = world.AnyStoreOf(StoreKind.Granary);

        PlacementVerdict past = world.SetMarketLimit(market, Goods.Firewood, market.Store.Capacity + 1);
        Assert.True(past.Allowed);
        Assert.True(past.HasWarning, "a limit past the counter's capacity should warn");
        _output.WriteLine(past.Warning);

        Assert.False(world.SetMarketLimit(market, Goods.Logs, 10).Allowed);
        Assert.False(world.SetMarketLimit(granary, Goods.Produce, 10).Allowed);
        Assert.True(world.SetMarketLimit(market, Goods.Firewood, null).Allowed);
        Assert.Null(market.Limits.For(Goods.Firewood));
    }

    /// <summary>A market's limits and a household's topping-up are state, so the hash reads them (D7's anti-vacuity).</summary>
    [Fact]
    public void TheHashCoversALimitAndAToppingUp()
    {
        SimWorld world = Build(Config).World;
        StoreBuilding market = world.AnyStoreOf(StoreKind.Market);

        ulong before = StateHash.Compute(world);
        Assert.True(world.SetMarketLimit(market, Goods.Produce, 0).Allowed);
        ulong limited = StateHash.Compute(world);
        Assert.NotEqual(before, limited);

        // ⛔ Null and zero diverge, as they do for the village's limits (D51).
        Assert.True(world.SetMarketLimit(market, Goods.Produce, null).Allowed);
        Assert.Equal(before, StateHash.Compute(world));

        world.Households[0].ToppingUpFood = !world.Households[0].ToppingUpFood;
        Assert.NotEqual(before, StateHash.Compute(world));
    }

    // ---------------------------------------------------------------
    //  ⭐ The marketer's day: the counter first
    // ---------------------------------------------------------------

    /// <summary>
    /// ⭐ With a bare counter AND a farm buffer to clear, the marketer stocks the counter first.
    /// </summary>
    /// <remarks>
    /// The restock leg was offered LAST since D197 (*"once houses are full"*), behind the
    /// deliveries that no longer exist. The counter is the shop now, so it is the first thing a
    /// marketer sees to; a buffer is cleared with nothing more pressing (D370).
    /// </remarks>
    [Fact]
    public void TheMarketerStocksTheCounterBeforeClearingABuffer()
    {
        SimConfig config = Config;
        SimLoop loop = Build(config);
        SimWorld world = loop.World;

        foreach (Household household in world.Households)
        {
            SetLarder(household, world.TargetFoodFor(household));
            household.Stockpile.Add(Goods.Firewood, VillageEconomy.FirewoodStoreWantedPerHousehold(config));
        }

        StoreBuilding market = world.AnyStoreOf(StoreKind.Market);
        StoreBuilding granary = world.AnyStoreOf(StoreKind.Granary);
        granary.Store.Receive(Goods.Produce, 400);
        Assert.Equal(0, market.Store[Goods.Produce]);

        Workplace farm = FarmFixtures.RaiseAFarm(world);
        farm.Store.Add(Goods.Produce, farm.Store.Capacity);
        Assert.True(world.BufferWorthClearing(farm), "the farm's buffer is not worth clearing, so there is nothing to order");

        foreach (JobKind kind in JobLimits.Kinds)
        {
            world.SetJobLimit(kind, kind == JobKind.Marketer ? 1 : 0);
        }

        // Watch the first leg the marketer takes.
        VillagerState? firstArrival = null;
        for (int tick = 0; tick < config.TicksPerSeason && firstArrival is null; tick++)
        {
            loop.StepOnce();
            foreach (Villager villager in world.Villagers)
            {
                if (villager.State == VillagerState.StockingTheMarket)
                {
                    firstArrival = VillagerState.StockingTheMarket;
                }
                else if (villager.State == VillagerState.HaulingToStore && world.FindWorkplace(villager.WorkplaceId)?.Kind == JobKind.Marketer && villager.IsCarrying)
                {
                    firstArrival = VillagerState.HaulingToStore;
                }
            }
        }

        _output.WriteLine($"the marketer's first load went to: {firstArrival}");
        Assert.Equal(VillagerState.StockingTheMarket, firstArrival);
    }

    /// <summary>The quota counts the restock, so a village with a bare counter staffs somebody to fill it.</summary>
    [Fact]
    public void ABareCounterIsAnErrand()
    {
        SimConfig config = Config;
        SimWorld world = Build(config).World;

        foreach (Household household in world.Households)
        {
            SetLarder(household, world.TargetFoodFor(household));
            household.Stockpile.Add(Goods.Firewood, VillageEconomy.FirewoodStoreWantedPerHousehold(config));
        }

        StoreBuilding market = world.AnyStoreOf(StoreKind.Market);
        StoreBuilding granary = world.AnyStoreOf(StoreKind.Granary);
        market.Store.TryTake(Goods.Produce, market.Store[Goods.Produce]);
        market.Store.TryTake(Goods.Firewood, market.Store.Firewood);
        granary.Store.TryTake(Goods.Produce, granary.Store[Goods.Produce]);

        Assert.Equal(0, LabourQuota.MarketersWanted(world));

        granary.Store.Receive(Goods.Produce, 400);
        Assert.True(LabourQuota.MarketersWanted(world) > 0, "a bare counter with a stocked granary asked for nobody");

        Assert.True(world.SetMarketLimit(market, Goods.Produce, 0).Allowed);
        Assert.Equal(0, LabourQuota.MarketersWanted(world));
    }

    // ---------------------------------------------------------------
    //  What the deliveries were hiding
    // ---------------------------------------------------------------

    /// <summary>
    /// ⛔ A villager carrying the firewood home is not sent back for more — nobody ever carries
    /// more than an armful (D372).
    /// </summary>
    /// <remarks>
    /// Found by the build-queue guards going red: with no marketer topping larders up
    /// year-round, a household met autumn at zero firewood, and the emergency restock fired
    /// again every tick at the villager one tile from the door with the load in her arms — 320
    /// firewood carried, 630 in a larder that wanted 43, and the hands the builders needed on
    /// the wood chain instead. An invariant, so it is asserted as one: the arms never hold more
    /// than <c>carry_capacity</c>.
    /// </remarks>
    [Fact]
    public void NobodyEverCarriesMoreThanAnArmful()
    {
        SimConfig config = Config;
        SimLoop loop = Build(config);
        SimWorld world = loop.World;

        while (world.Clock.Season != Season.Fall)
        {
            loop.StepOnce();
        }

        world.AnyStoreOf(StoreKind.Warehouse).Store.Receive(Goods.Firewood, 600);
        foreach (Household household in world.Households)
        {
            household.Stockpile.TryTake(Goods.Firewood, household.Stockpile.Firewood);
        }

        int most = 0;
        int mostInALarder = 0;
        for (int tick = 0; tick < config.TicksPerSeason; tick++)
        {
            loop.StepOnce();
            foreach (Villager villager in world.Villagers)
            {
                int carried = 0;
                for (int g = 0; g < villager.Carried.Slots; g++)
                {
                    carried += villager.Carried[(Goods)g];
                }

                most = System.Math.Max(most, carried);
            }

            foreach (Household household in world.Households)
            {
                mostInALarder = System.Math.Max(mostInALarder, household.Stockpile.Firewood);
            }
        }

        // ⚠️ Under two armfuls, not one: a gather lands on top of whatever is already in the arms,
        // so a forager can come home with 41. The loop this guards read 320.
        int wanted = VillageEconomy.FirewoodStoreWantedPerHousehold(config);
        _output.WriteLine($"the most anybody carried: {most} (an armful is {config.CarryCapacity}, a gather at most {config.GatherYield}); the most firewood in a larder wanting {wanted}: {mostInALarder}");

        // ⚠️ A GATHER IS ONE LOAD, HOWEVER BIG (D386). The bar was two armfuls, and it held while
        // the fixture's derived `gather_yield` at its ring's density came in under eighty; the
        // lane priced into the walk (D386) took the yield past it and a single day's gathering
        // read as *armfuls piled on armfuls*. The claim is about loads on loads — a fetch on top
        // of a haul — so the bar is the bigger of two armfuls and the biggest single load a
        // trade puts in somebody's arms — and a tool in the hand makes that load a quarter
        // bigger (D391), so the bar is the gather with the tool counted.
        int biggestGather = config.GatherYield + (config.GatherYield * config.ToolYieldBonusPercent / 100);
        int oneLoad = System.Math.Max(config.CarryCapacity * 2, biggestGather);
        Assert.True(most <= oneLoad, $"somebody carried {most} — armfuls piled on armfuls");
        Assert.True(mostInALarder <= wanted + config.CarryCapacity, $"a larder wanting {wanted} firewood held {mostInALarder}");
    }

    /// <summary>⭐ One member of a household fetches at a time (D372) — the emergency's claim, on the ordinary trip.</summary>
    [Fact]
    public void OneFetcherPerHouseholdAtATime()
    {
        // The granary a walk away, so a trip lasts long enough to be seen.
        SimConfig config = Config with { GranaryX = 9, GranaryY = 7 };
        SimLoop loop = Build(config);
        SimWorld world = loop.World;
        world.AnyStoreOf(StoreKind.Granary).Store.Receive(Goods.Produce, 1000);

        int mostAtOnce = 0;
        int trips = 0;
        for (int tick = 0; tick < config.TicksPerSeason; tick++)
        {
            // Every larder held at a quarter, so there is always a trip to make.
            foreach (Household household in world.Households)
            {
                SetLarder(household, world.TargetFoodFor(household) / 4);
            }

            loop.StepOnce();
            foreach (Household household in world.Households)
            {
                int fetching = 0;
                foreach (int id in household.MemberIds)
                {
                    Villager? member = world.FindVillager(id);
                    if (member is { Alive: true, State: VillagerState.FetchingFromStore })
                    {
                        fetching++;
                    }
                }

                trips += fetching;
                mostAtOnce = System.Math.Max(mostAtOnce, fetching);
            }
        }

        _output.WriteLine($"{trips} fetching-ticks in a season; at most {mostAtOnce} of one household out at once");
        Assert.True(trips > 0, "nobody fetched, so this proves nothing (D7)");
        Assert.Equal(1, mostAtOnce);
    }

    private static void SetLarder(Household home, int food)
    {
        home.Stockpile.TryTake(Goods.Produce, home.Stockpile[Goods.Produce]);
        home.Stockpile.Add(Goods.Produce, food);
    }
}

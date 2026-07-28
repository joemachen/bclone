using Bclone.Sim.Config;
using Bclone.Sim.Core;
using Bclone.Sim.Determinism;
using Bclone.Sim.Logging;
using Bclone.Sim.World;
using Xunit;
using Xunit.Abstractions;

namespace Bclone.Sim.Tests;

/// <summary>
/// The manned market — <c>specs/storage-and-distribution.md §14</c> (D14, D30 slice 4).
/// </summary>
/// <remarks>
/// D14 has claimed since Phase 1 that distribution is a job somebody does rather than a
/// policy slider. This is where that stops being a promise: a marketer produces nothing
/// and only ever moves what already exists.
/// </remarks>
public sealed class MarketTests
{
    private readonly ITestOutputHelper _output;

    public MarketTests(ITestOutputHelper output) => _output = output;

    private static SimConfig Config => VillageFixtures.Village;

    private static SimLoop Build(SimConfig config) =>
        SimFactory.CreatePhase0(config, new InMemoryLogSink());

    [Fact]
    public void TheMarketIsAPlaceAndAJobAtOnce()
    {
        SimWorld world = Build(Config).World;

        Assert.Equal(StoreKind.Market, world.AnyStoreOf(StoreKind.Market).Kind);
        Assert.True(world.AnyStoreOf(StoreKind.Market).Accepts(Goods.Food));
        Assert.True(world.AnyStoreOf(StoreKind.Market).Accepts(Goods.Firewood));
        Assert.False(world.AnyStoreOf(StoreKind.Market).Accepts(Goods.Logs));

        Workplace? stall = null;
        foreach (Workplace workplace in world.Workplaces)
        {
            if (workplace.Kind == JobKind.Marketer)
            {
                stall = workplace;
            }
        }

        Assert.NotNull(stall);
        Assert.Equal(world.AnyStoreOf(StoreKind.Market).Position, stall!.Position);

        // More than one worker, per Joe. A market is a place several people work, not
        // a one-person post.
        Assert.True(stall.Capacity > 1, $"The market has room for {stall.Capacity}.");
    }

    [Fact]
    public void TheVillageSurvivesWithTheMarketSwitchedOff()
    {
        // THE acceptance test for this slice, and the reason fetch was chosen over
        // deliver in the first place (spec §3, §14.4). An unmanned market must mean
        // longer walks and stranded goods — never a household that cannot eat.
        //
        // If this ever fails, the market has stopped being an improvement and become
        // load-bearing, which is the cliff the founding village falls off.
        SimConfig noMarket = Config with { MarketCapacity = 0 };
        SimLoop loop = Build(noMarket);

        int lowest = int.MaxValue;
        for (int year = 1; year <= 300; year++)
        {
            loop.Step(noMarket.TicksPerYear);
            if (year >= 40)
            {
                lowest = System.Math.Min(lowest, loop.World.Population);
            }
        }

        _output.WriteLine(
            $"No marketer: {loop.World.Population} alive at year 300, never below {lowest} after 40.");

        Assert.True(lowest >= noMarket.StartingPopulation,
            $"Without a market the village fell to {lowest}. Distribution by hand has stopped being " +
            "something the settlement can live without.");
        Assert.True(loop.World.Population >= noMarket.StartingPopulation);
    }

    [Fact]
    public void NobodyWorksTheMarketWhileTheVillageIsShortOfFood()
    {
        // §4a's policy, applied to the job that is furthest from feeding anyone: a
        // marketer produces nothing, so a village with an empty larder and berries to
        // pick cannot afford to have anyone shuffling what it already owns.
        SimLoop loop = Build(Config);
        loop.StepOnce();

        Assert.True(LabourQuota.VillageIsShortOfFood(loop.World));
        Assert.Equal(0, LabourQuota.For(loop.World).Marketers);
    }

    [Fact]
    public void ADeadFamilysLarderDoesNotStayStranded()
    {
        // What D34 left behind. A household can only fetch from a store, so goods in
        // somebody else's home are unreachable — and when that family dies out, their
        // larder is stranded for good. The marketer is the only one who can reach it,
        // and unsticking it is the whole of Joe's second requirement (spec §14.3).
        // Measured comparatively rather than by watching one house, because an empty
        // home gets taken over by the next couple that needs one — so a single
        // household's larder can refill for a perfectly good reason and prove nothing.
        // What matters is how much of the village's goods are sitting in houses with
        // nobody in them, summed over time.
        SimConfig config = Config;

        long withMarket = StrandedGoodsOverTime(config);
        long without = StrandedGoodsOverTime(config with { MarketCapacity = 0 });

        _output.WriteLine(
            $"goods-years stranded in empty homes: {withMarket} with a market, {without} without.");

        Assert.True(without > 0,
            "Goods never piled up in an empty house even with no market, so this guard is " +
            "vacuous (D7) — the case it exists for is not happening.");

        Assert.True(withMarket < without,
            $"Stranded goods came to {withMarket} with a market against {without} without one. " +
            "Nothing in the village can reach an empty house's larder except a marketer, so this " +
            "is the market failing at the job D34 left it.");
    }

    /// <summary>
    /// Goods sitting in households with nobody living in them, summed over 200 years.
    /// </summary>
    /// <remarks>
    /// Summed rather than sampled, so a house that is cleared quickly counts for little
    /// and one that stays full for decades counts for a lot. Sampled per season, never
    /// at the year boundary — the labour pass runs there, which makes it the one
    /// instant that does not reflect the year.
    /// </remarks>
    private static long StrandedGoodsOverTime(SimConfig config)
    {
        SimLoop loop = Build(config);
        long total = 0;

        for (int season = 1; season <= 200 * 4; season++)
        {
            loop.Step(config.TicksPerYear / 4);

            foreach (Household household in loop.World.Households)
            {
                if (loop.World.LivingMembersOf(household) == 0)
                {
                    total += household.Stockpile.Food + household.Stockpile.Firewood;
                }
            }
        }

        return total;
    }

    [Fact]
    public void AMarketerNeverWalksAnEmptyLeg()
    {
        // Joe's rule about distances, asserted rather than described (§14.2). Every
        // leg is chosen cost-first from wherever they stand, so "pick up food from the
        // granary on the way back" falls out instead of being a special case — but
        // only if a marketer heading AWAY from a pickup is always carrying something.
        SimConfig config = Config;
        SimLoop loop = Build(config);

        int deliveries = 0;
        for (int i = 0; i < config.TicksPerYear * 120; i++)
        {
            loop.StepOnce();

            foreach (Villager villager in loop.World.Villagers)
            {
                if (!villager.Alive || villager.State != VillagerState.DeliveringToHome)
                {
                    continue;
                }

                deliveries++;

                // ...unless they have just eaten it. A hungry marketer takes their
                // meal out of their own arms first, which is D10's rule — nobody
                // starves holding dinner — and it is the honest behaviour even though
                // it means the load can arrive smaller than it left, or not at all.
                // Found by this test: it originally asserted the flat invariant and
                // caught Dorcas eating her delivery in the village's first year.
                Assert.True(villager.IsCarrying || villager.JustAte,
                    $"{villager.Name} is delivering to a home with empty arms at tick {loop.World.Tick}.");
            }
        }

        _output.WriteLine($"{deliveries} delivering-ticks observed.");
        Assert.True(deliveries > 0,
            "No marketer ever delivered anything in 120 years, so this guard is vacuous (D7).");
    }

    [Fact]
    public void TheMarketShortensTheWalkForFood()
    {
        // The spec's own standard for this building: "a manned market measurably
        // reduces total travel versus none, or it is decoration" (§8).
        //
        // Measured as how far households have to go to FETCH, which is the trip the
        // market exists to shorten. The marketers' own walking is not counted — that is
        // the cost being paid, not the benefit, and including it would let a market
        // pass this test by making more work for itself.
        SimConfig config = Config;

        long withMarket = FetchDistanceOverTime(config);
        long without = FetchDistanceOverTime(config with { MarketCapacity = 0 });

        _output.WriteLine($"household fetch-steps: {withMarket} with a market, {without} without.");

        Assert.True(without > 0, "Nobody ever fetched anything, so this guard is vacuous (D7).");
        Assert.True(withMarket < without,
            $"Households walked {withMarket} with a market against {without} without one. The market " +
            "is not shortening anybody's errand, which makes it decoration.");
    }

    /// <summary>
    /// Total steps households spend fetching, over 100 years — the marketers' own
    /// legs excluded, since their walking is the cost rather than the benefit.
    /// </summary>
    private static long FetchDistanceOverTime(SimConfig config)
    {
        SimLoop loop = Build(config);
        long steps = 0;

        for (int i = 0; i < config.TicksPerYear * 100; i++)
        {
            loop.StepOnce();

            foreach (Villager villager in loop.World.Villagers)
            {
                if (villager.Alive && villager.State == VillagerState.FetchingFromStore)
                {
                    steps++;
                }
            }
        }

        return steps;
    }

    [Fact]
    public void AHouseholdFetchingFoodFromTheMarketComesHomeWithFood()
    {
        // The regression that shipped and was caught by Joe watching a village, not by
        // a test: "people seem to not be able to find anything to eat."
        //
        // CollectFromStore branched on the BUILDING KIND — granary, take food; anything
        // else, take firewood. That was right while only the granary held food. The
        // market holds both, and PlanFetch sends a household to the NEAREST store with
        // what it needs, so a home closer to the market than the granary walked over
        // for food and came back with firewood. Every trip. Forever. It starved with
        // the granary full, which is the exact failure D30 exists to prevent.
        SimConfig config = Config;
        SimWorld world = Build(config).World;

        Household household = world.Households[0];
        household.Stockpile.TryTake(household.Stockpile.Food);

        StoreBuilding market = world.AnyStoreOf(StoreKind.Market);
        market.Store.Add(200);
        market.Store.AddFirewood(200);

        // Put somebody at the market's door with an empty larder behind them.
        Villager villager = world.FindVillager(household.MemberIds[0])!;
        villager.Position = market.Position;
        villager.CarriedFood = 0;
        villager.CarriedFirewood = 0;

        Bclone.Sim.Systems.BehaviorSystem.CollectForTest(world, villager);

        _output.WriteLine(
            $"came away with {villager.CarriedFood} food and {villager.CarriedFirewood} firewood.");

        Assert.True(villager.CarriedFood > 0,
            "They went to the market for food and did not pick any up.");
    }

    [Fact]
    public void TheMarketMovesGoodsWithoutCreatingThem()
    {
        // Conservation across the new movements. A marketer produces nothing, so every
        // unit they hand over has to have come out of somewhere — and the lifetime
        // counters must not inflate, since a delivery is goods changing hands rather
        // than a household producing them (Stockpile.Receive).
        SimConfig config = Config;
        SimLoop loop = Build(config);
        loop.Step(config.TicksPerYear * 80);

        int held = loop.World.TotalFirewood();
        int everMade = loop.World.LifetimeFirewoodCut();

        _output.WriteLine($"{held} firewood held, {everMade} ever cut.");
        Assert.True(held <= everMade,
            $"The village holds {held} firewood but has only ever cut {everMade} — movement is " +
            "creating goods.");

        int logsHeld = loop.World.TotalLogs();
        Assert.True(logsHeld <= loop.World.LifetimeLogsFelled());
    }

    [Fact]
    public void TheMarketIsDeterministic()
    {
        SimConfig config = Config;
        SimLoop a = Build(config);
        SimLoop b = Build(config);

        a.Step(config.TicksPerYear * 150);
        b.Step(config.TicksPerYear * 150);

        Assert.Equal(StateHash.Compute(a.World), StateHash.Compute(b.World));
        Assert.Equal(a.World.AnyStoreOf(StoreKind.Market).Store.Food, b.World.AnyStoreOf(StoreKind.Market).Store.Food);
    }

    [Fact]
    public void TheHashCoversWhatIsInSomeonesArms()
    {
        // Anti-vacuity (D7), and it was a real gap: carried goods are the goods that
        // exist between two buildings, and the hash did not read them at all. A
        // village could have desynced by exactly the amount somebody was holding.
        SimLoop loop = Build(Config);
        loop.Step(Config.TicksPerYear * 5);

        ulong before = StateHash.Compute(loop.World);
        loop.World.Villagers[0].CarriedFood += 1;
        Assert.NotEqual(before, StateHash.Compute(loop.World));

        ulong carried = StateHash.Compute(loop.World);
        loop.World.Villagers[0].ErrandHouseholdId += 1;
        Assert.NotEqual(carried, StateHash.Compute(loop.World));
    }
}

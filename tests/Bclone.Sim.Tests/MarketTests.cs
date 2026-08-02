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
    public void TheMarketKeepsLardersFromRunningDry()
    {
        // THE SPEC SAID "measurably reduces total travel versus none, or it is decoration"
        // (§8) — and that is a consequence rather than the purpose. See D78 below.
        //
        // Measured as how far households have to go to FETCH, which is the trip the
        // market exists to shorten. The marketers' own walking is not counted — that is
        // the cost being paid, not the benefit, and including it would let a market
        // pass this test by making more work for itself.
        //
        // PER HEAD, NOT IN TOTAL, and that is not a refinement — the raw aggregate was
        // measuring the wrong village. Two runs of the same fixture do not hold the same
        // number of people; whichever one grows more does more walking, for the best of
        // reasons. So this compared a market against a control of a different size and
        // called the difference the market's doing. It passed for years on that, and
        // then failed loudly the first time a change to the LABOUR QUOTA — nothing to do
        // with the market at all — shrank the no-market control from a mean of 22 people
        // to 14. The market arm was the healthy one. A whole session went looking for a
        // market bug that was never there.
        //
        // Same lesson as D34's, one level up: an assertion about a window is not an
        // assertion about a system, and a raw aggregate is not a rate.
        SimConfig config = Config;

        Fetching withMarket = FetchDistanceOverTime(config);
        Fetching without = FetchDistanceOverTime(config with { MarketCapacity = 0 });

        _output.WriteLine(
            $"household fetch-steps per 10,000 person-ticks: {withMarket.PerPersonTick} with a " +
            $"market (mean population {withMarket.MeanPopulation}), {without.PerPersonTick} " +
            $"without (mean population {without.MeanPopulation}).");

        Assert.True(without.Steps > 0, "Nobody ever fetched anything, so this guard is vacuous (D7).");

        // AND THE CONTROL HAS TO BE A LIVING VILLAGE, checked before the comparison it
        // is the control for. Free to assert — both runs already happened — and it is
        // precisely what was missing: when the no-market arm collapsed to a mean of 14
        // against the market arm's 21, this test reported a market that had stopped
        // shortening walks. The market was fine. Its control had died down to a size
        // where it barely walked anywhere.
        //
        // A quarter is the one chosen number in this file rather than a derived one, and
        // the argument for it is spec §14.4: the market buys CONVENIENCE, so switching
        // it off may cost the village errands and stranded goods. Losing a quarter of
        // the people is not an inconvenience, it is the cliff §14.4 promises the market
        // is not — and whatever caused it will not be in this building.
        Assert.True(without.MeanPopulation * 4 >= withMarket.MeanPopulation * 3,
            $"The village without a market averaged {without.MeanPopulation} people against " +
            $"{withMarket.MeanPopulation} with one. The market has become load-bearing (spec §14.4), " +
            "and until that is fixed this test has no honest control to measure against.");

        // THE BAR IS DISTRIBUTION, NOT DISTANCE (D78, Joe's correction).
        //
        // This used to assert that households walked fewer steps with a market. That is a
        // CONSEQUENCE of the market rather than its purpose — Joe: "the market exists to
        // provide more equal goods distribution to homes, to lessen demand-curve bank
        // runs." Measuring the consequence made the guard fail a change that improved the
        // purpose: an emergency restock (D77) cut households running dry and raised the
        // step count, and this test called that a broken market.
        //
        // So the question is now the one the building answers: how much of the time does a
        // family sit on an empty larder while the village's stores are full? That is a bank
        // run — goods that exist and have not reached you — and it is exactly what a
        // marketer is for.
        _output.WriteLine(
            $"household-time on an empty larder while stores held food, per 10,000: " +
            $"{withMarket.DryPerTenThousand} with a market, {without.DryPerTenThousand} without.");

        Assert.True(without.DryPerTenThousand > 0,
            "No household ever ran dry in either arm, so this guard is vacuous (D7).");

        Assert.True(withMarket.DryPerTenThousand < without.DryPerTenThousand,
            $"Households sat on an empty larder {withMarket.DryPerTenThousand} per 10,000 " +
            $"household-ticks with a market against {without.DryPerTenThousand} without one, " +
            "with food in the stores the whole time. The market is not distributing anything, " +
            "which makes it decoration.");
    }

    /// <summary>Fetching done by households over a run, and the village that did it.</summary>
    /// <param name="Steps">Total steps spent walking to a store.</param>
    /// <param name="PersonTicks">Living villagers summed over every tick — the denominator.</param>
    /// <param name="Ticks">How long the run was.</param>
    /// <param name="DryHouseholdTicks">Household-ticks on an empty larder while stores held food.</param>
    /// <param name="HouseholdTicks">Living households summed over every tick — that denominator.</param>
    private readonly record struct Fetching(
        long Steps, long PersonTicks, int Ticks, long DryHouseholdTicks, long HouseholdTicks)
    {
        /// <summary>
        /// Share of household-time spent with an empty larder while the stores held food.
        /// </summary>
        /// <remarks>
        /// <b>The market's actual promise</b> (D78, Joe): it exists to distribute goods more
        /// evenly to homes and to take the edge off demand spikes — not to shorten walks.
        /// Walk length is a <em>consequence</em> the old bar measured, and measuring a
        /// consequence is how a guard ends up failing a change that improved the thing it
        /// was written to protect.
        /// </remarks>
        public long DryPerTenThousand =>
            HouseholdTicks == 0 ? 0 : DryHouseholdTicks * 10_000 / HouseholdTicks;

        /// <summary>Fetch-steps per 10,000 person-ticks. Integer, because floats do not
        /// belong anywhere near a comparison this test turns on (D2).</summary>
        /// <remarks>
        /// <b>Reported, no longer asserted</b> (D78). It is a useful thing to see and the
        /// wrong thing to fail on: a change that stops households running dry by sending
        /// somebody to fetch will raise this number while improving the village.
        /// </remarks>
        public long PerPersonTick => PersonTicks == 0 ? 0 : Steps * 10_000 / PersonTicks;

        /// <summary>Mean living population across the run, for the message.</summary>
        public long MeanPopulation => Ticks == 0 ? 0 : PersonTicks / Ticks;
    }

    /// <summary>
    /// Steps households spend fetching over 100 years, and the person-ticks to divide
    /// them by — the marketers' own legs excluded, since their walking is the cost
    /// rather than the benefit.
    /// </summary>
    private static Fetching FetchDistanceOverTime(SimConfig config)
    {
        SimLoop loop = Build(config);
        long steps = 0;
        long personTicks = 0;
        long dry = 0;
        long householdTicks = 0;
        int ticks = config.TicksPerYear * 100;

        for (int i = 0; i < ticks; i++)
        {
            loop.StepOnce();

            foreach (Villager villager in loop.World.Villagers)
            {
                if (!villager.Alive)
                {
                    continue;
                }

                personTicks++;
                if (villager.State == VillagerState.FetchingFromStore)
                {
                    steps++;
                }
            }

            // THE BANK RUN, COUNTED (D78). A household sitting on nothing while the
            // village's stores hold plenty is the failure the market exists to prevent —
            // goods that exist and have not reached you. Counted per household-tick so it
            // is a rate and not a headcount, for the same reason the steps above are.
            // The village's own stores, not its larders: the question is whether food
            // exists somewhere a marketer could have brought it from.
            bool storesHaveFood = false;
            foreach (StoreBuilding store in loop.World.StoreBuildings)
            {
                if (store.Store.Food > 0)
                {
                    storesHaveFood = true;
                    break;
                }
            }

            foreach (Household household in loop.World.Households)
            {
                if (loop.World.LivingMembersOf(household) == 0)
                {
                    continue;
                }

                householdTicks++;
                if (storesHaveFood && household.Stockpile.Food == 0)
                {
                    dry++;
                }
            }
        }

        return new Fetching(steps, personTicks, ticks, dry, householdTicks);
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

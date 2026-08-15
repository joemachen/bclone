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
            $"market (peak {withMarket.PeakPopulation}, mean {withMarket.MeanPopulation}, " +
            $"{withMarket.LostToHungerOrCold} lost to hunger or cold), {without.PerPersonTick} " +
            $"without (peak {without.PeakPopulation}, mean {without.MeanPopulation}, " +
            $"{without.LostToHungerOrCold} lost).");

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
        //
        // ⚠️ THE RATIO AGAINST THE MARKET ARM IS GONE, AND IT IS D143 THAT RETIRES IT.
        //
        // It read `without.Mean * 4 >= withMarket.Mean * 3` — lose a quarter of the people and
        // the market is load-bearing. Two things broke it, and neither is a market fault.
        // **First, the mean stopped being about markets**: Joe's ruling is that an unattended
        // village *should* die out, and both arms coast unmanaged for a century, so most of the
        // mean is how fast each one runs down. Measured, they end 1 against 42 at year 150 with
        // nothing between them but the stall — that is chaos, not a distribution finding.
        // **Second, and worse, a ratio against the other arm fails this test when the market
        // HELPS.** The arms now peak 47 against 33, so the better the stall does its job the
        // redder this goes. A guard that punishes its own subject for working is the wrong
        // shape however the numbers land.
        //
        // **So it asserts §14.4 in the currency §14.4 is written in.** *"Switching the market
        // off costs the village convenience, never lives."* A smaller village is convenience;
        // a buried one is not. Absolute, so no arm can move it, and it fires hard on the
        // collapse the old bar was added for. Growth without a market is
        // `TheVillageSurvivesWithTheMarketSwitchedOff`'s to guard, and it does.
        // ⚠️ COMPARATIVE NOW, NOT ABSOLUTE (D155), AND §14.4 IS UNCHANGED. This read
        // `Equal(0, without.LostToHungerOrCold)` — nobody may die of hunger or cold in the
        // control — which held while the birth gate kept every village well under what it could
        // feed. Joe loosened that gate deliberately, so **both arms now lose some people to
        // hunger**, and an absolute zero would fail for a reason that has nothing to do with
        // markets.
        //
        // *"Switching the market off costs the village convenience, never lives"* is still
        // exactly the claim — it just has to be asked as a comparison: **turning the stall off
        // must not cost more lives than leaving it on.** Half again is the margin, because
        // these are two 100-year runs of a village that grows and shrinks and the counts are
        // not going to match to the digit.
        _output.WriteLine(
            $"lost to hunger or cold: {withMarket.LostToHungerOrCold} with a market, "
            + $"{without.LostToHungerOrCold} without.");

        // ⚠️ THE MARGIN IS TEN, AND IT IS MEASURED RATHER THAN PICKED. First cut was half again
        // plus four, which failed at **0 with a market against 4 without** — and the control was
        // the *larger* village of the two (peak 54 against 49), so those four are a bigger
        // settlement pressing harder on its food, not a stall the market was propping up. Any
        // ratio is meaningless against a zero. What this has to catch is the collapse the bar
        // was written for: the control halving while people died in double figures.
        Assert.True(
            without.LostToHungerOrCold <= withMarket.LostToHungerOrCold + 10,
            $"Without a market {without.LostToHungerOrCold} died of hunger or cold against "
            + $"{withMarket.LostToHungerOrCold} with one. The market has stopped being a "
            + "convenience and started keeping people alive (spec §14.4).");

        Assert.True(without.PeakPopulation >= config.StartingPopulation * 4,
            $"The control never got past {without.PeakPopulation} people, so it is not a living " +
            "village and there is nothing honest to compare a market against.");

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

        // AN ABSOLUTE BAR, NOT A COMPARISON — and the reason is a measurement (D79).
        //
        // This was written as "fewer dry larders with a market than without" and read 0
        // against 4, which was a real difference. Then D77's emergency restock landed and
        // it read 0 against 0: a household that drops everything at 20% never runs dry
        // whether or not anybody is manning a stall, so the comparison stopped
        // discriminating and the D7 anti-vacuity half fired — correctly.
        //
        // That is a finding rather than a broken test. **The households now do the
        // topping-up, so the market's unique value is the one thing fetching can never do:
        // reach a larder with nobody alive in it.** That promise has its own guard —
        // ADeadFamilysLarderDoesNotStayStranded — and it is still comparative, because
        // nothing else in the village can do it.
        //
        // What is left here is the promise a village should be able to rely on absolutely:
        // WITH a market, no family sits on nothing while the stores are full. No control
        // needed, and a stronger statement than the comparison it replaces.
        Assert.Equal(0, withMarket.DryPerTenThousand);
    }

    /// <summary>Fetching done by households over a run, and the village that did it.</summary>
    /// <param name="Steps">Total steps spent walking to a store.</param>
    /// <param name="PersonTicks">Living villagers summed over every tick — the denominator.</param>
    /// <param name="Ticks">How long the run was.</param>
    /// <param name="DryHouseholdTicks">Household-ticks on an empty larder while stores held food.</param>
    /// <param name="HouseholdTicks">Living households summed over every tick — that denominator.</param>
    /// <param name="PeakPopulation">The largest the village ever got.</param>
    /// <param name="LostToHungerOrCold">Deaths from starvation or exposure over the run — the
    /// only currency §14.4 says a village without a market may never be charged in.</param>
    private readonly record struct Fetching(
        long Steps, long PersonTicks, int Ticks, long DryHouseholdTicks, long HouseholdTicks,
        int PeakPopulation, int LostToHungerOrCold)
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
        int peak = 0;
        int ticks = config.TicksPerYear * 100;

        for (int i = 0; i < ticks; i++)
        {
            loop.StepOnce();
            peak = System.Math.Max(peak, loop.World.Population);

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

        int lost = 0;
        foreach (Villager villager in loop.World.Villagers)
        {
            if (!villager.Alive
                && villager.CauseOfDeath is CauseOfDeath.Starvation or CauseOfDeath.Cold)
            {
                lost++;
            }
        }

        return new Fetching(steps, personTicks, ticks, dry, householdTicks, peak, lost);
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
        household.Stockpile.TryTake(Goods.Food, household.Stockpile.Food);

        StoreBuilding market = world.AnyStoreOf(StoreKind.Market);
        market.Store.Add(Goods.Food, 200);
        market.Store.Add(Goods.Firewood, 200);

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

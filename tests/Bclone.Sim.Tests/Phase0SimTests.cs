using System.Linq;
using Bclone.Sim.Config;
using Bclone.Sim.Core;
using Bclone.Sim.Determinism;
using Bclone.Sim.World;
using Xunit;

namespace Bclone.Sim.Tests;

/// <summary>Unit tests for the Phase 0 sim rules (spec §8).</summary>
public sealed class Phase0SimTests
{
    private static SimConfig Config => Phase0Fixtures.Plenty;

    // ---------------------------------------------------------------
    //  Clock
    // ---------------------------------------------------------------

    [Fact]
    public void Clock_StartsOnDayOneOfSpringYearOne()
    {
        SimClock clock = SimClock.FromTick(0UL, Config);

        Assert.Equal(1, clock.DayOfSeason);
        Assert.Equal(Season.Spring, clock.Season);
        Assert.Equal(1, clock.Year);
    }

    [Fact]
    public void Clock_RollsOverTicksIntoDays()
    {
        // 4 ticks per day, so tick 4 is day 2.
        Assert.Equal(1, SimClock.FromTick(3UL, Config).DayOfSeason);
        Assert.Equal(2, SimClock.FromTick(4UL, Config).DayOfSeason);
    }

    [Theory]
    [InlineData(0, Season.Spring)]
    [InlineData(60, Season.Summer)]   // 15 days x 4 ticks
    [InlineData(120, Season.Fall)]
    [InlineData(180, Season.Winter)]
    [InlineData(240, Season.Spring)]  // next year
    public void Clock_RollsOverDaysIntoSeasons(int tick, Season expected)
    {
        Assert.Equal(expected, SimClock.FromTick((ulong)tick, Config).Season);
    }

    [Fact]
    public void Clock_RollsOverSeasonsIntoYears()
    {
        Assert.Equal(1, SimClock.FromTick(239UL, Config).Year);
        Assert.Equal(2, SimClock.FromTick(240UL, Config).Year);
        Assert.Equal(3, SimClock.FromTick(480UL, Config).Year);
    }

    [Fact]
    public void Clock_IsAPureFunctionOfTheTick()
    {
        // Derived, not accumulated — so it cannot drift out of sync with the tick
        // no matter how the sim got there.
        for (ulong tick = 0; tick < 2_000; tick++)
        {
            Assert.Equal(SimClock.FromTick(tick, Config), SimClock.FromTick(tick, Config));
        }
    }

    [Fact]
    public void VillagerAges_OnTheYearBoundary()
    {
        // Year 1 is 240 ticks. Systems run for tick N and then the counter
        // increments, so the tick that first *computes* Year 2 is the 241st step.
        var (loop, _) = Phase0Fixtures.Build(Config);

        loop.Step(240);
        Assert.Equal(0, loop.World.Villager.AgeYears);

        loop.StepOnce();
        Assert.Equal(1, loop.World.Villager.AgeYears);
        Assert.Equal(2, loop.World.Clock.Year);
    }

    // ---------------------------------------------------------------
    //  Hunger
    // ---------------------------------------------------------------

    [Fact]
    public void Hunger_AccruesPerTickAndClampsAtMax()
    {
        // Meals priced out of reach, so nothing interferes with the climb.
        //
        // ⚠️ AND THE PERSONAL RHYTHM OFF, because it starts a villager's hunger a few points
        // above zero (§3.5, D190) — people do not all get hungry at the same instant. **This
        // guard is about the arithmetic of the climb**, which needs a known starting point, and
        // a one-villager world has nobody to be staggered against anyway.
        SimConfig config = Config with { FoodPerMeal = 999, SeededRhythm = false };
        var (loop, _) = Phase0Fixtures.Build(config);

        loop.StepOnce();
        Assert.Equal(config.HungerPerTick, loop.World.Villager.Hunger);

        loop.Step(100);
        Assert.Equal(config.HungerMax, loop.World.Villager.Hunger);
    }

    [Fact]
    public void Eating_ConsumesStockpileAndReducesHunger()
    {
        var (loop, _) = Phase0Fixtures.Build(Config);
        SimWorld world = loop.World;

        // Run until the first meal is taken. Measured against food the villager can
        // reach — larder plus what is in their arms — because a meal may now come out
        // of either, and a villager carrying food while starving eats it (D30).
        int foodBefore = 0;
        for (int i = 0; i < 500; i++)
        {
            foodBefore = world.Stockpile.Food + world.Villager.CarriedFood;
            loop.StepOnce();
            if (world.Villager.JustAte)
            {
                break;
            }
        }

        Assert.True(world.Villager.JustAte, "Expected the villager to eat within 500 ticks.");
        Assert.Equal(
            foodBefore - Config.FoodPerMeal,
            world.Stockpile.Food + world.Villager.CarriedFood);
        Assert.True(world.Villager.Hunger < Config.EatThreshold);
    }

    [Fact]
    public void Eating_IsBlockedWhenTheStoreIsEmpty()
    {
        // Nothing to gather anywhere, so the store stays empty and hunger maxes out.
        SimConfig config = Config with { GatherYield = 1, FoodPerMeal = 999 };
        var (loop, _) = Phase0Fixtures.Build(config);

        loop.Step(50);

        Assert.Equal(config.HungerMax, loop.World.Villager.Hunger);
        Assert.False(loop.World.Villager.JustAte);
    }

    [Fact]
    public void Eating_PreemptsAnActionRatherThanWaitingForIt()
    {
        // The bug this guards: a round trip to the berry patch is longer than the
        // gap between meals, so a villager who cannot interrupt starves with a full
        // larder. Hunger must never reach max while food is in store.
        var (loop, _) = Phase0Fixtures.Build(Config);

        for (int i = 0; i < 5_000; i++)
        {
            loop.StepOnce();

            if (loop.World.Stockpile.Food >= Config.FoodPerMeal)
            {
                Assert.True(
                    loop.World.Villager.Hunger < Config.HungerMax,
                    $"Hunger hit max at tick {loop.World.Tick} with " +
                    $"{loop.World.Stockpile.Food} food in store.");
            }
        }
    }

    // ---------------------------------------------------------------
    //  Gathering
    // ---------------------------------------------------------------

    [Fact]
    public void Gathering_AddsYieldAfterGatherTicks()
    {
        var (loop, _) = Phase0Fixtures.Build(Config);

        // Food is carried now rather than banked where it is picked (D30), so the
        // yield lands in the villager's arms first and in the larder when they get
        // home. Both are the same gather; this waits for it to arrive.
        for (int i = 0; i < 100; i++)
        {
            loop.StepOnce();
            if (loop.World.Villager.TotalGathers > 0)
            {
                break;
            }
        }

        Assert.Equal(1, loop.World.Villager.TotalGathers);

        // ⚠️ AGAINST WHAT THIS HUT IS WORTH, NOT AGAINST THE CONFIG KEY. `gather_yield` is
        // the value of a trip at a FULLY WOODED ring; what a villager actually carries home
        // is that scaled by how wooded their own hut's ring is (`GatherYieldAt`), which is
        // the whole of "less trees, less food". Comparing against the raw key asserted the
        // yield of a hut standing in unbroken forest, which no hut ever is.
        Workplace hut = loop.World.Workplaces.Single(
            place => place.Kind == JobKind.Forager && !place.IsSite);

        Assert.Equal(
            loop.World.GatherYieldAt(hut),
            loop.World.Villager.CarriedFood + loop.World.Stockpile.Food);
    }

    [Fact]
    public void Gathering_IsImpossibleInWinter()
    {
        var (loop, _) = Phase0Fixtures.Build(Config);

        // Run to the start of winter, then note the store and watch it only fall.
        while (!loop.World.Clock.IsWinter)
        {
            loop.StepOnce();
        }

        // Not one single gather. A trip already underway is abandoned the moment
        // the season turns — otherwise the life log announces "Foraging stops" and
        // then reports a gather on the next line.
        int gathersAtWinterStart = loop.World.Villager.TotalGathers;
        int atWinterStart = loop.World.Stockpile.Food;

        while (loop.World.Clock.IsWinter)
        {
            loop.StepOnce();

            // The rule is that nobody GATHERS in winter — not that the larder can
            // never rise. Since goods are carried rather than banked where they are
            // picked (D30), a villager who was walking home when the season turned
            // arrives with an autumn armful and puts it away, which is honest. The
            // store growing is the delivery; what must not happen is a new gather.
            Assert.Equal(gathersAtWinterStart, loop.World.Villager.TotalGathers);
        }

        Assert.Equal(gathersAtWinterStart, loop.World.Villager.TotalGathers);
        Assert.True(loop.World.Stockpile.Food < atWinterStart, "Winter should drain the store.");
    }

    [Fact]
    public void Gathering_StopsAtTheStockpileTarget()
    {
        var (loop, _) = Phase0Fixtures.Build(Config);
        loop.Step(2_000);

        // A gather can overshoot the target by one yield, but never more —
        // otherwise the villager is hoarding instead of resting.
        Assert.True(
            loop.World.Stockpile.Food <= Config.StockpileTarget + Config.GatherYield,
            $"Stockpile ran away to {loop.World.Stockpile.Food}.");
    }

    [Fact]
    public void Stockpile_NeverGoesNegative()
    {
        var (loop, _) = Phase0Fixtures.Build(Phase0Fixtures.Scarcity);

        for (int i = 0; i < 20_000; i++)
        {
            loop.StepOnce();
            Assert.True(loop.World.Stockpile.Food >= 0);
        }
    }

    // ---------------------------------------------------------------
    //  Death
    // ---------------------------------------------------------------

    [Fact]
    public void Starvation_FiresAtTheThresholdNotBefore()
    {
        // Boundary is >= (spec §11). With no food, hunger maxes at tick 10, and
        // death lands starvation_ticks later.
        SimConfig config = Config with { GatherYield = 1, FoodPerMeal = 999, StarvationTicks = 24 };
        var (loop, _) = Phase0Fixtures.Build(config);

        while (loop.World.Villager.TicksAtMaxHunger < config.StarvationTicks - 1)
        {
            loop.StepOnce();
        }

        Assert.True(loop.World.Villager.Alive, "Died one tick early.");

        loop.StepOnce();
        Assert.False(loop.World.Villager.Alive);
        Assert.Equal(CauseOfDeath.Starvation, loop.World.Villager.CauseOfDeath);
    }

    [Fact]
    public void OldAge_FiresAtTheDrawnLifespan()
    {
        var (loop, _) = Phase0Fixtures.Build(Config);
        int lifespan = loop.World.Villager.LifespanYears;

        Phase0Fixtures.RunUntilDeath(loop);

        Assert.Equal(CauseOfDeath.OldAge, loop.World.Villager.CauseOfDeath);
        Assert.Equal(lifespan, loop.World.Villager.AgeYears);
    }

    [Fact]
    public void LifespanVariance_StaysInTheConfiguredBand()
    {
        for (ulong seed = 1; seed <= 50; seed++)
        {
            var (loop, _) = Phase0Fixtures.Build(Config, seed);
            int lifespan = loop.World.Villager.LifespanYears;

            Assert.InRange(
                lifespan,
                Config.LifespanYearsBase - Config.LifespanYearsVariance,
                Config.LifespanYearsBase + Config.LifespanYearsVariance);
        }
    }

    [Fact]
    public void TheDead_StopActingEntirely()
    {
        var (loop, sink) = Phase0Fixtures.Build(Phase0Fixtures.Scarcity);
        Phase0Fixtures.RunUntilDeath(loop);

        ulong hashAtDeath = StateHash.Compute(loop.World);
        int foodAtDeath = loop.World.Stockpile.Food;
        GridPos posAtDeath = loop.World.Villager.Position;

        int ageAtDeath = loop.World.Villager.AgeYears;
        int logAtDeath = Phase0Fixtures.LifeLog(sink).Count;

        loop.Step(2_000);

        Assert.Equal(VillagerState.Dead, loop.World.Villager.State);
        Assert.Equal(foodAtDeath, loop.World.Stockpile.Food);
        Assert.Equal(posAtDeath, loop.World.Villager.Position);

        // The clock keeps turning, but the story is over: no more ageing, and no
        // more narration. Otherwise the log announces winters to an empty house and
        // the age on screen drifts past the age in the epitaph.
        Assert.Equal(ageAtDeath, loop.World.Villager.AgeYears);
        Assert.Equal(logAtDeath, Phase0Fixtures.LifeLog(sink).Count);

        // Only the tick and clock advance after death, so the hash must change —
        // but nothing about the villager may.
        Assert.NotEqual(hashAtDeath, StateHash.Compute(loop.World));
    }

    [Fact]
    public void DeathIsRecordedWithATick()
    {
        var (loop, _) = Phase0Fixtures.Build(Phase0Fixtures.Scarcity);
        Phase0Fixtures.RunUntilDeath(loop);

        Assert.NotNull(loop.World.Villager.DiedAtTick);
        Assert.True(loop.World.Villager.DiedAtTick <= loop.World.Tick);
    }

    // ---------------------------------------------------------------
    //  Movement
    // ---------------------------------------------------------------

    [Fact]
    public void ManhattanDistance_IsIntegerOnly()
    {
        Assert.Equal(5, new GridPos(0, 0).ManhattanDistanceTo(new GridPos(5, 0)));
        Assert.Equal(7, new GridPos(0, 0).ManhattanDistanceTo(new GridPos(3, 4)));
        Assert.Equal(7, new GridPos(3, 4).ManhattanDistanceTo(new GridPos(0, 0)));
    }

    [Fact]
    public void StepToward_ClosesDistanceByExactlyOne()
    {
        var pos = new GridPos(0, 0);
        var target = new GridPos(3, 2);

        int distance = pos.ManhattanDistanceTo(target);
        while (pos != target)
        {
            pos = pos.StepToward(target);
            Assert.Equal(--distance, pos.ManhattanDistanceTo(target));
        }

        Assert.Equal(target, pos.StepToward(target));
    }

    [Fact]
    public void Villager_StaysWithinTheirWorld()
    {
        // Inside the valley, which is what "their world" means and what the villager
        // must never leave. It used to assert they stayed between home and the berry
        // patch, which was true when those were the only two places anyone went —
        // there is a granary and a shed to walk to now, and both are on the other side
        // of home.
        var (loop, _) = Phase0Fixtures.Build(Config);
        SimConfig config = Config;

        for (int i = 0; i < 3_000; i++)
        {
            loop.StepOnce();
            GridPos where = loop.World.Villager.Position;

            Assert.InRange(where.X, config.MapMinX, config.MapMaxX);
            Assert.InRange(where.Y, config.MapMinY, config.MapMaxY);

            // And never on the water (D40), which is the stronger claim.
            Assert.NotEqual(Terrain.Water, loop.World.Map.TerrainAt(where));
        }
    }

    // ---------------------------------------------------------------
    //  Golden replay (spec §8)
    // ---------------------------------------------------------------

    [Fact]
    public void GoldenReplay_ASeededLifeIsReproducible()
    {
        // Locks the shipped tuning against silent behavioural drift. If this fails
        // after an intentional balance change, re-read the life log, confirm the new
        // story is the one you wanted, then update these numbers deliberately.
        var (loop, sink) = Phase0Fixtures.Build(Config, seed: 12345UL);
        int ticks = Phase0Fixtures.RunUntilDeath(loop);

        Villager villager = loop.World.Villager;

        Assert.Equal("Dorcas", villager.Name);
        Assert.Equal(45, villager.LifespanYears);
        Assert.Equal(45, villager.AgeYears);
        Assert.Equal(CauseOfDeath.OldAge, villager.CauseOfDeath);
        Assert.Equal(10_801, ticks);
        Assert.Equal(45, villager.WintersSurvived);

        IReadOnlyList<string> log = Phase0Fixtures.LifeLog(sink);
        Assert.Equal("Dorcas begins. Spring, Year 1, no food stored.", log[0]);
        // ⚠️ THE ENDING, NOT THE LAST LINE — the third guard to want this, and all three for one
        // cause. Dorcas masters foraging inside her forty-five years, so her death is followed by
        // a line saying what went with her (`KnowledgeSystem` runs after `MortalitySystem`, by
        // design). **`log[^1]` quietly meant "the death" across this suite and now means "the
        // consequence"** — which is worth knowing before the next system appends anything.
        Assert.Contains(
            "died of old age at 45",
            string.Join(" | ", log.TakeLast(3)),
            StringComparison.Ordinal);
    }
}

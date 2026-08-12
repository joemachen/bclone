using Bclone.Sim.Config;
using Bclone.Sim.Core;
using Bclone.Sim.Logging;

namespace Bclone.Sim.Tests;

/// <summary>Shared configs for the Phase 0 tests.</summary>
public static class Phase0Fixtures
{
    /// <summary>The shipped tuning — the one the player actually experiences.</summary>
    public static SimConfig Plenty => new()
    {
        Seed = 12345UL,

        // Phase 0 is not a special mode - it is the village of one, born at zero.
        StartingHouseholds = 1,
        AdultsPerHousehold = 1,
        FounderAge = 0,

        // A village of one never sprawls, so its economy is derived for exactly
        // one home rather than budgeting for a settlement it will never build.
        EconomyHorizonHouseholds = 1,

        // Phase 0's villager is able-bodied from birth. Its spec says so
        // explicitly and flags it as the known oddity that childhood would fix
        // once households existed - so this encodes the documented Phase 0 world
        // rather than quietly exempting it from the Phase 1 rule.
        AdultAge = 0,

        // No fuel. Phase 0's spec rules warmth out by name — "winter's danger is food
        // scarcity only; do not add a second overlapping death system" — and a lone
        // villager who has to both feed themselves and keep a fire lit is exactly the
        // double jeopardy it refused. Firewood arrives with households to heat and a
        // labour system for it to compete inside (D17, D29); this fixture is the world
        // the Phase 0 spec describes, and it stays that way.
        FirewoodPerWinterDay = 0,

        TicksPerDay = 4,
        DaysPerSeason = 15,
        TargetTicksPerSecond = 1.0,
        // Seven per tick — a meal every 2.8 days. See SimConfig.HungerPerTick for why
        // it is not ten; Phase 0's world moves at the same pace as the village's.
        HungerPerTick = 7,
        HungerMax = 100,
        EatThreshold = 80,
        EatReducesHunger = 80,
        FoodPerMeal = 5,
        StarvationTicks = 24,
        // ⚠️ 28 -> 80, AND IT IS THE SAME WORLD RATHER THAN A RICHER ONE. `gather_yield` is
        // what a trip is worth at a FULLY WOODED ring now; what a villager actually brings
        // home is that scaled by how wooded their hut's ring really is
        // (`SimWorld.GatherYieldAt`). Phase 0's lone villager gathers at the warm start's
        // gatherer's hut, whose ring is about as wooded as the valley — so 28 became about
        // ten a trip and she starved at 37 instead of dying of old age at 45.
        //
        // 28 x 100/35 = 80 puts an AVERAGE ring back where it was — and 80 still starved
        // seeds 99 and 777777, because how wooded a particular hut's ring is varies by
        // valley and those two came in under the average. **This fixture is called `Plenty`
        // and its job is that food is never the constraint** (`Scarcity` is where the other
        // case lives), so it is set with real margin rather than to the mean: 120 is 28 a
        // trip at a ring only 23% wooded.
        //
        // The arithmetic is written out rather than derived because Phase 0's target is its
        // own — one villager, no dependants — and `VillageEconomy` solves for the village's.
        GatherYield = 120,
        GatherTicks = 3,
        TravelTicksPerUnit = 1,
        StockpileTarget = 60,

        // Phase 0's world, expressed as generator rules now that the valley is
        // generated (D18). It used to say ONE patch, five tiles out; the patches are
        // retired and Phase 0's villager gathers at the warm start's gatherer's hut
        // instead, which is the same slice — one person, one food source, one walk.
        //
        // Zero jitter and no river on purpose. Phase 0 is the vertical slice whose
        // whole point is that you can read why one villager lived or died, and a
        // fixture that moved things a tile each seed would put noise in the one place
        // the project most needs none. The village fixture is where varied valleys get
        // exercised.
        SiteJitterTiles = 0,
        FoundingJitterTiles = 0,
        RiverWidthTiles = 0,

        VigourFullUntilAge = 30,
        VigourMinPercent = 55,
        LifespanYearsBase = 45,
        LifespanYearsVariance = 5,
    };

    /// <summary>
    /// A world too thin to live in: a distant patch yielding almost nothing.
    /// Foraging cannot keep up with eating, so the villager dies young.
    /// </summary>
    /// <remarks>
    /// It used to push the patch out to twelve tiles as well as thinning the yield. The
    /// distance lever is gone with the patches — the hut stands where the warm start puts
    /// it — so scarcity is now purely what a trip is worth, which is the half that was
    /// always doing the work.
    /// </remarks>
    public static SimConfig Scarcity => Plenty with
    {
        GatherYield = 3,
        StockpileTarget = 60,
    };

    /// <summary>
    /// The speed the game is meant to be watched at.
    /// </summary>
    /// <remarks>
    /// Joe's pacing constraint is stated at this speed: <b>one in-game year takes
    /// 60 real seconds at 4x</b>, with a lifespan of 40–50 years. So 4x is the
    /// default watching speed, and the slower settings are for studying a
    /// particular season rather than for normal play.
    /// </remarks>
    public const double WatchingSpeed = 4.0;

    /// <summary>Target real seconds per in-game year at <see cref="WatchingSpeed"/>.</summary>
    public const double TargetSecondsPerYearAtWatchingSpeed = 60.0;

    /// <summary>Real seconds one in-game year takes at a given speed.</summary>
    public static double SecondsPerYear(SimConfig config, double speedMultiplier) =>
        config.TicksPerYear / (config.TargetTicksPerSecond * speedMultiplier);

    /// <summary>Real minutes a run of <paramref name="ticks"/> takes at a given speed.</summary>
    public static double RealMinutes(int ticks, SimConfig config, double speedMultiplier) =>
        ticks / (config.TargetTicksPerSecond * speedMultiplier) / 60.0;

    public static (SimLoop Loop, InMemoryLogSink Log) Build(SimConfig config, ulong? seed = null)
    {
        var sink = new InMemoryLogSink();
        return (SimFactory.CreatePhase0(config, sink, seed), sink);
    }

    /// <summary>Run until the villager dies, or give up after <paramref name="maxTicks"/>.</summary>
    public static int RunUntilDeath(SimLoop loop, int maxTicks = 200_000)
    {
        for (int i = 0; i < maxTicks; i++)
        {
            if (!loop.World.Villager.Alive)
            {
                return i;
            }

            loop.StepOnce();
        }

        return maxTicks;
    }

    /// <summary>The life log — INFO entries from the "life" subsystem (spec §7).</summary>
    public static IReadOnlyList<string> LifeLog(InMemoryLogSink sink)
    {
        var lines = new List<string>();
        foreach (LogEntry entry in sink.Entries)
        {
            if (entry.Level == LogLevel.Info && entry.Subsystem == "life")
            {
                lines.Add(entry.Message);
            }
        }

        return lines;
    }
}

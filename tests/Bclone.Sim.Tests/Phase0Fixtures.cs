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

        // Phase 0's villager is able-bodied from birth. Its spec says so
        // explicitly and flags it as the known oddity that childhood would fix
        // once households existed - so this encodes the documented Phase 0 world
        // rather than quietly exempting it from the Phase 1 rule.
        AdultAge = 0,

        TicksPerDay = 4,
        DaysPerSeason = 15,
        TargetTicksPerSecond = 1.0,
        HungerPerTick = 10,
        HungerMax = 100,
        EatThreshold = 80,
        EatReducesHunger = 80,
        FoodPerMeal = 5,
        StarvationTicks = 24,
        GatherYield = 24,
        GatherTicks = 3,
        TravelTicksPerUnit = 1,
        StockpileTarget = 60,
        FoodSourceX = 5,
        VigourFullUntilAge = 30,
        VigourMinPercent = 55,
        LifespanYearsBase = 45,
        LifespanYearsVariance = 5,
    };

    /// <summary>
    /// A world too thin to live in: a distant patch yielding almost nothing.
    /// Foraging cannot keep up with eating, so the villager dies young.
    /// </summary>
    public static SimConfig Scarcity => Plenty with
    {
        GatherYield = 3,
        FoodSourceX = 12,
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

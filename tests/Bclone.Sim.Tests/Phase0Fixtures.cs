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
        TicksPerDay = 4,
        DaysPerSeason = 15,
        TargetTicksPerSecond = 10.0,
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
        LifespanYearsBase = 52,
        LifespanYearsVariance = 6,
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
    /// The speed a life is meant to be <em>watched</em> at.
    /// </summary>
    /// <remarks>
    /// Joe's constraint is that a full life runs 9–12 minutes. After the base tick
    /// rate was halved (the seasons went by faster than they could be read), that
    /// window lands at 2x rather than 1x — so 1x is now the study speed, 2x is the
    /// watching speed, and 4x is the skip gear.
    /// </remarks>
    public const double WatchingSpeed = 2.0;

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

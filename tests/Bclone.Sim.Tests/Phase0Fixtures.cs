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
        TargetTicksPerSecond = 20.0,
        HungerPerTick = 10,
        HungerMax = 100,
        EatThreshold = 80,
        EatReducesHunger = 80,
        FoodPerMeal = 5,
        StarvationTicks = 24,
        GatherYield = 15,
        GatherTicks = 3,
        TravelTicksPerUnit = 1,
        StockpileTarget = 60,
        FoodSourceX = 5,
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

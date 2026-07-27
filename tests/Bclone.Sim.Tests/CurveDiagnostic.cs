using Bclone.Sim.Config;
using Bclone.Sim.Core;
using Bclone.Sim.Logging;
using Bclone.Sim.World;
using Xunit;
using Xunit.Abstractions;

namespace Bclone.Sim.Tests;

/// <summary>TEMPORARY measurement scaffold — delete before committing.</summary>
public sealed class CurveDiagnostic
{
    private readonly ITestOutputHelper _output;

    public CurveDiagnostic(ITestOutputHelper output) => _output = output;

    /// <summary>
    /// feeds=100000 makes the granary effectively unbounded, which is the pre-slice-5
    /// behaviour — the control. Does the village that "holds a stable size" actually
    /// hold it, or is 150 years just a short enough window to miss the decline?
    /// </summary>
    [Theory]
    [InlineData(100000)]
    [InlineData(60)]
    public void ThreeHundredYears(int feeds)
    {
        SimConfig config = VillageFixtures.Village with { GranaryFeedsPeople = feeds };
        SimLoop loop = SimFactory.CreatePhase0(config, new InMemoryLogSink());

        var line = new System.Text.StringBuilder();
        for (int year = 1; year <= 300; year++)
        {
            loop.Step(config.TicksPerYear);
            if (year % 20 == 0) line.Append($"{loop.World.Population,4}");
        }

        int starv = 0, cold = 0, aged = 0;
        foreach (Villager v in loop.World.Villagers)
        {
            if (v.CauseOfDeath == CauseOfDeath.Starvation) starv++;
            if (v.CauseOfDeath == CauseOfDeath.Cold) cold++;
            if (v.CauseOfDeath == CauseOfDeath.OldAge) aged++;
        }

        _output.WriteLine($"RESULT feeds={feeds,6} every 20y:{line} | final {loop.World.Population} "
            + $"| starved {starv}, froze {cold}, old age {aged}");
    }
}

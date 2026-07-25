using Bclone.Sim.Config;
using Bclone.Sim.Core;
using Bclone.Sim.World;
using Xunit;
using Xunit.Abstractions;

namespace Bclone.Sim.Tests;

/// <summary>
/// The two life arcs. These are the tests that say whether Phase 0 works.
/// </summary>
public sealed class Phase0ScenarioTests
{
    private readonly ITestOutputHelper _output;

    public Phase0ScenarioTests(ITestOutputHelper output) => _output = output;

    [Fact]
    public void PlentyConfig_VillagerReachesOldAge()
    {
        var (loop, sink) = Phase0Fixtures.Build(Phase0Fixtures.Plenty);
        Phase0Fixtures.RunUntilDeath(loop);

        Villager villager = loop.World.Villager;

        Assert.False(villager.Alive);
        Assert.Equal(CauseOfDeath.OldAge, villager.CauseOfDeath);
        Assert.True(villager.WintersSurvived >= 40,
            $"Expected a long life, but only survived {villager.WintersSurvived} winters.");

        // The epitaph has to make the good arc unmistakable.
        string last = Phase0Fixtures.LifeLog(sink)[^1];
        Assert.Contains("old age", last, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ScarcityConfig_VillagerStarves()
    {
        var (loop, sink) = Phase0Fixtures.Build(Phase0Fixtures.Scarcity);
        Phase0Fixtures.RunUntilDeath(loop);

        Villager villager = loop.World.Villager;

        Assert.False(villager.Alive);
        Assert.Equal(CauseOfDeath.Starvation, villager.CauseOfDeath);
        Assert.True(villager.AgeYears < 15,
            $"Scarcity should kill young, but they reached {villager.AgeYears}.");

        string last = Phase0Fixtures.LifeLog(sink)[^1];
        Assert.Contains("starved", last, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void TheTwoDeaths_ReadCompletelyDifferently()
    {
        // The Success Test (spec §9) turns on this: a full life must not read like
        // a young death. If these two epitaphs ever converge, the phase has failed
        // regardless of what the rest of the suite says.
        var (plenty, plentyLog) = Phase0Fixtures.Build(Phase0Fixtures.Plenty);
        var (scarce, scarceLog) = Phase0Fixtures.Build(Phase0Fixtures.Scarcity);

        Phase0Fixtures.RunUntilDeath(plenty);
        Phase0Fixtures.RunUntilDeath(scarce);

        string plentyEpitaph = Phase0Fixtures.LifeLog(plentyLog)[^1];
        string scarceEpitaph = Phase0Fixtures.LifeLog(scarceLog)[^1];

        Assert.NotEqual(plentyEpitaph, scarceEpitaph);
        Assert.Contains("winters", plentyEpitaph, StringComparison.OrdinalIgnoreCase);
        Assert.True(
            plenty.World.Villager.AgeYears > scarce.World.Villager.AgeYears * 3,
            "A full life should be dramatically longer than a starvation death.");
    }

    [Fact]
    public void LifeSpansTheIntendedRealTimeWindow()
    {
        // Joe's constraint: a life should take 9-12 minutes to watch.
        // This is a design requirement, so it gets a test rather than a comment.
        SimConfig config = Phase0Fixtures.Plenty;
        var (loop, _) = Phase0Fixtures.Build(config);

        int ticks = Phase0Fixtures.RunUntilDeath(loop);
        double watching = Phase0Fixtures.RealMinutes(ticks, config, Phase0Fixtures.WatchingSpeed);

        _output.WriteLine(
            $"{loop.World.Villager.Name} lived {loop.World.Villager.AgeYears} years — {ticks} ticks. " +
            $"1x: {Phase0Fixtures.RealMinutes(ticks, config, 1.0):F1} min · " +
            $"2x: {watching:F1} min · " +
            $"4x: {Phase0Fixtures.RealMinutes(ticks, config, 4.0):F1} min.");

        Assert.InRange(watching, 9.0, 12.0);
    }

    [Theory]
    [InlineData(1UL)]
    [InlineData(2UL)]
    [InlineData(99UL)]
    [InlineData(4242UL)]
    [InlineData(777777UL)]
    public void EverySeed_ProducesAFullLifeInTheWindow(ulong seed)
    {
        // Lifespan variance must not push any seeded outcome outside the window.
        SimConfig config = Phase0Fixtures.Plenty;
        var (loop, _) = Phase0Fixtures.Build(config, seed);

        int ticks = Phase0Fixtures.RunUntilDeath(loop);
        double minutes = Phase0Fixtures.RealMinutes(ticks, config, Phase0Fixtures.WatchingSpeed);

        Assert.Equal(CauseOfDeath.OldAge, loop.World.Villager.CauseOfDeath);
        Assert.InRange(minutes, 9.0, 12.0);
    }

    [Fact]
    public void TheLifeLogNeverContradictsItself()
    {
        // "Winter came ... Foraging stops." must never be followed by a foraging
        // line. The log is the deliverable; a log that argues with itself fails the
        // phase no matter what the sim underneath is doing.
        var (loop, sink) = Phase0Fixtures.Build(Phase0Fixtures.Plenty);
        Phase0Fixtures.RunUntilDeath(loop);

        IReadOnlyList<string> log = Phase0Fixtures.LifeLog(sink);
        bool foragingStopped = false;

        foreach (string line in log)
        {
            if (line.Contains("Foraging stops", StringComparison.Ordinal))
            {
                foragingStopped = true;
                continue;
            }

            if (line.Contains("survived winter", StringComparison.Ordinal))
            {
                foragingStopped = false;
                continue;
            }

            Assert.False(
                foragingStopped && line.Contains("foraged", StringComparison.OrdinalIgnoreCase),
                $"Life log claims foraging stopped, then reports: {line}");
        }
    }

    [Fact]
    public void TheLifeLogIsShortEnoughToRead()
    {
        // A life should read as a story, not a receipt. Six hundred lines of
        // "Gathered 15 food" is a spreadsheet with extra steps — exactly what the
        // design says this game is not (DESIGN.md §1.4).
        var (loop, sink) = Phase0Fixtures.Build(Phase0Fixtures.Plenty);
        Phase0Fixtures.RunUntilDeath(loop);

        IReadOnlyList<string> log = Phase0Fixtures.LifeLog(sink);

        _output.WriteLine($"{log.Count} life-log entries for a {loop.World.Villager.AgeYears}-year life.");
        Assert.InRange(log.Count, 50, 300);
    }

    /// <summary>
    /// Not an assertion — a window onto the actual story. Run with
    /// <c>dotnet test --logger "console;verbosity=detailed"</c> to read a life.
    /// </summary>
    [Fact]
    public void PrintALife()
    {
        var (loop, sink) = Phase0Fixtures.Build(Phase0Fixtures.Plenty);
        Phase0Fixtures.RunUntilDeath(loop);

        IReadOnlyList<string> log = Phase0Fixtures.LifeLog(sink);
        _output.WriteLine($"=== {log.Count} life-log entries ===");

        for (int i = 0; i < log.Count; i++)
        {
            // Gathering is frequent; show the shape of the life, not every berry.
            if (i < 12 || i >= log.Count - 12 || !log[i].StartsWith("Gathered", StringComparison.Ordinal))
            {
                _output.WriteLine(log[i]);
            }
        }
    }
}

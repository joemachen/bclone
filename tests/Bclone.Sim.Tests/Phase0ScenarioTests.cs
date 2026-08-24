using System.Collections.Generic;
using System.Linq;
using Bclone.Sim.Config;
using Bclone.Sim.Core;
using Bclone.Sim.Logging;
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
    public void AYearTakesSixtySecondsAtTheWatchingSpeed()
    {
        // Joe's pacing constraint, stated directly. Design requirements get tests
        // rather than comments, because a comment cannot fail.
        SimConfig config = Phase0Fixtures.Plenty;
        double seconds = Phase0Fixtures.SecondsPerYear(config, Phase0Fixtures.WatchingSpeed);

        _output.WriteLine(
            $"{config.TicksPerYear} ticks/year at {config.TargetTicksPerSecond} ticks/s — " +
            $"1x: {Phase0Fixtures.SecondsPerYear(config, 1.0):F0}s/yr · " +
            $"4x: {seconds:F0}s/yr.");

        Assert.Equal(Phase0Fixtures.TargetSecondsPerYearAtWatchingSpeed, seconds, precision: 6);
    }

    [Fact]
    public void ALifeSpansFortyToFiftyYears()
    {
        SimConfig config = Phase0Fixtures.Plenty;
        var (loop, _) = Phase0Fixtures.Build(config);

        int ticks = Phase0Fixtures.RunUntilDeath(loop);
        Villager villager = loop.World.Villager;

        _output.WriteLine(
            $"{villager.Name} lived {villager.AgeYears} years — {ticks} ticks = " +
            $"{Phase0Fixtures.RealMinutes(ticks, config, Phase0Fixtures.WatchingSpeed):F1} min at 4x.");

        Assert.InRange(villager.AgeYears, 40, 50);
    }

    [Theory]
    [InlineData(1UL)]
    [InlineData(2UL)]
    [InlineData(99UL)]
    [InlineData(4242UL)]
    [InlineData(777777UL)]
    public void EverySeed_ProducesAFullLifeInTheBand(ulong seed)
    {
        var (loop, _) = Phase0Fixtures.Build(Phase0Fixtures.Plenty, seed);
        Phase0Fixtures.RunUntilDeath(loop);

        // Lifespan variance must not push any seeded outcome outside the band.
        Assert.Equal(CauseOfDeath.OldAge, loop.World.Villager.CauseOfDeath);
        Assert.InRange(loop.World.Villager.AgeYears, 40, 50);
    }

    /// <summary>
    /// "Winter came … Foraging stops." must never be followed by somebody foraging.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The log is the deliverable; a log that argues with itself fails the phase no matter what
    /// the sim underneath is doing.
    /// </para>
    /// <para>
    /// <b>⚠️ A CAREER LINE IS NOT AN ACTIVITY LINE, AND THIS GUARD OUTGREW ITS OWN VOCABULARY
    /// (D200).</b> It matched the bare word *"foraged"*, which was every foraging line in the
    /// world when Phase 0 wrote it. **Mastery narration arrived in D181** — *"Dorcas has foraged
    /// these woods for 18 years"* — and that is a statement about a **life**, not about this
    /// winter. When a cadence change shifted the timings enough to land one inside a winter, the
    /// guard reported a contradiction that is not one. *A guard that outlives the rule it was
    /// written for looks exactly like a regression* (D150).
    /// </para>
    /// <para>
    /// <b>⭐ So the exemption is asked of the config rather than hardcoded here.</b> Each
    /// <c>SkillRow.MasteryLine</c> is a format string; its fixed middle is what a career sentence
    /// reads like, and pulling it from the same place the narration comes from means the guard
    /// cannot drift from the words the game actually says (D142, D148).
    /// </para>
    /// </remarks>
    [Fact]
    public void TheLifeLogNeverContradictsItself()
    {
        var (loop, sink) = Phase0Fixtures.Build(Phase0Fixtures.Plenty);
        Phase0Fixtures.RunUntilDeath(loop);

        IReadOnlyList<string> log = Phase0Fixtures.LifeLog(sink);
        IReadOnlyList<string> careerLines = CareerSentenceFragments(loop.World.Config);
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

            if (careerLines.Any(fragment => line.Contains(fragment, StringComparison.Ordinal)))
            {
                continue;
            }

            Assert.False(
                foragingStopped && line.Contains("foraged", StringComparison.OrdinalIgnoreCase),
                $"Life log claims foraging stopped, then reports: {line}");
        }
    }

    /// <summary>
    /// The fixed middle of every mastery line — what a sentence about a <b>career</b> reads like.
    /// </summary>
    /// <remarks>
    /// Taken from the config's own format strings, so a reworded mastery line changes this guard
    /// with it rather than quietly slipping past.
    /// </remarks>
    private static IReadOnlyList<string> CareerSentenceFragments(SimConfig config)
    {
        var fragments = new List<string>();

        foreach (SkillRow skill in config.Skills)
        {
            string line = skill.MasteryLine;
            int from = line.IndexOf("{0}", StringComparison.Ordinal);
            int to = line.IndexOf("{1}", StringComparison.Ordinal);

            if (from >= 0 && to > from)
            {
                fragments.Add(line[(from + 3)..to].Trim());
            }
        }

        return fragments;
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

    [Fact]
    public void ACleanPlaythroughLogsNoErrorsOrWarnings()
    {
        // Definition of Done item 5 (METHODOLOGY.md §3): no new errors in the log
        // during a clean playthrough. Asserted rather than eyeballed, and across
        // both arcs — a starvation run exercises different code than a full life.
        foreach (SimConfig config in new[] { Phase0Fixtures.Plenty, Phase0Fixtures.Scarcity })
        {
            var sink = new InMemoryLogSink(LogLevel.Trace);
            SimLoop loop = SimFactory.CreatePhase0(config, sink);
            Phase0Fixtures.RunUntilDeath(loop);
            loop.Step(1_000);   // and well past the death, too

            var bad = new List<LogEntry>();
            foreach (LogEntry entry in sink.Entries)
            {
                if (entry.Level >= LogLevel.Warn)
                {
                    bad.Add(entry);
                }
            }

            Assert.True(bad.Count == 0,
                $"Clean playthrough logged {bad.Count} warning(s)/error(s), first: {(bad.Count > 0 ? bad[0].ToString() : "-")}");
        }
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

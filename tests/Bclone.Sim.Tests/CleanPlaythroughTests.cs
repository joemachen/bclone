using Bclone.Sim.Config;
using Bclone.Sim.Core;
using Bclone.Sim.Logging;
using Xunit;
using Xunit.Abstractions;

namespace Bclone.Sim.Tests;

/// <summary>
/// Definition of Done item 5 (<c>METHODOLOGY.md §3</c>): <b>no new errors in the log
/// during a clean playthrough.</b>
/// </summary>
/// <remarks>
/// <para>
/// This was a manual check, which meant it was a check nobody ran. It is a test now,
/// because the sim already logs everything through one tick-stamped sink and asserting
/// on it costs nothing.
/// </para>
/// <para>
/// It is not decoration. <c>METHODOLOGY.md §4</c> forbids swallowing exceptions —
/// catch, log with context, then handle or fail loudly — so the codebase deliberately
/// contains handlers that log an <c>ERROR</c> and carry on rather than throwing. The
/// stockpile underflow guard in <c>BehaviorSystem.TryEat</c> is exactly that: it says
/// "this is a bug" in its own message and keeps the village running. Without this
/// test, a bug of that shape would be reported faithfully to a log nobody reads.
/// </para>
/// </remarks>
public sealed class CleanPlaythroughTests
{
    private readonly ITestOutputHelper _output;

    public CleanPlaythroughTests(ITestOutputHelper output) => _output = output;

    [Fact]
    public void AFullVillageRunsForACenturyAndAHalfWithoutLoggingAWarningOrAnError()
    {
        SimConfig config = VillageFixtures.Village;
        var log = new InMemoryLogSink(LogLevel.Warn);
        SimLoop loop = SimFactory.CreatePhase0(config, log);

        loop.Step(config.TicksPerYear * 150);

        foreach (LogEntry entry in log.Entries)
        {
            _output.WriteLine(entry.ToString());
        }

        Assert.True(log.Entries.Count == 0,
            $"A clean playthrough logged {log.Entries.Count} warnings or errors; " +
            $"the first was: {(log.Entries.Count > 0 ? log.Entries[0].ToString() : string.Empty)}");
    }

    [Fact]
    public void ThePhase0LoneVillagerRunsCleanToo()
    {
        // Phase 0 is not a special mode, it is the one-household-one-adult case — so
        // it has to stay clean as well, or the claim that Phase 1 subsumes it is false.
        SimConfig config = Phase0Fixtures.Plenty;
        var log = new InMemoryLogSink(LogLevel.Warn);
        SimLoop loop = SimFactory.CreatePhase0(config, log);

        loop.Step(config.TicksPerYear * 60);

        foreach (LogEntry entry in log.Entries)
        {
            _output.WriteLine(entry.ToString());
        }

        Assert.Empty(log.Entries);
    }

    [Fact]
    public void TheWarningGuardCanActuallyFail()
    {
        // Anti-vacuity (D7). A clean-log test that cannot go red is a test that buys
        // false confidence forever, so this proves the sink reports what it is asked to.
        var log = new InMemoryLogSink(LogLevel.Warn);
        log.Log(1UL, LogLevel.Error, "test", "a deliberate error");
        log.Log(2UL, LogLevel.Debug, "test", "a debug line that must not be counted");

        Assert.Single(log.Entries);
        Assert.Equal(LogLevel.Error, log.Entries[0].Level);
    }
}

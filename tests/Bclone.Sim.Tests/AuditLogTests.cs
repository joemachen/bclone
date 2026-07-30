using Bclone.Sim.Config;
using Bclone.Sim.Core;
using Bclone.Sim.Logging;
using Bclone.Sim.World;
using Xunit;
using Xunit.Abstractions;

namespace Bclone.Sim.Tests;

/// <summary>
/// The audit trail — every event, tick-stamped (METHODOLOGY §4).
/// </summary>
/// <remarks>
/// <para>
/// The village log the player reads is the INFO view of this same stream (D8), kept
/// deliberately sparse because six hundred foraging trips would bury the handful of
/// lines that carry the story (D9). <b>This is the other view: everything.</b>
/// </para>
/// <para>
/// What makes it worth testing rather than trusting is that a log is only useful if it
/// can answer a question. So these assert the questions: can I follow one villager
/// through a day? Can I find out why the village rearranged its work? Can I see where a
/// load of food went?
/// </para>
/// </remarks>
public sealed class AuditLogTests
{
    private readonly ITestOutputHelper _output;

    public AuditLogTests(ITestOutputHelper output) => _output = output;

    private static SimConfig Config => VillageFixtures.Village;

    private static (SimLoop Loop, InMemoryLogSink Sink) Audited(SimConfig config)
    {
        var sink = new InMemoryLogSink(LogLevel.Debug);
        return (SimFactory.CreatePhase0(config, sink), sink);
    }

    [Fact]
    public void OneVillagersDayCanBeFollowedFromEndToEnd()
    {
        SimConfig config = Config;
        var (loop, sink) = Audited(config);
        loop.Step(config.TicksPerYear);

        Villager subject = loop.World.Villagers[0];
        var theirs = new List<LogEntry>();
        foreach (LogEntry entry in sink.Entries)
        {
            if (entry.Message.Contains($"{subject.Name} #{subject.Id}:", System.StringComparison.Ordinal))
            {
                theirs.Add(entry);
            }
        }

        _output.WriteLine($"{theirs.Count} entries for {subject.Name} in one year. First six:");
        for (int i = 0; i < System.Math.Min(6, theirs.Count); i++)
        {
            _output.WriteLine($"  {theirs[i]}");
        }

        Assert.True(theirs.Count > 20,
            $"Only {theirs.Count} entries for {subject.Name} across a whole year — that is not a trail.");
    }

    [Fact]
    public void EveryEntryIsTickStampedAndAttributedToASubsystem()
    {
        // METHODOLOGY §4's requirement, asserted rather than assumed: a line that
        // cannot be tied back to an exact simulation state is a line you cannot debug
        // from.
        SimConfig config = Config;
        var (loop, sink) = Audited(config);
        loop.Step(config.TicksPerYear * 2);

        Assert.NotEmpty(sink.Entries);
        foreach (LogEntry entry in sink.Entries)
        {
            Assert.False(string.IsNullOrWhiteSpace(entry.Subsystem),
                $"An entry arrived with no subsystem: {entry.Message}");
            Assert.False(string.IsNullOrWhiteSpace(entry.Message));
        }
    }

    [Fact]
    public void TheTrailCoversWorkGoodsAndNeeds()
    {
        // The three things you actually want to reconstruct: what people did, what they
        // carried, and what it cost them. A trail missing any one of them cannot answer
        // "why did that household starve".
        SimConfig config = Config;
        var (loop, sink) = Audited(config);
        loop.Step(config.TicksPerYear * 5);

        var subsystems = new HashSet<string>();
        foreach (LogEntry entry in sink.Entries)
        {
            subsystems.Add(entry.Subsystem);
        }

        _output.WriteLine($"subsystems seen: {string.Join(", ", subsystems.OrderBy(s => s))}");

        Assert.Contains("behavior", subsystems);
        Assert.Contains("goods", subsystems);
        Assert.Contains("labour", subsystems);
        Assert.Contains("life", subsystems);
    }

    [Fact]
    public void WhyTheVillageRearrangedItsWorkIsInTheLog()
    {
        // The per-villager sentence answers "why is she doing that?" for whoever is
        // looking now. The log has to answer "why did the whole village rearrange
        // itself in year 84?" — which is a question nobody can ask a UI panel a century
        // later.
        SimConfig config = Config;
        var (loop, sink) = Audited(config);
        loop.Step(config.TicksPerYear * 3);

        string? demand = null;
        string? assignment = null;

        foreach (LogEntry entry in sink.Entries)
        {
            if (entry.Subsystem != "labour")
            {
                continue;
            }

            if (entry.Message.StartsWith("The village wants:", System.StringComparison.Ordinal))
            {
                demand ??= entry.Message;
            }
            else if (entry.Message.Contains("Took work at", System.StringComparison.Ordinal))
            {
                assignment ??= entry.Message;
            }
        }

        _output.WriteLine(demand ?? "(no quota logged)");
        _output.WriteLine(assignment ?? "(no assignment logged)");

        Assert.True(demand is not null, "The quota behind a reshuffle was never written down.");
        Assert.True(assignment is not null, "No individual assignment was written down.");
    }

    [Fact]
    public void ADeathSaysWhatKilledThem()
    {
        // The one entry that has to be unambiguous. D17 made the whole reversal of
        // Phase 0's no-second-death-system rule conditional on this: a death must never
        // be ambiguous between cold and hunger.
        SimConfig config = Config;
        var (loop, sink) = Audited(config);
        loop.Step(config.TicksPerYear * 80);

        var deaths = new List<string>();
        foreach (LogEntry entry in sink.Entries)
        {
            if (entry.Subsystem == "life"
                && (entry.Message.Contains("died", System.StringComparison.OrdinalIgnoreCase)
                    || entry.Message.Contains("froze", System.StringComparison.OrdinalIgnoreCase)
                    || entry.Message.Contains("starved", System.StringComparison.OrdinalIgnoreCase)))
            {
                deaths.Add(entry.Message);
            }
        }

        _output.WriteLine($"{deaths.Count} deaths narrated. First: {(deaths.Count > 0 ? deaths[0] : "-")}");
        Assert.True(deaths.Count > 0, "Eighty years and nobody died, so this guard is vacuous (D7).");
    }

    [Fact]
    public void TheQuietSinkStaysQuietAndCostsNothing()
    {
        // Anti-vacuity in the other direction, and the reason every DEBUG line is
        // guarded: a village logging at INFO must not be paying to build DEBUG strings
        // it then throws away. The 300-year acceptance runs depend on this.
        SimConfig config = Config;
        var quiet = new InMemoryLogSink(LogLevel.Info);
        SimLoop loop = SimFactory.CreatePhase0(config, quiet);
        loop.Step(config.TicksPerYear * 5);

        foreach (LogEntry entry in quiet.Entries)
        {
            Assert.True(entry.Level >= LogLevel.Info,
                $"A {entry.Level} entry reached a sink that only wanted Info: {entry.Message}");
        }

        _output.WriteLine($"{quiet.Entries.Count} entries at Info over five years.");
    }
}

using System.Collections.Generic;
using Bclone.Sim.Config;
using Bclone.Sim.Core;
using Bclone.Sim.Logging;
using Xunit;
using Xunit.Abstractions;

namespace Bclone.Sim.Tests;

/// <summary>
/// The village log says what KIND of thing each line is — Joe, 2026-08-27.
/// </summary>
/// <remarks>
/// <para>
/// <i>"the village log is doing so much work that maybe it needs color coded entries and a filter
/// for each category? deaths, important events in colors, general info in white. optimize noise
/// to signal ratio."</i>
/// </para>
/// <para>
/// <b>⭐ Decided at the source, which was his call.</b> The view could have guessed a category by
/// matching words in the sentence — and that would be a second place that knows what a death
/// looks like, wrong the first time somebody rephrases an epitaph. The system that raises the
/// event says what kind it is; the view only picks a colour.
/// </para>
/// <para>
/// ⚠️ <b>The failure this guards against is silent and total.</b> Every category defaults to
/// <see cref="LogCategory.Ordinary"/>, so a categorisation that was never applied — or one
/// dropped in a later edit — leaves a log that renders and filters perfectly and is <em>entirely
/// one colour</em>. Nothing would throw and no other test would notice.
/// </para>
/// </remarks>
public sealed class LogCategoryTests
{
    private readonly ITestOutputHelper _output;

    public LogCategoryTests(ITestOutputHelper output) => _output = output;

    /// <summary>⭐ A played century speaks in more than one voice.</summary>
    [Fact]
    public void APlayedVillageProducesSeveralCategoriesAndNotJustOrdinary()
    {
        SimConfig config = VillageFixtures.Village;
        var sink = new InMemoryLogSink();
        SimLoop loop = SimFactory.CreatePhase0(config, sink);

        ColdStartTests.PlayTheOpening(loop.World);
        loop.Step(config.TicksPerYear * 60);

        var seen = new Dictionary<LogCategory, int>();
        foreach (LogEntry entry in sink.Entries)
        {
            if (entry.Subsystem != "life")
            {
                continue;
            }

            seen.TryGetValue(entry.Category, out int count);
            seen[entry.Category] = count + 1;
        }

        foreach (KeyValuePair<LogCategory, int> pair in seen)
        {
            _output.WriteLine($"{pair.Key,-10} {pair.Value}");
        }

        // ⚠️ ANTI-VACUITY (D7): a village that never narrated anything proves nothing.
        Assert.NotEmpty(seen);

        // ⭐ THE CLAIM. Sixty years of a working village kills people, builds things, turns
        // seasons and works techniques out — so a log with one category in it means the
        // categorisation is not reaching the call sites, whatever the enum says.
        Assert.True(
            seen.Count >= 4,
            $"Sixty years produced only {seen.Count} distinct log categories, so the "
                + "categorisation is not reaching the sentences. Everything defaults to "
                + "Ordinary, which renders and filters perfectly while being one colour.");

        // ⭐ And the ones a player would actually go looking for are among them. These are the
        // filters that justify the feature — a log that cannot separate a death from a season
        // summary has not changed the noise-to-signal ratio it exists to change.
        Assert.Contains(LogCategory.Death, seen.Keys);
        Assert.Contains(LogCategory.Building, seen.Keys);
        Assert.Contains(LogCategory.Season, seen.Keys);
    }

    /// <summary>⛔ An uncategorised line still reads exactly as it always did.</summary>
    /// <remarks>
    /// <b>The audit trail is the thing this project debugs from</b>, and it is greppable by
    /// habit. <see cref="LogCategory.Ordinary"/> writes no tag at all, so the tens of thousands
    /// of DEBUG lines in a played log are byte-identical to what they were and every existing
    /// grep still works. **A categorised line gains exactly one bracketed word.**
    /// </remarks>
    [Fact]
    public void OnlyACategorisedLineGainsATagInTheAuditTrail()
    {
        var plain = new LogEntry(7UL, LogLevel.Debug, "behavior", "Otto walked home.");
        var tagged = new LogEntry(7UL, LogLevel.Info, "life", "Otto died.", LogCategory.Death);

        _output.WriteLine(plain.ToString());
        _output.WriteLine(tagged.ToString());

        Assert.DoesNotContain("[", plain.ToString()[10..], System.StringComparison.Ordinal);
        Assert.Contains("[death]", tagged.ToString(), System.StringComparison.Ordinal);
    }
}

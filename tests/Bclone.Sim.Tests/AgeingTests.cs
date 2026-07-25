using Bclone.Sim.Config;
using Bclone.Sim.Systems;
using Bclone.Sim.World;
using Xunit;
using Xunit.Abstractions;

namespace Bclone.Sim.Tests;

/// <summary>
/// Ageing is the only source of year-to-year variation in Phase 0, so these tests
/// are really about whether a life has a <em>shape</em> — not just whether a number
/// goes down.
/// </summary>
public sealed class AgeingTests
{
    private readonly ITestOutputHelper _output;

    public AgeingTests(ITestOutputHelper output) => _output = output;

    private static SimConfig Config => Phase0Fixtures.Plenty;

    [Fact]
    public void PrimeYears_AreFullStrength()
    {
        for (int age = 0; age <= Config.VigourFullUntilAge; age++)
        {
            Assert.Equal(100, AgeingSystem.ComputeVigour(age, 52, Config));
        }
    }

    [Fact]
    public void VigourDeclinesMonotonicallyAfterThePeak()
    {
        int previous = 101;
        for (int age = Config.VigourFullUntilAge; age <= 52; age++)
        {
            int vigour = AgeingSystem.ComputeVigour(age, 52, Config);
            Assert.True(vigour <= previous, $"Vigour rose at age {age}: {previous} -> {vigour}.");
            previous = vigour;
        }
    }

    [Fact]
    public void VigourNeverFallsBelowTheConfiguredFloor()
    {
        for (int age = 0; age <= 100; age++)
        {
            Assert.InRange(AgeingSystem.ComputeVigour(age, 52, Config), Config.VigourMinPercent, 100);
        }
    }

    [Fact]
    public void FinalYearReachesTheFloor()
    {
        Assert.Equal(Config.VigourMinPercent, AgeingSystem.ComputeVigour(52, 52, Config));
    }

    [Fact]
    public void AVillagerWhoDiesYoungNeverDeclines()
    {
        // Guards the divide-by-zero when a drawn lifespan lands at or below the
        // prime-years cutoff — and encodes the reading that matters: someone who
        // dies at thirty died young, they did not die frail.
        Assert.Equal(100, AgeingSystem.ComputeVigour(30, 30, Config));
        Assert.Equal(100, AgeingSystem.ComputeVigour(5, 10, Config));
        Assert.Equal(100, AgeingSystem.ComputeVigour(29, 30, Config));
    }

    [Theory]
    [InlineData(100, VigourStage.Prime)]
    [InlineData(99, VigourStage.Slowing)]
    [InlineData(81, VigourStage.Slowing)]
    [InlineData(80, VigourStage.Frail)]
    [InlineData(70, VigourStage.Frail)]
    public void StagesBandCorrectly(int vigour, VigourStage expected)
    {
        Assert.Equal(expected, AgeingSystem.StageFor(vigour));
    }

    // ---------------------------------------------------------------
    //  The point of the whole exercise
    // ---------------------------------------------------------------

    [Fact]
    public void OldAgeCostsMoreWorkForTheSameFood()
    {
        // The flat-middle fix. If a season in old age takes no more effort than a
        // season in youth, ageing is still just a countdown and Phase 0 is still a
        // spreadsheet.
        var (loop, sink) = Phase0Fixtures.Build(Config);
        Phase0Fixtures.RunUntilDeath(loop);

        var youngSeasons = new List<int>();
        var oldSeasons = new List<int>();

        foreach (string line in Phase0Fixtures.LifeLog(sink))
        {
            if (!line.Contains("foraged", StringComparison.Ordinal))
            {
                continue;
            }

            int year = ExtractYear(line);
            int trips = ExtractTrips(line);
            if (year <= 15)
            {
                youngSeasons.Add(trips);
            }
            else if (year >= 35)
            {
                oldSeasons.Add(trips);
            }
        }

        Assert.NotEmpty(youngSeasons);
        Assert.NotEmpty(oldSeasons);

        double young = Average(youngSeasons);
        double old = Average(oldSeasons);
        _output.WriteLine($"Average foraging trips per season — young: {young:F2}, old: {old:F2}");

        Assert.True(old > young,
            $"Old age must cost more work: young {young:F2} trips/season vs old {old:F2}.");
    }

    [Fact]
    public void TheDeclineIsNarratedSoThePlayerCanSeeIt()
    {
        // A mechanic the player cannot read is not legible, and legibility is the
        // deliverable (non-negotiable 1).
        var (loop, sink) = Phase0Fixtures.Build(Config);
        Phase0Fixtures.RunUntilDeath(loop);

        IReadOnlyList<string> log = Phase0Fixtures.LifeLog(sink);

        Assert.Contains(log, l => l.Contains("past her strongest years", StringComparison.Ordinal));
        Assert.Contains(log, l => l.Contains("grown frail", StringComparison.Ordinal));
    }

    [Fact]
    public void DeclineNarrationFiresOncePerStage()
    {
        var (loop, sink) = Phase0Fixtures.Build(Config);
        Phase0Fixtures.RunUntilDeath(loop);

        IReadOnlyList<string> log = Phase0Fixtures.LifeLog(sink);

        Assert.Equal(1, Count(log, "past her strongest years"));
        Assert.Equal(1, Count(log, "grown frail"));
    }

    [Fact]
    public void AgeingDoesNotTurnOldAgeIntoStarvation()
    {
        // Declining vigour must make old age HARD, not fatal. If the villager
        // starves at 48 every run, the two death arcs stop reading differently and
        // the phase loses its point.
        for (ulong seed = 1; seed <= 25; seed++)
        {
            var (loop, _) = Phase0Fixtures.Build(Config, seed);
            Phase0Fixtures.RunUntilDeath(loop);

            Assert.Equal(CauseOfDeath.OldAge, loop.World.Villager.CauseOfDeath);
        }
    }

    [Fact]
    public void YearsAreNoLongerIdentical()
    {
        // The finding this whole change exists to fix: every year from 2 to 50 used
        // to be byte-identical. Compare the seasonal summaries of a young year and
        // an old one — they must differ.
        var (loop, sink) = Phase0Fixtures.Build(Config);
        Phase0Fixtures.RunUntilDeath(loop);

        List<string> young = SeasonLinesForYear(sink, 8);
        List<string> old = SeasonLinesForYear(sink, 42);

        Assert.NotEmpty(young);
        Assert.NotEmpty(old);
        Assert.NotEqual(
            string.Join("|", StripYear(young)),
            string.Join("|", StripYear(old)));
    }

    // ---------------------------------------------------------------

    private static double Average(List<int> values)
    {
        int total = 0;
        foreach (int v in values)
        {
            total += v;
        }

        return (double)total / values.Count;
    }

    private static int Count(IReadOnlyList<string> log, string needle)
    {
        int count = 0;
        foreach (string line in log)
        {
            if (line.Contains(needle, StringComparison.Ordinal))
            {
                count++;
            }
        }

        return count;
    }

    private static List<string> SeasonLinesForYear(Bclone.Sim.Logging.InMemoryLogSink sink, int year)
    {
        var lines = new List<string>();
        foreach (string line in Phase0Fixtures.LifeLog(sink))
        {
            if (line.Contains("foraged", StringComparison.Ordinal) && ExtractYear(line) == year)
            {
                lines.Add(line);
            }
        }

        return lines;
    }

    private static List<string> StripYear(List<string> lines)
    {
        var stripped = new List<string>();
        foreach (string line in lines)
        {
            stripped.Add(line.Replace($"Year {ExtractYear(line)}", "Year N", StringComparison.Ordinal));
        }

        return stripped;
    }

    /// <summary>Pull the year out of "Spring of Year 12 — ...".</summary>
    private static int ExtractYear(string line)
    {
        const string Marker = "Year ";
        int start = line.IndexOf(Marker, StringComparison.Ordinal);
        if (start < 0)
        {
            return -1;
        }

        start += Marker.Length;
        int end = start;
        while (end < line.Length && char.IsDigit(line[end]))
        {
            end++;
        }

        return int.Parse(line[start..end]);
    }

    /// <summary>Pull the trip count out of "... foraged 4 times ...".</summary>
    private static int ExtractTrips(string line)
    {
        const string Marker = "foraged ";
        int start = line.IndexOf(Marker, StringComparison.Ordinal) + Marker.Length;
        int end = start;
        while (end < line.Length && char.IsDigit(line[end]))
        {
            end++;
        }

        return int.Parse(line[start..end]);
    }
}

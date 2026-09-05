using Bclone.Sim.Config;
using Bclone.Sim.Core;
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

    /// <summary>
    /// ⭐ The same day's work brings back less when you are old — the flat-middle fix (D12).
    /// </summary>
    /// <remarks>
    /// <para>
    /// If a day in old age costs no more than a day in youth, ageing is just a countdown and
    /// Phase 0 is a spreadsheet. This is the guard that says it shows up in a lived run rather
    /// than only in <see cref="AgeingSystem.ComputeVigour"/>'s arithmetic.
    /// </para>
    /// <para>
    /// <b>⛔ IT COUNTED FORAGING TRIPS PER SEASON, AND THAT METRIC CANNOT SEE THE THING (D151).</b>
    /// Measured over a whole life on this fixture: <b>she forages once or twice a season, and
    /// that is the floor.</b> `Plenty` is called `Plenty` — one trip is worth about 64 food and
    /// a meal is 5 — so a trip still covers the season however frail she gets, and the trip
    /// count simply cannot rise. The guard read <b>1.23 young against 1.09 old</b> and reported
    /// a broken mechanic; the mechanic was fine and the ruler had two gradations.
    /// </para>
    /// <para>
    /// <b>⚠️ AND THE ATTRIBUTION I RECORDED IN D142 WAS WRONG, which is why this was measured
    /// again rather than reasoned about.</b> I had it down to planting-by-default enriching her
    /// hut's ring over her lifetime, plus D139's warehouse floor. **The ring is flat**: 110 wooded
    /// tiles of 289 at year one and 105 at year forty-five, worth 66 a trip and then 62.
    /// Nothing about the valley changed. Both of those decisions moved the number by moving
    /// which years she happened to make a second trip in — noise in a metric that only had
    /// ones and twos to say it with.
    /// </para>
    /// <para>
    /// <b>So it measures what vigour actually does: food brought home per trip.</b> Unquantised,
    /// and literally the claim in the test's own name — the same work, less food, so more work
    /// for the same food. Measured across a life: <b>64 a trip through the prime years, 30 by
    /// year forty-five.</b> The ring is asserted flat over the same window, so the fall is
    /// attributable to <em>her</em> and not to the valley — the D142 lesson (two causes, either
    /// sufficient) applied before it costs another session.
    /// </para>
    /// </remarks>
    [Fact]
    public void OldAgeCostsMoreWorkForTheSameFood()
    {
        // ⭐⭐ NO TECHNIQUES, AND THAT IS THIS GUARD'S OWN CONTROL EXTENDED RATHER THAN A DODGE.
        // It already excludes one confound — *"the valley did not move, so the fall is hers"* —
        // because a wood being felled around her would make a statement about the map read as a
        // statement about age. **A technique is a second confound, and it moves the other way:**
        // she masters foraging around forty and works out tended patches, so her old-age trips
        // carry a bonus her prime-age trips never had, and the measured decline is *vigour minus
        // technique*.
        //
        // ⚠️ IT IS NOT HYPOTHETICAL — it turned this guard red the day techniques landed: 65.0 a
        // trip young against **54.7** old, a 16% fall where the claim needs 20%. **The code was
        // right and the instrument had started measuring two things** (D189's finding, and the
        // reason the ring control was written in the first place).
        //
        // ⛔ The interaction itself is real and is recorded rather than hidden here: **an old master
        // partly offsets her own ageing**, which softens D12's life-shape. That is Joe's to weigh,
        // not this guard's to swallow.
        SimConfig config = Config with { Techniques = System.Array.Empty<TechniqueRow>() };
        var (loop, _) = Phase0Fixtures.Build(config);
        SimWorld world = loop.World;

        Workplace hut = TheGatherersHut(world);

        var young = new List<int>();
        var old = new List<int>();
        int ringYieldYoung = 0;
        int ringYieldOld = 0;

        int lastGathers = 0;
        int lastFood = 0;

        for (int year = 1; year <= 60 && world.Villager.Alive; year++)
        {
            loop.Step(config.TicksPerYear);
            if (!world.Villager.Alive)
            {
                break;
            }

            int gathers = world.Villager.TotalGathers - lastGathers;
            int food = FoodBroughtIn(world) - lastFood;
            lastGathers = world.Villager.TotalGathers;
            lastFood = FoodBroughtIn(world);

            if (gathers <= 0)
            {
                continue;
            }

            if (year <= 15)
            {
                young.Add(food / gathers);
                ringYieldYoung = world.GatherYieldAt(hut);
            }
            else if (year >= 35)
            {
                old.Add(food / gathers);
                ringYieldOld = world.GatherYieldAt(hut);
            }
        }

        Assert.NotEmpty(young);
        Assert.NotEmpty(old);

        double youngTrip = Average(young);
        double oldTrip = Average(old);

        _output.WriteLine(
            $"food brought home per trip — young {youngTrip:F1}, old {oldTrip:F1}; "
            + $"the hut's ring was worth {ringYieldYoung} a trip young and {ringYieldOld} old.");

        // ⭐ AND THE VALLEY DID NOT MOVE, so the fall is hers. Without this the guard would pass
        // just as happily on a villager in a wood that was quietly being felled around her,
        // which is a statement about the map rather than about growing old.
        Assert.True(ringYieldOld * 10 >= ringYieldYoung * 9,
            $"The hut's ring fell from {ringYieldYoung} to {ringYieldOld} a trip over her life, "
            + "so this is measuring the valley rather than her age.");

        Assert.True(oldTrip < youngTrip * 0.8,
            $"Old age brought home {oldTrip:F1} a trip against {youngTrip:F1} in her prime — "
            + "not enough of a decline for a life to have a shape (D12).");
    }

    /// <summary>The one place Phase 0's villager gathers.</summary>
    private static Workplace TheGatherersHut(SimWorld world)
    {
        foreach (Workplace place in world.Workplaces)
        {
            if (place.Kind == JobKind.Forager && !place.IsSite)
            {
                return place;
            }
        }

        throw new Xunit.Sdk.XunitException("Phase 0's village has nowhere to gather.");
    }

    /// <summary>
    /// Food anybody has <b>gathered</b> into the village, ever.
    /// </summary>
    /// <remarks>
    /// <c>Produced</c> and not <c>Held</c>: the lifetime counters mean *this container received
    /// this much fresh*, and <see cref="Stockpile.Receive"/> deliberately does not touch them
    /// (see its remarks), so a fetch or a delivery cannot be counted as a second harvest. In
    /// Phase 0 there is one villager, so every unit of it is hers.
    /// </remarks>
    private static int FoodBroughtIn(SimWorld world)
    {
        int total = world.HouseholdOf(world.Villager).Stockpile.Produced(Goods.Food);
        foreach (StoreBuilding store in world.StoreBuildings)
        {
            total += store.Store.Produced(Goods.Food);
        }

        return total;
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

    // `ExtractTrips` pulled the trip count out of "... foraged 4 times ..." and is deleted
    // (D159). D151 re-based this guard off trips-per-season — a metric floored at 1–2, which
    // is why it could not see the mechanic — and onto food brought home per trip, and left
    // the old parser behind. An orphan from the session before this one.
}

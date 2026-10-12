using Bclone.Sim.Config;
using Bclone.Sim.World;
using Xunit;

namespace Bclone.Sim.Tests;

/// <summary>
/// Condition is derived from two stamps and the calendar — <c>specs/building-condition.md §4.1–4.2</c> (D552; Joe,
/// D553: *"yes to all"*). Pure: every guard here poses the stamps and reads the answer, and none of them runs a village.
/// </summary>
public sealed class ConditionTests
{
    private static SimConfig Shipped => ShippedConfig.Load();

    /// <summary>A row that wears at the shipped years; the numbers are read, not typed, so a re-tuned key moves nothing here.</summary>
    private static BuildingRow Timber(SimConfig config) => new()
    {
        Id = 99,
        Name = "a timber thing",
        WearsInYears = config.BuildingWearsInYears,
        FailsInYears = config.BuildingFailsInYears,
        RuinedInYears = config.BuildingRuinedInYears,
    };

    private static BuildingRow NeverWears => new() { Id = 98, Name = "a stone thing" };

    // ---------------------------------------------------------------
    //  §4.2 — the four states, at the exact tick
    // ---------------------------------------------------------------

    [Fact]
    public void TheStatesTurnAtTheirYearsExactly()
    {
        SimConfig config = Shipped;
        BuildingRow row = Timber(config);
        ulong year = (ulong)config.TicksPerYear;

        Assert.Equal(Condition.Sound, ConditionRules.Of(0, row, config));
        Assert.Equal(Condition.Sound, ConditionRules.Of(((ulong)row.WearsInYears * year) - 1, row, config));
        Assert.Equal(Condition.Worn, ConditionRules.Of((ulong)row.WearsInYears * year, row, config));
        Assert.Equal(Condition.Worn, ConditionRules.Of(((ulong)row.FailsInYears * year) - 1, row, config));
        Assert.Equal(Condition.Failing, ConditionRules.Of((ulong)row.FailsInYears * year, row, config));
        Assert.Equal(Condition.Failing, ConditionRules.Of(((ulong)row.RuinedInYears * year) - 1, row, config));
        Assert.Equal(Condition.Ruin, ConditionRules.Of((ulong)row.RuinedInYears * year, row, config));
        Assert.Equal(Condition.Ruin, ConditionRules.Of(1000UL * year, row, config));
    }

    [Fact]
    public void ARowThatNeverWearsIsSoundAtAThousandYears()
    {
        SimConfig config = Shipped;
        ulong aThousandYears = 1000UL * (ulong)config.TicksPerYear;
        Assert.Equal(Condition.Sound, ConditionRules.Of(0, 0, aThousandYears, NeverWears, config));
        Assert.Null(ConditionRules.YearsUntil(aThousandYears, Condition.Worn, NeverWears, config));
    }

    // ---------------------------------------------------------------
    //  §4.1 — the wear: ticks since the later stamp, and a winter's extra
    // ---------------------------------------------------------------

    [Fact]
    public void WearIsTheTicksSinceTheLaterStampAndNothingBeforeIt()
    {
        SimConfig config = Shipped;
        ulong season = (ulong)config.TicksPerSeason;

        // Spring to summer: no winter begun, so the wear is the ticks and nothing more.
        Assert.Equal(season, ConditionRules.WearTicks(0, 0, season, config));

        // A repair later than the raise is where the clock starts over.
        Assert.Equal(1UL, ConditionRules.WearTicks(0, season - 1, season, config));

        // Nothing is owed before a thing stands.
        Assert.Equal(0UL, ConditionRules.WearTicks(season, 0, season, config));
        Assert.Equal(0UL, ConditionRules.WearTicks(season, 0, season - 5, config));
    }

    [Fact]
    public void AWinterBegunAddsExactlyItsExtraAndASummerNone()
    {
        SimConfig config = Shipped;
        ulong season = (ulong)config.TicksPerSeason;
        ulong year = (ulong)config.TicksPerYear;
        ulong firstWinter = 3 * season;
        ulong extra = season * (ulong)config.WinterWearExtraPercent / 100;
        Assert.True(extra > 0, "the shipped winter adds nothing, so this guard cannot see it");

        // The tick before winter to the tick it begins: one tick of age, and the winter's whole extra.
        Assert.Equal(1 + extra, ConditionRules.WearTicks(firstWinter - 1, 0, firstWinter, config));

        // A thing raised on winter's first tick did not see that winter begin — it is not charged for it.
        Assert.Equal(1UL, ConditionRules.WearTicks(firstWinter, 0, firstWinter + 1, config));

        // A whole year from New Year begins exactly one winter; ten years, ten.
        Assert.Equal(year + extra, ConditionRules.WearTicks(0, 0, year, config));
        Assert.Equal((10 * year) + (10 * extra), ConditionRules.WearTicks(0, 0, 10 * year, config));

        // The same length of summer, no winter in it: the ticks alone.
        Assert.Equal(season, ConditionRules.WearTicks(season, 0, 2 * season, config));
    }

    [Fact]
    public void AWinterBringsTheWornDayForwardAndARepairPutsItBack()
    {
        SimConfig config = Shipped;
        BuildingRow row = Timber(config);
        ulong year = (ulong)config.TicksPerYear;
        ulong wornAt = (ulong)row.WearsInYears * year;

        // Raised at New Year: by its eighth New Year it has lived eight winters' extra too, so it is worn already.
        Assert.Equal(Condition.Worn, ConditionRules.Of(0, 0, wornAt, row, config));

        // Mended a tick ago: sound, however old the timber.
        Assert.Equal(Condition.Sound, ConditionRules.Of(0, wornAt - 1, wornAt, row, config));
    }

    // ---------------------------------------------------------------
    //  §6 — the card's "about N years"
    // ---------------------------------------------------------------

    [Fact]
    public void TheYearsUntilAStateCountDownAndStopAtZero()
    {
        SimConfig config = Shipped;
        BuildingRow row = Timber(config);
        ulong year = (ulong)config.TicksPerYear;
        ulong aYear = year + ((ulong)config.TicksPerSeason * (ulong)config.WinterWearExtraPercent / 100);

        int? fresh = ConditionRules.YearsUntil(0, Condition.Worn, row, config);
        Assert.NotNull(fresh);
        Assert.InRange(fresh.Value, 1, row.WearsInYears);
        Assert.Equal(1, ConditionRules.YearsUntil(((ulong)row.FailsInYears * year) - 1, Condition.Failing, row, config));
        Assert.Equal(0, ConditionRules.YearsUntil((ulong)row.FailsInYears * year, Condition.Failing, row, config));
        Assert.Equal(2, ConditionRules.YearsUntil(((ulong)row.RuinedInYears * year) - aYear - 1, Condition.Ruin, row, config));
        Assert.Null(ConditionRules.YearsUntil(0, Condition.Sound, row, config));
    }

    /// <summary>The failing card counts in seasons: one tick short is one season, two seasons' wear short is two, and it stops at zero.</summary>
    [Fact]
    public void TheSeasonsUntilAStateCountDownAndStopAtZero()
    {
        SimConfig config = Shipped;
        BuildingRow row = Timber(config);
        ulong year = (ulong)config.TicksPerYear;
        ulong aSeason = (year + ((ulong)config.TicksPerSeason * (ulong)config.WinterWearExtraPercent / 100) + 3) / 4;
        ulong ruinAt = (ulong)row.RuinedInYears * year;

        Assert.Equal(1, ConditionRules.SeasonsUntil(ruinAt - 1, Condition.Ruin, row, config));
        Assert.Equal(2, ConditionRules.SeasonsUntil(ruinAt - (2 * aSeason), Condition.Ruin, row, config));
        Assert.Equal(0, ConditionRules.SeasonsUntil(ruinAt, Condition.Ruin, row, config));
        Assert.Null(ConditionRules.SeasonsUntil(0, Condition.Ruin, NeverWears, config));
    }

    /// <summary>
    /// The card's percentage runs toward fallen in (Joe, D556): 0 new, the worn and failing thresholds at their share of
    /// the ruin's years (40 % and 60 % at the shipped 8 / 12 / 20), 100 at a ruin and no further; none for stone.
    /// </summary>
    [Fact]
    public void ThePercentageRunsTowardFallenIn()
    {
        SimConfig config = Shipped;
        BuildingRow row = Timber(config);
        ulong year = (ulong)config.TicksPerYear;

        Assert.Equal(0, ConditionRules.PercentWorn(0, row, config));
        Assert.Equal(row.WearsInYears * 100 / row.RuinedInYears, ConditionRules.PercentWorn((ulong)row.WearsInYears * year, row, config));
        Assert.Equal(row.FailsInYears * 100 / row.RuinedInYears, ConditionRules.PercentWorn((ulong)row.FailsInYears * year, row, config));
        Assert.Equal(99, ConditionRules.PercentWorn(((ulong)row.RuinedInYears * year) - 1, row, config));
        Assert.Equal(100, ConditionRules.PercentWorn((ulong)row.RuinedInYears * year, row, config));
        Assert.Equal(100, ConditionRules.PercentWorn(1000UL * year, row, config));
        Assert.Null(ConditionRules.PercentWorn(1000UL * year, NeverWears, config));
    }

    // ---------------------------------------------------------------
    //  The rows and the keys
    // ---------------------------------------------------------------

    /// <summary>Every shipped timber row wears at the config's years; the stone bridge and the town hall never (D553 call 2); the free, instant rows never.</summary>
    [Fact]
    public void TheShippedRowsWearExceptTheOnesThatLast()
    {
        SimConfig config = Shipped;
        var lasting = new HashSet<BuildingKind> { BuildingKind.StoneBridge, BuildingKind.TownHall, BuildingKind.Pile, BuildingKind.BuilderHut };

        foreach (BuildingRow row in config.BuildingRows)
        {
            if (lasting.Contains((BuildingKind)row.Id))
            {
                Assert.Equal((0, 0, 0), (row.WearsInYears, row.FailsInYears, row.RuinedInYears));
            }
            else
            {
                Assert.Equal(
                    (config.BuildingWearsInYears, config.BuildingFailsInYears, config.BuildingRuinedInYears),
                    (row.WearsInYears, row.FailsInYears, row.RuinedInYears));
            }
        }

        Assert.Equal((8, 12, 20), (config.BuildingWearsInYears, config.BuildingFailsInYears, config.BuildingRuinedInYears));
        Assert.Equal(50, config.WinterWearExtraPercent);
        Assert.Equal(25, config.RepairCostPercent);
        Assert.Equal(150, config.FailingDeckSlowPercent);
    }

    [Fact]
    public void TheKeysMustRiseAndTheSharesStayInRange()
    {
        SimConfig config = Shipped;
        // ⚠️ The key is named, not just "rising": the rows the keys write would refuse the same village with a sentence about the row, and a guard that accepted either scored zero against the key check removed (D554).
        Assert.Contains("building_wears_in_years", Assert.Throws<SimConfigException>(() => (config with { BuildingFailsInYears = config.BuildingWearsInYears }).Validate()).Message, StringComparison.Ordinal);
        Assert.Contains("building_wears_in_years", Assert.Throws<SimConfigException>(() => (config with { BuildingWearsInYears = 0 }).Validate()).Message, StringComparison.Ordinal);
        Assert.Contains("repair_cost_percent", Assert.Throws<SimConfigException>(() => (config with { RepairCostPercent = 0 }).Validate()).Message, StringComparison.Ordinal);
        Assert.Contains("failing_deck_slow_percent", Assert.Throws<SimConfigException>(() => (config with { FailingDeckSlowPercent = 99 }).Validate()).Message, StringComparison.Ordinal);
        Assert.Contains("winter_wear_extra_percent", Assert.Throws<SimConfigException>(() => (config with { WinterWearExtraPercent = -1 }).Validate()).Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ARowWearsInThreeRisingYearsOrNever()
    {
        SimConfig config = Shipped;
        var rows = new List<BuildingRow>(config.BuildingRows);
        int granary = rows.FindIndex(r => r.Id == (int)BuildingKind.Granary);

        rows[granary] = rows[granary] with { FailsInYears = 0 };
        SimConfigException lone = Assert.Throws<SimConfigException>(() => (config with { Buildings = rows }).Validate());
        Assert.Contains("wears_in_years", lone.Message, StringComparison.Ordinal);
        Assert.Contains("granary", lone.Message, StringComparison.Ordinal);

        rows[granary] = rows[granary] with { WearsInYears = 12, FailsInYears = 12, RuinedInYears = 20 };
        Assert.Throws<SimConfigException>(() => (config with { Buildings = rows }).Validate());

        rows[granary] = rows[granary] with { WearsInYears = 0, FailsInYears = 0, RuinedInYears = 0 };
        (config with { Buildings = rows }).Validate();
    }
}

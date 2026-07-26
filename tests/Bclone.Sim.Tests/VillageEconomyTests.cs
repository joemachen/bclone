using Bclone.Sim.Config;
using Bclone.Sim.World;
using Xunit;
using Xunit.Abstractions;

namespace Bclone.Sim.Tests;

/// <summary>
/// The economy's stated target, asserted rather than tuned toward.
/// </summary>
/// <remarks>
/// Six values were changed across two sittings chasing a boom-bust, each guessed
/// from a symptom. Tuning by iteration cannot say <em>why</em> a number works, so it
/// cannot say when a later change breaks it. These tests make the target a contract:
/// change hunger, travel, or vigour in a way that breaks it and the build fails
/// rather than the village.
/// </remarks>
public sealed class VillageEconomyTests
{
    private readonly ITestOutputHelper _output;

    public VillageEconomyTests(ITestOutputHelper output) => _output = output;

    private static SimConfig Config => VillageFixtures.Village;

    [Fact]
    public void TheStatedTargetIsMet()
    {
        // THE contract: one adult at minimum vigour, with no partner, must feed
        // themselves and two children. That is the widowed-parent case, which the
        // diagnostic run showed was killing nearly every household.
        SimConfig config = Config;
        _output.WriteLine(VillageEconomy.Describe(config));

        Assert.True(
            VillageEconomy.DependantsSupportedAtWorst(config) >= VillageEconomy.RequiredDependants,
            $"A weakest-case adult supports only " +
            $"{VillageEconomy.DependantsSupportedAtWorst(config)} dependants; " +
            $"target is {VillageEconomy.RequiredDependants}.");
    }

    [Fact]
    public void TheConfiguredYieldIsTheDerivedOne()
    {
        // gather_yield is a CONSEQUENCE of the target, not an opinion. If this
        // fails, either the target moved or something upstream of it did.
        SimConfig config = Config;

        Assert.True(
            config.GatherYield >= VillageEconomy.RequiredGatherYield(config),
            $"gather_yield is {config.GatherYield} but the target requires at least " +
            $"{VillageEconomy.RequiredGatherYield(config)}.");
    }

    [Fact]
    public void TheDerivedYieldIsNotWildlyOverShot()
    {
        // Guards the other direction: a yield far above the requirement means the
        // village is trivially rich and winter stops meaning anything.
        SimConfig config = Config;
        int required = VillageEconomy.RequiredGatherYield(config);

        Assert.True(config.GatherYield <= required * 3 / 2,
            $"gather_yield {config.GatherYield} is more than 1.5x the required {required} — " +
            "winter will not bite.");
    }

    [Fact]
    public void AHealthyAdultSupportsMoreThanAFrailOne()
    {
        // Sanity check on the vigour model: the whole ageing arc depends on this
        // being true, and it is easy to invert a percentage by accident.
        SimConfig config = Config;

        Assert.True(
            VillageEconomy.FoodGatheredPerYear(config, 100)
            > VillageEconomy.FoodGatheredPerYearAtWorst(config));
    }

    [Fact]
    public void AChildCostsLessThanAnAdult()
    {
        SimConfig config = Config;
        Assert.True(VillageEconomy.ChildFoodPerYear(config) < VillageEconomy.AdultFoodPerYear(config));
    }

    [Fact]
    public void WinterStoreCoversWinterWithAMargin()
    {
        // Surviving winter exactly is not surviving winter — the shock that kills a
        // household is a worker dying part-way through one.
        SimConfig config = Config;

        int winterNeed = VillageEconomy.AdultFoodPerYear(config) / 4;
        int stored = VillageEconomy.RequiredStockpilePerAdult(config);

        _output.WriteLine($"winter need {winterNeed}/adult, store target {stored}/adult");
        Assert.True(stored > winterNeed, $"Store {stored} does not even cover winter need {winterNeed}.");
    }

    [Fact]
    public void TheConfiguredStockpileTargetMatchesTheDerivedOne()
    {
        SimConfig config = Config;

        Assert.True(
            config.StockpileTarget >= VillageEconomy.RequiredStockpilePerAdult(config),
            $"stockpile_target is {config.StockpileTarget} but winter needs at least " +
            $"{VillageEconomy.RequiredStockpilePerAdult(config)} per member.");
    }

    [Fact]
    public void ForagingIsPossibleAtAll()
    {
        // A config where the food source is unreachable within a year would make
        // every other number meaningless.
        Assert.True(VillageEconomy.TripsPerYear(Config) > 0);
    }

    [Fact]
    public void AnImpossibleConfigFailsLoudly()
    {
        // travel so slow that no round trip fits in a gatherable year.
        SimConfig broken = Config with { TravelTicksPerUnit = 10_000 };

        Assert.Equal(0, VillageEconomy.TripsPerYear(broken));
        Assert.Throws<InvalidOperationException>(() => VillageEconomy.RequiredGatherYield(broken));
    }

    [Fact]
    public void TheShippedConfigFileMeetsTheTarget()
    {
        // The fixtures deriving correctly is not much use if the file the game
        // actually loads has drifted. This is the one that guards the real village.
        string path = Path.Combine(RepoRoot(), "data", "sim.config.json");
        SimConfig shipped = SimConfigLoader.LoadFromFile(path);

        _output.WriteLine(VillageEconomy.Describe(shipped));

        Assert.True(
            VillageEconomy.DependantsSupportedAtWorst(shipped) >= VillageEconomy.RequiredDependants,
            $"data/sim.config.json supports only " +
            $"{VillageEconomy.DependantsSupportedAtWorst(shipped)} dependants; " +
            $"gather_yield should be at least {VillageEconomy.RequiredGatherYield(shipped)}.");

        Assert.True(
            shipped.StockpileTarget >= VillageEconomy.RequiredStockpilePerAdult(shipped),
            $"stockpile_target is {shipped.StockpileTarget}, needs " +
            $"{VillageEconomy.RequiredStockpilePerAdult(shipped)}.");
    }

    private static string RepoRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "DESIGN.md")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new InvalidOperationException("Could not locate repo root.");
    }

    [Fact]
    public void Phase0SVillageOfOneAlsoMeetsTheTarget()
    {
        // Phase 0 has no children, so its economy only has to feed one adult — but
        // it should not be accidentally impossible either.
        SimConfig config = Phase0Fixtures.Plenty;
        _output.WriteLine(VillageEconomy.Describe(config));

        Assert.True(VillageEconomy.FoodGatheredPerYearAtWorst(config) > VillageEconomy.AdultFoodPerYear(config),
            "A Phase 0 villager cannot feed themselves at minimum vigour.");
    }
}

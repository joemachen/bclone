using Bclone.Sim.Config;
using Bclone.Sim.Core;
using Bclone.Sim.Logging;
using Bclone.Sim.World;
using Xunit;
using Xunit.Abstractions;

namespace Bclone.Sim.Tests;

/// <summary>
/// The woodcutter leaves the logs a building waits on — <c>specs/armful-and-meals.md §7a</c> (D538, Joe: *"fix it"*).
/// </summary>
/// <remarks>
/// <para>
/// Traced on shipped seed 2: with the player's firewood limit at 400 a woodcutter split every log the stores held,
/// whatever the village needed for winter, and the founding's two marked houses waited at four logs into a winter
/// that killed all four founders. 15 → 22 lost foundings of 100 once the armful was 80.
/// </para>
/// <para>
/// The rule: above the village's own winter need, split only <b>spare</b> logs — beyond what the sites still need.
/// Below it, fuel first, as before.
/// </para>
/// </remarks>
public sealed class LogsForBuildingTests
{
    private readonly ITestOutputHelper _output;

    public LogsForBuildingTests(ITestOutputHelper output) => _output = output;

    private static SimLoop Build(SimConfig config) =>
        SimFactory.CreatePhase0(config, new InMemoryLogSink());

    /// <summary>The rule's three states: held back, spare logs, and fuel first.</summary>
    [Fact]
    public void AWoodcutterSplitsOnlySpareLogsOnceTheWinterNeedIsMet()
    {
        SimWorld world = Build(VillageFixtures.Village).World;
        StoreBuilding yard = PoseAWarehouseAndASiteWaitingOnLogs(world, out int waiting);
        int batch = world.Config.LogsPerSplit;

        Assert.True(world.TheVillageWantsMoreFirewood(), "the player's limit is met already — the pose cannot ask the question");
        Assert.Equal(0, LabourQuota.FirewoodShortfall(world));

        // Every log in store is wanted for building.
        SetLogs(yard, waiting);
        _output.WriteLine($"sites still need {waiting} logs; the stores hold {world.LogsInWarehouses()}");
        Assert.False(world.MaySplitLogs(), "the woodcutter would split logs a site is waiting on");

        // A batch to spare.
        SetLogs(yard, waiting + batch);
        Assert.True(world.MaySplitLogs(), "a batch beyond what the sites need is the woodcutter's");

        // ⭐ Fuel first: under the winter's need the woodcutter splits whatever a site is waiting on.
        SetLogs(yard, waiting);
        yard.Store.TakeAll(Goods.Firewood);
        foreach (Household household in world.Households)
        {
            household.Stockpile.TakeAll(Goods.Firewood);
        }

        Assert.True(LabourQuota.FirewoodShortfall(world) > 0);
        Assert.True(world.MaySplitLogs(), "a village short of its winter's firewood is cold before it is roofless");
    }

    /// <summary>
    /// ⭐ A staffed woodcutter in a village with its winter's firewood never splits the stores below what a site
    /// waits on — against the same village with nothing waiting, where they split the stores bare.
    /// </summary>
    /// <remarks>
    /// ⚠️ <b>Not "splits nothing".</b> The first draft asked that and the rule failed it honestly: the fixture's
    /// other stores held logs beyond the site's 40, and the woodcutter split exactly those (12) and left 43. The
    /// claim is what is left, not what was done.
    /// </remarks>
    [Fact]
    public void AWoodcutterLeavesTheLogsABuildingWaitsOn()
    {
        (int withASite, int lowestSpare) = SplitsInASeason(markASite: true, out int waiting);
        (int without, _) = SplitsInASeason(markASite: false, out _);

        _output.WriteLine(
            $"logs split in a season: {withASite} with a site waiting on {waiting} (the stores never fell more than "
            + $"{-Math.Min(0, lowestSpare)} under it), {without} with none");
        Assert.True(without >= 2 * VillageFixtures.Village.LogsPerSplit,
            $"only {without} logs split with nothing waiting — the woodcutter never worked, so the guard proves nothing");
        Assert.True(lowestSpare >= 0, $"the stores fell to {-lowestSpare} logs under what the site waits on");
    }

    private (int Split, int LowestSpare) SplitsInASeason(bool markASite, out int waiting)
    {
        SimConfig config = VillageFixtures.Village;
        SimLoop loop = Build(config);
        SimWorld world = loop.World;
        StoreBuilding yard = PoseAWarehouseAndASiteWaitingOnLogs(world, out waiting, markASite);
        SetLogs(yard, Math.Max(waiting, 4 * config.LogsPerSplit));

        // One woodcutter asked for, nobody to carry logs to a site or to fell more.
        world.SetJobLimit(JobKind.Woodcutter, 1);
        world.SetJobLimit(JobKind.Builder, 0);
        world.SetJobLimit(JobKind.Forester, 0);

        int before = world.LogsEverSplit;
        int lowestSpare = int.MaxValue;
        for (int tick = 0; tick < config.TicksPerSeason; tick++)
        {
            KeepTheWinterNeedMet(world, yard);
            loop.StepOnce();
            lowestSpare = Math.Min(lowestSpare, world.LogsInWarehouses() - world.LogsTheSitesStillNeed());
        }

        int split = world.LogsEverSplit - before;
        _output.WriteLine($"{(markASite ? "a site" : "no site")}: {split} split; {world.LogsInWarehouses()} logs left, {world.FirewoodInWarehouses()} firewood");
        return (split, lowestSpare);
    }

    private static StoreBuilding PoseAWarehouseAndASiteWaitingOnLogs(SimWorld world, out int waiting, bool markASite = true)
    {
        world.SetStockLimit(Goods.Firewood, 400);
        StoreBuilding yard = world.AnyStoreOf(StoreKind.Warehouse);
        KeepTheWinterNeedMet(world, yard);

        if (markASite)
        {
            ColdStartTests.MarkSomewhereNear(world, BuildingKind.Granary, world.Map.FoundingSite, 8);
        }

        waiting = world.LogsTheSitesStillNeed();
        if (markASite)
        {
            Assert.True(waiting > world.Config.LogsPerSplit, $"the marked site waits on only {waiting} logs");
        }

        return yard;
    }

    /// <summary>Firewood in the warehouse up to the derived need, never past it — so the limit (400) still wants more.</summary>
    private static void KeepTheWinterNeedMet(SimWorld world, StoreBuilding yard)
    {
        int shortfall = LabourQuota.FirewoodShortfall(world);
        if (shortfall > 0)
        {
            yard.Store.Add(Goods.Firewood, shortfall);
        }
    }

    private static void SetLogs(StoreBuilding yard, int logs)
    {
        yard.Store.TakeAll(Goods.Logs);
        yard.Store.Add(Goods.Logs, logs);
    }
}

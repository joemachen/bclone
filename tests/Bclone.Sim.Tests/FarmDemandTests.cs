using Bclone.Sim.Config;
using Bclone.Sim.Core;
using Bclone.Sim.Logging;
using Bclone.Sim.World;
using Xunit;
using Xunit.Abstractions;

namespace Bclone.Sim.Tests;

/// <summary>
/// ⛔⛔ The seasonal demand for farmers — <c>specs/crops-and-orchards.md §11b</c>, and the one
/// part of the farm that had to be proved before anything could be sown.
/// </summary>
/// <remarks>
/// <para>
/// <b><see cref="Workplace.StaffingOverride"/> is a ceiling, not a summons (D146).</b> If
/// <see cref="LabourQuota"/> does not <em>actively want</em> farmers when the fields are
/// standing, the harvest is never reaped, winter takes it — and every guard written for the
/// crop blames <c>CropSystem</c>, which will be working perfectly. That is D146's own bug
/// waiting one job over, and D146 cost a measurement to find: *most hands ever at the hut 0 of
/// 2, quota wants 0 foresters*, with the behaviour branch flawless the whole time.
/// </para>
/// <para>
/// <b>So this file exists before sowing does.</b> Nothing here reaps anything; the states are
/// posed directly (D146's other lesson — a test that waits for the village to happen to do
/// something is at the mercy of what it wants that season), and what is asserted is only that
/// the village <em>asks for hands</em> at the right times.
/// </para>
/// </remarks>
public sealed class FarmDemandTests
{
    private readonly ITestOutputHelper _output;

    public FarmDemandTests(ITestOutputHelper output) => _output = output;

    private static SimConfig Config => VillageFixtures.Village;

    private static SimLoop Loop(SimConfig config) =>
        SimFactory.CreatePhase0(config, new InMemoryLogSink());

    // ---------------------------------------------------------------
    //  ⭐ The arm with teeth
    // ---------------------------------------------------------------

    /// <summary>
    /// ⭐⭐ Bare ground in spring, a standing crop through summer and autumn, nothing in winter.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>⚠️ SUMMER IS WANTED, AND THAT IS A CORRECTION TO THE OBVIOUS READING OF THE
    /// CALENDAR.</b> `crops-and-orchards.md §5` reads *spring sows, summer tends, autumn reaps,
    /// winter nothing*, and turning that straight into *no farmers wanted in summer* is a trap
    /// with a mechanism behind it: <c>LabourSystem</c> reshuffles the whole village every three
    /// years, and <c>TakeUpSlack</c> fills openings only from villagers who are <em>idle</em>.
    /// A reshuffle landing in July would therefore empty the farm, and autumn would find
    /// nobody free to put back into it — the harvest rotting for a scheduling reason nobody
    /// could see. <b>The standing crop is why the hands are wanted</b>, which is the truer
    /// sentence: somebody has to be there in September, and the village settles that in June.
    /// </para>
    /// <para>
    /// <b>Winter really is zero</b>, and it costs nothing — the forager quota is zero in winter
    /// too (D44), so spring opens on a village full of idle hands and the scarce kinds are
    /// matched first.
    /// </para>
    /// </remarks>
    [Fact]
    public void TheVillageWantsFarmersWhileTheYearHasFieldWorkInIt()
    {
        SimLoop loop = Loop(Config);
        SimWorld world = loop.World;
        Workplace farm = FarmFixtures.RaiseAFarm(world);
        int tiles = FarmFixtures.GiveItGround(world, farm, reach: 2);
        Assert.True(tiles > 0, "The farm was given no ground to work.");

        // Spring, bare ground: the year is waiting to be committed.
        FarmFixtures.StepToTheStartOf(loop, Season.Spring);
        int inSpring = LabourQuota.For(world).Farmers;

        // Summer, with the crop in the ground: the hands are held for the harvest.
        FarmFixtures.SowEveryTileOf(world, farm);
        FarmFixtures.StepToTheStartOf(loop, Season.Summer);
        int inSummer = LabourQuota.For(world).Farmers;

        // Autumn, standing ripe: the season the whole thing is for.
        FarmFixtures.StepToTheStartOf(loop, Season.Fall);
        int inAutumn = LabourQuota.For(world).Farmers;

        // Winter, stubble: nothing to do, and the hands go back to the village.
        FarmFixtures.StepToTheStartOf(loop, Season.Winter);
        int inWinter = LabourQuota.For(world).Farmers;

        _output.WriteLine(
            $"farmers wanted — spring {inSpring}, summer {inSummer}, autumn {inAutumn}, "
            + $"winter {inWinter} (of {farm.Places} seats on {tiles} tiles)");

        Assert.True(inSpring > 0, "Nobody wanted to sow in spring — the year is never committed.");
        Assert.True(inSummer > 0, "The farm was emptied in summer, so autumn may find nobody.");
        Assert.True(inAutumn > 0, "Nobody wanted to reap — the harvest stands and winter takes it.");
        Assert.Equal(0, inWinter);
    }

    /// <summary>
    /// The anti-vacuity companion (D7): a farm with no ground is a building, not a demand.
    /// </summary>
    /// <remarks>
    /// Without this, a quota arm that simply returned "every seat at every farmhouse, always"
    /// would pass the guard above and would be wrong in all four seasons. It is also the
    /// no-op contract the goldens depend on: a village that has painted nothing spends nothing.
    /// </remarks>
    [Fact]
    public void AFarmWithNoGroundIsWantedInNoSeasonAtAll()
    {
        SimLoop loop = Loop(Config);
        SimWorld world = loop.World;
        Workplace farm = FarmFixtures.RaiseAFarm(world);

        foreach (Season season in new[] { Season.Spring, Season.Summer, Season.Fall, Season.Winter })
        {
            FarmFixtures.StepToTheStartOf(loop, season);
            int wanted = LabourQuota.For(world).Farmers;
            _output.WriteLine($"{season}: {wanted} farmers wanted at {farm.Name} with no ground");
            Assert.Equal(0, wanted);
        }
    }

    /// <summary>And a village with no farmhouse at all never asks for one.</summary>
    [Fact]
    public void AVillageWithNoFarmWantsNoFarmers()
    {
        SimLoop loop = Loop(Config);

        for (int i = 0; i < Config.TicksPerYear; i++)
        {
            loop.StepOnce();
            Assert.Equal(0, LabourQuota.For(loop.World).Farmers);
        }
    }

    // ---------------------------------------------------------------
    //  The quota reaches the allocator — a demand nobody staffs is not a demand
    // ---------------------------------------------------------------

    /// <summary>
    /// ⭐ Somebody is actually standing in the farm when the crop is ripe.
    /// </summary>
    /// <remarks>
    /// <b>The demand and the staffing are two different claims, and D146 is the record of
    /// believing the first proves the second.</b> The quota can want three farmers and the
    /// allocator can put nobody in the building — that is exactly what happened to the capped
    /// forester's hut, where the behaviour branch was correct and the building was empty. So
    /// this asserts the thing that actually matters: at the moment the fields are standing
    /// ripe, a villager holds the farm as their job.
    /// </remarks>
    [Fact]
    public void SomebodyIsWorkingTheFarmWhenTheCropStandsRipe()
    {
        SimLoop loop = Loop(Config);
        SimWorld world = loop.World;
        Workplace farm = FarmFixtures.RaiseAFarm(world);
        FarmFixtures.GiveItGround(world, farm, reach: 2);
        FarmFixtures.SowEveryTileOf(world, farm);

        FarmFixtures.StepToTheStartOf(loop, Season.Fall);

        _output.WriteLine(
            $"{world.Clock.SeasonAndYear()}: {farm.WorkerIds.Count} of {farm.Places} at "
            + $"{farm.Name}; the village wants {LabourQuota.For(world).Farmers}");

        Assert.True(
            farm.WorkerIds.Count > 0,
            "The fields are ripe and nobody holds the farm — SetStaffing is a ceiling, not a "
            + "summons (D146), and this is that bug one job over.");
    }

    // ---------------------------------------------------------------
    //  The player's food limit reaches the farm — and reaches only half of it
    // ---------------------------------------------------------------

    /// <summary>
    /// ⭐ A met food limit stops the sowing and <b>never</b> the reaping.
    /// </summary>
    /// <remarks>
    /// <para>
    /// D145's sweep asks one question of every control — <em>does the player's state reach the
    /// code that does the work?</em> — and the answer for a farm has to be *yes, and only as
    /// far as it should go*. A limit says how much to keep; leaving a standing harvest to rot
    /// because the granary happens to be full would spend a year of the player's work in order
    /// to obey them, and `crops-and-orchards.md §5.1` means use-it-or-lose-it to punish
    /// inattention rather than obedience.
    /// </para>
    /// <para>
    /// Both arms in one test on purpose: they are the same predicate
    /// (<see cref="SimWorld.MaySow"/>) read at two seasons, and a guard for either one alone
    /// would pass against a rule that stopped everything or stopped nothing.
    /// </para>
    /// <para>
    /// <b>⚠️ The limit is set at the moment it is read, never a year ahead</b>, and the first
    /// draft got that wrong: capping the food at zero and then living through three seasons
    /// starves the whole village, so the autumn arm read <c>hands = 0</c> and reported a broken
    /// crop rule. That is D157's lesson in miniature — an arm that dies for an unrelated reason
    /// has said nothing about the thing it was pointed at.
    /// </para>
    /// </remarks>
    [Fact]
    public void AMetFoodLimitStopsTheSowingAndNotTheReaping()
    {
        SimLoop loop = Loop(Config);
        SimWorld world = loop.World;
        Workplace farm = FarmFixtures.RaiseAFarm(world);
        FarmFixtures.GiveItGround(world, farm, reach: 2);

        // The control arm first (D145's own lesson — the food limit's guard was nearly
        // vacuous because the granary's capacity was doing the stopping).
        FarmFixtures.StepToTheStartOf(loop, Season.Spring);
        int uncapped = LabourQuota.For(world).Farmers;
        Assert.True(uncapped > 0, "Nobody wanted to sow even before the limit was set.");

        world.SetStockLimit(Goods.Food, 0);
        int capped = LabourQuota.For(world).Farmers;
        _output.WriteLine($"spring: {uncapped} wanted uncapped, {capped} at a food limit of 0");
        Assert.Equal(0, capped);

        // And the harvest still comes in — the limit set at the moment it is read.
        world.SetStockLimit(Goods.Food, null);
        FarmFixtures.SowEveryTileOf(world, farm);
        FarmFixtures.StepToTheStartOf(loop, Season.Fall);

        int beforeTheCap = LabourQuota.For(world).Farmers;
        world.SetStockLimit(Goods.Food, 0);
        int reaping = LabourQuota.For(world).Farmers;

        _output.WriteLine(
            $"autumn: {beforeTheCap} wanted uncapped, {reaping} at a food limit of 0, "
            + $"{world.StandingCropTiles(farm)} tiles standing");

        Assert.True(beforeTheCap > 0, "Nobody wanted to reap even before the limit was set.");
        Assert.True(
            reaping > 0,
            "A met food limit left a standing harvest to rot — the cap belongs on the sowing.");
    }
}

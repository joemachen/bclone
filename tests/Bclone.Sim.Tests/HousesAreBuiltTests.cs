using Bclone.Sim.Config;
using Bclone.Sim.Core;
using Bclone.Sim.Logging;
using Bclone.Sim.World;
using Xunit;
using Xunit.Abstractions;

namespace Bclone.Sim.Tests;

/// <summary>
/// A house is a construction site like everything else — Joe, D102.
/// </summary>
/// <remarks>
/// <para>
/// <b>The inconsistency <c>specs/cold-start.md §7.1b</c> has carried since Joe's second run:</b>
/// <em>"they built homes (immediate builds btw, not a visual timed thing like other
/// buildings)."</em> A house took its timber straight out of the stores and set
/// <c>HomePosition</c> in one tick, where a granary is marked, hauled to and worked on.
/// </para>
/// <para>
/// <b>It hid what a house costs, and it meant houses never competed for builders</b> — which
/// is exactly the distortion that made winter 1 look winnable when it was not.
/// </para>
/// </remarks>
public sealed class HousesAreBuiltTests
{
    private readonly ITestOutputHelper _output;

    public HousesAreBuiltTests(ITestOutputHelper output) => _output = output;

    private static SimLoop Loop(SimConfig config) =>
        SimFactory.CreatePhase0(config, new InMemoryLogSink());

    private static int HomeSites(SimWorld world)
    {
        int count = 0;
        for (int i = 0; i < world.Workplaces.Count; i++)
        {
            if (world.Workplaces[i].Construction?.Kind == BuildingKind.Home)
            {
                count++;
            }
        }

        return count;
    }

    private static int Homes(SimWorld world)
    {
        int homes = 0;
        for (int i = 0; i < world.Households.Count; i++)
        {
            if (world.Households[i].HasHome)
            {
                homes++;
            }
        }

        return homes;
    }

    private static void PaintHomeGround(SimWorld world)
    {
        GridPos site = world.Map.FoundingSite;
        for (int dy = -4; dy <= 4; dy++)
        {
            for (int dx = -4; dx <= 4; dx++)
            {
                world.PaintResidential(new GridPos(site.X + dx, site.Y + dy));
            }
        }
    }

    /// <summary>⭐ A roofless family gets a building site, not a house out of thin air.</summary>
    [Fact]
    public void AHouseIsMarkedOutRatherThanConjured()
    {
        SimConfig config = ShippedConfig.Load();
        SimLoop loop = Loop(config);
        SimWorld world = loop.World;

        PaintHomeGround(world);

        int logsBefore = world.TotalLogs();
        loop.Step(5);

        _output.WriteLine(
            $"five ticks after painting: {HomeSites(world)} house sites, {Homes(world)} homes, "
            + $"logs {logsBefore} -> {world.TotalLogs()}");

        Assert.True(HomeSites(world) > 0, "No house was ever marked out for the founders.");
        Assert.Equal(0, Homes(world));

        // ⭐ AND NO TIMBER WAS TAKEN. `TryTakeBuildingTimber` drew a house's logs straight
        // out of the stores in the same tick; a builder hauls them to the site now, which is
        // D43's rule about construction not being a purchase, applied at last to the building
        // the village raises most often.
        Assert.Equal(logsBefore, world.TotalLogs());
    }

    /// <summary>A house costs work as well as timber, and the recipe says so.</summary>
    [Fact]
    public void AHouseOwesWorkAndNotOnlyTimber()
    {
        SimConfig config = ShippedConfig.Load();
        BuildingRecipe recipe = BuildingRecipe.For(BuildingKind.Home, config);

        Assert.Equal(config.LogsPerHouse, recipe.Logs);
        Assert.Equal(config.HomeWorkTicks, recipe.WorkTicks);
        Assert.True(recipe.WorkTicks > 0, "A house that owes no work is an instant house again.");
    }

    /// <summary>And the village still ends up housed — the site is a delay, not a wall.</summary>
    [Fact]
    public void TheVillageStillHousesItself()
    {
        SimConfig config = VillageFixtures.Village;
        SimLoop loop = Loop(config);
        SimWorld world = loop.World;

        loop.Step(config.TicksPerYear * 40);

        _output.WriteLine(
            $"forty years: {world.Population} alive in {world.Households.Count} households, "
            + $"{Homes(world)} of them housed, {HomeSites(world)} still being raised");

        Assert.True(world.Households.Count > 2, "The village never formed a new household.");
        Assert.True(Homes(world) > 2, "The village never finished a house it started.");
    }

    /// <summary>
    /// ⭐ What the player marked is built before a house the village marked for itself.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Measured, and the founding died without it.</b> <c>HouseTheRoofless</c> marks a
    /// house for every roofless family on tick 4, so two house sites went in front of the
    /// woodcutter's hut the player marked at tick 0. The hut's timber then arrived at
    /// <b>t364</b> against a winter starting at t360, and the shipped guard read <b>2 alive,
    /// 2 frozen</b>. With the rank, the hut is back to logs t129, standing t172, first
    /// firewood t249 — exactly where it was before houses were built.
    /// </para>
    /// <para>
    /// <b>The cost was never the work.</b> With <c>home_work_ticks</c> set to zero the
    /// timeline did not move by one tick: the bottleneck is the timber a builder hauls, and
    /// two houses' worth of it went first.
    /// </para>
    /// <para>
    /// <b>A priority, not an exclusion</b> — see <see cref="TheVillageStillHousesItself"/>.
    /// </para>
    /// </remarks>
    [Fact]
    public void TheHutThePlayerMarkedIsBuiltBeforeTheHousesTheVillageWants()
    {
        SimConfig config = ShippedConfig.Load();
        SimLoop loop = Loop(config);
        SimWorld world = loop.World;

        PaintHomeGround(world);
        MarkSomewhereNear(world, BuildingKind.Pile, world.Map.FoundingSite, 2);
        MarkSomewhereNear(world, BuildingKind.WoodcutterHut, world.Map.FoundingSite, 3);

        long hutStood = -1;
        long firstHouse = -1;

        for (long tick = 0; tick < config.TicksPerYear; tick++)
        {
            loop.StepOnce();

            if (hutStood < 0 && AnyWoodcutterHut(world))
            {
                hutStood = tick;
            }

            if (firstHouse < 0 && Homes(world) > 0)
            {
                firstHouse = tick;
            }
        }

        _output.WriteLine(
            $"the hut stood at t{hutStood}; the first house at t{firstHouse} "
            + $"(winter starts at t360)");

        Assert.True(hutStood >= 0, "The hut the player marked was never built.");
        Assert.True(
            hutStood < 360,
            $"The hut the player marked was not standing until t{hutStood}, after winter began.");
    }

    private static bool AnyWoodcutterHut(SimWorld world)
    {
        for (int i = 0; i < world.Workplaces.Count; i++)
        {
            if (world.Workplaces[i].Kind == JobKind.Woodcutter)
            {
                return true;
            }
        }

        return false;
    }

    private static void MarkSomewhereNear(
        SimWorld world, BuildingKind kind, GridPos site, int radius)
    {
        for (int dy = -radius; dy <= radius; dy++)
        {
            for (int dx = -radius; dx <= radius; dx++)
            {
                if (world.Mark(kind, new GridPos(site.X + dx, site.Y + dy)).Allowed)
                {
                    return;
                }
            }
        }
    }
}

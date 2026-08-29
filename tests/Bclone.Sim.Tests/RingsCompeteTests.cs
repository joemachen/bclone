using Bclone.Sim.Config;
using Bclone.Sim.Core;
using Bclone.Sim.Determinism;
using Bclone.Sim.Logging;
using Bclone.Sim.World;
using Xunit;
using Xunit.Abstractions;

namespace Bclone.Sim.Tests;

/// <summary>
/// ⭐ Two huts that reach the same trees share them — <b>rings compete</b> (D260, Joe).
/// </summary>
/// <remarks>
/// <para>
/// <b>⭐⭐ Joe, 2026-08-29:</b> *"a 2nd hut should 'compete' with the first hut for gathered food
/// resources if they are too close."* ⛔ **Before this, it did not.** Two huts on one copse each
/// counted every tree, so a second hut doubled the village's food for the price of some timber and
/// **where you put it did not matter at all.** *"Build another hut" was the answer to every food
/// problem, and placement was not a decision.*
/// </para>
/// <para>
/// <b>⛔ THIS IS THE HALF THAT MAKES A SEAT CAP MEAN ANYTHING.</b> Capping the seats without this
/// is a tax — you build a second hut on the same wood and carry on. With it, feeding more people
/// means **finding more forest**, which is a placement decision the player can see on the map.
/// </para>
/// </remarks>
public sealed class RingsCompeteTests
{
    private readonly ITestOutputHelper _output;

    public RingsCompeteTests(ITestOutputHelper output) => _output = output;

    private static SimConfig Config => VillageFixtures.Village;

    private static SimLoop Loop(SimConfig config) =>
        SimFactory.CreatePhase0(config, new InMemoryLogSink());

    /// <summary>
    /// Take away every gathering place the founding gave us, so "alone" means alone.
    /// </summary>
    /// <remarks>
    /// <b>⚠️ THE FIRST VERSION OF THESE GUARDS DID NOT, and reported a lone hut holding 55 of the
    /// 92 trees it could reach.</b> The founding already stands a forager's hut, and it overlapped
    /// the one the test planted — *so "alone" was two huts sharing, and the arithmetic under test
    /// was doing its job correctly on a fixture that meant something else.*
    /// </remarks>
    private static void ClearTheValleyOfHuts(SimWorld world)
    {
        for (int i = world.Workplaces.Count - 1; i >= 0; i--)
        {
            if (world.Workplaces[i].GatheringRadius > 0)
            {
                world.Workplaces.RemoveAt(i);
            }
        }
    }

    /// <summary>Put a gathering hut down outright, wherever we say.</summary>
    private static Workplace PlantAHut(SimWorld world, GridPos at)
    {
        var hut = new Workplace
        {
            Store = new Stockpile(world.GoodsCatalog.Count),
            Id = 9000 + world.Workplaces.Count,
            Kind = JobKind.Forager,
            Name = $"hut at {at}",
            Position = at,
            Capacity = 2,
            GatheringRadius = world.Config.GathererHutRingTiles,
        };

        world.Workplaces.Add(hut);
        return hut;
    }

    /// <summary>The most wooded tile we can find, so the arithmetic has something to divide.</summary>
    private static GridPos WoodiestNear(SimWorld world, GridPos around, int reach)
    {
        GridPos best = around;
        int most = -1;

        for (int dy = -reach; dy <= reach; dy++)
        {
            for (int dx = -reach; dx <= reach; dx++)
            {
                var at = new GridPos(around.X + dx, around.Y + dy);
                if (!world.Map.Contains(at))
                {
                    continue;
                }

                var probe = new Workplace
                {
                    Store = new Stockpile(world.GoodsCatalog.Count),
                    Id = 1,
                    Kind = JobKind.Forager,
                    Name = "probe",
                    Position = at,
                    Capacity = 1,
                    GatheringRadius = world.Config.GathererHutRingTiles,
                };

                int wooded = world.WoodedTilesAround(probe);
                if (wooded > most)
                {
                    most = wooded;
                    best = at;
                }
            }
        }

        return best;
    }

    // -----------------------------------------------------------------

    /// <summary>⛔ A hut standing alone is worth exactly what it always was.</summary>
    /// <remarks>
    /// <b>The no-op half, and it is what let this ship without moving a single golden.</b> Nothing
    /// in the suite crowds two rings together, so *a village with one hut is the village that came
    /// before.* ⚠️ If this ever goes red, competition has started charging huts for their own trees.
    /// </remarks>
    [Fact]
    public void OneHutAloneIsWorthEveryTreeItReaches()
    {
        SimWorld world = Loop(Config).World;
        ClearTheValleyOfHuts(world);
        GridPos woods = WoodiestNear(world, world.Map.FoundingSite, 6);
        Workplace only = PlantAHut(world, woods);

        int raw = world.WoodedTilesAround(only);
        int share = world.WoodedShareAround(only);

        _output.WriteLine($"alone: {raw} wooded tiles, share {share} = {share / SimWorld.ShareScale} tiles");

        Assert.True(raw > 0, "The fixture found no wood, so this proves nothing.");
        Assert.Equal(raw * SimWorld.ShareScale, share);
    }

    /// <summary>⭐ Two huts on the same trees split them, and the pair is worth what one was.</summary>
    /// <remarks>
    /// <b>⭐⭐ THE SHARPEST FORM OF THE CLAIM: stacked exactly on top of each other, the two huts
    /// together are worth ONE hut.</b> Not "less" — *exactly one*, because a tree is one tree. That
    /// is a stronger statement than an inequality and it is the one that says the arithmetic is a
    /// split rather than a penalty.
    /// </remarks>
    [Fact]
    public void TwoHutsOnOneCopseAreWorthOneHutBetweenThem()
    {
        SimWorld world = Loop(Config).World;
        ClearTheValleyOfHuts(world);
        GridPos woods = WoodiestNear(world, world.Map.FoundingSite, 6);

        Workplace first = PlantAHut(world, woods);
        int alone = world.WoodedShareAround(first);

        Workplace second = PlantAHut(world, woods);

        int firstNow = world.WoodedShareAround(first);
        int secondNow = world.WoodedShareAround(second);

        _output.WriteLine($"alone {alone}; crowded {firstNow} + {secondNow} = {firstNow + secondNow}");

        Assert.True(alone > 0, "The fixture found no wood, so this proves nothing.");
        Assert.Equal(alone / 2, firstNow);
        Assert.Equal(alone, firstNow + secondNow);
    }

    /// <summary>⭐ Far enough apart, two huts cost each other nothing.</summary>
    /// <remarks>
    /// <b>The other side of the decision, and without it this is a nerf rather than a choice.</b>
    /// Spreading out has to actually work, or the rule reads as *"a second hut is always worse"*.
    /// Rings are diamonds of radius r, so <b>more than 2r apart in Manhattan distance is no overlap
    /// at all</b>.
    /// </remarks>
    [Fact]
    public void HutsFarEnoughApartDoNotTouchEachOther()
    {
        SimWorld world = Loop(Config).World;
        ClearTheValleyOfHuts(world);
        int radius = world.Config.GathererHutRingTiles;

        GridPos woods = WoodiestNear(world, world.Map.FoundingSite, 6);
        Workplace first = PlantAHut(world, woods);
        int alone = world.WoodedShareAround(first);

        // Beyond twice the radius, the diamonds cannot share a tile.
        PlantAHut(world, new GridPos(woods.X + (radius * 2) + 2, woods.Y));
        int apart = world.WoodedShareAround(first);

        _output.WriteLine($"alone {alone}, with a distant neighbour {apart}");

        Assert.True(alone > 0, "The fixture found no wood, so this proves nothing.");
        Assert.Equal(alone, apart);
    }

    /// <summary>⛔ A village that never crowds two rings hashes as it always did.</summary>
    [Fact]
    public void AVillageWithOneHutIsTheVillageThatCameBefore()
    {
        SimConfig config = Config;
        SimLoop a = Loop(config);
        SimLoop b = Loop(config);

        a.Step(config.TicksPerYear * 30);
        b.Step(config.TicksPerYear * 30);

        Assert.Equal(StateHash.Compute(a.World), StateHash.Compute(b.World));

        int gathering = 0;
        for (int i = 0; i < a.World.Workplaces.Count; i++)
        {
            if (a.World.Workplaces[i].GatheringRadius > 0)
            {
                gathering++;
            }
        }

        _output.WriteLine($"{gathering} gathering places after thirty years");
    }
}

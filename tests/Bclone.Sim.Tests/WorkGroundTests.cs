using Bclone.Sim.Core;
using Bclone.Sim.Determinism;
using Bclone.Sim.Logging;
using Bclone.Sim.World;
using Xunit;
using Xunit.Abstractions;

namespace Bclone.Sim.Tests;

/// <summary>
/// Ground that belongs to a building — <c>specs/mutable-terrain.md §5.1</c> (D86), slice C3b.
/// </summary>
/// <remarks>
/// <para>
/// <b>Residential ground is global and work ground is owned</b>, which is Joe's call and two
/// shapes rather than one general one. Residential belongs to the village — D42's division is
/// that the player picks the neighbourhood and the sim picks the tile. Work ground belongs to
/// a hut, because the labour allocator is built entirely around workplaces with a catchment
/// (D21–D25) and a zone owned by nobody contains no workplace.
/// </para>
/// <para>
/// The forester's hut that will use this is C3c. This is the ground under it.
/// </para>
/// </remarks>
public sealed class WorkGroundTests
{
    private readonly ITestOutputHelper _output;

    public WorkGroundTests(ITestOutputHelper output) => _output = output;

    private static SimWorld Build() =>
        SimFactory.CreatePhase0(VillageFixtures.Village, new InMemoryLogSink()).World;

    private static GridPos Somewhere(SimWorld world, int dx, int dy) =>
        new(world.Map.FoundingSite.X + dx, world.Map.FoundingSite.Y + dy);

    // ---------------------------------------------------------------
    //  Owning ground
    // ---------------------------------------------------------------

    [Fact]
    public void GroundStartsBelongingToNobody()
    {
        SimWorld world = Build();

        Assert.Equal(0, world.Zones.WorkGroundOwner(Somewhere(world, 3, 3)));
        Assert.Equal(0, world.Zones.WorkGroundTiles(1));
    }

    [Fact]
    public void ABuildingCanBeGivenGroundAndGiveItBack()
    {
        SimWorld world = Build();
        GridPos tile = Somewhere(world, 3, 3);

        Assert.True(world.Zones.SetWorkGround(tile, 7));
        Assert.Equal(7, world.Zones.WorkGroundOwner(tile));
        Assert.Equal(1, world.Zones.WorkGroundTiles(7));

        // Painting the same owner over its own ground is not a change.
        Assert.False(world.Zones.SetWorkGround(tile, 7));

        Assert.True(world.Zones.SetWorkGround(tile, 0));
        Assert.Equal(0, world.Zones.WorkGroundOwner(tile));
        Assert.Equal(0, world.Zones.WorkGroundTiles(7));
    }

    /// <summary>⭐ One owner per tile. A second hut cannot be given ground the first holds.</summary>
    /// <remarks>
    /// Two huts sharing ground would put two crews on the same trees, and the village could
    /// not say whose a stump was — the <em>right stuff in the wrong place</em> shape that has
    /// cost this project four investigations. <b>Refused rather than taken</b>, so a careless
    /// drag cannot quietly unstaff a hut across the valley.
    /// </remarks>
    [Fact]
    public void GroundBelongsToOneBuildingAtATime()
    {
        SimWorld world = Build();
        GridPos tile = Somewhere(world, 3, 3);

        Assert.True(world.Zones.SetWorkGround(tile, 7));
        Assert.False(world.Zones.SetWorkGround(tile, 9));

        Assert.Equal(7, world.Zones.WorkGroundOwner(tile));
        Assert.Equal(1, world.Zones.WorkGroundTiles(7));
        Assert.Equal(0, world.Zones.WorkGroundTiles(9));
    }

    [Fact]
    public void OffTheMapIsRefusedRatherThanThrown()
    {
        SimWorld world = Build();
        var offMap = new GridPos(world.Map.MinX - 50, world.Map.MinY - 50);

        Assert.False(world.Zones.SetWorkGround(offMap, 7));
        Assert.Equal(0, world.Zones.WorkGroundOwner(offMap));
    }

    [Fact]
    public void ReleasingGivesEveryTileBackAtOnce()
    {
        SimWorld world = Build();

        for (int i = 0; i < 5; i++)
        {
            world.Zones.SetWorkGround(Somewhere(world, i, 4), 7);
        }

        Assert.Equal(5, world.Zones.WorkGroundTiles(7));
        Assert.Equal(5, world.Zones.ReleaseWorkGround(7));
        Assert.Equal(0, world.Zones.WorkGroundTiles(7));
        Assert.Equal(0, world.Zones.ReleaseWorkGround(7));

        // And it is genuinely free — somebody else may have it now.
        Assert.True(world.Zones.SetWorkGround(Somewhere(world, 0, 4), 9));
    }

    // ---------------------------------------------------------------
    //  The two shapes stay two shapes
    // ---------------------------------------------------------------

    /// <summary>Residential is the village's and answers to no building (Joe, D86).</summary>
    [Fact]
    public void ResidentialGroundIsGlobalAndWorkGroundIsNot()
    {
        SimWorld world = Build();
        GridPos tile = Somewhere(world, 3, 3);

        world.PaintResidential(tile);
        Assert.True(world.Zones.IsResidential(tile));
        Assert.Equal(0, world.Zones.WorkGroundOwner(tile));

        // The two layers are independent: a hut may be given ground people also live on.
        Assert.True(world.Zones.SetWorkGround(tile, 7));
        Assert.True(world.Zones.IsResidential(tile));
    }

    // ---------------------------------------------------------------
    //  Determinism
    // ---------------------------------------------------------------

    /// <summary>⭐ Who owns which ground is part of the world.</summary>
    [Fact]
    public void WhoOwnsTheGroundIsHashed()
    {
        SimWorld world = Build();
        ulong before = StateHash.Compute(world);

        world.Zones.SetWorkGround(Somewhere(world, 3, 3), 7);

        Assert.NotEqual(before, StateHash.Compute(world));
    }

    /// <summary>
    /// ⭐ The same tiles given to different huts are different villages.
    /// </summary>
    /// <remarks>
    /// D51's trap: two states that read alike to the hash let a determinism test pass across
    /// a real divergence. Hashing the tile without the owner would have done exactly that.
    /// </remarks>
    [Fact]
    public void TheSameGroundGivenToADifferentHutIsADifferentWorld()
    {
        SimWorld first = Build();
        SimWorld second = Build();

        first.Zones.SetWorkGround(Somewhere(first, 3, 3), 7);
        second.Zones.SetWorkGround(Somewhere(second, 3, 3), 9);

        Assert.NotEqual(StateHash.Compute(first), StateHash.Compute(second));
    }

    /// <summary>Same decisions, same village.</summary>
    [Fact]
    public void TheSamePaintingGivesTheSameWorld()
    {
        SimWorld first = Build();
        SimWorld second = Build();

        foreach (SimWorld world in new[] { first, second })
        {
            for (int i = 0; i < 5; i++)
            {
                world.Zones.SetWorkGround(Somewhere(world, i, 4), 7);
            }
        }

        Assert.Equal(StateHash.Compute(first), StateHash.Compute(second));
    }

    /// <summary>
    /// A village where nobody paints any hashes as though the layer were not there.
    /// </summary>
    /// <remarks>
    /// The sparse convention, and it is what lets this ship without moving a golden — the
    /// same argument the stock-limit control shipped on (D62).
    /// </remarks>
    [Fact]
    public void AnUnusedLayerIsInvisible()
    {
        SimWorld world = Build();
        ulong before = StateHash.Compute(world);

        // Paint and take back: the world must be exactly where it started.
        GridPos tile = Somewhere(world, 3, 3);
        world.Zones.SetWorkGround(tile, 7);
        world.Zones.SetWorkGround(tile, 0);

        Assert.Equal(before, StateHash.Compute(world));
    }

    // ---------------------------------------------------------------
    //  Ground cannot outlive its building
    // ---------------------------------------------------------------

    /// <summary>⭐ Pulling a building down frees the ground it kept.</summary>
    /// <remarks>
    /// <b>Ground that is not given up is haunted</b> — land no other hut may be given,
    /// refused on behalf of a building that no longer exists, and the refusal cannot even
    /// name who holds it. There are three ways a workplace can end and they all go through
    /// one door, which is D76's lesson applied before it cost anything.
    /// </remarks>
    [Fact]
    public void GroundIsFreedWhenTheBuildingThatHeldItGoesAway()
    {
        SimWorld world = Build();

        StoreBuilding market = world.AnyStoreOf(StoreKind.Market);
        Workplace stall = world.Workplaces.Single(
            place => place.Kind == JobKind.Marketer && place.Position == market.Position);

        for (int i = 0; i < 4; i++)
        {
            world.Zones.SetWorkGround(Somewhere(world, i, 5), stall.Id);
        }

        Assert.Equal(4, world.Zones.WorkGroundTiles(stall.Id));

        world.Demolish(market);

        _output.WriteLine(
            $"pulled down {market.Name}; the stall kept {world.Zones.WorkGroundTiles(stall.Id)} tiles");

        Assert.DoesNotContain(world.Workplaces, place => place.Id == stall.Id);
        Assert.Equal(0, world.Zones.WorkGroundTiles(stall.Id));
        Assert.Equal(0, world.Zones.WorkGroundOwner(Somewhere(world, 0, 5)));
    }
}

using Bclone.Sim.Config;
using Bclone.Sim.Core;
using Bclone.Sim.Logging;
using Bclone.Sim.World;
using Xunit;
using Xunit.Abstractions;

namespace Bclone.Sim.Tests;

/// <summary>
/// ⭐⭐ Gridless slice 3 — <b>a villager holds a <see cref="Point"/>, and walks in fixed point</b>
/// (`specs/gridless.md §8`, D354).
/// </summary>
/// <remarks>
/// <para>
/// D329 did this for buildings: the anchor became a <see cref="Point"/>, the tile became a derived
/// question, and the tile-indexed world did not move. This is the same shape for people. <b>What
/// the player sees</b>: a worker stands <em>on</em> a free-placed hut rather than on its anchor
/// tile's centre half a tile away; a family stands on its doorstep. <b>What must not change</b>:
/// the route (still the tile cost field), the pace (one tile a tick at the shipped config), and
/// the tick anybody arrives on.
/// </para>
/// <para>
/// ⛔ <b>Joe kept slice 2c to buildings only so a movement bug could not arrive tangled with a
/// placement bug.</b> These are the guards that would catch the movement bug.
/// </para>
/// </remarks>
public sealed class VillagerPointTests
{
    private readonly ITestOutputHelper _output;

    public VillagerPointTests(ITestOutputHelper output) => _output = output;

    /// <summary>The tile a point is in is the FLOOR on both axes — negatives included.</summary>
    /// <remarks>
    /// The valley straddles its own founding site, so a villager at −0.25 is on tile −1, not tile
    /// 0. C#'s <c>(int)</c> truncates toward zero and would fold two tiles into one at the origin —
    /// the trap <see cref="Fixed"/> and <see cref="SubTile"/> each already record.
    /// </remarks>
    [Theory]
    [InlineData(3, 999, 3)]
    [InlineData(4, 0, 4)]
    [InlineData(-1, 750, -1)]
    [InlineData(0, 0, 0)]
    [InlineData(-1, 0, -1)]
    public void AVillagersTileIsTheTileTheirPointIsIn(int whole, int thousandths, int tile)
    {
        var at = new Point(
            Fixed.FromInt(whole) + Fixed.FromRatio(thousandths, 1000),
            Fixed.FromInt(whole) + Fixed.FromRatio(thousandths, 1000));

        Assert.Equal(new GridPos(tile, tile), at.ToTile());
        Assert.Equal(new GridPos(7, -3), Point.CentreOf(new GridPos(7, -3)).ToTile());
    }

    /// <summary>
    /// ⛔ At the shipped pace every tick's position is a TILE CENTRE, and consecutive positions
    /// are the same tile or an axis-aligned neighbour — <b>exactly the walk the tile stepping
    /// made</b>.
    /// </summary>
    /// <remarks>
    /// The cost field is 4-connected (<c>TerrainCostField</c> tries east, west, south, north), so
    /// a diagonal position or a fraction of a tile at a tick boundary would mean the walk had
    /// stopped following the field — the two-cost-systems regression `gridless.md §6` forbids.
    /// </remarks>
    [Fact]
    public void AtTheShippedPaceEveryTickIsATileCentreOnTheRoute()
    {
        var (loop, _) = Phase0Fixtures.Build(Phase0Fixtures.Plenty);
        Villager villager = loop.World.Villager;

        int moves = 0;
        Point previous = villager.Position;
        for (int i = 0; i < 3_000; i++)
        {
            loop.StepOnce();
            Point now = villager.Position;

            Assert.Equal(Point.CentreOf(now.ToTile()), now);

            GridPos a = previous.ToTile();
            GridPos b = now.ToTile();
            int manhattan = System.Math.Abs(a.X - b.X) + System.Math.Abs(a.Y - b.Y);
            if (manhattan > 0)
            {
                moves++;
            }

            // A step is one tile along one axis, or a snap home / to a site (D102) — never a
            // diagonal, never a fraction.
            Assert.True(
                manhattan <= 1 || (a.X == b.X || a.Y == b.Y) || manhattan > 2,
                $"tick {loop.World.Tick}: {a} → {b} is a diagonal step");

            previous = now;
        }

        _output.WriteLine($"{moves} moves in 3,000 ticks, every one a whole tile");
        Assert.True(moves > 100, "the villager never walked anywhere");
    }

    /// <summary>
    /// ⛔ The walk is tile by tile at EVERY pace, and <b>arrives on the tick it always did</b>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// ⛔ **The tick the villager first gathers is pinned to the value measured on the tile-stepping
    /// code before this slice** (20 at the shipped pace, 41 at `travel_ticks_per_unit = 3`). The
    /// economy is derived from ticks per tile, and a walk that arrived a tick early at every tile
    /// would re-derive it silently — the D122 shape.
    /// </para>
    /// <para>
    /// ⚠️ **And no fraction of a tile at any tick boundary, at any pace — deliberately.** A
    /// fractional walk needs to know which way it is going mid-leg, and the tile a villager is on
    /// cannot say whether they are leaving it or arriving; that is a waypoint, which is slice 4's
    /// state. This slice changes the type and where arrival stands, nothing about the walk. The
    /// only off-centre position a villager can have is standing on a free-placed building, which
    /// the Phase 0 fixture has none of.
    /// </para>
    /// </remarks>
    [Theory]
    [InlineData(1, FirstGatherAtPace1)]
    [InlineData(3, FirstGatherAtPace3)]
    public void TheWalkIsTileByTileAtEveryPaceAndArrivesWhenItAlwaysDid(int pace, int firstGatherTick)
    {
        SimConfig config = Phase0Fixtures.Plenty with { TravelTicksPerUnit = pace };
        var (loop, _) = Phase0Fixtures.Build(config);
        Villager villager = loop.World.Villager;

        int firstGather = -1;
        for (int i = 0; i < 600 && firstGather < 0; i++)
        {
            loop.StepOnce();
            Point now = villager.Position;
            Assert.Equal(Point.CentreOf(now.ToTile()), now);

            if (villager.State == VillagerState.Gathering)
            {
                firstGather = (int)loop.World.Tick;
            }
        }

        _output.WriteLine($"pace {pace}: first gather at tick {firstGather}");
        Assert.Equal(firstGatherTick, firstGather);
    }

    // ⚠️ MEASURED ON THE TILE-STEPPING CODE BEFORE THIS SLICE, then pinned. If either moves, the
    // walk's timing moved, and that is the economy moving under a slice that promised not to.
    private const int FirstGatherAtPace1 = 20;
    private const int FirstGatherAtPace3 = 41;

    /// <summary>
    /// ⭐⭐ A villager who arrives at a free-placed building stands ON it — <b>the thing the
    /// player sees</b>.
    /// </summary>
    /// <remarks>
    /// Since D330 a hut can stand at (3.3, 7.6); until this slice its forager stood at (3.5, 7.5),
    /// the anchor tile's centre, half a tile from the door. The route is still the cost field's,
    /// tile by tile, to the anchor tile; the last thing arrival does is step to where the building
    /// actually is. Zero extra ticks — the tile was already paid for.
    /// </remarks>
    [Fact]
    public void AVillagerStandsOnTheBuildingTheyArriveAt()
    {
        SimLoop loop = SimFactory.CreatePhase0(VillageFixtures.Village, new InMemoryLogSink());
        SimWorld world = loop.World;

        // A gatherer's hut a third of a tile off its anchor, raised outright — on ground where the
        // turned-out rectangle collides with nobody (touching is apart, overlapping is not: D331).
        var offset = new Point(Fixed.FromRatio(-1, 3), Fixed.FromRatio(1, 4));
        (GridPos anchor, Point where) = FreeGroundNear(world, offset);
        Assert.True(world.Mark(BuildingKind.GathererHut, where, Angle.Zero).Allowed);

        Workplace site = Assert.Single(
            world.Workplaces, place => place.Construction?.Kind == BuildingKind.GathererHut && place.Tile == anchor);
        BuildFixtures.StockTheSite(site);
        for (int i = 0; i <= site.Construction!.Recipe.WorkTicks; i++)
        {
            site.Construction.Work();
        }

        world.Complete(site);
        Workplace hut = Assert.Single(
            world.Workplaces, place => place.Kind == JobKind.Forager && place.Tile == anchor && !place.IsSite);
        Assert.Equal(where, hut.Position);
        Assert.NotEqual(Point.CentreOf(anchor), hut.Position);

        // Run until somebody works there and is standing at it.
        Villager? there = null;
        for (int i = 0; i < 4_000 && there is null; i++)
        {
            loop.StepOnce();
            foreach (Villager villager in world.Villagers)
            {
                if (villager.Alive && villager.WorkplaceId == hut.Id && villager.Tile == anchor
                    && villager.State == VillagerState.Gathering)
                {
                    there = villager;
                    break;
                }
            }
        }

        Assert.NotNull(there);
        _output.WriteLine($"{there!.Name} gathers at {there.Position}; the hut stands at {hut.Position}");

        Assert.Equal(hut.Position, there.Position);
        Assert.Equal(anchor, there.Tile);
    }

    /// <summary>A villager at home stands on the doorstep — the home's own <c>Point</c>.</summary>
    [Fact]
    public void AVillagerAtHomeStandsOnTheDoorstep()
    {
        SimLoop loop = SimFactory.CreatePhase0(VillageFixtures.Village, new InMemoryLogSink());
        SimWorld world = loop.World;

        Villager? resting = null;
        for (int i = 0; i < 4_000 && resting is null; i++)
        {
            loop.StepOnce();
            foreach (Villager villager in world.Villagers)
            {
                Household home = world.HouseholdOf(villager);
                if (villager.Alive && home.HomePosition is Point door
                    && villager.State == VillagerState.Resting && villager.Tile == door.ToTile())
                {
                    resting = villager;
                    break;
                }
            }
        }

        Assert.NotNull(resting);
        Point doorstep = world.HouseholdOf(resting!).HomePosition!.Value;
        _output.WriteLine($"{resting.Name} rests at {resting.Position}; the door is at {doorstep}");
        Assert.Equal(doorstep, resting.Position);
    }

    /// <summary>Bare, reachable ground near the founding site where a hut at <paramref name="offset"/> from the tile centre may stand.</summary>
    private static (GridPos Anchor, Point Where) FreeGroundNear(SimWorld world, Point offset)
    {
        GridPos site = world.Map.FoundingSite;
        for (int radius = 1; radius < 14; radius++)
        {
            for (int dy = -radius; dy <= radius; dy++)
            {
                for (int dx = -radius; dx <= radius; dx++)
                {
                    var at = new GridPos(site.X + dx, site.Y + dy);
                    Point where = Point.CentreOf(at) + offset;
                    if (world.Map.Contains(at)
                        && !world.HasSomethingToHarvest(at)
                        && where.ToTile() == at
                        && world.CanBuildAt(BuildingKind.GathererHut, where, facing: Angle.Zero).Allowed)
                    {
                        return (at, where);
                    }
                }
            }
        }

        throw new Xunit.Sdk.XunitException("Nowhere near the founding site takes an off-centre hut.");
    }
}

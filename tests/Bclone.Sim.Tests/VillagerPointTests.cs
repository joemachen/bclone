using Bclone.Sim.Config;
using Bclone.Sim.Core;
using Bclone.Sim.Determinism;
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
    /// ⛔⛔ A walk takes EXACTLY the ticks the tile route took — <b>clock A, Joe's call</b>
    /// (gridless slice 4, D356).
    /// </summary>
    /// <remarks>
    /// <para>
    /// People walk straight lines now, and a straight line is shorter than a staircase — so the
    /// one thing this slice must not do is make them arrive earlier. The economy is derived from
    /// ticks per tile (`VillageEconomy.RoundTripTicks` and everything above it), and D122 is what
    /// one tile of drift costs. **The tick the villager first gathers is pinned to the value
    /// measured on the tile-stepping code before slice 3** — 20 at the shipped pace, 41 at
    /// `travel_ticks_per_unit = 3`. A leg of <c>k</c> route tiles costs <c>k</c> ticks whatever
    /// its straight length; this is the guard that says so.
    /// </para>
    /// <para>
    /// ⚠️ **Clock B — the real-clock rebalance — is Joe's eventual want and its own slice**
    /// (`DESIGN.md §4`, Phase 4.5). When it lands, these pins move deliberately, with the
    /// re-derivation, not by accident here.
    /// </para>
    /// </remarks>
    [Theory]
    [InlineData(1, FirstGatherAtPace1)]
    [InlineData(3, FirstGatherAtPace3)]
    public void AWalkTakesExactlyAsLongAsTheTileRouteDid(int pace, int firstGatherTick)
    {
        SimConfig config = Phase0Fixtures.Plenty with { TravelTicksPerUnit = pace };
        var (loop, _) = Phase0Fixtures.Build(config);
        Villager villager = loop.World.Villager;

        int firstGather = -1;
        for (int i = 0; i < 600 && firstGather < 0; i++)
        {
            loop.StepOnce();
            if (villager.State == VillagerState.Gathering)
            {
                firstGather = (int)loop.World.Tick;
            }
        }

        _output.WriteLine($"pace {pace}: first gather at tick {firstGather}");
        Assert.Equal(firstGatherTick, firstGather);
    }

    // ⚠️ MEASURED ON THE TILE-STEPPING CODE BEFORE SLICE 3, then pinned. If either moves, the
    // walk's timing moved, and that is the economy moving under a slice that promised not to.
    private const int FirstGatherAtPace1 = 20;
    private const int FirstGatherAtPace3 = 41;

    /// <summary>
    /// ⛔⛔ The VALLEY walks on the same clock as before — <b>the pin that can actually see
    /// clock B</b> (D356).
    /// </summary>
    /// <remarks>
    /// <para>
    /// The Phase 0 pins above cannot: that fixture's home, hut and store stand on one row, so a
    /// straight line and a staircase are the same length there, and a leg charged its straight
    /// length instead of its route steps passed both pins green. **Found by red check.** The
    /// village fixture has a river and buildings off the row, so its walks have diagonals — and
    /// under clock B its foragers would start gathering earlier.
    /// </para>
    /// <para>
    /// Measured on slice 3's code before this slice: in the first 2,000 ticks somebody enters
    /// <c>Gathering</c> **50** times, the first at tick **17**, the tenth at **247**, the fiftieth
    /// at **1,963**. Identical after — which is the clock-A promise as four numbers.
    /// </para>
    /// </remarks>
    [Fact]
    public void TheValleyWalksOnTheSameClockAsBefore()
    {
        SimLoop loop = SimFactory.CreatePhase0(VillageFixtures.Village, new InMemoryLogSink());
        SimWorld world = loop.World;

        var was = new Dictionary<int, VillagerState>();
        int entries = 0;
        var at = new List<ulong>();
        for (int i = 0; i < 2_000; i++)
        {
            loop.StepOnce();
            foreach (Villager villager in world.Villagers)
            {
                VillagerState before = was.GetValueOrDefault(villager.Id, VillagerState.Idle);
                if (villager.State == VillagerState.Gathering && before != VillagerState.Gathering)
                {
                    entries++;
                    if (entries is 1 or 10 or 50)
                    {
                        at.Add(world.Tick);
                    }
                }

                was[villager.Id] = villager.State;
            }
        }

        _output.WriteLine($"{entries} gathering trips began; the 1st at {at[0]}, the 10th at {at[1]}, the 50th at {at[2]}");
        Assert.Equal(50, entries);
        Assert.Equal(new ulong[] { 17, 247, 1963 }, at);
    }

    /// <summary>
    /// ⭐⭐ The staircase is pulled taut — <b>a walk with a diagonal in it is a straight line, and
    /// the feature is not vacuous</b> (D356).
    /// </summary>
    /// <remarks>
    /// Two things that could not be true under tile stepping: a position at a tick boundary that
    /// is on no tile centre, and a position off the centre on BOTH axes at once — a row leg is off on
    /// one axis only, so both means a diagonal. Over three thousand ticks both happen many times. ⚠️ And a leg lands exactly
    /// on its waypoint — a rounding crumb short would leave a villager one tick from arriving for
    /// ever — so every tick where the leg has just ended is asserted to be a tile centre.
    /// </remarks>
    [Fact]
    public void AStaircaseIsPulledTaut()
    {
        // ⚠️ The village fixture, not Phase 0's: Phase 0's home, hut and store all stand on one row,
        // so every walk there is a straight row and nothing can be pulled. Found by measuring.
        SimLoop loop = SimFactory.CreatePhase0(VillageFixtures.Village, new InMemoryLogSink());
        SimWorld world = loop.World;

        int offCentre = 0;
        int diagonal = 0;
        int atRest = 0;

        for (int i = 0; i < 3_000; i++)
        {
            loop.StepOnce();
            foreach (Villager villager in world.Villagers)
            {
                if (!villager.Alive)
                {
                    continue;
                }

                Point now = villager.Position;
                Point centre = Point.CentreOf(now.ToTile());

                if (now != centre)
                {
                    offCentre++;
                }

                if (now.X != centre.X && now.Y != centre.Y)
                {
                    diagonal++;
                }

                // ⛔ With no leg in progress and nothing to stand on, a villager is on a tile
                // centre EXACTLY — a leg lands on its waypoint, not a crumb short of it, or arrival
                // (`Position == centre`) would never be recognised.
                if (villager.LegSteps == 0 && world.StandingPlaceAt(villager.Tile) is null)
                {
                    Assert.Equal(centre, now);
                    atRest++;
                }
            }
        }

        _output.WriteLine($"{offCentre} villager-ticks off a tile centre, {diagonal} off on both axes, {atRest} at rest on exact centres");
        Assert.True(offCentre > 50, "nobody was ever off a tile centre — the string is not pulled");
        Assert.True(diagonal > 20, "nobody was ever off-centre on both axes — no leg was diagonal");
        Assert.True(atRest > 10, "nobody was ever at rest on bare ground");
    }

    /// <summary>⛔ Nobody ever stands on water, walking a straight line or otherwise.</summary>
    /// <remarks>
    /// The line-of-sight test is conservative at corners so a string cannot squeeze between two
    /// ponds; this is that promise measured on the valley with the river in it, every villager,
    /// every tick, for three thousand ticks. The red check is a raycast that ignores water.
    /// </remarks>
    [Fact]
    public void NobodyEverStandsOnWater()
    {
        SimLoop loop = SimFactory.CreatePhase0(VillageFixtures.Village, new InMemoryLogSink());
        SimWorld world = loop.World;

        int checked_ = 0;
        for (int i = 0; i < 3_000; i++)
        {
            loop.StepOnce();
            foreach (Villager villager in world.Villagers)
            {
                if (!villager.Alive)
                {
                    continue;
                }

                checked_++;
                Assert.NotEqual(Terrain.Water, world.Map.TerrainAt(villager.Tile));
            }
        }

        _output.WriteLine($"{checked_} villager-ticks, none on water");
        Assert.True(checked_ > 3_000);
    }

    /// <summary>
    /// ⭐ Changing your mind mid-leg re-plans from where you ARE, not from where the leg was going.
    /// </summary>
    /// <remarks>
    /// Tile stepping re-aimed every tick for free; a leg is committed state, so a villager who is
    /// sent home mid-walk must drop it and plan a fresh one from their off-centre point — or they
    /// would finish walking the wrong way first. Posed: walk toward food, then force the state
    /// home, and the next leg's target is home.
    /// </remarks>
    [Fact]
    public void ALegIsDroppedWhenTheTargetChanges()
    {
        var (loop, _) = Phase0Fixtures.Build(Phase0Fixtures.Plenty);
        Villager villager = loop.World.Villager;

        // Walk until a leg is in progress toward food.
        int guard = 0;
        while ((villager.State != VillagerState.TravelingToFood || villager.LegSteps == 0) && guard++ < 400)
        {
            loop.StepOnce();
        }

        Assert.True(villager.LegSteps > 0, "never caught the villager mid-leg toward food");
        GridPos foodLegTarget = villager.LegTarget;

        // Send them home from wherever they are.
        villager.State = VillagerState.TravelingHome;
        loop.StepOnce();

        _output.WriteLine($"food leg toward {foodLegTarget}; after re-aim the leg is toward {villager.LegTarget}, home is {loop.World.RestingPlaceOf(villager)}");
        Assert.Equal(loop.World.RestingPlaceOf(villager), villager.LegTarget);
    }

    /// <summary>The leg is sim state, so it is in the hash — two worlds a step apart along one leg differ.</summary>
    [Fact]
    public void TheLegIsHashed()
    {
        var (loop, _) = Phase0Fixtures.Build(Phase0Fixtures.Plenty);
        Villager villager = loop.World.Villager;
        int guard = 0;
        while (villager.LegSteps < 2 && guard++ < 600)
        {
            loop.StepOnce();
        }

        Assert.True(villager.LegSteps >= 2, "never caught a leg two steps long");
        ulong before = StateHash.Compute(loop.World);
        villager.LegStep = villager.LegStep == 0 ? 1 : 0;
        ulong after = StateHash.Compute(loop.World);
        Assert.NotEqual(before, after);
    }

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

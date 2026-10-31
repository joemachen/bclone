using Bclone.Sim.Core;
using Bclone.Sim.Logging;
using Bclone.Sim.World;
using Xunit;
using Xunit.Abstractions;

namespace Bclone.Sim.Tests;

/// <summary>
/// Every roofed building has a door on its front (D619–D621, `specs/doors.md` §4.1–§4.3, commit 1: the door, derived).
/// </summary>
/// <remarks>
/// Commit 1 changes nothing anybody does: the door is worked out and said, never walked through yet. So these guards pose
/// shapes and read the door; none of them steps the village.
/// </remarks>
public sealed class DoorTests
{
    private readonly ITestOutputHelper _output;

    public DoorTests(ITestOutputHelper output) => _output = output;

    private static SimWorld World() =>
        SimFactory.CreatePhase0(VillageFixtures.Village, new InMemoryLogSink()).World;

    private static readonly Angle[] Quarters =
    {
        Angle.Zero, Angle.FromTurnFraction(1, 4), Angle.FromTurnFraction(2, 4), Angle.FromTurnFraction(3, 4),
    };

    /// <summary>The sim's door faces the nearest of the four ways; a facing exactly between two turns back (§4.2).</summary>
    [Theory]
    [InlineData(0, Compass.North)]
    [InlineData(16384, Compass.East)]
    [InlineData(32768, Compass.South)]
    [InlineData(49152, Compass.West)]
    [InlineData(8191, Compass.North)]
    [InlineData(8192, Compass.North)]
    [InlineData(8193, Compass.East)]
    [InlineData(24576, Compass.East)]
    [InlineData(40960, Compass.South)]
    [InlineData(57344, Compass.West)]
    [InlineData(57345, Compass.North)]
    [InlineData(65535, Compass.North)]
    public void TheFrontIsTheNearestOfTheFourWaysAndATieTurnsBack(int raw, Compass expected) =>
        Assert.Equal(expected, SimWorld.FrontOf(Angle.FromRaw((ushort)raw)));

    /// <summary>
    /// ⭐ On open ground, every shape's door is on the middle of its front at every quarter turn — the tile half a tile
    /// beyond the front edge's middle, the rule `ZzDoors` measured on Joe's saves (§3).
    /// </summary>
    [Theory]
    [InlineData(BuildingKind.GathererHut)]
    [InlineData(BuildingKind.WoodcutterHut)]
    [InlineData(BuildingKind.Granary)]
    [InlineData(BuildingKind.TownHall)]
    public void OnOpenGroundTheDoorIsOnTheMiddleOfTheFront(BuildingKind kind)
    {
        SimWorld world = World();
        GridPos at = OpenGround(world);
        foreach (Angle facing in Quarters)
        {
            Footprint shape = Pose(world, kind, at, facing);
            Door door = world.DoorAt(shape);
            GridPos middle = (shape.Origin + new Point(Fixed.Zero, -Fixed.FromRatio(shape.Height + 1, 2)).RotatedBy(facing)).ToTile();
            _output.WriteLine($"{kind} facing {facing.Raw}: door {door.Tile} → {door.Doorstep} faces {door.Faces}; the front's middle {middle}");

            Assert.False(door.Shut);
            Assert.Equal(DoorBlock.None, door.FrontBlockedBy);
            Assert.Equal(SimWorld.FrontOf(facing), door.Faces);
            Assert.True(shape.Covers(door.Tile), $"the door tile {door.Tile} is not the building's");
            Assert.False(shape.Covers(door.Doorstep), $"the doorstep {door.Doorstep} is the building's own tile");
            Assert.Equal(Step(door.Tile, door.Faces), door.Doorstep);
            Assert.Equal(middle, door.Doorstep);
        }
    }

    /// <summary>
    /// A house's door does not move (§4.2): at a quarter turn its doorstep is the tile its plot has always called its door
    /// (`PlotShape.Door`), which the site chooser keeps clear.
    /// </summary>
    [Fact]
    public void AHousesDoorstepIsItsPlotsDoor()
    {
        SimWorld world = World();
        GridPos front = OpenGround(world);
        foreach (Angle facing in Quarters)
        {
            Footprint house = world.FootprintOf(BuildingKind.Home, world.HomeAnchorOn(front, facing), facing);
            PlotShape plot = world.PlotFor(front, facing, 1);
            Door door = world.DoorAt(house);
            _output.WriteLine($"facing {facing.Raw}: doorstep {door.Doorstep}, the plot's door {plot.Door}");
            Assert.Equal(plot.Door, door.Doorstep);
        }
    }

    /// <summary>
    /// ⭐⭐ A front that cannot open moves the door to the back, then the right, then the left; with none it is shut — and
    /// the door says what blocked the front (§4.3, Joe's call 2).
    /// </summary>
    [Fact]
    public void ABlockedFrontOpensTheBackThenTheRightThenTheLeft()
    {
        SimWorld world = World();
        GridPos at = OpenGround(world);
        Footprint hut = Pose(world, BuildingKind.GathererHut, at, Angle.Zero);
        Assert.Equal(Compass.North, world.DoorAt(hut).Faces);

        (GridPos Pile, Compass Then)[] blocks =
        {
            (new GridPos(at.X, at.Y - 1), Compass.South),
            (new GridPos(at.X, at.Y + 1), Compass.East),
            (new GridPos(at.X + 1, at.Y), Compass.West),
        };
        foreach ((GridPos pile, Compass then) in blocks)
        {
            Assert.True(world.Mark(BuildingKind.Pile, pile).Allowed, $"the pile at {pile} did not stand");
            Door door = world.DoorAt(hut);
            _output.WriteLine($"a pile at {pile}: the door faces {door.Faces}, the front blocked by {door.FrontBlockedBy}");
            Assert.False(door.Shut);
            Assert.Equal(then, door.Faces);
            Assert.Equal(DoorBlock.Built, door.FrontBlockedBy);
            Assert.Equal(new GridPos(at.X, at.Y - 1), door.FrontStep);
        }

        Assert.True(world.Mark(BuildingKind.Pile, new GridPos(at.X - 1, at.Y)).Allowed);
        Door shut = world.DoorAt(hut);
        Assert.True(shut.Shut);
        Assert.Equal(Compass.North, shut.Faces);
    }

    /// <summary>
    /// ⭐ A door slides along its front before it turns the corner (§4.3, D621), and a side is only the tiles that face
    /// that way: a granary's front with one doorstep taken opens from its other front tile, still north and said nowhere;
    /// with both taken it turns to the back — from a back tile, never from a tile that opens into the granary itself.
    /// </summary>
    [Fact]
    public void AWideFrontSlidesBeforeItTurns()
    {
        SimWorld world = World();
        GridPos at = OpenGround(world);
        Footprint granary = Pose(world, BuildingKind.Granary, at, Angle.Zero);
        Door first = world.DoorAt(granary);
        Assert.Equal(new GridPos(at.X, at.Y - 2), first.Doorstep);

        Assert.True(world.Mark(BuildingKind.Pile, first.Doorstep).Allowed);
        Door slid = world.DoorAt(granary);
        _output.WriteLine($"one doorstep taken: the door {slid.Tile} → {slid.Doorstep} faces {slid.Faces}");
        Assert.Equal(Compass.North, slid.Faces);
        Assert.Equal(new GridPos(at.X - 1, at.Y - 2), slid.Doorstep);
        Assert.Equal(DoorBlock.Built, slid.FrontBlockedBy);
        Assert.Null(world.SayWhereTheDoorIs(slid));

        Assert.True(world.Mark(BuildingKind.Pile, slid.Doorstep).Allowed);
        Door turned = world.DoorAt(granary);
        _output.WriteLine($"both taken: the door {turned.Tile} → {turned.Doorstep} faces {turned.Faces}");
        Assert.Equal(Compass.South, turned.Faces);
        Assert.Equal(at.Y, turned.Tile.Y);
        Assert.False(granary.Covers(turned.Doorstep));
    }

    /// <summary>
    /// Free ground the village does not reach is a blocked front too (§4.3): a granary's doorsteps shut into a pocket by
    /// piles open nothing, so its door is at the back and the card says nobody can reach the ground in front.
    /// </summary>
    [Fact]
    public void FreeGroundNobodyReachesIsABlockedFront()
    {
        SimWorld world = World();
        GridPos at = OpenGround(world);
        PlacementVerdict raised = world.Mark(BuildingKind.Granary, at);
        Assert.True(raised.Allowed, raised.Reason);
        StoreBuilding? store = world.StoreBuildings.Find(s => s.Footprint.Covers(at));
        Workplace? site = world.Workplaces.Find(w => w.Footprint.Covers(at));
        Door? before = store is not null ? world.DoorOf(store) : world.DoorOf(site!);
        Assert.Equal(Compass.North, before!.Value.Faces);

        // Walls round the two doorsteps' outer edges, none between them and the granary: free, unwalled from the door,
        // and reached by nobody. (Piles were tried first: a pile on uncleared ground waits to stand, and the pocket leaked.)
        GridPos westStep = new(at.X - 1, at.Y - 2);
        GridPos eastStep = new(at.X, at.Y - 2);
        world.Zones.Wall(westStep, new GridPos(westStep.X - 1, westStep.Y), up: true);
        world.Zones.Wall(westStep, new GridPos(westStep.X, westStep.Y - 1), up: true);
        world.Zones.Wall(eastStep, new GridPos(eastStep.X + 1, eastStep.Y), up: true);
        world.Zones.Wall(eastStep, new GridPos(eastStep.X, eastStep.Y - 1), up: true);

        Assert.True(world.IsWalkable(new GridPos(at.X, at.Y - 2)) && !world.StandsOn(new GridPos(at.X, at.Y - 2)),
            "the pocket is not free ground");
        Door after = store is not null ? world.DoorOf(store)!.Value : world.DoorOf(site!)!.Value;
        _output.WriteLine($"the doorsteps shut in: the door faces {after.Faces}, the front blocked by {after.FrontBlockedBy}");
        Assert.Equal(Compass.South, after.Faces);
        Assert.Equal(DoorBlock.Unreached, after.FrontBlockedBy);
        Assert.Equal("Its door is on its south side — nobody can reach the ground in front of it.", world.SayWhereTheDoorIs(after));
    }

    /// <summary>A fence across the front is a blocked front too (§4.3): the wall is read between the door and its doorstep.</summary>
    [Fact]
    public void AFenceAcrossTheFrontMovesTheDoor()
    {
        SimWorld world = World();
        GridPos at = OpenGround(world);
        Footprint hut = Pose(world, BuildingKind.GathererHut, at, Angle.Zero);
        world.Zones.Wall(at, new GridPos(at.X, at.Y - 1), up: true);

        Door door = world.DoorAt(hut);
        Assert.Equal(Compass.South, door.Faces);
        Assert.Equal(DoorBlock.Fence, door.FrontBlockedBy);
        Assert.Equal("Its door is on its south side — a fence runs across its front.", world.SayWhereTheDoorIs(door));
    }

    /// <summary>
    /// ⭐ A building faced onto water opens landward — §3's fishing huts and trading posts, without a rule of their own
    /// (Joe's call 5) — and the card's sentence says why.
    /// </summary>
    [Fact]
    public void AFrontOntoWaterOpensLandward()
    {
        SimWorld world = World();
        (GridPos water, Compass toward) = AShore(world);
        GridPos at = Step(water, (Compass)(((int)toward + 2) & 3));
        Footprint hut = Pose(world, BuildingKind.GathererHut, at, Angle.FromTurnFraction((int)toward, 4));
        var landward = (Compass)(((int)toward + 2) & 3);

        Door door = world.DoorAt(hut);
        _output.WriteLine($"water at {water}, the hut at {at} facing {toward} onto it: the door faces {door.Faces}");
        Assert.Equal(toward, door.Front);
        Assert.Equal(landward, door.Faces);
        Assert.Equal(DoorBlock.Water, door.FrontBlockedBy);
        Assert.Equal($"Its door is on its {landward.ToString().ToLowerInvariant()} side — water lies in front of it.", world.SayWhereTheDoorIs(door));
    }

    /// <summary>
    /// The sentence: nothing for a door on its front, the name of what stands in front of a moved one, and a shut building
    /// said as shut (§4.3).
    /// </summary>
    [Fact]
    public void TheSentenceSaysWhyADoorMoved()
    {
        SimWorld world = World();
        GridPos at = OpenGround(world);
        Footprint hut = Pose(world, BuildingKind.GathererHut, at, Angle.Zero);
        Assert.Null(world.SayWhereTheDoorIs(world.DoorAt(hut)));

        GridPos front = new(at.X, at.Y - 1);
        Assert.True(world.Mark(BuildingKind.Pile, front).Allowed);
        string pile = world.StoreBuildings.Find(s => s.Tile == front)!.Name;
        Assert.Equal($"Its door is on its south side — {pile} stands in front of it.", world.SayWhereTheDoorIs(world.DoorAt(hut)));

        foreach (GridPos side in new[] { new GridPos(at.X, at.Y + 1), new GridPos(at.X + 1, at.Y), new GridPos(at.X - 1, at.Y) })
        {
            Assert.True(world.Mark(BuildingKind.Pile, side).Allowed);
        }

        Assert.Equal("No side of it opens onto ground the village can reach.", world.SayWhereTheDoorIs(world.DoorAt(hut)));
    }

    /// <summary>
    /// Wells, piles and bridges have no door (§4.1, Joe's call 1); every other building the founding raises has one, and a
    /// building with no door has nothing to say.
    /// </summary>
    [Fact]
    public void APileHasNoDoorAndTheFoundingsBuildingsHaveOne()
    {
        SimWorld world = World();
        GridPos at = OpenGround(world);
        Assert.True(world.Mark(BuildingKind.Pile, at).Allowed);
        StoreBuilding pile = world.StoreBuildings.Find(s => s.Tile == at)!;
        Assert.Null(world.DoorOf(pile));
        Assert.Null(world.SayWhereTheDoorIs(null));

        int doors = 0;
        foreach (Workplace place in world.Workplaces)
        {
            Assert.NotNull(world.DoorOf(place));
            doors++;
        }

        foreach (StoreBuilding store in world.StoreBuildings)
        {
            if (store.Kind != StoreKind.Pile)
            {
                Assert.NotNull(world.DoorOf(store));
                doors++;
            }
        }

        foreach (Household household in world.Households)
        {
            Assert.Equal(household.HasHome, world.DoorOf(household) is not null);
            doors += household.HasHome ? 1 : 0;
        }

        _output.WriteLine($"{doors} doors at the founding");
        Assert.True(doors > 0);
    }

    /// <summary>
    /// ⭐⭐ A building is shut exactly when nothing reaches it (§4.3): a door slides along a side and turns to the next, so
    /// it opens wherever any free tile the village reaches touches the building across an unwalled edge — D383's own
    /// test, asked of every doored building of a fixture village fifteen years on.
    /// </summary>
    /// <remarks>
    /// Reached is asked through the cost field from the first home (<see cref="TravelCostField.CanReach"/>), not through
    /// the sweep the door reads, so the guard does not agree with itself (D419).
    /// </remarks>
    [Fact]
    public void ABuildingIsShutExactlyWhenNothingReachesIt()
    {
        SimLoop loop = SimFactory.CreatePhase0(VillageFixtures.Village, new InMemoryLogSink());
        SimWorld world = loop.World;
        loop.Step(15 * world.Config.TicksPerYear);
        GridPos from = world.FirstHomeOrFoundingSite();

        int open = 0, moved = 0;
        foreach ((string name, Footprint shape, Door door) in Doored(world))
        {
            bool reached = false;
            foreach (GridPos tile in shape.CoveredTiles())
            {
                foreach (Compass way in new[] { Compass.North, Compass.East, Compass.South, Compass.West })
                {
                    GridPos beside = Step(tile, way);
                    reached |= !shape.Covers(beside)
                        && !world.StandsOn(beside)
                        && (world.WallsOn(tile) & ZoneMap.EdgeBit(beside.X - tile.X, beside.Y - tile.Y)) == 0
                        && world.TravelCost.CanReach(from, beside);
                }
            }

            Assert.True(reached != door.Shut, $"{name}: reached {reached}, shut {door.Shut}");
            open += door.Shut ? 0 : 1;
            moved += door.FrontBlockedBy != DoorBlock.None && !door.Shut ? 1 : 0;
            if (!door.Shut)
            {
                Assert.True(shape.Covers(door.Tile) && !shape.Covers(door.Doorstep), name);
                Assert.Equal(Step(door.Tile, door.Faces), door.Doorstep);
            }
        }

        _output.WriteLine($"{open} doors open, {moved} of them off their front");
        Assert.True(open > 5, $"only {open} doored buildings — the pose did not grow a village");
    }

    /// <summary>
    /// ⛔ JOE'S RULE: the doors are an index on the standing, wall and terrain counters — worked out once per change, never
    /// per ask (CLAUDE.md).
    /// </summary>
    [Fact]
    public void ADoorIsWorkedOutOncePerChangeNotPerAsk()
    {
        SimWorld world = World();
        GridPos at = OpenGround(world);
        Footprint hut = Pose(world, BuildingKind.GathererHut, at, Angle.Zero);

        int before = world.DoorsWorkedOut;
        world.DoorAt(hut);
        world.DoorAt(hut);
        world.DoorAt(hut);
        Assert.Equal(before + 1, world.DoorsWorkedOut);

        world.Zones.Wall(at, new GridPos(at.X, at.Y - 1), up: true);
        Assert.Equal(Compass.South, world.DoorAt(hut).Faces);
        world.DoorAt(hut);
        Assert.Equal(before + 2, world.DoorsWorkedOut);
    }

    private static IEnumerable<(string Name, Footprint Shape, Door Door)> Doored(SimWorld world)
    {
        foreach (Workplace place in world.Workplaces)
        {
            if (world.DoorOf(place) is Door door)
            {
                yield return (place.Name, place.Footprint, door);
            }
        }

        foreach (StoreBuilding store in world.StoreBuildings)
        {
            if (world.DoorOf(store) is Door door)
            {
                yield return (store.Name, store.Footprint, door);
            }
        }

        foreach (Household household in world.Households)
        {
            if (world.DoorOf(household) is Door door)
            {
                yield return ($"the {household.Name} house", world.HomeFootprintOf(household)!.Value, door);
            }
        }
    }

    private static GridPos Step(GridPos tile, Compass way) => way switch
    {
        Compass.North => new GridPos(tile.X, tile.Y - 1),
        Compass.East => new GridPos(tile.X + 1, tile.Y),
        Compass.South => new GridPos(tile.X, tile.Y + 1),
        _ => new GridPos(tile.X - 1, tile.Y),
    };

    /// <summary>A shape of this kind centred on a tile, its even extents shifted half a tile as turned — so it covers its own count of tiles.</summary>
    /// <summary>
    /// ⭐ The ghost's door (commit 5, `doors.md §12.1`): a proposal's door is the door the building has once it is marked
    /// there — on open ground and against water — and a kind with no door has none.
    /// </summary>
    [Fact]
    public void AProposalsDoorIsTheDoorItHasOnceMarked()
    {
        SimWorld world = World();
        (GridPos water, Compass toward) = AShore(world);
        GridPos shore = Step(water, (Compass)(((int)toward + 2) & 3));
        var cases = new List<(GridPos At, Angle Facing)>
        {
            (OpenGround(world), Angle.Zero),
            (shore, Angle.FromTurnFraction((int)toward, 4)),
        };

        foreach ((GridPos at, Angle facing) in cases)
        {
            Footprint shape = Pose(world, BuildingKind.GathererHut, at, facing);
            Door? asked = world.DoorOfAProposal(BuildingKind.GathererHut, shape);
            Assert.True(world.Mark(BuildingKind.GathererHut, shape.Origin, shape.Facing).Allowed);
            Workplace site = world.Workplaces.Single(w => w.IsSite && w.Footprint == shape);
            _output.WriteLine($"a hut at {at} facing {facing.Raw}: asked {asked}, marked {world.DoorOf(site)}");
            Assert.NotNull(asked);
            Assert.Equal(world.DoorOf(site), asked);
        }

        Footprint open = Pose(world, BuildingKind.Well, OpenGround(world), Angle.Zero);
        Assert.Null(world.DoorOfAProposal(BuildingKind.Well, open));
        Assert.Null(world.DoorOfAProposal(BuildingKind.Farmhouse, Pose(world, BuildingKind.Farmhouse, OpenGround(world), Angle.Zero)));
    }

    private static Footprint Pose(SimWorld world, BuildingKind kind, GridPos at, Angle facing)
    {
        (int width, int height) = world.ExtentOf(kind);
        bool sideways = SimWorld.FrontOf(facing) is Compass.East or Compass.West;
        int across = sideways ? height : width;
        int deep = sideways ? width : height;
        Point centre = Point.CentreOf(at);
        Point origin = new(
            across % 2 == 0 ? centre.X - Fixed.FromRatio(1, 2) : centre.X,
            deep % 2 == 0 ? centre.Y - Fixed.FromRatio(1, 2) : centre.Y);
        Footprint shape = world.FootprintOf(kind, origin, facing);
        Assert.Equal(width * height, shape.CoveredTiles().Count);
        return shape;
    }

    /// <summary>A tile with a 9 × 9 square of free, unwalled ground round it that the village reaches, nearest the founding site.</summary>
    private static GridPos OpenGround(SimWorld world)
    {
        GridPos from = world.FirstHomeOrFoundingSite();
        GridPos site = world.Map.FoundingSite;
        for (int r = 6; r < 40; r++)
        {
            for (int dy = -r; dy <= r; dy++)
            {
                for (int dx = -r; dx <= r; dx++)
                {
                    var at = new GridPos(site.X + dx, site.Y + dy);
                    if (Math.Max(Math.Abs(dx), Math.Abs(dy)) == r && Clear(at, 4))
                    {
                        return at;
                    }
                }
            }
        }

        throw new InvalidOperationException("no open ground near the founding site");

        bool Clear(GridPos at, int half)
        {
            for (int y = at.Y - half; y <= at.Y + half; y++)
            {
                for (int x = at.X - half; x <= at.X + half; x++)
                {
                    var tile = new GridPos(x, y);
                    // No wall on any of 81 tiles keeps a yard out: every yard is fenced.
                    if (!world.IsWalkable(tile) || world.StandsOn(tile) || world.WallsOn(tile) != 0
                        || !world.TravelCost.CanReach(from, tile))
                    {
                        return false;
                    }
                }
            }

            return true;
        }
    }

    /// <summary>
    /// A water tile and the way from the land to it, with a 3 × 2 patch of free, reached land on the landward side,
    /// nearest the founding site.
    /// </summary>
    private static (GridPos Water, Compass Toward) AShore(SimWorld world)
    {
        GridPos from = world.FirstHomeOrFoundingSite();
        GridPos site = world.Map.FoundingSite;
        for (int r = 1; r < 80; r++)
        {
            for (int dy = -r; dy <= r; dy++)
            {
                for (int dx = -r; dx <= r; dx++)
                {
                    var water = new GridPos(site.X + dx, site.Y + dy);
                    if (Math.Max(Math.Abs(dx), Math.Abs(dy)) != r || !world.Map.Contains(water) || world.IsWalkable(water))
                    {
                        continue;
                    }

                    foreach (Compass toward in new[] { Compass.North, Compass.East, Compass.South, Compass.West })
                    {
                        var back = (Compass)(((int)toward + 2) & 3);
                        var across = (Compass)(((int)toward + 1) & 3);
                        bool land = true;
                        GridPos row = water;
                        for (int up = 1; up <= 2; up++)
                        {
                            row = Step(row, back);
                            foreach (GridPos tile in new[] { row, Step(row, across), Step(row, (Compass)(((int)across + 2) & 3)) })
                            {
                                land &= world.IsWalkable(tile) && !world.StandsOn(tile) && world.WallsOn(tile) == 0
                                    && world.TravelCost.CanReach(from, tile);
                            }
                        }

                        if (land)
                        {
                            return (water, toward);
                        }
                    }
                }
            }
        }

        throw new InvalidOperationException("no shore near the founding site");
    }
}

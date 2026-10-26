using Bclone.Sim.Config;
using Bclone.Sim.Core;
using Bclone.Sim.Logging;
using Bclone.Sim.World;
using Xunit;
using Xunit.Abstractions;

namespace Bclone.Sim.Tests;

/// <summary>
/// Walkers go in and out through the door (D622–D625, `specs/doors.md` §4.4–§4.7): the route, the leg, the cost the
/// catchment reads, the facings nobody chose, and the buildings that have no door.
/// </summary>
public sealed class DoorWalkTests
{
    private readonly ITestOutputHelper _output;

    public DoorWalkTests(ITestOutputHelper output) => _output = output;

    private static SimWorld Warm() => SimFactory.CreatePhase0(VillageFixtures.Village, new InMemoryLogSink()).World;

    /// <summary>
    /// ⭐⭐ No step anybody takes crosses a doored building's edge but at its door (§4.4) — every tile step of a played
    /// village over five years, into a building or out of it.
    /// </summary>
    /// <remarks>
    /// The leg is judged by the same question (<see cref="LineOfSight.ADoorBars"/>), and this asks it of what was walked,
    /// so a pulled string that cut a corner through a wall the route went round would show here.
    /// </remarks>
    [Fact]
    public void NoStepEverCrossesABuildingsEdgeButAtItsDoor()
    {
        SimConfig config = VillageFixtures.Village;
        SimLoop loop = SimFactory.CreatePhase0(config, new InMemoryLogSink());
        SimWorld world = loop.World;
        ColdStartTests.PlayTheOpening(world);

        var was = new Dictionary<Villager, (Point Position, Point LegFrom)>();
        long steps = 0, throughADoor = 0;
        for (int tick = 0; tick < config.TicksPerYear * 5; tick++)
        {
            was.Clear();
            foreach (Villager villager in world.Villagers)
            {
                if (villager.Alive)
                {
                    was[villager] = (villager.Position, villager.LegFrom);
                }
            }

            loop.StepOnce();
            foreach ((Villager villager, (Point from, Point legFrom)) in was)
            {
                if (!villager.Alive || from == villager.Position)
                {
                    continue;
                }

                if (villager.LegFrom != legFrom)
                {
                    Check(villager, from, villager.LegFrom);
                    Check(villager, villager.LegFrom, villager.Position);
                }
                else
                {
                    Check(villager, from, villager.Position);
                }
            }
        }

        _output.WriteLine($"{steps} tile steps in five years, {throughADoor} of them through a door, none across a wall of a building.");
        Assert.True(throughADoor > 100, $"only {throughADoor} steps through a door — the guard proved nothing");

        void Check(Villager villager, Point a, Point b)
        {
            List<GridPos> tiles = LineOfSight.TilesCrossed(a, b);
            for (int i = 1; i < tiles.Count; i++)
            {
                GridPos s = tiles[i - 1], t = tiles[i];
                if (Math.Abs(s.X - t.X) + Math.Abs(s.Y - t.Y) != 1)
                {
                    continue;
                }

                steps++;
                Door? into = world.DoorCovering(t);
                Door? outOf = world.DoorCovering(s);
                if (into != outOf && (into is Door d && s == d.Doorstep || outOf is Door o && t == o.Doorstep))
                {
                    throughADoor++;
                }

                Assert.False(LineOfSight.ADoorBars(world, s, t),
                    $"Tick {world.Tick}: {villager.Name} ({villager.State}) stepped {s} → {t}, across a building's edge that is not its door.");
            }
        }
    }

    /// <summary>
    /// A route to a building goes in at its door, and a route out leaves by its doorstep (§4.4) — asked of a granary from
    /// the tile straight behind it, where the cheapest side would have been the back.
    /// </summary>
    [Fact]
    public void ARouteGoesInAtTheDoorAndOutByTheDoorstep()
    {
        SimWorld world = Warm();
        StoreBuilding granary = world.StoreBuildings.First(s => s.Kind == StoreKind.Granary);
        Door door = world.DoorOf(granary)!.Value;
        GridPos behind = ABehindTile(world, granary, door);

        List<GridPos> inward = world.TravelCost.RouteFrom(behind, granary.Tile);
        int entered = inward.FindIndex(t => granary.Footprint.Covers(t));
        _output.WriteLine($"{behind} → the granary at {granary.Tile} (door {door.Tile}, doorstep {door.Doorstep}): {string.Join(" ", inward)}");
        Assert.True(entered > 0, "the route never entered the granary");
        Assert.Equal(door.Doorstep, inward[entered - 1]);
        Assert.Equal(door.Tile, inward[entered]);

        List<GridPos> outward = world.TravelCost.RouteFrom(granary.Tile, behind);
        int left = outward.FindIndex(t => !granary.Footprint.Covers(t));
        Assert.True(left >= 0, "the route never left the granary");
        Assert.Equal(door.Doorstep, outward[left]);
    }

    /// <summary>
    /// ⭐ The cost the catchment reads is the walk through the door (§4.4): from straight behind a granary dearer than by any
    /// side, from its doorstep the same — <c>LabourAllocator</c> and every errand ask this one field.
    /// </summary>
    [Fact]
    public void AWalkFromBehindABuildingPaysForGoingRoundToItsDoor()
    {
        SimWorld world = Warm();
        StoreBuilding granary = world.StoreBuildings.First(s => s.Kind == StoreKind.Granary);
        Door door = world.DoorOf(granary)!.Value;
        GridPos behind = ABehindTile(world, granary, door);

        int fromBehind = world.TravelCost.Cost(behind, granary.Tile);
        int fromTheStep = world.TravelCost.Cost(door.Doorstep, granary.Tile);
        world.DoorsOnEverySide = true;
        world.TravelCost.Forget();
        int fromBehindAnySide = world.TravelCost.Cost(behind, granary.Tile);
        int fromTheStepAnySide = world.TravelCost.Cost(door.Doorstep, granary.Tile);
        world.DoorsOnEverySide = false;
        world.TravelCost.Forget();

        _output.WriteLine($"from behind {fromBehind} (any side {fromBehindAnySide}); from the doorstep {fromTheStep} (any side {fromTheStepAnySide})");
        Assert.True(fromBehind > fromBehindAnySide, "the walk from behind did not go round to the door");
        Assert.Equal(fromTheStepAnySide, fromTheStep);
    }

    /// <summary>The founding buildings face the founding site (§4.6, Joe's call 3) — a building that is not square, north or south.</summary>
    [Fact]
    public void TheFoundingBuildingsFaceTheFoundingSite()
    {
        SimWorld world = Warm();
        Point site = Point.CentreOf(world.Map.FoundingSite);
        var faced = new List<(string Name, Footprint Shape)>();
        faced.AddRange(world.Workplaces.Where(w => !w.IsSite && world.DoorOf(w) is not null).Select(w => (w.Name, w.Footprint)));
        faced.AddRange(world.StoreBuildings.Where(s => world.DoorOf(s) is not null).Select(s => (s.Name, s.Footprint)));
        Assert.True(faced.Count >= 4, $"only {faced.Count} founding buildings with doors");

        foreach ((string name, Footprint shape) in faced)
        {
            Compass front = SimWorld.FrontOf(shape.Facing);
            long dx = site.X.RawBits - shape.Origin.X.RawBits;
            long dy = site.Y.RawBits - shape.Origin.Y.RawBits;
            _output.WriteLine($"{name} at {shape.Origin} faces {front}, the site lies ({dx >> 32}, {dy >> 32}) off");
            Assert.Equal(Toward(dx, dy, shape.Width == shape.Height), front);
        }
    }

    /// <summary>A building marked with no facing opens toward the nearest store (§4.7, Joe's call 4) — the ghost's starting facing.</summary>
    [Fact]
    public void ABuildingMarkedWithNoFacingOpensTowardTheNearestStore()
    {
        SimWorld world = Warm();
        GridPos at = FarmFixtures.ClearGroundNear(world);
        Assert.True(world.Mark(BuildingKind.Granary, at).Allowed);
        Workplace site = world.Workplaces.Single(w => w.Construction?.Kind == BuildingKind.Granary);
        StoreBuilding nearest = world.StoreBuildings.OrderBy(s => site.Footprint.Origin.DistanceTo(s.Footprint.Origin)).First();
        Compass front = SimWorld.FrontOf(site.Facing);
        long dx = nearest.Footprint.Origin.X.RawBits - site.Footprint.Origin.X.RawBits;
        long dy = nearest.Footprint.Origin.Y.RawBits - site.Footprint.Origin.Y.RawBits;
        _output.WriteLine($"a granary at {at} faces {front}; the nearest store, {nearest.Name}, lies ({dx >> 32}, {dy >> 32}) off");
        Assert.Equal(Toward(dx, dy, square: true), front);
    }

    /// <summary>The way a front faces something (dx, dy) off — the larger distance decides, a tie north, east, south, west; not square, north or south.</summary>
    private static Compass Toward(long dx, long dy, bool square) =>
        !square || Math.Abs(dy) > Math.Abs(dx)
            ? (dy > 0 ? Compass.South : Compass.North)
            : Math.Abs(dx) > Math.Abs(dy)
                ? (dx > 0 ? Compass.East : Compass.West)
                : (dy < 0 || dx == 0 ? Compass.North : dx > 0 ? Compass.East : Compass.South);

    /// <summary>A farmhouse, a barn, the cart, a well and a pile have no door (§4.1, D624, D625) — entered from any side, as before doors.</summary>
    [Fact]
    public void TheBuildingsThatOpenOnEverySideHaveNoDoor()
    {
        SimWorld cold = SimFactory.CreatePhase0(ColdStartTests.ColdVillage, new InMemoryLogSink()).World;
        Assert.Null(cold.DoorOf(cold.TheCart!));

        SimWorld world = Warm();
        Workplace farm = FarmFixtures.RaiseAFarm(world);
        Assert.Null(world.DoorOf(farm));
        Workplace barn = HerdTests.RaiseABarn(world);
        Assert.Null(world.DoorOf(barn));
        Assert.Null(world.DoorOf(world.BarnStoreOf(barn)!));
        Assert.True(world.Mark(BuildingKind.Pile, FarmFixtures.ClearGroundNear(world)).Allowed);
        Assert.Null(world.DoorOf(world.StoreBuildings.First(s => s.Kind == StoreKind.Pile)));
        Assert.True(HasADoor(BuildingKind.Granary) && !HasADoor(BuildingKind.Well) && !HasADoor(BuildingKind.Farmhouse) && !HasADoor(BuildingKind.Barn));

        static bool HasADoor(BuildingKind kind) => SimWorld.HasADoor(kind);
    }

    /// <summary>
    /// ⭐ No door opens into a pocket beside the founding site (D623): the free-ground sweep is seeded beside the site, so a
    /// tile there reads as reached even sealed in; doors read the village's own piece. A hall stood on the site of a warm
    /// start faced its door into such a pocket, and 1 tile of 493 was reached from the site.
    /// </summary>
    [Fact]
    public void ADoorBesideTheFoundingSiteOpensOntoTheVillagesGround()
    {
        SimWorld world = SimFactory.CreatePhase0(ShippedConfig.Established(), new InMemoryLogSink()).World;
        GridPos site = world.Map.FoundingSite;
        world.TownHall = new TownHall { Position = Point.CentreOf(site), Name = "the hall", RaisedAtTick = 1 };
        world.StandingChanged();

        Door door = world.DoorOf(world.TownHall)!.Value;
        GridPos far = new(site.X + 12, site.Y + 6);
        _output.WriteLine($"the hall's door {door}: doorstep to {far} {world.TravelCost.Cost(door.Doorstep, far)}");
        Assert.False(door.Shut);
        Assert.NotEqual(TravelCostField.Unreachable, world.TravelCost.Cost(door.Doorstep, far));
        Assert.NotEqual(TravelCostField.Unreachable, world.TravelCost.Cost(site, far));
    }

    /// <summary>Hay goes out at the pasture tile nearest the barn (D623), never the pasture's first tile in map order.</summary>
    [Fact]
    public void HayGoesOutNearestTheBarn()
    {
        SimWorld world = SimFactory.CreatePhase0(ShippedConfig.Established(), new InMemoryLogSink()).World;
        Workplace barn = HerdTests.RaiseABarn(world);
        HerdTests.GivePasture(world, barn, 40);
        GridPos? out_ = world.WhereTheHayGoesOut(barn);
        // Of the pasture a herdsman can stand on — not ground painted under the barn itself.
        int nearest = world.Zones.WorkGroundOf(barn.Id).Select(world.Zones.PositionOf)
            .Where(t => !world.StandsOn(t) && world.Map.TerrainAt(t) is Terrain.Grass or Terrain.Forest or Terrain.Sapling)
            .Min(t => barn.Tile.ManhattanDistanceTo(t));
        _output.WriteLine($"hay out at {out_}, {barn.Tile.ManhattanDistanceTo(out_!.Value)} from the barn; the nearest pasture tile {nearest}");
        Assert.Equal(nearest, barn.Tile.ManhattanDistanceTo(out_.Value));
    }

    /// <summary>
    /// ⛔ JOE'S RULE: a trial house stood by the site chooser does not throw the door index away (D622) — the world after a
    /// trial is the world before it, and asked twice a candidate the doors were worked out again on every one.
    /// </summary>
    [Fact]
    public void ATrialHouseDoesNotWorkTheDoorsOutAgain()
    {
        SimWorld world = Warm();
        StoreBuilding granary = world.StoreBuildings.First(s => s.Kind == StoreKind.Granary);
        // The index a village has built by the time its chooser stands a trial: its first wall-off ask builds it.
        world.DoorCovering(granary.Tile);
        world.DoorOf(granary);
        int before = world.DoorsWorkedOut;

        GridPos front = FarmFixtures.ClearGroundNear(world);
        world.DetourOfAHouseAt(world.TheDailyWalks(), front, Angle.Zero, world.Households[0].Id);
        world.DoorOf(granary);
        Assert.Equal(before, world.DoorsWorkedOut);
    }

    /// <summary>The free tile straight behind a building — across its back, one tile out — that the village reaches.</summary>
    private static GridPos ABehindTile(SimWorld world, StoreBuilding building, Door door)
    {
        int ox = door.Doorstep.X - door.Tile.X, oy = door.Doorstep.Y - door.Tile.Y;
        GridPos from = world.FirstHomeOrFoundingSite();
        foreach (GridPos tile in building.Footprint.CoveredTiles())
        {
            var back = new GridPos(tile.X - ox, tile.Y - oy);
            if (!building.Footprint.Covers(back) && world.IsWalkable(back) && !world.StandsOn(back)
                && world.WallsOn(tile) == 0 && world.TravelCost.CanReach(from, back))
            {
                return back;
            }
        }

        throw new Xunit.Sdk.XunitException($"nothing free behind {building.Name}");
    }
}

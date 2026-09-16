using Bclone.Sim.Config;
using Bclone.Sim.Core;
using Bclone.Sim.Logging;
using Bclone.Sim.World;
using Xunit;
using Xunit.Abstractions;

namespace Bclone.Sim.Tests;

/// <summary>
/// Buildings are obstacles — villagers go round, not through (D383, `specs/buildings-as-obstacles.md`).
/// </summary>
/// <remarks>
/// Joe, 2026-09-16, with a screenshot of a trail through a warehouse: *"villagers should go around
/// buildings, not through them."* One rule in the one cost field: a tile a building stands on
/// cannot be walked through, only to.
/// </remarks>
public sealed class ObstacleTests
{
    private readonly ITestOutputHelper _output;

    public ObstacleTests(ITestOutputHelper output) => _output = output;

    private static SimWorld World() =>
        SimFactory.CreatePhase0(VillageFixtures.Village, new InMemoryLogSink()).World;

    /// <summary>
    /// ⛔⛔ A route goes round a building — <b>it touches none of its tiles, and it is longer than
    /// the straight line was</b>.
    /// </summary>
    /// <remarks>
    /// Posed with a stockpile — free, one tile, raised the tick it is marked — on the straight
    /// route between two clear tiles. Red with the cost field's obstacles switched off: the route
    /// runs straight through the pile, five tiles for five.
    /// </remarks>
    [Fact]
    public void ARouteGoesRoundABuildingNotThroughIt()
    {
        SimWorld world = World();
        (GridPos left, GridPos middle, GridPos right) = AClearRow(world);

        int before = world.TravelCost.Cost(left, right);
        List<GridPos> straight = world.TravelCost.RouteFrom(left, right);
        Assert.Contains(middle, straight);

        Assert.True(world.Mark(BuildingKind.Pile, middle).Allowed, "the pile did not stand");
        Assert.True(world.StandsOn(middle));

        int after = world.TravelCost.Cost(left, right);
        List<GridPos> round = world.TravelCost.RouteFrom(left, right);
        _output.WriteLine($"{left} → {right}: {before} straight through {middle}, {after} round it; route {string.Join(" ", round)}");

        Assert.DoesNotContain(middle, round);
        Assert.True(after > before, $"the route did not lengthen round the pile ({before} → {after})");
        Assert.NotEqual(TravelCostField.Unreachable, after);
    }

    /// <summary>A villager standing on a building steps off it — home to work is still a walk.</summary>
    [Fact]
    public void AVillagerAtHomeStillReachesWork()
    {
        SimWorld world = World();
        Household home = world.Households.Find(h => h.HasHome)!;
        Workplace hut = world.Workplaces.Find(w => w.Kind == JobKind.Forager)!;
        GridPos homeTile = home.HomePosition!.Value.ToTile();

        Assert.True(world.StandsOn(homeTile), "the home is not an obstacle — the index is stale");
        int cost = world.TravelCost.Cost(homeTile, hut.Tile);
        int back = world.TravelCost.Cost(hut.Tile, homeTile);
        _output.WriteLine($"home {homeTile} → hut {hut.Tile}: {cost}; back {back}");

        Assert.NotEqual(TravelCostField.Unreachable, cost);
        Assert.NotEqual(TravelCostField.Unreachable, back);
        Assert.True(cost > 0);

        // And the first step off the home is a free tile beside it, not the home itself.
        GridPos step = world.TravelCost.StepToward(homeTile, hut.Tile);
        Assert.NotEqual(homeTile, step);
        Assert.False(world.StandsOn(step), $"the first step {step} is onto a building");
    }

    /// <summary>
    /// ⛔ The last free tile beside a building cannot be built on — <b>"that would wall off"</b>.
    /// </summary>
    /// <remarks>
    /// A stockpile on clear ground, then stockpiles on three of its four neighbours; the fourth
    /// is refused in words. Red with the sweep off.
    /// </remarks>
    [Fact]
    public void TheLastFreeTileBesideABuildingIsRefused()
    {
        SimWorld world = World();
        (GridPos _, GridPos at, GridPos _) = AClearRow(world);
        Assert.True(world.Mark(BuildingKind.Pile, at).Allowed);

        GridPos[] beside = { new(at.X + 1, at.Y), new(at.X - 1, at.Y), new(at.X, at.Y + 1), new(at.X, at.Y - 1) };
        for (int i = 0; i < 3; i++)
        {
            PlacementVerdict verdict = world.Mark(BuildingKind.Pile, beside[i]);
            Assert.True(verdict.Allowed, $"a pile beside the pile at {beside[i]} was refused: {verdict.Reason}");
        }

        PlacementVerdict last = world.CanBuildAt(BuildingKind.Pile, beside[3]);
        _output.WriteLine($"the last free tile {beside[3]} beside the pile at {at}: {last.Allowed} — {last.Reason}");
        Assert.False(last.Allowed);
        Assert.Contains("wall off", last.Reason);
    }

    /// <summary>
    /// The occupancy index agrees with the buildings after fifty years of houses raised, sites
    /// finished and stores pulled down — <b>the guard for a missed <c>StandingChanged</c></b>.
    /// </summary>
    [Fact]
    public void TheOccupancyIndexAgreesWithTheShapes()
    {
        SimConfig config = VillageFixtures.Village;
        SimLoop loop = SimFactory.CreatePhase0(config, new InMemoryLogSink());
        SimWorld world = loop.World;

        for (int year = 1; year <= 50; year++)
        {
            loop.Step(config.TicksPerYear);
            if (year % 10 != 0)
            {
                continue;
            }

            if (year == 30)
            {
                // Pull something down and move something, so the index has to follow.
                StoreBuilding store = world.StoreBuildings.Find(s => s.Kind == StoreKind.Warehouse)!;
                world.MarkDemolition(store.Tile);
            }

            var expected = new HashSet<GridPos>();
            foreach (Household h in world.Households)
            {
                if (h.HomePosition is Point p) { expected.UnionWith(world.FootprintOf(BuildingKind.Home, p).CoveredTiles()); }
            }

            foreach (Workplace w in world.Workplaces) { expected.UnionWith(w.Footprint.CoveredTiles()); }
            foreach (StoreBuilding s in world.StoreBuildings) { expected.UnionWith(s.Footprint.CoveredTiles()); }
            foreach (Library l in world.Libraries) { expected.UnionWith(l.Footprint.CoveredTiles()); }
            if (world.TownHall is TownHall hall) { expected.UnionWith(hall.Footprint.CoveredTiles()); }

            int wrong = 0;
            for (int y = world.Map.MinY; y < world.Map.MinY + world.Map.Height; y++)
            {
                for (int x = world.Map.MinX; x < world.Map.MinX + world.Map.Width; x++)
                {
                    var tile = new GridPos(x, y);
                    if (world.StandsOn(tile) != expected.Contains(tile))
                    {
                        wrong++;
                    }
                }
            }

            _output.WriteLine($"year {year}: {expected.Count} standing tiles, {wrong} disagreements");
            Assert.True(wrong == 0, $"year {year}: the index disagrees with the buildings on {wrong} tiles");
        }
    }

    /// <summary>
    /// ⛔ The founding layout leaves lanes: <b>no two of its placed buildings touch, and the
    /// founding site keeps all four neighbours free</b>.
    /// </summary>
    /// <remarks>
    /// The D382 layout was a ring round the founding site — warehouse and woodcutter's hut to
    /// the west, builder's hut and granary to the north, market to the east — and with
    /// buildings as obstacles the founders' homes went into its pockets: a twelve-tile walk to
    /// a hut eight tiles away, and the fixture village died out. The huts in the woods are
    /// sited by the trees, not by an offset, so they are not asked; nor is the founders' home,
    /// which `ChooseSite` puts beside the founding site. Red with the market at its old (2, 1):
    /// it touches the granary, and stands on the founding site's east neighbour.
    /// </remarks>
    [Fact]
    public void TheFoundingLayoutLeavesLanes()
    {
        SimWorld world = World();
        GridPos site = world.Map.FoundingSite;
        var placed = new List<(string Name, List<GridPos> Tiles)>();
        foreach (Workplace place in world.Workplaces)
        {
            if (place.Kind is JobKind.Builder or JobKind.Woodcutter or JobKind.Marketer)
            {
                placed.Add((place.Name, place.Footprint.CoveredTiles()));
            }
        }

        foreach (StoreBuilding store in world.StoreBuildings)
        {
            if (store.Kind != StoreKind.Market)
            {
                placed.Add((store.Name, store.Footprint.CoveredTiles()));
            }
        }

        Assert.Equal(5, placed.Count);
        var touching = new List<string>();
        for (int a = 0; a < placed.Count; a++)
        {
            for (int b = a + 1; b < placed.Count; b++)
            {
                if (placed[a].Tiles.Any(t => placed[b].Tiles.Any(u => t.ManhattanDistanceTo(u) == 1)))
                {
                    touching.Add($"{placed[a].Name} touches {placed[b].Name}");
                }
            }
        }

        GridPos[] beside = { new(site.X + 1, site.Y), new(site.X - 1, site.Y), new(site.X, site.Y + 1), new(site.X, site.Y - 1) };
        // Free of the PLACED buildings — the founders' first home is sited beside the founding
        // site by `ChooseSite`, and that is its own question.
        var taken = beside.Where(t => placed.Any(p => p.Tiles.Contains(t))).ToList();
        _output.WriteLine($"{placed.Count} placed buildings; touching: {touching.Count}; founding-site neighbours under one: {taken.Count}");

        Assert.True(touching.Count == 0, string.Join("; ", touching));
        Assert.True(taken.Count == 0, $"the founding site's neighbours {string.Join(", ", taken)} are under a placed building");
    }

    /// <summary>
    /// ⛔ A hut sited by the trees does not wall in another — <b>seed 42 founds, and every
    /// workplace can be reached from the founding site</b>.
    /// </summary>
    /// <remarks>
    /// Seed 42's forager's hut stands on a spit with water on three sides; the forester's hut,
    /// ranked by the same trees, took the fourth. Nothing could reach the village's only food,
    /// no home could be sited, and the world threw at creation. Red with the wall-off question
    /// taken out of <c>WhereTheTreesAre</c>.
    /// </remarks>
    [Fact]
    public void AHutSitedByTheTreesDoesNotWallInAnother()
    {
        SimWorld world = SimFactory.CreatePhase0(VillageFixtures.Village, new InMemoryLogSink(), 42).World;
        var cutOff = new List<string>();
        foreach (Workplace place in world.Workplaces)
        {
            if (!world.TravelCost.CanReach(world.Map.FoundingSite, place.Tile))
            {
                cutOff.Add($"{place.Name} at {place.Tile}");
            }
        }

        _output.WriteLine($"seed 42: {world.Workplaces.Count} workplaces, {cutOff.Count} unreachable");
        Assert.True(cutOff.Count == 0, "Unreachable from the founding site: " + string.Join("; ", cutOff));
    }

    /// <summary>Three tiles in a row on clear, reachable ground, well away from the founding.</summary>
    private static (GridPos Left, GridPos Middle, GridPos Right) AClearRow(SimWorld world)
    {
        GridPos site = world.Map.FoundingSite;
        for (int radius = 6; radius < 30; radius++)
        {
            for (int dy = -radius; dy <= radius; dy++)
            {
                for (int dx = -radius; dx <= radius; dx++)
                {
                    var middle = new GridPos(site.X + dx, site.Y + dy);
                    var left = new GridPos(middle.X - 2, middle.Y);
                    var right = new GridPos(middle.X + 2, middle.Y);
                    bool clear = true;
                    for (int ox = -2; ox <= 2 && clear; ox++)
                    {
                        for (int oy = -2; oy <= 2 && clear; oy++)
                        {
                            var t = new GridPos(middle.X + ox, middle.Y + oy);
                            clear = world.Map.Contains(t) && world.Map.TerrainAt(t) == Terrain.Grass && !world.StandsOn(t);
                        }
                    }

                    if (clear && world.CanBuildAt(BuildingKind.Pile, middle).Allowed
                        && world.TravelCost.RouteFrom(left, right).Contains(middle))
                    {
                        return (left, middle, right);
                    }
                }
            }
        }

        throw new Xunit.Sdk.XunitException("No clear 5×5 patch of grass near the founding.");
    }
}

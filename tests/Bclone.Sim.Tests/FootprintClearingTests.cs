using Bclone.Sim.Config;
using Bclone.Sim.Core;
using Bclone.Sim.Logging;
using Bclone.Sim.World;
using Xunit;
using Xunit.Abstractions;

namespace Bclone.Sim.Tests;

/// <summary>
/// ⭐ Every tile a building covers is cleared before it is built, not only the one it is filed
/// under (B5, D497 — `specs/footprints.md §7`).
/// </summary>
/// <remarks>
/// D100 and D101 were written while every building was one tile and asked <c>Workplace.Tile</c>.
/// Measured in D496: 30 of 80 houses rose with a tree or rock on their other tile, still there
/// thirty years later. Each guard here poses the second tile wooded and the anchor clear — the
/// shape that let it through.
/// </remarks>
public sealed class FootprintClearingTests
{
    private readonly ITestOutputHelper _output;

    public FootprintClearingTests(ITestOutputHelper output) => _output = output;

    private static SimConfig Config => VillageFixtures.Village with { ExtraStoneSeams = 0, ExtraIronSeams = 0 };

    private static SimWorld World() => SimFactory.CreatePhase0(Config, new InMemoryLogSink()).World;

    /// <summary>Every given tile turned to wood.</summary>
    private static void Wooded(SimWorld world, IEnumerable<GridPos> tiles)
    {
        foreach (GridPos tile in tiles)
        {
            world.SetTerrain(tile, Terrain.Forest);
        }
    }

    /// <summary>Bare reachable ground well clear of the founding.</summary>
    private static GridPos BareGround(SimWorld world) =>
        OrganicHousingTests.ABareSquareAtLeast(world, world.Map.FoundingSite, 6, 3);

    private static Household ANewFamily(SimWorld world)
    {
        var family = new Household
        {
            Stockpile = world.NewStockpile(),
            Id = 900 + world.Households.Count,
            Surname = "Ashford",
        };
        world.Households.Add(family);
        return family;
    }

    private Workplace AHouseOverTwoTrees(SimWorld world, out List<GridPos> tiles)
    {
        GridPos front = BareGround(world);
        tiles = world.HomeFootprintAt(front, Angle.Zero).CoveredTiles();
        Wooded(world, tiles);
        Household family = ANewFamily(world);
        world.MarkHome(family.Id, new HomeSite(front, Angle.Zero, "posed"));
        Workplace site = world.Workplaces.Single(w => w.Construction is { ForHouseholdId: var id } && id == family.Id);
        _output.WriteLine($"house at {front}, covering {string.Join(" ", tiles)}; filed under {site.Tile}");
        return site;
    }

    /// <summary>⭐ A house marked over two trees asks for both to be cleared.</summary>
    /// <remarks>Red with <c>MarkHome</c> painting the front tile only (D100 as written).</remarks>
    [Fact]
    public void MarkingAHousePaintsBothItsTiles()
    {
        SimWorld world = World();
        AHouseOverTwoTrees(world, out List<GridPos> tiles);

        Assert.Equal(2, tiles.Count);
        foreach (GridPos tile in tiles)
        {
            Assert.True(world.Zones.IsHarvest(tile), $"{tile} under the house was not marked for clearing.");
        }
    }

    /// <summary>⛔ No work goes in while ANY tile of the footprint still stands (D101, every tile).</summary>
    /// <remarks>Red with the gates asking <c>GroundIsClearAt(site.Tile)</c>: the anchor is clear, so the site was buildable.</remarks>
    [Fact]
    public void NoWorkGoesInWhileAnyTileStands()
    {
        SimWorld world = World();
        Workplace site = AHouseOverTwoTrees(world, out List<GridPos> tiles);
        BuildFixtures.StockTheSite(site);

        world.Harvest(site.Tile);
        GridPos other = tiles.Single(t => t != site.Tile);
        _output.WriteLine($"anchor {site.Tile} {world.Map.TerrainAt(site.Tile)}, other {other} {world.Map.TerrainAt(other)}");

        Assert.False(world.FootprintIsClear(site));
        Assert.NotEqual(site.Id, world.NextBuildableSite()?.Id);

        world.Harvest(other);
        Assert.True(world.FootprintIsClear(site));
        Assert.Equal(site.Id, world.NextBuildableSite()?.Id);
    }

    /// <summary>⭐ A laborer is sent to the tile still standing, ahead of nearer painted wood.</summary>
    /// <remarks>
    /// A nearer painted tree sits beside the founding, so nearest-first would go there: only the
    /// footprint rule sends anyone to the house. Red with <c>NextFootprintToClear</c> asking the
    /// anchor only.
    /// </remarks>
    [Fact]
    public void ALaborerIsSentToTheTileStillStanding()
    {
        SimWorld world = World();
        GridPos from = world.Map.FoundingSite;
        Assert.Null(world.NearestHarvest(from));

        Workplace site = AHouseOverTwoTrees(world, out List<GridPos> tiles);
        world.Harvest(site.Tile);
        GridPos other = tiles.Single(t => t != site.Tile);
        world.PaintHarvest(other);

        GridPos near = OrganicHousingTests.ABareSquareAtLeast(world, from, 2, 0);
        world.SetTerrain(near, Terrain.Forest);
        world.PaintHarvest(near);
        _output.WriteLine($"nearer tree {near} ({world.TravelCost.Cost(from, near)}), house tile {other} ({world.TravelCost.Cost(from, other)})");
        Assert.True(world.TravelCost.Cost(from, near) < world.TravelCost.Cost(from, other));

        Assert.Equal(other, world.NearestHarvest(from));
    }

    /// <summary>⭐ A 2×2 marked over four trees asks for all four (`Mark`, the player's door).</summary>
    /// <remarks>Red with <c>Mark</c> painting the anchor only.</remarks>
    [Fact]
    public void AWiderBuildingMarkedOnWoodPaintsEveryTile()
    {
        SimWorld world = World();
        GridPos at = BareGround(world);
        List<GridPos> tiles = world.FootprintOf(BuildingKind.Granary, world.AnchorOn(BuildingKind.Granary, at)).CoveredTiles();
        Wooded(world, tiles);

        PlacementVerdict verdict = world.Mark(BuildingKind.Granary, at);
        _output.WriteLine($"granary at {at}: {verdict.Allowed} {verdict.Reason}; {string.Join(" ", tiles.Select(t => $"{t}={world.Zones.IsHarvest(t)}"))}");

        Assert.True(verdict.Allowed);
        Assert.Equal(4, tiles.Count);
        Assert.All(tiles, t => Assert.True(world.Zones.IsHarvest(t), $"{t} under the granary was not marked."));
    }

    /// <summary>⛔ A free building waits for its LAST tile, not its first.</summary>
    /// <remarks>
    /// The builder's hut is 2×1 and free, so it stands the moment its ground is clear (D96). Red
    /// with <c>RaiseAnythingWaitingOn</c> raising it when the anchor comes clear — and, before
    /// that, with the other tile's clearing raising nothing at all.
    /// </remarks>
    [Fact]
    public void AFreeBuildingWaitsForItsLastTile()
    {
        SimWorld world = World();
        GridPos at = BareGround(world);
        Point anchor = world.AnchorOn(BuildingKind.BuilderHut, at);
        List<GridPos> tiles = world.FootprintOf(BuildingKind.BuilderHut, anchor).CoveredTiles();
        Wooded(world, tiles);
        int huts = world.Workplaces.Count(w => w.Kind == JobKind.Builder && !w.IsSite);

        Assert.True(world.Mark(BuildingKind.BuilderHut, at).Allowed);
        Assert.Single(world.BuildingsWaitingOnTheGround);

        // The anchor first: still waiting on the other tile.
        world.Harvest(at);
        _output.WriteLine($"anchor {at} cleared: {world.BuildingsWaitingOnTheGround.Count} waiting");
        Assert.Single(world.BuildingsWaitingOnTheGround);
        Assert.Equal(huts, world.Workplaces.Count(w => w.Kind == JobKind.Builder && !w.IsSite));

        // Then the last: it stands.
        world.Harvest(tiles.Single(t => t != at));
        Assert.Empty(world.BuildingsWaitingOnTheGround);
        Assert.Equal(huts + 1, world.Workplaces.Count(w => w.Kind == JobKind.Builder && !w.IsSite));
    }

    /// <summary>
    /// ⭐ Played: a house over two trees whose marks were taken off is still raised on bare ground —
    /// the builder clears every tile of it (D138, every tile).
    /// </summary>
    /// <remarks>
    /// With the paint gone nobody else will come, so this is the builder's errand and the builder's
    /// gate. Red with the errand asking the anchor only: the house rises on the other tree.
    /// </remarks>
    [Fact]
    public void ABuilderClearsEveryTileOfTheirSite()
    {
        SimLoop loop = SimFactory.CreatePhase0(Config, new InMemoryLogSink());
        SimWorld world = loop.World;
        Workplace site = AHouseOverTwoTrees(world, out List<GridPos> tiles);
        int family = site.Construction!.ForHouseholdId;
        foreach (GridPos tile in tiles)
        {
            world.EraseHarvest(tile);
        }

        BuildFixtures.StockTheSite(site);
        Household home = world.Households.Single(h => h.Id == family);
        string atRaise = "";
        for (int t = 0; t < Config.TicksPerYear * 2 && atRaise.Length == 0; t++)
        {
            loop.StepOnce();
            if (home.HomePosition is not null)
            {
                atRaise = string.Join(" ", tiles.Select(x => world.Map.TerrainAt(x).ToString()));
                _output.WriteLine($"raised at t{t} on {atRaise}");
            }
        }

        Assert.NotEqual("", atRaise);
        Assert.All(tiles, x => Assert.Equal(Terrain.Grass, world.Map.TerrainAt(x)));
    }

    /// <summary>⭐ A building being moved asks for the ground it is moving onto.</summary>
    /// <remarks>Red with <c>MarkRelocation</c> painting nothing (it relied on D138's builder alone).</remarks>
    [Fact]
    public void AMovedBuildingAsksForItsNewGround()
    {
        SimWorld world = World();
        Workplace hut = world.Workplaces.First(w => w.Kind == JobKind.Woodcutter && !w.IsSite);
        GridPos at = BareGround(world);
        Point to = world.AnchorOn(BuildingKind.WoodcutterHut, at);
        List<GridPos> tiles = world.FootprintOf(BuildingKind.WoodcutterHut, to).CoveredTiles();
        Wooded(world, tiles);

        PlacementVerdict verdict = world.MarkRelocation(hut.Tile, to);
        _output.WriteLine($"moving the woodcutter's hut {hut.Tile} → {at}: {verdict.Allowed} {verdict.Reason}");

        Assert.True(verdict.Allowed);
        Assert.Equal(2, tiles.Count);
        Assert.All(tiles, t => Assert.True(world.Zones.IsHarvest(t), $"{t} under the moved hut was not marked."));
    }
}

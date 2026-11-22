using Kith.Sim.Config;
using Kith.Sim.Core;
using Kith.Sim.Logging;
using Kith.Sim.World;
using Xunit;
using Xunit.Abstractions;

namespace Kith.Sim.Tests;

/// <summary>
/// ⭐ Nothing is built on steep ground (`heights.md §13`, D814; Joe, 2026-11-21: <i>"Shouldnt be able to build buildings or
/// put things like farm fields or livestock fields on the edge/peak of a hill/mountain - right or wrong?"</i> — the art
/// lane: right — Joe: <i>"Yes, as proposed"</i>). A building, a house plot, a field or a pasture refuses a steep tile in one
/// sentence; gentle ground takes them; the woods, a quarry and a mine are worked on any slope; what stands stays.
/// </summary>
public sealed class SteepGroundTests
{
    private readonly ITestOutputHelper _output;

    public SteepGroundTests(ITestOutputHelper output) => _output = output;

    /// <summary>The shipped valley with its hills raised, so steep ground is easy to find (HeightsTests' pose).</summary>
    private static SimConfig Hilly => ShippedConfig.Load() with { HillScalePercent = 200 };

    [Fact]
    public void TheSwitchIsOffInCodeAndOnInTheData()
    {
        Assert.False(new SimConfig().SteepRefusesBuilding);
        Assert.True(ShippedConfig.Load().SteepRefusesBuilding);
    }

    [Fact]
    public void ABuildingRefusesSteepGroundSaysWhyAndTakesGentle()
    {
        SimWorld on = SimFactory.CreatePhase0(Hilly, new InMemoryLogSink()).World;
        SimWorld off = SimFactory.CreatePhase0(Hilly with { SteepRefusesBuilding = false }, new InMemoryLogSink()).World;

        // A steep tile the same valley without the rule would take a pile on: the refusal is the rule's and nothing else.
        GridPos steep = Open(on, Slope.Steep).First(t => off.CanBuildAt(BuildingKind.Pile, t).Allowed);
        PlacementVerdict refused = on.CanBuildAt(BuildingKind.Pile, steep);
        Assert.False(refused.Allowed);
        Assert.Equal(SimWorld.TooSteepSentence, refused.Reason);

        // A building carried there (a move) is a new placement there.
        Assert.Equal(SimWorld.TooSteepSentence, on.CanBuildAt(BuildingKind.Pile, steep, alreadyStanding: true).Reason);

        // Gentle ground takes it.
        GridPos gentle = Open(on, Slope.Gentle).First(t => off.CanBuildAt(BuildingKind.Pile, t).Allowed);
        Assert.True(on.CanBuildAt(BuildingKind.Pile, gentle).Allowed, $"gentle {gentle} refused: {on.CanBuildAt(BuildingKind.Pile, gentle).Reason}");
        _output.WriteLine($"steep {steep} refused; gentle {gentle} taken");
    }

    [Fact]
    public void AHouseIsNeitherPaintedNorRaisedOnSteepGround()
    {
        SimWorld on = SimFactory.CreatePhase0(Hilly, new InMemoryLogSink()).World;
        SimWorld off = SimFactory.CreatePhase0(Hilly with { SteepRefusesBuilding = false }, new InMemoryLogSink()).World;
        List<GridPos> steep = Open(on, Slope.Steep);

        GridPos painted = steep.First(t => off.CanPaintResidential(t).Allowed);
        Assert.Equal(SimWorld.TooSteepSentence, on.CanPaintResidential(painted).Reason);
        GridPos gentle = Open(on, Slope.Gentle).First(t => off.CanPaintResidential(t).Allowed);
        Assert.True(on.CanPaintResidential(gentle).Allowed);

        // ⭐ Paint already there — an old save's — never raises a house on its steep tiles: the chooser refuses the plot.
        int posed = 0;
        foreach (GridPos front in steep)
        {
            PaintAround(on, front);
            PaintAround(off, front);
            PlotShape plot = on.PlotFor(front, Angle.Zero, 1);
            if (Household.TilesClippedOff(off, off.PlotFor(front, Angle.Zero, 1)) < 0)
            {
                continue;
            }

            Assert.Equal(-1, Household.TilesClippedOff(on, plot));
            posed++;
            if (posed == 3)
            {
                break;
            }
        }

        Assert.True(posed > 0, "No steep tile took a plot without the rule, so nothing was posed.");
    }

    [Fact]
    public void AFieldAndAPastureRefuseSteepGroundAndTheWoodsDoNot()
    {
        SimWorld on = SimFactory.CreatePhase0(Hilly, new InMemoryLogSink()).World;
        SimWorld off = SimFactory.CreatePhase0(Hilly with { SteepRefusesBuilding = false }, new InMemoryLogSink()).World;
        Workplace farm = Posed(on, JobKind.Farmer, 9101);
        Workplace barn = Posed(on, JobKind.Herdsman, 9102);
        Workplace forester = Posed(on, JobKind.Forester, 9103);

        GridPos steep = Open(on, Slope.Steep).First(t =>
            off.CanPaintWorkGround(farm, t).Allowed && off.CanPaintWorkGround(barn, t).Allowed && off.CanPaintWorkGround(forester, t).Allowed);
        Assert.Equal(SimWorld.TooSteepSentence, on.CanPaintWorkGround(farm, steep).Reason);
        Assert.Equal(SimWorld.TooSteepSentence, on.CanPaintWorkGround(barn, steep).Reason);
        Assert.True(on.CanPaintWorkGround(forester, steep).Allowed, "the woods are worked on any slope");
    }

    /// <summary>
    /// ⭐ A quarry and a mine stand on steep ground (D832; Joe, 2026-11-21, after a quarry beside his stone refused every tile
    /// in reach: <i>"yes, let quarries and mines stand on steep ground"</i>). Seams sit on the hills, so the ground around
    /// them is steep; a building whose trade works a face is exempt, decided by the face, never by a list of kinds. A pile
    /// on the same steep tile is still refused — the rule holds for everything else.
    /// </summary>
    [Theory]
    [InlineData(BuildingKind.Quarry)]
    [InlineData(BuildingKind.Mine)]
    public void AQuarryAndAMineStandOnSteepGroundBesideTheirSeam(BuildingKind kind)
    {
        SimWorld on = SimFactory.CreatePhase0(Hilly, new InMemoryLogSink()).World;
        SimWorld off = SimFactory.CreatePhase0(Hilly with { SteepRefusesBuilding = false }, new InMemoryLogSink()).World;

        // Steep tiles the rule-free valley takes this building on (so its seam is in reach) and a pile on too.
        List<GridPos> steep = Open(on, Slope.Steep)
            .Where(t => off.CanBuildAt(kind, t).Allowed && off.CanBuildAt(BuildingKind.Pile, t).Allowed)
            .ToList();
        Assert.True(steep.Count > 0, $"no steep tile in the hilly valley takes a {kind} without the rule, so nothing was posed");

        foreach (GridPos tile in steep)
        {
            PlacementVerdict verdict = on.CanBuildAt(kind, tile);
            Assert.True(verdict.Allowed, $"{kind} on steep {tile} refused: {verdict.Reason}");
            Assert.Equal(SimWorld.TooSteepSentence, on.CanBuildAt(BuildingKind.Pile, tile).Reason);
        }

        _output.WriteLine($"{kind}: {steep.Count} steep tiles beside its seam taken, a pile refused on each");
    }

    [Fact]
    public void TheFoundingNeverStandsOnSteepGround()
    {
        int seeds = 0;
        for (ulong seed = 1; seed <= 12; seed++)
        {
            SimWorld world = SimFactory.CreatePhase0(ShippedConfig.Load(), new InMemoryLogSink(), seed).World;
            GeneratedMap map = world.Map;
            for (int y = map.MinY; y < map.MinY + map.Height; y++)
            {
                for (int x = map.MinX; x < map.MinX + map.Width; x++)
                {
                    var tile = new GridPos(x, y);
                    Assert.False(world.StandsOn(tile) && world.TooSteepToBuild(tile), $"seed {seed}: the founding stands on steep {tile}");
                }
            }

            seeds++;
        }

        Assert.Equal(12, seeds);
    }

    /// <summary>Open land of this slope class — walkable, nothing standing, not water, not a seam — in sweep order.</summary>
    private static List<GridPos> Open(SimWorld world, Slope slope)
    {
        var found = new List<GridPos>();
        GeneratedMap map = world.Map;
        for (int y = map.MinY; y < map.MinY + map.Height; y++)
        {
            for (int x = map.MinX; x < map.MinX + map.Width; x++)
            {
                var tile = new GridPos(x, y);
                Terrain ground = map.TerrainAt(tile);
                if (ground != Terrain.Water && !TerrainRules.IsSeam(ground) && world.IsWalkable(tile) && !world.StandsOn(tile)
                    && world.SlopeAt(tile) == slope)
                {
                    found.Add(tile);
                }
            }
        }

        Assert.NotEmpty(found);
        return found;
    }

    private static void PaintAround(SimWorld world, GridPos centre)
    {
        for (int dy = -3; dy <= 3; dy++)
        {
            for (int dx = -3; dx <= 3; dx++)
            {
                var tile = new GridPos(centre.X + dx, centre.Y + dy);
                if (world.Map.Contains(tile))
                {
                    world.Zones.SetResidential(tile, true);
                }
            }
        }
    }

    private static Workplace Posed(SimWorld world, JobKind kind, int id) => new()
    {
        Store = new Stockpile(world.GoodsCatalog.Count),
        Id = id,
        Kind = kind,
        Name = $"posed {kind}",
        Position = Point.CentreOf(world.Map.FoundingSite),
        Capacity = 1,
    };
}

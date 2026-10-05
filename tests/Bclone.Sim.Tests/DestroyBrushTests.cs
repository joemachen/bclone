using Bclone.Sim.Config;
using Bclone.Sim.Core;
using Bclone.Sim.Determinism;
using Bclone.Sim.Logging;
using Bclone.Sim.World;
using Xunit;

namespace Bclone.Sim.Tests;

/// <summary>
/// The destroy brush, in the sim — <c>specs/destroy-brush.md §3.1–§3.2</c> (B4 slice 2, D493).
/// </summary>
/// <remarks>
/// Joe's answers: everything painted is destroyed and the goods are lost; destroyed ground grows
/// trees again only if a forester replants it; the village does the work (D491, Q1). Each guard
/// poses its own tiles near the founding, so it asserts the rule and not the valley's woods.
/// </remarks>
public sealed class DestroyBrushTests
{
    private static SimConfig Config => VillageFixtures.Village;

    private readonly HashSet<GridPos> _used = new();

    private static SimLoop AVillage() => SimFactory.CreatePhase0(Config, new InMemoryLogSink());

    /// <summary>Open, unowned tiles near the founding — the first <paramref name="count"/> after <paramref name="skip"/> — posed as <paramref name="terrain"/>.</summary>
    private List<GridPos> Pose(SimWorld world, Terrain terrain, int count, int skip = 0)
    {
        var posed = new List<GridPos>();
        int seen = 0;
        GridPos centre = world.Map.FoundingSite;
        for (int r = 3; r <= 12 && posed.Count < count; r++)
        {
            for (int dy = -r; dy <= r && posed.Count < count; dy++)
            {
                for (int dx = -r; dx <= r && posed.Count < count; dx++)
                {
                    if (Math.Max(Math.Abs(dx), Math.Abs(dy)) != r)
                    {
                        continue;
                    }

                    var at = new GridPos(centre.X + dx, centre.Y + dy);
                    if (!world.Map.Contains(at) || world.SomethingStandsAt(at) || world.Map.TerrainAt(at) == Terrain.Water
                        || world.Zones.WorkGroundOwner(at) != 0 || world.Zones.IsResidential(at) || world.Zones.PlotOwner(at) != 0
                        || world.Zones.IsHarvest(at) || _used.Contains(at) || seen++ < skip)
                    {
                        continue;
                    }

                    world.SetTerrain(at, terrain);
                    posed.Add(at);
                    _used.Add(at);
                }
            }
        }

        Assert.Equal(count, posed.Count);
        return posed;
    }

    private static GridPos[] Beside(GridPos at) =>
        new[] { new GridPos(at.X + 1, at.Y), new GridPos(at.X - 1, at.Y), new GridPos(at.X, at.Y + 1), new GridPos(at.X, at.Y - 1) };

    [Fact]
    public void TheBrushTakesWoodSaplingsAndSeamsAndNothingElse()
    {
        SimWorld world = AVillage().World;
        foreach (Terrain kind in new[] { Terrain.Forest, Terrain.Sapling, Terrain.Rock, Terrain.IronDeposit })
        {
            Assert.True(world.CanPaintDestroy(Pose(world, kind, 1)[0]).Allowed, $"{kind} refused the destroy brush.");
        }

        foreach (Terrain kind in new[] { Terrain.Grass, Terrain.Water, Terrain.Field })
        {
            PlacementVerdict verdict = world.CanPaintDestroy(Pose(world, kind, 1)[0]);
            Assert.False(verdict.Allowed, $"{kind} took the destroy brush.");
            Assert.Equal("There is nothing there to destroy.", verdict.Reason);
        }

        Workplace standing = world.Workplaces.First(w => !w.IsSite);
        Assert.Equal("Something stands there — demolish it first.", world.CanPaintDestroy(standing.Tile).Reason);
    }

    [Fact]
    public void RedOverridesOrangeAndOrangeDoesNotPaintOverRed()
    {
        SimWorld world = AVillage().World;
        GridPos wood = Pose(world, Terrain.Forest, 1)[0];

        Assert.True(world.PaintHarvest(wood).Allowed);
        Assert.True(world.PaintDestroy(wood).Allowed);
        Assert.False(world.Zones.IsHarvest(wood));
        Assert.True(world.Zones.IsDestroy(wood));

        PlacementVerdict orange = world.PaintHarvest(wood);
        Assert.False(orange.Allowed);
        Assert.True(world.Zones.IsDestroy(wood));
    }

    [Fact]
    public void TheVillageDestroysWhatIsMarkedAndKeepsNothing()
    {
        SimLoop loop = AVillage();
        SimWorld world = loop.World;
        var marked = new List<GridPos>();
        foreach (Terrain kind in new[] { Terrain.Forest, Terrain.Sapling, Terrain.Rock, Terrain.IronDeposit })
        {
            GridPos at = Pose(world, kind, 1)[0];
            Assert.True(world.PaintDestroy(at).Allowed);
            marked.Add(at);
        }

        int stone = world.StoneEverDug;
        int iron = world.IronEverDug;
        loop.Step(Config.TicksPerYear * 2);

        foreach (GridPos at in marked)
        {
            Assert.Equal(Terrain.Grass, world.Map.TerrainAt(at));
            Assert.True(world.Map.IsLaidBare(at), $"{at} is not laid bare.");
            Assert.False(world.Zones.IsDestroy(at));
            Assert.Empty(world.GroundStacksAt(at));
        }

        Assert.Equal(0, world.Zones.DestroyTiles);
        Assert.Equal(stone, world.StoneEverDug);
        Assert.Equal(iron, world.IronEverDug);
    }

    [Fact]
    public void LaidBareGroundBesideAWoodNeverSeeds()
    {
        SimLoop loop = AVillage();
        SimWorld world = loop.World;
        GridPos bare = Pose(world, Terrain.Forest, 1)[0];
        GridPos control = Pose(world, Terrain.Forest, 1, skip: 8)[0];

        // Wood on every open side of both, so each has a tree to seed from.
        foreach (GridPos tile in new[] { bare, control })
        {
            foreach (GridPos side in Beside(tile))
            {
                if (world.Map.Contains(side) && !world.SomethingStandsAt(side) && world.Map.TerrainAt(side) != Terrain.Water
                    && world.Zones.WorkGroundOwner(side) == 0 && !world.Zones.IsResidential(side) && !_used.Contains(side))
                {
                    world.SetTerrain(side, Terrain.Forest);
                    _used.Add(side);
                }
            }
        }

        Assert.True(world.Destroy(bare));
        world.SetTerrain(control, Terrain.Grass);

        loop.Step(Config.RegrowthPeriodDays * Config.TicksPerDay * 3);

        // ⭐ The premise, asserted: the same pose not laid bare DOES grow back.
        Assert.NotEqual(Terrain.Grass, world.Map.TerrainAt(control));
        Assert.Equal(Terrain.Grass, world.Map.TerrainAt(bare));
    }

    [Fact]
    public void AForesterPlantingLaidBareGroundBringsTheWoodBack()
    {
        SimLoop loop = AVillage();
        SimWorld world = loop.World;
        GridPos at = Pose(world, Terrain.Forest, 1)[0];
        Assert.True(world.Destroy(at));
        Assert.True(world.Map.IsLaidBare(at));

        Assert.True(world.Plant(at));
        Assert.False(world.Map.IsLaidBare(at));

        loop.Step(Config.RegrowthPeriodDays * Config.TicksPerDay * 3);
        Assert.Equal(Terrain.Forest, world.Map.TerrainAt(at));
    }

    [Fact]
    public void AMarkToDestroyComesBeforeNearerHarvestPaint()
    {
        SimWorld world = AVillage().World;
        GridPos red = Pose(world, Terrain.Forest, 1, skip: 20)[0];
        GridPos nearer = Pose(world, Terrain.Forest, 1)[0];
        Assert.True(world.PaintHarvest(nearer).Allowed);
        Assert.True(world.PaintDestroy(red).Allowed);

        // Every site stocked, so wood a building waits on (D215) is not what is measured.
        foreach (Workplace site in world.Workplaces.Where(w => w.IsSite))
        {
            BuildFixtures.StockTheSite(site);
        }

        Assert.Equal(red, world.NearestHarvest(nearer, out _));
    }

    [Fact]
    public void AMarkRetiresWhenItsGroundIsClearedAnotherWay()
    {
        SimWorld world = AVillage().World;
        GridPos rock = Pose(world, Terrain.Rock, 1)[0];
        world.PaintDestroy(rock);
        Assert.Equal(1, world.Zones.DestroyTiles);

        world.Harvest(rock);
        Assert.False(world.Zones.IsDestroy(rock));
        Assert.Equal(0, world.Zones.DestroyTiles);
        Assert.False(world.Map.IsLaidBare(rock));
    }

    [Fact]
    public void TheMarksAndTheBareGroundAreInTheHash()
    {
        // Same seed, same strokes: same fingerprint, tick by tick.
        SimLoop a = AVillage();
        SimLoop b = AVillage();
        var aPose = new DestroyBrushTests();
        var bPose = new DestroyBrushTests();
        GridPos aTile = aPose.Pose(a.World, Terrain.Forest, 1)[0];
        GridPos bTile = bPose.Pose(b.World, Terrain.Forest, 1)[0];
        a.World.PaintDestroy(aTile);
        b.World.PaintDestroy(bTile);
        for (int t = 0; t < Config.TicksPerYear; t++)
        {
            a.Step(1);
            b.Step(1);
            Assert.Equal(StateHash.Compute(a.World), StateHash.Compute(b.World));
        }

        // A red mark and an orange mark on the same tile are different villages.
        SimLoop red = AVillage();
        SimLoop orange = AVillage();
        GridPos redTile = new DestroyBrushTests().Pose(red.World, Terrain.Forest, 1)[0];
        GridPos orangeTile = new DestroyBrushTests().Pose(orange.World, Terrain.Forest, 1)[0];
        red.World.PaintDestroy(redTile);
        orange.World.PaintHarvest(orangeTile);
        Assert.NotEqual(StateHash.Compute(red.World), StateHash.Compute(orange.World));

        // ⚠️ And a red mark against none at all — the first version of this guard scored zero with
        // the destroy layer left out of the hash, because the orange mark was the only one hashed.
        SimLoop unmarked = AVillage();
        new DestroyBrushTests().Pose(unmarked.World, Terrain.Forest, 1);
        Assert.NotEqual(StateHash.Compute(red.World), StateHash.Compute(unmarked.World));

        // Laid bare and not are different villages, though the terrain is grass in both.
        SimLoop bare = AVillage();
        SimLoop cleared = AVillage();
        GridPos bareTile = new DestroyBrushTests().Pose(bare.World, Terrain.Forest, 1)[0];
        GridPos clearedTile = new DestroyBrushTests().Pose(cleared.World, Terrain.Forest, 1)[0];
        bare.World.Destroy(bareTile);
        cleared.World.SetTerrain(clearedTile, Terrain.Grass);
        Assert.Equal(bare.World.Map.TerrainAt(bareTile), cleared.World.Map.TerrainAt(clearedTile));
        Assert.NotEqual(StateHash.Compute(bare.World), StateHash.Compute(cleared.World));
    }
}

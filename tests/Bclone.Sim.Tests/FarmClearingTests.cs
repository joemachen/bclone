using Bclone.Sim.Config;
using Bclone.Sim.Core;
using Bclone.Sim.Logging;
using Bclone.Sim.World;
using Xunit;

namespace Bclone.Sim.Tests;

/// <summary>
/// A farm painted over trees and seams — <c>specs/destroy-brush.md §3.3</c> (B4 slice 1, D491).
/// </summary>
/// <remarks>
/// Measured before it was built (D490): farm ground was accepted over forest, saplings, rock and
/// iron; only grass was ploughed; 11 of 12 trees still stood at year 3; every sapling was a tree
/// within a year. Each guard below poses its tiles outright round a farm raised outright
/// (<see cref="FarmFixtures"/>), so it asserts the rule and not the founding's woods.
/// </remarks>
public sealed class FarmClearingTests
{
    private static SimConfig Config => VillageFixtures.Village;

    private static (SimLoop Loop, Workplace Farm) AFarm()
    {
        SimLoop loop = SimFactory.CreatePhase0(Config, new InMemoryLogSink());
        Workplace farm = FarmFixtures.RaiseAFarm(loop.World);
        return (loop, farm);
    }

    /// <summary>Tiles already posed by this test, so a second pose never reuses one.</summary>
    private readonly HashSet<GridPos> _used = new();

    /// <summary>Tiles round the farm that it could be given if they were grass, posed as <paramref name="terrain"/>.</summary>
    private List<GridPos> Pose(SimWorld world, Workplace farm, Terrain terrain, int count, int skip = 0)
    {
        var posed = new List<GridPos>();
        int seen = 0;
        for (int r = 2; r <= 6 && posed.Count < count; r++)
        {
            for (int dy = -r; dy <= r && posed.Count < count; dy++)
            {
                for (int dx = -r; dx <= r && posed.Count < count; dx++)
                {
                    if (Math.Max(Math.Abs(dx), Math.Abs(dy)) != r)
                    {
                        continue;
                    }

                    var at = new GridPos(farm.Tile.X + dx, farm.Tile.Y + dy);
                    if (!world.Map.Contains(at) || world.SomethingStandsAt(at)
                        || world.Map.TerrainAt(at) == Terrain.Water || world.Zones.WorkGroundOwner(at) != 0
                        || world.Zones.IsHarvest(at) || world.Zones.IsResidential(at) || _used.Contains(at))
                    {
                        continue;
                    }

                    world.SetTerrain(at, Terrain.Grass);
                    if (!world.CanPaintWorkGround(farm, at).Allowed || seen++ < skip)
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

    [Fact]
    public void FarmGroundGoesRoundASeamAndTakesAWood()
    {
        (SimLoop loop, Workplace farm) = AFarm();
        SimWorld world = loop.World;

        GridPos rock = Pose(world, farm, Terrain.Rock, 1)[0];
        GridPos iron = Pose(world, farm, Terrain.IronDeposit, 1)[0];
        GridPos wood = Pose(world, farm, Terrain.Forest, 1)[0];
        GridPos sapling = Pose(world, farm, Terrain.Sapling, 1)[0];

        PlacementVerdict onRock = world.PaintWorkGround(farm, rock);
        PlacementVerdict onIron = world.PaintWorkGround(farm, iron);

        Assert.False(onRock.Allowed);
        Assert.Equal("Not on a seam — clear it first.", onRock.Reason);
        Assert.False(onIron.Allowed);
        Assert.Equal(0, world.Zones.WorkGroundOwner(rock));
        Assert.True(world.PaintWorkGround(farm, wood).Allowed);
        Assert.True(world.PaintWorkGround(farm, sapling).Allowed);

        // ⭐ Once dug, the seam is grass and the farm takes it.
        world.Harvest(rock);
        Assert.True(world.PaintWorkGround(farm, rock).Allowed);
    }

    [Fact]
    public void AFarmsTreeIsMarkedTheMomentItIsGivenAndUnmarkedWhenTakenBack()
    {
        (SimLoop loop, Workplace farm) = AFarm();
        SimWorld world = loop.World;
        GridPos wood = Pose(world, farm, Terrain.Forest, 1)[0];

        world.PaintWorkGround(farm, wood);
        Assert.True(world.Zones.IsHarvest(wood));
        Assert.Equal(1, world.TreesToClearOn(farm));

        world.EraseWorkGround(farm, wood);
        Assert.False(world.Zones.IsHarvest(wood));
        Assert.Equal(Terrain.Forest, world.Map.TerrainAt(wood));
    }

    [Fact]
    public void AFarmsTreesAreClearedByTheVillageWithNobodyPaintingThem()
    {
        (SimLoop loop, Workplace farm) = AFarm();
        SimWorld world = loop.World;
        List<GridPos> woods = Pose(world, farm, Terrain.Forest, 4);
        foreach (GridPos wood in woods)
        {
            world.PaintWorkGround(farm, wood);
        }

        int feltBefore = world.LogsEverFelled;

        // Two years: the fixture has four or five working hands, and clearing is what a hand does
        // with nothing else to do (measured: three of the four trees fell in the first year).
        loop.Step(Config.TicksPerYear * 2);

        foreach (GridPos wood in woods)
        {
            // Field at once — the stump is ploughed, and the mark went with the tree.
            Assert.True(world.Map.TerrainAt(wood) == Terrain.Field || SimWorld.IsStandingCrop(world.Map.TerrainAt(wood)),
                $"{wood} is {world.Map.TerrainAt(wood)} two years after it was given to the farm.");
            Assert.False(world.Zones.IsHarvest(wood));
        }

        Assert.Equal(0, world.TreesToClearOn(farm));
        Assert.True(world.LogsEverFelled >= feltBefore + (4 * world.GoodsCatalog.YieldPerTileOf(Goods.Logs)),
            $"The farm's four trees gave {world.LogsEverFelled - feltBefore} logs.");
    }

    [Fact]
    public void AFarmsTreesComeBeforeNearerPaintedWood()
    {
        (SimLoop loop, Workplace farm) = AFarm();
        SimWorld world = loop.World;
        GridPos field = Pose(world, farm, Terrain.Forest, 1, skip: 20)[0];
        GridPos nearer = Pose(world, farm, Terrain.Forest, 1)[0];

        world.PaintWorkGround(farm, field);
        Assert.True(world.PaintHarvest(nearer).Allowed);

        // ⚠️ Every site stocked first: wood a building waits on outranks the farm (D215, by design —
        // the buildings come first), and the founding has a house waiting on logs.
        foreach (Workplace site in world.Workplaces.Where(w => w.IsSite))
        {
            BuildFixtures.StockTheSite(site);
        }

        // Asked from the painted wood itself, so it is as near as anything can be.
        Assert.Equal(field, world.NearestHarvest(nearer, out _));
    }

    [Fact]
    public void ASaplingOnAFarmIsPulledUpAndNeverGrows()
    {
        (SimLoop loop, Workplace farm) = AFarm();
        SimWorld world = loop.World;
        GridPos given = Pose(world, farm, Terrain.Sapling, 1)[0];

        world.PaintWorkGround(farm, given);
        Assert.Equal(Terrain.Field, world.Map.TerrainAt(given));

        // One that comes after (a forager's thinning can make one): pulled up by the next pass.
        GridPos later = Pose(world, farm, Terrain.Grass, 1)[0];
        world.PaintWorkGround(farm, later);
        world.SetTerrain(later, Terrain.Sapling);

        loop.Step(Config.RegrowthPeriodDays * Config.TicksPerDay * 2);

        Terrain now = world.Map.TerrainAt(later);
        Assert.True(now == Terrain.Field || SimWorld.IsStandingCrop(now), $"The farm's sapling became {now}.");
    }

    [Fact]
    public void AFarmsOpenGroundIsNotSeededByTheWoodBesideIt()
    {
        (SimLoop loop, Workplace farm) = AFarm();
        SimWorld world = loop.World;
        // ⚠️ Nobody works it: a farmhand sows open ground in spring, and a sown tile is not grass for
        // the wood to seed — the first pose of this guard scored zero for exactly that.
        world.SetStaffing(farm, 0);
        GridPos open = Pose(world, farm, Terrain.Grass, 1)[0];
        world.PaintWorkGround(farm, open);
        world.SetTerrain(open, Terrain.Grass);

        // Wood on every side that is not the farm's, and the tile counted as wooded once.
        foreach (GridPos side in new[] { new GridPos(open.X + 1, open.Y), new GridPos(open.X - 1, open.Y), new GridPos(open.X, open.Y + 1), new GridPos(open.X, open.Y - 1) })
        {
            if (world.Map.Contains(side) && !world.SomethingStandsAt(side) && world.Zones.WorkGroundOwner(side) == 0)
            {
                world.SetTerrain(side, Terrain.Forest);
            }
        }

        world.SetTerrain(open, Terrain.Sapling);
        world.Map.SetYoungSapling(open, false);
        world.SetTerrain(open, Terrain.Grass);
        Assert.True(world.Map.HasEverBeenWooded(open));
        int woodBeside = new[] { new GridPos(open.X + 1, open.Y), new GridPos(open.X - 1, open.Y), new GridPos(open.X, open.Y + 1), new GridPos(open.X, open.Y - 1) }
            .Count(side => world.Map.TerrainAt(side) == Terrain.Forest);
        Assert.True(woodBeside > 0, $"The pose put no wood beside {open}; the guard would prove nothing.");

        loop.Step(Config.RegrowthPeriodDays * Config.TicksPerDay * 2);

        // ⚠️ Still GRASS, not merely "not wood": seeded, it would be pulled up the next pass and read
        // as a field (the rule above), which is how this guard's first assertion scored zero.
        Assert.Equal(Terrain.Grass, world.Map.TerrainAt(open));
    }
}

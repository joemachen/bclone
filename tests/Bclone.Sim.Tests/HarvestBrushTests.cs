using Bclone.Sim.Config;
using Bclone.Sim.Core;
using Bclone.Sim.Determinism;
using Bclone.Sim.Logging;
using Bclone.Sim.World;
using Xunit;
using Xunit.Abstractions;

namespace Bclone.Sim.Tests;

/// <summary>
/// The harvest brush — what the village means to clear (D87).
/// </summary>
/// <remarks>
/// <para>
/// <b>Painting harvest is <em>taking</em>; a forester's ground is <em>keeping</em>.</b> Joe,
/// playing: <em>"if there is a 'harvest trees' paint brush then the user can paint the map to
/// harvest existing trees and laborers should harvest those trees — that is the intention for
/// the opening with respect to gathering building materials."</em> So the opening needs the
/// brush and not the hut, and the hut is what answers running out.
/// </para>
/// <para>
/// <b>This is the ground under that, and the laborers who work it are the next commit.</b> The
/// brush, the layer and the taking are complete and guarded here; nothing walks to a painted
/// tile yet.
/// </para>
/// </remarks>
public sealed class HarvestBrushTests
{
    private readonly ITestOutputHelper _output;

    public HarvestBrushTests(ITestOutputHelper output) => _output = output;

    private static SimWorld Build() =>
        ManagedVillage.Loop(VillageFixtures.Village, new InMemoryLogSink()).World;

    private static GridPos FindTile(SimWorld world, Terrain terrain)
    {
        GeneratedMap map = world.Map;
        for (int y = map.MinY; y < map.MinY + map.Height; y++)
        {
            for (int x = map.MinX; x < map.MinX + map.Width; x++)
            {
                var at = new GridPos(x, y);
                if (map.TerrainAt(at) == terrain)
                {
                    return at;
                }
            }
        }

        throw new Xunit.Sdk.XunitException($"The generated valley has no {terrain} at all.");
    }

    // ---------------------------------------------------------------
    //  Painting
    // ---------------------------------------------------------------

    [Fact]
    public void TreesCanBePaintedForTheTaking()
    {
        SimWorld world = Build();
        GridPos forest = FindTile(world, Terrain.Forest);

        Assert.True(world.PaintHarvest(forest).Allowed);
        Assert.True(world.Zones.IsHarvest(forest));
        Assert.Equal(1, world.Zones.HarvestTiles);

        Assert.True(world.EraseHarvest(forest));
        Assert.False(world.Zones.IsHarvest(forest));
        Assert.Equal(0, world.Zones.HarvestTiles);
    }

    /// <summary>⭐ Empty ground is never painted, so the brush cannot promise work.</summary>
    /// <remarks>
    /// <b>Permissive about where and firm about what</b>, the rule <c>PaintResidential</c>
    /// already follows. A painted tile with nothing on it would send somebody across the
    /// valley to fell a field.
    /// </remarks>
    [Fact]
    public void GroundWithNothingOnItIsNeverPainted()
    {
        SimWorld world = Build();
        GridPos grass = FindTile(world, Terrain.Grass);

        PlacementVerdict verdict = world.PaintHarvest(grass);
        _output.WriteLine(verdict.Reason);

        Assert.False(verdict.Allowed);
        Assert.False(world.Zones.IsHarvest(grass));
        Assert.Equal(0, world.Zones.HarvestTiles);
    }

    [Fact]
    public void OutsideTheValleyIsRefusedRatherThanThrown()
    {
        SimWorld world = Build();
        var offMap = new GridPos(world.Map.MinX - 50, world.Map.MinY - 50);

        Assert.False(world.PaintHarvest(offMap).Allowed);
        Assert.Equal(0, world.Zones.HarvestTiles);
    }

    /// <summary>Water holds nothing to take.</summary>
    [Fact]
    public void NobodyIsSentToHarvestTheRiver()
    {
        SimWorld world = Build();
        Assert.False(world.PaintHarvest(FindTile(world, Terrain.Water)).Allowed);
    }

    // ---------------------------------------------------------------
    //  Taking it
    // ---------------------------------------------------------------

    /// <summary>⭐ A forest tile is a deposit: take it and the ground is grass.</summary>
    /// <remarks>
    /// <b>D84's rule, in one method.</b> This is the whole difference between the brush and
    /// the forester's hut — the brush spends what is standing, the hut keeps it. Terrain goes
    /// through <c>SetTerrain</c>, so the routing cache hears about it.
    /// </remarks>
    [Fact]
    public void HarvestingATileSpendsIt()
    {
        SimWorld world = Build();
        GridPos forest = FindTile(world, Terrain.Forest);
        world.PaintHarvest(forest);

        (Goods goods, int amount) = world.Harvest(forest);

        _output.WriteLine($"{forest} gave {amount} {goods} and is now {world.Map.TerrainAt(forest)}");

        Assert.Equal(Goods.Logs, goods);
        Assert.Equal(VillageFixtures.Village.LogsPerForestTile, amount);
        Assert.Equal(Terrain.Grass, world.Map.TerrainAt(forest));

        // And the paint comes off, because the job is done.
        Assert.False(world.Zones.IsHarvest(forest));
        Assert.Equal(0, world.Zones.HarvestTiles);

        // Taking it twice yields nothing — the deposit is spent, not a tap.
        Assert.Equal(0, world.Harvest(forest).Amount);
    }

    // ---------------------------------------------------------------
    //  Finding the work
    // ---------------------------------------------------------------

    [Fact]
    public void ThereIsNoWorkUntilSomebodyPaintsSome()
    {
        SimWorld world = Build();
        Assert.Null(world.NearestHarvest(world.Map.FoundingSite));
    }

    [Fact]
    public void TheNearestPaintedTreeIsTheOneToGoTo()
    {
        SimWorld world = Build();
        GridPos from = world.Map.FoundingSite;

        var painted = new List<GridPos>();
        GeneratedMap map = world.Map;
        for (int y = map.MinY; y < map.MinY + map.Height && painted.Count < 6; y++)
        {
            for (int x = map.MinX; x < map.MinX + map.Width && painted.Count < 6; x++)
            {
                var at = new GridPos(x, y);
                if (map.TerrainAt(at) == Terrain.Forest && world.PaintHarvest(at).Allowed)
                {
                    painted.Add(at);
                }
            }
        }

        Assert.True(painted.Count > 1, "Need several painted tiles for 'nearest' to mean anything.");

        GridPos? nearest = world.NearestHarvest(from);
        Assert.NotNull(nearest);

        int best = int.MaxValue;
        foreach (GridPos tile in painted)
        {
            best = System.Math.Min(best, world.TravelCost.Cost(from, tile));
        }

        _output.WriteLine(
            $"painted {painted.Count}; nearest is {nearest} at "
            + $"{world.TravelCost.Cost(from, nearest!.Value)}, best possible {best}");

        Assert.Equal(best, world.TravelCost.Cost(from, nearest!.Value));
    }

    /// <summary>
    /// ⭐ Paint over a tree that has already gone and the village quietly forgets it.
    /// </summary>
    /// <remarks>
    /// A forester's hut may fell ground the player also painted, and regrowth will one day
    /// move trees about underneath the paint. <b>Stale paint must not become an errand</b> —
    /// somebody walking across the valley to fell a field is the kind of thing that reads as
    /// a broken sim rather than a stale zone.
    /// </remarks>
    [Fact]
    public void PaintOverGroundThatIsAlreadyClearedStopsBeingWork()
    {
        SimWorld world = Build();
        GridPos forest = FindTile(world, Terrain.Forest);

        world.PaintHarvest(forest);
        Assert.Equal(1, world.Zones.HarvestTiles);

        // Somebody else cleared it — a hut, or a road, or regrowth moving on.
        world.SetTerrain(forest, Terrain.Grass);

        Assert.Null(world.NearestHarvest(world.Map.FoundingSite));
        Assert.Equal(0, world.Zones.HarvestTiles);
    }

    // ---------------------------------------------------------------
    //  Determinism
    // ---------------------------------------------------------------

    [Fact]
    public void WhatTheVillageMeansToClearIsPartOfTheWorld()
    {
        SimWorld world = Build();
        ulong before = StateHash.Compute(world);

        world.PaintHarvest(FindTile(world, Terrain.Forest));

        Assert.NotEqual(before, StateHash.Compute(world));
    }

    /// <summary>
    /// A village that paints nothing hashes as though the layer were not there.
    /// </summary>
    /// <remarks>
    /// The sparse convention, and the reason this ships without moving a golden. Note the
    /// <em>count</em> is deliberately not mixed alongside, unlike residential's: adding a
    /// second unconditional line would mix a fresh zero into every village that has never
    /// painted a tree.
    /// </remarks>
    [Fact]
    public void AnUnusedLayerIsInvisible()
    {
        SimWorld world = Build();
        ulong before = StateHash.Compute(world);

        GridPos forest = FindTile(world, Terrain.Forest);
        world.PaintHarvest(forest);
        world.EraseHarvest(forest);

        Assert.Equal(before, StateHash.Compute(world));
    }

    [Fact]
    public void TheSamePaintingGivesTheSameWorld()
    {
        SimWorld first = Build();
        SimWorld second = Build();

        foreach (SimWorld world in new[] { first, second })
        {
            world.PaintHarvest(FindTile(world, Terrain.Forest));
        }

        Assert.Equal(StateHash.Compute(first), StateHash.Compute(second));
    }

    /// <summary>The shipped config says what a forest tile holds.</summary>
    [Fact]
    public void TheShippedConfigSaysWhatATreeIsWorth()
    {
        SimConfig shipped = ShippedConfig.Load();

        Assert.True(shipped.LogsPerForestTile > 0);
        Assert.Equal(VillageFixtures.Village.LogsPerForestTile, shipped.LogsPerForestTile);
    }
}

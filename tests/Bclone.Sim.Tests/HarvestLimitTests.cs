using System.Collections.Generic;
using Bclone.Sim.Config;
using Bclone.Sim.Core;
using Bclone.Sim.Logging;
using Bclone.Sim.World;
using Xunit;
using Xunit.Abstractions;

namespace Bclone.Sim.Tests;

/// <summary>
/// The stock limit reaches the harvest brush — <b>the control was a box that did nothing</b>.
/// </summary>
/// <remarks>
/// <para>
/// D62's control ships a row per good (<c>StockLimits.Kinds</c> is the whole enum) and the sim
/// read three of them: food, logs and firewood, each at the workplace that produces it.
/// <b>Clearing painted ground read none.</b> So a player could type <em>"keep 100 stone"</em>,
/// watch the village clear every seam it had, and get no explanation — §1.1's failure, and
/// D145's <em>"a control needs one door"</em> on the good the door was never cut for.
/// </para>
/// <para>
/// <b>⛔ AND THE FOOTPRINT IS EXEMPT, WHICH IS THE GUARD THAT MATTERS.</b> <c>Mark</c> paints
/// the ground a building will stand on (D100), and a limit that stopped <em>that</em> being
/// cleared would deadlock the village on its own instruction: the building waits on the ground,
/// the ground waits on the limit, and the limit waits on nothing at all.
/// </para>
/// </remarks>
public sealed class HarvestLimitTests
{
    private readonly ITestOutputHelper _output;

    public HarvestLimitTests(ITestOutputHelper output) => _output = output;

    private static SimLoop Loop(SimConfig config) =>
        SimFactory.CreatePhase0(config, new InMemoryLogSink());

    private static int Count(SimWorld world, Terrain terrain)
    {
        int n = 0;
        for (int i = 0; i < world.Map.Tiles.Count; i++)
        {
            if (world.Map.Tiles[i] == terrain)
            {
                n++;
            }
        }

        return n;
    }

    /// <summary>Paint the nearest reachable tiles of one kind of ground, cheapest first.</summary>
    private static int PaintNearest(SimWorld world, Terrain terrain, int howMany)
    {
        GridPos site = world.Map.FoundingSite;
        var found = new List<(int Cost, GridPos At)>();

        for (int y = world.Map.MinY; y < world.Map.MinY + world.Map.Height; y++)
        {
            for (int x = world.Map.MinX; x < world.Map.MinX + world.Map.Width; x++)
            {
                var at = new GridPos(x, y);
                if (world.Map.TerrainAt(at) != terrain)
                {
                    continue;
                }

                int cost = world.TravelCost.Cost(site, at);
                if (cost != TerrainCostField.Unreachable)
                {
                    found.Add((cost, at));
                }
            }
        }

        found.Sort(static (a, b) =>
            a.Cost != b.Cost ? a.Cost.CompareTo(b.Cost)
            : a.At.Y != b.At.Y ? a.At.Y.CompareTo(b.At.Y)
            : a.At.X.CompareTo(b.At.X));

        int painted = 0;
        for (int i = 0; i < found.Count && painted < howMany; i++)
        {
            if (world.PaintHarvest(found[i].At).Allowed)
            {
                painted++;
            }
        }

        return painted;
    }

    /// <summary>⭐ Ask for a little stone and the village stops when it has it.</summary>
    [Fact]
    public void APaintedSeamIsLeftAloneOnceTheVillageHasEnough()
    {
        SimConfig config = VillageFixtures.Village;
        SimLoop loop = Loop(config);
        SimWorld world = loop.World;

        int painted = PaintNearest(world, Terrain.Rock, 8);
        Assert.True(painted > 0, "The valley has no reachable stone to paint.");

        // Two tiles' worth, against eight painted. A tile is spent whole, so the village can
        // overshoot by at most one tile: it stops between seams rather than mid-seam.
        int perTile = world.GoodsCatalog.YieldPerTileOf(Goods.Stone);
        world.SetStockLimit(Goods.Stone, perTile * 2);

        int seamsBefore = Count(world, Terrain.Rock);
        loop.Step(config.TicksPerYear * 2);

        int stone = world.InStores(Goods.Stone);
        int cleared = seamsBefore - Count(world, Terrain.Rock);
        _output.WriteLine(
            $"limit {perTile * 2}: cleared {cleared} of {painted} painted; {stone} stone in stores");

        Assert.True(
            stone <= perTile * 3,
            $"The village was asked to keep {perTile * 2} stone and took {stone}.");
        Assert.True(cleared < painted, "Every painted seam was cleared despite the limit.");
        Assert.True(cleared > 0, "Nothing was cleared at all, so the limit is not what stopped it.");
    }

    /// <summary>The anti-vacuity half: with no limit set, the same village takes the lot.</summary>
    /// <remarks>
    /// D7's rule. A guard that watches the village stop means nothing unless a village nobody
    /// has told anything to keeps going — and <c>null</c> is the default that must stay the
    /// default, so this is also the licence for the feature moving no golden.
    /// </remarks>
    [Fact]
    public void WithNoLimitTheWholeSeamIsCleared()
    {
        SimConfig config = VillageFixtures.Village;
        SimLoop loop = Loop(config);
        SimWorld world = loop.World;

        int painted = PaintNearest(world, Terrain.Rock, 8);
        int seamsBefore = Count(world, Terrain.Rock);

        loop.Step(config.TicksPerYear * 2);

        int cleared = seamsBefore - Count(world, Terrain.Rock);
        _output.WriteLine($"no limit: cleared {cleared} of {painted} painted");

        Assert.Equal(painted, cleared);
    }

    /// <summary>⛔ A limit on one good does not stop the village taking another.</summary>
    [Fact]
    public void TheLimitOnOneGoodLeavesTheOtherAlone()
    {
        SimConfig config = VillageFixtures.Village;
        SimLoop loop = Loop(config);
        SimWorld world = loop.World;

        PaintNearest(world, Terrain.Rock, 6);
        int trees = PaintNearest(world, Terrain.Forest, 6);
        Assert.True(trees > 0, "The valley has no reachable forest to paint.");

        world.SetStockLimit(Goods.Stone, 0);

        int forestBefore = Count(world, Terrain.Forest);
        loop.Step(config.TicksPerYear);

        _output.WriteLine(
            $"stone capped at 0: {world.InStores(Goods.Stone)} stone, "
            + $"forest {forestBefore} -> {Count(world, Terrain.Forest)}");

        Assert.Equal(0, world.InStores(Goods.Stone));
        Assert.True(
            Count(world, Terrain.Forest) < forestBefore,
            "A stone limit stopped the village felling trees.");
    }

    /// <summary>
    /// ⛔⛔ The ground a building is waiting on is cleared whatever the limit says.
    /// </summary>
    /// <remarks>
    /// <b>The deadlock this feature could have created.</b> <c>Mark</c> paints the footprint
    /// (D100), and <c>NextFootprintToClear</c> runs <em>before</em> the painted scan precisely
    /// so a building nobody would reach by nearest-first still gets its ground. A limit applied
    /// there would mean <em>the village cannot build because it already has enough of what the
    /// site is standing on</em> — a stall with no sentence attached to it.
    /// </remarks>
    [Fact]
    public void GroundABuildingWaitsOnIsClearedEvenAtTheLimit()
    {
        SimConfig config = VillageFixtures.Village;
        SimLoop loop = Loop(config);
        SimWorld world = loop.World;

        ColdStartTests.PlayTheOpening(world);
        world.SetStockLimit(Goods.Stone, 0);

        GridPos? seam = MarkAHutOnAStoneSeam(world);
        Assert.NotNull(seam);
        Assert.True(world.Zones.IsHarvest(seam!.Value), "Marking did not paint the footprint.");

        loop.Step(config.TicksPerYear * 2);

        _output.WriteLine(
            $"footprint at {seam}: terrain now {world.Map.TerrainAt(seam.Value)}, "
            + $"{world.InStores(Goods.Stone)} stone in stores");

        Assert.True(
            world.GroundIsClearAt(seam.Value),
            "A stone limit of zero left a building waiting on its own ground for ever.");
    }

    private static GridPos? MarkAHutOnAStoneSeam(SimWorld world)
    {
        for (int y = world.Map.MinY; y < world.Map.MinY + world.Map.Height; y++)
        {
            for (int x = world.Map.MinX; x < world.Map.MinX + world.Map.Width; x++)
            {
                var at = new GridPos(x, y);
                if (world.Map.TerrainAt(at) == Terrain.Rock
                    && world.TravelCost.Cost(world.Map.FoundingSite, at)
                        != TerrainCostField.Unreachable
                    && world.Mark(BuildingKind.GathererHut, at).Allowed)
                {
                    return at;
                }
            }
        }

        return null;
    }
}

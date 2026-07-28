using Bclone.Sim.World;
using Xunit;
using Xunit.Abstractions;

namespace Bclone.Sim.Tests;

/// <summary>
/// The flow field that makes water impassable — <c>specs/pathfinding-and-water.md</c>
/// (D40).
/// </summary>
/// <remarks>
/// Tested on hand-built maps rather than generated ones, because the whole value of
/// this type is that its answers are checkable by counting on your fingers.
/// </remarks>
public sealed class TerrainCostFieldTests
{
    private readonly ITestOutputHelper _output;

    public TerrainCostFieldTests(ITestOutputHelper output) => _output = output;

    private const int TileCost = TravelCostField.BaseTileCost;

    /// <summary>A small square valley. <c>~</c> is water, anything else is walkable.</summary>
    /// <remarks>
    /// Rows are given top-down as they read on the page, so a test's map looks like the
    /// map it describes. Row 0 of the array is therefore the LOWEST y.
    /// </remarks>
    private static GeneratedMap Map(params string[] rowsTopDown)
    {
        int height = rowsTopDown.Length;
        int width = rowsTopDown[0].Length;
        var terrain = new Terrain[width * height];

        for (int row = 0; row < height; row++)
        {
            string line = rowsTopDown[height - 1 - row];
            for (int x = 0; x < width; x++)
            {
                terrain[(row * width) + x] = line[x] switch
                {
                    '~' => Terrain.Water,
                    '"' => Terrain.Forest,
                    _ => Terrain.Grass,
                };
            }
        }

        return new GeneratedMap(
            width, height, minX: 0, minY: 0, terrain, new byte[width * height],
            new[] { new GridPos(0, 0) }, new[] { new GridPos(0, 0) }, new GridPos(0, 0));
    }

    [Fact]
    public void OnOpenGroundItIsJustTheDistance()
    {
        // The anti-vacuity anchor for everything else (D7): if the field did not agree
        // with plain distance on an empty map, every other assertion here would be
        // measuring a broken field against itself.
        GeneratedMap map = Map(
            ".....",
            ".....",
            ".....");

        var field = TerrainCostField.Build(map, new GridPos(0, 0), TileCost);

        Assert.Equal(0, field.CostFrom(new GridPos(0, 0)));
        Assert.Equal(TileCost, field.CostFrom(new GridPos(1, 0)));
        Assert.Equal(4 * TileCost, field.CostFrom(new GridPos(4, 0)));
        Assert.Equal(6 * TileCost, field.CostFrom(new GridPos(4, 2)));
    }

    [Fact]
    public void AWallOfWaterMakesTheWalkLonger()
    {
        // A river across the middle with a gap at the east end. Getting from the
        // bottom-left to the top-left means walking round, not through.
        GeneratedMap map = Map(
            ".....",
            "~~~~.",
            ".....");

        var field = TerrainCostField.Build(map, new GridPos(0, 0), TileCost);

        int direct = new GridPos(0, 2).ManhattanDistanceTo(new GridPos(0, 0)) * TileCost;
        int actual = field.CostFrom(new GridPos(0, 2));

        _output.WriteLine($"straight line {direct}, round the water {actual}");

        // Round the gap: 4 east, 2 north, 4 west = 10 tiles, against 2 in a straight line.
        Assert.Equal(10 * TileCost, actual);
        Assert.True(actual > direct, "Water is not costing anything — the river is scenery.");
    }

    [Fact]
    public void WhatTheWaterCutsOffIsUnreachableRatherThanFarAway()
    {
        // The distinction the whole API rests on. "Very expensive" would quietly win a
        // nearest-thing search and put a villager on an errand they can never complete.
        GeneratedMap map = Map(
            ".....",
            "~~~~~",
            ".....");

        var field = TerrainCostField.Build(map, new GridPos(0, 0), TileCost);

        Assert.False(field.IsReachable(new GridPos(0, 2)));
        Assert.Equal(TerrainCostField.Unreachable, field.CostFrom(new GridPos(0, 2)));

        // And the near side is still perfectly fine.
        Assert.True(field.IsReachable(new GridPos(4, 0)));
    }

    [Fact]
    public void NobodyIsEverStandingOnWater()
    {
        GeneratedMap map = Map(
            ".....",
            "~~~~.",
            ".....");

        var field = TerrainCostField.Build(map, new GridPos(0, 0), TileCost);
        Assert.Equal(TerrainCostField.Unreachable, field.CostFrom(new GridPos(2, 1)));
    }

    [Fact]
    public void WalkingTheFieldArrives()
    {
        // The property that makes stored routes unnecessary: step to the cheapest
        // neighbour, repeatedly, and you get there — round obstacles included.
        GeneratedMap map = Map(
            ".....",
            "~~~~.",
            ".....");

        var destination = new GridPos(0, 0);
        var field = TerrainCostField.Build(map, destination, TileCost);

        var walker = new GridPos(0, 2);
        var route = new List<GridPos> { walker };

        for (int step = 0; step < 100 && walker != destination; step++)
        {
            GridPos next = field.StepFrom(walker);
            Assert.NotEqual(walker, next);
            Assert.NotEqual(Terrain.Water, map.TerrainAt(next));
            walker = next;
            route.Add(walker);
        }

        _output.WriteLine(string.Join(" -> ", route));
        Assert.Equal(destination, walker);
    }

    [Fact]
    public void AWalkerWhoCannotGetThereStaysPut()
    {
        // Rather than stepping into the bank forever. A villager who cannot reach their
        // errand needs to be told so, not to vibrate against a river.
        GeneratedMap map = Map(
            ".....",
            "~~~~~",
            ".....");

        var field = TerrainCostField.Build(map, new GridPos(0, 0), TileCost);
        var stranded = new GridPos(0, 2);

        Assert.Equal(stranded, field.StepFrom(stranded));
    }

    [Fact]
    public void ABuildingInTheRiverReachesNobody()
    {
        // Left unreachable rather than nudged to the bank. Something standing in the
        // water is a bug in whatever placed it, and quietly relocating it here would
        // make that bug surface somewhere else entirely.
        GeneratedMap map = Map(
            ".....",
            "~~~~~",
            ".....");

        var field = TerrainCostField.Build(map, new GridPos(2, 1), TileCost);

        Assert.False(field.IsReachable(new GridPos(0, 0)));
        Assert.False(field.IsReachable(new GridPos(4, 2)));
    }

    [Fact]
    public void TheSameMapAlwaysGivesTheSameField()
    {
        // Ties are broken by tile order, not by a priority queue's internal shuffling.
        // Two equally short ways round an obstacle must resolve identically every run,
        // or villagers take different routes on the same seed and the state hash
        // diverges on a journey nobody chose differently.
        GeneratedMap map = Map(
            ".....",
            "..~..",
            ".....");

        var a = TerrainCostField.Build(map, new GridPos(0, 0), TileCost);
        var b = TerrainCostField.Build(map, new GridPos(0, 0), TileCost);

        for (int y = 0; y < 3; y++)
        {
            for (int x = 0; x < 5; x++)
            {
                var here = new GridPos(x, y);
                Assert.Equal(a.CostFrom(here), b.CostFrom(here));
                Assert.Equal(a.StepFrom(here), b.StepFrom(here));
            }
        }
    }

    [Fact]
    public void ForestIsWalkable()
    {
        // Trees are where timber comes from, not a wall. Only water stops anyone, and
        // only until they learn to bridge it (D40).
        GeneratedMap map = Map(
            "\"\"\"\"\"",
            "\"\"\"\"\"",
            "\"\"\"\"\"");

        var field = TerrainCostField.Build(map, new GridPos(0, 0), TileCost);
        Assert.Equal(6 * TileCost, field.CostFrom(new GridPos(4, 2)));
    }
}

using Bclone.Sim.World;
using Xunit;

namespace Bclone.Sim.Tests;

/// <summary>
/// The shared cost field is load-bearing for both labour catchment and, later,
/// desire paths — so it gets tested as the contract between them, not just as a
/// distance function.
/// </summary>
public sealed class TravelCostFieldTests
{
    private static TravelCostField Field => new(ticksPerBaseTile: 1);

    [Fact]
    public void CostIsZeroForTheSameTile()
    {
        Assert.Equal(0, Field.Cost(new GridPos(3, 4), new GridPos(3, 4)));
    }

    [Fact]
    public void CostScalesWithManhattanDistance()
    {
        var field = Field;
        Assert.Equal(TravelCostField.BaseTileCost, field.Cost(new GridPos(0, 0), new GridPos(1, 0)));
        Assert.Equal(5 * TravelCostField.BaseTileCost, field.Cost(new GridPos(0, 0), new GridPos(5, 0)));
        Assert.Equal(7 * TravelCostField.BaseTileCost, field.Cost(new GridPos(0, 0), new GridPos(3, 4)));
    }

    [Fact]
    public void CostIsSymmetric()
    {
        // Asymmetric travel cost would make "who is nearest" depend on which end you
        // measure from, which is exactly the kind of thing that desyncs a village.
        var field = Field;
        var a = new GridPos(2, 7);
        var b = new GridPos(-3, 1);

        Assert.Equal(field.Cost(a, b), field.Cost(b, a));
    }

    [Fact]
    public void TicksMatchTilesAtTheDefaultRate()
    {
        Assert.Equal(5, Field.TicksBetween(new GridPos(0, 0), new GridPos(5, 0)));
    }

    [Fact]
    public void TicksScaleWithTheConfiguredRate()
    {
        var slow = new TravelCostField(ticksPerBaseTile: 3);
        Assert.Equal(15, slow.TicksBetween(new GridPos(0, 0), new GridPos(5, 0)));
    }

    // ⛔ `CatchmentIsMeasuredInCostNotTiles` and `CatchmentBoundaryIsInclusive` are deleted
    // with `IsWithinCatchment` itself (`forests-and-gathering.md §3`). They tested a helper
    // nothing in the sim called any more, named after a concept the game no longer has. The
    // distinction they were protecting — that distance is measured in *cost*, so a road can
    // extend a workplace's reach without either system knowing about the other — is intact
    // and is still tested, by `CostAgreesWithTheDistanceUsedForMovement` below.

    [Fact]
    public void CostAgreesWithTheDistanceUsedForMovement()
    {
        // Catchment and movement must not disagree about how far something is —
        // that is the "two competing travel-cost systems" failure §2.6 warns about.
        var field = Field;
        var from = new GridPos(1, 2);
        var to = new GridPos(6, 9);

        Assert.Equal(from.ManhattanDistanceTo(to), field.TicksBetween(from, to));
    }

    [Fact]
    public void InvalidConstructionThrows()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new TravelCostField(0));
        Assert.Throws<ArgumentOutOfRangeException>(() => new TravelCostField(-1));
    }

    [Fact]
    public void NegativeCostIsRejected()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => Field.TicksForCost(-1));
    }

    /// <summary>
    /// ⭐ The whole route, as the tiles <c>StepToward</c> would visit — <b>what a pulled string
    /// is pulled over</b> (gridless slice 4, D356).
    /// </summary>
    /// <remarks>
    /// Nothing new is computed: it is <c>StepToward</c> repeated over the cached flow field, so
    /// it cannot disagree with the step a villager would have taken. Each step is 4-connected,
    /// the route ends on the destination, and its length is the cost in base tiles — which on
    /// open ground is the Manhattan distance. Across water it goes round, and to an island it is
    /// empty rather than a lie.
    /// </remarks>
    [Fact]
    public void RouteFromWalksTheFieldToTheDestination()
    {
        var open = new TravelCostField();
        var from = new GridPos(1, 2);
        var to = new GridPos(6, 9);

        List<GridPos> route = open.RouteFrom(from, to);

        Assert.Equal(from.ManhattanDistanceTo(to), route.Count);
        Assert.Equal(to, route[^1]);
        GridPos previous = from;
        foreach (GridPos step in route)
        {
            Assert.Equal(1, previous.ManhattanDistanceTo(step));
            previous = step;
        }

        Assert.Empty(open.RouteFrom(to, to));
    }

    [Fact]
    public void RouteFromGoesRoundWaterAndIsEmptyToAnIsland()
    {
        // Rows top-down; row 0 of the array is the lowest y.
        string[] rows =
        {
            ".....",
            ".~~~.",
            ".~.~.",
            ".~~~.",
            ".....",
        };
        int height = rows.Length;
        int width = rows[0].Length;
        var terrain = new Terrain[width * height];
        for (int row = 0; row < height; row++)
        {
            string line = rows[height - 1 - row];
            for (int x = 0; x < width; x++)
            {
                terrain[(row * width) + x] = line[x] == '~' ? Terrain.Water : Terrain.Grass;
            }
        }

        var map = new GeneratedMap(width, height, 0, 0, terrain, new byte[width * height], new GridPos(0, 0));
        var field = new TravelCostField(1, map);

        List<GridPos> round = field.RouteFrom(new GridPos(0, 2), new GridPos(4, 2));
        Assert.Equal(new GridPos(4, 2), round[^1]);
        Assert.True(round.Count > 4, "the route across the pond must be longer than the straight line");
        Assert.All(round, tile => Assert.NotEqual(Terrain.Water, map.TerrainAt(tile)));

        Assert.Empty(field.RouteFrom(new GridPos(0, 2), new GridPos(2, 2)));
    }
}

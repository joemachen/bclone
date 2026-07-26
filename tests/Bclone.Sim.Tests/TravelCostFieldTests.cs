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

    [Fact]
    public void CatchmentIsMeasuredInCostNotTiles()
    {
        // The distinction that lets a road extend a workplace's reach later without
        // catchment needing to know roads exist.
        var field = Field;
        var workplace = new GridPos(0, 0);
        int radius = TravelCostField.TilesToCost(4);

        Assert.True(field.IsWithinCatchment(workplace, new GridPos(4, 0), radius));
        Assert.False(field.IsWithinCatchment(workplace, new GridPos(5, 0), radius));
    }

    [Fact]
    public void CatchmentBoundaryIsInclusive()
    {
        var field = Field;
        int radius = TravelCostField.TilesToCost(3);

        Assert.True(field.IsWithinCatchment(new GridPos(0, 0), new GridPos(3, 0), radius));
    }

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
}

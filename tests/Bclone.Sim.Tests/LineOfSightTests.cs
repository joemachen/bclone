using Bclone.Sim.Core;
using Bclone.Sim.World;
using Xunit;
using Xunit.Abstractions;

namespace Bclone.Sim.Tests;

/// <summary>
/// ⭐⭐ The straight line between two points, as the tiles it touches — <b>the question string-
/// pulling asks</b> (gridless slice 4, D356).
/// </summary>
/// <remarks>
/// <para>
/// A pulled string is only allowed to cut across the staircase where every tile under it is
/// passable, so the raycast has to be <b>exact and conservative</b>: exact so two machines agree
/// on which tiles a line touches (it is sim state — a leg is planned from it), conservative so a
/// line grazing the corner between two water tiles counts as touching both. Under-counting would
/// let a villager wade; over-counting only bends the string a tile early.
/// </para>
/// <para>
/// Integer and fixed-point rationals throughout, compared by cross-multiplication in
/// <c>Int128</c> — no float, no <c>Fixed</c> division, no square root (D2).
/// </para>
/// </remarks>
public sealed class LineOfSightTests
{
    private readonly ITestOutputHelper _output;

    public LineOfSightTests(ITestOutputHelper output) => _output = output;

    /// <summary>Rows top-down as they read on the page; row 0 of the array is the lowest y.</summary>
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
                terrain[(row * width) + x] = line[x] == '~' ? Terrain.Water : Terrain.Grass;
            }
        }

        return new GeneratedMap(width, height, minX: 0, minY: 0, terrain, new byte[width * height], new GridPos(0, 0));
    }

    private static List<GridPos> Crossed(GridPos a, GridPos b) =>
        LineOfSight.TilesCrossed(Point.CentreOf(a), Point.CentreOf(b));

    /// <summary>A line along a row touches exactly that row's tiles, in order.</summary>
    [Fact]
    public void ALineAlongARowTouchesExactlyThatRow()
    {
        List<GridPos> tiles = Crossed(new GridPos(0, 2), new GridPos(3, 2));
        Assert.Equal(new[] { new GridPos(0, 2), new GridPos(1, 2), new GridPos(2, 2), new GridPos(3, 2) }, tiles);

        // And backwards, and vertically.
        Assert.Equal(4, Crossed(new GridPos(3, 2), new GridPos(0, 2)).Count);
        Assert.Equal(3, Crossed(new GridPos(5, 5), new GridPos(5, 7)).Count);
        Assert.Equal(new[] { new GridPos(4, 4) }, Crossed(new GridPos(4, 4), new GridPos(4, 4)));
    }

    /// <summary>
    /// ⛔ A pure diagonal passes EXACTLY through tile corners, and a corner counts as touching
    /// both tiles beside it — <b>the conservative half of the contract</b>.
    /// </summary>
    /// <remarks>
    /// From (0,0) to (2,2) the line crosses the corner at (1,1) and at (2,2). Both neighbours of
    /// each corner are visited, so a diagonal through a gap one water tile wide is refused.
    /// </remarks>
    [Fact]
    public void ADiagonalThroughACornerTouchesBothTilesBesideIt()
    {
        List<GridPos> tiles = Crossed(new GridPos(0, 0), new GridPos(2, 2));
        _output.WriteLine(string.Join(" ", tiles));

        Assert.Contains(new GridPos(0, 0), tiles);
        Assert.Contains(new GridPos(1, 0), tiles);
        Assert.Contains(new GridPos(0, 1), tiles);
        Assert.Contains(new GridPos(1, 1), tiles);
        Assert.Contains(new GridPos(2, 1), tiles);
        Assert.Contains(new GridPos(1, 2), tiles);
        Assert.Contains(new GridPos(2, 2), tiles);
        Assert.Equal(7, tiles.Distinct().Count());
    }

    /// <summary>A knight's move touches the four tiles the segment actually passes through.</summary>
    /// <remarks>
    /// From (0.5, 0.5) to (2.5, 1.5): through (0,0), into (1,0), across y = 1 at x = 1.5 into
    /// (1,1), into (2,1). Nothing else — this is the exact half of the contract.
    /// </remarks>
    [Fact]
    public void AKnightsMoveTouchesExactlyTheTilesUnderIt()
    {
        List<GridPos> tiles = Crossed(new GridPos(0, 0), new GridPos(2, 1));
        _output.WriteLine(string.Join(" ", tiles));

        Assert.Equal(new[] { new GridPos(0, 0), new GridPos(1, 0), new GridPos(1, 1), new GridPos(2, 1) }, tiles);
    }

    /// <summary>Water anywhere under the line refuses it; the same line over grass is clear.</summary>
    [Fact]
    public void WaterUnderTheLineRefusesIt()
    {
        GeneratedMap open = Map(
            ".....",
            ".....",
            ".....");
        GeneratedMap ditch = Map(
            ".....",
            "..~..",
            ".....");

        Point from = Point.CentreOf(new GridPos(0, 1));
        Point to = Point.CentreOf(new GridPos(4, 1));

        Assert.True(LineOfSight.Clear(open, from, to));
        Assert.False(LineOfSight.Clear(ditch, from, to));

        // Symmetric: clear one way is clear the other, blocked one way is blocked the other.
        Assert.True(LineOfSight.Clear(open, to, from));
        Assert.False(LineOfSight.Clear(ditch, to, from));
    }

    /// <summary>
    /// ⛔ A diagonal squeezing between two water tiles that meet at a corner is REFUSED — a
    /// villager cannot walk through the point where two ponds touch.
    /// </summary>
    [Fact]
    public void TheCornerBetweenTwoWaterTilesIsNotAGap()
    {
        GeneratedMap pinch = Map(
            ".~",
            "~.");

        Assert.False(LineOfSight.Clear(pinch, Point.CentreOf(new GridPos(0, 0)), Point.CentreOf(new GridPos(1, 1))));
    }

    /// <summary>
    /// ⭐ From a point OFF a tile centre — where a villager is mid-leg when they change their
    /// mind — the tiles are still exactly the ones under the segment.
    /// </summary>
    /// <remarks>
    /// From (0.25, 0.5) to (2.5, 0.5): the line stays in row 0 the whole way — tiles (0,0),
    /// (1,0), (2,0). From (0.5, 0.75) to (2.5, 1.75): crosses y = 1 at x = 1.0, a corner — so
    /// (0,0), (1,0), (0,1), (1,1), (2,1).
    /// </remarks>
    [Fact]
    public void AnOffCentreStartStillReadsTheTilesUnderTheSegment()
    {
        var quarterIn = new Point(Fixed.FromRatio(1, 4), Fixed.FromRatio(1, 2));
        Assert.Equal(
            new[] { new GridPos(0, 0), new GridPos(1, 0), new GridPos(2, 0) },
            LineOfSight.TilesCrossed(quarterIn, Point.CentreOf(new GridPos(2, 0))));

        var lowLeft = new Point(Fixed.FromRatio(1, 2), Fixed.FromRatio(3, 4));
        var highRight = new Point(Fixed.FromRatio(5, 2), Fixed.FromRatio(7, 4));
        List<GridPos> tiles = LineOfSight.TilesCrossed(lowLeft, highRight);
        _output.WriteLine(string.Join(" ", tiles));

        Assert.Contains(new GridPos(0, 0), tiles);
        Assert.Contains(new GridPos(1, 0), tiles);
        Assert.Contains(new GridPos(0, 1), tiles);
        Assert.Contains(new GridPos(1, 1), tiles);
        Assert.Contains(new GridPos(2, 1), tiles);
        Assert.Equal(5, tiles.Distinct().Count());
    }
}

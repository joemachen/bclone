using Bclone.Sim.World;
using Xunit;
using Xunit.Abstractions;

namespace Bclone.Sim.Tests;

/// <summary>
/// ⭐⭐ The shape the brush lays down, in the one place that decides it (D327).
/// </summary>
/// <remarks>
/// <para>
/// <b>⛔ THE LOOP THIS GUARDS WAS WRITTEN TWICE.</b> <c>PaintAround</c> and
/// <c>DrawTheBrushful</c> each carried their own <c>for</c> over the same range, with the same
/// pasted comment block — including a doc-comment saying <em>"The diamond, not a square"</em>
/// three lines above an inline comment saying <b>SQUARE, NOT A DIAMOND</b>. Nothing but
/// discipline held them together, and the code said so out loud: *"a preview that disagrees with
/// the paint is worse than no preview."*
/// </para>
/// <para>
/// ⭐ <b><see cref="BrushPreviewTests"/> could not have caught it</b>, and that is worth stating
/// rather than assuming: those three tests assert <c>CanPaint*</c> ≡ <c>Paint*</c>
/// <em>per tile</em>, which is shape-agnostic. They would have stayed green through a preview
/// that drew a circle over a square stroke. **These are the guards that have an opinion about
/// the shape.**
/// </para>
/// </remarks>
public sealed class BrushShapeTests
{
    private readonly ITestOutputHelper _output;

    public BrushShapeTests(ITestOutputHelper output) => _output = output;

    private static readonly GridPos Somewhere = new(40, 31);

    // ---------------------------------------------------------------
    //  The square
    // ---------------------------------------------------------------

    /// <summary>A square brush is the whole block, and nothing is counted twice.</summary>
    [Theory]
    [InlineData(0, 1)]
    [InlineData(1, 9)]
    [InlineData(2, 25)]
    [InlineData(6, 169)]
    public void ASquareBrushCoversEveryTileInItsBlock(int radius, int expected)
    {
        List<GridPos> tiles = BrushStroke.TilesUnder(Somewhere, radius, BrushShape.Square);

        _output.WriteLine($"radius {radius}: {tiles.Count} tiles, {tiles.Distinct().Count()} distinct");

        Assert.Equal(expected, tiles.Count);
        Assert.Equal(expected, tiles.Distinct().Count());
        Assert.All(tiles, tile => Assert.True(
            System.Math.Abs(tile.X - Somewhere.X) <= radius
            && System.Math.Abs(tile.Y - Somewhere.Y) <= radius,
            $"{tile} is outside a radius-{radius} square"));
    }

    /// <summary>
    /// ⭐ A radius of zero is exactly the tile under the cursor — the precision setting.
    /// </summary>
    /// <remarks>
    /// Both shapes, because a round brush that vanished at its smallest would be a size the
    /// player can select and cannot use.
    /// </remarks>
    [Theory]
    [InlineData(BrushShape.Square)]
    [InlineData(BrushShape.Round)]
    public void TheSmallestBrushIsTheTileUnderTheCursor(BrushShape shape)
    {
        List<GridPos> tiles = BrushStroke.TilesUnder(Somewhere, 0, shape);

        Assert.Equal(new[] { Somewhere }, tiles);
    }

    // ---------------------------------------------------------------
    //  The round
    // ---------------------------------------------------------------

    /// <summary>
    /// ⛔⛔ A round brush is a CIRCLE, and the count is pinned so it cannot drift back to a diamond.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>21 tiles at radius 2, not 13.</b> Thirteen is what <c>Abs(dx) + Abs(dy) &lt;= radius</c>
    /// gives — the Manhattan diamond Joe threw out in D221 because *"the diamond left corners
    /// unpainted and made a dragged stroke scallop along its edges"* — and it is also what plain
    /// <c>dx² + dy² &lt;= r²</c> gives, which is the trap: **the obvious circle test reproduces
    /// the rejected shape exactly.** The <c>r(r+1)</c> term is the difference.
    /// </para>
    /// <para>
    /// ⚠️ <b>RADIUS 1 IS THE FULL 3×3 AND THAT IS GEOMETRY, NOT A BUG.</b> The test is *"is this
    /// tile's centre within r+½ of the cursor's?"*, and a disc of radius 1.5 genuinely contains a
    /// corner at √2 ≈ 1.414. **So the smallest round brush and the smallest square brush are the
    /// same nine tiles.** Recorded here rather than discovered later, because it looks like the
    /// shape toggle is broken at that size and it is not.
    /// </para>
    /// <para>
    /// ⚠️ These numbers are the shape. If one of them moves, the brush changed — re-take them
    /// deliberately or not at all.
    /// </para>
    /// </remarks>
    [Theory]
    [InlineData(1, 9)]
    [InlineData(2, 21)]
    [InlineData(3, 37)]
    [InlineData(4, 69)]
    [InlineData(6, 137)]
    public void ARoundBrushIsACircleAndNotTheDiamondD221ThrewOut(int radius, int expected)
    {
        List<GridPos> round = BrushStroke.TilesUnder(Somewhere, radius, BrushShape.Round);

        int diamond = BrushStroke.TilesUnder(Somewhere, radius, BrushShape.Square).Count(
            tile => System.Math.Abs(tile.X - Somewhere.X) + System.Math.Abs(tile.Y - Somewhere.Y)
                <= radius);

        _output.WriteLine($"radius {radius}: round {round.Count}, diamond {diamond}");

        Assert.Equal(expected, round.Count);
        Assert.True(round.Count > diamond, $"radius {radius}: round {round.Count} is the diamond's {diamond}");
    }

    /// <summary>The round brush is a subset of the square, and never the whole of it.</summary>
    /// <remarks>
    /// ⚠️ From radius 2 up. At radius 1 the two shapes are the same nine tiles — see
    /// <see cref="ARoundBrushIsACircleAndNotTheDiamondD221ThrewOut"/>, where that is stated as
    /// the geometry it is.
    /// </remarks>
    [Theory]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(6)]
    public void ARoundBrushLosesTheCornersAndNothingElse(int radius)
    {
        List<GridPos> square = BrushStroke.TilesUnder(Somewhere, radius, BrushShape.Square);
        List<GridPos> round = BrushStroke.TilesUnder(Somewhere, radius, BrushShape.Round);

        Assert.All(round, tile => Assert.Contains(tile, square));
        Assert.True(round.Count < square.Count, "a round brush that covers the square is not round");

        // The corner of the block is what a round brush is FOR losing.
        Assert.DoesNotContain(
            new GridPos(Somewhere.X + radius, Somewhere.Y + radius), round);

        // The far tile on each axis is what it must KEEP — losing those would be the plus-shape.
        Assert.Contains(new GridPos(Somewhere.X + radius, Somewhere.Y), round);
        Assert.Contains(new GridPos(Somewhere.X, Somewhere.Y + radius), round);
    }

    /// <summary>
    /// ⭐ Both shapes are symmetric under all four reflections — a brush that leans is a brush
    /// that aims somewhere other than where the cursor is.
    /// </summary>
    [Theory]
    [InlineData(BrushShape.Square)]
    [InlineData(BrushShape.Round)]
    public void TheBrushIsTheSameInEveryDirection(BrushShape shape)
    {
        var tiles = BrushStroke.TilesUnder(Somewhere, 4, shape).ToHashSet();

        foreach (GridPos tile in tiles)
        {
            int dx = tile.X - Somewhere.X;
            int dy = tile.Y - Somewhere.Y;

            Assert.Contains(new GridPos(Somewhere.X - dx, Somewhere.Y + dy), tiles);
            Assert.Contains(new GridPos(Somewhere.X + dx, Somewhere.Y - dy), tiles);
            Assert.Contains(new GridPos(Somewhere.X + dy, Somewhere.Y + dx), tiles);
        }
    }

    // ---------------------------------------------------------------
    //  The size, and the order
    // ---------------------------------------------------------------

    /// <summary>The brush cannot be sized past what it is allowed to be.</summary>
    /// <remarks>
    /// The wheel adds and subtracts without looking, so this is the only thing standing between
    /// a spun wheel and a stroke that covers the valley.
    /// </remarks>
    [Fact]
    public void TheBrushCannotBeSizedPastItsOwnEnds()
    {
        Assert.Equal(BrushStroke.MinRadius, BrushStroke.Clamp(-9));
        Assert.Equal(BrushStroke.MaxRadius, BrushStroke.Clamp(9_000));
        Assert.Equal(BrushStroke.DefaultRadius, BrushStroke.Clamp(BrushStroke.DefaultRadius));

        // Whatever the wheel does, the stroke stays inside the ceiling.
        int biggest = BrushStroke.TilesUnder(Somewhere, 9_000, BrushShape.Square).Count;
        Assert.Equal(BrushStroke.Across(BrushStroke.MaxRadius) * BrushStroke.Across(BrushStroke.MaxRadius), biggest);
    }

    /// <summary>What the sentence says the brush is, is what the brush is.</summary>
    [Fact]
    public void TheWidthTheSentenceQuotesIsTheWidthItPaints()
    {
        for (int radius = BrushStroke.MinRadius; radius <= BrushStroke.MaxRadius; radius++)
        {
            int across = BrushStroke.Across(radius);
            List<GridPos> tiles = BrushStroke.TilesUnder(Somewhere, radius, BrushShape.Square);

            Assert.Equal(across, tiles.Select(tile => tile.X).Distinct().Count());
            Assert.Equal(across * across, tiles.Count);
        }
    }

    /// <summary>
    /// ⛔ The order is part of the contract, because the stroke reports ONE warning.
    /// </summary>
    /// <remarks>
    /// <c>PaintAround</c> keeps the last warning and the last refusal it saw and says one of each
    /// for the whole drag (D42, D92). Which sentence survives therefore depends on which tile is
    /// visited last — so an unordered set here would make the message the player reads depend on
    /// iteration order nothing promised. Row-major, lowest tile first, same rule as
    /// <c>Footprint.CoveredTiles</c>.
    /// </remarks>
    [Fact]
    public void TheTilesComeBackInAStatedOrder()
    {
        List<GridPos> once = BrushStroke.TilesUnder(Somewhere, 3, BrushShape.Round);
        List<GridPos> twice = BrushStroke.TilesUnder(Somewhere, 3, BrushShape.Round);

        Assert.Equal(once, twice);

        // Lowest row first, highest last — the last tile visited is the one whose warning survives.
        Assert.Equal(Somewhere.Y - 3, once[0].Y);
        Assert.Equal(Somewhere.Y + 3, once[^1].Y);

        for (int i = 1; i < once.Count; i++)
        {
            bool ordered = once[i].Y > once[i - 1].Y
                || (once[i].Y == once[i - 1].Y && once[i].X > once[i - 1].X);

            Assert.True(ordered, $"{once[i - 1]} then {once[i]} is not row-major");
        }
    }
}

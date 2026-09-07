namespace Bclone.Sim.World;

/// <summary>What shape the brush lays down (D327).</summary>
/// <remarks>
/// <para>
/// ⚠️ <b><see cref="Round"/> is not the old diamond and must not become it.</b> The brush was
/// Manhattan distance — <c>Abs(dx) + Abs(dy) &lt;= radius</c> — until D221, and Joe rejected it
/// because *"the diamond left corners unpainted and made a dragged stroke scallop along its
/// edges."* A round brush is a <em>circle</em>: it keeps the corners of the axes and loses only
/// the corners of the square, which is what a player expects when they ask for a round brush.
/// </para>
/// </remarks>
public enum BrushShape
{
    /// <summary>The full <c>(2r+1)²</c> block. The default, and what has shipped since D221.</summary>
    Square = 0,

    /// <summary>A disc.</summary>
    Round = 1,
}

/// <summary>
/// <b>The tiles under the brush — one function, called by the paint and by the preview.</b>
/// </summary>
/// <remarks>
/// <para>
/// ⛔⛔ <b>THIS EXISTS BECAUSE THE TWO LOOPS WERE A COPY-PASTE OF EACH OTHER</b>, comment block
/// included, down to a doc-comment reading <em>"The diamond, not a square"</em> three lines above
/// an inline comment reading <b>SQUARE, NOT A DIAMOND</b>. The code's own warning was that
/// <em>"a preview that disagrees with the paint is worse than no preview"</em> — and nothing but
/// discipline was holding them together. <b>One function is what turns that comment into a
/// guarantee.</b>
/// </para>
/// <para>
/// ⛔ <b>And <c>BrushPreviewTests</c> (D198) could never have caught the divergence.</b> It asserts
/// <c>CanPaint*</c> ≡ <c>Paint*</c> <em>per tile</em>, which is shape-agnostic by construction: it
/// would have stayed green through a preview that drew a circle over a square stroke.
/// </para>
/// <para>
/// ⭐ <b>It lives in the sim rather than in the view because the view cannot be tested at all.</b>
/// Nothing in <c>tests/</c> references <c>Bclone.Game</c> (D11, D160), so a predicate left beside
/// the drawing code is untestable by construction. This is pure grid geometry — integer-only, no
/// sim state, no Godot — and <see cref="HarvestBrush"/> is the precedent for a brush concept
/// living here.
/// </para>
/// </remarks>
public static class BrushStroke
{
    /// <summary>A single tile. The precision setting.</summary>
    public const int MinRadius = 0;

    /// <summary>13×13. Big enough for a neighbourhood, small enough to still be aimed.</summary>
    /// <remarks>
    /// ⚠️ D221 noted that going from the 13-tile diamond to the 25-tile square meant
    /// <b>one click paints nearly twice the ground</b>. At the ceiling one click is 169 tiles, so
    /// this is a real limit rather than a formality — it is the point past which a brush stops
    /// being a brush and becomes a fill.
    /// </remarks>
    public const int MaxRadius = 6;

    /// <summary>5×5 — what has shipped since D221, so nothing changes for a player who ignores this.</summary>
    public const int DefaultRadius = 2;

    /// <summary>Hold a radius inside what the brush can be.</summary>
    public static int Clamp(int radius) => radius < MinRadius
        ? MinRadius
        : radius > MaxRadius ? MaxRadius : radius;

    /// <summary>How wide the brush reads, in tiles across — what the announce sentence says.</summary>
    public static int Across(int radius) => (Clamp(radius) * 2) + 1;

    /// <summary>
    /// The tiles this brush covers, in a stated order.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>⛔ ROW-MAJOR, LOWEST TILE FIRST, AND THE ORDER IS PART OF THE CONTRACT</b> — the same
    /// rule <see cref="Footprint.CoveredTiles"/> states, for the same reason: the stroke reports
    /// <em>one</em> warning and <em>one</em> refusal for the whole drag, and which one survives
    /// depends on which tile is visited last. A set with no order would make that sentence
    /// depend on iteration order nothing promised.
    /// </para>
    /// <para>
    /// ⚠️ Returns a fresh list rather than yielding, because the caller walks it and a lazy
    /// sequence would be re-evaluated silently. The largest brush is 169 tiles.
    /// </para>
    /// <para>
    /// ⚠️ <b>It does not know where the valley ends.</b> Tiles off the map are the caller's to
    /// skip — the map is not a parameter here, and giving this function a world would make the
    /// one thing the view and the sim share depend on the world it is being asked about.
    /// </para>
    /// </remarks>
    public static List<GridPos> TilesUnder(GridPos centre, int radius, BrushShape shape)
    {
        int reach = Clamp(radius);
        var tiles = new List<GridPos>();

        // ⭐ The round test is `dx² + dy² <= r(r+1)`, NOT `<= r²`. Integer arithmetic, no square
        // root, symmetric under all four reflections — and the `r(r+1)` term is what makes a
        // circle look like one. At radius 2, plain `r²` admits 13 tiles: exactly the plus-shape
        // the Manhattan diamond drew, which is the shape D221 threw out. `r(r+1)` admits 21.
        long limit = (long)reach * (reach + 1);

        for (int dy = -reach; dy <= reach; dy++)
        {
            for (int dx = -reach; dx <= reach; dx++)
            {
                if (shape == BrushShape.Round && (((long)dx * dx) + ((long)dy * dy)) > limit)
                {
                    continue;
                }

                tiles.Add(new GridPos(centre.X + dx, centre.Y + dy));
            }
        }

        return tiles;
    }
}

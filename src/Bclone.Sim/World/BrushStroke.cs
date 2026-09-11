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
    /// <summary>
    /// ⭐⭐ The sub-tiles under the brush — <b>the resolution the player actually paints at</b>
    /// (D336).
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>The same shape predicate, one grid finer.</b> Joe: *"haha this is a circle????"* — at
    /// five TILES across a disc has no choice but to look like a bitten square, because a square
    /// grid that size holds nothing rounder. **At five tiles across in QUARTER-tiles it is twenty
    /// wide**, and twenty is plenty.
    /// </para>
    /// <para>
    /// ⚠️ <b>The radius is in sub-tiles here and the sentence the player reads is in tiles.</b> Two
    /// units for one number is the trap D322 records, so they are kept in two places on purpose:
    /// this takes what it measures in, and <c>Across</c> converts once, where the words are made.
    /// </para>
    /// </remarks>
    public static List<SubTile> SubTilesUnder(SubTile centre, int radius, BrushShape shape)
    {
        int reach = ClampSub(radius);
        var under = new List<SubTile>();

        long limit = (long)reach * (reach + 1);

        for (int dy = -reach; dy <= reach; dy++)
        {
            for (int dx = -reach; dx <= reach; dx++)
            {
                if (shape == BrushShape.Round && (((long)dx * dx) + ((long)dy * dy)) > limit)
                {
                    continue;
                }

                under.Add(new SubTile(centre.X + dx, centre.Y + dy));
            }
        }

        return under;
    }

    /// <summary>The smallest brush: one quarter-tile, for work that has to be exact.</summary>
    public const int MinSubRadius = 0;

    /// <summary>About thirteen tiles across, which is where the tile-measured ceiling was.</summary>
    public const int MaxSubRadius = 26;

    /// <summary>Five and a quarter tiles across — what has shipped, to the nearest quarter.</summary>
    public const int DefaultSubRadius = 10;

    /// <summary>Hold a sub-tile radius inside what the brush can be.</summary>
    public static int ClampSub(int radius) => radius < MinSubRadius
        ? MinSubRadius
        : radius > MaxSubRadius ? MaxSubRadius : radius;

    /// <summary>How wide the brush is, <b>in sub-tiles</b>.</summary>
    /// <remarks>
    /// ⛔⛔ <b>THIS USED TO RETURN THE WIDTH IN TILES AS A `float`, AND `FloatBanTests` REDDENED —
    /// CORRECTLY (D336).</b> D2 bans floating point from the sim's public API, and the guard does
    /// not care that the number was only ever going into a sentence. ⭐ **It was right on the
    /// substance too, not only the letter:** converting quarter-tiles into a decimal number of
    /// tiles is *making words*, and words are the view's job. The sim says how many sub-tiles;
    /// `VillageMap` says *"5.25 tiles"*. *A guard that looks like bureaucracy is worth reading
    /// twice before you route around it* (D247).
    /// </remarks>
    public static int AcrossInSubTiles(int subRadius) => (ClampSub(subRadius) * 2) + 1;

    /// <summary>
    /// ⭐⭐ The WHOLE tiles under the brush — those with at least half their quarters covered
    /// (D350). <b>The farm's stroke.</b>
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Joe, with a screenshot of furrows a tile past his paint: *"the painted area needs to be
    /// accurate for the user across all use cases."*</b> Paint is quarter-tiles (D335); a plough is
    /// a tile. Anything tile-shaped that hangs off quarter-tile paint sticks out by up to half a
    /// tile — so for a farm the paint itself is laid in tiles, and *a ploughed field is man-made
    /// and reads as man-made precisely because its edges are straight* (D342). **Joe chose this
    /// (2026-09-11) over clipping the drawn field to the quarters.**
    /// </para>
    /// <para>
    /// ⭐ The threshold is D335's own — a tile counts when at least half of it is painted — applied
    /// at the brush rather than after it, so a stroke and the ground it holds are the same set.
    /// Row-major, lowest tile first, the contract <see cref="TilesUnder"/> states and for its
    /// reason: the stroke says one sentence, and which tile is visited last decides it.
    /// </para>
    /// <para>
    /// ⚠️ In the sim rather than the view, like <see cref="SubTilesUnder"/>, because the paint and
    /// its preview must be one shape function (D327) and nothing under <c>tests/</c> can see the view.
    /// </para>
    /// </remarks>
    public static List<GridPos> TilesMostlyUnder(SubTile centre, int subRadius, BrushShape shape)
    {
        var quarters = new Dictionary<GridPos, int>();
        foreach (SubTile at in SubTilesUnder(centre, subRadius, shape))
        {
            GridPos tile = at.Tile;
            quarters[tile] = quarters.TryGetValue(tile, out int had) ? had + 1 : 1;
        }

        var tiles = new List<GridPos>();
        foreach ((GridPos tile, int covered) in quarters)
        {
            if (covered >= SubTile.HalfATile)
            {
                tiles.Add(tile);
            }
        }

        // ⚠️ A dictionary's order is not a thing to paint from (D51's trap): sorted into the
        // stated order before anybody walks it.
        tiles.Sort((a, b) => a.Y != b.Y ? a.Y.CompareTo(b.Y) : a.X.CompareTo(b.X));
        return tiles;
    }

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

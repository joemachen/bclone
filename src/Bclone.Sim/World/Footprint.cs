using Bclone.Sim.Core;

namespace Bclone.Sim.World;

/// <summary>
/// The ground a building stands on — a rectangle, turned, resolved to tiles (gridless 2b, D319).
/// </summary>
/// <remarks>
/// <para>
/// <b>⭐⭐ JOE'S CALL ON WHAT THE SIM SHOULD BELIEVE</b> (2026-09-06), from three options: *"tile
/// coverage from the true rect"*. **The sim stores the real rotated rectangle and derives which
/// tiles it covers.** Occupancy stays per-tile, so terrain, clearing and pathing are untouched and
/// there is no second collision system living beside the tile world — which `CLAUDE.md` forbids in
/// as many words: *one shared cost field; do not build two competing travel-cost systems.*
/// </para>
/// <para>
/// ⛔ <b>The two options refused, and why.</b> A full continuous OBB-vs-OBB collision would be
/// truer to the word "gridless" and is a second physics system to get wrong. An axis-aligned
/// bounding box would be cheapest and **would reserve ground the building does not use** — a
/// player would see it and could not explain it, which is §1.1 failing.
/// </para>
///
/// <para>
/// <b>⭐ THE RULE, IN ONE SENTENCE A PLAYER COULD BE TOLD: a building covers the tiles whose
/// CENTRES it stands on.</b> Not "every tile it clips a corner of" — that would make a rotated
/// building creep outward by a hair at every angle and would be impossible to predict by eye. The
/// centre test is what makes coverage legible: *look at the middle of a tile and ask whether the
/// building is over it.*
/// </para>
/// <para>
/// ⭐⭐ <b>AND IT MAKES ROTATION FREE FOR EVERY BUILDING THAT EXISTS TODAY.</b> A 1×1 building's
/// centre sits exactly on its own tile's centre, so the offset is zero, so **rotating it cannot
/// change what it covers at any angle whatsoever.** Every building in the game is 1×1
/// (`specs/gridless.md §2.3`), which is why this slice can add facing to all of them and move no
/// golden: *a village where nothing is bigger than a tile is, to the hash, a village from before
/// facings existed.*
/// </para>
/// <para>
/// ⚠️ <b>The candidate box is deliberately generous.</b> Half the diagonal of a w×h rectangle is
/// √(w²+h²)/2, and <see cref="Fixed"/> has no square root — so the search uses <c>(w+h)/2</c>,
/// which is always at least the true half-diagonal and is never wrong, only occasionally a tile
/// wider than it strictly needed to be. *A conservative search that costs one extra loop beats a
/// tight one that needs a square root this project has not built.*
/// </para>
/// </remarks>
public readonly record struct Footprint
{
    /// <summary>Where the building's centre actually is (gridless 2c, D329).</summary>
    /// <remarks>
    /// ⭐⭐ <b>THIS WAS A <see cref="GridPos"/>, AND THAT WAS THE LAST PIECE OF THE GRID IN
    /// PLACEMENT.</b> <c>Point.cs</c> said so in as many words — *"A building's `Position` is still
    /// a `GridPos`, and its geometric centre is that tile's centre"* — which is the constraint this
    /// slice removes. A building's centre is now wherever it was put, and the tile it is indexed by
    /// is derived from that rather than the other way round.
    /// </remarks>
    public required Point Origin { get; init; }

    /// <summary>How many tiles across, before turning. One for every building that exists today.</summary>
    public required int Width { get; init; }

    /// <summary>How many tiles deep, before turning.</summary>
    public required int Height { get; init; }

    /// <summary>Which way it is turned.</summary>
    public Angle Facing { get; init; }

    /// <summary>A single tile, facing north — centred on the tile it names.</summary>
    public static Footprint OneTile(GridPos at) =>
        new() { Origin = Point.CentreOf(at), Width = 1, Height = 1, Facing = Angle.Zero };

    /// <summary>
    /// The tiles this building covers, in a stated order.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>⛔ ROW-MAJOR, LOWEST TILE FIRST, AND THE ORDER IS PART OF THE CONTRACT.</b> Anything that
    /// hashes or logs a coverage list would otherwise depend on iteration order that nothing
    /// promised — the same trap `BannedSymbols.txt` records for `Dictionary` iteration. *A set with
    /// no order is a determinism bug waiting for a second implementation.*
    /// </para>
    /// <para>
    /// ⚠️ Returns a fresh list rather than yielding, because callers compare and re-walk it and a
    /// lazy sequence would be re-evaluated silently. A footprint is at most a few dozen tiles.
    /// </para>
    /// </remarks>
    public List<GridPos> CoveredTiles()
    {
        var covered = new List<GridPos>();

        Point centre = Origin;
        Fixed halfWidth = Fixed.FromRatio(Width, 2);
        Fixed halfHeight = Fixed.FromRatio(Height, 2);

        // Generous, because the true half-diagonal wants a square root Fixed does not have.
        // ⚠️ ONE TILE WIDER THAN IT USED TO BE (D329). The old scan was centred on a tile and could
        // stop at the half-diagonal; a centre that sits anywhere inside its tile can push coverage
        // up to a further tile out in either direction. **It tests more tiles and covers exactly the
        // same ones** for a building on a tile centre, which is why nothing moved when the type did.
        int reach = ((Width + Height + 1) / 2) + 1;
        GridPos anchor = Origin.ToTile();

        for (int dy = -reach; dy <= reach; dy++)
        {
            for (int dx = -reach; dx <= reach; dx++)
            {
                var tile = new GridPos(anchor.X + dx, anchor.Y + dy);

                // Turn the tile's centre back into the building's own frame, where the rectangle
                // is axis-aligned and the test is two comparisons.
                Point local = (Point.CentreOf(tile) - centre).RotatedBy(-Facing);

                if (Abs(local.X) <= halfWidth && Abs(local.Y) <= halfHeight)
                {
                    covered.Add(tile);
                }
            }
        }

        return covered;
    }

    /// <summary>Does this building stand on that tile?</summary>
    /// <remarks>
    /// <para>
    /// ⭐⭐ <b>ASKED DIRECTLY, NOT BY BUILDING THE WHOLE LIST AND SEARCHING IT (D329).</b> This was
    /// <c>CoveredTiles().Contains(tile)</c> — a list allocation and up to a few dozen rotations to
    /// answer a question about <em>one</em> tile. It is the hottest call in the sim:
    /// <c>SomethingStandsAt</c> asks it of five collections, and <c>CanBuildAt</c> asks
    /// <c>SomethingStandsAt</c> once per covered tile.
    /// </para>
    /// <para>
    /// ⚠️ <b>Measured, because that is the rule (METHODOLOGY §3, D179).</b> Making
    /// <see cref="Origin"/> continuous widened the scan in <see cref="CoveredTiles"/> by a tile in
    /// each direction — 25 candidates for a 1×1 building where there were 9 — and **the suite went
    /// from 4m55s to 9m**. *The horizon nobody suspected was the one that moved.* This is the same
    /// arithmetic as the loop body, run once.
    /// </para>
    /// <para>
    /// ⛔ <b>It must stay the same arithmetic, in the same order.</b> <see cref="Fixed"/>
    /// multiplication is not associative, so a "tidier" rearrangement of
    /// <see cref="Point.RotatedBy"/>'s four products is a behaviour change that no determinism
    /// guard would object to. <c>FootprintTests</c> compares the two answers directly.
    /// </para>
    /// </remarks>
    public bool Covers(GridPos tile)
    {
        Point local = (Point.CentreOf(tile) - Origin).RotatedBy(-Facing);

        return Abs(local.X) <= Fixed.FromRatio(Width, 2)
            && Abs(local.Y) <= Fixed.FromRatio(Height, 2);
    }

    private static Fixed Abs(Fixed value) => value < Fixed.Zero ? -value : value;
}

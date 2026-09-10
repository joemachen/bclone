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
                if (tile == anchor || StandsOn(tile))
                {
                    covered.Add(tile);
                }
            }
        }

        return covered;
    }

    /// <summary>
    /// ⛔⛔ Does this building stand on that tile? — <b>and it ALWAYS stands on its own</b> (D331).
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>THE CENTRE RULE ALONE CAN CLAIM NOTHING, AND JOE FOUND IT IN AN AFTERNOON.</b> D319's
    /// rule is *a building covers the tiles whose CENTRES it stands on* — and a unit square
    /// reliably contains a point of a unit lattice only while it is <b>axis-aligned</b>. Turned 45°
    /// its axis-aligned reach falls to 1/√2 ≈ 0.707, so a 1×1 sitting between tile centres slips
    /// past all four and this returned an <b>empty list</b>. **On the grid that was unreachable**;
    /// free placement made it reachable the same day it shipped.
    /// </para>
    /// <para>
    /// ⛔ <b>What an empty footprint cost:</b> the building could not be selected, named or
    /// demolished (all twelve finders go through <see cref="Covers(GridPos)"/>), <b>and no builder
    /// could ever raise it</b> — <c>SiteAt</c> uses this too, and D108 means the builder reads the site from
    /// the tile they are standing on, so they arrived and there was nothing there. <c>CanBuildAt</c>
    /// raised no objection because its refusal loop iterates the covered tiles: *a check that
    /// iterates a set says nothing about the empty set.*
    /// </para>
    /// <para>
    /// ⭐ <b>The rule now, and it is one sentence a player could be told: a building always stands
    /// on at least the tile its centre is in.</b> Added at the anchor's own place in the scan, so
    /// the row-major order stays part of the contract.
    /// </para>
    /// </remarks>
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
    public bool Covers(GridPos tile) => tile == Origin.ToTile() || StandsOn(tile);

    /// <summary>
    /// ⭐⭐ Is this exact point inside the building's rectangle? — <b>what the player
    /// means when they click on it</b> (D338).
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Joe, from play: *"there are areas of a building in which clicking selects a non-building
    /// tile even though part of the building looks like it is in that spot."*</b> He is describing
    /// exactly what the centre rule does at the edges: a rotated building **draws** over ground
    /// whose tile centres it does not stand on, and the click was being rounded to a tile before
    /// anything was asked about buildings.
    /// </para>
    /// <para>
    /// ⛔ <b>THIS IS NOT A SECOND COVERAGE RULE AND MUST NOT BECOME ONE.</b> D319's centre
    /// rule is what the sim believes about ground — *which tiles does this building claim* —
    /// and it stays exactly as it was. **This answers a different question: where is the building
    /// drawn.** Ownership is still legible by the centre rule; only the mouse reads the rectangle.
    /// </para>
    /// <para>
    /// ⭐ <b>It is <see cref="StandsOn"/>'s body, factored, not rewritten.</b>
    /// <see cref="Fixed"/> multiplication is not associative, so retyping
    /// <see cref="Point.RotatedBy"/>'s four products in a different order is a behaviour change no
    /// determinism guard would object to. There is one copy of the arithmetic and there always
    /// has to be.
    /// </para>
    /// </remarks>
    public bool Covers(Point at)
    {
        Point local = (at - Origin).RotatedBy(-Facing);

        return Abs(local.X) <= Fixed.FromRatio(Width, 2)
            && Abs(local.Y) <= Fixed.FromRatio(Height, 2);
    }

    /// <summary>The centre test on its own — <b>the arithmetic both callers share</b>.</summary>
    /// <remarks>
    /// ⛔ <b>ONE COPY, BECAUSE THERE USED TO BE TWO.</b> <see cref="Covers(GridPos)"/> is a fast
    /// path for the question <see cref="CoveredTiles"/> answers in bulk, and D329 wrote the rotation
    /// out twice. <see cref="Fixed"/> multiplication is not associative, so two copies is two chances
    /// to reassociate one of them into a different answer that no determinism guard would object to.
    /// </remarks>
    private bool StandsOn(GridPos tile) => Covers(Point.CentreOf(tile));

    /// <summary>
    /// ⭐⭐ Do these two buildings occupy the same ground? — <b>the real geometry</b> (D331).
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>⛔ COVERAGE AND COLLISION ARE TWO QUESTIONS AND THEY WANTED TWO ANSWERS.</b> *Which ground
    /// does this building claim?* is the centre rule, and it is what makes ownership legible. *May I
    /// build here?* is whether two rectangles share any space — and asking it through tile occupancy
    /// was fine while everything sat on a tile centre and became wrong the moment placement was
    /// free: **two huts a hair either side of a tile boundary claim different tiles and stand on top
    /// of each other.** `gridless.md §7.3` promised this in as many words — *"collision becomes
    /// geometry rather than is this tile taken"* — and this is that promise.
    /// </para>
    /// <para>
    /// <b>The separating-axis test, over four axes</b> — two per rectangle, which is all an oriented
    /// box needs. If any axis exists on which the two projections do not reach each other, they are
    /// apart; if none does, they overlap.
    /// </para>
    /// <para>
    /// ⛔⛔ <b>TOUCHING IS APART, AND THAT IS LOAD-BEARING RATHER THAN A ROUNDING CHOICE.</b> Two
    /// 1×1 buildings on adjacent tile centres are exactly one apart with radii summing to exactly
    /// one — so a strict test would call every neighbouring pair in every village an overlap and
    /// refuse ground the game has always allowed. **`>=` separates**, and every golden depends on it.
    /// </para>
    /// <para>
    /// ⚠️ <b>Integer-only and order-sensitive.</b> Every product floors and <see cref="Fixed"/>
    /// multiplication is not associative, so the grouping here is part of the answer — the same
    /// warning <see cref="Point.RotatedBy"/> carries. The sine table's error (4.7 × 10⁻⁶, D318) is
    /// far below a pixel and far below the half-tile margins this test works in.
    /// </para>
    /// </remarks>
    public bool Overlaps(Footprint other)
    {
        Point apart = other.Origin - Origin;

        return !ApartAlong(Across(Facing), apart, other)
            && !ApartAlong(Along(Facing), apart, other)
            && !ApartAlong(Across(other.Facing), apart, other)
            && !ApartAlong(Along(other.Facing), apart, other);
    }

    /// <summary>Whether the two boxes fail to reach each other along one axis.</summary>
    private bool ApartAlong(Point axis, Point apart, Footprint other) =>
        Abs(Dot(apart, axis)) >= ReachAlong(axis) + other.ReachAlong(axis);

    /// <summary>How far this box reaches from its centre along an axis.</summary>
    private Fixed ReachAlong(Point axis) =>
        Abs(Fixed.FromRatio(Width, 2) * Dot(Across(Facing), axis))
        + Abs(Fixed.FromRatio(Height, 2) * Dot(Along(Facing), axis));

    /// <summary>The building's own across-axis, as a unit vector.</summary>
    private static Point Across(Angle facing) => new(facing.Cos(), facing.Sin());

    /// <summary>The building's own along-axis — the across-axis turned a quarter.</summary>
    private static Point Along(Angle facing) => new(-facing.Sin(), facing.Cos());

    private static Fixed Dot(Point left, Point right) => (left.X * right.X) + (left.Y * right.Y);

    private static Fixed Abs(Fixed value) => value < Fixed.Zero ? -value : value;
}

using System.Text.Json.Nodes;
using Bclone.Sim.Core;
using Bclone.Sim.Persistence;

namespace Bclone.Sim.World;

/// <summary>
/// A bridge — <b>a deck laid over the water, one tile wide, in a straight line from where the player
/// pressed to where they let go</b> (D40, D546, D549, `specs/bridges.md`).
/// </summary>
/// <remarks>
/// <para>
/// <b>⭐ ITS OWN KIND OF THING, THE WELL'S REASON ONE STEP FURTHER.</b> A well is a building that stands
/// on the ground and is walked to; a bridge is not walked to at all, it is walked <em>across</em>. So
/// it is not an obstacle (D383 closes every footprint to walkers), it is not a new kind of ground (the
/// tiles stay <see cref="Terrain.Water"/>, so the fishery, the valley's drawing and the hash's terrain
/// bytes never learn it exists), and it is not in the standing index. It is a deck: the water under its
/// line becomes walkable (<c>SimWorld.IsWalkable</c>) at its <see cref="BuildingRow.DeckTileCost"/>.
/// </para>
/// <para>
/// ⭐⭐ <b>TWO POINTS, AT ANY ANGLE (Joe, D549: <i>"a straight line in ANY direction"</i>).</b> The tiles
/// it covers are <see cref="LineOfSight.TilesCrossed"/> of the segment — the exact grid walk, which at a
/// grid corner lists <em>both</em> tiles beside it — so they always join edge to edge and the 4-connected
/// cost field can walk a deck at 23° as well as at 0°. That is what D412's corner trap needed, and why
/// the quarter-turn rule could go. Nothing but the two ends is stored; the tiles are derived (D335).
/// </para>
/// </remarks>
public sealed class Bridge
{
    /// <summary>The end on the village's side when it was marked — where its builders stood.</summary>
    public required Point Near { get; init; }

    /// <summary>The other end, across the water.</summary>
    public required Point Far { get; init; }

    /// <summary>
    /// Which row it was raised from — wood or stone. <b>Settable once, by a rebuild</b>: the stone
    /// bridge raised over a wooden one is the same bridge with a new deck (`bridges.md §5.3`).
    /// </summary>
    public required BuildingKind Kind { get; set; }

    /// <summary>What the village calls it.</summary>
    public required string Name { get; set; }

    /// <summary>The tick it was finished (or last rebuilt) — the card's line, and what wear will read (D546 call 5).</summary>
    public ulong RaisedAtTick { get; set; }

    /// <summary>The tick the builders last mended it, or 0 never — wear is read from the later of the two stamps (`building-condition.md §4.1`).</summary>
    public ulong RepairedAtTick { get; set; }

    /// <summary>
    /// Whether it has fallen in — closed until the builders mend it (`building-condition.md §4.5`, D555). Set by the
    /// season's walk when its condition turns <em>ruin</em>, cleared by the repair; a decision made at a moment, so a
    /// fact (hashed sparsely, saved), not a per-read derivation.
    /// </summary>
    public bool Closed { get; set; }

    /// <summary>How many times somebody has stepped onto its deck from land.</summary>
    /// <remarks>
    /// <b>History, and not hashed</b> — nothing in the sim reads one bridge's count; the unlock reads the
    /// village's <c>SimWorld.BridgeCrossings</c>, which is. The card says it.
    /// </remarks>
    public int Crossings { get; internal set; }

    /// <summary>Crossings since the year turned — the card's "this year". History, not hashed.</summary>
    public int CrossingsThisYear { get; internal set; }

    private List<GridPos>? _tiles;

    /// <summary>Every tile under the line, end to end, in order — edge-joined at any angle.</summary>
    public IReadOnlyList<GridPos> Tiles() => _tiles ??= TilesUnder(Near, Far);

    /// <summary>Whether the bridge lands on or crosses this tile.</summary>
    public bool Holds(GridPos tile) => Tiles().Contains(tile);

    /// <summary>How long it is, end to end, in tiles rounded up — what it was priced by.</summary>
    public int Length => LengthOf(Near, Far);

    /// <summary>The tiles under a straight line between two points — the one rule for a bridge's ground.</summary>
    public static List<GridPos> TilesUnder(Point from, Point to) => LineOfSight.TilesCrossed(from, to);

    /// <summary>
    /// The straight distance between two points in tiles, rounded up — <b>exact, in integers</b> (D2): the
    /// smallest whole n with n² ≥ dx² + dy², compared in raw fixed-point bits over <see cref="Int128"/>.
    /// </summary>
    public static int LengthOf(Point from, Point to)
    {
        Int128 dx = to.X.RawBits - from.X.RawBits;
        Int128 dy = to.Y.RawBits - from.Y.RawBits;
        Int128 squared = (dx * dx) + (dy * dy);
        Int128 one = (Int128)1 << (2 * Fixed.FractionBits);
        int n = 0;
        while ((Int128)n * n * one < squared)
        {
            n++;
        }

        return n;
    }

    internal JsonObject ToSave() => new()
    {
        ["near"] = SaveWriter.Point(Near),
        ["far"] = SaveWriter.Point(Far),
        ["kind"] = SaveWriter.Enum(Kind),
        ["name"] = Name,
        ["raised_at"] = SaveWriter.Hex(RaisedAtTick),
        ["repaired_at"] = SaveWriter.Hex(RepairedAtTick),
        ["closed"] = Closed,
        ["crossings"] = Crossings,
        ["crossings_this_year"] = CrossingsThisYear,
    };

    internal static Bridge FromSave(SaveReader save) => new()
    {
        Near = save.Point("near"),
        Far = save.Point("far"),
        Kind = save.Enum<BuildingKind>("kind"),
        Name = save.String("name"),
        RaisedAtTick = save.ULong("raised_at"),
        RepairedAtTick = save.ULong("repaired_at"),
        Closed = save.HasValue("closed") && save.Bool("closed"),
        Crossings = save.Int("crossings"),
        CrossingsThisYear = save.Int("crossings_this_year"),
    };
}

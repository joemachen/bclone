using System.Text.Json.Serialization;

namespace Bclone.Sim.World;

/// <summary>
/// A reason a villager may be unsettled (W2, `specs/morale.md §4.1`; the player's word is <i>contentment</i>, D797).
/// </summary>
/// <remarks>
/// ⭐ <b>Each is a life, never a body</b> (D797, one cause one system): diet and sickness are health's (W6), and no cause
/// here may ever be read by it. A new kind needs code to notice it — the row only tunes and weighs it — so a row naming a
/// kind this build does not know is refused at load. The cemetery's two (W4) and the tavern's (W3) join as kinds with their
/// buildings. ⛔ <b>The number is the save's and the bitmask's</b> (<see cref="Villager.ContentmentCauses"/>): append, never
/// reorder.
/// </remarks>
public enum ContentmentCause
{
    /// <summary>The household has no house at the season's turn.</summary>
    NoRoof = 0,

    /// <summary>The walk to work is at least <see cref="ContentmentCauseRow.At"/> tiles (24; D800 call 2).</summary>
    FarWalk = 1,

    /// <summary>
    /// A winter with at least <see cref="ContentmentCauseRow.At"/> days at the get-warm line (6; D800 call 3), held for
    /// <see cref="ContentmentCauseRow.HeldSeasons"/> seasons from that winter (4 — the year after).
    /// </summary>
    ColdWinter = 2,

    /// <summary>A winter day at home with no firewood, held <see cref="ContentmentCauseRow.HeldSeasons"/> seasons (4).</summary>
    FireOut = 3,

    /// <summary>At least <see cref="ContentmentCauseRow.At"/> days with the jar dry in a season (3; D800 call 4).</summary>
    DryJar = 4,

    /// <summary>Somebody of the household, or the partner, died in the last <see cref="ContentmentCauseRow.HeldSeasons"/> seasons (4).</summary>
    DeathInTheFamily = 5,
}

/// <summary>
/// One contentment cause and how it is weighed (D798–D800, `morale.md §13` step 2) — <c>contentment_causes</c> in the
/// data file, a row each.
/// </summary>
/// <remarks>
/// A person is <i>unsettled</i> at a season's turn when the weights of the causes held reach
/// <c>contentment_unsettled_at</c> (2; D800 call 1). Every shipped weight is 1, so that is <i>two causes at once</i>.
/// </remarks>
public sealed record ContentmentCauseRow
{
    /// <summary>Which cause.</summary>
    [JsonPropertyName("cause")]
    public ContentmentCause Cause { get; init; }

    /// <summary>What it counts toward the line when held — more than zero.</summary>
    [JsonPropertyName("weight")]
    public int Weight { get; init; } = 1;

    /// <summary>The cause's own threshold: tiles for the walk, days for a cold winter or a dry jar; unread by the others.</summary>
    [JsonPropertyName("at")]
    public int At { get; init; }

    /// <summary>How many seasons it is held from the season it happened in, that one counted; unread by the standing causes.</summary>
    [JsonPropertyName("held_seasons")]
    public int HeldSeasons { get; init; }
}

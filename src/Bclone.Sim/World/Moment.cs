namespace Bclone.Sim.World;

/// <summary>
/// Something worth stopping for — <b>a gift, or a moment the village will remember</b>.
/// </summary>
/// <remarks>
/// <para>
/// <b>⭐⭐ RARE BY DESIGN, AND THAT IS THE WHOLE SPECIFICATION.</b> Joe, 2026-08-26, settling what
/// may interrupt a player: **gifts and big moments only.** The village log is the home for
/// everything ordinary; a modal that fires often is the alert spam §1.2 refuses by name, and the
/// second one nobody reads is worse than none.
/// </para>
/// <para>
/// <b>⛔ A MOMENT NEVER REPLACES THE LOG LINE — IT ACCOMPANIES ONE.</b> Every raiser writes its
/// sentence to the village log as well, so a headless run, a player who dismissed the panel without
/// reading it, and the audit trail all end up knowing the same thing. **The modal is a second
/// surface, never the only one**, which is also what keeps D177's ruling intact: milestones are log
/// lines, and this is punctuation on top rather than a panel instead.
/// </para>
/// <para>
/// ⚠️ <b>It carries no verbs.</b> A moment says what happened; it never asks the player to choose,
/// because a modal with a decision in it is a modal that must be understood before it can be
/// dismissed, and that is the anxiety §1.2 exists to keep out.
/// </para>
/// </remarks>
public sealed record Moment
{
    /// <summary>A few words the player reads first — <em>"The village can write."</em></summary>
    public required string Title { get; init; }

    /// <summary>A sentence or two saying what happened and why it happened now.</summary>
    public required string Body { get; init; }
}

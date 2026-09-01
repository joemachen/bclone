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

    /// <summary>Whether the village holds its breath for this one.</summary>
    /// <remarks>
    /// <para>
    /// <b>⭐ TWO WEIGHTS OF MOMENT, ADDED 2026-08-27 ON JOE'S CALL</b> — <i>"should be a modal
    /// that pops up to alert the player (doesn't pause the game)… a little more celebratory than
    /// just a line in the village log."</i>
    /// </para>
    /// <para>
    /// <b>A gift WAITS</b> — a free library is something the player must act on by placing it, so
    /// its panel stays up until they say "Go on". <b>A discovery PASSES</b> — it is news, not a
    /// decision, and its banner fades on its own after fourteen seconds. A technique arrives every
    /// few decades in the ordinary run of a village; holding the world for each one would turn the
    /// rarity that justifies the modal into the frequency that discredits it.
    /// </para>
    /// <para>
    /// ⛔⛔ <b>RENAMED FROM <c>Stops</c> ON 2026-08-30, BECAUSE IT STOPPED STOPPING ANYTHING.</b>
    /// Joe: *"presently gifts pause the game; slow it down to 1x only."* **Neither kind of moment
    /// pauses the village now** — both slow it to 1× and hand the player's speed back afterwards
    /// (`Main._speedBeforeTheAlert`) — so the one thing this flag still decides is **whether the
    /// panel waits for a person or fades by itself**. ⭐ *A field named for a behaviour it no
    /// longer has is how a settled decision comes undone (D240, half a rename).*
    /// </para>
    /// <para>
    /// ⚠️ <b>The distinction is "is there anything to do about it?"</b> and not "is it
    /// important?". Losing a technique is important and is a log line, because there is nothing
    /// to be done at the moment it happens. <b>Keep the bar for waiting at what the player must
    /// answer</b>, or this flag becomes a way to make everything urgent.
    /// </para>
    /// </remarks>
    public bool WaitsToBeDismissed { get; init; } = true;
}

namespace Bclone.Sim.World;

/// <summary>
/// Whether a building may go here, and if not, why not — in words.
/// </summary>
/// <remarks>
/// <para>
/// <b>A refusal is a sentence, not a red square</b>, to the same standard
/// <c>JobReason</c> already holds: the player must be able to read why. "The ground is
/// under water" and "no route from the village" are different problems with different
/// answers, and collapsing them into "invalid" would be the shrug D15 rejected for
/// labour.
/// </para>
/// <para>
/// <b>Warnings are separate from refusals</b>, and that separation is D43. A site that
/// is legal but a long way from work is the player's call, told plainly at the time —
/// they may build there. A site under water is not a call, it is an impossibility.
/// </para>
/// </remarks>
public readonly record struct PlacementVerdict(bool Allowed, string Reason, string Warning)
{
    public static PlacementVerdict Fine => new(true, string.Empty, string.Empty);

    public static PlacementVerdict No(string reason) => new(false, reason, string.Empty);

    public static PlacementVerdict Yes(string warning) => new(true, string.Empty, warning);

    /// <summary>Whether the player is being told something they may want to act on.</summary>
    public bool HasWarning => Warning.Length > 0;
}

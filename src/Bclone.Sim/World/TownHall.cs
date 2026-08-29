namespace Bclone.Sim.World;

/// <summary>
/// The hall the village raised to the people who founded it (`specs/town-hall.md`, D251, D252).
/// </summary>
/// <remarks>
/// <para>
/// <b>⭐ A FIFTH THING A BUILDING CAN BE</b> — not a store, not a workplace, not a home, not a
/// library. It holds no goods, employs nobody, houses nobody and shelves no techniques.
/// <b>Its entire output is information about the village itself</b>, which is why the sim's copy
/// of it is this small: <em>everything the tabs will show is already in the world</em>, and the
/// building is the door rather than the filing cabinet.
/// </para>
/// <para>
/// <b>⛔ ONE, EVER</b> (D38 — `building-placement.md` has listed the town hall as <em>the</em>
/// example of a build-once building since long before one existed). It is therefore held as a
/// nullable field on <c>SimWorld</c> rather than in a list: <b>a list that can only hold one thing
/// is a list somebody will eventually put two things in.</b>
/// </para>
/// <para>
/// ⚠️ <b>Nothing here grants anything, and that is load-bearing rather than a scope choice</b>
/// (`tech-tree.md §7f.1`). The day this type gains a property the sim reads for a bonus, the
/// collections have become the ratchet §11 exists to prevent.
/// </para>
/// </remarks>
public sealed class TownHall
{
    private GridPos _position;

    /// <summary>Where it stands.</summary>
    /// <remarks>
    /// <b>⚠️ <c>init</c> for building it, <see cref="MoveTo"/> for moving it</b> — the same split
    /// the library carries. <b>Moving it is the answer to putting it in the wrong place</b>, and
    /// the reason demolishing it does not re-offer the gift: <em>the founders only die once.</em>
    /// </remarks>
    public required GridPos Position { get => _position; init => _position = value; }

    /// <summary>Move it. Only a finished relocation may.</summary>
    internal void MoveTo(GridPos to) => _position = to;

    /// <summary>What the village calls it.</summary>
    public required string Name { get; init; }

    /// <summary>The tick it began standing.</summary>
    /// <remarks>
    /// <b>The one fact about the building that is not about the village</b>, and it is here because
    /// the collections tab (slice 2) is a list of dates and this is the date the list itself
    /// begins. ⚠️ It is <b>not</b> the date the founders died — that is
    /// <c>SimWorld.SaidTheFoundersAreGone</c>'s moment, and the two are separated by however long
    /// the player took to place it and the builders took to raise it.
    /// </remarks>
    public required ulong RaisedAtTick { get; init; }
}

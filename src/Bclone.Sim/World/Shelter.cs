namespace Bclone.Sim.World;

/// <summary>
/// What the tile a villager is standing on does for them in winter (D45).
/// </summary>
/// <remarks>
/// <para>
/// The three rows of D45's table, as a type. Cold is <b>positional</b> after that
/// decision: a villager freezes because of where they have been standing and whether a
/// fire was burning there, not because of a counter attached to their household.
/// </para>
/// <para>
/// Buildings occupy exactly one tile in this game, so "inside" is a position equality.
/// That is crude and it is also the whole model — there is no interior to be in.
/// </para>
/// </remarks>
public enum Shelter
{
    /// <summary>
    /// Open ground: walking, a berry patch, a tree stand. The dangerous state, and the
    /// one clothing will eventually remove (D45, D19/D39).
    /// </summary>
    Outdoors = 0,

    /// <summary>
    /// A roof with no fire under it — a woodcutter's hut, a market, a shed, or a home
    /// whose household has run out of firewood.
    /// </summary>
    /// <remarks>
    /// This is the middle row of D45's table and the reason the middle row exists: it
    /// slows the cold rather than reversing it. A building is not a hearth.
    /// </remarks>
    Roof = 1,

    /// <summary>
    /// A home with somebody living in it and firewood still in the pile. The only state
    /// that gives anything back.
    /// </summary>
    /// <remarks>
    /// <b>Any occupied home, not only your own.</b> A neighbour with a fire lit does not
    /// turn a freezing man away, and the alternative encodes a cruelty the player cannot
    /// act on: two houses side by side, one warm and one not, and the sim insisting you
    /// freeze in the correct doorway. It also keeps this off the household-accounting
    /// road D45 exists to leave.
    /// </remarks>
    Fire = 2,
}

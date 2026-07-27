namespace Bclone.Sim.World;

/// <summary>What a store building is for.</summary>
public enum StoreKind
{
    /// <summary>Food, and only food (D32).</summary>
    Granary = 0,

    /// <summary>Materials — logs, firewood, and the stone, lumber and cloth to come.</summary>
    Shed = 1,
}

/// <summary>
/// A building that exists to hold things: a granary, or a materials shed.
/// </summary>
/// <remarks>
/// <para>
/// <b>Food and materials are deliberately kept in separate buildings</b> (D32). One
/// undifferentiated village pile would quietly delete the inequality D14 exists to
/// create — "one family starving beside a thriving neighbour" is the story
/// per-household food was introduced for, and a single shared store makes it
/// unexpressible.
/// </para>
/// <para>
/// Two buildings keep it, and change what inequality is <em>made of</em>. It stops
/// being about whose larder it is, which is an accident of which house a forager
/// happened to be born in, and becomes about <b>distance and hands</b>: a household on
/// the wrong end of the valley, or with nobody spare to send, eats worse than its
/// neighbours. That is spatial and watchable, and it feeds straight into catchment
/// (§2.2) and desire paths (§2.6) rather than sitting off to one side.
/// </para>
/// <para>
/// It is also the honest division. Food spoils and timber does not, which is why no
/// village has ever kept them in the same shed — and why <em>spoilage</em> is named in
/// D32 as the counterweight that stops a granary being a bank that solves winter
/// permanently. Not built here; it belongs with Phase 2's environment work.
/// </para>
/// </remarks>
public sealed class StoreBuilding
{
    public required int Id { get; init; }

    public required StoreKind Kind { get; init; }

    /// <summary>A place name, so the log reads "the granary" not "Store 2".</summary>
    public required string Name { get; init; }

    public required GridPos Position { get; init; }

    /// <summary>Goods held here, and how much of them will fit.</summary>
    /// <remarks>
    /// The capacity is set when the building is founded, from
    /// <see cref="VillageEconomy.GranaryCapacity"/> or
    /// <see cref="VillageEconomy.ShedCapacity"/> — never typed in.
    /// </remarks>
    public Stockpile Store { get; init; } = new();

    /// <summary>Whether this building will hold a given kind of goods.</summary>
    /// <remarks>
    /// The whole difference between the two buildings, in one method. Deliberately a
    /// plain question rather than a set of flags: a modder adding a good should be able
    /// to see at a glance where it can go.
    /// </remarks>
    public bool Accepts(Goods goods) => Kind switch
    {
        StoreKind.Granary => goods == Goods.Food,
        StoreKind.Shed => goods is Goods.Logs or Goods.Firewood,
        _ => false,
    };
}

/// <summary>The kinds of goods a store can hold. Stone, lumber and cloth land here.</summary>
public enum Goods
{
    Food = 0,
    Logs = 1,
    Firewood = 2,
}

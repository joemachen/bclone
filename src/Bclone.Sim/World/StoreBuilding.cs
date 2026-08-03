namespace Bclone.Sim.World;

/// <summary>What a store building is for.</summary>
public enum StoreKind
{
    /// <summary>Food, and only food (D32).</summary>
    Granary = 0,

    /// <summary>Materials — logs, firewood, and the stone, lumber and cloth to come.</summary>
    Shed = 1,

    /// <summary>
    /// The market — food and firewood, kept near the homes (D14).
    /// </summary>
    /// <remarks>
    /// The one store that is also a <see cref="Workplace"/>, because it is the one
    /// whose contents arrive by somebody's work rather than by producers dropping
    /// things off. It holds both kinds deliberately: the point is to be the short trip
    /// for whatever a household is short of, and sending them to the granary for one
    /// and the shed for the other would put the walking back.
    /// </remarks>
    Market = 2,

    /// <summary>
    /// The wagon the founders arrived in — the one building the player did not raise (D64).
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>It exists so that D30 survives the cold start.</b> Goods live in buildings, and a
    /// village with nothing built would otherwise have nowhere for the founders' supplies to
    /// be. A cart is the honest answer: it is a place, it arrived with them, and it is the
    /// story §0 already tells about exiles.
    /// </para>
    /// <para>
    /// <b>It holds anything</b>, because the founders' load is not sorted into a granary's
    /// worth of food and a shed's worth of timber — it is what they could carry. Small, and
    /// <b>demolishable once empty</b> (Joe): a wagon standing in the square forever is a
    /// monument to a slice rather than a building.
    /// </para>
    /// <para>
    /// <b>It is not shelter.</b> <c>SimWorld.ShelterAt</c> knows only about homes, so
    /// standing at the cart is standing outdoors and the cold counts it as such.
    /// </para>
    /// </remarks>
    Cart = 3,

    /// <summary>
    /// A storage pile — goods stacked on cleared ground, and the first thing the player
    /// places (D76).
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>It costs nothing but the ground it stands on</b>, which is what makes it the right
    /// first move: a village with nowhere to put things cannot begin, and asking it to build
    /// a shed out of timber it has nowhere to stack is a circle. Joe's opening starts here
    /// for the same reason Banished's does.
    /// </para>
    /// <para>
    /// <b>It holds anything, like the cart, so only its capacity restrains it.</b> That
    /// capacity is derived rather than typed in — a pile large enough to be the granary
    /// would delete the reason to build one.
    /// </para>
    /// <para>
    /// <b>Not shelter.</b> <c>SimWorld.ShelterAt</c> knows only about homes, so standing at
    /// a pile is standing outdoors and the cold counts it.
    /// </para>
    /// </remarks>
    Pile = 4,
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
/// It is also the honest division: no village has ever kept its food and its timber in
/// the same shed, because one of them rots.
/// </para>
/// <para>
/// <b>The sim does not model that rot, deliberately</b> (D37). Spoilage was proposed as
/// the counterweight that stops a granary being a bank which permanently solves winter,
/// and it was cut — it is a tax the player takes no decision about, and it punishes a
/// well-run town as hard as a careless one. <see cref="Stockpile.Capacity"/> answers the
/// same danger better: there is no unlimited bank to have, because the building will not
/// hold it.
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

        // Materials, which is what the shed has always been for — stone and tools join
        // logs and firewood on exactly that reading (D32). The market does not take
        // them: it exists to be the short trip for whatever a HOUSEHOLD is short of
        // (D14, D78), and no household consumes either yet.
        StoreKind.Shed => goods is Goods.Logs or Goods.Firewood or Goods.Stone or Goods.Tools
            or Goods.Iron,
        StoreKind.Market => goods is Goods.Food or Goods.Firewood,

        // Everything, because the founders' load was never sorted — it is what they could
        // carry (D64). A heap on the ground does not specialise either: the two stores that
        // take anything are the two that are not really buildings, and in both cases it is
        // the SIZE rather than the rules that stops them being the granary.
        StoreKind.Cart => true,
        StoreKind.Pile => true,
        _ => false,
    };
}

/// <summary>The kinds of goods a store can hold. Lumber and cloth land here next.</summary>
/// <remarks>
/// <para>
/// <b>The values are hashed by position</b> — <see cref="Stockpile"/> indexes by them and
/// <see cref="StockLimits.Kinds"/> is ordered by them — so a good may be <em>appended</em>
/// but never renumbered. Renumbering would silently reinterpret every saved limit and every
/// golden hash as being about a different good.
/// </para>
/// <para>
/// <b>Stone and tools exist before anything makes or spends them, deliberately.</b> Joe's
/// call: do the indexed-goods refactor when the first new good lands, not before and not
/// after — so the good is what proves the refactor, and the machinery around it (limits,
/// hashing, the panel, what a shed will take) lands in one piece rather than being
/// retrofitted per good. <b>Stone gets its source in slice C3</b>, where the map generator
/// places rock and the player paints it to be cleared; <b>tools get their workshop</b> when
/// D17 comes off the shelf. Until then the only tools in the world are the ones the founders
/// arrived with.
/// </para>
/// </remarks>
public enum Goods
{
    Food = 0,
    Logs = 1,
    Firewood = 2,

    /// <summary>What a building past a log hut costs (D63). Placed by the map, not grown.</summary>
    Stone = 3,

    /// <summary>
    /// What the founders brought and nobody can yet replace (D17, D64).
    /// </summary>
    Tools = 4,

    /// <summary>Ore, cleared from a visible seam or mined (D84, D90).</summary>
    Iron = 5,
}

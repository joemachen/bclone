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
    /// <b>It holds anything EXCEPT timber</b> (D90 step 4, landed with D96). It used to hold
    /// everything, because the founders' load is not sorted into a granary's worth of food
    /// and a shed's worth of timber — it is what they could carry. <b>Logs are the one thing
    /// that plausibly will not fit</b>, and Joe's opening turns on that: you cannot take
    /// timber until you have somewhere to put it, which is what gives the storage pile its
    /// reason back.
    /// </para>
    /// <para>
    /// <b>⭐ It also fixes a village that could be strangled by its own woodpile, in silence
    /// (D89).</b> Measured over forty years on the shipped config, the arm with trees painted
    /// and no store placed ran <b>4 → 6 → 2</b> against 8 or 9 for every other arm: the cart
    /// filled with timber — 677 logs of its 1,200 by year five — and the food it crowded out
    /// never arrived, 164 against 400+. The village then stopped having children and aged out
    /// with <b>zero starved and zero frozen</b>, which is a legibility failure before it is a
    /// balance one and precisely the uncozy death §0.1 rules out. <b>A cart that cannot hold
    /// logs cannot be strangled by them</b> — the constraint is structural rather than a
    /// warning the player has to read.
    /// </para>
    /// <para>
    /// Small, and <b>demolishable once empty</b> (Joe): a wagon standing in the square forever
    /// is a monument to a slice rather than a building.
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
    public required Stockpile Store { get; init; }

    /// <summary>
    /// The run's goods catalogue — <b>what this building asks whether it can hold a thing</b>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>⭐ Held here rather than passed to <see cref="Accepts"/></b> (D210, slice 1b), because
    /// <c>Accepts</c> has <b>46 call sites</b> and a store building has eight — the cheaper seam
    /// by a wide margin, and the one that keeps every caller reading the way it did.
    /// </para>
    /// </remarks>
    public required GoodsCatalog Catalog { get; init; }

    /// <summary>
    /// Which goods the player has told this building to take, as a bitmask. Zero means
    /// they have not said, which is every building until somebody says otherwise.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Joe, D141:</b> <i>"user should be able to set which materials are stored in which
    /// buildings — e.g. a given storage pile will only accept logs, another only firewood,
    /// another only iron ore. Set at the building level."</i>
    /// </para>
    /// <para>
    /// <b>⭐ IT CAN ONLY EVER NARROW, NEVER WIDEN, and that is the whole safety of it.</b> The
    /// kind still decides what is possible — a granary holds food because that is what a
    /// granary is (D32), and no amount of clicking makes one hold iron. The mask filters what
    /// the kind already allows. So this control cannot produce a store that breaks the model,
    /// only one that declines part of what it could have held.
    /// </para>
    /// <para>
    /// <b>Player intent, so it is sim state and it is hashed</b> — but <b>sparsely</b>, the
    /// same shape as <see cref="Workplace.QueueRank"/> and <see cref="Workplace.Mode"/>: zero
    /// is "no opinion", zero mixes nothing, and a village where nobody has touched a filter
    /// hashes exactly as it did before filters existed. No golden moves for its existing.
    /// </para>
    /// <para>
    /// <b>⚠️ An all-refusing store is allowed, and is the player's to make.</b> D42's rule is
    /// that the game says so once and then obeys; goods with nowhere to go are set down on the
    /// ground (D96) and now say so in the Overview and on the map (D134, D140), so the mistake
    /// is visible and reversible rather than silent.
    /// </para>
    /// </remarks>
    /// <remarks>
    /// <b>⭐ WIDENED FROM <c>int</c> TO <c>long</c> (D210, slice 1b), AND THE OLD WIDTH WAS A
    /// CEILING NOBODY HAD COUNTED.</b> One bit per good, with <see cref="Spoken"/> formerly at bit
    /// 30 — so **good 30 would have set the sentinel**, and a store the player had never touched
    /// would report that they had. Not a crash: a filter that switches itself on. At 64 bits with
    /// the sentinel at 62, the ceiling is 62 goods, which is comfortably past the ~35 the content
    /// pass needs.
    /// </remarks>
    public long AllowedGoods { get; set; }

    /// <summary>
    /// Set on <see cref="AllowedGoods"/> the moment the player touches the filter, so that
    /// "takes nothing" is a different state from "has not said".
    /// </summary>
    /// <remarks>
    /// <b>⚠️ WITHOUT THIS, SWITCHING OFF THE LAST GOOD SWITCHED EVERYTHING BACK ON.</b> Zero
    /// means no opinion, so clearing the final bit landed straight back on the default and a
    /// shed the player had just told to take nothing accepted everything again — while the
    /// game had said out loud that it would take nothing. Caught by
    /// <c>AStoreThatWillTakeNothingSaysSoOnce</c> on its first run, which is what that guard is
    /// for: the empty case is exactly where a bitmask sentinel goes wrong.
    /// </remarks>
    public const long Spoken = 1L << 62;

    /// <summary>Whether the player's filter permits this good. True when they have not said.</summary>
    public bool PlayerAllows(Goods goods) =>
        AllowedGoods == 0 || (AllowedGoods & (1L << (int)goods)) != 0;

    /// <summary>Whether this building will hold a given kind of goods, here and now.</summary>
    /// <remarks>
    /// The whole difference between the buildings, in one method — narrowed by whatever the
    /// player has said (D141). Deliberately a plain question rather than a set of flags: a
    /// modder adding a good should be able to see at a glance where it can go.
    /// </remarks>
    public bool Accepts(Goods goods) => PlayerAllows(goods) && KindAccepts(goods);

    /// <summary>
    /// What this kind of building can hold at all, before the player narrows it (D141).
    /// </summary>
    /// <remarks>
    /// Public because two callers need the question asked without the player's filter in the
    /// way: <c>SetStoreAccepts</c>, to refuse widening past what the kind allows, and the view,
    /// to decide which buttons to offer — a filter that hid its own "turn it back on" button
    /// would be a one-way door.
    /// </remarks>
    public bool CanEverHold(Goods goods) => KindAccepts(goods);

    /// <summary>
    /// Whether this building is somewhere the village <b>keeps</b> things, as opposed to
    /// somewhere it <b>hands them out</b> (D199).
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Joe's distinction, in his words:</b> *"I want to separate the actual storage buildings
    /// (storage pile, granary, shed, warehouse, etc) from the market (distribution building)."*
    /// The market is where goods are put **near the people who need them**; everywhere else is
    /// where the village's production is **kept**.
    /// </para>
    /// <para>
    /// <b>⛔ IT EXISTS BECAUSE THE MARKET WAS QUIETLY BECOMING A SECOND GRANARY.</b>
    /// <c>StoreForTheLoad</c> sends a producer's load to the nearest store of the right kind and
    /// then, if that is full, to <em>anything that accepts it</em> — and the market accepts food
    /// and firewood. Measured over thirty years, it sat **600 above** what the village's homes
    /// need, none of it carried there on purpose. That is exactly what
    /// <c>market_stock_per_household</c>'s own config comment says must not happen: *"a market is
    /// a short trip, not a second granary."*
    /// </para>
    /// <para>
    /// <b>⭐ It does not stop a MARKETER putting things there</b>, which is the whole of the
    /// market's job — including household overflow, because §14.3's *"in"* direction is carried
    /// by a marketer too. *A trader may stock it and empty a family's larder into it; a forager
    /// coming home with berries may not.*
    /// </para>
    /// <para>
    /// <b>Expressed as a property of the building rather than a list at each call site</b>, so a
    /// warehouse — Joe names one — is storage the day it exists, with nothing to remember.
    /// </para>
    /// </remarks>
    public bool IsStorage => Kind != StoreKind.Market;

    /// <summary>
    /// What this kind of building holds — <b>read off the good's row, not a switch</b>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>⭐ THIS WAS A SWITCH OVER <see cref="StoreKind"/> NAMING GOODS ON THE RIGHT-HAND
    /// SIDE</b> (D210), so adding a good meant editing a method in the sim — which is precisely
    /// what `goods-catalog.md §2.1` forbids and what made the row decorative until now. Every
    /// rule it encoded is preserved, as <c>stored_by</c> on each row:
    /// </para>
    /// <list type="bullet">
    /// <item><b>Granary</b> — food, and only food (D32).</item>
    /// <item><b>Shed</b> — materials, which is what it has always been for: logs, firewood,
    /// stone, tools and iron. The market does not take them, because it exists to be the short
    /// trip for whatever a HOUSEHOLD is short of (D14, D78).</item>
    /// <item><b>Market</b> — food and firewood, near the homes.</item>
    /// <item><b>Cart</b> — <b>everything but timber</b> (D90 step 4). The founders' load was never
    /// sorted; it is what they could carry (D64), and logs are the one thing that plausibly will
    /// not fit in a wagon you arrived in. That single refusal is what makes the storage pile
    /// load-bearing — you cannot take timber until you have somewhere to put it — and what stops
    /// D89's silent strangling, where a cart packed with logs crowded out the food and the village
    /// aged out with nobody starved and nobody frozen.</item>
    /// </list>
    /// <para>
    /// <b>⛔ The pile stays in code, and that is not an oversight.</b> It takes anything, and its
    /// <em>size</em> rather than its rules is what stops it being the granary — so *"takes
    /// everything"* is a statement about the pile, not a fact about any good. Putting it in every
    /// row would mean a modder had to remember to opt into it, and forgetting would silently
    /// narrow a heap.
    /// </para>
    /// </remarks>
    private bool KindAccepts(Goods goods) =>
        Kind == StoreKind.Pile || Catalog.StoredBy(goods, Kind);
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

namespace Bclone.Sim.World;

/// <summary>
/// Goods held somewhere — a home's larder, a workshop's buffer, a granary, a shed.
/// </summary>
/// <remarks>
/// <para>
/// <b>A store belongs to a place, not to a family</b> (D30). Everything held a
/// stockpile of its own before that, which is why every goods bug so far has had the
/// same shape: the right stuff in the wrong house. Logs piled up where the logger
/// lived and no home was ever built (D25); firewood piled up where the woodcutter
/// lived and the household next door froze beside it (D29). Both were patched
/// locally, twice, because there was nowhere to put things.
/// </para>
/// <para>
/// A home's store is still special in one way, and it is not negotiable: <b>a meal
/// must be takeable where the villager is standing</b>. Phase 0 killed a villager who
/// starved mid-gather beside a full larder, and decided that a survival game may kill
/// you for bad decisions but never for a scheduling artifact (D10). So homes keep a
/// working larder, and the only question storage answers is how it gets refilled.
/// </para>
/// <para>
/// <b>Indexed by <see cref="Goods"/>, not three hand-written fields</b> (C2, Joe's call:
/// refactor when the first new good lands, not before and not after). It held
/// <c>Food</c>, <c>Logs</c> and <c>Firewood</c> as separate properties with a matching
/// <c>Add</c>/<c>Receive</c>/<c>TryTake</c> trio each and a <c>Held</c> that summed them
/// by name — nine methods and a hard-coded sum that every new good would have had to be
/// threaded through by hand. <b>Adding stone meant remembering nine places</b>, and this
/// project's most repeated bug is code that kept reading the old shape (D25, D29, D48,
/// D57, D76, D79, D81). One array, one method per verb, and a new good is an enum value.
/// </para>
/// <para>
/// <b>The named readers stay</b> — <see cref="Food"/>, <see cref="Logs"/>,
/// <see cref="Firewood"/>. They are what the state hash, the panel and most of the suite
/// ask for, they read as English at the call site, and unlike the old <em>mutators</em>
/// they cannot go stale: there is one array underneath them now, so a reader and the
/// store can no longer disagree.
/// </para>
/// </remarks>
public sealed class Stockpile
{
    /// <summary>How many kinds of goods exist. Grows with <see cref="Goods"/>.</summary>
    /// <remarks>
    /// Read off the enum rather than typed, so adding a good cannot leave an array one
    /// short — which would be an <c>IndexOutOfRangeException</c> in the middle of the
    /// sim rather than a compile error.
    /// </remarks>
    public static readonly int Kinds = System.Enum.GetValues<Goods>().Length;

    private readonly int[] _held = new int[Kinds];

    /// <summary>
    /// How much of each good this store has ever taken in <em>as production</em>.
    /// </summary>
    /// <remarks>
    /// Deliberately not incremented by <see cref="Receive"/> — see its remarks. These
    /// numbers are claims about a life and a household, and inflating them by counting
    /// gifts is how the village once appeared to consume more logs than it had felled.
    /// </remarks>
    private readonly int[] _produced = new int[Kinds];

    /// <summary>
    /// How much this store can hold in total, across every kind of goods.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Total, not per good</b>, because it is physical room: a shed packed with logs
    /// has nowhere to stack firewood, and being made to choose is the interesting part.
    /// A per-good cap would be three independent shelves that never compete, which is
    /// bookkeeping wearing a constraint's clothes.
    /// </para>
    /// <para>
    /// Unlimited by default, and that default is deliberate. A home larder and a
    /// workplace buffer are <em>not</em> capped in this slice — the granary and the shed
    /// are what the pressure is about (spec §5, slice 5), and giving every store a
    /// number invites four made-up numbers instead of one derived one. Home larders get
    /// their cap with the market, where a short fetch is the answer to a small larder.
    /// </para>
    /// </remarks>
    public int Capacity { get; init; } = int.MaxValue;

    /// <summary>Everything held here, of every kind. What <see cref="Capacity"/> limits.</summary>
    public int Held
    {
        get
        {
            int total = 0;
            for (int i = 0; i < _held.Length; i++)
            {
                total += _held[i];
            }

            return total;
        }
    }

    /// <summary>Room left. Zero when full; never negative.</summary>
    public int FreeSpace
    {
        get
        {
            int free = Capacity - Held;
            return free < 0 ? 0 : free;
        }
    }

    /// <summary>Whether this store has no room for anything more.</summary>
    public bool IsFull => FreeSpace == 0;

    /// <summary>How much of one good is on hand. Never negative — asserted, not hoped.</summary>
    public int this[Goods goods] => _held[Index(goods)];

    /// <summary>How much of one good was ever produced here, for the epitaph.</summary>
    public int Produced(Goods goods) => _produced[Index(goods)];

    /// <summary>Food on hand.</summary>
    public int Food => _held[(int)Goods.Food];

    /// <summary>
    /// Felled timber on hand.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Logs are the <b>raw</b> half of wood (D29). They are what a logger brings back
    /// from a tree stand, and they are spent on two things: raising buildings, and
    /// feeding the woodcutter's hut that turns them into <see cref="Firewood"/>.
    /// </para>
    /// <para>
    /// Held per household but <b>drawn village-wide</b> — building already works that
    /// way (D25), because logs piling up in the logger's own house where nobody could
    /// spend them meant no home was ever built.
    /// </para>
    /// </remarks>
    public int Logs => _held[(int)Goods.Logs];

    /// <summary>
    /// Firewood on hand — the fuel a household burns to get through winter.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The <b>processed</b> half of wood (D29), made from <see cref="Logs"/> by a
    /// woodcutter. Deliberately a separate resource from logs rather than a flag on
    /// one: it is the output of the project's first processing chain, it will be
    /// traded (§2.4), and it will be distributed by the market (D14).
    /// </para>
    /// <para>
    /// Held per household and <b>consumed at home</b>, unlike logs. That asymmetry is
    /// the point — a family freezing beside a warm neighbour has to be expressible,
    /// which is the same argument D14 makes about food.
    /// </para>
    /// </remarks>
    public int Firewood => _held[(int)Goods.Firewood];

    /// <summary>Total food ever gathered here, for the epitaph.</summary>
    public int LifetimeGathered => _produced[(int)Goods.Food];

    /// <summary>Total logs ever felled here.</summary>
    public int LifetimeLogsFelled => _produced[(int)Goods.Logs];

    /// <summary>Total firewood ever cut here.</summary>
    public int LifetimeFirewoodCut => _produced[(int)Goods.Firewood];

    /// <summary>
    /// Put newly produced goods in, up to what will fit. Returns how much was
    /// <em>actually</em> taken.
    /// </summary>
    /// <remarks>
    /// <b>The return value is the whole point and it must not be ignored.</b> A store
    /// with a capacity can refuse, and goods that a store refused are still in
    /// somebody's arms — dropping them on the floor would break the conservation
    /// guarantee (spec §8) in exactly the direction that is hardest to notice, since
    /// the total only ever falls. Callers deposit what fits and keep the rest.
    /// </remarks>
    public int Add(Goods goods, int amount)
    {
        int accepted = Clamp(goods, amount, nameof(amount));
        _held[(int)goods] += accepted;
        _produced[(int)goods] += accepted;
        return accepted;
    }

    /// <summary>
    /// Take in goods that somebody else produced — a gift, or a delivery.
    /// </summary>
    /// <remarks>
    /// <b>Deliberately not <see cref="Add"/>.</b> The lifetime counters mean "this
    /// household produced this much", and routing shared goods through <c>Add</c> made
    /// them mean "this much passed through the door" instead. Every transfer was
    /// counted as fresh production by the receiver on top of the giver, so the totals
    /// inflated with every gift — the village appeared to consume more logs than it
    /// had ever felled, which is how this was found. It also quietly inflated the
    /// figure in a villager's epitaph, which is worse: that number is a claim about a
    /// life.
    /// </remarks>
    public int Receive(Goods goods, int amount)
    {
        int accepted = Clamp(goods, amount, nameof(amount));
        _held[(int)goods] += accepted;
        return accepted;
    }

    /// <summary>Take goods if there are enough, changing nothing otherwise.</summary>
    public bool TryTake(Goods goods, int amount)
    {
        if (amount < 0)
        {
            throw new System.ArgumentOutOfRangeException(
                nameof(amount), $"Cannot take negative {Name(goods)} ({amount}).");
        }

        int index = Index(goods);
        if (_held[index] < amount)
        {
            return false;
        }

        _held[index] -= amount;
        return true;
    }

    private int Clamp(Goods goods, int amount, string parameterName)
    {
        if (amount < 0)
        {
            throw new System.ArgumentOutOfRangeException(
                parameterName, $"Cannot add negative {Name(goods)} ({amount}).");
        }

        Index(goods);
        int free = FreeSpace;
        return amount < free ? amount : free;
    }

    private static int Index(Goods goods)
    {
        int index = (int)goods;
        if (index < 0 || index >= Kinds)
        {
            throw new System.ArgumentOutOfRangeException(
                nameof(goods), $"There is no such good as {goods}.");
        }

        return index;
    }

    /// <summary>
    /// The good's name for an exception message — <b>deliberately NOT the catalogue's word</b>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>⭐ THIS WAS A SWITCH, AND EVERY ARM OF IT PRODUCED EXACTLY WHAT ITS OWN DEFAULT ARM
    /// ALREADY DID</b> (D210). <c>Goods.Food =&gt; "food"</c> beside
    /// <c>_ =&gt; goods.ToString().ToLowerInvariant()</c> — three hand-written arms restating the
    /// fallback. It was **the second of two places carrying those same three words**, the other
    /// being <c>SimWorld</c>, which is D148 and D188's finding in code.
    /// </para>
    /// <para>
    /// <b>A <see cref="Stockpile"/> deliberately does not reach the catalogue.</b> It is a bare
    /// array held by every store, household and cart in the game, and threading a catalogue into
    /// all of them to spell a word in a <em>developer-facing exception</em> would be the tail
    /// wagging the dog. The player never sees this string.
    /// </para>
    /// </remarks>
    private static string Name(Goods goods) => goods.ToString().ToLowerInvariant();
}

// ⭐ `TreeStand` AND `FoodSource` ARE DELETED HERE (D159), which three comments elsewhere in
// this codebase had already claimed was true. Phase 0's single berry patch and the generator's
// tree stands went in step C (`forests-and-gathering.md` slice 5); these two classes outlived
// them because `FoodSource` was still holding one *static* predicate, and `TreeStand` was
// holding nothing at all. The predicate is `SeasonRules.IsGatherable` now — see `Season.cs`.

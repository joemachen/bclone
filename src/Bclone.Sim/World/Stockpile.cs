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
/// </remarks>
public sealed class Stockpile
{
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
    public int Held => Food + Logs + Firewood;

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

    /// <summary>Food on hand. Never negative — asserted, not hoped.</summary>
    public int Food { get; private set; }

    /// <summary>Total food ever gathered, for the epitaph.</summary>
    public int LifetimeGathered { get; private set; }

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
    public int Logs { get; private set; }

    /// <summary>Total logs ever felled.</summary>
    public int LifetimeLogsFelled { get; private set; }

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
    public int Firewood { get; private set; }

    /// <summary>Total firewood ever cut.</summary>
    public int LifetimeFirewoodCut { get; private set; }

    /// <summary>
    /// Put food in, up to what will fit. Returns how much was <em>actually</em> taken.
    /// </summary>
    /// <remarks>
    /// <b>The return value is the whole point and it must not be ignored.</b> A store
    /// with a capacity can refuse, and goods that a store refused are still in
    /// somebody's arms — dropping them on the floor would break the conservation
    /// guarantee (spec §8) in exactly the direction that is hardest to notice, since
    /// the total only ever falls. Callers deposit what fits and keep the rest.
    /// </remarks>
    public int Add(int amount)
    {
        if (amount < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(amount), $"Cannot add negative food ({amount}).");
        }

        int accepted = amount < FreeSpace ? amount : FreeSpace;
        Food += accepted;
        LifetimeGathered += accepted;
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
    /// <remarks>
    /// Where capacity binds, goods are taken in the order they are named — food, then
    /// logs, then firewood. Fixed rather than clever, because a store deciding for
    /// itself which of someone's goods to prefer is exactly the kind of hidden rule
    /// non-negotiable 1 is against. Returns the total accepted.
    /// </remarks>
    public int Receive(int food, int logs, int firewood)
    {
        if (food < 0 || logs < 0 || firewood < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(food), $"Cannot receive negative goods ({food}, {logs}, {firewood}).");
        }

        return ReceiveFood(food) + ReceiveLogs(logs) + ReceiveFirewood(firewood);
    }

    /// <summary>Take in food somebody else produced. Returns how much fitted.</summary>
    public int ReceiveFood(int amount)
    {
        int accepted = Clamp(amount, nameof(amount));
        Food += accepted;
        return accepted;
    }

    /// <summary>Take in logs somebody else produced. Returns how much fitted.</summary>
    public int ReceiveLogs(int amount)
    {
        int accepted = Clamp(amount, nameof(amount));
        Logs += accepted;
        return accepted;
    }

    /// <summary>Take in firewood somebody else produced. Returns how much fitted.</summary>
    public int ReceiveFirewood(int amount)
    {
        int accepted = Clamp(amount, nameof(amount));
        Firewood += accepted;
        return accepted;
    }

    private int Clamp(int amount, string parameterName)
    {
        if (amount < 0)
        {
            throw new ArgumentOutOfRangeException(parameterName, $"Cannot receive negative goods ({amount}).");
        }

        return amount < FreeSpace ? amount : FreeSpace;
    }

    /// <summary>Put felled logs in, up to what will fit. Returns how much was taken.</summary>
    public int AddLogs(int amount)
    {
        if (amount < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(amount), $"Cannot add negative logs ({amount}).");
        }

        int accepted = amount < FreeSpace ? amount : FreeSpace;
        Logs += accepted;
        LifetimeLogsFelled += accepted;
        return accepted;
    }

    /// <summary>Take logs if there are enough, changing nothing otherwise.</summary>
    public bool TryTakeLogs(int amount)
    {
        if (amount < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(amount), $"Cannot take negative logs ({amount}).");
        }

        if (Logs < amount)
        {
            return false;
        }

        Logs -= amount;
        return true;
    }

    /// <summary>Put split firewood in, up to what will fit. Returns how much was taken.</summary>
    public int AddFirewood(int amount)
    {
        if (amount < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(amount), $"Cannot add negative firewood ({amount}).");
        }

        int accepted = amount < FreeSpace ? amount : FreeSpace;
        Firewood += accepted;
        LifetimeFirewoodCut += accepted;
        return accepted;
    }

    /// <summary>Take firewood if there is enough, changing nothing otherwise.</summary>
    public bool TryTakeFirewood(int amount)
    {
        if (amount < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(amount), $"Cannot take negative firewood ({amount}).");
        }

        if (Firewood < amount)
        {
            return false;
        }

        Firewood -= amount;
        return true;
    }

    /// <summary>Take food if there is enough. Returns false without changing
    /// anything if there is not.</summary>
    public bool TryTake(int amount)
    {
        if (amount < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(amount), $"Cannot take negative food ({amount}).");
        }

        if (Food < amount)
        {
            return false;
        }

        Food -= amount;
        return true;
    }
}

/// <summary>
/// A stand of trees. Unlike the berry patch, timber can be cut year-round —
/// which is exactly why it is worth having someone on it in winter.
/// </summary>
public sealed class TreeStand
{
    public required GridPos Position { get; init; }

    public required int YieldPerCut { get; init; }
}

/// <summary>Where food is foraged. Barren in winter.</summary>
public sealed class FoodSource
{
    public required GridPos Position { get; init; }

    public required int YieldPerGather { get; init; }

    /// <summary>
    /// Winter is the pressure in Phase 0, and food scarcity is its only weapon —
    /// there is deliberately no separate cold/warmth stat (spec §3).
    /// </summary>
    public static bool IsGatherable(Season season) => season != Season.Winter;
}

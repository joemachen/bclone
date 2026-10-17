using Bclone.Sim.World;

namespace Bclone.Sim.Core;

/// <summary>
/// ⭐ The region (D594, commit 4 of `specs/trade.md §11`; §4.2–4.3): three neighbours with their own stocks and needs,
/// asleep until the village has a trading post, stepped once a season after that; and barter rates derived from them.
/// </summary>
/// <remarks>
/// <para>
/// <b>Neighbours with reasons</b> (Joe, D587 call 7): a good a neighbour makes recovers an eighth of the way back to its
/// size each season — what it sold, it makes again; a good it wants is used up an eighth of its size each season — what it
/// bought, it eats (<c>region_seasons_to_recover</c>). So a rate moves because a stock moved, and the region panel can say why.
/// </para>
/// <para>
/// ⛔ <b>Once a season, never per tick</b> (CLAUDE.md), and <b>asleep until a post stands</b> (`trade.md §11.1`): a village
/// that never builds one mixes nothing for the region, and the goldens hold. The rates are derived and never hashed (D335).
/// </para>
/// </remarks>
public sealed partial class SimWorld
{
    /// <summary>
    /// The tick the region woke — the first season turn with a trading post standing; 0 while it sleeps (D594).
    /// </summary>
    public ulong RegionAwakeSince { get; internal set; }

    /// <summary>Whether a finished trading post stands — what wakes the region.</summary>
    public bool APostStands
    {
        get
        {
            for (int i = 0; i < Workplaces.Count; i++)
            {
                if (Workplaces[i] is { Kind: JobKind.Merchant, IsSite: false, Closed: false })
                {
                    return true;
                }
            }

            return false;
        }
    }

    /// <summary>The store a trading post keeps its goods in — the post's own building, on its own tile.</summary>
    public StoreBuilding? PostStoreOf(Workplace post)
    {
        ArgumentNullException.ThrowIfNull(post);
        return StoreAt(post.Tile) is { Kind: StoreKind.TradingPost } store ? store : null;
    }

    /// <summary>
    /// The first good at a post with no keep order on it — what a boat left, going home (id order, the determinism rule).
    /// Null when there is none. The merchant's errand and the quota's ask both read this one copy.
    /// </summary>
    public Goods? GoodToBringHome(StoreBuilding post)
    {
        ArgumentNullException.ThrowIfNull(post);
        for (int g = 0; g < post.Store.Slots; g++)
        {
            var goods = (Goods)g;
            if (post.Store[goods] > 0 && TradeKeep.For(goods) is null)
            {
                return goods;
            }
        }

        return null;
    }

    /// <summary>The first good the village holds above its keep order with room for it at the post — what goes down to sell.</summary>
    public Goods? GoodToSell(StoreBuilding post)
    {
        ArgumentNullException.ThrowIfNull(post);
        for (int g = 0; g < post.Store.Slots; g++)
        {
            var goods = (Goods)g;
            if (AboveTheKeep(goods) > 0 && post.RoomFor(goods) > 0)
            {
                return goods;
            }
        }

        return null;
    }

    /// <summary>
    /// What the village holds of a good above its <i>keep N</i> — 0 with no order. ⛔ The most a merchant may carry down:
    /// what is at the post is not in <see cref="InStores"/>, so this never reaches below the keep.
    /// </summary>
    public int AboveTheKeep(Goods goods) => TradeKeep.For(goods) is int keep ? Math.Max(0, InStores(goods) - keep) : 0;

    /// <summary>On a season's first tick: wake the region if a post now stands, or step its stocks if it is awake.</summary>
    internal void TendTheRegion()
    {
        if (Clock.DayOfSeason != 1 || Tick % (ulong)Config.TicksPerDay != 0UL || Config.Neighbours.Count == 0)
        {
            return;
        }

        if (RegionAwakeSince == 0)
        {
            if (APostStands)
            {
                RegionAwakeSince = Tick == 0 ? 1UL : Tick;
                SeedTheRegion();
            }

            return;
        }

        StepTheRegion();
    }

    /// <summary>The region as it is the season it wakes: everything a neighbour makes at its size, everything it wants gone.</summary>
    private void SeedTheRegion()
    {
        for (int n = 0; n < Config.Neighbours.Count; n++)
        {
            NeighbourRow row = Config.Neighbours[n];
            NeighbourStock stock = NeighbourStocks[n];
            foreach (MaterialCost made in row.Makes)
            {
                stock.Held[(int)made.Goods] = made.Amount;
            }

            stock.Animals = row.MakesAnimals;
        }
    }

    /// <summary>
    /// A season: what each neighbour makes comes back <c>region_seasons_to_recover</c>'s share of the way to its size;
    /// what it wants is used that share of its size. Held between nothing and double its size.
    /// </summary>
    private void StepTheRegion()
    {
        int share = Config.RegionSeasonsToRecover;
        for (int n = 0; n < Config.Neighbours.Count; n++)
        {
            NeighbourRow row = Config.Neighbours[n];
            NeighbourStock stock = NeighbourStocks[n];
            foreach (MaterialCost made in row.Makes)
            {
                stock.Held[(int)made.Goods] = Recover(stock.Held[(int)made.Goods], made.Amount, share);
            }

            foreach (MaterialCost wanted in row.Wants)
            {
                int held = stock.Held[(int)wanted.Goods] - Math.Max(1, wanted.Amount / share);
                stock.Held[(int)wanted.Goods] = Math.Clamp(held, 0, wanted.Amount * 2);
            }

            stock.Animals = Recover(stock.Animals, row.MakesAnimals, share);
        }

        static int Recover(int held, int size, int share)
        {
            if (held >= size)
            {
                return Math.Min(held, size * 2);
            }

            return held + Math.Max(1, (size - held) / share);
        }
    }

    // ---------------------------------------------------------------
    //  Barter rates — derived, never hashed (D335)
    // ---------------------------------------------------------------

    /// <summary>
    /// What a neighbour asks, in worth a unit, for a good it makes: dearer as it runs short, cheaper as it piles up —
    /// <c>trade_worth</c> × between half and double (`trade.md §4.3`). Null for a good it does not make.
    /// </summary>
    public int? AskOf(int neighbour, Goods goods)
    {
        NeighbourRow row = Config.Neighbours[neighbour];
        foreach (MaterialCost made in row.Makes)
        {
            if (made.Goods == goods)
            {
                return Worth(goods) * Scarcity(NeighbourStocks[neighbour].Held[(int)goods], made.Amount) / 100;
            }
        }

        return null;
    }

    /// <summary>
    /// What a neighbour offers, in worth a unit, for a good it wants: up to double while it is short, as little as half
    /// once it has plenty (`trade.md §4.3`). Null for a good it does not want.
    /// </summary>
    public int? OfferOf(int neighbour, Goods goods)
    {
        NeighbourRow row = Config.Neighbours[neighbour];
        foreach (MaterialCost wanted in row.Wants)
        {
            if (wanted.Goods == goods)
            {
                return Worth(goods) * Scarcity(NeighbourStocks[neighbour].Held[(int)goods], wanted.Amount) / 100;
            }
        }

        return null;
    }

    /// <summary>
    /// The rate for selling <paramref name="sell"/> to a neighbour for <paramref name="buy"/>: how many of one buys how
    /// many of the other, in the smallest whole numbers — or null when the neighbour does not want the one or make the
    /// other. ⭐ Always within 1:4 and 4:1 of the goods' worths (half to double on each side).
    /// </summary>
    public (int Give, int Get)? RateFor(int neighbour, Goods sell, Goods buy)
    {
        if (OfferOf(neighbour, sell) is not int offer || AskOf(neighbour, buy) is not int ask || offer <= 0 || ask <= 0)
        {
            return null;
        }

        int divisor = GreatestCommonDivisor(offer, ask);
        return (ask / divisor, offer / divisor);
    }

    /// <summary>
    /// A rate in the village's words, with its reason (`trade.md §4.3`): <i>"Ashford is short of firewood and has plenty
    /// of stone: 3 firewood buys 1 stone."</i> Null when the neighbour does not want the one or make the other.
    /// </summary>
    public string? SayTheRate(int neighbour, Goods sell, Goods buy)
    {
        if (RateFor(neighbour, sell, buy) is not (int give, int get))
        {
            return null;
        }

        NeighbourRow row = Config.Neighbours[neighbour];
        int[] held = NeighbourStocks[neighbour].Held;
        string wants = held[(int)sell] < SizeOf(row.Wants, sell) ? "is short of" : "has enough";
        string makes = held[(int)buy] >= SizeOf(row.Makes, buy) ? "has plenty of" : "is running low on";
        return $"{NeighbourName(neighbour)} {wants} {GoodsCatalog.NameOf(sell)} and {makes} {GoodsCatalog.NameOf(buy)}: "
            + $"{give} {GoodsCatalog.NameOf(sell)} buys {get} {GoodsCatalog.NameOf(buy)}.";

        static int SizeOf(IReadOnlyList<MaterialCost> rows, Goods goods)
        {
            foreach (MaterialCost row in rows)
            {
                if (row.Goods == goods)
                {
                    return row.Amount;
                }
            }

            return 0;
        }
    }

    /// <summary>
    /// A neighbour's name: the town names after the village's own, in turn — a pure function of the seed, never hashed,
    /// as the village's own name is not (<see cref="Name"/>).
    /// </summary>
    public string NeighbourName(int neighbour)
    {
        int count = Config.TownNames.Count;
        return Config.TownNames[(int)((Seed % (ulong)count + 1UL + (ulong)neighbour) % (ulong)count)];
    }

    /// <summary>A good's base worth in barter — its row's <c>trade_worth</c>.</summary>
    private int Worth(Goods goods) => GoodsCatalog[goods].TradeWorth * 100;

    /// <summary>Half to double: 200 % with none held, 100 % at its size, 50 % at half again or more.</summary>
    private static int Scarcity(int held, int size)
    {
        if (size <= 0)
        {
            return 100;
        }

        return Math.Clamp(200 - (100 * held / size), 50, 200);
    }

    private static int GreatestCommonDivisor(int a, int b)
    {
        while (b != 0)
        {
            (a, b) = (b, a % b);
        }

        return a < 1 ? 1 : a;
    }
}

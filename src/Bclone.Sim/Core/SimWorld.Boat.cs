using Bclone.Sim.World;

namespace Bclone.Sim.Core;

/// <summary>
/// ⭐ The boat (D595, commit 5 of `specs/trade.md §11`; §4.4–4.5): a clock by hash, three ticks, and a settling of the
/// standing orders the day it docks.
/// </summary>
/// <remarks>
/// <para>
/// <b>A clock by hash, never a draw</b> (the knock's shape, D344): the year (two or three after the last boat went), the
/// day (so the whole visit falls in spring to autumn) and the neighbour (either end of the river — the overland one's
/// caravan waits for carts) are <see cref="NameHash.BoatRoll"/> of the seed and the visit count. The same seed brings the
/// same boats, and no stream moves.
/// </para>
/// <para>
/// <b><see cref="Boat"/> is always the next boat once the region is awake</b> — scheduled when the region wakes, and again
/// the tick the last one has sailed off the map — so <i>when is the next boat due</i> is a read, not a second fact.
/// </para>
/// <para>
/// ⭐ <b>Settling is barter at the day's rate</b>: the post's goods for sale that the neighbour wants (up to the hold) are
/// the village's credit; the buy orders are met from the boat's cargo, good by good, as far as the credit pays; then the
/// boat takes, good by good, just enough to cover what it left — the last unit may overpay by less than its own worth.
/// Every unit is conserved: what left the post the neighbour gained, what came the neighbour lost. ⛔ Nothing is sold for
/// nothing — with no buy order the boat takes nothing — and ⛔ a good with a keep order is never left (it would sit at
/// the post for sale).
/// </para>
/// </remarks>
public sealed partial class SimWorld
{
    /// <summary>
    /// The post's standing order for animals, <i>buy up to M head</i> across the village's herds (D595, `trade.md §4.6`) —
    /// null with no order. Hashed sparsely, saved.
    /// </summary>
    public int? TradeBuyHead { get; internal set; }

    /// <summary>Ticks a boat sails on the map each way — in from its edge, and out past the post.</summary>
    public ulong BoatSailTicks => (ulong)(Config.TradeBoatSailDays * Config.TicksPerDay);

    /// <summary>Ticks a boat stays tied up at the post.</summary>
    public ulong BoatDockTicks => (ulong)(Config.TradeBoatDockDays * Config.TicksPerDay);

    /// <summary>
    /// Whether the boat is tied up at the post now — it has docked (and settled) and has not yet left. Read between ticks:
    /// the tick it docks has run, the tick it leaves has not.
    /// </summary>
    public bool BoatIsDocked => Boat is TradeBoat boat && Tick > boat.DocksAtTick && Tick <= boat.LeavesAtTick;

    /// <summary>
    /// The boat after <paramref name="after"/> — visit number <paramref name="visit"/> (`trade.md §4.4`): in the year two or
    /// three after <paramref name="after"/>'s, on a day that lets it sail in, tie up and sail out before winter, from one
    /// end of the river or the other. Null when no neighbour lies on the river. A pure function of the seed.
    /// </summary>
    public TradeBoat? NextBoat(ulong after, int visit)
    {
        int rivers = 0;
        for (int n = 0; n < Config.Neighbours.Count; n++)
        {
            rivers += Config.Neighbours[n].Direction == NeighbourDirection.Overland ? 0 : 1;
        }

        if (rivers == 0)
        {
            return null;
        }

        int pick = NameHash.BoatRoll(Seed, visit, 2, rivers - 1);
        int neighbour = 0;
        for (int n = 0; n < Config.Neighbours.Count; n++)
        {
            if (Config.Neighbours[n].Direction != NeighbourDirection.Overland && pick-- == 0)
            {
                neighbour = n;
                break;
            }
        }

        ulong ticksPerYear = (ulong)Config.TicksPerYear;
        ulong year = (after / ticksPerYear) + (ulong)Config.TradeBoatGapYearsFloor
            + (ulong)NameHash.BoatRoll(Seed, visit, 0, Config.TradeBoatGapYearsSpread);
        int lastDay = (3 * Config.DaysPerSeason) - (2 * Config.TradeBoatSailDays) - Config.TradeBoatDockDays - 1;
        ulong appears = (year * ticksPerYear) + (ulong)(NameHash.BoatRoll(Seed, visit, 1, lastDay) * Config.TicksPerDay);
        return new TradeBoat
        {
            NeighbourId = neighbour,
            AppearsAtTick = appears,
            DocksAtTick = appears + BoatSailTicks,
            LeavesAtTick = appears + BoatSailTicks + BoatDockTicks,
        };
    }

    /// <summary>
    /// Set the post's order for animals (a player input). ⛔ Refused while the village has no pasture to put a beast on —
    /// and the refusal says so (`trade.md §4.6`). Null clears it, always.
    /// </summary>
    public PlacementVerdict SetTradeBuyHead(int? head)
    {
        if (head is int wanted)
        {
            if (wanted < 0)
            {
                return PlacementVerdict.No("A number of beasts cannot be less than none.");
            }

            if (PastureHeadRoom() + HeadKept() == 0)
            {
                return PlacementVerdict.No(
                    "There is nowhere to put a beast — paint a pasture round a barn first, and a boat can bring cattle.");
            }
        }

        TradeBuyHead = head;
        return PlacementVerdict.Fine;
    }

    /// <summary>
    /// <i>Send the boat on</i> (Joe, D587 call 6): a docked boat leaves on the next tick. A player input like any other —
    /// its leaving tick moves, so it is in the hash. Refused with no boat at the post.
    /// </summary>
    public PlacementVerdict SendTheBoatOn()
    {
        if (!BoatIsDocked || Boat is not TradeBoat boat)
        {
            return PlacementVerdict.No("No boat is tied up at the post.");
        }

        Boat = boat with { LeavesAtTick = Tick };
        return PlacementVerdict.Fine;
    }

    /// <summary>When the next boat is due, as far as the post knows — the post's window (`trade.md §4.5`).</summary>
    public string SayWhenTheBoatIsDue()
    {
        if (Boat is not TradeBoat boat)
        {
            return "No boat will come until a trading post stands.";
        }

        string name = NeighbourName(boat.NeighbourId);
        if (BoatIsDocked)
        {
            ulong days = (boat.LeavesAtTick - Tick + (ulong)Config.TicksPerDay - 1) / (ulong)Config.TicksPerDay;
            return $"The boat from {name} is tied up at the post for {days} more day{(days == 1 ? string.Empty : "s")}.";
        }

        if (Tick >= boat.AppearsAtTick && Tick <= boat.DocksAtTick)
        {
            return $"A boat from {name} is on the river, coming to the post.";
        }

        if (Tick > boat.LeavesAtTick)
        {
            return $"The boat from {name} has sailed on.";
        }

        ulong ticksPerYear = (ulong)Config.TicksPerYear;
        ulong yearsAhead = (boat.AppearsAtTick / ticksPerYear) - (Tick / ticksPerYear);
        return yearsAhead switch
        {
            0 => "A boat is expected before the winter.",
            1 => "No boat is expected before next spring.",
            2 => "No boat is expected before the spring after next.",
            _ => $"No boat is expected for {yearsAhead} years.",
        };
    }

    /// <summary>
    /// What a boat carries, derived (`trade.md §4.3`, Joe's <i>"random assortment"</i>): a share of everything its
    /// neighbour makes, 25–75 % of what it holds, picked by hash of the seed, the visit and the good — and its beasts the
    /// same way. Never hashed: it is a function of the neighbour's stock and the visit count.
    /// </summary>
    public (int[] Goods, int Animals) CargoOf(TradeBoat boat)
    {
        ArgumentNullException.ThrowIfNull(boat);
        NeighbourRow row = Config.Neighbours[boat.NeighbourId];
        NeighbourStock stock = NeighbourStocks[boat.NeighbourId];
        var goods = new int[stock.Held.Length];
        foreach (MaterialCost made in row.Makes)
        {
            int g = (int)made.Goods;
            goods[g] = stock.Held[g] * NameHash.CargoShare(Seed, BoatsEverCame, g, 25, 75) / 100;
        }

        int animals = stock.Animals * NameHash.CargoShare(Seed, BoatsEverCame, stock.Held.Length, 25, 75) / 100;
        return (goods, animals);
    }

    /// <summary>What a neighbour asks, in worth, for one of its beasts — <c>trade_worth</c> × its scarcity, as a good's.</summary>
    public int? AskOfAnimals(int neighbour)
    {
        NeighbourRow row = Config.Neighbours[neighbour];
        if (row.MakesAnimals <= 0 || Config.Animals.Count == 0)
        {
            return null;
        }

        return Config.Animals[0].TradeWorth * Scarcity(NeighbourStocks[neighbour].Animals, row.MakesAnimals);
    }

    /// <summary>Each tick: the boat comes into view, docks and settles, leaves, and once it is off the map the next is set.</summary>
    internal void SailTheBoat()
    {
        if (Boat is not TradeBoat boat)
        {
            return;
        }

        string name = NeighbourName(boat.NeighbourId);
        bool fromUpriver = Config.Neighbours[boat.NeighbourId].Direction == NeighbourDirection.Upriver;
        // ⭐ The passing banner (Joe, D587 call 6): a moment the village does not stop for, and its log line (D596).
        if (Tick == boat.AppearsAtTick)
        {
            RaiseMoment(
                $"A boat from {name}",
                $"A boat from {name} is coming {(fromUpriver ? "down" : "up")} the river to the trading post. {Clock.SeasonAndYear()}.",
                stops: false,
                Logging.LogCategory.Ordinary);
        }

        if (Tick == boat.DocksAtTick)
        {
            // Settled before it is counted, so the cargo is the one the post's window showed while it sailed in.
            SettleTheOrders(boat, name);
            BoatsEverCame++;
        }

        if (Tick == boat.LeavesAtTick)
        {
            Narrate($"The boat from {name} is leaving, on {(fromUpriver ? "down" : "up")} the river.");
        }

        if (Tick >= boat.LeavesAtTick + BoatSailTicks)
        {
            Boat = NextBoat(boat.LeavesAtTick + BoatSailTicks, BoatsEverCame);
        }
    }

    /// <summary>The day it docks: the orders settled at the day's rate, every unit conserved, and said in one line.</summary>
    private void SettleTheOrders(TradeBoat boat, string name)
    {
        StoreBuilding? post = null;
        for (int i = 0; i < Workplaces.Count && post is null; i++)
        {
            if (Workplaces[i] is { Kind: JobKind.Merchant, IsSite: false, Closed: false } standing)
            {
                post = PostStoreOf(standing);
            }
        }

        if (post is null)
        {
            Narrate($"The boat from {name} tied up where no trading post stands, and will sail on.");
            return;
        }

        int n = boat.NeighbourId;
        int slots = post.Store.Slots;
        (int[] cargo, int cargoAnimals) = CargoOf(boat);

        // The day's rates, read once before anything moves.
        var offer = new int[slots];
        var forSale = new int[slots];
        int hold = Config.TradeBoatHold;
        long credit = 0;
        for (int g = 0; g < slots; g++)
        {
            var goods = (Goods)g;
            if (TradeKeep.For(goods) is not null && post.Store[goods] > 0 && OfferOf(n, goods) is int o && o > 0)
            {
                offer[g] = o;
                forSale[g] = Math.Min(post.Store[goods], hold);
                hold -= forSale[g];
                credit += (long)forSale[g] * o;
            }
        }

        // What the orders ask for, from the cargo, as far as the credit pays — never a good the village sells.
        var left = new int[slots];
        int room = Config.TradeBoatHold;
        int free = post.Store.FreeSpace; // one count for every good — what the boat leaves shares the post's room
        long cost = 0;
        for (int g = 0; g < slots; g++)
        {
            var goods = (Goods)g;
            if (TradeBuy.For(goods) is int upTo && TradeKeep.For(goods) is null && AskOf(n, goods) is int ask && ask > 0)
            {
                int wanted = upTo - InStores(goods) - post.Store[goods];
                int units = Math.Min(Math.Min(wanted, cargo[g]), Math.Min(room, free));
                units = (int)Math.Min(units, (credit - cost) / ask);
                if (units > 0)
                {
                    left[g] = units;
                    room -= units;
                    free -= units;
                    cost += (long)units * ask;
                }
            }
        }

        int beasts = 0;
        if (TradeBuyHead is int head && AskOfAnimals(n) is int beastAsk && beastAsk > 0)
        {
            beasts = Math.Min(Math.Min(head - HeadKept(), PastureHeadRoom()), cargoAnimals);
            beasts = (int)Math.Min(beasts, (credit - cost) / beastAsk);
            beasts = Math.Max(0, beasts);
            cost += (long)beasts * beastAsk;
        }

        if (cost == 0)
        {
            string why = credit == 0
                ? $"the post held nothing {name} wants"
                : "nobody had asked for anything it carried";
            Narrate($"The boat from {name} called and found nothing to trade — {why}. {Clock.SeasonAndYear()}.");
            return;
        }

        // The boat takes just enough to cover what it left, good by good; what is not needed stays at the post.
        var took = new int[slots];
        long owed = cost;
        for (int g = 0; g < slots && owed > 0; g++)
        {
            if (forSale[g] > 0)
            {
                int units = (int)Math.Min(forSale[g], (owed + offer[g] - 1) / offer[g]);
                took[g] = units;
                owed -= (long)units * offer[g];
            }
        }

        NeighbourStock stock = NeighbourStocks[n];
        for (int g = 0; g < slots; g++)
        {
            var goods = (Goods)g;
            if (took[g] > 0 && post.Store.TryTake(goods, took[g]))
            {
                stock.Held[g] += took[g];
            }

            if (left[g] > 0)
            {
                stock.Held[g] -= post.Put(goods, left[g]);
            }
        }

        stock.Animals -= beasts;
        LandTheBeasts(beasts);
        Narrate($"The boat from {name} took {Listed(took)} and left {Listed(left, beasts)}. {Clock.SeasonAndYear()}.");
    }

    /// <summary>Bought beasts to the herds with room, barn by barn in id order — never past what a pasture holds.</summary>
    private void LandTheBeasts(int beasts)
    {
        for (int i = 0; i < Workplaces.Count && beasts > 0; i++)
        {
            if (Workplaces[i] is { Kind: JobKind.Herdsman, IsSite: false, Closed: false } barn)
            {
                int landed = Math.Min(beasts, Math.Max(0, PastureFor(barn) - barn.HerdHead));
                barn.HerdHead += landed;
                beasts -= landed;
            }
        }
    }

    /// <summary>Head the village's standing herds hold.</summary>
    private int HeadKept()
    {
        int head = 0;
        for (int i = 0; i < Workplaces.Count; i++)
        {
            if (Workplaces[i] is { Kind: JobKind.Herdsman, IsSite: false } barn)
            {
                head += barn.HerdHead;
            }
        }

        return head;
    }

    /// <summary>Head the village's open barns' pastures could still take.</summary>
    private int PastureHeadRoom()
    {
        int room = 0;
        for (int i = 0; i < Workplaces.Count; i++)
        {
            if (Workplaces[i] is { Kind: JobKind.Herdsman, IsSite: false, Closed: false } barn)
            {
                room += Math.Max(0, PastureFor(barn) - barn.HerdHead);
            }
        }

        return room;
    }

    /// <summary>"60 firewood and 20 stone" — goods in id order, then beasts, in words.</summary>
    private string Listed(int[] units, int beasts = 0)
    {
        var parts = new List<string>();
        for (int g = 0; g < units.Length; g++)
        {
            if (units[g] > 0)
            {
                parts.Add($"{units[g]} {GoodsCatalog.NameOf((Goods)g)}");
            }
        }

        if (beasts > 0 && Config.Animals.Count > 0)
        {
            AnimalRow animal = Config.Animals[0];
            parts.Add($"{beasts} {(beasts == 1 ? animal.Name : animal.Plural)}");
        }

        return parts.Count switch
        {
            0 => "nothing",
            1 => parts[0],
            _ => string.Join(", ", parts.Take(parts.Count - 1)) + " and " + parts[^1],
        };
    }
}

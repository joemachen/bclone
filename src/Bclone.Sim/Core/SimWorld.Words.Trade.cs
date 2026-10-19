using Bclone.Sim.World;

namespace Bclone.Sim.Core;

/// <summary>
/// ⭐ What the barn's card and the post's window say (D596, commit 6 of `specs/trade.md §11`) — the sim's own words, so
/// the view never restates a rule (D142: a preview computed from a second copy of the condition is a preview that can
/// lie). And the post's orders as player inputs, with their refusals.
/// </summary>
public sealed partial class SimWorld
{
    /// <summary>Days of winter feeding the barn's hay covers for the herd it holds now; null with no herd.</summary>
    public int? HayDaysFor(Workplace barn)
    {
        ArgumentNullException.ThrowIfNull(barn);
        int perDay = Config.Animals.Count == 0 ? 0 : barn.HerdHead * Config.Animals[0].HayPerHeadPerWinterDay;
        return perDay == 0 ? null : HayHeldFor(barn) / perDay;
    }

    /// <summary>
    /// The barn's card status, in one sentence with its reason (`trade.md §11` row 6: <i>N head, hay for N days, why</i>).
    /// </summary>
    public string SayTheHerd(Workplace barn)
    {
        ArgumentNullException.ThrowIfNull(barn);
        string plural = Config.Animals.Count == 0 ? "beasts" : Config.Animals[0].Plural;
        int tiles = Zones.WorkGroundTiles(barn.Id);
        int room = PastureFor(barn);
        if (tiles == 0)
        {
            return $"No pasture — give the barn ground round it, and {plural} can graze there.";
        }

        if (barn.HerdHead == 0)
        {
            return APostStands
                ? $"No {plural} yet — {tiles} tiles of pasture for {room}; set the trading post to buy some, and a boat brings them."
                : $"No {plural} yet — {tiles} tiles of pasture for {room}; {plural} come by trade, from a trading post on the river.";
        }

        int days = HayDaysFor(barn) ?? 0;
        string head = $"{barn.HerdHead} head";
        if (Clock.IsWinter)
        {
            int left = Config.DaysPerSeason - Clock.DayOfSeason + 1;
            return days >= left
                ? $"{head} in for the winter — hay for {days} days, and {left} of winter left."
                : $"{head} in for the winter — hay for only {days} days with {left} left; a beast is lost each day it runs short.";
        }

        string grazing = $"{head} grazing {tiles} tiles (room for {room}) — hay for {days} winter days of {Config.DaysPerSeason}";
        if (HayHeldFor(barn) >= WinterHayWanted(barn))
        {
            return grazing + "; the winter's hay is in.";
        }

        if (barn.WorkerIds.Count == 0)
        {
            return grazing + "; nobody is cutting hay — a barn needs a herdsman.";
        }

        return HayIsShort(barn)
            ? grazing + "; the herdsman is cutting the pasture."
            : grazing + "; the pasture has been cut over this year.";
    }

    /// <summary>
    /// Set what the village keeps of a good — sell the rest at the post (a player input; null clears it). ⛔ Refused on a
    /// good the post is set to buy: one good cannot be bought and sold at once (D595 — a boat never leaves a good the
    /// village sells).
    /// </summary>
    public PlacementVerdict SetTradeKeep(Goods goods, int? keep)
    {
        if (keep is not null && TradeBuy.For(goods) is not null)
        {
            return PlacementVerdict.No(
                $"The post is set to buy {GoodsCatalog.NameOf(goods)} — clear that first; a good is bought or sold, not both.");
        }

        TradeKeep.Set(goods, keep);
        return PlacementVerdict.Fine;
    }

    /// <summary>
    /// Set how much of a good the post buys up to (a player input; null clears it). ⛔ Refused on a good the village sells;
    /// warned on a good no boat brings (only a river neighbour's boat calls — the overland caravan waits for carts).
    /// </summary>
    public PlacementVerdict SetTradeBuy(Goods goods, int? upTo)
    {
        if (upTo is not null && TradeKeep.For(goods) is not null)
        {
            return PlacementVerdict.No(
                $"The village sells {GoodsCatalog.NameOf(goods)} — clear what it keeps first; a good is bought or sold, not both.");
        }

        TradeBuy.Set(goods, upTo);
        if (upTo is null || ABoatCanBring(goods))
        {
            return PlacementVerdict.Fine;
        }

        return PlacementVerdict.Yes(
            $"No neighbour on the river makes {GoodsCatalog.NameOf(goods)}, so no boat will bring it — the order waits for carts.");
    }

    /// <summary>Whether a neighbour on the river makes this good — so a boat may carry it.</summary>
    public bool ABoatCanBring(Goods goods)
    {
        foreach (NeighbourRow row in Config.Neighbours)
        {
            if (row.Direction == NeighbourDirection.Overland)
            {
                continue;
            }

            foreach (MaterialCost made in row.Makes)
            {
                if (made.Goods == goods)
                {
                    return true;
                }
            }
        }

        return false;
    }

    /// <summary>
    /// ⭐ The region in the village's words (`trade.md §4.2`, commit 7: the region panel) — one paragraph a neighbour, its
    /// name beside it: which way it lies, what it has plenty of and is running low on, what it is short of, and the rates
    /// for the village's own orders, each with its reason (<see cref="SayTheRate"/>). Empty while the region sleeps.
    /// </summary>
    /// <remarks>
    /// Read from the neighbours' stocks as they stand: a make at or above its size is plenty and below it running low; a
    /// want below its size is short and at or above it enough — the same lines <see cref="SayTheRate"/> draws. Derived,
    /// never hashed (D335).
    /// </remarks>
    public IReadOnlyList<(string Name, string Words)> SayTheRegion()
    {
        var said = new List<(string Name, string Words)>();
        if (RegionAwakeSince == 0UL)
        {
            return said;
        }

        for (int n = 0; n < Config.Neighbours.Count; n++)
        {
            NeighbourRow row = Config.Neighbours[n];
            NeighbourStock stock = NeighbourStocks[n];
            var plenty = new List<string>();
            var low = new List<string>();
            var shortOf = new List<string>();
            var enough = new List<string>();
            foreach (MaterialCost made in row.Makes)
            {
                (stock.Held[(int)made.Goods] >= made.Amount ? plenty : low).Add(GoodsCatalog.NameOf(made.Goods));
            }

            if (row.MakesAnimals > 0 && Config.Animals.Count > 0)
            {
                (stock.Animals >= row.MakesAnimals ? plenty : low).Add(Config.Animals[0].Plural);
            }

            foreach (MaterialCost wanted in row.Wants)
            {
                (stock.Held[(int)wanted.Goods] < wanted.Amount ? shortOf : enough).Add(GoodsCatalog.NameOf(wanted.Goods));
            }

            var words = new List<string>
            {
                row.Direction switch
                {
                    NeighbourDirection.Upriver => "Up the river.",
                    NeighbourDirection.Downriver => "Down the river.",
                    _ => "Overland.",
                },
            };
            AddIfAny(words, "Has plenty of", plenty);
            AddIfAny(words, "Running low on", low);
            AddIfAny(words, "Short of", shortOf);
            AddIfAny(words, "Has enough", enough);

            var rates = new List<string>();
            for (int sell = 0; sell < TradeKeep.Slots; sell++)
            {
                for (int buy = 0; buy < TradeBuy.Slots; buy++)
                {
                    if (TradeKeep.For((Goods)sell) is not null && TradeBuy.For((Goods)buy) is not null
                        && SayTheRate(n, (Goods)sell, (Goods)buy) is string rate)
                    {
                        rates.Add(rate);
                    }
                }
            }

            bool ordered = TradeKeep.AnySet && TradeBuy.AnySet;
            words.Add(row.Direction == NeighbourDirection.Overland
                ? "No road reaches it yet — its trade waits for carts."
                : !ordered
                    ? "Set what the village sells and what it buys at the post, and its rates are said here."
                    : rates.Count == 0
                        ? "It wants none of what the village sells, or makes none of what it buys."
                        : string.Join(" ", rates));
            said.Add((NeighbourName(n), string.Join(" ", words)));
        }

        return said;

        static void AddIfAny(List<string> words, string lead, List<string> goods)
        {
            if (goods.Count > 0)
            {
                words.Add($"{lead} {ListedTogether(goods)}.");
            }
        }

        static string ListedTogether(List<string> goods) =>
            goods.Count == 1 ? goods[0] : string.Join(", ", goods.Take(goods.Count - 1)) + " and " + goods[^1];
    }

    /// <summary>
    /// The region in a line a neighbour, for the top of the post's window (Joe, D607: the window had <i>"no region"</i>):
    /// <i>"Ashbourne, up the river — plenty of logs, stone and iron; short of wheat and bread."</i> The full words, with
    /// the rates, are <see cref="SayTheRegion"/>'s. One line saying when it wakes while the region sleeps.
    /// </summary>
    public IReadOnlyList<string> SayTheRegionInBrief()
    {
        if (RegionAwakeSince == 0UL)
        {
            return new[] { "The region wakes the season after a trading post stands." };
        }

        var lines = new List<string>();
        for (int n = 0; n < Config.Neighbours.Count; n++)
        {
            NeighbourRow row = Config.Neighbours[n];
            NeighbourStock stock = NeighbourStocks[n];
            var plenty = row.Makes.Where(m => stock.Held[(int)m.Goods] >= m.Amount).Select(m => GoodsCatalog.NameOf(m.Goods)).ToList();
            if (row.MakesAnimals > 0 && Config.Animals.Count > 0 && stock.Animals >= row.MakesAnimals)
            {
                plenty.Add(Config.Animals[0].Plural);
            }

            var shortOf = row.Wants.Where(w => stock.Held[(int)w.Goods] < w.Amount).Select(w => GoodsCatalog.NameOf(w.Goods)).ToList();
            string where = row.Direction switch
            {
                NeighbourDirection.Upriver => "up the river",
                NeighbourDirection.Downriver => "down the river",
                _ => "overland, no road yet",
            };
            var parts = new List<string>();
            if (plenty.Count > 0)
            {
                parts.Add($"plenty of {Together(plenty)}");
            }

            if (shortOf.Count > 0)
            {
                parts.Add($"short of {Together(shortOf)}");
            }

            lines.Add($"{NeighbourName(n)}, {where} — {(parts.Count == 0 ? "nothing to spare and nothing wanted" : string.Join("; ", parts))}.");
        }

        return lines;

        static string Together(List<string> goods) =>
            goods.Count == 1 ? goods[0] : string.Join(", ", goods.Take(goods.Count - 1)) + " and " + goods[^1];
    }

    /// <summary>
    /// What the post's orders will do, in one sentence — the post's window (Joe, D596: <i>"a keep order alone sells
    /// nothing, and the post's window says to set what to buy"</i>).
    /// </summary>
    public string SayWhatThePostWillDo()
    {
        bool selling = TradeKeep.AnySet;
        bool buying = TradeBuy.AnySet || TradeBuyHead is not null;
        return (selling, buying) switch
        {
            (true, false) => "Nothing will be sold — a boat pays in goods, not coin. Set what to buy, and the boat takes "
                + "enough of what is for sale to pay for it.",
            (false, true) => "Nothing is for sale to pay with — set what the village keeps of a good, and the merchant "
                + "carries the rest down to the post.",
            (true, true) => "The merchant carries what is above each keep down to the post; the next boat takes enough of "
                + "it to pay for what you asked.",
            _ => "Set what the village keeps and what it buys — a boat trades one for the other.",
        };
    }
}

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

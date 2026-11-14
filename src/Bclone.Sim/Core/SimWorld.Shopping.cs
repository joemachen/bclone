using Bclone.Sim.Systems;
using Bclone.Sim.World;

namespace Bclone.Sim.Core;

/// <summary>
/// ⭐ The shop walk said (D744, `phase-5-exit-gate.md §4`; logistics' last piece — D668 outcome 2, D670: *"the brush and the
/// card still owe the player the sentence"*). Since D385 a household eats from the nearest <b>shop</b>, a market or a
/// storehouse, and in Joe's villages that walk was a fifth of every adult's ticks with nothing saying how far it was.
/// Read-only: nothing here moves a villager, so no golden can move.
/// </summary>
public sealed partial class SimWorld
{
    /// <summary>
    /// The shop a household would walk to for food from its home, and the walk in tiles — the shopper's own rule
    /// (<c>BehaviorSystem.FoodShopFrom</c>, a trip's worth for its cart or its arms), or, with nothing stocked anywhere,
    /// the nearest shop that gives food at all. Null with no home, or no shop it can reach.
    /// </summary>
    public (StoreBuilding Shop, int Tiles)? ShopOf(Household home)
    {
        ArgumentNullException.ThrowIfNull(home);
        if (home.HomeTile is not GridPos at)
        {
            return null;
        }

        int armfuls = home.HandcartUses > 0 ? Config.CartArmfuls : 1;
        int trip = Math.Max(1, BehaviorSystem.ATripsWorth(Config, TargetFoodFor(home), armfuls));
        StoreBuilding? shop = BehaviorSystem.FoodShopFrom(this, at, trip) ?? NearestShop(at);
        return shop is null || WalkInTiles(at, shop.Tile) is not int tiles ? null : (shop, tiles);
    }

    /// <summary>The home card's sentence: <i>"Granary 2, 14 tiles away"</i>, and <i>"— a long walk"</i> past the budget.</summary>
    public string? SayTheShopWalk(Household home) =>
        ShopOf(home) is (StoreBuilding shop, int tiles) ? ShopWalkSentence(shop.Name, tiles, VillageEconomy.MaxHomeToWorkTiles(Config)) : null;

    /// <summary>
    /// The sentence itself, pure: past <paramref name="budget"/> — the economy's own walk from a home to its work
    /// (<c>VillageEconomy.MaxHomeToWorkTiles</c>), derived, never typed — it says so (D112: a fence you can see, never one
    /// you cannot).
    /// </summary>
    public static string ShopWalkSentence(string shop, int tiles, int budget) =>
        tiles > budget ? $"{shop}, {tiles} tiles away — a long walk" : $"{shop}, {tiles} tiles away";

    /// <summary>The walk in tiles from a tile to the nearest shop that gives food, or null with none it can reach.</summary>
    public int? NearestShopWalk(GridPos from) =>
        NearestShop(from) is StoreBuilding shop ? WalkInTiles(from, shop.Tile) : null;

    /// <summary>The nearest store that gives any food — a market or a storehouse — by the walk, or null.</summary>
    private StoreBuilding? NearestShop(GridPos from)
    {
        StoreBuilding? best = null;
        int bestCost = int.MaxValue;
        for (int i = 0; i < StoreBuildings.Count; i++)
        {
            StoreBuilding store = StoreBuildings[i];
            if (!GivesAnyFood(store))
            {
                continue;
            }

            int cost = TravelCost.Cost(from, store.Tile);
            if (cost != TravelCostField.Unreachable && cost < bestCost)
            {
                bestCost = cost;
                best = store;
            }
        }

        return best;
    }

    private bool GivesAnyFood(StoreBuilding store)
    {
        IReadOnlyList<Goods> edible = GoodsCatalog.EdibleGoods;
        for (int i = 0; i < edible.Count; i++)
        {
            if (store.Gives(edible[i]))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>A walk in tiles by the one cost field (§2.6: slopes and worn paths priced in), rounded; null if unreachable.</summary>
    private int? WalkInTiles(GridPos from, GridPos to)
    {
        int cost = TravelCost.Cost(from, to);
        return cost == TravelCostField.Unreachable ? null : (cost + (TravelCostField.BaseTileCost / 2)) / TravelCostField.BaseTileCost;
    }
}

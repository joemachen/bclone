using Bclone.Sim.Core;
using Bclone.Sim.World;

namespace Bclone.Sim.Systems;

/// <summary>
/// ⭐ The merchant (D594, commit 4 of `specs/trade.md §11`; §4.5): carries what the village holds above its <i>keep N</i>
/// down to the trading post for the next boat, and carries anything at the post that nobody means to sell back to the
/// stores — what a boat left.
/// </summary>
/// <remarks>
/// <para>
/// ⛔ <b>Never below <i>keep N</i></b>: an armful is at most what the village holds above the order. Goods at the post do
/// not count as the village's — no ordinary store takes from it and no fetch reaches it (it is a store no good's
/// <c>stored_by</c> names), so carried down is set aside for sale.
/// </para>
/// <para>
/// Each state is one errand, walked and then done, as the herdsman's are: <c>CollectingForThePost</c> to the store that
/// holds the good, <c>StockingThePost</c> to the post, <c>FetchingFromThePost</c> to the post for what goes back.
/// </para>
/// </remarks>
public sealed partial class BehaviorSystem
{
    /// <summary>A merchant's day: what a boat left goes home first, then what is to be sold goes down.</summary>
    /// <returns>Whether an errand was begun.</returns>
    private static bool TryTrade(SimWorld world, Villager villager, Workplace post)
    {
        if (world.PostStoreOf(post) is not StoreBuilding store)
        {
            villager.WorkNote = $"{post.Name} has no store.";
            return false;
        }

        if (world.GoodToBringHome(store) is not null)
        {
            villager.WorkNote = string.Empty;
            villager.State = VillagerState.FetchingFromThePost;
            Travel(world, villager, post.Position, VillagerState.FetchingFromThePost);
            return true;
        }

        if (world.GoodToSell(store) is Goods selling
            && NearestStoreHolding(world, post.Tile, selling) is StoreBuilding source)
        {
            villager.WorkNote = string.Empty;
            villager.ErrandX = source.Tile.X;
            villager.ErrandY = source.Tile.Y;
            villager.State = VillagerState.CollectingForThePost;
            Travel(world, villager, source.Position, VillagerState.CollectingForThePost);
            return true;
        }

        villager.WorkNote = world.TradeKeep.AnySet
            ? $"Nothing above what the village keeps to carry to {post.Name}."
            : $"Nothing to carry to {post.Name} — set what the village keeps, and the rest is sold.";
        return false;
    }

    /// <summary>On arriving for a merchant's errand.</summary>
    /// <returns>Whether this was a merchant's arrival.</returns>
    private static bool ArriveToTrade(SimWorld world, Villager villager, VillagerState onArrival)
    {
        switch (onArrival)
        {
            case VillagerState.CollectingForThePost:
            {
                Workplace? post = WorkplaceOf(world, villager);
                StoreBuilding? postStore = post is null ? null : world.PostStoreOf(post);
                StoreBuilding? source = world.StoreAt(new GridPos(villager.ErrandX, villager.ErrandY));
                villager.ErrandX = 0;
                villager.ErrandY = 0;
                if (post is null || postStore is null || source is null || world.GoodToSell(postStore) is not Goods goods)
                {
                    Decide(world, villager);
                    return true;
                }

                int armful = Math.Min(world.Config.CarryCapacity, Math.Min(world.AboveTheKeep(goods), source.Store[goods]));
                armful = Math.Min(armful, postStore.RoomFor(goods));
                if (armful <= 0 || !source.Store.TryTake(goods, armful))
                {
                    Decide(world, villager);
                    return true;
                }

                villager.Carried.Receive(goods, armful);
                villager.State = VillagerState.StockingThePost;
                Travel(world, villager, post.Position, VillagerState.StockingThePost);
                return true;
            }

            case VillagerState.StockingThePost:
            {
                if (WorkplaceOf(world, villager) is Workplace post && world.PostStoreOf(post) is StoreBuilding store)
                {
                    for (int g = 0; g < villager.Carried.Slots; g++)
                    {
                        var goods = (Goods)g;
                        if (villager.Carried[goods] > 0)
                        {
                            villager.Carried.TryTake(goods, store.Put(goods, villager.Carried[goods]));
                        }
                    }
                }

                if (villager.IsCarrying)
                {
                    villager.State = VillagerState.HaulingToStore;
                    HaulOrSetDown(world, villager);
                    return true;
                }

                Decide(world, villager);
                return true;
            }

            case VillagerState.FetchingFromThePost:
            {
                if (WorkplaceOf(world, villager) is not Workplace post
                    || world.PostStoreOf(post) is not StoreBuilding store
                    || world.GoodToBringHome(store) is not Goods goods)
                {
                    Decide(world, villager);
                    return true;
                }

                int armful = Math.Min(world.Config.CarryCapacity, store.Store[goods]);
                if (!store.Store.TryTake(goods, armful))
                {
                    Decide(world, villager);
                    return true;
                }

                villager.Carried.Receive(goods, armful);
                villager.State = VillagerState.HaulingToStore;
                HaulOrSetDown(world, villager);
                return true;
            }

            default:
                return false;
        }
    }

    private static bool IsTradingAtThePost(VillagerState state) =>
        state is VillagerState.CollectingForThePost or VillagerState.StockingThePost or VillagerState.FetchingFromThePost;
}

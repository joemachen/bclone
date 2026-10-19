using Bclone.Sim.Core;
using Bclone.Sim.World;

namespace Bclone.Sim.Systems;

/// <summary>
/// ⭐ The herdsman (D593, commit 3 of `specs/trade.md §11`; `specs/livestock.md §0.1`): cut and carry hay in the
/// growing seasons, feed it out in winter, cull what the pasture cannot hold.
/// </summary>
/// <remarks>
/// <para>
/// One trade, two seasonal faces (`livestock.md §6.4`), and the farmer's shapes reused rather than new machinery: a
/// cut tile is a reaped tile (<c>reap_ticks</c>, tool and vigour), and the armful goes home on the ordinary haul errand —
/// hay is kept only by a barn, so the nearest store that takes it is the barn.
/// </para>
/// <para>
/// Each state is one errand, walked and then worked, as the farmer's <c>Reaping</c> is: <c>CuttingHay</c> at a pasture
/// tile, <c>FetchingHay</c> at the barn, <c>FeedingTheHerd</c> out on the pasture — outdoors, where D53's cold counts —
/// and <c>Culling</c> at the barn.
/// </para>
/// </remarks>
public sealed partial class BehaviorSystem
{
    /// <summary>A herdsman's day: a cull owed first, then winter's feeding, then the growing seasons' hay.</summary>
    /// <returns>Whether an errand was begun.</returns>
    private static bool TryKeepTheHerd(SimWorld world, Villager villager, Workplace barn)
    {
        if (world.CullIsOwed(barn))
        {
            villager.WorkNote = string.Empty;
            villager.State = VillagerState.Culling;
            Travel(world, villager, barn.Position, VillagerState.Culling);
            return true;
        }

        if (world.HerdWantsFeeding(barn))
        {
            villager.WorkNote = string.Empty;
            villager.State = VillagerState.FetchingHay;
            Travel(world, villager, barn.Position, VillagerState.FetchingHay);
            return true;
        }

        if (world.NextPastureToCut(barn) is GridPos tile)
        {
            villager.WorkNote = string.Empty;
            villager.ErrandX = tile.X;
            villager.ErrandY = tile.Y;
            villager.State = VillagerState.CuttingHay;
            Travel(world, villager, tile, VillagerState.CuttingHay);
            return true;
        }

        villager.WorkNote = barn.HerdHead <= 0
            ? $"{barn.Name} has no herd yet — a boat brings the first beasts."
            : world.Zones.WorkGroundTiles(barn.Id) == 0
                ? $"{barn.Name} has no pasture — paint some for it."
                : world.Clock.IsWinter
                    ? $"The herd at {barn.Name} has its hay out."
                    : $"The hay for {barn.Name}'s winter is in.";
        return false;
    }

    /// <summary>On arriving for a herdsman's errand: begin the work there, or take the armful.</summary>
    /// <returns>Whether this was a herdsman's arrival.</returns>
    private static bool ArriveToKeepTheHerd(SimWorld world, Villager villager, VillagerState onArrival)
    {
        switch (onArrival)
        {
            case VillagerState.CuttingHay:
                villager.State = VillagerState.CuttingHay;
                villager.ActionTicksRemaining = world.BeginWork(villager, JobKind.Herdsman, world.Config.ReapTicks);
                return true;

            case VillagerState.Culling:
                villager.State = VillagerState.Culling;
                villager.ActionTicksRemaining = world.BeginWork(villager, JobKind.Herdsman, world.Config.CullTicks);
                return true;

            case VillagerState.FetchingHay:
                if (WorkplaceOf(world, villager) is not Workplace barn
                    || world.BarnStoreOf(barn) is not StoreBuilding store
                    || world.WhereTheHayGoesOut(barn) is not GridPos pasture)
                {
                    Decide(world, villager);
                    return true;
                }

                int armful = Math.Min(world.Config.CarryCapacity, store.Store[Goods.Hay]);
                if (armful <= 0 || !store.Store.TryTake(Goods.Hay, armful))
                {
                    Decide(world, villager);
                    return true;
                }

                villager.Carried.Receive(Goods.Hay, armful);
                villager.ErrandX = pasture.X;
                villager.ErrandY = pasture.Y;
                villager.State = VillagerState.FeedingTheHerd;
                Travel(world, villager, pasture, VillagerState.FeedingTheHerd);
                return true;

            case VillagerState.FeedingTheHerd:
                villager.State = VillagerState.FeedingTheHerd;
                villager.ActionTicksRemaining = world.BeginWork(villager, JobKind.Herdsman, world.Config.HayFeedTicks);
                return true;

            default:
                return false;
        }
    }

    /// <summary>When a herdsman's work is done: the cut armful goes home, the hay is out, the beast is culled.</summary>
    /// <returns>Whether this was a herdsman's work.</returns>
    private static bool FinishKeepingTheHerd(SimWorld world, Villager villager)
    {
        switch (villager.State)
        {
            case VillagerState.CuttingHay:
            {
                var cut = new GridPos(villager.ErrandX, villager.ErrandY);
                villager.ErrandX = 0;
                villager.ErrandY = 0;
                if (WorkplaceOf(world, villager) is not Workplace barn || world.NextPastureToCut(barn) is null)
                {
                    Decide(world, villager);
                    return true;
                }

                world.PassTheCutTile(barn, cut);
                int hay = world.WithTool(villager, JobKind.Herdsman, world.Config.HayPerTile) * villager.Vigour / 100;
                villager.Carried.Receive(Goods.Hay, hay < 1 ? 1 : hay);
                villager.State = VillagerState.HaulingToStore;
                HaulOrSetDown(world, villager);
                return true;
            }

            case VillagerState.FeedingTheHerd:
            {
                villager.ErrandX = 0;
                villager.ErrandY = 0;
                int hay = villager.Carried.TakeAll(Goods.Hay);
                if (WorkplaceOf(world, villager) is Workplace barn)
                {
                    barn.HayPutOut += hay;
                }

                Decide(world, villager);
                return true;
            }

            case VillagerState.Culling:
                if (WorkplaceOf(world, villager) is Workplace herd)
                {
                    world.CullABeast(herd);
                }

                Decide(world, villager);
                return true;

            default:
                return false;
        }
    }

    private static bool IsHerding(VillagerState state) =>
        state is VillagerState.CuttingHay or VillagerState.FetchingHay or VillagerState.FeedingTheHerd
            or VillagerState.Culling;
}

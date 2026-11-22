# TEMPORARY measurement patch for W14 (`specs/market-stand.md §4`, ZzMarketStand ZZ_OWN=1): a marketer stocks the counter of the market they work at
# first. Applied, measured, then reverted by copying back a backup of BehaviorSystem.cs taken before it. The PATCHED SIM is never committed; this script is.
import sys
p = r"D:\Projects\bclone\src\Kith.Sim\Systems\BehaviorSystem.cs"
s = open(p, encoding="utf-8").read()

a1 = """        int bestShare = int.MaxValue;
        for (int i = 0; i < world.StoreBuildings.Count; i++)
        {
            StoreBuilding market = world.StoreBuildings[i];
            if (market.Kind != StoreKind.Market || market.Store.IsFull)
            {
                continue;
            }
"""
b1 = """        int bestShare = int.MaxValue;
        StoreBuilding? zzOwn = ZzOwnCounter ? ZzOwnCounterOf(world, villager) : null;
        if (zzOwn is not null && !world.CounterWantsStocking(zzOwn))
        {
            zzOwn = null;
        }

        for (int i = 0; i < world.StoreBuildings.Count; i++)
        {
            StoreBuilding market = world.StoreBuildings[i];
            if (market.Kind != StoreKind.Market || market.Store.IsFull || (zzOwn is not null && market != zzOwn))
            {
                continue;
            }
"""
a2 = """        if (WorkplaceOf(world, villager)?.Kind == JobKind.Marketer)
        {
            Goods carrying = TheLoad(villager);
            StoreBuilding? nearest = NearestCounterWithRoomFor("""
b2 = """        if (WorkplaceOf(world, villager)?.Kind == JobKind.Marketer)
        {
            Goods carrying = TheLoad(villager);
            if (ZzOwnCounter && ZzOwnCounterOf(world, villager) is StoreBuilding own && own.HasRoomFor(carrying)
                && HeldOf(own.Store, carrying) + villager.Carried[carrying] <= world.MarketStockLimit(own, carrying))
            {
                return own;
            }

            StoreBuilding? nearest = NearestCounterWithRoomFor("""
a3 = """    private static Workplace? WorkplaceOf(SimWorld world, Villager villager) =>
        world.FindWorkplace(villager.WorkplaceId);
"""
b3 = a3 + """
    // ⛔ TEMPORARY (W14 measurement) — reverted before any commit.
    internal static bool ZzOwnCounter;

    private static StoreBuilding? ZzOwnCounterOf(SimWorld world, Villager villager)
    {
        if (WorkplaceOf(world, villager) is not { Kind: JobKind.Marketer } works)
        {
            return null;
        }

        for (int i = 0; i < world.StoreBuildings.Count; i++)
        {
            if (world.StoreBuildings[i].Kind == StoreKind.Market && world.StoreBuildings[i].Tile == works.Tile)
            {
                return world.StoreBuildings[i];
            }
        }

        return null;
    }
"""
for a, b in ((a1, b1), (a2, b2), (a3, b3)):
    n = s.count(a)
    if n != 1:
        sys.exit(f"anchor count {n}: {a[:60]!r}")
for a, b in ((a1, b1), (a2, b2), (a3, b3)):
    s = s.replace(a, b)
open(p, "w", encoding="utf-8", newline="").write(s)
print("patched")

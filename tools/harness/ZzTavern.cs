using Kith.Sim.Config;
using Kith.Sim.Core;
using Kith.Sim.Persistence;
using Kith.Sim.World;
using Xunit;
using Xunit.Abstractions;

namespace Kith.Sim.Tests;

// ⛔ D835's MEASUREMENT HARNESS — kept in tools/harness/, COPIED into tests/Kith.Sim.Tests/ to run and DELETED from there
// before any commit (D420). `tavern.md §8` step 1: before `tavern_reach_tiles`, `tavern_every_days` or the comfort's `at`
// is typed, how far adults' homes are from where a tavern would stand, how often a rest begins, and how many of the
// people W2 loses a tavern would keep.
//
// Where a tavern would stand: the HOME a player would put it beside — the home whose walk to every other adult's home,
// summed, is least (the medoid, in the one cost field). Not the market: half of Joe's villages have none.
//
// Per save (the NEWEST .save in each folder under ZZ_SAVES, a COPY of Joe's):
//   ZZT  — adults, homes, the adults' walk to the medoid in tiles (buckets ≤4 5–8 9–12 13–16 17–24 25+), the medoid's
//          walk to the nearest warehouse (the keeper's), barley farms;
//   then ZZ_YEARS (4) stepped on:
//   ZZR  — rests begun a working adult-day (a step into Resting from anything else), and well trips begun;
//   ZZW  — adult-seasons by held weight (0 1 2 3+) within R and beyond it, for R = 8 12 16 24;
//   ZZL  — every adult who left: their walk to the medoid, and whether any of their last eight seasons read exactly the
//          line (2) — the season a tavern within reach would have eased to 1 and so broken the streak (a keep).
// Copy in; `dotnet test tests/Kith.Sim.Tests -c Release --filter ZzTavern --logger "console;verbosity=detailed"`; delete.
public sealed class ZzTavern
{
    private static readonly int[] Reaches = { 8, 12, 16, 24 };
    private readonly ITestOutputHelper _o;

    public ZzTavern(ITestOutputHelper o) => _o = o;

    [Theory]
    [MemberData(nameof(Runs))]
    public void Measure(string what)
    {
        if (what.Length == 0)
        {
            return;
        }

        SaveOpened opened = SaveFile.Open(what, ShippedConfig.Load());
        if (opened.Loop is not SimLoop loop)
        {
            _o.WriteLine($"ZZX {what} refused {opened.Refusal} {opened.Detail}");
            return;
        }

        SimWorld world = loop.World;
        SimConfig c = world.Config;
        string village = Path.GetFileName(Path.GetDirectoryName(what)) ?? what;

        // Homes and their adults.
        var adultsAt = new Dictionary<GridPos, int>();
        foreach (Villager v in world.Villagers)
        {
            if (v.CanWork && v.State != VillagerState.Left && world.FindHousehold(v.HouseholdId) is { HomeTile: GridPos home })
            {
                adultsAt[home] = adultsAt.GetValueOrDefault(home) + 1;
            }
        }

        if (adultsAt.Count == 0)
        {
            _o.WriteLine($"ZZT {village} year {world.Clock.Year} no homes");
            return;
        }

        List<GridPos> homes = adultsAt.Keys.OrderBy(p => p.Y).ThenBy(p => p.X).ToList();
        GridPos medoid = homes[0];
        long best = long.MaxValue;
        foreach (GridPos candidate in homes)
        {
            long sum = 0;
            foreach (GridPos other in homes)
            {
                int cost = world.TravelCost.Cost(candidate, other);
                sum += (cost == TravelCostField.Unreachable ? 100_000 : cost) * (long)adultsAt[other];
            }

            if (sum < best)
            {
                best = sum;
                medoid = candidate;
            }
        }

        var reachOf = new Dictionary<GridPos, int>();
        foreach (GridPos home in homes)
        {
            int cost = world.TravelCost.Cost(home, medoid);
            reachOf[home] = cost == TravelCostField.Unreachable ? 999 : cost / TravelCostField.BaseTileCost;
        }

        var buckets = new int[6];
        foreach (GridPos home in homes)
        {
            int t = reachOf[home];
            buckets[t <= 4 ? 0 : t <= 8 ? 1 : t <= 12 ? 2 : t <= 16 ? 3 : t <= 24 ? 4 : 5] += adultsAt[home];
        }

        int keeperWalk = -1;
        foreach (StoreBuilding store in world.StoreBuildings)
        {
            if (store.Kind == StoreKind.Warehouse && !store.Closed)
            {
                int cost = world.TravelCost.Cost(medoid, store.Tile);
                if (cost != TravelCostField.Unreachable && (keeperWalk < 0 || cost / TravelCostField.BaseTileCost < keeperWalk))
                {
                    keeperWalk = cost / TravelCostField.BaseTileCost;
                }
            }
        }

        int barleyFarms = world.Workplaces.Count(p => p.Kind == JobKind.Farmer && !p.IsSite && p.Grows != Workplace.DefaultGrows);
        int adults = adultsAt.Values.Sum();
        _o.WriteLine(
            $"ZZT {village} year {world.Clock.Year} adults {adults} homes {homes.Count} "
            + $"reach {string.Join(' ', buckets)} keeper {keeperWalk} barleyfarms {barleyFarms}");

        // Step on.
        int years = int.Parse(Environment.GetEnvironmentVariable("ZZ_YEARS") ?? "4", System.Globalization.CultureInfo.InvariantCulture);
        var previous = new Dictionary<int, VillagerState>();
        long rests = 0, wells = 0, adultDays = 0, spellsAtHome = 0, restTicks = 0;
        var weights = new long[Reaches.Length, 2, 4];
        var history = new Dictionary<int, List<int>>();
        ulong seasonTicks = (ulong)c.TicksPerDay * (ulong)c.DaysPerSeason;
        int unsettledAt = c.ContentmentUnsettledAt;
        int leavers = 0;
        var kept = new int[Reaches.Length];
        var keptReach = new List<string>();

        int ReachOfVillager(Villager v) =>
            world.FindHousehold(v.HouseholdId) is { HomeTile: GridPos h }
                ? (reachOf.TryGetValue(h, out int known) ? known : ReachFrom(world, h, medoid))
                : 999;

        for (long t = 0; t < (long)c.TicksPerYear * years; t++)
        {
            loop.StepOnce();
            foreach (Villager v in world.Villagers)
            {
                if (!v.CanWork)
                {
                    continue;
                }

                VillagerState was = previous.GetValueOrDefault(v.Id, v.State);
                if (v.State == VillagerState.Resting && was != VillagerState.Resting)
                {
                    rests++;
                }

                // A spell begun at home this tick — where the well trip is offered, and where the tavern's evening will be.
                if (v.State == VillagerState.Resting && v.ActionTicksRemaining == c.RestTicks
                    && world.FindHousehold(v.HouseholdId) is { HomeTile: GridPos at } && v.Tile == at)
                {
                    spellsAtHome++;
                }

                if (v.State == VillagerState.Resting)
                {
                    restTicks++;
                }

                if (v.State == VillagerState.WalkingToTheWell && was != VillagerState.WalkingToTheWell)
                {
                    wells++;
                }

                if (v.State == VillagerState.Left && was != VillagerState.Left && history.TryGetValue(v.Id, out List<int>? seasons))
                {
                    leavers++;
                    int reach = ReachOfVillager(v);
                    bool atTheLine = seasons.TakeLast(8).Any(w => w == unsettledAt);
                    for (int r = 0; r < Reaches.Length; r++)
                    {
                        if (reach <= Reaches[r] && atTheLine)
                        {
                            kept[r]++;
                        }
                    }

                    keptReach.Add($"{reach}:{string.Join(',', seasons.TakeLast(8))}");
                }

                previous[v.Id] = v.State;
            }

            if (world.Tick % (ulong)c.TicksPerDay == 0UL)
            {
                adultDays += world.Villagers.Count(v => v.CanWork);
            }

            // The read runs on the season's first tick; one tick after it, it has run whichever side of the step it sits.
            if (world.Tick % seasonTicks != 1UL)
            {
                continue;
            }

            foreach (Villager v in world.Villagers)
            {
                if (!v.CanWork || v.State == VillagerState.Left)
                {
                    continue;
                }

                int weight = 0;
                foreach (ContentmentCauseRow row in c.ContentmentCauses)
                {
                    if ((v.ContentmentCauses & (1 << (int)row.Cause)) != 0)
                    {
                        weight += row.Weight;
                    }
                }

                if (!history.TryGetValue(v.Id, out List<int>? list))
                {
                    history[v.Id] = list = new List<int>();
                }

                list.Add(weight);
                int reach = ReachOfVillager(v);
                for (int r = 0; r < Reaches.Length; r++)
                {
                    weights[r, reach <= Reaches[r] ? 0 : 1, Math.Min(weight, 3)]++;
                }
            }
        }

        _o.WriteLine(
            $"ZZR {village} adultdays {adultDays} rests {rests} wells {wells} "
            + $"spellsAtHome {spellsAtHome} restTicks {restTicks}");
        for (int r = 0; r < Reaches.Length; r++)
        {
            _o.WriteLine(
                $"ZZW {village} R {Reaches[r]} in {weights[r, 0, 0]} {weights[r, 0, 1]} {weights[r, 0, 2]} {weights[r, 0, 3]} "
                + $"out {weights[r, 1, 0]} {weights[r, 1, 1]} {weights[r, 1, 2]} {weights[r, 1, 3]}");
        }

        _o.WriteLine(
            $"ZZL {village} left {leavers} kept8 {kept[0]} kept12 {kept[1]} kept16 {kept[2]} kept24 {kept[3]} "
            + $"[{string.Join(' ', keptReach)}]");
    }

    private static int ReachFrom(SimWorld world, GridPos home, GridPos medoid)
    {
        int cost = world.TravelCost.Cost(home, medoid);
        return cost == TravelCostField.Unreachable ? 999 : cost / TravelCostField.BaseTileCost;
    }

    public static IEnumerable<object[]> Runs()
    {
        if (Environment.GetEnvironmentVariable("ZZ_SAVES") is not string folder)
        {
            yield return new object[] { string.Empty };
            yield break;
        }

        foreach (string dir in Directory.GetDirectories(folder).OrderBy(d => d, StringComparer.Ordinal))
        {
            string? newest = Directory.GetFiles(dir, "*.save")
                .OrderByDescending(File.GetLastWriteTimeUtc).ThenBy(p => p, StringComparer.Ordinal).FirstOrDefault();
            if (newest is not null)
            {
                yield return new object[] { newest };
            }
        }
    }
}

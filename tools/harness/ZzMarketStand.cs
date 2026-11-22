using Kith.Sim.Config;
using Kith.Sim.Core;
using Kith.Sim.Logging;
using Kith.Sim.Persistence;
using Kith.Sim.World;
using Xunit;
using Xunit.Abstractions;

namespace Kith.Sim.Tests;

// ⛔ W14's MEASUREMENT HARNESS (`market-stand.md §4`) — kept in tools/harness/, COPIED into tests/Kith.Sim.Tests/ to run and
// DELETED from there before any commit (D420). Before a number in the long walk is typed: how long the shopping trips are,
// how many very long ones a village makes a year, and whether a far household would shop at a stand stood near it.
//
// A TRIP is one villager's spell from the tick they set out FetchingFromStore to the tick they arrive home again
// (TravelingHome ending on the home tile) — the shopping round trip, the walk the log would name. The store is the one they
// stood at when the spell turned (the nearest store within two tiles), the good what was in their arms then.
//
// Per save (the NEWEST .save in each folder under ZZ_SAVES, a COPY of Joe's), arm "base" (the save as it is):
//   ZZM  — village, year, homes, adults, markets, storehouses, trips, round trip p50 / p90 / p99 / max (ticks), trips begun
//          away from home;
//   ZZY  — one a stepped year: trips at or past 8 / 10 / 12 / 14 / 16 / 20 / 24 days, and the distinct households among them;
//   ZZP  — the same counts by the PLANNED walk (twice home → store in the one cost field) — the sim could count either;
//   ZZF  — every home: its trips, median round trip, the store kind it mostly turned at;
//   ZZL  — the five longest trips: who, the good, from where to which store, the year (the sentence's material).
// With ZZ_POSE=1 and at least ZZ_FARHOMES (2) homes whose median round trip is ZZ_FARDAYS (8) days or more, arm "stand":
// the save opened again, a finished market RAISED as near the far homes' medoid as it can stand (the stand's 2×2 is the
// market's footprint — a tile or two would fit nearer), told D839's shape — 4,000 room, 500 a good, ONE seat, a new
// handcart — and stepped the same years; the same lines with "stand", plus:
//   ZZS  — where it stood and how far from the medoid; the goods it counts and what they keep; per far home, the walk to
//          the stand against the walk to the shop it used; the far homes' trips at the stand / at another market / at a
//          storehouse; trips that PASSED it (turned at a store further from home than the stand); its seat filled share;
//          the food on its shelf (min / mean) and the ticks its shelf held less than a cart-load of food.
// ZZ_SHARD=k/n runs every n-th folder from k (parallel processes after one build).
// Copy in; `dotnet test tests/Kith.Sim.Tests -c Release --no-build --filter ZzMarketStand --logger "console;verbosity=detailed"`;
// delete.
public sealed class ZzMarketStand
{
    private static readonly int[] Days = { 8, 10, 12, 14, 16, 20, 24 };
    private readonly ITestOutputHelper _o;

    public ZzMarketStand(ITestOutputHelper o) => _o = o;

    private sealed record Trip(
        int VillagerId, string Who, int HouseholdId, GridPos Home, int Year, int Elapsed, int Planned, bool FromHome,
        StoreBuilding? Store, string Goods);

    private sealed class Open
    {
        public required ulong Start { get; init; }
        public required bool FromHome { get; init; }
        public StoreBuilding? Store { get; set; }
        public string Goods { get; set; } = "nothing";
        public bool Turned { get; set; }
    }

    [Theory]
    [MemberData(nameof(Runs))]
    public void Measure(string what)
    {
        if (what.Length == 0)
        {
            return;
        }

        string village = Path.GetFileName(Path.GetDirectoryName(what)) ?? what;
        int years = int.Parse(Environment.GetEnvironmentVariable("ZZ_YEARS") ?? "5", System.Globalization.CultureInfo.InvariantCulture);
        OwnCounter(false);
        SimLoop? loop = OpenSave(what, village);
        if (loop is null)
        {
            return;
        }

        List<Trip> trips = Step(loop, years, stand: null, out _);
        Report("base", village, loop.World, trips, years);

        if (Environment.GetEnvironmentVariable("ZZ_POSE") != "1")
        {
            return;
        }

        int farDays = int.Parse(Environment.GetEnvironmentVariable("ZZ_FARDAYS") ?? "8", System.Globalization.CultureInfo.InvariantCulture);
        int farHomesWanted = int.Parse(Environment.GetEnvironmentVariable("ZZ_FARHOMES") ?? "2", System.Globalization.CultureInfo.InvariantCulture);
        int tpd = loop.World.Config.TicksPerDay;
        var farHomes = trips.Where(t => t.FromHome).GroupBy(t => t.Home)
            .Where(g => Median(g.Select(t => t.Elapsed).ToList()) >= farDays * tpd)
            .ToDictionary(g => g.Key, g => g.Count());
        if (farHomes.Count < farHomesWanted)
        {
            _o.WriteLine($"ZZS {village} stand none farhomes {farHomes.Count}");
            return;
        }

        // The shop each far home used most in the base arm, for the walk-against-walk line.
        var usedBy = trips.Where(t => farHomes.ContainsKey(t.Home) && t.Store is not null).GroupBy(t => t.Home)
            .ToDictionary(g => g.Key, g => g.GroupBy(t => t.Store!.Id).OrderByDescending(x => x.Count()).First().First().Store!);

        SimLoop? again = OpenSave(what, village);
        if (again is null)
        {
            return;
        }

        SimWorld world = again.World;
        GridPos medoid = Medoid(world, farHomes);
        (Workplace? seat, StoreBuilding? stand) = RaiseTheStand(world, medoid);
        if (seat is null || stand is null)
        {
            _o.WriteLine($"ZZS {village} stand nowhere near ({medoid.X},{medoid.Y})");
            return;
        }

        var counts = new List<string>();
        foreach (Goods g in Enumerable.Range(0, world.GoodsCatalog.Count).Select(i => (Goods)i))
        {
            int keeps = stand.KeepsUpTo(g);
            if (stand.CanEverHold(g) && world.KnowsOf(g))
            {
                counts.Add($"{world.GoodsCatalog.NameOf(g)}={keeps}");
            }
        }

        int fromMedoid = world.TravelCost.TicksBetween(medoid, stand.Tile);
        var walks = farHomes.Keys.Select(h =>
            $"{world.TravelCost.TicksBetween(h, stand.Tile)}/{(usedBy.TryGetValue(h, out StoreBuilding? s) ? world.TravelCost.TicksBetween(h, s.Tile) : -1)}");
        _o.WriteLine(
            $"ZZS {village} stand at ({stand.Tile.X},{stand.Tile.Y}) medoid ({medoid.X},{medoid.Y}) off {fromMedoid} farhomes {farHomes.Count} "
            + $"counts {counts.Count} kept {stand.KeptInAll} of {stand.Store.Capacity} [{string.Join(' ', counts)}] walks stand/used [{string.Join(' ', walks)}]");

        int? asked = world.JobLimits.For(JobKind.Marketer);
        if (Environment.GetEnvironmentVariable("ZZ_LIFT") == "1" && asked is int a)
        {
            world.SetJobLimit(JobKind.Marketer, a + 1);
        }

        _o.WriteLine(
            $"ZZQ {village} marketers asked {asked?.ToString() ?? "none"} now {world.JobLimits.For(JobKind.Marketer)?.ToString() ?? "none"} "
            + $"wanted {LabourQuota.MarketersWanted(world)} seats {string.Join(' ', world.Workplaces.Where(w => w.Kind == JobKind.Marketer && !w.IsSite).Select(w => $"({w.Tile.X},{w.Tile.Y}):{w.Places}"))}");

        OwnCounter(Environment.GetEnvironmentVariable("ZZ_OWN") == "1");
        List<Trip> posed = Step(again, years, stand, out StandWatch watch);
        OwnCounter(false);
        _o.WriteLine(
            $"ZZQ {village} after wanted {LabourQuota.MarketersWanted(world)} working {string.Join(' ', world.Workplaces.Where(w => w.Kind == JobKind.Marketer && !w.IsSite).Select(w => $"({w.Tile.X},{w.Tile.Y}):{world.Villagers.Count(v => v.Alive && v.WorkplaceId == w.Id)}"))}");
        Report("stand", village, world, posed, years);

        int atStand = 0, atMarket = 0, atStore = 0, passed = 0, farTrips = 0;
        foreach (Trip t in posed.Where(t => farHomes.ContainsKey(t.Home)))
        {
            farTrips++;
            if (t.Store is null)
            {
                continue;
            }

            if (t.Store.Id == stand.Id)
            {
                atStand++;
            }
            else
            {
                if (t.Store.Kind == StoreKind.Market)
                {
                    atMarket++;
                }
                else
                {
                    atStore++;
                }

                if (world.TravelCost.TicksBetween(t.Home, t.Store.Tile) > world.TravelCost.TicksBetween(t.Home, stand.Tile))
                {
                    passed++;
                }
            }
        }

        int allAtStand = posed.Count(t => t.Store?.Id == stand.Id);
        _o.WriteLine(
            $"ZZS {village} far trips {farTrips} atstand {atStand} atmarket {atMarket} atstorehouse {atStore} passed {passed} "
            + $"allatstand {allAtStand} of {posed.Count} seat {watch.SeatFilled * 100.0 / Math.Max(watch.Ticks, 1):F0}% "
            + $"food min {watch.FoodMin} mean {watch.FoodSum / Math.Max(watch.Ticks, 1)} shortticks {watch.ShortTicks} "
            + $"cart {seat.HandcartUses} stocktrips {watch.StockTrips} moved {watch.Moved} outflow/yr {watch.Outflow / years} "
            + $"farmedian base {Median(trips.Where(t => farHomes.ContainsKey(t.Home)).Select(t => t.Elapsed).ToList())} stand {Median(posed.Where(t => farHomes.ContainsKey(t.Home)).Select(t => t.Elapsed).ToList())} "
            + $"farticks base {trips.Where(t => farHomes.ContainsKey(t.Home)).Sum(t => t.Elapsed)} stand {posed.Where(t => farHomes.ContainsKey(t.Home)).Sum(t => t.Elapsed)} "
            + $"basefartrips {trips.Count(t => farHomes.ContainsKey(t.Home))}");
        _o.WriteLine($"ZZK {village} keeper [{string.Join(' ', watch.KeeperStates.OrderByDescending(kv => kv.Value).Select(kv => $"{kv.Key}={kv.Value}"))}]");
        _o.WriteLine(
            $"ZZS {village} alive base {loop.World.Villagers.Count(v => v.Alive && v.State != VillagerState.Left)} "
            + $"stand {world.Villagers.Count(v => v.Alive && v.State != VillagerState.Left)}");
    }

    private sealed class StandWatch
    {
        public long Ticks { get; set; }
        public long SeatFilled { get; set; }
        public int FoodMin { get; set; } = int.MaxValue;
        public long FoodSum { get; set; }
        public long ShortTicks { get; set; }
        public int StockTrips { get; set; }
        public long Moved { get; set; }
        public long Outflow { get; set; }
        public Dictionary<VillagerState, int> KeeperStates { get; } = new();
    }

    /// <summary>The always-stocked shelf (ZZ_SHELF=full): the stand topped up to what it keeps, out of the storehouses.</summary>
    private static int TopUp(SimWorld world, StoreBuilding stand)
    {
        int moved = 0;
        for (int i = 0; i < world.GoodsCatalog.Count; i++)
        {
            var g = (Goods)i;
            int want = stand.KeepsUpTo(g) - stand.Store[g];
            foreach (StoreBuilding s in world.StoreBuildings)
            {
                if (want <= 0)
                {
                    break;
                }

                if (s.Kind is not (StoreKind.Granary or StoreKind.Warehouse) || s.Closed)
                {
                    continue;
                }

                int n = Math.Min(want, s.Store[g]);
                if (n > 0 && s.Store.TryTake(g, n))
                {
                    stand.Store.Add(g, n);
                    want -= n;
                    moved += n;
                }
            }
        }

        return moved;
    }

    private SimLoop? OpenSave(string what, string village)
    {
        SaveOpened opened = SaveFile.Open(what, ShippedConfig.Load());
        if (opened.Loop is SimLoop loop)
        {
            return loop;
        }

        _o.WriteLine($"ZZX {village} refused {opened.Refusal} {opened.Detail}");
        return null;
    }

    private static List<Trip> Step(SimLoop loop, int years, StoreBuilding? stand, out StandWatch watch, Action? atYearThree = null)
    {
        SimWorld world = loop.World;
        SimConfig c = world.Config;
        var open = new Dictionary<int, Open>();
        var previous = new Dictionary<int, VillagerState>();
        var trips = new List<Trip>();
        watch = new StandWatch();
        Workplace? standSeat = stand is null ? null : world.Workplaces.FirstOrDefault(w => w.Kind == JobKind.Marketer && w.Tile == stand.Tile && !w.IsSite);
        int cartLoad = c.CarryCapacity * c.CartArmfuls;

        for (long t = 0; t < (long)c.TicksPerYear * years; t++)
        {
            if (atYearThree is not null && t == (long)c.TicksPerYear * 3)
            {
                atYearThree();
            }

            int[] zzBefore = stand is null ? Array.Empty<int>() : Enumerable.Range(0, world.GoodsCatalog.Count).Select(i => stand.Store[(Goods)i]).ToArray();
            loop.StepOnce();
            if (stand is not null)
            {
                for (int i = 0; i < zzBefore.Length; i++)
                {
                    int now = stand.Store[(Goods)i];
                    if (now < zzBefore[i])
                    {
                        watch.Outflow += zzBefore[i] - now;
                    }
                }

                foreach (Villager k in world.Villagers.Where(k => k.Alive && standSeat is not null && k.WorkplaceId == standSeat.Id))
                {
                    watch.KeeperStates[k.State] = watch.KeeperStates.GetValueOrDefault(k.State) + 1;
                }
            }

            if (stand is not null && Environment.GetEnvironmentVariable("ZZ_SHELF") == "full")
            {
                watch.Moved += TopUp(world, stand);
            }

            foreach (Villager v in world.Villagers)
            {
                if (!v.CanWork)
                {
                    continue;
                }

                VillagerState was = previous.GetValueOrDefault(v.Id, v.State);
                previous[v.Id] = v.State;
                GridPos? home = world.FindHousehold(v.HouseholdId) is { HomeTile: GridPos h } ? h : null;

                if (v.State == VillagerState.FetchingFromStore && was != VillagerState.FetchingFromStore)
                {
                    open[v.Id] = new Open { Start = world.Tick, FromHome = home is GridPos at && was != VillagerState.TravelingHome && v.Tile.ChebyshevTo(at) <= 1 };
                }

                if (open.TryGetValue(v.Id, out Open? trip))
                {
                    if (!trip.Turned && was == VillagerState.FetchingFromStore && v.State != VillagerState.FetchingFromStore)
                    {
                        trip.Turned = true;
                        trip.Store = StoreAt(world, v.Tile);
                        trip.Goods = WhatIsCarried(world, v);
                    }

                    if (trip.Turned && was == VillagerState.TravelingHome && v.State != VillagerState.TravelingHome)
                    {
                        open.Remove(v.Id);
                        if (home is GridPos at && v.Tile.ChebyshevTo(at) <= 1)
                        {
                            int planned = trip.Store is null ? -1 : 2 * world.TravelCost.TicksBetween(at, trip.Store.Tile);
                            trips.Add(new Trip(
                                v.Id, v.Name, v.HouseholdId, at, world.Clock.Year, (int)(world.Tick - trip.Start), planned,
                                trip.FromHome, trip.Store, trip.Goods));
                        }
                    }
                    else if (trip.Turned && v.State != VillagerState.TravelingHome && was != VillagerState.TravelingHome
                             && v.State != VillagerState.FetchingFromStore)
                    {
                        open.Remove(v.Id); // turned into something other than the walk home: not a round trip.
                    }

                    if (!v.Alive || v.State == VillagerState.Left)
                    {
                        open.Remove(v.Id);
                    }
                }

                if (stand is not null && v.State == VillagerState.StockingTheMarket && was != VillagerState.StockingTheMarket
                    && v.WorkplaceId == standSeat?.Id)
                {
                    watch.StockTrips++;
                }
            }

            if (stand is not null)
            {
                watch.Ticks++;
                if (standSeat is not null && world.Villagers.Any(v => v.Alive && v.WorkplaceId == standSeat.Id))
                {
                    watch.SeatFilled++;
                }

                int food = world.GoodsCatalog.EdibleGoods.Sum(g => stand.Store[g]);
                watch.FoodMin = Math.Min(watch.FoodMin, food);
                watch.FoodSum += food;
                if (food < cartLoad)
                {
                    watch.ShortTicks++;
                }
            }
        }

        return trips;
    }

    private void Report(string arm, string village, SimWorld world, List<Trip> trips, int years)
    {
        int tpd = world.Config.TicksPerDay;
        var homes = world.Households.Where(h => h.HomeTile is not null && world.LivingMembersOf(h) > 0).ToList();
        int adults = world.Villagers.Count(v => v.CanWork && v.State != VillagerState.Left);
        int markets = world.StoreBuildings.Count(s => s.Kind == StoreKind.Market && !s.Closed);
        int storehouses = world.StoreBuildings.Count(s => (s.Kind == StoreKind.Granary || s.Kind == StoreKind.Warehouse) && !s.Closed);
        List<int> el = trips.Select(t => t.Elapsed).OrderBy(x => x).ToList();
        _o.WriteLine(
            $"ZZM {arm} {village} year {world.Clock.Year - years} homes {homes.Count} adults {adults} markets {markets} storehouses {storehouses} "
            + $"trips {trips.Count} p50 {Pct(el, 50)} p90 {Pct(el, 90)} p99 {Pct(el, 99)} max {(el.Count == 0 ? 0 : el[^1])} "
            + $"awayfromhome {trips.Count(t => !t.FromHome)}");
        _o.WriteLine($"ZZH {arm} {village} " + string.Join(' ', Days.Select(d => $"d{d} {Cumulative(trips, t => t.Elapsed >= d * tpd)}|{Cumulative(trips, t => t.Planned >= d * tpd)}")));

        foreach (int year in trips.Select(t => t.Year).Distinct().OrderBy(y => y))
        {
            List<Trip> inYear = trips.Where(t => t.Year == year).ToList();
            _o.WriteLine($"ZZY {arm} {village} year {year} trips {inYear.Count} " + string.Join(' ', Days.Select(d =>
            {
                List<Trip> longOnes = inYear.Where(t => t.Elapsed >= d * tpd).ToList();
                return $"d{d} {longOnes.Count}/{longOnes.Select(t => t.HouseholdId).Distinct().Count()}";
            })));
            _o.WriteLine($"ZZP {arm} {village} year {year} " + string.Join(' ', Days.Select(d =>
            {
                List<Trip> longOnes = inYear.Where(t => t.Planned >= d * tpd).ToList();
                return $"d{d} {longOnes.Count}/{longOnes.Select(t => t.HouseholdId).Distinct().Count()}";
            })));
        }

        foreach (IGrouping<GridPos, Trip> home in trips.Where(t => t.FromHome).GroupBy(t => t.Home).OrderBy(g => g.Key.Y).ThenBy(g => g.Key.X))
        {
            string kind = home.Where(t => t.Store is not null).GroupBy(t => t.Store!.Kind).OrderByDescending(g => g.Count()).FirstOrDefault()?.Key.ToString() ?? "-";
            _o.WriteLine($"ZZF {arm} {village} home ({home.Key.X},{home.Key.Y}) trips {home.Count()} median {Median(home.Select(t => t.Elapsed).ToList())} mostly {kind}");
        }

        foreach (Trip t in trips.OrderByDescending(t => t.Elapsed).Take(5))
        {
            _o.WriteLine(
                $"ZZL {arm} {village} {t.Who} {t.Elapsed} ticks ({t.Elapsed / (double)tpd:F1} days) for {t.Goods} from ({t.Home.X},{t.Home.Y}) "
                + $"to {t.Store?.Kind.ToString() ?? "?"} at ({t.Store?.Tile.X},{t.Store?.Tile.Y}) planned {t.Planned} year {t.Year} fromhome {t.FromHome}");
        }
    }

    private static StoreBuilding? StoreAt(SimWorld world, GridPos at)
    {
        StoreBuilding? best = null;
        int bestD = 3;
        foreach (StoreBuilding s in world.StoreBuildings)
        {
            int d = s.Tile.ChebyshevTo(at);
            if (d < bestD)
            {
                bestD = d;
                best = s;
            }
        }

        return best;
    }

    private static string WhatIsCarried(SimWorld world, Villager v)
    {
        for (int i = 0; i < world.GoodsCatalog.Count; i++)
        {
            if (v.Carried[(Goods)i] > 0)
            {
                return world.GoodsCatalog.NameOf((Goods)i);
            }
        }

        return "nothing";
    }

    private static GridPos Medoid(SimWorld world, Dictionary<GridPos, int> weightOf)
    {
        GridPos best = weightOf.Keys.First();
        long bestSum = long.MaxValue;
        foreach (GridPos candidate in weightOf.Keys.OrderBy(p => p.Y).ThenBy(p => p.X))
        {
            long sum = 0;
            foreach ((GridPos other, int w) in weightOf)
            {
                int cost = world.TravelCost.TicksBetween(candidate, other);
                sum += (cost == TravelCostField.Unreachable ? 100_000 : cost) * (long)w;
            }

            if (sum < bestSum)
            {
                bestSum = sum;
                best = candidate;
            }
        }

        return best;
    }

    /// <summary>A finished market as near <paramref name="near"/> as one will stand, told D839's shape.</summary>
    private static (Workplace?, StoreBuilding?) RaiseTheStand(SimWorld world, GridPos near)
    {
        for (int r = 1; r < 20; r++)
        {
            for (int dy = -r; dy <= r; dy++)
            {
                for (int dx = -r; dx <= r; dx++)
                {
                    if (Math.Max(Math.Abs(dx), Math.Abs(dy)) != r)
                    {
                        continue;
                    }

                    var at = new GridPos(near.X + dx, near.Y + dy);
                    if (!world.Map.Contains(at) || world.HasSomethingToHarvest(at) || !world.CanBuildAt(BuildingKind.Market, at).Allowed
                        || !world.Mark(BuildingKind.Market, at).Allowed)
                    {
                        continue;
                    }

                    Workplace site = world.Workplaces.Single(w => w.Construction?.Kind == BuildingKind.Market && w.Construction.IsFinished == false && w.IsSite && w.Tile.ChebyshevTo(at) <= 2);
                    BuildFixtures.StockTheSite(site);
                    for (int i = 0; i <= site.Construction!.Recipe.WorkTicks; i++)
                    {
                        site.Construction.Work();
                    }

                    GridPos stood = site.Tile;
                    world.Complete(site);
                    Workplace seat = world.Workplaces.Single(w => w.Kind == JobKind.Marketer && w.Tile == stood && !w.IsSite);
                    StoreBuilding store = world.StoreBuildings.Single(s => s.Kind == StoreKind.Market && s.Tile == stood);
                    store.Store.Capacity = world.Config.MarketStoreCapacity / 2;
                    store.StartingLimit = world.Config.MarketStartingLimit / 2;
                    world.SetStaffing(seat, 1);
                    seat.HandcartUses = world.Config.HandcartUses;
                    return (seat, store);
                }
            }
        }

        return (null, null);
    }

    /// <summary>Distinct households over the run, then the year the count first reached 3, 5 and 8 (-1 never).</summary>
    private static string Cumulative(List<Trip> trips, Func<Trip, bool> isLong)
    {
        var seen = new HashSet<int>();
        int at3 = -1, at5 = -1, at8 = -1;
        foreach (Trip t in trips.Where(isLong))
        {
            if (seen.Add(t.HouseholdId))
            {
                at3 = at3 < 0 && seen.Count >= 3 ? t.Year : at3;
                at5 = at5 < 0 && seen.Count >= 5 ? t.Year : at5;
                at8 = at8 < 0 && seen.Count >= 8 ? t.Year : at8;
            }
        }

        return $"{seen.Count}:{at3}:{at5}:{at8}:{trips.Count(isLong)}";
    }

    /// <summary>
    /// The unattended control (ZZ_SEEDS): the harness valley — shipped config, the played opening, a granary and a warehouse
    /// marked at Year 3 — run ZZ_YEARS (50); the same ZZM / ZZY / ZZH lines, arm "control". Whether a golden-shaped village
    /// ever makes the long walk.
    /// </summary>
    [Fact]
    public void OnTheHarness()
    {
        if (Environment.GetEnvironmentVariable("ZZ_SEEDS") is not string seeds)
        {
            return;
        }

        int years = int.Parse(Environment.GetEnvironmentVariable("ZZ_YEARS") ?? "50", System.Globalization.CultureInfo.InvariantCulture);
        foreach (ulong seed in seeds.Split(',').Select(x => ulong.Parse(x.Trim(), System.Globalization.CultureInfo.InvariantCulture)))
        {
            SimConfig config = ShippedConfig.Load() with { Seed = seed };
            SimLoop loop = SimFactory.CreatePhase0(config, new InMemoryLogSink());
            SimWorld world = loop.World;
            var limits = ShippedConfig.Load().StartingStockLimits;
            for (int id = 0; id < world.GoodsCatalog.Count; id++)
            {
                if (limits.TryGetValue(world.GoodsCatalog[id].Name, out int limit))
                {
                    world.SetStockLimit((Goods)id, limit);
                }
            }

            ColdStartTests.PlayTheOpening(world);
            List<Trip> trips = Step(loop, years, stand: null, out _, atYearThree: () =>
            {
                ColdStartTests.MarkSomewhereNear(world, BuildingKind.Granary, world.Map.FoundingSite, 8);
                ColdStartTests.MarkSomewhereNear(world, BuildingKind.Warehouse, world.Map.FoundingSite, 8);
            });
            Report("control", $"seed{seed}", world, trips, years);
        }
    }

    /// <summary>
    /// ZZ_OWN=1's rule — a marketer stocks the counter of the market they work at first — exists only with
    /// `market_stand_own_counter.py` applied to the sim (a TEMPORARY patch, reverted after the run), so it is reached by
    /// reflection and a run asking for it without the patch fails loudly.
    /// </summary>
    private static void OwnCounter(bool on)
    {
        System.Reflection.FieldInfo? flag = typeof(Kith.Sim.Systems.BehaviorSystem).GetField(
            "ZzOwnCounter", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
        if (flag is null)
        {
            if (on)
            {
                throw new InvalidOperationException("ZZ_OWN=1 needs tools/harness/market_stand_own_counter.py applied to the sim first");
            }

            return;
        }

        flag.SetValue(null, on);
    }

    private static int Median(List<int> xs) => Pct(xs.OrderBy(x => x).ToList(), 50);

    private static int Pct(List<int> sorted, int p) => sorted.Count == 0 ? 0 : sorted[Math.Min(sorted.Count - 1, sorted.Count * p / 100)];

    public static IEnumerable<object[]> Runs()
    {
        if (Environment.GetEnvironmentVariable("ZZ_SAVES") is not string folder)
        {
            yield return new object[] { string.Empty };
            yield break;
        }

        int k = 0, n = 1;
        if (Environment.GetEnvironmentVariable("ZZ_SHARD") is string shard && shard.Split('/') is [string a, string b])
        {
            k = int.Parse(a, System.Globalization.CultureInfo.InvariantCulture);
            n = int.Parse(b, System.Globalization.CultureInfo.InvariantCulture);
        }

        string[] dirs = Directory.GetDirectories(folder).OrderBy(d => d, StringComparer.Ordinal).ToArray();
        for (int i = k; i < dirs.Length; i += n)
        {
            string? newest = Directory.GetFiles(dirs[i], "*.save")
                .OrderByDescending(File.GetLastWriteTimeUtc).ThenBy(p => p, StringComparer.Ordinal).FirstOrDefault();
            if (newest is not null)
            {
                yield return new object[] { newest };
            }
        }
    }
}

internal static class ZzMarketStandGrid
{
    public static int ChebyshevTo(this GridPos a, GridPos b) => Math.Max(Math.Abs(a.X - b.X), Math.Abs(a.Y - b.Y));
}

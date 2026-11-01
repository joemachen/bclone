using Bclone.Sim.Config;
using Bclone.Sim.Core;
using Bclone.Sim.Logging;
using Bclone.Sim.Persistence;
using Bclone.Sim.World;
using Xunit;
using Xunit.Abstractions;

namespace Bclone.Sim.Tests;

// ⛔ D668's MEASUREMENT — logistics (`specs/logistics.md §4`): how much of an adult's year is walking, and how much
// of that is a FETCH — a walk whose point is to move goods rather than to get to work. Kept in tools/harness/, COPIED
// into tests/Bclone.Sim.Tests/ to run and DELETED from there before any commit.
//
//   ZZ_SAVES=<folder, a COPY of Joe's saves> [ZZ_PICK=autosave-1.save] [ZZ_YEARS=5]
//       dotnet test tests/Bclone.Sim.Tests -c Release --filter ZzLogistics.OnJoesSaves
//   ZZ_SEEDS=1,2,3 [ZZ_YEARS=50]
//       dotnet test tests/Bclone.Sim.Tests -c Release --filter ZzLogistics.OnTheHarness
//
// One ZZG line a year per village: every living adult's ticks in nine buckets — work / commute / deliver (the
// producer's own load to a store) / fetch (home) / market / haul / shelter / home / other. A ZZG end line sums the run
// as shares. A ZZG spells line per walking bucket: spells, spells an adult-year, median, p90, longest, and the share
// longer than a day (4 ticks), two and four. ZZG long names the five longest fetch-or-haul spells (who, from home, how
// far out, which year). ZZG shops: every lived-in home's walk to the nearest store holding food, in ticks.
public sealed class ZzLogistics
{
    private enum Bucket
    {
        Work,
        Commute,
        Deliver,
        Fetch,
        Market,
        Haul,
        Shelter,
        Home,
        Other,
    }

    private static readonly Bucket[] Walking = { Bucket.Commute, Bucket.Deliver, Bucket.Fetch, Bucket.Market, Bucket.Haul };

    private readonly ITestOutputHelper _o;

    public ZzLogistics(ITestOutputHelper o) => _o = o;

    public static IEnumerable<object[]> Saves()
    {
        string? folder = Environment.GetEnvironmentVariable("ZZ_SAVES");
        if (folder is null)
        {
            yield return new object[] { string.Empty };
            yield break;
        }

        string pick = Environment.GetEnvironmentVariable("ZZ_PICK") ?? "autosave-1.save";
        foreach (string p in Directory.GetFiles(folder, pick, SearchOption.AllDirectories).OrderBy(p => p, StringComparer.Ordinal))
        {
            yield return new object[] { p };
        }
    }

    [Theory]
    [MemberData(nameof(Saves))]
    public void OnJoesSaves(string path)
    {
        if (path.Length == 0)
        {
            return; // ZZ_SAVES unset: nothing to measure.
        }

        int years = Years(5);
        SaveOpened opened = SaveFile.Open(path, ShippedConfig.Load());
        Assert.True(opened.Loop is not null, $"{opened.Refusal} {opened.Detail}");
        string village = Path.GetFileName(Path.GetDirectoryName(path)!);
        Run(opened.Loop!, village, years, atYearThree: null);
    }

    [Fact]
    public void OnTheHarness()
    {
        string? seeds = Environment.GetEnvironmentVariable("ZZ_SEEDS");
        if (seeds is null)
        {
            return; // ZZ_SEEDS unset: nothing to measure.
        }

        int years = Years(50);
        foreach (ulong seed in seeds.Split(',').Select(s => ulong.Parse(s.Trim(), System.Globalization.CultureInfo.InvariantCulture)))
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
            Run(loop, $"seed{seed}", years, atYearThree: () =>
            {
                ColdStartTests.MarkSomewhereNear(world, BuildingKind.Granary, world.Map.FoundingSite, 8);
                ColdStartTests.MarkSomewhereNear(world, BuildingKind.Warehouse, world.Map.FoundingSite, 8);
            });
        }
    }

    private static int Years(int fallback) =>
        int.TryParse(Environment.GetEnvironmentVariable("ZZ_YEARS"), out int y) ? y : fallback;

    private static Bucket Classify(Villager v) => v.State switch
    {
        VillagerState.Gathering or VillagerState.Cutting or VillagerState.MakingFirewood or VillagerState.Building
            or VillagerState.Clearing or VillagerState.Sowing or VillagerState.Reaping or VillagerState.Fishing
            or VillagerState.Hunting or VillagerState.Forging or VillagerState.Quarrying or VillagerState.Mining
            or VillagerState.Sewing or VillagerState.Grinding or VillagerState.Baking or VillagerState.DrawingWater
            or VillagerState.Tending or VillagerState.CuttingHay or VillagerState.FeedingTheHerd or VillagerState.Culling
            => Bucket.Work,
        VillagerState.TravelingToFood or VillagerState.TravelingToTrees or VillagerState.TravelingToHut
            or VillagerState.TravelingToField or VillagerState.TravelingToWater or VillagerState.TravelingToGame
            or VillagerState.TravelingToSmithy or VillagerState.TravelingToQuarry or VillagerState.TravelingToMine
            or VillagerState.TravelingToMill or VillagerState.TravelingToBakery or VillagerState.TravelingToTailor
            or VillagerState.WalkingOutToTend or VillagerState.WalkingBackToTheSteading
            => Bucket.Commute,
        // The walk home is the back half of a shopping trip when the arms are full, and a commute when they are not.
        VillagerState.TravelingHome => v.IsCarrying ? Bucket.Fetch : Bucket.Commute,
        VillagerState.FetchingFromStore or VillagerState.FetchingACoat or VillagerState.FetchingATool
            or VillagerState.WalkingToTheWell
            => Bucket.Fetch,
        VillagerState.CollectingForMarket or VillagerState.StockingTheMarket => Bucket.Market,
        // The producer's own load to the store (D385: every load to a store, never home) is part of producing,
        // not logistics — its own column, so the fetch and haul columns are only goods moved a second time.
        VillagerState.HaulingToStore or VillagerState.HaulingToFarm => Bucket.Deliver,
        VillagerState.ClearingABuffer
            or VillagerState.ClearingAStore or VillagerState.TidyingGround or VillagerState.FetchingMaterials
            or VillagerState.FetchingHay or VillagerState.CollectingForThePost or VillagerState.StockingThePost
            or VillagerState.FetchingFromThePost
            => Bucket.Haul,
        VillagerState.SeekingShelter => Bucket.Shelter,
        VillagerState.Resting or VillagerState.Idle => Bucket.Home,
        _ => Bucket.Other,
    };

    private sealed class Open
    {
        public Bucket Bucket;
        public long Start;
        public GridPos From;
        public GridPos Far;
        public int FarDist;
        public int Year;
    }

    private sealed record Spell(Bucket Bucket, int Ticks, string Who, GridPos From, GridPos Far, int FarDist, int Year);

    /// <summary>Which kind of store a walk turned round at: the nearest store within two tiles of its far point (D669).</summary>
    private static string TurnedAt(SimWorld world, GridPos far)
    {
        StoreBuilding? best = null;
        int bestD = int.MaxValue;
        foreach (StoreBuilding s in world.StoreBuildings)
        {
            int d = Cheb(far, s.Tile);
            if (d < bestD)
            {
                bestD = d;
                best = s;
            }
        }

        return best is null || bestD > 2 ? "elsewhere" : best.Kind == StoreKind.Market ? "market" : "storehouse";
    }

    private static int Cheb(GridPos a, GridPos b) => Math.Max(Math.Abs(a.X - b.X), Math.Abs(a.Y - b.Y));

    private void Run(SimLoop loop, string label, int years, Action? atYearThree)
    {
        SimWorld world = loop.World;
        SimConfig config = world.Config;
        int ticksPerYear = config.TicksPerYear;
        int day = config.TicksPerDay;

        long[] run = new long[9];
        long[] year = new long[9];
        long adultTicksRun = 0, adultTicksYear = 0;
        var spells = new Dictionary<Bucket, List<int>>();
        foreach (Bucket b in Walking)
        {
            spells[b] = new List<int>();
        }

        var longest = new List<Spell>();
        var fetchBy = new SortedDictionary<string, List<int>>(StringComparer.Ordinal);
        var open = new Dictionary<Villager, Open>(ReferenceEqualityComparer.Instance);
        int yearShown = world.Clock.Year;
        long t = 0;

        void Close(Villager v, Open o, long now)
        {
            if (Array.IndexOf(Walking, o.Bucket) < 0)
            {
                return;
            }

            int ticks = (int)(now - o.Start);
            spells[o.Bucket].Add(ticks);
            if (o.Bucket == Bucket.Fetch && ticks > 1)
            {
                string at = TurnedAt(world, o.Far);
                if (!fetchBy.TryGetValue(at, out List<int>? list))
                {
                    fetchBy[at] = list = new List<int>();
                }

                list.Add(ticks);
            }

            if (o.Bucket is Bucket.Fetch or Bucket.Haul)
            {
                longest.Add(new Spell(o.Bucket, ticks, v.FullName, o.From, o.Far, o.FarDist, o.Year));
                if (longest.Count > 40)
                {
                    longest = longest.OrderByDescending(s => s.Ticks).Take(5).ToList();
                }
            }
        }

        void YearLine()
        {
            _o.WriteLine($"ZZG {label} Y{yearShown} adultTicks {adultTicksYear} " + Columns(year, adultTicksYear));
            Array.Clear(year);
            adultTicksYear = 0;
        }

        for (; t < (long)ticksPerYear * years; t++)
        {
            if (t == (long)ticksPerYear * 3 && atYearThree is not null)
            {
                atYearThree();
            }

            loop.StepOnce();
            if (world.Clock.Year != yearShown)
            {
                YearLine();
                yearShown = world.Clock.Year;
            }

            foreach (Villager v in world.Villagers)
            {
                bool counts = v.Alive && v.CanWork;
                open.TryGetValue(v, out Open? o);
                if (!counts)
                {
                    if (o is not null)
                    {
                        Close(v, o, t);
                        open.Remove(v);
                    }

                    continue;
                }

                Bucket b = Classify(v);
                adultTicksYear++;
                adultTicksRun++;
                year[(int)b]++;
                run[(int)b]++;
                if (o is null)
                {
                    open[v] = new Open { Bucket = b, Start = t, From = v.Tile, Far = v.Tile, FarDist = 0, Year = world.Clock.Year };
                }
                else if (o.Bucket != b)
                {
                    Close(v, o, t);
                    o.Bucket = b;
                    o.Start = t;
                    o.From = v.Tile;
                    o.Far = v.Tile;
                    o.FarDist = 0;
                    o.Year = world.Clock.Year;
                }
                else
                {
                    int d = Cheb(v.Tile, o.From);
                    if (d > o.FarDist)
                    {
                        o.FarDist = d;
                        o.Far = v.Tile;
                    }
                }
            }
        }

        foreach ((Villager v, Open o) in open)
        {
            Close(v, o, t);
        }

        YearLine();
        _o.WriteLine($"ZZG {label} end years {years} adultTicks {adultTicksRun} alive {world.Villagers.Count(v => v.Alive)} " + Columns(run, adultTicksRun));
        double adultYears = adultTicksRun / (double)ticksPerYear;
        foreach (Bucket b in Walking)
        {
            List<int> s = spells[b];
            s.Sort();
            if (s.Count == 0)
            {
                _o.WriteLine($"ZZG {label} spells {b} n 0");
                continue;
            }

            _o.WriteLine($"ZZG {label} spells {b} n {s.Count} perAdultYear {s.Count / Math.Max(adultYears, 1e-9):F1} "
                + $"median {s[s.Count / 2]} p90 {s[Math.Min(s.Count - 1, (int)(s.Count * 0.9))]} max {s[^1]} "
                + $"over1day {Pct(s.Count(x => x > day), s.Count)} over2days {Pct(s.Count(x => x > 2 * day), s.Count)} "
                + $"over4days {Pct(s.Count(x => x > 4 * day), s.Count)}");
        }

        foreach (Spell s in longest.OrderByDescending(s => s.Ticks).Take(5))
        {
            _o.WriteLine($"ZZG {label} long {s.Bucket} {s.Ticks} ticks {s.Who} Y{s.Year} from ({s.From.X},{s.From.Y}) out to ({s.Far.X},{s.Far.Y}) {s.FarDist} tiles");
        }

        // D669: where the shopping trips turned round — a market or a storehouse — since D372 sends a household to the
        // nearest MARKET holding the good anywhere reachable, and to a storehouse only when no market holds it.
        foreach ((string at, List<int> s) in fetchBy)
        {
            s.Sort();
            _o.WriteLine($"ZZG {label} fetchTo {at} n {s.Count} median {s[s.Count / 2]} p90 {s[Math.Min(s.Count - 1, (int)(s.Count * 0.9))]} max {s[^1]}");
        }

        // Why a fetch is as long as it is: every lived-in home's walk to the nearest store holding any food, at the end —
        // and separately to the nearest market and the nearest storehouse holding food, the two D372 chooses between.
        var shops = world.StoreBuildings.Where(s => world.FoodIn(s.Store) > 0).ToList();
        var walks = new List<int>();
        var toMarket = new List<int>();
        var toStorehouse = new List<int>();
        foreach (Household h in world.Households)
        {
            if (h.HomeTile is not GridPos home || !world.Villagers.Any(v => v.Alive && v.HouseholdId == h.Id))
            {
                continue;
            }

            int best = int.MaxValue, market = int.MaxValue, storehouse = int.MaxValue;
            foreach (StoreBuilding s in shops)
            {
                int walk = world.TravelCost.TicksBetween(home, s.Tile);
                best = Math.Min(best, walk);
                if (s.Kind == StoreKind.Market)
                {
                    market = Math.Min(market, walk);
                }
                else
                {
                    storehouse = Math.Min(storehouse, walk);
                }
            }

            if (best != int.MaxValue)
            {
                walks.Add(best);
            }

            if (market != int.MaxValue)
            {
                toMarket.Add(market);
            }

            if (storehouse != int.MaxValue)
            {
                toStorehouse.Add(storehouse);
            }
        }

        walks.Sort();
        toMarket.Sort();
        toStorehouse.Sort();
        int markets = shops.Count(s => s.Kind == StoreKind.Market);
        _o.WriteLine(walks.Count == 0
            ? $"ZZG {label} shops {shops.Count} markets {markets} homes 0"
            : $"ZZG {label} shops {shops.Count} markets {markets} homes {walks.Count} homeToNearestShopTicks median {walks[walks.Count / 2]} "
              + $"p90 {walks[Math.Min(walks.Count - 1, (int)(walks.Count * 0.9))]} max {walks[^1]}"
              + $" toMarket {Med(toMarket)} toStorehouse {Med(toStorehouse)}");

        static string Med(List<int> s) => s.Count == 0 ? "none" : $"median {s[s.Count / 2]} p90 {s[Math.Min(s.Count - 1, (int)(s.Count * 0.9))]} max {s[^1]}";
    }

    private static string Columns(long[] ticks, long total)
    {
        var sb = new System.Text.StringBuilder();
        foreach (Bucket b in Enum.GetValues<Bucket>())
        {
            sb.Append(b.ToString().ToLowerInvariant()).Append(' ').Append(ticks[(int)b]).Append(" (").Append(Pct(ticks[(int)b], total)).Append(") ");
        }

        return sb.ToString().TrimEnd();
    }

    private static string Pct(long part, long total) => total == 0 ? "0%" : $"{part * 100.0 / total:F1}%";
}

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
//   ZZ_SEEDS=1,2,3 [ZZ_YEARS=50] [ZZ_CARTS=N] [ZZ_ARMFULS=k] [ZZ_USES=n] [ZZ_SLOW=pct] [ZZ_RESTOCK=armfuls] [ZZ_LABEL=name]
//       dotnet test tests/Bclone.Sim.Tests -c Release --filter ZzLogistics.OnTheHarness
//
// One ZZG line a year per village: every living adult's ticks in nine buckets — work / commute / deliver (the
// producer's own load to a store) / fetch (home) / market / haul / shelter / home / other. A ZZG end line sums the run
// as shares. A ZZG spells line per walking bucket: spells, spells an adult-year, median, p90, longest, and the share
// longer than a day (4 ticks), two and four. ZZG long names the five longest fetch-or-haul spells (who, from home, how
// far out, which year). ZZG shops: every lived-in home's walk to the nearest store holding food, in ticks.
//
// SLICE H (handcart.md §5–§7): ZZ_CARTS=N puts N handcarts into the founders' cart store at Year 3, where the households
// fetch them as the mechanic says (the unattended valley never raises a workshop, so this is the only way a harness
// village is carted); ZZ_ARMFULS=k overrides cart_armfuls. The ZZG carts line: how many were taken, households carted at
// the end, shopping trips an adult-year WITH the cart against without and their median ticks, and the ground — trodden
// tiles and path tiles (worn or packed by class) of the valley's tiles, so the trodden share with carts against without
// is one number apart. ZZG trips: shopping trips a housed-household-year (the fetch spells that turned at a shop).
//
// THE TRADES' CARTS (handcart.md §13): ZZG market: the marketer's loads put on the counter (restock trips) and loads
// carried out of a buffer or a dead larder, the units a load, how many of each were made under the market's cart, and
// trips a marketer-year; ZZG post: the merchant's loads down to the post and up from it the same way, a merchant-year.
// ZZ_POST=1 raises a finished trading post at Year 3 on the harness (TradeTests.RaiseAPost) with a keep order of 400 on
// firewood (its limit is 600), so the merchant has the woodcutter's surplus to carry down every year the harness runs;
// ZZ_MARKET=1 raises a finished market near the founding at Year 3 (the harness valley raises none of its own). ZZG workscarts: which standing
// markets and posts own a cart at the end and the trips left in it.
//
// THE CART'S NIGHT AT HOME (handcart.md §13.2b, D700): the market: and post: lines end with the trade's WHOLE walking share
// of its ticks (the walk home with the cart is a Commute tick, not Market, so the Market bucket alone would flatter the
// change), the first legs begun (WalkingToTheMarket), the ticks the works' cart slept at a worker's door and the ticks it
// was pushed home. ZZ_RESTOCK=n sets restock_short_armfuls_with_cart (§13.2c).
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
        SimConfig config = ShippedConfig.Load();
        if (int.TryParse(Environment.GetEnvironmentVariable("ZZ_ARMFULS"), out int armfuls))
        {
            config = config with { CartArmfuls = armfuls };
        }

        if (int.TryParse(Environment.GetEnvironmentVariable("ZZ_USES"), out int uses))
        {
            config = config with { HandcartUses = uses };
        }

        if (int.TryParse(Environment.GetEnvironmentVariable("ZZ_SLOW"), out int slow))
        {
            config = config with { CartOffPathSlowPercent = slow };
        }

        if (int.TryParse(Environment.GetEnvironmentVariable("ZZ_RESTOCK"), out int restock))
        {
            config = config with { RestockShortArmfulsWithCart = restock };
        }

        SaveOpened opened = SaveFile.Open(path, config);
        Assert.True(opened.Loop is not null, $"{opened.Refusal} {opened.Detail}");
        string village = Path.GetFileName(Path.GetDirectoryName(path)!);
        string label = Environment.GetEnvironmentVariable("ZZ_LABEL") is string l ? $"{l}-{village}" : village;

        // Slice H's "before" on a played save whose warehouse already racks carts (Hollowmere, Fernhollow — D682): take
        // every cart off every shelf and every doorstep, so the same village is measured without the mechanic.
        if (Environment.GetEnvironmentVariable("ZZ_NOCARTS") == "1")
        {
            SimWorld w = opened.Loop!.World;
            int taken = 0;
            foreach (StoreBuilding s in w.StoreBuildings)
            {
                taken += s.Store.TakeAll(Goods.Handcart);
            }

            foreach (Household h in w.Households)
            {
                taken += h.HandcartUses > 0 ? 1 : 0;
                h.HandcartUses = 0;
            }

            // ⚠️ And the carpenter stopped (a handcarts limit of nought), or the first run's "before" re-made seven carts in five years.
            w.SetStockLimit(Goods.Handcart, 0);
            _o.WriteLine($"ZZG {label} nocarts: {taken} handcarts taken out of the village at load, the handcarts limit set to 0");
        }

        Run(opened.Loop!, label, years, atYearThree: null);
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
        int carts = int.TryParse(Environment.GetEnvironmentVariable("ZZ_CARTS"), out int c) ? c : 0;
        string label = Environment.GetEnvironmentVariable("ZZ_LABEL") ?? (carts > 0 ? $"carts{carts}" : "base");
        foreach (ulong seed in seeds.Split(',').Select(s => ulong.Parse(s.Trim(), System.Globalization.CultureInfo.InvariantCulture)))
        {
            SimConfig config = ShippedConfig.Load() with { Seed = seed };
            if (int.TryParse(Environment.GetEnvironmentVariable("ZZ_ARMFULS"), out int armfuls))
            {
                config = config with { CartArmfuls = armfuls };
            }

            if (int.TryParse(Environment.GetEnvironmentVariable("ZZ_USES"), out int uses))
            {
                config = config with { HandcartUses = uses };
            }

            if (int.TryParse(Environment.GetEnvironmentVariable("ZZ_SLOW"), out int slow))
            {
                config = config with { CartOffPathSlowPercent = slow };
            }

            if (int.TryParse(Environment.GetEnvironmentVariable("ZZ_RESTOCK"), out int restock))
            {
                config = config with { RestockShortArmfulsWithCart = restock };
            }

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
            Run(loop, $"{label}-seed{seed}", years, atYearThree: () =>
            {
                ColdStartTests.MarkSomewhereNear(world, BuildingKind.Granary, world.Map.FoundingSite, 8);
                ColdStartTests.MarkSomewhereNear(world, BuildingKind.Warehouse, world.Map.FoundingSite, 8);
                if (carts > 0)
                {
                    int racked = world.AnyStoreOf(StoreKind.Cart).Store.Add(Goods.Handcart, carts);
                    _o.WriteLine($"ZZG {label}-seed{seed} racked {racked} of {carts} handcarts in the founders' cart at Year 3");
                }

                // ⚠️ The harness valley raises NO market of its own (the opening marks a granary and a warehouse, and a
                // marked market is never built — the first run marked one and no valley raised it), so the marketer's trips
                // can only be measured here with one RAISED: ZZ_MARKET=1 raises a finished market near the founding, the way
                // TradeTests.RaiseAPost raises the post.
                if (Environment.GetEnvironmentVariable("ZZ_MARKET") == "1")
                {
                    Workplace market = RaiseFinished(world, BuildingKind.Market, JobKind.Marketer);
                    _o.WriteLine($"ZZG {label}-seed{seed} raised {market.Name} at ({market.Tile.X},{market.Tile.Y}) at Year 3");
                }

                if (Environment.GetEnvironmentVariable("ZZ_POST") == "1")
                {
                    Workplace post = TradeTests.RaiseAPost(world);
                    world.TradeKeep.Set(Goods.Firewood, 400);
                    _o.WriteLine($"ZZG {label}-seed{seed} raised {post.Name} at ({post.Tile.X},{post.Tile.Y}) at Year 3, keep 400 firewood (the limit is 600, so the woodcutter refills what the merchant carries down)");
                }
            });
        }
    }

    /// <summary>A finished building of a kind that is a workplace, near the founding — the post's shape (TradeTests.RaiseAPost).</summary>
    private static Workplace RaiseFinished(SimWorld world, BuildingKind kind, JobKind job)
    {
        GridPos founding = world.Map.FoundingSite;
        for (int r = 3; r < 30; r++)
        {
            for (int dy = -r; dy <= r; dy++)
            {
                for (int dx = -r; dx <= r; dx++)
                {
                    var at = new GridPos(founding.X + dx, founding.Y + dy);
                    if (!world.Map.Contains(at) || world.HasSomethingToHarvest(at) || !world.CanBuildAt(kind, at).Allowed || !world.Mark(kind, at).Allowed)
                    {
                        continue;
                    }

                    Workplace site = world.Workplaces.Single(w => w.Construction?.Kind == kind && w.Construction.IsFinished == false);
                    BuildFixtures.StockTheSite(site);
                    for (int i = 0; i <= site.Construction!.Recipe.WorkTicks; i++)
                    {
                        site.Construction.Work();
                    }

                    GridPos stood = site.Tile;
                    world.Complete(site);
                    return world.Workplaces.Single(w => w.Kind == job && w.Tile == stood && !w.IsSite);
                }
            }
        }

        throw new InvalidOperationException($"nowhere near the founding to raise a {kind}");
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
            or VillagerState.WalkingToTheWell or VillagerState.FetchingAHandcart or VillagerState.PushingAHandcart
            => Bucket.Fetch,
        VillagerState.CollectingForMarket or VillagerState.StockingTheMarket or VillagerState.WalkingToTheMarket => Bucket.Market,
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
        public bool Pushed;
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

        // Slice H: shopping trips (a fetch spell that turned at a shop) with the cart and without, household-years, carts taken.
        var tripsPushed = new List<int>();
        var tripsOnFoot = new List<int>();
        long housedHouseholdTicks = 0;
        long cartedHouseholdTicks = 0;
        int cartsTaken = 0;

        // The trades' carts (handcart.md §13): loads the marketer put on the counter and carried out of a buffer, loads the
        // merchant carried down to the post and up from it — each counted on the tick the carrier leaves the state with the
        // load, the units read on the tick before; and the carrier-ticks each trade worked, for trips a year.
        var trade = new TradeTrips();

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
                if (at != "elsewhere")
                {
                    (o.Pushed ? tripsPushed : tripsOnFoot).Add(ticks);
                }
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

            foreach (Household h in world.Households)
            {
                if (h.HasHome && world.LivingMembersOf(h) > 0)
                {
                    housedHouseholdTicks++;
                    cartedHouseholdTicks += h.HandcartUses > 0 ? 1 : 0;
                }
            }

            foreach (Villager v in world.Villagers)
            {
                bool counts = v.Alive && v.CanWork;
                open.TryGetValue(v, out Open? o);
                if (counts && v.State == VillagerState.PushingAHandcart && (o is null || o.Bucket != Bucket.Fetch || !o.Pushed))
                {
                    cartsTaken++;
                }

                if (o is not null && v.PushingACart)
                {
                    o.Pushed = true;
                }
                if (!counts)
                {
                    if (o is not null)
                    {
                        Close(v, o, t);
                        open.Remove(v);
                    }

                    continue;
                }

                trade.See(world, v);
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
                    o.Pushed = v.PushingACart;
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

        // Slice H (handcart.md §5–§7): the trips the cart made and the ground it left.
        tripsPushed.Sort();
        tripsOnFoot.Sort();
        double householdYears = housedHouseholdTicks / (double)ticksPerYear;
        int carted = world.Households.Count(h => h.HandcartUses > 0);
        int mapTiles = world.Map.Width * world.Map.Height;
        _o.WriteLine($"ZZG {label} trips shopping {tripsPushed.Count + tripsOnFoot.Count} perHouseholdYear {(tripsPushed.Count + tripsOnFoot.Count) / Math.Max(householdYears, 1e-9):F2} "
            + $"householdYears {householdYears:F1} pushed {tripsPushed.Count} {Med(tripsPushed)} onFoot {tripsOnFoot.Count} {Med(tripsOnFoot)}");
        _o.WriteLine($"ZZG {label} carts taken {cartsTaken} carted {carted} of {world.Households.Count(h => h.HasHome && world.LivingMembersOf(h) > 0)} housed "
            + $"cartedShare {cartedHouseholdTicks * 100.0 / Math.Max(housedHouseholdTicks, 1):F1}% uses {config.HandcartUses} "
            + $"onShelves {world.InStores(Goods.Handcart)} trodden {world.Paths.TroddenTiles} path {world.Paths.PathTiles} of {mapTiles} tiles "
            + $"({world.Paths.TroddenTiles * 100.0 / mapTiles:F1}% trodden, {world.Paths.PathTiles * 100.0 / mapTiles:F2}% path)");

        trade.Report(_o, label, ticksPerYear);
        foreach (Workplace w in world.Workplaces.Where(w => !w.IsSite && w.Kind is JobKind.Marketer or JobKind.Merchant))
        {
            _o.WriteLine($"ZZG {label} workscarts {w.Kind} {w.Name} at ({w.Tile.X},{w.Tile.Y}) uses {WorksCartUses(w)} workers {w.WorkerIds.Count}");
        }

        // Why a fetch is as long as it is: every lived-in home's walk to the nearest store holding any food, at the end —
        // and separately to the nearest market and the nearest storehouse holding food, the two D372 chooses between.
        var shops = world.StoreBuildings.Where(s => world.FoodIn(s.Store) > 0).ToList();
        var walks = new List<int>();
        var toMarket = new List<int>();
        var toStorehouse = new List<int>();
        var pairs = new List<(int market, int storehouse)>();
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

            if (market != int.MaxValue && storehouse != int.MaxValue)
            {
                pairs.Add((market, storehouse));
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

        // D670's SWEEP for `market_detour_ticks` (candidate (d), DESIGN §5): for each X, the homes that would walk to
        // the storehouse because the market is more than X one-way ticks further, and the one-way ticks each saves —
        // a static estimate from the end state, no sim change, so it runs on every format the harness can open.
        if (pairs.Count > 0)
        {
            int marketNearer = pairs.Count(p => p.market < p.storehouse);
            var line = new System.Text.StringBuilder($"ZZG {label} detour homesWithBoth {pairs.Count} marketNearer {marketNearer}");
            foreach (int x in new[] { 0, 2, 4, 6, 8, 12 })
            {
                var saved = pairs.Where(p => p.market > p.storehouse + x).Select(p => p.market - p.storehouse).ToList();
                saved.Sort();
                line.Append($" X{x} switch {saved.Count} saved {Med(saved)}");
            }

            _o.WriteLine(line.ToString());
        }

        static string Med(List<int> s) => s.Count == 0 ? "none" : $"median {s[s.Count / 2]} p90 {s[Math.Min(s.Count - 1, (int)(s.Count * 0.9))]} max {s[^1]}";
    }

    /// <summary>The works' cart's trips left — 0 before handcart.md §13 is built (the field does not exist yet), the field after.</summary>
    private static int WorksCartUses(Workplace w) =>
        typeof(Workplace).GetProperty("HandcartUses") is System.Reflection.PropertyInfo p ? (int)p.GetValue(w)! : 0;

    /// <summary>Whether the cart in their hands is the works' — false before §13 is built, the flag after.</summary>
    private static bool PushesTheWorksCart(Villager v) =>
        typeof(Villager).GetProperty("CartForTheWorks") is System.Reflection.PropertyInfo p && (bool)p.GetValue(v)!;

    private sealed class TradeTrips
    {
        private sealed class Last
        {
            public VillagerState State;
            public int Held;
            public bool Works;
        }

        private readonly Dictionary<Villager, Last> _last = new(ReferenceEqualityComparer.Instance);
        public long MarketerTicks, MerchantTicks;
        public long MarketerWalkingTicks, MerchantWalkingTicks, MarketNightTicks, PostNightTicks, MarketHomeLegTicks, PostHomeLegTicks;
        public int FirstLegs;
        public readonly List<int> ToCounter = new(), ToCounterCarted = new(), FromBuffer = new(), FromBufferCarted = new();
        public readonly List<int> ToPost = new(), ToPostCarted = new(), FromPost = new(), FromPostCarted = new();

        public void See(SimWorld world, Villager v)
        {
            JobKind? job = world.FindWorkplace(v.WorkplaceId)?.Kind;
            if (job == JobKind.Marketer) { MarketerTicks++; }
            if (job == JobKind.Merchant) { MerchantTicks++; }

            // §13.2b (D700): the trade's whole walking share, the cart's nights at a worker's door and its legs home.
            if (job is JobKind.Marketer or JobKind.Merchant)
            {
                bool walking = Array.IndexOf(Walking, Classify(v)) >= 0;
                bool night = PushesTheWorksCart(v) && v.State == VillagerState.Resting;
                bool homeLeg = PushesTheWorksCart(v) && v.State == VillagerState.TravelingHome;
                if (job == JobKind.Marketer)
                {
                    MarketerWalkingTicks += walking ? 1 : 0;
                    MarketNightTicks += night ? 1 : 0;
                    MarketHomeLegTicks += homeLeg ? 1 : 0;
                }
                else
                {
                    MerchantWalkingTicks += walking ? 1 : 0;
                    PostNightTicks += night ? 1 : 0;
                    PostHomeLegTicks += homeLeg ? 1 : 0;
                }
            }

            _last.TryGetValue(v, out Last? last);
            if (last is not null && last.State != v.State)
            {
                if (v.State == VillagerState.WalkingToTheMarket) { FirstLegs++; }

                // Leaving the carrying state: the load went where the state carried it (the arrival puts it down and
                // re-decides in the same tick, so the units are the tick before's).
                switch (last.State)
                {
                    case VillagerState.StockingTheMarket when last.Held > 0:
                        (last.Works ? ToCounterCarted : ToCounter).Add(last.Held);
                        break;
                    case VillagerState.CollectingForMarket when v.State == VillagerState.HaulingToStore && v.Carried.Held > 0:
                        (PushesTheWorksCart(v) ? FromBufferCarted : FromBuffer).Add(v.Carried.Held);
                        break;
                    case VillagerState.StockingThePost when last.Held > 0:
                        (last.Works ? ToPostCarted : ToPost).Add(last.Held);
                        break;
                    case VillagerState.FetchingFromThePost when v.State == VillagerState.HaulingToStore && v.Carried.Held > 0:
                        (PushesTheWorksCart(v) ? FromPostCarted : FromPost).Add(v.Carried.Held);
                        break;
                }
            }

            last ??= _last[v] = new Last();
            last.State = v.State;
            last.Held = v.Carried.Held;
            last.Works = PushesTheWorksCart(v);
        }

        public void Report(ITestOutputHelper o, string label, int ticksPerYear)
        {
            double marketerYears = MarketerTicks / (double)ticksPerYear;
            double merchantYears = MerchantTicks / (double)ticksPerYear;
            o.WriteLine($"ZZG {label} market: marketerYears {marketerYears:F1} toCounter {Line(ToCounter, ToCounterCarted, marketerYears)} fromBuffer {Line(FromBuffer, FromBufferCarted, marketerYears)} "
                + $"walking {MarketerWalkingTicks * 100.0 / Math.Max(1, MarketerTicks):F1}% firstLegs {FirstLegs} nightTicks {MarketNightTicks} homeLegTicks {MarketHomeLegTicks}");
            o.WriteLine($"ZZG {label} post: merchantYears {merchantYears:F1} toPost {Line(ToPost, ToPostCarted, merchantYears)} fromPost {Line(FromPost, FromPostCarted, merchantYears)} "
                + $"walking {MerchantWalkingTicks * 100.0 / Math.Max(1, MerchantTicks):F1}% nightTicks {PostNightTicks} homeLegTicks {PostHomeLegTicks}");
        }

        private static string Line(List<int> onFoot, List<int> carted, double years)
        {
            int n = onFoot.Count + carted.Count;
            long units = onFoot.Sum(x => (long)x) + carted.Sum(x => (long)x);
            return $"trips {n} perYear {n / Math.Max(years, 1e-9):F1} units {units} unitsATrip {(n == 0 ? 0 : units / (double)n):F0} "
                + $"carted {carted.Count} ({(carted.Count == 0 ? 0 : carted.Average()):F0} a trip) onFoot {onFoot.Count} ({(onFoot.Count == 0 ? 0 : onFoot.Average()):F0} a trip)";
        }
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

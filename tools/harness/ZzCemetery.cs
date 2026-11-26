using Kith.Sim.Config;
using Kith.Sim.Core;
using Kith.Sim.Persistence;
using Kith.Sim.World;
using Xunit;
using Xunit.Abstractions;

namespace Kith.Sim.Tests;

// ⛔ W4's MEASUREMENT HARNESS (D891) — kept in tools/harness/, COPIED into tests/Kith.Sim.Tests/ to run and DELETED from there
// before any commit (D420). `specs/cemetery.md §3`: how many die and when, where they fall, how far a plot stands from them,
// and what the two contentment causes would do — all read-only: nothing here changes the village it steps.
//
// Saves (ZZ_SAVES a COPY of Joe's, the NEWEST .save in each folder; ZZ_SHARD=k/n; ZZ_MIN people (12); ZZ_YEARS (20) stepped on):
//   ZZA — at load: year, people, adults, homes, the dead, leavers, the first death's year.
//   ZZP — the two posed plots (4x5 of clear, flat, unowned grass): NEAR = the cheapest mean walk from the lived-in homes; EDGE = the
//         same at least 8 tiles off every home. Mean and worst walk home->plot in ticks.
//   ZZG — a line a dead (src hist = already dead at load, step = died while stepped): the year, the season 0-3, cause, age, tiles
//         from their own home to where they lie, ticks from the NEAR plot and the EDGE plot to there (-1 unreachable), and how many
//         living adults their household / partner left.
//   ZZY — a line a year: people, adults, deaths, births, laborers (the mean of four seasons).
//   ZZC — the run's totals per arm: adult-seasons, unsettled, people ever thinking of leaving (4), people who would go (8, the
//         couple rule), couple-seasons unsettled. Arms (the cause added on top of the village's own, the tavern's ease honoured):
//         base; all = every adult, always; village4 = every adult for 4 seasons after any death; family4 / family8 = the dead
//         person's household and partner, 4 / 8 seasons from the season after the death; famstand = the same, for as long as they
//         live (never buried).
// Valleys (ZZ_VALLEYS=1, seeds 200-299, ZZ_SEEDS narrows; ZZ_YEARS 50): the same lines on the unattended harness village.
// Copy in; `dotnet test tests/Kith.Sim.Tests -c Release --filter ZzCemetery --logger "console;verbosity=detailed"`; delete;
// then `python -X utf8 -I tools/harness/cemetery.py <logs>`.
public sealed class ZzCemetery
{
    private static readonly string[] Arms = { "base", "all", "village4", "family4", "family8", "famstand" };
    private readonly ITestOutputHelper _o;

    public ZzCemetery(ITestOutputHelper o) => _o = o;

    [Theory]
    [MemberData(nameof(Runs))]
    public void OnTheSaves(string what)
    {
        if (what.Length == 0)
        {
            return;
        }

        SaveOpened opened = SaveFile.Open(what, ShippedConfig.Load());
        if (opened.Loop is not SimLoop loop)
        {
            _o.WriteLine($"ZZX {Path.GetFileName(Path.GetDirectoryName(what))} refused {opened.Refusal}");
            return;
        }

        if (loop.World.Population < Env("ZZ_MIN", 12))
        {
            _o.WriteLine($"ZZX {Path.GetFileName(Path.GetDirectoryName(what))} small {loop.World.Population} year {loop.World.Clock.Year}");
            return;
        }

        Measure(loop, "save", Path.GetFileName(Path.GetDirectoryName(what)) ?? what, Env("ZZ_YEARS", 20));
    }

    [Theory]
    [MemberData(nameof(Valleys))]
    public void OnTheHarness(ulong seed)
    {
        if (seed == 0UL)
        {
            return;
        }

        SimConfig config = ShippedConfig.Load() with { Seed = seed };
        SimLoop loop = SimFactory.CreatePhase0(config, new Kith.Sim.Logging.InMemoryLogSink());
        SimWorld world = loop.World;
        var limits = config.StartingStockLimits;
        for (int id = 0; id < world.GoodsCatalog.Count; id++)
        {
            if (limits.TryGetValue(world.GoodsCatalog[id].Name, out int limit))
            {
                world.SetStockLimit((Goods)id, limit);
            }
        }

        ColdStartTests.PlayTheOpening(world);
        loop.Step(2 * config.TicksPerYear);
        ColdStartTests.MarkSomewhereNear(world, BuildingKind.Granary, world.Map.FoundingSite, 8);
        ColdStartTests.MarkSomewhereNear(world, BuildingKind.Warehouse, world.Map.FoundingSite, 8);
        Measure(loop, "valley", "v" + seed, Env("ZZ_YEARS", 50) - 2);
    }

    private void Measure(SimLoop loop, string kind, string village, int years)
    {
        SimWorld world = loop.World;
        SimConfig c = world.Config;
        ulong seasonTicks = (ulong)c.TicksPerSeason;
        var deadAtLoad = world.Villagers.Where(v => !v.Alive && v.DiedAtTick is not null).ToList();
        _o.WriteLine(
            $"ZZA {kind} {village} year {world.Clock.Year} people {world.Population} adults {world.Villagers.Count(v => v.Alive && v.CanWork)} "
            + $"homes {LivedIn(world).Count()} dead {deadAtLoad.Count} left {world.Villagers.Count(v => v.LeftAtTick is not null)} "
            + $"first {(deadAtLoad.Count == 0 ? -1 : deadAtLoad.Min(v => YearOf(v.DiedAtTick!.Value, c)))}");

        (GridPos? near, GridPos? edge) = PosePlots(world, village);
        foreach (Villager v in deadAtLoad)
        {
            SayTheDead(world, village, "hist", v, near, edge);
        }

        var known = new HashSet<int>(world.Villagers.Where(v => !v.Alive).Select(v => v.Id));
        var seen = new HashSet<int>(world.Villagers.Select(v => v.Id));
        var diedSeason = new Dictionary<int, int>(); // a dead person's id -> the season number they died in, since load
        int n = Arms.Length;
        var streak = new Dictionary<int, int>[n];
        var gone = new HashSet<int>[n];
        var thinking = new HashSet<int>[n];
        var adultSeasons = new int[n];
        var unsettled = new int[n];
        var coupleSeasons = new int[n];
        var coupleUnsettled = new int[n];
        for (int a = 0; a < n; a++)
        {
            streak[a] = new Dictionary<int, int>();
            gone[a] = new HashSet<int>();
            thinking[a] = new HashSet<int>();
            foreach (Villager v in world.Villagers)
            {
                streak[a][v.Id] = v.SeasonsUnsettled;
            }
        }

        int yDeaths = 0, yBirths = 0, yLaborers = 0, lastDeathSeason = -1000;
        ulong end = world.Tick + ((ulong)years * (ulong)c.TicksPerYear);
        while (world.Tick < end)
        {
            loop.StepOnce();
            foreach (Villager v in world.Villagers)
            {
                if (seen.Add(v.Id))
                {
                    yBirths++;
                }

                if (!v.Alive && known.Add(v.Id) && v.DiedAtTick is not null)
                {
                    yDeaths++;
                    diedSeason[v.Id] = world.SeasonNumber;
                    lastDeathSeason = world.SeasonNumber;
                    SayTheDead(world, village, "step", v, near, edge);
                }
            }

            if (world.Tick % seasonTicks == 10UL)
            {
                int ended = world.SeasonNumber - 1;
                yLaborers += world.Villagers.Count(v => v.Alive && v.IsLaborer);
                for (int a = 0; a < n; a++)
                {
                    ReadArm(world, a, ended);
                }
            }

            if (world.Tick % (ulong)c.TicksPerYear == 0UL)
            {
                _o.WriteLine($"ZZY {kind} {village} year {world.Clock.Year - 1} people {world.Population} "
                    + $"adults {world.Villagers.Count(v => v.Alive && v.CanWork)} deaths {yDeaths} births {yBirths} laborers {yLaborers / 4}");
                yDeaths = yBirths = yLaborers = 0;
            }
        }

        for (int a = 0; a < n; a++)
        {
            _o.WriteLine($"ZZC {kind} {village} {Arms[a]} adultseasons {adultSeasons[a]} unsettled {unsettled[a]} thinking {thinking[a].Count} "
                + $"gone {gone[a].Count} coupleseasons {coupleSeasons[a]} coupleunsettled {coupleUnsettled[a]} "
                + $"reallyleft {world.Villagers.Count(v => v.LeftAtTick is not null)} people {world.Population}");
        }

        void ReadArm(SimWorld w, int arm, int ended)
        {
            var households = new HashSet<int>();
            var partners = new HashSet<int>();
            foreach (KeyValuePair<int, int> d in diedSeason)
            {
                int since = ended - d.Value; // 0 = died in the season just ended
                bool held = arm switch
                {
                    3 => since is >= 1 and <= 4,
                    4 => since is >= 1 and <= 8,
                    5 => since >= 1,
                    _ => false,
                };
                if (held && w.FindVillager(d.Key) is Villager dead)
                {
                    households.Add(dead.HouseholdId);
                    partners.Add(dead.PartnerId);
                }
            }

            bool villageWide = arm == 1 || (arm == 2 && ended - lastDeathSeason is >= 0 and < 4);
            foreach (Villager v in w.Villagers)
            {
                if (!v.Alive || !v.CanWork || gone[arm].Contains(v.Id))
                {
                    continue;
                }

                bool extra = villageWide || households.Contains(v.HouseholdId) || partners.Contains(v.Id);
                int weight = Bits(v.ContentmentCauses) + (extra ? 1 : 0);

                // Every shipped comfort eases 1, off a held cause only; the read records it only where a cause was held, so an adult
                // with the extra alone reads 1, below the line of 2 either way.
                weight = Math.Max(0, weight - Bits(v.ContentmentComforts));
                int now = weight >= c.ContentmentUnsettledAt ? streak[arm].GetValueOrDefault(v.Id) + 1 : 0;
                streak[arm][v.Id] = now;
                adultSeasons[arm]++;
                unsettled[arm] += now > 0 ? 1 : 0;
                if (now == c.ContentmentThinkingOfLeavingSeasons)
                {
                    thinking[arm].Add(v.Id);
                }
            }

            foreach (Villager v in w.Villagers)
            {
                if (!v.Alive || !v.CanWork || gone[arm].Contains(v.Id))
                {
                    continue;
                }

                Villager? partner = w.FindVillager(v.PartnerId) is { Alive: true } p && !gone[arm].Contains(p.Id) ? p : null;
                if (partner is not null && partner.Id > v.Id)
                {
                    coupleSeasons[arm]++;
                    coupleUnsettled[arm] += streak[arm].GetValueOrDefault(v.Id) > 0 || streak[arm].GetValueOrDefault(partner.Id) > 0 ? 1 : 0;
                }

                int leaving = c.ContentmentLeavingSeasons;
                if (streak[arm].GetValueOrDefault(v.Id) >= leaving && (partner is null || streak[arm].GetValueOrDefault(partner.Id) >= leaving))
                {
                    gone[arm].Add(v.Id);
                    if (partner is not null)
                    {
                        gone[arm].Add(partner.Id);
                    }
                }
            }
        }
    }

    private void SayTheDead(SimWorld world, string village, string src, Villager v, GridPos? near, GridPos? edge)
    {
        SimConfig c = world.Config;
        GridPos? home = world.FindHousehold(v.HouseholdId)?.HomeTile;
        int far = home is GridPos h ? Math.Abs(h.X - v.Tile.X) + Math.Abs(h.Y - v.Tile.Y) : -1;
        int left = world.Villagers.Count(o => o.Alive && o.CanWork && (o.HouseholdId == v.HouseholdId || o.PartnerId == v.Id));
        ulong at = v.DiedAtTick!.Value;
        _o.WriteLine($"ZZG {village} {src} year {YearOf(at, c)} season {(int)SimClock.FromTick(at, c).Season} cause {v.CauseOfDeath} age {v.AgeYears} "
            + $"tohome {far} near {Ticks(world, near, v.Tile)} edge {Ticks(world, edge, v.Tile)} kin {left} tick {at}");
    }

    private (GridPos? Near, GridPos? Edge) PosePlots(SimWorld world, string village)
    {
        List<GridPos> homes = LivedIn(world).Select(x => x.HomeTile!.Value).ToList();
        if (homes.Count == 0)
        {
            return (null, null);
        }

        var middle = new GridPos((int)homes.Average(t => t.X), (int)homes.Average(t => t.Y));
        var clear = new Dictionary<GridPos, bool>();
        bool Clear(GridPos t)
        {
            if (clear.TryGetValue(t, out bool known))
            {
                return known;
            }

            bool ok = world.Map.Contains(t) && world.Map.TerrainAt(t) == Terrain.Grass && world.Zones.WorkGroundOwner(t) == 0
                && world.Zones.PlotOwner(t) == 0 && !world.Zones.IsResidential(t) && world.CanBuildAt(BuildingKind.Well, t).Allowed;
            clear[t] = ok;
            return ok;
        }

        GridPos? near = null, edge = null;
        long bestNear = long.MaxValue, bestEdge = long.MaxValue;
        for (int dy = -30; dy <= 30; dy++)
        {
            for (int dx = -30; dx <= 30; dx++)
            {
                for (int turn = 0; turn < 2; turn++)
                {
                    (int pw, int ph) = PlotSize();
                    int w = turn == 0 ? pw : ph, hgt = turn == 0 ? ph : pw;
                    bool ok = true;
                    for (int y = 0; y < hgt && ok; y++)
                    {
                        for (int x = 0; x < w && ok; x++)
                        {
                            ok = Clear(new GridPos(middle.X + dx + x, middle.Y + dy + y));
                        }
                    }

                    if (!ok)
                    {
                        continue;
                    }

                    var centre = new GridPos(middle.X + dx + (w / 2), middle.Y + dy + (hgt / 2));
                    long sum = 0;
                    int closest = int.MaxValue;
                    foreach (GridPos home in homes)
                    {
                        int cost = world.TravelCost.Cost(home, centre);
                        sum = cost == TravelCostField.Unreachable || sum == long.MaxValue ? long.MaxValue : sum + cost;
                        closest = Math.Min(closest, Math.Abs(home.X - centre.X) + Math.Abs(home.Y - centre.Y));
                    }

                    if (sum < bestNear)
                    {
                        (bestNear, near) = (sum, centre);
                    }

                    if (closest >= 8 && sum < bestEdge)
                    {
                        (bestEdge, edge) = (sum, centre);
                    }
                }
            }
        }

        foreach ((string name, GridPos? plot) in new[] { ("near", near), ("edge", edge) })
        {
            List<int> walks = homes.Select(x => Ticks(world, plot, x)).Where(x => x >= 0).ToList();
            _o.WriteLine($"ZZP {village} {name} {(plot is GridPos p ? $"{p.X},{p.Y}" : "none")} "
                + $"mean {(walks.Count == 0 ? -1 : walks.Average()):0.0} worst {(walks.Count == 0 ? -1 : walks.Max())} clear {clear.Count(k => k.Value)} size {PlotSize().W}x{PlotSize().H}");
        }

        return (near, edge);
    }

    // ZZ_PLOT=WxH (default 4x5): the plot posed, for D904's three sizes.
    private static (int W, int H) PlotSize()
    {
        string[] p = (Environment.GetEnvironmentVariable("ZZ_PLOT") ?? "4x5").Split('x');
        return (int.Parse(p[0]), int.Parse(p[1]));
    }

    private static int Ticks(SimWorld world, GridPos? from, GridPos to)
    {
        if (from is not GridPos f)
        {
            return -1;
        }

        int t = world.TravelCost.TicksBetween(f, to);
        return t == TravelCostField.Unreachable ? -1 : t;
    }

    private static IEnumerable<Household> LivedIn(SimWorld world) =>
        world.Households.Where(h => h.HomeTile is not null && world.LivingMembersOf(h) > 0);

    private static int YearOf(ulong tick, SimConfig c) => SimClock.FromTick(tick, c).Year;

    private static int Bits(int mask) => System.Numerics.BitOperations.PopCount((uint)mask);

    public static IEnumerable<object[]> Valleys()
    {
        if (Environment.GetEnvironmentVariable("ZZ_VALLEYS") is null)
        {
            yield return new object[] { 0UL };
            yield break;
        }

        IEnumerable<ulong> seeds = Enumerable.Range(200, 100).Select(x => (ulong)x);
        if (Environment.GetEnvironmentVariable("ZZ_SEEDS") is string zs)
        {
            seeds = zs.Split(',').Select(x => ulong.Parse(x, System.Globalization.CultureInfo.InvariantCulture)).ToArray();
        }

        foreach (ulong seed in seeds)
        {
            yield return new object[] { seed };
        }
    }

    private static int Env(string name, int fallback) =>
        int.TryParse(Environment.GetEnvironmentVariable(name), System.Globalization.NumberStyles.Integer,
            System.Globalization.CultureInfo.InvariantCulture, out int value) ? value : fallback;

    public static IEnumerable<object[]> Runs()
    {
        if (Environment.GetEnvironmentVariable("ZZ_SAVES") is not string folder)
        {
            yield return new object[] { string.Empty };
            yield break;
        }

        string? shard = Environment.GetEnvironmentVariable("ZZ_SHARD");
        int k = 0, n = 1;
        if (shard is not null && shard.Split('/') is [string a, string b])
        {
            k = int.Parse(a, System.Globalization.CultureInfo.InvariantCulture);
            n = int.Parse(b, System.Globalization.CultureInfo.InvariantCulture);
        }

        int index = 0;
        foreach (string dir in Directory.GetDirectories(folder).OrderBy(d => d, StringComparer.Ordinal))
        {
            string? newest = Directory.GetFiles(dir, "*.save")
                .OrderByDescending(File.GetLastWriteTimeUtc).ThenBy(p => p, StringComparer.Ordinal).FirstOrDefault();
            if (newest is not null && index++ % n == k)
            {
                yield return new object[] { newest };
            }
        }
    }
}

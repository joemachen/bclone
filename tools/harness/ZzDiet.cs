using Kith.Sim.Config;
using Kith.Sim.Core;
using Kith.Sim.Persistence;
using Kith.Sim.World;
using Xunit;
using Xunit.Abstractions;

namespace Kith.Sim.Tests;

// ⛔ D933's MEASUREMENT HARNESS — kept in tools/harness/, COPIED into tests/Kith.Sim.Tests/ to run and DELETED from there
// before any commit (D420). `specs/health.md §4`: how varied a village's diet actually is, before any number is set.
//
// What a person ate is not kept anywhere in the sim, so it is read off the meal: each tick every living villager's arms and
// household larder are copied, the tick is stepped, and a villager whose hunger fell ate — their plate is what
// `SimWorld.FoodToTake` takes from the copy (arms first if they held a meal, as `BehaviorSystem.TryEat` does). Two members of
// one household eating on the same tick each read the larder as it was before both; the error is a unit, not a pattern.
//
// Food groups (D357): grain = wheat, barley, bread; fish; meat; forage (fruit and herbs).
//
// Two facts:
//   OnSaves  — ZZ_SAVES (a folder of COPIES of Joe's saves, one sub-folder each, the newest .save in it), ZZ_YEARS (5) on.
//   OnHarness — ZzW5's "every" opening (played opening, a lodge, a fishery and a farm raised free), seeds ZZ_SEEDS (1–24),
//              ZZ_YEARS (50); diet counted from Year 3.
// One ZZD line a village: alive, groups' share of every unit eaten, and per villager-year the top group's share bucketed
// (<50 / 50–75 / 75–90 / 90–99 / 100 %) and the groups eaten at ≥ 10 %; ZZY one line a village-year (the same, by year);
// ZZG the goods the village holds at the start.
//
// Copy in; `dotnet test tests/Kith.Sim.Tests -c Release --filter ZzDiet --logger "console;verbosity=detailed"`; delete.
public sealed class ZzDiet
{
    private readonly ITestOutputHelper _o;

    public ZzDiet(ITestOutputHelper o) => _o = o;

    private static readonly string[] GroupNames = { "grain", "fish", "meat", "forage" };

    private static int GroupOf(Goods g) => g switch
    {
        Goods.Wheat or Goods.Barley or Goods.Bread => 0,
        Goods.Fish => 1,
        Goods.Meat => 2,
        Goods.Produce => 3,
        _ => throw new InvalidOperationException($"no group for {g}"),
    };

    [Theory]
    [MemberData(nameof(Saves))]
    public void OnSaves(string folder)
    {
        string? root = Environment.GetEnvironmentVariable("ZZ_SAVES");
        if (root is null) { return; }
        int years = int.Parse(Environment.GetEnvironmentVariable("ZZ_YEARS") ?? "5");
        string file = Directory.GetFiles(Path.Combine(root, folder), "*.save").OrderByDescending(File.GetLastWriteTimeUtc).First();
        SaveOpened opened = SaveFile.Open(file, ShippedConfig.Load());
        if (opened.Loop is null)
        {
            _o.WriteLine($"ZZD {folder} refused {opened.Refusal}");
            return;
        }

        Measure(folder, opened.Loop, years, 0);
    }

    // ZZ_ALL: a folder of padded COPIES; every .save in every sub-folder, read at load only (ZZA: year, alive, food held by group).
    [Fact]
    public void OnEverySave()
    {
        string? root = Environment.GetEnvironmentVariable("ZZ_ALL");
        if (root is null) { return; }
        foreach (string file in Directory.GetFiles(root, "*.save", SearchOption.AllDirectories))
        {
            SaveOpened opened = SaveFile.Open(file, ShippedConfig.Load());
            if (opened.Loop is null) { continue; }
            SimWorld world = opened.Loop.World;
            var g = new long[4];
            foreach (Goods good in world.GoodsCatalog.EdibleGoods)
            {
                g[GroupOf(good)] += world.InStores(good) + world.Households.Sum(h => h.Stockpile[good]);
            }

            var ever = new long[4];
            foreach (Goods good in world.GoodsCatalog.EdibleGoods)
            {
                ever[GroupOf(good)] += world.FoodEverProducedOf(good);
            }

            _o.WriteLine($"ZZA {Path.GetFileName(Path.GetDirectoryName(file))} {Path.GetFileName(file)} Y{world.Clock.Year} alive {world.Population} held {string.Join(" ", GroupNames.Select((n, k) => $"{n}={g[k]}"))} ever {string.Join(" ", GroupNames.Select((n, k) => $"{n}={ever[k]}"))}");
        }
    }

    [Theory]
    [MemberData(nameof(Seeds))]
    public void OnHarness(int seed)
    {
        if (Environment.GetEnvironmentVariable("ZZ_HARNESS") != "1") { return; }
        int years = int.Parse(Environment.GetEnvironmentVariable("ZZ_YEARS") ?? "50");
        SimConfig config = ShippedConfig.Load() with { Seed = (ulong)seed };
        SimLoop loop = SimFactory.CreatePhase0(config, new Kith.Sim.Logging.InMemoryLogSink());
        SimWorld world = loop.World;
        ColdStartTests.PlayTheOpening(world);
        HuntingTests.RaiseALodgeFor(world);
        FishingTests.RaiseAFishery(world);
        Workplace farm = FarmFixtures.RaiseAFarm(world);
        FarmFixtures.GiveItGround(world, farm, 3);
        Measure($"seed{seed}", loop, years, 0, markAt: 3);
    }

    private void Measure(string label, SimLoop loop, int years, int fromYear, int markAt = 0)
    {
        SimWorld world = loop.World;
        SimConfig config = world.Config;
        IReadOnlyList<Goods> edible = world.GoodsCatalog.EdibleGoods;
        int startYear = world.Clock.Year;
        int alive0 = world.Population;

        string held = string.Join(" ", edible.Select(g => $"{g}={world.InStores(g) + world.Households.Sum(h => h.Stockpile[g])}"));
        _o.WriteLine($"ZZG {label} Y{startYear} alive {alive0} {held}");

        // per villager, per year index: units by group
        var eaten = new Dictionary<(int Villager, int Year), long[]>();
        // per villager, per season index: units by group, then units out of their own arms (slot 4)
        var seasons = new Dictionary<int, Dictionary<int, long[]>>();
        var total = new long[4];
        var snap = new Dictionary<Stockpile, int[]>(ReferenceEqualityComparer.Instance);
        var hunger = new Dictionary<int, int>();

        for (int t = 0; t < config.TicksPerYear * years; t++)
        {
            if (markAt > 0 && t == config.TicksPerYear * markAt)
            {
                ColdStartTests.MarkSomewhereNear(world, BuildingKind.Granary, world.Map.FoundingSite, 8);
                ColdStartTests.MarkSomewhereNear(world, BuildingKind.Warehouse, world.Map.FoundingSite, 8);
            }

            bool counting = t >= config.TicksPerYear * fromYear;
            if (counting)
            {
                snap.Clear();
                hunger.Clear();
                foreach (Villager v in world.Villagers)
                {
                    if (!v.Alive) { continue; }
                    hunger[v.Id] = v.Hunger;
                    Copy(v.Carried);
                    Copy(world.HouseholdOf(v).Stockpile);
                }
            }

            loop.StepOnce();
            if (!counting) { continue; }

            int yi = (t - config.TicksPerYear * fromYear) / config.TicksPerYear;
            foreach (Villager v in world.Villagers)
            {
                if (!v.Alive || !hunger.TryGetValue(v.Id, out int before) || v.Hunger >= before) { continue; }
                int cost = Systems.BehaviorSystem.MealCostFor(v, config);
                // A household that changed in the step (a wedding, a move) was not copied: skip that one meal.
                if (!snap.TryGetValue(v.Carried, out int[]? arms)
                    || !snap.TryGetValue(world.HouseholdOf(v).Stockpile, out int[]? larder)) { continue; }
                int[] from = arms.Sum() >= cost ? arms : larder;
                var pile = new Stockpile(Stockpile.Kinds);
                for (int k = 0; k < edible.Count; k++)
                {
                    if (from[k] > 0) { pile.Add(edible[k], from[k]); }
                }

                if (!eaten.TryGetValue((v.Id, yi), out long[]? row))
                {
                    row = new long[4];
                    eaten[(v.Id, yi)] = row;
                }

                int si = world.Clock.Year * 4 + (int)world.Clock.Season;
                if (!seasons.TryGetValue(v.Id, out Dictionary<int, long[]>? byS)) { byS = new(); seasons[v.Id] = byS; }
                if (!byS.TryGetValue(si, out long[]? srow)) { srow = new long[5]; byS[si] = srow; }
                foreach ((Goods good, int amount) in world.FoodToTake(pile, cost))
                {
                    row[GroupOf(good)] += amount;
                    total[GroupOf(good)] += amount;
                    srow[GroupOf(good)] += amount;
                    if (ReferenceEquals(from, arms)) { srow[4] += amount; }
                }
            }
        }

        _o.WriteLine($"ZZD {label} Y{startYear}+{years} alive {alive0}->{world.Population} {Summary(eaten.Values, total)}");
        for (int y = 0; y < years - fromYear; y++)
        {
            var rows = eaten.Where(kv => kv.Key.Year == y).Select(kv => kv.Value).ToList();
            var yt = new long[4];
            foreach (long[] r in rows) { for (int g = 0; g < 4; g++) { yt[g] += r[g]; } }
            _o.WriteLine($"ZZY {label} y{y} {Summary(rows, yt)}");
        }

        // The rule as specced (health.md §5.2–5.3): a season is plain at >= 90 % one group (read only with >= 6 meals' units);
        // plain +1, varied -2 (floor 0); told at 4, run down at 12. Counted from this run's first tick.
        int told = 0, rundown = 0, people = 0, plainS = 0, readS = 0, rdSeasons = 0;
        var firstTold = new List<int>();
        var firstRd = new List<int>();
        var plainTop = new long[4];
        long plainArms = 0, plainUnits = 0;
        int seasonsRun = config.TicksPerYear * years / (config.TicksPerDay * config.DaysPerSeason);
        foreach ((int id, Dictionary<int, long[]> byS) in seasons)
        {
            people++;
            int c = 0, ft = -1, fr = -1;
            foreach (int si in byS.Keys.OrderBy(k => k))
            {
                long[] r = byS[si];
                long sum = r[0] + r[1] + r[2] + r[3];
                if (sum < 36) { continue; }
                readS++;
                long topv = new[] { r[0], r[1], r[2], r[3] }.Max();
                if (topv * 10 >= sum * 9)
                {
                    plainS++;
                    plainTop[Array.IndexOf(r, topv)]++;
                    plainArms += r[4];
                    plainUnits += sum;
                    c++;
                }
                else { c = Math.Max(0, c - 2); }
                if (c >= 12) { rdSeasons++; }
                if (c >= 4 && ft < 0) { ft = si; }
                if (c >= 12 && fr < 0) { fr = si; }
            }

            if (ft >= 0) { told++; firstTold.Add(ft); }
            if (fr >= 0) { rundown++; firstRd.Add(fr); }
        }

        string Med(List<int> xs) => xs.Count == 0 ? "-" : $"{xs.OrderBy(x => x).ElementAt(xs.Count / 2) / 4.0:F1}y";
        _o.WriteLine($"ZZR {label} Y{startYear} seasons {seasonsRun} people {people} readS {readS} plainS {plainS} "
            + $"told {told} (median {Med(firstTold)}) rundown {rundown} (median {Med(firstRd)}) rdPersonSeasons {rdSeasons} "
            + $"plainTop {string.Join(" ", GroupNames.Select((n, g) => $"{n}={plainTop[g]}"))} plainFromArms {(plainUnits == 0 ? 0 : plainArms * 100 / plainUnits)}%");

        void Copy(Stockpile s)
        {
            if (snap.ContainsKey(s)) { return; }
            var a = new int[edible.Count];
            for (int k = 0; k < edible.Count; k++) { a[k] = s[edible[k]]; }
            snap[s] = a;
        }
    }

    private static string Summary(IEnumerable<long[]> rows, long[] total)
    {
        long all = total.Sum();
        string shares = string.Join(" ", GroupNames.Select((n, g) => $"{n}={(all == 0 ? 0 : total[g] * 100 / all)}%"));
        var buckets = new int[5];
        var groupsAt10 = new int[5];
        int n = 0;
        foreach (long[] r in rows)
        {
            long sum = r.Sum();
            if (sum < 20) { continue; } // a part-year (born, died, arrived): too few meals to read
            n++;
            long top = r.Max() * 100 / sum;
            buckets[top < 50 ? 0 : top < 75 ? 1 : top < 90 ? 2 : top < 100 ? 3 : 4]++;
            groupsAt10[r.Count(x => x * 10 >= sum)]++;
        }

        return $"{shares} vyears {n} top<50/50-75/75-90/90-99/100 {string.Join("/", buckets)} groups>=10%:1/2/3/4 {groupsAt10[1]}/{groupsAt10[2]}/{groupsAt10[3]}/{groupsAt10[4]}";
    }

    public static IEnumerable<object[]> Saves()
    {
        string? root = Environment.GetEnvironmentVariable("ZZ_SAVES");
        if (root is null) { return new[] { new object[] { "none" } }; }
        return Directory.GetDirectories(root).Select(d => new object[] { Path.GetFileName(d) });
    }

    public static IEnumerable<object[]> Seeds()
    {
        IEnumerable<int> seeds = Enumerable.Range(1, 24);
        if (Environment.GetEnvironmentVariable("ZZ_SEEDS") is string listed)
        {
            seeds = listed.Split(',').Select(int.Parse);
        }

        return seeds.Select(s => new object[] { s });
    }
}

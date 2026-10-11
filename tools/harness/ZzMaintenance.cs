using Bclone.Sim.Config;
using Bclone.Sim.Core;
using Bclone.Sim.World;
using Xunit;
using Xunit.Abstractions;

namespace Bclone.Sim.Tests;

// ⛔ D552's MEASUREMENT HARNESS for building condition and maintenance (specs/building-condition.md §8) — kept in
// tools/harness/, COPIED into tests/Bclone.Sim.Tests/ to run and DELETED from there before any commit. Never
// committed under tests/.
//
//   ZZM — one line per village per sampled year (5, 10, 20, 30, 40, 50): the people alive, every standing
//         building by kind with its age in years (first seen by the harness; nothing in the sim stamps a
//         raise but bridges and the town hall), the logs and stone those standing recipes cost, and the
//         village's wood flow — logs ever felled, ever split, in stores now — so a wear rate can be priced
//         against what the village actually has before a number is typed. Arms `shipped` / `every`.
//         ZZ_SEEDS narrows the seeds; ZZ_YEARS the horizon (50).
public sealed class ZzMaintenance
{
    private readonly ITestOutputHelper _o;

    public ZzMaintenance(ITestOutputHelper o) => _o = o;

    [Theory]
    [MemberData(nameof(Villages))]
    public void Standing(string arm, ulong seed)
    {
        SimConfig config = ShippedConfig.Load() with { Seed = seed };
        SimLoop loop = SimFactory.CreatePhase0(config, new Bclone.Sim.Logging.InMemoryLogSink());
        SimWorld world = loop.World;
        foreach (var (name, limit) in config.StartingStockLimits)
        {
            for (int id = 0; id < world.GoodsCatalog.Count; id++)
            {
                if (world.GoodsCatalog[id].Name == name) { world.SetStockLimit((Goods)id, limit); }
            }
        }

        ColdStartTests.PlayTheOpening(world);
        if (arm == "every")
        {
            HuntingTests.RaiseALodgeFor(world);
            FishingTests.RaiseAFishery(world);
            Workplace farm = FarmFixtures.RaiseAFarm(world);
            FarmFixtures.GiveItGround(world, farm, 3);
        }

        int years = int.Parse(Environment.GetEnvironmentVariable("ZZ_YEARS") ?? "50");
        var samples = new HashSet<int> { 5, 10, 20, 30, 40, 50 };

        // First year each standing thing was seen, by an identity the sim already has.
        var firstSeen = new Dictionary<string, int>();
        var recipes = new Dictionary<BuildingKind, BuildingRecipe>();
        BuildingRecipe Recipe(BuildingKind kind)
        {
            if (!recipes.TryGetValue(kind, out BuildingRecipe? r)) { r = BuildingRecipe.For(kind, config); recipes[kind] = r; }
            return r;
        }

        int felledLast = 0, splitLast = 0;
        for (int year = 1; year <= years; year++)
        {
            if (year == 3)
            {
                ColdStartTests.MarkSomewhereNear(world, BuildingKind.Granary, world.Map.FoundingSite, 8);
                ColdStartTests.MarkSomewhereNear(world, BuildingKind.Warehouse, world.Map.FoundingSite, 8);
            }

            // Walk the year a season at a time so a building raised mid-year is seen in the year it stood.
            for (int season = 0; season < 4; season++)
            {
                loop.Step(config.TicksPerSeason);
                foreach ((string key, BuildingKind _) in WhatStands(world)) { firstSeen.TryAdd(key, year); }
            }

            if (!samples.Contains(year) && year != years) { continue; }

            var standing = WhatStands(world).ToList();
            var byKind = new SortedDictionary<string, List<int>>(StringComparer.Ordinal);
            int logsStanding = 0, stoneStanding = 0;
            foreach ((string key, BuildingKind kind) in standing)
            {
                int age = year - firstSeen[key];
                string k = kind.ToString();
                if (!byKind.TryGetValue(k, out List<int>? ages)) { ages = new List<int>(); byKind[k] = ages; }
                ages.Add(age);
                BuildingRecipe r = Recipe(kind);
                logsStanding += r.Of(Goods.Logs);
                stoneStanding += r.Of(Goods.Stone);
            }

            string kinds = string.Join(" ", byKind.Select(kv => $"{kv.Key}:{string.Join(",", kv.Value.OrderByDescending(a => a))}"));
            int felled = world.LogsEverFelled, split = world.LogsEverSplit;
            _o.WriteLine(
                $"ZZM {arm} {seed} year {year} alive {world.Population} standing {standing.Count} logs-standing {logsStanding} "
                + $"stone-standing {stoneStanding} felled {felled} split {split} logs-stored {world.InStores(Goods.Logs)} "
                + $"stone-stored {world.InStores(Goods.Stone)} firewood-stored {world.InStores(Goods.Firewood)} "
                + $"felled-since {felled - felledLast} split-since {split - splitLast} | {kinds}");
            felledLast = felled;
            splitLast = split;
        }
    }

    /// <summary>Everything standing, each with a key that is stable for its life, and the row it was raised from.</summary>
    private static IEnumerable<(string Key, BuildingKind Kind)> WhatStands(SimWorld world)
    {
        foreach (Workplace w in world.Workplaces)
        {
            if (w.IsSite) { continue; }
            if (world.JobsCatalog.WorksAt(w.Kind) is BuildingKind kind && kind != BuildingKind.BuilderHut)
            {
                // The market is a store and a workplace on one tile; count it once, as the store below.
                if (kind == BuildingKind.Market) { continue; }
                yield return ($"w{w.Id}", kind);
            }
        }

        foreach (StoreBuilding s in world.StoreBuildings)
        {
            BuildingKind? kind = s.RaisedAs ?? world.BuildingKindOf(s);
            if (kind is null || kind == BuildingKind.Pile) { continue; }
            yield return ($"s{s.Id}", kind.Value);
        }

        foreach (Library l in world.Libraries) { yield return ($"l{l.Tile.X},{l.Tile.Y}", BuildingKind.Library); }
        foreach (Well w in world.Wells) { yield return ($"e{w.Tile.X},{w.Tile.Y}", w.Kind); }
        if (world.TownHall is not null) { yield return ("townhall", BuildingKind.TownHall); }
        foreach (Household h in world.Households)
        {
            if (h.HomeTile is GridPos home) { yield return ($"h{home.X},{home.Y}", BuildingKind.Home); }
        }

        foreach (Bridge b in world.Bridges) { yield return ($"b{b.Near.ToTile().X},{b.Near.ToTile().Y}", b.Kind); }
    }

    public static IEnumerable<object[]> Villages()
    {
        IEnumerable<ulong> shipped = new ulong[] { 12345, 1, 2, 3, 7, 11 }.Concat(Enumerable.Range(100, 24).Select(x => (ulong)x));
        IEnumerable<ulong> every = Enumerable.Range(1, 12).Select(x => (ulong)x).Append(12345UL);
        if (Environment.GetEnvironmentVariable("ZZ_SEEDS") is string zs)
        {
            ulong[] only = zs.Split(',').Select(ulong.Parse).ToArray();
            shipped = only;
            every = only;
        }

        foreach (ulong s in shipped) { yield return new object[] { "shipped", s }; }
        foreach (ulong s in every) { yield return new object[] { "every", s }; }
    }
}

using Bclone.Sim.Config;
using Bclone.Sim.Core;
using Bclone.Sim.World;
using Xunit;
using Xunit.Abstractions;

namespace Bclone.Sim.Tests;

// ⛔ THE HERD'S MEASUREMENT HARNESS — commit 0 of `specs/trade.md §11` (D588), the numbers `specs/livestock.md §10`
// asks for before anything is typed. Kept in tools/harness/, COPIED into tests/Bclone.Sim.Tests/ to run and DELETED
// from there before any commit. Never committed under tests/.
//
//   ZZK — Cold (once): the shipped exposure numbers as ticks — how long outdoors before a villager breaks off for a
//         fire, how long after that before they freeze, and how many tiles of grass a walk covers in that time. The
//         margin a herdsman feeding a herd in winter has (`livestock.md §7`'s claim, re-taken).
//   ZZS — Seasons (ZzClothing's `played` valleys, 60 years): working-age villager-ticks per season, in three twenty-year
//         windows, split into work / walking / at home or a fire / other — the spare hands a herd borrows in summer and
//         repays in winter (`livestock.md §3`).
//   ZZP — Pasture (the same runs, the year the last founder dies): free ground — grass or forest, reachable, not
//         painted for anything, not residential, not a plot, nothing standing on it — within 10 / 20 / 30 tiles' walk
//         of the founding site; and the fewest tiles' walk from a home to such ground.
public sealed class ZzHerd
{
    private static readonly HashSet<VillagerState> Work = new()
    {
        VillagerState.Gathering, VillagerState.Cutting, VillagerState.MakingFirewood, VillagerState.Building,
        VillagerState.Clearing, VillagerState.TidyingGround, VillagerState.Sowing, VillagerState.Reaping,
        VillagerState.Fishing, VillagerState.Hunting, VillagerState.Forging, VillagerState.Quarrying,
        VillagerState.Mining,
    };

    private readonly ITestOutputHelper _o;

    public ZzHerd(ITestOutputHelper o) => _o = o;

    [Fact]
    public void Cold()
    {
        SimConfig config = ShippedConfig.Load();
        SimLoop loop = SimFactory.CreatePhase0(config with { Seed = 12345 }, new Bclone.Sim.Logging.InMemoryLogSink());
        int perTick = config.ExposurePerTickOutdoors;
        int threshold = config.ExposureThreshold;
        int breakOff = perTick == 0 ? -1 : threshold * config.SeekShelterPercent / 100 / perTick;
        int freeze = perTick == 0 ? -1 : threshold / perTick;
        int tickPerTile = loop.World.TravelCost.TicksForCost(TravelCostField.BaseTileCost);
        _o.WriteLine($"ZZK threshold {threshold} per-tick-outdoors {perTick} break-off-after {breakOff} freeze-after {freeze} "
            + $"ticks-left {freeze - breakOff} ticks-a-grass-tile {tickPerTile} tiles-in-that-time {(tickPerTile == 0 ? -1 : (freeze - breakOff) / tickPerTile)} "
            + $"ticks-a-day {config.TicksPerDay} days-a-season {config.DaysPerSeason} winter-ticks {config.TicksPerSeason} "
            + $"field-tiles-a-farmer-keeps {VillageEconomy.FieldTilesOneFarmerKeeps(config)} crop-a-tile {config.CropYieldPerTile} "
            + $"winter-buffer-percent {config.WinterBufferPercent}");
    }

    [Theory]
    [MemberData(nameof(Valleys))]
    public void Seasons(ulong seed)
    {
        SimConfig config = ShippedConfig.Load() with { Seed = seed };
        int stoneTheyCost = BuildingRecipe.For(BuildingKind.Granary, config).Of(Goods.Stone)
            + BuildingRecipe.For(BuildingKind.Warehouse, config).Of(Goods.Stone);
        int aTile = new GoodsCatalog(config.GoodsCatalog).YieldPerTileOf(Goods.Stone);
        int extraRock = (stoneTheyCost + aTile - 1) / aTile;

        SimLoop loop = SimFactory.CreatePhase0(config, new Bclone.Sim.Logging.InMemoryLogSink());
        SimWorld world = loop.World;
        foreach ((string name, int limit) in config.StartingStockLimits)
        {
            for (int id = 0; id < world.GoodsCatalog.Count; id++)
            {
                if (world.GoodsCatalog[id].Name == name) { world.SetStockLimit((Goods)id, limit); }
            }
        }

        ColdStartTests.PlayTheOpening(world);

        // [window, season, bucket] — buckets: work, walking, home (resting at a home or a fire), other.
        var ticks = new long[3, 4, 4];
        bool sampled = false;
        int years = int.Parse(Environment.GetEnvironmentVariable("ZZ_YEARS") ?? "60");
        for (int t = 0; t < config.TicksPerYear * years; t++)
        {
            if (t == config.TicksPerYear * 3)
            {
                ColdStartTests.MarkSomewhereNear(world, BuildingKind.Granary, world.Map.FoundingSite, 8);
                ColdStartTests.MarkSomewhereNear(world, BuildingKind.Warehouse, world.Map.FoundingSite, 8);
                SeamFixtures.PaintNearest(world, Terrain.Rock, extraRock);
            }

            loop.StepOnce();
            if (!sampled && world.SaidTheFoundersAreGone && world.Population > 0)
            {
                sampled = true;
                _o.WriteLine($"ZZP {seed} year {t / config.TicksPerYear + 1} alive {world.Population} {Pasture(world)}");
            }

            int window = Math.Min(2, t / (config.TicksPerYear * 20));
            int season = (int)world.Clock.Season;
            foreach (Villager v in world.Villagers)
            {
                if (!v.CanWork) { continue; }
                Shelter here = world.ShelterAt(v.Tile);
                int bucket = Work.Contains(v.State) ? 0
                    : v.State is VillagerState.Resting or VillagerState.SeekingShelter || here == Shelter.Fire ? 2
                    : v.State.ToString().StartsWith("Travel", StringComparison.Ordinal) || v.State.ToString().Contains("ing") ? 1
                    : 3;
                ticks[window, season, bucket]++;
            }
        }

        for (int w = 0; w < 3; w++)
        {
            for (int s = 0; s < 4; s++)
            {
                _o.WriteLine($"ZZS {seed} years {(w * 20) + 1}-{(w + 1) * 20} season {(Season)s} "
                    + $"work {ticks[w, s, 0]} walking {ticks[w, s, 1]} home {ticks[w, s, 2]} other {ticks[w, s, 3]}");
            }
        }

        if (!sampled) { _o.WriteLine($"ZZP {seed} year -1 alive {world.Population} no-hall"); }
    }

    /// <summary>Free ground near the founding site, by walk, and the shortest walk from any home to it.</summary>
    private static string Pasture(SimWorld world)
    {
        GeneratedMap map = world.Map;
        GridPos from = map.FoundingSite;
        int tile = world.TravelCost.TicksForCost(TravelCostField.BaseTileCost);
        int in10 = 0, in20 = 0, in30 = 0;
        var free = new List<GridPos>();
        for (int y = map.MinY; y < map.MinY + map.Height; y++)
        {
            for (int x = map.MinX; x < map.MinX + map.Width; x++)
            {
                var at = new GridPos(x, y);
                Terrain t = map.TerrainAt(at);
                if (t is not (Terrain.Grass or Terrain.Forest)) { continue; }
                if (world.Zones.WorkGroundOwner(at) != 0 || world.Zones.IsResidential(at) || world.Zones.PlotOwner(at) != 0) { continue; }
                if (!world.CanBuildAt(BuildingKind.Pile, at).Allowed) { continue; }
                int cost = world.TravelCost.Cost(from, at);
                if (cost == TravelCostField.Unreachable) { continue; }
                int tiles = world.TravelCost.TicksForCost(cost) / Math.Max(1, tile);
                if (tiles <= 10) { in10++; }
                if (tiles <= 20) { in20++; }
                if (tiles <= 30) { in30++; }
                if (tiles <= 30) { free.Add(at); }
            }
        }

        int nearest = int.MaxValue;
        foreach (Household h in world.Households)
        {
            if (h.HomeTile is not GridPos home || world.LivingMembersOf(h) == 0) { continue; }
            foreach (GridPos at in free)
            {
                int c = world.TravelCost.Cost(home, at);
                if (c != TravelCostField.Unreachable) { nearest = Math.Min(nearest, world.TravelCost.TicksForCost(c) / Math.Max(1, tile)); }
            }
        }

        return $"free-in10 {in10} free-in20 {in20} free-in30 {in30} nearest-from-a-home {(nearest == int.MaxValue ? -1 : nearest)}";
    }

    public static IEnumerable<object[]> Valleys()
    {
        IEnumerable<ulong> seeds = new ulong[] { 12345, 1, 2, 3, 7, 11 }.Concat(Enumerable.Range(100, 24).Select(x => (ulong)x));
        if (Environment.GetEnvironmentVariable("ZZ_SEEDS") is string zs) { seeds = zs.Split(',').Select(ulong.Parse).ToArray(); }
        foreach (ulong s in seeds) { yield return new object[] { s }; }
    }
}

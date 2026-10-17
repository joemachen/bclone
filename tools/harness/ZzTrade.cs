using Bclone.Sim.Config;
using Bclone.Sim.Core;
using Bclone.Sim.Determinism;
using Bclone.Sim.World;
using Xunit;
using Xunit.Abstractions;

namespace Bclone.Sim.Tests;

// ⛔ THE TRADE MEASUREMENT HARNESS (specs/trade.md §8) — kept in tools/harness/, COPIED into tests/Bclone.Sim.Tests/
// to run and DELETED from there before any commit. Never committed under tests/.
//
// Measured before any of it exists (Joe, 2026-10-16: the town hall is the permission, the post stands on a navigable
// water edge, a boat comes along the river):
//   ZZR — River (pure map generation, 100 seeds × the four courses): which map edges the river touches and with how many
//         water tiles, how many land tiles on the founding's side touch water (a post's possible sites), and the walk
//         from the founding site to the nearest of them (4-way BFS over land, −1 if none).
//   ZZH — Village (the played opening, a granary and a warehouse marked at Year 3, the shipped config — nomads on):
//         the year the last founder died (the town hall owed, D252), the people then, and every good held in stores
//         against its limit at that year and 10 and 20 years on — what a valley has spare when trade opens.
//   ZZB — the usual survival line so dead.py reads it.
// Seeds: River 1–100 per course; Village 200–299 (ZZ_SEEDS narrows); ZZ_YEARS caps the run (default 100).
public sealed class ZzTrade
{
    private readonly ITestOutputHelper _o;

    public ZzTrade(ITestOutputHelper o) => _o = o;

    [Theory]
    [MemberData(nameof(Courses))]
    public void River(string course, ulong seed)
    {
        SimConfig config = ShippedConfig.Load() with { Seed = seed, RiverCourse = course };
        GeneratedMap map = MapGenerator.Generate(config, seed);
        int w = map.Width, h = map.Height;
        Terrain At(int x, int y) => map.TerrainAt(new GridPos(x + map.MinX, y + map.MinY));

        int north = 0, south = 0, west = 0, east = 0;
        for (int x = 0; x < w; x++)
        {
            if (At(x, 0) == Terrain.Water) { north++; }
            if (At(x, h - 1) == Terrain.Water) { south++; }
        }

        for (int y = 0; y < h; y++)
        {
            if (At(0, y) == Terrain.Water) { west++; }
            if (At(w - 1, y) == Terrain.Water) { east++; }
        }

        // The founding's land, by walk distance: 4-way BFS over anything but water.
        var dist = new int[w * h];
        Array.Fill(dist, -1);
        var queue = new Queue<int>();
        int start = (map.FoundingSite.X - map.MinX) + ((map.FoundingSite.Y - map.MinY) * w);
        dist[start] = 0;
        queue.Enqueue(start);
        var steps = new[] { (1, 0), (-1, 0), (0, 1), (0, -1) };
        while (queue.Count > 0)
        {
            int i = queue.Dequeue();
            int x = i % w, y = i / w;
            foreach ((int dx, int dy) in steps)
            {
                int nx = x + dx, ny = y + dy;
                if (nx < 0 || ny < 0 || nx >= w || ny >= h) { continue; }
                int j = nx + (ny * w);
                if (dist[j] >= 0 || At(nx, ny) == Terrain.Water) { continue; }
                dist[j] = dist[i] + 1;
                queue.Enqueue(j);
            }
        }

        // A bank tile: on the founding's land, grass, and touching water orthogonally (the fishery's `must_touch`).
        int banks = 0, nearest = -1;
        for (int y = 0; y < h; y++)
        {
            for (int x = 0; x < w; x++)
            {
                int i = x + (y * w);
                if (dist[i] < 0 || At(x, y) != Terrain.Grass) { continue; }
                bool touches = false;
                foreach ((int dx, int dy) in steps)
                {
                    int nx = x + dx, ny = y + dy;
                    if (nx >= 0 && ny >= 0 && nx < w && ny < h && At(nx, ny) == Terrain.Water) { touches = true; }
                }

                if (!touches) { continue; }
                banks++;
                if (nearest < 0 || dist[i] < nearest) { nearest = dist[i]; }
            }
        }

        int edges = (north > 0 ? 1 : 0) + (south > 0 ? 1 : 0) + (west > 0 ? 1 : 0) + (east > 0 ? 1 : 0);
        _o.WriteLine($"ZZR {course} {seed} map {w}x{h} edges {edges} n {north} s {south} w {west} e {east} banks {banks} nearest {nearest}");
    }

    [Theory]
    [MemberData(nameof(Villages))]
    public void Village(string arm, ulong seed)
    {
        SimConfig config = ShippedConfig.Load() with { Seed = seed };
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

        int years = int.Parse(Environment.GetEnvironmentVariable("ZZ_YEARS") ?? "100");
        int peak = 0;
        int hallYear = -1;
        int deadYear = -1;
        for (int year = 1; year <= years; year++)
        {
            if (year == 3)
            {
                ColdStartTests.MarkSomewhereNear(world, BuildingKind.Granary, world.Map.FoundingSite, 8);
                ColdStartTests.MarkSomewhereNear(world, BuildingKind.Warehouse, world.Map.FoundingSite, 8);
            }

            for (int season = 0; season < 4; season++)
            {
                loop.Step(config.TicksPerSeason);
                peak = Math.Max(peak, world.Population);
                if (hallYear < 0 && world.SaidTheFoundersAreGone) { hallYear = year; }
                if (deadYear < 0 && world.Population == 0) { deadYear = year; }
            }

            if (hallYear > 0 && (year - hallYear) is 0 or 10 or 20)
            {
                _o.WriteLine($"ZZH {arm} {seed} hall-year {hallYear} on {year - hallYear} alive {world.Population} {Held(world)}");
            }

            if (hallYear > 0 && year - hallYear >= 20) { break; }
        }

        int starved = world.Villagers.Count(v => !v.Alive && v.CauseOfDeath == CauseOfDeath.Starvation);
        int cold = world.Villagers.Count(v => !v.Alive && v.CauseOfDeath == CauseOfDeath.Cold);
        _o.WriteLine($"ZZB {arm} {seed} alive {world.Population} peak {peak} starved {starved} cold {cold} "
            + $"hall-year {hallYear} dead-year {deadYear} hash {StateHash.Compute(world)}");
    }

    /// <summary>Every good in the catalogue: held in stores / its limit (− for none).</summary>
    private static string Held(SimWorld world)
    {
        var parts = new List<string>();
        for (int id = 0; id < world.GoodsCatalog.Count; id++)
        {
            var good = (Goods)id;
            int? limit = world.StockLimits.For(good);
            parts.Add($"{world.GoodsCatalog[id].Name.Replace(' ', '_')}={world.InStores(good)}/{(limit is int l ? l.ToString() : "-")}");
        }

        return string.Join(" ", parts);
    }

    public static IEnumerable<object[]> Courses()
    {
        foreach (string c in new[] { "we", "ns", "nwse", "swne" })
        {
            foreach (ulong s in Enumerable.Range(1, 100).Select(x => (ulong)x)) { yield return new object[] { c, s }; }
        }
    }

    public static IEnumerable<object[]> Villages()
    {
        IEnumerable<ulong> seeds = Enumerable.Range(200, 100).Select(x => (ulong)x);
        if (Environment.GetEnvironmentVariable("ZZ_SEEDS") is string zs) { seeds = zs.Split(',').Select(ulong.Parse).ToArray(); }
        foreach (ulong s in seeds) { yield return new object[] { "played", s }; }
    }
}

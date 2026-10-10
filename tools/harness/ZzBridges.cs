using Bclone.Sim.Config;
using Bclone.Sim.Core;
using Bclone.Sim.World;
using Xunit;
using Xunit.Abstractions;

namespace Bclone.Sim.Tests;

// ⛔ D547's MEASUREMENT HARNESS for bridges (specs/bridges.md §8) — kept in tools/harness/, COPIED into
// tests/Bclone.Sim.Tests/ to run and DELETED from there before any commit. Never committed under tests/.
//
//   ZZV  — one line per valley: what lies across the river from the founding (land, woods, rock, iron) and
//          how wide the river is where a one-wide, quarter-turned deck could cross it.
//   ZZG  — one line per village: the first year it held 10 / 12 / 15 / 20 / 25 people (−1 never).
public sealed class ZzBridges
{
    private readonly ITestOutputHelper _o;

    public ZzBridges(ITestOutputHelper o) => _o = o;

    [Theory]
    [MemberData(nameof(Valleys))]
    public void Valley(string course, ulong seed)
    {
        SimConfig config = ShippedConfig.Load() with { Seed = seed, RiverCourse = course };
        GeneratedMap map = MapGenerator.Generate(config, seed);
        int w = map.Width, h = map.Height;
        Terrain At(int x, int y) => map.TerrainAt(new GridPos(x + map.MinX, y + map.MinY));

        // The founding's land: 4-connected over anything but water.
        var near = new bool[w * h];
        var queue = new Queue<int>();
        int start = (map.FoundingSite.X - map.MinX) + ((map.FoundingSite.Y - map.MinY) * w);
        near[start] = true;
        queue.Enqueue(start);
        while (queue.Count > 0)
        {
            int i = queue.Dequeue();
            int x = i % w, y = i / w;
            foreach ((int dx, int dy) in new[] { (1, 0), (-1, 0), (0, 1), (0, -1) })
            {
                int nx = x + dx, ny = y + dy;
                if (nx < 0 || ny < 0 || nx >= w || ny >= h) { continue; }
                int j = nx + (ny * w);
                if (near[j] || At(nx, ny) == Terrain.Water) { continue; }
                near[j] = true;
                queue.Enqueue(j);
            }
        }

        int landN = 0, landF = 0, woodN = 0, woodF = 0, rockN = 0, rockF = 0, ironN = 0, ironF = 0, water = 0;
        for (int y = 0; y < h; y++)
        {
            for (int x = 0; x < w; x++)
            {
                Terrain t = At(x, y);
                if (t == Terrain.Water) { water++; continue; }
                bool n = near[x + (y * w)];
                if (n) { landN++; } else { landF++; }
                if (t == Terrain.Forest) { if (n) { woodN++; } else { woodF++; } }
                if (t == Terrain.Rock) { if (n) { rockN++; } else { rockF++; } }
                if (t == Terrain.IronDeposit) { if (n) { ironN++; } else { ironF++; } }
            }
        }

        // Every straight cardinal run of water with land at both ends, one end on the founding's land and the
        // other not: the span a deck would have to cross there. Counted per run, both axes.
        var spans = new List<int>();
        void Runs(bool horizontal)
        {
            int outer = horizontal ? h : w, inner = horizontal ? w : h;
            for (int o = 0; o < outer; o++)
            {
                int i = 0;
                while (i < inner)
                {
                    (int x, int y) P(int k) => horizontal ? (k, o) : (o, k);
                    (int x0, int y0) = P(i);
                    if (At(x0, y0) != Terrain.Water) { i++; continue; }
                    int s = i;
                    while (i < inner && At(P(i).x, P(i).y) == Terrain.Water) { i++; }
                    if (s == 0 || i >= inner) { continue; }
                    (int ax, int ay) = P(s - 1);
                    (int bx, int by) = P(i);
                    if (near[ax + (ay * w)] != near[bx + (by * w)]) { spans.Add(i - s); }
                }
            }
        }

        Runs(horizontal: true);
        Runs(horizontal: false);
        spans.Sort();
        string Le(int k) => spans.Count(s => s <= k).ToString(System.Globalization.CultureInfo.InvariantCulture);
        _o.WriteLine(
            $"ZZV {course} {seed} land {landN} {landF} wood {woodN} {woodF} rock {rockN} {rockF} iron {ironN} {ironF} "
            + $"water {water} spans {spans.Count} min {(spans.Count > 0 ? spans[0] : -1)} "
            + $"le3 {Le(3)} le4 {Le(4)} le5 {Le(5)} le6 {Le(6)} le8 {Le(8)} max {(spans.Count > 0 ? spans[^1] : -1)}");
    }

    [Theory]
    [MemberData(nameof(Villages))]
    public void Growth(string arm, ulong seed)
    {
        SimConfig config = ShippedConfig.Load() with { Seed = seed };
        SimLoop loop = SimFactory.CreatePhase0(config, new Bclone.Sim.Logging.InMemoryLogSink());
        SimWorld world = loop.World;
        var limits = config.StartingStockLimits;
        for (int id = 0; id < world.GoodsCatalog.Count; id++)
        {
            if (limits.TryGetValue(world.GoodsCatalog[id].Name, out int limit)) { world.SetStockLimit((Goods)id, limit); }
        }

        ColdStartTests.PlayTheOpening(world);
        if (arm == "every")
        {
            HuntingTests.RaiseALodgeFor(world);
            FishingTests.RaiseAFishery(world);
            Workplace farm = FarmFixtures.RaiseAFarm(world);
            FarmFixtures.GiveItGround(world, farm, 3);
        }

        int[] marks = { 10, 12, 15, 20, 25 };
        int[] first = marks.Select(_ => -1).ToArray();
        int peak = 0;
        for (int t = 0; t < config.TicksPerYear * 50; t++)
        {
            if (t == config.TicksPerYear * 3)
            {
                ColdStartTests.MarkSomewhereNear(world, BuildingKind.Granary, world.Map.FoundingSite, 8);
                ColdStartTests.MarkSomewhereNear(world, BuildingKind.Warehouse, world.Map.FoundingSite, 8);
            }

            loop.StepOnce();
            peak = Math.Max(peak, world.Population);
            for (int m = 0; m < marks.Length; m++)
            {
                if (first[m] < 0 && world.Population >= marks[m]) { first[m] = t / config.TicksPerYear; }
            }
        }

        _o.WriteLine($"ZZG {arm} {seed} peak {peak} alive {world.Population} at10 {first[0]} at12 {first[1]} at15 {first[2]} at20 {first[3]} at25 {first[4]}");
    }

    /// <summary>
    /// ZZX — crossings a year through one bridge (`bridges.md §8.3`): a played opening, a wooden bridge raised
    /// free at Year 5 at the crossing nearest the founding, and an 8×8 patch of the far bank painted for homes,
    /// so households settle across and walk back for food and work. Years 5–35.
    /// </summary>
    [Theory]
    [MemberData(nameof(Villages))]
    public void Crossings(string arm, ulong seed)
    {
        SimConfig config = ShippedConfig.Load() with { Seed = seed, BridgeUnlockPopulation = 0 };
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

        var perYear = new List<int>();
        int last = 0;
        int across = 0;
        string bridged = "none";
        for (int year = 0; year < 35; year++)
        {
            if (year == 5)
            {
                try
                {
                    Bridge bridge = BridgeTests.RaiseABridge(world, BridgeTests.ACrossing(world));
                    bridged = bridge.Length.ToString(System.Globalization.CultureInfo.InvariantCulture);
                    GridPos nearEnd = bridge.Near.ToTile(), farEnd = bridge.Far.ToTile();
                    int dy = Math.Sign(farEnd.Y - nearEnd.Y);

                    // A forager's hut on the far bank — the reason a real player crosses: daily work over the water.
                    GridPos? hut = null;
                    for (int r = 1; r < 12 && hut is null; r++)
                    {
                        for (int x = -r; x <= r && hut is null; x++)
                        {
                            var at = new GridPos(farEnd.X + x, farEnd.Y + (dy * r));
                            if (world.CanBuildAt(BuildingKind.GathererHut, at).Allowed && world.Mark(BuildingKind.GathererHut, at).Allowed)
                            {
                                hut = at;
                            }
                        }
                    }

                    bridged += hut is null ? "/no-hut" : "/hut";
                    for (int y = 1; y <= 8; y++)
                    {
                        for (int x = -4; x < 4; x++)
                        {
                            world.PaintResidential(new GridPos(farEnd.X + x, farEnd.Y + (dy * y)));
                        }
                    }
                }
                catch (Xunit.Sdk.XunitException) { bridged = "no-crossing"; }
            }

            loop.Step(config.TicksPerYear);
            if (year >= 5)
            {
                perYear.Add(world.BridgeCrossings - last);
                last = world.BridgeCrossings;
            }
        }

        foreach (Household h in world.Households)
        {
            if (h.HomeTile is GridPos home && world.Bridges.Count > 0
                && Math.Sign(home.Y - world.Bridges[0].Near.ToTile().Y) == Math.Sign(world.Bridges[0].Far.ToTile().Y - world.Bridges[0].Near.ToTile().Y)
                && Math.Abs(home.Y - world.Bridges[0].Near.ToTile().Y) > Math.Abs(world.Bridges[0].Far.ToTile().Y - world.Bridges[0].Near.ToTile().Y))
            {
                across++;
            }
        }

        _o.WriteLine($"ZZX {arm} {seed} span {bridged} alive {world.Population} homes-across {across} total {world.BridgeCrossings} "
            + $"per-year {string.Join(",", perYear)}");
    }

    public static IEnumerable<object[]> Valleys()
    {
        foreach (string c in new[] { "we", "ns", "nwse", "swne" })
        {
            foreach (ulong s in Enumerable.Range(1, 100).Select(x => (ulong)x)) { yield return new object[] { c, s }; }
        }
    }

    public static IEnumerable<object[]> Villages()
    {
        foreach (ulong s in new ulong[] { 12345, 1, 2, 3, 7, 11 }.Concat(Enumerable.Range(100, 24).Select(x => (ulong)x)))
        {
            yield return new object[] { "shipped", s };
        }

        foreach (ulong s in Enumerable.Range(1, 12).Select(x => (ulong)x).Append(12345UL)) { yield return new object[] { "every", s }; }
    }
}

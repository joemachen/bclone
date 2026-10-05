using Bclone.Sim.Config;
using Bclone.Sim.Core;
using Bclone.Sim.World;
using Xunit;
using Xunit.Abstractions;

namespace Bclone.Sim.Tests;

// ⛔ B5 MEASUREMENT (D496) — kept in tools/harness/, COPIED into tests/Bclone.Sim.Tests/ to run and DELETED from
// there before any commit. Never committed under tests/. ZZ_YEARS sets the run length (10 by default; D496 read 30).
public sealed class ZzB5
{
    private readonly ITestOutputHelper _o;

    public ZzB5(ITestOutputHelper o) => _o = o;

    private sealed class House
    {
        public int Hh;
        public long Marked = -1;
        public long Raised = -1;
        public List<GridPos> Tiles = new();
        public string AtMark = "";
        public string AtRaise = "";
        public GridPos Front;
    }

    public static IEnumerable<object[]> Runs()
    {
        foreach (string arm in new[] { "played", "wood", "seam" })
        {
            foreach (ulong seed in new ulong[] { 12345, 11, 23, 99, 5, 13, 17, 31 })
            {
                yield return new object[] { arm, seed };
            }
        }
    }

    [Theory]
    [MemberData(nameof(Runs))]
    public void Measure(string arm, ulong seed)
    {
        SimConfig config = ShippedConfig.Load() with { Seed = seed };
        SimLoop loop = SimFactory.CreatePhase0(config, new Bclone.Sim.Logging.InMemoryLogSink());
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
        GridPos site = world.Map.FoundingSite;
        string posed = "";
        if (arm != "played")
        {
            for (int dy = -4; dy <= 4; dy++)
            {
                for (int dx = -4; dx <= 4; dx++)
                {
                    world.EraseResidential(new GridPos(site.X + dx, site.Y + dy));
                }
            }

            Func<Terrain, bool> wanted = arm == "wood"
                ? t => t == Terrain.Forest
                : t => t is Terrain.Rock or Terrain.IronDeposit;
            int half = arm == "wood" ? 4 : 3;
            GridPos best = site;
            int bestCount = -1;
            for (int y = site.Y - 18; y <= site.Y + 18; y++)
            {
                for (int x = site.X - 18; x <= site.X + 18; x++)
                {
                    var c = new GridPos(x, y);
                    if (!world.Map.Contains(c) || world.TravelCost.Cost(site, c) == TerrainCostField.Unreachable)
                    {
                        continue;
                    }

                    int n = 0;
                    for (int dy = -half; dy <= half; dy++)
                    {
                        for (int dx = -half; dx <= half; dx++)
                        {
                            var t = new GridPos(x + dx, y + dy);
                            if (world.Map.Contains(t) && wanted(world.Map.TerrainAt(t)))
                            {
                                n++;
                            }
                        }
                    }

                    if (n > bestCount)
                    {
                        bestCount = n;
                        best = c;
                    }
                }
            }

            int painted = 0;
            for (int dy = -half; dy <= half; dy++)
            {
                for (int dx = -half; dx <= half; dx++)
                {
                    if (world.PaintResidential(new GridPos(best.X + dx, best.Y + dy)).Allowed)
                    {
                        painted++;
                    }
                }
            }

            posed = $"posed at {best} ({best.ManhattanDistanceTo(site)} from founding) {bestCount} target tiles of {(2 * half + 1) * (2 * half + 1)}, painted {painted}";
        }

        // Every tile that was wood at the start, and whether it has ever been cleared.
        var wood = new List<GridPos>();
        for (int y = world.Map.MinY; y < world.Map.MinY + world.Map.Height; y++)
        {
            for (int x = world.Map.MinX; x < world.Map.MinX + world.Map.Width; x++)
            {
                var t = new GridPos(x, y);
                if (world.Map.TerrainAt(t) == Terrain.Forest)
                {
                    wood.Add(t);
                }
            }
        }

        var cleared = new bool[wood.Count];
        var houses = new Dictionary<int, House>();
        int years = int.Parse(Environment.GetEnvironmentVariable("ZZ_YEARS") ?? "10");
        for (long tick = 0; tick < config.TicksPerYear * years; tick++)
        {
            loop.StepOnce();

            for (int i = 0; i < world.Workplaces.Count; i++)
            {
                Workplace w = world.Workplaces[i];
                if (w.Construction is { IsFinished: false, Kind: BuildingKind.Home } c
                    && !houses.ContainsKey(c.ForHouseholdId))
                {
                    var h = new House { Hh = c.ForHouseholdId, Marked = tick, Front = w.Tile };
                    h.Tiles = world.FootprintOf(BuildingKind.Home, w.Position, w.Facing).CoveredTiles();
                    h.AtMark = Describe(world, h.Tiles);
                    houses[c.ForHouseholdId] = h;
                }
            }

            foreach (Household hh in world.Households)
            {
                if (hh.HomePosition is not null && houses.TryGetValue(hh.Id, out House? h) && h.Raised < 0)
                {
                    h.Raised = tick;
                    h.Tiles = world.HomeFootprintOf(hh)!.Value.CoveredTiles();
                    h.AtRaise = Describe(world, h.Tiles);
                }
            }

            for (int i = 0; i < wood.Count; i++)
            {
                if (!cleared[i] && world.Map.TerrainAt(wood[i]) != Terrain.Forest)
                {
                    cleared[i] = true;
                }
            }
        }

        _o.WriteLine($"ZZB5 {arm} {seed} pop {world.Population} houses {houses.Count} {posed}");
        int raisedOnGrowth = 0;
        int stuck = 0;
        foreach (House h in houses.Values.OrderBy(h => h.Marked))
        {
            string end = Describe(world, h.Tiles);
            bool hit = h.AtRaise.Contains("Forest") || h.AtRaise.Contains("Rock") || h.AtRaise.Contains("Iron");
            raisedOnGrowth += hit ? 1 : 0;
            stuck += h.Raised < 0 ? 1 : 0;
            if (h.AtMark != Grassy(h.Tiles.Count) || hit || h.Raised < 0)
            {
                GridPos front = h.Tiles[0];
                _o.WriteLine($"ZZB5H {arm} {seed} hh{h.Hh} front#{h.Tiles.IndexOf(h.Front)} marked t{h.Marked} raised t{h.Raised} "
                    + $"mark[{h.AtMark}] raise[{h.AtRaise}] end[{end}] harvestPaint[{string.Join(",", h.Tiles.Select(t => world.Zones.IsHarvest(t) ? "Y" : "n"))}]");
            }
        }

        // Regrowth census of wood that was ever cleared.
        var census = new Dictionary<string, int>();
        for (int i = 0; i < wood.Count; i++)
        {
            if (!cleared[i])
            {
                continue;
            }

            GridPos t = wood[i];
            string where = world.SomethingStandsAt(t) ? "underBuilding"
                : world.Zones.IsResidential(t) ? "residential"
                : world.IsFarmGround(t) ? "farm"
                : world.Zones.IsHarvest(t) ? "harvestPainted"
                : "other";
            string key = $"{where}:{world.Map.TerrainAt(t)}";
            census[key] = census.TryGetValue(key, out int n) ? n + 1 : 1;
        }

        // Wood still standing between houses: residential, not built on.
        int standingInResidential = 0;
        for (int i = 0; i < wood.Count; i++)
        {
            if (world.Map.TerrainAt(wood[i]) == Terrain.Forest && world.Zones.IsResidential(wood[i]) && !world.SomethingStandsAt(wood[i]))
            {
                standingInResidential++;
            }
        }

        _o.WriteLine($"ZZB5S {arm} {seed} raisedOverWoodOrSeam {raisedOnGrowth} unraised {stuck} woodStillStandingInResidential {standingInResidential} "
            + $"cleared-wood-census {string.Join(" ", census.OrderBy(k => k.Key).Select(k => $"{k.Key}={k.Value}"))}");
    }


    [Theory]
    [InlineData(12345UL)]
    [InlineData(11UL)]
    [InlineData(31UL)]
    public void Placed(ulong seed)
    {
        SimConfig config = ShippedConfig.Load() with { Seed = seed };
        SimLoop loop = SimFactory.CreatePhase0(config, new Bclone.Sim.Logging.InMemoryLogSink());
        SimWorld world = loop.World;
        ColdStartTests.PlayTheOpening(world);
        SeamFixtures.PaintNearest(world, Terrain.Rock, 12);
        GridPos site = world.Map.FoundingSite;
        var marked = new List<(BuildingKind Kind, Point At)>();
        foreach (BuildingKind kind in new[] { BuildingKind.WoodcutterHut, BuildingKind.Granary })
        {
            for (int r = 3; r <= 16 && !marked.Any(m => m.Kind == kind); r++)
            {
                for (int dy = -r; dy <= r && !marked.Any(m => m.Kind == kind); dy++)
                {
                    for (int dx = -r; dx <= r; dx++)
                    {
                        var at = new GridPos(site.X + dx, site.Y + dy);
                        Point anchor = world.AnchorOn(kind, at);
                        List<GridPos> tiles = world.FootprintOf(kind, anchor).CoveredTiles();
                        if (tiles.All(t => world.Map.Contains(t) && world.Map.TerrainAt(t) == Terrain.Forest)
                            && world.CanBuildAt(kind, anchor).Allowed && world.Mark(kind, anchor, Angle.Zero).Allowed)
                        {
                            marked.Add((kind, anchor));
                            break;
                        }
                    }
                }
            }
        }

        for (int t = 0; t < config.TicksPerYear * 6; t++)
        {
            loop.StepOnce();
        }

        foreach ((BuildingKind kind, Point at) in marked)
        {
            List<GridPos> tiles = world.FootprintOf(kind, at).CoveredTiles();
            bool standing = !world.Workplaces.Any(w => w.Construction is { IsFinished: false } c && c.Kind == kind && w.Position == at);
            _o.WriteLine($"ZZB5P {seed} {kind} anchorTile#{tiles.IndexOf(at.ToTile())} standing={standing} now[{Describe(world, tiles)}]");
        }
    }

    private static string Grassy(int n) => string.Join(" ", Enumerable.Repeat("Grass", n));

    private static string Describe(SimWorld world, List<GridPos> tiles) =>
        string.Join(" ", tiles.Select(t => world.Map.TerrainAt(t).ToString()));
}

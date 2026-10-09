using Bclone.Sim.Config;
using Bclone.Sim.Core;
using Bclone.Sim.World;
using Xunit;
using Xunit.Abstractions;

namespace Bclone.Sim.Tests;

// ⛔ D533's MEASUREMENT — `specs/clothing.md §5` re-taken on today's code. Kept in tools/harness/,
// COPIED into tests/Bclone.Sim.Tests/ to run and DELETED from there before any commit.
//
// Three cold worlds, the same valley each time:
//   bare — the shipped rates (outdoors 15 days, a roof 25);
//   coat — outdoors chills only as fast as a roof does (25 / 25): a garment that is a roof you wear;
//   off  — exposure_days_outdoors 0, cold switched off entirely (§5's "everyone clothed", an upper bound).
// Two openings: `played` (ZzBase's: the cold-start opening, a granary and a warehouse at year 3, rock
// painted) and `every` (the same plus a lodge, a fishery and a farm raised free at t0).
// One ZZC line a run: winter villager-ticks of working-age villagers, split into work outdoors / work
// under a roof / walking / seeking shelter / other, cold break-offs, and outdoor/roof winter work by trade.
public sealed class ZzClothing
{
    private static readonly HashSet<VillagerState> Work = new()
    {
        VillagerState.Gathering, VillagerState.Cutting, VillagerState.MakingFirewood, VillagerState.Building,
        VillagerState.Clearing, VillagerState.TidyingGround, VillagerState.Sowing, VillagerState.Reaping,
        VillagerState.Fishing, VillagerState.Hunting, VillagerState.Forging, VillagerState.Quarrying,
        VillagerState.Mining,
    };

    private static readonly HashSet<VillagerState> Walk = new()
    {
        VillagerState.TravelingToFood, VillagerState.TravelingHome, VillagerState.TravelingToTrees,
        VillagerState.TravelingToHut, VillagerState.HaulingToStore, VillagerState.FetchingFromStore,
        VillagerState.CollectingForMarket, VillagerState.StockingTheMarket, VillagerState.FetchingMaterials,
        VillagerState.TravelingToField, VillagerState.TravelingToWater, VillagerState.HaulingToFarm,
        VillagerState.TravelingToGame, VillagerState.ClearingAStore, VillagerState.ClearingABuffer,
        VillagerState.FetchingATool, VillagerState.TravelingToSmithy, VillagerState.WalkingToTheWell,
        VillagerState.TravelingToQuarry, VillagerState.TravelingToMine,
    };

    private readonly ITestOutputHelper _o;

    public ZzClothing(ITestOutputHelper o) => _o = o;

    [Theory]
    [MemberData(nameof(Runs))]
    public void Measure(string arm, string cold, ulong seed)
    {
        SimConfig config = ShippedConfig.Load() with { Seed = seed };
        config = cold switch
        {
            "coat" => config with { ExposureDaysOutdoors = config.ExposureDaysSheltered },
            "off" => config with { ExposureDaysOutdoors = 0 },
            _ => config,
        };

        int stoneTheyCost = BuildingRecipe.For(BuildingKind.Granary, config).Of(Goods.Stone)
            + BuildingRecipe.For(BuildingKind.Warehouse, config).Of(Goods.Stone);
        int aTile = new GoodsCatalog(config.GoodsCatalog).YieldPerTileOf(Goods.Stone);
        int extraRock = (stoneTheyCost + aTile - 1) / aTile;

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
        if (arm == "every")
        {
            HuntingTests.RaiseALodgeFor(world);
            FishingTests.RaiseAFishery(world);
            Workplace farm = FarmFixtures.RaiseAFarm(world);
            FarmFixtures.GiveItGround(world, farm, 3);
        }

        long winter = 0, workOut = 0, workRoof = 0, walking = 0, seeking = 0, atFire = 0, other = 0;
        long walkingOut = 0, breakOffs = 0;
        var byTradeOut = new SortedDictionary<string, long>();
        var byTradeRoof = new SortedDictionary<string, long>();
        var wasSeeking = new Dictionary<int, bool>();
        var hunterStates = new SortedDictionary<string, long>();
        long hunterColdSum = 0, hunterTicks = 0;
        int peak = 0;

        for (int t = 0; t < config.TicksPerYear * 50; t++)
        {
            if (t == config.TicksPerYear * 3)
            {
                ColdStartTests.MarkSomewhereNear(world, BuildingKind.Granary, world.Map.FoundingSite, 8);
                ColdStartTests.MarkSomewhereNear(world, BuildingKind.Warehouse, world.Map.FoundingSite, 8);
                SeamFixtures.PaintNearest(world, Terrain.Rock, extraRock);
            }

            if (t == config.TicksPerYear * 5 && Environment.GetEnvironmentVariable("ZZ_LIMITX") is string lx)
            {
                for (int id = 0; id < world.GoodsCatalog.Count; id++)
                {
                    if (limits.TryGetValue(world.GoodsCatalog[id].Name, out int limit) && !world.GoodsCatalog[id].Name.Contains("tools"))
                    {
                        world.SetStockLimit((Goods)id, limit * int.Parse(lx));
                    }
                }
            }

            var aliveBefore = world.Villagers.Where(v => v.Alive).Select(v => (v, world.ShelterAt(v.Tile), v.State)).ToList();
            loop.StepOnce();
            foreach (var (v, sh, st) in aliveBefore)
            {
                if (!v.Alive && v.CauseOfDeath == CauseOfDeath.Cold)
                {
                    int houses = world.Households.Count(h => h.HomeTile is not null);
                    _o.WriteLine($"ZZD {arm} {cold} {seed} year {t / config.TicksPerYear + 1} shelter {sh} state {st} {(v.HasJob ? world.FindWorkplace(v.WorkplaceId)?.Kind.ToString() : "laborer")} housesWithHome {houses} hh {world.Households.Count}");
                }
            }

            peak = Math.Max(peak, world.Population);
            if (!world.Clock.IsWinter)
            {
                wasSeeking.Clear();
                continue;
            }

            foreach (Villager v in world.Villagers)
            {
                if (!v.CanWork)
                {
                    continue;
                }

                winter++;
                Shelter here = world.ShelterAt(v.Tile);
                bool seekingNow = v.State == VillagerState.SeekingShelter;
                if (seekingNow && !(wasSeeking.TryGetValue(v.Id, out bool was) && was))
                {
                    breakOffs++;
                }

                wasSeeking[v.Id] = seekingNow;
                if (v.HasJob && world.FindWorkplace(v.WorkplaceId) is { Kind: JobKind.Hunter })
                {
                    string key = $"{v.State}@{here}";
                    hunterStates[key] = hunterStates.GetValueOrDefault(key) + 1;
                    hunterColdSum += v.Cold;
                    hunterTicks++;
                }

                if (Work.Contains(v.State))
                {
                    string trade = v.HasJob && world.FindWorkplace(v.WorkplaceId) is Workplace w
                        ? w.Kind.ToString()
                        : "Laborer";
                    if (here == Shelter.Outdoors)
                    {
                        workOut++;
                        byTradeOut[trade] = byTradeOut.GetValueOrDefault(trade) + 1;
                    }
                    else
                    {
                        workRoof++;
                        byTradeRoof[trade] = byTradeRoof.GetValueOrDefault(trade) + 1;
                    }
                }
                else if (seekingNow)
                {
                    seeking++;
                }
                else if (Walk.Contains(v.State))
                {
                    walking++;
                    if (here == Shelter.Outdoors) { walkingOut++; }
                }
                else if (here == Shelter.Fire)
                {
                    atFire++;
                }
                else
                {
                    other++;
                }
            }
        }

        int starved = world.Villagers.Count(v => !v.Alive && v.CauseOfDeath == CauseOfDeath.Starvation);
        int frozen = world.Villagers.Count(v => !v.Alive && v.CauseOfDeath == CauseOfDeath.Cold);
        string trades = string.Join(" ", byTradeOut.Keys.Union(byTradeRoof.Keys).OrderBy(k => k)
            .Select(k => $"{k}={byTradeOut.GetValueOrDefault(k)}/{byTradeRoof.GetValueOrDefault(k)}"));
        _o.WriteLine($"ZZC {arm} {cold} {seed} alive {world.Population} peak {peak} starved {starved} cold {frozen} " +
            $"winter {winter} workOut {workOut} workRoof {workRoof} walking {walking} walkingOut {walkingOut} " +
            $"seeking {seeking} atFire {atFire} other {other} breakOffs {breakOffs} | {trades}");
        if (Environment.GetEnvironmentVariable("ZZ_HUNT") == "1")
        {
            _o.WriteLine($"ZZH {arm} {cold} {seed} ticks {hunterTicks} meanCold {(hunterTicks == 0 ? 0 : hunterColdSum / hunterTicks)} threshold {config.ExposureThreshold} | " +
                string.Join(" ", hunterStates.OrderByDescending(k => k.Value).Select(k => $"{k.Key}={k.Value}")));
        }
    }

    public static IEnumerable<object[]> Runs()
    {
        string[] colds = (Environment.GetEnvironmentVariable("ZZ_COLDS") ?? "bare,coat,off").Split(',');
        string[] arms = (Environment.GetEnvironmentVariable("ZZ_ARMS") ?? "played,every").Split(',');
        ulong[] played = new ulong[] { 12345, 1, 2, 3, 7, 11 }
            .Concat(Enumerable.Range(100, 24).Select(x => (ulong)x)).ToArray();
        ulong[] every = Enumerable.Range(1, 12).Select(x => (ulong)x).Append(12345UL).ToArray();
        foreach (string arm in arms)
        {
            foreach (string cold in colds)
            {
                foreach (ulong s in arm == "every" ? every : played)
                {
                    yield return new object[] { arm, cold, s };
                }
            }
        }
    }
}

using Bclone.Sim.Config;
using Bclone.Sim.Core;
using Bclone.Sim.Determinism;
using Bclone.Sim.Logging;
using Bclone.Sim.World;
using Xunit;
using Xunit.Abstractions;

namespace Bclone.Sim.Tests;

// D643's measurement for `specs/phase-5-golden.md §5`: the posed Phase 5 village on the shipped data,
// stepped for ZZ_YEARS (default 50), one ZZP line a year — people, and what each chain made — so the
// golden's years are measured before they are typed. Copy in, `-c Release --filter ZzPose`, delete.
// ⚠️ StoneEverDug counts hand digging only (the quarry's unlock), so the quarry and the mine are read at
// their own stores: the sum of every rise is what they dug.
public sealed class ZzPose
{
    private readonly ITestOutputHelper _output;

    public ZzPose(ITestOutputHelper output) => _output = output;

    [Fact]
    public void ZzPosed()
    {
        int years = int.TryParse(Environment.GetEnvironmentVariable("ZZ_YEARS"), out int y) ? y : 50;
        SimConfig config = ShippedConfig.Established();
        // ZZ_SEED=n: another valley.
        if (ulong.TryParse(Environment.GetEnvironmentVariable("ZZ_SEED"), out ulong seed))
        {
            config = config with { Seed = seed };
        }

        // ZZ_AHEAD=n: the D643 arm — hay_feed_ahead_days at n instead of the shipped 5.
        if (int.TryParse(Environment.GetEnvironmentVariable("ZZ_AHEAD"), out int ahead))
        {
            config = config with { HayFeedAheadDays = ahead };
        }

        var sink = new InMemoryLogSink();
        SimLoop loop = SimFactory.CreatePhase0(config, sink);
        SimWorld world = loop.World;

        Workplace farm = FarmFixtures.RaiseAFarm(world);
        FarmFixtures.GiveItGround(world, farm, reach: 2);
        Workplace mill = FoodChainTests.RaiseA(world, BuildingKind.Mill);
        Workplace bakery = FoodChainTests.RaiseA(world, BuildingKind.Bakery);
        Workplace quarry = QuarryTests.RaiseAQuarry(world, QuarryTests.TheNearestReachableRock(world));
        QuarryTests.GiveItRock(world, quarry, 6);
        Workplace mine = MineTests.RaiseAMine(world, MineTests.TheNearestReachable(world, Terrain.IronDeposit));
        MineTests.GiveItIron(world, mine, 6);
        Workplace smithy = ToolsTests.RaiseASmithy(world);
        ObstacleTests.RaiseAWellNearTheFounding(world);
        Workplace post = TradeTests.RaiseAPost(world);
        Workplace barn = HerdTests.RaiseABarn(world);
        HerdTests.GivePasture(world, barn, 20);
        world.TradeKeep.Set(Goods.Firewood, 300);
        world.TradeBuy.Set(Goods.Stone, 50);
        Assert.True(world.SetTradeBuyHead(4).Allowed);

        var posed = new (string Name, Workplace W)[]
        {
            ("farm", farm), ("mill", mill), ("bakery", bakery), ("quarry", quarry), ("mine", mine),
            ("smithy", smithy), ("post", post), ("barn", barn),
        };
        var worked = new int[posed.Length];
        int herdUps = 0, herdDowns = 0, docked = 0, lastHerd = 0, stone = 0, iron = 0, sold = 0, leather = 0;
        int lastStone = 0, lastIron = 0, lastPost = 0, lastLeather = 0, hayMax = 0;
        Bridge? bridge = null;
        int traceLines = 0;
        long wantedSince = -1; int lastOut = 0; var waits = new List<long>();
        VillagerState[] watched =
        {
            VillagerState.Quarrying, VillagerState.Mining, VillagerState.Forging, VillagerState.Grinding,
            VillagerState.Baking, VillagerState.DrawingWater, VillagerState.CuttingHay, VillagerState.FeedingTheHerd,
            VillagerState.Culling, VillagerState.StockingThePost, VillagerState.FetchingFromThePost,
        };
        var inState = new int[watched.Length];
        int stoneHeldMax = 0, ironHeldMax = 0;
        _output.WriteLine($"ZZP posed at tick {world.Tick}; people {world.Villagers.Count}");

        for (int year = 1; year <= years; year++)
        {
            for (int t = 0; t < config.TicksPerYear; t++)
            {
                loop.StepOnce();
                if (bridge is null && world.MostEverAlive >= config.BridgeUnlockPopulation)
                {
                    HashSet<GridPos> before = BridgeTests.Reached(world);
                    bridge = BridgeTests.RaiseABridge(world, BridgeTests.ACrossing(world));
                    bool marked = world.Mark(BuildingKind.Well, BridgeTests.AcrossTheRiver(world, before, bridge)).Allowed;
                    _output.WriteLine($"ZZP bridge raised y{year} tick {world.Tick}; well across marked {marked}");
                    if (Environment.GetEnvironmentVariable("ZZ_HERD") == "1" && barn.HerdHead == 0)
                    {
                        barn.HerdHead = 4; // ZZ_HERD=1: four head at the 10th villager, whatever the boats bring
                    }
                }

                if (barn.HerdHead > 0 && !world.Villagers.Any(v => v.Alive && v.CanWork && v.PinnedTrade == JobKind.Herdsman))
                {
                    Villager hand = world.Villagers.Where(v => v.Alive && v.CanWork).OrderByDescending(v => v.Id).First();
                    world.SetPinnedTrade(hand, JobKind.Herdsman);
                    _output.WriteLine($"ZZP herdsman pinned y{year} tick {world.Tick}: {hand.Name}");
                }

                // ZZ_TRACE=1: every winter (and late-fall feeding) day with beasts in the barn, one ZZH line —
                // the barn's side and the pinned herdsman's side of D643's starving.
                if (traceLines < 400 && Environment.GetEnvironmentVariable("ZZ_TRACE") == "1" && barn.HerdHead > 0
                    && world.Tick % (ulong)config.TicksPerDay == 1)
                {
                    Villager? keeper = world.Villagers.FirstOrDefault(v => v.Alive && v.PinnedTrade == JobKind.Herdsman);
                    StoreBuilding? barnStore = world.BarnStoreOf(barn);
                    bool feedingTime = world.Clock.IsWinter || (world.Clock.Season == Season.Fall && world.Clock.DayOfSeason > config.DaysPerSeason - config.HayFeedAheadDays);
                    if (feedingTime)
                    {
                        traceLines++;
                        string held = barnStore is null ? "none" : barnStore.Store[Goods.Hay].ToString();
                        string goesOut = world.WhereTheHayGoesOut(barn) is GridPos g ? g.ToString() : "null";
                        string who = keeper is null ? "none" : keeper.Id.ToString();
                        string workers = string.Join(",", barn.WorkerIds);
                        _output.WriteLine(
                            $"ZZH y{year} {world.Clock.Season} d{world.Clock.DayOfSeason} head={barn.HerdHead} out={barn.HayPutOut} "
                            + $"store={held} wants={world.HerdWantsFeeding(barn)} goesOut={goesOut} "
                            + $"pasture={world.Zones.WorkGroundTiles(barn.Id)} workers=[{workers}] keeper={who} "
                            + $"kWp={keeper?.WorkplaceId} kState={keeper?.State} kStage={keeper?.LifeStage} kNote='{keeper?.WorkNote}' barnAt={barn.Tile} storeAt={barnStore?.Tile} kAt={keeper?.Position.ToTile()}");
                    }
                }

                if (barn.HerdHead > 0)
                {
                    bool wantsNow = world.HerdWantsFeeding(barn);
                    if (wantsNow && wantedSince < 0) wantedSince = (long)world.Tick;
                    if (barn.HayPutOut > lastOut && wantedSince >= 0) { waits.Add((long)world.Tick - wantedSince); wantedSince = -1; }
                    if (!wantsNow && barn.HayPutOut <= lastOut) wantedSince = wantsNow ? wantedSince : -1;
                    lastOut = barn.HayPutOut;
                }

                if (barn.HerdHead > lastHerd) herdUps += barn.HerdHead - lastHerd;
                if (barn.HerdHead < lastHerd) herdDowns += lastHerd - barn.HerdHead;
                lastHerd = barn.HerdHead;
                stone += Math.Max(0, quarry.Store[Goods.Stone] - lastStone);
                lastStone = quarry.Store[Goods.Stone];
                iron += Math.Max(0, mine.Store[Goods.Iron] - lastIron);
                lastIron = mine.Store[Goods.Iron];
                leather += Math.Max(0, barn.Store[Goods.Leather] - lastLeather);
                lastLeather = barn.Store[Goods.Leather];
                if (world.BoatIsDocked) sold += Math.Max(0, lastPost - post.Store.Held);
                lastPost = post.Store.Held;
                hayMax = Math.Max(hayMax, world.HayHeldFor(barn));
                foreach (Villager v in world.Villagers)
                {
                    int k = Array.IndexOf(watched, v.State);
                    if (k >= 0) inState[k]++;
                }
                stoneHeldMax = Math.Max(stoneHeldMax, world.InStores(Goods.Stone));
                ironHeldMax = Math.Max(ironHeldMax, world.InStores(Goods.Iron));
                if (world.Tick % (ulong)config.TicksPerDay == 0)
                {
                    if (world.BoatIsDocked) docked++;
                    for (int i = 0; i < posed.Length; i++)
                    {
                        if (posed[i].W.WorkerIds.Count > 0) worked[i]++;
                    }
                }
            }

            _output.WriteLine(
                $"ZZP y{year} people={world.Villagers.Count} stone={stone} iron={iron} "
                + $"tools={world.ToolsEverForged} ironTools={world.IronToolsEverForged} taken={world.ToolsEverTaken} "
                + $"wheat={world.WheatEverReaped} flour={world.FlourEverGround} bread={world.BreadEverBaked} "
                + $"cross={world.BridgeCrossings} boats={world.BoatsEverCame} docked={docked} sold={sold} "
                + $"herd={barn.HerdHead} up={herdUps} down={herdDowns} leather={leather} hay={hayMax} "
                + string.Join(" ", posed.Select((p, i) => $"{p.Name}:{worked[i]}"))
                + $" | stoneHeld={stoneHeldMax} ironHeld={ironHeldMax} "
                + string.Join(" ", watched.Select((w, i) => $"{w}={inState[i]}")));
        }

        foreach (var g in sink.Entries.Where(e => e.Message.Contains("starved") || e.Message.Contains("boat") || e.Message.Contains("cull"))
            .GroupBy(e => System.Text.RegularExpressions.Regex.Replace(e.Message, "[0-9]+", "#")).OrderByDescending(g => g.Count()).Take(12))
        {
            _output.WriteLine($"ZZL {g.Count()}x {g.Key}");
        }

        waits.Sort();
        if (waits.Count > 0)
        {
            _output.WriteLine($"ZZW refills {waits.Count}: ticks from wanting to hay out — min {waits[0]} p50 {waits[waits.Count / 2]} "
                + $"p90 {waits[waits.Count * 9 / 10]} max {waits[^1]}; lead is {config.HayFeedAheadDays * config.TicksPerDay}");
        }

        _output.WriteLine($"ZZP end hash {StateHash.Compute(world)} noskill {StateHash.ComputeIgnoringSkills(world)}");
    }
}

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
        int tickLines = 0;
        var ring = new Queue<string>();
        int deaths = 0;
        int calls = 0;
        var cut = new SortedDictionary<string, int>(); VillagerState lastKState = VillagerState.Idle; int lastKId = -1;
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
                    // ZZ_PIN=housed: the newest adult whose household has a home (D644's correction — the newest adult
                    // is often a new household still waiting for its house, and a homeless herdsman rests in the open).
                    bool housed = Environment.GetEnvironmentVariable("ZZ_PIN") == "housed";
                    Villager hand = world.Villagers
                        .Where(v => v.Alive && v.CanWork && (!housed || world.HouseholdOf(v).HasHome))
                        .OrderByDescending(v => v.Id).First();
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

                // ZZ_TICK=1: every tick the herd wants hay, the keeper's state, cold, tile and what he carries (D644's
                // second look: which of his own needs breaks the round trip).
                if (tickLines < 1500 && Environment.GetEnvironmentVariable("ZZ_TICK") == "1" && barn.HerdHead > 0
                    && world.Clock.IsWinter && (world.HerdWantsFeeding(barn) || barn.HayPutOut < barn.HerdHead * 2 * 2))
                {
                    Villager? k = world.Villagers.FirstOrDefault(v => v.Alive && v.PinnedTrade == JobKind.Herdsman);
                    if (k is not null)
                    {
                        tickLines++;
                        _output.WriteLine($"ZZT y{year} {world.Clock.Season} d{world.Clock.DayOfSeason} t{world.Tick} head={barn.HerdHead} "
                            + $"out={barn.HayPutOut} st={k.State} left={k.ActionTicksRemaining} cold={k.Cold} hunger={k.Hunger} "
                            + $"at={k.Position.ToTile()} hay={k.Carried[Goods.Hay]} homeWood={world.HouseholdOf(k).Stockpile.Firewood} "
                            + $"homeAt={world.HouseholdOf(k).HomeTile} villageWood={world.InStores(Goods.Firewood)} "
                            + $"founding={world.Map.FoundingSite} hall={world.TownHall?.Position.ToTile()} hh={k.HouseholdId}");
                    }
                }

                if (world.Villagers.FirstOrDefault(v => v.Alive && v.PinnedTrade == JobKind.Herdsman) is Villager kk && world.Clock.IsWinter)
                {
                    if (kk.Id == lastKId && kk.State == VillagerState.SeekingShelter && lastKState != VillagerState.SeekingShelter)
                    {
                        string key = $"{lastKState}{(kk.Carried[Goods.Hay] > 0 ? "+hay" : "")}";
                        cut[key] = cut.GetValueOrDefault(key) + 1;
                    }
                    lastKState = kk.State; lastKId = kk.Id;
                }

                // ZZ_CALL=1 (D646's prototype of the fix, harness only): while the herd wants feeding, the man seated at the
                // barn, empty-handed on an errand that is not eating, warmth or the herd itself, is set idle so he decides
                // again next tick — what "a hungry herd calls its herdsman" would do, measured before it is specced.
                if (Environment.GetEnvironmentVariable("ZZ_CALL") == "1" && barn.HerdHead > 0 && barn.WorkerIds.Count > 0
                    && world.HerdWantsFeeding(barn)
                    && world.Villagers.FirstOrDefault(v => v.Id == barn.WorkerIds[0]) is Villager called
                    && !called.IsCarrying
                    && called.State is VillagerState.TidyingGround or VillagerState.TravelingHome or VillagerState.Resting
                        or VillagerState.CuttingHay)
                {
                    called.State = VillagerState.Idle;
                    called.ActionTicksRemaining = 0;
                    calls++;
                }

                // ZZ_DEATHS=1 (D646): the last 48 ticks of the barn and its herdsman, kept while a herd stands, printed whole
                // at every head lost — one death, one story. The keeper is whoever is seated at the barn, pinned or not.
                if (Environment.GetEnvironmentVariable("ZZ_DEATHS") == "1" && (barn.HerdHead > 0 || lastHerd > 0))
                {
                    Villager? seated = barn.WorkerIds.Count > 0 ? world.Villagers.FirstOrDefault(v => v.Id == barn.WorkerIds[0]) : null;
                    string home = seated is null ? "-" : world.HouseholdOf(seated).HomeTile?.ToString() ?? "homeless";
                    ring.Enqueue(
                        $"{world.Clock.Season} d{world.Clock.DayOfSeason} t{world.Tick} head={barn.HerdHead} out={barn.HayPutOut} "
                        + $"barnHay={world.BarnStoreOf(barn)?.Store[Goods.Hay]} wants={world.HerdWantsFeeding(barn)} "
                        + $"seats=[{string.Join(",", barn.WorkerIds)}] k={seated?.Id} st={seated?.State} left={seated?.ActionTicksRemaining} "
                        + $"cold={seated?.Cold} at={seated?.Position.ToTile()} hay={seated?.Carried[Goods.Hay]} home={home} "
                        + $"note='{seated?.WorkNote}' "
                        + (seated is null ? "" : HouseholdLine(world, world.HouseholdOf(seated))));
                    while (ring.Count > 48)
                    {
                        ring.Dequeue();
                    }

                    if (barn.HerdHead < lastHerd && deaths < 20)
                    {
                        deaths++;
                        _output.WriteLine($"ZZD ===== head lost #{deaths}, y{year} =====");
                        foreach (string line in ring)
                        {
                            _output.WriteLine($"ZZD {line}");
                        }
                    }
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

        _output.WriteLine($"ZZK calls {calls}");
        _output.WriteLine($"ZZC sheltering began from: {string.Join(" ", cut.Select(kv => $"{kv.Key}={kv.Value}"))}");
        _output.WriteLine($"ZZP end hash {StateHash.Compute(world)} noskill {StateHash.ComputeIgnoringSkills(world)}");
    }

    // D646: the seated herdsman's household — food and firewood against the targets the emergency restock
    // reads (restock_emergency_percent of each), and its living members — so a report says why he was fetching.
    private static string HouseholdLine(SimWorld world, Household h)
    {
        int food = world.FoodIn(h.Stockpile);
        int foodTarget = world.TargetFoodFor(h);
        int wood = h.Stockpile.Firewood;
        int woodTarget = VillageEconomy.FirewoodStoreWantedPerHousehold(world.Config);
        int able = world.Villagers.Count(v => v.Alive && v.CanWork && v.HouseholdId == h.Id);
        return $"hh food={food}/{foodTarget} wood={wood}/{woodTarget} living={world.LivingMembersOf(h)} able={able}";
    }
}

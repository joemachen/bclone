using Bclone.Sim.Config;
using Bclone.Sim.Core;
using Bclone.Sim.Logging;
using Bclone.Sim.Systems;
using Bclone.Sim.World;
using Xunit;
using Xunit.Abstractions;

namespace Bclone.Sim.Tests;

/// <summary>
/// The player's stock limits, held by the sim from the first tick — one per good, no food total
/// (D409, `stock-limits-and-laborers.md §4.4`).
/// </summary>
/// <remarks>
/// <para>
/// ⛔⛔ <b>THE LIMITS LIVED IN THE VIEW.</b> The Stock limits panel set its own numbers when it was
/// built, so the game Joe played always had limits and no test or measurement ever did: D407's
/// *"shipped 56 alive, 28 starved"* was a game nobody plays, and the firewood branch as found —
/// played with the panel's firewood 400 — left <b>nobody alive on six shipped seeds of six</b>.
/// </para>
/// <para>
/// Joe: *"They should be synced with and regulated by the stock limit panel — that is literally the
/// whole point of the stock limit panel"*, and *"remove produce entirely and add a forage row."*
/// </para>
/// </remarks>
public sealed class StartingStockLimitsTests
{
    private readonly ITestOutputHelper _output;

    public StartingStockLimitsTests(ITestOutputHelper output) => _output = output;

    /// <summary>⭐ A new shipped game starts with Joe's numbers, and nothing else does.</summary>
    [Fact]
    public void TheShippedGameStartsWithThePlayersLimits()
    {
        SimWorld world = SimFactory.CreatePhase0(ShippedConfig.Load(), new InMemoryLogSink()).World;

        var expected = new Dictionary<Goods, int>
        {
            [Goods.Produce] = 2000,
            [Goods.Wheat] = 1000,
            [Goods.Fish] = 250,
            [Goods.Meat] = 250,
            [Goods.Logs] = 200,
            [Goods.Firewood] = 400,
            [Goods.Stone] = 200,
            [Goods.Tools] = 200,
            [Goods.IronTools] = 200,
            [Goods.Iron] = 200,
            [Goods.Leather] = 200,
        };

        for (int id = 0; id < world.GoodsCatalog.Count; id++)
        {
            var goods = (Goods)id;
            _output.WriteLine($"{world.GoodsCatalog.NameOf(goods)}: {world.StockLimits.For(goods)?.ToString() ?? "none"}");
            Assert.Equal(expected[goods], world.StockLimits.For(goods));
        }

        Assert.Equal("forage", world.GoodsCatalog.NameOf(Goods.Produce));

        // ⛔ The code-built fixtures say nothing and get no limits — the worlds they were written against.
        SimWorld fixture = SimFactory.CreatePhase0(VillageFixtures.Village, new InMemoryLogSink()).World;
        Assert.False(fixture.StockLimits.AnySet);
    }

    [Fact]
    public void AStartingLimitThatNamesNoGoodIsRefusedAtLoad()
    {
        SimConfigException error = Assert.Throws<SimConfigException>(() =>
            (VillageFixtures.Village with { StartingStockLimits = new Dictionary<string, int> { ["produce"] = 100 } }).Validate());

        _output.WriteLine(error.Message);
        Assert.Contains("'produce'", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void EveryGoodSitsUnderAHeadingAndEveryFoodUnderFood()
    {
        SimConfig config = VillageFixtures.Village;
        GoodRow[] rows = config.GoodsCatalog.ToArray();

        rows[(int)Goods.Stone] = rows[(int)Goods.Stone] with { Category = GoodCategory.Unset };
        SimConfigException unset = Assert.Throws<SimConfigException>(() => (config with { GoodsList = rows }).Validate());
        Assert.Contains("names no category", unset.Message, StringComparison.Ordinal);

        rows = config.GoodsCatalog.ToArray();
        rows[(int)Goods.Fish] = rows[(int)Goods.Fish] with { Category = GoodCategory.Materials };
        SimConfigException misfiled = Assert.Throws<SimConfigException>(() => (config with { GoodsList = rows }).Validate());
        Assert.Contains("can be eaten", misfiled.Message, StringComparison.Ordinal);
    }

    /// <summary>
    /// ⭐⭐ A met forage limit stands the foragers down and nobody else — there is no food total to
    /// stand the hunters down with it.
    /// </summary>
    [Fact]
    public void AMetForageLimitStopsTheForagersAndOnlyThem()
    {
        SimLoop loop = SimFactory.CreatePhase0(VillageFixtures.Village, new InMemoryLogSink());
        SimWorld world = loop.World;
        HuntingTests.RaiseALodgeFor(world);
        loop.Step(world.Config.TicksPerSeason / 2);

        // The lodge ranks first and covers every mouth here, so the patches want nobody — posed from
        // the other end: the MEAT row stands the hunters down, and the mouths fall to the patches.
        LabourQuota open = LabourQuota.For(world);
        Assert.True(open.For(JobKind.Hunter) > 0, "Nobody hunts, so there is nobody for the meat limit to stand down.");

        world.SetStockLimit(Goods.Meat, 0);
        LabourQuota meatMet = LabourQuota.For(world);

        world.SetStockLimit(Goods.Produce, 0);
        LabourQuota bothMet = LabourQuota.For(world);

        _output.WriteLine(
            $"open: {open.For(JobKind.Hunter)} hunters, {open.Foragers} foragers · meat 0: {meatMet.For(JobKind.Hunter)} / {meatMet.Foragers} · "
            + $"and forage 0: {bothMet.For(JobKind.Hunter)} / {bothMet.Foragers}");

        // ⭐ Each row stops its own trade and no other — the food total that stood them all down is gone.
        Assert.Equal(0, meatMet.For(JobKind.Hunter));
        Assert.True(meatMet.Foragers > 0, "A met meat limit left nobody on food: the mouths the lodge fed did not fall to the patches.");
        Assert.Equal(0, bothMet.Foragers);
        Assert.False(world.WantsMoreOf(Goods.Produce));
        Assert.Contains("0 forage", world.WhyTheVillageWantsNoMoreOf(Goods.Produce) ?? string.Empty, StringComparison.Ordinal);
    }

    /// <summary>
    /// ⛔⛔ An armful takes the good the stores are shortest of first — <b>not food first</b>.
    /// </summary>
    /// <remarks>
    /// Id order put forage in every armful ahead of the firewood heaped on the same tile, and a
    /// cold start's cart is the one store that takes both: the firewood never went in and the homes,
    /// which fetch from stores, froze beside it.
    /// </remarks>
    [Fact]
    public void AnArmfulTakesWhatTheStoresAreShortestOfFirst()
    {
        SimWorld world = SimFactory.CreatePhase0(ShippedConfig.Load(), new InMemoryLogSink()).World;
        StoreBuilding cart = world.AnyStoreOf(StoreKind.Cart);
        Assert.Equal(0, cart.Store.Firewood);

        // Make room for forage too, so id order would have put forage first and been allowed to.
        Assert.True(cart.Store.TryTake(Goods.Produce, cart.Store[Goods.Produce] - 100));
        Assert.True(cart.HasRoomFor(Goods.Produce) && cart.HasRoomFor(Goods.Firewood), "the cart must take both, or the order is not what decides");

        // On open ground where somebody stands — a building's own tile is not somewhere a heap is reached.
        Villager hauler = world.Villagers.First(v => v.CanWork);
        GridPos heap = hauler.Tile;
        world.SetDown(heap, Goods.Produce, 500);
        world.SetDown(heap, Goods.Firewood, 500);
        Assert.NotNull(world.NearestStorageWithRoomFor(heap, Goods.Firewood));
        Assert.NotNull(world.NearestStorageWithRoomFor(heap, Goods.Produce));

        hauler.ErrandX = heap.X;
        hauler.ErrandY = heap.Y;
        BehaviorSystem.PickUpFromTheGroundForTest(world, hauler);

        // The seam hauls in the same call when a store is a step away, so read the heaps, not the arms.
        int firewoodTaken = 500 - world.GroundStackAt(heap, Goods.Firewood);
        int forageTaken = 500 - world.GroundStackAt(heap, Goods.Produce);
        _output.WriteLine(
            $"the armful: {firewoodTaken} firewood, {forageTaken} forage "
            + $"(cart: forage {cart.Store[Goods.Produce]} of a 2000 limit, firewood {cart.Store.Firewood} of 400)");
        Assert.Equal(world.Config.CarryCapacity, firewoodTaken);
        Assert.Equal(0, forageTaken);
    }

    /// <summary>
    /// ⭐ Joe's rule on logs holds on a cold start (D408): once the village has its 200 — in the
    /// stores OR lying where they were cleared — nobody clears another tree.
    /// </summary>
    /// <remarks>
    /// The cart takes no logs and the pile is small, so a limit met against the stores alone was
    /// never met: the laborers cleared into heaps, 19,818 by year 50 on shipped seed 1.
    /// </remarks>
    [Fact]
    public void AColdStartKeepsItsLogLimitWithThePaintOut()
    {
        SimConfig config = ShippedConfig.Load() with { Seed = 1 };
        SimLoop loop = SimFactory.CreatePhase0(config, new InMemoryLogSink());
        SimWorld world = loop.World;
        ColdStartTests.PlayTheOpening(world);
        ColdStartTests.PaintTheNearbyTrees(world, 10);

        int limit = world.StockLimits.For(Goods.Logs) ?? throw new InvalidOperationException("no log limit");

        // A tree is a tile's yield, and every able hand can be mid-clear when the limit is met.
        int inFlight = (world.GoodsCatalog.YieldPerTileOf(Goods.Logs) * world.Villagers.Count(v => v.CanWork)) + world.Config.CarryCapacity;
        int most = 0;
        for (int tick = 0; tick < config.TicksPerYear * 10; tick++)
        {
            loop.StepOnce();
            most = Math.Max(most, world.HeldAgainstItsLimit(Goods.Logs));
        }

        _output.WriteLine($"ten years with the paint out: at most {most} logs held against a limit of {limit} (bar {limit + inFlight}); {world.OnTheGround(Goods.Logs)} on the ground now");
        Assert.True(most >= limit, "The village never reached its log limit, so this proves nothing.");
        Assert.True(most <= limit + inFlight, $"{most} logs against a limit of {limit} — the clearing is not stopping.");
    }

    /// <summary>
    /// ⛔ The heaps' per-good count is an index kept where a heap changes (D409) — it must say what a
    /// walk of the heaps says, on every tick.
    /// </summary>
    [Fact]
    public void TheHeapCountIsWhatTheHeapsHold()
    {
        SimConfig config = ShippedConfig.Load() with { Seed = 1, StartingStockLimits = new Dictionary<string, int>() };
        SimLoop loop = SimFactory.CreatePhase0(config, new InMemoryLogSink());
        SimWorld world = loop.World;
        ColdStartTests.PlayTheOpening(world);
        ColdStartTests.PaintTheNearbyTrees(world, 10);

        int ticksWithHeaps = 0;
        for (int tick = 0; tick < config.TicksPerYear * 5; tick++)
        {
            loop.StepOnce();
            ticksWithHeaps += world.GroundStacks.Count > 0 ? 1 : 0;
            for (int id = 0; id < world.GoodsCatalog.Count; id++)
            {
                var goods = (Goods)id;
                int walked = world.GroundStacks.Where(s => s.Goods == goods).Sum(s => s.Amount);
                Assert.Equal(walked, world.OnTheGround(goods));
            }
        }

        _output.WriteLine($"{ticksWithHeaps} ticks of five years had a heap somewhere");
        Assert.True(ticksWithHeaps > 0, "Nothing was ever set down, so the index was never exercised.");
    }
}

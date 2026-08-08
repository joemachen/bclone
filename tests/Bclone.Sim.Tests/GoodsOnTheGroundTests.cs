using Bclone.Sim.Config;
using Bclone.Sim.Core;
using Bclone.Sim.Determinism;
using Bclone.Sim.Logging;
using Bclone.Sim.World;
using Xunit;
using Xunit.Abstractions;

namespace Bclone.Sim.Tests;

/// <summary>
/// Goods can be set down on the ground, and anybody can pick them up — D96,
/// <c>specs/goods-on-the-ground.md</c>.
/// </summary>
/// <remarks>
/// <para>
/// <b>Joe took both restraints, and each gets its own guard here.</b> Goods on the ground are
/// <em>supply-invisible</em> — they count in no total, no quota and no birth gate — and
/// setting down is <em>last-resort-only</em>, never a convenience. Together they say one
/// thing: the ground is where goods end up, never where they are kept.
/// </para>
/// <para>
/// <b>The supply-invisible half is guarded structurally as well as here.</b> A heap is a
/// <see cref="GroundStack"/> and not a <see cref="StoreBuilding"/>, so it is not in the list
/// every total walks — which is why no reader had to be taught to skip it. These tests exist
/// because a later refactor could quietly move it into that list and nothing else would
/// complain.
/// </para>
/// </remarks>
public sealed class GoodsOnTheGroundTests
{
    private readonly ITestOutputHelper _output;

    public GoodsOnTheGroundTests(ITestOutputHelper output) => _output = output;

    private static SimLoop Loop(SimConfig config) =>
        ManagedVillage.Loop(config, new InMemoryLogSink());

    private static int OnTheGround(SimWorld world)
    {
        int total = 0;
        for (int i = 0; i < world.GroundStacks.Count; i++)
        {
            total += world.GroundStacks[i].Amount;
        }

        return total;
    }

    // ---------------------------------------------------------------
    //  A heap is sim state
    // ---------------------------------------------------------------

    /// <summary>Setting a load down changes the world's fingerprint.</summary>
    /// <remarks>
    /// Goods in a place are as much sim state as goods in a store — that is exactly the
    /// distinction D96 draws against D83's arms, and a heap that did not hash would let two
    /// villages disagree about where a hundred logs are while reading identical (D51).
    /// </remarks>
    [Fact]
    public void AHeapIsPartOfTheStateHash()
    {
        SimConfig config = VillageFixtures.Village;
        SimWorld bare = Loop(config).World;
        SimWorld littered = Loop(config).World;

        ulong before = StateHash.Compute(bare);
        Assert.Equal(before, StateHash.Compute(littered));

        littered.SetDown(littered.Map.FoundingSite, Goods.Logs, 40);

        Assert.NotEqual(before, StateHash.Compute(littered));
    }

    /// <summary>A village that has dropped nothing hashes exactly as it did before.</summary>
    /// <remarks>
    /// <b>Sparse and countless, like the harvest layer (D87) and unlike residential.</b> A
    /// count mixed unconditionally would put a fresh zero into every established village and
    /// move both goldens for a feature nobody used. This is the assertion that says the
    /// hashing is invisible when unused; <c>StockLimitTests</c>' goldens are the other half.
    /// </remarks>
    [Fact]
    public void AVillageThatDroppedNothingIsHashedAsThoughTheGroundDidNotExist()
    {
        SimConfig config = VillageFixtures.Village;
        SimWorld world = Loop(config).World;

        Assert.Empty(world.GroundStacks);

        // Set a heap down and take it straight back up: the world must return to exactly
        // where it was, which is only true if an empty list mixes nothing at all.
        ulong before = StateHash.Compute(world);
        world.SetDown(world.Map.FoundingSite, Goods.Food, 25);
        Assert.Equal(25, world.TakeFromGround(world.Map.FoundingSite, Goods.Food, 99));
        Assert.Empty(world.GroundStacks);

        Assert.Equal(before, StateHash.Compute(world));
    }

    // ---------------------------------------------------------------
    //  Supply-invisible — Joe's first restraint
    // ---------------------------------------------------------------

    /// <summary>⭐ A hundred logs in a field are not supply, by any reader that matters.</summary>
    /// <remarks>
    /// <para>
    /// <b>D83's rule applied rather than a new one invented:</b> the village can spend what it
    /// can reach <em>and has put away</em>, and a heap in a field is neither. The consequence
    /// is what makes the restraint self-enforcing — a village living off the ground never
    /// grows, because the birth gate cannot see it.
    /// </para>
    /// <para>
    /// Every one of these readers walks <c>StoreBuildings</c>, so none of them had to be
    /// taught anything. That is the whole argument for a heap not being a fifth
    /// <see cref="StoreKind"/> — see <see cref="GroundStack"/>.
    /// </para>
    /// </remarks>
    [Fact]
    public void GoodsOnTheGroundAreNotSupply()
    {
        SimConfig config = VillageFixtures.Village;
        SimWorld world = Loop(config).World;

        int food = world.TotalFood();
        int logs = world.LogsInSheds();
        int firewood = world.FirewoodInSheds();
        int room = world.FoodTheVillageHasRoomFor();

        GridPos field = world.Map.FoundingSite;
        world.SetDown(field, Goods.Food, 500);
        world.SetDown(field, Goods.Logs, 500);
        world.SetDown(field, Goods.Firewood, 500);

        _output.WriteLine(
            $"1500 goods on the ground: food {food} -> {world.TotalFood()}, "
            + $"logs {logs} -> {world.LogsInSheds()}, firewood {firewood} -> "
            + $"{world.FirewoodInSheds()}, room {room} -> {world.FoodTheVillageHasRoomFor()}");

        Assert.Equal(food, world.TotalFood());
        Assert.Equal(logs, world.LogsInSheds());
        Assert.Equal(firewood, world.FirewoodInSheds());
        Assert.Equal(room, world.FoodTheVillageHasRoomFor());
    }

    /// <summary>A heap is not in the store list, which is what makes the above true.</summary>
    [Fact]
    public void AHeapIsNotAStore()
    {
        SimConfig config = VillageFixtures.Village;
        SimWorld world = Loop(config).World;

        int stores = world.StoreBuildings.Count;
        world.SetDown(world.Map.FoundingSite, Goods.Logs, 40);

        Assert.Equal(stores, world.StoreBuildings.Count);
        Assert.Single(world.GroundStacks);
    }

    // ---------------------------------------------------------------
    //  The one door in, and the one door out
    // ---------------------------------------------------------------

    /// <summary>Loads of one good on one tile become one heap, not fifty.</summary>
    /// <remarks>
    /// A clearing worked over a year is one pile of logs. Without merging, a busy tile would
    /// grow a list entry per armful and the hash would carry the order they were dropped in —
    /// a fact that changes nothing about what happens (D51's trap, D92's guard).
    /// </remarks>
    [Fact]
    public void LoadsOfOneGoodOnOneTileMerge()
    {
        SimWorld world = Loop(VillageFixtures.Village).World;
        GridPos at = world.Map.FoundingSite;

        world.SetDown(at, Goods.Logs, 10);
        world.SetDown(at, Goods.Logs, 15);
        world.SetDown(at, Goods.Food, 5);

        Assert.Equal(2, world.GroundStacks.Count);
        Assert.Equal(25, world.GroundStackAt(at, Goods.Logs));
        Assert.Equal(5, world.GroundStackAt(at, Goods.Food));
    }

    /// <summary>An emptied heap goes, rather than lingering as a zero.</summary>
    [Fact]
    public void AnEmptiedHeapDisappears()
    {
        SimWorld world = Loop(VillageFixtures.Village).World;
        GridPos at = world.Map.FoundingSite;

        world.SetDown(at, Goods.Logs, 30);
        Assert.Equal(20, world.TakeFromGround(at, Goods.Logs, 20));
        Assert.Single(world.GroundStacks);

        Assert.Equal(10, world.TakeFromGround(at, Goods.Logs, 999));
        Assert.Empty(world.GroundStacks);
        Assert.Equal(0, world.TakeFromGround(at, Goods.Logs, 5));
    }

    // ---------------------------------------------------------------
    //  Somebody comes and gets it — D66's second errand, at last
    // ---------------------------------------------------------------

    /// <summary>⭐ A heap beside a store with room gets carried into it.</summary>
    /// <remarks>
    /// <para>
    /// <b>The errand has to exist in its own right</b>, and this is why: goods on the ground
    /// are supply-invisible, so <em>"the village wants more logs"</em> cannot reach them —
    /// that question reads stores. <em>"There is a load lying about; take it to a store"</em>
    /// reaches them, and needs no construction site to exist. That is the second of D66's two
    /// missing errands, arriving where D96 predicted.
    /// </para>
    /// <para>
    /// Dropped a short walk from the village so the trip is a trip. The village has spare
    /// hands most ticks (winter measured 86% idle), so this does not need staging.
    /// </para>
    /// </remarks>
    [Fact]
    public void SomebodyFetchesALoadOffTheGround()
    {
        SimConfig config = VillageFixtures.Village;
        SimLoop loop = Loop(config);
        SimWorld world = loop.World;

        GridPos at = world.Map.FoundingSite;
        world.SetDown(at, Goods.Logs, 120);

        int inStoresBefore = world.LogsInSheds();
        loop.Step(config.TicksPerYear / 2);

        _output.WriteLine(
            $"half a year on: {OnTheGround(world)} still on the ground, "
            + $"logs in stores {inStoresBefore} -> {world.LogsInSheds()}");

        Assert.Equal(0, world.GroundStackAt(at, Goods.Logs));
        Assert.Empty(world.GroundStacks);
    }

    /// <summary>⭐ And nobody shuttles when there is nowhere to put it.</summary>
    /// <remarks>
    /// <para>
    /// <b>The failure this guard exists for is a loop, not a stall.</b> Without the condition
    /// in <see cref="SimWorld.NearestGroundStack"/>, a spare hand picks up the heap beside a
    /// full shed, walks it back to the same full shed, and sets it down again — forever, and
    /// at the cost of every idle tick in the village.
    /// </para>
    /// <para>
    /// A village with no room simply leaves its heaps alone, which is the self-correcting
    /// behaviour D96 predicted and needs no rule telling anybody to.
    /// </para>
    /// </remarks>
    [Fact]
    public void NobodyFetchesALoadThereIsNowhereToPut()
    {
        SimConfig config = VillageFixtures.Village;
        SimLoop loop = Loop(config);
        SimWorld world = loop.World;

        // Fill every store that takes logs, so the heap has nowhere to go.
        for (int i = 0; i < world.StoreBuildings.Count; i++)
        {
            StoreBuilding store = world.StoreBuildings[i];
            if (store.Accepts(Goods.Logs))
            {
                store.Store.Receive(Goods.Logs, store.Store.FreeSpace);
            }
        }

        GridPos at = world.Map.FoundingSite;
        world.SetDown(at, Goods.Logs, 120);

        int tidyTicks = 0;
        for (int tick = 0; tick < config.TicksPerYear / 4; tick++)
        {
            loop.StepOnce();
            for (int i = 0; i < world.Villagers.Count; i++)
            {
                if (world.Villagers[i].State == VillagerState.TidyingGround)
                {
                    tidyTicks++;
                }
            }
        }

        _output.WriteLine(
            $"stores full: {world.GroundStackAt(at, Goods.Logs)} still on the ground, "
            + $"{tidyTicks} villager-ticks spent walking to it");

        Assert.Equal(0, tidyTicks);
    }

    // ---------------------------------------------------------------
    //  Last resort — Joe's second restraint
    // ---------------------------------------------------------------

    /// <summary>⭐ A village with room never puts anything on the ground.</summary>
    /// <remarks>
    /// <para>
    /// <b>Asserted over a played run rather than by reading the code</b>, because "last
    /// resort" is a claim about behaviour and there are four places a load can be set down.
    /// A year of an established village with room in its stores must produce no heaps at all.
    /// </para>
    /// <para>
    /// <b>It fails the moment setting down becomes convenient</b> — which is the way this
    /// mechanic goes wrong: a heap with no decay and no capacity would otherwise be a better
    /// granary than the granary.
    /// </para>
    /// </remarks>
    [Fact]
    public void AVillageWithRoomNeverSetsAnythingDown()
    {
        SimConfig config = VillageFixtures.Village;
        SimLoop loop = Loop(config);
        SimWorld world = loop.World;

        for (int tick = 0; tick < config.TicksPerYear * 5; tick++)
        {
            loop.StepOnce();

            bool anyFull = false;
            for (int i = 0; i < world.StoreBuildings.Count; i++)
            {
                if (world.StoreBuildings[i].Store.IsFull)
                {
                    anyFull = true;
                    break;
                }
            }

            if (anyFull)
            {
                // Past this point the village genuinely has nowhere to put things, which
                // is the case setting down exists for. The claim is about the years
                // BEFORE that, and stopping here keeps it an honest one.
                _output.WriteLine(
                    $"a store filled at tick {tick}; {OnTheGround(world)} on the ground");
                Assert.Equal(0, OnTheGround(world));
                return;
            }

            Assert.Equal(0, OnTheGround(world));
        }

        _output.WriteLine("five years with room everywhere and nothing was ever set down");
    }

    // ---------------------------------------------------------------
    //  Conservation — the leak this closes
    // ---------------------------------------------------------------

    /// <summary>⭐ A load that arrives at a full store is set down, not destroyed.</summary>
    /// <remarks>
    /// <para>
    /// <b>This was a live leak and it was large.</b> <c>Stockpile.Add</c> returns how much
    /// actually fitted and its own remarks say the return value must not be ignored;
    /// <c>ArriveAt</c> ignored it and zeroed the villager's arms, so anything a full store
    /// refused ceased to exist. Measured over fifty years of an established village:
    /// <b>17,451 food</b> went into the granary's doorstep and out of the world.
    /// </para>
    /// <para>
    /// The direction is what made it invisible — totals only ever fall, so nothing ever read
    /// as wrong. <c>RaiseTheBuilding</c> one file over states the rule this restores:
    /// <em>"never dropped, per the conservation rule."</em>
    /// </para>
    /// </remarks>
    [Fact]
    public void ALoadThatWillNotFitIsSetDownRatherThanLost()
    {
        SimConfig config = VillageFixtures.Village;
        SimLoop loop = Loop(config);
        SimWorld world = loop.World;

        // Every store packed, so wherever this load goes it will be refused. Filled with
        // logs rather than food so nobody eats the evidence.
        StoreBuilding shed = world.AnyStoreOf(StoreKind.Shed);
        for (int i = 0; i < world.StoreBuildings.Count; i++)
        {
            Stockpile store = world.StoreBuildings[i].Store;
            store.Receive(Goods.Logs, store.FreeSpace);
        }

        Assert.All(world.StoreBuildings, store => Assert.True(store.Store.IsFull));

        // One villager, one armful, standing at the shed door with it.
        Villager carrier = world.Villagers[0];
        carrier.Position = shed.Position;
        carrier.CarriedLogs = 60;
        carrier.State = VillagerState.HaulingToStore;
        carrier.ActionTicksRemaining = 0;

        int held = shed.Store.Held;
        loop.StepOnce();

        _output.WriteLine(
            $"{carrier.Name} arrived at a full shed with 60 logs: {carrier.CarriedLogs} still "
            + $"carried, {OnTheGround(world)} on the ground, shed {held} -> {shed.Store.Held}");

        // ⭐ The load still exists. Before D96 this assertion read 0 = 0 + 0: Add took what
        // fitted (nothing) and ArriveAt zeroed the arms anyway.
        Assert.Equal(60, OnTheGround(world) + carrier.CarriedLogs);
        Assert.Equal(held, shed.Store.Held);
    }

    /// <summary>And it happens in play, not only when a test poses it.</summary>
    /// <remarks>
    /// The behavioural half. A village whose stores are all full goes on producing — that is
    /// the state D80 crashed in and D96 answers — and what it produces has to end up
    /// somewhere. Anti-vacuity (D7) for the guard above: if nothing is ever refused in a
    /// played year, the mechanism is being asserted in a world where it cannot fire.
    /// </remarks>
    [Fact]
    public void AVillageWithNowhereToPutAnythingEndsTheYearWithHeaps()
    {
        SimConfig config = VillageFixtures.Village;
        SimLoop loop = Loop(config);
        SimWorld world = loop.World;

        for (int i = 0; i < world.StoreBuildings.Count; i++)
        {
            Stockpile store = world.StoreBuildings[i].Store;
            store.Receive(Goods.Logs, store.FreeSpace);
        }

        loop.Step(config.TicksPerYear);

        _output.WriteLine(
            $"a year with every store full: {OnTheGround(world)} goods on the ground in "
            + $"{world.GroundStacks.Count} heaps");

        Assert.True(
            OnTheGround(world) > 0,
            "A year passed with every store full and nobody ever had to put a load down, "
            + "so the mechanism was never exercised.");
    }
}

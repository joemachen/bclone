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
        SimFactory.CreatePhase0(config, new InMemoryLogSink());

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
        world.SetDown(world.Map.FoundingSite, Goods.Produce, 25);
        Assert.Equal(25, world.TakeFromGround(world.Map.FoundingSite, Goods.Produce, 99));
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
        int logs = world.LogsInWarehouses();
        int firewood = world.FirewoodInWarehouses();
        int room = world.FoodTheVillageHasRoomFor();

        GridPos field = world.Map.FoundingSite;
        world.SetDown(field, Goods.Produce, 500);
        world.SetDown(field, Goods.Logs, 500);
        world.SetDown(field, Goods.Firewood, 500);

        _output.WriteLine(
            $"1500 goods on the ground: food {food} -> {world.TotalFood()}, "
            + $"logs {logs} -> {world.LogsInWarehouses()}, firewood {firewood} -> "
            + $"{world.FirewoodInWarehouses()}, room {room} -> {world.FoodTheVillageHasRoomFor()}");

        Assert.Equal(food, world.TotalFood());
        Assert.Equal(logs, world.LogsInWarehouses());
        Assert.Equal(firewood, world.FirewoodInWarehouses());
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
        world.SetDown(at, Goods.Produce, 5);

        Assert.Equal(2, world.GroundStacks.Count);
        Assert.Equal(25, world.GroundStackAt(at, Goods.Logs));
        Assert.Equal(5, world.GroundStackAt(at, Goods.Produce));
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

        int inStoresBefore = world.LogsInWarehouses();
        loop.Step(config.TicksPerYear / 2);

        _output.WriteLine(
            $"half a year on: {OnTheGround(world)} still on the ground, "
            + $"logs in stores {inStoresBefore} -> {world.LogsInWarehouses()}");

        Assert.Equal(0, world.GroundStackAt(at, Goods.Logs));

        // And it went into a warehouse rather than being shuffled to another patch of dirt.
        Assert.True(
            world.LogsInWarehouses() > inStoresBefore,
            $"The heap left the ground but the stores still hold {world.LogsInWarehouses()}.");

        // ⚠️ NOT `Assert.Empty(world.GroundStacks)` ANY MORE, which is a claim about the whole
        // valley and was only ever true because the valley was quiet. A village that fells
        // for a living has heaps in flight at almost any instant — a forester sets a load
        // down, somebody comes for it — and after D126 and D127 the felling never stops, so
        // the emptiness this asserted was the absence of work rather than the presence of
        // hauling. It failed on a village doing its job.
        //
        // The claim in the title is about THIS heap, and that is what is checked above.
    }

    /// <summary>⭐ And nobody shuttles when there is nowhere to put it.</summary>
    /// <remarks>
    /// <para>
    /// <b>The failure this guard exists for is a loop, not a stall.</b> Without the condition
    /// in <see cref="SimWorld.NearestGroundStack"/>, a spare hand picks up the heap beside a
    /// full warehouse, walks it back to the same full warehouse, and sets it down again — forever, and
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

    /// <summary>
    /// ⭐⭐ A full granary beside a half-empty market never sends anyone back and forth (D370).
    /// </summary>
    /// <remarks>
    /// <para>
    /// Joe: *"when the granary is full and there is no more food storage on the map, the villagers
    /// will constantly bounce back and forth between their home and the granary."* Four predicates
    /// asked *"does a store have room?"* and disagreed: the heap-fetch and the buffer rule asked
    /// EVERY store, market included; the load's destination asked storage only (D199) and fell
    /// back to a granary *full or not* (D48/D80). So a spare hand fetched the heap because the
    /// market had room, walked it to the full granary because only storage may take it, set it
    /// down at the door, went home, and fetched it again.
    /// </para>
    /// <para>
    /// The logs guard above could not see it: the market never takes logs. This one poses food.
    /// </para>
    /// </remarks>
    [Fact]
    public void AFullGranaryNeverSendsAnyoneBackAndForth()
    {
        SimConfig config = VillageFixtures.Village;
        SimLoop loop = Loop(config);
        SimWorld world = loop.World;

        // Every STORAGE building that takes food is full; the market — a counter — has room.
        for (int i = 0; i < world.StoreBuildings.Count; i++)
        {
            StoreBuilding store = world.StoreBuildings[i];
            if (store.IsStorage && store.Accepts(Goods.Produce))
            {
                store.Store.Receive(Goods.Produce, store.Store.FreeSpace);
            }
        }

        StoreBuilding market = world.AnyStoreOf(StoreKind.Market);
        Assert.True(market.HasRoomFor(Goods.Produce), "the fixture's market is full, so the trap cannot be posed");

        // Joe's state: the larders are full and the village has the food it wants, so nobody is
        // foraging and the spare hands are spare — the heap is not somebody's dinner.
        foreach (Household household in world.Households)
        {
            int wanted = world.TargetFoodFor(household);
            if (world.FoodIn(household.Stockpile) < wanted)
            {
                household.Stockpile.Add(Goods.Produce, wanted);
            }
        }

        Assert.True(world.SetStockLimit(Goods.Produce, 1).Allowed);

        // And every hand is a laborer — the spare hands are the ones that tidy heaps.
        foreach (JobKind kind in JobLimits.Kinds)
        {
            world.SetJobLimit(kind, 0);
        }

        GridPos at = world.Map.FoundingSite;
        world.SetDown(at, Goods.Produce, 120);
        int onTheGround = world.OnTheGround(Goods.Produce);

        // ⚠️ THE BOUNCE IS INVISIBLE TO A STATE COUNT. The heap and the full store are a tile
        // apart, so pick-up, walk, refusal and set-down happen inside two ticks and the villager
        // is sampled as `TravelingHome` at the store's door, then `Resting` at home, for ever.
        // What CAN be seen is a villager turning for home EMPTY-HANDED from a full storage
        // building's door: nothing was taken (the larders are held full, so nobody fetches) and
        // nothing was put in (it is full), so they came with a load and left it there.
        var fullDoors = new HashSet<GridPos>();
        foreach (StoreBuilding store in world.StoreBuildings)
        {
            if (store.IsStorage && store.Accepts(Goods.Produce))
            {
                fullDoors.Add(store.Tile);
            }
        }

        int setDownAtAFullDoor = 0;
        int turnedBackEmptyHanded = 0;
        int heapedAtDoors = HeapedAt(fullDoors);
        for (int tick = 0; tick < config.TicksPerYear / 4; tick++)
        {
            // The larders stay full, so nobody eats the heap and the only thing that can move
            // it is a carrier with nowhere to put it.
            foreach (Household household in world.Households)
            {
                int wanted = world.TargetFoodFor(household);
                if (world.FoodIn(household.Stockpile) < wanted)
                {
                    household.Stockpile.Add(Goods.Produce, wanted - world.FoodIn(household.Stockpile));
                }
            }

            loop.StepOnce();
            int now = HeapedAt(fullDoors);
            if (now > heapedAtDoors)
            {
                setDownAtAFullDoor++;
            }

            heapedAtDoors = now;

            for (int i = 0; i < world.Villagers.Count; i++)
            {
                Villager villager = world.Villagers[i];
                if (fullDoors.Contains(villager.Tile) && villager.State == VillagerState.TravelingHome && !villager.IsCarrying)
                {
                    turnedBackEmptyHanded++;
                }
            }
        }

        _output.WriteLine(
            $"storage full, market with room: {onTheGround} produce on the ground became {world.OnTheGround(Goods.Produce)}; "
            + $"a load was set down at a full store's door {setDownAtAFullDoor} times, and somebody turned for home "
            + $"empty-handed from one {turnedBackEmptyHanded} times, in a season");

        // ⚠️ The heap and the door are a tile apart, so the pick-up, the refusal and the set-down
        // happen inside one tick and the heap reads unchanged between ticks — the set-down count
        // alone scored zero on the bounce. The empty-handed turn is what it leaves behind.
        Assert.True(
            setDownAtAFullDoor == 0 && turnedBackEmptyHanded == 0,
            $"a load was set down at a full store's door {setDownAtAFullDoor} times and somebody turned for home empty-handed from one "
            + $"{turnedBackEmptyHanded} times in a season — the heap is being carried to a store that cannot take it, set down, and carried again");

        int HeapedAt(HashSet<GridPos> doors)
        {
            int total = 0;
            foreach (GridPos door in doors)
            {
                total += world.GroundStackAt(door, Goods.Produce);
            }

            return total;
        }
    }

    /// <summary>
    /// ⭐⭐ A mixed heap at a full granary's door moves what has a shelf and leaves the rest —
    /// and one villager goes for it, not eight (D371).
    /// </summary>
    /// <remarks>
    /// <para>
    /// Joe's log, year 41: eight villagers cycling *resting → fetching a load off the ground →
    /// walking home* at the full granary's tile every two ticks, and nobody ever picking anything
    /// up. The heap at the door held fish beside something a warehouse takes; the heap fetch
    /// approved the TILE because one stack on it had a shelf, the pick-up took the fish first (id
    /// order), the fish's only destination was the full granary one tile away, and it went straight
    /// back down. And nothing claimed a heap, so every spare hand went at once.
    /// </para>
    /// <para>
    /// Now a stack is approved, and picked up, only if a reachable storage has room for THAT
    /// good; and a heap somebody is already walking to is nobody else's errand.
    /// </para>
    /// </remarks>
    [Fact]
    public void AMixedHeapAtAFullDoorMovesWhatHasAShelfAndLeavesTheRest()
    {
        SimConfig config = VillageFixtures.Village;
        SimLoop loop = Loop(config);
        SimWorld world = loop.World;

        StoreBuilding granary = world.AnyStoreOf(StoreKind.Granary);
        granary.Store.Receive(Goods.Meat, granary.Store.FreeSpace);
        Assert.True(granary.Store.IsFull, "the fixture's granary did not fill");

        StoreBuilding warehouse = world.AnyStoreOf(StoreKind.Warehouse);
        Assert.True(warehouse.HasRoomFor(Goods.Leather), "the fixture's warehouse has no room for leather");
        Assert.False(world.NearestStorageWithRoomFor(granary.Tile, Goods.Fish) is not null, "somewhere still takes fish, so the trap cannot be posed");

        foreach (Household household in world.Households)
        {
            int wanted = world.TargetFoodFor(household);
            if (world.FoodIn(household.Stockpile) < wanted)
            {
                household.Stockpile.Add(Goods.Produce, wanted);
            }
        }

        Assert.True(world.SetStockLimit(Goods.Produce, 1).Allowed);
        foreach (JobKind kind in JobLimits.Kinds)
        {
            world.SetJobLimit(kind, 0);
        }

        // The mixed heap at the granary's door: fish nobody can shelve, leather the warehouse takes.
        world.SetDown(granary.Tile, Goods.Fish, 80);
        world.SetDown(granary.Tile, Goods.Leather, 30);

        int walkersAtOnceWorst = 0;
        int emptyHandedTurns = 0;
        for (int tick = 0; tick < config.TicksPerSeason; tick++)
        {
            foreach (Household household in world.Households)
            {
                int wanted = world.TargetFoodFor(household);
                if (world.FoodIn(household.Stockpile) < wanted)
                {
                    household.Stockpile.Add(Goods.Produce, wanted - world.FoodIn(household.Stockpile));
                }
            }

            loop.StepOnce();

            int walkers = 0;
            foreach (Villager villager in world.Villagers)
            {
                if (villager.State == VillagerState.TidyingGround
                    && villager.ErrandX == granary.Tile.X && villager.ErrandY == granary.Tile.Y)
                {
                    walkers++;
                }

                if (villager.Tile == granary.Tile && villager.State == VillagerState.TravelingHome && !villager.IsCarrying)
                {
                    emptyHandedTurns++;
                }
            }

            walkersAtOnceWorst = System.Math.Max(walkersAtOnceWorst, walkers);
        }

        _output.WriteLine(
            $"a season on: {world.GroundStackAt(granary.Tile, Goods.Fish)} fish and {world.GroundStackAt(granary.Tile, Goods.Leather)} leather "
            + $"at the door; warehouse holds {warehouse.Store[Goods.Leather]} leather; at most {walkersAtOnceWorst} walking to the heap at once; "
            + $"{emptyHandedTurns} empty-handed turns from the door");

        Assert.True(warehouse.Store[Goods.Leather] >= 30, $"the leather never reached the warehouse ({warehouse.Store[Goods.Leather]})");
        Assert.Equal(80, world.GroundStackAt(granary.Tile, Goods.Fish));
        Assert.True(walkersAtOnceWorst <= 1, $"{walkersAtOnceWorst} villagers were walking to the same heap at once");
        Assert.True(emptyHandedTurns == 0, $"{emptyHandedTurns} empty-handed turns from the granary's door — the fish is being picked up and put straight back down");
    }

    /// <summary>⭐ One villager goes for a heap, not everybody who is idle (D371).</summary>
    /// <remarks>
    /// A heap eight tiles out, four laborers with nothing to do: over the walk out at most one of
    /// them is on their way to it at any tick. Without the claim all four set off and three come
    /// home empty-handed — Joe's crowd at the granary, one building over.
    /// </remarks>
    [Fact]
    public void OneVillagerGoesForAHeap()
    {
        SimConfig config = VillageFixtures.Village;
        SimLoop loop = Loop(config);
        SimWorld world = loop.World;

        foreach (Household household in world.Households)
        {
            int wanted = world.TargetFoodFor(household);
            if (world.FoodIn(household.Stockpile) < wanted)
            {
                household.Stockpile.Add(Goods.Produce, wanted);
            }
        }

        Assert.True(world.SetStockLimit(Goods.Produce, 1).Allowed);
        foreach (JobKind kind in JobLimits.Kinds)
        {
            world.SetJobLimit(kind, 0);
        }

        // A heap of leather well away from the homes, with a warehouse that takes it.
        GridPos far = default;
        bool found = false;
        for (int dx = 8; dx <= 12 && !found; dx++)
        {
            var at = new GridPos(world.Map.FoundingSite.X + dx, world.Map.FoundingSite.Y);
            if (world.Map.Contains(at) && world.NearestStorageWithRoomFor(at, Goods.Leather) is not null)
            {
                far = at;
                found = true;
            }
        }

        Assert.True(found, "no far tile with a shelf for leather — the fixture cannot pose the walk");
        world.SetDown(far, Goods.Leather, 30);

        int worst = 0;
        int ticksWalked = 0;
        for (int tick = 0; tick < config.TicksPerSeason; tick++)
        {
            loop.StepOnce();
            int walkers = 0;
            foreach (Villager villager in world.Villagers)
            {
                if (villager.State == VillagerState.TidyingGround && villager.ErrandX == far.X && villager.ErrandY == far.Y)
                {
                    walkers++;
                }
            }

            ticksWalked += walkers;
            worst = System.Math.Max(worst, walkers);
        }

        _output.WriteLine($"{ticksWalked} villager-ticks walking to the heap, at most {worst} at once; {world.GroundStackAt(far, Goods.Leather)} leather left");
        Assert.True(ticksWalked > 0, "nobody ever set off for the heap");
        Assert.True(worst <= 1, $"{worst} villagers were walking to the same heap at once");
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
                // BEFORE that, and stopping here keeps it an honest one. ⚠️ Not asserted on
                // THIS tick (D385): the load that filled the store and the load set down beside
                // it can land in the same tick, and with every gather pooled in the granary it
                // fills in year three rather than never — the tick before is the last honest
                // reading, and it was asserted on the way here.
                _output.WriteLine(
                    $"a store filled at tick {tick}; {OnTheGround(world)} on the ground");
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
        StoreBuilding warehouse = world.AnyStoreOf(StoreKind.Warehouse);
        for (int i = 0; i < world.StoreBuildings.Count; i++)
        {
            Stockpile store = world.StoreBuildings[i].Store;
            store.Receive(Goods.Logs, store.FreeSpace);
        }

        Assert.All(world.StoreBuildings, store => Assert.True(store.Store.IsFull));

        // One villager, one armful, standing at the warehouse door with it.
        Villager carrier = world.Villagers[0];
        carrier.Position = warehouse.Position;
        carrier.Carried.TakeAll(Goods.Logs);
        carrier.Carried.Receive(Goods.Logs, 60);
        carrier.State = VillagerState.HaulingToStore;
        carrier.ActionTicksRemaining = 0;

        int held = warehouse.Store.Held;
        loop.StepOnce();

        _output.WriteLine(
            $"{carrier.Name} arrived at a full warehouse with 60 logs: {carrier.CarriedLogs} still "
            + $"carried, {OnTheGround(world)} on the ground, warehouse {held} -> {warehouse.Store.Held}");

        // ⭐ The load still exists. Before D96 this assertion read 0 = 0 + 0: Add took what
        // fitted (nothing) and ArriveAt zeroed the arms anyway.
        Assert.Equal(60, OnTheGround(world) + carrier.CarriedLogs);
        Assert.Equal(held, warehouse.Store.Held);
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

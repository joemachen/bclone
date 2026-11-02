using System.Reflection;
using System.Text.Json.Nodes;
using Bclone.Sim.Config;
using Bclone.Sim.Core;
using Bclone.Sim.Determinism;
using Bclone.Sim.Logging;
using Bclone.Sim.Persistence;
using Bclone.Sim.Systems;
using Bclone.Sim.World;
using Xunit;
using Xunit.Abstractions;

namespace Bclone.Sim.Tests;

/// <summary>
/// `specs/workshop-and-the-carpenter.md §9` — Slice W (D678): the Workshop, learned by felling wood, and the Carpenter
/// who makes handcarts from logs in the tailor's batch shape; racked where tools are, never at the market; stopped by a
/// `handcarts` limit or, with none set, at one a household and a spare. The cart doing anything is Slice H
/// (`handcart.md`), not here.
/// </summary>
public sealed class WorkshopTests
{
    private readonly ITestOutputHelper _output;

    public WorkshopTests(ITestOutputHelper output) => _output = output;

    private static SimConfig Config => VillageFixtures.Village;

    private static StoreBuilding TheWarehouse(SimWorld world) => world.AnyStoreOf(StoreKind.Warehouse);

    /// <summary>
    /// A standing workshop, the village lent the knowledge first (the tailor's `RaiseAShop` lends the leather; this
    /// lends the flag — the gate is on the player's `Mark`, §4.1a).
    /// </summary>
    internal static Workplace RaiseAWorkshop(SimWorld world)
    {
        world.WorkshopLearned = true;
        return FoodChainTests.RaiseA(world, BuildingKind.Workshop);
    }

    /// <summary>A workshop with logs to work and nobody but a carpenter at work.</summary>
    private static Workplace AWorkingWorkshop(SimWorld world, int logs = 200)
    {
        Workplace workshop = RaiseAWorkshop(world);
        ToolsTests.KeepTheVillageWarmAndFed(world);
        FoodChainTests.OnlyOneTradeWorks(world, JobKind.Carpenter);
        TheWarehouse(world).Store.Add(Goods.Logs, logs);
        return workshop;
    }

    private static int Carts(SimWorld world) => world.InStores(Goods.Handcart) + world.OnTheGround(Goods.Handcart);

    // ---------------------------------------------------------------
    //  §4.3 — the make
    // ---------------------------------------------------------------

    /// <summary>§9 guard 1: `logs_per_handcart` out of a store, one handcart into a store that takes them.</summary>
    [Fact]
    public void ACarpenterMakesAHandcartFromLogs()
    {
        SimConfig config = Config;
        SimLoop loop = SimFactory.CreatePhase0(config, new InMemoryLogSink());
        SimWorld world = loop.World;
        Workplace workshop = AWorkingWorkshop(world);
        int logsBefore = world.InStores(Goods.Logs);

        int makingTicks = 0;
        for (int tick = 0; tick < config.TicksPerSeason && world.HandcartsEverMade < 2; tick++)
        {
            loop.StepOnce();
            makingTicks += world.Villagers.Count(v => v.State == VillagerState.Making && v.Tile == workshop.Tile);
        }

        int logsSpent = logsBefore - world.InStores(Goods.Logs);
        _output.WriteLine($"{world.HandcartsEverMade} handcarts made from {logsSpent} logs; {Carts(world)} racked or set down; {makingTicks} villager-ticks at the bench.");

        Assert.True(world.HandcartsEverMade > 0, "A staffed workshop with logs in the warehouse made nothing.");
        Assert.Equal(world.HandcartsEverMade * config.LogsPerHandcart, logsSpent);

        // ⛔ The recipe, to the unit: a tool quickens a make and never makes a cart from nothing (D522's rule).
        Assert.Equal(world.HandcartsEverMade, Carts(world));
        Assert.True(makingTicks >= world.HandcartsEverMade, "The carts came without the carpenter being seen at the bench.");
    }

    /// <summary>§9 guard 2: a cart goes where tools go — the warehouse, the founders' cart, the pile — never the market or a granary (Joe, D672 call 1).</summary>
    [Fact]
    public void AHandcartIsRackedWhereToolsAreAndNeverAtTheMarket()
    {
        SimConfig config = Config;
        SimLoop loop = SimFactory.CreatePhase0(config, new InMemoryLogSink());
        SimWorld world = loop.World;
        Workplace workshop = AWorkingWorkshop(world);

        // The row, to the letter (§5).
        IReadOnlyList<StoreKind> storedBy = world.GoodsCatalog[Goods.Handcart].StoredBy;
        Assert.Equal(new[] { StoreKind.Warehouse, StoreKind.Cart, StoreKind.Pile }, storedBy);
        foreach (StoreBuilding store in world.StoreBuildings)
        {
            Assert.Equal(store.Kind is StoreKind.Warehouse or StoreKind.Cart or StoreKind.Pile, store.Accepts(Goods.Handcart));
        }

        // A market and a granary beside the workshop, the warehouse further off — the cart still walks to the warehouse.
        StoreBuilding market = RaiseAStoreBeside(world, BuildingKind.Market, workshop.Tile);
        StoreBuilding granary = RaiseAStoreBeside(world, BuildingKind.Granary, workshop.Tile);
        StoreBuilding warehouse = TheWarehouse(world);
        int toMarket = Distance(workshop.Tile, market.Tile);
        int toGranary = Distance(workshop.Tile, granary.Tile);
        int toWarehouse = Distance(workshop.Tile, warehouse.Tile);
        _output.WriteLine($"from the workshop: market {toMarket}, granary {toGranary}, warehouse {toWarehouse} tiles.");
        Assert.True(toMarket < toWarehouse && toGranary < toWarehouse, "The pose did not put the market and the granary nearer than the warehouse.");

        for (int tick = 0; tick < config.TicksPerSeason && world.HandcartsEverMade < 2; tick++)
        {
            loop.StepOnce();
        }

        Assert.True(world.HandcartsEverMade > 0, "Nothing was made, so nothing was racked.");
        Assert.Equal(0, market.Store[Goods.Handcart]);
        Assert.Equal(0, granary.Store[Goods.Handcart]);
        Assert.Equal(world.HandcartsEverMade, world.StoreBuildings.Where(s => s.Kind is StoreKind.Warehouse or StoreKind.Cart or StoreKind.Pile).Sum(s => s.Store[Goods.Handcart]) + world.OnTheGround(Goods.Handcart));
    }

    /// <summary>§9 guard 3: the player's ceiling (D62, D139) — a met handcarts limit stops the make, and the sentence names it.</summary>
    [Fact]
    public void AMetHandcartsLimitStopsTheWorkshop()
    {
        SimWorld world = SimFactory.CreatePhase0(Config, new InMemoryLogSink()).World;
        Workplace workshop = AWorkingWorkshop(world);

        Assert.True(world.SetStockLimit(Goods.Handcart, 2).Allowed);
        TheWarehouse(world).Store.Add(Goods.Handcart, 1);
        Assert.Null(world.WhyTheBatchWaits(workshop));

        TheWarehouse(world).Store.Add(Goods.Handcart, 1);
        string? waits = world.WhyTheBatchWaits(workshop);
        _output.WriteLine(waits);
        Assert.NotNull(waits);
        Assert.StartsWith("Nothing to make", waits, StringComparison.Ordinal);
        Assert.Contains("asked the village to keep 2 handcarts", waits, StringComparison.Ordinal);

        // And the quota reads the same stop (§4.5's stood-down switch, through its row's `limited_by`): the village wants
        // no carpenter and says why, where a tick before it wanted one. The seat is kept — a met limit stops the work,
        // not the trade (D238).
        Assert.Equal(0, LabourQuota.For(world).Needed(JobKind.Carpenter));
        string? none = LabourQuota.WhyTheVillageWantsNone(world, JobKind.Carpenter);
        _output.WriteLine(none);
        Assert.Equal("the handcarts limit of 2 is met", none);
        world.SetStockLimit(Goods.Handcart, null);
        Assert.True(LabourQuota.For(world).Needed(JobKind.Carpenter) > 0);
    }

    /// <summary>§9 guard 4: with no limit set he makes one a household and a spare, and the sentence says so (§4.4).</summary>
    [Fact]
    public void WithNoLimitSetTheWorkshopStopsAtOneAHouseholdAndASpare()
    {
        SimConfig config = Config;
        SimLoop loop = SimFactory.CreatePhase0(config, new InMemoryLogSink());
        SimWorld world = loop.World;
        Workplace workshop = AWorkingWorkshop(world, logs: 2_000);

        int households = world.Households.Count(h => h.HasHome && world.LivingMembersOf(h) > 0);
        int wanted = LabourQuota.HandcartsWanted(world);
        _output.WriteLine($"{households} housed households; the village wants {wanted} handcarts and {LabourQuota.HandcartSpare} spare.");
        Assert.True(households > 0, "No housed household, so the stop would be at the spare alone and prove nothing.");
        Assert.Equal(households, wanted);
        Assert.Equal(1, LabourQuota.HandcartSpare);
        Assert.Null(world.StockLimits.For(Goods.Handcart));

        // One a household on the shelf is not yet enough — the spare is owed; one more and the bench is still.
        TheWarehouse(world).Store.Add(Goods.Handcart, wanted);
        Assert.Null(world.WhyTheBatchWaits(workshop));
        TheWarehouse(world).Store.Add(Goods.Handcart, 1);
        string? waits = world.WhyTheBatchWaits(workshop);
        _output.WriteLine(waits);
        Assert.NotNull(waits);
        Assert.Contains("every household has a handcart", waits, StringComparison.Ordinal);
        Assert.Contains($"({wanted + 1} stored)", waits, StringComparison.Ordinal);
        TheWarehouse(world).Store.TakeAll(Goods.Handcart);

        // And lived: two seasons of making with logs to spare stops at exactly the derived number.
        for (int tick = 0; tick < config.TicksPerSeason * 2; tick++)
        {
            loop.StepOnce();
        }

        int stoppedAt = LabourQuota.HandcartsWanted(world) + LabourQuota.HandcartSpare;
        _output.WriteLine($"{world.HandcartsEverMade} made; the derived stop is now {stoppedAt}.");
        Assert.Equal(stoppedAt, world.HandcartsEverMade);
        Assert.Equal(stoppedAt, Carts(world));
    }

    /// <summary>§9 guard 5: households, not homes — a home whose family has died wants no cart (§4.4).</summary>
    [Fact]
    public void AnEmptyHouseWantsNoCart()
    {
        SimWorld world = SimFactory.CreatePhase0(Config, new InMemoryLogSink()).World;
        int before = LabourQuota.HandcartsWanted(world);
        Household gone = world.Households.First(h => h.HasHome && world.LivingMembersOf(h) > 0);
        foreach (Villager villager in world.Villagers.Where(v => v.HouseholdId == gone.Id))
        {
            villager.Alive = false;
        }

        Assert.True(gone.HasHome, "The dead family's home is still standing; the pose is a house with nobody in it.");
        Assert.Equal(before - 1, LabourQuota.HandcartsWanted(world));

        // A living family with no roof wants none either: a cart serves a larder, and a larder is under a roof.
        int housed = world.Households.Count(h => h.HasHome && world.LivingMembersOf(h) > 0);
        Assert.Equal(housed, LabourQuota.HandcartsWanted(world));
    }

    /// <summary>§9 guard 6: the input refusal names the workshop, the store and what a cart takes.</summary>
    [Fact]
    public void AWorkshopWithNoLogsInReachSaysWhichStore()
    {
        SimWorld world = SimFactory.CreatePhase0(Config, new InMemoryLogSink()).World;
        Workplace workshop = RaiseAWorkshop(world);
        ToolsTests.KeepTheVillageWarmAndFed(world);
        foreach (StoreBuilding store in world.StoreBuildings)
        {
            store.Store.TakeAll(Goods.Logs);
        }

        string? waits = world.WhyTheBatchWaits(workshop);
        _output.WriteLine(waits);
        Assert.NotNull(waits);
        Assert.StartsWith("Nothing to make", waits, StringComparison.Ordinal);
        Assert.Contains(workshop.Name, waits, StringComparison.Ordinal);
        Assert.Contains($"the {world.Config.LogsPerHandcart} logs a handcart takes", waits, StringComparison.Ordinal);

        // And the card reads the same sentence once somebody is seated (one copy, the forge's rule; an empty building
        // the village has no use for is not a fault, D147).
        workshop.WorkerIds.Add(world.Villagers.First(v => v.Alive && v.CanWork).Id);
        Assert.Contains(waits, world.IdleNote(workshop), StringComparison.Ordinal);
    }

    /// <summary>§9 guard 7: a saw-pit has no fire — a village short of firewood still makes carts (§4.3).</summary>
    [Fact]
    public void TheWorkshopNeverAsksAfterTheWintersFirewood()
    {
        SimWorld world = SimFactory.CreatePhase0(Config, new InMemoryLogSink()).World;
        Workplace workshop = RaiseAWorkshop(world);
        TheWarehouse(world).Store.Add(Goods.Logs, 100);
        foreach (StoreBuilding store in world.StoreBuildings)
        {
            store.Store.TakeAll(Goods.Firewood);
        }

        foreach (Household household in world.Households)
        {
            household.Stockpile.TakeAll(Goods.Firewood);
        }

        Assert.True(LabourQuota.FirewoodShortfall(world) > 0, "The pose left the village with its firewood.");
        Assert.Equal(0, world.BatchFor(JobKind.Carpenter)!.Value.Firing);
        Assert.Null(world.WhyTheBatchWaits(workshop));
    }

    // ---------------------------------------------------------------
    //  §4.4 — demand to hands
    // ---------------------------------------------------------------

    /// <summary>§9 guard 8: carpenters are wanted only while households lack carts, and only into a workshop that can make.</summary>
    [Fact]
    public void CarpentersAreWantedOnlyWhenHouseholdsLackCarts()
    {
        SimWorld world = SimFactory.CreatePhase0(Config, new InMemoryLogSink()).World;
        ToolsTests.KeepTheVillageWarmAndFed(world);

        // No workshop: nobody, however short of carts the village is.
        Assert.True(LabourQuota.HandcartsShortfall(world) > 0);
        Assert.Equal(0, LabourQuota.CarpentersWanted(world));

        Workplace workshop = RaiseAWorkshop(world);
        foreach (StoreBuilding store in world.StoreBuildings)
        {
            store.Store.TakeAll(Goods.Logs);
        }

        // A workshop with nothing to make wants nobody — the batch waits, and a seat there would sit idle.
        Assert.Equal(0, LabourQuota.CarpentersWanted(world));

        TheWarehouse(world).Store.Add(Goods.Logs, 100);
        int wanted = LabourQuota.CarpentersWanted(world);
        _output.WriteLine($"shortfall {LabourQuota.HandcartsShortfall(world)}, a carpenter makes {VillageEconomy.HandcartsMadePerYearAtWorst(world.Config)} a year: {wanted} wanted of {workshop.Capacity} seats.");
        Assert.True(wanted > 0, "Households without carts, logs on the shelf, and no carpenter wanted.");
        Assert.True(wanted <= workshop.Capacity);
        Assert.Equal(wanted, LabourQuota.For(world).Needed(JobKind.Carpenter));

        // One a household and the spare on the shelves: nobody.
        TheWarehouse(world).Store.Add(Goods.Handcart, LabourQuota.HandcartsWanted(world) + LabourQuota.HandcartSpare);
        Assert.Equal(0, LabourQuota.HandcartsShortfall(world));
        Assert.Equal(0, LabourQuota.CarpentersWanted(world));
    }

    /// <summary>§9 guard 9: the carpenter's tool wears a use a make begun (`tools-and-the-smith.md §3.3`).</summary>
    [Fact]
    public void TheCarpentersToolWearsAUseAMake()
    {
        SimConfig config = Config;
        SimLoop loop = SimFactory.CreatePhase0(config, new InMemoryLogSink());
        SimWorld world = loop.World;
        Workplace workshop = AWorkingWorkshop(world);
        Assert.True(world.JobsCatalog.UsesTool(JobKind.Carpenter));
        Assert.False(world.JobsCatalog.ToolWaitsForTheSmithy(JobKind.Carpenter));

        foreach (Villager villager in world.Villagers.Where(v => v.Alive && v.CanWork))
        {
            villager.ToolGood = Goods.Tools;
            villager.ToolUses = config.ToolUses;
        }

        for (int tick = 0; tick < config.TicksPerSeason && world.HandcartsEverMade < 3; tick++)
        {
            loop.StepOnce();
        }

        Villager? carpenter = world.Villagers.FirstOrDefault(v => v.Alive && v.WorkplaceId == workshop.Id);
        Assert.NotNull(carpenter);
        int begun = world.WorkActionsBegun[(int)JobKind.Carpenter];
        _output.WriteLine($"{carpenter!.Name}: {begun} makes begun, tool at {carpenter.ToolUses} of {config.ToolUses} uses.");
        Assert.True(begun > 0, "No make was ever begun, so nothing could wear.");
        Assert.Equal(config.ToolUses - begun, carpenter.ToolUses);
    }

    /// <summary>
    /// §9 guard 10 — §4.5's list, as a table: every switch the tailor joined, the carpenter joins. The behaviour arm
    /// (state → trade, `ErrandKind`) is proved by guard 1: without it the carpenter is recalled home every tick and
    /// nothing is made (D281's lesson).
    /// </summary>
    [Fact]
    public void TheCarpenterJoinsEveryBatchTradeSwitch()
    {
        SimConfig config = Config;
        SimLoop loop = SimFactory.CreatePhase0(config, new InMemoryLogSink());
        SimWorld world = loop.World;

        // `BatchFor`.
        Batch? batch = world.BatchFor(JobKind.Carpenter);
        Assert.NotNull(batch);
        Assert.Equal(Goods.Logs, batch!.Value.Input);
        Assert.Equal(Goods.Handcart, batch.Value.Output);
        Assert.Equal(1, batch.Value.OutputPerBatch);
        Assert.Equal("make", batch.Value.Verb);

        // `LabourAllocator.KindsInOrder`: after Tailor, before Quarrier (Joe, D675 call 4).
        FieldInfo order = typeof(LabourAllocator).GetField("KindsInOrder", BindingFlags.NonPublic | BindingFlags.Static)!;
        var kinds = (JobKind[])order.GetValue(null)!;
        int carpenter = Array.IndexOf(kinds, JobKind.Carpenter);
        Assert.True(carpenter > 0, "The allocator never claims a seat for a carpenter.");
        Assert.Equal(Array.IndexOf(kinds, JobKind.Tailor) + 1, carpenter);
        Assert.Equal(Array.IndexOf(kinds, JobKind.Quarrier) - 1, carpenter);

        // `KnowsOf`: handcarts are unknown until the workshop is (the standing-shop list).
        Assert.False(world.KnowsOf(Goods.Handcart), "Handcarts were known before the workshop.");
        world.WorkshopLearned = true;
        Assert.True(world.KnowsOf(Goods.Handcart));

        // The idle note's switch: a staffed workshop with nothing to make says so on its card.
        Workplace workshop = RaiseAWorkshop(world);
        ToolsTests.KeepTheVillageWarmAndFed(world);
        foreach (StoreBuilding store in world.StoreBuildings)
        {
            store.Store.TakeAll(Goods.Logs);
        }

        workshop.WorkerIds.Add(world.Villagers.First(v => v.Alive && v.CanWork).Id);
        Assert.Contains("Nothing to make", world.IdleNote(workshop), StringComparison.Ordinal);
        workshop.WorkerIds.Clear();

        // The villager's words (`DescribeState`), and the skill the stint grows (`SkillSystem`).
        Villager hand = world.Villagers.First(v => v.Alive && v.CanWork);
        hand.State = VillagerState.TravelingToWorkshop;
        Assert.Equal($"walking to {workshop.Name}", hand.DescribeState(workshop.Name));
        hand.State = VillagerState.Making;
        hand.BatchesThisStint = 1;
        Assert.Equal("making handcarts, the 2nd of the day", hand.DescribeState(workshop.Name));
        hand.BatchesThisStint = 0;
        hand.State = VillagerState.Idle;

        SkillRow carpentry = Assert.Single(config.Skills, s => s.GrownBy == JobKind.Carpenter);
        Assert.Equal("carpentry", carpentry.Name);
        FoodChainTests.OnlyOneTradeWorks(world, JobKind.Carpenter);
        TheWarehouse(world).Store.Add(Goods.Logs, 200);
        int atTheBench = 0;
        int onTheTrade = 0;
        for (int tick = 0; tick < config.TicksPerSeason && world.HandcartsEverMade < 2; tick++)
        {
            loop.StepOnce();
            atTheBench += world.Villagers.Count(v => v.Alive && v.State == VillagerState.Making);
            onTheTrade += world.Villagers.Count(v => v.Alive && v.State is VillagerState.Making or VillagerState.TravelingToWorkshop);
        }

        // Every tick on the trade grows the skill (the calendar fact, `SkillProgress.Ticks`) — and the ruling that a tick at
        // the bench or on the walk to it is a tick OUT ON THE WORK (D183: worth more than one spent waiting) is asserted by
        // name, because `TicksIn` cannot see it: a `Making => false` in `SkillSystem.OutOnTheWork` scored zero against the
        // ticks twice over (D678's red-check) and is a decision, not a count.
        Assert.True(atTheBench > 0, "Nobody was seen making at the bench, so the skill's growth proves nothing.");
        int grown = world.Villagers.Where(v => v.Alive).Sum(v => v.TicksIn(carpentry.Id));
        Assert.True(grown >= onTheTrade, $"{onTheTrade} ticks on the trade ({atTheBench} at the bench) grew only {grown} ticks of carpentry.");
        Assert.True(SkillSystem.OutOnTheWork(VillagerState.Making), "A tick at the bench is a tick out on the work.");
        Assert.True(SkillSystem.OutOnTheWork(VillagerState.TravelingToWorkshop), "The walk to the workshop is a tick out on the work.");
    }

    // ---------------------------------------------------------------
    //  §4.1a — the unlock
    // ---------------------------------------------------------------

    /// <summary>§9 guard 11: hidden and refused in a sentence until the logs are felled; learned at the number, once; the tree reads the same counter and knob.</summary>
    [Fact]
    public void TheWorkshopIsLearnedAtTheNumberOfLogsFelled()
    {
        // ⚠️ A knob that is not the default (7, not 2,500), so a mutant that reads a constant for the knob goes red.
        SimConfig config = Config with { WorkshopUnlockLogs = 7 };
        SimWorld world = SimFactory.CreatePhase0(config, new InMemoryLogSink()).World;

        string? before = world.WhyNotYet(BuildingKind.Workshop);
        _output.WriteLine(before);
        Assert.NotNull(before);
        Assert.Contains("felled 0 of the 7 logs", before, StringComparison.Ordinal);
        Assert.False(world.IsUnlocked(BuildingKind.Workshop));
        Assert.False(world.WorkshopLearned);

        // `Mark` refuses in the same sentence (one rule, read by the bar and the placement).
        GridPos site = world.Map.FoundingSite;
        PlacementVerdict refused = world.Mark(BuildingKind.Workshop, new GridPos(site.X + 3, site.Y + 3));
        Assert.False(refused.Allowed);
        Assert.Equal(before, refused.Reason);

        // One short, then one more: the crossing is `>=`, checked where the counter moves.
        world.Felled(6);
        Assert.Equal(6, world.LogsEverFelled);
        Assert.False(world.WorkshopLearned, "One log short and the workshop was known.");
        Assert.Contains("felled 6 of the 7 logs", world.WhyNotYet(BuildingKind.Workshop), StringComparison.Ordinal);

        world.Moments.Clear();
        world.Felled(1);
        Assert.True(world.WorkshopLearned);
        Assert.True(world.IsUnlocked(BuildingKind.Workshop));
        Moment learned = Assert.Single(world.Moments);
        _output.WriteLine($"{learned.Title}: {learned.Body}");
        Assert.Contains("workshop", learned.Body, StringComparison.Ordinal);
        Assert.Contains("felled 7 logs", learned.Body, StringComparison.Ordinal);
        Assert.True(learned.WaitsToBeDismissed);
        Assert.True(world.ShownTheTechTree, "A hands-on learning must introduce the tree (D440).");
        Assert.False(world.AFreeSmithyIsOwed);

        // Once learned, learned: the next fell raises no second moment.
        world.Felled(10);
        Assert.Single(world.Moments);

        // The tree reads the counter and the knob the gate reads (`tech-tree-map.md §3.3`).
        TechNodeRow node = Assert.Single(config.TechNodeRows, n => n.Unlocks == BuildingKind.Workshop);
        Assert.Equal(TechCondition.LogsFelled, node.Condition);
        Assert.Equal((17, 7), TechTree.ProgressOf(world, TechCondition.LogsFelled));
        Assert.Equal(TechState.Known, TechTree.StateOf(world, node).State);
        Assert.True(TechTree.IntroducesTheMap(TechCondition.LogsFelled));
    }

    /// <summary>The forester's fell and the brush's clearing both count toward the workshop — counted at the stump (D382).</summary>
    [Fact]
    public void AFellingCountsTowardTheWorkshop()
    {
        SimConfig config = Config with { WorkshopUnlockLogs = 1 };
        SimLoop loop = SimFactory.CreatePhase0(config, new InMemoryLogSink());
        SimWorld world = loop.World;
        Assert.False(world.WorkshopLearned);

        for (int tick = 0; tick < config.TicksPerYear && !world.WorkshopLearned; tick++)
        {
            loop.StepOnce();
        }

        _output.WriteLine($"{world.LogsEverFelled} logs felled when the workshop was learned, at {world.Clock}.");
        Assert.True(world.LogsEverFelled > 0, "A year of the village and no log felled.");
        Assert.True(world.WorkshopLearned, "A year of felling and the workshop never came, at a knob of one.");
    }

    /// <summary>
    /// §9 guard 11a: the learned flag is the village's identity and is hashed; the counter below the gate is not —
    /// ⚠️ the gap, asserted on purpose (§4.1a choice 2): two villages at 100 and 2,400 logs felled hash alike. Read
    /// §4.1a before "fixing" this.
    /// </summary>
    [Fact]
    public void ALearnedWorkshopIsInTheFingerprint()
    {
        SimWorld world = SimFactory.CreatePhase0(Config, new InMemoryLogSink()).World;
        ulong before = StateHash.Compute(world);

        world.LogsEverFelled = 100;
        Assert.Equal(before, StateHash.Compute(world));
        world.LogsEverFelled = 2_400;
        Assert.Equal(before, StateHash.Compute(world));
        world.HandcartsEverMade = 3;
        Assert.Equal(before, StateHash.Compute(world));

        world.WorkshopLearned = true;
        Assert.NotEqual(before, StateHash.Compute(world));
    }

    /// <summary>§9 guard 11a: a workshop left on handcarts mixes nothing; set to a second product, it does (`StateHash.cs`'s `ForgeGood` rule).</summary>
    [Fact]
    public void TheWorkshopsProductIsInTheFingerprintOnlyOffItsDefault()
    {
        SimWorld world = SimFactory.CreatePhase0(ConfigWithPlanks(), new InMemoryLogSink()).World;
        Workplace workshop = RaiseAWorkshop(world);
        Assert.Equal(Goods.Handcart, workshop.MakeGood);
        ulong before = StateHash.Compute(world);

        workshop.MakeGood = Goods.Handcart;
        Assert.Equal(before, StateHash.Compute(world));

        Assert.True(world.SetMakeGood(workshop, Planks).Allowed);
        Assert.Equal(Planks, workshop.MakeGood);
        Assert.NotEqual(before, StateHash.Compute(world));
    }

    /// <summary>
    /// §9 guard 11b: the gate is on the player's `Mark` — a standing workshop works whatever the flag says, as a village
    /// loaded under a lowered knob (§4.1a) or a fixture's workshop must.
    /// </summary>
    [Fact]
    public void AStandingWorkshopWorksWhateverTheFlagSays()
    {
        SimConfig config = Config;
        SimLoop loop = SimFactory.CreatePhase0(config, new InMemoryLogSink());
        SimWorld world = loop.World;
        Workplace workshop = AWorkingWorkshop(world);
        Assert.Equal(0UL, world.Tick);
        Assert.False(workshop.IsSite);

        world.WorkshopLearned = false;
        Assert.True(LabourQuota.CarpentersWanted(world) > 0);
        for (int tick = 0; tick < config.TicksPerSeason && world.HandcartsEverMade < 1; tick++)
        {
            loop.StepOnce();
        }

        Assert.True(world.HandcartsEverMade > 0, "A standing workshop stopped working for want of the flag.");
    }

    // ---------------------------------------------------------------
    //  § Rows, data and the hash
    // ---------------------------------------------------------------

    /// <summary>§9 guard 12: data and C# defaults agree (`data/sim.config.json`, §4, §5).</summary>
    [Fact]
    public void TheWorkshopsRowsAreAsShipped()
    {
        SimConfig shipped = ShippedConfig.Load();
        SimWorld world = SimFactory.CreatePhase0(shipped, new InMemoryLogSink()).World;

        Assert.Equal(25, shipped.WorkshopLogs);
        Assert.Equal(12, shipped.WorkshopStone);
        Assert.Equal(40, shipped.WorkshopWorkTicks);
        Assert.Equal(2, shipped.WorkshopCapacity);
        Assert.Equal(2_500, shipped.WorkshopUnlockLogs); // Joe's number (D676).
        Assert.Equal(10, shipped.LogsPerHandcart);
        Assert.Equal(20, shipped.MakeTicks);
        Assert.Equal(1, shipped.MakesPerStint);

        BuildingRow row = world.BuildingsCatalog[(int)BuildingKind.Workshop];
        Assert.Equal("workshop", row.Name);
        Assert.Equal(2, row.ExtentWidth);
        Assert.Equal(1, row.ExtentHeight);
        Assert.Equal(2, row.Seats);
        Assert.Equal(40, row.WorkTicks);
        Assert.Equal(new[] { new MaterialCost(Goods.Logs, 25), new MaterialCost(Goods.Stone, 12) }, row.Materials);
        Assert.Equal(27, (int)BuildingKind.Workshop);

        Assert.Equal(16, (int)JobKind.Carpenter);
        Assert.Equal(BuildingKind.Workshop, world.JobsCatalog.WorksAt(JobKind.Carpenter));
        Assert.Equal(JobKind.Carpenter, world.BuildingsCatalog.EmployedBy(BuildingKind.Workshop));
        Assert.Equal("carpenters", world.JobsCatalog.PluralOf(JobKind.Carpenter));
        Assert.True(world.JobsCatalog.UsesTool(JobKind.Carpenter));
        Assert.False(world.JobsCatalog.ToolWaitsForTheSmithy(JobKind.Carpenter));
        Assert.Equal(Goods.Handcart, world.JobsCatalog.LimitedBy(JobKind.Carpenter));

        Assert.Equal(15, (int)Goods.Handcart);
        GoodRow handcart = world.GoodsCatalog[Goods.Handcart];
        Assert.Equal("handcarts", handcart.Name);
        Assert.Equal(20, handcart.TradeWorth);
        Assert.Equal("the workshop", handcart.SourceName);
        Assert.Equal(new[] { StoreKind.Warehouse, StoreKind.Cart, StoreKind.Pile }, handcart.StoredBy);
        Assert.Equal(new[] { new MaterialCost(Goods.Logs, 10) }, handcart.MadeFrom);
        Assert.False(handcart.IsTool);
        for (int g = 0; g < world.GoodsCatalog.Count; g++)
        {
            if ((Goods)g != Goods.Handcart)
            {
                Assert.Empty(world.GoodsCatalog[(Goods)g].MadeFrom);
            }
        }

        Batch batch = world.BatchFor(JobKind.Carpenter)!.Value;
        Assert.Equal(10, batch.InputPerBatch);
        Assert.Equal(20, batch.Ticks);
        Assert.Equal(1, batch.PerStint);
        Assert.Equal(0, batch.Firing);

        // No shipped starting limit (Joe, D675 call 2; D660's reason).
        Assert.DoesNotContain("handcarts", shipped.StartingStockLimits.Keys);
        Assert.Null(world.StockLimits.For(Goods.Handcart));

        Assert.Contains(shipped.Skills, s => s.GrownBy == JobKind.Carpenter && s.Name == "carpentry" && s.Id == 10);
        Assert.Contains(shipped.TechNodeRows, n => n.Id == "workshop" && n.Unlocks == BuildingKind.Workshop && n.Condition == TechCondition.LogsFelled);
        Assert.Equal(0, VillageEconomy.SurvivalFloorFor(shipped, Goods.Handcart, 40, 10));
    }

    /// <summary>§9 guard 13: a bad row is refused at load, in words; a product with no recipe is refused on the card.</summary>
    [Fact]
    public void ABadWorkshopRowIsRefusedAtLoad()
    {
        // A recipe naming a good the catalogue does not have.
        SimConfigException noSuchGood = Assert.Throws<SimConfigException>(() => SimConfigLoader.Parse(PlanksJson("""[{ "goods": 99, "amount": 10 }]"""), "<modded>"));
        _output.WriteLine(noSuchGood.Message);
        Assert.Contains("made_from", noSuchGood.Message, StringComparison.Ordinal);

        // A recipe of nothing.
        SimConfigException nothing = Assert.Throws<SimConfigException>(() => SimConfigLoader.Parse(PlanksJson("""[{ "goods": "Logs", "amount": 0 }]"""), "<modded>"));
        Assert.Contains("made_from", nothing.Message, StringComparison.Ordinal);

        foreach ((string key, int value) in new[] { ("logs_per_handcart", 0), ("make_ticks", 0), ("makes_per_stint", 0), ("workshop_capacity", 0), ("workshop_unlock_logs", -1) })
        {
            SimConfigException refused = Assert.Throws<SimConfigException>(() => SimConfigLoader.Parse($$$"""{ "{{{key}}}": {{{value}}} }""", "<modded>"));
            _output.WriteLine(refused.Message);
            Assert.Contains(key, refused.Message, StringComparison.Ordinal);
        }

        // The card's setter refuses a good no workshop makes, and a building that is not a workshop.
        SimWorld world = SimFactory.CreatePhase0(Config, new InMemoryLogSink()).World;
        Workplace workshop = RaiseAWorkshop(world);
        PlacementVerdict notMade = world.SetMakeGood(workshop, Goods.Clothing);
        Assert.False(notMade.Allowed);
        Assert.Contains("workshop", notMade.Reason, StringComparison.Ordinal);
        Assert.False(world.SetMakeGood(workshop, Goods.Tools).Allowed);
        Assert.False(world.SetMakeGood(world.Workplaces.First(w => w.Kind != JobKind.Carpenter), Goods.Handcart).Allowed);
        Assert.Equal(Goods.Handcart, workshop.MakeGood);
        Assert.True(world.SetMakeGood(workshop, Goods.Handcart).Allowed);
    }

    /// <summary>
    /// §9 guard 14: a village that has not learned the workshop hashes as before — the three new mixes are silent
    /// (the flag false, the product at its default, the counter unhashed), and the goods list grew at the end. **Every
    /// shipped golden is the real guard**; this says why they hold.
    /// </summary>
    [Fact]
    public void AVillageThatHasNotLearnedTheWorkshopHashesAsBefore()
    {
        SimWorld world = SimFactory.CreatePhase0(ShippedConfig.Load(), new InMemoryLogSink()).World;
        Assert.False(world.WorkshopLearned);
        Assert.Equal((int)Goods.Handcart, world.GoodsCatalog.Count - 1);
        Assert.Null(world.StockLimits.For(Goods.Handcart));
        Assert.Equal(0, world.InStores(Goods.Handcart));
        Assert.All(world.Workplaces, w => Assert.Equal(Goods.Handcart, w.MakeGood));

        ulong before = StateHash.Compute(world);
        world.LogsEverFelled += 1_000;
        world.HandcartsEverMade += 1;
        Assert.Equal(before, StateHash.Compute(world));
    }

    /// <summary>§9 guard 15: `uses_tool` and `made_from` read from a modder's file (`ModdedJobTests` / `ModdedGoodTests`' shape).</summary>
    [Fact]
    public void UsesToolAndMadeFromRoundTripThroughJson()
    {
        SimConfig config = ConfigWithPlanks();
        var goods = new GoodsCatalog(config.GoodsCatalog);
        GoodRow planks = goods[Planks];
        Assert.Equal("planks", planks.Name);
        Assert.Equal(new[] { new MaterialCost(Goods.Logs, 3) }, planks.MadeFrom);
        Assert.Equal(new[] { new MaterialCost(Goods.Logs, 10) }, goods[Goods.Handcart].MadeFrom);

        string jobs = """
        {
          "jobs": [
            { "id": 0, "name": "forager",    "plural": "foragers",    "doing": "gathering",          "works_at": "GathererHut" },
            { "id": 1, "name": "forester",   "plural": "foresters",   "doing": "felling timber",     "works_at": "ForesterHut",   "limited_by": "Logs" },
            { "id": 2, "name": "woodcutter", "plural": "woodcutters", "doing": "splitting firewood", "works_at": "WoodcutterHut", "limited_by": "Firewood" },
            { "id": 3, "name": "marketer",   "plural": "traders",     "doing": "the market",         "works_at": "Market" },
            { "id": 4, "name": "builder",    "plural": "builders",    "doing": "building",           "works_at": "BuilderHut" },
            { "id": 5, "name": "farmer",     "plural": "farmers",     "doing": "farming",            "works_at": "Farmhouse",     "limited_by": "Produce" },
            { "id": 6, "name": "fisher",     "plural": "fishers",     "doing": "fishing",            "works_at": "FishingHut",    "limited_by": "Fish" },
            { "id": 7, "name": "hunter",     "plural": "hunters",     "doing": "hunting",            "works_at": "HunterLodge",   "limited_by": "Meat" },
            { "id": 8, "name": "smith",      "plural": "smiths",      "doing": "forging tools",      "works_at": "Smithy",        "limited_by": "Tools" },
            { "id": 9, "name": "quarrier",   "plural": "quarriers",   "doing": "cutting stone",      "works_at": "Quarry",        "limited_by": "Stone" },
            { "id": 10, "name": "miner",     "plural": "miners",      "doing": "digging iron",       "works_at": "Mine",          "limited_by": "Iron" },
            { "id": 11, "name": "miller",    "plural": "millers",     "doing": "grinding wheat",     "works_at": "Mill",          "limited_by": "Flour" },
            { "id": 12, "name": "baker",     "plural": "bakers",      "doing": "baking bread",       "works_at": "Bakery",        "limited_by": "Bread" },
            { "id": 13, "name": "herdsman",  "plural": "herdsmen",    "doing": "keeping the herd",   "works_at": "Barn",          "limited_by": "Hay" },
            { "id": 14, "name": "merchant",  "plural": "merchants",   "doing": "trading at the post", "works_at": "TradingPost" },
            { "id": 15, "name": "tailor",    "plural": "tailors",     "doing": "sewing coats",       "works_at": "TailorShop",    "limited_by": "Clothing" },
            { "id": 16, "name": "carpenter", "plural": "carpenters",  "doing": "making handcarts",   "works_at": "Workshop",      "limited_by": "Handcart", "uses_tool": true }
          ]
        }
        """;
        var catalog = new JobsCatalog(SimConfigLoader.Parse(jobs, "<modded>").JobsCatalog);
        Assert.True(catalog.UsesTool(JobKind.Carpenter));
        Assert.False(catalog.UsesTool(JobKind.Tailor));
        Assert.Equal(BuildingKind.Workshop, catalog.WorksAt(JobKind.Carpenter));
        Assert.Equal(Goods.Handcart, catalog.LimitedBy(JobKind.Carpenter));
    }

    // ---------------------------------------------------------------
    //  §7 — saved
    // ---------------------------------------------------------------

    /// <summary>§9 guard 16: a workshop and its carts survive the round trip to the same hash and the same village a year on; a format-9 save is refused in words (D510).</summary>
    [Fact]
    public void AWorkshopAndItsCartsSurviveTheRoundTripAndAnOlderSaveIsRefused()
    {
        SimConfig config = ConfigWithPlanks();
        SimLoop loop = SimFactory.CreatePhase0(config, new InMemoryLogSink());
        SimWorld world = loop.World;
        Workplace workshop = AWorkingWorkshop(world);
        Assert.True(world.SetMakeGood(workshop, Planks).Allowed);
        TheWarehouse(world).Store.Add(Goods.Handcart, 3);
        world.HandcartsEverMade = 3;
        loop.Step(config.TicksPerSeason);
        Assert.True(world.WorkshopLearned);

        Assert.Equal(10, SaveGame.Format);
        SimLoop loaded = Reload(loop);
        Assert.Equal(StateHash.Compute(world), StateHash.Compute(loaded.World));
        Workplace again = loaded.World.Workplaces.Single(w => w.Id == workshop.Id);
        Assert.Equal(Planks, again.MakeGood);
        Assert.True(loaded.World.WorkshopLearned);
        Assert.Equal(3, loaded.World.HandcartsEverMade);
        Assert.Equal(world.InStores(Goods.Handcart), loaded.World.InStores(Goods.Handcart));

        loop.Step(config.TicksPerYear);
        loaded.Step(config.TicksPerYear);
        Assert.Equal(StateHash.Compute(world), StateHash.Compute(loaded.World));

        // Joe's format-9 saves (D675 call 3: *"I don't mind starting a new game"*) — refused, naming the build.
        string folder = Path.Combine(Path.GetTempPath(), "bclone-workshop-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);
        try
        {
            JsonObject document = SaveGame.Capture(loop, "0.0.1", "2026-11-01 12:00", NewGame.ShareCode(
                config, NewGame.Defaults(config, world.Seed.ToString(System.Globalization.CultureInfo.InvariantCulture))));
            document["format"] = 9;
            string path = Path.Combine(folder, $"format-nine{SaveFile.Extension}");
            SaveFile.Write(path, document);
            SaveOpened opened = SaveFile.Open(path, config);
            Assert.Null(opened.Loop);
            Assert.Equal("Saved by build 0.0.1, whose saves this build cannot read.", opened.Refusal);
        }
        finally
        {
            Directory.Delete(folder, recursive: true);
        }
    }

    // ---------------------------------------------------------------
    //  Helpers
    // ---------------------------------------------------------------

    private static SimLoop Reload(SimLoop loop)
    {
        JsonObject document = SaveGame.Capture(loop, "test", "2026-11-01 12:00", "test");
        return SaveGame.Load(JsonNode.Parse(SaveFile.TextOf(document))!.AsObject(), loop.World.Config, null);
    }

    /// <summary>A modder's second workshop product — planks, three logs each — above every built-in good.</summary>
    private static Goods Planks => (Goods)16;

    private static SimConfig ConfigWithPlanks() => SimConfigLoader.Parse(PlanksJson("""[{ "goods": "Logs", "amount": 3 }]"""), "<modded>");

    private static string PlanksJson(string madeFrom) => $$$"""
    {
      "goods": [
        { "id": 0, "name": "forage", "category": "Food",     "stored_by": ["Granary", "Market", "Cart"] },
        { "id": 1, "name": "logs", "category": "Materials",     "source_name": "woodland",     "yield_per_tile": 12, "stored_by": ["Warehouse", "Pile"] },
        { "id": 2, "name": "firewood", "category": "FuelAndGoods", "stored_by": ["Warehouse", "Market", "Cart", "Pile"] },
        { "id": 3, "name": "stone", "category": "Materials",    "source_name": "a stone seam", "yield_per_tile": 12, "stored_by": ["Warehouse", "Cart", "Pile"], "face_dig_ticks": 4, "face_per_dig": 10, "face_digs_per_stint": 4 },
        { "id": 4, "name": "stone tools", "category": "FuelAndGoods", "stored_by": ["Warehouse", "Cart", "Pile"],
          "tool_uses": 150, "tool_speed_bonus_percent": 34, "tool_yield_bonus_percent": 25,
          "forged_from": [{ "goods": "Stone", "amount": 2 }, { "goods": "Logs", "amount": 1 }] },
        { "id": 5, "name": "iron", "category": "Materials",     "source_name": "an iron seam", "yield_per_tile": 8,  "stored_by": ["Warehouse", "Cart", "Pile"], "face_dig_ticks": 6, "face_per_dig": 5, "face_digs_per_stint": 8 },
        { "id": 6, "name": "fish", "category": "Food",       "source_name": "the river",    "nutrition": 1, "stored_by": ["Granary", "Market", "Cart"] },
        { "id": 7, "name": "meat", "category": "Food",       "source_name": "the woods",    "nutrition": 1, "stored_by": ["Granary", "Market", "Cart"] },
        { "id": 8, "name": "leather", "category": "Materials",    "source_name": "the woods",    "stored_by": ["Warehouse", "Cart", "Pile"] },
        { "id": 9, "name": "wheat", "category": "Food",    "source_name": "the fields",   "nutrition": 1, "stored_by": ["Granary", "Market", "Cart"] },
        { "id": 10, "name": "iron tools", "category": "FuelAndGoods", "stored_by": ["Warehouse", "Cart", "Pile"],
          "tool_uses": 250, "tool_speed_bonus_percent": 50, "tool_yield_bonus_percent": 35,
          "forged_from": [{ "goods": "Iron", "amount": 4 }, { "goods": "Firewood", "amount": 4 }] },
        { "id": 11, "name": "flour", "category": "Materials",   "stored_by": ["Warehouse", "Cart"] },
        { "id": 12, "name": "bread", "category": "Food",        "nutrition": 2, "stored_by": ["Granary", "Market", "Cart"] },
        { "id": 13, "name": "hay",   "category": "Materials",   "stored_by": ["Barn"] },
        { "id": 14, "name": "clothing", "category": "Materials", "coat_winters": 3, "stored_by": ["Warehouse", "Cart", "Pile"] },
        { "id": 15, "name": "handcarts", "category": "Materials", "trade_worth": 20, "source_name": "the workshop", "stored_by": ["Warehouse", "Cart", "Pile"],
          "made_from": [{ "goods": "Logs", "amount": 10 }] },
        { "id": 16, "name": "planks", "category": "Materials", "trade_worth": 4, "source_name": "the workshop", "stored_by": ["Warehouse", "Pile"],
          "made_from": {{{madeFrom}}} }
      ]
    }
    """;

    /// <summary>A store raised on the first buildable tile out from <paramref name="near"/>, stocked and finished at once.</summary>
    private static StoreBuilding RaiseAStoreBeside(SimWorld world, BuildingKind kind, GridPos near)
    {
        GridPos? at = null;
        for (int radius = 1; radius < 20 && at is null; radius++)
        {
            for (int dy = -radius; dy <= radius && at is null; dy++)
            {
                for (int dx = -radius; dx <= radius && at is null; dx++)
                {
                    var tile = new GridPos(near.X + dx, near.Y + dy);
                    if (world.CanBuildAt(kind, tile).Allowed)
                    {
                        at = tile;
                    }
                }
            }
        }

        Assert.NotNull(at);
        Assert.True(world.Mark(kind, at!.Value).Allowed);
        Workplace plan = world.Workplaces.Last(w => w.Construction?.Kind == kind);
        BuildFixtures.StockTheSite(plan);
        for (int i = 0; i <= plan.Construction!.Recipe.WorkTicks; i++)
        {
            plan.Construction.Work();
        }

        world.Complete(plan);
        return world.StoreBuildings.Last(s => s.Kind == world.BuildingsCatalog[(int)kind].Stores);
    }

    private static int Distance(GridPos a, GridPos b) => Math.Abs(a.X - b.X) + Math.Abs(a.Y - b.Y);
}

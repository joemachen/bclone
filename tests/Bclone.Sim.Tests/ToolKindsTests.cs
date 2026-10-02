using Bclone.Sim.Config;
using Bclone.Sim.Core;
using Bclone.Sim.Determinism;
using Bclone.Sim.Logging;
using Bclone.Sim.World;
using Xunit;
using Xunit.Abstractions;

namespace Bclone.Sim.Tests;

/// <summary>
/// Stone tools and iron tools, and the smith who forges whichever the card says —
/// `specs/tools-and-the-smith.md §9.2–§9.3` (D434, built in D446).
/// </summary>
/// <remarks>
/// <para>
/// <b>The claims:</b> a tool's numbers are its good's row, and the stone row is today's tool to the
/// unit (so a village with only stone tools plays and hashes as before — every golden is the
/// broader check); an iron tool is quicker, richer and lasts longer; a hand with empty hands takes
/// the best kind in reach and a hand holding one does not trade it in; which kind is in a hand and
/// which kind a smithy forges are in the fingerprint; the smith forges what the card says — stone
/// from stone and a log with no fire, iron from iron and firewood — and the quota counts both.
/// </para>
/// </remarks>
public sealed class ToolKindsTests
{
    private readonly ITestOutputHelper _output;

    public ToolKindsTests(ITestOutputHelper output) => _output = output;

    private static SimConfig Config => VillageFixtures.Village;

    /// <summary>The fixture with no founder arriving skilled, so a hand is a novice at every trade.</summary>
    private static SimConfig NoMasters => Config with { FoundingMasters = 0, FoundingJourneymen = 0 };

    // ---------------------------------------------------------------
    //  § The rows
    // ---------------------------------------------------------------

    /// <summary>
    /// ⭐ The founders' cart carries STONE tools, and a stone tool is today's tool to the unit — its
    /// row is priced from the three keys every tool was priced from before iron (§9.2).
    /// </summary>
    [Fact]
    public void TheFoundersCartCarriesStoneToolsAtTodaysNumbers()
    {
        foreach (SimConfig config in new[] { Config, ShippedConfig.Load(), Config with { ToolUses = 77, ToolSpeedBonusPercent = 11, ToolYieldBonusPercent = 13 } })
        {
            SimWorld world = SimFactory.CreatePhase0(config, new InMemoryLogSink()).World;
            GoodRow stone = world.GoodsCatalog[Goods.Tools];

            Assert.Equal("stone tools", stone.Name);
            Assert.Equal(config.ToolUses, stone.ToolUses);
            Assert.Equal(config.ToolSpeedBonusPercent, stone.ToolSpeedBonusPercent);
            Assert.Equal(config.ToolYieldBonusPercent, stone.ToolYieldBonusPercent);
            Assert.Equal(config.CartTools, world.InStores(Goods.Tools));
            Assert.Equal(0, world.InStores(Goods.IronTools));
        }
    }

    /// <summary>The iron row's numbers and both recipes, in the data and the C# defaults alike (Joe, D434).</summary>
    [Fact]
    public void TheIronToolAndBothRecipesAreAsShipped()
    {
        SimConfig shipped = ShippedConfig.Load();
        var fresh = new SimConfig();
        foreach (SimConfig config in new[] { shipped, fresh })
        {
            var goods = new GoodsCatalog(config.GoodsCatalog);
            GoodRow iron = goods[Goods.IronTools];
            Assert.Equal("iron tools", iron.Name);
            Assert.Equal((250, 50, 35), (iron.ToolUses, iron.ToolSpeedBonusPercent, iron.ToolYieldBonusPercent));
            Assert.Equal(
                new[] { new MaterialCost(Goods.Iron, 4), new MaterialCost(Goods.Firewood, 4) },
                iron.ForgedFrom);
            Assert.Equal(
                new[] { new MaterialCost(Goods.Stone, 2), new MaterialCost(Goods.Logs, 1) },
                goods[Goods.Tools].ForgedFrom);
            Assert.Equal(new[] { Goods.IronTools, Goods.Tools }, goods.ToolsBestFirst);
        }

        // Each kind has its own starting limit, under its own name — 25 each (Joe, D447: "its going
        // to take a few years to have more than 25 people who need tools at once").
        Assert.Equal(25, shipped.StartingStockLimits["stone tools"]);
        Assert.Equal(25, shipped.StartingStockLimits["iron tools"]);
    }

    /// <summary>A tool's numbers are validated as a row's: a bonus over 100, or a recipe on a good nobody can hold, is refused at load.</summary>
    [Fact]
    public void ABadToolRowIsRefusedAtLoad()
    {
        SimConfig config = Config;
        GoodRow[] rows = config.GoodsCatalog.ToArray();
        rows[(int)Goods.IronTools] = rows[(int)Goods.IronTools] with { ToolSpeedBonusPercent = 101 };
        Assert.Throws<SimConfigException>(() => (config with { GoodsList = rows }).Validate());

        rows = config.GoodsCatalog.ToArray();
        rows[(int)Goods.Leather] = rows[(int)Goods.Leather] with { ForgedFrom = new[] { new MaterialCost(Goods.Iron, 1) } };
        SimConfigException error = Assert.Throws<SimConfigException>(() => (config with { GoodsList = rows }).Validate());
        Assert.Contains("forged_from", error.Message, StringComparison.Ordinal);

        Assert.Throws<SimConfigException>(() => (config with { StonePerStoneTool = 0, LogsPerStoneTool = 0 }).Validate());
    }

    // ---------------------------------------------------------------
    //  § Iron in the hand
    // ---------------------------------------------------------------

    /// <summary>
    /// ⭐ An iron tool takes half off the action it begins and brings in 35 % more (§9.2) — and a
    /// stone tool beside it is today's 34 % and 25 %.
    /// </summary>
    /// <remarks>
    /// At 50 % a three-tick gather still goes 3 → 2 (a whole tick is all three ticks can give), so
    /// iron's ticks bite hardest on the long actions. Red with the seams reading the stone row
    /// whatever is in the hand.
    /// </remarks>
    [Fact]
    public void AnIronToolIsQuickerAndRicherThanAStoneOne()
    {
        SimWorld world = SimFactory.CreatePhase0(NoMasters, new InMemoryLogSink()).World;
        Villager hand = world.Villagers.First(v => v.Alive && v.CanWork);
        Assert.Empty(hand.Skills);

        (JobKind Trade, int Base, int Stone, int Iron)[] table =
        {
            (JobKind.Forager, 3, 2, 2),
            (JobKind.Woodcutter, 4, 3, 2),
            (JobKind.Fisher, 10, 7, 5),
            (JobKind.Hunter, 15, 10, 8),
        };

        hand.ToolUses = 10;
        foreach ((JobKind trade, int baseTicks, int stone, int iron) in table)
        {
            hand.ToolGood = Goods.Tools;
            int withStone = world.WorkTicksFor(hand, trade, baseTicks);
            hand.ToolGood = Goods.IronTools;
            int withIron = world.WorkTicksFor(hand, trade, baseTicks);
            _output.WriteLine($"{trade,-11} {baseTicks,2} ticks: stone {withStone,2}, iron {withIron,2}");

            Assert.Equal(stone, withStone);
            Assert.Equal(iron, withIron);
        }

        int withTechnique = world.YieldWithTechnique(JobKind.Forager, 123);
        hand.ToolGood = Goods.Tools;
        Assert.Equal(withTechnique + (withTechnique * 25 / 100), world.YieldFor(hand, JobKind.Forager, 123));
        hand.ToolGood = Goods.IronTools;
        Assert.Equal(withTechnique + (withTechnique * 35 / 100), world.YieldFor(hand, JobKind.Forager, 123));

        // And it wears like any tool: one use an action begun.
        world.BeginWork(hand, JobKind.Hunter, 15);
        Assert.Equal(9, hand.ToolUses);
    }

    /// <summary>
    /// ⭐ A hand with empty hands takes iron before stone — <b>even when the stone is nearer</b> —
    /// and an iron tool lasts its own uses (§9.2). Red with the fetch looking for stone first.
    /// </summary>
    /// <remarks>
    /// ⚠️ <b>Posed with the iron in a store of its own, further out</b>, and that is the pose that
    /// bites: with both kinds on one shelf a hand sent for stone takes iron on arrival (the take is
    /// best-first too), so the first draft scored ZERO against the fetch looking for stone first.
    /// </remarks>
    [Fact]
    public void AHandTakesIronBeforeStone()
    {
        SimConfig config = Config;
        SimLoop loop = SimFactory.CreatePhase0(config, new InMemoryLogSink());
        SimWorld world = loop.World;
        StoreBuilding warehouse = TheWarehouse(world);
        Assert.True(warehouse.Store[Goods.Tools] > 0, "The premise: the founders' stone tools are in the warehouse.");

        int piles = world.StoreBuildings.Count;
        ColdStartTests.MarkSomewhereNear(world, BuildingKind.Pile, new GridPos(warehouse.Tile.X + 10, warehouse.Tile.Y), 4);
        StoreBuilding pile = world.StoreBuildings.Single(s => s.Kind == StoreKind.Pile && world.StoreBuildings.IndexOf(s) >= piles);
        Assert.True(
            world.TravelCost.Cost(world.Map.FoundingSite, pile.Tile) > world.TravelCost.Cost(world.Map.FoundingSite, warehouse.Tile),
            "The premise: the iron is further out than the stone.");
        pile.Store.Add(Goods.IronTools, 3);

        loop.Step(config.TicksPerSeason * 2);

        Villager[] withIron = world.Villagers.Where(v => v.Alive && v.ToolUses > 0 && v.ToolGood == Goods.IronTools).ToArray();
        int withStone = world.Villagers.Count(v => v.Alive && v.ToolUses > 0 && v.ToolGood == Goods.Tools);
        _output.WriteLine($"{withIron.Length} hands hold iron, {withStone} stone; {world.InStores(Goods.IronTools)} iron and {world.InStores(Goods.Tools)} stone on the shelves");

        Assert.Equal(0, world.InStores(Goods.IronTools) + world.OnTheGround(Goods.IronTools));
        Assert.Equal(3, withIron.Length);
        Assert.All(withIron, v => Assert.True(
            v.ToolUses > config.ToolUses,
            $"{v.Name} holds iron with {v.ToolUses} uses — no more than a stone tool's {config.ToolUses}."));
    }

    /// <summary>
    /// At a shelf holding both kinds, the hand takes iron (§9.2: the take is best-first, as the
    /// fetch is). Red with the take reading the kinds worst-first.
    /// </summary>
    [Fact]
    public void AtAShelfOfBothKindsTheHandTakesIron()
    {
        SimConfig config = Config;
        SimLoop loop = SimFactory.CreatePhase0(config, new InMemoryLogSink());
        SimWorld world = loop.World;
        StoreBuilding warehouse = TheWarehouse(world);
        Assert.True(warehouse.Store[Goods.Tools] > 3, "The premise: the founders' stone tools are on the same shelf.");
        warehouse.Store.Add(Goods.IronTools, 3);

        loop.Step(config.TicksPerSeason * 2);

        int withIron = world.Villagers.Count(v => v.Alive && v.ToolUses > 0 && v.ToolGood == Goods.IronTools);
        _output.WriteLine($"{withIron} hands hold iron; {warehouse.Store[Goods.IronTools]} iron and {warehouse.Store[Goods.Tools]} stone left on the shelf");
        Assert.Equal(0, warehouse.Store[Goods.IronTools]);
        Assert.Equal(3, withIron);
    }

    /// <summary>
    /// A hand holding a stone tool does not trade it in for iron (§9.2: a tool is fetched when the
    /// hands are empty). Red with the fetch asked of a full hand.
    /// </summary>
    [Fact]
    public void AHandHoldingAStoneToolDoesNotTradeItIn()
    {
        SimConfig config = Config with { ToolUses = 100_000 };
        SimLoop loop = SimFactory.CreatePhase0(config, new InMemoryLogSink());
        SimWorld world = loop.World;

        // Everyone already equipped with stone, then iron arrives on the shelf.
        loop.Step(config.TicksPerSeason * 2);
        int holdersBefore = world.Villagers.Count(v => v.Alive && v.ToolUses > 0);
        Assert.True(holdersBefore > 0, "The premise: hands hold the founders' stone tools.");
        TheWarehouse(world).Store.Add(Goods.IronTools, 3);

        loop.Step(config.TicksPerSeason);

        int ironTaken = 3 - world.InStores(Goods.IronTools);
        int newHands = world.Villagers.Count(v => v.Alive && v.ToolUses > 0) - holdersBefore;
        _output.WriteLine($"{holdersBefore} held stone; {ironTaken} iron taken since, {newHands} newly equipped hands");
        Assert.True(ironTaken <= Math.Max(0, newHands), "A hand holding a stone tool traded it for iron.");
    }

    /// <summary>
    /// Which kind is in a hand is in the fingerprint — and only as a difference from stone, so the
    /// founders' tools mix nothing new (§9.2). Red with the kind left out of the hash.
    /// </summary>
    [Fact]
    public void TheKindInHandIsInTheFingerprint()
    {
        SimWorld a = SimFactory.CreatePhase0(Config, new InMemoryLogSink()).World;
        SimWorld b = SimFactory.CreatePhase0(Config, new InMemoryLogSink()).World;
        Villager inA = a.Villagers.First(v => v.Alive && v.CanWork);
        Villager inB = b.Villagers.Single(v => v.Id == inA.Id);

        // No tool in the hand: the kind is meaningless and mixes nothing.
        inA.ToolGood = Goods.IronTools;
        Assert.Equal(StateHash.Compute(b), StateHash.Compute(a));

        inA.ToolUses = 10;
        inB.ToolUses = 10;
        Assert.NotEqual(StateHash.Compute(b), StateHash.Compute(a));

        inA.ToolGood = Goods.Tools;
        Assert.Equal(StateHash.Compute(b), StateHash.Compute(a));
    }

    // ---------------------------------------------------------------
    //  § The forge
    // ---------------------------------------------------------------

    /// <summary>
    /// ⭐ The smith forges what the card says — stone by default, from two stone and a log a tool
    /// (Joe, D434); iron once it is set, from iron and firewood. Red with the forge making stone
    /// tools whatever the card says, and with the recipe's inputs left untaken.
    /// </summary>
    [Fact]
    public void TheSmithForgesWhatTheCardSays()
    {
        SimConfig config = Config;
        SimLoop loop = SimFactory.CreatePhase0(config, new InMemoryLogSink());
        SimWorld world = loop.World;

        Workplace smithy = ToolsTests.RaiseASmithy(world);
        Assert.Equal(Goods.Tools, smithy.ForgeGood);
        StoreBuilding warehouse = TheWarehouse(world);
        ToolsTests.KeepTheVillageWarmAndFed(world);
        ToolsTests.OnlyASmithWorks(world);
        warehouse.Store.Add(Goods.Stone, 40);
        warehouse.Store.Add(Goods.Logs, 40);
        int stoneBefore = warehouse.Store[Goods.Stone];
        int logsBefore = warehouse.Store[Goods.Logs];

        for (int tick = 0; tick < config.TicksPerSeason * 2 && world.ToolsEverForged < 4; tick++)
        {
            loop.StepOnce();
        }

        int forged = world.ToolsEverForged;
        _output.WriteLine($"stone: {forged} forged; {stoneBefore - warehouse.Store[Goods.Stone]} stone and {logsBefore - warehouse.Store[Goods.Logs]} logs spent");
        Assert.True(forged > 0, "A smithy left on stone, with stone and logs in its store, forged nothing.");
        Assert.Equal(forged * config.StonePerStoneTool, stoneBefore - warehouse.Store[Goods.Stone]);
        Assert.Equal(forged * config.LogsPerStoneTool, logsBefore - warehouse.Store[Goods.Logs]);
        Assert.Equal(0, world.InStores(Goods.IronTools));

        // Set to iron, it forges iron and touches no more stone.
        Assert.True(world.SetForgeGood(smithy, Goods.IronTools).Allowed);
        warehouse.Store.Add(Goods.Iron, 40);
        int ironBefore = warehouse.Store[Goods.Iron];
        int stoneAfterStone = warehouse.Store[Goods.Stone];
        int ironToolsBefore = world.InStores(Goods.IronTools);
        int forgedBefore = world.ToolsEverForged;
        for (int tick = 0; tick < config.TicksPerSeason * 2 && world.ToolsEverForged < forgedBefore + 4; tick++)
        {
            loop.StepOnce();
        }

        int ironForged = world.ToolsEverForged - forgedBefore;
        _output.WriteLine($"iron: {ironForged} forged from {ironBefore - warehouse.Store[Goods.Iron]} iron");
        Assert.True(ironForged > 0, "A smithy set to iron, with iron and firewood in its store, forged nothing.");
        Assert.Equal(ironForged * config.IronPerTool, ironBefore - warehouse.Store[Goods.Iron]);
        Assert.Equal(stoneAfterStone, warehouse.Store[Goods.Stone]);
        Assert.True(world.InStores(Goods.IronTools) + world.OnTheGround(Goods.IronTools) > ironToolsBefore
            || world.Villagers.Any(v => v.Alive && v.ToolGood == Goods.IronTools && v.ToolUses > 0));
    }

    /// <summary>
    /// A smith set to a kind it has no stock for says which goods are short, and does not switch to
    /// the kind it could make (§9.3). Red with the refusal naming the stone recipe.
    /// </summary>
    [Fact]
    public void ASmithWithNothingForTheChosenKindSaysWhatIsShort()
    {
        SimConfig config = Config;
        SimLoop loop = SimFactory.CreatePhase0(config, new InMemoryLogSink());
        SimWorld world = loop.World;

        Workplace smithy = ToolsTests.RaiseASmithy(world);
        ToolsTests.KeepTheVillageWarmAndFed(world);
        ToolsTests.OnlyASmithWorks(world);
        TheWarehouse(world).Store.Add(Goods.Stone, 40);
        TheWarehouse(world).Store.Add(Goods.Logs, 40);
        Assert.True(world.SetForgeGood(smithy, Goods.IronTools).Allowed);

        string? cold = world.WhyTheForgeIsCold(smithy);
        _output.WriteLine(cold ?? "(no reason)");
        Assert.Equal(
            $"Nothing to forge — no store within reach of {smithy.Name} has the 4 iron and 4 firewood a forge of iron tools takes.",
            cold);

        loop.Step(config.TicksPerSeason);
        Assert.Equal(0, world.ToolsEverForged);
        Assert.Equal(Goods.IronTools, smithy.ForgeGood);
    }

    /// <summary>
    /// ⭐ A stone tool takes no fire, so its forge never waits on the winter's firewood (§9.3) — the
    /// guard that stops an iron forge (§3.7) is asked only of a recipe with firewood in it. Red with
    /// the firewood guard asked of every recipe.
    /// </summary>
    [Fact]
    public void AStoneForgeDoesNotWaitOnTheWintersFirewood()
    {
        SimConfig config = Config;
        SimLoop loop = SimFactory.CreatePhase0(config, new InMemoryLogSink());
        SimWorld world = loop.World;

        Workplace smithy = ToolsTests.RaiseASmithy(world);
        StoreBuilding warehouse = TheWarehouse(world);
        ToolsTests.OnlyASmithWorks(world);
        foreach (Household household in world.Households)
        {
            household.Stockpile.Add(Goods.Produce, world.TargetFoodFor(household));
        }

        warehouse.Store.Add(Goods.Stone, 40);
        warehouse.Store.Add(Goods.Logs, 40);
        Assert.True(LabourQuota.FirewoodShortfall(world) > 0, "The premise: the homes are still owed firewood.");
        Assert.Null(world.WhyTheForgeIsCold(smithy));

        Assert.True(world.SetForgeGood(smithy, Goods.IronTools).Allowed);
        warehouse.Store.Add(Goods.Iron, 40);
        Assert.Equal("Nothing to forge — the village needs its firewood for the winter.", world.WhyTheForgeIsCold(smithy));
    }

    /// <summary>
    /// The card can only ask a smithy for something a forge makes, and only of a smithy; and what it
    /// asked is in the fingerprint, silent while it is stone (§9.3). Red with the forge kind unhashed.
    /// </summary>
    [Fact]
    public void WhatASmithyForgesIsTheCardsAndInTheFingerprint()
    {
        SimWorld world = SimFactory.CreatePhase0(Config, new InMemoryLogSink()).World;
        Workplace smithy = ToolsTests.RaiseASmithy(world);
        ulong onStone = StateHash.Compute(world);

        Assert.False(world.SetForgeGood(smithy, Goods.Stone).Allowed);
        Assert.False(world.SetForgeGood(smithy, Goods.Leather).Allowed);
        Assert.False(world.SetForgeGood(world.Workplaces.First(w => w.Kind != JobKind.Smith), Goods.IronTools).Allowed);
        Assert.Equal(Goods.Tools, smithy.ForgeGood);
        Assert.Equal(onStone, StateHash.Compute(world));

        Assert.True(world.SetForgeGood(smithy, Goods.IronTools).Allowed);
        Assert.NotEqual(onStone, StateHash.Compute(world));

        Assert.True(world.SetForgeGood(smithy, Goods.Tools).Allowed);
        Assert.Equal(onStone, StateHash.Compute(world));
    }

    /// <summary>A met limit on the chosen kind stops the forge, and says which kind (D139, §9.3).</summary>
    [Fact]
    public void AMetLimitOnTheChosenKindStopsTheForge()
    {
        SimConfig config = Config;
        SimLoop loop = SimFactory.CreatePhase0(config, new InMemoryLogSink());
        SimWorld world = loop.World;

        Workplace smithy = ToolsTests.RaiseASmithy(world);
        ToolsTests.KeepTheVillageWarmAndFed(world);
        ToolsTests.OnlyASmithWorks(world);
        TheWarehouse(world).Store.Add(Goods.Stone, 40);
        TheWarehouse(world).Store.Add(Goods.Logs, 40);

        // The founders' twenty stone tools against a stone limit of 1.
        Assert.True(world.SetStockLimit(Goods.Tools, 1).Allowed);
        Assert.Contains("keep 1 stone tools", world.WhyTheForgeIsCold(smithy), StringComparison.Ordinal);

        // An iron limit says nothing about stone: on iron with no iron, the reason is the iron.
        Assert.True(world.SetForgeGood(smithy, Goods.IronTools).Allowed);
        Assert.DoesNotContain("keep", world.WhyTheForgeIsCold(smithy), StringComparison.Ordinal);
    }

    /// <summary>
    /// A smith is stood down by the limit of what the smithies are SET to forge, not by the row's
    /// stone limit alone — and the sentences say which (§9.3). Red with the smith read off the row.
    /// </summary>
    [Fact]
    public void ASmithIsStoodDownByTheLimitOfWhatTheSmithyForges()
    {
        SimWorld world = SimFactory.CreatePhase0(Config, new InMemoryLogSink()).World;
        Workplace smithy = ToolsTests.RaiseASmithy(world);
        Assert.True(world.SetStockLimit(Goods.Tools, 1).Allowed);

        // On stone, at the stone limit: stood down, and the sentence names stone tools.
        Assert.Equal("the stone tools limit of 1 is met", LabourQuota.WhyTheVillageWantsNone(world, JobKind.Smith));
        Assert.Contains("the smiths have stopped", world.WhyTheLimitIsMet(Goods.Tools), StringComparison.Ordinal);

        // On iron, the stone limit says nothing to the smith: not stood down at all.
        Assert.True(world.SetForgeGood(smithy, Goods.IronTools).Allowed);
        Assert.Null(LabourQuota.WhyTheVillageWantsNone(world, JobKind.Smith));
        Assert.DoesNotContain("smiths", world.WhyTheLimitIsMet(Goods.Tools), StringComparison.Ordinal);

        // And an iron limit met stands them down by name.
        Assert.True(world.SetStockLimit(Goods.IronTools, 0).Allowed);
        Assert.Equal("the iron tools limit of 0 is met", LabourQuota.WhyTheVillageWantsNone(world, JobKind.Smith));
        Assert.Contains("the smiths have stopped", world.WhyTheLimitIsMet(Goods.IronTools), StringComparison.Ordinal);
    }

    // ---------------------------------------------------------------
    //  § The quota
    // ---------------------------------------------------------------

    /// <summary>
    /// The tool shortfall counts every kind on the shelves and in hands (§9.3). Red with iron left
    /// out: two hundred iron tools on the shelf would read as none.
    /// </summary>
    [Fact]
    public void TheToolShortfallCountsBothKinds()
    {
        SimConfig config = Config;
        SimLoop loop = SimFactory.CreatePhase0(config, new InMemoryLogSink());
        SimWorld world = loop.World;
        loop.Step(config.TicksPerYear + config.TicksPerSeason + 4);

        foreach (StoreBuilding store in world.StoreBuildings)
        {
            store.Store.TryTake(Goods.Tools, store.Store[Goods.Tools]);
        }

        foreach (Villager villager in world.Villagers)
        {
            villager.ToolUses = 0;
        }

        Assert.True(LabourQuota.ToolShortfall(world) > 0, "The premise: with nothing held, tools are short.");

        TheWarehouse(world).Store.Add(Goods.IronTools, 200);
        Assert.Equal(0, LabourQuota.ToolShortfall(world));
    }

    private static StoreBuilding TheWarehouse(SimWorld world) =>
        world.StoreBuildings.First(s => s.Kind == StoreKind.Warehouse);
}

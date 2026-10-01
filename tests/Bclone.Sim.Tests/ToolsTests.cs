using Bclone.Sim.Config;
using Bclone.Sim.Core;
using Bclone.Sim.Determinism;
using Bclone.Sim.Logging;
using Bclone.Sim.World;
using Xunit;
using Xunit.Abstractions;

namespace Bclone.Sim.Tests;

/// <summary>
/// Tools that wear, and the smith who makes them — D391, `specs/tools-and-the-smith.md §7`.
/// </summary>
/// <remarks>
/// <para>
/// <b>The claims, in the spec's order:</b> a tool wears one use per action begun (§3.3); a worker
/// without one works at today's number to the unit and a tool is the bonus — a third off the
/// action's ticks since D429, multiplied with mastery and rounded once (§3.4); a tool in a
/// hand came out of a store's count (§3.5); the smith forges from iron and firewood, never the
/// winter's firewood, and stops at the player's limit (§3.7); smiths are wanted when tools run
/// short (§3.8); and a village with no tools plays as it did before tools existed (§3.1).
/// </para>
/// <para>
/// <b>Measured before typed (§6):</b> the fixture's hands begin about 55 actions a hand-year, so
/// at 150 uses a tool is about three years of one pair of hands; twelve fixture seeds × fifty
/// years read 201 / 222 / 0 with no tools, 202 / 221 / 0 with wear and no bonus, and
/// <b>218 / 238 / 0</b> at 25 %.
/// </para>
/// <para>
/// <b>Red-checked, six for six — and two zeros on the first try, written down:</b> the fetch
/// guard scored zero against <c>TryTake(...) || true</c> because the store always had a tool to
/// take, so nothing was conjured; the break that reddens it is skipping the take altogether,
/// so the count never drops. The quota guard scored zero against <em>"ignore the shelves"</em>
/// because the hands that had taken tools still counted as holding them; the break that
/// reddens it ignores everything held.
/// </para>
/// </remarks>
public sealed class ToolsTests
{
    private readonly ITestOutputHelper _output;

    public ToolsTests(ITestOutputHelper output) => _output = output;

    private static SimConfig Config => VillageFixtures.Village;

    /// <summary>The fixture with no founder arriving skilled, so a hand is a novice at every trade.</summary>
    private static SimConfig NoMasters => Config with { FoundingMasters = 0, FoundingJourneymen = 0 };

    // ---------------------------------------------------------------
    //  § Wear
    // ---------------------------------------------------------------

    /// <summary>A tool wears by one every time an action begins, and never below nothing.</summary>
    /// <remarks>
    /// The seam is <c>BeginWork</c>, the one place every action starts; asked directly so the
    /// claim is about the seam and not about which trade happened to be busy. Red with the
    /// decrement removed: the tool reads 50 after fifty gathers.
    /// </remarks>
    [Fact]
    public void AToolWearsOncePerAction()
    {
        SimWorld world = SimFactory.CreatePhase0(Config, new InMemoryLogSink()).World;
        Villager hand = world.Villagers.First(v => v.Alive && v.CanWork);

        hand.ToolUses = 50;
        for (int i = 0; i < 30; i++)
        {
            world.BeginWork(hand, JobKind.Forager, Config.GatherTicks);
        }

        Assert.Equal(20, hand.ToolUses);

        // The marketer carries no tool (§3.2): a trade whose row says so wears nothing.
        world.BeginWork(hand, JobKind.Marketer, 3);
        Assert.Equal(20, hand.ToolUses);

        for (int i = 0; i < 40; i++)
        {
            world.BeginWork(hand, JobKind.Woodcutter, Config.SplitTicks);
        }

        // Twenty uses, forty splits: it wore to nothing and stayed there — and with nothing in the
        // hand the wear changes nothing: BeginWork's ticks are WorkTicksFor's.
        Assert.Equal(0, hand.ToolUses);
        Assert.Equal(
            world.WorkTicksFor(hand, JobKind.Woodcutter, Config.SplitTicks),
            world.BeginWork(hand, JobKind.Woodcutter, Config.SplitTicks));
    }

    /// <summary>And over a fixture year the founders' tools are taken and worn — the wear is live, not a seam nobody reaches.</summary>
    [Fact]
    public void TheFoundersToolsAreTakenAndWornInPlay()
    {
        SimConfig config = Config;
        SimLoop loop = SimFactory.CreatePhase0(config, new InMemoryLogSink());
        SimWorld world = loop.World;

        int atStart = world.InStores(Goods.Tools);
        Assert.Equal(config.CartTools, atStart);

        loop.Step(config.TicksPerYear);

        int inHands = world.Villagers.Count(v => v.Alive && v.ToolUses > 0);
        int worn = world.Villagers.Where(v => v.Alive && v.ToolUses > 0).Sum(v => config.ToolUses - v.ToolUses);
        _output.WriteLine($"after a year: {world.InStores(Goods.Tools)} tools in the stores, {inHands} in hands, {worn} uses worn");

        Assert.True(inHands > 0, "A year in, nobody holds a tool.");
        Assert.True(worn > 0, "Tools are held and none has worn.");
    }

    // ---------------------------------------------------------------
    //  § The bonus, and the floor it never touches
    // ---------------------------------------------------------------

    /// <summary>
    /// ⭐ A tool takes a third off the action it begins, trade by trade — §3.4's table (D429).
    /// </summary>
    /// <remarks>
    /// The base ticks are the shipped durations, passed in rather than read from the fixture, so
    /// the table here is the table in the spec. 34 % is where a three-tick gather gets a whole tick;
    /// the four-tick trades gain least. Red with the tool's percent never read.
    /// </remarks>
    [Fact]
    public void AToolMakesTheActionItBeginsAThirdQuicker()
    {
        SimWorld world = SimFactory.CreatePhase0(NoMasters, new InMemoryLogSink()).World;
        Villager hand = world.Villagers.First(v => v.Alive && v.CanWork);
        Assert.Empty(hand.Skills);

        (JobKind Trade, int Base, int WithATool)[] table =
        {
            (JobKind.Forager, 3, 2),
            (JobKind.Farmer, 3, 2),
            (JobKind.Forester, 4, 3),
            (JobKind.Woodcutter, 4, 3),
            (JobKind.Smith, 4, 3),
            (JobKind.Fisher, 10, 7),
            (JobKind.Hunter, 15, 10),
        };

        foreach ((JobKind trade, int baseTicks, int withATool) in table)
        {
            hand.ToolUses = 10;
            int quick = world.WorkTicksFor(hand, trade, baseTicks);
            hand.ToolUses = 0;
            int bare = world.WorkTicksFor(hand, trade, baseTicks);
            _output.WriteLine($"{trade,-11} {baseTicks,2} ticks bare, {quick,2} with a tool");

            Assert.Equal(baseTicks, bare);
            Assert.Equal(withATool, quick);
        }

        // A trade that carries no tool is not quickened by one in the hand.
        hand.ToolUses = 10;
        Assert.Equal(10, world.WorkTicksFor(hand, JobKind.Marketer, 10));
    }

    /// <summary>
    /// ⭐ Mastery and a tool multiply, rounded once (Joe, D429): a master's three ticks go to one
    /// with a tool, a master's four stay at two.
    /// </summary>
    /// <remarks>
    /// Ticks left = base × (1 − 50 %) × (1 − 34 %), rounded once — so a master woodcutter gains
    /// nothing from a tool, and the spec says so. Red with the two added (a split would go to one)
    /// and with each rounded separately (a gather would stay at two).
    /// </remarks>
    [Fact]
    public void AMasterWithAToolMultipliesOnce()
    {
        SimConfig config = NoMasters;
        Assert.Equal(50, config.MasterySpeedBonusPercent);
        SimWorld world = SimFactory.CreatePhase0(config, new InMemoryLogSink()).World;
        Villager master = world.Villagers.First(v => v.Alive && v.CanWork);
        foreach (SkillRow skill in config.Skills)
        {
            master.ProgressIn(skill.Id).Work = config.MasteryWorkFor(skill);
        }

        (JobKind Trade, int Base, int Master, int MasterWithATool)[] table =
        {
            (JobKind.Forager, 3, 2, 1),
            (JobKind.Farmer, 3, 2, 1),
            (JobKind.Forester, 4, 2, 2),
            (JobKind.Woodcutter, 4, 2, 2),
            // No skill row (§6): the tool alone, master or not.
            (JobKind.Fisher, 10, 10, 7),
            (JobKind.Hunter, 15, 15, 10),
        };

        foreach ((JobKind trade, int baseTicks, int mastered, int both) in table)
        {
            master.ToolUses = 0;
            int bare = world.WorkTicksFor(master, trade, baseTicks);
            master.ToolUses = 10;
            int withATool = world.WorkTicksFor(master, trade, baseTicks);
            _output.WriteLine($"{trade,-11} {baseTicks,2} ticks: a master {bare,2}, with a tool {withATool,2}");

            Assert.Equal(mastered, bare);
            Assert.Equal(both, withATool);
        }
    }

    /// <summary>
    /// The tool that begins an action quickens it, even on its last use — the ticks are read
    /// before the wear (D429). Red with the wear moved ahead of the read.
    /// </summary>
    [Fact]
    public void TheLastUseStillSpeedsItsOwnAction()
    {
        SimWorld world = SimFactory.CreatePhase0(NoMasters, new InMemoryLogSink()).World;
        Villager hand = world.Villagers.First(v => v.Alive && v.CanWork);

        hand.ToolUses = 1;
        Assert.Equal(10, world.BeginWork(hand, JobKind.Hunter, 15));
        Assert.Equal(0, hand.ToolUses);
        Assert.Equal(15, world.BeginWork(hand, JobKind.Hunter, 15));
    }

    /// <summary>
    /// ⭐ A tool is both, as shipped (Joe, D429): a third off the ticks AND a quarter on the yield —
    /// the data and the C# default agree. Ticks alone was measured at about half of what tools had
    /// been worth, because a forager's trip is one gather between two walks.
    /// </summary>
    [Fact]
    public void AToolIsQuickerAndBringsInMoreAsShipped()
    {
        SimConfig shipped = ShippedConfig.Load();
        Assert.Equal(25, shipped.ToolYieldBonusPercent);
        Assert.Equal(34, shipped.ToolSpeedBonusPercent);
        Assert.Equal(new SimConfig().ToolYieldBonusPercent, shipped.ToolYieldBonusPercent);
        Assert.Equal(new SimConfig().ToolSpeedBonusPercent, shipped.ToolSpeedBonusPercent);

        SimWorld world = SimFactory.CreatePhase0(Config, new InMemoryLogSink()).World;
        Villager hand = world.Villagers.First(v => v.Alive && v.CanWork);

        hand.ToolUses = 10;
        int withTechnique = world.YieldWithTechnique(JobKind.Forager, 123);
        Assert.Equal(withTechnique + (withTechnique * 25 / 100), world.YieldFor(hand, JobKind.Forager, 123));
        Assert.Equal(3 - 1, world.WorkTicksFor(hand, JobKind.Forager, 3));
    }

    /// <summary>
    /// ⛔ A worker without a tool works at today's number to the unit — ticks and yield both — and
    /// a tool adds its percentage on top of the village's technique (§3.4). Red with the bonus
    /// handed to a bare hand.
    /// </summary>
    [Fact]
    public void AWorkerWithoutAToolWorksAtTodaysNumberToTheUnit()
    {
        SimConfig config = NoMasters;
        SimWorld world = SimFactory.CreatePhase0(config, new InMemoryLogSink()).World;
        Villager hand = world.Villagers.First(v => v.Alive && v.CanWork);

        hand.ToolUses = 0;
        Assert.Equal(3, world.WorkTicksFor(hand, JobKind.Forager, 3));
        Assert.Equal(15, world.BeginWork(hand, JobKind.Hunter, 15));
        Assert.Equal(world.YieldWithTechnique(JobKind.Forager, 123), world.YieldFor(hand, JobKind.Forager, 123));
        Assert.Equal(53, world.WithTool(hand, JobKind.Woodcutter, 53));
        Assert.Equal(world.YieldWithTechnique(JobKind.Forester, 40), world.YieldFor(null, JobKind.Forester, 40));

        hand.ToolUses = 1;
        int withTechnique = world.YieldWithTechnique(JobKind.Forager, 123);
        Assert.Equal(withTechnique + (withTechnique * 25 / 100), world.YieldFor(hand, JobKind.Forager, 123));
        Assert.Equal(53 + (53 * 25 / 100), world.WithTool(hand, JobKind.Woodcutter, 53));

        // Rounded down (D2): a tool never invents a unit. One tool a forge at 25 % is one tool.
        Assert.Equal(1, world.YieldFor(hand, JobKind.Smith, 1));

        // And a trade that carries no tool gets no bonus from one in the hand.
        Assert.Equal(40, world.WithTool(hand, JobKind.Marketer, 40));
    }

    // ---------------------------------------------------------------
    //  § Where a tool comes from
    // ---------------------------------------------------------------

    /// <summary>
    /// Every tool in a hand came out of a store's count — the warehouse's twenty are the village's
    /// twenty, in hands or on the shelf, until one wears out. Red with the fetch conjuring.
    /// </summary>
    [Fact]
    public void AToolIsFetchedFromAStoreNotConjured()
    {
        // Enough uses that none wears out inside the season being counted.
        SimConfig config = Config with { ToolUses = 100_000 };
        SimLoop loop = SimFactory.CreatePhase0(config, new InMemoryLogSink());
        SimWorld world = loop.World;

        int fetches = 0;
        for (int tick = 0; tick < config.TicksPerSeason * 2; tick++)
        {
            loop.StepOnce();
            fetches += world.Villagers.Count(v => v.State == VillagerState.FetchingATool);
        }

        int inHands = world.Villagers.Count(v => v.Alive && v.ToolUses > 0);
        int onShelves = world.InStores(Goods.Tools);
        _output.WriteLine($"{inHands} in hands + {onShelves} on the shelves = {inHands + onShelves}; {world.ToolsEverTaken} ever taken; {fetches} villager-ticks spent fetching");

        Assert.True(inHands > 0, "Nobody fetched a tool in two seasons.");
        Assert.True(fetches > 0, "Tools were taken and nobody was ever seen walking for one.");
        Assert.Equal(config.CartTools, inHands + onShelves);
        Assert.Equal(inHands, world.ToolsEverTaken);
    }

    /// <summary>With no tool anywhere, a hand with a job says so and works on at today's number.</summary>
    [Fact]
    public void WithNoToolToBeHadTheNoteSaysSo()
    {
        SimConfig config = Config with { CartTools = 0 };
        SimLoop loop = SimFactory.CreatePhase0(config, new InMemoryLogSink());
        SimWorld world = loop.World;

        // ⚠️ SAMPLED EVERY TICK, NOT READ ONCE AT THE YEAR'S END (D406): the note is on whoever is at
        // work, and with fences the walks moved so that nobody happened to be at work on that one
        // tick. The claim is that the note is said, not when.
        Villager? noted = null;
        for (int tick = 0; tick < config.TicksPerYear && noted is null; tick++)
        {
            loop.StepOnce();
            noted = world.Villagers.FirstOrDefault(v => v.Alive && v.WorkNote.Contains("without a tool", StringComparison.Ordinal));
        }

        Assert.All(world.Villagers, v => Assert.Equal(0, v.ToolUses));
        Assert.NotNull(noted);
        _output.WriteLine($"{noted!.Name}: {noted.WorkNote}");
        Assert.Equal("Working without a tool — none in any store.", noted.WorkNote);
    }

    // ---------------------------------------------------------------
    //  § The smith
    // ---------------------------------------------------------------

    /// <summary>The D29 shape, one good over: iron and firewood out of a store, tools into one.</summary>
    [Fact]
    public void ASmithForgesToolsFromIronAndFirewood()
    {
        SimConfig config = Config;
        SimLoop loop = SimFactory.CreatePhase0(config, new InMemoryLogSink());
        SimWorld world = loop.World;

        Workplace smithy = RaiseASmithy(world);
        StoreBuilding warehouse = TheWarehouse(world);
        KeepTheVillageWarmAndFed(world);
        OnlyASmithWorks(world);
        warehouse.Store.Add(Goods.Iron, 40);
        int ironBefore = warehouse.Store[Goods.Iron];
        int toolsBefore = world.InStores(Goods.Tools);

        int forgingTicks = 0;
        for (int tick = 0; tick < config.TicksPerSeason * 2 && world.ToolsEverForged < 4; tick++)
        {
            loop.StepOnce();
            forgingTicks += world.Villagers.Count(v => v.State == VillagerState.Forging && v.Tile == smithy.Tile);
        }

        int forged = world.ToolsEverForged;
        int ironSpent = ironBefore - warehouse.Store[Goods.Iron];
        _output.WriteLine($"{forged} tools forged from {ironSpent} iron; {world.InStores(Goods.Tools) - toolsBefore} more on the shelves; {forgingTicks} villager-ticks at the anvil");

        Assert.True(forged > 0, "A staffed smithy with iron and firewood forged nothing.");
        Assert.Equal(forged * config.IronPerTool, ironSpent);
        // The quickest a forge can be is with a tool in hand (D429: 4 → 3); the smith has no skill
        // row, so nothing else shortens it. Fewer ticks than that and a tool came from nowhere.
        int quickestForge = config.ForgeTicks - (config.ForgeTicks * config.ToolSpeedBonusPercent / 100);
        Assert.True(forgingTicks >= forged * quickestForge, "The tools came without the smith being seen at the anvil.");
    }

    /// <summary>
    /// ⛔ A forge never takes firewood the homes still want (§3.7). Red with the guard off: the
    /// sheds' last firewood becomes tools in autumn.
    /// </summary>
    [Fact]
    public void AForgeNeverTakesTheWintersFirewood()
    {
        SimConfig config = Config;
        SimLoop loop = SimFactory.CreatePhase0(config, new InMemoryLogSink());
        SimWorld world = loop.World;

        Workplace smithy = RaiseASmithy(world);
        StoreBuilding warehouse = TheWarehouse(world);
        OnlyASmithWorks(world);
        foreach (Household household in world.Households)
        {
            household.Stockpile.Add(Goods.Produce, world.TargetFoodFor(household));
        }

        // Iron to spare, and just enough firewood for one forge — with the homes' winter still
        // owed, so the shortfall reads above zero.
        warehouse.Store.Add(Goods.Iron, 40);
        int firewood = warehouse.Store[Goods.Firewood];
        if (firewood < config.FirewoodPerTool)
        {
            warehouse.Store.Add(Goods.Firewood, config.FirewoodPerTool - firewood);
        }

        Assert.True(LabourQuota.FirewoodShortfall(world) > 0, "The premise: the homes are still owed firewood.");
        int firewoodBefore = FirewoodEverywhere(world);

        loop.Step(config.TicksPerSeason);

        Villager? smith = world.Villagers.FirstOrDefault(v => v.Alive && v.WorkplaceId == smithy.Id);
        _output.WriteLine($"{smith?.Name ?? "nobody"} holds the smithy; note: {smith?.WorkNote}; forged {world.ToolsEverForged}");

        Assert.NotNull(smith);
        Assert.Equal(0, world.ToolsEverForged);
        Assert.Contains("firewood for the winter", smith!.WorkNote, StringComparison.Ordinal);
        // Nothing burns in spring, so every stick is still somewhere — unless the forge took it.
        Assert.Equal(firewoodBefore, FirewoodEverywhere(world));
    }

    /// <summary>A met tools limit stops the forge, not just the hiring (D139).</summary>
    [Fact]
    public void AMetToolsLimitStopsTheForge()
    {
        SimConfig config = Config;
        SimLoop loop = SimFactory.CreatePhase0(config, new InMemoryLogSink());
        SimWorld world = loop.World;

        Workplace smithy = RaiseASmithy(world);
        StoreBuilding warehouse = TheWarehouse(world);
        KeepTheVillageWarmAndFed(world);
        OnlyASmithWorks(world);
        warehouse.Store.Add(Goods.Iron, 40);
        Assert.True(world.SetStockLimit(Goods.Tools, 1).Allowed);

        loop.Step(config.TicksPerSeason);

        Villager? smith = world.Villagers.FirstOrDefault(v => v.Alive && v.WorkplaceId == smithy.Id);
        _output.WriteLine($"{smith?.Name ?? "nobody"} holds the smithy; note: {smith?.WorkNote}; forged {world.ToolsEverForged}");

        Assert.NotNull(smith);
        Assert.Equal(0, world.ToolsEverForged);
        Assert.Contains("asked the village to keep 1 tools", smith!.WorkNote, StringComparison.Ordinal);
    }

    // ---------------------------------------------------------------
    //  § The quota
    // ---------------------------------------------------------------

    /// <summary>Smiths are wanted when tools run short of the hands that use them, and not before.</summary>
    [Fact]
    public void SmithsAreWantedOnlyWhenToolsRunShort()
    {
        SimConfig config = Config;
        SimLoop loop = SimFactory.CreatePhase0(config, new InMemoryLogSink());
        SimWorld world = loop.World;

        // Into the second summer, when the seats have been handed out for the season.
        loop.Step(config.TicksPerYear + config.TicksPerSeason + 4);

        TheWarehouse(world).Store.Add(Goods.Tools, 200);
        Assert.Equal(0, LabourQuota.ToolShortfall(world));
        Assert.Equal(0, LabourQuota.SmithsWanted(world));

        foreach (StoreBuilding store in world.StoreBuildings)
        {
            store.Store.TryTake(Goods.Tools, store.Store[Goods.Tools]);
        }

        foreach (Villager villager in world.Villagers)
        {
            villager.ToolUses = 0;
        }

        int hands = world.Villagers.Count(v => v.Alive && v.CanWork
            && world.FindWorkplace(v.WorkplaceId) is Workplace job && world.JobsCatalog.UsesTool(job.Kind));
        _output.WriteLine($"{hands} hands in tool trades; shortfall {LabourQuota.ToolShortfall(world)}; smiths wanted {LabourQuota.SmithsWanted(world)}; a smith forges {VillageEconomy.ToolsForgedPerYearAtWorst(config)} a year");

        Assert.True(hands > 0, "The premise: somebody holds a tool trade a year in.");
        Assert.Equal(hands * 2, LabourQuota.ToolShortfall(world));
        Assert.True(LabourQuota.SmithsWanted(world) >= 1);
    }

    // ---------------------------------------------------------------
    //  § Inert without tools
    // ---------------------------------------------------------------

    /// <summary>
    /// A village with no tools plays exactly as it would with the bonus at any number — the
    /// machinery is inert until a tool is in a hand, which is what lets the floor stay the floor.
    /// </summary>
    [Fact]
    public void AVillageWithNoToolsPlaysAsItDidBeforeToolsExisted()
    {
        SimConfig none = Config with { CartTools = 0, ToolYieldBonusPercent = 0, ToolSpeedBonusPercent = 0 };
        SimConfig bonus = Config with { CartTools = 0, ToolYieldBonusPercent = 90, ToolSpeedBonusPercent = 90 };

        SimLoop a = SimFactory.CreatePhase0(none, new InMemoryLogSink());
        SimLoop b = SimFactory.CreatePhase0(bonus, new InMemoryLogSink());
        a.Step(none.TicksPerYear * 3);
        b.Step(none.TicksPerYear * 3);

        _output.WriteLine($"{StateHash.Compute(a.World):X16} against {StateHash.Compute(b.World):X16}");
        Assert.Equal(StateHash.Compute(a.World), StateHash.Compute(b.World));
        Assert.All(a.World.Villagers, v => Assert.Equal(0, v.ToolUses));
    }

    /// <summary>`uses_tool` is a column: read off the rows, true where the spec says and false where it does not.</summary>
    [Fact]
    public void WhichTradesCarryAToolIsAColumn()
    {
        var catalog = new JobsCatalog(ShippedConfig.Load().JobsCatalog);

        Assert.True(catalog.UsesTool(JobKind.Forager));
        Assert.True(catalog.UsesTool(JobKind.Forester));
        Assert.True(catalog.UsesTool(JobKind.Woodcutter));
        Assert.True(catalog.UsesTool(JobKind.Farmer));
        Assert.True(catalog.UsesTool(JobKind.Fisher));
        Assert.True(catalog.UsesTool(JobKind.Hunter));
        Assert.True(catalog.UsesTool(JobKind.Smith));
        Assert.False(catalog.UsesTool(JobKind.Marketer));
        Assert.False(catalog.UsesTool(JobKind.Builder));
    }

    // ---------------------------------------------------------------
    //  helpers
    // ---------------------------------------------------------------

    private static StoreBuilding TheWarehouse(SimWorld world) =>
        world.StoreBuildings.First(s => s.Kind == StoreKind.Warehouse);

    private static int FirewoodEverywhere(SimWorld world) =>
        world.StoreBuildings.Sum(s => s.Store[Goods.Firewood])
        + world.Households.Sum(h => h.Stockpile[Goods.Firewood])
        + world.Villagers.Sum(v => v.Carried[Goods.Firewood]);

    /// <summary>Everyone fed, the sheds holding the winter and more, so nothing outranks the forge.</summary>
    private static void KeepTheVillageWarmAndFed(SimWorld world)
    {
        foreach (Household household in world.Households)
        {
            household.Stockpile.Add(Goods.Produce, world.TargetFoodFor(household));
            household.Stockpile.Add(Goods.Firewood, VillageEconomy.FirewoodStoreWantedPerHousehold(world.Config));
        }

        TheWarehouse(world).Store.Add(Goods.Firewood, 400);
        Assert.Equal(0, LabourQuota.FirewoodShortfall(world));
    }

    /// <summary>One smith and nobody else, so the only trade at work is the one being watched.</summary>
    private static void OnlyASmithWorks(SimWorld world)
    {
        Assert.True(world.SetStockLimit(Goods.Produce, 1).Allowed);
        foreach (JobKind kind in JobLimits.Kinds)
        {
            world.SetJobLimit(kind, kind == JobKind.Smith ? 1 : 0);
        }
    }

    private static Workplace RaiseASmithy(SimWorld world)
    {
        GridPos site = world.Map.FoundingSite;
        GridPos? at = null;
        for (int radius = 3; radius < 30 && at is null; radius++)
        {
            for (int dy = -radius; dy <= radius && at is null; dy++)
            {
                for (int dx = -radius; dx <= radius && at is null; dx++)
                {
                    var tile = new GridPos(site.X + dx, site.Y + dy);
                    if (world.CanBuildAt(BuildingKind.Smithy, tile).Allowed)
                    {
                        at = tile;
                    }
                }
            }
        }

        Assert.NotNull(at);

        // The smith's craft is learned by digging iron since D444; these guards are about the forge,
        // so the village is lent the iron it takes to know how (paid — no gift is owed).
        world.IronEverDug = Math.Max(world.IronEverDug, world.Config.SmithyUnlockIron);
        world.Mark(BuildingKind.Smithy, at!.Value);
        Workplace plan = world.Workplaces.Single(w => w.Construction?.Kind == BuildingKind.Smithy);
        BuildFixtures.StockTheSite(plan);
        for (int i = 0; i <= plan.Construction!.Recipe.WorkTicks; i++)
        {
            plan.Construction.Work();
        }

        world.Complete(plan);
        return world.Workplaces.Single(w => w.Kind == JobKind.Smith && !w.IsSite);
    }
}

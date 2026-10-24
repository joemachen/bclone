using Bclone.Sim.Core;
using Bclone.Sim.Logging;
using Bclone.Sim.Systems;
using Bclone.Sim.World;
using Xunit;
using Xunit.Abstractions;

namespace Bclone.Sim.Tests;

/// <summary>
/// ⭐ Every hands-on adult carries a tool (D617, Joe: <i>"builders should carry tools. all adults should except maybe the
/// teacher and the preacher"</i>; `tools-and-the-smith.md §10`): builders (a third faster), herdsmen, and laborers from what
/// the trades can spare — never the carriers, the marketer and the merchant.
/// </summary>
public sealed class ToolsForEveryHandTests
{
    private readonly ITestOutputHelper _output;

    public ToolsForEveryHandTests(ITestOutputHelper output) => _output = output;

    private static SimLoop AVillage() => SimFactory.CreatePhase0(VillageFixtures.Village, new InMemoryLogSink());

    /// <summary>The column as shipped: hands-on trades carry a tool; the carriers do not.</summary>
    [Fact]
    public void EveryHandsOnTradeCarriesAToolAndTheCarriersDoNot()
    {
        JobsCatalog jobs = AVillage().World.JobsCatalog;
        foreach (JobKind trade in new[] { JobKind.Builder, JobKind.Herdsman, JobKind.Forester, JobKind.Woodcutter, JobKind.Smith })
        {
            Assert.True(jobs.UsesTool(trade), $"{trade} should carry a tool");
        }

        foreach (JobKind trade in new[] { JobKind.Marketer, JobKind.Merchant })
        {
            Assert.False(jobs.UsesTool(trade), $"{trade} carries goods, not a tool");
        }
    }

    /// <summary>
    /// A builder's tool does a third more of a site's work a tick: stone's 34 % is 34 extra units in every 66 ticks, so a
    /// site takes 66 % of the time; iron's 50 % doubles the work; empty hands do none extra.
    /// </summary>
    [Fact]
    public void ABuildersToolBuildsAThirdFaster()
    {
        SimWorld world = AVillage().World;
        ToolsTests.RaiseASmithy(world); // a builder's tool waits for one (Joe's call, D617)
        Villager builder = world.Villagers[0];

        int Sum(int ticks)
        {
            int extra = 0;
            for (int t = 0; t < ticks; t++)
            {
                world.Tick = (ulong)t;
                extra += world.ToolWorkThisTick(builder);
            }

            return extra;
        }

        builder.ToolUses = 0;
        Assert.Equal(0, Sum(660));

        builder.ToolUses = 10;
        builder.ToolGood = Goods.Tools;
        Assert.Equal(34, world.GoodsCatalog[Goods.Tools].ToolSpeedBonusPercent);
        Assert.Equal(340, Sum(660));

        builder.ToolGood = Goods.IronTools;
        Assert.Equal(50, world.GoodsCatalog[Goods.IronTools].ToolSpeedBonusPercent);
        Assert.Equal(660, Sum(660));
    }

    /// <summary>
    /// The wiring, at a real site: raised a tick at a time, a granary takes its recipe's work ticks with bare hands and about
    /// two thirds of them with a stone tool in hand.
    /// </summary>
    [Fact]
    public void ASiteGoesUpAThirdQuickerWithAToolInHand()
    {
        int TicksToRaise(bool tool, out int recipe)
        {
            SimWorld world = AVillage().World;
            ToolsTests.RaiseASmithy(world); // a builder's tool waits for one (Joe's call, D617)
            GridPos site = world.Map.FoundingSite;
            GridPos? at = null;
            for (int radius = 3; radius < 30 && at is null; radius++)
            {
                for (int dy = -radius; dy <= radius && at is null; dy++)
                {
                    for (int dx = -radius; dx <= radius && at is null; dx++)
                    {
                        var tile = new GridPos(site.X + dx, site.Y + dy);
                        if (world.CanBuildAt(BuildingKind.Granary, tile).Allowed)
                        {
                            at = tile;
                        }
                    }
                }
            }

            Assert.NotNull(at);
            Assert.True(world.Mark(BuildingKind.Granary, at!.Value).Allowed);
            Workplace plan = world.Workplaces.Last(w => w.Construction?.Kind == BuildingKind.Granary);
            foreach (GridPos covered in plan.CoveredTiles)
            {
                world.SetTerrain(covered, Terrain.Grass);
            }

            BuildFixtures.StockTheSite(plan);
            recipe = plan.Construction!.Recipe.WorkTicks;
            Villager builder = world.Villagers.First(v => v.CanWork);
            builder.Position = Point.CentreOf(plan.Tile);
            builder.ToolUses = tool ? 100 : 0;
            builder.ToolGood = Goods.Tools;
            int ticks = 0;
            while (!plan.Construction.IsFinished && ticks < 10_000)
            {
                world.Tick++;
                BehaviorSystem.RaiseForTest(world, builder);
                ticks++;
            }

            return ticks;
        }

        int bare = TicksToRaise(tool: false, out int work);
        int withATool = TicksToRaise(tool: true, out _);
        _output.WriteLine($"a granary of {work} work: {bare} ticks bare, {withATool} with a stone tool");
        Assert.Equal(work, bare);
        Assert.InRange(withATool, (work * 66 / 100) - 1, (work * 66 / 100) + 1);
    }

    /// <summary>
    /// A builder's tool wears a use each time they set off to build — a stint, as an action begun wears any trade's tool —
    /// and not again on every tick of the work.
    /// </summary>
    [Fact]
    public void ABuildersToolWearsAUseAStint()
    {
        SimWorld world = AVillage().World;
        ToolsTests.RaiseASmithy(world); // a builder's tool waits for one (Joe's call, D617)
        GridPos site = world.Map.FoundingSite;
        GridPos? at = null;
        for (int radius = 3; radius < 30 && at is null; radius++)
        {
            for (int dy = -radius; dy <= radius && at is null; dy++)
            {
                for (int dx = -radius; dx <= radius && at is null; dx++)
                {
                    var tile = new GridPos(site.X + dx, site.Y + dy);
                    if (world.CanBuildAt(BuildingKind.Granary, tile).Allowed)
                    {
                        at = tile;
                    }
                }
            }
        }

        Assert.True(world.Mark(BuildingKind.Granary, at!.Value).Allowed);
        Workplace plan = world.Workplaces.Last(w => w.Construction?.Kind == BuildingKind.Granary);
        foreach (GridPos covered in plan.CoveredTiles)
        {
            world.SetTerrain(covered, Terrain.Grass);
        }

        BuildFixtures.StockTheSite(plan);
        Villager builder = world.Villagers.First(v => v.CanWork);
        builder.ToolUses = 10;
        builder.ToolGood = Goods.Tools;

        Assert.True(BehaviorSystem.WorkTheSiteForTest(world, builder), "the premise: a builder sets off for a paid-for site");
        Assert.Equal(VillagerState.Building, builder.State);
        Assert.Equal(9, builder.ToolUses);

        builder.Position = Point.CentreOf(plan.Tile);
        for (int t = 0; t < 5; t++)
        {
            world.Tick++;
            BehaviorSystem.RaiseForTest(world, builder);
        }

        Assert.Equal(9, builder.ToolUses);
    }

    /// <summary>A herdsman's tool wears a use an action, as any trade's does.</summary>
    [Fact]
    public void AHerdsmansToolWears()
    {
        SimWorld world = AVillage().World;
        Villager herdsman = world.Villagers[0];
        herdsman.ToolUses = 5;

        // Before a smithy a herdsman's tool waits (Joe's call, D617): neither worn nor used at the herd.
        world.BeginWork(herdsman, JobKind.Herdsman, world.Config.ReapTicks);
        Assert.Equal(5, herdsman.ToolUses);

        ToolsTests.RaiseASmithy(world);
        world.BeginWork(herdsman, JobKind.Herdsman, world.Config.ReapTicks);
        Assert.Equal(4, herdsman.ToolUses);
    }

    /// <summary>
    /// ⭐ Trades first: a laborer takes a tool only past one on the shelf for every working pair of hands in a tool trade,
    /// and says why when there is none to spare; with one past the cover, they set off for it.
    /// </summary>
    [Fact]
    public void ALaborerTakesOnlyWhatTheTradesCanSpare()
    {
        SimWorld world = AVillage().World;
        ToolsTests.RaiseASmithy(world); // a laborer's tool waits for one (Joe's call, D617)
        var adults = world.Villagers.Where(v => v.Alive && v.CanWork).ToList();
        Assert.True(adults.Count >= 2, "the premise: two adults");

        // One in a tool trade — the woodcutter's hut — and one a laborer; nobody else seated (the founding is posed).
        foreach (Villager v in adults)
        {
            v.WorkplaceId = 0;
        }

        adults[0].WorkplaceId = world.Workplaces.First(w => w.Kind == JobKind.Woodcutter && !w.IsSite).Id;
        Villager laborer = adults[1];
        laborer.ToolUses = 0;
        Assert.True(laborer.IsLaborer, "the premise: a laborer");

        StoreBuilding shelf = world.StoreBuildings.First(s => s.Accepts(Goods.Tools));
        foreach (StoreBuilding store in world.StoreBuildings)
        {
            store.Store.TakeAll(Goods.Tools);
            store.Store.TakeAll(Goods.IronTools);
        }

        int hands = world.Villagers.Count(v => v.Alive && v.CanWork && world.FindWorkplace(v.WorkplaceId) is Workplace job
            && world.JobsCatalog.UsesTool(job.Kind));
        _output.WriteLine($"{hands} hands in tool trades");
        Assert.True(hands > 0, "the premise: somebody works a tool trade");

        shelf.Store.Add(Goods.Tools, hands);
        Assert.Equal(0, LabourQuota.ToolsTheTradesCanSpare(world));
        Assert.False(BehaviorSystem.TryFetchAToolForTest(world, laborer));
        Assert.Contains("the trades come first", laborer.WorkNote, StringComparison.Ordinal);

        shelf.Store.Add(Goods.Tools, 1);
        Assert.Equal(1, LabourQuota.ToolsTheTradesCanSpare(world));
        Assert.True(BehaviorSystem.TryFetchAToolForTest(world, laborer));
        Assert.Equal(VillagerState.FetchingATool, laborer.State);
    }

    /// <summary>
    /// ⭐ In the village's own day, not posed: with tools to spare on the shelves, a laborer decides to fetch one — the
    /// errand is asked in <c>Decide</c>, where a laborer with no job reaches it (D617; a mutant that left laborers out of
    /// <c>Decide</c> passed every posed guard until this one).
    /// </summary>
    [Fact]
    public void ALaborerFetchesASpareToolOfTheirOwnAccord()
    {
        SimLoop loop = AVillage();
        SimWorld world = loop.World;
        ToolsTests.RaiseASmithy(world); // a laborer's tool waits for one (Joe's call, D617)
        world.StoreBuildings.First(s => s.Accepts(Goods.Tools)).Store.Add(Goods.Tools, 100);

        // ⚠️ Every trade closed (its job limit 0), so the adults stay laborers — in a working village the allocator seats the
        // founders' laborers within a tick, and a laborer who is gone by the next look proves nothing. With no hands in a
        // tool trade every tool on the shelf is spare.
        foreach (JobKind kind in JobLimits.Kinds)
        {
            world.SetJobLimit(kind, 0);
        }

        // ⚠️ A laborer empty-handed before a step and still a laborer after it, now holding a tool — taken by the laborer's
        // own errand. Not "any laborer with a tool": a hand can take a trade, fetch through its job, and be a laborer again by
        // the next look (that confound passed the mutant that cut laborers out of Decide).
        var fetched = new HashSet<int>();
        for (int t = 0; t < world.Config.TicksPerYear && fetched.Count == 0; t++)
        {
            var emptyLaborers = world.Villagers.Where(v => v.Alive && v.IsLaborer && v.ToolUses == 0).Select(v => v.Id).ToHashSet();
            loop.StepOnce();
            foreach (Villager v in world.Villagers)
            {
                if (emptyLaborers.Contains(v.Id) && v.Alive && v.IsLaborer && v.ToolUses > 0)
                {
                    fetched.Add(v.Id);
                }
            }
        }

        Assert.True(world.Villagers.Any(v => v.Alive && v.IsLaborer), "the premise: the village has laborers");
        Assert.NotEmpty(fetched);
    }

    /// <summary>
    /// ⭐ Once a smithy stands (Joe's call, D617): before it, a builder and a laborer take no tool — the founders' few are the
    /// feeding trades' — and say why, while a woodcutter takes one as ever; with one standing, the builder does too.
    /// </summary>
    [Fact]
    public void BuildersAndLaborersWaitForASmithy()
    {
        SimWorld world = AVillage().World;
        Assert.False(world.ASmithyStands(), "the premise: no smithy yet");
        world.StoreBuildings.First(s => s.Accepts(Goods.Tools)).Store.Add(Goods.Tools, 50);
        var adults = world.Villagers.Where(v => v.Alive && v.CanWork).ToList();
        Assert.True(adults.Count >= 3, "the premise: three adults");
        foreach (Villager v in adults)
        {
            v.WorkplaceId = 0;
            v.ToolUses = 0;
        }

        Villager builder = adults[0];
        builder.WorkplaceId = world.Workplaces.First(w => w.Kind == JobKind.Builder && !w.IsSite).Id;
        Villager woodcutter = adults[1];
        woodcutter.WorkplaceId = world.Workplaces.First(w => w.Kind == JobKind.Woodcutter && !w.IsSite).Id;
        Villager laborer = adults[2];

        Assert.False(BehaviorSystem.TryFetchAToolForTest(world, builder));
        Assert.Contains("smithy", builder.WorkNote, StringComparison.Ordinal);
        Assert.False(BehaviorSystem.TryFetchAToolForTest(world, laborer));
        Assert.True(BehaviorSystem.TryFetchAToolForTest(world, woodcutter));

        ToolsTests.RaiseASmithy(world);
        Assert.True(world.ASmithyStands());
        Assert.True(BehaviorSystem.TryFetchAToolForTest(world, builder));
    }

    /// <summary>A marketer, with tools on every shelf, never sets off for one — their work is carrying.</summary>
    [Fact]
    public void ACarrierNeverTakesATool()
    {
        SimWorld world = AVillage().World;
        Workplace market = world.Workplaces.First(w => w.Kind == JobKind.Marketer);
        Villager marketer = world.Villagers.First(v => v.CanWork);
        marketer.WorkplaceId = market.Id;
        marketer.ToolUses = 0;
        world.StoreBuildings.First(s => s.Accepts(Goods.Tools)).Store.Add(Goods.Tools, 50);

        Assert.False(BehaviorSystem.TryFetchAToolForTest(world, marketer));
        Assert.NotEqual(VillagerState.FetchingATool, marketer.State);
    }
}

using System.Text.Json.Nodes;
using Bclone.Sim.Core;
using Bclone.Sim.Determinism;
using Bclone.Sim.Logging;
using Bclone.Sim.Persistence;
using Bclone.Sim.World;
using Xunit;
using Xunit.Abstractions;

namespace Bclone.Sim.Tests;

/// <summary>
/// The season's walk, the repair site and what <em>closed</em> means — <c>specs/building-condition.md §4.3–4.5</c>
/// (D555). Every pose sets a stamp back in time rather than running decades: the clock is derived, so a hut raised
/// "twelve years ago" is twelve years worn the moment the stamp says so.
/// </summary>
public sealed class RepairTests
{
    private readonly ITestOutputHelper _output;

    public RepairTests(ITestOutputHelper output) => _output = output;

    private static SimLoop AVillage() => SimFactory.CreatePhase0(VillageFixtures.Village, new InMemoryLogSink());

    /// <summary>
    /// Step through the next season's first tick — the one tick the walk runs on — and no further, so the builders have
    /// not yet had the season to serve what it raised. The turn is processed in the step whose tick is the boundary.
    /// </summary>
    private static void StepToTheSeasonTurn(SimLoop loop)
    {
        SimWorld world = loop.World;
        int season = world.Config.TicksPerSeason;
        int toBoundary = (season - (int)(world.Tick % (ulong)season)) % season;
        loop.Step(toBoundary + 1);
    }

    /// <summary>
    /// A village this many years on, landed three ticks past a season's turn — so the next tick is not a turn, and a
    /// stamp set back this many years does not run past tick 0 (a <c>ulong</c> wraps; a ruin needs twenty years of room).
    /// </summary>
    private static SimLoop AVillageAfter(int years)
    {
        SimLoop loop = AVillage();
        loop.Step((loop.World.Config.TicksPerYear * years) + 3);
        return loop;
    }

    private static ulong YearsAgo(SimWorld world, int years) =>
        world.Tick - ((ulong)years * (ulong)world.Config.TicksPerYear);

    private static Workplace WornHut(SimWorld world, int yearsOld)
    {
        Workplace hut = world.Workplaces.First(w => !w.IsSite && w.Kind == JobKind.Woodcutter);
        // Both stamps: the fixture's builders may already have mended it during the run-up (they did, at tick 3439 — the
        // mechanism working before its guards were green).
        hut.RaisedAtTick = YearsAgo(world, yearsOld);
        hut.RepairedAtTick = 0;
        return hut;
    }

    private static Workplace? RepairSiteOf(SimWorld world, Workplace hut) => world.RepairSiteAt(hut.Tile);

    // ---------------------------------------------------------------
    //  §4.3 — the repair site
    // ---------------------------------------------------------------

    /// <summary>A worn hut gets exactly one repair site at the season's turn, priced at the row's logs × the share and no stone, and none before the turn.</summary>
    [Fact]
    public void AWornHutGetsOneRepairSiteAtTheSeasonsTurnPricedInLogsAlone()
    {
        SimLoop loop = AVillageAfter(VillageFixtures.Village.BuildingWearsInYears + 1);
        SimWorld world = loop.World;
        Workplace hut = WornHut(world, world.Config.BuildingWearsInYears);
        Assert.Equal(Condition.Worn, world.ConditionOf(hut));

        loop.Step(1);
        Assert.Null(RepairSiteOf(world, hut));

        StepToTheSeasonTurn(loop);
        Workplace? site = RepairSiteOf(world, hut);
        Assert.NotNull(site);
        ConstructionSite plan = site.Construction!;
        Assert.True(plan.Repairing);
        BuildingRecipe whole = BuildingRecipe.For(BuildingKind.WoodcutterHut, world.Config);
        int logsOwed = ((whole.Of(Goods.Logs) * world.Config.RepairCostPercent) + 99) / 100;
        Assert.Equal(logsOwed, plan.Recipe.Of(Goods.Logs));
        Assert.Equal(0, plan.Recipe.Of(Goods.Stone));
        Assert.Equal(((whole.WorkTicks * world.Config.RepairCostPercent) + 99) / 100, plan.Recipe.WorkTicks);
        Assert.Contains("being repaired", site.Name, StringComparison.Ordinal);

        // A second season raises no second site for the hut — the rest of the village, nine years old too, gets its own.
        StepToTheSeasonTurn(loop);
        Assert.True(world.Workplaces.Count(w => w.Construction is { Repairing: true } && w.Footprint.Covers(hut.Tile)) <= 1);
        _output.WriteLine($"{site.Name}: {plan.Recipe.Of(Goods.Logs)} logs, {plan.Recipe.WorkTicks} ticks");
    }

    /// <summary>A repair site queues behind a new site marked earlier (D105: a site nobody ranked sits where it was marked).</summary>
    [Fact]
    public void ARepairQueuesBehindTheSitesMarkedBeforeIt()
    {
        SimLoop loop = AVillageAfter(VillageFixtures.Village.BuildingWearsInYears + 1);
        SimWorld world = loop.World;
        ColdStartTests.MarkSomewhereNear(world, BuildingKind.Granary, world.Map.FoundingSite, 8);
        Workplace granarySite = world.Workplaces.Single(w => w.Construction?.Kind == BuildingKind.Granary);
        Workplace hut = WornHut(world, world.Config.BuildingWearsInYears);
        StepToTheSeasonTurn(loop);

        List<Workplace> queue = world.BuildQueue();
        int granaryAt = queue.FindIndex(w => w.Id == granarySite.Id);
        int repairAt = queue.FindIndex(w => w.Construction is { Repairing: true });
        Assert.True(granaryAt >= 0 && repairAt > granaryAt, $"granary at {granaryAt}, repair at {repairAt}");
    }

    /// <summary>The builders fetch, work and finish a repair; the hut is sound again, stamped with the tick, and the site is gone.</summary>
    [Fact]
    public void TheBuildersMendAWornHutAndItIsSoundAgain()
    {
        SimLoop loop = AVillageAfter(VillageFixtures.Village.BuildingWearsInYears + 1);
        SimWorld world = loop.World;
        Workplace hut = WornHut(world, world.Config.BuildingWearsInYears);
        StepToTheSeasonTurn(loop);
        Assert.NotNull(RepairSiteOf(world, hut));
        Assert.True(world.InStores(Goods.Logs) > 0, "no logs anywhere, so the repair cannot be served and this proves nothing");

        int ticks = 0;
        while (RepairSiteOf(world, hut) is not null && ticks < world.Config.TicksPerYear * 2)
        {
            loop.StepOnce();
            ticks++;
        }

        _output.WriteLine($"mended in {ticks} ticks");
        Assert.Null(RepairSiteOf(world, hut));
        Assert.Equal(Condition.Sound, world.ConditionOf(hut));
        Assert.InRange(hut.RepairedAtTick, world.Tick - (ulong)ticks, world.Tick);
        Assert.Contains(world.Workplaces, w => w.Id == hut.Id && !w.IsSite);
    }

    /// <summary>A repair nobody can fund waits like any unfunded site: nobody is hired for it who would not have been hired anyway.</summary>
    [Fact]
    public void ARepairWithNoLogsAnywhereHoldsNoBuilder()
    {
        SimLoop loop = AVillageAfter(VillageFixtures.Village.BuildingWearsInYears + 1);
        SimWorld world = loop.World;
        foreach (StoreBuilding store in world.StoreBuildings)
        {
            int logs = store.Store[Goods.Logs];
            if (logs > 0) { store.Store.TryTake(Goods.Logs, logs); }
        }

        world.SetStockLimit(Goods.Logs, 0);
        Workplace hut = WornHut(world, world.Config.BuildingWearsInYears);
        StepToTheSeasonTurn(loop);
        Assert.NotNull(RepairSiteOf(world, hut));

        loop.Step(world.Config.TicksPerSeason);
        int builders = world.Villagers.Count(v => v.Alive && world.Workplaces.Any(w => w.Id == v.WorkplaceId && w.Kind == JobKind.Builder && !w.IsSite));
        int idleBuilders = world.Villagers.Count(v => v.Alive && v.State == VillagerState.Building);
        _output.WriteLine($"{builders} builders hired, {idleBuilders} standing at a site");
        Assert.Equal(0, idleBuilders);
        Assert.NotNull(RepairSiteOf(world, hut));
    }

    /// <summary>A hut under repair keeps its places and a home under repair still shelters — the site on the building's tiles changes nothing about the building.</summary>
    [Fact]
    public void AThingUnderRepairStillWorksAndShelters()
    {
        SimLoop loop = AVillageAfter(VillageFixtures.Village.BuildingWearsInYears + 1);
        SimWorld world = loop.World;
        Workplace hut = WornHut(world, world.Config.BuildingWearsInYears);
        Household home = world.Households.First(h => h.HasHome);
        home.HomeRaisedAtTick = YearsAgo(world, world.Config.BuildingWearsInYears);
        home.HomeRepairedAtTick = 0;
        int places = hut.Places;
        StepToTheSeasonTurn(loop);

        Assert.NotNull(RepairSiteOf(world, hut));
        Assert.NotNull(world.RepairSiteAt(home.HomeTile!.Value));
        Assert.Equal(places, hut.Places);
        Assert.NotEqual(Shelter.Outdoors, world.ShelterAt(home.HomeTile!.Value));
        Assert.Same(hut, world.StandingWorkplaceCovering(hut.Tile));
    }

    /// <summary>A demolition cancels the repair of the thing coming down.</summary>
    [Fact]
    public void ADemolitionCancelsTheRepair()
    {
        SimLoop loop = AVillageAfter(VillageFixtures.Village.BuildingWearsInYears + 1);
        SimWorld world = loop.World;
        Workplace hut = WornHut(world, world.Config.BuildingWearsInYears);
        StepToTheSeasonTurn(loop);
        Assert.NotNull(RepairSiteOf(world, hut));

        Assert.True(world.MarkDemolition(hut.Tile).Allowed);
        Assert.Null(RepairSiteOf(world, hut));
        Assert.NotNull(world.DemolitionSiteAt(hut.Tile));
    }

    // ---------------------------------------------------------------
    //  §4.5 — closed
    // ---------------------------------------------------------------

    [Fact]
    public void ARuinedHutHasNoPlacesAndSaysSo()
    {
        SimLoop loop = AVillageAfter(VillageFixtures.Village.BuildingRuinedInYears + 1);
        SimWorld world = loop.World;
        Workplace hut = WornHut(world, world.Config.BuildingRuinedInYears);
        Assert.False(hut.Closed);
        StepToTheSeasonTurn(loop);

        Assert.True(hut.Closed);
        Assert.Equal(0, hut.Places);
        Assert.Empty(hut.WorkerIds);
        Assert.Contains("fallen in", world.IdleNote(hut)!, StringComparison.Ordinal);
    }

    [Fact]
    public void ARuinedStoreTakesNothingInAndStillGives()
    {
        SimLoop loop = AVillageAfter(VillageFixtures.Village.BuildingRuinedInYears + 1);
        SimWorld world = loop.World;
        StoreBuilding store = world.StoreBuildings.First(s => s.Kind == StoreKind.Warehouse);
        int held = store.Store[Goods.Logs];
        Assert.True(held > 0, "the warehouse holds no logs, so giving cannot be shown");
        store.RaisedAtTick = YearsAgo(world, world.Config.BuildingRuinedInYears);
        store.RepairedAtTick = 0;
        StepToTheSeasonTurn(loop);

        Assert.True(store.Closed);
        Assert.False(store.Accepts(Goods.Logs));
        Assert.False(store.HasRoomFor(Goods.Logs));

        // ⚠️ And the village still sees and fetches what is inside (D553 call 4): the fetch paths asked `Accepts` for
        // "who holds this", so closing a store hid its stock — a fallen-in granary starved the people beside it.
        Assert.True(store.Gives(Goods.Logs));
        int inside = store.Store[Goods.Logs];
        Assert.True(world.InStores(Goods.Logs) >= inside);
        foreach (StoreBuilding other in world.StoreBuildings.Where(st => st.Id != store.Id))
        {
            other.Store.TryTake(Goods.Logs, other.Store[Goods.Logs]);
        }

        Assert.Equal(inside, world.InStores(Goods.Logs));
        Assert.True(world.AnyStoreHolding(Goods.Logs), "a fallen-in warehouse's logs vanished from the village's count");
        Assert.True(store.Store.TryTake(Goods.Logs, 1));
    }

    [Fact]
    public void ARuinedHomeSheltersNobodyAndLightsNoFire()
    {
        SimLoop loop = AVillageAfter(VillageFixtures.Village.BuildingRuinedInYears + 1);
        SimWorld world = loop.World;
        Household home = world.Households.First(h => h.HasHome);
        home.HomeRaisedAtTick = YearsAgo(world, world.Config.BuildingRuinedInYears);
        home.HomeRepairedAtTick = 0;
        StepToTheSeasonTurn(loop);

        Assert.True(home.HomeClosed);
        Assert.Equal(Shelter.Outdoors, world.ShelterAt(home.HomeTile!.Value));
        Assert.True(home.HasHome, "the family keeps the house; it is closed, not gone");
    }

    [Fact]
    public void AMendedRuinOpensAgain()
    {
        SimLoop loop = AVillageAfter(VillageFixtures.Village.BuildingRuinedInYears + 1);
        SimWorld world = loop.World;
        Workplace hut = WornHut(world, world.Config.BuildingRuinedInYears);
        StepToTheSeasonTurn(loop);
        Assert.True(hut.Closed);
        Workplace site = RepairSiteOf(world, hut)!;
        BuildFixtures.StockTheSite(site);
        for (int i = 0; i <= site.Construction!.Recipe.WorkTicks; i++) { site.Construction.Work(); }
        world.Complete(site);

        Assert.False(hut.Closed);
        Assert.Equal(Condition.Sound, world.ConditionOf(hut));
        Assert.Equal(world.Tick, hut.RepairedAtTick);
    }

    // ---------------------------------------------------------------
    //  The walk is seasonal; the flags are hashed and saved
    // ---------------------------------------------------------------

    [Fact]
    public void TheWalkRunsFourTimesAYearNotEveryTick()
    {
        SimLoop loop = AVillage();
        SimWorld world = loop.World;
        loop.Step(7);
        int before = world.ConditionWalks;
        loop.Step(world.Config.TicksPerYear);
        Assert.Equal(4, world.ConditionWalks - before);
    }

    [Fact]
    public void TheClosedFlagsAreHashedAndSaved()
    {
        SimLoop loop = AVillage();
        SimWorld world = loop.World;
        loop.Step(5);
        Workplace hut = world.Workplaces.First(w => !w.IsSite && w.Kind == JobKind.Woodcutter);
        StoreBuilding store = world.StoreBuildings.First(s => s.Kind == StoreKind.Warehouse);
        Household home = world.Households.First(h => h.HasHome);
        ulong open = StateHash.Compute(world);
        hut.Closed = true;
        Assert.NotEqual(open, StateHash.Compute(world));
        hut.Closed = false;
        store.Closed = true;
        Assert.NotEqual(open, StateHash.Compute(world));
        store.Closed = false;
        home.HomeClosed = true;
        Assert.NotEqual(open, StateHash.Compute(world));

        // All three closed for the save: with only the home closed, a store's flag left out of the save scored zero.
        hut.Closed = true;
        store.Closed = true;
        SimLoop loaded = SaveGame.Load(
            JsonNode.Parse(SaveFile.TextOf(SaveGame.Capture(loop, "test", "now", "code")))!.AsObject(), world.Config);
        Assert.Equal(StateHash.Compute(world), StateHash.Compute(loaded.World));
        Assert.True(loaded.World.Households.Single(h => h.Id == home.Id).HomeClosed);
        Assert.True(loaded.World.Workplaces.Single(w => w.Id == hut.Id).Closed);
        Assert.True(loaded.World.StoreBuildings.Single(st => st.Id == store.Id).Closed);
    }

    /// <summary>
    /// ⚠️ A repair mends everything standing on its tile: the market is a store and a workplace on one tile, and
    /// stamping only the first left the other half worn — a fresh repair raised for it every season.
    /// </summary>
    [Fact]
    public void AMendedMarketIsMendedWhole()
    {
        SimLoop loop = AVillageAfter(VillageFixtures.Village.BuildingWearsInYears + 1);
        SimWorld world = loop.World;
        StoreBuilding stall = world.StoreBuildings.First(st => st.Kind == StoreKind.Market);
        Workplace counter = world.StandingWorkplaceCovering(stall.Tile)!;
        Assert.Equal(JobKind.Marketer, counter.Kind);
        stall.RaisedAtTick = YearsAgo(world, world.Config.BuildingWearsInYears);
        stall.RepairedAtTick = 0;
        counter.RaisedAtTick = YearsAgo(world, world.Config.BuildingWearsInYears);
        counter.RepairedAtTick = 0;
        StepToTheSeasonTurn(loop);

        Workplace repair = world.RepairSiteAt(stall.Tile)!;
        Assert.NotNull(repair);
        BuildFixtures.StockTheSite(repair);
        for (int i = 0; i <= repair.Construction!.Recipe.WorkTicks; i++) { repair.Construction.Work(); }
        world.Complete(repair);

        Assert.Equal(world.Tick, stall.RepairedAtTick);
        Assert.Equal(world.Tick, counter.RepairedAtTick);
        Assert.Equal(Condition.Sound, world.ConditionOf(counter));
    }
}

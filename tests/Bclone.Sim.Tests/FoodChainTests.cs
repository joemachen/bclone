using Bclone.Sim.Config;
using Bclone.Sim.Core;
using Bclone.Sim.Determinism;
using Bclone.Sim.Logging;
using Bclone.Sim.World;
using Xunit;
using Xunit.Abstractions;

namespace Bclone.Sim.Tests;

/// <summary>
/// ⭐ The mill and the bakery (`specs/food-chain.md §4–§7`, D522): wheat to flour, flour to bread, each a
/// stint at its hut with every reason it stands still in one sentence, and each learned by doing.
/// </summary>
public sealed class FoodChainTests
{
    private readonly ITestOutputHelper _output;

    public FoodChainTests(ITestOutputHelper output) => _output = output;

    private static SimConfig Config => VillageFixtures.Village;

    // ---------------------------------------------------------------
    //  § The mill
    // ---------------------------------------------------------------

    /// <summary>The woodcutter's stint, one good over: wheat out of a granary, flour into a warehouse.</summary>
    [Fact]
    public void AMillerGrindsWheatIntoFlour()
    {
        SimConfig config = Config;
        SimLoop loop = SimFactory.CreatePhase0(config, new InMemoryLogSink());
        SimWorld world = loop.World;

        Workplace mill = RaiseA(world, BuildingKind.Mill);
        ToolsTests.KeepTheVillageWarmAndFed(world);
        OnlyOneTradeWorks(world, JobKind.Miller);
        TheGranary(world).Store.Add(Goods.Wheat, 400);
        int wheatBefore = WheatInStores(world);

        int grindingTicks = 0;
        for (int tick = 0; tick < config.TicksPerSeason && world.FlourEverGround < config.FlourPerGrind * 4; tick++)
        {
            loop.StepOnce();
            grindingTicks += world.Villagers.Count(v => v.State == VillagerState.Grinding && v.Tile == mill.Tile);
        }

        int wheatSpent = wheatBefore - WheatInStores(world);
        int grinds = wheatSpent / config.WheatPerGrind;
        _output.WriteLine($"{world.FlourEverGround} flour ground in {grinds} grinds from {wheatSpent} wheat; {world.InStores(Goods.Flour)} on the shelves; {grindingTicks} villager-ticks at the stones");

        Assert.True(grinds > 0, "A staffed mill with wheat in the granary ground nothing.");
        Assert.Equal(grinds * config.WheatPerGrind, wheatSpent);

        // ⛔ The recipe, to the unit: a tool quickens a grind and never makes flour from nothing.
        Assert.Equal(grinds * config.FlourPerGrind, world.FlourEverGround);
        Assert.True(world.InStores(Goods.Flour) > 0, "The flour went nowhere a baker can find it.");
        Assert.True(grindingTicks >= grinds, "The flour came without the miller being seen at the stones.");
    }

    /// <summary>
    /// ⛔ The mill never grinds while the village is short of food (§5 rule 1): flour is not food.
    /// Red with the guard off.
    /// </summary>
    [Fact]
    public void TheMillWaitsWhileTheVillageIsHungry()
    {
        SimConfig config = Config;
        SimLoop loop = SimFactory.CreatePhase0(config, new InMemoryLogSink());
        SimWorld world = loop.World;

        Workplace mill = RaiseA(world, BuildingKind.Mill);
        OnlyOneTradeWorks(world, JobKind.Miller);
        foreach (Household household in world.Households)
        {
            foreach (Goods good in world.GoodsCatalog.EdibleGoods)
            {
                household.Stockpile.TakeAll(good);
            }
        }

        foreach (StoreBuilding store in world.StoreBuildings)
        {
            foreach (Goods good in world.GoodsCatalog.EdibleGoods)
            {
                store.Store.TakeAll(good);
            }
        }

        TheGranary(world).Store.Add(Goods.Wheat, config.WheatPerGrind);
        Assert.True(LabourQuota.VillageIsShortOfFood(world), "The premise: the village is hungry.");

        int groundBefore = world.FlourEverGround;
        loop.Step(config.TicksPerDay * 4);

        Villager? miller = world.Villagers.FirstOrDefault(v => v.Alive && v.WorkplaceId == mill.Id);
        _output.WriteLine($"{miller?.Name ?? "nobody"} holds the mill; note: {miller?.WorkNote}; ground {world.FlourEverGround - groundBefore}");
        Assert.Equal(groundBefore, world.FlourEverGround);
        Assert.Contains("needs its grain to eat", world.WhyTheMillIsStill(mill), StringComparison.Ordinal);
    }

    // ---------------------------------------------------------------
    //  § The bakery
    // ---------------------------------------------------------------

    /// <summary>
    /// The forge's shape: flour and the oven's firewood from the one store holding both, bread into a
    /// granary — and the oven lit once a stint, not once a bake (§8.1 finding 6).
    /// </summary>
    [Fact]
    public void ABakerBakesFlourIntoBreadAndLightsTheOvenOnceAStint()
    {
        SimConfig config = Config;
        SimLoop loop = SimFactory.CreatePhase0(config, new InMemoryLogSink());
        SimWorld world = loop.World;

        Workplace bakery = RaiseA(world, BuildingKind.Bakery);
        ToolsTests.KeepTheVillageWarmAndFed(world);
        OnlyOneTradeWorks(world, JobKind.Baker);
        StoreBuilding warehouse = TheWarehouse(world);
        warehouse.Store.Add(Goods.Flour, 400);
        int flourBefore = warehouse.Store[Goods.Flour];
        int firewoodBefore = warehouse.Store[Goods.Firewood];

        for (int tick = 0; tick < config.TicksPerSeason && world.BreadEverBaked < config.BreadPerBake * config.BakesPerStint * 2; tick++)
        {
            loop.StepOnce();
        }

        int flourSpent = flourBefore - warehouse.Store[Goods.Flour];
        int bakes = flourSpent / config.FlourPerBake;
        int firewoodSpent = firewoodBefore - warehouse.Store[Goods.Firewood];
        _output.WriteLine($"{world.BreadEverBaked} bread in {bakes} bakes from {flourSpent} flour and {firewoodSpent} firewood; {world.InStores(Goods.Bread)} on the shelves");

        Assert.True(bakes >= 2, "A staffed bakery with flour and firewood baked next to nothing.");
        Assert.Equal(bakes * config.FlourPerBake, flourSpent);
        Assert.Equal(bakes * config.BreadPerBake, world.BreadEverBaked);
        Assert.True(firewoodSpent >= 1, "The oven was never lit.");
        Assert.True(firewoodSpent < bakes, $"The oven burned {firewoodSpent} firewood for {bakes} bakes — once a bake, not once a stint.");
        Assert.True(TheGranary(world).Store[Goods.Bread] > 0, "No bread reached a granary, where the birth gate reads it.");
        Assert.True(world.StoreBuildings.Where(s => s.Kind == StoreKind.Market).All(s => s.Store[Goods.Bread] == 0),
            "A baker put bread in the market — only a trader stocks the counter (D358).");
    }

    /// <summary>
    /// ⛔ The oven never burns firewood the homes still want (§5 rule 2, the forge's rule). Red with the
    /// guard off.
    /// </summary>
    [Fact]
    public void TheOvenNeverBurnsTheWintersFirewood()
    {
        SimConfig config = Config;
        SimLoop loop = SimFactory.CreatePhase0(config, new InMemoryLogSink());
        SimWorld world = loop.World;

        Workplace bakery = RaiseA(world, BuildingKind.Bakery);
        OnlyOneTradeWorks(world, JobKind.Baker);
        foreach (Household household in world.Households)
        {
            household.Stockpile.Add(Goods.Produce, world.TargetFoodFor(household));
        }

        StoreBuilding warehouse = TheWarehouse(world);
        warehouse.Store.Add(Goods.Flour, 200);
        if (warehouse.Store[Goods.Firewood] < config.FirewoodPerFiring)
        {
            warehouse.Store.Add(Goods.Firewood, config.FirewoodPerFiring);
        }

        Assert.True(LabourQuota.FirewoodShortfall(world) > 0, "The premise: the homes are still owed firewood.");

        loop.Step(config.TicksPerSeason);

        _output.WriteLine($"baked {world.BreadEverBaked}; the oven: {world.WhyTheOvenIsCold(bakery)}");
        Assert.Equal(0, world.BreadEverBaked);
        Assert.Contains("firewood for the winter", world.WhyTheOvenIsCold(bakery), StringComparison.Ordinal);
    }

    /// <summary>A met limit stops the work, not just the hiring (D139) — flour at the mill, bread at the oven.</summary>
    [Fact]
    public void AMetLimitStopsTheMillAndTheOven()
    {
        SimConfig config = Config;
        SimLoop loop = SimFactory.CreatePhase0(config, new InMemoryLogSink());
        SimWorld world = loop.World;

        Workplace mill = RaiseA(world, BuildingKind.Mill);
        Workplace bakery = RaiseA(world, BuildingKind.Bakery);
        ToolsTests.KeepTheVillageWarmAndFed(world);
        OnlyOneTradeWorks(world, JobKind.Miller, JobKind.Baker);
        TheGranary(world).Store.Add(Goods.Wheat, 200);
        TheWarehouse(world).Store.Add(Goods.Flour, 200);
        Assert.True(world.SetStockLimit(Goods.Flour, 0).Allowed);
        Assert.True(world.SetStockLimit(Goods.Bread, 0).Allowed);
        int groundBefore = world.FlourEverGround;

        loop.Step(config.TicksPerSeason);

        _output.WriteLine($"the mill: {world.WhyTheMillIsStill(mill)}; the oven: {world.WhyTheOvenIsCold(bakery)}");
        Assert.Equal(groundBefore, world.FlourEverGround);
        Assert.Equal(0, world.BreadEverBaked);
        Assert.Contains("asked the village to keep 0 flour", world.WhyTheMillIsStill(mill), StringComparison.Ordinal);
        Assert.Contains("asked the village to keep 0 bread", world.WhyTheOvenIsCold(bakery), StringComparison.Ordinal);
    }

    /// <summary>
    /// ⭐⭐ Joe's §9b call (a): the mill answers the farm that stops for good. Wheat held over its limit is
    /// ground and baked until the limit stops being met — so the farm would sow again.
    /// </summary>
    [Fact]
    public void TheMillDrawsStoredWheatBelowItsLimit()
    {
        SimConfig config = Config;
        SimLoop loop = SimFactory.CreatePhase0(config, new InMemoryLogSink());
        SimWorld world = loop.World;

        RaiseA(world, BuildingKind.Mill);
        RaiseA(world, BuildingKind.Bakery);
        ToolsTests.KeepTheVillageWarmAndFed(world);
        OnlyOneTradeWorks(world, JobKind.Miller, JobKind.Baker);
        TheGranary(world).Store.Add(Goods.Wheat, 300);
        Assert.True(world.SetStockLimit(Goods.Wheat, 100).Allowed);
        Assert.True(world.LimitIsMet(Goods.Wheat), "The premise: the wheat limit is met, so a farm would not sow.");

        int ticks = 0;
        while (world.LimitIsMet(Goods.Wheat) && ticks < config.TicksPerSeason * 2)
        {
            loop.StepOnce();
            ticks++;
        }

        _output.WriteLine($"after {ticks} ticks: wheat held {world.HeldAgainstItsLimit(Goods.Wheat)}, flour ground {world.FlourEverGround}, bread baked {world.BreadEverBaked}");
        Assert.False(world.LimitIsMet(Goods.Wheat), "Two seasons of milling and the wheat limit is still met.");
        Assert.True(world.BreadEverBaked > 0, "The wheat went down, but not into bread.");
    }

    // ---------------------------------------------------------------
    //  § The gifts (§7)
    // ---------------------------------------------------------------

    /// <summary>
    /// ⭐ The mill is learned by reaping wheat, and only reaping counts: the counter is the wheat the
    /// fields brought in, to the unit, and crossing it is a stopping moment.
    /// </summary>
    [Fact]
    public void TheMillIsLearnedByReapingWheat()
    {
        SimConfig config = Config with { MillUnlockWheat = 1 };
        SimLoop loop = SimFactory.CreatePhase0(config, new InMemoryLogSink());
        SimWorld world = loop.World;
        string? before = world.WhyNotYet(BuildingKind.Mill);
        _output.WriteLine(before);
        Assert.Contains("reaped", before, StringComparison.Ordinal);

        TheGranary(world).Store.Add(Goods.Wheat, 500);
        Assert.Equal(0, world.WheatEverReaped);

        Workplace farm = FarmFixtures.RaiseAFarm(world);
        FarmFixtures.GiveItGround(world, farm, 3);
        world.Moments.Clear();
        for (int t = 0; t < config.TicksPerYear * 2 && world.WheatEverReaped == 0; t++)
        {
            loop.StepOnce();
        }

        _output.WriteLine($"reaped {world.WheatEverReaped}; produced {world.FoodEverProducedOf(Goods.Wheat)}; moments: {string.Join(" | ", world.Moments.Select(m => m.Title))}");
        Assert.True(world.WheatEverReaped > 0, "The farm reaped nothing in two years.");
        Assert.Equal(world.FoodEverProducedOf(Goods.Wheat), world.WheatEverReaped);
        Assert.True(world.IsUnlocked(BuildingKind.Mill));
        Assert.Contains(world.Moments, m => m.Title.Contains("mill", StringComparison.Ordinal) && m.WaitsToBeDismissed);
    }

    /// <summary>The bakery is learned by the mill's first flour (Joe: *"after the first flour"*).</summary>
    [Fact]
    public void TheBakeryIsLearnedByTheFirstFlour()
    {
        SimConfig config = Config;
        SimLoop loop = SimFactory.CreatePhase0(config, new InMemoryLogSink());
        SimWorld world = loop.World;
        Assert.False(world.IsUnlocked(BuildingKind.Bakery));
        Assert.Contains("flour", world.WhyNotYet(BuildingKind.Bakery), StringComparison.Ordinal);

        RaiseA(world, BuildingKind.Mill);
        ToolsTests.KeepTheVillageWarmAndFed(world);
        OnlyOneTradeWorks(world, JobKind.Miller);
        TheGranary(world).Store.Add(Goods.Wheat, 200);
        world.Moments.Clear();
        for (int t = 0; t < config.TicksPerSeason && world.FlourEverGround == 0; t++)
        {
            loop.StepOnce();
        }

        Assert.True(world.FlourEverGround >= config.BakeryUnlockFlour);
        Assert.True(world.IsUnlocked(BuildingKind.Bakery));
        Assert.Contains(world.Moments, m => m.Title.Contains("bak", StringComparison.Ordinal));
    }

    /// <summary>The two counters are the village's identity, sparsely: they decide what may be built.</summary>
    [Fact]
    public void TheGiftCountersAreInTheFingerprint()
    {
        SimWorld world = SimFactory.CreatePhase0(Config, new InMemoryLogSink()).World;
        ulong before = StateHash.Compute(world);
        world.WheatEverReaped = 1;
        ulong reaped = StateHash.Compute(world);
        world.FlourEverGround = 1;
        Assert.NotEqual(before, reaped);
        Assert.NotEqual(reaped, StateHash.Compute(world));
    }

    // ---------------------------------------------------------------
    //  § The quota (§6)
    // ---------------------------------------------------------------

    /// <summary>Bakers are wanted only with flour to bake; millers never while the village is hungry.</summary>
    [Fact]
    public void BakersAndMillersAreWantedOnlyWithWorkToDo()
    {
        SimConfig config = Config;
        SimLoop loop = SimFactory.CreatePhase0(config, new InMemoryLogSink());
        SimWorld world = loop.World;
        RaiseA(world, BuildingKind.Mill);
        RaiseA(world, BuildingKind.Bakery);
        ToolsTests.KeepTheVillageWarmAndFed(world);

        Assert.Equal(0, LabourQuota.BakersWanted(world));
        Assert.Equal(0, LabourQuota.MillersWanted(world));

        TheWarehouse(world).Store.Add(Goods.Flour, 200);
        TheGranary(world).Store.Add(Goods.Wheat, 200);
        Workplace oven = world.Workplaces.First(w => w.Kind == JobKind.Baker && !w.IsSite);
        _output.WriteLine($"with flour and wheat: bakers {LabourQuota.BakersWanted(world)}, millers {LabourQuota.MillersWanted(world)}; the oven: {world.WhyTheOvenIsCold(oven)}");
        Assert.True(LabourQuota.BakersWanted(world) > 0, "Flour to bake and no baker wanted.");

        TheWarehouse(world).Store.TakeAll(Goods.Flour);
        Assert.True(LabourQuota.MillersWanted(world) > 0, "Wheat to grind, the bakery short of flour, and no miller wanted.");

        // Hungry: nothing to eat at home or in store but one grind's wheat (the store's food counts too).
        foreach (Goods good in world.GoodsCatalog.EdibleGoods)
        {
            foreach (Household household in world.Households)
            {
                household.Stockpile.TakeAll(good);
            }

            foreach (StoreBuilding store in world.StoreBuildings)
            {
                store.Store.TakeAll(good);
            }
        }

        TheGranary(world).Store.Add(Goods.Wheat, config.WheatPerGrind);
        Assert.True(LabourQuota.VillageIsShortOfFood(world), "The premise: the village is hungry.");
        Assert.Equal(0, LabourQuota.MillersWanted(world));
    }

    // ---------------------------------------------------------------
    //  § Rows
    // ---------------------------------------------------------------

    /// <summary>
    /// ⛔ The miller's and baker's skill rows move no founding (§9 call 3): the founders' trades are drawn
    /// from `founding_trades` since D392, never from the catalogue's length. Red if the draw reads the
    /// catalogue again.
    /// </summary>
    [Fact]
    public void NoFoundingMovesWithTheNewSkillRows()
    {
        SimConfig with = ShippedConfig.Load();
        SimConfig without = with with
        {
            Skills = with.Skills.Where(s => s.GrownBy is not (JobKind.Miller or JobKind.Baker)).ToList(),
        };
        Assert.True(with.Skills.Count > without.Skills.Count, "The premise: the shipped skills include the new rows.");

        SimWorld a = SimFactory.CreatePhase0(with, new InMemoryLogSink()).World;
        SimWorld b = SimFactory.CreatePhase0(without, new InMemoryLogSink()).World;
        Assert.Equal(StateHash.Compute(b), StateHash.Compute(a));
    }

    /// <summary>Bread is food and flour is not; flour is kept dry (a warehouse), bread where food is kept.</summary>
    [Fact]
    public void BreadIsFoodAndFlourIsNot()
    {
        var goods = new GoodsCatalog(Config.GoodsCatalog);
        Assert.True(goods.Edible(Goods.Bread));
        Assert.False(goods.Edible(Goods.Flour));
        Assert.Contains(StoreKind.Granary, goods[Goods.Bread].StoredBy);
        Assert.DoesNotContain(StoreKind.Granary, goods[Goods.Flour].StoredBy);
        Assert.Contains(StoreKind.Warehouse, goods[Goods.Flour].StoredBy);
        Assert.Equal(GoodCategory.Food, goods[Goods.Bread].Category);
    }

    // ---------------------------------------------------------------
    //  helpers
    // ---------------------------------------------------------------

    private static StoreBuilding TheWarehouse(SimWorld world) =>
        world.StoreBuildings.First(s => s.Kind == StoreKind.Warehouse);

    private static StoreBuilding TheGranary(SimWorld world) =>
        world.StoreBuildings.First(s => s.Kind == StoreKind.Granary);

    private static int WheatInStores(SimWorld world) => world.StoreBuildings.Sum(s => s.Store[Goods.Wheat]);

    /// <summary>These trades and nobody else, one hand each, so the only work is the work being watched.</summary>
    internal static void OnlyOneTradeWorks(SimWorld world, params JobKind[] trades)
    {
        Assert.True(world.SetStockLimit(Goods.Produce, 1).Allowed);
        foreach (JobKind kind in JobLimits.Kinds)
        {
            world.SetJobLimit(kind, trades.Contains(kind) ? 1 : 0);
        }
    }

    /// <summary>Raise a mill or a bakery near the founding, the village lent what it takes to know how — that one only.</summary>
    internal static Workplace RaiseA(SimWorld world, BuildingKind kind)
    {
        if (kind == BuildingKind.Mill)
        {
            world.WheatEverReaped = Math.Max(world.WheatEverReaped, world.Config.MillUnlockWheat);
        }
        else
        {
            world.FlourEverGround = Math.Max(world.FlourEverGround, world.Config.BakeryUnlockFlour);
        }

        GridPos site = world.Map.FoundingSite;
        GridPos? at = null;
        for (int radius = 3; radius < 30 && at is null; radius++)
        {
            for (int dy = -radius; dy <= radius && at is null; dy++)
            {
                for (int dx = -radius; dx <= radius && at is null; dx++)
                {
                    var tile = new GridPos(site.X + dx, site.Y + dy);
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

        GridPos stood = plan.Tile;
        world.Complete(plan);
        return world.Workplaces.Single(w => !w.IsSite && w.Tile == stood && world.BuildingsCatalog.EmployedBy(kind) == w.Kind);
    }
}

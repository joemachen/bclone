using Bclone.Sim.Config;
using Bclone.Sim.Core;
using Bclone.Sim.Logging;
using Bclone.Sim.World;
using Xunit;
using Xunit.Abstractions;

namespace Bclone.Sim.Tests;

/// <summary>
/// ⭐ The hunter's lodge — <b>the food that works in winter, and the first hide</b>.
/// </summary>
/// <remarks>
/// <para>
/// <b>Joe, 2026-09-02:</b> *"Hunting ultimately yields more food, but it takes longer and isn't
/// instantaneous. 3 hunters per hunting lodge. Different types of game meat. And leather."*
/// </para>
/// <para>
/// ⛔⛔ <b>THE END-TO-END GUARD GOES FIRST, BECAUSE FISHING SHIPPED BROKEN TWICE.</b> It was
/// unstaffable (D279) and then unable to walk (D281), and on both occasions every placement and
/// yield guard passed while the building did nothing at all. *A trade the labour system does not
/// know about is a building that cannot work.*
/// </para>
/// </remarks>
public sealed class HuntingTests
{
    private readonly ITestOutputHelper _output;

    public HuntingTests(ITestOutputHelper output) => _output = output;

    private static SimConfig Config => VillageFixtures.Village;

    /// <summary>A buildable tile with woods in reach of it.</summary>
    private static GridPos AWoodedTile(SimWorld world)
    {
        GridPos site = world.Map.FoundingSite;
        for (int radius = 1; radius < 60; radius++)
        {
            for (int dy = -radius; dy <= radius; dy++)
            {
                for (int dx = -radius; dx <= radius; dx++)
                {
                    var at = new GridPos(site.X + dx, site.Y + dy);
                    if (world.CanBuildAt(BuildingKind.HunterLodge, at).Allowed)
                    {
                        return at;
                    }
                }
            }
        }

        throw new Xunit.Sdk.XunitException("Nowhere near the woods was buildable.");
    }

    private static Workplace RaiseALodge(SimWorld world)
    {
        world.Mark(BuildingKind.HunterLodge, AWoodedTile(world));
        Workplace site = world.Workplaces.Single(
            w => w.Construction?.Kind == BuildingKind.HunterLodge);

        BuildFixtures.StockTheSite(site);
        for (int i = 0; i <= site.Construction!.Recipe.WorkTicks; i++)
        {
            site.Construction.Work();
        }

        world.Complete(site);
        return world.Workplaces.Single(w => w.Kind == JobKind.Hunter && !w.IsSite);
    }

    // ---------------------------------------------------------------
    //  § It needs something to hunt
    // ---------------------------------------------------------------

    /// <summary>⭐ A lodge is refused where there are no woods, and told why.</summary>
    /// <remarks>
    /// <b>The fishing hut's rule one building over, and a REACH rather than a TOUCH.</b> A fishery
    /// stands on the bank; a lodge stands where its range holds trees. Asking it to touch one
    /// would put every lodge in the treeline and none of them near a village. ⚠️ The claim is
    /// about the SENTENCE as much as the refusal (D43).
    /// </remarks>
    [Fact]
    public void ALodgeNeedsWoodsWithinReach()
    {
        SimLoop loop = SimFactory.CreatePhase0(Config, new InMemoryLogSink());
        SimWorld world = loop.World;

        GridPos wooded = AWoodedTile(world);
        Assert.True(world.CanBuildAt(BuildingKind.HunterLodge, wooded).Allowed);

        // Somewhere buildable with nothing to hunt near it.
        GridPos bare = default;
        bool found = false;
        for (int radius = 1; radius < 60 && !found; radius++)
        {
            for (int dy = -radius; dy <= radius && !found; dy++)
            {
                for (int dx = -radius; dx <= radius && !found; dx++)
                {
                    var at = new GridPos(
                        world.Map.FoundingSite.X + dx, world.Map.FoundingSite.Y + dy);
                    if (world.CanBuildAt(BuildingKind.Granary, at).Allowed
                        && world.ForestTilesWithin(at, world.Config.HuntingRadius) == 0)
                    {
                        bare = at;
                        found = true;
                    }
                }
            }
        }

        if (!found)
        {
            _output.WriteLine("every buildable tile in this valley has woods in reach");
            return;
        }

        PlacementVerdict verdict = world.CanBuildAt(BuildingKind.HunterLodge, bare);
        _output.WriteLine($"on bare ground at {bare}: {verdict.Reason}");

        Assert.False(verdict.Allowed);
        Assert.Contains("woods", verdict.Reason, System.StringComparison.OrdinalIgnoreCase);
    }

    // ---------------------------------------------------------------
    //  § Somebody actually works there
    // ---------------------------------------------------------------

    /// <summary>
    /// ⭐⭐⭐ A hunter <b>reaches the lodge, takes meat and a hide, and the village can eat it</b>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>⛔ THIS IS THE GUARD THAT WOULD HAVE CAUGHT D279 AND D281, AND IT IS WRITTEN FIRST.</b>
    /// Fishing shipped twice with a good, a building, a placement rule, a behaviour branch and a
    /// build button — and no <c>LabourQuota</c> arm the first time, no <c>ErrandKind</c> arm the
    /// second. Both times the suite was green and the building did nothing.
    /// </para>
    /// <para>
    /// ⭐ <b>It asserts the whole chain in one run</b>: the village wants hunters, posts one, they
    /// walk out, the lodge fills with meat, and leather — which nothing yet spends — piles up.
    /// </para>
    /// </remarks>
    [Fact]
    public void AHunterWorksTheLodgeAndBringsBackMeatAndLeather()
    {
        SimConfig config = Config;
        SimLoop loop = SimFactory.CreatePhase0(config, new InMemoryLogSink());
        SimWorld world = loop.World;

        Workplace lodge = RaiseALodge(world);
        loop.Step(config.TicksPerYear + 1);

        // The village asked for hunters and posted somebody.
        LabourQuota quota = LabourQuota.For(world);
        Villager? hunter = world.Villagers.FirstOrDefault(
            v => v.Alive && v.WorkplaceId == lodge.Id);

        _output.WriteLine(
            $"the village wants {quota.For(JobKind.Hunter)} hunters of {lodge.Capacity} seats; "
            + $"{(hunter is null ? "nobody" : hunter.Name)} holds the job");

        Assert.NotNull(hunter);

        // Give them a year at it, keeping somewhere for the catch to go.
        int meat = 0;
        int leather = 0;
        for (int tick = 0; tick < config.TicksPerYear * 2 && meat == 0; tick++)
        {
            loop.StepOnce();
            meat = lodge.Store[Goods.Meat] + hunter!.Carried[Goods.Meat];
            leather = LeatherEverywhere(world);
        }

        _output.WriteLine(
            $"{hunter!.Name} took {meat} meat, and the village holds {leather} leather");

        Assert.True(meat > 0, "The lodge was staffed and no meat was ever taken.");
        Assert.True(leather > 0, "Meat came back and no hide did — the by-product is missing.");
        Assert.Equal(config.HunterLodgeSeats, lodge.Capacity);
    }

    /// <summary>
    /// ⛔ <b>Winter does not recall a hunter</b> — which is the whole argument for the building.
    /// </summary>
    /// <remarks>
    /// <b>D3057 chose hunting over livestock for *"year-round outdoor work for the 86%-idle
    /// winter"*.</b> Nothing can be picked in winter (D44) and game does not stop, so a hunter
    /// marched home in December would delete the single best reason the lodge exists. ⚠️ It is
    /// the D281 trap by name: <c>IsForaging</c> is what the recall reads, and hunting must not be
    /// in it.
    /// </remarks>
    [Fact]
    public void WinterDoesNotRecallAHunter()
    {
        SimConfig config = Config;
        SimLoop loop = SimFactory.CreatePhase0(config, new InMemoryLogSink());
        SimWorld world = loop.World;

        Workplace lodge = RaiseALodge(world);
        loop.Step(config.TicksPerYear + 1);

        Villager hunter = world.Villagers.First(v => v.Alive && v.WorkplaceId == lodge.Id);
        hunter.Position = lodge.Position;

        int taken = 0;
        for (int tick = 0; tick < config.TicksPerYear * 2 && taken == 0; tick++)
        {
            loop.StepOnce();

            if (world.Clock.Season != Season.Winter)
            {
                continue;
            }

            foreach (StoreBuilding store in world.StoreBuildings)
            {
                store.Store.TakeAll(Goods.Food);
            }

            // ⚠️ THE LODGE, NOT THE HUNTER'S ARMS — D290's lesson, applied before it can bite.
            // With a buffer big enough the whole take goes into the lodge and the hunter carries
            // nothing, so a guard watching their hands can be reddened by a store being roomy.
            taken = lodge.Store[Goods.Meat] + hunter.Carried[Goods.Meat];
        }

        _output.WriteLine($"in winter {hunter.Name}'s lodge had taken {taken} meat");
        Assert.True(taken > 0, "Winter stopped the hunting, and game does not stop.");
    }

    // ---------------------------------------------------------------
    //  § What it is worth
    // ---------------------------------------------------------------

    /// <summary>
    /// ⭐⭐ The lodge holds <b>more than one hunt</b>, or it is not a buffer.
    /// </summary>
    /// <remarks>
    /// <b>D290 happened to the fishing hut and is not allowed to happen twice.</b> A catch of 300
    /// against a 300 buffer meant the hut held exactly one cast, so the fisher hauled after every
    /// single one and the marketer had nothing to come for — <b>while both numbers still read as
    /// the ones Joe asked for.</b> A capacity is only meaningful in loads, so this is a ratio.
    /// </remarks>
    [Fact]
    public void TheLodgeHoldsMoreThanOneHunt()
    {
        SimConfig config = Config;
        int hunts = config.HunterLodgeStoreCap / config.MeatYield;

        _output.WriteLine(
            $"a hunt is worth up to {config.MeatYield} and the lodge holds "
            + $"{config.HunterLodgeStoreCap} — {hunts} hunts");

        Assert.True(
            hunts >= 2,
            $"A lodge holds {config.HunterLodgeStoreCap} and a hunt is worth up to "
            + $"{config.MeatYield}, so the buffer takes {hunts} hunt(s). At one, the hunter hauls "
            + "after every hunt and the marketer has nothing to fetch. Raise "
            + "hunter_lodge_store_cap with meat_yield.");
    }

    /// <summary>
    /// ⛔ A lodge <b>competes with nothing over trees</b> — it has no gathering ring.
    /// </summary>
    /// <remarks>
    /// <b>The trap `specs/hunting.md §3` names, guarded.</b> <c>SharersOf</c> asks
    /// <c>GatheringRadius &gt; 0</c> and deliberately never asks <c>JobKind</c>, so a lodge given
    /// a ring would <b>silently start halving foragers' yields by standing near them</b> — over
    /// TREES, which is not what a hunter takes. Game is not wood.
    /// </remarks>
    [Fact]
    public void ALodgeCompetesWithNobodyOverTrees()
    {
        SimConfig config = Config;
        SimLoop loop = SimFactory.CreatePhase0(config, new InMemoryLogSink());
        SimWorld world = loop.World;

        Workplace ring = world.Workplaces.First(w => w.GatheringRadius > 0);
        int before = world.GatherYieldAt(ring);

        // A lodge as close to the forager's hut as the ground allows.
        world.Mark(BuildingKind.HunterLodge, AWoodedTile(world));
        Workplace site = world.Workplaces.Single(
            w => w.Construction?.Kind == BuildingKind.HunterLodge);
        BuildFixtures.StockTheSite(site);
        for (int i = 0; i <= site.Construction!.Recipe.WorkTicks; i++)
        {
            site.Construction.Work();
        }

        world.Complete(site);
        Workplace lodge = world.Workplaces.Single(w => w.Kind == JobKind.Hunter && !w.IsSite);
        int after = world.GatherYieldAt(ring);

        _output.WriteLine(
            $"a forager's trip was worth {before} and is worth {after} with a lodge standing "
            + $"{world.TravelCost.TicksBetween(ring.Position, lodge.Position)} tiles away; "
            + $"the lodge's gathering radius is {lodge.GatheringRadius}");

        Assert.Equal(0, lodge.GatheringRadius);
        Assert.Equal(before, after);
    }

    /// <summary>
    /// ⭐⭐ A hunter <b>out-earns a fisher per tick worked</b> — the top of Joe's totem pole.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Joe:</b> *"Hunting ultimately yields more food… foraging is bottom of the totem pole."*
    /// So the order is <b>hunting → fishing → foraging</b>, and this measures the top two against
    /// each other <b>in one village, on the same tick, with demand held open</b>.
    /// </para>
    /// <para>
    /// ⛔⛔ <b>PER HOUR WORKED, NEVER PER LOAD — AND THAT COMPARISON HAS BEEN WRONG TWICE.</b>
    /// D286: the retired fishing guard read <c>fish_yield</c> (100) against <c>GatherYieldAt</c>
    /// (77) per load and called fishing the winner while it was making <b>311 against 721</b>.
    /// A per-load comparison silently assumes both trades get the same number of loads a year.
    /// </para>
    /// <para>
    /// ⚠️ <b>AND DEMAND HAS TO BE HELD OPEN OR THE NUMBER MEANS NOTHING</b> (D286 again). Work is
    /// gated on the village still WANTING food, so a more productive trade simply works less —
    /// measured on the fishery, raising the yield gave <b>910 a year at 130 and 510 at 170</b>,
    /// and at 300 the fisher worked <b>37 ticks in the whole year</b>. A stockpile target nobody
    /// can reach is what makes the two comparable.
    /// </para>
    /// </remarks>
    [Fact]
    public void AHunterOutEarnsAFisherPerTickWorked()
    {
        SimConfig config = Config with { StockpileTarget = 100_000 };
        SimLoop loop = SimFactory.CreatePhase0(config, new InMemoryLogSink());
        SimWorld world = loop.World;

        Workplace lodge = RaiseALodge(world);
        loop.Step(config.TicksPerYear + 1);

        Villager? hunter = world.Villagers.FirstOrDefault(
            v => v.Alive && v.WorkplaceId == lodge.Id);

        Assert.True(
            hunter is not null, "Nobody was posted to the lodge, so this measures nothing.");

        // ⛔⛔ THE FISHER COMES FROM HIS OWN VILLAGE, AND THE FIRST DRAFT PROVED WHY.
        // Raising both in one valley left NOBODY AT THE FISHERY — hunting is asked first, so it
        // takes the hands and the rival never gets staffed. **That is the ranking working**, and
        // it makes a same-village comparison impossible rather than merely awkward.
        //
        // ⚠️ It is also D286's rule: a baseline that moves with the thing it is guarding is not
        // a baseline. The fishery's rate must not depend on what the lodge is doing.
        SimLoop rival = SimFactory.CreatePhase0(config, new InMemoryLogSink());
        Workplace fishery = RaiseAFisheryBeside(rival.World);
        rival.Step(config.TicksPerYear + 1);

        Villager? fisher = rival.World.Villagers.FirstOrDefault(
            v => v.Alive && v.WorkplaceId == fishery.Id);

        Assert.True(
            fisher is not null, "Nobody was posted to the fishery, so there is no rival.");

        int meat = 0;
        int fish = 0;
        int huntTicks = 0;
        int fishTicks = 0;
        int meatHeld = lodge.Store[Goods.Meat] + hunter!.Carried[Goods.Meat];
        int fishHeld = fishery.Store[Goods.Fish] + fisher!.Carried[Goods.Fish];

        for (int tick = 0; tick < config.TicksPerYear; tick++)
        {
            loop.StepOnce();
            rival.StepOnce();

            if (OnTheJob(hunter.State))
            {
                huntTicks++;
            }

            if (OnTheJob(fisher.State))
            {
                fishTicks++;
            }

            int meatNow = lodge.Store[Goods.Meat] + hunter.Carried[Goods.Meat];
            int fishNow = fishery.Store[Goods.Fish] + fisher.Carried[Goods.Fish];

            if (meatNow > meatHeld)
            {
                meat += meatNow - meatHeld;
            }

            if (fishNow > fishHeld)
            {
                fish += fishNow - fishHeld;
            }

            meatHeld = meatNow;
            fishHeld = fishNow;
        }

        int meatRate = huntTicks == 0 ? 0 : meat * 100 / huntTicks;
        int fishRate = fishTicks == 0 ? 0 : fish * 100 / fishTicks;

        _output.WriteLine(
            $"a hunter brought {meat} over {huntTicks} ticks on the job = {meatRate} per 100 "
            + $"worked; a fisher in his own village brought {fish} over {fishTicks} ticks = "
            + $"{fishRate} per 100 worked");

        Assert.True(huntTicks > 0, "The hunter never worked, so this measures nothing.");
        Assert.True(fishTicks > 0, "The fisher never worked, so there is nothing to compare to.");
        Assert.True(
            meatRate > fishRate,
            $"A hunter made {meatRate} food per 100 ticks worked against a fisher's {fishRate}. "
            + "Joe's ranking is hunting above fishing above foraging, so a lodge has to beat a "
            + "fishery per worker — measured over hours worked, never per load.");
    }

    /// <summary>The states that count as doing the job — the work, the walk out, and the haul.</summary>
    private static bool OnTheJob(VillagerState state) =>
        state is VillagerState.Hunting
            or VillagerState.TravelingToGame
            or VillagerState.Fishing
            or VillagerState.TravelingToWater
            or VillagerState.HaulingToStore;

    private static Workplace RaiseAFisheryBeside(SimWorld world)
    {
        GridPos bank = default;
        bool found = false;
        for (int radius = 1; radius < 60 && !found; radius++)
        {
            for (int dy = -radius; dy <= radius && !found; dy++)
            {
                for (int dx = -radius; dx <= radius && !found; dx++)
                {
                    var at = new GridPos(
                        world.Map.FoundingSite.X + dx, world.Map.FoundingSite.Y + dy);
                    if (world.CanBuildAt(BuildingKind.FishingHut, at).Allowed)
                    {
                        bank = at;
                        found = true;
                    }
                }
            }
        }

        world.Mark(BuildingKind.FishingHut, bank);
        Workplace site = world.Workplaces.Single(
            w => w.Construction?.Kind == BuildingKind.FishingHut);
        BuildFixtures.StockTheSite(site);
        for (int i = 0; i <= site.Construction!.Recipe.WorkTicks; i++)
        {
            site.Construction.Work();
        }

        world.Complete(site);
        return world.Workplaces.Single(w => w.Kind == JobKind.Fisher && !w.IsSite);
    }

    // ---------------------------------------------------------------
    //  § It runs out, and it comes back — slice 2
    // ---------------------------------------------------------------

    /// <summary>
    /// ⭐⭐ Hunting <b>thins the wood, and the wood comes back</b> — the pressure, both halves.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Joe's fishery *"does not run out"*; this does, and the contrast is the design.</b> D3057
    /// chose hunting over livestock partly for *"depletion as a §2.3 pressure"*, and D256 is the
    /// standing complaint it answers for this trade: *"the player can't milk one hut for the whole
    /// game."*
    /// </para>
    /// <para>
    /// ⛔ <b>IT EMPTIES TILES OF GAME AND FELLS NOTHING.</b> Doing this through
    /// <c>ThinTheRingOf</c> — which turns forest into sapling — would have made a hunter a logger
    /// and put lodges back into competition with forager huts over wood, *the exact thing
    /// <c>HuntingRadius</c> exists to prevent* (D292). **The trees are asserted unchanged here**,
    /// because that is the half a future refactor is most likely to break.
    /// </para>
    /// </remarks>
    [Fact]
    public void HuntingThinsTheGameAndItComesBack()
    {
        SimConfig config = Config with { StockpileTarget = 100_000 };
        SimLoop loop = SimFactory.CreatePhase0(config, new InMemoryLogSink());
        SimWorld world = loop.World;

        Workplace lodge = RaiseALodge(world);
        int reach = config.HuntingRadius;

        int treesBefore = world.ForestTilesWithin(lodge.Position, reach);
        int gameBefore = world.GameTilesWithin(lodge.Position, reach);
        int worthBefore = world.HuntYieldAt(lodge);

        loop.Step(config.TicksPerYear * 2);

        int treesAfter = world.ForestTilesWithin(lodge.Position, reach);
        int gameAfter = world.GameTilesWithin(lodge.Position, reach);
        int worthAfter = world.HuntYieldAt(lodge);

        _output.WriteLine(
            $"after two years: {world.GameRange.Count} tiles hunted out; game-bearing tiles "
            + $"{gameBefore} -> {gameAfter}; a hunt worth {worthBefore} -> {worthAfter}; "
            + $"TREES {treesBefore} -> {treesAfter}");

        Assert.True(gameBefore > 0, "The lodge had nothing to hunt, so this measures nothing.");
        Assert.True(
            gameAfter < gameBefore,
            "Two years of hunting emptied no tiles of game — the lodge is a faucet, not a trade.");
        Assert.True(
            worthAfter < worthBefore,
            "The wood thinned and a hunt was worth exactly as much, so depletion reaches the "
            + "yield through nothing at all.");

        // ⛔ THE HALF THAT MUST NOT HAVE HAPPENED.
        Assert.Equal(treesBefore, treesAfter);

        // And it comes back: stop hunting and let the recovery clock run out.
        foreach (Villager villager in world.Villagers)
        {
            villager.WorkplaceId = 0;
        }

        world.SetJobLimit(JobKind.Hunter, 0);
        loop.Step((config.GameReturnsDays * config.TicksPerDay) + config.TicksPerDay);

        int recovered = world.GameTilesWithin(lodge.Position, reach);
        _output.WriteLine(
            $"after {config.GameReturnsDays} quiet days: {world.GameRange.Count} still hunted out, "
            + $"{recovered} game-bearing tiles back of {gameBefore}");

        Assert.True(
            recovered > gameAfter,
            "The game never came back, so a hunted wood is dead ground rather than a thinned one.");
    }

    /// <summary>
    /// ⭐⭐ A village with <b>no lodge in it hashes byte-identically</b> — depletion is sparse.
    /// </summary>
    /// <remarks>
    /// <b>D291's rule, applied on the way in rather than retrofitted.</b> Two loops in the state
    /// hash mixed a zero per catalogue slot and cost five goldens when `Meat` and `Leather`
    /// arrived; <c>GameRange</c> is a sparse list from its first commit, and this is the guard
    /// that says a valley nobody hunts in cannot tell that hunting exists.
    /// </remarks>
    [Fact]
    public void AVillageWithNoLodgeCannotTellDepletionExists()
    {
        SimConfig config = Config;

        SimLoop a = SimFactory.CreatePhase0(config, new InMemoryLogSink());
        a.Step(config.TicksPerYear * 6);

        SimLoop b = SimFactory.CreatePhase0(config, new InMemoryLogSink());
        b.World.GameRange.Recover(b.World.Clock.Tick);
        b.Step(config.TicksPerYear * 6);

        ulong untouched = Determinism.StateHash.Compute(a.World);
        ulong swept = Determinism.StateHash.Compute(b.World);

        _output.WriteLine(
            $"{a.World.GameRange.Count} tiles hunted out either way; {untouched:X16} against "
            + $"{swept:X16}");

        Assert.Equal(0, a.World.GameRange.Count);
        Assert.Equal(untouched, swept);
    }

    private static int LeatherEverywhere(SimWorld world)
    {
        int total = 0;
        foreach (StoreBuilding store in world.StoreBuildings)
        {
            total += store.Store[Goods.Leather];
        }

        foreach (Workplace workplace in world.Workplaces)
        {
            total += workplace.Store[Goods.Leather];
        }

        foreach (Villager villager in world.Villagers)
        {
            total += villager.Carried[Goods.Leather];
        }

        return total;
    }
}

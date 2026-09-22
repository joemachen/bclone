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
        // From four out, not one: the founding leaves lanes since D383, and the first buildable
        // tile beside the founding site is the lane the hauls use — a lodge there halved what
        // four people carried out of it in a season (624 → 300).
        GridPos site = world.Map.FoundingSite;
        for (int radius = 4; radius < 60; radius++)
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

    /// <summary>
    /// ⭐ A hunter who comes to clear the lodge is SEEN at the lodge — they stand on it for the
    /// tick they load, and leave the next (D373, Joe: *"it looks like they stop a few pixels before
    /// actually going to it and then turn around"*).
    /// </summary>
    /// <remarks>
    /// `TakeFromTheBuffer` loaded the armful and took the first step of the haul in the same
    /// tick, so the villager's position was never the lodge's at any tick the view could draw:
    /// a hunter whose home stood beside the lodge went home → home-with-meat → cart → home, and
    /// on screen walked towards the lodge and turned round short of it. Every other arrival
    /// already stands its tick (`FetchingFromStore` → `TravelingHome`, the store → `TravelingHome`);
    /// this one and the marketer's `PutItInTheMarket` did not.
    /// </remarks>
    [Fact]
    public void AHunterClearingTheLodgeIsSeenStandingOnIt()
    {
        SimConfig config = Config;
        SimLoop loop = SimFactory.CreatePhase0(config, new InMemoryLogSink());
        SimWorld world = loop.World;
        Workplace lodge = RaiseALodge(world);

        // Everyone content, everyone a laborer but the hunter, and a lodge holding an armful to
        // clear — so the only errand at the lodge is the hunter's own haul.
        foreach (Household household in world.Households)
        {
            household.Stockpile.Add(Goods.Produce, world.TargetFoodFor(household));
        }

        Assert.True(world.SetStockLimit(Goods.Produce, 1).Allowed);
        foreach (JobKind kind in JobLimits.Kinds)
        {
            world.SetJobLimit(kind, kind == JobKind.Hunter ? 1 : 0);
        }

        lodge.Store.Add(Goods.Meat, config.CarryCapacity * 2);

        int standing = 0;
        int loaded = 0;
        for (int tick = 0; tick < config.TicksPerSeason && loaded == 0; tick++)
        {
            loop.StepOnce();
            foreach (Villager villager in world.Villagers)
            {
                if (villager.Carried[Goods.Meat] > 0 && villager.State == VillagerState.HaulingToStore)
                {
                    loaded++;
                    if (villager.Position == lodge.Position)
                    {
                        standing++;
                    }
                }
            }
        }

        _output.WriteLine($"{loaded} villager-ticks carrying meat off the lodge; {standing} of them standing on the lodge");
        Assert.True(loaded > 0, "nobody ever cleared the lodge, so this proves nothing (D7)");
        Assert.True(standing > 0, "the meat left the lodge without anybody ever standing on it — the load and the first step of the haul happen in one tick");
    }

    /// <summary>A lodge raised and finished in the fixture's woods — shared with <c>TradesVisiblyWorkTests</c> (D384).</summary>
    internal static Workplace RaiseALodgeFor(SimWorld world) => RaiseALodge(world);

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

                    // The cheap question first (`building-placement.md`'s trap): `CanBuildAt`
                    // builds a flow field and sweeps the valley for a tile that is mostly not
                    // bare anyway — asked second, the scan is seconds rather than a minute.
                    if (world.ForestTilesWithin(at, world.Config.HuntingRadius) == 0
                        && world.CanBuildAt(BuildingKind.Granary, at).Allowed)
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
                store.Store.TakeAll(Goods.Produce);
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
            + $"{world.TravelCost.TicksBetween(ring.Tile, lodge.Tile)} tiles away; "
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
            or VillagerState.HaulingToFarm // the catch carried back to the lodge (D384)
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
    //  § The woodland is the resource — Joe, 2026-09-05
    // ---------------------------------------------------------------

    /// <summary>
    /// ⭐⭐⭐ <b>Felling the wood takes the food and the game with it — and replanting brings both
    /// back.</b>
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Joe, 2026-09-05, and this guard is his sentence made executable:</b> *"Foraging food
    /// should only deplete if the woodland around the hut is felled. The volume of mature trees in
    /// the vicinity of the forager's hut should dictate the volume of gatherable food — similar
    /// for hunting and animals. More trees = max available animals, less trees = less available
    /// animals."*
    /// </para>
    /// <para>
    /// ⛔⛔ <b>THIS REPLACED A WHOLE MECHANIC.</b> Hunting briefly had its own per-tile depletion
    /// store (D295) which thinned the game every hunt on its own clock. Joe replaced it with one
    /// rule covering both trades, and **standing woodland is now the only thing that moves either
    /// yield.** So this is the guard the deleted ones were traded for — *two guards were removed
    /// and this one must carry both claims.*
    /// </para>
    /// <para>
    /// ⚠️ <b>Both halves matter and the second is the one that would rot quietly.</b> A yield that
    /// falls when the wood is cut and never recovers is a valley that dies once; recovery is what
    /// makes felling a DECISION rather than a mistake. Felled ground goes to <c>Grass</c>, and
    /// <c>RegrowthSystem</c> walks it back Grass → Sapling → Forest, so this runs long enough to
    /// clear both stages.
    /// </para>
    /// </remarks>
    [Fact]
    public void FellingTheWoodTakesTheFoodAndTheGameWithIt()
    {
        SimConfig config = Config;
        SimLoop loop = SimFactory.CreatePhase0(config, new InMemoryLogSink());
        SimWorld world = loop.World;

        Workplace lodge = RaiseALodge(world);
        Workplace ring = world.Workplaces.First(w => w.GatheringRadius > 0);

        int forageBefore = world.GatherYieldAt(ring);
        int huntBefore = world.HuntYieldAt(lodge);

        Assert.True(forageBefore > 0, "The hut's ring has no trees, so this measures nothing.");
        Assert.True(huntBefore > 0, "The lodge has no woods, so this measures nothing.");

        // Fell every tree in both reaches — the same transition a harvested tile makes
        // (`SimWorld.HarvestOne` sets felled ground to Grass).
        int felled = 0;
        felled += FellAround(world, ring.Tile, ring.GatheringRadius);
        felled += FellAround(world, lodge.Tile, config.HuntingRadius);

        int forageAfter = world.GatherYieldAt(ring);
        int huntAfter = world.HuntYieldAt(lodge);

        _output.WriteLine(
            $"felled {felled} tiles: a trip {forageBefore} -> {forageAfter}, "
            + $"a hunt {huntBefore} -> {huntAfter}");

        Assert.True(
            forageAfter < forageBefore,
            "The wood came down and a foraging trip was worth exactly as much. Standing trees are "
            + "supposed to be the only thing that sets it.");
        Assert.True(
            huntAfter < huntBefore,
            "The wood came down and a hunt was worth exactly as much. More trees means more game "
            + "and fewer means less — that is the whole rule.");

        // ⭐ AND IT COMES BACK. Grass -> Sapling -> Forest, with a young-sapling stage in
        // between, so several regrowth periods have to pass before the trees are mature again.
        loop.Step(config.RegrowthPeriodDays * config.TicksPerDay * 4);

        int forageBack = world.GatherYieldAt(ring);
        int huntBack = world.HuntYieldAt(lodge);

        _output.WriteLine(
            $"after four regrowth periods: a trip {forageAfter} -> {forageBack}, "
            + $"a hunt {huntAfter} -> {huntBack}");

        Assert.True(
            forageBack > forageAfter,
            "The wood never grew back, so felling is permanent and a valley dies once.");
        Assert.True(
            huntBack > huntAfter,
            "The game never came back with the trees, so the two are not actually tied together.");
    }

    /// <summary>Cut down every tree within reach, returning how many fell.</summary>
    private static int FellAround(SimWorld world, GridPos centre, int reach)
    {
        int felled = 0;
        for (int dy = -reach; dy <= reach; dy++)
        {
            int span = reach - System.Math.Abs(dy);
            for (int dx = -span; dx <= span; dx++)
            {
                var at = new GridPos(centre.X + dx, centre.Y + dy);
                if (world.Map.Contains(at)
                    && world.Map.TerrainAt(at) == Terrain.Forest
                    && world.SetTerrain(at, Terrain.Grass))
                {
                    felled++;
                }
            }
        }

        return felled;
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

    /// <summary>
    /// ⛔⛔ A village does not starve beside a lodge full of meat — <b>an armful of food in a buffer is
    /// worth the walk whenever a store has room</b> (D362).
    /// </summary>
    /// <remarks>
    /// <para>
    /// Joe's game, 2026-09-12: seven hunts filled the lodge, `FoodTheVillageHolds` counted it
    /// (D161), every food producer's Wants went to 0, and the only thing that moved meat was one
    /// marketer at forty an armful — because `BufferWorthClearing` cleared a buffer only when it was
    /// NEARLY FULL, and after the first eight hundred it was not. Fifteen people rested for two
    /// years while the granaries drained; thirteen starved beside 1,780 meat.
    /// </para>
    /// <para>
    /// Posed directly: a lodge with a season's meat in it and empty granaries with room. Within a
    /// season the meat is out of the lodge and into the stores, and the village's count of food
    /// it holds never claimed more than it could reach.
    /// </para>
    /// </remarks>
    [Fact]
    public void AVillageDoesNotStarveBesideAFullLodge()
    {
        SimConfig config = Config;
        SimLoop loop = SimFactory.CreatePhase0(config, new InMemoryLogSink());
        SimWorld world = loop.World;

        Workplace lodge = RaiseALodge(world);
        loop.Step(10);

        // Joe's state: the lodge a load short of full, the granaries thin — so the village's count
        // of the food it HOLDS says plenty (the lodge counts, D161) and every producer stands down,
        // while the food people can actually fetch runs out. The nearly-full rule read this lodge
        // as "not worth clearing".
        foreach (StoreBuilding store in world.StoreBuildings)
        {
            foreach (Goods goods in world.GoodsCatalog.EdibleGoods)
            {
                store.Store.TakeAll(goods);
            }
        }

        int meat = lodge.Store.Capacity - config.MeatYield - 1;
        lodge.Store.Add(Goods.Meat, meat);
        Assert.True(world.BufferWorthClearing(lodge), "a lodge a load short of full beside thin granaries was not worth clearing");

        // Joe had set a food limit; the lodge alone meets it.
        Assert.True(world.SetStockLimit(Goods.Produce, meat / 2).Allowed);
        Assert.True(
            world.StockLimits.IsMet(Goods.Produce, world.FoodTheVillageHolds()),
            $"the village does not think it has enough food ({world.FoodTheVillageHolds()} held), so this is not Joe's state");

        int inStoresBefore = world.FoodInGranaries();

        // ⭐ CARRIED OUT, COUNTED AT THE DOOR (D386). The lodge is a load short of full, so the
        // first hunt back from the woods fills it — 422 meat in one arrival — and a season's
        // net change reads as the lodge GAINING while two hunters carried thirteen armfuls out.
        // (Until D386 the carry-back never arrived at all — `HoldsTheJobFor` recalled every
        // hunter on it — so the net read as the clearing alone.) Every drop is summed, tick by
        // tick, as the D384 guard counts the rises.
        int carriedOut = 0;
        int putOnAShelf = 0;
        int before = lodge.Store[Goods.Meat];
        int shelved = world.FoodInGranaries();
        for (int tick = 0; tick < config.TicksPerSeason; tick++)
        {
            loop.StepOnce();
            int now = lodge.Store[Goods.Meat];
            if (now < before)
            {
                carriedOut += before - now;
            }

            before = now;

            // What arrives, not the net of what four people ate off the shelf meanwhile.
            int onShelves = world.FoodInGranaries();
            if (onShelves > shelved)
            {
                putOnAShelf += onShelves - shelved;
            }

            shelved = onShelves;
        }

        int left = lodge.Store[Goods.Meat];
        int inStores = world.FoodInGranaries();
        int inArms = 0;
        foreach (Villager villager in world.Villagers)
        {
            inArms += villager.Carried[Goods.Meat];
        }

        _output.WriteLine($"a season on: {meat} meat in the lodge became {left}, {carriedOut} carried out; the stores went {inStoresBefore} → {inStores} with {putOnAShelf} put on a shelf, {inArms} in arms; {world.Villagers.Count(v => v.Alive)} alive");

        // The hunters keep hunting into it (the granaries ARE thin), so the bar is what left the
        // lodge: at least eight armfuls in a season, from four people who also eat — read at
        // the lodge, where the hunters' own catches make the drop an UNDER-count, so the bar is
        // conservative. ⚠️ It used to be read at the stores (D384): a hunt happens out in the
        // woods now and the catch comes home in the hunter's arms, so at the season's end a
        // load is on its way and a hungry carrier has eaten from another — 244 in the stores
        // and 40 in arms read as seven armfuls when eight had left the lodge. Half of it must
        // still have reached a shelf within the season, or the carrying is not where it can be
        // eaten.
        int leftTheLodge = carriedOut;
        int reachedAShelf = putOnAShelf;
        // ⚠️ SIX ARMFULS, NOT EIGHT (D386): the hunters really hunt now — a day in the woods and a
        // 422-meat arrival that fills the lodge — and the clearing shares the season with it.
        // Measured at exactly eight (320) the day the carry-back started arriving, which is a
        // guard passing by its bar (trap 87); six is the claim with room to be wrong about.
        Assert.True(
            leftTheLodge >= 6 * config.CarryCapacity,
            $"a season passed and only {leftTheLodge} meat left a lodge holding {meat} — "
            + "nobody is carrying it where it can be eaten");
        Assert.True(
            reachedAShelf >= 4 * config.CarryCapacity,
            $"a season passed and only {reachedAShelf} meat reached a store ({inArms} in arms) — "
            + "it leaves the lodge and does not arrive");
        Assert.DoesNotContain(world.Villagers, v => v.CauseOfDeath == CauseOfDeath.Starvation);
    }

    // ---------------------------------------------------------------
    //  § The hunt answers the village, never the larder (D398)
    // ---------------------------------------------------------------

    /// <summary>
    /// ⭐⭐ A hunter whose own larder is short, in a village with full stores, walks to the granary —
    /// not into the woods (D398, Joe's call (a) on D397's audit).
    /// </summary>
    /// <remarks>
    /// <para>
    /// The household half of <c>needsFood</c> sent a hunter hunting whenever their cupboard was a
    /// little low; a hunt is 900 meat into a lodge the village has no room to clear, and twelve
    /// fixture seeds carried <b>3.67 million</b> food on the ground after fifty years. The forager
    /// keeps the household reason (an armful; D385 measured taking it away); the hunter and the
    /// fisher hear only the village's. <b>Measured before typed, twelve seeds × fifty years with a
    /// lodge and a fishery:</b> 190 / 221 / 11 → <b>198 / 205 / 9</b>, the ground <b>3,671,184 →
    /// 0</b>, produced 4.6M → 873k; without a lodge, byte-identical.
    /// </para>
    /// <para>
    /// Red with the hunter reading <c>needsFood</c> again: the hunter goes <c>TravelingToGame</c>.
    /// </para>
    /// </remarks>
    [Fact]
    public void AHunterWithAShortLarderFetchesFromTheGranaryInsteadOfHunting()
    {
        SimConfig config = Config;
        SimLoop loop = SimFactory.CreatePhase0(config, new InMemoryLogSink());
        SimWorld world = loop.World;
        Workplace lodge = RaiseALodge(world);

        // Only the hunter holds a job; the stores are full to the target and beyond, so the village
        // wants no more food; one household's larder is half empty, so the household half of
        // `needsFood` is true — the exact state that used to send the hunter out.
        foreach (JobKind kind in JobLimits.Kinds)
        {
            world.SetJobLimit(kind, kind == JobKind.Hunter ? 1 : 0);
        }

        loop.Step(config.TicksPerDay * 3);
        Villager? hunter = null;
        foreach (Villager villager in world.Villagers)
        {
            if (world.FindWorkplace(villager.WorkplaceId) is { Kind: JobKind.Hunter })
            {
                hunter = villager;
            }
        }

        Assert.True(hunter is not null, "Nobody took the lodge, so this measures nothing.");
        Household home = world.HouseholdOf(hunter!);

        foreach (StoreBuilding store in world.StoreBuildings)
        {
            store.Store.Add(Goods.Produce, store.Store.FreeSpace);
        }

        foreach (Household household in world.Households)
        {
            household.Stockpile.TakeAll(Goods.Produce);
            household.Stockpile.Add(Goods.Produce, ReferenceEquals(household, home) ? world.TargetFoodFor(household) / 3 : world.TargetFoodFor(household));
        }

        Assert.False(world.TheVillageWantsMoreFood(), "The village still wants food, so the larder is not the only reason left.");
        Assert.True(world.FoodIn(home.Stockpile) < world.TargetFoodFor(home), "The hunter's larder is not short, so nothing is posed.");

        // ⚠️ A hunt already in flight is finished — you do not drop the deer because the granary
        // filled while you were out — so the count starts once the hunter is home from it. And the
        // fetch is the household's, not the hunter's: a spare hand goes for the armful (D385).
        int hunting = 0;
        int fetching = 0;
        bool home_ = false;
        for (int tick = 0; tick < config.TicksPerSeason; tick++)
        {
            loop.StepOnce();
            bool out_ = hunter.State is VillagerState.TravelingToGame or VillagerState.Hunting or VillagerState.HaulingToStore;
            home_ |= !out_;
            if (home_ && hunter.State is VillagerState.TravelingToGame or VillagerState.Hunting)
            {
                hunting++;
            }

            foreach (int id in home.MemberIds)
            {
                if (world.FindVillager(id) is { State: VillagerState.FetchingFromStore })
                {
                    fetching++;
                }
            }
        }

        _output.WriteLine($"{hunter.Name}, larder short in a fed village: {hunting} ticks hunting, {fetching} ticks fetching from a store, over a season; {lodge.Name} holds {lodge.Store[Goods.Meat]} meat");
        Assert.True(hunting == 0, $"{hunter.Name} went hunting for {hunting} ticks to fill their own larder while the village wanted no more food — the hunt answered the larder.");
        Assert.True(fetching > 0, $"{hunter.Name} never fetched from a store either, so the short larder went unanswered (D7).");
    }
}

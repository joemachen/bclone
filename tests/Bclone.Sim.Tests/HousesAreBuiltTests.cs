using Bclone.Sim.Config;
using Bclone.Sim.Core;
using Bclone.Sim.Logging;
using Bclone.Sim.World;
using Xunit;
using Xunit.Abstractions;

namespace Bclone.Sim.Tests;

/// <summary>
/// A house is a construction site like everything else — Joe, D102.
/// </summary>
/// <remarks>
/// <para>
/// <b>The inconsistency <c>specs/cold-start.md §7.1b</c> has carried since Joe's second run:</b>
/// <em>"they built homes (immediate builds btw, not a visual timed thing like other
/// buildings)."</em> A house took its timber straight out of the stores and set
/// <c>HomePosition</c> in one tick, where a granary is marked, hauled to and worked on.
/// </para>
/// <para>
/// <b>It hid what a house costs, and it meant houses never competed for builders</b> — which
/// is exactly the distortion that made winter 1 look winnable when it was not.
/// </para>
/// </remarks>
public sealed class HousesAreBuiltTests
{
    private readonly ITestOutputHelper _output;

    public HousesAreBuiltTests(ITestOutputHelper output) => _output = output;

    private static SimLoop Loop(SimConfig config) =>
        SimFactory.CreatePhase0(config, new InMemoryLogSink());

    private static int HomeSites(SimWorld world)
    {
        int count = 0;
        for (int i = 0; i < world.Workplaces.Count; i++)
        {
            if (world.Workplaces[i].Construction?.Kind == BuildingKind.Home)
            {
                count++;
            }
        }

        return count;
    }

    private static int Homes(SimWorld world)
    {
        int homes = 0;
        for (int i = 0; i < world.Households.Count; i++)
        {
            if (world.Households[i].HasHome)
            {
                homes++;
            }
        }

        return homes;
    }

    /// <summary>
    /// Somebody to build, because nothing is raised without one (D108).
    /// </summary>
    /// <remarks>
    /// <b>The cold-start guards below all have to place this now, and it is the change
    /// rather than an oversight.</b> A construction site is an errand the hut's crew walks
    /// out to; a founding with no hut can mark a woodcutter's hut and two houses and watch
    /// all three sit at "0 of 25 logs" forever. It costs nothing but the ground it stands
    /// on, so the founding pays nothing for it — which is why the hut is free.
    /// </remarks>
    private static void MarkABuildersHut(SimWorld world) =>
        MarkSomewhereNear(world, BuildingKind.BuilderHut, world.Map.FoundingSite, 2);

    private static void PaintHomeGround(SimWorld world)
    {
        GridPos site = world.Map.FoundingSite;
        for (int dy = -4; dy <= 4; dy++)
        {
            for (int dx = -4; dx <= 4; dx++)
            {
                world.PaintResidential(new GridPos(site.X + dx, site.Y + dy));
            }
        }
    }

    /// <summary>⭐ A roofless family gets a building site, not a house out of thin air.</summary>
    [Fact]
    public void AHouseIsMarkedOutRatherThanConjured()
    {
        SimConfig config = ShippedConfig.Load();
        SimLoop loop = Loop(config);
        SimWorld world = loop.World;

        PaintHomeGround(world);

        int logsBefore = world.TotalLogs();
        loop.Step(5);

        _output.WriteLine(
            $"five ticks after painting: {HomeSites(world)} house sites, {Homes(world)} homes, "
            + $"logs {logsBefore} -> {world.TotalLogs()}");

        Assert.True(HomeSites(world) > 0, "No house was ever marked out for the founders.");
        Assert.Equal(0, Homes(world));

        // ⭐ AND NO TIMBER WAS TAKEN. `TryTakeBuildingTimber` drew a house's logs straight
        // out of the stores in the same tick; a builder hauls them to the site now, which is
        // D43's rule about construction not being a purchase, applied at last to the building
        // the village raises most often.
        Assert.Equal(logsBefore, world.TotalLogs());
    }

    /// <summary>A house costs work as well as timber, and the recipe says so.</summary>
    [Fact]
    public void AHouseOwesWorkAndNotOnlyTimber()
    {
        SimConfig config = ShippedConfig.Load();
        BuildingRecipe recipe = BuildingRecipe.For(BuildingKind.Home, config);

        Assert.Equal(config.LogsPerHouse, recipe.Logs);
        Assert.Equal(config.HomeWorkTicks, recipe.WorkTicks);
        Assert.True(recipe.WorkTicks > 0, "A house that owes no work is an instant house again.");
    }

    /// <summary>And the village still ends up housed — the site is a delay, not a wall.</summary>
    [Fact]
    public void TheVillageStillHousesItself()
    {
        SimConfig config = VillageFixtures.Village;
        SimLoop loop = Loop(config);
        SimWorld world = loop.World;

        loop.Step(config.TicksPerYear * 40);

        _output.WriteLine(
            $"forty years: {world.Population} alive in {world.Households.Count} households, "
            + $"{Homes(world)} of them housed, {HomeSites(world)} still being raised");

        Assert.True(world.Households.Count > 2, "The village never formed a new household.");
        Assert.True(Homes(world) > 2, "The village never finished a house it started.");
    }

    /// <summary>
    /// ⭐ What the player marked is built before a house the village marked for itself.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Measured, and the founding died without it.</b> <c>HouseTheRoofless</c> marks a
    /// house for every roofless family on tick 4, so two house sites went in front of the
    /// woodcutter's hut the player marked at tick 0. The hut's timber then arrived at
    /// <b>t364</b> against a winter starting at t360, and the shipped guard read <b>2 alive,
    /// 2 frozen</b>. With the rank, the hut is back to logs t129, standing t172, first
    /// firewood t249 — exactly where it was before houses were built.
    /// </para>
    /// <para>
    /// <b>The cost was never the work.</b> With <c>home_work_ticks</c> set to zero the
    /// timeline did not move by one tick: the bottleneck is the timber a builder hauls, and
    /// two houses' worth of it went first.
    /// </para>
    /// <para>
    /// <b>A priority, not an exclusion</b> — see <see cref="TheVillageStillHousesItself"/>.
    /// </para>
    /// </remarks>
    [Fact]
    public void TheHutThePlayerMarkedIsBuiltBeforeTheHousesTheVillageWants()
    {
        SimConfig config = ShippedConfig.Load();
        SimLoop loop = Loop(config);
        SimWorld world = loop.World;

        PaintHomeGround(world);
        MarkSomewhereNear(world, BuildingKind.Pile, world.Map.FoundingSite, 2);
        MarkABuildersHut(world);
        MarkSomewhereNear(world, BuildingKind.WoodcutterHut, world.Map.FoundingSite, 3);

        // Somewhere to gather and something to fell. Not scenery: a founding valley has held
        // no workplaces at all since the thickets retired, so without this the four founders
        // starve inside the year and every claim below is about a dead village.
        ColdStartTests.FeedTheFounding(world);

        long hutStood = -1;
        long firstHouse = -1;

        for (long tick = 0; tick < config.TicksPerYear; tick++)
        {
            loop.StepOnce();

            if (hutStood < 0 && AnyWoodcutterHut(world))
            {
                hutStood = tick;
            }

            if (firstHouse < 0 && Homes(world) > 0)
            {
                firstHouse = tick;
            }
        }

        _output.WriteLine(
            $"the hut stood at t{hutStood}; the first house at t{firstHouse} "
            + $"(winter starts at t360)");

        Assert.True(hutStood >= 0, "The hut the player marked was never built.");
        Assert.True(
            hutStood < 360,
            $"The hut the player marked was not standing until t{hutStood}, after winter began.");
    }

    /// <summary>
    /// ⭐ Marking a granary in the first spring does not freeze the village to death.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Joe, playing, and this is his run scripted:</b> <em>"they never built the houses.
    /// They eventually worked on the granary, but didn't finish it and died."</em> The panel
    /// said it out loud — <em>"Queue: 1st of 3"</em> — and that was the bug: the queue put
    /// everything the player marked in front of every house (D102), so a granary marked in
    /// spring jumped ahead of two houses that had been waiting since tick 4. It took every
    /// builder for forty logs and sixty ticks, <b>nobody ever got a roof</b>, and all four
    /// froze.
    /// </para>
    /// <para>
    /// Measured before and after, on the config the game loads: <b>0 alive, 4 frozen, 0
    /// houses</b> with the granary marked, against 4 alive and 2 houses without it. With the
    /// queue in plain marking order it is 4 alive and 2 houses either way (D105).
    /// </para>
    /// <para>
    /// <b>The reason it needs its own guard is that it is a REASONABLE thing to do.</b>
    /// `JoesOpeningSurvivesOnTheShippedConfig` marks a pile and a hut, and passed throughout;
    /// marking one more sensible building is not an error the player should have to know
    /// about, and §0.1 rules out a village killed by something it could not have seen coming.
    /// </para>
    /// </remarks>
    /// <remarks>
    /// <para>
    /// <b>▶️ UN-SKIPPED (D157), and the control that skipped it was reading a real corpse
    /// with the wrong name on it.</b> It was parked because the same opening
    /// <em>without</em> the granary died identically — 0 alive, 0 houses, 57 logs — which
    /// correctly exonerated the granary and then wrongly convicted the harness: the note
    /// said the opening *needs a player who keeps reacting*. It does not. Both arms died of
    /// one cause neither of them varied, and it was not the queue and not the script — the
    /// gatherer's hut stood on woodland whose ground the village would never clear, because
    /// nearest-first clearing can never reach a footprint with regrowing coppice between it
    /// and the village.
    /// </para>
    /// <para>
    /// <b>⭐ Which is why "the control says X is innocent" is only half a finding.</b> An arm
    /// that varies one thing and dies both ways has ruled that thing out and has said
    /// nothing whatever about what did it. That is D142's and D151's mistake for the third
    /// time, and this test carried the wrong cause in its own skip reason for two days.
    /// </para>
    /// </remarks>
    [Fact]
    public void MarkingAGranaryInTheFirstSpringDoesNotCostTheVillageItsHouses()
    {
        SimConfig config = ShippedConfig.Load();
        SimLoop loop = Loop(config);
        SimWorld world = loop.World;

        PaintHomeGround(world);
        MarkSomewhereNear(world, BuildingKind.Pile, world.Map.FoundingSite, 2);
        MarkABuildersHut(world);
        MarkSomewhereNear(world, BuildingKind.WoodcutterHut, world.Map.FoundingSite, 3);

        // Somewhere to gather and something to fell. Not scenery: a founding valley has held
        // no workplaces at all since the thickets retired, so without this the four founders
        // starve inside the year and every claim below is about a dead village.
        ColdStartTests.FeedTheFounding(world);

        // A month in, exactly as somebody playing would: the founding looks settled, so you
        // mark the next thing.
        loop.Step(30);
        MarkSomewhereNear(world, BuildingKind.Granary, world.Map.FoundingSite, 4);

        loop.Step(config.TicksPerYear * 2);

        int frozen = 0;
        for (int i = 0; i < world.Villagers.Count; i++)
        {
            if (!world.Villagers[i].Alive
                && world.Villagers[i].CauseOfDeath == CauseOfDeath.Cold)
            {
                frozen++;
            }
        }

        _output.WriteLine(
            $"two years after marking a granary in spring: {world.Population} alive, "
            + $"{frozen} frozen, {Homes(world)} houses, {world.FirewoodInSheds()} firewood");

        Assert.True(
            Homes(world) > 0,
            "Two years passed and the village never raised a single house — the granary is "
            + "in front of them in the queue again.");

        Assert.True(
            world.Population >= config.StartingPopulation,
            $"{config.StartingPopulation - world.Population} founders died because the player "
            + $"marked a granary; {frozen} of them froze.");
    }

    /// <summary>The queue is the order things were marked, and says so.</summary>
    /// <remarks>
    /// The panel shows the player a position and what is immediately ahead of it, so the
    /// order has to be one sentence long. <b>"First marked, first built"</b> is that sentence;
    /// "everything you marked, then the houses" was the one that killed Joe's village.
    /// </remarks>
    [Fact]
    public void TheQueueIsTheOrderThingsWereMarked()
    {
        SimConfig config = ShippedConfig.Load();
        SimLoop loop = Loop(config);
        SimWorld world = loop.World;

        PaintHomeGround(world);

        // Let the village mark its own two houses first…
        loop.Step(10);
        int housesQueued = world.BuildQueue().Count;
        Assert.True(housesQueued > 0, "The village never marked a house for its founders.");

        // …then mark a granary, which must go BEHIND them.
        MarkSomewhereNear(world, BuildingKind.Granary, world.Map.FoundingSite, 4);

        System.Collections.Generic.List<Workplace> queue = world.BuildQueue();
        _output.WriteLine(
            $"queue after marking a granary behind {housesQueued} houses: "
            + string.Join(", ", queue.ConvertAll(site => site.Construction!.Kind.ToString())));

        Assert.Equal(housesQueued + 1, queue.Count);
        Assert.Equal(BuildingKind.Granary, queue[^1].Construction!.Kind);
        Assert.Equal(queue.Count, world.QueuePositionOf(queue[^1]));
    }

    // ---------------------------------------------------------------
    //  The player can reorder the queue — D105, Joe's own answer
    // ---------------------------------------------------------------

    /// <summary>⭐ One press moves a site exactly one place.</summary>
    /// <remarks>
    /// <b>A swap with the neighbour, not a number the player nudges</b> — the panel says
    /// "3rd of 5", so pressing up had better make it 2nd. Incrementing a priority value would
    /// move it past two things or none depending on what its neighbours happened to hold.
    /// </remarks>
    [Fact]
    public void MovingASiteUpTheQueueMovesItExactlyOnePlace()
    {
        SimConfig config = ShippedConfig.Load();
        SimLoop loop = Loop(config);
        SimWorld world = loop.World;

        PaintHomeGround(world);
        loop.Step(10);
        MarkSomewhereNear(world, BuildingKind.Granary, world.Map.FoundingSite, 4);

        System.Collections.Generic.List<Workplace> queue = world.BuildQueue();
        Assert.True(queue.Count >= 3, "Need three sites to move one through the middle.");

        Workplace last = queue[^1];
        Assert.Equal(queue.Count, world.QueuePositionOf(last));

        Assert.True(world.MoveInBuildQueue(last, -1));
        Assert.Equal(queue.Count - 1, world.QueuePositionOf(last));

        Assert.True(world.MoveInBuildQueue(last, -1));
        Assert.Equal(queue.Count - 2, world.QueuePositionOf(last));

        _output.WriteLine(
            $"moved a granary from {queue.Count} to {world.QueuePositionOf(last)} of "
            + $"{world.BuildQueue().Count}");

        // And back down again, so the control is not one-way.
        Assert.True(world.MoveInBuildQueue(last, +1));
        Assert.Equal(queue.Count - 1, world.QueuePositionOf(last));
    }

    /// <summary>⭐ And the village actually builds it first — the control reaches the builders.</summary>
    /// <remarks>
    /// <para>
    /// <b>The gap this closes is the one D144 is about.</b> Everything else here tests
    /// <em>queue order</em> — a predicate — and <c>TheHeadOfTheQueueIsWhatTheBuildersGoTo</c>
    /// tests that builders serve the head. Between them the conclusion follows, and "follows"
    /// is exactly what was true of the store filter for a day: five guards, all correct, none
    /// of them ever making a villager put something down.
    /// </para>
    /// <para>
    /// So this presses the button the player presses and then watches the valley: mark a
    /// granary and then a shed, send the shed to the front, and the shed must be what gets
    /// raised. <b>The anti-vacuity half is the arm that presses nothing</b> — the same village,
    /// left alone, must finish the granary first, or this would pass on a queue that was never
    /// reordered.
    /// </para>
    /// <para>
    /// <b>On a living village rather than the founding, deliberately.</b> The first attempt used
    /// the cold start, and both arms reported *"nothing finished in 12 years — 0 alive"*: the
    /// founders die long before a granary is raised, so it was measuring a dead valley twice.
    /// That is D143's ruling arriving as a test-design constraint — an unattended founding is
    /// not a place to watch a twelve-year race.
    /// </para>
    /// </remarks>
    [Fact]
    public void MovingASiteToTheFrontIsWhatTheVillageBuildsFirst()
    {
        SimConfig config = VillageFixtures.Village;

        static BuildingKind? FirstRaised(SimConfig config, bool promote, ITestOutputHelper output)
        {
            SimLoop loop = Loop(config);
            SimWorld world = loop.World;

            // A village on its feet, with hands and timber, so the race is about the queue.
            loop.Step(config.TicksPerYear * 20);

            int granariesBefore = Granaries(world);
            int shedsBefore = Sheds(world);

            MarkSomewhereNear(world, BuildingKind.Granary, world.Map.FoundingSite, 5);
            MarkSomewhereNear(world, BuildingKind.Shed, world.Map.FoundingSite, 7);

            System.Collections.Generic.List<Workplace> queue = world.BuildQueue();
            Assert.True(queue.Count >= 2, "Both sites must be queued for there to be a race.");

            Workplace shed = queue[^1];
            Assert.Equal(BuildingKind.Shed, shed.Construction!.Kind);

            if (promote)
            {
                while (world.QueuePositionOf(shed) > 1)
                {
                    Assert.True(world.MoveInBuildQueue(shed, -1));
                }
            }

            // Watch what FINISHES first, rather than what stands at the end of a horizon —
            // a horizon would let both be built and say nothing about the order.
            for (int tick = 0; tick < config.TicksPerYear * 12; tick++)
            {
                loop.StepOnce();

                if (Sheds(world) > shedsBefore)
                {
                    output.WriteLine(
                        $"{(promote ? "promoted" : "left alone")}: the shed finished first, "
                        + $"year {world.Clock.Year}");
                    return BuildingKind.Shed;
                }

                if (Granaries(world) > granariesBefore)
                {
                    output.WriteLine(
                        $"{(promote ? "promoted" : "left alone")}: the granary finished first, "
                        + $"year {world.Clock.Year}");
                    return BuildingKind.Granary;
                }
            }

            output.WriteLine(
                $"{(promote ? "promoted" : "left alone")}: nothing finished in 12 years — "
                + $"{world.Population} alive, {world.BuildQueue().Count} sites queued, "
                + $"{world.TotalLogs()} logs");
            return null;
        }

        BuildingKind? leftAlone = FirstRaised(config, promote: false, _output);
        BuildingKind? promoted = FirstRaised(config, promote: true, _output);

        Assert.True(leftAlone is not null && promoted is not null,
            "Nothing was ever built in either village, so this guard proves nothing (D7).");

        // Anti-vacuity: left alone, the queue must genuinely have raised the other one first.
        Assert.Equal(BuildingKind.Granary, leftAlone!.Value);

        Assert.Equal(BuildingKind.Shed, promoted!.Value);
    }

    private static int Granaries(SimWorld world) => StoresOfKind(world, StoreKind.Granary);

    private static int Sheds(SimWorld world) => StoresOfKind(world, StoreKind.Shed);

    private static int StoresOfKind(SimWorld world, StoreKind kind)
    {
        int count = 0;
        foreach (StoreBuilding store in world.StoreBuildings)
        {
            if (store.Kind == kind)
            {
                count++;
            }
        }

        return count;
    }


    /// <summary>Nothing moves off either end, and the view is told so.</summary>
    [Fact]
    public void ASiteCannotBeMovedOffEitherEndOfTheQueue()
    {
        SimConfig config = ShippedConfig.Load();
        SimLoop loop = Loop(config);
        SimWorld world = loop.World;

        PaintHomeGround(world);
        loop.Step(10);

        System.Collections.Generic.List<Workplace> queue = world.BuildQueue();
        Assert.NotEmpty(queue);

        Assert.False(world.MoveInBuildQueue(queue[0], -1));
        Assert.False(world.MoveInBuildQueue(queue[^1], +1));
        Assert.Equal(1, world.QueuePositionOf(queue[0]));
    }

    /// <summary>
    /// ⭐ And the village actually builds what was moved to the front.
    /// </summary>
    /// <remarks>
    /// The behavioural half, and the reason the control exists rather than the reason it is
    /// tidy: Joe's village froze because a granary was in front of two houses. The answer is
    /// that he can put it behind them — <b>which is only an answer if the builders obey the
    /// order the panel shows him.</b>
    /// </remarks>
    [Fact]
    public void TheVillageBuildsWhateverThePlayerMovedToTheFront()
    {
        SimConfig config = ShippedConfig.Load();
        SimLoop loop = Loop(config);
        SimWorld world = loop.World;

        PaintHomeGround(world);
        MarkSomewhereNear(world, BuildingKind.Pile, world.Map.FoundingSite, 2);
        MarkABuildersHut(world);
        loop.Step(10);
        MarkSomewhereNear(world, BuildingKind.Granary, world.Map.FoundingSite, 4);

        Workplace granary = Assert.Single(
            world.Workplaces, place => place.Construction?.Kind == BuildingKind.Granary);

        // It is last by default, behind the houses the village marked at tick 4.
        Assert.Equal(world.BuildQueue().Count, world.QueuePositionOf(granary));

        while (world.QueuePositionOf(granary) > 1)
        {
            Assert.True(world.MoveInBuildQueue(granary, -1));
        }

        loop.Step(config.TicksPerYear);

        int delivered = granary.Construction?.LogsDelivered ?? granary.Construction?.Recipe.Logs ?? 0;
        _output.WriteLine(
            $"a year after moving the granary to the front: {delivered} logs delivered to it, "
            + $"{Homes(world)} houses standing");

        Assert.True(
            granary.Construction is null || granary.Construction.LogsDelivered > 0,
            "The granary was put at the front of the queue and the village still ignored it.");
    }

    /// <summary>A queue nobody has reordered hashes as though the control did not exist.</summary>
    /// <remarks>
    /// The same licence <c>StockLimitTests</c> takes for stock limits, and for the same reason:
    /// every acceptance band in the project was measured on a village that never touched this,
    /// so the default has to be indistinguishable from the feature's absence.
    /// </remarks>
    [Fact]
    public void ReorderingIsANoOpUntilSomebodyUsesIt()
    {
        SimConfig config = ShippedConfig.Load();

        SimWorld untouched = Loop(config).World;
        SimWorld reordered = Loop(config).World;

        PaintHomeGround(untouched);
        PaintHomeGround(reordered);

        Assert.Equal(
            Bclone.Sim.Determinism.StateHash.Compute(untouched),
            Bclone.Sim.Determinism.StateHash.Compute(reordered));

        SimLoop loop = Loop(config);
        PaintHomeGround(loop.World);
        loop.Step(10);

        ulong before = Bclone.Sim.Determinism.StateHash.Compute(loop.World);
        Assert.True(loop.World.MoveInBuildQueue(loop.World.BuildQueue()[^1], -1));

        Assert.NotEqual(before, Bclone.Sim.Determinism.StateHash.Compute(loop.World));
    }

    private static bool AnyWoodcutterHut(SimWorld world)
    {
        for (int i = 0; i < world.Workplaces.Count; i++)
        {
            if (world.Workplaces[i].Kind == JobKind.Woodcutter)
            {
                return true;
            }
        }

        return false;
    }

    private static void MarkSomewhereNear(
        SimWorld world, BuildingKind kind, GridPos site, int radius)
    {
        for (int dy = -radius; dy <= radius; dy++)
        {
            for (int dx = -radius; dx <= radius; dx++)
            {
                if (world.Mark(kind, new GridPos(site.X + dx, site.Y + dy)).Allowed)
                {
                    return;
                }
            }
        }
    }

    /// <summary>
    /// ⭐ A starved head of the queue does not stop the builders working behind it.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>D135, and Joe watched it happen:</b> <i>"the builder shouldn't just sit at the
    /// building waiting."</i> His woodcutter's hut was <em>"Queue: 1st of 3"</em> at 12 of 25
    /// logs with <em>"Work: 0 of 40 ticks done"</em>, and two sites queued behind it. Measured
    /// independently: a house reading <em>"30 logs delivered, 0 still wanted"</em> went
    /// untouched for three years while the builders waited on the site in front of it.
    /// </para>
    /// <para>
    /// Every builder asked <c>NextToBuild()</c>, which answers <em>what is first</em>. That is
    /// the right question for where scarce timber should go and the wrong one for what a pair
    /// of hands should do next.
    /// </para>
    /// <para>
    /// <b>⚠️ THE STORES ARE DRAINED EVERY TICK, and that is what makes this a discriminator
    /// rather than a coincidence.</b> If any timber were allowed to reach a store the builders
    /// would fetch it for the head, finish it, and reach the second site by ordinary queue
    /// order — and the test would pass with the bug still in place. Holding the village at
    /// zero timber for ever asks the one question that matters: <em>there is nothing to fetch
    /// and one site is already stocked; does anybody build it?</em>
    /// </para>
    /// <para>
    /// It deliberately does <b>not</b> assert that the head stays unbuilt. The queue governs
    /// materials, not labour (D102), and a head that somehow got its logs SHOULD be built.
    /// </para>
    /// </remarks>
    [Fact]
    public void AStarvedHeadOfQueueDoesNotStopTheBuildersBehindIt()
    {
        SimConfig config = VillageFixtures.Village;
        SimLoop loop = Loop(config);
        SimWorld world = loop.World;

        PaintHomeGround(world);
        MarkABuildersHut(world);
        loop.Step(config.TicksPerYear);

        // Two sites, in queue order. The first will never see a log; the second is handed
        // everything it needs up front.
        MarkSomewhereNear(world, BuildingKind.Granary, world.Map.FoundingSite, 4);
        MarkSomewhereNear(world, BuildingKind.Shed, world.Map.FoundingSite, 6);

        Workplace? starved = null;
        Workplace? stocked = null;
        foreach (Workplace place in world.Workplaces)
        {
            if (place.Construction is not { IsFinished: false } site)
            {
                continue;
            }

            if (site.Kind == BuildingKind.Granary)
            {
                starved = place;
            }
            else if (site.Kind == BuildingKind.Shed)
            {
                stocked = place;
            }
        }

        Assert.NotNull(starved);
        Assert.NotNull(stocked);

        stocked!.Construction!.Deliver(stocked.Construction.LogsStillNeeded);
        Assert.True(stocked.Construction.HasMaterials);

        // Anti-vacuity: the starved one must really be ahead of it, or nothing is blocked.
        Assert.True(
            starved!.EffectiveQueueRank < stocked.EffectiveQueueRank,
            $"The granary ranks {starved.EffectiveQueueRank} against the shed's "
            + $"{stocked.EffectiveQueueRank}, so nothing is in front of anything.");

        for (int tick = 0; tick < config.TicksPerYear && !stocked.Construction.IsFinished; tick++)
        {
            loop.Step(1);

            // Not one log anywhere, ever. There is nothing to fetch for the head.
            foreach (StoreBuilding store in world.StoreBuildings)
            {
                if (store.Store.Logs > 0)
                {
                    store.Store.TryTake(Goods.Logs, store.Store.Logs);
                }
            }
        }

        _output.WriteLine(
            $"a year with no timber anywhere: the stocked shed is "
            + $"{(stocked.Construction.IsFinished ? "built" : "STILL A SITE")}, the starved "
            + $"granary holds {starved.Construction!.LogsDelivered} logs.");

        Assert.True(
            stocked.Construction.IsFinished,
            "A shed with every log it needs went unbuilt for a year because the site ahead of "
            + "it in the queue was waiting on timber that does not exist. The queue is meant to "
            + "decide where materials go, not to stop a pair of hands working.");
    }
}

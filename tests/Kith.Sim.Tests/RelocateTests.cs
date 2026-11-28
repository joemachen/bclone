using Kith.Sim.Config;
using Kith.Sim.Core;
using Kith.Sim.Logging;
using Kith.Sim.World;
using Xunit;
using Xunit.Abstractions;

namespace Kith.Sim.Tests;

/// <summary>
/// ⭐⭐ Relocation — <b>the remedy that lets the sim impose a placement at all</b>.
/// </summary>
/// <remarks>
/// <para>
/// Joe, 2026-08-26: *"if we are going to allow the sim to place it, then we need to add a
/// 'relocate' function for all buildings (storage buildings must be 'emptied' first)."* **The sim
/// may only impose a placement if the player can undo it** — `§0.1`'s *recoverable by design*
/// applied to layout, and the reason a gifted building is a gift rather than a trap.
/// </para>
/// <para>
/// ⛔ <b>Houses are not here, and that is D228</b>: housing is the brush's business in both
/// directions. <see cref="ResidentialZoneTests"/> holds that half.
/// </para>
/// </remarks>
public sealed class RelocateTests
{
    private readonly ITestOutputHelper _output;

    public RelocateTests(ITestOutputHelper output) => _output = output;

    private static SimConfig Config => VillageFixtures.Village;

    private static SimLoop Loop() =>
        SimFactory.CreatePhase0(Config, new InMemoryLogSink());

    /// <summary>
    /// A tile the village may build on, found rather than assumed — <b>and found NEAR THEM</b>.
    /// </summary>
    /// <remarks>
    /// ⚠️ <b>It used to scan from the map's corner, and that quietly broke the one guard that
    /// needed people to walk.</b> The first buildable tile in the valley is in the far top-left;
    /// a granary put there is a granary nobody goes to, so *"emptied after three years"* measured
    /// the distance rather than the errand. **Searching outward from the founding site puts the
    /// building where a player would put it**, which is what the guard was always assuming.
    /// </remarks>
    private static GridPos Buildable(SimWorld world, params GridPos[] avoid)
    {
        GridPos centre = world.Map.FoundingSite;

        for (int ring = 1; ring < 30; ring++)
        {
            for (int dy = -ring; dy <= ring; dy++)
            {
                for (int dx = -ring; dx <= ring; dx++)
                {
                    var at = new GridPos(centre.X + dx, centre.Y + dy);
                    if (System.Array.Exists(avoid, a => a == at) || !world.Map.Contains(at))
                    {
                        continue;
                    }

                    if (world.Map.TerrainAt(at) == Terrain.Grass
                        && world.CanBuildAt(BuildingKind.Granary, at).Allowed)
                    {
                        return at;
                    }
                }
            }
        }

        throw new System.InvalidOperationException("No buildable tile near the village.");
    }

    /// <summary>Step until an emptying store has been emptied and closed (D413), or give up.</summary>
    private static bool StepUntilEmptied(SimLoop loop, StoreBuilding store, int ticks)
    {
        for (int t = 0; t < ticks; t++)
        {
            loop.StepOnce();
            if (store.Stocking == Stocking.Closed)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Deliver a site's materials and work it to completion, as a crew would.</summary>
    private static void Finish(SimWorld world, GridPos site)
    {
        Workplace found = world.Workplaces.Single(w => w.Tile == site && w.IsSite);
        ConstructionSite plan = found.Construction!;

        foreach (MaterialCost owed in plan.Recipe.Materials)
        {
            plan.Deliver(owed.Goods, owed.Amount);
        }

        while (!plan.IsFinished)
        {
            plan.Work();
        }

        world.Complete(found);
    }

    private static StoreBuilding StandAGranary(SimWorld world, out GridPos at)
    {
        at = Buildable(world);
        world.Mark(BuildingKind.Granary, at);
        Finish(world, at);
        return world.StoreAt(at)!;
    }

    // -----------------------------------------------------------------

    /// <summary>⭐ An empty store moves, and the same building arrives.</summary>
    [Fact]
    public void AnEmptyStoreMovesAndKeepsItsIdentity()
    {
        SimLoop loop = Loop();
        SimWorld world = loop.World;

        StoreBuilding granary = StandAGranary(world, out GridPos from);
        string name = granary.Name;
        int capacity = granary.Store.Capacity;

        GridPos to = Buildable(world, from);
        PlacementVerdict verdict = world.MarkRelocation(from, to);
        Assert.True(verdict.Allowed, verdict.Reason);

        Finish(world, to);

        // ⭐ Not a new granary — the same object, at a different tile.
        Assert.Same(granary, world.StoreAt(to));
        Assert.Null(world.StoreAt(from));
        Assert.Equal(name, world.StoreAt(to)!.Name);
        Assert.Equal(capacity, world.StoreAt(to)!.Store.Capacity);
    }

    /// <summary>
    /// ⛔ A store with goods in it refuses to move, and the sentence says what to do.
    /// </summary>
    /// <remarks>
    /// <b>Joe's second half:</b> *"storage buildings must be 'emptied' first."* ⚠️ **A refusal with
    /// a reason, not a disabled control** (D43) — and it names the number, because *"empty it
    /// first"* against a granary the player thought was empty is the untraceable outcome §1.1
    /// forbids.
    /// </remarks>
    [Fact]
    public void AStoreWithGoodsInItRefusesToMove()
    {
        SimLoop loop = Loop();
        SimWorld world = loop.World;

        StoreBuilding granary = StandAGranary(world, out GridPos from);
        granary.Store.Receive(Goods.Produce, 200);

        GridPos to = Buildable(world, from);
        PlacementVerdict refused = world.MarkRelocation(from, to);

        _output.WriteLine(refused.Reason);

        Assert.False(refused.Allowed);
        Assert.Contains("Empty it first", refused.Reason, System.StringComparison.Ordinal);
        Assert.Contains("200", refused.Reason, System.StringComparison.Ordinal);

        // ⭐ And emptying it is the whole of the remedy.
        granary.Store.TakeAll(Goods.Produce);
        Assert.True(world.MarkRelocation(from, to).Allowed);
    }

    /// <summary>
    /// ⭐⭐ A store marked for emptying is carried out by people, and then it can move.
    /// </summary>
    /// <remarks>
    /// <b>Joe's second function, end to end</b> (2026-08-26): *"storage buildings must be 'emptied'
    /// first — another function to build."* Before this, the only way to move a full store was to
    /// demolish it and lose everything inside (D221).
    /// </remarks>
    [Fact]
    public void AStoreMarkedForEmptyingIsCarriedOutAndThenCanMove()
    {
        SimLoop loop = Loop();
        SimWorld world = loop.World;

        // Two stores, so there is somewhere for the goods to go.
        StoreBuilding full = StandAGranary(world, out GridPos from);
        StandAGranary(world, out GridPos _);

        full.Store.Receive(Goods.Produce, 120);
        Assert.False(world.MarkRelocation(from, Buildable(world, from)).Allowed);

        // ⭐ No workaround (D413): an emptied store is closed until the player opens it, so it is
        // still empty a year after the last armful left, and it can move. Under D389 it reopened
        // as the last armful left and a delivery refilled it the same tick (D412 found it).
        full.Stocking = Stocking.Emptying;
        Assert.True(StepUntilEmptied(loop, full, Config.TicksPerYear * 3), $"{full.Name} was never emptied in three years.");
        loop.Step(Config.TicksPerYear);

        _output.WriteLine($"{full.Name} holds {full.Store.Held} a year after it was emptied, and is {full.Stocking}");

        Assert.Equal(0, full.Store.Held);
        Assert.True(world.MarkRelocation(from, Buildable(world, from)).Allowed);
    }

    /// <summary>
    /// ⛔ Whoever sets out to empty a store is on that errand, whatever they were doing before (D768, Joe: <i>"im seeing
    /// people bounce back and forth"</i>). The clearing errand walked them off without saying so, so they kept their last
    /// state — Gargard kept <i>going in to get warm</i>, and the warmth rule turned him round at the door every tick of a winter.
    /// </summary>
    [Fact]
    public void WhoeverSetsOutToEmptyAStoreSaysSo()
    {
        SimLoop loop = Loop();
        SimWorld world = loop.World;
        StoreBuilding full = StandAGranary(world, out GridPos _);
        StandAGranary(world, out GridPos _);
        full.Store.Receive(Goods.Produce, 120);
        full.Stocking = Stocking.Emptying;

        int seen = 0;
        var wrong = new List<string>();
        for (int t = 0; t < Config.TicksPerSeason; t++)
        {
            loop.StepOnce();
            foreach (Villager villager in world.Villagers)
            {
                if (villager.Alive && !villager.IsCarrying && villager.LegTarget == full.Tile && villager.Tile != full.Tile)
                {
                    seen++;
                    if (villager.State != VillagerState.ClearingAStore && wrong.Count < 5)
                    {
                        wrong.Add($"{villager.Name} walks to {full.Name} {villager.DescribeState(null, false)} at tick {world.Tick}");
                    }
                }
            }
        }

        _output.WriteLine($"{seen} villager-ticks walking empty-handed to {full.Name}; {wrong.Count} under another errand's name");
        Assert.True(seen > 0, "nobody walked to the store being emptied, so the guard asked nothing");
        Assert.Empty(wrong);
    }

    /// <summary>
    /// ⛔ And the armful taken out is HAULED (D771): the errand ends at the store, and the walk away with the load is
    /// <c>HaulingToStore</c>. Joe, on <c>main</c> after D768: <i>"Oakmont swift and a few of his cohorts are 'bouncing' back
    /// and forth between a few spots like crazy … they are stuck bouncing and wont do anything else."</i> D768 set
    /// <c>ClearingAStore</c> for the walk out, the armful kept it, and that state's continuation — which asks for a store to
    /// clear and finds none for full arms — turned them for home every other tick, one tile and back, all summer.
    /// </summary>
    [Fact]
    public void WhoeverCarriesAnArmfulOutHaulsIt()
    {
        SimLoop loop = Loop();
        SimWorld world = loop.World;
        StoreBuilding full = StandAGranary(world, out GridPos _);
        StandAGranary(world, out GridPos _);
        full.Store.Receive(Goods.Produce, 120);
        full.Stocking = Stocking.Emptying;

        int hauling = 0;
        var wrong = new List<string>();
        for (int t = 0; t < Config.TicksPerSeason; t++)
        {
            loop.StepOnce();
            foreach (Villager villager in world.Villagers)
            {
                if (!villager.Alive || !villager.IsCarrying)
                {
                    continue;
                }

                if (villager.State == VillagerState.HaulingToStore)
                {
                    hauling++;
                }
                else if (villager.State == VillagerState.ClearingAStore && wrong.Count < 5)
                {
                    wrong.Add($"{villager.Name} at {villager.Tile} holds {villager.Carried.Held} {villager.DescribeState(null, false)} at tick {world.Tick}");
                }
            }
        }

        _output.WriteLine($"{hauling} villager-ticks hauling; {wrong.Count} carrying under the clearing errand's name; {full.Name} holds {full.Store.Held}");
        Assert.True(hauling > 0, "nobody hauled anything, so the guard asked nothing");
        Assert.Empty(wrong);
    }

    /// <summary>
    /// ⛔ A store being emptied refuses everything, or the drain would never finish.
    /// </summary>
    /// <remarks>
    /// <b>The refusal IS the mechanism, not a side effect.</b> A store that still accepted goods
    /// would be refilled by the same errands emptying it, and the two would race for ever.
    /// </remarks>
    /// <summary>
    /// ⭐ An emptied store is closed, and stays closed until the player opens it (D413). Joe: *"an
    /// 'emptied' store should be 'closed' once it is marked to be emptied and remain closed until
    /// the user chooses to open it again."*
    /// </summary>
    /// <remarks>
    /// Reverses D389's reopening. Red with the last armful setting it Open: it refills (the
    /// fixture's foragers deliver here) and the year-on read finds it stocked.
    /// </remarks>
    [Fact]
    public void AnEmptiedStoreStaysClosedUntilOpened()
    {
        SimLoop loop = Loop();
        SimWorld world = loop.World;

        StoreBuilding full = StandAGranary(world, out GridPos _);
        StandAGranary(world, out GridPos _);
        full.Store.Receive(Goods.Produce, 120);

        Assert.True(world.SetStocking(full, Stocking.Emptying).Allowed);
        Assert.Equal(Stocking.Emptying, full.Stocking);
        Assert.False(full.Accepts(Goods.Produce));

        Assert.True(StepUntilEmptied(loop, full, Config.TicksPerYear * 3), $"{full.Name} was never emptied in three years.");
        loop.Step(Config.TicksPerYear);
        _output.WriteLine($"{full.Name} holds {full.Store.Held} a year after it was emptied, and is {full.Stocking}");

        Assert.Equal(Stocking.Closed, full.Stocking);
        Assert.Equal(0, full.Store.Held);
        Assert.False(full.Accepts(Goods.Produce), "the emptied store took deliveries nobody opened it for");

        // And the player's word opens it.
        Assert.True(world.SetStocking(full, Stocking.Open).Allowed);
        Assert.True(full.Accepts(Goods.Produce));
    }

    /// <summary>
    /// ⭐ A closed store takes no deliveries and nobody carries it out (D389) — the player's,
    /// until they say otherwise; households still fetch from it, which is what a shelf is for;
    /// and it is in the hash.
    /// </summary>
    [Fact]
    public void AClosedStoreTakesNothingAndKeepsWhatItHas()
    {
        SimLoop loop = Loop();
        SimWorld world = loop.World;

        StoreBuilding shut = StandAGranary(world, out GridPos _);
        StandAGranary(world, out GridPos _);
        shut.Store.Receive(Goods.Produce, 120);

        ulong open = Determinism.StateHash.Compute(world);
        Assert.True(world.SetStocking(shut, Stocking.Closed).Allowed);
        Assert.NotEqual(open, Determinism.StateHash.Compute(world));
        Assert.False(shut.Accepts(Goods.Produce));
        Assert.False(shut.Emptying);

        int cleared = 0;
        for (int tick = 0; tick < Config.TicksPerYear; tick++)
        {
            loop.StepOnce();
            foreach (Villager villager in world.Villagers)
            {
                if (villager.Alive && villager.State == VillagerState.ClearingAStore)
                {
                    cleared++;
                }
            }
        }

        _output.WriteLine($"{shut.Name} holds {shut.Store[Goods.Produce]} a year on, {cleared} clearing ticks");
        Assert.Equal(Stocking.Closed, shut.Stocking);
        Assert.Equal(0, cleared);

        // Emptying an empty store is being closed (nothing to carry out, D413 — it was "open" under D389).
        shut.Store.TakeAll(Goods.Produce);
        Assert.True(world.SetStocking(shut, Stocking.Open).Allowed);
        Assert.True(world.SetStocking(shut, Stocking.Emptying).Allowed);
        Assert.Equal(Stocking.Closed, shut.Stocking);
    }

    [Fact]
    public void AStoreBeingEmptiedRefusesEverything()
    {
        SimWorld world = Loop().World;
        StoreBuilding granary = StandAGranary(world, out GridPos _);

        Assert.True(granary.Accepts(Goods.Produce));

        granary.Stocking = Stocking.Emptying;
        Assert.False(granary.Accepts(Goods.Produce));

        // ⭐ And clearing the request puts it straight back to work.
        granary.Stocking = Stocking.Open;
        Assert.True(granary.Accepts(Goods.Produce));
    }

    /// <summary>
    /// ⚠️ With nowhere to put the goods, nobody is sent to fetch them.
    /// </summary>
    /// <remarks>
    /// <b>Otherwise the village would send people to carry armfuls they could only put back</b> —
    /// emptying a store into itself, for ever. The errand simply does not exist, which leaves the
    /// relocate refusal telling the player the truth rather than a queue of futile walks.
    /// </remarks>
    [Fact]
    public void WithNowhereToPutThemNobodyIsSentToFetchThem()
    {
        SimLoop loop = Loop();
        SimWorld world = loop.World;

        StoreBuilding only = StandAGranary(world, out GridPos _);

        // Every other store gone, so this one has nowhere to send anything.
        foreach (StoreBuilding other in world.StoreBuildings.Where(s => !ReferenceEquals(s, only)).ToList())
        {
            world.Demolish(other);
        }

        only.Store.Receive(Goods.Produce, 90);
        only.Stocking = Stocking.Emptying;
        loop.Step(Config.TicksPerYear);

        Assert.Equal(90, only.Store.Held);
        Assert.DoesNotContain(
            world.Villagers,
            v => v.Alive && v.State == VillagerState.ClearingAStore);
    }

    /// <summary>
    /// ⛔⛔ Somebody actually comes and pulls a marked building down.
    /// </summary>
    /// <remarks>
    /// <b>Joe, playing:</b> *"I marked the forester hut for demolition and even though there was a
    /// builder in the village, no one ever demolished the building."* **A marked building nobody
    /// comes to is the same shape as a feature with no button** — the work exists and never
    /// happens, which is worse than not having built it.
    /// </remarks>
    [Fact]
    public void SomebodyComesAndPullsAMarkedBuildingDown()
    {
        SimLoop loop = Loop();
        SimWorld world = loop.World;

        // A hut the village is not using, so nothing else competes for the crew.
        GridPos at = Buildable(world);
        world.Mark(BuildingKind.ForesterHut, at);
        Finish(world, at);
        Assert.NotNull(world.WhatStandsAt(at));

        Assert.True(world.MarkDemolition(at).Allowed);
        Assert.NotNull(world.DemolitionSiteAt(at));

        // ⚠️ AND SOMETHING ELSE IN THE QUEUE, WHICH IS THE CASE JOE ACTUALLY HAD. A demolition
        // site takes a fresh workplace id, and `EffectiveQueueRank` falls back to the id — so it
        // sorts behind every site already marked. **A village that is always building would never
        // pull anything down**, and the first version of this guard had an empty queue and so
        // could not see it.
        world.Mark(BuildingKind.Granary, Buildable(world, at));

        loop.Step(Config.TicksPerYear * 5);

        _output.WriteLine(world.DemolitionSiteAt(at) is Workplace still
            ? $"five years on it is still standing, {still.Construction!.WorkDone} ticks of work done"
            : "it came down");

        Assert.Null(world.DemolitionSiteAt(at));
        Assert.Null(world.WhatStandsAt(at));
    }

    /// <summary>⛔ A house is never moved by hand — the brush is its only control (D228).</summary>
    [Fact]
    public void AHouseIsNotMovedByHand()
    {
        SimLoop loop = Loop();
        loop.Step(Config.TicksPerYear * 20);
        SimWorld world = loop.World;

        GridPos lived = world.Households.First(h => h.HasHome).Home();
        PlacementVerdict refused = world.MarkRelocation(lived, Buildable(world, lived));

        _output.WriteLine(refused.Reason);

        Assert.False(refused.Allowed);
        Assert.Contains("Unpaint", refused.Reason, System.StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>⭐⭐ A library takes its records with it — the shelves are the building.</summary>
    [Fact]
    public void AMovedLibraryKeepsItsRecords()
    {
        SimLoop loop = Loop();
        SimWorld world = loop.World;

        GridPos from = Buildable(world);
        var library = new Library
        {
            Position = Point.CentreOf(from),
            Name = "library 1",
            Shelves = Config.LibraryShelves,
        };
        library.Records.Add(new LibraryRecord(2, "Wendell", 12));
        world.Libraries.Add(library);

        GridPos to = Buildable(world, from);
        Assert.True(world.MarkRelocation(from, to).Allowed);
        Finish(world, to);

        Assert.Equal(to, library.Tile);
        Assert.True(world.IsWrittenDown(2));
    }

    /// <summary>⛔ Nothing to move is a sentence, not a silent no-op.</summary>
    [Fact]
    public void ThereIsNothingThereToMove()
    {
        SimWorld world = Loop().World;
        GridPos empty = Buildable(world);

        PlacementVerdict refused = world.MarkRelocation(empty, Buildable(world, empty));

        Assert.False(refused.Allowed);
        Assert.Contains("nothing there", refused.Reason, System.StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// ⚠️ A source demolished mid-move raises nothing, and the village says so.
    /// </summary>
    /// <remarks>
    /// <b>The phantom this guards against would have been a free building.</b> A relocation site
    /// that fell through to the ordinary raise path would hand the player a granary nobody paid
    /// for — the *plausible default* shape of bug this project keeps finding, rather than a crash.
    /// </remarks>
    [Fact]
    public void ASourceDemolishedMidMoveRaisesNothing()
    {
        SimLoop loop = Loop();
        SimWorld world = loop.World;

        StoreBuilding granary = StandAGranary(world, out GridPos from);
        GridPos to = Buildable(world, from);
        Assert.True(world.MarkRelocation(from, to).Allowed);

        int stores = world.StoreBuildings.Count;
        world.Demolish(granary);
        Finish(world, to);

        Assert.Null(world.StoreAt(to));
        Assert.Equal(stores - 1, world.StoreBuildings.Count);
    }

    // -----------------------------------------------------------------
    //  The revamp (`specs/move-revamp.md`, D947): the outline asks the sim first, and may turn.
    // -----------------------------------------------------------------

    /// <summary>A hut stood and finished near the village, for something to be in the way.</summary>
    private static GridPos StandAHut(SimWorld world, params GridPos[] avoid)
    {
        GridPos at = Buildable(world, avoid);
        Assert.True(world.Mark(BuildingKind.ForesterHut, at).Allowed);
        Finish(world, at);
        Assert.NotNull(world.WhatStandsAt(at));
        return at;
    }

    /// <summary>
    /// ⛔⛔ A move lands only where a fresh building could stand — the hole `move-revamp.md §1.1` found.
    /// </summary>
    /// <remarks>
    /// <b><c>alreadyStanding</c> skipped the overlap, the doorstep, the wall-off and every covered tile</b>, to stop a
    /// moved building refusing itself — so it could be moved onto another building, onto a doorstep, or with a 2×2's
    /// far end in the river. Swept over the ground round the village at two facings: wherever the outline does not touch
    /// its own old ground and a fresh granary's refusal does not name the granary itself, the move and the fresh build
    /// must say the same thing. ⚠️ And the sweep must reach the shapes it is about, or it proves nothing (D423): it
    /// counts its overlaps and its water.
    /// </remarks>
    [Fact]
    public void AMoveLandsOnlyWhereAFreshBuildingCould()
    {
        SimWorld world = Loop().World;
        StoreBuilding granary = StandAGranary(world, out GridPos from);
        StandAHut(world, from);
        Footprint own = granary.Footprint;

        GridPos centre = world.Map.FoundingSite;
        var mismatches = new List<string>();
        int overlaps = 0, water = 0, compared = 0;
        foreach (Angle facing in new[] { Angle.Zero, Angle.FromTurnFraction(1, 8) })
        {
            for (int dy = -14; dy <= 14; dy++)
            {
                for (int dx = -14; dx <= 14; dx++)
                {
                    var tile = new GridPos(centre.X + dx, centre.Y + dy);
                    if (!world.Map.Contains(tile))
                    {
                        continue;
                    }

                    Point at = world.AnchorOn(BuildingKind.Granary, tile);
                    if (world.FootprintOf(BuildingKind.Granary, at, facing).Overlaps(own))
                    {
                        continue;
                    }

                    PlacementVerdict fresh = world.CanBuildAt(BuildingKind.Granary, at, facing: facing);
                    if (!fresh.Allowed && fresh.Reason.Contains(granary.Name, System.StringComparison.Ordinal))
                    {
                        continue;
                    }

                    PlacementVerdict moved = world.CanMoveTo(from, at, facing);
                    compared++;
                    overlaps += fresh.Reason == "Something already stands there." ? 1 : 0;
                    water += fresh.Reason.Contains("water", System.StringComparison.Ordinal) ? 1 : 0;
                    if (moved.Allowed != fresh.Allowed || (!fresh.Allowed && moved.Reason != fresh.Reason))
                    {
                        mismatches.Add($"{tile} at {facing.Raw}: fresh {(fresh.Allowed ? "yes" : fresh.Reason)}, move {(moved.Allowed ? "yes" : moved.Reason)}");
                    }
                }
            }
        }

        _output.WriteLine($"{compared} spots compared, {overlaps} on a building, {water} in the water, {mismatches.Count} that disagree");
        foreach (string line in mismatches.Take(12))
        {
            _output.WriteLine(line);
        }

        Assert.True(overlaps > 0, "the sweep never put the outline on another building");
        Assert.True(water > 0, "the sweep never reached the water");
        Assert.Empty(mismatches);
    }

    /// <summary>⛔ Onto another building is refused, and the refusal raises nothing.</summary>
    [Fact]
    public void AMoveOntoAnotherBuildingIsRefusedAndRaisesNoSite()
    {
        SimWorld world = Loop().World;
        StandAGranary(world, out GridPos from);
        GridPos hut = StandAHut(world, from);
        int sites = world.Workplaces.Count(w => w.IsSite);

        PlacementVerdict refused = world.MarkRelocation(from, hut);

        _output.WriteLine(refused.Reason);
        Assert.False(refused.Allowed);
        Assert.Equal("Something already stands there.", refused.Reason);
        Assert.Equal(sites, world.Workplaces.Count(w => w.IsSite));
    }

    /// <summary>⭐ A building may be turned where it stands (call 5) — its own old ground is the one thing it may cover.</summary>
    [Fact]
    public void ABuildingMayTurnWhereItStands()
    {
        SimWorld world = Loop().World;
        StoreBuilding granary = StandAGranary(world, out GridPos from);
        Angle quarter = granary.Facing + Angle.FromTurnFraction(1, 4);

        PlacementVerdict turned = world.CanMoveTo(from, granary.Position, quarter);

        Assert.True(turned.Allowed, turned.Reason);
    }

    /// <summary>⭐ It arrives facing the way the outline was turned (D229 kept the old facing, and nothing could change it).</summary>
    [Fact]
    public void AMoveArrivesAtTheFacingAskedFor()
    {
        SimWorld world = Loop().World;
        StoreBuilding granary = StandAGranary(world, out GridPos from);
        GridPos to = Buildable(world, from);

        // ⚠️ A quarter ON FROM ITS OWN, not a quarter: a fresh granary faces the nearest store (`doors.md §12.3`), and the
        // first version of this asked for the facing it already had — the red check that dropped the facing scored zero.
        Angle quarter = granary.Facing + Angle.FromTurnFraction(1, 4);

        PlacementVerdict moved = world.MarkRelocation(from, world.AnchorOn(BuildingKind.Granary, to), quarter);
        Assert.True(moved.Allowed, moved.Reason);
        Finish(world, to);

        Assert.Same(granary, world.StoreAt(to));
        Assert.Equal(quarter, granary.Facing);
    }

    /// <summary>
    /// ⭐ A building may step onto its own old doorstep — its door goes with it, so its own door is not in its way.
    /// </summary>
    /// <remarks>
    /// The sweep above never covers its own ground, so without this the door's exemption scored zero. ⚠️ The granary is
    /// stood where its doorstep, and the ground it would step onto, is no other building's: the first one stepped onto the
    /// founders' forager's hut's doorstep too, and that is rightly refused.
    /// </remarks>
    [Fact]
    public void ABuildingMayStepOntoItsOwnDoorstep()
    {
        SimWorld world = Loop().World;
        var tried = new List<GridPos>();
        StoreBuilding? granary = null;
        GridPos from = default;
        Door door = default;
        while (granary is null && tried.Count < 40)
        {
            GridPos at = Buildable(world, tried.ToArray());
            tried.Add(at);
            Assert.True(world.Mark(BuildingKind.Granary, at).Allowed);
            Finish(world, at);
            StoreBuilding stood = world.StoreAt(at)!;
            if (world.DoorCovering(at) is Door d && world.WhoseDoorstep(d.Doorstep) == stood.Name
                && world.FootprintOf(BuildingKind.Granary, world.AnchorOn(BuildingKind.Granary, d.Doorstep), stood.Facing)
                    .CoveredTiles().TrueForAll(t => world.WhoseDoorstep(t) is null || world.WhoseDoorstep(t) == stood.Name))
            {
                (granary, from, door) = (stood, at, d);
            }
        }

        Assert.NotNull(granary);

        Point onto = world.AnchorOn(BuildingKind.Granary, door.Doorstep);
        Assert.True(world.FootprintOf(BuildingKind.Granary, onto, granary.Facing).Covers(door.Doorstep));

        PlacementVerdict stepped = world.CanMoveTo(from, onto, granary.Facing);

        _output.WriteLine(stepped.Allowed ? "allowed" : stepped.Reason);
        Assert.True(stepped.Allowed, stepped.Reason);
    }

    /// <summary>⛔ A building already on the move is refused by name (call 7), so no builder's work is wasted on a second site.</summary>
    [Fact]
    public void ABuildingAlreadyOnTheMoveIsRefusedByName()
    {
        SimWorld world = Loop().World;
        StoreBuilding granary = StandAGranary(world, out GridPos from);
        GridPos first = Buildable(world, from);
        Assert.True(world.MarkRelocation(from, first).Allowed);

        PlacementVerdict again = world.CanMoveTo(from, world.AnchorOn(BuildingKind.Granary, Buildable(world, from, first)), granary.Facing);

        _output.WriteLine(again.Reason);
        Assert.False(again.Allowed);
        Assert.Contains("already being moved", again.Reason, System.StringComparison.Ordinal);
        Assert.Contains(granary.Name, again.Reason, System.StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>⚠️ A site is told it is still being built (call 8), not that there is nothing there.</summary>
    [Fact]
    public void ASiteIsToldItIsStillBeingBuilt()
    {
        SimWorld world = Loop().World;
        GridPos at = Buildable(world);
        Assert.True(world.Mark(BuildingKind.Granary, at).Allowed);

        PlacementVerdict refused = world.MarkRelocation(at, Buildable(world, at));

        _output.WriteLine(refused.Reason);
        Assert.False(refused.Allowed);
        Assert.Contains("still being built", refused.Reason, System.StringComparison.Ordinal);
    }

    /// <summary>⭐ A move can be called off from the building (call 11): the site goes, the building stays.</summary>
    [Fact]
    public void AMoveCanBeCalledOffFromTheBuilding()
    {
        SimWorld world = Loop().World;
        StoreBuilding granary = StandAGranary(world, out GridPos from);
        GridPos to = Buildable(world, from);
        Assert.True(world.MarkRelocation(from, to).Allowed);
        Assert.NotNull(world.MoveUnderWay(from));

        Assert.True(world.CallOffTheMove(from));

        Assert.Null(world.MoveUnderWay(from));
        Assert.DoesNotContain(world.Workplaces, w => w.IsSite && w.Construction!.MovingFrom == from);
        Assert.Same(granary, world.StoreAt(from));
        Assert.False(world.CallOffTheMove(from));
    }
}

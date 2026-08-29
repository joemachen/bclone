using Bclone.Sim.Config;
using Bclone.Sim.Core;
using Bclone.Sim.Logging;
using Bclone.Sim.World;
using Xunit;
using Xunit.Abstractions;

namespace Bclone.Sim.Tests;

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

    /// <summary>Deliver a site's materials and work it to completion, as a crew would.</summary>
    private static void Finish(SimWorld world, GridPos site)
    {
        Workplace found = world.Workplaces.Single(w => w.Position == site && w.IsSite);
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
        granary.Store.Receive(Goods.Food, 200);

        GridPos to = Buildable(world, from);
        PlacementVerdict refused = world.MarkRelocation(from, to);

        _output.WriteLine(refused.Reason);

        Assert.False(refused.Allowed);
        Assert.Contains("Empty it first", refused.Reason, System.StringComparison.Ordinal);
        Assert.Contains("200", refused.Reason, System.StringComparison.Ordinal);

        // ⭐ And emptying it is the whole of the remedy.
        granary.Store.TakeAll(Goods.Food);
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

        full.Store.Receive(Goods.Food, 120);
        Assert.False(world.MarkRelocation(from, Buildable(world, from)).Allowed);

        full.Emptying = true;
        loop.Step(Config.TicksPerYear * 3);

        _output.WriteLine($"{full.Name} holds {full.Store.Held} after three years of clearing");

        Assert.Equal(0, full.Store.Held);
        Assert.True(world.MarkRelocation(from, Buildable(world, from)).Allowed);
    }

    /// <summary>
    /// ⛔ A store being emptied refuses everything, or the drain would never finish.
    /// </summary>
    /// <remarks>
    /// <b>The refusal IS the mechanism, not a side effect.</b> A store that still accepted goods
    /// would be refilled by the same errands emptying it, and the two would race for ever.
    /// </remarks>
    [Fact]
    public void AStoreBeingEmptiedRefusesEverything()
    {
        SimWorld world = Loop().World;
        StoreBuilding granary = StandAGranary(world, out GridPos _);

        Assert.True(granary.Accepts(Goods.Food));

        granary.Emptying = true;
        Assert.False(granary.Accepts(Goods.Food));

        // ⭐ And clearing the request puts it straight back to work.
        granary.Emptying = false;
        Assert.True(granary.Accepts(Goods.Food));
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

        only.Store.Receive(Goods.Food, 90);
        only.Emptying = true;
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
            Position = from,
            Name = "library 1",
            Shelves = Config.LibraryShelves,
        };
        library.Records.Add(new LibraryRecord(2, "Wendell"));
        world.Libraries.Add(library);

        GridPos to = Buildable(world, from);
        Assert.True(world.MarkRelocation(from, to).Allowed);
        Finish(world, to);

        Assert.Equal(to, library.Position);
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
}

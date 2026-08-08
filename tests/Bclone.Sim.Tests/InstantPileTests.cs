using Bclone.Sim.Config;
using Bclone.Sim.Core;
using Bclone.Sim.Logging;
using Bclone.Sim.World;
using Xunit;
using Xunit.Abstractions;

namespace Bclone.Sim.Tests;

/// <summary>
/// A storage pile is instant, and its cost is the clearing — D96,
/// <c>specs/goods-on-the-ground.md §5</c>.
/// </summary>
/// <remarks>
/// <para>
/// Joe: <em>"If there are resources, they must first be cleared and then the stockpile can be
/// instant."</em> That moves the pile's price off an abstract <c>pile_work_ticks</c> — eight
/// ticks of levelling bare earth, strange on its own terms — and onto the harvest brush, where
/// it is visible, on the map, and paid in the currency the rest of the game uses.
/// </para>
/// <para>
/// <b>It is also what closes D95's window.</b> The cart's refusal of logs was built and
/// reverted because a pile was a construction site: between marking one and it standing, a
/// forester had nowhere on earth to put a load, and the village built nothing at all.
/// </para>
/// </remarks>
public sealed class InstantPileTests
{
    private readonly ITestOutputHelper _output;

    public InstantPileTests(ITestOutputHelper output) => _output = output;

    private static SimWorld World(SimConfig config) =>
        ManagedVillage.Loop(config, new InMemoryLogSink()).World;

    /// <summary>A tile near the village with nothing standing on it.</summary>
    private static GridPos ClearGroundNear(SimWorld world)
    {
        GridPos site = world.Map.FoundingSite;
        for (int radius = 1; radius < 12; radius++)
        {
            for (int dy = -radius; dy <= radius; dy++)
            {
                for (int dx = -radius; dx <= radius; dx++)
                {
                    var at = new GridPos(site.X + dx, site.Y + dy);

                    // Buildable AND bare. Wooded ground is buildable too now (D100) — the
                    // village clears it — but a pile marked there waits, and these guards
                    // are about the one that does not.
                    if (!world.HasSomethingToHarvest(at)
                        && world.CanBuildAt(BuildingKind.Pile, at).Allowed)
                    {
                        return at;
                    }
                }
            }
        }

        throw new Xunit.Sdk.XunitException("No clear ground near the founding site.");
    }

    /// <summary>A forest tile near the village, or null if this valley has none in reach.</summary>
    private static GridPos? WoodedGroundNear(SimWorld world)
    {
        GridPos site = world.Map.FoundingSite;
        for (int radius = 1; radius < 15; radius++)
        {
            for (int dy = -radius; dy <= radius; dy++)
            {
                for (int dx = -radius; dx <= radius; dx++)
                {
                    var at = new GridPos(site.X + dx, site.Y + dy);
                    if (world.Map.Contains(at) && world.HasSomethingToHarvest(at))
                    {
                        return at;
                    }
                }
            }
        }

        return null;
    }

    // ---------------------------------------------------------------
    //  Instant
    // ---------------------------------------------------------------

    /// <summary>⭐ A pile on clear ground stands the moment it is marked.</summary>
    /// <remarks>
    /// <b>No site, no builder, no wait.</b> A construction site for a building that costs
    /// nothing and owes no work is a builder walking over to a footprint to do nothing — and
    /// the window it opens is what killed D95's attempt at the cart.
    /// </remarks>
    [Fact]
    public void APileOnClearGroundStandsAtOnce()
    {
        SimWorld world = World(VillageFixtures.Village);

        int stores = world.StoreBuildings.Count;
        int workplaces = world.Workplaces.Count;

        GridPos at = ClearGroundNear(world);
        Assert.True(world.Mark(BuildingKind.Pile, at).Allowed);

        _output.WriteLine(
            $"marked a pile at {at}: {world.StoreBuildings.Count - stores} store built, "
            + $"{world.Workplaces.Count - workplaces} workplaces added");

        Assert.Equal(stores + 1, world.StoreBuildings.Count);
        Assert.Equal(workplaces, world.Workplaces.Count);
        Assert.Contains(
            world.StoreBuildings, store => store.Kind == StoreKind.Pile && store.Position == at);

        // And nothing anywhere is still waiting to be built.
        Assert.DoesNotContain(world.Workplaces, place => place.Construction is not null);
    }

    /// <summary>It holds things straight away, which is the whole reason it is instant.</summary>
    [Fact]
    public void APileIsUsableTheTickItIsPlaced()
    {
        SimWorld world = World(VillageFixtures.Village);
        GridPos at = ClearGroundNear(world);
        world.Mark(BuildingKind.Pile, at);

        StoreBuilding pile = Assert.Single(
            world.StoreBuildings, store => store.Kind == StoreKind.Pile);

        _output.WriteLine($"the pile holds {pile.Store.Capacity} of anything");

        Assert.True(pile.Store.Capacity > 0);
        Assert.True(pile.Accepts(Goods.Logs));
        Assert.Equal(40, pile.Store.Receive(Goods.Logs, 40));
    }

    // ---------------------------------------------------------------
    //  …but only on ground that is clear
    // ---------------------------------------------------------------

    /// <summary>
    /// ⭐ A pile marked on wooded ground is accepted, and the village clears the ground.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Joe, correcting me (D100):</b> <em>"I want laborers to auto-remove the resources if a
    /// building is placed on a resource — the user can if they choose to, but shouldn't have
    /// to."</em> The first version of this refused the mark and told the player to clear it
    /// first, which reads the rule backwards: <b>the clearing is still what a pile costs, but
    /// it is a price the village pays rather than an errand the player is sent on.</b>
    /// </para>
    /// <para>
    /// <b>No new machinery.</b> Marking paints the tile for harvest, and the laborers who
    /// already clear painted ground (D87) come and take it. What the mark adds is the
    /// intent — which is `building-placement.md §12.1`'s pattern exactly: the player paints
    /// intent, and the village acts on it when it has a reason to.
    /// </para>
    /// </remarks>
    [Fact]
    public void APileMarkedOnWoodedGroundPaintsItForClearing()
    {
        SimWorld world = World(VillageFixtures.Village);

        GridPos? wooded = WoodedGroundNear(world);
        Assert.NotNull(wooded);
        GridPos at = wooded.Value;

        Assert.False(world.Zones.IsHarvest(at));
        PlacementVerdict verdict = world.Mark(BuildingKind.Pile, at);
        _output.WriteLine($"marked a pile on {at}: allowed={verdict.Allowed}, "
            + $"painted={world.Zones.IsHarvest(at)}, "
            + $"waiting={world.BuildingsWaitingOnTheGround.Count}");

        Assert.True(verdict.Allowed);
        Assert.True(world.Zones.IsHarvest(at), "Marking did not ask for the ground to be cleared.");

        // Not standing yet — the ground is still wooded, and that is the cost.
        Assert.DoesNotContain(world.StoreBuildings, store => store.Kind == StoreKind.Pile);
        Assert.Contains(at, world.BuildingsWaitingOnTheGround);
    }

    /// <summary>⭐ And it goes up the moment the ground comes clear.</summary>
    /// <remarks>
    /// Hung off <c>SetTerrain</c> — the one door terrain changes through (D85) — so it fires
    /// whoever did the clearing: a laborer working the paint, or the player doing it by hand.
    /// </remarks>
    [Fact]
    public void APileWaitingOnItsGroundGoesUpWhenTheGroundIsCleared()
    {
        SimWorld world = World(VillageFixtures.Village);

        GridPos at = WoodedGroundNear(world)!.Value;
        world.Mark(BuildingKind.Pile, at);
        Assert.Single(world.BuildingsWaitingOnTheGround);

        // Exactly what a laborer's finished day of work does to the tile.
        (Goods goods, int amount) = world.Harvest(at);
        _output.WriteLine($"cleared {at} for {amount} {goods}; "
            + $"{world.BuildingsWaitingOnTheGround.Count} still waiting, "
            + $"{world.StoreBuildings.Count} stores");

        Assert.True(amount > 0);
        Assert.Empty(world.BuildingsWaitingOnTheGround);
        Assert.Contains(
            world.StoreBuildings, store => store.Kind == StoreKind.Pile && store.Position == at);
    }

    /// <summary>⭐ And the laborers actually do it, unprompted, in a played village.</summary>
    /// <remarks>
    /// The behavioural half, and the one that answers Joe's sentence rather than its
    /// mechanism: <em>the user shouldn't have to.</em> Nothing here paints anything or clears
    /// anything — a pile is marked on a tree and the village is left to get on with it.
    /// </remarks>
    [Fact]
    public void TheVillageClearsTheGroundForAPileWithoutBeingAskedTwice()
    {
        SimConfig config = VillageFixtures.Village;
        SimLoop loop = ManagedVillage.Loop(config, new InMemoryLogSink());
        SimWorld world = loop.World;

        GridPos at = WoodedGroundNear(world)!.Value;
        world.Mark(BuildingKind.Pile, at);

        loop.Step(config.TicksPerYear / 2);

        _output.WriteLine(
            $"half a year after marking a pile on woodland: "
            + $"{world.BuildingsWaitingOnTheGround.Count} waiting, terrain now "
            + $"{world.Map.TerrainAt(at)}");

        Assert.Empty(world.BuildingsWaitingOnTheGround);
        Assert.Contains(
            world.StoreBuildings, store => store.Kind == StoreKind.Pile && store.Position == at);
    }

    /// <summary>Every building asks for its ground to be cleared, not only the pile.</summary>
    /// <remarks>
    /// Joe's wording is <em>"if a building is placed on a resource"</em>, so the paint is not
    /// a pile rule. The site itself still exists straight away — a pile is the one thing that
    /// <em>is</em> the ground it stands on, so it is the one thing with nothing to exist as
    /// until the ground is bare.
    /// </remarks>
    [Fact]
    public void MarkingAnyBuildingOnAResourceAsksForItToBeCleared()
    {
        SimWorld world = World(VillageFixtures.Village);

        GridPos at = WoodedGroundNear(world)!.Value;
        Assert.True(world.Mark(BuildingKind.Shed, at).Allowed);

        Assert.True(world.Zones.IsHarvest(at));

        // …and the shed's site exists straight away, unlike a pile's.
        Assert.Contains(world.Workplaces, place => place.Construction?.Kind == BuildingKind.Shed);
        Assert.Empty(world.BuildingsWaitingOnTheGround);
    }

    /// <summary>⭐ …and no work goes into it until the ground is bare (Joe, D101).</summary>
    /// <remarks>
    /// <para>
    /// <b>Joe: "all sites should wait for clearing before building can begin."</b> Materials
    /// may still be stacked on the footprint — that is a delivery, not building — but not one
    /// tick of work goes in while a tree is standing on it.
    /// </para>
    /// <para>
    /// <b>And the builder who is waiting goes and does the clearing</b>, which is D87's
    /// position rule paying for itself: a builder with nothing to build falls through to the
    /// bottom of <c>Decide</c>, where clearing painted ground is exactly the work that
    /// unblocks them. Nobody had to write a rule saying so.
    /// </para>
    /// </remarks>
    [Fact]
    public void NoWorkGoesIntoASiteWhoseGroundIsStillStanding()
    {
        SimConfig config = VillageFixtures.Village;
        SimLoop loop = ManagedVillage.Loop(config, new InMemoryLogSink());
        SimWorld world = loop.World;

        GridPos at = WoodedGroundNear(world)!.Value;
        Assert.True(world.Mark(BuildingKind.Shed, at).Allowed);

        Workplace site = Assert.Single(
            world.Workplaces, place => place.Construction?.Kind == BuildingKind.Shed);

        // Give it every log it wants, so materials can never be the reason it is not built.
        site.Construction!.Deliver(site.Construction.Recipe.Logs);
        Assert.True(site.Construction.HasMaterials);

        int woodedTicks = 0;
        while (!world.GroundIsClearAt(at) && woodedTicks < config.TicksPerYear)
        {
            loop.StepOnce();
            woodedTicks++;
            Assert.Equal(0, site.Construction.WorkDone);
        }

        _output.WriteLine(
            $"the ground under the site stood for {woodedTicks} ticks with the site fully "
            + $"stocked, and {site.Construction.WorkDone} ticks of work went in");

        // Anti-vacuity (D7): if the tile was cleared on tick one this proved nothing.
        Assert.True(woodedTicks > 1, "The ground was clear immediately, so nothing was tested.");

        // And once it IS clear, the village gets on with it.
        Assert.True(world.GroundIsClearAt(at), "The village never cleared the marked ground.");
        loop.Step(config.TicksPerYear);
        _output.WriteLine($"a year later: {site.Construction?.WorkDone.ToString() ?? "finished"}");
        Assert.True(
            site.Construction is null || site.Construction.WorkDone > 0,
            "The ground was cleared and still nothing was ever built.");
    }

    // ---------------------------------------------------------------
    //  What it costs, and what it gives back
    // ---------------------------------------------------------------

    /// <summary>The recipe is nothing at all — no logs, and no work either.</summary>
    /// <remarks>
    /// <c>pile_work_ticks</c> is gone rather than zeroed, on Joe's own reasoning: a number
    /// that is always zero is a lie waiting to be found.
    /// </remarks>
    [Fact]
    public void APileCostsNothingAndOwesNoWork()
    {
        BuildingRecipe recipe = BuildingRecipe.For(BuildingKind.Pile, VillageFixtures.Village);

        Assert.Equal(0, recipe.Logs);
        Assert.Equal(0, recipe.WorkTicks);
    }

    /// <summary>⚠️ And pulling one down pays back nothing, where it used to pay a market's half.</summary>
    /// <remarks>
    /// <b>A free-timber press, found while reading <c>Demolish</c> to make the pile instant.</b>
    /// The pile and the cart both fell into a <c>_ =&gt; Market</c> arm, so demolishing either
    /// returned half a market's logs — seventeen — out of a building nobody paid for.
    /// </remarks>
    [Fact]
    public void PullingDownWhatYouNeverPaidForReturnsNothing()
    {
        SimConfig config = VillageFixtures.Village;
        SimWorld world = World(config);

        world.Mark(BuildingKind.Pile, ClearGroundNear(world));
        StoreBuilding pile = Assert.Single(
            world.StoreBuildings, store => store.Kind == StoreKind.Pile);

        int logsBefore = world.LogsInSheds();
        world.Demolish(pile);

        _output.WriteLine(
            $"pulled the pile down: logs in stores {logsBefore} -> {world.LogsInSheds()} "
            + $"(a market would have returned "
            + $"{config.MarketLogs * config.DemolitionReturnsPercent / 100})");

        Assert.Equal(logsBefore, world.LogsInSheds());
        Assert.DoesNotContain(world.StoreBuildings, store => store.Kind == StoreKind.Pile);
    }

    /// <summary>And the same for the wagon the founders turned up in.</summary>
    [Fact]
    public void PullingDownTheCartReturnsNothingEither()
    {
        SimConfig config = ShippedConfig.Load();
        SimWorld world = World(config);

        StoreBuilding cart = Assert.Single(world.StoreBuildings);
        Assert.Equal(StoreKind.Cart, cart.Kind);

        // ⭐ A SHED HAS TO BE STANDING OR THIS GUARD IS VACUOUS (D7). `Demolish` hands its
        // refund to the nearest shed, and a cold start has none — so without one this would
        // pass on a village where the bug could not fire, which is the exact shape of
        // failure D78 and D89 both record.
        world.Mark(BuildingKind.Shed, ClearGroundNear(world));
        Workplace site = Assert.Single(
            world.Workplaces, place => place.Construction?.Kind == BuildingKind.Shed);
        world.Complete(site);

        StoreBuilding shed = world.AnyStoreOf(StoreKind.Shed);

        int before = shed.Store.Logs;
        world.Demolish(cart);

        _output.WriteLine($"pulled the cart down: shed logs {before} -> {shed.Store.Logs}");
        Assert.Equal(before, shed.Store.Logs);
    }
}

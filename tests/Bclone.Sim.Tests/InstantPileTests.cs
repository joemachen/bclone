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
        SimFactory.CreatePhase0(config, new InMemoryLogSink()).World;

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
                    if (world.CanBuildAt(BuildingKind.Pile, at).Allowed)
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

    /// <summary>⭐ A pile is refused on wooded ground, and the refusal says what to do.</summary>
    /// <remarks>
    /// <b>A refusal the player cannot act on is worse than none</b> (§1.1, D43's shape). This
    /// one names both halves — what is standing there, and the tool that removes it — which is
    /// the same rule D92 applied to a brush refusing half a drag.
    /// </remarks>
    [Fact]
    public void APileIsRefusedOnGroundThatStillHasSomethingOnIt()
    {
        SimWorld world = World(VillageFixtures.Village);

        GridPos? wooded = WoodedGroundNear(world);
        Assert.NotNull(wooded);

        PlacementVerdict verdict = world.CanBuildAt(BuildingKind.Pile, wooded.Value);
        _output.WriteLine($"marking a pile on {wooded.Value}: \"{verdict.Reason}\"");

        Assert.False(verdict.Allowed);
        Assert.Contains("clear", verdict.Reason, System.StringComparison.OrdinalIgnoreCase);
        Assert.Contains("brush", verdict.Reason, System.StringComparison.OrdinalIgnoreCase);

        // Refused means refused: nothing was built and nothing was marked.
        Assert.False(world.Mark(BuildingKind.Pile, wooded.Value).Allowed);
        Assert.DoesNotContain(world.StoreBuildings, store => store.Kind == StoreKind.Pile);
    }

    /// <summary>⭐ Clear the tile and the same click is allowed — the price was the clearing.</summary>
    /// <remarks>
    /// The two halves together are the mechanic: siting a store in a wood is not forbidden, it
    /// is <em>priced</em>, and the price is one the player pays with a tool they already have.
    /// </remarks>
    [Fact]
    public void ClearingTheGroundIsWhatBuysThePile()
    {
        SimWorld world = World(VillageFixtures.Village);

        GridPos? wooded = WoodedGroundNear(world);
        Assert.NotNull(wooded);
        GridPos at = wooded.Value;

        Assert.False(world.CanBuildAt(BuildingKind.Pile, at).Allowed);

        // The clearing itself — what a laborer's day of work does to the tile.
        (Goods goods, int amount) = world.Harvest(at);
        _output.WriteLine($"cleared {at} for {amount} {goods}; now: "
            + $"\"{world.CanBuildAt(BuildingKind.Pile, at).Reason}\"");

        Assert.True(amount > 0);
        Assert.True(world.CanBuildAt(BuildingKind.Pile, at).Allowed);
        Assert.True(world.Mark(BuildingKind.Pile, at).Allowed);
        Assert.Contains(
            world.StoreBuildings, store => store.Kind == StoreKind.Pile && store.Position == at);
    }

    /// <summary>Every other building is unaffected — only the pile takes this rule.</summary>
    /// <remarks>
    /// Deliberate scope. Whether a granary may be marked in a forest is a separate question
    /// and D96 does not open it; the pile is the building whose <em>entire</em> cost the
    /// clearing becomes.
    /// </remarks>
    [Fact]
    public void OnlyThePileNeedsClearGround()
    {
        SimWorld world = World(VillageFixtures.Village);

        GridPos? wooded = WoodedGroundNear(world);
        Assert.NotNull(wooded);

        Assert.False(world.CanBuildAt(BuildingKind.Pile, wooded.Value).Allowed);
        Assert.True(world.CanBuildAt(BuildingKind.Shed, wooded.Value).Allowed);
        Assert.True(world.CanBuildAt(BuildingKind.WoodcutterHut, wooded.Value).Allowed);
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

using Bclone.Sim.Config;
using Bclone.Sim.Core;
using Bclone.Sim.Logging;
using Bclone.Sim.World;
using Xunit;
using Xunit.Abstractions;

namespace Bclone.Sim.Tests;

/// <summary>
/// A village may have more than one of a store — <c>specs/building-placement.md §4</c>
/// (D38), slice 1.
/// </summary>
/// <remarks>
/// <para>
/// No player-facing change: the village still founds itself with one of each. What
/// these guard is that a <em>second</em> one would not be silently ignored, which is
/// what would have happened the day placement shipped. Thirteen call sites read "the
/// granary" and every one of them was right only while there was exactly one.
/// </para>
/// <para>
/// The worst of them was the birth gate. A second granary the gate could not see would
/// have been a building the player paid for that did nothing whatsoever — and the
/// symptom, a village that stops growing for no stated reason, is the least debuggable
/// kind there is.
/// </para>
/// </remarks>
public sealed class PluralStoresTests
{
    private readonly ITestOutputHelper _output;

    public PluralStoresTests(ITestOutputHelper output) => _output = output;

    private static SimConfig Config => VillageFixtures.Village;

    /// <summary>A village with an extra granary and shed dropped in beside the first.</summary>
    /// <remarks>
    /// Added directly rather than through a placement system, which does not exist yet.
    /// That is the point of testing the seam separately: it can be proven closed before
    /// there is any way for a player to open it.
    /// </remarks>
    private static SimWorld WithASecondOfEach(SimConfig config, ulong seed = 12345UL)
    {
        SimWorld world = SimFactory.CreatePhase0(config, new InMemoryLogSink(), seed).World;

        StoreBuilding granary = world.AnyStoreOf(StoreKind.Granary);
        StoreBuilding shed = world.AnyStoreOf(StoreKind.Shed);

        world.StoreBuildings.Add(new StoreBuilding
        {
            Id = 4,
            Kind = StoreKind.Granary,
            Name = "the second granary",
            Position = FreeSpotNear(world, granary.Position),
            Store = new Stockpile { Capacity = granary.Store.Capacity },
        });

        world.StoreBuildings.Add(new StoreBuilding
        {
            Id = 5,
            Kind = StoreKind.Shed,
            Name = "the second shed",
            Position = FreeSpotNear(world, shed.Position),
            Store = new Stockpile { Capacity = shed.Store.Capacity },
        });

        return world;
    }

    private static GridPos FreeSpotNear(SimWorld world, GridPos near)
    {
        for (int radius = 1; radius < 20; radius++)
        {
            for (int dy = -radius; dy <= radius; dy++)
            {
                for (int dx = -radius; dx <= radius; dx++)
                {
                    if (System.Math.Abs(dx) + System.Math.Abs(dy) != radius)
                    {
                        continue;
                    }

                    var candidate = new GridPos(near.X + dx, near.Y + dy);
                    if (world.Map.TerrainAt(candidate) == Terrain.Water || Taken(world, candidate))
                    {
                        continue;
                    }

                    return candidate;
                }
            }
        }

        throw new System.InvalidOperationException("Nowhere free to put a second store.");
    }

    private static bool Taken(SimWorld world, GridPos position)
    {
        foreach (StoreBuilding store in world.StoreBuildings)
        {
            if (store.Position == position) return true;
        }

        foreach (Workplace workplace in world.Workplaces)
        {
            if (workplace.Position == position) return true;
        }

        foreach (Household household in world.Households)
        {
            if (household.Home() == position) return true;
        }

        return false;
    }

    [Fact]
    public void FoodInASecondGranaryIsFoodTheVillageHas()
    {
        // The birth gate's question. This is the assertion the whole slice is for.
        SimWorld world = WithASecondOfEach(Config);

        StoreBuilding first = world.StoreBuildings[0];
        StoreBuilding second = world.AnyStoreOf(StoreKind.Granary) == first
            ? world.StoreBuildings[3]
            : first;

        int before = world.FoodInGranaries();
        second.Store.Add(Goods.Food, 100);

        _output.WriteLine($"{before} food before, {world.FoodInGranaries()} after adding 100 to a second granary.");
        Assert.Equal(before + 100, world.FoodInGranaries());
    }

    [Fact]
    public void LogsInASecondShedCanBuildAHouse()
    {
        // House timber is drawn village-wide (D25), so splitting the logs across two
        // sheds must not stop a house being raised. Before this slice it would have:
        // the draw came from one shed and the rest were invisible.
        SimConfig config = Config;
        SimWorld world = WithASecondOfEach(config);

        foreach (StoreBuilding store in world.StoreBuildings)
        {
            if (store.Kind == StoreKind.Shed)
            {
                store.Store.TryTake(Goods.Logs, store.Store.Logs);
            }
        }

        // Half a house's worth in each, so neither alone is enough.
        int half = (config.LogsPerHouse / 2) + 1;
        foreach (StoreBuilding store in world.StoreBuildings)
        {
            if (store.Kind == StoreKind.Shed)
            {
                store.Store.Add(Goods.Logs, half);
            }
        }

        _output.WriteLine(
            $"{world.LogsInSheds()} logs across two sheds, {half} in each, " +
            $"a house needs {config.LogsPerHouse}.");

        Assert.True(world.LogsInSheds() >= config.LogsPerHouse);
        Assert.True(half < config.LogsPerHouse, "Neither shed alone should be enough, or this proves nothing.");
    }

    [Fact]
    public void MoreGranaryMeansABiggerVillage()
    {
        // The D33 payoff, asserted as arithmetic before there is any way to build one.
        // If this ever stops being true, placing a granary has stopped meaning
        // anything and the player's most legible decision has quietly died.
        SimConfig config = Config;

        int one = VillageEconomy.CeilingForCapacity(config, VillageEconomy.GranaryCapacity(config));
        int two = VillageEconomy.CeilingForCapacity(config, VillageEconomy.GranaryCapacity(config) * 2);

        _output.WriteLine($"one granary supports {one} people; two support {two}.");

        Assert.True(two > one, "A second granary did not raise the ceiling at all.");

        // AT LEAST double, not exactly double. The ceiling is a floor division of
        // capacity, so doubling the capacity can leave a remainder behind that the
        // single-granary answer had thrown away — 37 and 75, not 37 and 74. Asserting
        // exact equality made this a test of whether the division happened to come out
        // even, which it did at fifteen-day seasons and does not at thirty.
        Assert.True(two >= one * 2,
            $"Two granaries support {two} people where one supports {one}. Doubling the room " +
            "should never buy less than twice the village.");
    }

    [Fact]
    public void ThePluralStoreHelpersAgreeWithTheOneStoreVillage()
    {
        // Anti-vacuity (D7) in the other direction: with exactly one of each — which is
        // every village that exists today — the plural helpers must give precisely the
        // answers the singular accessors used to. If they did not, this slice would
        // have changed behaviour while claiming not to.
        SimWorld world = SimFactory.CreatePhase0(Config, new InMemoryLogSink()).World;

        Assert.Equal(world.AnyStoreOf(StoreKind.Granary).Store.Food, world.FoodInGranaries());
        Assert.Equal(world.AnyStoreOf(StoreKind.Shed).Store.Logs, world.LogsInSheds());
        Assert.Equal(world.AnyStoreOf(StoreKind.Shed).Store.Firewood, world.FirewoodInSheds());
        Assert.Equal(world.AnyStoreOf(StoreKind.Granary).Store.Capacity, world.GranaryCapacity());
    }

    [Fact]
    public void AProducerWalksToTheNearerOfTwoStores()
    {
        // What "nearest, not the" buys: with two granaries a forager should use the one
        // they can actually get to. Asserted through the shared cost field, so it
        // respects water being impassable (D40) rather than measuring a line.
        SimWorld world = WithASecondOfEach(Config);

        GridPos from = world.Households[0].Home();
        StoreBuilding? nearest = world.NearestStore(
            from, StoreKind.Granary, static store => !store.Store.IsFull);

        Assert.NotNull(nearest);

        foreach (StoreBuilding store in world.StoreBuildings)
        {
            if (store.Kind != StoreKind.Granary || store.Store.IsFull)
            {
                continue;
            }

            int theirs = world.TravelCost.Cost(from, store.Position);
            int ours = world.TravelCost.Cost(from, nearest!.Position);

            if (theirs != TravelCostField.Unreachable)
            {
                Assert.True(ours <= theirs,
                    $"{nearest.Name} at {ours} was chosen over {store.Name} at {theirs}.");
            }
        }
    }

    [Fact]
    public void AnUnreachableStoreIsNeverChosen()
    {
        // A granary across the river is not a long walk, it is no walk at all (D40).
        // Choosing one would send a villager on an errand they can never finish.
        SimConfig config = Config;
        SimWorld world = SimFactory.CreatePhase0(config, new InMemoryLogSink()).World;

        // A granary somewhere nobody can reach: the far edge of the valley, which the
        // river cuts off on this seed.
        var marooned = new GridPos(config.MapMaxX - 1, config.MapMaxY - 1);
        world.StoreBuildings.Add(new StoreBuilding
        {
            Id = 9,
            Kind = StoreKind.Granary,
            Name = "the marooned granary",
            Position = marooned,
            Store = new Stockpile { Capacity = 1000 },
        });

        GridPos from = world.Households[0].Home();
        StoreBuilding? chosen = world.NearestStore(from, StoreKind.Granary, static _ => true);

        _output.WriteLine(
            $"reachable: {world.TravelCost.CanReach(from, marooned)}; chose {chosen?.Name ?? "(none)"}");

        Assert.NotNull(chosen);
        Assert.True(world.TravelCost.CanReach(from, chosen!.Position),
            $"{chosen.Name} was chosen but cannot be reached.");
    }
}

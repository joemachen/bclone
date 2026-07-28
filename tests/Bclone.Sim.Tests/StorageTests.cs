using Bclone.Sim.Config;
using Bclone.Sim.Core;
using Bclone.Sim.Determinism;
using Bclone.Sim.Logging;
using Bclone.Sim.World;
using Xunit;
using Xunit.Abstractions;

namespace Bclone.Sim.Tests;

/// <summary>
/// Goods live in buildings — <c>specs/storage-and-distribution.md</c> (D30, D32).
/// </summary>
/// <remarks>
/// Slice 1: the places exist and can hold things. Nothing has started using them yet,
/// so these are the guards that have to be true <em>before</em> goods start moving —
/// most importantly that the two buildings really are separate, since that separation
/// is the whole of D32 and a single backing store would pass every later test.
/// </remarks>
public sealed class StorageTests
{
    private readonly ITestOutputHelper _output;

    public StorageTests(ITestOutputHelper output) => _output = output;

    private static SimConfig Config => VillageFixtures.Village;

    private static SimLoop Build(SimConfig config) =>
        SimFactory.CreatePhase0(config, new InMemoryLogSink());

    [Fact]
    public void TheVillageIsFoundedWithAGranaryAndAShed()
    {
        SimWorld world = Build(Config).World;

        foreach (StoreBuilding building in world.StoreBuildings)
        {
            _output.WriteLine($"{building.Id} {building.Name} at {building.Position}");
        }

        Assert.Equal(StoreKind.Granary, world.AnyStoreOf(StoreKind.Granary).Kind);
        Assert.Equal(StoreKind.Shed, world.AnyStoreOf(StoreKind.Shed).Kind);
        Assert.NotEqual(world.AnyStoreOf(StoreKind.Granary).Position, world.AnyStoreOf(StoreKind.Shed).Position);
    }

    [Fact]
    public void TheGranaryTakesFoodAndTheShedTakesMaterials()
    {
        // The whole of D32 in one assertion. One undifferentiated pile would delete
        // the per-household inequality D14 exists to create, so the two buildings
        // have to genuinely disagree about what they will hold.
        SimWorld world = Build(Config).World;

        Assert.True(world.AnyStoreOf(StoreKind.Granary).Accepts(Goods.Food));
        Assert.False(world.AnyStoreOf(StoreKind.Granary).Accepts(Goods.Logs));
        Assert.False(world.AnyStoreOf(StoreKind.Granary).Accepts(Goods.Firewood));

        Assert.False(world.AnyStoreOf(StoreKind.Shed).Accepts(Goods.Food));
        Assert.True(world.AnyStoreOf(StoreKind.Shed).Accepts(Goods.Logs));
        Assert.True(world.AnyStoreOf(StoreKind.Shed).Accepts(Goods.Firewood));
    }

    [Fact]
    public void EveryWorkplaceHasABufferOfItsOwn()
    {
        // A few logs beside the stumps, a little firewood at the hut — the point of
        // production keeps a buffer, and the bulk goes to a store (D30).
        SimWorld world = Build(Config).World;

        foreach (Workplace workplace in world.Workplaces)
        {
            Assert.NotNull(workplace.Store);
            Assert.Equal(0, workplace.Store.Food);
            Assert.Equal(0, workplace.Store.Logs);
            Assert.Equal(0, workplace.Store.Firewood);
        }
    }

    [Fact]
    public void TheHashCoversEveryStore()
    {
        // Anti-vacuity, per D7, and this is the moment it matters most: stores have
        // just multiplied from "one per household" to "one per household, workplace
        // and building", and a store left out of the hash is a store that can desync
        // in silence for the rest of the project.
        SimLoop loop = Build(Config);
        loop.Step(Config.TicksPerYear);

        ulong before = StateHash.Compute(loop.World);
        loop.World.AnyStoreOf(StoreKind.Granary).Store.Add(1);
        ulong afterGranary = StateHash.Compute(loop.World);
        Assert.NotEqual(before, afterGranary);

        loop.World.AnyStoreOf(StoreKind.Shed).Store.AddLogs(1);
        ulong afterShed = StateHash.Compute(loop.World);
        Assert.NotEqual(afterGranary, afterShed);

        loop.World.Workplaces[0].Store.AddFirewood(1);
        Assert.NotEqual(afterShed, StateHash.Compute(loop.World));
    }

    [Fact]
    public void TheStoresStayInsideTheValley()
    {
        // Same guard the workplaces and homes carry. A building outside the valley is
        // invisible on the map and villagers would walk off the edge of the world to
        // reach it.
        SimConfig config = Config;
        SimWorld world = Build(config).World;

        foreach (StoreBuilding building in world.StoreBuildings)
        {
            Assert.True(
                building.Position.X >= config.MapMinX && building.Position.X <= config.MapMaxX
                && building.Position.Y >= config.MapMinY && building.Position.Y <= config.MapMaxY,
                $"{building.Name} at {building.Position} is outside the valley.");
        }
    }

    // ---------------------------------------------------------------
    //  Slice 5 — capacity as a binding constraint
    // ---------------------------------------------------------------

    [Fact]
    public void NoStoreEverExceedsItsOwnCapacity()
    {
        // The flat assertion from the spec's §8. Checked every tick rather than at the
        // end, because a store that overflows and is drained again would look innocent
        // in a final snapshot.
        SimConfig config = Config;
        SimLoop loop = Build(config);

        for (int i = 0; i < config.TicksPerYear * 60; i++)
        {
            loop.StepOnce();

            foreach (StoreBuilding building in loop.World.StoreBuildings)
            {
                Assert.True(building.Store.Held <= building.Store.Capacity,
                    $"{building.Name} holds {building.Store.Held} of {building.Store.Capacity} " +
                    $"at tick {loop.World.Tick}.");
            }
        }
    }

    [Fact]
    public void AFullStoreRefusesGoodsRatherThanSwallowingThem()
    {
        // Conservation, at the one place capacity could break it. A store that took
        // goods it had no room for would destroy them silently, and the total only
        // ever falls — which is the direction nobody notices.
        var store = new Stockpile { Capacity = 10 };

        Assert.Equal(6, store.Add(6));
        Assert.Equal(4, store.AddLogs(9));      // only four would fit
        Assert.Equal(0, store.AddFirewood(1));  // and now nothing does

        Assert.True(store.IsFull);
        Assert.Equal(10, store.Held);
        Assert.Equal(6, store.Food);
        Assert.Equal(4, store.Logs);
        Assert.Equal(0, store.Firewood);
    }

    [Fact]
    public void CapacityCountsEveryKindOfGoodsTogether()
    {
        // Total, not per good — a shed packed with logs has nowhere to stack firewood.
        // A per-good cap would be three shelves that never compete, which is
        // bookkeeping wearing a constraint's clothes.
        var store = new Stockpile { Capacity = 10 };
        store.AddLogs(10);

        Assert.Equal(0, store.AddFirewood(5));
        Assert.Equal(0, store.Add(5));
    }

    [Fact]
    public void TheGranaryIsWhatDecidesHowBigTheVillageGets()
    {
        // The number slice 5 exists to create, and it must be a CONSEQUENCE rather
        // than a setting (D16). Deriving it from the granary is what makes "how big
        // can my village get" answerable by pointing at a building.
        SimConfig config = Config;

        int ceiling = VillageEconomy.PopulationCeiling(config);
        _output.WriteLine(
            $"granary feeds {config.GranaryFeedsPeople}, holds " +
            $"{VillageEconomy.GranaryCapacity(config)}, ceiling {ceiling} people.");

        // A bigger granary must mean a bigger village, or capacity is decoration.
        SimConfig larger = config with { GranaryFeedsPeople = config.GranaryFeedsPeople * 2 };
        Assert.True(VillageEconomy.PopulationCeiling(larger) > ceiling);

        // And the ceiling sits ABOVE what the granary comfortably feeds, by exactly
        // the slack in the birth gate — a village keeps having children until its
        // store is short of what everyone alive would want.
        Assert.True(ceiling >= config.GranaryFeedsPeople,
            $"Ceiling {ceiling} is below the {config.GranaryFeedsPeople} the granary feeds.");
    }

    [Fact]
    public void CapacityIsWhatHoldsThePopulationFlat()
    {
        // The claim slice 5 was taken ahead of the market to test, asserted rather
        // than left in a spec: a bounded granary makes growth stop at what the
        // buildings support, instead of overshooting them and falling back.
        //
        // Measured over 200 years — the unbounded village swings between 24 and 86,
        // the shipped one between 24 and 35.
        SimConfig bounded = Config;
        SimConfig unbounded = bounded with { GranaryFeedsPeople = 100_000 };

        (int Low, int High) boundedBand = Band(bounded);
        (int Low, int High) unboundedBand = Band(unbounded);

        _output.WriteLine(
            $"bounded {boundedBand.Low}-{boundedBand.High}, unbounded {unboundedBand.Low}-{unboundedBand.High}");

        Assert.True(
            boundedBand.High - boundedBand.Low < unboundedBand.High - unboundedBand.Low,
            $"A bounded granary swung {boundedBand.High - boundedBand.Low} against the unbounded " +
            $"village's {unboundedBand.High - unboundedBand.Low}. Capacity is not regulating anything.");
    }

    /// <summary>Population band after the founding decades, over 200 years.</summary>
    private static (int Low, int High) Band(SimConfig config)
    {
        SimLoop loop = Build(config);
        int low = int.MaxValue, high = 0;

        for (int year = 1; year <= 200; year++)
        {
            loop.Step(config.TicksPerYear);
            if (year < 40)
            {
                continue;
            }

            low = Math.Min(low, loop.World.Population);
            high = Math.Max(high, loop.World.Population);
        }

        return (low, high);
    }

    [Fact]
    public void StoresDoNotSitOnTopOfSomethingElse()
    {
        // A granary drawn underneath the tree stand is a granary nobody can see, and
        // "why is nobody fetching food?" becomes unanswerable by looking.
        SimWorld world = Build(Config).World;

        foreach (StoreBuilding building in world.StoreBuildings)
        {
            foreach (Workplace workplace in world.Workplaces)
            {
                // The market is deliberately both — a store and a place to work at it
                // (D14). Those are two types today only because merging them into the
                // spec's single Building is a bigger change than the market slice; the
                // exemption is the seam, and it is named in spec §14.5 rather than
                // left to be rediscovered here.
                if (building.Kind == StoreKind.Market && workplace.Kind == JobKind.Marketer)
                {
                    continue;
                }

                Assert.NotEqual(workplace.Position, building.Position);
            }

            foreach (Household household in world.Households)
            {
                Assert.NotEqual(household.HomePosition, building.Position);
            }
        }
    }
}

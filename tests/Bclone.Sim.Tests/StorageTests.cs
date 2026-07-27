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

        Assert.Equal(StoreKind.Granary, world.Granary.Kind);
        Assert.Equal(StoreKind.Shed, world.StorageShed.Kind);
        Assert.NotEqual(world.Granary.Position, world.StorageShed.Position);
    }

    [Fact]
    public void TheGranaryTakesFoodAndTheShedTakesMaterials()
    {
        // The whole of D32 in one assertion. One undifferentiated pile would delete
        // the per-household inequality D14 exists to create, so the two buildings
        // have to genuinely disagree about what they will hold.
        SimWorld world = Build(Config).World;

        Assert.True(world.Granary.Accepts(Goods.Food));
        Assert.False(world.Granary.Accepts(Goods.Logs));
        Assert.False(world.Granary.Accepts(Goods.Firewood));

        Assert.False(world.StorageShed.Accepts(Goods.Food));
        Assert.True(world.StorageShed.Accepts(Goods.Logs));
        Assert.True(world.StorageShed.Accepts(Goods.Firewood));
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
        loop.World.Granary.Store.Add(1);
        ulong afterGranary = StateHash.Compute(loop.World);
        Assert.NotEqual(before, afterGranary);

        loop.World.StorageShed.Store.AddLogs(1);
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
                Assert.NotEqual(workplace.Position, building.Position);
            }

            foreach (Household household in world.Households)
            {
                Assert.NotEqual(household.HomePosition, building.Position);
            }
        }
    }
}

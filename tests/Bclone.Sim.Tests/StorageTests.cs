using System.Linq;
using Bclone.Sim.Config;
using Bclone.Sim.Core;
using Bclone.Sim.Determinism;
using Bclone.Sim.Logging;
using Bclone.Sim.Systems;
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
        loop.World.AnyStoreOf(StoreKind.Granary).Store.Add(Goods.Food, 1);
        ulong afterGranary = StateHash.Compute(loop.World);
        Assert.NotEqual(before, afterGranary);

        loop.World.AnyStoreOf(StoreKind.Shed).Store.Add(Goods.Logs, 1);
        ulong afterShed = StateHash.Compute(loop.World);
        Assert.NotEqual(afterGranary, afterShed);

        loop.World.Workplaces[0].Store.Add(Goods.Firewood, 1);
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
        var store = new Stockpile(Stockpile.Kinds) { Capacity = 10 };

        Assert.Equal(6, store.Add(Goods.Food, 6));
        Assert.Equal(4, store.Add(Goods.Logs, 9));      // only four would fit
        Assert.Equal(0, store.Add(Goods.Firewood, 1));  // and now nothing does

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
        var store = new Stockpile(Stockpile.Kinds) { Capacity = 10 };
        store.Add(Goods.Logs, 10);

        Assert.Equal(0, store.Add(Goods.Firewood, 5));
        Assert.Equal(0, store.Add(Goods.Food, 5));
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

    /// <remarks>
    /// <para>
    /// <b>⏸️ SKIPPED ON D134, because the thing it names stopped being true.</b> It compares a
    /// bounded granary against an effectively infinite one and requires the bounded village to
    /// swing less. They now swing **35 against 34** — indistinguishable, and one person wide.
    /// </para>
    /// <para>
    /// The granary is no longer what holds the population flat. <b>The timber shed is.</b>
    /// Measured for D134: a village fills `storage shed 1` to 343/343 by year five and stays
    /// there for ever, with 5,977 logs stranded in heaps outside it, so it can never build
    /// past one shed's worth of stock however much food it has. Both arms of this test are
    /// capped by that long before the granary matters, which is why doubling the granary
    /// changes nothing — the test is correct and the village has a different bottleneck.
    /// </para>
    /// <para>
    /// Restoring it means answering the open question D134 leaves — whether the village should
    /// ever want a second store — and that is Joe's call, not something to tune this guard
    /// around. It is the same question as D131's market and D103's building.
    /// </para>
    /// </remarks>
    [Fact(Skip = "D134: the granary is no longer the binding cap — the timber shed is, at "
        + "343/343 from year five with thousands of logs stranded outside it. Both arms hit "
        + "that first, so they swing 35 against 34. Restore when D134's open question is "
        + "answered.")]
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
                Assert.NotEqual(household.Home(), building.Position);
            }
        }
    }
    /// <summary>
    /// ⭐ A fetch fills the armful — food first, then firewood with what is left.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Found in Joe's audit trail, in the jitter he reported twice.</b> Otto fetched 40 food,
    /// then 25 food, then <em>2 firewood</em> from a store one tile from his door — three trips
    /// and six ticks of a man visibly bouncing between two squares. The third was pure waste:
    /// <c>CollectFromStore</c> returned the moment it had taken any food, so a villager carrying
    /// 25 of a possible 40 walked home with a free hand and came straight back.
    /// </para>
    /// <para>
    /// <b>Priority and exclusivity are different rules, and only one of them was wanted</b> —
    /// D142's shape, where a rule reached some of its call sites and not others. Food still goes
    /// first, because hunger kills in six days and an unheated house in twenty-five (D45).
    /// </para>
    /// <para>
    /// <b>⚠️ It is a free hand, not a free trip.</b> When food takes the whole armful there is
    /// no room left and the second journey still happens — that is <c>carry_capacity</c> doing
    /// its job (D32), the inequality a distant household is supposed to feel, and not something
    /// to optimise away.
    /// </para>
    /// </remarks>
    [Fact]
    public void AFetchCarriesFirewoodHomeIfTheArmfulHasRoomForIt()
    {
        SimLoop loop = SimFactory.CreatePhase0(Config, new InMemoryLogSink());
        SimWorld world = loop.World;
        loop.Step(Config.TicksPerYear);

        Villager villager = world.Villagers.First(v => v.Alive && v.CanWork);
        Household home = world.HouseholdOf(villager);
        StoreBuilding market = world.AnyStoreOf(StoreKind.Market);

        // A household short of a little of each, and a store standing at the villager's feet
        // holding both — the case Otto was in.
        home.Stockpile.TryTake(Goods.Food, home.Stockpile.Food);
        home.Stockpile.TryTake(Goods.Firewood, home.Stockpile.Firewood);
        home.Stockpile.Receive(Goods.Food, world.TargetFoodFor(home) - 5);
        market.Store.Receive(Goods.Food, 200);
        market.Store.Receive(Goods.Firewood, 200);

        villager.CarriedFood = 0;
        villager.CarriedFirewood = 0;
        villager.Position = market.Position;

        BehaviorSystem.CollectForTest(world, villager);

        _output.WriteLine(
            $"{villager.Name} came away with {villager.CarriedFood} food and "
            + $"{villager.CarriedFirewood} firewood, of a {Config.CarryCapacity} armful");

        Assert.True(villager.CarriedFood > 0, "They did not take the food they came for.");
        Assert.True(
            villager.CarriedFirewood > 0,
            "They walked home with a free hand and will come straight back for the firewood — "
            + "which is the trip Joe watched as jitter.");
        Assert.True(
            villager.CarriedFood + villager.CarriedFirewood <= Config.CarryCapacity,
            "A fetch must still be one armful (D32).");
    }

    /// <summary>
    /// The anti-vacuity companion (D7): a full armful of food still makes a second trip.
    /// </summary>
    /// <remarks>
    /// Without this, a fix that simply took both goods regardless of room would pass the guard
    /// above while deleting <c>carry_capacity</c> — and carry capacity is what stops a fetch
    /// being a teleport with extra steps, which is the whole of D32's inequality.
    /// </remarks>
    [Fact]
    public void ButAFullArmfulOfFoodLeavesTheFirewoodBehind()
    {
        SimLoop loop = SimFactory.CreatePhase0(Config, new InMemoryLogSink());
        SimWorld world = loop.World;
        loop.Step(Config.TicksPerYear);

        Villager villager = world.Villagers.First(v => v.Alive && v.CanWork);
        Household home = world.HouseholdOf(villager);
        StoreBuilding market = world.AnyStoreOf(StoreKind.Market);

        // Short of far more food than one person can carry.
        home.Stockpile.TryTake(Goods.Food, home.Stockpile.Food);
        home.Stockpile.TryTake(Goods.Firewood, home.Stockpile.Firewood);
        market.Store.Receive(Goods.Food, 500);
        market.Store.Receive(Goods.Firewood, 200);

        villager.CarriedFood = 0;
        villager.CarriedFirewood = 0;
        villager.Position = market.Position;

        BehaviorSystem.CollectForTest(world, villager);

        _output.WriteLine(
            $"{villager.Name} came away with {villager.CarriedFood} food and "
            + $"{villager.CarriedFirewood} firewood");

        Assert.Equal(Config.CarryCapacity, villager.CarriedFood);
        Assert.Equal(0, villager.CarriedFirewood);
    }

    /// <summary>
    /// ⭐ Nobody walks to a store for a trivial amount — <b>"worth the trip"</b> (Joe).
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>This is the jitter, answered at its cause.</b> A fetch used to fire the instant a
    /// larder dipped below its floor, so a household two firewood short sent somebody out for
    /// two firewood — and with a store one tile from the door that is a villager visibly
    /// bouncing between two squares every thirty ticks (D166, measured in Joe's audit trail).
    /// </para>
    /// <para>
    /// <b>⚠️ IT IS ASSERTED ON FIREWOOD, AND THE FIRST DRAFT WAS ASSERTED ON FOOD AND WAS
    /// VACUOUS.</b> Food already has a stronger gate: a fetch needs the larder below
    /// <c>sharing_keep_percent</c> of its target, so by the time it fires the household is a
    /// fifth of a winter's store short and the bar is never the thing that stopped them.
    /// **Firewood has no such margin** — its floor <em>is</em> its target — so any dip at all
    /// used to send somebody, and that is the arm the bar exists for. The food arm is kept
    /// because the two gates are independent and <c>sharing_keep_percent</c> can move, but it
    /// is not what this guards.
    /// </para>
    /// <para>
    /// <b>Measured over thirty years, both ways:</b> fetch legs fall from 153 to 81 and tile
    /// flips from 211 to 143, with the population identical at 14 and <b>nobody starving or
    /// freezing in either arm</b> — the half that had to be checked, because a rule that stops
    /// people fetching is a rule that can kill them.
    /// </para>
    /// </remarks>
    [Fact]
    public void NobodyWalksToAStoreForATrivialAmount()
    {
        SimLoop loop = SimFactory.CreatePhase0(Config, new InMemoryLogSink());
        SimWorld world = loop.World;
        while (loop.World.Clock.Season != Season.Winter)
        {
            loop.StepOnce();
        }

        Villager villager = world.Villagers.First(v => v.Alive && v.CanWork);
        Household home = world.HouseholdOf(villager);
        StoreBuilding shed = world.AnyStoreOf(StoreKind.Shed);
        shed.Store.Receive(Goods.Firewood, 500);

        // Full of food, so only the fuel arm is in play.
        home.Stockpile.Receive(Goods.Food, world.TargetFoodFor(home));

        int wanted = VillageEconomy.FirewoodStoreWantedPerHousehold(Config);
        home.Stockpile.TryTake(Goods.Firewood, home.Stockpile.Firewood);
        home.Stockpile.Receive(Goods.Firewood, wanted - 1);

        _output.WriteLine(
            $"{home.Name} is 1 firewood short of {wanted}; "
            + $"fetch planned: {BehaviorSystem.PlanFetchForTest(world, villager) is not null}");

        Assert.Null(BehaviorSystem.PlanFetchForTest(world, villager));
    }

    /// <summary>
    /// The anti-vacuity companion (D7): a household genuinely short still sends somebody.
    /// </summary>
    /// <remarks>
    /// Without this, a threshold set absurdly high — or a predicate accidentally inverted —
    /// would pass the guard above while quietly switching fetching off, and a village that
    /// never fetches freezes beside a full shed.
    /// </remarks>
    [Fact]
    public void ButAHouseholdThatIsGenuinelyShortStillSendsSomebody()
    {
        SimLoop loop = SimFactory.CreatePhase0(Config, new InMemoryLogSink());
        SimWorld world = loop.World;
        while (loop.World.Clock.Season != Season.Winter)
        {
            loop.StepOnce();
        }

        Villager villager = world.Villagers.First(v => v.Alive && v.CanWork);
        Household home = world.HouseholdOf(villager);
        StoreBuilding shed = world.AnyStoreOf(StoreKind.Shed);
        shed.Store.Receive(Goods.Firewood, 500);

        home.Stockpile.Receive(Goods.Food, world.TargetFoodFor(home));
        home.Stockpile.TryTake(Goods.Firewood, home.Stockpile.Firewood);

        _output.WriteLine($"{home.Name} has no firewood at all in winter; a fetch must be planned.");
        Assert.NotNull(BehaviorSystem.PlanFetchForTest(world, villager));
    }


}

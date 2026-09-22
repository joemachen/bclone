using Bclone.Sim.Config;
using Bclone.Sim.Core;
using Bclone.Sim.Logging;
using Bclone.Sim.World;
using Xunit;
using Xunit.Abstractions;

namespace Bclone.Sim.Tests;

/// <summary>
/// ⭐⭐ A larder has walls — the house's row says how big (D399, Joe: <i>"a larder should have a
/// size limit"</i>).
/// </summary>
/// <remarks>
/// It was the one store in the game with none: <c>Stockpile.Capacity</c> defaults to
/// <c>int.MaxValue</c>, so a household's shelf was as deep as the village could fill it. The
/// number is the home's <c>local_store_cap</c> — the column the farmhouse, the fishery and the
/// lodge already carry — so the house tiers (D206) raise it by changing a row.
/// </remarks>
public sealed class LarderCapacityTests
{
    private readonly ITestOutputHelper _output;

    public LarderCapacityTests(ITestOutputHelper output) => _output = output;

    private static SimConfig Config => VillageFixtures.Village;

    /// <summary>⭐ The house's row is where the number comes from, and the food target is clamped to what is left after the fire.</summary>
    [Fact]
    public void TheHouseRowSaysHowMuchALarderHolds()
    {
        SimConfig config = Config with { HomeStoreCap = 300 };
        SimLoop loop = SimFactory.CreatePhase0(config, new InMemoryLogSink());
        SimWorld world = loop.World;

        Assert.Equal(300, world.LarderCapacity);
        foreach (Household household in world.Households)
        {
            Assert.Equal(300, household.Stockpile.Capacity);
        }

        // ⭐ THE HEARTH'S SHARE IS RESERVED. A household bigger than its house wants what is left
        // after the winter's firewood, not what `stockpile_target × members` asks for — or the
        // top-up loop would chase a number the larder can never hold.
        int firewood = VillageEconomy.FirewoodStoreWantedPerHousehold(config);
        Household big = world.Households[0];
        int members = world.LivingMembersOf(big);
        _output.WriteLine(
            $"a house of {300} holding a household of {members}: target {world.TargetFoodFor(big)}, "
            + $"unclamped {config.StockpileTarget * members}, the fire's share {firewood}");

        Assert.True(world.TargetFoodFor(big) <= 300 - firewood, "the food target does not leave room for the winter's firewood");
    }

    /// <summary>⛔⛔ A full larder takes what fits and the rest goes back to a store — <b>no food vanishes</b>.</summary>
    /// <remarks>
    /// <c>UnloadAtHome</c> put the whole armful in and emptied the arms regardless, which was
    /// harmless while a larder had no walls and <b>destroys food</b> the moment it has any. Red
    /// with the return values ignored again (and <c>FoodConservationTests</c> reddens with it).
    /// </remarks>
    [Fact]
    public void ALarderTakesWhatFitsAndTheRestStaysInTheirArms()
    {
        SimConfig config = Config with { HomeStoreCap = 200 };
        SimLoop loop = SimFactory.CreatePhase0(config, new InMemoryLogSink());
        SimWorld world = loop.World;
        loop.Step(config.TicksPerDay);

        Household home = world.Households[0];
        Villager carrier = world.Villagers.First(v => v.Alive && world.HouseholdOf(v).Id == home.Id);

        home.Stockpile.TakeAll(Goods.Produce);
        home.Stockpile.TakeAll(Goods.Firewood);
        home.Stockpile.Add(Goods.Produce, 190);

        carrier.Carried.TakeAll(Goods.Produce);
        carrier.Carried.Receive(Goods.Produce, config.CarryCapacity);

        int before = world.FoodHeldAnywhere();
        Bclone.Sim.Systems.BehaviorSystem.UnloadAtHomeForTest(world, carrier);
        int after = world.FoodHeldAnywhere();

        _output.WriteLine(
            $"a larder of 200 holding 190, handed {config.CarryCapacity}: it took "
            + $"{world.FoodIn(home.Stockpile) - 190}, {carrier.Carried[Goods.Produce]} stayed in "
            + $"{carrier.Name}'s arms; the valley held {before} before and {after} after");

        Assert.Equal(200, world.FoodIn(home.Stockpile));
        Assert.Equal(config.CarryCapacity - 10, carrier.Carried[Goods.Produce]);
        Assert.Equal(before, after);
    }

    /// <summary>⛔ A house that cannot hold the winter's firewood and an armful of food is refused by name.</summary>
    /// <remarks>
    /// The fire's share is reserved first, so a cap under <c>firewood + carry_capacity</c> leaves
    /// a household unable to keep a single trip's food — and it would show up as a village quietly
    /// starving rather than as a bad number. ⚠️ The floor is about the HOUSE and not about
    /// <c>stockpile_target</c>, which the yield rigs pose at 100,000 to hold demand open (D286).
    /// </remarks>
    [Fact]
    public void AHouseTooSmallToLiveInIsRefusedByName()
    {
        SimConfig config = Config;
        int floor = VillageEconomy.FirewoodStoreWantedPerHousehold(config) + config.CarryCapacity;

        SimConfigException blew = Assert.Throws<SimConfigException>(
            () => (config with { HomeStoreCap = floor - 1 }).Validate());

        _output.WriteLine(blew.Message);
        Assert.Contains("home_store_cap", blew.Message, System.StringComparison.Ordinal);

        // And the floor itself is allowed — the refusal is a floor, not a preference.
        (config with { HomeStoreCap = floor }).Validate();
    }
}

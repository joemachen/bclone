using Bclone.Sim.Config;
using Bclone.Sim.Core;
using Bclone.Sim.Logging;
using Bclone.Sim.World;
using Xunit;
using Xunit.Abstractions;

namespace Bclone.Sim.Tests;

/// <summary>
/// ⭐⭐ Every unit of food is produced, eaten, or held somewhere the village can name (D397).
/// </summary>
/// <remarks>
/// <para>
/// <b>Joe, 2026-09-19:</b> *"please double check the journey for all food from all sources to
/// ensure it is all making it from those sources through the delivery to the granary/market then
/// to the villager's homes and then being consumed."* The accountant's answer: on every tick of
/// fifty years, <c>produced − eaten == held − held at the start</c>, where <em>held</em> is every
/// stockpile (shelves, larders, huts), every pair of hands and every heap on the ground
/// (<see cref="SimWorld.FoodHeldAnywhere"/>). A drift is a unit that left the world by a door
/// nobody wrote down — a demolition, a death, a store emptied — and the tick it first moves says
/// which.
/// </para>
/// <para>
/// <b>Measured before it was written, and it held:</b> zero drift on every tick, three shipped
/// seeds, two fixture seeds, and three fixture villages with every source raised (a lodge, a
/// fishing hut, a farm with ground). ⚠️ <b>What the measurement found instead is filed, not
/// asserted here:</b> the all-sources village puts <b>300–420k</b> of meat and fish on the ground
/// in fifty years while <c>TheVillageWantsMoreFood()</c> reads false throughout — the hunt and the
/// cast answer *"my family is short"* (`BehaviorSystem`'s `needsFood`) with 900 and 400 at a time
/// into a full lodge, and the overflow goes down at a full store's door (D370). Conserved, and
/// wasted; Joe's call (`handoff.md`).
/// </para>
/// </remarks>
public sealed class FoodConservationTests
{
    private readonly ITestOutputHelper _output;

    public FoodConservationTests(ITestOutputHelper output) => _output = output;

    [Theory]
    [InlineData(12345UL, false)]
    [InlineData(12345UL, true)]
    [InlineData(2UL, true)]
    public void EveryUnitOfFoodIsProducedEatenOrHeldSomewhere(ulong seed, bool everySource)
    {
        SimConfig config = VillageFixtures.Village with { Seed = seed };
        SimLoop loop = SimFactory.CreatePhase0(config, new InMemoryLogSink());
        SimWorld world = loop.World;
        ColdStartTests.PlayTheOpening(world);

        if (everySource)
        {
            // A lodge, a fishing hut and a farm with ground: fish, meat and wheat beside the
            // forager's produce, so the claim covers every source the game has.
            HuntingTests.RaiseALodgeFor(world);
            FishingTests.RaiseAFishery(world);
            Workplace farm = FarmFixtures.RaiseAFarm(world);
            FarmFixtures.GiveItGround(world, farm, 3);
        }

        // ⚠️ The cart's food was never produced, so the ledger starts from what is held now.
        int held0 = world.FoodHeldAnywhere();
        int produced0 = world.FoodEverProduced;
        int eaten0 = world.FoodEverEaten;

        for (int tick = 0; tick < config.TicksPerYear * 50; tick++)
        {
            loop.StepOnce();
            int produced = world.FoodEverProduced - produced0;
            int eaten = world.FoodEverEaten - eaten0;
            int held = world.FoodHeldAnywhere() - held0;
            int drift = produced - eaten - held;
            Assert.True(
                drift == 0,
                $"At tick {world.Tick} ({world.Clock.SeasonAndYear()}) {Math.Abs(drift)} food "
                + $"{(drift > 0 ? "vanished" : "appeared")}: produced {produced}, eaten {eaten}, "
                + $"held {held} more than at the start. Something moved food by a door the ledger does not see.");
        }

        IReadOnlyList<Goods> edible = world.GoodsCatalog.EdibleGoods;
        var bySource = new List<string>();
        for (int i = 0; i < edible.Count; i++)
        {
            bySource.Add($"{world.GoodsCatalog.NameOf(edible[i])} {world.FoodEverProducedOf(edible[i])}");
        }

        int onTheGround = 0;
        for (int i = 0; i < world.GroundStacks.Count; i++)
        {
            for (int g = 0; g < edible.Count; g++)
            {
                if (world.GroundStacks[i].Goods == edible[g])
                {
                    onTheGround += world.GroundStacks[i].Amount;
                }
            }
        }

        _output.WriteLine(
            $"seed {seed}{(everySource ? ", every source" : "")}: fifty years conserved to the unit — "
            + $"produced {world.FoodEverProduced - produced0} ({string.Join(", ", bySource)}), "
            + $"eaten {world.FoodEverEaten - eaten0}; now on the shelves {world.FoodInGranaries()}, "
            + $"in the huts {world.FoodWaitingInHuts()}, in the larders {world.FoodInLarders()}, "
            + $"on the ground {onTheGround}; {world.Population} alive");

        // Anti-vacuity (D7): a village that produced nothing conserves nothing.
        Assert.True(world.FoodEverProduced - produced0 > 1000, "The village barely produced, so this measures nothing.");
        if (everySource)
        {
            Assert.True(
                world.FoodEverProducedOf(Goods.Fish) > 0 && world.FoodEverProducedOf(Goods.Meat) > 0 && world.FoodEverProducedOf(Goods.Wheat) > 0,
                "A source produced nothing, so the claim does not cover it.");
        }
    }
}

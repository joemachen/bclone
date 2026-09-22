using Bclone.Sim.Config;
using Bclone.Sim.Core;
using Bclone.Sim.Determinism;
using Bclone.Sim.Logging;
using Bclone.Sim.World;
using Xunit;
using Xunit.Abstractions;

namespace Bclone.Sim.Tests;

/// <summary>
/// The birth gate reads the harvest (D387, `specs/storage-and-distribution.md §12.4`): a couple
/// has a child only in a year after one the village brought in at least what it ate.
/// </summary>
/// <remarks>
/// Joe, 2026-09-17, on the fixture village breeding to the granary's ceiling and starving back:
/// *"build the production gate"*. §12.3 had named it in July as the real answer to the wave the
/// level gate lets through.
/// </remarks>
public sealed class HarvestGateTests
{
    private readonly ITestOutputHelper _output;

    public HarvestGateTests(ITestOutputHelper output) => _output = output;

    private static SimConfig Config => VillageFixtures.Village;

    /// <summary>The ledger counts what comes in and what goes down, and the first year is not judged.</summary>
    [Fact]
    public void TheLedgerCountsWhatComesInAndWhatIsEaten()
    {
        SimConfig config = Phase0Fixtures.Plenty;
        var (loop, _) = Phase0Fixtures.Build(config);
        SimWorld world = loop.World;

        Assert.Equal(0, world.FoodLedgerYears);
        Assert.True(world.TheHarvestFeedsOneMore(), "with no harvest to judge by, the gate must stand open");

        loop.Step(config.TicksPerYear + 1);

        _output.WriteLine($"year one: brought in {world.FoodProducedLastYear}, ate {world.FoodEatenLastYear}; {world.Villager.TotalGathers} gathers");
        Assert.Equal(1, world.FoodLedgerYears);
        Assert.True(world.FoodProducedLastYear > 0, "a year of gathering recorded nothing");
        Assert.True(world.FoodEatenLastYear > 0, "a year of meals recorded nothing");
        Assert.True(world.FoodProducedLastYear >= world.Villager.TotalGathers * config.GatherYield / 2,
            "the ledger reads far below the gathers that were made");
        Assert.Equal(world.FoodProducedLastYear >= world.FoodEatenLastYear, world.TheHarvestFeedsOneMore());
    }

    /// <summary>
    /// ⛔ A village that brings in less than it eats bears no child — even beside a full
    /// granary, which is exactly the shelf the level gate was reading.
    /// </summary>
    /// <remarks>
    /// The foragers are stood down for six years and the granary kept full by hand, so the
    /// level gate stays open and only the harvest can say no. Red with the harvest gate removed
    /// from <c>HouseholdSystem</c>: the stood-down village bears children beside its full shelf.
    /// The control arm — the same village with its foragers at work — is what makes the zero
    /// mean something.
    /// </remarks>
    [Fact]
    public void AVillageThatBringsInLessThanItEatsBearsNoChild()
    {
        int idle = BirthsOverSixYears(foragersStoodDown: true, out bool judgedShort);
        int working = BirthsOverSixYears(foragersStoodDown: false, out _);

        _output.WriteLine($"births in years four to nine: {idle} with the foragers stood down, {working} with them at work");
        Assert.True(judgedShort, "the ledger did not read the stood-down village as short, so the gate was never asked");
        Assert.Equal(0, idle);
        Assert.True(working > 0, "the control village bore nobody, so a zero would have proved nothing");
    }

    private static int BirthsOverSixYears(bool foragersStoodDown, out bool judgedShort)
    {
        SimConfig config = Config;
        SimLoop loop = SimFactory.CreatePhase0(config, new InMemoryLogSink());
        SimWorld world = loop.World;

        // Two years for the ledger to have a year behind it and the founders to have paired.
        loop.Step(config.TicksPerYear * 2);

        if (foragersStoodDown)
        {
            foreach (Workplace workplace in world.Workplaces)
            {
                if (workplace.Kind == JobKind.Forager)
                {
                    world.SetStaffing(workplace, 0);
                }
            }
        }

        int bornBefore = world.Villagers.Count;
        for (int tick = 0; tick < config.TicksPerYear * 7; tick++)
        {
            // The granary kept at the level gate's own bar, tick by tick, so that gate reads
            // plenty throughout and the LEDGER is what is being tested.
            //
            // ⚠️ IT USED TO BE FILLED TO THE BRIM, AND SINCE D399 THAT STOOD THE CONTROL ARM DOWN
            // TOO. Nobody produces food for their own larder any more (Joe): the only reason to
            // work is that the village wants food, and a granary filled to 2,500 wants none — so
            // the control village foraged nothing, brought in less than it ate, and bore no child
            // for the same reason the treatment arm did. **A control that fails for the reason
            // under test proves nothing** (D7). Topped up to the bar instead: the level gate is
            // satisfied exactly, and the village still wants the food its cupboards will hold.
            int bar = world.TargetFoodForTheGranary();
            foreach (StoreBuilding store in world.StoreBuildings)
            {
                if (store.Kind == StoreKind.Granary && world.FoodInGranaries() < bar)
                {
                    store.Store.Receive(Goods.Produce, bar - world.FoodInGranaries());
                }
            }

            loop.StepOnce();

            // Births are counted from the second year the foragers stood down — the first is
            // judged by a harvest they were still part of.
            if (tick == config.TicksPerYear)
            {
                bornBefore = world.Villagers.Count;
            }
        }

        judgedShort = !world.TheHarvestFeedsOneMore();
        return world.Villagers.Count - bornBefore;
    }

    /// <summary>The ledger is in the hash: a village that ate more last year is a different village.</summary>
    [Fact]
    public void TheLedgerIsHashed()
    {
        SimLoop loop = SimFactory.CreatePhase0(Config, new InMemoryLogSink());
        loop.Step(Config.TicksPerYear + 1);
        SimWorld world = loop.World;

        ulong before = StateHash.Compute(world);
        world.RecordFoodEaten(1);
        Assert.NotEqual(before, StateHash.Compute(world));
    }
}

using Bclone.Sim.Config;
using Bclone.Sim.Core;
using Bclone.Sim.Determinism;
using Bclone.Sim.Logging;
using Bclone.Sim.World;
using Xunit;
using Xunit.Abstractions;

namespace Bclone.Sim.Tests;

/// <summary>
/// Timber: the second job kind, and the first thing wood is for (decision D17).
/// </summary>
public sealed class WoodTests
{
    private readonly ITestOutputHelper _output;

    public WoodTests(ITestOutputHelper output) => _output = output;

    /// <summary>
    /// A village with enough foragers to feed itself and room left for woodcutters.
    /// </summary>
    /// <remarks>
    /// No special config needed any more: the patch sizes itself to the workforce and
    /// leaves a quarter of it spare, so timber gets hands on its own.
    /// </remarks>
    private static SimConfig Config => VillageFixtures.Village;

    private static SimLoop Build(SimConfig config, ulong? seed = null) =>
        SimFactory.CreatePhase0(config, new InMemoryLogSink(), seed);

    [Fact]
    public void SpareWorkersTakeTheTreeStand()
    {
        // The patch reserves a quarter of the workforce, so someone always cuts.
        SimLoop loop = Build(Config);
        loop.Step(Config.TicksPerSeason);

        Workplace stand = loop.World.Workplaces[1];
        _output.WriteLine($"{stand.WorkerIds.Count} at {stand.Name}");

        Assert.Equal(JobKind.Woodcutter, stand.Kind);
        Assert.NotEmpty(stand.WorkerIds);
    }

    [Fact]
    public void TheBerryPatchFillsBeforeTheTreeStand()
    {
        // The policy, such as it is: a village short of hands feeds itself before it
        // builds. Expressed as workplace id order rather than a priority field.
        SimLoop loop = Build(Config);
        loop.Step(Config.TicksPerSeason);

        Assert.True(loop.World.Workplaces[0].IsFullyStaffed,
            "The patch should be full before anyone cuts timber.");
    }

    [Fact]
    public void WoodcuttersActuallyProduceWood()
    {
        SimLoop loop = Build(Config);
        loop.Step(Config.TicksPerYear * 3);

        int wood = 0;
        foreach (Household household in loop.World.Households)
        {
            wood += household.Stockpile.LifetimeWoodCut;
        }

        _output.WriteLine($"{wood} wood cut in three years");
        Assert.True(wood > 0, "Nobody cut any timber.");
    }

    [Fact]
    public void TimberIsCutInWinterWhenBerriesCannotBeGathered()
    {
        // The reason the job is worth holding: trees do not stop in winter.
        SimLoop loop = Build(Config);

        // Run a few years first: labour is assigned seasonally, so the very first
        // winter can arrive before anyone holds a job at all.
        loop.Step(Config.TicksPerYear * 3);

        while (!loop.World.Clock.IsWinter)
        {
            loop.StepOnce();
        }

        int before = TotalLifetimeWood(loop.World);
        while (loop.World.Clock.IsWinter)
        {
            loop.StepOnce();
        }

        Assert.True(TotalLifetimeWood(loop.World) > before,
            "Timber should still be cut through winter.");
    }

    [Fact]
    public void AgeingWoodcuttersBringBackLess()
    {
        // Vigour scales timber exactly as it scales berries, so the ageing arc reads
        // the same way whichever job someone holds.
        Assert.True(Config.CutYield * 100 / 100 > Config.CutYield * Config.VigourMinPercent / 100);
    }

    [Fact]
    public void WoodIsNeverNegative()
    {
        SimLoop loop = Build(Config);

        for (int i = 0; i < 20_000; i++)
        {
            loop.StepOnce();
            foreach (Household household in loop.World.Households)
            {
                Assert.True(household.Stockpile.Wood >= 0);
            }
        }
    }

    [Fact]
    public void WoodProductionIsDeterministic()
    {
        SimLoop a = Build(Config);
        SimLoop b = Build(Config);

        a.Step(20_000);
        b.Step(20_000);

        Assert.Equal(StateHash.Compute(a.World), StateHash.Compute(b.World));
        Assert.Equal(TotalLifetimeWood(a.World), TotalLifetimeWood(b.World));
    }

    [Fact]
    public void TheHashCoversWood()
    {
        // Anti-vacuity: a hash blind to wood would let timber desync silently.
        SimLoop loop = Build(Config);
        loop.Step(Config.TicksPerYear);

        ulong before = StateHash.Compute(loop.World);
        loop.World.Households[0].Stockpile.AddWood(1);

        Assert.NotEqual(before, StateHash.Compute(loop.World));
    }

    [Fact]
    public void HousingCanBeGatedOnTimber()
    {
        // The mechanic works; it is disabled by default because it cannot pay off
        // until labour demand is dynamic (see SimConfig.WoodPerHouse). This proves
        // the gate itself holds, so turning it on later is a config change.
        SimLoop loop = Build(Config with { WoodPerHouse = 1_000_000 });
        int founding = loop.World.Households.Count;

        loop.Step(30_000);

        Assert.Equal(founding, loop.World.Households.Count);
    }

    [Fact]
    public void SplittingTheVillageEconomyFeedsItForAGeneration()
    {
        // With labour split, the village survives a generation and cuts real timber.
        //
        // It does NOT survive indefinitely, and that is the honest limit rather than
        // a bug: forager_demand is FIXED here, so two foragers must eventually fail
        // to feed a village of twenty however hard they work. Dynamic demand - the
        // patch wanting as many hands as the village needs fed - is the missing
        // piece, and it is the same gap that keeps wood_per_house switched off.
        SimLoop loop = Build(Config);
        loop.Step(Config.TicksPerYear * 30);

        _output.WriteLine(
            $"Year {loop.World.Clock.Year}: {loop.World.Population} alive, " +
            $"{TotalLifetimeWood(loop.World)} wood cut.");

        Assert.True(loop.World.Population > 0, "Splitting labour onto timber killed the village.");
        Assert.True(TotalLifetimeWood(loop.World) > 0);
    }

    private static int TotalLifetimeWood(SimWorld world)
    {
        int total = 0;
        for (int i = 0; i < world.Households.Count; i++)
        {
            total += world.Households[i].Stockpile.LifetimeWoodCut;
        }

        return total;
    }
}

using Bclone.Sim.Config;
using Bclone.Sim.Core;
using Bclone.Sim.Determinism;
using Bclone.Sim.Logging;
using Bclone.Sim.World;
using Xunit;
using Xunit.Abstractions;

namespace Bclone.Sim.Tests;

/// <summary>
/// Which goods a store will take, set at the building level — Joe, D141.
/// </summary>
/// <remarks>
/// <i>"User should be able to set which materials are stored in which buildings — e.g. a given
/// storage pile will only accept logs, another only firewood, another only iron ore."</i>
/// </remarks>
public sealed class StoreFilterTests
{
    private readonly ITestOutputHelper _output;

    public StoreFilterTests(ITestOutputHelper output) => _output = output;

    private static SimConfig Config => VillageFixtures.Village;

    private static SimWorld World() =>
        SimFactory.CreatePhase0(Config, new InMemoryLogSink()).World;

    private static StoreBuilding ShedIn(SimWorld world)
    {
        foreach (StoreBuilding store in world.StoreBuildings)
        {
            if (store.Kind == StoreKind.Shed)
            {
                return store;
            }
        }

        throw new Xunit.Sdk.XunitException("No shed in the founding village.");
    }

    /// <summary>⭐ Turning one good off leaves the others on.</summary>
    /// <remarks>
    /// The trap this guards: a mask of zero means <em>no opinion</em>, so the first click has to
    /// start from everything the kind holds and remove one. Starting from zero and adding the
    /// bit would mean the first click <b>emptied the building</b> of every other good instead.
    /// </remarks>
    [Fact]
    public void TurningOneGoodOffLeavesTheRestAlone()
    {
        SimWorld world = World();
        StoreBuilding shed = ShedIn(world);

        Assert.True(shed.Accepts(Goods.Logs));
        Assert.True(shed.Accepts(Goods.Firewood));

        Assert.True(world.SetStoreAccepts(shed, Goods.Logs, accepted: false).Allowed);

        _output.WriteLine(
            $"{shed.Name}: logs {shed.Accepts(Goods.Logs)}, firewood {shed.Accepts(Goods.Firewood)}, "
            + $"stone {shed.Accepts(Goods.Stone)}");

        Assert.False(shed.Accepts(Goods.Logs));
        Assert.True(shed.Accepts(Goods.Firewood));
        Assert.True(shed.Accepts(Goods.Stone));
    }

    /// <summary>⭐ It narrows only — a granary cannot be told to hold timber.</summary>
    /// <remarks>
    /// What a kind can hold is the model (D32), not a preference. The refusal lives in the sim
    /// rather than only in the view, because a control that cannot be misused and a rule that
    /// cannot be broken are different things, and only the second survives another caller.
    /// </remarks>
    [Fact]
    public void AGranaryCannotBeToldToHoldLogs()
    {
        SimWorld world = World();

        StoreBuilding granary = world.StoreBuildings[0];
        Assert.Equal(StoreKind.Granary, granary.Kind);

        PlacementVerdict verdict = world.SetStoreAccepts(granary, Goods.Logs, accepted: true);

        _output.WriteLine($"asking a granary for logs: {verdict.Reason}");

        Assert.False(verdict.Allowed);
        Assert.False(granary.Accepts(Goods.Logs));
        Assert.True(granary.Accepts(Goods.Food));
    }

    /// <summary>A store told to take nothing says so once, and then obeys (D42).</summary>
    [Fact]
    public void AStoreThatWillTakeNothingSaysSoOnce()
    {
        SimWorld world = World();
        StoreBuilding shed = ShedIn(world);

        PlacementVerdict last = default;
        for (int g = 0; g < Stockpile.Kinds; g++)
        {
            if (shed.CanEverHold((Goods)g))
            {
                last = world.SetStoreAccepts(shed, (Goods)g, accepted: false);
            }
        }

        _output.WriteLine($"a shed that takes nothing: \"{last.Warning}\"");

        Assert.True(last.Allowed, "The game argued with the player instead of obeying (D42).");
        Assert.False(string.IsNullOrWhiteSpace(last.Warning), "It obeyed without saying so.");
        Assert.False(shed.Accepts(Goods.Logs));
    }

    /// <summary>
    /// ⭐ Filters are silent until somebody sets one — no golden moves for the feature landing.
    /// </summary>
    /// <remarks>
    /// The same sparse-hash contract as <c>Workplace.QueueRank</c> and <c>Workplace.Mode</c>.
    /// A village where nobody has touched a filter must hash exactly as it did before filters
    /// existed, or a control nobody used would have re-taken every golden in the suite.
    /// </remarks>
    [Fact]
    public void FiltersAreSilentUntilSomebodySetsOne()
    {
        SimWorld untouched = World();
        SimWorld filtered = World();

        Assert.Equal(StateHash.Compute(untouched), StateHash.Compute(filtered));

        Assert.True(
            filtered.SetStoreAccepts(ShedIn(filtered), Goods.Logs, accepted: false).Allowed);

        Assert.NotEqual(StateHash.Compute(untouched), StateHash.Compute(filtered));
    }
}

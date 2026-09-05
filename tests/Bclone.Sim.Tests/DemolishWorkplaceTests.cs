using Bclone.Sim.Config;
using Bclone.Sim.Core;
using Bclone.Sim.Logging;
using Bclone.Sim.World;
using Xunit;
using Xunit.Abstractions;

namespace Bclone.Sim.Tests;

/// <summary>
/// A building the player put down can be taken back — <b>sites included</b> (D129).
/// </summary>
/// <remarks>
/// <para>
/// <b>Found by Joe, and invisible to 546 tests:</b> *"I can't cancel/demolish a building that
/// is under construction. Demolish says 'nothing there to pull down'."* The demolish tool only
/// ever searched the stores, so every hut and every construction site in the game was
/// permanent once marked — and nothing here asked whether they could be removed, because
/// `Demolish(Workplace)` had never existed to be tested.
/// </para>
/// <para>
/// <b>A misplaced building the player cannot take back is the opposite of the brush's
/// promise</b> (D43): marking is meant to be a decision you can explore, not one you are
/// stuck with.
/// </para>
/// </remarks>
public sealed class DemolishWorkplaceTests
{
    private readonly ITestOutputHelper _output;

    public DemolishWorkplaceTests(ITestOutputHelper output) => _output = output;

    private static SimConfig Config => VillageFixtures.Village;

    private static SimWorld Build() =>
        SimFactory.CreatePhase0(Config, new InMemoryLogSink()).World;

    /// <summary>Somewhere clear of the founding buildings to put something down.</summary>
    private static GridPos SomewhereFree(SimWorld world, BuildingKind kind)
    {
        GridPos site = world.Map.FoundingSite;
        for (int radius = 1; radius < 20; radius++)
        {
            for (int dy = -radius; dy <= radius; dy++)
            {
                for (int dx = -radius; dx <= radius; dx++)
                {
                    var at = new GridPos(site.X + dx, site.Y + dy);
                    if (world.CanBuildAt(kind, at).Allowed)
                    {
                        return at;
                    }
                }
            }
        }

        throw new Xunit.Sdk.XunitException($"Nowhere to put a {kind}.");
    }

    [Fact]
    public void AHalfBuiltThingCanBeTakenBack()
    {
        SimWorld world = Build();
        GridPos at = SomewhereFree(world, BuildingKind.Granary);

        Assert.True(world.Mark(BuildingKind.Granary, at).Allowed);

        Workplace site = Assert.Single(
            world.Workplaces, place => place.Construction?.Kind == BuildingKind.Granary);

        world.Demolish(site);

        Assert.DoesNotContain(
            world.Workplaces, place => place.Construction?.Kind == BuildingKind.Granary);
        Assert.False(world.SomethingStandsAt(at));
    }

    /// <summary>
    /// ⭐ And a builder's hut goes, which is the one Joe named and the one that pays nothing.
    /// </summary>
    /// <remarks>
    /// It is free and instant to raise (D108), so it is free and instant to remove — and its
    /// recipe of zero logs means the refund is zero without a special case, which is D98's
    /// free-timber press staying shut.
    /// </remarks>
    [Fact]
    public void AStandingBuildersHutGoesAndPaysNothingBack()
    {
        SimWorld world = Build();

        Workplace hut = Assert.Single(
            world.Workplaces, place => place.Kind == JobKind.Builder && !place.IsSite);

        int logsBefore = world.LogsInWarehouses();
        GridPos where = hut.Position;

        world.Demolish(hut);

        Assert.DoesNotContain(
            world.Workplaces, place => place.Kind == JobKind.Builder && !place.IsSite);
        Assert.False(world.SomethingStandsAt(where));

        _output.WriteLine($"logs {logsBefore} -> {world.LogsInWarehouses()} after pulling down a free hut");
        Assert.Equal(logsBefore, world.LogsInWarehouses());
    }

    /// <summary>Nobody keeps a job at a building that is no longer there.</summary>
    [Fact]
    public void ItsWorkersAreReleased()
    {
        SimWorld world = Build();
        SimLoop loop = new(world, SimFactory.CreatePhase0Systems());
        loop.Step(Config.TicksPerYear);

        Workplace staffed = Assert.Single(
            world.Workplaces,
            place => place.Kind == JobKind.Woodcutter && !place.IsSite);

        world.Demolish(staffed);

        Assert.DoesNotContain(world.Villagers, villager => villager.WorkplaceId == staffed.Id);
    }

    /// <summary>
    /// ⚠️ A market is one building in two lists, and half a demolition is worse than none.
    /// </summary>
    /// <remarks>
    /// D36's seam. Pulling down the stall and leaving the store would leave a building on the
    /// map still holding goods, which is exactly the right-stuff-in-the-wrong-place shape this
    /// project keeps paying for.
    /// </remarks>
    [Fact]
    public void DemolishingAMarketTakesBothHalves()
    {
        SimWorld world = Build();

        Workplace? stall = null;
        foreach (Workplace place in world.Workplaces)
        {
            if (place.Kind == JobKind.Marketer && !place.IsSite)
            {
                stall = place;
                break;
            }
        }

        Assert.True(stall is not null, "The fixture has no market, so this guard is vacuous.");
        GridPos where = stall!.Position;

        world.Demolish(stall);

        Assert.DoesNotContain(world.Workplaces, place => place.Position == where);
        Assert.DoesNotContain(world.StoreBuildings, store => store.Position == where);
    }
}

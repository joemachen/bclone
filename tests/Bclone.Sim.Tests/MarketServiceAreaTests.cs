using Bclone.Sim.Config;
using Bclone.Sim.Core;
using Bclone.Sim.Logging;
using Bclone.Sim.World;
using Xunit;
using Xunit.Abstractions;

namespace Bclone.Sim.Tests;

/// <summary>
/// ⭐⭐ A market's service area, stated truthfully — <b>a count, not a ring</b> (D201).
/// </summary>
/// <remarks>
/// <para>
/// <b>Joe asked to see the market's service radius before placing it.</b> ⛔ <b>There is no
/// radius, and drawing one would be drawing a lie:</b> a marketer picks the cheapest errand from
/// wherever they stand (§14.2) and households fetch from whatever store is nearest (§3), so
/// nothing in the model refuses a distance. <b>Inventing a ring would also rebuild the catchment
/// fence D120 deleted</b> — the one thing this project has already paid to take out.
/// </para>
/// <para>
/// <b>⭐ So the answer is the homes this would be the CLOSEST food store for</b>, which is exactly
/// the set whose walk it shortens — and it is not circular, because it depends on where the
/// granary already is. <b>That is Joe's own point about positioning, made checkable:</b> a market
/// beside the granary serves nobody.
/// </para>
/// </remarks>
public sealed class MarketServiceAreaTests
{
    private readonly ITestOutputHelper _output;

    public MarketServiceAreaTests(ITestOutputHelper output) => _output = output;

    private static SimConfig Config => ShippedConfig.Established();

    private static SimLoop Loop() => SimFactory.CreatePhase0(Config, new InMemoryLogSink());

    /// <summary>Run long enough that the village has built real homes to serve.</summary>
    private static SimWorld AVillageWithHomes(out SimLoop loop)
    {
        loop = Loop();
        for (int i = 0; i < Config.TicksPerYear * 12; i++)
        {
            loop.StepOnce();
        }

        return loop.World;
    }

    /// <summary>
    /// ⭐⭐ A market on top of the granary serves nobody — <b>which is the whole point</b>.
    /// </summary>
    /// <remarks>
    /// <b>Joe: *"that's the point. The user has to put thought into positioning."*</b> If a
    /// market beside the existing store reported a healthy number, the count would be telling the
    /// player that siting does not matter — the opposite of what it exists to say.
    /// </remarks>
    [Fact]
    public void AMarketOnTopOfTheGranaryIsNearestForNobody()
    {
        SimWorld world = AVillageWithHomes(out _);
        StoreBuilding granary = world.AnyStoreOf(StoreKind.Granary);

        int served = world.HomesAMarketHereWouldBeNearestFor(granary.Position);

        _output.WriteLine($"a market on the granary's own tile would be nearest for {served} homes");
        Assert.Equal(0, served);
    }

    /// <summary>
    /// ⭐⭐ …and a market on a family's doorstep is nearest for at least that family.
    /// </summary>
    /// <remarks>
    /// <b>The anti-vacuity half</b> (D7): without it, a method that returned zero for every tile
    /// in the valley would pass the guard above and the placement line would read *"0 homes"*
    /// wherever the player pointed.
    /// </remarks>
    [Fact]
    public void ButAMarketOnADoorstepIsNearestForThatHome()
    {
        SimWorld world = AVillageWithHomes(out _);

        Household home = world.Households.First(h =>
            h.HomePosition is not null && world.LivingMembersOf(h) > 0);

        int served = world.HomesAMarketHereWouldBeNearestFor(home.HomePosition!.Value);

        _output.WriteLine(
            $"a market on {home.Name}'s doorstep would be nearest for {served} homes");
        Assert.True(
            served >= 1,
            "A market standing on a family's own doorstep is not the nearest food store for even "
            + "that family, so the count cannot mean what the placement line says it means.");
    }

    /// <summary>
    /// ⭐ The count is what it claims: every home it counts really is nearer to here.
    /// </summary>
    /// <remarks>
    /// <b>Checked against the travel-cost field directly</b>, rather than against a second copy
    /// of the same loop — the point is that the sentence the player reads is true of the valley,
    /// not that two implementations agree.
    /// </remarks>
    [Fact]
    public void EveryHomeItCountsIsGenuinelyNearerToHere()
    {
        SimWorld world = AVillageWithHomes(out _);
        GridPos where = world.Map.FoundingSite;

        int claimed = world.HomesAMarketHereWouldBeNearestFor(where);
        int verified = 0;

        foreach (Household household in world.Households)
        {
            if (household.HomePosition is not GridPos home
                || world.LivingMembersOf(household) == 0)
            {
                continue;
            }

            int here = world.TravelCost.Cost(home, where);
            if (here == TravelCostField.Unreachable)
            {
                continue;
            }

            bool beatsEveryStore = world.StoreBuildings
                .Where(s => s.CanEverHold(Goods.Food))
                .Select(s => world.TravelCost.Cost(home, s.Position))
                .Where(c => c != TravelCostField.Unreachable)
                .All(c => here < c);

            if (beatsEveryStore)
            {
                verified++;
            }
        }

        _output.WriteLine($"claimed {claimed}, independently verified {verified}");
        Assert.Equal(verified, claimed);
    }

    /// <summary>⚠️ An empty house is not a home to serve.</summary>
    /// <remarks>
    /// A market sited to serve families that no longer exist is a market sited on a ghost, and
    /// the count is meant to be a reason to put a building somewhere.
    /// </remarks>
    [Fact]
    public void AHouseWithNobodyLeftInItIsNotCounted()
    {
        SimWorld world = AVillageWithHomes(out _);

        Household home = world.Households.First(h =>
            h.HomePosition is not null && world.LivingMembersOf(h) > 0);

        GridPos doorstep = home.HomePosition!.Value;
        int before = world.HomesAMarketHereWouldBeNearestFor(doorstep);

        foreach (Villager villager in world.Villagers.Where(v => v.HouseholdId == home.Id))
        {
            villager.Alive = false;
        }

        int after = world.HomesAMarketHereWouldBeNearestFor(doorstep);

        _output.WriteLine($"{home.Name} emptied: served {before} → {after}");
        Assert.True(
            after < before,
            $"Emptying {home.Name} did not change the count, so it is counting houses rather "
            + "than families.");
    }

    /// <summary>⭐ Asking changes nothing — it is a placement aid the view calls every frame.</summary>
    [Fact]
    public void AskingCostsTheValleyNothing()
    {
        SimWorld world = AVillageWithHomes(out SimLoop loop);
        ulong before = Bclone.Sim.Determinism.StateHash.Compute(world);

        for (int y = -6; y <= 6; y++)
        {
            for (int x = -6; x <= 6; x++)
            {
                world.HomesAMarketHereWouldBeNearestFor(
                    new GridPos(world.Map.FoundingSite.X + x, world.Map.FoundingSite.Y + y));
            }
        }

        Assert.Equal(before, Bclone.Sim.Determinism.StateHash.Compute(world));
        _output.WriteLine("asked about 169 tiles; the world is byte-identical");
    }
}

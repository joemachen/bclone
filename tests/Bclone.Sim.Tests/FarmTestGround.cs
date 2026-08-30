using Bclone.Sim.Config;
using Bclone.Sim.Core;
using Bclone.Sim.Logging;
using Bclone.Sim.World;
using Xunit;

namespace Bclone.Sim.Tests;

/// <summary>
/// Siting a farm at a chosen distance from the store it hauls to — <b>the fixture the distance
/// bug needed and did not have</b>.
/// </summary>
/// <remarks>
/// <b>D157's blind guard, in fixture form.</b> <see cref="FarmFixtures.ClearGroundNear"/> puts a
/// farm on the first buildable tile beside the founding site, so the walk the derivation budgets
/// and the walk the farmer takes are the same walk — which is why
/// <c>AFarmBringsInMostOfWhatItSows</c> reported 93% while Joe's village sat at 46%. <b>Every
/// guard about distance has to place the farm on purpose</b>, and this is the one place that
/// does it, so the three files that need it cannot drift apart (D142's three call sites).
/// </remarks>
internal static class FarmTestGround
{
    /// <summary>Buildable ground as close as possible to a given walk from the nearest granary.</summary>
    internal static GridPos GroundAtAboutThisWalk(SimWorld world, int walkAway, out int walk)
    {
        GridPos site = world.Map.FoundingSite;
        StoreBuilding granary = world.AnyStoreOf(StoreKind.Granary);

        GridPos best = FarmFixtures.ClearGroundNear(world);
        walk = world.TravelCost.TicksBetween(best, granary.Position);

        for (int dy = -14; dy <= 14; dy++)
        {
            for (int dx = -14; dx <= 14; dx++)
            {
                var at = new GridPos(site.X + dx, site.Y + dy);
                if (world.HasSomethingToHarvest(at)
                    || !world.CanBuildAt(BuildingKind.Farmhouse, at).Allowed)
                {
                    continue;
                }

                int cost = world.TravelCost.TicksBetween(at, granary.Position);
                if (cost != TravelCostField.Unreachable
                    && Math.Abs(cost - walkAway) < Math.Abs(walk - walkAway))
                {
                    best = at;
                    walk = cost;
                }
            }
        }

        return best;
    }

    /// <summary>Raise a farmhouse as close as possible to a chosen walk from the granary.</summary>
    internal static Workplace SiteAFarm(SimWorld world, int walkAway, out int walk) =>
        FarmFixtures.RaiseAFarm(world, GroundAtAboutThisWalk(world, walkAway, out walk));

    /// <summary>Put a granary up right beside a farm, without waiting for a builder.</summary>
    internal static StoreBuilding RaiseAGranaryBeside(SimWorld world, Workplace farm)
    {
        for (int radius = 1; radius < 6; radius++)
        {
            for (int dy = -radius; dy <= radius; dy++)
            {
                for (int dx = -radius; dx <= radius; dx++)
                {
                    var at = new GridPos(farm.Position.X + dx, farm.Position.Y + dy);
                    if (!world.CanBuildAt(BuildingKind.Granary, at).Allowed
                        || !world.Mark(BuildingKind.Granary, at).Allowed)
                    {
                        continue;
                    }

                    Workplace site = Assert.Single(
                        world.Workplaces, place => place.Construction?.Kind == BuildingKind.Granary);

                    BuildFixtures.StockTheSite(site);
                    for (int i = 0; i <= site.Construction!.Recipe.WorkTicks; i++)
                    {
                        site.Construction.Work();
                    }

                    world.Complete(site);
                    return Assert.Single(
                        world.StoreBuildings, store => store.Position == at);
                }
            }
        }

        throw new Xunit.Sdk.XunitException("Nowhere beside the farm would take a granary.");
    }

    /// <summary>
    /// Ten years of a farm sited at a chosen walk; the tiles it reaped and the share of what it
    /// sowed that it brought in.
    /// </summary>
    internal static int TilesReapedOverTenYears(
        SimConfig config, int walkAway, out int walk, out int broughtIn)
    {
        // Nothing in the stores, so the farm is measured while the village needs what it grows
        // — see `FarmFixtures.WithNothingInTheStores` for why that stopped being free (D262).
        SimLoop loop = FarmFixtures.WithNothingInTheStores(
            SimFactory.CreatePhase0(config, new InMemoryLogSink()));
        SimWorld world = loop.World;

        Workplace farm = SiteAFarm(world, walkAway, out walk);
        FarmFixtures.GiveItGround(world, farm, reach: 3);

        int sown = 0;
        int reaped = 0;

        for (int i = 0; i < config.TicksPerYear * 10; i++)
        {
            loop.StepOnce();
            foreach (Villager villager in world.Villagers)
            {
                if (!villager.Alive
                    || villager.WorkplaceId != farm.Id
                    || villager.ActionTicksRemaining != 1)
                {
                    continue;
                }

                if (villager.State == VillagerState.Sowing)
                {
                    sown++;
                }
                else if (villager.State == VillagerState.Reaping)
                {
                    reaped++;
                }
            }
        }

        broughtIn = sown == 0 ? 0 : reaped * 100 / sown;
        return reaped;
    }
}

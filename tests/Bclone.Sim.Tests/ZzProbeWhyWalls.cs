using Bclone.Sim.Config;
using Bclone.Sim.Core;
using Bclone.Sim.Logging;
using Bclone.Sim.World;
using Xunit;
using Xunit.Abstractions;
namespace Bclone.Sim.Tests;
public sealed class ZzProbeWhyWalls
{
    private readonly ITestOutputHelper _o;
    public ZzProbeWhyWalls(ITestOutputHelper o) => _o = o;
    [Fact]
    public void Why()
    {
        SimConfig config = ShippedConfig.Load() with { Seed = 12345UL };
        SimLoop loop = SimFactory.CreatePhase0(config, new InMemoryLogSink());
        SimWorld world = loop.World;
        ColdStartTests.PlayTheOpening(world);
        GridPos site = world.Map.FoundingSite;
        for (int y = 0; y < 50; y++)
        {
            loop.Step(config.TicksPerYear);
            int unreachableHomes = 0, homes = 0, noGate = 0;
            foreach (Household h in world.Households)
            {
                if (h.HomeTile is not GridPos home || world.LivingMembersOf(h) == 0) continue;
                homes++;
                if (!world.TravelCost.CanReach(site, home)) unreachableHomes++;
                if (h.FencedTiles.Count > 0 && !world.Zones.PlotIsGated(h.Id)) noGate++;
            }
            int unreachableStores = world.StoreBuildings.Count(s => !world.TravelCost.CanReach(site, s.Tile));
            if (y % 5 == 0 || y > 44)
                _o.WriteLine($"y{y + 1}: pop {world.Population} homes {homes} unreachableHomes {unreachableHomes} noGate {noGate} unreachableStores {unreachableStores} food {world.FoodTheVillageHolds()} wants {world.TheVillageWantsMoreFood()} starved {world.Villagers.Count(v => !v.Alive && v.CauseOfDeath == CauseOfDeath.Starvation)} cold {world.Villagers.Count(v => !v.Alive && v.CauseOfDeath == CauseOfDeath.Cold)}");
        }
    }
}

using Bclone.Sim.Config;
using Bclone.Sim.Core;
using Bclone.Sim.Logging;
using Bclone.Sim.World;
using Xunit;
using Xunit.Abstractions;
namespace Bclone.Sim.Tests;
public sealed class ZzProbeYards
{
    private readonly ITestOutputHelper _o;
    public ZzProbeYards(ITestOutputHelper o) => _o = o;
    [Fact]
    public void Measure()
    {
        long steps = 0, throughOthers = 0, ownYard = 0;
        int alive = 0, peak = 0, starved = 0, plots = 0, gated = 0;
        foreach (ulong seed in new ulong[] { 12345UL, 2UL, 7UL, 1UL, 3UL, 11UL })
        {
            SimConfig config = ShippedConfig.Load() with { Seed = seed };
            SimLoop loop = SimFactory.CreatePhase0(config, new InMemoryLogSink());
            SimWorld world = loop.World;
            ColdStartTests.PlayTheOpening(world);
            var wasAt = new Dictionary<int, GridPos>();
            int seedPeak = 0;
            for (int t = 0; t < config.TicksPerYear * 50; t++)
            {
                loop.StepOnce();
                if (t % config.TicksPerYear == 0) seedPeak = System.Math.Max(seedPeak, world.Population);
                foreach (Villager v in world.Villagers)
                {
                    if (!v.Alive) continue;
                    GridPos now = v.Tile;
                    if (wasAt.TryGetValue(v.Id, out GridPos before) && before != now)
                    {
                        steps++;
                        int owner = world.Zones.PlotOwner(now);
                        if (owner != 0) { if (owner == world.HouseholdOf(v).Id) ownYard++; else throughOthers++; }
                    }
                    wasAt[v.Id] = now;
                }
            }
            foreach (Household h in world.Households)
            {
                if (h.FencedTiles.Count == 0) continue;
                plots++; if (world.Zones.PlotIsGated(h.Id)) gated++;
            }
            alive += world.Population; peak += seedPeak;
            starved += world.Villagers.Count(v => !v.Alive && v.CauseOfDeath == CauseOfDeath.Starvation);
        }
        _o.WriteLine($"WALLS: {steps} steps, {throughOthers} ({throughOthers * 100.0 / steps:F2}%) in SOMEBODY ELSE'S yard, {ownYard} ({ownYard * 100.0 / steps:F1}%) own; alive {alive} peak {peak} starved {starved}; {plots} plots, {gated} gated");
    }
}

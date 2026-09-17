using Bclone.Sim.Config;
using Bclone.Sim.Core;
using Bclone.Sim.Logging;
using Bclone.Sim.World;
using Xunit;
using Xunit.Abstractions;

namespace Bclone.Sim.Tests;

/// <summary>
/// Trades visibly work — a forager in the ring, a hunter in the woods (D384,
/// `specs/trades-visibly-work.md`). The woodcutter's stint is guarded in <c>FirewoodTests</c>.
/// </summary>
/// <remarks>
/// Joe, 2026-09-15: *"im not sure that hunters spend any time at the hunting lodge or in the
/// forest actually 'hunting', same with foragers."* The look is where a person stands and for how
/// many ticks (D373); these guards watch the tile under a villager while they work.
/// </remarks>
public sealed class TradesVisiblyWorkTests
{
    private readonly ITestOutputHelper _output;

    public TradesVisiblyWorkTests(ITestOutputHelper output) => _output = output;

    private static SimConfig Config => VillageFixtures.Village;

    /// <summary>
    /// ⛔ A forager gathers in the ring, not on the hut — <b>every gathering tick stands on a
    /// wooded tile within <c>gather_walk_tiles</c> of the hut, and some of them are not the hut</b>.
    /// </summary>
    /// <remarks>
    /// Before D384 a forager stood on the hut's own point for every gather
    /// (`forests-and-gathering.md §3.3` said so). Red with the walk off: every gathering tick is
    /// on the hut tile.
    /// </remarks>
    [Fact]
    public void AForagerGathersInTheRingNotOnTheHut()
    {
        SimLoop loop = SimFactory.CreatePhase0(Config, new InMemoryLogSink());
        SimWorld world = loop.World;

        int gathering = 0;
        int onTheHut = 0;
        int offTheHut = 0;
        var wrong = new List<string>();
        var seen = new HashSet<GridPos>();

        for (int tick = 0; tick < Config.TicksPerYear * 3; tick++)
        {
            loop.StepOnce();
            foreach (Villager villager in world.Villagers)
            {
                if (villager.State != VillagerState.Gathering
                    || world.FindWorkplace(villager.WorkplaceId) is not { Kind: JobKind.Forager } hut)
                {
                    continue;
                }

                gathering++;
                GridPos at = villager.Tile;
                if (at == hut.Tile)
                {
                    onTheHut++;
                    continue;
                }

                offTheHut++;
                seen.Add(at);
                if (world.Map.TerrainAt(at) != Terrain.Forest
                    || at.ManhattanDistanceTo(hut.Tile) > Config.GatherWalkTiles)
                {
                    wrong.Add($"{villager.Name} gathering at {at}, {world.Map.TerrainAt(at)}, {at.ManhattanDistanceTo(hut.Tile)} from the hut");
                }
            }
        }

        _output.WriteLine(
            $"{gathering} gathering ticks over three years: {offTheHut} in the ring on {seen.Count} tiles, "
            + $"{onTheHut} on the hut; {wrong.Count} off the rule");

        Assert.True(gathering > 0, "Nobody ever gathered, so this guard watched nothing (D7).");
        Assert.True(offTheHut > onTheHut, $"foragers gathered on the hut {onTheHut} ticks and in the ring {offTheHut}");
        Assert.True(seen.Count > 1, "every gather was on one tile — the ring is not being walked");
        Assert.True(wrong.Count == 0, string.Join("; ", wrong.Take(5)));
    }
}

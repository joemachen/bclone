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

    /// <summary>
    /// ⛔ A hunter hunts at a forest tile in the woods and brings the meat back to the lodge —
    /// <b>every hunting tick stands on a forest tile within <c>hunt_walk_tiles</c> of the lodge,
    /// never on the lodge, and the lodge's store rises only when the hunter is standing on it</b>.
    /// </summary>
    /// <remarks>
    /// Before D384 a hunter hunted on the lodge tile and re-armed in place until the store was
    /// full. Posed with a lodge raised in the fixture's woods. Red with the walk off: every hunting
    /// tick is on the lodge.
    /// </remarks>
    [Fact]
    public void AHunterHuntsAtAForestTileAndBringsTheMeatToTheLodge()
    {
        SimLoop loop = SimFactory.CreatePhase0(Config, new InMemoryLogSink());
        SimWorld world = loop.World;
        Workplace lodge = HuntingTests.RaiseALodgeFor(world);
        world.SetStaffing(lodge, 1);

        int hunting = 0;
        int onTheLodge = 0;
        var seen = new HashSet<GridPos>();
        var wrong = new List<string>();
        int meatBefore = lodge.Store[Goods.Meat];
        int risesSeenFromTheLodge = 0;
        int risesSeenElsewhere = 0;

        for (int tick = 0; tick < Config.TicksPerYear * 2; tick++)
        {
            loop.StepOnce();
            int meatNow = lodge.Store[Goods.Meat];
            if (meatNow > meatBefore)
            {
                bool somebodyOnIt = false;
                foreach (Villager villager in world.Villagers)
                {
                    if (villager.WorkplaceId == lodge.Id && lodge.Footprint.CoveredTiles().Contains(villager.Tile))
                    {
                        somebodyOnIt = true;
                    }
                }

                if (somebodyOnIt) { risesSeenFromTheLodge++; } else { risesSeenElsewhere++; }
            }

            meatBefore = meatNow;

            foreach (Villager villager in world.Villagers)
            {
                if (villager.State != VillagerState.Hunting || villager.WorkplaceId != lodge.Id)
                {
                    continue;
                }

                hunting++;
                GridPos at = villager.Tile;
                if (lodge.Footprint.CoveredTiles().Contains(at))
                {
                    onTheLodge++;
                    continue;
                }

                seen.Add(at);
                if (world.Map.TerrainAt(at) != Terrain.Forest || at.ManhattanDistanceTo(lodge.Tile) > Config.HuntWalkTiles)
                {
                    wrong.Add($"{villager.Name} hunting at {at}, {world.Map.TerrainAt(at)}, {at.ManhattanDistanceTo(lodge.Tile)} from the lodge");
                }
            }
        }

        _output.WriteLine(
            $"{hunting} hunting ticks over two years on {seen.Count} forest tiles, {onTheLodge} on the lodge; "
            + $"the lodge's meat rose {risesSeenFromTheLodge} times with a hunter on it and {risesSeenElsewhere} times with nobody there; "
            + $"{wrong.Count} off the rule");

        Assert.True(hunting > 0, "Nobody ever hunted, so this guard watched nothing (D7).");
        Assert.True(onTheLodge == 0, $"the hunter hunted on the lodge for {onTheLodge} ticks");
        Assert.True(seen.Count > 1, "every hunt was on one tile — the woods are not being walked");
        Assert.True(wrong.Count == 0, string.Join("; ", wrong.Take(5)));
        Assert.True(risesSeenFromTheLodge > 0, "the lodge's store never rose with a hunter standing on it");
        Assert.True(risesSeenElsewhere == 0, $"the lodge's store rose {risesSeenElsewhere} times with nobody on it");
    }
}

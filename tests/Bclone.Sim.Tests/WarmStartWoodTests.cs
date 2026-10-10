using Bclone.Sim.Core;
using Bclone.Sim.Logging;
using Bclone.Sim.World;
using Xunit;
using Xunit.Abstractions;

namespace Bclone.Sim.Tests;

/// <summary>
/// The warm start's forester is given only wood the village can walk to (D544).
/// </summary>
/// <remarks>
/// <para>
/// <c>SimWorld.GiveItTheWoodAroundIt</c> took the nearest wooded tiles inside the gatherer ring and never asked whether
/// anybody could reach them — D110's mistake (<em>not water is not the same as reachable</em>), latent until D543's seams
/// reshuffled the fixture's seed 7: all 72 tiles lay across the river, no log was ever felled, and the founders froze in
/// Year 1. A real game starts cold and the player paints the ground; the warm start is what every village fixture in the
/// suite is built on.
/// </para>
/// </remarks>
public sealed class WarmStartWoodTests
{
    private readonly ITestOutputHelper _output;

    public WarmStartWoodTests(ITestOutputHelper output) => _output = output;

    /// <summary>⭐ Every tile of the warm-start forester's ground is one the village can walk to, in every fixture valley.</summary>
    [Fact]
    public void TheWarmStartForesterIsGivenOnlyWoodTheVillageCanReach()
    {
        int valleys = 0, tilesGiven = 0, cutOff = 0, thinnest = int.MaxValue;
        for (ulong seed = 1; seed <= 64; seed++)
        {
            SimWorld world = SimFactory.CreatePhase0(VillageFixtures.Village with { Seed = seed }, new InMemoryLogSink()).World;
            GridPos founding = world.Map.FoundingSite;
            foreach (Workplace forester in world.Workplaces.Where(w => w.Kind == JobKind.Forester))
            {
                int given = 0, reached = 0;
                for (int i = 0; i < world.Map.Tiles.Count; i++)
                {
                    GridPos tile = world.Zones.PositionOf(i);
                    if (world.Zones.WorkGroundOwner(tile) != forester.Id)
                    {
                        continue;
                    }

                    given++;
                    if (world.TravelCost.CanReach(tile, founding))
                    {
                        reached++;
                    }
                    else
                    {
                        _output.WriteLine($"seed {seed}: forester ground at {tile} is cut off from the founding at {founding}");
                    }
                }

                valleys++;
                tilesGiven += given;
                cutOff += given - reached;
                thinnest = Math.Min(thinnest, reached);
            }
        }

        _output.WriteLine($"{valleys} warm-start foresters, {tilesGiven} tiles given, {cutOff} cut off; the thinnest reachable ground {thinnest}");
        Assert.True(valleys >= 64, $"only {valleys} warm-start foresters in 64 valleys");
        Assert.Equal(0, cutOff);
        Assert.True(thinnest > 0, "a warm-start forester was given no ground it can reach");
    }
}

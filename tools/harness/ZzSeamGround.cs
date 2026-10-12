using Bclone.Sim.Config;
using Bclone.Sim.Core;
using Bclone.Sim.World;
using Xunit;

namespace Bclone.Sim.Tests;

// D561's measurement: per seam in 100 shipped valleys, its size (what a quarry or mine may paint now) against the most
// a building standing by it could paint under D541's diamond (seam tiles within 2 of the building's tile).
public sealed class ZzSeamGround
{
    public static IEnumerable<object[]> Seeds()
    {
        for (ulong s = 200; s < 300; s++)
        {
            yield return new object[] { s };
        }
    }

    [Theory]
    [MemberData(nameof(Seeds))]
    public void Measure(ulong seed)
    {
        SimConfig config = ShippedConfig.Load() with { Seed = seed };
        SimWorld world = SimFactory.CreatePhase0(config, new Bclone.Sim.Logging.InMemoryLogSink()).World;
        int reach = config.FaceReachTiles;
        var size = new Dictionary<int, int>();
        var kind = new Dictionary<int, Terrain>();
        for (int i = 0; i < world.Map.Tiles.Count; i++)
        {
            GridPos at = world.Zones.PositionOf(i);
            int patch = world.SeamPatchOf(at);
            if (patch != 0)
            {
                size[patch] = size.GetValueOrDefault(patch) + 1;
                kind[patch] = world.Map.Tiles[i];
            }
        }

        // The best diamond for each seam: over every non-seam tile within reach of it, the seam tiles within reach.
        var best = new Dictionary<int, int>();
        for (int i = 0; i < world.Map.Tiles.Count; i++)
        {
            GridPos c = world.Zones.PositionOf(i);
            if (TerrainRules.IsSeam(world.Map.Tiles[i]) || world.Map.Tiles[i] == Terrain.Water)
            {
                continue;
            }

            var counts = new Dictionary<int, int>();
            for (int dy = -reach; dy <= reach; dy++)
            {
                int span = reach - Math.Abs(dy);
                for (int dx = -span; dx <= span; dx++)
                {
                    int patch = world.SeamPatchOf(new GridPos(c.X + dx, c.Y + dy));
                    if (patch != 0)
                    {
                        counts[patch] = counts.GetValueOrDefault(patch) + 1;
                    }
                }
            }

            foreach ((int patch, int n) in counts)
            {
                best[patch] = Math.Max(best.GetValueOrDefault(patch), n);
            }
        }

        foreach ((int patch, int n) in size)
        {
            Console.WriteLine($"ZZG {seed} {kind[patch]} size {n} diamond {best.GetValueOrDefault(patch)}");
        }
    }
}

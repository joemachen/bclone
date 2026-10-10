using Bclone.Sim.Config;
using Bclone.Sim.World;
using Xunit;
using Xunit.Abstractions;

namespace Bclone.Sim.Tests;

/// <summary>
/// More stone and iron, across the whole valley, in many shapes and sizes — <c>specs/seams-revisited.md</c> (D542, D543).
/// </summary>
/// <remarks>
/// <para>
/// Joe: *"revisit iron and stone node volume and positioning and frequency and size and shape"*, then: volume as is;
/// stone where it is and iron a little closer; *"more variety, more frequency across the whole valley"* (about twice
/// as many); *"wider range"* of sizes; shapes *"all of the above, more variety"*; seams across the river kept (bridges
/// come later); and only the near iron seams must hold 50.
/// </para>
/// <para>
/// The near seams — <see cref="MapGenerator.SeamsOf"/>, the rings — keep their guarantees in <see cref="SeamsTests"/>.
/// These guards are about the valley-wide ones, <see cref="MapGenerator.ScatteredSeamsOf"/>, and the valley as a whole.
/// </para>
/// </remarks>
public sealed class SeamVarietyTests
{
    private const int Valleys = 64;

    private readonly ITestOutputHelper _output;

    public SeamVarietyTests(ITestOutputHelper output) => _output = output;

    private static SimConfig Shipped => ShippedConfig.Load();

    private static IEnumerable<(ulong Seed, SimConfig Config)> TheValleys()
    {
        SimConfig shipped = Shipped;
        for (ulong seed = 1; seed <= Valleys; seed++)
        {
            yield return (seed, shipped with { Seed = seed });
        }
    }

    private static List<MapGenerator.Seam> AllOf(SimConfig config, ulong seed, Terrain kind) =>
        MapGenerator.SeamsOf(config, seed, kind).Concat(MapGenerator.ScatteredSeamsOf(config, seed, kind)).ToList();

    // ---------------------------------------------------------------
    //  Frequency and variety
    // ---------------------------------------------------------------

    /// <summary>
    /// ⭐ About twice as many seams as before (12 stone and 4 iron), and valleys that differ from each other — one rich,
    /// the next lean.
    /// </summary>
    [Fact]
    public void ValleysHoldAboutTwiceTheSeamsAndDifferInHowMany()
    {
        foreach (var (kind, before) in new[] { (Terrain.Rock, 12), (Terrain.IronDeposit, 4) })
        {
            var counts = TheValleys().Select(v => AllOf(v.Config, v.Seed, kind).Count).ToList();
            double mean = counts.Average();
            _output.WriteLine($"{kind}: seams a valley {counts.Min()}–{counts.Max()}, mean {mean:0.0} (before: {before} in every valley)");

            Assert.InRange(mean, before * 1.7, before * 2.4);
            Assert.True(counts.Max() - counts.Min() >= before * 2 / 3,
                $"{kind}: every valley holds {counts.Min()}–{counts.Max()} seams — not the variety asked for");
        }
    }

    // ---------------------------------------------------------------
    //  Positioning
    // ---------------------------------------------------------------

    /// <summary>The valley-wide seams reach every quarter and the far edges, not only a ring round the village.</summary>
    [Fact]
    public void ScatteredSeamsReachTheWholeValley()
    {
        foreach (Terrain kind in new[] { Terrain.Rock, Terrain.IronDeposit })
        {
            var centres = TheValleys().SelectMany(v => MapGenerator.ScatteredSeamsOf(v.Config, v.Seed, kind)).Select(s => s.Centre).ToList();
            Assert.True(centres.Count >= Valleys, $"only {centres.Count} scattered {kind} seams in {Valleys} valleys");

            int far = centres.Count(c => (c.X * c.X) + (c.Y * c.Y) > 30 * 30);
            var quarters = new int[4];
            foreach (GridPos c in centres)
            {
                quarters[(c.X >= 0 ? 0 : 1) + (c.Y >= 0 ? 0 : 2)]++;
            }

            _output.WriteLine($"{kind}: {centres.Count} scattered; {far} beyond 30 tiles; quarters {string.Join("/", quarters)}");
            Assert.True(far * 4 >= centres.Count, $"{kind}: only {far} of {centres.Count} scattered seams lie beyond 30 tiles");
            Assert.All(quarters, q => Assert.True(q * 100 >= centres.Count * 15, $"{kind}: a quarter of the valley holds {q} of {centres.Count}"));
        }
    }

    /// <summary>No valley-wide seam lands on the founders' house plots (D434's reason the rings never come nearer).</summary>
    [Fact]
    public void NoScatteredSeamCrowdsTheFounding()
    {
        Assert.True(Shipped.StoneSeamClearOfFoundingTiles > 0);
        Assert.True(Shipped.IronSeamClearOfFoundingTiles > Shipped.StoneSeamClearOfFoundingTiles, "iron is no further off the doorstep than stone");
        foreach (var (seed, config) in TheValleys())
        {
            foreach (Terrain kind in new[] { Terrain.Rock, Terrain.IronDeposit })
            {
                int clear = kind == Terrain.Rock ? config.StoneSeamClearOfFoundingTiles : config.IronSeamClearOfFoundingTiles;
                foreach (MapGenerator.Seam seam in MapGenerator.ScatteredSeamsOf(config, seed, kind))
                {
                    int d2 = (seam.Centre.X * seam.Centre.X) + (seam.Centre.Y * seam.Centre.Y);
                    Assert.True(d2 >= clear * clear, $"seed {seed}: a {kind} seam at {seam.Centre}, within {clear} of the founding");
                }
            }
        }
    }

    /// <summary>⭐ Iron a little closer: the nearest iron to the founding is nearer than the 30 tiles it was.</summary>
    [Fact]
    public void TheNearestIronIsALittleCloser()
    {
        var nearest = new List<int>();
        foreach (var (seed, config) in TheValleys())
        {
            GeneratedMap map = MapGenerator.Generate(config, seed);
            int best = int.MaxValue;
            for (int i = 0; i < map.Tiles.Count; i++)
            {
                if (map.Tiles[i] == Terrain.IronDeposit)
                {
                    int x = map.MinX + (i % map.Width), y = map.MinY + (i / map.Width);
                    best = Math.Min(best, Math.Abs(x - map.FoundingSite.X) + Math.Abs(y - map.FoundingSite.Y));
                }
            }

            nearest.Add(best);
        }

        nearest.Sort();
        int median = nearest[nearest.Count / 2];
        _output.WriteLine($"nearest iron to the founding: {nearest[0]}–{nearest[^1]}, median {median} (was median 30)");

        // A little closer, not next door: measured 24 at the shipped ring of 22 (D543).
        Assert.InRange(median, 21, 26);
        Assert.True(nearest[0] >= 10, $"iron {nearest[0]} tiles from the founders is on the doorstep");
    }

    // ---------------------------------------------------------------
    //  Size and shape
    // ---------------------------------------------------------------

    /// <summary>⭐ A wider range: pebbles of a few tiles to outcrops of a few dozen.</summary>
    [Fact]
    public void ScatteredSeamsRangeFromPebblesToOutcrops()
    {
        var sizes = TheValleys()
            .SelectMany(v => new[] { Terrain.Rock, Terrain.IronDeposit }
                .SelectMany(k => MapGenerator.ScatteredSeamsOf(v.Config, v.Seed, k).Select(s => MapGenerator.FootprintOf(s, k).Count)))
            .OrderBy(n => n).ToList();
        int p10 = sizes[sizes.Count / 10], p90 = sizes[sizes.Count * 9 / 10];
        _output.WriteLine($"scattered seam tiles: {sizes[0]}–{sizes[^1]}, p10 {p10}, median {sizes[sizes.Count / 2]}, p90 {p90}");

        Assert.True(p10 <= 4, $"the smallest tenth are {p10}+ tiles — no pebbles");
        Assert.True(p90 >= 22, $"the largest tenth stop at {p90} tiles — no big outcrops");
    }

    /// <summary>
    /// ⭐ Every shape for both kinds — blobs, veins, clusters and blobs with arms — and the near seams only the
    /// solid ones, so they keep holding what their guarantees count.
    /// </summary>
    [Fact]
    public void EveryShapeAppearsForEachKind()
    {
        foreach (Terrain kind in new[] { Terrain.Rock, Terrain.IronDeposit })
        {
            var shapes = TheValleys().SelectMany(v => AllOf(v.Config, v.Seed, kind)).GroupBy(s => s.Shape)
                .ToDictionary(g => g.Key, g => g.Count());
            _output.WriteLine($"{kind}: " + string.Join(", ", shapes.OrderBy(p => p.Key).Select(p => $"{p.Key} {p.Value}")));
            foreach (MapGenerator.SeamShape shape in Enum.GetValues<MapGenerator.SeamShape>())
            {
                Assert.True(shapes.GetValueOrDefault(shape) > 0, $"no {kind} seam is a {shape}");
            }

            // The near seams: stone a blob or a blob with arms, iron a blob — what their guarantees count.
            Assert.All(TheValleys().SelectMany(v => MapGenerator.SeamsOf(v.Config, v.Seed, kind)),
                s => Assert.True(
                    s.Shape == MapGenerator.SeamShape.Blob || (kind == Terrain.Rock && s.Shape == MapGenerator.SeamShape.Arms),
                    $"a near {kind} seam is a {s.Shape}"));
        }
    }

    /// <summary>A vein is long and thin; a blob is round. Measured on each seam painted alone.</summary>
    [Fact]
    public void AVeinIsLongAndABlobIsRound()
    {
        var veins = new List<int>();
        var blobs = new List<int>();
        foreach (var (seed, config) in TheValleys())
        {
            foreach (Terrain kind in new[] { Terrain.Rock, Terrain.IronDeposit })
            {
                foreach (MapGenerator.Seam seam in MapGenerator.ScatteredSeamsOf(config, seed, kind))
                {
                    IReadOnlyList<GridPos> tiles = MapGenerator.FootprintOf(seam, kind);
                    if (tiles.Count < 6)
                    {
                        continue;
                    }

                    int stretch = 100 * SpanSquared(tiles) / tiles.Count;
                    (seam.Shape == MapGenerator.SeamShape.Vein ? veins : seam.Shape == MapGenerator.SeamShape.Blob ? blobs : null)?.Add(stretch);
                }
            }
        }

        veins.Sort();
        blobs.Sort();
        Assert.NotEmpty(veins);
        Assert.NotEmpty(blobs);
        int vein = veins[veins.Count / 2], blob = blobs[blobs.Count / 2];
        // A disc reads about 4/π (127); a vein one tile wide and L long reads 100 × L.
        _output.WriteLine($"(longest straight span)² ÷ tiles, x100 — median vein {vein} of {veins.Count}, median blob {blob} of {blobs.Count}");
        Assert.True(vein >= 300, $"veins are not long: median {vein}");
        Assert.True(blob <= 200, $"blobs are not round: median {blob}");
    }

    /// <summary>
    /// The valley-wide seams take ground only where they lie: every tree without them is a tree with them or a tile
    /// they took, and no tree appears that was not there. The river and the founding are untouched.
    /// </summary>
    [Theory]
    [InlineData(1UL)]
    [InlineData(7UL)]
    [InlineData(2024UL)]
    [InlineData(12345UL)]
    public void TheScatteredSeamsMovedNoForest(ulong seed)
    {
        SimConfig with = Shipped with { Seed = seed };
        SimConfig without = with with { ScatteredStoneSeams = 0, ScatteredIronSeams = 0 };
        Assert.True(with.ScatteredStoneSeams > 0 && with.ScatteredIronSeams > 0, "Nothing to compare.");

        GeneratedMap a = MapGenerator.Generate(without, seed);
        GeneratedMap b = MapGenerator.Generate(with, seed);
        int took = 0, woods = 0;
        for (int i = 0; i < a.Tiles.Count; i++)
        {
            Terrain before = a.Tiles[i], after = b.Tiles[i];
            woods += before == Terrain.Forest ? 1 : 0;
            if (before == Terrain.Forest && after != Terrain.Forest)
            {
                Assert.True(after is Terrain.Rock or Terrain.IronDeposit, $"Tile {i}: forest became {after}.");
                took++;
            }

            if (after == Terrain.Forest)
            {
                Assert.Equal(Terrain.Forest, before);
            }

            if (before == Terrain.Water || after == Terrain.Water)
            {
                Assert.Equal(before, after);
            }
        }

        _output.WriteLine($"seed {seed}: the scattered seams took {took} of {woods} wooded tiles ({100.0 * took / woods:0.0} %)");
        Assert.Equal(a.FoundingSite, b.FoundingSite);
    }

    /// <summary>The longest straight distance across a footprint, squared — integers, no root (D2).</summary>
    private static int SpanSquared(IReadOnlyList<GridPos> tiles)
    {
        int best = 0;
        for (int i = 0; i < tiles.Count; i++)
        {
            for (int j = i + 1; j < tiles.Count; j++)
            {
                int dx = tiles[i].X - tiles[j].X, dy = tiles[i].Y - tiles[j].Y;
                best = Math.Max(best, (dx * dx) + (dy * dy));
            }
        }

        return best;
    }
}

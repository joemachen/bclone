using Bclone.Sim.Config;
using Bclone.Sim.World;
using Xunit;
using Xunit.Abstractions;

namespace Bclone.Sim.Tests;

/// <summary>
/// Real heights — <c>specs/heights.md</c> (D734, D735; Joe: <i>"Uphill is slower"</i>, then <i>"Any slope and as you
/// recommend"</i>). §10's guards.
/// </summary>
public sealed class HeightsTests
{
    private readonly ITestOutputHelper _output;

    public HeightsTests(ITestOutputHelper output) => _output = output;

    private static SimConfig Shipped => ShippedConfig.Load();

    private static readonly ulong[] Seeds = { 1UL, 2UL, 7UL, 42UL, 1234UL, 98765UL };

    #region Step 1: the layer, the stage and the slope classes (guards 1–4)

    [Fact]
    public void TheSameSeedGivesTheSameHeightsAndAnotherSeedOthers()
    {
        GeneratedMap a = MapGenerator.Generate(Shipped, 42UL);
        GeneratedMap b = MapGenerator.Generate(Shipped, 42UL);
        GeneratedMap c = MapGenerator.Generate(Shipped, 43UL);

        Assert.Equal(a.Heights, b.Heights);
        Assert.NotEqual(a.Heights, c.Heights);

        // Anti-vacuity: a valley of hills, not a valley of zeros.
        Assert.True(a.Heights.Distinct().Count() >= 4, $"only {a.Heights.Distinct().Count()} levels in the valley");

        // ⚠️ And the hills themselves follow the seed, not only the river they ramp down to (the red-check's zero: with
        // the stage's own seed pinned, two seeds still differed by their rivers — and then by their founding rings). With
        // no river and no ring, only the hills are left.
        SimConfig riverless = Shipped with { RiverWidthTiles = 0, RiverWidthWanderTiles = 0, HeightFoundingFlatTiles = 0 };
        Assert.NotEqual(MapGenerator.Generate(riverless, 42UL).Heights, MapGenerator.Generate(riverless, 43UL).Heights);
    }

    [Fact]
    public void TheHeightsStageMovesNoOtherStage()
    {
        foreach (ulong seed in Seeds)
        {
            GeneratedMap hilly = MapGenerator.Generate(Shipped, seed);
            GeneratedMap flat = MapGenerator.Generate(Shipped with { HeightLevels = 1 }, seed);

            Assert.Equal(flat.FoundingSite, hilly.FoundingSite);
            Assert.Equal(flat.Tiles, hilly.Tiles);
            Assert.All(flat.Heights, h => Assert.Equal(0, h));
        }
    }

    [Fact]
    public void WaterIsLevelZeroItsBanksAtMostOneAndTheFoundingRingFlatOrGentle()
    {
        foreach (string hills in new[] { "50", "100", "200" })
        {
            SimConfig config = Shipped with { HillScalePercent = int.Parse(hills, System.Globalization.CultureInfo.InvariantCulture) };
            foreach (ulong seed in Seeds)
            {
                GeneratedMap map = MapGenerator.Generate(config, seed);
                int ring = config.HeightFoundingFlatTiles;
                for (int y = map.MinY; y < map.MinY + map.Height; y++)
                {
                    for (int x = map.MinX; x < map.MinX + map.Width; x++)
                    {
                        var tile = new GridPos(x, y);
                        int h = map.HeightAt(tile);
                        if (map.TerrainAt(tile) == Terrain.Water)
                        {
                            Assert.True(h == 0, $"hills {hills}, seed {seed}: water at {tile} is level {h}");
                        }
                        else if (BesideWater(map, tile))
                        {
                            Assert.True(h <= 1, $"hills {hills}, seed {seed}: the bank at {tile} is level {h}");
                        }

                        int fromFounding = Math.Max(Math.Abs(x - map.FoundingSite.X), Math.Abs(y - map.FoundingSite.Y));
                        if (fromFounding <= ring)
                        {
                            Assert.True(map.SteepnessAt(tile) <= 1,
                                $"hills {hills}, seed {seed}: {tile}, {fromFounding} from the founding, has steepness {map.SteepnessAt(tile)}");
                        }

                        Assert.InRange(h, 0, config.HeightLevels - 1);
                    }
                }
            }
        }
    }

    [Fact]
    public void HillierValleysAreSteeper()
    {
        // The Hilliness row's three settings, in order — and a Hilly valley has steep ground at all.
        var steep = new List<int>();
        foreach (int percent in new[] { 50, 100, 200 })
        {
            int count = 0;
            foreach (ulong seed in Seeds)
            {
                GeneratedMap map = MapGenerator.Generate(Shipped with { HillScalePercent = percent }, seed);
                count += map.Tiles.Select((_, i) => i).Count(i => map.SteepnessAtIndex(i) >= 2);
            }

            steep.Add(count);
        }

        _output.WriteLine($"steep tiles over {Seeds.Length} seeds: gentle {steep[0]}, rolling {steep[1]}, hilly {steep[2]}");
        Assert.True(steep[0] <= steep[1] && steep[1] < steep[2], string.Join(" / ", steep));
        Assert.True(steep[2] > 0);
    }

    [Theory]
    [InlineData(0, Slope.Flat)]
    [InlineData(1, Slope.Gentle)]
    [InlineData(2, Slope.Steep)]
    [InlineData(5, Slope.Steep)]
    public void TheSlopeClassesSitAtTheirThresholds(int steepness, Slope expected) =>
        Assert.Equal(expected, Slopes.ClassOf(steepness, gentleAt: 1, steepAt: 2));

    [Fact]
    public void SteepnessIsTheLargestStepToAFourNeighbour()
    {
        // Posed by hand: a 3 × 3 valley, the centre at 2, its four neighbours 2, 1, 4 and 2 — and its diagonals 9,
        // which a four-neighbour step never reaches, so the centre is 2 (to the 4), not 7.
        // ⚠️ The bottom-left corner is 1, so the left edge's 8 can come only from the step UP (the red-check's zero: with a
        // 9 below as well, dropping the up-neighbour still found an 8 going down).
        byte[] heights =
        {
            9, 2, 9,
            1, 2, 4,
            1, 2, 9,
        };
        var terrain = new Terrain[9];
        var map = new GeneratedMap(3, 3, 0, 0, terrain, new GridPos(1, 1), heights);

        Assert.Equal(2, map.SteepnessAt(new GridPos(1, 1)));

        // The left edge's middle (1): up to 9 is 8, and the side off the map counts for nothing.
        Assert.Equal(8, map.SteepnessAt(new GridPos(0, 1)));
    }

    private static bool BesideWater(GeneratedMap map, GridPos tile) =>
        map.TerrainAt(new GridPos(tile.X + 1, tile.Y)) == Terrain.Water && map.Contains(new GridPos(tile.X + 1, tile.Y))
        || map.TerrainAt(new GridPos(tile.X - 1, tile.Y)) == Terrain.Water && map.Contains(new GridPos(tile.X - 1, tile.Y))
        || map.TerrainAt(new GridPos(tile.X, tile.Y + 1)) == Terrain.Water && map.Contains(new GridPos(tile.X, tile.Y + 1))
        || map.TerrainAt(new GridPos(tile.X, tile.Y - 1)) == Terrain.Water && map.Contains(new GridPos(tile.X, tile.Y - 1));

    #endregion
}

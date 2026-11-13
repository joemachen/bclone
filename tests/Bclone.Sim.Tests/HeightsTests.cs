using Bclone.Sim.Config;
using Bclone.Sim.Core;
using Bclone.Sim.Logging;
using Bclone.Sim.Systems;
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

    #endregion

    #region Step 2: the slope in the one cost field (guards 5–9)

    /// <summary>A one-row valley, its heights given: 0 0 1 3 3 3 — flat, gentle, steep, steep, flat, flat.</summary>
    private static GeneratedMap Strip()
    {
        var terrain = new Terrain[6];
        return new GeneratedMap(6, 1, 0, 0, terrain, new GridPos(0, 0), new byte[] { 0, 0, 1, 3, 3, 3 });
    }

    private static TravelCostField SlopedField(GeneratedMap map, out PathWear wear)
    {
        wear = new PathWear(map);
        var field = new TravelCostField(1, map);
        field.ReadWearFrom(wear, wornAt: 12, packedAt: 40, wornCost: 9, packedCost: 8);
        field.ReadSlope(gentleAt: 1, steepAt: 2, gentlePercent: 120, steepPercent: 150);
        return field;
    }

    [Fact]
    public void EachSlopeClassCostsItsShareAndWearMultipliesWithIt()
    {
        GeneratedMap map = Strip();
        TravelCostField field = SlopedField(map, out PathWear wear);

        // Steepness 0 1 2 2 0 0 → flat 10, gentle 12, steep 15 (`heights.md §4`).
        Assert.Equal(new[] { 10, 12, 15, 15, 10, 10 }, Enumerable.Range(0, 6).Select(x => field.CostToEnter(new GridPos(x, 0))));

        // Worn (9) on steep: 9 × 150 % = 13.5, half up → 14; packed (8) on steep: 12; packed on the flat: 8, unchanged.
        wear.Tread(new GridPos(2, 0), 20);
        wear.Tread(new GridPos(3, 0), 60);
        wear.Tread(new GridPos(5, 0), 60);
        wear.Decay(0);
        Assert.Equal(14, field.CostToEnter(new GridPos(2, 0)));
        Assert.Equal(12, field.CostToEnter(new GridPos(3, 0)));
        Assert.Equal(8, field.CostToEnter(new GridPos(5, 0)));
    }

    [Fact]
    public void TheFieldCarriesASteepStepAndPricesTheWalkAcrossIt()
    {
        // ⛔ Before D735 the field's bucket ring held steps up to 10 and threw on a 15 (`heights.md §4.1`).
        GeneratedMap map = Strip();
        TravelCostField field = SlopedField(map, out _);

        // A walk is priced by the tiles it leaves: 0→5 leaves tiles 0–4, 10 + 12 + 15 + 15 + 10.
        Assert.Equal(62, field.Cost(new GridPos(0, 0), new GridPos(5, 0)));

        // ⭐ Any slope, both ways (Joe's call 2): back again leaves tiles 5–1, 10 + 10 + 15 + 15 + 12 — the same.
        Assert.Equal(62, field.Cost(new GridPos(5, 0), new GridPos(0, 0)));

        // And unsloped, the same strip is five grass tiles.
        var flat = new TravelCostField(1, map);
        Assert.Equal(50, flat.Cost(new GridPos(0, 0), new GridPos(5, 0)));
    }

    [Fact]
    public void AHillCostsTheSameEitherWayOnARealValley()
    {
        SimWorld world = SimFactory.CreatePhase0(Shipped with { HillScalePercent = 200 }, new InMemoryLogSink()).World;
        GridPos founding = world.Map.FoundingSite;
        var far = new GridPos(founding.X + 30, founding.Y + 20);
        int there = world.TravelCost.Cost(founding, far);
        int back = world.TravelCost.Cost(far, founding);
        _output.WriteLine($"founding → 30, 20 off: {there}; back: {back}");
        Assert.NotEqual(TravelCostField.Unreachable, there);

        // To within the two end tiles: the walk there pays for the first and not the last, the walk back the other way.
        int ends = world.TravelCost.CostToEnter(founding) + world.TravelCost.CostToEnter(far);
        Assert.True(Math.Abs(there - back) <= ends, $"there {there}, back {back}, the ends {ends}");
    }

    [Fact]
    public void SteepGroundIsWalkedAtTwoThirdsThePaceAndACartIsStillSlowOnItsGrass()
    {
        SimConfig config = Shipped with { HillScalePercent = 200 };
        SimWorld world = SimFactory.CreatePhase0(config, new InMemoryLogSink()).World;
        List<GridPos> steep = OpenTiles(world, Slope.Steep, 4);
        List<GridPos> flat = OpenTiles(world, Slope.Flat, 4);
        Assert.Equal(4, steep.Count);
        Assert.Equal(4, flat.Count);

        // Guard 7: the clock (`LegTicks` = length × WalkedOver ⁄ steps × 10) charges a steep tile half again a flat one.
        (int onSteep, int steepGrass) = BehaviorSystem.WalkedOver(world, steep, steep.Count - 1);
        (int onFlat, _) = BehaviorSystem.WalkedOver(world, flat, flat.Count - 1);
        Assert.Equal(onFlat * 3, onSteep * 2);

        // Guard 8 (`heights.md §4.1.3`): unworn steep ground is still GRASS — counted as grass, and the cart slow on it.
        Assert.Equal(steep.Count, steepGrass);
        (int pushedSteep, _) = BehaviorSystem.WalkedOver(world, steep, steep.Count - 1, pushing: true);
        Assert.True(pushedSteep > onSteep, $"a cart over unworn steep ground costs {pushedSteep}, on foot {onSteep} — not slower");

        // …and a worn steep path is not grass: the cart is as quick as the walker on it.
        foreach (GridPos t in steep)
        {
            world.Paths.Tread(t, config.PathPackedAt + 5);
        }

        world.Paths.Decay(0);
        (int footPacked, int packedGrass) = BehaviorSystem.WalkedOver(world, steep, steep.Count - 1);
        (int cartPacked, _) = BehaviorSystem.WalkedOver(world, steep, steep.Count - 1, pushing: true);
        Assert.Equal(0, packedGrass);
        Assert.Equal(footPacked, cartPacked);
    }

    /// <summary>Walkable, unbuilt, dry tiles of one slope class, in map order — the first <paramref name="count"/>.</summary>
    private static List<GridPos> OpenTiles(SimWorld world, Slope slope, int count)
    {
        var found = new List<GridPos>();
        GeneratedMap map = world.Map;
        for (int y = map.MinY; y < map.MinY + map.Height && found.Count < count; y++)
        {
            for (int x = map.MinX; x < map.MinX + map.Width && found.Count < count; x++)
            {
                var tile = new GridPos(x, y);
                if (world.IsWalkable(tile) && !world.StandsOn(tile) && map.TerrainAt(tile) != Terrain.Water
                    && Slopes.ClassOf(map.SteepnessAt(tile), world.Config.SlopeGentleAt, world.Config.SlopeSteepAt) == slope)
                {
                    found.Add(tile);
                }
            }
        }

        return found;
    }

    private static bool BesideWater(GeneratedMap map, GridPos tile) =>
        map.TerrainAt(new GridPos(tile.X + 1, tile.Y)) == Terrain.Water && map.Contains(new GridPos(tile.X + 1, tile.Y))
        || map.TerrainAt(new GridPos(tile.X - 1, tile.Y)) == Terrain.Water && map.Contains(new GridPos(tile.X - 1, tile.Y))
        || map.TerrainAt(new GridPos(tile.X, tile.Y + 1)) == Terrain.Water && map.Contains(new GridPos(tile.X, tile.Y + 1))
        || map.TerrainAt(new GridPos(tile.X, tile.Y - 1)) == Terrain.Water && map.Contains(new GridPos(tile.X, tile.Y - 1));

    #endregion
}

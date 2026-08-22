using System;
using System.Collections.Generic;
using Bclone.Sim.Config;
using Bclone.Sim.Core;
using Bclone.Sim.Logging;
using Bclone.Sim.World;
using Xunit;
using Xunit.Abstractions;

namespace Bclone.Sim.Tests;

/// <summary>
/// Per-site yield — <c>specs/per-site-yield.md</c> (D58, D178). Ground that is worth going to,
/// and the guards that say the valley survived being given it.
/// </summary>
/// <remarks>
/// <para>
/// <b>D58's mechanism is two halves and both are budgeted:</b> *distance costs, **and** distant
/// sites pay better.* Per-site yield without the second half is a tax on sprawl that buys
/// nothing — inequality with no reason to accept it.
/// </para>
/// </remarks>
public sealed class PerSiteYieldTests
{
    private readonly ITestOutputHelper _output;

    public PerSiteYieldTests(ITestOutputHelper output) => _output = output;

    /// <summary>A 13-tile diamond field — the ground the economy gives one farmer.</summary>
    private const int FieldTiles = 13;

    private static SimConfig Shipped(ulong seed) => ShippedConfig.Established() with { Seed = seed };

    private static SimWorld World(SimConfig config) =>
        SimFactory.CreatePhase0(config, new InMemoryLogSink()).World;

    // ---------------------------------------------------------------
    //  ⭐⭐ The guard that licenses the map golden to move alone
    // ---------------------------------------------------------------

    /// <summary>
    /// ⭐⭐ Soil became regional and <b>not one byte of any seed's layout moved.</b>
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Draw order is the seed contract</b> (`seeded-map-generation.md §1`), and soil is drawn
    /// at step 5 with **stone and iron at step 6**. So an algorithm that consumed one draw more
    /// or fewer than the per-tile noise it replaced would shift both seams, the woodland and
    /// everything after them, for every seed ever written down — the exact hazard D91 took
    /// explicit care over when it appended the seams in the first place.
    /// </para>
    /// <para>
    /// <b>The value noise consumes no draws</b> (`per-site-yield.md §3.1`): step 5 draws exactly
    /// what it always drew, and the reshaping is deterministic arithmetic afterwards. **These
    /// fingerprints were taken from `main` before the change and are unchanged after it** — so
    /// when the map golden moves, it moves for soil and for nothing else.
    /// </para>
    /// <para>
    /// ⚠️ <b>Soil is deliberately excluded from the fingerprint.</b> Including it would make this
    /// guard say only *"the map changed"*, which is what the map golden already says and is not
    /// the question.
    /// </para>
    /// </remarks>
    [Theory]
    [InlineData(12345UL, 1281079863175304094UL, 240, 2662, 49, 10)]
    [InlineData(2UL, 14483866833134380888UL, 240, 2691, 51, 10)]
    [InlineData(42UL, 15249256015855895832UL, 240, 2696, 45, 9)]
    public void MakingSoilRegionalMovedNoOtherTileInTheValley(
        ulong seed, ulong terrainPrint, int water, int forest, int stone, int iron)
    {
        GeneratedMap map = World(Shipped(seed)).Map;

        ulong actual = 1469598103934665603UL;
        int sawWater = 0;
        int sawForest = 0;
        int sawStone = 0;
        int sawIron = 0;

        for (int i = 0; i < map.Tiles.Count; i++)
        {
            actual = (actual ^ (byte)map.Tiles[i]) * 1099511628211UL;
            switch (map.Tiles[i])
            {
                case Terrain.Water: sawWater++; break;
                case Terrain.Forest: sawForest++; break;
                case Terrain.Rock: sawStone++; break;
                case Terrain.IronDeposit: sawIron++; break;
                default: break;
            }
        }

        _output.WriteLine(
            $"seed {seed}: terrain {actual}, water {sawWater}, forest {sawForest}, "
            + $"stone {sawStone}, iron {sawIron}");

        Assert.Equal(terrainPrint, actual);
        Assert.Equal(water, sawWater);
        Assert.Equal(forest, sawForest);
        Assert.Equal(stone, sawStone);
        Assert.Equal(iron, sawIron);
    }

    // ---------------------------------------------------------------
    //  ⭐ Soil is regional, not noise
    // ---------------------------------------------------------------

    /// <summary>
    /// ⭐ Whole <b>fields</b> differ from each other, which per-tile noise cannot deliver.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>The number that decides this slice is the per-SITE spread, not the per-tile one.</b> A
    /// farmer works thirteen tiles and averages them, so uniform noise gives per-tile variance
    /// and very little per-site variance — which is the one thing per-site yield needs. That is
    /// D67's *seams, not scatter* applied to soil: **scattered soil is texture.**
    /// </para>
    /// <para>
    /// ⚠️ <b>Without this guard the region pass could do nothing and every other guard here would
    /// still pass</b> — the yield would read soil correctly, the layout would be unmoved, and
    /// every site would quietly be worth the same. **It is the anti-vacuity half** (D7).
    /// </para>
    /// <para>
    /// <b>The bar is deliberately well below what was measured.</b> Value noise at scale 8 gives
    /// a p90÷p10 of 182–198% across seeds; asserting 150% leaves room for the soil range to be
    /// tuned (which `soil_quality_min`/`max` are *for*) without this going red for a reason that
    /// is not a regression.
    /// </para>
    /// </remarks>
    [Theory]
    [InlineData(12345UL)]
    [InlineData(2UL)]
    [InlineData(42UL)]
    public void GoodGroundAndPoorGroundAreTellableApart(ulong seed)
    {
        SimConfig config = Shipped(seed);
        GeneratedMap map = World(config).Map;

        List<int> fields = FieldMeans(map);
        fields.Sort();

        int p10 = fields[fields.Count / 10];
        int p90 = fields[fields.Count * 9 / 10];
        int spread = p10 == 0 ? 0 : p90 * 100 / p10;

        _output.WriteLine(
            $"seed {seed}: {fields.Count} fields, p10 {p10}, p90 {p90} — spread {spread}%");

        Assert.True(fields.Count > 50, "Too few candidate fields to say anything.");
        Assert.True(
            spread >= 150,
            $"The best ground is only {spread}% of the worst. Fields are all worth about the "
            + "same, so siting a farm is not a decision — which is the whole slice.");
    }

    /// <summary>
    /// ⛔ The founders settle for safety, so their ground is <b>ordinary at best</b>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Measured before it was written</b> (`per-site-yield.md §3.2`): the founding site is
    /// chosen at step 4 with no knowledge of soil, which makes its quality **uniformly random**
    /// rather than ordinary — and across eight seeds it landed at the 99th, 93rd, 91st and 83rd
    /// percentile in four of them. **Half of all games would have had the valley's best ground on
    /// the doorstep**, which deletes the reason to go anywhere.
    /// </para>
    /// <para>
    /// <b>It is a cap, so it can only ever take away.</b> Ground already below the reference is
    /// untouched — this cannot make a hard seed easier — and the founders' fields therefore yield
    /// <em>at most</em> exactly <c>crop_yield_per_tile</c>.
    /// </para>
    /// </remarks>
    [Theory]
    [InlineData(12345UL)]
    [InlineData(2UL)]
    [InlineData(42UL)]
    public void TheGroundTheFoundersSettleIsNeverTheBestInTheValley(ulong seed)
    {
        SimConfig config = Shipped(seed);
        GeneratedMap map = World(config).Map;

        int reference = VillageEconomy.ReferenceSoil(config);
        int worst = int.MaxValue;
        int best = 0;

        int radius = config.FoundingOrdinaryRadiusTiles;
        Assert.True(radius > 0, "The cap is switched off, so this guard proves nothing.");

        for (int dy = -radius; dy <= radius; dy++)
        {
            for (int dx = -radius; dx <= radius; dx++)
            {
                var at = new GridPos(map.FoundingSite.X + dx, map.FoundingSite.Y + dy);
                if (!map.Contains(at))
                {
                    continue;
                }

                int soil = map.SoilAt(at);
                worst = Math.Min(worst, soil);
                best = Math.Max(best, soil);
            }
        }

        _output.WriteLine(
            $"seed {seed}: founding ground {worst}-{best} against a reference of {reference}");

        Assert.True(
            best <= reference,
            $"The founders settled on ground worth {best} against a reference of {reference} — "
            + "the best ground in the valley is on their doorstep, and there is no longer any "
            + "reason to go anywhere.");
    }

    // ---------------------------------------------------------------
    //  ⭐ What the ground is worth
    // ---------------------------------------------------------------

    /// <summary>
    /// ⭐⭐ Average ground yields <b>exactly</b> what it yielded before this slice existed.
    /// </summary>
    /// <remarks>
    /// <b>This is what lets soil matter without re-deriving anything.</b>
    /// <c>crop_yield_per_tile</c> is on Joe's locked list, so soil is a multiplier *around*
    /// <see cref="VillageEconomy.ReferenceSoil"/> rather than a replacement — and the locked
    /// number acquires a precise meaning it never had: **it is the yield on average ground.**
    /// The same device `skills-catalog.md §3.2` uses one system over, for the same reason: a
    /// multiplier that averages to one leaves every derived number standing.
    /// </remarks>
    [Fact]
    public void AverageGroundYieldsExactlyTheNumberInTheConfig()
    {
        SimConfig config = Shipped(12345UL);
        SimWorld world = World(config);

        int reference = VillageEconomy.ReferenceSoil(config);
        GridPos average = FindGroundWorth(world, reference);

        _output.WriteLine(
            $"reference soil {reference}; tile {average} is worth {world.Map.SoilAt(average)} "
            + $"and yields {world.CropYieldAt(average)} against a config of "
            + $"{config.CropYieldPerTile}");

        Assert.Equal(config.CropYieldPerTile, world.CropYieldAt(average));
    }

    /// <summary>⭐ Rich ground is worth materially more than thin ground.</summary>
    /// <remarks>
    /// <b>The half that makes distance worth paying</b> (D58): *"distance costs, and distant
    /// sites pay better… the frontier homestead beside a rich patch, isolated and eating well
    /// for it."* Without this the slice is a tax on sprawl.
    /// </remarks>
    [Fact]
    public void RichGroundIsWorthMoreThanThinGround()
    {
        SimConfig config = Shipped(12345UL);
        SimWorld world = World(config);

        GridPos richest = default;
        GridPos thinnest = default;
        int best = -1;
        int worst = int.MaxValue;

        for (int y = config.MapMinY; y < config.MapMinY + config.MapHeight; y++)
        {
            for (int x = config.MapMinX; x < config.MapMinX + config.MapWidth; x++)
            {
                var at = new GridPos(x, y);
                int soil = world.Map.SoilAt(at);

                if (soil > best)
                {
                    best = soil;
                    richest = at;
                }

                if (soil < worst)
                {
                    worst = soil;
                    thinnest = at;
                }
            }
        }

        int rich = world.CropYieldAt(richest);
        int thin = world.CropYieldAt(thinnest);

        _output.WriteLine(
            $"richest {richest} soil {best} yields {rich}; thinnest {thinnest} soil {worst} "
            + $"yields {thin}");

        Assert.True(rich > thin * 3 / 2, $"Rich ground yields {rich} against thin ground's {thin}.");
        Assert.True(thin >= 1, "Thin ground yields nothing at all — poor is not barren.");
    }

    // ---------------------------------------------------------------

    private static List<int> FieldMeans(GeneratedMap map)
    {
        var means = new List<int>();

        for (int y = (map.Height / -2) + 3; y < (map.Height / 2) - 3; y += 4)
        {
            for (int x = (map.Width / -2) + 3; x < (map.Width / 2) - 3; x += 4)
            {
                int mean = FieldMeanAt(map, new GridPos(x, y));
                if (mean > 0)
                {
                    means.Add(mean);
                }
            }
        }

        return means;
    }

    private static int FieldMeanAt(GeneratedMap map, GridPos centre)
    {
        int total = 0;
        int seen = 0;

        for (int dy = -2; dy <= 2; dy++)
        {
            for (int dx = -2; dx <= 2; dx++)
            {
                if (Math.Abs(dx) + Math.Abs(dy) > 2)
                {
                    continue;
                }

                var at = new GridPos(centre.X + dx, centre.Y + dy);
                if (!map.Contains(at) || map.TerrainAt(at) == Terrain.Water)
                {
                    continue;
                }

                total += map.SoilAt(at);
                seen++;
            }
        }

        return seen == FieldTiles ? total / seen : 0;
    }

    private static GridPos FindGroundWorth(SimWorld world, int soil)
    {
        SimConfig config = world.Config;

        for (int y = config.MapMinY; y < config.MapMinY + config.MapHeight; y++)
        {
            for (int x = config.MapMinX; x < config.MapMinX + config.MapWidth; x++)
            {
                var at = new GridPos(x, y);
                if (world.Map.SoilAt(at) == soil)
                {
                    return at;
                }
            }
        }

        throw new Xunit.Sdk.XunitException($"No tile in the valley is worth exactly {soil}.");
    }
}

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

    // ---------------------------------------------------------------
    //  ⭐ The probe behind the words the player reads
    // ---------------------------------------------------------------

    /// <summary>
    /// ⭐ What the valley's soil actually looks like from the player's side — the spread the
    /// overlay has to render and the bands the panels have to name.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Joe, walking the shipped build: <em>"I can't really tell which areas are good or
    /// bad."</em></b> Half of that was a broken button — the ground toggle's label was written
    /// in the *routes* button's handler, so it read <em>"Ground: off"</em> whatever the overlay
    /// was doing. The other half is this: <b>how strong is the wash on a typical tile, and where
    /// do <em>rich</em>, <em>ordinary</em> and <em>thin</em> actually fall?</b>
    /// </para>
    /// <para>
    /// <b>⚠️ MEASURED RATHER THAN REASONED, because the reasoning was available and would have
    /// been wrong to trust.</b> Soil is drawn uniform in <c>[soil_quality_min,
    /// soil_quality_max]</c> and then bilinearly interpolated by <c>MakeSoilRegional</c> —
    /// lattice points keep the full drawn amplitude while every tile between them is a blend of
    /// four draws, which regresses toward the middle. That predicts a faint typical tile and
    /// strong region cores, and it is exactly the kind of prediction D178's own smoothing probe
    /// killed before a line shipped. The percentiles below come from a run.
    /// </para>
    /// <para>
    /// <b>It asserts the thing the wording depends on</b> rather than only printing: the bands
    /// `Main` names must each contain a real share of the valley, or the panel would call
    /// everything ordinary and say nothing at all.
    /// </para>
    /// </remarks>
    [Fact]
    public void TheValleysSoilSpreadIsWideEnoughToName()
    {
        SimConfig config = Shipped(12345UL);
        SimWorld world = World(config);

        var shares = new List<int>();
        for (int y = config.MapMinY; y < config.MapMinY + config.MapHeight; y++)
        {
            for (int x = config.MapMinX; x < config.MapMinX + config.MapWidth; x++)
            {
                var at = new GridPos(x, y);
                if (world.Map.TerrainAt(at) != Terrain.Water)
                {
                    shares.Add(world.SoilShareAt(at));
                }
            }
        }

        shares.Sort();

        int Percentile(int p) => shares[Math.Min(shares.Count - 1, shares.Count * p / 100)];

        int p05 = Percentile(5);
        int p10 = Percentile(10);
        int p50 = Percentile(50);
        int p90 = Percentile(90);
        int p95 = Percentile(95);

        // What `DrawSoil` makes of the same tile: alpha = 0.55 × |share − 100| / 100, in
        // percent so this stays integer.
        int AlphaAt(int share) => 55 * Math.Abs(share - 100) / 100;

        _output.WriteLine($"{shares.Count} dry tiles at seed 12345");
        _output.WriteLine(
            $"share of ordinary: p05 {p05}%  p10 {p10}%  p50 {p50}%  p90 {p90}%  p95 {p95}%"
            + $"  (min {shares[0]}%, max {shares[^1]}%)");
        _output.WriteLine(
            $"overlay alpha: p10 {AlphaAt(p10)}%  p50 {AlphaAt(p50)}%  p90 {AlphaAt(p90)}%"
            + $"  max {Math.Max(AlphaAt(shares[0]), AlphaAt(shares[^1]))}%");

        int rich = shares.FindAll(share => share >= RichAt).Count * 100 / shares.Count;
        int thin = shares.FindAll(share => share <= ThinAt).Count * 100 / shares.Count;
        _output.WriteLine(
            $"bands: rich (>={RichAt}%) {rich}% of the valley; thin (<={ThinAt}%) {thin}%; "
            + $"ordinary {100 - rich - thin}%");

        // ⭐ Each band has to be somewhere the player can actually walk to. A band holding
        // 1% of the valley is a word nobody ever reads, and one holding 90% is a word that
        // says nothing — which is the failure the sentence exists to avoid.
        Assert.InRange(rich, 5, 45);
        Assert.InRange(thin, 5, 45);
    }

    /// <summary>
    /// ⭐ A farm reports <b>the ground it works</b>, not the tile its farmhouse stands on.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>This is the guard for the shortcut that would have been easier to write.</b> Sampling
    /// <see cref="SimWorld.SoilShareAt"/> at <c>farm.Position</c> is one line and would pass any
    /// test that only asked *"does the panel say a number?"*. It would also be wrong: soil is
    /// regional at lattice 8 (`per-site-yield.md §3.1`) and a farm's ground reaches well past
    /// one lattice cell, so the doorstep is a sample of one region and the field can straddle
    /// two. The player would be told their farm was rich while most of it was thin.
    /// </para>
    /// <para>
    /// <b>Both halves are asserted</b> — that the number is the average of the field, and that
    /// the doorstep would have given a different one. Without the second half this guard is
    /// green against the shortcut it exists to catch, which is the failure D157 has now found
    /// three times.
    /// </para>
    /// </remarks>
    [Fact]
    public void AFarmReportsTheGroundItWorksNotItsDoorstep()
    {
        SimConfig config = Shipped(12345UL);
        SimWorld world = World(config);

        Workplace farm = FarmFixtures.RaiseAFarm(world);
        int given = FarmFixtures.GiveItGround(world, farm, reach: 4);
        Assert.True(given > 1, $"The fixture gave the farm {given} tiles; it needs several.");

        IReadOnlyList<int> owned = world.Zones.WorkGroundOf(farm.Id);
        int total = 0;
        for (int i = 0; i < owned.Count; i++)
        {
            total += world.SoilShareAt(world.Zones.PositionOf(owned[i]));
        }

        int doorstep = world.SoilShareAt(farm.Position);
        int reported = world.FarmGroundShare(farm);

        _output.WriteLine(
            $"farm at {farm.Position} holds {owned.Count} tiles; its ground averages "
            + $"{reported}% of ordinary, while the tile it stands on is worth {doorstep}%");

        Assert.Equal(total / owned.Count, reported);
        Assert.NotEqual(doorstep, reported);
    }

    /// <summary>A farm given no ground quotes no percentage at all.</summary>
    /// <remarks>
    /// The panel already tells a groundless farm what to do about it, and a second sentence
    /// quoting the soil of a field that does not exist would be two instructions where one
    /// will do — and one of them about nothing.
    /// </remarks>
    [Fact]
    public void AFarmWithNoGroundSaysNothingAboutSoil()
    {
        SimWorld world = World(Shipped(12345UL));
        Workplace farm = FarmFixtures.RaiseAFarm(world);

        Assert.Equal(0, world.Zones.WorkGroundTiles(farm.Id));
        Assert.Equal(0, world.FarmGroundShare(farm));
    }

    /// <summary>Where <c>Main</c> starts calling ground rich, as a share of ordinary.</summary>
    /// <remarks>
    /// ⚠️ <b>These two constants are duplicated in the view on purpose and must not drift.</b>
    /// `Bclone.Game` is deliberately outside `bclone.sln` (D11), so the tests cannot reference
    /// it — this guard is the only thing that can say the bands the panels name are bands the
    /// valley contains. If `Main.DescribeSoil` moves them, move them here.
    /// </remarks>
    private const int RichAt = 115;

    /// <summary>Where <c>Main</c> starts calling ground thin. See <see cref="RichAt"/>.</summary>
    private const int ThinAt = 85;

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

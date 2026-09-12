using Bclone.Sim.Config;
using Bclone.Sim.Core;
using Bclone.Sim.Determinism;
using Bclone.Sim.Logging;
using Bclone.Sim.World;
using Xunit;
using Xunit.Abstractions;

namespace Bclone.Sim.Tests;

/// <summary>
/// ⭐⭐ Desire paths — <b>the ground remembers where people walk, and a worn path is cheaper</b>
/// (`DESIGN.md §2.6`, `specs/desire-paths.md`, D358).
/// </summary>
/// <remarks>
/// <para>
/// The pillar waited from Phase 1 for exactly the two things slices 3 and 4 delivered: people at
/// points, walking straight lines — so a trail is worn where they actually go. These guard the
/// four rules: every step treads the tile under it; the season fades every tile and is the ONLY
/// moment wear reaches the routes; a worn tile is cheaper and the field is still exact; and a
/// walk over worn ground takes fewer ticks — the first deliberate change to the walk's clock,
/// Joe's (*"paths should be cheaper / should increase speed"*).
/// </para>
/// <para>
/// ⚠️ <b>§2.6's two failure modes are guarded by name</b>: *no paths* (a lone forager must not scar
/// the map, daily churn must) and *lock-in* (the discount is capped so a worn detour a quarter
/// longer than the straight walk is still slower).
/// </para>
/// </remarks>
public sealed class DesirePathTests
{
    private readonly ITestOutputHelper _output;

    public DesirePathTests(ITestOutputHelper output) => _output = output;

    private static SimConfig Config => VillageFixtures.Village;

    /// <summary>Rows top-down as they read on the page; row 0 of the array is the lowest y.</summary>
    private static GeneratedMap Map(params string[] rowsTopDown)
    {
        int height = rowsTopDown.Length;
        int width = rowsTopDown[0].Length;
        var terrain = new Terrain[width * height];
        for (int row = 0; row < height; row++)
        {
            string line = rowsTopDown[height - 1 - row];
            for (int x = 0; x < width; x++)
            {
                terrain[(row * width) + x] = line[x] == '~' ? Terrain.Water : Terrain.Grass;
            }
        }

        return new GeneratedMap(width, height, 0, 0, terrain, new byte[width * height], new GridPos(0, 0));
    }

    // ---------------------------------------------------------------
    //  The substrate
    // ---------------------------------------------------------------

    /// <summary>A footstep adds, a season subtracts to a floor of nothing, and the count follows.</summary>
    [Fact]
    public void TreadingAddsAndTheSeasonFadesToAFloor()
    {
        var wear = new PathWear(Map("....", "....", "...."));
        var tile = new GridPos(2, 1);

        Assert.Equal(0, wear.At(tile));
        Assert.Equal(0, wear.TroddenTiles);

        wear.Tread(tile, 1);
        wear.Tread(tile, 4);
        Assert.Equal(5, wear.At(tile));
        Assert.Equal(1, wear.TroddenTiles);

        int generation = wear.Generation;
        Assert.Equal(1, wear.Decay(3));
        Assert.Equal(2, wear.At(tile));
        Assert.Equal(generation + 1, wear.Generation);

        Assert.Equal(0, wear.Decay(3));
        Assert.Equal(0, wear.At(tile));
        Assert.Equal(0, wear.TroddenTiles);

        // Off the map is nothing, and stays nothing.
        wear.Tread(new GridPos(40, 40), 1);
        Assert.Equal(0, wear.At(new GridPos(40, 40)));
    }

    /// <summary>⛔ Wear saturates rather than wrapping — the most-walked tile can never turn back into fresh grass.</summary>
    [Fact]
    public void WearSaturatesAndNeverWraps()
    {
        var wear = new PathWear(Map("..", ".."));
        var tile = new GridPos(0, 0);
        wear.Tread(tile, ushort.MaxValue - 2);
        wear.Tread(tile, 10);
        Assert.Equal(ushort.MaxValue, wear.At(tile));
    }

    // ---------------------------------------------------------------
    //  The field
    // ---------------------------------------------------------------

    /// <summary>
    /// ⛔⛔ On uniform ground the Dijkstra and D179's breadth-first sweep agree BYTE FOR BYTE — the
    /// sweep is only ever used because it is exact, and this is what says so.
    /// </summary>
    [Fact]
    public void OnUniformGroundDijkstraIsTheSweep()
    {
        GeneratedMap map = Map(
            ".......",
            "..~~~..",
            "..~....",
            ".......");
        var to = new GridPos(6, 3);

        TerrainCostField sweep = TerrainCostField.Build(map, to, TravelCostField.BaseTileCost);
        var uniform = new byte[map.Width * map.Height];
        System.Array.Fill(uniform, (byte)TravelCostField.BaseTileCost);
        TerrainCostField dijkstra = TerrainCostField.Build(map, to, TravelCostField.BaseTileCost, uniform);

        for (int y = 0; y < map.Height; y++)
        {
            for (int x = 0; x < map.Width; x++)
            {
                var at = new GridPos(x, y);
                Assert.Equal(sweep.CostFrom(at), dijkstra.CostFrom(at));
            }
        }
    }

    /// <summary>
    /// ⭐⭐ A worn lane is cheaper, so the route takes it — and labour catchment, reading the same
    /// field, reaches further along it (§2.6's "critical integration").
    /// </summary>
    /// <remarks>
    /// Two rows from A to B: the bottom row worn to packed (cost 8), the top row grass (10). The
    /// field's cost from A drops with the worn row, and the step from A goes onto it.
    /// </remarks>
    [Fact]
    public void AWornLaneIsCheaperAndTheRouteTakesIt()
    {
        GeneratedMap map = Map(
            "......",
            "......");
        var wear = new PathWear(map);
        var field = new TravelCostField(1, map);
        field.ReadWearFrom(wear, wornAt: 12, packedAt: 40, wornCost: 9, packedCost: 8);

        var a = new GridPos(0, 1);
        var b = new GridPos(5, 1);
        int onGrass = field.Cost(a, b);
        Assert.Equal(5 * TravelCostField.BaseTileCost, onGrass);

        // Pack the bottom row hard, and hand it to the field the way a season would.
        for (int x = 0; x <= 5; x++)
        {
            wear.Tread(new GridPos(x, 0), 60);
        }

        wear.Decay(0);

        int alongThePath = field.Cost(a, b);
        _output.WriteLine($"on grass {onGrass}, with a packed row beside it {alongThePath}");

        // Down one (10), along five packed (5 × 8 = 40), up one (10) = 60 > 50 — the detour does
        // NOT pay, which is §2.6's lock-in cap working. So from a tile ON the row it must:
        var c = new GridPos(0, 0);
        var d = new GridPos(5, 0);
        Assert.Equal(5 * 8, field.Cost(c, d));
        Assert.Equal(new GridPos(1, 0), field.StepToward(c, d));
        Assert.True(field.TicksBetween(c, d) < field.TicksBetween(a, b), "the packed row must be quicker than the grass beside it");
    }

    /// <summary>
    /// ⛔ Wear reaches the routes ONLY when the season hands it over — a footstep never rebuilds a
    /// field, and the hand-over forgets every cached route.
    /// </summary>
    [Fact]
    public void WearReachesTheRoutesOnlyWhenTheSeasonTurns()
    {
        GeneratedMap map = Map("......", "......");
        var wear = new PathWear(map);
        var field = new TravelCostField(1, map);
        field.ReadWearFrom(wear, 12, 40, 9, 8);

        var c = new GridPos(0, 0);
        var d = new GridPos(5, 0);
        Assert.Equal(50, field.Cost(c, d));
        Assert.Equal(1, field.CachedFields);

        for (int x = 0; x <= 5; x++)
        {
            wear.Tread(new GridPos(x, 0), 60);
        }

        // Trodden hard, but the season has not turned: the cached route stands, at grass prices.
        Assert.Equal(50, field.Cost(c, d));
        Assert.Equal(1, field.CachedFields);

        wear.Decay(0);
        Assert.Equal(40, field.Cost(c, d));
    }

    /// <summary>
    /// ⛔ A lane hovering at the threshold does NOT re-price every season — hysteresis, which is
    /// what keeps a settled village from refilling a hundred flow fields four times a year.
    /// </summary>
    /// <remarks>
    /// Measured before it existed (D358): the shipped valley re-priced in 26 of its first 40
    /// seasons. A tile trodden to 15 fades to 11 — under the line of 12, within a decay's worth of
    /// it — and stays a path; it stops being one at 7, a full decay below.
    /// </remarks>
    [Fact]
    public void APathHoveringAtTheLineDoesNotRePriceEverySeason()
    {
        var wear = new PathWear(Map("....", "...."));
        wear.PriceAt(wornAt: 12, packedAt: 40);
        var tile = new GridPos(1, 1);

        wear.Tread(tile, 15);
        wear.Decay(0);
        int priced = wear.RoutesGeneration;

        // 15 → 11: under the line, but not a decay's worth under it. Still a path.
        wear.Decay(4);
        Assert.Equal(priced, wear.RoutesGeneration);

        // 11 → 7: now it has genuinely faded, and the routes are told once.
        wear.Decay(4);
        Assert.Equal(priced + 1, wear.RoutesGeneration);

        // And a season on bare ground tells them nothing.
        wear.Decay(4);
        Assert.Equal(priced + 1, wear.RoutesGeneration);
    }

    // ---------------------------------------------------------------
    //  The village
    // ---------------------------------------------------------------

    /// <summary>
    /// ⭐⭐ Daily churn wears a path and a lone forager does not — <b>§2.6's two failure modes,
    /// measured</b>.
    /// </summary>
    /// <remarks>
    /// Measured before the thresholds were set (D358): in the shipped valley a busy tile is trodden
    /// 10–18 times a season, the median trodden tile about 5, and the lone Phase 0 walker's tiles
    /// once. Twenty years of the village fixture wear paths through; two hundred seasons of Phase
    /// 0's one villager do not scar a tile.
    /// </remarks>
    [Fact]
    public void DailyChurnWearsAPathAndALoneForagerDoesNot()
    {
        SimConfig config = Config;
        SimLoop village = SimFactory.CreatePhase0(config, new InMemoryLogSink());
        village.Step(config.TicksPerYear * 20);

        int worn = village.World.Paths.TilesAtLeast(config.PathWornAt);
        int packed = village.World.Paths.TilesAtLeast(config.PathPackedAt);
        _output.WriteLine($"village, twenty years: {village.World.Paths.TroddenTiles} trodden tiles, {worn} worn, {packed} packed");

        Assert.True(worn > 0, "twenty years of a village wore no path at all — §2.6's 'no paths' failure");
        Assert.True(village.World.AFirstPathHasWorn, "the village never said its first path had worn");

        SimConfig alone = Phase0Fixtures.Plenty;
        var (lone, _) = Phase0Fixtures.Build(alone);
        lone.Step(alone.TicksPerYear * 20);

        int loneWorn = lone.World.Paths.TilesAtLeast(alone.PathWornAt);
        _output.WriteLine($"one villager, twenty years: {lone.World.Paths.TroddenTiles} trodden tiles, {loneWorn} worn");
        Assert.Equal(0, loneWorn);
    }

    /// <summary>
    /// ⭐ A worn path costs FEWER ticks in the field — the first deliberate change to the walk's
    /// clock since Phase 2, and Joe's (*"paths should be cheaper / should increase speed"*).
    /// </summary>
    /// <remarks>
    /// Posed on a bare map: the same eight-tile row priced on fresh grass and again after its
    /// tiles are packed and handed over. A leg's steps follow this cost (`PlanLeg`), so the walk
    /// shortens with it; the valley pin in `VillagerPointTests` is where that shows in the sim.
    /// </remarks>
    [Fact]
    public void AWornPathCostsFewerTicksInTheField()
    {
        GeneratedMap map = Map("........", "........");
        var wear = new PathWear(map);
        var field = new TravelCostField(1, map);
        field.ReadWearFrom(wear, 12, 40, 9, 8);

        var c = new GridPos(0, 0);
        var d = new GridPos(7, 0);
        Assert.Equal(7, field.TicksBetween(c, d));

        for (int x = 0; x <= 7; x++)
        {
            wear.Tread(new GridPos(x, 0), 60);
        }

        wear.Decay(0);

        // 7 tiles × 8 = 56 cost = 5.6 ticks → the field truncates to 5; a leg rounds to 6.
        Assert.Equal(56, field.Cost(c, d));
        Assert.True(field.TicksBetween(c, d) < 7);
    }

    /// <summary>The paths are hashed — two worlds whose grass differs by one footstep differ.</summary>
    [Fact]
    public void ThePathsAreHashed()
    {
        SimLoop loop = SimFactory.CreatePhase0(Config, new InMemoryLogSink());
        loop.Step(50);
        ulong before = StateHash.Compute(loop.World);
        loop.World.Paths.Tread(loop.World.Map.FoundingSite, 1);
        Assert.NotEqual(before, StateHash.Compute(loop.World));
    }
}

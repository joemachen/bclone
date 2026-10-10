using Bclone.Sim.Config;
using Bclone.Sim.Core;
using Bclone.Sim.Determinism;
using Bclone.Sim.Logging;
using Bclone.Sim.Systems;
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

        return new GeneratedMap(width, height, 0, 0, terrain, new GridPos(0, 0));
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

    /// <summary>
    /// ⛔ Wear stops at the ceiling (D414) — a tile walked a thousand times holds no more than one
    /// walked just past packed, because the class is the whole answer and the excess only ever
    /// bought a path that could not fade.
    /// </summary>
    [Fact]
    public void WearStopsAtTheCeiling()
    {
        var wear = new PathWear(Map("..", ".."));
        wear.CapAt(100);
        var tile = new GridPos(1, 1);
        for (int i = 0; i < 1_000; i++)
        {
            wear.Tread(tile, 3);
        }

        Assert.Equal(100, wear.At(tile));
    }

    /// <summary>
    /// ⭐⭐ An abandoned PACKED path fades in years, not centuries (D414) — however busy it once was.
    /// </summary>
    /// <remarks>
    /// Shipped numbers: packed at 100, a grace of 24, decay 4, ceiling 130. A hub tile walked a
    /// thousand times sits at the ceiling; abandoned, it is packed while its wear is 76 or more —
    /// thirteen sweeps — worn until it falls under 6, and grass on the thirty-second: eight years.
    /// ⛔ Before the ceiling the same tile held 3,000 wear and would have stayed packed for 731
    /// seasons; D414 measured hub tiles at 2,000–6,400 in a twenty-five-year village. (The spec's
    /// six seasons of grace is a lane AT the line; a ceiling low enough to give a hub tile that —
    /// 104 — measured 26 people fewer over 42 fifty-year villages, so it is 130.)
    /// </remarks>
    [Fact]
    public void AnAbandonedPackedPathFadesInYearsNotCenturies()
    {
        var wear = new PathWear(Map("..", ".."));
        wear.PriceAt(wornAt: 30, packedAt: 100, holdsFor: 24);
        wear.CapAt(130);
        var tile = new GridPos(0, 0);
        for (int i = 0; i < 1_000; i++)
        {
            wear.Tread(tile, 3);
        }

        wear.Decay(0);
        Assert.Equal(2, wear.ClassAt(tile));

        int packedFor = 0;
        int sweeps = 0;
        while (wear.ClassAt(tile) > 0 && sweeps < 1_000)
        {
            wear.Decay(4);
            sweeps++;
            if (wear.ClassAt(tile) == 2)
            {
                packedFor++;
            }
        }

        _output.WriteLine($"abandoned at the ceiling: packed for {packedFor} more seasons, grass after {sweeps}");
        Assert.Equal(13, packedFor);
        Assert.Equal(32, sweeps);
    }

    [Fact]
    public void TheCeilingAndTheAllowanceAreValidated()
    {
        SimConfig config = Config;
        Assert.Throws<SimConfigException>(() => (config with { PathWearCeiling = config.PathPackedAt + config.PathWearDecayPerSeason - 1 }).Validate());
        Assert.Throws<SimConfigException>(() => (config with { PathWearCeiling = ushort.MaxValue + 1 }).Validate());
        Assert.Throws<SimConfigException>(() => (config with { PathShortcutGrassAllowance = -1 }).Validate());
        (config with { PathWearCeiling = config.PathPackedAt + config.PathWearDecayPerSeason, PathShortcutGrassAllowance = 0 }).Validate();
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
    /// what keeps a settled village from refilling a hundred flow fields four times a year — and
    /// the band is <c>path_holds_for</c>, a dial (D362).
    /// </summary>
    /// <remarks>
    /// Measured before it existed (D358): the shipped valley re-priced in 26 of its first 40
    /// seasons. A tile trodden to 15 fades to 11 — under the line of 12, within the grace of 4 —
    /// and stays a path; it stops being one at 7, past the grace. Classes move every sweep; the
    /// ROUTES are told on a re-pricing sweep only, and only if something changed.
    /// </remarks>
    [Fact]
    public void APathHoveringAtTheLineDoesNotRePriceEverySeason()
    {
        GeneratedMap map = Map("....", "....");
        var wear = new PathWear(map);
        var field = new TravelCostField(1, map);
        field.ReadWearFrom(wear, wornAt: 12, packedAt: 40, wornCost: 9, packedCost: 8, holdsFor: 4);
        var tile = new GridPos(1, 1);

        wear.Tread(tile, 15);
        wear.Decay(0);
        int priced = wear.RoutesGeneration;
        Assert.Equal(1, wear.ClassAt(tile));
        Assert.Equal(9, field.CostToEnter(tile));

        // 15 → 11: under the line, but within the grace. Still a path — on the map AND to walk
        // on, since the routes price the class (D362) — and the routes untouched.
        wear.Decay(4);
        Assert.Equal(1, wear.ClassAt(tile));
        Assert.Equal(9, field.CostToEnter(tile));
        Assert.Equal(priced, wear.RoutesGeneration);

        // 11 → 7: now it has genuinely faded, and the routes are told once.
        wear.Decay(4);
        Assert.Equal(0, wear.ClassAt(tile));
        Assert.Equal(TravelCostField.BaseTileCost, field.CostToEnter(tile));
        Assert.Equal(priced + 1, wear.RoutesGeneration);

        // And a season on bare ground tells them nothing.
        wear.Decay(4);
        Assert.Equal(priced + 1, wear.RoutesGeneration);
    }

    /// <summary>
    /// ⭐ An abandoned path holds for its grace and then goes back to grass <b>as one lane</b>, and
    /// a new lane appears on the map the season it wears through — not the spring after (D362).
    /// </summary>
    /// <remarks>
    /// Joe: *"it should take longer before a pathway even starts to disappear … once it exists, it
    /// should exist for longer before growing back … they should appear sooner."* With decay 6 and
    /// a grace of 24, a lane nobody walks stays a path for four sweeps and is gone on the fifth —
    /// every tile of it together, because they share the grace, not the dots of a lane going tile
    /// by tile. And the class moves on a non-repricing sweep, so the picture sees it at once
    /// while the routes wait for spring.
    /// </remarks>
    [Fact]
    public void AnAbandonedPathHoldsForItsGraceAndThenGoesTogether()
    {
        GeneratedMap map = Map("........", "........");
        var wear = new PathWear(map);
        wear.PriceAt(wornAt: 30, packedAt: 100, holdsFor: 24);

        // A lane worn unevenly — 36 to 50 — as a real one is. The sweep fades before it classes,
        // so a tile has to be six over the line at the turn to be a path that season.
        for (int x = 0; x < 8; x++)
        {
            wear.Tread(new GridPos(x, 0), 36 + (x * 2));
        }

        // A summer sweep (no re-price): the lane is a path on the map at once …
        int routes = wear.RoutesGeneration;
        wear.Decay(6, reprice: false);
        Assert.Equal(8, wear.PathTiles);
        Assert.Equal(routes, wear.RoutesGeneration);

        // … and the routes learn in spring.
        wear.Decay(6, reprice: true);
        Assert.Equal(routes + 1, wear.RoutesGeneration);
        Assert.Equal(8, wear.PathTiles);

        // Nobody walks it. Three more sweeps — the lowest tile falls to 6, twenty-four under the
        // line, the edge of its grace — and every tile is still a path: four seasons abandoned.
        wear.Decay(6);
        wear.Decay(6);
        wear.Decay(6);
        Assert.Equal(8, wear.PathTiles);

        // Then it goes — the whole lane within the three sweeps its unevenness spans, not a dot
        // at a time over a year — and by the eighth sweep nothing is left.
        wear.Decay(6);
        int going = wear.PathTiles;
        wear.Decay(6);
        wear.Decay(6);
        _output.WriteLine($"a season after its grace ran out the lane had {going} of 8 tiles left; three seasons after, {wear.PathTiles}");
        Assert.True(going < 8, "the grace ran out and nothing changed");
        Assert.Equal(0, wear.PathTiles);
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

        // ⚠️ THE LONE WALKER'S DOORSTEP IS CHURN NOW (D385). Every load goes to the store and
        // the larder is fetched back from it, so the one villager treads the tiles between their
        // house and the granary, and the first steps out of their door, three times a trip —
        // daily churn by §2.6's own definition, and it wore six tiles. What must still not scar
        // is the WALK: the far half of the way to the hut and the ring, walked once a trip.
        GridPos door = lone.World.Households[0].Home();
        GridPos hut = lone.World.Workplaces.Find(w => w.Kind == JobKind.Forager)!.Tile;

        // ⚠️ AND THE DOORSTEP IS THE HOUSE'S WHOLE EDGE (D386): a house is two tiles wide and
        // fronts a lane, so the churn round it — the step out, the turn, the step back in — treads
        // the tiles beside either house tile, and one of those can lie a tile nearer the hut
        // than the tile the house is filed under. A tile touching the house is the doorstep.
        List<GridPos> houseTiles = lone.World.HomeFootprintOf(lone.World.Households[0])!.Value.CoveredTiles();
        int loneWorn = 0;
        int wornOnTheWalk = 0;
        for (int y = lone.World.Map.MinY; y < lone.World.Map.MinY + lone.World.Map.Height; y++)
        {
            for (int x = lone.World.Map.MinX; x < lone.World.Map.MinX + lone.World.Map.Width; x++)
            {
                var tile = new GridPos(x, y);
                if (lone.World.Paths.At(tile) < alone.PathWornAt)
                {
                    continue;
                }

                loneWorn++;
                bool onTheDoorstep = false;
                foreach (GridPos mine in houseTiles)
                {
                    onTheDoorstep |= tile.ManhattanDistanceTo(mine) <= 1;
                }

                if (!onTheDoorstep && tile.ManhattanDistanceTo(hut) < tile.ManhattanDistanceTo(door))
                {
                    wornOnTheWalk++;
                    _output.WriteLine($"  worn on the walk: {tile}");
                }
            }
        }

        _output.WriteLine($"one villager, twenty years: {lone.World.Paths.TroddenTiles} trodden tiles, {loneWorn} worn, {wornOnTheWalk} of them nearer the hut than the door");

        // ⚠️ ONE TILE IS NOT A PATH (D386). A house has a door now, on the lane it faces, so the
        // lone walker leaves by the same line every trip where a house dropped on a tile let the
        // route wander a tile either side — and twenty years of twelve trips wore the one tile
        // where that line turns toward the hut. A run of worn tiles is a path; one is a scuff.
        Assert.True(wornOnTheWalk <= 1,
            $"{wornOnTheWalk} worn tiles on a lone forager's walk — one person has worn a path.");
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

    /// <summary>
    /// ⭐⭐ A walker the route puts on a path stays on it — <b>no leg puts more feet on grass than
    /// the route it cuts</b> (D414), asked of every leg the village plans over six years.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The route bends onto a worn lane (on grass every staircase costs the same, so the field
    /// takes any path tile in the walk's rectangle); before D414 the string-pulling threw the bend
    /// away and the walker cut a chord across the grass beside the lane — every walker their own,
    /// and the fan wore Joe's blobs. The route is recomputed here from the leg's own start, which
    /// is the route `PlanLeg` pulled: the field does not move inside a season.
    /// </para>
    /// <para>
    /// ⚠️ Skipped: legs out through a building (D404's exit, not on the route), legs to the route's
    /// first tile (nothing is cut), and legs planned on a tick the season turned (the classes
    /// moved under them).
    /// </para>
    /// </remarks>
    [Fact]
    public void AWalkerKeepsToTheLaneTheRouteTakes()
    {
        SimConfig config = Config;
        SimLoop loop = SimFactory.CreatePhase0(config, new InMemoryLogSink());
        SimWorld world = loop.World;

        var seen = new Dictionary<int, (Point From, Point To)>();
        int legs = 0;
        int overPath = 0;
        int alongPathOnly = 0;
        int cut = 0;
        for (int t = 0; t < config.TicksPerYear * 6; t++)
        {
            int generation = world.Paths.Generation;
            loop.StepOnce();
            if (world.Paths.Generation != generation)
            {
                continue;
            }

            foreach (Villager villager in world.Villagers)
            {
                if (!villager.Alive || villager.LegTicks == Fixed.Zero || seen.GetValueOrDefault(villager.Id) == (villager.LegFrom, villager.LegTo))
                {
                    continue;
                }

                seen[villager.Id] = (villager.LegFrom, villager.LegTo);
                List<GridPos> route = world.TravelCost.RouteFrom(villager.LegFrom.ToTile(), villager.LegTarget);
                // ⚠️ A leg to the route's FIRST tile replaces no route and cuts nothing — the step off
                // a building (D404) is one, and from a door on a 2×2 it is two footsteps to one
                // route tile. The rule judges only a leg pulled past the first tile.
                int end = route.IndexOf(villager.LegTo.ToTile());
                if (end < 1)
                {
                    continue;
                }

                legs++;
                int routeGrass = 0;
                for (int i = 0; i <= end; i++)
                {
                    routeGrass += world.TravelCost.CostToEnter(route[i]) == TravelCostField.BaseTileCost ? 1 : 0;
                }

                if (routeGrass <= end)
                {
                    overPath++;
                    alongPathOnly += routeGrass == 0 ? 1 : 0;
                }

                if (BehaviorSystem.GrassUnder(world, villager.LegFrom, villager.LegTo) > routeGrass + config.PathShortcutGrassAllowance)
                {
                    cut++;
                }
            }
        }

        _output.WriteLine($"six years: {legs} legs checked, {overPath} whose route uses a path ({alongPathOnly} entirely on one), {cut} cut across the grass beside it");
        Assert.True(overPath > 100, $"only {overPath} legs had a path on their route — the guard proves nothing");
        Assert.Equal(0, cut);
    }

    /// <summary>
    /// ⭐⭐ The founding hub wears lanes, not a block (D414) — the outcome Joe asked for, measured
    /// against the same village walking the way it did before.
    /// </summary>
    /// <remarks>
    /// A block tile is one in any fully worn 2×2 — what the view fills as a yard. The same fixture,
    /// the same years, once as shipped and once with the allowance so large any shortcut the eye can
    /// see is taken (the walk before D414). ⚠️ And paths must still exist: §2.6's *no paths* failure
    /// is the other ditch.
    /// <para>
    /// ⚠️ SIX VALLEYS, NOT ONE (D537). On the fixture's valley alone it was a coin: the 4¼-day meal and the
    /// armful of 80 moved it from 16 block tiles against 31 to 24 against 33, over the bar, while ten valleys
    /// in a scratch arm read 0.66 before and 0.60 after — the rule working as well as it did. D470's lesson
    /// again: a one-valley guard is a coin.
    /// </para>
    /// </remarks>
    [Fact]
    public void TheFoundingHubWearsLanesNotABlock()
    {
        int kept = 0, keptPaths = 0, cut = 0, cutPaths = 0;
        foreach (ulong? seed in new ulong?[] { null, 1, 2, 3, 4, 5 })
        {
            SimConfig config = seed is ulong s ? Config with { Seed = s } : Config;
            (int k, int kp) = BlockTilesAfter(config, 15);
            (int c, int cp) = BlockTilesAfter(config with { PathShortcutGrassAllowance = 1_000 }, 15);
            kept += k;
            keptPaths += kp;
            cut += c;
            cutPaths += cp;
        }

        _output.WriteLine($"fifteen years in six valleys: {kept} block tiles of {keptPaths} path tiles keeping to the lanes; {cut} of {cutPaths} cutting corners");
        Assert.True(keptPaths >= 60, $"only {keptPaths} path tiles in six valleys — the villages wore no paths (§2.6)");
        Assert.True(kept * 10 <= cut * BlockBarTenths,
            $"keeping to the lanes left {kept} block tiles against {cut} — not a thinner network");
    }

    /// <summary>The bar for <see cref="TheFoundingHubWearsLanesNotABlock"/>, in tenths of the corner-cutting village's block.</summary>
    private const int BlockBarTenths = 7;

    /// <summary>
    /// ⭐⭐ A commute along a packed lane treads every tile it passes over — <b>no break in the
    /// lane</b> (D424, Joe: *"what's going on with these pathway segments? can they be a smooth
    /// path?"*).
    /// </summary>
    /// <remarks>
    /// <para>
    /// A step on packed ground is 1.25 tiles (clock B, 8 of 10), and a step trod only the tile it
    /// landed on — so one household walking the same steep lane to its store skipped the same rows
    /// every trip, and the lane wore into dashes a column apart (his screenshot). Posed as that:
    /// a steep lane already packed, one villager walking it end to end, and the ground they mark
    /// must be one unbroken 8-connected chain from the first tile to the last.
    /// </para>
    /// <para>
    /// ⚠️ Worn to just past packed and under the ceiling, so a footstep shows as wear; and the
    /// premise asserted — the walk takes fewer steps than it crosses rows, or nothing is skipped.
    /// ⚠️ A village-wide census was tried first and scored ZERO: an untrodden gap in a straight run is
    /// mostly a fence or a building, and a skipped row on a diagonal is not a straight-run gap.
    /// </para>
    /// </remarks>
    [Fact]
    public void ACommuteAlongAPackedLaneLeavesNoBreak()
    {
        SimWorld world = SimFactory.CreatePhase0(Config, new InMemoryLogSink()).World;
        const int across = 3;
        const int down = 9;

        GridPos? start = ClearStretch(world, across, down);
        Assert.True(start is not null, "no clear stretch of ground near the founding to pose a lane on");
        GridPos from = start!.Value;
        var to = new GridPos(from.X + across, from.Y + down);

        // The lane, packed: every tile within a tile of the straight line, worn just past packed.
        var lane = new HashSet<GridPos>();
        LineOfSight.Footprints(Point.CentreOf(from), Point.CentreOf(to), t =>
        {
            for (int dx = -1; dx <= 1; dx++)
            {
                lane.Add(new GridPos(t.X + dx, t.Y));
            }
        });
        lane.Add(from);
        int packed = world.Config.PathPackedAt + 5;
        Assert.True(packed + world.Config.PathWearPerStep <= world.Config.PathWearCeiling, "the pose sits at the ceiling, where a footstep cannot show");
        foreach (GridPos t in lane)
        {
            world.Paths.Tread(t, packed);
        }

        world.Paths.Decay(0);

        Villager walker = world.Villagers.First(v => v.Alive && v.CanWork);
        walker.StandAt(Point.CentreOf(from));
        var before = lane.ToDictionary(t => t, t => world.Paths.At(t));
        int steps = 0;
        while (steps < 100 && !(walker.Tile == to && walker.LegTicks == Fixed.Zero))
        {
            BehaviorSystem.TravelForTest(world, walker, to);
            steps++;
        }

        Assert.Equal(to, walker.Tile);
        var trodden = new HashSet<GridPos>();
        foreach (GridPos t in world.Paths.Tiles.Select((_, i) => world.Paths.PositionOf(i)))
        {
            int was = before.TryGetValue(t, out int w) ? w : 0;
            if (world.Paths.At(t) > was)
            {
                trodden.Add(t);
            }
        }

        // One 8-connected chain from the first tile stepped on to the destination.
        var reached = new HashSet<GridPos>();
        var queue = new Queue<GridPos>(trodden.Where(t => Math.Max(Math.Abs(t.X - from.X), Math.Abs(t.Y - from.Y)) == 1).Take(1));
        foreach (GridPos t in queue) { reached.Add(t); }
        while (queue.Count > 0)
        {
            GridPos t = queue.Dequeue();
            for (int dy = -1; dy <= 1; dy++)
            {
                for (int dx = -1; dx <= 1; dx++)
                {
                    var n = new GridPos(t.X + dx, t.Y + dy);
                    if (trodden.Contains(n) && reached.Add(n))
                    {
                        queue.Enqueue(n);
                    }
                }
            }
        }

        _output.WriteLine($"a lane {across} across and {down} down, packed: {steps} steps, {trodden.Count} tiles trodden, "
            + $"{reached.Count} of them in one chain from the start; the destination {(reached.Contains(to) ? "joined" : "cut off")}");
        Assert.True(steps - 1 < down, $"the walk took {steps} steps for {down} rows — no step is longer than a tile, so nothing could be skipped");
        Assert.Contains(to, reached);
        Assert.Equal(trodden.Count, reached.Count);
    }

    /// <summary>
    /// ⭐ The shortcut rule's count is the grass a walk would TREAD (D414; D424) — on open grass the
    /// tiles it enters, on a packed lane none.
    /// </summary>
    /// <remarks>
    /// ⚠️ Asked directly because the rule's own guard (<see cref="AWalkerKeepsToTheLaneTheRouteTakes"/>)
    /// judges legs with this same function and so agrees with it whatever it says: made to count
    /// nothing, the desire-path tests stayed green (D424's red check, D419's trap).
    /// </remarks>
    [Fact]
    public void GrassUnderCountsTheGrassAWalkWouldTread()
    {
        SimWorld world = SimFactory.CreatePhase0(Config, new InMemoryLogSink()).World;
        GridPos from = ClearStretch(world, 3, 5) ?? throw new InvalidOperationException("no clear ground");
        var to = new GridPos(from.X + 3, from.Y + 5);
        Point a = Point.CentreOf(from) + new Point(Fixed.FromRatio(1, 10), Fixed.FromRatio(-2, 10));
        Point b = Point.CentreOf(to);

        var entered = new List<GridPos>();
        LineOfSight.Footprints(a, b, entered.Add);
        Assert.Equal(3 + 5, entered.Count);
        Assert.Equal(entered.Count, BehaviorSystem.GrassUnder(world, a, b));

        foreach (GridPos t in entered)
        {
            world.Paths.Tread(t, world.Config.PathPackedAt + 5);
        }

        world.Paths.Decay(0);
        Assert.Equal(0, BehaviorSystem.GrassUnder(world, a, b));
    }

    /// <summary>The nearest stretch of open ground to the founding — passable, unbuilt, unfenced — with a tile of margin.</summary>
    private static GridPos? ClearStretch(SimWorld world, int across, int down)
    {
        bool Open(GridPos t) =>
            world.Map.Contains(t) && TerrainRules.IsPassable(world.Map.TerrainAt(t))
            && world.FootprintCovering(t).Count == 0 && world.Zones.WallsOn(t) == 0;
        for (int r = 0; r < 20; r++)
        {
            for (int dy = -r; dy <= r; dy++)
            {
                for (int dx = -r; dx <= r; dx++)
                {
                    var a = new GridPos(world.Map.FoundingSite.X + dx, world.Map.FoundingSite.Y + dy);
                    bool clear = true;
                    for (int y = -1; y <= down + 1 && clear; y++)
                    {
                        for (int x = -1; x <= across + 1 && clear; x++)
                        {
                            clear = Open(new GridPos(a.X + x, a.Y + y));
                        }
                    }

                    if (clear)
                    {
                        return a;
                    }
                }
            }
        }

        return null;
    }

    private static (int Block, int Paths) BlockTilesAfter(SimConfig config, int years)
    {
        SimLoop loop = SimFactory.CreatePhase0(config, new InMemoryLogSink());
        loop.Step(config.TicksPerYear * years);
        SimWorld world = loop.World;
        bool Path(int x, int y) => world.Paths.ClassAt(new GridPos(x, y)) > 0;
        int block = 0;
        int paths = 0;
        for (int y = world.Map.MinY; y < world.Map.MinY + world.Map.Height; y++)
        {
            for (int x = world.Map.MinX; x < world.Map.MinX + world.Map.Width; x++)
            {
                if (!Path(x, y))
                {
                    continue;
                }

                paths++;
                if ((Path(x + 1, y) && Path(x, y + 1) && Path(x + 1, y + 1))
                    || (Path(x - 1, y) && Path(x, y + 1) && Path(x - 1, y + 1))
                    || (Path(x + 1, y) && Path(x, y - 1) && Path(x + 1, y - 1))
                    || (Path(x - 1, y) && Path(x, y - 1) && Path(x - 1, y - 1)))
                {
                    block++;
                }
            }
        }

        return (block, paths);
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

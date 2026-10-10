using Bclone.Sim.Config;
using Bclone.Sim.Core;
using Bclone.Sim.Determinism;
using Bclone.Sim.Logging;
using Bclone.Sim.World;
using Xunit;
using Xunit.Abstractions;

namespace Bclone.Sim.Tests;

/// <summary>
/// Stone and iron are in the ground where you can see them (D67, D84, D90).
/// </summary>
/// <remarks>
/// <para>
/// <b>Seams, never a percentage roll.</b> D67's argument, and it is the legibility
/// non-negotiable rather than taste: <em>"why did we get a gem?"</em> answered by
/// <em>"you were lucky"</em> is not a causal chain a player can act on. You can see a
/// seam, so going after it is a decision.
/// </para>
/// <para>
/// <b>Deposits, so they are finite</b> (D84) — a laborer clears one and the ground is
/// grass. The quarry and the mine that never run out are buildings, and they come later.
/// </para>
/// </remarks>
public sealed class SeamsTests
{
    private readonly ITestOutputHelper _output;

    public SeamsTests(ITestOutputHelper output) => _output = output;

    private static SimWorld Build(ulong seed) =>
        SimFactory.CreatePhase0(
            VillageFixtures.Village with { Seed = seed }, new InMemoryLogSink()).World;

    private static int Count(SimWorld world, Terrain terrain)
    {
        int n = 0;
        for (int i = 0; i < world.Map.Tiles.Count; i++)
        {
            if (world.Map.Tiles[i] == terrain)
            {
                n++;
            }
        }

        return n;
    }

    /// <summary>Every valley has stone and iron in it.</summary>
    [Theory]
    [InlineData(12345UL)]
    [InlineData(7UL)]
    [InlineData(99UL)]
    [InlineData(2024UL)]
    [InlineData(31337UL)]
    public void EveryValleyHasOreInIt(ulong seed)
    {
        SimWorld world = Build(seed);

        int rock = Count(world, Terrain.Rock);
        int iron = Count(world, Terrain.IronDeposit);
        _output.WriteLine($"seed {seed}: {rock} stone, {iron} iron");

        Assert.True(rock > 0, $"Seed {seed} generated a valley with no stone at all.");
        Assert.True(iron > 0, $"Seed {seed} generated a valley with no iron at all.");
        Assert.True(rock > iron, "Stone is meant to be the common one.");
    }

    /// <summary>⭐ Seams never eat the forest, because the fuel economy is derived from it.</summary>
    /// <remarks>
    /// A seam laid over trees would quietly take timber out of the valley, and every
    /// firewood number in the project is derived against how much wood a village can reach.
    /// <b>That would be a balance change hiding inside a worldgen change</b> — the kind that
    /// passes every test and is found two phases later.
    /// </remarks>
    [Theory]
    [InlineData(12345UL)]
    [InlineData(7UL)]
    [InlineData(2024UL)]
    public void SeamsAreLaidOnlyOverOpenGround(ulong seed)
    {
        // ⚠️ BOTH ARMS ARE UNWOODED, AND THAT IS A CORRECTION TO THE MEASUREMENT RATHER THAN
        // TO THE CLAIM (`forests-and-gathering.md`, slice 1). The scattered woodland is drawn
        // AFTER the seams and only over open grass — so turning the seams off leaves more
        // grass for woodland to claim, and the two arms ended up with different forest counts
        // (2637 against 2654) while the thing this guard is about was still perfectly true.
        //
        // Setting coverage to zero in both arms puts the question back the way it was asked:
        // the only forest left is the tree stands, drawn before the seams, and they must be
        // untouched. **The other direction — woodland must not swallow the seams — is
        // `MapGenerationTests.WoodlandDoesNotSwallowTheSeams`.** Between them both orderings
        // are pinned.
        SimConfig config = VillageFixtures.Village with { Seed = seed, ForestCoveragePercent = 0 };

        // The same valley with no seams at all — the forest must be identical.
        SimWorld withOre = SimFactory.CreatePhase0(config, new InMemoryLogSink()).World;
        SimWorld withoutOre = SimFactory.CreatePhase0(
            config with { StoneSeamCount = 0, IronSeamCount = 0 }, new InMemoryLogSink()).World;

        _output.WriteLine(
            $"seed {seed}: forest {Count(withOre, Terrain.Forest)} with ore, "
            + $"{Count(withoutOre, Terrain.Forest)} without");

        Assert.Equal(Count(withoutOre, Terrain.Forest), Count(withOre, Terrain.Forest));
        Assert.Equal(Count(withoutOre, Terrain.Water), Count(withOre, Terrain.Water));
    }

    /// <summary>
    /// ⭐ The seams move no other stage's draws (D473): with no seams at all, the river and the
    /// founding site are where they were and the woods grow where they grew.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Each stage of the valley draws on a stream of its own</b> (`seeded-map-generation.md
    /// §13`). So a valley with its seams taken out keeps its water and its founding site to the
    /// tile, and every wooded tile of the valley with seams is wooded without them too — the
    /// woodland's clumps fall in the same places, and the only trees that differ are the ones
    /// that grow on grass the seams no longer take.
    /// </para>
    /// <para>
    /// It was <c>TheSeamsWereAppendedToTheDrawOrder</c> under one shared stream, where the seams
    /// had to come after everything they must not move. Under per-stage seeds the order is not
    /// what protects the other stages; their own streams are.
    /// </para>
    /// </remarks>
    [Theory]
    [InlineData(12345UL)]
    [InlineData(7UL)]
    [InlineData(2024UL)]
    public void TheSeamsMoveNoOtherStagesDraws(ulong seed)
    {
        SimConfig with = VillageFixtures.Village with { Seed = seed };
        // ⚠️ The valley-wide seams too (D543): left on, they are drawn after the rings on the same stage, so taking the
        // rings away moves them — and a wood then stands where one of them used to, which is the seams' stage, not the
        // woodland's.
        SimConfig without = with with
        {
            StoneSeamCount = 0, IronSeamCount = 0, ExtraStoneSeams = 0, ExtraIronSeams = 0,
            ScatteredStoneSeams = 0, ScatteredIronSeams = 0,
        };

        GeneratedMap withOre = MapGenerator.Generate(with, seed);
        GeneratedMap withoutOre = MapGenerator.Generate(without, seed);

        Assert.Equal(withoutOre.FoundingSite, withOre.FoundingSite);

        int woodsOnRock = 0;
        for (int i = 0; i < withOre.Tiles.Count; i++)
        {
            Terrain ore = withOre.Tiles[i];
            Terrain none = withoutOre.Tiles[i];

            Assert.True(
                (ore == Terrain.Water) == (none == Terrain.Water),
                $"Seed {seed}, tile {i}: the river moved with the seams ({none} → {ore}).");

            if (ore == Terrain.Forest)
            {
                Assert.True(
                    none == Terrain.Forest,
                    $"Seed {seed}, tile {i}: a wood stands here with the seams and not without — "
                    + "the woodland's clumps moved, so the seams drew on its stream.");
            }

            if (ore is Terrain.Rock or Terrain.IronDeposit && none == Terrain.Forest)
            {
                woodsOnRock++;
            }
        }

        _output.WriteLine($"seed {seed}: without the seams, woods grow on {woodsOnRock} tiles they took");
    }

    /// <summary>
    /// ⭐ The seams are not laid on the compass — Joe: <em>"stone and iron nodes look planned and
    /// symmetrical"</em> (D475).
    /// </summary>
    /// <remarks>
    /// <para>
    /// They sat on eight compass slots (E, W, S, N, then the diagonals) with a tile of jitter, so
    /// every one of them was within a few degrees of a compass bearing and the valley read as a
    /// stamped cross. Seams scattered at random fall within ±5° of one of the eight bearings about
    /// one time in four and a half (80° of 360); the bar is fewer than half.
    /// </para>
    /// <para>
    /// Asked of <see cref="MapGenerator.SeamsOf"/> — the generator's own answer — over 24 valleys.
    /// </para>
    /// </remarks>
    [Fact]
    public void TheSeamsAreNotLaidOnTheCompass()
    {
        SimConfig shipped = ShippedConfig.Load();
        int seams = 0;
        int onTheCompass = 0;
        for (ulong seed = 1; seed <= 24; seed++)
        {
            foreach (Terrain kind in new[] { Terrain.Rock, Terrain.IronDeposit })
            {
                foreach (MapGenerator.Seam seam in MapGenerator.SeamsOf(shipped with { Seed = seed }, seed, kind))
                {
                    double bearing = Math.Atan2(seam.Centre.Y, seam.Centre.X) * 180 / Math.PI;
                    double off = Math.Abs(bearing - (Math.Round(bearing / 45) * 45));
                    seams++;
                    if (off <= 5)
                    {
                        onTheCompass++;
                    }
                }
            }
        }

        _output.WriteLine($"{onTheCompass} of {seams} seams within 5° of a compass bearing");
        Assert.True(seams > 0, "No seams, so nothing to read.");
        Assert.True(
            onTheCompass * 2 < seams,
            $"{onTheCompass} of {seams} seams lie on the compass — the valley is a stamped cross again.");
    }

    /// <summary>
    /// ⭐ And still spread (D24): every quarter of the valley round the village holds a stone seam.
    /// </summary>
    /// <remarks>
    /// Scattering must not undo the guarantee the compass slots were for — four seams in one corner
    /// is a resource half the village cannot reach, the layout that starved a village once. Each
    /// seam keeps a sector of its own; the second ring's eight sectors, each swinging at most 18°,
    /// leave no gap as wide as a quarter turn.
    /// </remarks>
    [Fact]
    public void EveryQuarterOfTheValleyHasStone()
    {
        SimConfig shipped = ShippedConfig.Load();
        for (ulong seed = 1; seed <= 50; seed++)
        {
            var quarters = new bool[4];
            foreach (MapGenerator.Seam seam in MapGenerator.SeamsOf(shipped with { Seed = seed }, seed, Terrain.Rock))
            {
                quarters[(seam.Centre.X >= 0 ? 0 : 1) + (seam.Centre.Y >= 0 ? 0 : 2)] = true;
            }

            Assert.True(quarters.All(q => q), $"Seed {seed}: a quarter of the valley has no stone seam.");
        }
    }

    /// <summary>
    /// ⭐ Outcrops, not stamps: a valley's stone seams are not all one shape and size (D475).
    /// </summary>
    /// <remarks>
    /// They were Manhattan diamonds of one radius — thirteen tiles each wherever the river let them
    /// be. Each is now a wobbling outline (the forests' rule) on a size it drew. Read off the map:
    /// the rock tiles within four of each centre.
    /// </remarks>
    [Fact]
    public void StoneOutcropsAreNotAllOneShape()
    {
        SimConfig shipped = ShippedConfig.Load();
        int varied = 0;
        const int Seeds = 24;
        for (ulong seed = 1; seed <= Seeds; seed++)
        {
            SimConfig config = shipped with { Seed = seed };
            GeneratedMap map = MapGenerator.Generate(config, seed);
            var sizes = new HashSet<int>();
            foreach (MapGenerator.Seam seam in MapGenerator.SeamsOf(config, seed, Terrain.Rock))
            {
                sizes.Add(CountNear(map, seam.Centre, Terrain.Rock, 4));
            }

            if (sizes.Count >= 3)
            {
                varied++;
            }
        }

        _output.WriteLine($"{varied} of {Seeds} valleys have three or more outcrop sizes among their stone seams");
        Assert.True(varied * 10 >= Seeds * 9, $"Only {varied} of {Seeds} valleys vary their outcrops.");
    }

    /// <summary>Ore can be walked over — you have to stand on a seam to clear it.</summary>
    [Fact]
    public void OreIsSomethingYouWalkOnRatherThanRound()
    {
        Assert.True(TerrainRules.IsPassable(Terrain.Rock));
        Assert.True(TerrainRules.IsPassable(Terrain.IronDeposit));
        Assert.False(TerrainRules.IsPassable(Terrain.Water));
    }

    /// <summary>The terrain says what it yields, in one place.</summary>
    [Fact]
    public void TheGroundKnowsWhatItGivesUp()
    {
        Assert.Equal(Goods.Logs, TerrainRules.Yields(Terrain.Forest));
        Assert.Equal(Goods.Stone, TerrainRules.Yields(Terrain.Rock));
        Assert.Equal(Goods.Iron, TerrainRules.Yields(Terrain.IronDeposit));
        Assert.Null(TerrainRules.Yields(Terrain.Grass));
        Assert.Null(TerrainRules.Yields(Terrain.Water));
    }

    /// <summary>⭐ Clearing a seam spends it and yields its own good.</summary>
    [Theory]
    [InlineData(Terrain.Rock, Goods.Stone)]
    [InlineData(Terrain.IronDeposit, Goods.Iron)]
    public void ClearingASeamSpendsIt(Terrain terrain, Goods expected)
    {
        SimWorld world = Build(12345UL);

        GridPos tile = default;
        bool found = false;
        for (int y = world.Map.MinY; y < world.Map.MinY + world.Map.Height && !found; y++)
        {
            for (int x = world.Map.MinX; x < world.Map.MinX + world.Map.Width && !found; x++)
            {
                var at = new GridPos(x, y);
                if (world.Map.TerrainAt(at) == terrain)
                {
                    tile = at;
                    found = true;
                }
            }
        }

        Assert.True(found, $"The valley has no {terrain}.");
        Assert.True(world.PaintHarvest(tile).Allowed, "A seam must be paintable for harvest.");

        (Goods goods, int amount) = world.Harvest(tile);
        _output.WriteLine($"{tile} gave {amount} {goods}, now {world.Map.TerrainAt(tile)}");

        Assert.Equal(expected, goods);
        Assert.True(amount > 0);
        Assert.Equal(Terrain.Grass, world.Map.TerrainAt(tile));
        Assert.Equal(0, world.Harvest(tile).Amount);
    }

    /// <summary>The shipped config carries the seam rules too.</summary>
    [Fact]
    public void TheShippedConfigGeneratesOre()
    {
        SimConfig shipped = ShippedConfig.Load();

        Assert.True(shipped.StoneSeamCount > 0);
        Assert.True(shipped.IronSeamCount > 0);
        // Per-tile yields are rows in the goods catalogue since D210.
        var goods = new GoodsCatalog(shipped.GoodsCatalog);
        Assert.True(goods.YieldPerTileOf(Goods.Stone) > 0);
        Assert.True(goods.YieldPerTileOf(Goods.Iron) > 0);

        // ⭐ And the pairing that makes iron worth walking for is still assertable, which is the
        // half of that reasoning the config comment kept: less iron per tile than stone.
        Assert.True(
            goods.YieldPerTileOf(Goods.Iron) < goods.YieldPerTileOf(Goods.Stone),
            "Iron is meant to give less per tile than stone — that and the ring distance are "
            + "what make it worth walking for rather than merely further away.");
        Assert.True(
            shipped.IronSeamRingTiles > shipped.StoneSeamRingTiles,
            "Iron is meant to sit further out than stone — reaching it is the decision.");
    }

    /// <summary>
    /// ⭐ The seams the quarry added moved no forest (`quarry.md §3.1`, D434).
    /// </summary>
    /// <remarks>
    /// <para>
    /// The extra seams take their offsets from a hash, never a draw, so the woodland painted
    /// after them sits where it sat: every tree in the valley without them is a tree with them,
    /// or a tile the new rock took; no tree appears that was not there. The river and the
    /// founding site are untouched.
    /// </para>
    /// <para>
    /// ⚠️ <b>What it cannot catch (D435):</b> the generator passes its stream by value, so a draw
    /// made <em>inside</em> <c>PaintSeams</c> advances only a copy and would move nothing either.
    /// It catches a draw on <c>Generate</c>'s own stream, and seams laid after the woodland.
    /// </para>
    /// </remarks>
    [Theory]
    [InlineData(1UL)]
    [InlineData(7UL)]
    [InlineData(2024UL)]
    [InlineData(12345UL)]
    public void TheQuarrysSeamsMovedNoForest(ulong seed)
    {
        // ⚠️ With the valley-wide seams off in both arms (D543): they are drawn after the rings, so taking the extras
        // away moves them, and a wood then stands where one used to lie. `SeamVarietyTests.TheScatteredSeamsMovedNoForest`
        // asks the same of them.
        // ⚠️ AND THE STONE EXTRAS ONLY (D543). Since D475 the iron ring is laid four to a ring, so going from two iron
        // seams to four re-spaces the whole ring rather than adding two to it: a tile an iron seam leaves is grass, and
        // a wood may stand there. That was always so and the four seeds here never happened to show it until the ring
        // came in to 22. The stone extras ARE additive — the first ring's four stay, the second ring's eight are added —
        // and they are the claim this guard makes.
        SimConfig with = ShippedConfig.Load() with { Seed = seed, ScatteredStoneSeams = 0, ScatteredIronSeams = 0 };
        SimConfig without = with with { ExtraStoneSeams = 0 };
        Assert.True(with.ExtraStoneSeams > 0, "Nothing to compare.");

        GeneratedMap a = MapGenerator.Generate(without, seed);
        GeneratedMap b = MapGenerator.Generate(with, seed);

        int took = 0;
        for (int i = 0; i < a.Tiles.Count; i++)
        {
            Terrain before = a.Tiles[i];
            Terrain after = b.Tiles[i];
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

        _output.WriteLine($"seed {seed}: the new seams took {took} tiles the woods would have had");
        Assert.Equal(a.FoundingSite, b.FoundingSite);
    }

    /// <summary>
    /// ⭐ Every iron seam holds 50 iron — so the first one cleared unlocks the smithy (D395, D434).
    /// </summary>
    /// <remarks>
    /// Before the quarry spec no iron seam in 64 valleys held 50: radius-1 diamonds of five tiles
    /// at 8 a tile, clipped by the river. A seam now grows a ring at a time until it does. Counted
    /// around each seam's centre (<see cref="MapGenerator.SeamsOf"/>, D475), because the river can cut
    /// one seam in two and a player still sees one seam there.
    /// </remarks>
    [Theory]
    [InlineData("")]
    [InlineData("stone=sparse")]
    [InlineData("stone=rich")]
    [InlineData("iron=sparse")]
    [InlineData("iron=rich")]
    [InlineData("river=6")]
    [InlineData("flow=ns")]
    [InlineData("flow=nwse")]
    [InlineData("flow=swne")]
    public void EveryIronSeamHoldsFifty(string row)
    {
        SimConfig shipped = WithTheRow(row);
        int perTile = new GoodsCatalog(shipped.GoodsCatalog).YieldPerTileOf(Goods.Iron);
        int least = int.MaxValue;

        for (ulong seed = 1; seed <= 64; seed++)
        {
            GeneratedMap map = MapGenerator.Generate(shipped with { Seed = seed }, seed);
            foreach (MapGenerator.Seam seam in MapGenerator.SeamsOf(shipped with { Seed = seed }, seed, Terrain.IronDeposit))
            {
                int iron = CountNear(map, seam.Centre, Terrain.IronDeposit, 8) * perTile;
                least = Math.Min(least, iron);
                Assert.True(
                    iron >= shipped.IronSeamMinIron,
                    $"Seed {seed}'s iron seam at {seam.Centre} holds {iron}, short of {shipped.IronSeamMinIron}.");
            }
        }

        _output.WriteLine($"the leanest iron seam in 64 valleys holds {least}");
    }

    /// <summary>
    /// ⭐ Nobody is stranded: every valley keeps three stone seams it can walk to (§0.1, D434).
    /// </summary>
    /// <remarks>
    /// A quarry is cut only into rock, and laborers clear rock for good — so a valley whose
    /// reachable seams are all cleared can never quarry. With the extra seams, three are within
    /// reach in every one of 64 valleys (the river cuts some off; there are no bridges yet).
    /// </remarks>
    [Theory]
    [InlineData("")]
    [InlineData("stone=sparse")]
    [InlineData("stone=rich")]
    [InlineData("iron=sparse")]
    [InlineData("iron=rich")]
    [InlineData("river=6")]
    [InlineData("flow=ns")]
    [InlineData("flow=nwse")]
    [InlineData("flow=swne")]
    public void EveryValleyKeepsThreeStoneSeamsInReach(string row)
    {
        SimConfig shipped = WithTheRow(row);
        int fewest = int.MaxValue;

        for (ulong seed = 1; seed <= 64; seed++)
        {
            GeneratedMap map = MapGenerator.Generate(shipped with { Seed = seed }, seed);
            bool[] reach = Reachable(map, map.FoundingSite);
            int seams = 0;
            foreach (MapGenerator.Seam seam in MapGenerator.SeamsOf(shipped with { Seed = seed }, seed, Terrain.Rock))
            {
                if (ReachableNear(map, reach, seam.Centre, Terrain.Rock, 3) >= 5)
                {
                    seams++;
                }
            }

            fewest = Math.Min(fewest, seams);
            Assert.True(seams >= 3, $"Seed {seed} has only {seams} stone seams a villager can walk to.");
        }

        _output.WriteLine($"the fewest reachable stone seams in 64 valleys: {fewest}");
    }

    /// <summary>
    /// The shipped config with one new-game row set (`new-game-screen.md §7`, D480) — the screen may
    /// not offer a setting that breaks a promise the generator makes on the shipped one.
    /// </summary>
    private static SimConfig WithTheRow(string row)
    {
        SimConfig shipped = ShippedConfig.Load();
        if (row.Length == 0)
        {
            return shipped;
        }

        NewGameSettings settings = NewGame.Defaults(shipped, "1");
        string[] kv = row.Split('=');
        var values = new Dictionary<string, string>(settings.Values) { [kv[0]] = kv[1] };
        return NewGame.Apply(shipped, settings with { Values = values });
    }

    private static int CountNear(GeneratedMap map, GridPos at, Terrain kind, int within)
    {
        int n = 0;
        for (int dy = -within; dy <= within; dy++)
        {
            for (int dx = -within; dx <= within; dx++)
            {
                var p = new GridPos(at.X + dx, at.Y + dy);
                if (Math.Abs(dx) + Math.Abs(dy) <= within && map.Contains(p) && map.TerrainAt(p) == kind)
                {
                    n++;
                }
            }
        }

        return n;
    }

    private static int ReachableNear(GeneratedMap map, bool[] reach, GridPos at, Terrain kind, int within)
    {
        int n = 0;
        for (int dy = -within; dy <= within; dy++)
        {
            for (int dx = -within; dx <= within; dx++)
            {
                var p = new GridPos(at.X + dx, at.Y + dy);
                if (Math.Abs(dx) + Math.Abs(dy) <= within && map.Contains(p) && map.TerrainAt(p) == kind
                    && reach[Index(map, p)])
                {
                    n++;
                }
            }
        }

        return n;
    }

    private static int Index(GeneratedMap map, GridPos p) => ((p.Y - map.MinY) * map.Width) + (p.X - map.MinX);

    /// <summary>Every tile a villager could walk to from here — water is the only wall today.</summary>
    private static bool[] Reachable(GeneratedMap map, GridPos from)
    {
        var seen = new bool[map.Tiles.Count];
        var queue = new Queue<GridPos>();
        seen[Index(map, from)] = true;
        queue.Enqueue(from);
        while (queue.Count > 0)
        {
            GridPos p = queue.Dequeue();
            foreach (GridPos next in new[]
            {
                new GridPos(p.X + 1, p.Y), new GridPos(p.X - 1, p.Y),
                new GridPos(p.X, p.Y + 1), new GridPos(p.X, p.Y - 1),
            })
            {
                if (map.Contains(next) && !seen[Index(map, next)] && TerrainRules.IsPassable(map.TerrainAt(next)))
                {
                    seen[Index(map, next)] = true;
                    queue.Enqueue(next);
                }
            }
        }

        return seen;
    }
}

/// <summary>
/// The harvest brush has modes, and the mode is a filter rather than a layer (D67, D90).
/// </summary>
/// <remarks>
/// Joe: <em>"you pick trees or stone or all and drag."</em> So a mode decides which tiles take
/// the paint and is then forgotten — a marked tile is simply marked, and what a laborer gets
/// is whatever is standing there. <b>Nothing new is stored and nothing new is hashed</b>, and
/// it still answers what D67 asked for, because the wood in a stone-brushed drag never takes
/// the paint in the first place.
/// </remarks>
public sealed class HarvestBrushModeTests
{
    private readonly ITestOutputHelper _output;

    public HarvestBrushModeTests(ITestOutputHelper output) => _output = output;

    private static SimWorld Build() =>
        SimFactory.CreatePhase0(VillageFixtures.Village, new InMemoryLogSink()).World;

    private static GridPos Find(SimWorld world, Terrain terrain)
    {
        for (int y = world.Map.MinY; y < world.Map.MinY + world.Map.Height; y++)
        {
            for (int x = world.Map.MinX; x < world.Map.MinX + world.Map.Width; x++)
            {
                var at = new GridPos(x, y);
                if (world.Map.TerrainAt(at) == terrain)
                {
                    return at;
                }
            }
        }

        throw new Xunit.Sdk.XunitException($"The valley has no {terrain}.");
    }

    /// <summary>⭐ Each mode takes its own and refuses the rest, by name.</summary>
    [Theory]
    [InlineData(HarvestBrush.Trees, Terrain.Forest, Terrain.Rock)]
    [InlineData(HarvestBrush.Stone, Terrain.Rock, Terrain.Forest)]
    [InlineData(HarvestBrush.Iron, Terrain.IronDeposit, Terrain.Forest)]
    public void AModeTakesItsOwnAndLeavesTheRest(
        HarvestBrush brush, Terrain wanted, Terrain other)
    {
        SimWorld world = Build();

        GridPos mine = Find(world, wanted);
        GridPos theirs = Find(world, other);

        Assert.True(world.PaintHarvest(mine, brush).Allowed);
        Assert.True(world.Zones.IsHarvest(mine));

        PlacementVerdict refused = world.PaintHarvest(theirs, brush);
        _output.WriteLine($"{brush} over {other}: {refused.Reason}");

        Assert.False(refused.Allowed);
        Assert.False(world.Zones.IsHarvest(theirs));
        Assert.NotEmpty(refused.Reason);
    }

    /// <summary>The all-brush is the absence of a filter, so it takes everything.</summary>
    [Fact]
    public void TheAllBrushTakesWhateverIsStanding()
    {
        SimWorld world = Build();

        foreach (Terrain terrain in new[] { Terrain.Forest, Terrain.Rock, Terrain.IronDeposit })
        {
            GridPos tile = Find(world, terrain);
            Assert.True(
                world.PaintHarvest(tile, HarvestBrush.Everything).Allowed,
                $"The all-brush refused {terrain}.");
        }

        Assert.Equal(3, world.Zones.HarvestTiles);
    }

    /// <summary>Empty ground takes no brush at all.</summary>
    [Theory]
    [InlineData(HarvestBrush.Everything)]
    [InlineData(HarvestBrush.Trees)]
    [InlineData(HarvestBrush.Stone)]
    public void OpenGroundIsNeverPainted(HarvestBrush brush)
    {
        SimWorld world = Build();
        Assert.False(world.PaintHarvest(Find(world, Terrain.Grass), brush).Allowed);
    }

    /// <summary>
    /// ⭐ A tile marked by one mode is indistinguishable from one marked by another.
    /// </summary>
    /// <remarks>
    /// The point of modes-being-a-filter, stated as a property: the layer holds
    /// <em>marked</em>, not <em>marked for stone</em>. If the mode were stored, the same
    /// valley painted two ways would be two different worlds, and the hash would have to
    /// carry a fact that changes nothing about what happens.
    /// </remarks>
    [Fact]
    public void HowATileWasMarkedIsNotRemembered()
    {
        SimWorld byMode = Build();
        SimWorld byAll = Build();

        GridPos tile = Find(byMode, Terrain.Rock);

        byMode.PaintHarvest(tile, HarvestBrush.Stone);
        byAll.PaintHarvest(tile, HarvestBrush.Everything);

        Assert.Equal(StateHash.Compute(byMode), StateHash.Compute(byAll));
    }
}

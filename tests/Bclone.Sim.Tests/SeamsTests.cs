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

    /// <summary>⭐ Adding the seams moved nothing that was already in the valley.</summary>
    /// <remarks>
    /// <b>The draw order is the contract</b>, and this is what says the new draws were
    /// APPENDED rather than inserted. Anywhere earlier and every subsequent random value
    /// shifts, so the river, the stands, the sites, the founding and the soil all move for
    /// every seed anybody has written down — a save-breaking change wearing the clothes of
    /// a worldgen feature.
    /// </remarks>
    [Theory]
    [InlineData(12345UL)]
    [InlineData(7UL)]
    [InlineData(2024UL)]
    public void TheSeamsWereAppendedToTheDrawOrder(ulong seed)
    {
        SimConfig config = VillageFixtures.Village with { Seed = seed };

        SimWorld withOre = Build(seed);
        SimWorld withoutOre = SimFactory.CreatePhase0(
            config with { StoneSeamCount = 0, IronSeamCount = 0 }, new InMemoryLogSink()).World;

        Assert.Equal(withoutOre.Map.FoundingSite, withOre.Map.FoundingSite);
        Assert.Equal(withoutOre.Map.Soil, withOre.Map.Soil);
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
    /// or a tile the new rock took; no tree appears that was not there. The river, the soil and
    /// the founding site are untouched.
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
        SimConfig with = ShippedConfig.Load() with { Seed = seed };
        SimConfig without = with with { ExtraStoneSeams = 0, ExtraIronSeams = 0 };
        Assert.True(with.ExtraStoneSeams > 0 && with.ExtraIronSeams > 0, "Nothing to compare.");

        GeneratedMap a = MapGenerator.Generate(without, new DeterministicRandom(seed));
        GeneratedMap b = MapGenerator.Generate(with, new DeterministicRandom(seed));

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
        Assert.Equal(a.Soil, b.Soil);
    }

    /// <summary>
    /// ⭐ Every iron seam holds 50 iron — so the first one cleared unlocks the smithy (D395, D434).
    /// </summary>
    /// <remarks>
    /// Before the quarry spec no iron seam in 64 valleys held 50: radius-1 diamonds of five tiles
    /// at 8 a tile, clipped by the river. A seam now grows a ring at a time until it does. Counted
    /// around each iron slot, because the river can cut one seam in two and a player still sees one
    /// seam there.
    /// </remarks>
    [Fact]
    public void EveryIronSeamHoldsFifty()
    {
        SimConfig shipped = ShippedConfig.Load();
        int perTile = new GoodsCatalog(shipped.GoodsCatalog).YieldPerTileOf(Goods.Iron);
        int least = int.MaxValue;

        for (ulong seed = 1; seed <= 64; seed++)
        {
            GeneratedMap map = MapGenerator.Generate(shipped with { Seed = seed }, new DeterministicRandom(seed));
            foreach (int i in MapGenerator.SeamSlots(shipped.IronSeamCount, shipped.ExtraIronSeams, shipped.IronSeamRingTiles))
            {
                GridPos slot = MapGenerator.RingSlot(i, shipped.IronSeamRingTiles);
                int iron = CountNear(map, slot, Terrain.IronDeposit, 8) * perTile;
                least = Math.Min(least, iron);
                Assert.True(
                    iron >= shipped.IronSeamMinIron,
                    $"Seed {seed}'s iron seam at slot {i} holds {iron}, short of {shipped.IronSeamMinIron}.");
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
    [Fact]
    public void EveryValleyKeepsThreeStoneSeamsInReach()
    {
        SimConfig shipped = ShippedConfig.Load();
        int fewest = int.MaxValue;

        for (ulong seed = 1; seed <= 64; seed++)
        {
            GeneratedMap map = MapGenerator.Generate(shipped with { Seed = seed }, new DeterministicRandom(seed));
            bool[] reach = Reachable(map, map.FoundingSite);
            int seams = 0;
            foreach (int i in MapGenerator.SeamSlots(shipped.StoneSeamCount, shipped.ExtraStoneSeams, shipped.StoneSeamRingTiles))
            {
                if (ReachableNear(map, reach, MapGenerator.RingSlot(i, shipped.StoneSeamRingTiles), Terrain.Rock, 3) >= 5)
                {
                    seams++;
                }
            }

            fewest = Math.Min(fewest, seams);
            Assert.True(seams >= 3, $"Seed {seed} has only {seams} stone seams a villager can walk to.");
        }

        _output.WriteLine($"the fewest reachable stone seams in 64 valleys: {fewest}");
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

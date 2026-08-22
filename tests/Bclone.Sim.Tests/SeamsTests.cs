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
        Assert.True(shipped.StonePerRockTile > 0);
        Assert.True(shipped.IronPerDepositTile > 0);
        Assert.True(
            shipped.IronSeamRingTiles > shipped.StoneSeamRingTiles,
            "Iron is meant to sit further out than stone — reaching it is the decision.");
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

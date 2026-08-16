using Bclone.Sim.Config;
using Bclone.Sim.Core;
using Bclone.Sim.Determinism;
using Bclone.Sim.Logging;
using Bclone.Sim.World;
using Xunit;
using Xunit.Abstractions;

namespace Bclone.Sim.Tests;

/// <summary>
/// The ground a crop grows on — <c>specs/crops-and-orchards.md §4</c> (D161).
/// </summary>
/// <remarks>
/// The floor the rest of the slice stands on: three terrains for the three stages of a field,
/// and a crop id per tile saying what is growing there. **Nothing sows anything yet** — these
/// guard the storage and the seed contract before any behaviour uses them, which is the same
/// order <c>mutable-terrain.md</c> was built in.
/// </remarks>
public sealed class CropGroundTests
{
    private readonly ITestOutputHelper _output;

    public CropGroundTests(ITestOutputHelper output) => _output = output;

    private static SimConfig Config => VillageFixtures.Village;

    private static SimWorld Build() =>
        SimFactory.CreatePhase0(Config, new InMemoryLogSink()).World;

    /// <summary>⭐ The three field terrains are appended, and their values are pinned.</summary>
    /// <remarks>
    /// <b>Terrain is hashed by value</b>, so renumbering silently reinterprets every golden and
    /// every seed anybody has written down — the same rule <c>JobKind.Forester</c> is pinned to
    /// 1 by, and the reason <see cref="Terrain.Sapling"/>'s own remarks say *appended, never
    /// renumbered*. This is that promise as a test rather than as a comment.
    /// </remarks>
    [Fact]
    public void TheFieldTerrainsAreAppendedAndTheirValuesArePinned()
    {
        Assert.Equal(6, (int)Terrain.Field);
        Assert.Equal(7, (int)Terrain.Sown);
        Assert.Equal(8, (int)Terrain.Ripe);

        // And the ones that came before are where they were.
        Assert.Equal(0, (int)Terrain.Grass);
        Assert.Equal(2, (int)Terrain.Forest);
        Assert.Equal(5, (int)Terrain.Sapling);
    }

    /// <summary>A field is walked over, and it is not something the harvest brush may take.</summary>
    /// <remarks>
    /// <b>The second half is the seam</b> (`crops-and-orchards.md §6`). A ripe field is full of
    /// food and must still answer <c>null</c> to <see cref="TerrainRules.Yields"/>, because
    /// that question is read by <c>NearestHarvest</c> and by D157's footprint-clearing pass —
    /// so a yes would let a laborer clearing painted ground reap the farm.
    /// </remarks>
    [Fact]
    public void AFieldIsPassableAndIsNotHarvestBrushWork()
    {
        foreach (Terrain field in new[] { Terrain.Field, Terrain.Sown, Terrain.Ripe })
        {
            Assert.True(TerrainRules.IsPassable(field), $"{field} should be walkable.");
            Assert.Null(TerrainRules.Yields(field));
        }

        // Anti-vacuity (D7): the same call says yes for the things it is supposed to.
        Assert.Equal(Goods.Logs, TerrainRules.Yields(Terrain.Forest));
        Assert.False(TerrainRules.IsPassable(Terrain.Water));
    }

    /// <summary>Sowing a tile and clearing it returns the world to exactly where it was.</summary>
    /// <remarks>
    /// <para>
    /// Round-trip identity: nothing lingers in the hash once a field is given up, which is what
    /// makes an abandoned farm indistinguishable from one that never existed.
    /// </para>
    /// <para>
    /// <b>⚠️ AND THIS DOES NOT PROVE THE LAYER IS HASHED SPARSELY, WHICH ITS FIRST NAME CLAIMED
    /// IT DID.</b> A full pass that mixed a zero for every unsown tile would pass this exactly
    /// as happily — set, differ, clear, match — while moving both 50-year goldens the moment it
    /// shipped. **The sparse contract is proved by `StockLimitTests`' two goldens staying
    /// put**, and by <see cref="TheGeneratorSowsNothingAndPloughsNothing"/> establishing that a
    /// generated valley has nothing sown in it. Named for what it checks, not for what would
    /// have been nicer to claim.
    /// </para>
    /// </remarks>
    [Fact]
    public void SowingATileAndClearingItLeavesNoTrace()
    {
        SimWorld world = Build();
        GridPos tile = world.Map.FoundingSite;

        ulong before = StateHash.Compute(world);

        Assert.True(world.Map.SetCrop(tile, 1));
        Assert.NotEqual(before, StateHash.Compute(world));

        Assert.True(world.Map.SetCrop(tile, 0));
        Assert.Equal(before, StateHash.Compute(world));
    }

    /// <summary>Which crop is sown is part of the state, not decoration.</summary>
    /// <remarks>
    /// Two fields of the same size in the same places growing different things are different
    /// villages, and must not read as the same one — D51's rule, the same argument that puts
    /// the work-ground *owner* in the hash beside the tile.
    /// </remarks>
    [Fact]
    public void TwoDifferentCropsOnOneTileHashDifferently()
    {
        SimWorld world = Build();
        GridPos tile = world.Map.FoundingSite;

        world.Map.SetCrop(tile, 1);
        ulong wheat = StateHash.Compute(world);

        world.Map.SetCrop(tile, 2);
        ulong barley = StateHash.Compute(world);

        _output.WriteLine($"crop 1 hashes {wheat}, crop 2 hashes {barley}");
        Assert.NotEqual(wheat, barley);
    }

    /// <summary>The same tiles sown the same way, twice, hash identically.</summary>
    /// <remarks>
    /// Determinism is architectural (METHODOLOGY §3) and a new per-tile layer is exactly where
    /// a desync lives. Two worlds from one seed, given the same sowing, must agree.
    /// </remarks>
    [Fact]
    public void TwoVillagesSownTheSameWayAgree()
    {
        SimWorld a = Build();
        SimWorld b = Build();

        for (int dx = 0; dx < 5; dx++)
        {
            var tile = new GridPos(a.Map.FoundingSite.X + dx, a.Map.FoundingSite.Y);
            a.Map.SetCrop(tile, 1);
            b.Map.SetCrop(tile, 1);
        }

        Assert.Equal(StateHash.Compute(a), StateHash.Compute(b));
    }

    /// <summary>Out of bounds reads as nothing sown, and refuses to be sown.</summary>
    /// <remarks>
    /// The same contract <see cref="GeneratedMap.TerrainAt"/> keeps, and worth pinning because
    /// a brush dragged off the edge of the valley is a thing players do.
    /// </remarks>
    [Fact]
    public void OffTheMapHoldsNoCropAndCannotBeSown()
    {
        SimWorld world = Build();
        var outside = new GridPos(world.Map.MinX - 5, world.Map.MinY - 5);

        Assert.Equal(0, world.Map.CropAt(outside));
        Assert.False(world.Map.SetCrop(outside, 1));
        Assert.Equal(0, world.Map.CropAt(outside));
    }

    /// <summary>The valley arrives with nothing sown anywhere.</summary>
    /// <remarks>
    /// <b>The claim that keeps the map golden still.</b> The generator never produces a crop or
    /// a field terrain, so a generated valley hashes byte-identically to one from before this
    /// layer existed — see <c>crops-and-orchards.md §4</c>. If this fails, the map golden is
    /// about to move and the change was not what it claimed to be.
    /// </remarks>
    [Fact]
    public void TheGeneratorSowsNothingAndPloughsNothing()
    {
        SimWorld world = Build();

        int sown = 0;
        foreach (byte crop in world.Map.Crops)
        {
            if (crop != 0)
            {
                sown++;
            }
        }

        int fields = 0;
        for (int i = 0; i < world.Map.Tiles.Count; i++)
        {
            if (world.Map.Tiles[i] is Terrain.Field or Terrain.Sown or Terrain.Ripe)
            {
                fields++;
            }
        }

        _output.WriteLine(
            $"{world.Map.Crops.Count} tiles in the valley, {sown} sown, {fields} ploughed");

        Assert.Equal(0, sown);
        Assert.Equal(0, fields);
    }
}

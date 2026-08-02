using Bclone.Sim.Config;
using Bclone.Sim.Core;
using Bclone.Sim.Determinism;
using Bclone.Sim.Logging;
using Bclone.Sim.World;
using Xunit;
using Xunit.Abstractions;

namespace Bclone.Sim.Tests;

/// <summary>
/// The valley can change shape — <c>specs/mutable-terrain.md</c> (D41's prediction, slice C3a).
/// </summary>
/// <remarks>
/// <para>
/// <b>Nothing player-facing ships here.</b> This is the floor the harvest brush
/// (<c>building-placement.md §12</c>), the planting brush and bridges (D40) all stand on, and
/// it is on its own branch of the work because it is the one part of C3 that no open design
/// question touches.
/// </para>
/// <para>
/// <b>Half the slice was cut by reading the code first.</b> The plan called for
/// <c>StateHash.MixMap</c> to become incremental and for the map golden to be re-taken;
/// <c>MixMap</c> has always walked the live tile array, so mutable terrain was already hashed
/// correctly and neither was needed. <see cref="AChangedTileIsPartOfTheWorld"/> is what says
/// so out loud.
/// </para>
/// </remarks>
public sealed class MutableTerrainTests
{
    private readonly ITestOutputHelper _output;

    public MutableTerrainTests(ITestOutputHelper output) => _output = output;

    private static SimWorld Build() =>
        SimFactory.CreatePhase0(VillageFixtures.Village, new InMemoryLogSink()).World;

    /// <summary>Find a tile of one kind, so a test does not hard-code a generated valley.</summary>
    private static GridPos FindTile(SimWorld world, Terrain terrain)
    {
        GeneratedMap map = world.Map;
        for (int y = map.MinY; y < map.MinY + map.Height; y++)
        {
            for (int x = map.MinX; x < map.MinX + map.Width; x++)
            {
                var at = new GridPos(x, y);
                if (map.TerrainAt(at) == terrain)
                {
                    return at;
                }
            }
        }

        throw new Xunit.Sdk.XunitException($"The generated valley has no {terrain} at all.");
    }

    // ---------------------------------------------------------------
    //  Terrain is sim state
    // ---------------------------------------------------------------

    /// <summary>⭐ A changed tile changes the hash, so terrain cannot drift out of the world.</summary>
    /// <remarks>
    /// The guard that made the rest of the slice smaller: it passes on the hashing that was
    /// already there. Had it failed, <c>MixMap</c> would have needed rebuilding and both
    /// goldens re-taking.
    /// </remarks>
    [Fact]
    public void AChangedTileIsPartOfTheWorld()
    {
        SimWorld world = Build();
        ulong before = StateHash.Compute(world);

        GridPos forest = FindTile(world, Terrain.Forest);
        Assert.True(world.SetTerrain(forest, Terrain.Grass));

        _output.WriteLine($"felled {forest}: {before} -> {StateHash.Compute(world)}");
        Assert.NotEqual(before, StateHash.Compute(world));
    }

    /// <summary>Same seed and the same edits give the same valley.</summary>
    /// <remarks>
    /// Mutation must not become a way round the seed contract (D18): quoting one number still
    /// has to reproduce a whole run, world included.
    /// </remarks>
    [Fact]
    public void TheSameEditsGiveTheSameValley()
    {
        SimWorld first = Build();
        SimWorld second = Build();

        GridPos tile = FindTile(first, Terrain.Forest);
        first.SetTerrain(tile, Terrain.Grass);
        second.SetTerrain(tile, Terrain.Grass);

        Assert.Equal(StateHash.Compute(first), StateHash.Compute(second));
    }

    /// <summary>Setting a tile to what it already is changes nothing and says so.</summary>
    [Fact]
    public void SettingATileToWhatItAlreadyIsIsNotAChange()
    {
        SimWorld world = Build();
        GridPos forest = FindTile(world, Terrain.Forest);

        Assert.False(world.SetTerrain(forest, Terrain.Forest));
    }

    /// <summary>A brush dragged off the edge of the valley is refused, not thrown.</summary>
    [Fact]
    public void OutOfBoundsIsRefusedRatherThanThrown()
    {
        SimWorld world = Build();
        var offMap = new GridPos(world.Map.MinX - 50, world.Map.MinY - 50);

        Assert.False(world.Map.Contains(offMap));
        Assert.False(world.SetTerrain(offMap, Terrain.Grass));
    }

    // ---------------------------------------------------------------
    //  The cache — the half of the slice that was real
    // ---------------------------------------------------------------

    /// <summary>
    /// ⭐ Making ground impassable drops every cached route, and the answers change.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>D41's prediction, paid off.</b> The flow-field cache kept one field per destination
    /// forever, on the stated grounds that terrain never changed. A stale route is the worst
    /// bug available here — silent, rare, and it reads as a pathing quirk rather than as
    /// something being wrong.
    /// </para>
    /// <para>
    /// Walling a destination in with water is a blunt way to make the point, and it is the
    /// right one: it is the change a bridge makes in reverse, and it must be visible to
    /// anybody who asks how far away that place is.
    /// </para>
    /// </remarks>
    [Fact]
    public void WallingAPlaceOffDropsTheRoutesToIt()
    {
        SimWorld world = Build();

        // Somewhere with land around it, and a neighbour to ask about.
        GridPos destination = world.Map.FoundingSite;
        var from = new GridPos(destination.X + 6, destination.Y + 6);

        int before = world.TravelCost.Cost(from, destination);
        Assert.True(before > 0, "The two tiles must start out genuinely apart.");
        Assert.True(world.TravelCost.CachedFields > 0, "Asking should have cached a route.");

        // Ring the destination with water. Every route to it must be recomputed.
        for (int dy = -1; dy <= 1; dy++)
        {
            for (int dx = -1; dx <= 1; dx++)
            {
                if (dx != 0 || dy != 0)
                {
                    world.SetTerrain(new GridPos(destination.X + dx, destination.Y + dy), Terrain.Water);
                }
            }
        }

        Assert.Equal(0, world.TravelCost.CachedFields);

        int after = world.TravelCost.Cost(from, destination);
        _output.WriteLine($"cost {from} -> {destination}: {before} before, {after} after walling it in");

        Assert.NotEqual(before, after);
    }

    /// <summary>
    /// ⭐ Felling a stand keeps every route, and that is a rule now rather than luck.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>The performance claim, asserted.</b> Each cached field is a full Dijkstra over the
    /// valley, one per destination, and a logger fells several times a year — so invalidating
    /// on any terrain change would rebuild the village's whole routing over and over for a
    /// change that moves nothing.
    /// </para>
    /// <para>
    /// <c>building-placement.md §12.3</c> notes that felling happens to leave travel costs
    /// alone and calls that luck, correctly. It is a stated rule with a test under it now:
    /// the cache turns on <b>passability</b>, and grass and forest are both passable.
    /// </para>
    /// </remarks>
    [Fact]
    public void FellingAStandCostsTheVillageNoRoutes()
    {
        SimWorld world = Build();

        GridPos destination = world.Map.FoundingSite;
        var from = new GridPos(destination.X + 6, destination.Y + 6);

        int before = world.TravelCost.Cost(from, destination);
        int cached = world.TravelCost.CachedFields;
        Assert.True(cached > 0);

        GridPos forest = FindTile(world, Terrain.Forest);
        Assert.True(world.SetTerrain(forest, Terrain.Grass));

        Assert.Equal(cached, world.TravelCost.CachedFields);
        Assert.Equal(before, world.TravelCost.Cost(from, destination));
    }

    /// <summary>Passability is asked of the terrain, in one place.</summary>
    /// <remarks>
    /// <c>Terrain.Water</c> was named at two call sites and mutable terrain wanted a third,
    /// which is the <c>StoreKind</c> seam in a new costume — that one ran to five instalments
    /// before the question was replaced instead of the call site (D76).
    /// </remarks>
    [Fact]
    public void OnlyWaterStopsSomebodyWalking()
    {
        Assert.False(TerrainRules.IsPassable(Terrain.Water));
        Assert.True(TerrainRules.IsPassable(Terrain.Grass));
        Assert.True(TerrainRules.IsPassable(Terrain.Forest));
    }

    /// <summary>A village that never changes the ground hashes exactly as it always did.</summary>
    /// <remarks>
    /// The other half of the goldens in <c>StockLimitTests</c>: nothing in a run mutates
    /// terrain yet, so this slice must be invisible to any village that does not fell.
    /// </remarks>
    [Fact]
    public void AVillageThatNeverChangesTheGroundIsUnaffected()
    {
        SimConfig config = VillageFixtures.Village;
        SimLoop loop = SimFactory.CreatePhase0(config, new InMemoryLogSink());

        ulong mapBefore = StateHash.MixMap(0, loop.World.Map);
        loop.Step(config.TicksPerYear * 5);

        Assert.Equal(mapBefore, StateHash.MixMap(0, loop.World.Map));
    }
}

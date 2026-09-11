using Bclone.Sim.Core;
using Bclone.Sim.Logging;
using Bclone.Sim.World;
using Xunit;
using Xunit.Abstractions;

namespace Bclone.Sim.Tests;

/// <summary>
/// ⭐⭐ The paint is finer than the ground, and <b>every tile-level question keeps its answer</b>
/// (D335, `specs/sub-tile-zones.md`).
/// </summary>
/// <remarks>
/// <para>
/// <b>Joe, looking at a 5×5 round brush:</b> *"haha this is a circle????"* At five tiles across a
/// square grid holds a diamond, a square with its corners bitten out, or a square — **and none of
/// them is a circle.** So zones are stored at sixteen sub-tiles to a tile while terrain, the
/// travel-cost field and all nineteen tile-keyed economy entries stay exactly where they are.
/// </para>
/// <para>
/// ⛔⛔ <b>THE RULE THESE EXIST TO HOLD: a tile counts as painted when at least half of its sixteen
/// sub-tiles are.</b> One sentence, for housing and for harvest marks. It is what lets *"is this
/// tile residential?"* and *"which ground is marked for clearing?"* keep their meaning **and their
/// numbers** for any village that paints whole tiles — which is every village that exists today,
/// and every golden. ⚠️ <b>Work ground left the sentence in D352</b>: a farm works every tile it
/// has any paint on, with the harvest in proportion — Joe's call from a round field with a bare
/// corner. Whole tiles are still exactly what they were.
/// </para>
/// </remarks>
public sealed class SubTileZoneTests
{
    private readonly ITestOutputHelper _output;

    public SubTileZoneTests(ITestOutputHelper output) => _output = output;

    private static ZoneMap Zones() =>
        SimFactory.CreatePhase0(VillageFixtures.Village, new InMemoryLogSink()).World.Zones;

    private static readonly GridPos Somewhere = new(31, 24);

    /// <summary>
    /// ⛔⛔ Half is the threshold, and <b>the boundary is where it is posed</b>.
    /// </summary>
    /// <remarks>
    /// Seven sub-tiles is not enough, eight is, nine still is. **Exactly eight counts** — an
    /// off-by-one here would move the whole economy by a tile in every direction, quietly, and
    /// D122 is what that costs.
    /// </remarks>
    [Theory]
    [InlineData(7, false)]
    [InlineData(8, true)]
    [InlineData(9, true)]
    public void ATileIsPaintedWhenHalfItsSubTilesAre(int painted, bool counts)
    {
        ZoneMap zones = Zones();
        int before = zones.ResidentialTiles;

        for (int i = 0; i < painted; i++)
        {
            zones.SetResidential(SubTile.Of(Somewhere, i % 4, i / 4), true);
        }

        _output.WriteLine($"{painted} of {SubTile.PerWholeTile} sub-tiles: "
            + $"IsResidential {zones.IsResidential(Somewhere)}, "
            + $"count {zones.ResidentialTiles - before}");

        Assert.Equal(counts, zones.IsResidential(Somewhere));
        Assert.Equal(counts ? before + 1 : before, zones.ResidentialTiles);
    }

    /// <summary>
    /// ⭐ Painting a whole tile is exactly what it always was — <b>the guard the goldens rest on</b>.
    /// </summary>
    /// <remarks>
    /// Every village in every golden paints whole tiles. **If this were not true the slice would be
    /// a re-balance rather than a resolution change**, which is the one thing
    /// `sub-tile-zones.md §2` says it is not allowed to be.
    /// </remarks>
    [Fact]
    public void PaintingAWholeTileIsWhatItAlwaysWas()
    {
        ZoneMap zones = Zones();
        int before = zones.ResidentialTiles;

        Assert.True(zones.SetResidential(Somewhere, true));
        Assert.True(zones.IsResidential(Somewhere));
        Assert.Equal(before + 1, zones.ResidentialTiles);

        Assert.True(zones.SetResidential(Somewhere, false));
        Assert.False(zones.IsResidential(Somewhere));
        Assert.Equal(before, zones.ResidentialTiles);
    }

    /// <summary>⛔ Marked ground is still counted, iterated and cleared in whole tiles.</summary>
    /// <remarks>
    /// A villager is never sent to clear a quarter of a tile: the work is tile-shaped because the
    /// ground is. **Only the decision about WHERE got finer.**
    /// </remarks>
    [Fact]
    public void HarvestIsStillMarkedAndCountedInWholeTiles()
    {
        ZoneMap zones = Zones();

        for (int i = 0; i < SubTile.HalfATile; i++)
        {
            zones.SetHarvest(SubTile.Of(Somewhere, i % 4, i / 4), true);
        }

        Assert.True(zones.IsHarvest(Somewhere));
        Assert.Equal(1, zones.HarvestTiles);

        // The iterator the clearing scan walks hands back TILES.
        Assert.Single(zones.PaintedHarvest);
        Assert.Equal(Somewhere, zones.PositionOf(zones.PaintedHarvest[0]));
    }

    /// <summary>
    /// ⭐⭐ Work ground is spoken for AND held by a single quarter — <b>one threshold, Joe's</b>
    /// (D352, overturning D335's two).
    /// </summary>
    /// <remarks>
    /// <para>
    /// D335 gave work ground two thresholds: *whose ground is this tile?* at any sub-tile, and
    /// *how much ground does this hut have?* at half, because the economy asks in whole tiles.
    /// Then Joe painted a round field: *"why is part of this painted farm not farmland?"* — a
    /// tile a quarter painted was spoken for and never worked, and showed as bare paint at the
    /// edge of every field. **A farm works every tile it has any paint on now, and the harvest is
    /// in proportion** (`FarmTests.AQuarterOfATileYieldsAQuarterOfItsCrop`).
    /// </para>
    /// <para>
    /// ⚠️ Residential and harvest keep the half rule — see the guards above. This is a rule about
    /// work ground, and a whole-tile village is exactly what it was.
    /// </para>
    /// </remarks>
    [Fact]
    public void GroundIsSpokenForAndHeldByAQuarter()
    {
        ZoneMap zones = Zones();

        zones.SetWorkGround(SubTile.Of(Somewhere, 0, 0), 7);

        _output.WriteLine($"one sub-tile: owner {zones.WorkGroundOwner(Somewhere)}, "
            + $"tiles {zones.WorkGroundTiles(7)}");

        Assert.Equal(7, zones.WorkGroundOwner(Somewhere));
        Assert.Equal(1, zones.WorkGroundTiles(7));
        Assert.Single(zones.WorkGroundOf(7));

        // ⛔ And nobody else may take the rest of it.
        Assert.False(zones.SetWorkGround(SubTile.Of(Somewhere, 3, 3), 9));
        Assert.Equal(7, zones.WorkGroundOwner(Somewhere));

        // Fifteen more quarters change nothing the count can see.
        for (int i = 1; i < SubTile.PerWholeTile; i++)
        {
            zones.SetWorkGround(SubTile.Of(Somewhere, i % 4, i / 4), 7);
        }

        Assert.Equal(1, zones.WorkGroundTiles(7));

        // And the tile is held until the LAST quarter goes.
        for (int i = 0; i < SubTile.PerWholeTile - 1; i++)
        {
            zones.SetWorkGround(SubTile.Of(Somewhere, i % 4, i / 4), 0);
        }

        Assert.Equal(1, zones.WorkGroundTiles(7));
        zones.SetWorkGround(SubTile.Of(Somewhere, 3, 3), 0);
        Assert.Equal(0, zones.WorkGroundTiles(7));
        Assert.Equal(0, zones.WorkGroundOwner(Somewhere));
    }

    /// <summary>
    /// ⭐ <c>Holds</c> is the hold asked of one tile — <b>and it agrees with the index</b>
    /// (D350, D352).
    /// </summary>
    /// <remarks>
    /// The plough and the un-plough hang off this question, so it has to be the same answer
    /// <see cref="ZoneMap.WorkGroundOf"/> gives: a quarter is held, somebody else's ground is never
    /// held by us however much of it is painted, and nothing is held at zero.
    /// </remarks>
    [Fact]
    public void HoldsIsAnyQuarterPaintedForUs()
    {
        ZoneMap zones = Zones();

        Assert.False(zones.Holds(7, Somewhere));

        zones.SetWorkGround(SubTile.Of(Somewhere, 3, 1), 7);

        Assert.True(zones.Holds(7, Somewhere));
        Assert.Single(zones.WorkGroundOf(7));

        // Held by 7 is not held by 9, whatever the count says.
        Assert.False(zones.Holds(9, Somewhere));

        zones.SetWorkGround(SubTile.Of(Somewhere, 3, 1), 0);
        Assert.False(zones.Holds(7, Somewhere));
        Assert.Empty(zones.WorkGroundOf(7));
    }

    /// <summary>
    /// ⛔⛔ A home needs a tile painted IN FULL — <b>the half rule is the economy's, not the
    /// builder's</b> (D350).
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Joe, with a screenshot of two house sites straddling the border of his painted
    /// ground: *"the houses are building outside of the painted area. that shouldn't
    /// happen."*</b> A tile is residential at eight quarters, and a house is drawn on the whole
    /// tile — so a house on a half-painted edge tile stood 0.4 of a tile past the line the player
    /// drew. **The brush's ragged rim is a margin nobody builds on.**
    /// </para>
    /// <para>
    /// ⚠️ Every other reader of <c>IsResidential</c> is untouched, and so is the count the
    /// economy and the goldens rest on. Only the siting asks the stricter question.
    /// </para>
    /// </remarks>
    [Fact]
    public void AHomeIsOnlySitedOnATilePaintedInFull()
    {
        SimWorld world = SimFactory.CreatePhase0(VillageFixtures.Village, new InMemoryLogSink()).World;
        ZoneMap zones = world.Zones;

        // Nothing painted anywhere, so the one tile below is the only candidate.
        for (int i = 0; i < zones.Residential.Count; i++)
        {
            if (zones.Residential[i])
            {
                zones.SetResidential(zones.PositionOf(i), false);
            }
        }

        GridPos tile = ABareReachableTileNear(world, world.Map.FoundingSite);

        for (int i = 0; i < SubTile.HalfATile; i++)
        {
            zones.SetResidential(SubTile.Of(tile, i % 4, i / 4), true);
        }

        Assert.True(zones.IsResidential(tile), "Half a tile counts as painted — that rule stands.");

        var refused = Assert.Throws<Household.NoRoomToBuildException>(
            () => Household.ChooseSite(world, world.Map.FoundingSite));
        _output.WriteLine($"half painted: {refused.Message}");

        for (int i = SubTile.HalfATile; i < SubTile.PerWholeTile; i++)
        {
            zones.SetResidential(SubTile.Of(tile, i % 4, i / 4), true);
        }

        Assert.Equal(tile, Household.ChooseSite(world, world.Map.FoundingSite));
    }

    /// <summary>Open, standing-free ground the village can walk to, close by.</summary>
    private static GridPos ABareReachableTileNear(SimWorld world, GridPos site)
    {
        for (int radius = 1; radius < 12; radius++)
        {
            for (int dy = -radius; dy <= radius; dy++)
            {
                for (int dx = -radius; dx <= radius; dx++)
                {
                    var at = new GridPos(site.X + dx, site.Y + dy);
                    if (world.Map.Contains(at)
                        && world.Map.TerrainAt(at) == Terrain.Grass
                        && !world.SomethingStandsAt(at)
                        && world.TravelCost.CanReach(site, at))
                    {
                        return at;
                    }
                }
            }
        }

        throw new Xunit.Sdk.XunitException("No bare reachable ground near the founding site.");
    }

    /// <summary>⛔ A demolished building's ground goes, sub-tiles and all.</summary>
    /// <remarks>
    /// Wiping only the tile summary would leave the paint underneath — still drawn, still hashed.
    /// *The "right stuff in the wrong place" shape `ZoneMap`'s own comments record costing four
    /// investigations.*
    /// </remarks>
    [Fact]
    public void ReleasingGroundTakesTheSubTilesWithIt()
    {
        ZoneMap zones = Zones();

        zones.SetWorkGround(Somewhere, 7);
        zones.SetWorkGround(SubTile.Of(new GridPos(Somewhere.X + 1, Somewhere.Y), 0, 0), 7);

        // Two, since D352: the quarter-painted neighbour is a tile the hut holds too.
        Assert.Equal(2, zones.ReleaseWorkGround(7));

        Assert.Equal(0, zones.WorkGroundOwner(Somewhere));
        Assert.Equal(0, zones.WorkGroundTiles(7));

        int stillPainted = 0;
        for (int i = 0; i < zones.WorkGroundSub.Count; i++)
        {
            if (zones.WorkGroundSub[i] == 7)
            {
                stillPainted++;
            }
        }

        _output.WriteLine($"sub-tiles still owned by 7 after release: {stillPainted}");
        Assert.Equal(0, stillPainted);
    }

    /// <summary>
    /// ⚠️ A sub-tile knows which tile it is in, <b>and negatives floor</b>.
    /// </summary>
    /// <remarks>
    /// C#'s <c>/</c> truncates toward zero, which would put sub-tile −1 in tile 0 beside sub-tile 0
    /// — a tile twice as wide as every other, straddling the origin, **in a valley whose founding
    /// site is at the origin.** The same trap `Fixed` and `Point` each record.
    /// </remarks>
    [Theory]
    [InlineData(0, 0)]
    [InlineData(3, 0)]
    [InlineData(4, 1)]
    [InlineData(-1, -1)]
    [InlineData(-4, -1)]
    [InlineData(-5, -2)]
    public void ASubTileFallsInTheTileThatContainsIt(int sub, int tile)
    {
        Assert.Equal(tile, new SubTile(sub, sub).Tile.X);
        Assert.Equal(tile, new SubTile(sub, sub).Tile.Y);
    }

    /// <summary>⭐ And the round trip: every sub-tile of a tile reports that tile.</summary>
    [Theory]
    [InlineData(0, 0)]
    [InlineData(7, 3)]
    [InlineData(-3, -9)]
    public void EverySubTileOfATileReportsThatTile(int x, int y)
    {
        var tile = new GridPos(x, y);

        for (int dy = 0; dy < SubTile.PerTile; dy++)
        {
            for (int dx = 0; dx < SubTile.PerTile; dx++)
            {
                Assert.Equal(tile, SubTile.Of(tile, dx, dy).Tile);
            }
        }
    }
}

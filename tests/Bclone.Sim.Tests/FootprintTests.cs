using Bclone.Sim.Config;
using Bclone.Sim.Logging;
using Bclone.Sim.Core;
using Bclone.Sim.World;
using Xunit;
using Xunit.Abstractions;

namespace Bclone.Sim.Tests;

/// <summary>
/// <see cref="Footprint"/> and <see cref="Point"/> — the ground a turned building stands on.
/// </summary>
/// <remarks>
/// Joe's call (2026-09-06) out of three options: *"tile coverage from the true rect"* — the sim
/// keeps the real rotated rectangle and derives tiles from it, so there is no second collision
/// system and no facade. These guard the rule that makes that legible: **a building covers the
/// tiles whose centres it stands on.**
/// </remarks>
public sealed class FootprintTests
{
    private readonly ITestOutputHelper _output;

    public FootprintTests(ITestOutputHelper output) => _output = output;

    // ---------------------------------------------------------------
    //  Point, and the tile convention
    // ---------------------------------------------------------------

    [Theory]
    [InlineData(0, 0)]
    [InlineData(3, 7)]
    [InlineData(-4, -9)]   // the valley straddles its own origin
    public void ATileCentreRoundTripsToItsOwnTile(int x, int y)
    {
        var tile = new GridPos(x, y);
        Assert.Equal(tile, Point.CentreOf(tile).ToTile());
    }

    /// <summary>⚠️ The convention, pinned: a tile centre is half a tile in from its corner.</summary>
    [Fact]
    public void ATileCentreIsHalfATileIn()
    {
        Point centre = Point.CentreOf(new GridPos(0, 0));
        Assert.Equal(Fixed.FromRatio(1, 2), centre.X);
        Assert.Equal(Fixed.FromRatio(1, 2), centre.Y);

        // And a negative tile's centre is negative-and-a-half, not minus-a-half.
        Point below = Point.CentreOf(new GridPos(-1, -1));
        Assert.Equal(Fixed.FromRatio(-1, 2), below.X);
    }

    /// <summary>⭐ A quarter turn takes the x axis onto the y axis, exactly.</summary>
    /// <remarks>
    /// Exact because sine and cosine are exact at the cardinals (D318) — so this is a real check on
    /// the rotation arithmetic rather than on the table's interpolation.
    /// </remarks>
    [Fact]
    public void AQuarterTurnIsExact()
    {
        var along = new Point(Fixed.FromInt(1), Fixed.Zero);
        Point turned = along.RotatedBy(Angle.FromTurnFraction(1, 4));

        Assert.Equal(Fixed.Zero, turned.X);
        Assert.Equal(Fixed.FromInt(1), turned.Y);

        // Four quarter turns come home.
        Point home = along
            .RotatedBy(Angle.FromTurnFraction(1, 4))
            .RotatedBy(Angle.FromTurnFraction(1, 4))
            .RotatedBy(Angle.FromTurnFraction(1, 4))
            .RotatedBy(Angle.FromTurnFraction(1, 4));

        Assert.Equal(along, home);
    }

    // ---------------------------------------------------------------
    //  Coverage
    // ---------------------------------------------------------------

    /// <summary>A one-tile building covers its own tile and nothing else.</summary>
    [Fact]
    public void OneTileCoversOneTile()
    {
        var at = new GridPos(4, -2);
        List<GridPos> covered = Footprint.OneTile(at).CoveredTiles();

        Assert.Single(covered);
        Assert.Equal(at, covered[0]);
    }

    /// <summary>
    /// ⭐⭐ ROTATING A ONE-TILE BUILDING CHANGES NOTHING — AT ANY ANGLE, ALL 65,536 OF THEM.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>This is the guard that licenses the whole slice to move no golden.</b> A 1×1 building's
    /// centre <em>is</em> its tile's centre, so the offset under test is zero and no rotation can
    /// move it out of its own tile. Every building in the game is 1×1
    /// (`specs/gridless.md §2.3`), so facing is free for all of them.
    /// </para>
    /// <para>
    /// ⚠️ Checked at every representable angle rather than at a handful, because *"a handful of
    /// angles"* is exactly the sampling that would miss a quadrant whose symmetry is reflected the
    /// wrong way.
    /// </para>
    /// </remarks>
    [Fact]
    public void NoRotationOfAOneTileBuildingEverLeavesItsTile()
    {
        var at = new GridPos(2, 3);

        for (int raw = 0; raw <= ushort.MaxValue; raw++)
        {
            var turned = new Footprint
            {
                Origin = at,
                Width = 1,
                Height = 1,
                Facing = Angle.FromRaw((ushort)raw),
            };

            List<GridPos> covered = turned.CoveredTiles();
            Assert.Single(covered);
            Assert.Equal(at, covered[0]);
        }
    }

    /// <summary>A three-by-one building lies along x, and a quarter turn stands it along y.</summary>
    /// <remarks>
    /// ⭐ **The guard that proves rotation does something**, and the anti-vacuity partner to the one
    /// above: without it, a `CoveredTiles` that ignored `Facing` entirely would pass every other
    /// test in this file.
    /// </remarks>
    [Fact]
    public void AThreeByOneTurnsAcross()
    {
        var at = new GridPos(0, 0);

        var lying = new Footprint { Origin = at, Width = 3, Height = 1, Facing = Angle.Zero };
        var standing = new Footprint
        {
            Origin = at,
            Width = 3,
            Height = 1,
            Facing = Angle.FromTurnFraction(1, 4),
        };

        List<GridPos> along = lying.CoveredTiles();
        List<GridPos> across = standing.CoveredTiles();

        _output.WriteLine($"lying:    {string.Join(" ", along)}");
        _output.WriteLine($"standing: {string.Join(" ", across)}");

        Assert.Equal(new[] { new GridPos(-1, 0), new GridPos(0, 0), new GridPos(1, 0) }, along);
        Assert.Equal(new[] { new GridPos(0, -1), new GridPos(0, 0), new GridPos(0, 1) }, across);
    }

    /// <summary>Turning a building right round leaves it where it was.</summary>
    [Fact]
    public void AHalfTurnIsSymmetric()
    {
        var shape = new Footprint
        {
            Origin = new GridPos(5, 5),
            Width = 3,
            Height = 1,
            Facing = Angle.Zero,
        };

        var turned = shape with { Facing = Angle.FromTurnFraction(1, 2) };

        Assert.Equal(shape.CoveredTiles(), turned.CoveredTiles());
    }

    /// <summary>⛔ The order is part of the contract, not an accident of the loop.</summary>
    [Fact]
    public void CoverageComesBackLowestTileFirst()
    {
        var shape = new Footprint
        {
            Origin = new GridPos(0, 0),
            Width = 3,
            Height = 3,
            Facing = Angle.Zero,
        };

        List<GridPos> covered = shape.CoveredTiles();

        Assert.Equal(9, covered.Count);
        for (int i = 1; i < covered.Count; i++)
        {
            bool ordered = covered[i].Y > covered[i - 1].Y
                || (covered[i].Y == covered[i - 1].Y && covered[i].X > covered[i - 1].X);

            Assert.True(ordered, $"{covered[i - 1]} then {covered[i]} is not row-major order.");
        }
    }

    /// <summary>⭐ A three-wide workplace stands on three tiles, and a data row is what says so.</summary>
    /// <remarks>
    /// <b>The plumbing guard.</b> <c>Footprint</c> could be perfect and reached by nothing — D98's
    /// rule about a number that is always zero. This asserts the path from the
    /// <c>extent_width</c> column, through <c>Workplace</c>, to the ground it occupies — and
    /// therefore to <c>SomethingStandsAt</c>, which asks the footprint now rather than the position.
    /// </remarks>
    [Fact]
    public void AThreeWideWorkplaceStandsOnThreeTiles()
    {
        var wide = new Workplace
        {
            Id = 1,
            Kind = JobKind.Forager,
            Name = "a long shed",
            Position = new GridPos(10, 10),
            Store = new Stockpile(16),
            Capacity = 1,
            ExtentWidth = 3,
            ExtentHeight = 1,
        };

        List<GridPos> covered = wide.Footprint.CoveredTiles();
        _output.WriteLine(string.Join(" ", covered));

        Assert.Equal(3, covered.Count);
        Assert.True(wide.Footprint.Covers(new GridPos(9, 10)));
        Assert.True(wide.Footprint.Covers(new GridPos(11, 10)));

        // ⭐ And an ordinary building is still one tile, so nothing that exists today moved.
        var ordinary = new Workplace
        {
            Id = 2,
            Kind = JobKind.Forager,
            Name = "an ordinary hut",
            Position = new GridPos(10, 10),
            Store = new Stockpile(16),
            Capacity = 1,
        };

        Assert.Single(ordinary.Footprint.CoveredTiles());
    }

    /// <summary>⛔ A building that stands on no ground at all is refused when the config loads.</summary>
    /// <remarks>
    /// A zero extent would make <c>CoveredTiles</c> return nothing — so the building would occupy
    /// no ground, refuse nothing, and be buildable on top of itself. Caught in the config validator
    /// rather than in the geometry, so a modder's typo names the row it is in.
    /// </remarks>
    [Fact]
    public void ABuildingMustStandOnAtLeastOneTile()
    {
        SimConfig broken = VillageFixtures.Village with
        {
            Buildings = new[]
            {
                new BuildingRow { Id = 0, Name = "a rumour of a hut", ExtentWidth = 0 },
            },
        };

        SimConfigException refused = Assert.Throws<SimConfigException>(broken.Validate);
        _output.WriteLine(refused.Message);
        Assert.Contains("at least one tile", refused.Message, StringComparison.Ordinal);
    }

    /// <summary>A building covers what it says it covers.</summary>
    [Fact]
    public void CoversAgreesWithTheList()
    {
        var shape = new Footprint
        {
            Origin = new GridPos(1, 1),
            Width = 3,
            Height = 1,
            Facing = Angle.Zero,
        };

        Assert.True(shape.Covers(new GridPos(0, 1)));
        Assert.True(shape.Covers(new GridPos(1, 1)));
        Assert.True(shape.Covers(new GridPos(2, 1)));
        Assert.False(shape.Covers(new GridPos(1, 2)));
        Assert.False(shape.Covers(new GridPos(3, 1)));
    }

    /// <summary>
    /// ⭐⭐ A THREE-TILE BUILDING REFUSES A NEIGHBOUR ON ITS SECOND TILE — end to end (D321).
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>This is the assertion that would have caught the one-square longhouse.</b> Every guard in
    /// this file tested `Footprint` in isolation and every one passed while the longhouse occupied
    /// a single tile in the actual game — because `StoreBuilding.Footprint` was written and read by
    /// nothing, and `CanBuildAt` only ever validated the anchor.
    /// </para>
    /// <para>
    /// ⛔ <b>It goes through <c>CanBuildAt</c> rather than through <c>Footprint</c></b>, deliberately.
    /// The geometry was never wrong; the wiring was. *A guard on the shape proves the shape, and
    /// this bug was everywhere except the shape.*
    /// </para>
    /// </remarks>
    [Fact]
    public void AThreeTileBuildingRefusesANeighbourOnItsSecondTile()
    {
        SimConfig config = VillageFixtures.Village;
        SimWorld world = SimFactory.CreatePhase0(config, new InMemoryLogSink()).World;

        GridPos anchor = SomewhereBuildable(world);

        // The catalogue says three wide, so the sim must believe three wide.
        Assert.Equal(3, world.BuildingsCatalog[BuildingKind.Longhouse]!.ExtentWidth);
        Assert.Equal(3, world.FootprintOf(BuildingKind.Longhouse, anchor).CoveredTiles().Count);

        Assert.True(world.Mark(BuildingKind.Longhouse, anchor).Allowed);

        // ⛔⛔ RAISED, NOT JUST MARKED — AND THE FIRST VERSION OF THIS GUARD MISSED THAT.
        // A marked building is a construction SITE, which is a `Workplace`, and workplaces already
        // went through the footprint. So the guard passed while the bug Joe actually hit — a
        // FINISHED store drawn and occupying one tile — was untouched. **Reverting the store fix
        // left it green.** *A guard that stops one step short of the state the player reaches is
        // D157's green-and-blind, and this one was blind by one call.*
        Workplace site = world.Workplaces.Single(
            w => w.Construction?.Kind == BuildingKind.Longhouse);
        BuildFixtures.StockTheSite(site);
        for (int i = 0; i <= site.Construction!.Recipe.WorkTicks; i++)
        {
            site.Construction.Work();
        }

        world.Complete(site);
        Assert.Contains(world.StoreBuildings, s => s.Position == anchor);

        // ⭐ The anchor is refused because something stands there — that much always worked.
        Assert.False(world.CanBuildAt(BuildingKind.Granary, anchor).Allowed);

        // ⛔ AND SO ARE THE OTHER TWO, which is the half that did not.
        var left = new GridPos(anchor.X - 1, anchor.Y);
        var right = new GridPos(anchor.X + 1, anchor.Y);

        _output.WriteLine(
            $"anchor {anchor}: left {world.CanBuildAt(BuildingKind.Granary, left).Reason}; "
            + $"right {world.CanBuildAt(BuildingKind.Granary, right).Reason}");

        Assert.False(
            world.CanBuildAt(BuildingKind.Granary, left).Allowed,
            "A granary was allowed on ground the longhouse already stands on.");
        Assert.False(
            world.CanBuildAt(BuildingKind.Granary, right).Allowed,
            "A granary was allowed on ground the longhouse already stands on.");

        // ⭐ ANTI-VACUITY (D7): one tile further out is still free, or this is just refusing
        // everything and proving nothing.
        Assert.True(world.CanBuildAt(BuildingKind.Granary, new GridPos(anchor.X + 2, anchor.Y)).Allowed);
    }

    /// <summary>
    /// ⭐⭐ A SITE COVERS THE SAME GROUND AS THE GHOST AND THE FINISHED BUILDING (D324).
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>The state nothing guarded, and it is the middle one.</b> D321's guard raised the
    /// building and checked the finished store; the ghost is drawn from the row. **Nobody ever
    /// asked what the site in between looked like** — and Joe found it drawn as a single square in
    /// the default orientation, for the years a longhouse takes to build.
    /// </para>
    /// <para>
    /// ⚠️ *Three pictures of one building, and the two on either side were tested.* The site is
    /// also the moment the footprint matters MOST, because it is the last point at which there is
    /// still time to move it.
    /// </para>
    /// </remarks>
    [Fact]
    public void AMarkedBuildingReservesTheSameGroundItWillStandOn()
    {
        SimConfig config = VillageFixtures.Village;
        SimWorld world = SimFactory.CreatePhase0(config, new InMemoryLogSink()).World;

        GridPos anchor = SomewhereBuildable(world);
        Angle turned = Angle.FromTurnFraction(1, 4);

        Assert.True(world.Mark(BuildingKind.Longhouse, anchor, turned).Allowed);

        Workplace site = world.Workplaces.Single(
            w => w.Construction?.Kind == BuildingKind.Longhouse);

        _output.WriteLine(
            $"site: {site.ExtentWidth}x{site.ExtentHeight} facing {site.Facing}, "
            + $"covering {site.Footprint.CoveredTiles().Count} tiles");

        // ⛔ The extent, or the site draws one square while the ghost drew three.
        Assert.Equal(3, site.ExtentWidth);
        Assert.Equal(3, site.Footprint.CoveredTiles().Count);

        // ⛔ AND THE FACING, which is the half Joe named separately: a site drawn in the default
        // orientation is a picture of a building nobody asked for.
        Assert.Equal(turned, site.Facing);

        // ⭐ Turned a quarter, so it reserves ground ALONG Y where an unturned one runs along X.
        Assert.True(site.Footprint.Covers(new GridPos(anchor.X, anchor.Y - 1)));
        Assert.True(site.Footprint.Covers(new GridPos(anchor.X, anchor.Y + 1)));
        Assert.False(site.Footprint.Covers(new GridPos(anchor.X + 1, anchor.Y)));
    }

    /// <summary>Somewhere a three-tile building genuinely fits, found rather than assumed.</summary>
    private static GridPos SomewhereBuildable(SimWorld world)
    {
        GridPos site = world.Map.FoundingSite;

        for (int radius = 2; radius < 20; radius++)
        {
            for (int dy = -radius; dy <= radius; dy++)
            {
                for (int dx = -radius; dx <= radius; dx++)
                {
                    var at = new GridPos(site.X + dx, site.Y + dy);
                    if (world.CanBuildAt(BuildingKind.Longhouse, at).Allowed
                        && world.CanBuildAt(BuildingKind.Granary, new GridPos(at.X + 2, at.Y)).Allowed)
                    {
                        return at;
                    }
                }
            }
        }

        throw new Xunit.Sdk.XunitException("Nowhere in the valley fits a three-tile building.");
    }
}

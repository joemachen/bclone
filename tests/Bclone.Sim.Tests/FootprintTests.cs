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
                Origin = Point.CentreOf(at),
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

        var lying = new Footprint { Origin = Point.CentreOf(at), Width = 3, Height = 1, Facing = Angle.Zero };
        var standing = new Footprint
        {
            Origin = Point.CentreOf(at),
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
            Origin = Point.CentreOf(new GridPos(5, 5)),
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
            Origin = Point.CentreOf(new GridPos(0, 0)),
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
            Position = Point.CentreOf(new GridPos(10, 10)),
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
            Position = Point.CentreOf(new GridPos(10, 10)),
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
            Origin = Point.CentreOf(new GridPos(1, 1)),
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
        Assert.Contains(world.StoreBuildings, s => s.Tile == anchor);

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

    /// <summary>
    /// ⭐⭐ EVERY BUILDING CLASS HONOURS ITS ROW'S EXTENT — not just the two that were needed.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Joe asked whether the longhouse treatment would reach every building, existing and
    /// future (D325).</b> The honest risk was that it reached the two classes a longhouse happens
    /// to use — workplace and store — and quietly skipped libraries, the town hall and homes,
    /// which is exactly what had already happened once: D321's message claimed the occupancy
    /// conversion was "whole" when only two of five collections had been converted.
    /// </para>
    /// <para>
    /// ⭐ <b>So this asserts the PROPERTY rather than the classes</b>: reflect over every type that
    /// can stand on the ground and require it to expose a <c>Footprint</c>. A new building class
    /// added later fails here on the day it is written, rather than on the day somebody makes one
    /// three tiles wide. *A guard that lists the classes it knows about cannot catch the class
    /// nobody thought of.*
    /// </para>
    /// </remarks>
    [Fact]
    public void EveryBuildingThatStandsOnTheGroundHasAFootprint()
    {
        Type[] standOnGround =
        {
            typeof(Workplace),
            typeof(StoreBuilding),
            typeof(Library),
            typeof(TownHall),
        };

        var missing = new List<string>();
        foreach (Type type in standOnGround)
        {
            if (type.GetProperty("Footprint")?.PropertyType != typeof(Footprint))
            {
                missing.Add(type.Name);
            }
        }

        _output.WriteLine($"{standOnGround.Length} building classes checked");

        Assert.True(
            missing.Count == 0,
            "These stand on the ground and cannot say what ground they stand on, so a multi-tile "
            + "row of their kind would occupy one tile: " + string.Join(", ", missing));
    }

    /// <summary>
    /// ⭐⭐ EVERY TILE OF A LONGHOUSE ANSWERS — selection, naming, demolition and shelter (D326).
    /// </summary>
    /// <remarks>
    /// <b>Joe: *"to select the building i have to choose the middle tile - the 1st and 3rd tiles
    /// arent selectable."*</b> `SomethingStandsAt` was footprint-aware; twelve sibling lookups were
    /// not. ⛔ `MarkDemolition` disagreed with itself — it measured the building's ANGLE and then
    /// said there was nothing there to pull down.
    /// </remarks>
    [Fact]
    public void EveryTileOfALongBuildingAnswersWhenAsked()
    {
        SimConfig config = VillageFixtures.Village;
        SimWorld world = SimFactory.CreatePhase0(config, new InMemoryLogSink()).World;

        GridPos anchor = SomewhereBuildable(world);
        Assert.True(world.Mark(BuildingKind.Longhouse, anchor).Allowed);

        Workplace site = world.Workplaces.Single(w => w.Construction?.Kind == BuildingKind.Longhouse);
        BuildFixtures.StockTheSite(site);
        for (int i = 0; i <= site.Construction!.Recipe.WorkTicks; i++)
        {
            site.Construction.Work();
        }

        world.Complete(site);

        foreach (GridPos tile in new[]
        {
            new GridPos(anchor.X - 1, anchor.Y), anchor, new GridPos(anchor.X + 1, anchor.Y),
        })
        {
            _output.WriteLine($"{tile}: {world.NameOnTheTile(tile)}");

            Assert.NotNull(world.StoreAt(tile));
            Assert.Equal(Shelter.Roof, world.ShelterAt(tile));
            Assert.Contains("longhouse", world.NameOnTheTile(tile), StringComparison.OrdinalIgnoreCase);

            // ⛔ The one Joe actually hit: the ends could not be pulled down.
            Assert.True(
                world.MarkDemolition(tile).Allowed,
                $"Tile {tile} of the longhouse could not be marked for demolition.");

            world.CancelDemolition(tile);
        }
    }

    /// <summary>⭐ A FREE building keeps its facing too — the branch D320 missed (D326).</summary>
    /// <remarks>
    /// Joe: *"for stockpile and builders hut, the ghost rotates well, but the build at the default
    /// square orientation."* `Mark` forks on whether the recipe costs anything, and only the costed
    /// branch carried the angle — `RaiseFreeBuilding` had no parameter for it and `PendingBuilding`
    /// had nowhere to put one. ⚠️ Both free rows are 1×1, so the symptom was **drawing**, not
    /// placement; it becomes a placement bug the day any multi-tile building is also free.
    /// </remarks>
    [Fact]
    public void AFreeBuildingKeepsTheFacingItWasPlacedWith()
    {
        SimConfig config = VillageFixtures.Village;
        SimWorld world = SimFactory.CreatePhase0(config, new InMemoryLogSink()).World;

        GridPos at = SomewhereBuildable(world);
        Angle turned = Angle.FromTurnFraction(1, 8);

        Assert.True(world.Mark(BuildingKind.Pile, at, turned).Allowed);

        StoreBuilding pile = world.StoreAt(at)!;
        _output.WriteLine($"the stockpile faces {pile.Facing}");

        Assert.Equal(turned, pile.Facing);
    }

    /// <summary>
    /// ⛔ <c>Covers</c> and <c>CoveredTiles</c> are the same answer, asked two ways (D329).
    /// </summary>
    /// <remarks>
    /// <c>Covers</c> stopped building the whole list to answer about one tile, because that was
    /// costing the suite four minutes. **A faster path that disagrees with the slow one is worse
    /// than the slow one**, and `Fixed` multiplication is not associative — so this compares them
    /// directly, over a spread of extents and angles, rather than trusting that the arithmetic was
    /// copied faithfully.
    /// </remarks>
    [Theory]
    [InlineData(1, 1, 0)]
    [InlineData(3, 1, 0)]
    [InlineData(3, 1, 16384)]
    [InlineData(3, 1, 8192)]
    [InlineData(2, 5, 21845)]
    [InlineData(4, 3, 40000)]
    public void TheFastCoverTestAgreesWithTheWholeList(int width, int height, int rawFacing)
    {
        var anchor = new GridPos(12, 9);
        var shape = new Footprint
        {
            Origin = Point.CentreOf(anchor),
            Width = width,
            Height = height,
            Facing = Angle.FromRaw((ushort)rawFacing),
        };

        List<GridPos> listed = shape.CoveredTiles();
        _output.WriteLine($"{width}x{height} at {rawFacing} covers {listed.Count}: "
            + string.Join(" ", listed));

        int reach = width + height + 2;
        for (int dy = -reach; dy <= reach; dy++)
        {
            for (int dx = -reach; dx <= reach; dx++)
            {
                var tile = new GridPos(anchor.X + dx, anchor.Y + dy);
                Assert.Equal(listed.Contains(tile), shape.Covers(tile));
            }
        }
    }

    /// <summary>
    /// ⭐⭐ The point test and the tile test agree wherever they are asked the same
    /// question — <b>and they are the same arithmetic, so this is a guard on the factoring</b>
    /// (D338).
    /// </summary>
    /// <remarks>
    /// <para>
    /// <c>Covers(Point)</c> was factored out of <c>StandsOn</c> so the mouse can ask where a
    /// building is DRAWN while the sim keeps asking which tiles it CLAIMS. <see cref="Fixed"/>
    /// multiplication is not associative, so *"it is the same expression"* is a claim worth
    /// checking rather than an argument.
    /// </para>
    /// <para>
    /// ⚠️ <b>Asked at tile CENTRES, where the two are defined to agree</b> — and
    /// away from centres they are supposed to differ, which is the whole reason the point test
    /// exists. The one exception is the anchor tile, which
    /// <see cref="Footprint.Covers(GridPos)"/> forgives unconditionally (D331) and the rectangle
    /// does not.
    /// </para>
    /// </remarks>
    [Theory]
    [InlineData(1, 1, 0)]
    [InlineData(3, 1, 0)]
    [InlineData(3, 1, 16384)]
    [InlineData(3, 1, 8192)]
    [InlineData(2, 5, 21845)]
    [InlineData(4, 3, 40000)]
    public void ThePointTestAgreesWithTheTileTestAtEveryTileCentre(
        int width, int height, int rawFacing)
    {
        var anchor = new GridPos(12, 9);
        var shape = new Footprint
        {
            Origin = Point.CentreOf(anchor),
            Width = width,
            Height = height,
            Facing = Angle.FromRaw((ushort)rawFacing),
        };

        int reach = width + height + 2;
        for (int dy = -reach; dy <= reach; dy++)
        {
            for (int dx = -reach; dx <= reach; dx++)
            {
                var tile = new GridPos(anchor.X + dx, anchor.Y + dy);
                if (tile == anchor)
                {
                    continue;
                }

                Assert.Equal(shape.Covers(tile), shape.Covers(Point.CentreOf(tile)));
            }
        }
    }

    /// <summary>
    /// ⛔⛔ A building turned between tile centres is clickable across its whole
    /// rectangle — <b>Joe's complaint, pinned</b> (D338).
    /// </summary>
    /// <remarks>
    /// <b>Joe: *"there are areas of a building in which clicking selects a non-building tile even
    /// though part of the building looks like it is in that spot."*</b> The rectangle reaches into
    /// tiles whose centres it does not stand on — that is D319 working correctly — so
    /// the point test must find the building somewhere the tile test does not. **If this ever
    /// stops being true the fix has quietly become a no-op.**
    /// </remarks>
    [Fact]
    public void ATurnedBuildingIsFoundWhereItIsDrawnAndNotOnlyWhereItStands()
    {
        // Half a tile north-east of a centre, turned 45°: the case D331 was found on.
        Point origin = Point.CentreOf(new GridPos(4, 4))
            + new Point(Fixed.FromRatio(1, 2), Fixed.FromRatio(1, 2));

        var shape = new Footprint
        {
            Origin = origin,
            Width = 3,
            Height = 1,
            Facing = Angle.FromTurnFraction(1, 8),
        };

        int drawnOver = 0;
        int claimed = 0;

        for (int dy = -3; dy <= 3; dy++)
        {
            for (int dx = -3; dx <= 3; dx++)
            {
                var tile = new GridPos(4 + dx, 4 + dy);

                // A grid of sample points across the tile, because "is the building drawn here?"
                // is a question about the tile's AREA and its centre is one point of it.
                bool anywhereInside = false;
                for (int sy = 0; sy < 4 && !anywhereInside; sy++)
                {
                    for (int sx = 0; sx < 4 && !anywhereInside; sx++)
                    {
                        var at = new Point(
                            Fixed.FromRatio((tile.X * 8) + 1 + (sx * 2), 8),
                            Fixed.FromRatio((tile.Y * 8) + 1 + (sy * 2), 8));

                        anywhereInside = shape.Covers(at);
                    }
                }

                if (anywhereInside)
                {
                    drawnOver++;
                }

                if (shape.Covers(tile))
                {
                    claimed++;
                }
            }
        }

        _output.WriteLine($"drawn over {drawnOver} tiles, claims {claimed}");
        Assert.True(claimed > 0, "D331: a building always stands on at least its own tile.");
        Assert.True(
            drawnOver > claimed,
            $"The rectangle should reach into tiles it does not claim, but it is drawn over "
            + $"{drawnOver} and claims {claimed} — so clicking the overhang cannot be the "
            + "thing that was fixed.");
    }

    /// <summary>
    /// ⛔ What an EVEN-width building covers when its edge lands exactly on a tile centre
    /// — <b>pinned, because nothing in the suite posed it</b> (D338).
    /// </summary>
    /// <remarks>
    /// <para>
    /// ⚠️ <b>Found by a red check that scored zero.</b> Flipping <c>&lt;=</c> to
    /// <c>&lt;</c> in the coverage test left **the entire footprint suite green** — because
    /// every posed case is odd-width or turned off-axis, and the boundary is only reachable when
    /// a building has an even extent and is square to the grid. *The only multi-tile building in
    /// the game is the 3×1 longhouse, so no content can reach it either.*
    /// </para>
    /// <para>
    /// ⛔ <b>This pins the behaviour rather than arguing with it.</b> Touching counts as
    /// covered, so a 2-wide building claims THREE tiles — the half-tile at each end lands on
    /// a neighbour centre and takes it. *That is a real design question about even extents and it
    /// is nobody urgent's, because nothing even-sided exists.* **What matters is that a future
    /// tidy-up cannot change it in silence.**
    /// </para>
    /// </remarks>
    [Fact]
    public void AnEvenWidthBuildingsEdgeLandsOnATileCentreAndTouchingCounts()
    {
        var shape = new Footprint
        {
            Origin = Point.CentreOf(new GridPos(10, 10)),
            Width = 2,
            Height = 1,
            Facing = default,
        };

        System.Collections.Generic.List<GridPos> covered = shape.CoveredTiles();
        _output.WriteLine("a 2x1 square to the grid covers " + string.Join(" ", covered));

        Assert.Equal(3, covered.Count);
        Assert.Contains(new GridPos(9, 10), covered);
        Assert.Contains(new GridPos(11, 10), covered);
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

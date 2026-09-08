using Bclone.Sim.Core;
using Bclone.Sim.Logging;
using Bclone.Sim.World;
using Xunit;
using Xunit.Abstractions;

namespace Bclone.Sim.Tests;

/// <summary>
/// ⭐⭐ Every way of asking <em>"what is on this tile?"</em> gives the same answer (D326, D328).
/// </summary>
/// <remarks>
/// <para>
/// <b>⛔⛔ THE HALF-CONVERTED LOOKUP HAS NOW HAPPENED THREE TIMES.</b> <c>SomethingStandsAt</c>,
/// then <c>FacingOfWhatStandsAt</c>, then twelve sibling methods — and D326's fix reached only
/// two of them. <b>Ten callers were still walking their own collections when free placement was
/// scoped</b>, three of them comparing <c>Position ==</c>.
/// </para>
/// <para>
/// ⚠️ <b>AND THE HONEST TALLY: THREE OF FIVE RED CHECKS BIT, TWO SCORED ZERO.</b> The two that
/// scored zero were the <em>workplace</em> arms of naming, demolition and moving — unreachable,
/// because the only multi-tile building in the game is a <b>store</b> and <c>StoreAt</c> is asked
/// first. **A guard cannot be blamed for a shape the content cannot make.** See
/// <see cref="ATurnedLonghouseSiteIsFoundOnEveryTileItReserves"/> for the one place the game does
/// produce a multi-tile workplace, and what it does catch.
/// </para>
/// <para>
/// ⭐ <b>The guard is the shape, not the list.</b> A test that names the methods it knows about
/// cannot catch the method nobody thought of — so this poses a <b>turned three-tile building</b>
/// and requires every entry point to agree about all three of its tiles. **The anchor is the tile
/// a broken finder still gets right**, which is exactly why the middle one is not enough on its
/// own: D326's bug was that the first and third tiles were invisible.
/// </para>
/// </remarks>
public sealed class TileFindersAgreeTests
{
    private readonly ITestOutputHelper _output;

    public TileFindersAgreeTests(ITestOutputHelper output) => _output = output;

    private static SimWorld World() =>
        SimFactory.CreatePhase0(VillageFixtures.Village, new InMemoryLogSink()).World;

    /// <summary>
    /// ⭐⭐ Every tile of a turned longhouse answers the same way to every question.
    /// </summary>
    /// <remarks>
    /// The store is a <see cref="BuildingKind.Longhouse"/> because it is the only building in the
    /// game that is not one tile — and a 1×1 covers its own tile at every one of 65,536 angles, so
    /// **a village of one-tile buildings cannot tell a working finder from a broken one.**
    /// </remarks>
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    public void EveryTileOfATurnedBuildingAnswersEveryQuestionTheSameWay(int quarters)
    {
        SimWorld world = World();
        GridPos anchor = SomewhereALonghouseFits(world);
        Angle turned = Angle.FromTurnFraction(quarters, 4);

        Assert.True(world.Mark(BuildingKind.Longhouse, anchor, turned).Allowed);

        Workplace site = world.Workplaces.Single(
            w => w.Construction?.Kind == BuildingKind.Longhouse);
        world.Complete(site);

        StoreBuilding raised = TheLonghouse(world);
        System.Collections.Generic.List<GridPos> covered = raised.Footprint.CoveredTiles();

        _output.WriteLine(
            $"a longhouse at {anchor} turned {turned} covers "
            + string.Join(", ", covered));

        Assert.Equal(3, covered.Count);

        foreach (GridPos tile in covered)
        {
            Assert.True(world.SomethingStandsAt(tile), $"{tile}: nothing stands there");
            Assert.NotNull(world.StoreAt(tile));
            Assert.NotNull(world.WhatStandsAt(tile));
            Assert.NotEqual("it", world.NameOnTheTile(tile));
            Assert.Equal(turned, world.FacingOfWhatStandsAt(tile));
            Assert.Equal(Shelter.Roof, world.ShelterAt(tile));

            // It stands: it is neither a site being raised nor one coming down.
            Assert.Null(world.SiteAt(tile));
            Assert.Null(world.DemolitionSiteAt(tile));

            // ⛔ And nothing may be raised on any tile of it — the check that made D321's
            // longhouse buildable-over on two of its three tiles.
            Assert.False(
                world.CanBuildAt(BuildingKind.Granary, tile).Allowed,
                $"{tile}: a granary was allowed on ground a longhouse stands on");
        }
    }

    /// <summary>
    /// ⛔⛔ A longhouse is marked for demolition from any of its tiles — <b>D326's own bug</b>.
    /// </summary>
    /// <remarks>
    /// Joe: *"to select the building i have to choose the middle 'tile' — the 1st and 3rd tiles
    /// aren't selectable"*. <c>MarkDemolition</c> asks <c>FacingOfWhatStandsAt</c> and then
    /// <c>NameOfWhatStandsAt</c>, and **the two disagreed**: it measured a building's angle and
    /// then said there was nothing there to pull down. This is that sentence, guarded.
    /// </remarks>
    [Theory]
    [InlineData(0)]
    [InlineData(2)]
    public void ALonghouseIsPulledDownFromAnyTileOfIt(int whichTile)
    {
        SimWorld world = World();
        GridPos anchor = SomewhereALonghouseFits(world);

        Assert.True(world.Mark(BuildingKind.Longhouse, anchor).Allowed);
        world.Complete(world.Workplaces.Single(w => w.Construction?.Kind == BuildingKind.Longhouse));

        GridPos tile = TheLonghouse(world).Footprint.CoveredTiles()[whichTile];
        PlacementVerdict asked = world.MarkDemolition(tile);

        _output.WriteLine($"asked to pull down from {tile}: {asked.Allowed} — {asked.Reason}");

        Assert.True(asked.Allowed, asked.Reason);
        Assert.NotNull(world.DemolitionSiteAt(tile));
    }

    /// <summary>
    /// ⭐⭐ A longhouse SITE is the only multi-tile <b>workplace</b> the game can produce.
    /// </summary>
    /// <remarks>
    /// <para>
    /// ⛔⛔ <b>AND THAT IS WHY THREE OF THIS SLICE'S RED CHECKS SCORED ZERO.</b> Every finished
    /// multi-tile building in this game is a <see cref="StoreBuilding"/> — the longhouse is a
    /// storehouse (D321) — and <c>StoreAt</c> is footprint-aware and is asked <em>first</em>. So
    /// restoring <c>Position ==</c> to the <b>workplace</b> arms of <c>NameOfWhatStandsAt</c>,
    /// <c>PullDownWhatStandsAt</c> and <c>MoveWhatStandsAt</c> leaves every guard green: **those
    /// arms are unreachable for the only shape that could tell them apart.**
    /// </para>
    /// <para>
    /// ⭐ <b>A construction site is the exception, and it is a real one.</b> <c>RaiseSiteFor</c>
    /// gives the site its extent from the row, so a marked longhouse genuinely is a three-tile
    /// <see cref="Workplace"/> standing in a real valley — and the finders that do not filter out
    /// sites can therefore be told apart. *That is the fixture where the two differ (D218), found
    /// rather than built.*
    /// </para>
    /// </remarks>
    [Fact]
    public void ATurnedLonghouseSiteIsFoundOnEveryTileItReserves()
    {
        SimWorld world = World();
        GridPos anchor = SomewhereALonghouseFits(world);
        Angle turned = Angle.FromTurnFraction(1, 4);

        Assert.True(world.Mark(BuildingKind.Longhouse, anchor, turned).Allowed);

        Workplace site = world.Workplaces.Single(
            w => w.Construction?.Kind == BuildingKind.Longhouse);

        _output.WriteLine($"the site is {site.ExtentWidth}x{site.ExtentHeight} facing {site.Facing}");
        Assert.Equal(3, site.ExtentWidth);

        foreach (GridPos tile in site.Footprint.CoveredTiles())
        {
            Assert.True(world.SomethingStandsAt(tile), $"{tile}: the site reserves nothing there");
            Assert.NotNull(world.SiteAt(tile));
            Assert.Equal(site.Id, world.WorkplaceCovering(tile)?.Id);
            Assert.Equal(turned, world.FacingOfWhatStandsAt(tile));

            // ⛔ Nothing may be marked on ground a site has already reserved.
            Assert.False(
                world.CanBuildAt(BuildingKind.Granary, tile).Allowed,
                $"{tile}: a granary was allowed on ground a longhouse site reserves");
        }
    }

    /// <summary>
    /// ⛔ A turned longhouse can be MOVED from any of its tiles, not only its anchor.
    /// </summary>
    /// <remarks>
    /// <b><c>MoveWhatStandsAt</c> matched on <c>Position ==</c> for libraries, the town hall and
    /// workplaces</b> — so this is the guard that reddens the day somebody restores an exact
    /// comparison, and the reason it matters is that <see cref="Point"/> makes that comparison a
    /// test of sixty-four bits of fraction.
    /// </remarks>
    [Fact]
    public void ABuildingIsMovedFromAnyTileItStandsOn()
    {
        SimWorld world = World();
        GridPos anchor = SomewhereALonghouseFits(world);

        Assert.True(world.Mark(BuildingKind.Longhouse, anchor).Allowed);
        world.Complete(world.Workplaces.Single(w => w.Construction?.Kind == BuildingKind.Longhouse));

        StoreBuilding raised = TheLonghouse(world);
        GridPos farEnd = raised.Footprint.CoveredTiles()[^1];
        Assert.NotEqual(anchor, farEnd);

        var somewhereElse = new GridPos(anchor.X, anchor.Y + 4);
        PlacementVerdict asked = world.MarkRelocation(farEnd, somewhereElse);

        _output.WriteLine($"asked to move from the far end {farEnd}: {asked.Allowed}");
        Assert.True(asked.Allowed, asked.Reason);
    }

    /// <summary>
    /// ⭐ A moved building arrives facing the way it left (D325's rule, one door down — D328).
    /// </summary>
    [Fact]
    public void AMovedBuildingKeepsTheAngleItWasTurnedTo()
    {
        SimWorld world = World();
        GridPos anchor = SomewhereALonghouseFits(world);
        Angle turned = Angle.FromTurnFraction(1, 4);

        Assert.True(world.Mark(BuildingKind.Longhouse, anchor, turned).Allowed);
        world.Complete(world.Workplaces.Single(w => w.Construction?.Kind == BuildingKind.Longhouse));

        var somewhereElse = new GridPos(anchor.X + 6, anchor.Y);
        Assert.True(world.MarkRelocation(anchor, somewhereElse).Allowed, "the move was refused");

        Workplace moving = world.Workplaces.Single(w => w.Construction?.MovingFrom is not null);

        _output.WriteLine($"the site it will move into faces {moving.Facing}, from {turned}");

        Assert.Equal(turned, moving.Facing);
        Assert.Equal(turned, moving.Construction!.Facing);
    }

    /// <summary>
    /// ⛔ The verdict and the ghost are answers about the same shape (D328).
    /// </summary>
    /// <remarks>
    /// <c>CanBuildAt</c> took no <see cref="Angle"/> while <c>Mark</c>, <c>FootprintOf</c> and the
    /// ghost all carried one, so a turned longhouse was validated unturned. **Posed against a
    /// position where the two genuinely differ** — a spot where the building fits one way round
    /// and not the other — because a pose where every angle fits proves nothing (D7).
    /// </remarks>
    [Fact]
    public void TheVerdictIsAboutTheBuildingAtTheAngleItWillStand()
    {
        SimWorld world = World();
        GridPos anchor = SomewhereALonghouseFits(world);
        Angle turned = Angle.FromTurnFraction(1, 4);

        // Put something in the way of the TURNED building's far end, which the unturned one
        // never reaches. The anchor itself stays clear, so only the facing decides the answer.
        var inTheWay = new GridPos(anchor.X, anchor.Y + 1);
        Assert.True(world.Mark(BuildingKind.Granary, inTheWay).Allowed, "nothing was put in the way");

        bool flat = world.CanBuildAt(BuildingKind.Longhouse, anchor).Allowed;
        bool onItsSide = world.CanBuildAt(BuildingKind.Longhouse, anchor, facing: turned).Allowed;

        _output.WriteLine($"unturned: {flat}, turned a quarter: {onItsSide}");

        Assert.True(flat, "the unturned building should still fit");
        Assert.False(onItsSide, "the turned building reaches into the granary and was allowed");
    }

    /// <summary>The longhouse that now stands — found by its extent, which is what makes it one.</summary>
    private static StoreBuilding TheLonghouse(SimWorld world)
    {
        foreach (StoreBuilding store in world.StoreBuildings)
        {
            if (store.ExtentWidth == 3)
            {
                return store;
            }
        }

        throw new Xunit.Sdk.XunitException("No three-tile store stands in the valley.");
    }

    /// <summary>Somewhere a three-tile building fits with room around it, found rather than assumed.</summary>
    private static GridPos SomewhereALonghouseFits(SimWorld world)
    {
        GridPos site = world.Map.FoundingSite;

        for (int radius = 2; radius < 25; radius++)
        {
            for (int dy = -radius; dy <= radius; dy++)
            {
                for (int dx = -radius; dx <= radius; dx++)
                {
                    var at = new GridPos(site.X + dx, site.Y + dy);

                    // Room to turn it, room to move it, and room to put something in its way.
                    if (world.CanBuildAt(BuildingKind.Longhouse, at).Allowed
                        && world.CanBuildAt(BuildingKind.Longhouse, at, facing: Angle.FromTurnFraction(1, 4)).Allowed
                        && world.CanBuildAt(BuildingKind.Granary, new GridPos(at.X, at.Y + 1)).Allowed
                        && world.CanBuildAt(BuildingKind.Granary, new GridPos(at.X, at.Y + 4)).Allowed
                        && world.CanBuildAt(BuildingKind.Granary, new GridPos(at.X + 6, at.Y)).Allowed)
                    {
                        return at;
                    }
                }
            }
        }

        throw new Xunit.Sdk.XunitException("Nowhere in the valley fits a three-tile building.");
    }
}

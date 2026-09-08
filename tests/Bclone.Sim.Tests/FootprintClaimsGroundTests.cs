using Bclone.Sim.Core;
using Bclone.Sim.Logging;
using Bclone.Sim.World;
using Xunit;
using Xunit.Abstractions;

namespace Bclone.Sim.Tests;

/// <summary>
/// ⛔⛔ A building always stands on ground — <b>Joe's woodcutter's hut, guarded</b> (D331).
/// </summary>
/// <remarks>
/// <para>
/// <b>He placed a hut between four tiles, turned, and it became a ghost in the machine:</b> it could
/// not be selected, no builder ever went to it, and the log shows *"woodcutter's hut 1 was marked
/// out"* at tick 0 and **nothing about it again in 348 ticks** while a builder stood idle with 130
/// logs.
/// </para>
/// <para>
/// ⭐⭐ <b>ONE CAUSE, AND IT IS GEOMETRY.</b> D319's rule is *a building covers the tiles whose
/// CENTRES it stands on*. A unit square reliably contains a point of a unit lattice only while it is
/// <b>axis-aligned</b>; turned 45° its axis-aligned reach falls to 1/√2 ≈ 0.707, so a 1×1 sitting
/// between tile centres slips past all four and <see cref="Footprint.CoveredTiles"/> comes back
/// <b>empty</b>. **On the grid this was unreachable** — every building sat on a tile centre — and
/// free placement made it reachable the same afternoon.
/// </para>
/// <para>
/// ⛔ <b>Everything else followed from the empty list.</b> All twelve "what is here?" finders go
/// through <c>Covers</c>; so does <c>SiteAt</c>, and D108 means the builder reads the site from the
/// tile they are standing on — so they arrived and there was nothing there. And <c>CanBuildAt</c>
/// raised no objection because its refusal loop iterates the covered tiles, so an <em>empty</em> list
/// means the loop body never runs. *A check that iterates a set says nothing about the empty set.*
/// </para>
/// </remarks>
public sealed class FootprintClaimsGroundTests
{
    private readonly ITestOutputHelper _output;

    public FootprintClaimsGroundTests(ITestOutputHelper output) => _output = output;

    /// <summary>Half a tile, in the units the origin is measured in.</summary>
    private static Fixed HalfTile => Fixed.FromRatio(1, 2);

    /// <summary>
    /// ⛔⛔ A one-tile building claims ground <b>at every angle and from anywhere inside its tile</b>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>The sweep is the point.</b> A single pose would have been passed by the broken code at
    /// most angles — the failure needs the building to be turned <em>and</em> off centre, which is
    /// exactly the combination nothing had ever produced before free placement. **256 angles × nine
    /// offsets, because the bug lives in the corners of that space.**
    /// </para>
    /// <para>
    /// ⚠️ It asserts <b>non-empty</b> rather than a count: the count is legitimately 1, 2 or 4
    /// depending on whether the centre lands exactly on a tile boundary, and pinning it would be
    /// pinning arithmetic rather than the rule.
    /// </para>
    /// </remarks>
    [Fact]
    public void AOneTileBuildingAlwaysStandsOnAtLeastOneTile()
    {
        var offsets = new[] { -1, 0, 1 };
        int worstAngle = -1;
        int emptied = 0;

        foreach (int stepX in offsets)
        {
            foreach (int stepY in offsets)
            {
                // Just inside each corner and edge of the tile, and its centre.
                Point origin = Point.CentreOf(new GridPos(4, 7))
                    + new Point(
                        Fixed.FromRatio(stepX * 2047, 4096),
                        Fixed.FromRatio(stepY * 2047, 4096));

                for (int raw = 0; raw < 65536; raw += 256)
                {
                    var shape = new Footprint
                    {
                        Origin = origin,
                        Width = 1,
                        Height = 1,
                        Facing = Angle.FromRaw((ushort)raw),
                    };

                    if (shape.CoveredTiles().Count == 0)
                    {
                        emptied++;
                        worstAngle = worstAngle < 0 ? raw : worstAngle;
                    }
                }
            }
        }

        _output.WriteLine(emptied == 0
            ? "every pose claimed ground"
            : $"{emptied} poses claimed NOTHING, first at {Angle.FromRaw((ushort)worstAngle)}");

        Assert.Equal(0, emptied);
    }

    /// <summary>
    /// ⭐ The tile it claims is <b>the one its centre is in</b> — the sentence a player can be told.
    /// </summary>
    [Theory]
    [InlineData(0)]
    [InlineData(8192)]
    [InlineData(16384)]
    [InlineData(40000)]
    public void ABuildingStandsOnTheTileItsCentreIsIn(int raw)
    {
        var tile = new GridPos(-3, 5);

        // Well inside the tile, so there is no boundary case to argue about.
        Point origin = Point.CentreOf(tile) + new Point(
            Fixed.FromRatio(1, 8), Fixed.FromRatio(-1, 8));

        var shape = new Footprint
        {
            Origin = origin,
            Width = 1,
            Height = 1,
            Facing = Angle.FromRaw((ushort)raw),
        };

        _output.WriteLine($"at {Angle.FromRaw((ushort)raw)} it covers "
            + string.Join(" ", shape.CoveredTiles()));

        Assert.Contains(tile, shape.CoveredTiles());
        Assert.True(shape.Covers(tile), "the fast path disagrees with the list");
    }

    /// <summary>
    /// ⛔⛔ Joe's hut, end to end: marked between four tiles and turned, and <b>a builder finds it</b>.
    /// </summary>
    /// <remarks>
    /// <c>SiteAt</c> is the call a builder makes on arrival (D108 — the site is read from the tile
    /// they are standing on, never from a field on them). **This is the assertion that would have
    /// caught the whole thing**, and it is about the sim rather than about geometry.
    /// </remarks>
    [Fact]
    public void AHutMarkedBetweenFourTilesCanBeFoundAndBuilt()
    {
        SimWorld world = SimFactory.CreatePhase0(
            VillageFixtures.Village, new InMemoryLogSink()).World;

        GridPos tile = SomewhereClear(world);

        // The middle of four squares — the corner they share — turned an eighth of a turn.
        Point between = Point.CentreOf(tile) + new Point(HalfTile, HalfTile);
        Angle turned = Angle.FromTurnFraction(1, 8);

        PlacementVerdict marked = world.Mark(BuildingKind.WoodcutterHut, between, turned);
        _output.WriteLine($"marked at {between} turned {turned}: {marked.Allowed} {marked.Reason}");
        Assert.True(marked.Allowed, marked.Reason);

        Workplace site = world.Workplaces.Single(
            w => w.Construction?.Kind == BuildingKind.WoodcutterHut);

        System.Collections.Generic.List<GridPos> claimed = site.Footprint.CoveredTiles();
        _output.WriteLine($"it claims {claimed.Count}: {string.Join(" ", claimed)}");

        Assert.NotEmpty(claimed);

        foreach (GridPos on in claimed)
        {
            // ⛔ The builder's own question, asked where they would be standing.
            Assert.NotNull(world.SiteAt(on));

            // And every way the player has of pointing at it.
            Assert.True(world.SomethingStandsAt(on));
            Assert.Equal(site.Id, world.WorkplaceCovering(on)?.Id);
        }
    }

    /// <summary>
    /// ⛔⛔ Two buildings on ADJACENT tiles both fit — <b>touching is apart</b>.
    /// </summary>
    /// <remarks>
    /// <b>This is the guard the collision test could most easily have broken, and it would have
    /// broken every village at once.</b> Two 1×1 buildings on neighbouring tile centres are exactly
    /// one apart with radii summing to exactly one, so a strict separating-axis test calls them an
    /// overlap and refuses ground the game has always allowed. **`>=` separates**, and every golden
    /// in the suite depends on it.
    /// </remarks>
    [Theory]
    [InlineData(1, 0)]
    [InlineData(0, 1)]
    [InlineData(1, 1)]
    public void BuildingsOnNeighbouringTilesDoNotCollide(int dx, int dy)
    {
        SimWorld world = SimFactory.CreatePhase0(
            VillageFixtures.Village, new InMemoryLogSink()).World;

        GridPos tile = SomewhereClear(world);
        var beside = new GridPos(tile.X + dx, tile.Y + dy);

        Assert.True(world.Mark(BuildingKind.WoodcutterHut, tile).Allowed);

        PlacementVerdict second = world.Mark(BuildingKind.WoodcutterHut, beside);
        _output.WriteLine($"{tile} then {beside}: {second.Allowed} {second.Reason}");

        Assert.True(second.Allowed, second.Reason);
    }

    /// <summary>
    /// ⭐⭐ Two freely-placed huts cannot stand on top of each other, <b>even claiming different
    /// tiles</b>.
    /// </summary>
    /// <remarks>
    /// The case tile occupancy could not see: a hair either side of a tile boundary, each claiming
    /// its own tile, drawn overlapping. `gridless.md §7.3` promised *"collision becomes geometry
    /// rather than is this tile taken"* — this is the assertion that says it is kept.
    /// </remarks>
    [Fact]
    public void TwoFreelyPlacedBuildingsCannotShareTheSameGround()
    {
        SimWorld world = SimFactory.CreatePhase0(
            VillageFixtures.Village, new InMemoryLogSink()).World;

        GridPos tile = SomewhereClear(world);
        Point first = Point.CentreOf(tile) + new Point(Fixed.FromRatio(1, 4), Fixed.Zero);

        Assert.True(world.Mark(BuildingKind.WoodcutterHut, first).Allowed);

        // A quarter tile further on: a different tile centre is nearer it, so tile occupancy
        // would have called this free ground — and the two rectangles plainly share space.
        Point second = first + new Point(Fixed.FromRatio(1, 2), Fixed.Zero);
        Assert.Equal(tile, first.ToTile());
        Assert.NotEqual(tile, second.ToTile());

        PlacementVerdict onTop = world.Mark(BuildingKind.WoodcutterHut, second);
        _output.WriteLine($"{first} then {second} (tiles {first.ToTile()} and {second.ToTile()}): "
            + $"{onTop.Allowed} {onTop.Reason}");

        Assert.False(onTop.Allowed, "a second hut was allowed on top of the first");
    }

    /// <summary>Ground with room around it, found rather than assumed.</summary>
    private static GridPos SomewhereClear(SimWorld world)
    {
        GridPos site = world.Map.FoundingSite;

        for (int radius = 2; radius < 25; radius++)
        {
            for (int dy = -radius; dy <= radius; dy++)
            {
                for (int dx = -radius; dx <= radius; dx++)
                {
                    var at = new GridPos(site.X + dx, site.Y + dy);
                    if (world.CanBuildAt(BuildingKind.WoodcutterHut, at).Allowed
                        && world.CanBuildAt(BuildingKind.WoodcutterHut, new GridPos(at.X + 1, at.Y)).Allowed
                        && world.CanBuildAt(BuildingKind.WoodcutterHut, new GridPos(at.X, at.Y + 1)).Allowed
                        && world.CanBuildAt(BuildingKind.WoodcutterHut, new GridPos(at.X + 1, at.Y + 1)).Allowed)
                    {
                        return at;
                    }
                }
            }
        }

        throw new Xunit.Sdk.XunitException("Nowhere in the valley has four clear tiles together.");
    }
}

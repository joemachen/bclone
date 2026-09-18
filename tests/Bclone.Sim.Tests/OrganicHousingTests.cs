using Bclone.Sim.Config;
using Bclone.Sim.Core;
using Bclone.Sim.Determinism;
using Bclone.Sim.Logging;
using Bclone.Sim.World;
using Xunit;
using Xunit.Abstractions;

namespace Bclone.Sim.Tests;

/// <summary>
/// Organic housing — a home is a house in a plot, facing a lane (D386, `specs/organic-housing.md`).
/// </summary>
/// <remarks>
/// Joe's picture, from a <i>Foundation</i> screenshot: *each in its own fenced irregular yard,
/// packed like fields, with dirt lanes between them, houses facing the lane.* These guards read
/// the plot layer the chooser writes and the geometry it is derived from.
/// </remarks>
public sealed class OrganicHousingTests
{
    private readonly ITestOutputHelper _output;

    public OrganicHousingTests(ITestOutputHelper output) => _output = output;

    private static SimConfig Config => VillageFixtures.Village;

    private static SimWorld Bare()
    {
        SimWorld world = SimFactory.CreatePhase0(Config, new InMemoryLogSink()).World;
        ZoneMap zones = world.Zones;
        for (int i = 0; i < zones.Residential.Count; i++)
        {
            if (zones.Residential[i])
            {
                zones.SetResidential(zones.PositionOf(i), false);
            }
        }

        return world;
    }

    /// <summary>A bare, reachable square of grass of this half-width about a centre, at least this far from a site.</summary>
    private static GridPos ABareSquareAtLeast(SimWorld world, GridPos site, int tilesAway, int half)
    {
        for (int radius = tilesAway; radius < tilesAway + 20; radius++)
        {
            for (int dy = -radius; dy <= radius; dy++)
            {
                for (int dx = -radius; dx <= radius; dx++)
                {
                    var at = new GridPos(site.X + dx, site.Y + dy);
                    if (at.ManhattanDistanceTo(site) < tilesAway || !world.TravelCost.CanReach(site, at))
                    {
                        continue;
                    }

                    bool bare = true;
                    for (int by = -half; by <= half && bare; by++)
                    {
                        for (int bx = -half; bx <= half && bare; bx++)
                        {
                            var tile = new GridPos(at.X + bx, at.Y + by);
                            bare = world.Map.Contains(tile)
                                && world.Map.TerrainAt(tile) == Terrain.Grass
                                && !world.SomethingStandsAt(tile);
                        }
                    }

                    if (bare)
                    {
                        return at;
                    }
                }
            }
        }

        throw new Xunit.Sdk.XunitException($"No bare reachable square of half-width {half} near {site}.");
    }

    /// <summary>A roofless household added to the village, so <c>MarkHome</c> has somebody to site for.</summary>
    private static Household ANewFamily(SimWorld world, string name)
    {
        var family = new Household
        {
            Stockpile = world.NewStockpile(),
            Id = 900 + world.Households.Count,
            Name = name,
        };
        world.Households.Add(family);
        return family;
    }

    private static void Paint(SimWorld world, GridPos centre, int half)
    {
        for (int dy = -half; dy <= half; dy++)
        {
            for (int dx = -half; dx <= half; dx++)
            {
                world.Zones.SetResidential(new GridPos(centre.X + dx, centre.Y + dy), true);
            }
        }
    }

    /// <summary>
    /// ⭐ A turned 2×1 covers exactly its two tiles at every facing, and the pair is the plot's
    /// house pair (§5: the D382 anchor rule applied to the turned extent).
    /// </summary>
    /// <remarks>
    /// D331 showed the centre rule slipping on a turned building; an even extent anchored west
    /// and then turned a quarter claims four tiles. Red with <c>HomeAnchorOn</c> reading the extent
    /// unturned: the east and west facings cover four.
    /// </remarks>
    [Fact]
    public void ATurnedHouseCoversExactlyItsTwoTiles()
    {
        SimWorld world = Bare();
        var front = new GridPos(3, 5);
        foreach (Angle facing in PlotShape.Facings)
        {
            Footprint house = world.HomeFootprintAt(front, facing);
            List<GridPos> covered = house.CoveredTiles();
            PlotShape plot = world.PlotFor(front, facing, householdId: 7);

            _output.WriteLine($"facing {facing}: {string.Join(" ", covered)}; door {plot.Door}");
            Assert.Equal(2, covered.Count);
            Assert.Contains(front, covered);
            foreach (GridPos tile in plot.House)
            {
                Assert.Contains(tile, covered);
            }

            // The tile it is filed under is the front tile — every finder keys on it (D382).
            Assert.Equal(front, house.Origin.ToTile());

            // The door is the lane tile the facing points at, and the view's convention agrees:
            // the local north edge turned by the facing is the lane direction.
            GridPos toLane = PlotShape.LaneDirection(facing);
            Assert.Equal(new GridPos(front.X + toLane.X, front.Y + toLane.Y), plot.Door);
            Point turned = new Point(Fixed.Zero, -Fixed.One).RotatedBy(facing);
            Assert.Equal(Fixed.FromInt(toLane.X), turned.X);
            Assert.Equal(Fixed.FromInt(toLane.Y), turned.Y);
        }
    }

    /// <summary>
    /// ⭐ A home is a house in a plot: 3×3 tiles of the household's, the house on the front row,
    /// and the lane across the front nobody's (§3.1–3.2).
    /// </summary>
    [Fact]
    public void AHomeIsAHouseInAPlotWithALaneAcrossItsFront()
    {
        SimWorld world = Bare();
        GridPos centre = ABareSquareAtLeast(world, world.Map.FoundingSite, 1, 2);
        Paint(world, centre, 2);

        Household family = ANewFamily(world, "Ashford");
        HomeSite site = Household.ChooseSite(world, world.Map.FoundingSite, family.Id);
        world.MarkHome(family.Id, site);
        _output.WriteLine($"{site.Front} facing {site.Facing}: {site.WhyHere}");

        PlotShape plot = world.PlotFor(site.Front, site.Facing, family.Id);
        Assert.Equal(Config.PlotWidth * Config.PlotDepth, plot.Tiles.Count);
        foreach (GridPos tile in plot.Tiles)
        {
            Assert.Equal(family.Id, world.Zones.PlotOwner(tile));
        }

        foreach (GridPos tile in plot.Lane)
        {
            Assert.Equal(0, world.Zones.PlotOwner(tile));
            Assert.True(world.Zones.IsLane(tile), $"{tile} across the front is not a lane.");
        }

        Assert.Equal(site.WhyHere, family.WhyHere);
        Assert.Contains("facing the lane", family.WhyHere);
    }

    /// <summary>
    /// ⛔ The plot is the household's from the marking (§3.4): a second family choosing next day
    /// takes none of it and none of its lane — and stands beside it, sharing a side (§3.3).
    /// </summary>
    /// <remarks>
    /// ⚠️ <b>Scored ZERO against the <i>apart</i> term (D386), and the zero is written down:</b>
    /// on this open square the walks alone put the second plot beside the first, so the term is
    /// not what this guard proves. What it proves is the claim itself — no tile of the second
    /// plot is the first's ground or lane, and the chooser's sentence names the neighbour. The
    /// term's own measurement is in `organic-housing.md §7`: over twelve seeds and fifty years it
    /// moves 18 of 67 houses beside a neighbour to 22 of 69 — a nudge at two tiles, and Joe's to
    /// widen once he has seen the rows.
    /// </remarks>
    [Fact]
    public void TheNextPlotSharesASideAndKeepsOffTheLane()
    {
        SimWorld world = Bare();
        GridPos centre = ABareSquareAtLeast(world, world.Map.FoundingSite, 1, 5);
        Paint(world, centre, 5);

        Household first = ANewFamily(world, "Ashford");
        Household second = ANewFamily(world, "Byrne");
        HomeSite a = Household.ChooseSite(world, world.Map.FoundingSite, first.Id);
        world.MarkHome(first.Id, a);
        HomeSite b = Household.ChooseSite(world, world.Map.FoundingSite, second.Id);
        world.MarkHome(second.Id, b);
        _output.WriteLine($"first {a.Front} facing {a.Facing}: {a.WhyHere}");
        _output.WriteLine($"second {b.Front} facing {b.Facing}: {b.WhyHere}");

        PlotShape plotA = world.PlotFor(a.Front, a.Facing, first.Id);
        PlotShape plotB = world.PlotFor(b.Front, b.Facing, second.Id);

        foreach (GridPos tile in plotB.Tiles)
        {
            Assert.DoesNotContain(tile, plotA.Tiles);
            Assert.DoesNotContain(tile, plotA.Lane);
        }

        bool touches = false;
        foreach (IReadOnlyList<GridPos> side in plotB.Beside)
        {
            foreach (GridPos tile in side)
            {
                touches |= world.Zones.PlotOwner(tile) == first.Id;
            }
        }

        Assert.True(touches, "The second plot does not share a side with the first.");
        Assert.Contains($"beside the {first.Name}s", b.WhyHere);
    }

    /// <summary>
    /// ⭐ Which side of its front row the house sits on is a hash of the household, not a draw:
    /// the same valley twice sites the same houses, and two households differ.
    /// </summary>
    [Fact]
    public void TheHouseSitsLeftOrRightByHashNotByDraw()
    {
        int near = 0;
        for (int id = 1; id <= 64; id++)
        {
            if (PlotShape.HouseOnTheNearSide(id))
            {
                near++;
            }
        }

        _output.WriteLine($"{near} of 64 households have the house at the near end of the row.");
        Assert.InRange(near, 16, 48);

        SimWorld once = SimFactory.CreatePhase0(Config, new InMemoryLogSink()).World;
        SimWorld twice = SimFactory.CreatePhase0(Config, new InMemoryLogSink()).World;
        for (int i = 0; i < once.Households.Count; i++)
        {
            Assert.Equal(once.Households[i].HomePosition, twice.Households[i].HomePosition);
            Assert.Equal(once.Households[i].HomeFacing, twice.Households[i].HomeFacing);
        }
    }

    /// <summary>The facing is in the hash (§4): turn a house and the fingerprint moves.</summary>
    [Fact]
    public void TheFacingIsHashed()
    {
        SimWorld world = SimFactory.CreatePhase0(Config, new InMemoryLogSink()).World;
        Household family = world.Households[0];
        Assert.True(family.HasHome, "The founders have no house, so there is nothing to turn.");

        ulong before = StateHash.Compute(world);
        family.HomeFacing += Angle.Right;
        ulong after = StateHash.Compute(world);
        Assert.NotEqual(before, after);
    }

    /// <summary>
    /// A dead family's house hands its plot on with it (§3.5), and a pulled-down house frees
    /// the plot and the lane.
    /// </summary>
    [Fact]
    public void APlotGoesWithTheHouseAndComesFreeWithIt()
    {
        SimLoop loop = SimFactory.CreatePhase0(Config, new InMemoryLogSink());
        SimWorld world = loop.World;
        Household family = world.Households[0];
        Assert.True(family.HasHome);
        GridPos front = family.HomeTile!.Value;
        PlotShape plot = world.PlotFor(front, family.HomeFacing, family.Id);
        Assert.Equal(family.Id, world.Zones.PlotOwner(front));

        // The founders' houses stand at the founding, so their plots are claimed there too.
        int claimed = 0;
        foreach (Household household in world.Households)
        {
            if (household.HomeTile is GridPos tile && world.Zones.PlotOwner(tile) == household.Id)
            {
                claimed++;
            }
        }

        Assert.Equal(world.Households.Count, claimed);

        // Hand it on: the roofless family takes the empty house, and the plot with it.
        var heir = new Household { Stockpile = world.NewStockpile(), Id = 900, Name = "Heir" };
        world.Zones.HandPlotOn(family.Id, heir.Id);
        foreach (GridPos tile in plot.Tiles)
        {
            Assert.Equal(heir.Id, world.Zones.PlotOwner(tile));
        }

        Assert.True(world.Zones.IsLane(plot.Door));

        // And free: nothing owns the ground, the lane is a lane no more.
        Assert.Equal(plot.Tiles.Count, world.Zones.ReleasePlot(heir.Id));
        foreach (GridPos tile in plot.Tiles)
        {
            Assert.Equal(0, world.Zones.PlotOwner(tile));
        }

        Assert.False(world.Zones.IsLane(plot.Door));
    }

    /// <summary>
    /// The fixture village takes plots as it grows — four or more by year forty — and how many
    /// lane tiles have a house facing back across them is read out, not asserted: whether rows
    /// form is the picture, and the picture is Joe's (D352).
    /// </summary>
    [Fact]
    public void TheVillageTakesPlotsAsItGrows()
    {
        SimLoop loop = SimFactory.CreatePhase0(Config, new InMemoryLogSink());
        SimWorld world = loop.World;

        // Let the fixture build itself for a while: the founders' houses and whatever follows.
        loop.Step(Config.TicksPerYear * 40);

        int facingPairs = 0;
        int plots = 0;
        foreach (Household a in world.Households)
        {
            if (a.HomeTile is not GridPos frontA)
            {
                continue;
            }

            plots++;
            PlotShape plotA = world.PlotFor(frontA, a.HomeFacing, a.Id);
            foreach (GridPos lane in plotA.Lane)
            {
                // Who is across this lane tile — the plot beyond it, one further along the facing.
                GridPos toLane = PlotShape.LaneDirection(a.HomeFacing);
                var across = new GridPos(lane.X + toLane.X, lane.Y + toLane.Y);
                int owner = world.Zones.PlotOwner(across);
                if (owner == 0 || owner == a.Id || world.FindHousehold(owner) is not Household b || !b.HasHome)
                {
                    continue;
                }

                GridPos theirs = PlotShape.LaneDirection(b.HomeFacing);
                if (theirs.X == -toLane.X && theirs.Y == -toLane.Y)
                {
                    facingPairs++;
                }
            }
        }

        _output.WriteLine($"{plots} plots at year 40; {facingPairs} lane tiles with a house facing back across them.");
        Assert.True(plots >= 4, $"Only {plots} plots in forty years — nothing to read.");
    }
}

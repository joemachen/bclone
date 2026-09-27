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
    internal static GridPos ABareSquareAtLeast(SimWorld world, GridPos site, int tilesAway, int half)
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
        foreach (Angle facing in PlotShape.Quarters)
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
        Assert.True(plot.Tiles.Count > plot.House.Count, "The plot is the house alone — no yard.");
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
        Assert.Contains("facing ", family.WhyHere);
    }

    /// <summary>
    /// ⛔ The plot is the household's from the marking (§3.4): a second family choosing next day
    /// takes none of it and none of its lane.
    /// </summary>
    /// <remarks>
    /// D386 asserted the second plot also SHARED A SIDE with the first — the packing D404 and D411
    /// took away (§9.5 P2: a hashed gap between yards). The claim is what is left, and it is the
    /// half that was always the point.
    /// </remarks>
    [Fact]
    public void TheNextPlotKeepsOffTheFirstsGroundAndLane()
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
    }

    /// <summary>
    /// ⭐ A household's yard, setback and gap are a hash of the household, not a draw (§9.5): every
    /// value each range holds is reached, and the same valley twice sites the same houses.
    /// </summary>
    [Fact]
    public void TheYardIsHashedNotDrawn()
    {
        foreach ((int salt, IReadOnlyList<int> values) in new[]
        {
            (PlotShape.SideSalt, Config.HomeYardSideQuarters),
            (PlotShape.OtherSideSalt, Config.HomeYardOtherSideQuarters),
            (PlotShape.BackSalt, Config.HomeYardBackQuarters),
            (PlotShape.SetbackSalt, Config.HomeSetbackQuarters),
            (PlotShape.GapSalt, Config.HomeGapTiles),
        })
        {
            var seen = new HashSet<int>();
            for (int id = 1; id <= 64; id++)
            {
                seen.Add(PlotShape.ByHash(id, salt, values));
            }

            _output.WriteLine($"salt {salt}: {string.Join(", ", seen.OrderBy(v => v))} of {string.Join(", ", values)}");
            Assert.Equal(values.Count, seen.Count);
        }

        SimWorld once = SimFactory.CreatePhase0(Config, new InMemoryLogSink()).World;
        SimWorld twice = SimFactory.CreatePhase0(Config, new InMemoryLogSink()).World;
        for (int i = 0; i < once.Households.Count; i++)
        {
            Assert.Equal(once.Households[i].HomePosition, twice.Households[i].HomePosition);
            Assert.Equal(once.Households[i].HomeFacing, twice.Households[i].HomeFacing);
            Assert.Equal(once.Households[i].FencedTiles, twice.Households[i].FencedTiles);
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

        // Hand it on: the roofless family takes the empty house, and the plot with it. ⚠️ The plot
        // is what was FENCED (D388), not the proposal's rectangle — since D411 a turned yard is
        // clipped by the founding's buildings more often than a square one was, and this guard
        // walked the rectangle.
        var heir = new Household { Stockpile = world.NewStockpile(), Id = 900, Name = "Heir" };
        world.Zones.HandPlotOn(family.Id, heir.Id);
        foreach (GridPos tile in family.FencedTiles)
        {
            Assert.Equal(heir.Id, world.Zones.PlotOwner(tile));
        }

        Assert.True(world.Zones.IsLane(plot.Door));

        // And free: nothing owns the ground, the lane is a lane no more.
        Assert.Equal(family.FencedTiles.Count, world.Zones.ReleasePlot(heir.Id));
        foreach (GridPos tile in family.FencedTiles)
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

    // ---------------------------------------------------------------
    //  D411 — a village, not a street (`organic-housing.md §9`)
    // ---------------------------------------------------------------

    /// <summary>A painted square far from the village's walks, with ground worn along these tiles.</summary>
    /// <remarks>
    /// ⚠️ Eight tiles each way, and the paths posed well inside it. The chooser takes the shortest
    /// walk, which is the paint's edge nearest the village; a path run out to that edge puts the
    /// best site where its yard hangs over unpainted ground, and the square facing that loses less
    /// of it is right to win — found by <c>AHouseFacesThePathInFrontOfIt</c>'s first run.
    /// </remarks>
    private static (SimWorld World, GridPos Centre) ASquareWithAPath(Func<GridPos, IEnumerable<GridPos>> path)
    {
        SimWorld world = Bare();
        GridPos centre = ABareSquareAtLeast(world, world.Map.FoundingSite, 12, 6);
        Paint(world, centre, 8);
        foreach (GridPos tile in path(centre))
        {
            world.Paths.Tread(tile, 200);
        }

        return (world, centre);
    }

    /// <summary>Site this many families one after another, as a growing village would.</summary>
    private List<HomeSite> SiteFamilies(SimWorld world, int families)
    {
        var sites = new List<HomeSite>();
        for (int i = 0; i < families; i++)
        {
            Household family = ANewFamily(world, "F" + i);
            HomeSite site = Household.ChooseSite(world, world.Map.FoundingSite, family.Id);
            world.MarkHome(family.Id, site);
            _output.WriteLine($"{family.Name} at {site.Front} facing {site.Facing.Raw}: {site.WhyHere}");
            sites.Add(site);
        }

        return sites;
    }

    /// <summary>The smaller way round between two facings, in raw turns (65,536 a whole turn).</summary>
    private static int TurnBetween(Angle a, Angle b)
    {
        int turn = (ushort)(a.Raw - b.Raw);
        return System.Math.Min(turn, 65536 - turn);
    }

    /// <summary>
    /// ⭐⭐ Houses face the path in front of them, at whatever angle it lies — not at a compass point
    /// (§9.5 P1, Joe with a Foundation screenshot: every house turned to the path before it).
    /// </summary>
    /// <remarks>
    /// <para>
    /// A worn path on the diagonal through an open square, and six families. Every house's door
    /// looks at a path — a step along its facing, from one to eight tiles out, lands within a tile of
    /// worn ground or of a lane some house already fronts (P1's paths) — and at least half are
    /// turned off the four quarters. Red with the facing
    /// snapped to the quarters, which is D388's chooser: no house off them.
    /// </para>
    /// <para>
    /// ⚠️ <b>"At the path", not "square to it", and a rate, not the first house.</b> The first
    /// draft asserted the first family faced the diagonal square on and failed against a right
    /// answer twice: the chooser takes the shortest walk — the paint's edge nearest the village —
    /// and from there the nearest stretch of the path lies at 79°, not 45° (D344: a claim about one
    /// case is a claim about that case).
    /// </para>
    /// </remarks>
    [Fact]
    public void HousesFaceThePathInFrontOfThem()
    {
        (SimWorld world, _) = ASquareWithAPath(c =>
            Enumerable.Range(-7, 15).Select(k => new GridPos(c.X + k, c.Y + k)));

        List<HomeSite> sites = SiteFamilies(world, 6);
        int offTheQuarters = 0;
        foreach (HomeSite site in sites)
        {
            if (site.Facing.Raw % 16384 != 0)
            {
                offTheQuarters++;
            }

            Point from = Point.CentreOf(site.Front);
            Point step = new Point(Fixed.Zero, -Fixed.FromInt(1)).RotatedBy(site.Facing);
            bool looksAtIt = false;
            for (int k = 1; k <= 8 && !looksAtIt; k++)
            {
                GridPos ahead = (from + new Point(step.X * Fixed.FromInt(k), step.Y * Fixed.FromInt(k))).ToTile();
                for (int dy = -1; dy <= 1 && !looksAtIt; dy++)
                {
                    for (int dx = -1; dx <= 1 && !looksAtIt; dx++)
                    {
                        var near = new GridPos(ahead.X + dx, ahead.Y + dy);
                        looksAtIt = world.Paths.At(near) >= world.Config.PathWornAt || world.Zones.IsLane(near);
                    }
                }
            }

            Assert.True(looksAtIt, $"The house at {site.Front}, facing {site.Facing.Raw}, looks at no path: {site.WhyHere}");
        }

        _output.WriteLine($"{offTheQuarters} of {sites.Count} turned off the four quarters.");
        Assert.True(offTheQuarters * 2 >= sites.Count, $"Only {offTheQuarters} of {sites.Count} houses are turned off a compass point.");
    }

    /// <summary>
    /// ⭐⭐ Houses along a bend turn with it (§9.5 P1): a village on a curved path faces many ways,
    /// and no one way is most of them.
    /// </summary>
    /// <remarks>
    /// A quarter circle of worn ground through the square and a dozen families. Stated over the
    /// dozen, not house by house (D344: a rate, not an "every"). Red with the facing snapped to the
    /// four quarters: four facings at most.
    /// </remarks>
    [Fact]
    public void HousesAlongABendTurnWithIt()
    {
        (SimWorld world, _) = ASquareWithAPath(c =>
        {
            var arc = new List<GridPos>();
            for (int step = 0; step <= 24; step++)
            {
                double at = step * System.Math.PI / 48;
                arc.Add(new GridPos(
                    c.X - 6 + (int)System.Math.Round(10 * System.Math.Sin(at)),
                    c.Y + 6 - (int)System.Math.Round(10 * (1 - System.Math.Cos(at)))));
            }

            return arc;
        });

        List<HomeSite> sites = SiteFamilies(world, 12);
        var faced = sites.GroupBy(s => s.Facing.Raw).Select(g => g.Count()).ToList();
        _output.WriteLine($"{faced.Count} facings among {sites.Count}; the most common holds {faced.Max()}");

        Assert.True(faced.Count >= 6, $"A dozen houses on a bend face only {faced.Count} ways.");
        Assert.True(faced.Max() * 2 <= sites.Count, $"{faced.Max()} of {sites.Count} face one way — a street.");
    }

    /// <summary>
    /// ⭐⭐ No row of three along a line (§9.2's column of five, Joe: *"NOT uniform rows of housing"*).
    /// </summary>
    /// <remarks>
    /// A straight worn path across the square and a dozen families, every one of whom would face it
    /// square on: no house has two others on its front line (within ¾ of a tile of it and six tiles
    /// along), facing its way. ⚠️ <b>It guards the outcome, not a term.</b> D412 built a row price
    /// (§9.5 P3) and this guard scored ZERO against it — as did the whole 18-seed layout arm: facing
    /// the path and the gap between yards already break rows. Joe: *"delete it."* What would turn this
    /// red is the lane-first facing D388 had.
    /// </remarks>
    [Fact]
    public void NoThirdHouseInALine()
    {
        (SimWorld world, _) = ASquareWithAPath(c =>
            Enumerable.Range(-8, 17).Select(k => new GridPos(c.X + k, c.Y)));

        SiteFamilies(world, 12);
        var houses = new List<(Point Centre, Angle Facing, string Name)>();
        foreach (Household household in world.Households)
        {
            foreach (Workplace place in world.Workplaces)
            {
                if (place.Construction is { Kind: BuildingKind.Home } plan && plan.ForHouseholdId == household.Id)
                {
                    houses.Add((place.Position, plan.Facing, household.Name));
                }
            }
        }

        foreach ((Point centre, Angle facing, string name) in houses)
        {
            int inLine = 0;
            foreach ((Point other, Angle theirs, string _) in houses)
            {
                if (other == centre || TurnBetween(theirs, facing) > 4096)
                {
                    continue;
                }

                Point local = (other - centre).RotatedBy(-facing);
                Fixed x = local.X < Fixed.Zero ? -local.X : local.X;
                Fixed y = local.Y < Fixed.Zero ? -local.Y : local.Y;
                if (y <= Fixed.FromRatio(3, 4) && x <= Fixed.FromInt(6))
                {
                    inLine++;
                }
            }

            Assert.True(inLine <= 1, $"The {name}s' house has {inLine} others on its front line, facing its way — a row.");
        }
    }

    /// <summary>
    /// ⭐ Where there is room, every house has a yard (§9.5 P4) — a turned rectangle rasterised on
    /// the grid does not get to leave a family with none.
    /// </summary>
    /// <remarks>
    /// Found on the first picture: most houses by a diagonal path stood with no yard, because a yard
    /// no gate reaches was dropped at no cost. Now an unreached yard tile is priced as a clipped one,
    /// and a facing that keeps half its yard is taken before one that does not. Red with the
    /// unreached tiles left out of <c>TilesClippedOff</c>.
    /// </remarks>
    [Fact]
    public void EveryHouseHasAYardWhereThereIsRoom()
    {
        (SimWorld world, _) = ASquareWithAPath(c =>
            Enumerable.Range(-6, 13).Select(k => new GridPos(c.X + k, c.Y + k)));

        SiteFamilies(world, 6);
        foreach (Household household in world.Households)
        {
            if (household.Id < 900)
            {
                continue;
            }

            int ground = world.Zones.PlotOf(household.Id).Count;
            _output.WriteLine($"{household.Name}: {ground} tiles of plot");
            Assert.True(ground >= 4, $"The {household.Name}s' house stands with {ground - 2} tiles of yard.");
        }
    }

    /// <summary>
    /// With no path in reach, the first houses face the village — the paths start there (§9.5 P1).
    /// </summary>
    /// <remarks>
    /// Red with the no-path branch gone: the direction is empty and the house faces north whatever
    /// the village's bearing.
    /// </remarks>
    [Fact]
    public void AHouseWithNoPathNearFacesTheVillage()
    {
        (SimWorld world, _) = ASquareWithAPath(_ => Array.Empty<GridPos>());
        HomeSite site = SiteFamilies(world, 1)[0];

        // ⚠️ The bearing itself, to the 1/64 turn, not "anywhere toward it": the first draft scored
        // ZERO against the no-path branch removed, because a house facing north passed whenever the
        // village lay anywhere north of the square.
        GridPos village = world.Map.FoundingSite;
        double bearing = System.Math.Atan2(village.X - site.Front.X, -(village.Y - site.Front.Y));
        int expected = (int)System.Math.Round(bearing / (2 * System.Math.PI) * 64) & 63;
        int got = site.Facing.Raw / 1024;
        int apart = System.Math.Min((got - expected) & 63, (expected - got) & 63);
        Assert.True(apart <= 1, $"The house at {site.Front} faces {got}/64, not the village at {village} ({expected}/64).");
        Assert.Contains("facing the village", site.WhyHere);
    }

    /// <summary>
    /// ⛔ The fence is what was built (D388): fixed the day the house is marked, a log a yard
    /// tile on the recipe, unmoved by the brush afterwards, and refunded with the house.
    /// </summary>
    /// <remarks>
    /// Joe: *"it feels too malleable."* A yard tile the paint had not reached on the marking day
    /// is outside the fence for good, even painted the day after; a yard tile fenced that day
    /// stays fenced when its paint goes. Red with the fence re-read from the paint: the
    /// painted-after tile joins the plot.
    /// </remarks>
    [Fact]
    public void TheFenceIsWhatWasBuilt()
    {
        SimWorld world = Bare();
        GridPos centre = ABareSquareAtLeast(world, world.Map.FoundingSite, 1, 3);
        Paint(world, centre, 3);

        Household family = ANewFamily(world, "Ashford");
        HomeSite site = Household.ChooseSite(world, world.Map.FoundingSite, family.Id);
        PlotShape plot = world.PlotFor(site.Front, site.Facing, family.Id);

        // One yard tile unpainted on the marking day.
        GridPos bare = plot.Tiles[plot.Tiles.Count - 1];
        Assert.DoesNotContain(bare, plot.House);
        world.Zones.SetResidential(bare, false);

        world.MarkHome(family.Id, site);
        List<GridPos> fenced = new(family.FencedTiles);
        _output.WriteLine($"fenced {fenced.Count} of {plot.Tiles.Count}: {string.Join(" ", fenced)}; bare {bare}");
        Assert.DoesNotContain(bare, fenced);
        Assert.Equal(plot.Tiles.Count - 1, fenced.Count);

        // The recipe carries the fence: a log a yard tile over the catalogue's house.
        Workplace siteOf = world.HomeSiteFor(family.Id)!;
        int houseLogs = 0;
        foreach (MaterialCost cost in BuildingRecipe.For(BuildingKind.Home, world.Config).Materials)
        {
            if (cost.Goods == Goods.Logs) { houseLogs = cost.Amount; }
        }

        int siteLogs = 0;
        foreach (MaterialCost cost in siteOf.Construction!.Recipe.Materials)
        {
            if (cost.Goods == Goods.Logs) { siteLogs = cost.Amount; }
        }

        int yardTiles = fenced.Count - plot.House.Count;
        Assert.Equal(houseLogs + (yardTiles * world.Config.FenceLogsPerTile), siteLogs);
        Assert.Contains($"{yardTiles} tiles of fence", siteOf.Construction.Name);

        // Painted the day after: still outside. Unpainted a fenced yard tile: still inside.
        world.Zones.SetResidential(bare, true);
        GridPos yard = fenced[fenced.Count - 1];
        Assert.DoesNotContain(yard, plot.House);
        world.EraseResidential(yard);
        Assert.Equal(fenced, family.FencedTiles);
        Assert.Equal(0, world.Zones.PlotOwner(bare));
        Assert.Equal(family.Id, world.Zones.PlotOwner(yard));
        Assert.NotNull(world.HomeSiteFor(family.Id));
        Assert.False(world.HomeSiteFor(family.Id)!.Construction!.Demolishing, "unpainting the yard marked the house for demolition");

        // And the fence is in the hash.
        ulong before = StateHash.Compute(world);
        family.FencedTiles.RemoveAt(family.FencedTiles.Count - 1);
        Assert.NotEqual(before, StateHash.Compute(world));
    }
}

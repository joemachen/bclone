using Bclone.Sim.Config;
using Bclone.Sim.Core;
using Bclone.Sim.Determinism;
using Bclone.Sim.Logging;
using Bclone.Sim.World;
using Xunit;
using Xunit.Abstractions;

namespace Bclone.Sim.Tests;

/// <summary>
/// The well (D427, `specs/organic-housing.md §9.12`): a building households walk to for water, and
/// the far end of the lane their feet wear.
/// </summary>
public sealed class WellTests
{
    private readonly ITestOutputHelper _output;

    public WellTests(ITestOutputHelper output) => _output = output;

    private static SimConfig Config => VillageFixtures.Village;

    private static SimLoop Loop(SimConfig? config = null) =>
        SimFactory.CreatePhase0(config ?? Config, new InMemoryLogSink());

    // -----------------------------------------------------------------
    //  The building
    // -----------------------------------------------------------------

    /// <summary>
    /// ⛔ Drawing water is a reason to exist — <b>the validator takes a row that does nothing else</b>,
    /// and still refuses one that does nothing at all.
    /// </summary>
    [Fact]
    public void DrawingWaterIsAReasonForABuildingToExist()
    {
        BuildingRow well = Config.BuildingRows.Single(r => r.Id == (int)BuildingKind.Well);
        Assert.True(well.DrawsWater);
        Assert.Null(well.Stores);
        Assert.Equal(0, well.Shelves);
        Assert.False(well.Civic);
        Assert.Single(Config.BuildingRows, r => r.DrawsWater);

        // The shipped catalogue validates with it in.
        Config.Validate();

        // And with the column taken off, the same row is a building that does nothing.
        var rows = Config.BuildingRows
            .Select(r => r.Id == (int)BuildingKind.Well ? r with { DrawsWater = false } : r)
            .ToList();
        SimConfigException blew = Assert.Throws<SimConfigException>(
            () => (Config with { Buildings = rows }).Validate());
        _output.WriteLine(blew.Message);
        Assert.Contains("well", blew.Message, System.StringComparison.Ordinal);
    }

    /// <summary>
    /// ⭐ A finished well stands: it is on the occupancy index, nothing may be built on it, and the
    /// finders name it.
    /// </summary>
    [Fact]
    public void AFinishedWellStandsInTheWay()
    {
        SimWorld world = Loop().World;

        Well well = ObstacleTests.RaiseAWellNearTheFounding(world);

        Assert.True(world.StandsOn(well.Tile));
        Assert.False(world.CanBuildAt(BuildingKind.Well, well.Tile).Allowed);
        Assert.False(world.CanBuildAt(BuildingKind.Granary, well.Tile).Allowed);
        Assert.Equal(BuildingKind.Well, world.WhatStandsAt(well.Tile));
        Assert.Equal(well.Position, world.StandingPlaceAt(well.Tile));
    }

    /// <summary>⭐ A well can be moved by a crew, like any building that is not a house.</summary>
    [Fact]
    public void AWellCanBeMoved()
    {
        SimWorld world = Loop().World;
        Well well = ObstacleTests.RaiseAWellNearTheFounding(world);
        GridPos from = well.Tile;

        Well elsewhere = ObstacleTests.RaiseAWellNearTheFounding(world, from: 6);
        GridPos to = elsewhere.Tile;
        world.Demolish(elsewhere);

        Assert.True(world.MarkRelocation(from, to).Allowed);
        FinishTheSiteAt(world, to);

        Assert.Single(world.Wells);
        Assert.Equal(to, world.Wells[0].Tile);
        Assert.False(world.StandsOn(from));
        Assert.True(world.StandsOn(to));
    }

    /// <summary>⭐ A well can be pulled down by a crew, and hands a share of its materials back.</summary>
    [Fact]
    public void AWellCanBePulledDown()
    {
        var sink = new InMemoryLogSink();
        SimWorld world = SimFactory.CreatePhase0(Config, sink).World;
        Well well = ObstacleTests.RaiseAWellNearTheFounding(world);
        GridPos at = well.Tile;

        Assert.True(world.MarkDemolition(at).Allowed);
        Workplace site = world.DemolitionSiteAt(at)!;
        while (!site.Construction!.IsFinished)
        {
            site.Construction.Work();
        }

        world.Complete(site);

        Assert.Empty(world.Wells);
        Assert.False(world.StandsOn(at));
        Assert.Null(world.WhatStandsAt(at));
        Assert.Contains(sink.Entries, e => e.Message.Contains("was pulled down", System.StringComparison.Ordinal)
            && e.Message.Contains("Well 1 was pulled down — ", System.StringComparison.Ordinal));
    }

    /// <summary>
    /// ⛔ Sparse: a village with no well hashes as it did before wells existed, and one with a well
    /// does not.
    /// </summary>
    [Fact]
    public void AWellIsInTheHashAndNoWellIsNot()
    {
        SimWorld world = Loop().World;
        ulong none = StateHash.Compute(world);

        // Posed directly, so nothing but the list differs — a site raised and retired would leave
        // a workplace id behind, which is a different fact about the village.
        var well = new Well
        {
            Position = Point.CentreOf(world.Map.FoundingSite),
            Name = "well 1",
            Kind = BuildingKind.Well,
        };
        world.Wells.Add(well);
        ulong one = StateHash.Compute(world);
        Assert.NotEqual(none, one);

        world.Wells.Clear();
        Assert.Equal(none, StateHash.Compute(world));
    }

    // -----------------------------------------------------------------
    //  The water trip
    // -----------------------------------------------------------------

    /// <summary>One water trip, as the ticks saw it.</summary>
    private sealed record Trip(int VillagerId, int HouseholdId, int Day, bool LeftFromHome, bool DrewAtTheWell, int DrawTicks, bool WentHome, bool BrokeOffForShelter = false);

    /// <summary>
    /// A fixture village with a well raised near the founding, run for some years, and every trip
    /// anybody made written down tick by tick.
    /// </summary>
    private static (SimWorld World, Well Well, List<Trip> Trips, int MostOutAtOnce) RunWithAWell(
        int years, SimConfig? config = null, int wellFrom = 3)
    {
        SimLoop loop = Loop(config);
        SimWorld world = loop.World;
        loop.Step(world.Config.TicksPerYear);
        Well well = ObstacleTests.RaiseAWellNearTheFounding(world, wellFrom);

        var trips = new List<Trip>();
        var open = new Dictionary<int, Trip>();
        var before = new Dictionary<int, VillagerState>();
        var stoodOn = new Dictionary<int, GridPos>();
        int mostOutAtOnce = 0;
        foreach (Villager v in world.Villagers)
        {
            before[v.Id] = v.State;
            stoodOn[v.Id] = v.Tile;
        }

        for (int t = 0; t < world.Config.TicksPerYear * years; t++)
        {
            loop.StepOnce();
            var outByHousehold = new Dictionary<int, int>();
            foreach (Villager v in world.Villagers)
            {
                VillagerState was = before.TryGetValue(v.Id, out VillagerState w) ? w : VillagerState.Idle;
                VillagerState now = v.State;
                before[v.Id] = now;
                GridPos from = stoodOn.TryGetValue(v.Id, out GridPos f) ? f : v.Tile;
                stoodOn[v.Id] = v.Tile;

                if (now is VillagerState.WalkingToTheWell or VillagerState.DrawingWater)
                {
                    outByHousehold[v.HouseholdId] = outByHousehold.GetValueOrDefault(v.HouseholdId) + 1;
                }

                if (was != VillagerState.WalkingToTheWell && now is VillagerState.WalkingToTheWell or VillagerState.DrawingWater
                    && !open.ContainsKey(v.Id))
                {
                    // Set off this tick — one step already taken, so "from home" is where they stood
                    // the tick before.
                    Household h = world.HouseholdOf(v);
                    open[v.Id] = new Trip(v.Id, v.HouseholdId, h.WaterDrawnOnDay,
                        from == world.RestingPlaceOf(v), false, 0, false);
                }

                if (open.TryGetValue(v.Id, out Trip? trip))
                {
                    if (now == VillagerState.DrawingWater)
                    {
                        open[v.Id] = trip with
                        {
                            DrewAtTheWell = trip.DrewAtTheWell || v.Tile == well.Tile,
                            DrawTicks = trip.DrawTicks + 1,
                        };
                    }
                    else if (now != VillagerState.WalkingToTheWell)
                    {
                        open.Remove(v.Id);
                        trips.Add(trip with
                        {
                            WentHome = now is VillagerState.TravelingHome or VillagerState.Resting,
                            BrokeOffForShelter = now == VillagerState.SeekingShelter,
                        });
                    }
                }
            }

            foreach (int n in outByHousehold.Values)
            {
                mostOutAtOnce = System.Math.Max(mostOutAtOnce, n);
            }
        }

        return (world, well, trips, mostOutAtOnce);
    }

    /// <summary>
    /// ⭐ A water trip is home → the well → home: they draw on the well for <c>well_draw_ticks</c>
    /// and turn for home.
    /// </summary>
    [Fact]
    public void TheTripIsHomeWellHome()
    {
        (SimWorld world, Well well, List<Trip> trips, _) = RunWithAWell(years: 2);

        _output.WriteLine($"{trips.Count} trips to {well.Name} at {well.Tile}; drawn {well.Draws} times");
        Assert.True(trips.Count >= 10, $"only {trips.Count} trips in two years");
        Assert.All(trips, t => Assert.True(t.LeftFromHome, $"villager {t.VillagerId} set off from somewhere that is not home"));

        // Every trip that got as far as the well drew for the whole spell there and went home —
        // ⚠️ unless the cold broke it off (D475): exposure outranks an errand (D45), and a drawer who
        // turns dangerously cold mid-spell goes for shelter. Under the scattered seams the fixture's
        // second winter did that once (1 tick drawn, then `SeekingShelter`). Such a trip is counted
        // and drawn, and must have gone for shelter — nowhere else.
        List<Trip> drawn = trips.Where(t => t.DrawTicks > 0 && !t.BrokeOffForShelter).ToList();
        Assert.NotEmpty(drawn);
        Assert.All(drawn, t => Assert.True(t.DrewAtTheWell, "drew somewhere that is not the well"));
        // At least the spell — a meal taken at the well holds the count for a tick, because eating
        // runs above the action's clock (`ActOne`), which is the order `HungerStillComesFirst` guards.
        Assert.All(drawn, t => Assert.InRange(t.DrawTicks, world.Config.WellDrawTicks, world.Config.WellDrawTicks + 1));
        Assert.All(drawn, t => Assert.True(t.WentHome, "left the well for somewhere that is not home"));
        Assert.Equal(drawn.Count, well.Draws); // a spell the cold broke off is not a draw
    }

    /// <summary>
    /// ⛔ One trip per household per <c>water_trip_every_days</c>, and never two housemates out at
    /// once — at the interval shipped and at the shortest there is.
    /// </summary>
    [Theory]
    [InlineData(10)]
    [InlineData(1)]
    public void OneTripPerHouseholdPerInterval(int everyDays)
    {
        SimConfig config = Config with { WaterTripEveryDays = everyDays };
        (_, _, List<Trip> trips, int mostOutAtOnce) = RunWithAWell(years: 2, config);

        Assert.True(trips.Count >= 10, $"only {trips.Count} trips in two years");
        Assert.Equal(1, mostOutAtOnce);

        foreach (IGrouping<int, Trip> household in trips.GroupBy(t => t.HouseholdId))
        {
            int[] days = household.Select(t => t.Day).OrderBy(d => d).ToArray();
            for (int i = 1; i < days.Length; i++)
            {
                Assert.True(days[i] - days[i - 1] >= everyDays,
                    $"household {household.Key} went on day {days[i - 1]} and again on day {days[i]}");
            }
        }
    }

    /// <summary>⛔ No well, no trip — and nothing about the household's water is ever written.</summary>
    [Fact]
    public void NoWellNoTrip()
    {
        SimLoop loop = Loop();
        SimWorld world = loop.World;
        for (int t = 0; t < world.Config.TicksPerYear * 5; t++)
        {
            loop.StepOnce();
            Assert.DoesNotContain(world.Villagers,
                v => v.State is VillagerState.WalkingToTheWell or VillagerState.DrawingWater);
        }

        Assert.All(world.Households, h => Assert.Equal(0, h.WaterDrawnOnDay));
    }

    /// <summary>
    /// ⛔ Hunger still comes first: somebody on their way to the well who gets hungry eats that tick.
    /// </summary>
    [Fact]
    public void HungerStillComesFirst()
    {
        SimLoop loop = Loop();
        SimWorld world = loop.World;
        loop.Step(world.Config.TicksPerYear);
        ObstacleTests.RaiseAWellNearTheFounding(world);

        Villager? walker = null;
        for (int t = 0; t < world.Config.TicksPerYear && walker is null; t++)
        {
            loop.StepOnce();
            walker = world.Villagers.FirstOrDefault(v => v.State == VillagerState.WalkingToTheWell
                && world.FoodIn(world.HouseholdOf(v).Stockpile) > 0);
        }

        Assert.NotNull(walker);
        walker!.Hunger = world.Config.EatThreshold;
        loop.StepOnce();

        _output.WriteLine($"{walker.Name}: hunger {walker.Hunger}, {walker.State}");
        Assert.True(walker.Hunger < world.Config.EatThreshold, "walked on hungry");
    }

    /// <summary>
    /// ⭐⭐ THE POINT OF THE SLICE: the water trips wear the ground round the well into a path, where
    /// the same village with the same well and no trips leaves it grass.
    /// </summary>
    /// <remarks>
    /// ⚠️ <b>The control keeps the well and stops the trips</b> (an interval nobody reaches, so each
    /// household goes once). Its first draft compared against a village with no well at all and
    /// scored zero with the trips switched off: a standing well bends everybody else's walks round
    /// it, and at three tiles from the founding that alone wears the ring beside it.
    /// </remarks>
    [Fact]
    public void APathWearsToTheWell()
    {
        (SimWorld world, Well well, List<Trip> trips, _) = RunWithAWell(years: 2, wellFrom: AwayFromTheCore);
        (SimWorld still, Well same, List<Trip> once, _) = RunWithAWell(
            years: 2, Config with { WaterTripEveryDays = 1_000_000 }, AwayFromTheCore);
        Assert.Equal(well.Tile, same.Tile);

        int worn = WornBeside(world, well.Tile);
        int wornWithout = WornBeside(still, well.Tile);
        int wear = WearBeside(world, well.Tile);
        int wearWithout = WearBeside(still, well.Tile);
        _output.WriteLine($"{trips.Count} trips against {once.Count}; worn tiles beside the well {worn} against {wornWithout}; "
            + $"wear beside it {wear} against {wearWithout}");

        Assert.True(worn >= 1, "no worn tile beside the well");
        Assert.True(wear >= wearWithout * 2, $"the trips did not double the wear beside the well ({wear} against {wearWithout})");
    }

    /// <summary>How far out the path guard puts its well — out of the founding's own traffic.</summary>
    /// <remarks>
    /// ⚠️ <b>Measured, not picked.</b> Three tiles out, the founding's own walks had already worn
    /// the ring to a path, and 30 trips against 2 moved it 811 → 880 — the guard could not tell
    /// them apart. From five to ten tiles out the ring is grass without the trips (wear 0) and worn
    /// with them (104–282); seven is in the middle of that.
    /// </remarks>
    private const int AwayFromTheCore = 7;

    /// <summary>The wear on the ring round <paramref name="at"/>, summed.</summary>
    private static int WearBeside(SimWorld world, GridPos at)
    {
        int wear = 0;
        for (int dy = -1; dy <= 1; dy++)
        {
            for (int dx = -1; dx <= 1; dx++)
            {
                if (dx != 0 || dy != 0)
                {
                    wear += world.Paths.At(new GridPos(at.X + dx, at.Y + dy));
                }
            }
        }

        return wear;
    }

    /// <summary>Tiles in the ring round <paramref name="at"/> worn to a path.</summary>
    private static int WornBeside(SimWorld world, GridPos at)
    {
        int worn = 0;
        for (int dy = -1; dy <= 1; dy++)
        {
            for (int dx = -1; dx <= 1; dx++)
            {
                if ((dx != 0 || dy != 0)
                    && world.Paths.At(new GridPos(at.X + dx, at.Y + dy)) >= world.Config.PathWornAt)
                {
                    worn++;
                }
            }
        }

        return worn;
    }

    /// <summary>Deliver a site's materials and work it out, as a crew would.</summary>
    private static void FinishTheSiteAt(SimWorld world, GridPos site)
    {
        Workplace found = world.Workplaces.Last(w => w.Tile == site && w.IsSite);
        ConstructionSite plan = found.Construction!;
        foreach (MaterialCost owed in plan.Recipe.Materials)
        {
            plan.Deliver(owed.Goods, owed.Amount);
        }

        while (!plan.IsFinished)
        {
            plan.Work();
        }

        world.Complete(found);
    }
}

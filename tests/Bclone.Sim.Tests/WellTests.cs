using Bclone.Sim.Config;
using Bclone.Sim.Core;
using Bclone.Sim.Determinism;
using Bclone.Sim.Logging;
using Bclone.Sim.Persistence;
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
    /// <remarks>Water: the household's jar as they set off and as the trip closed, and how many lived there as they set off (D778).
    /// The river (D779): whether it was a trip to the bank, whether the drawing stood on a bank tile, the walks to the nearest
    /// well and bank from home as they set off (−1 for none), and the ticks spent walking out.</remarks>
    private sealed record Trip(int VillagerId, int HouseholdId, int Day, bool LeftFromHome, bool DrewAtTheWell, int DrawTicks, bool WentHome,
        bool BrokeOffForShelter = false, int WaterAtSetOff = 0, int LivingAtSetOff = 0, int WaterAtEnd = -1,
        bool AtTheRiver = false, bool DrewBesideTheRiver = false, int WellWalk = -1, int BankWalk = -1, int WalkTicks = 0);

    private static bool Walking(VillagerState s) => s is VillagerState.WalkingToTheWell or VillagerState.WalkingToTheRiver;

    private static bool Drawing(VillagerState s) => s is VillagerState.DrawingWater or VillagerState.DrawingAtTheRiver;

    /// <summary>
    /// A fixture village with a well raised near the founding, run for some years, and every trip
    /// anybody made written down tick by tick.
    /// </summary>
    private static (SimWorld World, Well Well, List<Trip> Trips, int MostOutAtOnce) RunWithAWell(
        int years, SimConfig? config = null, int wellFrom = 3)
    {
        (SimWorld world, Well? well, List<Trip> trips, int most) = Run(years, config, wellFrom);
        return (world, well!, trips, most);
    }

    /// <summary>The same, with no well raised: every trip is to the river bank (D779).</summary>
    private static (SimWorld World, List<Trip> Trips, int MostOutAtOnce) RunWithoutAWell(int years, SimConfig? config = null)
    {
        (SimWorld world, _, List<Trip> trips, int most) = Run(years, config, wellFrom: null);
        return (world, trips, most);
    }

    private static (SimWorld World, Well? Well, List<Trip> Trips, int MostOutAtOnce) Run(int years, SimConfig? config, int? wellFrom)
    {
        SimLoop loop = Loop(config);
        SimWorld world = loop.World;
        loop.Step(world.Config.TicksPerYear);
        Well? well = wellFrom is int from0 ? ObstacleTests.RaiseAWellNearTheFounding(world, from0) : null;

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

                if (Walking(now) || Drawing(now))
                {
                    outByHousehold[v.HouseholdId] = outByHousehold.GetValueOrDefault(v.HouseholdId) + 1;
                }

                if (!Walking(was) && (Walking(now) || Drawing(now)) && !open.ContainsKey(v.Id))
                {
                    // Set off this tick — one step already taken, so "from home" is where they stood
                    // the tick before.
                    Household h = world.HouseholdOf(v);
                    int wellWalk = world.NearestWell(from, out int wTiles) is null ? -1 : wTiles;
                    int bankWalk = world.NearestBank(from, out int bTiles) is null ? -1 : bTiles;
                    open[v.Id] = new Trip(v.Id, v.HouseholdId, world.Today,
                        from == world.RestingPlaceOf(v), false, 0, false,
                        WaterAtSetOff: h.Water, LivingAtSetOff: world.LivingMembersOf(h),
                        AtTheRiver: now is VillagerState.WalkingToTheRiver or VillagerState.DrawingAtTheRiver,
                        WellWalk: wellWalk, BankWalk: bankWalk, WalkTicks: Walking(now) ? 1 : 0);
                }
                else if (open.TryGetValue(v.Id, out Trip? walking) && Walking(now))
                {
                    open[v.Id] = walking with { WalkTicks = walking.WalkTicks + 1 };
                }

                if (open.TryGetValue(v.Id, out Trip? trip))
                {
                    if (Drawing(now))
                    {
                        open[v.Id] = trip with
                        {
                            DrewAtTheWell = trip.DrewAtTheWell || (well is not null && v.Tile == well.Tile),
                            DrewBesideTheRiver = trip.DrewBesideTheRiver || (now == VillagerState.DrawingAtTheRiver && world.IsBank(v.Tile)),
                            DrawTicks = trip.DrawTicks + 1,
                        };
                    }
                    else if (!Walking(now))
                    {
                        open.Remove(v.Id);
                        trips.Add(trip with
                        {
                            WentHome = now is VillagerState.TravelingHome or VillagerState.Resting,
                            BrokeOffForShelter = now == VillagerState.SeekingShelter,
                            WaterAtEnd = world.HouseholdOf(v).Water,
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
        // The well's trips: a household nearer the river than this well draws at the bank instead (D779), and is not this guard's.
        List<Trip> drawn = trips.Where(t => t.DrawTicks > 0 && !t.BrokeOffForShelter && !t.AtTheRiver).ToList();
        Assert.NotEmpty(drawn);
        Assert.All(drawn, t => Assert.True(t.DrewAtTheWell, "drew somewhere that is not the well"));
        // At least the spell — a meal taken at the well holds the count for a tick, because eating
        // runs above the action's clock (`ActOne`), which is the order `HungerStillComesFirst` guards.
        Assert.All(drawn, t => Assert.InRange(t.DrawTicks, world.Config.WellDrawTicks, world.Config.WellDrawTicks + 1));
        Assert.All(drawn, t => Assert.True(t.WentHome, "left the well for somewhere that is not home"));
        Assert.Equal(drawn.Count, well.Draws); // a spell the cold broke off is not a draw

        // ⭐ And the draw fills the household's jar (D778, `water-need.md §5` step 2) — to the brim, on the tick it ends.
        Assert.All(drawn, t => Assert.Equal(world.Config.WaterJar, t.WaterAtEnd));
    }

    /// <summary>
    /// ⛔ THE JAR SAYS WHEN (D778, `water-need.md §5` step 2): a household sets off only when its water cannot cover
    /// another day of its living members' drink, and never two housemates at once — at the jar shipped and at a jar
    /// of a single day for two. <i>(Was <c>OneTripPerHouseholdPerInterval</c>, the 20-day clock's guard, D427.)</i>
    /// </summary>
    [Theory]
    [InlineData(null)]
    [InlineData(2)]
    // ⚠️ A jar of exactly one day for the fixture's households of three — the arm that bites on "less than a day" against
    // "a day or less" (D778's red check: the shipped jar and the jar of two both scored zero on that off-by-one, because
    // 40 never lands on a multiple of three and two is below three either way).
    [InlineData(3)]
    public void TheJarSaysWhenToGo(int? jar)
    {
        SimConfig config = jar is int size ? Config with { WaterJar = size } : Config;
        (_, _, List<Trip> trips, int mostOutAtOnce) = RunWithAWell(years: 2, config);

        Assert.True(trips.Count >= 10, $"only {trips.Count} trips in two years");
        Assert.Equal(1, mostOutAtOnce);
        Assert.All(trips, t => Assert.True(t.WaterAtSetOff < config.WaterPerPersonPerDay * t.LivingAtSetOff,
            $"household {t.HouseholdId} set off on day {t.Day} with {t.WaterAtSetOff} in the jar for {t.LivingAtSetOff}"));
    }

    /// <summary>
    /// ⭐ ACCEPTANCE 1 (`water-need.md §6`): a household's trips follow its size — at the same jar, a big household goes
    /// more often than a small one.
    /// </summary>
    [Fact]
    public void ABiggerHouseholdGoesMoreOften()
    {
        (_, _, List<Trip> trips, _) = RunWithAWell(years: 12);

        // From each trip to the same household's next, by how many lived there when the first set off.
        var gaps = trips.GroupBy(t => t.HouseholdId)
            .SelectMany(g =>
            {
                Trip[] inOrder = g.OrderBy(t => t.Day).ToArray();
                return inOrder.Zip(inOrder.Skip(1), (a, b) => (Living: a.LivingAtSetOff, Days: b.Day - a.Day));
            })
            .ToList();
        foreach (IGrouping<int, (int Living, int Days)> size in gaps.GroupBy(g => g.Living).OrderBy(g => g.Key))
        {
            _output.WriteLine($"{size.Key} living: {size.Count()} gaps, {size.Average(g => g.Days):F1} days on average");
        }

        var small = gaps.Where(g => g.Living <= 2).ToList();
        var big = gaps.Where(g => g.Living >= 4).ToList();
        Assert.NotEmpty(small);
        Assert.NotEmpty(big);
        Assert.True(big.Average(g => g.Days) < small.Average(g => g.Days),
            $"households of four or more went every {big.Average(g => g.Days):F1} days, of two or fewer every {small.Average(g => g.Days):F1}");
    }

    /// <summary>
    /// ⭐ JOE'S PICK (D779, `water-need.md` call 4): the shipped jar and drink send a household of two every sixty days and one
    /// of three every forty. <i>(Was <c>AHouseholdOfTwoGoesEveryTwentyDaysAsTheClockDid</c>, step 2's promise — D427's clock at
    /// a jar of 40 — which the river made too dear, D779.)</i>
    /// </summary>
    [Fact]
    public void AHouseholdOfTwoGoesEverySixtyDaysAtJoesJar()
    {
        SimConfig shipped = ShippedConfig.Load();
        int aDayForTwo = 2 * shipped.WaterPerPersonPerDay;
        Assert.Equal(0, shipped.WaterJar % aDayForTwo);
        Assert.Equal(60, shipped.WaterJar / aDayForTwo);
        Assert.Equal(40, shipped.WaterJar / (3 * shipped.WaterPerPersonPerDay));
    }

    /// <summary>
    /// ⭐ Every living member drinks <c>water_per_person_per_day</c> on each day's turn, and a jar goes no lower than
    /// empty (D778). <b>The needs pass alone</b>, twice on a day's first tick (D779): since the river, a run refills a low
    /// jar on the way, which is the trip's guard and not this one's.
    /// </summary>
    [Fact]
    public void EachDayDrinksAShareForEveryoneLiving()
    {
        SimWorld world = Loop().World;
        Assert.Equal(0UL, world.Tick % (ulong)world.Config.TicksPerDay);
        List<Household> pairs = world.Households.Where(h => world.LivingMembersOf(h) >= 2).Take(2).ToList();
        Assert.Equal(2, pairs.Count);
        Household full = pairs[0];
        Household nearlyDry = pairs[1];
        int per = world.Config.WaterPerPersonPerDay;
        int living = world.LivingMembersOf(full);
        full.Water = world.Config.WaterJar;
        nearlyDry.Water = 1;

        var needs = new Bclone.Sim.Systems.NeedsSystem();
        needs.Execute(world);
        needs.Execute(world);

        Assert.Equal(living, world.LivingMembersOf(full));
        Assert.Equal(world.Config.WaterJar - (2 * living * per), full.Water);
        Assert.Equal(0, nearlyDry.Water);
    }

    /// <summary>⛔ Sparse: an empty jar is not in the hash, so a village with no well hashes as it did before water (D778).</summary>
    [Fact]
    public void AJarWithWaterIsInTheHashAndAnEmptyOneIsNot()
    {
        SimWorld world = Loop().World;
        foreach (Household household in world.Households)
        {
            household.Water = 0; // the founders' jars start full (D780)
        }

        ulong none = StateHash.Compute(world);
        world.Households[0].Water = 3;
        Assert.NotEqual(none, StateHash.Compute(world));
        world.Households[0].Water = 0;
        Assert.Equal(none, StateHash.Compute(world));
    }

    /// <summary>
    /// ⭐ The jar is saved — every household's, empty or not (D780) — so a save from before D778, with no <c>water</c> key
    /// and an old <c>water_drawn_on_day</c>, loads with every jar <b>full</b>, as the founders' start (Joe's format-13 saves
    /// still load, and nobody in them runs dry on the first tick). <i>(Was <c>TheJarIsSavedAndAnOldSaveReadsItEmpty</c>: an
    /// empty jar was written null until D780, and a dry household would have loaded indistinguishable from an old save.)</i>
    /// </summary>
    [Fact]
    public void TheJarIsSavedAndAnOldSaveReadsItFull()
    {
        SimLoop loop = Loop();
        SimWorld world = loop.World;
        Assert.All(world.Households, h => Assert.Equal(world.Config.WaterJar, h.Water));
        ulong full = StateHash.Compute(world);
        Household dry = world.Households[1];
        dry.Water = 0;
        System.Text.Json.Nodes.JsonObject withADryOne = SaveGame.Capture(loop, "test", "2026-11-15 12:00", "test");
        SimWorld dryBack = SaveGame.Load(
            System.Text.Json.Nodes.JsonNode.Parse(SaveFile.TextOf(withADryOne))!.AsObject(), world.Config, null).World;
        Assert.Equal(0, dryBack.FindHousehold(dry.Id)!.Water);
        dry.Water = world.Config.WaterJar;
        Household household = world.Households[0];
        household.Water = 7;
        ulong wet = StateHash.Compute(world);

        System.Text.Json.Nodes.JsonObject document = SaveGame.Capture(loop, "test", "2026-11-15 12:00", "test");
        SimWorld back = SaveGame.Load(
            System.Text.Json.Nodes.JsonNode.Parse(SaveFile.TextOf(document))!.AsObject(), world.Config, null).World;
        Assert.Equal(7, back.FindHousehold(household.Id)!.Water);
        Assert.Equal(wet, StateHash.Compute(back));

        // The same village as a save from before water: every household's `water` key off, the retired key on.
        System.Text.Json.Nodes.JsonObject older = System.Text.Json.Nodes.JsonNode.Parse(SaveFile.TextOf(document))!.AsObject();
        Assert.Equal(world.Households.Count, AsAnOldSave(older));
        SimWorld oldBack = SaveGame.Load(older, world.Config, null).World;
        Assert.All(oldBack.Households, h => Assert.Equal(world.Config.WaterJar, h.Water));
        Assert.Equal(full, StateHash.Compute(oldBack));
    }

    /// <summary>Every household's <c>water</c> key removed and <c>water_drawn_on_day</c> put beside its <c>larder</c>, as a
    /// save before D778 had them — how many households carried the key. (A household is the object with a larder and members: a
    /// <c>water</c> key elsewhere in the save is something else's.)</summary>
    private static int AsAnOldSave(System.Text.Json.Nodes.JsonNode? node)
    {
        int removed = 0;
        if (node is System.Text.Json.Nodes.JsonObject obj)
        {
            if (obj.ContainsKey("larder") && obj.ContainsKey("members"))
            {
                removed += obj.Remove("water") ? 1 : 0;
                obj["water_drawn_on_day"] = 12;
            }

            foreach (KeyValuePair<string, System.Text.Json.Nodes.JsonNode?> child in obj.ToList())
            {
                removed += AsAnOldSave(child.Value);
            }
        }
        else if (node is System.Text.Json.Nodes.JsonArray array)
        {
            foreach (System.Text.Json.Nodes.JsonNode? item in array)
            {
                removed += AsAnOldSave(item);
            }
        }

        return removed;
    }

    // -----------------------------------------------------------------
    //  The river bank (D779, `water-need.md §5` step 3; Joe's call 2A)
    // -----------------------------------------------------------------

    /// <summary>
    /// ⭐ NO WELL, AND THE RIVER (D779): a village with no well draws at the river bank — home, the bank, home; a land tile
    /// beside the water, the jar filled to the brim, one housemate out at a time. <i>(Was <c>NoWellNoTrip</c>, D427: with no
    /// well nobody walked for water at all, a need that did nothing until a building — call 2B, which Joe did not pick.)</i>
    /// </summary>
    [Fact]
    public void WithNoWellTheyDrawAtTheRiverBank()
    {
        (SimWorld world, List<Trip> trips, int mostOutAtOnce) = RunWithoutAWell(years: 2);
        List<Trip> drawn = trips.Where(t => t.DrawTicks > 0 && !t.BrokeOffForShelter).ToList();
        _output.WriteLine($"{trips.Count} trips, {drawn.Count} drawn; walks out {string.Join(", ", trips.Select(t => t.WalkTicks).Distinct().OrderBy(n => n))} ticks");

        Assert.Empty(world.Wells);
        Assert.True(trips.Count >= 10, $"only {trips.Count} trips in two years");
        Assert.Equal(1, mostOutAtOnce);
        Assert.All(trips, t => Assert.True(t.AtTheRiver, $"villager {t.VillagerId} went to a well that does not exist"));
        Assert.All(trips, t => Assert.True(t.LeftFromHome, $"villager {t.VillagerId} set off from somewhere that is not home"));
        Assert.NotEmpty(drawn);
        Assert.All(drawn, t => Assert.True(t.DrewBesideTheRiver, "drew somewhere that is not the bank"));
        Assert.All(drawn, t => Assert.True(t.WentHome, "left the bank for somewhere that is not home"));
        Assert.All(drawn, t => Assert.Equal(world.Config.WaterJar, t.WaterAtEnd));
    }

    /// <summary>
    /// ⭐ ACCEPTANCE 2 (`water-need.md §6`): the nearer of the well and the bank — a well within reach whose walk from home is
    /// no longer than the bank's is taken, and otherwise the bank. In a valley whose river is far from the houses, a well by them
    /// is used; in the fixture's own, whose river runs three to six tiles from the homes, a well seven out is not.
    /// </summary>
    [Theory]
    [InlineData(FarFromTheRiver, 3, true)]
    [InlineData(0UL, AwayFromTheCore, false)]
    public void TheNearerOfTheWellAndTheBankIsTaken(ulong seed, int wellFrom, bool wellUsed)
    {
        SimConfig config = seed == 0 ? Config : Config with { Seed = seed };
        (_, _, List<Trip> trips, _) = RunWithAWell(years: 2, config, wellFrom);
        int toTheWell = trips.Count(t => !t.AtTheRiver);
        _output.WriteLine($"{trips.Count} trips: {toTheWell} to the well, {trips.Count - toTheWell} to the bank; "
            + string.Join("; ", trips.GroupBy(t => (t.WellWalk, t.BankWalk)).Select(g => $"well {g.Key.WellWalk} bank {g.Key.BankWalk}: {g.Count()}")));

        Assert.True(trips.Count >= 10, $"only {trips.Count} trips in two years");
        Assert.All(trips, t => Assert.Equal(!(t.WellWalk >= 0 && (t.BankWalk < 0 || t.WellWalk <= t.BankWalk)), t.AtTheRiver));
        Assert.Equal(wellUsed, toTheWell > 0);
    }

    /// <summary>
    /// A fixture seed whose founding is seventeen tiles' walk from the river bank (measured over seeds 1–40, D779: from 6 to 29,
    /// and the fixture's own seed three to six from its homes).
    /// </summary>
    private const ulong FarFromTheRiver = 16;

    /// <summary>
    /// ⭐ THE WELL'S PAYOFF IS WALKING SAVED (call 2A): the same village with a well by the houses walks less to fetch its water
    /// than with only the river.
    /// </summary>
    [Fact]
    public void AWellByTheHousesShortensTheWalk()
    {
        SimConfig far = Config with { Seed = FarFromTheRiver };
        (_, List<Trip> river, _) = RunWithoutAWell(years: 2, far);
        (_, _, List<Trip> well, _) = RunWithAWell(years: 2, far, wellFrom: 3);
        double riverWalk = river.Average(t => t.WalkTicks);
        double wellWalk = well.Average(t => t.WalkTicks);
        _output.WriteLine($"to the river: {river.Count} trips, {riverWalk:F1} ticks out on average; with a well 3 tiles out: {well.Count} trips, {wellWalk:F1}");
        Assert.True(wellWalk < riverWalk, $"a well by the houses walked {wellWalk:F1} ticks out against the river's {riverWalk:F1}");
    }

    // -----------------------------------------------------------------
    //  Running dry (D780, `water-need.md §5` step 4; Joe's call 3A, and D780: "Say it, don't rush")
    // -----------------------------------------------------------------

    private static bool OutForWater(Villager v) => Walking(v.State) || Drawing(v.State);

    private const string RanDry = "has run out of water";

    /// <summary>
    /// ⭐ SAID THE DAY IT RUNS DRY, ONCE (call 3A: <i>"and says so"</i>): the day's drink that empties a jar says so, naming the
    /// household; a day that finds it already empty says nothing. <b>The needs pass alone</b>, so no trip can fill it between.
    /// </summary>
    [Fact]
    public void AJarThatRunsDryIsSaidOnce()
    {
        var sink = new InMemoryLogSink();
        SimWorld world = SimFactory.CreatePhase0(Config, sink).World;
        Household household = world.Households.First(h => world.LivingMembersOf(h) > 0);
        household.Water = world.Config.WaterPerPersonPerDay * world.LivingMembersOf(household); // exactly a day
        var needs = new Bclone.Sim.Systems.NeedsSystem();

        int before = sink.Entries.Count;
        needs.Execute(world);
        string[] first = sink.Entries.Skip(before).Select(e => e.Message).Where(m => m.Contains(RanDry, StringComparison.Ordinal)).ToArray();
        before = sink.Entries.Count;
        needs.Execute(world);
        int second = sink.Entries.Skip(before).Count(e => e.Message.Contains(RanDry, StringComparison.Ordinal));

        _output.WriteLine(string.Join(" | ", first));
        Assert.Equal(0, household.Water);
        Assert.Single(first);
        Assert.StartsWith($"The {household.Name} household {RanDry}", first[0], StringComparison.Ordinal);
        Assert.Equal(0, second);
    }

    /// <summary>
    /// ⭐ NOBODY RUSHES (Joe, D780): the dry household's adults go on with what they were doing — nobody is out for water the tick
    /// it is said — and it is filled at a rest at home, as every trip is, within the season.
    /// </summary>
    [Fact]
    public void ADryJarWaitsForARestAtHome()
    {
        SimLoop loop = Loop();
        SimWorld world = loop.World;
        loop.Step(world.Config.TicksPerYear);
        Household dry = world.Households.First(h => h.HasHome && world.LivingMembersOf(h) > 0
            && !world.Villagers.Any(v => v.HouseholdId == h.Id && OutForWater(v)));
        var awayFromHome = world.Villagers.Where(v => v.Alive && v.HouseholdId == dry.Id && v.CanWork && v.Tile != world.HomePlaceOf(v))
            .ToDictionary(v => v.Id, v => v.State);
        dry.Water = 0;

        loop.StepOnce();
        Assert.All(world.Villagers.Where(v => awayFromHome.ContainsKey(v.Id)),
            v => Assert.False(OutForWater(v), $"{v.Name} dropped {awayFromHome[v.Id]} to walk for water"));

        int ticks = 1;
        for (; ticks < world.Config.TicksPerSeason && dry.Water == 0; ticks++)
        {
            loop.StepOnce();
        }

        _output.WriteLine($"the {dry.Name} jar filled after {ticks} ticks");
        Assert.True(dry.Water > 0, "a dry jar was not filled in a season");
    }

    /// <summary>
    /// ⭐ ACCEPTANCE 3 — SAID, AND NEVER TWICE A SPELL: over a year at a jar of three, a household of three runs dry often; every
    /// sentence names a household whose jar is empty on its tick, and no household is said dry twice without being filled
    /// between. Nobody dies of it: there is no thirst (call 3A).
    /// </summary>
    [Fact]
    public void EveryDrySpellIsSaidOnce()
    {
        var sink = new InMemoryLogSink();
        SimLoop loop = SimFactory.CreatePhase0(Config with { WaterJar = 3 }, sink);
        SimWorld world = loop.World;
        var saidAndNotFilled = new HashSet<int>();
        int said = 0;
        for (int t = 0; t < world.Config.TicksPerYear; t++)
        {
            int before = sink.Entries.Count;
            loop.StepOnce();
            foreach (Household h in world.Households.Where(h => h.Water > 0))
            {
                saidAndNotFilled.Remove(h.Id);
            }

            foreach (string line in sink.Entries.Skip(before).Select(e => e.Message).Where(m => m.Contains(RanDry, StringComparison.Ordinal)))
            {
                said++;
                Household? h = world.Households.FirstOrDefault(x => line.StartsWith($"The {x.Name} household ", StringComparison.Ordinal));
                Assert.NotNull(h);
                Assert.Equal(0, h!.Water);
                Assert.True(saidAndNotFilled.Add(h.Id), $"tick {world.Tick}: the {h.Name} household said dry twice in one spell");
            }
        }

        _output.WriteLine($"{said} dry spells said in a year at a jar of three");
        Assert.True(said > 0, "nobody ran dry at a jar of three");
    }

    /// <summary>⭐ The founders start with a full jar (D780), so a new valley does not open with every family dry.</summary>
    [Fact]
    public void TheFoundersStartWithAFullJar()
    {
        SimWorld world = Loop().World;
        Assert.All(world.Households, h => Assert.Equal(world.Config.WaterJar, h.Water));
    }

    /// <summary>
    /// ⭐ THE CARD'S SENTENCE (call 3A: <i>"No water — Dovlow has gone to the river"</i>): nothing while there is water; that
    /// somebody will go at their next rest at home while nobody has; who has gone and where once somebody has.
    /// </summary>
    [Fact]
    public void TheCardSaysWhoHasGoneForTheWater()
    {
        SimLoop loop = Loop();
        SimWorld world = loop.World;
        loop.Step(world.Config.TicksPerYear);
        Household dry = world.Households.First(h => h.HasHome && world.LivingMembersOf(h) > 0
            && !world.Villagers.Any(v => v.HouseholdId == h.Id && OutForWater(v)));
        Household wet = world.Households.First(h => h.Id != dry.Id);
        wet.Water = world.Config.WaterJar;
        dry.Water = 0;
        Assert.Null(world.WaterNoteFor(wet));
        Assert.Equal("No water — somebody will go at their next rest at home", world.WaterNoteFor(dry));

        Villager? went = null;
        for (int t = 0; t < world.Config.TicksPerSeason && went is null; t++)
        {
            loop.StepOnce();
            went = world.Villagers.FirstOrDefault(v => v.HouseholdId == dry.Id && OutForWater(v));
        }

        Assert.NotNull(went);
        string? note = world.WaterNoteFor(dry);
        _output.WriteLine(note);
        Assert.NotNull(note);
        Assert.StartsWith($"No water — {went!.Name} has gone to the ", note, StringComparison.Ordinal);
    }

    /// <summary>
    /// ⭐ THE BANK IS THE ONE COST FIELD'S (§2.6): for every home, the bank is a land tile beside the river that nothing stands
    /// on, and no such tile in the whole valley — looked for tile by tile, not from the world's own list — is a cheaper walk home.
    /// </summary>
    [Fact]
    public void TheBankIsBesideTheRiverAndTheCheapestWalkHome()
    {
        SimLoop loop = Loop();
        SimWorld world = loop.World;
        loop.Step(world.Config.TicksPerYear);
        GeneratedMap map = world.Map;
        var everyBank = new List<GridPos>();
        for (int y = map.MinY; y < map.MinY + map.Height; y++)
        {
            for (int x = map.MinX; x < map.MinX + map.Width; x++)
            {
                var at = new GridPos(x, y);
                bool beside = new[] { new GridPos(x + 1, y), new GridPos(x - 1, y), new GridPos(x, y + 1), new GridPos(x, y - 1) }
                    .Any(n => map.Contains(n) && map.TerrainAt(n) == Terrain.Water);
                if (map.TerrainAt(at) != Terrain.Water && beside && !world.StandsOn(at))
                {
                    everyBank.Add(at);
                }
            }
        }

        Assert.NotEmpty(everyBank);
        List<Household> housed = world.Households.Where(h => h.HomeTile is not null).ToList();
        Assert.NotEmpty(housed);
        foreach (Household household in housed)
        {
            GridPos home = household.HomeTile!.Value;
            GridPos? bank = world.NearestBank(home, out int tiles);
            Assert.NotNull(bank);
            Assert.Contains(bank!.Value, everyBank);
            Assert.True(world.IsBank(bank.Value));
            int cost = world.TravelCost.Cost(bank.Value, home);
            int cheapest = everyBank.Select(b => world.TravelCost.Cost(b, home)).Where(c => c != TravelCostField.Unreachable).Min();
            _output.WriteLine($"{household.Surname}: bank {bank} at {tiles} tiles ({cost}); cheapest anywhere {cheapest}");
            Assert.Equal(cheapest, cost);
            Assert.Equal(cost / TravelCostField.BaseTileCost, tiles);
        }
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
    /// ⚠️ <b>The control keeps the well and stops the trips</b> (nobody drinks, so each household goes once and its
    /// jar stays full — an interval nobody reaches until D778). Its first draft compared against a village with no well at all and
    /// scored zero with the trips switched off: a standing well bends everybody else's walks round
    /// it, and at three tiles from the founding that alone wears the ring beside it.
    /// </remarks>
    [Fact]
    public void APathWearsToTheWell()
    {
        (SimWorld world, Well well, List<Trip> trips, _) = RunWithAWell(years: 2, wellFrom: AwayFromTheCore);
        (SimWorld still, Well same, List<Trip> once, _) = RunWithAWell(
            years: 2, Config with { WaterPerPersonPerDay = 0 }, AwayFromTheCore);
        Assert.Equal(well.Tile, same.Tile);

        int worn = WornBeside(world, well.Tile);
        int wornWithout = WornBeside(still, well.Tile);
        int wear = WearBeside(world, well.Tile);
        int wearWithout = WearBeside(still, well.Tile);
        _output.WriteLine($"{trips.Count} trips against {once.Count}; worn tiles beside the well {worn} against {wornWithout}; "
            + $"wear beside it {wear} against {wearWithout}");

        // ⚠️ WEAR, NOT YET A WORN TILE, IN TWO YEARS — the price of doors, measured and accepted (D625, Joe:
        // "keep D388 as it is ... record the house-siting and herd guards as measured costs"). Walks go
        // through doors now (`doors.md §4.4`) and the site chooser prices every plot by them; switching that
        // one rule off (`DoorsOnEverySide`) puts this guard back at its old bar, and neither siting rule of
        // D622 moves it. Measured: 11 trips, wear beside the well 20 against 0, no tile there worn to the path
        // grade (one or more before doors). The doubling below still holds.
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

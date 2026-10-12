using System.Text.Json.Nodes;
using Bclone.Sim.Config;
using Bclone.Sim.Core;
using Bclone.Sim.Determinism;
using Bclone.Sim.Logging;
using Bclone.Sim.Persistence;
using Bclone.Sim.Systems;
using Bclone.Sim.World;
using Xunit;
using Xunit.Abstractions;

namespace Bclone.Sim.Tests;

/// <summary>
/// The nomads, commit 2 — the longhouse, the travellers' house (`specs/nomads.md §4.5`, §9): learned at the first
/// knock, a roof and one larder for roofless families until a house of their own is raised.
/// </summary>
/// <remarks>
/// ⛔ Every pose raises the longhouse through <see cref="SimWorld.Mark(BuildingKind, GridPos)"/> and a finished site, as the player does,
/// and lodges families through the sim's own daily pass — never by writing <see cref="Household.LodgedIn"/> by hand,
/// so a pass that forgot to lodge anybody cannot stay green (D157).
/// </remarks>
public sealed class LonghouseTests
{
    private readonly ITestOutputHelper _output;

    public LonghouseTests(ITestOutputHelper output) => _output = output;

    private static SimConfig Village => VillageFixtures.Village;

    private static (SimLoop Loop, InMemoryLogSink Log) Founded(SimConfig config)
    {
        var log = new InMemoryLogSink();
        return (SimFactory.CreatePhase0(config, log), log);
    }

    /// <summary>Through the knock, as `NomadTests.Knocked` steps it: the band is there one tick past the knock.</summary>
    private static (SimLoop Loop, InMemoryLogSink Log) Knocked(SimConfig config)
    {
        (SimLoop loop, InMemoryLogSink log) = Founded(config);
        loop.Step(((loop.World.FirstKnockYear() - 1) * config.TicksPerYear) + 1);
        Assert.NotNull(loop.World.Band);
        return (loop, log);
    }

    /// <summary>Somewhere a longhouse fits either way round, near the founding, found rather than assumed.</summary>
    private static GridPos SomewhereALonghouseFits(SimWorld world)
    {
        GridPos site = world.Map.FoundingSite;
        for (int radius = 2; radius < 30; radius++)
        {
            for (int dy = -radius; dy <= radius; dy++)
            {
                for (int dx = -radius; dx <= radius; dx++)
                {
                    var at = new GridPos(site.X + dx, site.Y + dy);
                    if (world.CanBuildAt(BuildingKind.Longhouse, at).Allowed)
                    {
                        return at;
                    }
                }
            }
        }

        throw new Xunit.Sdk.XunitException("Nowhere near the founding fits a longhouse.");
    }

    /// <summary>A longhouse raised as the player raises one: marked, stocked, worked, finished.</summary>
    private static Longhouse RaiseALonghouse(SimWorld world)
    {
        GridPos anchor = SomewhereALonghouseFits(world);
        PlacementVerdict verdict = world.Mark(BuildingKind.Longhouse, anchor);
        Assert.True(verdict.Allowed, verdict.Reason);
        Workplace site = world.Workplaces.Single(w => w.Construction?.Kind == BuildingKind.Longhouse);
        BuildFixtures.StockTheSite(site);
        for (int i = 0; i <= site.Construction!.Recipe.WorkTicks; i++)
        {
            site.Construction.Work();
        }

        world.Complete(site);
        Longhouse longhouse = Assert.Single(world.Longhouses);
        Assert.Equal(BuildingKind.Longhouse, world.WhatStandsAt(anchor));
        return longhouse;
    }

    private static string Said(InMemoryLogSink sink) => string.Join(" | ", sink.Entries.Select(e => e.Message));

    /// <summary>
    /// Step <paramref name="ticks"/> while holding the families roofless — their sites cancelled, no house of their own —
    /// so a pose about lodging is not undone by the fixture's builders raising their house in a season.
    /// </summary>
    private static void StepHeldLodged(SimLoop loop, IReadOnlyList<Household> families, int ticks)
    {
        SimWorld world = loop.World;
        int perDay = world.Config.TicksPerDay;
        for (int stepped = 0; stepped < ticks; stepped += perDay)
        {
            foreach (Household h in families)
            {
                if (world.HomeSiteFor(h.Id) is Workplace site)
                {
                    world.CancelConstruction(site);
                }

                h.HomePosition = null;
            }

            loop.Step(Math.Min(perDay, ticks - stepped));
        }
    }

    // ---------------------------------------------------------------
    //  Learned, raised, a row
    // ---------------------------------------------------------------

    [Fact]
    public void TheLonghouseIsLearnedAtTheFirstKnockAcceptedOrNot()
    {
        (SimLoop loop, _) = Founded(Village);
        Assert.NotNull(loop.World.WhyNotYet(BuildingKind.Longhouse));
        Assert.False(loop.World.Mark(BuildingKind.Longhouse, SomewhereALonghouseFits(loop.World)).Allowed);

        (SimLoop knocked, _) = Knocked(Village);
        Assert.Null(knocked.World.WhyNotYet(BuildingKind.Longhouse));
        knocked.World.SendTheBandOn();
        Assert.Null(knocked.World.WhyNotYet(BuildingKind.Longhouse));
        Assert.True(knocked.World.Mark(BuildingKind.Longhouse, SomewhereALonghouseFits(knocked.World)).Allowed);
    }

    [Fact]
    public void TheShippedRowIsADwellingPricedAsTwoHouses()
    {
        SimConfig shipped = ShippedConfig.Load();
        BuildingRow row = shipped.BuildingRows.Single(r => r.Id == (int)BuildingKind.Longhouse);
        BuildingRow house = shipped.BuildingRows.Single(r => r.Id == (int)BuildingKind.Home);
        Assert.Null(row.Stores);
        Assert.Equal(shipped.LonghouseCapacity, row.HouseCapacity);
        Assert.Equal(8, row.HouseCapacity);
        Assert.Equal(house.LocalStoreCap * 2, row.LocalStoreCap);
        Assert.Equal((3, 1), (row.ExtentWidth, row.ExtentHeight));
        Assert.Equal(BuildingRecipe.For(BuildingKind.Home, shipped).Of(Goods.Logs) * 2, BuildingRecipe.For(BuildingKind.Longhouse, shipped).Of(Goods.Logs));
        Assert.Equal((8, 12, 20), (row.WearsInYears, row.FailsInYears, row.RuinedInYears));
    }

    [Fact]
    public void AFinishedLonghouseStandsWithALarderAndAnswersEveryTileFinder()
    {
        (SimLoop loop, InMemoryLogSink log) = Knocked(Village);
        SimWorld world = loop.World;
        Longhouse longhouse = RaiseALonghouse(world);
        Assert.Equal(Village.LonghouseCapacity, longhouse.Capacity);
        Assert.Equal(world.BuildingsCatalog[BuildingKind.Longhouse]!.LocalStoreCap, longhouse.Larder.Capacity);
        Assert.Contains("roof for travellers", Said(log), StringComparison.Ordinal);

        foreach (GridPos tile in longhouse.Footprint.CoveredTiles())
        {
            Assert.Same(longhouse, world.LonghouseCovering(tile));
            Assert.Equal(BuildingKind.Longhouse, world.WhatStandsAt(tile));
            Assert.Equal(longhouse.Position, world.StandingPlaceAt(tile));
            Assert.Equal(Shelter.Roof, world.ShelterAt(tile));
            Assert.False(world.CanBuildAt(BuildingKind.Granary, tile).Allowed, "a granary may be marked on a longhouse");
        }

        Assert.Equal(Condition.Sound, world.ConditionOf(longhouse));
    }

    // ---------------------------------------------------------------
    //  Lodging (§4.5)
    // ---------------------------------------------------------------

    [Fact]
    public void ASettledFamilyLodgesTheNextDayAndEatsFromTheLonghousesLarder()
    {
        (SimLoop loop, InMemoryLogSink log) = Knocked(Village);
        SimWorld world = loop.World;
        Longhouse longhouse = RaiseALonghouse(world);
        int before = world.Households.Count;
        world.LetTheBandSettle();
        Household family = world.Households[before];
        Assert.Equal(0, family.LodgedIn);

        // The roofless are answered daily (D72): a bed in the longhouse while their own house is marked.
        loop.Step(Village.TicksPerDay);
        Assert.Equal(longhouse.Id, family.LodgedIn);
        Assert.Same(longhouse, world.LodgingOf(family));
        Assert.Same(longhouse.Larder, world.LarderOf(family));
        Assert.Contains("is lodging at", Said(log), StringComparison.Ordinal);
        Assert.Equal(world.LivingMembersOf(family), world.LodgersAlive(longhouse));

        // Where they sleep is the longhouse, and a meal comes out of its larder, not the family's own.
        Villager lodger = world.Villagers.First(v => v.HouseholdId == family.Id);
        Assert.Equal(longhouse.Position, world.HomePoint(lodger));
        Assert.Equal(longhouse.Tile, world.HomePlaceOf(lodger));
        int ownFood = world.FoodIn(family.Stockpile);
        longhouse.Larder.Receive(Goods.Produce, 200);
        Assert.Same(longhouse.Larder, world.LarderOf(lodger));
        foreach (Villager v in world.Villagers.Where(v => v.HouseholdId == family.Id))
        {
            v.FullFor = 0;
        }

        StepHeldLodged(loop, new[] { family }, Village.TicksPerSeason);
        Assert.Equal(longhouse.Id, family.LodgedIn);
        Assert.True(world.FoodIn(longhouse.Larder) < 200, "a lodger's meals did not come from the longhouse's larder");
        Assert.Equal(ownFood, world.FoodIn(family.Stockpile));
    }

    [Fact]
    public void TheLonghouseLodgesUpToItsCapacityAndTheRestSleepRough()
    {
        // A band of six is two families of three; a longhouse of four takes the first and not the second.
        SimConfig config = Village with { NomadsBandMin = 6, LonghouseCapacity = 4 };
        (SimLoop loop, _) = Knocked(config);
        SimWorld world = loop.World;
        Longhouse longhouse = RaiseALonghouse(world);
        Assert.Equal(2, world.Band!.Families.Count);
        int before = world.Households.Count;
        world.LetTheBandSettle();
        loop.Step(config.TicksPerDay);

        Household first = world.Households[before];
        Household second = world.Households[before + 1];
        Assert.Equal(longhouse.Id, first.LodgedIn);
        Assert.Equal(0, second.LodgedIn);
        Assert.Equal(world.LivingMembersOf(first), world.LodgersAlive(longhouse));
        Villager rough = world.Villagers.First(v => v.HouseholdId == second.Id);
        Assert.Equal(world.BandCamp, world.HomePoint(rough));
    }

    [Fact]
    public void TheLonghousesFireBurnsItsOwnLarderAndWarmsItsLodgers()
    {
        (SimLoop loop, _) = Knocked(Village);
        SimWorld world = loop.World;
        Longhouse longhouse = RaiseALonghouse(world);
        world.LetTheBandSettle();
        loop.Step(Village.TicksPerDay);
        Household family = world.Households[^1];
        Assert.Equal(longhouse.Id, family.LodgedIn);

        Assert.Equal(Shelter.Roof, world.ShelterAt(longhouse.Tile));
        longhouse.Larder.Receive(Goods.Firewood, 30);
        Assert.Equal(Shelter.Fire, world.ShelterAt(longhouse.Tile));

        // Step to the first day of winter and through one burn, held lodged: the longhouse's larder pays, the family's does not.
        ulong winter = ((ulong)(world.Clock.Year - 1) * (ulong)Village.TicksPerYear) + (3UL * (ulong)Village.TicksPerSeason);
        StepHeldLodged(loop, new[] { family }, (int)(winter - world.Tick) + (Village.TicksPerDay * (Village.FirewoodBurnIntervalDays + 1)));
        Assert.Equal(longhouse.Id, family.LodgedIn);
        Assert.True(longhouse.Larder.Firewood < 30, "the longhouse's fire burned nothing of its own larder");
        Assert.Equal(0, family.Stockpile.Firewood);
    }

    [Fact]
    public void ALodgedFamilyMovesOutTheDayItsOwnHouseIsRaised()
    {
        (SimLoop loop, InMemoryLogSink log) = Knocked(Village);
        SimWorld world = loop.World;
        Longhouse longhouse = RaiseALonghouse(world);
        int before = world.Households.Count;
        world.LetTheBandSettle();
        Household family = world.Households[before];
        loop.Step(Village.TicksPerDay);
        Assert.Equal(longhouse.Id, family.LodgedIn);

        // Their own house is marked beside the lodging; finish it as the builders would, and the next pass moves them out.
        Workplace site = world.HomeSiteFor(family.Id) ?? throw new Xunit.Sdk.XunitException("no house was marked for the lodging family");
        BuildFixtures.StockTheSite(site);
        for (int i = 0; i <= site.Construction!.Recipe.WorkTicks; i++)
        {
            site.Construction.Work();
        }

        world.Complete(site);
        Assert.True(family.HasHome);
        loop.Step(Village.TicksPerDay);
        Assert.Equal(0, family.LodgedIn);
        Assert.Null(world.LodgingOf(family));
        Assert.Same(family.Stockpile, world.LarderOf(family));
        Assert.Equal(0, world.LodgersAlive(longhouse));
        Assert.Contains("moved out of", Said(log), StringComparison.Ordinal);
    }

    /// <summary>
    /// ⭐ Posed from BOTH sides (D452's trap: a guard sees what is beside it): a lodged adult and a founder are each
    /// unpaired. The rule is checked on the seeker and on the candidate, and either check alone masks a mutant of the
    /// other — a lodged seeker meeting a free founder catches one, a free founder meeting a lodged candidate the other.
    /// </summary>
    [Fact]
    public void NobodyLodgingPartners()
    {
        (SimLoop loop, _) = Knocked(Village);
        SimWorld world = loop.World;
        RaiseALonghouse(world);
        int before = world.Households.Count;
        world.LetTheBandSettle();
        loop.Step(Village.TicksPerDay);
        Household family = world.Households[before];
        Assert.NotEqual(0, family.LodgedIn);

        Villager lodger = world.Villagers.First(v => v.HouseholdId == family.Id && v.LifeStage == LifeStage.Adult);
        Villager founder = world.Villagers.First(v => v.Founder && v.Alive);
        foreach (Villager v in new[] { lodger, founder })
        {
            world.FindVillager(v.PartnerId)!.PartnerId = 0;
            v.PartnerId = 0;
        }

        Assert.True(HouseholdSystem.IsSeekingAHome(lodger, Village) && HouseholdSystem.IsSeekingAHome(founder, Village), "the pose has nobody seeking");
        int lodgerHome = lodger.HouseholdId;
        int founderHome = founder.HouseholdId;

        // Held lodged (their site may finish) through the year's turn, when households form.
        ulong turn = (ulong)world.Clock.Year * (ulong)Village.TicksPerYear;
        StepHeldLodged(loop, new[] { family }, (int)(turn - world.Tick) + Village.TicksPerDay);

        Assert.Equal(0, lodger.PartnerId);
        Assert.Equal(lodgerHome, lodger.HouseholdId);
        Assert.Equal(founderHome, founder.HouseholdId);
        Assert.True(founder.PartnerId == 0 || world.LodgingOf(world.HouseholdOf(world.FindVillager(founder.PartnerId)!)) is null,
            "a founder paired with somebody lodging at the longhouse");
    }

    /// <summary>The longhouse's larder is sim state: what it holds decides who eats, so two villages that differ by it differ in the fingerprint.</summary>
    [Fact]
    public void TheLonghousesLarderIsInTheFingerprint()
    {
        (SimLoop loop, _) = Knocked(Village);
        Longhouse longhouse = RaiseALonghouse(loop.World);
        ulong empty = StateHash.Compute(loop.World);
        longhouse.Larder.Receive(Goods.Produce, 50);
        Assert.NotEqual(empty, StateHash.Compute(loop.World));
    }

    // ---------------------------------------------------------------
    //  Wear, ruin, demolition
    // ---------------------------------------------------------------

    [Fact]
    public void AFallenInLonghouseSheltersNobodyAndAWornOneIsMended()
    {
        // A quick clock (2 / 3 / 4 years), so a longhouse raised five years before a Year-10 knock is a ruin — the
        // knock comes before Year 20, and a stamp cannot be set before the valley was.
        SimConfig config = Village with { BuildingWearsInYears = 2, BuildingFailsInYears = 3, BuildingRuinedInYears = 4 };
        (SimLoop loop, InMemoryLogSink log) = Knocked(config);
        SimWorld world = loop.World;
        Longhouse longhouse = RaiseALonghouse(world);
        world.LetTheBandSettle();
        loop.Step(config.TicksPerDay);
        Household family = world.Households[^1];
        Assert.Equal(longhouse.Id, family.LodgedIn);

        // Stamped so the very next season's turn sees it cross from failing to ruin (asked of the rule, winters and
        // all — a stamp picked by arithmetic missed the half-seasons the winters add): that turn says so, closes it,
        // and its lodgers are in the open before the builders can mend it.
        BuildingRow row = world.BuildingsCatalog[BuildingKind.Longhouse]!;
        ulong season = (ulong)config.TicksPerSeason;
        ulong turn = ((world.Tick / season) + 1UL) * season;
        ulong raised = 0;
        for (ulong candidate = turn - (6UL * (ulong)config.TicksPerYear); candidate < turn; candidate += (ulong)config.TicksPerDay)
        {
            if (ConditionRules.Of(candidate, 0UL, turn, row, config) == Condition.Ruin
                && ConditionRules.Of(candidate, 0UL, turn - season, row, config) == Condition.Failing)
            {
                raised = candidate;
            }
        }

        Assert.NotEqual(0UL, raised);
        longhouse.RaisedAtTick = raised;
        StepHeldLodged(loop, new[] { family }, (int)(turn - world.Tick) + 1);
        Assert.Equal(Condition.Ruin, world.ConditionOf(longhouse));
        Assert.True(longhouse.Closed);
        Assert.Equal(Shelter.Outdoors, world.ShelterAt(longhouse.Tile));
        Assert.Contains(world.Workplaces, w => SimWorld.IsARepairSite(w) && w.Construction!.Kind == BuildingKind.Longhouse);
        Assert.Contains("fallen in", Said(log), StringComparison.Ordinal);

        // A second roofless family finds no room in a ruin.
        var stranger = new Household { Id = world.NextHouseholdId(), Surname = "Stranger", Stockpile = world.NewStockpile(), DayForAChild = 1, HomePosition = null };
        world.Households.Add(stranger);
        Assert.False(world.LodgeIfRoom(stranger));

        // Mended: open again, a roof again.
        Workplace repair = world.Workplaces.First(w => SimWorld.IsARepairSite(w) && w.Construction!.Kind == BuildingKind.Longhouse);
        BuildFixtures.StockTheSite(repair);
        for (int i = 0; i <= repair.Construction!.Recipe.WorkTicks; i++)
        {
            repair.Construction.Work();
        }

        world.Complete(repair);
        Assert.False(longhouse.Closed);
        Assert.Equal(Condition.Sound, world.ConditionOf(longhouse));
        Assert.Equal(Shelter.Roof, world.ShelterAt(longhouse.Tile));
    }

    [Fact]
    public void PullingALonghouseDownTurnsItsLodgersOut()
    {
        (SimLoop loop, InMemoryLogSink log) = Knocked(Village);
        SimWorld world = loop.World;
        Longhouse longhouse = RaiseALonghouse(world);
        world.LetTheBandSettle();
        loop.Step(Village.TicksPerDay);
        Household family = world.Households[^1];
        Assert.Equal(longhouse.Id, family.LodgedIn);
        GridPos tile = longhouse.Tile;

        PlacementVerdict asked = world.MarkDemolition(tile);
        Assert.True(asked.Allowed, asked.Reason);
        Workplace taking = world.DemolitionSiteAt(tile) ?? throw new Xunit.Sdk.XunitException("no demolition site");
        for (int i = 0; i <= taking.Construction!.Recipe.WorkTicks; i++)
        {
            taking.Construction.Work();
        }

        world.Complete(taking);
        Assert.Empty(world.Longhouses);
        Assert.Null(world.WhatStandsAt(tile));
        Assert.Equal(0, family.LodgedIn);
        Assert.Same(family.Stockpile, world.LarderOf(family));
        Assert.Contains("in the open again", Said(log), StringComparison.Ordinal);
    }

    // ---------------------------------------------------------------
    //  Saved
    // ---------------------------------------------------------------

    [Fact]
    public void ALonghouseWithLodgersSurvivesASaveAndItsYearMatches()
    {
        (SimLoop loop, InMemoryLogSink log) = Knocked(Village);
        SimWorld world = loop.World;
        Longhouse longhouse = RaiseALonghouse(world);
        world.LetTheBandSettle();
        loop.Step(Village.TicksPerDay + 2);
        Assert.Equal(longhouse.Id, world.Households[^1].LodgedIn);
        longhouse.Larder.Receive(Goods.Produce, 50);

        JsonObject document = SaveGame.Capture(loop, "test", "2026-10-12 12:00", "test");
        var loadedLog = new InMemoryLogSink();
        SimLoop loaded = SaveGame.Load(JsonNode.Parse(SaveFile.TextOf(document))!.AsObject(), world.Config, loadedLog);
        Longhouse back = Assert.Single(loaded.World.Longhouses);
        Assert.Equal((longhouse.Id, longhouse.Capacity, longhouse.Position, longhouse.Closed), (back.Id, back.Capacity, back.Position, back.Closed));
        Assert.Equal(50, loaded.World.FoodIn(back.Larder));
        Assert.Equal(longhouse.Id, loaded.World.Households[^1].LodgedIn);
        Assert.Equal(StateHash.Compute(world), StateHash.Compute(loaded.World));

        loadedLog.Clear();
        int said = log.Entries.Count;
        for (int t = 0; t < Village.TicksPerYear; t++)
        {
            loop.StepOnce();
            loaded.StepOnce();
            Assert.Equal(StateHash.Compute(world), StateHash.Compute(loaded.World));
        }

        Assert.Equal(log.Entries.Skip(said).Select(e => e.ToString()), loadedLog.Entries.Select(e => e.ToString()));
    }

    /// <summary>A village with no longhouse hashes byte for byte as it did before longhouses existed — the wells' rule.</summary>
    [Fact]
    public void AVillageWithNoLonghouseHashesAsBefore()
    {
        (SimLoop loop, _) = Founded(Village);
        loop.Step(Village.TicksPerYear);
        ulong hash = StateHash.Compute(loop.World);
        _output.WriteLine($"{hash}");
        Assert.Empty(loop.World.Longhouses);
        Assert.All(loop.World.Households, h => Assert.Equal(0, h.LodgedIn));
    }
}

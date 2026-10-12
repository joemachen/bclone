using System.Text.Json.Nodes;
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
/// The nomads, commit 3 — a dead valley re-founded (`specs/nomads.md §4.6`, §9): with nobody alive, four nomads
/// walk in on the spring turn a hashed few years after the last death, a second generation of founders.
/// </summary>
/// <remarks>
/// The cold village (`ColdStartTests.ColdVillage`) dies in its first winter by design (D143), so a re-founding is posed
/// a few years in; the warm fixture village, killed by hand at Year 3, poses what the four inherit.
/// </remarks>
public sealed class RefoundTests
{
    private readonly ITestOutputHelper _output;

    public RefoundTests(ITestOutputHelper output) => _output = output;

    private static SimConfig Cold => ColdStartTests.ColdVillage;

    private static SimConfig Warm => VillageFixtures.Village;

    private static (SimLoop Loop, InMemoryLogSink Log) Founded(SimConfig config)
    {
        var log = new InMemoryLogSink();
        return (SimFactory.CreatePhase0(config, log), log);
    }

    /// <summary>Everybody dies on this tick, by hand — the warm village's founders, their house keys left on the table.</summary>
    private static void KillEverybody(SimWorld world)
    {
        foreach (Villager villager in world.Villagers)
        {
            if (!villager.Alive)
            {
                continue;
            }

            villager.Alive = false;
            villager.State = VillagerState.Dead;
            villager.DiedAtTick = world.Tick;
            villager.CauseOfDeath = CauseOfDeath.Cold;
        }
    }

    /// <summary>The spring tick the re-founding is due, from the sim's own rule.</summary>
    private static ulong RefoundTick(SimWorld world)
    {
        ulong emptied = world.EmptiedAtTick ?? throw new Xunit.Sdk.XunitException("the valley is not empty");
        int year = world.RefoundYearAfter(SimClock.FromTick(emptied, world.Config).Year);
        return (ulong)(year - 1) * (ulong)world.Config.TicksPerYear;
    }

    /// <summary>A warm village killed at Year 3, stepped through its re-founding: the four are there one tick past the turn.</summary>
    private static (SimLoop Loop, InMemoryLogSink Log, ulong Turn) Refounded(SimConfig config)
    {
        (SimLoop loop, InMemoryLogSink log) = Founded(config);
        loop.Step(config.TicksPerYear * 2);
        KillEverybody(loop.World);
        ulong turn = RefoundTick(loop.World);
        loop.Step((int)(turn - loop.World.Tick) + 1);
        Assert.Equal(4, loop.World.Population);
        return (loop, log, turn);
    }

    private static string Said(InMemoryLogSink sink) => string.Join(" | ", sink.Entries.Select(e => e.Message));

    // ---------------------------------------------------------------
    //  The wait, and the four
    // ---------------------------------------------------------------

    [Fact]
    public void TheColdVillageDiesAndIsRefoundedOnTheSpringTurnOfItsYear()
    {
        (SimLoop loop, InMemoryLogSink log) = Founded(Cold);
        loop.Step(Cold.TicksPerYear * 2);
        Assert.Equal(0, loop.World.Population);
        Assert.NotNull(loop.World.EmptiedAtTick);
        int emptiedIn = SimClock.FromTick(loop.World.EmptiedAtTick!.Value, Cold).Year;
        int due = loop.World.RefoundYearAfter(emptiedIn);
        Assert.InRange(due - emptiedIn, Cold.RefoundYearsFloor, Cold.RefoundYearsFloor + Cold.RefoundYearsSpread);
        _output.WriteLine($"emptied in Year {emptiedIn}, re-founded in Year {due}");

        ulong turn = RefoundTick(loop.World);
        loop.Step((int)(turn - loop.World.Tick));
        Assert.Equal(0, loop.World.Population);
        Assert.Equal(0, loop.World.Refoundings);

        loop.StepOnce();
        Assert.Equal(4, loop.World.Population);
        Assert.Equal(1, loop.World.Refoundings);
        Assert.Equal(due, loop.World.Clock.Year);
        Assert.Equal(Season.Spring, loop.World.Clock.Season);
        Moment moment = Assert.Single(loop.World.Moments, m => m.Title.Contains("found the empty valley", StringComparison.Ordinal));
        Assert.True(moment.WaitsToBeDismissed);
        Assert.Contains("The valley is theirs now", Said(log), StringComparison.Ordinal);
        Assert.Null(loop.World.EmptiedAtTick);
    }

    [Fact]
    public void AFloorOfZeroNeverRefounds()
    {
        (SimLoop loop, _) = Founded(Cold with { RefoundYearsFloor = 0 });
        loop.Step(Cold.TicksPerYear * 12);
        Assert.Equal(0, loop.World.Population);
        Assert.Equal(0, loop.World.Refoundings);
        Assert.Equal(0, loop.World.RefoundYearAfter(1));
    }

    [Fact]
    public void NobodyRefoundsAValleyThatIsNotEmpty()
    {
        (SimLoop loop, _) = Founded(Warm);
        loop.Step(Warm.TicksPerYear * 12);
        Assert.True(loop.World.Population > 0);
        Assert.Equal(0, loop.World.Refoundings);
        Assert.Null(loop.World.EmptiedAtTick);
    }

    [Fact]
    public void TheFourAreASecondGenerationOfFoundersWithNamesNobodyEverHeld()
    {
        (SimLoop loop, _, ulong turn) = Refounded(Warm);
        SimWorld world = loop.World;
        var four = world.Villagers.Where(v => v.Alive).ToList();
        Assert.Equal(4, four.Count);
        var dead = new HashSet<string>(world.Households.Where(h => world.LivingMembersOf(h) == 0).Select(h => h.Surname));

        Assert.All(four, v =>
        {
            Assert.True(v.Founder);
            Assert.Equal(turn, v.ArrivedAtTick);
            Assert.Equal(Warm.FounderAge, v.AgeYears);
            Assert.Equal(LifeStage.Adult, v.LifeStage);
            Assert.NotEqual(0, v.PartnerId);
            Assert.DoesNotContain(v.Surname, dead);
        });

        var households = four.Select(v => v.HouseholdId).Distinct().ToList();
        Assert.Equal(2, households.Count);
        Assert.NotEqual(world.FindHousehold(households[0])!.Surname, world.FindHousehold(households[1])!.Surname);
        Assert.Equal(four.Select(v => v.Name).Count(), four.Select(v => v.Name).Distinct().Count());

        // The founding's mix: a master and a journeyman among the four, the rest novices.
        int masters = four.Count(v => v.Skills.Any(s => s.Mastered));
        int journeymen = four.Count(v => v.Skills.Any(s => !s.Mastered && s.Work > 0));
        Assert.Equal(Warm.FoundingMasters, masters);
        Assert.Equal(Warm.FoundingJourneymen, journeymen);
    }

    [Fact]
    public void TheCartIsRestockedWhereItStandsAndRaisedAgainWhereItWasNot()
    {
        // The cold village keeps its cart: restocked.
        (SimLoop loop, _) = Founded(Cold);
        loop.Step(Cold.TicksPerYear * 2);
        StoreBuilding cart = loop.World.TheCart ?? throw new Xunit.Sdk.XunitException("no cart");
        int foodBefore = cart.Store[Goods.Produce];
        loop.Step((int)(RefoundTick(loop.World) - loop.World.Tick) + 1);
        Assert.Same(cart, loop.World.TheCart);
        Assert.True(cart.Store[Goods.Produce] > foodBefore, "the cart was not restocked");

        // The warm village never had one: raised again at the founding site, with the cart's food.
        (SimLoop warm, _, _) = Refounded(Warm);
        StoreBuilding raised = warm.World.TheCart ?? throw new Xunit.Sdk.XunitException("no cart was raised for the re-founding");
        Assert.Equal(Math.Min(Warm.CartFood, Warm.CartCapacity), raised.Store[Goods.Produce]);
        Assert.Equal(warm.World.StoreBuildings.Select(s => s.Id).Distinct().Count(), warm.World.StoreBuildings.Count);
    }

    [Fact]
    public void TheStandingHousesAreTakenTheNextDayKeepingTheirOwnNames()
    {
        (SimLoop loop, _, _) = Refounded(Warm);
        SimWorld world = loop.World;
        var four = world.Villagers.Where(v => v.Alive).ToList();
        var names = four.Select(v => v.Surname).Distinct().ToList();
        loop.Step(Warm.TicksPerDay);
        foreach (int id in four.Select(v => v.HouseholdId).Distinct())
        {
            Household home = world.FindHousehold(id)!;
            Assert.True(home.HasHome, $"the {home.Surname} household was not moved into a standing house");
            Assert.Contains(home.Surname, names);
        }

        Assert.Contains(world.Moments, m => m.Body.Contains("sound", StringComparison.Ordinal) && m.Title.Contains("found the empty valley", StringComparison.Ordinal));
    }

    // ---------------------------------------------------------------
    //  The empty valley keeps time
    // ---------------------------------------------------------------

    [Fact]
    public void RuinsCloseOnTimeInAnEmptyValleyAndNoRepairIsRaisedUntilSomebodyReturns()
    {
        // A quick clock so a house raised at the founding is a ruin while the valley lies empty.
        SimConfig config = Warm with { BuildingWearsInYears = 1, BuildingFailsInYears = 2, BuildingRuinedInYears = 3, RefoundYearsFloor = 4, RefoundYearsSpread = 0 };
        (SimLoop loop, InMemoryLogSink log) = Founded(config);
        loop.Step(config.TicksPerYear * 2);
        KillEverybody(loop.World);
        SimWorld world = loop.World;
        Household house = world.Households.First(h => h.HasHome);
        int walks = world.ConditionWalks;

        // A year on: the walk ran each season, nothing was repaired, and once a ruin it was closed on its turn.
        loop.Step(config.TicksPerYear * 2);
        Assert.True(world.ConditionWalks > walks, "the season's walk did not run in the empty valley");
        Assert.Equal(Condition.Ruin, world.ConditionOf(house));
        Assert.True(house.HomeClosed, "a ruin in an empty valley was not closed on its turn");
        Assert.DoesNotContain(world.Workplaces, w => SimWorld.IsARepairSite(w));

        // The log still tells the roofs' wearing — the valley crumbling is the one story an empty valley has.
        Assert.Contains("has fallen in", Said(log), StringComparison.Ordinal);

        // The four return: the first turn after raises repairs for what is worn.
        loop.Step((int)(RefoundTick(world) - world.Tick) + 1);
        Assert.Equal(4, world.Population);
        loop.Step(config.TicksPerSeason);
        Assert.Contains(world.Workplaces, w => SimWorld.IsARepairSite(w));
    }

    // ---------------------------------------------------------------
    //  The hall
    // ---------------------------------------------------------------

    [Fact]
    public void TheHallIsOwedOnTheLastOfTheSecondGenerationWhenItWasNeverGiven()
    {
        (SimLoop loop, InMemoryLogSink log, _) = Refounded(Warm);
        SimWorld world = loop.World;
        Assert.False(world.SaidTheFoundersAreGone);
        loop.Step(Warm.TicksPerDay);

        // Somebody born here, so the last founder's death is the village outgrowing them, not ending.
        Household home = world.Households.First(h => world.LivingMembersOf(h) > 0);
        var bornHere = new Villager
        {
            Id = world.NextVillagerId(),
            Name = "Native",
            LifespanYears = 60,
            Carried = world.NewStockpile(),
            BirthTick = (long)world.Tick,
            AgeYears = 0,
            LifeStage = LifeStage.Child,
            HouseholdId = home.Id,
            Position = home.HomePosition ?? world.BandCamp,
        };
        home.AddMember(bornHere.Id);
        world.Villagers.Add(bornHere);

        foreach (Villager founder in world.Villagers.Where(v => v.Founder && v.Alive))
        {
            founder.Alive = false;
            founder.State = VillagerState.Dead;
            founder.DiedAtTick = world.Tick;
        }

        loop.StepOnce();
        Assert.True(world.SaidTheFoundersAreGone);
        Assert.True(world.ATownHallIsOwed);
        string said = Said(log);
        Assert.Contains("found this valley empty", said, StringComparison.Ordinal);
        Assert.Contains("went before them", said, StringComparison.Ordinal);
        foreach (Villager first in world.Villagers.Where(v => v.Founder && v.ArrivedAtTick == 0UL))
        {
            Assert.Contains(first.Name, said, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void NoSecondHallWhereOneWasAlreadyGiven()
    {
        (SimLoop loop, _) = Founded(Warm);
        loop.Step(Warm.TicksPerYear * 2);
        loop.World.SaidTheFoundersAreGone = true;
        loop.World.ATownHallIsOwed = false;
        KillEverybody(loop.World);
        loop.Step((int)(RefoundTick(loop.World) - loop.World.Tick) + 1);
        Assert.Equal(4, loop.World.Population);
        int moments = loop.World.Moments.Count(m => m.Title.Contains("founders", StringComparison.Ordinal));
        KillEverybody(loop.World);
        loop.Step(2);
        Assert.False(loop.World.ATownHallIsOwed);
        Assert.Equal(moments, loop.World.Moments.Count(m => m.Title.Contains("founders", StringComparison.Ordinal)));
    }

    [Fact]
    public void TheKnockClockResumesFromTheRefounding()
    {
        (SimLoop loop, _, _) = Refounded(Warm);
        Assert.True(loop.World.NextKnockYear > loop.World.Clock.Year);
        Assert.InRange(loop.World.NextKnockYear - loop.World.Clock.Year, Warm.NomadsYearsBetweenFloor, Warm.NomadsYearsBetweenFloor + Warm.NomadsYearsBetweenSpread);
    }

    // ---------------------------------------------------------------
    //  Saved
    // ---------------------------------------------------------------

    [Fact]
    public void ARefoundedValleySurvivesASaveAndItsYearMatches()
    {
        (SimLoop loop, InMemoryLogSink log, _) = Refounded(Warm);
        loop.Step(3);
        JsonObject document = SaveGame.Capture(loop, "test", "2026-10-12 12:00", "test");
        var loadedLog = new InMemoryLogSink();
        SimLoop loaded = SaveGame.Load(JsonNode.Parse(SaveFile.TextOf(document))!.AsObject(), loop.World.Config, loadedLog);
        Assert.Equal(1, loaded.World.Refoundings);
        Assert.Equal(StateHash.Compute(loop.World), StateHash.Compute(loaded.World));

        loadedLog.Clear();
        int said = log.Entries.Count;
        for (int t = 0; t < Warm.TicksPerYear; t++)
        {
            loop.StepOnce();
            loaded.StepOnce();
            Assert.Equal(StateHash.Compute(loop.World), StateHash.Compute(loaded.World));
        }

        Assert.Equal(log.Entries.Skip(said).Select(e => e.ToString()), loadedLog.Entries.Select(e => e.ToString()));
    }

    /// <summary>
    /// ⭐ Posed so the skip bites (D423): with four surnames in the pool the dead founders hold two, and the re-founders
    /// must take the other two whatever their hash says — a ghost's name reused scored zero against the pool of sixty.
    /// </summary>
    [Theory]
    [InlineData(1UL)]
    [InlineData(2UL)]
    [InlineData(3UL)]
    [InlineData(4UL)]
    [InlineData(5UL)]
    public void TheRefoundersTakeNamesNoHouseholdEverHeld(ulong seed)
    {
        SimConfig config = Warm with { Seed = seed, HouseholdNames = new[] { "Ash", "Birch", "Cedar", "Dale" } };
        (SimLoop loop, _) = Founded(config);
        loop.Step(config.TicksPerYear * 2);
        var dead = new HashSet<string>(loop.World.Households.Select(h => h.Surname));
        Assert.Equal(2, dead.Count);
        KillEverybody(loop.World);
        loop.Step((int)(RefoundTick(loop.World) - loop.World.Tick) + 1);
        Assert.Equal(4, loop.World.Population);
        var fresh = loop.World.Households.Where(h => loop.World.LivingMembersOf(h) > 0).Select(h => h.Surname).ToList();
        Assert.Equal(2, fresh.Count);
        Assert.All(fresh, name => Assert.DoesNotContain(name, dead));
        Assert.NotEqual(fresh[0], fresh[1]);
    }

    /// <summary>The count of re-foundings is sim state — the next wait is hashed on it — so it is in the fingerprint.</summary>
    [Fact]
    public void TheRefoundingCountIsInTheFingerprint()
    {
        (SimLoop loop, _, _) = Refounded(Warm);
        ulong once = StateHash.Compute(loop.World);
        loop.World.Refoundings = 0;
        Assert.NotEqual(once, StateHash.Compute(loop.World));
        loop.World.Refoundings = 1;
        Assert.Equal(once, StateHash.Compute(loop.World));
    }

    /// <summary>A valley that never empties hashes byte for byte as it did before re-founding existed — the count is sparse.</summary>
    [Fact]
    public void AValleyThatNeverEmptiesCarriesNoRefounding()
    {
        (SimLoop loop, _) = Founded(Warm);
        loop.Step(Warm.TicksPerYear * 3);
        Assert.Equal(0, loop.World.Refoundings);
        Assert.Null(loop.World.EmptiedAtTick);
    }
}

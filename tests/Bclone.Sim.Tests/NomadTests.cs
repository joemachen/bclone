using Bclone.Sim.Config;
using Bclone.Sim.Core;
using Bclone.Sim.Determinism;
using System.Text.Json.Nodes;
using Bclone.Sim.Logging;
using Bclone.Sim.Persistence;
using Bclone.Sim.World;
using Xunit;
using Xunit.Abstractions;

namespace Bclone.Sim.Tests;

/// <summary>
/// The nomads — the band that knocks and settles the same tick (`specs/nomads.md §4.1–4.4`, §9; D565, D572).
/// </summary>
/// <remarks>
/// <para>
/// ⭐ <b>The anti-vacuity twin comes first:</b> a village whose first-year floor is 0 never meets a band and hashes,
/// to the tick, as a village did before bands existed; and a village on the shipped clock hashes the same until its
/// knock. Everything else here is posed on the fixture village, whose first knock is a hash of its seed —
/// <see cref="KnockTick"/> asks the sim rather than assuming a year.
/// </para>
/// <para>
/// ⛔ The arrivals are posed by <b>stepping to the knock</b>, never by building a band by hand: a band built by hand
/// cannot tell the test that the sim fails to raise one (D157's green-and-blind). Since D572 nothing waits — the
/// band is gone into the roster by the end of the knock tick — so the fact every guard reads is the arrivals:
/// villagers whose <see cref="Villager.ArrivedAtTick"/> is the knock.
/// </para>
/// </remarks>
public sealed class NomadTests
{
    private readonly ITestOutputHelper _output;

    public NomadTests(ITestOutputHelper output) => _output = output;

    private static SimConfig Village => VillageFixtures.Village;

    private static SimConfig Silent => Village with { NomadsFirstYearFloor = 0 };

    /// <summary>The fixture founded with the Nomads switch off — the new-game row's <i>Never come</i> (D570).</summary>
    private static SimConfig Off => Village with { NomadsEnabled = false };

    private static (SimLoop Loop, InMemoryLogSink Log) Founded(SimConfig config)
    {
        var log = new InMemoryLogSink();
        return (SimFactory.CreatePhase0(config, log), log);
    }

    /// <summary>The spring tick the fixture's first band knocks on.</summary>
    private static ulong KnockTick(SimWorld world) => (ulong)(world.FirstKnockYear() - 1) * (ulong)world.Config.TicksPerYear;

    /// <summary>Everybody who came to the valley on <paramref name="tick"/>.</summary>
    private static List<Villager> ArrivedAt(SimWorld world, ulong tick) =>
        world.Villagers.Where(v => v.ArrivedAtTick == tick && tick != 0UL).ToList();

    /// <summary>Everybody who came with the first band.</summary>
    private static List<Villager> Arrived(SimWorld world) => ArrivedAt(world, KnockTick(world));

    /// <summary>The households the first band became, in the order they were made.</summary>
    private static List<Household> ArrivedHouseholds(SimWorld world) =>
        Arrived(world).Select(v => v.HouseholdId).Distinct().Select(id => world.Households.Single(h => h.Id == id)).ToList();

    /// <summary>
    /// Step through the knock: the systems compute a tick before the loop advances it (`SimLoop.StepOnce`), so the
    /// family settled on the knock tick is there once the loop has stepped one tick past it.
    /// </summary>
    private static (SimLoop Loop, InMemoryLogSink Log) Knocked(SimConfig config)
    {
        (SimLoop loop, InMemoryLogSink log) = Founded(config);
        loop.Step((int)KnockTick(loop.World) + 1);
        Assert.NotEmpty(Arrived(loop.World));
        return (loop, log);
    }

    private static string Said(InMemoryLogSink sink)
    {
        var said = new System.Text.StringBuilder();
        foreach (LogEntry entry in sink.Entries)
        {
            said.Append(entry.Message).Append(" | ");
        }

        return said.ToString();
    }

    // ---------------------------------------------------------------
    //  The clock (§4.1)
    // ---------------------------------------------------------------

    [Fact]
    public void TheFirstKnockIsInsideTheFloorAndSpread()
    {
        (SimLoop loop, _) = Founded(Village);
        int year = loop.World.FirstKnockYear();
        Assert.InRange(year, Village.NomadsFirstYearFloor, Village.NomadsFirstYearFloor + Village.NomadsFirstYearSpread);
        _output.WriteLine($"the fixture's first band knocks in Year {year}");

        // Another seed, another year — somewhere in the band; the hash is the seed's.
        var years = new HashSet<int>();
        for (ulong seed = 1; seed <= 40; seed++)
        {
            years.Add(SimFactory.CreatePhase0(Village with { Seed = seed }, new InMemoryLogSink()).World.FirstKnockYear());
        }

        Assert.True(years.Count > 1, "forty seeds all knock in the same year — the hash is not reading the seed");
    }

    [Fact]
    public void AFloorOfZeroMeansNobodyEverKnocks()
    {
        (SimLoop loop, _) = Founded(Silent);
        loop.Step(Silent.TicksPerYear * 20);
        Assert.DoesNotContain(loop.World.Villagers, v => v.ArrivedAtTick != 0UL);
        Assert.Equal(0, loop.World.BandsEverKnocked);
        Assert.Equal(0, loop.World.FirstKnockYear());
    }

    /// <summary>
    /// ⭐ THE ANTI-VACUITY TWIN: until the knock, a village on the shipped clock is byte for byte the village with no
    /// nomads at all — nothing is drawn, nothing is mixed. And on the knock tick they part: one has more people.
    /// </summary>
    [Fact]
    public void AVillageHashesAsBeforeUntilItsFirstKnock()
    {
        (SimLoop clocked, _) = Founded(Village);
        (SimLoop silent, _) = Founded(Silent);
        ulong knock = KnockTick(clocked.World);

        clocked.Step((int)knock);
        silent.Step((int)knock);
        Assert.Equal(StateHash.Compute(silent.World), StateHash.Compute(clocked.World));
        Assert.Equal(silent.World.Rng.State, clocked.World.Rng.State);

        clocked.StepOnce();
        silent.StepOnce();
        Assert.NotEmpty(Arrived(clocked.World));
        Assert.True(clocked.World.Population > silent.World.Population, "the band settled and nobody was added");
        Assert.NotEqual(StateHash.Compute(silent.World), StateHash.Compute(clocked.World));

        // ⛔ And still no draw: the knock is a hash, so the stream is where the silent village's is.
        Assert.Equal(silent.World.Rng.State, clocked.World.Rng.State);
        Assert.Equal(silent.World.Rng.Inc, clocked.World.Rng.Inc);
    }

    /// <summary>The switch is the permission (Joe, D572): on, the band is villagers by the end of the knock tick.</summary>
    [Fact]
    public void ABandArrivesAndSettlesOnTheSpringTurnOfItsYear()
    {
        (SimLoop loop, InMemoryLogSink log) = Founded(Village);
        ulong knock = KnockTick(loop.World);
        loop.Step((int)knock);
        Assert.Empty(ArrivedAt(loop.World, knock));
        int year = loop.World.Clock.Year;

        loop.StepOnce();
        List<Villager> arrived = ArrivedAt(loop.World, knock);
        Assert.NotEmpty(arrived);
        Assert.Equal(Season.Spring, loop.World.Clock.Season);
        Assert.Equal(1, loop.World.Clock.DayOfSeason);
        Assert.Equal(loop.World.FirstKnockYear(), loop.World.Clock.Year);
        Assert.Equal(1, loop.World.BandsEverKnocked);
        Assert.True(loop.World.NextKnockYear > year, "the next knock's year was not set at the knock");
        Assert.All(arrived, v => Assert.False(loop.World.HouseholdOf(v).HasHome));

        Moment moment = Assert.Single(loop.World.Moments, m => m.Title.Contains("Nomads", StringComparison.Ordinal));
        Assert.True(moment.WaitsToBeDismissed, "the arrival does not wait — at 4× it slides past unread (D232)");
        Assert.Contains("have settled", moment.Title, StringComparison.Ordinal);
        Assert.Contains("settled", moment.Body, StringComparison.Ordinal);
        Assert.DoesNotContain("Click", moment.Body, StringComparison.Ordinal);
        Assert.DoesNotContain("decide", moment.Body, StringComparison.Ordinal);
        Assert.Contains("have settled", Said(log), StringComparison.Ordinal);
    }

    /// <summary>
    /// ⛔ Nothing waits (D572): the band is settled once, that tick, and no moment asks for anything. A band settled
    /// twice would show as more arrivals than the share rule gives.
    /// </summary>
    [Fact]
    public void AKnockNeverLeavesABandWaitingAndSettlesOnce()
    {
        (SimLoop loop, _) = Founded(Village);
        ulong knock = KnockTick(loop.World);
        loop.Step((int)knock);
        int met = loop.World.Population;
        int householdsBefore = loop.World.Households.Count;
        loop.StepOnce();

        List<Villager> arrived = ArrivedAt(loop.World, knock);
        Assert.Equal(NomadBand.PeopleFor(Village, met, townHallStands: false), arrived.Count);
        Assert.Equal(NomadBand.FamiliesFor(arrived.Count, townHallStands: false), ArrivedHouseholds(loop.World).Count);
        Assert.Equal(householdsBefore + ArrivedHouseholds(loop.World).Count, loop.World.Households.Count);
        Assert.DoesNotContain(loop.World.Moments, m => m.Body.Contains("Click", StringComparison.Ordinal));
        Assert.Equal(1, loop.World.BandsEverKnocked);

        // The tick after: nobody else came, and nothing was settled twice.
        loop.StepOnce();
        Assert.Equal(arrived.Count, loop.World.Villagers.Count(v => v.ArrivedAtTick != 0UL));
        Assert.Equal(1, loop.World.BandsEverKnocked);
    }

    [Fact]
    public void NobodyKnocksOnAnEmptyValley()
    {
        (SimLoop loop, _) = Founded(Village);
        ulong knock = KnockTick(loop.World);
        loop.Step((int)knock - 10);
        foreach (Villager villager in loop.World.Villagers)
        {
            villager.Alive = false;
            villager.State = VillagerState.Dead;
            villager.DiedAtTick = loop.World.Tick;
        }

        loop.Step(20);
        Assert.Equal(0, loop.World.Population);
        Assert.Empty(ArrivedAt(loop.World, knock));
        Assert.Equal(0, loop.World.BandsEverKnocked);
    }

    // ---------------------------------------------------------------
    //  The band (§4.2)
    // ---------------------------------------------------------------

    [Theory]
    [InlineData(4, 2, 1, 2, 0)]
    [InlineData(8, 2, 1, 2, 0)]
    [InlineData(13, 3, 1, 2, 1)]
    [InlineData(20, 5, 2, 4, 1)]
    [InlineData(40, 8, 2, 4, 4)]
    [InlineData(200, 8, 2, 4, 4)]
    public void ABandIsAShareOfTheVillageClamped(int population, int people, int families, int adults, int children)
    {
        Assert.Equal(people, NomadBand.PeopleFor(Village, population, townHallStands: false));
        Assert.Equal(families, NomadBand.FamiliesFor(people, townHallStands: false));
        Assert.Equal(adults, families * 2);
        Assert.Equal(children, people - adults);
    }

    [Fact]
    public void TheHallWidensTheBandAndShortensTheWait()
    {
        Assert.Equal(NomadBand.PeopleFor(Village, 8, false) + 2, NomadBand.PeopleFor(Village, 8, true));
        Assert.Equal(NomadBand.FamiliesFor(2, false) + 1, NomadBand.FamiliesFor(4, true));

        (SimLoop loop, _) = Founded(Village);
        int without = loop.World.NextKnockYearAfter(10, 1);
        loop.World.TownHall = new TownHall { Position = Point.CentreOf(loop.World.Map.FoundingSite), Name = "the hall", RaisedAtTick = loop.World.Tick };
        int with = loop.World.NextKnockYearAfter(10, 1);
        Assert.True(with < without, $"the hall does not shorten the wait ({with} against {without})");
        Assert.InRange(with - 10, Village.NomadsHallYearsBetweenFloor, Village.NomadsHallYearsBetweenFloor + Village.NomadsYearsBetweenSpread);
        Assert.InRange(without - 10, Village.NomadsYearsBetweenFloor, Village.NomadsYearsBetweenFloor + Village.NomadsYearsBetweenSpread);
    }

    [Fact]
    public void TheBandIsTwoAdultsAFamilyWithChildrenAndOneJourneyman()
    {
        (SimLoop loop, _) = Knocked(Village);
        SimWorld world = loop.World;
        List<Villager> arrived = Arrived(world);
        Assert.Equal(NomadBand.PeopleFor(Village, world.Population - arrived.Count, false), arrived.Count);

        foreach (Household family in ArrivedHouseholds(world))
        {
            var members = arrived.Where(v => v.HouseholdId == family.Id).ToList();
            Assert.Equal(2, members.Count(v => v.LifeStage == LifeStage.Adult));
            foreach (Villager person in members)
            {
                bool adult = person.LifeStage == LifeStage.Adult;
                Assert.InRange(person.AgeYears, adult ? 18 : 2, adult ? 40 : 11);
                Assert.InRange(person.LifespanYears, Village.LifespanYearsBase - Village.LifespanYearsVariance, Village.LifespanYearsBase + Village.LifespanYearsVariance);
                Assert.InRange(person.Rhythm, 0, Village.TicksPerDay - 1);
            }
        }

        var journeymen = arrived.Where(v => v.Skills.Any(s => s.Work > 0)).ToList();
        Villager journeyman = Assert.Single(journeymen);
        SkillProgress progress = Assert.Single(journeyman.Skills, s => s.Work > 0);
        Assert.Contains(Village.FoundingSkills(), s => s.Id == progress.SkillId);
    }

    [Fact]
    public void EverySurnameIsNewAndEveryFirstNameItsOwn()
    {
        // Posed with a village large enough for two families, so the two-family rule is exercised.
        (SimLoop loop, _) = Knocked(Village with { NomadsBandMin = 6 });
        SimWorld world = loop.World;
        List<Household> families = ArrivedHouseholds(world);
        Assert.Equal(2, families.Count);

        var arrivedIds = new HashSet<int>(families.Select(f => f.Id));
        var living = new HashSet<string>(world.Households.Where(h => !arrivedIds.Contains(h.Id) && world.LivingMembersOf(h) > 0).Select(h => h.Surname));
        Assert.All(families, f => Assert.DoesNotContain(f.Surname, living));
        Assert.NotEqual(families[0].Surname, families[1].Surname);

        List<Villager> arrived = Arrived(world);
        var names = arrived.Select(p => p.Name).ToList();
        Assert.Equal(names.Count, names.Distinct(StringComparer.Ordinal).Count());
        var alive = new HashSet<string>(world.Villagers.Where(v => v.Alive && v.ArrivedAtTick == 0UL).Select(v => v.Name));
        Assert.All(names, n => Assert.DoesNotContain(n, alive));
    }

    // ---------------------------------------------------------------
    //  The arrival (§4.3, §4.4)
    // ---------------------------------------------------------------

    [Fact]
    public void TheNextKnockFollowsTheLastOnItsOwnYear()
    {
        (SimLoop loop, _) = Knocked(Village);
        int next = loop.World.NextKnockYear;
        ulong secondKnock = (ulong)(next - 1) * (ulong)Village.TicksPerYear;
        loop.Step((int)(secondKnock - loop.World.Tick));
        Assert.Empty(ArrivedAt(loop.World, secondKnock));
        loop.StepOnce();
        Assert.True(loop.World.Population == 0 || ArrivedAt(loop.World, secondKnock).Count > 0, "the second band did not come on its year");
        if (loop.World.Population > 0)
        {
            Assert.Equal(2, loop.World.BandsEverKnocked);
            Assert.Equal(next, loop.World.Clock.Year);
        }
    }

    [Fact]
    public void TheKnockAddsRooflessFamiliesHoldingTheirFood()
    {
        (SimLoop loop, InMemoryLogSink log) = Founded(Village);
        SimWorld world = loop.World;
        ulong knock = KnockTick(world);
        loop.Step((int)knock);
        int householdsBefore = world.Households.Count;
        loop.StepOnce();

        List<Villager> arrived = ArrivedAt(world, knock);
        List<Household> families = ArrivedHouseholds(world);
        Assert.NotEmpty(arrived);
        Assert.Equal(householdsBefore + families.Count, world.Households.Count);
        Assert.Contains("have settled", Said(log), StringComparison.Ordinal);

        foreach (Household home in families)
        {
            var members = arrived.Where(v => v.HouseholdId == home.Id).ToList();
            Assert.Null(home.HomePosition);
            Assert.Equal(members.Count * Village.NomadsFoodEach, home.Stockpile[Goods.Produce]);
            Assert.Equal(members.Count, world.LivingMembersOf(home));

            var adults = members.Where(v => v.LifeStage == LifeStage.Adult).ToList();
            Assert.Equal(2, adults.Count);
            Assert.Equal(adults[0].Id, adults[1].PartnerId);
            Assert.Equal(adults[1].Id, adults[0].PartnerId);
            foreach (Villager villager in members)
            {
                Assert.Equal(knock, villager.ArrivedAtTick);
                Assert.False(villager.Founder);
                Assert.Equal(home.Surname, villager.Surname);
                Assert.Equal(world.BandCamp, villager.Position);
                Assert.True(villager.LifeStage == LifeStage.Adult || villager.PartnerId == 0);
            }
        }

        Assert.True(world.NextKnockYear > world.Clock.Year);
    }

    [Fact]
    public void TheWoodenBridgeIsLearnedByArrivals()
    {
        // The most the village has ever held the tick before the knock, read off a first run; the posed village learns
        // the bridge at one more than that, so only an arrival can teach it.
        (SimLoop first, _) = Founded(Village);
        first.Step((int)KnockTick(first.World));
        SimConfig config = Village with { BridgeUnlockPopulation = first.World.MostEverAlive + 1 };

        (SimLoop posed, _) = Founded(config);
        posed.Step((int)KnockTick(posed.World));
        Assert.False(posed.World.IsUnlocked(BuildingKind.Bridge), "the bridge is known before anybody arrived — the pose is wrong");
        posed.StepOnce();
        Assert.NotEmpty(Arrived(posed.World));
        Assert.True(posed.World.IsUnlocked(BuildingKind.Bridge));
        Assert.Contains(posed.World.Moments, m => m.Title.Contains("bridge", StringComparison.Ordinal));
    }

    [Fact]
    public void ASettledFamilyIsHousedByTheVillagesOwnQueue()
    {
        (SimLoop loop, _) = Knocked(Village);
        SimWorld world = loop.World;
        Household home = ArrivedHouseholds(world)[0];

        // The roofless are answered daily (D72): within a season a house is marked for them, or they have one.
        loop.Step(Village.TicksPerSeason);
        Assert.True(home.HasHome || world.HomeSiteFor(home.Id) is not null,
            $"the {home.Surname} household was neither housed nor given a site in a season ({(world.NeedsMoreResidentialLand ? "nowhere to build" : "room in the paint")})");
    }

    [Fact]
    public void NoChildIsBornToARooflessFamily()
    {
        (SimLoop loop, _) = Knocked(Village);
        SimWorld world = loop.World;
        Household home = ArrivedHouseholds(world)[0];
        int members = world.LivingMembersOf(home);

        // Hold their house off and run a year: no roof, no child (D71).
        for (int day = 0; day < Village.DaysPerSeason * 4; day++)
        {
            if (world.HomeSiteFor(home.Id) is Workplace site)
            {
                world.CancelConstruction(site);
            }

            home.HomePosition = null;
            loop.Step(Village.TicksPerDay);
        }

        Assert.Equal(members, world.Villagers.Count(v => v.HouseholdId == home.Id));
    }

    [Fact]
    public void ARooflessFamilyFreezesAtThePileInWinter()
    {
        // A settled family held roofless — its site cancelled daily, as a village with no paint and no logs would
        // leave it — sleeps in the open at the camp, and the winter kills them where they lie (Joe's rule, §4.4).
        (SimLoop loop, _) = Knocked(Village);
        SimWorld world = loop.World;
        List<Villager> settled = Arrived(world);
        List<Household> families = ArrivedHouseholds(world);
        Assert.All(settled, v => Assert.Equal(Shelter.Outdoors, world.ShelterAt(v.Position.ToTile())));

        // Held roofless for a year, the winter chills them in the open; whether it kills them is the exposure
        // model's (D45, D53 — a villager may warm at a neighbour's fire), guarded where it lives. What this
        // guards is that a settled family has no shelter of its own and feels the cold for it.
        int coldest = 0;
        for (int day = 0; day < Village.DaysPerSeason * 4; day++)
        {
            foreach (Household household in families)
            {
                if (world.HomeSiteFor(household.Id) is Workplace site)
                {
                    world.CancelConstruction(site);
                }

                household.HomePosition = null;
            }

            loop.Step(Village.TicksPerDay);
            foreach (Villager villager in settled)
            {
                coldest = Math.Max(coldest, villager.Cold);
            }
        }

        Assert.True(coldest > 0, "a family with no roof went a whole winter without feeling the cold");
        Assert.All(families, h => Assert.False(h.HasHome));
    }

    /// <summary>
    /// ⭐ Posed so the skip bites (D423): with three surnames in the pool the founders hold two, and the band must take
    /// the third whatever its hash says — a band that reused a living name scored zero against the shipped pool of sixty.
    /// </summary>
    [Theory]
    [InlineData(1UL)]
    [InlineData(2UL)]
    [InlineData(3UL)]
    [InlineData(4UL)]
    [InlineData(5UL)]
    public void ABandTakesTheOneSurnameNobodyLivingHolds(ulong seed)
    {
        SimConfig config = Village with { Seed = seed, HouseholdNames = new[] { "Ash", "Birch", "Cedar" } };
        (SimLoop loop, _) = Knocked(config);
        SimWorld world = loop.World;
        Household family = Assert.Single(ArrivedHouseholds(world));
        var living = new HashSet<string>(world.Households.Where(h => h.Id != family.Id && world.LivingMembersOf(h) > 0).Select(h => h.Surname));
        Assert.Equal(2, living.Count);
        string free = new[] { "Ash", "Birch", "Cedar" }.Single(n => !living.Contains(n));
        Assert.Equal(free, family.Surname);
    }

    /// <summary>
    /// ⭐ An arrival counts from the tick it came, not from tick 0 (D565): a village that has lost more people than a
    /// band brings keeps its old peak. Counted from tick 0, the whole history would shift up by the band — the mutant
    /// that scored zero against the bridge guard, where the peak was the present.
    /// </summary>
    [Fact]
    public void AnArrivalCountsFromTheTickItCame()
    {
        (SimLoop loop, _) = Founded(Village);
        SimWorld world = loop.World;
        loop.Step((int)KnockTick(world) - 10);
        int peak = world.MostEverAlive;

        // Three deaths before the knock, so the village is below its peak by more than a band of two brings.
        int killed = 0;
        foreach (Villager villager in world.Villagers)
        {
            if (killed == 3) { break; }
            if (!villager.Alive || villager.LifeStage == LifeStage.Child) { continue; }
            villager.Alive = false;
            villager.State = VillagerState.Dead;
            villager.DiedAtTick = world.Tick;
            killed++;
        }

        Assert.Equal(3, killed);
        loop.Step(11);
        List<Villager> arrived = Arrived(world);
        Assert.NotEmpty(arrived);
        Assert.True(arrived.Count <= 2, "the pose needs a band smaller than the deaths");
        Assert.Equal(peak, world.MostEverAlive);
    }

    // ---------------------------------------------------------------
    //  Saved
    // ---------------------------------------------------------------

    /// <summary>A settled family survives a save — their arrival tick with them (the hash reads it, so a dropped one parts the year).</summary>
    [Fact]
    public void ASettledFamilySurvivesASaveAndItsYearMatches()
    {
        (SimLoop loop, InMemoryLogSink log) = Knocked(Village);
        loop.Step(3);
        JsonObject document = SaveGame.Capture(loop, "test", "2026-10-11 12:00", "test");
        var loadedLog = new InMemoryLogSink();
        SimLoop loaded = SaveGame.Load(JsonNode.Parse(SaveFile.TextOf(document))!.AsObject(), loop.World.Config, loadedLog);
        Assert.Equal(StateHash.Compute(loop.World), StateHash.Compute(loaded.World));
        Assert.Equal(Arrived(loop.World).Count, Arrived(loaded.World).Count);

        loadedLog.Clear();
        int said = log.Entries.Count;
        for (int t = 0; t < Village.TicksPerYear; t++)
        {
            loop.StepOnce();
            loaded.StepOnce();
            Assert.Equal(StateHash.Compute(loop.World), StateHash.Compute(loaded.World));
        }

        Assert.Equal(log.Entries.Skip(said).Select(e => e.ToString()), loadedLog.Entries.Select(e => e.ToString()));
    }

    // ---- The Nomads switch (D570; Joe, D568 and D572: "if nomads are on, that is the permission") ----

    [Fact]
    public void SwitchedOffNobodyEverKnocks()
    {
        (SimLoop loop, _) = Founded(Off);
        Assert.False(loop.World.NomadsEnabled);
        loop.Step((int)KnockTick(loop.World) + Village.TicksPerYear + 1);
        Assert.True(loop.World.Population > 0);
        Assert.DoesNotContain(loop.World.Villagers, v => v.ArrivedAtTick != 0UL);
        Assert.Equal(0, loop.World.BandsEverKnocked);
    }

    /// <summary>Back on with the knock's year already past: the band comes on the next year's turn, not never.</summary>
    [Fact]
    public void SwitchedOnAgainTheOverdueBandKnocksAtTheNextYearTurn()
    {
        (SimLoop loop, _) = Founded(Off);
        loop.Step((int)KnockTick(loop.World) + (Village.TicksPerYear * 2) + 7);
        Assert.DoesNotContain(loop.World.Villagers, v => v.ArrivedAtTick != 0UL);

        loop.World.NomadsEnabled = true;
        loop.Step(Village.TicksPerYear - (int)(loop.World.Tick % (ulong)Village.TicksPerYear));
        ulong turn = loop.World.Tick;
        Assert.Empty(ArrivedAt(loop.World, turn));
        loop.StepOnce();
        Assert.NotEmpty(ArrivedAt(loop.World, turn));
        Assert.Equal(1, loop.World.Clock.DayOfSeason);
        Assert.Equal(Season.Spring, loop.World.Clock.Season);
        Assert.Equal(1, loop.World.BandsEverKnocked);
    }

    /// <summary>Hashed only when off (the share-out shape), so a village that never touched it hashes as it did.</summary>
    [Fact]
    public void TheSwitchIsInTheFingerprintOnlyWhenOff()
    {
        (SimLoop on, _) = Founded(Village);
        (SimLoop off, _) = Founded(Off);
        on.Step(100);
        off.Step(100);
        Assert.NotEqual(StateHash.Compute(on.World), StateHash.Compute(off.World));

        off.World.NomadsEnabled = true;
        Assert.Equal(StateHash.Compute(on.World), StateHash.Compute(off.World));
    }

    /// <summary>The save carries it, and wins over the config it is loaded with.</summary>
    [Fact]
    public void TheSwitchSurvivesASave()
    {
        (SimLoop loop, _) = Founded(Off);
        loop.Step(50);
        JsonObject document = SaveGame.Capture(loop, "test", "2026-10-12 12:00", "test");
        SimLoop loaded = SaveGame.Load(JsonNode.Parse(SaveFile.TextOf(document))!.AsObject(), Village, new InMemoryLogSink());
        Assert.False(loaded.World.NomadsEnabled);
        Assert.Equal(StateHash.Compute(loop.World), StateHash.Compute(loaded.World));
    }

    /// <summary>
    /// ⛔ The longhouse is gone (D571; Joe, D568): a settled family builds in the paint like everybody, and when the
    /// paint is full the village says so and KEEPS saying so — `NeedsMoreResidentialLand` is the latch the villagers
    /// bar's amber *unhoused* count reads, set the day there is nowhere to build and cleared the day a site is marked.
    /// </summary>
    [Fact]
    public void ASettledFamilyWithNoPaintIsUnhousedAndTheVillageSaysSo()
    {
        (SimLoop loop, InMemoryLogSink log) = Founded(Village);
        SimWorld world = loop.World;
        ulong knock = KnockTick(world);
        loop.Step((int)knock);

        // The paint erased the tick before the band comes, everywhere a house does not already stand.
        var erased = new List<GridPos>();
        for (int y = world.Map.MinY; y < world.Map.MinY + world.Map.Height; y++)
        {
            for (int x = world.Map.MinX; x < world.Map.MinX + world.Map.Width; x++)
            {
                var tile = new GridPos(x, y);
                bool lived = false;
                foreach (Household household in world.Households)
                {
                    if (world.HomeFootprintOf(household)?.Covers(tile) == true)
                    {
                        lived = true;
                    }
                }

                if (!lived && world.Zones.IsResidential(tile))
                {
                    world.EraseResidential(tile);
                    erased.Add(tile);
                }
            }
        }

        Assert.NotEmpty(erased);
        Assert.False(world.NeedsMoreResidentialLand);
        loop.StepOnce();
        List<Household> settled = ArrivedHouseholds(world);
        Assert.NotEmpty(settled);
        loop.Step(Village.TicksPerDay * 2);

        Assert.All(settled, h => Assert.False(h.HasHome));
        Assert.All(settled, h => Assert.Null(world.HomeSiteFor(h.Id)));
        Assert.All(settled, h => Assert.True(world.LivingMembersOf(h) > 0));
        Assert.True(world.NeedsMoreResidentialLand, "nowhere to build, and the latch the bar reads is not set");
        Assert.Contains("has nowhere to build", Said(log), StringComparison.Ordinal);

        // Paint it back: a site is marked the next day and the latch clears — the warning is standing, not sticky.
        foreach (GridPos tile in erased)
        {
            world.PaintResidential(tile);
        }

        loop.Step(Village.TicksPerDay * 2);
        Assert.Contains(settled, h => h.HasHome || world.HomeSiteFor(h.Id) is not null);
        Assert.False(world.NeedsMoreResidentialLand, "a site was marked and the latch did not clear");
    }

    /// <summary>
    /// "The pile" said nothing to Joe (D568); the arrival names the camp in the village's words (D569) — by the cart
    /// while it stands, at the founding site where there is none. The warm fixture has no cart; the cold village,
    /// its opening played, keeps the founders' wagon.
    /// </summary>
    [Fact]
    public void TheKnockSaysWhereTheBandCamps()
    {
        (SimLoop warm, _) = Knocked(Village);
        Assert.Null(warm.World.TheCart);
        Assert.Equal("at the founding site", warm.World.WhereTheBandCamps);
        Moment atTheSite = Assert.Single(warm.World.Moments, m => m.Title.Contains("Nomads", StringComparison.Ordinal));
        Assert.Equal("Nomads have settled at the founding site", atTheSite.Title);
        Assert.Contains("in the open at the founding site", atTheSite.Body, StringComparison.Ordinal);
        Assert.DoesNotContain("pile", atTheSite.Body, StringComparison.Ordinal);

        var log = new InMemoryLogSink();
        SimLoop cold = SimFactory.CreatePhase0(ColdStartTests.ColdVillage, log);
        ColdStartTests.PlayTheOpening(cold.World);
        cold.Step((int)KnockTick(cold.World) + 1);
        Assert.NotNull(cold.World.TheCart);
        List<Villager> arrived = Arrived(cold.World);
        Assert.NotEmpty(arrived);
        Assert.Equal(cold.World.TheCart!.Position, cold.World.BandCamp);
        Assert.All(arrived, v => Assert.Equal(cold.World.BandCamp, v.Position));
        Assert.Equal("by the cart", cold.World.WhereTheBandCamps);
        Moment byTheCart = Assert.Single(cold.World.Moments, m => m.Title.Contains("Nomads", StringComparison.Ordinal));
        Assert.Equal("Nomads have settled by the cart", byTheCart.Title);
        Assert.Contains("in the open by the cart", byTheCart.Body, StringComparison.Ordinal);
        Assert.True(arrived.Count >= Village.NomadsBandMin);
    }
}

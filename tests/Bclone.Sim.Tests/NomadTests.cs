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
/// The nomads, commit 1 — the band that knocks, waits, and settles or moves on (`specs/nomads.md §4.1–4.4`, §9).
/// </summary>
/// <remarks>
/// <para>
/// ⭐ <b>The anti-vacuity twin comes first:</b> a village whose first-year floor is 0 never meets a band and hashes,
/// to the tick, as a village did before bands existed; and a village on the shipped clock hashes the same until its
/// knock. Everything else here is posed on the fixture village, whose first knock is a hash of its seed —
/// <see cref="KnockTick"/> asks the sim rather than assuming a year.
/// </para>
/// <para>
/// ⛔ The band is posed by <b>stepping to the knock</b>, never by building one by hand: a band built by hand
/// cannot tell the test that the sim fails to raise one (D157's green-and-blind).
/// </para>
/// </remarks>
public sealed class NomadTests
{
    private readonly ITestOutputHelper _output;

    public NomadTests(ITestOutputHelper output) => _output = output;

    private static SimConfig Village => VillageFixtures.Village;

    private static SimConfig Silent => Village with { NomadsFirstYearFloor = 0 };

    private static (SimLoop Loop, InMemoryLogSink Log) Founded(SimConfig config)
    {
        var log = new InMemoryLogSink();
        return (SimFactory.CreatePhase0(config, log), log);
    }

    /// <summary>The spring tick the fixture's first band knocks on.</summary>
    private static ulong KnockTick(SimWorld world) => (ulong)(world.FirstKnockYear() - 1) * (ulong)world.Config.TicksPerYear;

    /// <summary>
    /// Step through the knock: the systems compute a tick before the loop advances it (`SimLoop.StepOnce`), so the
    /// band raised on the knock tick is there once the loop has stepped one tick past it.
    /// </summary>
    private static (SimLoop Loop, InMemoryLogSink Log) Knocked(SimConfig config)
    {
        (SimLoop loop, InMemoryLogSink log) = Founded(config);
        loop.Step((int)KnockTick(loop.World) + 1);
        Assert.NotNull(loop.World.Band);
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
        Assert.Null(loop.World.Band);
        Assert.Equal(0, loop.World.BandsEverKnocked);
        Assert.Equal(0, loop.World.FirstKnockYear());
    }

    /// <summary>
    /// ⭐ THE ANTI-VACUITY TWIN: until the knock, a village on the shipped clock is byte for byte the village with no
    /// nomads at all — nothing is drawn, nothing is mixed. And on the knock tick they part.
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
        Assert.NotNull(clocked.World.Band);
        Assert.NotEqual(StateHash.Compute(silent.World), StateHash.Compute(clocked.World));

        // ⛔ And still no draw: the knock is a hash, so the stream is where the silent village's is.
        Assert.Equal(silent.World.Rng.State, clocked.World.Rng.State);
        Assert.Equal(silent.World.Rng.Inc, clocked.World.Rng.Inc);
    }

    [Fact]
    public void TheBandKnocksOnTheSpringTurnOfItsYearAndStops()
    {
        (SimLoop loop, InMemoryLogSink log) = Founded(Village);
        ulong knock = KnockTick(loop.World);
        loop.Step((int)knock);
        Assert.Null(loop.World.Band);

        loop.StepOnce();
        NomadBand band = Assert.IsType<NomadBand>(loop.World.Band);
        Assert.Equal(knock, band.KnockedAtTick);
        Assert.Equal(Season.Spring, loop.World.Clock.Season);
        Assert.Equal(1, loop.World.Clock.DayOfSeason);
        Assert.Equal(loop.World.FirstKnockYear(), loop.World.Clock.Year);
        Assert.Equal(1, loop.World.BandsEverKnocked);
        Assert.Equal(loop.World.Population, band.MetAPopulationOf);

        Moment moment = Assert.Single(loop.World.Moments, m => m.Title.Contains("Nomads", StringComparison.Ordinal));
        Assert.True(moment.WaitsToBeDismissed, "the knock does not wait — at 4× it slides past unread (D232)");
        Assert.DoesNotContain("settle?", moment.Body, StringComparison.Ordinal);
        Assert.Contains("Click them to decide", moment.Body, StringComparison.Ordinal);
        Assert.Contains("come up the valley", Said(log), StringComparison.Ordinal);
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
        Assert.Null(loop.World.Band);
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
        NomadBand band = loop.World.Band!;
        Assert.Equal(NomadBand.PeopleFor(Village, band.MetAPopulationOf, false), band.People);
        foreach (NomadFamily family in band.Families)
        {
            Assert.Equal(2, family.Adults);
            foreach (NomadPerson person in family.People)
            {
                Assert.InRange(person.AgeYears, person.Adult ? 18 : 2, person.Adult ? 40 : 11);
                Assert.InRange(person.LifespanYears, Village.LifespanYearsBase - Village.LifespanYearsVariance, Village.LifespanYearsBase + Village.LifespanYearsVariance);
                Assert.InRange(person.Rhythm, 0, Village.TicksPerDay - 1);
            }
        }

        int journeymen = band.Families.Sum(f => f.People.Count(p => p.TradeSkillId != 0));
        Assert.Equal(1, journeymen);
        Assert.Contains(Village.FoundingSkills(), s => s.Id == band.Families.SelectMany(f => f.People).Single(p => p.TradeSkillId != 0).TradeSkillId);
    }

    [Fact]
    public void EverySurnameIsNewAndEveryFirstNameItsOwn()
    {
        // Posed with a village large enough for two families, so the two-family rule is exercised.
        (SimLoop loop, _) = Knocked(Village with { NomadsBandMin = 6 });
        NomadBand band = loop.World.Band!;
        Assert.Equal(2, band.Families.Count);

        var living = new HashSet<string>(loop.World.Households.Where(h => loop.World.LivingMembersOf(h) > 0).Select(h => h.Surname));
        Assert.All(band.Families, f => Assert.DoesNotContain(f.Surname, living));
        Assert.NotEqual(band.Families[0].Surname, band.Families[1].Surname);

        var names = band.Families.SelectMany(f => f.People).Select(p => p.Name).ToList();
        Assert.Equal(names.Count, names.Distinct(StringComparer.Ordinal).Count());
        var alive = new HashSet<string>(loop.World.Villagers.Where(v => v.Alive).Select(v => v.Name));
        Assert.All(names, n => Assert.DoesNotContain(n, alive));
    }

    // ---------------------------------------------------------------
    //  The wait, and the two answers (§4.3, §4.4)
    // ---------------------------------------------------------------

    [Fact]
    public void AnUnansweredBandMovesOnAtTheAutumnTurn()
    {
        (SimLoop loop, InMemoryLogSink log) = Knocked(Village);
        int population = loop.World.Population;
        int knockYear = loop.World.Clock.Year;
        ulong leaves = loop.World.BandLeavesAtTick;
        Assert.Equal(loop.World.Band!.KnockedAtTick + (2UL * (ulong)Village.TicksPerSeason), leaves);

        loop.Step((int)(leaves - loop.World.Tick));
        Assert.NotNull(loop.World.Band);
        loop.StepOnce();
        Assert.Null(loop.World.Band);
        Assert.Equal(Season.Fall, loop.World.Clock.Season);
        Assert.Equal(population, loop.World.Population);
        Assert.Equal(1, loop.World.BandsEverKnocked);
        Assert.InRange(loop.World.NextKnockYear, knockYear + Village.NomadsYearsBetweenFloor, knockYear + Village.NomadsYearsBetweenFloor + Village.NomadsYearsBetweenSpread);
        Assert.Contains("moved on", Said(log), StringComparison.Ordinal);
        Moment gone = Assert.Single(loop.World.Moments, m => m.Title.Contains("moved on", StringComparison.Ordinal));
        Assert.False(gone.WaitsToBeDismissed, "a band leaving is a passing line, not a stop");
    }

    [Fact]
    public void SendingThemOnLeavesTheVillageAsItWasButForTheCount()
    {
        (SimLoop loop, InMemoryLogSink log) = Knocked(Village);
        int people = loop.World.Villagers.Count;
        int households = loop.World.Households.Count;
        loop.World.SendTheBandOn();
        Assert.Null(loop.World.Band);
        Assert.Equal(people, loop.World.Villagers.Count);
        Assert.Equal(households, loop.World.Households.Count);
        Assert.True(loop.World.NextKnockYear > loop.World.Clock.Year);
        Assert.Contains("sent on their way", Said(log), StringComparison.Ordinal);
        Assert.Throws<InvalidOperationException>(() => loop.World.SendTheBandOn());

        // ⚠️ "But for the count": the count and the next year are state — a village a band came to and left is not the
        // village nobody ever knocked on, and the fingerprint says so (the hash block that did not build as a mutant).
        (SimLoop silent, _) = Founded(Silent);
        silent.Step((int)loop.World.Tick);
        Assert.NotEqual(StateHash.Compute(silent.World), StateHash.Compute(loop.World));
    }

    [Fact]
    public void TheNextKnockFollowsTheLastOnItsOwnYear()
    {
        (SimLoop loop, _) = Knocked(Village);
        loop.World.SendTheBandOn();
        int next = loop.World.NextKnockYear;
        loop.Step((int)(((ulong)(next - 1) * (ulong)Village.TicksPerYear) - loop.World.Tick));
        Assert.Null(loop.World.Band);
        loop.StepOnce();
        Assert.True(loop.World.Population == 0 || loop.World.Band is not null, "the second band did not knock on its year");
        if (loop.World.Band is not null)
        {
            Assert.Equal(2, loop.World.BandsEverKnocked);
            Assert.Equal(next, loop.World.Clock.Year);
        }
    }

    [Fact]
    public void SettlingAddsRooflessFamiliesHoldingTheirFood()
    {
        (SimLoop loop, InMemoryLogSink log) = Knocked(Village);
        SimWorld world = loop.World;
        NomadBand band = world.Band!;
        int before = world.Population;
        int mostBefore = world.MostEverAlive;
        int householdsBefore = world.Households.Count;
        ulong tick = world.Tick;

        world.LetTheBandSettle();

        Assert.Null(world.Band);
        Assert.Equal(before + band.People, world.Population);
        Assert.Equal(mostBefore + band.People, world.MostEverAlive);
        Assert.Equal(householdsBefore + band.Families.Count, world.Households.Count);
        Assert.Contains("have settled", Said(log), StringComparison.Ordinal);

        for (int f = 0; f < band.Families.Count; f++)
        {
            Household home = world.Households[householdsBefore + f];
            Assert.Equal(band.Families[f].Surname, home.Surname);
            Assert.Null(home.HomePosition);
            Assert.Equal(band.Families[f].People.Count * Village.NomadsFoodEach, home.Stockpile[Goods.Produce]);
            Assert.Equal(band.Families[f].People.Count, world.LivingMembersOf(home));

            var members = world.Villagers.Where(v => v.HouseholdId == home.Id).ToList();
            var adults = members.Where(v => v.LifeStage == LifeStage.Adult).ToList();
            Assert.Equal(2, adults.Count);
            Assert.Equal(adults[0].Id, adults[1].PartnerId);
            Assert.Equal(adults[1].Id, adults[0].PartnerId);
            foreach (Villager villager in members)
            {
                Assert.Equal(tick, villager.ArrivedAtTick);
                Assert.False(villager.Founder);
                Assert.Equal(home.Surname, villager.Surname);
                Assert.Equal(world.BandCamp, villager.Position);
                Assert.True(villager.LifeStage == LifeStage.Adult || villager.PartnerId == 0);
            }
        }

        Assert.True(world.NextKnockYear > world.Clock.Year);
        Assert.Throws<InvalidOperationException>(() => world.LetTheBandSettle());
    }

    [Fact]
    public void TheWoodenBridgeIsLearnedByArrivals()
    {
        // The most the village has ever held at the knock, read off a first run; the posed village learns the bridge
        // at one more than that, so only an arrival can teach it.
        (SimLoop first, _) = Knocked(Village);
        SimConfig config = Village with { BridgeUnlockPopulation = first.World.MostEverAlive + 1 };
        (SimLoop posed, _) = Knocked(config);
        Assert.False(posed.World.IsUnlocked(BuildingKind.Bridge), "the bridge is known before anybody arrived — the pose is wrong");
        posed.World.LetTheBandSettle();
        Assert.True(posed.World.IsUnlocked(BuildingKind.Bridge));
        Assert.Contains(posed.World.Moments, m => m.Title.Contains("bridge", StringComparison.Ordinal));
    }

    [Fact]
    public void ASettledFamilyIsHousedByTheVillagesOwnQueue()
    {
        (SimLoop loop, _) = Knocked(Village);
        SimWorld world = loop.World;
        int householdsBefore = world.Households.Count;
        world.LetTheBandSettle();
        Household home = world.Households[householdsBefore];

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
        int householdsBefore = world.Households.Count;
        world.LetTheBandSettle();
        Household home = world.Households[householdsBefore];
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
        int before = world.Villagers.Count;
        int families = world.Band!.Families.Count;
        world.LetTheBandSettle();
        var settled = world.Villagers.Skip(before).ToList();
        Household home = world.Households[^1];
        Assert.All(settled, v => Assert.Equal(Shelter.Outdoors, world.ShelterAt(v.Position.ToTile())));

        // Held roofless for a year, the winter chills them in the open; whether it kills them is the exposure
        // model's (D45, D53 — a villager may warm at a neighbour's fire), guarded where it lives. What this
        // guards is that a settled family has no shelter of its own and feels the cold for it.
        int coldest = 0;
        for (int day = 0; day < Village.DaysPerSeason * 4; day++)
        {
            if (world.HomeSiteFor(home.Id) is Workplace site)
            {
                world.CancelConstruction(site);
            }

            foreach (Household household in world.Households.Skip(world.Households.Count - families))
            {
                household.HomePosition = null;
            }

            loop.Step(Village.TicksPerDay);
            foreach (Villager villager in settled)
            {
                coldest = Math.Max(coldest, villager.Cold);
            }
        }

        Assert.True(coldest > 0, "a family with no roof went a whole winter without feeling the cold");
        Assert.All(world.Households.Skip(world.Households.Count - families), h => Assert.False(h.HasHome));
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
        var living = new HashSet<string>(loop.World.Households.Where(h => loop.World.LivingMembersOf(h) > 0).Select(h => h.Surname));
        Assert.Equal(2, living.Count);
        string free = new[] { "Ash", "Birch", "Cedar" }.Single(n => !living.Contains(n));
        Assert.Equal(free, loop.World.Band!.Families[0].Surname);
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
        Assert.NotNull(world.Band);
        Assert.True(world.Band!.People <= 2, "the pose needs a band smaller than the deaths");
        world.LetTheBandSettle();
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
        loop.World.LetTheBandSettle();
        loop.Step(3);
        JsonObject document = SaveGame.Capture(loop, "test", "2026-10-11 12:00", "test");
        var loadedLog = new InMemoryLogSink();
        SimLoop loaded = SaveGame.Load(JsonNode.Parse(SaveFile.TextOf(document))!.AsObject(), loop.World.Config, loadedLog);
        Assert.Equal(StateHash.Compute(loop.World), StateHash.Compute(loaded.World));
        Assert.Contains(loaded.World.Villagers, v => v.ArrivedAtTick != 0UL);

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

    /// <summary>
    /// "The pile" said nothing to Joe (D568); the knock names the camp in the village's words (D569) — by the cart
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
        Assert.Equal("Nomads at the founding site", atTheSite.Title);
        Assert.Contains("camp at the founding site", atTheSite.Body, StringComparison.Ordinal);
        Assert.DoesNotContain("pile", atTheSite.Body, StringComparison.Ordinal);

        var log = new InMemoryLogSink();
        SimLoop cold = SimFactory.CreatePhase0(ColdStartTests.ColdVillage, log);
        ColdStartTests.PlayTheOpening(cold.World);
        cold.Step((int)KnockTick(cold.World) + 1);
        Assert.NotNull(cold.World.TheCart);
        NomadBand band = Assert.IsType<NomadBand>(cold.World.Band);
        Assert.Equal(cold.World.TheCart!.Position, cold.World.BandCamp);
        Assert.Equal("by the cart", cold.World.WhereTheBandCamps);
        Moment byTheCart = Assert.Single(cold.World.Moments, m => m.Title.Contains("Nomads", StringComparison.Ordinal));
        Assert.Equal("Nomads by the cart", byTheCart.Title);
        Assert.Contains("camp by the cart", byTheCart.Body, StringComparison.Ordinal);
        Assert.True(band.People >= Village.NomadsBandMin);
    }

    [Fact]
    public void ABandSurvivesASaveAndItsYearMatches()
    {
        (SimLoop loop, InMemoryLogSink log) = Knocked(Village);
        loop.Step(3);
        JsonObject document = SaveGame.Capture(loop, "test", "2026-10-11 12:00", "test");
        var loadedLog = new InMemoryLogSink();
        SimLoop loaded = SaveGame.Load(JsonNode.Parse(SaveFile.TextOf(document))!.AsObject(), loop.World.Config, loadedLog);
        Assert.NotNull(loaded.World.Band);
        Assert.Equal(loop.World.Band!.FamilyNames, loaded.World.Band!.FamilyNames);
        Assert.Equal(StateHash.Compute(loop.World), StateHash.Compute(loaded.World));

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
}

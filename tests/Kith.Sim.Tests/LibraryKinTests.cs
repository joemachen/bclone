using System.Text.Json.Nodes;
using Kith.Sim.Config;
using Kith.Sim.Core;
using Kith.Sim.Determinism;
using Kith.Sim.Logging;
using Kith.Sim.Persistence;
using Kith.Sim.World;
using Xunit;
using Xunit.Abstractions;

namespace Kith.Sim.Tests;

/// <summary>
/// The library's borrowing, step 2 (`specs/library-borrowing.md §5.2–§5.3a`; Joe's calls D865): a household's letters are
/// answered on a hashed letter; kin come when there is a bed and the village has food to spare, else the answer stands, said
/// once, and is honoured at the first season's turn both hold; once a generation; a new family in the house starts afresh.
/// </summary>
public sealed class LibraryKinTests
{
    private readonly ITestOutputHelper _output;

    public LibraryKinTests(ITestOutputHelper output) => _output = output;

    private static SimConfig Config => VillageFixtures.Village;

    /// <summary>
    /// ⭐ The answer comes with its hashed letter, never before: one unmarried adult of 18–30, a surname of their own, generation
    /// 1, new to the valley, at the door, kin of the household, carrying the nomads' 20 food into its larder by the ledger's
    /// door — and the household's generation closes.
    /// </summary>
    [Fact]
    public void TheAnswerComesWithItsHashedLetter()
    {
        var sink = new InMemoryLogSink();
        (_, SimWorld world, _) = LibraryBorrowingTests.AVillageWithALibrary(Config with { LettersAnsweredWithin = 6 }, sink);
        (Household home, Villager writer) = AWritingHousehold(world);
        int answering = world.AnsweringLetter(home);
        Assert.InRange(answering, 1, 6);

        int people = world.Villagers.Count;
        int larder = world.FoodIn(home.Stockpile);
        int broughtIn = world.FoodEverBroughtIn;
        for (int letter = 1; letter < answering; letter++)
        {
            world.WriteALetter(writer);
            Assert.Equal(letter, home.LettersWritten);
            Assert.Equal(people, world.Villagers.Count);
        }

        world.WriteALetter(writer);
        Villager kin = Assert.Single(world.Villagers.Skip(people));
        _output.WriteLine($"letter {answering}: {kin.FullName}, {kin.AgeYears}, kin of the {home.Name}");

        Assert.Equal(home.Id, kin.CameByLetterOf);
        Assert.Equal(home.Id, kin.HouseholdId);
        Assert.Contains(kin.Id, home.MemberIds);
        Assert.Equal(LifeStage.Adult, kin.LifeStage);
        Assert.InRange(kin.AgeYears, 18, 30);
        Assert.False(kin.IsPaired);
        Assert.False(kin.Founder);
        Assert.Equal(1, kin.Generation);
        Assert.Equal(world.Tick, kin.ArrivedAtTick);
        Assert.Equal(home.HomeTile, kin.Tile);
        Assert.NotEqual(home.Surname, kin.Surname);
        Assert.DoesNotContain(world.Villagers, v => v.Alive && v.Id != kin.Id && v.Surname == kin.Surname);

        Assert.Equal(kin.Id, home.KinId);
        Assert.Equal(writer.Id, home.LetterWriterId);
        Assert.Equal(1, home.KinAnswered);
        Assert.Equal(0, home.LettersWritten);
        Assert.False(home.AnswerWaiting);
        Assert.Equal(broughtIn + world.Config.NomadsFoodEach, world.FoodEverBroughtIn);
        Assert.Equal(larder + world.Config.NomadsFoodEach, world.FoodIn(home.Stockpile));
        Assert.Contains(sink.Entries, e => e.Message.StartsWith(
            $"{kin.FullName} has come up the valley to live with the {home.Name} household, at {writer.Name}'s letter.",
            StringComparison.Ordinal));

        // The generation is closed: a spare bed still, and nobody writes; a letter brought home now is put away uncounted.
        Assert.True(world.HasASpareBed(home));
        Assert.False(world.HouseholdIsWriting(home));
        Assert.Equal(world.Clock.Year + world.Config.LettersGenerationYears, world.KinMayComeAgainFrom(home));
        world.WriteALetter(writer);
        Assert.Equal(0, home.LettersWritten);
        Assert.Equal(people + 1, world.Villagers.Count);
    }

    /// <summary>
    /// ⛔ Joe's caps (D865): a full house holds the answer, said once; then the village's empty stores hold it (the birth gate's
    /// food half, call 4); then it is honoured, at the writer's letter. Red with either gate unread.
    /// </summary>
    [Fact]
    public void AFullHouseThenEmptyStoresHoldTheAnswerThenItIsHonoured()
    {
        var sink = new InMemoryLogSink();
        (_, SimWorld world, Library library) =
            LibraryBorrowingTests.AVillageWithALibrary(Config with { LettersAnsweredWithin = 1 }, sink);
        (Household home, Villager writer) = AWritingHousehold(world);
        List<Villager> children = FillTheHouse(world, home);
        GiveTheVillageFoodToSpare(world);
        int people = world.Villagers.Count;

        // The paper was fetched while there was a bed; a child filled it before the letter was written.
        world.WriteALetter(writer);
        Assert.Equal(people, world.Villagers.Count);
        Assert.True(home.AnswerWaiting);
        Assert.Equal("a bed", world.WhatTheAnswerWaitsOn(home));
        Assert.False(world.HouseholdIsWriting(home));
        Assert.Contains("1 answer waits on a bed", world.SayTheLibrary(library).Sentence, StringComparison.Ordinal);
        string refusal = $"A letter came back for the {home.Name} household: kin will come when there is a bed in the house.";
        Assert.Single(sink.Entries, e => e.Message.StartsWith(refusal, StringComparison.Ordinal));

        world.HonourTheWaitingAnswers();
        Assert.Equal(people, world.Villagers.Count);

        // A bed comes free; the stores are emptied.
        children[0].Alive = false;
        Assert.True(world.HasASpareBed(home));
        EmptyTheStores(world);
        Assert.False(world.TheVillageHasFoodToSpare());
        world.HonourTheWaitingAnswers();
        Assert.Equal(people, world.Villagers.Count);
        Assert.Equal("food to spare", world.WhatTheAnswerWaitsOn(home));
        Assert.False(world.HouseholdIsWriting(home), "A house with an answer standing wrote again.");
        Assert.Contains("1 answer waits on food to spare", world.SayTheLibrary(library).Sentence, StringComparison.Ordinal);

        // Food to spare again: honoured, at the letter that was answered.
        GiveTheVillageFoodToSpare(world);
        world.HonourTheWaitingAnswers();
        Villager kin = Assert.Single(world.Villagers.Skip(people));
        Assert.Equal(home.Id, kin.CameByLetterOf);
        Assert.False(home.AnswerWaiting);
        Assert.Null(world.WhatTheAnswerWaitsOn(home));
        Assert.Contains(sink.Entries, e => e.Message.StartsWith(
            $"{kin.FullName} has come up the valley to live with the {home.Name} household, at {writer.Name}'s letter.",
            StringComparison.Ordinal));
        Assert.Contains("kin have come to 1 home", world.SayTheLibrary(library).Sentence, StringComparison.Ordinal);
        Assert.Single(sink.Entries, e => e.Message.StartsWith("A letter came back for", StringComparison.Ordinal));
    }

    /// <summary>A waiting answer is asked again only on a season's turn, by the sim itself — not every tick.</summary>
    [Fact]
    public void AWaitingAnswerIsAskedOnTheSeasonsTurn()
    {
        (SimLoop loop, SimWorld world, _) = LibraryBorrowingTests.AVillageWithALibrary(Config with { LettersAnsweredWithin = 1 });
        (Household home, Villager writer) = AWritingHousehold(world);
        List<Villager> children = FillTheHouse(world, home);
        GiveTheVillageFoodToSpare(world);
        world.WriteALetter(writer);
        Assert.True(home.AnswerWaiting);
        children[0].Alive = false;

        for (int tick = 0; tick < 2 * world.Config.TicksPerSeason && home.AnswerWaiting; tick++)
        {
            loop.StepOnce();
        }

        Assert.False(home.AnswerWaiting);
        Villager kin = world.Villagers.Single(v => v.CameByLetterOf == home.Id);
        Assert.Equal(0UL, kin.ArrivedAtTick % (ulong)world.Config.TicksPerSeason);
    }

    /// <summary>⛔ Once a generation (call 2A): with a one-year generation the house writes again the next year, and a fresh hash answers it.</summary>
    [Fact]
    public void KinComeOnceAGeneration()
    {
        (SimLoop loop, SimWorld world, _) =
            LibraryBorrowingTests.AVillageWithALibrary(Config with { LettersAnsweredWithin = 1, LettersGenerationYears = 1 });
        (Household home, Villager writer) = AWritingHousehold(world);
        world.WriteALetter(writer);
        Villager first = world.Villagers.Single(v => v.CameByLetterOf == home.Id);
        Assert.False(world.HouseholdIsWriting(home));

        int from = world.KinMayComeAgainFrom(home);
        while (world.Clock.Year < from)
        {
            Assert.False(world.HouseholdIsWriting(home), $"The house wrote again in Year {world.Clock.Year}, inside its generation.");
            loop.StepOnce();
        }

        Assert.True(world.HasASpareBed(home));
        Assert.True(world.HouseholdIsWriting(home));
        // The first gone before the second comes: alive, they hold their name and surname, and the name rules alone would send
        // somebody else — the fresh key would go unasked (D326's zero, found on the first red-check and fixed).
        first.Alive = false;
        Villager second = world.Villagers.First(v => v.HouseholdId == home.Id && v.CanWork && v.Alive);
        world.WriteALetter(second);
        Assert.Equal(2, home.KinAnswered);
        Villager next = world.Villagers.Single(v => v.CameByLetterOf == home.Id && v.Id != first.Id);
        Assert.NotEqual(first.FullName, next.FullName);
    }

    /// <summary>⭐ A hash, never a draw (§6): the same seed sends the same relative; and the answering letter differs between houses.</summary>
    [Fact]
    public void TheSameSeedSendsTheSameRelative()
    {
        Villager Send()
        {
            (_, SimWorld world, _) = LibraryBorrowingTests.AVillageWithALibrary(Config with { LettersAnsweredWithin = 1 });
            (Household home, Villager writer) = AWritingHousehold(world);
            world.WriteALetter(writer);
            return world.Villagers.Single(v => v.CameByLetterOf == home.Id);
        }

        Villager a = Send(), b = Send();
        Assert.Equal((a.Id, a.FullName, a.AgeYears, a.LifespanYears, a.Rhythm, a.BirthTick),
            (b.Id, b.FullName, b.AgeYears, b.LifespanYears, b.Rhythm, b.BirthTick));

        var letters = Enumerable.Range(1, 40).Select(id => NameHash.AnsweringLetter(Config.Seed, id, 0, 24)).ToList();
        Assert.All(letters, n => Assert.InRange(n, 1, 24));
        Assert.True(letters.Distinct().Count() > 8, "Forty houses' answering letters are nearly all the same.");
        // And a fresh hash a generation: one house's ten generations are not all answered on the same letter.
        Assert.True(Enumerable.Range(0, 10).Select(g => NameHash.AnsweringLetter(Config.Seed, 1, g, 24)).Distinct().Count() > 1);
    }

    /// <summary>
    /// ⭐ End to end, by the real walk: a relative comes up the valley at a letter written at home, and the food ledger sees the
    /// 20 food come in by its own door on every tick (`FoodConservationTests`' drift).
    /// </summary>
    [Fact]
    public void ARelativeComesByTheWalkAndTheLedgerSeesTheirFood()
    {
        SimConfig config = Config with { LettersAnsweredWithin = 1, LetterEveryDays = 10 };
        (SimLoop loop, SimWorld world, _) = LibraryBorrowingTests.AVillageWithALibrary(config);
        int held0 = world.FoodHeldAnywhere(), produced0 = world.FoodEverProduced;
        int broughtIn0 = world.FoodEverBroughtIn, eaten0 = world.FoodEverEaten;
        for (int tick = 0; tick < 2 * config.TicksPerSeason && !world.Villagers.Any(v => v.CameByLetterOf != 0); tick++)
        {
            loop.StepOnce();
            int drift = (world.FoodEverProduced - produced0) + (world.FoodEverBroughtIn - broughtIn0)
                - (world.FoodEverEaten - eaten0) - (world.FoodHeldAnywhere() - held0);
            Assert.True(drift == 0, $"At tick {world.Tick} {drift} food moved by a door the ledger does not see.");
        }

        Villager kin = Assert.Single(world.Villagers, v => v.CameByLetterOf != 0);
        Household home = world.FindHousehold(kin.CameByLetterOf)!;
        Assert.Equal(kin.Id, home.KinId);
        Assert.True(world.Villagers.Single(v => v.Id == home.LetterWriterId).CanWork);
    }

    /// <summary>⭐ Letters off (D865's switch): a standing answer waits until they are back on. Kin come with the Nomads switch off.</summary>
    [Fact]
    public void LettersOffHoldAWaitingAnswerAndTheNomadsSwitchDoesNot()
    {
        (_, SimWorld world, _) = LibraryBorrowingTests.AVillageWithALibrary(Config with { LettersAnsweredWithin = 1 });
        world.NomadsEnabled = false;
        (Household home, Villager writer) = AWritingHousehold(world);
        List<Villager> children = FillTheHouse(world, home);
        GiveTheVillageFoodToSpare(world);
        world.WriteALetter(writer);
        children[0].Alive = false;

        world.LettersEnabled = false;
        world.HonourTheWaitingAnswers();
        Assert.True(home.AnswerWaiting);
        Assert.DoesNotContain(world.Villagers, v => v.CameByLetterOf != 0);

        world.LettersEnabled = true;
        world.HonourTheWaitingAnswers();
        Assert.Single(world.Villagers, v => v.CameByLetterOf == home.Id);
    }

    /// <summary>A couple moving into a house that stood empty is a new family there (§5.2): its letters start afresh; the hash's count does not.</summary>
    [Fact]
    public void ANewFamilyInTheHouseStartsAfresh()
    {
        (_, SimWorld world, _) = LibraryBorrowingTests.AVillageWithALibrary(Config with { LettersAnsweredWithin = 1 });
        (Household home, Villager writer) = AWritingHousehold(world);
        world.WriteALetter(writer);
        home.LettersWritten = 3;
        home.LastLetterDay = 40;
        home.AnswerWaiting = true;

        home.BecomeTheFamilyOf("Miller");
        Assert.Equal((0, 0, 0, 0, false), (home.LettersWritten, home.LastLetterDay, home.KinId, home.LetterWriterId, home.AnswerWaiting));
        Assert.Equal(1, home.KinAnswered);
        Assert.Equal(0, world.KinMayComeAgainFrom(home));
    }

    /// <summary>The shipped numbers are Joe's (D865); the validator refuses a nought.</summary>
    [Fact]
    public void TheShippedAnswerNumbersAreJoesAndTheValidatorRefusesNought()
    {
        SimConfig shipped = ShippedConfig.Load();
        Assert.Equal(24, shipped.LettersAnsweredWithin);
        Assert.Equal(24, shipped.LettersGenerationYears);
        Assert.Throws<SimConfigException>(() => (shipped with { LettersAnsweredWithin = 0 }).Validate());
        Assert.Throws<SimConfigException>(() => (shipped with { LettersGenerationYears = 0 }).Validate());
    }

    /// <summary>Every field the answer keeps is hashed (sparsely) and saved — a village with none hashes as it did.</summary>
    [Theory]
    [InlineData("kin_id")]
    [InlineData("letter_writer_id")]
    [InlineData("kin_answered")]
    [InlineData("answer_waiting")]
    [InlineData("came_by_letter_of")]
    public void EveryKinFieldIsHashedAndSaved(string field)
    {
        SimLoop loop = SimFactory.CreatePhase0(Config, new InMemoryLogSink());
        SimWorld world = loop.World;
        Household household = world.Households.First();
        Villager adult = world.Villagers.First(v => v.CanWork);
        ulong before = StateHash.Compute(world);
        switch (field)
        {
            case "kin_id": household.KinId = adult.Id; break;
            case "letter_writer_id": household.LetterWriterId = adult.Id; break;
            case "kin_answered": household.KinAnswered = 2; break;
            case "answer_waiting": household.AnswerWaiting = true; break;
            case "came_by_letter_of": adult.CameByLetterOf = household.Id; break;
            default: throw new ArgumentOutOfRangeException(nameof(field), field, "no such field");
        }

        Assert.NotEqual(before, StateHash.Compute(world));
        JsonObject document = SaveGame.Capture(loop, "test", "now", "test");
        SimWorld loaded = SaveGame.Load(JsonNode.Parse(SaveFile.TextOf(document))!.AsObject(), world.Config).World;
        Household back = loaded.Households.Single(h => h.Id == household.Id);
        Assert.Equal(
            (household.KinId, household.LetterWriterId, household.KinAnswered, household.AnswerWaiting),
            (back.KinId, back.LetterWriterId, back.KinAnswered, back.AnswerWaiting));
        Assert.Equal(adult.CameByLetterOf, loaded.Villagers.Single(v => v.Id == adult.Id).CameByLetterOf);
        Assert.Equal(StateHash.Compute(world), StateHash.Compute(loaded));
    }

    /// <summary>Saved with an answer standing, loaded, and honoured the same: the same relative, the same hash after.</summary>
    [Fact]
    public void AWaitingAnswerSurvivesSaveAndLoad()
    {
        (SimLoop loop, SimWorld world, _) = LibraryBorrowingTests.AVillageWithALibrary(Config with { LettersAnsweredWithin = 1 });
        (Household home, Villager writer) = AWritingHousehold(world);
        List<Villager> children = FillTheHouse(world, home);
        GiveTheVillageFoodToSpare(world);
        world.WriteALetter(writer);
        Assert.True(home.AnswerWaiting);

        JsonObject document = SaveGame.Capture(loop, "test", "now", "test");
        SimWorld loaded = SaveGame.Load(JsonNode.Parse(SaveFile.TextOf(document))!.AsObject(), world.Config).World;
        Assert.Equal(StateHash.Compute(world), StateHash.Compute(loaded));
        Assert.True(loaded.Households.Single(h => h.Id == home.Id).AnswerWaiting);

        children[0].Alive = false;
        loaded.Villagers.Single(v => v.Id == children[0].Id).Alive = false;
        world.HonourTheWaitingAnswers();
        loaded.HonourTheWaitingAnswers();
        Villager a = world.Villagers.Single(v => v.CameByLetterOf == home.Id);
        Villager b = loaded.Villagers.Single(v => v.CameByLetterOf == home.Id);
        Assert.Equal((a.Id, a.FullName, a.AgeYears), (b.Id, b.FullName, b.AgeYears));
        Assert.Equal(StateHash.Compute(world), StateHash.Compute(loaded));
    }

    /// <summary>The first household with a home, a spare bed and an adult at it — and that adult, the writer.</summary>
    private static (Household Home, Villager Writer) AWritingHousehold(SimWorld world)
    {
        Household home = world.Households.First(world.HouseholdIsWriting);
        Villager writer = world.Villagers.First(v => v.HouseholdId == home.Id && v.Alive && v.CanWork);
        return (home, writer);
    }

    /// <summary>Children posed into the house until its beds are full (`LibraryBorrowingTests.AChildIn`).</summary>
    private static List<Villager> FillTheHouse(SimWorld world, Household home)
    {
        var children = new List<Villager>();
        while (world.HasASpareBed(home))
        {
            children.Add(LibraryBorrowingTests.AChildIn(world, home));
        }

        Assert.NotEmpty(children);
        return children;
    }

    /// <summary>
    /// A birth's food into the first store that gives it — the children posed to fill a house raise what the birth gate asks
    /// (D409's food a head), so the fixture's stores fall short without it.
    /// </summary>
    private static void GiveTheVillageFoodToSpare(SimWorld world)
    {
        StoreBuilding store = world.StoreBuildings.First(s => s.Gives(Goods.Produce));
        store.Store.Add(Goods.Produce, world.FoodABirthNeeds(world.Population));
        Assert.True(world.TheVillageHasFoodToSpare(), "The village's stores would not take a birth's food.");
    }

    /// <summary>Every edible good out of every store and workplace — what the birth gate's food half asks of the village.</summary>
    private static void EmptyTheStores(SimWorld world)
    {
        foreach (Stockpile store in world.StoreBuildings.Select(s => s.Store).Concat(world.Workplaces.Select(w => w.Store)))
        {
            foreach (Goods goods in world.GoodsCatalog.EdibleGoods)
            {
                store.TakeAll(goods);
            }
        }
    }
}

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
/// The library's borrowing, step 1 (`specs/library-borrowing.md §5.1`, §5.4a; Joe's calls D865): an adult whose home is within
/// <c>library_reach_tiles</c> of an open library walks there from a rest at home, takes paper, and writes a letter at their own
/// door — a season apart, while the household has a spare bed and letters are on. The library's card counts the homes in reach
/// and the letters out (D843's lesson).
/// </summary>
public sealed class LibraryBorrowingTests
{
    private readonly ITestOutputHelper _output;

    public LibraryBorrowingTests(ITestOutputHelper output) => _output = output;

    private static SimConfig Config => VillageFixtures.Village;

    /// <summary>
    /// ⭐ The walk and the letter: an adult in reach walks to the library and back, paper in hand, and the household's count rises
    /// on the tick they reach their own door — never before, never for a child, never twice inside <c>letter_every_days</c>.
    /// </summary>
    [Fact]
    public void AnAdultInReachFetchesPaperAndTheLetterIsWrittenAtHome()
    {
        SimConfig config = Config with { LetterEveryDays = 10 };
        (SimLoop loop, SimWorld world, Library library) = AVillageWithALibrary(config);
        Villager child = AChildIn(world, world.Households.First(h => h.HasHome));

        var letters = new Dictionary<int, int>();
        var lastDay = new Dictionary<int, int>();
        bool sawPaper = false, sawAChild = false, tooSoon = false, writtenAway = false;
        for (int tick = 0; tick < 2 * config.TicksPerSeason; tick++)
        {
            var carrying = world.Villagers.Where(v => v.CarryingPaper).Select(v => v.Id).ToHashSet();
            loop.StepOnce();
            foreach (Villager v in world.Villagers)
            {
                sawPaper |= v.CarryingPaper;
                sawAChild |= !v.CanWork && (v.CarryingPaper
                    || v.State is VillagerState.WalkingToTheLibrary or VillagerState.AtTheLibrary);
            }

            foreach (Household h in world.Households)
            {
                int was = letters.GetValueOrDefault(h.Id);
                if (h.LettersWritten == was)
                {
                    continue;
                }

                Assert.Equal(was + 1, h.LettersWritten);
                letters[h.Id] = h.LettersWritten;
                Assert.NotNull(world.LibraryWithinReachOf(h));

                // Written by somebody who was carrying paper and is now at this household's door.
                Villager writer = world.Villagers.Single(v => carrying.Contains(v.Id) && !v.CarryingPaper && v.HouseholdId == h.Id);
                writtenAway |= writer.Tile != world.HomePlaceOf(writer);
                if (lastDay.TryGetValue(h.Id, out int before) && h.LastLetterDay - before < config.LetterEveryDays)
                {
                    tooSoon = true;
                }

                lastDay[h.Id] = h.LastLetterDay;
            }
        }

        _output.WriteLine($"letters {string.Join(", ", letters.Select(kv => $"{kv.Key}:{kv.Value}"))}; {world.SayTheLibrary(library).Sentence}");
        Assert.True(sawPaper, "Nobody carried paper home from a library beside their homes.");
        Assert.NotEmpty(letters);
        Assert.False(sawAChild, "A child went to the library.");
        Assert.True(child.Alive && !child.CanWork, "The posed child grew up or died inside the run — the guard would be vacuous.");
        Assert.False(writtenAway, "A letter was written away from the writer's own door.");
        Assert.False(tooSoon, "A household wrote again inside letter_every_days.");
    }

    /// <summary>⛔ Beyond <c>library_reach_tiles</c> nobody borrows — the walk, not a ring. Red with the reach unread.</summary>
    [Fact]
    public void NobodyBorrowsFromBeyondReach()
    {
        (SimLoop loop, SimWorld world, Library library) = AVillageWithALibrary(Config with { LibraryReachTiles = 1 });
        Assert.All(world.Households, h => Assert.Null(world.LibraryWithinReachOf(h)));

        NobodySetsOut(loop, world, 2 * world.Config.TicksPerSeason);
        NobodyWrote(world);
        Assert.StartsWith("No home is within a 1-tile walk", world.SayTheLibrary(library).Sentence, StringComparison.Ordinal);
        Assert.False(world.SayTheLibrary(library).Working);
    }

    /// <summary>⛔ A full house does not write (Joe's cap: kin need a spare bed). Red with the bed unread.</summary>
    [Fact]
    public void AFullHouseDoesNotWrite()
    {
        (SimLoop loop, SimWorld world, _) = AVillageWithALibrary(Config with { MaxHouseholdSize = 2 });
        Assert.All(
            world.Households.Where(h => world.LivingMembersOf(h) >= 2),
            h => Assert.False(world.HouseholdIsWriting(h)));

        loop.Step(2 * world.Config.TicksPerSeason);
        Assert.All(world.Households.Where(h => h.LettersWritten > 0), h => Assert.True(world.LivingMembersOf(h) < 2));
    }

    /// <summary>⛔ A library fallen in lends nothing, and its card says why. Red with <c>Closed</c> unread.</summary>
    [Fact]
    public void AClosedLibraryLendsNothing()
    {
        (SimLoop loop, SimWorld world, Library library) = AVillageWithALibrary(Config);
        library.Closed = true;

        NobodySetsOut(loop, world, 2 * world.Config.TicksPerSeason);
        NobodyWrote(world);
        Assert.Contains("fallen in", world.SayTheLibrary(library).Sentence, StringComparison.Ordinal);
    }

    /// <summary>⭐ Joe's addition 2 (D865): letters off, nobody fetches paper, and the card says so. Red with the switch unread.</summary>
    [Fact]
    public void WithLettersOffNobodyWrites()
    {
        (SimLoop loop, SimWorld world, Library library) = AVillageWithALibrary(Config);
        world.LettersEnabled = false;

        NobodySetsOut(loop, world, 2 * world.Config.TicksPerSeason);
        NobodyWrote(world);
        Assert.Equal("Letters to kin are off — nobody writes, and nobody comes.", world.SayTheLibrary(library).Sentence);
    }

    /// <summary>The shipped switch starts on and the shipped numbers are Joe's (D865); the validator refuses a nought.</summary>
    [Fact]
    public void TheShippedNumbersAreJoesAndTheValidatorRefusesNought()
    {
        SimConfig shipped = ShippedConfig.Load();
        Assert.True(shipped.LettersEnabled);
        Assert.Equal(12, shipped.LibraryReachTiles);
        Assert.Equal(30, shipped.LetterEveryDays);
        Assert.Equal(2, shipped.LibrarySitTicks);
        Assert.True(SimFactory.CreatePhase0(shipped with { Seed = 3 }, new InMemoryLogSink()).World.LettersEnabled);
        Assert.False(SimFactory.CreatePhase0(shipped with { Seed = 3, LettersEnabled = false }, new InMemoryLogSink()).World.LettersEnabled);
        Assert.Throws<SimConfigException>(() => (shipped with { LibraryReachTiles = 0 }).Validate());
        Assert.Throws<SimConfigException>(() => (shipped with { LetterEveryDays = 0 }).Validate());
        Assert.Throws<SimConfigException>(() => (shipped with { LibrarySitTicks = 0 }).Validate());
    }

    /// <summary>Every field the borrowing keeps is hashed (sparsely) and saved — a village with none hashes as it did.</summary>
    [Theory]
    [InlineData("letters_written")]
    [InlineData("last_letter_day")]
    [InlineData("carrying_paper")]
    [InlineData("letters_off")]
    public void EveryBorrowingFieldIsHashedAndSaved(string field)
    {
        SimLoop loop = SimFactory.CreatePhase0(Config, new InMemoryLogSink());
        SimWorld world = loop.World;
        Household household = world.Households.First();
        Villager adult = world.Villagers.First(v => v.CanWork);
        ulong before = StateHash.Compute(world);
        switch (field)
        {
            case "letters_written": household.LettersWritten = 3; break;
            case "last_letter_day": household.LastLetterDay = 41; break;
            case "carrying_paper": adult.CarryingPaper = true; break;
            case "letters_off": world.LettersEnabled = false; break;
            default: throw new ArgumentOutOfRangeException(nameof(field), field, "no such field");
        }

        Assert.NotEqual(before, StateHash.Compute(world));
        JsonObject document = SaveGame.Capture(loop, "test", "now", "test");
        SimWorld loaded = SaveGame.Load(JsonNode.Parse(SaveFile.TextOf(document))!.AsObject(), world.Config).World;
        Household backHousehold = loaded.Households.Single(h => h.Id == household.Id);
        Assert.Equal(household.LettersWritten, backHousehold.LettersWritten);
        Assert.Equal(household.LastLetterDay, backHousehold.LastLetterDay);
        Assert.Equal(adult.CarryingPaper, loaded.Villagers.Single(v => v.Id == adult.Id).CarryingPaper);
        Assert.Equal(world.LettersEnabled, loaded.LettersEnabled);
        Assert.Equal(StateHash.Compute(world), StateHash.Compute(loaded));
    }

    /// <summary>
    /// A child of eight posed into a household — the nomads' shape (`SimWorld.Settle`): the founding's village has no child for its
    /// first years, so without one the "never a child" guard has nobody to refuse (D326's zero, found and fixed).
    /// </summary>
    private static Villager AChildIn(SimWorld world, Household household)
    {
        SimConfig c = world.Config;
        int id = world.NextVillagerId();
        var child = new Villager
        {
            Id = id,
            Name = "Wren",
            Surname = household.Surname,
            LifespanYears = c.LifespanYearsBase,
            Carried = world.NewStockpile(),
            Position = Point.CentreOf(household.HomeTile!.Value),
            HouseholdId = household.Id,
            BirthTick = (long)world.Tick - (8L * c.TicksPerYear),
            AgeYears = 8,
            LifeStage = LifeStage.Child,
        };

        household.AddMember(id);
        world.Villagers.Add(child);
        return child;
    }

    /// <summary>Steps on, asserting on every tick that nobody is on the library's walk — the offer refused, not only the arrival.</summary>
    private static void NobodySetsOut(SimLoop loop, SimWorld world, int ticks)
    {
        for (int tick = 0; tick < ticks; tick++)
        {
            loop.StepOnce();
            Assert.DoesNotContain(
                world.Villagers, v => v.State is VillagerState.WalkingToTheLibrary or VillagerState.AtTheLibrary);
        }
    }

    private static void NobodyWrote(SimWorld world)
    {
        Assert.All(world.Households, h => Assert.Equal(0, h.LettersWritten));
        Assert.All(world.Villagers, v => Assert.False(v.CarryingPaper));
    }

    /// <summary>
    /// The fixture's village kept warm and fed, with a library stood as near the founding as one may stand — given, as the
    /// village's first library is (`TechniqueTests.GiveThemALibrary`'s shape), not built.
    /// </summary>
    internal static (SimLoop Loop, SimWorld World, Library Library) AVillageWithALibrary(SimConfig config)
    {
        SimLoop loop = SimFactory.CreatePhase0(config, new InMemoryLogSink());
        SimWorld world = loop.World;
        ToolsTests.KeepTheVillageWarmAndFed(world);
        GridPos site = world.Map.FoundingSite;
        for (int radius = 3; radius < 30; radius++)
        {
            for (int dy = -radius; dy <= radius; dy++)
            {
                for (int dx = -radius; dx <= radius; dx++)
                {
                    var tile = new GridPos(site.X + dx, site.Y + dy);
                    // Clear ground by the granary's rule — the library itself is not offered until the village can write.
                    if (!world.CanBuildAt(BuildingKind.Granary, tile).Allowed)
                    {
                        continue;
                    }

                    var library = new Library { Position = Point.CentreOf(tile), Name = "library 1", Shelves = config.LibraryShelves };
                    world.Libraries.Add(library);
                    return (loop, world, library);
                }
            }
        }

        throw new InvalidOperationException("Nowhere near the founding takes a library.");
    }
}

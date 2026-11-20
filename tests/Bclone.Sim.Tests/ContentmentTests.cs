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
/// Contentment, steps 2 and 3 (W2, `specs/morale.md §13`, D798–D802): the causes as rows, the day's tallies, the read at
/// each season's turn, and the card's sentence — and nothing acting on any of it yet.
/// </summary>
public sealed class ContentmentTests
{
    private readonly ITestOutputHelper _output;

    public ContentmentTests(ITestOutputHelper output) => _output = output;

    private static SimConfig Config => VillageFixtures.Village;

    private static ulong SeasonTicks(SimConfig c) => (ulong)c.TicksPerDay * (ulong)c.DaysPerSeason;

    /// <summary>A founded world with its clock set to the first tick of season <paramref name="season"/>.</summary>
    private static SimWorld WorldAtTheTurnOf(int season, SimConfig? config = null)
    {
        SimWorld world = SimWorld.Create(config ?? Config);
        world.Tick = SeasonTicks(world.Config) * (ulong)season;
        return world;
    }

    /// <summary>The first adult, housed (a pose: the founders arrive with a cart, not a house).</summary>
    private static (Villager Adult, Household Home) AHousedAdult(SimWorld world)
    {
        Villager adult = world.Villagers.First(v => v.CanWork);
        Household home = world.FindHousehold(adult.HouseholdId)!;
        home.HomePosition ??= adult.Position;
        foreach (Villager v in world.Villagers)
        {
            if (v.HouseholdId != home.Id && world.FindHousehold(v.HouseholdId) is Household other)
            {
                other.HomePosition ??= v.Position;
            }
        }

        return (adult, home);
    }

    private static void Read(SimWorld world) => new ContentmentSystem().Execute(world);

    private static bool Holds(Villager v, ContentmentCause cause) => (v.ContentmentCauses & (1 << (int)cause)) != 0;

    // -----------------------------------------------------------------
    //  The rows
    // -----------------------------------------------------------------

    /// <summary>⭐ The shipped file carries W2's six causes at Joe's numbers (D800), two at once the line.</summary>
    [Fact]
    public void TheShippedRowsAreJoesNumbers()
    {
        SimConfig shipped = ShippedConfig.Load();
        Assert.Equal(2, shipped.ContentmentUnsettledAt);
        Assert.Equal(
            new[]
            {
                (ContentmentCause.NoRoof, 1, 0, 0), (ContentmentCause.FarWalk, 1, 24, 0),
                (ContentmentCause.ColdWinter, 1, 6, 4), (ContentmentCause.FireOut, 1, 0, 4),
                (ContentmentCause.DryJar, 1, 3, 0), (ContentmentCause.DeathInTheFamily, 1, 0, 4),
            },
            shipped.ContentmentCauses.Select(r => (r.Cause, r.Weight, r.At, r.HeldSeasons)));

        // ⛔ And none of them is health's (D797): the kinds are a life's, and there are six.
        Assert.Equal(6, Enum.GetValues<ContentmentCause>().Length);
    }

    /// <summary>⛔ A row the build cannot read is refused at load, not discovered as a cause that never fires.</summary>
    [Fact]
    public void TheValidatorRefusesARowItCannotRead()
    {
        var bad = new (string Why, SimConfig Config)[]
        {
            ("twice", Config with { ContentmentCauses = new[] { new ContentmentCauseRow(), new ContentmentCauseRow() } }),
            ("weight", Config with { ContentmentCauses = new[] { new ContentmentCauseRow { Weight = 0 } } }),
            ("at", Config with { ContentmentCauses = new[] { new ContentmentCauseRow { At = -1 } } }),
            ("held", Config with { ContentmentCauses = new[] { new ContentmentCauseRow { HeldSeasons = -1 } } }),
            ("unknown", Config with { ContentmentCauses = new[] { new ContentmentCauseRow { Cause = (ContentmentCause)99 } } }),
            ("line", Config with { ContentmentUnsettledAt = 0 }),
        };

        foreach ((string why, SimConfig config) in bad)
        {
            SimConfigException blew = Assert.Throws<SimConfigException>(config.Validate);
            _output.WriteLine($"{why}: {blew.Message}");
        }

        // An empty list is allowed — contentment off.
        (Config with { ContentmentCauses = Array.Empty<ContentmentCauseRow>() }).Validate();
    }

    // -----------------------------------------------------------------
    //  Each cause, read at the season's turn
    // -----------------------------------------------------------------

    /// <summary>A family with no house at the season's turn holds <i>no roof</i>; housed, it does not.</summary>
    [Fact]
    public void NoRoofIsHeldWhileThereIsNoHouse()
    {
        SimWorld world = WorldAtTheTurnOf(1);
        (Villager adult, Household home) = AHousedAdult(world);
        Read(world);
        Assert.False(Holds(adult, ContentmentCause.NoRoof));

        home.HomePosition = null;
        world.Tick += SeasonTicks(world.Config);
        Read(world);
        Assert.True(Holds(adult, ContentmentCause.NoRoof));
    }

    /// <summary>The walk counts from 24 tiles (D800 call 2), not from the card's note at 8.</summary>
    [Fact]
    public void AFarWalkCountsFromTwentyFourTiles()
    {
        SimWorld world = WorldAtTheTurnOf(1);
        (Villager adult, _) = AHousedAdult(world);
        adult.CommuteTiles = 23;
        Read(world);
        Assert.False(Holds(adult, ContentmentCause.FarWalk));

        adult.CommuteTiles = 24;
        world.Tick += SeasonTicks(world.Config);
        Read(world);
        Assert.True(Holds(adult, ContentmentCause.FarWalk));
    }

    /// <summary>
    /// ⭐ Six days at the get-warm line make a cold winter, held at the winter's read and the three after it — the year
    /// after (D800 call 3) — and gone at the fourth; the winter's count is spent at its read.
    /// </summary>
    [Fact]
    public void AColdWinterIsHeldTheYearAfter()
    {
        // Season 4 is the first winter (spring 0 … winter 3); its read is at the turn of season 4.
        SimWorld world = WorldAtTheTurnOf(4);
        (Villager adult, _) = AHousedAdult(world);
        adult.ColdDaysThisWinter = 5;
        Read(world);
        Assert.False(Holds(adult, ContentmentCause.ColdWinter));
        Assert.Equal(0, adult.ColdDaysThisWinter);

        world.Tick = SeasonTicks(world.Config) * 8; // the second winter's read
        adult.ColdDaysThisWinter = 6;
        var held = new List<bool>();
        for (int s = 0; s < 5; s++)
        {
            Read(world);
            held.Add(Holds(adult, ContentmentCause.ColdWinter));
            world.Tick += SeasonTicks(world.Config);
        }

        Assert.Equal(new[] { true, true, true, true, false }, held);
    }

    /// <summary>A jar dry three days in a season counts (D800 call 4); two do not; the tally is spent at the read.</summary>
    [Fact]
    public void ADryJarCountsAtThreeDays()
    {
        SimWorld world = WorldAtTheTurnOf(1);
        (Villager adult, Household home) = AHousedAdult(world);
        home.DryDaysThisSeason = 2;
        home.Water = 50;
        Read(world);
        Assert.False(Holds(adult, ContentmentCause.DryJar));
        Assert.Equal(0, home.DryDaysThisSeason);

        // A dry day is counted on the day's turn, housed: three of them.
        home.Water = 0;
        for (int d = 0; d < 3; d++)
        {
            world.Tick += (ulong)world.Config.TicksPerDay;
            Read(world);
        }

        Assert.Equal(3, home.DryDaysThisSeason);
        world.Tick = SeasonTicks(world.Config) * 2;
        home.Water = 50;
        Read(world);
        Assert.True(Holds(adult, ContentmentCause.DryJar));
    }

    /// <summary>A winter day at home with no firewood puts the fire out, held from that winter for four seasons.</summary>
    [Fact]
    public void AFireOutIsHeldTheYearAfter()
    {
        SimWorld world = WorldAtTheTurnOf(3); // the first day of the first winter
        (Villager adult, Household home) = AHousedAdult(world);
        home.Stockpile.TryTake(Goods.Firewood, home.Stockpile.Firewood);
        world.Tick += (ulong)world.Config.TicksPerDay;
        Read(world);
        Assert.Equal(3 + 4, home.FireOutHeldUntil);

        var held = new List<bool>();
        for (int s = 4; s <= 8; s++)
        {
            world.Tick = SeasonTicks(world.Config) * (ulong)s;
            home.Stockpile.Add(Goods.Firewood, 100);
            Read(world);
            held.Add(Holds(adult, ContentmentCause.FireOut));
        }

        Assert.Equal(new[] { true, true, true, true, false }, held);
    }

    /// <summary>A death in the household is held four seasons from the day it happened, then lifts.</summary>
    [Fact]
    public void ADeathInTheFamilyIsHeldFourSeasons()
    {
        SimWorld world = WorldAtTheTurnOf(2);
        (Villager adult, _) = AHousedAdult(world);
        Villager kin = world.Villagers.First(v => v.Id != adult.Id && v.HouseholdId == adult.HouseholdId);
        kin.Alive = false;
        kin.CauseOfDeath = CauseOfDeath.OldAge;
        kin.DiedAtTick = world.Tick - 1;

        var held = new List<bool>();
        for (int s = 2; s <= 6; s++)
        {
            world.Tick = SeasonTicks(world.Config) * (ulong)s;
            Read(world);
            held.Add(Holds(adult, ContentmentCause.DeathInTheFamily));
        }

        Assert.Equal(new[] { true, true, true, true, false }, held);
    }

    // -----------------------------------------------------------------
    //  The line
    // -----------------------------------------------------------------

    /// <summary>
    /// ⭐ Two causes held at once is unsettled (D800 call 1) and counts the seasons running; one is not; the count restarts
    /// the season a cause lifts.
    /// </summary>
    [Fact]
    public void TwoCausesAtOnceIsUnsettledAndCountsTheSeasons()
    {
        SimWorld world = WorldAtTheTurnOf(1);
        (Villager adult, Household home) = AHousedAdult(world);
        adult.CommuteTiles = 30;
        Read(world);
        Assert.Equal(0, adult.SeasonsUnsettled);

        home.HomePosition = null;
        var counts = new List<int>();
        for (int s = 2; s <= 4; s++)
        {
            world.Tick = SeasonTicks(world.Config) * (ulong)s;
            Read(world);
            counts.Add(adult.SeasonsUnsettled);
        }

        home.HomePosition = adult.Position;
        world.Tick = SeasonTicks(world.Config) * 5;
        Read(world);
        counts.Add(adult.SeasonsUnsettled);

        Assert.Equal(new[] { 1, 2, 3, 0 }, counts);
        Assert.True(Holds(adult, ContentmentCause.FarWalk));
        Assert.False(Holds(adult, ContentmentCause.NoRoof));
    }

    /// <summary>A child is never read — contentment is an adult's (D798: a child never leaves alone).</summary>
    [Fact]
    public void AChildIsNeverRead()
    {
        SimWorld world = WorldAtTheTurnOf(1);
        (Villager adult, Household home) = AHousedAdult(world);
        home.HomePosition = null;
        adult.CommuteTiles = 30;
        adult.LifeStage = LifeStage.Child;
        Read(world);
        Assert.Equal(0, adult.ContentmentCauses);
        Assert.Equal(0, adult.SeasonsUnsettled);
    }

    // -----------------------------------------------------------------
    //  The day's tallies at their source
    // -----------------------------------------------------------------

    /// <summary>
    /// ⭐ A day at or past the get-warm line counts once, however many ticks of it there are; the next day counts again.
    /// </summary>
    [Fact]
    public void AColdDayCountsOnceADay()
    {
        SimWorld world = WorldAtTheTurnOf(3); // winter
        Villager adult = world.Villagers.First(v => v.CanWork);
        var hearth = new HearthSystem();
        for (int t = 0; t < world.Config.TicksPerDay; t++)
        {
            adult.Cold = world.Config.ExposureThreshold - 1;
            hearth.Execute(world);
            world.Tick++;
        }

        Assert.Equal(1, adult.ColdDaysThisWinter);

        adult.Cold = world.Config.ExposureThreshold - 1;
        hearth.Execute(world);
        Assert.Equal(2, adult.ColdDaysThisWinter);
    }

    /// <summary>
    /// The walk's tiles ride with its note: said, they are the note's number; unsaid, they are 0 — over a shipped valley's
    /// twenty years, at every season's turn.
    /// </summary>
    [Fact]
    public void TheWalksTilesRideWithItsNote()
    {
        SimConfig config = ShippedConfig.Load() with { Seed = 200 };
        SimLoop loop = SimFactory.CreatePhase0(config, new InMemoryLogSink());
        SimWorld world = loop.World;
        ColdStartTests.PlayTheOpening(world);
        int notes = 0;
        for (int t = 0; t < config.TicksPerYear * 20; t++)
        {
            loop.StepOnce();
            if (world.Tick % SeasonTicks(config) != 0UL)
            {
                continue;
            }

            foreach (Villager v in world.Villagers.Where(v => v.Alive))
            {
                if (v.CommuteNote.Length == 0)
                {
                    Assert.Equal(0, v.CommuteTiles);
                    continue;
                }

                notes++;
                Assert.StartsWith($"It is {v.CommuteTiles} tiles to ", v.CommuteNote, StringComparison.Ordinal);
            }
        }

        _output.WriteLine($"{notes} notes read");
        Assert.True(notes > 0, "no walk note was ever said — the guard is vacuous");
    }

    // -----------------------------------------------------------------
    //  Nothing acts on it yet, and it is kept
    // -----------------------------------------------------------------

    /// <summary>
    /// ⛔ STEP 2 READS AND REMEMBERS, AND CHANGES NOTHING: a shipped village with contentment and one without run to the
    /// same state hash and the same people over twenty years. Step 4 (births) is where this guard is retired.
    /// </summary>
    [Fact]
    public void NothingActsOnItYet()
    {
        SimConfig on = ShippedConfig.Load() with { Seed = 201 };
        SimConfig off = on with { ContentmentCauses = Array.Empty<ContentmentCauseRow>() };
        SimLoop a = SimFactory.CreatePhase0(on, new InMemoryLogSink());
        SimLoop b = SimFactory.CreatePhase0(off, new InMemoryLogSink());
        ColdStartTests.PlayTheOpening(a.World);
        ColdStartTests.PlayTheOpening(b.World);
        int everUnsettled = 0;
        bool sawACause = false;
        for (int t = 0; t < on.TicksPerYear * 20; t++)
        {
            a.StepOnce();
            b.StepOnce();
            if (a.World.Tick % SeasonTicks(on) == 0UL)
            {
                everUnsettled += a.World.Villagers.Count(v => v.SeasonsUnsettled > 0);
                sawACause |= a.World.Villagers.Any(v => v.ContentmentCauses != 0);
            }
        }

        _output.WriteLine($"unsettled adult-seasons {everUnsettled}; alive {a.World.Population}");
        Assert.Equal(StateHash.Compute(b.World), StateHash.Compute(a.World));
        Assert.Equal(b.World.Villagers.Count, a.World.Villagers.Count);
        Assert.Equal(b.World.Population, a.World.Population);
        Assert.True(sawACause, "the read never saw a cause — the guard is vacuous");
    }

    /// <summary>Every tally survives a save and a load, so a reloaded village is as unsettled as it was.</summary>
    [Fact]
    public void TheTalliesAreSaved()
    {
        SimLoop loop = SimFactory.CreatePhase0(Config, new InMemoryLogSink());
        SimWorld world = loop.World;
        (Villager adult, Household home) = AHousedAdult(world);
        adult.ColdDaysThisWinter = 3;
        adult.LastColdDay = 17;
        adult.ColdWinterHeldUntil = 9;
        adult.CommuteTiles = 25;
        adult.ContentmentCauses = 0b101;
        adult.SeasonsUnsettled = 2;
        home.FireOutHeldUntil = 7;
        home.DryDaysThisSeason = 4;

        JsonObject document = SaveGame.Capture(loop, "test", "now", "test");
        SimWorld back = SaveGame.Load(JsonNode.Parse(SaveFile.TextOf(document))!.AsObject(), world.Config).World;
        Villager a = back.Villagers.Single(v => v.Id == adult.Id);
        Household h = back.FindHousehold(home.Id)!;
        Assert.Equal((3, 17, 9, 25, 0b101, 2), (a.ColdDaysThisWinter, a.LastColdDay, a.ColdWinterHeldUntil, a.CommuteTiles, a.ContentmentCauses, a.SeasonsUnsettled));
        Assert.Equal((7, 4), (h.FireOutHeldUntil, h.DryDaysThisSeason));
    }

    // -----------------------------------------------------------------
    //  The card's sentence (step 3, `morale.md §6`): the read in words, never the number
    // -----------------------------------------------------------------

    /// <summary>An adult the read found nothing troubling has no sentence; the card says they are content.</summary>
    [Fact]
    public void AContentAdultHasNoNote()
    {
        SimWorld world = WorldAtTheTurnOf(1);
        (Villager adult, _) = AHousedAdult(world);
        Read(world);
        Assert.False(adult.IsUnsettled);
        Assert.Null(world.ContentmentNoteFor(adult));
    }

    /// <summary>⭐ One cause is short of the line: content, and the sentence says what is wearing on them anyway.</summary>
    [Fact]
    public void OneCauseIsContentThough()
    {
        SimWorld world = WorldAtTheTurnOf(1);
        (Villager adult, _) = AHousedAdult(world);
        adult.CommuteTiles = 30;
        Read(world);
        Assert.False(adult.IsUnsettled);
        Assert.Equal($"{adult.Name} is content, though the walk to work is a long one.", world.ContentmentNoteFor(adult));
    }

    /// <summary>⭐ Two causes is unsettled, and both are named — in the data file's order.</summary>
    [Fact]
    public void TwoCausesAreUnsettledAndBothNamed()
    {
        SimWorld world = WorldAtTheTurnOf(1);
        (Villager adult, Household home) = AHousedAdult(world);
        adult.CommuteTiles = 30;
        home.HomePosition = null;
        Read(world);
        Assert.True(adult.IsUnsettled);
        Assert.Equal(
            $"{adult.Name} is unsettled — they have no roof of their own and the walk to work is a long one.",
            world.ContentmentNoteFor(adult));
    }

    /// <summary>A child is never read, so never has a sentence, whatever the bits say.</summary>
    [Fact]
    public void AChildHasNoNote()
    {
        SimWorld world = WorldAtTheTurnOf(1);
        (Villager adult, _) = AHousedAdult(world);
        adult.ContentmentCauses = 1 << (int)ContentmentCause.FarWalk;
        adult.LifeStage = LifeStage.Child;
        Assert.Null(world.ContentmentNoteFor(adult));
    }

    /// <summary>
    /// ⭐ Grief names who was lost (§6: a person, not a spreadsheet) — the read's own dead, still found partway through the
    /// season, and not a stranger who died since.
    /// </summary>
    [Fact]
    public void TheGriefNamesWhoWasLost()
    {
        SimWorld world = WorldAtTheTurnOf(2);
        (Villager adult, _) = AHousedAdult(world);
        // Two losses in the family, the one first in the list dying first: the sentence names the latest, not the first found.
        Villager one = world.Villagers.First(v => v.Id != adult.Id && v.HouseholdId == adult.HouseholdId);
        Villager other = world.Villagers.First(v => v.HouseholdId != adult.HouseholdId && v.PartnerId != adult.Id);
        other.HouseholdId = adult.HouseholdId;
        bool oneFirst = world.Villagers.IndexOf(one) < world.Villagers.IndexOf(other);
        Villager kin = oneFirst ? other : one;
        Villager earlier = oneFirst ? one : other;
        foreach (Villager dead in new[] { kin, earlier })
        {
            dead.Alive = false;
            dead.CauseOfDeath = CauseOfDeath.OldAge;
        }

        kin.DiedAtTick = world.Tick - 1;
        earlier.DiedAtTick = world.Tick - 2;
        world.Tick = SeasonTicks(world.Config) * 3;
        Read(world);

        Villager stranger = world.Villagers.First(v => v.Alive && v.HouseholdId != adult.HouseholdId && v.PartnerId != adult.Id);
        stranger.Alive = false;
        stranger.CauseOfDeath = CauseOfDeath.OldAge;
        stranger.DiedAtTick = world.Tick + 5;
        world.Tick += 10;

        Assert.Equal($"{adult.Name} is content, though they are grieving {kin.Name}.", world.ContentmentNoteFor(adult));
    }
}

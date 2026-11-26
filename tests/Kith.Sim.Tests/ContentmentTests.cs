using System.Text.Json.Nodes;
using Kith.Sim.Config;
using Kith.Sim.Core;
using Kith.Sim.Determinism;
using Kith.Sim.Logging;
using Kith.Sim.Persistence;
using Kith.Sim.Systems;
using Kith.Sim.World;
using Xunit;
using Xunit.Abstractions;

namespace Kith.Sim.Tests;

/// <summary>
/// Contentment, steps 2–5a (W2, `specs/morale.md §13`, D798–D806): the causes as rows, the day's tallies, the read at
/// each season's turn, the card's sentence, the first effect — an unsettled couple's longer wait between children — and
/// the second: warned after a year unsettled, gone from the valley after two.
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

                // W4 (D910): the village's dead have a place — Joe's D797 rule, its two cases.
                (ContentmentCause.DeadInTheOpen, 1, 0, 0), (ContentmentCause.CemeteryFull, 1, 0, 0),
            },
            shipped.ContentmentCauses.Select(r => (r.Cause, r.Weight, r.At, r.HeldSeasons)));

        // ⛔ And none of them is health's (D797): the kinds are a life's, and there are eight since W4's two (D910).
        Assert.Equal(8, Enum.GetValues<ContentmentCause>().Length);
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

    /// <summary>
    /// ⭐ No fire to keep, no fire to go out (D808): with fuel switched off (<c>firewood_per_winter_day</c> 0) an empty woodpile
    /// in winter is not a cause — §4, nothing the player cannot change. Phase 0's village of one held it every winter.
    /// </summary>
    [Fact]
    public void AFireOutNeedsAFireToKeep()
    {
        SimWorld world = WorldAtTheTurnOf(3, Config with { FirewoodPerWinterDay = 0 });
        (Villager adult, Household home) = AHousedAdult(world);
        home.Stockpile.TryTake(Goods.Firewood, home.Stockpile.Firewood);
        world.Tick += (ulong)world.Config.TicksPerDay;
        Read(world);
        Assert.Equal(0, home.FireOutHeldUntil);
        world.Tick = SeasonTicks(world.Config) * 4;
        Read(world);
        Assert.False(Holds(adult, ContentmentCause.FireOut));
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
    /// ⭐ STEP 4, THE FIRST EFFECT (D804): a village where most adults are unsettled has fewer children than the same
    /// village with the longer wait turned off (<c>contentment_birth_interval_percent</c> 100) — the read the same in both,
    /// only the births gate differing. <i>(Replaces <c>NothingActsOnItYet</c>, steps 2–3's guard that it changed nothing.)</i>
    /// </summary>
    [Fact]
    public void AnUnsettledVillageHasFewerChildren()
    {
        // Every housed adult unsettled from the first season: the jar counted from no dry days at all, on a line of one —
        // so the effect has every couple to act on, and the village grows enough for second children (the fixture's three
        // households have six or more in forty years, `BirthdayTests`). ⚠️ Leaving off (step 5): unsettled forty years, both
        // villages would empty after two and the wait would have nobody to act on — this guard is the births gate's alone.
        SimConfig grows = VillageFixtures.Village with { StartingHouseholds = 3, ContentmentUnsettledAt = 1, ContentmentLeavingSeasons = 0 };
        SimConfig on = grows with
        {
            ContentmentCauses = grows.ContentmentCauses
                .Select(r => r.Cause == ContentmentCause.DryJar ? r with { At = 0 } : r).ToArray(),
        };
        SimConfig off = on with { ContentmentBirthIntervalPercent = 100 };
        SimLoop a = SimFactory.CreatePhase0(on, new InMemoryLogSink());
        SimLoop b = SimFactory.CreatePhase0(off, new InMemoryLogSink());
        int everUnsettled = 0;
        for (int t = 0; t < on.TicksPerYear * 40; t++)
        {
            a.StepOnce();
            b.StepOnce();
            if (a.World.Tick % SeasonTicks(on) == 0UL)
            {
                everUnsettled += a.World.Villagers.Count(v => v.IsUnsettled);
            }
        }

        static int Born(SimWorld w) => w.Villagers.Count(v => v.BirthTick > 0 && v.ArrivedAtTick == 0UL);
        _output.WriteLine($"unsettled adult-seasons {everUnsettled}; born {Born(a.World)} with the wait, {Born(b.World)} without");
        Assert.True(everUnsettled > 0, "nobody was ever unsettled — the guard is vacuous");
        Assert.True(Born(a.World) < Born(b.World), $"born {Born(a.World)} with the longer wait, {Born(b.World)} without");
    }

    /// <summary>The household's fertile couple, both alive and at home.</summary>
    private static (Household Home, Villager One, Villager Two) ACouple(SimWorld world)
    {
        foreach (Household home in world.Households)
        {
            foreach (int id in home.MemberIds)
            {
                if (world.FindVillager(id) is { Alive: true, IsPaired: true } one
                    && world.FindVillager(one.PartnerId) is { Alive: true } two && two.HouseholdId == home.Id)
                {
                    return (home, one, two);
                }
            }
        }

        throw new InvalidOperationException("no couple in the founding");
    }

    /// <summary>
    /// ⭐ An unsettled couple waits twice the interval between children (D798 call 4) — either of them unsettled is enough —
    /// and the household card says so only while it is that wait holding the next child off. ⚠️ A couple's FIRST child
    /// does not wait on it: the interval only ever counted from a child (the gate's other terms untouched, call 4).
    /// </summary>
    [Fact]
    public void AnUnsettledCoupleWaitsTwiceTheInterval()
    {
        SimConfig config = Config with { BirthFoodPercent = 0 };
        SimWorld world = WorldAtTheTurnOf(4 * 12, config);
        AHousedAdult(world);
        (Household home, Villager one, Villager two) = ACouple(world);
        int year = world.Clock.Year;
        int n = config.BirthIntervalYears;
        bool Ready(int yearsSince)
        {
            home.LastBirthYear = yearsSince < 0 ? 0 : year - yearsSince;
            return HouseholdSystem.IsReadyForAChild(world, home, config, year);
        }

        Assert.True(Ready(n), "a settled couple is ready the ordinary interval after a child");
        Assert.Null(world.ChildNoteFor(home));

        two.SeasonsUnsettled = 1;
        Assert.Equal(2 * n, HouseholdSystem.BirthIntervalFor(world, home, config));
        Assert.False(Ready(n));
        Assert.Equal($"Not ready for another child — {two.Name} is unsettled.", world.ChildNoteFor(home));
        Assert.False(Ready(2 * n - 1));
        Assert.True(Ready(2 * n));
        Assert.Null(world.ChildNoteFor(home));
        Assert.False(Ready(n - 1));
        Assert.Null(world.ChildNoteFor(home)); // the ordinary interval is what holds it off — not said as contentment
        Assert.True(Ready(-1), "a first child does not wait on contentment");

        two.SeasonsUnsettled = 0;
        one.SeasonsUnsettled = 3;
        Assert.False(Ready(n));
        Assert.Equal($"Not ready for another child — {one.Name} is unsettled.", world.ChildNoteFor(home));

        // Nor a couple with no roof: the roof holds their child off (D71), not contentment.
        Point? roof = home.HomePosition;
        home.HomePosition = null;
        Assert.Null(world.ChildNoteFor(home));
        home.HomePosition = roof;
        Assert.NotNull(world.ChildNoteFor(home));

        // A couple with no child yet is never "not ready for another" — in the years when the founding itself is between
        // one interval and two old, too.
        home.LastBirthYear = 0;
        world.Tick = SeasonTicks(config) * 4 * (ulong)(n + 1);
        Assert.Null(world.ChildNoteFor(home));
    }

    /// <summary>
    /// ⭐ Every contentment field is in the state hash since step 4 (D804), each on its own — the goldens alone could not
    /// say so: a village nobody is unsettled in at its last tick hashes the same with <c>SeasonsUnsettled</c> left out.
    /// </summary>
    [Theory]
    [InlineData("cold_days")]
    [InlineData("last_cold_day")]
    [InlineData("cold_winter_held")]
    [InlineData("commute_tiles")]
    [InlineData("causes")]
    [InlineData("seasons_unsettled")]
    [InlineData("fire_out_held")]
    [InlineData("dry_days")]
    public void EveryContentmentFieldIsHashed(string field)
    {
        SimWorld world = SimWorld.Create(Config);
        (Villager adult, Household home) = AHousedAdult(world);
        ulong before = StateHash.Compute(world);
        switch (field)
        {
            case "cold_days": adult.ColdDaysThisWinter = 1; break;
            case "last_cold_day": adult.LastColdDay = 1; break;
            case "cold_winter_held": adult.ColdWinterHeldUntil = 1; break;
            case "commute_tiles": adult.CommuteTiles = 1; break;
            case "causes": adult.ContentmentCauses = 1; break;
            case "seasons_unsettled": adult.SeasonsUnsettled = 1; break;
            case "fire_out_held": home.FireOutHeldUntil = 1; break;
            case "dry_days": home.DryDaysThisSeason = 1; break;
            default: throw new ArgumentOutOfRangeException(nameof(field), field, "no such field");
        }

        Assert.NotEqual(before, StateHash.Compute(world));
    }

    /// <summary>The validator refuses a wait shorter than a settled couple's.</summary>
    [Fact]
    public void TheLongerWaitIsNeverShorter()
    {
        Assert.Throws<SimConfigException>(() => (Config with { ContentmentBirthIntervalPercent = 99 }).Validate());
        (Config with { ContentmentBirthIntervalPercent = 100 }).Validate();
        Assert.Equal(200, ShippedConfig.Load().ContentmentBirthIntervalPercent);
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

    // -----------------------------------------------------------------
    //  Leaving (step 5, `morale.md §13.2`): warned after a year, gone after two
    // -----------------------------------------------------------------

    /// <summary>A founded world logging to <paramref name="log"/>, its clock at the turn of <paramref name="season"/>.</summary>
    private static SimWorld LoggedWorldAt(int season, InMemoryLogSink log, SimConfig? config = null)
    {
        SimWorld world = SimWorld.Create(config ?? Config, log);
        world.Tick = SeasonTicks(world.Config) * (ulong)season;
        return world;
    }

    /// <summary>Two causes held from here on (no roof, and the walk) — unsettled every season it is read.</summary>
    private static void Unsettle(SimWorld world, Villager adult, int seasonsSoFar)
    {
        if (world.FindHousehold(adult.HouseholdId) is Household home)
        {
            home.HomePosition = null;
        }

        adult.CommuteTiles = 30;
        adult.SeasonsUnsettled = seasonsSoFar;
    }

    private static void NextSeason(SimWorld world)
    {
        world.Tick += SeasonTicks(world.Config);
        Read(world);
    }

    private static List<string> Lines(InMemoryLogSink log, string containing) =>
        log.Entries.Where(e => e.Message.Contains(containing, StringComparison.Ordinal)).Select(e => e.Message).ToList();

    private static Villager AddTo(SimWorld world, Household home, string name, LifeStage stage, ulong arrivedAt = 0UL)
    {
        var person = new Villager
        {
            Id = world.Villagers.Max(v => v.Id) + 1,
            Name = name,
            Surname = home.Surname,
            LifespanYears = 70,
            AgeYears = stage == LifeStage.Child ? 6 : 20,
            LifeStage = stage,
            HouseholdId = home.Id,
            Carried = new Stockpile(Stockpile.Kinds),
            ArrivedAtTick = arrivedAt,
        };
        world.Villagers.Add(person);
        home.AddMember(person.Id);
        return person;
    }

    /// <summary>A couple's household, its partner dead long enough ago that the grief has passed: a lone adult.</summary>
    private static (Household Home, Villager Lone) ALoneAdult(SimWorld world)
    {
        (Household home, Villager one, Villager two) = ACouple(world);
        two.Alive = false;
        two.State = VillagerState.Dead;
        two.CauseOfDeath = CauseOfDeath.OldAge;
        two.DiedAtTick = 1;
        return (home, one);
    }

    private static bool HasLeft(Villager v) =>
        !v.Alive && v.State == VillagerState.Left && v.LeftAtTick is not null && v.DiedAtTick is null && v.CauseOfDeath == CauseOfDeath.None;

    /// <summary>⭐ Joe's numbers (D798 call 3): thinking of leaving after a year, gone after two; the validator holds the order.</summary>
    [Fact]
    public void TheLeavingKeysAreJoesNumbersAndValidated()
    {
        SimConfig shipped = ShippedConfig.Load();
        Assert.Equal((4, 8), (shipped.ContentmentThinkingOfLeavingSeasons, shipped.ContentmentLeavingSeasons));
        (Config with { ContentmentLeavingSeasons = 0 }).Validate();
        Assert.Throws<SimConfigException>(() => (Config with { ContentmentThinkingOfLeavingSeasons = 0 }).Validate());
        Assert.Throws<SimConfigException>(() => (Config with { ContentmentThinkingOfLeavingSeasons = 8 }).Validate());
        Assert.Throws<SimConfigException>(() => (Config with { ContentmentLeavingSeasons = -1 }).Validate());
    }

    /// <summary>
    /// ⭐ A year unsettled and the log says once that they are thinking of leaving (§5: never without warning); the card says
    /// it from then on; a settled season ends the spell, and the next spell warns again.
    /// </summary>
    [Fact]
    public void AYearUnsettledIsThinkingOfLeavingOnceASpell()
    {
        var log = new InMemoryLogSink();
        SimWorld world = LoggedWorldAt(9, log);
        (Villager adult, _) = AHousedAdult(world);
        Unsettle(world, adult, 2);

        NextSeason(world);
        Assert.Equal(3, adult.SeasonsUnsettled);
        Assert.Empty(Lines(log, "thinking of leaving"));
        Assert.StartsWith($"{adult.Name} is unsettled — ", world.ContentmentNoteFor(adult));

        NextSeason(world);
        NextSeason(world);
        List<string> warned = Lines(log, "thinking of leaving");
        Assert.Single(warned);
        Assert.StartsWith(
            $"{adult.FullName} is thinking of leaving the valley — they have no roof of their own and the walk to work is a long one. ",
            warned[0]);
        Assert.Equal(LogCategory.Warning, log.Entries.Single(e => e.Message == warned[0]).Category);
        Assert.Equal(
            $"{adult.Name} is thinking of leaving — they have no roof of their own and the walk to work is a long one.",
            world.ContentmentNoteFor(adult));

        world.FindHousehold(adult.HouseholdId)!.HomePosition = adult.Position;
        NextSeason(world);
        Assert.Equal(0, adult.SeasonsUnsettled);
        Unsettle(world, adult, 3);
        NextSeason(world);
        Assert.Equal(2, Lines(log, "thinking of leaving").Count);
    }

    /// <summary>
    /// ⭐ Two years unsettled and a lone adult goes, at the season's turn — alive no longer, and not dead: a fate of their own,
    /// with one line naming them and why (§11 item 5).
    /// </summary>
    [Fact]
    public void ALoneAdultLeavesAfterTwoYears()
    {
        var log = new InMemoryLogSink();
        SimWorld world = LoggedWorldAt(9, log);
        AHousedAdult(world);
        (_, Villager lone) = ALoneAdult(world);
        int population = world.Population;
        Unsettle(world, lone, 6);

        NextSeason(world);
        Assert.True(lone.Alive, "seven seasons is not yet two years");
        NextSeason(world);

        Assert.True(HasLeft(lone), "two years unsettled: gone, and not dead");
        Assert.Equal(world.Tick, lone.LeftAtTick);
        Assert.Equal(population - 1, world.Population);
        Assert.Equal(
            new[] { $"{lone.FullName} has left the valley — they have no roof of their own and the walk to work is a long one. {world.Clock.SeasonAndYear()}." },
            Lines(log, "left the valley"));
        Assert.Equal(LogCategory.Life, log.Entries.Single(e => e.Message.Contains("left the valley")).Category);
        Assert.NotEqual(0, lone.ContentmentCauses); // the reason they went stays on them

        NextSeason(world);
        Assert.Single(Lines(log, "left the valley"));
    }

    /// <summary>
    /// ⭐ A couple goes only when both are ready (call 2: <i>"she stayed for him"</i>), and takes the household's children;
    /// the one held back says so on the card.
    /// </summary>
    [Fact]
    public void ACoupleLeavesOnlyWhenBothAreReadyAndTakesTheChildren()
    {
        var log = new InMemoryLogSink();
        SimWorld world = LoggedWorldAt(9, log);
        AHousedAdult(world);
        (Household home, Villager one, Villager two) = ACouple(world);
        two.Surname = one.Surname;
        Villager child = AddTo(world, home, "Pip", LifeStage.Child);
        Unsettle(world, one, 7);
        Unsettle(world, two, 3);

        NextSeason(world);
        Assert.True(one.Alive && two.Alive && child.Alive, "one of the two is not ready: nobody goes");
        Assert.Equal(
            $"{one.Name} would leave, but {two.Name} is staying — they have no roof of their own and the walk to work is a long one.",
            world.ContentmentNoteFor(one));

        for (int s = 0; s < 3; s++)
        {
            NextSeason(world);
        }

        Assert.True(one.Alive && two.Alive, "still one season short");
        NextSeason(world);
        Assert.True(HasLeft(one) && HasLeft(two) && HasLeft(child), "both ready: the couple goes, and the child with them");
        Assert.Equal(new ulong?[] { world.Tick, world.Tick, world.Tick }, new[] { one.LeftAtTick, two.LeftAtTick, child.LeftAtTick });
        Assert.Equal(0, world.LivingMembersOf(home));
        Assert.Equal(
            new[] { $"{one.Name} and {two.Name} {one.Surname} have left the valley, taking the child — they have no roof of their own and the walk to work is a long one. {world.Clock.SeasonAndYear()}." },
            Lines(log, "left the valley"));
    }

    /// <summary>
    /// ⭐ A lone leaver's children stay while an adult stays (a grown child goes and the little ones keep their parents), and go
    /// with the last adult to leave the house (a widow takes them). A child never leaves alone.
    /// </summary>
    [Fact]
    public void TheChildrenGoWithTheLastAdultToLeave()
    {
        var log = new InMemoryLogSink();
        SimWorld world = LoggedWorldAt(9, log);
        AHousedAdult(world);
        (Household home, Villager one, Villager two) = ACouple(world);
        Villager grown = AddTo(world, home, "Wren", LifeStage.Adult);
        Villager little = AddTo(world, home, "Pip", LifeStage.Child);
        Unsettle(world, grown, 7);
        home.HomePosition = one.Position; // the grown child's walk alone is not enough: give the house its roof back…
        grown.ColdWinterHeldUntil = world.SeasonNumber + 4; // …and a cold winter for the second cause
        NextSeason(world);
        Assert.True(HasLeft(grown), "the grown child goes alone");
        Assert.True(one.Alive && two.Alive && little.Alive, "the little one stays with the parents who stay");
        Assert.Equal(
            new[] { $"{grown.FullName} has left the valley — the walk to work is a long one and last winter was a cold one. {world.Clock.SeasonAndYear()}." },
            Lines(log, "left the valley"));

        // The widow, the last adult in the house, takes the child.
        two.Alive = false;
        two.State = VillagerState.Dead;
        two.CauseOfDeath = CauseOfDeath.OldAge;
        two.DiedAtTick = 1;
        Unsettle(world, one, 7);
        NextSeason(world);
        Assert.True(HasLeft(one) && HasLeft(little), "the last adult to leave takes the children");
        Assert.Equal($"{one.FullName} has left the valley, taking the child — ", Lines(log, "left the valley")[1][..$"{one.FullName} has left the valley, taking the child — ".Length]);
    }

    /// <summary>⭐ A child is never read, so a child never leaves on their own count — whatever the count says.</summary>
    [Fact]
    public void AChildNeverLeavesAlone()
    {
        SimWorld world = WorldAtTheTurnOf(9);
        AHousedAdult(world);
        (Household home, _, _) = ACouple(world);
        Villager little = AddTo(world, home, "Pip", LifeStage.Child);
        little.SeasonsUnsettled = 20;
        NextSeason(world);
        Assert.True(little.Alive);
    }

    /// <summary>
    /// ⭐ Leaving lets go as a death does — the household empties, so the house is free for the next couple — and grief reads
    /// only the dead: a household whose grown child left is not grieving.
    /// </summary>
    [Fact]
    public void LeavingIsNotGrief()
    {
        SimWorld world = WorldAtTheTurnOf(9);
        AHousedAdult(world);
        (Household home, Villager one, Villager two) = ACouple(world);
        Villager grown = AddTo(world, home, "Wren", LifeStage.Adult);
        Unsettle(world, grown, 7);
        home.HomePosition = one.Position;
        grown.ColdWinterHeldUntil = world.SeasonNumber + 4;
        NextSeason(world);
        Assert.True(HasLeft(grown));

        NextSeason(world);
        Assert.False(Holds(one, ContentmentCause.DeathInTheFamily), "a departure is not a death in the family");

        // And with a death too, the one grieved is the one who died — not the one who left after them.
        two.Alive = false;
        two.State = VillagerState.Dead;
        two.CauseOfDeath = CauseOfDeath.OldAge;
        two.DiedAtTick = grown.LeftAtTick - 5;
        NextSeason(world);
        Assert.True(Holds(one, ContentmentCause.DeathInTheFamily));
        Assert.Contains($"they are grieving {two.Name}", world.ContentmentNoteFor(one), StringComparison.Ordinal); // a winter: the fire is out too
    }

    /// <summary>
    /// ⭐ The readers that meant <i>died</i> read <i>gone</i> (§13.2 item 5): the valley empties at the departure, its peak
    /// counts the leaver out, and a departure that empties the valley says so as a last death does.
    /// </summary>
    [Fact]
    public void AValleyEmptiedByLeavingIsEmptyFromThatTick()
    {
        var log = new InMemoryLogSink();
        SimWorld world = LoggedWorldAt(9, log);
        AHousedAdult(world);
        int peak = world.MostEverAlive;
        foreach (Villager v in world.Villagers.Where(v => v.CanWork))
        {
            Unsettle(world, v, 7);
        }

        Assert.Null(world.EmptiedAtTick);
        NextSeason(world);
        Assert.Equal(0, world.Population);
        Assert.Equal(world.Tick, world.EmptiedAtTick);
        Assert.Contains(world.Moments, m => m.Title == $"{world.Name} is empty" && m.Body.Contains("left the valley", StringComparison.Ordinal));
        Assert.False(world.Moments.Single(m => m.Title == $"{world.Name} is empty").WaitsToBeDismissed);
        Assert.Equal(peak, world.MostEverAlive);

        AddTo(world, world.Households[0], "Ash", LifeStage.Adult, world.Tick + 1);
        Assert.Equal(peak, world.MostEverAlive); // one alive after the leavers went, not the peak plus one
        Assert.Equal(world.Tick, world.Villagers[0].GoneAtTick);
    }

    /// <summary>⭐ The founders' tribute names a founder who left as having left — never <i>"is dead at"</i>.</summary>
    [Fact]
    public void TheTributeSaysAFounderLeft()
    {
        SimWorld world = WorldAtTheTurnOf(9);
        AHousedAdult(world);
        (Household home, _, _) = ACouple(world);
        AddTo(world, home, "Wren", LifeStage.Adult); // born here: not a founder
        // The leaver LAST in the list, so list order alone cannot pick them as the last to go (D802's trap, again).
        Villager last = world.Villagers.Last(v => v.Founder);
        foreach (Villager founder in world.Villagers.Where(v => v.Founder))
        {
            founder.Alive = false;
            if (founder == last)
            {
                founder.State = VillagerState.Left;
                founder.LeftAtTick = world.Tick;
            }
            else
            {
                founder.State = VillagerState.Dead;
                founder.CauseOfDeath = CauseOfDeath.OldAge;
                founder.DiedAtTick = 1;
            }
        }

        new CivicSystem().Execute(world);
        Moment tribute = world.Moments.Single(m => m.Title == "The last of the founders is gone");
        Assert.Contains($"{last.Name} was the last of the four", tribute.Body, StringComparison.Ordinal);
        Assert.Contains("and has left the valley.", tribute.Body, StringComparison.Ordinal);
        Assert.DoesNotContain("is dead at", tribute.Body, StringComparison.Ordinal);
    }

    /// <summary>With leaving off (0), nobody leaves and nobody is warned, however long they are unsettled.</summary>
    [Fact]
    public void LeavingOffNobodyLeaves()
    {
        var log = new InMemoryLogSink();
        SimWorld world = LoggedWorldAt(9, log, Config with { ContentmentLeavingSeasons = 0 });
        AHousedAdult(world);
        (_, Villager lone) = ALoneAdult(world);
        Unsettle(world, lone, 20);
        NextSeason(world);
        Assert.True(lone.Alive);
        Assert.Empty(Lines(log, "leaving"));
        Assert.Equal($"{lone.Name} is unsettled — they have no roof of their own and the walk to work is a long one.", world.ContentmentNoteFor(lone));
    }

    // -----------------------------------------------------------------
    //  Step 6, the guards (`morale.md §11` item 4, both ways) and the gate's golden
    // -----------------------------------------------------------------

    /// <summary>Seeds 1–12 of <paramref name="config"/> for <paramref name="years"/>, in parallel: each village and its log.</summary>
    private static List<(int Seed, SimWorld World, InMemoryLogSink Log)> Valleys(SimConfig config, int years)
    {
        var runs = new System.Collections.Concurrent.ConcurrentBag<(int, SimWorld, InMemoryLogSink)>();
        Parallel.ForEach(Enumerable.Range(1, 12), seed =>
        {
            var log = new InMemoryLogSink(LogLevel.Info);
            SimLoop loop = SimFactory.CreatePhase0(config, log, (ulong)seed);
            for (int y = 0; y < years; y++)
            {
                loop.Step(config.TicksPerYear);
            }

            runs.Add((seed, loop.World, log));
        });
        return runs.OrderBy(r => r.Item1).ToList();
    }

    private int CountTheCauses(Villager v) => Enumerable.Range(0, 32).Count(b => (v.ContentmentCauses & (1 << b)) != 0);

    /// <summary>
    /// ⭐ ANTI-VACUITY (§11 item 4): a village that ignores contentment loses people — the fixture as the unattended harness
    /// plays it, which never builds a well and lets winters go cold. Measured (D808, `ZzLeaving`): 12 people in 3 of 12
    /// valleys by Year 50, the first in Years 11 and 13, every adult with a cold winter and a dry jar held two years. ⛔ Not
    /// the no-house pose: with no roof possible all twelve froze to death and nobody left — a death test, not this one.
    /// </summary>
    [Fact]
    public void AVillageThatIgnoresContentmentLosesPeople()
    {
        var runs = Valleys(Config, 15);
        int adults = 0;
        foreach ((int seed, SimWorld world, InMemoryLogSink log) in runs)
        {
            foreach (Villager gone in world.Villagers.Where(v => v.LeftAtTick is not null && v.ContentmentCauses != 0))
            {
                adults++;
                Assert.True(CountTheCauses(gone) >= Config.ContentmentUnsettledAt, $"seed {seed}: {gone.Name} left holding one cause");
                Assert.True(gone.SeasonsUnsettled >= Config.ContentmentLeavingSeasons, $"seed {seed}: {gone.Name} left early");
            }

            int parties = world.Villagers.Where(v => v.LeftAtTick is not null).Select(v => v.LeftAtTick).Distinct().Count();
            Assert.True(parties <= log.Entries.Count(e => e.Message.Contains(" left the valley", StringComparison.Ordinal)),
                $"seed {seed}: a departure with no line naming it");
        }

        _output.WriteLine($"adults gone in 12 neglected valleys by Year 15: {adults}");
        Assert.True(adults > 0, "nobody left a village that ignores contentment — the system is decorative");
    }

    /// <summary>
    /// ⭐ AND ITS MIRROR (§11 item 4): a kept village loses nobody it keeps — the same valleys with the water answered (the
    /// harness never builds a well) for fifty years: nobody with a roof leaves. Measured (D808): one departure in twelve
    /// valleys, Year 48, a grown child still waiting for a house and grieving — the unhoused, whom the harness never housed.
    /// </summary>
    [Fact]
    public void AKeptVillageLosesNobodyItKeeps()
    {
        // ⚠️ And the dead's place answered too (W4, D910): the harness never builds a cemetery, as it never builds a well, so the
        // two rows that ask for one are taken out — a kept village is one whose every cause the harness could not answer is answered.
        var runs = Valleys(Config with
        {
            WaterPerPersonPerDay = 0,
            ContentmentCauses = Config.ContentmentCauses
                .Where(r => r.Cause is not (ContentmentCause.DeadInTheOpen or ContentmentCause.CemeteryFull)).ToArray(),
        }, 50);
        int alive = 0;
        foreach ((int seed, SimWorld world, _) in runs)
        {
            alive += world.Population;
            foreach (Villager gone in world.Villagers.Where(v => v.LeftAtTick is not null && v.ContentmentCauses != 0))
            {
                Assert.True(Holds(gone, ContentmentCause.NoRoof), $"seed {seed}: {gone.Name} left a kept village with a roof over them");
            }
        }

        Assert.True(alive > 0, "every kept valley died — the guard is vacuous");
    }

    /// <summary>
    /// ⭐ PHASE 6 GATE ITEM 2 — A GOLDEN THAT REACHES A DEPARTURE (D808): the fixture's seed 9, fifteen years — its first family
    /// left in Year 13 (two adults and two children). Every other golden ends with nobody gone, so this is the one that sees
    /// leaving's state in the hash.
    /// </summary>
    [Fact]
    public void TheGoldenThatSeesADeparture()
    {
        SimLoop loop = SimFactory.CreatePhase0(Config, new InMemoryLogSink(), 9UL);
        for (int y = 0; y < 15; y++)
        {
            loop.Step(Config.TicksPerYear);
        }

        Assert.True(loop.World.Villagers.Count(v => v.LeftAtTick is not null) >= 2, "the golden's village has nobody gone");
        _output.WriteLine($"hash {StateHash.Compute(loop.World)}");
        Assert.Equal(LeavingGolden, StateHash.Compute(loop.World));
    }

    /// <summary>Taken 2026-11-20 (D808), on its first run: four gone in Year 13, the village still standing at 15.</summary>
    // RE-TAKEN (D825) — barley is learned when the foragers have gathered `barley_unlock_forage` (5,000), a tag byte in the hash and Lore introduced (W7, `barley.md §11`). ⭐ PROVEN BY ABLATION: with the gate set out of reach (`barley_unlock_forage` 2147483647) the old value passes. Was 593170502162136593.
    private const ulong LeavingGolden = 16825038126313242079UL;

    /// <summary>⭐ The departure is saved and hashed sparsely (§13.2 item 8); a save without it reads as never gone.</summary>
    [Fact]
    public void LeavingIsSavedAndHashed()
    {
        SimLoop loop = SimFactory.CreatePhase0(Config, new InMemoryLogSink());
        SimWorld world = loop.World;
        Villager adult = world.Villagers.First(v => v.CanWork);
        ulong before = StateHash.Compute(world);
        adult.Alive = false;
        adult.State = VillagerState.Left;
        ulong stateOnly = StateHash.Compute(world);
        adult.LeftAtTick = 78;
        ulong at78 = StateHash.Compute(world);
        adult.LeftAtTick = 77;
        Assert.NotEqual(before, stateOnly);
        Assert.NotEqual(stateOnly, StateHash.Compute(world));
        Assert.NotEqual(at78, StateHash.Compute(world)); // the tick itself, not only that there is one

        JsonObject document = SaveGame.Capture(loop, "test", "now", "test");
        SimWorld back = SaveGame.Load(JsonNode.Parse(SaveFile.TextOf(document))!.AsObject(), world.Config).World;
        Villager a = back.Villagers.Single(v => v.Id == adult.Id);
        Assert.Equal((false, VillagerState.Left, (ulong?)77), (a.Alive, a.State, a.LeftAtTick));
        Assert.Null(back.Villagers.First(v => v.Id != adult.Id && v.CanWork).LeftAtTick);
        Assert.Equal(StateHash.Compute(world), StateHash.Compute(back));
    }
}

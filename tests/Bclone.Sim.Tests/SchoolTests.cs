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
/// ⭐ The school without its benefits (D754, `school-and-education.md §12` step 1): learned once a library stands; each
/// staffed teacher brings places; the village fills them from its 12–15-year-olds by itself, the youngest first; a pupil
/// holds no job and is no hand; the term is counted and kept, whole or in part. What school GIVES is step 2.
/// </summary>
public sealed class SchoolTests
{
    private readonly ITestOutputHelper _output;

    public SchoolTests(ITestOutputHelper output) => _output = output;

    /// <summary>Two places a teacher, so four founders can show a full school and a child left over.</summary>
    private static SimConfig Config => VillageFixtures.Village with { SchoolPlacesPerTeacher = 2 };

    /// <summary>A school learned and raised, one teacher asked for and nothing else, and the founders posed by age.</summary>
    private static (SimLoop Loop, Workplace School, Villager Teacher) AStaffedSchool(SimConfig config, params int[] ages)
    {
        SimLoop loop = SimFactory.CreatePhase0(config, new InMemoryLogSink());
        SimWorld world = loop.World;
        world.LearnTheSchool();
        Workplace school = FoodChainTests.RaiseA(world, BuildingKind.School);
        ToolsTests.KeepTheVillageWarmAndFed(world);

        List<Villager> living = world.Villagers.Where(v => v.Alive).OrderBy(v => v.Id).ToList();
        Assert.True(living.Count > ages.Length, $"the fixture has {living.Count} villagers; the pose wants {ages.Length + 1}");
        for (int i = 0; i < ages.Length; i++)
        {
            living[i + 1].AgeYears = ages[i];

            // ⚠️ The stage with the age (D758): a labour pass runs inside this pose, and an eleven-year-old left an adult
            // was enrolled there before any rule about children was asked — the new guard first scored zero for it.
            living[i + 1].LifeStage = AgeingSystem.StageForAge(ages[i], living[i + 1].Vigour, config);
        }

        FoodChainTests.OnlyOneTradeWorks(world, JobKind.Teacher);
        return (loop, school, living[0]);
    }

    // ---------------------------------------------------------------
    //  §11.3 call 1 — learned once a library stands
    // ---------------------------------------------------------------

    [Fact]
    public void TheSchoolIsLearnedOnceALibraryStands()
    {
        var sink = new InMemoryLogSink();
        SimConfig config = VillageFixtures.Village with { LiteracyYears = 0 };
        SimLoop loop = SimFactory.CreatePhase0(config, sink);
        SimWorld world = loop.World;
        loop.Step(2);

        // ⭐ The gate the build bar and `Mark` both read (`WhyNotYet`) — `CanBuildAt` is where a building fits, not whether
        // the village knows it.
        Assert.False(world.SchoolLearned);
        Assert.Contains("library", world.WhyNotYet(BuildingKind.School) ?? "nothing", StringComparison.Ordinal);

        Raise(world, BuildingKind.Granary);
        loop.Step(2);
        Assert.True(world.HasLiteracy, "the pose wanted a granary to teach writing at once (literacy_years 0)");
        Assert.False(world.SchoolLearned, "the school was learned by the granary, before any library stood");

        Raise(world, BuildingKind.Library);
        Assert.True(world.SchoolLearned);
        Assert.Null(world.WhyNotYet(BuildingKind.School));
        Assert.Contains(sink.Entries, e => e.Message.Contains("teach the young", StringComparison.Ordinal));

        // ⭐ The tree's node says so too, from the same latch (the map's own reader).
        TechNodeRow node = world.Config.TechNodeRows.Single(n => n.Id == "school");
        Assert.Equal(BuildingKind.School, node.Unlocks);
        Assert.Equal(TechState.Known, TechTree.StateOf(world, node).State);
    }

    /// <summary>A building raised the way the player raises one — marked, stocked, worked and completed; a store or a library.</summary>
    private static void Raise(SimWorld world, BuildingKind kind)
    {
        GridPos site = world.Map.FoundingSite;
        for (int radius = 3; radius < 30; radius++)
        {
            for (int dy = -radius; dy <= radius; dy++)
            {
                for (int dx = -radius; dx <= radius; dx++)
                {
                    var tile = new GridPos(site.X + dx, site.Y + dy);
                    if (world.CanBuildAt(kind, tile).Allowed && world.Mark(kind, tile).Allowed)
                    {
                        Workplace plan = world.Workplaces.Last(w => w.Construction?.Kind == kind);
                        BuildFixtures.StockTheSite(plan);
                        for (int i = 0; i <= plan.Construction!.Recipe.WorkTicks; i++)
                        {
                            plan.Construction.Work();
                        }

                        world.Complete(plan);
                        return;
                    }
                }
            }
        }

        Assert.Fail($"nowhere to raise a {kind} near the founding");
    }

    // ---------------------------------------------------------------
    //  §11.3 call 4 — the teachers bring places, the village fills them
    // ---------------------------------------------------------------

    [Fact]
    public void AStaffedSchoolFillsItsPlacesWithTheYoungestFirst()
    {
        SimConfig config = Config;
        (SimLoop loop, Workplace school, Villager teacher) = AStaffedSchool(config, 15, 12, 13);
        SimWorld world = loop.World;
        loop.Step(config.TicksPerDay);

        List<Villager> pupils = SchoolRoll.PupilsAt(world, school.Id);
        _output.WriteLine($"teachers {school.WorkerIds.Count}, places {SchoolRoll.PlacesAt(world, school)}, pupils "
            + string.Join(", ", pupils.Select(p => $"{p.Name} {p.AgeYears}")));

        Assert.Equal(new[] { teacher.Id }, school.WorkerIds);
        Assert.Equal(config.SchoolPlacesPerTeacher, SchoolRoll.PlacesAt(world, school));

        // ⭐ Two places, three children: the twelve- and thirteen-year-olds, never the fifteen (the most term ahead first).
        Assert.Equal(new[] { 12, 13 }, pupils.Select(p => p.AgeYears).OrderBy(a => a).ToArray());
        Villager left = world.Villagers.Single(v => v.Alive && v.AgeYears == 15);
        Assert.False(left.IsPupil);

        // A pupil holds no job and is not one of the village's hands — the cost, counted where the quota counts it.
        Assert.All(pupils, p => Assert.False(p.HasJob, $"{p.Name} holds a job at school"));
        Assert.All(pupils, p => Assert.False(p.IsLaborer, $"{p.Name} reads as a laborer at school"));
        Assert.Equal(world.Villagers.Count(v => v.Alive && v.CanWork) - pupils.Count, world.AbleAdults);
    }

    [Fact]
    public void ATermThatStartsBeforeWorkTakesAChild()
    {
        // ⭐ D758 (Joe: "2 years in school from age 11-13"): below adult_age a pupil is a child — by age, not by CanWork.
        SimConfig config = Config with { SchoolStartingAge = 11, SchoolLeavingAge = 13 };
        Assert.Equal(2 * config.TicksPerYear, config.SchoolTermTicks);
        (SimLoop loop, Workplace school, _) = AStaffedSchool(config, 11, 12, 14);
        SimWorld world = loop.World;
        Villager child = world.Villagers.Single(v => v.Alive && v.AgeYears == 11);
        loop.Step(config.TicksPerDay);

        Assert.Equal(new[] { 11, 12 }, SchoolRoll.PupilsAt(world, school.Id).Select(p => p.AgeYears).OrderBy(a => a).ToArray());
        Assert.False(child.CanWork, "the pose wanted a child");

        // And as shipped (twelve) the eleven-year-old waits a year.
        (SimLoop shipped, Workplace itsSchool, _) = AStaffedSchool(Config, 11, 12);
        shipped.Step(Config.TicksPerDay);
        Assert.Equal(new[] { 12 }, SchoolRoll.PupilsAt(shipped.World, itsSchool.Id).Select(p => p.AgeYears).ToArray());
    }

    // ---------------------------------------------------------------
    //  §12.6 — a warm school (D764, Joe: "Warm, burns firewood")
    // ---------------------------------------------------------------

    [Fact]
    public void AStaffedSchoolIsWarmInWinter()
    {
        SimConfig config = Config;
        (SimLoop loop, Workplace school, _) = AStaffedSchool(config, 12, 13);
        SimWorld world = loop.World;
        loop.Step(config.TicksPerDay);

        // A roof before any wood is in it — not the open ground D757 found.
        Assert.Equal(Shelter.Roof, world.ShelterAt(school.Tile));

        while (world.Clock.Season != Season.Winter)
        {
            loop.Step(1);
        }

        loop.Step(config.TicksPerDay);
        _output.WriteLine($"winter: the school's pile {school.Store.Firewood}, {world.ShelterAt(school.Tile)}");
        Assert.True(school.Store.Firewood > 0, "a staffed school in winter, the homes covered, has no wood in its pile");
        Assert.Equal(Shelter.Fire, world.ShelterAt(school.Tile));

        // ⭐ And it burns its winter: a pile put by now goes down by the winter's beats still to come.
        int winter = config.SchoolWinterFirewood;
        int beats = (config.DaysPerSeason + config.FirewoodBurnIntervalDays - 1) / config.FirewoodBurnIntervalDays;
        school.Store.Add(Goods.Firewood, 100);
        int put = school.Store.Firewood;
        while (world.Clock.Season == Season.Winter)
        {
            loop.Step(1);
        }

        int burned = put - school.Store.Firewood;
        _output.WriteLine($"the school burned {burned} of a {winter}-log winter after its first beat");
        Assert.InRange(burned, winter - HearthSystem.BurnOnBeat(winter, 0, beats) - HearthSystem.BurnOnBeat(winter, 1, beats), winter);

        // Unstaffed, its fire has nobody to keep it: a roof, whatever is in the pile.
        world.SetJobLimit(JobKind.Teacher, 0);
        loop.Step(config.LabourSlackTicks + 1);
        Assert.Empty(school.WorkerIds);
        Assert.Equal(Shelter.Roof, world.ShelterAt(school.Tile));
    }

    [Fact]
    public void TheHomesComeFirstForTheSchoolsFire()
    {
        var sink = new InMemoryLogSink();
        SimConfig config = Config;
        SimLoop loop = SimFactory.CreatePhase0(config, sink);
        SimWorld world = loop.World;
        world.LearnTheSchool();
        Workplace school = FoodChainTests.RaiseA(world, BuildingKind.School);
        ToolsTests.KeepTheVillageWarmAndFed(world);
        FoodChainTests.OnlyOneTradeWorks(world, JobKind.Teacher);
        while (world.Clock.Season != Season.Winter)
        {
            loop.Step(1);
        }

        loop.Step(config.TicksPerDay);
        Assert.True(school.Store.Firewood > 0, "the pose wanted a lit school first");

        // The homes' woodpiles emptied and the stores down to a few logs: the homes are short, so the school's pile is not
        // kept — though a store still holds wood it could take — and goes out, said once.
        foreach (Household household in world.Households)
        {
            household.Stockpile.TryTake(Goods.Firewood, household.Stockpile.Firewood);
        }

        foreach (StoreBuilding store in world.StoreBuildings)
        {
            store.Store.TryTake(Goods.Firewood, store.Store.Firewood);
        }

        // ⚠️ One log short, not a few logs in the store: a household fetches a few logs before the school's next beat and
        // the gate is never asked (this guard first scored zero for it). A store-to-home fetch leaves the shortfall where it is.
        world.StoreBuildings.First(s => s.Gives(Goods.Firewood)).Store.Add(Goods.Firewood, LabourQuota.HomesFirewoodShortfall(world) - 1);
        Assert.Equal(1, LabourQuota.HomesFirewoodShortfall(world));

        int took = 0;
        int before = school.Store.Firewood;
        for (int t = 0; t < config.TicksPerDay * config.FirewoodBurnIntervalDays * 3; t++)
        {
            loop.Step(1);
            took = Math.Max(took, school.Store.Firewood - before);
            before = Math.Min(before, school.Store.Firewood);
        }

        Assert.Equal(0, took);
        Assert.True(LabourQuota.HomesFirewoodShortfall(world) > 0, "the homes were covered by the end — the gate was not what was asked");
        Assert.Equal(0, school.Store.Firewood);
        Assert.Equal(Shelter.Roof, world.ShelterAt(school.Tile));
        Assert.Single(sink.Entries, e => e.Message.Contains("is cold — the homes need", StringComparison.Ordinal));
    }

    [Fact]
    public void TheSchoolsWinterIsInTheWoodcuttersWant()
    {
        SimConfig config = Config;
        (SimLoop loop, Workplace school, _) = AStaffedSchool(config, 12, 13);
        SimWorld world = loop.World;
        loop.Step(config.TicksPerDay);
        Assert.NotEmpty(school.WorkerIds);
        foreach (StoreBuilding store in world.StoreBuildings)
        {
            store.Store.TryTake(Goods.Firewood, store.Store.Firewood);
        }

        Assert.Equal(config.SchoolWinterFirewood, LabourQuota.FirewoodShortfall(world) - LabourQuota.HomesFirewoodShortfall(world));
    }

    // ---------------------------------------------------------------
    //  Step 4 — what the school says (D765, §6)
    // ---------------------------------------------------------------

    /// <summary>
    /// ⭐ NO ASK BEFORE ANYBODY TEACHES (D786, Joe's Ashbourne log, D773): a school standing with no teacher leaves nobody
    /// "without a place" — nobody can have one yet — so the village says nothing and counts nobody waiting; once a teacher
    /// starts and the places fall short, it asks, once.
    /// </summary>
    [Fact]
    public void AnUntaughtSchoolAsksForNothing()
    {
        var sink = new InMemoryLogSink();
        SimConfig config = Config;
        SimLoop loop = SimFactory.CreatePhase0(config, sink);
        SimWorld world = loop.World;
        world.LearnTheSchool();
        Workplace school = FoodChainTests.RaiseA(world, BuildingKind.School);
        ToolsTests.KeepTheVillageWarmAndFed(world);
        List<Villager> living = world.Villagers.Where(v => v.Alive).OrderBy(v => v.Id).ToList();
        int[] ages = { 12, 13, 14 };
        for (int i = 0; i < ages.Length; i++)
        {
            living[i + 1].AgeYears = ages[i];
            living[i + 1].LifeStage = AgeingSystem.StageForAge(ages[i], living[i + 1].Vigour, config);
        }

        world.SetJobLimit(JobKind.Teacher, 0);
        loop.Step(config.TicksPerDay);
        Assert.Empty(school.WorkerIds);
        Assert.Equal(0, world.ChildrenWaitingForSchool);
        Assert.DoesNotContain(sink.Entries, e => e.Message.Contains("no place at school", StringComparison.Ordinal));

        FoodChainTests.OnlyOneTradeWorks(world, JobKind.Teacher);
        loop.Step(config.LabourSlackTicks + config.TicksPerDay);
        Assert.NotEmpty(school.WorkerIds);
        Assert.Single(sink.Entries, e => e.Message.Contains("no place at school", StringComparison.Ordinal));
    }

    /// <summary>
    /// ⭐ NEVER "AFTER 0 YEARS" (D786, Joe's Ashbourne log, D773): a pupil who leaves inside their first year is said to have
    /// been there seasons, or less than a season.
    /// </summary>
    [Theory]
    [InlineData(0, "less than a season")]
    [InlineData(1, "a season")]
    [InlineData(3, "3 seasons")]
    [InlineData(4, "a year")]
    [InlineData(9, "2 years")]
    public void ALeaverIsSaidToHaveBeenThereSomeTime(int seasons, string said)
    {
        SimConfig config = Config;
        Assert.Equal(said, SchoolRoll.HowLong((seasons * config.TicksPerSeason) + 1, config));
    }

    [Fact]
    public void TheSchoolSaysItsPlacesAndAsksForAnotherOnce()
    {
        var sink = new InMemoryLogSink();
        SimConfig config = Config;
        SimLoop loop = SimFactory.CreatePhase0(config, sink);
        SimWorld world = loop.World;
        world.LearnTheSchool();
        Workplace school = FoodChainTests.RaiseA(world, BuildingKind.School);
        ToolsTests.KeepTheVillageWarmAndFed(world);
        List<Villager> living = world.Villagers.Where(v => v.Alive).OrderBy(v => v.Id).ToList();
        int[] ages = { 12, 13, 14 };
        for (int i = 0; i < ages.Length; i++)
        {
            living[i + 1].AgeYears = ages[i];
            living[i + 1].LifeStage = AgeingSystem.StageForAge(ages[i], living[i + 1].Vigour, config);
        }

        FoodChainTests.OnlyOneTradeWorks(world, JobKind.Teacher);
        loop.Step(config.TicksPerDay);
        Assert.False(world.Clock.IsWinter, "the pose wanted a season without the stove's sentence");

        // Two places, three children: the card says so, and the village asks once — naming the cheaper answer, the empty seat.
        Assert.Equal("2 of 2 places taken — a child of school age waits for a place.", world.SayTheSchool(school));
        Assert.Equal(1, world.ChildrenWaitingForSchool);
        LogEntry asked = Assert.Single(sink.Entries, e => e.Message.Contains("no place at school", StringComparison.Ordinal));
        Assert.Contains($"another teacher at {school.Name}", asked.Message, StringComparison.Ordinal);

        loop.Step(config.LabourSlackTicks * 3);
        Assert.Single(sink.Entries, e => e.Message.Contains("no place at school", StringComparison.Ordinal));

        // Nobody teaching: the card says how places come.
        world.SetJobLimit(JobKind.Teacher, 0);
        loop.Step(config.LabourSlackTicks + 1);
        Assert.StartsWith("Nobody teaches here", world.SayTheSchool(school), StringComparison.Ordinal);
    }

    [Fact]
    public void ASchoolingIsSaidInWords()
    {
        SimConfig config = Config with { SchoolCarryPercent = 25, SchoolOldAgePercent = 20, SchoolHeedPercent = 80 };
        (SimLoop loop, Workplace school, Villager teacher) = AStaffedSchool(config, 12);
        SimWorld world = loop.World;
        loop.Step(config.TicksPerDay);

        Villager pupil = Assert.Single(SchoolRoll.PupilsAt(world, school.Id));
        Assert.Equal($"At {school.Name} — in the first of four years.", world.SayTheSchooling(pupil));

        int term = config.SchoolTermTicks;
        teacher.SchoolTicks = term;
        Assert.Equal("Spent four years at the school — carries a fuller load, will age more gently, and goes in from the cold "
            + "by the length of the walk to a fire.", world.SayTheSchooling(teacher));

        // A quarter of the term: a share of the first two, not yet half — so not the third.
        teacher.SchoolTicks = term / 4;
        Assert.Equal("Spent a year at the school before the place went — carries a fuller load and will age more gently.",
            world.SayTheSchooling(teacher));

        // Never went: nothing to say. And at nought, the years without the gifts.
        teacher.SchoolTicks = 0;
        Assert.Null(world.SayTheSchooling(teacher));
        teacher.SchoolTicks = term;
        SimLoop plain = SimFactory.CreatePhase0(Config, new InMemoryLogSink());
        Villager someone = plain.World.Villagers[0];
        someone.SchoolTicks = Config.SchoolTermTicks;
        Assert.Equal("Spent four years at the school.", plain.World.SayTheSchooling(someone));
    }

    [Fact]
    public void AnUnstaffedSchoolHasNoPlaces()
    {
        SimConfig config = Config;
        (SimLoop loop, Workplace school, _) = AStaffedSchool(config, 12, 13);
        SimWorld world = loop.World;
        world.SetJobLimit(JobKind.Teacher, 0);
        loop.Step(config.TicksPerDay);

        Assert.Empty(school.WorkerIds);
        Assert.Equal(0, SchoolRoll.PlacesAt(world, school));
        Assert.Empty(SchoolRoll.PupilsAt(world, school.Id));
    }

    [Fact]
    public void ATeacherIsPastSchoolAge()
    {
        SimConfig config = Config;
        (SimLoop loop, Workplace school, Villager teacher) = AStaffedSchool(config, 12, 13, 14);

        // Every adult but the children is out of reach: the founder who would teach is gone, so nobody may.
        teacher.AgeYears = teacher.LifespanYears;
        loop.Step(config.TicksPerDay);

        Assert.All(school.WorkerIds.Select(id => loop.World.FindVillager(id)!),
            v => Assert.True(v.AgeYears >= config.SchoolLeavingAge, $"{v.Name}, {v.AgeYears}, teaches the class they would sit in"));
    }

    // ---------------------------------------------------------------
    //  The term, and call 5's credit
    // ---------------------------------------------------------------

    [Fact]
    public void APupilLeavesAtTheEndOfTheTermWithEveryYearServed()
    {
        var sink = new InMemoryLogSink();
        SimConfig config = Config;
        SimLoop loop = SimFactory.CreatePhase0(config, sink);
        SimWorld world = loop.World;
        world.LearnTheSchool();
        Workplace school = FoodChainTests.RaiseA(world, BuildingKind.School);
        ToolsTests.KeepTheVillageWarmAndFed(world);
        Villager pupil = world.Villagers.Where(v => v.Alive).OrderBy(v => v.Id).Last();
        pupil.AgeYears = 14;
        pupil.SchoolTicks = config.SchoolTermTicks - 5;
        FoodChainTests.OnlyOneTradeWorks(world, JobKind.Teacher);
        loop.Step(1);
        Assert.True(pupil.IsPupil, "a fourteen-year-old with five ticks of term left took no place");

        loop.Step(10);

        Assert.False(pupil.IsPupil);
        Assert.Equal(config.SchoolTermTicks, pupil.SchoolTicks);
        Assert.Contains(sink.Entries, e => e.Message.StartsWith($"{pupil.Name} leaves {school.Name} after", StringComparison.Ordinal)
            && e.Message.Contains("for work", StringComparison.Ordinal));

        // ⛔ A full term is never sat twice — past two labour passes, which are where places are filled (the first build
        // of this guard stepped two days and never reached one: a mutant letting the term be sat again scored zero).
        // ⚠️ Counted in the log, not read off `IsPupil`: the term system sends a full-term pupil away the very next tick, so a
        // place taken again shows only as a second "starts at" — the churn is the fault.
        loop.Step((config.LabourSlackTicks * 2) + 1);
        Assert.False(pupil.IsPupil, "a pupil who had sat the full term took a place again");
        Assert.Equal(1, sink.Entries.Count(e => e.Message.StartsWith($"{pupil.Name}, ", StringComparison.Ordinal)
            && e.Message.Contains("starts at", StringComparison.Ordinal)));
    }

    [Fact]
    public void APupilWhoseSchoolLosesItsTeacherKeepsTheYearsServed()
    {
        var sink = new InMemoryLogSink();
        SimConfig config = Config;
        SimLoop loop = SimFactory.CreatePhase0(config, sink);
        SimWorld world = loop.World;
        world.LearnTheSchool();
        FoodChainTests.RaiseA(world, BuildingKind.School);
        ToolsTests.KeepTheVillageWarmAndFed(world);
        Villager pupil = world.Villagers.Where(v => v.Alive).OrderBy(v => v.Id).Last();
        pupil.AgeYears = 12;
        FoodChainTests.OnlyOneTradeWorks(world, JobKind.Teacher);
        loop.Step(config.TicksPerSeason);
        Assert.True(pupil.IsPupil);
        int served = pupil.SchoolTicks;
        Assert.True(served > 0);

        world.SetJobLimit(JobKind.Teacher, 0);
        loop.Step(1);

        Assert.False(pupil.IsPupil);
        Assert.True(pupil.SchoolTicks >= served, "the years served were taken back when the place went");
        Assert.Contains(sink.Entries, e => e.Message.StartsWith($"{pupil.Name} leaves", StringComparison.Ordinal)
            && e.Message.Contains("no place", StringComparison.Ordinal));
    }

    // ---------------------------------------------------------------
    //  Seen, saved, and a village with no school untouched
    // ---------------------------------------------------------------

    [Fact]
    public void APupilIsSeenAtTheSchool()
    {
        SimConfig config = Config;
        (SimLoop loop, Workplace school, _) = AStaffedSchool(config, 12);
        SimWorld world = loop.World;

        int atSchool = 0, teaching = 0;
        for (int tick = 0; tick < config.TicksPerSeason; tick++)
        {
            loop.StepOnce();
            atSchool += world.Villagers.Count(v => v.State == VillagerState.AtSchool && v.Tile == school.Tile);
            teaching += world.Villagers.Count(v => v.State == VillagerState.Teaching && v.Tile == school.Tile);
        }

        _output.WriteLine($"{atSchool} pupil-ticks and {teaching} teacher-ticks at {school.Name} in a season");
        Assert.True(atSchool > 0, "a pupil held a place a whole season and was never seen at the school");
        Assert.True(teaching > 0, "the teacher was never seen teaching");
    }

    [Fact]
    public void APupilAndTheLatchComeBackFromASave()
    {
        SimConfig config = Config;
        (SimLoop loop, Workplace school, _) = AStaffedSchool(config, 12);
        loop.Step(config.TicksPerDay * 3);
        Villager pupil = loop.World.Villagers.Single(v => v.IsPupil);

        JsonObject document = SaveGame.Capture(loop, "test", "2026-11-14 12:00", "test");
        SimWorld back = SaveGame.Load(JsonNode.Parse(SaveFile.TextOf(document))!.AsObject(), config, null).World;

        Villager loaded = back.FindVillager(pupil.Id)!;
        Assert.Equal((school.Id, pupil.SchoolTicks), (loaded.SchoolId, loaded.SchoolTicks));
        Assert.True(back.SchoolLearned);
        Assert.Equal(StateHash.Compute(loop.World), StateHash.Compute(back));
    }

    /// <summary>
    /// Where a villager is a pupil and the term served are in the fingerprint (D754) — they decide what the villager does.
    /// ⚠️ A save round trip cannot see a missing hash (both sides lack it); this asks the fingerprint directly.
    /// </summary>
    [Fact]
    public void APupilIsInTheFingerprint()
    {
        SimWorld world = SimFactory.CreatePhase0(Config, new InMemoryLogSink()).World;
        Villager child = world.Villagers[0];
        ulong before = StateHash.Compute(world);

        child.SchoolId = 7;
        ulong enrolled = StateHash.Compute(world);
        child.SchoolId = 0;
        child.SchoolTicks = 9;
        ulong served = StateHash.Compute(world);
        child.SchoolTicks = 0;

        Assert.NotEqual(before, enrolled);
        Assert.NotEqual(before, served);
        Assert.Equal(before, StateHash.Compute(world));
    }

    // ---------------------------------------------------------------
    //  §11.4 — what school gives (step 2, its sizes at nought until step 3)
    // ---------------------------------------------------------------

    /// <summary>A, reshaped (D761) — <i>"a gentler old age"</i>: full strength ends at 30 as everyone's, the fall is shallower.</summary>
    [Fact]
    public void AGraduateAgesMoreGently()
    {
        SimConfig config = VillageFixtures.Village with { SchoolOldAgePercent = 20 };
        IReadOnlyList<Villager> people = SimFactory.CreatePhase0(config, new InMemoryLogSink()).World.Villagers;
        (Villager graduate, Villager halfway, Villager never) = (people[0], people[1], people[2]);
        graduate.SchoolTicks = config.SchoolTermTicks;
        halfway.SchoolTicks = config.SchoolTermTicks / 2;

        Assert.Equal(20, AgeingSystem.OldAgeFromSchool(config, graduate));
        Assert.Equal(10, AgeingSystem.OldAgeFromSchool(config, halfway));
        Assert.Equal(0, AgeingSystem.OldAgeFromSchool(config, never));

        // ⛔ Never more than a full term's, however long the ticks run.
        graduate.SchoolTicks = config.SchoolTermTicks * 3;
        Assert.Equal(20, AgeingSystem.OldAgeFromSchool(config, graduate));

        int lifespan = 67;
        int min = config.VigourMinPercent;

        // The prime years are everyone's: full until 30 either way, and not a year longer.
        Assert.Equal(100, AgeingSystem.ComputeVigour(config.VigourFullUntilAge, lifespan, config, 20));
        Assert.True(AgeingSystem.ComputeVigour(config.VigourFullUntilAge + 2, lifespan, config, 20) < 100,
            "a graduate is at full strength past 30 — that was D753's A, measured weak and replaced");

        // ⭐ The end is higher, and so is every year of the fall.
        Assert.Equal(min + 20, AgeingSystem.ComputeVigour(lifespan, lifespan, config, 20));
        Assert.Equal(min, AgeingSystem.ComputeVigour(lifespan, lifespan, config, 0));
        for (int age = config.VigourFullUntilAge + 2; age < lifespan; age++)
        {
            Assert.True(AgeingSystem.ComputeVigour(age, lifespan, config, 20) > AgeingSystem.ComputeVigour(age, lifespan, config, 0),
                $"at {age} a graduate is no stronger than anybody");
        }

        // At nought — as shipped until it is measured — nobody's strength moves.
        Assert.Equal(0, AgeingSystem.OldAgeFromSchool(VillageFixtures.Village, graduate));
    }

    /// <summary>Fuller loads (D761): a share more a trip by the term served; at nought exactly the load.</summary>
    [Fact]
    public void AGraduateCarriesAFullerLoad()
    {
        SimConfig config = VillageFixtures.Village with { SchoolCarryPercent = 50 };
        IReadOnlyList<Villager> people = SimFactory.CreatePhase0(config, new InMemoryLogSink()).World.Villagers;
        (Villager graduate, Villager halfway, Villager never) = (people[0], people[1], people[2]);
        graduate.SchoolTicks = config.SchoolTermTicks * 3;
        halfway.SchoolTicks = config.SchoolTermTicks / 2;

        Assert.Equal(120, SchoolGives.Load(config, graduate, 80));
        Assert.Equal(100, SchoolGives.Load(config, halfway, 80));
        Assert.Equal(80, SchoolGives.Load(config, never, 80));
        Assert.Equal(80, SchoolGives.Load(VillageFixtures.Village, graduate, 80));
    }

    /// <summary>
    /// ⭐ And it is seen in a trip (D761): the same gatherer in the same valley brings more home a trip as a graduate —
    /// <c>AgeingTests.OldAgeCostsMoreWorkForTheSameFood</c>'s ruler (food per trip, no techniques), one person each way.
    /// </summary>
    [Fact]
    public void AGraduateBringsMoreHomeFromAGather()
    {
        SimConfig config = Phase0Fixtures.Plenty with { SchoolCarryPercent = 50, Techniques = Array.Empty<TechniqueRow>() };
        double asNever = FoodPerGather(config, graduate: false);
        double asGraduate = FoodPerGather(config, graduate: true);
        _output.WriteLine($"food per gather — never at school {asNever:F1}, a graduate {asGraduate:F1}");

        Assert.True(asGraduate >= asNever * 1.4, $"a graduate brought {asGraduate:F1} a gather against {asNever:F1}");
    }

    private static double FoodPerGather(SimConfig config, bool graduate)
    {
        (SimLoop loop, _) = Phase0Fixtures.Build(config);
        SimWorld world = loop.World;
        if (graduate)
        {
            world.Villager.SchoolTicks = config.SchoolTermTicks;
        }

        loop.Step(config.TicksPerYear * 5);
        int food = world.HouseholdOf(world.Villager).Stockpile.Produced(Goods.Produce)
            + world.StoreBuildings.Sum(s => s.Store.Produced(Goods.Produce));
        Assert.True(world.Villager.TotalGathers > 0, "the pose wanted somebody gathering");
        return (double)food / world.Villager.TotalGathers;
    }

    /// <summary>
    /// B(i) — <i>"she reads the year"</i>: a graduate goes in by the walk, not at the halfway line — on near home, early from
    /// the far wood. Asked of the one rule both kinds of villager go in by.
    /// </summary>
    [Fact]
    public void AGraduateJudgesTheWalkToTheFire()
    {
        SimConfig config = VillageFixtures.Village with { SchoolHeedPercent = 80 };
        int threshold = config.ExposureThreshold;
        IReadOnlyList<Villager> people = SimFactory.CreatePhase0(config, new InMemoryLogSink()).World.Villagers;
        (Villager graduate, Villager neighbour, Villager aYear) = (people[0], people[1], people[2]);
        graduate.SchoolTicks = config.SchoolTermTicks;
        Assert.True(BehaviorSystem.ReadsTheYear(config, graduate));
        Assert.False(BehaviorSystem.ReadsTheYear(config, neighbour));

        // Beside the village, at 60 % of the way: the neighbour is past the halfway line and goes in; she works on.
        int near = threshold * 2 / 100;
        graduate.Cold = neighbour.Cold = threshold * 60 / 100;
        Assert.True(BehaviorSystem.GoesInForWarmth(config, neighbour, readsTheYear: false, walkChill: near));
        Assert.False(BehaviorSystem.GoesInForWarmth(config, graduate, readsTheYear: true, walkChill: near));

        // In the far wood at 30 %, the walk itself worth 55 %: the neighbour works on toward a walk that will take them past
        // 80; she heads in now.
        int far = threshold * 55 / 100;
        graduate.Cold = neighbour.Cold = threshold * 30 / 100;
        Assert.False(BehaviorSystem.GoesInForWarmth(config, neighbour, readsTheYear: false, walkChill: far));
        Assert.True(BehaviorSystem.GoesInForWarmth(config, graduate, readsTheYear: true, walkChill: far));

        // ⛔ It takes half the term, and the knob: a year at school does not teach it, and at nought nobody has it.
        aYear.SchoolTicks = (config.SchoolTermTicks / 2) - 1;
        Assert.False(BehaviorSystem.ReadsTheYear(config, aYear));
        Assert.False(BehaviorSystem.ReadsTheYear(VillageFixtures.Village, graduate));
    }

    /// <summary>Step 5 (D766): the school's latch is in the fingerprint — learn it, and the hash moves.</summary>
    [Fact]
    public void ALearnedSchoolIsInTheFingerprint()
    {
        SimWorld world = SimFactory.CreatePhase0(Config, new InMemoryLogSink()).World;
        ulong before = StateHash.Compute(world);
        world.LearnTheSchool();
        Assert.NotEqual(before, StateHash.Compute(world));
    }

    [Fact]
    public void AVillageWithNoSchoolNeverSeesAPupil()
    {
        SimConfig config = VillageFixtures.Village;
        SimLoop loop = SimFactory.CreatePhase0(config, new InMemoryLogSink());
        loop.Step(config.TicksPerYear * 2);

        Assert.All(loop.World.Villagers, v => Assert.Equal((0, 0), (v.SchoolId, v.SchoolTicks)));
    }
}

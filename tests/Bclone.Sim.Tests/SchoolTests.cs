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

    /// <summary>A — <i>"she learned to look after herself"</i>: full strength longer, by the term served; the lifespan untouched.</summary>
    [Fact]
    public void AGraduateKeepsFullStrengthLonger()
    {
        SimConfig config = VillageFixtures.Village with { SchoolVigourYears = 5 };
        IReadOnlyList<Villager> people = SimFactory.CreatePhase0(config, new InMemoryLogSink()).World.Villagers;
        (Villager graduate, Villager halfway, Villager never) = (people[0], people[1], people[2]);
        graduate.SchoolTicks = config.SchoolTermTicks;
        halfway.SchoolTicks = config.SchoolTermTicks / 2;

        Assert.Equal(5, AgeingSystem.StrongYearsFromSchool(config, graduate));
        Assert.Equal(2, AgeingSystem.StrongYearsFromSchool(config, halfway));
        Assert.Equal(0, AgeingSystem.StrongYearsFromSchool(config, never));

        // ⛔ Never more than a full term's, however long the ticks run.
        graduate.SchoolTicks = config.SchoolTermTicks * 3;
        Assert.Equal(5, AgeingSystem.StrongYearsFromSchool(config, graduate));

        int fullUntil = config.VigourFullUntilAge, lifespan = 67;
        int at34 = fullUntil + 4;
        Assert.Equal(100, AgeingSystem.ComputeVigour(at34, lifespan, config, 5));
        Assert.True(AgeingSystem.ComputeVigour(at34, lifespan, config, 0) < 100, "an ungraduated 34-year-old is at full strength already");

        // The same floor in the same final year: school buys strong years, not more years.
        Assert.Equal(config.VigourMinPercent, AgeingSystem.ComputeVigour(lifespan, lifespan, config, 5));
        Assert.Equal(AgeingSystem.ComputeVigour(lifespan, lifespan, config, 0), AgeingSystem.ComputeVigour(lifespan, lifespan, config, 5));

        // At nought — as shipped until step 3 — nobody's strength moves.
        Assert.Equal(0, AgeingSystem.StrongYearsFromSchool(VillageFixtures.Village, graduate));
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

    [Fact]
    public void AVillageWithNoSchoolNeverSeesAPupil()
    {
        SimConfig config = VillageFixtures.Village;
        SimLoop loop = SimFactory.CreatePhase0(config, new InMemoryLogSink());
        loop.Step(config.TicksPerYear * 2);

        Assert.All(loop.World.Villagers, v => Assert.Equal((0, 0), (v.SchoolId, v.SchoolTicks)));
    }
}

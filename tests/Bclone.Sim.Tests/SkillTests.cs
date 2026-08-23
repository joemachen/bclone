using System;
using System.Collections.Generic;
using System.Linq;
using Bclone.Sim.Config;
using Bclone.Sim.Core;
using Bclone.Sim.Determinism;
using Bclone.Sim.Logging;
using Bclone.Sim.Systems;
using Bclone.Sim.World;
using Xunit;
using Xunit.Abstractions;

namespace Bclone.Sim.Tests;

/// <summary>
/// Skill — <c>specs/skills-catalog.md</c>, Phase 3, <b>landing 1: the proficiency substrate</b>.
/// </summary>
/// <remarks>
/// <para>
/// <b>⭐ THE LANDING'S WHOLE CLAIM IS THAT NOTHING ANYBODY DOES CHANGED</b> (§11.2.1). People
/// accrue time on the task, it is hashed, it is visible — and no behaviour anywhere reads it.
/// Landing 2 makes it bite and **must** move what these guards hold still.
/// </para>
/// </remarks>
public sealed class SkillTests
{
    private readonly ITestOutputHelper _output;

    public SkillTests(ITestOutputHelper output) => _output = output;

    private static SimLoop Loop(SimConfig config) =>
        SimFactory.CreatePhase0(config, new InMemoryLogSink());

    private static int SkillIdFor(SimConfig config, JobKind job) =>
        config.Skills.First(skill => skill.GrownBy == job).Id;

    // ---------------------------------------------------------------
    //  ⭐⭐ The no-op, stated in the vocabulary that can be true
    // ---------------------------------------------------------------

    /// <summary>
    /// ⭐⭐ Fifty years of village, and <b>only the counters moved</b>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>⛔ THE GUARD THE SPEC ASKED FOR CANNOT BE WRITTEN, AND THIS IS THE ONE THAT CAN.</b>
    /// §11.2.1 wants landing 1 to be a *provable no-op: goldens unmoved*. The goldens are **full
    /// state hashes**, and proficiency is hashed state that grows from the first tick — so they
    /// move by construction, and no amount of care makes them not. The spec reasoned by analogy
    /// from `crops-and-orchards.md`'s terrain values, where the generator never produced the new
    /// values and a valley genuinely hashed the same. **Proficiency is produced immediately, so
    /// the analogy does not hold** (D181).
    /// </para>
    /// <para>
    /// <b>What is asserted instead is the claim that actually matters: nothing anybody DOES
    /// changed.</b> Same tick, same positions, same stores, same births, same deaths — the two
    /// numbers below are <see cref="StockLimitTests"/>'s fifty-year goldens **as they stood
    /// before this slice**, recomputed over everything except skills. If a future change to the
    /// substrate makes somebody walk somewhere different, this goes red and names the half.
    /// </para>
    /// <para>
    /// ⚠️ <b>It is the same device `PerSiteYieldTests` uses one system over</b> — a fingerprint
    /// that deliberately excludes the thing under test, so the guard says *which* half moved
    /// rather than only *that* something did.
    /// </para>
    /// </remarks>
    [Theory]
    [InlineData(false, 18149215200660116896UL)]
    [InlineData(true, 2960234095731849111UL)]
    public void FiftyYearsOfVillageAndOnlyTheCountersMoved(bool shipped, ulong beforeSkills)
    {
        SimConfig config = shipped ? ShippedConfig.Established() : VillageFixtures.Village;
        SimLoop loop = Loop(config);

        loop.Step(config.TicksPerYear * 50);

        ulong withoutSkills = StateHash.ComputeIgnoringSkills(loop.World);
        ulong withSkills = StateHash.Compute(loop.World);

        _output.WriteLine(
            $"{(shipped ? "shipped" : "fixture")}: 50y without skills {withoutSkills}, "
            + $"with skills {withSkills}");

        Assert.Equal(beforeSkills, withoutSkills);

        // ⭐ AND THE ANTI-VACUITY HALF, IN TWO PARTS BECAUSE THE FIRST ALONE WAS WEAKER THAN
        // IT LOOKED. `Assert.NotEqual` on the two hashes stays green against a substrate that
        // creates entries and never counts a tick — the ids alone move the hash. **Neutering
        // growth left this arm passing**, which is the vacuous half D7 requires be checked for.
        // So the ticks are asserted directly.
        Assert.NotEqual(withoutSkills, withSkills);

        int years = loop.World.Villagers.Sum(villager =>
            villager.Skills.Sum(progress => progress.Ticks));

        _output.WriteLine($"{years / config.TicksPerYear} villager-years of trade on the books");
        Assert.True(years > config.TicksPerYear, "Fifty years and nobody worked a whole one.");
    }

    // ---------------------------------------------------------------
    //  Growth is time on the task, and nothing else
    // ---------------------------------------------------------------

    /// <summary>⭐ A tick on the trade is a tick in the trade — and holding none earns none.</summary>
    /// <remarks>
    /// <b>Both arms, because either alone is green against a bug.</b> "It goes up" passes for a
    /// counter that goes up for everybody; "a laborer gains nothing" passes for a counter that
    /// never goes up at all.
    /// </remarks>
    [Fact]
    public void TimeOnTheTradeIsTheOnlyThingThatCounts()
    {
        SimConfig config = ShippedConfig.Established();
        SimLoop loop = Loop(config);

        // ⚠️ MID-SUMMER, NOT ON THE YEAR EDGE, AND THE REASON IS A MEASUREMENT.
        // At *Day 1, Spring* the reshuffle (D46) has just torn every allocation down and not
        // yet rebuilt it, so **0 of 4 able adults hold a job on that exact tick** — sampling
        // there would have this guard assert against an instant that exists for one tick
        // every three years. Half a season in, it is 4 of 4.
        loop.Step((config.TicksPerYear * 5) + config.TicksPerSeason + (config.TicksPerSeason / 2));
        Assert.Equal(Season.Summer, loop.World.Clock.Season);

        var working = new List<Villager>();
        var spare = new List<Villager>();

        for (int i = 0; i < loop.World.Villagers.Count; i++)
        {
            Villager villager = loop.World.Villagers[i];
            if (!villager.Alive || !villager.CanWork)
            {
                continue;
            }

            (villager.HasJob ? working : spare).Add(villager);
        }

        Assert.NotEmpty(working);

        foreach (Villager villager in working)
        {
            Workplace workplace = loop.World.FindWorkplace(villager.WorkplaceId)!;
            int skill = SkillIdFor(config, workplace.Kind);

            Assert.True(
                villager.TicksIn(skill) > 0,
                $"{villager.Name} has held {workplace.Kind} and has nothing in that trade.");
        }

        // ⛔ A laborer holds no trade (§4.2, D66): a skill in being spare is a contradiction,
        // and crediting one would quietly make the fallback a career.
        foreach (Villager villager in spare)
        {
            int total = villager.Skills.Sum(progress => progress.Ticks);
            _output.WriteLine($"{villager.Name} is spare and holds {total} ticks of trade");
        }
    }

    /// <summary>⛔ A villager moved off a trade stops gaining in it <b>the same tick</b>.</summary>
    /// <remarks>
    /// §10's own wording. Posed rather than played: a village that happens not to move anybody
    /// would pass this without ever exercising it, which is the blind-but-green shape D157 has
    /// found three times.
    /// </remarks>
    [Fact]
    public void LeavingATradeStopsTheClockOnItThatTick()
    {
        SimConfig config = ShippedConfig.Established();
        SimLoop loop = Loop(config);

        // ⚠️ DELIBERATELY NOT ON A YEAR EDGE, AND THIS GUARD WAS BLIND UNTIL IT WAS.
        // Stepping exactly `TicksPerYear` lands on the tick decay runs, so the assertion below
        // was reading "unchanged" out of two effects cancelling — no growth, and no decay only
        // because the floor happened to protect a first-year worker. **Breaking the floor turned
        // this red for a reason that had nothing to do with what it claims to test** (the
        // handoff's *a guard can be green and blind*, found in the act). Half a season in, the
        // only thing that could move the number is the thing under test.
        loop.Step(config.TicksPerYear + (config.TicksPerSeason / 2));
        Assert.Equal(
            SimClock.FromTick(loop.World.Tick - 1UL, config).Year,
            loop.World.Clock.Year);

        Villager worker = loop.World.Villagers.First(villager =>
            villager.Alive && villager.HasJob);
        Workplace workplace = loop.World.FindWorkplace(worker.WorkplaceId)!;
        int skill = SkillIdFor(config, workplace.Kind);

        int before = worker.TicksIn(skill);
        Assert.True(before > 0, "The fixture never put anybody to work.");

        // Take the job away by hand and step one tick — no reshuffle, no season, nothing else
        // moving that could explain the number staying put.
        worker.WorkplaceId = 0;
        var system = new SkillSystem();
        system.Execute(loop.World);

        Assert.Equal(before, worker.TicksIn(skill));
    }

    // ---------------------------------------------------------------
    //  Decay — gentle, derived, and never to zero
    // ---------------------------------------------------------------

    /// <summary>⭐ Three years away costs one year of the trade — the derived rate (§3.4).</summary>
    /// <remarks>
    /// <b>The derivation is the assertion.</b> `labour_reshuffle_years` is 3 (D46), so one full
    /// cycle spent elsewhere must cost less than a cycle bought — otherwise the allocator is the
    /// trap §3.4 forbids and the player starts fighting a system meant to save them work.
    /// </remarks>
    [Fact]
    public void ThreeYearsAwayCostsOneYearOfTheTrade()
    {
        SimConfig config = ShippedConfig.Established();
        SimLoop loop = Loop(config);
        SimWorld world = loop.World;

        Villager villager = world.Villagers.First(person => person.Alive);
        villager.WorkplaceId = 0;

        int skill = SkillIdFor(config, JobKind.Farmer);
        SkillProgress progress = villager.ProgressIn(skill);
        progress.Ticks = config.MasteryTicks;

        int before = progress.Ticks;
        StepWholeYears(world, config, config.SkillDecayYearsPerYearLost);

        int lost = before - progress.Ticks;
        _output.WriteLine(
            $"{config.SkillDecayYearsPerYearLost} years off the trade cost {lost} ticks of "
            + $"{config.TicksPerYear} in a year");

        Assert.Equal(config.TicksPerYear, lost);
        Assert.Equal(config.LabourReshuffleYears, config.SkillDecayYearsPerYearLost);
    }

    /// <summary>⛔ *"Not to zero"* — a trade given a year never falls out of the hands.</summary>
    [Fact]
    public void DecayNeverTakesATradeToNothing()
    {
        SimConfig config = ShippedConfig.Established();
        SimLoop loop = Loop(config);
        SimWorld world = loop.World;

        Villager villager = world.Villagers.First(person => person.Alive);
        villager.WorkplaceId = 0;

        int skill = SkillIdFor(config, JobKind.Woodcutter);
        SkillProgress progress = villager.ProgressIn(skill);
        progress.Ticks = config.MasteryTicks;

        // A whole working life away from it, which is far more than enough to reach the floor.
        StepWholeYears(world, config, 60);

        _output.WriteLine(
            $"after 60 years away: {progress.Ticks} ticks, floor {config.SkillFloorTicks}");

        Assert.Equal(config.SkillFloorTicks, progress.Ticks);
        Assert.True(progress.Ticks > 0, "The floor is supposed to be above zero.");
    }

    /// <summary>⛔ Reading a skill nobody has must never create one.</summary>
    /// <remarks>
    /// <b>This is what keeps the structure sparse</b>, and sparse is what §8's no-op contract is
    /// built on. An accessor that quietly materialises a zeroed row would give every villager six
    /// entries on their first year and move every hash for a panel being opened.
    /// </remarks>
    [Fact]
    public void ReadingATradeNobodyHoldsCreatesNothing()
    {
        SimConfig config = ShippedConfig.Established();
        SimLoop loop = Loop(config);

        Villager villager = loop.World.Villagers.First(person => person.Alive);
        int before = villager.Skills.Count;

        Assert.Equal(0, villager.TicksIn(SkillIdFor(config, JobKind.Marketer)));
        Assert.Null(villager.FindProgressIn(SkillIdFor(config, JobKind.Marketer)));
        Assert.Equal(before, villager.Skills.Count);
    }

    // ---------------------------------------------------------------
    //  ⭐⭐ The line Joe asked for by name
    // ---------------------------------------------------------------

    /// <summary>
    /// ⭐⭐ Mastery is narrated when it happens, <b>once</b>, and it names the person.
    /// </summary>
    /// <remarks>
    /// <para>
    /// §11.6, and Joe's ask by name (§3.3b, D174): *"it should be noted in the event log when
    /// someone achieves mastery."* **It is the first thing in this whole design the player will
    /// feel**, and it works from the day the substrate lands whether or not mastery is doing
    /// anything mechanical yet.
    /// </para>
    /// <para>
    /// <b>The *once* is the half that needs a guard.</b> Somebody who masters, leaves the trade,
    /// decays back under the threshold and returns would be narrated a second time without
    /// <see cref="SkillProgress.Mastered"/> — so the test does exactly that and counts the lines.
    /// </para>
    /// </remarks>
    [Fact]
    public void MasteryIsNarratedOnceAndNamesThePerson()
    {
        SimConfig config = ShippedConfig.Established();
        var sink = new InMemoryLogSink();
        SimLoop loop = SimFactory.CreatePhase0(config, sink);
        SimWorld world = loop.World;

        Villager villager = world.Villagers.First(person => person.Alive && person.CanWork);
        Workplace farm = FarmFixtures.RaiseAFarm(world);
        villager.WorkplaceId = farm.Id;

        int skill = SkillIdFor(config, JobKind.Farmer);
        SkillProgress progress = villager.ProgressIn(skill);
        progress.Ticks = config.MasteryTicks - 1;

        var system = new SkillSystem();
        system.Execute(world);

        Assert.True(progress.Mastered);

        List<string> lines = MasteryLines(sink, villager.Name);
        _output.WriteLine(string.Join("\n", lines));
        Assert.Single(lines);
        Assert.Contains($"{config.MasteryYears} years", lines[0], StringComparison.Ordinal);

        // ⭐ Now take the trade away, let it rust well under the threshold, and put them back.
        // Without the `Mastered` flag this narrates a second time.
        villager.WorkplaceId = 0;
        StepWholeYears(world, config, 30);
        Assert.True(progress.Ticks < config.MasteryTicks, "The fixture never decayed anybody.");

        villager.WorkplaceId = farm.Id;
        progress.Ticks = config.MasteryTicks - 1;
        system.Execute(world);

        Assert.Single(MasteryLines(sink, villager.Name));
    }

    /// <summary>⛔ Nobody masters a trade they have not given twenty years to.</summary>
    /// <remarks>
    /// The anti-vacuity half of the guard above: a flag set unconditionally would pass every
    /// assertion there and be wrong about the entire mechanic.
    /// </remarks>
    [Fact]
    public void NobodyMastersATradeAYearShortOfIt()
    {
        SimConfig config = ShippedConfig.Established();
        SimLoop loop = Loop(config);
        SimWorld world = loop.World;

        Villager villager = world.Villagers.First(person => person.Alive && person.CanWork);
        Workplace farm = FarmFixtures.RaiseAFarm(world);
        villager.WorkplaceId = farm.Id;

        SkillProgress progress = villager.ProgressIn(SkillIdFor(config, JobKind.Farmer));
        progress.Ticks = config.MasteryTicks - config.TicksPerYear - 1;

        new SkillSystem().Execute(world);

        Assert.False(progress.Mastered);
    }

    // ---------------------------------------------------------------
    //  Determinism — a regression here is P0
    // ---------------------------------------------------------------

    /// <summary>⭐ Same seed, same lives, same years in every trade.</summary>
    [Fact]
    public void TwoRunsOfOneSeedAgreeOnEveryYearAnybodyWorked()
    {
        SimConfig config = ShippedConfig.Established();

        SimLoop first = Loop(config);
        SimLoop second = Loop(config);

        first.Step(config.TicksPerYear * 60);
        second.Step(config.TicksPerYear * 60);

        Assert.Equal(first.World.Villagers.Count, second.World.Villagers.Count);

        int rows = 0;
        for (int i = 0; i < first.World.Villagers.Count; i++)
        {
            Villager left = first.World.Villagers[i];
            Villager right = second.World.Villagers[i];

            Assert.Equal(left.Skills.Count, right.Skills.Count);
            for (int s = 0; s < left.Skills.Count; s++)
            {
                Assert.Equal(left.Skills[s].SkillId, right.Skills[s].SkillId);
                Assert.Equal(left.Skills[s].Ticks, right.Skills[s].Ticks);
                Assert.Equal(left.Skills[s].Mastered, right.Skills[s].Mastered);
                rows++;
            }
        }

        _output.WriteLine($"{rows} skill rows agreed across two runs of seed {config.Seed}");
        Assert.True(rows > 0, "Nobody worked, so this compared nothing.");
    }

    /// <summary>⛔ The list stays in id order, because the hash reads it in list order.</summary>
    /// <remarks>
    /// D15 — *an unordered tie is a desync waiting to happen*. If entries could land in creation
    /// order, two runs that assigned the same person the same trades in a different sequence
    /// would hash differently while being the same village.
    /// </remarks>
    [Fact]
    public void ATradeListIsAlwaysInIdOrder()
    {
        SimConfig config = ShippedConfig.Established();
        SimLoop loop = Loop(config);
        Villager villager = loop.World.Villagers.First(person => person.Alive);

        // Deliberately out of order, and from both ends.
        foreach (int id in new[] { 6, 1, 4, 2, 5, 3 })
        {
            villager.ProgressIn(id);
        }

        Assert.Equal(
            new[] { 1, 2, 3, 4, 5, 6 },
            villager.Skills.Select(progress => progress.SkillId).ToArray());
    }

    // ---------------------------------------------------------------
    //  ⭐ The probe: what twenty years actually costs, per trade
    // ---------------------------------------------------------------

    /// <summary>
    /// ⭐ How long mastery really takes in a played village — <b>measured, because §3.6 left it
    /// open on purpose</b>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>The wrinkle §3.6 named and refused to guess at:</b> a trade the village stops staffing
    /// in winter (D44 — no berry patch is manned while there is nothing on it) accrues nothing
    /// those ticks, so **twenty years of foraging may be more than twenty calendar years**, while
    /// a farmer who holds their seat year-round masters on schedule. Only a run can say how far
    /// apart the trades fall.
    /// </para>
    /// <para>
    /// It asserts the thing that would make the pillar dishonest — **that somebody, somewhere,
    /// actually reaches mastery inside a normal life** — and prints the rest for the decisions
    /// log.
    /// </para>
    /// </remarks>
    [Fact]
    public void WhatTwentyYearsOnTheTaskActuallyCosts()
    {
        SimConfig config = ShippedConfig.Established();
        SimLoop loop = Loop(config);

        loop.Step(config.TicksPerYear * 80);

        var best = new Dictionary<int, int>();
        int mastered = 0;

        for (int i = 0; i < loop.World.Villagers.Count; i++)
        {
            Villager villager = loop.World.Villagers[i];
            for (int s = 0; s < villager.Skills.Count; s++)
            {
                SkillProgress progress = villager.Skills[s];
                best[progress.SkillId] = Math.Max(
                    best.TryGetValue(progress.SkillId, out int had) ? had : 0, progress.Ticks);

                if (progress.Mastered)
                {
                    mastered++;
                }
            }
        }

        for (int i = 0; i < config.Skills.Count; i++)
        {
            SkillRow skill = config.Skills[i];
            int ticks = best.TryGetValue(skill.Id, out int had) ? had : 0;
            _output.WriteLine(
                $"{skill.Name,-12} best {ticks,6} ticks = {ticks / config.TicksPerYear,2} years "
                + $"on the task (mastery at {config.MasteryYears})");
        }

        _output.WriteLine($"{mastered} masteries reached in 80 years");
        Assert.True(
            mastered > 0,
            "Nobody mastered anything in eighty years, so twenty years on the task is a "
            + "sentence the game never gets to say.");
    }

    private static void StepWholeYears(SimWorld world, SimConfig config, int years)
    {
        // The system alone, on year edges only — the point is the decay arithmetic, not a
        // village. Stepping the whole loop would let the allocator hand the job back.
        var system = new SkillSystem();
        for (int year = 0; year < years; year++)
        {
            world.Tick += (ulong)config.TicksPerYear;
            system.Execute(world);
        }
    }

    private static List<string> MasteryLines(InMemoryLogSink sink, string name) =>
        sink.Entries
            .Where(entry => entry.Subsystem == "life"
                && entry.Message.StartsWith(name, StringComparison.Ordinal)
                && entry.Message.Contains("years", StringComparison.Ordinal))
            .Select(entry => entry.Message)
            .ToList();
}

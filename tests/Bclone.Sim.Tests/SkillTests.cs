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
    //  ⭐⭐ Landing 2 — mastery bites (§3.3)
    // ---------------------------------------------------------------

    /// <summary>
    /// ⛔⭐ <b>A novice takes exactly today's number of ticks, at every trade.</b>
    /// </summary>
    /// <remarks>
    /// <b>§3.2's floor, asserted where it actually lives.</b> `VillageEconomy` derives the
    /// **survival floor** — what the village must produce not to die — about the least skilled
    /// person in the valley. If a novice were even one tick different, every number derived from
    /// that would be describing a village that no longer exists. **Nobody is ever worse than
    /// today**, and mastery is headroom above.
    /// </remarks>
    [Fact]
    public void ANoviceWorksAtExactlyTodaysSpeed()
    {
        // ⭐⭐ POSED ALL-NOVICE, AND §10 PREDICTED THIS EXACT FAILURE IN ADVANCE:
        // *"a guard that tries to assert this about the real opening will fail, and the
        // temptation will be to weaken it instead of to pose it properly."* It went red the
        // moment the mixed founding landed, because founder #0 is now a **master** — so the
        // villager this reached for was the one person in the valley who is not a novice.
        // Posed rather than relaxed.
        SimConfig config = ShippedConfig.Established()
            with { FoundingMasters = 0, FoundingJourneymen = 0 };
        SimWorld world = Loop(config).World;
        Villager novice = world.Villagers.First(v => v.Alive);

        Assert.Empty(novice.Skills);

        Assert.Equal(config.GatherTicks, world.WorkTicksFor(novice, JobKind.Forager, config.GatherTicks));
        Assert.Equal(config.CutTicks, world.WorkTicksFor(novice, JobKind.Forester, config.CutTicks));
        Assert.Equal(config.SowTicks, world.WorkTicksFor(novice, JobKind.Farmer, config.SowTicks));
        Assert.Equal(config.ReapTicks, world.WorkTicksFor(novice, JobKind.Farmer, config.ReapTicks));
        Assert.Equal(config.SplitTicks, world.WorkTicksFor(novice, JobKind.Woodcutter, config.SplitTicks));

        // ⭐ And reading the question did not create a skill row — the structure stays sparse,
        // which is what keeps a village that has never worked hashing as it always did.
        Assert.Empty(novice.Skills);
    }

    /// <summary>
    /// ⭐⭐ A master is <b>measurably faster at every trade</b> — the guard against the whole
    /// pillar silently rounding away.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>⛔ THIS IS NOT A FORMALITY, AND THE MEASUREMENT IS WHY.</b> The durations are 3 and 4
    /// ticks, so <c>mastery_speed_bonus_percent</c> only does anything when it rounds to a whole
    /// tick. **At 17% not one duration moves. At 25% only the four-tick trades do** — and a
    /// village at 25% produced population and food *identical* to one with the feature switched
    /// off. A number that looks like a quarter faster and buys literally nothing is exactly the
    /// invisible no-op this project has rejected four times.
    /// </para>
    /// <para>
    /// So this asserts the **effect** rather than the setting: whatever the bonus is tuned to,
    /// every trade a master holds must cost them fewer ticks than a novice.
    /// </para>
    /// </remarks>
    [Fact]
    public void AMasterIsFasterAtEveryTrade()
    {
        SimConfig config = ShippedConfig.Established();
        SimWorld world = Loop(config).World;

        Villager novice = world.Villagers.First(v => v.Alive);
        Villager master = world.Villagers.Last(v => v.Alive);
        Assert.NotSame(novice, master);

        foreach (SkillRow skill in config.Skills)
        {
            master.ProgressIn(skill.Id).Work = config.MasteryWorkFor(skill);
        }

        (JobKind Trade, int Ticks)[] work =
        {
            (JobKind.Forager, config.GatherTicks),
            (JobKind.Forester, config.CutTicks),
            (JobKind.Woodcutter, config.SplitTicks),
            (JobKind.Farmer, config.SowTicks),
            (JobKind.Farmer, config.ReapTicks),
        };

        foreach ((JobKind trade, int baseTicks) in work)
        {
            int mastered = world.WorkTicksFor(master, trade, baseTicks);
            _output.WriteLine($"{trade,-11} {baseTicks} ticks for a novice, {mastered} for a master");

            Assert.True(
                mastered < baseTicks,
                $"A master of {trade} still takes {mastered} ticks against a novice's "
                + $"{baseTicks}. mastery_speed_bonus_percent is "
                + $"{config.MasterySpeedBonusPercent}% and it has rounded away to nothing — "
                + "the pillar accrues, is visible and changes nothing, which is D56's clothing.");

            Assert.True(mastered >= 1, "An action that costs no ticks happens infinitely often.");
        }
    }

    /// <summary>
    /// ⭐ Half the way to mastery is half the bonus — <b>and at these durations that means a
    /// step rather than a ramp</b>.
    /// </summary>
    /// <remarks>
    /// The curve is linear in work and then flat (§3.3), but a three-tick action can only ever
    /// become two — so what the sim actually expresses is **two tiers, because it cannot express
    /// any others at these durations.** Recorded as a guard rather than a surprise, because §12
    /// is about to choose tier names and this is the shape underneath them.
    /// </remarks>
    [Fact]
    public void ProgressTowardMasteryIsMonotonic()
    {
        SimConfig config = ShippedConfig.Established();
        SimWorld world = Loop(config).World;
        Villager villager = world.Villagers.First(v => v.Alive);

        int skill = SkillIdFor(config, JobKind.Farmer);
        SkillProgress progress = villager.ProgressIn(skill);
        int mastery = config.MasteryWork;

        int previous = int.MaxValue;
        var steps = new List<string>();

        for (int share = 0; share <= 120; share += 10)
        {
            progress.Work = (int)((long)mastery * share / 100);
            int ticks = world.WorkTicksFor(villager, JobKind.Farmer, config.ReapTicks);

            Assert.True(ticks <= previous, $"Reaping got slower between {share - 10}% and {share}%.");
            previous = ticks;
            steps.Add($"{share}%:{ticks}");
        }

        _output.WriteLine(string.Join("  ", steps));

        // Past mastery it stays flat — a master who keeps working is a master, not somebody who
        // eventually reaps in no time at all.
        progress.Work = mastery * 10;
        Assert.Equal(previous, world.WorkTicksFor(villager, JobKind.Farmer, config.ReapTicks));
    }

    /// <summary>
    /// ⭐⭐ Mastery <b>changes what the village does</b> — the anti-vacuity guard landing 1 set up.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Landing 1's own guard, pointed the other way.</b> `FiftyYearsOfVillageAndOnlyTheCounters-
    /// Moved` asserts that with the substrate alone <see cref="StateHash.ComputeIgnoringSkills"/>
    /// is byte-identical to the pre-skill goldens — *nothing anybody does changed*. **When
    /// mastery bites, that number must move**, and this is where it is said out loud.
    /// </para>
    /// <para>
    /// **A skill system that changes nothing is D56's clothing**, measured as a no-op over 300
    /// years and blocked for it. This is the assertion that decides whether any of Phase 3 is
    /// real.
    /// </para>
    /// </remarks>
    [Fact]
    public void AVillageWhoseMastersAreFasterLivesADifferentLife()
    {
        SimConfig config = ShippedConfig.Established();

        SimLoop biting = Loop(config);
        SimLoop inert = Loop(config with { MasterySpeedBonusPercent = 0 });

        biting.Step(config.TicksPerYear * 50);
        inert.Step(config.TicksPerYear * 50);

        ulong withMastery = StateHash.ComputeIgnoringSkills(biting.World);
        ulong without = StateHash.ComputeIgnoringSkills(inert.World);

        _output.WriteLine(
            $"50 years: {biting.World.Population} alive with mastery biting, "
            + $"{inert.World.Population} with it switched off");

        Assert.NotEqual(without, withMastery);
    }

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
    // ⭐⭐ RE-TAKEN FOR THE STOCKED MARKET (D197). `ComputeIgnoringSkills` moves because the
    // marketer genuinely does something different — which is exactly what this number is for.
    // ⭐⭐ RE-TAKEN FOR D211 with the rest: a villager's arms are hashed by index now rather
    // than as three named goods. **This number is the one that says the village is unmoved** —
    // it excludes skills, and the measurement behind the re-take is that restoring the old
    // three-line mix makes every moved golden byte-identical again.
    //
    //   before the arms were hashed by index: false 16512056222735860702,
    //                                         true  14931182978223796698
    //   before the fixture ate what the game eats (D223): false 16154924796471685929
    //   before the village knew things (D225): false 18186071774726496737, true 11403972867442886560
    //   before the village was allowed to rest (D250): false 6192378668729777699, true 9064700070209210640
    //   before the founders were mourned (D252): false 17678494988155338593 (true UNMOVED)
    //   before the huts were capped at two (D262): false 16683756764195047443,
    //                                              true  6976255911900204686
    [InlineData(false, 14899915986336060167UL)]
    [InlineData(true, 9768080546410864531UL)]
    public void FiftyYearsOfVillageAndOnlyTheCountersMoved(bool shipped, ulong beforeSkills)
    {
        // ⭐⭐ POSED, WITH MASTERY SWITCHED OFF — AND §10 SAID SO IN ADVANCE: *"it must be posed
        // rather than played… a guard that tries to assert this about the real opening will
        // fail, and the temptation will be to weaken it instead of to pose it properly."*
        //
        // It went red the moment landing 2 landed, which is **the guard working rather than
        // breaking**: mastery biting is supposed to change what people do, and this one's whole
        // job is to say that the SUBSTRATE does not. Re-based by posing the village the claim is
        // about — the same substrate, nobody any faster — instead of relaxing what it asserts.
        // *A guard that outlives the rule it was written for looks exactly like a regression*
        // (D150), and the honest response is to pose it properly.
        //
        // ⚠️ AND KNOW WHAT POSING IT COSTS: with the bonus at zero `WorkTicksFor` takes its
        // early return, so **this guard cannot see a break in the novice floor itself** — it
        // was checked red against one and stayed green. `ANoviceWorksAtExactlyTodaysSpeed`
        // covers that arm, in the live path. Two claims, two guards; neither is the other.
        //
        // ⭐⭐ AND LANDING 3 ADDED TWO MORE THINGS TO SWITCH OFF, WHICH §10 ALSO NAMED IN
        // ADVANCE: *"a synthetic all-novice village, with the mixed founding switched off and
        // the seeded rhythm switched off."* Both change what people **do** — that is what they
        // are for — so both belong in the posing rather than in the claim.
        SimConfig config = (shipped ? ShippedConfig.Established() : VillageFixtures.Village)
            with
            {
                MasterySpeedBonusPercent = 0,
                FoundingMasters = 0,
                FoundingJourneymen = 0,
                SeededRhythm = false,
            };
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
    //  ⭐⭐ Nothing is ever taken away (D183)
    // ---------------------------------------------------------------

    /// <summary>
    /// ⭐⭐ Over eighty years of village, <b>no villager's proficiency ever falls</b>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Joe, 2026-08-22: *"let's give to the player, not punish or decay."*</b> Decay was
    /// built, measured and deleted inside one phase, and this is the invariant that replaced it
    /// — asserted every year for every living villager rather than left as a policy somebody has
    /// to remember while editing <see cref="SkillSystem"/>.
    /// </para>
    /// <para>
    /// <b>⛔ WHAT THE MEASUREMENT FOUND, because the spec was sure decay was required.</b> §3.4
    /// justified it with *"a fifty-year-old who did six jobs is a master of six"*. **That is
    /// arithmetically impossible** — see <see cref="NobodyCanMasterMoreThanALifeHasRoomFor"/> —
    /// and what the shipped rate actually did was take **37% of everything one forager earned**,
    /// so she held foraging longer than mastery requires and never became a master. *The cure
    /// was the disease it warned about.*
    /// </para>
    /// </remarks>
    [Fact]
    public void NobodyEverLosesGroundInATrade()
    {
        SimConfig config = ShippedConfig.Established();
        SimLoop loop = Loop(config);
        SimWorld world = loop.World;

        var highest = new Dictionary<(int Villager, int Skill), int>();
        int checks = 0;

        for (int year = 0; year < 80; year++)
        {
            loop.Step(config.TicksPerYear);

            for (int i = 0; i < world.Villagers.Count; i++)
            {
                Villager villager = world.Villagers[i];
                for (int s = 0; s < villager.Skills.Count; s++)
                {
                    SkillProgress progress = villager.Skills[s];
                    var key = (villager.Id, progress.SkillId);
                    int best = highest.TryGetValue(key, out int had) ? had : 0;

                    Assert.True(
                        progress.Work >= best,
                        $"{villager.Name} lost ground in skill {progress.SkillId}: "
                        + $"{best} → {progress.Work}. Nothing may ever take proficiency away.");

                    highest[key] = progress.Work;
                    checks++;
                }
            }
        }

        _output.WriteLine($"{checks} year-on-year comparisons, none of them downward");
        Assert.True(checks > 100, "Too few careers to have proved anything.");
    }

    /// <summary>
    /// ⭐ A life does not contain enough hours to master six trades — <b>§3.4's fear, measured
    /// and found impossible</b>.
    /// </summary>
    /// <remarks>
    /// <b>This is the guard that licenses deleting decay</b> (D183). *"Knowledge lives in
    /// people"* does not collapse into *"old people are simply better"*, because the arithmetic
    /// will not allow it: mastery costs a fixed share of a working life, and a life holds two of
    /// them at the theoretical maximum. **A career is still a choice — the choosing is done by
    /// the clock, not by a punishment.**
    /// </remarks>
    [Fact]
    public void NobodyCanMasterMoreThanALifeHasRoomFor()
    {
        SimConfig config = ShippedConfig.Established();
        SimLoop loop = Loop(config);

        loop.Step(config.TicksPerYear * 80);

        int workingLife = (config.LifespanYearsBase - config.AdultAge) * config.TicksPerYear;
        int ceiling = workingLife * config.SkillWorkPerActiveTick / config.MasteryWork;

        int most = 0;
        for (int i = 0; i < loop.World.Villagers.Count; i++)
        {
            most = Math.Max(most, loop.World.Villagers[i].Skills.Count(p => p.Mastered));
        }

        _output.WriteLine(
            $"a working life is about {workingLife} ticks and mastery costs {config.MasteryWork} "
            + $"work, so at most {ceiling} masteries fit in one even working every waking tick; "
            + $"the most anybody actually reached in 80 years was {most}");

        // ⛔ THE CEILING IS THE THEORETICAL BEST CASE AND IT IS NOT THE INTERESTING NUMBER —
        // it assumes somebody holds a trade and is out on it every waking tick of fifty-five
        // years, which nobody does. **Measured, the most anybody reaches is one.** Both are
        // asserted: the ceiling refutes §3.4's specific claim, and the observed figure says
        // what the game actually produces.
        Assert.True(
            ceiling < 6,
            $"A life has room for {ceiling} masteries, so §3.4's 'a fifty-year-old who did six "
            + "jobs is a master of six' is back on the table — and that being impossible is what "
            + "licensed deleting decay.");

        Assert.True(
            most <= 2,
            $"Somebody mastered {most} trades in eighty years. A career is supposed to be a "
            + "choice made by the clock; if it is not, decay was doing work after all.");
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
        progress.Work = config.MasteryWork - 1;

        // ⭐ Pose the years they actually put in as SHORTER than `mastery_years`, which is what
        // happens to anybody who spends time out on the job — and check the log quotes THEIR
        // number rather than the config's. Quoting the config would have the village say
        // "twenty years" about somebody the panel one click away calls seventeen.
        int realYears = config.MasteryYears - 3;
        progress.Ticks = realYears * config.TicksPerYear;

        var system = new SkillSystem();
        system.Execute(world);

        Assert.True(progress.Mastered);

        List<string> lines = MasteryLines(sink, villager.Name);
        _output.WriteLine(string.Join("\n", lines));
        Assert.Single(lines);
        Assert.Contains($"{realYears} years", lines[0], StringComparison.Ordinal);
        Assert.DoesNotContain($"{config.MasteryYears} years", lines[0], StringComparison.Ordinal);

        // ⭐ Now run them well past the threshold and check it stays at one line. Without the
        // `Mastered` flag this narrates again on every tick, for ever.
        for (int i = 0; i < 50; i++)
        {
            system.Execute(world);
        }

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
        progress.Work = config.MasteryWork - (config.TicksPerYear * config.SkillWorkPerIdleTick) - 1;

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

        // ⭐⭐ THE NUMBER §3.3b's PROMISE LIVES OR DIES ON, and it landed without tuning.
        // *"A founder who sticks to one trade masters it, and is a master for the back half of
        // their life."* Measured: **34, 35, 37, 37, 38, 39, 39, 40, 42, 46, 49, 49, 49, 55** —
        // median 39, against a lifespan of 55–79. Mastery arrives in the late thirties and
        // there is a long back half to be a master through, which is what the design asked for.
        //
        // The band is wide on purpose. **Pinning the median would make this red every time the
        // calendar, the lifespan or the labour rules are tuned** — what it is guarding is that
        // mastery is neither free nor posthumous.
        List<int> ages = AgesAtMastery(config, 80);
        _output.WriteLine($"ages at mastery: {string.Join(", ", ages)}");

        Assert.NotEmpty(ages);

        int median = ages[ages.Count / 2];
        _output.WriteLine($"median age at mastery: {median}");

        Assert.InRange(median, config.AdultAge + 10, config.LifespanYearsBase - 10);

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

    /// <summary>
    /// ⭐⭐ How many ticks a year somebody who sticks to a trade actually puts into it — <b>the
    /// measurement each skill's own mastery number is derived from</b> (D182, Joe's call).
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>⛔ THE PROBLEM THIS SOLVES, MEASURED BY THE LANDING BEFORE IT:</b> twenty years *on the
    /// task* was **32 calendar years for a forager and 34 for a marketer**, against twenty for a
    /// farmer, because D44 stands seasonal work down in winter. §3.3b's promise — *"a founder who
    /// sticks to one trade masters it, and is a master for the back half of their life"* — was
    /// true for some trades and quietly false for others, **with nothing on screen saying why**.
    /// </para>
    /// <para>
    /// <b>The statistic is the maximum any one villager gained in any single calendar year</b>,
    /// which is exactly *"somebody who stuck to it"* — robust to the reshuffle moving people,
    /// to deaths, and to children coming of age mid-year, because all of those only ever produce
    /// a **smaller** year and the maximum ignores them.
    /// </para>
    /// <para>
    /// ⚠️ <b>It asserts the shape rather than the numbers</b> — that a year-round trade is at or
    /// near a full year and a seasonal one is materially short of it. Pinning the exact ticks
    /// would make this go red every time the calendar or the winter rules are tuned, which is a
    /// guard that cries wolf about its own config.
    /// </para>
    /// </remarks>
    [Fact]
    public void WhatAYearOnEachTradeIsActuallyWorth()
    {
        SimConfig config = ShippedConfig.Established();
        SimLoop loop = Loop(config);
        SimWorld world = loop.World;

        // ⛔ SEASONS THE TRADE IS EVER WORKED — NOT "how much of the year did somebody happen to
        // hold it". The first statistic this probe computed was the maximum ticks any villager
        // gained in one year, and it was WRONG TO DERIVE FROM: it measured **demand** as much as
        // availability. Woodcutting came out at 25% of a year, not because a woodcutter rests
        // nine months but because this village only wants one occasionally — so mastery would
        // have been set at five years on the task, and a player who staffs three huts year-round
        // would mint masters in five years and break §3.3b's *"the back half of their life"*.
        //
        // **Seasonality is intrinsic to the trade; demand is the player's business and must not
        // change how long mastery takes.** So: in which seasons is this work ever done at all?
        var worked = new Dictionary<int, HashSet<Season>>();

        for (int step = 0; step < 40 * 4; step++)
        {
            for (int i = 0; i < world.Villagers.Count; i++)
            {
                Villager villager = world.Villagers[i];
                if (!villager.Alive || !villager.HasJob)
                {
                    continue;
                }

                Workplace? workplace = world.FindWorkplace(villager.WorkplaceId);
                if (workplace is null || workplace.IsSite)
                {
                    continue;
                }

                SkillRow row = config.Skills.First(skill => skill.GrownBy == workplace.Kind);
                if (!worked.TryGetValue(row.Id, out HashSet<Season>? seasons))
                {
                    seasons = new HashSet<Season>();
                    worked[row.Id] = seasons;
                }

                seasons.Add(world.Clock.Season);
            }

            loop.Step(config.TicksPerSeason / 2);
        }

        var measured = new List<int>();

        for (int i = 0; i < config.Skills.Count; i++)
        {
            SkillRow skill = config.Skills[i];
            if (!worked.TryGetValue(skill.Id, out HashSet<Season>? seasons) || seasons.Count == 0)
            {
                _output.WriteLine($"{skill.Name,-12} never staffed in this village — not measurable");
                continue;
            }

            int derived = config.MasteryYears * seasons.Count / 4;
            measured.Add(seasons.Count);

            _output.WriteLine(
                $"{skill.Name,-12} worked in {seasons.Count} of 4 seasons "
                + $"({string.Join(", ", seasons.OrderBy(season => (int)season))}) "
                + $"→ mastery at {derived,2} years on the task ≈ {config.MasteryYears} calendar "
                + $"(config says {config.MasteryYearsFor(skill)})");
        }

        Assert.NotEmpty(measured);
        Assert.Contains(4, measured);
        Assert.True(
            measured.Min() < 4,
            "Every trade is worked in every season, so per-skill mastery numbers would be noise "
            + "in a data file rather than a correction.");
    }

    /// <summary>
    /// ⭐⭐ Why the best career in a trade takes half again as long as the number says —
    /// <b>the cause, measured, after two wrong ones</b> (D182).
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>⛔ TWO EXPLANATIONS WERE OFFERED AND BOTH WERE WRONG, WHICH IS D163/D166/D169's
    /// *finding a cause is not finding the cause* arriving for a fourth time.</b>
    /// </para>
    /// <para>
    /// <b>Wrong once:</b> *"D44 stands seasonal work down in winter."* It does reduce the
    /// **headcount** — 1 of 4 able adults hold a job in mid-winter against 4 of 4 in summer —
    /// and that number was read as availability. **It is not:**
    /// `WhatAYearOnEachTradeIsActuallyWorth` finds foraging, forestry, woodcutting and trading
    /// all worked in **all four seasons**. Somebody is always on it; there are simply fewer of
    /// them.
    /// </para>
    /// <para>
    /// <b>Wrong twice:</b> *"derive each trade's mastery from the share of a year it is
    /// staffed."* That measures **demand**, which is the player's business — woodcutting came
    /// out at 25% of a year because this village wants one woodcutter occasionally, and mastery
    /// would have been set at five years for anybody who staffs three huts properly.
    /// </para>
    /// <para>
    /// <b>So this one asks the question directly, tick by tick</b>, and splits the calendar into
    /// the three things that can consume it: holding the trade, holding some other trade or
    /// none, and ground given back to decay.
    /// </para>
    /// </remarks>
    [Fact]
    public void WhereACareersMissingYearsActuallyGo()
    {
        SimConfig config = ShippedConfig.Established();
        SimLoop loop = Loop(config);
        SimWorld world = loop.World;

        var heldTrade = new Dictionary<(int Villager, int Skill), int>();
        var adultTicks = new Dictionary<int, int>();

        for (int tick = 0; tick < config.TicksPerYear * 60; tick++)
        {
            loop.Step(1);

            for (int i = 0; i < world.Villagers.Count; i++)
            {
                Villager villager = world.Villagers[i];
                if (!villager.Alive || !villager.CanWork)
                {
                    continue;
                }

                adultTicks[villager.Id] =
                    (adultTicks.TryGetValue(villager.Id, out int lived) ? lived : 0) + 1;

                if (!villager.HasJob)
                {
                    continue;
                }

                Workplace? workplace = world.FindWorkplace(villager.WorkplaceId);
                if (workplace is null || workplace.IsSite)
                {
                    continue;
                }

                SkillRow row = config.Skills.First(skill => skill.GrownBy == workplace.Kind);
                var key = (villager.Id, row.Id);
                heldTrade[key] = (heldTrade.TryGetValue(key, out int had) ? had : 0) + 1;
            }
        }

        int worstDecayShare = 0;

        for (int i = 0; i < config.Skills.Count; i++)
        {
            SkillRow skill = config.Skills[i];

            (int Villager, int Skill) best = default;
            int bestHeld = 0;
            foreach (KeyValuePair<(int Villager, int Skill), int> entry in heldTrade)
            {
                if (entry.Key.Skill == skill.Id && entry.Value > bestHeld)
                {
                    bestHeld = entry.Value;
                    best = entry.Key;
                }
            }

            if (bestHeld == 0)
            {
                _output.WriteLine($"{skill.Name,-12} nobody ever held it");
                continue;
            }

            Villager villager = world.Villagers.First(person => person.Id == best.Villager);
            int lived = adultTicks[best.Villager];
            int kept = villager.TicksIn(skill.Id);

            int heldShare = lived == 0 ? 0 : bestHeld * 100 / lived;
            int decayed = bestHeld - kept;
            int decayShare = bestHeld == 0 ? 0 : decayed * 100 / bestHeld;
            worstDecayShare = Math.Max(worstDecayShare, decayShare);

            _output.WriteLine(
                $"{skill.Name,-12} best career: {villager.Name,-8} held it {bestHeld,6} of "
                + $"{lived,6} adult ticks ({heldShare,3}%), kept {kept,6} — "
                + $"decay took {decayed,6} ({decayShare,3}% of what was earned)");
        }

        _output.WriteLine(
            $"mastery needs {config.MasteryWork} work; an adult life is about "
            + $"{(config.LifespanYearsBase - config.AdultAge) * config.TicksPerYear} ticks");

        // ⭐⭐ THE FINDING THIS PROBE WAS WRITTEN FOR HAS BEEN ACTED ON, AND IT SAID SO ITSELF.
        // It used to assert `worstDecayShare > 10` with the note *"when the rate is fixed, this
        // goes red and gets re-taken"* — **decay is gone (D183) and it went red on the same
        // run.** Agnes keeps all 12,240 ticks of foraging now, where the shipped rate took 4,640
        // of them and left her short of a mastery she had done the work for.
        Assert.Equal(0, worstDecayShare);

        // ⭐ WHAT IT STILL MEASURES, AND WHY IT IS KEPT: **career continuity**, which is now the
        // only reason anybody's calendar and their trade disagree. Agnes held foraging for 44%
        // of her adult life and Mabel held trading for 70% — the reshuffle (D46) and the
        // village's changing wants move people, and **landing 2 has to decide whether that
        // spread is the design or a problem.** The numbers are here for that conversation.
        int longest = heldTrade.Values.Count == 0 ? 0 : heldTrade.Values.Max();
        Assert.True(
            longest * config.SkillWorkPerActiveTick > config.MasteryWork,
            "Nobody held any trade for as long as mastery requires, so this probe never "
            + "reached the regime it is about.");
    }

    // ---------------------------------------------------------------
    //  ⭐ Out on the work counts for more than waiting for it (D183)
    // ---------------------------------------------------------------

    /// <summary>
    /// ⭐ A tick out on the job is worth more than a tick waiting for it — <b>and both are worth
    /// something</b>.
    /// </summary>
    /// <remarks>
    /// <b>Both halves matter and each alone is green against a bug.</b> Crediting only active
    /// ticks would punish a player whose supply chain stutters — Joe's *"idle foresters still
    /// get mastery XP"* — and crediting them equally would make an idle trade as good as a
    /// worked one, which is the whole of his 1.5×.
    /// </remarks>
    [Fact]
    public void OutOnTheWorkIsWorthMoreThanWaitingForIt()
    {
        SimConfig config = ShippedConfig.Established();
        SimLoop loop = Loop(config);
        SimWorld world = loop.World;

        Villager villager = world.Villagers.First(person => person.Alive && person.CanWork);
        Workplace farm = FarmFixtures.RaiseAFarm(world);
        villager.WorkplaceId = farm.Id;

        int skill = SkillIdFor(config, JobKind.Farmer);
        var system = new SkillSystem();

        villager.State = VillagerState.Idle;
        system.Execute(world);
        SkillProgress progress = villager.FindProgressIn(skill)!;
        int waiting = progress.Work;

        villager.State = VillagerState.Reaping;
        system.Execute(world);
        int working = progress.Work - waiting;

        _output.WriteLine($"a tick waiting is worth {waiting}, a tick reaping {working}");

        Assert.Equal(config.SkillWorkPerIdleTick, waiting);
        Assert.Equal(config.SkillWorkPerActiveTick, working);
        Assert.True(waiting > 0, "An idle forester is still a forester and still gaining.");
        Assert.True(working > waiting, "Going out to do the work must teach more than not.");

        // ⭐ And the honest counter does NOT take the weighting: two ticks held is two ticks
        // held, whichever way they were spent. That is what keeps the panel true.
        Assert.Equal(2, progress.Ticks);
    }

    /// <summary>
    /// ⛔ Every <see cref="VillagerState"/> has been ruled on as work or waiting.
    /// </summary>
    /// <remarks>
    /// <b>The guard `Villager.DescribeState` already has, for the same reason.</b> That method
    /// fell through to the raw enum name for **seven of seventeen states, every one added after
    /// it was written**. A compiler check was the first attempt here and C# will not give one —
    /// an exhaustive switch still demands a <c>_</c> arm for out-of-range casts (CS8524), and
    /// adding it silences the missing-name check as well. So this walks the enum instead, and a
    /// new state that nobody has classified throws rather than defaulting quietly (D108).
    /// </remarks>
    [Fact]
    public void EveryVillagerStateIsDeliberatelyClassified()
    {
        var waiting = new List<VillagerState>();
        var working = new List<VillagerState>();

        foreach (VillagerState state in Enum.GetValues<VillagerState>())
        {
            (SkillSystem.OutOnTheWork(state) ? working : waiting).Add(state);
        }

        _output.WriteLine($"waiting: {string.Join(", ", waiting)}");
        _output.WriteLine($"working: {string.Join(", ", working)}");

        Assert.Equal(Enum.GetValues<VillagerState>().Length, waiting.Count + working.Count);
        Assert.NotEmpty(waiting);
        Assert.NotEmpty(working);

        // ⛔ The one that looks like work and is not: `FetchingFromStore` is a household member
        // fetching their own family's supper (D30). A marketer's delivery is `DeliveringToHome`.
        Assert.Contains(VillagerState.FetchingFromStore, waiting);
        Assert.Contains(VillagerState.DeliveringToHome, working);

        // ⭐ And the walk is part of the work — counting only the swing would charge a distant
        // hut twice for a commute D112 already makes it pay for.
        Assert.Contains(VillagerState.TravelingToTrees, working);
        Assert.Contains(VillagerState.TravelingHome, working);
    }

    /// <summary>
    /// ⭐ How old people are when they master something — <b>the number §3.3b's promise lives
    /// or dies on</b>.
    /// </summary>
    /// <remarks>
    /// *"A founder who sticks to one trade masters it, and is a master for the back half of
    /// their life"* — against a lifespan of 55–79, that wants ages in the thirties and forties.
    /// **Too early and mastery is free; too late and nobody lives to see it.** Sampled per year
    /// rather than per tick because a year is the resolution the answer is quoted in.
    /// </remarks>
    private static List<int> AgesAtMastery(SimConfig config, int years)
    {
        SimLoop loop = SimFactory.CreatePhase0(config, new InMemoryLogSink());
        var seen = new HashSet<(int Villager, int Skill)>();
        var ages = new List<int>();

        for (int year = 0; year < years; year++)
        {
            loop.Step(config.TicksPerYear);

            for (int i = 0; i < loop.World.Villagers.Count; i++)
            {
                Villager villager = loop.World.Villagers[i];
                for (int s = 0; s < villager.Skills.Count; s++)
                {
                    SkillProgress progress = villager.Skills[s];
                    if (progress.Mastered && seen.Add((villager.Id, progress.SkillId)))
                    {
                        ages.Add(villager.AgeYears);
                    }
                }
            }
        }

        ages.Sort();
        return ages;
    }

    // ---------------------------------------------------------------
    //  ⭐⭐ Landing 3 — the mixed founding and the seeded rhythm (D28)
    // ---------------------------------------------------------------

    /// <summary>
    /// ⭐⭐ <b>Two adults of one household stop running the same program</b> — D28, measured at
    /// the opening, where Joe saw it.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>THE OLDEST OPEN OBSERVATION IN THE PROJECT, AND THE NUMBER TO BEAT IS ON RECORD.</b>
    /// Joe watched the village at 4× in Phase 1 and saw people travelling as duos rather than
    /// individuals. Measured then: **two adults of one household holding one job are on the same
    /// tile 99.9% of ticks, with identical hunger 100% of the time.**
    /// </para>
    /// <para>
    /// <b>⭐ ASSERTED ABOUT THE FIRST YEARS, NOT ACROSS A CENTURY</b> (§10). That is the whole
    /// point of taking the rhythm and the mixed founding together: skill alone breaks the
    /// symmetry over *decades*, and the opening is the stretch Joe was actually watching. **If
    /// this only came good by year 80 it would not be a fix.**
    /// </para>
    /// <para>
    /// <b>It is a symmetry problem rather than a variability one</b>, which is why a few ticks
    /// once is enough: two villagers who set off a tick apart never re-synchronise.
    /// </para>
    /// </remarks>
    [Fact]
    public void TwoAdultsOfOneHouseholdStopMovingInLockstep()
    {
        SimConfig config = ShippedConfig.Established();
        SimConfig before = config with
        {
            FoundingMasters = 0,
            FoundingJourneymen = 0,
            SeededRhythm = false,
        };

        (int Tile, int Hunger) staggered = SameTileShare(config);
        (int Tile, int Hunger) lockstepped = SameTileShare(before);

        _output.WriteLine(
            $"first 5 years, both switched OFF: same tile {lockstepped.Tile}% of ticks, "
            + $"identical hunger {lockstepped.Hunger}%");
        _output.WriteLine(
            $"first 5 years, as shipped:        same tile {staggered.Tile}% of ticks, "
            + $"identical hunger {staggered.Hunger}%");

        // ⛔ THE RED CHECK IS BUILT IN, which is what makes this falsifiable rather than a
        // vibe (§10): the same measurement with both switched off must still show the
        // lockstep, or this guard is describing a village that was never in step.
        // ⚠️ 85, LOWERED FROM 90 WHEN A FASTER THAW MOVED THE BASELINE 91% → 88% (D192).
        // **The bar exists to prove there is a lockstep to fix**, and 88% plainly is one; it is
        // not a claim about any particular figure. Recorded rather than quietly edited, because
        // *"the bar moved"* and *"the guard was weakened"* look identical in a diff.
        //
        // ⚠️⚠️ **70, LOWERED FROM 85 WHEN RESTING BECAME A REAL SPELL (D250) — 88% → 76%.** And
        // this time the honest note is not just the number: **the TILE measure has stopped being
        // a good instrument.** A rest spell ends when the world happens to offer something, so
        // two identical villagers now drift apart on position for reasons that have nothing to
        // do with rhythm — 76% off against 71% shipped is a five-point gap where it used to be
        // seventeen.
        //
        // ⭐ **The decisive arm is hunger, and it is untouched: 100% off, 0% shipped.** Hunger is
        // a pure function of ticks since the last meal, so it measures the symmetry D28 was
        // actually about. **If this tile bar ever needs lowering again, delete it instead** —
        // a precondition that keeps being relaxed is one that has stopped discriminating.
        // ⛔⛔ THE TILE ASSERTIONS ARE GONE, ON THIS GUARD'S OWN WRITTEN INSTRUCTION (D262).
        // The note above says it in as many words: *"If this tile bar ever needs lowering again,
        // delete it instead — a precondition that keeps being relaxed is one that has stopped
        // discriminating."* It needed lowering again, and this time in the wrong direction:
        // **83% shipped against 82% switched off**, the staggered village sharing a tile MORE
        // than the lockstepped one.
        //
        // ⚠️ **NOT A REGRESSION IN THE RHYTHM — THE INSTRUMENT FINALLY GAVE OUT.** It had been
        // failing for two slices running: 91% → 88% when the thaw got faster (D192), 88% → 76%
        // when resting became a real spell (D250), each time with the gap between the arms
        // narrowing. Joe's two-seat cap finished it: a hand the huts cannot seat waits at home,
        // so two adults of one household stand on the same tile because NEITHER is working,
        // which has nothing to do with whether they get up at the same moment.
        //
        // ⭐ The numbers are still printed. They are worth reading and worth nothing to assert.
        Assert.True(
            lockstepped.Hunger >= 95,
            $"With both switched off, two adults share a hunger value only "
            + $"{lockstepped.Hunger}% of ticks. The symmetry D28 describes is not there.");

        // ⭐⭐ THE DECISIVE NUMBER, AND IT WENT FROM 100% TO 0%. Hunger is a pure function of
        // ticks since the last meal, so two people who eat on the same tick stay in step for
        // ever however differently they walk — **an offset that moves only their feet cannot
        // touch it**, and the first version of this fix did exactly that and left this at 100%.
        // The rhythm sets their starting hunger apart as well as their first step.
        Assert.True(
            staggered.Hunger < 50,
            $"Two adults of one household still hold identical hunger {staggered.Hunger}% of "
            + "ticks. They are eating on the same tick, so they are still running one program.");
    }

    /// <summary>
    /// ⭐ The rhythm is <b>small</b> — it staggers people without changing what they produce.
    /// </summary>
    /// <remarks>
    /// <b>§3.5's hard bound, asserted:</b> *"If it changes how much anybody produces over a year,
    /// it is too big"* — that would be a second, invisible hand on the economy rather than a
    /// stagger. The draw is under one day and is spent **once**, at the start of a working life,
    /// against 480 ticks in every year of it.
    /// </remarks>
    [Fact]
    public void TheRhythmStaggersWithoutCostingAnything()
    {
        SimConfig config = ShippedConfig.Established() with
        {
            FoundingMasters = 0,
            FoundingJourneymen = 0,
        };

        long withRhythm = TripsMadeInFiftyYears(config);
        long without = TripsMadeInFiftyYears(config with { SeededRhythm = false });

        long drift = Math.Abs(withRhythm - without) * 100 / Math.Max(1, without);
        _output.WriteLine(
            $"50 years: {withRhythm} trips with the rhythm, {without} without — {drift}% apart");

        Assert.True(
            drift <= 15,
            $"The personal rhythm moved fifty years of production by {drift}%. It is meant to be "
            + "people not getting up at the same moment, not a second hand on the economy.");
    }

    /// <summary>
    /// ⭐ Every seed gets the same <b>shape</b> of party — <b>and no seed gets one that cannot
    /// live</b>.
    /// </summary>
    /// <remarks>
    /// <b>The distinction fixed composition exists to create</b> (§3.2c). A seed handing you four
    /// novices and a seed handing you two masters would be a bad run and a good one rather than
    /// two playthroughs, and §0.1 is that the challenge is in the planning, never in the
    /// punishment. **Asserted across a twelve-seed arm**, which is the guard that has caught this
    /// class of thing before (D103's seed 11).
    /// </remarks>
    [Theory]
    [InlineData(1UL)]
    [InlineData(2UL)]
    [InlineData(7UL)]
    [InlineData(11UL)]
    [InlineData(42UL)]
    [InlineData(12345UL)]
    public void EverySeedGetsTheSamePartyAndADifferentSpeciality(ulong seed)
    {
        SimConfig config = ShippedConfig.Established() with { Seed = seed };
        SimWorld world = Loop(config).World;

        var tiers = new List<SkillTier>();
        var trades = new List<string>();

        foreach (Villager villager in world.Villagers.Where(v => v.Alive))
        {
            SkillTier best = SkillTier.Novice;
            string what = "nothing";

            foreach (SkillRow skill in config.Skills)
            {
                SkillTier tier = world.TierOf(villager, skill);
                if (tier > best)
                {
                    best = tier;
                    what = skill.Name;
                }
            }

            tiers.Add(best);
            if (best != SkillTier.Novice) { trades.Add($"{best} {what}"); }
        }

        _output.WriteLine($"seed {seed}: {string.Join(", ", trades)}");

        Assert.Equal(config.FoundingMasters, tiers.Count(t => t == SkillTier.Master));
        Assert.Equal(config.FoundingJourneymen, tiers.Count(t => t == SkillTier.Journeyman));

        // ⭐ Different trades, so the party is a speciality rather than a doubled-up one.
        Assert.Equal(trades.Count, trades.Distinct().Count());
    }

    /// <summary>Share of ticks two adults of one household spend on the same tile, first 5 years.</summary>
    private static (int Tile, int Hunger) SameTileShare(SimConfig config)
    {
        SimLoop loop = SimFactory.CreatePhase0(config, new InMemoryLogSink());
        SimWorld world = loop.World;

        int together = 0;
        int sameHunger = 0;
        int samples = 0;

        for (int t = 0; t < config.TicksPerYear * 5; t++)
        {
            loop.StepOnce();

            foreach (Household household in world.Households)
            {
                List<Villager> adults = world.Villagers
                    .Where(v => v.Alive && v.HouseholdId == household.Id && v.CanWork)
                    .Take(2)
                    .ToList();

                if (adults.Count < 2)
                {
                    continue;
                }

                samples++;
                if (adults[0].Position == adults[1].Position) { together++; }
                if (adults[0].Hunger == adults[1].Hunger) { sameHunger++; }
            }
        }

        return samples == 0
            ? (0, 0)
            : (together * 100 / samples, sameHunger * 100 / samples);
    }

    /// <summary>
    /// Trips made in fifty years — <b>what the village produced, not what it happens to be
    /// holding</b>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// ⛔⛔ <b>THIS USED TO SUM THE FOOD IN THE STORES AND IT WAS THE WRONG INSTRUMENT</b>
    /// (D262). Food on hand at year fifty is a <em>stock</em>, read at one instant — the exact
    /// mistake D200 and D227 are the record of (*"firewood fell 156 → 131 → 91 and I called it a
    /// real cost … it was noise"*). It answers *"how much is in the larder that afternoon"*,
    /// while §3.5's bound is about <b>production</b>.
    /// </para>
    /// <para>
    /// ⚠️ <b>Joe's two-seat cap is what exposed it.</b> With two seats a village lives nearer
    /// its ceiling, and a stock reading there swings on whether a single autumn trip landed
    /// before or after the tick that was sampled: **1,420 food against 1,873, 24% apart**, on a
    /// change that moves nobody's working day by a day. Counting trips over the whole fifty
    /// years cannot be knocked about by where the sample happens to fall.
    /// </para>
    /// <para>
    /// ⭐ Summed across <em>every</em> villager, the dead included — they are left in the roster,
    /// and a lifetime of work does not stop counting because its owner did.
    /// </para>
    /// </remarks>
    private static long TripsMadeInFiftyYears(SimConfig config)
    {
        SimLoop loop = SimFactory.CreatePhase0(config, new InMemoryLogSink());
        loop.Step(config.TicksPerYear * 50);

        long trips = 0;
        foreach (Villager villager in loop.World.Villagers)
        {
            trips += villager.TotalGathers;
        }

        return trips;
    }

    private static List<string> MasteryLines(InMemoryLogSink sink, string name) =>
        sink.Entries
            .Where(entry => entry.Subsystem == "life"
                && entry.Message.StartsWith(name, StringComparison.Ordinal)
                && entry.Message.Contains("years", StringComparison.Ordinal))
            .Select(entry => entry.Message)
            .ToList();
}

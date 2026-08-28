using Bclone.Sim.Config;
using Bclone.Sim.Core;
using Bclone.Sim.Logging;
using Bclone.Sim.World;
using Xunit;
using Xunit.Abstractions;

namespace Bclone.Sim.Tests;

/// <summary>
/// ⭐⭐ Apprenticeship — <c>skills-catalog.md §5.1a</c> (D202), and <b>the pillar's whole point</b>.
/// </summary>
/// <remarks>
/// <para>
/// <b>§2.1:</b> *"a villager is an agent with a growing, **transferable** skill… that skill dies
/// with the person unless an elder apprentices a youth."* Until this shipped, **skill was
/// personal and nothing transferred** — a master died and their years died with them, and the
/// village had no way to have done anything about it.
/// </para>
/// <para>
/// <b>⭐ Nobody is assigned to anybody.</b> The pair is *noticed*, not made: the player says how
/// many hands a workplace gets and the sim says who (D51, D62, D106). A per-pair screen would be
/// the slotting UI §2.2 exists to delete, which is why §5.3 makes the lever **staffing** — and
/// §7's at-risk line is what tells the player to use it.
/// </para>
/// </remarks>
public sealed class ApprenticeshipTests
{
    private readonly ITestOutputHelper _output;

    public ApprenticeshipTests(ITestOutputHelper output) => _output = output;

    private static SimConfig Shipped => ShippedConfig.Established();

    private static SimLoop Loop(SimConfig config) =>
        SimFactory.CreatePhase0(config, new InMemoryLogSink());

    private static int MasterYearsAcrossTheLastTwentyYears(SimConfig config)
    {
        SimLoop loop = Loop(config);

        // ⭐⭐ SAMPLED ACROSS THE LAST TWENTY YEARS, NOT READ OFF AT ONE INSTANT — and the change
        // came out of a probe that reversed its own hypothesis (D227).
        //
        // ⛔ THIS GUARD USED TO COUNT MASTERS ALIVE AT EXACTLY TICK N, and that is a spot reading
        // of a fluctuating stock, which this project has been bitten by before: *"firewood fell
        // 156 → 131 → 91 and I called it a real cost; across three seeds it goes down, up, and
        // down-then-up. It was noise"* (D200). **Masters alive on one particular tick swings on
        // whether somebody died at ninety-nine or a hundred and one.**
        //
        // ⚠️ IT WAS NOT A THEORETICAL WEAKNESS. Seed 2 read **8 against 8 — no difference at all**,
        // and the honest conclusion looked like *apprenticeship has stopped mattering*. Measured
        // over the last twenty years instead, that same seed is **5.15 against 8.40 — the LARGEST
        // margin of the three.** *The instant said the feature was dead and the truth was the
        // opposite.*
        //
        // **So the bar stays strictly-more and stays per-seed**, which is stronger than aggregating
        // across seeds would have been. The instrument was wrong, not the claim.
        long mastersOverTheYears = 0;

        for (int year = 0; year < 100; year++)
        {
            loop.Step(config.TicksPerYear);

            if (year >= 80)
            {
                mastersOverTheYears += loop.World.Villagers.Count(
                    v => v.Alive && v.Skills.Any(s => s.Mastered));
            }
        }

        return (int)mastersOverTheYears;
    }

    // ---------------------------------------------------------------
    //  ⭐⭐ §10's anti-vacuity guard, and the one that decides if any of this is real
    // ---------------------------------------------------------------

    /// <summary>
    /// ⭐⭐ A village that teaches ends up with more masters than one that does not.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>This is the guard §10 was written for and it could not exist until now:</b> *"a run
    /// with no apprenticeships must actually lose something. If a village that never teaches ends
    /// up where a village that does ends up, the whole pillar is decoration."* **This project has
    /// shipped a decorative system before and only found out by measuring** — D56's clothing, a
    /// no-op over three hundred years.
    /// </para>
    /// <para>
    /// <b>Measured as masters alive AVERAGED OVER THE LAST TWENTY YEARS, on three seeds:
    /// 6.00 → 6.95, 5.15 → 8.40, 5.35 → 7.45.</b> The bar is *strictly more*, because the claim is
    /// directional and the magnitude is content.
    /// </para>
    /// <para>
    /// ⚠️ <b>The original figures were read at one instant — 3 → 6, 4 → 8, 8 → 10 — and the last
    /// of those went to 8 → 8 when Phase 4 changed the founding</b> (D227). That looked like
    /// apprenticeship dying and was the instrument: smoothed, that same seed has the widest margin
    /// of the three. <em>See the sampling comment above.</em>
    /// </para>
    /// </remarks>
    [Theory]
    [InlineData(12345UL)]
    [InlineData(2UL)]
    [InlineData(42UL)]
    public void AVillageThatTeachesKeepsMoreMastersThanOneThatDoesNot(ulong seed)
    {
        SimConfig teaching = Shipped with { Seed = seed };
        SimConfig silent = teaching with { ApprenticeLearningBonusPercent = 0 };

        int taught = MasterYearsAcrossTheLastTwentyYears(teaching);
        int untaught = MasterYearsAcrossTheLastTwentyYears(silent);

        _output.WriteLine(
            $"seed {seed}: {untaught / 20.0:F2} masters on average over the last twenty years "
            + $"with nobody teaching, {taught / 20.0:F2} with the village teaching");

        Assert.True(
            taught > untaught,
            $"A village that teaches averaged {taught / 20.0:F2} masters over the last twenty "
            + $"years and one that never taught averaged {untaught / 20.0:F2}. If teaching changes nothing, §2.1's whole claim — "
            + "that skill is *transferable* — is decoration, which is D56's clothing again.");
    }

    // ---------------------------------------------------------------
    //  The mechanism
    // ---------------------------------------------------------------

    /// <summary>⭐ A learner beside a master gains faster than the same learner alone.</summary>
    /// <remarks>
    /// <b>Posed as one tick against one tick</b>, so the claim is about the rule rather than
    /// about a century of weather. Two villagers, one workplace, one of them a master.
    /// </remarks>
    [Fact]
    public void ALearnerBesideAMasterGainsFaster()
    {
        SimConfig config = Shipped;
        SimLoop loop = Loop(config);
        SimWorld world = loop.World;
        SkillRow skill = config.Skills[0];

        (Villager learner, Villager other) = TwoAtOneWorkplace(world, skill);

        int aloneFrom = WorkAfterATick(loop, learner, skill);

        // Now make the colleague a master of the same trade, and take another tick.
        MakeThemAMaster(world, other, skill);
        int taughtFrom = WorkAfterATick(loop, learner, skill);

        _output.WriteLine(
            $"{learner.Name} gained {aloneFrom} on a tick alone, {taughtFrom} beside "
            + $"{other.Name} once {other.Name} was a master");

        Assert.True(
            taughtFrom > aloneFrom,
            $"{learner.Name} gained {taughtFrom} beside a master against {aloneFrom} without "
            + "one — the bonus is not reaching the learner at all.");
    }

    /// <summary>⛔ A master learns nothing extra from another master.</summary>
    /// <remarks>
    /// <b>The anti-vacuity companion</b> (D7): a bonus that reached everybody would inflate the
    /// people who need it least and would make the guard above pass for the wrong reason.
    /// </remarks>
    [Fact]
    public void ButAMasterGainsNothingExtraFromAnotherMaster()
    {
        SimConfig config = Shipped;
        SimLoop loop = Loop(config);
        SimWorld world = loop.World;
        SkillRow skill = config.Skills[0];

        (Villager one, Villager two) = TwoAtOneWorkplace(world, skill);
        MakeThemAMaster(world, one, skill);

        int alone = WorkAfterATick(loop, one, skill);
        MakeThemAMaster(world, two, skill);
        int together = WorkAfterATick(loop, one, skill);

        _output.WriteLine($"a master gained {alone} alone and {together} beside another master");
        Assert.Equal(alone, together);
    }

    /// <summary>⛔ A master somewhere else teaches nobody — it is *alongside*, not *in the village*.</summary>
    /// <remarks>
    /// <b>§5.1 says "working alongside", and the stricter reading is the one that means
    /// something:</b> it makes **where the player puts people** decide whether knowledge passes
    /// on, which is the same lesson the farm and the market both landed on. A village-wide bonus
    /// would be a free multiplier nobody sited anything for.
    /// </remarks>
    [Fact]
    public void AMasterAtAnotherWorkplaceTeachesNobody()
    {
        SimConfig config = Shipped;
        SimLoop loop = Loop(config);
        SimWorld world = loop.World;
        SkillRow skill = config.Skills[0];

        (Villager learner, Villager elsewhere) = TwoAtOneWorkplace(world, skill);

        int before = WorkAfterATick(loop, learner, skill);

        // A master of the same skill, at a different building. ⚠️ ANY other building will do,
        // and the first draft demanded another of the same TRADE — which the founding does not
        // have, so the fixture threw rather than testing anything. The rule keys on the
        // workplace id, so the claim is about being elsewhere, not about being elsewhere in the
        // same trade.
        Workplace other = world.Workplaces.First(w => w.Id != learner.WorkplaceId && !w.IsSite);
        elsewhere.WorkplaceId = other.Id;
        MakeThemAMaster(world, elsewhere, skill);

        int after = WorkAfterATick(loop, learner, skill);

        _output.WriteLine(
            $"{learner.Name} gained {before} then {after} with a master at {other.Name}");
        Assert.Equal(before, after);
    }

    /// <summary>⭐ Teaching is free — the master's own years are untouched by it.</summary>
    /// <remarks>
    /// <b>Joe's call (D202), following D183's *"let's give to the player, not punish or
    /// decay"*.</b> The guard exists because the obvious "balancing" instinct is to charge the
    /// teacher, and doing so would quietly make staffing a master beside a youth a *cost* — which
    /// is the opposite of what §7's at-risk line is telling the player to do.
    /// </remarks>
    [Fact]
    public void AndTheMasterPaysNothingForIt()
    {
        SimConfig config = Shipped;
        SimLoop loop = Loop(config);
        SimWorld world = loop.World;
        SkillRow skill = config.Skills[0];

        (Villager learner, Villager master) = TwoAtOneWorkplace(world, skill);
        MakeThemAMaster(world, master, skill);

        int before = master.ProgressIn(skill.Id).Work;
        int gained = WorkAfterATick(loop, master, skill);
        int alone = WorkWithNobodyToTeach(config, skill);

        _output.WriteLine(
            $"the master gained {gained} on a tick with {learner.Name} beside them, "
            + $"{alone} with nobody — starting from {before}");

        Assert.Equal(alone, gained);
    }

    // ---------------------------------------------------------------
    //  Posing helpers
    // ---------------------------------------------------------------

    /// <summary>Two living adults holding the same workplace of the skill's trade.</summary>
    private static (Villager Learner, Villager Other) TwoAtOneWorkplace(
        SimWorld world, SkillRow skill)
    {
        Workplace place = world.Workplaces.First(w => w.Kind == skill.GrownBy && !w.IsSite);

        Villager[] pair = world.Villagers.Where(v => v.Alive && v.CanWork).Take(2).ToArray();
        Assert.True(pair.Length == 2, "Need two able adults to pose a pair.");

        foreach (Villager villager in pair)
        {
            villager.WorkplaceId = place.Id;
            if (!place.WorkerIds.Contains(villager.Id))
            {
                place.WorkerIds.Add(villager.Id);
            }
        }

        return (pair[0], pair[1]);
    }

    /// <summary>Work this villager puts into a skill over exactly one tick.</summary>
    private static int WorkAfterATick(SimLoop loop, Villager villager, SkillRow skill)
    {
        int before = villager.ProgressIn(skill.Id).Work;
        new Systems.SkillSystem().Execute(loop.World);
        return villager.ProgressIn(skill.Id).Work - before;
    }

    /// <summary>What one tick is worth to a lone master, for the free-teaching comparison.</summary>
    private static int WorkWithNobodyToTeach(SimConfig config, SkillRow skill)
    {
        SimLoop loop = Loop(config);
        SimWorld world = loop.World;

        Workplace place = world.Workplaces.First(w => w.Kind == skill.GrownBy && !w.IsSite);
        Villager only = world.Villagers.First(v => v.Alive && v.CanWork);

        only.WorkplaceId = place.Id;
        if (!place.WorkerIds.Contains(only.Id))
        {
            place.WorkerIds.Add(only.Id);
        }

        MakeThemAMaster(world, only, skill);
        return WorkAfterATick(loop, only, skill);
    }

    private static void MakeThemAMaster(SimWorld world, Villager villager, SkillRow skill)
    {
        SkillProgress progress = villager.ProgressIn(skill.Id);
        progress.Work = world.Config.MasteryWorkFor(skill);
        progress.Ticks = world.Config.MasteryYearsFor(skill) * world.Config.TicksPerYear;
        progress.Mastered = true;
    }
}

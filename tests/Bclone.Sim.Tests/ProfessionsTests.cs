using Bclone.Sim.Config;
using Bclone.Sim.Core;
using Bclone.Sim.Determinism;
using Bclone.Sim.Logging;
using Bclone.Sim.World;
using Xunit;
using Xunit.Abstractions;

namespace Bclone.Sim.Tests;

/// <summary>
/// The professions panel: how many people on each kind of work, village-wide — D106, Joe.
/// </summary>
/// <remarks>
/// <para>
/// <b>Banished's panel, and Joe's screenshot is the spec.</b> The per-building control (D104)
/// answers <em>"how many at this hut?"</em>; this answers <em>"how many woodcutters at all?"</em>
/// </para>
/// <para>
/// <b>⭐ It is also the answer to D103, which the village could not solve for itself.</b>
/// Building is funded from whatever is left after eating and heating, and measured, that is
/// nothing for most of the year. Two attempts to fix that with a rule each killed a valley. A
/// player who can say <em>"two builders"</em> fixes the village in front of them, which is what
/// a management sim is for.
/// </para>
/// </remarks>
public sealed class ProfessionsTests
{
    private readonly ITestOutputHelper _output;

    public ProfessionsTests(ITestOutputHelper output) => _output = output;

    private static SimLoop Loop(SimConfig config) =>
        SimFactory.CreatePhase0(config, new InMemoryLogSink());

    private static int WorkingAt(SimWorld world, JobKind kind)
    {
        int count = 0;
        for (int i = 0; i < world.Workplaces.Count; i++)
        {
            if (world.Workplaces[i].Kind == kind)
            {
                count += world.Workplaces[i].WorkerIds.Count;
            }
        }

        return count;
    }

    // ---------------------------------------------------------------
    //  The default is a no-op — the licence for the whole control
    // ---------------------------------------------------------------

    /// <summary>A village played without opening the panel is the village that came before.</summary>
    /// <remarks>
    /// Stated in hashes rather than prose, exactly as <c>StockLimitTests</c> does for goods:
    /// every acceptance band in the project was measured on a village that never touched this,
    /// so the default has to be indistinguishable from the feature's absence. The two
    /// fifty-year goldens are the other half of this claim.
    /// </remarks>
    [Fact]
    public void SettingNothingChangesNothing()
    {
        SimConfig config = VillageFixtures.Village;

        SimLoop untouched = Loop(config);
        untouched.Step(config.TicksPerYear * 5);

        SimLoop opened = Loop(config);
        opened.World.SetJobLimit(JobKind.Builder, 2);
        opened.World.SetJobLimit(JobKind.Builder, null);
        opened.Step(config.TicksPerYear * 5);

        Assert.False(untouched.World.JobLimits.AnySet);
        Assert.False(opened.World.JobLimits.AnySet);
        Assert.Equal(StateHash.Compute(untouched.World), StateHash.Compute(opened.World));
    }

    /// <summary>"No opinion" and "nobody on this" are different worlds.</summary>
    [Fact]
    public void NoOpinionAndAnExplicitZeroAreDifferentStates()
    {
        SimConfig config = VillageFixtures.Village;

        SimWorld noOpinion = Loop(config).World;
        SimWorld explicitZero = Loop(config).World;
        explicitZero.SetJobLimit(JobKind.Forester, 0);

        Assert.Null(noOpinion.JobLimits.For(JobKind.Forester));
        Assert.Equal(0, explicitZero.JobLimits.For(JobKind.Forester));
        Assert.NotEqual(StateHash.Compute(noOpinion), StateHash.Compute(explicitZero));
    }

    /// <summary>Every kind of work the game has can be asked for.</summary>
    /// <remarks>
    /// Read off the enum, so a job kind added later cannot be silently missing from the panel —
    /// the mistake <c>StockLimits.Kinds</c> made by listing goods by hand and had to correct.
    /// </remarks>
    [Fact]
    public void EveryKindOfWorkCanBeAskedFor()
    {
        SimWorld world = Loop(VillageFixtures.Village).World;

        foreach (JobKind kind in System.Enum.GetValues<JobKind>())
        {
            Assert.Contains(kind, JobLimits.Kinds);
            world.SetJobLimit(kind, 1);
            Assert.Equal(1, world.JobLimits.For(kind));
        }
    }

    // ---------------------------------------------------------------
    //  It binds, in both directions
    // ---------------------------------------------------------------

    /// <summary>⭐ Asking for nobody on a job takes everybody off it.</summary>
    [Fact]
    public void AskingForNobodyEmptiesTheJob()
    {
        SimConfig config = VillageFixtures.Village;
        SimLoop loop = Loop(config);
        SimWorld world = loop.World;

        // ANTI-VACUITY FIRST (D7). The forester quota rises and falls with the woodpile, so
        // reading it at one instant proves nothing: an arm that happened to stop on a tick
        // wanting none would pass this while the control did nothing at all.
        int mostWantedUnlimited = 0;
        for (int tick = 0; tick < config.TicksPerYear * 10; tick++)
        {
            loop.StepOnce();
            int wanted = LabourQuota.For(world).For(JobKind.Forester);
            if (wanted > mostWantedUnlimited)
            {
                mostWantedUnlimited = wanted;
            }
        }

        Assert.True(
            mostWantedUnlimited > 0,
            "An unlimited village never wanted a forester, so asking for none proves nothing.");

        world.SetJobLimit(JobKind.Forester, 0);

        int mostWantedAfter = 0;
        int mostWorkingAfter = 0;
        for (int tick = 0; tick < config.TicksPerYear * 4; tick++)
        {
            loop.StepOnce();
            mostWantedAfter = System.Math.Max(
                mostWantedAfter, LabourQuota.For(world).For(JobKind.Forester));
            mostWorkingAfter = System.Math.Max(
                mostWorkingAfter, WorkingAt(world, JobKind.Forester));
        }

        _output.WriteLine(
            $"foresters most wanted {mostWantedUnlimited} unlimited -> {mostWantedAfter} at a "
            + $"limit of nobody; most at work after {mostWorkingAfter}");

        Assert.Equal(0, mostWantedAfter);
        Assert.Equal(0, mostWorkingAfter);
    }

    /// <summary>
    /// ⭐ And asking for MORE than the village would choose actually gets it — which is the
    /// whole point, and the difference from a stock limit.
    /// </summary>
    /// <remarks>
    /// <b>D103 is the case.</b> The village funds building from what is left after eating and
    /// heating, and measured, that is zero for most of the year — so a player who wants a
    /// granary this decade has to be able to overrule it. A control that could only ever ask
    /// for <em>less</em> would be no answer at all.
    /// </remarks>
    [Fact]
    public void AskingForMoreBuildersThanTheVillageWouldChooseGetsThem()
    {
        SimConfig config = VillageFixtures.Village;
        SimLoop loop = Loop(config);
        SimWorld world = loop.World;

        loop.Step(config.TicksPerYear * 10);

        // Something to build, so there are seats to fill.
        GridPos site = world.Map.FoundingSite;
        for (int radius = 2; radius <= 6; radius++)
        {
            bool marked = false;
            for (int dy = -radius; dy <= radius && !marked; dy++)
            {
                for (int dx = -radius; dx <= radius && !marked; dx++)
                {
                    marked = world.Mark(
                        BuildingKind.Granary, new GridPos(site.X + dx, site.Y + dy)).Allowed;
                }
            }

            if (marked)
            {
                break;
            }
        }

        loop.Step(20);
        int villageWants = LabourQuota.For(world).For(JobKind.Builder);

        world.SetJobLimit(JobKind.Builder, 2);
        loop.Step(20);

        _output.WriteLine(
            $"builders: village wanted {villageWants}, asked for 2, quota now "
            + $"{LabourQuota.For(world).For(JobKind.Builder)}, "
            + $"{WorkingAt(world, JobKind.Builder)} at work");

        Assert.Equal(2, LabourQuota.For(world).For(JobKind.Builder));
        Assert.True(
            2 > villageWants,
            $"The village already wanted {villageWants} builders, so this proves nothing.");
    }

    // ---------------------------------------------------------------
    //  Bounded by what exists, and said out loud
    // ---------------------------------------------------------------

    /// <summary>You may ask for six woodcutters; you may not conjure the seats.</summary>
    /// <remarks>
    /// <b>Bounded rather than refused</b>, and the player is told which it was. A number the
    /// game silently cannot honour is worse than one it declines, because the player goes on
    /// planning against it.
    /// </remarks>
    [Fact]
    public void AskingForMorePlacesThanExistIsBoundedAndSaidOutLoud()
    {
        SimConfig config = VillageFixtures.Village;
        SimLoop loop = Loop(config);
        SimWorld world = loop.World;

        loop.Step(config.TicksPerYear * 5);

        int seats = 0;
        for (int i = 0; i < world.Workplaces.Count; i++)
        {
            if (world.Workplaces[i].Kind == JobKind.Woodcutter)
            {
                seats += world.Workplaces[i].Capacity;
            }
        }

        PlacementVerdict verdict = world.SetJobLimit(JobKind.Woodcutter, seats + 50);
        int wanted = LabourQuota.For(world).For(JobKind.Woodcutter);
        _output.WriteLine(
            $"{seats} seats and {world.Population} people, asked for {seats + 50}, "
            + $"quota {wanted}: \"{verdict.Warning}\"");

        Assert.True(verdict.Allowed);
        Assert.True(verdict.HasWarning, "The village accepted an impossible number in silence.");

        // Bounded by the seats AND by the people — you cannot conjure either. Asserted as an
        // upper bound rather than as "exactly the seats", because a young village has fewer
        // adults than a woodyard has places and the smaller of the two is the honest answer.
        Assert.True(
            wanted <= seats,
            $"Asked for {seats + 50} on {seats} seats and the village wanted {wanted}.");
        Assert.True(wanted > 0, "Nobody was put on it at all, so the bound is not the thing.");
    }

    /// <summary>Taking everyone off the food or the fuel is allowed, and says what it means.</summary>
    /// <remarks>
    /// D62's shape: a game that refuses the player's number is arguing with them, and one that
    /// obeys it silently has killed them without saying so. These are the two jobs whose
    /// absence kills — hunger in six days, an unheated house in twenty-five (D45).
    /// </remarks>
    [Theory]
    [InlineData(JobKind.Forager)]
    [InlineData(JobKind.Woodcutter)]
    public void TakingEverybodyOffTheJobsThatKeepPeopleAliveIsWarnedAbout(JobKind kind)
    {
        SimWorld world = Loop(VillageFixtures.Village).World;

        PlacementVerdict verdict = world.SetJobLimit(kind, 0);
        _output.WriteLine($"{kind} set to 0: \"{verdict.Warning}\"");

        Assert.True(verdict.Allowed, "The village refused the player's number.");
        Assert.True(verdict.HasWarning);
        Assert.Equal(0, world.JobLimits.For(kind));
    }

    /// <summary>
    /// ⭐ Laborers are made by capping the GATHERERS, and that is worth knowing before you
    /// reach for the panel.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Measured while writing this guard, and it is not what I expected.</b> Emptying the
    /// foresters, the vendors and the builders made the number of laborers go <em>down</em>,
    /// 4 → 2 — because the quota's last act is <em>"everyone still spare forages"</em>, so a
    /// hand freed from one profession lands on a berry patch rather than becoming spare.
    /// </para>
    /// <para>
    /// <b>So the gatherer row is the laborer row, from the other end</b>, and it works because
    /// a profession target is applied <em>after</em> that mop-up (see <c>LabourQuota.Asked</c>)
    /// — which is the whole reason it is applied last. Joe asked to "set the number of
    /// laborers"; capping gathering is that control, and it is the honest one, because a
    /// laborer is defined as somebody with no job (D66) and not as a job of their own.
    /// </para>
    /// </remarks>
    [Fact]
    public void CappingTheGatherersIsWhatMakesLaborers()
    {
        SimConfig config = VillageFixtures.Village;
        SimLoop loop = Loop(config);
        SimWorld world = loop.World;

        // ⚠️ MID-SEASON, NOT ON A YEAR BOUNDARY. A year boundary is the one instant where the
        // last reshuffle's work is finished and the next has not been decided, so every
        // WorkerIds list reads empty — `WoodTests` records the same trap in its own comment.
        loop.Step((config.TicksPerYear * 10) + (config.TicksPerSeason / 2));

        int before = world.Laborers;
        int gathering = WorkingAt(world, JobKind.Forager);
        Assert.True(gathering > 1, $"Need gatherers to take hands off; {gathering} are at work.");

        world.SetJobLimit(JobKind.Forager, 1);

        // A season, not years: this is about hands moving, and waiting longer lets births and
        // deaths move the number for reasons that are not the control.
        loop.Step(config.TicksPerSeason);

        _output.WriteLine(
            $"gathering capped at 1 (was {gathering} at work): laborers {before} -> "
            + $"{world.Laborers} of {world.Population} alive");

        Assert.True(world.Population > 0, "The village died, so this compares nothing.");
        Assert.True(
            world.Laborers > before,
            $"Gathering was capped and the number of laborers went {before} -> "
            + $"{world.Laborers}.");
    }

    /// <summary>⭐ The professions panel's arithmetic works out — laborers are the remainder.</summary>
    /// <remarks>
    /// <para>
    /// <b>The sum a player does in their head, made a guard (D148).</b> Joe, playing: <i>"why
    /// does it say there is one laborer when I only have 4 villagers and they are all assigned
    /// jobs?"</i> The sim was right — his woodcutter row read <c>1</c> in the staffing box and
    /// <c>0 of 2</c> beside it, because firewood was at its limit and nobody was actually there
    /// — but nothing on screen made the four add up, so the panel looked wrong.
    /// </para>
    /// <para>
    /// <b>The invariant the panel now presents:</b> every able adult is either holding a job or
    /// is a laborer, and nobody is both or neither. If that ever stops being true the numbers
    /// stop reconciling and a player is right to be confused — so it is checked over a run
    /// rather than at one instant, and across the seasons where hands move.
    /// </para>
    /// </remarks>
    [Fact]
    public void EveryAbleAdultIsEitherAtWorkOrALaborer()
    {
        SimConfig config = VillageFixtures.Village;
        SimLoop loop = Loop(config);
        SimWorld world = loop.World;

        int checkedTicks = 0;
        int mostEmployed = 0;

        for (int tick = 0; tick < config.TicksPerYear * 12; tick++)
        {
            loop.StepOnce();

            int employed = 0;
            foreach (Villager villager in world.Villagers)
            {
                if (villager.Alive && villager.CanWork && villager.HasJob)
                {
                    employed++;
                }
            }

            mostEmployed = System.Math.Max(mostEmployed, employed);
            checkedTicks++;

            Assert.Equal(world.AbleAdults, employed + world.Laborers);
        }

        _output.WriteLine(
            $"{checkedTicks} ticks reconciled; at the end {world.AbleAdults} able adults = "
            + $"{world.AbleAdults - world.Laborers} at work + {world.Laborers} spare "
            + $"(most ever employed {mostEmployed}).");

        // Anti-vacuity: a village where nobody ever held a job would reconcile trivially.
        Assert.True(mostEmployed > 0, "Nobody ever held a job, so the sum proves nothing (D7).");
        Assert.True(world.AbleAdults > 0, "Nobody was left alive to count.");
    }
}

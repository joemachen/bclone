using Bclone.Sim.Config;
using Bclone.Sim.Core;
using Bclone.Sim.Logging;
using Bclone.Sim.World;
using Xunit;
using Xunit.Abstractions;

namespace Bclone.Sim.Tests;

/// <summary>
/// ⭐⭐ *"Mabel is 68 and the only soul who knows herbalism."* — `skills-catalog.md §7`, and
/// **§11's last outstanding Definition-of-Done item** (D195).
/// </summary>
/// <remarks>
/// <para>
/// <b>§2.1 names the failure mode this exists to answer:</b> *"punishing the player for losses
/// they couldn't foresee. Knowledge-at-risk must be **visible and actionable**."* Measured over a
/// century on three seeds, **11 to 16 masters die** in a village that never notices — everything
/// quietly gets slower and the only evidence is a funeral. <b>That is the surprise D103's rule
/// forbids</b>, and it is what makes this a gate rather than a nicety.
/// </para>
/// <para>
/// <b>⭐ The remedy is named in the sentence because the remedy is the point.</b>
/// `skills-catalog.md §5.3` is explicit that the player's lever is *staffing* — put somebody
/// beside the elder — rather than a pairing screen, so the warning says so. **A warning whose
/// remedy is unstated is an alert, not information.**
/// </para>
/// </remarks>
public sealed class KnowledgeAtRiskTests
{
    private readonly ITestOutputHelper _output;

    public KnowledgeAtRiskTests(ITestOutputHelper output) => _output = output;

    private static SimConfig Config => ShippedConfig.Established();

    private static SimLoop Loop(SimConfig config) =>
        SimFactory.CreatePhase0(config, new InMemoryLogSink());

    // ---------------------------------------------------------------
    //  The condition
    // ---------------------------------------------------------------

    /// <summary>⭐ The last living master, and old — the sentence fires.</summary>
    [Fact]
    public void TheLastLivingMasterOfATradeIsWarnedAbout()
    {
        SimLoop loop = Loop(Config);
        SimWorld world = loop.World;
        SkillRow skill = Config.Skills[0];

        Villager elder = MakeThemAMaster(world, StepUntilSomebodyIsFrail(loop), skill);

        string? note = world.KnowledgeAtRiskNote(elder);
        _output.WriteLine(note ?? "(nothing)");

        Assert.NotNull(note);
        Assert.Contains(elder.Name, note, System.StringComparison.Ordinal);
        Assert.Contains(
            skill.Name.ToLowerInvariant(), note, System.StringComparison.OrdinalIgnoreCase);

        // ⭐ AND IT SAYS WHAT TO DO. §5.3's lever is staffing, and a warning that only states a
        // fact leaves the player watching every elder forever — which is the babysitting §1.2
        // forbids.
        Assert.Contains("beside", note, System.StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// ⭐⭐ A second master silences it — <b>the anti-vacuity half</b> (D7).
    /// </summary>
    /// <remarks>
    /// Without this, a warning that fired on every elder in the valley would pass the guard above
    /// and teach the player to stop reading the log — D42, D123 and D147 all settled that an
    /// always-on alert is one nobody reads.
    /// </remarks>
    [Fact]
    public void ButNotWhenSomebodyElseKnowsItToo()
    {
        SimLoop loop = Loop(Config);
        SimWorld world = loop.World;
        SkillRow skill = Config.Skills[0];

        Villager elder = MakeThemAMaster(world, StepUntilSomebodyIsFrail(loop), skill);
        Assert.NotNull(world.KnowledgeAtRiskNote(elder));

        MakeThemAMaster(world, world.Villagers.First(v => v.Alive && v.Id != elder.Id), skill);

        _output.WriteLine(world.KnowledgeAtRiskNote(elder) ?? "(nothing, correctly)");
        Assert.Null(world.KnowledgeAtRiskNote(elder));
    }

    /// <summary>⚠️ A master in their prime is not at risk — the warning is about a lifetime.</summary>
    /// <remarks>
    /// <b>Both halves of the condition are load-bearing.</b> Warning about every sole master
    /// regardless of age would fire on a thirty-year-old with twenty-five years left in them, and
    /// the player would learn that the sentence does not mean anything.
    /// </remarks>
    [Fact]
    public void AndNotAboutAMasterInTheirPrime()
    {
        SimWorld world = Loop(Config).World;
        SkillRow skill = Config.Skills[0];

        Villager master = MakeThemAMaster(world, world.Villagers[0], skill);
        master.LifeStage = LifeStage.Adult;

        Assert.Null(world.KnowledgeAtRiskNote(master));
    }

    /// <summary>⚠️ And not about an elder who never mastered anything.</summary>
    [Fact]
    public void AndNotAboutAnElderWhoNeverMasteredAnything()
    {
        SimLoop loop = Loop(Config);
        Villager elder = StepUntilSomebodyIsFrail(loop);

        Assert.Null(loop.World.KnowledgeAtRiskNote(elder));
    }

    // ---------------------------------------------------------------
    //  The narration
    // ---------------------------------------------------------------

    /// <summary>⭐ Once, on the edge — not every year until they die.</summary>
    /// <remarks>
    /// <b>D123's rule and D147's, and the mastery line one system over is the model:</b> narrated
    /// when it changes, never a standing banner. A warning repeated annually for the fifteen years
    /// of somebody's old age is a nag, and the player learns to scroll past it.
    /// </remarks>
    [Fact]
    public void TheVillageIsToldOnceRatherThanEveryYear()
    {
        var sink = new InMemoryLogSink();
        SimConfig config = Config;
        SimLoop loop = SimFactory.CreatePhase0(config, sink);
        SimWorld world = loop.World;

        SkillRow skill = config.Skills[0];
        Villager elder = MakeThemAMaster(world, StepUntilSomebodyIsFrail(loop), skill);

        for (int i = 0; i < config.TicksPerYear * 4; i++)
        {
            loop.StepOnce();
        }

        int said = CountWarnings(sink, elder);
        _output.WriteLine($"warned {said} times over four years");
        Assert.Equal(1, said);
    }

    /// <summary>
    /// ⭐⭐ And told <b>again</b> if the trade becomes at risk a second time.
    /// </summary>
    /// <remarks>
    /// <b>This is what makes it an edge detector rather than a one-shot</b>, and it is the guard
    /// that fails if somebody "simplifies" the bookkeeping into a flag that only ever sets. A
    /// village that trains a second master and then loses them is right back where it started,
    /// and it must hear about it — otherwise the one warning it got, years ago, was the only one
    /// it will ever get about that trade.
    /// </remarks>
    [Fact]
    public void AndToldAgainIfTheTradeFallsBackToOneHolder()
    {
        var sink = new InMemoryLogSink();
        SimConfig config = Config;
        SimLoop loop = SimFactory.CreatePhase0(config, sink);
        SimWorld world = loop.World;

        SkillRow skill = config.Skills[0];
        Villager elder = MakeThemAMaster(world, StepUntilSomebodyIsFrail(loop), skill);
        Villager second = MakeThemAMaster(
            world, world.Villagers.First(v => v.Alive && v.Id != elder.Id), skill);

        // A year with two masters: nothing to say.
        StepAYear(loop, config);
        Assert.Equal(0, CountWarnings(sink, elder));

        // The second master dies. Now the elder is the last.
        //
        // ⚠️ EVERY OTHER MASTER, NOT JUST THE ONE THIS FIXTURE MADE (D200). The village grows
        // its own masters — measured, 15 to 19 a century — so killing the posed second one left
        // a third that nobody in this test had heard of, the condition was correctly false, and
        // the guard failed for the feature working.
        StepToTheNextSweep(loop, config, skill, elder);
        Assert.Equal(1, CountWarnings(sink, elder));

        // Somebody else masters it — the warning stands down.
        Villager third = MakeThemAMaster(
            world,
            world.Villagers.First(v => v.Alive && v.Id != elder.Id && v.Id != second.Id),
            skill);
        StepAYear(loop, config);
        Assert.Equal(1, CountWarnings(sink, elder));

        // …and dies. The village must hear it a second time.
        StepToTheNextSweep(loop, config, skill, elder);

        int said = CountWarnings(sink, elder);
        _output.WriteLine($"warned {said} times across two separate at-risk spells");
        Assert.Equal(2, said);
    }

    // ---------------------------------------------------------------
    //  ⭐⭐ It fires in a village nobody posed
    // ---------------------------------------------------------------

    /// <summary>
    /// ⭐⭐ A played village actually reaches this — <b>the guard that says the feature is real</b>.
    /// </summary>
    /// <remarks>
    /// <b>D157's rule, and this project has been caught by it three times:</b> a guard can be
    /// green and blind because its fixture makes the case impossible. Every guard above poses a
    /// master by hand. **This one plays a century and requires the sentence to have been said**,
    /// because a warning that only fires in a test fixture is D103's unreachable feature.
    /// </remarks>
    [Theory]
    [InlineData(12345UL)]
    [InlineData(2UL)]
    [InlineData(42UL)]
    public void APlayedVillageActuallyHearsIt(ulong seed)
    {
        var sink = new InMemoryLogSink();
        SimConfig config = Config with { Seed = seed };
        SimLoop loop = SimFactory.CreatePhase0(config, sink);

        for (int i = 0; i < config.TicksPerYear * 100; i++)
        {
            loop.StepOnce();
        }

        List<string> warnings = sink.Entries
            .Where(e => e.Message.Contains(
                "the only soul in the village", System.StringComparison.Ordinal))
            .Select(e => e.Message)
            .ToList();

        _output.WriteLine($"seed {seed}: {warnings.Count} warnings over a century");
        foreach (string line in warnings.Take(4))
        {
            _output.WriteLine("  " + line);
        }

        Assert.True(
            warnings.Count > 0,
            $"A century on seed {seed} and the village was never once told that a trade was about "
            + "to die with somebody. Measured before this shipped, 11 to 16 masters die per "
            + "century — so either the condition cannot be reached or the sweep is not running, "
            + "and both make this D103's unreachable feature.");
    }

    // ---------------------------------------------------------------
    //  Posing helpers
    // ---------------------------------------------------------------

    /// <summary>
    /// Step until somebody in the village is genuinely frail, and hand them back.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>⛔⛔ AN ELDER CANNOT BE POSED, AND TWO DRAFTS OF THIS FILE TRIED.</b> Writing
    /// <see cref="LifeStage.Elder"/> lasts one tick — <see cref="Systems.AgeingSystem"/>
    /// recomputes the stage from vigour on every one. Writing <c>AgeYears</c> lasts one tick too,
    /// because <see cref="Systems.ClockSystem"/> recomputes it as <c>year - BirthYear</c>; the
    /// guard watched a 51-year-old turn 21 between the first tick and the second and read the
    /// resulting silence as a broken sweep. And <c>BirthYear</c> is <c>init</c>-only, which is
    /// the model telling you the truth: <b>age is derived, and the only honest way to have an
    /// old villager is to let one get old.</b>
    /// </para>
    /// <para>
    /// A few thousand ticks, which is cheap — and it makes these guards run against the ageing
    /// the game actually does rather than against a state it cannot reach.
    /// </para>
    /// </remarks>
    private static Villager StepUntilSomebodyIsFrail(SimLoop loop)
    {
        SimWorld world = loop.World;

        for (int i = 0; i < world.Config.TicksPerYear * 80; i++)
        {
            // ⚠️ THE FRAIL VILLAGER WITH THE MOST LIFE LEFT, NOT THE FIRST ONE FOUND (D200).
            // These guards step several years and the annual sweep means they must: an elder
            // picked at random is one who may well die part-way through, and **a warning that
            // stops because the person died reads exactly like a warning that stopped working.**
            // It failed that way the day `labour_slack_ticks` changed the village's timings.
            Villager? oldest = null;
            for (int v = 0; v < world.Villagers.Count; v++)
            {
                Villager villager = world.Villagers[v];
                if (!villager.Alive || villager.LifeStage != LifeStage.Elder)
                {
                    continue;
                }

                if (oldest is null
                    || villager.LifespanYears - villager.AgeYears
                        > oldest.LifespanYears - oldest.AgeYears)
                {
                    oldest = villager;
                }
            }

            if (oldest is not null)
            {
                return oldest;
            }

            loop.StepOnce();
        }

        throw new Xunit.Sdk.XunitException("Eighty years and nobody in the village grew old.");
    }

    /// <summary>
    /// Step to the next annual sweep, holding the trade at exactly one master as it lands.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>⚠️ CLEARING THE OTHER MASTERS A YEAR IN ADVANCE DOES NOT WORK, AND FINDING OUT WHY WAS
    /// THE USEFUL PART (D200).</b> <c>Skills[0]</c> is <b>foraging</b> — the trade the village
    /// holds more of than any other — and it produces masters faster than a fixture can clear
    /// them: measured, <b>15 to 19 a century</b>. Killing the two this test posed left a third
    /// nobody had heard of, the condition was correctly false, and the guard failed for the
    /// feature working.
    /// </para>
    /// <para>
    /// <b>So the state is held true at the moment it is read</b> rather than a year before. The
    /// sweep runs on the year boundary, so this walks to it clearing as it goes.
    /// </para>
    /// </remarks>
    private static void StepToTheNextSweep(
        SimLoop loop, SimConfig config, SkillRow skill, Villager keep)
    {
        for (int i = 0; i < config.TicksPerYear; i++)
        {
            LeaveOnlyOneMasterOf(loop.World, skill, keep);
            loop.StepOnce();

            if (loop.World.Tick % (ulong)config.TicksPerYear != 0UL)
            {
                continue;
            }

            // ⚠️ ONE MORE, AND IT IS NOT PADDING (FarmFixtures records the same trap).
            // `StepOnce` runs the systems and THEN advances the tick, so the moment the clock
            // first reads a year boundary the sweep has not seen it yet.
            LeaveOnlyOneMasterOf(loop.World, skill, keep);
            loop.StepOnce();
            return;
        }
    }

    /// <summary>Kill every living master of a skill except one.</summary>
    /// <remarks>
    /// <b>The village makes masters of its own accord</b> — 15 to 19 a century, measured — so a
    /// fixture that only removes the ones it posed is not posing what it thinks it is.
    /// </remarks>
    private static void LeaveOnlyOneMasterOf(SimWorld world, SkillRow skill, Villager keep)
    {
        foreach (Villager villager in world.Villagers)
        {
            if (villager.Alive
                && villager.Id != keep.Id
                && villager.FindProgressIn(skill.Id) is { Mastered: true })
            {
                villager.Alive = false;
            }
        }
    }

    private static Villager MakeThemAMaster(SimWorld world, Villager villager, SkillRow skill)
    {
        SkillProgress progress = villager.ProgressIn(skill.Id);
        progress.Work = world.Config.MasteryWorkFor(skill);
        progress.Ticks = world.Config.MasteryYearsFor(skill) * world.Config.TicksPerYear;
        progress.Mastered = true;
        return villager;
    }

    private static void StepAYear(SimLoop loop, SimConfig config)
    {
        for (int i = 0; i < config.TicksPerYear; i++)
        {
            loop.StepOnce();
        }
    }

    private static int CountWarnings(InMemoryLogSink sink, Villager about) =>
        sink.Entries.Count(e =>
            e.Message.Contains("the only soul in the village", System.StringComparison.Ordinal)
            && e.Message.Contains(about.Name, System.StringComparison.Ordinal));
}

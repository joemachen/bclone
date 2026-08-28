using Bclone.Sim.Config;
using Bclone.Sim.Core;
using Bclone.Sim.Logging;
using Bclone.Sim.World;

namespace Bclone.Sim.Systems;

/// <summary>
/// Step 11 of the tick order: <b>people get better at what they keep doing</b>
/// (`specs/skills-catalog.md`, Phase 3, landing 1).
/// </summary>
/// <remarks>
/// <para>
/// <b>⭐⭐ THIS IS THE SUBSTRATE AND NOTHING READS IT YET</b> (§11.2.1). Proficiency accrues, is
/// hashed and is visible; **no behaviour anywhere consults it.** Landing 2 makes it bite —
/// duration first, yield second (§3.3) — and landing them apart is what makes a regression
/// attributable.
/// </para>
/// <para>
/// <b>⭐⭐ NOTHING HERE EVER TAKES ANYTHING AWAY (D183, Joe: *"let's give to the player, not
/// punish or decay"*).</b> Skill decay was built, measured and deleted inside one phase — see
/// <see cref="SkillProgress"/> for the measurement that killed it. **Proficiency only ever goes
/// up**, and the suite asserts that as an invariant rather than leaving it as a policy anybody
/// has to remember.
/// </para>
/// <para>
/// <b>Last in the order on purpose.</b> It reads who holds which job *after* the labour system
/// has allocated and the behaviour system has acted, so a tick is credited to the trade the
/// villager actually held during it, in the state they actually spent it in. Appending rather
/// than inserting also leaves every existing system's relative order untouched (D5).
/// </para>
/// </remarks>
public sealed class SkillSystem : ISimSystem
{
    public string Name => "skill";

    public void Execute(SimWorld world)
    {
        ArgumentNullException.ThrowIfNull(world);

        SimConfig config = world.Config;
        if (config.Skills.Count == 0)
        {
            return;
        }

        for (int i = 0; i < world.Villagers.Count; i++)
        {
            Advance(world, world.Villagers[i], config);
        }

        // ⭐⭐ AND ONCE A YEAR, WHO IS ABOUT TO TAKE SOMETHING WITH THEM (§7, `DESIGN.md §2.1`).
        // *"Mabel is 68 and the only soul who knows herbalism."* — the sentence §2.1 demands, and
        // the last outstanding item in §11's Definition of Done.
        //
        // ⭐ ANNUAL, NOT PER TICK, AND THE CADENCE IS THE DESIGN. This is a warning about a
        // lifetime; checking it four times a day would cost a sweep of the whole roster per tick
        // to say something that changes once in a generation. It is also why the sweep is
        // affordable at all — see `SimWorld.SayWhatKnowledgeIsAtRisk`, which explains why it is
        // swept rather than triggered.
        if (world.Tick > 0UL
            && config.TicksPerYear > 0
            && world.Tick % (ulong)config.TicksPerYear == 0UL)
        {
            world.SayWhatKnowledgeIsAtRisk();
        }
    }

    private static void Advance(SimWorld world, Villager villager, SimConfig config)
    {
        if (!villager.Alive)
        {
            return;
        }

        // ⭐⭐ A TICK COUNTS WHILE THEY HOLD THE TRADE, NOT ONLY WHILE MID-ACTION (§3.6, D181).
        // The tempting reading of §3.1's "time spent doing it" is to count only the ticks of
        // `gather_ticks`/`sow_ticks` somebody is actually swinging. **§3.3b's own arithmetic
        // rules it out** — "a child born in year 1 works at twelve and masters at thirty-two"
        // is twenty CALENDAR years, and nobody is mid-action every tick.
        JobKind? held = HeldTrade(world, villager);
        if (held is not JobKind trade)
        {
            return;
        }

        // ⭐ AND A TICK OUT ON THE WORK IS WORTH MORE THAN A TICK WAITING FOR IT (D183, Joe).
        // A forester who is out felling learns faster than one sitting at home because the hut
        // has no logs — but the second is still a forester, and still gaining. **Both directions
        // matter:** crediting only active ticks would punish a player whose supply chain
        // stutters, and crediting them equally would make an idle trade as good as a worked one.
        int worth = OutOnTheWork(villager.State)
            ? config.SkillWorkPerActiveTick
            : config.SkillWorkPerIdleTick;

        for (int i = 0; i < config.Skills.Count; i++)
        {
            SkillRow skill = config.Skills[i];
            if (skill.GrownBy != trade)
            {
                continue;
            }

            // ⭐⭐ AND A YOUTH BESIDE A MASTER LEARNS FASTER — §2.1's whole point, and the last
            // thing Phase 3 owed it (`skills-catalog.md §5.1a`, D202). *"That skill dies with
            // the person unless an elder apprentices a youth."*
            //
            // ⛔ THE MASTER PAYS NOTHING (Joe, D202, following D183's *give, never take*). This
            // adds to the learner and takes from nobody, which is why there is no policy dial:
            // a control with nothing to trade off is a switch, and §5.3's dial was only ever
            // worth having if teaching cost something.
            //
            // ⭐ NOBODY IS ASSIGNED TO ANYBODY. The pair is *noticed*, not made — the player says
            // how many hands a workplace gets and the sim says who (D51, D62, D106), and a
            // per-pair screen would be the slotting UI §2.2 exists to delete. **The player's
            // lever is staffing**, and §7's at-risk line is what tells them to use it.
            Grow(world, villager, skill, config, worth + ApprenticeBonus(world, villager, skill, worth));
        }
    }

    /// <summary>
    /// What this villager gains for working beside a master of the same trade — <b>zero unless
    /// one is actually standing there</b>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>The same workplace, not merely the same trade.</b> §5.1 says *"working alongside"*, and
    /// the stricter reading is the one that means something: it makes **where the player puts
    /// people** the thing that decides whether knowledge passes on, which is the same lesson the
    /// farm and the market both landed on this week.
    /// </para>
    /// <para>
    /// <b>⛔ A master learns nothing from another master</b>, so this cannot inflate the very
    /// people who need it least — and a trade with one seat gets nothing at all, which is a real
    /// hole the library is meant to fill later (D196) rather than something to paper over here.
    /// </para>
    /// </remarks>
    private static int ApprenticeBonus(
        SimWorld world, Villager learner, SkillRow skill, int worth)
    {
        int bonus = world.Config.ApprenticeLearningBonusPercent;
        if (bonus <= 0 || learner.FindProgressIn(skill.Id) is { Mastered: true })
        {
            return 0;
        }

        for (int i = 0; i < world.Villagers.Count; i++)
        {
            Villager other = world.Villagers[i];

            if (other.Alive
                && other.Id != learner.Id
                && other.WorkplaceId == learner.WorkplaceId
                && other.FindProgressIn(skill.Id) is { Mastered: true })
            {
                return worth * bonus / 100;
            }
        }

        return 0;
    }

    /// <summary>The trade this villager currently holds, or null if they hold none.</summary>
    /// <remarks>
    /// <b>⛔ A LABORER HOLDS NO TRADE, AND THAT IS THE POINT</b> (§4.2, D66). A laborer is *"the
    /// villagers no job currently wants"* — a position in the priority order, not a profession
    /// (D87) — so **a skill in being spare is a contradiction**, and crediting one would quietly
    /// make the fallback a career.
    /// </remarks>
    private static JobKind? HeldTrade(SimWorld world, Villager villager)
    {
        if (!villager.CanWork || !villager.HasJob)
        {
            return null;
        }

        Workplace? workplace = world.FindWorkplace(villager.WorkplaceId);

        // A site is not yet a workplace: nobody is doing the trade at a building that does not
        // stand, so nobody is getting better at it there.
        return workplace is null || workplace.IsSite ? null : workplace.Kind;
    }

    /// <summary>
    /// Whether this tick is being spent <b>out on the job</b> rather than waiting to do it.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>⭐ THE WALK IS PART OF THE WORK.</b> "Active" here is not *mid-action* — a forester who
    /// spends nine ticks walking to a stand and three felling it did twelve ticks of forestry,
    /// and counting only the three would charge them twice for the commute D112 already makes
    /// them pay. What this excludes is the states where somebody is **holding a seat and not on
    /// a trip for it**: the D147 hut that cannot do its job, resting, and getting warm.
    /// </para>
    /// <para>
    /// <b>⛔ <see cref="VillagerState.FetchingFromStore"/> IS NOT WORK, AND IT IS THE ONE THAT
    /// LOOKS LIKE IT.</b> It is a household member fetching their own family's supper (D30),
    /// not a job of work. A marketer's delivery is
    /// <see cref="VillagerState.DeliveringToHome"/>, and that one is.
    /// </para>
    /// <para>
    /// <b>⚠️ EVERY STATE IS LISTED, AND A TEST WALKS THE ENUM TO KEEP IT THAT WAY.</b>
    /// <see cref="VillagerState"/> has grown repeatedly, and <c>Villager.DescribeState</c>'s own
    /// history is the warning: it fell through to the raw enum name for seven of seventeen
    /// states, **every one of them added after it was written**. A compiler check was the first
    /// attempt and C# will not give one — an exhaustive switch expression still demands a
    /// <c>_</c> arm for values cast in from outside the enum (CS8524), and adding that arm
    /// silences the missing-name check too. **So this uses the same guard
    /// <c>DescribeState</c> already has**: `SkillTests.EveryVillagerStateIsDeliberatelyClassified`
    /// walks all of them and fails on one nobody has ruled on. <c>internal</c> so it can.
    /// </para>
    /// </remarks>
    internal static bool OutOnTheWork(VillagerState state) => state switch
    {
        // Holding a seat, but not on a trip for it.
        VillagerState.Idle => false,
        VillagerState.Resting => false,
        VillagerState.SeekingShelter => false,
        VillagerState.FetchingFromStore => false,
        VillagerState.Dead => false,

        // Out on it — the walk there, the work itself, and the load home.
        VillagerState.TravelingToFood => true,
        VillagerState.Gathering => true,
        VillagerState.TravelingHome => true,
        VillagerState.TravelingToTrees => true,
        VillagerState.Cutting => true,
        VillagerState.TravelingToHut => true,
        VillagerState.MakingFirewood => true,
        VillagerState.HaulingToStore => true,
        VillagerState.CollectingForMarket => true,
        VillagerState.DeliveringToHome => true,
        VillagerState.FetchingMaterials => true,
        VillagerState.Building => true,
        VillagerState.Clearing => true,
        VillagerState.TidyingGround => true,

        // Clearing a store out is work, and it is nobody's TRADE -- like tidying a load off the
        // ground, it is what somebody does when the village has asked for something doing. It
        // grows no skill of its own because there is no skill of carrying boxes, which is the
        // same ruling `TidyingGround` already carries.
        VillagerState.ClearingAStore => true,
        VillagerState.TravelingToField => true,
        VillagerState.Sowing => true,
        VillagerState.Reaping => true,
        VillagerState.HaulingToFarm => true,

        // Stocking the market is a marketer out on their round like any other leg (§14.8).
        VillagerState.StockingTheMarket => true,

        // Only reachable by casting an integer that is not a state at all. Loud rather than
        // swallowed (METHODOLOGY §4), and the walking test above is what catches a real new
        // state long before this could.
        _ => throw new ArgumentOutOfRangeException(
            nameof(state), state, "Unclassified villager state — is it work, or waiting for it?"),
    };

    private static void Grow(
        SimWorld world, Villager villager, SkillRow skill, SimConfig config, int worth)
    {
        SkillProgress progress = villager.ProgressIn(skill.Id);

        // ⭐ TWO COUNTERS, AND THE SECOND IS NOT A DUPLICATE OF THE FIRST (Joe's call, D183).
        // `Ticks` is the honest calendar fact — how long this person has held this trade — and
        // it is what the panel and the mastery line quote, so *"seventeen years in the woods"*
        // means seventeen years. `Work` is the weighted total that mastery reads, and it runs
        // ahead for somebody who is out on the job. **One counter would have had the panel
        // overstate a forager's life by about a fifth**, and this game's whole claim is that its
        // numbers mean what they say.
        progress.Ticks++;
        progress.Work += worth;

        if (progress.Mastered || progress.Work < config.MasteryWorkFor(skill))
        {
            return;
        }

        // ⭐⭐ THE LINE JOE ASKED FOR BY NAME (§3.3b, D174) — and it works from the day the
        // substrate lands, whether or not mastery is doing anything mechanical yet. One line,
        // in the village log, ON THE EDGE: the shape D123 settled and D147 restated, narrated
        // when it changes and never a standing banner.
        //
        // `Mastered` is what makes it fire ONCE (§11.6). Without it, anybody at the threshold
        // would be narrated again on the following tick, and every tick after that.
        progress.Mastered = true;

        // ⭐ AND THEY WORKED IT OUT HERE — the flag the founding never sets. This is the only
        // place it is written, which is what makes "a founding master cannot discover anything"
        // true by construction rather than by a check somebody has to remember.
        progress.MasteredHere = true;

        if (skill.MasteryLine.Length == 0)
        {
            return;
        }

        // ⭐ THEIR OWN YEARS, NOT THE CONFIG'S. `mastery_years` is what the *work* is measured
        // against; how long it actually took this person depends on how much of it they spent
        // out on the job. Quoting the config number would have the log say "twenty years" about
        // somebody who did it in seventeen — and the panel one click away would disagree.
        int years = config.TicksPerYear <= 0 ? 0 : progress.Ticks / config.TicksPerYear;

        world.Narrate(string.Format(
            System.Globalization.CultureInfo.InvariantCulture,
            skill.MasteryLine,
            villager.Name,
            years), LogCategory.Discovery);
    }
}

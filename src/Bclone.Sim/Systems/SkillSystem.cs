using Bclone.Sim.Config;
using Bclone.Sim.Core;
using Bclone.Sim.World;

namespace Bclone.Sim.Systems;

/// <summary>
/// Step 11 of the tick order: <b>people get better at what they keep doing, and rustier at what
/// they left</b> (`specs/skills-catalog.md`, Phase 3, landing 1).
/// </summary>
/// <remarks>
/// <para>
/// <b>⭐⭐ THIS IS THE SUBSTRATE AND NOTHING READS IT YET</b> (§11.2.1). Proficiency accrues, is
/// hashed and is visible; **no behaviour anywhere consults it.** Landing 2 makes it bite —
/// duration first, yield second (§3.3) — and landing them apart is what makes a regression
/// attributable, which is D157's own lesson about a hash being evidence only about the code it
/// executes.
/// </para>
/// <para>
/// <b>⛔ AND LANDING 2 IS NOT OPTIONAL, WHICH IS WHY IT FOLLOWS IMMEDIATELY.</b> A system that
/// accrues, is visible and changes nothing is **the exact shape of D56's clothing** — measured as
/// a no-op over 300 years and blocked for it. The thing that keeps this honest is that mastery is
/// gated by nothing (D177): twenty years on the task is twenty years on the task, and no tech
/// node permits it.
/// </para>
/// <para>
/// <b>Last in the order on purpose.</b> It reads who holds which job *after* the labour system
/// has allocated and the behaviour system has acted, so a tick is credited to the trade the
/// villager actually held during it. Appending rather than inserting also leaves every existing
/// system's relative order untouched, which is D5's contract.
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

        // ⚠️ THE YEAR EDGE, TAKEN THE WAY `ClockSystem` TAKES THE SEASON EDGE rather than by
        // a modulo on the tick. Two ways of asking what year it is would eventually disagree,
        // and this one cannot: it is the same `SimClock.FromTick` the calendar on screen uses.
        bool yearTurned = world.Tick > 0UL
            && world.Clock.Year != SimClock.FromTick(world.Tick - 1UL, config).Year;

        for (int i = 0; i < world.Villagers.Count; i++)
        {
            Advance(world, world.Villagers[i], yearTurned);
        }
    }

    private static void Advance(SimWorld world, Villager villager, bool yearTurned)
    {
        if (!villager.Alive)
        {
            return;
        }

        SimConfig config = world.Config;

        // ⭐⭐ A TICK COUNTS WHILE THEY HOLD THE TRADE, NOT ONLY WHILE MID-ACTION (§3.6, D181).
        // The tempting reading of §3.1's "time spent doing it" is to count only the ticks of
        // `gather_ticks`/`sow_ticks` somebody is actually swinging. **§3.3b's own arithmetic
        // rules it out** — "a child born in year 1 works at twelve and masters at thirty-two"
        // is twenty CALENDAR years, and nobody is mid-action every tick.
        //
        // It also refuses a feedback loop nobody designed: landing 2 makes skill shorten the
        // action, so under the tight reading a master would spend fewer ticks mid-action per
        // trip and accrue MORE SLOWLY the better they got.
        JobKind? held = HeldTrade(world, villager);

        for (int i = 0; i < config.Skills.Count; i++)
        {
            SkillRow skill = config.Skills[i];

            if (held is JobKind trade && skill.GrownBy == trade)
            {
                Grow(world, villager, skill, config);
                continue;
            }

            // Decay is a year's business, not a tick's — a rate slow enough to be gentle
            // (§3.4) cannot be expressed as an integer subtracted every tick, and rounding it
            // into one would be a float in a sim-critical path (D2).
            if (yearTurned)
            {
                Fade(villager, skill, config);
            }
        }
    }

    /// <summary>The trade this villager currently holds, or null if they hold none.</summary>
    /// <remarks>
    /// <b>⛔ A LABORER HOLDS NO TRADE, AND THAT IS THE POINT</b> (§4.2, D66). A laborer is *"the
    /// villagers no job currently wants"* — a position in the priority order, not a profession
    /// (D87) — so **a skill in being spare is a contradiction**, and crediting one would quietly
    /// make the fallback a career. A villager between jobs simply gains nothing that tick.
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

    private static void Grow(SimWorld world, Villager villager, SkillRow skill, SimConfig config)
    {
        SkillProgress progress = villager.ProgressIn(skill.Id);
        progress.Ticks++;

        if (progress.Mastered || progress.Ticks < config.MasteryTicks)
        {
            return;
        }

        // ⭐⭐ THE LINE JOE ASKED FOR BY NAME (§3.3b, D174) — and it works from the day the
        // substrate lands, whether or not mastery is doing anything mechanical yet. One line,
        // in the village log, ON THE EDGE: the shape D123 settled and D147 restated, narrated
        // when it changes and never a standing banner.
        //
        // `Mastered` is what makes it fire ONCE (§11.6). Without it, somebody who masters,
        // moves trades, decays back under the threshold and returns would be narrated twice.
        progress.Mastered = true;

        if (skill.MasteryLine.Length > 0)
        {
            world.Narrate(string.Format(
                System.Globalization.CultureInfo.InvariantCulture,
                skill.MasteryLine,
                villager.Name,
                config.MasteryYears));
        }
    }

    /// <summary>
    /// A year away from a trade, and what it costs — <b>not to zero, and not fast</b> (§3.4).
    /// </summary>
    /// <remarks>
    /// <b>The rate is derived against <c>labour_reshuffle_years</c>, not picked</b> (§12, D16).
    /// The village moves people on every three years, so one full cycle spent elsewhere must
    /// cost less than it bought — otherwise the allocator is a trap and the player starts
    /// fighting a system that exists to save them work (§1.2, D51).
    /// </remarks>
    private static void Fade(Villager villager, SkillRow skill, SimConfig config)
    {
        // ⚠️ `FindProgressIn`, NOT `ProgressIn`. Reading must never create an entry, or every
        // villager would grow six zeroed rows on their first year and the structure would stop
        // being sparse — which is the whole of §8's no-op contract.
        SkillProgress? progress = villager.FindProgressIn(skill.Id);
        if (progress is null || progress.Ticks <= config.SkillFloorTicks)
        {
            return;
        }

        int lost = config.TicksPerYear / config.SkillDecayYearsPerYearLost;
        progress.Ticks = Math.Max(config.SkillFloorTicks, progress.Ticks - lost);
    }
}

using System.Collections.Generic;
using Bclone.Sim.Config;
using Bclone.Sim.Core;
using Bclone.Sim.World;

namespace Bclone.Sim.Systems;

/// <summary>
/// Villagers take work themselves. The pillar: <c>DESIGN.md §2.2</c>.
/// </summary>
/// <remarks>
/// <para>
/// This system owns only the <b>cadence</b>. The decision itself lives in
/// <see cref="LabourAllocator"/>, specified in <c>specs/labour-allocation.md</c>.
/// </para>
/// <para>
/// There is deliberately <b>no public way for a caller to assign a villager to a
/// workplace</b> (D15). Not a discouraged API — an absent one. The Banished pattern
/// this deletes is slotting N workers into a building and teleporting their brains
/// there, and the surest way not to drift back toward it is to make it unexpressible.
/// </para>
/// <para>
/// <b>Two rhythms, deliberately:</b>
/// </para>
/// <list type="bullet">
///   <item>
///     <b>Every <c>labour_reshuffle_years</c> the village shares out all its work again
///     from scratch</b> — three years as shipped (D20, revised by D46). Workers drift
///     toward the jobs nearest where they live, and a household whose forager died — or
///     who built a house on the far side of the valley — is corrected without any rule
///     having to anticipate it.
///   </item>
///   <item>
///     <b>Every <c>labour_slack_ticks</c>, whoever is idle takes any opening</b> — ten days as
///     shipped (D200, Joe: *"30 days feels unresponsive"*). Food is stored per household (D14),
///     so a household left with nobody working cannot wait until next spring.
///     <para>
///     <b>⚠️ THIS USED TO CLAIM IT "NEVER MOVES SOMEONE WHO ALREADY HAS A JOB", AND THAT WAS
///     NEVER TRUE</b> — found by writing a guard for the claim (D200). <c>ShedSurplus</c>
///     releases a villager the village no longer wants and <c>Match</c> puts them into an
///     opening <em>in the same pass</em>, so from outside it is one move from trade to trade.
///     Measured at the OLD seasonal cadence: <b>67, 78 and 83</b> such moves over fifty years
///     on three seeds. <b>The behaviour is right and the sentence was wrong</b> — *the village
///     wanted fewer foragers, so Agnes was let go and took the open builder's seat the same
///     day* is exactly what this pass is for.
///     </para>
///     <para>
///     What it genuinely does not do is <em>reconsider</em> a job nobody has asked it to: a
///     villager whose trade is still wanted is left alone, and that is what keeps their stated
///     reason true between reshuffles.
///     </para>
///   </item>
/// </list>
/// <para>
/// Reassignment is emphatically not a per-tick decision. Villagers do not reconsider
/// their livelihood four times a day, and re-running the match every tick would churn
/// assignments until the stated reasons meant nothing.
/// </para>
/// </remarks>
public sealed class LabourSystem : ISimSystem
{
    public string Name => "labour";

    /// <summary>
    /// How often the slack pass runs, in ticks — <b>a season unless the config says otherwise</b>.
    /// </summary>
    /// <remarks>
    /// <b>Defaulted rather than required</b>, so every fixture written before
    /// <c>labour_slack_ticks</c> existed keeps the cadence it was measured at, and the key is a
    /// change the shipped file makes rather than one every config has to answer.
    /// </remarks>
    private static int SlackInterval(SimConfig config) =>
        config.LabourSlackTicks > 0 ? config.LabourSlackTicks : config.TicksPerSeason;

    public void Execute(SimWorld world)
    {
        ArgumentNullException.ThrowIfNull(world);

        SimConfig config = world.Config;

        // Tick 0 lands on both boundaries; the reshuffle is the right one to run,
        // because at tick 0 there is nothing to preserve anyway.
        ulong reshuffleInterval = (ulong)config.TicksPerYear * (ulong)config.LabourReshuffleYears;
        if (world.Tick % reshuffleInterval == 0UL)
        {
            LabourAllocator.Reshuffle(world);
            SayIfWorkIsGoingUndone(world);
            return;
        }

        if (world.Tick % (ulong)SlackInterval(config) == 0UL)
        {
            LabourAllocator.TakeUpSlack(world);
            SayIfWorkIsGoingUndone(world);
            return;
        }

        // A DEATH IS NOT SOMETHING THE VILLAGE WAITS OUT (D47).
        //
        // The seasonal pass above already fills openings, but waiting up to a season
        // for it is how a settlement limps: the one person who split logs dies in
        // early winter and the hut stands empty until spring. That was tolerable while
        // work was shared out every year; at every three years (D46) it is not, and
        // this is the half of that trade that makes the slower cadence affordable.
        //
        // <b>Detected rather than signalled, deliberately.</b> Asking "is anyone dead
        // still holding a job?" needs no flag on the world, nothing to hash, and
        // nothing that can be set and not cleared — the question is answered from the
        // state itself, so it is self-correcting and cannot drift. That matters more
        // than the loop costs: this project's recurring bug is code reading state from
        // where it used to live, and a bookkeeping flag is exactly that shape.
        if (AnyVacancyLeftByTheDead(world))
        {
            LabourAllocator.TakeUpSlack(world);
            SayIfWorkIsGoingUndone(world);
        }
    }

    /// <summary>
    /// Tell the village log when work the village wants has nobody on it — <b>once</b>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Joe moved this out of the Overview panel and into the log</b> (2026-08-10), which
    /// makes it an <em>event</em> where D47 built it as a standing <em>state</em>. So it
    /// needs an edge: the panel could recompute the answer every frame and show it while it
    /// was true, and a log line has to be written exactly at the moment it becomes true or
    /// it is either missing or repeated four times a second.
    /// </para>
    /// <para>
    /// <b>Said again when it clears</b>, and that is not symmetry for its own sake: a line
    /// that only ever reports bad news leaves the player wondering whether they fixed it.
    /// The pair is what makes the log answer <em>"is that still going on?"</em> without a
    /// panel having to sit there saying so.
    /// </para>
    /// <para>
    /// Checked only after a labour pass has actually moved somebody, so it costs a
    /// <see cref="LabourQuota"/> on the ticks that were already doing one — never per tick.
    /// </para>
    /// </remarks>
    private static void SayIfWorkIsGoingUndone(SimWorld world)
    {
        IReadOnlyList<Workplace> idle = UnmannedWork(world);

        if (idle.Count == 0)
        {
            if (world.WorkIsGoingUndone)
            {
                world.WorkIsGoingUndone = false;
                world.Narrate("Every job the village wants doing has somebody on it again.");
            }

            world.WorkSeenUndoneOnce = false;
            return;
        }

        if (world.WorkIsGoingUndone)
        {
            return;
        }

        // ⚠️ SAID ON THE SECOND SIGHTING, NOT THE FIRST, AND THE FIRST VERSION OF THIS WAS
        // MEASURED AS NOISE. Narrating the moment the state appeared put **56 lines into a
        // 45-year life** — 28 begins and 28 clears — and tripped the guard that asks whether
        // a life still reads as a story rather than a receipt (§1.4). It was catching a
        // TRANSIENT: at a season boundary the quota jumps (a berry patch is wanted again in
        // spring) and staffing catches up a pass later, so the village was reporting a
        // problem it had already solved by the time anybody read it.
        //
        // One labour pass of patience costs nothing — a real shortage is still true next
        // pass, and D47's whole point is the shortage nobody notices, which lasts. The flag
        // resets whenever the work is covered, so two separate flickers never add up to a
        // sighting.
        if (!world.WorkSeenUndoneOnce)
        {
            world.WorkSeenUndoneOnce = true;
            return;
        }

        world.WorkIsGoingUndone = true;
        world.Narrate(
            $"Nobody is working {NameThem(idle)} — and the village wants it done. "
            + $"There is no one spare to send. {world.Clock.SeasonAndYear()}.");
    }

    /// <summary>
    /// The idle workplaces, as something a person would say out loud.
    /// </summary>
    /// <remarks>
    /// Moved down from the view with the alert. It named at most a few places and then said
    /// "and N more", because a sentence listing eleven huts is a sentence nobody finishes.
    /// </remarks>
    private static string NameThem(IReadOnlyList<Workplace> idle)
    {
        const int MostToName = 4;

        var names = new List<string>();
        for (int i = 0; i < idle.Count && i < MostToName; i++)
        {
            names.Add(idle[i].Name);
        }

        string listed = names.Count == 1
            ? names[0]
            : string.Join(", ", names.Take(names.Count - 1)) + " and " + names[^1];

        int rest = idle.Count - names.Count;
        return rest > 0 ? $"{listed} and {rest} more" : listed;
    }

    /// <summary>Whether a dead villager is still holding a job nobody has taken.</summary>
    private static bool AnyVacancyLeftByTheDead(SimWorld world)
    {
        for (int i = 0; i < world.Villagers.Count; i++)
        {
            Villager villager = world.Villagers[i];
            if (!villager.Alive && villager.HasJob)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Work the village wants doing that nobody is doing — for the shell to show.
    /// </summary>
    /// <remarks>
    /// The other half of D47. Filling a vacancy at once is only half an answer,
    /// because sometimes there is nobody spare to fill it — and a workplace standing
    /// empty is currently <em>silent</em>, which is the one thing a legibility-first
    /// game cannot do (§1.1). A reader, not a writer: it computes from world state on
    /// demand rather than being maintained, for the same reason as above.
    /// </remarks>
    public static IReadOnlyList<Workplace> UnmannedWork(SimWorld world)
    {
        ArgumentNullException.ThrowIfNull(world);

        LabourQuota quota = LabourQuota.For(world);
        var idle = new List<Workplace>();

        for (int i = 0; i < world.Workplaces.Count; i++)
        {
            Workplace workplace = world.Workplaces[i];

            // A construction site is never manned now (D108) — builders hold their job at
            // the hut and treat sites as errands — so every site the player has marked would
            // report itself here, permanently and by design. That is the nag D42 refuses:
            // an alert that is always on is an alert nobody reads. The build queue panel is
            // where a waiting site says where it stands.
            if (workplace.IsSite)
            {
                continue;
            }

            // Only work the village actually wants done. A berry patch with nobody at
            // it in winter is not a problem, it is winter — and crying about it would
            // train the player to ignore the warning that matters.
            if (workplace.WorkerIds.Count == 0 && quota.For(workplace.Kind) > 0)
            {
                idle.Add(workplace);
            }
        }

        return idle;
    }

    // Two one-line passthroughs to LabourAllocator used to sit here — CostToWork and
    // InCatchment — kept public on the stated grounds that "the view layer and the tests
    // both ask it". The view layer never asked. Tests reach LabourAllocator directly
    // through InternalsVisibleTo, so the wrappers were public API maintained for nobody.
}

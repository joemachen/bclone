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
///     <b>Every season, whoever is idle takes any opening.</b> Food is stored per
///     household (D14), so a household left with nobody working cannot wait until
///     next spring. This never moves someone who already has a job, so the reason
///     they were given for holding it stays true.
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
            return;
        }

        if (world.Tick % (ulong)config.TicksPerSeason == 0UL)
        {
            LabourAllocator.TakeUpSlack(world);
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
        List<(Villager Dead, Workplace Where)> vacancies = VacanciesLeftByTheDead(world);
        if (vacancies.Count > 0)
        {
            LabourAllocator.TakeUpSlack(world);
            FillOrGiveUpTheSeat(world, vacancies);
        }
    }

    /// <summary>
    /// ⭐ A dead worker's seat is taken by a free laborer — or the profession loses it, out
    /// loud (Joe, D109).
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Joe's rule, and the second half is the one that matters:</b> <em>"a free laborer
    /// takes the empty seat; if there is none, the building's number AND the profession's
    /// total drop by one — and the timeline says so."</em> Since D109 the player owns those
    /// numbers, so a number that changes without anybody being told is the village quietly
    /// editing the player's instructions behind their back, which is the untraceable outcome
    /// §1.1 forbids. <b>Silence here would be a profession draining away over a generation
    /// while the panel went on claiming it was staffed.</b>
    /// </para>
    /// <para>
    /// <b>Only when there is nobody at all to take it.</b> A laborer who exists but cannot
    /// reach the place leaves the seat open and the number standing — that is a catchment
    /// problem the player can solve by building nearer, and dropping the number would hide it.
    /// </para>
    /// </remarks>
    private static void FillOrGiveUpTheSeat(
        SimWorld world, List<(Villager Dead, Workplace Where)> vacancies)
    {
        for (int i = 0; i < vacancies.Count; i++)
        {
            (Villager dead, Workplace where) = vacancies[i];

            // Somebody stepped in, or the place is gone entirely. Nothing to say.
            if (where.OpenPositions <= 0 || world.FindWorkplace(where.Id) is null)
            {
                continue;
            }

            if (world.Laborers > 0)
            {
                continue;
            }

            where.Staffing--;

            world.Narrate($"{dead.Name}, the {Describe(where.Kind)}, died and no laborer was "
                + $"available to replace them — {where.Name} is down to {where.Staffing} "
                + $"{(where.Staffing == 1 ? "hand" : "hands")}. {world.Clock.SeasonAndYear()}.");
        }
    }

    private static string Describe(JobKind kind) => kind switch
    {
        JobKind.Forager => "gatherer",
        JobKind.Forester => "forester",
        JobKind.Woodcutter => "woodcutter",
        JobKind.Marketer => "vendor",
        JobKind.Builder => "builder",
        _ => "worker",
    };

    /// <summary>Dead villagers still holding a job, and where they held it.</summary>
    /// <remarks>
    /// <b>Detected rather than signalled, deliberately.</b> Asking "is anyone dead still
    /// holding a job?" needs no flag on the world, nothing to hash, and nothing that can be
    /// set and not cleared — the question is answered from the state itself, so it is
    /// self-correcting and cannot drift. That matters more than the loop costs: this project's
    /// recurring bug is code reading state from where it used to live, and a bookkeeping flag
    /// is exactly that shape.
    /// </remarks>
    private static List<(Villager Dead, Workplace Where)> VacanciesLeftByTheDead(SimWorld world)
    {
        var vacancies = new List<(Villager, Workplace)>();

        for (int i = 0; i < world.Villagers.Count; i++)
        {
            Villager villager = world.Villagers[i];
            if (!villager.Alive
                && villager.HasJob
                && world.FindWorkplace(villager.WorkplaceId) is Workplace where
                && !where.IsSite)
            {
                vacancies.Add((villager, where));
            }
        }

        return vacancies;
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

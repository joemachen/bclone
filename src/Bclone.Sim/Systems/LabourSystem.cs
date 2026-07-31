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
///     <b>Once a year the village shares out all its work again from scratch</b>
///     (D20). Workers drift toward the jobs nearest where they live, and a household
///     whose forager died — or who built a house on the far side of the valley — is
///     corrected without any rule having to anticipate it.
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
        if (AnyVacancyLeftByTheDead(world))
        {
            LabourAllocator.TakeUpSlack(world);
        }
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

    /// <summary>
    /// Travel cost from a villager's home to a workplace, from the one shared field.
    /// </summary>
    /// <remarks>Kept public because the view layer and the tests both ask it. Reading
    /// a distance is not assigning a job.</remarks>
    public static int CostToWork(SimWorld world, Villager villager, Workplace workplace) =>
        LabourAllocator.CostBetween(world, villager, workplace);

    /// <summary>Whether a villager's home is inside a workplace's catchment.</summary>
    public static bool InCatchment(SimWorld world, Villager villager, Workplace workplace) =>
        LabourAllocator.InCatchment(world, villager, workplace);
}

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
        }
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

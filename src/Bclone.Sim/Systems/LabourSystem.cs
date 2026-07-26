using Bclone.Sim.Config;
using Bclone.Sim.Core;
using Bclone.Sim.World;

namespace Bclone.Sim.Systems;

/// <summary>
/// Villagers take work themselves. The pillar: <c>DESIGN.md §2.2</c>.
/// </summary>
/// <remarks>
/// <para>
/// There is deliberately <b>no public way for a caller to assign a villager to a
/// workplace.</b> Not a discouraged API — an absent one. The Banished pattern this
/// deletes is slotting N workers into a building and teleporting their brains there,
/// and the surest way not to drift back toward it is to make it unexpressible.
/// </para>
/// <para>
/// <b>The rule, in the order it is applied:</b>
/// </para>
/// <list type="number">
///   <item>The villager must be able to work — not a child, not dead.</item>
///   <item>The workplace must still want someone.</item>
///   <item>The workplace must be within its catchment of the villager's home,
///         measured in travel cost from the one shared field.</item>
///   <item>Nearest home wins.</item>
///   <item>Ties break by villager id — explicitly, because a tie resolved by
///         iteration order is a desync waiting to happen (spec §4b).</item>
/// </list>
/// <para>
/// A ranked list rather than a weighted score, for one reason: the player has to be
/// able to click a villager and get a straight answer. A score can be computed but
/// not explained. Every assignment records that answer in
/// <see cref="Villager.JobReason"/>, naming the runner-up where there was one.
/// </para>
/// </remarks>
public sealed class LabourSystem : ISimSystem
{
    public string Name => "labour";

    public void Execute(SimWorld world)
    {
        SimConfig config = world.Config;

        // Reassignment is not a per-tick decision. Villagers do not reconsider their
        // livelihood four times a day, and re-running the match every tick would
        // churn assignments and make the reason strings meaningless.
        if (world.Tick % (ulong)config.TicksPerSeason != 0UL)
        {
            return;
        }

        ReleaseUnfitWorkers(world);
        FillOpenPositions(world);
    }

    /// <summary>
    /// Give up jobs held by the dead, the grown-down, and anyone whose home has moved
    /// out of catchment.
    /// </summary>
    private static void ReleaseUnfitWorkers(SimWorld world)
    {
        for (int i = 0; i < world.Villagers.Count; i++)
        {
            Villager villager = world.Villagers[i];
            if (!villager.HasJob)
            {
                continue;
            }

            Workplace? workplace = world.FindWorkplace(villager.WorkplaceId);
            if (workplace is null)
            {
                Release(villager, "their workplace no longer exists");
                continue;
            }

            if (!villager.CanWork)
            {
                workplace.WorkerIds.Remove(villager.Id);
                Release(villager, villager.Alive ? "too young to work" : "died");
                continue;
            }

            if (!InCatchment(world, villager, workplace))
            {
                workplace.WorkerIds.Remove(villager.Id);
                Release(villager, $"moved too far from {workplace.Name} to keep working there");
            }
        }
    }

    private static void Release(Villager villager, string why)
    {
        villager.WorkplaceId = 0;
        villager.JobReason = $"No work: {why}.";
    }

    private static void FillOpenPositions(SimWorld world)
    {
        // Workplaces in id order, so which place fills first is a fact about the
        // village rather than about iteration.
        for (int w = 0; w < world.Workplaces.Count; w++)
        {
            Workplace workplace = world.Workplaces[w];

            while (!workplace.IsFullyStaffed)
            {
                Villager? best = null;
                Villager? runnerUp = null;
                int bestCost = int.MaxValue;
                int runnerUpCost = int.MaxValue;

                for (int i = 0; i < world.Villagers.Count; i++)
                {
                    Villager candidate = world.Villagers[i];
                    if (candidate.HasJob || !candidate.CanWork)
                    {
                        continue;
                    }

                    int cost = CostToWork(world, candidate, workplace);
                    if (cost > workplace.CatchmentRadius)
                    {
                        continue;
                    }

                    // Strictly-less-than, walking villagers in id order, means the
                    // lowest id wins a tie without needing a second comparison.
                    if (cost < bestCost)
                    {
                        runnerUp = best;
                        runnerUpCost = bestCost;
                        best = candidate;
                        bestCost = cost;
                    }
                    else if (cost < runnerUpCost)
                    {
                        runnerUp = candidate;
                        runnerUpCost = cost;
                    }
                }

                if (best is null)
                {
                    break;
                }

                Assign(world, best, workplace, bestCost, runnerUp, runnerUpCost);
            }
        }
    }

    private static void Assign(
        SimWorld world,
        Villager villager,
        Workplace workplace,
        int cost,
        Villager? runnerUp,
        int runnerUpCost)
    {
        villager.WorkplaceId = workplace.Id;
        workplace.WorkerIds.Add(villager.Id);

        int tiles = cost / TravelCostField.BaseTileCost;
        string reason = $"Took work at {workplace.Name} — nearest able worker, {tiles} tiles from home.";

        if (runnerUp is not null)
        {
            int runnerUpTiles = runnerUpCost / TravelCostField.BaseTileCost;
            reason += runnerUpCost == cost
                ? $" {runnerUp.Name} was equally close; the tie went to the elder claim."
                : $" Next nearest was {runnerUp.Name} at {runnerUpTiles} tiles.";
        }

        villager.JobReason = reason;
    }

    /// <summary>
    /// Travel cost from a villager's home to a workplace, from the one shared field.
    /// </summary>
    /// <remarks>
    /// Measured from <em>home</em>, not from wherever they happen to be standing.
    /// A job is a daily commute, and using current position would make assignment
    /// flicker as people walk about.
    /// </remarks>
    public static int CostToWork(SimWorld world, Villager villager, Workplace workplace) =>
        world.TravelCost.Cost(world.HomeOf(villager), workplace.Position);

    /// <summary>Whether a villager's home is inside a workplace's catchment.</summary>
    public static bool InCatchment(SimWorld world, Villager villager, Workplace workplace) =>
        CostToWork(world, villager, workplace) <= workplace.CatchmentRadius;
}

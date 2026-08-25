using Bclone.Sim.World;

namespace Bclone.Sim.Tests;

/// <summary>
/// Paying for a construction site outright, so a test about something else can skip the haul.
/// </summary>
/// <remarks>
/// <b>⭐ ONE HELPER RATHER THAN SIX COPIES OF <c>Deliver(Recipe.Logs)</c></b> (D213). Every one of
/// those copies named a single good, so pricing stone into a recipe would have left them each
/// delivering the timber and silently leaving the site short — six tests failing for a reason
/// none of them is about. What they all mean is <em>"assume this was built"</em>, and that is
/// what this says.
/// </remarks>
internal static class BuildFixtures
{
    /// <summary>Deliver everything a site is still waiting for, whatever its recipe asks.</summary>
    internal static void StockTheSite(Workplace site)
    {
        ConstructionSite plan = site.Construction!;

        for (int i = 0; i < plan.Recipe.Materials.Count; i++)
        {
            MaterialCost cost = plan.Recipe.Materials[i];
            plan.Deliver(cost.Goods, plan.StillNeeded(cost.Goods));
        }
    }
}

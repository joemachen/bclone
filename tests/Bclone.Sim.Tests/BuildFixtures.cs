using Bclone.Sim.Config;
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
    /// <summary>
    /// The longhouse as it was before D566 — a three-by-one warehouse — typed as a modder's row, so the footprint
    /// and tile-finder guards keep the one multi-tile store they were written against. The shipped row is a dwelling.
    /// </summary>
    internal static SimConfig WithTheOldLonghouseStore(SimConfig config)
    {
        var rows = new List<BuildingRow>(config.BuildingRows);
        int i = rows.FindIndex(r => r.Id == (int)BuildingKind.Longhouse);
        rows[i] = rows[i] with
        {
            Materials = new[] { new MaterialCost(Goods.Logs, config.WarehouseLogs), new MaterialCost(Goods.Stone, config.WarehouseStone) },
            WorkTicks = config.WarehouseWorkTicks,
            Stores = StoreKind.Warehouse,
            StoreCapacity = VillageEconomy.WarehouseCapacity(config) * 3,
            HouseCapacity = 0,
            LocalStoreCap = 0,
        };
        return config with { Buildings = rows };
    }

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

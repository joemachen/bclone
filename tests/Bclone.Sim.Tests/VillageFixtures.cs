using Bclone.Sim.Config;
using Bclone.Sim.World;

namespace Bclone.Sim.Tests;

/// <summary>
/// The village config, with its food economy <b>derived</b> rather than tuned.
/// </summary>
/// <remarks>
/// <see cref="VillageEconomy"/> states the target — one adult at minimum vigour must
/// feed themselves and two children — and computes what that requires. The values
/// here are read back from it rather than guessed, so changing hunger, travel, or
/// vigour moves the economy with them instead of silently invalidating it.
/// </remarks>
public static class VillageFixtures
{
    /// <summary>Four founding adults across two households, per Joe's chosen start.</summary>
    public static SimConfig Village
    {
        get
        {
            // Start from everything except the derived food numbers.
            SimConfig shape = Phase0Fixtures.Plenty with
            {
                StartingHouseholds = 2,
                AdultsPerHousehold = 2,
                FounderAge = 20,
                AdultAge = 15,
                MaxHouseholdSize = 7,

                // A real village sprawls, and the furthest home sets the worst-case
                // round trip that the whole economy has to afford.
                EconomyHorizonHouseholds = 12,

                // Fuel back ON for the village: households to heat, and a labour
                // system for firewood to compete inside (D17, D29).
                FirewoodPerWinterDay = 1,
            };

            // Then derive the values the targets actually determine — food first,
            // then fuel, which depends on what the food target leaves spare.
            SimConfig fed = shape with
            {
                GatherYield = VillageEconomy.RequiredGatherYield(shape),
                StockpileTarget = VillageEconomy.RequiredStockpilePerAdult(shape),
            };

            return fed with
            {
                FirewoodPerSplit = VillageEconomy.RequiredFirewoodPerSplit(fed),
            };
        }
    }
}

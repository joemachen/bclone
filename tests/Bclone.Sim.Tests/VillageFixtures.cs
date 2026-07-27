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
                EconomyHorizonHouseholds = 20,

                // Fuel back ON for the village: households to heat, and a labour
                // system for firewood to compete inside (D17, D29).
                FirewoodPerWinterDay = 1,

                // A real valley, generated (D18). Phase0Fixtures.Plenty deliberately
                // describes a single fixed patch — that is Phase 0's world and it must
                // stay legible — so the village puts the generator's rules back:
                // several sites spread around a ring, which is what makes a binding
                // catchment survivable rather than merely cruel (D19, D24), and enough
                // jitter that two seeds are two places.
                ForageSiteCount = 6,
                ForageSiteRingTiles = 5,
                SiteJitterTiles = 1,
                FoundingJitterTiles = 2,
                TreeStandCount = 2,
                TreeStandRingTiles = 4,
                RiverWidthTiles = 2,
            };

            // Then derive the values the targets actually determine — food first,
            // then fuel, which depends on what the food target leaves spare.
            SimConfig fed = shape with
            {
                GatherYield = VillageEconomy.RequiredGatherYield(shape),
                StockpileTarget = VillageEconomy.RequiredStockpilePerAdult(shape),
            };

            SimConfig fuelled = fed with
            {
                FirewoodPerSplit = VillageEconomy.RequiredFirewoodPerSplit(fed),
            };

            // And the buildings have to be big enough for the village the rest of
            // this is budgeted for. Deriving the yields but not the CAPACITIES left
            // the village physically unable to make enough firewood, however many
            // hands were free.
            return fuelled with
            {
                WoodcutterHutCapacity = VillageEconomy.RequiredWoodcutterSeats(fuelled),
                TreeStandCapacity = VillageEconomy.RequiredTreeStandSeats(fuelled),
            };
        }
    }
}

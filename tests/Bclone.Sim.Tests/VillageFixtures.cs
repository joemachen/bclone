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
                // THE SHIPPED CALENDAR (D49), which this fixture did not have for four
                // commits. D49 moved `data/sim.config.json` to thirty-day seasons and
                // re-derived the economy around them; nothing moved the tests, so every
                // village test went on running a fifteen-day season and a 240-tick year
                // while the game ran 30 and 480. That is the divergence D50 lived in
                // entirely and D48 was four times worse inside.
                //
                // It blocks D45 specifically, which is how it was found: the whole point
                // of "25 days sheltered without a fire" is that it fits INSIDE a 30-day
                // winter, so an unheated house can still kill within one season. Against
                // a 15-day winter it never fires, `CauseOfDeath.Cold` goes dormant, and
                // every test written for it would be vacuous (D7).
                //
                // Phase0Fixtures keeps fifteen. That is Phase 0's world, its spec
                // describes it, and its pacing is stated against it.
                DaysPerSeason = 30,

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

                // A real valley, generated (D18). The forage sites and the tree stands
                // this used to ask for are retired — food comes from a hut the player
                // sites in woodland, and the woodland is painted across the whole valley
                // — so what is left of the generator's rules is the jitter that makes two
                // seeds two places, and a river wide enough to be in the way.
                SiteJitterTiles = 1,
                FoundingJitterTiles = 2,
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

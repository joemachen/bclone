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
                // 15 -> 12 with the shipped file (D156) — an uneducated child works at
                // twelve. Kept in step deliberately; this is the number the birth curve and
                // the labour supply both turn on.
                AdultAge = 12,
                // 7 -> 5 with the shipped file (D153). Kept in step deliberately: a cap that
                // differed between the two would be METHODOLOGY §3's drift on the one number
                // the birth gate turns on, and that has bitten five times (D48-D50, D128, D132).
                MaxHouseholdSize = 5,

                // A real village sprawls, and the furthest home sets the worst-case
                // round trip that the whole economy has to afford.
                EconomyHorizonHouseholds = 20,

                // Fuel back ON for the village: households to heat, and a labour
                // system for firewood to compete inside (D17, D29).
                FirewoodPerWinterDay = 1,

                // ⚠️ AND THE BURN INTERVAL THE GAME ACTUALLY SHIPS. Found by a probe asking
                // why a village was not breeding: the fixture wanted **43 firewood in every
                // larder** before anybody could have a child, where the shipped file wants
                // 11 — because the interval landed in `data/sim.config.json` and never here,
                // so every test in the suite was running a fuel economy four times hungrier
                // than the game. That gap is precisely what METHODOLOGY §3 exists for and
                // what D48, D49 and D50 each were.
                FirewoodBurnIntervalDays = 4,

                // A real valley, generated (D18). The forage sites and the tree stands
                // this used to ask for are retired — food comes from a hut the player
                // sites in woodland, and the woodland is painted across the whole valley
                // — so what is left of the generator's rules is the jitter that makes two
                // seeds two places, and a river wide enough to be in the way.
                SiteJitterTiles = 1,
                FoundingJitterTiles = 2,
                RiverWidthTiles = 2,

                // ⭐ The valley comes back (D126). Not optional for the village fixture:
                // without it a settlement fells its own gatherer's ring simply by living in
                // it and starves at year 45 (D125), so every long-horizon guard here would
                // be describing a world the shipped game does not have.
                // ⚠️ 60 -> 72 WITH THE SHIPPED FILE (Joe, 2026-08-25), and moved together on this
                // comment's own reasoning: a fixture left at 60 would be "describing a world the
                // shipped game does not have" -- trees coming back 20%% faster than the player
                // ever sees. That is METHODOLOGY §3's shipped-versus-fixture drift, and there is
                // already one open against food (§5); adding a second in the same change that
                // complains about the first would be indefensible.
                RegrowthPeriodDays = 72,
            };

            // Then derive the values the targets actually determine — food first,
            // then fuel, which depends on what the food target leaves spare.
            SimConfig fed = shape with
            {
                GatherYield = VillageEconomy.RequiredGatherYield(shape),
                StockpileTarget = VillageEconomy.RequiredStockpilePerAdult(shape),
            };

            // And the crop, which is derived AGAINST the gathering above rather than beside it
            // (`crops-and-orchards.md`): a farmer's year is worth a gatherer's year, so this
            // has to be taken after `GatherYield` is settled or it would be measured against
            // the placeholder. One ordering mistake here and the fixture would ship a farm
            // worth a fraction of a hut while the arithmetic still balanced.
            fed = fed with { CropYieldPerTile = VillageEconomy.RequiredCropYield(fed) };

            // ⚠️ THE SHIPPED VALUE, NOT THE DERIVED MINIMUM, and the difference is enormous.
            // `RequiredFirewoodPerSplit` answers *what is the least that works* — and using
            // the least makes fuel maximally LOG-hungry, because a smaller batch means more
            // batches and every batch costs `logs_per_split`. Measured over twelve years at
            // the derived 7: **219 of the village's 365 logs went up the chimney, 60% of all
            // timber produced**, and there was never enough left to raise anything.
            //
            // The shipped file sets 50 deliberately (Joe), which costs about 31 logs for the
            // same heat. A fixture deriving to the minimum was not a stricter test, it was a
            // different game — METHODOLOGY §3's drift, arriving through a derivation rather
            // than through a typed number.
            SimConfig fuelled = fed with
            {
                FirewoodPerSplit = ShippedConfig.Load().FirewoodPerSplit,
            };

            // And the buildings have to be big enough for the village the rest of
            // this is budgeted for. Deriving the yields but not the CAPACITIES left
            // the village physically unable to make enough firewood, however many
            // hands were free.
            return fuelled with
            {
                WoodcutterHutCapacity = VillageEconomy.RequiredWoodcutterSeats(fuelled),
                ForesterHutCapacity = VillageEconomy.RequiredForesterSeats(fuelled),
            };
        }
    }
}

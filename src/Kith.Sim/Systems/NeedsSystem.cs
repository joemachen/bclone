using Kith.Sim.Config;
using Kith.Sim.Core;
using Kith.Sim.Logging;
using Kith.Sim.World;

namespace Kith.Sim.Systems;

/// <summary>
/// Step 4 of the tick order: hunger rises.
/// </summary>
/// <remarks>
/// Also tracks how long the villager has sat at maximum hunger, which is what
/// <see cref="MortalitySystem"/> reads to decide starvation. Keeping the counter here
/// — next to the thing that increments it — means there is exactly one place hunger
/// is reasoned about.
/// </remarks>
public sealed class NeedsSystem : ISimSystem
{
    public string Name => "needs";

    public void Execute(SimWorld world)
    {
        SimConfig config = world.Config;

        // The day's water, on its first tick, as the coats wear (D778).
        if (world.Tick % (ulong)config.TicksPerDay == 0UL)
        {
            DrinkTheDaysWater(world, config);
        }

        // Always in id order — see spec §4b.
        for (int i = 0; i < world.Villagers.Count; i++)
        {
            UpdateOne(world, world.Villagers[i], config);
        }
    }

    /// <summary>
    /// ⭐ THE DAY'S DRINK (D778, `water-need.md §5` step 2): every living member of a household drinks
    /// <c>water_per_person_per_day</c> from its jar, never below empty. An empty jar costs nothing (Joe's call 3A: nobody
    /// dies of thirst) and is said, the day it empties (D780).
    /// </summary>
    /// <remarks>
    /// ⛔ A dry jar is never read past its <c>Water</c>, so a village with no well, whose jars are never filled, writes
    /// nothing and counts nobody. The living are counted once a day in one pass, and only when a jar has water.
    /// </remarks>
    private static void DrinkTheDaysWater(SimWorld world, SimConfig config)
    {
        int perPerson = config.WaterPerPersonPerDay;
        if (perPerson <= 0)
        {
            return;
        }

        Dictionary<int, int>? living = null;
        for (int i = 0; i < world.Households.Count; i++)
        {
            Household household = world.Households[i];
            if (household.Water <= 0)
            {
                continue;
            }

            living ??= LivingByHousehold(world);
            int drink = perPerson * living.GetValueOrDefault(household.Id);
            household.Water = Math.Max(0, household.Water - drink);

            // ⭐ SAID THE DAY IT RUNS DRY, AND ONLY THEN (D780, call 3A: "and says so"; Joe: "Say it, don't rush"): the jar
            // goes from water to none here and nowhere else, so the sentence is once a dry spell by construction — a jar
            // already empty is never read past its first line above. Nobody goes for it at once; the next rest at home does.
            if (household.Water == 0 && drink > 0)
            {
                world.Narrate($"The {household.Name} household has run out of water — "
                    + (household.HasHome ? "somebody will go for it the next time one of them is home." : "somebody will go for it once they have a house."));
            }
        }
    }

    /// <summary>How many of each household are alive — one pass over the villagers.</summary>
    private static Dictionary<int, int> LivingByHousehold(SimWorld world)
    {
        var living = new Dictionary<int, int>();
        for (int i = 0; i < world.Villagers.Count; i++)
        {
            Villager villager = world.Villagers[i];
            if (villager.Alive)
            {
                living[villager.HouseholdId] = living.GetValueOrDefault(villager.HouseholdId) + 1;
            }
        }

        return living;
    }

    private static void UpdateOne(SimWorld world, Villager villager, SimConfig config)
    {
        if (!villager.Alive)
        {
            return;
        }

        bool wasAtMax = villager.Hunger >= config.HungerMax;

        // ⭐ A FULL BELLY HOLDS HUNGER OFF (`food-chain.md §3.2`, D522): while the last meal was worth
        // more than it cost, hunger does not rise — the fullness counts down instead. Hunger itself
        // never goes below nought, so the invariant at the bottom of this method stands.
        if (villager.FullFor > 0)
        {
            villager.FullFor--;
        }
        else
        {
            villager.Hunger += config.HungerPerTick;
        }

        if (villager.Hunger > config.HungerMax)
        {
            villager.Hunger = config.HungerMax;
        }

        if (villager.Hunger >= config.HungerMax)
        {
            villager.TicksAtMaxHunger++;

            // Narrate once per episode, and only when the larder is genuinely bare.
            // Hitting maximum hunger with food in store is not a story beat — it is
            // a bug, and saying "nothing left to eat" while sitting on sixty food
            // would be the log lying to the player.
            // ⛔ ANYTHING EDIBLE, NOT `Goods.Produce` — AND THE COMMENT ABOVE IS WHY IT MATTERS.
            // *"Saying 'nothing left to eat' while sitting on sixty food would be the log
            // lying to the player"* — and a larder holding nothing but meat is exactly that
            // case, now that a village can live on meat and fish (D277, D283).
            if (!wasAtMax
                && world.FoodIn(world.HouseholdOf(villager).Stockpile)
                    < BehaviorSystem.MealCostFor(villager, config))
            {
                world.Narrate(
                    $"{villager.Name} has nothing left to eat — {world.Clock.SeasonAndYear()}.", LogCategory.Death);
            }
        }
        else
        {
            villager.TicksAtMaxHunger = 0;
        }

        if (villager.Hunger < 0)
        {
            // An invariant, asserted rather than assumed (METHODOLOGY.md §4).
            world.Log(LogLevel.Error, "needs",
                $"Hunger went negative ({villager.Hunger}) — clamping. This is a bug.");
            villager.Hunger = 0;
        }
    }
}

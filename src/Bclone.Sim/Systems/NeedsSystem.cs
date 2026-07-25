using Bclone.Sim.Config;
using Bclone.Sim.Core;
using Bclone.Sim.Logging;
using Bclone.Sim.World;

namespace Bclone.Sim.Systems;

/// <summary>
/// Step 2 of the tick order: hunger rises.
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
        Villager villager = world.Villager;
        if (!villager.Alive)
        {
            return;
        }

        SimConfig config = world.Config;
        bool wasAtMax = villager.Hunger >= config.HungerMax;

        villager.Hunger += config.HungerPerTick;
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
            if (!wasAtMax && world.Stockpile.Food < config.FoodPerMeal)
            {
                world.Narrate(
                    $"{villager.Name} has nothing left to eat — {world.Clock.SeasonAndYear()}.");
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

using Bclone.Sim.Config;
using Bclone.Sim.Core;
using Bclone.Sim.Logging;
using Bclone.Sim.World;

namespace Bclone.Sim.Systems;

/// <summary>
/// Step 3 of the tick order: the villager decides, then acts.
/// </summary>
/// <remarks>
/// <para>
/// Priority order, evaluated top-down every tick (spec §6):
/// </para>
/// <list type="number">
///   <item>Hungry enough to eat, and food in the store → eat.</item>
///   <item>Below the stockpile target, and it is not winter → forage.</item>
///   <item>Otherwise → rest.</item>
/// </list>
/// <para>
/// Deliberately a plain top-down if-chain rather than a utility score or behaviour
/// tree. The player has to be able to read <em>why</em> a villager did something
/// (non-negotiable 1), and a ranked list of reasons can be explained in one sentence
/// in the UI. A weighted score cannot. When this grows into real labour assignment in
/// Phase 1, that constraint carries forward.
/// </para>
/// </remarks>
public sealed class BehaviorSystem : ISimSystem
{
    public string Name => "behavior";

    public void Execute(SimWorld world)
    {
        Villager villager = world.Villager;
        villager.JustAte = false;

        if (!villager.Alive)
        {
            return;
        }

        // Eating preempts everything.
        //
        // The obvious ordering — finish the current action, then decide — produces a
        // villager who starves to death mid-gather with a full larder, because a
        // round trip to the berry patch is longer than the gap between meals. That
        // is not a hard survival choice, it is a bug that reads as one, and it would
        // wreck the legibility this phase exists to prove. The stockpile is on hand
        // (spec §11), so pausing for a bite costs one tick and the interrupted
        // action resumes untouched next tick.
        if (TryEat(world, villager))
        {
            return;
        }

        // Abandon a foraging trip the moment the season turns.
        //
        // The season is checked in Decide(), but a villager already walking to the
        // patch never returns there, so without this they forage straight through
        // winter — and the life log reports "Foraging stops" and then a gather on
        // the very next line. A log that contradicts itself is worse than no log.
        if (!FoodSource.IsGatherable(world.Clock.Season) && IsForaging(villager.State))
        {
            villager.ActionTicksRemaining = 0;
            villager.State = VillagerState.TravelingHome;
            Travel(world, villager, world.Home, VillagerState.Idle);
            return;
        }

        // Otherwise finish whatever is underway before reconsidering. Without this,
        // a villager could re-decide mid-gather every tick and never finish anything.
        if (villager.ActionTicksRemaining > 0)
        {
            villager.ActionTicksRemaining--;
            if (villager.ActionTicksRemaining == 0)
            {
                CompleteAction(world, villager);
            }

            return;
        }

        switch (villager.State)
        {
            case VillagerState.TravelingToFood:
                Travel(world, villager, world.FoodSource.Position, VillagerState.Gathering);
                return;

            case VillagerState.TravelingHome:
                Travel(world, villager, world.Home, VillagerState.Idle);
                return;
        }

        Decide(world, villager);
    }

    private static bool IsForaging(VillagerState state) =>
        state is VillagerState.TravelingToFood or VillagerState.Gathering;

    /// <summary>
    /// Eat if hungry enough and there is food. Returns true if a meal was taken,
    /// which costs the villager this tick.
    /// </summary>
    private static bool TryEat(SimWorld world, Villager villager)
    {
        SimConfig config = world.Config;

        if (villager.Hunger < config.EatThreshold || world.Stockpile.Food < config.FoodPerMeal)
        {
            return false;
        }

        if (!world.Stockpile.TryTake(config.FoodPerMeal))
        {
            // Unreachable given the check above; if it ever fires, something else
            // is mutating the stockpile and we want to know loudly.
            world.Log(LogLevel.Error, "behavior",
                $"{villager.Name} tried to eat {config.FoodPerMeal} food but only " +
                $"{world.Stockpile.Food} was available. This is a bug.");
            return false;
        }

        villager.Hunger -= config.EatReducesHunger;
        if (villager.Hunger < 0)
        {
            villager.Hunger = 0;
        }

        villager.TicksAtMaxHunger = 0;
        villager.JustAte = true;
        return true;
    }

    private static void Decide(SimWorld world, Villager villager)
    {
        SimConfig config = world.Config;

        // Eating was already handled by TryEat before anything got here.
        // Forage — but only if there is anything to forage.
        bool needsFood = world.Stockpile.Food < config.StockpileTarget;
        bool canForage = FoodSource.IsGatherable(world.Clock.Season);

        if (needsFood && canForage)
        {
            if (villager.Position == world.FoodSource.Position)
            {
                BeginGathering(villager, config);
            }
            else
            {
                villager.State = VillagerState.TravelingToFood;
                Travel(world, villager, world.FoodSource.Position, VillagerState.Gathering);
            }

            return;
        }

        // Rest — at home if not already there.
        if (villager.Position != world.Home)
        {
            villager.State = VillagerState.TravelingHome;
            Travel(world, villager, world.Home, VillagerState.Idle);
            return;
        }

        villager.State = VillagerState.Resting;
    }

    /// <summary>Take one step, and switch to <paramref name="onArrival"/> if that
    /// step completes the journey.</summary>
    private static void Travel(SimWorld world, Villager villager, GridPos target, VillagerState onArrival)
    {
        if (villager.Position == target)
        {
            ArriveAt(world, villager, onArrival);
            return;
        }

        villager.Position = villager.Position.StepToward(target);

        // travel_ticks_per_unit > 1 means each step costs extra ticks; the step is
        // already applied, so the remainder is the extra waiting.
        int extraTicks = world.Config.TravelTicksPerUnit - 1;
        if (extraTicks > 0)
        {
            villager.ActionTicksRemaining = extraTicks;
            return;
        }

        if (villager.Position == target)
        {
            ArriveAt(world, villager, onArrival);
        }
    }

    private static void ArriveAt(SimWorld world, Villager villager, VillagerState onArrival)
    {
        if (onArrival == VillagerState.Gathering)
        {
            BeginGathering(villager, world.Config);
            return;
        }

        villager.State = onArrival == VillagerState.Idle ? VillagerState.Resting : onArrival;
    }

    private static void BeginGathering(Villager villager, SimConfig config)
    {
        villager.State = VillagerState.Gathering;
        villager.ActionTicksRemaining = config.GatherTicks;
    }

    /// <summary>Apply the effect of a timed action that just finished.</summary>
    private static void CompleteAction(SimWorld world, Villager villager)
    {
        switch (villager.State)
        {
            case VillagerState.Gathering:
                world.Stockpile.Add(world.FoodSource.YieldPerGather);
                villager.TotalGathers++;
                villager.GathersThisSeason++;

                // Individual gathers are DEBUG, not life log. A fifty-year life is
                // some six hundred foraging trips, and narrating each one buries the
                // handful of lines that actually tell the story — the seasons
                // turning, the winters surviving, the death. ClockSystem sums them
                // up per season instead.
                world.Log(LogLevel.Debug, "behavior",
                    $"Gathered {world.FoodSource.YieldPerGather} food " +
                    $"({world.Stockpile.Food} stored) — {world.Clock}.");

                villager.State = VillagerState.TravelingHome;
                return;

            default:
                // A timed action finished in a state that has no completion effect —
                // that is the travel-delay case, which resolves on the next tick.
                return;
        }
    }
}

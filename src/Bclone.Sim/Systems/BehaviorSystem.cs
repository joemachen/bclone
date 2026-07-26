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
        // Always in id order — see spec §4b.
        for (int i = 0; i < world.Villagers.Count; i++)
        {
            ActOne(world, world.Villagers[i]);
        }
    }

    private static void ActOne(SimWorld world, Villager villager)
    {
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
            GoHome(world, villager);
            return;
        }

        // Abandon an errand for a job you no longer hold.
        //
        // The village shares out its work again once a year (D20), and that can move
        // someone who is already half-way to the old site. Without this they walk on
        // and work it anyway — which is wrong, and, worse, contradicts the sentence
        // the game just told the player about where they work.
        if (IsOnAWorkErrand(villager.State) && !HoldsTheJobFor(world, villager))
        {
            GoHome(world, villager);
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
                // THEIR site, not the one global patch. With several forage sites
                // this is the difference between catchment meaning something and the
                // whole village trooping to the same thicket regardless of where
                // they live.
                Travel(world, villager, WorkplaceOf(world, villager)!.Position, VillagerState.Gathering);
                return;

            case VillagerState.TravelingToTrees:
                Travel(world, villager, WorkplaceOf(world, villager)!.Position, VillagerState.Cutting);
                return;

            case VillagerState.TravelingHome:
                Travel(world, villager, world.HomeOf(villager), VillagerState.Idle);
                return;
        }

        Decide(world, villager);
    }

    private static bool IsForaging(VillagerState state) =>
        state is VillagerState.TravelingToFood or VillagerState.Gathering;

    private static bool IsCutting(VillagerState state) =>
        state is VillagerState.TravelingToTrees or VillagerState.Cutting;

    private static bool IsOnAWorkErrand(VillagerState state) =>
        IsForaging(state) || IsCutting(state);

    /// <summary>The workplace this villager holds a job at, or null.</summary>
    private static Workplace? WorkplaceOf(SimWorld world, Villager villager) =>
        world.FindWorkplace(villager.WorkplaceId);

    /// <summary>Whether the job they hold matches the errand they are on.</summary>
    private static bool HoldsTheJobFor(SimWorld world, Villager villager)
    {
        Workplace? job = WorkplaceOf(world, villager);
        if (job is null)
        {
            return false;
        }

        return IsForaging(villager.State)
            ? job.Kind == JobKind.Forager
            : job.Kind == JobKind.Woodcutter;
    }

    private static void GoHome(SimWorld world, Villager villager)
    {
        villager.ActionTicksRemaining = 0;
        villager.State = VillagerState.TravelingHome;
        Travel(world, villager, world.HomeOf(villager), VillagerState.Idle);
    }

    // Cutting is deliberately NOT included here. Berries stop in winter; trees do
    // not, so a woodcutter keeps working when a forager cannot.

    /// <summary>
    /// Eat if hungry enough and there is food. Returns true if a meal was taken,
    /// which costs the villager this tick.
    /// </summary>
    private static bool TryEat(SimWorld world, Villager villager)
    {
        SimConfig config = world.Config;
        int mealCost = MealCostFor(villager, config);

        if (villager.Hunger < config.EatThreshold || world.HouseholdOf(villager).Stockpile.Food < mealCost)
        {
            return false;
        }

        if (!world.HouseholdOf(villager).Stockpile.TryTake(mealCost))
        {
            // Unreachable given the check above; if it ever fires, something else
            // is mutating the stockpile and we want to know loudly.
            world.Log(LogLevel.Error, "behavior",
                $"{villager.Name} tried to eat {mealCost} food but only " +
                $"{world.HouseholdOf(villager).Stockpile.Food} was available. This is a bug.");
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

    /// <summary>
    /// Food one meal costs. Children eat a smaller portion — see
    /// <see cref="SimConfig.ChildFoodSharePercent"/>.
    /// </summary>
    public static int MealCostFor(Villager villager, SimConfig config)
    {
        if (villager.LifeStage != LifeStage.Child)
        {
            return config.FoodPerMeal;
        }

        int cost = config.FoodPerMeal * config.ChildFoodSharePercent / 100;
        return cost < 1 ? 1 : cost;
    }

    private static void Decide(SimWorld world, Villager villager)
    {
        SimConfig config = world.Config;

        // Eating was already handled by TryEat before anything got here.
        // Forage — but only if there is anything to forage, and only if this
        // villager is old enough to work. A child eats from the household store and
        // gives nothing back; that dependency is the whole reason childhood needed
        // households to exist first (D13).
        Household household = world.HouseholdOf(villager);
        bool needsFood = household.Stockpile.Food < world.TargetFoodFor(household);

        // Foraging is a JOB, not something anyone wanders off and does. Held via
        // LabourSystem, which decides who works where and records why - and, since
        // there are several forage sites now, WHICH ONE.
        Workplace? job = WorkplaceOf(world, villager);
        bool canForage = villager.CanWork
            && job?.Kind == JobKind.Forager
            && FoodSource.IsGatherable(world.Clock.Season);

        if (needsFood && canForage)
        {
            if (villager.Position == job!.Position)
            {
                BeginGathering(villager, config);
            }
            else
            {
                villager.State = VillagerState.TravelingToFood;
                Travel(world, villager, job.Position, VillagerState.Gathering);
            }

            return;
        }

        // Timber. Cuttable year-round, unlike berries, so a woodcutter still has
        // something to do in winter - which is part of why the job is worth holding.
        if (villager.CanWork && job?.Kind == JobKind.Woodcutter)
        {
            if (villager.Position == job.Position)
            {
                villager.State = VillagerState.Cutting;
                villager.ActionTicksRemaining = config.CutTicks;
            }
            else
            {
                villager.State = VillagerState.TravelingToTrees;
                Travel(world, villager, job.Position, VillagerState.Cutting);
            }

            return;
        }

        // Rest — at home if not already there.
        if (villager.Position != world.HomeOf(villager))
        {
            villager.State = VillagerState.TravelingHome;
            Travel(world, villager, world.HomeOf(villager), VillagerState.Idle);
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

        if (onArrival == VillagerState.Cutting)
        {
            villager.State = VillagerState.Cutting;
            villager.ActionTicksRemaining = world.Config.CutTicks;
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
                // Vigour scales what a trip actually brings home. This is where
                // ageing stops being a countdown and starts being something the
                // player can watch: the same year's work yields less, so the
                // seasonal trip count climbs and the winter margin thins.
                int yield = world.FoodSource.YieldPerGather * villager.Vigour / 100;
                if (yield < 1)
                {
                    yield = 1;
                }

                world.HouseholdOf(villager).Stockpile.Add(yield);
                villager.TotalGathers++;
                villager.GathersThisSeason++;

                // Individual gathers are DEBUG, not life log. A fifty-year life is
                // some six hundred foraging trips, and narrating each one buries the
                // handful of lines that actually tell the story — the seasons
                // turning, the winters surviving, the death. ClockSystem sums them
                // up per season instead.
                world.Log(LogLevel.Debug, "behavior",
                    $"Gathered {world.FoodSource.YieldPerGather} food " +
                    $"({world.HouseholdOf(villager).Stockpile.Food} stored) — {world.Clock}.");

                villager.State = VillagerState.TravelingHome;
                return;

            case VillagerState.Cutting:
                // Vigour scales timber the same way it scales berries: an ageing
                // woodcutter brings back less for the same day's walk.
                int wood = world.TreeStand.YieldPerCut * villager.Vigour / 100;
                if (wood < 1)
                {
                    wood = 1;
                }

                world.HouseholdOf(villager).Stockpile.AddWood(wood);
                villager.State = VillagerState.TravelingHome;
                return;

            default:
                // A timed action finished in a state that has no completion effect —
                // that is the travel-delay case, which resolves on the next tick.
                return;
        }
    }
}

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

            case VillagerState.TravelingToHut:
                Travel(world, villager, WorkplaceOf(world, villager)!.Position, VillagerState.MakingFirewood);
                return;

            case VillagerState.HaulingToStore:
                Travel(world, villager, StoreForTheLoad(world, villager).Position, VillagerState.HaulingToStore);
                return;

            case VillagerState.FetchingFromStore:
                Travel(world, villager, PlanFetch(world, villager)?.Position ?? world.HomeOf(villager),
                    VillagerState.FetchingFromStore);
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

    private static bool IsSplitting(VillagerState state) =>
        state is VillagerState.TravelingToHut or VillagerState.MakingFirewood;

    private static bool IsOnAWorkErrand(VillagerState state) =>
        IsForaging(state) || IsCutting(state) || IsSplitting(state);

    /// <summary>The workplace this villager holds a job at, or null.</summary>
    private static Workplace? WorkplaceOf(SimWorld world, Villager villager) =>
        world.FindWorkplace(villager.WorkplaceId);

    /// <summary>The kind of job an errand implies, so state and job can be compared.</summary>
    /// <remarks>
    /// A lookup rather than a two-branch conditional, because there are three kinds
    /// now and there will be more (D19). A conditional that reads "foraging, else
    /// assume timber" is correct only while there are exactly two.
    /// </remarks>
    private static JobKind? ErrandKind(VillagerState state)
    {
        if (IsForaging(state))
        {
            return JobKind.Forager;
        }

        if (IsCutting(state))
        {
            return JobKind.Logger;
        }

        return IsSplitting(state) ? JobKind.Woodcutter : null;
    }

    /// <summary>Whether the job they hold matches the errand they are on.</summary>
    private static bool HoldsTheJobFor(SimWorld world, Villager villager) =>
        WorkplaceOf(world, villager)?.Kind == ErrandKind(villager.State);

    /// <summary>Where a load in someone's arms is going.</summary>
    /// <remarks>
    /// Decided by what they are carrying rather than remembered on the villager: food
    /// goes to the granary, materials to the shed, and there is no third answer to get
    /// out of step (D32).
    /// </remarks>
    private static StoreBuilding StoreForTheLoad(SimWorld world, Villager villager) =>
        villager.CarriedFood > 0 ? world.Granary : world.StorageShed;

    /// <summary>
    /// The store worth walking to, or null if no trip would achieve anything.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>"Short of it" and "a store has it" have to be one decision, not two.</b>
    /// Splitting them killed a founding household in the first winter: they were short
    /// of food <em>and</em> firewood, the granary was picked because food comes first,
    /// the granary was empty — so they walked there and back for ten days and froze
    /// with a hundred and sixteen firewood sitting in the shed.
    /// </para>
    /// <para>
    /// Food before firewood where both are available, because hunger kills in six days
    /// and cold in ten.
    /// </para>
    /// </remarks>
    private static StoreBuilding? PlanFetch(SimWorld world, Villager villager)
    {
        Household household = world.HouseholdOf(villager);
        SimConfig config = world.Config;

        // Topped up when the larder DIPS, not when it is nearly empty.
        //
        // The sharing floor (50%) was the obvious threshold and it strangled the
        // village: food piles up in the granary, households sit at two-thirds of
        // target, and births are gated on the larder — so the settlement ran for a
        // century with seven hundred food in store and almost no children. Keeping
        // homes near their target is what a village with a granary is FOR.
        int foodFloor = world.TargetFoodFor(household) * config.SharingKeepPercent / 100;
        if (household.Stockpile.Food < foodFloor && world.Granary.Store.Food > 0)
        {
            return world.Granary;
        }

        // Firewood only matters where it is burned, so there is no point hauling fuel
        // about in spring instead of building the food store that gets them through
        // the winter they would be hauling it for.
        if (FoodSource.IsGatherable(world.Clock.Season) && world.Clock.Season != Season.Fall)
        {
            return null;
        }

        int firewoodFloor = VillageEconomy.FirewoodStoreWantedPerHousehold(config);
        return household.Stockpile.Firewood < firewoodFloor && world.StorageShed.Store.Firewood > 0
            ? world.StorageShed
            : null;
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

        if (villager.Hunger < config.EatThreshold)
        {
            return false;
        }

        // Eat out of your own arms first.
        //
        // Obvious once seen, and it was a real regression the moment goods started
        // being carried (D30): a villager walked home from the patch with an armful
        // of food and hunger at maximum, and did not eat, because the meal check runs
        // before they get through the door. Nobody starves holding dinner. This is
        // D10's rule — never kill someone for a scheduling artifact — applied to a
        // scheduling artifact that did not exist when D10 was written.
        if (villager.CarriedFood >= mealCost)
        {
            villager.CarriedFood -= mealCost;
            Feed(villager, config);
            return true;
        }

        if (world.HouseholdOf(villager).Stockpile.Food < mealCost)
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

        Feed(villager, config);
        return true;
    }

    /// <summary>Apply the effect of a meal, wherever it came from.</summary>
    private static void Feed(Villager villager, SimConfig config)
    {
        villager.Hunger -= config.EatReducesHunger;
        if (villager.Hunger < 0)
        {
            villager.Hunger = 0;
        }

        villager.TicksAtMaxHunger = 0;
        villager.JustAte = true;
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
        // Two reasons to go out: my family is short, or the village is. Without the
        // second, a forager stops the moment their own larder is full, the granary
        // never fills, and a household with nobody foraging starves beside neighbours
        // who are resting on three hundred food.
        bool needsFood = household.Stockpile.Food < world.TargetFoodFor(household)
            || world.Granary.Store.Food < world.TargetFoodForTheGranary();

        // FETCH — before work, because a household with an empty larder has a more
        // pressing errand than its job.
        //
        // This is what replaced the two sharing policies (D30). Those moved goods
        // between houses by a rule the world enforced from nowhere; this is a person
        // walking to a building and carrying an armful back, which is the whole claim
        // D14 makes about distribution being work rather than a slider.
        //
        // It is also where D32's inequality actually lives: a household far from the
        // granary, or with nobody spare to send, eats worse than one beside it.
        StoreBuilding? errand = villager.CanWork ? PlanFetch(world, villager) : null;
        if (errand is not null)
        {
            villager.State = VillagerState.FetchingFromStore;
            Travel(world, villager, errand.Position, VillagerState.FetchingFromStore);
            return;
        }

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

        // Splitting logs into firewood. The one job that can be blocked by something
        // other than the season or the worker: no logs in the village, no work at the
        // hut (D29). Checked here rather than on arrival so nobody walks over to find
        // the yard empty.
        if (villager.CanWork && job?.Kind == JobKind.Woodcutter)
        {
            if (world.StorageShed.Store.Logs < config.LogsPerSplit)
            {
                villager.WorkNote =
                    $"Nothing to split — {world.StorageShed.Name} has no logs, and {job.Name} needs " +
                    $"{config.LogsPerSplit} for a batch.";
                GoHome(world, villager);
                return;
            }

            villager.WorkNote = string.Empty;

            if (villager.Position == job.Position)
            {
                villager.State = VillagerState.MakingFirewood;
                villager.ActionTicksRemaining = config.SplitTicks;
            }
            else
            {
                villager.State = VillagerState.TravelingToHut;
                Travel(world, villager, job.Position, VillagerState.MakingFirewood);
            }

            return;
        }

        // Timber. Fellable year-round, unlike berries, so a logger still has
        // something to do in winter - which is part of why the job is worth holding.
        if (villager.CanWork && job?.Kind == JobKind.Logger)
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

    /// <summary>
    /// Take what the household is short of, up to what one person can carry.
    /// </summary>
    /// <remarks>
    /// The load limit is what stops a fetch being a teleport with extra steps: one
    /// trip brings back one armful, so a household far from the granary genuinely
    /// eats worse than one beside it. That is the inequality D32 is built on, and it
    /// only exists if a trip has a size.
    /// </remarks>
    private static void CollectFromStore(SimWorld world, Villager villager)
    {
        Household household = world.HouseholdOf(villager);
        SimConfig config = world.Config;
        int load = config.CarryCapacity;

        // Whatever store they are actually standing at, rather than a plan made
        // before they set off — the village may have changed while they walked.
        StoreBuilding? target = null;
        for (int i = 0; i < world.StoreBuildings.Count; i++)
        {
            if (world.StoreBuildings[i].Position == villager.Position)
            {
                target = world.StoreBuildings[i];
            }
        }

        if (target is null)
        {
            return;
        }

        if (target.Kind == StoreKind.Granary)
        {
            int wanted = world.TargetFoodFor(household) - household.Stockpile.Food;
            int take = Smallest(wanted, load, target.Store.Food);
            if (take > 0 && target.Store.TryTake(take))
            {
                villager.CarriedFood += take;
            }

            return;
        }

        int firewoodWanted =
            VillageEconomy.FirewoodStoreWantedPerHousehold(config) - household.Stockpile.Firewood;
        int firewood = Smallest(firewoodWanted, load, target.Store.Firewood);
        if (firewood > 0 && target.Store.TryTakeFirewood(firewood))
        {
            villager.CarriedFirewood += firewood;
        }
    }

    private static int Smallest(int a, int b, int c)
    {
        int smallest = a < b ? a : b;
        return smallest < c ? smallest : c;
    }

    /// <summary>Put down whatever they are carrying, into their own household's store.</summary>
    private static void UnloadAtHome(SimWorld world, Villager villager)
    {
        if (!villager.IsCarrying)
        {
            return;
        }

        Stockpile larder = world.HouseholdOf(villager).Stockpile;

        // Received, not produced — the lifetime counters mean "this household made
        // this much", and a fetch is goods changing hands (see Stockpile.Receive).
        // Food carried straight back from a gather IS production, though, so that
        // one is added rather than received.
        larder.Receive(0, villager.CarriedLogs, villager.CarriedFirewood);
        larder.Add(villager.CarriedFood);

        villager.CarriedFood = 0;
        villager.CarriedLogs = 0;
        villager.CarriedFirewood = 0;
    }

    private static void ArriveAt(SimWorld world, Villager villager, VillagerState onArrival)
    {
        if (onArrival == VillagerState.Gathering)
        {
            BeginGathering(villager, world.Config);
            return;
        }

        if (onArrival == VillagerState.HaulingToStore)
        {
            // The load goes into the building, and only then does it exist anywhere
            // the village can spend it. This is the moment goods stopped teleporting.
            Stockpile store = StoreForTheLoad(world, villager).Store;
            store.Add(villager.CarriedFood);
            store.AddLogs(villager.CarriedLogs);
            store.AddFirewood(villager.CarriedFirewood);
            villager.CarriedFood = 0;
            villager.CarriedLogs = 0;
            villager.CarriedFirewood = 0;

            villager.State = VillagerState.TravelingHome;
            return;
        }

        if (onArrival == VillagerState.FetchingFromStore)
        {
            CollectFromStore(world, villager);
            villager.State = VillagerState.TravelingHome;
            return;
        }

        if (onArrival == VillagerState.MakingFirewood)
        {
            villager.State = VillagerState.MakingFirewood;
            villager.ActionTicksRemaining = world.Config.SplitTicks;
            return;
        }

        if (onArrival == VillagerState.Cutting)
        {
            villager.State = VillagerState.Cutting;
            villager.ActionTicksRemaining = world.Config.CutTicks;
            return;
        }

        // Home. Anything in their arms goes into the larder — a gather brought back
        // directly, or an armful fetched from the granary.
        UnloadAtHome(world, villager);
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

                // Their own larder first, the granary with the rest.
                //
                // A forager brings the day's food home if home needs it, and takes it
                // to the granary if it does not. That keeps the working case working —
                // a household with a forager in it feeds itself directly, no round
                // trip through a building — while surplus ends up somewhere the whole
                // village can draw on. It is also just what a person would do.
                villager.CarriedFood += yield;
                villager.TotalGathers++;
                villager.GathersThisSeason++;

                Household home = world.HouseholdOf(villager);
                bool homeNeedsIt = home.Stockpile.Food < world.TargetFoodFor(home);

                villager.State = homeNeedsIt ? VillagerState.TravelingHome : VillagerState.HaulingToStore;

                // Individual gathers are DEBUG, not life log. A fifty-year life is
                // some six hundred foraging trips, and narrating each one buries the
                // handful of lines that actually tell the story — the seasons
                // turning, the winters surviving, the death. ClockSystem sums them
                // up per season instead.
                world.Log(LogLevel.Debug, "behavior",
                    $"Gathered {yield} food, bound for " +
                    $"{(homeNeedsIt ? "home" : world.Granary.Name)} — {world.Clock}.");
                return;

            case VillagerState.Cutting:
                // Vigour scales timber the same way it scales berries: an ageing
                // woodcutter brings back less for the same day's walk.
                int wood = world.TreeStand.YieldPerCut * villager.Vigour / 100;
                if (wood < 1)
                {
                    wood = 1;
                }

                // Picked up, not banked. The logs go to the shed on the way home,
                // which is what makes them the village's rather than this family's.
                villager.CarriedLogs += wood;
                villager.State = VillagerState.HaulingToStore;
                Travel(world, villager, world.StorageShed.Position, VillagerState.HaulingToStore);
                return;

            case VillagerState.MakingFirewood:
                // Logs come out of the SHED, which stands beside the hut — a woodyard.
                // That adjacency is the whole reason this is not a teleport, and a
                // test asserts the two buildings stay neighbours.
                //
                // It replaces a sweep across every household's private pile, which was
                // a shed in all but name and could not be seen, sited or reasoned about.
                if (!world.StorageShed.Store.TryTakeLogs(world.Config.LogsPerSplit))
                {
                    // The yard emptied while they were working. Not an error: another
                    // woodcutter, or a house being raised, got there first.
                    villager.State = VillagerState.TravelingHome;
                    return;
                }

                int firewood = world.Config.FirewoodPerSplit * villager.Vigour / 100;
                if (firewood < 1)
                {
                    firewood = 1;
                }

                // Straight into the shed beside them, rather than home with the
                // woodcutter. Carrying the village's whole fuel supply back to one
                // house is what froze the household next door (D29), and the daily
                // sharing policy only existed to undo it.
                world.StorageShed.Store.AddFirewood(firewood);
                villager.State = VillagerState.TravelingHome;
                return;

            default:
                // A timed action finished in a state that has no completion effect —
                // that is the travel-delay case, which resolves on the next tick.
                return;
        }
    }
}

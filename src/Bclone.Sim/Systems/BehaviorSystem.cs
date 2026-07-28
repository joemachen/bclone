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

            case VillagerState.FetchingMaterials:
            case VillagerState.Building:
                // To the spot they set off for, not to whatever looks best from this
                // step — same rule as a marketer's leg, and for the same reason.
                Travel(world, villager, new GridPos(villager.ErrandX, villager.ErrandY),
                    villager.State);
                return;

            case VillagerState.CollectingForMarket:
                // To the pickup they chose when they set off, not to whatever is
                // cheapest from this step. Re-deciding mid-walk let a marketer shuttle
                // between two sources forever and complete nothing, which showed up as
                // stranded goods getting WORSE with a market than without one.
                Travel(world, villager, new GridPos(villager.ErrandX, villager.ErrandY),
                    VillagerState.CollectingForMarket);
                return;

            case VillagerState.DeliveringToHome:
                Travel(world, villager, new GridPos(villager.ErrandX, villager.ErrandY),
                    VillagerState.DeliveringToHome);
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

    private static bool IsTrading(VillagerState state) =>
        state is VillagerState.CollectingForMarket or VillagerState.DeliveringToHome;

    private static bool IsBuilding(VillagerState state) =>
        state is VillagerState.FetchingMaterials or VillagerState.Building;

    private static bool IsOnAWorkErrand(VillagerState state) =>
        IsForaging(state) || IsCutting(state) || IsSplitting(state) || IsTrading(state)
        || IsBuilding(state);

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

        if (IsTrading(state))
        {
            return JobKind.Marketer;
        }

        if (IsBuilding(state))
        {
            return JobKind.Builder;
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
    private static StoreBuilding StoreForTheLoad(SimWorld world, Villager villager)
    {
        // A TRADER puts it down at the nearest counter that will take it, which is
        // usually the market — and that is the only way the market ever gets stocked,
        // since nothing else in the village has a reason to walk goods there. Without
        // it the market is a building with an empty shelf: households cannot fetch
        // from it, so it shortens nobody's errand and is decoration.
        //
        // Producers are deliberately NOT given this rule. A forager's harvest goes to
        // the granary by name, because the birth gate reads the granary specifically —
        // let the day's gathering land wherever happens to be closest and the village's
        // population ceiling starts depending on where people were standing.
        if (WorkplaceOf(world, villager)?.Kind == JobKind.Marketer)
        {
            Goods carrying = villager.CarriedFood > 0 ? Goods.Food : Goods.Firewood;
            StoreBuilding? nearest = NearestStoreAccepting(world, villager.Position, carrying);
            if (nearest is not null)
            {
                return nearest;
            }
        }

        // A producer takes their load to the nearest building OF THE RIGHT KIND, which
        // with one of each is exactly what it did before.
        //
        // Kind still matters, and that is the half worth keeping: a forager's harvest
        // goes to a GRANARY rather than to whatever store is closest, because the birth
        // gate reads granaries. Letting the day's gathering land in the market would
        // make the village's population ceiling depend on where somebody was standing.
        StoreKind wanted = villager.CarriedFood > 0 ? StoreKind.Granary : StoreKind.Shed;

        return world.NearestStore(villager.Position, wanted, static store => !store.Store.IsFull)
            ?? world.AnyStoreOf(wanted);
    }

    /// <summary>The nearest shed holding a full batch of logs, or null.</summary>
    /// <remarks>
    /// Asked from the HUT rather than from the woodcutter, because the walk that
    /// matters is the one between the yard and the work — that adjacency is what makes
    /// splitting a job rather than a teleport (D30).
    /// </remarks>
    private static StoreBuilding? NearestShedWithLogs(SimWorld world, GridPos hut, int batch) =>
        world.NearestStore(hut, StoreKind.Shed, store => store.Store.Logs >= batch);

    /// <summary>The nearest store that will take this good and has room, or null.</summary>
    private static StoreBuilding? NearestStoreAccepting(SimWorld world, GridPos from, Goods goods)
    {
        StoreBuilding? best = null;
        int bestCost = int.MaxValue;

        for (int i = 0; i < world.StoreBuildings.Count; i++)
        {
            StoreBuilding store = world.StoreBuildings[i];
            if (!store.Accepts(goods) || store.Store.IsFull)
            {
                continue;
            }

            int cost = world.TravelCost.TicksBetween(from, store.Position);
            if (cost < bestCost)
            {
                bestCost = cost;
                best = store;
            }
        }

        return best;
    }

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
        // Nearest store that actually has it, not the granary by name. That is the
        // whole mechanical value of a market (D14): it is somewhere closer to walk to,
        // so a stocked one shortens the errand rather than merely duplicating it. With
        // no market, or an empty one, this is the granary again and nothing changes —
        // which is the property spec §14.4 turns into a test.
        int foodFloor = world.TargetFoodFor(household) * config.SharingKeepPercent / 100;
        if (household.Stockpile.Food < foodFloor)
        {
            StoreBuilding? source = NearestStoreHolding(world, villager.Position, Goods.Food);
            if (source is not null)
            {
                return source;
            }
        }

        // Firewood only matters where it is burned, so there is no point hauling fuel
        // about in spring instead of building the food store that gets them through
        // the winter they would be hauling it for.
        if (FoodSource.IsGatherable(world.Clock.Season) && world.Clock.Season != Season.Fall)
        {
            return null;
        }

        int firewoodFloor = VillageEconomy.FirewoodStoreWantedPerHousehold(config);
        return household.Stockpile.Firewood < firewoodFloor
            ? NearestStoreHolding(world, villager.Position, Goods.Firewood)
            : null;
    }

    // ---------------------------------------------------------------
    //  Building (D43)
    // ---------------------------------------------------------------

    /// <summary>
    /// A builder's day: carry logs to the site until it has enough, then raise it.
    /// </summary>
    /// <remarks>
    /// The same two-legged shape as the marketer, and for the same reason — a building
    /// that appeared the instant it was paid for would make placement a menu
    /// transaction, and D14's whole argument is that things which happen in this
    /// village are work somebody does.
    /// </remarks>
    private static void WorkTheSite(SimWorld world, Villager villager, Workplace job)
    {
        ConstructionSite site = job.Construction!;

        // Carrying logs, or the site already has what it needs: head for the site.
        if (villager.CarriedLogs > 0 || site.HasMaterials)
        {
            villager.WorkNote = string.Empty;
            HeadFor(world, villager, job.Position, VillagerState.Building);
            return;
        }

        // Otherwise fetch materials from the nearest shed that has any.
        StoreBuilding? shed = world.NearestStore(
            villager.Position, StoreKind.Shed, static store => store.Store.Logs > 0);

        if (shed is null)
        {
            villager.WorkNote =
                $"Nothing to build with — {site.Name} still wants {site.LogsStillNeeded} logs, " +
                "and no shed within reach has any.";
            GoHome(world, villager);
            return;
        }

        villager.WorkNote = string.Empty;
        HeadFor(world, villager, shed.Position, VillagerState.FetchingMaterials);
    }

    /// <summary>Set off for a spot, remembering it so the walk survives re-deciding.</summary>
    private static void HeadFor(
        SimWorld world, Villager villager, GridPos target, VillagerState state)
    {
        villager.ErrandX = target.X;
        villager.ErrandY = target.Y;
        villager.State = state;
        Travel(world, villager, target, state);
    }

    /// <summary>A builder at a shed, picking up as many logs as the site still wants.</summary>
    private static void LoadMaterials(SimWorld world, Villager villager)
    {
        Workplace? job = WorkplaceOf(world, villager);
        ConstructionSite? site = job?.Construction;

        if (site is null)
        {
            villager.State = VillagerState.Idle;
            return;
        }

        int wanted = Math.Min(site.LogsStillNeeded, world.Config.CarryCapacity);

        for (int i = 0; i < world.StoreBuildings.Count && wanted > 0; i++)
        {
            StoreBuilding store = world.StoreBuildings[i];
            if (store.Kind != StoreKind.Shed || store.Position != villager.Position)
            {
                continue;
            }

            int take = Math.Min(wanted, store.Store.Logs);
            if (take > 0 && store.Store.TryTakeLogs(take))
            {
                villager.CarriedLogs += take;
            }

            break;
        }

        villager.State = VillagerState.Idle;
    }

    /// <summary>A builder at the site: put the logs down, then put a tick of work in.</summary>
    private static void RaiseTheBuilding(SimWorld world, Villager villager)
    {
        Workplace? job = WorkplaceOf(world, villager);
        ConstructionSite? site = job?.Construction;

        if (site is null)
        {
            // Finished, or cancelled, while they were walking. Not an error.
            villager.State = VillagerState.Idle;
            return;
        }

        if (villager.CarriedLogs > 0)
        {
            int accepted = site.Deliver(villager.CarriedLogs);
            villager.CarriedLogs -= accepted;

            // Anything the site could not take stays in their arms and goes back to a
            // shed on the next errand — never dropped, per the conservation rule.
            if (villager.CarriedLogs > 0)
            {
                villager.State = VillagerState.HaulingToStore;
                return;
            }
        }

        site.Work();
        villager.State = VillagerState.Building;

        if (site.IsFinished)
        {
            world.Complete(job!);
        }
    }

    // ---------------------------------------------------------------
    //  The market (D14, spec §14)
    // ---------------------------------------------------------------

    /// <summary>One leg of a marketer's round, chosen cost-first from where they stand.</summary>
    /// <param name="Source">Where the goods are picked up.</param>
    /// <param name="HouseholdId">The household this errand concerns.</param>
    /// <param name="Goods">What is being moved.</param>
    /// <param name="Delivering">
    /// True to carry from a store out to that household; false to bring goods the
    /// household does not need back to a store.
    /// </param>
    private readonly record struct MarketErrand(
        GridPos Source, int HouseholdId, Goods Goods, bool Delivering);

    /// <summary>
    /// What a marketer does next.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>A marketer never walks empty-handed</b>, and that single rule is what the
    /// spec's "if the distances make sense" turned into (§14.2). Every leg is picked
    /// cost-first from wherever they are standing <em>right now</em> — the same
    /// principle the labour allocator uses (D15, D23), off the same travel-cost field.
    /// </para>
    /// <para>
    /// So "pick up food from the granary on the way back" is not a special case in
    /// here. After a delivery near the granary, the granary is simply the cheapest next
    /// stop. There is no detour to evaluate and no threshold to tune, because there is
    /// no round trip — only the next-cheapest useful leg.
    /// </para>
    /// </remarks>
    private static void WorkTheMarket(SimWorld world, Villager villager, Workplace job)
    {
        // Mid-errand: finish the leg. Re-deciding every tick would let a marketer
        // change their mind halfway and shuttle between two equally needy homes.
        if (villager.IsCarrying && villager.ErrandHouseholdId != 0)
        {
            Household bound = world.HouseholdOf(villager);
            Household? recipient = world.FindHousehold(villager.ErrandHouseholdId);
            if (recipient is not null)
            {
                villager.State = VillagerState.DeliveringToHome;
                villager.ErrandX = recipient.HomePosition.X;
                villager.ErrandY = recipient.HomePosition.Y;
                Travel(world, villager, recipient.HomePosition, VillagerState.DeliveringToHome);
                return;
            }

            // The household died out while they were walking. Take it to a store
            // rather than dropping it in the road.
            _ = bound;
            villager.ErrandHouseholdId = 0;
            villager.State = VillagerState.HaulingToStore;
            Travel(world, villager, StoreForTheLoad(world, villager).Position,
                VillagerState.HaulingToStore);
            return;
        }

        MarketErrand? next = PlanMarketErrand(world, villager);
        if (next is null)
        {
            villager.WorkNote =
                $"Nothing to move — every household {job.Name} can reach has what it needs.";
            GoHome(world, villager);
            return;
        }

        villager.WorkNote = string.Empty;
        villager.ErrandHouseholdId = next.Value.Delivering ? next.Value.HouseholdId : 0;
        villager.ErrandX = next.Value.Source.X;
        villager.ErrandY = next.Value.Source.Y;
        villager.State = VillagerState.CollectingForMarket;
        Travel(world, villager, next.Value.Source, VillagerState.CollectingForMarket);
    }

    /// <summary>
    /// The cheapest useful leg from where this marketer is standing, or null.
    /// </summary>
    /// <remarks>
    /// Two directions, and the second is what unsticks stranded goods (spec §14.3): out
    /// from a store to a household below its target, and <em>in</em> from a household
    /// holding more than it needs. A house whose family has died is not a special case
    /// — it is simply a household whose need is zero and whose store is not.
    /// </remarks>
    private static MarketErrand? PlanMarketErrand(SimWorld world, Villager villager)
    {
        SimConfig config = world.Config;
        MarketErrand? best = null;
        int bestCost = int.MaxValue;

        // Households in id order, so an exact tie in travel cost always resolves the
        // same way. An unordered tie is a desync waiting to happen (D15).
        for (int i = 0; i < world.Households.Count; i++)
        {
            Household household = world.Households[i];
            bool occupied = world.LivingMembersOf(household) > 0;

            int foodWanted = occupied ? world.TargetFoodFor(household) : 0;
            int fuelWanted = occupied
                ? VillageEconomy.FirewoodStoreWantedPerHousehold(config)
                : 0;

            Consider(household, occupied, Goods.Food, household.Stockpile.Food, foodWanted);
            Consider(household, occupied, Goods.Firewood, household.Stockpile.Firewood, fuelWanted);
        }

        return best;

        void Consider(Household household, bool occupied, Goods goods, int held, int wanted)
        {
            if (held < wanted)
            {
                // OUT: somebody has to bring them some. Pick up wherever it is
                // cheapest to reach from here.
                StoreBuilding? source = NearestStoreHolding(world, villager.Position, goods);
                if (source is null)
                {
                    return;
                }

                Offer(source.Position, household.Id, goods, delivering: true);
                return;
            }

            // IN: goods in a house with nobody left in it — the stranded larder D34
            // left behind, which nothing else in the sim can reach.
            //
            // ONLY empty houses. Collecting "surplus" from a living household was the
            // obvious generalisation and it wrecked the village: a home sits above
            // target every time its forager walks in the door, so marketers stripped
            // families the moment they got ahead, carried it off, and the households
            // fetched it straight back. Pure churn — and worse, the granary stopped
            // filling, so the birth gate never opened and the settlement died out at
            // five people. A trader moves what nobody is using, not what somebody has
            // just earned.
            if (!occupied && held > 0)
            {
                Offer(household.HomePosition, household.Id, goods, delivering: false);
            }
        }

        void Offer(GridPos source, int householdId, Goods goods, bool delivering)
        {
            int cost = world.TravelCost.TicksBetween(villager.Position, source);
            if (cost < bestCost)
            {
                bestCost = cost;
                best = new MarketErrand(source, householdId, goods, delivering);
            }
        }
    }

    /// <summary>The nearest store that has some of this good, or null.</summary>
    private static StoreBuilding? NearestStoreHolding(SimWorld world, GridPos from, Goods goods)
    {
        StoreBuilding? best = null;
        int bestCost = int.MaxValue;

        for (int i = 0; i < world.StoreBuildings.Count; i++)
        {
            StoreBuilding store = world.StoreBuildings[i];
            if (!store.Accepts(goods) || HeldOf(store.Store, goods) <= 0)
            {
                continue;
            }

            int cost = world.TravelCost.TicksBetween(from, store.Position);
            if (cost < bestCost)
            {
                bestCost = cost;
                best = store;
            }
        }

        return best;
    }

    private static int HeldOf(Stockpile store, Goods goods) => goods switch
    {
        Goods.Food => store.Food,
        Goods.Logs => store.Logs,
        _ => store.Firewood,
    };

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
        //
        // The village's half is measured against what the granary has ROOM for, not
        // against what everyone alive would ideally have stored. Past the point where
        // the granary can hold a winter for everybody, the ideal is unreachable — and
        // an unreachable target means "go and forage" is the answer forever, which put
        // every hand on the berry patches and froze the village to death (see
        // SimWorld.TargetFoodForTheGranary).
        bool needsFood = household.Stockpile.Food < world.TargetFoodFor(household)
            || world.FoodInGranaries() < world.FoodTheGranaryHasRoomFor();

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

        // Raising a building the player marked out (D43). Materials first, then work:
        // a builder standing on an empty footprint has nothing to build WITH, and
        // making them fetch it is what stops construction being a purchase.
        if (villager.CanWork && job?.Kind == JobKind.Builder && job.Construction is not null)
        {
            WorkTheSite(world, villager, job);
            return;
        }

        // Working the market — moving what is already made to where it is wanted
        // (D14). The only job that produces nothing, and the only one the village can
        // do entirely without.
        if (villager.CanWork && job?.Kind == JobKind.Marketer)
        {
            WorkTheMarket(world, villager, job);
            return;
        }

        // Splitting logs into firewood. The one job that can be blocked by something
        // other than the season or the worker: no logs in the village, no work at the
        // hut (D29). Checked here rather than on arrival so nobody walks over to find
        // the yard empty.
        if (villager.CanWork && job?.Kind == JobKind.Woodcutter)
        {
            // The nearest shed that actually has a batch in it. Naming THAT shed rather
            // than "the shed" is the point of the refusal: with more than one, "the
            // shed has no logs" would be a sentence the player could not check.
            StoreBuilding? yard = NearestShedWithLogs(world, job.Position, config.LogsPerSplit);
            if (yard is null)
            {
                villager.WorkNote =
                    $"Nothing to split — no shed within reach of {job.Name} has the " +
                    $"{config.LogsPerSplit} logs a batch needs.";
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

        // Through the shared cost field, so the step a villager takes and the distance
        // the economy budgeted for come from the same place. Straight-line stepping
        // would walk them over the river while every other system believed they went
        // round it — the worst of both, and invisible until somebody starved on a
        // journey the sim had priced differently from the one they made.
        GridPos next = world.TravelCost.StepToward(villager.Position, target);
        if (next == villager.Position)
        {
            // Nowhere to go: the target is across water with no way round. Not an
            // error — a real state a village can be in before it can build bridges —
            // so they go home rather than pressing against the bank forever.
            GoHome(world, villager);
            return;
        }

        villager.Position = next;

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

    /// <summary>
    /// A marketer picks up their armful — from a store to deliver, or from a home
    /// holding what it does not need.
    /// </summary>
    private static void LoadForTheRound(SimWorld world, Villager villager)
    {
        int load = world.Config.CarryCapacity;

        // Standing at a store: this is the outward leg, and the household they are
        // serving is already recorded.
        for (int i = 0; i < world.StoreBuildings.Count; i++)
        {
            StoreBuilding store = world.StoreBuildings[i];
            if (store.Position != villager.Position)
            {
                continue;
            }

            Household? recipient = world.FindHousehold(villager.ErrandHouseholdId);
            if (recipient is null)
            {
                break;
            }

            int foodWanted = world.TargetFoodFor(recipient) - recipient.Stockpile.Food;
            int food = Smallest(foodWanted, load, store.Store.Food);
            if (food > 0 && store.Store.TryTake(food))
            {
                villager.CarriedFood += food;
                villager.State = VillagerState.DeliveringToHome;
                return;
            }

            int fuelWanted = VillageEconomy.FirewoodStoreWantedPerHousehold(world.Config)
                - recipient.Stockpile.Firewood;
            int fuel = Smallest(fuelWanted, load, store.Store.Firewood);
            if (fuel > 0 && store.Store.TryTakeFirewood(fuel))
            {
                villager.CarriedFirewood += fuel;
                villager.State = VillagerState.DeliveringToHome;
                return;
            }

            break;
        }

        // Otherwise they are standing at a home, collecting what it does not need —
        // the direction that unsticks a dead family's larder (spec §14.3).
        for (int i = 0; i < world.Households.Count; i++)
        {
            Household household = world.Households[i];
            if (household.HomePosition != villager.Position)
            {
                continue;
            }

            // Only ever from a house with nobody in it — see PlanMarketErrand for the
            // village this killed when it also took "surplus" from living families.
            if (world.LivingMembersOf(household) > 0)
            {
                break;
            }

            const int keepFood = 0;
            const int keepFuel = 0;

            int food = Smallest(household.Stockpile.Food - keepFood, load, household.Stockpile.Food);
            if (food > 0 && household.Stockpile.TryTake(food))
            {
                villager.CarriedFood += food;
            }

            int spareFuel = household.Stockpile.Firewood - keepFuel;
            int fuel = Smallest(spareFuel, load - food, household.Stockpile.Firewood);
            if (fuel > 0 && household.Stockpile.TryTakeFirewood(fuel))
            {
                villager.CarriedFirewood += fuel;
            }

            break;
        }

        // Empty-handed after all — whatever was here has gone. Reconsider next tick
        // rather than standing about; the errand is stale, not the job.
        villager.ErrandHouseholdId = 0;
        villager.State = villager.IsCarrying
            ? VillagerState.HaulingToStore
            : VillagerState.Idle;
    }

    /// <summary>A marketer hands their load to the household they carried it for.</summary>
    private static void HandOverAtHome(SimWorld world, Villager villager)
    {
        Household? recipient = world.FindHousehold(villager.ErrandHouseholdId);
        if (recipient is not null)
        {
            // Received, never Add — a delivery is goods changing hands, and routing it
            // through Add would credit this household with producing what somebody
            // else gathered. That is the bug Stockpile.Receive exists for.
            recipient.Stockpile.Receive(villager.CarriedFood, 0, villager.CarriedFirewood);
            villager.CarriedFood = 0;
            villager.CarriedFirewood = 0;
        }

        villager.ErrandHouseholdId = 0;

        // Straight on to the next leg rather than home — this is the rule that makes
        // "pick up food from the granary on the way back" fall out for free (§14.2).
        villager.State = VillagerState.Idle;
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

        if (onArrival == VillagerState.FetchingMaterials)
        {
            LoadMaterials(world, villager);
            return;
        }

        if (onArrival == VillagerState.Building)
        {
            RaiseTheBuilding(world, villager);
            return;
        }

        if (onArrival == VillagerState.CollectingForMarket)
        {
            LoadForTheRound(world, villager);
            return;
        }

        if (onArrival == VillagerState.DeliveringToHome)
        {
            HandOverAtHome(world, villager);
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
                    $"{(homeNeedsIt ? "home" : StoreForTheLoad(world, villager).Name)} — {world.Clock}.");
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
                Travel(world, villager, StoreForTheLoad(world, villager).Position,
                    VillagerState.HaulingToStore);
                return;

            case VillagerState.MakingFirewood:
                // Logs come out of the SHED, which stands beside the hut — a woodyard.
                // That adjacency is the whole reason this is not a teleport, and a
                // test asserts the two buildings stay neighbours.
                //
                // It replaces a sweep across every household's private pile, which was
                // a shed in all but name and could not be seen, sited or reasoned about.
                StoreBuilding? woodyard = NearestShedWithLogs(
                    world, villager.Position, world.Config.LogsPerSplit);

                if (woodyard is null || !woodyard.Store.TryTakeLogs(world.Config.LogsPerSplit))
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
                //
                // Back into the shed the logs came out of where there is room, so a
                // woodyard stays one place rather than becoming a two-shed shuffle.
                StoreBuilding wall = woodyard.Store.IsFull
                    ? world.NearestStore(
                        villager.Position, StoreKind.Shed, static store => !store.Store.IsFull)
                        ?? woodyard
                    : woodyard;

                wall.Store.AddFirewood(firewood);
                villager.State = VillagerState.TravelingHome;
                return;

            default:
                // A timed action finished in a state that has no completion effect —
                // that is the travel-delay case, which resolves on the next tick.
                return;
        }
    }
}

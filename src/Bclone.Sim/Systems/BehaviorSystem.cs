using Bclone.Sim.Config;
using Bclone.Sim.Core;
using Bclone.Sim.Logging;
using Bclone.Sim.World;

namespace Bclone.Sim.Systems;

/// <summary>
/// Step 7 of the tick order: the villager decides, then acts.
/// </summary>
/// <remarks>
/// <para>
/// Priority order, evaluated top-down every tick. It began as three items — eat, forage,
/// rest — and is now about a dozen: eat, get warm (D45), abandon work the season or a
/// reshuffle has taken away, finish whatever is underway, then the job they hold, then
/// fetch what the household is short of, then rest. <c>ActOne</c> is the list; read it
/// there rather than trusting a copy here.
/// </para>
/// <para>
/// Deliberately a plain top-down if-chain rather than a utility score or behaviour tree,
/// and that constraint has survived every one of those additions. The player has to be
/// able to read <em>why</em> a villager did something (non-negotiable 1); a ranked list
/// of reasons can be explained in one sentence in the UI and a weighted score cannot.
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
            Villager villager = world.Villagers[i];

            // EVERY CHANGE OF STATE IS RECORDED, from one place.
            //
            // The alternative was a log line inside each of the twenty-odd branches
            // below, which would have been wrong within a week: somebody adds a branch
            // and the audit trail quietly has a hole in exactly the case they were
            // debugging. Taking a before-and-after here catches all of them, including
            // the ones nobody has written yet.
            //
            // Guarded, because building these strings for a sink that discards them
            // would cost real time in the 300-year runs.
            if (!world.Logs(LogLevel.Debug))
            {
                ActOne(world, villager);
                continue;
            }

            VillagerState before = villager.State;
            GridPos wasAt = villager.Position;
            int hadFood = villager.CarriedFood;
            int hadLogs = villager.CarriedLogs;
            int hadFirewood = villager.CarriedFirewood;

            ActOne(world, villager);

            if (villager.State != before)
            {
                world.LogVillager(LogLevel.Debug, villager, "behavior",
                    $"{Describe(before)} -> {Describe(villager.State)} at {villager.Position}");
            }
            else if (villager.Position != wasAt)
            {
                world.LogVillager(LogLevel.Trace, villager, "behavior",
                    $"{Describe(villager.State)}, {wasAt} -> {villager.Position}");
            }

            // Goods changing hands is the other half of an audit: "why is the granary
            // empty" is answered by who carried what, and when.
            int food = villager.CarriedFood - hadFood;
            int logs = villager.CarriedLogs - hadLogs;
            int firewood = villager.CarriedFirewood - hadFirewood;

            if (food != 0 || logs != 0 || firewood != 0)
            {
                world.LogVillager(LogLevel.Debug, villager, "goods",
                    $"carrying {Change(food, "food")}{Change(logs, "logs")}" +
                    $"{Change(firewood, "firewood")}" +
                    $"— now {villager.CarriedFood}f/{villager.CarriedLogs}l/{villager.CarriedFirewood}w");
            }
        }
    }

    /// <summary>A state in words rather than an enum name, so the log reads.</summary>
    /// <remarks>
    /// Terse on purpose, and a deliberate second mapping alongside
    /// <see cref="Villager.DescribeState"/>: this writes <em>"idle → gathering"</em> into a
    /// log, that writes <em>"gathering berries"</em> for a person reading a panel. Two
    /// registers for two readers. <b>Internal so a test can walk every value of the enum
    /// through it</b> — the panel's copy of this fell seven states behind before anybody
    /// noticed, and the only reason to keep two is that neither is allowed to rot.
    /// </remarks>
    internal static string Describe(VillagerState state) => state switch
    {
        VillagerState.Idle => "idle",
        VillagerState.TravelingToFood => "walking to forage",
        VillagerState.Gathering => "gathering",
        VillagerState.TravelingHome => "walking home",
        VillagerState.TravelingToTrees => "walking to the stand",
        VillagerState.Cutting => "felling",
        VillagerState.Resting => "resting",
        VillagerState.Dead => "dead",
        VillagerState.TravelingToHut => "walking to the hut",
        VillagerState.MakingFirewood => "splitting logs",
        VillagerState.HaulingToStore => "hauling to a store",
        VillagerState.FetchingFromStore => "fetching from a store",
        VillagerState.SeekingShelter => "going in to get warm",
        VillagerState.CollectingForMarket => "collecting for the market",
        VillagerState.DeliveringToHome => "delivering to a home",
        VillagerState.FetchingMaterials => "fetching building materials",
        VillagerState.Building => "building",
        _ => state.ToString(),
    };

    private static string Change(int delta, string what) =>
        delta == 0 ? string.Empty : $"{(delta > 0 ? "+" : string.Empty)}{delta} {what} ";

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

        // Then get warm, if the cold has got dangerous (D45).
        //
        // BELOW EATING AND ABOVE EVERYTHING ELSE, and the position is the whole of the
        // rule. Above eating and a villager starves to death in a warm house; below the
        // work branches and it never fires for anybody who has a job, which is everyone
        // it exists to protect.
        //
        // It is a WALK to a fire, not a teleport to safety, and the walk is outdoors —
        // so breaking off early is worth more than breaking off late. That is the
        // decision seek_shelter_percent exists to create, and the reason the threshold
        // is stated as a share of the way to dying rather than as a temperature.
        if (TrySeekWarmth(world, villager))
        {
            return;
        }

        // Then refill a household that has nearly nothing left (D77).
        //
        // ABOVE WORK AND BELOW WARMTH, and the position is the rule. Below the work
        // branches — where the ordinary errand still sits — it never fires for anybody who
        // holds a job, which is everyone it exists to protect: Joe watched a house come
        // through a winter with no firewood at all, its residents walking to the
        // neighbour's fire, while the header read "there is no one spare to send" every
        // tick. An errand that loses exactly when it matters is not an errand.
        //
        // Above warmth would be worse than useless — a villager freezing on the way to
        // collect the fuel they are freezing for.
        if (TryEmergencyRestock(world, villager))
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
                Travel(world, villager, PlanFetch(world, villager)?.Position ?? world.RestingPlaceOf(villager),
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
                Travel(world, villager, world.RestingPlaceOf(villager), VillagerState.Idle);
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
        Goods load = villager.CarriedFood > 0 ? Goods.Food
            : villager.CarriedLogs > 0 ? Goods.Logs
            : Goods.Firewood;

        // THE PREFERRED KIND FIRST, THEN ANYWHERE THAT WILL TAKE IT (D76).
        //
        // The preference is the part worth keeping and is not a tidiness rule: a forager's
        // harvest belongs in a GRANARY because the birth gate reads granaries, and letting
        // the day's gathering land in the market would make the village's population ceiling
        // depend on where somebody happened to be standing (D33).
        //
        // But a preference that cannot be satisfied must not become a refusal, which is what
        // it was: with no shed built, a villager holding logs found nothing and this method
        // fell through to the cart by name — one more place that had to be told about each
        // new store. Asking "what will take this?" needs telling about none of them.
        StoreBuilding? proper =
            world.NearestStore(villager.Position, wanted, static store => !store.Store.IsFull)
            ?? FirstOfKind(world, wanted)
            ?? world.NearestStoreAccepting(
                villager.Position, load, static store => !store.Store.IsFull);

        if (proper is not null)
        {
            return proper;
        }

        // FULL IS NOT THE SAME AS ABSENT, and conflating them crashed Joe's village (D80).
        //
        // Every branch above rejects a store with no room, which is right while somewhere
        // else has room and catastrophic when nowhere does: he demolished the cart, the
        // storage pile filled, and the sim threw "the village has no Shed and no cart" —
        // every tick, forever — while a perfectly good pile stood in the square.
        //
        // A village whose stores are full is a village with a PROBLEM, not a village with
        // an invariant violation. Somewhere to walk beats a stack trace: they go to the
        // nearest place that would take this good if it could, and the load stays in their
        // arms until there is room, which is the same thing a person would do.
        StoreBuilding? anywhere =
            world.NearestStoreAccepting(villager.Position, load, static _ => true)
            ?? world.TheCart;

        if (anywhere is not null)
        {
            return anywhere;
        }

        // Genuinely nowhere — no store of any kind takes this. That IS an invariant
        // violation and it is said out loud rather than swallowed (METHODOLOGY §4).
        throw new InvalidOperationException(
            $"{villager.Name} is holding {load} and the village has nowhere at all to put "
            + "it — no store of any kind accepts it. Every village must have somewhere to "
            + "put things.");
    }

    /// <summary>Any store of this kind, reachable or not, or null if there are none.</summary>
    /// <remarks>
    /// <b>Reachable or not is the point, and it is not new behaviour.</b> This is what
    /// <c>SimWorld.AnyStoreOf</c> did before the cold start needed a non-throwing version:
    /// a villager holding an armful must be given somewhere to walk even when the only
    /// granary is across the water, because the alternative is standing still forever with
    /// goods that nothing can spend (D48). The difference is that this returns null instead
    /// of throwing, so the caller can offer the cart before giving up.
    /// </remarks>
    private static StoreBuilding? FirstOfKind(SimWorld world, StoreKind kind)
    {
        for (int i = 0; i < world.StoreBuildings.Count; i++)
        {
            if (world.StoreBuildings[i].Kind == kind)
            {
                return world.StoreBuildings[i];
            }
        }

        return null;
    }

    /// <summary>The nearest store holding a full batch of logs, or null.</summary>
    /// <remarks>
    /// <para>
    /// Asked from the HUT rather than from the woodcutter, because the walk that
    /// matters is the one between the yard and the work — that adjacency is what makes
    /// splitting a job rather than a teleport (D30).
    /// </para>
    /// <para>
    /// <b>Asked by what holds logs, not by which building does (D76).</b> This line named
    /// <see cref="StoreKind.Shed"/> and was the fourth site of one bug: Joe marked a
    /// woodcutter's hut, marked no shed, and the hut reported <em>"no logs here to split"</em>
    /// while four hundred logs sat in the cart. His village froze around a working hut it
    /// could not feed.
    /// </para>
    /// </remarks>
    private static StoreBuilding? NearestStoreWithLogs(SimWorld world, GridPos hut, int batch) =>
        world.NearestStoreAccepting(hut, Goods.Logs, store => store.Store.Logs >= batch);

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
    /// and an unheated house in twenty-five (D45).
    /// </para>
    /// </remarks>
    /// <summary>
    /// Drop everything and refill a household that is nearly out of food or fuel (D77).
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Emergency only</b>, at <see cref="SimConfig.RestockEmergencyPercent"/> of what the
    /// household needs. This errand outranks a job, and an errand that outranks a job has to
    /// be rare or it empties the berry patch (D52).
    /// </para>
    /// <para>
    /// <b>Children may go.</b> Carrying is not work, and a family with both adults at the
    /// tree stand still has legs in it — which is why this is not gated on
    /// <see cref="Villager.CanWork"/> the way the ordinary errand is.
    /// </para>
    /// <para>
    /// <b>One at a time, asked rather than recorded.</b> Without the limit a family of five
    /// all down tools for the same armful. It is a <em>reader</em> over the household's
    /// members rather than a flag, because a flag is one more thing that can be set and not
    /// cleared (D66, D71).
    /// </para>
    /// <para>
    /// <b>Judged on distribution, not distance</b> (D78): this raises how far households
    /// walk and takes the time they spend on an empty larder to zero. The second is what the
    /// errand is for, and what the guard measures.
    /// </para>
    /// </remarks>
    private static bool TryEmergencyRestock(SimWorld world, Villager villager)
    {
        SimConfig config = world.Config;
        int share = config.RestockEmergencyPercent;
        if (share <= 0)
        {
            return false;
        }

        Household household = world.HouseholdOf(villager);

        // Already on the way — keep going rather than reconsidering every tick, or they
        // shuttle between the two nearest stores forever.
        bool alreadyGoing = villager.State == VillagerState.FetchingFromStore;

        Goods? wanted = WhatTheHouseholdIsNearlyOutOf(world, household, share);
        if (wanted is null)
        {
            return false;
        }

        if (!alreadyGoing && SomebodyElseIsFetching(world, household, villager))
        {
            return false;
        }

        StoreBuilding? source = NearestStoreHolding(world, villager.Position, wanted.Value);
        if (source is null)
        {
            return false;
        }

        villager.ActionTicksRemaining = 0;
        villager.State = VillagerState.FetchingFromStore;
        Travel(world, villager, source.Position, VillagerState.FetchingFromStore);
        return true;
    }

    /// <summary>Food first, then fuel — the one that kills soonest (D45's ordering).</summary>
    private static Goods? WhatTheHouseholdIsNearlyOutOf(
        SimWorld world, Household household, int share)
    {
        if (world.LivingMembersOf(household) == 0)
        {
            return null;
        }

        int foodFloor = world.TargetFoodFor(household) * share / 100;
        if (household.Stockpile.Food < foodFloor)
        {
            return Goods.Food;
        }

        // Fuel only matters where it is burned. Hauling it about in spring is time not
        // spent on the food store that gets them to the winter they would burn it in.
        if (FoodSource.IsGatherable(world.Clock.Season) && world.Clock.Season != Season.Fall)
        {
            return null;
        }

        int fuelFloor =
            VillageEconomy.FirewoodStoreWantedPerHousehold(world.Config) * share / 100;

        return household.Stockpile.Firewood < fuelFloor ? Goods.Firewood : null;
    }

    /// <summary>Whether a housemate is already away fetching. Asked, never recorded.</summary>
    private static bool SomebodyElseIsFetching(
        SimWorld world, Household household, Villager villager)
    {
        for (int i = 0; i < household.MemberIds.Count; i++)
        {
            int id = household.MemberIds[i];
            if (id == villager.Id)
            {
                continue;
            }

            Villager? housemate = world.FindVillager(id);
            if (housemate is { Alive: true, State: VillagerState.FetchingFromStore })
            {
                return true;
            }
        }

        return false;
    }

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

        // Otherwise fetch materials from the nearest shed that has any — OR THE CART,
        // while it is the only store the village has (D75).
        //
        // Measured, after two playthroughs where a marked hut sat at "0 of 25 logs" with
        // a builder assigned to it and 426 logs standing in the village: the builder was
        // funded all along and simply could not see the timber. This is the third place
        // that read sheds-only and had to learn about the cart — TryTakeBuildingTimber
        // and StoreForTheLoad were the other two — which says the seam is the KIND check
        // rather than any one call site.
        StoreBuilding? shed = world.NearestStoreAccepting(
            villager.Position, Goods.Logs, static store => store.Store.Logs > 0);

        if (shed is null)
        {
            villager.WorkNote =
                $"Nothing to build with — {site.Name} still wants {site.LogsStillNeeded} logs, " +
                "and nowhere within reach has any.";
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

            // The cart counts here too, and it must: the walk above may have sent them to
            // one, and a builder who arrives at the timber and cannot pick it up is worse
            // than one who never set off (D75).
            if (!store.Accepts(Goods.Logs)
                || store.Position != villager.Position)
            {
                continue;
            }

            int take = Math.Min(wanted, store.Store.Logs);
            if (take > 0 && store.Store.TryTake(Goods.Logs, take))
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
            // A house to deliver to, not merely a family: a marketer cannot leave an armful
            // at a household that has not built anything yet (D70), and the branch below
            // already knows how to put a load back rather than drop it in the road.
            Household? recipient = world.FindHousehold(villager.ErrandHouseholdId);
            if (recipient?.HomePosition is GridPos doorstep)
            {
                villager.State = VillagerState.DeliveringToHome;
                villager.ErrandX = doorstep.X;
                villager.ErrandY = doorstep.Y;
                Travel(world, villager, doorstep, VillagerState.DeliveringToHome);
                return;
            }

            // The household died out while they were walking. Take it to a store
            // rather than dropping it in the road.
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
            // Only a house can strand goods, and only a house can be collected from. A
            // homeless family has no shelf for anything to sit on.
            if (!occupied && held > 0 && household.HomePosition is GridPos larder)
            {
                Offer(larder, household.Id, goods, delivering: false);
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
        Travel(world, villager, world.RestingPlaceOf(villager), VillagerState.Idle);
    }

    // Cutting is deliberately NOT included here. Berries stop in winter; trees do
    // not, so a woodcutter keeps working when a forager cannot.

    /// <summary>
    /// Break off and head for a fire once the cold has got dangerous (D45).
    /// </summary>
    /// <remarks>
    /// <para>
    /// Returns true if this villager spent the tick getting warm, which includes standing
    /// at the fire while they thaw — a villager who turned round the moment they crossed
    /// back under the line would shuttle in and out of the doorway forever.
    /// </para>
    /// <para>
    /// <b>The nearest fire, not their own house.</b> §4.3 of the spec: a neighbour with a
    /// fire lit does not turn a freezing man away, and sending them past a warm door to
    /// die at a cold one of their own would be a cruelty the player cannot act on. If the
    /// village has no fire anywhere then there is nothing to walk to, and they carry on —
    /// standing still in the open would be strictly worse.
    /// </para>
    /// </remarks>
    private static bool TrySeekWarmth(SimWorld world, Villager villager)
    {
        SimConfig config = world.Config;

        int threshold = config.ExposureThreshold;
        if (threshold <= 0 || config.SeekShelterPercent <= 0)
        {
            return false;
        }

        // Once they are here they stay until they are properly warm, not merely until
        // they are back under the line they came in over.
        bool alreadyComingIn = villager.State == VillagerState.SeekingShelter;
        int line = threshold * config.SeekShelterPercent / 100;

        if (villager.Cold < line && !(alreadyComingIn && villager.Cold > 0))
        {
            return false;
        }

        if (villager.Cold == 0)
        {
            return false;
        }

        GridPos? fire = NearestFire(world, villager.Position);
        if (fire is null)
        {
            return false;
        }

        villager.ActionTicksRemaining = 0;
        villager.State = VillagerState.SeekingShelter;
        Travel(world, villager, fire.Value, VillagerState.SeekingShelter);
        return true;
    }

    /// <summary>The closest home with somebody in it and firewood still in the pile.</summary>
    /// <remarks>
    /// Households in id order so an exact tie in travel cost always resolves the same
    /// way — an unordered tie is a desync waiting to happen (D15).
    /// </remarks>
    private static GridPos? NearestFire(SimWorld world, GridPos from)
    {
        GridPos? best = null;
        int bestCost = int.MaxValue;

        for (int i = 0; i < world.Households.Count; i++)
        {
            Household household = world.Households[i];
            if (world.LivingMembersOf(household) == 0 || household.Stockpile.Firewood <= 0)
            {
                continue;
            }

            if (household.HomePosition is not GridPos hearth)
            {
                continue;
            }

            int cost = world.TravelCost.TicksBetween(from, hearth);
            if (cost < bestCost)
            {
                bestCost = cost;
                best = hearth;
            }
        }

        return best;
    }

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
            world.LogVillager(LogLevel.Debug, villager, "needs",
                $"ate {mealCost} from their own arms (hunger was {villager.Hunger})");
            return true;
        }

        if (world.HouseholdOf(villager).Stockpile.Food < mealCost)
        {
            return false;
        }

        if (!world.HouseholdOf(villager).Stockpile.TryTake(Goods.Food, mealCost))
        {
            // Unreachable given the check above; if it ever fires, something else
            // is mutating the stockpile and we want to know loudly.
            world.Log(LogLevel.Error, "behavior",
                $"{villager.Name} tried to eat {mealCost} food but only " +
                $"{world.HouseholdOf(villager).Stockpile.Food} was available. This is a bug.");
            return false;
        }

        // The ordinary meal, and the one the audit log was missing: only eating from
        // one's own arms was recorded, so the trail showed four meals in a lifetime
        // instead of the several thousand a villager actually eats. A log that reports
        // the rare path and not the common one is worse than none — it invites exactly
        // the wrong conclusion about how often people eat.
        world.LogVillager(LogLevel.Debug, villager, "needs",
            $"ate {mealCost} from the {world.HouseholdOf(villager).Name} larder " +
            $"(hunger was {villager.Hunger}, {world.HouseholdOf(villager).Stockpile.Food} left)");

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
        // The village's half is measured against what the village has ROOM for, not
        // against what everyone alive would ideally have stored. Past the point where
        // the stores can hold a winter for everybody, the ideal is unreachable — and
        // an unreachable target means "go and forage" is the answer forever, which put
        // every hand on the berry patches and froze the village to death (see
        // SimWorld.TargetFoodForTheGranary).
        //
        // ROOM IS ASKED OF EVERY STORE THAT TAKES FOOD, NOT OF GRANARIES (D81). This
        // half used to be dead in a cold start — no granary, no room, so the only
        // reason to work was your own larder, and the household that filled theirs
        // first rested while their neighbours did everything.
        bool needsFood = household.Stockpile.Food < world.TargetFoodFor(household)
            || world.FoodInGranaries() < world.FoodTheVillageHasRoomFor();

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
            StoreBuilding? yard = NearestStoreWithLogs(world, job.Position, config.LogsPerSplit);
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
        if (villager.Position != world.RestingPlaceOf(villager))
        {
            villager.State = VillagerState.TravelingHome;
            Travel(world, villager, world.RestingPlaceOf(villager), VillagerState.Idle);
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
    /// <summary>Collecting, exposed so a test can pose the case directly.</summary>
    /// <remarks>
    /// A seam rather than a public API: what a household picks up at a store is worth
    /// asserting on its own, because getting it wrong starves a village quietly and
    /// takes a hundred years of simulation to show up in an acceptance test.
    /// </remarks>
    internal static void CollectForTest(SimWorld world, Villager villager) =>
        CollectFromStore(world, villager);

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

        // WHAT THEY CAME FOR AND WHAT THIS PLACE HAS — not what kind of building it is.
        //
        // This used to read "granary? take food; anything else? take firewood", which
        // was right while only a granary held food. The market holds both (D14), and
        // PlanFetch sends people to the NEAREST store with what they need — so a
        // household nearer the market than the granary walked over for food and came
        // home with firewood, every time, and starved with the granary full. Joe
        // watched it happen: "people seem to not be able to find anything to eat."
        //
        // Food first where both are short, for the same reason PlanFetch prefers it:
        // hunger kills in six days and an unheated house in twenty-five (D45).
        int foodShort = world.TargetFoodFor(household) - household.Stockpile.Food;
        int food = Smallest(foodShort, load, target.Store.Food);
        if (food > 0 && target.Store.TryTake(Goods.Food, food))
        {
            villager.CarriedFood += food;
            return;
        }

        int firewoodShort =
            VillageEconomy.FirewoodStoreWantedPerHousehold(config) - household.Stockpile.Firewood;
        int firewood = Smallest(firewoodShort, load, target.Store.Firewood);
        if (firewood > 0 && target.Store.TryTake(Goods.Firewood, firewood))
        {
            villager.CarriedFirewood += firewood;
        }
    }

    private static int Smallest(int a, int b, int c)
    {
        int smallest = a < b ? a : b;
        return smallest < c ? smallest : c;
    }

    /// <summary>
    /// Put down whatever they are carrying, into their own household's store —
    /// <b>except logs</b>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Logs are never household goods, and letting them become so is a leak that
    /// cannot drain.</b> A house is paid for by the whole village out of a shed (D25),
    /// and goods live in buildings (D30) — so `TryTakeBuildingTimber`, the woodcutter
    /// and every builder all read sheds. Nothing anywhere reads
    /// <c>Household.Stockpile.Logs</c> except the state hash. A log that reaches a
    /// larder is dead for the rest of the run.
    /// </para>
    /// <para>
    /// <b>Measured, because it was killing villages.</b> A villager arrives home still
    /// holding timber whenever a trip is interrupted — the season turns, the yearly
    /// reshuffle takes their job, they stop to eat. Those armfuls accumulated: 240 logs
    /// frozen in two houses for twenty years while a construction site wanted 28 and a
    /// waiting couple wanted 30. Nobody starved and nobody froze; the village simply
    /// could not build, so no new household ever formed and it aged out with a full
    /// granary. This is D25's bug returning by a different road, and D30 was supposed
    /// to have closed it.
    /// </para>
    /// <para>
    /// So the timber stays in their arms and <see cref="ArriveAt"/> turns them round
    /// toward a shed. Keeping it on the villager rather than teleporting it is the
    /// point: goods move only by trips people make.
    /// </para>
    /// </remarks>
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
        larder.Receive(Goods.Firewood, villager.CarriedFirewood);
        larder.Add(Goods.Food, villager.CarriedFood);

        villager.CarriedFood = 0;
        villager.CarriedFirewood = 0;

        // The degenerate case, handled rather than assumed away: a village with no
        // shed at all has nowhere to put timber, and sending them to one that does not
        // exist would throw. Put it down and say so loudly — never silently (METHODOLOGY §4).
        if (villager.CarriedLogs > 0 && !AnyShedStanding(world))
        {
            world.Log(
                LogLevel.Warn,
                "goods",
                $"{villager.Name} came home with {villager.CarriedLogs} logs and the village has " +
                "no shed to put them in, so they are stranded in the larder where nothing can " +
                "spend them. Build a shed.");

            larder.Receive(Goods.Logs, villager.CarriedLogs);
            villager.CarriedLogs = 0;
        }
    }

    private static bool AnyShedStanding(SimWorld world)
    {
        for (int i = 0; i < world.StoreBuildings.Count; i++)
        {
            if (world.StoreBuildings[i].Kind == StoreKind.Shed)
            {
                return true;
            }
        }

        return false;
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
            if (food > 0 && store.Store.TryTake(Goods.Food, food))
            {
                villager.CarriedFood += food;
                villager.State = VillagerState.DeliveringToHome;
                return;
            }

            int fuelWanted = VillageEconomy.FirewoodStoreWantedPerHousehold(world.Config)
                - recipient.Stockpile.Firewood;
            int fuel = Smallest(fuelWanted, load, store.Store.Firewood);
            if (fuel > 0 && store.Store.TryTake(Goods.Firewood, fuel))
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

            // Everything, because there is nobody left to keep any of it for. This once
            // subtracted a `keepFood` and a `keepFuel`, both of which were const zero.
            int food = Smallest(household.Stockpile.Food, load, household.Stockpile.Food);
            if (food > 0 && household.Stockpile.TryTake(Goods.Food, food))
            {
                villager.CarriedFood += food;
            }

            int fuel = Smallest(household.Stockpile.Firewood, load - food, household.Stockpile.Firewood);
            if (fuel > 0 && household.Stockpile.TryTake(Goods.Firewood, fuel))
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
            recipient.Stockpile.Receive(Goods.Food, villager.CarriedFood);
            recipient.Stockpile.Receive(Goods.Firewood, villager.CarriedFirewood);
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
            store.Add(Goods.Food, villager.CarriedFood);
            store.Add(Goods.Logs, villager.CarriedLogs);
            store.Add(Goods.Firewood, villager.CarriedFirewood);
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

        // Except timber. Anyone who reaches their door still carrying logs turns round
        // and walks them to a shed, because a log in a larder is a log nobody can ever
        // spend — see UnloadAtHome for the twenty years this cost.
        if (villager.CarriedLogs > 0)
        {
            villager.State = VillagerState.HaulingToStore;
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
                StoreBuilding? woodyard = NearestStoreWithLogs(
                    world, villager.Position, world.Config.LogsPerSplit);

                if (woodyard is null || !woodyard.Store.TryTake(Goods.Logs, world.Config.LogsPerSplit))
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
                    ? world.NearestStoreAccepting(
                        villager.Position, Goods.Firewood, static store => !store.Store.IsFull)
                        ?? woodyard
                    : woodyard;

                wall.Store.Add(Goods.Firewood, firewood);
                villager.State = VillagerState.TravelingHome;
                return;

            default:
                // A timed action finished in a state that has no completion effect —
                // that is the travel-delay case, which resolves on the next tick.
                return;
        }
    }
}

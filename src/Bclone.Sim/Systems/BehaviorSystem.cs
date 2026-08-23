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
        VillagerState.Clearing => "clearing painted ground",
        VillagerState.TidyingGround => "fetching a load off the ground",
        VillagerState.TravelingToField => "walking out to the field",
        VillagerState.Sowing => "sowing",
        VillagerState.Reaping => "reaping",
        VillagerState.HaulingToFarm => "carrying the harvest to the farm",
        _ => state.ToString(),
    };

    /// <summary>
    /// The one crop that is defined — <b>one crop, in a model shaped for many</b> (Joe, D161).
    /// </summary>
    /// <remarks>
    /// A tile carries a crop id beside its terrain, and the crops themselves belong in data
    /// (CLAUDE.md's rule, and the assumption that a modder will want to touch them). Exactly
    /// one is defined, and the cost of deferring the <em>id</em> is what is being avoided:
    /// retrofitting one onto a shipped terrain triple means touching the hash, both goldens
    /// and every call site at once, where adding a row to a data file later costs nothing.
    /// </remarks>
    private const byte TheOneCrop = 1;

    private static string Change(int delta, string what) =>
        delta == 0 ? string.Empty : $"{(delta > 0 ? "+" : string.Empty)}{delta} {what} ";

    private static void ActOne(SimWorld world, Villager villager)
    {
        villager.JustAte = false;

        if (!villager.Alive)
        {
            return;
        }

        // ⭐⭐ THEIR OWN RHYTHM, SPENT ONCE AT THE START OF A WORKING LIFE (§3.5, D28, D190).
        //
        // **This is the oldest open observation in the project.** Joe watched the village at 4×
        // in Phase 1 and saw people travelling as duos rather than individuals; measured, two
        // adults of one household holding one job are on the same tile 99.9% of ticks with
        // identical hunger 100% of the time. They are not short of variability, they are
        // SYMMETRIC — same home tile, same stepping rule, same constant durations — so they even
        // stop to eat on the same tick.
        //
        // ⭐ A FEW TICKS, ONCE, IS ENOUGH, because nothing ever puts them back in step: two
        // villagers who set off a tick apart arrive, work and eat a tick apart for the rest of
        // their lives. That is §3.3's argument about differing durations, arriving on day one
        // instead of after twenty years.
        //
        // ⚠️ ABOVE EATING ON PURPOSE. Below it, a villager whose rhythm is unspent would take
        // their first meal in lockstep with their sibling and the stagger would be spent against
        // an already-synchronised clock. Nobody starves for it: the draw is under one day and
        // `TicksAtMaxHunger` needs six.
        if (villager.Rhythm > 0 && villager.CanWork)
        {
            villager.Rhythm--;
            villager.State = VillagerState.Resting;
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
        if (!SeasonRules.IsGatherable(world.Clock.Season) && IsForaging(villager.State))
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
                // To the tile they set off for when the hut owns ground, otherwise to the
                // stand itself — the same rule as a marketer's leg (D86,
                // `forests-and-gathering.md`). Re-deciding mid-walk would have a forester
                // shuttle between two trees and fell neither.
                Travel(
                    world,
                    villager,
                    WorkplaceOf(world, villager) is { } trees
                        && world.Zones.WorkGroundTiles(trees.Id) > 0
                            ? new GridPos(villager.ErrandX, villager.ErrandY)
                            : WorkplaceOf(world, villager)!.Position,
                    VillagerState.Cutting);
                return;

            case VillagerState.TravelingToHut:
                Travel(world, villager, WorkplaceOf(world, villager)!.Position, VillagerState.MakingFirewood);
                return;

            case VillagerState.HaulingToStore:
                if (StoreForTheLoad(world, villager) is not StoreBuilding bound)
                {
                    // Nowhere left to carry it — it goes down here, and somebody comes
                    // back for it when there is room (D96).
                    SetDownWhereTheyStand(world, villager);
                    GoHome(world, villager);
                    return;
                }

                Travel(world, villager, bound.Position, VillagerState.HaulingToStore);
                return;

            case VillagerState.FetchingFromStore:
                Travel(world, villager, PlanFetch(world, villager)?.Position ?? world.RestingPlaceOf(villager),
                    VillagerState.FetchingFromStore);
                return;

            case VillagerState.Clearing:
                // To the tree they set off for, not to whatever is nearest from this
                // step — the same rule as a marketer's leg, and for the same reason.
                Travel(world, villager, new GridPos(villager.ErrandX, villager.ErrandY),
                    VillagerState.Clearing);
                return;

            case VillagerState.TidyingGround:
                // To the heap they set off for, same rule again (D96).
                Travel(world, villager, new GridPos(villager.ErrandX, villager.ErrandY),
                    VillagerState.TidyingGround);
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

            case VillagerState.TravelingToField:
            case VillagerState.Sowing:
            case VillagerState.Reaping:
                // To the tile they set off for, same rule as clearing and the market's leg:
                // the nearest tile is judged from where somebody is standing, so re-deciding
                // mid-walk lets them shuttle between two rows for ever and reach neither.
                Travel(world, villager, new GridPos(villager.ErrandX, villager.ErrandY),
                    villager.State == VillagerState.TravelingToField
                        ? (SeasonRules.IsReaping(world.Clock.Season)
                            ? VillagerState.Reaping
                            : VillagerState.Sowing)
                        : villager.State);
                return;

            case VillagerState.HaulingToFarm:
                if (WorkplaceOf(world, villager) is not Workplace barn)
                {
                    // Their farm was demolished while their arms were full. Not an error —
                    // the load is still real, so it goes to a store or on to the ground.
                    HaulOrSetDown(world, villager);
                    return;
                }

                Travel(world, villager, barn.Position, VillagerState.HaulingToFarm);
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

    private static bool IsFarming(VillagerState state) =>
        state is VillagerState.TravelingToField or VillagerState.Sowing or VillagerState.Reaping
            or VillagerState.HaulingToFarm;

    private static bool IsOnAWorkErrand(VillagerState state) =>
        IsForaging(state) || IsCutting(state) || IsSplitting(state) || IsTrading(state)
        || IsBuilding(state) || IsFarming(state);

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
            return JobKind.Forester;
        }

        if (IsTrading(state))
        {
            return JobKind.Marketer;
        }

        if (IsBuilding(state))
        {
            return JobKind.Builder;
        }

        if (IsFarming(state))
        {
            return JobKind.Farmer;
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
    private static StoreBuilding? StoreForTheLoad(SimWorld world, Villager villager)
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
        //
        // ⭐ AND ROOM OUTRANKS KIND ONCE THE PREFERRED KIND IS FULL (D96). The middle arm
        // used to be `FirstOfKind(wanted)`, which returns a store of the right kind
        // WHATEVER ITS STATE — so a forager holding berries with every granary full was
        // sent to a full granary while the market stood half empty. That was merely wasteful
        // while the load then evaporated; with goods able to be set down it is a loop: put
        // the food down at the granary door, pick it up again as a spare hand, walk nowhere,
        // put it down again, forever.
        //
        // FirstOfKind survives as the arm BELOW, which is the case D48 wrote it for — the
        // only granary is across the water, and somebody holding an armful must still be
        // given somewhere to walk rather than standing still with goods nothing can spend.
        StoreBuilding? proper =
            world.NearestStore(villager.Position, wanted, static store => !store.Store.IsFull)
            ?? world.NearestStoreAccepting(
                villager.Position, load, static store => !store.Store.IsFull)
            ?? FirstOfKind(world, wanted);

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
            world.NearestStoreAccepting(villager.Position, load, static _ => true);

        // The cart even where no route reaches it, as before — but only if it would take
        // this good. It stopped taking logs (D90 step 4), and a fallback that ignores
        // Accepts would walk a forester to a wagon that refuses the load.
        if (anywhere is null && world.TheCart is StoreBuilding cart && cart.Accepts(load))
        {
            anywhere = cart;
        }

        if (anywhere is not null)
        {
            return anywhere;
        }

        // GENUINELY NOWHERE, AND THAT IS NO LONGER AN INVARIANT VIOLATION (D96).
        //
        // This threw — "every village must have somewhere to put things" — and that was
        // true only while every village did. A founding whose cart will not take timber is
        // a village with nowhere to put logs until it clears ground for a pile, which is
        // the whole shape of Joe's opening, so the throw would fire on the intended path.
        //
        // Null instead, and the caller sets the load down where they are standing. A
        // village whose stores are all full is a village with a PROBLEM, not a broken
        // world — D80's own words, one level further down than it applied them. Said out
        // loud rather than swallowed (METHODOLOGY §4); the heap is on the map either way.
        world.Log(LogLevel.Debug, "goods",
            $"{villager.Name} has {load} and nowhere in the village will take it — "
            + $"setting it down at {villager.Position}. {world.Clock}.");

        return null;
    }

    /// <summary>
    /// Put everything in somebody's arms down where they are standing (D96).
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Last resort, and the only three callers are the three that have just been told
    /// there is nowhere.</b> Setting down is never a choice — that is the second of D96's
    /// two restraints, and together with supply-invisibility it says one thing: the ground
    /// is where goods <em>end up</em>, never where they are <em>kept</em>.
    /// </para>
    /// <para>
    /// <b>It is also the conservation rule finally being kept.</b> <c>Stockpile.Add</c>
    /// returns what actually fitted and its own remarks say the return value must not be
    /// ignored; <c>ArriveAt</c> ignored it and zeroed the arms, so a load that arrived at a
    /// full store was silently destroyed. Measured over fifty years, both configs spend
    /// roughly a quarter of their ticks with some store full. <b>A heap you can see and
    /// send somebody to fetch is the honest end of that walk.</b>
    /// </para>
    /// </remarks>
    /// <summary>Set off for wherever this load can go — or set it down, if nowhere can.</summary>
    /// <remarks>
    /// The felling and clearing branches both end here. A forester who has just cut timber
    /// the village has nowhere to put leaves it <b>at the stump</b>, which is what a person
    /// does and is the bootstrap D95 needed: the founding can always start moving material,
    /// whether or not a store exists yet.
    /// </remarks>
    private static void HaulOrSetDown(SimWorld world, Villager villager)
    {
        if (StoreForTheLoad(world, villager) is StoreBuilding destination)
        {
            Travel(world, villager, destination.Position, VillagerState.HaulingToStore);
            return;
        }

        SetDownWhereTheyStand(world, villager);
        GoHome(world, villager);
    }

    private static void SetDownWhereTheyStand(SimWorld world, Villager villager)
    {
        world.SetDown(villager.Position, Goods.Food, villager.CarriedFood);
        world.SetDown(villager.Position, Goods.Logs, villager.CarriedLogs);
        world.SetDown(villager.Position, Goods.Firewood, villager.CarriedFirewood);
        villager.CarriedFood = 0;
        villager.CarriedLogs = 0;
        villager.CarriedFirewood = 0;
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
        if (SeasonRules.IsGatherable(world.Clock.Season) && world.Clock.Season != Season.Fall)
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

    /// <summary>
    /// Whether a shortfall is big enough to be worth walking for (Joe, 2026-08-16).
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>⭐ THIS IS THE JITTER JOE WATCHED FOR MONTHS, ANSWERED AT ITS CAUSE.</b> A fetch used
    /// to fire the instant a larder dipped below its floor, so a household two firewood short
    /// sent somebody out for two firewood — and with a store one tile from the door that is a
    /// villager bouncing between two squares. D166 measured it in his own audit trail: runs of
    /// four to six flips every thirty-odd ticks, for ever.
    /// </para>
    /// <para>
    /// <b>The smaller of two bars, and both are needed.</b> An armful says what a trip is worth
    /// carrying; the good's own target says what the household is trying to keep. The armful
    /// alone would set a bar of ten on firewood a household only wants eleven of — nobody would
    /// fetch fuel until they were nearly out, in winter, which is how people freeze.
    /// </para>
    /// <para>
    /// <b>⚠️ Never below one</b>, or a village with a tiny target would stop fetching entirely
    /// — the degenerate case D98 keeps warning about, where a derived number reaches zero and
    /// silently switches a system off.
    /// </para>
    /// </remarks>
    private static bool WorthTheTrip(SimConfig config, int shortfall, int wanted)
    {
        int byArmful = config.CarryCapacity * config.FetchWorthThisSharePercent / 100;
        int byTarget = wanted * config.FetchWorthThisSharePercent / 100;

        int bar = byArmful < byTarget ? byArmful : byTarget;
        return shortfall >= (bar < 1 ? 1 : bar);
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
        int foodWanted = world.TargetFoodFor(household);
        int foodFloor = foodWanted * config.SharingKeepPercent / 100;
        if (household.Stockpile.Food < foodFloor
            && WorthTheTrip(config, foodWanted - household.Stockpile.Food, foodWanted))
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
        if (SeasonRules.IsGatherable(world.Clock.Season) && world.Clock.Season != Season.Fall)
        {
            return null;
        }

        int firewoodFloor = VillageEconomy.FirewoodStoreWantedPerHousehold(config);
        int firewoodShort = firewoodFloor - household.Stockpile.Firewood;

        return firewoodShort > 0 && WorthTheTrip(config, firewoodShort, firewoodFloor)
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
    /// <para>
    /// The same two-legged shape as the marketer, and for the same reason — a building
    /// that appeared the instant it was paid for would make placement a menu
    /// transaction, and D14's whole argument is that things which happen in this
    /// village are work somebody does.
    /// </para>
    /// <para>
    /// <b>⭐ The site is an errand, not a workplace (Joe, D108).</b> <em>"A construction site
    /// is a place that builders should treat as errands. If there is an incomplete
    /// construction site on the map and an active/staffed builder's hut, then the builders'
    /// priority should be completing the construction site."</em> So the job passed in is the
    /// <b>hut</b>, and which site they walk to is the head of the build queue — which keeps
    /// the player's ▲ Sooner / ▼ Later (D105) meaning real hands, and keeps D102's
    /// player-before-village guarantee as the marking order it became in D104.
    /// </para>
    /// </remarks>
    /// <returns>
    /// False when there is nothing a builder can do here this tick, so the caller lets them
    /// fall through to the errands anybody spare may take (D87, D96).
    /// </returns>
    private static bool WorkTheSite(SimWorld world, Villager villager)
    {
        Workplace? standing = world.NextToBuild();
        if (standing is null)
        {
            // A builder with nothing marked out. Not idle — they fall through to the spare
            // errands at the bottom of Decide, like the woodcutter with an empty yard.
            villager.WorkNote = "Nothing is marked out to build.";
            return false;
        }

        ConstructionSite site = standing.Construction!;

        // ⭐ NOTHING IS BUILT ON GROUND THAT IS STILL STANDING (Joe, D101). The trees under a
        // marked site come down first, and the mark is what asked for them (D100).
        //
        // The builder is not merely sent home: they fall through to the bottom of Decide,
        // where clearing painted ground is exactly the work that unblocks them. That is D87's
        // position rule paying for itself — the person waiting on a clearing is allowed to go
        // and do the clearing, and nobody had to write a rule saying so.
        // ⭐ THE BUILDER CLEARS THEIR OWN SITE (D138), and it fixes a permanent stall.
        //
        // Joe, on a fresh build: *"the builder doesn't seem to fetch — he's just sitting there
        // while there is construction material to move."* His screenshot ruled out D135 in one
        // line — *"Queue: 1st of 1 — nothing is ahead of it"* — with 169 logs in store, one
        // site, one builder, and **"Work: 0 of 45 ticks done"**.
        //
        // This branch is why, and it sits *above* the fetch, so a site whose ground is not
        // clear never even looks for a store. The note said the ground *"is still being
        // cleared"*, which was a comfortable lie: **nobody was clearing it and nobody ever
        // would.** A site can be marked on a tile that still has a tree on it — measured, a
        // shed marked near the founding site reported `clear ground False` the moment it was
        // laid out — and laborers only fell PAINTED ground (D87). So unless the player happened
        // to paint that exact tile, the site was stuck for the rest of the run.
        //
        // The comment this replaces claimed the position rule paid for itself, *"the person
        // waiting on a clearing is allowed to go and do the clearing, and nobody had to write a
        // rule saying so."* That only worked while the tile was painted. It is written down now.
        //
        // ⚠️ AND IT IS NOT A HOLE IN D87. The brush is the only way to fell woodland the player
        // has not spoken about — but marking a building on a tile IS speaking about that tile,
        // and it is the whole of what the mark means. The builder clears the ground they were
        // sent to build on and nothing else, which is Joe's own division: builders own the site.
        // ⚠️ ONLY WHEN NOBODY ELSE WILL, and the first version of this cost the opening.
        // Clearing the site unconditionally made the builder duplicate work the laborers were
        // already doing on painted ground and spend their fetching time on it: the woodcutter's
        // hut in `TheHutThePlayerMarkedIsBuiltBeforeTheHousesTheVillageWants` went from
        // standing at **t150 to t394**, past a winter that starts at t360. Painted ground has
        // somebody coming for it; unpainted ground has nobody, for ever.
        if (!world.GroundIsClearAt(standing.Position))
        {
            if (world.Zones.IsHarvest(standing.Position))
            {
                villager.WorkNote =
                    $"{site.Name} cannot be started yet — the ground it stands on is still being "
                    + "cleared.";
                return false;
            }

            villager.WorkNote = $"Clearing the ground {site.Name} is to stand on.";
            villager.ErrandX = standing.Position.X;
            villager.ErrandY = standing.Position.Y;
            HeadFor(world, villager, standing.Position, VillagerState.Clearing);
            return true;
        }

        // Carrying logs, or the site already has what it needs: head for the site.
        if (villager.CarriedLogs > 0 || site.HasMaterials)
        {
            villager.WorkNote = string.Empty;
            HeadFor(world, villager, standing.Position, VillagerState.Building);
            return true;
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
            // ⭐ THE HEAD OF THE QUEUE IS STARVED — SO WORK A SITE THAT IS NOT (D135).
            //
            // Joe: *"the builder shouldn't just sit at the building waiting."* Until now every
            // builder asked only `NextToBuild()`, which answers *what is first*, so a head with
            // no timber and no timber anywhere to fetch stopped the whole trade — measured, a
            // house standing at "30 logs delivered, 0 still wanted" went untouched for three
            // years while the builders waited on the site in front of it.
            //
            // ⚠️ THE QUEUE IS NOT REORDERED, which is D102's line and it holds: fetching above
            // still serves the head, so scarce timber goes where the player pointed. Only the
            // builder's otherwise-idle time moves. Priority over materials, not over labour.
            Workplace? ready = world.NextBuildableSite();
            if (ready is not null && ready.Id != standing.Id)
            {
                villager.WorkNote =
                    $"{site.Name} is waiting on {site.LogsStillNeeded} logs nobody has, so "
                    + $"{ready.Construction!.Name} is getting the day instead.";
                HeadFor(world, villager, ready.Position, VillagerState.Building);
                return true;
            }

            villager.WorkNote =
                $"Nothing to build with — {site.Name} still wants {site.LogsStillNeeded} logs, " +
                "and nowhere within reach has any.";

            // ⭐ AND NOTHING TO BUILD EITHER, so they go and MAKE materials rather than stand
            // there — Joe's rule: *"if there are no materials available, the builder should
            // harvest materials and take them to storage."* Returning false drops them through
            // to `TryTidyGround` and `TryHelpWithHarvest` below, which is felling painted ground
            // and carrying heaps to a store. Measured over three years: a builder blocked this
            // way spends more ticks clearing than building.
            //
            // ⚠️ PAINTED ground only, and that is deliberate rather than a shortfall. D87 is
            // Joe's own rule that the brush is the only way a tree comes down — a builder who
            // felled unpainted woodland to unblock themselves would be the one actor in the
            // game allowed to reshape the valley without being told to.
            return false;
        }

        villager.WorkNote = string.Empty;
        HeadFor(world, villager, shed.Position, VillagerState.FetchingMaterials);
        return true;
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
    /// <remarks>
    /// <b>The site is asked for again rather than remembered</b> (D108). This used to read
    /// <c>WorkplaceOf(villager).Construction</c>, which is the hut now and has none — a
    /// builder would have walked to the shed, picked nothing up, walked to the site,
    /// delivered nothing, and repeated it forever. Re-asking the queue is the same question
    /// <see cref="WorkTheSite"/> asked when it sent them, so the answer is the same one
    /// unless the player has reordered the queue mid-errand — in which case carrying for the
    /// site they now want is the right outcome anyway.
    /// </remarks>
    private static void LoadMaterials(SimWorld world, Villager villager)
    {
        ConstructionSite? site = world.NextToBuild()?.Construction;

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
    /// <remarks>
    /// <b>⭐ The site is read from where they are standing</b> (D108). They walked here
    /// deliberately — <c>ErrandX/ErrandY</c> is the footprint <see cref="WorkTheSite"/> sent
    /// them to — so the position is already the answer, and a <c>Villager.SiteId</c> would be
    /// the set-and-not-cleared flag D66 and D71 argue against, and one more thing to hash.
    /// That is D87's position rule again.
    /// </remarks>
    private static void RaiseTheBuilding(SimWorld world, Villager villager)
    {
        Workplace? job = world.SiteAt(villager.Position);
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

        // Materials may be stacked on a site whose ground is still standing — that is a
        // delivery, not building — but NO WORK GOES IN until the ground is clear (D101).
        // Checked here as well as in WorkTheSite because a builder can already be standing
        // here when the site is marked, and a rule enforced in one of two places is a rule
        // that gets around.
        if (!world.GroundIsClearAt(job!.Position))
        {
            villager.WorkNote =
                $"{site.Name} cannot be started yet — the ground it stands on is still being "
                + "cleared.";
            villager.State = VillagerState.Idle;
            return;
        }

        // ⛔ AND IF THE SITE IS STILL SHORT OF TIMBER, GO AND GET IT (Joe, D154).
        //
        // *"The builder waits at the construction site for 13 more logs. Just idling there for
        // hundreds of ticks. It says they are 'raising a building' but they are waiting for
        // materials instead of picking them up."* His screenshot: **27 of 40 logs, 0 of 60 ticks
        // done, 240 logs in store.**
        //
        // **`Construction.Work()` is a no-op while materials are short** — by design, it will
        // not start a building that has not been paid for — and this fell straight through it
        // into `State = Building`. A villager in `Building` travels to their errand tile each
        // tick, is already standing on it, and arrives again: deliver nothing, work nothing,
        // stay `Building`. **`Decide` is never reached, so they never look for a store.**
        //
        // ⭐ CONFIRMED IN JOE'S OWN AUDIT TRAIL rather than argued from the code (METHODOLOGY
        // §4, which is what that file is for). Hattie #3 sat in `building` from t2057 to t2309
        // — **250 unbroken ticks, eating every eleventh one and doing nothing else** — and what
        // finally moved her was the cold, not the work:
        //   [t 2309] Hattie is dangerously cold and is going in to get warm
        //   [t 2309] Hattie #3: building -> going in to get warm at (1, -3)
        //
        // So it is not a deadlock — hunger, cold or the three-yearly reshuffle eventually
        // knocks them out and the site does finish (Joe's granary: marked t1985, finished
        // t2489). It is hundreds of ticks of a villager standing on a footprint under a label
        // that says they are working, which is §1.1 failing as squarely as any wrong number.
        //
        // Idle rather than a fetch of its own, so `WorkTheSite` makes the choice next tick with
        // everything it knows — including D135's *"work a site that is not starved"* arm.
        if (!site.HasMaterials)
        {
            villager.WorkNote =
                $"{site.Name} still wants {site.LogsStillNeeded} logs — going for them.";
            villager.State = VillagerState.Idle;
            return;
        }

        villager.WorkNote = string.Empty;
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
            // rather than dropping it in the road — and if no store will take it, set it
            // down rather than carrying it about forever (D96).
            villager.ErrandHouseholdId = 0;
            villager.State = VillagerState.HaulingToStore;
            HaulOrSetDown(world, villager);
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

        // ⭐⭐ AND THE THIRD LEG: A WORKPLACE BUFFER THAT CAN NO LONGER TAKE A WHOLE LOAD
        // (§3.2a, D171). `crops-and-orchards.md §3.2` ruling 1 has said since the farm shipped
        // that the buffer is free and *"running it dry is the market's job"* — and nothing ever
        // ran it dry. Ruling 2 built the market's *sourcing* half only, so a trader would take
        // from a farm to fill a hungry larder and never to empty one.
        //
        // ⛔ WHY THAT COSTS THE HARVEST RATHER THAN BEING UNTIDY. `VillageEconomy.FieldTileTicks`
        // budgets a reaped tile at the work plus a round trip *to the steading*, in its own
        // words — so `FieldTilesOneFarmerKeeps` is derived on the assumption that the buffer
        // takes every load. A buffer that takes one load a year makes the derivation describe a
        // farm nobody is running, which is D165's finding in the same method. Measured in Joe's
        // village: 27 hauls, none of them to the farm, eight of thirteen tiles brought in.
        //
        // ⭐ THE CONDITION IS DERIVED, NOT TUNED (D16, and ruling 2's own standard). A buffer is
        // worth clearing exactly when it can no longer take a whole armful — which is precisely
        // when `HaulTheHarvest` stops choosing it and starts sending the farmer to the granary.
        // No threshold, no new number, one comparison.
        //
        // ⚠️ AND IT IS OFFERED, NOT PRIORITISED. It competes with every other leg on travel cost
        // through the same `Offer`, so a trader passing the farm clears it and a trader across
        // the village does not detour — the same shape ruling 2 chose, for the same reason.
        for (int i = 0; i < world.Workplaces.Count; i++)
        {
            Workplace workplace = world.Workplaces[i];

            // ⭐ ASKED OF THE WORLD RATHER THAN SPELLED OUT HERE (D185). It used to be two
            // lines of comparison in this loop and nowhere else — and `MarketersWanted` did not
            // have them, so **the village never staffed anybody to run this leg.** One
            // condition, both callers; see `SimWorld.BufferWorthClearing`.
            if (!world.BufferWorthClearing(workplace))
            {
                continue;
            }

            // Household 0 is the errand saying *nobody is waiting for this* — the same
            // sentinel a stranded-larder collection already uses.
            Offer(workplace.Position, 0, Goods.Food, delivering: false);
        }

        return best;

        void Consider(Household household, bool occupied, Goods goods, int held, int wanted)
        {
            if (held < wanted)
            {
                // OUT: somebody has to bring them some. Pick up wherever it is
                // cheapest to reach from here.
                StoreBuilding? source = NearestStoreHolding(world, villager.Position, goods);

                // ⭐ AND A FARM COUNTS, BUT ONLY IF IT HAPPENS TO BE NEARER (Joe, D161):
                // *"focus on granary first and only grab from a farm if it happens to be near
                // by — filling up residential larders for example."*
                //
                // Expressed without a magic number, which is the whole of the design: a
                // workplace store is a candidate **only when it is strictly nearer than the
                // nearest store building holding that good**. No threshold, no new tunable, one
                // comparison, deterministic — and it produces exactly the behaviour asked for,
                // a trader passing the farm using it and a trader across the village not
                // detouring. A tuned radius here would be a number nobody could derive (D16),
                // and D112 has already traded a fence for a consequence once.
                Workplace? farm = NearerWorkplaceStore(world, villager.Position, goods, source);
                if (farm is not null)
                {
                    Offer(farm.Position, household.Id, goods, delivering: true);
                    return;
                }

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

    /// <summary>
    /// A workplace store holding this good that is <b>strictly nearer</b> than the nearest
    /// store building holding it, or null.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>⭐ The whole of §3.2's ruling 2, in one comparison.</b> The granary comes first and a
    /// farm is used only when it happens to be on the way — stated as a comparison rather than
    /// as a radius, so there is no number to tune and no number anybody would have to derive.
    /// </para>
    /// <para>
    /// <b>⚠️ AND IT IS DELIBERATELY NOT A SECOND WAY TO FIND A STORE</b> (D145: *a control is
    /// safe when its state is read at a chokepoint, and at risk the moment there are two ways
    /// to do the thing*). <see cref="NearestStoreHolding"/> still answers *"where does the
    /// village keep this?"* and is untouched; this answers a different question — *"is there a
    /// buffer between me and it?"* — and its answer is only ever compared against that one.
    /// Making <c>NearestStoreHolding</c> itself iterate workplaces would have put a
    /// <c>Workplace</c> into every caller that expects a <c>StoreBuilding</c>, which is D36's
    /// seam widening rather than being crossed once.
    /// </para>
    /// <para>
    /// <b>A null <paramref name="nearest"/> means nothing in the village's stores has it</b>,
    /// and then any farm holding it qualifies — a village whose granary is empty and whose
    /// farm is full should not have its larders go short over a technicality.
    /// </para>
    /// </remarks>
    private static Workplace? NearerWorkplaceStore(
        SimWorld world, GridPos from, Goods goods, StoreBuilding? nearest)
    {
        int beat = nearest is null
            ? int.MaxValue
            : world.TravelCost.TicksBetween(from, nearest.Position);

        Workplace? best = null;
        int bestCost = int.MaxValue;

        for (int i = 0; i < world.Workplaces.Count; i++)
        {
            Workplace workplace = world.Workplaces[i];
            if (workplace.IsSite || HeldOf(workplace.Store, goods) <= 0)
            {
                continue;
            }

            int cost = world.TravelCost.TicksBetween(from, workplace.Position);
            if (cost < beat && cost < bestCost)
            {
                best = workplace;
                bestCost = cost;
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
    /// Food one meal costs. <b>Dependants — the young and the old — eat a smaller portion</b>
    /// (see <see cref="SimConfig.DependantFoodSharePercent"/>).
    /// </summary>
    /// <remarks>
    /// <b>⭐ ELDERS JOINED CHILDREN HERE ON JOE'S CALL (2026-08-23), AND THE OLD BEHAVIOUR WAS
    /// NEVER A DECISION.</b> This had one branch and it tested for <see cref="LifeStage.Child"/>,
    /// so **an elder ate a full adult portion while producing at <c>vigour_min_percent</c>** —
    /// because the other arm of an <c>if</c> caught them, not because anybody ruled on ageing.
    /// *A number nobody picked is still a number the game is balanced on.*
    /// </remarks>
    public static int MealCostFor(Villager villager, SimConfig config)
    {
        if (villager.LifeStage is not (LifeStage.Child or LifeStage.Elder))
        {
            return config.FoodPerMeal;
        }

        int cost = config.FoodPerMeal * config.DependantFoodSharePercent / 100;
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
        // AND WHAT THE VILLAGE HOLDS, NOT WHAT IS IN THE GRANARIES (D161) — the same
        // correction one store over: food buffered at a farm is food the village has.
        bool needsFood = household.Stockpile.Food < world.TargetFoodFor(household)
            || world.FoodTheVillageHolds() < world.FoodTheVillageHasRoomFor();

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
            && SeasonRules.IsGatherable(world.Clock.Season);

        if (needsFood && canForage)
        {
            if (villager.Position == job!.Position)
            {
                BeginGathering(world, villager, config);
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
        //
        // The job is the HUT now (D108) — `!job.IsSite` rather than the old
        // `job.Construction is not null`, which is the same test read the other way round
        // and was true of exactly the workplaces that are no longer staffed. The hut is not
        // passed on: which site they walk to comes from the build queue, not from where they
        // hold their job.
        if (villager.CanWork && job?.Kind == JobKind.Builder && !job.IsSite
            && WorkTheSite(world, villager))
        {
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
            // ⭐ A MET LIMIT STOPS THE WORK, NOT JUST THE HIRING (D139). Joe: *"the woodcutter
            // keeps making firewood way past the limit."* He was right, and the panel heading
            // says what it should have been doing all along — *"how much to keep before the
            // work stops"*.
            //
            // The limit only ever reached `LabourQuota`, which decides how many woodcutters the
            // village ASKS for. Since D109 the player's number is the one that staffs the hut,
            // so a woodcutter Joe posted himself went on splitting for ever and the limit never
            // touched them. Measured once the shed was big enough to show it:
            // **firewood settled at 401 against a limit of 40.**
            //
            // Checked here, where the work actually happens, so it holds however the villager
            // came to be standing at the hut. Not an idle villager — they fall through to the
            // spare work below, which is the same thing a woodcutter with no logs does.
            if (world.StockLimits.IsMet(Goods.Firewood, world.FirewoodInSheds()))
            {
                villager.WorkNote =
                    $"Nothing to split — you asked the village to keep "
                    + $"{world.StockLimits.For(Goods.Firewood)} firewood and it has "
                    + $"{world.FirewoodInSheds()}.";
            }
            else
            {
            // The nearest shed that actually has a batch in it. Naming THAT shed rather
            // than "the shed" is the point of the refusal: with more than one, "the
            // shed has no logs" would be a sentence the player could not check.
            StoreBuilding? yard = NearestStoreWithLogs(world, job.Position, config.LogsPerSplit);
            if (yard is null)
            {
                villager.WorkNote =
                    $"Nothing to split — no store within reach of {job.Name} has the " +
                    $"{config.LogsPerSplit} logs a batch needs.";

                // Idle from their trade, so they may tidy or help clear (D87, D96). This
                // branch returns early rather than falling through to the bottom of
                // Decide, so it has to ask — and a woodcutter with an empty yard is the
                // most idle person in a winter village, which is exactly who both errands
                // are for. Same order as the bottom of Decide, deliberately: two copies of
                // one ranking is how they come to disagree.
                if (!TryTidyGround(world, villager) && !TryHelpWithHarvest(world, villager))
                {
                    GoHome(world, villager);
                }

                return;
            }

            villager.WorkNote = string.Empty;

            if (villager.Position == job.Position)
            {
                villager.State = VillagerState.MakingFirewood;
                villager.ActionTicksRemaining =
                    world.WorkTicksFor(villager, JobKind.Woodcutter, config.SplitTicks);
            }
            else
            {
                villager.State = VillagerState.TravelingToHut;
                Travel(world, villager, job.Position, VillagerState.MakingFirewood);
            }

            return;
            }

            // Held by the limit: idle from their trade, so they take spare work — the same
            // ranking a woodcutter with an empty yard takes, for the same reason.
            if (!TryTidyGround(world, villager) && !TryHelpWithHarvest(world, villager))
            {
                GoHome(world, villager);
            }

            return;
        }

        // ⭐ THE FARM (`specs/crops-and-orchards.md`). Sow in spring, reap in autumn, and be a
        // spare pair of hands the rest of the year.
        //
        // Placed beside the forester because it is the same shape — a workplace whose extent
        // is painted ground, asking *which of my tiles do I work next?* — and below the
        // gathering branch because a family with an empty larder still forages first.
        //
        // ⚠️ THE SEASON IS ANSWERED BY `NextFieldToWork`, NOT HERE, and that is deliberate:
        // the same question decides the errand and the quota's demand
        // (`SimWorld.FarmerSeatsWithGroundToWork`), so the village cannot want farmers for
        // work the farmer would decline, nor decline work the village wanted. Two copies of
        // one ranking is how they come to disagree (D142's three call sites).
        if (villager.CanWork && job?.Kind == JobKind.Farmer)
        {
            if (world.Zones.WorkGroundTiles(job.Id) == 0)
            {
                villager.WorkNote = $"{job.Name} has no ground to work — paint some for it.";
            }
            else if (world.NextFieldToWork(job, villager.Position) is GridPos field)
            {
                villager.WorkNote = string.Empty;
                villager.ErrandX = field.X;
                villager.ErrandY = field.Y;
                villager.State = VillagerState.TravelingToField;
                Travel(
                    world,
                    villager,
                    field,
                    SeasonRules.IsReaping(world.Clock.Season)
                        ? VillagerState.Reaping
                        : VillagerState.Sowing);
                return;
            }
            else
            {
                // Nothing for them this tick, and each reason has a different fix — so each
                // gets its own sentence rather than one shrug (§1.1). The order matters: a
                // met limit is the reason the player cannot see from the farm's own panel,
                // which is D147's finding and why that marker was worth building.
                villager.WorkNote = !world.MaySow() && SeasonRules.IsSowing(world.Clock.Season)
                    ? $"Nothing to sow at {job.Name} — you asked the village to keep "
                      + $"{world.StockLimits.For(Goods.Food)} food and it has "
                      + $"{world.FoodTheVillageHolds()}."
                    : SeasonRules.IsSowing(world.Clock.Season)
                        ? $"Every tile at {job.Name} is already sown."
                        : SeasonRules.IsReaping(world.Clock.Season)
                            ? $"Nothing standing to reap at {job.Name}."
                            : $"{job.Name} is out of season — nothing to sow or reap.";
            }

            // Idle from their trade, so they tidy or help clear — the same ranking the
            // bottom of this method uses, and the same one a woodcutter with an empty yard
            // takes. Two copies of it is how they come to disagree, so it is the same order.
            if (!TryTidyGround(world, villager) && !TryHelpWithHarvest(world, villager))
            {
                GoHome(world, villager);
            }

            return;
        }

        // Timber. Fellable year-round, unlike berries, so a logger still has
        // something to do in winter - which is part of why the job is worth holding.
        if (villager.CanWork && job?.Kind == JobKind.Forester)
        {
            // ⭐ A MET LIMIT STOPS THE FELLING AND NOT THE FORESTER (Joe, D146). D145 found that
            // D139's rule — *a met limit stops the work, not just the hiring* — had only ever
            // reached the woodcutter, and stopped the forester outright to match. Joe's
            // correction: *"a capped hut can replant. Priority should be replant → extra-hands
            // labour. It just shouldn't fell if it has met its cap."*
            //
            // **So the cap is not handled here at all any more.** It lives in `SimWorld.MayFell`
            // beside the felling toggle, because they are the same instruction at two distances,
            // and `NextGroundToWork` then hands back bare tiles to plant instead of trees to
            // fell. A capped hut works its way through its own ground putting it back, and only
            // reaches the spare-work branch below — tidying, clearing, home — once there is
            // nothing bare left. That ordering is Joe's, and it is the opposite of what I argued
            // for in D145.
            //
            // ⭐ A FORESTER'S HUT WORKS ITS OWN GROUND (D86, `forests-and-gathering.md`), where
            // a tree stand is an inexhaustible spot you stand on. So the question is which of
            // MY tiles to work next — a wooded one to fell, or a bare one to plant, depending
            // on the mode the player set. A hut with no ground painted yet falls through to the
            // stand behaviour below, which is what keeps this a no-op until somebody paints.
            if (world.Zones.WorkGroundTiles(job.Id) > 0)
            {
                if (world.NextGroundToWork(job, villager.Position) is GridPos ground)
                {
                    villager.WorkNote = string.Empty;
                    villager.ErrandX = ground.X;
                    villager.ErrandY = ground.Y;
                    villager.State = VillagerState.TravelingToTrees;
                    Travel(world, villager, ground, VillagerState.Cutting);
                    return;
                }

                // Nothing of theirs left to do — every tile is already the way the player asked
                // for it. Said out loud, because a forester standing about is otherwise the
                // silent stall §1.1 forbids, and each of the three reasons has a different fix.
                //
                // ⚠️ THE ONE THAT NEEDED SAYING IS THE CAP (D146). A hut whose ground is fully
                // wooded because the village stopped felling reads exactly like a hut that has
                // run out of work, and the answer — *raise the limit* — is nowhere near the
                // forester's panel unless the sentence points at it.
                villager.WorkNote = !world.MayFell(job)
                    ? job.Mode != WorkMode.FellAndPlant
                        ? $"{job.Name} is resting its wood — felling is switched "
                          + "off, and its ground is wooded again."
                        : $"{job.Name} has stopped felling — you asked the village "
                          + $"to keep {world.StockLimits.For(Goods.Logs)} logs and it has "
                          + $"{world.LogsInSheds()}. Its ground is wooded again."
                    : $"Nothing bare left to plant at {job.Name} — its ground is wooded again.";

                if (!TryTidyGround(world, villager) && !TryHelpWithHarvest(world, villager))
                {
                    GoHome(world, villager);
                }

                return;
            }

            // No ground painted: the legacy stand behaviour, felling a flat yield where they
            // stand. It answers to the same gate — a hut told not to fell does not fell here
            // either, and with no ground of its own there is nothing for it to plant instead.
            if (!world.MayFell(job))
            {
                villager.WorkNote =
                    $"{job.Name} is not felling, and has no ground to tend.";

                if (!TryTidyGround(world, villager) && !TryHelpWithHarvest(world, villager))
                {
                    GoHome(world, villager);
                }

                return;
            }

            if (villager.Position == job.Position)
            {
                villager.State = VillagerState.Cutting;
                villager.ActionTicksRemaining =
                    world.WorkTicksFor(villager, JobKind.Forester, config.CutTicks);
            }
            else
            {
                villager.State = VillagerState.TravelingToTrees;
                Travel(world, villager, job.Position, VillagerState.Cutting);
            }

            return;
        }

        // Pick up a load somebody left lying about, if there is nothing else to do (D96).
        //
        // ⭐ ABOVE CLEARING AND BELOW EVERY JOB, and both halves of that are the rule.
        // Below every job for D87's reason — anybody who reaches this line has already
        // declined their own work this tick, so it needs no quota and no job kind. Above
        // clearing because a load already won is worth more than a load not yet taken:
        // tidying before felling is what stops a painted valley producing heaps faster than
        // it produces order.
        //
        // It has to be its own errand rather than a pull, and that is D96's gift rather than
        // its cost: goods on the ground are supply-invisible, so "the village wants more
        // food" cannot reach them — that question reads stores. "There is a load lying about;
        // take it to a store" reaches them, and needs no construction site to exist, which is
        // the second of D66's two missing errands arriving at last.
        if (TryTidyGround(world, villager))
        {
            return;
        }

        // Clear ground the village painted, if there is nothing else to do (D87).
        //
        // ⭐ THE POSITION IS THE WHOLE RULE, and it is Joe's: "the harvest brush is only
        // for the jobless / laborers. Users with a specific job can harvest, but ONLY
        // when they are idle from their full-time job. They are just helping out on the
        // side. Their primary job comes first."
        //
        // Below every job branch and above resting is exactly that sentence in code.
        // Anybody who reaches this line has already declined their own work this tick —
        // a laborer who holds no job at all, a forager in winter, a forager whose larder
        // and village are both full, a woodcutter with an empty yard. Putting it any
        // higher would make felling outrank somebody's trade; any lower is resting.
        //
        // It is also the answer to the idle winter (D52, D66). Winter measured 86% idle
        // and the laborer shipped as a NAME because both errands proposed for them
        // occurred on 0.0% of ticks. This is work that was always on the map.
        if (TryHelpWithHarvest(world, villager))
        {
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

    /// <summary>
    /// Go and clear the nearest painted tile, if the village has asked for any.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Never for a child</b> — this is work, not carrying, which is the line D77 drew
    /// when it let children fetch.
    /// </para>
    /// <para>
    /// <b>The tile is remembered on the villager</b> rather than recomputed each step, for
    /// the reason <see cref="Villager.ErrandX"/> already records: the nearest tile is judged
    /// from where somebody is standing, so re-deciding while walking lets them shuttle
    /// between two trees forever without reaching either.
    /// </para>
    /// </remarks>
    /// <summary>
    /// Go and fetch a load somebody left on the ground, if any is worth fetching (D96).
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Never for a child</b> and never while already carrying: a pair of arms holds one
    /// load, and picking up a heap on top of a load is how goods get double-counted.
    /// </para>
    /// <para>
    /// <b>Only a heap a store would actually take</b> — the condition lives in
    /// <see cref="SimWorld.NearestGroundStack"/> and is what stops somebody shuttling a load
    /// back and forth to a full shed. A village with no room leaves its heaps alone, and
    /// tidies up when it has somewhere to tidy into.
    /// </para>
    /// <para>
    /// <b>The heap is remembered on the villager</b>, like a painted tile and for the reason
    /// <see cref="Villager.ErrandX"/> records: the nearest heap is judged from where somebody
    /// is standing, so re-deciding while walking lets them shuttle between two heaps forever
    /// without reaching either.
    /// </para>
    /// </remarks>
    private static bool TryTidyGround(SimWorld world, Villager villager)
    {
        if (!villager.CanWork || villager.IsCarrying)
        {
            return false;
        }

        GroundStack? stack = world.NearestGroundStack(villager.Position);
        if (stack is null)
        {
            return false;
        }

        villager.ErrandX = stack.Position.X;
        villager.ErrandY = stack.Position.Y;
        villager.State = VillagerState.TidyingGround;
        Travel(world, villager, stack.Position, VillagerState.TidyingGround);
        return true;
    }

    /// <summary>Standing on a heap: pick up an armful and take it to a store.</summary>
    /// <remarks>
    /// <b>One armful, like every other trip</b> (<c>carry_capacity</c>). That is what stops
    /// tidying being a teleport with extra steps — the same rule that makes a household's
    /// fetch a real journey (D32), applied to the mess.
    /// </remarks>
    private static void PickUpFromTheGround(SimWorld world, Villager villager)
    {
        var at = new GridPos(villager.ErrandX, villager.ErrandY);
        villager.ErrandX = 0;
        villager.ErrandY = 0;

        int room = world.Config.CarryCapacity;
        villager.CarriedFood += world.TakeFromGround(at, Goods.Food, room);
        room -= villager.CarriedFood;
        villager.CarriedLogs += world.TakeFromGround(at, Goods.Logs, room);
        room -= villager.CarriedLogs;
        villager.CarriedFirewood += world.TakeFromGround(at, Goods.Firewood, room);

        if (!villager.IsCarrying)
        {
            // Somebody got here first. Not an error — reconsider next tick.
            GoHome(world, villager);
            return;
        }

        villager.State = VillagerState.HaulingToStore;
        HaulOrSetDown(world, villager);
    }

    private static bool TryHelpWithHarvest(SimWorld world, Villager villager)
    {
        if (!villager.CanWork)
        {
            return false;
        }

        GridPos? tile = world.NearestHarvest(villager.Position);
        if (tile is null)
        {
            return false;
        }

        villager.ErrandX = tile.Value.X;
        villager.ErrandY = tile.Value.Y;
        villager.State = VillagerState.Clearing;
        Travel(world, villager, tile.Value, VillagerState.Clearing);
        return true;
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
    /// <summary>Whether a fetch is planned at all, exposed so the "worth the trip" bar can
    /// be asserted without waiting for a household to happen to dip.</summary>
    internal static StoreBuilding? PlanFetchForTest(SimWorld world, Villager villager) =>
        PlanFetch(world, villager);

    internal static void CollectForTest(SimWorld world, Villager villager) =>
        CollectFromStore(world, villager);

    /// <summary>Putting a load down at a store, exposed so a test can pose the case directly.</summary>
    /// <remarks>
    /// The same seam as <see cref="CollectForTest"/>, and for the same reason (D144). An
    /// armful of <em>two</em> goods arriving at a store that will only have one of them is a
    /// case the fixture village does not reliably produce — it depends on who is interrupted
    /// carrying what — so a twenty-year run is not a guard for it. Posed directly, it is three
    /// lines and cannot go vacuous.
    /// </remarks>
    internal static void ArriveWithALoadForTest(SimWorld world, Villager villager) =>
        ArriveAt(world, villager, VillagerState.HaulingToStore);

    /// <summary>A builder arriving at their site, posed directly (D154).</summary>
    /// <remarks>
    /// <b>The emergent case could not be reproduced, and that is the reason this seam exists.</b>
    /// The stall needs a builder to arrive carrying less than the site wants, which depends on
    /// what the nearest store happened to hold that tick — in the warm fixture the yard is
    /// always stocked well enough for one trip to finish the job, so a synthetic run passes
    /// however broken the branch is. Joe's audit trail is the evidence that it happens; this is
    /// the invariant that stops it happening again, and unlike a long run it cannot go vacuous.
    /// </remarks>
    internal static void RaiseForTest(SimWorld world, Villager villager) =>
        ArriveAt(world, villager, VillagerState.Building);

    /// <summary>A villager reaching a hearth to thaw, posed directly.</summary>
    /// <remarks>
    /// <b>The same seam as <see cref="RaiseForTest"/>, for the same reason and off the same
    /// evidence.</b> Joe's audit trail shows the jitter plainly — one tick at the fire, out
    /// again the next, for the whole of a winter — and the case needs a villager who is both
    /// cold and holding logs at the moment they arrive, which is a coincidence a long run
    /// produces only on some seeds. Posed directly it is four lines and cannot go vacuous.
    /// </remarks>
    internal static void ArriveAtAFireForTest(SimWorld world, Villager villager) =>
        ArriveAt(world, villager, VillagerState.SeekingShelter);

    /// <summary>Arriving at one's own door, posed directly — the anti-vacuity half.</summary>
    /// <remarks>
    /// Paired with <see cref="ArriveAtAFireForTest"/>: the fire rule is only worth anything if
    /// the <em>home</em> rule still holds, and a fix that stopped re-routing timber for
    /// everybody would revive D30's leak — a log in a larder is a log nobody can spend.
    /// </remarks>
    internal static void ArriveHomeForTest(SimWorld world, Villager villager) =>
        ArriveAt(world, villager, VillagerState.Idle);

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
        //
        // ⛔⭐ BUT FIRST IS NOT INSTEAD OF, AND CONFLATING THE TWO COST A WHOLE TRIP IN THREE.
        // This used to `return` the moment it had taken any food, so a villager who filled
        // fifteen of their forty carrying food walked home with a free hand and came straight
        // back for two firewood. **Found in Joe's audit trail, in the jitter he reported
        // twice:** Otto fetching 40 food, then 25 food, then 2 firewood from a store one tile
        // from his door — six ticks of a man visibly bouncing between two squares.
        //
        // Priority and exclusivity are different rules and only one of them was wanted. That is
        // D142's shape exactly — a rule that reached some of its call sites — and the fix is
        // the same: both halves in one place, with the second reading what the first left.
        int foodShort = world.TargetFoodFor(household) - household.Stockpile.Food;
        int food = Smallest(foodShort, load, target.Store.Food);
        if (food > 0 && target.Store.TryTake(Goods.Food, food))
        {
            villager.CarriedFood += food;
            load -= food;
        }

        // ⚠️ A FREE HAND, NOT A FREE TRIP. If food took the whole armful there is nothing left
        // to carry and this does nothing — the second trip is then carry capacity doing its
        // job (D32), which is the inequality distant households are supposed to feel, and not
        // something to optimise away.
        if (load <= 0)
        {
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
        if (villager.CarriedLogs > 0 && !AnywhereToPutTimber(world))
        {
            world.Log(
                LogLevel.Warn,
                "goods",
                $"{villager.Name} came home with {villager.CarriedLogs} logs and the village has " +
                "nowhere to put them, so they are stranded in the larder where nothing can " +
                "spend them. Build a storage pile or a shed.");

            larder.Receive(Goods.Logs, villager.CarriedLogs);
            villager.CarriedLogs = 0;
        }
    }

    /// <summary>
    /// Timber a feller could not carry away stays on the tile rather than ceasing to exist.
    /// </summary>
    /// <remarks>
    /// D133. Called from both felling paths, which had drifted into two copies of the same
    /// arithmetic and the same leak. Goods move only by trips people make (D96) — so the
    /// remainder is set down where it fell, and the tidy-ground errand brings it in.
    /// </remarks>
    private static void LeaveTheRestOnTheGround(SimWorld world, GridPos where, int logs)
    {
        if (logs > 0)
        {
            world.SetDown(where, Goods.Logs, logs);
        }
    }

    /// <summary>Is there anywhere in the village at all that will take timber?</summary>
    /// <remarks>
    /// <para>
    /// <b>⭐ IT ASKS WHAT IT NEEDS TO KNOW, WHICH IS NOT "IS THERE A SHED" (D132).</b> This was
    /// <c>AnyShedStanding</c> and it compared <c>Kind == StoreKind.Shed</c> — so a village with
    /// a <b>storage pile and no shed</b> was told it had nowhere to put timber, and every
    /// interrupted trip emptied its armful into a larder where nothing can spend it. **A pile
    /// takes anything**; it is the one store that does.
    /// </para>
    /// <para>
    /// <b>Joe caught it in his own game:</b> <i>"even with no one cutting wood, and laborers
    /// clearing forest, I can barely get enough logs. Nothing extra is being built."</i> His
    /// village held <b>31 logs in store and 50 in the Thatcher household's larder</b> — more
    /// timber in one family's house than in the whole settlement — while the woodcutter sat
    /// idle for want of the six logs a batch needs. The felling was fine and the hauling was
    /// fine. This is the leak, and it is precisely the failure the comment above already
    /// describes ("240 logs frozen in two houses for twenty years"), returning through a
    /// predicate rather than through the code it guards.
    /// </para>
    /// <para>
    /// <c>Accepts</c> is the question every other timber path already asks —
    /// <see cref="SimWorld.LogsInSheds"/> counts by it, <c>HaulOrSetDown</c> falls back to it
    /// (D76). This one place asked by name instead, and needed telling about each new kind of
    /// store. Asking "what will take this?" needs telling about none of them.
    /// </para>
    /// </remarks>
    private static bool AnywhereToPutTimber(SimWorld world)
    {
        for (int i = 0; i < world.StoreBuildings.Count; i++)
        {
            if (world.StoreBuildings[i].Accepts(Goods.Logs))
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

        // ⭐ OR STANDING AT A WORKPLACE WHOSE BUFFER THEY CAME FOR (§3.2 ruling 2, D161).
        //
        // ⛔ THIS IS THE HALF THAT WOULD HAVE BEEN MISSED, AND D144 IS THE RECORD OF MISSING
        // IT. *"A control tested at its predicate and never at its deposit is a control nobody
        // has tested"* — five guards asked `Accepts` and got the right answer, and not one of
        // them ever made a villager put anything down. Here the predicate is
        // `NearerWorkplaceStore` and this is the deposit: widen the market's reach without
        // this loop and a trader walks to the farm, finds nothing they know how to pick up,
        // and goes home empty-handed for ever.
        for (int i = 0; i < world.Workplaces.Count; i++)
        {
            Workplace workplace = world.Workplaces[i];
            if (workplace.IsSite || workplace.Position != villager.Position)
            {
                continue;
            }

            Household? family = world.FindHousehold(villager.ErrandHouseholdId);

            // ⭐⭐ NO HOUSEHOLD MEANS THEY CAME TO CLEAR THE BUFFER, NOT TO FILL A LARDER
            // (§3.2a, D171). Joe: *"the vendor can collect the food from the farm's stores…
            // and move it to the market (or the granary if the market is full)."*
            //
            // ⛔ WHERE IT GOES IS THE EXISTING PATH AND DELIBERATELY NOT A NEW ONE. Falling
            // out of this method carrying something ends in `HaulingToStore` → `HaulOrSetDown`
            // → *the nearest store with room*, which is the market when the market has room and
            // the granary when it does not. Writing a second store-finding rule here to say the
            // same thing is D145's *two ways to do the thing*, and D36's seam is exactly where
            // this project has paid for that before.
            if (family is null && villager.ErrandHouseholdId == 0)
            {
                int clearing = Smallest(load, load, workplace.Store.Food);
                if (clearing > 0 && workplace.Store.TryTake(Goods.Food, clearing))
                {
                    villager.CarriedFood += clearing;
                }

                break;
            }

            if (family is null)
            {
                break;
            }

            int shortOf = world.TargetFoodFor(family) - family.Stockpile.Food;
            int fromTheBuffer = Smallest(shortOf, load, workplace.Store.Food);
            if (fromTheBuffer > 0 && workplace.Store.TryTake(Goods.Food, fromTheBuffer))
            {
                villager.CarriedFood += fromTheBuffer;
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
            BeginGathering(world, villager, world.Config);
            return;
        }

        if (onArrival == VillagerState.HaulingToStore)
        {
            // The load goes into the building, and only then does it exist anywhere
            // the village can spend it. This is the moment goods stopped teleporting.
            //
            // ⭐ WHAT FITS GOES IN AND THE REMAINDER GOES DOWN (D96). Add returns what it
            // actually took — its own remarks say so and say it must not be ignored — and
            // this zeroed the arms regardless, so a load that arrived at a full store was
            // destroyed. RaiseTheBuilding one screen up gets it right and says why:
            // "never dropped, per the conservation rule".
            // ⚠️ AND EACH GOOD IS ASKED ABOUT SEPARATELY (D144). `StoreForTheLoad` chooses on
            // the ONE good it treats as the load — food, else logs, else firewood — so a
            // villager carrying two things walked to a store picked for the first and emptied
            // both arms into it. `Stockpile` is a dumb container and knows nothing of the
            // player's filter, so a pile set to logs-only took the firewood in the other arm.
            // Whatever the destination will not have goes down where they stand, one line
            // below, which is D96's rule already sitting here.
            if (StoreForTheLoad(world, villager) is StoreBuilding destination)
            {
                Stockpile store = destination.Store;

                if (destination.Accepts(Goods.Food))
                {
                    villager.CarriedFood -= store.Add(Goods.Food, villager.CarriedFood);
                }

                if (destination.Accepts(Goods.Logs))
                {
                    villager.CarriedLogs -= store.Add(Goods.Logs, villager.CarriedLogs);
                }

                if (destination.Accepts(Goods.Firewood))
                {
                    villager.CarriedFirewood -= store.Add(Goods.Firewood, villager.CarriedFirewood);
                }
            }

            if (villager.IsCarrying)
            {
                SetDownWhereTheyStand(world, villager);
            }

            // ⭐⭐ A FARMER GOES BACK TO THE ROWS, NOT HOME (Joe, 2026-08-16: *"2x farmers
            // planted 20 fields in the spring, and harvested only 9 in the fall — review the
            // efficiencies with planting and harvesting"*).
            //
            // **Measured in his own audit trail, and the circuit is the whole story.** A whole
            // autumn contained exactly five reaping cycles, each of them
            // `home → field → reap → store → home → rest`. **They walked all the way home
            // between every single tile**, and the home leg does nothing at all: `Decide` runs
            // there and sends them straight back out. Five round trips is a hundred and twenty
            // ticks, so the farm brought in five tiles of the seventeen it had sown.
            //
            // ⚠️ **Scoped to the harvest deliberately, and not made a general rule.** Going home
            // between loads is D30's trip model and it is what makes distance cost something
            // for every other job; a forester who felled one tree has no reason to be at the
            // stand again this instant. **A field is different because the work is a block of
            // ground the farmer is part-way through**, and a season is the deadline — a reaper
            // who stops for the walk home after every armful is not a reaper.
            if (SeasonRules.IsReaping(world.Clock.Season)
                && WorkplaceOf(world, villager) is { Kind: JobKind.Farmer } farm
                && world.NextFieldToWork(farm, villager.Position) is not null)
            {
                Decide(world, villager);
                return;
            }

            villager.State = VillagerState.TravelingHome;
            return;

        }

        if (onArrival == VillagerState.FetchingFromStore)
        {
            CollectFromStore(world, villager);
            villager.State = VillagerState.TravelingHome;
            return;
        }

        if (onArrival == VillagerState.Clearing)
        {
            // Somebody else may have taken it while this one walked, and regrowth or a
            // forester's hut can clear ground from under the paint. Not an error — go
            // and find another, or go home.
            var tile = new GridPos(villager.ErrandX, villager.ErrandY);
            if (!world.HasSomethingToHarvest(tile))
            {
                villager.ErrandX = 0;
                villager.ErrandY = 0;
                if (!TryHelpWithHarvest(world, villager))
                {
                    GoHome(world, villager);
                }

                return;
            }

            villager.State = VillagerState.Clearing;
            villager.ActionTicksRemaining =
                world.WorkTicksFor(villager, JobKind.Forester, world.Config.CutTicks);
            return;
        }

        if (onArrival == VillagerState.TidyingGround)
        {
            PickUpFromTheGround(world, villager);
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
            villager.ActionTicksRemaining =
                world.WorkTicksFor(villager, JobKind.Woodcutter, world.Config.SplitTicks);
            return;
        }

        if (onArrival is VillagerState.Sowing or VillagerState.Reaping)
        {
            ArriveInTheField(world, villager, onArrival);
            return;
        }

        if (onArrival == VillagerState.HaulingToFarm)
        {
            PutTheHarvestInTheFarm(world, villager);
            return;
        }

        if (onArrival == VillagerState.Cutting)
        {
            villager.State = VillagerState.Cutting;

            // ⭐ PUTTING A TREE BACK COSTS MORE THAN TAKING ONE DOWN
            // (`forests-and-gathering.md`), which is the number that decides whether
            // over-clearing is a mistake or a shrug. Stated as a multiple of felling rather
            // than as a second tick count, so the two can never drift apart.
            //
            // ⚠️ AND IT IS THE ERRAND THAT COSTS THAT, NOT THE MODE (D142). This asked
            // `Mode == Plant`, which was right while the modes were exclusive and became
            // wrong the moment D137 made planting a second errand and the default: every
            // fell in the village started billing three times its own price, and nobody
            // could see it because the villager still walked to a tree and came back with
            // logs. `IsPlantingErrand` is the one place that decides, so the duration and
            // the outcome cannot disagree about which job is being done.
            villager.ActionTicksRemaining = world.WorkTicksFor(
                villager,
                JobKind.Forester,
                IsPlantingErrand(world, villager)
                    ? VillageEconomy.PlantTicks(world.Config)
                    : world.Config.CutTicks);
            return;
        }

        // ⛔⛔ SOMEBODY WHO CAME IN TO GET WARM STAYS IN, AND THIS IS THE JITTER JOE HAS
        // WATCHED SINCE SHELTER SHIPPED (D45).
        //
        // **Found in his own audit trail, not in the suite** — the same file D154 came out of,
        // and the same shape. `TrySeekWarmth` has hysteresis on purpose: *"once they are here
        // they stay until they are properly warm, not merely until they are back under the line
        // they came in over."* **That promise was being overwritten one line later.** A villager
        // arriving at a hearth fell through to the tail of this method, and anyone still holding
        // logs was flipped straight to `HaulingToStore` and walked back out on the very next
        // tick — so they never thawed at all:
        //
        //   [t 425] Otto: hauling to a store -> going in to get warm at (-1, -1)
        //   [t 426] Otto: going in to get warm -> hauling to a store at (-1, 0)
        //   [t 437] Otto: hauling to a store -> going in to get warm at (-1, -1)
        //   [t 438] Otto: going in to get warm -> hauling to a store at (-1, 0)
        //
        // **One tick at the fire, every time, for the whole of winter.** On the map that is a
        // villager bouncing between two tiles; in the sim it is a man freezing to death beside
        // a lit hearth because his arms were full. Hattie crossed the seek-shelter line upward
        // **every two ticks for sixty ticks** in the same run.
        //
        // ⚠️ AND IT ALSO UNLOADED INTO THE WRONG LARDER. `NearestFire` returns *any* household's
        // hearth (§4.3 — a neighbour with a fire does not turn a freezing man away), while
        // `UnloadAtHome` posts to the villager's OWN household wherever they are standing. So
        // warming up at a neighbour's fire teleported an armful home. Both bugs die here,
        // because the answer to both is the same: **arriving at a fire is not arriving home.**
        if (onArrival == VillagerState.SeekingShelter)
        {
            villager.State = VillagerState.SeekingShelter;
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

    private static void BeginGathering(SimWorld world, Villager villager, SimConfig config)
    {
        villager.State = VillagerState.Gathering;
        villager.ActionTicksRemaining =
            world.WorkTicksFor(villager, JobKind.Forager, config.GatherTicks);
    }

    // ---------------------------------------------------------------
    //  The farm (`specs/crops-and-orchards.md`)
    // ---------------------------------------------------------------

    /// <summary>Standing on the tile they set off for: start the work, or find another.</summary>
    /// <remarks>
    /// <b>The ground is re-asked on arrival, and every errand in this file that walks to a tile
    /// has learnt to do that.</b> A season can turn mid-walk, another farmer can take the row
    /// first, and a building can be marked over a standing crop (Joe's warn-and-allow ruling) —
    /// so the tile they set off for may not be the tile they find. Not an error: they go and
    /// find another, exactly as the clearing errand does.
    /// </remarks>
    private static void ArriveInTheField(SimWorld world, Villager villager, VillagerState work)
    {
        var tile = new GridPos(villager.ErrandX, villager.ErrandY);
        Terrain here = world.Map.TerrainAt(tile);

        bool stillTheWork = work == VillagerState.Sowing
            ? SimWorld.IsSowable(here) && SeasonRules.IsSowing(world.Clock.Season)
            : here == Terrain.Ripe && SeasonRules.IsReaping(world.Clock.Season);

        if (!stillTheWork)
        {
            villager.ErrandX = 0;
            villager.ErrandY = 0;
            Decide(world, villager);
            return;
        }

        villager.State = work;
        villager.ActionTicksRemaining = world.WorkTicksFor(
            villager,
            JobKind.Farmer,
            work == VillagerState.Sowing ? world.Config.SowTicks : world.Config.ReapTicks);
    }

    /// <summary>
    /// Put a harvest into the farm's own store — <b>the first write to
    /// <see cref="Workplace.Store"/> in the project's history</b>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>⛔⛔ <c>Stockpile.Add</c> RETURNS WHAT IT ACTUALLY TOOK, AND THIS READS IT.</b> Not
    /// reading it is D96 exactly — 17,451 food deposited into a full granary and out of the
    /// world over fifty years — and D144 is the same shape one deposit path over, where every
    /// batch of firewood after the woodyard filled was destroyed at the doorstep. Both were
    /// invisible, and both were found by Joe playing rather than by the suite. <b>This path had
    /// never been exercised by anything</b>, so it has never had the chance to be got wrong,
    /// and it is the reason `crops-and-orchards.md §6` gives the farm's store a seam of its
    /// own.
    /// </para>
    /// <para>
    /// <b>What will not fit stays in their arms and walks on to a store</b> rather than going on
    /// the ground here. The farm buffer is a convenience; a full one is not a village with
    /// nowhere to put things, it is a village with a longer walk — which is exactly the
    /// behaviour <c>farm_store_cap</c> exists to create.
    /// </para>
    /// </remarks>
    private static void PutTheHarvestInTheFarm(SimWorld world, Villager villager)
    {
        if (WorkplaceOf(world, villager) is Workplace farm)
        {
            villager.CarriedFood -= farm.Store.Add(Goods.Food, villager.CarriedFood);
        }

        if (villager.CarriedFood > 0 || villager.IsCarrying)
        {
            HaulOrSetDown(world, villager);
            return;
        }

        // Empty-handed and standing at the farm: straight back to the rows.
        Decide(world, villager);
    }

    /// <summary>
    /// Set off with an armful of harvest — <b>to the nearest storage with room</b> (Joe).
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>⭐ THIS IS WHAT MAKES <c>farm_store_cap</c> MEAN SOMETHING.</b> Joe: *"the farmer
    /// reaps and hauls to the nearest available storage"* — nearest <em>with room</em>. The
    /// farm's own store is underfoot, so it wins while it has space and the walk is short;
    /// once it is full the granary is the nearest storage with room and the walk lengthens.
    /// The buffer is free, and running it dry is the market's job (§3.2).
    /// </para>
    /// <para>
    /// <b>One place decides, and that is the point.</b> A workplace store and a store building
    /// live in two lists with two id spaces (D36's seam), so the comparison has to be made
    /// somewhere; making it here — once, on the way out — is what stops it becoming a second
    /// way to find a store, which D145 names as the moment a control stops being safe.
    /// </para>
    /// </remarks>
    private static void HaulTheHarvest(SimWorld world, Villager villager, Workplace farm)
    {
        // ⛔ "WITH ROOM" MEANS ROOM FOR THE WHOLE LOAD, AND ASKING `IsFull` INSTEAD COST A
        // MEASUREMENT TO FIND. A tile of crop yields more than the buffer's entire capacity, so
        // a farm with one unit of space left counted as "not full", took that one unit, and the
        // farmer walked on to the granary with the rest — **two long walks for one tile**, and a
        // measured throughput of one tile a year against the four the economy budgets for.
        //
        // The spec's own words are *"the nearest available storage — nearest WITH ROOM"*, and
        // room for a fraction of an armful is not room. Asking the honest question makes the
        // buffer do what `farm_store_cap` was described as doing: it absorbs whole loads while
        // it can, and when it cannot the walk is long ONCE rather than one-and-a-half times.
        int toTheFarm = farm.Store.FreeSpace < villager.CarriedFood
            ? int.MaxValue
            : world.TravelCost.Cost(villager.Position, farm.Position);

        StoreBuilding? store = world.NearestStoreAccepting(
            villager.Position, Goods.Food, static place => !place.Store.IsFull);

        int toAStore = store is null
            ? int.MaxValue
            : world.TravelCost.Cost(villager.Position, store.Position);

        // ⭐ AND IT SAYS WHY, BECAUSE THIS CHOICE IS INVISIBLE AND IT DECIDES THE HARVEST
        // (Joe, 2026-08-22: *"the farmer cant harvest as much as it sows"*). His run made
        // **27 hauls and not one of them to the farm** — and nothing in the trail said whether
        // the buffer was full, farther away, or never consulted, so answering it meant reading
        // the code and guessing. METHODOLOGY §4's rule is that every refusal writes its own
        // reason; this is a refusal.
        //
        // ⚠️ Guarded, because it is one line per reaped tile and the long acceptance runs
        // discard it (METHODOLOGY §4 — interpolation happens before the sink gets a say).
        if (world.Logs(LogLevel.Debug))
        {
            world.Log(
                LogLevel.Debug,
                "goods",
                $"{villager.Name} #{villager.Id}: {villager.CarriedFood} food from the field — "
                + $"{farm.Name} has {farm.Store.FreeSpace} free of {farm.Store.Capacity} "
                + $"(cost {(toTheFarm == int.MaxValue ? "no room" : toTheFarm.ToString())}), "
                + $"nearest store {(store is null ? "none" : store.Name)} "
                + $"(cost {(toAStore == int.MaxValue ? "unreachable" : toAStore.ToString())}).");
        }

        // Ties go to the farm, which is the reading that matches the sentence: the buffer is
        // underfoot, so "nearest with room" means the farm whenever the farm will have it.
        if (toTheFarm != int.MaxValue && toTheFarm <= toAStore)
        {
            villager.State = VillagerState.HaulingToFarm;
            Travel(world, villager, farm.Position, VillagerState.HaulingToFarm);
            return;
        }

        // Everything full, or no store at all: the ordinary path, which ends in a heap on the
        // ground rather than in goods leaving the world (D96).
        villager.State = VillagerState.HaulingToStore;
        HaulOrSetDown(world, villager);
    }

    /// <summary>
    /// Whether the errand this forester has walked out to is a <b>planting</b> one rather
    /// than a fell.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Both halves, in one place (D142).</b> D137 made planting a second errand rather
    /// than a different job — <em>trees first while any stand on their ground, bare tiles
    /// when none do</em> — and it named the two call sites that had to carry that. There
    /// were three, and the two that were changed each kept only half the rule:
    /// </para>
    /// <list type="bullet">
    /// <item>the <b>duration</b> asked the mode alone, so with planting on by default every
    /// <em>fell</em> in the village was billed at <c>PlantTicks</c> — three times its own
    /// price, invisibly, because the villager still walked to a tree and came back with
    /// logs;</item>
    /// <item>the <b>outcome</b> asked the tile alone, so a hut with planting switched
    /// <em>off</em> planted a sapling whenever the tree it had walked to was gone by the
    /// time it arrived.</item>
    /// </list>
    /// <para>
    /// <b>⭐ AND SINCE D146 THE TILE IS THE WHOLE ANSWER, because planting is no longer a mode
    /// that can be off.</b> Felling is the toggle now, and a hut that may not fell is only ever
    /// sent to bare ground — so <em>the errand is a plant exactly when the tile has no tree on
    /// it</em>, whatever stopped the felling. The mode test that D142 added here is gone
    /// because the state it guarded against no longer exists, and the guard that covered it
    /// is now <c>FellingOffMeansItNeverFells</c>.
    /// </para>
    /// <para>
    /// A hut with no ground of its own is the old tree-stand behaviour and never plants;
    /// that is the same guard <c>CompleteAction</c> puts round the tile path, so the two
    /// cannot disagree about which job is being done.
    /// </para>
    /// </remarks>
    private static bool IsPlantingErrand(SimWorld world, Villager villager)
    {
        Workplace? hut = WorkplaceOf(world, villager);

        if (hut is null || world.Zones.WorkGroundTiles(hut.Id) == 0)
        {
            return false;
        }

        return world.Map.TerrainAt(new GridPos(villager.ErrandX, villager.ErrandY))
            != Terrain.Forest;
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
                // ⭐ WHAT THE PLACE IS WORTH, THEN WHAT THE PERSON IS WORTH
                // (`forests-and-gathering.md`). This read `world.FoodSource.YieldPerGather`
                // flat, because every gathering job was a berry patch and every berry patch
                // was the same. A gatherer's hut is worth what the trees in its ring say.
                //
                // **No workplace now means no food**, where it used to fall back to the
                // patch's flat yield. With the patches retired there is nowhere to gather
                // that is not a hut, so a gatherer with no hut is somebody standing in a
                // field — and paying them for it would be the last of the free food.
                Workplace? patch = WorkplaceOf(world, villager);
                int perTrip = patch is null ? 0 : world.GatherYieldAt(patch);

                int yield = perTrip * villager.Vigour / 100;

                // ⚠️ AND THE FLOOR OF ONE APPLIES TO VIGOUR, NOT TO THE GROUND. An old
                // gatherer still brings something back; a hut with no trees in its ring
                // brings back NOTHING, which is the whole of "no forest, no food" and the
                // rule Joe asked for by name. Flooring both together would have quietly made
                // a bald ring feed the village.
                if (yield < 1 && perTrip > 0)
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
                    $"{(homeNeedsIt ? "home" : StoreForTheLoad(world, villager)?.Name ?? "the ground")}" +
                    $" — {world.Clock}.");
                return;

            case VillagerState.Cutting:
                // ⭐ A FORESTER'S HUT WORKS A TILE; A TREE STAND IS A SPOT YOU STAND ON.
                // (`forests-and-gathering.md`, D86.) The stand yields forever, which is the
                // placeholder this replaces — its wood has a location and a quantity now, and
                // felling it takes it off the map where the player can see the gap.
                Workplace? wood_ = WorkplaceOf(world, villager);
                if (wood_ is not null && world.Zones.WorkGroundTiles(wood_.Id) > 0)
                {
                    var tile = new GridPos(villager.ErrandX, villager.ErrandY);

                    // ⭐ AND PLANTING IS THE ONLY THING IN THIS GAME THAT GIVES BACK (Joe).
                    // It carries nothing home, which is why it is a mode rather than a job:
                    // a forester who plants all year feeds nobody, and that is the trade the
                    // player is making when they switch it.
                    // ⭐ WHAT IS ON THE TILE DECIDES, AND SO DOES THE MODE (D137, corrected by
                    // D142). The tile half is why this cannot ask the mode alone —
                    // `NextGroundToWork` sends a tending forester to a standing tree while any
                    // remain, so a mode-only test would plant on top of the tree it just walked
                    // to. But the mode half went missing with it, and a hut reading
                    // *"Planting: off"* planted every time it arrived at a tile whose tree had
                    // gone in the meantime — the exact thing D137 set out to stop.
                    if (IsPlantingErrand(world, villager))
                    {
                        world.Plant(tile);
                        villager.State = VillagerState.Idle;
                        return;
                    }

                    (Goods felled, int fromTheTile) = world.Harvest(tile);
                    if (felled == Goods.Logs && fromTheTile > 0)
                    {
                        // Vigour scales what they carry home in one go — the same way it
                        // scales a gather — and WHAT THEY CANNOT CARRY STAYS ON THE TILE.
                        // See the clearing case below for why the second half matters.
                        int carried = fromTheTile * villager.Vigour / 100;
                        carried = carried < 1 ? 1 : carried;
                        villager.CarriedLogs += carried;
                        LeaveTheRestOnTheGround(world, tile, fromTheTile - carried);
                    }

                    villager.State = VillagerState.HaulingToStore;
                    HaulOrSetDown(world, villager);
                    return;
                }

                // Vigour scales timber the same way it scales berries: an ageing
                // woodcutter brings back less for the same day's walk.
                //
                // Read from the config rather than from `world.TreeStand`, which is deleted
                // with the stands (slice 5). It was always just `cut_yield` in a wrapper —
                // one stand's worth, read by every forester in the village.
                int wood = world.Config.CutYield * villager.Vigour / 100;
                if (wood < 1)
                {
                    wood = 1;
                }

                // Picked up, not banked. The logs go to the shed on the way home,
                // which is what makes them the village's rather than this family's.
                villager.CarriedLogs += wood;
                villager.State = VillagerState.HaulingToStore;
                HaulOrSetDown(world, villager);
                return;

            case VillagerState.Sowing:
                // ⭐ THE YEAR IS COMMITTED. Nothing is carried and nothing is produced — this
                // is the one action in the game whose whole payoff is two seasons away, which
                // is what makes spring a decision rather than a chore.
                //
                // The crop id is written beside the terrain (`crops-and-orchards.md §4`): the
                // triple says what STAGE the tile is at, the id says WHAT is growing on it,
                // and exactly one crop is defined so far. A field remembers what it grows.
                var sown = new GridPos(villager.ErrandX, villager.ErrandY);
                if (SimWorld.IsSowable(world.Map.TerrainAt(sown)))
                {
                    world.SetTerrain(sown, Terrain.Sown);
                    world.Map.SetCrop(sown, TheOneCrop);
                }

                villager.ErrandX = 0;
                villager.ErrandY = 0;
                villager.State = VillagerState.Idle;
                return;

            case VillagerState.Reaping:
                // ⭐ AND THE HARVEST COMES IN. The tile goes back to bare `Field` and KEEPS ITS
                // CROP ID, so next spring knows what to put in it — the reaping and the
                // abandoning of a field are different events and only the second clears the id
                // (`GeneratedMap.SetCrop`).
                //
                // ⛔ IT DOES NOT GO THROUGH `world.Harvest`, AND THAT IS THE SEAM. `Harvest`
                // answers the harvest BRUSH — *what may a laborer clearing painted ground
                // take?* — and `Terrain.Ripe` is deliberately absent from
                // `TerrainRules.Yields` so that a laborer can never reap the farm. Two verbs
                // that look alike and must not share a door (`crops-and-orchards.md §6`).
                var reaped = new GridPos(villager.ErrandX, villager.ErrandY);
                villager.ErrandX = 0;
                villager.ErrandY = 0;

                if (world.Map.TerrainAt(reaped) != Terrain.Ripe)
                {
                    // Beaten to it, or built over between arriving and finishing. Not an error.
                    Decide(world, villager);
                    return;
                }

                world.SetTerrain(reaped, Terrain.Field);

                // Vigour scales what a day's work brings home, the same way it scales a gather
                // and a fell — so a farm run by an ageing household feeds fewer people, which
                // is D12 arriving in the newest food source rather than being forgotten by it.
                //
                // ⭐ AND THE GROUND SCALES IT TOO (D178). `CropYieldAt` is the farm's half of
                // per-site yield — the sibling of `GatherYieldAt`, which has made a gatherer's
                // hut worth what the trees around it are worth since D112. **Asked of the tile
                // that was reaped, not of the farm**, because a field can span better and worse
                // ground and the player should be able to see that on the map.
                int crop = world.CropYieldAt(reaped) * villager.Vigour / 100;
                villager.CarriedFood += crop < 1 ? 1 : crop;

                if (WorkplaceOf(world, villager) is Workplace theirFarm)
                {
                    HaulTheHarvest(world, villager, theirFarm);
                    return;
                }

                villager.State = VillagerState.HaulingToStore;
                HaulOrSetDown(world, villager);
                return;

            case VillagerState.Clearing:
                // The tile is SPENT — that is D84's deposit rule, and the whole
                // difference between this and a tree stand. Vigour scales it the same
                // way it scales berries and timber: the same day's work brings back
                // less as somebody ages.
                var cleared = new GridPos(villager.ErrandX, villager.ErrandY);
                (Goods goods, int amount) = world.Harvest(cleared);
                villager.ErrandX = 0;
                villager.ErrandY = 0;

                if (amount <= 0)
                {
                    // Beaten to it between arriving and finishing. Not an error.
                    GoHome(world, villager);
                    return;
                }

                int taken = amount * villager.Vigour / 100;
                if (taken < 1)
                {
                    taken = 1;
                }

                villager.CarriedLogs += goods == Goods.Logs ? taken : 0;

                // ⭐ AND THE REST OF THE TREE STAYS WHERE IT FELL (D133).
                //
                // Joe, playing, after the larder leak was closed and it still felt wrong:
                // *"it still feels wrong — there's never enough wood but so many trees are
                // harvested."* This is why, and it was deliberate rather than a slip. The
                // comment on the forester's copy of this said it out loud: *"an ageing
                // forester fells the tree and brings back less of it."*
                //
                // **That reasoning is right for a gather and wrong for a fell, and the
                // difference is whether the source survives.** Scaling a berry harvest by
                // vigour is fine — the bush is still standing, and a tired picker simply
                // picks fewer. `Harvest` has already set this tile to Grass. The tree is
                // GONE from the map, so scaling what the villager carries did not mean they
                // took less: it meant the remainder stopped existing.
                //
                // At `vigour_min_percent: 55` an old villager destroyed 45% of every tree
                // they touched, and the player watched their woodland disappear into a
                // trickle of logs. Nothing in the game showed the loss, because a number
                // that is never written down cannot be read back.
                //
                // Vigour keeps its meaning — a weak villager still carries less in one
                // armful — and D96's ground stacks are exactly the machinery for the other
                // half: the timber lies on the tile until somebody fetches it.
                int left = goods == Goods.Logs ? amount - taken : 0;
                LeaveTheRestOnTheGround(world, cleared, left);

                world.Log(LogLevel.Debug, "behavior",
                    $"{villager.Name} cleared {cleared} for {taken} {goods}"
                    + (left > 0 ? $", leaving {left} on the ground" : string.Empty)
                    + $" — {world.Clock}.");

                villager.State = VillagerState.HaulingToStore;
                HaulOrSetDown(world, villager);
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
                // Back into the shed the logs came out of where it will take them, so a
                // woodyard stays one place rather than becoming a two-shed shuffle.
                //
                // ⛔ IT USED TO ASK ONLY WHETHER THE YARD WAS FULL, AND THAT PUT FIREWOOD IN
                // A STORE THE PLAYER HAD JUST SAID WOULD NOT TAKE IT (D144). Joe, playing:
                // *"my logs-only storage pile allowed firewood. I set it to logs-only as soon
                // as it was built."* A pile that takes logs is exactly what `NearestStoreWithLogs`
                // picks to split from, so the one store guaranteed to be chosen here was the one
                // his filter was about — **80 firewood in a pile set to logs.** `Accepts` is the
                // question, and this line asked `IsFull` instead: **D132's bug, one predicate
                // over**, and the third time a timber path has decided by something other than
                // what a store will hold.
                //
                // The fallback is the same one the full case always used, and it is now the
                // fallback for both reasons a yard can refuse — no room, and not allowed.
                StoreBuilding? wall =
                    woodyard.Accepts(Goods.Firewood) && !woodyard.Store.IsFull
                        ? woodyard
                        : world.NearestStoreAccepting(
                            villager.Position, Goods.Firewood, static store => !store.Store.IsFull);

                // ⚠️ AND WHAT WILL NOT FIT GOES ON THE GROUND RATHER THAN NOWHERE (D96). The
                // return value of `Add` was discarded, so a batch that overflowed the last
                // inch of a store was destroyed at the doorstep — the leak D96 closed for
                // every other producer and never for this one. A village whose stores are all
                // full or all filtered now has a visible heap of firewood beside the hut,
                // which is the signal to build somewhere to put it (D134).
                int stored = wall?.Store.Add(Goods.Firewood, firewood) ?? 0;
                if (stored < firewood)
                {
                    world.SetDown(villager.Position, Goods.Firewood, firewood - stored);
                }

                if (world.Logs(LogLevel.Debug))
                {
                    world.Log(LogLevel.Debug, "behavior",
                        $"{villager.Name} split {firewood} firewood — "
                        + (stored > 0 ? $"{stored} into {wall!.Name}" : "no store would take it")
                        + (stored < firewood ? $", {firewood - stored} set down" : string.Empty)
                        + $" — {world.Clock}.");
                }

                villager.State = VillagerState.TravelingHome;
                return;

            default:
                // A timed action finished in a state that has no completion effect —
                // that is the travel-delay case, which resolves on the next tick.
                return;
        }
    }
}

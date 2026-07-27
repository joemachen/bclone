using Bclone.Sim.Config;

namespace Bclone.Sim.World;

/// <summary>
/// Works out whether a village's food economy can actually sustain itself.
/// </summary>
/// <remarks>
/// <para>
/// This exists because the economy was tuned by iteration — six values changed
/// across two sittings, each guessed from a symptom, and the village still
/// boom-busted. Tuning by iteration cannot tell you <em>why</em> a number works, so
/// it cannot tell you when a later change breaks it.
/// </para>
/// <para>
/// So the target is stated up front instead:
/// </para>
/// <para>
/// <b>A single adult at their weakest — minimum vigour, no partner — must be able to
/// feed themselves and <see cref="RequiredDependants"/> children.</b>
/// </para>
/// <para>
/// That number is not arbitrary. It is the widowed-parent case, which the diagnostic
/// run showed was killing nearly every household: one parent dies and the survivor
/// carries the children alone on declining vigour. If the weakest realistic worker
/// cannot hold a household together, the village dies out however the rest is tuned.
/// </para>
/// <para>
/// Everything here is derived from <see cref="SimConfig"/> and asserted by tests, so
/// a future change to hunger, travel, or vigour that quietly breaks the target fails
/// the build rather than the village.
/// </para>
/// </remarks>
public static class VillageEconomy
{
    /// <summary>
    /// Children one weakest-case adult must be able to support on top of themselves.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Three, not two.</b> Two was the bare widowed-parent case — a survivor
    /// carrying a household of three — and solving for exactly that produced a
    /// village sitting at break-even by construction. It survived only while nothing
    /// pushed on it: switching on a real catchment or a thinner winter store killed
    /// it, each independently.
    /// </para>
    /// <para>
    /// So the target is deliberately set <em>above</em> the bare case. The third
    /// dependant is not a mouth anyone has to feed — it is the slack that pressure
    /// eats into. A village with no margin cannot have systems that push on it, and
    /// systems that push are the entire point of §2.3.
    /// </para>
    /// </remarks>
    public const int RequiredDependants = 3;

    /// <summary>Ticks between meals: hunger climbs to the eat threshold, then resets.</summary>
    public static int MealIntervalTicks(SimConfig config)
    {
        ArgumentNullException.ThrowIfNull(config);

        int interval = config.EatThreshold / config.HungerPerTick;
        return interval < 1 ? 1 : interval;
    }

    /// <summary>Meals one villager eats in a year.</summary>
    public static int MealsPerYear(SimConfig config) =>
        config.TicksPerYear / MealIntervalTicks(config);

    /// <summary>Food one adult eats in a year.</summary>
    public static int AdultFoodPerYear(SimConfig config) =>
        MealsPerYear(config) * config.FoodPerMeal;

    /// <summary>Food one child eats in a year.</summary>
    public static int ChildFoodPerYear(SimConfig config)
    {
        int childMeal = config.FoodPerMeal * config.ChildFoodSharePercent / 100;
        return MealsPerYear(config) * (childMeal < 1 ? 1 : childMeal);
    }

    /// <summary>Ticks for one round trip to the food source and back, including gathering.</summary>
    public static int RoundTripTicks(SimConfig config)
    {
        ArgumentNullException.ThrowIfNull(config);

        // Budget for the worst walk the village will ever ASK anyone to make — which
        // is now a promise rather than a measurement (see MaxHomeToWorkTiles).
        //
        // This used to scan where a square spiral happened to drop twenty homes and
        // take the worst of them, and both ways of getting that wrong killed a village
        // before. Budgeting for household #1 made every outlying family a rounding
        // error that starved. Budgeting for the LAST home's nearest site was worse the
        // other way: that home happened to sit one tile from a thicket, so the economy
        // was derived as though everybody had a one-tile commute and the yield it
        // produced fed nobody.
        //
        // Both were symptoms of the same thing — the economy taking whatever the
        // layout gave it. Household.ChooseSite now refuses to build further out than
        // this, so the budget is something the village keeps rather than something it
        // discovers.
        int travel = MaxHomeToWorkTiles(config) * config.TravelTicksPerUnit;
        return (travel * 2) + config.GatherTicks;
    }

    /// <summary>Distance from a home to the <em>nearest</em> forage site.</summary>
    /// <remarks>
    /// Nearest, not first. Several sites exist precisely so that an outlying household
    /// has a short walk to one of them (D19), and an economy budgeting for the far
    /// patch would throw that away — it would derive a yield generous enough that
    /// catchment could never bind without the village getting rich, which is the
    /// opposite of what the sites are for.
    /// </remarks>
    public static int NearestForageDistance(SimConfig config, GridPos from)
    {
        ArgumentNullException.ThrowIfNull(config);

        // Against the CANONICAL valley — the jitter-free ring — plus the worst jitter
        // the generator is allowed to add (D18).
        //
        // This is what keeps ONE economy for every seed. Deriving from the actual
        // generated sites would make gather_yield a property of the seed, so two runs
        // would have different physics and a shared seed would stop being comparable.
        // Budgeting against the canonical layout and paying for the worst jitter up
        // front means every valley the generator can produce fits inside the economy
        // by construction — no reject-and-redraw, and no seed quietly unsurvivable.
        List<GridPos> sites = MapGenerator.CanonicalForageSites(config);

        int nearest = int.MaxValue;
        for (int i = 0; i < sites.Count; i++)
        {
            int distance = from.ManhattanDistanceTo(sites[i]);
            if (distance < nearest)
            {
                nearest = distance;
            }
        }

        if (nearest == int.MaxValue)
        {
            throw new InvalidOperationException(
                "The valley has no forage sites at all; no economy can be derived for it.");
        }

        // Jitter can only ever move a site further from a given home by this much, so
        // paying for it here is the conservative reading.
        return nearest + (config.SiteJitterTiles * 2);
    }

    // ---------------------------------------------------------------
    //  What placement guarantees, and what the economy is derived from
    // ---------------------------------------------------------------

    /// <summary>
    /// The furthest a home may ever be from its work.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>This is a guarantee, not an observation, and that inversion is the point.</b>
    /// The economy used to be derived by scanning where a square spiral <em>happened</em>
    /// to put twenty homes and taking the worst — so the budget was whatever the layout
    /// gave it, and a generated valley could hand it a layout it could not afford.
    /// Now <see cref="Household.ChooseSite"/> refuses to build beyond this distance, so
    /// the budget holds by construction and the derivation has something firm to stand
    /// on.
    /// </para>
    /// <para>
    /// Stated as: <b>a home is never further from work than the sites are from the
    /// middle of the valley</b> — the ring radius, plus the worst the jitter can add.
    /// A village that cannot honour that has run out of valley, which is a legible
    /// thing to be told.
    /// </para>
    /// </remarks>
    public static int MaxHomeToWorkTiles(SimConfig config)
    {
        ArgumentNullException.ThrowIfNull(config);

        return config.ForageSiteRingTiles + (config.SiteJitterTiles * 2);
    }

    /// <summary>How far a home may sit from the middle of the village.</summary>
    /// <remarks>
    /// The other half of the bound. Without it a household could chase a distant site
    /// out to the valley's edge and then spend its life walking back to the granary —
    /// which is the same mistake in the other direction, and D32 says the interesting
    /// inequality is distance to the store.
    /// </remarks>
    public static int MaxHomeToVillageTiles(SimConfig config)
    {
        ArgumentNullException.ThrowIfNull(config);

        return config.ForageSiteRingTiles + (config.SiteJitterTiles * 2);
    }

    /// <summary>
    /// Foraging trips one adult can complete in a year.
    /// </summary>
    /// <remarks>
    /// Only three seasons are gatherable, and meals interrupt the working day — a
    /// villager who eats mid-trip loses that tick to eating.
    /// </remarks>
    public static int TripsPerYear(SimConfig config)
    {
        int gatherableTicks = config.TicksPerYear * 3 / 4;
        int ticksLostToMeals = MealsPerYear(config) * 3 / 4;
        int available = gatherableTicks - ticksLostToMeals;

        return available <= 0 ? 0 : available / RoundTripTicks(config);
    }

    /// <summary>Ticks for one round trip to the tree stand and back, including cutting.</summary>
    /// <remarks>Same worst-home budget as foraging, so the two kinds of work are
    /// costed on the same basis rather than one being quietly cheaper.</remarks>
    public static int CutRoundTripTicks(SimConfig config)
    {
        ArgumentNullException.ThrowIfNull(config);

        // The canonical stand, furthest-out slot, plus worst-case jitter — same basis
        // as the forage budget above, so neither kind of work is quietly cheaper.
        List<GridPos> stands = MapGenerator.CanonicalTreeStands(config);
        GridPos stand = stands.Count > 0
            ? stands[stands.Count - 1]
            : new GridPos(config.TreeStandRingTiles, 0);
        stand = new GridPos(
            stand.X + config.SiteJitterTiles, stand.Y + config.SiteJitterTiles);

        var shed = new GridPos(config.StorageShedX, config.StorageShedY);

        // Home to the stand, the stand to the SHED, and the shed home again.
        //
        // The middle leg is new (D30) and it is not a rounding error: a logger no
        // longer banks timber where they stand, they carry it to a building. The spec
        // called this out as the thing that must be re-derived rather than patched —
        // trips per year is what the whole timber economy is built on, and quietly
        // leaving a leg out of it is exactly the D16 mistake.
        // Same basis as the forage budget: the furthest a home is allowed to be, not
        // wherever a spiral happened to put one.
        int fromVillage = MaxHomeToVillageTiles(config);
        var worstHome = new GridPos(fromVillage, 0);

        int worst = worstHome.ManhattanDistanceTo(stand) + stand.ManhattanDistanceTo(shed)
            + shed.ManhattanDistanceTo(worstHome);

        return (worst * config.TravelTicksPerUnit) + config.CutTicks;
    }

    /// <summary>
    /// Cutting trips one worker completes in a year.
    /// </summary>
    /// <remarks>
    /// <b>All four seasons</b>, unlike foraging. Trees do not stop in winter, and that
    /// asymmetry is most of why the job is worth holding.
    /// </remarks>
    public static int CutTripsPerYear(SimConfig config)
    {
        int available = config.TicksPerYear - MealsPerYear(config);
        int trip = CutRoundTripTicks(config);

        return available <= 0 || trip <= 0 ? 0 : available / trip;
    }

    /// <summary>Logs one worker brings home in a year, at their weakest.</summary>
    public static int WoodCutPerYearAtWorst(SimConfig config)
    {
        ArgumentNullException.ThrowIfNull(config);

        int wood = CutTripsPerYear(config) * config.CutYield * config.VigourMinPercent / 100;
        return wood < 1 ? 1 : wood;
    }

    // ---------------------------------------------------------------
    //  Firewood (D29) — the processed half
    // ---------------------------------------------------------------

    /// <summary>Firewood one household burns to get through one winter.</summary>
    /// <remarks>
    /// Per <em>household</em>, not per member: a house costs the same to heat whether
    /// two people live in it or five. That is what makes sprawl the thing that costs,
    /// rather than population — a pressure that traces back to a player decision
    /// (§2.3) instead of merely punishing growth.
    /// </remarks>
    public static int FirewoodPerHouseholdPerWinter(SimConfig config)
    {
        ArgumentNullException.ThrowIfNull(config);

        return config.FirewoodPerWinterDay * config.DaysPerSeason;
    }

    /// <summary>
    /// Firewood a household aims to have stacked — a winter's burn plus a margin.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>The same margin the food store carries</b>
    /// (<see cref="SimConfig.WinterBufferPercent"/>), and for a sharper reason. A
    /// village that only wants woodcutters once its firewood has run low finds out too
    /// late: the shortfall appears in the middle of winter, the labour pass that could
    /// answer it runs once a season, and freezing takes ten days. Measured, the village
    /// reached eleven people and then froze to death in its third decade, every run.
    /// </para>
    /// <para>
    /// Aiming a winter <em>and a bit</em> ahead means the store is refilled in autumn,
    /// while there is still time to do something about it. This is the fuel version of
    /// what the food target already does — and the reason it is stated as a target
    /// rather than tuned is D16.
    /// </para>
    /// </remarks>
    public static int FirewoodStoreWantedPerHousehold(SimConfig config)
    {
        ArgumentNullException.ThrowIfNull(config);

        return FirewoodPerHouseholdPerWinter(config) * config.WinterBufferPercent / 100;
    }

    /// <summary>Ticks for one round trip to the woodcutter's hut and back.</summary>
    public static int FirewoodRoundTripTicks(SimConfig config)
    {
        ArgumentNullException.ThrowIfNull(config);

        var hut = new GridPos(config.WoodcutterHutX, config.WoodcutterHutY);

        // The furthest home the village will build, walking to the hut and back.
        var worstHome = new GridPos(MaxHomeToVillageTiles(config), 0);
        int worst = worstHome.ManhattanDistanceTo(hut);

        return (worst * config.TravelTicksPerUnit * 2) + config.SplitTicks;
    }

    /// <summary>Firewood one woodcutter makes in a year, at their weakest.</summary>
    /// <remarks>Year-round work, like felling — a hut does not care what season it is.</remarks>
    public static int FirewoodMadePerYearAtWorst(SimConfig config)
    {
        ArgumentNullException.ThrowIfNull(config);

        int available = config.TicksPerYear - MealsPerYear(config);
        int trip = FirewoodRoundTripTicks(config);
        int trips = available <= 0 || trip <= 0 ? 0 : available / trip;

        int firewood = trips * config.FirewoodPerSplit * config.VigourMinPercent / 100;
        return firewood < 1 ? 1 : firewood;
    }

    /// <summary>Logs one woodcutter consumes in a year, at their weakest.</summary>
    public static int LogsConsumedPerYearAtWorst(SimConfig config)
    {
        ArgumentNullException.ThrowIfNull(config);

        int perFirewood = config.FirewoodPerSplit < 1 ? 1 : config.FirewoodPerSplit;
        return FirewoodMadePerYearAtWorst(config) * config.LogsPerSplit / perFirewood;
    }

    /// <summary>
    /// Whether a village of <see cref="SimConfig.EconomyHorizonHouseholds"/> homes can
    /// keep itself warm with the hands it has left after feeding itself.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The stated target for fuel, in the same shape as the food target above:
    /// </para>
    /// <para>
    /// <b>After feeding everyone, the hands the village can spare must be enough to
    /// heat every home through winter — and still leave someone to build.</b>
    /// </para>
    /// <para>
    /// Asserted by a test rather than hoped for. Phase 0 rejected warmth outright as
    /// "a second overlapping death system", and the honest reading of that fear is
    /// exactly this arithmetic: if heating the village costs more hands than it has
    /// spare, then cold is not a pressure, it is a slow extinction with extra steps.
    /// </para>
    /// </remarks>
    public static int HandsNeededForFuel(SimConfig config, int households)
    {
        ArgumentNullException.ThrowIfNull(config);

        int firewoodNeeded = households * FirewoodStoreWantedPerHousehold(config);
        int woodcutters = CeilingDivide(firewoodNeeded, FirewoodMadePerYearAtWorst(config));

        int logsNeeded = woodcutters * LogsConsumedPerYearAtWorst(config);
        int loggers = CeilingDivide(logsNeeded, WoodCutPerYearAtWorst(config));

        return woodcutters + loggers;
    }

    /// <summary>
    /// Share of the village's spare hands that heating it is allowed to cost.
    /// </summary>
    /// <remarks>
    /// <b>Half, and the margin is the point</b> — the same argument
    /// <see cref="RequiredDependants"/> makes about the third dependant. Solving for
    /// "fuel costs exactly what the village can spare" produces a settlement that is
    /// warm by construction and has nothing left over: nobody to build with, and no
    /// slack for the winter that runs long. D16 records what that looks like — a
    /// village that survives only while nothing pushes on it, which is useless when
    /// pushing on it is the entire point of §2.3.
    /// </remarks>
    public const int FuelMayCostThisShareOfSpareHands = 2;

    /// <summary>
    /// Hands a village of this many households can spare once everyone is fed.
    /// </summary>
    public static int SpareHandsAt(SimConfig config, int households)
    {
        ArgumentNullException.ThrowIfNull(config);

        int mouths = households * config.MaxHouseholdSize;
        int hands = households * config.AdultsPerHousehold;
        int spare = hands - CeilingDivide(mouths, MouthsFedByOneAdult(config));

        return spare < 0 ? 0 : spare;
    }

    /// <summary>
    /// The smallest <c>firewood_per_split</c> that meets the fuel target.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The fuel counterpart of <see cref="RequiredGatherYield"/>, and it exists for
    /// exactly the same reason: rather than guessing a batch size and finding out
    /// thirty in-game years later that heating the village costs more hands than it
    /// has, ask what the stated target <em>requires</em>.
    /// </para>
    /// <para>
    /// Solved by search rather than algebra because there are two ceilings in the
    /// chain — woodcutters rounded up, then the loggers who feed them rounded up
    /// again — and inverting that in closed form would be clever in the bad way.
    /// A handful of integer iterations at start-up is not worth being clever about.
    /// </para>
    /// </remarks>
    public static int RequiredFirewoodPerSplit(SimConfig config)
    {
        ArgumentNullException.ThrowIfNull(config);

        int households = config.EconomyHorizonHouseholds;
        int budget = FuelBudgetInHands(config, households);

        // A generous ceiling on the search: one batch covering a whole village's
        // winter is certainly enough, and if even that fails the config is broken in
        // a way a bigger number will not fix.
        int ceiling = (households * FirewoodPerHouseholdPerWinter(config)) + 1;

        for (int perSplit = 1; perSplit <= ceiling; perSplit++)
        {
            if (HandsNeededForFuel(config with { FirewoodPerSplit = perSplit }, households) <= budget)
            {
                return perSplit;
            }
        }

        throw new InvalidOperationException(
            $"No batch size lets {households} households heat themselves within the {budget} hands " +
            "budgeted for it. Either fuel is too expensive or the food economy leaves too little over.");
    }

    /// <summary>
    /// Seats a woodcutter's hut needs, for the village the economy is derived for.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Capacity has to be derived too, and forgetting that cost a long run.</b>
    /// Raising the economy horizon re-derived every <em>yield</em> — how much a trip
    /// brings back, how much a batch makes — but left the buildings' capacities at
    /// the hand-picked threes they were given when a village was a dozen people. One
    /// hut holding three woodcutters heats about fifteen households; the village
    /// reached twenty-five, could not physically make more firewood however many
    /// hands were free, and thirty-six people froze.
    /// </para>
    /// <para>
    /// That is a real pressure and one day it should be the player's to answer, by
    /// building a second hut. Until building placement exists there is no answer
    /// available, so the founding capacity has to be large enough for the village the
    /// rest of the economy is budgeted for.
    /// </para>
    /// </remarks>
    public static int RequiredWoodcutterSeats(SimConfig config)
    {
        ArgumentNullException.ThrowIfNull(config);

        int households = config.EconomyHorizonHouseholds;
        int firewood = households * (FirewoodStoreWantedPerHousehold(config)
            + FirewoodPerHouseholdPerWinter(config));

        return CeilingDivide(firewood, FirewoodMadePerYearAtWorst(config));
    }

    /// <summary>Seats a tree stand needs, to keep those huts in logs and homes built.</summary>
    public static int RequiredTreeStandSeats(SimConfig config)
    {
        ArgumentNullException.ThrowIfNull(config);

        int logs = RequiredWoodcutterSeats(config) * LogsConsumedPerYearAtWorst(config);
        int forHuts = CeilingDivide(logs, WoodCutPerYearAtWorst(config));

        // Plus a hand for building, or the village heats itself and never grows.
        return forHuts + 1;
    }

    /// <summary>Hands the village may spend on staying warm — see
    /// <see cref="FuelMayCostThisShareOfSpareHands"/>.</summary>
    public static int FuelBudgetInHands(SimConfig config, int households) =>
        SpareHandsAt(config, households) / FuelMayCostThisShareOfSpareHands;

    private static int CeilingDivide(int numerator, int denominator) =>
        denominator <= 0 ? 0 : (numerator + denominator - 1) / denominator;

    /// <summary>Food one adult gathers in a year at a given vigour.</summary>
    public static int FoodGatheredPerYear(SimConfig config, int vigourPercent) =>
        TripsPerYear(config) * config.GatherYield * vigourPercent / 100;

    /// <summary>Food one adult gathers in a year at their weakest.</summary>
    public static int FoodGatheredPerYearAtWorst(SimConfig config) =>
        FoodGatheredPerYear(config, config.VigourMinPercent);

    /// <summary>Vigour of an ordinary working adult, rather than the weakest one.</summary>
    public static int TypicalVigourPercent(SimConfig config)
    {
        ArgumentNullException.ThrowIfNull(config);

        return (config.VigourMinPercent + 100) / 2;
    }

    /// <summary>
    /// Mouths one ordinary adult can keep fed — themselves, plus dependants.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Not the same question as <see cref="RequiredDependants"/>, and conflating
    /// the two was a real bug that hid for two phases.</b> That constant asks what the
    /// <em>weakest</em> adult must be able to carry, and it is exactly right for
    /// deriving <see cref="RequiredGatherYield"/> — a yield has to work in the worst
    /// case or the worst case kills someone.
    /// </para>
    /// <para>
    /// But the same number was also being used to decide <em>how many hands to put on
    /// food</em>, and there it is badly wrong: it assumes the entire village is
    /// simultaneously at its weakest. It never is. The result was a settlement that
    /// spent every hand it had gathering food it did not need — larders in the
    /// hundreds — while there was nobody left to do anything else. That was invisible
    /// while timber was optional. It became fatal the moment firewood was survival
    /// work: four adults feeding ten mouths had a floor of exactly four, so the fuel
    /// chain could never be staffed at all, and the village froze with full stores.
    /// </para>
    /// <para>
    /// So the labour floor uses an ordinary worker. The worst case has not been
    /// forgotten — it is carried by <see cref="RequiredStockpilePerAdult"/>'s winter
    /// buffer, which is the right place for it: a margin in the <em>store</em> rather
    /// than a permanent tax on the <em>workforce</em>.
    /// </para>
    /// </remarks>
    public static int MouthsFedByOneAdult(SimConfig config)
    {
        ArgumentNullException.ThrowIfNull(config);

        int surplus = FoodGatheredPerYear(config, TypicalVigourPercent(config)) - AdultFoodPerYear(config);
        int childFood = ChildFoodPerYear(config);

        int dependants = surplus <= 0 || childFood <= 0 ? 0 : surplus / childFood;

        // Never below the weakest-case target: the floor may be optimistic about
        // vigour, but it must never claim an adult feeds fewer mouths than the yield
        // was derived to guarantee.
        int mouths = 1 + dependants;
        int guaranteed = 1 + RequiredDependants;

        return mouths < guaranteed ? guaranteed : mouths;
    }

    /// <summary>
    /// Children a weakest-case adult can support after feeding themselves.
    /// </summary>
    /// <remarks>
    /// This is the number the whole economy is judged on. Must be at least
    /// <see cref="RequiredDependants"/>.
    /// </remarks>
    public static int DependantsSupportedAtWorst(SimConfig config)
    {
        int surplus = FoodGatheredPerYearAtWorst(config) - AdultFoodPerYear(config);
        if (surplus <= 0)
        {
            return 0;
        }

        return surplus / ChildFoodPerYear(config);
    }

    /// <summary>
    /// The smallest <c>gather_yield</c> that meets the target.
    /// </summary>
    /// <remarks>
    /// This is the point of the whole class: rather than guessing a yield and
    /// watching the village die, ask what yield the stated target <em>requires</em>.
    /// </remarks>
    public static int RequiredGatherYield(SimConfig config)
    {
        ArgumentNullException.ThrowIfNull(config);

        int needed = AdultFoodPerYear(config) + (RequiredDependants * ChildFoodPerYear(config));
        int trips = TripsPerYear(config);

        if (trips <= 0 || config.VigourMinPercent <= 0)
        {
            throw new InvalidOperationException(
                "Config allows no foraging at all; no gather yield can sustain a village.");
        }

        // yield * trips * vigour/100 >= needed, solved for yield and rounded up.
        int denominator = trips * config.VigourMinPercent;
        return ((needed * 100) + denominator - 1) / denominator;
    }

    /// <summary>
    /// Food a household should keep to survive winter, per member.
    /// </summary>
    /// <remarks>
    /// Winter is a quarter of the year with no foraging, so the store has to carry
    /// everyone through it — plus a margin for the shock that actually kills
    /// households, which is a worker dying or ageing out mid-winter.
    /// </remarks>
    public static int RequiredStockpilePerAdult(SimConfig config)
    {
        int winterFood = AdultFoodPerYear(config) / 4;
        return winterFood * config.WinterBufferPercent / 100;
    }

    // ---------------------------------------------------------------
    //  Storage capacity (D30/D32, spec slice 5)
    // ---------------------------------------------------------------

    /// <summary>
    /// How much food one granary holds.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>The stated target: a granary holds a full winter's store for the village it
    /// is built to feed</b> — <see cref="RequiredStockpilePerAdult"/> per head, for
    /// <see cref="SimConfig.GranaryFeedsPeople"/> heads. Derived, per D16, so that
    /// changing hunger or the winter margin moves the building with them instead of
    /// quietly invalidating it.
    /// </para>
    /// <para>
    /// <b>How many people a granary is built for is content, not economy.</b> It is a
    /// fact about the building — the same kind of number as how many hands fit at a
    /// berry patch — so it lives in the config where a modder can change it. What must
    /// not be typed in is the *consequence*, which is
    /// <see cref="PopulationCeiling"/>.
    /// </para>
    /// </remarks>
    public static int GranaryCapacity(SimConfig config)
    {
        ArgumentNullException.ThrowIfNull(config);

        return RequiredStockpilePerAdult(config) * config.GranaryFeedsPeople;
    }

    /// <summary>
    /// The population at which the granary stops the village growing.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>This is the number slice 5 exists to create, and it is a consequence rather
    /// than a setting.</b> Births are gated on the granary holding
    /// <see cref="SimConfig.BirthFoodPercent"/> of <c>stockpile_target × population</c>
    /// — a demand that grows with the village and has, until now, been unbounded. Give
    /// the granary a ceiling and the demand meets it at a fixed population, so growth
    /// stops at <em>what the buildings support</em> rather than overshooting them and
    /// falling back (spec §12).
    /// </para>
    /// <para>
    /// Note it comes out <em>above</em> <see cref="SimConfig.GranaryFeedsPeople"/>, by
    /// exactly the slack in the birth gate: a village will keep having children until
    /// its store is 80% of what everyone alive would want, which is a larger village
    /// than the granary comfortably feeds. That is the intended reading — a granary
    /// built for thirty supports a village that runs a little hungrier than thirty.
    /// </para>
    /// </remarks>
    public static int PopulationCeiling(SimConfig config)
    {
        ArgumentNullException.ThrowIfNull(config);

        int perHead = RequiredStockpilePerAdult(config) * config.BirthFoodPercent / 100;
        return perHead <= 0 ? int.MaxValue : GranaryCapacity(config) / perHead;
    }

    /// <summary>
    /// How much one storage shed holds, across logs and firewood together.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>The stated target: a shed holds the village's winter fuel, and the logs
    /// waiting to become it.</b> Sized for
    /// <see cref="SimConfig.EconomyHorizonHouseholds"/> — the same village the rest of
    /// the economy is budgeted for — because a shed too small to hold a winter's
    /// firewood does not create pressure, it freezes people.
    /// </para>
    /// <para>
    /// Deliberately more generous than the granary, and the asymmetry is the design.
    /// Food is what regulates the village (births are gated on it), so the granary is
    /// where a ceiling <em>should</em> bind. The shed binding as well would mean two
    /// constraints fighting for the same job, and the player could not tell which one
    /// was stopping them — which is non-negotiable 1 failing.
    /// </para>
    /// </remarks>
    public static int ShedCapacity(SimConfig config)
    {
        ArgumentNullException.ThrowIfNull(config);

        int households = config.EconomyHorizonHouseholds;
        int firewood = households * FirewoodStoreWantedPerHousehold(config);

        // Plus the logs to make that firewood, and enough to raise a house without
        // having to empty the woodpile first.
        int logs = (households * FirewoodStoreWantedPerHousehold(config) * config.LogsPerSplit
            / (config.FirewoodPerSplit < 1 ? 1 : config.FirewoodPerSplit)) + config.LogsPerHouse;

        return firewood + logs;
    }

    /// <summary>
    /// How much the market holds, across food and firewood together.
    /// </summary>
    /// <remarks>
    /// <b>Stated target: enough that a household's errand is usually satisfied at the
    /// market rather than at the granary</b>, and no more. Scaled by the village the
    /// economy is budgeted for, so a growing settlement does not quietly turn its
    /// market into the real store — which would re-centralise everything D30 just
    /// spread out, and put the walking back.
    /// </remarks>
    public static int MarketCapacity(SimConfig config)
    {
        ArgumentNullException.ThrowIfNull(config);

        return config.MarketStockPerHousehold * config.EconomyHorizonHouseholds;
    }

    /// <summary>A one-line summary for logs and tests.</summary>
    public static string Describe(SimConfig config) =>
        $"adult eats {AdultFoodPerYear(config)}/yr, child {ChildFoodPerYear(config)}/yr; " +
        $"{TripsPerYear(config)} trips/yr yields {FoodGatheredPerYearAtWorst(config)}/yr at worst " +
        $"({config.VigourMinPercent}% vigour) => supports {DependantsSupportedAtWorst(config)} dependants " +
        $"(target {RequiredDependants}); required yield {RequiredGatherYield(config)}, configured {config.GatherYield}.";
}

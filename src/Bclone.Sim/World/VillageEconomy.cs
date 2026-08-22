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

    // `NearestForageDistance` is deleted with the sites it measured (slice 5). It answered
    // *"how far is a home from the nearest berry patch, in the canonical jitter-free
    // valley?"*, which kept one economy across every seed — and it had **no callers left**
    // even before this slice, because `MaxHomeToWorkTiles` stopped being derived from a ring
    // of patches and became the gatherer hut's own ring. Deleted rather than kept against
    // some future use (D98).

    // ---------------------------------------------------------------
    //  What placement guarantees, and what the economy is derived from
    // ---------------------------------------------------------------

    /// <summary>
    /// The walk to work the economy budgets for — <b>the gatherer's own ring</b>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>⭐ IT IS A BUDGET NOW, NOT A FENCE</b> (`forests-and-gathering.md §3.2`). It used to
    /// be a guarantee: <see cref="Household.ChooseSite"/> refused to build beyond it, so the
    /// derivation stood on something the village could not break. Catchment is gone and the
    /// refusal with it — building further out is allowed, warned about, and genuinely costs
    /// food, because the villager really does walk further and really does make fewer trips.
    /// <b>That is D58's settled mechanism: distance stops being a restriction and becomes a
    /// consequence.</b>
    /// </para>
    /// <para>
    /// <b>⚠️ It could not simply be deleted, and that is why this number still exists.</b>
    /// <see cref="RequiredGatherYield"/> solves <em>yield = need ÷ (trips × vigour)</em>
    /// against the worst walk; widen that walk to the map diagonal and
    /// <see cref="TripsPerYear"/> rounds to zero, at which point the economy has **no
    /// solution at all** (`DESIGN.md §5`'s recorded finding). An anchor is required. The only
    /// question was which.
    /// </para>
    /// <para>
    /// <b>The anchor is <c>gatherer_hut_ring_tiles</c>, and three things recommend it.</b> It
    /// is a number the player can <em>see on the map</em> — the ring the hut draws is also the
    /// distance the economy assumes people live within, where the old 7 was an artefact of
    /// where a generator happened to drop a berry patch and no player could ever have learned
    /// it. The stated target barely changes: <em>one gatherer at a <b>fully wooded</b> hut, at
    /// minimum vigour, feeds themselves and their dependants</em> — only the two bold words
    /// are new. And the ring was chosen as 8 against the old 7 precisely so this re-derivation
    /// would be an adjustment rather than a rewrite.
    /// </para>
    /// </remarks>
    public static int MaxHomeToWorkTiles(SimConfig config)
    {
        ArgumentNullException.ThrowIfNull(config);

        return config.GathererHutRingTiles;
    }

    /// <summary>How far a home may sit from the middle of the village.</summary>
    /// <remarks>
    /// <para>
    /// The other half of the bound. Without it a household could chase a distant site
    /// out to the valley's edge and then spend its life walking back to the granary —
    /// which is the same mistake in the other direction, and D32 says the interesting
    /// inequality is distance to the store.
    /// </para>
    /// <para>
    /// <b>The same number as <see cref="MaxHomeToWorkTiles"/>, and it delegates rather
    /// than repeating the sum.</b> Two questions, one answer: a home that is within reach
    /// of the ring is within reach of the middle of it. They were two identical
    /// copy-pasted bodies, which is a formula waiting to be corrected in one place and
    /// not the other — and <see cref="Household.ChooseSite"/> applies both as if they
    /// were independent, so a divergence would have been silent.
    /// </para>
    /// </remarks>
    public static int MaxHomeToVillageTiles(SimConfig config) => MaxHomeToWorkTiles(config);

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

        // ⭐ WHERE THE TIMBER IS, NOW THAT NOBODY DROPS A STAND ON THE MAP (slice 5). This
        // used to be the canonical stand's furthest slot plus worst-case jitter — a position
        // the generator chose. A forester works **their own hut's painted ground** now, so
        // the walk is the same walk everything else budgets for, and saying so here finally
        // makes true the sentence this method has always carried: *same basis as the forage
        // budget, so neither kind of work is quietly cheaper.*
        var stand = new GridPos(MaxHomeToWorkTiles(config), 0);

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

        // Divided by the burn interval, so the budget follows the burn. Leaving this as
        // "per day" while the hearth burned every fourth day would have had the village
        // stocking four winters' fuel and calling it one — the exact doc-versus-reality
        // drift D48, D49 and D50 were each an instance of.
        //
        // Rounded UP, deliberately: a winter that needs seven and a half burns needs eight
        // logs, and a village that budgets seven is cold on the last day of it.
        return CeilingDivide(
            config.FirewoodPerWinterDay * config.DaysPerSeason,
            config.FirewoodBurnIntervalDays < 1 ? 1 : config.FirewoodBurnIntervalDays);
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
    /// answer it runs once a season, and a house with no fire in it kills in
    /// twenty-five days (D45). Measured, the village
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
        int foresters = CeilingDivide(logsNeeded, WoodCutPerYearAtWorst(config));

        return woodcutters + foresters;
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
    /// How many souls a house of this kind will hold.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>⭐ THE ONE PLACE THAT ANSWERS "HOW MANY FIT IN THIS HOUSE" (Joe, D153).</b> The cap
    /// is what limits how many children a couple can have, and Joe wants it to be a property of
    /// the <em>house</em> rather than a global number — <i>"eventually an unlock/tech that
    /// allows for larger homes/denser population."</i> This is the seam that unlock lands on: a
    /// second arm, beside a `BuildingKind` appended to the enum, and every reader already asks
    /// the right question.
    /// </para>
    /// <para>
    /// <b>⚠️ NO PER-HOUSE STATE YET, AND THAT IS DELIBERATE.</b> A house is not an entity in
    /// this sim — it is a <c>GridPos?</c> on <see cref="Household"/>, with no id and no record
    /// (<c>SimWorld.NameFor</c>: *"A HOUSE IS NOT NUMBERED"*). Recording a kind on the household
    /// today would be a field that can only ever hold one value, which is D98's rule —
    /// <c>construction_site_capacity</c> was <em>deleted rather than zeroed</em> on the grounds
    /// that *"a number which is always zero is a lie waiting to be found."* The field arrives
    /// with the second dwelling, when it has two values on its first day. It must then travel
    /// with <c>HomePosition</c> through <c>HouseholdSystem</c>'s empty-house swap, which moves a
    /// family into a standing empty home — that is the one place this design can go wrong.
    /// </para>
    /// <para>
    /// <b>Content, not derivation, and D16 does not bite.</b> How many people fit under a roof
    /// is a fact about the building — the same class as <c>work_ground_tiles_per_worker</c> —
    /// so it lives in the config where a modder can change it. What must stay derived is the
    /// <em>consequence</em>, which is <see cref="PopulationCeiling"/>.
    /// </para>
    /// </remarks>
    public static int HouseholdCapacity(BuildingKind kind, SimConfig config)
    {
        ArgumentNullException.ThrowIfNull(config);

        return kind switch
        {
            BuildingKind.Home => config.MaxHouseholdSize,

            // Named rather than defaulted, which is D108's rule: five of six silent default
            // arms would have mis-priced or mis-named a new building kind, so every arm says
            // what it means and a new one has to be added on purpose.
            _ => throw new ArgumentOutOfRangeException(
                nameof(kind), kind, "That kind of building is not somewhere anybody lives."),
        };
    }

    /// <summary>
    /// Hands a village of this many households can spare once everyone is fed.
    /// </summary>
    /// <remarks>
    /// <b>⚠️ Reads <c>MaxHouseholdSize</c> and not <see cref="HouseholdCapacity"/>, on purpose.</b>
    /// This is a *budgeting worst case* — assume every household is full — feeding the derived
    /// fuel target. It wants "the biggest a household can get", which stays a village-wide fact
    /// even once individual houses differ.
    /// </remarks>
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
    /// chain — woodcutters rounded up, then the foresters who feed them rounded up
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
    /// That pressure is the player's to answer now, by building a second hut (D43). The
    /// founding capacity still has to be large enough for the village the rest of the
    /// economy is budgeted for, so that a player who builds nothing is not quietly
    /// punished for it — D50 is what happens when it is not.
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

    /// <summary>
    /// Seats a builder's hut has — the hands the village can put on building (D108).
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Stated target: a hut holds every hand the village could spare for building once
    /// it has fed and heated itself.</b> That is not a new quantity — it is the two figures
    /// the economy already budgets against, subtracted: <see cref="SpareHandsAt"/> is what
    /// is left after everybody eats, and <see cref="HandsNeededForFuel"/> is what keeping
    /// them warm costs. What remains is what building has ever been funded from, and the
    /// hut's job is to be big enough to hold it.
    /// </para>
    /// <para>
    /// <b>Derived rather than typed, and <c>woodcutter_hut_capacity</c> is why</b> (D16, D50).
    /// That one was a hand-picked three from when a village was a dozen people; the yields
    /// were re-derived when the horizon moved and the capacities were not, the village
    /// physically could not make enough firewood however many hands were free, and
    /// thirty-six people froze. A capacity is a consequence of the economy, so it is
    /// computed from it.
    /// </para>
    /// <para>
    /// <b>Never below one.</b> A hut with no seat in it is a building that can never do the
    /// one thing it exists for, and — since D108 makes the hut the only path to any other
    /// building — a village that could never build anything again.
    /// </para>
    /// </remarks>
    public static int BuilderHutCapacity(SimConfig config)
    {
        ArgumentNullException.ThrowIfNull(config);

        int households = config.EconomyHorizonHouseholds;
        int afterFuel = SpareHandsAt(config, households) - HandsNeededForFuel(config, households);

        return afterFuel < 1 ? 1 : afterFuel;
    }

    /// <summary>Tiles in a diamond of this radius — the shape every ring in this game is.</summary>
    public static int TilesInRing(int radius) =>
        radius < 0 ? 0 : (2 * radius * radius) + (2 * radius) + 1;

    /// <summary>
    /// Seats a gatherer's hut has — <b>its ring, priced in workers</b>
    /// (`forests-and-gathering.md`).
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>⭐ D86's rule reused rather than a new one invented.</b> Work ground is already priced
    /// in workers — <see cref="SimConfig.WorkGroundTilesPerWorker"/>, *"how much land one person
    /// can look after"*, which D86 called the first limit in this game that is not distance. A
    /// hut's ring is ground it keeps, so it is priced the same way. **One stated rule serving
    /// two buildings beats two numbers that can drift apart.**
    /// </para>
    /// <para>
    /// <b>Derived, because <c>woodcutter_hut_capacity</c> is the recorded case</b> (D16, D50):
    /// yields were re-derived when the economy horizon moved and capacities were not, the
    /// village could not physically make enough firewood however many hands were free, and
    /// thirty-six people froze. A capacity is a consequence.
    /// </para>
    /// <para>
    /// <b>Never below one</b>, on the same reasoning as the builder's hut: a building that can
    /// never do the one thing it exists for is not a building.
    /// </para>
    /// </remarks>
    public static int GathererHutCapacity(SimConfig config)
    {
        ArgumentNullException.ThrowIfNull(config);

        int seats = CeilingDivide(
            TilesInRing(config.GathererHutRingTiles), config.WorkGroundTilesPerWorker);

        return seats < 1 ? 1 : seats;
    }

    /// <summary>Ticks a forester spends putting one tree back.</summary>
    /// <remarks>
    /// Felling, times the stated multiple. One number rather than a second tick count that
    /// could drift away from <see cref="SimConfig.CutTicks"/> — planting is defined as *harder
    /// than felling*, so it is written as harder than felling.
    /// </remarks>
    public static int PlantTicks(SimConfig config)
    {
        ArgumentNullException.ThrowIfNull(config);

        int ticks = config.CutTicks * config.PlantingCostsThisMuchMoreThanFelling;
        return ticks < 1 ? 1 : ticks;
    }

    /// <summary>
    /// Years for one forester to re-wood the ground they keep — <b>the consequence, derived</b>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>This is the number `building-placement.md §12.8` asks for</b>, restated for a world
    /// where planting is not gated: *a valley cleared by a village should take about a
    /// generation to come back.* The config states how much harder planting is than felling;
    /// this says what that means in years, so the design target is checkable rather than hoped
    /// for (D16).
    /// </para>
    /// <para>
    /// One forester, the ground one pair of hands keeps
    /// (<see cref="SimConfig.WorkGroundTilesPerWorker"/>), and the same gatherable-year budget
    /// <see cref="TripsPerYear"/> uses — so felling, gathering and planting are all costed on
    /// one basis rather than one of them being quietly cheaper.
    /// </para>
    /// </remarks>
    public static int YearsToRewoodOnesGround(SimConfig config)
    {
        ArgumentNullException.ThrowIfNull(config);

        // The same round trip felling gets, with planting's cost in place of cutting's.
        int travel = MaxHomeToWorkTiles(config) * config.TravelTicksPerUnit;
        int perTree = (travel * 2) + PlantTicks(config);

        int available = (config.TicksPerYear * 3 / 4) - (MealsPerYear(config) * 3 / 4);
        int treesPerYear = available <= 0 ? 0 : available / perTree;

        return treesPerYear <= 0
            ? int.MaxValue
            : CeilingDivide(config.WorkGroundTilesPerWorker, treesPerYear);
    }

    /// <summary>Seats a forester's hut needs, to keep the woodcutters in logs and homes built.</summary>
    /// <remarks><b><c>RequiredTreeStandSeats</c> until D159</b> — the stands are gone and this
    /// has sized the forester's hut since step C.</remarks>
    public static int RequiredForesterSeats(SimConfig config)
    {
        ArgumentNullException.ThrowIfNull(config);

        int logs = RequiredWoodcutterSeats(config) * LogsConsumedPerYearAtWorst(config);
        int forHuts = CeilingDivide(logs, WoodCutPerYearAtWorst(config));

        // Plus a hand for building, or the village heats itself and never grows.
        return forHuts + 1;
    }

    // ---------------------------------------------------------------
    //  The farm (`specs/crops-and-orchards.md`, D161)
    // ---------------------------------------------------------------

    /// <summary>Ticks in one season that are actually available for work.</summary>
    /// <remarks>
    /// <b>A season, not a year, and that is the mechanic rather than a unit choice.</b> Sowing
    /// happens in spring and only spring (<see cref="SeasonRules.IsSowing"/>) and reaping in
    /// autumn and only autumn — a missed sowing is a missed year — so the budget that decides
    /// how much ground a farm can really keep is a quarter of the calendar, less the meals
    /// eaten in it and the one walk out from home.
    /// </remarks>
    public static int FieldSeasonTicks(SimConfig config)
    {
        ArgumentNullException.ThrowIfNull(config);

        int commute = MaxHomeToWorkTiles(config) * config.TravelTicksPerUnit * 2;
        int available = (config.TicksPerYear / 4) - (MealsPerYear(config) / 4) - commute;

        return available < 0 ? 0 : available;
    }

    /// <summary>
    /// Ticks one tile costs in a field of the given radius — <b>the work plus the walk it
    /// actually makes</b>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Sowing carries nothing</b>, so a farmer works along the rows and pays one step between
    /// tiles. <b>Reaping carries an armful</b> to the steading and comes back, so it pays the
    /// field's own radius twice.
    /// </para>
    /// <para>
    /// <b>⛔⭐ THIS MODEL WAS ACCUSED OF BEING WRONG AND WAS NOT — THE CODE WAS.</b> Measured, a
    /// farmer reaped 5.3 tiles an autumn against the 13 budgeted here, and reaping saturated at
    /// 5–6 whatever the size of the field. That looked exactly like a budget charging a
    /// two-tile hop for a cross-village walk, and it was rewritten to charge
    /// <see cref="MaxHomeToWorkTiles"/> — which fitted the measurement and produced a four-tile
    /// field and a yield of 216 from one tile.
    /// </para>
    /// <para>
    /// <b>The real cause was one word in <c>BehaviorSystem.HaulTheHarvest</c>.</b> It asked the
    /// farm's store <c>IsFull</c> rather than whether it had room for the load, so a buffer with
    /// one unit of space took one unit and the farmer carried the rest on to the granary —
    /// <b>two long walks per tile</b>. Fix that and a farmer reaps 13 a year, which is what this
    /// model said all along. <b>A measurement that disagrees with a derivation has found a bug
    /// in one of them, and it is worth knowing which before rewriting the other.</b>
    /// </para>
    /// </remarks>
    public static int FieldTileTicks(SimConfig config, int workTicks, int radius, bool carrying)
    {
        ArgumentNullException.ThrowIfNull(config);

        int walk = carrying
            ? radius * config.TravelTicksPerUnit * 2
            : config.TravelTicksPerUnit;

        int cost = (workTicks < 1 ? 1 : workTicks) + walk;
        return cost < 1 ? 1 : cost;
    }

    /// <summary>
    /// Tiles one farmer keeps — <b>the biggest field they can bring in over one autumn</b>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>⭐ STATED AS A SHAPE, WHICH IS WHAT MAKES IT DERIVABLE AND WHAT MAKES IT LEGIBLE.</b>
    /// A field is a diamond around its steading — <see cref="TilesInRing"/>, the shape every
    /// ring in this game is — and this asks the only question that has a right answer:
    /// <em>how big a diamond can one pair of hands reap in one autumn, walking each armful back
    /// to the steading?</em> Grow the radius and you gain tiles faster than you lose time, until
    /// the walk overtakes you; the last radius that still fits is the answer.
    /// </para>
    /// <para>
    /// <b>Autumn binds, not spring</b>, and that is the mechanic rather than a convenience: a
    /// tile sown and not reaped is worth nothing at all, because winter takes what is left
    /// standing (Joe — <em>use it or lose it</em>). Deriving against the cheaper season would
    /// have the village promise itself a harvest the autumn cannot physically bring in.
    /// </para>
    /// <para>
    /// <b>⭐ AND IT IS CHECKED AGAINST THE GAME, NOT ONLY AGAINST ITSELF</b>
    /// (<c>FarmTests.AFarmerCanActuallyReapTheFieldTheDerivationGivesThem</c>). That guard did
    /// not exist when this was first written, which is why a code bug spent a slice being
    /// mistaken for an arithmetic one — every other guard asked whether the sums were
    /// self-consistent rather than whether they described the village.
    /// </para>
    /// </remarks>
    public static int FieldTilesOneFarmerKeeps(SimConfig config)
    {
        ArgumentNullException.ThrowIfNull(config);

        int budget = FieldSeasonTicks(config);
        int best = 0;

        // Bounded rather than open-ended: the walk grows with the radius and the tiles with its
        // square, so this always terminates long before the bound — which is there so that a
        // config with a pathological travel cost fails as a zero rather than as a hang.
        for (int radius = 1; radius <= MaxHomeToWorkTiles(config); radius++)
        {
            int tiles = TilesInRing(radius);
            int reaping = tiles * FieldTileTicks(config, config.ReapTicks, radius, carrying: true);
            int sowing = tiles * FieldTileTicks(config, config.SowTicks, radius, carrying: false);

            if (reaping > budget || sowing > budget)
            {
                break;
            }

            best = tiles;
        }

        return best;
    }


    /// <summary>Food one farmer brings in over a year, at their weakest.</summary>
    /// <remarks>
    /// <b>At their weakest, like every other yield in this file</b> — a farm sized against a
    /// villager in their prime is a farm that stops feeding the village as its founders age,
    /// which is D12's whole point arriving as an economy bug.
    /// </remarks>
    public static int FoodFarmedPerYearAtWorst(SimConfig config)
    {
        ArgumentNullException.ThrowIfNull(config);

        int food = FieldTilesOneFarmerKeeps(config) * config.CropYieldPerTile
            * config.VigourMinPercent / 100;

        return food < 1 ? 1 : food;
    }

    /// <summary>
    /// What <see cref="SimConfig.CropYieldPerTile"/> has to be for a farmer's year to be worth
    /// as much as a gatherer's.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>⭐ THE TARGET IS A COMPARISON, AND THAT IS WHAT MAKES IT DERIVABLE AT ALL.</b>
    /// `crops-and-orchards.md §1` inherits `environment-and-seasons.md §5.1`'s surviving
    /// target — <em>a household working normally through spring, summer and autumn fills its
    /// winter store by the first day of winter</em> — and the village already has one kind of
    /// work that meets it: gathering, whose yield <see cref="RequiredGatherYield"/> derives
    /// against exactly that sentence. <b>So a farmer's year must be worth a gatherer's year</b>,
    /// and the target is inherited rather than restated.
    /// </para>
    /// <para>
    /// <b>⚠️ AND STATING IT ANY OTHER WAY IS CIRCULAR, which cost the first draft of this
    /// method.</b> The obvious form — *"enough yield that a farm's seats feed a household"* —
    /// reads the seats, and the seats are derived from the yield. It produced a farmhouse with
    /// fourteen seats in it and no way to tell whether the yield or the capacity was the thing
    /// that was wrong. Two numbers that define each other are not a derivation; they are a
    /// fixed point nobody chose.
    /// </para>
    /// <para>
    /// <b>Why it is the right comparison and not merely a convenient one (D19):</b> a second
    /// raw food source exists so a distant household has <em>something</em> nearby to work. A
    /// farm worth materially less than a gatherer's hut is a building nobody rationally places;
    /// one worth materially more deletes gathering. Parity is what makes the choice about
    /// <em>where the ground is</em>, which is the decision the slice is for.
    /// </para>
    /// </remarks>
    public static int RequiredCropYield(SimConfig config)
    {
        ArgumentNullException.ThrowIfNull(config);

        int tiles = FieldTilesOneFarmerKeeps(config);
        if (tiles <= 0)
        {
            throw new InvalidOperationException(
                "A farmer cannot work a single tile in a season — sow_ticks, reap_ticks or the "
                + "walk to work make the crop impossible.");
        }

        // An ordinary adult on both sides, not the weakest one: the two kinds of work are
        // compared at the same vigour, so the answer is about the work rather than about who
        // happens to be doing it. Vigour then scales both identically at the point of use.
        int gathered = FoodGatheredPerYear(config, TypicalVigourPercent(config));
        int perTile = CeilingDivide(gathered * 100, tiles * TypicalVigourPercent(config));

        return perTile < 1 ? 1 : perTile;
    }

    /// <summary>
    /// Seats a farmhouse has — <b>two, and it is content rather than a derivation</b> (Joe).
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>⚠️ IT WAS DERIVED AND CAME OUT AT ONE, AND ONE WAS THE WRONG ANSWER TO A QUESTION
    /// ARITHMETIC CANNOT SETTLE.</b> The first version asked <em>how many hands does it take to
    /// keep one household in food?</em> — <c>ceil(max_household_size / MouthsFedByOneAdult)</c>
    /// — and got <c>ceil(5/6) = 1</c>. That is a defensible number and a bad building: a
    /// workplace with a single seat is not somewhere people work, it is somewhere a person
    /// works, and it reads on the panel as broken rather than as small.
    /// </para>
    /// <para>
    /// <b>⭐ So it moves to the same class as <c>work_ground_tiles_per_worker</c> and
    /// <c>granary_feeds_people</c>: a stated fact about the world, with the consequence
    /// derived</b> (D16's real split — state the fact, derive the outcome). How many people fit
    /// in a steading is not something <see cref="MouthsFedByOneAdult"/> knows; what two pairs of
    /// hands then produce is, and that is <see cref="FoodFarmedPerYearAtWorst"/> times the
    /// seats. **A farm keeps about two households fed, and the village-wide answer to wanting
    /// more is to build another one** — <c>granary_feeds_people</c>'s pattern (D39).
    /// </para>
    /// <para>
    /// <b>It reads <c>farmhouse_seats</c> rather than returning a literal</b>, so a modder can
    /// touch it and so the number lives with the rest of the content (DESIGN.md §3). Floored at
    /// one for the same reason as the builder's and gatherer's huts: a building that can never
    /// do the one thing it exists for is not a building.
    /// </para>
    /// </remarks>
    public static int RequiredFarmerSeats(SimConfig config)
    {
        ArgumentNullException.ThrowIfNull(config);

        return config.FarmhouseSeats < 1 ? 1 : config.FarmhouseSeats;
    }


    /// <summary>Hands the village may spend on staying warm — see
    /// <see cref="FuelMayCostThisShareOfSpareHands"/>.</summary>
    public static int FuelBudgetInHands(SimConfig config, int households) =>
        SpareHandsAt(config, households) / FuelMayCostThisShareOfSpareHands;

    /// <summary>
    /// Integer division rounding up. No floats anywhere near this (D2).
    /// </summary>
    /// <remarks>
    /// Internal rather than private because <see cref="LabourQuota"/> had an identical
    /// private copy. One arithmetic rule, one place — a rounding rule that exists twice
    /// is one that can be corrected once.
    /// </remarks>
    internal static int CeilingDivide(int numerator, int denominator) =>
        denominator <= 0 ? 0 : (numerator + denominator - 1) / denominator;

    /// <summary>
    /// How wooded the economy assumes a working hut's ring actually is.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>⭐ THE ECONOMY USED TO BE DERIVED FOR A VALLEY THAT DOES NOT EXIST</b> (Joe's call,
    /// 2026-08-11). `forests-and-gathering.md §3.2` states the target as *"one gatherer at a
    /// <b>fully wooded</b> hut feeds themselves and their dependants"*, and flagged those two
    /// words as the only new ones. This is what they cost: **no real hut is ever fully
    /// wooded.** Measured on the warm start, a well-sited hut yields **29 of a possible 51**
    /// at founding and **19 by year twenty**, because the village clears its own ring as it
    /// builds. The village starved at year thirty with 2,627 of the valley's 2,662 trees
    /// still standing — the derivation was simply asking for food that no hut could produce.
    /// </para>
    /// <para>
    /// <b>Anchored on <c>forest_coverage_percent</c>, which is a number that already exists
    /// and is already stated.</b> The target becomes *one gatherer at a hut whose ring is as
    /// wooded as the valley is* — true of an averagely-sited hut, and **conservative for a
    /// well-sited one**, which is the right direction: siting a hut in thick wood should be
    /// rewarded with slack, not required to break even. That is §0.1's *challenge in the
    /// planning* — the decision pays off — rather than a tax for making it badly.
    /// </para>
    /// <para>
    /// ⚠️ <b>It does not soften "no forest, no food".</b> A bald ring still yields nothing;
    /// what changed is what the village <em>budgets</em> for, not what a trip is worth.
    /// </para>
    /// </remarks>
    public static int WorkingRingWoodedPercent(SimConfig config)
    {
        ArgumentNullException.ThrowIfNull(config);

        // Clamped above zero: a valley configured with no woodland at all cannot support a
        // food economy, and the throw for that belongs in `RequiredGatherYield` where the
        // impossibility is stated, not in a division here.
        return config.ForestCoveragePercent < 1 ? 1 : config.ForestCoveragePercent;
    }

    /// <summary>Food one adult gathers in a year at a given vigour.</summary>
    /// <remarks>
    /// Through <see cref="WorkingRingWoodedPercent"/>, so what the economy believes a trip is
    /// worth and what <c>SimWorld.GatherYieldAt</c> actually hands over cannot drift apart.
    /// </remarks>
    public static int FoodGatheredPerYear(SimConfig config, int vigourPercent) =>
        TripsPerYear(config) * config.GatherYield * WorkingRingWoodedPercent(config) / 100
            * vigourPercent / 100;

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

        // yield * wooded/100 * trips * vigour/100 >= needed, solved for yield, rounded up.
        //
        // The wooded fraction is the new term and it is why this number roughly trebles: the
        // village is budgeting for the hut it will actually have rather than for a
        // hypothetical one standing in unbroken forest.
        int wooded = WorkingRingWoodedPercent(config);
        int denominator = trips * config.VigourMinPercent * wooded;
        return ((needed * 100 * 100) + denominator - 1) / denominator;
    }

    /// <summary>
    /// What one person must eat to get through the season nothing can be gathered in.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>The bare ration, deliberately without the winter buffer</b> — this is the
    /// <em>hunger</em> line, where <see cref="RequiredStockpilePerAdult"/> is the
    /// <em>stocking</em> target (D73). The two were conflated in
    /// <c>LabourQuota.VillageIsShortOfFood</c>, which asked "do we have everything we
    /// would like?" and called the answer <em>short of food</em>. In an established
    /// village that is nearly always false and nobody noticed; in a cold start it is true
    /// from the first week and never stops, so nothing was ever built and the founders
    /// froze.
    /// </para>
    /// <para>
    /// Falling below the buffer is a village that should work harder at food. Falling
    /// below this is a village that should drop everything, and only the second is what
    /// that question was ever asked for.
    /// </para>
    /// </remarks>
    public static int WinterRationPerHead(SimConfig config)
    {
        ArgumentNullException.ThrowIfNull(config);

        return AdultFoodPerYear(config) / 4;
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
    /// <para>
    /// <b>Per granary.</b> For the ceiling an actual village lives under, ask
    /// <see cref="CeilingForCapacity"/> with the capacity it has actually built — that is
    /// the number placement is about, and the reason the singleton seam had to be closed
    /// before a player could build a second one (D38).
    /// </para>
    /// </remarks>
    public static int PopulationCeiling(SimConfig config)
    {
        ArgumentNullException.ThrowIfNull(config);

        int perHead = PerHeadDemand(config);
        return perHead <= 0 ? int.MaxValue : GranaryCapacity(config) / perHead;
    }

    /// <summary>
    /// The population a given amount of granary supports — the number placement moves.
    /// </summary>
    /// <remarks>
    /// Takes total capacity rather than a count of buildings, so a larger granary
    /// unlocked through the tech tree raises the ceiling the same way a second ordinary
    /// one does (D39: the winter buffer is priced, not capped).
    /// </remarks>
    public static int CeilingForCapacity(SimConfig config, int totalGranaryCapacity)
    {
        ArgumentNullException.ThrowIfNull(config);

        int perHead = PerHeadDemand(config);
        return perHead <= 0 ? int.MaxValue : totalGranaryCapacity / perHead;
    }

    /// <summary>Food the birth gate demands per living villager.</summary>
    private static int PerHeadDemand(SimConfig config) =>
        RequiredStockpilePerAdult(config) * config.BirthFoodPercent / 100;

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

        // ⭐ AND NEVER LESS THAN A GRANARY (Joe, D139), which is this method's own stated
        // intent finally enforced rather than assumed. The paragraph above promises the shed
        // is *"deliberately more generous than the granary"*, because food is what regulates
        // the village and a second ceiling fighting the first is Non-Negotiable 1 failing —
        // the player cannot tell which constraint stopped them.
        //
        // ⛔ IT HAD INVERTED BY AN ORDER OF MAGNITUDE: 343 against the granary's 2,850. The
        // derivation prices the logs needed to MAKE the winter's firewood, so raising
        // `firewood_per_split` to 50 (Joe, chasing a different problem) divided that term by
        // seven and quietly shrank the shed. A number derived from one lever moving under
        // another is exactly what D134 then measured — a village at **Logs 15 in store and
        // 1,968 on the ground**, capped from year five, which is what Joe played for
        // twenty-seven years and read as "there's never enough wood".
        //
        // The floor is the granary's own capacity rather than a typed constant, so the two
        // cannot drift apart again and the promise above stays true by construction.
        int wanted = firewood + logs;
        int floor = GranaryCapacity(config);

        return wanted < floor ? floor : wanted;
    }

    /// <summary>
    /// How much a storage pile holds, across every kind of goods.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Stated target: enough to raise the village's first buildings out of, and no
    /// more.</b> A pile costs nothing, so the only thing restraining it is its size — one
    /// large enough to be the granary would delete the reason to build a granary, and one
    /// too small to hold the timber for a house makes the opening a puzzle about hauling.
    /// </para>
    /// <para>
    /// So it is derived from what the opening actually needs: <b>a house and a woodcutter's
    /// hut</b>, plus a winter's firewood for the households the founding starts with. That is
    /// the shape of every capacity in this file — a sentence about what the building is for,
    /// turned into arithmetic — rather than a number somebody liked (D16).
    /// </para>
    /// </remarks>
    public static int PileCapacity(SimConfig config)
    {
        ArgumentNullException.ThrowIfNull(config);

        int toBuild = config.LogsPerHouse + config.HutLogs;
        int firewood = config.StartingHouseholds * FirewoodStoreWantedPerHousehold(config);

        return toBuild + firewood;
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

    /// <summary>
    /// How much of a good the village needs not to die — the floor a stock limit sits above.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>The derived half of D62's "derived floor, player ceiling".</b> A limit governs
    /// everything above this; set below it, the limit is still obeyed and the player is
    /// <em>told</em> (D43's pattern). One method rather than the caller working it out per
    /// good, because a floor computed in two places is a floor that can be corrected in one —
    /// this project's most repeated bug (D57).
    /// </para>
    /// <para>
    /// Each answer is the same number the village already aims at elsewhere, not a new one
    /// invented for the warning: food is the winter store per head, firewood is what every
    /// household burns and banks, and logs are what it takes to make that firewood plus a
    /// house's worth so building never has to empty the woodpile.
    /// </para>
    /// </remarks>
    public static int SurvivalFloorFor(SimConfig config, Goods goods, int people, int households)
    {
        ArgumentNullException.ThrowIfNull(config);

        if (people < 0)
        {
            people = 0;
        }

        if (households < 0)
        {
            households = 0;
        }

        switch (goods)
        {
            case Goods.Food:
                return RequiredStockpilePerAdult(config) * people;

            case Goods.Firewood:
                return FirewoodStoreWantedPerHousehold(config) * households;

            case Goods.Logs:
                int perSplit = config.FirewoodPerSplit < 1 ? 1 : config.FirewoodPerSplit;
                int forFirewood = households * FirewoodStoreWantedPerHousehold(config)
                    * config.LogsPerSplit / perSplit;
                return forFirewood + config.LogsPerHouse;

            case Goods.Stone:
            case Goods.Tools:
                // No floor, because nothing spends them yet — a survival floor is
                // derived from consumption, and neither has any. Named rather than left
                // to the default so that the day stone becomes what a building costs,
                // this is the line that is obviously wrong instead of quietly right.
                return 0;

            default:
                return 0;
        }
    }

    /// <summary>A one-line summary for logs and tests.</summary>
    public static string Describe(SimConfig config) =>
        $"adult eats {AdultFoodPerYear(config)}/yr, child {ChildFoodPerYear(config)}/yr; " +
        $"{TripsPerYear(config)} trips/yr yields {FoodGatheredPerYearAtWorst(config)}/yr at worst " +
        $"({config.VigourMinPercent}% vigour) => supports {DependantsSupportedAtWorst(config)} dependants " +
        $"(target {RequiredDependants}); required yield {RequiredGatherYield(config)}, configured {config.GatherYield}.";
}

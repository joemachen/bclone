using System.Text.Json.Serialization;
using Bclone.Sim.World;

namespace Bclone.Sim.Config;

/// <summary>
/// Tunables for a run, loaded from <c>data/sim.config.json</c>.
/// </summary>
/// <remarks>
/// Data-driven from day one (DESIGN.md §3) — nothing here is hardcoded, and a
/// modder is expected to edit the JSON. Immutable for the duration of a run:
/// config that changes mid-run is config that breaks replay.
/// </remarks>
public sealed record SimConfig
{
    /// <summary>
    /// Seed for the run. Same seed + same config + same tick count produces a
    /// byte-identical history.
    /// </summary>
    [JsonPropertyName("seed")]
    public ulong Seed { get; init; } = 12345UL;

    /// <summary>
    /// Ticks per in-game day. The only thing that gives a tick a "duration".
    /// </summary>
    [JsonPropertyName("ticks_per_day")]
    public int TicksPerDay { get; init; } = 4;

    /// <summary>
    /// Ticks per real second at 1x speed. <b>Playback only</b> — sim logic must
    /// never read this.
    /// </summary>
    [JsonPropertyName("target_ticks_per_second")]
    public double TargetTicksPerSecond { get; init; } = 1.0;

    /// <summary>
    /// Spiral-of-death guard: most ticks the driver will run for one frame.
    /// <b>Playback only</b> — sim logic must never read this.
    /// </summary>
    [JsonPropertyName("max_ticks_per_frame")]
    public int MaxTicksPerFrame { get; init; } = 250;

    // ---------------------------------------------------------------
    //  Calendar
    // ---------------------------------------------------------------

    /// <summary>Days in each of the four seasons.</summary>
    [JsonPropertyName("days_per_season")]
    public int DaysPerSeason { get; init; } = 15;

    // ---------------------------------------------------------------
    //  Needs
    // ---------------------------------------------------------------

    /// <summary>Hunger gained per tick.</summary>
    /// <remarks>
    /// <para>
    /// Seven, not ten. At ten a villager crossed <see cref="EatThreshold"/> every eight
    /// ticks — a meal every two days — which read as people permanently interrupting
    /// themselves to eat. Joe watching a run: <em>"the hunger is too aggressive and
    /// maybe it should be slower."</em> Seven is a meal every 2.8 days: forty per cent
    /// slower, and still a cadence you can watch rather than calculate.
    /// </para>
    /// <para>
    /// <b>Seven is where it stops being free, and that was measured rather than
    /// guessed.</b> Over 300 years the village holds a band of 17–32 at seven, against
    /// 16–31 at ten — unchanged. Below that it comes apart: 12–31 at six, 9–31 at five.
    /// Nobody starves at any of them, so it is not a food failure — a slower-eating
    /// village needs a smaller winter store per head, which is a thinner buffer, and a
    /// thinner buffer swings harder. Slowing hunger further means widening the
    /// population wave, and that trade should be made deliberately if it is made.
    /// </para>
    /// <para>
    /// <b>This is not a cosmetic dial.</b> It sets what an adult eats in a year, which
    /// is the number <see cref="World.VillageEconomy"/> derives the entire food economy
    /// from — the gather yield, the winter store, and the hands that can be spared for
    /// anything else. Changing it re-derives all of them (D16), and the tests assert
    /// the shipped config still meets its targets, so a change here fails the build
    /// rather than the village.
    /// </para>
    /// </remarks>
    [JsonPropertyName("hunger_per_tick")]
    public int HungerPerTick { get; init; } = 7;

    /// <summary>Hunger ceiling. Sitting here is what eventually kills.</summary>
    [JsonPropertyName("hunger_max")]
    public int HungerMax { get; init; } = 100;

    /// <summary>Hunger at or above which the villager will eat if they can.</summary>
    [JsonPropertyName("eat_threshold")]
    public int EatThreshold { get; init; } = 80;

    /// <summary>Hunger removed by one meal.</summary>
    [JsonPropertyName("eat_reduces_hunger")]
    public int EatReducesHunger { get; init; } = 80;

    /// <summary>Food consumed by one meal.</summary>
    [JsonPropertyName("food_per_meal")]
    public int FoodPerMeal { get; init; } = 5;

    /// <summary>
    /// Consecutive ticks at <see cref="HungerMax"/> before starvation.
    /// Boundary is <c>&gt;=</c> (spec §11).
    /// </summary>
    [JsonPropertyName("starvation_ticks")]
    public int StarvationTicks { get; init; } = 24;

    // ---------------------------------------------------------------
    //  Foraging
    // ---------------------------------------------------------------

    /// <summary>Food added by one completed gather.</summary>
    [JsonPropertyName("gather_yield")]
    public int GatherYield { get; init; } = 24;

    /// <summary>Ticks spent gathering once at the source.</summary>
    [JsonPropertyName("gather_ticks")]
    public int GatherTicks { get; init; } = 3;

    /// <summary>Ticks to cross one unit of distance.</summary>
    [JsonPropertyName("travel_ticks_per_unit")]
    public int TravelTicksPerUnit { get; init; } = 1;

    /// <summary>
    /// Food a household stores <b>per member</b> before it stops foraging and rests.
    /// </summary>
    [JsonPropertyName("stockpile_target")]
    public int StockpileTarget { get; init; } = 60;

    // ---------------------------------------------------------------
    //  The valley
    // ---------------------------------------------------------------

    /// <summary>Width of the world, in tiles, centred on the origin.</summary>
    /// <remarks>
    /// <para>
    /// Wide rather than square, because the terrain DESIGN.md §2.5 describes is a
    /// river valley — and the river runs along it, not across it.
    /// </para>
    /// <para>
    /// Nothing in the simulation reads this yet: it bounds the camera and gives the
    /// drawn ground an edge, so a mostly-empty map reads as <em>a valley with room to
    /// grow</em> rather than as a bug. It lives in sim config rather than view config
    /// because <b>the map generator will need it</b> (D18) — terrain, water, forest
    /// stands and forage sites all get generated into these bounds from the run's
    /// seed, at which point this stops being a drawing hint and becomes world state.
    /// </para>
    /// </remarks>
    [JsonPropertyName("map_width")]
    public int MapWidth { get; init; } = 120;

    /// <summary>Height of the world, in tiles, centred on the origin.</summary>
    [JsonPropertyName("map_height")]
    public int MapHeight { get; init; } = 80;

    /// <summary>Westmost tile of the valley. Derived, not configured.</summary>
    [JsonIgnore]
    public int MapMinX => -(MapWidth / 2);

    /// <summary>Eastmost tile of the valley. Derived, not configured.</summary>
    [JsonIgnore]
    public int MapMaxX => MapWidth - (MapWidth / 2) - 1;

    /// <summary>Northmost tile of the valley. Derived, not configured.</summary>
    [JsonIgnore]
    public int MapMinY => -(MapHeight / 2);

    /// <summary>Southmost tile of the valley. Derived, not configured.</summary>
    [JsonIgnore]
    public int MapMaxY => MapHeight - (MapHeight / 2) - 1;

    /// <summary>Where the villager starts and returns to.</summary>
    [JsonPropertyName("home_x")]
    public int HomeX { get; init; }

    /// <summary>Where the villager starts and returns to.</summary>
    [JsonPropertyName("home_y")]
    public int HomeY { get; init; }

    // food_source_x/y and extra_forage_sites used to live here, as literal
    // coordinates, and the remark on them said "these become generator output once the
    // map is seeded (D18)". They have. Deleted rather than left in place, because a
    // config key nobody reads is a trap: a modder edits it, nothing happens, and there
    // is no way to tell from the file that it is decoration. The rules that replaced
    // them are forage_site_count / forage_site_ring_tiles / site_jitter_tiles.

    // ---------------------------------------------------------------
    //  Life
    // ---------------------------------------------------------------

    /// <summary>
    /// What a <b>dependant</b> eats — a child or an elder — as a percentage of an adult's meal.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A child eating a full adult portion is not just wrong, it is fatal: two working adults
    /// cannot feed a household of four at full rations, so the village grew, starved, and died
    /// out every time. Children eat less because they are smaller — and that is what makes
    /// raising them survivable.
    /// </para>
    /// <para>
    /// <b>⭐ ELDERS EAT THE SAME SHARE (Joe, 2026-08-23), AND THE OLD BEHAVIOUR WAS NEVER
    /// CHOSEN.</b> <c>MealCostFor</c> had exactly one branch and it tested for
    /// <see cref="World.LifeStage.Child"/>, so **an elder ate like a prime adult while producing
    /// at <c>vigour_min_percent</c>** — not as a decision about ageing, but because the other
    /// arm of an <c>if</c> caught them. *A number nobody picked is still a number the game is
    /// balanced on*, which is why it was worth asking about rather than assuming.
    /// </para>
    /// <para>
    /// <b>⚠️ RENAMED FROM <c>child_food_share_percent</c> IN THE SAME BREATH</b>, because a key
    /// that governs elders while calling itself *child* is precisely the name-that-lies this
    /// project keeps catching (D148, D188). *Dependant* is the word the economy already uses —
    /// see <see cref="VillageEconomy.RequiredDependants"/>.
    /// </para>
    /// </remarks>
    [JsonPropertyName("dependant_food_share_percent")]
    public int DependantFoodSharePercent { get; init; } = 50;

    /// <summary>
    /// Age at which an unpaired adult starts looking for a partner and a home of
    /// their own.
    /// </summary>
    /// <remarks>
    /// Without household formation the village suffocates: children stay in their
    /// birth household forever, every house fills to max_household_size, births
    /// stop, and the settlement dies out with its last generation.
    /// </remarks>
    [JsonPropertyName("leave_home_age")]
    public int LeaveHomeAge { get; init; } = 18;

    // tree_stand_x/y likewise became generator output (D18) — see tree_stand_count
    // and tree_stand_ring_tiles.

    /// <summary>Wood one completed cut brings home.</summary>
    [JsonPropertyName("cut_yield")]
    public int CutYield { get; init; } = 12;

    /// <summary>Ticks spent cutting once at the stand.</summary>
    [JsonPropertyName("cut_ticks")]
    public int CutTicks { get; init; } = 4;

    /// <summary>How many people can work one forester's hut at once.</summary>
    /// <remarks>
    /// <para>
    /// A local fact about the place, not a statement about the village. What the
    /// village needs cut is decided by <c>LabourQuota</c>, which will happily leave
    /// this capacity unfilled in a year when there are barely enough hands to eat.
    /// </para>
    /// <para>
    /// <b>This was <c>tree_stand_capacity</c> until D159.</b> Tree stands were deleted in
    /// step C and this number never was, because it is load-bearing — it sizes the
    /// <b>forester's hut</b>, which is the same question wearing a different name: how many
    /// pairs of hands the village's timber needs in one place. A live setting named after a
    /// deleted mechanic is the D56 and D148 shape, so it is named for what it does.
    /// </para>
    /// </remarks>
    [JsonPropertyName("forester_hut_capacity")]
    public int ForesterHutCapacity { get; init; } = 3;

    // ---------------------------------------------------------------
    //  Firewood (D29) — the woodcutter's hut
    // ---------------------------------------------------------------

    /// <summary>
    /// Roughly how much of the valley is wooded, as a percentage (`forests-and-gathering.md`).
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>The target is content, the clump count is derived from it</b>
    /// (<c>MapGenerator.ForestClumpCount</c>, D16). *"About this much of the valley is wooded"*
    /// says what kind of place this is; how many clumps that takes is arithmetic, and typing
    /// the count would mean a bigger map quietly got a barer valley.
    /// </para>
    /// <para>
    /// ⚠️ <b>A target, not a promise.</b> Clumps are dropped independently and overlap, and
    /// none may fall on water or on a stone or iron seam — so the coverage actually achieved
    /// is lower than this. What the valley really ends up with is asserted by a measurement.
    /// </para>
    /// </remarks>
    [JsonPropertyName("forest_coverage_percent")]
    public int ForestCoveragePercent { get; init; } = 35;

    /// <summary>How big one clump of woodland is, as a diamond radius in tiles.</summary>
    /// <remarks>
    /// Content — a fact about what a wood looks like, and since the tree stands retired it is
    /// the only number that says so. Small clumps give a mottled valley, large ones give a few
    /// great forests; both are legitimate places to live and neither is derivable.
    /// </remarks>
    [JsonPropertyName("forest_clump_radius_tiles")]
    public int ForestClumpRadiusTiles { get; init; } = 4;

    /// <summary>How much open ground the founding site keeps around it.</summary>
    /// <remarks>
    /// <para>
    /// <b>⭐ Measured, not chosen, and the alternative was fatal.</b> With the valley wooded, 40
    /// of the 81 tiles within four of the founding site were forest, so every building the
    /// opening marks waited on a clearing first: the pile stood at <b>t67 instead of t1</b>, the
    /// woodcutter's hut never stood at all, and <b>all four founders froze</b> — against 4 alive
    /// and 2 roofed on bare ground. That is D93's rule (any inserted hop kills winter 1)
    /// arriving from worldgen.
    /// </para>
    /// <para>
    /// Exiles arriving in a river valley settle a glade; the woods begin a few tiles out, which
    /// is close enough for a gatherer's hut and far enough that the opening is not a clearing
    /// puzzle.
    /// </para>
    /// </remarks>
    [JsonPropertyName("founding_clearing_radius_tiles")]
    public int FoundingClearingRadiusTiles { get; init; } = 4;

    /// <summary>
    /// How far a gatherer's hut reaches for food, as a diamond radius in tiles.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Content, and the most load-bearing piece of content in the game</b>
    /// (`forests-and-gathering.md §3.2`). It is the ring the hut draws on the map, the ground
    /// whose trees decide what a trip is worth, and — from slice 3 — <b>the distance the whole
    /// food economy is derived against</b>, replacing the forage-site ring. A number the player
    /// can see beats one that is an artefact of where a generator dropped a berry patch.
    /// </para>
    /// <para>
    /// <b>Eight, and the eight is chosen to keep slice 3 small.</b> `MaxHomeToWorkTiles` is 7
    /// today; moving the anchor to 8 changes the round trip by one tile rather than
    /// re-deriving the economy from nothing, so the largest re-derivation on the board arrives
    /// as an adjustment instead of a rewrite.
    /// </para>
    /// </remarks>
    [JsonPropertyName("gatherer_hut_ring_tiles")]
    public int GathererHutRingTiles { get; init; } = 8;

    /// <summary>What a gatherer's hut costs to raise.</summary>
    /// <remarks>
    /// A real building with a real price, unlike the pile and the builder's hut — neither of
    /// those can be charged because nothing can be built without them, and a gatherer's hut has
    /// no such circle to break.
    /// </remarks>
    [JsonPropertyName("gatherer_hut_logs")]
    public int GathererHutLogs { get; init; } = 25;

    /// <summary>Ticks of work a gatherer's hut owes.</summary>
    [JsonPropertyName("gatherer_hut_work_ticks")]
    public int GathererHutWorkTicks { get; init; } = 40;

    /// <summary>What a forester's hut costs to raise.</summary>
    [JsonPropertyName("forester_hut_logs")]
    public int ForesterHutLogs { get; init; } = 25;

    /// <summary>Ticks of work a forester's hut owes.</summary>
    [JsonPropertyName("forester_hut_work_ticks")]
    public int ForesterHutWorkTicks { get; init; } = 40;

    /// <summary>
    /// How much harder putting a tree back is than taking one down.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Content, and it is the number that decides whether over-clearing is a mistake or a
    /// shrug.</b> A design statement about the world — the same class as
    /// <see cref="WorkGroundTilesPerWorker"/> — because nothing in the economy can compute how
    /// much effort a sapling is worth. What is <em>derived</em> from it is the consequence:
    /// <c>VillageEconomy.YearsToRewoodOnesGround</c>, which a guard holds to about a
    /// generation (D16's split — state the fact, derive the outcome).
    /// </para>
    /// <para>
    /// <b>Three, so felling is a decision you can regret for years without regretting the
    /// run.</b> One would make planting free and a cleared valley meaningless; ten would make
    /// it theoretical. §0.1: the mistake is real, visible and expensive, and the valley forgives
    /// you slowly.
    /// </para>
    /// </remarks>
    [JsonPropertyName("planting_costs_this_much_more_than_felling")]
    public int PlantingCostsThisMuchMoreThanFelling { get; init; } = 3;

    // ---------------------------------------------------------------
    //  The farm (`specs/crops-and-orchards.md`, D161)
    // ---------------------------------------------------------------

    /// <summary>What a farmhouse costs to raise.</summary>
    [JsonPropertyName("farmhouse_logs")]
    public int FarmhouseLogs { get; init; } = 25;

    /// <summary>Ticks of work a farmhouse owes.</summary>
    [JsonPropertyName("farmhouse_work_ticks")]
    public int FarmhouseWorkTicks { get; init; } = 40;

    /// <summary>Ticks a farmer spends putting one tile of ground under seed.</summary>
    /// <remarks>
    /// <para>
    /// Content, in the same class as <see cref="CutTicks"/> and <see cref="GatherTicks"/>: how
    /// long an action takes is a fact about the work, and nothing in the economy can compute
    /// it. What is <em>derived</em> from it is the consequence — how big a field one pair of
    /// hands can keep, which is what <c>VillageEconomy.FieldTilesOneFarmerKeeps</c> says and
    /// what a farm's work-ground allowance is priced against.
    /// </para>
    /// <para>
    /// <b>The same as <see cref="GatherTicks"/>, deliberately.</b> It is the same kind of thing
    /// — one person doing one job on one tile — and pricing it differently would be a claim
    /// about farming nobody could defend. <b>What makes farming expensive is the walking</b>,
    /// and that is charged where it actually happens rather than folded into a swing of a
    /// scythe.
    /// </para>
    /// </remarks>
    [JsonPropertyName("sow_ticks")]
    public int SowTicks { get; init; } = 3;

    /// <summary>Ticks a farmer spends taking one tile of standing crop.</summary>
    /// <remarks>
    /// <para>
    /// <b>⚠️ EQUAL TO <see cref="SowTicks"/> TODAY, AND STILL ITS OWN NUMBER.</b> The first
    /// draft made this dearer <em>"because reaping is the half with the load in it"</em>, and
    /// that reasoning was wrong twice over: the load is now charged as the walk it really is
    /// (<c>VillageEconomy.FieldTileTicks</c>), and folding it in here as well billed it twice.
    /// It stays a separate key because a modder changing what a crop costs to bring in should
    /// not have to change what it costs to plant — two facts about the work, which happen to
    /// have the same value.
    /// </para>
    /// </remarks>
    [JsonPropertyName("reap_ticks")]
    public int ReapTicks { get; init; } = 3;

    /// <summary>
    /// Food one tile of ripe field gives up when it is reaped.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Typed here and derived against a target, exactly like <see cref="GatherYield"/></b>
    /// (D16): <c>VillageEconomy.RequiredCropYield</c> says what it has to be for
    /// `crops-and-orchards.md §1`'s surviving target to hold — <em>a household working normally
    /// through spring, summer and autumn fills its winter store by the first day of winter</em>
    /// — and a guard checks the shipped file against it as well as the fixture (METHODOLOGY §3,
    /// and the six recorded times those two diverged).
    /// </para>
    /// </remarks>
    [JsonPropertyName("crop_yield_per_tile")]
    public int CropYieldPerTile { get; init; } = 67;

    /// <summary>
    /// How much of its own harvest a farm may hold before the walk gets longer (Joe: 100).
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>⭐ It is the first <c>Workplace.Store</c> in the project's history that anything
    /// writes to</b> — `professions.md §4`'s fifth element, on the type since D30 and dead
    /// ever since, with a branch in the building panel that could never be true. Joe: *"the
    /// harvested goods go to the granary although the farm itself can store up to 100 of the
    /// harvest goods by default."*
    /// </para>
    /// <para>
    /// <b>A buffer, not a destination.</b> The granary is still where food lives; the farm
    /// holds a harvest until somebody carries it. The 100 means something because the farmer
    /// hauls to the nearest storage <em>with room</em> and the farm's own store is underfoot —
    /// so it fills first and the walk lengthens once it is full.
    /// </para>
    /// <para>
    /// <b>⚠️ And a full store must REFUSE the overflow rather than swallow it.</b>
    /// <c>Stockpile.Add</c> returns what it actually took and the caller has to read it: D96 is
    /// precisely the bug of not reading it (17,451 food into a full granary and out of the
    /// world) and D144 is the same shape one deposit path over.
    /// </para>
    /// </remarks>
    /// <summary>How many people fit in a farmhouse — <b>two</b> (Joe, 2026-08-16).</summary>
    /// <remarks>
    /// <b>Content, not a derivation, and it used to be the other way round.</b> Deriving it
    /// from <c>max_household_size / MouthsFedByOneAdult</c> gave <c>1</c> — arithmetically fine
    /// and a bad building, because a workplace with one seat reads as broken rather than as
    /// small. How many pairs of hands fit in a steading is a fact about the world, in the same
    /// class as <c>work_ground_tiles_per_worker</c>; <b>what they produce is what gets derived</b>
    /// (D16 — state the fact, derive the outcome).
    /// </remarks>
    /// <summary>
    /// How much a household must be short before somebody walks to a store for it.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>⭐ "WORTH THE TRIP" (Joe, 2026-08-16), and it is the jitter he watched for months.</b>
    /// A fetch fired the moment a larder dipped below its floor, so a household two firewood
    /// short sent somebody out for two firewood — and with a store one tile from the door that
    /// is a villager visibly bouncing between two squares every thirty ticks. D166 measured it:
    /// runs of four to six flips, about a second of vibration every four seconds at 10x.
    /// </para>
    /// <para>
    /// <b>A percentage, because the bar is taken against two different things and the smaller
    /// wins.</b> An armful (<see cref="CarryCapacity"/>) says what a trip is worth carrying; the
    /// good's own target says what the household is actually trying to keep. Using the armful
    /// alone would set a bar of ten on firewood a household only wants eleven of — nobody would
    /// fetch fuel until they were nearly out, in winter, which is how people freeze. Taking the
    /// smaller keeps the rule honest for a good the village holds very little of.
    /// </para>
    /// <para>
    /// <b>⚠️ It is never a reason to starve or freeze.</b> `TryEmergencyRestock` sits ABOVE work
    /// in the priority order and fires when a household is nearly out regardless of this — so
    /// the bar delays a convenience errand and can never block a desperate one (D77).
    /// </para>
    /// </remarks>
    [JsonPropertyName("fetch_worth_this_share_percent")]
    public int FetchWorthThisSharePercent { get; init; } = 25;

    [JsonPropertyName("farmhouse_seats")]
    public int FarmhouseSeats { get; init; } = 2;

    [JsonPropertyName("farm_store_cap")]
    public int FarmStoreCap { get; init; } = 100;

    /// <summary>Where a warm start's builder's hut stands (D108).</summary>
    /// <remarks>
    /// <b>A position is content; the hut's SEATS are not.</b> Where a building sits is a fact
    /// about the valley that a modder may move freely, and how many hands fit in it is a
    /// consequence of the economy — so this is typed and
    /// <c>VillageEconomy.BuilderHutCapacity</c> is derived (D16, D50).
    /// <para>
    /// Only a warm start reads it. In the game as it ships the founders arrive to an empty
    /// valley and the hut is the player's first act (D70).
    /// </para>
    /// </remarks>
    [JsonPropertyName("builder_hut_x")]
    public int BuilderHutX { get; init; } = -1;

    /// <summary>Where a warm start's builder's hut stands (D108).</summary>
    [JsonPropertyName("builder_hut_y")]
    public int BuilderHutY { get; init; } = -1;

    // ⚠️ THERE IS DELIBERATELY NO `gatherer_hut_x` / `gatherer_hut_y`, and I added the pair
    // before measuring what they did. Every other founding building sits at a configured
    // offset from the village, so a hut did too — and it landed **inside the founding glade**,
    // the clearing D112 skips woodland over. A gatherer's hut in a clearing yields almost
    // nothing (measured: 3 food a trip against 51), the warm-start village starved, and 105
    // tests failed.
    //
    // A coordinate cannot know where the wood is. `SimWorld.WhereTheTreesAre` looks, which is
    // what a player does and what the mechanic is about.

    /// <summary>Where logs are split into firewood.</summary>
    /// <remarks>
    /// Near the homes rather than near the stand. Logs are drawn village-wide, so the
    /// only distance that costs anything is the woodcutter's daily walk from home.
    /// </remarks>
    [JsonPropertyName("woodcutter_hut_x")]
    public int WoodcutterHutX { get; init; } = -2;

    /// <summary>Where logs are split into firewood.</summary>
    [JsonPropertyName("woodcutter_hut_y")]
    public int WoodcutterHutY { get; init; } = 1;

    /// <summary>How many people can work one woodcutter's hut at once.</summary>
    [JsonPropertyName("woodcutter_hut_capacity")]
    public int WoodcutterHutCapacity { get; init; } = 3;

    /// <summary>Logs consumed by one splitting job.</summary>
    /// <remarks>
    /// The first workplace in the game that <b>consumes an input</b> (D29). It can be
    /// idle for want of logs rather than for want of a worker, which is a state the
    /// player has to be able to read — see the no-logs refusal in
    /// <c>LabourAllocator</c>.
    /// </remarks>
    [JsonPropertyName("logs_per_split")]
    public int LogsPerSplit { get; init; } = 6;

    /// <summary>Firewood produced by one splitting job.</summary>
    [JsonPropertyName("firewood_per_split")]
    public int FirewoodPerSplit { get; init; } = 12;

    /// <summary>Ticks spent splitting once at the hut.</summary>
    [JsonPropertyName("split_ticks")]
    public int SplitTicks { get; init; } = 4;

    // ---------------------------------------------------------------
    //  Storage (D30, D32)
    // ---------------------------------------------------------------

    /// <summary>Where the village's food is kept.</summary>
    /// <remarks>
    /// Near the homes. A granary is a building people walk to several times a season, so
    /// its position is the difference between a short errand and a long one. This places
    /// only the <em>founding</em> granary; every one after it the player sites themselves
    /// (D43), which is the first decision storage makes interesting.
    /// </remarks>
    [JsonPropertyName("granary_x")]
    public int GranaryX { get; init; } = 1;

    /// <summary>Where the village's food is kept.</summary>
    [JsonPropertyName("granary_y")]
    public int GranaryY { get; init; } = -1;

    /// <summary>Where materials are kept — logs, firewood, and later stone and cloth.</summary>
    /// <remarks>
    /// Separate from the granary on purpose (D32): food spoils and timber does not,
    /// and one undifferentiated pile would delete the per-household inequality D14
    /// exists to create.
    /// </remarks>
    [JsonPropertyName("storage_shed_x")]
    public int StorageShedX { get; init; } = -2;

    /// <summary>Where materials are kept.</summary>
    [JsonPropertyName("storage_shed_y")]
    public int StorageShedY { get; init; }

    /// <summary>Firewood one household burns per day of winter.</summary>
    /// <remarks>
    /// Per household, not per member — a house costs the same to heat whether two
    /// live in it or five. Winter only, for legibility: fuel that trickles all year
    /// is a background tax, whereas fuel demanded exactly when foraging stops is a
    /// season with teeth.
    /// </remarks>
    [JsonPropertyName("firewood_per_winter_day")]
    public int FirewoodPerWinterDay { get; init; } = 1;

    /// <summary>
    /// Days one burn of firewood lasts a household.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Joe, 2026-08-11: <em>"make firewood consumption take longer. like 4x longer. it
    /// goes too fast."</em></b> The rate had to move here rather than into
    /// <see cref="FirewoodPerWinterDay"/> because sim state is integer-only (D2) and a
    /// quarter of a log is not a number this game can hold. Four days per log is the same
    /// four-times-slower burn, expressed in a unit the sim has.
    /// </para>
    /// <para>
    /// <b>The economy follows it</b> — <c>FirewoodPerHouseholdPerWinter</c> divides by it, so
    /// the winter store target, the woodcutter seats and the foresters feeding them all come
    /// down together rather than the burn quietly getting cheaper than the budget.
    /// </para>
    /// </remarks>
    [JsonPropertyName("firewood_burn_interval_days")]
    public int FirewoodBurnIntervalDays { get; init; } = 1;

    /// <summary>
    /// Days between sweeps of the valley for regrowth. <b>Zero switches it off.</b>
    /// </summary>
    /// <remarks>
    /// <para>
    /// One sweep is both stages of a tree: a sapling seen by a sweep has stood for one
    /// period and becomes wood, and a grass tile touching wood becomes a sapling. So at
    /// sixty days — half a year on the shipped calendar — a cleared tile is a sapling within
    /// six months and a mature tree within a year, which is the rate Joe asked for.
    /// </para>
    /// <para>
    /// It is also the cost control: the sweep examines one period's worth of tiles per tick,
    /// so a longer period is cheaper per tick as well as slower. Zero is off, which is what
    /// Phase 0's world wants — its spec describes a fixed valley and a fixture that grew
    /// trees under the villager would put noise in the one place the project needs none.
    /// </para>
    /// </remarks>
    [JsonPropertyName("regrowth_period_days")]
    public int RegrowthPeriodDays { get; init; }

    /// <summary>How much of anything one villager can carry in one trip.</summary>
    /// <remarks>
    /// What stops a fetch being a teleport with extra steps (D30). One trip brings
    /// back one armful, so a household far from the granary genuinely eats worse than
    /// one beside it — which is where D32 says the interesting inequality lives.
    /// </remarks>
    [JsonPropertyName("carry_capacity")]
    public int CarryCapacity { get; init; } = 40;

    /// <summary>How much food one granary holds. <b>A size, not a promise.</b></summary>
    /// <remarks>
    /// <para>
    /// <b>⭐⭐ A RAW QUANTITY SINCE D219 (Joe): *"It's fine if the granary feeds a different
    /// number of people. The user should build more granaries — and will need to! — and upgrade
    /// them."*</b> It was <c>granary_feeds_people: 30</c>, and
    /// <see cref="World.VillageEconomy.GranaryCapacity"/> multiplied that by a winter's ration to
    /// reach a quantity.
    /// </para>
    /// <para>
    /// <b>The argument that settled it is about what a granary IS.</b> A granary is a box of a
    /// certain size; <em>how many people it feeds is a consequence of how much they eat.</em>
    /// Stating it as people made the building promise something about population and quietly
    /// resized itself whenever the food economy moved — so a village that ate more got a bigger
    /// granary for free, which is the opposite of a pressure. **Now, a village that eats more
    /// needs more granaries**, which is D39's *"the buffer is priced, not capped"* applied to the
    /// building rather than to the food, and it is what gives an upgrade tier something to be.
    /// </para>
    /// <para>
    /// ⚠️ <b>What it stops guaranteeing, stated plainly:</b> nothing here promises the founders
    /// can be fed. <see cref="World.VillageEconomy.PopulationCeiling"/> is a *consequence* of this
    /// number now — divide by what a head demands — rather than this number being derived to hit a
    /// population. **A granary too small for the village that starts in it is now possible**, and
    /// the validator below is what refuses it.
    /// </para>
    /// </remarks>
    [JsonPropertyName("granary_capacity")]
    public int GranaryCapacity { get; init; } = 2500;

    /// <summary>Where the market stands — among the homes, which is its whole value.</summary>
    [JsonPropertyName("market_x")]
    public int MarketX { get; init; } = 2;

    /// <summary>Where the market stands.</summary>
    [JsonPropertyName("market_y")]
    public int MarketY { get; init; } = 1;

    /// <summary>
    /// How many traders the market has room for. <b>Zero switches the market off.</b>
    /// </summary>
    /// <remarks>
    /// More than one, per Joe — a market is a place several people work, not a
    /// one-person post. Zero is a supported value and there is a test that runs the
    /// whole village on it: distribution by hand must stay something the settlement can
    /// live without, because the moment it cannot, an unstaffed market becomes a cliff
    /// the founding village falls off (spec §3, §14.4).
    /// </remarks>
    [JsonPropertyName("market_capacity")]
    public int MarketCapacity { get; init; } = 2;

    /// <summary>Goods the market keeps on hand, per household it serves.</summary>
    /// <remarks>
    /// A market is a short trip, not a second granary — it holds enough that a
    /// household's errand is usually satisfied there, and no more. Sized per household
    /// rather than flat so it does not become the village's real store as the
    /// settlement grows, which would quietly re-centralise everything storage just
    /// spread out.
    /// </remarks>
    [JsonPropertyName("market_stock_per_household")]
    public int MarketStockPerHousehold { get; init; } = 40;

    // ---------------------------------------------------------------
    //  What buildings cost to raise (D43)
    // ---------------------------------------------------------------
    //
    // Two numbers each, on purpose. Logs are what the village must HAVE; work ticks are
    // what it must SPEND. A building that is dear in one and cheap in the other is a
    // different decision from one that is dear in both, and collapsing them into a
    // single "cost" would delete that.

    // ---------------------------------------------------------------
    //  ⭐ THE STONE HALF OF EVERY RECIPE (`multi-material-construction.md`, D213)
    // ---------------------------------------------------------------
    //
    // ⭐ THE MACHINERY SHIPPED AT ZERO AND THE PRICES CAME AFTER, which is D82's and D210's
    // shape: the refactor landed as a **provable no-op** — every golden byte-identical — so the
    // balance change could not hide inside it. A recipe drops its zeros, so a building priced at
    // 0 stone has exactly the recipe it had before any of this existed.
    //
    // ⭐⭐ WHICH BUILDINGS PAY, AND IT WAS MEASURED RATHER THAN CHOSEN (D213, D214). Everything
    // the **player marks** pays; the two free buildings and the **house** do not.
    //
    //   STORES (granary 10, shed 8, market 10) — a granary the village cannot pay for waits, the
    //   settlement carries on out of its pile, and the site says what it is short of. Measured at
    //   fifty years: identical to charging nothing until somebody marks one.
    //
    //   HUTS (3 each, Joe's call — "a nominal amount") — fifty years of the shipped opening:
    //
    //     hut stone 0, no seam   →  24 alive, 2 gatherer huts, 2 woodcutter huts, 0 unfinished
    //     hut stone 3, no seam   →  24 alive, 1 gatherer hut,  1 woodcutter hut,  2 unfinished
    //     hut stone 3, a seam    →  24 alive, 2 gatherer huts, 2 woodcutter huts, 0 unfinished
    //
    //   **Full population either way.** The cost of not painting a seam is that the village
    //   builds FEWER huts, which is a legible and recoverable pressure rather than a death —
    //   `DESIGN.md §0.1`, the challenge is in the planning and never in the punishment.
    //
    // ⚠️⚠️ AN EARLIER MEASUREMENT SAID PRICING THE HUTS TOOK THE FOUNDING FROM 24 ALIVE TO 7,
    // AND IT IS RECORDED HERE BECAUSE IT WAS WRONG FOR AN INSTRUCTIVE REASON. That probe ran
    // BEFORE `SimWorld.NextSiteToServe` existed, so what it measured was **D135's starved-head
    // stall** — a site blocked on stone froze every site behind it — and not the price at all.
    // Re-measured with the stall fixed, the collapse is gone entirely. *A number is only as good
    // as the build it was taken on.*
    //
    // ⛔ A HOUSE STILL PAYS NOTHING, for a reason no measurement changes: a house is the one
    // building the VILLAGE decides to raise (D42), so a stone price there is a growth gate on a
    // resource an unattended valley never gathers.
    //
    // ⚠️ ONE KEY PER (BUILDING, GOOD), symmetric with the log keys above, and that is
    // deliberately a stopgap rather than the catalogue. `content-inventory.md` finding 1 and
    // `goods-catalog.md §9` both scope **BuildingKind becoming a row** as its own axis — ~45
    // buildings against 10 — and 45 × 4 materials is not a flat key list. What this buys is the
    // *structure*: `BuildingRecipe` holds N materials now, so the tier climb is unblocked and
    // the catalogue is a refactor rather than a redesign.

    /// <summary>Logs a granary takes to build.</summary>
    [JsonPropertyName("granary_logs")]
    public int GranaryLogs { get; init; } = 40;

    /// <summary>Ticks of work a granary takes, once the logs are on site.</summary>
    [JsonPropertyName("granary_work_ticks")]
    public int GranaryWorkTicks { get; init; } = 60;

    /// <summary>Logs a storage shed takes to build.</summary>
    [JsonPropertyName("shed_logs")]
    public int ShedLogs { get; init; } = 30;

    /// <summary>Ticks of work a storage shed takes.</summary>
    [JsonPropertyName("shed_work_ticks")]
    public int ShedWorkTicks { get; init; } = 45;

    /// <summary>Logs a market takes to build.</summary>
    [JsonPropertyName("market_logs")]
    public int MarketLogs { get; init; } = 35;

    /// <summary>Ticks of work a market takes.</summary>
    [JsonPropertyName("market_work_ticks")]
    public int MarketWorkTicks { get; init; } = 50;

    /// <summary>Stone a granary takes to build.</summary>
    [JsonPropertyName("granary_stone")]
    public int GranaryStone { get; init; } = 10;

    /// <summary>Stone a storage shed takes to build.</summary>
    [JsonPropertyName("shed_stone")]
    public int ShedStone { get; init; } = 8;

    /// <summary>Stone a market takes to build.</summary>
    [JsonPropertyName("market_stone")]
    public int MarketStone { get; init; } = 10;

    /// <summary>Stone a woodcutter's hut takes to build.</summary>
    [JsonPropertyName("hut_stone")]
    public int HutStone { get; init; } = 3;

    /// <summary>Stone a gatherer's hut takes to build.</summary>
    [JsonPropertyName("gatherer_hut_stone")]
    public int GathererHutStone { get; init; } = 3;

    /// <summary>Stone a forester's hut takes to build.</summary>
    [JsonPropertyName("forester_hut_stone")]
    public int ForesterHutStone { get; init; } = 3;

    /// <summary>Stone a farmhouse takes to build.</summary>
    [JsonPropertyName("farmhouse_stone")]
    public int FarmhouseStone { get; init; } = 3;

    /// <summary>
    /// Stone a house takes to raise.
    /// </summary>
    /// <remarks>
    /// ⚠️ <b>The one to think hardest about before raising above zero.</b> A house is the only
    /// building the <em>village</em> decides to put up — the player never places one (D42) — so
    /// a stone price here is a growth gate rather than a purchase, and an unattended valley
    /// paints no seam. `VillageEconomy` derives the timber budget against
    /// <see cref="LogsPerHouse"/> and has no stone term at all.
    /// </remarks>
    [JsonPropertyName("home_stone")]
    public int HomeStone { get; init; }

    /// <summary>Logs a woodcutter's hut takes to build.</summary>
    [JsonPropertyName("hut_logs")]
    public int HutLogs { get; init; } = 25;

    /// <summary>Ticks of work a woodcutter's hut takes.</summary>
    [JsonPropertyName("hut_work_ticks")]
    public int HutWorkTicks { get; init; } = 40;

    /// <summary>Ticks of work a house takes to raise (D102).</summary>
    /// <remarks>
    /// <para>
    /// <b>New because houses used to be instant</b>, which
    /// <c>specs/cold-start.md §7.1b</c> has been carrying as an open inconsistency since Joe
    /// watched it: every other building is marked, hauled to and worked on, and a house simply
    /// appeared the moment its timber was paid for.
    /// </para>
    /// <para>
    /// <b>The cheapest thing that is still a building.</b> Below a woodcutter's hut, because a
    /// house is one room and a hut is a workshop — and deliberately not below a pile, which
    /// costs no work at all because a pile is not a building. What this number really controls
    /// is how much a growing village's houses compete with its granaries and huts for the
    /// hands that build both, which is the competition D102 exists to create.
    /// </para>
    /// </remarks>
    [JsonPropertyName("home_work_ticks")]
    public int HomeWorkTicks { get; init; } = 30;

    // `pile_work_ticks` WAS HERE AND IS GONE (D96, Joe). It said "small, but not zero — the
    // work is what makes placing a pile a decision with a cost", and the cost turned out to
    // belong somewhere better: a pile may only go on ground that is already clear, so ITS
    // COST IS THE CLEARING. That is paid in the same currency the rest of the game uses and
    // it ties the harvest brush to placement, where an abstract eight ticks of levelling bare
    // earth tied it to nothing.
    //
    // Deleted rather than set to zero, on Joe's own reasoning: a number that is always zero
    // is a lie waiting to be found.

    /// <summary>
    /// How empty a household's stores get before somebody drops everything to refill them
    /// (D77).
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>An emergency line, not the ordinary one.</b> This is the share below which a
    /// family stops what it is doing — because a household at nothing in winter is the case
    /// the whole rule exists for, and the errand used to lose to ordinary work every tick.
    /// Joe watched a house come through a winter with no firewood at all while its residents
    /// walked next door to get warm.
    /// </para>
    /// <para>
    /// Deliberately low. A high number makes every villager an errand-runner and empties the
    /// berry patch, which is the shape D52 records; a low one means the interruption is rare
    /// and always justified. Measured at 20: household-time on an empty larder goes to
    /// <b>zero</b>, with a market and without.
    /// </para>
    /// </remarks>
    [JsonPropertyName("restock_emergency_percent")]
    public int RestockEmergencyPercent { get; init; } = 20;

    /// <summary>How much painted ground one worker can keep (D86).</summary>
    /// <remarks>
    /// <para>
    /// <b>The first limit in this game that is not distance.</b> Everything bounding work so
    /// far — <c>MaxHomeToWorkTiles</c>, catchment radii — asks how far somebody must walk.
    /// This asks how much they can look after, which is what makes the staffing control
    /// (D62's <c>−1/+1</c>) matter: paint more ground, hire more hands.
    /// </para>
    /// <para>
    /// <b>Content, not a derived number.</b> It says how much land one person keeps, which is
    /// a fact about the world rather than a consequence of the economy — the same class of
    /// number as a building's capacity. **The ceiling it implies is derived, though**: a
    /// workplace cannot be staffed past its capacity, so the most ground anybody can hold is
    /// capacity × this, and D86's *"to a limit"* needs no second number (D16).
    /// </para>
    /// </remarks>
    [JsonPropertyName("work_ground_tiles_per_worker")]
    public int WorkGroundTilesPerWorker { get; init; } = 24;

    // ⭐⭐ `logs_per_forest_tile`, `stone_per_rock_tile` and `iron_per_deposit_tile` MOVED TO THE
    // GOODS CATALOGUE (D210, `goods-catalog.md`) and are `GoodRow.YieldPerTile` now.
    //
    // All three were read by exactly ONE switch in `SimWorld` and by nothing else in the
    // codebase, which is what made moving them safe — and that switch's own comment had been
    // asking for this: *"a new harvestable kind is a row… not a fifth place to remember."*
    //
    // ⛔ They are DELETED rather than left in place, because a config key nothing reads is worse
    // than no key at all: a modder edits it, the game ignores them, and nothing says so. The
    // reasoning they carried is preserved on the rows and beside the seam counts below.
    //
    // The values were identical in the shipped file and the defaults (12, 12, 8), so this moved
    // no behaviour — which is what let the goldens stay byte-identical.

    // ---------------------------------------------------------------
    //  Visible seams (D67, D84, D90)
    // ---------------------------------------------------------------

    /// <summary>How many stone seams the generator lays down.</summary>
    /// <remarks>
    /// <b>Rules, not coordinates</b> (D18): a modder controls how much ore a valley has and
    /// roughly where, never which tile. Stone is the common one and sits near the village —
    /// it is what a building past a log hut costs (D63).
    /// </remarks>
    [JsonPropertyName("stone_seam_count")]
    public int StoneSeamCount { get; init; } = 4;

    /// <summary>How far out the stone seams ring the origin.</summary>
    [JsonPropertyName("stone_seam_ring_tiles")]
    public int StoneSeamRingTiles { get; init; } = 14;

    /// <summary>How wide one stone seam is.</summary>
    [JsonPropertyName("stone_seam_radius_tiles")]
    public int StoneSeamRadiusTiles { get; init; } = 2;

    /// <summary>How many iron seams the generator lays down.</summary>
    /// <remarks>
    /// <b>Fewer and further out than stone, and that is the design rather than scarcity for
    /// its own sake.</b> Reaching the iron is a decision the player makes — a valley whose ore
    /// is in the far woods plays differently from one where it is on the doorstep, which is
    /// the argument D67 makes for visible seams over a percentage roll.
    /// </remarks>
    [JsonPropertyName("iron_seam_count")]
    public int IronSeamCount { get; init; } = 2;

    /// <summary>How far out the iron seams ring the origin.</summary>
    [JsonPropertyName("iron_seam_ring_tiles")]
    public int IronSeamRingTiles { get; init; } = 26;

    /// <summary>How wide one iron seam is.</summary>
    [JsonPropertyName("iron_seam_radius_tiles")]
    public int IronSeamRadiusTiles { get; init; } = 1;

    /// <summary>How much land the exiles arrive having already chosen to live on (D42).</summary>
    /// <remarks>
    /// A village founded with no residential zone could never build a house, so the
    /// starter area exists to stop the game opening on a decision the player has no
    /// basis for — and to show them what a zone looks like before asking them to paint
    /// one.
    /// <para>
    /// Deliberately modest: large enough that a village nobody helps behaves as it
    /// always has, small enough that a player who is actively growing the place will
    /// run out and meet the brush rather than never needing it.
    /// </para>
    /// </remarks>
    /// <remarks>
    /// <para>
    /// <b>Four is a measured floor, not a taste.</b> At three, ten of eleven seeds held
    /// and the eleventh died out — and it died for a reason worth understanding: a
    /// village that cannot spread cannot form new households, so every home fills to
    /// <see cref="MaxHouseholdSize"/>, births stop, and the settlement ages out. That is
    /// D34's failure arriving by a different road.
    /// </para>
    /// <para>
    /// So the starter zone holds comfortably more than the population one granary
    /// supports. A village nobody helps behaves as it always has; the brush becomes
    /// necessary when the player builds more granaries and grows past it, which is
    /// exactly when they are paying attention.
    /// </para>
    /// </remarks>
    [JsonPropertyName("starting_residential_radius")]
    public int StartingResidentialRadius { get; init; } = 4;

    /// <summary>
    /// Whether the founders arrive to a village already built, or to an empty valley (D70).
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>False is the game; true is the world most of the tests describe.</b> The shipped
    /// file sets this false — the founders get a cart and nothing else, and every building,
    /// every home and the residential zone itself are the player's to make. Fixtures leave
    /// it true, so the several hundred tests written about an established village go on
    /// testing an established village rather than four people freezing in a field.
    /// </para>
    /// <para>
    /// <b>The precedent is <see cref="FirewoodPerWinterDay"/>.</b> Phase 0's spec rules
    /// warmth out by name, so its fixture switches fuel off entirely and
    /// <c>HearthSystem</c> honours that — <em>"the fixture should encode the world its
    /// tests describe"</em>. This is the same move one layer up, and the same discipline
    /// applies with it: <b>the cold start needs its own tests, or it becomes the one path
    /// in the game nothing exercises.</b> METHODOLOGY §3's rule about the gap between the
    /// fixture and the shipped file is what makes that non-optional — the gap has already
    /// produced D48, D49 and D50.
    /// </para>
    /// </remarks>
    [JsonPropertyName("founding_buildings")]
    public bool FoundingBuildings { get; init; } = true;

    /// <summary>How much the founders' cart can hold, across every kind of goods (D64).</summary>
    /// <remarks>
    /// <b>Small on purpose.</b> The cart accepts every good, unlike every other store, so
    /// its capacity rather than its rules is the only thing stopping it being the granary
    /// and the shed at once. A village that never outgrows its wagon never has to build.
    /// </remarks>
    [JsonPropertyName("cart_capacity")]
    public int CartCapacity { get; init; } = 200;

    /// <summary>Food the founders arrive carrying.</summary>
    /// <remarks>
    /// <b>One of the two dials for how hard the opening is</b>
    /// (<c>specs/cold-start.md §7.2</c>), the other being the founding season. The exposure
    /// rates are deliberately <em>not</em> a dial: they describe a person in the cold, and
    /// D53 refused moving them to make a body count come out right.
    /// </remarks>
    [JsonPropertyName("cart_food")]
    public int CartFood { get; init; } = 400;

    // `cart_logs` WAS HERE AND IS GONE (D90 step 4, D95, landed with D96). It was a start on
    // the first house, and it existed because of D72 — building timber was drawn only from
    // sheds and a cold start has none, so even felled logs could not become a house. The
    // harvest brush gave the village its own way to timber and that gate lifted without
    // anybody noticing.
    //
    // D95 measured taking it away rather than assuming: forty years went from 13 alive in 5
    // households to 14 in 6, houses still raised, and DoingNothingKillsTheFounders still
    // killed all four. Thirty logs was never the constraint, and that cart space is worth
    // more as food. The shipped file had already been set to 0.
    //
    // DELETED RATHER THAN LEFT AT ZERO, because the cart now REFUSES logs and Stockpile.
    // Receive knows nothing about Accepts — so the key would have gone on quietly loading
    // the fixture's default of ten into a wagon that will not take them.

    /// <summary>Tools the founders arrive carrying — the only ones in the world (D17, D64).</summary>
    /// <remarks>
    /// <para>
    /// <b>Inert until there is a workshop</b>, and that is the honest state of it: nothing
    /// consumes tools yet, so this is a stock the player can see and cannot spend. D17 parked
    /// tools waiting on somewhere to make them, and Joe's opening has the founders arrive with
    /// them, so the good and the founding stock land together and the mechanic follows.
    /// </para>
    /// <para>
    /// <b>Not a difficulty dial</b>, unlike <see cref="CartFood"/> beside it — it cannot be,
    /// while nothing spends it. When tools start wearing out it becomes one, and this remark
    /// is what should be deleted then.
    /// </para>
    /// </remarks>
    [JsonPropertyName("cart_tools")]
    public int CartTools { get; init; } = 20;

    // ⛔⛔ `cart_stone` WAS HERE FOR ONE COMMIT AND IS GONE (Joe, D215): *"there should already
    // be stone on the map for the user to ask the laborers to harvest. I don't want stone in the
    // cart. Only food and tools."*
    //
    // ⚠️⚠️ IT EXISTED BECAUSE OF A UNIT I MISREAD, AND THAT IS THE PART WORTH KEEPING. D214
    // measured the distance from a founding site to the nearest reachable stone as **120**, read
    // it as 120 tiles, and concluded the seams were out of reach — so the founders were given a
    // pile of stone to start with. `TravelCostField.Cost` returns **cost units**, and
    // `BaseTileCost` is 10: the real distance is **twelve tiles**, on every seed. Worldgen says
    // so out loud one file over — *"STONE NEAR, IRON FAR… a valley whose ore sits in the far
    // woods plays differently from one where it is on the doorstep"* — and
    // `stone_seam_ring_tiles` is 14.
    //
    // **The founding was never short of stone; the test fixture simply never painted a seam.**
    // A number in the wrong unit bought a starting resource the game did not need — the same
    // shape as D214's other correction, where a probe measured a stall and was read as measuring
    // a price.

    /// <summary>Share of a building's logs returned when it is pulled down, as a percentage.</summary>
    /// <remarks>
    /// Deliberately less than everything. Demolition is how a player corrects a mistake
    /// (D43), and it should cost something — otherwise a badly-sited granary is free to
    /// undo and the placement decision carries no weight at all.
    /// </remarks>
    [JsonPropertyName("demolition_returns_percent")]
    public int DemolitionReturnsPercent { get; init; } = 50;

    /// <summary>
    /// Days outdoors in winter, unclothed, before a healthy adult is in danger (D45).
    /// </summary>
    /// <remarks>
    /// <para>
    /// Stated in <b>days</b>, not ticks, because it is a statement about a human being
    /// in the cold rather than about the tick rate — and the tick rate has already moved
    /// once (D49). Fifteen days is half a winter, which is Joe's number and is chosen so
    /// that going out is dangerous without being immediately fatal.
    /// </para>
    /// <para>
    /// Zero switches outdoor cold off entirely, which is the world clothing eventually
    /// creates — the <c>market_capacity: 0</c> pattern, so the village can be tested
    /// against it before the clothing chain exists.
    /// </para>
    /// </remarks>
    [JsonPropertyName("exposure_days_outdoors")]
    public int ExposureDaysOutdoors { get; init; } = 15;

    /// <summary>
    /// Days under a roof with no fire burning before a healthy adult is in danger (D45).
    /// </summary>
    /// <remarks>
    /// Twenty-five, and what that number decides is worth knowing: it is <em>less</em>
    /// than a thirty-day winter, so an unheated house can still kill inside one season.
    /// Had it landed above thirty, <c>CauseOfDeath.Cold</c> would have gone dormant until
    /// clothing shipped and D17's whole reversal with it.
    /// </remarks>
    [JsonPropertyName("exposure_days_sheltered")]
    public int ExposureDaysSheltered { get; init; } = 25;

    /// <summary>
    /// Days at a burning hearth to come back <b>from the brink of freezing</b> (Joe, D192).
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>The third number in D45's table, and the one that was never stated.</b> Fifteen days
    /// outdoors kills; twenty-five under a fireless roof kills; **five at a fire brings you all
    /// the way back.** Together those three are the whole cold model, readable in one line each.
    /// </para>
    /// <para>
    /// <b>⭐ IT USED TO BE DERIVED BY MIRRORING THE OUTDOOR RATE, WHICH MEANT FIFTEEN.</b> The
    /// old reasoning was that mirroring *"needs no number of its own"* — true, and it quietly
    /// chose one anyway: half a winter to thaw. Joe, watching it: *"fire warm up should be much
    /// faster than it currently is."* **A derivation that avoids stating a number still states
    /// one**, and this is what that costs when nobody checks what it came out as.
    /// </para>
    /// <para>
    /// ⚠️ <b>Bounded below by a measurement</b>: a fire that zeroed the count outright was built,
    /// measured and rejected — villagers spend **76% of winter at a lit hearth**, so nobody froze
    /// in 120 years. Five days is fast; instant is a reset.
    /// </para>
    /// </remarks>
    [JsonPropertyName("thaw_days_at_a_fire")]
    public int ThawDaysAtAFire { get; init; } = 5;

    /// <summary>
    /// How far toward freezing a villager gets before they break off work to get warm.
    /// </summary>
    /// <remarks>
    /// Halfway is where the shipped system already put its "you are cold" narration, so
    /// the player has been trained on that line; this makes it mean something rather
    /// than merely be said. Zero switches the behaviour off and nobody ever comes in.
    /// </remarks>
    [JsonPropertyName("seek_shelter_percent")]
    public int SeekShelterPercent { get; init; } = 50;

    /// <summary>Wood a couple needs before they can build a home of their own.</summary>
    /// <remarks>
    /// <para>
    /// This is what makes timber matter: the village spreads only as fast as it can
    /// build, so how it spends its labour decides how it grows. Drawn from both
    /// parent households, all-or-nothing, like the food dowry.
    /// </para>
    /// <para>
    /// Switched on only after dynamic labour demand landed. Before that, a fixed
    /// forager demand sent everyone to the berry patch, nobody cut timber, and this
    /// gate quietly stopped the village growing at all.
    /// </para>
    /// </remarks>
    [JsonPropertyName("logs_per_house")]
    public int LogsPerHouse { get; init; } = 30;

    // `forage_site_capacity` is deleted with the patches. *"How many people can work one
    // forage site at once"* has no subject any more: a gatherer's hut prices its seats from
    // its own ring (D112), which is the same idea derived rather than typed.

    // ---------------------------------------------------------------
    //  Map generation (D18) — rules, not outcomes
    // ---------------------------------------------------------------

    // ⭐ `forage_site_count` AND `forage_site_ring_tiles` ARE DELETED, and the second of them
    // was the anchor the whole food economy hung off — *"the economy reads this, which is why
    // generation is bounded rather than checked"*. The bound is `gatherer_hut_ring_tiles`
    // now: a number the player can see drawn on the map, rather than the radius of a ring of
    // berry patches no player could ever have learned (`forests-and-gathering.md §3.2`).
    //
    // What made them worth deleting is what they were FOR. They kept one economy across every
    // seed, so a shared seed stayed comparable — and a single stated ring size does that job
    // without also deciding where the village's food comes from.

    /// <summary>
    /// How far a site or stand may wander off its slot. What makes one valley differ
    /// from another.
    /// </summary>
    /// <remarks>
    /// <b>Every tile of this is paid for up front by the whole economy.</b> The food
    /// budget assumes the worst case — a site jittered directly away from the home that
    /// needs it, which costs twice this in Manhattan distance — so generosity here
    /// makes every villager in every valley work harder, including on the seeds where
    /// the sites happen to land close. One tile buys visibly different valleys; two
    /// pushed the required gather yield up by two thirds.
    /// </remarks>
    [JsonPropertyName("site_jitter_tiles")]
    public int SiteJitterTiles { get; init; } = 1;

    // `tree_stand_count`, `tree_stand_ring_tiles` and `tree_stand_radius_tiles` are deleted
    // with the stands. Timber comes off ground a forester's hut was given, and the woodland
    // it is given is painted across the whole valley by `forest_coverage_percent`.

    /// <summary>How far the founding site may sit from the middle of the ring.</summary>
    /// <remarks>
    /// Small on purpose. The economy's distance budget assumes a village inside the
    /// ring of sites rather than off to one side of it, so this is the one piece of
    /// jitter that has to stay modest.
    /// </remarks>
    [JsonPropertyName("founding_jitter_tiles")]
    public int FoundingJitterTiles { get; init; } = 2;

    /// <summary>How wide the river runs.</summary>
    /// <remarks>
    /// Zero generates no river at all, which is a supported valley and a useful control
    /// in tests. <b>A river is load-bearing, not scenery</b> — water is impassable and
    /// every travel query routes round it (D40, D41), so widening this genuinely moves
    /// the village's walks.
    /// </remarks>
    [JsonPropertyName("river_width_tiles")]
    public int RiverWidthTiles { get; init; } = 2;

    /// <summary>Poorest ground the generator will produce, 0–255.</summary>
    /// <remarks>
    /// <b>No longer "reserved"</b> — a farm's yield reads the ground under its field
    /// (`specs/per-site-yield.md §4.1`). ⚠️ **This pair is the tuning lever for how much
    /// siting a farm matters**, and at the shipped values a well-sited field is worth about
    /// twice a badly-sited one. The algorithm is not the lever; these are.
    /// </remarks>
    [JsonPropertyName("soil_quality_min")]
    public int SoilQualityMin { get; init; } = 40;

    /// <summary>Best ground the generator will produce, 0–255.</summary>
    [JsonPropertyName("soil_quality_max")]
    public int SoilQualityMax { get; init; } = 200;

    /// <summary>
    /// How many tiles across a patch of comparable ground is — <b>the size of a soil region</b>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>⭐ Measured, not picked</b> (D16, `per-site-yield.md §3.1`). Across 104 candidate
    /// thirteen-tile fields: scale 4 averages out over a field, scale 24 leaves too few
    /// distinct regions, and <b>scale 8 gives a p90÷p10 of 200%</b> — a genuine two-to-one
    /// between good ground and poor. Eight tiles is a couple of fields across, which is a
    /// region a player can see and choose to walk to rather than a lottery per tile (D67).
    /// </para>
    /// <para>
    /// <b>1 switches regions off</b> and returns the per-tile noise this replaced, which is
    /// the honest way to disable it rather than a special case somebody has to remember.
    /// </para>
    /// </remarks>
    [JsonPropertyName("soil_region_scale")]
    public int SoilRegionScale { get; init; } = 8;

    /// <summary>
    /// How far around the founding site the ground is capped at ordinary
    /// (<see cref="World.VillageEconomy.ReferenceSoil"/>). 0 switches it off.
    /// </summary>
    /// <remarks>
    /// <b>The founders settled for safety, not for richness.</b> Without this the founding
    /// ground's quality is a coin flip — measured at the 99th, 93rd, 91st and 83rd percentile
    /// in four seeds of eight — and half of all games would start on the best ground in the
    /// valley, which deletes the reason to go anywhere (`per-site-yield.md §3.2`). It only
    /// ever caps, so it cannot make a poor start poorer.
    /// </remarks>
    [JsonPropertyName("founding_ordinary_radius_tiles")]
    public int FoundingOrdinaryRadiusTiles { get; init; } = 10;

    /// <summary>
    /// Years between the village sharing out its work again from scratch.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Joe's call, following <em>Banished</em> (D20): rather than pinning people in
    /// place with rules, the village periodically re-runs the whole allocation, so
    /// workers drift toward the jobs nearest where they live. A household whose
    /// forager died, or a couple who built a house on the far side of the valley,
    /// gets corrected by the next reshuffle instead of needing a special case.
    /// </para>
    /// <para>
    /// <b>Every three years</b> (D46), and the shipped config says so — this default of 1
    /// is the older answer, kept only because a config file that sets it is the one that
    /// matters. D20 argued that a <em>seasonal</em> reshuffle churns jobs faster than a
    /// player can read the reason for holding one; a yearly one is the same objection at
    /// lower volume. Three years is a rhythm a person can notice.
    /// </para>
    /// <para>
    /// Affordable because the urgent cases do not wait for it: <c>TakeUpSlack</c> runs at
    /// every season boundary, and a job left vacant by a death is filled the tick after it
    /// happens (D47).
    /// </para>
    /// </remarks>
    [JsonPropertyName("labour_reshuffle_years")]
    public int LabourReshuffleYears { get; init; } = 1;

    /// <summary>
    /// How often the village fills its openings and lets go of anybody it no longer wants — in
    /// <b>ticks</b> (D200).
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Joe, playing: *"30 days feels unresponsive."*</b> This pass was hard-wired to
    /// <see cref="TicksPerSeason"/>, so a change made on the professions panel waited up to a
    /// whole season to bite — measured at 25, 15 and 5 in-game days depending on where in the
    /// season it was set.
    /// </para>
    /// <para>
    /// <b>⚠️ IT IS NOT THE RESHUFFLE AND MUST NOT BECOME IT.</b>
    /// <see cref="LabourReshuffleYears"/> moves people who already have jobs, and D20/D46 both
    /// argue that doing so often *"churns jobs faster than a player can read the reason for
    /// holding one."* **This pass never moves anybody who already holds a job** — it fills
    /// openings from the idle and sheds a surplus the player asked to shed. Running it more
    /// often makes the village obey sooner; it does not make anybody's career shorter.
    /// </para>
    /// <para>
    /// <b>Zero is not allowed.</b> A pass on every tick is the per-tick reassignment
    /// <c>LabourSystem</c>'s own remarks rule out.
    /// </para>
    /// </remarks>
    [JsonPropertyName("labour_slack_ticks")]
    public int LabourSlackTicks { get; init; } = 120;

    // ---------------------------------------------------------------
    //  Skill (`specs/skills-catalog.md`, Phase 3)
    // ---------------------------------------------------------------

    /// <summary>
    /// The catalogue — <b>rows, not enum values</b> (`skills-catalog.md §4.1`, D168).
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>One per job that exists, and no more</b> (§4.2). Every ❌ profession in
    /// `professions.md §4` brings its own when it lands; inventing them now would be a
    /// catalogue of things nobody can hold.
    /// </para>
    /// <para>
    /// <b>⛔ LABORERS HOLD NO SKILL, AND THAT IS DELIBERATE.</b> D66 refused
    /// <c>JobKind.Laborer</c> on the grounds that a laborer is *"the villagers no job currently
    /// wants"* — a position in the priority order, not a trade (D87). **A skill in being spare
    /// is a contradiction**, and it would quietly make the fallback a career.
    /// </para>
    /// <para>
    /// Defaults live here rather than in the shipped file, exactly like
    /// <see cref="HouseholdNames"/> and <see cref="TownNames"/>: a modder replaces the list, and
    /// a config that says nothing gets the game as designed.
    /// </para>
    /// </remarks>
    /// <summary>
    /// The goods that exist — <b>rows, so a modder can add a seventh</b> (`goods-catalog.md`, D210).
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>⭐ Ids 0–5 match <see cref="Goods"/> exactly, and must.</b> The enum survives as an alias
    /// for the built-in six (Joe's call) so the economy can go on naming food directly; <b>this list
    /// is what says how many goods there are and what each one does.</b>
    /// </para>
    /// <para>
    /// <b>⛔ Appended, never renumbered.</b> Ids are what a stockpile indexes by, what a stock limit
    /// is saved under and what the state hash mixes in order.
    /// </para>
    /// <para>
    /// Defaults live here rather than in the shipped file, like <see cref="Skills"/> and
    /// <see cref="HouseholdNames"/> — <b>one source of truth, not two.</b> Writing them into
    /// `data/sim.config.json` as well would recreate exactly the fixture-versus-shipped drift
    /// METHODOLOGY §3 warns about, which has already produced D48, D49 and D50.
    /// </para>
    /// </remarks>
    [JsonPropertyName("goods")]
    public IReadOnlyList<GoodRow> GoodsCatalog { get; init; } = new[]
    {
        new GoodRow
        {
            Id = (int)World.Goods.Food,
            Name = "food",
            StoredBy = new[] { StoreKind.Granary, StoreKind.Market, StoreKind.Cart },
        },
        new GoodRow
        {
            Id = (int)World.Goods.Logs,
            Name = "logs",
            SourceName = "woodland",
            YieldPerTile = 12,

            // ⛔ No cart. The founders' load is what they could carry, and logs are the one thing
            // that plausibly will not fit in a wagon you arrived in (D90 step 4) — the refusal that
            // makes the storage pile load-bearing.
            StoredBy = new[] { StoreKind.Shed, StoreKind.Pile },
        },
        new GoodRow
        {
            Id = (int)World.Goods.Firewood,
            Name = "firewood",
            StoredBy = new[] { StoreKind.Shed, StoreKind.Market, StoreKind.Cart, StoreKind.Pile },
        },
        new GoodRow
        {
            Id = (int)World.Goods.Stone,
            Name = "stone",
            SourceName = "a stone seam",
            YieldPerTile = 12,
            StoredBy = new[] { StoreKind.Shed, StoreKind.Cart, StoreKind.Pile },
        },
        new GoodRow
        {
            Id = (int)World.Goods.Tools,
            Name = "tools",
            StoredBy = new[] { StoreKind.Shed, StoreKind.Cart, StoreKind.Pile },
        },
        new GoodRow
        {
            Id = (int)World.Goods.Iron,
            Name = "iron",
            SourceName = "an iron seam",
            YieldPerTile = 8,
            StoredBy = new[] { StoreKind.Shed, StoreKind.Cart, StoreKind.Pile },
        },
    };

    /// <summary>
    /// The trades that exist — <b>rows, so a modder can add a seventh</b> (`jobs-catalog.md`, D218).
    /// </summary>
    /// <remarks>
    /// <b>Ids 0–5 match <see cref="JobKind"/> exactly, and must</b> — every golden and every saved
    /// staffing figure is pinned to them. Defaults live here rather than in the shipped file, like
    /// <see cref="GoodsCatalog"/> and <see cref="Skills"/>: <b>one source of truth, not two.</b>
    /// </remarks>
    [JsonPropertyName("jobs")]
    public IReadOnlyList<JobRow> JobsCatalog { get; init; } = new[]
    {
        new JobRow
        {
            Id = (int)JobKind.Forager,
            Name = "forager",
            Plural = "foragers",
            Doing = "gathering",
            WorksAt = BuildingKind.GathererHut,

            // ⛔ No limit. Food is gathered as well as farmed, and standing the gatherers down on
            // a full granary is a decision nobody has taken.
            LimitedBy = null,
        },
        new JobRow
        {
            Id = (int)JobKind.Forester,
            Name = "forester",
            Plural = "foresters",
            Doing = "felling timber",
            WorksAt = BuildingKind.ForesterHut,
            LimitedBy = World.Goods.Logs,
        },
        new JobRow
        {
            Id = (int)JobKind.Woodcutter,
            Name = "woodcutter",
            Plural = "woodcutters",
            Doing = "splitting firewood",
            WorksAt = BuildingKind.WoodcutterHut,
            LimitedBy = World.Goods.Firewood,
        },
        new JobRow
        {
            Id = (int)JobKind.Marketer,

            // ⚠️ TWO WORDS FOR ONE TRADE, AND THAT IS D188 UNRESOLVED RATHER THAN A TYPO. The
            // staffing panel says "traders"; the roster beside a villager's name says "marketer".
            // Both are carried so the row does not settle Joe's question by accident.
            Name = "marketer",
            Plural = "traders",
            Doing = "the market",
            WorksAt = BuildingKind.Market,
            LimitedBy = null,
        },
        new JobRow
        {
            Id = (int)JobKind.Builder,
            Name = "builder",
            Plural = "builders",
            Doing = "building",
            WorksAt = BuildingKind.BuilderHut,
            LimitedBy = null,
        },
        new JobRow
        {
            Id = (int)JobKind.Farmer,
            Name = "farmer",
            Plural = "farmers",
            Doing = "farming",
            WorksAt = BuildingKind.Farmhouse,
            LimitedBy = World.Goods.Food,
        },
    };

    /// <summary>
    /// The buildings that exist — <b>rows, not enum values</b> (`specs/buildings-catalog.md`).
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>⚠️ NULL BY DEFAULT, AND THAT IS THE ONE PLACE THIS CATALOGUE DIFFERS FROM
    /// <see cref="GoodsCatalog"/> AND <see cref="JobsCatalog"/>.</b> Their defaults are literals, so
    /// a plain initialiser works. <b>The built-in ten are priced from other keys on this same
    /// config</b> — <c>logs_per_house</c>, <c>granary_logs</c>, <c>hut_stone</c> — and a property
    /// initialiser cannot read <c>this</c>. Null therefore means <em>"the built-in ten, priced from
    /// this config"</em>; see <see cref="BuildingRows"/>.
    /// </para>
    /// <para>
    /// <b>⭐ AND THAT IS NOT A WORKAROUND, IT IS WHAT KEEPS ONE SOURCE OF TRUTH.</b> Restating
    /// <c>logs_per_house</c> as a row literal would make it two numbers that must agree — the shed's
    /// capacity, the stockpile's capacity and the timber quota all derive against it, and every
    /// <c>Config with { LogsPerHouse = … }</c> in the suite would quietly stop meaning anything.
    /// <b>Folding those keys into the rows is a separate slice with no behaviour in it</b>
    /// (`buildings-catalog.md §8`), because it is a re-derivation rather than a move.
    /// </para>
    /// <para>
    /// <b>⛔ Ids 0–9 match <see cref="BuildingKind"/> exactly, and must</b> — <c>works_at</c> names
    /// them, and the enum survives as an alias for the built-in ten.
    /// </para>
    /// </remarks>
    [JsonPropertyName("buildings")]
    public IReadOnlyList<BuildingRow>? Buildings { get; init; }

    /// <summary>The buildings this config describes — its own list, or the built-in ten.</summary>
    public IReadOnlyList<BuildingRow> BuildingRows => Buildings ?? DefaultBuildings();

    /// <summary>The built-in ten, priced from this config's own keys.</summary>
    private IReadOnlyList<BuildingRow> DefaultBuildings() => new[]
    {
        new BuildingRow
        {
            Id = (int)BuildingKind.Granary,
            Name = "granary",
            Materials = new[]
            {
                new MaterialCost(World.Goods.Logs, GranaryLogs),
                new MaterialCost(World.Goods.Stone, GranaryStone),
            },
            WorkTicks = GranaryWorkTicks,
            Stores = StoreKind.Granary,

            // ⭐ STATED, SINCE D219 (Joe): *"it's fine if the granary feeds a different number of
            // people. The user should build more granaries — and will need to!"* A granary is a box
            // of a stated size; how many people it feeds falls out of how much they eat.
            StoreCapacity = GranaryCapacity,
        },
        new BuildingRow
        {
            Id = (int)BuildingKind.Shed,
            Name = "storage shed",
            Materials = new[]
            {
                new MaterialCost(World.Goods.Logs, ShedLogs),
                new MaterialCost(World.Goods.Stone, ShedStone),
            },
            WorkTicks = ShedWorkTicks,
            Stores = StoreKind.Shed,

            // ⛔ DERIVED, and it must stay derived (D16): a horizon of households, the firewood they
            // want, the logs to split it out of, a house's timber, floored at a granary. Typing that
            // number in is the move `buildings-catalog.md §2.2` refuses.
            StoreCapacity = null,
        },
        new BuildingRow
        {
            Id = (int)BuildingKind.Market,
            Name = "market",
            Materials = new[]
            {
                new MaterialCost(World.Goods.Logs, MarketLogs),
                new MaterialCost(World.Goods.Stone, MarketStone),
            },
            WorkTicks = MarketWorkTicks,

            // A market is a place to work as well as a place to keep things (D14) — the one row
            // that both stores and employs.
            Stores = StoreKind.Market,
            StoreCapacity = null,
            Seats = MarketCapacity,
        },
        new BuildingRow
        {
            Id = (int)BuildingKind.WoodcutterHut,
            Name = "woodcutter's hut",
            Materials = new[]
            {
                new MaterialCost(World.Goods.Logs, HutLogs),
                new MaterialCost(World.Goods.Stone, HutStone),
            },
            WorkTicks = HutWorkTicks,
            Seats = WoodcutterHutCapacity,
        },
        new BuildingRow
        {
            Id = (int)BuildingKind.Pile,

            // ⭐ "stockpile", not "storage pile" (Joe, D217). ⚠️ It shares a word with the
            // `Stockpile` class and they are not the same thing: that is the goods container every
            // store, larder, workplace and pair of arms holds; this is the name of one kind of
            // store building.
            Name = "stockpile",

            // NOTHING AT ALL — no materials and no work (D96), which is what `Mark` reads to know
            // it is free and instant. A village with nowhere to put things cannot begin, and asking
            // it to build a store out of timber it has nowhere to stack is a circle. Its cost moved
            // somewhere better rather than being abolished: a pile may only stand on clear ground,
            // so THE CLEARING IS WHAT IT COSTS.
            Stores = StoreKind.Pile,
            StoreCapacity = null,
        },
        new BuildingRow
        {
            Id = (int)BuildingKind.Home,

            // ⚠️ NOT USED AS A LABEL — a house is not numbered (`SimWorld.NameFor`), because a home
            // is identified by the family in it. Carried so the row is complete and so a modded
            // dwelling has somewhere to put its word.
            Name = "house",
            Materials = new[]
            {
                new MaterialCost(World.Goods.Logs, LogsPerHouse),
                new MaterialCost(World.Goods.Stone, HomeStone),
            },
            WorkTicks = HomeWorkTicks,
            HouseCapacity = MaxHouseholdSize,
        },
        new BuildingRow
        {
            Id = (int)BuildingKind.BuilderHut,
            Name = "builder's hut",

            // ⭐ FREE AND INSTANT, LIKE THE STOCKPILE (D108). It is the one building that must exist
            // before any other can be raised, so charging timber for it would be the same circle.
            Seats = null,
        },
        new BuildingRow
        {
            Id = (int)BuildingKind.GathererHut,
            Name = "gatherer's hut",
            Materials = new[]
            {
                new MaterialCost(World.Goods.Logs, GathererHutLogs),
                new MaterialCost(World.Goods.Stone, GathererHutStone),
            },
            WorkTicks = GathererHutWorkTicks,

            // ⛔ Derived from the ring: tiles in it ÷ tiles per worker.
            Seats = null,
            GatheringRadius = GathererHutRingTiles,
        },
        new BuildingRow
        {
            Id = (int)BuildingKind.ForesterHut,
            Name = "forester's hut",
            Materials = new[]
            {
                new MaterialCost(World.Goods.Logs, ForesterHutLogs),
                new MaterialCost(World.Goods.Stone, ForesterHutStone),
            },
            WorkTicks = ForesterHutWorkTicks,

            // ⛔ Derived from what the woodcutters can eat, plus a hand for building.
            Seats = null,
        },
        new BuildingRow
        {
            Id = (int)BuildingKind.Farmhouse,
            Name = "farmhouse",
            Materials = new[]
            {
                new MaterialCost(World.Goods.Logs, FarmhouseLogs),
                new MaterialCost(World.Goods.Stone, FarmhouseStone),
            },
            WorkTicks = FarmhouseWorkTicks,
            Seats = FarmhouseSeats < 1 ? 1 : FarmhouseSeats,

            // ⭐ The only building with a buffer of its own (`crops-and-orchards.md §3.2a`): reaping
            // is bursty and the granary is across the village, so the store underfoot fills first
            // and the walk lengthens once it is full.
            LocalStoreCap = FarmStoreCap,
        },
        new BuildingRow
        {
            Id = (int)BuildingKind.Library,
            Name = "library",
            Materials = new[]
            {
                new MaterialCost(World.Goods.Logs, LibraryLogs),
                new MaterialCost(World.Goods.Stone, LibraryStone),
            },
            WorkTicks = LibraryWorkTicks,

            // ⛔ THE HARD CAP, AND IT IS CARRYING `tech-tree.md §11`'s GUARD NEARLY ALONE. Three
            // costs were meant to stop *"write everything down"* being always correct; D204 deleted
            // one of them by making recording automatic. **Choosing which techniques get shelves is
            // what is left**, so the number is small on purpose and *"build another library"* is
            // the answer to wanting more.
            Shelves = LibraryShelves,
        },
    };

    [JsonPropertyName("skills")]
    public IReadOnlyList<SkillRow> Skills { get; init; } = new[]
    {
        new SkillRow
        {
            Id = 1,
            Name = "foraging",
            GrownBy = JobKind.Forager,
            YearsPhrase = "as a forager",
            MasteryLine = "{0} has foraged these woods for {1} years. "
                + "Nothing that grows here goes unnoticed now.",
        },
        new SkillRow
        {
            Id = 2,
            Name = "forestry",
            GrownBy = JobKind.Forester,
            YearsPhrase = "as a forester",
            MasteryLine = "{0} has worked these woods for {1} years. "
                + "Where to fell and where to plant takes no thinking about now.",
        },
        new SkillRow
        {
            Id = 3,
            Name = "woodcutting",
            GrownBy = JobKind.Woodcutter,
            YearsPhrase = "as a woodcutter",
            MasteryLine = "{0} has split the village's wood for {1} years. "
                + "The grain gives way where it always did.",
        },
        new SkillRow
        {
            Id = 4,
            Name = "farming",
            GrownBy = JobKind.Farmer,
            YearsPhrase = "as a farmer",

            // ⭐⭐ Joe's own sentence, from `DESIGN.md`'s opening paragraph by way of §3.3b —
            // the one he asked for by name. Pronoun-free: villagers have names and no sex.
            MasteryLine = "{0} has farmed these fields for {1} years. "
                + "There is nothing about this ground left to learn.",
        },
        new SkillRow
        {
            Id = 5,
            Name = "building",
            GrownBy = JobKind.Builder,
            YearsPhrase = "as a builder",
            MasteryLine = "{0} has raised the village's roofs for {1} years. "
                + "The work goes up straight without measuring twice.",
        },
        new SkillRow
        {
            Id = 6,
            Name = "trading",
            GrownBy = JobKind.Marketer,
            YearsPhrase = "as a marketer",
            MasteryLine = "{0} has carried the village's goods for {1} years. "
                + "Every door and every shortcut is known ground.",
        },
    };

    /// <summary>
    /// Years a granary's count must be kept before anybody here can write (D32, §7a).
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>⚠️ FIFTEEN IS A PACING NUMBER AND IT IS A PROPOSAL.</b> It is chosen to sit **just
    /// before** the first home-grown master, not after: a granary goes up around year three to
    /// five, so literacy lands about year eighteen to twenty, and the first technique needs
    /// twenty years on a trade. **The library becomes buildable slightly before there is anything
    /// to write in it**, which gives the player a season or two to prepare rather than a scramble.
    /// </para>
    /// <para>
    /// ⛔ <b>If the two ever cross, the feature reads as broken</b> — a technique announced as lost
    /// before the player was ever allowed to build the thing that would have saved it is the
    /// funeral surprise `tech-tree.md §11` forbids. <b>Whichever way these numbers move, they move
    /// together.</b>
    /// </para>
    /// </remarks>
    [JsonPropertyName("literacy_years")]
    public int LiteracyYears { get; init; } = 15;

    /// <summary>Logs a library takes to build.</summary>
    [JsonPropertyName("library_logs")]
    public int LibraryLogs { get; init; } = 35;

    /// <summary>Stone a library takes to build.</summary>
    /// <remarks>
    /// <b>More than a hut and less than a granary.</b> A library is the first building the village
    /// raises for a reason other than eating, so it should cost enough to be a decision and not so
    /// much that it is only ever a late-game monument.
    /// </remarks>
    [JsonPropertyName("library_stone")]
    public int LibraryStone { get; init; } = 12;

    /// <summary>Ticks of work a library takes, once the materials are on site.</summary>
    [JsonPropertyName("library_work_ticks")]
    public int LibraryWorkTicks { get; init; } = 55;

    /// <summary>
    /// How many techniques one library can hold records of.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>⚠️ THREE AGAINST FOUR TECHNIQUES, AND THE RATIO IS THE POINT RATHER THAN THE NUMBER.</b>
    /// A library that holds everything the village can know is not a decision; one that holds most
    /// of it is. **The moment a fifth technique exists this number must be looked at again**, which
    /// is why it is a stated key and not a constant.
    /// </para>
    /// <para>
    /// <b>⛔ A PROPOSAL UNTIL A RUN ARGUES WITH IT</b> — `tech-tree.md §12` lists *"starting library
    /// capacity"* among the things it deliberately refuses to invent, and the standing rule is that
    /// a number in a document comes from a run. This one has not had one.
    /// </para>
    /// </remarks>
    [JsonPropertyName("library_shelves")]
    public int LibraryShelves { get; init; } = 3;

    /// <summary>
    /// The techniques that exist — <b>rows, not code</b> (`specs/tech-tree.md`).
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>⛔ FOUR, NOT THIRTY-NINE, AND THAT IS THE POINT.</b> `TECH-EXAMPLE.md` holds 39 named
    /// techniques from Joe (D206) and <b>almost every one names a building that does not exist</b>
    /// — a sawmill, a dairy house, a gristmill. These four hang off <b>trades the village already
    /// has</b>, which is what makes Phase 4 buildable before the T1/T2 building tier
    /// (`phase-4-the-tech-tree.md §1`). The other thirty-five arrive with their buildings.
    /// </para>
    /// <para>
    /// <b>⚠️ EVERY NUMBER HERE IS A PROPOSAL AND NONE HAS HAD A RUN.</b> D196's own example is
    /// *"+15% firewood per log"*, and that is where the woodcutter's starts. `tech-tree.md §12`
    /// refuses false precision on exactly these, and the standing rule is <em>if a number goes
    /// into a document, it comes from a run.</em>
    /// </para>
    /// <para>
    /// <b>⭐ The lines are written as sentences about a person doing something</b>, because that is
    /// the whole difference between this and a research menu — and because `phase-4-the-tech-tree.md
    /// §6`'s success test fails if the answer to *"what happened?"* is *"a node re-locked"* rather
    /// than a name.
    /// </para>
    /// </remarks>
    [JsonPropertyName("techniques")]
    public IReadOnlyList<TechniqueRow> Techniques { get; init; } = new[]
    {
        // ⭐ D196's OWN EXAMPLE, WORD FOR WORD: *"a master woodcutter works out splitting lumber in
        // a way that gives more cords — +15% firewood per log."* It is here first because it is the
        // one technique in this list Joe specified himself, down to the number.
        new TechniqueRow
        {
            Id = 0,
            Name = "splitting lumber",
            Skill = 3,
            YieldBonusPercent = 15,
            DiscoveryLine = "{0} has split the village's firewood long enough to see the grain "
                + "before the axe falls. The same log gives more cords now.",
            LostLine = "{0} took the trick of the grain with them. The woodpile will be thinner "
                + "for it.",
        },
        new TechniqueRow
        {
            Id = 1,
            // ⚠️ THE LINES SAY WHAT IT DOES, WHICH IS NOT WHAT THE WORD USUALLY MEANS. Coppicing
            // is properly about *regrowth* — cutting so the stool comes back — and the first draft
            // of these lines said exactly that, while the effect was more timber per stand. **A
            // sentence that promises a mechanic the code does not have is the untraceable outcome
            // §1.1 forbids**, and it would have been read as a regrowth bug for a phase. The
            // effect is honest and the words follow it; the regrowth reading is a technique of its
            // own to be written when regrowth is something a technique can touch.
            Name = "coppicing",
            Skill = 2,
            YieldBonusPercent = 12,
            DiscoveryLine = "{0} has worked these stands long enough to cut them so they throw up "
                + "straight poles instead of scrub. The same wood gives more usable timber now.",
            LostLine = "The stands are being cut anyhow again. {0} was the last who knew how to "
                + "take them.",
        },

        // ⭐ `DESIGN.md §2.7`'s OWN WORKED EXAMPLE — *"your master farmer develops crop rotation
        // after 25 years"* — arriving as content at last. The pillar has used it as an illustration
        // since the first day of the project.
        new TechniqueRow
        {
            Id = 2,
            Name = "crop rotation",
            Skill = 4,
            YieldBonusPercent = 15,
            DiscoveryLine = "{0} has read this ground for twenty years, and has begun resting one "
                + "field in three. What grows in the others comes up thicker for it.",
            LostLine = "The fields are all sown again this spring. {0} was the only one who knew "
                + "to rest them.",
        },
        new TechniqueRow
        {
            Id = 3,
            Name = "tended patches",
            Skill = 1,
            YieldBonusPercent = 10,
            DiscoveryLine = "{0} has walked these woods so long that the good patches are tended "
                + "rather than merely found. They give more every year now.",
            LostLine = "The tended patches are going back to wild. {0} was the last who knew "
                + "which they were.",
        },
    };

    /// <summary>
    /// Years on the task before somebody is a master of it.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Twenty</b> (Joe, D174), against a working life of about fifty-five —
    /// <see cref="AdultAge"/> to a lifespan of 55–79. **A bit over a third of a career**, which
    /// means a founder who sticks to one trade masters it and is a master for the back half of
    /// their life, and a child born in year 1 masters at thirty-two — **so mastery and the first
    /// grandchildren arrive together** (§3.3b). The generational loop does the pacing rather
    /// than a timer.
    /// </para>
    /// <para>
    /// <b>Content, not derivation</b> — the class <c>granary_feeds_people</c> is in (D165: a
    /// stated fact about the world, with the consequence derived). A modder can move it.
    /// </para>
    /// </remarks>
    [JsonPropertyName("mastery_years")]
    public int MasteryYears { get; init; } = 20;

    // ⭐⭐ SKILL DECAY IS GONE, AND IT WAS BUILT AND MEASURED BEFORE IT WENT (D183, Joe:
    // *"let's give to the player, not punish or decay"*). It was `skill_decay_years_per_year_lost`
    // and `skill_floor_years`, derived against `labour_reshuffle_years` — three years away costs
    // one year of the trade, floored at a year.
    //
    // **§3.4 required it on the grounds that "a fifty-year-old who did six jobs is a master of
    // six". Measured, that cannot happen:** mastery needs 9,600 ticks and an adult life is about
    // 26,400, so **at most two masteries fit in a whole life** even holding a trade every waking
    // tick — and over sixty years the most any living villager had mastered was **one**.
    //
    // **What the rate did do was take 37% of everything one forager earned**, because a villager
    // spends over half their adult life off any given trade (D46 moves them every three years).
    // Agnes held foraging for 12,240 ticks — more than mastery requires — and never became a
    // master. *That is the trap §3.4 itself forbids, produced by §3.4's own cure.*

    /// <summary>
    /// What a tick counts for when the villager is <b>out on the job</b>, in hundredths.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>⭐ A TICK OUT ON THE WORK IS WORTH MORE THAN A TICK WAITING FOR IT</b> (Joe, D183).
    /// A forester who is out felling learns faster than one sitting at home because the hut has
    /// no logs — **but the second is still a forester, and still gaining**, which is the half
    /// that keeps a stuttering supply chain from being a punishment as well as a shortage.
    /// </para>
    /// <para>
    /// <b>Hundredths of a tick, so the weighting is a percentage with no float near sim state</b>
    /// (D2). 150 against an idle 100 is Joe's 1.5×.
    /// </para>
    /// <para>
    /// ⚠️ <b>Measured consequence, stated rather than discovered later:</b> time out on the job
    /// varies by trade — **forestry 88%, woodcutting 82%, foraging 41%, trading 30%, building
    /// 27%** — so at 1.5× a forester accrues about **27% faster than a builder**. That is
    /// divergence the player can *see and act on* (keep the hut supplied, staff it properly),
    /// which is §2.3's traceable pressure rather than an invisible tax.
    /// </para>
    /// </remarks>
    [JsonPropertyName("skill_work_per_active_tick")]
    public int SkillWorkPerActiveTick { get; init; } = 150;

    /// <summary>
    /// What a tick counts for when the villager <b>holds the trade but is not out on it</b>.
    /// </summary>
    /// <remarks>
    /// <b>100 is the anchor</b>: <c>mastery_years</c> means *that many years of holding a seat
    /// you never leave the house for*, and anybody who actually works it gets there sooner.
    /// **The generous direction on purpose** (D183) — the idle forester is idle because the
    /// village ran out of logs, which is not their doing.
    /// </remarks>
    [JsonPropertyName("skill_work_per_idle_tick")]
    public int SkillWorkPerIdleTick { get; init; } = 100;

    /// <summary>
    /// Extra work a learner gets per tick for standing beside a <b>master of the same trade at
    /// the same workplace</b>, as a percentage (`skills-catalog.md §5.1a`, D202).
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>§2.1's whole point:</b> *"that skill dies with the person unless an elder apprentices a
    /// youth."* Until this existed, skill was personal and **nothing transferred** — a master
    /// died and their years died with them, with no way for the village to have done anything
    /// about it.
    /// </para>
    /// <para>
    /// <b>⛔ THE TEACHER PAYS NOTHING</b> (Joe's call, D202, following D183's *"give, never
    /// punish or decay"*). This adds to the learner and takes nothing from anybody. ⚠️ **The
    /// stated consequence is that §5.3's policy dial has nothing to trade off**, which is why
    /// there is no dial rather than a dial that does nothing.
    /// </para>
    /// <para>
    /// <b>Zero is a supported state and is what the guards pose</b> to measure the feature
    /// against its own absence — §10's anti-vacuity rule: *a village that never teaches must
    /// produce measurably less than one that does.*
    /// </para>
    /// </remarks>
    [JsonPropertyName("apprentice_learning_bonus_percent")]
    public int ApprenticeLearningBonusPercent { get; init; }

    /// <summary>
    /// How much faster a <b>master</b> does the work, as a percentage — <b>the width of the
    /// whole pillar</b> (`skills-catalog.md §3.3`, §12).
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>⭐⭐ SKILL SCALES HOW LONG A JOB TAKES, NOT WHAT IT YIELDS — DURATION FIRST, YIELD
    /// SECOND</b> (§3.3). The reason is legibility rather than arithmetic: **a villager who is
    /// out longer is visible on the map, and one who brings back less is only visible in a
    /// panel.** It is also what discharges D28, which has asked since Phase 1 for time-on-task to
    /// be personal — two people who take different numbers of ticks to do the same thing stop
    /// arriving together within a season and never re-synchronise.
    /// </para>
    /// <para>
    /// <b>⛔ THE NOVICE FLOOR IS UNTOUCHED AND THAT IS WHAT KEEPS THE ECONOMY STANDING</b>
    /// (§3.2). At zero progress this scales nothing at all, so `VillageEconomy`'s derivation —
    /// which solves the **survival floor**, about the least skilled person in the valley — goes
    /// on being exactly as true as it was. **Mastery is headroom above a floor, and headroom
    /// above a floor is what progression is.**
    /// </para>
    /// <para>
    /// <b>⚠️ QUANTISED HARD, BECAUSE THE DURATIONS ARE TINY.</b> `sow_ticks` and `reap_ticks`
    /// are **3**, `cut_ticks` and `split_ticks` are **4** — so the only reachable speeds are
    /// whole ticks, and a bonus that does not round to one **does nothing whatsoever**.
    /// Measured: at **17% not one duration moves**, and at **25% only the four-tick trades
    /// do** — a village at 25% produces population and food identical to a village with the
    /// feature switched off. <c>AMasterIsFasterAtEveryTrade</c> exists to fail the build if a
    /// tweak here ever rounds the whole pillar away again.
    /// </para>
    /// <para>
    /// <b>⭐⭐ FIFTY IS MEASURED, NOT PICKED (§12), AND IT HAS A CLEAN STATEMENT: a master's
    /// action takes half the ticks, rounded up.</b> 3 → 2 and 4 → 2. Across three seeds at a
    /// century, against the same villages with the bonus at zero:
    /// </para>
    /// <code>
    /// seed     population        food stored
    /// 12345    23 → 29           3,677 → 4,631
    /// 2        63 → 65           4,870 → 9,648
    /// 42       15 → 20           2,204 → 2,178
    /// </code>
    /// <para>
    /// <b>Population rises on every seed</b>, which is D161's mid-game answer arriving: a
    /// masterful village supports more people. **34% was tried first and is marginal** —
    /// population unchanged on two seeds of three. *Narrow makes skill a footnote, and at these
    /// durations narrow means literally nothing.*
    /// </para>
    /// <para>
    /// ⚠️ <b>The spec predicted a different shape and it is worth recording.</b> §3.2 expected
    /// mastery to cash out as *"the same output from fewer hands — and the hands it frees become
    /// laborers"*. **Laborer counts did not move at all**; the village grew instead. Both are
    /// D161's answer, but through a different door than the one predicted.
    /// </para>
    /// </remarks>
    [JsonPropertyName("mastery_speed_bonus_percent")]
    public int MasterySpeedBonusPercent { get; init; } = 50;

    /// <summary>
    /// How many of the founders arrive already <b>masters</b> of a trade
    /// (`skills-catalog.md §3.2c`).
    /// </summary>
    /// <remarks>
    /// <b>⭐ THE SHAPE IS FIXED AND THE TRADES ARE SEEDED</b> — one master, one journeyman and
    /// the rest novices (Joe, 2026-08-23). Every seed gets the same *strength* of party and a
    /// different *speciality*, so a second playthrough differs in **what you can do** rather than
    /// in **whether you can live**. A fully seeded roll would make a four-novice seed and a
    /// two-master seed a bad run and a good one rather than two playthroughs, and §0.1 is that
    /// the challenge is in the planning, never in the punishment.
    /// </remarks>
    [JsonPropertyName("founding_masters")]
    public int FoundingMasters { get; init; } = 1;

    /// <summary>How many founders arrive as <b>journeymen</b>. See <see cref="FoundingMasters"/>.</summary>
    [JsonPropertyName("founding_journeymen")]
    public int FoundingJourneymen { get; init; } = 1;

    /// <summary>
    /// Whether every villager is drawn a personal rhythm at birth (`skills-catalog.md §3.5`).
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>⭐ A SWITCH BECAUSE THE TEST PLAN REQUIRES ONE, NOT BECAUSE ANYBODY WOULD TURN IT
    /// OFF.</b> §10 asks for a **synthetic all-novice village with the mixed founding switched
    /// off and the seeded rhythm switched off**, and for D28's lockstep guard to be *"checked red
    /// by running with both switched off"*. Neither is posable without this.
    /// </para>
    /// <para>
    /// <b>⛔ IT SKIPS THE DRAW RATHER THAN ZEROING THE RESULT, AND THAT IS THE WHOLE POINT.</b>
    /// Draw order is the seed contract — a run with the rhythm off consumes exactly the draws it
    /// consumed before §3.5 existed (name, then lifespan), so it reproduces the old history
    /// byte for byte. Zeroing after drawing would shift every subsequent number and prove
    /// nothing.
    /// </para>
    /// </remarks>
    [JsonPropertyName("seeded_rhythm")]
    public bool SeededRhythm { get; init; } = true;

    // ⭐ `forager_catchment_tiles` IS DELETED (`forests-and-gathering.md §3`, Joe: *"get rid
    // of the ring and the distance restrictions"*). It was ten tiles, and past it a villager
    // simply could not hold a job however much they wanted it.
    //
    // **What replaces it is not a bigger number, it is a different kind of thing.** The
    // allocator sorts candidates by travel cost, so the nearest hands are still claimed
    // first; what has gone is the *refusal*. A ruinous commute is now a mistake the player
    // can make, watch and pay for, which is D58's settled mechanism and §2.3's argument that
    // pressure should be traceable to something the player did.
    //
    // ⚠️ **Its removal carried a condition rather than a caveat** (D112): deleting the fence
    // makes a ruinous commute *silent*, so the consequence had to become readable in the same
    // slice — see `Villager.CommuteNote`.

    /// <summary>
    /// How many households the economy is derived to support.
    /// </summary>
    /// <remarks>
    /// The furthest home in a village this size sets the worst-case round trip, and
    /// therefore the yield the whole economy needs. Deriving from the first
    /// household instead made every outlying family unable to feed itself.
    /// </remarks>
    [JsonPropertyName("economy_horizon_households")]
    public int EconomyHorizonHouseholds { get; init; } = 20;

    /// <summary>
    /// Winter store as a percentage of one member's winter need.
    /// </summary>
    /// <remarks>
    /// Above 100 because surviving winter exactly is not surviving winter — the
    /// shock that actually kills a household is a worker dying or ageing out
    /// part-way through it. The margin is what absorbs that.
    /// </remarks>
    [JsonPropertyName("winter_buffer_percent")]
    public int WinterBufferPercent { get; init; } = 180;

    /// <summary>
    /// A household is short below this percentage of its food target.
    /// </summary>
    /// <remarks>
    /// <b>The name is older than the job.</b> Nothing shares anything automatically any
    /// more — D30 deleted both sharing policies and the market replaced them. This is now
    /// the line at which the <em>village</em> counts itself short of food
    /// (<see cref="World.LabourQuota.VillageIsShortOfFood"/>), which is what decides
    /// whether anyone can be spared from gathering.
    /// </remarks>
    [JsonPropertyName("sharing_need_percent")]
    public int SharingNeedPercent { get; init; } = 50;

    /// <summary>
    /// A household sets out to fetch once its own store falls below this share of target.
    /// </summary>
    /// <remarks>
    /// Also named for the deleted sharing policy, where it was how much a giver kept back.
    /// It is a fetch trigger now: above <see cref="SharingNeedPercent"/> so a household
    /// starts walking to the store before it is in the state the village would call short.
    /// </remarks>
    [JsonPropertyName("sharing_keep_percent")]
    public int SharingKeepPercent { get; init; } = 80;

    /// <summary>
    /// Share of a parent household's larder that leaves with a departing child, as
    /// a percentage.
    /// </summary>
    [JsonPropertyName("dowry_percent")]
    public int DowryPercent { get; init; } = 25;

    /// <summary>Youngest age at which a villager can become a parent.</summary>
    [JsonPropertyName("fertility_min_age")]
    public int FertilityMinAge { get; init; } = 18;

    /// <summary>Oldest age at which a villager can become a parent.</summary>
    [JsonPropertyName("fertility_max_age")]
    public int FertilityMaxAge { get; init; } = 40;

    /// <summary>Years a household waits between children.</summary>
    [JsonPropertyName("birth_interval_years")]
    public int BirthIntervalYears { get; init; } = 4;

    /// <summary>
    /// Food a household must have stored before it will have a child.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A village that breeds into a famine is not telling a story, it is just
    /// oscillating. Requiring a surplus first makes population growth a
    /// <em>consequence</em> of a good decade rather than a background process.
    /// </para>
    /// <para>
    /// <b>A percentage of the household's own target, not a flat number — and the
    /// comment above was describing behaviour the code did not have.</b> It used to be
    /// an absolute 45 while the food target scales with members, so a household of
    /// seven with a target of 462 would have a child at 45: a tenth of a full larder,
    /// which is not "a surplus" by any reading. The village grew to fifty, outran what
    /// the valley could feed, and sixty-one people starved on the way back down.
    /// </para>
    /// <para>
    /// Scaling it is what makes population <em>self-limiting</em>: as the village
    /// approaches what its forage sites can support, households stop reaching their
    /// targets, and births slow before anyone starves rather than after. That is the
    /// difference between a stable settlement and a boom-bust one (D31).
    /// </para>
    /// </remarks>
    /// <remarks>
    /// <para>
    /// <b>⭐ 80 -> 60 (Joe, D155), and it is the dial D153 deliberately left alone.</b> He
    /// played a village that sat at **four people for eleven years** and asked why nobody was
    /// having children. Nothing was broken: both households were housed in Year 1, and the bar
    /// is <c>stockpile_target × population × this</c> — at four people that is **304 food**, and
    /// his stores read **292**. The bar scales with the village, so a small settlement sits
    /// just under it more or less permanently.
    /// </para>
    /// <para>
    /// <b>Since D153 this is the ONLY food term in the birth gate</b>, and it is read in exactly
    /// two places — the gate and <c>VillageEconomy.PopulationCeiling</c> — so lowering it
    /// raises the derived ceiling by the same arithmetic. The formula does not approximate the
    /// gate; it *is* the gate.
    /// </para>
    /// <para>
    /// <b>⚠️ IT BUYS GROWTH WITH HUNGER, MEASURED RATHER THAN HOPED.</b> This is the term the
    /// deleted household gate was redundant against, and taking it down is the closest this
    /// economy has come to the famine D153 records — *"bred to ninety-two, thirty-three starved
    /// on the way back down."* The bar is what stops that, so it is lowered to where growth
    /// arrives and starvation is still a minority of deaths, and no further.
    /// </para>
    /// <para>
    /// <b>⛔ 70 WAS REFUSED THOUGH IT LOOKS BETTER.</b> It is the best row on the fixture — 48
    /// alive, 4 starved over 300 years — and one of the worst on the shipped file: 16 alive, 27
    /// starved. A setting that is excellent on one config and bad on the other is the divergence
    /// that has bitten this project five times (D48, D49, D50, D128, D132), and is a number to
    /// avoid rather than to tune toward.
    /// </para>
    /// </remarks>
    [JsonPropertyName("birth_food_percent")]
    public int BirthFoodPercent { get; init; } = 60;

    /// <summary>Most people one household will hold before it stops growing.</summary>
    [JsonPropertyName("max_household_size")]
    public int MaxHouseholdSize { get; init; } = 5;

    /// <summary>Households the village is founded with.</summary>
    /// <remarks>
    /// Phase 0's lone villager is simply the <c>1 household × 1 adult</c> case rather
    /// than a special mode — which is what keeps the Phase 0 tests meaningful instead
    /// of grandfathered.
    /// </remarks>
    [JsonPropertyName("starting_households")]
    public int StartingHouseholds { get; init; } = 2;

    /// <summary>Founding adults in each starting household.</summary>
    [JsonPropertyName("adults_per_household")]
    public int AdultsPerHousehold { get; init; } = 2;

    /// <summary>Family names for founding households.</summary>
    [JsonPropertyName("household_names")]
    public IReadOnlyList<string> HouseholdNames { get; init; } = new[]
    {
        "Thatcher", "Fletcher", "Cooper", "Mason", "Weaver", "Chandler",
    };

    /// <summary>Names a valley can be settled under, one of which the seed picks.</summary>
    /// <remarks>
    /// <b>A pool, not a name</b>, and the seed indexes it arithmetically — see
    /// <see cref="Core.SimWorld.Name"/> for why it must never be a draw. Content, so a
    /// modder can swap the whole list for their own without touching code, exactly like
    /// <see cref="HouseholdNames"/> and <see cref="VillagerNames"/>.
    /// </remarks>
    [JsonPropertyName("town_names")]
    public IReadOnlyList<string> TownNames { get; init; } = new[]
    {
        "Ashbourne", "Millbrook", "Thornfield", "Elderwood", "Greyford", "Hollowmere",
        "Ravenscar", "Willowdale", "Stonebridge", "Fernhollow", "Larkspur", "Oakhaven",
    };

    /// <summary>Age founding adults start at.</summary>
    [JsonPropertyName("founder_age")]
    public int FounderAge { get; init; } = 20;

    /// <summary>
    /// Age at which a child becomes an adult and can take work.
    /// </summary>
    /// <remarks>
    /// Below this a villager eats from the household store and contributes nothing —
    /// which is exactly why childhood needs households to exist first (D13).
    /// </remarks>
    [JsonPropertyName("adult_age")]
    public int AdultAge { get; init; } = 15;

    /// <summary>
    /// Age up to which the villager works at full strength. After this, vigour
    /// declines linearly toward <see cref="VigourMinPercent"/> at death.
    /// </summary>
    [JsonPropertyName("vigour_full_until_age")]
    public int VigourFullUntilAge { get; init; } = 30;

    /// <summary>
    /// Vigour floor, as a percentage, reached in the final year of life.
    /// </summary>
    /// <remarks>
    /// Tuned so an old villager visibly struggles — many more foraging trips for
    /// the same food — without tipping into starvation. Old age must stay the
    /// normal ending, or the two death arcs stop reading differently, which is
    /// the whole point of the phase.
    /// </remarks>
    [JsonPropertyName("vigour_min_percent")]
    public int VigourMinPercent { get; init; } = 55;

    /// <summary>Median lifespan in years.</summary>
    [JsonPropertyName("lifespan_years_base")]
    public int LifespanYearsBase { get; init; } = 45;

    /// <summary>
    /// Seeded spread around <see cref="LifespanYearsBase"/>, drawn once at birth.
    /// A little variance stops old age landing on a suspiciously round number.
    /// </summary>
    [JsonPropertyName("lifespan_years_variance")]
    public int LifespanYearsVariance { get; init; } = 5;

    /// <summary>
    /// Names to draw from. A villager is "Mabel", never "Villager_01" — the
    /// people-not-spreadsheets non-negotiable starts here (DESIGN.md §1.4).
    /// </summary>
    [JsonPropertyName("villager_names")]
    public IReadOnlyList<string> VillagerNames { get; init; } = new[]
    {
        "Mabel", "Otto", "Bess", "Silas", "Agnes", "Wendell", "Hattie", "Amos",
        "Edith", "Cyrus", "Marta", "Josiah", "Lena", "Ambrose", "Clara", "Ansel",
        "Ruth", "Elias", "Nell", "Barnaby", "Ida", "Gideon", "Prudence", "Ezra",
        "Winifred", "Alden", "Tabitha", "Rufus", "Dorcas", "Hollis", "Verity", "Caleb",
    };

    /// <summary>
    /// Fail loudly on nonsense values rather than letting them cause a baffling
    /// symptom a thousand ticks later.
    /// </summary>
    /// <exception cref="SimConfigException">If any value is out of range.</exception>
    public void Validate()
    {
        if (TicksPerDay <= 0)
        {
            throw new SimConfigException($"ticks_per_day must be greater than zero (got {TicksPerDay}).");
        }

        if (TargetTicksPerSecond <= 0.0 || double.IsNaN(TargetTicksPerSecond) || double.IsInfinity(TargetTicksPerSecond))
        {
            throw new SimConfigException(
                $"target_ticks_per_second must be a positive finite number (got {TargetTicksPerSecond}).");
        }

        if (MaxTicksPerFrame <= 0)
        {
            throw new SimConfigException($"max_ticks_per_frame must be greater than zero (got {MaxTicksPerFrame}).");
        }

        if (DaysPerSeason <= 0)
        {
            throw new SimConfigException($"days_per_season must be greater than zero (got {DaysPerSeason}).");
        }

        if (HungerMax <= 0)
        {
            throw new SimConfigException($"hunger_max must be greater than zero (got {HungerMax}).");
        }

        if (HungerPerTick <= 0)
        {
            throw new SimConfigException($"hunger_per_tick must be greater than zero (got {HungerPerTick}).");
        }

        if (EatThreshold <= 0 || EatThreshold > HungerMax)
        {
            throw new SimConfigException(
                $"eat_threshold must be in 1..hunger_max ({HungerMax}) (got {EatThreshold}).");
        }

        if (EatReducesHunger <= 0)
        {
            throw new SimConfigException($"eat_reduces_hunger must be greater than zero (got {EatReducesHunger}).");
        }

        if (FoodPerMeal <= 0)
        {
            throw new SimConfigException($"food_per_meal must be greater than zero (got {FoodPerMeal}).");
        }

        if (StarvationTicks <= 0)
        {
            throw new SimConfigException($"starvation_ticks must be greater than zero (got {StarvationTicks}).");
        }

        if (GatherYield <= 0)
        {
            throw new SimConfigException($"gather_yield must be greater than zero (got {GatherYield}).");
        }

        if (GatherTicks <= 0)
        {
            throw new SimConfigException($"gather_ticks must be greater than zero (got {GatherTicks}).");
        }

        if (TravelTicksPerUnit <= 0)
        {
            throw new SimConfigException($"travel_ticks_per_unit must be greater than zero (got {TravelTicksPerUnit}).");
        }

        if (StockpileTarget <= 0)
        {
            throw new SimConfigException($"stockpile_target must be greater than zero (got {StockpileTarget}).");
        }

        if (VigourFullUntilAge < 0)
        {
            throw new SimConfigException($"vigour_full_until_age cannot be negative (got {VigourFullUntilAge}).");
        }

        if (VigourMinPercent is <= 0 or > 100)
        {
            throw new SimConfigException($"vigour_min_percent must be in 1..100 (got {VigourMinPercent}).");
        }

        if (LifespanYearsBase <= 0)
        {
            throw new SimConfigException($"lifespan_years_base must be greater than zero (got {LifespanYearsBase}).");
        }

        if (LifespanYearsVariance < 0 || LifespanYearsVariance >= LifespanYearsBase)
        {
            throw new SimConfigException(
                $"lifespan_years_variance must be in 0..lifespan_years_base-1 (got {LifespanYearsVariance}).");
        }

        if (VillagerNames is null || VillagerNames.Count == 0)
        {
            throw new SimConfigException("villager_names must contain at least one name.");
        }

        if (CutYield <= 0)
        {
            throw new SimConfigException($"cut_yield must be greater than zero (got {CutYield}).");
        }

        if (CutTicks <= 0)
        {
            throw new SimConfigException($"cut_ticks must be greater than zero (got {CutTicks}).");
        }

        if (ForesterHutCapacity <= 0)
        {
            throw new SimConfigException($"forester_hut_capacity must be greater than zero (got {ForesterHutCapacity}).");
        }

        if (WoodcutterHutCapacity <= 0)
        {
            throw new SimConfigException(
                $"woodcutter_hut_capacity must be greater than zero (got {WoodcutterHutCapacity}).");
        }

        if (SowTicks <= 0 || ReapTicks <= 0 || CropYieldPerTile <= 0)
        {
            throw new SimConfigException(
                "sow_ticks, reap_ticks and crop_yield_per_tile must all be greater than zero " +
                $"(got {SowTicks}, {ReapTicks}, {CropYieldPerTile}).");
        }

        // A farm that may hold nothing is a farm whose every armful is a walk to the granary,
        // which deletes the buffer the store exists to be. Zero is not "no buffer", it is a
        // number that makes the deposit path unreachable and the guard on it vacuous (D98's
        // rule: a number that is always zero is a lie waiting to be found).
        if (FarmStoreCap <= 0)
        {
            throw new SimConfigException(
                $"farm_store_cap must be greater than zero (got {FarmStoreCap}).");
        }

        if (FarmhouseSeats <= 0)
        {
            throw new SimConfigException(
                $"farmhouse_seats must be greater than zero (got {FarmhouseSeats}) — "
                + "a farm nobody can work is not a building.");
        }

        // ⛔⛔ THE OLD "CAN IT FEED THE FOUNDERS?" GUARD IS GONE, AND DELETING IT IS PART OF THE
        // DECISION RATHER THAN A CASUALTY OF IT (D219).
        //
        // It read `granary_feeds_people >= StartingPopulation` — 30 against 4 — which was
        // trivially true and therefore harmless. Re-expressing it in units was tried first and was
        // WRONG IN AN INSTRUCTIVE WAY: it asked whether the granary could hold a winter's ration
        // per founder, which reintroduced **exactly the coupling this decision removed**, one
        // level up. The capacity stopped depending on the food economy and the validator started
        // depending on it instead.
        //
        // ⚠️ It failed on `Phase0SimTests`, which deliberately sets `FoodPerMeal = 999` to price
        // meals out of reach while it tests the hunger climb: a winter's ration came to 9,439 and
        // a 2,500 granary was refused. *A fixture that is not trying to be a viable village must
        // not be told it is an invalid one.*
        //
        // So the only thing checked here is that the box has a size. **A granary too small for
        // its village is now a legitimate configuration with a visible consequence** — the village
        // stops growing sooner and the player builds another — which is the whole of Joe's ruling:
        // *"it's fine if the granary feeds a different number of people."*
        if (GranaryCapacity < 1)
        {
            throw new SimConfigException(
                $"granary_capacity must be at least 1 (got {GranaryCapacity}) — "
                + "a granary that holds nothing is not a building.");
        }

        if (LogsPerSplit <= 0 || FirewoodPerSplit <= 0 || SplitTicks <= 0)
        {
            throw new SimConfigException(
                $"logs_per_split, firewood_per_split and split_ticks must all be greater than zero " +
                $"(got {LogsPerSplit}, {FirewoodPerSplit}, {SplitTicks}).");
        }

        if (FirewoodBurnIntervalDays < 1)
        {
            throw new SimConfigException(
                "firewood_burn_interval_days must be at least one — a burn cannot happen more "
                + $"than once a day (got {FirewoodBurnIntervalDays}).");
        }

        if (FirewoodPerWinterDay < 0)
        {
            throw new SimConfigException(
                $"firewood_per_winter_day cannot be negative (got {FirewoodPerWinterDay}).");
        }

        // Zero is legal for either — it switches that half of cold off (D45 §4.5), which
        // is how a world with clothing in it gets tested before clothing exists.
        if (ExposureDaysOutdoors < 0 || ExposureDaysSheltered < 0)
        {
            throw new SimConfigException(
                $"exposure_days_outdoors and exposure_days_sheltered cannot be negative " +
                $"(got {ExposureDaysOutdoors}, {ExposureDaysSheltered}).");
        }

        // ⛔ A THAW OF ZERO DAYS IS AN INSTANT RESET, AND THAT WAS BUILT AND REJECTED (D45,
        // D192). Villagers spend 76% of winter at a lit hearth, so a fire that wiped the count
        // meant nobody froze in 120 years. Refused here rather than left as a division by zero
        // somebody discovers as an immortal village.
        if (ThawDaysAtAFire <= 0)
        {
            throw new SimConfigException(
                $"thaw_days_at_a_fire must be greater than zero (got {ThawDaysAtAFire}). "
                + "A fire that thaws instantly is the reset D45 measured and rejected.");
        }

        if (SeekShelterPercent is < 0 or > 100)
        {
            throw new SimConfigException(
                $"seek_shelter_percent must be between 0 and 100 (got {SeekShelterPercent}).");
        }

        if (LogsPerHouse < 0)
        {
            throw new SimConfigException($"logs_per_house cannot be negative (got {LogsPerHouse}).");
        }

        if (LabourReshuffleYears <= 0)
        {
            throw new SimConfigException(
                $"labour_reshuffle_years must be greater than zero (got {LabourReshuffleYears}).");
        }

        // The guards for forage_site_capacity, forage_site_count, tree_stand_count and
        // forage_site_ring_tiles went with their keys. Two of them said something worth
        // keeping — *"a valley with nowhere to forage cannot be lived in"* and *"nothing
        // could be built without timber"* — and both are still true; what changed is that
        // the answer is now `forest_coverage_percent`, which has its own guard below, and a
        // hut the player has to build. **The valley owes the village trees; it no longer
        // owes it jobs.**
        if (SiteJitterTiles < 0)
        {
            throw new SimConfigException(
                $"site_jitter_tiles cannot be negative (got {SiteJitterTiles}).");
        }

        if (MapWidth <= 0 || MapHeight <= 0)
        {
            throw new SimConfigException(
                $"map_width and map_height must both be greater than zero (got {MapWidth}x{MapHeight}).");
        }


        if (EconomyHorizonHouseholds <= 0)
        {
            throw new SimConfigException(
                $"economy_horizon_households must be greater than zero (got {EconomyHorizonHouseholds}).");
        }

        if (WinterBufferPercent < 100)
        {
            throw new SimConfigException(
                $"winter_buffer_percent must be at least 100 (got {WinterBufferPercent}).");
        }

        if (SharingNeedPercent is < 0 or > 100)
        {
            throw new SimConfigException($"sharing_need_percent must be in 0..100 (got {SharingNeedPercent}).");
        }

        if (SharingKeepPercent < SharingNeedPercent || SharingKeepPercent > 100)
        {
            throw new SimConfigException(
                $"sharing_keep_percent must be between sharing_need_percent ({SharingNeedPercent}) and 100 " +
                $"(got {SharingKeepPercent}) - otherwise giving pushes the giver into need.");
        }

        if (DowryPercent is < 0 or > 100)
        {
            throw new SimConfigException($"dowry_percent must be in 0..100 (got {DowryPercent}).");
        }

        if (LeaveHomeAge < 0)
        {
            throw new SimConfigException($"leave_home_age cannot be negative (got {LeaveHomeAge}).");
        }

        if (DependantFoodSharePercent is <= 0 or > 100)
        {
            throw new SimConfigException(
                $"dependant_food_share_percent must be in 1..100 (got {DependantFoodSharePercent}).");
        }

        if (FertilityMinAge < 0 || FertilityMaxAge < FertilityMinAge)
        {
            throw new SimConfigException(
                $"fertility ages must satisfy 0 <= min ({FertilityMinAge}) <= max ({FertilityMaxAge}).");
        }

        if (BirthIntervalYears <= 0)
        {
            throw new SimConfigException($"birth_interval_years must be greater than zero (got {BirthIntervalYears}).");
        }

        if (BirthFoodPercent < 0)
        {
            throw new SimConfigException($"birth_food_percent cannot be negative (got {BirthFoodPercent}).");
        }

        if (MaxHouseholdSize <= 0)
        {
            throw new SimConfigException($"max_household_size must be greater than zero (got {MaxHouseholdSize}).");
        }

        if (StartingHouseholds <= 0)
        {
            throw new SimConfigException($"starting_households must be greater than zero (got {StartingHouseholds}).");
        }

        if (AdultsPerHousehold <= 0)
        {
            throw new SimConfigException($"adults_per_household must be greater than zero (got {AdultsPerHousehold}).");
        }

        if (HouseholdNames is null || HouseholdNames.Count == 0)
        {
            throw new SimConfigException("household_names must contain at least one name.");
        }

        // An empty pool would divide by zero when the seed picks a name, and it would do it
        // at the first frame that drew the header rather than here — METHODOLOGY §4's
        // fail-loudly rule applied where the mistake actually is.
        if (TownNames is null || TownNames.Count == 0)
        {
            throw new SimConfigException("town_names must contain at least one name.");
        }

        if (FounderAge < 0)
        {
            throw new SimConfigException($"founder_age cannot be negative (got {FounderAge}).");
        }

        ValidateGoods();
        ValidateJobs();
        ValidateBuildings();
        ValidateSkills();
        ValidateTechniques();
    }

    /// <summary>Check the techniques catalogue says something the village can actually learn.</summary>
    /// <remarks>
    /// <b>⛔ THE ONE THAT MATTERS IS THE SKILL REFERENCE.</b> A technique pointing at a skill id no
    /// row claims is a technique <b>nobody can ever work out</b> — it would sit at Unknown for
    /// three hundred years, produce no error, and read exactly like a technique whose masters had
    /// simply never appeared. <em>A plausible default, not a crash</em>, which is the shape of
    /// almost every near-miss in this project.
    /// </remarks>
    private void ValidateTechniques()
    {
        if (Techniques is null)
        {
            throw new SimConfigException("techniques must be a list, not null.");
        }

        var seen = new HashSet<int>();
        for (int i = 0; i < Techniques.Count; i++)
        {
            World.TechniqueRow technique = Techniques[i];

            if (technique.Id < 0)
            {
                throw new SimConfigException(
                    $"techniques[{i}] has id {technique.Id}; ids index the catalogue and are "
                    + "hashed, so they cannot be negative.");
            }

            if (!seen.Add(technique.Id))
            {
                throw new SimConfigException(
                    $"techniques[{i}] repeats id {technique.Id}. An id is what the village's "
                    + "knowledge of a technique is stored and hashed under, so two sharing one "
                    + "would be one technique wearing two names.");
            }

            if (string.IsNullOrWhiteSpace(technique.Name))
            {
                throw new SimConfigException(
                    $"techniques[{i}] (id {technique.Id}) has no name.");
            }

            bool anySkillClaimsIt = false;
            for (int s = 0; s < Skills.Count; s++)
            {
                anySkillClaimsIt |= Skills[s].Id == technique.Skill;
            }

            if (!anySkillClaimsIt)
            {
                throw new SimConfigException(
                    $"techniques[{i}] ({technique.Name}) is worked out by skill {technique.Skill}, "
                    + "which no skill row claims. Nobody could ever learn it, and nothing would "
                    + "say so.");
            }

            if (technique.YieldBonusPercent < 0)
            {
                throw new SimConfigException(
                    $"techniques[{i}] ({technique.Name}) has a negative yield bonus. A technique "
                    + "the village would be better off forgetting is not a technique.");
            }

            if (string.IsNullOrWhiteSpace(technique.DiscoveryLine)
                || string.IsNullOrWhiteSpace(technique.LostLine))
            {
                throw new SimConfigException(
                    $"techniques[{i}] ({technique.Name}) is missing a discovery or lost line. "
                    + "Every unlock and every loss owes the player one sentence naming the person "
                    + "(non-negotiable 1); a silent one is the untraceable outcome the design "
                    + "forbids.");
            }
        }
    }

    /// <summary>
    /// The catalogue has to be a catalogue — <b>failing at load rather than at the hash</b>.
    /// </summary>
    /// <remarks>
    /// <b>⚠️ A DUPLICATE OR ZERO ID IS A DESYNC, NOT A TYPO.</b> Ids are what proficiency is
    /// stored and hashed under (§4.1, §8), so two rows sharing one would have two trades writing
    /// the same counter — a village that diverges from itself for a reason no log would name.
    /// **Id 0 is refused** because a default <c>int</c> must never name something (D108).
    /// A modder editing this file is exactly who this message is for.
    /// </remarks>
    /// <summary>
    /// Check the goods catalogue — <b>every failure here is silent and expensive if it ships</b>.
    /// </summary>
    private void ValidateGoods()
    {
        if (GoodsCatalog is null)
        {
            throw new SimConfigException("goods must be a list, not null.");
        }

        var seen = new HashSet<int>();
        int builtIn = System.Enum.GetValues<World.Goods>().Length;

        for (int i = 0; i < GoodsCatalog.Count; i++)
        {
            World.GoodRow good = GoodsCatalog[i];

            if (good.Id < 0)
            {
                throw new SimConfigException(
                    $"goods[{i}] has id {good.Id}; ids index a stockpile and cannot be negative.");
            }

            if (!seen.Add(good.Id))
            {
                throw new SimConfigException(
                    $"goods[{i}] repeats id {good.Id}. Ids are what a stockpile indexes by and "
                    + "what the state hash mixes in order, so two goods sharing one would share "
                    + "a counter.");
            }

            if (string.IsNullOrWhiteSpace(good.Name))
            {
                throw new SimConfigException($"goods[{i}] (id {good.Id}) has no name.");
            }

            // ⛔ A good no store will take can be produced and never put down. That is not a
            // config anybody meant to write, and the symptom — a hauler that never completes an
            // errand — reads as a pathfinding bug rather than a missing line in a data file.
            if (good.StoredBy.Count == 0)
            {
                throw new SimConfigException(
                    $"goods[{i}] ('{good.Name}') names no store kind in stored_by, so nothing "
                    + "in the village could ever hold it.");
            }
        }

        // ⛔ The enum is an alias for the first ids (`goods-catalog.md §2.1`). If the catalogue
        // does not cover them, `Goods.Food` indexes a row that is not there — and it would fail
        // as a null reference deep in the sim rather than here, at load, with a sentence.
        for (int id = 0; id < builtIn; id++)
        {
            if (!seen.Contains(id))
            {
                throw new SimConfigException(
                    $"goods is missing id {id} ({(World.Goods)id}). The built-in goods are named "
                    + "directly by the economy and every golden is pinned to their ids, so a "
                    + "catalogue may add rows above them but may not omit them.");
            }
        }

        // ✅ THE SIX-GOOD CEILING IS LIFTED (D210, slice 1b). Every stockpile in a run is now
        // sized from this catalogue rather than from `Enum.GetValues<Goods>().Length`, and the
        // state hash is bounded by the store's own size — so a seventh good is held, hashed and
        // carried like any other. The check that used to stand here is gone rather than relaxed.

        // ⚠️ The remaining ceiling, raised from 30 to 62 by widening the mask (D210, slice 1b).
        // `StoreBuilding.AllowedGoods` holds one bit per good with the `Spoken` sentinel at bit
        // 62, so a good at 62 or beyond would set the sentinel and a store the player never
        // touched would report that they had — not a crash, a filter that switches itself on.
        //
        // Kept as a guard rather than deleted because 62 is a real edge, not a theoretical one:
        // it is only about twice what the content pass already asks for.
        const int filterCeiling = 62;
        if (seen.Count > filterCeiling)
        {
            throw new SimConfigException(
                $"goods has {seen.Count} rows; the store filter is a 64-bit mask with its "
                + $"sentinel at bit {filterCeiling}, so at most {filterCeiling} goods can exist "
                + "until it is widened again.");
        }
    }

    /// <summary>Check the jobs catalogue (`jobs-catalog.md`, D218).</summary>
    private void ValidateJobs()
    {
        if (JobsCatalog is null)
        {
            throw new SimConfigException("jobs must be a list, not null.");
        }

        var seen = new HashSet<int>();
        for (int i = 0; i < JobsCatalog.Count; i++)
        {
            World.JobRow job = JobsCatalog[i];

            if (job.Id < 0)
            {
                throw new SimConfigException(
                    $"jobs[{i}] has id {job.Id}; ids index the quota and cannot be negative.");
            }

            if (!seen.Add(job.Id))
            {
                throw new SimConfigException(
                    $"jobs[{i}] repeats id {job.Id}. Ids are what a staffing figure is stored "
                    + "and hashed under, so two trades sharing one would share a quota.");
            }

            if (string.IsNullOrWhiteSpace(job.Name) || string.IsNullOrWhiteSpace(job.Plural))
            {
                throw new SimConfigException(
                    $"jobs[{i}] (id {job.Id}) needs both a name and a plural — they are different "
                    + "words for a reason (D188), so neither may be left blank.");
            }
        }

        // The enum is an alias for the first ids, exactly as it is for goods. A missing built-in
        // would index a row that is not there, failing deep in the allocator rather than here.
        int builtIn = System.Enum.GetValues<World.JobKind>().Length;
        for (int id = 0; id < builtIn; id++)
        {
            if (!seen.Contains(id))
            {
                throw new SimConfigException(
                    $"jobs is missing id {id} ({(World.JobKind)id}). The built-in trades are named "
                    + "directly by the allocator and every golden is pinned to their ids, so a "
                    + "catalogue may add rows above them but may not omit them.");
            }
        }
    }

    /// <summary>Check the buildings catalogue says something a village can be built out of.</summary>
    /// <remarks>
    /// <b>⛔ THE THREE THINGS THAT WOULD FAIL SILENTLY OTHERWISE</b>, each of which
    /// `buildings-catalog.md §4` names as a failure mode: a repeated id (two buildings sharing a
    /// name and a recipe), a building that neither stores, employs nor houses anybody (raised, and
    /// then doing nothing for ever), and <b>a null capacity on a row the economy derives nothing
    /// for</b> — which would throw in the middle of a run, on the tick the building was finished,
    /// rather than at load.
    /// </remarks>
    private void ValidateBuildings()
    {
        IReadOnlyList<BuildingRow> rows = BuildingRows;
        if (rows is null)
        {
            throw new SimConfigException("buildings must be a list, not null.");
        }

        var seen = new HashSet<int>();
        for (int i = 0; i < rows.Count; i++)
        {
            BuildingRow row = rows[i];

            if (row.Id < 0)
            {
                throw new SimConfigException(
                    $"buildings[{i}] has id {row.Id}; ids index the catalogue and cannot be "
                    + "negative.");
            }

            if (!seen.Add(row.Id))
            {
                throw new SimConfigException(
                    $"buildings[{i}] repeats id {row.Id}. An id is what a trade's works_at names "
                    + "and what a building is counted under, so two sharing one would be one "
                    + "building wearing two names.");
            }

            if (string.IsNullOrWhiteSpace(row.Name))
            {
                throw new SimConfigException(
                    $"buildings[{i}] (id {row.Id}) has no name. Every building the village raises "
                    + "is named in the log and on the panel, so a blank one is a sentence with a "
                    + "hole in it.");
            }

            if (row.Stores == StoreKind.Cart)
            {
                throw new SimConfigException(
                    $"buildings[{i}] (id {row.Id}) stores as a cart. The cart is the wagon the "
                    + "founders arrive in, not a building anybody puts up.");
            }
        }

        // ⛔ THE CAPACITY CHECK, AND IT IS WHAT MAKES §2.2's EXEMPTION HONEST. A null capacity
        // means *the economy derives this*, and the economy derives it for exactly six built-in
        // buildings. Any other row leaving it null is a building that throws on the tick it is
        // finished — which is the shape of bug this project treats as worse than a crash, because
        // it surfaces in a played run rather than at load.
        for (int i = 0; i < rows.Count; i++)
        {
            BuildingRow row = rows[i];
            var kind = (BuildingKind)row.Id;

            bool economyDerivesTheStore =
                kind is BuildingKind.Shed or BuildingKind.Pile or BuildingKind.Market;
            if (row.Stores is not null && row.StoreCapacity is null && !economyDerivesTheStore)
            {
                throw new SimConfigException(
                    $"buildings[{i}] (id {row.Id}, {row.Name}) is a store with no capacity. Only "
                    + "the shed, the stockpile and the market have one derived for them; every "
                    + "other store must state how much it holds.");
            }

            bool economyDerivesTheSeats =
                kind is BuildingKind.GathererHut or BuildingKind.ForesterHut
                    or BuildingKind.BuilderHut;
            bool anybodyWorksThere = false;
            for (int j = 0; j < JobsCatalog.Count; j++)
            {
                anybodyWorksThere |= (int?)JobsCatalog[j].WorksAt == row.Id;
            }

            if (anybodyWorksThere && row.Seats is null && !economyDerivesTheSeats)
            {
                throw new SimConfigException(
                    $"buildings[{i}] (id {row.Id}, {row.Name}) is a workplace with no seats. Only "
                    + "the gatherer's hut, the forester's hut and the builder's hut have them "
                    + "derived; every other workplace must state how many work there.");
            }

            // ⭐ SHELVES ARE A FOURTH REASON TO EXIST, AND THIS CHECK IS WHY THEY HAD TO BE.
            // The library holds no goods, employs nobody yet and houses nobody — **so this guard
            // refused it**, correctly, before it had a column saying what it was for. *The
            // validator caught that a library needed a reason to exist before the library did*,
            // which is the best argument for having written it.
            if (row.Stores is null && !anybodyWorksThere && row.HouseCapacity <= 0
                && row.Shelves <= 0)
            {
                throw new SimConfigException(
                    $"buildings[{i}] (id {row.Id}, {row.Name}) stores nothing, employs nobody, "
                    + "houses nobody and keeps no records. The village would raise it and it would "
                    + "do nothing for ever.");
            }
        }

        // The enum is an alias for the first ids, exactly as it is for goods and trades. A missing
        // built-in would index a row that is not there, failing deep in a placement rather than
        // here.
        int builtIn = System.Enum.GetValues<BuildingKind>().Length;
        for (int id = 0; id < builtIn; id++)
        {
            if (!seen.Contains(id))
            {
                throw new SimConfigException(
                    $"buildings is missing id {id} ({(BuildingKind)id}). The built-in buildings are "
                    + "named directly by the placement rules and the founding, so a catalogue may "
                    + "add rows above them but may not omit them.");
            }
        }
    }

    private void ValidateSkills()
    {
        if (Skills is null)
        {
            throw new SimConfigException("skills must be a list, not null.");
        }

        var seen = new HashSet<int>();
        for (int i = 0; i < Skills.Count; i++)
        {
            SkillRow skill = Skills[i];

            if (skill.Id <= 0)
            {
                throw new SimConfigException(
                    $"skills[{i}] has id {skill.Id}; ids must be greater than zero, because a "
                    + "default int must never name a skill.");
            }

            if (!seen.Add(skill.Id))
            {
                throw new SimConfigException(
                    $"skills[{i}] repeats id {skill.Id}. Ids are what proficiency is stored and "
                    + "hashed under, so two skills sharing one would share a counter.");
            }

            if (string.IsNullOrWhiteSpace(skill.Name))
            {
                throw new SimConfigException($"skills[{i}] (id {skill.Id}) has no name.");
            }
        }

        if (MasteryYears <= 0)
        {
            throw new SimConfigException(
                $"mastery_years must be greater than zero (got {MasteryYears}).");
        }

        if (SkillWorkPerIdleTick <= 0)
        {
            throw new SimConfigException(
                $"skill_work_per_idle_tick must be greater than zero (got "
                + $"{SkillWorkPerIdleTick}) — a villager who holds a trade is always gaining "
                + "in it, even when the village has nothing for them to do.");
        }

        if (SkillWorkPerActiveTick < SkillWorkPerIdleTick)
        {
            throw new SimConfigException(
                $"skill_work_per_active_tick ({SkillWorkPerActiveTick}) must be at least "
                + $"skill_work_per_idle_tick ({SkillWorkPerIdleTick}), or going out to do the "
                + "work would teach somebody less than staying at home.");
        }
    }

    /// <summary>Founding population. Derived, not configured.</summary>
    [JsonIgnore]
    public int StartingPopulation => StartingHouseholds * AdultsPerHousehold;

    /// <summary>Ticks in one in-game year. Derived, not configured.</summary>
    [JsonIgnore]
    public int TicksPerYear => TicksPerDay * DaysPerSeason * 4;

    /// <summary>Work that makes a master, for a trade that does not state its own years.</summary>
    [JsonIgnore]
    public int MasteryWork => MasteryYears * TicksPerYear * SkillWorkPerIdleTick;

    /// <summary>Years on the task that make a master of <paramref name="skill"/>.</summary>
    /// <remarks>
    /// <b>The trade's own number if it states one, the village's otherwise</b> — see
    /// <see cref="SkillRow.MasteryYears"/>. **No row states one today**, and D182 records the
    /// measurement that removed the reason to.
    /// </remarks>
    public int MasteryYearsFor(SkillRow skill)
    {
        ArgumentNullException.ThrowIfNull(skill);
        return skill.MasteryYears ?? MasteryYears;
    }

    /// <summary>Work that makes a master of <paramref name="skill"/>.</summary>
    public int MasteryWorkFor(SkillRow skill) =>
        MasteryYearsFor(skill) * TicksPerYear * SkillWorkPerIdleTick;

    /// <summary>Ticks in one season. Derived, not configured.</summary>
    [JsonIgnore]
    public int TicksPerSeason => TicksPerDay * DaysPerSeason;

    /// <summary>Ticks outdoors, unclothed, before danger. Derived from days (D45).</summary>
    [JsonIgnore]
    public int ExposureTicksOutdoors => ExposureDaysOutdoors * TicksPerDay;

    /// <summary>Ticks under a roof with no fire before danger. Derived from days (D45).</summary>
    [JsonIgnore]
    public int ExposureTicksSheltered => ExposureDaysSheltered * TicksPerDay;

    /// <summary>
    /// What <see cref="World.Villager.Cold"/> has to reach to kill somebody (D45).
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>One accumulator, two rates</b>, and the product of the two tick-counts is the
    /// scale that makes both rates exact integers for any pair of day-counts a config can
    /// state. No lowest common multiple, no rounding, no float (D2).
    /// </para>
    /// <para>
    /// The alternative — a counter per row of D45's table — leaves a hole in the ordinary
    /// case: a villager who alternates between a fortnight outdoors and a spell in a cold
    /// room trips neither counter and is immortal in conditions that should kill them.
    /// Partial exposure has to add up.
    /// </para>
    /// <para>
    /// Zero when either half is switched off, which callers read as "cold cannot kill".
    /// </para>
    /// </remarks>
    [JsonIgnore]
    public int ExposureThreshold => ExposureTicksOutdoors * ExposureTicksSheltered;

    /// <summary>What a tick outdoors in winter costs. Derived (D45).</summary>
    [JsonIgnore]
    public int ExposurePerTickOutdoors =>
        ExposureTicksOutdoors == 0 ? 0 : ExposureThreshold / ExposureTicksOutdoors;

    /// <summary>What a tick under a fireless roof costs. Derived (D45).</summary>
    [JsonIgnore]
    public int ExposurePerTickSheltered =>
        ExposureTicksSheltered == 0 ? 0 : ExposureThreshold / ExposureTicksSheltered;

    /// <summary>
    /// What a tick beside a burning fire gives back — <b>a day by the fire undoes a day
    /// outdoors</b> (D45 §4.1, Joe's answer (c)).
    /// </summary>
    /// <remarks>
    /// A fire used to zero the count outright, and that was measured before it was built:
    /// villagers spend <b>76% of winter standing at a lit hearth</b>, so the count was
    /// wiped constantly and <em>nobody ever froze in 120 years</em>. Thawing keeps the
    /// sentence true — you never freeze while a fire is burning — without letting one warm
    /// minute erase a fortnight in the snow.
    /// <para>
    /// <b>⭐ IT USED TO MIRROR THE OUTDOOR RATE, ON THE GROUNDS THAT MIRRORING NEEDED NO NUMBER
    /// OF ITS OWN — AND THE NUMBER IT AVOIDED CHOOSING WAS WRONG (Joe, 2026-08-23, D192).</b>
    /// A day by the fire undoing a day outdoors meant **fifteen days at a hearth to come back
    /// from the brink**, which is half a winter spent thawing. Joe, having watched it: *"fire
    /// warm up should be much faster than it currently is."*
    /// </para>
    /// <para>
    /// <b>Now stated as the fact and derived from it</b> (D165's split): <em>a fire brings you
    /// back from the brink in <see cref="ThawDaysAtAFire"/> days.</em> Five, against fifteen
    /// outdoors and twenty-five under a fireless roof — so a hearth is **three times** the
    /// rescue it was, and getting warm is now something you can watch happen rather than
    /// something that takes a season.
    /// </para>
    /// <para>
    /// ⚠️ <b>The old argument's second half still holds and is what bounds this.</b> A fire that
    /// zeroed the count outright was measured and rejected: villagers spend **76% of winter at a
    /// lit hearth**, so the count was wiped constantly and **nobody froze in 120 years**. Five
    /// days is fast, not instant — *faster and it is the reset again wearing a delay.*
    /// </para>
    /// </remarks>
    [JsonIgnore]
    public int ThawPerTickAtAFire =>
        ThawTicksAtAFire == 0 ? 0 : ExposureThreshold / ThawTicksAtAFire;

    /// <summary>Ticks at a fire to come back from the brink. Derived from days.</summary>
    [JsonIgnore]
    public int ThawTicksAtAFire => ThawDaysAtAFire * TicksPerDay;
}

/// <summary>Thrown when config is missing, malformed, or out of range.</summary>
public sealed class SimConfigException : Exception
{
    public SimConfigException(string message) : base(message) { }

    public SimConfigException(string message, Exception innerException) : base(message, innerException) { }
}

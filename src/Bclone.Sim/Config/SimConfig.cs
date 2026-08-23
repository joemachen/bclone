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
    /// What a child eats, as a percentage of an adult's meal.
    /// </summary>
    /// <remarks>
    /// A child eating a full adult portion is not just wrong, it is fatal: two
    /// working adults cannot feed a household of four at full rations, so the
    /// village grew, starved, and died out every time. Children eat less because
    /// they are smaller — and that is what makes raising them survivable.
    /// </remarks>
    [JsonPropertyName("child_food_share_percent")]
    public int ChildFoodSharePercent { get; init; } = 50;

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

    /// <summary>How many people one granary is built to carry through a winter.</summary>
    /// <remarks>
    /// <para>
    /// A fact about the <em>building</em>, like how many hands fit at a berry patch —
    /// which is why it lives here rather than being derived. What it implies is not a
    /// setting: <see cref="World.VillageEconomy.GranaryCapacity"/> turns it into a
    /// quantity of food, and <see cref="World.VillageEconomy.PopulationCeiling"/> turns
    /// that into the size the village stops growing at.
    /// </para>
    /// <para>
    /// <b>This is the number that answers "how big can my village get" — per granary.</b>
    /// The village-wide answer is now "build another one" (D33, D43), which is what turns
    /// this from a ceiling the player is handed into a price they can choose to pay.
    /// </para>
    /// </remarks>
    [JsonPropertyName("granary_feeds_people")]
    public int GranaryFeedsPeople { get; init; } = 30;

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

    /// <summary>Logs a laborer gets from clearing one forest tile (D87).</summary>
    /// <remarks>
    /// <para>
    /// <b>A forest tile is a deposit and this is what is in it</b> (D84) — take it and the
    /// ground is grass. That makes it a genuinely new quantity: a tree stand yields
    /// <c>cut_yield</c> forever, and this yields once.
    /// </para>
    /// <para>
    /// <b>⚠️ It is content today and it becomes derived the moment the tree stand retires.</b>
    /// `building-placement.md §12.8` is explicit that per-tile yield is what the whole timber
    /// economy gets re-derived against, and that is not this slice — while stands still stand,
    /// the brush is an extra source rather than the only one, so nothing hangs off this
    /// number yet. Matching <c>cut_yield</c> is the honest starting point: one tile is one
    /// visit to the stand.
    /// </para>
    /// </remarks>
    [JsonPropertyName("logs_per_forest_tile")]
    public int LogsPerForestTile { get; init; } = 12;

    // ---------------------------------------------------------------
    //  Visible seams (D67, D84, D90)
    // ---------------------------------------------------------------

    /// <summary>Stone a laborer gets from clearing one seam tile (D90).</summary>
    /// <remarks>
    /// A deposit, so this is what is in one tile and taking it leaves grass — the same
    /// shape as <see cref="LogsPerForestTile"/>, and the opposite of a quarry.
    /// </remarks>
    [JsonPropertyName("stone_per_rock_tile")]
    public int StonePerRockTile { get; init; } = 12;

    /// <summary>Iron a laborer gets from clearing one seam tile (D90).</summary>
    /// <remarks>
    /// <b>Less than stone per tile, and there is less of it in the valley</b> — the two
    /// together are what make iron worth walking for rather than merely further away.
    /// </remarks>
    [JsonPropertyName("iron_per_deposit_tile")]
    public int IronPerDepositTile { get; init; } = 8;

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
    [JsonPropertyName("skills")]
    public IReadOnlyList<SkillRow> Skills { get; init; } = new[]
    {
        new SkillRow
        {
            Id = 1,
            Name = "foraging",
            GrownBy = JobKind.Forager,
            YearsPhrase = "in the woods",
            MasteryLine = "{0} has foraged these woods for {1} years. "
                + "Nothing that grows here goes unnoticed now.",
        },
        new SkillRow
        {
            Id = 2,
            Name = "forestry",
            GrownBy = JobKind.Forester,
            YearsPhrase = "among the trees",
            MasteryLine = "{0} has worked these woods for {1} years. "
                + "Where to fell and where to plant takes no thinking about now.",
        },
        new SkillRow
        {
            Id = 3,
            Name = "woodcutting",
            GrownBy = JobKind.Woodcutter,
            YearsPhrase = "at the woodpile",
            MasteryLine = "{0} has split the village's wood for {1} years. "
                + "The grain gives way where it always did.",
        },
        new SkillRow
        {
            Id = 4,
            Name = "farming",
            GrownBy = JobKind.Farmer,
            YearsPhrase = "in the fields",

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
            YearsPhrase = "on the village's frames",
            MasteryLine = "{0} has raised the village's roofs for {1} years. "
                + "The work goes up straight without measuring twice.",
        },
        new SkillRow
        {
            Id = 6,
            Name = "trading",
            GrownBy = JobKind.Marketer,
            YearsPhrase = "on the village's errands",
            MasteryLine = "{0} has carried the village's goods for {1} years. "
                + "Every door and every shortcut is known ground.",
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

    /// <summary>
    /// How many years away from a trade cost one year of it — <b>the decay rate, derived</b>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>⭐ DERIVED AGAINST <see cref="LabourReshuffleYears"/> RATHER THAN PICKED</b> (§3.4,
    /// §12, D16). The village moves people on every three years (D46), so **one full reshuffle
    /// cycle spent elsewhere must cost less than it bought** — otherwise the allocator is the
    /// trap §3.4 forbids, and the player starts fighting a system that exists to save them work.
    /// Three years away for one year lost is the widest rate that clears that bar.
    /// </para>
    /// <para>
    /// <b>And it still makes a career a choice</b>, which is the reason decay exists at all:
    /// master farming in twenty years, then give twenty to forestry, and the farming is back
    /// under mastery. Without that, a fifty-year-old who did six jobs is a master of six and
    /// *"knowledge lives in people"* collapses into *"old people are simply better"*.
    /// </para>
    /// </remarks>
    [JsonPropertyName("skill_decay_years_per_year_lost")]
    public int SkillDecayYearsPerYearLost { get; init; } = 3;

    /// <summary>
    /// The floor decay never takes anybody below, in years — <b>*"not to zero"*, stated</b>.
    /// </summary>
    /// <remarks>
    /// §3.4 says a villager who leaves a trade loses ground **not to zero**. This is that as a
    /// number: **you do not forget a trade you gave a year to.** A floor proportional to some
    /// personal high-water mark was the alternative and it costs a second integer per skill per
    /// villager for a number nobody can read; this is a plain fact about the world, which is
    /// where D165 puts content.
    /// </remarks>
    [JsonPropertyName("skill_floor_years")]
    public int SkillFloorYears { get; init; } = 1;

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

        if (GranaryFeedsPeople < StartingPopulation)
        {
            // A granary too small for the village it is founded with is not a pressure,
            // it is an execution: the birth gate would be shut from tick one and the
            // founders would age out having had no children at all.
            throw new SimConfigException(
                $"granary_feeds_people ({GranaryFeedsPeople}) is below the {StartingPopulation} " +
                "founders — the village could never grow at all.");
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

        if (ChildFoodSharePercent is <= 0 or > 100)
        {
            throw new SimConfigException(
                $"child_food_share_percent must be in 1..100 (got {ChildFoodSharePercent}).");
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

        ValidateSkills();
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

        if (SkillDecayYearsPerYearLost <= 0)
        {
            throw new SimConfigException(
                "skill_decay_years_per_year_lost must be greater than zero (got "
                + $"{SkillDecayYearsPerYearLost}).");
        }

        if (SkillFloorYears < 0)
        {
            throw new SimConfigException(
                $"skill_floor_years cannot be negative (got {SkillFloorYears}).");
        }

        if (SkillFloorYears >= MasteryYears)
        {
            throw new SimConfigException(
                $"skill_floor_years ({SkillFloorYears}) must be below mastery_years "
                + $"({MasteryYears}), or decay could never take anybody out of mastery and a "
                + "career would stop being a choice.");
        }
    }

    /// <summary>Founding population. Derived, not configured.</summary>
    [JsonIgnore]
    public int StartingPopulation => StartingHouseholds * AdultsPerHousehold;

    /// <summary>Ticks in one in-game year. Derived, not configured.</summary>
    [JsonIgnore]
    public int TicksPerYear => TicksPerDay * DaysPerSeason * 4;

    /// <summary>Ticks on the task before mastery. Derived from a stated number of years.</summary>
    [JsonIgnore]
    public int MasteryTicks => MasteryYears * TicksPerYear;

    /// <summary>The ticks decay never takes anybody below. Derived (§3.4's *"not to zero"*).</summary>
    [JsonIgnore]
    public int SkillFloorTicks => SkillFloorYears * TicksPerYear;

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
    /// minute erase a fortnight in the snow. Mirroring the outdoor rate is the only
    /// choice that needs no number of its own: slower and a hearth is not really safety,
    /// faster and it is the reset again wearing a delay.
    /// </remarks>
    [JsonIgnore]
    public int ThawPerTickAtAFire => ExposurePerTickOutdoors;
}

/// <summary>Thrown when config is missing, malformed, or out of range.</summary>
public sealed class SimConfigException : Exception
{
    public SimConfigException(string message) : base(message) { }

    public SimConfigException(string message, Exception innerException) : base(message, innerException) { }
}

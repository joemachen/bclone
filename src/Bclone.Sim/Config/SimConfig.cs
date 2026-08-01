using System.Text.Json.Serialization;

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

    /// <summary>How many people can work one tree stand at once.</summary>
    /// <remarks>
    /// A local fact about the place, not a statement about the village. What the
    /// village needs cut is decided by <c>LabourQuota</c>, which will happily leave
    /// this capacity unfilled in a year when there are barely enough hands to eat.
    /// </remarks>
    [JsonPropertyName("tree_stand_capacity")]
    public int TreeStandCapacity { get; init; } = 3;

    // ---------------------------------------------------------------
    //  Firewood (D29) — the woodcutter's hut
    // ---------------------------------------------------------------

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

    /// <summary>How many builders fit on one construction site.</summary>
    [JsonPropertyName("construction_site_capacity")]
    public int ConstructionSiteCapacity { get; init; } = 3;

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

    /// <summary>How many people can work one forage site at once.</summary>
    /// <remarks>
    /// <para>
    /// A patch of berries only has so many berries within arm's reach. This is the
    /// reason a growing village eventually has to find more sites rather than
    /// crowding the one it started beside — and, since it is a local number, the
    /// reason it can be said in one sentence when someone is turned away.
    /// </para>
    /// <para>
    /// It is emphatically <em>not</em> "how many foragers the village needs". That
    /// question is village-level and is answered by <c>LabourQuota</c>; putting it
    /// here is the mistake <c>specs/labour-allocation.md §3</c> is a record of.
    /// </para>
    /// </remarks>
    [JsonPropertyName("forage_site_capacity")]
    public int ForageSiteCapacity { get; init; } = 4;

    // ---------------------------------------------------------------
    //  Map generation (D18) — rules, not outcomes
    // ---------------------------------------------------------------

    /// <summary>How many forage sites the valley gets.</summary>
    /// <remarks>
    /// These replaced the literal coordinates the world used to be typed in as. A
    /// modder controls the <em>rules</em> of a valley — how many sites, how far out,
    /// how wide the river — rather than the outcome, which is the honest data-driven
    /// form once the world is generated.
    /// </remarks>
    [JsonPropertyName("forage_site_count")]
    public int ForageSiteCount { get; init; } = 6;

    /// <summary>
    /// How far out the ring of forage sites sits, in tiles.
    /// </summary>
    /// <remarks>
    /// <b>The economy reads this, which is why generation is bounded rather than
    /// checked.</b> <see cref="World.VillageEconomy"/> derives the food economy from how
    /// far the worst-placed home is from its nearest site, so a generator free to put
    /// sites anywhere would make the food economy a property of the seed. Drawing
    /// within a stated radius keeps one economy for every seed — which is what makes a
    /// shared seed comparable — and the distance budget then holds by construction
    /// rather than by a reject-and-redraw loop.
    /// </remarks>
    [JsonPropertyName("forage_site_ring_tiles")]
    public int ForageSiteRingTiles { get; init; } = 5;

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

    /// <summary>How many stands of trees the valley gets.</summary>
    [JsonPropertyName("tree_stand_count")]
    public int TreeStandCount { get; init; } = 2;

    /// <summary>How far out the tree stands sit.</summary>
    [JsonPropertyName("tree_stand_ring_tiles")]
    public int TreeStandRingTiles { get; init; } = 4;

    /// <summary>How big a patch of forest a stand paints.</summary>
    [JsonPropertyName("tree_stand_radius_tiles")]
    public int TreeStandRadiusTiles { get; init; } = 3;

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

    /// <summary>Poorest ground the generator will produce, 0–255. Reserved for §2.3.</summary>
    [JsonPropertyName("soil_quality_min")]
    public int SoilQualityMin { get; init; } = 40;

    /// <summary>Best ground the generator will produce, 0–255. Reserved for §2.3.</summary>
    [JsonPropertyName("soil_quality_max")]
    public int SoilQualityMax { get; init; } = 200;

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
    /// How far, in tiles, it is reasonable to travel to forage.
    /// </summary>
    /// <remarks>
    /// This is the "villager who walks across the map for one log" guard from
    /// DESIGN.md §2.2. Stored in tiles for readability and converted to travel cost,
    /// because catchment is measured in cost - that is what lets a worn path widen a
    /// workplace's reach later without catchment knowing roads exist.
    /// </remarks>
    /// <remarks>
    /// Ten, lowered from twelve once the forage sites were spread properly. At twelve
    /// a home in the middle of the village could reach very nearly everything, so the
    /// rule constrained almost nothing; at ten no home reaches every workplace and
    /// outlying households really are restricted to what is near them. Lower than ten
    /// still kills the village, and the cause is timber rather than food — see
    /// <c>specs/labour-allocation.md §8</c>.
    /// </remarks>
    [JsonPropertyName("forager_catchment_tiles")]
    public int ForagerCatchmentTiles { get; init; } = 10;

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
    [JsonPropertyName("birth_food_percent")]
    public int BirthFoodPercent { get; init; } = 80;

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

        if (TreeStandCapacity <= 0)
        {
            throw new SimConfigException($"tree_stand_capacity must be greater than zero (got {TreeStandCapacity}).");
        }

        if (WoodcutterHutCapacity <= 0)
        {
            throw new SimConfigException(
                $"woodcutter_hut_capacity must be greater than zero (got {WoodcutterHutCapacity}).");
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

        if (ForageSiteCapacity <= 0)
        {
            throw new SimConfigException(
                $"forage_site_capacity must be greater than zero (got {ForageSiteCapacity}).");
        }

        if (LabourReshuffleYears <= 0)
        {
            throw new SimConfigException(
                $"labour_reshuffle_years must be greater than zero (got {LabourReshuffleYears}).");
        }

        if (ForageSiteCount <= 0)
        {
            throw new SimConfigException(
                $"forage_site_count must be at least one (got {ForageSiteCount}) — a valley with " +
                "nowhere to forage cannot be lived in.");
        }

        if (TreeStandCount <= 0)
        {
            throw new SimConfigException(
                $"tree_stand_count must be at least one (got {TreeStandCount}) — nothing could be " +
                "built without timber.");
        }

        if (ForageSiteRingTiles <= 0 || SiteJitterTiles < 0)
        {
            throw new SimConfigException(
                $"forage_site_ring_tiles must be positive and site_jitter_tiles non-negative " +
                $"(got {ForageSiteRingTiles}, {SiteJitterTiles}).");
        }

        if (MapWidth <= 0 || MapHeight <= 0)
        {
            throw new SimConfigException(
                $"map_width and map_height must both be greater than zero (got {MapWidth}x{MapHeight}).");
        }

        if (ForagerCatchmentTiles <= 0)
        {
            throw new SimConfigException(
                $"forager_catchment_tiles must be greater than zero (got {ForagerCatchmentTiles}).");
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

        if (FounderAge < 0)
        {
            throw new SimConfigException($"founder_age cannot be negative (got {FounderAge}).");
        }
    }

    /// <summary>Founding population. Derived, not configured.</summary>
    [JsonIgnore]
    public int StartingPopulation => StartingHouseholds * AdultsPerHousehold;

    /// <summary>Ticks in one in-game year. Derived, not configured.</summary>
    [JsonIgnore]
    public int TicksPerYear => TicksPerDay * DaysPerSeason * 4;

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

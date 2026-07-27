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
    [JsonPropertyName("hunger_per_tick")]
    public int HungerPerTick { get; init; } = 10;

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

    /// <summary>Where food is foraged.</summary>
    [JsonPropertyName("food_source_x")]
    public int FoodSourceX { get; init; } = 5;

    /// <summary>Where food is foraged.</summary>
    [JsonPropertyName("food_source_y")]
    public int FoodSourceY { get; init; }

    /// <summary>
    /// Further forage sites, beyond the first.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Not content dressing. The measured finding was that a <em>binding</em>
    /// catchment radius starves outlying households when there is only one food
    /// source — no amount of economic slack helps, because those families have
    /// nothing within reach to work. Several sources spread around the valley are the
    /// prerequisite for §2.2's catchment rule constraining anything at all (D19).
    /// </para>
    /// <para>
    /// <b>A ring around the homes, plus two further out.</b> The first attempt put all
    /// three sites out at the edges, which left every home near the middle of the
    /// village competing for the one original berry patch — so tightening catchment
    /// simply left people idle beside a full patch, and their households starved. A
    /// site in each direction at roughly the width of the settlement means every home
    /// has somewhere close; the two distant ones are what a growing village spreads
    /// toward. Measured: with these positions the village survives a catchment of 10
    /// tiles, against 12 for the edge-only layout.
    /// </para>
    /// <para>
    /// These become generator output once the map is seeded (D18); the literal
    /// coordinates are the placeholder.
    /// </para>
    /// </remarks>
    [JsonPropertyName("extra_forage_sites")]
    public IReadOnlyList<SitePosition> ExtraForageSites { get; init; } = new[]
    {
        new SitePosition { X = -6, Y = 0 },
        new SitePosition { X = 0, Y = 6 },
        new SitePosition { X = 0, Y = -6 },
        new SitePosition { X = -7, Y = -6 },
        new SitePosition { X = 7, Y = 7 },
    };

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

    /// <summary>Where timber is cut.</summary>
    [JsonPropertyName("tree_stand_x")]
    public int TreeStandX { get; init; } = -4;

    /// <summary>Where timber is cut.</summary>
    [JsonPropertyName("tree_stand_y")]
    public int TreeStandY { get; init; } = 2;

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
    /// Consecutive ticks in an unheated home before someone freezes.
    /// </summary>
    /// <remarks>
    /// Longer than <see cref="StarvationTicks"/> on purpose. Firewood is made by a
    /// two-step chain and hunger by a one-step one, so a household can be short of
    /// fuel for reasons further away from anything it controls — and the village
    /// needs time to notice and put hands back on the hut. A cold snap that killed as
    /// fast as famine would give nobody a chance to respond, which is the difference
    /// between pressure and a coin flip.
    /// </remarks>
    [JsonPropertyName("freezing_ticks")]
    public int FreezingTicks { get; init; } = 40;

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
    /// Once a year, not once a season. A seasonal reshuffle churns jobs fast enough
    /// that the stated reason for holding one goes stale before the player reads it,
    /// and a reshuffle that cannot explain itself is worse than no reshuffle.
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
    public int EconomyHorizonHouseholds { get; init; } = 12;

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
    /// A household is considered in need below this percentage of its food target.
    /// </summary>
    [JsonPropertyName("sharing_need_percent")]
    public int SharingNeedPercent { get; init; } = 50;

    /// <summary>
    /// A household keeps at least this percentage of its own target before giving
    /// anything away.
    /// </summary>
    /// <remarks>
    /// Set above <see cref="SharingNeedPercent"/> so generosity can never push a
    /// giver into the state it is trying to relieve.
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

    /// <summary>Tiles between neighbouring founding homes.</summary>
    [JsonPropertyName("household_spacing")]
    public int HouseholdSpacing { get; init; } = 3;

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

        if (FreezingTicks <= 0)
        {
            throw new SimConfigException($"freezing_ticks must be greater than zero (got {FreezingTicks}).");
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

        if (ExtraForageSites is null)
        {
            throw new SimConfigException("extra_forage_sites must be a list, even if an empty one.");
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

        if (HouseholdSpacing <= 0)
        {
            throw new SimConfigException($"household_spacing must be greater than zero (got {HouseholdSpacing}).");
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
}

/// <summary>Thrown when config is missing, malformed, or out of range.</summary>
public sealed class SimConfigException : Exception
{
    public SimConfigException(string message) : base(message) { }

    public SimConfigException(string message, Exception innerException) : base(message, innerException) { }
}

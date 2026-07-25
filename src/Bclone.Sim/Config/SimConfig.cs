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
    public double TargetTicksPerSecond { get; init; } = 10.0;

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

    /// <summary>Stockpile level at which the villager stops foraging and rests.</summary>
    [JsonPropertyName("stockpile_target")]
    public int StockpileTarget { get; init; } = 60;

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

    // ---------------------------------------------------------------
    //  Life
    // ---------------------------------------------------------------

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
    public int LifespanYearsBase { get; init; } = 52;

    /// <summary>
    /// Seeded spread around <see cref="LifespanYearsBase"/>, drawn once at birth.
    /// A little variance stops old age landing on a suspiciously round number.
    /// </summary>
    [JsonPropertyName("lifespan_years_variance")]
    public int LifespanYearsVariance { get; init; } = 6;

    /// <summary>
    /// Names to draw from. A villager is "Mabel", never "Villager_01" — the
    /// people-not-spreadsheets non-negotiable starts here (DESIGN.md §1.4).
    /// </summary>
    [JsonPropertyName("villager_names")]
    public IReadOnlyList<string> VillagerNames { get; init; } = new[]
    {
        "Mabel", "Otto", "Bess", "Silas", "Agnes", "Wendell", "Hattie", "Amos",
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
    }

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

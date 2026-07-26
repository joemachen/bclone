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

        // Budget for the furthest home the village will plausibly build, not the
        // first one. Deriving the economy from household #1 made every outlying
        // family a rounding error that starved.
        var source = new GridPos(config.FoodSourceX, config.FoodSourceY);
        GridPos furthest = Household.PlacementFor(
            config.EconomyHorizonHouseholds, config.HomeX, config.HomeY, config.HouseholdSpacing);

        int travel = furthest.ManhattanDistanceTo(source) * config.TravelTicksPerUnit;
        return (travel * 2) + config.GatherTicks;
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

    /// <summary>Food one adult gathers in a year at a given vigour.</summary>
    public static int FoodGatheredPerYear(SimConfig config, int vigourPercent) =>
        TripsPerYear(config) * config.GatherYield * vigourPercent / 100;

    /// <summary>Food one adult gathers in a year at their weakest.</summary>
    public static int FoodGatheredPerYearAtWorst(SimConfig config) =>
        FoodGatheredPerYear(config, config.VigourMinPercent);

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

    /// <summary>A one-line summary for logs and tests.</summary>
    public static string Describe(SimConfig config) =>
        $"adult eats {AdultFoodPerYear(config)}/yr, child {ChildFoodPerYear(config)}/yr; " +
        $"{TripsPerYear(config)} trips/yr yields {FoodGatheredPerYearAtWorst(config)}/yr at worst " +
        $"({config.VigourMinPercent}% vigour) => supports {DependantsSupportedAtWorst(config)} dependants " +
        $"(target {RequiredDependants}); required yield {RequiredGatherYield(config)}, configured {config.GatherYield}.";
}

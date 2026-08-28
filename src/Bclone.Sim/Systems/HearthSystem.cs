using Bclone.Sim.Config;
using Bclone.Sim.Core;
using Bclone.Sim.Logging;
using Bclone.Sim.World;

namespace Bclone.Sim.Systems;

/// <summary>
/// Homes burn firewood through winter, and the people in a cold one start to freeze.
/// </summary>
/// <remarks>
/// <para>
/// The second way winter can kill (D17, D29). Phase 0 refused this outright — *"winter's
/// danger is food scarcity only; do not add a second overlapping death system"* — and
/// reversing it is deliberate, now that there are households to heat and a labour
/// system for fuel to compete inside. The half of that reasoning which has <b>not</b>
/// expired is legibility: a death must never be ambiguous between cold and hunger.
/// <see cref="MortalitySystem"/> is where that promise is kept.
/// </para>
/// <para>
/// <b>Burning is per household and per day.</b> Per household because a house costs the
/// same to heat whether two live in it or five — which makes sprawl the thing that
/// costs, rather than population, and ties the pressure back to a decision rather than
/// to growth. Per day because fuel is measured in whole logs and a tick is a quarter of
/// a day; charging a fraction of a log per tick would need either floats (D2 forbids
/// them) or a rounding rule nobody could read off the screen.
/// </para>
/// </remarks>
public sealed class HearthSystem : ISimSystem
{
    public string Name => "hearth";

    public void Execute(SimWorld world)
    {
        ArgumentNullException.ThrowIfNull(world);

        SimConfig config = world.Config;

        // Fuel switched off entirely.
        //
        // Not a convenience: Phase 0 is the one-villager slice and its spec rules
        // warmth out by name — "winter's danger is food scarcity only; do not add a
        // second overlapping death system". A lone villager who must both feed
        // themselves and keep a fire going is precisely the double jeopardy that spec
        // refused, and switching fuel on under its fixtures killed them. The fixture
        // should encode the world its tests describe.
        if (config.FirewoodPerWinterDay <= 0)
        {
            ClearTheCold(world);
            return;
        }

        // Nothing to do outside winter — and, crucially, cold does not accumulate
        // either. Spring resets everyone, so a household that scraped through
        // February is not still dying of it in May.
        if (!IsHeatingSeason(world.Clock.Season))
        {
            ClearTheCold(world);
            return;
        }

        // Every few days rather than every day (Joe: *"make firewood consumption take
        // longer, like 4x longer"*). The interval carries the rate because sim state is
        // integer-only and a quarter of a log is not a number this game can hold (D2).
        ulong burnEvery = (ulong)config.TicksPerDay * (ulong)config.FirewoodBurnIntervalDays;
        if (world.Tick % burnEvery == 0UL)
        {
            BurnADaysFirewood(world, config);
        }

        ChillTheUnsheltered(world, config);
    }

    /// <summary>Winter only, for now. Shoulder seasons are a config change away.</summary>
    private static bool IsHeatingSeason(Season season) => season == Season.Winter;

    private static void BurnADaysFirewood(SimWorld world, SimConfig config)
    {
        for (int i = 0; i < world.Households.Count; i++)
        {
            Household household = world.Households[i];
            if (world.LivingMembersOf(household) == 0)
            {
                continue;
            }

            if (!household.Stockpile.TryTake(Goods.Firewood, config.FirewoodPerWinterDay))
            {
                continue;
            }

            // Warned, not surprised. The line lands when the last log goes on the
            // fire, not when somebody dies of its absence — the same principle §2.7
            // states about knowledge at risk: a foreseeable loss has to be visible
            // and actionable, or it reads as unfair rather than as a consequence.
            if (household.Stockpile.Firewood == 0)
            {
                world.Narrate(
                    $"The {household.Name} household put its last firewood on the fire " +
                    $"— {world.Clock.SeasonAndYear()}.", LogCategory.Warning);
            }
        }
    }

    /// <summary>
    /// Cold, counted from where each villager is standing (D45).
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>This is the half of the old system that D45 replaced.</b> It used to ask one
    /// question — does this villager's household have firewood? — of everyone, everywhere,
    /// identically, so a villager froze because of a number attached to their family
    /// rather than because of anything you could watch. Now the tile answers it: open
    /// ground costs the most, a roof with no hearth costs less, and a burning fire gives
    /// it back.
    /// </para>
    /// <para>
    /// One accumulator with three rates rather than a counter per state — see
    /// <see cref="SimConfig.ExposureThreshold"/> for why a counter per state leaves a
    /// villager who alternates immortal.
    /// </para>
    /// </remarks>
    private static void ChillTheUnsheltered(SimWorld world, SimConfig config)
    {
        int threshold = config.ExposureThreshold;
        if (threshold <= 0)
        {
            ClearTheCold(world);
            return;
        }

        int warnAt = threshold * config.SeekShelterPercent / 100;

        for (int i = 0; i < world.Villagers.Count; i++)
        {
            Villager villager = world.Villagers[i];
            if (!villager.Alive)
            {
                continue;
            }

            int before = villager.Cold;

            villager.Cold += world.ShelterAt(villager.Position) switch
            {
                Shelter.Fire => -config.ThawPerTickAtAFire,
                Shelter.Roof => config.ExposurePerTickSheltered,
                _ => config.ExposurePerTickOutdoors,
            };

            // Clamped both ends. Below zero would bank warmth against next winter, which
            // is not a thing a body does; above the threshold is death and is
            // MortalitySystem's to read, so there is nothing to gain by counting past it.
            if (villager.Cold < 0)
            {
                villager.Cold = 0;
            }
            else if (villager.Cold > threshold)
            {
                villager.Cold = threshold;
            }

            // Once per episode, on the way UP through the line where they break off work
            // to get warm — so the sentence a player reads and the decision the villager
            // makes are the same moment. Crossing it downward is somebody thawing out,
            // and saying it again then would train them to ignore it.
            if (before < warnAt && villager.Cold >= warnAt && warnAt > 0)
            {
                world.Narrate(
                    $"{villager.Name} is dangerously cold and is going in to get warm " +
                    $"— {world.Clock.SeasonAndYear()}.", LogCategory.Warning);
            }
        }
    }

    /// <summary>
    /// Spring thaws everyone.
    /// </summary>
    /// <remarks>
    /// Cold is a <em>winter</em> condition, so it cannot carry over. Without this a
    /// villager who ended winter half-frozen would still be counting toward death in
    /// midsummer, which is neither survivable nor explicable.
    /// </remarks>
    private static void ClearTheCold(SimWorld world)
    {
        for (int i = 0; i < world.Villagers.Count; i++)
        {
            world.Villagers[i].Cold = 0;
        }
    }
}

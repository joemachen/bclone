using Bclone.Sim.Config;
using Bclone.Sim.Core;
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

        if (world.Tick % (ulong)config.TicksPerDay == 0UL)
        {
            ShareFirewood(world, config);
            BurnADaysFirewood(world, config);
        }

        ChillTheUnheated(world, config);
    }

    /// <summary>
    /// Firewood is issued from the shed to the homes that need it, every day of winter.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Burning could not ship without this, and finding that out cost a run.</b>
    /// A woodcutter carries their firewood home, so with sharing switched off the
    /// entire village's fuel sits in whichever house the woodcutter happens to live
    /// in. The first winter killed both members of the other founding household while
    /// eighty-one firewood sat next door. That is not a pressure the player can
    /// respond to, it is a lottery on where one worker was born — and it is the same
    /// shape as D25, where logs piled up in the logger's house and no home was ever
    /// built.
    /// </para>
    /// <para>
    /// <b>Daily, where food sharing is seasonal.</b> The cadences differ because the
    /// resources do: most households gather their own food, so sharing it is an
    /// occasional correction. Firewood is made by one or two specialists <em>for
    /// everybody</em>, so it has to flow continuously or it never reaches anyone.
    /// Freezing takes ten days and a season is fifteen; a seasonal top-up would
    /// arrive after the funeral.
    /// </para>
    /// <para>
    /// Like the food policy, this is <b>a placeholder for a building</b> — the market
    /// D14 describes distributes goods as well as food, and should delete both.
    /// </para>
    /// </remarks>
    private static void ShareFirewood(SimWorld world, SimConfig config)
    {
        int target = VillageEconomy.FirewoodStoreWantedPerHousehold(config);
        Stockpile shed = world.StorageShed.Store;

        // Households in id order, so who is served first when the shed runs low is a
        // fact about the village rather than about iteration.
        for (int i = 0; i < world.Households.Count; i++)
        {
            Household household = world.Households[i];
            if (world.LivingMembersOf(household) == 0)
            {
                continue;
            }

            int wanted = target - household.Stockpile.Firewood;
            if (wanted <= 0)
            {
                continue;
            }

            int given = shed.Firewood < wanted ? shed.Firewood : wanted;
            if (given > 0 && shed.TryTakeFirewood(given))
            {
                household.Stockpile.Receive(0, 0, given);
            }
        }
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

            if (!household.Stockpile.TryTakeFirewood(config.FirewoodPerWinterDay))
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
                    $"— {world.Clock.SeasonAndYear()}.");
            }
        }
    }

    private static void ChillTheUnheated(SimWorld world, SimConfig config)
    {
        for (int i = 0; i < world.Villagers.Count; i++)
        {
            Villager villager = world.Villagers[i];
            if (!villager.Alive)
            {
                continue;
            }

            if (world.HouseholdOf(villager).Stockpile.Firewood > 0)
            {
                villager.TicksCold = 0;
                continue;
            }

            villager.TicksCold++;

            // Once per household per episode, at the moment the cold starts to be
            // dangerous rather than merely uncomfortable — halfway to killing them.
            if (villager.TicksCold == config.FreezingTicks / 2)
            {
                world.Narrate(
                    $"{villager.Name} is cold — the {world.HouseholdOf(villager).Name} household " +
                    $"has had no firewood for days. {world.Clock.SeasonAndYear()}.");
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
            world.Villagers[i].TicksCold = 0;
        }
    }
}

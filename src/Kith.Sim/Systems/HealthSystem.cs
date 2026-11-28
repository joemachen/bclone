using Kith.Sim.Config;
using Kith.Sim.Core;
using Kith.Sim.World;

namespace Kith.Sim.Systems;

/// <summary>
/// ⭐ W6, health (`specs/health.md`): the body. At a season's turn, what each person ate is read — a season one food group made
/// <c>health_plain_percent</c> (90) of is <i>plain</i> — and the plain seasons behind them are counted (§5.2–5.3).
/// </summary>
/// <remarks>
/// <para>
/// ⛔ <b>D207's two refusals, held here too:</b> nothing in this system slows anyone's work, and a diet erodes from
/// <b>variety, never quantity</b> — a hungry village is not made sicker by being hungry, so hunger never makes anyone ill (the
/// other way it costs work: a few days in bed a year, ). ⛔ <b>One cause, one system (D797):</b> it reads what people ate and nothing of contentment's.
/// </para>
/// <para>
/// Step 6d of the tick order, after the hearths and the herds and before anyone decides what to do: a meal eaten on the
/// turn's own tick is the new season's.
/// </para>
/// </remarks>
public sealed class HealthSystem : ISimSystem
{
    public string Name => "health";

    public void Execute(SimWorld world)
    {
        ArgumentNullException.ThrowIfNull(world);

        SimConfig config = world.Config;
        if (!config.HealthOn || world.Tick == 0 || world.Tick % (ulong)config.TicksPerDay != 0UL)
        {
            return;
        }

        if (world.Tick % ((ulong)config.TicksPerDay * (ulong)config.DaysPerSeason) == 0UL)
        {
            ReadTheSeason(world, config);
        }

        FallIll(world, config);
    }

    /// <summary>
    /// ⭐ Each day, a run-down person who is not already ill falls ill with a chance of one in
    /// <c>health_fall_ill_one_in_days</c> (§5.5): <see cref="Villager.SickDays"/> is set and they go home to bed when they next
    /// choose what to do. In id order on the world's seeded generator, and a draw only for the run down — so a village with
    /// nobody run down draws nothing. ⛔ Never fatal in piece 1.
    /// </summary>
    private static void FallIll(SimWorld world, SimConfig config)
    {
        for (int i = 0; i < world.Villagers.Count; i++)
        {
            Villager villager = world.Villagers[i];
            if (!villager.Alive || villager.SickDays > 0 || world.HealthOf(villager) != HealthState.RunDown)
            {
                continue;
            }

            if (world.Rng.NextInt(0, config.HealthFallIllOneInDays) == 0)
            {
                villager.SickDays = config.HealthSickDays;
                world.SayTheyFellIll(villager);
            }
        }
    }

    /// <summary>
    /// The season's read: plain adds one, varied takes <c>health_recover_seasons</c> off (never below nought), too few meals
    /// leave the count as it was; then the plates are cleared. ⭐ The village is told the first time anyone reaches the plain
    /// diet, and the first time anyone is run down — said on the turn it happens, derived from the turn's before and after, so
    /// nothing is latched.
    /// </summary>
    private static void ReadTheSeason(SimWorld world, SimConfig config)
    {
        int plainBefore = 0, runDownBefore = 0, plainAfter = 0, runDownAfter = 0;
        var newlyPlain = new List<Villager>();
        var newlyRunDown = new List<Villager>();
        for (int i = 0; i < world.Villagers.Count; i++)
        {
            Villager villager = world.Villagers[i];
            if (!villager.Alive)
            {
                continue;
            }

            int was = villager.PlainSeasons;
            plainBefore += was >= config.HealthPlainDietSeasons ? 1 : 0;
            runDownBefore += was >= config.HealthRunDownSeasons ? 1 : 0;

            if (villager.MealsThisSeason >= config.HealthReadMinMeals)
            {
                (FoodGroup top, bool plain) = PlateOf(villager, config);
                if (plain)
                {
                    villager.PlainSeasons++;
                    villager.PlainOn = top;
                }
                else
                {
                    villager.PlainSeasons = Math.Max(0, villager.PlainSeasons - config.HealthRecoverSeasons);
                }
            }

            int now = villager.PlainSeasons;
            plainAfter += now >= config.HealthPlainDietSeasons ? 1 : 0;
            runDownAfter += now >= config.HealthRunDownSeasons ? 1 : 0;
            if (was < config.HealthPlainDietSeasons && now >= config.HealthPlainDietSeasons)
            {
                newlyPlain.Add(villager);
            }

            if (was < config.HealthRunDownSeasons && now >= config.HealthRunDownSeasons)
            {
                newlyRunDown.Add(villager);
            }

            Array.Clear(villager.AteThisSeason);
            villager.MealsThisSeason = 0;
        }

        if (plainBefore == 0 && newlyPlain.Count > 0)
        {
            world.SayThePlainDiet(plainAfter, newlyPlain);
        }

        if (runDownBefore == 0 && newlyRunDown.Count > 0)
        {
            world.SayTheFirstRunDown(runDownAfter, newlyRunDown);
        }
    }

    /// <summary>The group most of the season's plate was, and whether it made <c>health_plain_percent</c> of it.</summary>
    internal static (FoodGroup Top, bool Plain) PlateOf(Villager villager, SimConfig config)
    {
        int[] ate = villager.AteThisSeason;
        long sum = 0;
        int topSlot = 0;
        for (int g = 0; g < ate.Length; g++)
        {
            sum += ate[g];
            if (ate[g] > ate[topSlot])
            {
                topSlot = g;
            }
        }

        FoodGroup top = FoodGroups.All[topSlot];
        return (top, sum > 0 && ate[topSlot] * 100L >= sum * config.HealthPlainPercent);
    }
}

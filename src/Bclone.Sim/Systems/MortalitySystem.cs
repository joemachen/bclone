using Bclone.Sim.Config;
using Bclone.Sim.Core;
using Bclone.Sim.World;

namespace Bclone.Sim.Systems;

/// <summary>
/// Step 4 of the tick order: the two ways a life ends.
/// </summary>
/// <remarks>
/// <para>
/// Starvation and old age must read as <em>completely different events</em> — that
/// distinction is the phase's Success Test (spec §9). One is a failure the player
/// should be able to trace back to a thin autumn; the other is a full life. So they
/// get different epitaphs, and the old-age one counts the winters.
/// </para>
/// <para>
/// Old age is checked first. If both would fire on the same tick, dying of old age is
/// the better reading of that life — a villager who reaches their last year and is
/// also hungry died old, not starving.
/// </para>
/// </remarks>
public sealed class MortalitySystem : ISimSystem
{
    public string Name => "mortality";

    public void Execute(SimWorld world)
    {
        for (int i = 0; i < world.Villagers.Count; i++)
        {
            CheckOne(world, world.Villagers[i]);
        }
    }

    private static void CheckOne(SimWorld world, Villager villager)
    {
        if (!villager.Alive)
        {
            return;
        }

        SimConfig config = world.Config;

        if (villager.AgeYears >= villager.LifespanYears)
        {
            Kill(world, villager, CauseOfDeath.OldAge);
            return;
        }

        // Boundary is >=, decided in spec §11.
        if (villager.TicksAtMaxHunger >= config.StarvationTicks)
        {
            Kill(world, villager, CauseOfDeath.Starvation);
        }
    }

    private static void Kill(SimWorld world, Villager villager, CauseOfDeath cause)
    {
        villager.Alive = false;
        villager.CauseOfDeath = cause;
        villager.State = VillagerState.Dead;
        villager.DiedAtTick = world.Tick;
        villager.ActionTicksRemaining = 0;

        string epitaph = cause switch
        {
            CauseOfDeath.OldAge =>
                $"{villager.Name} died of old age at {villager.AgeYears}, " +
                $"having survived {villager.WintersSurvived} winters and gathered " +
                $"{world.HouseholdOf(villager).Stockpile.LifetimeGathered} food across a full life.",

            CauseOfDeath.Starvation =>
                $"{villager.Name} starved to death at {villager.AgeYears}, " +
                $"{world.Clock}. They had survived {villager.WintersSurvived} winters.",

            _ => $"{villager.Name} died.",
        };

        world.Narrate(epitaph);
    }
}

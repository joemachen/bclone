using Bclone.Sim.Config;
using Bclone.Sim.Core;
using Bclone.Sim.Logging;
using Bclone.Sim.World;

namespace Bclone.Sim.Systems;

/// <summary>
/// Step 8 of the tick order, and the last: the three ways a life ends.
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
/// <para>
/// <b>Cold and hunger must never be ambiguous</b> (D17). That is the condition Phase 0
/// attached to ever allowing a second death system, and it is the one thing here that
/// is not merely tidy. Where both counters have crossed, the death names whichever is
/// <em>further past</em> its threshold and reports the other in the same breath, so the
/// player is never left inferring which one actually did it.
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
        bool starving = villager.TicksAtMaxHunger >= config.StarvationTicks;
        bool freezing = config.ExposureThreshold > 0 && villager.Cold >= config.ExposureThreshold;

        if (!starving && !freezing)
        {
            return;
        }

        // Whichever counter is further past its own threshold, measured as a share of
        // it so the two are comparable despite counting to different numbers. A tie
        // goes to hunger: it is the older system, and "they starved, and were also
        // cold" is the more ordinary reading of a bad winter.
        //
        // Deliberately NOT "whichever system ran first this tick" — that would make
        // the cause of death a fact about the tick order rather than about the
        // villager, which is exactly the ambiguity D17 forbids.
        int hungerOverrun = starving ? villager.TicksAtMaxHunger * 100 / config.StarvationTicks : 0;
        int coldOverrun = freezing ? villager.Cold * 100 / config.ExposureThreshold : 0;

        Kill(world, villager, coldOverrun > hungerOverrun ? CauseOfDeath.Cold : CauseOfDeath.Starvation);
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
                $"{world.HouseholdOf(villager).Stockpile.LifetimeGathered.Grouped()} food across a full life.",

            CauseOfDeath.Starvation =>
                $"{villager.Name} starved to death at {villager.AgeYears}, " +
                $"{world.Clock}. They had survived {villager.WintersSurvived} winters." +
                AndAlso(world, villager, CauseOfDeath.Starvation),

            // A statement about the PERSON, not about their household's shelf (D45).
            // It used to read "the household had been without firewood for N days",
            // which stopped being true the moment cold became positional: they may have
            // frozen on the walk back from a tree stand with a full woodpile at home.
            // Where they were standing when it killed them is the fact that explains it.
            CauseOfDeath.Cold =>
                $"{villager.Name} froze to death at {villager.AgeYears}, {world.Clock}, " +
                $"{WhereTheyWere(world, villager)}. " +
                $"They had survived {villager.WintersSurvived} winters." +
                AndAlso(world, villager, CauseOfDeath.Cold),

            _ => $"{villager.Name} died.",
        };

        world.Narrate(epitaph, LogCategory.Death);
    }

    /// <summary>Where somebody was when the cold finished them (D45).</summary>
    /// <remarks>
    /// Three sentences for three genuinely different stories, and a player acts on each
    /// differently: out in the open is a walk that was too long, a fireless roof is a
    /// woodpile that ran out, and a house with a fire in it should be impossible — if it
    /// ever prints, the thaw is not being applied and that is a bug, said out loud
    /// rather than swallowed (METHODOLOGY §4).
    /// </remarks>
    private static string WhereTheyWere(SimWorld world, Villager villager) =>
        world.ShelterAt(villager.Position) switch
        {
            Shelter.Fire => "beside a burning fire, which should not be possible",
            Shelter.Roof => "under a roof with no fire under it",
            _ => "out in the open",
        };

    /// <summary>
    /// Name the other affliction, when there was one.
    /// </summary>
    /// <remarks>
    /// The other half of the promise D17 extracted in exchange for allowing a second
    /// death system. Saying "she froze" when she was also starving is true but
    /// misleading, and a player who acts on it fixes the wrong thing. So the epitaph
    /// reports both and is explicit about which one won.
    /// </remarks>
    private static string AndAlso(SimWorld world, Villager villager, CauseOfDeath cause)
    {
        SimConfig config = world.Config;

        if (cause == CauseOfDeath.Cold && villager.TicksAtMaxHunger > 0)
        {
            return villager.TicksAtMaxHunger >= config.StarvationTicks
                ? " They were starving as well; the cold got there first."
                : " They were going hungry too, but it was the cold that killed them.";
        }

        if (cause == CauseOfDeath.Starvation && villager.Cold > 0)
        {
            return config.ExposureThreshold > 0 && villager.Cold >= config.ExposureThreshold
                ? " They were freezing as well; hunger got there first."
                : " They were cold too, but it was hunger that killed them.";
        }

        return string.Empty;
    }
}

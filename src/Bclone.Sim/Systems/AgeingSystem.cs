using Bclone.Sim.Config;
using Bclone.Sim.Core;
using Bclone.Sim.World;

namespace Bclone.Sim.Systems;

/// <summary>
/// Step 2 of the tick order: age turns into physical decline.
/// </summary>
/// <remarks>
/// <para>
/// Without this, ageing is just a countdown to a death event — the villager works
/// exactly as well at 51 as at 19, every year of the life is identical, and
/// "generational time is the core loop" (non-negotiable 5) is a claim the sim does
/// not actually make. Declining vigour is what gives a life a shape: strong middle
/// years where the store fills easily and there is time to rest, then a long
/// tightening where the same food costs more trips and the margin thins.
/// </para>
/// <para>
/// It was the only source of year-to-year variation in Phase 0, and the systemic
/// pressures that will eventually supply more — climate drift, soil depletion, disease
/// (DESIGN.md §2.3) — still belong to later phases.
/// </para>
/// <para>
/// <b>Childhood arrived with households</b>, as Phase 0's spec said it would: this returns
/// <see cref="LifeStage.Child"/> below <c>adult_age</c>, children eat and hold no job, and
/// the economy is derived against a single adult supporting three of them. What Phase 0
/// excluded was a frail <em>vigour</em> curve for the young — a weak childhood on top of a
/// weak old age, with one villager and nobody to depend on, was simply an unsurvivable
/// opening.
/// </para>
/// </remarks>
public sealed class AgeingSystem : ISimSystem
{
    /// <summary>Below this, the villager is past their peak.</summary>
    public const int SlowingThreshold = 100;

    /// <summary>At or below this, they are visibly failing.</summary>
    public const int FrailThreshold = 80;

    public string Name => "ageing";

    public void Execute(SimWorld world)
    {
        for (int i = 0; i < world.Villagers.Count; i++)
        {
            AgeOne(world, world.Villagers[i]);
        }
    }

    private static void AgeOne(SimWorld world, Villager villager)
    {
        if (!villager.Alive)
        {
            return;
        }

        villager.Vigour = ComputeVigour(villager.AgeYears, villager.LifespanYears, world.Config);
        villager.LifeStage = StageForAge(villager.AgeYears, villager.Vigour, world.Config);

        VigourStage stage = StageFor(villager.Vigour);
        if (stage == villager.Stage)
        {
            return;
        }

        villager.Stage = stage;

        // Narrate the turn once. This is the beat that makes ageing legible rather
        // than merely true.
        switch (stage)
        {
            case VigourStage.Slowing:
                world.Narrate(
                    $"{villager.Name} is past her strongest years — {world.Clock.SeasonAndYear()}, aged {villager.AgeYears}. " +
                    "The same food takes more walking now.");
                break;

            case VigourStage.Frail:
                world.Narrate(
                    $"{villager.Name} has grown frail — {world.Clock.SeasonAndYear()}, aged {villager.AgeYears}. " +
                    "Every winter is a question now.");
                break;
        }
    }

    /// <summary>
    /// Vigour as a percentage: full until <c>vigour_full_until_age</c>, then a
    /// straight line down to <c>vigour_min_percent</c> in the final year.
    /// </summary>
    /// <remarks>
    /// Integer arithmetic throughout, per decision D2 — this feeds gather yield,
    /// which feeds the stockpile, which decides whether the villager lives.
    /// </remarks>
    public static int ComputeVigour(int ageYears, int lifespanYears, SimConfig config)
    {
        ArgumentNullException.ThrowIfNull(config);

        int fullUntil = config.VigourFullUntilAge;
        int floor = config.VigourMinPercent;

        if (ageYears <= fullUntil)
        {
            return 100;
        }

        if (ageYears >= lifespanYears || lifespanYears <= fullUntil)
        {
            return floor;
        }

        int declineYears = lifespanYears - fullUntil;
        int yearsIntoDecline = ageYears - fullUntil;

        // Multiply before dividing so the integer division truncates once, at the end.
        return 100 - ((100 - floor) * yearsIntoDecline / declineYears);
    }

    /// <summary>
    /// Life stage from age and vigour.
    /// </summary>
    /// <remarks>
    /// Childhood is an age gate; elderhood is a <em>vigour</em> gate rather than a
    /// second age threshold, so "elder" means the same thing as the frailty the
    /// player can already see on screen. Two separate definitions of old would be
    /// two things to keep in sync and one more way for the UI to contradict itself.
    /// </remarks>
    public static LifeStage StageForAge(int ageYears, int vigour, SimConfig config)
    {
        ArgumentNullException.ThrowIfNull(config);

        if (ageYears < config.AdultAge)
        {
            return LifeStage.Child;
        }

        return StageFor(vigour) == VigourStage.Frail ? LifeStage.Elder : LifeStage.Adult;
    }

    /// <summary>Which band a vigour value falls in.</summary>
    public static VigourStage StageFor(int vigour) => vigour switch
    {
        >= SlowingThreshold => VigourStage.Prime,
        > FrailThreshold => VigourStage.Slowing,
        _ => VigourStage.Frail,
    };
}

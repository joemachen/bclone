using Bclone.Sim.Core;
using Bclone.Sim.Logging;
using Bclone.Sim.World;

namespace Bclone.Sim.Systems;

/// <summary>
/// Step 1 of the tick order: advance the calendar and narrate its turning points.
/// </summary>
/// <remarks>
/// The clock itself is derived (<see cref="SimClock.FromTick"/>), so this system's
/// real job is <em>detecting transitions</em> — the season turning, the year turning,
/// villagers growing a year older. Those are the beats the life log is built from.
/// </remarks>
public sealed class ClockSystem : ISimSystem
{
    public string Name => "clock";

    public void Execute(SimWorld world)
    {
        if (world.Tick == 0UL)
        {
            return;
        }

        SimClock current = world.Clock;
        SimClock previous = SimClock.FromTick(world.Tick - 1UL, world.Config);

        // Age everyone every tick rather than only on the year boundary, so age can
        // never disagree with the year on screen. Born in Year 1 at age 0.
        int foragedThisSeason = 0;
        int livingCount = 0;

        for (int i = 0; i < world.Villagers.Count; i++)
        {
            Villager villager = world.Villagers[i];
            if (!villager.Alive)
            {
                continue;
            }

            livingCount++;
            villager.AgeYears = current.Year - villager.BirthYear;

            foragedThisSeason += villager.GathersThisSeason;
        }

        if (livingCount == 0 || current.Season == previous.Season)
        {
            return;
        }

        NarrateSeasonTurn(world, current, previous, foragedThisSeason);

        // ⭐⭐ AND WHAT THE VILLAGE HAS MARKED OUT BUT CANNOT RAISE (2026-08-27). Joe's second
        // granary was marked in Winter, Year 23 and was still a site at Year 44 — twenty-one
        // years, in total silence, while three houses went up around it. **A building that is
        // never coming has to be distinguishable from one that is coming slowly**, which is
        // §1.1, and nothing in the game drew that line.
        //
        // ⭐ SEASONAL RATHER THAN ANNUAL, and the cadence is the difference between the two
        // warnings. `SayWhatKnowledgeIsAtRisk` is annual because it is about a lifetime; this is
        // about a decision the player can act on this afternoon by painting a seam. It is said
        // once per site per material and forgotten when it stops being true, so a village that
        // fixes it hears nothing more.
        world.SayWhatIsWaitingToBeBuilt();

        for (int i = 0; i < world.Villagers.Count; i++)
        {
            world.Villagers[i].GathersThisSeason = 0;
        }
    }

    private static void NarrateSeasonTurn(SimWorld world, SimClock current, SimClock previous, int foraged)
    {
        int stored = TotalStored(world);

        // A season's foraging in one line. Six hundred individual gather entries
        // across a life is a receipt; "foraged 12 times" is a season.
        if (foraged > 0)
        {
            world.Narrate(BuildForagingLine(world, previous, foraged, stored), LogCategory.Season);
        }

        // Winter is the one that matters, so it gets its own line with the stores in
        // it — that number is the whole story of the winter about to happen.
        if (current.IsWinter)
        {
            world.Narrate($"Winter came to Year {current.Year}. Foraging stops. {stored} food stored.", LogCategory.Season);
            return;
        }

        if (!previous.IsWinter)
        {
            return;
        }

        for (int i = 0; i < world.Villagers.Count; i++)
        {
            if (world.Villagers[i].Alive)
            {
                world.Villagers[i].WintersSurvived++;
            }
        }

        if (world.Villagers.Count == 1)
        {
            Villager only = world.Villagers[0];
            world.Narrate(
                $"{only.Name} survived winter {only.WintersSurvived} " +
                $"({stored} food left). {current.Season} of Year {current.Year} begins.", LogCategory.Season);
        }
        else
        {
            world.Narrate(
                $"The village came through winter with {stored} food left — " +
                $"{world.Population} alive. {current.Season} of Year {current.Year} begins.", LogCategory.Season);
        }
    }

    private static string BuildForagingLine(SimWorld world, SimClock previous, int foraged, int stored)
    {
        if (world.Villagers.Count == 1)
        {
            Villager only = world.Villagers[0];

            // The trip count is where declining vigour becomes visible: the same
            // season's food costs four trips at thirty and seven at fifty.
            string effort = only.Stage == VigourStage.Prime ? string.Empty : $" (vigour {only.Vigour}%)";
            return $"{previous.Season} of Year {previous.Year} — {only.Name} foraged {foraged} times{effort}. " +
                   $"{stored} food stored.";
        }

        return $"{previous.Season} of Year {previous.Year} — the village foraged {foraged} times. " +
               $"{stored} food stored.";
    }

    /// <summary>
    /// Food the village has, everywhere it keeps it.
    /// </summary>
    /// <remarks>
    /// This summed the household larders only, which stopped being "what the village has"
    /// the moment the granary arrived (D30) — and the granary is where most of it lives.
    /// The season summary was reporting a fraction of the stores as the whole, in the line
    /// a player uses to judge whether the winter is affordable. It was also a
    /// copy-for-copy duplicate of a sum in the view.
    /// </remarks>
    private static int TotalStored(SimWorld world) => world.TotalFood();
}

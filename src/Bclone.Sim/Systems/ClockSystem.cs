using Bclone.Sim.Core;
using Bclone.Sim.World;

namespace Bclone.Sim.Systems;

/// <summary>
/// Step 1 of the tick order: advance the calendar and narrate its turning points.
/// </summary>
/// <remarks>
/// The clock itself is derived (<see cref="SimClock.FromTick"/>), so this system's
/// real job is <em>detecting transitions</em> — the season turning, the year turning,
/// the villager growing a year older. Those are the beats the life log is built from.
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

        // Kept in sync every tick rather than only on the year boundary, so age can
        // never disagree with the year on screen. Born in Year 1 at age 0.
        world.Villager.AgeYears = current.Year - 1;

        if (current.Season == previous.Season)
        {
            return;
        }

        Villager living = world.Villager;

        // Sum the season's foraging into one line. Six hundred individual gather
        // entries across a life is a receipt; "foraged 12 times" is a season.
        if (living.Alive && living.GathersThisSeason > 0)
        {
            // The trip count is where declining vigour becomes visible: the same
            // season's food costs four trips at thirty and seven at fifty.
            string effort = living.Stage == VigourStage.Prime
                ? string.Empty
                : $" (vigour {living.Vigour}%)";

            world.Narrate(
                $"{previous.Season} of Year {previous.Year} — {living.Name} foraged " +
                $"{living.GathersThisSeason} times{effort}. {world.Stockpile.Food} food stored.");
        }

        living.GathersThisSeason = 0;

        // Winter is the one that matters, so it gets its own line with the stockpile
        // in it — that number is the whole story of the winter about to happen.
        if (current.IsWinter)
        {
            world.Narrate(
                $"Winter came to Year {current.Year}. Foraging stops. {world.Stockpile.Food} food stored.");
        }
        else if (previous.IsWinter && living.Alive)
        {
            living.WintersSurvived++;

            world.Narrate(
                $"{living.Name} survived winter {living.WintersSurvived} " +
                $"({world.Stockpile.Food} food left). {current.Season} of Year {current.Year} begins.");
        }
    }
}

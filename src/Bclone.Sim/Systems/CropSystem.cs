using Bclone.Sim.Core;
using Bclone.Sim.Logging;
using Bclone.Sim.World;

namespace Bclone.Sim.Systems;

/// <summary>
/// The year happening to a field — <c>specs/crops-and-orchards.md §5</c> (D161).
/// </summary>
/// <remarks>
/// <para>
/// <b>Two events, both on a season boundary:</b> autumn ripens what was sown, and winter takes
/// what nobody reaped. Sowing and reaping themselves are a farmer's work and live in
/// <see cref="BehaviorSystem"/> — <em>this</em> is only what the calendar does whether or not
/// anybody is there to watch.
/// </para>
/// <para>
/// <b>⭐ Why this is the last slice of Phase 2 rather than a seasonal yield curve.</b> The curve
/// (`environment-and-seasons.md §5.1`) varied forage yield by season, and its own account of
/// what the player would see was *"a villager simply comes home with more in autumn"* — offered
/// as a virtue, because it needed no UI. That is the objection: it is a number going up where
/// nobody can watch it. A field is bare, then sown, then standing, then gone, and **the
/// difference is the mechanic**.
/// </para>
/// <para>
/// <b>A full sweep, deliberately, and only on a season turn.</b> <see cref="RegrowthSystem"/>
/// slices the valley across a period because it runs every tick; this runs four times a year,
/// so nine thousand tile reads twice a year is cheaper than the arithmetic that would avoid it —
/// and it needs no cursor, which is the property that keeps it out of the state hash.
/// </para>
/// <para>
/// <b>It does not check whether anyone is alive.</b> A field outlives the village that sowed it,
/// and D143 says an unattended village is supposed to die out — so the ground going on turning
/// without them is the correct picture, not an oversight.
/// </para>
/// </remarks>
public sealed class CropSystem : ISimSystem
{
    public string Name => "crops";

    public void Execute(SimWorld world)
    {
        ArgumentNullException.ThrowIfNull(world);

        if (world.Tick == 0UL)
        {
            return;
        }

        SimClock current = world.Clock;
        Season previous = SimClock.FromTick(world.Tick - 1UL, world.Config).Season;
        if (current.Season == previous)
        {
            return;
        }

        if (current.Season == Season.Fall)
        {
            Ripen(world, current);
        }
        else if (current.Season == Season.Winter)
        {
            Rot(world, current);
        }
    }

    /// <summary>Autumn: everything sown is standing.</summary>
    private static void Ripen(SimWorld world, SimClock clock)
    {
        int ripened = 0;

        for (int i = 0; i < world.Map.Tiles.Count; i++)
        {
            if (world.Map.Tiles[i] != Terrain.Sown)
            {
                continue;
            }

            world.SetTerrain(world.Zones.PositionOf(i), Terrain.Ripe);
            ripened++;
        }

        if (ripened > 0)
        {
            world.Narrate(
                $"The fields are ripe — {ripened} {Tiles(ripened)} standing and ready to reap. "
                + $"{clock.SeasonAndYear()}.");
        }
    }

    /// <summary>
    /// Winter: what was not reaped is lost, and the village is told once (Joe — use it or lose it).
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>⭐ ONE LINE FOR THE EVENT, NOT ONE PER TILE.</b> The village log is the story (D8, D9)
    /// and a receipt roll is what Phase 1's QA checklist rules out by name.
    /// </para>
    /// <para>
    /// <b>And it must not be silent, which is a rule this project has paid for twice.</b> D96
    /// deposited 17,451 food into a full granary and out of the world; D144 destroyed firewood
    /// once the woodyard filled. Both were invisible and both were found by Joe playing rather
    /// than by the suite. **A rotting harvest is goods leaving the world by design** — so it is
    /// the one case that has to say so out loud.
    /// </para>
    /// <para>
    /// <b>A <see cref="Terrain.Sown"/> tile is taken too.</b> Nothing should be able to sow
    /// after autumn, so a sown tile at midwinter is a field that missed its own year — losing it
    /// silently would leave ground that never becomes anything.
    /// </para>
    /// </remarks>
    private static void Rot(SimWorld world, SimClock clock)
    {
        int lost = 0;

        for (int i = 0; i < world.Map.Tiles.Count; i++)
        {
            if (world.Map.Tiles[i] is not (Terrain.Ripe or Terrain.Sown))
            {
                continue;
            }

            // The crop id stays. A field remembers what it grows, so next spring knows what
            // to put back in it — see `GeneratedMap.SetCrop`.
            world.SetTerrain(world.Zones.PositionOf(i), Terrain.Field);
            lost++;
        }

        if (lost > 0)
        {
            world.Narrate(
                $"Winter took {lost} {Tiles(lost)} of unreaped crop — it rotted where it stood. "
                + $"{clock.SeasonAndYear()}.");
        }
    }

    private static string Tiles(int count) => count == 1 ? "field" : "fields";
}

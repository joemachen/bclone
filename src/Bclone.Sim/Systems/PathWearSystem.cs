using Bclone.Sim.Core;
using Bclone.Sim.Logging;
using Bclone.Sim.World;

namespace Bclone.Sim.Systems;

/// <summary>
/// ⭐ The season turns and the grass recovers a little — <b>the one moment wear reaches the
/// routes</b> (§2.6, D358, `specs/desire-paths.md`).
/// </summary>
/// <remarks>
/// <para>
/// Footsteps land every tick (`BehaviorSystem.Travel`); this runs four times a year. It fades
/// every tile by <c>path_wear_decay_per_season</c>, and because <see cref="PathWear.Decay"/> bumps
/// <see cref="PathWear.Generation"/>, the next route anybody asks for is built on the new wear
/// (`TravelCostField.FieldTo`). ⛔ Never per tick: every cost change clears every cached flow
/// field, and a village that re-Dijkstra'd itself each step would be D179's regression on purpose.
/// </para>
/// <para>
/// ⭐ And the village says so, once: the first time any tile wears through to a path, the log
/// tells the player the ground has started to remember where people go — §1.1's rule that a
/// mechanic the player cannot see does not exist.
/// </para>
/// </remarks>
internal sealed class PathWearSystem : ISimSystem
{
    public string Name => "paths";

    public void Execute(SimWorld world)
    {
        ArgumentNullException.ThrowIfNull(world);

        SimClock current = SimClock.FromTick(world.Tick, world.Config);
        Season previous = SimClock.FromTick(world.Tick - 1UL, world.Config).Season;
        if (current.Season == previous)
        {
            return;
        }

        // ⭐ The routes re-price once a year, in spring (see `PathWear.Decay`); the trails fade every season.
        bool spring = current.Season == Season.Spring;
        int trodden = world.Paths.Decay(world.Config.PathWearDecayPerSeason, reprice: spring);
        int worn = world.Paths.TilesAtLeast(world.Config.PathWornAt);

        if (worn > 0 && !world.AFirstPathHasWorn)
        {
            world.AFirstPathHasWorn = true;
            world.Narrate(
                "The grass has worn through where people walk most — the village has its first "
                + $"path. {current.SeasonAndYear()}.", LogCategory.Building);
        }

        if (world.Logs(LogLevel.Debug))
        {
            world.Log(LogLevel.Debug, "paths",
                $"The season turned: {trodden} tiles carry wear, {worn} of them are worn paths. "
                + $"{current.SeasonAndYear()}.");
        }
    }
}

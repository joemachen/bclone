using Bclone.Sim.Core;
using Bclone.Sim.World;
using Xunit;

namespace Bclone.Sim.Tests;

/// <summary>
/// Posing a farm directly, rather than waiting for a village to build one
/// (<c>specs/crops-and-orchards.md</c>).
/// </summary>
/// <remarks>
/// <b>D146's lesson, and D143's.</b> A test that waits for the village to happen to raise a
/// farmhouse is at the mercy of what the quota wants that season and of an unattended founding
/// that is <em>supposed</em> to die out. Every state a farm can be in is posed here in a
/// sentence, so the guards assert the mechanic rather than the weather.
/// </remarks>
internal static class FarmFixtures
{
    /// <summary>Raise a farmhouse outright, without waiting years for a builder.</summary>
    internal static Workplace RaiseAFarm(SimWorld world, GridPos? at = null)
    {
        GridPos where = at ?? ClearGroundNear(world);
        Assert.True(world.Mark(BuildingKind.Farmhouse, where).Allowed, $"Could not mark at {where}.");

        Workplace site = Assert.Single(
            world.Workplaces, place => place.Construction?.Kind == BuildingKind.Farmhouse);

        site.Construction!.Deliver(site.Construction.Recipe.Logs);
        for (int i = 0; i <= site.Construction.Recipe.WorkTicks; i++)
        {
            site.Construction.Work();
        }

        GridPos stood = site.Position;
        world.Complete(site);

        return Assert.Single(
            world.Workplaces,
            place => place.Kind == JobKind.Farmer && place.Position == stood && !place.IsSite);
    }

    /// <summary>A buildable, bare tile near the village.</summary>
    internal static GridPos ClearGroundNear(SimWorld world)
    {
        GridPos site = world.Map.FoundingSite;
        for (int radius = 1; radius < 12; radius++)
        {
            for (int dy = -radius; dy <= radius; dy++)
            {
                for (int dx = -radius; dx <= radius; dx++)
                {
                    var at = new GridPos(site.X + dx, site.Y + dy);
                    if (!world.HasSomethingToHarvest(at)
                        && world.CanBuildAt(BuildingKind.Farmhouse, at).Allowed)
                    {
                        return at;
                    }
                }
            }
        }

        throw new Xunit.Sdk.XunitException("Nowhere buildable near the founding site.");
    }

    /// <summary>
    /// Paint every bare tile within reach for the farm. Returns how many it took.
    /// </summary>
    /// <remarks>
    /// <b>Bare only, deliberately.</b> A farm ploughs the ground that is already open and
    /// leaves the wood standing (<see cref="SimWorld.Plough"/>), so painting over trees here
    /// would give a tile count that does not match the field the player can see.
    /// </remarks>
    internal static int GiveItGround(SimWorld world, Workplace farm, int reach)
    {
        int given = 0;
        for (int dy = -reach; dy <= reach; dy++)
        {
            for (int dx = -reach; dx <= reach; dx++)
            {
                var at = new GridPos(farm.Position.X + dx, farm.Position.Y + dy);
                if (!world.Map.Contains(at) || world.Map.TerrainAt(at) != Terrain.Grass)
                {
                    continue;
                }

                if (world.PaintWorkGround(farm, at).Allowed)
                {
                    given++;
                }
            }
        }

        return given;
    }

    /// <summary>Put every tile of a farm's ground under seed, without waiting for a spring.</summary>
    internal static int SowEveryTileOf(SimWorld world, Workplace farm)
    {
        System.Collections.Generic.IReadOnlyList<int> owned = world.Zones.WorkGroundOf(farm.Id);
        int sown = 0;

        for (int i = 0; i < owned.Count; i++)
        {
            GridPos at = world.Zones.PositionOf(owned[i]);
            if (!SimWorld.IsSowable(world.Map.TerrainAt(at)))
            {
                continue;
            }

            world.SetTerrain(at, Terrain.Sown);
            world.Map.SetCrop(at, 1);
            sown++;
        }

        return sown;
    }

    /// <summary>Step until the given season has begun <em>and the systems have seen it</em>.</summary>
    /// <remarks>
    /// <b>⚠️ The extra step is not padding.</b> <see cref="SimLoop.StepOnce"/> runs the systems
    /// and <em>then</em> advances the tick, so the moment <c>World.Clock</c> first reports the
    /// new season nothing has run on it yet — the calendar has turned and nothing has answered.
    /// Stopping there had three of the crop-calendar guards reporting a field that never
    /// ripened, and an off-by-one in a harness reads exactly like a broken feature.
    /// </remarks>
    internal static void StepToTheStartOf(SimLoop loop, Season season)
    {
        for (int i = 0; i < loop.World.Config.TicksPerYear; i++)
        {
            loop.StepOnce();
            if (loop.World.Clock.Season == season)
            {
                loop.StepOnce();
                return;
            }
        }

        throw new System.InvalidOperationException($"A whole year passed without reaching {season}.");
    }
}

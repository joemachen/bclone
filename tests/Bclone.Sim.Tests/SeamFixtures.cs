using Bclone.Sim.Core;
using Bclone.Sim.World;

namespace Bclone.Sim.Tests;

/// <summary>
/// Painting the stone and iron a village can reach — <b>the player action tests kept re-writing</b>.
/// </summary>
/// <remarks>
/// <b>⭐ THREE COPIES OF THIS EXISTED BEFORE D213 AND A FOURTH WAS ABOUT TO.</b> Every one sorted
/// the valley by travel cost and painted the cheapest reachable tiles, because that is what
/// <c>NearestHarvest</c> itself sorts by — a seam on the far bank is not a long walk, it is no
/// walk at all (D40), so painting one would test the river rather than the thing under test.
/// </remarks>
internal static class SeamFixtures
{
    /// <summary>Paint the cheapest reachable tiles of one kind of ground. Returns how many.</summary>
    internal static int PaintNearest(SimWorld world, Terrain terrain, int howMany)
    {
        GridPos from = world.Map.FoundingSite;
        var found = new List<(int Cost, GridPos At)>();

        for (int y = world.Map.MinY; y < world.Map.MinY + world.Map.Height; y++)
        {
            for (int x = world.Map.MinX; x < world.Map.MinX + world.Map.Width; x++)
            {
                var at = new GridPos(x, y);
                if (world.Map.TerrainAt(at) != terrain)
                {
                    continue;
                }

                int cost = world.TravelCost.Cost(from, at);
                if (cost != TerrainCostField.Unreachable)
                {
                    found.Add((cost, at));
                }
            }
        }

        found.Sort(static (a, b) =>
            a.Cost != b.Cost ? a.Cost.CompareTo(b.Cost)
            : a.At.Y != b.At.Y ? a.At.Y.CompareTo(b.At.Y)
            : a.At.X.CompareTo(b.At.X));

        int painted = 0;
        for (int i = 0; i < found.Count && painted < howMany; i++)
        {
            if (world.PaintHarvest(found[i].At).Allowed)
            {
                painted++;
            }
        }

        return painted;
    }

    /// <summary>
    /// Enough stone painted that a village asked to build a store can actually pay for it.
    /// </summary>
    /// <remarks>
    /// <b>Called by every test that marks a granary, a warehouse or a market</b> (D213). Those three
    /// cost stone now, and stone comes from nowhere but the brush — so a test that marks one
    /// without this is not testing building, it is testing that the village cannot afford it,
    /// which <c>StoneCostsTests</c> already does on purpose.
    /// </remarks>
    internal static int PaintStoneForBuilding(SimWorld world) =>
        PaintNearest(world, Terrain.Rock, 4);
}

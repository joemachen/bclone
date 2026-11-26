using Kith.Sim.Core;
using Kith.Sim.World;
using Godot;

namespace Kith.Game;

/// <remarks>
/// ⭐ What a card's picture borrows from the map (A262, `specs/building-portrait.md`; Joe's play note 5, <i>"on building windows,
/// the building should look like the actual in-game version"</i>): the painted building the map itself laid, never a second copy
/// of how to paint it — so the card and the map cannot disagree.
/// </remarks>
public partial class VillageMap
{
    /// <summary>A sim point as the view's tile — where the map draws a building that stands there.</summary>
    internal static Vector2 TileOf(Point at) => InViewTiles(at);

    /// <summary>
    /// The painted building the map drew at <paramref name="tile"/>: its pieces as the map laid them, from its look, colour,
    /// wear, door and yard. Null when the map is not painting (the watercolour tick off — the map draws rectangles, and so does
    /// the card), or draws nothing painted there (a site).
    /// </summary>
    /// <remarks>
    /// ⭐ The map paints every building every frame, wherever the camera is, so a far building's card finds it too. Its key is
    /// the drawn centre rounded to 1/1024 of a tile; a centre that went to the screen and back may round one step away, so a
    /// miss asks the nearest within half a tile.
    /// </remarks>
    internal RoofCache? PaintedAt(Vector2 tile)
    {
        if (!PaintsBuildings)
        {
            return null;
        }

        if (_roofCache.TryGetValue(RoofRounded(tile), out RoofCache? held))
        {
            return held;
        }

        RoofCache? nearest = null;
        float best = 0.5f;
        foreach (KeyValuePair<Vector2, RoofCache> entry in _roofCache)
        {
            float d = entry.Key.DistanceTo(tile);
            if (d < best)
            {
                best = d;
                nearest = entry.Value;
            }
        }

        return nearest;
    }

    /// <summary>A material's painting, as the map paints its roofs with it.</summary>
    internal CanvasTexture? RoofTexture(RoofMaterial material) => RoofTextureOf(material);

    /// <summary>
    /// How full a site is drawn, 0 to 1: every material plus the work, so a two-material building fills honestly rather than
    /// showing full while its stone is still coming (D213); a demolition drains where a construction fills.
    /// </summary>
    internal static float SiteShown(ConstructionSite site)
    {
        int total = System.Math.Max(1, site.Recipe.TotalMaterials + site.Recipe.WorkTicks);
        float done = (site.TotalDelivered + site.WorkDone) / (float)total;
        return site.Demolishing ? 1f - done : done;
    }
}

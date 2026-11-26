using Kith.Sim.Core;
using Kith.Sim.World;
using Godot;

namespace Kith.Game;

/// <remarks>What the probe's <c>portrait:</c> line poses on the map (A262, `specs/building-portrait.md §5`).</remarks>
public partial class VillageMap
{
    /// <summary>
    /// Pose the map painting, and a painted building laid at <paramref name="at"/> as the map lays one — a granary's roof. The
    /// map's own cache holds it, so a card asks exactly what the map would draw. <see cref="UnposeThePainting"/> takes it away.
    /// </summary>
    internal RoofCache PoseAPaintedBuildingAt(Point at, float wide, float deep, ushort facing)
    {
        _probeWatercolourWas ??= _watercolour;
        _watercolour = true;
        Vector2 tile = RoofRounded(TileOf(at));
        return CachedRoof(new RoofSignature(RoofOf(StoreKind.Granary)!.Value, tile, wide, deep, facing, GranaryColour, false, null, tile, 7));
    }

    /// <summary>Turn the posed painting off or on — a card should follow the map (rectangles when it is off).</summary>
    internal void PoseThePainting(bool on) => _watercolour = on;

    /// <summary>Take the posed building away and put the tick back as it was.</summary>
    internal void UnposeThePainting(RoofCache posed)
    {
        _roofCache.Remove(posed.Signature.At);
        _watercolour = _probeWatercolourWas ?? _watercolour;
        _probeWatercolourWas = null;
    }

    private bool? _probeWatercolourWas;
}

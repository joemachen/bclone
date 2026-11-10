using Godot;

namespace Bclone.Game;

/// <summary>
/// ⭐ The watercolour's own values — <b>what the valley adds on its way toward the concept art</b>
/// (<c>specs/terrain-shader.md §7</c>, A26–A27), kept apart from the grass's approved look and the lab's terrain file.
/// </summary>
/// <remarks>
/// <para>
/// The same rule as <see cref="GrassLook"/> and <see cref="GrassLabTerrain"/>: <b>this class gives its properties no
/// defaults</b>; the numbers live in <see cref="Path"/>, the shader holds none, and the probe asks that every one reaches
/// the layer. A value here is Joe's to approve by eye; until he does, the file says so.
/// </para>
/// </remarks>
[GlobalClass]
public sealed partial class ValleyWatercolourLook : Resource
{
    /// <summary>Where the values live.</summary>
    internal const string Path = "res://looks/valley_watercolour.tres";

    /// <summary>The pen's ink, sRGB: a warm sepia-black (the art direction, A26).</summary>
    [Export]
    public Color InkColour { get; set; }

    /// <summary>How dark the ink lies at the line's heart, 0 to 1: a pen line, not a cut.</summary>
    [Export]
    public float InkStrength { get; set; }

    /// <summary>
    /// The bank's stroke at its middle pressure, in screen pixels — the same at every zoom, as a pen's is on paper. ⛔ The
    /// bank is the only ground the pen draws (A28: woods and seams are not outlined; the trees are).
    /// </summary>
    [Export]
    public float InkWidthPx { get; set; }

    /// <summary>How far the pen's pressure swells and thins the stroke along its length: 0 even, 1 from nothing to double.</summary>
    [Export]
    public float InkPressure { get; set; }

    /// <summary>How long one swell of the pen's pressure is, along the bank, in tiles.</summary>
    [Export]
    public float InkPressureTiles { get; set; }

    /// <summary>Where the pressure is lightest the pen lifts: the share of the pressure's range under which the stroke breaks.</summary>
    [Export]
    public float InkLifts { get; set; }

    /// <summary>How far the ink's edge creeps raggedly into the paper's fibres, in screen pixels.</summary>
    [Export]
    public float InkFeatherPx { get; set; }

    /// <summary>
    /// How far each tree's ink outline reaches past its crown, in tiles (A28: the trees are outlined, the ground is not). In
    /// tiles, because the crowns are a mesh in tiles: a pixel and a half at 32 px a tile, three at the closest zoom.
    /// </summary>
    [Export]
    public float TreeInkTiles { get; set; }

    /// <summary>How much darker the river's wash lies at its own edge, where the pigment pools as it dries (0 to 1).</summary>
    [Export]
    public float PoolStrength { get; set; }

    /// <summary>How far inside its edge a wash's pooling reaches, in screen pixels.</summary>
    [Export]
    public float PoolWidthPx { get; set; }

    /// <summary>The values as they stand in <see cref="Path"/>.</summary>
    internal static ValleyWatercolourLook Current => GD.Load<ValleyWatercolourLook>(Path);
}

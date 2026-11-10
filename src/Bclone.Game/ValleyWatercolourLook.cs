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

    /// <summary>The line's width in screen pixels — the same at every zoom, as a pen's is on paper.</summary>
    [Export]
    public float InkWidthPx { get; set; }

    /// <summary>How much heavier the river's bank is drawn than a woods' or a seam's edge.</summary>
    [Export]
    public float BankInkScale { get; set; }

    /// <summary>How much a line's width wanders along it, 0 to 1 of the width: the pen's hand.</summary>
    [Export]
    public float InkWobble { get; set; }

    /// <summary>How much darker a wash lies at its own edge, where the pigment pools as it dries (0 to 1).</summary>
    [Export]
    public float PoolStrength { get; set; }

    /// <summary>How far inside its edge a wash's pooling reaches, in screen pixels.</summary>
    [Export]
    public float PoolWidthPx { get; set; }

    /// <summary>The values as they stand in <see cref="Path"/>.</summary>
    internal static ValleyWatercolourLook Current => GD.Load<ValleyWatercolourLook>(Path);
}

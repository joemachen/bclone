using System.Globalization;
using System.Text;
using Godot;

namespace Bclone.Game;

/// <summary>
/// The terrain options' starting values — <b>a lab file, and nothing in it is approved</b>
/// (`specs/terrain-shader.md §4`, D700).
/// </summary>
/// <remarks>
/// <para>
/// Kept apart from <see cref="GrassLook"/> so that <c>grass_approved.tres</c> does not change while these are tuned. The
/// same rule holds, though: <b>this class gives its properties no defaults</b>, the numbers live in
/// <see cref="LabPath"/>, and the probe asks that the file sets every one and leaves every switch off.
/// </para>
/// <para>
/// When Joe approves an option its value moves to the look's file, as the grass's did (D632).
/// </para>
/// </remarks>
[GlobalClass]
public sealed partial class GrassLabTerrain : Resource
{
    /// <summary>Where the options start.</summary>
    internal const string LabPath = "res://looks/terrain_lab.tres";

    /// <summary>The fake valley under the grass; off, the ground is the approved parchment everywhere.</summary>
    [Export]
    public bool CoverageOn { get; set; }

    /// <summary>How far past the contour the grass takes to reach full strength, in the map's weight (0.5 is the whole way).</summary>
    [Export]
    public float CoverageFade { get; set; }

    /// <summary>The options, from their file.</summary>
    internal static GrassLabTerrain StartingPoint => GD.Load<GrassLabTerrain>(LabPath);

    /// <summary>The options as a list like Joe's, to follow the look's on <i>Copy current values</i>.</summary>
    internal string Say()
    {
        var said = new StringBuilder();
        said.AppendLine("TERRAIN LAB SETTINGS (options, none approved)");
        said.Append($"- Coverage map: {(CoverageOn ? "ON" : "OFF")}. Fade {CoverageFade.ToString("0.00", CultureInfo.InvariantCulture)}");
        return said.ToString();
    }
}

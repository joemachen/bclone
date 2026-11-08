using System.Globalization;
using System.Text;
using Godot;

namespace Bclone.Game;

/// <summary>
/// The terrain options' starting values — <b>a lab file, and nothing in it is approved</b>
/// (`specs/terrain-shader.md §4`, A1).
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

    /// <summary>The paper's grain in tiles rather than screen pixels, so it moves with the land (A3).</summary>
    [Export]
    public bool WorldGrainOn { get; set; }

    /// <summary>The world-anchored grain's finest octave, in tiles a cycle; the other two keep the placeholder's ratios.</summary>
    [Export]
    public float GrainScaleTiles { get; set; }

    /// <summary>The grain drawn under the placeholders rather than over them (D630 lays it over).</summary>
    [Export]
    public bool GrainUnderVillage { get; set; }

    /// <summary>Snow, its own layer over the grass with its own blend: Multiply cannot lighten (A4).</summary>
    [Export]
    public bool SnowOn { get; set; }

    /// <summary>How snow meets what is under it: 0 Lerp, 1 Screen, 2 Lighten (`terrain-shader.md §2`).</summary>
    [Export]
    public int SnowBlend { get; set; }

    /// <summary>How much of the valley is white, 0 to 1: one number for the whole valley.</summary>
    [Export]
    public float SnowAmount { get; set; }

    /// <summary>How soft a patch's edge is, in the threshold's units: 0 is a hard line.</summary>
    [Export]
    public float SnowPatchiness { get; set; }

    /// <summary>How big the patches are, in tiles a cycle of the threshold's noise.</summary>
    [Export]
    public float SnowPatchTiles { get; set; }

    /// <summary>The snow, sRGB: near white, warmed to sit on the paper.</summary>
    [Export]
    public Color SnowColour { get; set; }

    /// <summary>The blends' names, in <see cref="SnowBlend"/>'s order.</summary>
    internal static readonly string[] SnowBlends = { "Lerp", "Screen", "Lighten" };

    /// <summary>Ground kinds blended over a width, <c>w^sharpness</c>, rather than the bake's hard contour (A5).</summary>
    [Export]
    public bool SplatOn { get; set; }

    /// <summary>The splat's power: 48 is the bake's hard contour, 1 a wide wash.</summary>
    [Export]
    public float SplatSharpness { get; set; }

    /// <summary>Land edges warped by a noise of their own, as pigment runs past the line; never the water's (D342).</summary>
    [Export]
    public bool BleedOn { get; set; }

    /// <summary>How far a land edge may wander, in tiles (0–0.5).</summary>
    [Export]
    public float BleedReachTiles { get; set; }

    /// <summary>The bleed's noise, in tiles a cycle.</summary>
    [Export]
    public float BleedScaleTiles { get; set; }

    /// <summary>Joe's painted ground tiles in place of the kinds' flat colours (`ground-textures.md`, A14): the woods first.</summary>
    [Export]
    public bool GroundTexturesOn { get; set; }

    /// <summary>How many tiles one repeat of a painting covers; 64 is the paintings' own scale, 32 px a tile.</summary>
    [Export]
    public float TextureTilesPerRepeat { get; set; }

    /// <summary>How the painting's repeat is hidden: 0 Off (as painted), 1 Hex (the grass's hex tiling).</summary>
    [Export]
    public int TextureAntiTiling { get; set; }

    /// <summary>Every wood drawn as young wood — the woods tile × (young ÷ woods) — so the derived colour can be judged.</summary>
    [Export]
    public bool YoungWoodPreview { get; set; }

    /// <summary>The anti-tilings' names, in <see cref="TextureAntiTiling"/>'s order.</summary>
    internal static readonly string[] TextureAntiTilings = { "Off", "Hex" };

    /// <summary>The options, from their file.</summary>
    internal static GrassLabTerrain StartingPoint => GD.Load<GrassLabTerrain>(LabPath);

    /// <summary>The options as a list like Joe's, to follow the look's on <i>Copy current values</i>.</summary>
    internal string Say()
    {
        var said = new StringBuilder();
        said.AppendLine("TERRAIN LAB SETTINGS (options; all approved but the coverage map, A7-A9)");
        said.AppendLine($"- Coverage map: {(CoverageOn ? "ON" : "OFF")}. Fade {CoverageFade.ToString("0.00", CultureInfo.InvariantCulture)}");
        said.AppendLine($"- World-anchored grain: {(WorldGrainOn ? "ON" : "OFF")}. Scale {GrainScaleTiles.ToString("0.000", CultureInfo.InvariantCulture)} tiles. "
            + $"Under the village: {(GrainUnderVillage ? "ON" : "OFF")}");
        said.AppendLine($"- Snow: {(SnowOn ? "ON" : "OFF")}. Blend {SnowBlends[Math.Clamp(SnowBlend, 0, SnowBlends.Length - 1)]}. "
            + $"Amount {SnowAmount.ToString("0.00", CultureInfo.InvariantCulture)}. Patchiness {SnowPatchiness.ToString("0.00", CultureInfo.InvariantCulture)}. "
            + $"Patch {SnowPatchTiles.ToString("0", CultureInfo.InvariantCulture)} tiles. Colour #{SnowColour.ToHtml(false)}");
        said.AppendLine($"- Splat blending: {(SplatOn ? "ON" : "OFF")}. Sharpness {SplatSharpness.ToString("0.0", CultureInfo.InvariantCulture)}. "
            + $"Edge bleed: {(BleedOn ? "ON" : "OFF")}. Reach {BleedReachTiles.ToString("0.00", CultureInfo.InvariantCulture)} tiles. "
            + $"Scale {BleedScaleTiles.ToString("0.00", CultureInfo.InvariantCulture)} tiles");
        said.Append($"- Ground textures: {(GroundTexturesOn ? "ON" : "OFF")}. Tiles per repeat {TextureTilesPerRepeat.ToString("0", CultureInfo.InvariantCulture)}. "
            + $"Anti-tiling {TextureAntiTilings[Math.Clamp(TextureAntiTiling, 0, TextureAntiTilings.Length - 1)]}. "
            + $"Young wood preview: {(YoungWoodPreview ? "ON" : "OFF")}");
        return said.ToString();
    }
}

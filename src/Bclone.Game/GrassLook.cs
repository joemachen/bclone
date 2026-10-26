using System.Globalization;
using System.Text;
using Godot;

namespace Bclone.Game;

/// <summary>
/// ⭐ The grass look, as Joe approved it in the lab — <b>one place every number lives</b> (`specs/grass-look.md`, D632).
/// </summary>
/// <remarks>
/// <para>
/// The values are in <see cref="ApprovedPath"/>, a text resource a modder can open and change; <b>this class gives them
/// no defaults</b>, so there is no second copy of a number to drift from the first, and the probe asks that the file
/// sets every one. The lab opens at them and <i>Reset to approved</i> goes back to them; the game's terrain shader will
/// read the same file when the grass reaches the valley.
/// </para>
/// <para>
/// ⛔ <b>Rejected, kept switchable, off</b> (D632): <see cref="MixingOn"/> — blending layouts ghosted stamps at part
/// strength and covered the paper like camouflage; and Linear blending (<see cref="HeightBlend"/> false) — the same
/// ghosting. Their code paths stay so they can be compared again; nothing here turns them on.
/// </para>
/// </remarks>
[GlobalClass]
public sealed partial class GrassLook : Resource
{
    /// <summary>Where the approved look lives.</summary>
    internal const string ApprovedPath = "res://looks/grass_approved.tres";

    /// <summary>The mask layout shown: <c>v06</c>, …</summary>
    [Export]
    public string Masks { get; set; } = string.Empty;

    /// <summary>0 = bare parchment, 1 = the pack as painted.</summary>
    [Export]
    public float Strength { get; set; }

    /// <summary>The pack's per-stamp colour jitter stand-in (its snippet's <c>grass_jitter</c>).</summary>
    [Export]
    public float Jitter { get; set; }

    /// <summary>The lab's paper grain — a starting point only; real grain belongs to the terrain shader.</summary>
    [Export]
    public float PaperGrain { get; set; }

    /// <summary>How many tiles one mask repeat covers.</summary>
    [Export]
    public float TilesPerRepeat { get; set; }

    [Export]
    public bool MacroOn { get; set; }

    /// <summary>One cycle of the macro noise every this many tiles.</summary>
    [Export]
    public float MacroCycleTiles { get; set; }

    /// <summary>How far a region's strength may fall: strength × (1 − amount × noise).</summary>
    [Export]
    public float MacroStrengthAmount { get; set; }

    /// <summary>How far a region's season may lead or lag, in seasons.</summary>
    [Export]
    public float MacroSeasonAmount { get; set; }

    [Export]
    public bool HexOn { get; set; }

    /// <summary>The hex weights' power — higher is a narrower blend between hexagons.</summary>
    [Export]
    public float HexSharpness { get; set; }

    /// <summary>True for Height ("each stamp whole"); false is Linear — rejected (D632).</summary>
    [Export]
    public bool HeightBlend { get; set; }

    /// <summary>⛔ Rejected (D632): version mixing ghosted and read as camouflage. Kept, off.</summary>
    [Export]
    public bool MixingOn { get; set; }

    [Export]
    public float MixRegionTiles { get; set; }

    [Export]
    public float MixWidthTiles { get; set; }

    /// <summary>The approved look, from its file.</summary>
    internal static GrassLook Approved => GD.Load<GrassLook>(ApprovedPath);

    /// <summary>
    /// The look as Joe writes it — the same list he approved, so a set copied from the lab can be pasted straight back.
    /// </summary>
    internal string Say()
    {
        string On(bool on) => on ? "ON" : "OFF";
        var said = new StringBuilder();
        said.AppendLine("APPROVED SETTINGS");
        said.AppendLine($"- Masks: {Masks}");
        said.AppendLine($"- Strength: {N(Strength, "0.00")}");
        said.AppendLine($"- Jitter: {N(Jitter, "0.00#")}");
        said.AppendLine($"- Paper grain: {N(PaperGrain, "0.00#")}");
        said.AppendLine($"- Tiles per repeat: {N(TilesPerRepeat, "0.#")}");
        said.AppendLine($"- Macro variation: {On(MacroOn)}. Tiles per cycle {N(MacroCycleTiles, "0.#")}, strength amount "
            + $"{N(MacroStrengthAmount, "0.00#")}, season amount {N(MacroSeasonAmount, "0.00#")}");
        said.AppendLine($"- Hex tiling: {On(HexOn)}. Blend sharpness {N(HexSharpness, "0.0#")}, blend mode "
            + $"\"{(HeightBlend ? "Height — each stamp whole" : "Linear")}\"");
        said.AppendLine($"- Version mixing: {On(MixingOn)} (rejected). Region {N(MixRegionTiles, "0.#")} tiles, transition "
            + $"{N(MixWidthTiles, "0.0#")} tiles");
        said.Append($"- Linear blend: {On(!HeightBlend)} (rejected)");
        return said.ToString();
    }

    private static string N(float value, string format) => value.ToString(format, CultureInfo.InvariantCulture);
}

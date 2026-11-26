using Godot;

namespace Kith.Game;

/// <remarks>
/// ⭐ The land's height drawn as a map (A265–A267, <c>specs/topography.md</c>; Joe's play note 3, <i>"a topography layer that is
/// off by default, but can be toggled on in settings like the grid is?"</i>, and his A on the mocks — contour lines). The valley's
/// shader already holds every tile's height and steepness (<c>heights_map</c>, A66), so this is one uniform: nothing is baked
/// for it and nothing runs per frame on the CPU. It is drawn on the ground, as the grid is, so a wood hides it.
/// ⚠️ It is drawn by the watercolour layer: with that tick off there is nothing to draw it on.
/// ⚠️ View-only, like every other map toggle — nothing about the village changes, so the hash cannot diverge on it.
/// </remarks>
public partial class VillageMap
{
    /// <summary>Whether the land's height is drawn. ⛔ <b>Off by default</b> (Joe: <i>"off by default"</i>).</summary>
    private bool _topography;

    /// <summary>Whether the land's height is drawn (A267).</summary>
    public bool TopographyShown => _topography;

    /// <summary>Draw the land's height, or stop.</summary>
    public void ShowTopography(bool on)
    {
        _topography = on;
        _watercolourMaterial?.SetShaderParameter("topo_on", on);
        QueueRedraw();
    }

    /// <summary>
    /// ⭐ The probe's <c>topography:</c> line (A267) — what can be checked headless: the tick's row off by default, the tick and
    /// the map agreeing, and the tick pressed on and off moving the map and the valley's shader with it. The setting's
    /// round trip is the settings line's, which reads every view tick. How it looks is Joe's eyes.
    /// </summary>
    internal string TheTopographyHolds(CheckBox? tick, bool rowDefault)
    {
        var faults = new System.Collections.Generic.List<string>();
        if (tick is null)
        {
            return "[widths] topography: ⛔ no tick in Settings";
        }

        if (rowDefault)
        {
            faults.Add("the tick's row is on by default (want off)");
        }

        if (tick.ButtonPressed != _topography)
        {
            faults.Add($"the tick shows {tick.ButtonPressed} and the map {_topography}");
        }

        if (_watercolourMaterial is not ShaderMaterial material)
        {
            return $"[widths] topography: ⛔ no watercolour layer to draw it on{(faults.Count > 0 ? "; " + string.Join("; ", faults) : "")}";
        }

        // ⚠️ A material keeps a value the shader never declared, so the switch alone reads green over a shader without the
        // uniform (A267's red-check, M4): ask the shader.
        bool declared = false;
        foreach (Godot.Collections.Dictionary uniform in material.Shader.GetShaderUniformList())
        {
            declared |= uniform["name"].AsString() == "topo_on";
        }

        if (!declared)
        {
            faults.Add("the valley's shader declares no topo_on");
        }

        bool wasOn = tick.ButtonPressed;
        bool Uniform() => material.GetShaderParameter("topo_on").AsBool();
        tick.ButtonPressed = true;
        if (!_topography || !Uniform())
        {
            faults.Add($"pressed on, the map says {_topography} and the shader's topo_on {Uniform()} (want both on)");
        }

        tick.ButtonPressed = false;
        if (_topography || Uniform())
        {
            faults.Add($"pressed off, the map says {_topography} and the shader's topo_on {Uniform()} (want both off)");
        }

        tick.ButtonPressed = wasOn;
        return faults.Count == 0
            ? "[widths] topography: ✅ its own tick, off by default; pressed on, the map and the valley's shader draw the land's height; pressed off, neither does"
            : $"[widths] topography: ⛔ {string.Join("; ", faults)}";
    }
}

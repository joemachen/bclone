using System.Reflection;
using Godot;

namespace Bclone.Game;

/// <summary>
/// ⭐ The terrain shader's options in the lab (`specs/terrain-shader.md §4`, D700): each behind its own switch, off at
/// opening, so the lab still opens at the approved look.
/// </summary>
public sealed partial class GrassLab
{
    /// <summary>Every uniform this file sets: the probe asks the compiled shader for each.</summary>
    private static readonly string[] TerrainUniforms =
    {
        "coverage_on", "coverage_fade", "ground_map", "ground_map_tiles", "kind_wood", "kind_stone", "kind_water", "kind_shallows",
    };

    private PanelContainer _terrain = null!;
    private Label _gpuOnTerrain = null!;
    private bool _terrainShown = true;

    internal CheckBox Coverage { get; private set; } = null!;

    internal HSlider CoverageFade { get; private set; } = null!;

    /// <summary>True once the fake map has been baked — only when it is first switched on, as the mixed layouts are.</summary>
    internal bool GroundLoaded { get; private set; }

    /// <summary>
    /// The panel, in the top-right corner it shares with <i>Anti-tiling</i>: three panels do not fit 1280 wide.
    /// </summary>
    private void BuildTheTerrainPanel(GrassLabTerrain look)
    {
        _material.SetShaderParameter("ground_map_tiles", new Vector2(GrassLabGround.TilesWide, GrassLabGround.TilesTall));
        _material.SetShaderParameter("kind_wood", Srgb(VillageMap.ColourOf(Bclone.Sim.World.Terrain.Forest)));
        _material.SetShaderParameter("kind_stone", Srgb(VillageMap.ColourOf(Bclone.Sim.World.Terrain.Rock)));
        _material.SetShaderParameter("kind_water", Srgb(VillageMap.ColourOf(Bclone.Sim.World.Terrain.Water)));
        _material.SetShaderParameter("kind_shallows", Srgb(VillageMap.ShallowsColour));

        _terrain = Panel("Terrain");
        _terrain.AnchorLeft = 1f;
        _terrain.AnchorRight = 1f;
        _terrain.GrowHorizontal = GrowDirection.Begin;
        _terrain.OffsetLeft = -16f;
        _terrain.OffsetRight = -16f;
        _terrain.OffsetTop = 16f;
        AddChild(_terrain);

        var body = new VBoxContainer();
        body.AddThemeConstantOverride("separation", 4);
        _terrain.AddChild(body);

        var heading = new Label { Text = "Terrain" };
        heading.AddThemeFontSizeOverride("font_size", 20);
        body.AddChild(heading);

        var note = new Label { Text = "options, none approved; all off is the approved look", Modulate = new Color(1, 1, 1, 0.6f) };
        note.AddThemeFontSizeOverride("font_size", 12);
        body.AddChild(note);

        Coverage = AddSwitch(body, "Coverage map", "coverage_on", false);
        Coverage.Toggled += on =>
        {
            if (on)
            {
                LoadTheGround();
            }
        };
        Coverage.ButtonPressed = look.CoverageOn;
        CoverageFade = AddSlider(body, "Coverage fade", 0.0, 0.5, 0.01, look.CoverageFade, Sets("coverage_fade"), Plain("0.00"));

        _gpuOnTerrain = new Label { Name = "GpuOnTerrain", Modulate = new Color(1, 1, 1, 0.75f) };
        _gpuOnTerrain.AddThemeFontSizeOverride("font_size", 13);
        body.AddChild(_gpuOnTerrain);

        // ⚠️ Follows the left panel, so `lab_render.gd` and `lab_cost.gd`, which hide it and *Tiling* by name, need no change.
        _panel.VisibilityChanged += ShowTheCornerPanel;
        ShowTheCornerPanel();
    }

    /// <summary>Whichever of the two the corner is showing, and neither when the left panel is hidden.</summary>
    private void ShowTheCornerPanel()
    {
        _tiling.Visible = _panel.Visible && !_terrainShown;
        _terrain.Visible = _panel.Visible && _terrainShown;
    }

    private void SwapTheCornerPanel()
    {
        _terrainShown = !_terrainShown;
        ShowTheCornerPanel();
    }

    /// <summary>The fake map, baked once on first switching it on (0.44 s, measured), so the lab opens without paying for it.</summary>
    private void LoadTheGround()
    {
        if (GroundLoaded)
        {
            return;
        }

        _material.SetShaderParameter("ground_map", GrassLabGround.Texture());
        GroundLoaded = true;
    }

    /// <summary>Every terrain control back to its file, read again so an edit shows without a restart.</summary>
    private void ResetTheTerrain()
    {
        var look = (GrassLabTerrain)ResourceLoader.Load(GrassLabTerrain.LabPath, null, ResourceLoader.CacheMode.Replace);
        Coverage.ButtonPressed = look.CoverageOn;
        CoverageFade.Value = look.CoverageFade;
    }

    /// <summary>The panel's options, as a <see cref="GrassLabTerrain"/>.</summary>
    internal GrassLabTerrain CurrentTerrain() => new()
    {
        CoverageOn = Coverage.ButtonPressed,
        CoverageFade = (float)CoverageFade.Value,
    };

    private static Vector3 Srgb(Color colour) => new(colour.R, colour.G, colour.B);

    /// <summary>
    /// The probe's lines for the terrain (D700), one a control. <paramref name="lab"/> stands at the approved look, and
    /// is left there.
    /// </summary>
    private static IEnumerable<string> TerrainSelfCheck(GrassLab lab)
    {
        float Uniform(string name) => lab._material.GetShaderParameter(name).AsSingle();
        bool Flag(string name) => lab._material.GetShaderParameter(name).AsBool();
        static string Line(string what, string good, List<string> faults) => faults.Count == 0
            ? $"[widths] terrain lab, {what}: ✅ {good}"
            : $"[widths] terrain lab, {what}: ❌ {string.Join("; ", faults)}";

        // The file: as the look's, the only place a number lives; and it leaves every switch off.
        var faults = new List<string>();
        string file = Godot.FileAccess.GetFileAsString(GrassLabTerrain.LabPath);
        foreach (PropertyInfo property in typeof(GrassLabTerrain).GetProperties())
        {
            if (property.GetCustomAttribute<ExportAttribute>() is null)
            {
                continue;
            }

            if (!file.Contains($"\n{property.Name} = ", StringComparison.Ordinal))
            {
                faults.Add($"{GrassLabTerrain.LabPath} does not set {property.Name}");
            }

            if (property.PropertyType == typeof(bool) && (bool)property.GetValue(GrassLabTerrain.StartingPoint)!)
            {
                faults.Add($"{GrassLabTerrain.LabPath} has {property.Name} on, so the lab would not open at the approved look");
            }
        }

        yield return Line("file", $"{GrassLabTerrain.LabPath} sets every option and leaves every switch off", faults);

        // ⚠️ The shader compiles. Godot only logs a failure and every other line here stays green over it, because a
        // material holds what it is told whether or not the shader has such a uniform. A failed shader declares none.
        faults = new List<string>();
        var declared = new HashSet<string>();
        foreach (Godot.Collections.Dictionary uniform in lab._material.Shader.GetShaderUniformList().Select(u => u.AsGodotDictionary()))
        {
            declared.Add(uniform["name"].AsString());
        }

        foreach (string uniform in TerrainUniforms.Where(name => !declared.Contains(name)))
        {
            faults.Add($"grass_lab.gdshader declares no {uniform} ({declared.Count} uniforms in all; 0 is a shader that did not compile)");
        }

        yield return Line("shader", $"grass_lab.gdshader compiles and declares all {TerrainUniforms.Length} of the terrain's uniforms", faults);

        // The panels: the corner swaps, and neither outlives the left panel, which is what the tools hide.
        faults = new List<string>();
        if (!lab._terrain.Visible || lab._tiling.Visible)
        {
            faults.Add($"opens with terrain {lab._terrain.Visible}, anti-tiling {lab._tiling.Visible} (want the terrain panel alone)");
        }

        lab.SwapTheCornerPanel();
        if (lab._terrain.Visible || !lab._tiling.Visible)
        {
            faults.Add("the swap does not show anti-tiling");
        }

        lab.SwapTheCornerPanel();
        lab._panel.Visible = false;
        if (lab._terrain.Visible || lab._tiling.Visible)
        {
            faults.Add("hiding the left panel left a corner panel standing, so lab_render.gd would draw it");
        }

        lab._panel.Visible = true;
        if (!lab._terrain.Visible)
        {
            faults.Add("showing the left panel did not bring the terrain panel back");
        }

        yield return Line("panels", "the corner opens on Terrain, swaps with Anti-tiling, and follows the left panel", faults);

        // Coverage map: the switch.
        faults = new List<string>();
        if (lab.Coverage.ButtonPressed || Flag("coverage_on") || lab.GroundLoaded)
        {
            faults.Add($"opens with the box {lab.Coverage.ButtonPressed}, the uniform {Flag("coverage_on")}, the map baked {lab.GroundLoaded} (want off, off, not baked)");
        }

        lab.Coverage.ButtonPressed = true;
        var map = lab._material.GetShaderParameter("ground_map").As<Texture2D>();
        if (!Flag("coverage_on") || !lab.GroundLoaded || map is null
            || map.GetWidth() != GrassLabGround.ImageWide || map.GetHeight() != GrassLabGround.ImageTall)
        {
            faults.Add($"switched on: the uniform {Flag("coverage_on")}, baked {lab.GroundLoaded}, a map of {map?.GetWidth()} x {map?.GetHeight()}");
        }

        if (lab._material.GetShaderParameter("kind_water").AsVector3() != Srgb(VillageMap.ColourOf(Bclone.Sim.World.Terrain.Water))
            || lab._material.GetShaderParameter("kind_wood").AsVector3() != Srgb(VillageMap.ColourOf(Bclone.Sim.World.Terrain.Forest))
            || lab._material.GetShaderParameter("kind_stone").AsVector3() != Srgb(VillageMap.ColourOf(Bclone.Sim.World.Terrain.Rock))
            || lab._material.GetShaderParameter("kind_shallows").AsVector3() != Srgb(VillageMap.ShallowsColour))
        {
            faults.Add("the ground kinds are not VillageMap's palette");
        }

        lab.ResetToApproved();
        if (lab.Coverage.ButtonPressed || Flag("coverage_on"))
        {
            faults.Add("Reset to approved left the coverage map on");
        }

        yield return Line("coverage map", "opens off and unbaked, reaches the shader with a map of the right size over VillageMap's palette, and Reset turns it off", faults);

        // Coverage fade: the slider.
        faults = new List<string>();
        float fade = GrassLabTerrain.StartingPoint.CoverageFade;
        if (Math.Abs(Uniform("coverage_fade") - fade) > 1e-6f)
        {
            faults.Add($"opens at {Uniform("coverage_fade")} (want the file's {fade})");
        }

        lab.CoverageFade.Value = 0.4;
        if (Math.Abs(Uniform("coverage_fade") - 0.4f) > 1e-6f)
        {
            faults.Add($"0.4 reached the shader as {Uniform("coverage_fade")}");
        }

        lab.ResetToApproved();
        if (Math.Abs(Uniform("coverage_fade") - fade) > 1e-6f)
        {
            faults.Add($"Reset left it at {Uniform("coverage_fade")}");
        }

        yield return Line("coverage fade", "opens at the file's value, reaches the shader, and Reset brings it back", faults);

        // The list: Joe's ten lines stand as they were, and the options follow.
        faults = new List<string>();
        string[] said = lab.CurrentValues().Split("\n\n");
        if (said.Length != 2 || said[0] != GrassLook.Approved.Say() || said[1] != GrassLabTerrain.StartingPoint.Say()
            || !said[1].Contains("- Coverage map: OFF. Fade ", StringComparison.Ordinal))
        {
            faults.Add($"Copy says \"{lab.CurrentValues()}\"");
        }

        yield return Line("copy", "Copy says Joe's list unchanged, then the options' own", faults);

        yield return GroundCheck();
    }

    /// <summary>The fake map is what `terrain-shader.md §4` says it is.</summary>
    private static string GroundCheck()
    {
        var faults = new List<string>();
        byte[] first = GrassLabGround.Bake();
        if (!first.AsSpan().SequenceEqual(GrassLabGround.Bake()))
        {
            faults.Add("two bakes disagree, so a place does not always look the same");
        }

        var kinds = new bool[4];
        for (int i = 0; i < first.Length; i += 4)
        {
            if (first[i] + first[i + 1] + first[i + 2] + first[i + 3] != 255)
            {
                faults.Add($"texel {i / 4}'s weights sum to {first[i] + first[i + 1] + first[i + 2] + first[i + 3]}");
                break;
            }

            for (int k = 0; k < 4; k++)
            {
                kinds[k] |= first[i + k] > 200;
            }
        }

        if (Array.IndexOf(kinds, false) >= 0)
        {
            faults.Add($"a kind never wins: grass, wood, stone, water = {string.Join(", ", kinds)}");
        }

        foreach (Vector2 standing in GrassLabVillage.StandingOnGrass)
        {
            if (GrassLabGround.WeightsAt(standing).Grass < 0.99f)
            {
                faults.Add($"a placeholder at {standing} does not stand on open grass ({GrassLabGround.WeightsAt(standing).Grass})");
            }
        }

        Rect2I wood = GrassLabVillage.Wood;
        Vector2 middle = wood.Position + (wood.Size / 2);
        if (GrassLabGround.WeightsAt(middle).Wood < 0.99f)
        {
            faults.Add($"the placeholders' wood does not stand on a wood ({GrassLabGround.WeightsAt(middle).Wood})");
        }

        float narrowest = GrassLabGround.NarrowestRiver();
        if (narrowest < 2f)
        {
            faults.Add($"the river narrows to {narrowest} tiles (the game's is two)");
        }

        return faults.Count == 0
            ? "[widths] terrain lab, fake map: ✅ a pure function of the tile, every texel's weights sum to one, all four kinds win "
                + $"somewhere, the placeholders stand on grass and their wood on a wood, the river never under two tiles (narrowest {narrowest:0.00})"
            : $"[widths] terrain lab, fake map: ❌ {string.Join("; ", faults)}";
    }
}

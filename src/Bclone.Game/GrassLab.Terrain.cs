using System.Globalization;
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
        "view_tile", "snow_on", "snow_blend", "snow_amount", "snow_patchiness", "snow_patch_tiles", "snow_srgb",
        "splat_on", "splat_sharpness", "bleed_on", "bleed_reach_tiles", "bleed_scale_tiles",
    };

    /// <summary>Every uniform this file and the zoom set on the grain: the probe asks its compiled shader too.</summary>
    private static readonly string[] GrainUniforms = { "amount", "world_on", "grain_scale_tiles", "size_px", "px_per_tile", "view_tile" };

    /// <summary>How far the view may go: the fake map's own edge, so the village can always be found again.</summary>
    private static readonly Vector2 ViewLimit = new(GrassLabGround.TilesWide / 2f, GrassLabGround.TilesTall / 2f);

    private PanelContainer _terrain = null!;
    private Label _gpuOnTerrain = null!;
    private bool _terrainShown = true;

    internal CheckBox Coverage { get; private set; } = null!;

    internal HSlider CoverageFade { get; private set; } = null!;

    internal CheckBox WorldGrain { get; private set; } = null!;

    internal HSlider GrainScale { get; private set; } = null!;

    internal CheckBox GrainUnder { get; private set; } = null!;

    internal CheckBox Snow { get; private set; } = null!;

    internal OptionButton SnowBlend { get; private set; } = null!;

    internal HSlider SnowAmount { get; private set; } = null!;

    internal HSlider SnowPatchiness { get; private set; } = null!;

    internal HSlider SnowPatchSize { get; private set; } = null!;

    internal CheckBox Splat { get; private set; } = null!;

    internal HSlider SplatSharpness { get; private set; } = null!;

    internal CheckBox Bleed { get; private set; } = null!;

    internal HSlider BleedReach { get; private set; } = null!;

    internal HSlider BleedScale { get; private set; } = null!;

    /// <summary>
    /// The tile at the window's centre (D702): a view control, like the zoom, so <i>Reset</i> leaves it. Grass, grain,
    /// map and placeholders all read it, so they move together.
    /// </summary>
    internal Vector2 View { get; private set; }

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

        var note = new Label { Text = "splat and bleed approved; all off is the approved look", Modulate = new Color(1, 1, 1, 0.6f) };
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

        // Piece 2 (D702): the grain anchored to the land. Its strength stays the left panel's *Paper grain*.
        WorldGrain = new CheckBox { Name = "World-anchored grain", Text = "World-anchored grain", ButtonPressed = look.WorldGrainOn };
        WorldGrain.Toggled += on => _grainMaterial.SetShaderParameter("world_on", on);
        _grainMaterial.SetShaderParameter("world_on", look.WorldGrainOn);
        body.AddChild(WorldGrain);
        GrainScale = AddSlider(
            body,
            "Grain scale",
            0.02,
            0.5,
            0.005,
            look.GrainScaleTiles,
            v => _grainMaterial.SetShaderParameter("grain_scale_tiles", (float)v),
            v => $"{v.ToString("0.000", CultureInfo.InvariantCulture)} tiles");
        GrainUnder = new CheckBox { Name = "Grain under the village", Text = "Grain under the village", ButtonPressed = look.GrainUnderVillage };
        GrainUnder.Toggled += LayTheGrain;
        LayTheGrain(look.GrainUnderVillage);
        body.AddChild(GrainUnder);
        SetTheView(Vector2.Zero);

        // Piece 3 (D703): snow, its own layer with its own blend. Its colour is the file's, never a slider.
        Snow = AddSwitch(body, "Snow", "snow_on", look.SnowOn);
        SnowBlend = new OptionButton { Name = "Snow blend" };
        SnowBlend.AddItem("Lerp — covers");
        SnowBlend.AddItem("Screen — lifts, darks show");
        SnowBlend.AddItem("Lighten — the brighter wins");
        SnowBlend.ItemSelected += index => _material.SetShaderParameter("snow_blend", (int)index);
        body.AddChild(Row("Snow blend", SnowBlend, null));
        SnowAmount = AddSlider(body, "Snow amount", 0.0, 1.0, 0.01, look.SnowAmount, Sets("snow_amount"), Plain("0.00"));
        SnowPatchiness = AddSlider(body, "Snow patchiness", 0.0, 0.3, 0.01, look.SnowPatchiness, Sets("snow_patchiness"), Plain("0.00"));
        SnowPatchSize = AddSlider(
            body,
            "Snow patch size",
            2.0,
            48.0,
            1.0,
            look.SnowPatchTiles,
            Sets("snow_patch_tiles"),
            v => $"{v.ToString("0", CultureInfo.InvariantCulture)} tiles");
        SetTheSnowFromFile(look);

        // Piece 4 (D704): how the ground kinds meet. Both read the coverage map, so they show only with it on.
        Splat = AddSwitch(body, "Splat blending", "splat_on", look.SplatOn);
        SplatSharpness = AddSlider(body, "Splat sharpness", 1.0, 48.0, 0.5, look.SplatSharpness, Sets("splat_sharpness"), Plain("0.0"));
        Bleed = AddSwitch(body, "Edge bleed", "bleed_on", look.BleedOn);
        BleedReach = AddSlider(body, "Bleed reach", 0.0, 0.5, 0.01, look.BleedReachTiles, Sets("bleed_reach_tiles"), Tiles("0.00"));
        BleedScale = AddSlider(body, "Bleed scale", 0.25, 4.0, 0.05, look.BleedScaleTiles, Sets("bleed_scale_tiles"), Tiles("0.00"));

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

    /// <summary><b>c</b>: back to the village.</summary>
    private void CentreTheView() => SetTheView(Vector2.Zero);

    private void SwapTheCornerPanel()
    {
        _terrainShown = !_terrainShown;
        ShowTheCornerPanel();
    }

    /// <summary>The snow's blend and colour as the file says: the colour has no control, so a file edit shows on <i>Reset</i>.</summary>
    private void SetTheSnowFromFile(GrassLabTerrain look)
    {
        SnowBlend.Select(look.SnowBlend);
        _material.SetShaderParameter("snow_blend", look.SnowBlend);
        _material.SetShaderParameter("snow_srgb", Srgb(look.SnowColour));
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

    /// <summary>
    /// The grain over the placeholders (D630) or under them: its place among the children, ground first. Only the order
    /// changes, so over is the lab as it always was.
    /// </summary>
    private void LayTheGrain(bool under) => MoveChild(_grain, _ground.GetIndex() + (under ? 1 : 2));

    /// <summary>The view, on everything that draws the land.</summary>
    private void SetTheView(Vector2 tile)
    {
        View = tile.Clamp(-ViewLimit, ViewLimit);
        _material.SetShaderParameter("view_tile", View);
        _grainMaterial.SetShaderParameter("view_tile", View);
        Village.View = View;
    }

    /// <summary>Drag to pan: the land follows the pointer, at the zoom the slider says.</summary>
    public override void _GuiInput(InputEvent input)
    {
        if (input is InputEventMouseMotion motion && (motion.ButtonMask & (MouseButtonMask.Left | MouseButtonMask.Middle)) != 0)
        {
            SetTheView(View - (motion.Relative / (float)PixelsPerTile.Value));
            AcceptEvent();
        }
    }

    /// <summary>Every terrain control back to its file, read again so an edit shows without a restart. The view stays.</summary>
    private void ResetTheTerrain()
    {
        var look = (GrassLabTerrain)ResourceLoader.Load(GrassLabTerrain.LabPath, null, ResourceLoader.CacheMode.Replace);
        Coverage.ButtonPressed = look.CoverageOn;
        CoverageFade.Value = look.CoverageFade;
        WorldGrain.ButtonPressed = look.WorldGrainOn;
        GrainScale.Value = look.GrainScaleTiles;
        GrainUnder.ButtonPressed = look.GrainUnderVillage;
        Snow.ButtonPressed = look.SnowOn;
        SnowAmount.Value = look.SnowAmount;
        SnowPatchiness.Value = look.SnowPatchiness;
        SnowPatchSize.Value = look.SnowPatchTiles;
        SetTheSnowFromFile(look);
        Splat.ButtonPressed = look.SplatOn;
        SplatSharpness.Value = look.SplatSharpness;
        Bleed.ButtonPressed = look.BleedOn;
        BleedReach.Value = look.BleedReachTiles;
        BleedScale.Value = look.BleedScaleTiles;
    }

    /// <summary>The panel's options, as a <see cref="GrassLabTerrain"/>.</summary>
    internal GrassLabTerrain CurrentTerrain() => new()
    {
        CoverageOn = Coverage.ButtonPressed,
        CoverageFade = (float)CoverageFade.Value,
        WorldGrainOn = WorldGrain.ButtonPressed,
        GrainScaleTiles = (float)GrainScale.Value,
        GrainUnderVillage = GrainUnder.ButtonPressed,
        SnowOn = Snow.ButtonPressed,
        SnowBlend = SnowBlend.Selected,
        SnowAmount = (float)SnowAmount.Value,
        SnowPatchiness = (float)SnowPatchiness.Value,
        SnowPatchTiles = (float)SnowPatchSize.Value,
        SnowColour = SnowColourShown(),
        SplatOn = Splat.ButtonPressed,
        SplatSharpness = (float)SplatSharpness.Value,
        BleedOn = Bleed.ButtonPressed,
        BleedReachTiles = (float)BleedReach.Value,
        BleedScaleTiles = (float)BleedScale.Value,
    };

    /// <summary>A value said in tiles.</summary>
    private static Func<double, string> Tiles(string format) => v => $"{v.ToString(format, CultureInfo.InvariantCulture)} tiles";

    /// <summary>The colour the shader has, back as a <see cref="Color"/>, so <i>Copy</i> says what is drawn.</summary>
    private Color SnowColourShown()
    {
        Vector3 srgb = _material.GetShaderParameter("snow_srgb").AsVector3();
        return new Color(srgb.X, srgb.Y, srgb.Z);
    }

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

        var grainDeclared = new HashSet<string>();
        foreach (Godot.Collections.Dictionary uniform in lab._grainMaterial.Shader.GetShaderUniformList().Select(u => u.AsGodotDictionary()))
        {
            grainDeclared.Add(uniform["name"].AsString());
        }

        foreach (string uniform in GrainUniforms.Where(name => !grainDeclared.Contains(name)))
        {
            faults.Add($"paper_grain.gdshader declares no {uniform} ({grainDeclared.Count} uniforms in all; 0 is a shader that did not compile)");
        }

        yield return Line(
            "shader",
            $"grass_lab.gdshader and paper_grain.gdshader compile and declare all {TerrainUniforms.Length} and {GrainUniforms.Length} of the uniforms the lab sets",
            faults);

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
            || !said[1].Contains("- Coverage map: OFF. Fade ", StringComparison.Ordinal)
            || !said[1].Contains("\n- World-anchored grain: OFF. Scale ", StringComparison.Ordinal)
            || !said[1].Contains("\n- Snow: OFF. Blend Lerp. Amount ", StringComparison.Ordinal)
            || !said[1].Contains("\n- Splat blending: OFF. Sharpness ", StringComparison.Ordinal))
        {
            faults.Add($"Copy says \"{lab.CurrentValues()}\"");
        }

        yield return Line("copy", "Copy says Joe's list unchanged, then the options' own", faults);

        foreach (string line in GrainSelfCheck(lab))
        {
            yield return line;
        }

        foreach (string line in SnowSelfCheck(lab))
        {
            yield return line;
        }

        foreach (string line in SplatSelfCheck(lab))
        {
            yield return line;
        }

        yield return GroundCheck();
    }

    /// <summary>Piece 2's lines (D702): the grain's three controls and the pan. <paramref name="lab"/> is left at the approved look.</summary>
    private static IEnumerable<string> GrainSelfCheck(GrassLab lab)
    {
        float Grain(string name) => lab._grainMaterial.GetShaderParameter(name).AsSingle();
        bool GrainFlag(string name) => lab._grainMaterial.GetShaderParameter(name).AsBool();
        Vector2 At(ShaderMaterial material) => material.GetShaderParameter("view_tile").AsVector2();
        static string Line(string what, string good, List<string> faults) => faults.Count == 0
            ? $"[widths] terrain lab, {what}: ✅ {good}"
            : $"[widths] terrain lab, {what}: ❌ {string.Join("; ", faults)}";
        GrassLabTerrain start = GrassLabTerrain.StartingPoint;

        // World-anchored grain: the switch, and the zoom it reads.
        var faults = new List<string>();
        if (lab.WorldGrain.ButtonPressed || GrainFlag("world_on"))
        {
            faults.Add($"opens with the box {lab.WorldGrain.ButtonPressed}, the uniform {GrainFlag("world_on")} (want off)");
        }

        lab.WorldGrain.ButtonPressed = true;
        double zoom = lab.PixelsPerTile.Value;
        lab.PixelsPerTile.Value = 40.0;
        if (!GrainFlag("world_on") || Math.Abs(Grain("px_per_tile") - 40f) > 1e-6f)
        {
            faults.Add($"switched on: the uniform {GrainFlag("world_on")}, the grain at {Grain("px_per_tile")} px a tile when the zoom says 40");
        }

        // The grain turns UV into tiles as the grass does, so it needs the same rect. ⚠️ Inside the probe the rect has its
        // size but has not yet said so (Resized comes at layout), so the check raises it, as the window does.
        lab._ground.EmitSignal(Control.SignalName.Resized);
        Vector2 grainSize = lab._grainMaterial.GetShaderParameter("size_px").AsVector2();
        if (grainSize != lab._ground.Size || grainSize != lab._material.GetShaderParameter("size_px").AsVector2())
        {
            faults.Add($"the grain thinks the window is {grainSize}, the grass {lab._material.GetShaderParameter("size_px").AsVector2()}, and it is {lab._ground.Size}");
        }

        lab.PixelsPerTile.Value = zoom;
        lab.ResetToApproved();
        if (lab.WorldGrain.ButtonPressed || GrainFlag("world_on"))
        {
            faults.Add("Reset to approved left the world-anchored grain on");
        }

        yield return Line("world-anchored grain", "opens off, reaches the grain's shader, the grain follows the zoom and the window's size as the grass does, and Reset turns it off", faults);

        // Grain scale: the slider.
        faults = new List<string>();
        if (Math.Abs(Grain("grain_scale_tiles") - start.GrainScaleTiles) > 1e-6f)
        {
            faults.Add($"opens at {Grain("grain_scale_tiles")} (want the file's {start.GrainScaleTiles})");
        }

        lab.GrainScale.Value = 0.3;
        if (Math.Abs(Grain("grain_scale_tiles") - 0.3f) > 1e-6f)
        {
            faults.Add($"0.3 reached the shader as {Grain("grain_scale_tiles")}");
        }

        lab.ResetToApproved();
        if (Math.Abs(Grain("grain_scale_tiles") - start.GrainScaleTiles) > 1e-6f)
        {
            faults.Add($"Reset left it at {Grain("grain_scale_tiles")}");
        }

        yield return Line("grain scale", "opens at the file's value, reaches the shader, and Reset brings it back", faults);

        // Grain under the village: only the order of the children changes.
        faults = new List<string>();
        int ground = lab._ground.GetIndex();
        if (lab.GrainUnder.ButtonPressed || lab.Village.GetIndex() != ground + 1 || lab._grain.GetIndex() != ground + 2)
        {
            faults.Add($"opens with the box {lab.GrainUnder.ButtonPressed} and ground, village, grain at {ground}, {lab.Village.GetIndex()}, {lab._grain.GetIndex()} (want off, the grain over the village)");
        }

        lab.GrainUnder.ButtonPressed = true;
        if (lab._grain.GetIndex() != ground + 1 || lab.Village.GetIndex() != ground + 2 || lab._ground.GetIndex() != ground)
        {
            faults.Add($"switched on: ground, grain, village at {lab._ground.GetIndex()}, {lab._grain.GetIndex()}, {lab.Village.GetIndex()} (want the grain between the two)");
        }

        lab.ResetToApproved();
        if (lab.GrainUnder.ButtonPressed || lab._grain.GetIndex() != ground + 2)
        {
            faults.Add("Reset to approved left the grain under the village");
        }

        yield return Line("grain under the village", "opens over the placeholders as D630 lays it, on draws it under them, and Reset puts it back", faults);

        // The pan: one drag moves grass, grain and placeholders by the same tiles, and the view is the lab's, not the look's.
        faults = new List<string>();
        if (lab.View != Vector2.Zero || At(lab._material) != Vector2.Zero || At(lab._grainMaterial) != Vector2.Zero || lab.Village.View != Vector2.Zero)
        {
            faults.Add($"opens at {lab.View} (grass {At(lab._material)}, grain {At(lab._grainMaterial)}, village {lab.Village.View}; want the origin)");
        }

        float ppt = (float)lab.PixelsPerTile.Value;
        lab._GuiInput(new InputEventMouseMotion { Relative = new Vector2(3f, -2f) * ppt });
        if (lab.View != Vector2.Zero)
        {
            faults.Add($"a move with no button held panned to {lab.View}");
        }

        lab._GuiInput(new InputEventMouseMotion { Relative = new Vector2(3f, -2f) * ppt, ButtonMask = MouseButtonMask.Left });
        var want = new Vector2(-3f, 2f);
        if (lab.View.DistanceTo(want) > 1e-4f || At(lab._material) != lab.View || At(lab._grainMaterial) != lab.View || lab.Village.View != lab.View)
        {
            faults.Add($"a drag of 3 tiles right and 2 up reached {lab.View}: grass {At(lab._material)}, grain {At(lab._grainMaterial)}, village {lab.Village.View} (want {want} on all)");
        }

        // ⚠️ And the placeholders are drawn where the grass says: the view's tile at the window's centre.
        Vector2 middle = lab.Village.Size / 2f;
        if (lab.Village.ToScreen(lab.View).DistanceTo(middle) > 1e-3f || lab.Village.ToScreen(Vector2.Zero).DistanceTo(middle + (new Vector2(3f, -2f) * ppt)) > 1e-3f)
        {
            faults.Add($"the placeholders draw the view's tile at {lab.Village.ToScreen(lab.View)} and the origin at {lab.Village.ToScreen(Vector2.Zero)} (want {middle} and 3 tiles right, 2 up of it)");
        }

        lab.ResetToApproved();
        if (lab.View.DistanceTo(want) > 1e-4f)
        {
            faults.Add($"Reset moved the view to {lab.View} (it is the lab's, like the zoom)");
        }

        lab._GuiInput(new InputEventMouseMotion { Relative = new Vector2(-1e6f, 1e6f), ButtonMask = MouseButtonMask.Middle });
        if (lab.View != new Vector2(ViewLimit.X, -ViewLimit.Y))
        {
            faults.Add($"a drag off the world stopped at {lab.View} (want the fake map's corner {new Vector2(ViewLimit.X, -ViewLimit.Y)})");
        }

        lab._UnhandledKeyInput(new InputEventKey { Pressed = true, Keycode = Key.C });
        if (lab.View != Vector2.Zero || At(lab._material) != Vector2.Zero || At(lab._grainMaterial) != Vector2.Zero || lab.Village.View != Vector2.Zero)
        {
            faults.Add($"c left the view at {lab.View}");
        }

        yield return Line("pan", "opens at the origin; a drag moves grass, grain and placeholders by the same tiles, stops at the map's edge, survives Reset, and c centres it", faults);
    }

    /// <summary>Piece 3's lines (D703): the snow's switch, blend, three sliders and colour. <paramref name="lab"/> is left at the approved look.</summary>
    private static IEnumerable<string> SnowSelfCheck(GrassLab lab)
    {
        float Uniform(string name) => lab._material.GetShaderParameter(name).AsSingle();
        bool Flag(string name) => lab._material.GetShaderParameter(name).AsBool();
        int Blend() => lab._material.GetShaderParameter("snow_blend").AsInt32();
        static string Line(string what, string good, List<string> faults) => faults.Count == 0
            ? $"[widths] terrain lab, {what}: ✅ {good}"
            : $"[widths] terrain lab, {what}: ❌ {string.Join("; ", faults)}";
        GrassLabTerrain start = GrassLabTerrain.StartingPoint;

        // Snow: the switch, and the colour, which is the file's and §2's.
        var faults = new List<string>();
        if (lab.Snow.ButtonPressed || Flag("snow_on"))
        {
            faults.Add($"opens with the box {lab.Snow.ButtonPressed}, the uniform {Flag("snow_on")} (want off)");
        }

        lab.Snow.ButtonPressed = true;
        if (!Flag("snow_on"))
        {
            faults.Add("switched on, the uniform stayed off");
        }

        Vector3 colour = lab._material.GetShaderParameter("snow_srgb").AsVector3();
        if (colour != Srgb(start.SnowColour) || start.SnowColour.ToHtml(false) != "f4f1ea")
        {
            faults.Add($"the shader's snow is {colour}, the file's #{start.SnowColour.ToHtml(false)} (want the file's, and §2's #f4f1ea)");
        }

        lab.ResetToApproved();
        if (lab.Snow.ButtonPressed || Flag("snow_on"))
        {
            faults.Add("Reset to approved left the snow on");
        }

        yield return Line("snow", "opens off, reaches the shader, draws the file's #f4f1ea, and Reset turns it off", faults);

        // Snow blend: three, in the shader's order, and the file's at opening.
        faults = new List<string>();
        if (lab.SnowBlend.ItemCount != GrassLabTerrain.SnowBlends.Length || lab.SnowBlend.Selected != start.SnowBlend || Blend() != start.SnowBlend)
        {
            faults.Add($"opens with {lab.SnowBlend.ItemCount} blends at {lab.SnowBlend.Selected}, the shader at {Blend()} (want 3, the file's {start.SnowBlend} on both)");
        }

        for (int i = 0; i < GrassLabTerrain.SnowBlends.Length; i++)
        {
            lab.SnowBlend.Select(i);
            lab.SnowBlend.EmitSignal(OptionButton.SignalName.ItemSelected, (long)i);
            if (Blend() != i || !lab.SnowBlend.GetItemText(i).StartsWith(GrassLabTerrain.SnowBlends[i], StringComparison.Ordinal))
            {
                faults.Add($"\"{lab.SnowBlend.GetItemText(i)}\" reached the shader as {Blend()} (want {i}, {GrassLabTerrain.SnowBlends[i]})");
            }
        }

        lab.ResetToApproved();
        if (lab.SnowBlend.Selected != start.SnowBlend || Blend() != start.SnowBlend)
        {
            faults.Add($"Reset left the blend at {lab.SnowBlend.Selected}, the shader at {Blend()}");
        }

        yield return Line("snow blend", "opens at the file's Lerp, each of Lerp, Screen and Lighten reaches the shader as its own number, and Reset brings it back", faults);

        // The three sliders.
        foreach ((string what, HSlider slider, string uniform, float file, double probe) in new[]
        {
            ("snow amount", lab.SnowAmount, "snow_amount", start.SnowAmount, 0.25),
            ("snow patchiness", lab.SnowPatchiness, "snow_patchiness", start.SnowPatchiness, 0.2),
            ("snow patch size", lab.SnowPatchSize, "snow_patch_tiles", start.SnowPatchTiles, 30.0),
        })
        {
            faults = new List<string>();
            if (Math.Abs(Uniform(uniform) - file) > 1e-6f)
            {
                faults.Add($"opens at {Uniform(uniform)} (want the file's {file})");
            }

            slider.Value = probe;
            if (Math.Abs(Uniform(uniform) - (float)probe) > 1e-6f)
            {
                faults.Add($"{probe} reached the shader as {Uniform(uniform)}");
            }

            lab.ResetToApproved();
            if (Math.Abs(Uniform(uniform) - file) > 1e-6f)
            {
                faults.Add($"Reset left it at {Uniform(uniform)}");
            }

            yield return Line(what, "opens at the file's value, reaches the shader, and Reset brings it back", faults);
        }
    }

    /// <summary>Piece 4's lines (D704): two switches and three sliders. <paramref name="lab"/> is left at the approved look.</summary>
    private static IEnumerable<string> SplatSelfCheck(GrassLab lab)
    {
        float Uniform(string name) => lab._material.GetShaderParameter(name).AsSingle();
        bool Flag(string name) => lab._material.GetShaderParameter(name).AsBool();
        static string Line(string what, string good, List<string> faults) => faults.Count == 0
            ? $"[widths] terrain lab, {what}: ✅ {good}"
            : $"[widths] terrain lab, {what}: ❌ {string.Join("; ", faults)}";
        GrassLabTerrain start = GrassLabTerrain.StartingPoint;

        foreach ((string what, CheckBox box, string uniform) in new[]
        {
            ("splat blending", lab.Splat, "splat_on"),
            ("edge bleed", lab.Bleed, "bleed_on"),
        })
        {
            var faults = new List<string>();
            if (box.ButtonPressed || Flag(uniform))
            {
                faults.Add($"opens with the box {box.ButtonPressed}, the uniform {Flag(uniform)} (want off)");
            }

            box.ButtonPressed = true;
            if (!Flag(uniform))
            {
                faults.Add("switched on, the uniform stayed off");
            }

            lab.ResetToApproved();
            if (box.ButtonPressed || Flag(uniform))
            {
                faults.Add($"Reset to approved left {what} on");
            }

            yield return Line(what, "opens off, reaches the shader, and Reset turns it off", faults);
        }

        foreach ((string what, HSlider slider, string uniform, float file, double probe) in new[]
        {
            ("splat sharpness", lab.SplatSharpness, "splat_sharpness", start.SplatSharpness, 3.0),
            ("bleed reach", lab.BleedReach, "bleed_reach_tiles", start.BleedReachTiles, 0.4),
            ("bleed scale", lab.BleedScale, "bleed_scale_tiles", start.BleedScaleTiles, 2.5),
        })
        {
            var faults = new List<string>();
            if (Math.Abs(Uniform(uniform) - file) > 1e-6f)
            {
                faults.Add($"opens at {Uniform(uniform)} (want the file's {file})");
            }

            slider.Value = probe;
            if (Math.Abs(Uniform(uniform) - (float)probe) > 1e-6f)
            {
                faults.Add($"{probe} reached the shader as {Uniform(uniform)}");
            }

            lab.ResetToApproved();
            if (Math.Abs(Uniform(uniform) - file) > 1e-6f)
            {
                faults.Add($"Reset left it at {Uniform(uniform)}");
            }

            yield return Line(what, "opens at the file's value, reaches the shader, and Reset brings it back", faults);
        }

        // ⛔ The reach stops at half a tile (D342's rule, §3.2): the river is two tiles wide.
        if (lab.BleedReach.MaxValue > 0.5)
        {
            yield return $"[widths] terrain lab, bleed's limit: ❌ the reach goes to {lab.BleedReach.MaxValue} tiles (half a tile at most)";
        }
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

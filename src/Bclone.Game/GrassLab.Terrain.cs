using System.Globalization;
using System.Reflection;
using Godot;

namespace Bclone.Game;

/// <summary>
/// ⭐ The terrain shader's options in the lab (`specs/terrain-shader.md §4`, A1): each behind its own switch, off at
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
        "ground_wood", "ground_tex_on", "ground_tex_tiles", "ground_tex_antitile", "kind_young", "ground_young_preview",
        "ground_stone", "ground_water", "ground_map_iron", "kind_iron", "ground_iron",
    };

    /// <summary>Joe's woods tile (`specs/ground-textures.md §2`, A13): 2048², 64 × 64 tiles, lossless, mipmapped.</summary>
    internal const string WoodsTilePath = "res://textures/ground/ground_woods.png";

    /// <summary>The stone seam's tile (A16), as the woods'.</summary>
    internal const string StoneTilePath = "res://textures/ground/ground_stone.png";

    /// <summary>The water's tile (A16), as the woods'; the shallows are derived from it in the shader.</summary>
    internal const string WaterTilePath = "res://textures/ground/ground_water.png";

    /// <summary>The iron seam's tile (A20), as the woods'.</summary>
    internal const string IronTilePath = "res://textures/ground/ground_iron.png";

    /// <summary>Each painted kind's sampler and its tile, in the order the loader loads them and the probe asks.</summary>
    private static readonly (string Uniform, string Path)[] PaintedTiles =
    {
        ("ground_wood", WoodsTilePath), ("ground_stone", StoneTilePath), ("ground_water", WaterTilePath), ("ground_iron", IronTilePath),
    };

    /// <summary>Every uniform this file and the zoom set on the grain: the probe asks its compiled shader too.</summary>
    private static readonly string[] GrainUniforms = { "amount", "world_on", "grain_scale_tiles", "size_px", "px_per_tile", "view_tile" };

    /// <summary>How far the view may go: the fake map's own edge, so the village can always be found again.</summary>
    private static readonly Vector2 ViewLimit = new(GrassLabGround.TilesWide / 2f, GrassLabGround.TilesTall / 2f);

    private PanelContainer _terrain = null!;
    private ScrollContainer _terrainScroll = null!;
    private VBoxContainer _terrainBody = null!;
    private Label _gpuOnTerrain = null!;

    /// <summary>The panel's own top and bottom margins (its StyleBoxFlat) plus the 16 px it keeps from the window's edges.</summary>
    private const float TerrainChrome = 12f + 12f + 16f + 16f;
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

    internal CheckBox GroundTextures { get; private set; } = null!;

    internal HSlider TextureTiles { get; private set; } = null!;

    internal OptionButton TextureAntiTiling { get; private set; } = null!;

    internal CheckBox YoungWood { get; private set; } = null!;

    /// <summary>True once the painted tiles have been loaded — only when the textures are first switched on, as the map is baked.</summary>
    internal bool TilesLoaded { get; private set; }

    /// <summary>
    /// The tile at the window's centre (A3): a view control, like the zoom, so <i>Reset</i> leaves it. Grass, grain,
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
        _material.SetShaderParameter("kind_iron", Srgb(VillageMap.ColourOf(Bclone.Sim.World.Terrain.IronDeposit)));

        _terrain = Panel("Terrain");
        _terrain.AnchorLeft = 1f;
        _terrain.AnchorRight = 1f;
        _terrain.GrowHorizontal = GrowDirection.Begin;
        _terrain.OffsetLeft = -16f;
        _terrain.OffsetRight = -16f;
        _terrain.OffsetTop = 16f;
        AddChild(_terrain);

        // ⚠️ With piece 5 the panel outgrew a 1080-tall window, and a PanelContainer neither clips nor scrolls (Joe's first
        // look at A14). The body scrolls, capped to the window's height, so every control can be reached at any size.
        _terrainScroll = new ScrollContainer { Name = "TerrainScroll", HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled };
        _terrain.AddChild(_terrainScroll);
        var body = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        body.AddThemeConstantOverride("separation", 4);
        _terrainScroll.AddChild(body);
        _terrainBody = body;

        var heading = new Label { Text = "Terrain" };
        heading.AddThemeFontSizeOverride("font_size", 20);
        body.AddChild(heading);

        var note = new Label { Text = "all but the map approved; all off is the approved look", Modulate = new Color(1, 1, 1, 0.6f) };
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

        // Piece 2 (A3): the grain anchored to the land. Its strength stays the left panel's *Paper grain*.
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

        // Piece 3 (A4): snow, its own layer with its own blend. Its colour is the file's, never a slider.
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

        // Piece 4 (A5): how the ground kinds meet. Both read the coverage map, so they show only with it on.
        Splat = AddSwitch(body, "Splat blending", "splat_on", look.SplatOn);
        SplatSharpness = AddSlider(body, "Splat sharpness", 1.0, 48.0, 0.5, look.SplatSharpness, Sets("splat_sharpness"), Plain("0.0"));
        Bleed = AddSwitch(body, "Edge bleed", "bleed_on", look.BleedOn);
        BleedReach = AddSlider(body, "Bleed reach", 0.0, 0.5, 0.01, look.BleedReachTiles, Sets("bleed_reach_tiles"), Tiles("0.00"));
        BleedScale = AddSlider(body, "Bleed scale", 0.25, 4.0, 0.05, look.BleedScaleTiles, Sets("bleed_scale_tiles"), Tiles("0.00"));

        // Piece 5 (A14, `ground-textures.md §5` pieces 1–3, A17, A20): Joe's painted woods, stone, iron and water tiles for
        // kind_wood, kind_stone, kind_iron and kind_water — one switch for all four, the shallows derived from the water in the shader. Needs the
        // coverage map to have a ground to draw on. Young wood is derived from it by the palette's ratio, so the ratio's other half is set here.
        _material.SetShaderParameter("kind_young", Srgb(VillageMap.ColourOf(Bclone.Sim.World.Terrain.Sapling)));
        GroundTextures = AddSwitch(body, "Ground textures", "ground_tex_on", false);
        GroundTextures.Toggled += on =>
        {
            if (on)
            {
                LoadTheTiles();
            }
        };
        GroundTextures.ButtonPressed = look.GroundTexturesOn;
        var needsTheMap = new Label { Text = "draws on the coverage map's ground: tick Coverage map", Modulate = new Color(1, 1, 1, 0.6f) };
        needsTheMap.AddThemeFontSizeOverride("font_size", 12);
        body.AddChild(needsTheMap);
        TextureTiles = AddSlider(body, "Texture tiles per repeat", 16.0, 128.0, 1.0, look.TextureTilesPerRepeat, Sets("ground_tex_tiles"), Plain("0"));
        TextureAntiTiling = new OptionButton { Name = "Texture anti-tiling" };
        TextureAntiTiling.AddItem("Off — as painted");
        TextureAntiTiling.AddItem("Hex — turned and shifted");
        TextureAntiTiling.ItemSelected += index => _material.SetShaderParameter("ground_tex_antitile", (int)index);
        body.AddChild(Row("Texture anti-tiling", TextureAntiTiling, null));
        SetTheAntiTiling(look.TextureAntiTiling);
        YoungWood = AddSwitch(body, "Young wood preview", "ground_young_preview", look.YoungWoodPreview);

        _gpuOnTerrain = new Label { Name = "GpuOnTerrain", Modulate = new Color(1, 1, 1, 0.75f) };
        _gpuOnTerrain.AddThemeFontSizeOverride("font_size", 13);
        body.AddChild(_gpuOnTerrain);

        // ⚠️ Follows the left panel, so `lab_render.gd` and `lab_cost.gd`, which hide it and *Tiling* by name, need no change.
        _panel.VisibilityChanged += ShowTheCornerPanel;
        ShowTheCornerPanel();
        _ground.Resized += FitTheCornerPanel;
        FitTheCornerPanel();
    }

    private void FitTheCornerPanel() => FitTheCornerPanel(GetViewportRect().Size.Y);

    /// <summary>
    /// The Terrain panel's scroll is as tall as its body, or as tall as a window <paramref name="windowHeight"/> px high
    /// allows, whichever is less: the panel hugs its content on a tall screen and scrolls on a short one. Minimum sizes,
    /// not laid-out sizes, so the probe can ask before a frame has drawn, and with any window height it likes.
    /// </summary>
    private void FitTheCornerPanel(float windowHeight)
    {
        float room = Mathf.Max(windowHeight - TerrainChrome, 120f);
        _terrainScroll.CustomMinimumSize = new Vector2(0f, Mathf.Min(_terrainBody.GetCombinedMinimumSize().Y, room));
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

    /// <summary>The painting's anti-tiling, option and uniform together.</summary>
    private void SetTheAntiTiling(int which)
    {
        TextureAntiTiling.Select(which);
        _material.SetShaderParameter("ground_tex_antitile", which);
    }

    /// <summary>Joe's woods, stone, water and iron tiles, loaded once on first switching the textures on: ~21 MB of VRAM each with its mipmaps.</summary>
    private void LoadTheTiles()
    {
        if (TilesLoaded)
        {
            return;
        }

        foreach ((string uniform, string path) in PaintedTiles)
        {
            _material.SetShaderParameter(uniform, GD.Load<Texture2D>(path));
        }

        TilesLoaded = true;
    }

    /// <summary>The fake map, baked once on first switching it on (0.44 s, measured), so the lab opens without paying for it.</summary>
    private void LoadTheGround()
    {
        if (GroundLoaded)
        {
            return;
        }

        _material.SetShaderParameter("ground_map", GrassLabGround.Texture());
        _material.SetShaderParameter("ground_map_iron", GrassLabGround.IronTexture());
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
        GroundTextures.ButtonPressed = look.GroundTexturesOn;
        TextureTiles.Value = look.TextureTilesPerRepeat;
        SetTheAntiTiling(look.TextureAntiTiling);
        YoungWood.ButtonPressed = look.YoungWoodPreview;
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
        GroundTexturesOn = GroundTextures.ButtonPressed,
        TextureTilesPerRepeat = (float)TextureTiles.Value,
        TextureAntiTiling = TextureAntiTiling.Selected,
        YoungWoodPreview = YoungWood.ButtonPressed,
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
    /// The probe's lines for the terrain (A1), one a control. <paramref name="lab"/> stands at the approved look, and
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

        // The body scrolls inside the window (Joe's 1080-tall window cut piece 5's controls off): the scroll's height is
        // the body's, or the window's room, whichever is less. Asked at the probe's own window and at a 600 px one, where
        // the body cannot fit and the cap must bite; then left at the probe's.
        float body = lab._terrainBody.GetCombinedMinimumSize().Y;
        float own = lab.GetViewportRect().Size.Y;
        foreach (float window in new[] { own, 600f, own })
        {
            lab.FitTheCornerPanel(window);
            float room = window - TerrainChrome;
            float cap = lab._terrainScroll.CustomMinimumSize.Y;
            if (Math.Abs(cap - Math.Min(body, room)) > 0.5f)
            {
                faults.Add($"in a {window} px window the Terrain panel's scroll is {cap} px tall for a body of {body} px and {room} px of room (want the smaller)");
            }
        }

        if (body <= 600f - TerrainChrome)
        {
            faults.Add($"the body is only {body} px, so the 600 px window does not test the cap");
        }

        yield return Line("panels", $"the corner opens on Terrain, swaps with Anti-tiling, follows the left panel, and its body ({body:0} px) is capped to the window's height ({own - TerrainChrome:0} px of room here, {600f - TerrainChrome:0} in a 600 px window)", faults);

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

        var ironMap = lab._material.GetShaderParameter("ground_map_iron").As<Texture2D>();
        if (ironMap is null || ironMap.GetWidth() != GrassLabGround.ImageWide || ironMap.GetHeight() != GrassLabGround.ImageTall)
        {
            faults.Add($"switched on, the seam's kind is a map of {ironMap?.GetWidth()} x {ironMap?.GetHeight()} (want the map's {GrassLabGround.ImageWide} x {GrassLabGround.ImageTall})");
        }

        if (lab._material.GetShaderParameter("kind_water").AsVector3() != Srgb(VillageMap.ColourOf(Bclone.Sim.World.Terrain.Water))
            || lab._material.GetShaderParameter("kind_wood").AsVector3() != Srgb(VillageMap.ColourOf(Bclone.Sim.World.Terrain.Forest))
            || lab._material.GetShaderParameter("kind_stone").AsVector3() != Srgb(VillageMap.ColourOf(Bclone.Sim.World.Terrain.Rock))
            || lab._material.GetShaderParameter("kind_shallows").AsVector3() != Srgb(VillageMap.ShallowsColour)
            || lab._material.GetShaderParameter("kind_iron").AsVector3() != Srgb(VillageMap.ColourOf(Bclone.Sim.World.Terrain.IronDeposit)))
        {
            faults.Add("the ground kinds are not VillageMap's palette");
        }

        lab.ResetToApproved();
        if (lab.Coverage.ButtonPressed || Flag("coverage_on"))
        {
            faults.Add("Reset to approved left the coverage map on");
        }

        yield return Line("coverage map", "opens off and unbaked, reaches the shader with a map and its seams' kind at the right size over VillageMap's palette, iron's included, and Reset turns it off", faults);

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
            || !said[1].Contains("\n- Splat blending: OFF. Sharpness ", StringComparison.Ordinal)
            || !said[1].Contains("\n- Ground textures: OFF. Tiles per repeat 64. Anti-tiling Off. Young wood preview: OFF", StringComparison.Ordinal))
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

        foreach (string line in GroundTextureSelfCheck(lab))
        {
            yield return line;
        }

        yield return GroundCheck();
    }

    /// <summary>Piece 5's lines (A14): the textures' switch and loader, the tiles slider, the anti-tiling, the young-wood preview, and the tile's import. <paramref name="lab"/> is left at the approved look.</summary>
    private static IEnumerable<string> GroundTextureSelfCheck(GrassLab lab)
    {
        float Uniform(string name) => lab._material.GetShaderParameter(name).AsSingle();
        bool Flag(string name) => lab._material.GetShaderParameter(name).AsBool();
        int AntiTiling() => lab._material.GetShaderParameter("ground_tex_antitile").AsInt32();
        static string Line(string what, string good, List<string> faults) => faults.Count == 0
            ? $"[widths] terrain lab, {what}: ✅ {good}"
            : $"[widths] terrain lab, {what}: ❌ {string.Join("; ", faults)}";
        GrassLabTerrain start = GrassLabTerrain.StartingPoint;

        // Ground textures: the switch, and the loader behind it.
        var faults = new List<string>();
        if (lab.GroundTextures.ButtonPressed || Flag("ground_tex_on") || lab.TilesLoaded)
        {
            faults.Add($"opens with the box {lab.GroundTextures.ButtonPressed}, the uniform {Flag("ground_tex_on")}, the tiles loaded {lab.TilesLoaded} (want off, off, not loaded)");
        }

        lab.GroundTextures.ButtonPressed = true;
        if (!Flag("ground_tex_on") || !lab.TilesLoaded)
        {
            faults.Add($"switched on: the uniform {Flag("ground_tex_on")}, loaded {lab.TilesLoaded} (want on, loaded)");
        }

        // ⚠️ Its own list, never the loader's PaintedTiles: a tile dropped from that list would drop its check with it
        // (red-checked, A17 — the first version scored zero on exactly that).
        foreach ((string uniform, string file) in new[] { ("ground_wood", "ground_woods.png"), ("ground_stone", "ground_stone.png"), ("ground_water", "ground_water.png"), ("ground_iron", "ground_iron.png") })
        {
            var tile = lab._material.GetShaderParameter(uniform).As<Texture2D>();
            if (tile is null || tile.ResourcePath != $"res://textures/ground/{file}" || tile.GetWidth() != 2048 || tile.GetHeight() != 2048)
            {
                faults.Add($"switched on, {uniform} holds {tile?.ResourcePath ?? "no tile"} of {tile?.GetWidth()} x {tile?.GetHeight()} (want res://textures/ground/{file} at 2048 x 2048)");
            }
        }

        Vector3 young = lab._material.GetShaderParameter("kind_young").AsVector3();
        if (young != Srgb(VillageMap.ColourOf(Bclone.Sim.World.Terrain.Sapling)) || VillageMap.ColourOf(Bclone.Sim.World.Terrain.Sapling).ToHtml(false) != "b3b98d")
        {
            faults.Add($"young wood's colour is {young}, the palette's #{VillageMap.ColourOf(Bclone.Sim.World.Terrain.Sapling).ToHtml(false)} (want the palette's, and §1's #b3b98d)");
        }

        lab.ResetToApproved();
        if (lab.GroundTextures.ButtonPressed || Flag("ground_tex_on"))
        {
            faults.Add("Reset to approved left the ground textures on");
        }

        yield return Line("ground textures", $"opens off and unloaded, switched on loads the woods, stone, water and iron tiles at 2048 x 2048 each into their samplers over the palette's young wood, and Reset turns it off", faults);

        // Texture tiles per repeat: the slider, and 64 is the painting's own scale.
        faults = new List<string>();
        if (Math.Abs(Uniform("ground_tex_tiles") - start.TextureTilesPerRepeat) > 1e-6f || Math.Abs(start.TextureTilesPerRepeat - 64f) > 1e-6f)
        {
            faults.Add($"opens at {Uniform("ground_tex_tiles")} (want the file's {start.TextureTilesPerRepeat}, and the painting's 64)");
        }

        lab.TextureTiles.Value = 32.0;
        if (Math.Abs(Uniform("ground_tex_tiles") - 32f) > 1e-6f)
        {
            faults.Add($"32 reached the shader as {Uniform("ground_tex_tiles")}");
        }

        lab.ResetToApproved();
        if (Math.Abs(Uniform("ground_tex_tiles") - start.TextureTilesPerRepeat) > 1e-6f)
        {
            faults.Add($"Reset left it at {Uniform("ground_tex_tiles")}");
        }

        yield return Line("texture tiles per repeat", "opens at the file's 64, the painting's own scale, reaches the shader, and Reset brings it back", faults);

        // Texture anti-tiling: Off and Hex, in the shader's order, the file's at opening.
        faults = new List<string>();
        if (lab.TextureAntiTiling.ItemCount != GrassLabTerrain.TextureAntiTilings.Length || lab.TextureAntiTiling.Selected != start.TextureAntiTiling || AntiTiling() != start.TextureAntiTiling)
        {
            faults.Add($"opens with {lab.TextureAntiTiling.ItemCount} options at {lab.TextureAntiTiling.Selected}, the shader at {AntiTiling()} (want 2, the file's {start.TextureAntiTiling} on both)");
        }

        for (int i = 0; i < GrassLabTerrain.TextureAntiTilings.Length; i++)
        {
            lab.TextureAntiTiling.Select(i);
            lab.TextureAntiTiling.EmitSignal(OptionButton.SignalName.ItemSelected, (long)i);
            if (AntiTiling() != i || !lab.TextureAntiTiling.GetItemText(i).StartsWith(GrassLabTerrain.TextureAntiTilings[i], StringComparison.Ordinal))
            {
                faults.Add($"\"{lab.TextureAntiTiling.GetItemText(i)}\" reached the shader as {AntiTiling()} (want {i}, {GrassLabTerrain.TextureAntiTilings[i]})");
            }
        }

        lab.ResetToApproved();
        if (lab.TextureAntiTiling.Selected != start.TextureAntiTiling || AntiTiling() != start.TextureAntiTiling)
        {
            faults.Add($"Reset left the anti-tiling at {lab.TextureAntiTiling.Selected}, the shader at {AntiTiling()}");
        }

        yield return Line("texture anti-tiling", "opens at the file's Off, each of Off and Hex reaches the shader as its own number, and Reset brings it back", faults);

        // Young wood preview: the switch.
        faults = new List<string>();
        if (lab.YoungWood.ButtonPressed || Flag("ground_young_preview"))
        {
            faults.Add($"opens with the box {lab.YoungWood.ButtonPressed}, the uniform {Flag("ground_young_preview")} (want off)");
        }

        lab.YoungWood.ButtonPressed = true;
        if (!Flag("ground_young_preview"))
        {
            faults.Add("switched on, the uniform stayed off");
        }

        lab.ResetToApproved();
        if (lab.YoungWood.ButtonPressed || Flag("ground_young_preview"))
        {
            faults.Add("Reset to approved left the young wood preview on");
        }

        yield return Line("young wood preview", "opens off, reaches the shader, and Reset turns it off", faults);

        // The tile's import (`ground-textures.md §4`): lossless and mipmapped, level 0 byte for byte the PNG, as the masks are.
        faults = new List<string>();
        ImportHolds(WoodsTilePath, mipmapped: true, Image.Format.Rgb8, faults);
        yield return Line("woods tile import", $"{WoodsTilePath} imports mipmapped and lossless, byte for byte its PNG", faults);

        faults = new List<string>();
        ImportHolds(StoneTilePath, mipmapped: true, Image.Format.Rgb8, faults);
        yield return Line("stone tile import", $"{StoneTilePath} imports mipmapped and lossless, byte for byte its PNG", faults);

        faults = new List<string>();
        ImportHolds(WaterTilePath, mipmapped: true, Image.Format.Rgb8, faults);
        yield return Line("water tile import", $"{WaterTilePath} imports mipmapped and lossless, byte for byte its PNG", faults);

        faults = new List<string>();
        ImportHolds(IronTilePath, mipmapped: true, Image.Format.Rgb8, faults);
        yield return Line("iron tile import", $"{IronTilePath} imports mipmapped and lossless, byte for byte its PNG", faults);
    }

    /// <summary>Piece 2's lines (A3): the grain's three controls and the pan. <paramref name="lab"/> is left at the approved look.</summary>
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

    /// <summary>Piece 3's lines (A4): the snow's switch, blend, three sliders and colour. <paramref name="lab"/> is left at the approved look.</summary>
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
        if (colour != Srgb(start.SnowColour) || start.SnowColour.ToHtml(false) != "f8f6f1")
        {
            faults.Add($"the shader's snow is {colour}, the file's #{start.SnowColour.ToHtml(false)} (want the file's, and §2's #f8f6f1)");
        }

        lab.ResetToApproved();
        if (lab.Snow.ButtonPressed || Flag("snow_on"))
        {
            faults.Add("Reset to approved left the snow on");
        }

        yield return Line("snow", "opens off, reaches the shader, draws the file's #f8f6f1, and Reset turns it off", faults);

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

    /// <summary>Piece 4's lines (A5): two switches and three sliders. <paramref name="lab"/> is left at the approved look.</summary>
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
        byte[] iron = GrassLabGround.BakeIron();
        if (!first.AsSpan().SequenceEqual(GrassLabGround.Bake()) || !iron.AsSpan().SequenceEqual(GrassLabGround.BakeIron()))
        {
            faults.Add("two bakes disagree, so a place does not always look the same");
        }

        // Grass, wood, stone, iron, water: the seam (B) is stone or iron as the second map says (A20).
        var kinds = new bool[5];
        for (int i = 0; i < first.Length; i += 4)
        {
            if (first[i] + first[i + 1] + first[i + 2] + first[i + 3] != 255)
            {
                faults.Add($"texel {i / 4}'s weights sum to {first[i] + first[i + 1] + first[i + 2] + first[i + 3]}");
                break;
            }

            bool isIron = iron[i / 4] == 255;
            kinds[0] |= first[i] > 200;
            kinds[1] |= first[i + 1] > 200;
            kinds[2] |= first[i + 2] > 200 && !isIron;
            kinds[3] |= first[i + 2] > 200 && isIron;
            kinds[4] |= first[i + 3] > 200;
        }

        if (Array.IndexOf(kinds, false) >= 0)
        {
            faults.Add($"a kind never wins: grass, wood, stone, iron, water = {string.Join(", ", kinds)}");
        }

        // ⛔ The seam's kind is a pick, so it must not change within a texel of anywhere a seam is drawn from: bilinear
        // reads a texel's neighbours, and an iron beside a stone would draw half of each at the line.
        int mixed = 0;
        for (int y = 0; y < GrassLabGround.ImageTall; y++)
        {
            for (int x = 0; x < GrassLabGround.ImageWide; x++)
            {
                int at = (y * GrassLabGround.ImageWide) + x;
                if (iron[at] is not (0 or 255))
                {
                    mixed++;
                }
                else if (first[(at * 4) + 2] > 0 && !OneSeamRound(iron, x, y))
                {
                    mixed++;
                }
            }
        }

        if (mixed > 0)
        {
            faults.Add($"{mixed} seam texels have stone and iron within a texel of them (the kind must be one or the other wherever a seam is drawn)");
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
            ? "[widths] terrain lab, fake map: ✅ a pure function of the tile, every texel's weights sum to one, all five kinds win "
                + $"somewhere, every seam all stone or all iron a texel round, the placeholders stand on grass and their wood on a wood, the river never under two tiles (narrowest {narrowest:0.00})"
            : $"[widths] terrain lab, fake map: ❌ {string.Join("; ", faults)}";
    }

    /// <summary>True when the seam's kind is the same at a texel and its eight neighbours (clamped at the edge, as the sampler is).</summary>
    private static bool OneSeamRound(byte[] iron, int x, int y)
    {
        byte kind = iron[(y * GrassLabGround.ImageWide) + x];
        for (int dy = -1; dy <= 1; dy++)
        {
            for (int dx = -1; dx <= 1; dx++)
            {
                int nx = Math.Clamp(x + dx, 0, GrassLabGround.ImageWide - 1);
                int ny = Math.Clamp(y + dy, 0, GrassLabGround.ImageTall - 1);
                if (iron[(ny * GrassLabGround.ImageWide) + nx] != kind)
                {
                    return false;
                }
            }
        }

        return true;
    }
}

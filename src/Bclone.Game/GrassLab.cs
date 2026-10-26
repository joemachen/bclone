using System.Globalization;
using System.Reflection;
using Godot;

namespace Bclone.Game;

/// <summary>
/// ⭐ THE SCENE — the seasonal grass on its own, before it goes anywhere near the valley
/// (`specs/seasonal-grass.md §5`, D628).
/// </summary>
/// <remarks>
/// <para>
/// <b>Parchment under the grass, not the game's slate</b> (§6.0): the pack's colours are Multiply over parchment
/// <c>#e6d6b0</c>, and over <c>VillageMap.Ground</c> they turn near black. Whether the game moves to parchment is
/// its own decision, made after this scene has been looked at.
/// </para>
/// <para>
/// One full-window rect with one material (<c>shaders/grass_lab.gdshader</c>, which includes the pack's snippet
/// unchanged), and a panel of sliders that only set uniforms — nothing is rebuilt when a slider moves. Only the
/// version on show is loaded (§6.5). Launched with <c>.\run.bat grass</c>; <b>H</b> hides the panels.
/// </para>
/// <para>
/// ⭐⭐ <b>It opens at the approved look</b> (D632, `specs/grass-look.md`): every look value comes from
/// <see cref="GrassLook.Approved"/> — none is written here. <i>Reset to approved</i> goes back to it, and <i>Copy current
/// values</i> puts the panel's settings on the clipboard in the list Joe approved, so a new set can be pasted back.
/// Only the view's own controls — the season, the zoom, the placeholders, the marks — are the lab's.
/// </para>
/// <para>
/// ⭐ <b>Joe's three after the first look (D630):</b> a <i>strength</i> that fades the grass to bare parchment; a
/// <i>paper grain</i> multiplied over everything, because grain knocks the contrast down and changes how loud the
/// grass reads; and <i>placeholders</i> — the game's own buildings, trees and villagers (<see cref="GrassLabVillage"/>)
/// — so the grass is judged under what will stand on it. Grass and village count tiles from the window's centre, so
/// they zoom together.
/// </para>
/// </remarks>
public sealed partial class GrassLab : Control
{
    /// <summary>v06 first, v04 the first alternative (Joe, §5); v01 is retired and not in the project.</summary>
    internal static readonly string[] Versions = { "v06", "v04", "v02", "v03", "v05" };

    /// <summary>Summer: the season the lab opens on. A view control, not part of the look.</summary>
    internal const double DefaultSeason = 1.0;

    /// <summary>
    /// The zoom the lab opens at, in screen pixels a tile — between the game's fit-to-width (~13 at 1280 wide) and
    /// <c>MaxPixelsPerTile = 48</c>, so a repeat and a half is on screen and the detail still reads. A view control.
    /// </summary>
    internal const double DefaultPixelsPerTile = 24.0;

    /// <summary>The five layouts version mixing draws on, in the shader's order <c>grass_v0</c> … <c>grass_v4</c>.</summary>
    internal static readonly string[] Mixed = { "v02", "v03", "v04", "v05", "v06" };

    internal static readonly Color Parchment = new("#e6d6b0");

    private static readonly string[] SeasonNames = { "spring", "summer", "fall", "winter" };

    private readonly ShaderMaterial _material = new() { Shader = GD.Load<Shader>("res://shaders/grass_lab.gdshader") };
    private readonly ShaderMaterial _grainMaterial = new() { Shader = GD.Load<Shader>("res://shaders/paper_grain.gdshader") };
    private ColorRect _ground = null!;
    private ColorRect _grain = null!;
    private PanelContainer _panel = null!;
    private Texture2D? _masks;
    private PanelContainer _tiling = null!;
    private Label _gpu = null!;
    private Label _copied = null!;
    private double _gpuMs;
    private double _gpuSaidAt;

    internal string ShownVersion { get; private set; } = string.Empty;

    internal OptionButton Version { get; private set; } = null!;

    internal HSlider Season { get; private set; } = null!;

    internal HSlider Strength { get; private set; } = null!;

    internal HSlider Jitter { get; private set; } = null!;

    internal HSlider Grain { get; private set; } = null!;

    internal HSlider TilesPerRepeat { get; private set; } = null!;

    internal HSlider PixelsPerTile { get; private set; } = null!;

    internal CheckBox Placeholders { get; private set; } = null!;

    internal GrassLabVillage Village { get; private set; } = null!;

    internal CheckBox Macro { get; private set; } = null!;

    internal HSlider MacroCycle { get; private set; } = null!;

    internal HSlider MacroStrength { get; private set; } = null!;

    internal HSlider MacroSeason { get; private set; } = null!;

    internal CheckBox Mixing { get; private set; } = null!;

    internal HSlider Region { get; private set; } = null!;

    internal HSlider Width { get; private set; } = null!;

    internal CheckBox Hex { get; private set; } = null!;

    internal HSlider Sharpness { get; private set; } = null!;

    internal OptionButton Blend { get; private set; } = null!;

    internal CheckBox MarkTransitions { get; private set; } = null!;

    /// <summary>True once version mixing has loaded all five layouts — only when it is first switched on.</summary>
    internal bool MixedLoaded { get; private set; }

    public override void _Ready()
    {
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        GrassLook look = GrassLook.Approved;

        _material.SetShaderParameter("grass_season_atlas", GD.Load<Texture2D>("res://textures/grass/grass_seasons.png"));
        _material.SetShaderParameter("ground_srgb", new Vector3(Parchment.R, Parchment.G, Parchment.B));

        _ground = new ColorRect { Name = "Ground", Material = _material, MouseFilter = MouseFilterEnum.Ignore };
        _ground.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        AddChild(_ground);
        _ground.Resized += () => _material.SetShaderParameter("size_px", _ground.Size);

        // The placeholder village stands on the grass, and the paper's grain lies over both.
        Village = new GrassLabVillage { Name = "Village" };
        Village.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        AddChild(Village);

        _grain = new ColorRect { Name = "Grain", Material = _grainMaterial, MouseFilter = MouseFilterEnum.Ignore };
        _grain.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        AddChild(_grain);

        _panel = Panel("Panel");
        _panel.Position = new Vector2(16, 16);
        AddChild(_panel);

        var body = new VBoxContainer();
        body.AddThemeConstantOverride("separation", 6);
        _panel.AddChild(body);

        var heading = new Label { Text = "Seasonal grass" };
        heading.AddThemeFontSizeOverride("font_size", 20);
        body.AddChild(heading);

        Version = new OptionButton { Name = "Version" };
        foreach (string version in Versions)
        {
            Version.AddItem(version);
        }

        Version.ItemSelected += index => ShowVersion(Versions[index]);
        body.AddChild(Row("Version", Version, null));

        Season = AddSlider(body, "Season", 0.0, 4.0, 0.01, DefaultSeason, Sets("grass_season"), SeasonSays);
        Strength = AddSlider(body, "Strength", 0.0, 1.0, 0.01, look.Strength, Sets("grass_strength"), Plain("0.00"));
        Jitter = AddSlider(body, "Jitter", 0.0, 0.25, 0.005, look.Jitter, Sets("grass_jitter"), Plain("0.000"));
        Grain = AddSlider(body, "Paper grain", 0.0, 0.4, 0.01, look.PaperGrain, SetTheGrain, Plain("0.00"));
        TilesPerRepeat = AddSlider(body, "Tiles per repeat", 8.0, 128.0, 1.0, look.TilesPerRepeat, Sets("tiles_per_repeat"), Plain("0"));
        PixelsPerTile = AddSlider(
            body,
            "Pixels per tile",
            13.0,
            48.0,
            1.0,
            DefaultPixelsPerTile,
            v =>
            {
                _material.SetShaderParameter("px_per_tile", (float)v);
                Village.PixelsPerTile = (float)v;
            },
            Plain("0"));

        Placeholders = new CheckBox { Name = "Placeholders", Text = "Placeholders", ButtonPressed = true };
        Placeholders.Toggled += on => Village.Visible = on;
        body.AddChild(Placeholders);

        var buttons = new HBoxContainer();
        buttons.AddThemeConstantOverride("separation", 8);
        var reset = new Button { Name = "Reset", Text = "Reset to approved", TooltipText = GrassLook.ApprovedPath };
        reset.Pressed += ResetToApproved;
        var copy = new Button { Name = "Copy", Text = "Copy current values" };
        copy.Pressed += CopyCurrentValues;
        buttons.AddChild(reset);
        buttons.AddChild(copy);
        body.AddChild(buttons);

        _copied = new Label { Modulate = new Color(1, 1, 1, 0.6f) };
        _copied.AddThemeFontSizeOverride("font_size", 12);
        body.AddChild(_copied);

        var hint = new Label { Text = "h hides the panels", Modulate = new Color(1, 1, 1, 0.6f) };
        hint.AddThemeFontSizeOverride("font_size", 12);
        body.AddChild(hint);

        Version.Select(Array.IndexOf(Versions, look.Masks));
        ShowVersion(look.Masks);
        BuildTheTilingPanel(look);
        RenderingServer.ViewportSetMeasureRenderTime(GetViewport().GetViewportRid(), true);
    }

    /// <summary>
    /// The anti-tiling options (D631, `seasonal-grass.md §5.3`), top right, each behind its own switch and opened at the
    /// approved look (D632); and the GPU's time for the whole window, so each option's cost can be read off.
    /// </summary>
    private void BuildTheTilingPanel(GrassLook look)
    {
        _tiling = Panel("Tiling");
        _tiling.AnchorLeft = 1f;
        _tiling.AnchorRight = 1f;
        _tiling.GrowHorizontal = GrowDirection.Begin;
        _tiling.OffsetLeft = -16f;
        _tiling.OffsetRight = -16f;
        _tiling.OffsetTop = 16f;
        AddChild(_tiling);

        var body = new VBoxContainer();
        body.AddThemeConstantOverride("separation", 4);
        _tiling.AddChild(body);

        var heading = new Label { Text = "Anti-tiling" };
        heading.AddThemeFontSizeOverride("font_size", 20);
        body.AddChild(heading);

        Macro = AddSwitch(body, "Macro variation", "macro_on", look.MacroOn);
        MacroCycle = AddSlider(body, "Tiles a cycle", 50.0, 200.0, 1.0, look.MacroCycleTiles, Sets("macro_cycle_tiles"), Plain("0"));
        MacroStrength = AddSlider(body, "Strength amount", 0.0, 1.0, 0.01, look.MacroStrengthAmount, Sets("macro_strength_amount"), Plain("0.00"));
        MacroSeason = AddSlider(body, "Season amount", 0.0, 0.5, 0.01, look.MacroSeasonAmount, Sets("macro_season_amount"), Plain("0.00"));

        // ⛔ REJECTED (D632), kept switchable and off: blending one layout into another drew stamps of both at part
        // strength — the ghosts §5.3 measured — and at any region size the mixed layouts read as camouflage.
        Mixing = AddSwitch(body, "Version mixing (rejected)", "mix_on", false);
        Mixing.Toggled += on =>
        {
            if (on)
            {
                LoadTheMixedVersions();
            }
        };
        Mixing.ButtonPressed = look.MixingOn;
        Region = AddSlider(body, "Region, tiles", 16.0, 128.0, 1.0, look.MixRegionTiles, Sets("mix_region_tiles"), Plain("0"));
        Width = AddSlider(body, "Transition, tiles", 0.0, 8.0, 0.1, look.MixWidthTiles, Sets("mix_width_tiles"), Plain("0.0"));

        Hex = AddSwitch(body, "Hex tiling", "hex_on", look.HexOn);
        Sharpness = AddSlider(body, "Blend sharpness", 1.0, 16.0, 0.5, look.HexSharpness, Sets("hex_sharpness"), Plain("0.0"));

        // ⛔ Linear is REJECTED (D632) for the same reason as mixing — it draws stamps at part strength — and kept for
        // comparison. Height, "each stamp whole", is the approved blend.
        Blend = new OptionButton { Name = "Blend" };
        Blend.AddItem("Linear (rejected)");
        Blend.AddItem("Height — each stamp whole");
        Blend.ItemSelected += index => _material.SetShaderParameter("height_blend", index == 1);
        SelectBlend(look.HeightBlend);
        body.AddChild(Row("Blend (mixing and hex)", Blend, null));

        MarkTransitions = AddSwitch(body, "Mark transitions", "mark_transitions", false);

        _gpu = new Label { Name = "Gpu", Modulate = new Color(1, 1, 1, 0.75f) };
        _gpu.AddThemeFontSizeOverride("font_size", 13);
        body.AddChild(_gpu);
    }

    /// <summary>A switch for one boolean uniform.</summary>
    private CheckBox AddSwitch(VBoxContainer body, string name, string uniform, bool on)
    {
        var box = new CheckBox { Name = name, Text = name, ButtonPressed = on };
        box.Toggled += pressed => _material.SetShaderParameter(uniform, pressed);
        _material.SetShaderParameter(uniform, on);
        body.AddChild(box);
        return box;
    }

    private void SelectBlend(bool height)
    {
        Blend.Select(height ? 1 : 0);
        _material.SetShaderParameter("height_blend", height);
    }

    /// <summary>
    /// <i>Reset to approved</i>: every look control back to <see cref="GrassLook.Approved"/>, read from its file again
    /// so an edit to the file shows without a restart. The season, the zoom, the placeholders and the marks stay.
    /// </summary>
    internal void ResetToApproved()
    {
        GrassLook look = (GrassLook)ResourceLoader.Load(GrassLook.ApprovedPath, null, ResourceLoader.CacheMode.Replace);
        Version.Select(Array.IndexOf(Versions, look.Masks));
        ShowVersion(look.Masks);
        Strength.Value = look.Strength;
        Jitter.Value = look.Jitter;
        Grain.Value = look.PaperGrain;
        TilesPerRepeat.Value = look.TilesPerRepeat;
        Macro.ButtonPressed = look.MacroOn;
        MacroCycle.Value = look.MacroCycleTiles;
        MacroStrength.Value = look.MacroStrengthAmount;
        MacroSeason.Value = look.MacroSeasonAmount;
        Mixing.ButtonPressed = look.MixingOn;
        Region.Value = look.MixRegionTiles;
        Width.Value = look.MixWidthTiles;
        Hex.ButtonPressed = look.HexOn;
        Sharpness.Value = look.HexSharpness;
        SelectBlend(look.HeightBlend);
        _copied.Text = "reset to the approved look";
    }

    /// <summary>The panel's look, as a <see cref="GrassLook"/> — what <i>Copy current values</i> says.</summary>
    internal GrassLook CurrentLook() => new()
    {
        Masks = ShownVersion,
        Strength = (float)Strength.Value,
        Jitter = (float)Jitter.Value,
        PaperGrain = (float)Grain.Value,
        TilesPerRepeat = (float)TilesPerRepeat.Value,
        MacroOn = Macro.ButtonPressed,
        MacroCycleTiles = (float)MacroCycle.Value,
        MacroStrengthAmount = (float)MacroStrength.Value,
        MacroSeasonAmount = (float)MacroSeason.Value,
        HexOn = Hex.ButtonPressed,
        HexSharpness = (float)Sharpness.Value,
        HeightBlend = Blend.Selected == 1,
        MixingOn = Mixing.ButtonPressed,
        MixRegionTiles = (float)Region.Value,
        MixWidthTiles = (float)Width.Value,
    };

    /// <summary><i>Copy current values</i>: the list on the clipboard and in the log, ready to paste back.</summary>
    private void CopyCurrentValues()
    {
        string said = CurrentLook().Say();
        DisplayServer.ClipboardSet(said);
        GD.Print(said);
        _copied.Text = "copied — paste it back to change the approved look";
    }

    /// <summary>
    /// All five layouts, once (§6.5's one-version rule does not hold here: ~105 MB of VRAM while mixing is on).
    /// </summary>
    private void LoadTheMixedVersions()
    {
        if (MixedLoaded)
        {
            return;
        }

        for (int i = 0; i < Mixed.Length; i++)
        {
            _material.SetShaderParameter($"grass_v{i}", GD.Load<Texture2D>($"res://textures/grass/grass_{Mixed[i]}_masks.png"));
        }

        MixedLoaded = true;
    }

    /// <summary>The GPU's time for the window, smoothed and said four times a second.</summary>
    public override void _Process(double delta)
    {
        double ms = RenderingServer.ViewportGetMeasuredRenderTimeGpu(GetViewport().GetViewportRid());
        _gpuMs = _gpuMs <= 0.0 ? ms : (0.95 * _gpuMs) + (0.05 * ms);
        _gpuSaidAt += delta;
        if (_gpuSaidAt >= 0.25)
        {
            _gpuSaidAt = 0.0;
            _gpu.Text = $"GPU {_gpuMs.ToString("0.00", CultureInfo.InvariantCulture)} ms a frame, the whole window";
        }
    }

    private static PanelContainer Panel(string name)
    {
        var panel = new PanelContainer { Name = name, CustomMinimumSize = new Vector2(340, 0) };
        panel.AddThemeStyleboxOverride("panel", new StyleBoxFlat
        {
            BgColor = new Color(0.09f, 0.10f, 0.10f, 0.9f),
            ContentMarginLeft = 16,
            ContentMarginRight = 16,
            ContentMarginTop = 12,
            ContentMarginBottom = 12,
            CornerRadiusTopLeft = 6,
            CornerRadiusTopRight = 6,
            CornerRadiusBottomLeft = 6,
            CornerRadiusBottomRight = 6,
        });
        return panel;
    }

    public override void _UnhandledKeyInput(InputEvent key)
    {
        if (key is InputEventKey { Pressed: true, Echo: false, Keycode: Key.H })
        {
            _panel.Visible = !_panel.Visible;
            _tiling.Visible = _panel.Visible;
        }
    }

    /// <summary>
    /// Loads one version's masks and drops the last (§6.5): a lossless 2048² mask with mipmaps is ~21 MB of VRAM.
    /// </summary>
    private void ShowVersion(string version)
    {
        _masks = GD.Load<Texture2D>($"res://textures/grass/grass_{version}_masks.png");
        _material.SetShaderParameter("grass_masks", _masks);
        ShownVersion = version;
    }

    /// <summary>A uniform of the grass material, set from a slider.</summary>
    private Action<double> Sets(string uniform) => v => _material.SetShaderParameter(uniform, (float)v);

    /// <summary>The grain's amount — and at 0 the overlay is not drawn at all, so the lab is the pack exactly.</summary>
    private void SetTheGrain(double amount)
    {
        _grainMaterial.SetShaderParameter("amount", (float)amount);
        _grain.Visible = amount > 0.0;
    }

    private static Func<double, string> Plain(string format) => v => v.ToString(format, CultureInfo.InvariantCulture);

    /// <summary>A labelled slider that applies its value, with the value said beside it.</summary>
    private static HSlider AddSlider(VBoxContainer body, string name, double min, double max, double step, double start, Action<double> apply, Func<double, string> says)
    {
        var value = new Label { CustomMinimumSize = new Vector2(150, 0) };
        var slider = new HSlider
        {
            Name = name,
            MinValue = min,
            MaxValue = max,
            Step = step,
            CustomMinimumSize = new Vector2(160, 0),
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ShrinkCenter,
        };
        slider.ValueChanged += v =>
        {
            apply(v);
            value.Text = says(v);
        };
        slider.Value = start;

        // `Value = start` raises nothing when start is the slider's own default, so it is applied here too.
        apply(start);
        value.Text = says(start);

        body.AddChild(Row(name, slider, value));
        return slider;
    }

    private static VBoxContainer Row(string name, Control control, Label? value)
    {
        var row = new VBoxContainer();
        row.AddThemeConstantOverride("separation", 0);
        var top = new HBoxContainer();
        top.AddChild(new Label { Text = name, SizeFlagsHorizontal = SizeFlags.ExpandFill });
        if (value is not null)
        {
            value.HorizontalAlignment = HorizontalAlignment.Right;
            top.AddChild(value);
        }

        row.AddChild(top);
        row.AddChild(control);
        return row;
    }

    /// <summary><c>1.00 summer</c>, or <c>2.60 fall → winter 60%</c>: the snippet's season value, said.</summary>
    internal static string SeasonSays(double season)
    {
        double s = ((season % 4.0) + 4.0) % 4.0;
        int from = (int)Math.Floor(s) % 4;
        double t = s - Math.Floor(s);
        string number = season.ToString("0.00", CultureInfo.InvariantCulture);
        return t < 0.005
            ? $"{number} {SeasonNames[from]}"
            : $"{number} {SeasonNames[from]} → {SeasonNames[(from + 1) % 4]} {t * 100:0}%";
    }

    /// <summary>
    /// Where the material differs from <paramref name="look"/>, said — or <c>null</c> when it holds the look exactly.
    /// </summary>
    private string? DiffersFrom(GrassLook look)
    {
        var off = new List<string>();
        float Uniform(string name) => _material.GetShaderParameter(name).AsSingle();
        bool Flag(string name) => _material.GetShaderParameter(name).AsBool();
        void Near(string what, float got, float want)
        {
            if (Math.Abs(got - want) > 1e-5f)
            {
                off.Add($"{what} {got} (want {want})");
            }
        }

        void Same(string what, bool got, bool want)
        {
            if (got != want)
            {
                off.Add($"{what} {got} (want {want})");
            }
        }

        var masks = _material.GetShaderParameter("grass_masks").As<Texture2D>();
        if (ShownVersion != look.Masks || masks?.ResourcePath.EndsWith($"grass_{look.Masks}_masks.png", StringComparison.Ordinal) != true)
        {
            off.Add($"masks {ShownVersion} / {masks?.ResourcePath ?? "none"} (want {look.Masks})");
        }

        Near("strength", Uniform("grass_strength"), look.Strength);
        Near("jitter", Uniform("grass_jitter"), look.Jitter);
        Near("grain", _grainMaterial.GetShaderParameter("amount").AsSingle(), look.PaperGrain);
        Same("grain drawn", _grain.Visible, look.PaperGrain > 0f);
        Near("tiles a repeat", Uniform("tiles_per_repeat"), look.TilesPerRepeat);
        Same("macro", Flag("macro_on"), look.MacroOn);
        Near("macro cycle", Uniform("macro_cycle_tiles"), look.MacroCycleTiles);
        Near("macro strength", Uniform("macro_strength_amount"), look.MacroStrengthAmount);
        Near("macro season", Uniform("macro_season_amount"), look.MacroSeasonAmount);
        Same("hex", Flag("hex_on"), look.HexOn);
        Near("hex sharpness", Uniform("hex_sharpness"), look.HexSharpness);
        Same("height blend", Flag("height_blend"), look.HeightBlend);
        Same("mixing", Flag("mix_on"), look.MixingOn);
        Near("mix region", Uniform("mix_region_tiles"), look.MixRegionTiles);
        Near("mix width", Uniform("mix_width_tiles"), look.MixWidthTiles);
        return off.Count == 0 ? null : string.Join(", ", off);
    }

    /// <summary>
    /// The probe's line (D629–D632): the approved look's file sets every value and keeps the rejected options off; the
    /// lab opens at it, its controls reach what they move, <i>Reset to approved</i> brings it back and <i>Copy</i> says
    /// it in Joe's list; and the grass textures are still imported as §3 says — <b>because Godot's own defaults wipe
    /// the masks, and its Import dock can put them back</b> (D627). Each imported level 0 is compared with its PNG byte
    /// for byte: a sampled spot-check scored zero against Fix Alpha Border.
    /// </summary>
    internal static string SelfCheck(Node host)
    {
        var faults = new List<string>();

        // ⭐ The file is the only place the numbers live (the class gives none), so every property must be set in it.
        string file = Godot.FileAccess.GetFileAsString(GrassLook.ApprovedPath);
        foreach (PropertyInfo property in typeof(GrassLook).GetProperties())
        {
            if (property.GetCustomAttribute<ExportAttribute>() is not null && !file.Contains($"\n{property.Name} = ", StringComparison.Ordinal))
            {
                faults.Add($"{GrassLook.ApprovedPath} does not set {property.Name}");
            }
        }

        GrassLook approved = GrassLook.Approved;
        if (approved.MixingOn || !approved.HeightBlend || Array.IndexOf(Versions, approved.Masks) < 0)
        {
            faults.Add($"the approved look has mixing {approved.MixingOn} and height blending {approved.HeightBlend} on {approved.Masks} "
                + "(the rejected options must stay off, and the masks one of the five)");
        }

        var lab = new GrassLab();
        host.AddChild(lab);
        try
        {
            if (lab.DiffersFrom(approved) is string opened)
            {
                faults.Add($"opens off the approved look: {opened}");
            }

            float Uniform(string name) => lab._material.GetShaderParameter(name).AsSingle();
            bool Flag(string name) => lab._material.GetShaderParameter(name).AsBool();
            if (lab.Version.ItemCount != 5 || lab.Version.GetItemText(0) != "v06" || lab.Version.GetItemText(1) != "v04"
                || Math.Abs(Uniform("grass_season") - 1f) > 1e-6f || Math.Abs(Uniform("px_per_tile") - 24f) > 1e-6f
                || !lab.Placeholders.ButtonPressed || !lab.Village.Visible || Flag("mark_transitions") || lab.MixedLoaded)
            {
                faults.Add($"opens on {lab.Version.ItemCount} versions ({lab.Version.GetItemText(0)}, {lab.Version.GetItemText(1)} …), "
                    + $"season {Uniform("grass_season")}, {Uniform("px_per_tile")} px a tile, placeholders {lab.Village.Visible}, "
                    + $"marks {Flag("mark_transitions")}, five layouts loaded {lab.MixedLoaded} (want v06, v04 of 5; 1; 24; on; off; not loaded)");
            }

            if (lab.Jitter.MinValue != 0.0 || lab.Jitter.MaxValue != 0.25)
            {
                faults.Add($"the jitter slider runs {lab.Jitter.MinValue}–{lab.Jitter.MaxValue} (want 0–0.25)");
            }

            // ⚠️ Against the pack's own hex, not only the constant: the palettes were derived over #e6d6b0 (D628).
            if (Parchment.ToHtml(false) != "e6d6b0"
                || lab._material.GetShaderParameter("ground_srgb").AsVector3() != new Vector3(Parchment.R, Parchment.G, Parchment.B))
            {
                faults.Add($"the ground is not the pack's parchment #e6d6b0 (#{Parchment.ToHtml(false)})");
            }

            // ⭐ Copy says the approved look in Joe's own list, line for line.
            string[] said = lab.CurrentLook().Say().Split('\n');
            string[] heads = { "APPROVED SETTINGS", "- Masks: ", "- Strength: ", "- Jitter: ", "- Paper grain: ", "- Tiles per repeat: ",
                "- Macro variation: ", "- Hex tiling: ", "- Version mixing: ", "- Linear blend: " };
            if (said.Length != heads.Length || heads.Where((head, i) => !said[i].StartsWith(head, StringComparison.Ordinal)).Any()
                || lab.CurrentLook().Say() != approved.Say())
            {
                faults.Add($"Copy says \"{string.Join(" | ", said)}\" (want Joe's ten lines, the approved look's)");
            }

            lab.Jitter.Value = 0.2;
            lab.Season.Value = 2.6;
            lab.Version.Select(1);
            lab.Version.EmitSignal(OptionButton.SignalName.ItemSelected, 1L);
            lab.Strength.Value = 0.5;
            lab.Grain.Value = 0.0;
            lab.PixelsPerTile.Value = 40.0;
            lab.Placeholders.ButtonPressed = false;
            lab.Macro.ButtonPressed = false;
            lab.Hex.ButtonPressed = false;
            lab.Mixing.ButtonPressed = true;
            lab.MarkTransitions.ButtonPressed = true;
            lab.Blend.Select(0);
            lab.Blend.EmitSignal(OptionButton.SignalName.ItemSelected, 0L);
            lab.MacroCycle.Value = 150.0;
            lab.Width.Value = 3.0;
            lab.Sharpness.Value = 2.0;
            var masks = lab._material.GetShaderParameter("grass_masks").As<Texture2D>();
            var last = lab._material.GetShaderParameter("grass_v4").As<Texture2D>();
            if (Math.Abs(Uniform("grass_jitter") - 0.2f) > 1e-6f || Math.Abs(Uniform("grass_season") - 2.6f) > 1e-6f
                || lab.ShownVersion != "v04" || masks?.ResourcePath.EndsWith("grass_v04_masks.png", StringComparison.Ordinal) != true
                || Math.Abs(Uniform("grass_strength") - 0.5f) > 1e-6f || lab._grain.Visible || lab.Village.Visible
                || Math.Abs(Uniform("px_per_tile") - 40f) > 1e-6f || Math.Abs(lab.Village.PixelsPerTile - 40f) > 1e-6f
                || Flag("macro_on") || Flag("hex_on") || !Flag("mix_on") || !Flag("mark_transitions") || Flag("height_blend")
                || !lab.MixedLoaded || last?.ResourcePath.EndsWith("grass_v06_masks.png", StringComparison.Ordinal) != true
                || Math.Abs(Uniform("macro_cycle_tiles") - 150f) > 1e-6f || Math.Abs(Uniform("mix_width_tiles") - 3f) > 1e-6f
                || Math.Abs(Uniform("hex_sharpness") - 2f) > 1e-6f)
            {
                faults.Add($"moving every control reached jitter {Uniform("grass_jitter")}, season {Uniform("grass_season")}, "
                    + $"{masks?.ResourcePath ?? "no masks"}, strength {Uniform("grass_strength")}, grain drawn {lab._grain.Visible} at 0, "
                    + $"placeholders drawn {lab.Village.Visible} when off, the grass at {Uniform("px_per_tile")} px and the village at "
                    + $"{lab.Village.PixelsPerTile}, macro {Flag("macro_on")}, hex {Flag("hex_on")}, mixing {Flag("mix_on")} (layouts "
                    + $"{lab.MixedLoaded}, the fifth {last?.ResourcePath ?? "none"}), marks {Flag("mark_transitions")}, height "
                    + $"{Flag("height_blend")}, cycle {Uniform("macro_cycle_tiles")}, width {Uniform("mix_width_tiles")}, "
                    + $"sharpness {Uniform("hex_sharpness")}");
            }

            // ⭐ Reset brings back every look value, and leaves the view's own controls where they were.
            lab.ResetToApproved();
            if (lab.DiffersFrom(approved) is string reset)
            {
                faults.Add($"Reset to approved left {reset}");
            }

            if (Math.Abs(Uniform("grass_season") - 2.6f) > 1e-6f || Math.Abs(Uniform("px_per_tile") - 40f) > 1e-6f || lab.Village.Visible)
            {
                faults.Add("Reset to approved moved the season, the zoom or the placeholders");
            }
        }
        finally
        {
            host.RemoveChild(lab);
            lab.Free();
        }

        foreach (string version in Versions)
        {
            ImportHolds($"res://textures/grass/grass_{version}_masks.png", mipmapped: true, Image.Format.Rgba8, faults);
        }

        ImportHolds("res://textures/grass/grass_seasons.png", mipmapped: false, Image.Format.Rgb8, faults);

        return faults.Count == 0
            ? $"[widths] grass lab: ✅ {GrassLook.ApprovedPath} sets every value, rejected options off; the lab opens at it "
                + "over parchment with the placeholders standing, every control reaches what it moves, Reset brings the look back "
                + "and Copy says it in Joe's list; the five masks import mipmapped and the atlas unmipped, each byte for byte its PNG"
            : $"[widths] grass lab: ❌ {string.Join("; ", faults)}";
    }

    private static void ImportHolds(string path, bool mipmapped, Image.Format format, List<string> faults)
    {
        string name = path[(path.LastIndexOf('/') + 1)..];
        Image imported = GD.Load<Texture2D>(path).GetImage();
        if (imported.HasMipmaps() != mipmapped)
        {
            faults.Add($"{name} {(mipmapped ? "has no" : "has")} mipmaps");
        }

        if (imported.IsCompressed())
        {
            faults.Add($"{name} is compressed ({imported.GetFormat()})");
            return;
        }

        var level0 = (Image)imported.Duplicate();
        level0.ClearMipmaps();
        level0.Convert(format);
        Image source = Image.LoadFromFile(ProjectSettings.GlobalizePath(path));
        source.Convert(format);
        if (!level0.GetData().AsSpan().SequenceEqual(source.GetData()))
        {
            faults.Add($"{name} differs from its PNG — Fix Alpha Border or Premult Alpha is on, or it is not lossless (§3)");
        }
    }
}

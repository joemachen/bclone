using System.Globalization;
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
/// version on show is loaded (§6.5). Launched with <c>.\run.bat grass</c>; <b>H</b> hides the panel.
/// </para>
/// <para>
/// The numbers here are the lab's starting points, not the game's: the repeat and the zoom found by eye go into data
/// when the grass goes into the valley (§6.3).
/// </para>
/// </remarks>
public sealed partial class GrassLab : Control
{
    /// <summary>v06 first, v04 the first alternative (Joe, §5); v01 is retired and not in the project.</summary>
    internal static readonly string[] Versions = { "v06", "v04", "v02", "v03", "v05" };

    internal const double DefaultSeason = 1.0;
    internal const double DefaultJitter = 0.15;
    internal const double DefaultTilesPerRepeat = 32.0;

    /// <summary>
    /// The zoom the lab opens at, in screen pixels a tile — between the game's fit-to-width (~13 at 1280 wide) and
    /// <c>MaxPixelsPerTile = 48</c>, so a repeat and a half is on screen and the detail still reads.
    /// </summary>
    internal const double DefaultPixelsPerTile = 24.0;

    internal static readonly Color Parchment = new("#e6d6b0");

    private static readonly string[] SeasonNames = { "spring", "summer", "fall", "winter" };

    private readonly ShaderMaterial _material = new() { Shader = GD.Load<Shader>("res://shaders/grass_lab.gdshader") };
    private ColorRect _ground = null!;
    private PanelContainer _panel = null!;
    private Texture2D? _masks;

    internal string ShownVersion { get; private set; } = Versions[0];

    internal OptionButton Version { get; private set; } = null!;

    internal HSlider Season { get; private set; } = null!;

    internal HSlider Jitter { get; private set; } = null!;

    internal HSlider TilesPerRepeat { get; private set; } = null!;

    internal HSlider PixelsPerTile { get; private set; } = null!;

    public override void _Ready()
    {
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);

        _material.SetShaderParameter("grass_season_atlas", GD.Load<Texture2D>("res://textures/grass/grass_seasons.png"));
        _material.SetShaderParameter("ground_srgb", new Vector3(Parchment.R, Parchment.G, Parchment.B));

        _ground = new ColorRect { Name = "Ground", Material = _material, MouseFilter = MouseFilterEnum.Ignore };
        _ground.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        AddChild(_ground);
        _ground.Resized += () => _material.SetShaderParameter("size_px", _ground.Size);

        _panel = new PanelContainer { Name = "Panel", Position = new Vector2(16, 16), CustomMinimumSize = new Vector2(340, 0) };
        _panel.AddThemeStyleboxOverride("panel", new StyleBoxFlat
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

        Season = AddSlider(body, "Season", 0.0, 4.0, 0.01, DefaultSeason, "grass_season", SeasonSays);
        Jitter = AddSlider(body, "Jitter", 0.0, 0.25, 0.005, DefaultJitter, "grass_jitter", v => v.ToString("0.000", CultureInfo.InvariantCulture));
        TilesPerRepeat = AddSlider(body, "Tiles per repeat", 8.0, 128.0, 1.0, DefaultTilesPerRepeat, "tiles_per_repeat", v => v.ToString("0", CultureInfo.InvariantCulture));
        PixelsPerTile = AddSlider(body, "Pixels per tile", 13.0, 48.0, 1.0, DefaultPixelsPerTile, "px_per_tile", v => v.ToString("0", CultureInfo.InvariantCulture));

        var hint = new Label { Text = "h hides this panel", Modulate = new Color(1, 1, 1, 0.6f) };
        hint.AddThemeFontSizeOverride("font_size", 12);
        body.AddChild(hint);

        ShowVersion(Versions[0]);
    }

    public override void _UnhandledKeyInput(InputEvent key)
    {
        if (key is InputEventKey { Pressed: true, Echo: false, Keycode: Key.H })
        {
            _panel.Visible = !_panel.Visible;
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

    /// <summary>A labelled slider that sets one uniform, with its value said beside it.</summary>
    private HSlider AddSlider(VBoxContainer body, string name, double min, double max, double step, double start, string uniform, Func<double, string> says)
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
            _material.SetShaderParameter(uniform, (float)v);
            value.Text = says(v);
        };
        slider.Value = start;

        // `Value = start` raises nothing when start is the slider's own default, so the uniform is set here too.
        _material.SetShaderParameter(uniform, (float)start);
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
    /// The probe's line (D629): the lab opens as §5 says and its sliders reach the shader; and the grass textures are
    /// still imported as §3 says — <b>because Godot's own defaults wipe the masks, and its Import dock can put them
    /// back</b> (D627). Each imported level 0 is compared with its PNG byte for byte: a sampled spot-check scored
    /// zero against Fix Alpha Border.
    /// </summary>
    internal static string SelfCheck(Node host)
    {
        var faults = new List<string>();
        var lab = new GrassLab();
        host.AddChild(lab);
        try
        {
            if (lab.ShownVersion != "v06" || lab.Version.ItemCount != 5 || lab.Version.GetItemText(1) != "v04")
            {
                faults.Add($"opens on {lab.ShownVersion} of {lab.Version.ItemCount}, the second {lab.Version.GetItemText(1)} (want v06 of 5, then v04)");
            }

            float Uniform(string name) => lab._material.GetShaderParameter(name).AsSingle();
            if (Math.Abs(Uniform("grass_jitter") - 0.15f) > 1e-6f || Math.Abs(Uniform("grass_season") - 1f) > 1e-6f
                || Math.Abs(Uniform("tiles_per_repeat") - 32f) > 1e-6f || Math.Abs(Uniform("px_per_tile") - 24f) > 1e-6f)
            {
                faults.Add($"opens at jitter {Uniform("grass_jitter")}, season {Uniform("grass_season")}, {Uniform("tiles_per_repeat")} tiles a repeat, {Uniform("px_per_tile")} px a tile (want 0.15, 1, 32, 24)");
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

            lab.Jitter.Value = 0.2;
            lab.Season.Value = 2.6;
            lab.Version.Select(1);
            lab.Version.EmitSignal(OptionButton.SignalName.ItemSelected, 1L);
            var masks = lab._material.GetShaderParameter("grass_masks").As<Texture2D>();
            if (Math.Abs(Uniform("grass_jitter") - 0.2f) > 1e-6f || Math.Abs(Uniform("grass_season") - 2.6f) > 1e-6f
                || lab.ShownVersion != "v04" || masks?.ResourcePath.EndsWith("grass_v04_masks.png", StringComparison.Ordinal) != true)
            {
                faults.Add($"moving the sliders reached jitter {Uniform("grass_jitter")}, season {Uniform("grass_season")}, {masks?.ResourcePath ?? "no masks"}");
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
            ? "[widths] grass lab: ✅ opens on v06 (v04 next) at jitter 0.15, season 1, 32 tiles a repeat, over parchment, and its sliders reach the shader; the five masks import mipmapped and the atlas unmipped, each byte for byte its PNG"
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

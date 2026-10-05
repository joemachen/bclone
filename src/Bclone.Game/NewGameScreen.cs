using System.Globalization;
using Bclone.Sim.Config;
using Bclone.Sim.Core;
using Godot;

namespace Bclone.Game;

/// <summary>
/// ⭐ The new-game screen — choose a valley before you found a village (D477, D479,
/// `specs/new-game-screen.md §3`).
/// </summary>
/// <remarks>
/// <para>
/// <b>Every rule is the sim's</b> (<see cref="NewGame"/>, <see cref="SeedText"/>): what a seed names, what
/// a row sets, what a share code says, what is refused and in which words. This class lays them out
/// and nothing else — so the screen cannot disagree with the game about which valley a code names.
/// </para>
/// <para>
/// ⭐ <b>The preview is the real valley</b>: <see cref="SimWorld.Create"/> from exactly the settings on
/// screen (about a millisecond), painted by the same <see cref="ValleyTexture"/> the game uses — one
/// look, or the preview would lie about the valley. The stats are read off that world
/// (<see cref="NewGame.Summarise"/>), never restated from the sliders.
/// </para>
/// <para>
/// ⚠️ <b>The dice are the view's randomness</b> (<see cref="System.Random"/>), choosing an <em>input</em>:
/// the sim only ever sees the text, and <c>BannedSymbols.txt</c> binds the sim, not this.
/// </para>
/// </remarks>
public sealed partial class NewGameScreen : Control
{
    /// <summary>
    /// The config the village is founded on, the share code that names its valley, and the rows it was
    /// chosen with — which the settings remember for the next screen (D504).
    /// </summary>
    public event Action<SimConfig, string, IReadOnlyDictionary<string, string>>? Founded;

    private const float ColumnWidth = 400f;

    /// <summary>How long after the last change the preview waits before it bakes again, in seconds.</summary>
    private const double BakeAfter = 0.12;

    private readonly SimConfig _config;
    private readonly System.Random _dice = new();
    /// <summary>
    /// The preview's bake, at <see cref="PreviewPixelsPerTile"/> — the whole valley fits about 6½
    /// pixels a tile beside the column at 1280, so 8 is sharp there at a quarter of the game's pixels.
    /// </summary>
    private readonly ValleyTexture _bake = new(PreviewPixelsPerTile);

    private const int PreviewPixelsPerTile = 8;
    private readonly Dictionary<string, Control> _rowControls = new(StringComparer.Ordinal);
    private readonly Dictionary<string, Label> _rowReadings = new(StringComparer.Ordinal);

    private NewGameSettings _settings;
    private SimWorld? _previewWorld;

    private LineEdit _seed = null!;
    private LineEdit _name = null!;
    private LineEdit _code = null!;
    private Label _refusal = null!;
    private Label _stats = null!;
    private TextureRect _preview = null!;
    private Control _mark = null!;
    private Button _found = null!;

    private bool _changed = true;
    private double _sinceChange;
    private bool _settingControls;

    /// <summary>For Godot, which may construct a script's node itself; the game uses the other.</summary>
    public NewGameScreen()
        : this(new SimConfig(), new Dictionary<string, string>(), new List<PlayerSettingsProblem>())
    {
    }

    /// <summary>
    /// ⭐ Opens on <paramref name="remembered"/> — the rows of the valley last founded (D504,
    /// `settings-persistence.md §4`) — with a fresh seed; a remembered value the rows now refuse opens
    /// at the config's default and says so in <paramref name="problems"/>.
    /// </summary>
    public NewGameScreen(
        SimConfig config, IReadOnlyDictionary<string, string> remembered, ICollection<PlayerSettingsProblem> problems)
    {
        _config = config;
        _settings = NewGame.Remembered(config, NewGame.Defaults(config, RollASeed()), remembered, problems);
    }

    /// <summary>For the probe: the slowest preview bake so far, in milliseconds.</summary>
    internal double SlowestBakeMs { get; private set; }

    /// <summary>For the probe: the last preview bake, in milliseconds — past the first's warm-up.</summary>
    internal double LastBakeMs => _bake.LastBakeMs;

    /// <summary>For the probe: bake the preview again now, as a change would.</summary>
    internal void BakeAgain() => BakeThePreview();

    /// <summary>For the probe: the preview has a valley on it.</summary>
    internal bool HasAPreview => _bake.Texture is not null && _preview.Texture is not null;

    /// <summary>For the probe: the right-hand column, whose width is the one that must fit.</summary>
    internal Control Column { get; private set; } = null!;

    /// <summary>For the probe: the widest valley the stats can describe, baked now.</summary>
    internal void PoseTheLongestStats()
    {
        var values = new Dictionary<string, string>(_settings.Values, StringComparer.Ordinal);
        foreach (NewGameRow row in _config.NewGameOptions)
        {
            values[row.Id] = row.Kind == NewGame.LevelsKind
                ? row.Levels.OrderByDescending(l => l.Label.Length).First().Value
                : row.Max.ToString(CultureInfo.InvariantCulture);
        }

        _settings = _settings with { Values = values };
        ShowTheSettings();
        BakeThePreview();
    }

    /// <summary>For the probe: show the longest sentence the screen can refuse with.</summary>
    internal void PoseTheLongestRefusal()
    {
        string longest = string.Empty;
        foreach (NewGameRow row in _config.NewGameOptions)
        {
            NewGame.IsAllowed(row, "999999", out string? said);
            if (said is not null && said.Length > longest.Length)
            {
                longest = said;
            }
        }

        ShowRefusal(longest);
    }

    public override void _Ready()
    {
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);

        var backdrop = new Panel();
        backdrop.AddThemeStyleboxOverride("panel", new StyleBoxFlat { BgColor = new Color(0.07f, 0.08f, 0.08f) });
        backdrop.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        AddChild(backdrop);

        var margin = new MarginContainer();
        margin.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        foreach (string side in new[] { "margin_left", "margin_right", "margin_top", "margin_bottom" })
        {
            margin.AddThemeConstantOverride(side, 24);
        }

        AddChild(margin);

        var split = new HBoxContainer();
        split.AddThemeConstantOverride("separation", 24);
        margin.AddChild(split);

        split.AddChild(BuildThePreview());
        split.AddChild(BuildTheColumn());

        ShowTheSettings();
    }

    private Control BuildThePreview()
    {
        var left = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        left.AddThemeConstantOverride("separation", 10);

        _preview = new TextureRect
        {
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill,
            TextureFilter = TextureFilterEnum.Linear,
        };
        left.AddChild(_preview);

        // ⛔ Wrapped, with a small minimum: a bare label is a minimum WIDTH, and the longest stats line
        // ("River 6 wide, north-west to south-east") pushed the column off the screen (D479, seen in a
        // snapshot the probe had not posed — it poses that line now).
        _stats = new Label
        {
            HorizontalAlignment = HorizontalAlignment.Center,
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
            CustomMinimumSize = new Vector2(120, 0),
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
        };
        _stats.AddThemeFontSizeOverride("font_size", 15);
        left.AddChild(_stats);

        // The founders' camp, ringed on the preview: where the village will begin.
        _mark = new Control { MouseFilter = MouseFilterEnum.Ignore };
        _mark.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        _mark.Draw += DrawTheFounding;
        _preview.AddChild(_mark);
        return left;
    }

    private Control BuildTheColumn()
    {
        var column = new VBoxContainer { CustomMinimumSize = new Vector2(ColumnWidth, 0) };
        column.AddThemeConstantOverride("separation", 6);
        Column = column;

        var title = new Label { Text = "A new valley" };
        title.AddThemeFontSizeOverride("font_size", 24);
        column.AddChild(title);

        column.AddChild(Caption("Seed — any words, or paste a share code"));
        var seedRow = new HBoxContainer();
        _seed = new LineEdit { Text = _settings.SeedText, SizeFlagsHorizontal = SizeFlags.ExpandFill };
        _seed.TextChanged += OnSeedTyped;
        seedRow.AddChild(_seed);
        var dice = new Button { Text = "Roll", TooltipText = "Roll a new seed of words" };
        dice.Pressed += () => _seed.Text = RollASeed();
        dice.Pressed += () => OnSeedTyped(_seed.Text);
        seedRow.AddChild(dice);
        column.AddChild(seedRow);

        _refusal = new Label
        {
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
            Modulate = new Color(1f, 0.55f, 0.45f),
            Visible = false,
        };
        _refusal.AddThemeFontSizeOverride("font_size", 13);
        column.AddChild(_refusal);

        column.AddChild(Caption("Village name"));
        _name = new LineEdit { MaxLength = NewGame.LongestVillageName };
        column.AddChild(_name);

        column.AddChild(Caption("Valley"));
        var valley = new OptionButton { TooltipText = "One kind of valley for now — more will come." };
        valley.AddItem("River valley");
        column.AddChild(valley);

        foreach (NewGameRow row in _config.NewGameOptions)
        {
            column.AddChild(BuildARow(row));
        }

        column.AddChild(new Control { CustomMinimumSize = new Vector2(0, 8) });
        column.AddChild(Caption("Share code — the seed and every setting"));
        var codeRow = new HBoxContainer();
        _code = new LineEdit { Editable = false, SizeFlagsHorizontal = SizeFlags.ExpandFill };
        codeRow.AddChild(_code);
        var copy = new Button { Text = "Copy" };
        copy.Pressed += () => DisplayServer.ClipboardSet(_code.Text);
        codeRow.AddChild(copy);
        column.AddChild(codeRow);

        column.AddChild(new Control { SizeFlagsVertical = SizeFlags.ExpandFill });

        var buttons = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.End };
        buttons.AddThemeConstantOverride("separation", 10);
        var defaults = new Button { Text = "Default settings", TooltipText = "Every setting back to the game's own" };
        defaults.Pressed += OnDefaults;
        buttons.AddChild(defaults);
        _found = new Button { Text = "Found a new village" };
        _found.Pressed += OnFound;
        buttons.AddChild(_found);
        column.AddChild(buttons);

        return column;
    }

    private Control BuildARow(NewGameRow row)
    {
        var box = new VBoxContainer();
        box.AddThemeConstantOverride("separation", 2);
        var heading = new HBoxContainer();
        Label caption = Caption(row.Label);
        caption.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        heading.AddChild(caption);
        var reading = new Label { HorizontalAlignment = HorizontalAlignment.Right };
        heading.AddChild(reading);
        _rowReadings[row.Id] = reading;
        box.AddChild(heading);

        if (row.Kind == NewGame.LevelsKind)
        {
            var choice = new OptionButton();
            foreach (NewGameLevel level in row.Levels)
            {
                choice.AddItem(level.Label);
            }

            choice.ItemSelected += index =>
            {
                if (!_settingControls)
                {
                    SetRow(row.Id, row.Levels[(int)index].Value);
                }
            };
            box.AddChild(choice);
            _rowControls[row.Id] = choice;
        }
        else
        {
            var slider = new HSlider
            {
                MinValue = row.Min,
                MaxValue = row.Max,
                Step = row.Step,
                SizeFlagsHorizontal = SizeFlags.ExpandFill,
            };
            slider.ValueChanged += value =>
            {
                if (!_settingControls)
                {
                    SetRow(row.Id, ((int)value).ToString(CultureInfo.InvariantCulture));
                }
            };
            if (row.HasEndLabels)
            {
                // The ends named, no number (D481): the share code still carries the number.
                var span = new HBoxContainer();
                span.AddThemeConstantOverride("separation", 8);
                span.AddChild(Caption(row.MinLabel));
                span.AddChild(slider);
                span.AddChild(Caption(row.MaxLabel));
                box.AddChild(span);
            }
            else
            {
                box.AddChild(slider);
            }

            _rowControls[row.Id] = slider;
        }

        return box;
    }

    private static Label Caption(string text)
    {
        var label = new Label { Text = text };
        label.AddThemeFontSizeOverride("font_size", 13);
        label.Modulate = new Color(1, 1, 1, 0.65f);
        return label;
    }

    private string RollASeed()
    {
        IReadOnlyList<string> words = _config.SeedWords;
        return $"{words[_dice.Next(words.Count)]}-{words[_dice.Next(words.Count)]}-{_dice.Next(10, 100)}";
    }

    private void OnSeedTyped(string text)
    {
        if (!NewGame.TryRead(_config, text, _settings, out NewGameSettings read, out string? refusal))
        {
            ShowRefusal(refusal!);
            return;
        }

        ShowRefusal(null);
        _settings = read with { VillageName = _settings.VillageName };

        // A pasted code leaves the seed in the box and the settings on the rows.
        if (text.Contains(SeedText.SettingsMark, StringComparison.Ordinal))
        {
            _seed.Text = read.SeedText;
            _seed.CaretColumn = _seed.Text.Length;
        }

        ShowTheSettings();
    }

    private void SetRow(string id, string value)
    {
        var values = new Dictionary<string, string>(_settings.Values, StringComparer.Ordinal) { [id] = value };
        _settings = _settings with { Values = values };
        ShowTheSettings();
    }

    private void OnDefaults()
    {
        _settings = NewGame.Defaults(_config, _settings.SeedText);
        ShowTheSettings();
    }

    private void OnFound()
    {
        try
        {
            NewGameSettings settings = _settings with { VillageName = _name.Text };
            SimConfig founded = NewGame.Apply(_config, settings);
            Founded?.Invoke(founded, NewGame.ShareCode(_config, settings), settings.Values);
        }
        catch (SimConfigException refused)
        {
            // Never swallowed (METHODOLOGY §4): the sentence goes on screen, and the screen stays.
            ShowRefusal(refused.Message);
        }
    }

    private void ShowRefusal(string? sentence)
    {
        _refusal.Text = sentence ?? string.Empty;
        _refusal.Visible = sentence is not null;
        _found.Disabled = sentence is not null;
    }

    /// <summary>Put the settings on every row, the code in its box, and ask for a new preview.</summary>
    private void ShowTheSettings()
    {
        _settingControls = true;
        foreach (NewGameRow row in _config.NewGameOptions)
        {
            string value = _settings.Values[row.Id];
            switch (_rowControls[row.Id])
            {
                case OptionButton choice:
                    int index = row.Levels.ToList().FindIndex(l => l.Value == value);
                    choice.Selected = index;
                    _rowReadings[row.Id].Text = index < 0 ? "as configured" : string.Empty;
                    break;
                case HSlider slider:
                    slider.Value = int.Parse(value, CultureInfo.InvariantCulture);
                    _rowReadings[row.Id].Text = row.HasEndLabels ? string.Empty : value + row.Unit;
                    break;
            }
        }

        _settingControls = false;
        _code.Text = NewGame.ShareCode(_config, _settings);
        _changed = true;
        _sinceChange = 0;
    }

    public override void _Process(double delta)
    {
        if (!_changed)
        {
            return;
        }

        // ⭐ Wait for the hand to stop: a slider dragged end to end would otherwise bake every
        // value it passes through. The first preview bakes at once.
        _sinceChange += delta;
        if (_previewWorld is not null && _sinceChange < BakeAfter)
        {
            return;
        }

        _changed = false;
        BakeThePreview();
    }

    private void BakeThePreview()
    {
        SimConfig config;
        try
        {
            config = NewGame.Apply(_config, _settings);
        }
        catch (SimConfigException refused)
        {
            ShowRefusal(refused.Message);
            return;
        }

        _previewWorld = SimWorld.Create(config);
        _bake.BakeAfresh(_previewWorld);
        SlowestBakeMs = Math.Max(SlowestBakeMs, _bake.LastBakeMs);
        _preview.Texture = _bake.Texture;

        ValleySummary s = NewGame.Summarise(config, _previewWorld.Map);
        _stats.Text = $"Wooded {s.WoodedPercent}% of the land   ·   Stone {s.StoneSeams} seams, {s.StoneTiles} tiles   ·   "
            + $"Iron {s.IronSeams} seams, {s.IronTiles} tiles   ·   River {RiverWords(config, s.Course)}";
        _name.PlaceholderText = _previewWorld.Name;
        _mark.QueueRedraw();
    }

    /// <summary>Ring the founding site where the texture is drawn — centred and kept to its aspect.</summary>
    private void DrawTheFounding()
    {
        if (_previewWorld is null || _bake.Texture is null)
        {
            return;
        }

        Vector2 texture = _bake.Texture.GetSize();
        Vector2 room = _preview.Size;
        float scale = Mathf.Min(room.X / texture.X, room.Y / texture.Y);
        Vector2 origin = (room - (texture * scale)) / 2f;
        float perTile = texture.X * scale / _previewWorld.Config.MapWidth;

        var site = _previewWorld.Map.FoundingSite;
        Vector2 at = origin + new Vector2(
            (site.X - _previewWorld.Config.MapMinX + 0.5f) * perTile,
            (site.Y - _previewWorld.Config.MapMinY + 0.5f) * perTile);
        float radius = Mathf.Max(6f, _previewWorld.Config.StartingResidentialRadius * perTile);

        _mark.DrawArc(at, radius, 0f, Mathf.Tau, 48, new Color(0.98f, 0.78f, 0.46f), 2f, true);
        _mark.DrawCircle(at, 2.5f, new Color(0.98f, 0.78f, 0.46f));
    }

    private static string RiverWords(SimConfig config, string course)
    {
        if (config.RiverWidthTiles == 0)
        {
            return "none";
        }

        string runs = course switch
        {
            "ns" => "north to south",
            "nwse" => "north-west to south-east",
            "swne" => "south-west to north-east",
            _ => "west to east",
        };
        return $"{config.RiverWidthTiles} wide, {runs}";
    }
}

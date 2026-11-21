using System.Globalization;
using Kith.Sim.Config;
using Kith.Sim.Core;
using Godot;

namespace Kith.Game;

/// <summary>
/// ⭐ The new-game screen — choose a valley before you found a village (D477, D479,
/// `specs/new-game-screen.md §3`), restyled (A147, §12): the map fills the screen, one parchment panel docks right.
/// </summary>
/// <remarks>
/// <para>
/// <b>Every rule is the sim's</b> (<see cref="NewGame"/>, <see cref="SeedText"/>): what a seed names, what
/// a row sets, what a share code says, what is refused and in which words, which heading a row sits under and what
/// the strip says. This class lays them out and nothing else — so the screen cannot disagree with the game about
/// which valley a code names.
/// </para>
/// <para>
/// ⭐ <b>The preview is the real valley</b>: <see cref="SimWorld.Create"/> from exactly the settings on
/// screen (about a millisecond), painted by the same <see cref="ValleyTexture"/> the game uses — one
/// look, or the preview would lie about the valley. The strip is read off that world
/// (<see cref="NewGame.Summarise"/>, <see cref="NewGame.Describe"/>), never restated from the settings.
/// </para>
/// <para>
/// ⭐ <b>The camera</b> (§12.2, Joe's call 3): the valley's height fills the window, and the founding sits in the
/// middle of the room left of the panel — as near it as the valley allows, since the camera stops at the valley's
/// edge and no bare paper shows there.
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

    /// <summary><i>Back</i>: the title screen (D516). Nothing is founded.</summary>
    public event Action? BackAsked;

    /// <summary>The panel's share of the window (§12.2: Joe's 25–30 %); never narrower than its widest row.</summary>
    private const float PanelShare = 0.28f;

    /// <summary>The most the panel may take, Joe's upper end — the probe's line (§12.7).</summary>
    private const float PanelMostShare = 0.30f;

    /// <summary>The strip's distance from the window's bottom-left corner.</summary>
    private const float StripInset = 16f;

    /// <summary>How long after the last change the preview waits before it bakes again, in seconds.</summary>
    private const double BakeAfter = 0.12;

    /// <summary>The window's height the probe poses: the smallest the game plays at, 1280 × 720 (§12.7).</summary>
    private const float SmallestHeight = 720f;

    private readonly SimConfig _config;
    private readonly System.Random _dice = new();
    /// <summary>
    /// The preview's bake, at <see cref="PreviewPixelsPerTile"/> — at 1280 × 720 the valley's height fills the window at
    /// 9 screen pixels a tile, so 8 is near 1 : 1 there at a quarter of the game's pixels.
    /// </summary>
    private readonly ValleyTexture _bake = new(PreviewPixelsPerTile);

    private const int PreviewPixelsPerTile = 8;
    private readonly Dictionary<string, Control> _rowControls = new(StringComparer.Ordinal);
    private readonly Dictionary<string, Label> _rowReadings = new(StringComparer.Ordinal);
    private readonly List<Label> _rowLabels = new();

    private NewGameSettings _settings;
    private SimWorld? _previewWorld;

    private LineEdit _seed = null!;
    private LineEdit _name = null!;
    private Label _code = null!;
    private Label _refusal = null!;
    private Label _stats = null!;
    private PanelContainer _strip = null!;
    private PanelContainer _panel = null!;
    private ScrollContainer _scroll = null!;
    private TextureRect _preview = null!;
    private Control _map = null!;
    private Control _mark = null!;
    private Button _found = null!;

    /// <summary>Where the valley's top-left corner is drawn, and how many screen pixels a tile is — the camera.</summary>
    private Vector2 _origin;

    private float _perTile;

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

    /// <summary>For the probe: the panel, whose width is the one that must fit.</summary>
    internal Control Column { get; private set; } = null!;

    /// <summary>For the probe: the widest valley the strip can describe, baked now.</summary>
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

    /// <summary>For the probe: the screen at the smallest window the game plays at (§12.7) — 720 high.</summary>
    internal void PoseTheSmallestWindow()
    {
        Vector2 window = GetViewportRect().Size;
        SetAnchorsPreset(LayoutPreset.TopLeft);
        Position = Vector2.Zero;
        Size = new Vector2(window.X, SmallestHeight);
    }

    /// <summary>
    /// For the probe (§12.7): what is wrong with the layout, in sentences — empty when nothing is. The footer pinned and
    /// on screen with the settings at their top; the founding's ring left of the panel, and the marker left of it for a
    /// founding at the valley's east edge too; the share code never widening the panel.
    /// </summary>
    internal IReadOnlyList<string> LayoutFaults()
    {
        var faults = new List<string>();
        float height = Size.Y;
        float panelLeft = _panel.GlobalPosition.X;

        _scroll.ScrollVertical = 0;
        if (_scroll.IsAncestorOf(_found))
        {
            faults.Add("Found scrolls with the settings (the footer is not pinned)");
        }

        Rect2 found = _found.GetGlobalRect();
        if (found.Position.Y < -0.5f || found.End.Y > GlobalPosition.Y + height + 0.5f)
        {
            faults.Add($"Found runs {found.Position.Y:F0}–{found.End.Y:F0}px in a {height:F0}px window");
        }

        if (_previewWorld is not null)
        {
            (Vector2 at, float radius) = RingOfTheFounding();
            if (GlobalPosition.X + at.X + radius > panelLeft + 0.5f)
            {
                faults.Add($"the founding's ring reaches {GlobalPosition.X + at.X + radius:F0}px, under the panel at {panelLeft:F0}");
            }

            SimConfig c = _previewWorld.Config;
            (Vector2 east, _) = Ring(c.MapMaxX - c.StartingResidentialRadius, c.MapMinY + (c.MapHeight / 2));
            if (GlobalPosition.X + east.X > panelLeft + 0.5f)
            {
                faults.Add($"a founding at the valley's east edge is marked at {GlobalPosition.X + east.X:F0}px, under the panel at {panelLeft:F0}");
            }
        }

        // Joe's range is 25–30 % (§12.2): 28 % is the aim, and a row that cannot narrow may take the panel to 30, no further.
        float most = Mathf.Ceil(Size.X * PanelMostShare);
        if (_panel.Size.X > most + 0.5f)
        {
            faults.Add($"the panel is {_panel.Size.X:F0}px, past {most:F0} ({PanelMostShare:P0}) — something in it will not narrow");
        }

        return faults;
    }

    public override void _Ready()
    {
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);

        // ⭐ Joe's reading face for every word here (A147, Cardo), the blackletter put back on the title and headings below.
        if (UiLook.ReadingFace is { } reading)
        {
            Theme = new Theme { DefaultFont = reading };
        }

        var backdrop = new Panel { MouseFilter = MouseFilterEnum.Ignore };
        backdrop.AddThemeStyleboxOverride("panel", UiLook.Backdrop());
        backdrop.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        AddChild(backdrop);

        AddChild(BuildTheMap());
        AddChild(BuildTheStrip());
        AddChild(BuildThePanel());
        AlignTheLabels();

        Resized += PlaceEverything;
        ShowTheSettings();
        CallDeferred(MethodName.PlaceEverything);
    }

    private Control BuildTheMap()
    {
        _map = new Control { MouseFilter = MouseFilterEnum.Ignore, ClipContents = true };
        _map.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);

        _preview = new TextureRect
        {
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.Scale,
            TextureFilter = TextureFilterEnum.Linear,
            MouseFilter = MouseFilterEnum.Ignore,
        };
        _map.AddChild(_preview);

        // The founders' camp, ringed: where the village will begin.
        _mark = new Control { MouseFilter = MouseFilterEnum.Ignore };
        _mark.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        _mark.Draw += DrawTheFounding;
        _map.AddChild(_mark);
        return _map;
    }

    /// <summary>The summary strip (§12.4): a slip at the map's bottom-left, what the valley has, in the sim's words.</summary>
    private Control BuildTheStrip()
    {
        _strip = new PanelContainer { MouseFilter = MouseFilterEnum.Ignore };
        _strip.AddThemeStyleboxOverride("panel", UiLook.TopBox());

        // ⛔ Wrapped, and its width set where it is placed: a bare label is a minimum WIDTH, and the longest line
        // ("River 6 wide, north-west to south-east (the seed's choice)") once pushed a column off the screen (D479).
        _stats = new Label { AutowrapMode = TextServer.AutowrapMode.WordSmart };
        _stats.AddThemeFontSizeOverride("font_size", 15);
        _strip.AddChild(_stats);
        return _strip;
    }

    private Control BuildThePanel()
    {
        _panel = new PanelContainer
        {
            AnchorLeft = 1f,
            AnchorRight = 1f,
            AnchorTop = 0f,
            AnchorBottom = 1f,
        };
        _panel.AddThemeStyleboxOverride("panel", UiLook.Panel());
        Column = _panel;

        var body = new VBoxContainer();
        body.AddThemeConstantOverride("separation", 8);
        _panel.AddChild(body);

        body.AddChild(Heading("A new valley", 30));

        // The settings scroll; the title above and the footer below never do (§12.2, 1280 × 720).
        _scroll = new ScrollContainer
        {
            HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled,
            SizeFlagsVertical = SizeFlags.ExpandFill,
        };
        body.AddChild(_scroll);
        var settings = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        settings.AddThemeConstantOverride("separation", 6);
        _scroll.AddChild(settings);

        BuildTheSeedAndName(settings);
        BuildTheGroups(settings);

        body.AddChild(new HSeparator());
        BuildTheFooter(body);
        return _panel;
    }

    /// <summary>Seed and Village name: the label above, the field the panel's full width (§12.2).</summary>
    private void BuildTheSeedAndName(VBoxContainer settings)
    {
        settings.AddChild(Caption("Seed — any words, or paste a share code"));
        var seedRow = new HBoxContainer();
        _seed = new LineEdit { Text = _settings.SeedText, SizeFlagsHorizontal = SizeFlags.ExpandFill };
        _seed.TextChanged += OnSeedTyped;
        seedRow.AddChild(_seed);
        var dice = new Button { Text = "Roll", TooltipText = "Roll a new seed of words" };
        dice.Pressed += () => _seed.Text = RollASeed();
        dice.Pressed += () => OnSeedTyped(_seed.Text);
        seedRow.AddChild(dice);
        settings.AddChild(seedRow);

        _refusal = new Label
        {
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
            Visible = false,
            CustomMinimumSize = new Vector2(120, 0),
        };
        UiLook.Tint(_refusal, UiLook.Refused);
        _refusal.AddThemeFontSizeOverride("font_size", 14);
        settings.AddChild(_refusal);

        settings.AddChild(Caption("Village name"));
        _name = new LineEdit { MaxLength = NewGame.LongestVillageName, SizeFlagsHorizontal = SizeFlags.ExpandFill };
        // The seed's own name shows in the empty field: inked like a caption, not the theme's near-invisible hint.
        _name.AddThemeColorOverride("font_placeholder_color", UiLook.Hint);
        settings.AddChild(_name);
    }

    /// <summary>
    /// ⭐ The rows under their headings (§12.2): each row's <see cref="NewGameRow.Group"/>, in the rows' own order — which
    /// is the share code's — with a rule between groups; a row with no group after the last heading, unheaded. The
    /// Valley row (one archetype for now) leads the first group.
    /// </summary>
    private void BuildTheGroups(VBoxContainer settings)
    {
        var groups = new List<(string Heading, List<NewGameRow> Rows)>();
        foreach (NewGameRow row in _config.NewGameOptions.Where(r => r.Group.Length > 0))
        {
            int at = groups.FindIndex(g => g.Heading == row.Group);
            if (at < 0)
            {
                groups.Add((row.Group, new List<NewGameRow>()));
                at = groups.Count - 1;
            }

            groups[at].Rows.Add(row);
        }

        List<NewGameRow> unheaded = _config.NewGameOptions.Where(r => r.Group.Length == 0).ToList();
        if (unheaded.Count > 0 || groups.Count == 0)
        {
            groups.Add((string.Empty, unheaded));
        }

        for (int i = 0; i < groups.Count; i++)
        {
            settings.AddChild(new HSeparator());
            if (groups[i].Heading.Length > 0)
            {
                settings.AddChild(Heading(groups[i].Heading, 21));
            }

            var grid = new GridContainer { Columns = 2, SizeFlagsHorizontal = SizeFlags.ExpandFill };
            grid.AddThemeConstantOverride("h_separation", 12);
            grid.AddThemeConstantOverride("v_separation", 6);
            if (i == 0)
            {
                var valley = new OptionButton { TooltipText = "One kind of valley for now — more will come.", SizeFlagsHorizontal = SizeFlags.ExpandFill };
                valley.AddItem("River valley");
                AddARow(grid, "Valley", valley);
            }

            foreach (NewGameRow row in groups[i].Rows)
            {
                AddARow(grid, row.Label, BuildARow(row));
            }

            settings.AddChild(grid);
        }
    }

    /// <summary>
    /// One label column through every group, so the controls line up down the panel, heading to heading. Measured once
    /// the panel is in the tree — before that a label measures in the default face, not the reading one.
    /// </summary>
    private void AlignTheLabels()
    {
        float widest = _rowLabels.Max(l => l.GetCombinedMinimumSize().X);
        foreach (Label label in _rowLabels)
        {
            label.CustomMinimumSize = label.CustomMinimumSize with { X = widest };
        }
    }

    private void AddARow(GridContainer grid, string text, Control control)
    {
        // The label sits level with the control's first line — a slider's groove, not its named ends.
        var label = new Label { Text = text, SizeFlagsVertical = SizeFlags.ShrinkBegin, CustomMinimumSize = new Vector2(0, 28), VerticalAlignment = VerticalAlignment.Center };
        _rowLabels.Add(label);
        grid.AddChild(label);
        grid.AddChild(control);
    }

    private Control BuildARow(NewGameRow row)
    {
        var box = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        box.AddThemeConstantOverride("separation", 0);

        if (row.Kind == NewGame.LevelsKind)
        {
            var choice = new OptionButton { SizeFlagsHorizontal = SizeFlags.ExpandFill, CustomMinimumSize = new Vector2(0, 28) };
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

            // "as configured", when the file's values match no level — said under the choice, hidden otherwise.
            Label reading = Caption(string.Empty);
            box.AddChild(reading);
            _rowReadings[row.Id] = reading;
            return box;
        }

        var line = new HBoxContainer { CustomMinimumSize = new Vector2(0, 28) };
        line.AddThemeConstantOverride("separation", 8);
        var slider = new HSlider
        {
            MinValue = row.Min,
            MaxValue = row.Max,
            Step = row.Step,
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ShrinkCenter,
            CustomMinimumSize = new Vector2(100, 0),
        };
        slider.ValueChanged += value =>
        {
            if (!_settingControls)
            {
                SetRow(row.Id, ((int)value).ToString(CultureInfo.InvariantCulture));
            }
        };
        line.AddChild(slider);
        _rowControls[row.Id] = slider;

        var number = new Label { HorizontalAlignment = HorizontalAlignment.Right, CustomMinimumSize = new Vector2(40, 0), VerticalAlignment = VerticalAlignment.Center };
        number.Visible = !row.HasEndLabels;
        line.AddChild(number);
        _rowReadings[row.Id] = number;
        box.AddChild(line);

        if (row.HasEndLabels)
        {
            // The ends named, no number (D481) — under the slider's two ends (§12.2); the share code keeps the number.
            var ends = new HBoxContainer();
            ends.AddChild(Caption(row.MinLabel));
            ends.AddChild(new Control { SizeFlagsHorizontal = SizeFlags.ExpandFill });
            ends.AddChild(Caption(row.MaxLabel));
            box.AddChild(ends);
        }

        return box;
    }

    /// <summary>The share code and Copy, then Back, Defaults and Found — pinned under the scroll (§12.2).</summary>
    private void BuildTheFooter(VBoxContainer body)
    {
        body.AddChild(Caption("Share code — the seed and every setting"));
        var codeRow = new HBoxContainer();

        // ⛔ Never shrunk, never widening the panel: one line in the field's own look, cut with an ellipsis where it runs
        // out (§12.2); the tooltip and Copy carry all of it.
        var codeBox = new PanelContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        codeBox.AddThemeStyleboxOverride("panel", GetThemeStylebox("normal", "LineEdit"));
        _code = new Label
        {
            ClipText = true,
            TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis,
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            CustomMinimumSize = new Vector2(40, 0),
            MouseFilter = MouseFilterEnum.Pass,
        };
        codeBox.AddChild(_code);
        codeRow.AddChild(codeBox);
        var copy = new Button { Text = "Copy", TooltipText = "Copy the share code" };
        copy.Pressed += () => DisplayServer.ClipboardSet(_code.Text);
        codeRow.AddChild(copy);
        body.AddChild(codeRow);

        var buttons = new HBoxContainer();
        buttons.AddThemeConstantOverride("separation", 6);

        // ⭐ Back to the title (D516): nothing is founded, and the rows are not remembered — they are when a
        // village is founded (D504).
        var back = new Button { Text = "Back", TooltipText = "Back to the title" };
        back.Pressed += () => BackAsked?.Invoke();
        buttons.AddChild(back);
        var defaults = new Button { Text = "Defaults", TooltipText = "Every setting back to the game's own" };
        defaults.Pressed += OnDefaults;
        buttons.AddChild(defaults);
        buttons.AddChild(new Control { SizeFlagsHorizontal = SizeFlags.ExpandFill });

        // The one primary button (§12.2): filled rubric and larger in the parchment look.
        _found = new Button { Text = "Found a new village", ThemeTypeVariation = UiLook.PrimaryButton };
        _found.Pressed += OnFound;
        buttons.AddChild(_found);
        body.AddChild(buttons);
    }

    /// <summary>A title or a group's heading: the blackletter, and only here (§12.3).</summary>
    private static Label Heading(string text, int size)
    {
        var label = new Label { Text = text };
        if (UiLook.Blackletter is { } blackletter)
        {
            label.AddThemeFontOverride("font", blackletter);
        }

        label.AddThemeFontSizeOverride("font_size", size);
        return label;
    }

    private static Label Caption(string text)
    {
        var label = new Label { Text = text };
        label.AddThemeFontSizeOverride("font_size", 13);
        UiLook.Tint(label, UiLook.Hint);
        return label;
    }

    private string RollASeed() => RollASeed(_config, _dice);

    /// <summary>A seed of words, rolled by the view's dice — the title's rolled valley uses it too (D516).</summary>
    internal static string RollASeed(SimConfig config, System.Random dice)
    {
        IReadOnlyList<string> words = config.SeedWords;
        return $"{words[dice.Next(words.Count)]}-{words[dice.Next(words.Count)]}-{dice.Next(10, 100)}";
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
                    _rowReadings[row.Id].Visible = index < 0;
                    break;
                case HSlider slider:
                    slider.Value = int.Parse(value, CultureInfo.InvariantCulture);
                    _rowReadings[row.Id].Text = row.HasEndLabels ? string.Empty : value + row.Unit;
                    break;
            }
        }

        _settingControls = false;
        _code.Text = NewGame.ShareCode(_config, _settings);
        _code.TooltipText = _code.Text;
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

        _stats.Text = NewGame.Describe(config, NewGame.Summarise(config, _previewWorld.Map));
        _name.PlaceholderText = _previewWorld.Name;
        PlaceEverything();
    }

    /// <summary>
    /// The panel's width, the strip's place and the camera — on every resize, and whenever the valley or the strip's
    /// words change. Nothing here is per frame.
    /// </summary>
    private void PlaceEverything()
    {
        if (!IsInsideTree())
        {
            return;
        }

        Vector2 window = Size;
        float panelWidth = Mathf.Ceil(Mathf.Max(window.X * PanelShare, _panel.GetCombinedMinimumSize().X));
        _panel.OffsetLeft = -panelWidth;
        _panel.OffsetRight = 0f;
        _panel.OffsetTop = 0f;
        _panel.OffsetBottom = 0f;
        float room = Room;

        PlaceTheStrip(room, window.Y);

        if (_previewWorld is null || _bake.Texture is null)
        {
            return;
        }

        (_origin, _perTile) = Camera(_previewWorld.Map.FoundingSite.X, _previewWorld.Map.FoundingSite.Y, room, window.Y);
        _preview.Position = _origin;
        _preview.Size = new Vector2(_previewWorld.Config.MapWidth, _previewWorld.Config.MapHeight) * _perTile;
        _mark.QueueRedraw();
    }

    private void PlaceTheStrip(float room, float height)
    {
        StyleBox skin = _strip.GetThemeStylebox("panel");
        Font font = _stats.GetThemeFont("font");
        int size = _stats.GetThemeFontSize("font_size");
        float words = Mathf.Ceil(font.GetStringSize(_stats.Text, HorizontalAlignment.Left, -1, size).X) + 2f;
        float most = Mathf.Max(120f, room - (2 * StripInset) - skin.GetMinimumSize().X);
        _stats.CustomMinimumSize = new Vector2(Mathf.Min(words, most), 0f);
        _strip.ResetSize();
        _strip.Position = new Vector2(StripInset, height - StripInset - _strip.Size.Y);
    }

    /// <summary>
    /// ⭐ The camera for a founding at tile (<paramref name="x"/>, <paramref name="y"/>) (§12.2, call 3): the valley
    /// scaled so its height fills the window (and at least the room's width), then moved so the founding sits in the
    /// middle of the room left of the panel — stopped at the valley's edge, so no bare paper shows in the room.
    /// </summary>
    private (Vector2 Origin, float PerTile) Camera(int x, int y, float room, float height)
    {
        SimConfig c = _previewWorld!.Config;
        float perTile = Mathf.Max(height / c.MapHeight, room / c.MapWidth);
        var valley = new Vector2(c.MapWidth * perTile, c.MapHeight * perTile);
        var site = new Vector2((x - c.MapMinX + 0.5f) * perTile, (y - c.MapMinY + 0.5f) * perTile);

        return (new Vector2(Stopped((room / 2f) - site.X, room, valley.X), Stopped((height / 2f) - site.Y, height, valley.Y)), perTile);

        // Wider than the room: the valley's edges stay outside it. Narrower (a tall window): kept inside it.
        static float Stopped(float at, float span, float extent) =>
            extent >= span ? Mathf.Clamp(at, span - extent, 0f) : Mathf.Clamp(at, 0f, span - extent);
    }

    /// <summary>The room left of the panel, in screen pixels.</summary>
    private float Room => Size.X - _panel.Size.X;

    /// <summary>
    /// Where the ring for a founding at that tile would be drawn, in the screen's own coordinates, and its radius — the
    /// camera worked out afresh for that founding (the probe's east-edge pose).
    /// </summary>
    private (Vector2 At, float Radius) Ring(int x, int y)
    {
        (Vector2 origin, float perTile) = Camera(x, y, Room, Size.Y);
        return RingAt(x, y, origin, perTile);
    }

    /// <summary>The founding's ring under the camera the map was actually placed with — what the player sees.</summary>
    private (Vector2 At, float Radius) RingOfTheFounding() =>
        RingAt(_previewWorld!.Map.FoundingSite.X, _previewWorld.Map.FoundingSite.Y, _origin, _perTile);

    private (Vector2 At, float Radius) RingAt(int x, int y, Vector2 origin, float perTile)
    {
        SimConfig c = _previewWorld!.Config;
        Vector2 at = origin + new Vector2((x - c.MapMinX + 0.5f) * perTile, (y - c.MapMinY + 0.5f) * perTile);
        return (at, Mathf.Max(6f, c.StartingResidentialRadius * perTile));
    }

    /// <summary>Ring the founding site — the red ring and dot — where the camera put it.</summary>
    private void DrawTheFounding()
    {
        if (_previewWorld is null || _bake.Texture is null)
        {
            return;
        }

        (Vector2 at, float radius) = RingOfTheFounding();
        _mark.DrawArc(at, radius, 0f, Mathf.Tau, 64, UiLook.FoundingMark, 2.5f, true);
        _mark.DrawCircle(at, 4f, UiLook.FoundingMark);
    }
}

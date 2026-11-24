using Kith.Sim.Config;
using Godot;

namespace Kith.Game;

/// <summary>
/// ⭐ The title screen — what the game opens on (D516, `specs/title-and-pause.md §3`).
/// </summary>
/// <remarks>
/// <para>
/// <b>Its rules are Main's and the sim's; the screen only lays them out</b> — the new-game screen's shape. It
/// raises an event for every button and Main acts on it, so a village still comes in from disk by one door
/// (<c>Main.OpenTheVillage</c>).
/// </para>
/// <para>
/// ⭐ <b>Behind it, the player's newest village with a home, painted in watercolour</b> (A234, `title-snapshot.md`; Joe: <i>"Yes,
/// (e) and the lift"</i>, then <i>"Yes to all three"</i>): the <c>title.jpg</c> in the newest save's folder that has one
/// (<see cref="TitleSnapshot"/>), covering the window and centred, under two shaders. With none, one village in the four seasons
/// (A190, `title-screen-art.md §4`; Joe: <i>"B with ink seams"</i>), written by <c>tools/title-capture</c>, painted the same way.
/// </para>
/// <para>
/// ⭐⭐ <b>A parchment panel docked left</b> (A180, `title-screen-art.md §2`; Joe's brief, <i>"All as recommended"</i>): the dark
/// scrim it replaces left ink words on a dark ground. <i>Kith</i> in the blackletter, the items in the heading face, the one
/// red button the primary — <i>Continue</i> when a save opens, <i>New village</i> otherwise — and with no save neither
/// <i>Continue</i> nor <i>Load…</i> is shown. <i>Settings</i> (Joe: <i>"add a 'settings' option"</i>) turns the panel to the
/// player's own settings — the window and the map's ticks — read and written through <see cref="SettingsDoors"/>.
/// </para>
/// </remarks>
public sealed partial class TitleScreen : Control
{
    /// <summary>The name on the title (Joe, D516: <i>"bclone" for now</i>) — one string, changed once.</summary>
    internal const string GameName = "Kith";

    /// <summary>The panel's share of a wide window — the new-valley panel's (A148); never narrower than its contents.</summary>
    internal const float PanelShare = 0.28f;

    /// <summary>The four seasons, written by <c>tools/title-capture</c> (A190): 3840 × 2160.</summary>
    internal const string BackdropPath = "res://textures/ui/title/valley_seasons.jpg";

    /// <summary>The washes, drawn once over the picture; the paint, every frame, over the washes (A233, `title-snapshot.md §5b`).</summary>
    internal const string WashShaderPath = "res://shaders/title_watercolour_wash.gdshader", PaintShaderPath = "res://shaders/title_watercolour.gdshader";

    /// <summary>What the two shaders read besides the picture (<c>bake_wc.py</c>'s three).</summary>
    internal const string MarksPath = "res://textures/ui/title/watercolour-wash.png",
        FinishPath = "res://textures/ui/title/watercolour-finish.png",
        PaperPath = "res://textures/ui/title/watercolour-paper.png";

    /// <summary>Where the village's houses centre in that picture, as shares of its width and height (the tool's framing B).</summary>
    internal static readonly Vector2 VillageInPicture = new(0.56f, 0.50f);

    /// <summary>
    /// ⭐ Kith's seal, the logo (A220–A223, `specs/logo.md`; Joe: <i>"i also want to see it here in the main menu somewhere"</i>,
    /// then <i>"A, above the name"</i>): the game's wax seal with the title's K pressed into it, 512 × 512, its wax 382 wide.
    /// </summary>
    internal const string SealPath = "res://textures/ui/screens/seal-k.png";

    /// <summary>The seal's box, square: 118 px shows its wax at 88 (the picture is 512 with the wax 382 across).</summary>
    internal const float SealBox = 118f;

    /// <summary>The wax inside the box: 13.6 px of clear canvas above it, 16.4 below (rows 59–441 of 512).</summary>
    internal const float SealWaxTop = 13.6f, SealWaxBottom = 101.6f;

    /// <summary>
    /// Where the K's capital starts under the name's top edge: the blackletter at 96 px carries a tall empty band above its
    /// capitals (measured on the 1280 × 800 capture, A223: 46.6 px).
    /// </summary>
    internal const float NameCapTop = 46f;

    /// <summary>
    /// Where the seal's box ends under the name's top edge: its wax (16.4 px above the box's foot) ends 20 px above the
    /// name's capitals, as Joe's mock A had it (A223: 18 left 45 px of paper between them, which read as two things).
    /// </summary>
    internal const float SealFoot = 43f;

    /// <summary>Clear paper the seal keeps above it, inside the sheet's margins, or it is not shown (a short window).</summary>
    internal const float SealGap = 8f;

    /// <summary>The picture, loaded once and held (art trap 69: a resource is never loaded and dropped).</summary>
    private static Texture2D? _seasons;

    /// <summary>The paint's own pictures and shaders, held for the same reason.</summary>
    private static Texture2D? _marks, _finish, _paper;

    private static Shader? _washShader, _paintShader;

    private static Texture2D? _seal;

    private readonly SimConfig _config;
    private readonly string _build;
    private readonly SettingsDoors _doors;

    private TextureRect _backdrop = null!;
    private Label _refusal = null!;
    private VBoxContainer _menu = null!;
    private Label _scaleReading = null!;

    /// <summary>For Godot, which may construct a script's node itself; the game uses the other.</summary>
    public TitleScreen()
        : this(new SimConfig(), Array.Empty<SaveListing>(), "?", SettingsDoors.None)
    {
    }

    /// <summary>The title over <paramref name="saves"/> (newest first, as the load list shows them).</summary>
    internal TitleScreen(SimConfig config, IReadOnlyList<SaveListing> saves, string build, SettingsDoors doors)
    {
        _config = config;
        Saves = saves;
        _build = build;
        _doors = doors;
    }

    /// <summary>Asked to open the save at this path — <i>Continue</i> or <i>Load…</i>.</summary>
    public event Action<string>? LoadAsked;

    /// <summary><i>New village</i>: the new-game screen.</summary>
    public event Action? NewVillageAsked;

    /// <summary><i>Quit</i>: no village is open, so nothing is saved.</summary>
    public event Action? QuitAsked;

    /// <summary>A save the player can open, as the list shows it — or one it can only show, greyed, with why.</summary>
    internal sealed record SaveListing(string Path, string Line, string? Refusal);

    /// <summary>
    /// What the title's Settings reads and writes (A180): the player's ticks (<c>Main.PlayerTicks</c>, the village's Settings'
    /// own table), each one's value as last left, where a change goes, and the UI size's reading and its step.
    /// </summary>
    internal sealed record SettingsDoors(
        IReadOnlyList<PlayerTick> Ticks, Func<string, bool> Value, Action<string, bool> Changed, Func<int> UiScale, Func<int, int> MoveUiScale)
    {
        /// <summary>No settings — Godot's own construction only.</summary>
        internal static SettingsDoors None { get; } = new(Array.Empty<PlayerTick>(), _ => false, (_, _) => { }, () => 0, _ => 0);
    }

    /// <summary>Every save on disk, newest first.</summary>
    internal IReadOnlyList<SaveListing> Saves { get; }

    /// <summary>The newest save that opens, which <i>Continue</i> opens — none, and there is no <i>Continue</i>.</summary>
    internal static SaveListing? NewestThatOpens(IReadOnlyList<SaveListing> saves) => saves.FirstOrDefault(s => s.Refusal is null);

    /// <summary>For the probe: the panel, docked left.</summary>
    internal PanelContainer Band { get; private set; } = null!;

    /// <summary>For the probe: <i>Kith</i>.</summary>
    internal Label TitleLabel { get; private set; } = null!;

    /// <summary>For the probe: Kith's seal above the name.</summary>
    internal TextureRect Seal { get; private set; } = null!;

    /// <summary>For the probe: the version at the foot.</summary>
    internal Label VersionLabel { get; private set; } = null!;

    /// <summary>For the probe and the tooltip: <i>Continue</i>.</summary>
    internal Button ContinueButton { get; private set; } = null!;

    /// <summary>For the probe: <i>New village</i>.</summary>
    internal Button NewVillageButton { get; private set; } = null!;

    /// <summary>For the probe: <i>Load…</i>.</summary>
    internal MenuButton LoadButton { get; private set; } = null!;

    /// <summary>For the probe: <i>Settings</i>.</summary>
    internal Button SettingsButton { get; private set; } = null!;

    /// <summary>For the probe: <i>Quit</i>.</summary>
    internal Button QuitButton { get; private set; } = null!;

    /// <summary>The one red item: <i>Continue</i> with a save that opens, <i>New village</i> without.</summary>
    internal Button PrimaryItem { get; private set; } = null!;

    /// <summary>The items shown, top to bottom — the order the arrow keys walk.</summary>
    internal IReadOnlyList<Button> ShownItems { get; private set; } = Array.Empty<Button>();

    /// <summary>For the probe: the Settings page.</summary>
    internal VBoxContainer SettingsPage { get; private set; } = null!;

    /// <summary>For the probe: the Settings page's ticks, by key, in the table's order.</summary>
    internal IReadOnlyList<(string Key, CheckBox Box)> SettingsTicks { get; private set; } = Array.Empty<(string, CheckBox)>();

    /// <summary>For the probe: the Settings page's way back.</summary>
    internal Button SettingsBack { get; private set; } = null!;

    /// <summary>For the probe: the backdrop has a valley on it.</summary>
    internal bool HasABackdrop => _backdrop.Texture is not null;

    /// <summary>For the probe: the picture behind the title.</summary>
    internal TextureRect Backdrop => _backdrop;

    /// <summary>The village picture shown, or null over the four seasons.</summary>
    internal string? VillagePicture { get; private set; }

    /// <summary>For the probe: where the washes are drawn, once — null with the paint missing.</summary>
    internal SubViewport? Washes { get; private set; }

    public override void _Ready()
    {
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);

        var floor = new Panel();
        floor.AddThemeStyleboxOverride("panel", UiLook.Backdrop());
        floor.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        AddChild(floor);

        _backdrop = new TextureRect
        {
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.KeepAspectCovered,
            TextureFilter = TextureFilterEnum.Linear,
            MouseFilter = MouseFilterEnum.Ignore,
        };
        _backdrop.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        AddChild(_backdrop);

        AddChild(BuildThePanel());
        LinkTheItems();
        PlaceThePanel();
        Resized += PlaceThePanel;
        Callable.From(() =>
        {
            if (PrimaryItem.IsInsideTree())
            {
                PrimaryItem.GrabFocus();
            }
        }).CallDeferred();
        ShowTheVillage();
    }

    /// <summary>Esc on the Settings page goes back to the menu, as its <i>Back</i> does.</summary>
    public override void _UnhandledKeyInput(InputEvent e)
    {
        if (SettingsPage.Visible && e is InputEventKey { Pressed: true, Keycode: Key.Escape })
        {
            ShowTheMenu();
            GetViewport().SetInputAsHandled();
        }
    }

    /// <summary>A save that would not open, said where the player is looking. The title stays.</summary>
    internal void RefuseTheLoad(string sentence)
    {
        _refusal.Text = sentence;
        _refusal.Visible = true;
    }

    /// <summary>For the probe: what pressing <i>New village</i> does.</summary>
    internal void PressNewVillage() => NewVillageAsked?.Invoke();

    /// <summary>The panel turns to Settings, the first tick taking the keyboard.</summary>
    internal void ShowTheSettings()
    {
        _menu.Visible = false;
        SettingsPage.Visible = true;
        _scaleReading.Text = $"{_doors.UiScale()}%";
        if (SettingsTicks.Count > 0)
        {
            SettingsTicks[0].Box.GrabFocus();
        }
    }

    /// <summary>Back to the menu, <i>Settings</i> taking the keyboard again.</summary>
    internal void ShowTheMenu()
    {
        SettingsPage.Visible = false;
        _menu.Visible = true;
        SettingsButton.GrabFocus();
    }

    /// <summary>
    /// <b>Load…</b>: every save, newest first, a refused one greyed with its refusal as the tooltip — the title's
    /// and the pause screen's (`title-and-pause.md §3, §5`). A popup, never a list: a list widens its column to
    /// its longest line.
    /// </summary>
    /// <remarks>
    /// ⭐ <b>It owns its list</b> (D518): the pause screen's stayed as it was opened, so a save made while it was up
    /// never appeared in it — Joe saved <i>test</i> three times and saw no <i>test</i>. ⭐ <b>And the pause screen's
    /// reads it only as its popup opens</b> (D805, §5.2): built when the pause screen opened, it read every save on disk
    /// and froze Joe's game for 830 ms. Read at the popup, it is today's list every time, a fresh save included.
    /// </remarks>
    internal sealed class LoadMenu
    {
        /// <summary>The one row a list read at the popup holds when there is no save — the title's <i>Continue</i> tooltip.</summary>
        internal const string NoSaveYet = "No saved village yet.";

        private readonly List<SaveListing> _listed = new();

        /// <summary>The title's: the list it was given, read up front — it needs the newest save for <i>Continue</i>.</summary>
        internal LoadMenu(IReadOnlyList<SaveListing> saves, Action<string> picked)
            : this(picked)
        {
            Fill(saves);
            Button.Disabled = _listed.Count == 0;
        }

        /// <summary>The pause screen's: nothing read until the popup is about to open, then <paramref name="read"/> each time.</summary>
        internal LoadMenu(Func<IReadOnlyList<SaveListing>> read, Action<string> picked)
            : this(picked)
        {
            Button.AboutToPopup += () => Fill(read());
        }

        private LoadMenu(Action<string> picked)
        {
            // ⚠️ Focusable (A180): a MenuButton is not by default, so the arrow keys walked past Load… (found by the title's mock).
            Button = new MenuButton { Text = "Load…", Flat = false, FocusMode = FocusModeEnum.All };
            Button.GetPopup().IdPressed += id =>
            {
                if (id < _listed.Count)
                {
                    picked(_listed[(int)id].Path);
                }
            };
        }

        internal MenuButton Button { get; }

        /// <summary>Every row again, from <paramref name="saves"/>; with none, one greyed row that says so.</summary>
        private void Fill(IReadOnlyList<SaveListing> saves)
        {
            _listed.Clear();
            _listed.AddRange(saves);
            PopupMenu menu = Button.GetPopup();
            menu.Clear();
            for (int i = 0; i < _listed.Count; i++)
            {
                menu.AddItem(_listed[i].Line, i);
                menu.SetItemDisabled(i, _listed[i].Refusal is not null);
                menu.SetItemTooltip(i, _listed[i].Refusal ?? _listed[i].Path);
            }

            if (_listed.Count == 0)
            {
                menu.AddItem(NoSaveYet, 0);
                menu.SetItemDisabled(0, true);
            }
        }
    }

    private Control BuildThePanel()
    {
        Band = new PanelContainer { AnchorLeft = 0f, AnchorRight = 0f, AnchorTop = 0f, AnchorBottom = 1f };
        Band.AddThemeStyleboxOverride("panel", UiLook.Panel());

        // Two pages in one sheet, one shown: the menu, and the player's settings.
        var stack = new VBoxContainer();
        Band.AddChild(stack);
        _menu = BuildTheMenu();
        stack.AddChild(_menu);
        SettingsPage = BuildTheSettings();
        SettingsPage.Visible = false;
        stack.AddChild(SettingsPage);
        return Band;
    }

    private VBoxContainer BuildTheMenu()
    {
        var column = new VBoxContainer { SizeFlagsVertical = SizeFlags.ExpandFill };
        column.AddThemeConstantOverride("separation", 6);
        column.AddChild(new Control { SizeFlagsVertical = SizeFlags.ExpandFill, SizeFlagsStretchRatio = 0.5f });

        TitleLabel = new Label { Text = GameName, HorizontalAlignment = HorizontalAlignment.Center };
        if (UiLook.Blackletter is { } blackletter)
        {
            TitleLabel.AddThemeFontOverride("font", blackletter);
        }

        TitleLabel.AddThemeFontSizeOverride("font_size", 96);
        column.AddChild(TitleLabel);
        PressTheSeal();
        column.AddChild(new ColorRect { Color = UiLook.SelectedEdge, CustomMinimumSize = new Vector2(120, 2), SizeFlagsHorizontal = SizeFlags.ShrinkCenter });
        column.AddChild(new Control { CustomMinimumSize = new Vector2(0, 36) });

        SaveListing? newest = NewestThatOpens(Saves);
        ContinueButton = new Button { Text = "Continue", TooltipText = newest?.Line ?? LoadMenu.NoSaveYet, Visible = newest is not null };
        ContinueButton.Pressed += () => LoadAsked?.Invoke(newest!.Path);
        NewVillageButton = new Button { Text = "New village" };
        NewVillageButton.Pressed += () => NewVillageAsked?.Invoke();
        LoadButton = new LoadMenu(Saves, path => LoadAsked?.Invoke(path)).Button;
        LoadButton.Visible = Saves.Count > 0;
        SettingsButton = new Button { Text = "Settings" };
        SettingsButton.Pressed += ShowTheSettings;
        QuitButton = new Button { Text = "Quit" };
        QuitButton.Pressed += () => QuitAsked?.Invoke();
        PrimaryItem = newest is not null ? ContinueButton : NewVillageButton;

        foreach (Button item in new Button[] { ContinueButton, NewVillageButton, LoadButton, SettingsButton, QuitButton })
        {
            item.ThemeTypeVariation = item == PrimaryItem ? UiLook.TitlePrimary : UiLook.TitleItem;
            item.CustomMinimumSize = new Vector2(250, 46);
            item.SizeFlagsHorizontal = SizeFlags.ShrinkCenter;
            column.AddChild(item);
        }

        ShownItems = new Button[] { ContinueButton, NewVillageButton, LoadButton, SettingsButton, QuitButton }.Where(b => b.Visible).ToArray();

        _refusal = new Label
        {
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
            Visible = false,
            CustomMinimumSize = new Vector2(260, 0),
            HorizontalAlignment = HorizontalAlignment.Center,
        };
        UiLook.Tint(_refusal, UiLook.Refused);
        _refusal.AddThemeFontSizeOverride("font_size", 14);
        column.AddChild(_refusal);
        column.AddChild(new Control { SizeFlagsVertical = SizeFlags.ExpandFill });

        VersionLabel = new Label { Text = _build, HorizontalAlignment = HorizontalAlignment.Center };
        UiLook.Tint(VersionLabel, UiLook.Faint);
        VersionLabel.AddThemeFontSizeOverride("font_size", 13);
        column.AddChild(VersionLabel);
        return column;
    }

    /// <summary>
    /// The player's own settings (A180, Joe: <i>"Player's settings"</i>): the window — full screen and the UI size — then every
    /// tick of the village's <i>On the map</i>, then <i>Alerts</i>, each under the heading its row names, as last left. Not here,
    /// because an open village owns them: the window list, <i>How the village runs</i>, <i>About this run</i>.
    /// </summary>
    private VBoxContainer BuildTheSettings()
    {
        var page = new VBoxContainer { SizeFlagsVertical = SizeFlags.ExpandFill };
        page.AddThemeConstantOverride("separation", 8);

        var heading = new Label { Text = "Settings", HorizontalAlignment = HorizontalAlignment.Center, ThemeTypeVariation = UiLook.HeaderLabel };
        heading.AddThemeFontSizeOverride("font_size", 28);
        page.AddChild(heading);

        var scroll = new ScrollContainer { HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled, SizeFlagsVertical = SizeFlags.ExpandFill };
        page.AddChild(scroll);
        var rows = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        rows.AddThemeConstantOverride("separation", 4);
        scroll.AddChild(rows);

        var ticks = new List<(string, CheckBox)>();
        string? group = null;
        foreach (PlayerTick tick in _doors.Ticks)
        {
            if (tick.Group != group)
            {
                group = tick.Group;
                var title = new Label { Text = group };
                UiLook.Tint(title, UiLook.Hint);
                title.AddThemeFontSizeOverride("font_size", 14);
                rows.AddChild(title);
                if (group == "The window")
                {
                    rows.AddChild(TheUiSizeRow());
                }
            }

            var box = new CheckBox { Text = tick.Label, ButtonPressed = _doors.Value(tick.Key) };
            box.AddThemeFontSizeOverride("font_size", 16);
            string key = tick.Key;
            box.Toggled += on => _doors.Changed(key, on);
            rows.AddChild(box);
            ticks.Add((key, box));
        }

        SettingsTicks = ticks;
        var caption = new Label { Text = "Kept for the next village you open.", AutowrapMode = TextServer.AutowrapMode.WordSmart };
        UiLook.Tint(caption, UiLook.Hint);
        caption.AddThemeFontSizeOverride("font_size", 13);
        page.AddChild(caption);

        SettingsBack = new Button
        {
            Text = "Back",
            ThemeTypeVariation = UiLook.TitleItem,
            CustomMinimumSize = new Vector2(250, 46),
            SizeFlagsHorizontal = SizeFlags.ShrinkCenter,
        };
        SettingsBack.Pressed += ShowTheMenu;
        page.AddChild(SettingsBack);
        return page;
    }

    /// <summary>The UI size, the village's dial: − 75 % +, a step of five.</summary>
    private HBoxContainer TheUiSizeRow()
    {
        var row = new HBoxContainer();
        row.AddThemeConstantOverride("separation", 6);
        var name = new Label { Text = "UI size", SizeFlagsHorizontal = SizeFlags.ExpandFill };
        name.AddThemeFontSizeOverride("font_size", 16);
        row.AddChild(name);
        var smaller = new Button { Text = "−" };
        var bigger = new Button { Text = "+" };
        _scaleReading = new Label { Text = $"{_doors.UiScale()}%", HorizontalAlignment = HorizontalAlignment.Center, CustomMinimumSize = new Vector2(52, 0) };
        _scaleReading.AddThemeFontSizeOverride("font_size", 16);
        smaller.Pressed += () => _scaleReading.Text = $"{_doors.MoveUiScale(-PlayerSettings.UiScaleStep)}%";
        bigger.Pressed += () => _scaleReading.Text = $"{_doors.MoveUiScale(PlayerSettings.UiScaleStep)}%";
        row.AddChild(smaller);
        row.AddChild(_scaleReading);
        row.AddChild(bigger);
        return row;
    }

    /// <summary>↑ and ↓ walk the shown items and wrap — Godot's own neighbours stop at the ends.</summary>
    private void LinkTheItems()
    {
        for (int i = 0; i < ShownItems.Count; i++)
        {
            Button item = ShownItems[i];
            item.FocusNeighborBottom = item.GetPathTo(ShownItems[(i + 1) % ShownItems.Count]);
            item.FocusNeighborTop = item.GetPathTo(ShownItems[(i + ShownItems.Count - 1) % ShownItems.Count]);
        }
    }

    /// <summary>
    /// ⭐ Kith's seal above the name (A223, `logo.md §7`), laid over the sheet as a child of the name and outside the column's
    /// layout, so the menu stands where it stood (Joe's mock A). Shown only while the paper above the name holds it.
    /// </summary>
    private void PressTheSeal()
    {
        _seal ??= ResourceLoader.Exists(SealPath) ? GD.Load<Texture2D>(SealPath) : null;
        Seal = new TextureRect
        {
            Texture = _seal,
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
            TextureFilter = TextureFilterEnum.LinearWithMipmaps,
            MouseFilter = MouseFilterEnum.Ignore,
            AnchorLeft = 0.5f,
            AnchorRight = 0.5f,
            AnchorTop = 0f,
            AnchorBottom = 0f,
            OffsetLeft = -SealBox / 2f,
            OffsetRight = SealBox / 2f,
            OffsetTop = SealFoot - SealBox,
            OffsetBottom = SealFoot,
        };
        if (_seal is null)
        {
            GD.PushError($"[Kith] the title's seal is missing at {SealPath}");
        }

        TitleLabel.AddChild(Seal);
        TitleLabel.ItemRectChanged += FitTheSeal;
    }

    /// <summary>On the name's move (a resize), never per frame: the seal shows only where the paper above the name holds it.</summary>
    private void FitTheSeal() => Seal.Visible = _seal is not null && SealFits(TitleLabel.Position.Y);

    /// <summary>Pure: does the seal's wax, with its clear paper, fit above a name whose top is this far down the column?</summary>
    internal static bool SealFits(float nameTop) => nameTop + SealFoot - SealBox + SealWaxTop >= SealGap;

    /// <summary>The panel's width: the share of the window, never narrower than what it holds. On every resize, never per frame.</summary>
    private void PlaceThePanel()
    {
        float width = Mathf.Ceil(Mathf.Max(Size.X * PanelShare, Band.GetCombinedMinimumSize().X));
        Band.OffsetLeft = 0f;
        Band.OffsetRight = width;
        Band.OffsetTop = 0f;
        Band.OffsetBottom = 0f;
    }

    /// <summary>
    /// The newest village's picture behind the title, or the four seasons; painted either way. A picture that will not decode, or
    /// paint that will not load, is said loudly and the title opens on what it has (`title-snapshot.md §2`).
    /// </summary>
    private void ShowTheVillage()
    {
        if (PictureOf(Saves) is string path && Decode(path) is { } village)
        {
            VillagePicture = path;
            _backdrop.Texture = village;
        }
        else
        {
            _seasons ??= ResourceLoader.Exists(BackdropPath) ? GD.Load<Texture2D>(BackdropPath) : null;
            if (_seasons is null)
            {
                GD.PushError($"[Kith] the title's picture is missing at {BackdropPath}: run tools/title-capture to write it");
                return;
            }

            _backdrop.Texture = _seasons;
        }

        PaintIt(_backdrop.Texture);
    }

    /// <summary>
    /// The picture of the newest village that has one: the folder of the first save that opens and holds a
    /// <see cref="TitleSnapshot.FileName"/> — a founding writes none (call 2), so the title keeps the last village with a home.
    /// No save is opened, and each folder is asked for once.
    /// </summary>
    internal static string? PictureOf(IReadOnlyList<SaveListing> saves)
    {
        var asked = new HashSet<string>(StringComparer.Ordinal);
        foreach (SaveListing save in saves)
        {
            string? folder = save.Refusal is null ? System.IO.Path.GetDirectoryName(save.Path) : null;
            if (folder is null || !asked.Add(folder))
            {
                continue;
            }

            string picture = System.IO.Path.Combine(folder, TitleSnapshot.FileName);
            if (System.IO.File.Exists(picture))
            {
                return picture;
            }
        }

        return null;
    }

    /// <summary>A picture off the disk, with the mips the washes read; null and a loud line when it will not be read.</summary>
    private static ImageTexture? Decode(string path)
    {
        string why;
        try
        {
            var image = new Image();
            Error read = image.LoadJpgFromBuffer(System.IO.File.ReadAllBytes(path));
            if (read == Error.Ok && !image.IsEmpty())
            {
                image.GenerateMipmaps();
                return ImageTexture.CreateFromImage(image);
            }

            why = read.ToString();
        }
        catch (Exception ex) when (ex is System.IO.IOException or UnauthorizedAccessException)
        {
            why = ex.Message;
        }

        GD.PushError($"[Kith] the village's picture for the title could not be read ({why}): {path} — the four seasons stand in");
        return null;
    }

    /// <summary>
    /// The watercolour over <paramref name="picture"/> (A233, `title-snapshot.md §5b`): the washes drawn once into a viewport of
    /// the picture's size, then the paint as the backdrop's material.
    /// </summary>
    private void PaintIt(Texture2D picture)
    {
        _marks ??= Load<Texture2D>(MarksPath);
        _finish ??= Load<Texture2D>(FinishPath);
        _paper ??= Load<Texture2D>(PaperPath);
        _washShader ??= Load<Shader>(WashShaderPath);
        _paintShader ??= Load<Shader>(PaintShaderPath);
        if (_marks is null || _finish is null || _paper is null || _washShader is null || _paintShader is null)
        {
            return; // each is already said; the picture stands unpainted
        }

        var wash = new ShaderMaterial { Shader = _washShader };
        wash.SetShaderParameter("picture", picture);
        wash.SetShaderParameter("finish", _finish);
        Vector2 size = picture.GetSize();
        Washes = new SubViewport
        {
            Size = new Vector2I((int)size.X, (int)size.Y),
            Disable3D = true,
            RenderTargetUpdateMode = SubViewport.UpdateMode.Once,
        };
        Washes.AddChild(new TextureRect { Texture = picture, Size = size, Material = wash, MouseFilter = MouseFilterEnum.Ignore });
        AddChild(Washes);

        var paint = new ShaderMaterial { Shader = _paintShader };
        paint.SetShaderParameter("picture", picture);
        paint.SetShaderParameter("washes", Washes.GetTexture());
        paint.SetShaderParameter("marks", _marks);
        paint.SetShaderParameter("finish", _finish);
        paint.SetShaderParameter("paper", _paper);
        paint.SetShaderParameter("strength", 1f);
        _backdrop.Material = paint;
    }

    private static T? Load<T>(string path)
        where T : Resource
    {
        T? loaded = ResourceLoader.Exists(path) ? GD.Load<T>(path) : null;
        if (loaded is null)
        {
            GD.PushError($"[Kith] the title's paint is missing {path}: the picture stands unpainted");
        }

        return loaded;
    }

    /// <summary>
    /// Pure: where the village at <paramref name="at"/> of its picture lands in a window the picture covers, centred
    /// (<c>KeepAspectCovered</c>'s own rule: scaled to the larger of the two ratios, the overflow cut evenly from both sides).
    /// </summary>
    internal static Vector2 WhereTheVillageLands(Vector2 at, Vector2 picture, Vector2 window)
    {
        float scale = Mathf.Max(window.X / picture.X, window.Y / picture.Y);
        Vector2 drawn = picture * scale;
        return ((window - drawn) / 2f) + (at * drawn);
    }
}

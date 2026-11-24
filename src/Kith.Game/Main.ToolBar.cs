using Kith.Sim.World;
using Godot;

namespace Kith.Game;

/// <remarks>
/// ⭐ THE TOOL BAR AND ITS FLYOUTS (<c>hud.md §8</c>, D879). One band at the bottom centre, one row: the categories with
/// anything earned (each an icon over its word — its first building's painted icon, call 9), then Paint land, Move,
/// Remove ▴ and Harvest ▴. A click opens one flyout above its button — that category's buildings, or Remove's and
/// Harvest's tools — and it closes on a pick, the same button, Esc, right-click or a click elsewhere; one at a time, never
/// on hover (§1.2). The flyout is its own floater, so the bar is one height in every state. The tabs, the <i>Show:</i>
/// row, Cancel and the hotkey line went: Esc and right-click put a tool down, and each key is in its button's tooltip or
/// Settings' <i>Keys</i>. While a tool is in hand its options stand beside the bar: the brush's shape for a brush, and an
/// Esc button for every tool, so putting a tool down stays something the player can see.
/// </remarks>
public partial class Main
{
    /// <summary>
    /// ⭐ THE BAR'S PINNED HEIGHT, logical px — measured at D879, one row in every state (a flyout is not the bar), and
    /// asserted by <c>bar height:</c>. <c>CLAUDE.md</c>'s line carries it. Was 183 (three rows and a hint line) to D878.
    /// </summary>
    private const float ToolBarHeight = 104f;

    private PanelContainer _toolBarPanel = null!;
    private HBoxContainer _toolBarRow = null!;
    private PanelContainer _flyout = null!;
    private PanelContainer _toolOptions = null!;
    private Button _escButton = null!;

    /// <summary>Whether a flyout is open — which one is <c>_tab</c> and <c>_filter</c> (a category's on Build).</summary>
    private bool _flyoutOpen;

    /// <summary>The category buttons on the bar, in today's order.</summary>
    private readonly List<(BuildCategory Category, Button Button)> _categoryButtons = new();

    /// <summary>Remove ▴ and Harvest ▴.</summary>
    private Button _removeButton = null!;

    private Button _harvestButton = null!;

    /// <summary>Paint land and Move: tools on the bar itself, never in a flyout (§8.1).</summary>
    private readonly HashSet<Button> _barTools = new();

    /// <summary>The keys that have no button, for Settings' <i>Keys</i> (§8.4) — the old hotkey line's, one a line.</summary>
    private static readonly string[] KeysWithNoButton =
    {
        "W A S D — pan the map", "wheel — zoom", "R — turn what you are placing · shift+R squares it", "Tab — routes",
        "Home — centre on the village", "C — fold every window", "H — hide every window", "B — the brush's shape",
        "Esc — close, put down, pause", "F5 — quicksave · F7 — load it",
    };

    private VBoxContainer? _keysList;

    private static readonly (BuildCategory Category, string Label)[] Categories =
    {
        (BuildCategory.Works, "Works"), (BuildCategory.Food, "Food"), (BuildCategory.Resources, "Resources"),
        (BuildCategory.Storage, "Storage & trade"), (BuildCategory.Knowledge, "Knowledge"), (BuildCategory.Civic, "Civic"),
        (BuildCategory.Homes, "Homes"), (BuildCategory.Other, "Other"),
    };

    /// <summary>The bar, its flyout floater and the tool options — built once.</summary>
    private void BuildControlPanel()
    {
        VBoxContainer body = Floating(Edge, Edge, 0f, 0f, Corner.BottomLeft);
        _toolBarPanel = _panels[^1];
        _toolBarPanel.AddThemeStyleboxOverride("panel", UiLook.Strip(0.72f));
        _controlBar = body;
        _toolBarRow = new HBoxContainer();
        _toolBarRow.AddThemeConstantOverride("separation", 4);
        body.AddChild(_toolBarRow);

        // The flyout: one floater, the strip's buttons in a flow that wraps before it leaves the window (D242's flow).
        VBoxContainer flyBody = Floating(Edge, Edge, 0f, 0f, Corner.TopLeft);
        _flyout = _panels[^1];
        _flyout.AddThemeStyleboxOverride("panel", UiLook.Strip(0.9f));
        _flyout.Visible = false;
        _stripRow = FlowRow();
        flyBody.AddChild(_stripRow);

        BuildingsCatalog catalogue = _loop.World.BuildingsCatalog;
        foreach ((BuildCategory category, string label) in Categories)
        {
            BuildingKind? first = FirstBuildingIn(category);
            Control mark = first is BuildingKind kind
                ? UiIcons.Or("buildings", catalogue.NameOf(kind), () => new BuildingGlyph(kind, true))
                : new BuildingGlyph(BuildingKind.Pile, known: false);
            BuildCategory captured = category;
            Button button = StripButton(label, mark, () => ToggleTheFlyout(BuildTab.Build, captured));
            button.TooltipText = $"{label}: the buildings the village knows";
            _categoryButtons.Add((category, button));
            _toolBarRow.AddChild(button);
        }

        _toolBarRow.AddChild(new VSeparator());

        // The strip's buttons, then Paint land and Move lifted onto the bar (tools, not buildings, §8.1).
        BuildTheStrip();
        foreach ((BuildTab _, BuildCategory _, Button button, BuildingKind? _, ToolMark? mark) in _strip)
        {
            if (mark is ToolMark.PaintLand or ToolMark.Move)
            {
                _stripRow.RemoveChild(button);
                _toolBarRow.AddChild(button);
                _barTools.Add(button);
                button.TooltipText = mark == ToolMark.PaintLand
                    ? "Paint land: where the village may build its homes — right-click or Esc to put it down"
                    : "Move a building — right-click or Esc to put it down";
            }
            else
            {
                // ⭐ A pick closes the flyout (§8.2), and a building picked is seen — its gold goes (D880).
                button.Pressed += CloseTheFlyout;
                foreach ((BuildTab _, BuildCategory _, Button b, BuildingKind? kind, ToolMark? _) in _strip)
                {
                    if (ReferenceEquals(b, button) && kind is BuildingKind picked)
                    {
                        button.Pressed += () => Seen(SeenKey(picked));
                    }
                }
            }
        }

        foreach ((BuildingKind _, Button homes) in _homesAs)
        {
            homes.Pressed += CloseTheFlyout;
        }

        _removeButton = StripButton("Remove ▴", UiIcons.Or("tools", nameof(ToolMark.Demolish), () => new ToolGlyph(ToolMark.Demolish)),
            () => ToggleTheFlyout(BuildTab.Removal, null));
        _removeButton.TooltipText = "Remove: demolish, take back, empty, destroy — taking a building back is a builder's job, and costs half what raising it did";
        _harvestButton = StripButton("Harvest ▴", UiIcons.Or("tools", nameof(ToolMark.HarvestAll), () => new ToolGlyph(ToolMark.HarvestAll)),
            () => ToggleTheFlyout(BuildTab.Harvest, null));
        _harvestButton.TooltipText = "Harvest: mark trees, stone and iron to take — painted ground is felled or dug by whoever is spare";
        _toolBarRow.AddChild(_removeButton);
        _toolBarRow.AddChild(_harvestButton);

        // ⭐ THE TOOL'S OPTIONS, BESIDE THE BAR WHILE A TOOL IS IN HAND (§8.4): the brush's shape (key B) for a brush, and Esc.
        VBoxContainer optBody = Floating(Edge, Edge, 0f, 0f, Corner.BottomLeft);
        _toolOptions = _panels[^1];
        _toolOptions.AddThemeStyleboxOverride("panel", UiLook.Strip(0.72f));
        var options = new HBoxContainer();
        options.AddThemeConstantOverride("separation", 4);
        optBody.AddChild(options);
        _shapeButton = new Button { Text = string.Empty, Flat = true, TooltipText = "The brush's shape — B" };
        _shapeButton.Pressed += () => _map.CycleBrushShape();
        options.AddChild(_shapeButton);
        _escButton = new Button { Text = "Esc", Flat = true, TooltipText = "Put the tool down — Esc, or right-click on the map" };
        _escButton.Pressed += () =>
        {
            CloseTheFlyout();
            _map.PutTheToolDown();
        };
        options.AddChild(_escButton);
        _toolOptions.Visible = false;
        _map.BrushChanged += RelabelTheBrush;
        RelabelTheBrush();

        UiLook.HeadersIn(_toolBarRow);
        RefreshTheStrip();

        // ⛔⛔ NO BUTTON MAY KEEP KEYBOARD FOCUS (D706): a focused button fires on Space's release.
        RefuseKeyboardFocus(body);
        RefuseKeyboardFocus(flyBody);
        RefuseKeyboardFocus(optBody);

        // ⭐ THE BAR RELIGHTS ITSELF FROM THE MAP, NOT FROM THE BUTTON THAT WAS PRESSED — one source (unchanged).
        _map.ToolChanged += RelightTheStrip;

        // ⛔ The refusal goes to the village log (D396); the announce is the lit button's tooltip (D397).
        _map.PlacementMessageChanged += SayInTheLog;
        _map.ToolAnnounced += _ => RelightTheStrip();
    }

    /// <summary>The first building of a category, in the catalogue's order — its icon is the category's (call 9).</summary>
    private BuildingKind? FirstBuildingIn(BuildCategory category)
    {
        BuildingsCatalog catalogue = _loop.World.BuildingsCatalog;
        for (int id = 0; id < catalogue.Count; id++)
        {
            var kind = (BuildingKind)id;
            if (catalogue.Has(id) && catalogue[kind]?.HouseCapacity is not > 0 && System.Enum.IsDefined(kind)
                && CategoryOf(kind, known: true) == category)
            {
                return kind;
            }
        }

        return null;
    }

    /// <summary>Open this flyout, or close it if it is the one open — one at a time (§8.2).</summary>
    private void ToggleTheFlyout(BuildTab tab, BuildCategory? category)
    {
        bool same = _flyoutOpen && _tab == tab && (tab != BuildTab.Build || _filter == category);
        _flyoutOpen = !same;
        _tab = tab;
        _filter = tab == BuildTab.Build ? category : null;
        if (_flyoutOpen && category is BuildCategory opened)
        {
            _seen.Add(SeenKey(opened)); // ⭐ a category's gold goes when its flyout opens (D880); the refresh below repaints
        }
        RefreshTheStrip();
    }

    private void CloseTheFlyout()
    {
        if (!_flyoutOpen)
        {
            return;
        }

        _flyoutOpen = false;
        RefreshTheStrip();
    }

    /// <summary>The bar's button whose flyout this is.</summary>
    private Button? TheFlyoutsButton() => _tab switch
    {
        BuildTab.Removal => _removeButton,
        BuildTab.Harvest => _harvestButton,
        _ => _categoryButtons.Find(c => c.Category == _filter).Button,
    };

    /// <summary>A click outside the flyout and the bar, or a right-click, closes it (§8.2). Never consumed.</summary>
    public override void _Input(InputEvent @event)
    {
        if (!_flyoutOpen || @event is not InputEventMouseButton { Pressed: true } click)
        {
            return;
        }

        bool inside = _flyout.GetGlobalRect().HasPoint(click.Position) || _toolBarPanel.GetGlobalRect().HasPoint(click.Position);
        if (click.ButtonIndex == MouseButton.Right || !inside)
        {
            CloseTheFlyout();
        }
    }

    /// <summary>Whether the tool in hand is a brush — the one kind with a shape.</summary>
    private bool ABrushIsInHand() => _map.Tool is VillageMap.MapTool.PaintingHomes or VillageMap.MapTool.ErasingHomes
        or VillageMap.MapTool.Harvesting or VillageMap.MapTool.Unmarking or VillageMap.MapTool.Destroying
        or VillageMap.MapTool.Undestroying or VillageMap.MapTool.PaintingGround or VillageMap.MapTool.ErasingGround;

    /// <summary>
    /// Every frame with the floaters: the bar centred at the bottom; the open flyout above its button, touching the bar,
    /// as wide as one line or the window; the tool options to the bar's right. Positions only — nothing is rebuilt.
    /// </summary>
    private void PlaceTheToolBar()
    {
        if (_toolBarPanel is null || Size.X <= 0f)
        {
            return;
        }

        Rect2 bar = ToolBarRect(Size.X, Size.Y);
        _toolBarPanel.OffsetLeft = bar.Position.X;
        _toolBarPanel.OffsetRight = bar.Position.X;
        _toolBarPanel.OffsetTop = -Edge;
        _toolBarPanel.OffsetBottom = -Edge;

        bool shown = _furnitureShown;
        _flyout.Visible = shown && _flyoutOpen;
        if (_flyout.Visible)
        {
            float width = Mathf.Min(FlyoutOnOneLine(), (Size.X - (Edge * 2f)) / _uiScale);
            StyleBox skin = _flyout.GetThemeStylebox("panel");
            float inside = width - skin.GetMargin(Side.Left) - skin.GetMargin(Side.Right);
            if (!Mathf.IsEqualApprox(_stripRow.CustomMinimumSize.X, inside))
            {
                _stripRow.CustomMinimumSize = new Vector2(inside, 0f);
            }

            Rect2 fly = FlyoutRect(Size.X, Size.Y, TheFlyoutsButton());
            _flyout.OffsetLeft = fly.Position.X;
            _flyout.OffsetRight = fly.Position.X;
            _flyout.OffsetTop = fly.Position.Y;
            _flyout.OffsetBottom = fly.Position.Y;
        }

        _toolOptions.Visible = shown && _map.IsPlacing;
        if (_shapeButton is not null)
        {
            _shapeButton.Visible = ABrushIsInHand();
        }

        Vector2 opt = _toolOptions.Size * _uiScale;
        _toolOptions.OffsetLeft = bar.End.X + Edge;
        _toolOptions.OffsetRight = bar.End.X + Edge;
        _toolOptions.OffsetTop = -Edge;
        _toolOptions.OffsetBottom = -Edge;
        _ = opt;
    }

    /// <summary>The bar as drawn on a window this size: centred, its foot an edge above the bottom.</summary>
    private Rect2 ToolBarRect(float width, float height)
    {
        Vector2 drawn = _toolBarPanel.GetCombinedMinimumSize() * _uiScale;
        return new Rect2((width - drawn.X) / 2f, height - Edge - drawn.Y, drawn);
    }

    /// <summary>The flyout's width on one line, logical px — its visible buttons and the skin's margins.</summary>
    private float FlyoutOnOneLine()
    {
        float sum = 0f;
        int shown = 0;
        foreach (Node child in _stripRow.GetChildren())
        {
            if (child is Control { Visible: true } item)
            {
                sum += item.GetCombinedMinimumSize().X;
                shown++;
            }
        }

        StyleBox skin = _flyout.GetThemeStylebox("panel");
        return sum + (_stripRow.GetThemeConstant("h_separation") * Mathf.Max(0, shown - 1)) + skin.GetMargin(Side.Left) + skin.GetMargin(Side.Right) + 1f;
    }

    /// <summary>The open flyout as drawn: over its button's centre, kept inside the window, its foot on the bar's top.</summary>
    private Rect2 FlyoutRect(float width, float height, Button? over)
    {
        Rect2 bar = ToolBarRect(width, height);
        Vector2 drawn = _flyout.GetCombinedMinimumSize() * _uiScale;
        float centre = over is null ? width / 2f : bar.Position.X + ((over.GetGlobalRect().GetCenter().X - _toolBarPanel.GetGlobalRect().Position.X));
        // ⭐ Kept clear of the windows' column at the bottom-left (hud overlap:, D879): a flyout over Works clamped to the
        // window's edge ran up into the column at 1,280 wide.
        float left = _windowsColumn is { Visible: true } column ? Edge + (column.GetCombinedMinimumSize().X * _uiScale) + Edge : Edge;
        float x = Mathf.Clamp(centre - (drawn.X / 2f), left, Mathf.Max(left, width - Edge - drawn.X));
        return new Rect2(x, bar.Position.Y - drawn.Y, drawn);
    }

    /// <summary>The tool options as drawn: to the bar's right, on the same foot.</summary>
    private Rect2 ToolOptionsRect(float width, float height)
    {
        Rect2 bar = ToolBarRect(width, height);
        Vector2 drawn = _toolOptions.GetCombinedMinimumSize() * _uiScale;
        return new Rect2(bar.End.X + Edge, height - Edge - drawn.Y, drawn);
    }

    // ---------------------------------------------------------------
    //  The probe
    // ---------------------------------------------------------------

    /// <summary>
    /// ⭐ <c>categories:</c>, <c>flyouts:</c> and <c>bar height:</c> (<c>hud.md §8</c>, step 6). At the founding Knowledge,
    /// Civic and Other are hidden and Works and Food shown (A210, the old grey-out made a hide). Then, with every category
    /// posed earned: every category's flyout and both tools' open above their buttons, inside the window, their foot on the
    /// bar's top, one at a time; and the bar stands <see cref="ToolBarHeight"/> tall in every state — closed, each flyout
    /// open, a tool in hand — the number, not only the spread (the old line asserted 183 nowhere).
    /// </summary>
    private void ProbeTheControlBar()
    {
        var hidden = _categoryButtons.Where(c => !c.Button.Visible).Select(c => c.Category.ToString()).ToList();
        var shown = _categoryButtons.Where(c => c.Button.Visible).Select(c => c.Category.ToString()).ToList();
        bool rightAtTheFounding = hidden.Contains("Knowledge") && hidden.Contains("Civic") && hidden.Contains("Other")
            && shown.Contains("Works") && shown.Contains("Food");
        GD.Print(rightAtTheFounding
            ? $"[widths] categories: ✅ hidden at the founding: {string.Join(", ", hidden)}; shown: {string.Join(", ", shown)}"
            : $"[widths] categories: ⛔ hidden at the founding: {string.Join(", ", hidden)}; shown: {string.Join(", ", shown)} — Knowledge, Civic and Other should be hidden and Works and Food shown");

        using PosedEarned posed = PoseEveryCategoryEarned();
        var faults = new List<string>();
        var heights = new List<string>();
        void Height(string where)
        {
            float tall = _toolBarPanel.GetCombinedMinimumSize().Y;
            heights.Add($"{where} {tall:F0}");
            if (!Mathf.IsEqualApprox(tall, ToolBarHeight))
            {
                faults.Add($"{where}: the bar stands {tall:F0}, not {ToolBarHeight:F0}");
            }
        }

        Height("closed");
        var opened = new List<string>();
        var targets = _categoryButtons.Select(c => (BuildTab.Build, (BuildCategory?)c.Category, c.Button)).ToList();
        targets.Add((BuildTab.Removal, null, _removeButton));
        targets.Add((BuildTab.Harvest, null, _harvestButton));
        foreach ((BuildTab tab, BuildCategory? category, Button button) in targets)
        {
            ToggleTheFlyout(tab, category);
            PlaceTheToolBar();
            Rect2 fly = FlyoutRect(Size.X, Size.Y, button);
            Rect2 bar = ToolBarRect(Size.X, Size.Y);
            string name = category?.ToString() ?? tab.ToString();
            int items = _stripRow.GetChildren().OfType<Control>().Count(c => c.Visible);
            // Other is posed shown with nothing in it (no modded building here): its count is not asked.
            if (!_flyoutOpen || !button.ButtonPressed || (items == 0 && category != BuildCategory.Other))
            {
                faults.Add($"{name}: opened {_flyoutOpen}, its button held {button.ButtonPressed}, {items} items");
            }

            if (fly.Position.X < Edge - 0.5f || fly.End.X > Size.X - Edge + 0.5f || fly.Position.Y < 0f
                || !Mathf.IsEqualApprox(fly.End.Y, bar.Position.Y))
            {
                faults.Add($"{name}'s flyout at {fly} leaves the window or does not stand on the bar at {bar.Position.Y:F0}");
            }

            Height($"{name} open");
            opened.Add($"{name} {items}");
            ToggleTheFlyout(tab, category);
            if (_flyoutOpen)
            {
                faults.Add($"{name}: pressed again it stayed open");
            }
        }

        // A pick closes it (§8.2): the first building of Works, pressed as a click does.
        ToggleTheFlyout(BuildTab.Build, BuildCategory.Works);
        Button? pick = _strip.Find(e => e.Category == BuildCategory.Works && e.Kind is not null && e.Button.Visible).Button;
        pick?.EmitSignal(BaseButton.SignalName.Pressed);
        bool pickCloses = pick is not null && !_flyoutOpen;
        _map.PutTheToolDown();

        // A click on the map closes it, and so does a right-click anywhere (§8.2) — not consumed, so the map still has it.
        ToggleTheFlyout(BuildTab.Build, BuildCategory.Food);
        _Input(new InputEventMouseButton { ButtonIndex = MouseButton.Left, Pressed = true, Position = new Vector2(Size.X / 2f, Size.Y / 3f) });
        bool mapCloses = !_flyoutOpen;
        ToggleTheFlyout(BuildTab.Build, BuildCategory.Food);
        Vector2 onTheFlyout = _toolBarPanel.GetGlobalRect().GetCenter();
        _Input(new InputEventMouseButton { ButtonIndex = MouseButton.Left, Pressed = true, Position = onTheFlyout });
        bool barKeeps = _flyoutOpen;
        _Input(new InputEventMouseButton { ButtonIndex = MouseButton.Right, Pressed = true, Position = onTheFlyout });
        bool rightCloses = !_flyoutOpen;
        if (!pickCloses || !mapCloses || !barKeeps || !rightCloses)
        {
            faults.Add($"a pick closed it {pickCloses}; a map click {mapCloses}; a click on the bar kept it {barKeeps}; a right-click closed it {rightCloses}");
        }

        // Settings holds the keys with no button, one a line (§8.4).
        if (_keysList is null || _keysList.GetChildCount() != KeysWithNoButton.Length || !_settingsPanel.IsAncestorOf(_keysList))
        {
            faults.Add("Settings has no Keys list of the keys with no button");
        }

        // Esc closes an open flyout before anything else (§8.2).
        ToggleTheFlyout(BuildTab.Build, BuildCategory.Works);
        _UnhandledKeyInput(new InputEventKey { Keycode = Key.Escape, Pressed = true });
        if (_flyoutOpen || ThePauseIsUp)
        {
            faults.Add($"Esc left the flyout open {_flyoutOpen} or opened the pause {ThePauseIsUp}");
            if (ThePauseIsUp)
            {
                CloseThePause(resume: false);
            }
        }

        // A tool in hand: the bar's height holds, its options stand beside it with Esc, the shape only for a brush.
        _map.BeginHarvesting(HarvestBrush.Trees, 1);
        PlaceTheToolBar();
        Height("a brush in hand");
        bool brushOptions = _toolOptions.Visible && _shapeButton is { Visible: true } && _escButton.Visible;
        _map.PutTheToolDown();
        _map.BeginMoving();
        PlaceTheToolBar();
        bool moveOptions = _toolOptions.Visible && _shapeButton is { Visible: false };
        _map.PutTheToolDown();
        PlaceTheToolBar();
        if (!brushOptions || !moveOptions || _toolOptions.Visible)
        {
            faults.Add($"the tool options: with a brush {brushOptions}, with Move (no shape) {moveOptions}, put down hidden {!_toolOptions.Visible}");
        }

        foreach (string gone in new[] { "BUILD", "REMOVAL", "HARVEST", "Cancel", "Show:" })
        {
            if (_toolBarPanel.FindChildren("*", owned: false).Any(n => n is Button b && b.Text == gone || n is Label l && l.Text == gone))
            {
                faults.Add($"the bar still holds {gone}");
            }
        }

        GD.Print($"[widths] bar height, every state: {string.Join(", ", heights)}");
        GD.Print(faults.Count == 0
            ? $"[widths] flyouts: ✅ {opened.Count} flyouts ({string.Join(", ", opened)}) open one at a time above their buttons, inside the window, on the bar; pressed again, a pick, a map click, a right-click or Esc, closed (a click on the bar keeps it); Settings lists the keys; a brush shows its shape and Esc beside the bar, Move only Esc; no tab, Cancel or Show: left"
            : $"[widths] flyouts: ⛔ {string.Join("; ", faults)}");
        GD.Print(heights.All(h => h.EndsWith($" {ToolBarHeight:F0}", StringComparison.Ordinal))
            ? $"[widths] bar height: ✅ {ToolBarHeight:F0} everywhere"
            : $"[widths] bar height: ⛔ {string.Join(", ", heights)} — want {ToolBarHeight:F0}");
    }

    /// <summary>Every category earned for a while — the latched flags the bar reads, and Other's button shown — then put back.</summary>
    private PosedEarned PoseEveryCategoryEarned() => new(this);

    private sealed class PosedEarned : IDisposable
    {
        private readonly Main _main;
        private readonly bool[] _was;
        private readonly bool _otherWas;
        private readonly string[] _seenWas;

        public PosedEarned(Main main)
        {
            _main = main;

            // ⚠️ The seen set too (D880): opening every flyout here marks every category seen, and the `new:` line after
            // read Knowledge as already looked at — the pose has to put back everything it touches (D876's trap).
            _seenWas = main._seen.ToArray();
            _was = new[] { main._literacy, main._foundersGone, main._quarryKnown, main._mineKnown, main._smithyKnown, main._millKnown,
                main._bakeryKnown, main._tailorKnown, main._workshopKnown, main._schoolKnown, main._breweryKnown, main._tavernKnown,
                main._marketStallKnown, main._bridgeKnown, main._stoneBridgeKnown, main._hallTradeKnown };
            Set(main, Enumerable.Repeat(true, _was.Length).ToArray());
            main.RefreshTheStrip();
            Button other = main._categoryButtons.Find(c => c.Category == BuildCategory.Other).Button;
            _otherWas = other.Visible;
            other.Visible = true;
        }

        private static void Set(Main m, bool[] v) =>
            (m._literacy, m._foundersGone, m._quarryKnown, m._mineKnown, m._smithyKnown, m._millKnown, m._bakeryKnown, m._tailorKnown,
                m._workshopKnown, m._schoolKnown, m._breweryKnown, m._tavernKnown, m._marketStallKnown, m._bridgeKnown,
                m._stoneBridgeKnown, m._hallTradeKnown) =
            (v[0], v[1], v[2], v[3], v[4], v[5], v[6], v[7], v[8], v[9], v[10], v[11], v[12], v[13], v[14], v[15]);

        public void Dispose()
        {
            Set(_main, _was);
            _main._seen.Clear();
            _main._seen.UnionWith(_seenWas);
            _main._flyoutOpen = false;
            _main.RefreshTheStrip();
            _main._categoryButtons.Find(c => c.Category == BuildCategory.Other).Button.Visible = _otherWas;
        }
    }

    /// <summary>
    /// ⭐ <c>hud overlap:</c> (<c>hud.md §9.3, §14</c> step 6; A215) — no two floaters meet: the strip, the dev bar, the
    /// valley, the log, Professions, the windows' column, the tool bar, each open flyout and the tool options; a flyout
    /// touching its own bar is the one meeting allowed. Posed with every category earned, at 1,280 × 800 and 1,422 × 800 —
    /// each floater placed by its own rule for that window (anchored right, centred, under the strip …), from its drawn size.
    /// </summary>
    private string NoTwoFloatersMeet()
    {
        using PosedEarned posed = PoseEveryCategoryEarned();
        bool optionsWere = _toolOptions.Visible;
        var faults = new List<string>();
        var widths = new List<string>();
        foreach ((float w, float h) in new[] { (1280f, 800f), (1422f, 800f) })
        {
            var rects = TheFloatersAt(w, h);
            Rect2 bar = rects.Find(r => r.Name == "the tool bar").Rect;
            var targets = _categoryButtons.Where(c => c.Button.Visible).Select(c => (BuildTab.Build, (BuildCategory?)c.Category, c.Button)).ToList();
            targets.Add((BuildTab.Removal, null, _removeButton));
            targets.Add((BuildTab.Harvest, null, _harvestButton));
            foreach ((BuildTab tab, BuildCategory? category, Button button) in targets)
            {
                ToggleTheFlyout(tab, category);
                PlaceTheToolBar();
                Rect2 fly = FlyoutRect(w, h, button);
                foreach ((string name, Rect2 rect) in rects)
                {
                    if (name != "the tool bar" && Meet(fly, rect))
                    {
                        faults.Add($"at {w:F0}: {category?.ToString() ?? tab.ToString()}'s flyout meets {name}");
                    }
                }

                ToggleTheFlyout(tab, category);
            }

            for (int i = 0; i < rects.Count; i++)
            {
                for (int j = i + 1; j < rects.Count; j++)
                {
                    if (Meet(rects[i].Rect, rects[j].Rect))
                    {
                        faults.Add($"at {w:F0}: {rects[i].Name} {rects[i].Rect} meets {rects[j].Name} {rects[j].Rect}");
                    }
                }
            }

            Rect2 column = rects.Find(r => r.Name == "the windows' column").Rect;
            widths.Add($"{w:F0}: the bar {bar.Position.X:F0}–{bar.End.X:F0}, {bar.Position.X - column.End.X:F0} px clear of the column");
        }

        _toolOptions.Visible = optionsWere;
        return faults.Count == 0
            ? $"[widths] hud overlap: ✅ every category earned, no two floaters meet at 1,280 × 800 or 1,422 × 800 ({string.Join("; ", widths)}); every flyout clear of all but its bar"
            : $"[widths] hud overlap: ⛔ {string.Join("; ", faults.Distinct())}";

        static bool Meet(Rect2 a, Rect2 b) => a.Size.X > 0f && b.Size.X > 0f && a.Grow(-0.5f).Intersects(b.Grow(-0.5f));
    }

    /// <summary>Each floater on screen at the founding (and the tool options, posed), placed by its own rule for a window this size.</summary>
    private List<(string Name, Rect2 Rect)> TheFloatersAt(float w, float h)
    {
        var rects = new List<(string, Rect2)>();
        float s = _uiScale;
        float stripLines = StripLinesAt(Mathf.Min(StripOnOneLine(), StripRoom(w)));
        float stripTall = stripLines > 1 ? StripTwoLinesTall() : _topBar.GetCombinedMinimumSize().Y * s;
        rects.Add(("the strip", new Rect2(Edge, Edge, Mathf.Min(StripOnOneLine(), StripRoom(w)) * s, stripTall)));
        if (_devBar is not null)
        {
            Vector2 dev = _devBar.GetCombinedMinimumSize() * s;
            rects.Add(("the dev bar", new Rect2(RightColumnBegins(w) - Edge - dev.X, TopOfTheLeftColumn(), dev)));
        }

        // The right column stacks from the top, the left from under the strip (`ArrangeDefaults`' own rule).
        float rightY = Edge;
        foreach ((PanelContainer panel, string name) in new[] { (_minimapPanel, "the valley"), (_villageLogPanel, "the log") })
        {
            Vector2 size = panel.GetCombinedMinimumSize() * s;
            rects.Add((name, new Rect2(w - Edge - size.X, rightY, size)));
            rightY += size.Y + Edge;
        }

        Vector2 professions = _professionsPanel.GetCombinedMinimumSize() * s;
        rects.Add(("Professions", new Rect2(Edge, TopOfTheLeftColumn(), professions)));
        Vector2 column = _windowsColumn.GetCombinedMinimumSize() * s;
        rects.Add(("the windows' column", new Rect2(Edge, h - Edge - column.Y, column)));
        rects.Add(("the tool bar", ToolBarRect(w, h)));
        _toolOptions.Visible = true;
        rects.Add(("the tool options", ToolOptionsRect(w, h)));
        return rects;
    }
}

using Godot;

namespace Kith.Game;

/// <remarks>
/// ⭐ THE WINDOWS' COLUMN (<c>hud.md §9.3</c>, D876; Joe, A215: <i>"stack the window buttons as a narrow column"</i>): one
/// word a line in the bottom-left corner — Centre, People, Professions, Stores, Build queue, Lore, Log, Valley, Settings, and
/// Records once the hall stands. Each word shows or hides its window and is held in lapis while it is shown (§10). It
/// replaced Settings' window ticks (D306) and took Centre, Settings, Records and Lore off the control bar: the column is the
/// one thing always on screen now, so a window switched off is always one click back (D103).
/// </remarks>
public partial class Main
{
    /// <summary>The column itself: an unskinned-title sheet, not a window — never foldable, never in its own list.</summary>
    private PanelContainer _windowsColumn = null!;

    /// <summary>Settings' word, held while Settings is open.</summary>
    private Button? _settingsWord;

    /// <summary>Every word on the column, in its order, for the probe.</summary>
    private readonly List<(string Word, Button Button)> _columnWords = new();

    private void BuildTheWindowsColumn()
    {
        VBoxContainer body = Floating(Edge, Edge, 0, 0, Corner.BottomLeft);
        _windowsColumn = _panels[^1];
        body.AddThemeConstantOverride("separation", 0);

        Button Word(string word, string tooltip, bool toggles)
        {
            var button = new Button
            {
                Text = word,
                Flat = true,
                ToggleMode = toggles,
                Alignment = HorizontalAlignment.Left,
                TooltipText = tooltip,
            };
            UiLook.Header(button);
            body.AddChild(button);
            _columnWords.Add((word, button));
            return button;
        }

        void WindowWord(string word, PanelContainer panel, string tooltip)
        {
            Button button = Word(word, tooltip, toggles: true);
            ShellWindow window = WindowOf(panel) ?? throw new InvalidOperationException($"{word} names no window");
            window.Toggle = button;
            button.SetPressedNoSignal(window.Wanted);
            button.Toggled += on => SetShown(window, on);
        }

        // With a valley this size and free panning, getting lost is easy and a way back is not optional.
        Word("Centre", "Centre on the village — Home", toggles: false).Pressed += () => _map.CentreOnTheVillage();
        WindowWord("People", _rosterPanel, "The village's people, by name, age and trade");
        WindowWord("Professions", _professionsPanel, "How many of each trade you ask for, and who works it");
        WindowWord("Stores", _stockLimitsPanel, "How much of each good to keep before the work stops");
        WindowWord("Build queue", _buildQueuePanel, "What is waiting to be built, in the order the builders take it");

        // ⭐ THE TREE, ONCE THE VILLAGE HAS LEARNED SOMETHING BY DOING (D440; B2, D486) — hidden until then.
        _treeButton = Word(LoreTitle, "What the village may yet learn, and what each will take", toggles: true);
        _treeButton.Visible = false;
        if (WindowOf(_treePanel) is ShellWindow tree)
        {
            tree.Toggle = _treeButton;
            _treeButton.SetPressedNoSignal(tree.Wanted);
        }

        _treeButton.Pressed += ToggleTheTree;

        WindowWord("Log", _villageLogPanel, "The village's story, as it happens");
        WindowWord("Valley", _minimapPanel, "The whole valley, small — click to look there");

        // Settings is not a window (it lists itself out of nothing to switch off), but it is held while open.
        _settingsWord = Word("Settings", "The window, the map's ticks, the UI size and the run", toggles: true);
        _settingsWord.Pressed += () =>
        {
            ToggleSettings();
            _settingsWord.SetPressedNoSignal(_settingsPanel.Visible);
        };

        // ⭐ RECORDS, ONE CLICK AWAY, ONCE THE HALL STANDS (D397) — on the column for the bar's old reason.
        _recordsButton = Word("Records", "The town hall's records", toggles: false);
        _recordsButton.Visible = false;
        _recordsButton.Pressed += OpenTheRecords;

        // D706: no word keeps keyboard focus, or Space would press it on release.
        RefuseKeyboardFocus(body);
    }

    /// <summary>A word toggled: its window shown or hidden — the state the ✕ writes too (as Settings' tick did, D380).</summary>
    private void SetShown(ShellWindow window, bool on)
    {
        window.Wanted = on;
        window.Panel.Visible = on;
        window.Toggle?.SetPressedNoSignal(on);
        SettingsChanged();
    }

    /// <summary>
    /// The column's corner: bottom left. ⚠️ Until the control bar leaves the full width (<c>hud.md §14</c> step 6) it
    /// stands on the bar, so the two never meet.
    /// </summary>
    private void PlaceTheWindowsColumn()
    {
        if (_windowsColumn is null)
        {
            return;
        }

        float barTop = _controlBar?.GetParent()?.GetParent() is PanelContainer { Visible: true } bar ? DrawnTopLeft(bar).Y : Size.Y;
        float up = Mathf.Max(Edge, Size.Y - barTop + Edge);
        _windowsColumn.OffsetTop = -up;
        _windowsColumn.OffsetBottom = -up;
    }

    /// <summary>
    /// ⚠️ The probe opens the roster for its own lines (D876): it starts shut now (A211), and a shut window is never laid
    /// out — so the roster's height (D306's trap) and its columns could not be measured. Opened at frame 12 beside What's
    /// here, shut again after the roster's lines, before <c>windows:</c> asks for the defaults.
    /// </summary>
    /// <remarks>
    /// ⛔ It puts back what it found, never "shut": the first draft shut it, and a mutant that opened the roster on a new
    /// village scored ZERO at <c>windows:</c> — the probe's own pose had hidden the default it was there to check (D876).
    /// </remarks>
    private void PoseTheRosterOpen(bool open)
    {
        if (WindowOf(_rosterPanel) is not ShellWindow roster)
        {
            return;
        }

        if (open)
        {
            _rosterWantedBeforeThePose = roster.Wanted;
        }

        bool shown = open || _rosterWantedBeforeThePose;
        roster.Wanted = shown;
        _rosterPanel.Visible = shown;
        roster.Toggle?.SetPressedNoSignal(shown);
    }

    private bool _rosterWantedBeforeThePose;

    /// <summary>
    /// ⭐ <c>windows:</c> (<c>hud.md §9.1, §9.3</c>, step 3) — a new village opens the valley, the log and Professions (shown
    /// and unfolded, B1) and keeps People, Stores and Build queue shut; every titled window but What's here has its word on
    /// the column, in the spec's order, each word's hold reading its window; a word shows its window and hides it again; Lore's
    /// word is hidden until the tree is shown; and Settings holds no window ticks.
    /// </summary>
    /// <remarks>Red on the code it replaced: the ticks were written <c>true</c> once at build (D380).</remarks>
    private string EveryWindowHasItsWordAndOpensAsItShould()
    {
        Refresh();
        var wrong = new List<string>();
        foreach ((PanelContainer panel, string name, bool open) in new[]
        {
            (_minimapPanel, "the valley", true), (_villageLogPanel, "the log", true),
            (_professionsPanel, "Professions", true), (_rosterPanel, RosterTitle, false), (_stockLimitsPanel, StoresTitle, false),
            (_buildQueuePanel, "Build queue", false),
        })
        {
            if (WindowOf(panel) is not ShellWindow window || window.Wanted != open || panel.Visible != open)
            {
                wrong.Add($"{name} should start {(open ? "shown" : "shut")} (wanted {WindowOf(panel)?.Wanted}, drawn {panel.Visible})");
            }
        }

        // B1 (Joe, 2026-10-04): the Professions window is shown AND unfolded at the founding.
        if (!_professionsBody.Visible)
        {
            wrong.Add("Professions should start unfolded");
        }

        string[] order = { "Centre", "People", "Professions", "Stores", "Build queue", LoreTitle, "Log", "Valley", "Settings", "Records" };
        string[] words = _columnWords.Select(w => w.Word).ToArray();
        if (!words.SequenceEqual(order))
        {
            wrong.Add($"the column reads {string.Join(" · ", words)}");
        }

        int worded = 0;
        foreach (ShellWindow window in _windows)
        {
            if (window.Panel == _whatsHerePanel)
            {
                continue;
            }

            if (window.Toggle is not Button word || !_columnWords.Exists(w => w.Button == word))
            {
                wrong.Add($"{window.Name} has no word on the column");
                continue;
            }

            worded++;
            if (word.ButtonPressed != window.Wanted)
            {
                wrong.Add($"{window.Name}'s word is held {word.ButtonPressed}, the window is {(window.Wanted ? "wanted" : "not wanted")}");
            }
        }

        // A word shows its window, and hides it again — toggled, as a click does.
        if (WindowOf(_rosterPanel) is { Toggle: Button people } roster)
        {
            people.ButtonPressed = !people.ButtonPressed;
            bool shown = roster.Wanted && _rosterPanel.Visible && people.ButtonPressed;
            people.ButtonPressed = !people.ButtonPressed;
            bool hidden = !roster.Wanted && !_rosterPanel.Visible && !people.ButtonPressed;
            if (!shown || !hidden)
            {
                wrong.Add($"People's word did not show and hide its window (shown {shown}, hidden again {hidden})");
            }
        }

        // A window's ✕ writes the same state, and its word lets go at the next refresh (D380's rule, kept).
        if (WindowOf(_villageLogPanel) is { Toggle: Button log } logWindow)
        {
            CloseTheWindow(_villageLogPanel);
            Refresh();
            bool letGo = !log.ButtonPressed;
            SetShown(logWindow, true);
            Refresh();
            if (!letGo || !log.ButtonPressed || !_villageLogPanel.Visible)
            {
                wrong.Add($"the log's ✕ left its word held {!letGo}, or showing it again did not hold it");
            }
        }

        if (!_loop.World.ShownTheTechTree && _treeButton is { Visible: true })
        {
            wrong.Add($"{LoreTitle} is on the column before it is shown");
        }

        var ticks = new List<string>();
        foreach (Node node in _settingsPanel.FindChildren("*", nameof(CheckBox), owned: false))
        {
            if (node is CheckBox box && _windows.Exists(w => w.Name == box.Text))
            {
                ticks.Add(box.Text);
            }
        }

        if (ticks.Count > 0)
        {
            wrong.Add($"Settings still ticks {string.Join(", ", ticks)}");
        }

        return wrong.Count == 0
            ? $"[widths] windows: ✅ the valley, the log and Professions open, {RosterTitle}, {StoresTitle} and Build queue shut; "
              + $"{worded} windows worded on the column ({string.Join(" · ", order)}), each held as its window is; People shows "
              + $"and hides its window; the log's ✕ lets its word go; {LoreTitle} is not on it until shown; Settings ticks no window"
            : "[widths] windows: ⛔ " + string.Join("; ", wrong);
    }
}

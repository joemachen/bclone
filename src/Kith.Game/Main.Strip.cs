using Kith.Sim.World;
using Godot;

namespace Kith.Game;

/// <remarks>
/// ⭐ THE STRIP AND THE DEV BAR (<c>hud.md §6–§7</c>, D877). The two top boxes became one slip whose groups — the name and
/// time, the goods, the people, the nomads switch — sit in one flow. ⭐ <b>It wraps a whole group onto a second line only
/// when one line would reach the right column</b> (Joe, 2026-11-23: <i>"Wrap to two lines"</i>, after D876 measured a busy
/// village's line at ≈ 2,060 logical px against 1,238 of room at 16:10). The left column starts below where two lines
/// would end, so a strip that wraps mid-game never covers Professions and no window has to move.
/// </remarks>
public partial class Main
{
    /// <summary>The gap between the strip's groups, logical px.</summary>
    private const int StripGap = 14;

    /// <summary>How far short of the right column the strip stops, drawn px.</summary>
    private const float StripClearance = 8f;

    private PanelContainer _stripSlip = null!;
    private HFlowContainer _topStrip = null!;
    private Label _householdsLabel = null!;
    private TextureRect _seasonIcon = null!;
    private Season? _seasonShown;

    /// <summary>What play resumes at — the speed held in lapis on the strip, paused or not (hud.md §6 item 2).</summary>
    private double _resumeSpeed = 1.0;

    /// <summary>Pause and 1×–10× on the strip, each with the multiplier it sets (0 is Pause).</summary>
    private readonly List<(Button Button, double Multiplier)> _speedButtons = new();

    /// <summary>The slip's width as last set — written only when it changes, so the layout is not asked again for nothing.</summary>
    private float _stripWidthSet = -1f;

    /// <summary>The season's icon, the name (Metamorphous), the date, then Pause and 1×–10× — the strip's first group.</summary>
    private HBoxContainer BuildTheNameAndTime()
    {
        var group = new HBoxContainer();
        group.AddThemeConstantOverride("separation", 6);

        _seasonIcon = UiIcons.Slot(UiIcons.Side);
        _seasonIcon.SizeFlagsVertical = Control.SizeFlags.ShrinkCenter;
        group.AddChild(_seasonIcon);

        Label name = Body(_loop.World.Name);
        UiLook.Header(name);
        group.AddChild(name);

        _clockLabel = Muted(string.Empty);
        group.AddChild(_clockLabel);
        group.AddChild(new VSeparator());

        // ⭐ PAUSE READS "PAUSED" AND IS HELD WHILE IT IS, SO THE PAUSED LABEL WENT; the chosen speed is held beside it — what
        // play resumes at. 20× is the dev bar's (A212). Keys 1–4 as before; the key is the tooltip's end (hud.md §8.4).
        foreach ((string text, double multiplier, string key) in new[]
        {
            ("Pause", 0.0, "space"), ("1×", 1.0, "1"), ("2×", 2.0, "2"), ("4×", 4.0, "3"), ("10×", 10.0, "4"),
        })
        {
            var button = new Button { Text = text, ToggleMode = true, Flat = true, TooltipText = $"{(multiplier == 0.0 ? "Pause" : $"{text} speed")} — {key}" };
            button.Pressed += () => SetSpeed(multiplier == 0.0 && _driver.IsPaused ? _resumeSpeed : multiplier);
            group.AddChild(button);
            _speedButtons.Add((button, multiplier));
        }

        // STOPPED only, now (D364): a village halted by a fault says so here, in the warnings' red.
        _speedLabel = Body(string.Empty);
        _speedLabel.Visible = false;
        UiLook.Tint(_speedLabel, UiLook.Stopped);
        group.AddChild(_speedLabel);
        return group;
    }

    /// <summary>Hold Pause while paused (reading <i>Paused</i>) and the speed play resumes at — the state, not the last click.</summary>
    private void HoldTheSpeed()
    {
        bool paused = _driver.IsPaused;
        foreach ((Button button, double multiplier) in _speedButtons)
        {
            button.SetPressedNoSignal(multiplier == 0.0 ? paused : multiplier == _resumeSpeed);
            if (multiplier == 0.0)
            {
                button.Text = paused ? "Paused" : "Pause";
            }
        }
    }

    /// <summary>The season's painted icon, swapped when the season turns — never asked per frame for the same season.</summary>
    private void ShowTheSeason(Season season)
    {
        if (_seasonShown == season)
        {
            return;
        }

        _seasonShown = season;
        UiIcons.Show(_seasonIcon, "time", season.ToString());
    }

    /// <summary>Where the right column begins on a window this wide, drawn px — its panels' drawn width from the edge.</summary>
    private float RightColumnBegins(float windowWidth) => windowWidth - Edge - (DefaultPanelWidth * _uiScale);

    /// <summary>The slip's widest, logical px, on a window this wide: up to the right column, short by the clearance.</summary>
    private float StripRoom(float windowWidth) => (RightColumnBegins(windowWidth) - StripClearance - Edge) / _uiScale;

    /// <summary>The slip's width if every group stood on one line, logical px.</summary>
    private float StripOnOneLine()
    {
        float sum = 0f;
        int shown = 0;
        foreach (Node child in _topStrip.GetChildren())
        {
            if (child is Control { Visible: true } group)
            {
                sum += group.GetCombinedMinimumSize().X;
                shown++;
            }
        }

        StyleBox slip = _stripSlip.GetThemeStylebox("panel");
        return sum + (StripGap * Mathf.Max(0, shown - 1)) + slip.GetMargin(Side.Left) + slip.GetMargin(Side.Right) + 1f;
    }

    /// <summary>
    /// The slip as wide as one line needs, or the room if one line would reach the right column — then the flow wraps a
    /// whole group onto the next line. Asked each frame with the other floaters (a few groups' cached minimums); written
    /// only when the answer changes.
    /// </summary>
    private void FitTheStrip()
    {
        if (_topStrip is null || Size.X <= 0f)
        {
            return;
        }

        float width = Mathf.Min(StripOnOneLine(), StripRoom(Size.X));
        if (!Mathf.IsEqualApprox(width, _stripWidthSet))
        {
            _stripWidthSet = width;
            _stripSlip.CustomMinimumSize = new Vector2(width, 0f);
        }
    }

    /// <summary>How many lines the flow takes in a slip this wide — its own greedy rule, by whole groups.</summary>
    private int StripLinesAt(float slipWidth)
    {
        StyleBox slip = _stripSlip.GetThemeStylebox("panel");
        float inside = slipWidth - slip.GetMargin(Side.Left) - slip.GetMargin(Side.Right);
        int lines = 1;
        float used = 0f;
        foreach (Node child in _topStrip.GetChildren())
        {
            if (child is not Control { Visible: true } group)
            {
                continue;
            }

            float wide = group.GetCombinedMinimumSize().X;
            if (used > 0f && used + StripGap + wide > inside)
            {
                lines++;
                used = 0f;
            }

            used += (used > 0f ? StripGap : 0f) + wide;
        }

        return lines;
    }

    /// <summary>
    /// The strip's height at two lines or as it stands, whichever is taller, drawn px — what the left column clears, so a wrap
    /// covers nothing.
    /// </summary>
    private float StripTwoLinesTall()
    {
        float standing = _topBar.GetCombinedMinimumSize().Y * _uiScale;
        float line = 0f;
        foreach (Node child in _topStrip.GetChildren())
        {
            if (child is Control group)
            {
                line = Mathf.Max(line, group.GetCombinedMinimumSize().Y);
            }
        }

        StyleBox slip = _stripSlip.GetThemeStylebox("panel");
        float two = ((line * 2f) + _topStrip.GetThemeConstant("v_separation") + slip.GetMargin(Side.Top) + slip.GetMargin(Side.Bottom)) * _uiScale;
        return Mathf.Max(two, standing);
    }

    // ---------------------------------------------------------------
    //  The dev bar (hud.md §7, A212)
    // ---------------------------------------------------------------

    private PanelContainer? _devBar;

    /// <summary>The dev bar's title — remembered by it for its place (D504); not a window, never on the column.</summary>
    private const string DevBarTitle = "Dev";

    /// <summary>
    /// ⭐ THE DEV BAR (A212, Joe: <i>"the debug options (skip 1 year, skip 10 years, 20x speed) should be in their own floating
    /// "dev bar""</i>): 20×, Skip 1y, Skip 10y and the frame readout, <b>built only in a debug build</b> — the skips' gate
    /// since D254, so a release has no 20× and no key 5. Draggable and its place saved like a window's; not on the windows'
    /// column, not in Settings, not folded by <c>c</c>. It starts at the top, just left of the right column, under the strip.
    /// </summary>
    private void BuildTheDevBar()
    {
        if (!OS.IsDebugBuild())
        {
            return;
        }

        VBoxContainer body = Floating(Edge, Edge, 0f, 0f, Corner.TopRight, DevBarTitle, startOpen: true);
        _devBar = _panels[^1];

        // Not a window (Settings' own pattern): off the windows' list and the headers `c` folds.
        _windows.RemoveAt(_windows.Count - 1);
        _headers.RemoveAt(_headers.Count - 1);

        var row = new HBoxContainer();
        row.AddThemeConstantOverride("separation", 6);
        body.AddChild(row);

        var twenty = new Button { Text = "20×", ToggleMode = true, Flat = true, TooltipText = "20× speed — 5 (QA only)" };
        twenty.Pressed += () => SetSpeed(20.0);
        row.AddChild(twenty);
        _speedButtons.Add((twenty, 20.0));
        AddTheSkipControls(row);

        if (_frameCounter is not null)
        {
            RemoveChild(_frameCounter);
            _frameCounter.Position = Vector2.Zero;
            body.AddChild(_frameCounter);
        }

        RefuseKeyboardFocus(body);
    }

    /// <summary>The dev bar's default place: under the strip's two lines, its right edge at the right column's left.</summary>
    private void PlaceTheDevBar(float top)
    {
        if (_devBar is null)
        {
            return;
        }

        float fromRight = Size.X - RightColumnBegins(Size.X) + Edge;
        _devBar.OffsetLeft = -fromRight;
        _devBar.OffsetRight = -fromRight;
        _devBar.OffsetTop = top;
        _devBar.OffsetBottom = top;
    }

    /// <summary>
    /// ⭐ <c>bars:</c> (<c>hud.md §6</c>, step 4) — every cell posed at its longest (+12,345, the longest date and counts):
    /// the strip takes at most two lines at 1,280 and 1,422 logical wide, the second only where one would reach the right
    /// column; at the founding it is one line at both; it ends left of the right column; the left column starts below its
    /// two lines; the popup does not move; and the strip holds no 20×, no skip and no PAUSED label — Pause is held and reads
    /// <i>Paused</i> while the village is, and the speed it resumes at is held beside it.
    /// </summary>
    private string TheBarsHoldTheirShape()
    {
        Refresh();
        var faults = new List<string>();
        // ⏭️ Step 5 (hud.md §6.1–6.2) folds the food goods into one figure and hides a good or a headcount at zero; until then
        // the strip holds every cell and takes three lines, so "one line at the founding, two at most" is step 5's clause.
        int foundingAt1280 = StripLinesAt(Mathf.Min(StripOnOneLine(), StripRoom(1280f)));

        // The layout's own width, read back — the rest of this line computes from the rule, and a rule nobody applies passes.
        float wanted = Mathf.Min(StripOnOneLine(), StripRoom(Size.X));
        if (!Mathf.IsEqualApprox(_stripSlip.CustomMinimumSize.X, wanted))
        {
            faults.Add($"the slip is laid out {_stripSlip.CustomMinimumSize.X:F0} wide, not the {wanted:F0} its rule gives on this window");
        }

        Vector2 popupBefore = _morePopup.GetContentsMinimumSize();
        var cells = new List<Label> { _populationCell, _adultsCell, _childrenCell, _inSchoolCell, _eldersCell, _onTheGround };
        cells.AddRange(_goodsReadouts.Select(r => r.Held));
        var was = cells.Select(c => c.Text).ToList();
        foreach (Label cell in cells)
        {
            cell.Text = "+12,345";
        }

        foreach ((Goods _, Label mark) in _limitMarks)
        {
            mark.Text = "⚠";
        }

        (string clock, string households, string unhoused) texts = (_clockLabel.Text, _householdsLabel.Text, _unhousedLabel.Text);
        _clockLabel.Text = "Day 30, Autumn, Year 1234";
        _householdsLabel.Text = "·  999 households";
        _unhousedLabel.Text = "12,345 unhoused";

        float one = StripOnOneLine();
        var widths = new List<string>();
        foreach (float window in new[] { 1280f, 1422f })
        {
            float room = StripRoom(window);
            float slip = Mathf.Min(one, room);
            int lines = StripLinesAt(slip);
            float ends = Edge + (slip * _uiScale);
            widths.Add($"{window:F0}: {lines} line(s), ends {ends:F0} of {RightColumnBegins(window):F0}");
            if ((lines > 1 && one <= room) || ends > RightColumnBegins(window))
            {
                faults.Add($"at {window:F0} wide the strip ({one:F0} on one line, room {room:F0}) takes {lines} line(s) and ends at {ends:F0}, the right column at {RightColumnBegins(window):F0}");
            }
        }

        _stripSlip.CustomMinimumSize = new Vector2(Mathf.Min(one, StripRoom(1280f)), 0f);
        float stripFoot = Edge + (_topBar.GetCombinedMinimumSize().Y * _uiScale);
        _stripWidthSet = -1f;
        if (TopOfTheLeftColumn() < stripFoot + Edge - 0.5f)
        {
            faults.Add($"the left column starts at {TopOfTheLeftColumn():F0}, above the posed strip's foot at {stripFoot:F0}");
        }

        Vector2 popupAfter = _morePopup.GetContentsMinimumSize();
        if (!popupBefore.IsEqualApprox(popupAfter))
        {
            faults.Add($"posing the popup's rows moves it {popupBefore.X:F0}x{popupBefore.Y:F0} → {popupAfter.X:F0}x{popupAfter.Y:F0}");
        }

        for (int i = 0; i < cells.Count; i++)
        {
            cells[i].Text = was[i];
        }

        (_clockLabel.Text, _householdsLabel.Text, _unhousedLabel.Text) = texts;

        var onTheStrip = _topStrip.FindChildren("*", nameof(Label), owned: false).OfType<Label>().Select(l => l.Text)
            .Concat(_topStrip.FindChildren("*", nameof(Button), owned: false).OfType<Button>().Where(b => b.IsVisibleInTree()).Select(b => b.Text)).ToList();
        foreach (string gone in new[] { "20×", "20x", "Skip 1y", "Skip 10y", "PAUSED" })
        {
            if (onTheStrip.Contains(gone))
            {
                faults.Add($"the strip still holds {gone}");
            }
        }

        double speedWas = _driver.SpeedMultiplier;
        SetSpeed(4.0);
        SetSpeed(0.0);
        Button pause = _speedButtons[0].Button;
        Button four = _speedButtons.Find(s => s.Multiplier == 4.0).Button;
        bool heldPaused = pause.ButtonPressed && pause.Text == "Paused" && four.ButtonPressed
            && _speedButtons.Count(s => s.Button.ButtonPressed) == 2;
        pause.EmitSignal(BaseButton.SignalName.Pressed);
        bool resumed = _driver.SpeedMultiplier == 4.0 && !pause.ButtonPressed && pause.Text == "Pause" && four.ButtonPressed;
        SetSpeed(0.0);
        _UnhandledKeyInput(new InputEventKey { Keycode = Key.Space, Pressed = true });
        bool spaced = _driver.SpeedMultiplier == 4.0;
        SetSpeed(speedWas);
        if (!heldPaused || !resumed || !spaced)
        {
            faults.Add($"paused at 4× the strip held Pause and 4× {heldPaused}; Pause pressed resumed at 4× {resumed}; Space resumed at 4× {spaced}");
        }

        Refresh();
        return faults.Count == 0
            ? $"[widths] bars: ✅ {foundingAt1280} line(s) at the founding at 1,280 (step 5 makes it one); {cells.Count} cells at +12,345 and the longest date: "
              + $"{one:F0} on one line, so {string.Join("; ", widths)}; the left column clears the strip posed; no 20×, skip or PAUSED on it; "
              + "the slip laid out as its rule says; paused at 4×, Pause and 4× held, and Pause and Space resume at 4×"
            : $"[widths] bars: ⛔ {string.Join("; ", faults)}";
    }

    /// <summary>
    /// ⭐ <c>dev bar:</c> (<c>hud.md §7</c>, step 4) — in a debug build the dev bar stands, holding 20×, Skip 1y, Skip 10y and
    /// the frame readout; it is no window (not on the column, not in the windows' list) and key 5 is 20× through it.
    /// ⚠️ A release build cannot be probed here: its gate is <c>OS.IsDebugBuild()</c>, the one the skips have had since D254.
    /// </summary>
    private string TheDevBarHoldsTheDebugTools()
    {
        if (!OS.IsDebugBuild())
        {
            return "[widths] dev bar: ✅ a release build — no dev bar, no 20×, no key 5";
        }

        var faults = new List<string>();
        if (_devBar is not { Visible: true } bar)
        {
            return "[widths] dev bar: ⛔ a debug build with no dev bar shown";
        }

        var texts = bar.FindChildren("*", nameof(Button), owned: false).OfType<Button>().Select(b => b.Text).ToList();
        foreach (string want in new[] { "20×", "Skip 1y", "Skip 10y" })
        {
            if (!texts.Contains(want))
            {
                faults.Add($"no {want} on it");
            }
        }

        if (_frameCounter is null || !bar.IsAncestorOf(_frameCounter))
        {
            faults.Add("the frame readout is not on it");
        }

        if (WindowOf(bar) is not null || _columnWords.Exists(w => w.Word == DevBarTitle))
        {
            faults.Add("it is a window, or on the windows' column");
        }

        double speedWas = _driver.SpeedMultiplier;
        _UnhandledKeyInput(new InputEventKey { Keycode = Key.Key5, Pressed = true });
        bool five = _driver.SpeedMultiplier == 20.0 && _speedButtons.Find(s => s.Multiplier == 20.0).Button.ButtonPressed;
        SetSpeed(speedWas);
        if (!five)
        {
            faults.Add("key 5 did not set 20× and hold its button");
        }

        return faults.Count == 0
            ? "[widths] dev bar: ✅ a debug build: 20×, Skip 1y, Skip 10y and the frame readout on a floating bar that is no window; key 5 holds 20×"
            : $"[widths] dev bar: ⛔ {string.Join("; ", faults)}";
    }
}

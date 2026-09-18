using Bclone.Sim.Config;
using Bclone.Sim.Core;
using Bclone.Sim.Logging;
using Bclone.Sim.World;
using Godot;

namespace Bclone.Game;

/// <summary>
/// The Phase 1 view: watch a village, and be able to ask any villager why.
/// </summary>
/// <remarks>
/// <para>
/// Reads sim state, never writes it (DESIGN.md §3). The only thing it does to the
/// simulation is decide how many ticks to run.
/// </para>
/// <para>
/// The layout is built around the phase's Success Test — <em>watching twelve
/// villagers is still legible</em>. So the roster is always visible and one click
/// gives a full account of a person: what they are doing, why they hold that job,
/// who they live with. The reason strings exist in the sim either way; this is where
/// they become legibility for a player rather than a test assertion.
/// </para>
/// </remarks>
public partial class Main : Control
{
    private const int MaxLogLines = 400;

    /// <summary>
    /// How many idle workplaces to name before falling back to a count.
    /// </summary>
    /// <remarks>
    /// The alert wraps inside a floating panel now, so this is about how much a person
    /// wants to read rather than about what fits on a line. Four names, then a count —
    /// a player told the village is short of hands does not need the full inventory to
    /// act on it.
    /// </remarks>

    private SimLoop _loop = null!;
    private FixedTimestepDriver _driver = null!;
    private InMemoryLogSink _sink = null!;

    /// <summary>The full audit trail on disk — everything down to DEBUG.</summary>
    private FileLogSink _audit = null!;
    private string _logPath = string.Empty;

    private string _configSource = string.Empty;

    /// <summary>Which build this is, read off the assembly rather than typed anywhere.</summary>
    /// <remarks>
    /// <b>It comes from the <c>VERSION</c> file by way of <c>Directory.Build.props</c></b>, so
    /// there is one number and no second place to forget to update — which is the whole point of
    /// METHODOLOGY §5's "single source of version truth", and was not true of it until now.
    /// `VersionTests` fails the build if the wiring ever comes undone.
    /// </remarks>
    private static string BuildVersion
    {
        get
        {
            System.Version? v = typeof(Main).Assembly.GetName().Version;
            return v is null ? "?" : $"v{v.Major}.{v.Minor}.{v.Build}";
        }
    }

    private Label _clockLabel = null!;
    private Label _seedLabel = null!;
    private Label _speedLabel = null!;
    private ItemList _roster = null!;
    private RichTextLabel _inspector = null!;
    private PanelContainer _whatsHerePanel = null!;
    private RichTextLabel _villageLog = null!;
    private VillageMap _map = null!;

    private Button _detailButton = null!;
    private Button _soilButton = null!;
    private Button _wearButton = null!;

    private int _renderedLogEntries;
    private int _selectedVillagerId;

    /// <summary>
    /// The tile the player last clicked, when they clicked a building rather than
    /// picking a villager off the roster.
    /// </summary>
    /// <remarks>
    /// <b>Mutually exclusive with <see cref="_selectedVillagerId"/> on purpose.</b> One
    /// panel answers "what is that?", so there is exactly one thing selected at a time
    /// and no question about which of two selections the panel is describing.
    /// </remarks>
    private GridPos? _selectedTile;

    private MapDetail _detail = MapDetail.Selected;

    public override void _Ready()
    {
        SimConfig config = ConfigLocator.LoadOrDefault(out _configSource);

        // TWO SINKS, WANTING DIFFERENT THINGS.
        //
        // The village log on screen is the story (D8) and stays at INFO — six hundred
        // foraging trips would bury the handful of lines that carry it (D9). The file
        // takes everything down to DEBUG so a run can be audited afterwards: every
        // state change, every load carried, every job and every refusal, tick-stamped.
        //
        // The wall-clock filename is a filesystem concern and never enters the sim,
        // which is why FileLogSink takes the name rather than reading a clock —
        // Bclone.Sim is not allowed to know what time it is (BannedSymbols.txt).
        _sink = new InMemoryLogSink(LogLevel.Info);

        // ⚠️ NOTHING PRUNES THESE, AND THEY ARE THE LARGEST THING IN THE REPOSITORY DIRECTORY
        // (measured 2026-08-28: 235 files, **159 MB**, one of them 24 MB, against 5.5 MB of
        // tracked source). Every launch writes a new unbounded DEBUG file; there is no cap, no
        // rotation and no delete path here or in `FileLogSink`.
        //
        // ⛔ **Do not "fix" this by lowering the level or capping the file.** The audit trail at
        // full DEBUG is how D236 was found — the bug that had stopped the village working in
        // Year 3 was invisible in the code and obvious in a histogram of state transitions per
        // year. **A truncated log would have hidden it.**
        //
        // ⭐ The right cure is housekeeping, not a smaller log: delete old ones periodically and
        // keep the recent ones. Pruned by hand on 2026-08-28 to everything from 2026-08-26
        // onward (159 MB → 16 MB), deliberately keeping Joe's Year-44 session because it is the
        // evidence D236 rests on.
        string logDirectory = System.IO.Path.Combine(
            System.IO.Directory.GetCurrentDirectory(), "logs");
        string logPath = System.IO.Path.Combine(
            logDirectory, $"bclone-{System.DateTime.Now:yyyyMMdd-HHmmss}.log");

        _audit = new FileLogSink(logPath, LogLevel.Debug, alsoConsole: false);
        _logPath = logPath;

        var sinks = new CompositeLogSink(_sink, _audit);

        _loop = SimFactory.CreatePhase0(config, sinks);
        _driver = new FixedTimestepDriver(config, sinks);

        BuildUi();
        Refresh();
    }

    // ⛔ THE SCREENSHOT HOOK IS DELETED, NOT FIXED (D160, Joe's call). `BCLONE_SCREENSHOT` and
    // `BCLONE_SCREENSHOT_YEARS` fast-forwarded an *unattended* founding and then photographed
    // it — and since D110 an unattended founding raises nothing, so the hook had been
    // photographing a dead valley ever since. D143 then made that correct rather than broken:
    // an unattended village is supposed to die out.
    //
    // Teaching the harness to play the opening would mean moving `PlayTheOpening` out of the
    // test project and into shipped code, which is a real cost for a convenience.
    //
    // ⚠️ THIS REOPENS D11'S WARNING RATHER THAN ANSWERING IT: `src/Bclone.Game` now has no
    // automated verification of any kind. Looking at it is the verification, and Joe's eyes are
    // the test.

    public override void _Process(double delta)
    {
        // The single wall-clock read in the entire program.
        int ticks = _driver.Advance(delta, _loop.World.Tick);
        if (ticks > 0 && !_halted)
        {
            try
            {
                _loop.Step(ticks);
            }
            catch (SimSystemException fault)
            {
                HaltTheVillage(fault);
            }
        }

        Refresh();
        ProbeColumnWidths();
    }

    /// <summary>
    /// ⭐⭐ The error boundary — <b>what the game does when a tick throws</b> (D364,
    /// `tick-loop.md §5d`, the shell's first piece).
    /// </summary>
    /// <remarks>
    /// <para>
    /// For a week `gridless.md §10` carried the question: four things throw from inside a tick and
    /// nothing caught any of them — a throw took the process down and the village with it, with
    /// nothing on screen to say why. Now: the driver pauses and refuses every speed key from here
    /// on; the village log gets one line in the death colour that says which system failed, when
    /// in the village's own calendar, and where the log file and the seed are; the full exception
    /// goes to Godot's error stream; and the view keeps drawing the last state so every panel still
    /// reads. It does not crash, and it does not quietly carry on — a half-run tick is not a state
    /// anyone can resume from (`SimLoop.Fault`).
    /// </para>
    /// <para>
    /// ⚠️ Not a recovery. Save/load is its own shell piece and will want this same door for a
    /// corrupt file.
    /// </para>
    /// </remarks>
    private void HaltTheVillage(SimSystemException fault)
    {
        _halted = true;
        _driver.SpeedMultiplier = 0.0;
        _speedLabel.Text = "STOPPED";

        string sentence = TheVillageStoppedBecause(fault);
        _villageLog.AppendText(
            $"[color=#{ColourOf(LogCategory.Death).ToRgba32():x8}][b]⛔ {sentence.Replace("[", "[lb]", StringComparison.Ordinal)}[/b][/color]\n");
        GD.PushError($"{sentence}\n{fault}");
    }

    /// <summary>The one sentence the player reads when the village stops — the calendar, the system, the cause, and where to look.</summary>
    private string TheVillageStoppedBecause(SimSystemException fault)
    {
        SimClock when = SimClock.FromTick(fault.Tick, _loop.World.Config);
        Exception cause = fault.InnerException ?? fault;
        return $"The village stopped: system '{fault.SystemName}' failed at tick {fault.Tick:N0}, "
            + $"{when.SeasonAndYear()} — {cause.GetType().Name}: {cause.Message.TrimEnd('.')}. Nothing more will "
            + $"happen. This is a bug; the log at {_logPath} has the details, and the seed is {_loop.World.Seed}.";
    }

    private bool _halted;

    /// <summary>
    /// Print what every control in the two panel columns is claiming as a minimum width,
    /// then quit. Off unless <c>BCLONE_PROBE_WIDTHS</c> is set.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>⭐ THE THIRD TIME THIS QUESTION HAS BEEN ASKED, SO IT STOPS BEING A THROWAWAY.</b>
    /// D149 wrote this probe by hand to find that <em>"six stock-limit rows at 438 held a
    /// column at 450"</em>; D169 wrote it again for Joe's *"how dumb the width of the windows
    /// on the right side of the screen are."* Both times the answer was invisible from the
    /// layout code, because <b>a column is never narrower than its widest child</b> —
    /// a column could hand a <see cref="ScrollContainer"/> whatever
    /// <c>Offset</c> it likes and Godot will overrule it.
    /// </para>
    /// <para>
    /// <b>⚠️ The inspector's rows are empty and hidden until something is selected</b>, so a
    /// probe of a fresh village would report them at nothing and miss the ones that matter.
    /// The second pass poses the longest sentence each row can really hold and asks what it
    /// would then want — which is the number that decides the column the moment the player
    /// clicks a building.
    /// </para>
    /// <para>
    /// Diagnostic output only: it never changes what the game does and it is not a hook with
    /// a side effect, which is the distinction D160 drew when it deleted the screenshot one.
    /// </para>
    /// </remarks>
    private void ProbeColumnWidths()
    {
        if (_probed || System.Environment.GetEnvironmentVariable("BCLONE_PROBE_WIDTHS") is null)
        {
            return;
        }

        // A few frames in, so the containers have been laid out at least once.
        if (++_probeFrames < 20)
        {
            return;
        }

        _probed = true;

        GD.Print(
            $"[widths] window {Size.X:F0} x {Size.Y:F0}, drawn at {_uiScale * 100f:F0}%");

        ProbePanelWidths("at the founding");
        GD.Print(TheCardsHoldTheirShape());
        GD.Print(TheBarsHoldTheirShape());

        // ⭐⭐ THE TWO SELF-SCROLLING PANELS, MEASURED — because they are the two that can hold
        // their content correctly and draw NONE of it. Both were `size 288x0` for the life of
        // D306: an `ItemList` and a `ScrollFollowing` `RichTextLabel` each report a minimum
        // height of zero, so a `ScrollContainer` around either lays it out at nothing. **Joe saw
        // a blank roster beside "6 villagers in 2 households".** *A count beside a drawn height
        // is the only pair that can say this; neither number alone can.*
        ProbeASelfScroller("roster", _roster, _roster.ItemCount, "items");
        ProbeASelfScroller("vlog", _villageLog, _villageLog.GetParsedText().Length, "chars");

        ProbeFolding();

        // ⛔ BEFORE `ProbeTheControlBar`, NOT AFTER, AND THE ORDER IS THE MEASUREMENT. That method
        // poses the bar with every button showing at once — 1657px, wider than the 1280 window —
        // and the restore does not shrink `Size.X` back within the same call. Asked afterwards,
        // this measured every sentence against 377 pixels the player does not have.
        GD.Print(_map.TheCentreOfATileDrawsWhereTheTileDoes());
        GD.Print(_map.AVillagerDrawsWhereTheyStand());
        GD.Print(ZoneOutline.SelfCheck());
        GD.Print(_map.ATracedOutlineLandsOnItsOwnRectangle());
        GD.Print(ValleyTexture.SelfCheck(_loop.World));
        SaveTheValleyBake();
        GD.Print(EveryTickSaysWhatTheMapIsActuallyDoing());
        GD.Print(EveryWindowTickSaysWhatTheWindowIsDoing());
        GD.Print(_map.TheTreesAreScatteredAndOverhang());
        GD.Print(_map.TheDepositsAreScatteredAndOverhang());
        GD.Print(_map.TheSceneryIsMeshed());
        GD.Print(_map.AHeapAtADoorIsSeen());
        GD.Print(_map.TheFieldsStayInsideTheirFences());
        ProbeThePlacementSentences();

        ProbeTheControlBar();
        ProbeTheProfessionsPanel();

        ProbeTheLogLines();

        // ⛔ AND THE PANELS AGAIN, TWELVE YEARS IN (D367). At the founding there are no heaps and
        // the larders are empty, so the first measurement cannot see the strings that used to
        // widen the Overview; this one can. The two rows under `more ▾` are posed as well, in
        // case the run happens to have nothing on the ground.
        Refresh();
        _onTheGround.Text = "+1,234";
        _foodElsewhere.Text = "+12,345";
        ForceUpdateTransform();
        ProbePanelWidths("twelve years in, with heaps and larders posed");

        // ⚠️ After the log probe, which is what runs the valley twelve years — asked before it the
        // line reads "0 worn tiles" and proves nothing (D358).
        GD.Print(_map.TheTrailsLieOnTheGround());
        ProbeTheErrorBoundary();
        GD.Print("[widths] done.");
        GetTree().Quit();
        return;

        static void ProbeASelfScroller(string tag, Control control, int held, string unit)
        {
            bool wrapped = control.GetParent()?.GetParent() is ScrollContainer;
            GD.Print(
                $"[widths] {tag}: {held} {unit}, drawn {control.Size.X:F0}x{control.Size.Y:F0}, "
                + $"min {control.GetCombinedMinimumSize().Y:F0}"
                + (control.Size.Y < 1f
                    ? "  ⛔ ZERO TALL — it holds its content and draws none of it"
                    : string.Empty)
                + (wrapped
                    ? "  ⛔ inside a ScrollContainer, which lays it out at its minimum"
                    : string.Empty));
        }
    }

    /// <summary>The control bar at the bottom, which the column probe never looked at.</summary>
    /// <remarks>
    /// <para>
    /// <b>⛔ ADDED BECAUSE THE BAR BROKE TWICE IN ONE DAY AND THIS TOOL COULD NOT SEE IT</b>
    /// (2026-08-27). D169 built the probe for the columns and the inspector rows, and the bottom
    /// bar — the one part of the UI that <b>grows during play</b>, when the library button
    /// appears — was the part nothing measured.
    /// </para>
    /// <para>
    /// <b>⭐ It prints the two numbers whose disagreement IS the bug, for each row:</b> what the
    /// row's contents demand, and how much room the row actually has. Wider than the window is
    /// the first failure (buttons walk off the edge); a bar far narrower than the window is the
    /// second (a flow container with no width to wrap inside, stacking into a column). <b>Both
    /// are obvious here and neither is guessable from the layout code.</b>
    /// </para>
    /// </remarks>
    /// <summary>
    /// What the village log will actually render, run through the real markup and stripped of it.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>⭐⭐ KEPT BECAUSE IT EARNED ITS KEEP IN ONE RUN (2026-08-29).</b> Written as a throwaway
    /// to check the new timestamp column, it immediately printed
    /// <em>"Agnes is dangerously cold and is going in to get warm —"</em>: **taking the date off
    /// the end of a sentence that joined it with an em dash left the line hanging mid-thought.**
    /// No amount of reading <c>Stamped</c> would have shown that. *Print what the control will
    /// actually show before believing a string transform.*
    /// </para>
    /// <para>
    /// <b>⚠️ IT STEPS THE SIM, WHICH THE OTHER PROBES DELIBERATELY DO NOT.</b> A fresh village has
    /// three log lines and none of the interesting shapes, so a probe of tick zero would report
    /// almost nothing. **That is only acceptable because it runs LAST, one line before
    /// <c>GetTree().Quit()</c>** — nothing observes the world afterwards. ⛔ *If anything is ever
    /// added after this call, this has to move or stop stepping.*
    /// </para>
    /// <para>
    /// It goes through <see cref="LogMarkup"/> rather than reimplementing it, then strips the
    /// BBCode back off — so what it prints is what the panel would show, including the stamp, the
    /// stripping and the season lines that deliberately have no stamp.
    /// </para>
    /// </remarks>
    /// <summary>
    /// Every docked panel's position and size — <b>and whether it has widened past the width it
    /// was given</b> (D367).
    /// </summary>
    /// <remarks>
    /// A panel's rect is a starting width, not a limit: Godot grows a Control to its children's
    /// minimum, so a Label with a long string in it is a wider panel. Joe: *"I hate how the size
    /// of the panels change when information updates."* The width a panel was given is
    /// <c>OffsetRight − OffsetLeft</c> (a drag moves all four offsets together); what it wants is
    /// its combined minimum. Called twice — at the founding, and twelve years in with heaps and
    /// larders posed — because the strings that widen a panel do not exist at tick zero.
    /// </remarks>
    private void ProbePanelWidths(string when)
    {
        // ⚠️ UNFOLDED, OR A FOLDED PANEL HIDES ITS WIDTH. A folded panel's contents are not
        // visible and do not count toward its minimum — the Professions window is folded at the
        // founding, and the first red check of this probe scored zero on it for exactly that.
        var wereOpen = new List<bool>(_headers.Count);
        foreach (Button header in _headers)
        {
            wereOpen.Add(header.ButtonPressed);
            header.ButtonPressed = true;
        }

        ForceUpdateTransform();

        int widened = 0;
        for (int i = 0; i < _docked.Count; i++)
        {
            (PanelContainer panel, bool right) = _docked[i];
            float given = panel.OffsetRight - panel.OffsetLeft;
            float wants = panel.GetCombinedMinimumSize().X;
            bool wide = wants > given + 1f;
            widened += wide ? 1 : 0;
            GD.Print(
                $"[widths] panel {(right ? "right" : "left ")} at "
                + $"({panel.Position.X:F0}, {panel.Position.Y:F0}) "
                + $"size {panel.Size.X:F0}x{panel.Size.Y:F0}"
                + (panel.Visible ? string.Empty : " (hidden)")
                + (wide ? $"  ⛔ widened to {wants:F0} from {given:F0}" : string.Empty));
        }

        for (int i = 0; i < _headers.Count; i++)
        {
            _headers[i].ButtonPressed = wereOpen[i];
        }

        // ⭐ AND THE TOP BAR (D378), which is content-sized rather than given a width, so the
        // question for it is not "did it outgrow its box" but "did it move between the two
        // passes" — the founding against twelve years of heaps, larders and a grown village.
        Vector2 bar = _topBar.GetCombinedMinimumSize();
        bool moved = _barMeasured is Vector2 first && !first.IsEqualApprox(bar);
        widened += moved ? 1 : 0;
        GD.Print(
            $"[widths] panel top   at ({_topBar.Position.X:F0}, {_topBar.Position.Y:F0}) "
            + $"size {_topBar.Size.X:F0}x{_topBar.Size.Y:F0}, wants {bar.X:F0}x{bar.Y:F0}"
            + (moved ? $"  ⛔ moved from {_barMeasured!.Value.X:F0}x{_barMeasured.Value.Y:F0} at the founding" : string.Empty));
        _barMeasured ??= bar;

        GD.Print(widened == 0
            ? $"[widths] panels: ✅ none wider than it was given, {when}"
            : $"[widths] panels: ⛔ {widened} widened past the width they were given, {when}");
    }

    /// <summary>The top bar's minimum at the founding, so the second pass can say whether it moved.</summary>
    private Vector2? _barMeasured;

    /// <summary>
    /// The top bars at their longest: every number posed at six figures and a sign, the clock
    /// line at a long date — and the bar must not move (D378).
    /// </summary>
    /// <remarks>
    /// <para>
    /// The founding's numbers are one and two digits, so a bar measured there proves nothing
    /// about a cell without <see cref="Amount"/>'s trio — the same zero the first cards probe
    /// scored until a long name was posed (handoff trap 58). Posed instead: <c>+12,345</c> in
    /// every cell on the bar and in the popup, and a four-digit year with two-digit households.
    /// </para>
    /// <para>
    /// Also says where the bar ends against where the right column begins at the 1280 layout
    /// width, because a bar that runs under the minimap is a bar nobody can read.
    /// </para>
    /// </remarks>
    private string TheBarsHoldTheirShape()
    {
        Refresh();
        ForceUpdateTransform();
        Vector2 before = _topBar.GetCombinedMinimumSize();
        Vector2 popupBefore = _morePopup.GetContentsMinimumSize();

        var cells = new List<Label> { _foodTotal, _populationCell, _adultsCell, _childrenCell, _eldersCell, _laborersCell, _foodElsewhere, _onTheGround };
        foreach ((Goods _, Label held) in _goodsReadouts)
        {
            cells.Add(held);
        }

        foreach (Label cell in cells)
        {
            cell.Text = "+12,345";
        }

        string clockWas = _clockLabel.Text;
        _clockLabel.Text = $"{_loop.World.Name}   ·   Day 30, Autumn, Year 1234   ·   99 households";

        ForceUpdateTransform();
        Vector2 after = _topBar.GetCombinedMinimumSize();
        Vector2 popupAfter = _morePopup.GetContentsMinimumSize();

        _clockLabel.Text = clockWas;
        Refresh();

        float barEnds = Edge + (after.X * _uiScale);
        float columnBegins = 1280f - Edge - DefaultPanelWidth;
        string where = $"the bar ends at {barEnds:F0}px and the right column begins at {columnBegins:F0}px at {_uiScale * 100f:F0}%";

        if (!before.IsEqualApprox(after))
        {
            return $"[widths] bars: ⛔ posing every cell at +12,345 moves the bar {before.X:F0}x{before.Y:F0} → {after.X:F0}x{after.Y:F0}";
        }

        if (!popupBefore.IsEqualApprox(popupAfter))
        {
            return $"[widths] bars: ⛔ posing the popup's rows moves it {popupBefore.X:F0}x{popupBefore.Y:F0} → {popupAfter.X:F0}x{popupAfter.Y:F0}";
        }

        return barEnds < columnBegins
            ? $"[widths] bars: ✅ {cells.Count} cells posed at +12,345 and the bar stays {after.X:F0}x{after.Y:F0}; {where}"
            : $"[widths] bars: ⛔ {where} — the bar runs under the right column";
    }

    /// <summary>
    /// The error boundary, posed — <b>a throw the sim never made, handed to the handler, and the
    /// sentence it produces</b> (D364). Last, because it stops the village.
    /// </summary>
    private void ProbeTheErrorBoundary()
    {
        var posed = new SimSystemException(
            "paths", _loop.World.Tick, new InvalidOperationException("posed by the probe"));
        HaltTheVillage(posed);
        SetSpeed(4.0);

        bool stopped = _halted && _driver.IsPaused && _speedLabel.Text == "STOPPED";
        GD.Print(stopped
            ? $"[widths] fault: ✅ the village stops and stays stopped — \"{TheVillageStoppedBecause(posed)}\""
            : "[widths] fault: ⛔ a speed key restarted a stopped village");
    }

    private void ProbeTheLogLines()
    {
        GD.Print("[log] --- what the village log renders, twelve years in ---");
        _loop.Step(_loop.World.Config.TicksPerYear * 12);

        IReadOnlyList<LogEntry> seen = _sink.Entries;
        int shown = 0;
        for (int i = 0; i < seen.Count && shown < 18; i++)
        {
            if (!Shown(seen[i]))
            {
                continue;
            }

            shown++;
            string plain = System.Text.RegularExpressions.Regex.Replace(
                LogMarkup(seen[i]), @"\[[^\]]*\]", string.Empty);
            GD.Print("[log] " + plain.TrimEnd());
        }
    }

    /// <summary>
    /// Measure the professions window, which is the first table this project has drawn.
    /// </summary>
    /// <remarks>
    /// <para>
    /// ⛔⛔ <b>A `GridContainer`'s minimum width is the SUM of its column minimums</b>, which is why
    /// this table could not live in a side column and why it needs measuring rather than
    /// eyeballing. The bug it is guarding against is recorded twice already: an inspector row
    /// wanting 733px against a 267px column, and six stock-limit rows holding the left column at
    /// 450.
    /// </para>
    /// <para>
    /// ⚠️ <b>Measured while HIDDEN, and that is fine</b> — `GetCombinedMinimumSize` is a layout
    /// question, not a drawing one. It is also the only way to measure it: the panel starts closed,
    /// and D242's whole lesson is that a layout correct at startup can be wrong later.
    /// </para>
    /// </remarks>
    private void ProbeTheProfessionsPanel()
    {
        if (_professionsPanel is null)
        {
            GD.Print("[widths] --- professions panel: NOT BUILT ---");
            return;
        }

        // ⚠️ The width it was ASKED for, read off the panel rather than repeated here — the
        // first draft hard-coded 520 and went on saying it after the panel was narrowed to
        // 380, which is a probe lying about the thing it exists to measure.
        float given = _professionsPanel.Size.X;
        float wants = _professionsPanel.GetCombinedMinimumSize().X;

        GD.Print(
            $"[widths] --- professions panel wants {wants:F0}, is {given:F0} wide, drawn at "
            + $"{_uiScale * 100f:F0}% ---"
            + (wants > given ? "  ⚠️ TOO WIDE" : string.Empty));

        PrintWidths(_professionsPanel, "prof", 0);
    }

    private void ProbeTheControlBar()
    {
        if (_controlBar is null)
        {
            GD.Print("[widths] --- control bar: NOT BUILT ---");
            return;
        }

        // ⛔⛔ POSED AT ITS WIDEST, BECAUSE MEASURING IT AS IT STARTS IS THE BUG THIS PROBE EXISTS
        // TO CATCH (D242, extended 2026-08-29 for the town hall). **Every look anybody takes at
        // the UI is a look at a young village** — and this bar's two conditional groups, Knowledge
        // and Civic, are hidden until the village can write and until its founders are gone. The
        // bar the player has in year sixty is a bar with two more categories in it, and until now
        // *the tool built to measure the growing bar was itself only ever measuring the young one.*
        //
        // ⚠️ Shown, then measured, then put back — the probe quits immediately afterwards, but
        // leaving the game in a state it could not have reached on its own is how an instrument
        // starts lying about something else.
        // ⛔⛔ AND NOW IT HAS A THIRD WAY TO HIDE FROM ITS OWN INSTRUMENT: THE TABS. Two thirds of
        // the strip is hidden at any moment, so a probe that measured what was showing would
        // measure the BUILD tab, report a bar that fits, and ship the *"correct at startup, wrong
        // later"* fault this whole method exists because of. **Every button on the strip is shown
        // at once**, which is wider than any tab will ever be — a deliberate over-estimate, and
        // the right direction to be wrong in.
        var wereShowing = new List<(Button Button, bool Was)>(_strip.Count);
        foreach ((BuildTab Tab, BuildCategory Category, Button Button, BuildingKind? Kind, ToolMark? Mark) entry in _strip)
        {
            wereShowing.Add((entry.Button, entry.Button.Visible));
            entry.Button.Visible = true;
        }

        // Every child of the filter row AND the tab note at once — wider than any real tab,
        // which is the deliberate over-estimate this probe exists to make.
        bool filterWas = _filterRow.Visible;
        bool noteWas = _tabNote.Visible;
        _filterRow.Visible = true;
        _tabNote.Visible = true;
        foreach (Node chip in _filterRow.GetChildren())
        {
            if (chip is Control control)
            {
                control.Visible = true;
            }
        }

        // A flow container's minimum is recomputed on the next layout pass, not on assignment.
        _controlBar.QueueSort();
        ForceUpdateTransform();

        GD.Print($"[widths] --- control bar, POSED with every tab, every filter and every "
            + $"category the village will ever unlock showing at once: "
            + $"is {_controlBar.Size.X:F0} wide "
            + $"of a {Size.X:F0} window, and {_controlBar.Size.Y:F0} tall ---");

        foreach (Node child in _controlBar.GetChildren())
        {
            if (child is not Control row)
            {
                continue;
            }

            float wants = row.GetCombinedMinimumSize().X;

            // ⭐⭐ THE NUMBER THAT ACTUALLY DECIDES WRAPPING, AND `wants` IS NOT IT (2026-08-29).
            // **An `HFlowContainer`'s minimum width is its WIDEST SINGLE CHILD** — so `wants` is
            // blind to how many buttons are on the row, and adding a whole category moved it by
            // exactly zero. *That is the property that made the flow container collapse the bar
            // into a corner in the first place (D242), read from the other side.*
            //
            // What fills a row is the SUM, and that is what says how close the bar is to needing
            // another line. Printed beside `wants` rather than instead of it: the two answer
            // different questions, and the day one of them is the bug you want both on screen.
            float laidEndToEnd = 0f;
            int shown = 0;
            foreach (Node grandchild in row.GetChildren())
            {
                if (grandchild is Control item && item.Visible)
                {
                    laidEndToEnd += item.GetCombinedMinimumSize().X;
                    shown++;
                }
            }

            string verdict = wants > _controlBar.Size.X + 1f
                ? "  ⛔ WANTS MORE THAN IT HAS — this row runs off the screen"
                : laidEndToEnd > _controlBar.Size.X + 1f
                    ? "  ⚠️ wraps — its contents do not fit on one line"
                    : string.Empty;

            GD.Print($"[widths] bar    {row.GetType().Name} wants {wants:F0}, "
                + $"{shown} items end to end want {laidEndToEnd:F0}, "
                + $"is {row.Size.X:F0} x {row.Size.Y:F0}{verdict}");
        }

        foreach ((Button button, bool was) in wereShowing)
        {
            button.Visible = was;
        }

        _filterRow.Visible = filterWas;
        _tabNote.Visible = noteWas;

        // ⭐⭐ AND THE HEIGHT OF EACH TAB IN TURN, WHICH IS THE THING JOE ACTUALLY REPORTED.
        // The pose above measures the widest the bar can ever be; this measures whether the bar
        // MOVES. **A bar that is two rows on two tabs and three on the third makes the map jump
        // every time the player switches**, and no width figure can say so — only the same
        // number read three times can. *The over-estimate and the comparison are different
        // questions and the probe now answers both.*
        // ⛔⛔ EVERY TAB **AND EVERY FILTER**, because the first version of this probe measured
        // the tabs at the default filter and reported a steady 151 — and Joe then found BUILD
        // taller than the others and *"build → civic collapses the bottom bar when empty"*.
        // **The strip row is what moves**: ALL wraps to two rows, and a category with nothing in
        // it has no rows at all. *A probe that varies one dimension of a two-dimensional space
        // reports a steadiness that does not exist.*
        BuildTab wasOn = _tab;
        BuildCategory? filterWasSet = _filter;
        var heights = new List<string>();
        float shortest = float.MaxValue;
        float tallest = 0f;
        string shortestAt = string.Empty;
        string tallestAt = string.Empty;

        foreach (BuildTab tab in new[] { BuildTab.Build, BuildTab.Removal, BuildTab.Harvest })
        {
            foreach (BuildCategory? category in TheFilterStates(tab))
            {
                _tab = tab;
                _filter = category;
                RefreshTheStrip();
                _controlBar.QueueSort();
                ForceUpdateTransform();

                float tall = _controlBar.Size.Y;
                string where = $"{tab}/{(category is null ? "ALL" : category.ToString())}";
                heights.Add($"{where} {tall:F0}");

                if (tall < shortest)
                {
                    shortest = tall;
                    shortestAt = where;
                }

                if (tall > tallest)
                {
                    tallest = tall;
                    tallestAt = where;
                }
            }
        }

        _tab = wasOn;
        _filter = filterWasSet;
        RefreshTheStrip();
        _controlBar.QueueSort();
        ForceUpdateTransform();

        GD.Print($"[widths] bar height, every tab and filter: {string.Join(", ", heights)}");
        GD.Print(
            tallest - shortest <= 1f
                ? $"[widths] bar height: ✅ {tallest:F0} everywhere"
                : $"[widths] bar height: ⛔ {shortest:F0} at {shortestAt} to {tallest:F0} at "
                    + $"{tallestAt} — the map jumps by {tallest - shortest:F0}px");
    }

    /// <summary>
    /// ⭐⭐ What the placement line actually renders each tool's sentence to (D327).
    /// </summary>
    /// <remarks>
    /// <para>
    /// ⛔⛔ <b>THE PLACEMENT LABEL IS THE ONE CONTROL <see cref="PinTheBarHeight"/> MEASURES WITH A
    /// PLACEHOLDER RATHER THAN REAL TEXT.</b> It reserves a bare newline, for a stated reason — the
    /// messages come from the map *and* from the sim's own refusals, so there is no list to take a
    /// longest from. **The consequence is that a sentence which wraps at runtime grows the bar past
    /// its own pin and nothing catches it**, which is exactly the 161 → 181 fault D323 found by
    /// hand.
    /// </para>
    /// <para>
    /// ⭐ The map's own sentences <em>are</em> a list, so they can be posed. This prints what each
    /// one renders to and flags any that takes more than one line — D255's rule applied to the
    /// other label: <em>print what the control will show before believing a string transform.</em>
    /// </para>
    /// <para>
    /// ⚠️ Posed and put back, like every other pose in this probe. It reads the sentences from the
    /// map rather than holding its own copies, or it would be measuring text the game does not say.
    /// </para>
    /// </remarks>
    private void ProbeThePlacementSentences()
    {
        if (_placementLabel is null)
        {
            GD.Print("[widths] --- placement line: NOT BUILT ---");
            return;
        }

        // ⛔⛔ MEASURED THROUGH THE FONT, NOT BY POSING THE TEXT AND READING `Size.Y` — AND THE
        // FIRST VERSION DID THE SECOND AND REPORTED A GREEN THAT MEANT NOTHING (D327). The
        // placement label is hidden whenever there is no message, so it is never laid out; posing
        // it visible and re-sorting does not make the container reflow within the same call, and
        // every sentence — including a 209-character one — duly measured **18 tall at 120 wide**,
        // which is `WrappedTextMinWidth` rather than any line the player has. *A wrap check
        // performed at the wrong width is exactly the instrument-that-assumes-a-default trap
        // D326 paid for, and it passes everything.*
        //
        // ⭐ The font knows without being laid out. `GetStringSize` is what the label's own
        // minimum-size calculation asks, so this is the same number by the same route.
        Font font = _placementLabel.GetThemeFont("font");
        int size = _placementLabel.GetThemeFontSize("font_size");
        // ⛔⛔ THE WINDOW, NOT THE BAR, AND THE TWO DIFFER BY 377 PIXELS TODAY. The control bar is
        // content-sized and its strip row already overflows — it measures 1657 against a 1280
        // window — so a sentence that "fits the bar" can still be running off the screen. **The
        // window is the ceiling that exists**, and `--resolution` is ignored here because
        // `project.godot` lays the UI out at 1280 logical pixels and scales it: *there is no "it
        // will fit on a bigger screen"* (D242).
        float available = Mathf.Min(_controlBar?.Size.X ?? Size.X, Size.X);

        GD.Print($"[widths] --- placement line has {available:F0} of a {Size.X:F0} window "
            + $"(bar claims {_controlBar?.Size.X ?? 0f:F0}), font {size} ---");

        foreach ((string Tool, string Sentence) posed in _map.EverySentenceAToolCanSay())
        {
            float wide = font.GetStringSize(
                posed.Sentence, HorizontalAlignment.Left, -1f, size).X;

            // ⚠️ A "tight" band, because the longest sentence sat at 6px spare when this probe was
            // written and one added clause is 45. **A line with no headroom is a line the next
            // edit wraps**, and the failure is invisible: the label grows the bar past the height
            // `PinTheBarHeight` reserved for one line of it.
            string verdict = wide > available
                ? "  ⛔ WRAPS — this grows the bar past its pin"
                : wide > available * 0.92f
                    ? $"  ⚠️ tight — only {available - wide:F0} spare"
                    : $"  ({available - wide:F0} spare)";

            GD.Print($"[widths] say    {posed.Tool,-12} {posed.Sentence.Length,3} chars, "
                + $"{wide:F0} of {available:F0}px{verdict}");
        }
    }

    /// <summary>
    /// ⭐ Does folding a panel actually make it smaller? — Joe, 2026-09-06.
    /// </summary>
    /// <remarks>
    /// <b>*"the panels do not minimize properly, the shape stays open but the content
    /// minimizes."*</b> A fold that hides the contents and leaves the frame at full size is worse
    /// than no fold at all: it costs the same map room and loses the information. **Only a height
    /// read on both sides of the toggle can say whether it worked**, which is why this poses the
    /// fold rather than trusting that `Visible = false` shrinks anything.
    /// </remarks>
    private void ProbeFolding()
    {
        GD.Print("[widths] --- folding, panel by panel ---");

        // ⛔⛔ MINIMUM HEIGHT, NOT `Size.Y`, AND THE FIRST VERSION OF THIS PROBE GOT IT WRONG.
        // Godot settles an anchored control's SIZE in its own layout pass, so reading `Size.Y`
        // in the same synchronous block returns the height from before the fold — it reported
        // `444 → 444` while the panel's minimum had correctly dropped to 37 and the offsets were
        // already a zero-height rectangle. **The probe was measuring staleness and calling it a
        // bug.** The minimum is what the layout will settle to, and it is available immediately.
        // ⚠️ UNFOLD EVERYTHING FIRST, AND PUT IT BACK AFTERWARDS (D326). This measured whatever
        // state each panel happened to be in, which was fine while every panel started open — and
        // became a FALSE POSITIVE the moment one started folded, because its "open" height was
        // already its folded height and it duly "failed to shrink".
        // *An instrument that assumes a default is an instrument that breaks when the default moves.*
        var wereOpen = new List<bool>(_headers.Count);
        foreach (Button header in _headers)
        {
            wereOpen.Add(header.ButtonPressed);
            header.ButtonPressed = true;
        }

        ForceUpdateTransform();

        var open = new List<float>(_docked.Count);
        foreach ((PanelContainer panel, bool _) in _docked)
        {
            open.Add(panel.GetCombinedMinimumSize().Y);
        }

        foreach (Button header in _headers)
        {
            header.ButtonPressed = false;
        }

        foreach ((PanelContainer panel, bool _) in _docked)
        {
            panel.QueueSort();
        }

        ForceUpdateTransform();

        int stuck = 0;
        for (int i = 0; i < _docked.Count; i++)
        {
            (PanelContainer panel, bool _) = _docked[i];
            if (!panel.Visible)
            {
                continue;
            }

            float folded = panel.GetCombinedMinimumSize().Y;

            // ⛔ AND THE OFFSETS HAVE TO BE A ZERO-HEIGHT RECTANGLE, or the minimum is irrelevant:
            // a panel whose top and bottom are pinned apart renders that far apart whatever it
            // wants to be. That is exactly what `ArrangeDefaults` used to do.
            bool free = Mathf.Abs(panel.OffsetBottom - panel.OffsetTop) < 1f;
            bool shrank = folded < open[i] - 1f && free;
            if (!shrank)
            {
                stuck++;
            }

            GD.Print(
                $"[widths] fold: wants {open[i]:F0} open → {folded:F0} folded, "
                + $"offsets {panel.OffsetTop:F0}..{panel.OffsetBottom:F0}"
                + (free ? string.Empty : " ⛔ PINNED APART")
                + (shrank ? string.Empty : "  ⛔ THE FRAME WILL NOT SHRINK"));
        }

        for (int i = 0; i < _headers.Count; i++)
        {
            _headers[i].ButtonPressed = wereOpen[i];
        }

        ForceUpdateTransform();

        GD.Print(
            stuck == 0
                ? "[widths] fold: ✅ every panel shrinks when folded"
                : $"[widths] fold: ⛔ {stuck} panels keep their full height when folded");
    }

    /// <summary>
    /// ⭐ The inspector holds ALL of a market's description — <b>the `Holding:` line included,
    /// above any fold</b> (D350).
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Joe: *"the market doesn't tell how many units of each item are within."*</b> It did.
    /// The market is a workplace and a store, its description was the longest in the game, and the
    /// line sat one below a 140px scroll with no visible bar. Posed rather than waited for: the
    /// cold start has no market, so the text is the two descriptions a market produces, and the
    /// label's minimum height has to hold every line of it.
    /// </para>
    /// <para>
    /// ⚠️ One thing measured — the label's minimum against its content. The store-first ORDER is
    /// a fact about `DescribeWhatIsAt` and is read there, not here: the cold start has no market to
    /// ask, and a probe that checks its own posed string would be measuring itself.
    /// </para>
    /// </remarks>

    /// <summary>One line per control: how wide it insists on being, and what it is.</summary>
    private static void PrintWidths(Node node, string side, int depth)
    {
        foreach (Node child in node.GetChildren())
        {
            if (child is Control control)
            {
                float wants = control.GetCombinedMinimumSize().X;

                // Only the ones that could actually be pinning a column open. A tree of
                // four hundred lines is the same as no answer at all.
                if (wants >= 200f)
                {
                    string text = control switch
                    {
                        Label label => label.Text,
                        Button button => button.Text,
                        _ => string.Empty,
                    };

                    GD.Print($"[widths] {side} {new string(' ', depth * 2)}{control.GetType().Name} "
                        + $"wants {wants:F0}{(control.Visible ? string.Empty : " (hidden)")}"
                        + $"{(text.Length == 0 ? string.Empty : $" — \"{text}\"")}");
                }
            }

            PrintWidths(child, side, depth + 1);
        }
    }

    /// <summary>Enough of a sentence to recognise it in a probe line.</summary>

    private bool _probed;

    /// <summary>The bottom bar, kept so the width probe can measure it (2026-08-27).</summary>
    private VBoxContainer? _controlBar;
    private int _probeFrames;

    /// <summary>Close the audit log cleanly when the window goes.</summary>
    /// <remarks>
    /// It auto-flushes on every line, so nothing is lost if the process is killed — but
    /// leaving the handle open would keep the file locked against whoever wants to read
    /// it, which is the whole point of writing it.
    /// </remarks>
    public override void _ExitTree() => _audit?.Dispose();

    public override void _UnhandledKeyInput(InputEvent @event)
    {
        if (@event is not InputEventKey { Pressed: true, Echo: false } key)
        {
            return;
        }

        // ⛔ WHILE A MOMENT IS UP, THE KEYBOARD DISMISSES IT AND DOES NOTHING ELSE. A player who
        // hits space to unpause would otherwise resume the village behind a panel that is still
        // covering it — and the speed keys would fight `DismissTheMoment`'s restore. **One thing to
        // do, and the two obvious keys both do it.**
        //
        // ⭐ THIS DELIBERATELY ASKS ABOUT `_momentPanel` ONLY, NOT THE PASSING BANNER (2026-08-27).
        // A moment that does not stop the village must not take the keyboard either — the whole
        // point of it is that play continues, and a banner that swallowed the speed keys for
        // fourteen seconds would be the interruption `Moment.WaitsToBeDismissed` exists to avoid.
        if (_momentPanel is { Visible: true })
        {
            if (key.Keycode is Key.Space or Key.Escape or Key.Enter)
            {
                DismissTheMoment();
            }

            return;
        }

        switch (key.Keycode)
        {
            case Key.Space: SetSpeed(_driver.IsPaused ? 1.0 : 0.0); break;
            case Key.Key1: SetSpeed(1.0); break;
            case Key.Key2: SetSpeed(2.0); break;
            case Key.Key3: SetSpeed(4.0); break;
            case Key.Key4: SetSpeed(10.0); break;
            case Key.Tab: CycleDetail(); break;
            case Key.G: ToggleSoil(); break;
            case Key.P: ToggleWear(); break;

            // ⭐ R TURNS WHAT IS IN YOUR HAND (gridless 2b, D320). Without a key, facing would be
            // a sim capability the player cannot reach — this project's fifth such feature if it
            // shipped that way, and D227's standing rule is that a sim feature is not done until
            // something in the view calls it.
            case Key.R: _map.TurnTheGhost(toTheQuarter: key.ShiftPressed); break;
            case Key.Home: _map.CentreOnTheVillage(); break;

            // ⭐⭐ ESC IS THE CANCEL FOR EVERY TOOL (D327). Right-click used to be, and it is a
            // brush's "take back" now — **a gesture removed without a replacement is a tool the
            // player cannot put down**, so this is not a convenience.
            // ⚠️ Correctly shadowed while a moment panel is up: that branch early-returns above
            // this switch, and Esc there means "dismiss", which is the nearer meaning.
            // ⭐ AND WITH NOTHING IN HAND, ESC CLOSES *WHAT'S HERE* (D390, Joe: *"the what's here
            // window should disappear with the esc button"*) — the ✕'s own path, so the selection
            // clears with it. A tool in hand goes down first; the next Esc closes the window.
            case Key.Escape:
                if (_map.IsPlacing)
                {
                    _map.PutTheToolDown();
                }
                else if (_whatsHerePanel.Visible)
                {
                    CloseTheWindow(_whatsHerePanel);
                }

                break;

            // The brush's shape, beside its own tools rather than in Settings (Joe's call). The
            // button on the filter row says the same thing; a key is there because sizing with
            // the wheel and shaping with the mouse would be two hands for one brush.
            case Key.B: _map.CycleBrushShape(); break;

            // H hides the furniture, C rolls it up. Two keys because they answer two different
            // wants: "get out of the way, I am watching" and "I need more room to work".
            case Key.H: ToggleFurniture(); break;
            case Key.C: ToggleAllPanels(); break;
        }
    }

    /// <summary>
    /// Cycle how much explanation the map draws: nothing, the selected villager, or
    /// everybody.
    /// </summary>
    private void CycleDetail()
    {
        _detail = _detail switch
        {
            MapDetail.Off => MapDetail.Selected,
            MapDetail.Selected => MapDetail.All,
            _ => MapDetail.Off,
        };

        _detailButton.Text = _detail switch
        {
            MapDetail.Off => "Routes: off",
            MapDetail.Selected => "Routes: selected",
            _ => "Routes: all",
        };
    }

    /// <summary>Switch the soil overlay, and say so on the button that switched it.</summary>
    /// <remarks>
    /// <b>⛔ THE LABEL USED TO BE WRITTEN IN <see cref="CycleDetail"/>, WHICH IS THE *ROUTES*
    /// BUTTON'S HANDLER</b>, so pressing Ground flipped the overlay and left the button
    /// insisting <em>"Ground: off"</em> until the player happened to press Routes or Tab.
    /// Joe: <em>"it stays as 'off' regardless."</em> The overlay had been working the whole
    /// time; the only feedback the control had contradicted what it did.
    /// <para>
    /// The fix is the rule <see cref="CycleDetail"/>'s own comment already states — one place
    /// writes the text, and every caller goes through it — so the label cannot drift from the
    /// thing it describes again.
    /// </para>
    /// </remarks>
    private void ToggleSoil()
    {
        _map.ShowSoil(!_map.SoilShown);
        RefreshSoilButton();
    }

    /// <summary>The one place the ground button's text is written.</summary>
    private void RefreshSoilButton() =>
        _soilButton.Text = _map.SoilShown ? "Ground: ON" : "Ground: off";

    /// <summary>Switch the wear overlay (D358), the same shape as <see cref="ToggleSoil"/> for the same reason.</summary>
    private void ToggleWear()
    {
        _map.ShowWear(!_map.WearShown);
        RefreshWearButton();
    }

    /// <summary>The one place the paths button's text is written.</summary>
    private void RefreshWearButton() =>
        _wearButton.Text = _map.WearShown ? "Paths: ON" : "Paths: off";

    /// <summary>
    /// Change playback speed — ticks per real second, never the size of a tick
    /// (decision D4).
    /// </summary>
    /// <summary>
    /// The one panel that sits over the valley rather than beside it.
    /// </summary>
    /// <remarks>
    /// <b>⭐ EVERY OTHER PANEL LIVES IN A COLUMN so that two of them cannot overlap</b> — the rule
    /// D54/D55 arrived at after a growing panel shoved the map sideways. **This one deliberately
    /// breaks that rule**, because it is the only thing in the game that is meant to be in the
    /// way: the village is paused behind it and nothing else is competing for the space.
    /// </remarks>
    private Button? _libraryButton;
    private Button? _townHallButton;

    /// <summary>Which tab of the build bar is showing.</summary>
    private BuildTab _tab = BuildTab.Build;

    /// <summary>Which category the BUILD tab is narrowed to, or null for ALL.</summary>
    private BuildCategory? _filter;

    /// <summary>True once the village can write, so the library has a button.</summary>
    private bool _literacy;

    /// <summary>True once the founders are gone, so the town hall has one (D252).</summary>
    private bool _foundersGone;

    /// <summary>Every button on the strip, with what decides whether it is showing.</summary>
    /// <remarks>
    /// ⭐ <b>Built once and shown or hidden, never rebuilt.</b> Tearing the strip down on every
    /// tab press would make the widths unmeasurable — <see cref="ProbeTheControlBar"/> has to be
    /// able to pose the whole thing at once (`specs/build-bar.md §5.3`) — and it would throw away
    /// the library's tint mid-frame.
    /// </remarks>
    private readonly List<(BuildTab Tab, BuildCategory Category, Button Button, BuildingKind? Kind, ToolMark? Mark)> _strip = new();

    private readonly List<(BuildTab Tab, Button Button)> _tabButtons = new();

    private readonly List<(BuildCategory? Category, Button Button)> _filterButtons = new();

    private HFlowContainer _filterRow = null!;

    private HFlowContainer _stripRow = null!;

    /// <summary>What the tab in front of you does — the row that keeps the bar one height.</summary>
    /// <remarks>
    /// ⛔ <b>It replaces <c>_harvestNote</c>, which lived in the strip row and showed on one tab
    /// of three.</b> That is what made the bar change height as Joe switched tabs. This label
    /// sits in the filter row and is written for every tab, so the row is always exactly one
    /// line tall and the bar never moves under the cursor.
    /// </remarks>
    private Label _tabNote = null!;

    /// <summary>Square or round, for the brush (D327). Sits on the filter row, on every tab.</summary>
    /// <remarks>
    /// ⚠️ Nullable and null-checked in <see cref="RefreshTheStrip"/>, because that method runs from
    /// inside <c>BuildControlPanel</c> before this is assigned — the same ordering hazard
    /// <see cref="PinTheBarHeight"/> guards against with its two-field test.
    /// </remarks>
    private Button? _shapeButton;

    /// <summary>Say which shape the brush is set to, on the button and after every change.</summary>
    private void RelabelTheBrush()
    {
        if (_shapeButton is not null)
        {
            _shapeButton.Text = _map.BrushShapeInHand == BrushShape.Round
                ? "Brush: round"
                : "Brush: square";
        }
    }

    /// <summary>
    /// Show the library only once the village can write, and glow while the gift is unspent.
    /// </summary>
    /// <remarks>
    /// <b>⭐ A BUTTON YOU HAVE BEEN LOOKING AT FOR EIGHTEEN YEARS IS NOT A SURPRISE WHEN IT
    /// UNLOCKS</b> (Joe, from play). Hiding it until literacy is what makes the moment land as a
    /// gift rather than as permission. **The tint clears when the free one is spent**, so the
    /// highlight means *"this is the gift"* rather than *"this is a library"*.
    /// </remarks>
    private void RefreshTheLibraryButton(SimWorld world)
    {
        if (_literacy != world.HasLiteracy)
        {
            _literacy = world.HasLiteracy;
            RefreshTheStrip();
        }

        if (_libraryButton is null)
        {
            return;
        }

        _libraryButton.Modulate = world.AFreeLibraryIsOwed
            ? new Color(1f, 0.85f, 0.4f)
            : Colors.White;

        // ⭐ THE NAME COMES FROM THE CATALOGUE AND THE STAR IS ADDED TO IT, rather than the word
        // "Library" being typed here. D240 is why: the build bar said *"gatherer's hut"* for a
        // year after the trade became *forager*, because the bar held its own copy of the word.
        _libraryButton.Text = world.AFreeLibraryIsOwed
            ? Titled(world.BuildingsCatalog.NameOf(BuildingKind.Library)) + " ★"
            : Titled(world.BuildingsCatalog.NameOf(BuildingKind.Library));
    }

    /// <summary>
    /// Show the town hall only once the founders are gone, and glow while the gift is unplaced.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>⭐ THE SAME THREE RULES AS THE LIBRARY BUTTON, FOR THE SAME REASON</b> (Joe, from play,
    /// about the library: <i>"the library is in the UI from the beginning — shouldn't it show up
    /// once gifted?"</i>). A button you have been looking at for fifty years is not a surprise
    /// when it unlocks, and the tint means <em>"this is the gift"</em> rather than <em>"this is a
    /// town hall"</em>.
    /// </para>
    /// <para>
    /// <b>⛔ IT DOES NOT GO AWAY ONCE THE HALL STANDS</b>, and the reason is the singleton refusal
    /// (D38): hiding the button would leave a player who pulled theirs down with no way to raise
    /// another, and a control that vanishes is harder to reason about than one that says no.
    /// <b>Pressing it while one stands gets the sentence</b> — <em>"there is only ever one"</em> —
    /// which is `SimWorld.CanBuildAt`'s job, not this method's.
    /// </para>
    /// <para>
    /// ⚠️ <b>The Civic group appears mid-play, which is the failure mode D242 cost an evening on.</b>
    /// The bottom bar grows by a whole category the year the founders die, exactly as it did the
    /// year the village learned to write — and that bug was invisible because <em>every look
    /// anybody takes at the UI is a look at a young village.</em> The bar wraps now
    /// (<c>HFlowContainer</c> + <c>spanWidth</c>), so this is a second category it has to absorb
    /// rather than a second bug. <b>Ask what this bar looks like in year sixty.</b>
    /// </para>
    /// </remarks>
    private void RefreshTheTownHallButton(SimWorld world)
    {
        if (_foundersGone != world.SaidTheFoundersAreGone)
        {
            _foundersGone = world.SaidTheFoundersAreGone;
            RefreshTheStrip();
        }

        if (_townHallButton is null)
        {
            return;
        }

        _townHallButton.Modulate = world.ATownHallIsOwed
            ? new Color(1f, 0.85f, 0.4f)
            : Colors.White;

        _townHallButton.Text = world.ATownHallIsOwed
            ? Titled(world.BuildingsCatalog.NameOf(BuildingKind.TownHall)) + " ★"
            : Titled(world.BuildingsCatalog.NameOf(BuildingKind.TownHall));
    }

    private PanelContainer? _momentPanel;
    private Label _momentTitle = null!;
    private Label _momentBody = null!;
    /// <summary>The speed the player had set before an alert slowed the village down.</summary>
    /// <remarks>
    /// <para>
    /// <b>⭐⭐ AN ALERT SLOWS THE VILLAGE TO 1×; IT DOES NOT STOP IT</b> (Joe, 2026-08-30):
    /// *"they should both slow the game down to 1x. If it was already at 1x no change. Presently
    /// gifts pause the game; slow it down to 1x only … after acknowledging the alert, the game
    /// goes back to whatever speed the user was at before the alert."*
    /// </para>
    /// <para>
    /// ⛔ <b>WHAT THAT REPLACES, AND WHY THE OLD ARGUMENT NO LONGER HOLDS.</b> The gift modal used
    /// to pause outright, on the reasoning that *"at 4× or 10× an unpaused panel slides past
    /// unread"*. **A panel does not slide past at 1×** — that was an argument against 10×, and it
    /// is answered by slowing down rather than by stopping. ⭐ Slowing keeps §1.2's promise
    /// (interruptions remove time pressure, never add it) **without taking the village away from
    /// the player mid-look**, which is what a hard pause does to a discovery worth watching.
    /// </para>
    /// <para>
    /// ⚠️ <b>ONE HOLD SHARED BY BOTH KINDS OF ALERT, AND THAT IS NOT TIDINESS.</b> A passing
    /// banner can still be up when a gift modal arrives. Two independent remember-and-restore
    /// pairs would have the second one record **1×** as *"the speed the player was at"* and hand
    /// that back — quietly stealing the 10× they actually chose. The first alert to slow the
    /// village takes the hold; the last one to leave gives it back.
    /// </para>
    /// </remarks>
    private double _speedBeforeTheAlert = 1.0;

    /// <summary>True while an alert is holding the village at 1×.</summary>
    private bool _slowedForAnAlert;

    /// <summary>The passing moment — celebrated, but the village keeps working.</summary>
    private PanelContainer? _passingPanel;
    private Label _passingTitle = null!;
    private Label _passingBody = null!;

    /// <summary>When the passing moment fades, in view-side milliseconds.</summary>
    /// <remarks>
    /// <b>⚠️ WALL-CLOCK, AND THAT IS ALLOWED HERE BECAUSE IT IS THE VIEW.</b> The sim may never
    /// read a clock (`DESIGN.md §3`), but how long a banner stays on screen is a fact about the
    /// person reading it, not about the village — the same licence the speed control already
    /// takes. **Nothing in the sim knows this number exists.**
    /// </remarks>
    private ulong _passingUntilMsec;

    /// <summary>How long a passing moment stays up — long enough to read twice.</summary>
    private const ulong PassingMomentMsec = 14_000;

    /// <summary>Bodies of moments already raised, so the log can pick them out in colour.</summary>
    /// <remarks>
    /// <b>⭐ The colour comes from the moment, not from parsing the sentence</b> (Joe wanted the
    /// discovery *"a different font color in the village log"*). The sim writes every moment's
    /// body to the log as well as raising it, so the two are the same string by construction —
    /// which means the view can highlight it without the sim growing a presentation field, and
    /// without `LogEntry` changing shape and moving the audit trail.
    /// <para>
    /// ⚠️ <b>Drained before the log is appended in the same frame</b> (`Refresh` calls
    /// `ShowAnyMoment` first), so a moment's own line is always already known by the time it is
    /// drawn. That ordering is load-bearing — swap the two calls and every celebration renders
    /// plain for one frame and then never again.
    /// </para>
    /// </remarks>
    private readonly HashSet<string> _celebrated = new(StringComparer.Ordinal);

    /// <summary>
    /// Stop the village and show the next thing worth stopping for, if there is one.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>⭐⭐ IT SLOWS THE VILLAGE TO 1× RATHER THAN PAUSING IT</b> (Joe, 2026-08-30 — see
    /// <c>_speedBeforeTheAlert</c>). The 2026-08-26 original paused outright, on the reasoning
    /// that *"at 4× or 10× an unpaused panel slides past unread"*. **That is an argument against
    /// 10×, and slowing down answers it** — a panel does not slide past at 1× — while leaving the
    /// player the village they were watching. §1.2's test is still met: the interruption removes
    /// time pressure, it does not add any.
    /// </para>
    /// <para>
    /// <b>⭐ It restores the speed the player was at, not 1×.</b> Resuming slower than they left is
    /// a small theft of a setting they chose, and the sort of thing that makes an interruption feel
    /// like an imposition.
    /// </para>
    /// <para>
    /// ⚠️ <b>One at a time, and the queue keeps the rest.</b> Two gifts in one tick would otherwise
    /// stack panels; the second waits for the first to be dismissed, which is also why the sim
    /// hands over a list rather than an event.
    /// </para>
    /// </remarks>
    private void ShowAnyMoment(SimWorld world)
    {
        // ⭐⭐ PASSING MOMENTS ARE DRAINED FIRST AND ALL AT ONCE, AND NEITHER HALF OF THAT IS
        // INCIDENTAL (2026-08-27). They must not queue behind a gift waiting to be dismissed —
        // a player who leaves the library modal up for a minute would otherwise collect a
        // backlog of discoveries that then fire one per frame. And every body has to reach
        // `_celebrated` this frame, or its own log line renders plain and never gets another
        // chance.
        for (int i = world.Moments.Count - 1; i >= 0; i--)
        {
            Moment passing = world.Moments[i];
            if (passing.WaitsToBeDismissed)
            {
                continue;
            }

            world.Moments.RemoveAt(i);
            _celebrated.Add(passing.Body);

            // ⚠️ THE NEWEST WINS RATHER THAN A QUEUE. Two discoveries in one season is already
            // unusual; showing them in sequence would hold the second banner up long after its
            // news went stale, and the log has both regardless — a moment is never the only
            // surface (`Moment` remarks).
            _passingTitle.Text = passing.Title;
            _passingBody.Text = passing.Body;
            _passingPanel!.Visible = true;
            _passingUntilMsec = Time.GetTicksMsec() + PassingMomentMsec;

            // ⭐ A DISCOVERY SLOWS THE VILLAGE TOO, WHICH IT NEVER USED TO (Joe, 2026-08-30):
            // *"technique alerts dont affect game speed; change that to slow down to 1x."* At 10×
            // the fourteen seconds this banner is up are two and a half village years, and the
            // news it carries — somebody worked something out — is exactly the kind of thing
            // §1.2 says the player should get to look at.
            SlowDownForAnAlert();
        }

        if (_passingPanel is { Visible: true } && Time.GetTicksMsec() >= _passingUntilMsec)
        {
            _passingPanel.Visible = false;

            // The banner fading IS the acknowledgement — there is no button on it.
            LetTheVillageBackUpToSpeed();
        }

        if (_momentPanel is { Visible: true })
        {
            return;
        }

        for (int i = 0; i < world.Moments.Count; i++)
        {
            if (!world.Moments[i].WaitsToBeDismissed)
            {
                continue;
            }

            Moment moment = world.Moments[i];
            world.Moments.RemoveAt(i);
            _celebrated.Add(moment.Body);

            _momentTitle.Text = moment.Title;
            _momentBody.Text = moment.Body;

            // ⭐⭐ SLOWED TO 1×, NOT PAUSED (Joe, 2026-08-30). See `_speedBeforeTheAlert`.
            //
            // ⛔ THE THREE CASES THAT USED TO NEED THEIR OWN BRANCHES ARE NOW ONE SENTENCE, and
            // that is the point of putting the rule in `SlowDownForAnAlert`: it only ever slows
            // down. **A paused village stays paused** — which is what `SkipYears` needs and what
            // the old `_stayPausedAfterTheMoment` flag existed to arrange — a village at 1× is
            // untouched, and only a village running faster than the player can read gives
            // anything up, and gets it straight back when the panel closes.
            SlowDownForAnAlert();

            _momentPanel!.Visible = true;
            return;
        }
    }

    /// <summary>The banner for a moment the village does not stop for.</summary>
    /// <remarks>
    /// <b>⭐ TOP-CENTRE, NOT CENTRE.</b> A panel over the middle of the map while the village is
    /// still moving covers the thing the player is watching; the gift modal may do that because
    /// it has stopped the world and there is nothing behind it to see.
    /// </remarks>
    private void BuildThePassingPanel()
    {
        var panel = new PanelContainer { Visible = false };
        panel.SetAnchorsPreset(LayoutPreset.CenterTop, keepOffsets: false);
        panel.GrowHorizontal = GrowDirection.Both;
        panel.GrowVertical = GrowDirection.End;
        panel.OffsetTop = 24f;
        panel.CustomMinimumSize = new Vector2(460f, 0f);

        // ⚠️ It must not eat clicks meant for the map behind it. `Stop` is the default for a
        // PanelContainer, and a banner nobody asked for stealing a build click would be a worse
        // bug than the one this feature fixes.
        panel.MouseFilter = MouseFilterEnum.Pass;

        var box = new VBoxContainer();
        box.AddThemeConstantOverride("separation", 8);

        _passingTitle = Heading(string.Empty);
        box.AddChild(_passingTitle);

        _passingBody = Wrapped(Body(string.Empty));
        _passingBody.CustomMinimumSize = new Vector2(420f, 0f);
        box.AddChild(_passingBody);

        var go = new Button { Text = "Good" };
        go.Pressed += () =>
        {
            if (_passingPanel is not null)
            {
                _passingPanel.Visible = false;
            }
        };
        box.AddChild(go);

        panel.AddChild(box);
        AddChild(panel);
        _passingPanel = panel;
    }

    private void BuildTheMomentPanel()
    {
        // ⚠️ CENTRED PROPERLY, WHICH IT WAS NOT (Joe, from play: *"it should be centered"*).
        // `LayoutPreset.Center` anchors the middle of the control to the middle of the screen and
        // then leaves the offsets where they were — so a panel that grows to fit its text drifts
        // off to one side, which is what the screenshot showed. **Growing both ways from the
        // centre is what `Center` sounds like it does and does not.**
        //
        // ⚠️ Draggable is Joe's other half and is NOT built — he said the UI pass comes later, so
        // this is the centring only. **Recorded rather than silently dropped.**
        var panel = new PanelContainer { Visible = false };
        panel.SetAnchorsPreset(LayoutPreset.Center, keepOffsets: false);
        panel.GrowHorizontal = GrowDirection.Both;
        panel.GrowVertical = GrowDirection.Both;
        panel.CustomMinimumSize = new Vector2(460f, 0f);

        var box = new VBoxContainer();
        box.AddThemeConstantOverride("separation", 10);

        _momentTitle = Heading(string.Empty);
        box.AddChild(_momentTitle);

        _momentBody = Wrapped(Body(string.Empty));
        _momentBody.CustomMinimumSize = new Vector2(420f, 0f);
        box.AddChild(_momentBody);

        var go = new Button { Text = "Go on" };
        go.Pressed += DismissTheMoment;
        box.AddChild(go);

        panel.AddChild(box);
        AddChild(panel);
        _momentPanel = panel;
    }

    private void DismissTheMoment()
    {
        if (_momentPanel is null || !_momentPanel.Visible)
        {
            return;
        }

        _momentPanel.Visible = false;
        LetTheVillageBackUpToSpeed();
    }

    /// <summary>Bring the village down to 1× for an alert, remembering where it was.</summary>
    /// <remarks>
    /// <para>
    /// <b>⛔ IT ONLY EVER SLOWS DOWN.</b> A player at 1× sees no change, which is Joe's own
    /// condition; a player who is <b>paused</b> stays paused, and that is what keeps `Skip 1y` /
    /// `Skip 10y` honest — a skip stops early on a moment and pauses, and starting the village
    /// running the instant they read the thing they skipped to is the complaint the skip exists
    /// to answer.
    /// </para>
    /// <para>
    /// ⚠️ <b>The first alert to take the hold keeps it.</b> A second one arriving while the
    /// village is already held must not overwrite the remembered speed with the 1× the first one
    /// imposed.
    /// </para>
    /// </remarks>
    private void SlowDownForAnAlert()
    {
        if (_slowedForAnAlert || _driver.IsPaused || _driver.SpeedMultiplier <= 1.0)
        {
            return;
        }

        _speedBeforeTheAlert = _driver.SpeedMultiplier;
        _slowedForAnAlert = true;
        SetSpeed(1.0);
    }

    /// <summary>Give the player their speed back, once no alert is still holding it.</summary>
    /// <remarks>
    /// <b>⚠️ BOTH PANELS ARE ASKED, NOT JUST THE ONE BEING CLOSED.</b> Dismissing a gift while a
    /// discovery banner is still on screen would otherwise jump straight back to 10× and leave
    /// the banner to slide past unread — the exact failure the slowdown exists to prevent.
    /// </remarks>
    private void LetTheVillageBackUpToSpeed()
    {
        if (!_slowedForAnAlert
            || _momentPanel is { Visible: true }
            || _passingPanel is { Visible: true })
        {
            return;
        }

        _slowedForAnAlert = false;
        SetSpeed(_speedBeforeTheAlert);
    }

    private void SetSpeed(double multiplier)
    {
        // A stopped village stays stopped (D364): nothing sound is left to run.
        if (_halted)
        {
            return;
        }

        _driver.SpeedMultiplier = multiplier;
        _speedLabel.Text = _driver.IsPaused ? "PAUSED" : $"{_driver.SpeedMultiplier:0.#}x";
    }

    // ---------------------------------------------------------------
    //  Rendering
    // ---------------------------------------------------------------

    private void Refresh()
    {
        SimWorld world = _loop.World;

        ShowAnyMoment(world);
        RefreshTheLibraryButton(world);
        RefreshTheTownHallButton(world);

        RefreshCards(world);

        ShowTheFrameCost();
        CentreSettingsIfItJustOpened();

        // WHO IS HERE, BROKEN DOWN BY LIFE STAGE (Joe's area 1, on the villagers bar since D378).
        // "17 villagers" is the number; "11 adults and 4 children" is the one that tells you
        // whether the village is growing or ageing out, which is the question a generational
        // game is about. Counted here rather than on the world: the roster already walks this
        // list every frame, a village is tens of people, and a sim reader would be a second way
        // of asking the same question.
        int adults = 0;
        int children = 0;
        int elders = 0;
        for (int i = 0; i < world.Villagers.Count; i++)
        {
            Villager person = world.Villagers[i];
            if (!person.Alive)
            {
                continue;
            }

            switch (person.LifeStage)
            {
                case LifeStage.Child: children++; break;
                case LifeStage.Elder: elders++; break;
                default: adults++; break;
            }
        }

        _populationCell.Text = world.Population.ToString();
        _adultsCell.Text = adults.ToString();
        _childrenCell.Text = children.ToString();
        _eldersCell.Text = elders.ToString();
        _laborersCell.Text = world.Laborers.ToString();
        _clockLabel.Text = $"{world.Name}   ·   {world.Clock}   ·   {LivingHouseholds(world)} households";
        _seedLabel.Text = TheRunLine(world);

        // The Settings ticks read the windows' state rather than remembering what they were set
        // to (D380): a ✕ on a panel writes `Wanted`, and the tick follows next frame.
        for (int i = 0; i < _windows.Count; i++)
        {
            _windows[i].Tick?.SetPressedNoSignal(_windows[i].Wanted);
        }

        // WHAT IS IN THE STORES, one cell per good (D83). Totals across every granary and
        // warehouse, not the first of each (D38) — a village that has built a second one should
        // see what is in it.
        int onTheGround = 0;
        var whyOnTheGround = new List<string>();
        for (int i = 0; i < _goodsReadouts.Count; i++)
        {
            (Goods goods, Label held) = _goodsReadouts[i];
            int inStores = world.InStores(goods);

            // ⭐ AND WHAT IS LYING IN THE YARD (D134). A valley has one timber store, it fills,
            // and everything hauled in after that is set down outside it — measured at 320 logs
            // in store against 5,977 on the ground. Reading "Logs 320" while a mountain sits in
            // the open is the village lying to the player about a shortage it does not have.
            // ⛔ AND IT USED TO ASSERT A CAUSE IT NEVER TESTED (2026-08-27). The string was
            // hard-coded to "no room in store" and fired on `OnTheGround > 0` alone. Joe read
            // "Stone 0 (+12 on the ground — no room in store)" while a stone-filtered pile stood
            // empty beside it, and reasonably reported an impossible village — the panel was
            // simply naming the wrong reason. **The real one was that the store REFUSED it.**
            //
            // Full and unwilling are different states with different remedies — empty a store,
            // versus change what it takes — so the sentence has to tell them apart.
            // ⚠️ `Grouped()` on every good, not just food. Four figures of stone read as "1968"
            // in one row and "1,968" in another, which is the panel looking unfinished for no
            // reason anybody chose.
            // ⛔⛔ THE NUMBER ALONE, IN A CELL THAT CANNOT WIDEN (D367). This cell used to carry
            // the parenthetical — `132  (+4 on the ground — still to be carried in)` — and a
            // Label's minimum width is its text, so the whole panel grew and shrank as heaps came
            // and went. Joe: *"I hate how the size of the panels change when information
            // updates."* The heaps go to the permanent row under `more ▾`; the sentence is its tooltip.
            int inHeaps = world.OnTheGround(goods);
            held.Text = inStores.Grouped();
            if (inHeaps > 0)
            {
                onTheGround += inHeaps;
                whyOnTheGround.Add($"{inHeaps.Grouped()} {GoodsName(world, goods)} — {WhyItIsOnTheGround(world, goods)}");
            }

            // ⭐ AMBER WHILE THE VILLAGE IS SHORT OF IT (D378) — by the trade's own reckoning, so
            // the bar says what the Professions panel is about to staff for.
            switch (goods)
            {
                case Goods.Firewood:
                    int woodcutters = LabourQuota.WoodcuttersWanted(world);
                    ShowShortfall(held, woodcutters > 0,
                        $"the village is short of firewood — {woodcutters} on splitting it would cover the winter");
                    break;
                case Goods.Logs:
                    int foresters = LabourQuota.ForestersWanted(world);
                    ShowShortfall(held, foresters > 0,
                        $"the village is short of logs — {foresters} on felling would cover what is waiting to be built");
                    break;
                case Goods.Tools:
                    // ⭐ Tools have a quota since D391 (they had none when D378 ruled *"stone and
                    // tools never go amber"*): short when the hands that use one, and a spare
                    // each, outnumber what the stores and the hands hold.
                    int smiths = LabourQuota.SmithsWanted(world);
                    ShowShortfall(held, smiths > 0,
                        $"the village is short of tools — {smiths} at a forge would cover the hands that use them");
                    break;
            }
        }

        // The umbrella, split the way the old Food row was: what the stores hold, and what is out
        // in the larders behind it — on its OWN row now, always present, so the popup's width and
        // height are the same whether the larders are full or empty (D367).
        int foodInStores = world.FoodInGranaries();
        int foodElsewhere = world.TotalFood() - foodInStores;
        _foodTotal.Text = foodInStores.Grouped();
        ShowShortfall(_foodTotal, world.TheVillageWantsMoreFood(),
            $"the village is short of food — it holds {world.FoodTheVillageHolds().Grouped()} and wants "
            + $"{(world.StockLimits.For(Goods.Produce) ?? world.TargetFoodForTheGranary()).Grouped()}");
        _foodElsewhere.Text = foodElsewhere > 0 ? $"+{foodElsewhere.Grouped()}" : "—";
        _onTheGround.Text = onTheGround > 0 ? $"+{onTheGround.Grouped()}" : "—";
        _onTheGround.TooltipText = string.Join("\n", whyOnTheGround);
        _onTheGroundLabel.TooltipText = _onTheGround.TooltipText;

        // What each limited good actually stands at, beside the number the player set —
        // so "nobody is splitting logs" and "you asked for 200 and there are 214" are the
        // same glance rather than two.
        for (int i = 0; i < _stockLimitReadouts.Count; i++)
        {
            (Goods goods, Label held) = _stockLimitReadouts[i];

            // ⭐ THE ROW SAYS WHETHER A LIMIT IS ACTUALLY IN FORCE (D139), and it did not.
            //
            // Joe: *"the woodcutter keeps making firewood way past the limit."* He was reading
            // a spin box that said 200 beside a stock of 570 and concluding the sim ignored it.
            // The sim was obeying perfectly — **there was no limit**. `SetStockLimit` is called
            // from `ValueChanged`, so a row the player never touches shows its default number
            // while the good is uncapped. He typed 2000 into Food, so Food bound; he left
            // Firewood on its default, so Firewood was free.
            //
            // A number displayed as though it were a rule, which is not one, is the panel
            // lying — and it is a regression I introduced removing the "village decides" tick,
            // because that tick was the thing that used to say "this number is not in force".
            // Read from the sim rather than from the widget: the label cannot drift from the
            // state it describes.
            int? limit = world.StockLimits.For(goods);
            held.Text = limit is null
                ? $"no limit · have {HeldFor(world, goods)}"
                : $"stop at {limit.Value} · have {HeldFor(world, goods)}";
        }

        // The same glance for the professions: how many are actually on this work, and how
        // many places there are to be on it (D106). "0 of 2" is Joe's screenshot, and it is
        // what tells you whether asking for three would achieve anything.
        // ⚠️ AND IT SAYS "WORKING", BECAUSE THE COLUMN HELD TWO MEANINGS AT ONCE (Joe, D148).
        // He read *"Laborer 1"* against four villagers he had assigned to four jobs and asked
        // why one was spare. The sim was right: the number in the − N + box is what he ASKED
        // for, and the row beside it is who actually turned up — his woodcutter row said
        // *"1"* in the box and *"0 of 2"* next to it, because firewood was at its limit. Two
        // different meanings in one column, and the Laborer row's number is a third: a count.
        //
        // **That is D139's bug one panel over** — a number that reads like a fact and is not —
        // and the fix is the same one: say the word. "0 of 2" becomes "nobody working of 2
        // seats", and where the player's number is not being met the row says so out loud
        // rather than leaving them to subtract.
        LabourQuota quota = LabourQuota.For(world);
        for (int i = 0; i < _professionReadouts.Count; i++)
        {
            (JobKind kind, Label maximum, Label name) = _professionReadouts[i];

            int working = WorkingAt(world, kind);
            int seats = SeatsFor(world, kind);
            int? asked = world.JobLimits.For(kind);

            // The MAX half of the ASSIGNED / MAX cell. It cannot be written once at construction:
            // seats appear and vanish as the player raises buildings and pulls them down.
            maximum.Text = $"/ {seats}";

            string row = $"Wants: {quota.For(kind)}";

            // ⭐⭐ HOW MANY ARE ACTUALLY WORKING, AND ONLY WHEN IT DIFFERS FROM WHAT WAS ASKED (Joe,
            // 2026-09-05). His mockup dropped this number, and it is the one he has already had to
            // ask for once: D270/D271 exist because a hut with somebody in it read as unstaffed —
            // *"it IS staffed and somebody DOES work there, even if there is presently no demand."*
            //
            // ⚠️ Silent when they agree, which is nearly always. A row that says "1 working" beside
            // "1 assigned" every frame teaches the player to stop reading the column, and then the
            // one time it matters they will not see it either.
            if (asked is int wanted && wanted != working)
            {
                row += $" · {working} working of {wanted} asked";
            }

            // ⛔⛔ NO ALERTS ON THIS PANEL (D379, Joe: *"the alert is on again and it doesn't make
            // sense. remove professions panels alerts altogether."*). Two clauses used to follow —
            // *"⚠ {why the village wants none}"* (D147's why, when the player asked for hands the
            // village would not use) and *"⚠ needs N, build another X"* (D274's seat-cap
            // shortage, gated by D374, re-based by D375) — with a ⚠ on the trade's name while
            // either stood. He read *"needs 3, build another forager's hut"* beside 1,875 food a
            // third time and the words were true by the quota and wrong to him, which is the
            // test. The row is what he asked for and what turned up; the sentence stays as the
            // name's tooltip, plain. The numbers behind the deleted clauses are still the sim's
            // (`LabourQuota.Needed`, `WhyTheVillageWantsNone`) and still guarded there.
            name.Text = ProfessionName(world, kind);
            name.TooltipText = row;
        }

        // And what the 1 is one OF, which is the whole of Joe's question.
        _laborerReadout.Text =
            $"Able adults: {world.AbleAdults}   ·   Laborers (unassigned): {world.Laborers}";

        // The two standing alerts used to be composed here every frame and shown in the
        // overview. They are narrated by the sim on their edges now and read in the village
        // log like everything else that happens (Joe) — so the view no longer asks the
        // question at all.
        //
        // ⚠️ ~~which also takes a `LabourQuota.For` off every single frame~~ — WRONG, corrected
        // 2026-08-28. That was true of the ALERTS and false of this method: the professions panel
        // a few lines up still calls `LabourQuota.For(world)` every frame, and `_Process` calls
        // `Refresh()` unconditionally, paused or not. **D220's shape in a performance claim** —
        // true of one path, written as though true of the method.
        //
        // ⭐ And it is not a cheap call: `LabourQuota.For` walks every villager, and
        // `MarketersWanted` loops every household calling `LivingMembersOf`, which loops every
        // villager again. **Left measured rather than fixed** — it is a real cost and changing
        // when the quota is computed is a behaviour change, not housekeeping.

        RefreshRoster(world);
        RefreshInspector(world);
        AppendNewLogLines();

        // After the panels have been filled, because how tall a column wants to be depends on
        // what was just put in it — an alert that grew to six lines this tick included.
        FitColumns();
        FitFloaters();

        // Alpha is the fraction of a tick elapsed, so villagers glide between tiles
        // instead of teleporting once a second.
        _map.Present(world, _driver.Alpha, _selectedVillagerId, _selectedTile, _detail);

        // After the map, because the box it draws is the map's own camera and asking for it
        // before the map has been told what frame this is would box the previous one.
        _minimap.Present(world, _map.VisibleTiles);
    }

    private void RefreshRoster(SimWorld world)
    {
        // Rebuilt each frame rather than diffed. A village is tens of people, not
        // thousands, and a rebuilt list cannot drift out of sync with the sim —
        // which matters more here than the handful of allocations.
        int previousSelection = _selectedVillagerId;
        _roster.Clear();

        for (int i = 0; i < world.Villagers.Count; i++)
        {
            Villager villager = world.Villagers[i];
            if (!villager.Alive)
            {
                continue;
            }

            string stage = villager.LifeStage switch
            {
                LifeStage.Child => "child",
                LifeStage.Elder => "elder",
                _ => "adult",
            };

            // WHAT THEY ARE, NOT WHAT THEY ARE DOING RIGHT NOW (D80).
            //
            // This said "working" for anybody who HELD a job, while the panel two feet away
            // said "resting at home" about the same person on the same tick. Both were true
            // and the pair was a lie: Joe read them side by side and reasonably concluded
            // something was broken. One word cannot answer two questions.
            //
            // The roster answers "who is this?" — their trade — and the panel answers "what
            // are they doing?". Exactly the distinction the vacancy alert settled: holding a
            // job and being at it this instant are different facts.
            string work = villager.IsLaborer ? "laborer"
                : villager.HasJob ? TradeOf(villager)
                : "not working yet";
            int index = _roster.AddItem($"{villager.Name}, {villager.AgeYears} ({stage}) — {work}");
            _roster.SetItemMetadata(index, villager.Id);

            if (villager.Id == previousSelection)
            {
                _roster.Select(index);
            }
        }
    }

    private void RefreshInspector(SimWorld world)
    {
        // The docked panel is for what has no card (D377): hidden the moment the selection has one.
        bool carded = _selectedVillagerId != 0
            || (_selectedTile is GridPos at
                && (world.StoreAt(at) is not null || world.WorkplaceCovering(at) is not null || world.HouseholdAt(at) is not null));
        // ⚠️ And only while the player wants the window at all (its Settings tick, D380) and the
        // furniture is shown (`h`) — this line used to override both every frame.
        _whatsHerePanel.Visible = _furnitureShown
            && (WindowOf(_whatsHerePanel)?.Wanted ?? true)
            && !carded && (_selectedTile is not null);

        if (_selectedTile is GridPos tile)
        {
            _inspector.Text = DescribeWhatIsAt(world, tile);
            return;
        }

        _inspector.Text = string.Empty;
    }

    private void OnVillagerSelected(long index)
    {
        Variant metadata = _roster.GetItemMetadata((int)index);
        SelectVillager(metadata.AsInt32());
    }

    /// <summary>
    /// The player clicked a person on the map — <b>which has never worked until now</b>.
    /// </summary>
    /// <remarks>
    /// Straight into the same selection the roster sets, so there is one selected villager
    /// and not two: the roster highlights whoever you clicked on the map, the inspector
    /// describes them, and the map draws their route. Two ways in, one answer — which is the
    /// distinction the roster and the panel got wrong in D80 and is worth not repeating.
    /// </remarks>
    private void OnVillagerClicked(int villagerId) => SelectVillager(villagerId);

    private void SelectVillager(int villagerId)
    {
        _selectedVillagerId = villagerId;

        // Clearing the tile is what makes the inspector describe the person rather than
        // the doorstep they are standing on: RefreshInspector reads the tile first.
        _selectedTile = null;

        // ⭐ AND THE PERSON GETS A CARD (D376) — the card is what you read now.
        if (villagerId != 0 && _loop.World.FindVillager(villagerId) is { Alive: true })
        {
            OpenCard(new CardSubject(CardKind.Villager, villagerId));
        }

        RefreshInspector(_loop.World);
    }

    /// <summary>The player clicked the map while not placing anything.</summary>
    private Workplace? SelectedWorkplace()
    {
        if (_selectedTile is not GridPos tile)
        {
            return null;
        }

        // A standing workplace first, then a site — because the market is a store and a
        // workplace at one position (D36's seam) and a finished building is the more likely
        // thing the player meant.
        //
        // ⚠️ A site is still SELECTABLE — it has a queue position and a materials line to
        // read — but it is no longer STAFFABLE (D108). The caller decides which of those it
        // is asking about; this only decides what the player clicked on.
        // ⭐ Through the sim's own finders (D328), not a fourth copy of the loop. Standing first,
        // then whatever else covers the tile — which can only be a site, because nothing may be
        // raised on ground something already stands on.
        return _loop.World.StandingWorkplaceCovering(tile) ?? _loop.World.WorkplaceCovering(tile);
    }

    /// <summary>Nudge the selected workplace's staffing, or clear the number you set.</summary>
    /// <remarks>
    /// Buttons rather than a spinner, because the numbers are small and a click is
    /// cheaper to reach for than a text field.
    /// <para>
    /// <b>⛔ There is no way back to "untouched" and that is deliberate</b> (Joe, 2026-08-16).
    /// The row briefly carried a "Village decides" button and then a "Clear" one; both are
    /// gone, because the whole idea is. An untouched building is staffed by everyone who fits
    /// — a fact about the building — and the moment the player states a number, that number is
    /// the answer from then on.
    /// </para>
    /// </remarks>
    /// <summary>Keep the selected villager on a trade, or hand them back.</summary>
    /// <remarks>
    /// <b>Pressing the trade they are already kept on hands them back</b>, which is why there is
    /// no separate "release" control: one button per trade, and the pressed one is the answer to
    /// *"what is this person kept on?"*. ⚠️ The sim owns the decision — this only asks.
    /// </remarks>
    private void TogglePin(JobKind trade)
    {
        Villager? villager = _loop.World.FindVillager(_selectedVillagerId);
        if (villager is null)
        {
            return;
        }

        _loop.World.SetPinnedTrade(villager, villager.PinnedTrade == trade ? null : trade);
        RefreshInspector(_loop.World);
    }

    /// <summary>A bare right-click (D390): <i>What's here</i> for the tile, or closed again on the same tile.</summary>
    private void OnWhatsHereAsked(GridPos tile)
    {
        if (_selectedTile == tile && _whatsHerePanel.Visible)
        {
            CloseTheWindow(_whatsHerePanel);
            return;
        }

        OnBuildingClicked(tile);
    }

    private void OnBuildingClicked(GridPos tile)
    {
        _selectedTile = tile;
        _selectedVillagerId = 0;
        _roster.DeselectAll();

        // ⭐ A CARD FOR WHAT WAS CLICKED (D376): a store, a workplace or a home; bare ground still
        // reads in the Settings panel's line and opens nothing.
        SimWorld world = _loop.World;
        if (world.StoreAt(tile) is StoreBuilding store)
        {
            OpenCard(new CardSubject(CardKind.Store, store.Id));
        }
        else if (world.WorkplaceCovering(tile) is Workplace place)
        {
            OpenCard(new CardSubject(CardKind.Workplace, place.Id));
        }
        else if (world.HouseholdAt(tile) is Household home)
        {
            OpenCard(new CardSubject(CardKind.Household, home.Id));
        }

        RefreshInspector(world);
    }

    // ---------------------------------------------------------------
    //  "What is that?" — the building inspector
    // ---------------------------------------------------------------

    /// <summary>
    /// Everything the sim has standing on one tile, in plain sentences.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Reports every occupant of the tile rather than the first one found.</b> The
    /// market is both a <see cref="StoreBuilding"/> and a <see cref="Workplace"/> at one
    /// position — D36's recorded seam — so a panel that picked one would describe the
    /// market as half of itself. Asking "what is here?" sidesteps the seam instead of
    /// having to know about it.
    /// </para>
    /// <para>
    /// Every branch ends in a sentence a player can act on. A workplace with nobody at
    /// it says so; a store that is full says so; a site that is waiting on logs says how
    /// many. That is non-negotiable 1 applied to the thing this panel exists for — a
    /// building standing idle has always been silent, and silence is the one thing a
    /// legibility-first game cannot do.
    /// </para>
    /// </remarks>
    private static string DescribeWhatIsAt(SimWorld world, GridPos tile)
    {
        var lines = new List<string>();

        // ⭐ A BUILDING IS ITS CARD NOW (D376). This panel keeps the controls; one line says which
        // building they are for. Joe: *"such a mess of stacked sentences i dont even know what to
        // read. Remove text where you can."* Bare ground, the library and the hall still read here.

        // ⭐ THE STORE FIRST, THEN WHO WORKS IT (D350). A market is both, and described stall-first
        // its `Holding:` line — the one sentence a store exists to say — came eleventh, below the
        // fold of a panel that gave no sign there was more. Joe: *"the market doesn't tell how many
        // units of each item are within."* It did; nobody could see it.
        foreach (StoreBuilding store in world.StoreBuildings)
        {
            if (store.Footprint.Covers(tile))
            {
                return string.Empty;
            }
        }

        foreach (Workplace workplace in world.Workplaces)
        {
            if (workplace.Footprint.Covers(tile))
            {
                return string.Empty;
            }
        }

        if (world.HouseholdAt(tile) is not null)
        {
            return string.Empty;
        }

        // ⛔⛔ THE FOURTH LIST, AND LEAVING IT OUT MADE A FINISHED LIBRARY READ AS "OPEN GROUND"
        // (Joe, playing: *"it was constructed as buildings usually are, but no final building
        // showed up upon completion"*). **This method knew about three kinds of thing that can
        // stand on a tile and a library is a fourth** — the same shape as `SomethingStandsAt`,
        // which had the identical hole in the sim half.
        //
        // ⚠️ AND THE BUTTON WAS NOT ENOUGH. The build button shipped with the building on D103's
        // rule — *a feature the player cannot reach does not exist* — and that was checked off as
        // done. **Placeable is not reachable.** A building the player can mark, pay for, watch get
        // built, and then never see is D221's finding for the sixth time, arriving through the one
        // door that had just been declared closed.
        foreach (Library library in world.Libraries)
        {
            if (library.Footprint.Covers(tile))
            {
                DescribeLibrary(world, library, lines);
            }
        }

        // ⛔ THE FIFTH LIST, AND IT IS HERE IN THE SAME COMMIT AS THE BUILDING (D252). The comment
        // directly above records the library shipping built, paid for, watched being raised, and
        // then reading as *"open ground"* — because this method knew about three kinds of thing
        // that can stand on a tile and did not know about a fourth. **A fifth was always going to
        // arrive; this is it.**
        if (world.TownHall is { } hall && world.TownHallCovers(tile))
        {
            DescribeTheTownHall(world, hall, lines);
        }

        if (lines.Count == 0)
        {
            DescribeBareGround(world, tile, lines);
        }

        // ⭐ A HEAP IS SOMETHING HERE (D390, Joe: *"piles of resources on the ground should be
        // clickable and show in the 'what's here' window"*). What was set down on this tile and
        // why it is still here — the reason the overview bar gives, per good.
        foreach (GroundStack heap in world.GroundStacksAt(tile))
        {
            lines.Add($"On the ground: {heap.Amount.Grouped()} {GoodsName(world, heap.Goods)} — "
                + $"{WhyItIsOnTheGround(world, heap.Goods)}.");
        }

        lines.Add(string.Empty);
        lines.Add($"Tile {tile.X}, {tile.Y}");
        return string.Join("\n", lines);
    }

    /// <summary>"1st", "2nd", "3rd", "4th" — for a queue position a player reads aloud.</summary>
    private static string Ordinal(int number)
    {
        int lastTwo = number % 100;
        if (lastTwo is >= 11 and <= 13)
        {
            return $"{number}th";
        }

        return (number % 10) switch
        {
            1 => $"{number}st",
            2 => $"{number}nd",
            3 => $"{number}rd",
            _ => $"{number}th",
        };
    }

    /// <summary>What a library says when you click it — its shelves, and what is on them.</summary>
    /// <remarks>
    /// <b>⭐ THE SHELVES ARE THE WHOLE PANEL, because they are the whole decision.</b> The player is
    /// choosing which techniques outlive the people who worked them out, and *"two of three shelves
    /// used"* is the sentence that makes the choice visible before it bites rather than afterwards.
    /// </remarks>
    private static void DescribeLibrary(SimWorld world, Library library, List<string> lines)
    {
        Separate(lines);

        lines.Add($"{library.Name} — where the village writes things down");
        lines.Add($"Shelves: {library.Records.Count} of {library.Shelves} used");

        if (library.Records.Count == 0)
        {
            lines.Add("Nothing written yet. A master who has worked a trade for twenty "
                + "years works something out, and it is recorded here.");
            return;
        }

        for (int i = 0; i < library.Records.Count; i++)
        {
            // ⭐ THE SHELF SAYS WHO WORKED IT OUT (Joe, 2026-08-29: *"the written technique should
            // source who found the technique. right now it is blank"*). ⚠️ The name is on the
            // record rather than looked up, because by the time anybody reads this shelf that
            // person has usually been dead for decades — which is what the library is FOR.
            LibraryRecord record = library.Records[i];
            string what = world.TechniquesCatalog[record.TechniqueId].Name;

            lines.Add(record.FoundBy.Length > 0
                ? $"  · {what} — worked out by {record.FoundBy}"
                : $"  · {what}");
        }

        if (!library.HasRoom)
        {
            lines.Add("Full. The next technique anybody works out has nowhere to go, and "
                + "will die with them unless another library stands.");
        }
    }

    /// <summary>What the town hall says when you click it — <b>who it is for</b>.</summary>
    /// <remarks>
    /// <para>
    /// <b>⭐⭐ THE FOUNDERS ARE THE WHOLE PANEL, AND THAT IS SLICE 1's ENTIRE CLAIM</b>
    /// (`specs/town-hall.md §6`): <em>standing in the village, it says what it is and who it is
    /// for.</em> The collections, the charts and the knowledge roster are slices 2–4 and none of
    /// them is here — but the tribute is, because the tribute is the reason the building exists.
    /// </para>
    /// <para>
    /// <b>⛔ THE ORDERING OF THE SLICES IS A DEFENCE, NOT AN ACCIDENT.</b> `DESIGN.md §1`'s
    /// non-negotiable most at risk in this building is <em>people, not a spreadsheet</em> — charts
    /// and itemised collections are literally a spreadsheet. **Building the Founders panel first
    /// means the first thing anybody ever sees inside a town hall is four people.**
    /// </para>
    /// </remarks>
    private static void DescribeTheTownHall(SimWorld world, TownHall hall, List<string> lines)
    {
        Separate(lines);

        lines.Add($"{hall.Name} — raised to the people who founded this village");

        int named = 0;
        for (int i = 0; i < world.Villagers.Count; i++)
        {
            Villager founder = world.Villagers[i];
            if (!founder.Founder)
            {
                continue;
            }

            named++;

            // ⚠️ A founder is dead by the time this building can stand — the hall's own trigger is
            // the last of them dying — so this reads their age at death, which `AgeYears` stops
            // advancing at. **Written as a life rather than as a row**: the register that keeps
            // this panel from being a stat block is the same one D195's at-risk line uses.
            lines.Add($"  · {founder.Name}, who lived {Years(founder.AgeYears)} "
                + $"and saw {founder.WintersSurvived} winters here");
        }

        if (named == 0)
        {
            lines.Add("Nobody's names are cut into the lintel, which should not be possible.");
        }

        lines.Add(string.Empty);
        lines.Add("The village keeps its records here. There is nothing to read yet.");
    }

    private static void DescribeBareGround(SimWorld world, GridPos tile, List<string> lines)
    {
        string ground = world.Map.TerrainAt(tile) switch
        {
            Terrain.Water => "The river. Nobody can cross it and nothing can be built on it.",
            Terrain.Forest => "Woodland.",

            // ⭐⭐ THE SAPLING SAYS SO (Joe, 2026-08-25): *"right now it reads as open land until
            // it's woodland with no sapling specific verbiage in between."* It fell through to
            // the `_ =>` arm, so a tile with young trees standing on it described itself as bare.
            //
            // ⚠️ AND IT SAYS THE TWO THINGS THAT MATTER TO A DECISION, not just the name. It
            // cannot be felled — `TerrainRules.Yields` returns null for it — and it is worth
            // nothing to a gatherer's ring, which counts `Forest` only. A player looking at a
            // cleared patch needs to know their wood is on its way and not yet theirs; that is
            // D125's whole argument for making a sapling its own ground rather than a hidden
            // timer, and the inspector was the one place still not making it.
            // ⭐ And the hut is named by the buildings catalogue for the same reason as the
            // workers one screen up: this said *"a gatherer's ring"* while the build button,
            // the roster and the professions panel all said forager.
            Terrain.Sapling => "Young trees, not yet grown. Nothing to fell here yet, "
                + $"and a {world.BuildingsCatalog.NameOf(BuildingKind.GathererHut)}'s ring "
                + "counts it as bare.",

            // The field says which part of its year it is in, because that is the mechanic
            // (`specs/crops-and-orchards.md`) rather than a label on one.
            // ⚠️ Both seams fell through to "Open ground." until D347 — the same hole
            // the sapling line above records being fixed once already.
            Terrain.Rock =>
                $"A stone seam. Each tile gives {world.GoodsCatalog.YieldPerTileOf(Goods.Stone)} "
                + "stone when dug, by whoever is spare, and then it is ground.",
            Terrain.IronDeposit =>
                $"An iron seam. Each tile gives {world.GoodsCatalog.YieldPerTileOf(Goods.Iron)} "
                + "iron when dug, by whoever is spare, and then it is ground.",
            Terrain.Field => "Ploughed field, bare. It will be sown in spring.",
            Terrain.Sown => "A sown field. It will stand ripe in autumn.",
            Terrain.Ripe => "A ripe field, ready to reap. Winter will take what is left standing.",
            _ => "Open ground.",
        };

        lines.Add(ground);

        // ⭐ WHAT THE GROUND IS WORTH, IN WORDS. Until this line existed the soil overlay's
        // wash was the *only* channel the game had for saying so — no panel, no log, no
        // sentence anywhere stated a tile's soil — and Joe walked the shipped build and said
        // he could not tell good ground from bad. A wash is a thing you compare; this is a
        // thing you read, and D67's rule is that going after a site should be a decision
        // rather than a lottery.
        //
        // Not on the river, which grows nothing and is not ground.
        if (world.Map.TerrainAt(tile) != Terrain.Water)
        {
            lines.Add(DescribeSoil(world.SoilShareAt(tile)));
        }

        // ⭐ AND WHETHER PEOPLE WALK HERE (D358). The trail on the map is a thing you compare; this
        // is a thing you read — the same reason the soil got its sentence. Only where it is true:
        // "nobody walks here" on nine thousand tiles would be noise.
        int wear = world.Paths.At(tile);
        if (wear >= world.Config.PathPackedAt)
        {
            lines.Add("A packed trail — walked so often the earth is hard, and quick underfoot.");
        }
        else if (wear >= world.Config.PathWornAt)
        {
            lines.Add("A worn path — the grass has gone where people walk, and the going is easier.");
        }
        else if (wear > 0)
        {
            lines.Add("Trodden a little. Left alone, the grass will have it back by next season.");
        }

        if (world.Zones.IsResidential(tile))
        {
            lines.Add("Painted for housing — the village may build a home here.");
        }
    }

    /// <summary>
    /// What this person has given their working life to — <b>the years, not a number</b>
    /// (`specs/skills-catalog.md §7`).
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>⭐ THE SENTENCE, NOT THE NUMBER.</b> *"Sixteen years as a farmer"* is the diegetic
    /// fact; <c>proficiency 73</c> is the spreadsheet this game is defined against (§1.4), and
    /// §7 rejects it by name. The years are also the only thing the sim actually stores — time
    /// on the task (§3.1) — so the panel is not translating anything, it is reading it out.
    /// </para>
    /// <para>
    /// <b>Every trade they have given a year to, longest first</b>, because that is the career
    /// rather than the job — a farmer who spent a decade as a forester first is a different
    /// person from one who did not, and §5's whole argument is that what a village loses when
    /// somebody dies is that history.
    /// </para>
    /// <para>
    /// <b>⚠️ The workplace panel's version of this is landing 2's, deliberately.</b> §7 wants a
    /// hut to say how practised its workers are *"because that is the panel a player looks at
    /// when they want to know why a hut is slow"* — and until mastery bites (§3.3), a hut is
    /// never slow **for that reason**, so the sentence would be answering a question the sim
    /// cannot yet be asked.
    /// </para>
    /// </remarks>
    private static void DescribeTheirTrades(SimWorld world, Villager villager, List<string> lines)
    {
        int ticksPerYear = world.Config.TicksPerYear;
        if (ticksPerYear <= 0)
        {
            return;
        }

        // Longest first. Copied rather than sorted in place: `Villager.Skills` is kept in id
        // order because the state hash reads it in list order (D15), and a view that reordered
        // it would desync the sim from the panel that drew it.
        List<SkillProgress> held = villager.Skills
            .Where(progress => progress.Ticks >= ticksPerYear)
            .OrderByDescending(progress => progress.Ticks)
            .ThenBy(progress => progress.SkillId)
            .ToList();

        for (int i = 0; i < held.Count; i++)
        {
            SkillProgress progress = held[i];
            SkillRow? skill = world.Config.Skills.FirstOrDefault(row => row.Id == progress.SkillId);
            if (skill is null)
            {
                continue;
            }

            int years = progress.Ticks / ticksPerYear;
            string phrase = skill.YearsPhrase.Length > 0
                ? skill.YearsPhrase
                : $"at {skill.Name}";

            lines.Add(progress.Mastered
                ? $"{Years(years)} {phrase} — a master of the work."
                : $"{Years(years)} {phrase}.");
        }
    }

    /// <summary>"Nineteen years", spelled out — a life is counted, not measured.</summary>
    /// <remarks>
    /// Words up to the end of a working life, digits past it. §7's example is written out in
    /// words (*"sixteen years as a farmer"*) and the difference is register: a number in a
    /// sentence about a person reads like a stat block, and this game keeps saying it is not one.
    /// </remarks>
    private static string Years(int years)
    {
        if (years == 1)
        {
            return "One year";
        }

        string[] ones =
        {
            "zero", "one", "two", "three", "four", "five", "six", "seven", "eight", "nine",
            "ten", "eleven", "twelve", "thirteen", "fourteen", "fifteen", "sixteen",
            "seventeen", "eighteen", "nineteen",
        };

        string[] tens =
        {
            string.Empty, string.Empty, "twenty", "thirty", "forty", "fifty", "sixty",
            "seventy", "eighty", "ninety",
        };

        if (years < 0 || years >= 100)
        {
            return $"{years} years";
        }

        string word = years < 20
            ? ones[years]
            : years % 10 == 0
                ? tens[years / 10]
                : $"{tens[years / 10]}-{ones[years % 10]}";

        return $"{char.ToUpperInvariant(word[0])}{word[1..]} years";
    }

    /// <summary>Ground worth this share of ordinary, said the way the rest of the game says it.</summary>
    /// <remarks>
    /// <para>
    /// <b>⭐ THE BANDS COME FROM A RUN, NOT FROM THE RANGE IN THE CONFIG</b>
    /// (`PerSiteYieldTests.TheValleysSoilSpreadIsWideEnoughToName`). Measured on seed 12345 over
    /// 9,360 dry tiles: <b>p10 70%, median 101%, p90 135%, min 32%, max 165%</b> — so at 115/85
    /// the valley is <b>31% rich, 44% ordinary, 25% thin</b>, and each word names ground the
    /// player can actually walk to.
    /// </para>
    /// <para>
    /// <b>⚠️ The reasoning that preceded the probe was wrong, which is why the probe exists.</b>
    /// `MakeSoilRegional` bilinearly interpolates between lattice draws, and the obvious
    /// inference — that blending four draws would regress the typical tile toward the middle and
    /// leave the wash faint everywhere but the region cores — is not what the valley does. Same
    /// finding D178 got when smoothing turned out to *destroy* the amplitude it was meant to
    /// create: **when a spec and a measurement disagree, the spec is the one that is wrong.**
    /// </para>
    /// <para>
    /// <b>The share, not the soil byte.</b> 100 is ordinary because <c>crop_yield_per_tile</c> is
    /// locked and means *the yield on average ground* (D178) — so the number the panel quotes is
    /// the number the farm reaps, and the panel and the harvest cannot disagree.
    /// </para>
    /// <para>
    /// ⚠️ <b>115 and 85 are duplicated in `PerSiteYieldTests` on purpose.</b> `Bclone.Game` is
    /// outside `bclone.sln` (D11) so the suite cannot reference it; that guard is the only thing
    /// that can say these bands are bands the valley contains. Move one, move both.
    /// </para>
    /// </remarks>
    private static string DescribeSoil(int share) => share switch
    {
        >= 115 => $"Rich ground — a field here reaps {share}% of what ordinary ground gives.",
        <= 85 => $"Thin ground — a field here reaps {share}% of what ordinary ground gives.",
        _ => "Ordinary ground — a field here reaps about what average ground gives.",
    };

    /// <summary>A good's name as a player would say it.</summary>
    private static string Describe(JobKind kind) => kind switch
    {
        JobKind.Forager => "food is gathered here",
        JobKind.Forester => "trees are felled here",
        JobKind.Woodcutter => "logs are split into firewood here",
        JobKind.Marketer => "goods are handed out from here",
        // The HUT, not the site — a site describes itself and never reaches this (D108).
        JobKind.Builder => "the village's builders work from here",
        JobKind.Farmer => "the fields around it are sown and reaped from here",
        _ => kind.ToString().ToLowerInvariant(),
    };

    /// <summary>A blank line between two things standing on the same tile.</summary>
    private static void Separate(List<string> lines)
    {
        if (lines.Count > 0)
        {
            lines.Add(string.Empty);
        }
    }

    /// <summary>Append only entries not yet drawn; rebuilding would reset scroll.</summary>
    private void AppendNewLogLines()
    {
        IReadOnlyList<LogEntry> entries = _sink.Entries;

        for (int i = _renderedLogEntries; i < entries.Count; i++)
        {
            if (Shown(entries[i]))
            {
                _villageLog.AppendText(LogMarkup(entries[i]));
            }
        }

        _renderedLogEntries = entries.Count;

        if (_villageLog.GetLineCount() > MaxLogLines * 2)
        {
            _villageLog.Clear();
            _villageLog.AppendText("(earlier entries trimmed)\n");
            for (int i = entries.Count - MaxLogLines; i < entries.Count; i++)
            {
                if (i >= 0 && Shown(entries[i]))
                {
                    _villageLog.AppendText(LogMarkup(entries[i]));
                }
            }
        }
    }

    /// <summary>Whether this entry belongs in the village log as the player has set it.</summary>
    private bool Shown(LogEntry entry) =>
        entry.Subsystem == "life" && _shownCategories.Contains(entry.Category);

    /// <summary>One village-log line, escaped, and coloured if it was a moment.</summary>
    /// <remarks>
    /// <para>
    /// <b>⭐ A celebration reads as one</b> (Joe, 2026-08-27). The line is picked out by identity
    /// rather than by parsing: the sim writes a moment's body to the log as well as raising it,
    /// so the two are the same string by construction and the view needs no rule about what
    /// counts as good news.
    /// </para>
    /// <para>
    /// ⚠️ <b>The escape is not optional now BBCode is on.</b> A `[` in a village sentence would
    /// otherwise be swallowed as markup and take the rest of the tag with it. Nothing narrated
    /// today contains one — this is so nothing has to keep checking.
    /// </para>
    /// <para>
    /// ⚠️ <b>One colour, not a palette.</b> The full categorisation Joe asked for — deaths,
    /// important events, ordinary information, with filters — is its own slice and wants the
    /// category to come from the sim rather than from a set of strings the view happens to hold.
    /// <b>This is the foothold, not that feature.</b>
    /// </para>
    /// </remarks>
    private string LogMarkup(LogEntry entry)
    {
        string safe = Stamped(entry, out string when).Replace("[", "[lb]", StringComparison.Ordinal);

        // ⭐ THE CATEGORY DECIDES THE COLOUR NOW, AND `_celebrated` ONLY DECIDES EMPHASIS.
        // D241 picked celebrations out by identity because the sim had no category to offer; it
        // has one now, so the colour comes from the sim's own answer and the moment-matching is
        // left doing the one thing it is still better at — **saying which discovery was big
        // enough to interrupt for.** A technique worked out is gold; the one that stopped the
        // game to tell you is gold and bold.
        Color colour = ColourOf(entry.Category);
        string tinted = $"[color=#{colour.ToRgba32():x8}]{safe}[/color]";

        // ⭐ THE STAMP IS DIMMED AND THE SENTENCE IS NOT. It is a finding aid, not a thing to read
        // — the eye should land on the prose and use the column only when it is looking for a
        // date. A stamp in the line's own colour would make every entry start with the least
        // interesting words in it.
        string stamp = when.Length == 0
            ? string.Empty
            : $"[color=#{StampColour.ToRgba32():x8}]{when}[/color]  ";

        return _celebrated.Contains(entry.Message)
            ? $"{stamp}[b]{tinted}[/b]\n"
            : $"{stamp}{tinted}\n";
    }

    /// <summary>
    /// The entry's own date, and its sentence with the now-redundant prose date taken off.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>⭐⭐ HALF THE LOG ALREADY ENDS WITH THE DATE, SO A STAMP BOLTED ON THE FRONT WOULD SAY IT
    /// TWICE.</b> **Measured over a sixty-year run, not guessed: 94 of 193 stamped lines (48%)**
    /// end with exactly the date their own tick produces. **The stamp replaces that rather than
    /// joining it** — and because the prose form is longer than the stamp, <b>this makes those
    /// lines SHORTER, not longer.</b>
    /// </para>
    /// <para>
    /// <b>⛔⛔ SEASONAL SUMMARIES GET NO STAMP, AND FINDING OUT WHY IS THE REASON TO PROBE BEFORE
    /// SHIPPING.</b> They are written at the turn INTO a season but are ABOUT the one just ended —
    /// so a stamp read <em>"Y1 Sum · Spring of Year 1 — the village foraged 12 times"</em>, which
    /// looks like the log contradicting itself. **The stamp was right and it still read as a bug.**
    /// ⭐ They already carry their own date, in better English than a stamp, so the sim's own
    /// <c>LogCategory.Season</c> answers this — <em>no parsing, and no view-side rule about which
    /// sentences are retrospective.</em> ⚠️ They are **42% of a sixty-year log**, so the column is
    /// deliberately ragged: unstamped season lines read as headers between stamped entries, and a
    /// player who filters seasons off (one click, the biggest noise cut there is) sees every
    /// remaining line stamped.
    /// </para>
    /// <para>
    /// ⚠️ <b>THE OTHER 52% STILL SAY THE DATE MID-SENTENCE AND ARE LEFT ALONE ON PURPOSE</b> —
    /// <em>"Amos was born to the Thatcher household — Spring, Year 2. The village is now 5."</em>
    /// Ten narrating call sites across `HouseholdSystem`, `MortalitySystem`, `AgeingSystem`,
    /// `LabourAllocator` and the founding write it into the middle of a clause. **Taking those out
    /// is a change to the game's narrative voice, which is Joe's call rather than a refactor** —
    /// and two of them (the deaths) carry <c>Day 4, Spring, Year 58</c>, which is more precise than
    /// any stamp. *Recorded rather than done.*
    /// </para>
    /// <para>
    /// <b>⛔ IT IS NOT PARSING THE SENTENCE, AND THE DISTINCTION MATTERS BECAUSE D241 REFUSED TO
    /// PARSE ONE.</b> That ruling was about inferring *meaning* from prose — which line is a
    /// celebration — and it stands. This asks a different question: the view **constructs** the
    /// exact string the clock would have produced for this entry's own tick and tests whether the
    /// sentence ends with it. **A match is proof, not a guess**, and a miss changes nothing:
    /// the eleven sentences that carry the date mid-clause (<em>"X starved to death at 77, Day 4,
    /// Spring, Year 58. They had survived…"</em>) simply keep it, and are still stamped.
    /// </para>
    /// <para>
    /// <b>⚠️ THE HONEST ALTERNATIVE, NAMED SO NOBODY THINKS THIS WAS THE ONLY OPTION:</b> take the
    /// dates out of all 47 sentences at the source. **That is the cleaner end state and it is a
    /// change to the game's narrative voice across every system**, which is Joe's call rather than
    /// a refactor — several of those sentences are written to end on the date. If he wants it,
    /// this method collapses to two lines.
    /// </para>
    /// <para>
    /// ⚠️ <b>The clock is derived from the tick, never stored</b> (<see cref="SimClock.FromTick"/>),
    /// so this is the same calendar the sim used when it wrote the line — not a second opinion
    /// about what time it was.
    /// </para>
    /// </remarks>
    private string Stamped(LogEntry entry, out string when)
    {
        SimClock at = SimClock.FromTick(entry.Tick, _loop.World.Config);

        // "Y58 Spr" — the year first, because that is what a player scanning sixty years is
        // looking for, and three letters of season because the four are distinguishable at three.
        // Empty for a seasonal summary, which dates itself; see this method's remarks.
        when = entry.Category == LogCategory.Season
            ? string.Empty
            : $"Y{at.Year} {at.Season.ToString()[..3]}";

        string prose = $" {at.SeasonAndYear()}.";
        if (!entry.Message.EndsWith(prose, StringComparison.Ordinal))
        {
            return entry.Message;
        }

        // ⛔⛔ AND THE CUT LEAVES A DANGLING CONNECTIVE IF YOU DO NOT TIDY AFTER IT — which is not
        // a thing I reasoned out, it is a thing the rendered output said out loud:
        // *"Agnes is dangerously cold and is going in to get warm —"*. **Some sentences join the
        // date with an em dash rather than a full stop**, so removing the date removes what the
        // dash was pointing at and leaves the line hanging mid-thought.
        // ⭐ *Print what the control will actually show before believing a string transform.*
        string cut = entry.Message[..^prose.Length].TrimEnd(' ', '—', '-', ',', ';', ':');

        return cut.Length == 0 || cut[^1] is '.' or '!' or '?'
            ? cut
            : cut + ".";
    }

    /// <summary>The village log's timestamp column — present, and quiet.</summary>
    private static readonly Color StampColour = new("#7f8c94");

    private static int LivingHouseholds(SimWorld world)
    {
        int count = 0;
        for (int i = 0; i < world.Households.Count; i++)
        {
            if (world.LivingMembersOf(world.Households[i]) > 0)
            {
                count++;
            }
        }

        return count;
    }

    // ---------------------------------------------------------------
    //  Layout
    // ---------------------------------------------------------------

    /// <summary>
    /// The valley fills the window and everything else floats on top of it.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Rebuilt on Joe's call after watching the old one — <em>"I'm finding the info hard
    /// to read"</em> — with Banished itself as the reference. The old layout was a single
    /// vertical stack: header, controls, map, panels underneath. Everything competed with
    /// the map for height, the map got whatever was left, and the header's own sentences
    /// were squeezed into one clipped line because there was nowhere else for them to go.
    /// </para>
    /// <para>
    /// <b>Floating panels fix the class of bug rather than an instance of it.</b> Nothing
    /// shares a layout with the map any more, so a panel that grows by a line cannot move
    /// the world — which is what D54 spent a fixed-height strip and an ellipsis working
    /// around. The alert wraps to three lines now and the only consequence is that the
    /// alert is three lines tall.
    /// </para>
    /// <para>
    /// Panels are pinned to the corners the way Banished pins them: what the village is
    /// doing top-left, what just happened top-right, who lives here bottom-left, whatever
    /// you clicked on the right, and the controls bottom-right where your hand already is.
    /// </para>
    /// </remarks>
    private void BuildUi()
    {
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);

        // The valley, full-window and BEHIND everything. Added first, because draw order
        // here is child order and every panel has to be on top of the world it describes.
        _map = new VillageMap { ClipContents = true };
        _map.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        _map.BuildingClicked += OnBuildingClicked;
        _map.WhatsHereAsked += OnWhatsHereAsked;
        _map.VillagerClicked += OnVillagerClicked;
        AddChild(_map);

        AddTheFrameCounter();

        // ⭐ TWO COLUMNS, AND PANELS LIVE IN THEM RATHER THAN BESIDE THEM. Joe: *"when the
        // 'what the village is told' window is open, you can see 'the village' window
        // underneath."* Every panel used to anchor itself to a corner at an offset somebody
        // had to work out, so two panels growing toward each other overlapped — and once they
        // were see-through, what showed through was another panel rather than the valley.
        //
        // **A column makes overlap impossible by construction** instead of by choosing sizes
        // carefully, which is the only kind of fix that survives adding a seventh panel.

        BuildTopBars();
        // ⭐ THE ROSTER FIRST, SO PROFESSIONS STACKS BELOW IT (D326, Joe). `_docked` order IS the
        // default stacking order, so "immediately below The village" is a build-order fact rather
        // than a coordinate — which is what keeps it true when a panel above them changes height.
        BuildRosterPanel();
        BuildProfessionsPanel();
        BuildStockLimitsPanel();

        // Top of the right-hand column, which is where Banished puts it and where Joe's
        // screenshot has it — above the log, so the two things you glance at are together.
        BuildMinimapPanel();
        BuildLogPanel();
        BuildInspectorPanel();

        // Last of the panels, because it lists the ones built before it.
        BuildSettingsPanel();
        BuildControlPanel();

        // ⭐ START PAUSED (Joe, 2026-08-25): the player unpauses to begin. It was 1.0, so
        // the valley started running the moment the window appeared — and the founding is
        // the one stretch of this game that asks the player to act inside a single year
        // (D74). Reading the map, painting ground and marking a store is not something to
        // do against a clock that started without being asked.
        //
        // Space unpauses, and `SetSpeed` already writes "PAUSED" into the label from
        // `_driver.IsPaused`, so the header says so from the first frame.
        SetSpeed(0.0);

        BuildTheMomentPanel();
        BuildThePassingPanel();

        // Start on Selected, and set the button's label from the same switch that the
        // key binding uses — two places writing that text would eventually disagree.
        _detail = MapDetail.Off;
        CycleDetail();
    }

    /// <summary>Where the panels sit, in pixels from the edge they are pinned to.</summary>
    private const int Edge = 14;

    /// <summary>
    /// How tall the two scrolling lists stand — the roster and the village log.
    /// </summary>
    /// <remarks>
    /// They were 280 and 210, chosen separately and for no stated reason. One number, and a
    /// smaller one, because the type came down (<see cref="RowSize"/>): 190 pixels is about
    /// eleven names at 13-point, against ten at 16. <b>More list in less panel</b>, which is
    /// the whole trade Joe asked for.
    /// </remarks>
    private const int ListHeight = 190;

    /// <summary>
    /// The village at a glance, along the map's top edge: what it holds, and who is here (D378).
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Joe's UI pass, slice 2</b> (`specs/the-cards.md §5`): the Overview panel — a dozen
    /// rows in a window — becomes two bars where the eye already is. *"Remove text where you
    /// can."* The resources box is two rows of eight goods with a <c>more ▾</c> for the rest; the
    /// villagers box is the four counts he looks at most, a laborer count beside them, and the
    /// clock. Everything the Overview held that is not a number the player glances at — the
    /// seed, the log path, the build, the roadmap of goods that do not exist yet — went to
    /// Settings, where a thing consulted once belongs.
    /// </para>
    /// <para>
    /// <b>⛔ Fixed height and fixed width, by construction.</b> Every number is an
    /// <see cref="Amount"/> cell (D367), so a heap of 12,345 cannot widen the bar; the names
    /// are static; <c>more ▾</c> opens a popup rather than a fold, because a fold would grow the
    /// bar and push the roster under it. The probe's <c>bars:</c> line poses every cell at its
    /// longest and refuses a bar that moved.
    /// </para>
    /// <para>
    /// <b>One floater, two boxes.</b> The outer panel has no skin and no title — so it is not
    /// foldable, not draggable and not in Settings' window list, exactly like the control bar —
    /// and the two skinned boxes inside it are what the player sees. One node to position, one
    /// to measure, and the left column starts below it (<see cref="TopOfTheLeftColumn"/>).
    /// </para>
    /// </remarks>
    private void BuildTopBars()
    {
        VBoxContainer body = Floating(Edge, Edge, 0, 0, Corner.TopLeft);
        _topBar = _panels[^1];
        _topBar.AddThemeStyleboxOverride("panel", new StyleBoxEmpty());

        var boxes = new HBoxContainer();
        boxes.AddThemeConstantOverride("separation", 10);
        body.AddChild(boxes);

        boxes.AddChild(BuildResourcesBox());
        boxes.AddChild(BuildVillagersBox());
    }

    /// <summary>The bar itself, for the probe and for whatever has to sit below it.</summary>
    private PanelContainer _topBar = null!;

    /// <summary>
    /// The eight goods on the bar, in Joe's order, two rows. <b>Every other good the catalogue
    /// has goes behind <c>more ▾</c></b>, in catalogue order.
    /// </summary>
    /// <remarks>
    /// A short hand list rather than the catalogue walk the Overview did, because the rows are
    /// his (*"food, produce, wheat, fish, meat / logs, firewood, stone, tools"*) and the
    /// catalogue's order is not (fish and meat sit before wheat there). ⚠️ The catalogue is
    /// still what decides <em>whether</em> a good exists: a modded good lands in the popup the
    /// day it is added, which is D210's rule kept — the bar never hides a good, it only
    /// decides which eight are on the front. *"Customisable which goods show"* is Joe's later.
    /// </remarks>
    private static readonly Goods[][] BarRows =
    {
        new[] { Goods.Produce, Goods.Wheat, Goods.Fish, Goods.Meat },
        new[] { Goods.Logs, Goods.Firewood, Goods.Stone, Goods.Tools },
    };

    private PanelContainer BuildResourcesBox()
    {
        SimWorld world = _loop.World;
        var box = new PanelContainer();
        box.AddThemeStyleboxOverride("panel", PanelSkin(0.94f));

        var rows = new VBoxContainer();
        rows.AddThemeConstantOverride("separation", 3);
        box.AddChild(rows);

        // ⭐⭐ FOOD IS AN UMBRELLA (Joe, 2026-09-05) AND IT COMES FIRST. `FoodTheVillageHolds` is
        // what the birth gate, the food limit and the labour quota all read, so the first number
        // on the bar is the number the village actually decides on; the four foods after it are
        // its parts. The umbrella's chip is produce's, as the Overview's was.
        HBoxContainer first = BarRow();
        rows.AddChild(first);
        _foodTotal = AddBarCell(first, ChipColour(Goods.Produce), "food");
        foreach (Goods goods in BarRows[0])
        {
            AddGoodsCell(first, world, goods);
        }

        HBoxContainer second = BarRow();
        rows.AddChild(second);
        foreach (Goods goods in BarRows[1])
        {
            AddGoodsCell(second, world, goods);
        }

        second.AddChild(BuildTheMoreButton(world));
        return box;
    }

    private static HBoxContainer BarRow()
    {
        var row = new HBoxContainer();
        row.AddThemeConstantOverride("separation", 9);
        return row;
    }

    /// <summary>A chip, a number that cannot widen, and a name — one cell of a bar.</summary>
    private static Label AddBarCell(HBoxContainer row, Color? chip, string name, float width = BarAmountWidth)
    {
        var cell = new HBoxContainer();
        cell.AddThemeConstantOverride("separation", 5);
        if (chip is Color colour)
        {
            cell.AddChild(Chip(colour));
        }

        // ⚠️ `Fill`, not the `ExpandFill` the Overview's cells had: in a row of cells an
        // expanding one takes the slack and the bar's width stops being its contents'.
        Label number = Amount(width);
        number.SizeFlagsHorizontal = SizeFlags.Fill;
        cell.AddChild(number);
        cell.AddChild(Muted(name));
        row.AddChild(cell);
        return number;
    }

    private void AddGoodsCell(HBoxContainer row, SimWorld world, Goods goods)
    {
        Label held = AddBarCell(row, ChipColour(goods), world.GoodsCatalog.NameOf(goods));
        _goodsReadouts.Add((goods, held));
    }

    /// <summary>Room for "123,456" at <see cref="RowSize"/> — a bar's cell, narrower than a panel's.</summary>
    private const float BarAmountWidth = 48f;

    /// <summary>
    /// <c>more ▾</c>: the goods not on the front of the bar, and the two things that are not
    /// in the stores — in a popup, so the bar's height never moves.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The two permanent rows are D367's: food out in the larders and buffers, and goods lying
    /// in the yard, <c>—</c> when there is nothing and <c>+N</c> when there is. The per-good
    /// reason a heap is on the ground (D134's three states) is the ground row's tooltip.
    /// </para>
    /// <para>
    /// ⚠️ A <see cref="PopupPanel"/> is a <see cref="Window"/>, not a child in the scaled tree, so
    /// it is told the UI scale each time it opens rather than inheriting it.
    /// </para>
    /// </remarks>
    private Button BuildTheMoreButton(SimWorld world)
    {
        var more = new Button
        {
            Text = "more ▾",
            Flat = true,
            TooltipText = "The rest of the goods · in homes and huts · on the ground",
        };
        more.AddThemeFontSizeOverride("font_size", 12);

        _morePopup = new PopupPanel { WrapControls = true };
        _morePopup.AddThemeStyleboxOverride("panel", PanelSkin());
        more.AddChild(_morePopup);

        var table = new GridContainer { Columns = 3 };
        table.AddThemeConstantOverride("h_separation", 10);
        table.AddThemeConstantOverride("v_separation", 2);
        _morePopup.AddChild(table);

        for (int id = 0; id < world.GoodsCatalog.Count; id++)
        {
            var goods = (Goods)id;
            if (OnTheFrontOfTheBar(goods))
            {
                continue;
            }

            table.AddChild(Chip(ChipColour(goods)));
            table.AddChild(Muted(world.GoodsCatalog.NameOf(goods)));
            Label held = Amount();
            table.AddChild(held);
            _goodsReadouts.Add((goods, held));
        }

        table.AddChild(new Control());
        table.AddChild(Muted("in homes and huts"));
        _foodElsewhere = Amount();
        table.AddChild(_foodElsewhere);

        table.AddChild(new Control());
        _onTheGroundLabel = Muted("on the ground");
        _onTheGroundLabel.MouseFilter = MouseFilterEnum.Pass;
        table.AddChild(_onTheGroundLabel);
        _onTheGround = Amount();
        _onTheGround.MouseFilter = MouseFilterEnum.Pass;
        table.AddChild(_onTheGround);

        more.Pressed += () =>
        {
            Rect2 at = more.GetGlobalRect();
            _morePopup.ContentScaleFactor = _uiScale;
            _morePopup.Popup(new Rect2I((int)at.Position.X, (int)at.End.Y + 2, 0, 0));
        };

        return more;
    }

    private static bool OnTheFrontOfTheBar(Goods goods)
    {
        foreach (Goods[] row in BarRows)
        {
            if (System.Array.IndexOf(row, goods) >= 0)
            {
                return true;
            }
        }

        return false;
    }

    private PopupPanel _morePopup = null!;
    private Label _foodElsewhere = null!;
    private Label _onTheGround = null!;
    private Label _onTheGroundLabel = null!;

    /// <summary>
    /// Who is here — total, adults, children, elders, laborers — and the clock.
    /// </summary>
    /// <remarks>
    /// *"17 villagers"* is the number; *"11 adults and 4 children"* is the one that tells you
    /// whether the village is growing or ageing out, which is the question a generational game
    /// is about. The laborer count is the Professions panel's, put where it is read: a limit is
    /// what creates laborers (D63), and this is where the player sees the limit bite. The dots
    /// are the map's own villager colours, so a child on the bar and a child in the valley are
    /// one fact.
    /// </remarks>
    private PanelContainer BuildVillagersBox()
    {
        var box = new PanelContainer();
        box.AddThemeStyleboxOverride("panel", PanelSkin(0.94f));

        var lines = new VBoxContainer();
        lines.AddThemeConstantOverride("separation", 3);
        box.AddChild(lines);

        HBoxContainer counts = BarRow();
        lines.AddChild(counts);
        _populationCell = AddBarCell(counts, VillageMap.AdultColour, "villagers", HeadcountWidth);
        _adultsCell = AddBarCell(counts, null, "adults", HeadcountWidth);
        _childrenCell = AddBarCell(counts, VillageMap.ChildColour, "children", HeadcountWidth);
        _eldersCell = AddBarCell(counts, VillageMap.ElderColour, "elders", HeadcountWidth);
        _laborersCell = AddBarCell(counts, null, "laborers", HeadcountWidth);

        // The valley's name is the one word that says which run you are watching — derived
        // from the seed, not drawn from it (`SimWorld.Name`). The tick left this line for the
        // seed line in Settings: it is a bug report's number, not a player's.
        _clockLabel = Muted(string.Empty);
        lines.AddChild(_clockLabel);

        return box;
    }

    /// <summary>
    /// Room for "1,234" — a headcount, which is never six figures. Narrower than a goods cell so
    /// the whole bar clears the right-hand column at the default scale (the probe says where it ends).
    /// </summary>
    private const float HeadcountWidth = 34f;

    private Label _populationCell = null!;
    private Label _adultsCell = null!;
    private Label _childrenCell = null!;
    private Label _eldersCell = null!;
    private Label _laborersCell = null!;

    /// <summary>
    /// A right-aligned number cell that <b>cannot widen its column</b> (D367).
    /// </summary>
    /// <remarks>
    /// A Godot <see cref="Label"/> reports its text's width as its minimum, so a long string in
    /// one cell is a wider panel — which is how the Overview breathed in and out as heaps and
    /// larders came and went. <c>ClipText</c> is what stops the text from being the minimum; the
    /// fixed minimum is what keeps the numbers aligned; the ellipsis is the honest failure if a
    /// number ever outgrows it.
    /// </remarks>
    private static Label Amount(float width = AmountWidth)
    {
        Label label = Body(string.Empty);
        label.HorizontalAlignment = HorizontalAlignment.Right;
        label.SizeFlagsHorizontal = SizeFlags.ExpandFill;

        // ⚠️ THE OVERRUN BEHAVIOUR IS THE ONE THAT MATTERS, measured: a Label with any trimming
        // set stops counting its text toward its minimum; `ClipText` alone does not (the red
        // check with only it reverted scored zero). Both, so the intent is legible.
        label.ClipText = true;
        label.TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis;
        label.CustomMinimumSize = new Vector2(width, 0f);
        return label;
    }

    /// <summary>
    /// Amber on a bar's number: <b>the village is short of it</b>, by the sim's own reckoning.
    /// </summary>
    /// <remarks>
    /// <para>
    /// ⭐ <b>The same predicates that staff the trades, so the bar and the Professions panel
    /// cannot disagree</b> (D378): food while <see cref="SimWorld.TheVillageWantsMoreFood"/>,
    /// firewood while <see cref="LabourQuota.WoodcuttersWanted"/> wants hands, logs while
    /// <see cref="LabourQuota.ForestersWanted"/> does, tools while
    /// <see cref="LabourQuota.SmithsWanted"/> does (D391). Nothing else has a demand function
    /// today, so nothing else goes amber — and a stock limit is deliberately not one: a limit is
    /// a ceiling, and holding less than a ceiling is not a shortage.
    /// </para>
    /// <para>
    /// A founding village is short of all three, so the bar opens amber. That is honest: it is.
    /// </para>
    /// </remarks>
    private static void ShowShortfall(Label number, bool wanting, string why)
    {
        if (wanting)
        {
            number.AddThemeColorOverride("font_color", LightStopped);
            number.TooltipText = why;
            number.MouseFilter = MouseFilterEnum.Pass;
        }
        else
        {
            number.RemoveThemeColorOverride("font_color");
            number.TooltipText = string.Empty;
        }
    }

    /// <summary>Room for "1,269,000" at <see cref="RowSize"/>.</summary>
    private const float AmountWidth = 64f;

    /// <summary>The goods that do not exist yet, behind a fold of their own.</summary>
    private static VBoxContainer BuildGoodsRoadmap()
    {
        VBoxContainer inside = Foldaway(
            $"Not here yet — {NotYetInTheValley.Length} more, and why", out VBoxContainer fold);

        var table = new GridContainer { Columns = 3 };
        table.AddThemeConstantOverride("h_separation", 10);
        table.AddThemeConstantOverride("v_separation", 2);
        inside.AddChild(table);

        foreach ((string name, string reason) in NotYetInTheValley)
        {
            table.AddChild(Chip(new Color(1, 1, 1, 0.10f)));
            table.AddChild(Muted(name));

            Label why = Muted(reason);
            why.HorizontalAlignment = HorizontalAlignment.Right;
            why.SizeFlagsHorizontal = SizeFlags.ExpandFill;
            why.AutowrapMode = TextServer.AutowrapMode.WordSmart;
            table.AddChild(why);
        }

        return fold;
    }

    /// <summary>
    /// A section that is rolled up until asked for. <b>A fold inside a fold, on purpose.</b>
    /// </summary>
    /// <remarks>
    /// <para>
    /// D113 argued against exactly this — <em>"folding inside a fold is two clicks to reach the
    /// number you opened the panel for"</em> — and that argument still holds for every number
    /// in these panels. <b>It does not hold for the roadmap rows</b>, which are the one thing
    /// here that is not a number you came for: <em>Coal — no mine to dig it</em> is consulted
    /// once and then known.
    /// </para>
    /// <para>
    /// <b>And they were most of the reason the columns overflowed</b> (Joe: <em>"when all
    /// panels are open, the bottom panels go off screen"</em>) — nineteen rows between the two
    /// panels, none of which changes from one century to the next. Folded, the roadmap costs
    /// one line and still says how much is behind it, which is the part that must not be
    /// hidden: a fold that does not announce its contents is just a missing feature.
    /// </para>
    /// </remarks>
    private static VBoxContainer Foldaway(string caption, out VBoxContainer fold)
    {
        fold = new VBoxContainer();
        fold.AddThemeConstantOverride("separation", 2);

        var inside = new VBoxContainer { Visible = false };
        inside.AddThemeConstantOverride("separation", 2);

        var toggle = new Button
        {
            Text = $"▸ {caption}",
            Flat = true,
            Alignment = HorizontalAlignment.Left,
            ToggleMode = true,
        };

        toggle.AddThemeFontSizeOverride("font_size", 12);
        toggle.Modulate = new Color(1, 1, 1, 0.45f);

        VBoxContainer opening = inside;
        toggle.Toggled += open =>
        {
            opening.Visible = open;
            toggle.Text = open ? $"▾ {caption}" : $"▸ {caption}";
        };

        fold.AddChild(toggle);
        fold.AddChild(inside);
        return inside;
    }

    /// <summary>
    /// The rows that are a roadmap rather than a readout. <b>Delete a row when it ships.</b>
    /// </summary>
    private static readonly (string Name, string Reason)[] NotYetInTheValley =
    {
        ("Coal", "no mine to dig it"),
        ("Cloth", "waiting on livestock"),
        ("Clothes", "waiting on cloth and leather"),
        ("Ale", "no brewer, no barley"),
        ("Medicine", "no physician"),
        ("Health", "illness is not modelled"),
        ("Happiness", "not modelled"),
        ("Students", "no school"),
    };

    /// <summary>A small square of colour standing in for an icon.</summary>
    private static ColorRect Chip(Color colour) => new()
    {
        Color = colour,
        CustomMinimumSize = new Vector2(10, 10),
        SizeFlagsVertical = SizeFlags.ShrinkCenter,
    };

    /// <summary>What colour a good reads as — <b>one answer, shared with the map</b>.</summary>
    /// <remarks>
    /// ⭐ The table moved to <see cref="GoodsPalette"/> when goods started being drawn where
    /// they lie (2026-09-05). A chip in this panel and a heap in the valley have to be the
    /// same colour, or they are two facts rather than one.
    /// </remarks>
    private static Color ChipColour(Goods goods) => GoodsPalette.ColourOf(goods);

    /// <summary>Every goods row's amount label, so the tick can fill them in.</summary>
    private readonly List<(Goods Goods, Label Held)> _goodsReadouts = new();

    /// <summary>The Food row — <b>every kind of food, which no single good answers</b>.</summary>
    private Label _foodTotal = null!;

    /// <summary>The whole valley, small, with a box round what you are looking at.</summary>
    /// <remarks>
    /// <b>An ordinary panel</b>, so it collapses with <c>c</c>, hides with <c>h</c> and turns
    /// off from Settings like everything else — the alternative was a control anchored to a
    /// corner of its own, which is precisely the second panel mechanism D113 spent a session
    /// arguing out of existence.
    /// </remarks>
    private void BuildMinimapPanel()
    {
        VBoxContainer body = InColumn(right: true, 0, "The valley");

        _minimap = new Minimap();
        _minimap.LookAt += tile => _map.CentreOn(tile);
        body.AddChild(_minimap);
    }

    private Minimap _minimap = null!;

    /// <summary>The story so far — Banished's event log, in much the same corner.</summary>
    private void BuildLogPanel()
    {
        // ⛔ HEIGHT 0 for the same reason as the roster: a `RichTextLabel` with
        // `ScrollFollowing` scrolls itself, so a `ScrollContainer` around it lays it out at its
        // minimum of zero. Measured: `chars 73, size 288x0` — **every line the village had
        // narrated was present and none of it was drawn.** See `BuildRosterPanel`.
        VBoxContainer body = InColumn(right: true, 0, "Village log");

        // ⭐ BBCODE IS ON SO A CELEBRATION CAN READ AS ONE (Joe, 2026-08-27: a discovery should be
        // *"a different font color in the village log"*). ⚠️ **Everything appended must go through
        // `LogMarkup` from here on** — with BBCode enabled a stray `[` in a village sentence
        // becomes markup and the line silently loses text. Nothing narrated today contains one;
        // the escape is there so nothing has to remember that.
        _villageLog = new RichTextLabel
        {
            ScrollFollowing = true,
            BbcodeEnabled = true,
            SizeFlagsVertical = SizeFlags.ExpandFill,
            CustomMinimumSize = new Vector2(0, ListHeight),
        };

        // A RichTextLabel does not go through `Body`, so it kept Godot's 16 while everything
        // around it shrank — which is exactly the panel Joe named first.
        // ⛔⛔ EVERY VARIANT, NOT JUST `normal` — Joe, 2026-08-28, with a screenshot: *"what is this
        // font size in the village log? got big out of nowhere!"*
        //
        // **A `RichTextLabel` picks a DIFFERENT theme font per markup variant**, and only
        // `normal_font_size` was overridden. That was invisible for as long as nothing in the log
        // was ever bold — and D241 started wrapping a celebrated discovery in `[b]`, which fell
        // through to Godot's default **16** while every other line rendered at `RowSize`. So the
        // one line the player is most meant to notice was the one that looked broken.
        //
        // ⚠️ **Turning BBCode on is what made a second font size reachable at all.** The lesson
        // is not "remember bold": it is that enabling markup enables every theme slot the markup
        // can name, and overriding one of them is overriding none of them.
        // ⭐ AND THE LOG ALONE RUNS SMALLER THAN EVERY OTHER PANEL SINCE 2026-08-29 (Joe, asking
        // for timestamps: *"smaller font to compensate for the extra text"*). ⚠️ **It is the one
        // panel that is READ rather than SCANNED** — every other list is short labelled rows, and
        // this is prose that has to hold sixty years of it. **The variants are all set together
        // for the reason the paragraph above records**: overriding one theme slot is overriding
        // none of them.
        _villageLog.AddThemeFontSizeOverride("normal_font_size", LogSize);
        _villageLog.AddThemeFontSizeOverride("bold_font_size", LogSize);
        _villageLog.AddThemeFontSizeOverride("italics_font_size", LogSize);
        _villageLog.AddThemeFontSizeOverride("bold_italics_font_size", LogSize);
        _villageLog.AddThemeFontSizeOverride("mono_font_size", LogSize);
        body.AddChild(_villageLog);

        // ⭐⭐ ONE SWITCH PER CATEGORY (Joe, 2026-08-27: *"a filter for each category… optimize
        // noise to signal ratio"*). **Under the log rather than above it**, because the log is
        // what the panel is for and a row of controls above it pushes the newest line down.
        //
        // ⚠️ **`Ordinary` gets a switch too, and it is the one worth having.** It is the bulk of
        // the log by a wide margin, so *"show me only what I chose to care about"* is exactly
        // "turn Ordinary off" — a filter set that could not mute the majority would not change
        // the ratio it exists to change.
        var filters = new HFlowContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        filters.AddThemeConstantOverride("h_separation", 6);
        filters.AddThemeConstantOverride("v_separation", 2);

        foreach (LogCategory category in System.Enum.GetValues<LogCategory>())
        {
            _shownCategories.Add(category);

            var toggle = new CheckBox
            {
                Text = CategoryName(category),
                ButtonPressed = true,
                Flat = true,
            };

            toggle.AddThemeFontSizeOverride("font_size", RowSize);

            // ⭐ THE SWITCH IS THE COLOUR, so the filter row doubles as the legend. A key
            // somewhere else is a second thing to keep in step with the first.
            toggle.AddThemeColorOverride("font_color", ColourOf(category));
            toggle.AddThemeColorOverride("font_pressed_color", ColourOf(category));
            toggle.AddThemeColorOverride("font_hover_color", ColourOf(category));

            LogCategory captured = category;
            toggle.Toggled += on =>
            {
                if (on)
                {
                    _shownCategories.Add(captured);
                }
                else
                {
                    _shownCategories.Remove(captured);
                }

                RedrawTheLog();
            };

            filters.AddChild(toggle);
        }

        body.AddChild(filters);
    }

    /// <summary>Which categories the player is currently letting through.</summary>
    private readonly HashSet<LogCategory> _shownCategories = new();

    /// <summary>What a category is called on the switch that filters it.</summary>
    /// <remarks>
    /// <b>Named for the player, not for the enum.</b> <c>Life</c> is the births and the pairings
    /// and reads as nothing at all on a switch; <em>"Born"</em> says what it lets through.
    /// </remarks>
    private static string CategoryName(LogCategory category) => category switch
    {
        LogCategory.Ordinary => "Everyday",
        LogCategory.Death => "Deaths",
        LogCategory.Life => "Born",
        LogCategory.Discovery => "Learned",
        LogCategory.Warning => "Warnings",
        LogCategory.Building => "Building",
        LogCategory.Season => "Seasons",
        _ => category.ToString(),
    };

    /// <summary>What a category reads as in the log.</summary>
    /// <remarks>
    /// <b>Joe's shape:</b> <i>"deaths, important events in colors, general info in white."</i> So
    /// <c>Ordinary</c> is the parchment white everything used to be, and every other colour has
    /// to earn its place against it — <b>a log where everything is coloured is a log where
    /// nothing is.</b> Deaths take the red the food chip already uses and discoveries the gold
    /// the library gift used, so a colour means the same thing in two places.
    /// </remarks>
    private static Color ColourOf(LogCategory category) => category switch
    {
        LogCategory.Death => new Color(0.85f, 0.40f, 0.40f),
        LogCategory.Life => new Color(0.55f, 0.80f, 0.55f),
        LogCategory.Discovery => new Color(0.91f, 0.77f, 0.41f),
        LogCategory.Warning => new Color(1f, 0.78f, 0.35f),
        LogCategory.Building => new Color(0.62f, 0.72f, 0.85f),
        LogCategory.Season => new Color(0.60f, 0.62f, 0.60f),
        _ => new Color(0.88f, 0.88f, 0.86f),
    };

    /// <summary>Rebuild the whole log — the one thing a filter change has to do.</summary>
    /// <remarks>
    /// <b>⚠️ A filter change cannot be incremental</b>, which is the one place `AppendNewLogLines`'
    /// cursor has to be given up: turning a category back on has to bring back lines that were
    /// never drawn. Resetting the cursor and clearing is the whole of it, and it happens on a
    /// click rather than per frame.
    /// </remarks>
    private void RedrawTheLog()
    {
        _villageLog.Clear();
        _renderedLogEntries = 0;
        AppendNewLogLines();
    }

    /// <summary>Everyone alive, and what they are doing about it.</summary>
    private void BuildRosterPanel()
    {
        // ⛔⛔ HEIGHT 0 — NO SCROLL WRAPPER — BECAUSE AN `ItemList` SCROLLS ITSELF.
        // Passing `ListHeight` here wraps the list in a `ScrollContainer`, and a scroll
        // container lays its content out at the content's MINIMUM height — that is what makes
        // scrolling possible. An `ItemList` reports a minimum of **0** (it scrolls internally),
        // and `SizeFlagsVertical = ExpandFill` buys nothing inside a scroll because there is no
        // spare space to expand into. Measured: `items 4, size 288x0`. **The village was listed
        // correctly and drawn zero pixels tall**, which is what Joe saw as a blank panel.
        // ⚠️ D306's own lesson arriving from the other side: it gave every panel its own scroll
        // because panels used to borrow the column's — and the two panels whose content already
        // scrolled are the two it broke. *A self-scrolling control needs a HEIGHT, not a scroll.*
        VBoxContainer body = InColumn(right: false, 0, "The village");

        _roster = new ItemList
        {
            SizeFlagsVertical = SizeFlags.ExpandFill,
            CustomMinimumSize = new Vector2(0, ListHeight),
        };
        _roster.AddThemeFontSizeOverride("font_size", RowSize);
        _roster.ItemSelected += OnVillagerSelected;
        body.AddChild(_roster);
    }

    /// <summary>Whoever — or whatever — you last clicked on.</summary>
    private void BuildInspectorPanel()
    {
        // Below the log on the right-hand edge, which is where Banished puts the panel
        // for the thing you have selected.
        // ⚠️ SIZED TO ITS CONTENT, NOT PINNED TO A HEIGHT — and that was a real bug rather
        // than a preference. Pinned at 330 the panel could not grow, so the rows added to the
        // bottom of it (staffing, the build queue, and then a forester's ground) were drawn
        // OUTSIDE the panel, on top of the map and underneath the control bar — which is added
        // after it and therefore draws over it. Joe: *"clicking 'give ground' seems to bring
        // the bottom menu to the foreground and clicking on the map doesn't paint anything."*
        // The button was never receiving the click at all.
        //
        // A height of zero means "as tall as what is in you", so a control can never end up
        // outside the panel that owns it — and with nothing selected the panel is a title bar
        // rather than 330 pixels of reserved emptiness, which is a down payment on Joe's
        // *"they are HUGE and take up so much real estate"*.
        //
        // ⚠️ A busy selection can still reach the control bar. The z-order rule below stops
        // that being fatal; making panels small, movable and resizable is the real answer and
        // is its own piece of work.
        // ⛔⛔ A FIXED HEIGHT AGAIN, BUT ON THE INSIDE, AND WITH A BAR (D367). Joe: *"I hate how
        // the size of the panels change when information updates"* — this panel was a different
        // height for every thing he clicked. `InColumn` with a height puts a `ScrollContainer` of
        // that height INSIDE the panel's contents: the frame is still zero-height-means-content
        // (D314, so folding still shrinks it), the content is `InspectorHeight` tall whatever is
        // selected, and a description longer than that scrolls with a VISIBLE bar — which is what
        // D350's "a scroll with no visible bar is a cut" forbids, and the probe checks.
        // ⛔ A THING WITH A CARD IS NOT DESCRIBED HERE (D376, D377). Joe: *"such a mess of stacked
        // sentences i dont even know what to read"* and then *"why 2 panels for one structure?"* —
        // the card is the one surface for a building or a person, its controls under its Settings
        // fold. This panel is left for what has no card yet: bare ground, the library, the hall.
        // It hides whenever the selection has a card.
        VBoxContainer body = InColumn(right: true, InspectorHeight, "What's here");

        // ScrollActive so a long reason scrolls rather than being cut off. The one panel
        // whose job is explaining a decision must never truncate the explanation.
        //
        // ⚠️ AND IT NEEDS A HEIGHT OF ITS OWN, which is the whole of the bug Joe reported as
        // *"I have selected the cart but none of the windows tell me what is in the cart."*
        // A scrolling RichTextLabel has a minimum height of ZERO — it assumes something else
        // is giving it room. That was true while the panel was pinned to 330 and stopped being
        // true the moment the panel sized itself to its contents, so the label asked for
        // nothing, got nothing, and every description in the game was rendered into a box no
        // pixels tall. **The text was always there. There was nowhere to draw it.**
        //
        // A minimum rather than a fixed size, so the panel still grows for the staffing, queue
        // and ground rows beneath it.
        //
        // ⛔⛔ AND IT GROWS TO ITS CONTENT NOW, BECAUSE A SCROLL WITH NO VISIBLE BAR IS A CUT
        // (D350). The market is a workplace AND a store, so its description was the longest in the
        // game — and its `Holding:` line, the one a store exists to say, sat one line below the
        // 140px fold. Joe: *"the market doesn't tell how many units of each item are within."* It
        // did; the panel hid it and gave no sign. `FitContent` makes the label as tall as its text,
        // so the window (a free-floating one since D306, folding to its title bar since D314) is
        // exactly as tall as what it has to say. The comment two paragraphs up said *"the one
        // panel whose job is explaining a decision must never truncate the explanation"* — this
        // is that sentence kept.
        _inspector = new RichTextLabel
        {
            BbcodeEnabled = false,
            ScrollActive = false,

            // As tall as its text (D350), inside a box that is always `InspectorHeight` (D367):
            // the label never scrolls itself, the box around it does, so the bar is the box's
            // and it shows.
            FitContent = true,
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
        };

        _inspector.AddThemeFontSizeOverride("normal_font_size", RowSize);
        body.AddChild(_inspector);

        _whatsHerePanel = _docked[^1].Panel;
    }

        // ⭐ STAFFING WHERE THE BUILDING IS (Joe). It lived on the toolbar and acted on
        // whatever happened to be selected, which D93 recorded as "in a weird place right
        // now" — and it becomes a lever the player reaches for often, so hunting for it
        // reads as the game being unfair.
        //
        // Here it is unmistakably ABOUT the thing named directly above it. It hides when
        // the selection is not a staffable workplace, which is the opposite of the old
        // reasoning ("a button that comes and goes is a button you hunt for") — and the
        // reason it is safe to reverse is that the buttons now sit inside the panel that
        // says what they would act on. A control with no subject is worse than an absent one.
        // ⭐⭐ KEEPING A NAMED VILLAGER ON A TRADE (Joe, 2026-08-22, built 2026-08-28) — the first
        // per-VILLAGER control in the game; every other one acts on a building or on the village.
        //
        // ⛔ A BUTTON PER TRADE RATHER THAN A DROPDOWN. Joe asked for a dropdown and a
        // *"permanent?"* checkbox, which is two controls and two clicks for one decision. Six
        // trades fit on a wrapped row, the current pin reads off which button is pressed, and
        // pressing the pressed one hands them back — **so the checkbox has nothing left to do.**
        //
        // ⚠️ It sits ABOVE staffing deliberately: staffing is about a building you have selected,
        // this is about a person, and the inspector shows one or the other.


    /// <summary>Set, or hand back to the derived number, one good's limit at the selected market (D372).</summary>
    private void SetSelectedMarketLimit(Goods goods, int? limit)
    {
        if (SelectedStore() is not { Kind: StoreKind.Market } market)
        {
            return;
        }

        Warn(_loop.World.SetMarketLimit(market, goods, limit));
        RefreshInspector(_loop.World);
    }

    /// <summary>
    /// One row of the inspector: what it is about on its own wrapped line, and the controls
    /// that act on it flowing underneath.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>⛔⭐⭐ THIS IS WHY THE PANELS ATE THE MAP (D169, Joe: *"look at the attached screenshot
    /// to see how dumb the width of the windows on the right side of the screen are"*).</b>
    /// Every one of these rows used to be an <see cref="HBoxContainer"/> holding a sentence and
    /// some buttons, and <b>an HBox's minimum width is the sum of its children's</b> while a
    /// column's is its widest child's. So a column could be handed
    /// 27% of the window and Godot would overrule it the instant the player selected a
    /// building. <b>Measured with the probe rather than guessed</b>
    /// (<see cref="ProbeColumnWidths"/>): the idle row wanted <b>733</b> pixels, the ground row
    /// 548 and the build-queue row 459, against a column that is 267 on Joe's window. **Over
    /// half his screen, decided by one sentence.**
    /// </para>
    /// <para>
    /// <b>Two changes, and both are about minimum width rather than about layout.</b> The
    /// caption <em>wraps</em>, so a long sentence grows downwards; and the controls sit in an
    /// <see cref="HFlowContainer"/>, whose minimum width is its <em>widest single child</em>
    /// rather than the sum — so three buttons that will not fit side by side stack instead of
    /// forcing the column open. A wide window still draws them on one line and looks exactly as
    /// it did.
    /// </para>
    /// <para>
    /// <b>D114 reached this answer once already</b>, for the build menu: *"the caption sits
    /// above its group rather than beside it, which is a width decision."* It was right, and it
    /// was applied to one panel.
    /// </para>
    /// </remarks>
    private static (VBoxContainer Row, HFlowContainer Controls) InspectorRow(
        Control parent, Label caption)
    {
        var row = new VBoxContainer { Visible = false };
        row.AddThemeConstantOverride("separation", 4);

        row.AddChild(Wrapped(caption));

        var controls = new HFlowContainer();
        controls.AddThemeConstantOverride("h_separation", 6);
        controls.AddThemeConstantOverride("v_separation", 4);
        row.AddChild(controls);

        parent.AddChild(row);
        return (row, controls);
    }



    /// <summary>Turn one kind of goods on or off for the selected store.</summary>
    private void SetSelectedStocking(Stocking state)
    {
        if (SelectedStore() is not StoreBuilding store)
        {
            return;
        }

        Warn(_loop.World.SetStocking(store, state));
        RefreshInspector(_loop.World);
    }

    private void ToggleSelectedAccepts(Goods goods)
    {
        if (SelectedStore() is not StoreBuilding store)
        {
            return;
        }

        Warn(_loop.World.SetStoreAccepts(store, goods, !store.Accepts(goods)));
        RefreshInspector(_loop.World);
    }

    /// <summary>Silence, or restore, the idle ring on the selected workplace.</summary>
    private void ToggleSelectedIdleMarker()
    {
        if (SelectedWorkplace() is not { IsSite: false } workplace)
        {
            return;
        }

        _map.ToggleIdleMarker(workplace.Id);
        RefreshInspector(_loop.World);
    }

    /// <summary>Silence, or restore, the full-store ring on the selected store.</summary>
    private void ToggleSelectedFullMarker()
    {
        if (SelectedStore() is not StoreBuilding store)
        {
            return;
        }

        _map.ToggleFullMarker(store.Id);
        RefreshInspector(_loop.World);
    }

    /// <summary>The store on the selected tile, if the selection is one.</summary>
    private StoreBuilding? SelectedStore()
    {
        if (_selectedTile is not GridPos tile)
        {
            return null;
        }

        return _loop.World.StoreAt(tile);
    }

    /// <summary>Hand the ground brush to whichever building is selected (D86).</summary>
    private void PaintGroundForSelection(int direction)
    {
        if (SelectedWorkplace() is { IsSite: false } workplace)
        {
            _map.BeginPaintingGround(workplace.Id, direction);
        }
    }

    /// <summary>Switch a forester's hut between taking trees down and putting them back.</summary>
    private void ToggleSelectedMode()
    {
        if (SelectedWorkplace() is not { IsSite: false } workplace)
        {
            return;
        }

        workplace.Mode = workplace.Mode == WorkMode.FellAndPlant
            ? WorkMode.PlantOnly
            : WorkMode.FellAndPlant;
        RefreshInspector(_loop.World);
    }

    /// <summary>Move the selected construction site one place along the queue.</summary>
    private void MoveSelectedInQueue(int places)
    {
        Workplace? site = SelectedWorkplace();
        if (site?.Construction is not null)
        {
            _loop.World.MoveInBuildQueue(site, places);
            RefreshInspector(_loop.World);
        }
    }

    /// <summary>
    /// The two standing instructions the player gives the village: who works, and how much.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>⭐ A panel rather than two slide-ups off the toolbar</b> (Joe: *"the professions and
    /// stock limits slide-up menus suck for UX"*). The old shape had them unfold the control
    /// bar <em>upward over the map</em>, which is the worst possible place for them: these are
    /// the two controls whose whole purpose is watching what changes when you move them, and
    /// they covered the thing that changes.
    /// </para>
    /// <para>
    /// <b>Both, together, because they are one question asked twice.</b> A profession says how
    /// many hands on this work; a stock limit says until when. D62 and D106 both call them
    /// halves of one control, and putting them in one panel is that finally being true on
    /// screen rather than only in the decisions log.
    /// </para>
    /// <para>
    /// <b>Collapsible and left open by default</b> — they are standing orders, not a dialog you
    /// dismiss, and the numbers beside them (*"200 · have 214"*) are worth watching while the
    /// year runs.
    /// </para>
    /// </remarks>
    private void BuildProfessionsPanel()
    {
        // ⚠️ ROLLED UP BY DEFAULT, and that is the whole point rather than a compromise. These
        // are STANDING ORDERS — you set them and then watch the year — so the panel's resting
        // state should be a strip of title, not eleven rows of numbers competing with the
        // valley. Open, it is tall enough to reach the roster and the control bar; closed, it
        // costs one line. Joe asked for less on screen, and a panel that is only there when it
        // is wanted is more of an answer than a smaller one that is always there.
        // ⛔⛔ A FLOATING WINDOW, NOT A COLUMN PANEL, AND THE TABLE WAS WHY. A side column clamps
        // to 240–400 logical px, and a `GridContainer`'s minimum width is the SUM of its column
        // minimums — so a table with a notes column in it re-opened the exact bug recorded at
        // `BuildInspectorPanel` (an idle row wanting 733px against a 267px column). ⭐ The notes
        // column is gone (D367) and the table is glyph + name + stepper, so the window is the
        // ordinary width now — Joe: *"professions is too wide."*
        VBoxContainer body = Floating(
            Edge, Edge, DefaultPanelWidth, 0f, Corner.TopLeft, "Professions", startOpen: false);

        // ⚠️ Taken off the end of `_panels` the way `BuildSettingsPanel` does, because
        // `Dress` owns the registration and handing the panel back would be a second way to
        // do it. It starts HIDDEN but OPEN, which are two different things and both matter:
        // standing orders are set and then watched, so the resting state of the screen is
        // the valley — but a player who presses the button wants the table, not a title bar
        // they then have to unfold.
        // Registered so "Reset window positions" reaches it too — Joe asked for *all* panels,
        // and a window the reset cannot find is a window that can still be lost.
        _docked.Add((_panels[^1], false));
        _professionsPanel = _panels[^1];
        // ⭐ ON SCREEN BUT ROLLED UP (Joe, 2026-09-07). Hidden meant a player had to know it existed
        // and go to Settings to find it; folded means it is a title bar they can open in one click.
        // *A panel you cannot see and a panel you have not opened are different states.*
        _professionsPanel.Visible = true;

        // ⭐ What the village HAS, before what it is doing with it. The old panel opened with a
        // "Laborer" row among the trades, which read as an eighth profession rather than as the
        // pool the other seven are drawn from.
        // ⚠️ Clipped, not wrapped: the one line whose numbers grow with the village, and a
        // wrapped line is a taller panel every time it does (D367).
        _laborerReadout = Body(string.Empty);
        _laborerReadout.ClipText = true;
        _laborerReadout.TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis;
        body.AddChild(_laborerReadout);
        body.AddChild(Caption("Laborers are the spare hands: clearing ground, hauling and tidying."));

        body.AddChild(BuildProfessionsTable());
        body.AddChild(BuildTheNotHiredYet());
    }

    /// <summary>
    /// The stock limits, in <b>their own window</b> (Joe, 2026-09-05).
    /// </summary>
    /// <remarks>
    /// <para>
    /// *"Separate the resource limits into its own panel."* ⭐ **They were only ever together
    /// because both were lists of numbers**, which is a reason to put two things in one panel and
    /// not a good one: professions are about **who does what**, limits are about **when to stop**,
    /// and stacking nine goods under eight trades made a window tall enough to cover the valley.
    /// </para>
    /// <para>
    /// ⚠️ Its own corner, so opening both does not bury one behind the other — and both are
    /// draggable now anyway.
    /// </para>
    /// </remarks>
    private void BuildStockLimitsPanel()
    {
        VBoxContainer body = Floating(
            Edge + DefaultPanelWidth + 16f, Edge, StockLimitsWidth, 0f, Corner.TopLeft, "Stock limits", startOpen: true);

        _docked.Add((_panels[^1], false));
        _stockLimitsPanel = _panels[^1];
        _stockLimitsPanel.Visible = false;
        _windows[^1].Wanted = false;

        body.AddChild(Caption("How much to keep before the work stops."));
        body.AddChild(BuildStockLimitTable());
    }

    /// <summary>
    /// ⭐ The professions table — <b>type, what you asked for against what there is room for, and
    /// what the village makes of it</b>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Joe's mockup, built. The three-line-per-trade stack it replaces existed only because there
    /// was no table: a name padded to 84px, a stepper, then a wrapped sentence indented under it.
    /// Columns say the same things and line up while doing it.
    /// </para>
    /// <para>
    /// ⛔ <b>There is no notes column any more (D367).</b> It wrapped rather than widened, which
    /// kept the width honest and let the HEIGHT move with every clause the sim produced — Joe:
    /// *"I hate how the size of the panels change when information updates."* The sentence is the
    /// trade name's tooltip now, with a ⚠ on the name while a warning stands.
    /// </para>
    /// </remarks>
    private GridContainer BuildProfessionsTable()
    {
        var table = new GridContainer { Columns = 3 };
        table.AddThemeConstantOverride("h_separation", 6);
        table.AddThemeConstantOverride("v_separation", 1);

        table.AddChild(new Control());
        table.AddChild(Muted("TYPE"));
        table.AddChild(Muted("ASSIGNED / MAX"));

        foreach (JobKind kind in JobLimits.Kinds)
        {
            AddProfessionRow(table, kind);
        }

        return table;
    }

    /// <summary>Three cells for one trade.</summary>
    private void AddProfessionRow(GridContainer table, JobKind kind)
    {
        table.AddChild(new TradeGlyph(kind));

        // ⚠️ A Label ignores the mouse unless told otherwise, and a tooltip needs the mouse.
        //
        // ⛔ AND A FIXED WIDTH, OR THE ⚠ WIDENS THE COLUMN (D374, Joe: *"the alert symbol changes
        // the width of the panel"*). `Amount()`'s trio — the overrun behaviour is the bound
        // (D367): a Label with trimming set stops counting its text toward its minimum.
        Label name = Body(ProfessionName(_loop.World, kind));
        name.MouseFilter = MouseFilterEnum.Pass;
        name.ClipText = true;
        name.TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis;
        name.CustomMinimumSize = new Vector2(ProfessionNameWidth, 0f);
        table.AddChild(name);

        // ---- assigned / max ----
        var stepper = new HBoxContainer();
        stepper.AddThemeConstantOverride("separation", 4);

        // ⚠️ SEEDED AT ZERO AND MEANT (D136). Every profession carries an explicit number from the
        // first frame; a laborer is not an unemployed villager, so an unstaffed founding is four
        // people doing the work that is on the map.
        int asked = 0;

        Label amount = Body("0");
        amount.CustomMinimumSize = new Vector2(18, 0);
        amount.HorizontalAlignment = HorizontalAlignment.Right;

        var fewer = new Button { Text = "−", Flat = true };
        var more = new Button { Text = "+", Flat = true };

        Label seats = Muted(string.Empty);
        seats.CustomMinimumSize = new Vector2(26, 0);

        void Apply()
        {
            amount.Text = $"{asked}";
            Warn(_loop.World.SetJobLimit(kind, asked));
        }

        fewer.Pressed += () =>
        {
            asked = System.Math.Max(0, asked - 1);
            Apply();
        };

        more.Pressed += () =>
        {
            int spoken = 0;
            foreach (JobKind trade in System.Enum.GetValues<JobKind>())
            {
                spoken += _loop.World.JobLimits.For(trade) ?? 0;
            }

            int hands = _loop.World.AbleAdults;
            if (spoken >= hands)
            {
                Warn(PlacementVerdict.Yes(
                    $"There are only {hands} able "
                    + $"{(hands == 1 ? "adult" : "adults")} in {_loop.World.Name}, and all of "
                    + $"{(hands == 1 ? "them is" : "them are")} already spoken for. Take somebody "
                    + "off another kind of work first."));
                return;
            }

            asked++;
            Apply();
        };

        stepper.AddChild(fewer);
        stepper.AddChild(amount);
        stepper.AddChild(more);
        stepper.AddChild(seats);
        table.AddChild(stepper);

        _professionReadouts.Add((kind, seats, name));
        Apply();
    }

    /// <summary>
    /// The stock limits table — <b>resource, limit, clear, have</b>.
    /// </summary>
    /// <remarks>
    /// ⚠️ <b>Bounded by the ENUM where the overview's goods table is bounded by the CATALOGUE</b>,
    /// so a mod-added good gets an overview row and no limit row. That mismatch predates this
    /// redesign and is not its to fix, but it is written here rather than left to be rediscovered:
    /// <c>EveryGoodTheGameHasCanBeLimited</c> currently pins the enum bound.
    /// </remarks>
    private GridContainer BuildStockLimitTable()
    {
        var table = new GridContainer { Columns = 5 };
        table.AddThemeConstantOverride("h_separation", 6);
        table.AddThemeConstantOverride("v_separation", 1);

        table.AddChild(new Control());
        table.AddChild(Muted("RESOURCE"));
        table.AddChild(Muted("LIMIT"));
        table.AddChild(new Control());
        table.AddChild(Muted("HAVE"));

        foreach (Goods goods in StockLimits.Kinds)
        {
            AddStockLimitRow(table, goods);
        }

        return table;
    }

    // Six hand-worked panel sizes used to live here and just above — a width and a height for
    // the status panel, the log, the roster, the inspector and this one. Every last one was
    // dead: since the columns arrived nothing reads a panel's position or size but the column
    // it is in. Deleted rather than left, on D98's rule that a number nothing reads is a lie
    // waiting to be found — and these were the exact numbers whose hand-tuning D113 replaced.

    /// <summary>Speed, what the map draws, and what the player can ask the village for.</summary>
    private void BuildControlPanel()
    {
        // ⭐ SPANS THE WINDOW (2026-08-27). It hung off the bottom-right corner and took its
        // width from whatever the widest row demanded — which is how it came to run off the
        // LEFT edge, since a panel pinned right grows leftward without limit. Pinned on both
        // sides it can only ever be as wide as the window, and the rows below wrap inside it.
        VBoxContainer body = Floating(Edge, Edge, 0, 0, Corner.BottomRight, spanWidth: true);
        body.AddThemeConstantOverride("separation", 8);
        _controlBar = body;

        // ⛔ THE SPEED BAR WRAPS TOO — Joe's screenshot shows **"Pause" clipped to "use"** on the
        // left edge, which is this row overflowing rather than the build row below it. Ten
        // controls including *"Centre on village"* is already wider than a narrow window, and
        // nothing here was ever going to get shorter. Same fix, same reason: an
        // `HBoxContainer` cannot fail gracefully, and the first thing it throws away is the
        // control the player reaches for most.
        var controls = new HFlowContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        controls.AddThemeConstantOverride("h_separation", 6);
        controls.AddThemeConstantOverride("v_separation", 4);
        body.AddChild(controls);

        controls.AddChild(SpeedButton("Pause", 0.0));
        controls.AddChild(SpeedButton("1x", 1.0));
        controls.AddChild(SpeedButton("2x", 2.0));
        controls.AddChild(SpeedButton("4x", 4.0));
        controls.AddChild(SpeedButton("10x", 10.0));

        // ⭐ 20x (Joe, 2026-08-29, for QA: *"i also sometimes dont want to spend 20 minutes
        // starting up to get to a later-stage QA item"*).
        //
        // ⚠️ AND IT IS NOWHERE NEAR THE CEILING, WHICH IS WORTH KNOWING BEFORE ANYONE ADDS 40x.
        // `target_ticks_per_second` is **0.75**, so 20x asks for 15 ticks a second — a quarter of
        // a tick per frame at 60fps, against `max_ticks_per_frame` of 250. **The spiral guard does
        // not begin to bite until roughly 20,000x.** The limit is not the driver and it is not the
        // sim: measured, the sim runs **58 years in 1.53 seconds** (18,218 ticks/sec, ~24,000x).
        // *The speed buttons are a pacing choice, not a performance one* — which is why the answer
        // to "get me to year 58" is `SkipYears` below and not a bigger number here.
        controls.AddChild(SpeedButton("20x", 20.0));

        _speedLabel = Body(string.Empty);
        controls.AddChild(_speedLabel);

        AddTheSkipControls(controls);

        controls.AddChild(new VSeparator());

        // ⛔⛔ ROUTES, GROUND, PROFESSIONS AND LIMITS HAVE LEFT THIS BAR (Joe, 2026-09-06:
        // *"move all of these from the control bar into settings"*). ⭐ **Professions and Limits
        // needed no new home** — every titled panel is registered in `_windows` by `Floating`,
        // so both have been switchable from Settings since the day they were written, and
        // adding them again would have made two controls for one state. *He asked not to
        // duplicate them and the registry is why that was already true.*
        // ⭐ Routes and Ground move to Settings' **"On the map"** group, beside the full-store
        // and idle markers and the wildlife — every one of them answers *"what is drawn on the
        // valley"*, which is the question this bar had no business holding a quarter of.
        // ⚠️ They are still BUTTONS rather than checkboxes: Routes is a three-way cycle and
        // Ground is on/off-with-a-reading, and a checkbox cannot say either.

        // With a valley this size and free panning, getting lost is easy and a way
        // back is not optional.
        var recentre = new Button { Text = "Centre on village" };
        recentre.Pressed += () => _map.CentreOnTheVillage();
        controls.AddChild(recentre);

        // ⭐ THE WAY BACK TO EVERY WINDOW YOU SWITCHED OFF (Joe). It lives on the control bar
        // rather than in a panel, because the control bar is the one thing that is always
        // there — a settings menu reachable only from a window you might have hidden would be
        // a door that locks behind you. **It is the last of the four to survive here, and it
        // is the one that cannot move**: Settings is now the door to Routes, Ground,
        // Professions and Limits alike, so a Settings button hidden inside Settings would lock
        // every one of them away at once.
        var settings = new Button { Text = "Settings" };
        settings.Pressed += ToggleSettings;
        controls.AddChild(settings);

        // ⭐ THE TABS SIT WITH THE SPEED CONTROLS, NOT ABOVE THEM (Joe's mockup). One strip is
        // the whole point: *"give me more room to see the game map"* (D305) was answered by
        // shrinking the furniture, and this answers the other half by having less of it.
        controls.AddChild(new VSeparator());
        AddTheTabs(controls);

        // Uncaptioned and on the end, because it belongs to no tab — it puts down whichever
        // tool is in your hand, including the harvest brushes two rows below.
        controls.AddChild(TheCancelButton());

        // ⭐ THE FILTER, AND IT IS A ROW OF ITS OWN BECAUSE IT ANSWERS A DIFFERENT QUESTION.
        // The tabs say what KIND of act; the chips say which buildings are worth looking at
        // right now. `TECH-EXAMPLE.md` names forty-five buildings, and forty-five icons in one
        // strip is a search rather than a browse — which is §1.2's click-farm in another medium.
        _filterRow = FlowRow();
        _filterRow.AddChild(Muted("Show:"));
        AddTheFilters(_filterRow);

        // ⛔⛔ THE BAR MUST NOT CHANGE HEIGHT WHEN THE TAB CHANGES (Joe, 2026-09-06: *"the bar
        // height collapses if a category doesn't have a 'show' filter. and its weird that the
        // bar changes heights. set it to the same height across all 3."*). **Hiding this row
        // was what moved it** — only BUILD has categories to filter, so two tabs in three drew
        // a two-row bar and one drew three, and the map jumped every time he switched.
        //
        // ⭐ **THE ROW STAYS; ITS CONTENTS SWAP.** Reserving a blank strip would have held the
        // height and said nothing — this holds the height and answers *"what does this tab
        // do?"*, which REMOVAL and HARVEST had no line for at all. *A constant height bought
        // with a sentence costs the same pixels as one bought with a spacer.*
        // ⚠️ `_harvestNote` used to live in the strip row below and is folded in here, because
        // two tabs owning two different sentences in two different rows is how the height
        // started varying in the first place.
        _tabNote = Muted(string.Empty);
        _filterRow.AddChild(_tabNote);

        // ⭐⭐ THE BRUSH'S SHAPE, BESIDE THE BRUSH RATHER THAN IN SETTINGS (Joe's call, D327).
        // ⛔ **On the filter row, not the tool strip**, and the reason is measured rather than
        // aesthetic: the strip already wraps to two rows on BUILD + ALL, so a fourth tool button
        // there is a candidate third row — and the bar's height is pinned across every tab × filter
        // from the tallest of them. **This row is one line on every tab and has room.**
        // ⚠️ It is a BUTTON that reads its own state rather than a checkbox, per the rule this bar
        // already follows: a checkbox cannot say which of two shapes is chosen.
        _shapeButton = new Button { Text = string.Empty, TooltipText = "The brush's shape (B)" };
        _shapeButton.Pressed += () => _map.CycleBrushShape();
        _filterRow.AddChild(_shapeButton);
        _map.BrushChanged += RelabelTheBrush;
        RelabelTheBrush();

        body.AddChild(_filterRow);

        _stripRow = FlowRow();
        body.AddChild(_stripRow);
        BuildTheStrip();
        RefreshTheStrip();

        // ⭐ THE BAR RELIGHTS ITSELF FROM THE MAP, NOT FROM THE BUTTON THAT WAS PRESSED. Wiring
        // each button to light its own tab would go wrong the moment anything else changed the
        // tool — right-clicking to cancel, or the ground brush being handed over from a
        // building's panel. **One source, and it is the thing that actually knows.**
        _map.ToolChanged += RelightTheStrip;

        // ⚠️ PROFESSIONS AND STOCK LIMITS HAVE LEFT THIS BAR (Joe: *"the professions and stock
        // limits slide-up menus suck for UX"*). They were toggles that unfolded the control bar
        // upward over the map — so the two controls you most want to watch the effect of were
        // the two that hid the effect. They are their own collapsible panel now, on the left,
        // where they can be left open while the village gets on with it.

        // The refusal or the warning, in the words the sim already produced — on its own
        // line rather than squeezed between the buttons. Same standard as JobReason: a
        // red square on its own is the shrug this project keeps refusing, and a sentence
        // that has to share a row with nine buttons is a sentence nobody finishes.
        _placementLabel = Wrapped(Body(string.Empty));
        _placementLabel.Modulate = new Color(1f, 0.78f, 0.35f);
        body.AddChild(_placementLabel);
        _map.PlacementMessageChanged += message =>
        {
            _placementLabel.Text = message;
            _placementLabel.Visible = message.Length > 0;
        };
        _placementLabel.Visible = false;

        body.AddChild(Wrapped(Muted(
            "space to pause · 1-4 speed · WASD pan · wheel zoom · r turn · tab routes · "
            + "g ground · "
            + "home recentre · c fold panels · h hide them")));
    }

    // ---------------------------------------------------------------
    //  The furniture the panels are made of
    // ---------------------------------------------------------------

    /// <summary>Which corner a floating panel is pinned to.</summary>
    private enum Corner { TopLeft, TopRight, BottomLeft, BottomRight }

    /// <summary>Every floating panel, with the corner it hangs off, so scaling holds that corner.</summary>
    private readonly List<(PanelContainer Panel, bool Right, bool Bottom, bool Spans)> _floaters = new();

    /// <summary>Scale every floating panel about the corner it hangs off.</summary>
    /// <remarks>
    /// ⚠️ <b>The pivot is the anchored corner</b>, the same rule the columns follow: scaling about
    /// the origin would walk a bottom-anchored bar up off its edge and a right-anchored panel in
    /// from the window. Set every frame rather than once, because the pivot depends on the panel's
    /// own size and that changes with its contents.
    /// </remarks>
    private void FitFloaters()
    {
        for (int i = 0; i < _floaters.Count; i++)
        {
            (PanelContainer panel, bool right, bool bottom, bool spans) = _floaters[i];

            // ⛔⛔ A BAR THAT SPANS THE WINDOW HAS TO BE WIDENED BEFORE IT IS SCALED, OR SHRINKING
            // THE UI MAKES IT TALLER. The control bar is an `HFlowContainer`: it wraps to fit its
            // width, so scaling it to four fifths leaves it four fifths as WIDE, which wraps more
            // rows and eats more of the valley than it saved. **The opposite of what was asked
            // for**, and it would have looked like the dial was backwards.
            //
            // ⭐ So its unscaled width is set to `window / scale`, and scaling brings it back to
            // exactly the window. The bar keeps its full width and only its CONTENTS get smaller,
            // which is the thing Joe was actually asking to shrink.
            if (spans)
            {
                float wanted = (Size.X - (Edge * 2f)) / Mathf.Max(0.01f, _uiScale);
                panel.OffsetLeft = Edge;
                panel.OffsetRight = Edge + wanted - Size.X;

                // ⭐ AND ONCE IT IS AS WIDE AS IT IS GOING TO BE, PIN HOW TALL IT IS. Width has
                // to be settled first: the strip wraps to the width it is given, so measuring
                // the height before this line would measure a bar of the wrong shape.
                PinTheBarHeight(wanted);
            }

            panel.PivotOffset = new Vector2(
                right && !spans ? panel.Size.X : 0f,
                bottom ? panel.Size.Y : 0f);

            panel.Scale = new Vector2(_uiScale, _uiScale);
        }
    }

    /// <summary>
    /// What still has to happen every frame now the columns are gone.
    /// </summary>
    /// <remarks>
    /// ⛔ <b>THE COLUMNS WENT (2026-09-06) AND TOOK MOST OF THIS WITH THEM.</b> It used to size
    /// two <c>ScrollContainer</c>s against the window, reserve room above the control bar, and
    /// scale them — all of it there to lay panels out automatically. <b>Panels are windows the
    /// player arranges now</b>, so the only thing left is the scaling, which
    /// <see cref="FitFloaters"/> does for every panel at once.
    /// </remarks>
    private void FitColumns()
    {
        FitFloaters();

        // ⭐ ONCE, AND NOT ON THE FIRST FRAME. A panel has no settled height until Godot has run
        // a layout pass or two over it, and arranging against a stale height stacks the windows
        // ON TOP of one another — measured: three right-hand panels landing at y=14, 61 and 260
        // when 14, 194 and 400 were wanted. **The probe waits twenty frames for the same reason.**
        //
        // ⚠️ And then NEVER AGAIN automatically. Re-running it is precisely what Joe objected to:
        // *"adding or removing windows from the settings panel resets the position of all four
        // right-side-default panels — it shouldn't do that."*
        if (!_arranged && Size.X > 0f && ++_settling > 8)
        {
            ArrangeDefaults();
            _arranged = true;
        }

        if (_arranged)
        {
            KeepWindowsOnScreen();
        }
    }


    /// <summary>
    /// How much of its natural size a side column is drawn at — <b>four fifths</b> (Joe, 2026-08-30).
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>⭐ *"Make all of the UI info panels 20% smaller (height and width), to give more UI space
    /// back to the user."*</b> Applied as one scale on the two columns rather than as twenty edits
    /// to font sizes and paddings — which is what "20% smaller" actually means, keeps every panel
    /// in proportion with every other, and is one number to change if he wants it back.
    /// </para>
    /// <para>
    /// ⚠️ <b>THE LAYOUT IS DONE IN UNSCALED UNITS AND THEN SCALED, WHICH IS WHY <see
    /// cref="FitColumns"/> DIVIDES.</b> A column is laid out to a logical width and height and
    /// drawn at 0.8 of it, so the space it is allowed to occupy on screen has to be converted
    /// back into logical units before it is handed to the container — otherwise the panels would
    /// shrink AND stop using the room they were given, which is a fifth of the screen wasted
    /// rather than returned.
    /// </para>
    /// <para>
    /// ⛔ <b>The pivot is the column's own outer edge</b>, so a scaled panel hugs the side of the
    /// window it is anchored to. Scaling about the default top-left would leave the right-hand
    /// column floating twenty per cent of a column away from the edge — a gap that reads as a
    /// layout bug rather than as more room.
    /// </para>
    /// </remarks>
    /// <summary>
    /// How big the furniture is drawn, against the window — <b>one dial for every panel</b>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Joe, 2026-09-05:</b> *"These all need to be smaller. Including the build bar. Smaller
    /// please. Give me more room to see the game map."*
    /// </para>
    /// <para>
    /// ⛔⛔ <b>IT USED TO REACH THE COLUMNS AND NOTHING ELSE, WHICH IS WHY THE PROFESSIONS WINDOW
    /// DWARFED EVERYTHING.</b> `FitColumns` scaled the two side columns and floating panels were
    /// never in that list — so the new table and the control bar were drawn at **full size beside
    /// panels drawn at four fifths**, and looked 25% too big because they were.
    /// </para>
    /// <para>
    /// ⭐ <b>A setting rather than a constant</b> (his call): one number he can turn until the map
    /// has the room he wants, instead of four hand-tuned font sizes that drift apart. Clamped so
    /// it cannot be driven to something unreadable or off-screen.
    /// </para>
    /// <para>
    /// ⭐ <b>The default is 75%</b> (Joe, 2026-09-06, having played the build bar at 80%). It is
    /// the <em>opening</em> value only — the dial still runs 55–115% and the player's turn of it
    /// wins. *A default is what the map looks like before anybody has opened Settings, which is
    /// the only view most players will ever judge it on.*
    /// </para>
    /// </remarks>
    private float _uiScale = 0.75f;

    private const float MinUiScale = 0.55f;
    private const float MaxUiScale = 1.15f;

    /// <summary>A panel stacked into one of the side columns.</summary>
    /// <remarks>
    /// <para>
    /// <b>The ordinary way to add a panel now.</b> It takes no position at all — the column
    /// decides that, which is the point: an offset somebody works out by hand is an overlap
    /// waiting to happen the next time a panel is added or grows.
    /// </para>
    /// <para>
    /// A <paramref name="height"/> of zero means <em>as big as the contents need</em>: Godot
    /// clamps a control to its own minimum size, so a panel asked for nothing grows to fit and
    /// then stops. That is what lets the status panel swell by a line when the village starts
    /// asking for something.
    /// </para>
    /// <para>
    /// <b>Mouse filter set to Stop, explicitly.</b> A Container defaults to Pass, so every
    /// click on a panel would have gone through to the map behind it — and the map covers the
    /// whole window, so "behind it" means placing a granary under the button you just pressed.
    /// </para>
    /// </remarks>
    /// <summary>
    /// A window that <b>starts</b> on one side of the screen and is free after that.
    /// </summary>
    /// <remarks>
    /// <para>
    /// ⛔⛔ <b>THESE USED TO LIVE INSIDE A SCROLLING COLUMN, AND THAT IS WHAT JOE HIT.</b> Three of
    /// his four complaints were one cause: *"most of them cannot go overtop of the middle 1/3rd of
    /// the UI, they go underneath"* and *"adding or removing windows from the settings panel resets
    /// the position of all four right-side-default panels."*
    /// </para>
    /// <para>
    /// **A `ScrollContainer` clips its children — it has to, that is what scrolling is** — so a
    /// panel dragged out of its column was cut off at the column's edge rather than moving. And a
    /// `VBoxContainer` owns its children's positions, so hiding one shuffled the rest **and threw
    /// away wherever they had been put.** Neither is a bug in the columns; it is what columns are.
    /// They were simply the wrong container for windows the player can move.
    /// </para>
    /// <para>
    /// ⭐ So every panel is free-floating now, and the side is only a <b>starting position</b>,
    /// arranged once by <see cref="ArrangeDefaults"/> and never again — which is the whole of what
    /// he asked for: *"it shouldn't do that. 'reset all panels' could maybe be a settings button."*
    /// </para>
    /// <para>
    /// ⚠️ <b>A panel that asked the column for a height needs its own scroll now.</b> The column
    /// was doing that job; a roster of forty villagers in a 190px window would otherwise simply
    /// run off the bottom of it.
    /// </para>
    /// </remarks>
    private VBoxContainer InColumn(
        bool right, float height, string? title = null, bool startOpen = true)
    {
        VBoxContainer contents = Floating(
            Edge, Edge, DefaultPanelWidth, 0f,
            right ? Corner.TopRight : Corner.TopLeft, title, startOpen);

        _docked.Add((_panels[^1], right));

        if (height <= 0f)
        {
            return contents;
        }

        var scroll = new ScrollContainer
        {
            HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled,
            VerticalScrollMode = ScrollContainer.ScrollMode.Auto,
            CustomMinimumSize = new Vector2(0, height),
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
        };

        var inner = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        inner.AddThemeConstantOverride("separation", 4);
        scroll.AddChild(inner);
        contents.AddChild(scroll);

        return inner;
    }

    /// <summary>How wide a panel starts. It is free to be dragged, not resized.</summary>
    private const float DefaultPanelWidth = 300f;

    /// <summary>
    /// How tall the inspector's contents are, whatever is selected (D367) — logical px, scaled
    /// with the rest. ⚠️ Measured, not guessed: the probe poses a market-sized description (the
    /// longest in the game) with its store rows and prints the content height beside this number.
    /// </summary>
    private const float InspectorHeight = 460f;

    /// <summary>
    /// The stock-limits window's width: five columns with a spin box and a wrapped readout want
    /// 360, and a window given less than it wants is a window that grows (the probe's
    /// <c>widened</c> check, D367). The readout wraps, so it wants no more than this.
    /// </summary>
    private const float StockLimitsWidth = 360f;

    /// <summary>Panels that have a default side, in the order they were built.</summary>
    private readonly List<(PanelContainer Panel, bool Right)> _docked = new();

    /// <summary>
    /// Put every window back where it started — <b>once at launch, and on request</b>.
    /// </summary>
    /// <remarks>
    /// ⭐ Joe: *"'reset all panels' could maybe be a settings button."* Run once after the first
    /// layout pass so the panels have real heights to stack by, and then **never automatically
    /// again** — the entire complaint was windows moving when he had not asked them to.
    /// </remarks>
    private void ArrangeDefaults()
    {
        // ⭐ THE LEFT COLUMN STARTS UNDER THE TOP BARS (D378); the right one still starts at the
        // top, because the minimap is top-right and the bar is content-sized on the left. The
        // debug frame counter sits in the gap under the bar, where it can actually be read — it
        // used to sit at the corner, under the Overview.
        float leftY = TopOfTheLeftColumn();
        float rightY = Edge;

        if (_frameCounter is not null)
        {
            _frameCounter.Position = new Vector2(Edge, leftY - Edge);
        }

        // The passing banner is centred at the top and 460 wide, which now runs under the
        // villagers box; it drops below the bar with the column.
        if (_passingPanel is not null)
        {
            _passingPanel.OffsetTop = leftY;
        }

        for (int i = 0; i < _docked.Count; i++)
        {
            (PanelContainer panel, bool right) = _docked[i];

            // ⚠️ A hidden window takes no room in the stack. Otherwise switching one off in
            // Settings would leave a gap where it used to be, which reads as a layout bug.
            if (!panel.Visible)
            {
                continue;
            }

            float tall = Mathf.Max(panel.Size.Y, panel.GetCombinedMinimumSize().Y) * _uiScale;

            // ⚠️ A stack taller than the window would push the last panels off the bottom with no
            // handle left to drag them back by — the same trap the drag clamp exists for. Once the
            // side is full, the rest start again at the top; overlapping is recoverable, off-screen
            // is not.
            float y = right ? rightY : leftY;
            if (y + tall > Size.Y - Edge)
            {
                y = Edge;
            }

            // ⛔⛔ THE BOTTOM EDGE IS NOT PINNED, AND PINNING IT IS WHAT BROKE FOLDING.
            // This used to write `OffsetBottom = y + panel.Size.Y`, which fixes the panel's
            // HEIGHT to whatever it happened to be at arrange time — so `contents.Visible =
            // false` hid the contents and **left the frame at full size.** Joe: *"the panels do
            // not minimize properly, the shape stays open but the content minimizes."* Measured
            // before the fix: all five panels `open 444 → folded 444`.
            // ⭐ Top and bottom equal means a zero-height rectangle that Godot clamps up to the
            // panel's own minimum — so the frame is **always exactly as tall as what is in it**,
            // which is what makes a fold a fold. *A window that reserves the room it is not
            // using costs the map the same pixels either way.*
            panel.OffsetTop = y;
            panel.OffsetBottom = y;

            if (right)
            {
                rightY = y + tall + Edge;
            }
            else
            {
                leftY = y + tall + Edge;
            }
        }
    }

    private bool _arranged;

    /// <summary>
    /// Where the left-hand stack — and a new card — may begin: just under the top bars, in
    /// screen pixels (D378).
    /// </summary>
    /// <remarks>
    /// The bar is laid out in logical units and drawn at <see cref="_uiScale"/>, so its drawn
    /// height is its size times the scale — the same conversion <see cref="ArrangeDefaults"/>
    /// makes for every panel it stacks. Asked of the bar's minimum as well as its size, because
    /// a panel that has not settled reports a size of nothing.
    /// </remarks>
    private float TopOfTheLeftColumn()
    {
        if (_topBar is null || !_topBar.Visible)
        {
            return Edge;
        }

        float tall = Mathf.Max(_topBar.Size.Y, _topBar.GetCombinedMinimumSize().Y) * _uiScale;
        return Edge + tall + Edge;
    }

    private int _settling;

    private VBoxContainer Floating(
        float x,
        float y,
        float width,
        float height,
        Corner corner,
        string? title = null,
        bool startOpen = true,
        bool spanWidth = false)
    {
        var panel = new PanelContainer { MouseFilter = MouseFilterEnum.Stop };

        // ⚠️ The bar is the one panel that stays see-through: it spans the window, so a solid
        // one would wall off the bottom of the valley. Every other window is something the
        // player reads, and the map showing through a table of numbers costs legibility.
        panel.AddThemeStyleboxOverride("panel", PanelSkin(spanWidth ? 0.72f : 1f));

        bool right = corner is Corner.TopRight or Corner.BottomRight;
        bool bottom = corner is Corner.BottomLeft or Corner.BottomRight;

        // ⭐⭐ `spanWidth` PINS BOTH SIDES, AND THE CONTROL BAR NEEDS IT TO WRAP AT ALL
        // (2026-08-27). Every other floating panel is sized BY ITS CONTENTS and hangs off one
        // corner, which is right for a panel and **wrong for a bar that must fill the window**.
        //
        // ⛔ **This is what my first attempt at D242 got wrong, and Joe caught it in one look:**
        // *"i think you messed it up. its tall and wide on the right side."* Swapping the bar to
        // an `HFlowContainer` removed the very thing that had been holding it open. An
        // `HBoxContainer`'s minimum width is **the sum of its children**, so the panel was
        // dragged wide (and then off the left edge — the original bug); a flow container's
        // minimum width is **its widest single child**, so the panel collapsed to one button and
        // wrapped everything into a tall column in the corner.
        //
        // ⚠️ **A wrapping container cannot decide where to wrap unless something else decides
        // how wide it is.** Flow containers do not create width; they consume it. The two
        // changes are one change and shipping either alone is a different bug.
        if (spanWidth)
        {
            panel.AnchorLeft = 0f;
            panel.AnchorRight = 1f;
            panel.OffsetLeft = x;
            panel.OffsetRight = -x;
            panel.GrowHorizontal = GrowDirection.Both;
        }
        else
        {
            panel.AnchorLeft = panel.AnchorRight = right ? 1f : 0f;
            panel.OffsetLeft = right ? -(x + width) : x;
            panel.OffsetRight = right ? -x : x + width;
        }

        panel.AnchorTop = panel.AnchorBottom = bottom ? 1f : 0f;
        panel.OffsetTop = bottom ? -(y + height) : y;
        panel.OffsetBottom = bottom ? -y : y + height;

        // WHICH WAY IT GROWS WHEN IT OUTGROWS THE SIZE IT WAS ASKED FOR, and the default
        // is wrong for half the corners. Godot clamps a control to its minimum size by
        // pushing its right and bottom edges outward, so a panel pinned to the
        // bottom-right and asked for nothing grew off the screen entirely — the controls
        // vanished, leaving one lit pixel in the corner. Panels pinned to an edge have to
        // grow away from it.
        if (!spanWidth)
        {
            panel.GrowHorizontal = right ? GrowDirection.Begin : GrowDirection.End;
        }

        panel.GrowVertical = bottom ? GrowDirection.Begin : GrowDirection.End;

        VBoxContainer floated = Dress(panel, title, startOpen);
        AddChild(panel);

        // ⭐ Registered so `FitColumns` can scale it with everything else. Before this the side
        // columns were drawn at `_uiScale` and every floating panel at full size, which is the
        // whole reason the professions window looked enormous beside the overview.
        _floaters.Add((panel, right, bottom, spanWidth));
        return floated;
    }

    /// <summary>
    /// Give a panel its collapsible title and its contents box. <b>The one panel mechanism.</b>
    /// </summary>
    /// <remarks>
    /// Shared by the columns and by the control bar deliberately: two ways of making a panel is
    /// how two layouts come to disagree about what a panel is, and this project has a standing
    /// record of what that costs (D76, five instalments).
    /// </remarks>
    /// <summary>
    /// Let a grip drag its panel around, and <b>leave it where it is dropped</b>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Joe, 2026-09-05:</b> *"it needs to be draggable/pinnable"* — and his call on what pinning
    /// means: **moving it IS pinning it.** A window stays where you put it for the session rather
    /// than snapping back to its corner.
    /// </para>
    /// <para>
    /// ⚠️ <b>The offsets move, not the position.</b> A panel is anchored to a corner and Godot
    /// recomputes `Position` from the anchors every layout pass, so writing `Position` directly
    /// would be undone on the next frame — the panel would judder back under the cursor. Nudging
    /// all four offsets moves the anchor itself, which is the thing layout reads.
    /// </para>
    /// <para>
    /// ⛔⛔ <b>IT MOVES IN SCREEN SPACE, AND THE FIRST DRAFT DID NOT.</b> Joe: *"the dragging feels
    /// sluggish and slow — like the window moves faster than the cursor and the distance between
    /// the two keeps growing."* **Exactly right, and it was arithmetic rather than feel.**
    /// </para>
    /// <para>
    /// A <c>_GuiInput</c> event arrives <b>already transformed into the control's local space</b>,
    /// and this panel is drawn at <c>_uiScale</c> — so <c>Relative</c> was already 1.25× the screen
    /// delta at 80%. Dividing by the scale on top of that applied <b>1.5625×</b>, the panel drew at
    /// 0.8 of it, and the window ran away from the pointer at 1.25× with the gap compounding for as
    /// long as the drag lasted.
    /// </para>
    /// <para>
    /// ⭐ <b>So the delta is taken from the global mouse instead</b>, which is in screen pixels
    /// whatever any ancestor is scaled to. That is one fact rather than a chain of two, and it
    /// stays correct if the scale ever moves mid-drag.
    /// </para>
    /// <para>
    /// ⚠️ <b>And it is clamped to the window</b> (Joe: *"panels aren't bound to the window and they
    /// can get stuck outside it with no way to move them"*). The grip is what has to stay
    /// reachable, so the clamp keeps a strip of the title bar on screen rather than the whole
    /// panel — a window half off the right edge is a legitimate thing to want, one whose only
    /// handle is off the edge is a lost window.
    /// </para>
    /// </remarks>
    private void MakeDraggable(Control grip, PanelContainer panel)
    {
        bool dragging = false;
        Vector2 last = Vector2.Zero;

        grip.GuiInput += @event =>
        {
            if (@event is InputEventMouseButton click && click.ButtonIndex == MouseButton.Left)
            {
                dragging = click.Pressed;
                last = GetGlobalMousePosition();
                grip.AcceptEvent();
                return;
            }

            if (!dragging || @event is not InputEventMouseMotion)
            {
                return;
            }

            Vector2 now = GetGlobalMousePosition();
            Vector2 by = now - last;
            last = now;

            // ⚠️ Set HERE rather than in `MovePanel`, because the centring goes through
            // `MovePanel` too and would otherwise mark the panel as dragged the first time it
            // opened — which would make the setting work exactly once.
            _settingsWasDragged |= panel == _settingsPanel;

            MovePanel(panel, by);
            grip.AcceptEvent();
        };
    }

    /// <summary>Shift a floating panel by a screen-space delta, keeping its handle reachable.</summary>
    /// <remarks>
    /// ⚠️ <b>The OFFSETS move, never <c>Position</c>.</b> Layout recomputes position from the
    /// anchors every frame, so a panel written to directly judders straight back under the cursor.
    /// </remarks>
    private void MovePanel(PanelContainer panel, Vector2 by)
    {
        Vector2 corner = DrawnTopLeft(panel);

        Vector2 want = corner + by;
        Vector2 allowed = ClampToWindow(panel, want);

        by = allowed - corner;

        panel.OffsetLeft += by.X;
        panel.OffsetRight += by.X;
        panel.OffsetTop += by.Y;
        panel.OffsetBottom += by.Y;
    }

    /// <summary>Where a panel's top-left corner actually lands on screen, scaling included.</summary>
    /// <remarks>
    /// <para>
    /// ⛔⛔ <b>THIS IS WHAT THE RIGHT-HAND WINDOWS WERE MISSING, AND IT IS WHY ONLY THEY WERE
    /// BROKEN.</b> Joe: *"the windows on the left side move smoothly. The windows on the right only
    /// display in a small column on the right side and still suffer from the drifting issue, and
    /// the inability to move left at all."*
    /// </para>
    /// <para>
    /// A right-anchored panel is pinned to <c>AnchorLeft = AnchorRight = 1</c>, so its
    /// <c>OffsetLeft</c> is <b>negative</b> — a distance back from the right edge, not a position
    /// on screen. The first clamp treated it as a screen X and compared <b>-314 against a floor of
    /// -254</b>, so every drag was shoved straight back to the right: the window could not go left,
    /// sat in a narrow band, and *appeared* to drift because it was refusing to follow a cursor
    /// that kept going. **Left-anchored panels have positive offsets, so they worked by accident.**
    /// </para>
    /// <para>
    /// ⚠️ <c>Position</c> is the laid-out rect whichever edge it is anchored to, so it is the
    /// honest quantity — but the panel is then SCALED about its pivot, and for a right-anchored
    /// panel that pivot is its right edge. So the drawn corner is the rect's corner pulled in by
    /// the width the scaling removed.
    /// </para>
    /// </remarks>
    private Vector2 DrawnTopLeft(PanelContainer panel)
    {
        Vector2 shrunk = panel.Size * (1f - _uiScale);

        return new Vector2(
            panel.Position.X + (panel.PivotOffset.X > 0.5f ? shrunk.X : 0f),
            panel.Position.Y + (panel.PivotOffset.Y > 0.5f ? shrunk.Y : 0f));
    }

    /// <summary>Nudge a wanted corner back until enough of the window is reachable.</summary>
    /// <remarks>
    /// ⚠️ <b>The title strip is what has to stay on screen, not the whole panel.</b> A window
    /// hanging half off the right edge is a legitimate thing to want; a window whose only handle is
    /// past the edge is one the player cannot get back.
    /// </remarks>
    private Vector2 ClampToWindow(PanelContainer panel, Vector2 corner)
    {
        const float Handle = 60f;

        float wide = panel.Size.X * _uiScale;
        float tall = panel.Size.Y * _uiScale;

        return new Vector2(
            Mathf.Clamp(corner.X, Handle - wide, Size.X - Handle),
            Mathf.Clamp(corner.Y, 0f, Mathf.Max(0f, Size.Y - Mathf.Min(Handle, tall))));
    }

    /// <summary>
    /// Shove every window back inside the screen — <b>every frame, not only while dragging</b>.
    /// </summary>
    /// <remarks>
    /// ⛔ Joe: *"all windows/panels can still be moved outside of the game window — they should not
    /// be able to."* Clamping only on drag left three other ways out: the default arrangement, the
    /// UI-size dial changing how much room a panel takes, and **the player resizing the window
    /// smaller**, which moves the edge rather than the panel. Checking every frame closes all of
    /// them at once and costs a comparison per window.
    /// </remarks>
    private void KeepWindowsOnScreen()
    {
        for (int i = 0; i < _docked.Count; i++)
        {
            PanelContainer panel = _docked[i].Panel;

            // ⚠️ A hidden panel has no settled size — Godot reports the unconstrained content
            // height, measured at 1,676 for a professions window that lays out at a fraction
            // of it. Clamping against that shoves the window somewhere wrong, and it gets
            // clamped honestly on the first frame it is actually shown.
            if (!panel.Visible)
            {
                continue;
            }

            Vector2 corner = DrawnTopLeft(panel);
            Vector2 allowed = ClampToWindow(panel, corner);

            if (allowed.IsEqualApprox(corner))
            {
                continue;
            }

            Vector2 by = allowed - corner;
            panel.OffsetLeft += by.X;
            panel.OffsetRight += by.X;
            panel.OffsetTop += by.Y;
            panel.OffsetBottom += by.Y;
        }
    }

    private VBoxContainer Dress(PanelContainer panel, string? title, bool startOpen)
    {
        var body = new VBoxContainer();
        body.AddThemeConstantOverride("separation", 4);
        panel.AddChild(body);

        // ⭐ A TITLE YOU CAN CLICK TO ROLL THE PANEL UP (Joe). The contents go in their own
        // box so the header can hide everything below it and leave a strip of title behind —
        // *"let me see more of what's happening in the game"* answered by letting the player
        // choose what is on screen moment to moment, rather than by guessing which panel they
        // wanted smaller.
        //
        // A title is what makes a panel collapsible, which is why the control bar has none:
        // rolling up the thing you press to do anything would be a trap.
        // ⚠️ AND IT EXPANDS, WHICH IS THE SAME BUG AS THE INSPECTOR ONE LEVEL DEEPER. A VBox
        // child defaults to Fill, not ExpandFill — so this box took only its own minimum
        // height, and anything inside it asking to expand was expanding into nothing. The
        // roster is an `ItemList` doing exactly that: it was being filled with five villagers
        // every frame and given no pixels to draw them in, which is Joe's *"there used to be a
        // list of all villagers, but that list doesn't show up now"*.
        //
        // **The inspector only escaped because I had just given its label a minimum height** —
        // a fix for the symptom that hid the cause. This is the cause.
        var contents = new VBoxContainer { SizeFlagsVertical = SizeFlags.ExpandFill };
        contents.AddThemeConstantOverride("separation", 4);

        if (title is not null)
        {
            var header = new Button
            {
                Text = startOpen ? $"▾ {title}" : $"▸ {title}",
                Flat = true,
                Alignment = HorizontalAlignment.Left,
                ToggleMode = true,
                ButtonPressed = startOpen,
            };

            contents.Visible = startOpen;

            header.AddThemeFontSizeOverride("font_size", 12);
            header.Modulate = new Color(1, 1, 1, 0.55f);
            header.Toggled += open =>
            {
                contents.Visible = open;
                header.Text = open ? $"▾ {title}" : $"▸ {title}";
            };

            // ⭐⭐ A SEPARATE GRIP RATHER THAN DRAGGING THE HEADER ITSELF (Joe: *"it needs to be
            // draggable"*). The header is a toggle: dragging it would mean guessing, on every
            // release, whether the player meant to move the panel or fold it — and guessing wrong
            // either strands the panel or folds it under the cursor. **A grip that only drags and
            // a title that only folds cannot be confused for one another**, which is the same
            // argument that gave the map its own brush modes rather than one clever click.
            var handle = new HBoxContainer();
            handle.AddThemeConstantOverride("separation", 0);

            var grip = new Button
            {
                Text = "⠿",
                Flat = true,
                TooltipText = "Drag to move this window",
                MouseDefaultCursorShape = CursorShape.Move,
            };

            grip.AddThemeFontSizeOverride("font_size", 12);
            grip.Modulate = new Color(1, 1, 1, 0.35f);
            MakeDraggable(grip, panel);
            handle.AddChild(grip);

            header.SizeFlagsHorizontal = SizeFlags.ExpandFill;
            handle.AddChild(header);

            // ✕, the card's own glyph, at the far right where the card keeps it (D380).
            var close = new Button { Text = "✕", Flat = true, TooltipText = "Close this window" };
            close.AddThemeFontSizeOverride("font_size", 12);
            close.Modulate = new Color(1, 1, 1, 0.55f);
            close.Pressed += () => CloseTheWindow(panel);
            handle.AddChild(close);

            body.AddChild(handle);
            _headers.Add(header);
        }

        body.AddChild(contents);
        _panels.Add(panel);

        // Every titled panel is something the player can switch off from Settings (Joe:
        // *"that should be a menu the user can activate/deactivate from the settings menu —
        // that's how the user should be able to show/hide all of these information windows"*).
        // Registered here rather than listed by hand, so a panel added later appears in the
        // menu the day it is written instead of the day somebody remembers.
        if (title is not null)
        {
            _windows.Add(new ShellWindow { Name = title, Panel = panel });
        }

        // ⚠️ THE PANEL YOU CLICKED ON WINS THE CLICK. Panels are siblings, so the one added
        // last draws — and receives — on top, which made the order they happen to be built in
        // decide whether a control could be pressed at all. That is how a forester's "Give
        // ground" button ended up under the control bar (D11: nothing here is testable, so it
        // took Joe playing it to find).
        //
        // Raised on mouse-enter rather than by fixing a build order, because there is no build
        // order that is right for every selection: whichever panel the pointer is over is the
        // one the player means. What actually gets raised is whatever ancestor of the panel the
        // root is holding, since `MoveChild` reorders a node's own children and nothing else.
        //
        // ⚠️ THIS COUNTED THE LEVELS RATHER THAN WALKING THEM, and D116's scrolling columns
        // added one — panel → column → scroller → root, where it had been panel → column →
        // root. So every hover on a panel in a column threw *"Child is not a child of this
        // node"* into the terminal and raised nothing. Found by Joe running the game; a
        // screenshot cannot hover, so nothing I can take would have caught it.
        //
        // Walking is the fix rather than counting one level more, because the next container
        // somebody wraps a column in would break a count again and this cannot.
        panel.MouseEntered += () => RaiseToTheFront(panel);

        return contents;
    }

    /// <summary>
    /// Draw a panel — and whatever is carrying it — on top of everything else.
    /// </summary>
    /// <remarks>
    /// Climbs to the ancestor this node actually holds, because that is the only thing
    /// <see cref="Node.MoveChild"/> can reorder. If the panel is not under us at all it is
    /// left alone rather than throwing, which is the honest answer to a question with no
    /// answer: a panel nobody is holding cannot be raised above anything.
    /// </remarks>
    private void RaiseToTheFront(Control panel)
    {
        Node top = panel;
        while (top.GetParent() is Node parent && parent != this)
        {
            top = parent;
        }

        if (top.GetParent() == this)
        {
            MoveChild(top, -1);
        }
    }

    /// <summary>Every floating panel, so one key can put them all away.</summary>
    private readonly List<PanelContainer> _panels = new();

    /// <summary>Every panel header, so one key can roll them all up.</summary>
    private readonly List<Button> _headers = new();

    /// <summary>Every information window the player may switch off, by name.</summary>
    private readonly List<ShellWindow> _windows = new();

    /// <summary>
    /// An information window: its panel, whether the player wants it on screen, and the tick in
    /// Settings that says so (D380).
    /// </summary>
    /// <remarks>
    /// ⛔ <b><c>Wanted</c> is the state; <c>Panel.Visible</c> is a consequence of it</b> — and of
    /// the selection, for *What's here*, and of <c>h</c>. The Settings ticks used to be written
    /// <c>true</c> once at build and never read: *Stock limits* showed ticked while hidden from
    /// the founding, and *What's here*'s tick held for one frame before the selection rewrote
    /// its visibility. The tick reads <c>Wanted</c> every refresh now, and the ✕ on a panel
    /// writes the same state the tick does.
    /// </remarks>
    private sealed class ShellWindow
    {
        public required string Name { get; init; }
        public required PanelContainer Panel { get; init; }
        public bool Wanted { get; set; } = true;
        public CheckBox? Tick { get; set; }
    }

    /// <summary>The window a panel belongs to, or null for the two that are not windows (Settings, the bars).</summary>
    private ShellWindow? WindowOf(PanelContainer panel) => _windows.Find(w => w.Panel == panel);

    /// <summary>
    /// The ✕ on a panel's header (D380, Joe: *"add an X to close on to all panels — otherwise i
    /// cant figure out how to close the 'what's here' pane"*).
    /// </summary>
    /// <remarks>
    /// One rule: it does what unticking the window in Settings does, so the tick follows.
    /// ⭐ <b>*What's here* is the exception, deliberately:</b> it is about what you clicked, like a
    /// card, so its ✕ clears the selection and it returns on the next bare-ground click; its
    /// Settings tick remains the permanent switch. Settings itself is not a window and simply hides.
    /// </remarks>
    private void CloseTheWindow(PanelContainer panel)
    {
        if (panel == _whatsHerePanel)
        {
            _selectedTile = null;
            _selectedVillagerId = 0;
            RefreshInspector(_loop.World);
            return;
        }

        if (WindowOf(panel) is ShellWindow window)
        {
            window.Wanted = false;
        }

        panel.Visible = false;
    }

    /// <summary>The settings panel itself, which is the one window not in that list.</summary>
    private PanelContainer _professionsPanel = null!;

    private PanelContainer _stockLimitsPanel = null!;

    private PanelContainer _settingsPanel = null!;

    /// <summary>How tall Settings stands before it scrolls — on screen at the default scale, centred.</summary>
    private const float SettingsHeight = 600f;

    /// <summary>
    /// ⭐ Open Settings in the middle of the screen — <b>where the player is looking</b>
    /// (D340).
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Joe: *"when the settings button is clicked in the control menu (bottom) — it should
    /// spawn the settings menu in the middle of the screen. presently it spawns on the right-side
    /// behind the other panels there and is annoying to get to."*</b> It is built through
    /// <see cref="InColumn"/> with <c>right: true</c>, so its starting position is the bottom of
    /// the right-hand stack — *underneath every panel already open there.*
    /// </para>
    /// <para>
    /// ⭐ <b>It stays in <c>_docked</c></b>, so *"Reset window positions"* still reaches it,
    /// which is what Joe asked for when that button was built (*"all panels"*).
    /// </para>
    /// <para>
    /// ⚠️ <b>And it is centred only until he moves it.</b> Re-centring a window the
    /// player has parked somewhere is the complaint <see cref="ArrangeDefaults"/> exists to
    /// prevent — *"the panels do that thing where they move when I haven't asked them to"* —
    /// so one flag remembers that it was dragged and this stops touching it.
    /// </para>
    /// <para>
    /// ⛔ <b><c>LayoutPreset.Center</c> is NOT available here</b>, though
    /// <c>BuildTheMomentPanel</c> uses it: an anchored panel cannot then be dragged by offsets.
    /// The size comes from <c>GetCombinedMinimumSize</c> the way <see cref="ArrangeDefaults"/>
    /// takes it.
    /// </para>
    /// </remarks>
    private void ToggleSettings()
    {
        bool opening = !_settingsPanel.Visible;

        _settingsPanel.Visible = opening;
        _centreSettingsWhenItHasASize = opening && !_settingsWasDragged;
    }

    /// <summary>
    /// ⚠️ Centre it on the frame after it opens, <b>because a hidden panel has no
    /// settled size</b>.
    /// </summary>
    /// <remarks>
    /// <c>KeepWindowsOnScreen</c> carries the same warning ten lines down and measured **1,676px
    /// for a professions window that lays out at a fraction of that**. Centring on the click would
    /// use that number and put the panel somewhere arbitrary, so this waits for a real one —
    /// which costs one comparison a frame and never has to guess.
    /// </remarks>
    private void CentreSettingsIfItJustOpened()
    {
        if (!_centreSettingsWhenItHasASize || !_settingsPanel.Visible || _settingsPanel.Size.Y <= 0f)
        {
            return;
        }

        _centreSettingsWhenItHasASize = false;

        // ⭐ Through `MovePanel`, not by writing offsets: a right-anchored panel's OffsetLeft
        // is a negative distance back from the right edge rather than a position on screen, and
        // treating it as one is the exact bug D242's drag clamp was written to fix.
        Vector2 drawn = _settingsPanel.Size * _uiScale;
        var wanted = new Vector2((Size.X - drawn.X) / 2f, (Size.Y - drawn.Y) / 2f);

        MovePanel(_settingsPanel, wanted - DrawnTopLeft(_settingsPanel));
    }

    private bool _centreSettingsWhenItHasASize;

    /// <summary>Whether the player has moved Settings themselves, in which case leave it alone.</summary>
    private bool _settingsWasDragged;

    /// <summary>
    /// Which information windows are on screen — Joe's answer to *"they are HUGE"*.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>⭐ The player decides what is on screen, permanently, rather than per moment.</b>
    /// Collapsing (D113) answers *"not right now"*; this answers *"I never want to see that"*,
    /// and they are different wants — which is why both exist rather than one standing in for
    /// the other.
    /// </para>
    /// <para>
    /// <b>It lists itself out of the list it draws</b>, and that is not tidiness: a settings
    /// menu that could switch itself off is a menu the player cannot get back to.
    /// </para>
    /// </remarks>
    private void BuildSettingsPanel()
    {
        // ⭐ SCROLLS (D380): opened centred, the panel ran off the bottom under the control bar and
        // *About this run* was behind it — in Joe's screenshot and in the probe's shot both.
        // 600 logical plus the header draws at 478 of an 800-logical window at the default 75 %, which clears
        // the bar (161 × 0.75 + the edge) when centred.
        VBoxContainer body = InColumn(right: true, SettingsHeight, "Settings");
        _settingsPanel = _panels[^1];
        _settingsPanel.Visible = false;

        // Dropped from the list of switchable windows for the reason above, and from the
        // headers list so `c` cannot fold the thing you opened to un-fold something else.
        _windows.RemoveAt(_windows.Count - 1);
        _headers.RemoveAt(_headers.Count - 1);

        // ⭐ TIDIED (D380, Joe: *"review the settings panel and clean up the UI to be more aligned
        // with what we're doing"*): five groups under one style of heading — Windows, Size, On
        // the map, How the village runs, About this run — the same controls, less prose. The
        // hint line the control bar already carries is not repeated here.
        body.AddChild(Muted("Windows"));

        // ⛔ THE TICK READS THE STATE; IT DOES NOT ONLY WRITE IT (D380). These were `ButtonPressed
        // = true` once at build: *Stock limits* read ticked while hidden from the founding, and
        // *What's here* untick held for a frame. `Refresh` syncs every tick from `Wanted`.
        foreach (ShellWindow window in _windows)
        {
            var shown = new CheckBox { Text = window.Name, ButtonPressed = window.Wanted };
            shown.AddThemeFontSizeOverride("font_size", 12);
            shown.Toggled += on =>
            {
                window.Wanted = on;
                window.Panel.Visible = on;
            };
            window.Tick = shown;
            body.AddChild(shown);
        }

        // ⭐ Joe: *"'reset all panels' could maybe be a settings button."* Under Windows, because
        // it is about windows; run once after the first layout pass and then never automatically.
        var reset = new Button { Text = "Reset window positions", Flat = true, Alignment = HorizontalAlignment.Left };
        reset.AddThemeFontSizeOverride("font_size", 12);
        reset.Pressed += ArrangeDefaults;
        body.AddChild(reset);

        // ⭐ THE ONE DIAL JOE ASKED FOR: *"smaller please. give me more room to see the game
        // map."* It reaches every panel, every card and the control bar, so the whole furniture
        // shrinks together rather than four font sizes drifting apart.
        body.AddChild(Muted("Size"));

        var sizing = new HBoxContainer();
        sizing.AddThemeConstantOverride("separation", 4);

        Label reading = Muted(string.Empty);
        reading.CustomMinimumSize = new Vector2(38, 0);
        reading.HorizontalAlignment = HorizontalAlignment.Right;

        void ShowScale() => reading.Text = $"{_uiScale * 100f:F0}%";

        var smaller = new Button { Text = "−", Flat = true };
        var bigger = new Button { Text = "+", Flat = true };

        smaller.Pressed += () =>
        {
            _uiScale = Mathf.Max(MinUiScale, _uiScale - 0.05f);
            ShowScale();
        };

        bigger.Pressed += () =>
        {
            _uiScale = Mathf.Min(MaxUiScale, _uiScale + 0.05f);
            ShowScale();
        };

        smaller.AddThemeFontSizeOverride("font_size", 12);
        bigger.AddThemeFontSizeOverride("font_size", 12);

        sizing.AddChild(Muted("UI size"));
        sizing.AddChild(smaller);
        sizing.AddChild(reading);
        sizing.AddChild(bigger);
        body.AddChild(sizing);
        ShowScale();

        // ⭐ THE GLOBAL HALF OF THE FULL-STORE MARKER (Joe, D140): *"visibility of which should
        // be able to be disabled by building or globally."* The per-building half lives on the
        // building's own panel, which is where you are already standing when one store is the
        // one annoying you.
        body.AddChild(Muted("On the map"));

        // ⭐ ROUTES AND GROUND, MOVED OFF THE CONTROL BAR (Joe, 2026-09-06). They sit at the top
        // of this group because they are the two that change what the whole valley looks like,
        // where the four below them each add or remove one mark.
        // ⚠️ Buttons, not checkboxes, and deliberately: Routes cycles through three detail
        // levels and Ground carries its own state in its label. **A checkbox that cycles is a
        // control that lies about what it will do next.**
        _detailButton = new Button { Flat = true, Alignment = HorizontalAlignment.Left };
        _detailButton.AddThemeFontSizeOverride("font_size", 12);
        _detailButton.Pressed += CycleDetail;
        body.AddChild(_detailButton);

        // ⭐ WHERE THE GOOD GROUND IS (D178). Without it, per-site yield is an invisible
        // multiplier and siting a farm is a lottery — which is what D67 refused for ore and
        // what §1.1 refuses in general. Off by default: it answers a question the player asks
        // occasionally, and a permanent wash over the valley is D42's standing alert in
        // another medium.
        _soilButton = new Button { Flat = true, Alignment = HorizontalAlignment.Left };
        _soilButton.AddThemeFontSizeOverride("font_size", 12);
        _soilButton.Pressed += ToggleSoil;
        body.AddChild(_soilButton);
        RefreshSoilButton();

        // ⭐ WHERE EVERYBODY WALKS (D358). The trails on the map show what has become a path; this
        // shows every trodden tile on its way to becoming one — the diagnostic §2.6 needs for its
        // own tuning (*lock-in* or *no paths*) and the only heatmap the game allows, because a
        // tile's wear is sim state and hashed (D357). Off by default, like Ground, for the same reason.
        _wearButton = new Button { Flat = true, Alignment = HorizontalAlignment.Left };
        _wearButton.AddThemeFontSizeOverride("font_size", 12);
        _wearButton.Pressed += ToggleWear;
        body.AddChild(_wearButton);
        RefreshWearButton();

        var markers = new CheckBox { Text = "mark stores with no room", ButtonPressed = true };
        markers.AddThemeFontSizeOverride("font_size", 12);
        markers.Toggled += on => _map.ShowFullMarkers(on);
        body.AddChild(markers);
        _mapToggles.Add(("stores with no room", markers, () => _map.FullMarkersShown));

        // The global half of D147's idle ring, beside the global half of D140's, because they
        // are the same kind of preference and a player looking for one will look for the other.
        var idle = new CheckBox { Text = "mark buildings that cannot work", ButtonPressed = true };
        idle.AddThemeFontSizeOverride("font_size", 12);
        idle.Toggled += on => _map.ShowIdleMarkers(on);
        body.AddChild(idle);
        _mapToggles.Add(("buildings that cannot work", idle, () => _map.IdleMarkersShown));

        // ⭐ The wildlife is scenery rather than a marker, but it belongs with the other two:
        // all three answer *"what is drawn on the valley"*, and a player who wants a plainer
        // map will look for them in one place. ⚠️ It is deliberately NOT on the "Routes:"
        // cycle — that control says routes, and hiding animals under it would surprise.
        // ⭐⭐ SNAP TO GRID (D330, Joe's call: on by default). Filed under "On the map" beside the
        // other four view toggles even though it changes what placement DOES — because it acts on
        // the input before the sim sees it, so nothing about the village changes when it is off,
        // only what the player is allowed to aim at.
        // ⛔ OFF BY DEFAULT SINCE D345 (Joe: *"snap buildings to the grid should be off by
        // default"*). ⚠️ Two defaults, this tick and `VillageMap._snapToGrid`, and the
        // probe below checks they agree — it did not check this one until D345, and its own
        // doc said an unregistered toggle was invisible to it.
        var snap = new CheckBox { Text = "snap buildings to the grid" };
        snap.AddThemeFontSizeOverride("font_size", 12);
        snap.Toggled += on => _map.SnapToGrid(on);
        body.AddChild(snap);
        _mapToggles.Add(("snap to the grid", snap, () => _map.SnapsToGrid));

        // ⚠️ This caption used to promise that the ghost shows the tiles a building will
        // claim when snap is off. **The code never did that** — it showed them always, and
        // since D345 it shows them only while the grid is drawn.
        body.AddChild(Caption("With the grid drawn, the ghost also shows the tiles it will claim."));

        // ⭐⭐ THE GRID LINES (D332, Joe: *"if the game is gridless, then why is everything still in
        // a grid?"*). **They were always on above 6px/tile with no way to turn them off**, and they
        // are the most literal answer to his question.
        // ⛔ **OFF BY DEFAULT SINCE D340** (Joe: *"the default setting for showing the grid
        // should be off. it is presently on."*). ⚠️ **TWO DEFAULTS, AND THEY HAVE TO
        // MOVE TOGETHER**: this tick and `VillageMap._showGrid`. Changing one leaves the checkbox
        // lying about the map, which is worse than either state.
        var grid = new CheckBox { Text = "draw the tile grid" };
        grid.AddThemeFontSizeOverride("font_size", 12);
        grid.Toggled += on => _map.ShowGrid(on);
        body.AddChild(grid);
        _mapToggles.Add(("the tile grid", grid, () => _map.GridShown));

        // ⭐⭐ THE THREE PAINTED LAYERS, ONE SWITCH EACH (D340, Joe: *"i would like to be
        // able to toggle these overlays of the painted areas for trees, houses, and farms off in
        // the settings."*). **One each rather than a single "painted ground" tick was his call**,
        // and it is the right one: the three answer different questions and somebody quietening
        // the map may well want to keep one of them.
        // ⛔ **Each hides the wash AND the border.** Half a layer showing reads as a bug
        // rather than as a setting — and the border is the louder half, so hiding only the
        // fill would barely quieten anything.
        var homes = new CheckBox { Text = "shade ground marked for housing", ButtonPressed = true };
        homes.AddThemeFontSizeOverride("font_size", 12);
        homes.Toggled += on => _map.ShowResidentialLand(on);
        body.AddChild(homes);
        _mapToggles.Add(("ground marked for housing", homes, () => _map.ResidentialLandShown));

        var fields = new CheckBox
        {
            Text = "shade ground a workplace has claimed",
            ButtonPressed = true,
        };
        fields.AddThemeFontSizeOverride("font_size", 12);
        fields.Toggled += on => _map.ShowWorkGround(on);
        body.AddChild(fields);
        _mapToggles.Add(("ground a workplace claimed", fields, () => _map.WorkGroundShown));

        var marked = new CheckBox
        {
            Text = "shade ground marked for harvest",
            ButtonPressed = true,
        };
        marked.AddThemeFontSizeOverride("font_size", 12);
        marked.Toggled += on => _map.ShowHarvestLand(on);
        body.AddChild(marked);
        _mapToggles.Add(("ground marked for harvest", marked, () => _map.HarvestLandShown));

        var wildlife = new CheckBox { Text = "animals in the woods", ButtonPressed = true };
        wildlife.AddThemeFontSizeOverride("font_size", 12);
        wildlife.Toggled += on => _map.ShowGame(on);
        body.AddChild(wildlife);
        _mapToggles.Add(("animals in the woods", wildlife, () => _map.GameShown));

        // Its own switch rather than one "scenery" tick, because the two answer different
        // questions: what the woods FEED and what they HOLD. A player hunting for a quieter
        // map may well want one and not the other.
        var forage = new CheckBox { Text = "berry patches in the woods", ButtonPressed = true };
        forage.AddThemeFontSizeOverride("font_size", 12);
        forage.Toggled += on => _map.ShowForage(on);
        body.AddChild(forage);
        _mapToggles.Add(("berry patches in the woods", forage, () => _map.ForageShown));

        // ⭐ THE SHARE-OUT SWITCH (Joe, 2026-09-03): *"give the user the option to toggle the
        // 'work share' function on/off."* Every three years the village tears every allocation
        // down and rebuilds it, which is how a fifteen-year woodcutter ends up pushing a cart —
        // and a player who has arranged their village on purpose is entitled to keep it.
        //
        // ⛔ UNDER ITS OWN HEADING, NOT WITH THE MARKERS. Everything above this line changes what
        // is DRAWN; this changes what the village DOES. Filing a rule among the view preferences
        // is how a player switches off a mechanic while believing they dimmed an icon.
        body.AddChild(Muted("How the village runs"));

        var share = new CheckBox
        {
            Text = "share the work out every few years",
            ButtonPressed = true,
        };
        share.AddThemeFontSizeOverride("font_size", 12);
        share.Toggled += on => _loop.World.VillageSharesOutWork = on;
        body.AddChild(share);

        body.AddChild(Caption(
            "Off: nobody is moved between jobs unless you change a professions number. "
            + "Empty seats are still filled and a death is still answered."));

        // ⭐ THE OVERVIEW'S LEFTOVERS (D378). The seed and the audit log together, because they
        // are the two things you need to reproduce and explain a run: the seed says which world,
        // the log says what happened in it — and the build, which is the third (METHODOLOGY §5):
        // a bug report quoting a seed and a log is worth much less if nobody can say which build
        // produced them. The tick joined them here when it left the villagers bar. And the
        // roadmap of goods that do not exist yet, which is consulted once and then known —
        // which is why neither belongs on a bar the player reads every minute.
        body.AddChild(Muted("About this run"));
        _seedLabel = Wrapped(Muted(TheRunLine(_loop.World)));
        body.AddChild(_seedLabel);
        body.AddChild(BuildGoodsRoadmap());
    }

    /// <summary>Build, seed, tick, config and log — the line a bug report quotes.</summary>
    private string TheRunLine(SimWorld world) =>
        $"bclone {BuildVersion}   ·   seed {world.Seed}   ·   tick {world.Tick}   ·   "
        + $"config: {_configSource}   ·   log: {_logPath}";

    /// <summary>
    /// Hide the furniture entirely, so the valley is the only thing on screen.
    /// </summary>
    /// <remarks>
    /// <b>The whole point of a generational village-builder is watching it</b> (§1.5, and D49's
    /// argument that a life is the unit that matters). Collapsing panels one at a time answers
    /// *"I want more room"*; this answers *"get out of the way, I am watching"*, and they are
    /// different wants. The control bar goes too — there is nothing to press while you watch.
    /// </remarks>
    private void ToggleFurniture()
    {
        _furnitureShown = !_furnitureShown;
        foreach (PanelContainer panel in _panels)
        {
            panel.Visible = _furnitureShown;
        }
    }

    private bool _furnitureShown = true;

    /// <summary>Roll every panel up to its title, or open them all again.</summary>
    private void ToggleAllPanels()
    {
        // Open them all unless they are all already open, so one press always does something
        // rather than toggling half of them each way.
        bool open = _headers.Exists(header => !header.ButtonPressed);
        foreach (Button header in _headers)
        {
            header.ButtonPressed = open;
        }
    }

    /// <summary>The look of every floating panel: dark, bordered, readable over a lit map.</summary>
    /// <remarks>
    /// Nearly opaque rather than lightly tinted. A panel you can see the valley through is
    /// a panel whose text sits on grass one moment and on a roof the next, and the whole
    /// complaint that prompted this was that the information was hard to read.
    /// </remarks>
    /// <summary>
    /// How a panel is drawn — <b>see-through, and tight</b> (Joe, 2026-08-08).
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Joe: <em>"They are HUGE and take up so much real estate. Let's make them smaller so I
    /// can see more of what's happening in the game."</em></b> Two of his three answers land
    /// here: the panels let the valley through now (0.93 → 0.72), and their padding is closer
    /// (10 → 6). Between them a panel costs attention rather than area, which is the point —
    /// this is a game you <em>watch</em>, and a panel that hides the thing it is describing is
    /// working against the reason it exists.
    /// </para>
    /// <para>
    /// <b>Not fully transparent, deliberately.</b> The village log is small text over a moving
    /// map, and legibility is the non-negotiable this whole layer serves (§1.1) — a panel you
    /// cannot read is worse than one that covers something.
    /// </para>
    /// </remarks>
    /// <summary>
    /// The panel background — <b>solid, except the control bar</b>.
    /// </summary>
    /// <remarks>
    /// ⭐ Joe, 2026-09-06: *"I want to remove the transparency from all panel backgrounds EXCEPT
    /// the build menu."* Every window is something you read; the valley showing through a table of
    /// numbers costs legibility and buys nothing. **The bar is the exception because it runs the
    /// width of the screen** — solid, it would wall off the bottom of the valley entirely.
    /// </remarks>
    private static StyleBoxFlat PanelSkin(float alpha = 1f)
    {
        var skin = new StyleBoxFlat
        {
            BgColor = new Color(0.08f, 0.09f, 0.10f, alpha),
            BorderColor = new Color(0.58f, 0.53f, 0.40f, 0.55f),
        };

        skin.SetBorderWidthAll(1);
        skin.SetContentMarginAll(6);
        skin.SetCornerRadiusAll(3);
        return skin;
    }

    /// <summary>
    /// A label that wraps instead of running off the edge or ending in an ellipsis.
    /// </summary>
    /// <remarks>
    /// The opposite call from D54's, and only because the layout changed underneath it.
    /// Wrapping was refused then because a label whose height depends on the window width
    /// would move the map; inside a floating panel it moves nothing, so the sentence the
    /// game took the trouble to write can be read to the end.
    /// </remarks>
    /// <remarks>
    /// <para>
    /// <b>⭐ AND IT IS ALSO THE ANSWER TO D169, WHICH IS WORTH SAYING OUT LOUD:</b> this helper
    /// existed, did the right thing, and had been applied to five labels while <em>every
    /// sentence in the inspector</em> went into a bare <c>Label</c> inside an
    /// <see cref="HBoxContainer"/> — which is a minimum width, which is a column width, which
    /// is why Joe's map was a strip. **The fix was not a new idea; it was this one, reaching
    /// the panels that needed it.**
    /// </para>
    /// <para>
    /// The minimum is deliberately small and not zero: at zero a column could be squeezed to a
    /// sliver of one word per line, and a panel's own minimum width is the floor that is
    /// supposed to decide how narrow a column gets.
    /// </para>
    /// </remarks>
    private static Label Wrapped(Label label)
    {
        label.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        label.CustomMinimumSize = new Vector2(WrappedTextMinWidth, 0);

        // Take the leftover width rather than only the minimum, so a sentence in a row that
        // has room uses it instead of wrapping early into a tall thin block.
        label.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        return label;
    }

    /// <summary>How narrow a wrapped sentence may be asked to get.</summary>
    private const float WrappedTextMinWidth = 120f;

    /// <summary>What the cursor is over, or what just happened. Empty when not placing.</summary>
    // ⚠️ Genuinely null until the control bar is built, and typed to say so. It was `null!`,
    // which promised it was always there and cost a crash the first time a panel warned
    // during construction — see `Warn`.
    private Label? _placementLabel;

    /// <summary>
    /// The professions this village cannot hire yet. <b>Delete a row when it ships.</b>
    /// </summary>
    /// <remarks>
    /// Hand-written and meant to be, because the whole point of a row here is that there is no
    /// <see cref="JobKind"/> to read it off. Taken from <c>specs/professions.md §4</c> so the
    /// panel and the spec say the same thing; the reason names what would have to be built, so
    /// the row deletes itself the day that lands rather than going quietly stale.
    /// </remarks>
    private static readonly (string Name, string Reason)[] ProfessionsNotYetHired =
    {
        ("Tailor", "waiting on the hunter for leather"),

        // ⭐ The farmer moved OFF this list and into the real rows above
        // (`specs/crops-and-orchards.md`, D161) — this is what a greyed roadmap entry is for:
        // it names the gap while it is a gap, and then it goes away.
        ("Herdsman", "no livestock"),
        ("Miner", "iron is on the map; nothing digs it"),
        ("Stonecutter", "stone is on the map; nothing quarries it"),
        ("Blacksmith", "tools cannot be made, only brought"),
        ("Brewer", "no barley"),
        ("Teacher", "no school"),
        ("Physician", "illness is not modelled"),
    };

    /// <summary>
    /// Show the sim's own warning, <b>if there is anywhere to show it yet</b>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>⚠️ THE GUARD IS THE POINT, AND IT COST A CRASH TO LEARN.</b> <c>_placementLabel</c>
    /// lives on the control bar, which is built <em>last</em> — and once the professions rows
    /// started applying a real number at construction (rather than a null that changed
    /// nothing), one of them returned <em>"there is only room for N on this kind of work"</em>
    /// and wrote it to a label that did not exist yet. <c>BuildUi</c> threw halfway through,
    /// so the roster was never created either, and every frame after that died on
    /// <c>_roster.Clear()</c> — a null reference a long way from its cause.
    /// </para>
    /// <para>
    /// Guarded rather than reordered: there is no build order that is right for every panel
    /// somebody adds later, and a warning with nowhere to go is not worth a crash. It goes to
    /// the console instead, so it is never simply lost.
    /// </para>
    /// </remarks>
    private void Warn(PlacementVerdict verdict)
    {
        if (!verdict.HasWarning)
        {
            return;
        }

        if (_placementLabel is null)
        {
            GD.Print($"[placement] {verdict.Warning}");
            return;
        }

        _placementLabel.Text = verdict.Warning;
        _placementLabel.Visible = true;
    }

    /// <summary>What a kind of work is called on screen. Every value named (D108).</summary>
    /// <summary>What a kind of work is called on screen — <b>the one place that decides</b>.</summary>
    /// <remarks>
    /// <para>
    /// <b>⛔ THERE USED TO BE TWO OF THESE AND THEY DISAGREED</b> (found 2026-08-23, D188). This
    /// said **Gatherer** and **Vendor** while <see cref="TradeOf"/> said **forager** and
    /// **marketer** — the professions panel and the stock rows using one vocabulary, the roster
    /// beside every villager's name using the other. **The same job, two words, two panels**,
    /// which is D148's finding arriving in the UI.
    /// </para>
    /// <para>
    /// <b>It stopped being cosmetic when the skill line landed.</b> *"Sixteen years as a
    /// farmer"* sits directly under the roster entry that names the villager, so a third place
    /// had to agree with the other two — and two of the three did not.
    /// </para>
    /// <para>
    /// <b>Joe's call: *"forager and marketer win."*</b> <see cref="TradeOf"/> lower-cases this
    /// rather than keeping its own list, so there is now exactly one place a job is named.
    /// </para>
    /// </remarks>
    /// <remarks>
    /// ⛔⛔ <b>THIS WAS SIX NAMED ARMS AND A DEFAULT THAT THREW, AND IT IS THE SAME BUG AS
    /// <c>GoodsName</c> ONE PANEL OVER.</b> D108's every-value-named rule is right for a closed
    /// enum; `JobKind` has been open since D218 (*"a seventh trade has a slot here the day a
    /// modder adds one"*), so a seventh trade **crashed the professions panel on every frame** —
    /// measured when fishing shipped: the suite was 905 green and the game threw **3,609 times in
    /// two hundred ticks**, which is what a headless run is for.
    /// <para>
    /// ⭐ `JobsCatalog.NameOf` had the answer the whole time, and `jobs-catalog.md` already
    /// forbids the sim switching on a trade by name — the view had simply never been held to it.
    /// </para>
    /// </remarks>
    private static string ProfessionName(SimWorld world, JobKind kind)
    {
        string name = world.JobsCatalog.NameOf(kind);
        return name.Length == 0 ? name : char.ToUpperInvariant(name[0]) + name[1..];
    }

    /// <summary>The "have N" labels, refreshed with everything else.</summary>
    /// <summary>
    /// The trades that do not exist yet, and what each is waiting on.
    /// </summary>
    /// <remarks>
    /// ⭐ Kept through the redesign because it earns its place: it answers *"where is the miner?"*
    /// before the player has to ask, and a row deletes itself the day its trade ships — which is
    /// how "Fisherman" and "Hunter" were caught still sitting in it two commits ago.
    /// </remarks>
    private Control BuildTheNotHiredYet()
    {
        VBoxContainer inside = Foldaway(
            $"Not hired yet — {ProfessionsNotYetHired.Length} more, and why",
            out VBoxContainer roadmap);

        var table = new GridContainer { Columns = 2 };
        table.AddThemeConstantOverride("h_separation", 8);
        table.AddThemeConstantOverride("v_separation", 2);

        foreach ((string name, string reason) in ProfessionsNotYetHired)
        {
            Label label = Muted(name);
            label.Modulate = new Color(1, 1, 1, 0.3f);
            table.AddChild(label);

            Label why = Wrapped(Muted(reason));
            why.Modulate = new Color(1, 1, 1, 0.3f);
            table.AddChild(why);
        }

        inside.AddChild(table);
        return roadmap;
    }

    /// <summary>Five cells for one good's limit.</summary>
    /// <remarks>
    /// The controls are unchanged from the two-line rows this replaced — same <c>SpinBox</c>, same
    /// defaults, same <c>SetStockLimit</c> call, same clear button. **Only the layout moved.**
    /// </remarks>
    private void AddStockLimitRow(GridContainer table, Goods goods)
    {
        table.AddChild(Chip(ChipColour(goods)));
        table.AddChild(Body(GoodsName(_loop.World, goods)));

        int startsAt = goods switch
        {
            Goods.Produce => 2000,

            // ⭐ The same as food's, deliberately (D348): the wheat limit is what a limit on
            // this row ALONE does to the farmers, and the default must change nothing until a
            // player sets it.
            Goods.Wheat => 2000,
            Goods.Firewood => 400,
            _ => 200,
        };

        var amount = new SpinBox
        {
            MinValue = 0,
            MaxValue = 100_000,
            Step = 10,
            Value = startsAt,
            Editable = true,
            CustomMinimumSize = new Vector2(74, 0),
        };

        var clear = new Button { Text = "clear", Flat = true, Disabled = true };

        Label held = Wrapped(Muted(string.Empty));

        void Set(int? limit)
        {
            clear.Disabled = limit is null;
            Warn(_loop.World.SetStockLimit(goods, limit));
        }

        amount.ValueChanged += _ => Set((int)amount.Value);
        clear.Pressed += () => Set(null);

        _loop.World.SetStockLimit(goods, startsAt);
        clear.Disabled = false;

        table.AddChild(amount);
        table.AddChild(clear);
        table.AddChild(held);

        _stockLimitReadouts.Add((goods, held));
    }

    private readonly List<(Goods Goods, Label Held)> _stockLimitReadouts = new();
    /// <summary>
    /// Two live cells per trade — <b>the max, and the notes</b>.
    /// </summary>
    /// <remarks>
    /// ⚠️ The seat count needs its own readout now that it lives in the ASSIGNED / MAX cell
    /// rather than inside the sentence: seats change as buildings go up and come down, so it
    /// cannot be written once at construction.
    /// </remarks>
    private readonly List<(JobKind Kind, Label Seats, Label Name)> _professionReadouts = new();

    /// <summary>The trade name's column, wide enough for "Woodcutter ⚠" and no wider for anything (D374).</summary>
    private const float ProfessionNameWidth = 110f;

    private Label _laborerReadout = null!;

    /// <summary>How many people are actually on this kind of work right now.</summary>
    private static int WorkingAt(SimWorld world, JobKind kind)
    {
        int count = 0;
        foreach (Workplace workplace in world.Workplaces)
        {
            if (workplace.Kind == kind)
            {
                count += workplace.WorkerIds.Count;
            }
        }

        return count;
    }

    /// <summary>How many places there are to be on it — the "of N" in Joe's screenshot.</summary>
    private static int SeatsFor(SimWorld world, JobKind kind)
    {
        int seats = 0;
        foreach (Workplace workplace in world.Workplaces)
        {
            if (workplace.Kind == kind)
            {
                // ⛔ `Places`, NOT `Capacity` (2026-09-01). `Places` is `StaffingOverride ?? Capacity`
            // — what `IsFull` and the allocator both use — so counting `Capacity` here had the
            // professions row describing a rule that is not in force: turn a 2-seat hut down to
            // one and the inspector said *"1 of 1 places filled"* while this column said
            // *"1 working of 2 seats"*, eight pixels apart.
            seats += workplace.Places;
            }
        }

        return seats;
    }

    /// <summary>A villager's trade, for the roster. What they are, not where they are.</summary>
    /// <summary>The same names as <see cref="ProfessionName"/>, in the roster's register.</summary>
    /// <remarks>
    /// <b>⛔ IT USED TO KEEP ITS OWN LIST, AND THE TWO DRIFTED</b> (D188) — this said *forager*
    /// and *marketer* where <see cref="ProfessionName"/> said *Gatherer* and *Vendor*. **One
    /// place names a job now**; this only lower-cases it, because the roster reads
    /// *"Hattie, 39 (adult) — farmer"* mid-sentence while the professions panel heads a column.
    /// Two registers of one vocabulary is fine; two vocabularies is what was not.
    /// </remarks>
    private string TradeOf(Villager villager)
    {
        Workplace? job = _loop.World.FindWorkplace(villager.WorkplaceId);
        return job is null ? "working" : ProfessionName(_loop.World, job.Kind).ToLowerInvariant();
    }

    /// <summary>What a good is called on screen.</summary>
    /// <remarks>
    /// <b>Every value named, and the default throws</b> (D108's rule). This used to fall back
    /// to <c>goods.ToString()</c>, which was harmless while three goods had limits and became
    /// load-bearing the moment the overview listed all six — a new good would have shown up
    /// under its enum spelling, which is the version of a name nobody chose.
    /// </remarks>
    /// <summary>
    /// What a good is called on screen — <b>asked of the catalogue, capitalised for a heading</b>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// ⛔⛔ <b>THIS WAS SIX NAMED ARMS AND A DEFAULT THAT THREW</b>, on D108's rule that every
    /// value be named. That rule is right for a <em>closed</em> enum and wrong here: `Goods` has
    /// been open since D210 (*"a seventh good is held, hashed and carried like any other"*), so
    /// **the first good a modder added would crash the overview panel** the moment it was drawn —
    /// and `GoodsCatalog.NameOf` had the answer the whole time. `goods-catalog.md §2.1` forbids the
    /// sim switching on a good by name; the view had simply never been held to it.
    /// </para>
    /// <para>
    /// ⭐ Capitalisation is the view's business, not the catalogue's. The row holds *"produce"*
    /// because that is the word the village uses in a sentence (*"12 food"*); a table heading
    /// wants *"Produce"*. **One word, two presentations, and the row keeps the word.**
    /// </para>
    /// </remarks>
    private static string GoodsName(SimWorld world, Goods goods)
    {
        string name = world.GoodsCatalog.NameOf(goods);
        return name.Length == 0 ? name : char.ToUpperInvariant(name[0]) + name[1..];
    }

    /// <summary>
    /// How much of a good the limit is actually measured against.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>The same total <c>LabourQuota</c> reads, and it must stay that way.</b> Firewood is
    /// counted in the warehouses, not everywhere, because a pile in somebody else's home is not
    /// supply — no errand reaches it. Showing the player a village-wide total beside a limit
    /// that governs the warehouse would explain a stopped woodcutter with a number that had
    /// nothing to do with why it stopped, which is D29 wearing a UI.
    /// </para>
    /// <para>
    /// <b>⛔ AND FOR MONTHS IT DID NOT (2026-08-27). The invariant above was asserted and
    /// broken in the same method</b>, in two places:
    /// </para>
    /// <para>
    /// <b>Stone, tools and iron fell to <c>_ =&gt; 0</c></b>, so a village holding three hundred
    /// stone read <em>"stop at 100 · have 0"</em> — a row telling the player their limit is
    /// nowhere near binding while it binds. The sim never had this gap: <c>MayTake</c> is
    /// <c>InStores(goods)</c> for every good by index, with nothing switching on a name, which
    /// is `goods-catalog.md §2.1`'s rule. **A modded good read zero here for ever.**
    /// </para>
    /// <para>
    /// <b>And Food read <c>FoodInGranaries()</c> where every sim decision reads
    /// <c>FoodTheVillageHolds()</c></b> — granaries <em>plus</em> workplace stores. They stopped
    /// being the same number when D161 gave the farm a buffer, and the row has disagreed with
    /// the quota it describes ever since.
    /// </para>
    /// <para>
    /// ⚠️ <b>The arms are the sim's own reads, per good, and not a tidy single call</b>: logs
    /// and firewood genuinely are counted in the warehouses by <c>LabourQuota</c>, and collapsing all
    /// five to <c>InStores</c> would break the invariant in the other direction.
    /// </para>
    /// </remarks>
    private static int HeldFor(SimWorld world, Goods goods) => goods switch
    {
        Goods.Produce => world.FoodTheVillageHolds(),
        Goods.Logs => world.LogsInWarehouses(),
        Goods.Firewood => world.FirewoodInWarehouses(),
        _ => world.InStores(goods),
    };

    /// <summary>Why a good is lying in the open, asked rather than assumed.</summary>
    /// <remarks>
    /// <b>Three states, and they have different remedies.</b> Nothing will take it (build or
    /// re-filter a store); something would but is full (empty one, or build another); or
    /// somewhere has room right now and it simply has not been carried yet, which after a
    /// clearing is the ordinary case and not a problem at all. Reporting all three as
    /// <em>"no room in store"</em> sent Joe looking for space he already had.
    /// </remarks>
    private static string WhyItIsOnTheGround(SimWorld world, Goods goods)
    {
        bool anyWilling = false;
        for (int i = 0; i < world.StoreBuildings.Count; i++)
        {
            StoreBuilding store = world.StoreBuildings[i];
            if (!store.Accepts(goods))
            {
                continue;
            }

            anyWilling = true;
            if (!store.Store.IsFull)
            {
                return "still to be carried in";
            }
        }

        return anyWilling
            ? "no room in store"
            : "nothing in the village will take it";
    }

    // ---------------------------------------------------------------
    //  The build bar (`specs/build-bar.md`)
    // ---------------------------------------------------------------

    /// <summary>Which kind of act the strip is showing.</summary>
    /// <remarks>
    /// ⭐ <b>Three tabs and more than three brushes</b>, and the strays are placed by what the
    /// player is <em>doing</em> rather than by growing the tab row (Joe, 2026-09-06). The land
    /// brush and Move are things you add, so they are BUILD; taking land back and emptying a
    /// store are things you undo, so they are REMOVAL.
    /// ⚠️ <b>Move and Empty were this spec's reading rather than his word</b> —
    /// `specs/build-bar.md §8.1`.
    /// </remarks>
    private enum BuildTab
    {
        Build,
        Removal,
        Harvest,
    }

    /// <summary>
    /// What kind of thing a building is, <b>for the player</b>.
    /// </summary>
    /// <remarks>
    /// ⛔⛔ <b>THIS IS A VIEW ENUM AND IT MUST STAY ONE.</b> `buildings-catalog.md §8.1` settled
    /// it: the category is <em>"a column the sim does not want — it is presentation, and putting
    /// it on the row would be the sim carrying the view's vocabulary."</em> The catalogue answers
    /// what a building <em>costs</em>, <em>stores</em> and <em>employs</em>; which shelf of a menu
    /// it belongs on is nobody's business but this bar's.
    /// </remarks>
    private enum BuildCategory
    {
        Works,
        Food,
        Resources,
        Storage,
        Knowledge,
        Civic,
        Homes,

        /// <summary>
        /// A building this view has never heard of — a modder's, or a built-in added since.
        /// </summary>
        /// <remarks>
        /// ⭐ <b>This is `buildings-catalog.md §8.2`'s recorded cheap option, arriving inside the
        /// catalogue-driven answer rather than instead of it.</b> Built-ins keep their hand-placed
        /// grouping and ordering; <b>anything past them appears automatically here</b>, with a
        /// drawn mark and a name, on the day it has a row. D223's deferral is what this closes,
        /// and D221's hole — <em>a feature the player cannot reach does not exist</em> — with it.
        /// </remarks>
        Other,
    }

    /// <summary>A flow row with the bar's spacing. Never an <c>HBoxContainer</c>.</summary>
    /// <remarks>
    /// ⛔ <b>An <c>HBoxContainer</c> has no way to fail gracefully</b> (D242): it has one line, and
    /// children that do not fit simply leave the screen — <em>"Pause"</em> clipped to <em>"use"</em>
    /// on the left edge and <em>"Cancel"</em> half off the right. A flow container puts the
    /// overflow on a second row. <b>This bar grows by a whole category twice in a normal game</b>,
    /// when the village learns to write and when its founders die, so it will need that.
    /// ⚠️ Flow containers take <c>h_separation</c>/<c>v_separation</c>; the plain <c>separation</c>
    /// an <c>HBoxContainer</c> uses is silently ignored.
    /// </remarks>
    private static HFlowContainer FlowRow()
    {
        var row = new HFlowContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        row.AddThemeConstantOverride("h_separation", 6);
        row.AddThemeConstantOverride("v_separation", 4);
        return row;
    }

    /// <summary>A catalogue name, as a button reads it.</summary>
    /// <remarks>
    /// The catalogue holds <em>"forager's hut"</em> because that is how a sentence says it. A
    /// button starts its own sentence, so it gets the capital — and <b>only the capital</b>: the
    /// word itself is never restated here, which is what D240 cost a year of the bar saying
    /// <em>"gatherer's hut"</em> under a roster that said <em>forager</em>.
    /// </remarks>
    private static string Titled(string name) =>
        name.Length == 0 ? name : char.ToUpperInvariant(name[0]) + name[1..];

    /// <summary>BUILD / REMOVAL / HARVEST.</summary>
    private void AddTheTabs(Container into)
    {
        foreach ((BuildTab Tab, string Label) entry in new[]
        {
            (BuildTab.Build, "BUILD"),
            (BuildTab.Removal, "REMOVAL"),
            (BuildTab.Harvest, "HARVEST"),
        })
        {
            BuildTab captured = entry.Tab;
            var button = new Button { Text = entry.Label, ToggleMode = true };
            button.Pressed += () =>
            {
                _tab = captured;
                RefreshTheStrip();
            };

            _tabButtons.Add((captured, button));
            into.AddChild(button);
        }
    }

    /// <summary>ALL, and one chip per category.</summary>
    /// <summary>
    /// ⭐⭐ ONE HEIGHT FOR THE CONTROL BAR, WHATEVER TAB AND FILTER THE PLAYER IS ON.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Joe, 2026-09-06, twice:</b> *"the build tab still pushes the whole bottom bar taller
    /// than the cancel tab, removal tab, and harvest tab. build → civic collapses the bottom bar
    /// when empty … uniform height across all please!"*
    /// </para>
    /// <para>
    /// ⛔⛔ <b>THE FIRST ATTEMPT FIXED ONE ROW AND MEASURED ONE DIMENSION, AND THAT IS THE
    /// LESSON.</b> D312 stopped the *filter row* hiding and the probe then reported a steady 151
    /// across the three tabs — **because it walked the tabs at one filter.** The strip row is the
    /// other axis: ALL wraps to two rows, a category with nothing in it has none, and the
    /// placement sentence is a third row that comes and goes with the tool in your hand.
    /// *A probe that varies one dimension of a two-dimensional space reports a steadiness that
    /// does not exist, and it reported it convincingly enough to ship.*
    /// </para>
    /// <para>
    /// ⭐ <b>So the bar is pinned to its own tallest configuration rather than to any row's
    /// height.</b> Every tab × every filter is measured with the placement line reserved, and the
    /// largest wins. It is recomputed only when something that could change the answer changes —
    /// the width, the UI scale, or how many buildings are unlocked — because it costs a layout
    /// pass per state and `FitFloaters` runs every frame.
    /// </para>
    /// <para>
    /// ⚠️ <b>The honest limit:</b> the placement line is reserved at two lines, so a warning long
    /// enough to wrap to three still grows the bar. That is the one remaining case, and it is
    /// left rather than clipped — <c>Wrapped</c> exists because this project decided a sentence
    /// the player cannot finish is worse than a bar that moves.
    /// </para>
    /// </remarks>
    private void PinTheBarHeight(float wanted)
    {
        // ⚠️ BOTH, AND THE SECOND IS NOT BELT-AND-BRACES. `_controlBar` is assigned at the TOP of
        // `BuildControlPanel` and `_placementLabel` at the bottom, so a `FitFloaters` landing
        // between them sees a bar with no label — the null warning was pointing at a real
        // ordering hazard rather than at a formality.
        if (_controlBar is null || _placementLabel is null)
        {
            return;
        }

        // The three things that can change the answer. Anything else — the tab, the filter, the
        // tool — is a state this method already walks, so it must not trigger a recompute.
        int earned = 0;
        foreach ((BuildTab Tab, BuildCategory Category, Button Button, BuildingKind? Kind, ToolMark? Mark) entry in _strip)
        {
            if (EarnedYet(entry.Kind))
            {
                earned++;
            }
        }

        var key = new Vector3(Mathf.Round(wanted), Mathf.Round(_uiScale * 1000f), earned);
        if (key == _barPinnedFor)
        {
            return;
        }

        _barPinnedFor = key;

        BuildTab wasOn = _tab;
        BuildCategory? filterWas = _filter;
        bool noteWas = _placementLabel.Visible;
        string noteText = _placementLabel.Text;

        // Reserved rather than posed with a real sentence: the messages come from the map AND
        // from the sim's own refusals, so there is no list to take a longest from.
        _placementLabel.Visible = true;
        _placementLabel.Text = "\n";

        // Measure from unpinned, or the pin from the last window size becomes a floor that can
        // only ever grow — a bar that never gets shorter when the window gets wider.
        _controlBar.CustomMinimumSize = new Vector2(0f, 0f);

        float tallest = 0f;
        foreach (BuildTab tab in new[] { BuildTab.Build, BuildTab.Removal, BuildTab.Harvest })
        {
            foreach (BuildCategory? category in TheFilterStates(tab))
            {
                _tab = tab;
                _filter = category;
                RefreshTheStrip();
                _controlBar.QueueSort();
                ForceUpdateTransform();
                tallest = Mathf.Max(tallest, _controlBar.Size.Y);
            }
        }

        _tab = wasOn;
        _filter = filterWas;
        _placementLabel.Visible = noteWas;
        _placementLabel.Text = noteText;
        RefreshTheStrip();

        _controlBar.CustomMinimumSize = new Vector2(0f, tallest);
        _controlBar.QueueSort();
    }

    /// <summary>What the bar's pinned height was last computed for — width, scale, unlocks.</summary>
    private Vector3 _barPinnedFor = new(-1f, -1f, -1f);

    /// <summary>Every filter the player can be looking at on a tab — ALL, plus the categories.</summary>
    /// <remarks>
    /// Only BUILD has a filter row, so the other two tabs have exactly one state. Used by the
    /// width probe, which has to walk tab × filter rather than tab alone (D314).
    /// </remarks>
    private static IEnumerable<BuildCategory?> TheFilterStates(BuildTab tab)
    {
        yield return null;

        if (tab != BuildTab.Build)
        {
            yield break;
        }

        foreach (BuildCategory category in System.Enum.GetValues<BuildCategory>())
        {
            yield return category;
        }
    }

    private void AddTheFilters(Container into)
    {
        foreach ((BuildCategory? Category, string Label) entry in new (BuildCategory?, string)[]
        {
            (null, "All"),
            (BuildCategory.Works, "Works"),
            (BuildCategory.Food, "Food"),
            (BuildCategory.Resources, "Resources"),
            (BuildCategory.Storage, "Storage & trade"),
            (BuildCategory.Knowledge, "Knowledge"),
            (BuildCategory.Civic, "Civic"),
            (BuildCategory.Homes, "Homes"),
            (BuildCategory.Other, "Other"),
        })
        {
            BuildCategory? captured = entry.Category;
            var button = new Button { Text = entry.Label, ToggleMode = true };
            button.Pressed += () =>
            {
                _filter = captured;
                RefreshTheStrip();
            };

            _filterButtons.Add((captured, button));
            into.AddChild(button);
        }
    }

    /// <summary>Put down whatever is in hand.</summary>
    private Button TheCancelButton()
    {
        var stop = new Button { Text = "Cancel" };
        stop.Pressed += () => _map.PutTheToolDown();
        return stop;
    }

    /// <summary>
    /// Every button the bar will ever show, built once from the <b>catalogue</b>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// ⭐⭐ <b>THIS IS WHAT D223 DEFERRED AND `buildings-catalog.md §8.2` SAID TO DECIDE HERE.</b>
    /// The bar was ten hand-written <c>BuildButton("Granary", BuildingKind.Granary)</c> calls in
    /// four groups, and §8.2's own words are that this <em>"does not scale to 45 buildings
    /// whatever the data model underneath says … the menu wants a redesign when the content
    /// lands, and THAT is the moment to decide whether it reads from the catalogue."</em> This is
    /// that moment, and Joe's call is that it does.
    /// </para>
    /// <para>
    /// ⛔ <b>The house is the one row skipped.</b> A house is placed by the land brush and never
    /// by a button (D42, D102) — the village decides which tiles and when, and the player decides
    /// where it <em>may</em>. Everything else in the catalogue gets a button, including a row that
    /// did not exist when this code was written.
    /// </para>
    /// <para>
    /// ⚠️ <b>The names come from the catalogue too</b>, which is the half that is a bug fix rather
    /// than a feature: D240 found the bar still saying <em>"gatherer's hut"</em> a year after the
    /// trade became <em>forager</em>, because the bar held its own copy of every word.
    /// </para>
    /// </remarks>
    private void BuildTheStrip()
    {
        BuildingsCatalog catalogue = _loop.World.BuildingsCatalog;

        for (int id = 0; id < catalogue.Count; id++)
        {
            var kind = (BuildingKind)id;
            if (kind == BuildingKind.Home)
            {
                continue;
            }

            // ⚠️ ASKED OF THE VALUE, NOT OF A RANGE. `BuildingKind` is appended to and never
            // renumbered, so a hand-written `id <= (int)BuildingKind.HunterLodge` would go stale
            // the day a fifteenth built-in lands — quietly, by drawing it as a modder's square.
            bool known = System.Enum.IsDefined(kind);
            BuildingKind captured = kind;

            Button button = StripButton(
                Titled(catalogue.NameOf(kind)),
                new BuildingGlyph(kind, known),
                () => _map.BeginBuilding(captured));

            if (kind == BuildingKind.Library)
            {
                _libraryButton = button;
            }
            else if (kind == BuildingKind.TownHall)
            {
                _townHallButton = button;
            }

            _strip.Add((BuildTab.Build, CategoryOf(kind, known), button, kind, null));
            _stripRow.AddChild(button);
        }

        // The brush (D42). Its own category because it is a different kind of decision: the
        // others place one thing, this says where a whole neighbourhood may grow — and the
        // village decides which tiles, and when, and whether at all.
        Add(BuildTab.Build, BuildCategory.Homes, "Paint land", ToolMark.PaintLand,
            () => _map.BeginPainting(1));

        // ⛔ MOVE AND EMPTY SHIP WITH THE SIM FEATURES THEY DRIVE, and their absence is what Joe
        // hit: *"I don't see anything in the UI that allows me to move a building?"* and *"no
        // option to 'empty' to another storage building."* **Both had been built and neither was
        // reachable.** *Placeable is not reachable, and neither is relocatable.*
        Add(BuildTab.Build, BuildCategory.Works, "Move", ToolMark.Move, () => _map.BeginMoving());

        Add(BuildTab.Removal, BuildCategory.Works, "Demolish", ToolMark.Demolish,
            () => _map.BeginDemolishing());
        Add(BuildTab.Removal, BuildCategory.Homes, "Take back", ToolMark.TakeBack,
            () => _map.BeginPainting(-1));
        Add(BuildTab.Removal, BuildCategory.Storage, "Empty", ToolMark.Empty,
            () => _map.BeginEmptying());

        foreach ((string Label, HarvestBrush Mode, ToolMark Mark) entry in new[]
        {
            ("Trees", HarvestBrush.Trees, ToolMark.HarvestTrees),
            ("Stone", HarvestBrush.Stone, ToolMark.HarvestStone),
            ("Iron", HarvestBrush.Iron, ToolMark.HarvestIron),
            ("All", HarvestBrush.Everything, ToolMark.HarvestAll),
        })
        {
            HarvestBrush mode = entry.Mode;
            Add(BuildTab.Harvest, BuildCategory.Resources, entry.Label, entry.Mark,
                () => _map.BeginHarvesting(mode, 1));
        }

        Add(BuildTab.Harvest, BuildCategory.Resources, "Unmark", ToolMark.Unmark,
            () => _map.BeginHarvesting(HarvestBrush.Everything, -1));

        void Add(BuildTab tab, BuildCategory category, string label, ToolMark mark, System.Action act)
        {
            Button button = StripButton(label, new ToolGlyph(mark), act);
            _strip.Add((tab, category, button, null, mark));
            _stripRow.AddChild(button);
        }
    }

    /// <summary>One button on the strip: a drawn mark over its word.</summary>
    /// <remarks>
    /// ⭐ <b>THE WORD STAYS.</b> §1.1 is the hardest non-negotiable and an icon-only strip is a
    /// memory test — the player would learn nine shapes or hover nine times. The mark is drawn
    /// inside the button rather than beside it, so <b>the whole button is the click target</b>
    /// and the mark is not a decoration you can miss.
    /// ⚠️ The mark is anchored to the button's top centre by hand: a <c>Button</c> is not a
    /// container, so nothing lays its children out, which is exactly what makes the position
    /// predictable at every UI scale.
    /// </remarks>
    private static Button StripButton(string label, Control mark, System.Action act)
    {
        var button = new Button
        {
            Text = label,
            Alignment = HorizontalAlignment.Center,
            CustomMinimumSize = new Vector2(0, 48),

            // ⭐ TOGGLE MODE SO THE HELD BUTTON CAN BE LIT WITHOUT TOUCHING `Modulate`. The
            // library and the town hall already use `Modulate` to say *"this is the gift"*, and
            // two meanings in one channel is how a highlight stops meaning anything.
            ToggleMode = true,
        };

        mark.AnchorLeft = 0.5f;
        mark.AnchorRight = 0.5f;
        mark.AnchorTop = 0f;
        mark.AnchorBottom = 0f;
        mark.OffsetLeft = -7f;
        mark.OffsetRight = 7f;
        mark.OffsetTop = 4f;
        mark.OffsetBottom = 18f;
        button.AddChild(mark);

        button.Pressed += act;
        return button;
    }

    /// <summary>Which shelf of the menu a building belongs on. <b>View vocabulary only.</b></summary>
    private static BuildCategory CategoryOf(BuildingKind kind, bool known) => !known
        ? BuildCategory.Other
        : kind switch
        {
            // Works first, because it is first in the game (D108): nothing anywhere on this
            // strip is ever raised without a builder's hut. The group will hold roads, bridges
            // and fences when the builder gets them (`professions.md §4`).
            BuildingKind.BuilderHut => BuildCategory.Works,

            // The smithy beside it (D391): the second building whose product is for every trade.
            BuildingKind.Smithy => BuildCategory.Works,

            BuildingKind.GathererHut or BuildingKind.Farmhouse
                or BuildingKind.FishingHut or BuildingKind.HunterLodge => BuildCategory.Food,

            BuildingKind.ForesterHut or BuildingKind.WoodcutterHut => BuildCategory.Resources,

            // The pile leads its group because it leads the game (D76): it costs nothing but
            // the ground, and a village with nowhere to put things cannot begin.
            BuildingKind.Pile or BuildingKind.Granary or BuildingKind.Warehouse
                or BuildingKind.Market or BuildingKind.Longhouse => BuildCategory.Storage,

            // ⭐ ITS OWN GROUP, BECAUSE IT IS ITS OWN KIND OF DECISION (Phase 4). Everything else
            // here is about producing or keeping goods; a library keeps *techniques*, and it is
            // the first building the village raises for a reason other than eating.
            BuildingKind.Library => BuildCategory.Knowledge,

            // The hall is not a knowledge building with extras, and putting it under Knowledge
            // would say the opposite of what D251 settled.
            BuildingKind.TownHall => BuildCategory.Civic,

            _ => BuildCategory.Other,
        };

    /// <summary>
    /// Show what the tab and the chip ask for, and light what the player is holding.
    /// </summary>
    /// <remarks>
    /// ⛔ <b>A BUILDING THE VILLAGE HAS NOT EARNED IS NOT ON THE STRIP AT ALL.</b> Joe, from play:
    /// <em>"the library is in the UI from the beginning — shouldn't it show up once gifted?"</em>
    /// <b>A button you have been looking at for eighteen years is not a surprise when it
    /// unlocks</b>, and the same is true of the hall at year sixty (D252).
    /// </remarks>
    private void RefreshTheStrip()
    {
        // ⛔ THE ROW ITSELF NEVER HIDES — that is the whole of Joe's constant-height ask. What
        // changes is which of its children are showing: the chips on BUILD, one sentence
        // otherwise. **Both are a single line, so the bar is three rows on every tab.**
        bool building = _tab == BuildTab.Build;
        foreach (Node child in _filterRow.GetChildren())
        {
            if (child is Control control)
            {
                control.Visible = building;
            }
        }

        // ⛔ THE SHAPE BUTTON IS EXEMPT FROM THE LOOP ABOVE, WHICH HIDES THIS ROW'S CHILDREN OFF
        // BUILD (D327). It is a setting rather than a filter — it belongs to the brush, and all
        // three tabs have brushes on them. **Always visible is also what keeps this row one line
        // on every tab**, which is the whole of Joe's constant-height ask.
        if (_shapeButton is not null)
        {
            _shapeButton.Visible = true;
        }

        _tabNote.Visible = !building;
        _tabNote.Text = _tab switch
        {
            BuildTab.Removal => "— taking a building back is a builder's job, and costs half what raising it did",
            _ => "— painted ground is felled or dug by whoever is spare",
        };

        foreach ((BuildTab Tab, BuildCategory Category, Button Button, BuildingKind? Kind, ToolMark? Mark) entry in _strip)
        {
            bool onThisTab = entry.Tab == _tab;
            bool pastTheFilter = _tab != BuildTab.Build || _filter is null || _filter == entry.Category;
            entry.Button.Visible = onThisTab && pastTheFilter && EarnedYet(entry.Kind);
        }

        // ⚠️ NO-SIGNAL, OR THIS METHOD CALLS ITSELF. Every one of these buttons is in toggle
        // mode and every one of them re-enters here when pressed, so writing `ButtonPressed`
        // directly would be a loop waiting on Godot's exact signal semantics to not close it.
        // *Not relying on that is cheaper than checking it.*
        foreach ((BuildTab tab, Button button) in _tabButtons)
        {
            button.SetPressedNoSignal(tab == _tab);
        }

        foreach ((BuildCategory? category, Button button) in _filterButtons)
        {
            button.SetPressedNoSignal(category == _filter);
        }

        RelightTheStrip();
    }

    /// <summary>Whether the village has earned the right to see this button yet.</summary>
    private bool EarnedYet(BuildingKind? kind) => kind switch
    {
        BuildingKind.Library => _literacy,
        BuildingKind.TownHall => _foundersGone,
        _ => true,
    };

    /// <summary>
    /// Light the tab and the button for whatever is in the player's hand.
    /// </summary>
    /// <remarks>
    /// ⭐ <b>This is what `VillageMap.MapTool` was built for.</b> Until it existed the seven
    /// <c>Begin*</c> methods set eight private fields that nothing could read back, so
    /// <b>no tab could light up</b> and the bar could not tell the player what they were holding
    /// — which is §1.1's first duty.
    /// </remarks>
    private void RelightTheStrip()
    {
        VillageMap.MapTool tool = _map.Tool;
        BuildTab? held = tool switch
        {
            VillageMap.MapTool.Building or VillageMap.MapTool.PaintingHomes
                or VillageMap.MapTool.Moving => BuildTab.Build,
            VillageMap.MapTool.Demolishing or VillageMap.MapTool.ErasingHomes
                or VillageMap.MapTool.Emptying => BuildTab.Removal,
            VillageMap.MapTool.Harvesting or VillageMap.MapTool.Unmarking => BuildTab.Harvest,

            // ⚠️ The work-ground brush belongs to a BUILDING and is reached from that building's
            // panel (D86, D93), so it is on no tab — and while it is in hand, nothing here lights.
            _ => null,
        };

        foreach ((BuildTab tab, Button button) in _tabButtons)
        {
            // A pressed tab is where you are looking; the tint says where your hand is. They are
            // different questions and they are answered in different channels on purpose.
            button.Modulate = held == tab ? new Color(1f, 0.85f, 0.4f) : Colors.White;
        }

        BuildingKind? building = tool == VillageMap.MapTool.Building ? _map.PendingBuilding : null;
        ToolMark? mark = MarkFor(tool, _map.PendingHarvest);

        foreach ((BuildTab Tab, BuildCategory Category, Button Button, BuildingKind? Kind, ToolMark? Mark) entry in _strip)
        {
            entry.Button.SetPressedNoSignal(
                (entry.Kind is not null && entry.Kind == building)
                || (entry.Mark is not null && entry.Mark == mark));
        }
    }

    /// <summary>Which mark on the strip a tool is drawn by, or null for the buildings.</summary>
    /// <remarks>
    /// ⚠️ <b>Harvesting is one tool and four buttons</b>, which is D92's *"modes of one tool"* seen
    /// from the bar: the mode decides which tiles take the paint and is then forgotten, so
    /// <c>MapTool.Harvesting</c> alone cannot say which button to light. It needs the mode beside
    /// it, which is why <c>PendingHarvest</c> is public.
    /// </remarks>
    private static ToolMark? MarkFor(VillageMap.MapTool tool, HarvestBrush? harvest) => tool switch
    {
        VillageMap.MapTool.PaintingHomes => ToolMark.PaintLand,
        VillageMap.MapTool.ErasingHomes => ToolMark.TakeBack,
        VillageMap.MapTool.Demolishing => ToolMark.Demolish,
        VillageMap.MapTool.Moving => ToolMark.Move,
        VillageMap.MapTool.Emptying => ToolMark.Empty,
        VillageMap.MapTool.Unmarking => ToolMark.Unmark,
        VillageMap.MapTool.Harvesting => harvest switch
        {
            HarvestBrush.Trees => ToolMark.HarvestTrees,
            HarvestBrush.Stone => ToolMark.HarvestStone,
            HarvestBrush.Iron => ToolMark.HarvestIron,
            HarvestBrush.Everything => ToolMark.HarvestAll,
            _ => null,
        },
        _ => null,
    };

    /// <summary>
    /// ⭐ The QA fast-forward — <b>debug builds only</b> (Joe, 2026-08-29).
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>⭐⭐ THE SPEED BUTTONS WERE NEVER GOING TO ANSWER HIS QUESTION, AND THE ARITHMETIC SAYS
    /// SO.</b> A year is 480 ticks at 0.75 ticks/second, so **one year is 10.7 real minutes at 1x
    /// and 64 seconds at 10x.** The 20x he asked for halves that to 32 seconds a year —
    /// <b>still half an hour to reach the town hall at year 58.</b> ⭐ Measured, the sim runs those
    /// 58 years in **1.53 seconds**. *The gap between 31 minutes and 1.5 seconds is the whole
    /// reason this control exists rather than a bigger multiplier.*
    /// </para>
    /// <para>
    /// <b>⛔ IT STOPS EARLY ON A MOMENT, AND THAT IS THE DESIGN RATHER THAN A COURTESY.</b> The
    /// thing being skipped towards is usually the thing that raises one — a gift, a discovery, the
    /// founders' hall. **A skip that ran straight past the event you were skipping to would be
    /// worse than no skip**, because you would have to do it again, more carefully, from a save
    /// you do not have.
    /// </para>
    /// <para>
    /// <b>⚠️ DEBUG BUILDS ONLY, AND THAT IS A NON-NEGOTIABLE CALL RATHER THAN CAUTION.</b>
    /// `DESIGN.md §1` makes the **meditative pace** a constraint on every feature; a button that
    /// skips a decade is a different game, and shipping one to a player would be answering the
    /// pillar with a control that opts out of it. <b>Speed is pacing; a skip is a tool.</b>
    /// `run.bat` builds debug, so Joe has it and an export never will.
    /// </para>
    /// <para>
    /// ⚠️ <b>It leaves the game paused.</b> Landing at speed on the year you wanted to inspect and
    /// immediately watching it scroll past is the same complaint one level down.
    /// </para>
    /// </remarks>
    /// <summary>
    /// ⭐ The frame rate, and the one number that explains it — <b>debug builds only</b>
    /// (D338, Joe: *"can we put an FPS counter in the UI for development purposes?"*).
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Gated on <see cref="OS.IsDebugBuild"/>, the same way <see cref="AddTheSkipControls"/>
    /// is</b> — <c>run.bat</c> builds debug, so Joe has it and an export never will.
    /// </para>
    /// <para>
    /// ⛔ <b>NOT IN THE CONTROL BAR AND NOT A PANEL.</b> The bar's height is a probe invariant
    /// (161 everywhere) and every panel in <c>_docked</c> joins the stacking order — *a
    /// development readout must not be able to move furniture the player uses.* It is a bare label
    /// pinned over the top-left of the valley.
    /// </para>
    /// <para>
    /// ⭐⭐ <b>It reports the zone pass's rectangle count beside the frame rate, and that
    /// is the point of it.</b> *"The framerate feels A LOT more sluggish"* is a feeling; the thing
    /// that made it true was the painted-ground pass drawing sixteen rectangles per tile over
    /// ground with nothing on it (D338). **A frame rate says something is wrong; the rect count
    /// says what.**
    /// </para>
    /// </remarks>
    /// <summary>
    /// ⛔⛔ Every map tick in Settings agrees with the map — <b>the guard on two
    /// independent defaults</b> (D340).
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>A view toggle is written down twice</b>: a field on <c>VillageMap</c> and a
    /// <c>ButtonPressed</c> on a <c>CheckBox</c>. **Nothing has ever connected them** — they
    /// are wired one way, from the tick to the map, so a default changed on one side leaves the
    /// other lying and the player has to click a box twice to make it mean anything.
    /// </para>
    /// <para>
    /// ⚠️ <b>It was a live risk the moment the grid default moved</b> (Joe: *"the
    /// default setting for showing the grid should be off"*) and it is a bigger one now there are
    /// four of them. ⭐ *This is the cheapest kind of guard — the one that reads both
    /// halves of a fact that is stated twice and asks whether they match.*
    /// </para>
    /// <para>
    /// ⚠️ It only covers the toggles registered in <c>_mapToggles</c>, and a fifth
    /// toggle that forgets to register is invisible to it. *The list is one line beside the
    /// control; there is no way to make it automatic without reflection, which this project does
    /// not use.*
    /// </para>
    /// <para>
    /// ⛔⛔ <b>AND IT CHECKS THE DEFAULTS, NOT THE DRAWING — BECAUSE THE PROBE
    /// CANNOT SEE DRAWING AT ALL.</b> Measured (D340): <c>VillageMap._Draw</c> never runs under
    /// <c>--headless</c>, so the zone pass reports **zero rectangles** in the probe. *Every guard
    /// this project has for the view is about layout, geometry or state; whether a hidden layer
    /// actually stops being painted is verified by Joe looking at it and by nothing else.*
    /// </para>
    /// </remarks>
    private string EveryTickSaysWhatTheMapIsActuallyDoing()
    {
        var wrong = new System.Collections.Generic.List<string>();

        for (int i = 0; i < _mapToggles.Count; i++)
        {
            (string name, CheckBox box, System.Func<bool> mapSays) = _mapToggles[i];

            if (box.ButtonPressed != mapSays())
            {
                wrong.Add($"{name}: the tick says {box.ButtonPressed}, the map says {mapSays()}");
            }
        }

        return wrong.Count == 0
            ? $"[widths] map toggles: ✅ all {_mapToggles.Count} ticks match the map"
            : "[widths] map toggles: ⛔ " + string.Join("; ", wrong);
    }

    /// <summary>
    /// Every Settings window tick equals its window's state, and a window that starts hidden
    /// starts unticked — <b>a probe line</b> (D380).
    /// </summary>
    /// <remarks>
    /// Red on the code it replaced: the ticks were written <c>true</c> once at build, so *Stock
    /// limits* read *"the tick says True, the window is hidden"* from the first frame.
    /// </remarks>
    private string EveryWindowTickSaysWhatTheWindowIsDoing()
    {
        Refresh();
        var wrong = new List<string>();
        for (int i = 0; i < _windows.Count; i++)
        {
            ShellWindow window = _windows[i];
            bool ticked = window.Tick?.ButtonPressed ?? window.Wanted;
            if (ticked != window.Wanted)
            {
                wrong.Add($"{window.Name}: the tick says {ticked}, the window is {(window.Wanted ? "wanted" : "not wanted")}");
            }

            // What's here also answers to the selection, so only the others are held to it.
            if (window.Panel != _whatsHerePanel && window.Panel.Visible != window.Wanted)
            {
                wrong.Add($"{window.Name}: the tick says {ticked}, the window is {(window.Panel.Visible ? "shown" : "hidden")}");
            }
        }

        bool limitsStartHidden = _windows.Exists(w => w.Panel == _stockLimitsPanel && !w.Wanted && !w.Panel.Visible);
        if (!limitsStartHidden)
        {
            wrong.Add("Stock limits should start hidden and unticked");
        }

        return wrong.Count == 0
            ? $"[widths] windows: ✅ all {_windows.Count} ticks match their windows; Stock limits starts hidden and unticked"
            : "[widths] windows: ⛔ " + string.Join("; ", wrong);
    }

    /// <summary>Each map toggle, its tick, and what the map itself believes — for the probe.</summary>
    private readonly System.Collections.Generic.List<(
        string Name, CheckBox Box, System.Func<bool> MapSays)> _mapToggles = new();

    /// <summary>
    /// ⭐ Write the baked valley out as a PNG — <b>the only way anything but Joe has ever
    /// looked at it</b> (D342).
    /// </summary>
    /// <remarks>
    /// ⛔ <c>VillageMap._Draw</c> never runs under <c>--headless</c> (measured, D340), so the
    /// probe cannot check a single thing that is drawn. **It can write a file**, and a session
    /// can open that file — which is how the field's warp and kernel were tuned rather than
    /// guessed. Off unless <c>BCLONE_PROBE_WIDTHS</c> is set, like the rest of the probe.
    /// </remarks>
    private void SaveTheValleyBake()
    {
        var bake = new ValleyTexture();
        bake.Refresh(_loop.World);

        // ⚠️ Into `logs/`, which `.gitignore` already covers — a dev
        // artefact that lands beside the source is one that gets committed by accident.
        string folder = System.IO.Path.Combine(
            System.IO.Directory.GetCurrentDirectory(), "logs");

        System.IO.Directory.CreateDirectory(folder);
        string path = System.IO.Path.Combine(folder, "valley-bake.png");

        bake.SaveTo(path);
        GD.Print($"[widths] valley bake written to {path}");
    }

    private void AddTheFrameCounter()
    {
        if (!OS.IsDebugBuild())
        {
            return;
        }

        _frameCounter = new Label
        {
            MouseFilter = Control.MouseFilterEnum.Ignore,
            Position = new Vector2(Edge, Edge),
        };

        _frameCounter.AddThemeFontSizeOverride("font_size", 11);
        _frameCounter.AddThemeColorOverride("font_color", new Color(1f, 1f, 1f, 0.45f));
        AddChild(_frameCounter);
    }

    private Label? _frameCounter;

    /// <summary>Refresh the debug readout. Costs nothing in an export, where it does not exist.</summary>
    private void ShowTheFrameCost()
    {
        if (_frameCounter is null)
        {
            return;
        }

        _frameCounter.Text =
            $"{Engine.GetFramesPerSecond()} fps  ·  {_map.ZoneTrianglesLastFrame} zone tris  ·  {_map.LastFrame}";
    }

    private void AddTheSkipControls(Container controls)
    {
        if (!OS.IsDebugBuild())
        {
            return;
        }

        controls.AddChild(new VSeparator());

        var year = new Button { Text = "Skip 1y", TooltipText = "QA only: run one year at once." };
        year.Pressed += () => SkipYears(1);
        controls.AddChild(year);

        var decade = new Button { Text = "Skip 10y", TooltipText = "QA only: run ten years at once." };
        decade.Pressed += () => SkipYears(10);
        controls.AddChild(decade);
    }

    /// <summary>Run <paramref name="years"/> of sim at once, stopping early on a moment.</summary>
    /// <remarks>
    /// <b>⚠️ IT STEPS THE SAME <c>SimLoop.Step</c> EVERYTHING ELSE DOES, ONE TICK AT A TIME.</b>
    /// Nothing here reaches past the loop or touches the driver's accumulator except to reset it,
    /// so **a village skipped through is byte-identical to one played through at 1x** — the
    /// property `FixedTimestepDriver` already guarantees, and the reason a skip is safe to hand a
    /// tester at all. ⛔ *The moment this method learns a shortcut, that stops being true and QA
    /// stops testing the game.*
    /// </remarks>
    private void SkipYears(int years)
    {
        int ticks = _loop.World.Config.TicksPerYear * years;
        int momentsBefore = _loop.World.Moments.Count;

        var watch = System.Diagnostics.Stopwatch.StartNew();
        int ran = 0;
        for (; ran < ticks; ran++)
        {
            _loop.Step(1);

            if (_loop.World.Moments.Count > momentsBefore)
            {
                ran++;
                break;
            }
        }

        watch.Stop();

        bool onAMoment = _loop.World.Moments.Count > momentsBefore;

        // ⚠️ The accumulator holds real time that has not become ticks yet. Left alone, the first
        // frame after a skip would run whatever was owed before it — a lurch on top of a jump.
        _driver.ResetAccumulator();
        SetSpeed(0.0);

        GD.Print($"[skip] ran {ran} ticks ({ran / (float)_loop.World.Config.TicksPerYear:F1} years) "
            + $"in {watch.Elapsed.TotalSeconds:F2}s, now {_loop.World.Clock}"
            + (onAMoment ? " — stopped early on a moment" : string.Empty));
    }

    private Button SpeedButton(string text, double multiplier)
    {
        var button = new Button { Text = text, CustomMinimumSize = new Vector2(64, 0) };
        button.Pressed += () => SetSpeed(multiplier);
        return button;
    }

    // `NameThem` moved into `LabourSystem` with the alert it was written for. It listed at
    // most four idle workplaces and then said "and N more", because a sentence naming eleven
    // huts is a sentence nobody finishes — and that judgement belongs beside the sentence,
    // which is now the sim's to write.

    /// <summary>
    /// The three type sizes the shell has. <b>Smaller than they were</b> (Joe, 2026-08-09).
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Joe, playing: <em>"the font size for the village log panel is too large. small. same
    /// with the overview. and 'who they are and why'."</em></b> Heading 22 → 18 and body 16 →
    /// 13, and the two scrolling labels are given <see cref="RowSize"/> explicitly rather than
    /// inheriting Godot's default — which is 16 and is why the log and the inspector were the
    /// two panels he named.
    /// </para>
    /// <para>
    /// <b>Type size is the other half of D113's answer.</b> Panels were made see-through and
    /// tighter so they cost attention rather than area; a panel set in 16-point costs area
    /// again by being tall, and the overview grew a twelve-row goods table since. The
    /// alternative — showing less — is the one Joe has repeatedly declined.
    /// </para>
    /// </remarks>
    private const int RowSize = 13;

    /// <summary>
    /// The village log's own size — <b>one point smaller than every other panel</b>.
    /// </summary>
    /// <remarks>
    /// <b>⭐ Joe, 2026-08-29, asking for timestamps: *"smaller font to compensate for the extra
    /// text."*</b> ⚠️ **The timestamp does not actually make a line longer** — the prose date it
    /// replaces is longer than the stamp that replaces it (see <c>Stamped</c>) — but the log is
    /// the one panel that is **read rather than scanned**, and it is the only one that has to hold
    /// sixty years of prose in a fixed box. **12, not 11**: at 11 the log stops matching the
    /// column it sits in and starts looking like a different application.
    /// </remarks>
    private const int LogSize = 12;

    private static Label Heading(string text)
    {
        var label = new Label { Text = text };
        label.AddThemeFontSizeOverride("font_size", 18);
        return label;
    }

    private static Label Body(string text)
    {
        var label = new Label { Text = text };
        label.AddThemeFontSizeOverride("font_size", RowSize);
        return label;
    }

    /// <summary>
    /// A muted line that <b>wraps rather than widening its column</b>.
    /// </summary>
    /// <remarks>
    /// <b>⭐ A LABEL THAT CANNOT WRAP SETS A FLOOR UNDER THE WHOLE COLUMN (D149).</b> The
    /// column is a <see cref="ScrollContainer"/> with horizontal scrolling disabled, so its
    /// minimum width is its widest child's — and one caption of forty-nine characters
    /// (*"Professions — how many people on each kind of work"*) held the panel at 325 pixels
    /// however narrow the window told it to be. Making the columns a share of the window
    /// achieved nothing for the panel that needed it most until this landed with it.
    /// </remarks>
    private static Label Caption(string text)
    {
        Label label = Muted(text);
        label.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        label.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        return label;
    }

    private static Label Muted(string text)
    {
        var label = new Label { Text = text };
        label.AddThemeFontSizeOverride("font_size", 12);
        label.Modulate = new Color(1, 1, 1, 0.55f);
        return label;
    }
}

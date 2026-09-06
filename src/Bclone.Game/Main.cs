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
    private Label _villageLabel = null!;
    private Label _seedLabel = null!;
    private Label _speedLabel = null!;
    private ItemList _roster = null!;
    private RichTextLabel _inspector = null!;
    private VBoxContainer _staffingRow = null!;

    /// <summary>The per-villager trade pins — one toggle per trade.</summary>
    private VBoxContainer _pinRow = null!;
    private Label _pinLabel = null!;
    private readonly List<(JobKind Trade, Button Button)> _pinButtons = new();
    private Label _staffingLabel = null!;
    private VBoxContainer _queueRow = null!;
    private Label _queueLabel = null!;
    private VBoxContainer _groundRow = null!;
    private Label _groundLabel = null!;
    private Label _groundNote = null!;
    private Button _modeButton = null!;
    private VBoxContainer _storeRow = null!;
    private VBoxContainer _acceptRow = null!;
    private Button _fullMarkerButton = null!;
    private VBoxContainer _idleRow = null!;
    private Label _idleLabel = null!;
    private Button _idleMarkerButton = null!;
    private RichTextLabel _villageLog = null!;
    private VillageMap _map = null!;

    private Button _detailButton = null!;
    private Button _soilButton = null!;

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
        if (ticks > 0)
        {
            _loop.Step(ticks);
        }

        Refresh();
        ProbeColumnWidths();
    }

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

        for (int i = 0; i < _docked.Count; i++)
        {
            (PanelContainer panel, bool right) = _docked[i];
            GD.Print(
                $"[widths] panel {(right ? "right" : "left ")} at "
                + $"({panel.Position.X:F0}, {panel.Position.Y:F0}) "
                + $"size {panel.Size.X:F0}x{panel.Size.Y:F0}"
                + (panel.Visible ? string.Empty : " (hidden)"));
        }

        // ⭐⭐ THE TWO SELF-SCROLLING PANELS, MEASURED — because they are the two that can hold
        // their content correctly and draw NONE of it. Both were `size 288x0` for the life of
        // D306: an `ItemList` and a `ScrollFollowing` `RichTextLabel` each report a minimum
        // height of zero, so a `ScrollContainer` around either lays it out at nothing. **Joe saw
        // a blank roster beside "6 villagers in 2 households".** *A count beside a drawn height
        // is the only pair that can say this; neither number alone can.*
        ProbeASelfScroller("roster", _roster, _roster.ItemCount, "items");
        ProbeASelfScroller("vlog", _villageLog, _villageLog.GetParsedText().Length, "chars");

        ProbeTheInspectorRows();
        ProbeTheControlBar();
        ProbeTheProfessionsPanel();

        ProbeTheLogLines();
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
        BuildTab wasOn = _tab;
        var heights = new List<string>();
        float first = -1f;
        bool steady = true;

        foreach (BuildTab tab in new[] { BuildTab.Build, BuildTab.Removal, BuildTab.Harvest })
        {
            _tab = tab;
            RefreshTheStrip();
            _controlBar.QueueSort();
            ForceUpdateTransform();

            float tall = _controlBar.Size.Y;
            heights.Add($"{tab} {tall:F0}");

            if (first < 0f)
            {
                first = tall;
            }
            else if (Mathf.Abs(tall - first) > 1f)
            {
                steady = false;
            }
        }

        _tab = wasOn;
        RefreshTheStrip();
        _controlBar.QueueSort();
        ForceUpdateTransform();

        GD.Print(
            $"[widths] bar height per tab: {string.Join(", ", heights)}"
            + (steady
                ? "  ✅ one height on every tab"
                : "  ⛔ THE BAR CHANGES HEIGHT — the map will jump as the player switches tabs"));
    }

    /// <summary>
    /// What the inspector's rows will want once the player selects something — posed, because
    /// they are empty until then.
    /// </summary>
    private void ProbeTheInspectorRows()
    {
        GD.Print("[widths] --- inspector rows, with the longest sentence each can hold ---");

        Pose(_staffingRow, _staffingLabel, "Staffing the south-western forester's hut 2 — 2 of 3:");
        Pose(_groundRow, _groundLabel, "Ground — 128 tiles, enough hands for 26:");
        Pose(_queueRow, _queueLabel, "3rd in the queue, after a granary and a stockpile:");
        Pose(
            _idleRow,
            _idleLabel,
            "Nothing to sow at the south-western farmhouse 2 — you asked the village to keep "
            + "2000 food and it has 1834.");
        Pose(
            null,
            _groundNote,
            "The south-western farmhouse 2 is 128 tiles of field and 2 pairs of hands can sow "
            + "26 of them. The other 102 will lie fallow — put another farmer on, or paint a "
            + "smaller field.");

        void Pose(Container? row, Label label, string worst)
        {
            string was = label.Text;
            label.Text = worst;

            float wants = row is null
                ? label.GetCombinedMinimumSize().X
                : row.GetCombinedMinimumSize().X;

            GD.Print($"[widths] right   {(row is null ? "note" : "row ")} wants {wants:F0} — \"{worst}\"");

            // ⭐ AND WHAT IN THE ROW IS ASKING FOR IT. A row's minimum width is the sum of its
            // children's, so the total says a column is being held open and says nothing about
            // by what — which is the question the fix turns on.
            if (row is not null)
            {
                foreach (Node child in row.GetChildren())
                {
                    if (child is Control part)
                    {
                        string what = part switch
                        {
                            Label inner => $"Label \"{Shorten(inner.Text)}\"",
                            Button button => $"Button \"{button.Text}\"",
                            _ => part.GetType().Name,
                        };

                        GD.Print($"[widths] right       {part.GetCombinedMinimumSize().X,4:F0}  {what}");
                    }
                }
            }

            label.Text = was;
        }
    }

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
    private static string Shorten(string text) =>
        text.Length <= 40 ? text : text[..40] + "…";

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
            case Key.Home: _map.CentreOnTheVillage(); break;

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

        _clockLabel.Text = $"{world.Clock}   ·   tick {world.Tick}";

        // WHO IS HERE, BROKEN DOWN BY LIFE STAGE (Joe's area 1). "17 villagers" is the
        // number; "11 adults and 4 children" is the one that tells you whether the village
        // is growing or ageing out, which is the question a generational game is about.
        // Counted here rather than on the world: the roster already walks this list every
        // frame, a village is tens of people, and a sim reader would be a second way of
        // asking the same question.
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

        // Two short lines rather than one long one: at this column width a single sentence
        // wrapped mid-clause, which reads as an accident rather than as a layout.
        _villageLabel.Text =
            $"{world.Population} villagers in {LivingHouseholds(world)} households\n" +
            $"{adults} adults · {children} children · {elders} elders";

        // WHAT IS IN THE STORES, one row per good (D83). Totals across every granary and
        // warehouse, not the first of each (D38) — a village that has built a second one should
        // see what is in it.
        //
        // Food carries what is NOT in the stores in the same row rather than in a sentence
        // of its own. The two numbers do not overlap, and showing them apart is how the old
        // line read as a total and its largest part when it was neither. "Homes and huts"
        // rather than "larders", because a workplace buffer is neither a store nor a larder
        // and calling it one would be the kind of near-enough label D76 keeps punishing.
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
            int inHeaps = world.OnTheGround(goods);
            held.Text = inHeaps > 0
                ? $"{inStores.Grouped()}  (+{inHeaps.Grouped()} on the ground — "
                    + $"{WhyItIsOnTheGround(world, goods)})"
                : inStores.Grouped();
        }

        // The umbrella, split the way the old Food row was: what the stores hold, and what is out
        // in the larders behind it.
        int foodInStores = world.FoodInGranaries();
        int foodElsewhere = world.TotalFood() - foodInStores;
        _foodTotal.Text = foodElsewhere > 0
            ? $"{foodInStores.Grouped()}  (+{foodElsewhere.Grouped()} in homes and huts)"
            : foodInStores.Grouped();

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
            (JobKind kind, Label maximum, Label notes) = _professionReadouts[i];

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

            // ⭐⭐ AND WHY IT WANTS NONE, WHEN THE PLAYER HAS ASKED FOR SOME (Joe, 2026-08-30).
            // **That is the one combination that reads as a contradiction** — *"asked 1 · village
            // wants 0"* — and it is the row he was looking at. Every other row is short because
            // there is nothing to reconcile: a village that wants none and was asked for none is
            // simply agreeing with itself.
            //
            // ⚠️ Same method as the inspector, so the two panels cannot drift apart, which was
            // the actual complaint. The label wraps (D113), so the extra clause costs a line
            // rather than running off the edge of the column.
            if (quota.For(kind) == 0 && asked is int some && some > 0
                && LabourQuota.WhyTheVillageWantsNone(world, kind) is string reason)
            {
                row += $"  ⚠ {reason}";
            }

            // ⭐⭐ AND WHEN THE VILLAGE NEEDS MORE THAN IT HAS ROOM FOR, IT SAYS SO (Joe,
            // 2026-09-01). *"I want 2 seats at a woodcutter. Players have to build another
            // building if they want more woodcutters."* ⛔ **That answer only works if the player
            // is told**, and this row could not tell them: `quota.For` is capped by the seats
            // that exist, so a village needing three woodcutters and holding two reported
            // *"village wants 2"* — true, and it hides the one fact worth acting on.
            //
            // ⚠️ This is the sentence that does for every trade what competing rings (D260) did
            // for the forager: **it is what stops a seat cap being a silent shortage.**
            if (quota.Needed(kind) > seats && world.JobsCatalog.WorksAt(kind) is BuildingKind at)
            {
                row += $"  ⚠ needs {quota.Needed(kind)}, build another "
                    + $"{world.BuildingsCatalog[at]?.Name ?? "one"}";
            }

            notes.Text = row;
        }

        // And what the 1 is one OF, which is the whole of Joe's question.
        _laborerReadout.Text =
            $"Available adults: {world.AbleAdults}   |   Unassigned (laborers): {world.Laborers}";

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
        // The staffing buttons belong to whatever is selected, so they follow it.
        //
        // ⚠️ EXCEPT A CONSTRUCTION SITE, WHICH IS NO LONGER STAFFABLE (D108). D104 made
        // sites staffable on the grounds that "how many builders on this?" is the question a
        // player most often wants to answer — and it still is, but the place to answer it is
        // the builder's hut now. A −1/+1 on a footprint would set a number nothing reads,
        // which is worse than a control that is not there.
        Workplace? selected = SelectedWorkplace();
        // ⭐⭐ THE PIN ROW, AND IT SHIPS IN THE SAME COMMIT AS THE SIM FEATURE — the rule this
        // project has paid for seven times (*"a sim feature is not done until something in the
        // view calls it"*). It shows only when a villager is selected, because a control with no
        // subject is worse than an absent one.
        Villager? pinnable = world.FindVillager(_selectedVillagerId);
        if (pinnable is not null && !pinnable.Alive)
        {
            pinnable = null;
        }

        _pinRow.Visible = pinnable is not null;
        if (pinnable is not null)
        {
            _pinLabel.Text = pinnable.PinnedTrade is JobKind kept
                ? $"Kept on {world.JobsCatalog.NameOf(kept)} — press it again to hand them back:"
                : $"Kept on: (the village decides where {pinnable.Name} works)";

            foreach ((JobKind trade, Button button) in _pinButtons)
            {
                button.Text = ProfessionName(_loop.World, trade);
                button.SetPressedNoSignal(pinnable.PinnedTrade == trade);

                // ⚠️ A trade nowhere in the village can still be pressed — the sim says why
                // rather than the button refusing, because "you kept them on forestry and there
                // is no forester's hut" is more use than a control that does nothing.
                button.Disabled = !pinnable.CanWork;
            }
        }

        Workplace? staffable = selected is { IsSite: false } ? selected : null;

        _staffingRow.Visible = staffable is not null;
        if (staffable is not null)
        {
            // ⭐ THE LABEL SAYS WHAT IS TRUE, WHICH IS NOT WHAT IT USED TO SAY. It read
            // "village.s choice" for an untouched building — and the village never chose
            // anything: `Places => StaffingOverride ?? Capacity` means an untouched building is
            // worked by everyone who fits. One number either way now, because the difference
            // the old wording drew is a difference the sim does not make.
            //
            // ⛔⭐ AND IT WAS STILL LYING, BECAUSE `Places` IS A CEILING AND NOT A COUNT — D148'S
            // BUG, ONE PANEL OVER, AND JOE READ IT (2026-08-22). His farm said *"Staffing
            // farmhouse 1 — 2 of 2"* directly above *"1 pair of hands can sow 13"*: two true
            // sentences that cannot both be about the same thing. The farm has two seats and
            // one person in it, because the village has four adults and three other jobs.
            //
            // The allowance is read off `WorkerIds` on purpose (D86 — a hut whose forester dies
            // is overstretched that moment), so the fix is not to change what the ground line
            // counts. **It is for the staffing line to say who turned up**, in the vocabulary
            // the professions panel already learned in D148.
            int working = staffable.WorkerIds.Count;
            string turnout = working == 0
                ? $"nobody working of {staffable.Capacity} seats"
                : $"{working} working of {staffable.Capacity} seats";

            _staffingLabel.Text = staffable.Places == working
                ? $"Staffing {staffable.Name} — {turnout}:"
                : $"Staffing {staffable.Name} — {turnout} · asked {staffable.Places}:";
        }

        // The ground controls belong to a building that keeps ground. ⭐ ASKED OF THE SIM NOW
        // (`SimWorld.KeepsWorkGround`) RATHER THAN BY NAMING A KIND — this line used to read
        // `Kind: JobKind.Forester` under a comment promising it did not, and the farmhouse
        // shipped with no brush because of it. Joe placed a farm, read *"give it some with the
        // work-ground brush"* on its own panel, and there was no brush.
        bool keepsGround = staffable is not null && SimWorld.KeepsWorkGround(staffable.Kind);
        _groundRow.Visible = keepsGround;
        _groundNote.Visible = false;
        if (keepsGround)
        {
            int tiles = world.Zones.WorkGroundTiles(staffable!.Id);
            int allowance = world.WorkGroundAllowanceFor(staffable);

            // ⚠️ "ENOUGH HANDS FOR 0" IS ARITHMETIC, NOT A SENTENCE. An unstaffed building
            // reads as though the ground itself were worthless; what is true is that nobody
            // is on it, and that is a different thing to go and fix.
            _groundLabel.Text = staffable.WorkerIds.Count == 0
                ? $"Ground — {tiles} tiles, nobody working it:"
                : $"Ground — {tiles} tiles, enough hands for {allowance}:";

            // The sentence is the sim's (`SimWorld.OverstretchedNote`), the same one the
            // brush says on the stroke — so the panel and the brush cannot describe one
            // state two ways (D147's rule for the idle marker, one control over).
            if (world.OverstretchedNote(staffable) is string stretched)
            {
                _groundNote.Text = stretched;
                _groundNote.Visible = true;
            }

            // ⭐ THE TOGGLE IS FELLING NOW, NOT PLANTING (Joe, D146). Painting ground for a hut
            // is already the instruction to keep it wooded, so planting was never the
            // interesting question — what the player decides is whether timber comes out.
            //
            // And it says when the village has stopped felling for a reason the player set
            // somewhere else: a met Logs limit reads on this button rather than only on the
            // stock panel, because this is the building that looks idle because of it.
            //
            // ⚠️ FORESTER ONLY. A farm keeps ground too, and there is nothing on it to fell —
            // a "Felling: ON" button beside a field is a control that acts on nothing, which is
            // worse than one that is missing.
            _modeButton.Visible = staffable.Kind == JobKind.Forester;
            if (_modeButton.Visible)
            {
                _modeButton.Text = staffable.Mode != WorkMode.FellAndPlant
                    ? "Felling: off"
                    : world.MayFell(staffable)
                        ? "Felling: ON"
                        : "Felling: ON — held by the log limit";
            }
        }

        // ⭐ AND THE IDLE MARKER, which belongs to a workplace the same way the full marker
        // belongs to a store (Joe, D147). The sentence is `SimWorld.IdleNote`'s, so the panel
        // and the ring on the map can never say different things about the same building.
        _idleRow.Visible = staffable is not null;
        if (staffable is not null)
        {
            string? why = world.IdleNote(staffable);
            _idleLabel.Text = why ?? $"{staffable.Name} is working.";
            _idleMarkerButton.Text = _map.IdleMarkerShownFor(staffable.Id)
                ? "Marker: ON"
                : "Marker: off";
        }

        // The full-store marker belongs to a store, and every store can fill.
        StoreBuilding? store = SelectedStore();
        _storeRow.Visible = store is not null;
        if (store is not null)
        {
            _fullMarkerButton.Text = _map.FullMarkerShownFor(store.Id)
                ? "Marker: ON"
                : "Marker: off";
        }

        _acceptRow.Visible = store is not null;
        if (store is not null)
        {
            foreach ((Goods goods, Button button) in _acceptButtons)
            {
                // Shown only where the KIND could hold it — a granary is not offered "iron".
                // Asked of a bare copy so the player's own filter does not hide the button
                // that would turn it back on.
                button.Visible = store.CanEverHold(goods);
                button.ButtonPressed = store.Accepts(goods);
            }
        }

        // The queue controls only mean anything for something still being built — and they
        // read `selected` rather than `staffable`, because a site is exactly what they are
        // for and exactly what is no longer staffable.
        _queueRow.Visible = selected is { IsSite: true };
        if (selected is { IsSite: true })
        {
            _queueLabel.Text =
                $"Build queue — {world.QueuePositionOf(selected)} of {world.BuildQueue().Count}:";
        }

        if (_selectedTile is GridPos tile)
        {
            _inspector.Text = DescribeWhatIsAt(world, tile);
            return;
        }

        Villager? villager = world.FindVillager(_selectedVillagerId);
        if (villager is null)
        {
            _inspector.Text =
                "Select a villager to see what they are doing, and why — " +
                "or click anything on the map to see what it is.";
            return;
        }

        Household household = world.HouseholdOf(villager);
        Workplace? workplace = world.FindWorkplace(villager.WorkplaceId);

        int hungerPercent = world.Config.HungerMax == 0
            ? 0
            : villager.Hunger * 100 / world.Config.HungerMax;

        var lines = new List<string>
        {
            $"{villager.Name}, aged {villager.AgeYears}",
            villager.Alive ? $"Currently: {villager.DescribeState(workplace?.Name)}" : "Dead.",
            $"Household: the {household.Name} household "
                + $"({_loop.World.FoodIn(household.Stockpile)} food, " +
                $"{household.Stockpile.Firewood} firewood, {household.Stockpile.Logs} logs)",
            $"Hunger: {hungerPercent}%",
        };

        // Why someone with a job is sitting at home. Before the woodcutter's hut this
        // never had an interesting answer; now a manned building can be idle for want
        // of logs, and that has to be readable (D29).
        if (!string.IsNullOrWhiteSpace(villager.WorkNote))
        {
            lines.Add(villager.WorkNote);
        }

        // ⭐ WHAT THE WALK COSTS THEM, and this panel is where deleting catchment is paid
        // for (spec §7.1). The fence used to make a ruinous commute impossible; with it gone
        // the village can quietly thin out because half its hands are on the road, and a
        // sentence on the person doing the walking is the only thing that makes that fair
        // rather than merely hard. Empty for an ordinary commute, so it means something when
        // it is there.
        if (!string.IsNullOrWhiteSpace(villager.CommuteNote))
        {
            lines.Add(villager.CommuteNote);
        }

        if (villager.Stage != VigourStage.Prime)
        {
            lines.Add(villager.Stage == VigourStage.Frail
                ? $"Vigour: {villager.Vigour}% — frail; every trip brings back less"
                : $"Vigour: {villager.Vigour}% — past their strongest years");
        }

        DescribeTheirTrades(world, villager, lines);

        // ⭐⭐ AND WHAT GOES WITH THEM IF NOBODY LEARNS IT (`skills-catalog.md §7`, D195).
        // The village log says this once when it becomes true; the panel says it for as long as
        // it IS true, because the player who clicked on Mabel is exactly the player who can act
        // on it. **Both read `SimWorld.KnowledgeAtRiskNote`** — D147's rule for `IdleNote`, and
        // the reason is that two copies of one condition is how the log and the panel come to
        // disagree about who is at risk (D142, D148).
        if (world.KnowledgeAtRiskNote(villager) is string atRisk)
        {
            lines.Add(atRisk);
        }

        if (villager.IsPaired)
        {
            Villager? partner = world.FindVillager(villager.PartnerId);
            if (partner is not null)
            {
                lines.Add($"Partner: {partner.Name}");
            }
        }

        // ⭐⭐ WHAT THIS PERSON HAS WORKED OUT (Joe, 2026-08-27: *"if a villager has unlocked a
        // technique, it should be highlighted in their 'inspector' going forward"*).
        //
        // ⛔ **The sim knew this all along and had no way to be asked.** `KnowledgeStates` says
        // what the VILLAGE has and `SkillProgress` says what a PERSON has mastered; nothing
        // joined them, so the one screen about a particular villager could not say the one thing
        // that makes them irreplaceable. **A technique was a village-level fact with a person's
        // name in the log entry and nowhere else** — the sixth instance of a sim feature the view
        // could not reach, and the first that needed a query rather than a button.
        //
        // ⭐ THE AT-RISK HALF IS WHAT MAKES IT ACTIONABLE, not the list. Knowing Mabel understands
        // coppicing is pleasant; knowing she is the ONLY one is a decision — put somebody beside
        // her, or build a library — and it is the same claim `KnowledgeAtRiskNote` makes about a
        // skill, one level up (D195).
        List<TechniqueRow> carried = world.TechniquesCarriedBy(villager);
        if (carried.Count > 0)
        {
            lines.Add(string.Empty);
            lines.Add(carried.Count == 1 ? "Knows a technique:" : "Knows techniques:");

            foreach (TechniqueRow technique in carried)
            {
                lines.Add(world.IsOnlyCarrierOf(villager, technique)
                    ? $"  ★ {technique.Name} — and nobody else alive knows it"
                    : $"  ★ {technique.Name}");
            }

            // ⚠️ Said once for the person rather than once per technique: a villager holding
            // three unwritten techniques does not need the remedy three times.
            bool anyUnwritten = false;
            foreach (TechniqueRow technique in carried)
            {
                if (!world.IsWrittenDown(technique.Id))
                {
                    anyUnwritten = true;
                    break;
                }
            }

            if (anyUnwritten)
            {
                lines.Add("  Not all of it is written down — a library keeps what a life cannot.");
            }
        }

        lines.Add(string.Empty);
        lines.Add(workplace is null ? "Work: none" : $"Work: {workplace.Name}");

        // The phase's actual deliverable: a straight answer to "why this job?".
        if (!string.IsNullOrWhiteSpace(villager.JobReason))
        {
            lines.Add($"Why: {villager.JobReason}");
        }

        _inspector.Text = string.Join("\n", lines);
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
        Workplace? site = null;
        foreach (Workplace workplace in _loop.World.Workplaces)
        {
            if (workplace.Position != tile)
            {
                continue;
            }

            if (!workplace.IsSite)
            {
                return workplace;
            }

            site ??= workplace;
        }

        return site;
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

    private void ChangeStaffing(int delta)
    {
        Workplace? workplace = SelectedWorkplace();
        if (workplace is null || workplace.IsSite)
        {
            return;
        }

        int from = workplace.StaffingOverride ?? workplace.Places;
        int wanted = from + delta;
        if (wanted < 0)
        {
            wanted = 0;
        }

        _loop.World.SetStaffing(workplace, wanted);
        RefreshInspector(_loop.World);
    }

    private void OnBuildingClicked(GridPos tile)
    {
        _selectedTile = tile;
        _selectedVillagerId = 0;
        _roster.DeselectAll();
        RefreshInspector(_loop.World);
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

        foreach (Workplace workplace in world.Workplaces)
        {
            if (workplace.Position == tile)
            {
                DescribeWorkplace(world, workplace, lines);
            }
        }

        foreach (StoreBuilding store in world.StoreBuildings)
        {
            if (store.Position == tile)
            {
                DescribeStore(world, store, lines);
            }
        }

        foreach (Household household in world.Households)
        {
            if (household.HomePosition == tile)
            {
                DescribeHome(world, household, lines);
            }
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
            if (library.Position == tile)
            {
                DescribeLibrary(world, library, lines);
            }
        }

        // ⛔ THE FIFTH LIST, AND IT IS HERE IN THE SAME COMMIT AS THE BUILDING (D252). The comment
        // directly above records the library shipping built, paid for, watched being raised, and
        // then reading as *"open ground"* — because this method knew about three kinds of thing
        // that can stand on a tile and did not know about a fourth. **A fifth was always going to
        // arrive; this is it.**
        if (world.TownHall is { } hall && hall.Position == tile)
        {
            DescribeTheTownHall(world, hall, lines);
        }

        if (lines.Count == 0)
        {
            DescribeBareGround(world, tile, lines);
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

    private static void DescribeWorkplace(SimWorld world, Workplace workplace, List<string> lines)
    {
        Separate(lines);

        // A site under construction is a different thing from the building it will
        // become, and saying "the granary" of a patch of pegged-out ground would be a
        // small lie the player would have to un-learn.
        if (workplace.Construction is ConstructionSite site)
        {
            lines.Add($"{site.Name} — under construction");
            // ⭐ EVERY MATERIAL, NAMED BY THE CATALOGUE (D213). This read one good and one
            // number, so a site short of stone reported all its timber delivered and looked
            // finished while nothing moved — §1.1 failing in the player's favour, which is
            // still failing.
            lines.Add(site.HasMaterials
                ? $"Materials: all of {site.Recipe.Describe(world.GoodsCatalog)} delivered"
                : $"Materials: still wants "
                  + $"{site.DescribeWhatIsMissing(world.GoodsCatalog)} of "
                  + $"{site.Recipe.Describe(world.GoodsCatalog)}");
            lines.Add($"Work: {site.WorkDone} of {site.Recipe.WorkTicks} ticks done");

            // ⭐ WHERE IT IS IN THE QUEUE, AND WHAT IS AHEAD OF IT (Joe). A site sitting at
            // "0 of 30 ticks" with nobody on it is the opaque stall D93 rules out twice: the
            // player can only act on it — by freeing a hand, or by cancelling something —
            // if they can see WHAT is in front of it.
            //
            // The number is the real order the village works in, not a display convention.
            List<Workplace> queue = world.BuildQueue();
            int place = world.QueuePositionOf(workplace);
            if (place > 0)
            {
                lines.Add(place == 1
                    ? $"Queue: 1st of {queue.Count} — nothing is ahead of it."
                    : $"Queue: {Ordinal(place)} of {queue.Count} — "
                        + $"{queue[place - 2].Construction!.Name} is immediately ahead of it.");
            }

            // And the ground, which is the other thing that can stop it dead (D101).
            if (!world.GroundIsClearAt(workplace.Position))
            {
                lines.Add("Waiting: the ground it stands on is still being cleared.");
            }

            // ⭐⭐ AND THE THING THAT ACTUALLY STOPPED JOE'S GRANARY FOR TWENTY-ONE YEARS
            // (2026-08-27). "Materials: still wants 10 stone" above is true and is not the
            // answer — it says WHAT is missing, never that the village has no way to get it.
            // His granary read exactly that line every year from 23 to 44 while nobody ever
            // went to a seam.
            //
            // ⭐ THE SAME METHOD THE VILLAGE LOG USES, so the two cannot disagree — D195's
            // rule for the at-risk line, and the reason it is one method rather than two
            // sentences. Narrated once on the edge, shown here for as long as it is true.
            if (world.SiteWaitingNote(workplace) is string stalled)
            {
                lines.Add(stalled);
            }

            // ⭐ A SITE HAS NOBODY POSTED TO IT ANY MORE (D108), so it must not go on to the
            // staffing lines below — they would read "Nobody works here. Room for 0", which
            // is true of a place nobody can ever be posted to and is the wrong answer to the
            // question the player is asking. What they want to know is whether anybody is
            // coming, and the honest answer is about the hut.
            lines.Add(world.HasABuildersHut()
                ? "Raised by the builders, who walk out to it from their hut — a site is an "
                    + "errand, not a place anybody is posted to."
                : "Nobody in the village builds, so nothing will be raised here. A builder's "
                    + "hut costs nothing but the ground it stands on.");

            return;
        }

        lines.Add($"{workplace.Name} — a workplace ({Describe(workplace.Kind)})");

        // ⭐⭐ WHO IS HERE, AND — WHEN NOBODY IS, OR WHEN THEY HAVE NOTHING TO DO — WHY
        // (Joe, 2026-08-30). *"I dont like that because the village 'wants' 0 of a type of work,
        // the workplace shows as unstaffed, even though it is staffed and the worker is just
        // idle … show 'X works here, but there is no need for this work at this time because of
        // X, Y, Z'. They are inconsistent now and i want them to be aligned."*
        //
        // ⛔ HE WAS READING TWO TRUE SENTENCES THAT DID NOT ADD UP: this panel said *"Nobody
        // works here. Room for 2."* while the professions column said *"nobody working of 2 seats
        // · asked 1 · village wants 0."* **Neither said why**, so the only way to reconcile them
        // was to already know how the quota works.
        //
        // ⭐ The reason comes from `LabourQuota.WhyTheVillageWantsNone` — the same method the
        // professions row now reads, asked of the same state the decision was made from. *Two
        // panels explaining one decision in two places is how they come to disagree (D139, D195).*
        int wanted = LabourQuota.For(world).For(workplace.Kind);
        string plural = world.JobsCatalog.PluralOf(workplace.Kind);
        string? why = wanted == 0 ? LabourQuota.WhyTheVillageWantsNone(world, workplace.Kind) : null;

        string noNeed = wanted > 0
            ? string.Empty
            : why is null
                ? $"the village needs no {plural} at the moment"
                : $"the village needs no {plural} at the moment, because {why}";

        if (workplace.WorkerIds.Count == 0)
        {
            lines.Add(noNeed.Length == 0
                ? $"Nobody works here yet — the village wants {wanted} on this work and has "
                  + $"nobody to spare. Room for {workplace.Places}."
                : $"Nobody works here — {noNeed}. Room for {workplace.Places}.");
        }
        else
        {
            string filled = $"Worked by {WorkerNames(world, workplace)} — "
                + $"{workplace.WorkerIds.Count} of {workplace.Places} places filled";

            // ⭐ JOE'S SENTENCE, VERBATIM IN SHAPE: *"X works here, but there is no need for this
            // work at this time because of X, Y, Z."* A staffed building whose trade the village
            // has no call for is the case that read as a contradiction across two panels.
            lines.Add(noNeed.Length == 0 ? filled : $"{filled}, though {noNeed}.");
        }

        // Who decided that number (D51). Said in words rather than shown as a widget
        // state, because "the village decides" and "you said two" are different facts
        // about the same building and the player should be able to tell which they are
        // looking at.
        //
        // ⚠️ The "it wants N" half moved into the line above, where it now travels with its
        // reason. Repeating it here read as a second, quieter opinion about the same number.
        // ⚠️ BOTH ARMS SAY `Capacity` DELIBERATELY, AND THIS IS THE ONE PLACE IT IS RIGHT.
        // The line above reports `Places` — the seats in force — and this one is about the
        // BUILDING: *"you asked for 1; the hut holds 2"* is the sentence that tells the player
        // their own override is what is binding. Until 2026-09-01 the two lines used the same
        // word "Room for" for those two different facts, so with an override set the panel
        // printed two different numbers eight lines apart and neither said which was which.
        lines.Add(workplace.StaffingOverride is int set
            ? $"Staffing: you have asked for {set}, and the building holds {workplace.Capacity}."
            : $"Staffing: left to the village. The building holds {workplace.Capacity}.");

        // ⭐ WHAT THE GROUND IS WORTH, AND THIS IS NOT POLISH (`forests-and-gathering.md`
        // §7.1). A gatherer's hut whose ring has been felled brings back less and less, and a
        // village thinning out with nothing on screen saying why is §1.1 failing — the one
        // uncozy state §0.1 rules out. **The sentence is what makes "no forest, no food"
        // fair**, exactly as D93 ruled about a stalled construction site, so it ships with the
        // mechanic rather than after it.
        if (workplace.GatheringRadius > 0)
        {
            int ring = VillageEconomy.TilesInRing(workplace.GatheringRadius);
            int wooded = world.WoodedTilesAround(workplace);
            int share = ring <= 0 ? 0 : wooded * 100 / ring;

            lines.Add($"Ground: {wooded} wooded tiles of {ring} within {workplace.GatheringRadius}.");
            // ⭐ THE WORKERS ARE NAMED BY THE JOBS CATALOGUE, NOT BY A WORD TYPED HERE
            // (Joe, 2026-08-27: *"forager hut workers still referred to as 'gatherers'"*).
            // D188 made one place name a trade and this sentence was not asking it — which is
            // precisely D108's bug, where a naming path ignored the right answer sitting one
            // call away. **Nothing in the suite can guard a string in the view** (there is no
            // view test project at all), so the only real defence is not to hold the word here.
            lines.Add(wooded == 0
                ? $"Nothing grows here any more — its {world.JobsCatalog.PluralOf(workplace.Kind)} "
                    + "bring back nothing at all. Plant it, or move the work."
                : $"A trip brings back {world.GatherYieldAt(workplace)} food — {share}% of what "
                    + "this hut would yield in full woodland.");
        }

        // ⭐ THE FIELD (`specs/crops-and-orchards.md`). Same argument as the gatherer's ring
        // one screen up, and the same reason it ships with the mechanic rather than after it:
        // *use it or lose it* is only fair if the player can see it coming, so a standing crop
        // in autumn has to be readable off the panel while it can still be acted on.
        if (workplace.Kind == JobKind.Farmer)
        {
            int ground = world.Zones.WorkGroundTiles(workplace.Id);
            int standing = world.StandingCropTiles(workplace);

            // ⭐⭐ WHAT *THIS* FARM COMMITS, NOT WHAT THE DERIVATION GIVES A WELL-SITED ONE
            // (D194, `per-site-yield.md §4.2a`). This said *"every hand here can keep 13"* on
            // every farm in the valley — and for a farm ten ticks from a granary that number is
            // simply false: it can bring in six, and it says so on the panel now. **A number the
            // building cannot achieve is worse than no number**, because the player reads it and
            // then watches the farm miss it every autumn with no explanation offered.
            int keeps = world.FieldTilesThisFarmCommitsPerHand(workplace);
            int derived = world.TilesOneWorkerKeeps(JobKind.Farmer);

            lines.Add(ground == 0
                ? "Ground: none. Give it some with the work-ground brush and it will be "
                    + "ploughed."
                : $"Ground: {ground} tiles, {standing} of them under crop. Every hand here "
                    + $"sows {keeps}.");

            // ⭐ AND WHY IT IS LESS, WHICH IS THE HALF THE PLAYER CAN ACT ON. The walk to the
            // store is the lever — a granary beside the fields and the same farm commits the
            // whole field — so the sentence names it rather than leaving the number bare.
            // Silent on a well-sited farm: one considered sentence, not a nag (D42).
            if (ground > 0 && keeps < derived)
            {
                int haul = world.HaulWalkFor(workplace);
                lines.Add(
                    $"That is short of the {derived} a farm beside a store keeps — its harvest "
                    + $"walks {haul} ticks to the nearest one. Build a store near the fields.");
            }

            // ⭐ AND WHETHER THAT GROUND WAS WORTH GIVING IT (D178). The farm is the one
            // building whose output soil actually moves — a field on rich ground out-yields a
            // field on thin by two to one — so "why is this farm slow?" has an answer the panel
            // was not giving. Averaged over the tiles it holds rather than sampled at the
            // farmhouse: soil is regional at lattice 8 and a farm's ground can straddle two
            // regions, so the doorstep tile is not the answer.
            //
            // Only once it has ground, because the line above already says what to do about
            // having none and two instructions are one too many.
            if (ground > 0)
            {
                lines.Add(DescribeSoil(world.FarmGroundShare(workplace)));
            }

            lines.Add(SeasonRules.IsSowing(world.Clock.Season)
                ? "Spring: the year's one commitment. A field not sown now is a year missed."
                : SeasonRules.IsReaping(world.Clock.Season)
                    ? "Autumn: what is not reaped before winter rots where it stands."
                    : world.Clock.Season == Season.Summer
                        ? "Summer: the crop is growing. Its hands are held for the harvest."
                        : "Winter: stubble. Its farmers are spare hands until spring.");
        }

        // The buffer at the point of production (D30). Worth showing because it is how
        // you tell "idle for want of a worker" from "idle for want of logs" (D29).
        //
        // ⭐ AND IT HAD NEVER ONCE RENDERED UNTIL THE FARM. `Workplace.Store` has been on the
        // type since D30 with nothing in the sim writing to it, so this branch could not be
        // true — `professions.md §4`'s fifth element, *"exists and is dead"*. The farm is where
        // it wakes up, which is also why the farm is the one that says its capacity: the buffer
        // filling up is exactly what makes the farmer's walk get longer.
        if (workplace.Kind == JobKind.Farmer)
        {
            lines.Add(workplace.Store.Held > 0
                ? $"Holding: {DescribeGoods(world, workplace.Store)} — {workplace.Store.Held.Grouped()} of "
                    + $"{workplace.Store.Capacity.Grouped()}. Past that, the harvest goes to a store."
                : $"Holding: nothing. It keeps up to {workplace.Store.Capacity.Grouped()} of its own "
                    + "harvest before the walk gets longer.");
        }
        else if (workplace.Store.Held > 0)
        {
            lines.Add($"Holding: {DescribeGoods(world, workplace.Store)}");
        }
        else if (workplace.Kind == JobKind.Woodcutter)
        {
            lines.Add("Holding: nothing — no logs here to split.");
        }
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

    private static void DescribeStore(SimWorld world, StoreBuilding store, List<string> lines)
    {
        Separate(lines);

        lines.Add($"{store.Name} — a {Describe(world, store.Kind)}");
        lines.Add($"Holding: {DescribeGoods(world, store.Store)}");

        // Capacity is derived rather than typed in (D33), and it is the number that
        // decides how big the village gets — so it belongs on screen, not just in a
        // spec.
        lines.Add(store.Store.IsFull
            ? $"Full: {store.Store.Held.Grouped()} of {store.Store.Capacity.Grouped()} — nothing more will fit."
            : $"Space: {store.Store.Held.Grouped()} of {store.Store.Capacity.Grouped()} used, " +
              $"{store.Store.FreeSpace.Grouped()} free");
    }

    private static void DescribeHome(SimWorld world, Household household, List<string> lines)
    {
        Separate(lines);

        int living = world.LivingMembersOf(household);
        lines.Add($"The {household.Name} household — a home");

        if (living == 0)
        {
            // An empty house is not a ruin: the next couple to pair up moves in rather
            // than felling thirty logs beside it. Worth saying, because otherwise it
            // reads as a bug.
            lines.Add("Nobody lives here now. The next couple to pair up will move in.");
        }
        else
        {
            lines.Add($"Home to {HouseholdNames(world, household)}");
        }

        lines.Add($"Larder: {DescribeGoods(world, household.Stockpile)}");
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

    private static string WorkerNames(SimWorld world, Workplace workplace)
    {
        var names = new List<string>();
        for (int i = 0; i < workplace.WorkerIds.Count; i++)
        {
            Villager? worker = world.FindVillager(workplace.WorkerIds[i]);
            if (worker is not null)
            {
                names.Add(worker.Name);
            }
        }

        return names.Count == 0 ? "nobody" : string.Join(", ", names);
    }

    private static string HouseholdNames(SimWorld world, Household household)
    {
        var names = new List<string>();
        for (int i = 0; i < world.Villagers.Count; i++)
        {
            Villager villager = world.Villagers[i];
            if (villager.Alive && villager.HouseholdId == household.Id)
            {
                names.Add($"{villager.Name} ({villager.AgeYears})");
            }
        }

        return names.Count == 0 ? "nobody" : string.Join(", ", names);
    }

    /// <summary>Only the goods actually present, so an empty shelf is not three zeroes.</summary>
    /// <remarks>
    /// ⛔ <b>BOUNDED BY THE STOCKPILE'S OWN SLOTS, NOT BY THE ENUM.</b> This read
    /// `Stockpile.Kinds`, which is `Enum.GetValues&lt;Goods&gt;().Length` and can only ever return six —
    /// against `Stockpile`'s own warning that *"iterating 0..Kinds over a village that has more
    /// goods than the enum silently ignores every good above the sixth."* A panel whose whole job
    /// is to say what is here would have quietly stopped saying it.
    /// </remarks>
    private static string DescribeGoods(SimWorld world, Stockpile store)
    {
        var parts = new List<string>();

        for (int i = 0; i < store.Slots; i++)
        {
            var goods = (Goods)i;
            if (store[goods] > 0)
            {
                parts.Add($"{store[goods].Grouped()} {world.GoodsCatalog.NameOf(goods)}");
            }
        }

        return parts.Count == 0 ? "nothing" : string.Join(", ", parts);
    }

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

    /// <summary>What a store is, and what it will actually take — asked, not remembered.</summary>
    /// <remarks>
    /// <para>
    /// <b>⛔ ALL THREE OF THESE SENTENCES WERE WRONG, AND ONE OF THEM CONTRADICTED THE OPENING
    /// MECHANIC</b> (found 2026-08-28). The warehouse said *"holds logs and firewood"* and holds
    /// **stone, tools and iron** as well; the pile said *"holds anything"* and **refuses food**;
    /// the cart said *"holds anything"* and **refuses logs** since D90.
    /// </para>
    /// <para>
    /// ⭐⭐ <b>The cart and pile refusals are not a detail — `StoreBuilding` calls them "the two
    /// refusals that make the opening a sequence rather than a pile of options".</b> So the
    /// inspector was telling the player the exact opposite of the rule the first ten minutes of
    /// the game are built on.
    /// </para>
    /// <para>
    /// <b>⭐ It asks the catalogue now, so it cannot drift again.</b> `Bclone.Sim` went
    /// catalogue-driven in D210 precisely so adding a good would not mean editing a method — the
    /// view did not follow, and this is what that cost. A modded seventh good appears here for
    /// free.
    /// </para>
    /// </remarks>
    private static string Describe(SimWorld world, StoreKind kind)
    {
        string what = kind switch
        {
            StoreKind.Granary => "granary",
            StoreKind.Warehouse => "warehouse",
            StoreKind.Market => "market",
            StoreKind.Pile => "stockpile — cleared ground",
            StoreKind.Cart => "cart the founders arrived in",
            _ => kind.ToString().ToLowerInvariant(),
        };

        var takes = new List<string>();
        for (int g = 0; g < world.GoodsCatalog.Count; g++)
        {
            var goods = (Goods)g;
            if (world.GoodsCatalog.StoredBy(goods, kind))
            {
                takes.Add(world.GoodsCatalog.NameOf(goods).ToLowerInvariant());
            }
        }

        if (takes.Count == 0)
        {
            return $"{what}, which holds nothing";
        }

        // ⭐ "everything" only when it is true of the whole catalogue, so the day a good is added
        // that the pile refuses, this stops claiming otherwise on its own.
        string held = takes.Count == world.GoodsCatalog.Count
            ? "everything"
            : takes.Count == 1
                ? takes[0]
                : $"{string.Join(", ", takes.GetRange(0, takes.Count - 1))} and {takes[^1]}";

        return $"{what}, which holds {held}";
    }

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
        _map.VillagerClicked += OnVillagerClicked;
        AddChild(_map);

        // ⭐ TWO COLUMNS, AND PANELS LIVE IN THEM RATHER THAN BESIDE THEM. Joe: *"when the
        // 'what the village is told' window is open, you can see 'the village' window
        // underneath."* Every panel used to anchor itself to a corner at an offset somebody
        // had to work out, so two panels growing toward each other overlapped — and once they
        // were see-through, what showed through was another panel rather than the valley.
        //
        // **A column makes overlap impossible by construction** instead of by choosing sizes
        // carefully, which is the only kind of fix that survives adding a seventh panel.

        BuildStatusPanel();
        BuildProfessionsPanel();
        BuildStockLimitsPanel();
        BuildRosterPanel();

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
    /// What the village is: the date, what it holds, and anything it is asking for.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Joe's area 1, rebuilt from his Banished notes.</b> It used to be two run-on
    /// sentences — the date, then every total the village had, comma-separated. Reading
    /// <em>how much firewood is there?</em> off that meant reading the whole line, which is
    /// how a panel ends up "HUGE" (D113) without holding much.
    /// </para>
    /// <para>
    /// <b>⭐ The goods are driven off the <see cref="Goods"/> enum, not listed by hand</b>,
    /// which is the point of the slice rather than tidiness: a good appears here the day it
    /// is added to the sim, not the day somebody remembers this method. Stone, tools and iron
    /// have been in the enum since D82 and were in no panel until now, which is exactly the
    /// failure being designed out.
    /// </para>
    /// </remarks>
    private void BuildStatusPanel()
    {
        // Titled, so it can be folded and switched off like everything else. It is Joe's
        // area 1 and the panel he calls the Overview, so it is called that.
        VBoxContainer body = InColumn(right: false, 0, "Overview");

        // ⭐ THE VALLEY HAS A NAME NOW, and it is the heading rather than a line in the
        // middle: this is the one word that says which run you are watching. Derived from
        // the seed and not drawn from it — see `SimWorld.Name` for why that distinction is
        // load-bearing rather than pedantic.
        body.AddChild(Heading(_loop.World.Name));

        _clockLabel = Body(string.Empty);
        body.AddChild(_clockLabel);

        _villageLabel = Wrapped(Body(string.Empty));
        body.AddChild(_villageLabel);

        body.AddChild(BuildGoodsTable());
        body.AddChild(BuildGoodsRoadmap());

        // ⭐ THE STANDING ALERTS HAVE LEFT THIS PANEL AND GONE TO THE LOG (Joe, 2026-08-10,
        // pointing at them in a screenshot: *"I don't want to see the part in the UI I've
        // outlined… those should be in the village log window."*)
        //
        // **This reverses D42/D47's reasoning, and it is worth saying which part.** They were
        // put here because both are STATES rather than events — *a couple is waiting right
        // now, a workplace is empty right now* — on the argument that "a line that scrolls
        // away is a problem the player never learns they have". That argument was made when
        // the log was the only alternative and the overview was three lines long.
        //
        // What changed is the panel around them: the overview is now a dozen rows the player
        // reads at a glance, and two wrapped amber paragraphs in the middle of it were the
        // tallest and loudest thing on screen — permanently, because a state that is true
        // stays true. **An alert that is always on is an alert nobody reads**, which is the
        // nag D42 refuses in its own words.
        //
        // The state is not lost: both now narrate on the EDGE, when they begin and when they
        // clear, so the log answers *"is that still going on?"* without a panel sitting there
        // saying so. See `HouseholdSystem` for the first and `LabourSystem` for the second.

        // The seed and the audit log together, because they are the two things you need
        // to reproduce and explain a run: the seed says which world, the log says what
        // happened in it.
        //
        // ⭐ AND THE BUILD, which is the third (METHODOLOGY §5). `VERSION` has been the
        // "single source of version truth" since Phase 0 with **nothing reading it**; it
        // reaches every assembly now, and putting it here is what makes that checkable
        // rather than merely true — a bug report quoting a seed and a log is worth much
        // less if nobody can say which build produced them.
        _seedLabel = Wrapped(Muted(
            $"bclone {BuildVersion}   ·   seed {_loop.World.Seed}   ·   "
            + $"config: {_configSource}   ·   log: {_logPath}"));
        body.AddChild(_seedLabel);
    }

    /// <summary>
    /// What the village holds, one line per good — and one greyed line per good it has not
    /// invented yet.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>⚠️ A greyed row says WHY it is empty rather than showing a zero</b>, which is D98's
    /// rule — <em>a number that is always zero is a lie waiting to be found</em> — applied to
    /// a panel instead of a config key. "Coal 0" tells the player their village has run out
    /// of something; "Coal — no mine to dig it" tells them the truth, which is that the game
    /// has not got there yet. Joe asked for the overview to double as a roadmap, and that
    /// only works if the roadmap is honest about which half is which.
    /// </para>
    /// <para>
    /// <b>The greyed list is hand-written and is meant to be deleted, a row at a time.</b>
    /// It cannot be driven off anything, because the whole point of a row here is that the
    /// thing it names does not exist — there is no enum value to read. Each row's reason
    /// therefore names what would have to be built, so the row deletes itself the day that
    /// lands rather than sitting here going quietly stale.
    /// </para>
    /// <para>
    /// Coloured chips rather than icons: the project ships no image assets (D26), and an
    /// emoji glyph is at the mercy of whatever the default font happens to cover. A
    /// <c>ColorRect</c> draws the same on every machine.
    /// </para>
    /// </remarks>
    private GridContainer BuildGoodsTable()
    {
        var table = new GridContainer { Columns = 3 };
        table.AddThemeConstantOverride("h_separation", 10);
        table.AddThemeConstantOverride("v_separation", 2);

        // ⛔ THE CATALOGUE, NOT THE ENUM. `Stockpile` warns against exactly this loop bound:
        // *"iterating 0..Kinds over a village that has more goods than the enum silently
        // ignores every good above the sixth"* — so a mod-added good had a working slot in the
        // sim, a stock limit, a place in the hash, and no row on screen.
        // ⭐⭐ FOOD IS AN UMBRELLA (Joe, 2026-09-05), AND THE PANEL USED TO CONTRADICT THE ONE
        // BELOW IT. This row read `InStores(Goods.Produce)` — one good — so it said **Food 0** while
        // the stock-limits table said **have 3,043** from `FoodTheVillageHolds()`. Two panels, one
        // screen, two different answers to the same question, and this was the wrong one.
        //
        // ⚠️ The total comes from the sim rather than being re-added here: `FoodTheVillageHolds`
        // is what the birth gate, the food limit and the labour quota all read, so the number on
        // screen is now the number the village actually decides on.
        table.AddChild(Chip(ChipColour(Goods.Produce)));
        table.AddChild(Body("Food"));

        _foodTotal = Body(string.Empty);
        _foodTotal.HorizontalAlignment = HorizontalAlignment.Right;
        _foodTotal.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        table.AddChild(_foodTotal);

        // ⛔ THE FOODS COME FIRST, TOGETHER, AND THAT IS NOT COSMETIC. The first cut indented
        // every edible where it already stood in catalogue order — which put Fish (6) and Meat
        // (7) below Iron (5), so the panel read as though **iron had two kinds of food indented
        // under it**. Joe, immediately: *"why are fish and meat indented under iron?"*
        //
        // ⚠️ An indent is a claim about WHAT OWNS WHAT, so the rows have to be arranged to match
        // it. Catalogue order still decides the order WITHIN each group, so a modder's good lands
        // somewhere predictable rather than wherever the loop happened to reach it.
        for (int pass = 0; pass < 2; pass++)
        {
            bool foods = pass == 0;

            for (int id = 0; id < _loop.World.GoodsCatalog.Count; id++)
            {
                var goods = (Goods)id;
                if (_loop.World.GoodsCatalog.Edible(goods) != foods)
                {
                    continue;
                }

                table.AddChild(Chip(ChipColour(goods)));

                // ⭐ Every food sits UNDER the total, which is what makes the sum read as a sum.
                // Once Joe's subtypes land — venison, trout, wheat — they fall in here for free.
                table.AddChild(Body(foods
                    ? $"    {GoodsName(_loop.World, goods)}"
                    : GoodsName(_loop.World, goods)));

                Label held = Body(string.Empty);
                held.HorizontalAlignment = HorizontalAlignment.Right;
                held.SizeFlagsHorizontal = SizeFlags.ExpandFill;
                table.AddChild(held);

                _goodsReadouts.Add((goods, held));
            }
        }

        return table;
    }

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
        VBoxContainer body = InColumn(right: true, 0, "Who they are, and why");

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
        _inspector = new RichTextLabel
        {
            BbcodeEnabled = false,
            ScrollActive = true,

            // Smaller type, so the minimum comes down with it — 170 was eight lines at 16
            // and is eleven at 13, which is more of a description in less of the screen.
            CustomMinimumSize = new Vector2(0, 140),
            SizeFlagsVertical = SizeFlags.ExpandFill,
        };

        _inspector.AddThemeFontSizeOverride("normal_font_size", RowSize);
        body.AddChild(_inspector);

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
        _pinLabel = Muted("Kept on:");
        (_pinRow, HFlowContainer pinControls) = InspectorRow(body, _pinLabel);

        foreach (JobKind trade in System.Enum.GetValues<JobKind>())
        {
            JobKind captured = trade;
            var button = new Button { ToggleMode = true };
            button.Pressed += () => TogglePin(captured);
            _pinButtons.Add((captured, button));
            pinControls.AddChild(button);
        }

        _staffingLabel = Muted("Staffing:");
        (_staffingRow, HFlowContainer staffingControls) = InspectorRow(body, _staffingLabel);

        var fewer = new Button { Text = "−1" };
        fewer.Pressed += () => ChangeStaffing(-1);
        staffingControls.AddChild(fewer);

        var more = new Button { Text = "+1" };
        more.Pressed += () => ChangeStaffing(+1);
        staffingControls.AddChild(more);

        // ⛔⛔ "VILLAGE DECIDES" IS GONE FROM THE WHOLE GAME (Joe, 2026-08-16): *"i want
        // village decides gone entirely from all aspects of the game for now."* D136 took the
        // phrase off the stock-limit rows and off the professions panel; this row was the last
        // place still offering it, first as a mode and then (briefly, D163) as a "Clear" button
        // that put a building back to it. Both are gone.
        //
        // ⭐ AND THE DEFAULT WAS NEVER REALLY "THE VILLAGE DECIDES" — the label was lying.
        // `Places => StaffingOverride ?? Capacity`, so an untouched building has always been
        // staffed by **everyone who fits**. That is a fact about the building, not a decision
        // anybody made, and saying so is what actually removes the idea rather than hiding it.
        // There is no longer any way to reach the untouched state once you leave it, which is
        // the same bargain the professions panel struck: the player always has an opinion.

        // ⭐ AND THE BUILD QUEUE, WHICH IS JOE'S OWN ANSWER TO HIS VILLAGE FREEZING:
        // "I think this is solved by letting the user increase/decrease the priority level of
        // a building under construction." It is — and it is better than any rule about which
        // KIND of building matters most, because the village cannot know whether this winter
        // needs a granary or a roof and the player can.
        _queueLabel = Muted("Build queue:");
        (_queueRow, HFlowContainer queueControls) = InspectorRow(body, _queueLabel);

        var sooner = new Button { Text = "▲ Sooner" };
        sooner.Pressed += () => MoveSelectedInQueue(-1);
        queueControls.AddChild(sooner);

        var later = new Button { Text = "▼ Later" };
        later.Pressed += () => MoveSelectedInQueue(+1);
        queueControls.AddChild(later);

        // ⭐ THE GROUND A BUILDING KEEPS (D86), reaching the player at last. The sim side has
        // been built and unused since C3c — painted per workplace, priced in workers, with the
        // overstretched warning already written — because there was no building that owned
        // ground until the forester's hut. It sits in the panel rather than on the toolbar for
        // the reason D104 settled: a brush that belongs to ONE building needs to be beside the
        // name of that building, or the player has to remember which one it will paint for.
        _groundLabel = Muted("Ground:");
        (_groundRow, HFlowContainer groundControls) = InspectorRow(body, _groundLabel);

        var give = new Button { Text = "Give ground" };
        give.Pressed += () => PaintGroundForSelection(1);
        groundControls.AddChild(give);

        var takeBack = new Button { Text = "Take back" };
        takeBack.Pressed += () => PaintGroundForSelection(-1);
        groundControls.AddChild(takeBack);

        // ⭐ AND THE MODE — the first control in this game that tells a building to PUT
        // SOMETHING BACK (Joe, ungated). It ships enabled rather than greyed behind managed
        // forestry, which is the change `professions.md §6.2` records.
        _modeButton = new Button { Text = "Planting: off" };
        _modeButton.Pressed += ToggleSelectedMode;
        groundControls.AddChild(_modeButton);

        // ⭐ AND THE OVERSTRETCHED SENTENCE, WHICH IS A STATE AND NOT JUST A MOMENT (D86).
        // The brush says it once per stroke; this says it for as long as it is true, so
        // losing a farmer in summer is readable in autumn.
        _groundNote = Wrapped(Muted(string.Empty));
        _groundNote.Visible = false;
        _groundRow.AddChild(_groundNote);

        // ⭐ WHY THIS BUILDING IS NOT WORKING, AND A SWITCH TO STOP ASKING (Joe, D147). The
        // same shape as the full-store marker below, and D140's per-building/global pair.
        //
        // The label is the whole point rather than decoration: the ring on the map says *look
        // here* and this says *why*, and a hut held by a log limit set on the stock panel is
        // exactly the case where the second half cannot be guessed from the first.
        _idleLabel = Muted(string.Empty);
        (_idleRow, HFlowContainer idleControls) = InspectorRow(body, _idleLabel);

        _idleMarkerButton = new Button { Text = "Marker: ON" };
        _idleMarkerButton.Pressed += ToggleSelectedIdleMarker;
        idleControls.AddChild(_idleMarkerButton);

        // ⭐ THE PER-BUILDING HALF OF THE FULL-STORE MARKER (Joe, D140): *"visibility of which
        // should be able to be disabled by building or globally."* Beside the store's own name
        // for D104's reason — a control that belongs to ONE building has to sit next to that
        // building, or the player has to remember which one it will act on.
        (_storeRow, HFlowContainer storeControls) = InspectorRow(body, Muted("When full:"));

        _fullMarkerButton = new Button { Text = "Marker: ON" };
        _fullMarkerButton.Pressed += ToggleSelectedFullMarker;
        storeControls.AddChild(_fullMarkerButton);

        // ⭐ WHAT THIS BUILDING WILL TAKE (Joe, D141): *"a given storage pile will only accept
        // logs, another only firewood, another only iron ore. Set at the building level."*
        //
        // One button per good, built once and shown or hidden by what the KIND can hold — so a
        // granary offers "food" and nothing else, and the player is never presented with a
        // choice the model would refuse. The refusal still exists in `SetStoreAccepts`, because
        // a control that cannot be misused and a rule that cannot be broken are different
        // things and only the second one survives somebody calling it from elsewhere.
        (_acceptRow, HFlowContainer acceptControls) = InspectorRow(body, Muted("Takes:"));

        for (int g = 0; g < _loop.World.GoodsCatalog.Count; g++)
        {
            var goods = (Goods)g;
            var button = new Button { Text = GoodsName(_loop.World, goods), ToggleMode = true };
            button.Pressed += () => ToggleSelectedAccepts(goods);
            acceptControls.AddChild(button);
            _acceptButtons.Add((goods, button));
        }
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


    private readonly List<(Goods Goods, Button Button)> _acceptButtons = new();

    /// <summary>Turn one kind of goods on or off for the selected store.</summary>
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

        foreach (StoreBuilding store in _loop.World.StoreBuildings)
        {
            if (store.Position == tile)
            {
                return store;
            }
        }

        return null;
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
        // ⛔⛔ A FLOATING WINDOW, NOT A COLUMN PANEL, AND THE TABLE IS WHY. A side column clamps
        // to 240–400 logical px, and a `GridContainer`'s minimum width is the SUM of its column
        // minimums — so a three-column table with a notes column in it re-opens the exact bug
        // recorded at `BuildInspectorPanel` (an idle row wanting 733px against a 267px column) and
        // again at the old stock-limit rows (six of them holding the left column at 450).
        //
        // ⭐ Joe's mockup is a wide overlay anyway, so the constraint and the design agree.
        VBoxContainer body = Floating(
            Edge, Edge, 380f, 0f, Corner.TopLeft, "Professions", startOpen: true);

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
        _professionsPanel.Visible = false;

        // ⭐ What the village HAS, before what it is doing with it. The old panel opened with a
        // "Laborer" row among the trades, which read as an eighth profession rather than as the
        // pool the other seven are drawn from.
        _laborerReadout = Body(string.Empty);
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
            Edge + 396f, Edge, 300f, 0f, Corner.TopLeft, "Stock limits", startOpen: true);

        _docked.Add((_panels[^1], false));
        _stockLimitsPanel = _panels[^1];
        _stockLimitsPanel.Visible = false;

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
    /// ⚠️ <b>The notes column must WRAP rather than widen</b>, or this table walks straight back
    /// into the width bug it was moved out of the column to escape. <see cref="Wrapped"/> is the
    /// tool — autowrap plus a floor — and every long clause the sim can produce goes through it.
    /// </para>
    /// </remarks>
    private GridContainer BuildProfessionsTable()
    {
        var table = new GridContainer { Columns = 4 };
        table.AddThemeConstantOverride("h_separation", 6);
        table.AddThemeConstantOverride("v_separation", 1);

        table.AddChild(new Control());
        table.AddChild(Muted("TYPE"));
        table.AddChild(Muted("ASSIGNED / MAX"));
        table.AddChild(Muted("GOAL / NOTES"));

        foreach (JobKind kind in JobLimits.Kinds)
        {
            AddProfessionRow(table, kind);
        }

        return table;
    }

    /// <summary>Four cells for one trade.</summary>
    private void AddProfessionRow(GridContainer table, JobKind kind)
    {
        table.AddChild(new TradeGlyph(kind));
        table.AddChild(Body(ProfessionName(_loop.World, kind)));

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

        // ---- goal / notes ----
        Label notes = Wrapped(Muted(string.Empty));
        table.AddChild(notes);

        _professionReadouts.Add((kind, seats, notes));
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
        settings.Pressed += () => _settingsPanel.Visible = !_settingsPanel.Visible;
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
            "space to pause · 1-4 speed · WASD pan · wheel zoom · tab routes · g ground · "
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
        float leftY = Edge;
        float rightY = Edge;

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

            panel.OffsetTop = y;
            panel.OffsetBottom = y + panel.Size.Y;

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
            _windows.Add((title, panel));
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
    private readonly List<(string Name, PanelContainer Panel)> _windows = new();

    /// <summary>The settings panel itself, which is the one window not in that list.</summary>
    private PanelContainer _professionsPanel = null!;

    private PanelContainer _stockLimitsPanel = null!;

    private PanelContainer _settingsPanel = null!;

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
        VBoxContainer body = InColumn(right: true, 0, "Settings");
        _settingsPanel = _panels[^1];
        _settingsPanel.Visible = false;

        // Dropped from the list of switchable windows for the reason above, and from the
        // headers list so `c` cannot fold the thing you opened to un-fold something else.
        _windows.RemoveAt(_windows.Count - 1);
        _headers.RemoveAt(_headers.Count - 1);

        // ⭐ THE ONE DIAL JOE ASKED FOR: *"smaller please. give me more room to see the game
        // map."* It reaches every panel and the control bar, so the whole furniture shrinks
        // together rather than four font sizes drifting apart.
        body.AddChild(Muted("How big the furniture is"));

        var reset = new Button { Text = "Reset window positions", Flat = true };
        reset.AddThemeFontSizeOverride("font_size", 12);
        reset.Pressed += ArrangeDefaults;
        body.AddChild(reset);

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

        body.AddChild(Caption("Windows — what is on screen"));

        foreach ((string name, PanelContainer panel) in _windows)
        {
            var shown = new CheckBox { Text = name, ButtonPressed = true };
            shown.AddThemeFontSizeOverride("font_size", 12);
            shown.Toggled += on => panel.Visible = on;
            body.AddChild(shown);
        }

        body.AddChild(Caption("c folds every panel · h hides the lot"));

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
        _detailButton = new Button { CustomMinimumSize = new Vector2(140, 0) };
        _detailButton.AddThemeFontSizeOverride("font_size", 12);
        _detailButton.Pressed += CycleDetail;
        body.AddChild(_detailButton);

        // ⭐ WHERE THE GOOD GROUND IS (D178). Without it, per-site yield is an invisible
        // multiplier and siting a farm is a lottery — which is what D67 refused for ore and
        // what §1.1 refuses in general. Off by default: it answers a question the player asks
        // occasionally, and a permanent wash over the valley is D42's standing alert in
        // another medium.
        _soilButton = new Button { CustomMinimumSize = new Vector2(110, 0) };
        _soilButton.AddThemeFontSizeOverride("font_size", 12);
        _soilButton.Pressed += ToggleSoil;
        body.AddChild(_soilButton);
        RefreshSoilButton();

        var markers = new CheckBox { Text = "mark stores with no room", ButtonPressed = true };
        markers.AddThemeFontSizeOverride("font_size", 12);
        markers.Toggled += on => _map.ShowFullMarkers(on);
        body.AddChild(markers);

        // The global half of D147's idle ring, beside the global half of D140's, because they
        // are the same kind of preference and a player looking for one will look for the other.
        var idle = new CheckBox { Text = "mark buildings that cannot work", ButtonPressed = true };
        idle.AddThemeFontSizeOverride("font_size", 12);
        idle.Toggled += on => _map.ShowIdleMarkers(on);
        body.AddChild(idle);

        // ⭐ The wildlife is scenery rather than a marker, but it belongs with the other two:
        // all three answer *"what is drawn on the valley"*, and a player who wants a plainer
        // map will look for them in one place. ⚠️ It is deliberately NOT on the "Routes:"
        // cycle — that control says routes, and hiding animals under it would surprise.
        var wildlife = new CheckBox { Text = "animals in the woods", ButtonPressed = true };
        wildlife.AddThemeFontSizeOverride("font_size", 12);
        wildlife.Toggled += on => _map.ShowGame(on);
        body.AddChild(wildlife);

        // Its own switch rather than one "scenery" tick, because the two answer different
        // questions: what the woods FEED and what they HOLD. A player hunting for a quieter
        // map may well want one and not the other.
        var forage = new CheckBox { Text = "berry patches in the woods", ButtonPressed = true };
        forage.AddThemeFontSizeOverride("font_size", 12);
        forage.Toggled += on => _map.ShowForage(on);
        body.AddChild(forage);

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
    }

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
    private readonly List<(JobKind Kind, Label Seats, Label Notes)> _professionReadouts = new();
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

            BuildingKind.GathererHut or BuildingKind.Farmhouse
                or BuildingKind.FishingHut or BuildingKind.HunterLodge => BuildCategory.Food,

            BuildingKind.ForesterHut or BuildingKind.WoodcutterHut => BuildCategory.Resources,

            // The pile leads its group because it leads the game (D76): it costs nothing but
            // the ground, and a village with nowhere to put things cannot begin.
            BuildingKind.Pile or BuildingKind.Granary
                or BuildingKind.Warehouse or BuildingKind.Market => BuildCategory.Storage,

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

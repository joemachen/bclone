using System.Collections.Generic;
using Bclone.Sim.Config;
using Bclone.Sim.Core;
using Bclone.Sim.Logging;
using Bclone.Sim.World;
using Godot;

namespace Bclone.Game;

/// <summary>
/// The Phase 0 view: watch one villager live and die, and be able to read why.
/// </summary>
/// <remarks>
/// <para>
/// This class <b>reads</b> sim state and never writes it (DESIGN.md §3). The only
/// thing it does to the simulation is decide how many ticks to run, via
/// <see cref="FixedTimestepDriver"/>.
/// </para>
/// <para>
/// The UI is built in code rather than authored as a scene tree. For a panel of
/// labels that all change every tick, a hand-edited .tscn is mostly a liability —
/// the layout is easier to read as thirty lines of C# than as a diff of node
/// properties, and it cannot drift out of sync with the fields it displays.
/// </para>
/// </remarks>
public partial class Main : Control
{
    private const int MaxLogLines = 400;

    private SimLoop _loop = null!;
    private FixedTimestepDriver _driver = null!;
    private InMemoryLogSink _sink = null!;
    private string _configSource = string.Empty;

    private Label _clockLabel = null!;
    private Label _seedLabel = null!;
    private Label _nameLabel = null!;
    private Label _actionLabel = null!;
    private Label _foodLabel = null!;
    private Label _hungerLabel = null!;
    private ProgressBar _hungerBar = null!;
    private Label _speedLabel = null!;
    private RichTextLabel _lifeLog = null!;
    private Label _epitaph = null!;

    private int _renderedLogEntries;
    private bool _deathAnnounced;

    public override void _Ready()
    {
        SimConfig config = ConfigLocator.LoadOrDefault(out _configSource);

        _sink = new InMemoryLogSink(LogLevel.Info);
        _loop = SimFactory.CreatePhase0(config, _sink);
        _driver = new FixedTimestepDriver(config, _sink);

        BuildUi();
        Refresh();
    }

    public override void _Process(double delta)
    {
        // The single wall-clock read in the entire program. Everything downstream
        // of here counts in ticks.
        int ticks = _driver.Advance(delta, _loop.World.Tick);
        if (ticks > 0)
        {
            _loop.Step(ticks);
        }

        Refresh();
    }

    public override void _UnhandledKeyInput(InputEvent @event)
    {
        if (@event is not InputEventKey { Pressed: true, Echo: false } key)
        {
            return;
        }

        switch (key.Keycode)
        {
            case Key.Space:
                SetSpeed(_driver.IsPaused ? 1.0 : 0.0);
                break;
            case Key.Key1:
                SetSpeed(1.0);
                break;
            case Key.Key2:
                SetSpeed(2.0);
                break;
            case Key.Key3:
                SetSpeed(4.0);
                break;
        }
    }

    /// <summary>
    /// Change playback speed.
    /// </summary>
    /// <remarks>
    /// This scales how many ticks run per real second — never how big a tick is.
    /// A run at 4x has exactly the same history as a run at 1x (decision D4).
    /// </remarks>
    private void SetSpeed(double multiplier)
    {
        _driver.SpeedMultiplier = multiplier;
        UpdateSpeedLabel();
    }

    private void UpdateSpeedLabel() =>
        _speedLabel.Text = _driver.IsPaused ? "PAUSED" : $"{_driver.SpeedMultiplier:0.#}x";

    // ---------------------------------------------------------------
    //  Rendering
    // ---------------------------------------------------------------

    private void Refresh()
    {
        SimWorld world = _loop.World;
        Villager villager = world.Villager;
        SimConfig config = world.Config;

        _clockLabel.Text = $"{world.Clock}   ·   tick {world.Tick}";

        _nameLabel.Text = villager.Alive
            ? $"{villager.Name}, age {villager.AgeYears}"
            : $"{villager.Name}, died at {villager.AgeYears}";

        _actionLabel.Text = villager.DescribeState();

        _foodLabel.Text = $"{world.Stockpile.Food} food stored";

        int hungerPercent = config.HungerMax == 0 ? 0 : villager.Hunger * 100 / config.HungerMax;
        _hungerBar.Value = hungerPercent;
        _hungerLabel.Text = villager.Alive ? $"Hunger {hungerPercent}%" : "—";

        // Colour is a hint, never the only signal — the number is always there too.
        _hungerBar.Modulate = hungerPercent >= 100 ? Colors.OrangeRed
            : hungerPercent >= 80 ? Colors.Goldenrod
            : Colors.ForestGreen;

        AppendNewLogLines();

        if (!villager.Alive && !_deathAnnounced)
        {
            _deathAnnounced = true;
            _epitaph.Visible = true;
            _epitaph.Text = villager.CauseOfDeath == CauseOfDeath.OldAge
                ? $"{villager.Name} lived {villager.AgeYears} years and survived " +
                  $"{villager.WintersSurvived} winters."
                : $"{villager.Name} starved in year {world.Clock.Year}, aged {villager.AgeYears}, " +
                  $"after {villager.WintersSurvived} winters.";
        }
    }

    /// <summary>
    /// Append only entries we have not drawn yet.
    /// </summary>
    /// <remarks>
    /// Rebuilding the whole log every frame would be O(life) per frame and would
    /// reset the player's scroll position, which matters — the log is meant to be
    /// read back through, not just watched.
    /// </remarks>
    private void AppendNewLogLines()
    {
        IReadOnlyList<LogEntry> entries = _sink.Entries;

        for (int i = _renderedLogEntries; i < entries.Count; i++)
        {
            LogEntry entry = entries[i];
            if (entry.Subsystem != "life")
            {
                continue;
            }

            _lifeLog.AppendText($"{entry.Message}\n");
        }

        _renderedLogEntries = entries.Count;

        // Keep the buffer bounded; a full life is several hundred lines.
        if (_lifeLog.GetLineCount() > MaxLogLines * 2)
        {
            _lifeLog.Clear();
            _lifeLog.AppendText("(earlier entries trimmed)\n");
            for (int i = entries.Count - MaxLogLines; i < entries.Count; i++)
            {
                if (i >= 0 && entries[i].Subsystem == "life")
                {
                    _lifeLog.AppendText($"{entries[i].Message}\n");
                }
            }
        }
    }

    // ---------------------------------------------------------------
    //  Layout
    // ---------------------------------------------------------------

    private void BuildUi()
    {
        var root = new MarginContainer { AnchorRight = 1, AnchorBottom = 1 };
        root.AddThemeConstantOverride("margin_left", 24);
        root.AddThemeConstantOverride("margin_top", 20);
        root.AddThemeConstantOverride("margin_right", 24);
        root.AddThemeConstantOverride("margin_bottom", 20);
        AddChild(root);

        var column = new VBoxContainer();
        column.AddThemeConstantOverride("separation", 14);
        root.AddChild(column);

        // ---- Header: where we are in time -------------------------
        _clockLabel = Heading("");
        column.AddChild(_clockLabel);

        _seedLabel = Muted($"seed {_loop.World.Seed}   ·   config: {_configSource}");
        column.AddChild(_seedLabel);

        column.AddChild(new HSeparator());

        // ---- The villager ----------------------------------------
        _nameLabel = Heading("");
        column.AddChild(_nameLabel);

        _actionLabel = Body("");
        column.AddChild(_actionLabel);

        var hungerRow = new HBoxContainer();
        hungerRow.AddThemeConstantOverride("separation", 12);
        column.AddChild(hungerRow);

        _hungerLabel = Body("");
        _hungerLabel.CustomMinimumSize = new Vector2(120, 0);
        hungerRow.AddChild(_hungerLabel);

        _hungerBar = new ProgressBar
        {
            MinValue = 0,
            MaxValue = 100,
            ShowPercentage = false,
            CustomMinimumSize = new Vector2(260, 18),
        };
        hungerRow.AddChild(_hungerBar);

        _foodLabel = Body("");
        column.AddChild(_foodLabel);

        _epitaph = Heading("");
        _epitaph.Visible = false;
        _epitaph.Modulate = Colors.Gold;
        column.AddChild(_epitaph);

        column.AddChild(new HSeparator());

        // ---- Life log: the actual deliverable ---------------------
        column.AddChild(Muted("Life log"));

        _lifeLog = new RichTextLabel
        {
            ScrollFollowing = true,
            BbcodeEnabled = false,
            SizeFlagsVertical = SizeFlags.ExpandFill,
            CustomMinimumSize = new Vector2(0, 360),
        };
        column.AddChild(_lifeLog);

        // ---- Playback controls ------------------------------------
        var controls = new HBoxContainer();
        controls.AddThemeConstantOverride("separation", 10);
        column.AddChild(controls);

        controls.AddChild(SpeedButton("Pause", 0.0));
        controls.AddChild(SpeedButton("1x", 1.0));
        controls.AddChild(SpeedButton("2x", 2.0));
        controls.AddChild(SpeedButton("4x", 4.0));

        _speedLabel = Body(string.Empty);
        controls.AddChild(_speedLabel);

        controls.AddChild(Muted("   (space to pause · 1 / 2 / 3 for speed)"));

        UpdateSpeedLabel();
    }

    private Button SpeedButton(string text, double multiplier)
    {
        var button = new Button { Text = text, CustomMinimumSize = new Vector2(64, 0) };
        button.Pressed += () => SetSpeed(multiplier);
        return button;
    }

    private static Label Heading(string text)
    {
        var label = new Label { Text = text };
        label.AddThemeFontSizeOverride("font_size", 22);
        return label;
    }

    private static Label Body(string text)
    {
        var label = new Label { Text = text };
        label.AddThemeFontSizeOverride("font_size", 16);
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

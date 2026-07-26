using System.Collections.Generic;
using Bclone.Sim.Config;
using Bclone.Sim.Core;
using Bclone.Sim.Logging;
using Bclone.Sim.Systems;
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

    private SimLoop _loop = null!;
    private FixedTimestepDriver _driver = null!;
    private InMemoryLogSink _sink = null!;
    private string _configSource = string.Empty;

    private Label _clockLabel = null!;
    private Label _villageLabel = null!;
    private Label _seedLabel = null!;
    private Label _speedLabel = null!;
    private ItemList _roster = null!;
    private RichTextLabel _inspector = null!;
    private RichTextLabel _villageLog = null!;
    private VillageMap _map = null!;

    private int _renderedLogEntries;
    private int _selectedVillagerId;

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
        // The single wall-clock read in the entire program.
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
            case Key.Space: SetSpeed(_driver.IsPaused ? 1.0 : 0.0); break;
            case Key.Key1: SetSpeed(1.0); break;
            case Key.Key2: SetSpeed(2.0); break;
            case Key.Key3: SetSpeed(4.0); break;
            case Key.Key4: SetSpeed(10.0); break;
        }
    }

    /// <summary>
    /// Change playback speed — ticks per real second, never the size of a tick
    /// (decision D4).
    /// </summary>
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

        _clockLabel.Text = $"{world.Clock}   ·   tick {world.Tick}";
        _villageLabel.Text =
            $"{world.Population} villagers · {LivingHouseholds(world)} households · " +
            $"{TotalFood(world)} food stored";

        RefreshRoster(world);
        RefreshInspector(world);
        AppendNewLogLines();

        // Alpha is the fraction of a tick elapsed, so villagers glide between tiles
        // instead of teleporting once a second.
        _map.Present(world, _driver.Alpha, _selectedVillagerId);
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

            string work = villager.HasJob ? "working" : "no work";
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
        Villager? villager = world.FindVillager(_selectedVillagerId);
        if (villager is null)
        {
            _inspector.Text = "Select a villager to see what they are doing, and why.";
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
            villager.Alive ? $"Currently: {villager.DescribeState()}" : "Dead.",
            $"Household: the {household.Name} household ({household.Stockpile.Food} food stored)",
            $"Hunger: {hungerPercent}%",
        };

        if (villager.Stage != VigourStage.Prime)
        {
            lines.Add(villager.Stage == VigourStage.Frail
                ? $"Vigour: {villager.Vigour}% — frail; every trip brings back less"
                : $"Vigour: {villager.Vigour}% — past their strongest years");
        }

        if (villager.IsPaired)
        {
            Villager? partner = world.FindVillager(villager.PartnerId);
            if (partner is not null)
            {
                lines.Add($"Partner: {partner.Name}");
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
        _selectedVillagerId = metadata.AsInt32();
        RefreshInspector(_loop.World);
    }

    /// <summary>Append only entries not yet drawn; rebuilding would reset scroll.</summary>
    private void AppendNewLogLines()
    {
        IReadOnlyList<LogEntry> entries = _sink.Entries;

        for (int i = _renderedLogEntries; i < entries.Count; i++)
        {
            if (entries[i].Subsystem == "life")
            {
                _villageLog.AppendText($"{entries[i].Message}\n");
            }
        }

        _renderedLogEntries = entries.Count;

        if (_villageLog.GetLineCount() > MaxLogLines * 2)
        {
            _villageLog.Clear();
            _villageLog.AppendText("(earlier entries trimmed)\n");
            for (int i = entries.Count - MaxLogLines; i < entries.Count; i++)
            {
                if (i >= 0 && entries[i].Subsystem == "life")
                {
                    _villageLog.AppendText($"{entries[i].Message}\n");
                }
            }
        }
    }

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

    private static int TotalFood(SimWorld world)
    {
        int total = 0;
        for (int i = 0; i < world.Households.Count; i++)
        {
            total += world.Households[i].Stockpile.Food;
        }

        return total;
    }

    // ---------------------------------------------------------------
    //  Layout
    // ---------------------------------------------------------------

    private void BuildUi()
    {
        var root = new MarginContainer();
        foreach (string side in new[] { "margin_left", "margin_top", "margin_right", "margin_bottom" })
        {
            root.AddThemeConstantOverride(side, 20);
        }

        AddChild(root);

        // Anchors have to be APPLIED, not just assigned. Setting AnchorRight/Bottom
        // alone left this container sized to its own content, so it grew past the
        // bottom of the window and took the time controls with it - twice.
        root.SetAnchorsPreset(LayoutPreset.FullRect);

        var column = new VBoxContainer();
        column.AddThemeConstantOverride("separation", 10);
        root.AddChild(column);

        _clockLabel = Heading(string.Empty);
        column.AddChild(_clockLabel);

        _villageLabel = Body(string.Empty);
        column.AddChild(_villageLabel);

        _seedLabel = Muted($"seed {_loop.World.Seed}   ·   config: {_configSource}");
        column.AddChild(_seedLabel);

        // Time controls sit at the TOP, under the header. Belt and braces after two
        // failed attempts to pin them at the bottom: nothing below them in the
        // layout can push them off screen if there is nothing below them.
        var controls = new HBoxContainer { CustomMinimumSize = new Vector2(0, 34) };
        controls.AddThemeConstantOverride("separation", 10);
        column.AddChild(controls);

        column.AddChild(new HSeparator());

        // The village itself, drawn. Everything else is here to explain it.
        _map = new VillageMap
        {
            SizeFlagsVertical = SizeFlags.ExpandFill,
            SizeFlagsStretchRatio = 1.4f,
            CustomMinimumSize = new Vector2(0, 200),
        };
        column.AddChild(_map);

        // Roster on the left, the person you clicked on the right.
        var middle = new HBoxContainer { SizeFlagsVertical = SizeFlags.ExpandFill };
        middle.AddThemeConstantOverride("separation", 18);
        column.AddChild(middle);

        var rosterColumn = new VBoxContainer { CustomMinimumSize = new Vector2(320, 0) };
        rosterColumn.AddChild(Muted("The village"));
        middle.AddChild(rosterColumn);

        _roster = new ItemList { SizeFlagsVertical = SizeFlags.ExpandFill };
        _roster.ItemSelected += OnVillagerSelected;
        rosterColumn.AddChild(_roster);

        var detailColumn = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        detailColumn.AddChild(Muted("Who they are, and why"));
        middle.AddChild(detailColumn);

        // ScrollActive so a long reason scrolls rather than being cut off by the log
        // beneath it. The one panel whose job is explaining a decision must never
        // truncate the explanation.
        _inspector = new RichTextLabel
        {
            BbcodeEnabled = false,
            ScrollActive = true,
            SizeFlagsVertical = SizeFlags.ExpandFill,
            CustomMinimumSize = new Vector2(0, 150),
        };
        detailColumn.AddChild(_inspector);

        detailColumn.AddChild(Muted("Village log"));
        _villageLog = new RichTextLabel
        {
            ScrollFollowing = true,
            BbcodeEnabled = false,
            SizeFlagsVertical = SizeFlags.ExpandFill,
            CustomMinimumSize = new Vector2(0, 120),
        };
        detailColumn.AddChild(_villageLog);

        controls.AddChild(SpeedButton("Pause", 0.0));
        controls.AddChild(SpeedButton("1x", 1.0));
        controls.AddChild(SpeedButton("2x", 2.0));
        controls.AddChild(SpeedButton("4x", 4.0));
        controls.AddChild(SpeedButton("10x", 10.0));

        _speedLabel = Body(string.Empty);
        controls.AddChild(_speedLabel);
        controls.AddChild(Muted("   (space to pause · 1 / 2 / 3 / 4 for speed)"));

        SetSpeed(1.0);
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

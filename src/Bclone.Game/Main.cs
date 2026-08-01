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

    /// <summary>
    /// Lines the standing-alert strip always occupies, said or unsaid.
    /// </summary>
    /// <remarks>
    /// One per standing alert there can be — nowhere to build, and work nobody is doing.
    /// The strip is this tall whether or not either is true, because the alternative is
    /// a header that changes height and a map that moves under the player to match.
    /// </remarks>
    private const int StandingAlertLines = 2;

    /// <summary>
    /// How many idle workplaces to name before falling back to a count.
    /// </summary>
    /// <remarks>
    /// The alert has one line and the line has to fit. Three names is about what fits on
    /// a normal window, and the fourth was never actionable anyway — a player who is
    /// told the village is short of hands does not need the full inventory to act.
    /// </remarks>
    private const int MostPlacesToName = 3;

    private SimLoop _loop = null!;
    private FixedTimestepDriver _driver = null!;
    private InMemoryLogSink _sink = null!;

    /// <summary>The full audit trail on disk — everything down to DEBUG.</summary>
    private FileLogSink _audit = null!;
    private string _logPath = string.Empty;

    private string _configSource = string.Empty;

    private Label _clockLabel = null!;
    private Label _villageLabel = null!;
    private Label _alertLabel = null!;
    private Label _seedLabel = null!;
    private Label _speedLabel = null!;
    private ItemList _roster = null!;
    private RichTextLabel _inspector = null!;
    private RichTextLabel _villageLog = null!;
    private VillageMap _map = null!;

    private Button _detailButton = null!;

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

        switch (key.Keycode)
        {
            case Key.Space: SetSpeed(_driver.IsPaused ? 1.0 : 0.0); break;
            case Key.Key1: SetSpeed(1.0); break;
            case Key.Key2: SetSpeed(2.0); break;
            case Key.Key3: SetSpeed(4.0); break;
            case Key.Key4: SetSpeed(10.0); break;
            case Key.Tab: CycleDetail(); break;
            case Key.Home: _map.CentreOnTheVillage(); break;
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
        // Totals across every granary and shed, not the first of each (D38) — a village
        // that has built a second one should see what is in it.
        _villageLabel.Text =
            $"{world.Population} villagers · {LivingHouseholds(world)} households · " +
            $"{TotalFood(world)} food · {world.FoodInGranaries()} in the granaries · " +
            $"{world.LogsInSheds()} logs and {world.FirewoodInSheds()} firewood in the sheds";

        // The standing alerts, into their own strip rather than onto the end of the
        // header. Both are STATES rather than events — a couple is waiting right now, a
        // workplace is empty right now — which is why they are here at all instead of
        // only in the log: a line that scrolls away is a problem the player never learns
        // they have. Both clear themselves the moment the village sorts them out.
        var alerts = new List<string>();

        // Somewhere to build (D42).
        if (world.NeedsMoreResidentialLand)
        {
            alerts.Add(
                "Somebody wants a home of their own and there is nowhere to put one — " +
                "paint more land for houses.");
        }

        // Work the village wants doing that nobody is doing (D47).
        IReadOnlyList<Workplace> unmanned = LabourSystem.UnmannedWork(world);
        if (unmanned.Count > 0)
        {
            alerts.Add(
                $"Nobody is working {NameThem(unmanned)} — and the village wants it done. " +
                "There is no one spare to send.");
        }

        // Padded to the reserved height so the strip is the same size empty as full.
        // Setting the text to "" would let the label shrink to nothing and put the
        // reflow straight back — CustomMinimumSize holds the box, but a label with no
        // lines in it still reports a smaller natural size on some themes, and this
        // costs one newline to be certain of.
        while (alerts.Count < StandingAlertLines)
        {
            alerts.Add(string.Empty);
        }

        _alertLabel.Text = string.Join("\n", alerts);

        RefreshRoster(world);
        RefreshInspector(world);
        AppendNewLogLines();

        // Alpha is the fraction of a tick elapsed, so villagers glide between tiles
        // instead of teleporting once a second.
        _map.Present(world, _driver.Alpha, _selectedVillagerId, _selectedTile, _detail);
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
            $"Household: the {household.Name} household ({household.Stockpile.Food} food, " +
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

        foreach (Workplace workplace in _loop.World.Workplaces)
        {
            if (workplace.Position == tile && workplace.Construction is null)
            {
                return workplace;
            }
        }

        return null;
    }

    /// <summary>Nudge the selected workplace's staffing, or hand it back to the village.</summary>
    /// <remarks>
    /// Buttons rather than a spinner, because the numbers are small and a click is
    /// cheaper to reach for than a text field. "Let the village decide" is a first-class
    /// control and not a magic value like -1: reverting is a thing players do often, and
    /// it has to be as easy as setting.
    /// </remarks>
    private void ChangeStaffing(int delta)
    {
        Workplace? workplace = SelectedWorkplace();
        if (workplace is null)
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

    private void LetTheVillageDecideStaffing()
    {
        Workplace? workplace = SelectedWorkplace();
        if (workplace is not null)
        {
            _loop.World.SetStaffing(workplace, null);
            RefreshInspector(_loop.World);
        }
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
                DescribeStore(store, lines);
            }
        }

        foreach (Household household in world.Households)
        {
            if (household.HomePosition == tile)
            {
                DescribeHome(world, household, lines);
            }
        }

        if (lines.Count == 0)
        {
            DescribeBareGround(world, tile, lines);
        }

        lines.Add(string.Empty);
        lines.Add($"Tile {tile.X}, {tile.Y}");
        return string.Join("\n", lines);
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
            lines.Add(site.HasMaterials
                ? $"Materials: all {site.Recipe.Logs} logs delivered"
                : $"Materials: {site.LogsDelivered} of {site.Recipe.Logs} logs — " +
                  $"{site.LogsStillNeeded} still to come");
            lines.Add($"Work: {site.WorkDone} of {site.Recipe.WorkTicks} ticks done");
        }
        else
        {
            lines.Add($"{workplace.Name} — a workplace ({Describe(workplace.Kind)})");
        }

        lines.Add(workplace.WorkerIds.Count == 0
            ? $"Nobody works here. Room for {workplace.Places}."
            : $"Worked by {WorkerNames(world, workplace)} — {workplace.WorkerIds.Count} of " +
              $"{workplace.Places} places filled");

        // Who decided that number (D51). Said in words rather than shown as a widget
        // state, because "the village decides" and "you said two" are different facts
        // about the same building and the player should be able to tell which they are
        // looking at.
        lines.Add(workplace.StaffingOverride is int set
            ? $"Staffing: you have asked for {set}. Room for {workplace.Capacity}."
            : $"Staffing: left to the village — it wants {LabourQuota.For(world).For(workplace.Kind)} " +
              $"on this kind of work. Room for {workplace.Capacity}.");

        // The buffer at the point of production (D30). Worth showing because it is how
        // you tell "idle for want of a worker" from "idle for want of logs" (D29).
        if (workplace.Store.Held > 0)
        {
            lines.Add($"Holding: {DescribeGoods(workplace.Store)}");
        }
        else if (workplace.Kind == JobKind.Woodcutter)
        {
            lines.Add("Holding: nothing — no logs here to split.");
        }
    }

    private static void DescribeStore(StoreBuilding store, List<string> lines)
    {
        Separate(lines);

        lines.Add($"{store.Name} — a {Describe(store.Kind)}");
        lines.Add($"Holding: {DescribeGoods(store.Store)}");

        // Capacity is derived rather than typed in (D33), and it is the number that
        // decides how big the village gets — so it belongs on screen, not just in a
        // spec.
        lines.Add(store.Store.IsFull
            ? $"Full: {store.Store.Held} of {store.Store.Capacity} — nothing more will fit."
            : $"Space: {store.Store.Held} of {store.Store.Capacity} used, " +
              $"{store.Store.FreeSpace} free");
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

        lines.Add($"Larder: {DescribeGoods(household.Stockpile)}");
    }

    private static void DescribeBareGround(SimWorld world, GridPos tile, List<string> lines)
    {
        string ground = world.Map.TerrainAt(tile) switch
        {
            Terrain.Water => "The river. Nobody can cross it and nothing can be built on it.",
            Terrain.Forest => "Woodland.",
            _ => "Open ground.",
        };

        lines.Add(ground);

        if (world.Zones.IsResidential(tile))
        {
            lines.Add("Painted for housing — the village may build a home here.");
        }
    }

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
    private static string DescribeGoods(Stockpile store)
    {
        var parts = new List<string>();

        if (store.Food > 0)
        {
            parts.Add($"{store.Food} food");
        }

        if (store.Logs > 0)
        {
            parts.Add($"{store.Logs} logs");
        }

        if (store.Firewood > 0)
        {
            parts.Add($"{store.Firewood} firewood");
        }

        return parts.Count == 0 ? "nothing" : string.Join(", ", parts);
    }

    private static string Describe(JobKind kind) => kind switch
    {
        JobKind.Forager => "food is gathered here",
        JobKind.Logger => "trees are felled here",
        JobKind.Woodcutter => "logs are split into firewood here",
        JobKind.Marketer => "goods are handed out from here",
        JobKind.Builder => "something is being raised here",
        _ => kind.ToString().ToLowerInvariant(),
    };

    private static string Describe(StoreKind kind) => kind switch
    {
        StoreKind.Granary => "granary, which holds the village's food",
        StoreKind.Shed => "storage shed, which holds logs and firewood",
        StoreKind.Market => "market, which holds food and firewood for the houses near it",
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

        // Anchors AND offsets. SetAnchorsPreset alone moves the anchors but leaves the
        // offsets, so the container still sized itself to its content - which first
        // pushed the time controls off the bottom of the window, and then left the
        // whole UI huddled in the top-left corner. This is the call that does both.
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        root.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);

        var column = new VBoxContainer();
        column.AddThemeConstantOverride("separation", 10);
        root.AddChild(column);

        _clockLabel = OneLine(Heading(string.Empty));
        column.AddChild(_clockLabel);

        _villageLabel = OneLine(Body(string.Empty));
        column.AddChild(_villageLabel);

        // STANDING ALERTS GET THEIR OWN FIXED-HEIGHT STRIP, AND THE HEIGHT IS THE POINT.
        //
        // These used to be appended to _villageLabel with a newline, so the header was
        // one line tall, or two, or three, depending on what the village happened to be
        // short of. Everything below it shifted to match — and what is below it is the
        // MAP, which is the one element with ExpandFill and therefore absorbs the whole
        // difference. VillageMap draws around the centre of its own rect (ToScreen adds
        // Size / 2), so a header growing by one line moved the entire valley on screen
        // and changed how much of it fit. Joe watched it and called it the viewing
        // portal jumping around, which is exactly what it was.
        //
        // So the strip is always here, always exactly two lines tall whether or not
        // there is anything to say. Reserving the space costs a little of the window
        // once; reflowing costs the player their place in the world every time a couple
        // starts waiting for a house.
        //
        // Two lines because there are two standing alerts (nowhere to build, work nobody
        // is doing). If a third is ever added, this number moves with it — and the
        // height is asked of the font rather than written down, so it stays right when
        // somebody changes the font size.
        _alertLabel = OneLine(Body(string.Empty));
        _alertLabel.Modulate = new Color(1f, 0.78f, 0.35f);
        column.AddChild(_alertLabel);

        // Added to the tree FIRST, because GetLineHeight() reads the resolved theme and
        // a label that is not in the tree yet has none. The floor is there for the same
        // reason belt is worn with braces: a zero would collapse the strip and quietly
        // restore the exact bug this is here to fix, and a wrong-but-present height only
        // looks slightly off.
        float line = _alertLabel.GetLineHeight();
        _alertLabel.CustomMinimumSize =
            new Vector2(0, Mathf.Max(line, 18f) * StandingAlertLines);

        // The seed and the audit log together, because they are the two things you need
        // to reproduce and explain a run: the seed says which world, the log says what
        // happened in it.
        _seedLabel = OneLine(Muted(
            $"seed {_loop.World.Seed}   ·   config: {_configSource}   ·   log: {_logPath}"));
        column.AddChild(_seedLabel);

        // Time controls sit at the TOP, under the header. Belt and braces after two
        // failed attempts to pin them at the bottom: nothing below them in the
        // layout can push them off screen if there is nothing below them.
        var controls = new HBoxContainer { CustomMinimumSize = new Vector2(0, 34) };
        controls.AddThemeConstantOverride("separation", 10);
        column.AddChild(controls);

        column.AddChild(new HSeparator());

        // The village itself, drawn. Everything else is here to explain it — so it
        // gets the largest share of the window, and the panels below keep only their
        // minimums.
        _map = new VillageMap
        {
            SizeFlagsVertical = SizeFlags.ExpandFill,
            SizeFlagsStretchRatio = 2.2f,
            CustomMinimumSize = new Vector2(0, 320),

            // Without this the catchment rings - which are far larger than the map
            // panel - draw straight across the rest of the window.
            ClipContents = true,
        };
        _map.BuildingClicked += OnBuildingClicked;
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

        controls.AddChild(new VSeparator());

        _detailButton = new Button { CustomMinimumSize = new Vector2(140, 0) };
        _detailButton.Pressed += CycleDetail;
        controls.AddChild(_detailButton);

        // With a valley this size and free panning, getting lost is easy and a way
        // back is not optional.
        var recentre = new Button { Text = "Centre on village" };
        recentre.Pressed += () => _map.CentreOnTheVillage();
        controls.AddChild(recentre);

        controls.AddChild(Muted(
            "   (space to pause · 1-4 speed · WASD pan · wheel zoom · tab routes · home recentre)"));

        BuildBuildMenu(column, controls);

        SetSpeed(1.0);

        // Start on Selected, and set the button's label from the same switch that the
        // key binding uses — two places writing that text would eventually disagree.
        _detail = MapDetail.Off;
        CycleDetail();
    }

    /// <summary>What the cursor is over, or what just happened. Empty when not placing.</summary>
    private Label _placementLabel = null!;

    /// <summary>
    /// The build menu (D43) — the first controls in the game that change the world
    /// rather than the view.
    /// </summary>
    /// <remarks>
    /// Every other control here alters how you are looking: speed, pan, zoom, how much
    /// explanation is drawn. These four buttons and a demolish are the first that ask
    /// the village for something, so they get their own row rather than being mixed in
    /// with the camera.
    /// </remarks>
    private void BuildBuildMenu(VBoxContainer column, Control controls)
    {
        var row = new HBoxContainer();
        row.AddThemeConstantOverride("separation", 8);

        row.AddChild(Muted("Build:"));
        row.AddChild(BuildButton("Granary", BuildingKind.Granary));
        row.AddChild(BuildButton("Shed", BuildingKind.Shed));
        row.AddChild(BuildButton("Market", BuildingKind.Market));
        row.AddChild(BuildButton("Woodcutter", BuildingKind.WoodcutterHut));

        var demolish = new Button { Text = "Demolish" };
        demolish.Pressed += () => _map.BeginDemolishing();
        row.AddChild(demolish);

        row.AddChild(new VSeparator());

        // The brush (D42). Separated from the buildings because it is a different kind
        // of decision: those place one thing, this says where a whole neighbourhood may
        // grow — and the village decides which tiles, and when, and whether at all.
        row.AddChild(Muted("Homes:"));

        var paint = new Button { Text = "Paint land" };
        paint.Pressed += () => _map.BeginPainting(1);
        row.AddChild(paint);

        var erase = new Button { Text = "Take back" };
        erase.Pressed += () => _map.BeginPainting(-1);
        row.AddChild(erase);

        var stop = new Button { Text = "Cancel" };
        stop.Pressed += () => _map.BeginBuilding(null);
        row.AddChild(stop);

        // The refusal or the warning, in the words the sim already produced. Same
        // standard as JobReason: a red square on its own is the shrug this project
        // keeps refusing.
        // ExpandFill so the message takes whatever room is going SPARE rather than
        // demanding room of its own. Without it a long refusal — and refusals here are
        // full sentences on purpose — widened the row until the staffing controls at the
        // end of it were pushed off the right of the window. A message that hides a
        // button is a message that costs the player a control to gain a sentence.
        _placementLabel = OneLine(Body(string.Empty));
        _placementLabel.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        row.AddChild(_placementLabel);

        _map.PlacementMessageChanged += message => _placementLabel.Text = message;

        // Staffing, on the same row (D51). Deliberately NOT a control that appears only
        // when a workplace is selected: a button that comes and goes is a button the
        // player has to hunt for, and these do nothing harmful when nothing is selected.
        row.AddChild(new VSeparator());
        row.AddChild(Muted("Staffing:"));

        var fewer = new Button { Text = "−1" };
        fewer.Pressed += () => ChangeStaffing(-1);
        row.AddChild(fewer);

        var more = new Button { Text = "+1" };
        more.Pressed += () => ChangeStaffing(+1);
        row.AddChild(more);

        var auto = new Button { Text = "Let the village decide" };
        auto.Pressed += LetTheVillageDecideStaffing;
        row.AddChild(auto);

        // Under the time controls, above the map. Taking the column explicitly rather
        // than casting whatever was handed in: the first version took the UI root and
        // tested `is VBoxContainer`, which is a MarginContainer — so the whole menu was
        // built, wired up, and silently never added to anything.
        //
        // ASKED WHERE THE CONTROLS ARE rather than told. This was a literal 4, counted
        // by hand off the order the header happened to be built in — so adding the
        // standing-alert strip above it silently moved the build menu ABOVE the time
        // controls, which is a layout nobody chose and nothing would have caught. The
        // node knows its own index; there is no reason to keep a second copy of it in
        // an argument that goes stale the first time the header changes.
        column.AddChild(row);
        column.MoveChild(row, controls.GetIndex() + 1);
    }

    private Button BuildButton(string text, BuildingKind kind)
    {
        var button = new Button { Text = text };
        button.Pressed += () => _map.BeginBuilding(kind);
        return button;
    }

    private Button SpeedButton(string text, double multiplier)
    {
        var button = new Button { Text = text, CustomMinimumSize = new Vector2(64, 0) };
        button.Pressed += () => SetSpeed(multiplier);
        return button;
    }

    /// <summary>
    /// The idle workplaces, as something a person would say out loud.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Distinct names, and that is not merely tidiness.</b> Joe's screen read
    /// <em>"the berry patch, the southern western thicket, the southern eastern thicket,
    /// the southern eastern thicket"</em> — the same phrase twice, because two different
    /// tree stands were generated with the same name. Saying it twice tells the player
    /// nothing they can act on and reads like a bug, which for their purposes it is.
    /// </para>
    /// <para>
    /// <b>The collision itself is the sim's to fix</b>, not this method's: two workplaces
    /// that a player cannot tell apart by name are two the game cannot explain. Collapsing
    /// them here stops the sentence embarrassing itself; it does not make the names
    /// unique.
    /// </para>
    /// </remarks>
    private static string NameThem(IReadOnlyList<Workplace> unmanned)
    {
        var names = new List<string>();
        for (int i = 0; i < unmanned.Count; i++)
        {
            if (!names.Contains(unmanned[i].Name))
            {
                names.Add(unmanned[i].Name);
            }
        }

        int shown = names.Count < MostPlacesToName ? names.Count : MostPlacesToName;

        var said = new List<string>();
        for (int i = 0; i < shown; i++)
        {
            said.Add(names[i]);
        }

        string list = string.Join(", ", said);
        int more = names.Count - shown;

        return more <= 0 ? list : $"{list} and {more} more";
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

    /// <summary>
    /// One line, ending in an ellipsis rather than off the edge of the window.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Every one of these was a sentence written to be read and then drawn past the
    /// right-hand edge of the screen, which is the worst of both: the game has taken the
    /// trouble to explain itself and the player cannot see the end of the explanation.
    /// Joe's screenshot caught the alert strip losing <em>"There is no one spare to
    /// send."</em>
    /// </para>
    /// <para>
    /// An ellipsis is an honest failure — it says <em>there is more here</em> — where a
    /// hard clip just looks like the sentence stopped. Wrapping is the other option and
    /// it is refused on purpose: a label that wraps has a height that depends on the
    /// window width, and a header whose height moves is what makes the map jump.
    /// </para>
    /// </remarks>
    private static Label OneLine(Label label)
    {
        label.AutowrapMode = TextServer.AutowrapMode.Off;
        label.ClipText = true;
        label.TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis;
        return label;
    }
}

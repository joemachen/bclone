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
    /// How many idle workplaces to name before falling back to a count.
    /// </summary>
    /// <remarks>
    /// The alert wraps inside a floating panel now, so this is about how much a person
    /// wants to read rather than about what fits on a line. Four names, then a count —
    /// a player told the village is short of hands does not need the full inventory to
    /// act on it.
    /// </remarks>
    private const int MostPlacesToName = 4;

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
        FastForwardForAScreenshot(config);
        Refresh();
    }

    /// <summary>
    /// Run the sim forward before drawing anything, when asked to
    /// (<c>BCLONE_SCREENSHOT_YEARS</c>).
    /// </summary>
    /// <remarks>
    /// A village at tick 3 has four people standing on their doorstep, an empty log and
    /// nothing built — which is a true picture of the game and a useless one. This steps
    /// the loop before the first frame so a screenshot can show a settlement that has
    /// lived a while. It is also the quickest way to look at a state that takes an hour
    /// to reach by playing: <c>BCLONE_SCREENSHOT_YEARS=80</c> and see what year 80 does
    /// to the panels.
    /// </remarks>
    private void FastForwardForAScreenshot(SimConfig config)
    {
        if (!int.TryParse(
                System.Environment.GetEnvironmentVariable("BCLONE_SCREENSHOT_YEARS"),
                out int years)
            || years <= 0)
        {
            return;
        }

        _loop.Step(config.TicksPerYear * years);
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
        MaybeTakeAScreenshot();
    }

    /// <summary>Frames to let the village run before a screenshot is taken.</summary>
    /// <remarks>
    /// Long enough for the map to frame itself and for the panels to have something in
    /// them — a shot of tick 0 shows an empty log and a village that has not moved.
    /// </remarks>
    private const int FramesBeforeAScreenshot = 300;

    private int _framesDrawn;

    /// <summary>
    /// Render the window to a PNG and quit, when asked to (<c>BCLONE_SCREENSHOT</c>).
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>The only verification a view change has.</b> Nothing in <c>src/Bclone.Game</c>
    /// can be unit-tested — D11 puts it outside the solution on purpose — so "does it
    /// look right?" is answered by looking, and this is what makes looking repeatable.
    /// It is also how the README's screenshot is taken, and how one gets captured for
    /// each version (METHODOLOGY §5).
    /// </para>
    /// <para>
    /// <b>Permanent rather than throwaway, deliberately.</b> This exact hook was written
    /// and deleted twice while chasing a map that jumped and a panel that clipped, and
    /// D54 recorded the conclusion: <em>the fact that it took throwaway code to make it
    /// is the argument for giving the view a permanent way to do this.</em> It costs one
    /// integer compare per frame when the variable is unset.
    /// </para>
    /// <para>
    /// Environment rather than a command-line flag because Godot owns argument parsing
    /// and would have to be taught to ignore an unknown one.
    /// </para>
    /// </remarks>
    private void MaybeTakeAScreenshot()
    {
        if (++_framesDrawn != FramesBeforeAScreenshot)
        {
            return;
        }

        string? path = System.Environment.GetEnvironmentVariable("BCLONE_SCREENSHOT");
        if (string.IsNullOrWhiteSpace(path))
        {
            return;
        }

        // Forced, because the texture otherwise holds whatever was drawn last frame —
        // which on the frame a panel changes is the version without the change in it.
        RenderingServer.ForceDraw();

        Error saved = GetViewport().GetTexture().GetImage().SavePng(path);
        if (saved != Error.Ok)
        {
            // Loudly (METHODOLOGY §4). A screenshot that silently did not happen is
            // worse than none, because the file on disk is the previous run's.
            GD.PushError($"bclone: could not write the screenshot to {path} — {saved}.");
        }

        GetTree().Quit();
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
        //
        // "Food" is now everything the village has, granaries included, and says so. It
        // used to be a households-only sum sitting next to "N in the granaries", which
        // read as a total-and-its-largest-part but was neither: the two numbers did not
        // overlap, so a village with 2,000 food in store showed a few hundred.
        _villageLabel.Text =
            $"{world.Population} villagers · {LivingHouseholds(world)} households · " +
            $"{world.TotalFood()} food all told, {world.FoodInGranaries()} of it in the granaries · " +
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

        // Shown only when there is something to say, and no padding to a reserved height.
        // The panel floats, so it can grow and shrink without moving anything on screen —
        // which is the whole reason the layout changed.
        _alertLabel.Text = string.Join("\n\n", alerts);
        _alertLabel.Visible = alerts.Count > 0;

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

            // "no work" was true and unhelpful: it read as a fault when it is usually the
            // village resting, and it said the same thing about a child as about an adult
            // nobody needs. A laborer is a state with a name (D63).
            string work = villager.HasJob ? "working"
                : villager.IsLaborer ? "laborer"
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
        AddChild(_map);

        BuildStatusPanel();
        BuildLogPanel();
        BuildRosterPanel();
        BuildInspectorPanel();
        BuildControlPanel();

        SetSpeed(1.0);

        // Start on Selected, and set the button's label from the same switch that the
        // key binding uses — two places writing that text would eventually disagree.
        _detail = MapDetail.Off;
        CycleDetail();
    }

    /// <summary>Where the panels sit, in pixels from the edge they are pinned to.</summary>
    private const int Edge = 14;

    private const int StatusWidth = 560;
    private const int LogWidth = 620;
    private const int LogHeight = 210;
    private const int RosterWidth = 330;
    private const int RosterHeight = 280;

    /// <summary>
    /// Room kept clear along the bottom for the controls, which are wider than the window.
    /// </summary>
    /// <remarks>
    /// The build menu is a long row and the roster is pinned to the same corner of the
    /// screen, so the two overlapped and the controls drew straight over the names. The
    /// roster stands off the bottom by this much instead. A measured constant rather than
    /// a computed one because the controls' height is settled by their own contents, and
    /// asking a container for its size during layout is how circular dependencies start.
    /// </remarks>
    private const int ControlsReserve = 160;
    private const int InspectorWidth = 400;
    private const int InspectorHeight = 330;

    /// <summary>
    /// What the village is: the date, what it holds, and anything it is asking for.
    /// </summary>
    private void BuildStatusPanel()
    {
        VBoxContainer body = Floating(Edge, Edge, StatusWidth, 0, Corner.TopLeft);

        _clockLabel = Heading(string.Empty);
        body.AddChild(_clockLabel);

        _villageLabel = Wrapped(Body(string.Empty));
        body.AddChild(_villageLabel);

        // STANDING ALERTS, AND THEY WRAP NOW.
        //
        // Both of them — nowhere to build (D42), work nobody is doing (D47) — are STATES
        // rather than events, which is why they are here and not only in the log: a line
        // that scrolls away is a problem the player never learns they have.
        //
        // D54 had to give these a fixed two-line strip and cut them off with an ellipsis,
        // because they lived in the same column as the map and any change in their height
        // moved the world. Joe's next screenshot showed the cost of that: the sentence now
        // ended in "There is no one spa…" instead of running off the edge, which is tidier
        // and no more readable. In a floating panel neither problem exists — the text
        // wraps, the panel grows, and nothing else on screen notices.
        _alertLabel = Wrapped(Body(string.Empty));
        _alertLabel.Modulate = new Color(1f, 0.78f, 0.35f);
        body.AddChild(_alertLabel);

        // The seed and the audit log together, because they are the two things you need
        // to reproduce and explain a run: the seed says which world, the log says what
        // happened in it.
        _seedLabel = Wrapped(Muted(
            $"seed {_loop.World.Seed}   ·   config: {_configSource}   ·   log: {_logPath}"));
        body.AddChild(_seedLabel);
    }

    /// <summary>The story so far — Banished's event log, in much the same corner.</summary>
    private void BuildLogPanel()
    {
        VBoxContainer body = Floating(Edge, Edge, LogWidth, LogHeight, Corner.TopRight, "Village log");

        _villageLog = new RichTextLabel
        {
            ScrollFollowing = true,
            BbcodeEnabled = false,
            SizeFlagsVertical = SizeFlags.ExpandFill,
        };
        body.AddChild(_villageLog);
    }

    /// <summary>Everyone alive, and what they are doing about it.</summary>
    private void BuildRosterPanel()
    {
        VBoxContainer body = Floating(
            Edge, ControlsReserve, RosterWidth, RosterHeight, Corner.BottomLeft, "The village");

        _roster = new ItemList { SizeFlagsVertical = SizeFlags.ExpandFill };
        _roster.ItemSelected += OnVillagerSelected;
        body.AddChild(_roster);
    }

    /// <summary>Whoever — or whatever — you last clicked on.</summary>
    private void BuildInspectorPanel()
    {
        // Below the log on the right-hand edge, which is where Banished puts the panel
        // for the thing you have selected.
        VBoxContainer body = Floating(
            Edge, Edge + LogHeight + Edge, InspectorWidth, InspectorHeight,
            Corner.TopRight, "Who they are, and why");

        // ScrollActive so a long reason scrolls rather than being cut off. The one panel
        // whose job is explaining a decision must never truncate the explanation.
        _inspector = new RichTextLabel
        {
            BbcodeEnabled = false,
            ScrollActive = true,
            SizeFlagsVertical = SizeFlags.ExpandFill,
        };
        body.AddChild(_inspector);
    }

    /// <summary>Speed, what the map draws, and what the player can ask the village for.</summary>
    private void BuildControlPanel()
    {
        VBoxContainer body = Floating(Edge, Edge, 0, 0, Corner.BottomRight);
        body.AddThemeConstantOverride("separation", 8);

        var controls = new HBoxContainer();
        controls.AddThemeConstantOverride("separation", 6);
        body.AddChild(controls);

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

        body.AddChild(BuildBuildMenu());

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
            "space to pause · 1-4 speed · WASD pan · wheel zoom · tab routes · home recentre")));
    }

    // ---------------------------------------------------------------
    //  The furniture the panels are made of
    // ---------------------------------------------------------------

    /// <summary>Which corner a floating panel is pinned to.</summary>
    private enum Corner { TopLeft, TopRight, BottomLeft, BottomRight }

    /// <summary>
    /// A panel pinned to one corner, with a body for the caller to fill.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A <paramref name="width"/> or <paramref name="height"/> of zero means <em>as big as
    /// the contents need</em>: Godot clamps a control to its own minimum size, so a panel
    /// asked for nothing grows to fit and then stops. That is what lets the status panel
    /// swell by a line when the village starts asking for something.
    /// </para>
    /// <para>
    /// <b>Mouse filter set to Stop, explicitly.</b> A Container defaults to Pass, so every
    /// click on a panel would have gone through to the map behind it — and the map now
    /// covers the whole window, so "behind it" means placing a granary under the button
    /// you just pressed.
    /// </para>
    /// </remarks>
    private VBoxContainer Floating(
        float x, float y, float width, float height, Corner corner, string? title = null)
    {
        var panel = new PanelContainer { MouseFilter = MouseFilterEnum.Stop };
        panel.AddThemeStyleboxOverride("panel", PanelSkin());

        bool right = corner is Corner.TopRight or Corner.BottomRight;
        bool bottom = corner is Corner.BottomLeft or Corner.BottomRight;

        panel.AnchorLeft = panel.AnchorRight = right ? 1f : 0f;
        panel.AnchorTop = panel.AnchorBottom = bottom ? 1f : 0f;

        panel.OffsetLeft = right ? -(x + width) : x;
        panel.OffsetRight = right ? -x : x + width;
        panel.OffsetTop = bottom ? -(y + height) : y;
        panel.OffsetBottom = bottom ? -y : y + height;

        // WHICH WAY IT GROWS WHEN IT OUTGROWS THE SIZE IT WAS ASKED FOR, and the default
        // is wrong for half the corners. Godot clamps a control to its minimum size by
        // pushing its right and bottom edges outward, so a panel pinned to the
        // bottom-right and asked for nothing grew off the screen entirely — the controls
        // vanished, leaving one lit pixel in the corner. Panels pinned to an edge have to
        // grow away from it.
        panel.GrowHorizontal = right ? GrowDirection.Begin : GrowDirection.End;
        panel.GrowVertical = bottom ? GrowDirection.Begin : GrowDirection.End;

        var body = new VBoxContainer();
        body.AddThemeConstantOverride("separation", 6);
        panel.AddChild(body);

        if (title is not null)
        {
            body.AddChild(Muted(title));
        }

        AddChild(panel);
        return body;
    }

    /// <summary>The look of every floating panel: dark, bordered, readable over a lit map.</summary>
    /// <remarks>
    /// Nearly opaque rather than lightly tinted. A panel you can see the valley through is
    /// a panel whose text sits on grass one moment and on a roof the next, and the whole
    /// complaint that prompted this was that the information was hard to read.
    /// </remarks>
    private static StyleBoxFlat PanelSkin()
    {
        var skin = new StyleBoxFlat
        {
            BgColor = new Color(0.08f, 0.09f, 0.10f, 0.93f),
            BorderColor = new Color(0.58f, 0.53f, 0.40f, 0.70f),
        };

        skin.SetBorderWidthAll(1);
        skin.SetContentMarginAll(10);
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
    private static Label Wrapped(Label label)
    {
        label.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        label.CustomMinimumSize = new Vector2(120, 0);
        return label;
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
    private HBoxContainer BuildBuildMenu()
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

        // HANDED BACK RATHER THAN INSERTED, which is the third and last version of this.
        // It began by taking the UI root and testing `is VBoxContainer` — the root is a
        // MarginContainer, so the whole menu was built, wired up and silently never added
        // to anything. Then it took the column and a hand-counted index, which went stale
        // the moment a line was added to the header and put the build menu above the time
        // controls. Returning the row lets the caller decide where it goes, and there is
        // nothing left to get wrong.
        return row;
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
    /// Joe's screen once read <em>"the berry patch, the southern western thicket, the
    /// southern eastern thicket, the southern eastern thicket"</em> — the same phrase
    /// twice, because a bearing has eight values and the village has six forage sites.
    /// This method collapsed the repeat for a day.
    /// </para>
    /// <para>
    /// <b>It does not any more, because it should never have been the view's job.</b>
    /// <c>SimWorld</c> now guarantees that no two places share a name (D56), and a test
    /// over fifty valleys says so — so a second copy of the rule here would be a second
    /// place for it to be true, which is how two rules end up disagreeing. All that is
    /// left is the count, which is a question about how much a person wants to read.
    /// </para>
    /// </remarks>
    private static string NameThem(IReadOnlyList<Workplace> unmanned)
    {
        int shown = unmanned.Count < MostPlacesToName ? unmanned.Count : MostPlacesToName;

        var said = new List<string>();
        for (int i = 0; i < shown; i++)
        {
            said.Add(unmanned[i].Name);
        }

        string list = string.Join(", ", said);
        int more = unmanned.Count - shown;

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
}

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
    private HBoxContainer _staffingRow = null!;
    private Label _staffingLabel = null!;
    private HBoxContainer _queueRow = null!;
    private Label _queueLabel = null!;
    private HBoxContainer _groundRow = null!;
    private Label _groundLabel = null!;
    private Button _modeButton = null!;
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
            $"{world.TotalFood()} food all told, {world.FoodInGranaries()} of it in the stores · " +
            $"{world.LogsInSheds()} logs and {world.FirewoodInSheds()} firewood in the stores";

        // What each limited good actually stands at, beside the number the player set —
        // so "nobody is splitting logs" and "you asked for 200 and there are 214" are the
        // same glance rather than two.
        for (int i = 0; i < _stockLimitReadouts.Count; i++)
        {
            (Goods goods, Label held) = _stockLimitReadouts[i];
            held.Text = $"have {HeldFor(world, goods)}";
        }

        // The same glance for the professions: how many are actually on this work, and how
        // many places there are to be on it (D106). "0 of 2" is Joe's screenshot, and it is
        // what tells you whether asking for three would achieve anything.
        LabourQuota quota = LabourQuota.For(world);
        for (int i = 0; i < _professionReadouts.Count; i++)
        {
            (JobKind kind, Label places) = _professionReadouts[i];
            places.Text = $"{WorkingAt(world, kind)} of {SeatsFor(world, kind)} — "
                + $"village wants {quota.For(kind)}";
        }

        _laborerReadout.Text = $"{world.Laborers}";

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
        Workplace? staffable = selected is { IsSite: false } ? selected : null;

        _staffingRow.Visible = staffable is not null;
        if (staffable is not null)
        {
            _staffingLabel.Text = staffable.StaffingOverride is int asked
                ? $"Staffing {staffable.Name} — you asked for {asked} of {staffable.Capacity}:"
                : $"Staffing {staffable.Name} — village's choice, {staffable.Places} of "
                    + $"{staffable.Capacity}:";
        }

        // The ground controls belong to a building that keeps ground — a forester's hut
        // today, and whatever else earns one later. Shown by whether the building CAN own
        // ground rather than by what kind it is, so the next one needs no line here.
        bool keepsGround = staffable is { Kind: JobKind.Forester };
        _groundRow.Visible = keepsGround;
        if (keepsGround)
        {
            int tiles = world.Zones.WorkGroundTiles(staffable!.Id);
            int allowance = world.WorkGroundAllowanceFor(staffable);
            _groundLabel.Text = $"Ground — {tiles} tiles, enough hands for {allowance}:";
            _modeButton.Text = staffable.Mode == WorkMode.Plant
                ? "Planting: ON"
                : "Planting: off";
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

    private void LetTheVillageDecideStaffing()
    {
        Workplace? workplace = SelectedWorkplace();
        if (workplace is { IsSite: false })
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
            lines.Add(site.HasMaterials
                ? $"Materials: all {site.Recipe.Logs} logs delivered"
                : $"Materials: {site.LogsDelivered} of {site.Recipe.Logs} logs — " +
                  $"{site.LogsStillNeeded} still to come");
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
            lines.Add(wooded == 0
                ? "Nothing grows here any more — its gatherers bring back nothing at all. "
                    + "Plant it, or move the work."
                : $"A trip brings back {world.GatherYieldAt(workplace)} food — {share}% of what "
                    + "this hut would yield in full woodland.");
        }

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
        // Every good, by index. Written three times by hand until stone and tools
        // arrived, which is the point at which a panel whose whole job is to say what is
        // here would have started silently omitting things.
        var parts = new List<string>();

        for (int i = 0; i < Stockpile.Kinds; i++)
        {
            var goods = (Goods)i;
            if (store[goods] > 0)
            {
                parts.Add($"{store[goods]} {NameOf(goods)}");
            }
        }

        return parts.Count == 0 ? "nothing" : string.Join(", ", parts);
    }

    /// <summary>A good's name as a player would say it.</summary>
    private static string NameOf(Goods goods) => goods switch
    {
        Goods.Food => "food",
        Goods.Logs => "logs",
        Goods.Firewood => "firewood",
        Goods.Stone => "stone",
        Goods.Tools => "tools",
        _ => goods.ToString().ToLowerInvariant(),
    };

    private static string Describe(JobKind kind) => kind switch
    {
        JobKind.Forager => "food is gathered here",
        JobKind.Forester => "trees are felled here",
        JobKind.Woodcutter => "logs are split into firewood here",
        JobKind.Marketer => "goods are handed out from here",
        // The HUT, not the site — a site describes itself and never reaches this (D108).
        JobKind.Builder => "the village's builders work from here",
        _ => kind.ToString().ToLowerInvariant(),
    };

    private static string Describe(StoreKind kind) => kind switch
    {
        StoreKind.Granary => "granary, which holds the village's food",
        StoreKind.Shed => "storage shed, which holds logs and firewood",
        StoreKind.Market => "market, which holds food and firewood for the houses near it",
        StoreKind.Pile => "storage pile — cleared ground, and it holds anything",
        StoreKind.Cart => "cart the founders arrived in, which holds anything",
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

        // ⭐ TWO COLUMNS, AND PANELS LIVE IN THEM RATHER THAN BESIDE THEM. Joe: *"when the
        // 'what the village is told' window is open, you can see 'the village' window
        // underneath."* Every panel used to anchor itself to a corner at an offset somebody
        // had to work out, so two panels growing toward each other overlapped — and once they
        // were see-through, what showed through was another panel rather than the valley.
        //
        // **A column makes overlap impossible by construction** instead of by choosing sizes
        // carefully, which is the only kind of fix that survives adding a seventh panel.
        _leftColumn = Column(Corner.TopLeft);
        _rightColumn = Column(Corner.TopRight);

        BuildStatusPanel();
        BuildVillageOrdersPanel();
        BuildRosterPanel();
        BuildLogPanel();
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
        VBoxContainer body = InColumn(_leftColumn, 0);

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
        VBoxContainer body = InColumn(_rightColumn, LogHeight, "Village log");

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
        VBoxContainer body = InColumn(_leftColumn, RosterHeight, "The village");

        _roster = new ItemList { SizeFlagsVertical = SizeFlags.ExpandFill };
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
        VBoxContainer body = InColumn(_rightColumn, 0, "Who they are, and why");

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
            CustomMinimumSize = new Vector2(0, 170),
            SizeFlagsVertical = SizeFlags.ExpandFill,
        };
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
        _staffingRow = new HBoxContainer { Visible = false };
        _staffingRow.AddThemeConstantOverride("separation", 6);
        body.AddChild(_staffingRow);

        _staffingLabel = Muted("Staffing:");
        _staffingRow.AddChild(_staffingLabel);

        var fewer = new Button { Text = "−1" };
        fewer.Pressed += () => ChangeStaffing(-1);
        _staffingRow.AddChild(fewer);

        var more = new Button { Text = "+1" };
        more.Pressed += () => ChangeStaffing(+1);
        _staffingRow.AddChild(more);

        var auto = new Button { Text = "Village decides" };
        auto.Pressed += LetTheVillageDecideStaffing;
        _staffingRow.AddChild(auto);

        // ⭐ AND THE BUILD QUEUE, WHICH IS JOE'S OWN ANSWER TO HIS VILLAGE FREEZING:
        // "I think this is solved by letting the user increase/decrease the priority level of
        // a building under construction." It is — and it is better than any rule about which
        // KIND of building matters most, because the village cannot know whether this winter
        // needs a granary or a roof and the player can.
        _queueRow = new HBoxContainer { Visible = false };
        _queueRow.AddThemeConstantOverride("separation", 6);
        body.AddChild(_queueRow);

        _queueLabel = Muted("Build queue:");
        _queueRow.AddChild(_queueLabel);

        var sooner = new Button { Text = "▲ Sooner" };
        sooner.Pressed += () => MoveSelectedInQueue(-1);
        _queueRow.AddChild(sooner);

        var later = new Button { Text = "▼ Later" };
        later.Pressed += () => MoveSelectedInQueue(+1);
        _queueRow.AddChild(later);

        // ⭐ THE GROUND A BUILDING KEEPS (D86), reaching the player at last. The sim side has
        // been built and unused since C3c — painted per workplace, priced in workers, with the
        // overstretched warning already written — because there was no building that owned
        // ground until the forester's hut. It sits in the panel rather than on the toolbar for
        // the reason D104 settled: a brush that belongs to ONE building needs to be beside the
        // name of that building, or the player has to remember which one it will paint for.
        _groundRow = new HBoxContainer { Visible = false };
        _groundRow.AddThemeConstantOverride("separation", 6);
        body.AddChild(_groundRow);

        _groundLabel = Muted("Ground:");
        _groundRow.AddChild(_groundLabel);

        var give = new Button { Text = "Give ground" };
        give.Pressed += () => PaintGroundForSelection(1);
        _groundRow.AddChild(give);

        var takeBack = new Button { Text = "Take back" };
        takeBack.Pressed += () => PaintGroundForSelection(-1);
        _groundRow.AddChild(takeBack);

        // ⭐ AND THE MODE — the first control in this game that tells a building to PUT
        // SOMETHING BACK (Joe, ungated). It ships enabled rather than greyed behind managed
        // forestry, which is the change `professions.md §6.2` records.
        _modeButton = new Button { Text = "Planting: off" };
        _modeButton.Pressed += ToggleSelectedMode;
        _groundRow.AddChild(_modeButton);
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

        workplace.Mode = workplace.Mode == WorkMode.Plant ? WorkMode.Harvest : WorkMode.Plant;
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
    private void BuildVillageOrdersPanel()
    {
        // ⚠️ ROLLED UP BY DEFAULT, and that is the whole point rather than a compromise. These
        // are STANDING ORDERS — you set them and then watch the year — so the panel's resting
        // state should be a strip of title, not eleven rows of numbers competing with the
        // valley. Open, it is tall enough to reach the roster and the control bar; closed, it
        // costs one line. Joe asked for less on screen, and a panel that is only there when it
        // is wanted is more of an answer than a smaller one that is always there.
        VBoxContainer body = InColumn(
            _leftColumn, 0, "What the village is told", startOpen: false);

        body.AddChild(BuildProfessionsMenu());
        body.AddChild(BuildStockLimitMenu());
    }

    private const int OrdersWidth = 360;

    /// <summary>Room the status panel needs above this one. Generous — panels overlap safely now.</summary>
    private const int StatusHeight = 290;

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
        body.AddChild(BuildHarvestMenu());

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
            "space to pause · 1-4 speed · WASD pan · wheel zoom · tab routes · home recentre · "
            + "c fold panels · h hide them")));
    }

    // ---------------------------------------------------------------
    //  The furniture the panels are made of
    // ---------------------------------------------------------------

    /// <summary>Which corner a floating panel is pinned to.</summary>
    private enum Corner { TopLeft, TopRight, BottomLeft, BottomRight }

    private VBoxContainer _leftColumn = null!;
    private VBoxContainer _rightColumn = null!;

    /// <summary>
    /// A stack of panels down one side of the screen, which is what stops them overlapping.
    /// </summary>
    /// <remarks>
    /// <b>Pinned at the top and grown downward, never stretched to the full height.</b> A
    /// column that filled the window would put an invisible mouse-blocker over the whole side
    /// of the map; sized to its contents, it is exactly as tall as the panels in it and the
    /// valley below them stays clickable.
    /// </remarks>
    private VBoxContainer Column(Corner corner)
    {
        var column = new VBoxContainer { MouseFilter = MouseFilterEnum.Ignore };
        column.AddThemeConstantOverride("separation", Edge);

        bool right = corner is Corner.TopRight or Corner.BottomRight;
        column.AnchorLeft = column.AnchorRight = right ? 1f : 0f;
        column.AnchorTop = column.AnchorBottom = 0f;
        column.OffsetTop = Edge;
        column.OffsetBottom = Edge;
        column.OffsetLeft = right ? -(Edge + ColumnWidth) : Edge;
        column.OffsetRight = right ? -Edge : Edge + ColumnWidth;
        column.GrowHorizontal = right ? GrowDirection.Begin : GrowDirection.End;
        column.GrowVertical = GrowDirection.End;

        AddChild(column);
        return column;
    }

    /// <summary>How wide a column of panels is. One number, so the two sides match.</summary>
    private const int ColumnWidth = 400;

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
    private VBoxContainer InColumn(
        VBoxContainer column, float height, string? title = null, bool startOpen = true)
    {
        var panel = new PanelContainer { MouseFilter = MouseFilterEnum.Stop };
        panel.AddThemeStyleboxOverride("panel", PanelSkin());
        panel.CustomMinimumSize = new Vector2(0, height);

        VBoxContainer contents = Dress(panel, title, startOpen);
        column.AddChild(panel);
        return contents;
    }

    private VBoxContainer Floating(
        float x,
        float y,
        float width,
        float height,
        Corner corner,
        string? title = null,
        bool startOpen = true)
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

        VBoxContainer floated = Dress(panel, title, startOpen);
        AddChild(panel);
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
        var contents = new VBoxContainer();
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

            body.AddChild(header);
            _headers.Add(header);
        }

        body.AddChild(contents);
        _panels.Add(panel);

        // ⚠️ THE PANEL YOU CLICKED ON WINS THE CLICK. Panels are siblings, so the one added
        // last draws — and receives — on top, which made the order they happen to be built in
        // decide whether a control could be pressed at all. That is how a forester's "Give
        // ground" button ended up under the control bar (D11: nothing here is testable, so it
        // took Joe playing it to find).
        //
        // Raised on mouse-enter rather than by fixing a build order, because there is no build
        // order that is right for every selection: whichever panel the pointer is over is the
        // one the player means. A panel inside a column raises its column, since that is what
        // the root actually holds.
        panel.MouseEntered += () =>
        {
            Node top = panel.GetParent() == this ? panel : panel.GetParent();
            MoveChild(top, -1);
        };

        return contents;
    }

    /// <summary>Every floating panel, so one key can put them all away.</summary>
    private readonly List<PanelContainer> _panels = new();

    /// <summary>Every panel header, so one key can roll them all up.</summary>
    private readonly List<Button> _headers = new();

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
    private static StyleBoxFlat PanelSkin()
    {
        var skin = new StyleBoxFlat
        {
            BgColor = new Color(0.08f, 0.09f, 0.10f, 0.72f),
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
    /// <summary>
    /// The stock limits (D62) — how much of each good the village should keep.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Folded away behind a toggle, and that is the design rather than tidiness.</b> The
    /// sim's default is null — <em>let the village decide</em> — and §1.2 says a game that
    /// opens with a number against every good is the spreadsheet game whatever the numbers
    /// say. A player who never opens this drawer plays exactly the village that existed
    /// before it, and there is a golden hash test that says so. Putting three spin boxes on
    /// screen permanently would undo that in the only place it actually matters: what the
    /// player sees on the first frame.
    /// </para>
    /// <para>
    /// <b>Each good gets a "village decides" tick alongside its number</b>, because null and
    /// zero are different instructions — <em>no opinion</em> against <em>stop, I mean it</em>
    /// — and a spin box alone cannot say the first. Conflating them in the UI would make the
    /// sim's careful distinction unreachable from the game.
    /// </para>
    /// <para>
    /// <b>The current stock sits beside the limit</b>, so a stopped workplace explains
    /// itself where the decision was made: <em>200 · have 214</em> is the whole causal chain
    /// in five characters, which is §1.1's test.
    /// </para>
    /// </remarks>
    private VBoxContainer BuildStockLimitMenu()
    {
        var wrapper = new VBoxContainer();
        wrapper.AddThemeConstantOverride("separation", 4);

        // ⚠️ NO TOGGLE OF ITS OWN ANY MORE. It used to be a button that unfolded the control
        // bar over the map; the panel it now lives in collapses by its own title, so a second
        // level of folding would be two clicks to reach a number the player wants in front of
        // them anyway. **A section label, not a control** — the rows are simply there.
        var rows = new VBoxContainer();
        rows.AddThemeConstantOverride("separation", 2);

        wrapper.AddChild(Muted("Stock limits — how much to keep before the work stops"));
        wrapper.AddChild(rows);

        foreach (Goods goods in StockLimits.Kinds)
        {
            rows.AddChild(BuildStockLimitRow(goods));
        }

        return wrapper;
    }

    /// <summary>
    /// Banished's professions panel: how many people on each kind of work, village-wide (D106).
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Joe's ask, and his screenshot is the spec:</b> a row per profession, a number you
    /// set, and <em>"of N"</em> for how many places exist. The per-building control (D104)
    /// answers <em>"how many at THIS hut?"</em>; this answers <em>"how many woodcutters at
    /// all?"</em>, which is the question you actually have an opinion about.
    /// </para>
    /// <para>
    /// <b>Laborers are shown and not set</b>, because they are what is left over — the same
    /// relationship Banished has, and the reason <c>Villager.IsLaborer</c> is a reader rather
    /// than a stored state (D66). Take two hands off gathering and there are two more laborers;
    /// there is nothing else "setting laborers" could mean that is not just that.
    /// </para>
    /// </remarks>
    private VBoxContainer BuildProfessionsMenu()
    {
        var wrapper = new VBoxContainer();
        wrapper.AddThemeConstantOverride("separation", 4);

        // A section label rather than a toggle, for the reason given in BuildStockLimitMenu:
        // the panel this lives in already collapses, and folding inside a fold is two clicks
        // to reach the number the player opened the panel for.
        var rows = new VBoxContainer();
        rows.AddThemeConstantOverride("separation", 2);

        wrapper.AddChild(Muted("Professions — how many people on each kind of work"));
        wrapper.AddChild(rows);

        // Laborers first, as in Joe's screenshot, and as a readout: they are the remainder.
        var laborRow = new HBoxContainer();
        laborRow.AddThemeConstantOverride("separation", 6);
        Label laborName = Muted("Laborer");
        laborName.CustomMinimumSize = new Vector2(90, 0);
        laborRow.AddChild(laborName);
        _laborerReadout = Muted(string.Empty);
        laborRow.AddChild(_laborerReadout);
        laborRow.AddChild(Muted("— whatever is spare: clearing, hauling, tidying"));
        rows.AddChild(laborRow);

        foreach (JobKind kind in JobLimits.Kinds)
        {
            rows.AddChild(BuildProfessionRow(kind));
        }

        return wrapper;
    }

    /// <summary>One profession: a name, a "village decides" tick, a number, and the places.</summary>
    private HBoxContainer BuildProfessionRow(JobKind kind)
    {
        var row = new HBoxContainer();
        row.AddThemeConstantOverride("separation", 6);

        Label name = Muted(ProfessionName(kind));
        name.CustomMinimumSize = new Vector2(90, 0);
        row.AddChild(name);

        var auto = new CheckBox { Text = "village decides", ButtonPressed = true };
        var amount = new SpinBox
        {
            MinValue = 0,
            MaxValue = 999,
            Step = 1,
            Value = 0,
            Editable = false,
            CustomMinimumSize = new Vector2(90, 0),
        };

        Label places = Muted(string.Empty);
        places.CustomMinimumSize = new Vector2(150, 0);

        void Apply()
        {
            amount.Editable = !auto.ButtonPressed;
            int? target = auto.ButtonPressed ? null : (int)amount.Value;

            PlacementVerdict verdict = _loop.World.SetJobLimit(kind, target);
            if (verdict.HasWarning)
            {
                _placementLabel.Text = verdict.Warning;
                _placementLabel.Visible = true;
            }
        }

        auto.Toggled += _ => Apply();
        amount.ValueChanged += _ => Apply();

        row.AddChild(auto);
        row.AddChild(amount);
        row.AddChild(places);

        _professionReadouts.Add((kind, places));
        return row;
    }

    private static string ProfessionName(JobKind kind) => kind switch
    {
        JobKind.Forager => "Gatherer",
        JobKind.Forester => "Forester",
        JobKind.Woodcutter => "Woodcutter",
        JobKind.Marketer => "Vendor",
        JobKind.Builder => "Builder",
        _ => kind.ToString(),
    };

    /// <summary>One good's limit: a name, a "village decides" tick, a number, and the stock.</summary>
    private HBoxContainer BuildStockLimitRow(Goods goods)
    {
        var row = new HBoxContainer();
        row.AddThemeConstantOverride("separation", 6);

        Label name = Muted(GoodsName(goods));
        name.CustomMinimumSize = new Vector2(74, 0);
        row.AddChild(name);

        var auto = new CheckBox { Text = "village decides", ButtonPressed = true };
        var amount = new SpinBox
        {
            MinValue = 0,
            MaxValue = 100_000,
            Step = 10,
            Value = 200,
            Editable = false,
            CustomMinimumSize = new Vector2(110, 0),
        };

        Label held = Muted(string.Empty);
        held.CustomMinimumSize = new Vector2(90, 0);

        void Apply()
        {
            amount.Editable = !auto.ButtonPressed;
            int? limit = auto.ButtonPressed ? null : (int)amount.Value;

            PlacementVerdict verdict = _loop.World.SetStockLimit(goods, limit);
            if (verdict.HasWarning)
            {
                // The sim's own sentence, in the channel that already carries the sim's own
                // sentences (D43's placement warnings). One voice, not two.
                _placementLabel.Text = verdict.Warning;
                _placementLabel.Visible = true;
            }
        }

        auto.Toggled += _ => Apply();
        amount.ValueChanged += _ => Apply();

        row.AddChild(auto);
        row.AddChild(amount);
        row.AddChild(held);

        _stockLimitReadouts.Add((goods, held));
        return row;
    }

    /// <summary>The "have N" labels, refreshed with everything else.</summary>
    private readonly List<(Goods Goods, Label Held)> _stockLimitReadouts = new();
    private readonly List<(JobKind Kind, Label Places)> _professionReadouts = new();
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
                seats += workplace.Capacity;
            }
        }

        return seats;
    }

    /// <summary>A villager's trade, for the roster. What they are, not where they are.</summary>
    private string TradeOf(Villager villager)
    {
        Workplace? job = _loop.World.FindWorkplace(villager.WorkplaceId);
        return job?.Kind switch
        {
            JobKind.Forager => "forager",
            JobKind.Forester => "forester",
            JobKind.Woodcutter => "woodcutter",
            JobKind.Marketer => "marketer",
            JobKind.Builder => "builder",
            _ => "working",
        };
    }

    private static string GoodsName(Goods goods) => goods switch
    {
        Goods.Food => "Food",
        Goods.Logs => "Logs",
        Goods.Firewood => "Firewood",
        _ => goods.ToString(),
    };

    /// <summary>
    /// How much of a good the limit is actually measured against.
    /// </summary>
    /// <remarks>
    /// <b>The same total <c>LabourQuota</c> reads, and it must stay that way.</b> Firewood is
    /// counted in the sheds, not everywhere, because a pile in somebody else's home is not
    /// supply — no errand reaches it. Showing the player a village-wide total beside a limit
    /// that governs the shed would explain a stopped woodcutter with a number that had
    /// nothing to do with why it stopped, which is D29 wearing a UI.
    /// </remarks>
    private static int HeldFor(SimWorld world, Goods goods) => goods switch
    {
        Goods.Food => world.FoodInGranaries(),
        Goods.Logs => world.LogsInSheds(),
        Goods.Firewood => world.FirewoodInSheds(),
        _ => 0,
    };

    /// <summary>
    /// What the village should take off the map — modes of one tool (D87, D92).
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Its own row, and that is not cosmetic.</b> Six more buttons on the build row
    /// pushed it past the window and clipped "Market", "Woodcutter" and "Demolish" off the
    /// left edge — buttons that exist and cannot be pressed, which is D55's finding
    /// arriving again from the other direction. Caught by taking a screenshot, which is the
    /// only verification the view has (D11).
    /// </para>
    /// <para>
    /// Ordered the way a player reaches for them: trees are the opening's timber, stone and
    /// iron are what a building past a log hut will cost, and <em>All</em> is the
    /// clear-this-area brush D67 asked for.
    /// </para>
    /// </remarks>
    private HBoxContainer BuildHarvestMenu()
    {
        var row = new HBoxContainer();
        row.AddChild(Muted("Harvest:"));

        foreach ((string Label, HarvestBrush Mode) entry in new[]
        {
            ("Trees", HarvestBrush.Trees),
            ("Stone", HarvestBrush.Stone),
            ("Iron", HarvestBrush.Iron),
            ("All", HarvestBrush.Everything),
        })
        {
            HarvestBrush captured = entry.Mode;
            var button = new Button { Text = entry.Label };
            button.Pressed += () => _map.BeginHarvesting(captured, 1);
            row.AddChild(button);
        }

        var unmark = new Button { Text = "Unmark" };
        unmark.Pressed += () => _map.BeginHarvesting(HarvestBrush.Everything, -1);
        row.AddChild(unmark);

        row.AddChild(Muted("— painted ground is felled or dug by whoever is spare"));
        return row;
    }

    private HBoxContainer BuildBuildMenu()
    {
        var row = new HBoxContainer();
        row.AddThemeConstantOverride("separation", 8);

        row.AddChild(Muted("Build:"));

        // First in the row because it is first in the game (D76): a pile costs nothing but
        // the ground, and a village with nowhere to put things cannot begin.
        row.AddChild(BuildButton("Storage pile", BuildingKind.Pile));

        // Second, because it is second in the game (D108): nothing else on this row is ever
        // raised without it. Free and instant like the pile, and beside it for that reason —
        // the two buildings that cost nothing but the ground they stand on are the two the
        // player puts down before anything can begin.
        row.AddChild(BuildButton("Builder's hut", BuildingKind.BuilderHut));
        row.AddChild(BuildButton("Gatherer", BuildingKind.GathererHut));
        row.AddChild(BuildButton("Forester", BuildingKind.ForesterHut));
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

        // ⭐ STAFFING USED TO BE ON THIS ROW AND HAS MOVED TO THE BUILDING PANEL (Joe).
        // The old note said it deliberately never came and went, "because a button the
        // player has to hunt for" is worse — and D93 then recorded Joe's verdict on the
        // result: "the staffing control is in a weird place right now". Both things were
        // true. A control that is always there but never says WHAT it acts on is the
        // thing you hunt for; beside the name of the building, it needs no explaining.

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

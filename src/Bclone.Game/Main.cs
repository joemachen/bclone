using System;
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

    private SimLoop _loop = null!;
    private FixedTimestepDriver _driver = null!;
    private InMemoryLogSink _sink = null!;

    /// <summary>The full audit trail on disk — everything down to DEBUG.</summary>
    private FileLogSink _audit = null!;
    private string _logPath = string.Empty;

    private string _configSource = string.Empty;

    private Label _clockLabel = null!;
    private Label _villageLabel = null!;
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
    private HBoxContainer _storeRow = null!;
    private HBoxContainer _acceptRow = null!;
    private Button _fullMarkerButton = null!;
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
        // shed, not the first of each (D38) — a village that has built a second one should
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

            if (goods == Goods.Food)
            {
                int elsewhere = world.TotalFood() - inStores;
                held.Text = elsewhere > 0
                    ? $"{inStores}  (+{elsewhere} in homes and huts)"
                    : $"{inStores}";
                continue;
            }

            // ⭐ AND WHAT IS LYING IN THE YARD (D134). A valley has one timber store, it fills,
            // and everything hauled in after that is set down outside it — measured at 320 logs
            // in store against 5,977 on the ground. Reading "Logs 320" while a mountain sits in
            // the open is the village lying to the player about a shortage it does not have.
            int inHeaps = world.OnTheGround(goods);
            held.Text = inHeaps > 0
                ? $"{inStores}  (+{inHeaps} on the ground — no room in store)"
                : $"{inStores}";
        }

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
        LabourQuota quota = LabourQuota.For(world);
        for (int i = 0; i < _professionReadouts.Count; i++)
        {
            (JobKind kind, Label places) = _professionReadouts[i];
            places.Text = $"{WorkingAt(world, kind)} of {SeatsFor(world, kind)} — "
                + $"village wants {quota.For(kind)}";
        }

        _laborerReadout.Text = $"{world.Laborers}";

        // The two standing alerts used to be composed here every frame and shown in the
        // overview. They are narrated by the sim on their edges now and read in the village
        // log like everything else that happens (Joe) — so the view no longer asks the
        // question at all, which also takes a `LabourQuota.For` off every single frame.

        RefreshRoster(world);
        RefreshInspector(world);
        AppendNewLogLines();

        // After the panels have been filled, because how tall a column wants to be depends on
        // what was just put in it — an alert that grew to six lines this tick included.
        FitColumns();

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
        _leftColumn = Column(Corner.TopLeft);
        _rightColumn = Column(Corner.TopRight);

        BuildStatusPanel();
        BuildVillageOrdersPanel();
        BuildRosterPanel();

        // Top of the right-hand column, which is where Banished puts it and where Joe's
        // screenshot has it — above the log, so the two things you glance at are together.
        BuildMinimapPanel();
        BuildLogPanel();
        BuildInspectorPanel();

        // Last of the panels, because it lists the ones built before it.
        BuildSettingsPanel();
        BuildControlPanel();

        SetSpeed(1.0);

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
    /// Room kept clear along the bottom for the controls, which are wider than the window.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The build menu is a long row and the roster is pinned to the same corner of the
    /// screen, so the two overlapped and the controls drew straight over the names. A
    /// measured constant rather than a computed one because the controls' height is settled
    /// by their own contents, and asking a container for its size during layout is how
    /// circular dependencies start.
    /// </para>
    /// <para>
    /// <b>It is the columns' bottom edge now</b>, not just a gap the roster stood off by —
    /// which is what makes "no panel is ever pushed off the screen" a property of the layout
    /// rather than of how much anybody happens to have opened.
    /// </para>
    /// </remarks>
    private const int ControlsReserve = 160;

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
        VBoxContainer body = InColumn(_leftColumn, 0, "Overview");

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
        _seedLabel = Wrapped(Muted(
            $"seed {_loop.World.Seed}   ·   config: {_configSource}   ·   log: {_logPath}"));
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

        foreach (Goods goods in Enum.GetValues<Goods>())
        {
            table.AddChild(Chip(ChipColour(goods)));
            table.AddChild(Body(GoodsName(goods)));

            Label held = Body(string.Empty);
            held.HorizontalAlignment = HorizontalAlignment.Right;
            held.SizeFlagsHorizontal = SizeFlags.ExpandFill;
            table.AddChild(held);

            _goodsReadouts.Add((goods, held));
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

    /// <summary>
    /// What colour a good reads as. <b>Borrowed from the map where the map has one.</b>
    /// </summary>
    /// <remarks>
    /// Logs and firewood are the timber colours, stone and iron the seam colours, so a chip
    /// in this panel and a tile in the valley mean the same thing — which is the only reason
    /// to colour them at all. Food has no tile of its own since the thickets started being
    /// forest, so it takes the berry colour it had.
    /// </remarks>
    private static Color ChipColour(Goods goods) => goods switch
    {
        Goods.Food => new Color(0.82f, 0.35f, 0.38f),
        Goods.Logs => new Color(0.45f, 0.33f, 0.20f),
        Goods.Firewood => new Color(0.88f, 0.55f, 0.24f),
        Goods.Stone => new Color(0.62f, 0.62f, 0.64f),
        Goods.Tools => new Color(0.72f, 0.76f, 0.82f),
        Goods.Iron => new Color(0.55f, 0.36f, 0.30f),
        _ => new Color(1, 1, 1, 0.4f),
    };

    /// <summary>Every goods row's amount label, so the tick can fill them in.</summary>
    private readonly List<(Goods Goods, Label Held)> _goodsReadouts = new();

    /// <summary>The whole valley, small, with a box round what you are looking at.</summary>
    /// <remarks>
    /// <b>An ordinary panel</b>, so it collapses with <c>c</c>, hides with <c>h</c> and turns
    /// off from Settings like everything else — the alternative was a control anchored to a
    /// corner of its own, which is precisely the second panel mechanism D113 spent a session
    /// arguing out of existence.
    /// </remarks>
    private void BuildMinimapPanel()
    {
        VBoxContainer body = InColumn(_rightColumn, 0, "The valley");

        _minimap = new Minimap();
        _minimap.LookAt += tile => _map.CentreOn(tile);
        body.AddChild(_minimap);
    }

    private Minimap _minimap = null!;

    /// <summary>The story so far — Banished's event log, in much the same corner.</summary>
    private void BuildLogPanel()
    {
        VBoxContainer body = InColumn(_rightColumn, ListHeight, "Village log");

        _villageLog = new RichTextLabel
        {
            ScrollFollowing = true,
            BbcodeEnabled = false,
            SizeFlagsVertical = SizeFlags.ExpandFill,
        };

        // A RichTextLabel does not go through `Body`, so it kept Godot's 16 while everything
        // around it shrank — which is exactly the panel Joe named first.
        _villageLog.AddThemeFontSizeOverride("normal_font_size", RowSize);
        body.AddChild(_villageLog);
    }

    /// <summary>Everyone alive, and what they are doing about it.</summary>
    private void BuildRosterPanel()
    {
        VBoxContainer body = InColumn(_leftColumn, ListHeight, "The village");

        _roster = new ItemList { SizeFlagsVertical = SizeFlags.ExpandFill };
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

        // ⭐ THE PER-BUILDING HALF OF THE FULL-STORE MARKER (Joe, D140): *"visibility of which
        // should be able to be disabled by building or globally."* Beside the store's own name
        // for D104's reason — a control that belongs to ONE building has to sit next to that
        // building, or the player has to remember which one it will act on.
        _storeRow = new HBoxContainer { Visible = false };
        _storeRow.AddThemeConstantOverride("separation", 6);
        body.AddChild(_storeRow);

        _storeRow.AddChild(Muted("When full:"));

        _fullMarkerButton = new Button { Text = "Marker: ON" };
        _fullMarkerButton.Pressed += ToggleSelectedFullMarker;
        _storeRow.AddChild(_fullMarkerButton);

        // ⭐ WHAT THIS BUILDING WILL TAKE (Joe, D141): *"a given storage pile will only accept
        // logs, another only firewood, another only iron ore. Set at the building level."*
        //
        // One button per good, built once and shown or hidden by what the KIND can hold — so a
        // granary offers "food" and nothing else, and the player is never presented with a
        // choice the model would refuse. The refusal still exists in `SetStoreAccepts`, because
        // a control that cannot be misused and a rule that cannot be broken are different
        // things and only the second one survives somebody calling it from elsewhere.
        _acceptRow = new HBoxContainer { Visible = false };
        _acceptRow.AddThemeConstantOverride("separation", 6);
        body.AddChild(_acceptRow);

        _acceptRow.AddChild(Muted("Takes:"));

        for (int g = 0; g < Stockpile.Kinds; g++)
        {
            var goods = (Goods)g;
            var button = new Button { Text = GoodsName(goods), ToggleMode = true };
            button.Pressed += () => ToggleSelectedAccepts(goods);
            _acceptRow.AddChild(button);
            _acceptButtons.Add((goods, button));
        }
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

    // Six hand-worked panel sizes used to live here and just above — a width and a height for
    // the status panel, the log, the roster, the inspector and this one. Every last one was
    // dead: since the columns arrived nothing reads a panel's position or size but the column
    // it is in. Deleted rather than left, on D98's rule that a number nothing reads is a lie
    // waiting to be found — and these were the exact numbers whose hand-tuning D113 replaced.

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

        // ⭐ THE WAY BACK TO EVERY WINDOW YOU SWITCHED OFF (Joe). It lives on the control bar
        // rather than in a panel, because the control bar is the one thing that is always
        // there — a settings menu reachable only from a window you might have hidden would be
        // a door that locks behind you.
        var settings = new Button { Text = "Settings" };
        settings.Pressed += () => _settingsPanel.Visible = !_settingsPanel.Visible;
        controls.AddChild(settings);

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
    /// <para>
    /// <b>⚠️ IT HAS A DEFINITE HEIGHT NOW, and that is Joe's <em>"when all panels are open,
    /// the bottom panels go off screen."</em></b> It used to be pinned at the top and grown
    /// downward, sized to its contents — which meant the contents decided how tall it was, and
    /// once the overview grew a goods table there were more contents than window. A panel you
    /// cannot see is the same bug as a button you cannot press (D113), arriving from the other
    /// direction.
    /// </para>
    /// <para>
    /// So the column runs from the top edge to just above the control bar, and the panels
    /// inside it share <em>that</em>. The two list panels absorb the slack (see
    /// <see cref="InColumn"/>), so there is always somewhere for the leftover height to go and
    /// never a panel pushed past the bottom.
    /// </para>
    /// <para>
    /// <b>Still not a mouse-blocker</b>, which was the original reason it was sized to its
    /// contents: the filter is <c>Ignore</c>, so the column itself never takes a click and the
    /// valley showing between the panels stays clickable. Only the panels stop clicks.
    /// </para>
    /// </remarks>
    private VBoxContainer Column(Corner corner)
    {
        var scroll = new ScrollContainer
        {
            HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled,
            VerticalScrollMode = ScrollContainer.ScrollMode.Auto,
            FollowFocus = false,
        };

        bool right = corner is Corner.TopRight or Corner.BottomRight;
        scroll.AnchorLeft = scroll.AnchorRight = right ? 1f : 0f;
        scroll.AnchorTop = scroll.AnchorBottom = 0f;
        scroll.OffsetTop = Edge;
        scroll.OffsetBottom = Edge;
        scroll.OffsetLeft = right ? -(Edge + ColumnWidth) : Edge;
        scroll.OffsetRight = right ? -Edge : Edge + ColumnWidth;
        scroll.GrowHorizontal = right ? GrowDirection.Begin : GrowDirection.End;
        scroll.GrowVertical = GrowDirection.End;

        var column = new VBoxContainer { MouseFilter = MouseFilterEnum.Ignore };
        column.AddThemeConstantOverride("separation", Edge);
        column.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        scroll.AddChild(column);

        AddChild(scroll);
        _columns.Add((scroll, column));
        return column;
    }

    /// <summary>Each side's scroller and the stack of panels inside it.</summary>
    private readonly List<(ScrollContainer Scroll, VBoxContainer Column)> _columns = new();

    /// <summary>
    /// Keep each column as tall as its panels, and never taller than the screen.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>⭐ Joe: <em>"when all panels are open, the bottom panels go off screen."</em></b> The
    /// column was sized to its contents and grown downward, so the contents decided how tall
    /// it was — and once there were more contents than window, the panels at the bottom were
    /// simply not on the screen. A panel you cannot see is the same bug as a button you cannot
    /// press (D113), arriving from the other end.
    /// </para>
    /// <para>
    /// <b>Sized to the panels, capped at the room available</b>, and the cap is the whole
    /// design: below the cap the column is exactly as tall as what is in it, so the valley
    /// underneath stays clickable and nothing has changed. At the cap it scrolls — which costs
    /// the clicks under the column, but only in the state where the panels were covering that
    /// strip anyway. <b>Nothing the player opens can become unreachable.</b>
    /// </para>
    /// <para>
    /// Recomputed every frame rather than on a signal, because every one of the things that
    /// changes it — folding a panel, switching one off in Settings, an alert growing by three
    /// lines, resizing the window — would otherwise need its own hook, and the one that got
    /// forgotten would be the bug. It is two container measurements a frame.
    /// </para>
    /// </remarks>
    private void FitColumns()
    {
        float room = Size.Y - Edge - (Edge + ControlsReserve);

        foreach ((ScrollContainer scroll, VBoxContainer column) in _columns)
        {
            float wanted = column.GetCombinedMinimumSize().Y;
            bool overflowing = wanted > room;

            scroll.CustomMinimumSize = new Vector2(0, Mathf.Min(wanted, Mathf.Max(0f, room)));

            // ⚠️ AND IT ONLY TAKES THE MOUSE WHEN IT HAS SOMETHING TO DO WITH IT. A
            // ScrollContainer stops clicks, and this one covers the whole column — so the
            // gaps *between* panels, which were click-through when the column was a plain
            // box, started swallowing clicks meant for the valley behind them. A brush that
            // does nothing because you happened to click in a fourteen-pixel gap is
            // indistinguishable from a broken brush.
            //
            // Ignored while everything fits (the panels still stop their own clicks, which
            // is all that was ever wanted), and Stop only when there is genuinely something
            // to scroll.
            scroll.MouseFilter = overflowing ? MouseFilterEnum.Stop : MouseFilterEnum.Ignore;
        }
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

        VBoxContainer contents = Dress(panel, title, startOpen);

        // ⚠️ THE HEIGHT BELONGS TO THE CONTENTS, NOT TO THE PANEL — and putting it on the
        // panel is why "The village" and "Village log" **did not appear to fold at all**
        // (Joe, playing). Folding hides the contents box; a minimum height on the panel
        // outlives it, so both panels rolled up into a title strip with 280 and 210 pixels
        // of empty bordered nothing hanging below it. The panel was folded and looked broken.
        //
        // On the contents, the height goes away with them, which is what "fold" means.
        contents.CustomMinimumSize = new Vector2(0, height);

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

            body.AddChild(header);
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
        VBoxContainer body = InColumn(_rightColumn, 0, "Settings");
        _settingsPanel = _panels[^1];
        _settingsPanel.Visible = false;

        // Dropped from the list of switchable windows for the reason above, and from the
        // headers list so `c` cannot fold the thing you opened to un-fold something else.
        _windows.RemoveAt(_windows.Count - 1);
        _headers.RemoveAt(_headers.Count - 1);

        body.AddChild(Muted("Windows — what is on screen"));

        foreach ((string name, PanelContainer panel) in _windows)
        {
            var shown = new CheckBox { Text = name, ButtonPressed = true };
            shown.AddThemeFontSizeOverride("font_size", 12);
            shown.Toggled += on => panel.Visible = on;
            body.AddChild(shown);
        }

        body.AddChild(Muted("c folds every panel · h hides the lot"));

        // ⭐ THE GLOBAL HALF OF THE FULL-STORE MARKER (Joe, D140): *"visibility of which should
        // be able to be disabled by building or globally."* The per-building half lives on the
        // building's own panel, which is where you are already standing when one store is the
        // one annoying you.
        body.AddChild(Muted("On the map"));

        var markers = new CheckBox { Text = "mark stores with no room", ButtonPressed = true };
        markers.AddThemeFontSizeOverride("font_size", 12);
        markers.Toggled += on => _map.ShowFullMarkers(on);
        body.AddChild(markers);
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
    // ⚠️ Genuinely null until the control bar is built, and typed to say so. It was `null!`,
    // which promised it was always there and cost a crash the first time a panel warned
    // during construction — see `Warn`.
    private Label? _placementLabel;

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

        // ⛔ AND THE ONES THAT DO NOT EXIST, greyed and with a reason each — the same rule the
        // overview's goods table follows, for the same reason. A player who cannot see that
        // fishing is coming has no way to tell it apart from fishing being absent on purpose.
        // Behind a fold, because eleven rows that never change were half of why this panel
        // pushed the roster off the bottom of the screen.
        VBoxContainer inside = Foldaway(
            $"Not hired yet — {ProfessionsNotYetHired.Length} more, and why",
            out VBoxContainer roadmap);

        foreach ((string name, string reason) in ProfessionsNotYetHired)
        {
            var row = new HBoxContainer();
            row.AddThemeConstantOverride("separation", 6);

            Label label = Muted(name);
            label.CustomMinimumSize = new Vector2(ProfessionNameWidth, 0);
            label.Modulate = new Color(1, 1, 1, 0.3f);
            row.AddChild(label);

            Label why = Muted(reason);
            why.Modulate = new Color(1, 1, 1, 0.3f);
            row.AddChild(why);

            inside.AddChild(row);
        }

        wrapper.AddChild(roadmap);
        return wrapper;
    }

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
        ("Fisherman", "no fishing hut — needs building beside water"),
        ("Hunter", "no lodge, and leather is not a good yet"),
        ("Tailor", "waiting on the hunter for leather"),
        ("Farmer", "no fields, no crops"),
        ("Herdsman", "no livestock"),
        ("Miner", "iron is on the map; nothing digs it"),
        ("Stonecutter", "stone is on the map; nothing quarries it"),
        ("Blacksmith", "tools cannot be made, only brought"),
        ("Brewer", "no barley"),
        ("Teacher", "no school"),
        ("Physician", "illness is not modelled"),
    };

    /// <summary>One column width for every profession name, real or promised.</summary>
    private const int ProfessionNameWidth = 84;

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

    /// <summary>
    /// One profession: a name, <b>− N +</b>, and how many places there are.
    /// </summary>
    /// <remarks>
    /// <b>± buttons rather than a spin box</b> — the same control the per-building staffing row
    /// uses (D104), because they set the same number from two ends (`professions.md §3.0`) and
    /// two different widgets for one quantity is how a player comes to believe they are two
    /// quantities. It is also narrower, which is what Joe asked the whole panel for.
    /// </remarks>
    private HBoxContainer BuildProfessionRow(JobKind kind)
    {
        var row = new HBoxContainer();
        row.AddThemeConstantOverride("separation", 6);

        Label name = Muted(ProfessionName(kind));
        name.CustomMinimumSize = new Vector2(ProfessionNameWidth, 0);
        row.AddChild(name);

        // ⭐ THE "VILLAGE DECIDES" TICK IS GONE (Joe, 2026-08-11: *"remove all of the village
        // decides from the jobs. we want it user-decided only for now."*)
        //
        // It expressed the difference between *no opinion* (null) and *none, I mean it*
        // (zero), which D106 was careful to keep apart and the sim still does. **What has
        // changed is that the player is never without an opinion**: every profession now
        // carries an explicit number from the first frame, so there is one source of truth
        // for who is working rather than two that argue — which is D109's whole argument,
        // arriving through the panel rather than through the sim.
        //
        // ⭐ SEEDED AT NOUGHT — EVERYBODY STARTS A LABORER (Joe, D136). *"By default 4
        // villagers are set as gatherer profession. The default should be laborers."*
        //
        // ⚠️ The comment this replaces argued the opposite and was right at the time: seeding
        // from `LabourQuota.For(world).For(kind)` meant *"nothing moves until somebody moves
        // it, and the first thing the player sees is what the village was already doing."*
        // That reasoning belonged to a village that decided its own staffing. Since D109 the
        // player always has an opinion and the quota no longer overrules one, so seeding from
        // the quota is not a neutral starting point — it is the game silently making the
        // first decision and attributing it to the player.
        //
        // A laborer is not an unemployed villager (§3.1): they clear painted ground, haul
        // heaps and tidy. So an unstaffed founding is four people doing the work that is on
        // the map, which is the honest opening — the player says what the village becomes.
        int asked = 0;

        Label amount = Muted("0");
        amount.CustomMinimumSize = new Vector2(22, 0);
        amount.HorizontalAlignment = HorizontalAlignment.Right;

        var fewer = new Button { Text = "−", Flat = true };
        var more = new Button { Text = "+", Flat = true };

        void Apply()
        {
            amount.Text = $"{asked}";
            Warn(_loop.World.SetJobLimit(kind, asked));
        }

        // Clamped at zero, and with no ceiling of its own: the panel is allowed to ask for
        // more than the village would choose — that is the whole difference from a stock
        // limit (D106) — and the sim says out loud when it cannot honour the number.
        fewer.Pressed += () =>
        {
            asked = System.Math.Max(0, asked - 1);
            Apply();
        };

        more.Pressed += () =>
        {
            asked++;
            Apply();
        };

        Label places = Muted(string.Empty);
        places.SizeFlagsHorizontal = SizeFlags.ExpandFill;

        row.AddChild(fewer);
        row.AddChild(amount);
        row.AddChild(more);
        row.AddChild(places);

        Apply();

        _professionReadouts.Add((kind, places));
        return row;
    }

    /// <summary>What a kind of work is called on screen. Every value named (D108).</summary>
    private static string ProfessionName(JobKind kind) => kind switch
    {
        JobKind.Forager => "Gatherer",
        JobKind.Forester => "Forester",
        JobKind.Woodcutter => "Woodcutter",
        JobKind.Marketer => "Vendor",
        JobKind.Builder => "Builder",
        _ => throw new ArgumentOutOfRangeException(
            nameof(kind), kind, "That kind of work has no name on screen."),
    };

    /// <summary>One good's limit: a name, a "village decides" tick, a number, and the stock.</summary>
    private HBoxContainer BuildStockLimitRow(Goods goods)
    {
        var row = new HBoxContainer();
        row.AddThemeConstantOverride("separation", 6);

        Label name = Muted(GoodsName(goods));
        name.CustomMinimumSize = new Vector2(74, 0);
        row.AddChild(name);

        // ⭐ NO "VILLAGE DECIDES" TICK (Joe, D136): *"remove 'village decides' for stock
        // limits. We'll revisit adding that later."* The same call he made for the professions,
        // one panel along, and for the same reason — since D109 the player always has an
        // opinion, so a control that hands the decision back is a second voice arguing with
        // the first.
        //
        // ⚠️ "NO LIMIT" IS STILL REACHABLE, AND IT HAD TO BE. `null` is not the village
        // deciding — it is *nobody has said*, which is the state every good starts in and the
        // only one that means "do not cap this at all". Deleting the tick without replacing it
        // would have forced a number onto every good at startup, and **a Food row defaulting to
        // 200 would cap a granary that needs thousands** — the village quietly starved by a
        // control the player never touched. So the tick becomes a button that clears.
        var amount = new SpinBox
        {
            MinValue = 0,
            MaxValue = 100_000,
            Step = 10,
            Value = 200,
            Editable = true,
            CustomMinimumSize = new Vector2(110, 0),
        };

        // "Clear" rather than "no limit", because the label beside it now REPORTS whether
        // there is a limit and a button must not read like a state (D139).
        var clear = new Button { Text = "clear", Flat = true, Disabled = true };

        Label held = Muted(string.Empty);
        held.CustomMinimumSize = new Vector2(190, 0);

        void Set(int? limit)
        {
            clear.Disabled = limit is null;

            // The sim's own sentence, in the channel that already carries the sim's own
            // sentences (D43's placement warnings). One voice, not two — and through `Warn`,
            // so this cannot be the next thing to fire before the control bar exists.
            Warn(_loop.World.SetStockLimit(goods, limit));
        }

        amount.ValueChanged += _ => Set((int)amount.Value);
        clear.Pressed += () => Set(null);

        row.AddChild(amount);
        row.AddChild(clear);
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

    /// <summary>What a good is called on screen.</summary>
    /// <remarks>
    /// <b>Every value named, and the default throws</b> (D108's rule). This used to fall back
    /// to <c>goods.ToString()</c>, which was harmless while three goods had limits and became
    /// load-bearing the moment the overview listed all six — a new good would have shown up
    /// under its enum spelling, which is the version of a name nobody chose.
    /// </remarks>
    private static string GoodsName(Goods goods) => goods switch
    {
        Goods.Food => "Food",
        Goods.Logs => "Logs",
        Goods.Firewood => "Firewood",
        Goods.Stone => "Stone",
        Goods.Tools => "Tools",
        Goods.Iron => "Iron",
        _ => throw new ArgumentOutOfRangeException(
            nameof(goods), goods, "That good has no name on screen."),
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

    /// <summary>
    /// Everything the player can put on the map, <b>in categories</b> (Joe's area 4).
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>The category caption sits ABOVE its buttons, not beside them</b>, and that is a
    /// width decision rather than a taste one. This row already reached within thirty pixels
    /// of the window edge, and D55 records what happens when it goes past: <em>"Market",
    /// "Woodcutter" and "Demolish" clipped off the left edge — buttons that exist and cannot
    /// be pressed.</em> Six captions beside their groups would have cost about four hundred
    /// pixels the row has not got; above them they cost one line of height and nothing else.
    /// </para>
    /// <para>
    /// <b>Grouping when a group holds one thing is the point, not an accident.</b> Food is a
    /// single hut today and Works is a single hut; captioning them anyway is what makes it
    /// visible that a village has one way to feed itself, which a flat row of eight buttons
    /// never said.
    /// </para>
    /// </remarks>
    private HBoxContainer BuildBuildMenu()
    {
        var row = new HBoxContainer();
        row.AddThemeConstantOverride("separation", 10);

        row.AddChild(Muted("Build:"));

        // Works first, because it is first in the game (D108): nothing anywhere on this row is
        // ever raised without a builder's hut. The group will hold roads, bridges and fences
        // when the builder gets them (`professions.md §4`).
        row.AddChild(Category("Works", BuildButton("Builder's hut", BuildingKind.BuilderHut)));

        row.AddChild(Category("Food", BuildButton("Gatherer", BuildingKind.GathererHut)));

        row.AddChild(Category(
            "Resources",
            BuildButton("Forester", BuildingKind.ForesterHut),
            BuildButton("Woodcutter", BuildingKind.WoodcutterHut)));

        // The pile leads its group because it leads the game (D76): it costs nothing but the
        // ground, and a village with nowhere to put things cannot begin.
        row.AddChild(Category(
            "Storage & trade",
            BuildButton("Storage pile", BuildingKind.Pile),
            BuildButton("Granary", BuildingKind.Granary),
            BuildButton("Shed", BuildingKind.Shed),
            BuildButton("Market", BuildingKind.Market)));

        // The brush (D42). Its own category because it is a different kind of decision: the
        // others place one thing, this says where a whole neighbourhood may grow — and the
        // village decides which tiles, and when, and whether at all.
        var paint = new Button { Text = "Paint land" };
        paint.Pressed += () => _map.BeginPainting(1);

        var erase = new Button { Text = "Take back" };
        erase.Pressed += () => _map.BeginPainting(-1);

        row.AddChild(Category("Homes", paint, erase));

        var demolish = new Button { Text = "Demolish" };
        demolish.Pressed += () => _map.BeginDemolishing();
        row.AddChild(Category("Removal", demolish));

        row.AddChild(new VSeparator());

        // Uncaptioned and on the end, because it belongs to no category — it puts down
        // whichever tool is in your hand, including the harvest brushes on the row below.
        // Bottom-aligned so it lines up with the buttons rather than with the captions.
        var stop = new Button { Text = "Cancel", SizeFlagsVertical = SizeFlags.ShrinkEnd };
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

    /// <summary>One captioned group of build buttons.</summary>
    private static VBoxContainer Category(string caption, params Button[] buttons)
    {
        var group = new VBoxContainer();
        group.AddThemeConstantOverride("separation", 1);
        group.AddChild(Muted(caption));

        var line = new HBoxContainer();
        line.AddThemeConstantOverride("separation", 4);
        foreach (Button button in buttons)
        {
            line.AddChild(button);
        }

        group.AddChild(line);
        return group;
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

    private static Label Muted(string text)
    {
        var label = new Label { Text = text };
        label.AddThemeFontSizeOverride("font_size", 12);
        label.Modulate = new Color(1, 1, 1, 0.55f);
        return label;
    }
}

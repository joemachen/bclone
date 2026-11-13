using Bclone.Sim.Core;
using Bclone.Sim.World;
using Godot;

namespace Bclone.Game;

/// <summary>
/// ⭐ The <i>Build queue</i> window: every site in the queue, at a glance, with ▲ and ▼ (D730, Joe: <i>"add a 'build
/// queue' panel that shows everything in the build queue at a glance with increase/decrease priority buttons. this panel
/// should be off by default in the settings."</i>). <c>specs/build-queue-panel.md</c>.
/// </summary>
public partial class Main
{
    /// <summary>Wide enough for the materials of a two-material site and a name of a dozen letters; a longer one trims.</summary>
    private const float BuildQueueWidth = 400f;

    /// <summary>The most the rows stand before they scroll: about ten rows, logical px.</summary>
    private const float BuildQueueHeight = 260f;

    private const int BuildQueuePlaceWidth = 22;
    private const int BuildQueueMaterialsWidth = 150;
    private const int BuildQueueWorkWidth = 56;

    /// <summary>The tooltip on a rebuild's ▲ and ▼, and on its neighbours' toward it (D721).</summary>
    private const string RebuildWaits = "A house's rebuild waits behind every other site.";

    private PanelContainer _buildQueuePanel = null!;
    private ScrollContainer _buildQueueScroll = null!;
    private GridContainer _buildQueueGrid = null!;
    private Label _buildQueueEmpty = null!;

    /// <summary>The rows, made once each and refilled or hidden — never rebuilt.</summary>
    private readonly List<BuildQueueRow> _buildQueueRows = new();

    /// <summary>What the rows were last filled from; equal, and nothing a row says can have changed (spec §3).</summary>
    private (SimWorld? World, ulong Tick, int Buildings, int Asked) _buildQueueBuiltFrom;

    /// <summary>
    /// Bumped where the view changes a row between ticks: a ▲ or ▼ here or on a card, a rename (spec §3). Those are the
    /// only callers of <c>MoveInBuildQueue</c> and <c>Rename(Workplace)</c>.
    /// </summary>
    private int _buildQueueAsked;

    /// <summary>One row's controls, and the site they say now.</summary>
    private sealed class BuildQueueRow
    {
        public int SiteId { get; set; }
        public required Label Place { get; init; }
        public required Button Name { get; init; }
        public required Label Materials { get; init; }
        public required Label Work { get; init; }
        public required Button Up { get; init; }
        public required Button Down { get; init; }

        public Control[] Cells => new Control[] { Place, Name, Materials, Work, Up, Down };
    }

    private void BuildTheBuildQueuePanel()
    {
        VBoxContainer body = Floating(
            Edge + DefaultPanelWidth + 16f, Edge, BuildQueueWidth, 0f, Corner.TopLeft, "Build queue", startOpen: true);

        // ⭐ OFF BY DEFAULT (Joe): hidden and unticked at build, as Stock limits is (D380). The Settings tick, the ✕
        // and the remembered state all come from `Floating` registering it.
        _docked.Add((_panels[^1], false));
        _buildQueuePanel = _panels[^1];
        _buildQueuePanel.Visible = false;
        _windows[^1].Wanted = false;

        body.AddChild(Caption("What the builders go to, the top first. ▲ and ▼ move a site one place."));

        _buildQueueEmpty = Muted("Nothing is waiting to be built.");
        body.AddChild(_buildQueueEmpty);

        _buildQueueScroll = new ScrollContainer
        {
            HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled,
            VerticalScrollMode = ScrollContainer.ScrollMode.Auto,
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
        };
        _buildQueueGrid = new GridContainer { Columns = 6, SizeFlagsHorizontal = SizeFlags.ExpandFill };
        _buildQueueGrid.AddThemeConstantOverride("h_separation", 6);
        _buildQueueGrid.AddThemeConstantOverride("v_separation", 2);
        _buildQueueScroll.AddChild(_buildQueueGrid);
        body.AddChild(_buildQueueScroll);

        // The header: the same grid, so the titles sit over their columns.
        foreach (string title in new[] { "#", "Site", "Materials", "Work", string.Empty, string.Empty })
        {
            _buildQueueGrid.AddChild(Muted(title));
        }
    }

    /// <summary>The rows, refilled only while the window is shown and only when what they say has changed (spec §3).</summary>
    private void RefreshBuildQueue(SimWorld world)
    {
        if (!_buildQueuePanel.Visible)
        {
            return;
        }

        var key = (world, world.Tick, world.BuildingGeneration, _buildQueueAsked);
        if (key == _buildQueueBuiltFrom)
        {
            return;
        }

        _buildQueueBuiltFrom = key;
        FillTheBuildQueue(world);
    }

    private void FillTheBuildQueue(SimWorld world)
    {
        List<Workplace> queue = world.BuildQueue();
        while (_buildQueueRows.Count < queue.Count)
        {
            AddABuildQueueRow();
        }

        for (int i = 0; i < _buildQueueRows.Count; i++)
        {
            BuildQueueRow row = _buildQueueRows[i];
            bool shown = i < queue.Count;
            foreach (Control cell in row.Cells)
            {
                cell.Visible = shown;
            }

            if (!shown)
            {
                row.SiteId = 0;
                continue;
            }

            Workplace place = queue[i];
            ConstructionSite site = place.Construction!;
            row.SiteId = place.Id;
            row.Place.Text = (i + 1).ToString(System.Globalization.CultureInfo.InvariantCulture);
            row.Name.Text = place.Name;
            string doing = site.Repairing ? "Being repaired" : site.RebuildsHome ? "Being rebuilt" : "Being built";
            row.Name.TooltipText = $"{doing} — open its card";
            row.Materials.Text = MaterialsOf(world, site);
            row.Materials.TooltipText = site.HasMaterials
                ? "Everything is delivered."
                : $"Still wants {site.DescribeWhatIsMissing(world.GoodsCatalog)}.";
            row.Work.Text = $"{site.WorkDone}/{site.Recipe.WorkTicks}";

            // ⛔ Disabled exactly where `MoveInBuildQueue` refuses: the ends, and a house's rebuild either way (D721).
            bool upToRebuild = i > 0 && queue[i - 1].Construction is { RebuildsHome: true };
            bool downToRebuild = i < queue.Count - 1 && queue[i + 1].Construction is { RebuildsHome: true };
            row.Up.Disabled = i == 0 || site.RebuildsHome || upToRebuild;
            row.Down.Disabled = i == queue.Count - 1 || site.RebuildsHome || downToRebuild;
            row.Up.TooltipText = site.RebuildsHome || upToRebuild ? RebuildWaits : "Sooner";
            row.Down.TooltipText = site.RebuildsHome || downToRebuild ? RebuildWaits : "Later";
        }

        _buildQueueEmpty.Visible = queue.Count == 0;
        _buildQueueScroll.Visible = queue.Count > 0;

        // As tall as its rows up to the ceiling, then it scrolls (spec §2).
        float tall = _buildQueueGrid.GetCombinedMinimumSize().Y;
        _buildQueueScroll.CustomMinimumSize = new Vector2(0, Math.Min(tall, BuildQueueHeight));
    }

    /// <summary><c>12/40 logs · 0/5 stone</c> — every material, as the card's numbers say them.</summary>
    private static string MaterialsOf(SimWorld world, ConstructionSite site)
    {
        var parts = new List<string>();
        foreach (MaterialCost material in site.Recipe.Materials)
        {
            parts.Add($"{site.Delivered(material.Goods)}/{material.Amount} {world.GoodsCatalog.NameOf(material.Goods)}");
        }

        return string.Join(" · ", parts);
    }

    private void AddABuildQueueRow()
    {
        Label place = Body(string.Empty);
        place.HorizontalAlignment = HorizontalAlignment.Right;
        place.CustomMinimumSize = new Vector2(BuildQueuePlaceWidth, 0);

        var name = new Button
        {
            Flat = true,
            Alignment = HorizontalAlignment.Left,
            ClipText = true,
            TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis,
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            FocusMode = FocusModeEnum.None,
        };
        name.AddThemeFontSizeOverride("font_size", RowSize);

        Label materials = Body(string.Empty);
        materials.ClipText = true;
        materials.TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis;
        materials.CustomMinimumSize = new Vector2(BuildQueueMaterialsWidth, 0);
        materials.MouseFilter = MouseFilterEnum.Pass;

        Label work = Body(string.Empty);
        work.HorizontalAlignment = HorizontalAlignment.Right;
        work.CustomMinimumSize = new Vector2(BuildQueueWorkWidth, 0);

        var row = new BuildQueueRow
        {
            Place = place,
            Name = name,
            Materials = materials,
            Work = work,
            Up = new Button { Text = "▲", FocusMode = FocusModeEnum.None },
            Down = new Button { Text = "▼", FocusMode = FocusModeEnum.None },
        };

        name.Pressed += () => OpenTheSite(row.SiteId);
        row.Up.Pressed += () => MoveInTheQueue(row.SiteId, -1);
        row.Down.Pressed += () => MoveInTheQueue(row.SiteId, +1);

        foreach (Control cell in row.Cells)
        {
            _buildQueueGrid.AddChild(cell);
        }

        _buildQueueRows.Add(row);
    }

    /// <summary>A site's name clicked: its card, and the map centred on it.</summary>
    private void OpenTheSite(int siteId)
    {
        if (_loop.World.FindWorkplace(siteId) is Workplace site)
        {
            OpenCard(new CardSubject(CardKind.Workplace, site.Id));
            _map.CentreOn(new Vector2(site.Tile.X + 0.5f, site.Tile.Y + 0.5f));
        }
    }

    /// <summary>▲ or ▼ on a row: the card's ▲ Sooner / ▼ Later, one place (D105).</summary>
    private void MoveInTheQueue(int siteId, int places)
    {
        SimWorld world = _loop.World;
        if (world.FindWorkplace(siteId) is { Construction: not null } site)
        {
            world.MoveInBuildQueue(site, places);
            _buildQueueAsked++;
            RefreshBuildQueue(world);
            RefreshInspector(world);
        }
    }

    /// <summary>
    /// <c>build queue:</c> (D730, spec §4) — the window ticked on, its rows read against <c>BuildQueue()</c> cell by cell,
    /// the ends disabled, ▼ then ▲ through its own buttons and a card's ▲ Sooner seen with no tick between, then off.
    /// </summary>
    /// <remarks>⛔ Every expected value is read from the sim, never from the panel (D419's trap).</remarks>
    private string TheBuildQueueReadsTheQueue()
    {
        var faults = new List<string>();
        SimWorld world = _loop.World;
        ShellWindow? window = WindowOf(_buildQueuePanel);
        if (window?.Tick is not CheckBox tick)
        {
            return "[widths] build queue: ⛔ the window has no Settings tick";
        }

        bool wasWanted = window.Wanted;

        // ⚠️ The card opened below selects its site, and What's here follows a selection: put it back, or the next
        // probe line measures a window this one left open (the first run's fold line did).
        GridPos? wasTile = _selectedTile;
        int wasVillager = _selectedVillagerId;

        // Three sites at least, so one can move both ways; the posed ones are cancelled at the end.
        var posed = new List<Workplace>();
        GridPos origin = world.Map.FoundingSite;
        for (int r = 2; r < 40 && world.BuildQueue().Count < 3; r++)
        {
            for (int dy = -r; dy <= r && world.BuildQueue().Count < 3; dy += r)
            {
                for (int dx = -r; dx <= r && world.BuildQueue().Count < 3; dx += r)
                {
                    var tile = new GridPos(origin.X + dx, origin.Y + dy);
                    if (world.Map.Contains(tile) && world.IsWalkable(tile) && !world.StandsOn(tile)
                        && world.Mark(BuildingKind.GathererHut, Point.CentreOf(tile), Angle.Zero).Allowed
                        && world.Workplaces.Find(w => w.IsSite && w.Tile == tile) is Workplace site)
                    {
                        posed.Add(site);
                    }
                }
            }
        }

        tick.ButtonPressed = true;
        Refresh();
        if (!_buildQueuePanel.Visible)
        {
            faults.Add("ticked, the window stayed hidden");
        }

        List<Workplace> queue = world.BuildQueue();
        int[] before = queue.Select(w => w.Id).ToArray();
        List<BuildQueueRow> rows = _buildQueueRows.Where(r => r.Place.Visible).ToList();
        if (rows.Count != queue.Count)
        {
            faults.Add($"{rows.Count} rows for {queue.Count} sites queued");
        }

        for (int i = 0; i < Math.Min(rows.Count, queue.Count); i++)
        {
            ConstructionSite site = queue[i].Construction!;
            string materials = string.Join(" · ", site.Recipe.Materials.Select(m =>
                $"{site.Delivered(m.Goods)}/{m.Amount} {world.GoodsCatalog.NameOf(m.Goods)}"));
            if (rows[i].SiteId != queue[i].Id || rows[i].Place.Text != $"{world.QueuePositionOf(queue[i])}"
                || rows[i].Name.Text != queue[i].Name || rows[i].Materials.Text != materials
                || rows[i].Work.Text != $"{site.WorkDone}/{site.Recipe.WorkTicks}")
            {
                faults.Add($"row {i + 1} reads \"{rows[i].Place.Text} | {rows[i].Name.Text} | {rows[i].Materials.Text} | {rows[i].Work.Text}\" for {queue[i].Name}");
            }
        }

        if (rows.Count > 0 && (!rows[0].Up.Disabled || !rows[^1].Down.Disabled))
        {
            faults.Add("▲ on the first row or ▼ on the last is not disabled");
        }

        if (_buildQueueRows.SelectMany(r => r.Cells).OfType<BaseButton>().Any(b => b.FocusMode != FocusModeEnum.None))
        {
            faults.Add("a button takes keyboard focus (D706)");
        }

        // ▼ then ▲ through the panel's own buttons, with no tick between.
        string moved = "nothing movable";
        int at = rows.FindIndex(r => !r.Down.Disabled);
        if (at >= 0 && at + 1 < rows.Count)
        {
            Workplace site = world.FindWorkplace(rows[at].SiteId)!;
            moved = site.Name;
            rows[at].Down.EmitSignal(BaseButton.SignalName.Pressed);
            if (world.QueuePositionOf(site) != at + 2 || rows[at + 1].SiteId != site.Id)
            {
                faults.Add($"▼ left {site.Name} {world.QueuePositionOf(site)} in the sim and at row {rows.FindIndex(r => r.SiteId == site.Id) + 1}, not {at + 2}");
            }

            rows[at + 1].Up.EmitSignal(BaseButton.SignalName.Pressed);
            if (!world.BuildQueue().Select(w => w.Id).SequenceEqual(before) || rows[at].SiteId != site.Id)
            {
                faults.Add($"▲ did not put {site.Name} back");
            }

            // A card's ▲ Sooner, seen by the panel with no tick between.
            Workplace second = world.FindWorkplace(rows[at + 1].SiteId)!;
            OpenCard(new CardSubject(CardKind.Workplace, second.Id));
            MoveSelectedInQueue(-1);
            Refresh();
            if (rows[at].SiteId != second.Id || world.QueuePositionOf(second) != at + 1)
            {
                faults.Add($"a card's ▲ Sooner on {second.Name} is not in the panel");
            }

            if (_selectedCard is Card card)
            {
                CloseCard(card);
            }

            world.MoveInBuildQueue(second, +1);
            _buildQueueAsked++;
        }
        else
        {
            faults.Add("no site could be moved");
        }

        int shownRows = rows.Count;
        tick.ButtonPressed = false;
        Refresh();
        if (_buildQueuePanel.Visible)
        {
            faults.Add("unticked, the window is still shown");
        }

        foreach (Workplace site in posed)
        {
            world.CancelConstruction(site);
        }

        tick.ButtonPressed = wasWanted;
        _selectedTile = wasTile;
        _selectedVillagerId = wasVillager;
        Refresh();

        return faults.Count == 0
            ? $"[widths] build queue: ✅ ticked, it lists the {shownRows} sites queued ({posed.Count} posed) in the sim's order, each cell as the sim says it; the ends disabled and no button takes focus; ▼ and ▲ moved {moved} one place and back, and a card's ▲ Sooner showed with no tick between; unticked, it hides"
            : $"[widths] build queue: ⛔ {string.Join("; ", faults)}";
    }
}

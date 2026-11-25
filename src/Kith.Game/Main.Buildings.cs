using Kith.Sim.Config;
using Kith.Sim.Core;
using Kith.Sim.World;
using Godot;

namespace Kith.Game;

/// <summary>How <i>Buildings</i> orders its rows (Joe's play note 1, D900) — in <see cref="PlayerSettings.BuildingsSorts"/>' order.</summary>
internal enum BuildingsOrder
{
    /// <summary>The building's own name, A→Z, a number in it read as a number.</summary>
    Name,

    /// <summary>What kind of building, A→Z, then its name.</summary>
    Kind,

    /// <summary>The worst first, then its name.</summary>
    Condition,
}

/// <summary>One standing building as the list shows and sorts it; equal rows are the same list, so nothing is rebuilt for them.</summary>
/// <param name="CardKind">Which card it opens, as a number — the card's own kind is the view's private enum.</param>
/// <param name="Id">The card's id: a store's, a workplace's, a household's, or an index for the rest.</param>
/// <param name="Name">What the village calls it.</param>
/// <param name="Kind">What it is, the catalogue's word.</param>
/// <param name="Condition">How it is holding up.</param>
/// <param name="Tile">Where the map goes for it.</param>
internal readonly record struct BuildingRowShown(int CardKind, int Id, string Name, string Kind, Condition Condition, GridPos Tile)
{
    /// <summary>The order, total: a tie is broken by the name, the kind, the card and its id.</summary>
    public static int Compare(BuildingRowShown a, BuildingRowShown b, BuildingsOrder order)
    {
        int c = order switch
        {
            BuildingsOrder.Kind => string.Compare(a.Kind, b.Kind, StringComparison.OrdinalIgnoreCase),
            BuildingsOrder.Condition => ((int)b.Condition).CompareTo((int)a.Condition),
            _ => 0,
        };

        c = c != 0 ? c : CompareNames(a.Name, b.Name);
        c = c != 0 ? c : string.Compare(a.Kind, b.Kind, StringComparison.OrdinalIgnoreCase);
        c = c != 0 ? c : a.CardKind.CompareTo(b.CardKind);
        return c != 0 ? c : a.Id.CompareTo(b.Id);
    }

    /// <summary>Two names as a person orders them: <i>granary 2</i> before <i>granary 10</i>, case ignored.</summary>
    internal static int CompareNames(string a, string b)
    {
        int i = 0;
        int j = 0;
        while (i < a.Length && j < b.Length)
        {
            if (char.IsAsciiDigit(a[i]) && char.IsAsciiDigit(b[j]))
            {
                long x = 0;
                long y = 0;
                for (; i < a.Length && char.IsAsciiDigit(a[i]) && x < 1_000_000_000_000L; i++)
                {
                    x = (x * 10) + (a[i] - '0');
                }

                for (; j < b.Length && char.IsAsciiDigit(b[j]) && y < 1_000_000_000_000L; j++)
                {
                    y = (y * 10) + (b[j] - '0');
                }

                if (x != y)
                {
                    return x.CompareTo(y);
                }

                continue;
            }

            int c = char.ToUpperInvariant(a[i]).CompareTo(char.ToUpperInvariant(b[j]));
            if (c != 0)
            {
                return c;
            }

            i++;
            j++;
        }

        return (a.Length - i).CompareTo(b.Length - j);
    }
}

/// <remarks>
/// ⭐ BUILDINGS — every building standing in the village (Joe's play note 1, D900: <i>"Add a new panel: list of all buildings in
/// game"</i>; asked which, <i>"A"</i> — the standing ones, never a catalogue, which would name what is not yet learned, D460). Three
/// columns like <i>People</i>'s and no grid: the name, the kind with its icon, the condition; a heading clicked is the sort, marked ▾
/// and remembered. <b>A row clicked goes there</b> and opens its card, as a click on the map would. Shut on a new village, its word
/// on the windows' column after <i>Professions</i>.
/// <para>
/// ⛔ <b>One building, one row</b> (the doors spec's trap: one building, two lists). A market and a stall are a store and a
/// workplace — the store's row; a barn and a trading post are too — the workplace's row, as a click on the map opens it. A site is
/// not standing and has no row. ⛔ <b>Not every frame</b> (CLAUDE.md): read while the window is shown, once a day and when a building
/// is raised, pulled down or renamed, and the tree rebuilt only when a row actually differs — so it holds its scroll.
/// </para>
/// </remarks>
public partial class Main
{
    internal const string BuildingsTitle = "Buildings";

    /// <summary>Wider than <i>People</i>: <i>"builder's hut 1"</i> whole beside its kind (its first capture cut it short).</summary>
    private const float BuildingsWidth = 380f;

    /// <summary>The least the names and the kinds may have: the longest kind's name whole, with a number after it or its icon before.</summary>
    private const int BuildingsNameWidth = 110;

    private const int BuildingsKindWidth = 124;

    /// <summary>About ten buildings before it scrolls.</summary>
    private const int BuildingsHeight = 300;

    private static readonly string[] BuildingsTitles = { "Name", "Kind", "Condition" };

    private PanelContainer _buildingsPanel = null!;
    private Tree _buildings = null!;
    private BuildingsOrder _buildingsOrder = BuildingsOrder.Name;

    /// <summary>The rows as the tree shows them, in its order.</summary>
    private readonly List<BuildingRowShown> _buildingsShown = new();

    private (SimWorld? World, ulong Day, int Generation, int Asked) _buildingsReadAt;

    /// <summary>How often the tree has been rebuilt — for the probe.</summary>
    private int _buildingsRebuilt;

    private void BuildTheBuildingsPanel()
    {
        VBoxContainer body = Floating(Edge, Edge, BuildingsWidth, 0f, Corner.TopLeft, BuildingsTitle, startOpen: true);
        _docked.Add((_panels[^1], false));
        _buildingsPanel = _panels[^1];
        _buildingsPanel.Visible = false;
        _windows[^1].Wanted = false;

        var tree = new Tree
        {
            Columns = BuildingsTitles.Length,
            HideRoot = true,
            HideFolding = true, // no room kept for an arrow before every name
            ColumnTitlesVisible = true,
            SelectMode = Tree.SelectModeEnum.Row,
            AllowReselect = true, // a second click on the row goes there again, after the player has looked away
            ScrollHorizontalEnabled = false,
            SizeFlagsVertical = SizeFlags.ExpandFill,
            CustomMinimumSize = new Vector2(0, BuildingsHeight),
        };
        tree.AddThemeFontSizeOverride("font_size", RowSize);
        tree.AddThemeFontSizeOverride("title_button_font_size", RowSize);
        tree.AddThemeConstantOverride("draw_relationship_lines", 0);
        tree.AddThemeConstantOverride("draw_guides", 0);
        for (int c = 0; c < BuildingsTitles.Length; c++)
        {
            tree.SetColumnExpand(c, c == 0);
            tree.SetColumnClipContent(c, true);
            tree.SetColumnTitleAlignment(c, HorizontalAlignment.Left);
        }

        tree.CreateItem();
        tree.ColumnTitleClicked += (column, button) =>
        {
            if (button == (long)MouseButton.Left && column >= 0 && column < BuildingsTitles.Length)
            {
                SortTheBuildings((BuildingsOrder)column);
            }
        };
        tree.ItemSelected += () =>
        {
            if (_buildings.GetSelected() is TreeItem item && item.GetMetadata(0).AsInt32() is int at && at >= 0 && at < _buildingsShown.Count)
            {
                GoToTheBuilding(_buildingsShown[at]);
            }
        };
        _buildings = tree;
        body.AddChild(tree);
        ShowTheBuildingsOrder();
    }

    /// <summary>A row clicked: the map goes there and its card opens — the trading post's its window, as on the map.</summary>
    private void GoToTheBuilding(BuildingRowShown row)
    {
        _map.CentreOn(new Vector2(row.Tile.X + 0.5f, row.Tile.Y + 0.5f));
        var subject = new CardSubject((CardKind)row.CardKind, row.Id);
        if (subject.Kind == CardKind.Workplace && _loop.World.FindWorkplace(row.Id) is { Kind: JobKind.Merchant })
        {
            OpenThePostsWindow(row.Id);
        }
        else
        {
            OpenCard(subject);
        }
    }

    private void SortTheBuildings(BuildingsOrder order)
    {
        _buildingsOrder = order;
        ShowTheBuildingsOrder();
        ShowTheBuildings(_loop.World, rebuild: true);
        SettingsChanged();
    }

    /// <summary>The ▾ on the column the list is sorted by; every column as wide as its title with the mark (D816's rule, the roster's).</summary>
    private void ShowTheBuildingsOrder()
    {
        float padding = TitleGap + new[] { "title_button_normal", "title_button_hover", "title_button_pressed" }
            .Select(s => _buildings.GetThemeStylebox(s))
            .Max(b => b.GetMargin(Side.Left) + b.GetMargin(Side.Right));
        for (int c = 0; c < BuildingsTitles.Length; c++)
        {
            string marked = $"{BuildingsTitles[c]} ▾";
            float wide = Mathf.Max(
                _buildings.GetThemeFont("title_button_font").GetStringSize(marked, HorizontalAlignment.Left, -1, _buildings.GetThemeFontSize("title_button_font_size")).X,
                _buildings.GetThemeFont("font").GetStringSize(marked, HorizontalAlignment.Left, -1, _buildings.GetThemeFontSize("font_size")).X);
            _buildings.SetColumnCustomMinimumWidth(c, Math.Max(c switch { 0 => BuildingsNameWidth, 1 => BuildingsKindWidth, _ => 0 }, Mathf.CeilToInt(wide + padding)));
            _buildings.SetColumnTitle(c, c == (int)_buildingsOrder ? marked : BuildingsTitles[c]);
        }
    }

    /// <summary>Asked every frame, read only when something it says can have changed, and only while the window is shown.</summary>
    private void RefreshTheBuildings(SimWorld world)
    {
        if (!_buildingsPanel.IsVisibleInTree())
        {
            return;
        }

        var key = (world, world.Tick / (ulong)world.Config.TicksPerDay, world.BuildingGeneration, _buildQueueAsked);
        if (key != _buildingsReadAt)
        {
            _buildingsReadAt = key;
            ShowTheBuildings(world, rebuild: false);
        }
    }

    private void ShowTheBuildings(SimWorld world, bool rebuild)
    {
        List<BuildingRowShown> rows = BuildingsStanding(world);
        BuildingsOrder order = _buildingsOrder;
        rows.Sort((a, b) => BuildingRowShown.Compare(a, b, order));
        if (!rebuild && rows.SequenceEqual(_buildingsShown))
        {
            return;
        }

        _buildingsShown.Clear();
        _buildingsShown.AddRange(rows);
        _buildingsRebuilt++;

        // A rebuilt tree starts at its top: the scroll is put back where it was once the new rows are laid out.
        VScrollBar? bar = _buildings.GetChildren(includeInternal: true).OfType<VScrollBar>().FirstOrDefault();
        double scrolled = bar?.Value ?? 0.0;
        _buildings.Clear();
        TreeItem top = _buildings.CreateItem();
        for (int i = 0; i < rows.Count; i++)
        {
            BuildingRowShown row = rows[i];
            TreeItem item = _buildings.CreateItem(top);
            item.SetText(0, row.Name);
            item.SetText(1, row.Kind);
            item.SetText(2, ConditionWord(row.Condition));
            for (int c = 0; c < BuildingsTitles.Length; c++)
            {
                item.SetTextOverrunBehavior(c, TextServer.OverrunBehavior.TrimEllipsis);
                item.SetTextAlignment(c, HorizontalAlignment.Left);
            }

            if (UiIcons.Find("buildings", row.Kind) is Texture2D icon)
            {
                item.SetIcon(1, icon);
                item.SetIconMaxWidth(1, (int)UiIcons.Side);
            }

            if (row.Condition is Condition.Failing or Condition.Ruin)
            {
                item.SetCustomColor(2, UiLook.Stopped);
            }

            item.SetTooltipText(0, $"{row.Name} — click to go there");
            item.SetMetadata(0, i);
        }

        if (scrolled > 0.0)
        {
            bar?.SetDeferred(Godot.Range.PropertyName.Value, scrolled);
        }
    }

    /// <summary>The condition in the cards' own words.</summary>
    private static string ConditionWord(Condition condition) => condition switch
    {
        Condition.Sound => "sound",
        Condition.Worn => "worn",
        Condition.Failing => "failing",
        Condition.Ruin => "fallen in",
        _ => condition.ToString().ToLowerInvariant(),
    };

    /// <summary>Every building standing, once each, in the world's own order — nothing being built, nothing not learned.</summary>
    private static List<BuildingRowShown> BuildingsStanding(SimWorld world)
    {
        BuildingsCatalog catalogue = world.BuildingsCatalog;
        var rows = new List<BuildingRowShown>();
        void Row(CardKind card, int id, string name, string kind, Condition condition, GridPos tile) =>
            rows.Add(new BuildingRowShown((int)card, id, name, kind, condition, tile));

        foreach (Household home in world.Households)
        {
            if (home.HomePosition is Point at)
            {
                string kind = home.HomeKind == BuildingKind.Home ? "cabin" : catalogue.NameOf(home.HomeKind);
                Row(CardKind.Household, home.Id, world.HouseNameOf(home) ?? $"{home.Name} cabin", kind, world.ConditionOf(home), at.ToTile());
            }
        }

        foreach (StoreBuilding store in world.StoreBuildings)
        {
            // A barn and a post are their workplace's row, as a click on the map opens them (D596).
            if (store.Kind is not (StoreKind.Barn or StoreKind.TradingPost))
            {
                string kind = world.BuildingKindOf(store) is BuildingKind raised ? catalogue.NameOf(raised) : store.Kind.ToString().ToLowerInvariant();
                Row(CardKind.Store, store.Id, store.Name, kind, world.ConditionOf(store), store.Tile);
            }
        }

        foreach (Workplace place in world.Workplaces)
        {
            // A market's and a stall's marketers stand in the store already listed.
            if (!place.IsSite && place.Kind != JobKind.Marketer && world.BuildingKindOf(place) is BuildingKind works)
            {
                Row(CardKind.Workplace, place.Id, place.Name, catalogue.NameOf(works), world.ConditionOf(place), place.Tile);
            }
        }

        for (int i = 0; i < world.Libraries.Count; i++)
        {
            Library library = world.Libraries[i];
            Row(CardKind.Library, i, library.Name, catalogue.NameOf(BuildingKind.Library), world.ConditionOf(library), library.Tile);
        }

        if (world.TownHall is TownHall hall)
        {
            Row(CardKind.TownHall, 0, hall.Name, catalogue.NameOf(BuildingKind.TownHall), world.ConditionOf(hall), hall.Tile);
        }

        for (int i = 0; i < world.Wells.Count; i++)
        {
            Well well = world.Wells[i];
            Row(CardKind.Well, i, well.Name, catalogue.NameOf(well.Kind), world.ConditionOf(well), well.Tile);
        }

        for (int i = 0; i < world.Bridges.Count; i++)
        {
            Bridge bridge = world.Bridges[i];
            GridPos middle = new(
                (bridge.Near.ToTile().X + bridge.Far.ToTile().X) / 2,
                (bridge.Near.ToTile().Y + bridge.Far.ToTile().Y) / 2);
            Row(CardKind.Bridge, i, bridge.Name, catalogue.NameOf(bridge.Kind), world.ConditionOf(bridge), middle);
        }

        return rows;
    }

    /// <summary>
    /// ⭐ <c>buildings list:</c> (Joe's play note 1, D900) — posed rows, each order against a list typed by hand with every tie
    /// posed; then a world with its founding buildings: every standing building once, the market once though it is in two lists,
    /// no site and no kind the village has not raised; then the real window: each heading sorts it, marks itself, is captured and
    /// moves no width; a row clicked goes there and opens its card, and again on a second click; nothing is read while it is shut,
    /// and the tree is not rebuilt for a day that changed nothing.
    /// </summary>
    private string TheBuildingsAreListed()
    {
        var faults = new List<string>();

        // Names that only a number read as a number orders, a case ignored, a kind tie and a condition tie.
        BuildingRowShown[] posed =
        {
            new(0, 1, "granary 10", "granary", Condition.Worn, default), new(0, 2, "Granary 2", "granary", Condition.Ruin, default),
            new(1, 3, "Ashen", "smithy", Condition.Worn, default), new(1, 4, "zither", "bakery", Condition.Sound, default),
        };
        foreach ((BuildingsOrder order, int[] wanted) in new[]
        {
            (BuildingsOrder.Name, new[] { 3, 2, 1, 4 }),
            (BuildingsOrder.Kind, new[] { 4, 2, 1, 3 }),
            (BuildingsOrder.Condition, new[] { 2, 3, 1, 4 }),
        })
        {
            var sorted = posed.ToList();
            sorted.Sort((a, b) => BuildingRowShown.Compare(a, b, order));
            if (!sorted.Select(r => r.Id).SequenceEqual(wanted))
            {
                faults.Add($"posed by {order}: {string.Join(",", sorted.Select(r => r.Id))}, not {string.Join(",", wanted)}");
            }
        }

        // A world that begins with its buildings: the market, granaries, huts and homes the old founding raises.
        SimWorld founded = SimFactory.CreatePhase0(
            _loop.World.Config with { FoundingBuildings = true, MarketCapacity = 2 }, new Kith.Sim.Logging.InMemoryLogSink()).World;
        List<BuildingRowShown> all = BuildingsStanding(founded);
        int homes = founded.Households.Count(h => h.HomePosition is not null);
        int others = founded.Workplaces.Count(w => !w.IsSite && w.Kind != JobKind.Marketer);
        int markets = all.Count(r => r.Kind == founded.BuildingsCatalog.NameOf(BuildingKind.Market));
        if (all.Count != homes + founded.StoreBuildings.Count + others || homes == 0 || markets != 1
            || all.Select(r => r.Tile).Distinct().Count() != all.Count || all.Any(r => r.Name.Length == 0 || r.Kind.Length == 0))
        {
            faults.Add($"a founded village lists {all.Count}: {homes} homes, {founded.StoreBuildings.Count} stores, {others} other workplaces; the market {markets} times; "
                + $"{all.Count - all.Select(r => r.Tile).Distinct().Count()} rows share a tile");
        }

        // A site is not a building yet.
        Workplace site = founded.Workplaces.Find(w => !w.IsSite && w.Kind != JobKind.Marketer)!;
        var raising = new Workplace
        {
            Store = new Stockpile(founded.GoodsCatalog.Count), Id = 990_004, Kind = JobKind.Builder, Name = "Posed site",
            Position = site.Position, Capacity = 0, Construction = _loop.World.Workplaces.Find(w => w.IsSite)?.Construction,
        };
        founded.Workplaces.Add(raising);
        bool sitePosed = raising.IsSite;
        if (sitePosed && BuildingsStanding(founded).Exists(r => r.Id == raising.Id))
        {
            faults.Add("a site is listed as standing");
        }

        founded.Workplaces.Remove(raising);

        // ---- the window itself, over the probe's own village ----
        SimWorld world = _loop.World;
        bool liveWas = _settingsLive;
        _settingsLive = false;
        ShellWindow window = WindowOf(_buildingsPanel)!;
        bool wantedWas = window.Wanted;
        BuildingsOrder orderWas = _buildingsOrder;
        try
        {
            // ⚠️ The shown rows forgotten, or a read that found the same rows would rebuild nothing and pass (it scored zero).
            int rebuilt = _buildingsRebuilt;
            _buildingsReadAt = default;
            _buildingsShown.Clear();
            RefreshTheBuildings(world);
            if (_buildingsPanel.Visible || _buildingsRebuilt != rebuilt)
            {
                faults.Add($"shut on a new village {!_buildingsPanel.Visible}, and read while shut {_buildingsRebuilt != rebuilt}");
            }

            SetShown(window, true);
            RefreshTheBuildings(world);
            rebuilt = _buildingsRebuilt;
            _buildingsReadAt = default;
            RefreshTheBuildings(world);
            if (_buildingsRebuilt != rebuilt)
            {
                faults.Add("a day that changed nothing rebuilt the list");
            }

            var shapes = new HashSet<string>();
            int standing = BuildingsStanding(world).Count;
            foreach (BuildingsOrder order in Enum.GetValues<BuildingsOrder>())
            {
                _buildings.EmitSignal(Tree.SignalName.ColumnTitleClicked, (long)order, (long)MouseButton.Left);
                var listed = _buildings.GetRoot().GetChildren();
                var wanted = BuildingsStanding(world);
                wanted.Sort((a, b) => BuildingRowShown.Compare(a, b, order));
                if (listed.Count != standing || standing == 0 || !listed.Select(i => i.GetText(0)).SequenceEqual(wanted.Select(r => r.Name)))
                {
                    faults.Add($"by {order} it lists {listed.Count} of {standing}");
                }

                string marks = string.Join("", Enumerable.Range(0, BuildingsTitles.Length).Where(c => _buildings.GetColumnTitle(c).EndsWith('▾')));
                if (marks != ((int)order).ToString(System.Globalization.CultureInfo.InvariantCulture))
                {
                    faults.Add($"by {order} the ▾ is on {marks}");
                }

                if (CaptureSettings().BuildingsSort != PlayerSettings.BuildingsSorts[(int)order])
                {
                    faults.Add($"{order} is captured as {CaptureSettings().BuildingsSort ?? "nothing"}");
                }

                shapes.Add($"{_buildings.GetCombinedMinimumSize().X:F0}");
            }

            if (shapes.Count != 1)
            {
                faults.Add($"sorting moves the list: {string.Join(", ", shapes)} wide");
            }

            ApplySettings(CaptureSettings() with { BuildingsSort = "kind" });
            if (_buildingsOrder != BuildingsOrder.Kind)
            {
                faults.Add("the settings' order is not applied");
            }

            // The longest name the bar can raise is read whole, and the longest kind beside its icon; and the window holds them.
            float longest = Enumerable.Range(0, world.BuildingsCatalog.Count).Where(world.BuildingsCatalog.Has)
                .Max(id => _buildings.GetThemeFont("font").GetStringSize(world.BuildingsCatalog.NameOf((BuildingKind)id) + " 1", HorizontalAlignment.Left, -1, RowSize).X);
            if (_buildings.GetColumnWidth(0) < longest)
            {
                faults.Add($"the names' column is {_buildings.GetColumnWidth(0)} wide and the longest kind's name wants {longest:F0}");
            }

            if (_buildings.GetColumnWidth(1) < longest + UiIcons.Side)
            {
                faults.Add($"the kinds' column is {_buildings.GetColumnWidth(1)} wide and the longest wants {longest + UiIcons.Side:F0}");
            }

            float wants = _buildingsPanel.GetCombinedMinimumSize().X;
            if (wants > BuildingsWidth)
            {
                faults.Add($"the window wants {wants:F0}, not {BuildingsWidth:F0}");
            }

            // ⭐ Click to go there, and again.
            SetShown(window, true);
            ShowTheBuildings(world, rebuild: true);
            TreeItem? first = _buildings.GetRoot().GetChildCount() > 0 ? _buildings.GetRoot().GetChild(0) : null;
            GridPos there = first is null ? default : _buildingsShown[0].Tile;
            for (int click = 0; click < 2 && first is not null; click++)
            {
                _map.CentreOn(new Vector2(there.X + 25f, there.Y + 10f));
                Vector2 away = _map.CentredOn;
                first.Select(0);
                _buildings.EmitSignal(Tree.SignalName.ItemSelected);
                if (_map.CentredOn.DistanceTo(new Vector2(there.X + 0.5f, there.Y + 0.5f)) > away.DistanceTo(new Vector2(there.X + 0.5f, there.Y + 0.5f)) - 1f
                    || _selectedCard is null || _selectedCard.Subject.Id != _buildingsShown[0].Id)
                {
                    faults.Add($"click {click + 1} went to {_map.CentredOn} (the building is at {there}) and opened {(_selectedCard is null ? "nothing" : _selectedCard.Title.Text)}");
                }

                while (_cards.Count > 0)
                {
                    CloseCard(_cards[0]);
                }
            }

            if (!_buildings.AllowReselect)
            {
                faults.Add("a second click on the same row does nothing");
            }
        }
        finally
        {
            _buildingsOrder = orderWas;
            ShowTheBuildingsOrder();
            SetShown(window, wantedWas);
            _map.CentreOnTheVillage();
            _settingsLive = liveWas;
        }

        return faults.Count == 0
            ? $"[widths] buildings list: ✅ a founded village's {all.Count} buildings once each (the market once){(sitePosed ? ", no site" : string.Empty)}; Name, Kind and Condition each sort it as typed and as clicked; "
              + "a row goes there and opens its card, twice running; shut on a new village and not read while shut, nor rebuilt for a day that changed nothing"
            : $"[widths] buildings list: ⛔ {string.Join("; ", faults)}";
    }
}

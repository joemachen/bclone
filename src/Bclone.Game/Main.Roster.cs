using Bclone.Sim.Config;
using Bclone.Sim.Core;
using Bclone.Sim.World;
using Godot;

namespace Bclone.Game;

/// <summary>How <i>The village</i> panel orders its people (D609) — in <see cref="PlayerSettings.RosterSorts"/>' order.</summary>
internal enum RosterOrder
{
    /// <summary>First name A→Z, then surname, then who came first.</summary>
    Name,

    /// <summary>Oldest first, by the tick they were born.</summary>
    Age,

    /// <summary>Trade A→Z — laborers among them — then name; the children, who have none yet, last.</summary>
    Trade,
}

/// <summary>One person as the roster sorts them: only what the three orders read, so the probe can pose rows by hand.</summary>
/// <param name="Id">The villager's id — the last tiebreak, and what the row's metadata carries.</param>
/// <param name="Name">The first name, which <see cref="RosterOrder.Name"/> sorts by.</param>
/// <param name="Surname">The family name, the first tiebreak after it.</param>
/// <param name="BirthTick">When they were born — the earliest is the oldest.</param>
/// <param name="Work">The trade word the row shows — <c>laborer</c>, a trade's name, or <c>child</c>.</param>
/// <param name="Child">A child: sorted after every trade, not among them under <i>c</i>.</param>
internal readonly record struct RosterRow(int Id, string Name, string Surname, long BirthTick, string Work, bool Child)
{
    /// <summary>The order, total: every tie ends on the id, so the same people always come out the same way round.</summary>
    public static int Compare(RosterRow a, RosterRow b, RosterOrder order)
    {
        int c = order switch
        {
            RosterOrder.Age => a.BirthTick.CompareTo(b.BirthTick),
            RosterOrder.Trade => a.Child != b.Child
                ? a.Child.CompareTo(b.Child)
                : string.Compare(a.Work, b.Work, StringComparison.OrdinalIgnoreCase),
            _ => 0,
        };

        c = c != 0 ? c : string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase);
        c = c != 0 ? c : string.Compare(a.Surname, b.Surname, StringComparison.OrdinalIgnoreCase);
        return c != 0 ? c : a.Id.CompareTo(b.Id);
    }
}

/// <summary>
/// ⭐ <i>The village</i> panel: everyone alive, in the order the player chose (D609, Joe: <i>"add 'sort' options to the
/// 'the village panel' alphabetical (first name), by age, by profession (alphabetical too)"</i>).
/// </summary>
public partial class Main
{
    private RosterOrder _rosterOrder = RosterOrder.Name;

    /// <summary>The column titles, in <see cref="RosterOrder"/>'s order — the sort is the column you click (D616).</summary>
    private static readonly string[] RosterTitles = { "Name", "Age", "Trade" };

    /// <summary>The age column's width: three digits and the sort mark, right-aligned so the years line up.</summary>
    private const int RosterAgeWidth = 44;

    /// <summary>The trade column's width: <i>woodcutter</i> whole at the panel's own size; anything longer trims.</summary>
    private const int RosterTradeWidth = 96;

    /// <summary>The rows in the order they were added, each with its villager — what a selection made on the map is found by.</summary>
    private readonly List<(int Id, TreeItem Item)> _rosterItems = new();

    /// <summary>What the rows were last built from; equal, and nothing a row says can have changed.</summary>
    private (SimWorld? World, ulong Tick, RosterOrder Order, int People, long Jobs) _rosterBuiltFrom;

    /// <summary>The villager the list last showed selected — so a click on the map moves the highlight without a rebuild.</summary>
    private int _rosterShowsSelected;

    /// <summary>True while the code selects a row, so its own <c>ItemSelected</c> is not taken for a click.</summary>
    private bool _rosterSelecting;

    /// <summary>
    /// ⭐ <i>The village</i> as three columns that line up, with no lines drawn (D616, Joe: <i>"i dont want an actual grid …
    /// but its hard to scan because of the varied position of the information"</i>) — and the column titles are the sort:
    /// click <i>Age</i> and it sorts by age, the column it sorts by marked ▾.
    /// </summary>
    private Tree MakeTheRoster()
    {
        var tree = new Tree
        {
            Columns = RosterTitles.Length,
            HideRoot = true,
            ColumnTitlesVisible = true,
            SelectMode = Tree.SelectModeEnum.Row,
            ScrollHorizontalEnabled = false,
            SizeFlagsVertical = SizeFlags.ExpandFill,
            CustomMinimumSize = new Vector2(0, ListHeight),
        };
        tree.AddThemeFontSizeOverride("font_size", RowSize);
        tree.AddThemeFontSizeOverride("title_button_font_size", RowSize);

        // ⭐ NO GRID: no relationship lines, no guides — the columns line up and nothing is drawn between them.
        tree.AddThemeConstantOverride("draw_relationship_lines", 0);
        tree.AddThemeConstantOverride("draw_guides", 0);

        tree.SetColumnExpand(0, true);
        tree.SetColumnClipContent(0, true);
        tree.SetColumnExpand(1, false);
        tree.SetColumnCustomMinimumWidth(1, RosterAgeWidth);
        tree.SetColumnTitleAlignment(1, HorizontalAlignment.Right);
        tree.SetColumnExpand(2, false);
        tree.SetColumnCustomMinimumWidth(2, RosterTradeWidth);
        tree.SetColumnClipContent(2, true);
        tree.SetColumnTitleAlignment(0, HorizontalAlignment.Left);
        tree.SetColumnTitleAlignment(2, HorizontalAlignment.Left);
        tree.CreateItem();

        tree.ColumnTitleClicked += (column, button) =>
        {
            if (button == (long)MouseButton.Left && column >= 0 && column < RosterTitles.Length)
            {
                SortTheRoster((RosterOrder)column);
            }
        };
        tree.ItemSelected += OnVillagerSelected;
        return tree;
    }

    /// <summary>A column title clicked, or a remembered setting: the order, the titles, and the file.</summary>
    private void SortTheRoster(RosterOrder order)
    {
        _rosterOrder = order;
        ShowTheRosterOrder();
        RefreshRoster(_loop.World);
        SettingsChanged();
    }

    /// <summary>The ▾ on the column the list is sorted by, and on no other.</summary>
    private void ShowTheRosterOrder()
    {
        for (int c = 0; c < RosterTitles.Length; c++)
        {
            _roster.SetColumnTitle(c, c == (int)_rosterOrder ? $"{RosterTitles[c]} ▾" : RosterTitles[c]);
        }
    }

    /// <summary>A row clicked: that villager is selected, as a click on them on the map would.</summary>
    private void OnVillagerSelected()
    {
        if (!_rosterSelecting && _roster.GetSelected() is TreeItem item)
        {
            SelectVillager(item.GetMetadata(0).AsInt32());
        }
    }

    /// <summary>The roster's rows, rebuilt only when something a row says has changed.</summary>
    /// <remarks>
    /// <para>
    /// ⛔ <b>Not every frame</b> (CLAUDE.md, Joe's 2026-09-10 rule). It was cleared and refilled each frame since D80 —
    /// "a rebuilt list cannot drift out of sync with the sim" — and a sort on top would have been a sort a frame for a
    /// list that changes a few times a day. The key is what the rows are made of: a name, an age and a stage move only
    /// on a tick; a death or an arrival changes the head count; and <b>a job can change between ticks</b> —
    /// <c>SetStaffing</c> and <c>SetJobLimit</c> take up the slack at once (D396) — so the workplaces are folded in too.
    /// </para>
    /// <para>
    /// The selection is followed separately: a click on the map moves the highlight, never the rows.
    /// </para>
    /// </remarks>
    private void RefreshRoster(SimWorld world)
    {
        int people = 0;
        long jobs = 17;
        for (int i = 0; i < world.Villagers.Count; i++)
        {
            Villager villager = world.Villagers[i];
            if (villager.Alive)
            {
                people++;
                jobs = unchecked((jobs * 31) + (villager.Id * 7919L) + villager.WorkplaceId);
            }
        }

        var key = (world, world.Tick, _rosterOrder, people, jobs);
        if (key != _rosterBuiltFrom)
        {
            _rosterBuiltFrom = key;
            RebuildTheRoster(world);
            _rosterShowsSelected = -1;
        }

        if (_rosterShowsSelected != _selectedVillagerId)
        {
            _rosterShowsSelected = _selectedVillagerId;
            int index = _rosterItems.FindIndex(r => r.Id == _selectedVillagerId);
            _rosterSelecting = true;
            try
            {
                if (index >= 0)
                {
                    _rosterItems[index].Item.Select(0);
                }
                else
                {
                    _roster.DeselectAll();
                }
            }
            finally
            {
                _rosterSelecting = false;
            }
        }
    }

    private void RebuildTheRoster(SimWorld world)
    {
        List<(RosterRow Row, string Age)> rows = RosterRowsOf(world);
        RosterOrder order = _rosterOrder;
        rows.Sort((a, b) => RosterRow.Compare(a.Row, b.Row, order));

        _roster.Clear();
        _rosterItems.Clear();
        TreeItem top = _roster.CreateItem();
        foreach ((RosterRow row, string age) in rows)
        {
            TreeItem item = _roster.CreateItem(top);
            item.SetText(0, row.Surname.Length == 0 ? row.Name : $"{row.Name} {row.Surname}");
            item.SetTextOverrunBehavior(0, TextServer.OverrunBehavior.TrimEllipsis);
            item.SetText(1, age);
            item.SetTextAlignment(1, HorizontalAlignment.Right);
            item.SetText(2, row.Work);
            item.SetTextOverrunBehavior(2, TextServer.OverrunBehavior.TrimEllipsis);
            item.SetMetadata(0, row.Id);
            _rosterItems.Add((row.Id, item));
        }
    }

    /// <summary>Everyone alive, as a row and their age in years, in the order they joined the village.</summary>
    private List<(RosterRow Row, string Age)> RosterRowsOf(SimWorld world)
    {
        var rows = new List<(RosterRow Row, string Age)>();
        for (int i = 0; i < world.Villagers.Count; i++)
        {
            Villager villager = world.Villagers[i];
            if (!villager.Alive)
            {
                continue;
            }

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
            // ⭐ The stage words went with the columns (D616): the age says it, and a child's trade is "child".
            bool child = !villager.IsLaborer && !villager.HasJob;
            string work = villager.IsLaborer ? "laborer"
                : villager.HasJob ? TradeOf(villager)
                : "child";
            rows.Add((
                new RosterRow(villager.Id, villager.Name, villager.Surname, villager.BirthTick, work, child),
                villager.AgeYears.ToString(System.Globalization.CultureInfo.InvariantCulture)));
        }

        return rows;
    }

    /// <summary>
    /// <c>roster sort:</c> (D609) — posed rows, each order against a list typed by hand, every tie posed so its tiebreak
    /// bites; then the real panel (D616): each column title clicked, every living villager listed once in its columns, the ▾
    /// on the clicked one, and the capture says the order.
    /// </summary>
    /// <remarks>
    /// ⛔ The expected orders are written out, never computed by the comparer they check (D419's trap: a check that
    /// counts from what it checks agrees with itself).
    /// </remarks>
    private string TheRosterSortsAsAsked()
    {
        var faults = new List<string>();

        // Aldmoor and aldo share a birth tick (Age falls to the name) and their first letters (case is ignored, so the
        // fourth letter decides); bram is the oldest, so Age and Name disagree; Cara is the child, after every trade
        // though "child" sorts under c, ahead of "fisher" — only the child rule puts her last.
        RosterRow[] posed =
        {
            new(3, "bram", "Frost", 40, "woodcutter", false),
            new(1, "Aldmoor", "Atwood", 50, "laborer", false),
            new(2, "Cara", "Frost", 300, "child", true),
            new(4, "aldo", "Frost", 50, "fisher", false),
        };

        foreach ((RosterOrder order, int[] wanted) in new[]
        {
            (RosterOrder.Name, new[] { 1, 4, 3, 2 }),
            (RosterOrder.Age, new[] { 3, 1, 4, 2 }),
            (RosterOrder.Trade, new[] { 4, 1, 3, 2 }),
        })
        {
            var sorted = posed.ToList();
            sorted.Sort((a, b) => RosterRow.Compare(a, b, order));
            int[] got = sorted.Select(r => r.Id).ToArray();
            if (!got.SequenceEqual(wanted))
            {
                faults.Add($"{order} posed {string.Join(",", got)}, not {string.Join(",", wanted)}");
            }
        }

        // A name tie with an age tie: the surname, then the id — Atwood has the highest id, so the surname must decide.
        var twins = new List<RosterRow>
        {
            new(8, "Wren", "Thatcher", 10, "forager", false),
            new(9, "Wren", "Atwood", 10, "forager", false),
            new(7, "Wren", "Thatcher", 10, "forager", false),
        };
        twins.Sort((a, b) => RosterRow.Compare(a, b, RosterOrder.Age));
        if (!twins.Select(r => r.Id).SequenceEqual(new[] { 9, 7, 8 }))
        {
            faults.Add($"twins came out {string.Join(",", twins.Select(r => r.Id))}, not 9,7,8");
        }

        // The real panel, through its own column titles (D616).
        SimWorld world = _loop.World;
        RosterOrder was = _rosterOrder;
        var orders = new List<string>();
        var living = world.Villagers.Where(v => v.Alive).Select(v => v.Id).OrderBy(id => id).ToList();
        foreach (RosterOrder order in new[] { RosterOrder.Name, RosterOrder.Age, RosterOrder.Trade })
        {
            _roster.EmitSignal(Tree.SignalName.ColumnTitleClicked, (long)order, (long)MouseButton.Left);
            if (_rosterOrder != order)
            {
                faults.Add($"clicking {RosterTitles[(int)order]} left the order at {_rosterOrder}");
            }

            for (int c = 0; c < RosterTitles.Length; c++)
            {
                if (_roster.GetColumnTitle(c).EndsWith('▾') != (c == (int)order))
                {
                    faults.Add($"sorted by {order}, the {RosterTitles[c]} title reads \"{_roster.GetColumnTitle(c)}\"");
                }
            }

            var items = _roster.GetRoot().GetChildren();
            var listed = items.Select(i => i.GetMetadata(0).AsInt32()).ToList();
            if (!listed.OrderBy(id => id).SequenceEqual(living))
            {
                faults.Add($"{order} listed {listed.Count} people, not the {living.Count} alive, each once");
            }

            // ⭐ The columns hold what they say: a name, the years as digits, a trade word — on every row.
            foreach (TreeItem item in items)
            {
                Villager v = world.Villagers.First(x => x.Id == item.GetMetadata(0).AsInt32());
                if (item.GetText(0) != v.FullName || item.GetText(1) != v.AgeYears.ToString(System.Globalization.CultureInfo.InvariantCulture)
                    || item.GetText(2).Length == 0 || item.GetText(2).Any(char.IsDigit))
                {
                    faults.Add($"a row reads \"{item.GetText(0)}\" | \"{item.GetText(1)}\" | \"{item.GetText(2)}\"");
                    break;
                }
            }

            // The wiring, not the comparer (the posed rows check that): the panel is in the clicked order NOW.
            List<(RosterRow Row, string Age)> rows = RosterRowsOf(world);
            rows.Sort((a, b) => RosterRow.Compare(a.Row, b.Row, order));
            if (!listed.SequenceEqual(rows.Select(r => r.Row.Id)))
            {
                faults.Add($"clicking {RosterTitles[(int)order]} did not re-sort the panel");
            }

            orders.Add(string.Join(",", listed));

            string? captured = CaptureSettings().RosterSort;
            if (captured != PlayerSettings.RosterSorts[(int)order])
            {
                faults.Add($"{order} is captured as {captured ?? "nothing"}");
            }
        }

        // ⭐ AND THEY FIT: the age and trade columns at their widths, the name taking the rest, inside the panel.
        if (_roster.GetColumnWidth(1) < RosterAgeWidth || _roster.GetColumnWidth(2) < RosterTradeWidth
            || _roster.GetColumnWidth(0) < 100)
        {
            faults.Add($"the columns are {_roster.GetColumnWidth(0)} / {_roster.GetColumnWidth(1)} / {_roster.GetColumnWidth(2)} wide");
        }

        SortTheRoster(was);

        return faults.Count == 0
            ? $"[widths] roster sort: ✅ name, age and trade against posed rows typed by hand, every tiebreak posed; the three column titles each set the order and wear the ▾, list the {living.Count} alive once in it, a name, the years and a trade in their columns ({_roster.GetColumnWidth(0)} / {_roster.GetColumnWidth(1)} / {_roster.GetColumnWidth(2)} wide), and are captured ({orders.Distinct().Count()} distinct orders here)"
            : $"[widths] roster sort: ⛔ {string.Join("; ", faults)}";
    }
}

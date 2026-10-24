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
/// <param name="Work">The trade word the row shows — <c>laborer</c>, a trade's name, or <c>not working yet</c>.</param>
/// <param name="Child">Not working yet: sorted after every trade, not among them under <i>n</i>.</param>
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

    private readonly List<(RosterOrder Order, Button Button)> _rosterSortButtons = new();

    /// <summary>The ids in the order the rows were added — what a selection made on the map is found by.</summary>
    private readonly List<int> _rosterIds = new();

    /// <summary>What the rows were last built from; equal, and nothing a row says can have changed.</summary>
    private (SimWorld? World, ulong Tick, RosterOrder Order, int People, long Jobs) _rosterBuiltFrom;

    /// <summary>The villager the list last showed selected — so a click on the map moves the highlight without a rebuild.</summary>
    private int _rosterShowsSelected;

    /// <summary><i>Sort:</i> and one toggle per order, above the list.</summary>
    private void AddTheRosterSorts(VBoxContainer body)
    {
        var row = new HBoxContainer();
        row.AddThemeConstantOverride("separation", 6);
        row.AddChild(Muted("Sort:"));
        foreach ((RosterOrder order, string label) in new[]
        {
            (RosterOrder.Name, "Name"),
            (RosterOrder.Age, "Age"),
            (RosterOrder.Trade, "Trade"),
        })
        {
            var button = new Button { Text = label, ToggleMode = true, Flat = true };
            button.AddThemeFontSizeOverride("font_size", RowSize);
            button.Pressed += () => SortTheRoster(order);
            _rosterSortButtons.Add((order, button));
            row.AddChild(button);
        }

        body.AddChild(row);
        ShowTheRosterOrder();
    }

    /// <summary>A sort button, or a remembered setting: the order, the buttons, and the file.</summary>
    private void SortTheRoster(RosterOrder order)
    {
        _rosterOrder = order;
        ShowTheRosterOrder();
        RefreshRoster(_loop.World);
        SettingsChanged();
    }

    /// <summary>One button down — the order's — whichever was pressed, so a press on the down button cannot raise it.</summary>
    private void ShowTheRosterOrder()
    {
        foreach ((RosterOrder order, Button button) in _rosterSortButtons)
        {
            button.SetPressedNoSignal(order == _rosterOrder);
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
            int index = _rosterIds.IndexOf(_selectedVillagerId);
            if (index >= 0)
            {
                _roster.Select(index);
            }
            else
            {
                _roster.DeselectAll();
            }
        }
    }

    private void RebuildTheRoster(SimWorld world)
    {
        List<(RosterRow Row, string Text)> rows = RosterRowsOf(world);
        RosterOrder order = _rosterOrder;
        rows.Sort((a, b) => RosterRow.Compare(a.Row, b.Row, order));

        _roster.Clear();
        _rosterIds.Clear();
        foreach ((RosterRow row, string text) in rows)
        {
            int index = _roster.AddItem(text);
            _roster.SetItemMetadata(index, row.Id);
            _rosterIds.Add(row.Id);
        }
    }

    /// <summary>Everyone alive, as a row and its text, in the order they joined the village.</summary>
    private List<(RosterRow Row, string Text)> RosterRowsOf(SimWorld world)
    {
        var rows = new List<(RosterRow Row, string Text)>();
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
            bool child = !villager.IsLaborer && !villager.HasJob;
            string work = villager.IsLaborer ? "laborer"
                : villager.HasJob ? TradeOf(villager)
                : "not working yet";
            rows.Add((
                new RosterRow(villager.Id, villager.Name, villager.Surname, villager.BirthTick, work, child),
                $"{villager.FullName}, {villager.AgeYears} ({stage}) — {work}"));
        }

        return rows;
    }

    /// <summary>
    /// <c>roster sort:</c> (D609) — posed rows, each order against a list typed by hand, every tie posed so its tiebreak
    /// bites; then the real panel: each button pressed, every living villager listed once, and the capture says the order.
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
        // though "not working yet" sorts under n.
        RosterRow[] posed =
        {
            new(3, "bram", "Frost", 40, "woodcutter", false),
            new(1, "Aldmoor", "Atwood", 50, "laborer", false),
            new(2, "Cara", "Frost", 300, "not working yet", true),
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

        // The real panel, through its own buttons.
        SimWorld world = _loop.World;
        RosterOrder was = _rosterOrder;
        var orders = new List<string>();
        var living = world.Villagers.Where(v => v.Alive).Select(v => v.Id).OrderBy(id => id).ToList();
        foreach ((RosterOrder order, Button button) in _rosterSortButtons)
        {
            button.EmitSignal(BaseButton.SignalName.Pressed);
            if (_rosterOrder != order || !button.ButtonPressed || _rosterSortButtons.Count(b => b.Button.ButtonPressed) != 1)
            {
                faults.Add($"pressing {button.Text} left the order at {_rosterOrder}");
            }

            var listed = Enumerable.Range(0, _roster.ItemCount).Select(i => _roster.GetItemMetadata(i).AsInt32()).ToList();
            if (!listed.OrderBy(id => id).SequenceEqual(living))
            {
                faults.Add($"{order} listed {listed.Count} people, not the {living.Count} alive, each once");
            }

            // The wiring, not the comparer (the posed rows check that): the panel is in the pressed order NOW.
            List<(RosterRow Row, string Text)> rows = RosterRowsOf(world);
            rows.Sort((a, b) => RosterRow.Compare(a.Row, b.Row, order));
            if (!listed.SequenceEqual(rows.Select(r => r.Row.Id)))
            {
                faults.Add($"pressing {button.Text} did not re-sort the panel");
            }

            orders.Add(string.Join(",", listed));

            string? captured = CaptureSettings().RosterSort;
            if (captured != PlayerSettings.RosterSorts[(int)order])
            {
                faults.Add($"{order} is captured as {captured ?? "nothing"}");
            }
        }

        SortTheRoster(was);

        return faults.Count == 0
            ? $"[widths] roster sort: ✅ name, age and trade against posed rows typed by hand, every tiebreak posed; {_rosterSortButtons.Count} buttons each set the order, list the {living.Count} alive once in it and are captured ({orders.Distinct().Count()} distinct orders here)"
            : $"[widths] roster sort: ⛔ {string.Join("; ", faults)}";
    }
}

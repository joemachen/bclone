using Kith.Sim.Config;
using Kith.Sim.Core;
using Kith.Sim.World;
using Godot;

namespace Kith.Game;

/// <summary>How Professions orders its trades (Joe's play note 6, D896) — in <see cref="PlayerSettings.ProfessionsSorts"/>' order.</summary>
internal enum ProfessionsOrder
{
    /// <summary>The trade's name, A→Z.</summary>
    Type,

    /// <summary>The number asked for, most first.</summary>
    Assigned,

    /// <summary>The seats there are, most first.</summary>
    Max,
}

/// <summary>One trade as the table sorts it: only what the three orders read, so the probe can pose rows by hand.</summary>
/// <param name="Kind">The trade — the last tiebreak.</param>
/// <param name="Name">The word the row shows.</param>
/// <param name="Assigned">The number asked for.</param>
/// <param name="Max">The seats there are.</param>
internal readonly record struct ProfessionRow(JobKind Kind, string Name, int Assigned, int Max)
{
    /// <summary>The order, total: a tie is broken by the name, then the trade, so the same numbers come out the same way round.</summary>
    public static int Compare(ProfessionRow a, ProfessionRow b, ProfessionsOrder order)
    {
        int c = order switch
        {
            ProfessionsOrder.Assigned => b.Assigned.CompareTo(a.Assigned),
            ProfessionsOrder.Max => b.Max.CompareTo(a.Max),
            _ => 0,
        };

        c = c != 0 ? c : string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase);
        return c != 0 ? c : ((int)a.Kind).CompareTo((int)b.Kind);
    }
}

/// <remarks>
/// ⭐ PROFESSIONS SORTED BY A COLUMN (Joe's play note 6, D896: <i>"the professions window should allow sorting by column the same
/// way that the people window does"</i>) — click <i>Type</i>, <i>Assigned</i> or <i>Max</i>; the column it was sorted by is marked ▾,
/// and the choice is remembered. Until a heading is clicked the trades stand in the data's own order.
/// ⛔ <b>Sorted on a click, on a load and when a trade appears — never on a press of − or +</b> (Joe's "e"): the rows hold the
/// buttons, and a row that moved as its number did would slide out from under the hand pressing it. So the ▾ says what the rows
/// were last sorted by, as <i>People</i>'s says what they are. The rows move inside the grid, so the scroll stays where it was.
/// </remarks>
public partial class Main
{
    /// <summary>The order clicked, or null for the data's own.</summary>
    private ProfessionsOrder? _professionsOrder;

    private readonly List<(ProfessionsOrder Order, Label Head, string Word)> _professionHeads = new();

    /// <summary>The second and third columns' heads: three words, each a click away from being the sort.</summary>
    private void AddTheProfessionHeads(GridContainer table)
    {
        Label Head(ProfessionsOrder order, string word, string tooltip)
        {
            Label head = Muted(word);
            UiLook.Header(head); // column heads are headers (A121, Joe: "Yes, headers")
            head.MouseFilter = MouseFilterEnum.Stop;
            head.MouseDefaultCursorShape = CursorShape.PointingHand;
            head.TooltipText = tooltip;
            head.GuiInput += @event =>
            {
                if (@event is InputEventMouseButton { ButtonIndex: MouseButton.Left, Pressed: true })
                {
                    head.AcceptEvent();
                    SortTheProfessions(order);
                }
            };
            _professionHeads.Add((order, head, word));
            return head;
        }

        table.AddChild(Head(ProfessionsOrder.Type, "TYPE", "Sort by the trade, A to Z"));
        var numbers = new HBoxContainer();
        numbers.AddThemeConstantOverride("separation", 4);
        numbers.AddChild(Head(ProfessionsOrder.Assigned, "ASSIGNED", "Sort by how many you have asked for, most first"));
        numbers.AddChild(Muted("/"));
        numbers.AddChild(Head(ProfessionsOrder.Max, "MAX", "Sort by how many seats there are, most first"));
        table.AddChild(numbers);
        ShowTheProfessionsOrder();
    }

    /// <summary>A heading clicked: the order, the mark, the rows, and the file.</summary>
    private void SortTheProfessions(ProfessionsOrder order)
    {
        _professionsOrder = order;
        ShowTheProfessionsOrder();
        PutTheProfessionsInOrder();
        SettingsChanged();
    }

    /// <summary>The ▾ on the heading the rows were sorted by, and on no other — every heading as wide as with it (D816's rule).</summary>
    private void ShowTheProfessionsOrder()
    {
        foreach ((ProfessionsOrder order, Label head, string word) in _professionHeads)
        {
            string marked = word + " ▾";
            head.Text = _professionsOrder == order ? marked : word;
            float wide = head.GetThemeFont("font").GetStringSize(marked, HorizontalAlignment.Left, -1, head.GetThemeFontSize("font_size")).X;
            head.CustomMinimumSize = new Vector2(Mathf.Ceil(wide) + 1f, 0f);
        }
    }

    /// <summary>The trades as they would sort now, each with its three cells.</summary>
    private List<(ProfessionRow Row, Control[] Cells)> ProfessionRowsNow(SimWorld world)
    {
        var rows = new List<(ProfessionRow Row, Control[] Cells)>();
        foreach ((JobKind kind, Control[] cells) in _professionRows)
        {
            rows.Add((new ProfessionRow(kind, ProfessionName(world, kind), world.JobLimits.For(kind) ?? 0, SeatsFor(world, kind)), cells));
        }

        return rows;
    }

    /// <summary>The rows moved into the order clicked; none clicked, and they are left in the data's.</summary>
    private void PutTheProfessionsInOrder()
    {
        if (_professionsTable is null)
        {
            return;
        }

        List<(ProfessionRow Row, Control[] Cells)> rows = ProfessionRowsNow(_loop.World);
        if (_professionsOrder is ProfessionsOrder order)
        {
            rows.Sort((a, b) => ProfessionRow.Compare(a.Row, b.Row, order));
        }

        // The heads are the first row of three cells.
        int at = 3;
        foreach ((ProfessionRow _, Control[] cells) in rows)
        {
            foreach (Control cell in cells)
            {
                _professionsTable.MoveChild(cell, at++);
            }
        }
    }

    /// <summary>The trades in the order the table shows them, hidden ones left out — read off the grid, for the probe.</summary>
    private List<JobKind> TheProfessionsShown()
    {
        var shown = new List<JobKind>();
        foreach (Node cell in _professionsTable!.GetChildren())
        {
            int at = _professionRows.FindIndex(r => ReferenceEquals(r.Cells[0], cell));
            if (at >= 0 && _professionRows[at].Cells[0].Visible)
            {
                shown.Add(_professionRows[at].Kind);
            }
        }

        return shown;
    }

    /// <summary>
    /// ⭐ <c>professions sort:</c> (Joe's play note 6, D896) — posed trades, each order against a list typed by hand with a tie in
    /// each; then the real table: a click on each heading reorders it, whole and row by row, marks that heading alone, is
    /// captured, and moves neither the table's width nor the scroll; a press of + moves no row; and a load's order is applied.
    /// </summary>
    /// <remarks>⛔ The expected orders are written out, never computed by the comparer they check (D419).</remarks>
    private string TheProfessionsSortAsAsked()
    {
        var faults = new List<string>();
        if (_professionsTable is null || _professionsScroll is null)
        {
            return "[widths] professions sort: ⛔ no table";
        }

        // 3 and 1 tie on assigned (the name decides, case ignored), 4 and 2 on max, and no two orders agree.
        ProfessionRow[] posed =
        {
            new((JobKind)1, "Smith", 2, 1), new((JobKind)2, "baker", 0, 5), new((JobKind)3, "Forager", 2, 3), new((JobKind)4, "Hunter", 1, 5),
        };
        foreach ((ProfessionsOrder order, int[] wanted) in new[]
        {
            (ProfessionsOrder.Type, new[] { 2, 3, 4, 1 }),
            (ProfessionsOrder.Assigned, new[] { 3, 1, 4, 2 }),
            (ProfessionsOrder.Max, new[] { 2, 4, 3, 1 }),
        })
        {
            int[] got = posed.OrderBy(r => r, Comparer<ProfessionRow>.Create((a, b) => ProfessionRow.Compare(a, b, order))).Select(r => (int)r.Kind).ToArray();
            if (!got.SequenceEqual(wanted))
            {
                faults.Add($"posed by {order}: {string.Join(",", got)}, not {string.Join(",", wanted)}");
            }
        }

        ProfessionsOrder? was = _professionsOrder;
        SimWorld world = _loop.World;
        bool liveWas = _settingsLive;
        _settingsLive = false; // a probe's clicks are not the player's
        var lodge = new Workplace
        {
            Store = new Stockpile(world.GoodsCatalog.Count),
            Id = 990_003,
            Kind = JobKind.Hunter,
            Name = "Posed lodge",
            Position = world.StoreBuildings[0].Position,
            Capacity = 4,
        };
        var asked = _professionRows.Select(r => (r.Kind, world.JobLimits.For(r.Kind))).ToList();
        try
        {
            _professionsOrder = null;
            ShowTheProfessionsOrder();
            PutTheProfessionsInOrder();
            List<JobKind> dataOrder = TheProfessionsShown();

            // Numbers for the two number columns to sort: a founding asks for nobody and has no seat, and three equal orders say nothing.
            world.SetJobLimit(JobKind.Woodcutter, 3);
            world.Workplaces.Add(lodge);
            float wide = _professionsTable.GetCombinedMinimumSize().X;
            var orders = new HashSet<string>();
            foreach ((ProfessionsOrder order, Label head, string _) in _professionHeads)
            {
                head.EmitSignal(Control.SignalName.GuiInput, new InputEventMouseButton { ButtonIndex = MouseButton.Left, Pressed = true });
                List<JobKind> shown = TheProfessionsShown();
                List<JobKind> wanted = ProfessionRowsNow(world).Where(r => r.Cells[0].Visible).Select(r => r.Row)
                    .OrderBy(r => r, Comparer<ProfessionRow>.Create((a, b) => ProfessionRow.Compare(a, b, order))).Select(r => r.Kind).ToList();
                if (!shown.SequenceEqual(wanted) || shown.Count < 2)
                {
                    faults.Add($"clicking {order} shows {string.Join(",", shown)}");
                }

                // Every row is still its own three cells, in their columns.
                foreach ((JobKind kind, Control[] cells) in _professionRows)
                {
                    int at = cells[0].GetIndex();
                    if (at % 3 != 0 || at < 3 || cells[1].GetIndex() != at + 1 || cells[2].GetIndex() != at + 2)
                    {
                        faults.Add($"by {order}, {kind}'s cells stand at {at}, {cells[1].GetIndex()}, {cells[2].GetIndex()}");
                        break;
                    }
                }

                string marks = string.Join("", _professionHeads.Select(h => h.Head.Text.EndsWith('▾') ? h.Order.ToString() : string.Empty));
                if (marks != order.ToString())
                {
                    faults.Add($"by {order}, the ▾ is on '{marks}'");
                }

                if (CaptureSettings().ProfessionsSort != PlayerSettings.ProfessionsSorts[(int)order])
                {
                    faults.Add($"{order} is captured as {CaptureSettings().ProfessionsSort ?? "nothing"}");
                }

                if (Mathf.Abs(_professionsTable.GetCombinedMinimumSize().X - wide) > 0.5f)
                {
                    faults.Add($"by {order} the table is {_professionsTable.GetCombinedMinimumSize().X:F0} wide, not {wide:F0}");
                }

                orders.Add(string.Join(",", shown));
            }

            // A press of + moves no row: by assigned, the last row's + would lift it.
            _professionHeads[(int)ProfessionsOrder.Assigned].Head.EmitSignal(
                Control.SignalName.GuiInput, new InputEventMouseButton { ButtonIndex = MouseButton.Left, Pressed = true });
            List<JobKind> before = TheProfessionsShown();
            // ⛔ Through the row's own + (a mutant that re-sorted on the press scored zero while this set the number behind its back).
            Control[] last = _professionRows.Find(r => r.Kind == before[^1]).Cells;
            Button Step(int at) => (Button)last[2].GetChild(at);
            Step(2).EmitSignal(BaseButton.SignalName.Pressed);
            Refresh();
            RefreshTheStrip();
            bool wouldMove = !ProfessionRowsNow(world).Where(r => r.Cells[0].Visible).Select(r => r.Row)
                .OrderBy(r => r, Comparer<ProfessionRow>.Create((a, b) => ProfessionRow.Compare(a, b, ProfessionsOrder.Assigned))).Select(r => r.Kind)
                .SequenceEqual(before);
            if (!wouldMove || !TheProfessionsShown().SequenceEqual(before))
            {
                faults.Add($"a press of + moved the rows {!TheProfessionsShown().SequenceEqual(before)} (the pose needs it to change the order: {wouldMove})");
            }

            Step(0).EmitSignal(BaseButton.SignalName.Pressed);

            // A trade learned takes its place in the order.
            bool quarryWas = _quarryKnown;
            _quarryKnown = !quarryWas;
            RefreshTheStrip();
            bool tookItsPlace = TheProfessionsShown().SequenceEqual(ProfessionRowsNow(world).Where(r => r.Cells[0].Visible).Select(r => r.Row)
                .OrderBy(r => r, Comparer<ProfessionRow>.Create((a, b) => ProfessionRow.Compare(a, b, ProfessionsOrder.Assigned))).Select(r => r.Kind));
            _quarryKnown = quarryWas;
            RefreshTheStrip();
            if (!tookItsPlace)
            {
                faults.Add("a trade that appears does not take its place in the order");
            }

            // A load's order, through the settings.
            ApplySettings(CaptureSettings() with { ProfessionsSort = "type" });
            if (_professionsOrder != ProfessionsOrder.Type || !TheProfessionsShown().Select(k => ProfessionName(world, k)).SequenceEqual(
                TheProfessionsShown().Select(k => ProfessionName(world, k)).Order(StringComparer.OrdinalIgnoreCase)))
            {
                faults.Add("the settings' order is not applied to the table");
            }

            if (orders.Count != 3 || dataOrder.SequenceEqual(TheProfessionsShown()))
            {
                faults.Add($"vacuous: {orders.Count} different orders, or the data's own is already A to Z");
            }
        }
        finally
        {
            world.Workplaces.Remove(lodge);
            foreach ((JobKind kind, int? limit) in asked)
            {
                if (limit is int n)
                {
                    world.SetJobLimit(kind, n);
                }
            }

            _professionsOrder = was;
            ShowTheProfessionsOrder();
            PutTheProfessionsInOrder();
            _settingsLive = liveWas;
        }

        return faults.Count == 0
            ? $"[widths] professions sort: ✅ Type, Assigned and Max each sort a posed table as typed and the real one as clicked, marked on that heading alone, the table no wider; "
              + "a press of + moves no row, a trade learned takes its place, and the order is captured and applied"
            : $"[widths] professions sort: ⛔ {string.Join("; ", faults)}";
    }
}

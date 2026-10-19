using Bclone.Sim.Core;
using Godot;

namespace Bclone.Game;

/// <summary>
/// ⭐ The region panel (`specs/trade.md §4.2`, commit 7 — View II): each neighbour, which way it lies, what it has plenty of
/// and is short of, and its rates for the village's own orders in words — <c>SimWorld.SayTheRegion</c>, written as it
/// stands. A window, opened from the post's window, until the town hall's tabs land.
/// </summary>
/// <remarks>
/// The writer (<see cref="WriteTheRegionWindow"/>) takes the words, so the probe can pose a full region without a post
/// standing; the words themselves are the sim's, guarded there (<c>TradeWordsTests.TheRegionSaysEachNeighbourAndItsRates</c>).
/// </remarks>
public partial class Main
{
    private sealed class RegionWindow
    {
        public required PanelContainer Panel { get; init; }
        public required VBoxContainer Column { get; init; }
        public required Label Asleep { get; init; }

        /// <summary>A neighbour's name and its words, made as many as the region has.</summary>
        public List<(Label Name, Label Words)> Rows { get; } = new();
    }

    private RegionWindow? _regionWindow;

    /// <summary>The window's width, the words wrapped inside it.</summary>
    private const float RegionWindowWidth = 520f;

    /// <summary>The tallest it may stand — the probe holds it to this, as it holds the post's.</summary>
    private const float RegionWindowTallest = 480f;

    /// <summary>What the window says while the region sleeps.</summary>
    private const string TheRegionSleeps = "The region wakes the season after a trading post stands — then its neighbours, "
        + "what they have plenty of and what they are short of, are said here.";

    /// <summary>Open the region's window over the map (the post's window's button).</summary>
    private void OpenTheRegionWindow()
    {
        RegionWindow w = EnsureTheRegionWindow();
        if (!w.Panel.Visible)
        {
            w.Panel.Position = new Vector2(Edge + DefaultPanelWidth + CardWidth + 64f, TopOfTheLeftColumn() + 58f);
        }

        w.Panel.Visible = true;
        RaiseToTheFront(w.Panel);
        RefreshTheRegionWindow(_loop.World);
    }

    private RegionWindow EnsureTheRegionWindow()
    {
        if (_regionWindow is not null)
        {
            return _regionWindow;
        }

        var panel = new PanelContainer { MouseFilter = MouseFilterEnum.Stop, Visible = false, CustomMinimumSize = new Vector2(RegionWindowWidth, 0) };
        panel.AddThemeStyleboxOverride("panel", CardSkin(true));
        var column = new VBoxContainer();
        column.AddThemeConstantOverride("separation", 6);
        panel.AddChild(column);

        var head = new HBoxContainer { MouseFilter = MouseFilterEnum.Stop };
        head.AddThemeConstantOverride("separation", 6);
        Label grip = Muted("⋮⋮");
        grip.MouseFilter = MouseFilterEnum.Stop;
        grip.MouseDefaultCursorShape = CursorShape.Move;
        head.AddChild(grip);
        Label title = Body("the region — the neighbours on the river and beyond");
        title.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        title.MouseFilter = MouseFilterEnum.Stop;
        head.AddChild(title);
        var close = new Button { Text = "✕", Flat = true, TooltipText = "Close" };
        head.AddChild(close);
        column.AddChild(head);

        // ⚠️ Every wrapped label gets the width it wraps at — the post's window stood 641 tall without (D597).
        Label asleep = Wrapped(Muted(TheRegionSleeps));
        asleep.CustomMinimumSize = new Vector2(RegionWindowWidth - 40f, 0);
        column.AddChild(asleep);

        MakeDraggable(grip, panel);
        MakeDraggable(title, panel);
        close.Pressed += () => panel.Visible = false;
        panel.MouseEntered += () => RaiseToTheFront(panel);
        AddChild(panel);
        _regionWindow = new RegionWindow { Panel = panel, Column = column, Asleep = asleep };
        return _regionWindow;
    }

    /// <summary>Write the window as the region stands, if it is open.</summary>
    private void RefreshTheRegionWindow(SimWorld world)
    {
        if (_regionWindow is { Panel.Visible: true } w)
        {
            WriteTheRegionWindow(w, world.SayTheRegion());
        }
    }

    /// <summary>A neighbour a row — its name, then its words wrapped under it; the sleeping sentence while there are none.</summary>
    private static void WriteTheRegionWindow(RegionWindow w, IReadOnlyList<(string Name, string Words)> said)
    {
        w.Asleep.Visible = said.Count == 0;
        while (w.Rows.Count < said.Count)
        {
            Label name = Heading(string.Empty);
            Label words = Wrapped(Body(string.Empty));
            words.CustomMinimumSize = new Vector2(RegionWindowWidth - 40f, 0);
            w.Column.AddChild(name);
            w.Column.AddChild(words);
            w.Rows.Add((name, words));
        }

        for (int i = 0; i < w.Rows.Count; i++)
        {
            bool shown = i < said.Count;
            w.Rows[i].Name.Visible = shown;
            w.Rows[i].Words.Visible = shown;
            if (shown)
            {
                w.Rows[i].Name.Text = said[i].Name;
                w.Rows[i].Words.Text = said[i].Words;
            }
        }
    }
}

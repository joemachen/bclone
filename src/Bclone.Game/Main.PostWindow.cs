using Bclone.Sim.Core;
using Bclone.Sim.World;
using Godot;

namespace Bclone.Game;

/// <summary>
/// ⭐ The trading post's window (D597, `specs/trade.md §4.5`; Joe, D587 call 6) — opened from the post's card.
/// </summary>
/// <remarks>
/// <para>
/// <b>Joe, playing D596:</b> <i>"trading post window is way too long. do multiple columns or something? the colors in the
/// trading post window dont tell me anything. they need text labels too."</i> The orders lived in the card's Settings fold,
/// 268 wide: the dropdown and the number took every pixel and each good's name clipped to nothing — and the probe, which
/// measured minimum widths, passed, because a clipped label's minimum is zero. So the orders moved to a window of their
/// own, in <b>two columns, every good named in words</b>, and the probe now asks that every name shows.
/// </para>
/// <para>
/// ⭐ <b>One window for the village</b>, not one per post: the orders are the village's (<c>TradeKeep</c>,
/// <c>TradeBuy</c>, <c>TradeBuyHead</c>), whichever post the merchant carries them to. Its controls act on the world;
/// the writer (<see cref="WriteThePostsWindow"/>) takes facts, so the probe can pose it without a post standing.
/// </para>
/// </remarks>
public partial class Main
{
    /// <summary>The window's controls, built once the first time it is opened.</summary>
    private sealed class PostWindow
    {
        public required PanelContainer Panel { get; init; }
        public required Label Title { get; init; }
        public required Label Note { get; init; }
        public required Label BoatLine { get; init; }
        public required Button SendOn { get; init; }
        public required Label Verdict { get; init; }
        public required CheckBox HeadOn { get; init; }
        public required SpinBox HeadAmount { get; init; }

        /// <summary>One row a good: its name in words, what the village holds, the order's kind and its number.</summary>
        public List<(Goods Goods, HBoxContainer Row, Label Name, Label Held, OptionButton Order, SpinBox Amount)> Orders { get; } = new();

        /// <summary>The post the window was opened from — its name in the title, and the window closes if it goes.</summary>
        public int PostId { get; set; }
    }

    private PostWindow? _postWindow;

    /// <summary>The order dropdown's items, by index.</summary>
    private const int NoOrder = 0;
    private const int SellAbove = 1;
    private const int BuyUpTo = 2;

    /// <summary>Open the post's window over the map, for this post (the card's button).</summary>
    private void OpenThePostsWindow(int postId)
    {
        PostWindow w = EnsureThePostsWindow();
        w.PostId = postId;
        if (!w.Panel.Visible)
        {
            w.Panel.Position = new Vector2(Edge + DefaultPanelWidth + CardWidth + 32f, TopOfTheLeftColumn() + 26f);
        }

        w.Panel.Visible = true;
        RaiseToTheFront(w.Panel);
        RefreshThePostsWindow(_loop.World);
    }

    /// <summary>Build the window once — every good a row, in two columns, named.</summary>
    private PostWindow EnsureThePostsWindow()
    {
        if (_postWindow is not null)
        {
            return _postWindow;
        }

        SimWorld world = _loop.World;
        // ⚠️ A FIXED WIDTH, so the two sentences wrap at it: unconstrained, a wrapping label measures its height at its
        // narrowest and the window stood 641 tall (D597's first probe).
        var panel = new PanelContainer { MouseFilter = MouseFilterEnum.Stop, Visible = false, CustomMinimumSize = new Vector2(PostWindowWidth, 0) };
        panel.AddThemeStyleboxOverride("panel", CardSkin(true));
        var column = new VBoxContainer();
        column.AddThemeConstantOverride("separation", 6);
        panel.AddChild(column);

        // ---- head: grip · title · ✕ (a card's) ----
        var head = new HBoxContainer { MouseFilter = MouseFilterEnum.Stop };
        head.AddThemeConstantOverride("separation", 6);
        Label grip = Muted("⋮⋮");
        grip.MouseFilter = MouseFilterEnum.Stop;
        grip.MouseDefaultCursorShape = CursorShape.Move;
        head.AddChild(grip);
        Label title = Body("the trading post");
        title.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        title.MouseFilter = MouseFilterEnum.Stop;
        head.AddChild(title);
        var close = new Button { Text = "✕", Flat = true, TooltipText = "Close" };
        head.AddChild(close);
        column.AddChild(head);

        // ⚠️ Each sentence gets the width it wraps at: an autowrapping label measures its height at its own width, and
        // with none it stood a word a line — 165 and 137 tall (D597's probe, part by part).
        Label note = Wrapped(Body(string.Empty));
        note.CustomMinimumSize = new Vector2(PostWindowWidth - 40f, 0);
        column.AddChild(note);

        var boat = new HBoxContainer();
        boat.AddThemeConstantOverride("separation", 8);
        Label boatLine = Wrapped(Muted(string.Empty));
        boatLine.CustomMinimumSize = new Vector2(PostWindowWidth - 200f, 0);
        boatLine.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        boat.AddChild(boatLine);
        var sendOn = new Button { Text = "Send the boat on" };
        boat.AddChild(sendOn);
        column.AddChild(boat);

        Label verdict = Wrapped(Muted(string.Empty));
        verdict.CustomMinimumSize = new Vector2(PostWindowWidth - 40f, 0);
        verdict.Visible = false;
        column.AddChild(verdict);

        // ⭐ Two columns, every good named (Joe, D597).
        var grid = new GridContainer { Columns = 2 };
        grid.AddThemeConstantOverride("h_separation", 18);
        grid.AddThemeConstantOverride("v_separation", 4);
        column.AddChild(grid);

        var w = new PostWindow
        {
            Panel = panel,
            Title = title,
            Note = note,
            BoatLine = boatLine,
            SendOn = sendOn,
            Verdict = verdict,
            HeadOn = new CheckBox { Text = "buy up to", TooltipText = "head the village's herds should hold" },
            HeadAmount = new SpinBox { MinValue = 0, MaxValue = 1_000, Step = 1, CustomMinimumSize = new Vector2(PostAmountWidth, 0) },
        };

        for (int g = 0; g < world.GoodsCatalog.Count; g++)
        {
            var goods = (Goods)g;
            var row = new HBoxContainer { Visible = false };
            row.AddThemeConstantOverride("separation", 4);
            row.AddChild(Chip(ChipColour(goods)));
            Label name = Body(GoodsName(world, goods));
            name.CustomMinimumSize = new Vector2(PostNameWidth, 0);
            name.SizeFlagsHorizontal = SizeFlags.ExpandFill;
            name.ClipText = true;
            name.TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis;
            row.AddChild(name);
            Label held = Amount(PostHeldWidth);
            held.TooltipText = $"what the village holds of {GoodsName(world, goods)}, not counting the post";
            row.AddChild(held);
            var order = new OptionButton { FitToLongestItem = false, CustomMinimumSize = new Vector2(PostOrderWidth, 0), ClipText = true };
            order.AddItem("—", NoOrder);
            order.AddItem("sell above", SellAbove);
            order.AddItem("buy up to", BuyUpTo);
            row.AddChild(order);
            var amount = new SpinBox { MinValue = 0, MaxValue = 100_000, Step = 10, CustomMinimumSize = new Vector2(PostAmountWidth, 0) };
            row.AddChild(amount);
            order.ItemSelected += index => ActInThePostsWindow(() => SetTheOrder(goods, (int)index, (int)amount.Value));
            amount.ValueChanged += value => ActInThePostsWindow(() => SetTheOrder(goods, order.Selected, (int)value));
            grid.AddChild(row);
            w.Orders.Add((goods, row, name, held, order, amount));
        }

        // The beasts' row, a good's shape: their name, then the order and its number in head.
        var beasts = new HBoxContainer();
        beasts.AddThemeConstantOverride("separation", 4);
        Label herd = Body(BeastsCaption(world));
        herd.CustomMinimumSize = new Vector2(PostNameWidth, 0);
        herd.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        beasts.AddChild(herd);
        beasts.AddChild(w.HeadOn);
        beasts.AddChild(w.HeadAmount);
        column.AddChild(beasts);

        // ---- wiring ----
        MakeDraggable(grip, panel);
        MakeDraggable(title, panel);
        close.Pressed += () => panel.Visible = false;
        sendOn.Pressed += () => ActInThePostsWindow(() => SayTheVerdict(_loop.World.SendTheBoatOn()));
        w.HeadOn.Toggled += on => ActInThePostsWindow(() => SayTheVerdict(_loop.World.SetTradeBuyHead(on ? (int)w.HeadAmount.Value : null)));
        w.HeadAmount.ValueChanged += value => ActInThePostsWindow(() =>
        {
            if (_loop.World.TradeBuyHead is not null)
            {
                SayTheVerdict(_loop.World.SetTradeBuyHead((int)value));
            }
        });
        panel.MouseEntered += () => RaiseToTheFront(panel);
        AddChild(panel);
        _postWindow = w;
        return w;
    }

    /// <summary>Two columns of rows and the gap between them — the rows' own widths, measured by the probe (716).</summary>
    private const float PostWindowWidth = 720f;

    /// <summary>The tallest the window may stand — the probe holds it to this (Joe, D597: <i>"way too long"</i>).</summary>
    private const float PostWindowTallest = 480f;

    private const float PostNameWidth = 84f;
    private const float PostHeldWidth = 52f;
    private const float PostOrderWidth = 94f;
    private const float PostAmountWidth = 66f;

    private static string BeastsCaption(SimWorld world) =>
        world.Config.Animals.Count == 0 ? "beasts" : world.Config.Animals[0].Plural;

    /// <summary>Act on the world from the window, then write the window and the cards afresh.</summary>
    private void ActInThePostsWindow(System.Action action)
    {
        action();
        RefreshCards(_loop.World);
    }

    /// <summary>Set one good's order from its row: the dropdown's kind and the row's number.</summary>
    private void SetTheOrder(Goods goods, int kind, int amount)
    {
        SimWorld world = _loop.World;
        PlacementVerdict verdict = kind switch
        {
            SellAbove => world.SetTradeBuy(goods, null) is var cleared && cleared.Allowed ? world.SetTradeKeep(goods, amount) : cleared,
            BuyUpTo => world.SetTradeKeep(goods, null) is var cleared && cleared.Allowed ? world.SetTradeBuy(goods, amount) : cleared,
            _ => Both(world.SetTradeKeep(goods, null), world.SetTradeBuy(goods, null)),
        };
        SayTheVerdict(verdict);

        static PlacementVerdict Both(PlacementVerdict keep, PlacementVerdict buy) => keep.Allowed ? buy : keep;
    }

    /// <summary>The last order's refusal or warning, under the window's sentence — empty when it was simply done.</summary>
    private void SayTheVerdict(PlacementVerdict verdict)
    {
        if (_postWindow is not PostWindow w)
        {
            return;
        }

        w.Verdict.Text = !verdict.Allowed ? verdict.Reason : verdict.Warning;
        w.Verdict.Visible = w.Verdict.Text.Length > 0;
    }

    /// <summary>One good's line in the post's window.</summary>
    private readonly record struct OrderFacts(bool Shown, int Held, int? Keep, int? Buy, string Waits);

    /// <summary>What the post's window says — read from the sim once, written by <see cref="WriteThePostsWindow"/>.</summary>
    private readonly record struct PostFacts(string Title, string Note, string BoatLine, bool Docked, OrderFacts[] Orders, int? Head);

    /// <summary>Write the window as the village stands, if it is open; close it if its post is gone.</summary>
    private void RefreshThePostsWindow(SimWorld world)
    {
        if (_postWindow is not { Panel.Visible: true } w)
        {
            return;
        }

        if (world.FindWorkplace(w.PostId) is not { Kind: JobKind.Merchant } post)
        {
            w.Panel.Visible = false;
            return;
        }

        var orders = new OrderFacts[w.Orders.Count];
        for (int i = 0; i < w.Orders.Count; i++)
        {
            Goods goods = w.Orders[i].Goods;
            int? keep = world.TradeKeep.For(goods), buy = world.TradeBuy.For(goods);
            orders[i] = new OrderFacts(
                GoodKnown(goods) || keep is not null || buy is not null,
                world.InStores(goods),
                keep,
                buy,
                buy is not null && !world.ABoatCanBring(goods) ? $"No boat brings {GoodsName(world, goods)} — the order waits for carts." : string.Empty);
        }

        string due = world.SayWhenTheBoatIsDue();
        string boatLine = world.Boat is TradeBoat boat && world.Tick >= boat.AppearsAtTick && world.Tick <= boat.LeavesAtTick
            ? $"{due} It carries {WhatTheBoatCarries(world, boat)}."
            : due;
        WriteThePostsWindow(w, new PostFacts(post.Name, world.SayWhatThePostWillDo(), boatLine, world.BoatIsDocked, orders, world.TradeBuyHead));
    }

    private static void WriteThePostsWindow(PostWindow w, PostFacts f)
    {
        w.Title.Text = $"{f.Title} — what the village sells and buys";
        w.Note.Text = f.Note;
        w.SendOn.Disabled = !f.Docked;
        w.SendOn.TooltipText = f.Docked ? "The boat leaves on the next tick." : "No boat is tied up at the post.";
        w.BoatLine.Text = f.BoatLine;

        for (int i = 0; i < w.Orders.Count; i++)
        {
            (Goods _, HBoxContainer row, Label _, Label held, OptionButton order, SpinBox amount) = w.Orders[i];
            OrderFacts o = f.Orders[i];
            row.Visible = o.Shown;
            if (!o.Shown)
            {
                continue;
            }

            held.Text = o.Held.ToString("N0", System.Globalization.CultureInfo.InvariantCulture);
            int kind = o.Keep is not null ? SellAbove : o.Buy is not null ? BuyUpTo : NoOrder;
            if (order.Selected != kind)
            {
                order.Select(kind);
            }

            if ((o.Keep ?? o.Buy) is int set)
            {
                amount.SetValueNoSignal(set);
            }

            amount.Editable = kind != NoOrder;
            order.TooltipText = o.Waits;
        }

        w.HeadOn.SetPressedNoSignal(f.Head is not null);
        if (f.Head is int head)
        {
            w.HeadAmount.SetValueNoSignal(head);
        }

        w.HeadAmount.Editable = f.Head is not null;
    }

    /// <summary>What a boat on the river carries, in words — its cargo, derived (`SimWorld.CargoOf`).</summary>
    private static string WhatTheBoatCarries(SimWorld world, TradeBoat boat)
    {
        (int[] goods, int animals) = world.CargoOf(boat);
        var parts = new List<string>();
        for (int g = 0; g < goods.Length; g++)
        {
            if (goods[g] > 0)
            {
                parts.Add($"{goods[g]:N0} {world.GoodsCatalog.NameOf((Goods)g)}");
            }
        }

        if (animals > 0 && world.Config.Animals.Count > 0)
        {
            parts.Add($"{animals} {(animals == 1 ? world.Config.Animals[0].Name : world.Config.Animals[0].Plural)}");
        }

        return parts.Count == 0 ? "nothing" : string.Join(", ", parts);
    }
}

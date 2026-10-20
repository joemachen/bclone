using Bclone.Sim.Core;
using Bclone.Sim.World;
using Godot;

namespace Bclone.Game;

/// <summary>
/// ⭐ The probe's lines for trade and the herd (D596, commit 6 of `specs/trade.md §11`): <c>herd:</c> and <c>post:</c>.
/// </summary>
/// <remarks>
/// <para>
/// ⚠️ <b>The view cannot raise a barn or a post</b> — <c>SimWorld.Complete</c> and the hall are the sim's, and a probe hook
/// with a side effect is what D160 deleted. So each line poses <b>facts</b> into the same writers the game uses
/// (<c>WriteBarnCard</c>, <c>WriteThePostsWindow</c>, <c>VillageMap.HerdSpots</c>) — the library's and the hall's shape —
/// and drives the post's real controls against the real world, where the order setters and <i>Send the boat on</i> answer.
/// </para>
/// <para>
/// Run last but one (before the error boundary), so the orders it sets and clears can disturb no other line.
/// </para>
/// </remarks>
public partial class Main
{
    /// <summary>
    /// <c>herd:</c> — the barn on the Food strip and the post on Storage, drawn in their trades' colours; a herd grazes
    /// only its own pasture, a circle a head, still on a still clock and moving on a running one; the barn's card reads
    /// the head, the room and the hay in winter days.
    /// </summary>
    private string TheHerdGrazesItsPasture()
    {
        var faults = new List<string>();
        if (CategoryOf(BuildingKind.Barn, true) != BuildCategory.Food)
        {
            faults.Add($"the barn is on {CategoryOf(BuildingKind.Barn, true)}, not Food");
        }

        if (CategoryOf(BuildingKind.TradingPost, true) != BuildCategory.Storage)
        {
            faults.Add($"the post is on {CategoryOf(BuildingKind.TradingPost, true)}, not Storage");
        }

        if (VillageMap.ColourOf(StoreKind.Barn) != TradeGlyph.ColourOf(JobKind.Herdsman)
            || VillageMap.ColourOf(StoreKind.TradingPost) != TradeGlyph.ColourOf(JobKind.Merchant))
        {
            faults.Add("the barn or the post does not draw in its trade's colour");
        }

        // A 4 × 3 pasture at (10, 10), and a herd of five on it.
        var pasture = new List<GridPos>();
        for (int y = 10; y < 13; y++)
        {
            for (int x = 10; x < 14; x++)
            {
                pasture.Add(new GridPos(x, y));
            }
        }

        var owned = new HashSet<GridPos>(pasture);
        List<Vector2> now = VillageMap.HerdSpots(7, 5, i => pasture[i], pasture.Count, owned.Contains, 1000.0);
        List<Vector2> again = VillageMap.HerdSpots(7, 5, i => pasture[i], pasture.Count, owned.Contains, 1000.0);
        List<Vector2> later = VillageMap.HerdSpots(7, 5, i => pasture[i], pasture.Count, owned.Contains, 1400.0);
        int off = now.Concat(later).Count(s => !owned.Contains(new GridPos(Mathf.RoundToInt(s.X), Mathf.RoundToInt(s.Y))));
        int moved = now.Zip(later).Count(p => p.First.DistanceTo(p.Second) > 0.05f);
        if (now.Count != 5)
        {
            faults.Add($"a herd of five drew {now.Count}");
        }

        if (off > 0)
        {
            faults.Add($"{off} beasts stood off their pasture");
        }

        if (!now.SequenceEqual(again))
        {
            faults.Add("the same clock drew the herd in two places");
        }

        if (moved == 0)
        {
            faults.Add("the herd never moved as the clock ran");
        }

        if (VillageMap.HerdSpots(7, 5, i => pasture[i], 0, owned.Contains, 1000.0).Count != 0)
        {
            faults.Add("a barn with no pasture drew beasts");
        }

        // The barn's card, posed through its writer on the first workplace's card.
        string sentence = "5 head grazing 40 tiles (room for 8) — hay for 30 winter days of 30; the herdsman is cutting the pasture.";
        if (WithAPosedCard(card =>
            {
                WriteBarnCard(card, new BarnFacts(sentence, true, 5, 8, 40, 300, 30));
                ForceUpdateTransform();
                return card.Status.Text != sentence ? $"its status reads \"{card.Status.Text}\""
                    : card.Values[1].Text != "5 / 8" ? $"its head reads \"{card.Values[1].Text}\""
                    : card.Keys[2].Text != "hay · 30 winter days" ? $"its hay reads \"{card.Keys[2].Text}\""
                    : card.Panel.GetCombinedMinimumSize().X > CardWidth + 1f ? $"it widens to {card.Panel.GetCombinedMinimumSize().X:F0}"
                    : null;
            }) is string barnFault)
        {
            faults.Add("the barn's card: " + barnFault);
        }

        return faults.Count == 0
            ? $"[widths] herd: ✅ the barn on Food and the post on Storage in their trades' colours; five head on their own pasture, still on a still clock, {moved} of them moved as it ran; the barn's card reads 5 / 8 head and 30 winter days of hay, and fits"
            : $"[widths] herd: ❌ {string.Join("; ", faults)}";
    }

    /// <summary>
    /// <c>post:</c> — the post's window (D597) posed at its fullest: two columns, <b>every good's name showing in words</b>
    /// (Joe: <i>"the colors … dont tell me anything. they need text labels"</i> — D596's rows clipped every name to nothing,
    /// and this line measured only widths, so it passed), the window inside the game's, its orders read back; then its
    /// real controls against the real world: a good's dropdown sells, buys and clears it (never both at once), <i>Send the
    /// boat on</i> with no boat docked says why, and an order for beasts with no pasture is refused with the reason.
    /// </summary>
    private string ThePostsWindowWorks()
    {
        SimWorld world = _loop.World;
        PostWindow w = EnsureThePostsWindow();
        bool wasOpen = w.Panel.Visible;
        w.Panel.Visible = true;
        try
        {
            var orders = new OrderFacts[w.Orders.Count];
            for (int i = 0; i < orders.Length; i++)
            {
                Goods goods = w.Orders[i].Goods;
                orders[i] = new OrderFacts(
                    true,
                    123_456,
                    goods == Goods.Firewood ? 400 : null,
                    goods == Goods.Stone ? 50 : null,
                    goods == Goods.Stone ? "No boat brings stone — the order waits for carts." : string.Empty);
            }

            WriteThePostsWindow(w, new PostFacts(
                new string('P', SimWorld.NameLengthLimit),
                string.Join(
                    '\n',
                    "Ashbourne, up the river — plenty of logs, stone, iron, stone tools, iron tools, leather and cattle; short of wheat, flour, bread, fish and firewood.",
                    "Millbrook, down the river — plenty of forage, fish, wheat, flour, bread and firewood; short of logs, stone, iron, leather and meat.",
                    "Thornfield, overland, no road yet — plenty of meat, leather, hay, iron tools and cattle; short of firewood, bread, wheat and stone."),
                "The merchant carries what is above each keep down to the post; the next boat takes enough of it to pay for what you asked.",
                "The boat from Ashford is tied up at the post for 10 more days. It carries 1,234 stone, 56 leather, 7 iron tools and 3 cattle.",
                true,
                orders,
                4));
            ForceUpdateTransform();

            // ⭐ Every name shows, in words — its text is its good's, and it keeps the width a name needs.
            var nameless = w.Orders
                .Where(o => !o.Name.Visible || o.Name.Text != GoodsName(world, o.Goods) || o.Name.GetCombinedMinimumSize().X < PostNameWidth - 1f)
                .Select(o => GoodsName(world, o.Goods))
                .ToList();
            (Goods _, HBoxContainer _, Label _, Label _, OptionButton fire, SpinBox fireAmount) = w.Orders[(int)Goods.Firewood];
            (Goods _, HBoxContainer _, Label _, Label _, OptionButton stone, SpinBox _) = w.Orders[(int)Goods.Stone];
            Vector2 wants = w.Panel.GetCombinedMinimumSize();
            string? posed = nameless.Count > 0 ? $"{nameless.Count} goods show no name ({string.Join(", ", nameless.Take(3))})"
                : w.Orders.Count(o => o.Row.Visible) != orders.Length ? "not every good's row shows"
                : fire.Selected != SellAbove || (int)fireAmount.Value != 400 ? $"firewood reads {fire.Selected} at {fireAmount.Value}"
                : stone.Selected != BuyUpTo ? $"stone reads {stone.Selected}"
                : !w.Region.Visible || w.Region.Text.Split('\n').Length != 3 ? "the region's three lines do not show at the top (D607)"
                : w.SendOn.Disabled ? "Send the boat on is disabled with a boat docked"
                : !w.HeadOn.ButtonPressed ? "the order for beasts is not shown"
                : wants.X > Size.X - (2f * Edge) ? $"the window wants {wants.X:F0} of a {Size.X:F0} game"
                : wants.Y > PostWindowTallest ? $"the window is {wants.Y:F0} tall, past {PostWindowTallest:F0} — too long again ({string.Join(", ", w.Panel.GetChild(0).GetChildren().OfType<Control>().Select(k => $"{k.GetClass()} {k.GetCombinedMinimumSize().Y:F0}"))})"
                : null;
            if (posed is not null)
            {
                return $"[widths] post: ❌ {posed}";
            }

            // ⭐ The real controls against the real world.
            fireAmount.SetValueNoSignal(300);
            fire.Select(SellAbove);
            fire.EmitSignal(OptionButton.SignalName.ItemSelected, (long)SellAbove);
            bool sold = world.TradeKeep.For(Goods.Firewood) == 300 && world.TradeBuy.For(Goods.Firewood) is null;
            fire.Select(BuyUpTo);
            fire.EmitSignal(OptionButton.SignalName.ItemSelected, (long)BuyUpTo);
            bool bought = world.TradeKeep.For(Goods.Firewood) is null && world.TradeBuy.For(Goods.Firewood) == 300;
            fire.Select(NoOrder);
            fire.EmitSignal(OptionButton.SignalName.ItemSelected, (long)NoOrder);
            bool cleared = world.TradeKeep.For(Goods.Firewood) is null && world.TradeBuy.For(Goods.Firewood) is null;

            w.SendOn.EmitSignal(BaseButton.SignalName.Pressed);
            string sendSaid = w.Verdict.Text;
            bool hadNoPasture = world.SetTradeBuyHead(1).Allowed is false;
            world.SetTradeBuyHead(null);
            w.HeadOn.EmitSignal(BaseButton.SignalName.Toggled, true);
            string headSaid = w.Verdict.Text;
            if (!hadNoPasture)
            {
                world.SetTradeBuyHead(null);
            }

            string? driven = !sold ? "choosing sell above did not set the keep"
                : !bought ? "choosing buy up to did not swap the keep for a buy"
                : !cleared ? "choosing — left an order"
                : world.BoatIsDocked || sendSaid != "No boat is tied up at the post." ? $"Send the boat on with no boat said \"{sendSaid}\""
                : hadNoPasture && !headSaid.Contains("pasture", System.StringComparison.Ordinal) ? $"beasts with no pasture said \"{headSaid}\""
                : null;
            return driven is null
                ? $"[widths] post: ✅ the window in two columns, {orders.Length} goods each named in words, {wants.X:F0} × {wants.Y:F0} inside the game's {Size.X:F0} × {Size.Y:F0}; its orders read back; a good's dropdown sells, buys and clears it, never both; Send the boat on with no boat says why; beasts with no pasture are refused with the reason"
                : $"[widths] post: ❌ {driven}";
        }
        finally
        {
            w.Panel.Visible = wasOpen;
        }
    }

    /// <summary>
    /// <c>region:</c> (D606) — the region's window posed at its fullest (three neighbours with the longest names and every
    /// sentence they can say), each name showing and its words wrapped inside the window; asleep, the one sentence that says
    /// when it wakes; and the post's window's button opens it.
    /// </summary>
    private string TheRegionIsSaid()
    {
        RegionWindow w = EnsureTheRegionWindow();
        bool wasOpen = w.Panel.Visible;
        w.Panel.Visible = true;
        try
        {
            string words = "Up the river. Has plenty of stone, iron, iron tools, leather and cattle. Running low on bread. "
                + "Short of firewood, fish, wheat and flour. Has enough logs. Ashford is short of firewood and has plenty of "
                + "stone: 3 firewood buys 1 stone. Ashford is short of fish and has plenty of iron: 7 fish buys 2 iron.";
            var said = Enumerable.Range(0, 3).Select(n => (new string((char)('A' + n), SimWorld.NameLengthLimit), words)).ToList();
            WriteTheRegionWindow(w, said);
            ForceUpdateTransform();
            Vector2 wants = w.Panel.GetCombinedMinimumSize();
            var nameless = w.Rows.Take(3).Where(r => !r.Name.Visible || r.Name.Text.Length == 0 || r.Name.GetCombinedMinimumSize().X < 10f).Count();
            string? fault = nameless > 0 ? $"{nameless} neighbours show no name"
                : w.Rows.Take(3).Any(r => !r.Words.Visible || r.Words.Text != words) ? "a neighbour's words do not show"
                : w.Asleep.Visible ? "the sleeping sentence shows over a woken region"
                : wants.X > Size.X - (2f * Edge) ? $"the window wants {wants.X:F0} of a {Size.X:F0} game"
                : wants.Y > RegionWindowTallest ? $"the window is {wants.Y:F0} tall with three neighbours, past {RegionWindowTallest:F0}"
                : null;
            if (fault is not null)
            {
                return $"[widths] region: ❌ {fault}";
            }

            WriteTheRegionWindow(w, System.Array.Empty<(string, string)>());
            if (!w.Asleep.Visible || w.Rows.Any(r => r.Name.Visible || r.Words.Visible))
            {
                return "[widths] region: ❌ asleep, the window still shows neighbours or not the sentence that says when it wakes";
            }

            // The post's window's button opens it.
            w.Panel.Visible = false;
            PostWindow post = EnsureThePostsWindow();
            Button? button = post.Panel.FindChildren("*", nameof(Button), true, false).OfType<Button>().FirstOrDefault(b => b.Text == "The region");
            button?.EmitSignal(BaseButton.SignalName.Pressed);
            return button is null ? "[widths] region: ❌ the post's window has no button for the region"
                : !w.Panel.Visible ? "[widths] region: ❌ the post's window's button did not open the region"
                : $"[widths] region: ✅ three neighbours with the longest names, each named and its words wrapped, {wants.X:F0} × {wants.Y:F0}; asleep, the sentence that says when it wakes; the post's window opens it";
        }
        finally
        {
            w.Panel.Visible = wasOpen;
        }
    }

    /// <summary>
    /// <c>fence:</c> (D599, D610) — a posed round pasture's fence drawn <b>round</b>: one open line, no corner sharper than
    /// 45°, every point within half a tile of a wall, every wall reached, and the gap exactly at the gate; a yard's fence
    /// side on its tile's real edge; and the barn card's fence row in each state: <i>Fence the pasture</i> (disabled with
    /// its reason when refused), <i>Cancel the fence</i>, <i>Take the fence down</i>. The view cannot raise a barn (D596),
    /// so both are posed facts.
    /// </summary>
    private string TheFenceIsDrawnAndItsButtonFollowsIt()
    {
        var faults = new List<string>();

        // A disc 3.6 tiles across its radius, as the round brush paints one, with the gate on the east of its middle row.
        var pasture = new List<GridPos>();
        for (int y = 15; y <= 25; y++)
        {
            for (int x = 15; x <= 25; x++)
            {
                if (((x - 20) * (x - 20)) + ((y - 20) * (y - 20)) <= 12.96f)
                {
                    pasture.Add(new GridPos(x, y));
                }
            }
        }

        var gate = new GridPos(24, 20);
        List<(GridPos From, GridPos To)> edges = ZoneMap.FenceEdges(
            pasture, new[] { gate }, System.Array.Empty<GridPos>(), out (GridPos Yard, GridPos Lane)? opening);
        var middle = new Vector2(23.5f, 20f);
        List<Vector2[]> lines = VillageMap.PastureFenceLines(pasture, opening);
        var drawn = lines.Select(l => l.Select(v => VillageMap.InTileSpace(v, SubTile.PerTile)).ToArray()).ToList();

        // The walls in tile space: the side two tiles share, a tile long.
        var walls = edges.Select(e => WallSide(e.From, e.To)).ToList();
        if (opening is not { } g || g.Lane != gate)
        {
            faults.Add("the posed pasture has no gate where it was asked for");
        }

        if (drawn.Count != 1 || drawn[0].Length < 8 || drawn[0][0].DistanceTo(drawn[0][^1]) < 0.9f)
        {
            faults.Add($"the round pasture drew {drawn.Count} line(s), not one open line");
        }
        else
        {
            Vector2[] line = drawn[0];
            float sharpest = 0f;
            for (int k = 1; k < line.Length - 1; k++)
            {
                Vector2 into = line[k] - line[k - 1];
                Vector2 outOf = line[k + 1] - line[k];
                if (into.LengthSquared() > 1e-8f && outOf.LengthSquared() > 1e-8f)
                {
                    sharpest = Mathf.Max(sharpest, Mathf.RadToDeg(Mathf.Abs(into.AngleTo(outOf))));
                }
            }

            if (sharpest >= 45f)
            {
                faults.Add($"a corner turns {sharpest:F0}° — a stair, not a curve");
            }

            float furthest = line.Max(v => walls.Min(w => v.DistanceTo(Geometry2D.GetClosestPointToSegment(v, w.A, w.B))));
            if (furthest > 0.5f)
            {
                faults.Add($"a point stands {furthest:F2} tiles from any wall");
            }

            int unreached = walls.Count(w => line.All(v => v.DistanceTo((w.A + w.B) / 2f) > 0.6f));
            if (unreached > 0)
            {
                faults.Add($"{unreached} of {walls.Count} walls have no fence drawn by them");
            }

            if (line.Any(v => v.DistanceTo(middle) < 0.49f))
            {
                faults.Add("the fence is drawn across its gate");
            }

            if (Mathf.Abs(line[0].DistanceTo(middle) - 0.5f) > 0.01f || Mathf.Abs(line[^1].DistanceTo(middle) - 0.5f) > 0.01f)
            {
                faults.Add($"the line ends {line[0].DistanceTo(middle):F2} and {line[^1].DistanceTo(middle):F2} from the gate's middle, not at its posts");
            }
        }

        // ⛔ And a block with the gate in the middle of a long straight side, where a rounded loop has no points of its own
        // (`livestock.md §0.2`'s 8 × 5 example). The first draft drew this gate shut; the disc above could not see it.
        var block = new List<GridPos>();
        for (int y = 40; y < 45; y++)
        {
            for (int x = 40; x < 48; x++)
            {
                block.Add(new GridPos(x, y));
            }
        }

        ZoneMap.FenceEdges(block, new[] { new GridPos(44, 39) }, System.Array.Empty<GridPos>(), out (GridPos Yard, GridPos Lane)? blockGate);
        List<Vector2[]> blockLines = VillageMap.PastureFenceLines(block, blockGate);
        var blockMiddle = new Vector2(44f, 39.5f);
        if (blockLines.Count != 1
            || blockLines[0].Select(v => VillageMap.InTileSpace(v, SubTile.PerTile)).Any(v => v.DistanceTo(blockMiddle) < 0.49f)
            || Mathf.Abs(VillageMap.InTileSpace(blockLines[0][0], SubTile.PerTile).DistanceTo(blockMiddle) - 0.5f) > 0.01f)
        {
            faults.Add("an 8 × 5 block's gate, in the middle of its long side, is not drawn open at its posts");
        }

        // A yard's fence side on its tile's real edge — the tile at (10, 10) spans 9.5 to 10.5 (D338's eighth, D610).
        (Vector2 from, Vector2 to) = VillageMap.FenceSide(new GridPos(10, 10), 0, -1);
        Vector2 a = VillageMap.InTileSpace(from, SubTile.PerTile);
        Vector2 b = VillageMap.InTileSpace(to, SubTile.PerTile);
        if (a.DistanceTo(new Vector2(9.5f, 9.5f)) > 0.001f || b.DistanceTo(new Vector2(10.5f, 9.5f)) > 0.001f)
        {
            faults.Add($"a yard's north side is drawn from {a} to {b}, not along its tile's edge");
        }

        if (WithAPosedCard(card =>
            {
                CardControls c = card.Controls;
                if (c.FenceRow is null)
                {
                    return "a workplace card has no fence row";
                }

                var said = new List<string>();
                foreach ((PastureFenceState state, bool refused, string expected) in new[]
                {
                    (PastureFenceState.None, false, "Fence the pasture"),
                    (PastureFenceState.None, true, "Fence the pasture"),
                    (PastureFenceState.Site, false, "Cancel the fence"),
                    (PastureFenceState.Standing, false, "Take the fence down"),
                    (PastureFenceState.ComingDown, false, "Keep the fence"),
                })
                {
                    WriteTheFenceRow(c, new FenceFacts(state, "posed", refused));
                    if (c.FenceButton!.Text != expected || c.FenceButton.Disabled != refused)
                    {
                        said.Add($"{state}{(refused ? " refused" : string.Empty)} reads \"{c.FenceButton.Text}\"{(c.FenceButton.Disabled ? " (disabled)" : string.Empty)}");
                    }
                }

                return said.Count == 0 ? null : string.Join("; ", said);
            }) is string rowFault)
        {
            faults.Add("the fence row: " + rowFault);
        }

        return faults.Count == 0
            ? $"[widths] fence: ✅ a round pasture's fence drawn as one curve ({pasture.Count} tiles, {edges.Count} walls; no corner past 45°, within half a tile of its walls, open at the gate's posts) and an 8 × 5 block's open mid-side; a yard's side on its tile's edge; the barn card's button reads Fence the pasture (disabled when refused) / Cancel the fence / Take the fence down / Keep the fence"
            : $"[widths] fence: ❌ {string.Join("; ", faults)}";

        // The side two tiles share, in tile space (a tile's centre is its whole coordinate).
        static (Vector2 A, Vector2 B) WallSide(GridPos from, GridPos to)
        {
            var mid = new Vector2((from.X + to.X) / 2f, (from.Y + to.Y) / 2f);
            var along = from.X == to.X ? new Vector2(0.5f, 0f) : new Vector2(0f, 0.5f);
            return (mid - along, mid + along);
        }
    }

    /// <summary>
    /// <c>market limits:</c> (D612) — a market's <i>Keeps up to</i> rows posed through the game's own writer: the caption says
    /// how much of the room is kept, each box stops at what the others leave and shows what the counter keeps, <i>clear</i>
    /// only on a set limit — and a ceiling dropped under the shown value fires no <c>ValueChanged</c>, which would set a
    /// limit nobody typed. On a real store's card (the view cannot raise a market, D596).
    /// </summary>
    private string TheMarketCardStopsAtTheRoom()
    {
        StoreBuilding? any = _loop.World.StoreBuildings.FirstOrDefault();
        if (any is null)
        {
            return "[widths] market limits: ❌ no store to open a card on";
        }

        OpenCard(new CardSubject(CardKind.Store, any.Id));
        Card? card = _cards.Find(c => c.Subject.Kind == CardKind.Store && c.Subject.Id == any.Id);
        if (card is null)
        {
            return "[widths] market limits: ❌ the store's card did not open";
        }

        var faults = new List<string>();
        try
        {
            CardControls c = card.Controls;
            if (c.Limits.Count == 0 || c.LimitCaption is null)
            {
                return "[widths] market limits: ❌ a store card has no limit rows";
            }

            WriteTheMarketLimits(c, 5000, 8000, 1000, _ => new MarketLimitFacts(true, 1000, 3000, false));
            if (!c.LimitCaption.Text.Contains("5,000 of its 8,000", System.StringComparison.Ordinal))
            {
                faults.Add($"the caption reads \"{c.LimitCaption.Text}\"");
            }

            foreach ((Goods goods, Control cell, SpinBox amount, Button clear) in c.Limits)
            {
                if (!cell.Visible || (int)amount.MaxValue != 3000 || (int)amount.Value != 1000 || !clear.Disabled)
                {
                    faults.Add($"{goods}: max {amount.MaxValue}, shows {amount.Value}, clear {(clear.Disabled ? "off" : "on")}");
                }
            }

            // The trap: a box showing 2,500 whose ceiling drops to 1,000 must not fire a change on the way down.
            WriteTheMarketLimits(c, 8000, 8000, 1000, _ => new MarketLimitFacts(true, 2500, 2500, true));
            int fired = 0;
            void Count(double _) => fired++;
            foreach ((Goods _, Control _, SpinBox amount, Button _) in c.Limits)
            {
                amount.ValueChanged += Count;
            }

            WriteTheMarketLimits(c, 8000, 8000, 1000, _ => new MarketLimitFacts(true, 1000, 1000, false));
            foreach ((Goods _, Control _, SpinBox amount, Button _) in c.Limits)
            {
                amount.ValueChanged -= Count;
            }

            if (fired > 0)
            {
                faults.Add($"a ceiling dropped under the shown value fired {fired} change(s) — a limit nobody typed");
            }

            if (c.Limits.Any(l => (int)l.Amount.Value != 1000 || (int)l.Amount.MaxValue != 1000))
            {
                faults.Add("after the ceiling dropped, a box does not show 1,000 of at most 1,000");
            }
        }
        finally
        {
            CloseCard(card);
        }

        return faults.Count == 0
            ? $"[widths] market limits: ✅ the caption says 5,000 of its 8,000 kept; each box stops at what the others leave and shows what the counter keeps; clear only on a set limit; a ceiling dropped under the shown value fired no change"
            : $"[widths] market limits: ❌ {string.Join("; ", faults)}";
    }

    /// <summary>
    /// <c>quarry claim:</c> (D613) — a posed claimed seam drawn as its palisade: one closed line round it, no corner sharper
    /// than 45°, every point within half a tile of the seam's edge, a post about every tile of it; and the brush offered to
    /// no trade whose ground is claimed (the quarrier and the miner) while every painted trade keeps it. The view cannot raise
    /// a quarry (D596), so the seam is posed.
    /// </summary>
    private string TheClaimedSeamIsStakedRound()
    {
        var faults = new List<string>();
        var seam = new List<GridPos>
        {
            new(30, 30), new(31, 30), new(32, 30), new(30, 31), new(31, 31), new(30, 32), new(29, 33),
        };
        var set = seam.ToHashSet();
        var sides = new List<(Vector2 A, Vector2 B)>();
        foreach (GridPos t in seam)
        {
            foreach ((int dx, int dy) in new[] { (0, -1), (1, 0), (0, 1), (-1, 0) })
            {
                if (!set.Contains(new GridPos(t.X + dx, t.Y + dy)))
                {
                    var mid = new Vector2(t.X + (dx / 2f), t.Y + (dy / 2f));
                    var along = dx == 0 ? new Vector2(0.5f, 0f) : new Vector2(0f, 0.5f);
                    sides.Add((mid - along, mid + along));
                }
            }
        }

        // ⚠️ (29, 33) touches the rest only at a corner — one seam (D561: eight neighbours), two pieces on the ground — so it
        // is staked as two rings: the palisade goes round what is drawn, and a ring across a corner would stake grass.
        List<Vector2[]> lines = VillageMap.PastureFenceLines(seam, null);
        if (lines.Count != 2 || lines.Any(l => l.Length < 4 || l[0].DistanceTo(l[^1]) > 0.001f))
        {
            faults.Add($"the seam drew {lines.Count} line(s), not a closed ring round each of its two pieces");
        }
        else
        {
            Vector2[] drawn = lines.SelectMany(l => l).Select(v => VillageMap.InTileSpace(v, SubTile.PerTile)).ToArray();
            float furthest = drawn.Max(v => sides.Min(e => v.DistanceTo(Geometry2D.GetClosestPointToSegment(v, e.A, e.B))));
            if (furthest > 0.5f)
            {
                faults.Add($"a stake stands {furthest:F2} tiles from the seam's edge");
            }

            // Each ring on its own — the jump from one ring's end to the next one's start is not a corner.
            float sharpest = 0f;
            foreach (Vector2[] ring in lines)
            {
                for (int k = 1; k < ring.Length - 1; k++)
                {
                    Vector2 into = ring[k] - ring[k - 1];
                    Vector2 outOf = ring[k + 1] - ring[k];
                    if (into.LengthSquared() > 1e-8f && outOf.LengthSquared() > 1e-8f)
                    {
                        sharpest = Mathf.Max(sharpest, Mathf.RadToDeg(Mathf.Abs(into.AngleTo(outOf))));
                    }
                }
            }

            if (sharpest >= 45f)
            {
                faults.Add($"a corner turns {sharpest:F0}°");
            }

            int posts = lines.Sum(l => VillageMap.PalisadePosts(l).Count);
            if (posts < sides.Count * 6 / 10 || posts > sides.Count * 12 / 10)
            {
                faults.Add($"{posts} posts round a seam {sides.Count} tile-sides long — not one a tile");
            }
        }

        foreach (JobKind claims in new[] { JobKind.Quarrier, JobKind.Miner })
        {
            if (GroundIsPaintedFor(claims, _loop.World))
            {
                faults.Add($"the {claims}'s card still offers the brush");
            }
        }

        foreach (JobKind painted in new[] { JobKind.Farmer, JobKind.Forester, JobKind.Herdsman })
        {
            if (!GroundIsPaintedFor(painted, _loop.World))
            {
                faults.Add($"the {painted}'s card lost the brush");
            }
        }

        return faults.Count == 0
            ? $"[widths] quarry claim: ✅ a posed seam of {seam.Count} tiles staked round, a closed ring round each of its two pieces, within half a tile of its edge, no corner past 45°, a post a tile; the quarrier's and the miner's cards offer no brush, the farmer's, forester's and herdsman's keep it"
            : $"[widths] quarry claim: ❌ {string.Join("; ", faults)}";
    }

    /// <summary>Open a card on the first workplace, hand it to <paramref name="pose"/>, and close it again.</summary>
    private string? WithAPosedCard(System.Func<Card, string?> pose)
    {
        // Any workplace, a site as well: the writers are posed, and a site's card builds the same Settings fold.
        Workplace? any = _loop.World.Workplaces.FirstOrDefault();
        if (any is null)
        {
            return "no workplace to pose a card on";
        }

        OpenCard(new CardSubject(CardKind.Workplace, any.Id));
        Card? card = _cards.Find(c => c.Subject.Kind == CardKind.Workplace && c.Subject.Id == any.Id);
        if (card is null)
        {
            return "the card did not open";
        }

        RefreshCards(_loop.World);
        try
        {
            return pose(card);
        }
        finally
        {
            CloseCard(card);
        }
    }
}

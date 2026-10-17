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
    /// <c>post:</c> — the post's window posed at its fullest fits a card and reads its orders back; then its real controls
    /// against the real world: a good's dropdown sells, buys and clears it (never both at once), <i>Send the boat on</i>
    /// with no boat docked says why, and an order for beasts with no pasture is refused with the reason.
    /// </summary>
    private string ThePostsWindowWorks()
    {
        var faults = new List<string>();
        SimWorld world = _loop.World;
        string? fault = WithAPosedCard(card =>
        {
            CardControls c = card.Controls;
            if (c.PostRow is null || c.Orders.Count != world.GoodsCatalog.Count)
            {
                return "a workplace card has no post's window to pose";
            }

            card.Settings.Visible = true;
            c.PostRow.Visible = true;
            var orders = new OrderFacts[c.Orders.Count];
            for (int i = 0; i < orders.Length; i++)
            {
                Goods goods = c.Orders[i].Goods;
                orders[i] = new OrderFacts(
                    true,
                    123_456,
                    goods == Goods.Firewood ? 400 : null,
                    goods == Goods.Stone ? 50 : null,
                    goods == Goods.Stone ? "No boat brings stone — the order waits for carts." : string.Empty);
            }

            WriteThePostsWindow(c, new PostFacts(
                "The merchant carries what is above each keep down to the post; the next boat takes enough of it to pay for what you asked.",
                "The boat from Ashford is tied up at the post for 10 more days. It carries 1,234 stone, 56 leather, 7 iron tools and 3 cattle.",
                true,
                orders,
                4));
            ForceUpdateTransform();

            (Goods _, HBoxContainer fireRow, Label _, OptionButton fire, SpinBox fireAmount) = c.Orders[(int)Goods.Firewood];
            (Goods _, HBoxContainer _, Label _, OptionButton stone, SpinBox _) = c.Orders[(int)Goods.Stone];
            float wide = c.PostRow.GetCombinedMinimumSize().X;
            string? posed = c.Orders.Count(o => o.Row.Visible) != orders.Length ? "not every good's row shows"
                : fire.Selected != SellAbove || (int)fireAmount.Value != 400 ? $"firewood reads {fire.Selected} at {fireAmount.Value}"
                : stone.Selected != BuyUpTo ? $"stone reads {stone.Selected}"
                : c.SendOn!.Disabled ? "Send the boat on is disabled with a boat docked"
                : !c.HeadOn!.ButtonPressed ? "the order for beasts is not shown"
                : wide > CardWidth + 1f ? $"the window wants {wide:F0} of a {CardWidth:F0} card ({string.Join(", ", c.PostRow.GetChildren().OfType<Control>().Where(k => k.Visible).Select(k => $"{k.GetClass()} {k.GetCombinedMinimumSize().X:F0}").Distinct())})"
                : fireRow.GetCombinedMinimumSize().X > CardWidth + 1f ? $"a good's row wants {fireRow.GetCombinedMinimumSize().X:F0}"
                : null;
            if (posed is not null)
            {
                return posed;
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

            c.SendOn!.EmitSignal(BaseButton.SignalName.Pressed);
            string sendSaid = c.PostVerdict!.Text;
            bool hadNoPasture = world.SetTradeBuyHead(1).Allowed is false;
            world.SetTradeBuyHead(null);
            c.HeadOn!.EmitSignal(BaseButton.SignalName.Toggled, true);
            string headSaid = c.PostVerdict.Text;
            if (!hadNoPasture)
            {
                world.SetTradeBuyHead(null);
            }

            return !sold ? "choosing sell above did not set the keep"
                : !bought ? "choosing buy up to did not swap the keep for a buy"
                : !cleared ? "choosing — left an order"
                : world.BoatIsDocked || sendSaid != "No boat is tied up at the post." ? $"Send the boat on with no boat said \"{sendSaid}\""
                : hadNoPasture && !headSaid.Contains("pasture", System.StringComparison.Ordinal) ? $"beasts with no pasture said \"{headSaid}\""
                : null;
        });

        if (fault is not null)
        {
            faults.Add(fault);
        }

        return faults.Count == 0
            ? "[widths] post: ✅ the window at its fullest fits a card and reads its orders back; a good's dropdown sells, buys and clears it, never both; Send the boat on with no boat says why; beasts with no pasture are refused with the reason"
            : $"[widths] post: ❌ {string.Join("; ", faults)}";
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

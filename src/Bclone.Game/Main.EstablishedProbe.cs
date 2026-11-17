using Bclone.Sim.Core;
using Bclone.Sim.World;

namespace Bclone.Game;

/// <remarks>The probe's <c>established:</c> line (`the-cards.md §6`, D784).</remarks>
public partial class Main
{
    /// <summary>
    /// ⭐ EVERY BUILDING SAYS WHEN IT WAS ESTABLISHED (`the-cards.md §6`, D784): a store finished on a posed day reads that day
    /// on its card, in the birthday's calendar; one finished at the founding reads the founding; the founders' cart reads that
    /// it came with them; a site still being built reads nothing. Each posed thing is taken away again.
    /// </summary>
    private string TheEstablishedLineReads()
    {
        SimWorld world = _loop.World;
        var faults = new List<string>();
        ulong day23Spring16 = (ulong)((15 * world.Config.TicksPerYear) + (22 * world.Config.TicksPerDay));
        if (EstablishedLine(day23Spring16, world.Config) != "Established Day 23, Spring, Year 16.")
        {
            faults.Add($"day 23 of spring, year 16 reads \"{EstablishedLine(day23Spring16, world.Config)}\"");
        }

        // ---- the cart ----
        if (world.TheCart is StoreBuilding cart)
        {
            OpenCard(new CardSubject(CardKind.Store, cart.Id));
            Card card = _selectedCard!;
            if (!card.Established.Visible || card.Established.Text != CameWithTheFounders)
            {
                faults.Add($"the cart reads \"{(card.Established.Visible ? card.Established.Text : "(hidden)")}\"");
            }

            CloseCard(card);
        }

        // ---- a store, posed on clear ground near the founding: a pile stands at once ----
        StoreBuilding? pile = MarkNearTheFounding<StoreBuilding>(world, StoreKind.Pile);
        if (pile is null)
        {
            faults.Add("no pile could be posed near the founding");
        }
        else
        {
            ulong was = pile.RaisedAtTick;
            try
            {
                pile.RaisedAtTick = day23Spring16;
                OpenCard(new CardSubject(CardKind.Store, pile.Id));
                Card card = _selectedCard!;
                if (!card.Established.Visible || card.Established.Text != "Established Day 23, Spring, Year 16.")
                {
                    faults.Add($"a pile finished on day 23 of spring, year 16 reads \"{(card.Established.Visible ? card.Established.Text : "(hidden)")}\"");
                }

                pile.RaisedAtTick = 0;
                RefreshCards(world);
                if (card.Established.Text != $"Established at the founding — {SimClock.FromTick(0, world.Config)}.")
                {
                    faults.Add($"a pile finished at the founding reads \"{card.Established.Text}\"");
                }

                ForceUpdateTransform();
                if (card.Panel.GetCombinedMinimumSize().X > CardWidth + 1f)
                {
                    faults.Add($"the Established line widens a card to {card.Panel.GetCombinedMinimumSize().X:F0}");
                }

                CloseCard(card);
            }
            finally
            {
                pile.RaisedAtTick = was;
                world.Demolish(pile);
            }
        }

        // ---- a site: nothing stands yet ----
        Workplace? site = MarkNearTheFounding<Workplace>(world, null);
        if (site is null)
        {
            faults.Add("no well site could be posed near the founding");
        }
        else
        {
            try
            {
                OpenCard(new CardSubject(CardKind.Workplace, site.Id));
                Card card = _selectedCard!;
                if (card.Established.Visible)
                {
                    faults.Add($"a well still being built reads \"{card.Established.Text}\"");
                }

                CloseCard(card);
            }
            finally
            {
                world.Demolish(site);
            }
        }

        return faults.Count == 0
            ? "[widths] established: ✅ a store finished on day 23 of spring, year 16 says so, one at the founding says the founding, "
                + "the cart came with the founders, a site says nothing, and the card holds its width"
            : $"[widths] established: ⛔ {string.Join("; ", faults)}";
    }

    /// <summary>
    /// The first thing that can be marked on clear ground, ring by ring out from the founding: a pile (a store, at once) when
    /// <paramref name="store"/> is given, else a well (a site). ⚠️ Clear ground only, and the first allowed mark only — a free
    /// building on wooded ground waits and paints the clearing (D782's trap).
    /// </summary>
    private static T? MarkNearTheFounding<T>(SimWorld world, StoreKind? store)
        where T : class
    {
        BuildingKind? kind = store is StoreKind s ? world.BuildingsCatalog.ThatStores(s) : BuildingKind.Well;
        if (kind is not BuildingKind what)
        {
            return null;
        }

        GridPos founding = world.Map.FoundingSite;
        for (int r = 3; r <= 12; r++)
        {
            for (int dy = -r; dy <= r; dy++)
            {
                for (int dx = -r; dx <= r; dx++)
                {
                    var tile = new GridPos(founding.X + dx, founding.Y + dy);
                    if (Math.Max(Math.Abs(dx), Math.Abs(dy)) != r || !world.GroundIsClearAt(tile))
                    {
                        continue;
                    }

                    int stores = world.StoreBuildings.Count, workplaces = world.Workplaces.Count;
                    if (!world.Mark(what, Point.CentreOf(tile)).Allowed)
                    {
                        continue;
                    }

                    if (store is not null)
                    {
                        return world.StoreBuildings.Count == stores + 1 ? world.StoreBuildings[^1] as T : null;
                    }

                    return world.Workplaces.Count == workplaces + 1 && world.Workplaces[^1].IsSite ? world.Workplaces[^1] as T : null;
                }
            }
        }

        return null;
    }
}

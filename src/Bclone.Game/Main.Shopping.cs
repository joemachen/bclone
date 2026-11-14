using Bclone.Sim.Core;
using Bclone.Sim.World;

namespace Bclone.Game;

/// <summary>The shell's half of the shop walk (D744, `phase-5-exit-gate.md §4`): the probe line that reads the real home card.</summary>
public partial class Main
{
    /// <summary>
    /// <c>shop walk:</c> — every household with a home and somebody living in it: its card's status names the shop the sim
    /// says it walks to, and how far (<see cref="SimWorld.SayTheShopWalk"/>) — not "the market" whatever store it uses.
    /// </summary>
    /// <remarks>⛔ The expected sentence is the sim's, never read back off the card it checks (D419's trap).</remarks>
    private string TheHomeCardSaysTheShopWalk()
    {
        SimWorld world = _loop.World;
        var faults = new List<string>();
        (GridPos? wasTile, int wasVillager) = (_selectedTile, _selectedVillagerId);
        int read = 0, asked = 0;
        string example = "nothing";

        // ⚠️ The founding probe world has nobody housed yet, so with no home anywhere one family is POSED beside the first
        // store for the read and put back after it — `HomePosition` is the only fact touched, restored before anything ticks.
        Household? posed = null;
        Point? posedWas = null;
        if (!world.Households.Any(h => h.HomeTile is not null && world.LivingMembersOf(h) > 0) && world.StoreBuildings.Count > 0
            && world.Households.FirstOrDefault(h => world.LivingMembersOf(h) > 0) is Household family)
        {
            GridPos store = world.StoreBuildings[0].Tile;
            (posed, posedWas) = (family, family.HomePosition);
            foreach (GridPos tile in new[] { new GridPos(store.X + 3, store.Y), new GridPos(store.X, store.Y + 3), store })
            {
                family.HomePosition = Point.CentreOf(tile);
                if (world.SayTheShopWalk(family) is not null)
                {
                    break;
                }
            }
        }

        foreach (Household home in world.Households)
        {
            if (home.HomeTile is null || world.LivingMembersOf(home) == 0 || world.SayTheShopWalk(home) is not string said)
            {
                continue;
            }

            OpenCard(new CardSubject(CardKind.Household, home.Id));
            string status = _selectedCard!.Status.Text;
            CloseCard(_selectedCard);
            read++;
            example = status;

            // A cold house says that first, and is not asked about its shop; a hungry one names where somebody is going.
            bool cold = status.StartsWith("Cold", StringComparison.Ordinal);
            asked += cold ? 0 : 1;
            if (!cold && !status.Contains(said, StringComparison.Ordinal))
            {
                faults.Add($"the {home.Name} card reads \"{status}\", not \"{said}\"");
            }
        }

        if (posed is not null)
        {
            posed.HomePosition = posedWas;
        }

        (_selectedTile, _selectedVillagerId) = (wasTile, wasVillager);
        Refresh();

        if (read == 0)
        {
            faults.Add("no household with a home and a shop to ask");
        }
        else if (asked == 0)
        {
            // ⛔ Every card read was a cold house, which does not name its shop: a pass here would have checked nothing (D326).
            faults.Add($"all {read} home cards read were cold, so none was asked about its shop");
        }

        return faults.Count == 0
            ? $"[widths] shop walk: ✅ {read} home card{(read == 1 ? "" : "s")}{(posed is null ? "" : " (one family posed beside a store, put back after)")} name the shop the sim says each family walks to, and how far — e.g. \"{example}\""
            : $"[widths] shop walk: ⛔ {string.Join("; ", faults)}";
    }
}

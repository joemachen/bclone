using Kith.Sim.Core;
using Kith.Sim.World;
using Godot;

namespace Kith.Game;

/// <remarks>The probe's <c>portrait:</c> line (A262, `specs/building-portrait.md §5`).</remarks>
public partial class Main
{
    /// <summary>
    /// ⭐ A CARD'S PICTURE IS THE MAP'S OWN PAINTED BUILDING (Joe's play note 5): with the map painting, a store's card shows the
    /// very pieces the map laid where it stands — not a copy; with the painting off it is the rectangle, as the map's is; the
    /// picture is 120 tall; and a site's card draws the site as the map does, its colours and its fill from the map's own rule.
    /// Everything posed is taken away again.
    /// </summary>
    private string ThePictureIsTheMaps()
    {
        SimWorld world = _loop.World;
        var faults = new List<string>();

        // ---- a store, painted on the map ----
        if (world.StoreBuildings.Count == 0)
        {
            faults.Add("no store stands to pose a picture on");
        }
        else
        {
            StoreBuilding store = world.StoreBuildings[0];
            VillageMap.RoofCache posed = _map.PoseAPaintedBuildingAt(store.Position, 1.6f, 0.8f, 9000);
            try
            {
                OpenCard(new CardSubject(CardKind.Store, store.Id));
                Card card = _selectedCard!;
                if (!ReferenceEquals(card.Portrait.Painted, posed))
                {
                    faults.Add(card.Portrait.Painted is null
                        ? "a store the map paints shows the rectangle on its card"
                        : "a store's card shows a painted building that is not the one the map laid there");
                }

                if (card.Portrait.CustomMinimumSize.Y != 120f)
                {
                    faults.Add($"the picture is {card.Portrait.CustomMinimumSize.Y:F0} tall, not 120");
                }

                _map.PoseThePainting(false);
                RefreshCards(world);
                if (card.Portrait.Painted is not null)
                {
                    faults.Add("with the map's painting off, the card still shows the painted building (the map draws a rectangle)");
                }

                CloseCard(card);
            }
            finally
            {
                _map.UnposeThePainting(posed);
            }
        }

        // ---- a site: drawn as the map draws a site, painting or not ----
        Workplace? site = MarkNearTheFounding<Workplace>(world, null);
        if (site is null)
        {
            faults.Add("no site could be posed near the founding");
        }
        else
        {
            try
            {
                OpenCard(new CardSubject(CardKind.Workplace, site.Id));
                Card card = _selectedCard!;
                (Vector2 _, float _, float _, ushort _, Color fill, Color edge) = _map.SiteShapeOf(world, site);
                float shown = VillageMap.SiteShown(site.Construction!);
                if (card.Portrait.Site is not BuildingPortrait.SiteLook look)
                {
                    faults.Add("a site's card draws no site");
                }
                else if (look.Fill != fill || look.Edge != edge || look.Shown != shown)
                {
                    faults.Add($"a site's card draws {look.Fill.ToHtml()}/{look.Edge.ToHtml()} at {look.Shown:F2}, the map {fill.ToHtml()}/{edge.ToHtml()} at {shown:F2}");
                }

                CloseCard(card);
            }
            finally
            {
                world.Demolish(site);
            }
        }

        return faults.Count == 0
            ? "[widths] portrait: ✅ a store the map paints shows the map's own painted building on its card, 120 tall; the rectangle when the "
                + "painting is off, as the map's is; a site is drawn as the map draws one"
            : $"[widths] portrait: ⛔ {string.Join("; ", faults)}";
    }
}

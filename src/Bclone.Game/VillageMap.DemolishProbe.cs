using Bclone.Sim.Core;
using Bclone.Sim.World;
using Godot;

namespace Bclone.Game;

/// <remarks>The probe's <c>demolish:</c> line (D782).</remarks>
public partial class VillageMap
{
    /// <summary>
    /// ⭐ THE TOOLS FIND WHAT IS DRAWN (D782, Joe: <i>"the game isnt letting me demolish this stockpile … it says there's nothing
    /// to pull down"</i>): a pile marked a quarter-tile off a tile's centre, and Demolish clicked on its drawing over the
    /// neighbouring tile — the part its footprint does not claim (D319's centre rule) — must cancel it, as a click there already
    /// opens its card (D338).
    /// </summary>
    internal string TheDemolishFindsWhatIsDrawn()
    {
        SimWorld world = _world!;
        if (world.BuildingsCatalog.ThatStores(StoreKind.Pile) is not BuildingKind pile)
        {
            return "[widths] demolish: ⛔ no building stores a pile";
        }

        GridPos founding = world.Map.FoundingSite;
        Fixed off = Fixed.FromRatio(72, 100);
        // ⚠️ A pile is free (it owes nothing to raise), so marking one puts it up at once, a store, never a site — and
        // comes down at once (`MarkDemolition`'s no-work arm). The first mark allowed is the one; the loop stops on it.
        StoreBuilding? site = null;
        int storesBefore = world.StoreBuildings.Count;
        bool allowed = false;
        for (int r = 3; r <= 12 && !allowed; r++)
        {
            for (int dy = -r; dy <= r && !allowed; dy++)
            {
                for (int dx = -r; dx <= r && !allowed; dx++)
                {
                    if (Math.Max(Math.Abs(dx), Math.Abs(dy)) != r)
                    {
                        continue;
                    }

                    var at = new Point(Fixed.FromInt(founding.X + dx) + off, Fixed.FromInt(founding.Y + dy) + off);
                    allowed = world.GroundIsClearAt(at.ToTile()) && world.Mark(pile, at).Allowed;
                }
            }
        }

        if (world.StoreBuildings.Count == storesBefore + 1 && world.StoreBuildings[^1].Kind == StoreKind.Pile)
        {
            site = world.StoreBuildings[^1];
        }

        if (site is null)
        {
            return $"[widths] demolish: ⛔ a pile marked near the founding did not stand at once (allowed {allowed}, "
                + $"stores {storesBefore} → {world.StoreBuildings.Count}, workplaces {world.Workplaces.Count})";
        }

        Point origin = site.Footprint.Origin;
        GridPos anchor = origin.ToTile();
        Point onTheDrawing = origin + new Point(Fixed.FromRatio(4, 10), Fixed.FromRatio(4, 10));
        Vector2 screen = ToScreen(onTheDrawing);
        Vector2 inView = ToTile(screen);
        var rounded = new GridPos(Mathf.RoundToInt(inView.X), Mathf.RoundToInt(inView.Y));
        string? fault = null;
        if (!site.Footprint.Covers(onTheDrawing) || rounded == anchor)
        {
            fault = $"the pose missed: {onTheDrawing} on the pile {site.Footprint.Covers(onTheDrawing)}, its tile {rounded} against the anchor {anchor}";
        }

        (MapTool tool, BuildingKind? building, HarvestBrush? harvest, int groundFor, int brush) =
            (Tool, _building, _harvestMode, _groundFor, _brush);
        try
        {
            SetTool(MapTool.Demolishing);
            PlaceOrPullDown(screen);
        }
        finally
        {
            SetTool(tool, building, harvest, groundFor, brush);
        }

        if (world.StoreBuildings.Contains(site))
        {
            fault ??= $"Demolish on the pile's drawing over {rounded} left it standing (its own tile is {anchor})";
            world.Demolish(site);
        }

        return fault is null
            ? $"[widths] demolish: ✅ a pile marked at {origin} is cancelled by Demolish on its "
                + $"drawing over {rounded}, not its own tile {anchor}"
            : $"[widths] demolish: ⛔ {fault}";
    }
}

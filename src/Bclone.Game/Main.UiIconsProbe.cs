using Bclone.Sim.Core;
using Bclone.Sim.World;
using Godot;

namespace Bclone.Game;

/// <remarks>The probe's <c>ui icons:</c> line (<c>ui-art.md §9.3</c> step 2, A111).</remarks>
public partial class Main
{
    /// <summary>
    /// ⭐ THE PAINTED ICONS ARE WHERE THE GLYPHS WERE (<c>ui-assets.md §4</c>): every building on the bar, every tool,
    /// every good and every trade finds its file by its own name, and a name with none keeps its glyph. Each file loads at
    /// its master size with mipmaps; the bar's icons sit at 20 logical, over the label and clear of it; an owed building
    /// shows its owed file and goes back; a worker's card and the roster show their trade; the Professions table and the
    /// moments wear the painted marks.
    /// </summary>
    private string TheIconsArePainted()
    {
        SimWorld world = _loop.World;
        var faults = new List<string>();
        var missing = new List<string>();
        int found = 0;

        void Check(string set, string name, int side)
        {
            if (UiIcons.Find(set, name) is not { } texture)
            {
                missing.Add($"{set}/{UiIcons.Slug(name)}");
                return;
            }

            found++;
            if (texture.GetWidth() != side || texture.GetHeight() != side)
            {
                faults.Add($"{set}/{UiIcons.Slug(name)} is {texture.GetWidth()}x{texture.GetHeight()}, not {side}");
            }
            else if (texture.GetImage() is not { } image || !image.HasMipmaps())
            {
                faults.Add($"{set}/{UiIcons.Slug(name)} has no mipmaps");
            }
        }

        // ---- every row finds its file by its name ----
        BuildingsCatalog buildings = world.BuildingsCatalog;
        for (int id = 0; id < buildings.Count; id++)
        {
            if (buildings.Has(id) && buildings[(BuildingKind)id]?.EnclosesPasture != true)
            {
                Check("buildings", buildings.NameOf((BuildingKind)id), 80);
                if (UiIcons.Find("buildings", buildings.NameOf((BuildingKind)id), "owed") is null)
                {
                    missing.Add($"buildings/{UiIcons.Slug(buildings.NameOf((BuildingKind)id))}-owed");
                }
            }
        }

        for (int g = 0; g < world.GoodsCatalog.Count; g++)
        {
            Check("goods", world.GoodsCatalog.NameOf((Goods)g), 80);
        }

        foreach (JobKind kind in JobLimits.Kinds)
        {
            Check("trades", world.JobsCatalog.NameOf(kind), 80);
        }

        foreach (ToolMark mark in System.Enum.GetValues<ToolMark>())
        {
            Check("tools", mark.ToString(), 80);
        }

        Check("moments", nameof(MomentMark.Nomads), 104);
        Check("moments", nameof(MomentMark.Bridge), 104);

        // ---- a name with no file keeps its glyph ----
        if (UiIcons.Or("buildings", "a modder's brewery", () => new BuildingGlyph(BuildingKind.Granary, false)) is not BuildingGlyph fallback)
        {
            faults.Add("a building with no file did not keep its glyph");
        }
        else
        {
            fallback.Free();
        }

        if (UiIcons.Slug("woodcutter's hut") != "woodcutters-hut" || UiIcons.Slug("HarvestTrees") != "harvest-trees"
            || UiIcons.Slug("stone tools") != "stone-tools")
        {
            faults.Add($"names slug wrongly: {UiIcons.Slug("woodcutter's hut")}, {UiIcons.Slug("HarvestTrees")}, {UiIcons.Slug("stone tools")}");
        }

        // ---- the bar: a painted icon on every building and tool, the button's own, at 20, above its label ----
        int painted = 0;
        float tallest = 0f;
        foreach ((_, _, Button button, BuildingKind? kind, ToolMark? mark) in _strip)
        {
            if (kind is null && mark is null)
            {
                continue;
            }

            UiIcon? icon = null;
            foreach (Node child in button.GetChildren())
            {
                icon ??= child as UiIcon;
            }

            if (icon is null)
            {
                continue;
            }

            painted++;
            if (!ReferenceEquals(button.Icon, icon.Texture) || button.VerticalIconAlignment != VerticalAlignment.Top
                || button.GetThemeConstant("icon_max_width") != (int)UiIcons.Side)
            {
                faults.Add($"{button.Text}'s icon is not its own, above its label, at 20");
            }

            tallest = Mathf.Max(tallest, button.GetCombinedMinimumSize().Y);
        }

        if (painted == 0)
        {
            faults.Add("no button on the bar wears a painted icon");
        }

        // ---- owed, and back ----
        if (_libraryButton is { } library)
        {
            UiIcon? icon = null;
            foreach (Node child in library.GetChildren())
            {
                icon ??= child as UiIcon;
            }

            bool was = world.AFreeLibraryIsOwed;
            UiIcons.MarkOwed(library, true);
            bool showedOwed = icon?.Owed == true;
            UiIcons.MarkOwed(library, false);
            bool wentBack = icon?.Owed == false;
            UiIcons.MarkOwed(library, was);
            if (!showedOwed || !wentBack)
            {
                faults.Add($"the library's icon {(showedOwed ? "did not go back from owed" : "did not show owed")}");
            }
        }

        // ---- a worker's card and the roster show their trade; the Professions table wears the painted marks ----
        // Posed: the first adult given the first workplace, put back after — this early, nobody may hold a job yet.
        Villager? worker = world.Villagers.Find(v => v.Alive && v.CanWork);
        Workplace? place = world.Workplaces.Count > 0 ? world.Workplaces[0] : null;
        if (worker is null || place is null)
        {
            faults.Add("no adult and workplace to pose a worker with");
        }
        else
        {
            int heldBefore = worker.WorkplaceId;
            worker.WorkplaceId = place.Id;
            try
            {
                TheWorkerIsShown(worker, faults);
            }
            finally
            {
                worker.WorkplaceId = heldBefore;
                RebuildTheRoster(world);
            }
        }

        int tradeIcons = 0;
        foreach ((_, Control[] cells) in _professionRows)
        {
            tradeIcons += cells[0] is UiIcon ? 1 : 0;
        }

        if (tradeIcons != _professionRows.Count)
        {
            faults.Add($"{_professionRows.Count - tradeIcons} of the Professions table's {_professionRows.Count} rows keep a glyph");
        }

        if (missing.Count > 0)
        {
            faults.Add($"no file for {string.Join(", ", missing)}");
        }

        return faults.Count == 0
            ? $"[widths] ui icons: ✅ {found} painted icons found by name, each at its master size with mipmaps; {painted} bar buttons "
              + $"wear one at 20 above the label, the tallest wanting {tallest:F0}px; the library shows owed and goes back; a "
              + $"worker's card and the roster show the trade; {tradeIcons} Professions rows painted; a name with no file keeps its glyph"
            : $"[widths] ui icons: ⛔ {string.Join("; ", faults)}";
    }

    /// <summary>A worker's card and the roster show their trade's icon.</summary>
    private void TheWorkerIsShown(Villager worker, List<string> faults)
    {
        OpenCard(new CardSubject(CardKind.Villager, worker.Id));
        Card card = _selectedCard!;
        if (!card.Person.JobIcon.Visible || card.Person.JobIcon.Texture is null)
        {
            faults.Add("a worker's card shows no trade icon");
        }

        CloseCard(card);

        RebuildTheRoster(_loop.World);
        bool rosterShows = false;
        foreach ((int id, TreeItem item) in _rosterItems)
        {
            rosterShows |= id == worker.Id && item.GetIcon(2) is not null;
        }

        if (!rosterShows)
        {
            faults.Add("the roster shows no trade icon for a worker");
        }
    }
}

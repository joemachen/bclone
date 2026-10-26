using Bclone.Sim.World;
using Godot;

namespace Bclone.Game;

/// <summary>
/// ⭐ The lab's placeholder village — <b>the game's own shapes, drawn over the grass</b>, so the grass is judged under the
/// things that will stand on it (`specs/seasonal-grass.md §5`, D630).
/// </summary>
/// <remarks>
/// <para>
/// <b>Borrowed, never invented</b> (<see cref="BuildingGlyph"/>'s rule): the footprint quad
/// (<see cref="VillageMap.FootprintQuadAt"/>), every colour, the canopy scatter (<see cref="VillageMap.CanopyOn"/>) and
/// the villager's radius are the map's; the footprints are the loaded config's rows, as the game reads them. Only the
/// layout is the lab's.
/// </para>
/// <para>
/// ⚠️ The game draws a wood as live canopies only above <c>TreeZoomFloor</c> (24 px a tile) and as the baked valley
/// below it; the lab has no bake, so it draws the canopies at every zoom.
/// </para>
/// </remarks>
internal sealed partial class GrassLabVillage : Control
{
    /// <summary>Six homes about a lane: tile offsets from the window's centre, and a facing each.</summary>
    private static readonly (Vector2 At, ushort Facing)[] Homes =
    {
        (new Vector2(-6f, -3f), 0), (new Vector2(-2.6f, -3.2f), 0), (new Vector2(1f, -3.4f), 16384),
        (new Vector2(-6f, 1.2f), 32768), (new Vector2(-2.4f, 1.4f), 32768), (new Vector2(3.6f, -0.2f), 49152),
    };

    private static readonly Vector2 Granary = new(0.6f, 3.6f);

    /// <summary>The wood: these tiles, each with the game's own canopies on it.</summary>
    private static readonly Rect2I Wood = new(7, -6, 5, 6);

    private static readonly (Vector2 At, Color Colour)[] Villagers =
    {
        (new Vector2(-4.2f, -1f), VillageMap.AdultColour), (new Vector2(-3.5f, -0.8f), VillageMap.ChildColour),
        (new Vector2(-0.4f, -0.9f), VillageMap.AdultColour), (new Vector2(1.8f, 1.6f), VillageMap.ElderColour),
        (new Vector2(2.4f, 2.2f), VillageMap.AdultColour), (new Vector2(5.8f, -3.4f), VillageMap.AdultColour),
        (new Vector2(6.3f, -2.9f), VillageMap.ChildColour), (new Vector2(-7.4f, -0.6f), VillageMap.AdultColour),
    };

    private readonly Vector2 _home;
    private readonly Vector2 _granary;
    private float _pixelsPerTile = (float)GrassLab.DefaultPixelsPerTile;

    internal GrassLabVillage()
    {
        MouseFilter = MouseFilterEnum.Ignore;
        IReadOnlyList<BuildingRow> rows = ConfigLocator.LoadOrDefault(out _).BuildingRows;
        _home = ExtentOf(rows, BuildingKind.Home);
        _granary = ExtentOf(rows, BuildingKind.Granary);
    }

    internal float PixelsPerTile
    {
        get => _pixelsPerTile;
        set
        {
            _pixelsPerTile = value;
            QueueRedraw();
        }
    }

    public override void _Draw()
    {
        float ppt = _pixelsPerTile;
        Vector2 ToScreen(Vector2 tile) => (Size / 2f) + (tile * ppt);

        // As `DrawTheWoods` lays them: each tile's canopies, darkened by their own shade.
        for (int y = Wood.Position.Y; y < Wood.End.Y; y++)
        {
            for (int x = Wood.Position.X; x < Wood.End.X; x++)
            {
                var tile = new GridPos(x, y);
                for (int i = 0; i < VillageMap.TreesOn(tile, grown: true); i++)
                {
                    (Vector2 where, float shade) = VillageMap.CanopyOn(tile, i);
                    Color leaf = VillageMap.TreeCanopy;
                    DrawCircle(ToScreen(where), VillageMap.CanopyRadius * shade * ppt, leaf with { R = leaf.R * shade, G = leaf.G * shade, B = leaf.B * shade });
                }
            }
        }

        // As `DrawStores` draws a granary: its footprint at 80 %, the fill a little clear, a 2px edge.
        Color store = VillageMap.ColourOf(StoreKind.Granary);
        Vector2[] granary = VillageMap.FootprintQuadAt(ToScreen(Granary), _granary.X * 0.8f, _granary.Y * 0.8f, 0, ppt);
        DrawColoredPolygon(granary, store with { A = 0.85f });
        for (int i = 0; i < 4; i++)
        {
            DrawLine(granary[i], granary[(i + 1) % 4], store, 2f);
        }

        // As `DrawHomes` draws an occupied house: its footprint at 80 %, and the door's notch on its front.
        foreach ((Vector2 at, ushort facing) in Homes)
        {
            DrawColoredPolygon(VillageMap.FootprintQuadAt(ToScreen(at), _home.X * 0.8f, _home.Y * 0.8f, facing, ppt), VillageMap.HomeColour);
            DrawColoredPolygon(VillageMap.FootprintQuadAt(ToScreen(at), 0.28f, _home.Y * 0.8f, facing, ppt, from: 0f, to: 0.22f), VillageMap.DoorColour);
        }

        // As `DrawVillagers`: a dot, never smaller than 3px.
        float radius = Mathf.Max(3f, ppt * 0.2f);
        foreach ((Vector2 at, Color colour) in Villagers)
        {
            DrawCircle(ToScreen(at), radius, colour);
        }
    }

    private static Vector2 ExtentOf(IReadOnlyList<BuildingRow> rows, BuildingKind kind)
    {
        foreach (BuildingRow row in rows)
        {
            if (row.Id == (int)kind)
            {
                return new Vector2(row.ExtentWidth, row.ExtentHeight);
            }
        }

        return Vector2.One;
    }
}

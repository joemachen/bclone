using Bclone.Sim.Config;
using Bclone.Sim.Core;
using Bclone.Sim.World;
using Godot;

namespace Bclone.Game;

/// <summary>
/// The whole valley at a glance, with a box round what the camera can see (Joe's area 5).
/// </summary>
/// <remarks>
/// <para>
/// <b>Why it exists.</b> The map is 120×80 and the camera shows a fraction of it, so getting
/// lost is easy — <c>Centre on village</c> is the way back the shell has had until now, and it
/// only answers <em>"where is home?"</em>, never <em>"what is over there?"</em>. A valley you
/// can see all of is the difference between panning to look for your woods and knowing where
/// they are.
/// </para>
/// <para>
/// <b>⚠️ The terrain is baked into a texture, not redrawn.</b> Nine thousand six hundred rects
/// a frame is exactly the per-frame full-map walk this project has been bitten by twice in the
/// sim (D87's <c>NearestHarvest</c>, D112's ring scan), and there is no reason for the view to
/// repeat it. The bake is invalidated through <see cref="SimWorld.TerrainGeneration"/> — the
/// same counter the hut rings read, so there is one answer to <em>"has the ground changed?"</em>
/// and the minimap cannot come to disagree with the sim about it.
/// </para>
/// <para>
/// <b>Buildings and the camera box are drawn live</b>, because they change constantly and there
/// are tens of them rather than thousands. People are deliberately <em>not</em> drawn: at one
/// pixel per tile a villager is smaller than the dot marking the house they live in, so they
/// would read as noise over the thing they are standing on.
/// </para>
/// </remarks>
public partial class Minimap : Control
{
    /// <summary>How wide the minimap is drawn. Its height follows the valley's shape.</summary>
    private const int WidthPixels = 250;

    private SimWorld? _world;
    private Rect2 _view;
    private ImageTexture? _valley;
    private int _bakedAtGeneration = -1;

    /// <summary>The player asked to look somewhere else, in tile coordinates.</summary>
    public event System.Action<Vector2>? LookAt;

    public override void _Ready()
    {
        MouseFilter = MouseFilterEnum.Stop;

        // Nearest-neighbour, stated rather than inherited. The bake is one pixel per tile and
        // is drawn two or three times that size, and Godot's default filtering is linear — so
        // left alone the river gets soft edges, and water reading as an obstacle at a glance
        // is the one thing its colour was chosen for (D40).
        TextureFilter = TextureFilterEnum.Nearest;

        // ⚠️ IT DOES NOT TAKE THE COLUMN'S WIDTH, and that is a layout fix rather than a
        // preference. Stretched to the full 400 the valley is 267 pixels tall, and the right
        // column is a stack — so the log and the panel describing whatever you clicked were
        // pushed down under the control bar. A minimap is a glance, not a view; it costs the
        // column as little height as it can and still be read.
        SizeFlagsHorizontal = SizeFlags.ShrinkCenter;
    }

    /// <summary>
    /// Hand the minimap what to draw. Called every frame by <see cref="Main"/>.
    /// </summary>
    public void Present(SimWorld world, Rect2 visibleTiles)
    {
        _world = world;
        _view = visibleTiles;

        SimConfig config = world.Config;
        int tilesWide = config.MapMaxX - config.MapMinX + 1;
        int tilesTall = config.MapMaxY - config.MapMinY + 1;

        // ⚠️ TALL ENOUGH FOR THE SHAPE THE VALLEY ACTUALLY IS, and no taller. Asking for a
        // width and a fixed height while the container was free to stretch the width fixed
        // the small map at 2.9:1 against a valley of 1.5:1 — the river flat across a squashed
        // map, which is the one thing a minimap must never do. The height comes from the
        // width and the shrink flag in `_Ready` stops anything changing the width; `_Draw`
        // letterboxes as well, so even a frame where those disagree is a margin rather than
        // a distortion.
        CustomMinimumSize = new Vector2(
            WidthPixels, Mathf.Round(WidthPixels * (float)tilesTall / tilesWide));

        if (_bakedAtGeneration != world.TerrainGeneration)
        {
            Bake(world, tilesWide, tilesTall);
            _bakedAtGeneration = world.TerrainGeneration;
        }

        QueueRedraw();
    }

    /// <summary>
    /// Paint every tile into a one-pixel-per-tile image, once.
    /// </summary>
    /// <remarks>
    /// One pixel per tile, scaled up at draw time — see <see cref="_Ready"/> for why the
    /// filtering is stated there rather than left to the default.
    /// </remarks>
    private void Bake(SimWorld world, int tilesWide, int tilesTall)
    {
        SimConfig config = world.Config;
        Image image = Image.CreateEmpty(tilesWide, tilesTall, false, Image.Format.Rgba8);

        for (int y = 0; y < tilesTall; y++)
        {
            for (int x = 0; x < tilesWide; x++)
            {
                Terrain terrain = world.Map.TerrainAt(
                    new GridPos(config.MapMinX + x, config.MapMinY + y));
                image.SetPixel(x, y, VillageMap.ColourOf(terrain));
            }
        }

        _valley = ImageTexture.CreateFromImage(image);
    }

    public override void _Draw()
    {
        if (_world is null || _valley is null)
        {
            return;
        }

        DrawRect(new Rect2(Vector2.Zero, Size), VillageMap.BeyondColour);
        DrawTextureRect(_valley, Valley(), tile: false);

        DrawBuildings();

        // The camera box last, over everything, because it is the one thing here that
        // answers "where am I?" — and an outline rather than a wash, so it never hides the
        // ground it is telling you about.
        DrawRect(TilesToPixels(_view), new Color(1f, 0.76f, 0.31f, 0.85f), filled: false, width: 1.5f);

        // A hairline round the valley, so the minimap reads as a map of it rather than as
        // a texture that happens to be there.
        DrawRect(Valley(), new Color(1, 1, 1, 0.18f), filled: false);
    }

    /// <summary>
    /// The part of this control the valley is drawn in — <b>always the valley's own shape</b>.
    /// </summary>
    /// <remarks>
    /// Letterboxed rather than stretched. A container is free to hand a control whatever size
    /// it likes, and a minimap that quietly takes the shape of the box it was put in is worse
    /// than no minimap: every distance on it is a lie, and the player has no way to know.
    /// Every tile-to-pixel conversion goes through this, so the dots, the camera box and the
    /// ground cannot come apart.
    /// </remarks>
    private Rect2 Valley()
    {
        SimConfig config = _world!.Config;
        float tilesWide = config.MapMaxX - config.MapMinX + 1;
        float tilesTall = config.MapMaxY - config.MapMinY + 1;

        float scale = Mathf.Min(Size.X / tilesWide, Size.Y / tilesTall);
        var span = new Vector2(tilesWide * scale, tilesTall * scale);
        return new Rect2((Size - span) / 2f, span);
    }

    /// <summary>
    /// Where the village is, as dots. <b>Three colours for three kinds of thing.</b>
    /// </summary>
    /// <remarks>
    /// Borrowed from <see cref="VillageMap"/> rather than chosen again, so a granary is the
    /// same yellow on both maps. Sites are drawn like the buildings they will become: at this
    /// size a fourth colour for <em>not finished yet</em> would be a pixel nobody could read,
    /// and the big map is where that distinction is made.
    /// </remarks>
    private void DrawBuildings()
    {
        SimWorld world = _world!;
        float dot = Mathf.Max(2f, Size.X / 90f);

        foreach (Household household in world.Households)
        {
            if (household.HomePosition is not GridPos home)
            {
                continue;
            }

            // A house whose family has died still stands, and reads as abandoned — the same
            // rule the big map follows, because the same thing is true.
            Color colour = world.LivingMembersOf(household) > 0
                ? VillageMap.DwellingColour
                : VillageMap.DwellingColour with { A = 0.3f };

            Mark(home, colour, dot);
        }

        foreach (StoreBuilding store in world.StoreBuildings)
        {
            Mark(store.Position, VillageMap.StoreColour, dot);
        }

        foreach (Workplace workplace in world.Workplaces)
        {
            Mark(workplace.Position, VillageMap.BuildingColour, dot);
        }
    }

    private void Mark(GridPos tile, Color colour, float size)
    {
        Vector2 centre = TileToPixel(new Vector2(tile.X, tile.Y));
        DrawRect(new Rect2(centre - (Vector2.One * size / 2f), Vector2.One * size), colour);
    }

    public override void _GuiInput(InputEvent @event)
    {
        if (_world is null)
        {
            return;
        }

        // Click or drag to look somewhere. The whole point of seeing the valley at once is
        // being able to go to the part of it you just spotted, and Banished's minimap works
        // exactly this way.
        bool pressed = @event is InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left };
        bool dragged = @event is InputEventMouseMotion motion
            && motion.ButtonMask.HasFlag(MouseButtonMask.Left);

        if (!pressed && !dragged)
        {
            return;
        }

        Vector2 at = @event is InputEventMouse mouse ? mouse.Position : Vector2.Zero;
        LookAt?.Invoke(PixelToTile(at));
        AcceptEvent();
    }

    // ---------------------------------------------------------------
    //  Tiles ↔ pixels. One conversion each way, so they cannot disagree.
    // ---------------------------------------------------------------

    /// <summary>Pixels per tile. One number, because the valley is never stretched.</summary>
    private float Scale()
    {
        SimConfig config = _world!.Config;
        return Valley().Size.X / (config.MapMaxX - config.MapMinX + 1);
    }

    private Vector2 TileToPixel(Vector2 tile)
    {
        SimConfig config = _world!.Config;
        float scale = Scale();
        return Valley().Position + new Vector2(
            (tile.X - config.MapMinX + 0.5f) * scale,
            (tile.Y - config.MapMinY + 0.5f) * scale);
    }

    private Vector2 PixelToTile(Vector2 pixel)
    {
        SimConfig config = _world!.Config;
        float scale = Scale();
        Vector2 inside = pixel - Valley().Position;
        return new Vector2(
            (inside.X / scale) + config.MapMinX - 0.5f,
            (inside.Y / scale) + config.MapMinY - 0.5f);
    }

    private Rect2 TilesToPixels(Rect2 tiles) =>
        new(TileToPixel(tiles.Position), tiles.Size * Scale());
}

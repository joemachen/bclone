using Kith.Sim.Core;
using Kith.Sim.World;
using Godot;

namespace Kith.Game;

/// <summary>
/// A building's own map drawing, scaled up to fill a card's picture (D376, Joe: *"add building's
/// real map drawing scaled up"*): the footprint quad in the map's colour, turned as it is turned,
/// with its ring when the map would draw one.
/// </summary>
/// <remarks>
/// ⭐⭐ <b>AND NOW THE BUILDING AS THE MAP PAINTS IT</b> (A262, `specs/building-portrait.md`; Joe's play note 5, <i>"on building
/// windows, the building should look like the actual in-game version"</i>, and his pick of the mocks, <i>"Yes, as
/// recommended"</i>: B, 120, S1). With the watercolour tick on, a card's picture is the map's own painted building — the very
/// pieces the map laid (<see cref="VillageMap.PaintedAt"/>), turned as it stands, its yard and stock with it, its wear and
/// a ruin's fallen roof — scaled by its footprint, its yard running off the edges. A site is drawn as the map draws a site:
/// the outline filling as it is built (over the building, when it is a repair). With the tick off, the map draws rectangles
/// and so does this, as it always did. A card that only calls <see cref="Show"/> (a bridge, a posed hall, a new building no
/// one has painted) keeps the rectangle.
/// </remarks>
public sealed partial class BuildingPortrait : Control
{
    /// <summary>Room round a painted building's footprint, each side, in tiles.</summary>
    internal const float PaintedMargin = 0.35f;

    private Color _fill;
    private float _wide = 0.8f;
    private float _tall = 0.8f;
    private ushort _facing;
    private Color? _ring;
    private Condition _wear;
    private BuildingKind? _house;

    private VillageMap? _map;
    private SiteLook? _site;

    /// <summary>The map's runs this picture last drew, laid again for the picture's own scale only when they or it change.</summary>
    private VillageMap.RoofCache? _laidFor;
    private float _laidPpt;
    private readonly List<VillageMap.RoofRun> _laid = new();

    public BuildingPortrait()
    {
        ClipContents = true;
    }

    /// <summary>A site as the map draws it: its fill and edge colours and how full it is (<see cref="VillageMap.SiteShown"/>).</summary>
    internal readonly record struct SiteLook(Color Fill, Color Edge, float Shown);

    /// <summary>The building's condition — the portrait weathers as the map does (D556).</summary>
    public Condition Wear
    {
        get => _wear;
        set
        {
            if (_wear != value)
            {
                _wear = value;
                QueueRedraw();
            }
        }
    }

    /// <summary>The map's painted building this picture shows, or null for the rectangle (the probe asks).</summary>
    internal VillageMap.RoofCache? Painted { get; private set; }

    /// <summary>The site this picture shows, or null (the probe asks).</summary>
    internal SiteLook? Site => _site;

    /// <remarks><c>house</c> is a house's row, for the marks its tier wears inside its walls (D722) — null for every other building.</remarks>
    public void Show(Color fill, float wideTiles, float tallTiles, ushort facing, Color? ring, BuildingKind? house = null)
    {
        _fill = fill;
        _wide = wideTiles;
        _tall = tallTiles;
        _facing = facing;
        _ring = ring;
        _house = house;
        Painted = null;
        _site = null;
        QueueRedraw();
    }

    /// <summary>After <see cref="Show"/>: the building the map painted where it stands, if the map is painting.</summary>
    internal void Paint(VillageMap map, Point? at)
    {
        _map = map;
        Painted = at is Point p ? map.PaintedAt(VillageMap.TileOf(p)) : null;
        QueueRedraw();
    }

    /// <summary>After <see cref="Show"/>: a workplace — painted as it stands, or a site as the map draws one.</summary>
    internal void Paint(VillageMap map, SimWorld world, Workplace place)
    {
        Paint(map, place.Position);
        if (place.Construction is ConstructionSite site)
        {
            (Vector2 _, float wide, float deep, ushort facing, Color fill, Color edge) = map.SiteShapeOf(world, place);
            _wide = wide;
            _tall = deep;
            _facing = facing;
            _site = new SiteLook(fill, edge, VillageMap.SiteShown(site));
        }
    }

    public override void _Draw()
    {
        // A picture of a world thing sits on the world's own ground (D637): a parchment chip, the building inked on it as
        // the map draws it — so the card and the map stay one palette while the card's chrome stays slate.
        DrawRect(new Rect2(Vector2.Zero, Size), VillageMap.Parchment);
        Vector2 centre = Size / 2f;

        if (Painted is not null || _site is not null)
        {
            float ppt = Mathf.Min(Size.X, Size.Y) / (Mathf.Max(_wide, _tall) + (2f * PaintedMargin));
            if (Painted is VillageMap.RoofCache held)
            {
                DrawThePainting(held, ppt);
            }

            if (_site is SiteLook site)
            {
                DrawTheSite(site, centre, ppt);
            }

            DrawTheRing(centre, ppt);
            return;
        }

        float longest = Mathf.Max(_wide, _tall) + 0.6f;
        float pixelsPerTile = Mathf.Min(Size.X, Size.Y) / longest;

        Vector2[] quad = VillageMap.FootprintQuadAt(centre, _wide, _tall, _facing, pixelsPerTile);
        Color fill = VillageMap.Weathered(_fill, _wear);
        DrawColoredPolygon(quad, fill with { A = 0.85f });
        if (_house is BuildingKind house)
        {
            foreach ((Vector2[] shape, Color colour) in VillageMap.HouseMarksAt(house, centre, _wide, _tall, _facing, pixelsPerTile))
            {
                DrawColoredPolygon(shape, VillageMap.Weathered(colour, _wear));
            }
        }

        for (int i = 0; i < 4; i++)
        {
            if (_wear == Condition.Ruin)
            {
                DrawDashedLine(quad[i], quad[(i + 1) % 4], VillageMap.Ink, 2f, 4f);
            }
            else
            {
                DrawLine(quad[i], quad[(i + 1) % 4], VillageMap.Ink, 2f);
            }
        }

        DrawTheRing(centre, pixelsPerTile);
    }

    private void DrawTheRing(Vector2 centre, float pixelsPerTile)
    {
        if (_ring is Color ring)
        {
            DrawArc(centre, pixelsPerTile * 0.8f * 0.85f, 0f, Mathf.Tau, 32, ring, 2f);
        }
    }

    /// <summary>
    /// The map's runs through one transform, its centre on the picture's: fills and paintings as the map laid them, the pen
    /// lines laid again for this scale (their width is in pixels). ⚠️ The meshes are held until they are laid again — one laid
    /// and dropped is freed before Godot draws it (A261's trap).
    /// </summary>
    private void DrawThePainting(VillageMap.RoofCache held, float ppt)
    {
        if (!ReferenceEquals(_laidFor, held) || _laidPpt != ppt)
        {
            _laid.Clear();
            foreach (VillageMap.RoofRun run in held.Runs)
            {
                _laid.Add(run.Lines is { } lines ? VillageMap.RelaidForTheZoom(run, lines, ppt) : run);
            }

            _laidFor = held;
            _laidPpt = ppt;
        }

        var place = new Transform2D(new Vector2(ppt, 0f), new Vector2(0f, ppt), (Size / 2f) - (held.Signature.At * ppt));
        Rid canvas = GetCanvasItem();
        foreach (VillageMap.RoofRun run in _laid)
        {
            if (run.Mesh.GetSurfaceCount() == 0)
            {
                continue;
            }

            // A mill's sails stand where the map's are.
            Transform2D at = run.Turns ? place * VillageMap.SailsTurned(held.Hub, held.SailsAt) : place;
            if (run.Material is RoofMaterial material)
            {
                CanvasTexture? painting = _map?.RoofTexture(material);
                float lift = 1f / VillageMap.RoofTextureMean;
                RenderingServer.CanvasItemAddMesh(canvas, run.Mesh.GetRid(), at, new Color(lift, lift, lift), painting?.GetRid() ?? default);
            }
            else
            {
                RenderingServer.CanvasItemAddMesh(canvas, run.Mesh.GetRid(), at, Colors.White);
            }
        }
    }

    /// <summary>A site as <c>VillageMap.DrawWorkplaces</c> draws one: a wash, the band filled from its own bottom edge, the edge.</summary>
    private void DrawTheSite(SiteLook site, Vector2 centre, float ppt)
    {
        Vector2[] quad = VillageMap.FootprintQuadAt(centre, _wide, _tall, _facing, ppt);
        DrawColoredPolygon(quad, site.Fill with { A = 0.18f });
        if (site.Shown > 0f)
        {
            DrawColoredPolygon(VillageMap.FootprintQuadAt(centre, _wide, _tall, _facing, ppt, 1f - site.Shown, 1f), site.Fill with { A = 0.55f });
        }

        for (int i = 0; i < 4; i++)
        {
            DrawLine(quad[i], quad[(i + 1) % 4], site.Edge, 2f);
        }
    }
}

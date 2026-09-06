using Bclone.Sim.World;
using Godot;

namespace Bclone.Game;

/// <summary>
/// A small drawn mark for a building — <b>the game's own shapes, not an imported icon</b>.
/// </summary>
/// <remarks>
/// <para>
/// ⭐ <b><see cref="TradeGlyph"/>'s rules, applied to the build bar</b> (`specs/build-bar.md §4`).
/// The project ships <b>no image assets</b> (D26) and the goods table rejects emoji because a
/// glyph <em>"is at the mercy of whatever the default font happens to cover"</em>. Two to four
/// shapes each, read at a centimetre across, so these are silhouettes and not drawings.
/// </para>
/// <para>
/// ⭐⭐ <b>A workplace reads as the colour of the trade that works it</b>, and the stores read as
/// the colour the map paints them. So the icon on the bar, the mark in the professions table and
/// the building in the valley are <em>one fact</em>. The tones come from
/// <see cref="VillageMap"/>'s own constants rather than from a restated hex, which is the one
/// thing <see cref="TradeGlyph"/> did not do.
/// </para>
/// <para>
/// ⚠️ <b>A modder's building gets a plain drab mark rather than a crash</b> — the same answer
/// <see cref="TradeGlyph"/> gives an unrecognised trade and <see cref="GoodsPalette"/> an
/// unrecognised good. <b>The greyness says nobody chose this shape</b>, rather than implying
/// somebody did.
/// </para>
/// </remarks>
public sealed partial class BuildingGlyph : Control
{
    /// <summary>Drawn at this size, whatever the button does around it.</summary>
    private const float Side = 14f;

    /// <summary>Nobody's colour, for a building nobody has drawn.</summary>
    private static readonly Color Unchosen = new(0.70f, 0.70f, 0.70f);

    private readonly BuildingKind _kind;
    private readonly bool _known;

    /// <param name="kind">Which building. Ids past the built-ins get the plain mark.</param>
    /// <param name="known">
    /// False for a row this class has no shape for — a modder's building. Kept as a parameter
    /// rather than inferred from an enum range, because <see cref="BuildingKind"/> is appended to
    /// and a range check here would go stale the day a fifteenth built-in lands.
    /// </param>
    public BuildingGlyph(BuildingKind kind, bool known = true)
    {
        _kind = kind;
        _known = known;
        CustomMinimumSize = new Vector2(Side, Side);
        SizeFlagsVertical = SizeFlags.ShrinkCenter;
        SizeFlagsHorizontal = SizeFlags.ShrinkCenter;
        MouseFilter = MouseFilterEnum.Ignore;
    }

    /// <summary>The colour a building reads as — borrowed, never invented.</summary>
    internal static Color ColourOf(BuildingKind kind) => kind switch
    {
        // The stores, from the map's own palette.
        BuildingKind.Granary => VillageMap.StoreColour,
        BuildingKind.Warehouse => VillageMap.StoreTone,
        BuildingKind.Market => VillageMap.MarketTone,
        BuildingKind.Pile => VillageMap.StoneTone,
        BuildingKind.Home => VillageMap.DwellingColour,
        BuildingKind.Library => VillageMap.LibraryTone,
        BuildingKind.TownHall => VillageMap.CivicTone,

        // The workplaces, from the trade that works them.
        BuildingKind.BuilderHut => TradeGlyph.ColourOf(JobKind.Builder),
        BuildingKind.GathererHut => TradeGlyph.ColourOf(JobKind.Forager),
        BuildingKind.ForesterHut => TradeGlyph.ColourOf(JobKind.Forester),
        BuildingKind.WoodcutterHut => TradeGlyph.ColourOf(JobKind.Woodcutter),
        BuildingKind.Farmhouse => TradeGlyph.ColourOf(JobKind.Farmer),
        BuildingKind.FishingHut => TradeGlyph.ColourOf(JobKind.Fisher),
        BuildingKind.HunterLodge => TradeGlyph.ColourOf(JobKind.Hunter),

        _ => Unchosen,
    };

    public override void _Draw()
    {
        float s = Side;

        if (!_known)
        {
            // Honestly nobody's shape.
            DrawRect(new Rect2(s * 0.22f, s * 0.22f, s * 0.56f, s * 0.56f), Unchosen);
            return;
        }

        Color ink = ColourOf(_kind);

        switch (_kind)
        {
            // A bin: a tall body under a peak.
            case BuildingKind.Granary:
                Roof(new Rect2(s * 0.22f, s * 0.12f, s * 0.56f, s * 0.26f), ink);
                DrawRect(new Rect2(s * 0.28f, s * 0.38f, s * 0.44f, s * 0.48f), ink);
                break;

            // A shed: wide, low, and long enough to hold everything.
            case BuildingKind.Warehouse:
                Roof(new Rect2(s * 0.06f, s * 0.22f, s * 0.88f, s * 0.22f), ink);
                DrawRect(new Rect2(s * 0.12f, s * 0.44f, s * 0.76f, s * 0.42f), ink);
                break;

            // A stall: an awning over a counter. The marketer's own glyph, one building over.
            case BuildingKind.Market:
                Roof(new Rect2(s * 0.08f, s * 0.16f, s * 0.84f, s * 0.28f), ink);
                DrawRect(new Rect2(s * 0.20f, s * 0.52f, s * 0.60f, s * 0.30f), ink);
                break;

            // A heap on bare ground: goods stacked, and nothing built over them (D76).
            case BuildingKind.Pile:
                DrawColoredPolygon(
                    new[]
                    {
                        new Vector2(s * 0.50f, s * 0.28f),
                        new Vector2(s * 0.88f, s * 0.72f),
                        new Vector2(s * 0.12f, s * 0.72f),
                    }, ink);
                DrawRect(new Rect2(s * 0.10f, s * 0.72f, s * 0.80f, s * 0.12f), ink);
                break;

            // A house: a roof, a wall and a door.
            case BuildingKind.Home:
                Roof(new Rect2(s * 0.14f, s * 0.14f, s * 0.72f, s * 0.28f), ink);
                DrawRect(new Rect2(s * 0.22f, s * 0.42f, s * 0.56f, s * 0.44f), ink);
                DrawRect(new Rect2(s * 0.44f, s * 0.60f, s * 0.14f, s * 0.26f), VillageMap.BeyondColour);
                break;

            // A hut with a hammer laid across it.
            case BuildingKind.BuilderHut:
                Hut(ink);
                DrawRect(new Rect2(s * 0.16f, s * 0.52f, s * 0.68f, s * 0.12f), VillageMap.BeyondColour);
                break;

            // A hut with a basket at the door.
            case BuildingKind.GathererHut:
                Hut(ink);
                DrawArc(new Vector2(s * 0.72f, s * 0.66f), s * 0.18f, 0f, Mathf.Pi, 10, VillageMap.BeyondColour, 1.8f);
                break;

            // A hut with a conifer standing beside it.
            case BuildingKind.ForesterHut:
                Hut(ink);
                Tree(new Vector2(s * 0.80f, s * 0.86f), s * 0.34f, VillageMap.TimberTone);
                break;

            // A hut over a split log.
            case BuildingKind.WoodcutterHut:
                Hut(ink);
                DrawLine(
                    new Vector2(s * 0.26f, s * 0.86f),
                    new Vector2(s * 0.74f, s * 0.56f),
                    VillageMap.BeyondColour,
                    1.6f);
                break;

            // A house over furrows: the farm is the ground, not the building.
            case BuildingKind.Farmhouse:
                Roof(new Rect2(s * 0.18f, s * 0.10f, s * 0.64f, s * 0.22f), ink);
                DrawRect(new Rect2(s * 0.26f, s * 0.32f, s * 0.48f, s * 0.26f), ink);
                for (int i = 0; i < 2; i++)
                {
                    DrawRect(new Rect2(s * 0.10f, s * (0.66f + (i * 0.16f)), s * 0.80f, s * 0.08f), ink);
                }

                break;

            // A hut on stilts, standing in the water.
            case BuildingKind.FishingHut:
                Roof(new Rect2(s * 0.16f, s * 0.10f, s * 0.68f, s * 0.24f), ink);
                DrawRect(new Rect2(s * 0.24f, s * 0.34f, s * 0.52f, s * 0.26f), ink);
                DrawRect(new Rect2(s * 0.28f, s * 0.60f, s * 0.08f, s * 0.18f), ink);
                DrawRect(new Rect2(s * 0.64f, s * 0.60f, s * 0.08f, s * 0.18f), ink);
                DrawRect(new Rect2(s * 0.06f, s * 0.80f, s * 0.88f, s * 0.10f), VillageMap.WaterTone);
                break;

            // A low lodge under a drawn bow.
            case BuildingKind.HunterLodge:
                Roof(new Rect2(s * 0.10f, s * 0.34f, s * 0.80f, s * 0.24f), ink);
                DrawRect(new Rect2(s * 0.18f, s * 0.58f, s * 0.64f, s * 0.28f), ink);
                DrawArc(new Vector2(s * 0.40f, s * 0.22f), s * 0.22f, -Mathf.Pi / 2.2f, Mathf.Pi / 2.2f, 10, ink, 1.6f);
                break;

            // A book, open: two leaves and the spine between them.
            case BuildingKind.Library:
                DrawRect(new Rect2(s * 0.10f, s * 0.26f, s * 0.36f, s * 0.50f), ink);
                DrawRect(new Rect2(s * 0.54f, s * 0.26f, s * 0.36f, s * 0.50f), ink);
                DrawLine(new Vector2(s * 0.50f, s * 0.20f), new Vector2(s * 0.50f, s * 0.82f), ink, 1.6f);
                break;

            // A pediment on columns: the one building the village raises for itself (D251).
            case BuildingKind.TownHall:
                DrawColoredPolygon(
                    new[]
                    {
                        new Vector2(s * 0.50f, s * 0.10f),
                        new Vector2(s * 0.94f, s * 0.40f),
                        new Vector2(s * 0.06f, s * 0.40f),
                    }, ink);
                for (int i = 0; i < 3; i++)
                {
                    DrawRect(new Rect2(s * (0.18f + (i * 0.26f)), s * 0.44f, s * 0.14f, s * 0.34f), ink);
                }

                DrawRect(new Rect2(s * 0.08f, s * 0.80f, s * 0.84f, s * 0.10f), ink);
                break;

            // A built-in nobody has drawn yet — the same honest mark a modder's building gets.
            default:
                DrawRect(new Rect2(s * 0.22f, s * 0.22f, s * 0.56f, s * 0.56f), ink);
                break;
        }
    }

    /// <summary>A gable over the given span — the shape every building here starts from.</summary>
    private void Roof(Rect2 span, Color ink) =>
        DrawColoredPolygon(
            new[]
            {
                new Vector2(span.Position.X + (span.Size.X * 0.5f), span.Position.Y),
                new Vector2(span.Position.X + span.Size.X, span.Position.Y + span.Size.Y),
                new Vector2(span.Position.X, span.Position.Y + span.Size.Y),
            }, ink);

    /// <summary>The common hut: a roof and a wall, left of centre so a mark fits beside it.</summary>
    private void Hut(Color ink)
    {
        float s = Side;
        Roof(new Rect2(s * 0.08f, s * 0.14f, s * 0.62f, s * 0.26f), ink);
        DrawRect(new Rect2(s * 0.16f, s * 0.40f, s * 0.46f, s * 0.46f), ink);
    }

    /// <summary>One conifer — <see cref="TradeGlyph"/>'s tree, so the two agree.</summary>
    private void Tree(Vector2 foot, float height, Color ink)
    {
        float w = height * 0.62f;
        DrawRect(new Rect2(foot.X - (w * 0.10f), foot.Y - (height * 0.10f), w * 0.20f, height * 0.22f), ink);
        DrawColoredPolygon(
            new[]
            {
                new Vector2(foot.X, foot.Y - height),
                new Vector2(foot.X + w, foot.Y - (height * 0.08f)),
                new Vector2(foot.X - w, foot.Y - (height * 0.08f)),
            }, ink);
    }
}

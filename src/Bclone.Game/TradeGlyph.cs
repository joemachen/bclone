using Bclone.Sim.World;
using Godot;

namespace Bclone.Game;

/// <summary>
/// A small drawn mark for a trade — <b>the game's own shapes, not an imported icon</b>.
/// </summary>
/// <remarks>
/// <para>
/// ⭐ <b>Joe asked for icons on the professions table and this is what an icon is here.</b> The
/// project ships <b>no image assets</b> (D26), and the goods table's own note explains why emoji
/// were rejected in its place: a glyph *"is at the mercy of whatever the default font happens to
/// cover"*. A shape painted from polygons and rects renders identically on every machine, needs no
/// file, and is the same visual language as the valley itself — <b>the real thing rather than a
/// placeholder for one</b>.
/// </para>
/// <para>
/// ⚠️ <b>These are read at a centimetre across, so they are silhouettes and not drawings.</b> Two
/// to four shapes each. The colour carries as much of the meaning as the outline does, which is
/// why every one that can borrow from the map does — <c>VillageMap</c> already paints a forester's
/// trees, a river and a game animal, and a trade whose glyph disagreed with its own tiles would be
/// worse than no glyph.
/// </para>
/// <para>
/// <b>`Minimap` is the precedent</b> for a <c>Control</c> that paints itself in this project.
/// </para>
/// </remarks>
public sealed partial class TradeGlyph : Control
{
    /// <summary>Drawn at this size, whatever the row does around it.</summary>
    private const float Side = 14f;

    private readonly JobKind _kind;

    public TradeGlyph(JobKind kind)
    {
        _kind = kind;
        CustomMinimumSize = new Vector2(Side, Side);
        SizeFlagsVertical = SizeFlags.ShrinkCenter;
        MouseFilter = MouseFilterEnum.Ignore;
    }

    /// <summary>
    /// The colour a trade reads as — <b>borrowed from the map wherever the map has one</b>.
    /// </summary>
    /// <remarks>
    /// The same rule <see cref="GoodsPalette"/> follows, and for the same reason: a mark in a panel
    /// and a thing in the valley meaning different colours would be two facts rather than one.
    /// </remarks>
    /// <remarks>
    /// ⚠️ <b>Internal, because <see cref="BuildingGlyph"/> asks it</b>: a hut and the person who
    /// works it read as the same colour, which is one fact rather than two. It also means these
    /// five hexes are now borrowed from in a second place — see <c>VillageMap.TimberTone</c> and
    /// its siblings, which is where a colour borrowed from the map <em>should</em> come from.
    /// </remarks>
    internal static Color ColourOf(JobKind kind) => kind switch
    {
        // Borrowed: the forester's trees, the river, the animals in the woods, the market's stall.
        JobKind.Forester => new Color("#2f6b3a"),
        JobKind.Fisher => new Color("#2f5f7a"),
        JobKind.Hunter => new Color("#8a6a3f"),
        JobKind.Marketer => new Color("#c98f4a"),
        JobKind.Farmer => new Color("#4a3a2b"),

        // Chosen here, because these trades have no ground of their own: the forager takes the
        // berry red the produce chip uses, the woodcutter the firewood orange, the builder the
        // pale grey of tools.
        JobKind.Forager => new Color(0.82f, 0.35f, 0.38f),
        JobKind.Woodcutter => new Color(0.88f, 0.55f, 0.24f),
        JobKind.Builder => new Color(0.72f, 0.76f, 0.82f),

        // ⚠️ A MODDER'S TRADE GETS A MARK RATHER THAN A CRASH, and it is deliberately drab — the
        // same answer `GoodsPalette` gives a good nobody has chosen a colour for.
        _ => new Color(0.70f, 0.70f, 0.70f),
    };

    public override void _Draw()
    {
        Color ink = ColourOf(_kind);
        float s = Side;

        switch (_kind)
        {
            // Two conifers: the forester's own tiles, shrunk.
            case JobKind.Forester:
                Tree(new Vector2(s * 0.32f, s * 0.55f), s * 0.30f, ink);
                Tree(new Vector2(s * 0.68f, s * 0.75f), s * 0.36f, ink);
                break;

            // A basket: a shallow bowl with a handle over it.
            case JobKind.Forager:
                DrawRect(new Rect2(s * 0.18f, s * 0.50f, s * 0.64f, s * 0.34f), ink);
                DrawArc(new Vector2(s * 0.5f, s * 0.50f), s * 0.30f, Mathf.Pi, Mathf.Tau, 10, ink, 1.6f);
                break;

            // Split wood: two wedges leaning apart from one cut.
            case JobKind.Woodcutter:
                DrawColoredPolygon(
                    new[]
                    {
                        new Vector2(s * 0.44f, s * 0.16f),
                        new Vector2(s * 0.44f, s * 0.84f),
                        new Vector2(s * 0.16f, s * 0.84f),
                    }, ink);
                DrawColoredPolygon(
                    new[]
                    {
                        new Vector2(s * 0.56f, s * 0.16f),
                        new Vector2(s * 0.84f, s * 0.84f),
                        new Vector2(s * 0.56f, s * 0.84f),
                    }, ink);
                break;

            // A stall: an awning over a counter.
            case JobKind.Marketer:
                DrawColoredPolygon(
                    new[]
                    {
                        new Vector2(s * 0.10f, s * 0.44f),
                        new Vector2(s * 0.50f, s * 0.14f),
                        new Vector2(s * 0.90f, s * 0.44f),
                    }, ink);
                DrawRect(new Rect2(s * 0.20f, s * 0.52f, s * 0.60f, s * 0.30f), ink);
                break;

            // A hammer: head and handle.
            case JobKind.Builder:
                DrawRect(new Rect2(s * 0.18f, s * 0.18f, s * 0.64f, s * 0.22f), ink);
                DrawRect(new Rect2(s * 0.42f, s * 0.40f, s * 0.16f, s * 0.44f), ink);
                break;

            // Furrows: three rows of worked ground.
            case JobKind.Farmer:
                for (int i = 0; i < 3; i++)
                {
                    DrawRect(new Rect2(s * 0.14f, s * (0.28f + (i * 0.22f)), s * 0.72f, s * 0.12f), ink);
                }

                break;

            // A fish: a body and a tail.
            case JobKind.Fisher:
                DrawColoredPolygon(
                    new[]
                    {
                        new Vector2(s * 0.16f, s * 0.50f),
                        new Vector2(s * 0.56f, s * 0.24f),
                        new Vector2(s * 0.76f, s * 0.50f),
                        new Vector2(s * 0.56f, s * 0.76f),
                    }, ink);
                DrawColoredPolygon(
                    new[]
                    {
                        new Vector2(s * 0.76f, s * 0.50f),
                        new Vector2(s * 0.94f, s * 0.30f),
                        new Vector2(s * 0.94f, s * 0.70f),
                    }, ink);
                break;

            // A bow, drawn: the stave and the string.
            case JobKind.Hunter:
                DrawArc(new Vector2(s * 0.34f, s * 0.5f), s * 0.36f, -Mathf.Pi / 2.2f, Mathf.Pi / 2.2f, 12, ink, 1.8f);
                DrawLine(new Vector2(s * 0.66f, s * 0.18f), new Vector2(s * 0.66f, s * 0.82f), ink, 1.2f);
                break;

            // A modder's trade: a plain mark, honestly nobody's shape.
            default:
                DrawRect(new Rect2(s * 0.22f, s * 0.22f, s * 0.56f, s * 0.56f), ink);
                break;
        }
    }

    /// <summary>One conifer — a stacked pair of triangles over a trunk.</summary>
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

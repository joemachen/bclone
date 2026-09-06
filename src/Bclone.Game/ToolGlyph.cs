using Godot;

namespace Bclone.Game;

/// <summary>What a non-building button on the build bar does.</summary>
/// <remarks>
/// ⭐ <b>A view enum, not a <see cref="Bclone.Sim.World.BuildingKind"/> and not a
/// <c>VillageMap.MapTool</c>.</b> It is neither: <c>MapTool</c> says what is in the player's
/// hand and <b>has values that are deliberately not on the bar</b> (the work-ground brush),
/// while this says what a <em>mark</em> depicts. Two of these draw one <c>MapTool</c> —
/// <see cref="Unmark"/> and every harvest mode share <c>MapTool.Unmarking</c>'s tool.
/// </remarks>
public enum ToolMark
{
    /// <summary>Paint where the village may live (D42).</summary>
    PaintLand,

    /// <summary>Take that land back — and over a house, an order to pull it down.</summary>
    TakeBack,

    /// <summary>Pull a building down.</summary>
    Demolish,

    /// <summary>Pick a building up and put it down elsewhere (D229).</summary>
    Move,

    /// <summary>Carry a store out into the others (D231).</summary>
    Empty,

    /// <summary>Mark trees to be felled.</summary>
    HarvestTrees,

    /// <summary>Mark stone to be dug.</summary>
    HarvestStone,

    /// <summary>Mark iron to be dug.</summary>
    HarvestIron,

    /// <summary>Mark whatever is there.</summary>
    HarvestAll,

    /// <summary>Rub a marking out.</summary>
    Unmark,

    /// <summary>Put down whatever is in hand.</summary>
    Cancel,
}

/// <summary>
/// A small drawn mark for the bar's non-building buttons.
/// </summary>
/// <remarks>
/// <see cref="BuildingGlyph"/>'s rules exactly — two to four shapes, colours borrowed from
/// <see cref="VillageMap"/>, no image assets and no emoji (D26). These are the buttons that act
/// on ground the village already has rather than putting something new on it.
/// </remarks>
public sealed partial class ToolGlyph : Control
{
    private const float Side = 14f;

    /// <summary>The pale hand of the player, for the tools that are pure instruction.</summary>
    private static readonly Color Hand = new(0.78f, 0.80f, 0.82f);

    /// <summary>Pulling down, and taking back — the map's own demolition orange.</summary>
    private static readonly Color Undo = new("#b5714a");

    private readonly ToolMark _mark;

    public ToolGlyph(ToolMark mark)
    {
        _mark = mark;
        CustomMinimumSize = new Vector2(Side, Side);
        SizeFlagsVertical = SizeFlags.ShrinkCenter;
        SizeFlagsHorizontal = SizeFlags.ShrinkCenter;
        MouseFilter = MouseFilterEnum.Ignore;
    }

    public override void _Draw()
    {
        float s = Side;

        switch (_mark)
        {
            // A patch of painted ground: filled, in the colour the map washes it.
            case ToolMark.PaintLand:
                DrawRect(new Rect2(s * 0.12f, s * 0.22f, s * 0.76f, s * 0.56f), VillageMap.DwellingColour);
                break;

            // The same patch, emptied. ⭐ AN OUTLINE IS THE ONLY HONEST OPPOSITE OF A FILL —
            // a second colour would read as a different kind of ground rather than as none.
            case ToolMark.TakeBack:
                DrawRect(new Rect2(s * 0.12f, s * 0.22f, s * 0.76f, s * 0.56f), Undo, filled: false, width: 1.4f);
                break;

            // A wall coming down: an upright course, and one that has toppled off it.
            case ToolMark.Demolish:
                DrawRect(new Rect2(s * 0.14f, s * 0.44f, s * 0.36f, s * 0.42f), Undo);
                DrawColoredPolygon(
                    new[]
                    {
                        new Vector2(s * 0.54f, s * 0.44f),
                        new Vector2(s * 0.90f, s * 0.58f),
                        new Vector2(s * 0.78f, s * 0.86f),
                        new Vector2(s * 0.54f, s * 0.86f),
                    }, Undo);
                break;

            // A building lifted and set down to the right.
            case ToolMark.Move:
                DrawRect(new Rect2(s * 0.08f, s * 0.32f, s * 0.34f, s * 0.36f), Hand, filled: false, width: 1.3f);
                DrawRect(new Rect2(s * 0.58f, s * 0.32f, s * 0.34f, s * 0.36f), Hand);
                DrawLine(new Vector2(s * 0.44f, s * 0.50f), new Vector2(s * 0.56f, s * 0.50f), Hand, 1.3f);
                break;

            // A store tipped out: a vessel leaning, and what came out of it.
            case ToolMark.Empty:
                DrawColoredPolygon(
                    new[]
                    {
                        new Vector2(s * 0.14f, s * 0.20f),
                        new Vector2(s * 0.58f, s * 0.20f),
                        new Vector2(s * 0.48f, s * 0.62f),
                        new Vector2(s * 0.24f, s * 0.62f),
                    }, Hand);
                DrawRect(new Rect2(s * 0.60f, s * 0.70f, s * 0.16f, s * 0.16f), Hand);
                DrawRect(new Rect2(s * 0.80f, s * 0.76f, s * 0.12f, s * 0.12f), Hand);
                break;

            // The three things the ground gives up, each in the colour the valley paints it.
            case ToolMark.HarvestTrees:
                Tree(new Vector2(s * 0.34f, s * 0.82f), s * 0.44f, VillageMap.TimberTone);
                Tree(new Vector2(s * 0.70f, s * 0.88f), s * 0.36f, VillageMap.TimberTone);
                break;

            case ToolMark.HarvestStone:
                Lump(VillageMap.StoneTone);
                break;

            case ToolMark.HarvestIron:
                Lump(VillageMap.IronTone);
                break;

            // Everything: one of each, small, side by side.
            case ToolMark.HarvestAll:
                DrawRect(new Rect2(s * 0.08f, s * 0.40f, s * 0.24f, s * 0.34f), VillageMap.TimberTone);
                DrawRect(new Rect2(s * 0.38f, s * 0.40f, s * 0.24f, s * 0.34f), VillageMap.StoneTone);
                DrawRect(new Rect2(s * 0.68f, s * 0.40f, s * 0.24f, s * 0.34f), VillageMap.IronTone);
                break;

            // A marking, struck through.
            case ToolMark.Unmark:
                DrawRect(new Rect2(s * 0.16f, s * 0.24f, s * 0.68f, s * 0.52f), Undo, filled: false, width: 1.3f);
                DrawLine(new Vector2(s * 0.16f, s * 0.76f), new Vector2(s * 0.84f, s * 0.24f), Undo, 1.4f);
                break;

            // Two strokes, and nothing else. The one button that is not about the ground at all.
            default:
                DrawLine(new Vector2(s * 0.22f, s * 0.22f), new Vector2(s * 0.78f, s * 0.78f), Hand, 1.6f);
                DrawLine(new Vector2(s * 0.78f, s * 0.22f), new Vector2(s * 0.22f, s * 0.78f), Hand, 1.6f);
                break;
        }
    }

    /// <summary>A lump of ore or rock: a squat, uneven mass.</summary>
    private void Lump(Color ink)
    {
        float s = Side;
        DrawColoredPolygon(
            new[]
            {
                new Vector2(s * 0.30f, s * 0.24f),
                new Vector2(s * 0.72f, s * 0.32f),
                new Vector2(s * 0.86f, s * 0.68f),
                new Vector2(s * 0.56f, s * 0.86f),
                new Vector2(s * 0.16f, s * 0.66f),
            }, ink);
    }

    /// <summary>One conifer — <see cref="TradeGlyph"/>'s tree, so the three agree.</summary>
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

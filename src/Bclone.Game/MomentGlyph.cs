using Bclone.Sim.World;
using Godot;

namespace Bclone.Game;

/// <summary>
/// The mark beside a paragraph of a moment — <b>the shape the map already draws for the thing</b> (D577).
/// </summary>
/// <remarks>
/// <para>
/// <see cref="BuildingGlyph"/>'s rules: a few shapes, colours borrowed from <see cref="VillageMap"/>, no image assets
/// and no emoji (D26). Larger than the bar's marks, because it sits beside body text rather than in a button.
/// </para>
/// <para>
/// ⭐ <b>The nomads' mark is the one that rides over their heads as they walk in</b> — a villager's dot under the dun
/// chevron (<see cref="VillageMap.ArrivalMark"/>) — so the panel and the walk-in it waited for are one picture.
/// <b>The bridge's is a deck over water</b> in the map's own deck colour. A mark this build does not know draws nothing.
/// </para>
/// </remarks>
public sealed partial class MomentGlyph : Control
{
    /// <summary>Drawn at this size, beside the paragraph's first line.</summary>
    internal const float Side = 26f;

    private readonly MomentMark _mark;

    public MomentGlyph(MomentMark mark)
    {
        _mark = mark;
        CustomMinimumSize = new Vector2(Side, Side);
        SizeFlagsVertical = SizeFlags.ShrinkBegin;
        SizeFlagsHorizontal = SizeFlags.ShrinkCenter;
        MouseFilter = MouseFilterEnum.Ignore;
    }

    /// <summary>Which mark this is — for the probe.</summary>
    internal MomentMark Mark => _mark;

    public override void _Draw()
    {
        float s = Side;

        switch (_mark)
        {
            // A villager's dot, and the chevron over their head on the road in.
            case MomentMark.Nomads:
                DrawCircle(new Vector2(s * 0.50f, s * 0.70f), s * 0.17f, VillageMap.AdultColour);
                DrawLine(new Vector2(s * 0.26f, s * 0.18f), new Vector2(s * 0.50f, s * 0.38f), VillageMap.ArrivalMark, 2.4f);
                DrawLine(new Vector2(s * 0.50f, s * 0.38f), new Vector2(s * 0.74f, s * 0.18f), VillageMap.ArrivalMark, 2.4f);
                break;

            // Water, and a timber deck across it on two piers.
            case MomentMark.Bridge:
                DrawRect(new Rect2(s * 0.04f, s * 0.62f, s * 0.92f, s * 0.26f), VillageMap.WaterTone);
                DrawRect(new Rect2(s * 0.24f, s * 0.44f, s * 0.10f, s * 0.36f), VillageMap.BridgeColour.Darkened(0.35f));
                DrawRect(new Rect2(s * 0.66f, s * 0.44f, s * 0.10f, s * 0.36f), VillageMap.BridgeColour.Darkened(0.35f));
                DrawRect(new Rect2(s * 0.04f, s * 0.34f, s * 0.92f, s * 0.14f), VillageMap.BridgeColour);
                break;
        }
    }
}

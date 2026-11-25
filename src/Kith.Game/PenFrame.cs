using Godot;

namespace Kith.Game;

/// <summary>
/// A filled box with a thin pen line drawn inside its edge (A242, <c>logo.md §9</c>) — the new valley's <i>Found</i>. A
/// <see cref="StyleBoxFlat"/> has one border; this is that box and a second line <see cref="Gap"/> px in from its edge.
/// </summary>
[GlobalClass]
public partial class PenFrame : StyleBox
{
    /// <summary>The box: its fill and its outer edge; none for a focus line alone.</summary>
    [Export]
    public StyleBoxFlat? Box { get; set; }

    /// <summary>The inner line's colour.</summary>
    [Export]
    public Color Line { get; set; }

    /// <summary>The inner line's thickness, logical px.</summary>
    [Export]
    public int LineWidth { get; set; } = 1;

    /// <summary>Logical px from the box's edge to the inner line.</summary>
    [Export]
    public float Gap { get; set; } = 4f;

    public override void _Draw(Rid toCanvasItem, Rect2 rect)
    {
        Box?.Draw(toCanvasItem, rect);
        using var line = new StyleBoxFlat { DrawCenter = false, BorderColor = Line };
        line.SetBorderWidthAll(LineWidth);
        line.Draw(toCanvasItem, rect.Grow(-Gap));
    }
}

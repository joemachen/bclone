using Godot;

namespace Bclone.Game;

/// <summary>
/// A painted 9-patch drawn at a fraction of its master's pixels (<c>ui-assets.md §1.5</c>, <c>ui-art.md §9.3</c> step 3):
/// the scroll chrome is painted at 2 × (controls at 4 ×) so it stays sharp up to a 4K window, and Godot's own
/// <see cref="StyleBoxTexture"/> draws a texture's pixels 1 : 1 — its corners would land twice their size.
/// </summary>
/// <remarks>
/// <para>
/// <b>How:</b> the patch is laid out at master size over a rectangle <c>1 / Scale</c> times the control's, under a
/// transform that scales it back down, so the corners keep their master proportions and the edges and centre stretch.
/// Its margins are in master pixels, as the spec's tables give them. Drawn through the texture's mipmaps, so a corner
/// read at a tenth of its master size is filtered, not sampled.
/// </para>
/// <para>
/// ⚠️ <b>Its minimum size is its content margins</b> (a <see cref="StyleBox"/> has no other), so a box squeezed below
/// its painted corners draws them overlapping; the windows and bars it skins are never that small.
/// </para>
/// </remarks>
[GlobalClass]
public partial class PaintedPatch : StyleBox
{
    /// <summary>The painted master.</summary>
    [Export]
    public Texture2D? Texture { get; set; }

    /// <summary>Logical pixels per master pixel: 0.5 for the 2 × chrome, 0.25 for the 4 × controls.</summary>
    [Export]
    public float Scale { get; set; } = 0.5f;

    /// <summary>The fixed corners, master pixels: left, top, right, bottom.</summary>
    [Export]
    public Vector4 Margins { get; set; }

    /// <summary>A tint over the painting (a state's, or a hover's warmth).</summary>
    [Export]
    public Color Modulate { get; set; } = Colors.White;

    /// <summary>
    /// Above zero, the painting is drawn only in a band this many logical px tall along the box's foot — a held button's
    /// rubric swash under its label, not stretched over the whole button.
    /// </summary>
    [Export]
    public float Band { get; set; }

    /// <summary>
    /// Logical px the painting reaches past the box on each side (left, top, right, bottom) — paper outside the control's
    /// own rect, so a box whose room cannot grow (the top bar's, A116) still has clear paper past its outermost words.
    /// </summary>
    [Export]
    public Vector4 Outset { get; set; }

    /// <summary>
    /// A patch of this master at this scale; <paramref name="content"/> is the room inside it, logical px (left, top,
    /// right, bottom) — chosen by what is painted where, so text can sit inside a plain corner but never on a roll.
    /// </summary>
    internal static PaintedPatch Make(Texture2D texture, float scale, Vector4 margins, Vector4 content)
    {
        var patch = new PaintedPatch { Texture = texture, Scale = scale, Margins = margins };
        patch.ContentMarginLeft = content.X;
        patch.ContentMarginTop = content.Y;
        patch.ContentMarginRight = content.Z;
        patch.ContentMarginBottom = content.W;
        return patch;
    }

    public override void _Draw(Rid toCanvasItem, Rect2 rect)
    {
        if (Texture is null || Scale <= 0f)
        {
            return;
        }

        if (Band > 0f)
        {
            rect = new Rect2(rect.Position.X, rect.End.Y - Band, rect.Size.X, Band);
        }

        rect = rect.GrowIndividual(Outset.X, Outset.Y, Outset.Z, Outset.W);

        RenderingServer.CanvasItemSetDefaultTextureFilter(toCanvasItem, RenderingServer.CanvasItemTextureFilter.LinearWithMipmaps);
        RenderingServer.CanvasItemAddSetTransform(toCanvasItem, new Transform2D(0f, new Vector2(Scale, Scale), 0f, rect.Position));
        RenderingServer.CanvasItemAddNinePatch(
            toCanvasItem,
            new Rect2(Vector2.Zero, rect.Size / Scale),
            new Rect2(Vector2.Zero, Texture.GetSize()),
            Texture.GetRid(),
            new Vector2(Margins.X, Margins.Y),
            new Vector2(Margins.Z, Margins.W),
            RenderingServer.NinePatchAxisMode.Stretch,
            RenderingServer.NinePatchAxisMode.Stretch,
            true,
            Modulate);
        RenderingServer.CanvasItemAddSetTransform(toCanvasItem, Transform2D.Identity);
    }
}

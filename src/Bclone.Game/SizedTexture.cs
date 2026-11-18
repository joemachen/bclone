using Godot;

namespace Bclone.Game;

/// <summary>
/// A painted master that reports, and draws at, a logical size (<c>ui-art.md §9.3</c> step 3): a theme's icons — a
/// checkbox, a drop-down's arrow — are drawn at their texture's own size, and the controls are painted at 4 ×, so a
/// 56 px checkbox would land 56 px wide. This one says 14 and draws the 56 into it, through the mipmaps.
/// </summary>
[GlobalClass]
public partial class SizedTexture : Texture2D
{
    /// <summary>The painted master.</summary>
    [Export]
    public Texture2D? Source { get; set; }

    /// <summary>The logical size it reports and draws at.</summary>
    [Export]
    public Vector2 Logical { get; set; }

    internal static SizedTexture Of(Texture2D source, float width, float height) =>
        new() { Source = source, Logical = new Vector2(width, height) };

    public override int _GetWidth() => (int)Logical.X;

    public override int _GetHeight() => (int)Logical.Y;

    public override bool _HasAlpha() => true;

    public override bool _IsPixelOpaque(int x, int y) => true;

    public override void _Draw(Rid toCanvasItem, Vector2 pos, Color modulate, bool transpose) =>
        _DrawRect(toCanvasItem, new Rect2(pos, Logical), false, modulate, transpose);

    public override void _DrawRect(Rid toCanvasItem, Rect2 rect, bool tile, Color modulate, bool transpose)
    {
        if (Source is null)
        {
            return;
        }

        RenderingServer.CanvasItemSetDefaultTextureFilter(toCanvasItem, RenderingServer.CanvasItemTextureFilter.LinearWithMipmaps);
        Source.DrawRect(toCanvasItem, rect, false, modulate, transpose);
    }

    public override void _DrawRectRegion(Rid toCanvasItem, Rect2 rect, Rect2 srcRect, Color modulate, bool transpose, bool clipUv)
    {
        if (Source is null || Logical.X <= 0f || Logical.Y <= 0f)
        {
            return;
        }

        var scale = new Vector2(Source.GetWidth() / Logical.X, Source.GetHeight() / Logical.Y);
        RenderingServer.CanvasItemSetDefaultTextureFilter(toCanvasItem, RenderingServer.CanvasItemTextureFilter.LinearWithMipmaps);
        Source.DrawRectRegion(toCanvasItem, rect, new Rect2(srcRect.Position * scale, srcRect.Size * scale), modulate, transpose, clipUv);
    }
}

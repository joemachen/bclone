using System.Text;
using Godot;

namespace Bclone.Game;

/// <summary>
/// The painted icons (<c>ui-assets.md §4</c>, <c>ui-art.md §9.3</c> step 2, A111): <b>found by the thing's own name</b> —
/// a building's, a good's or a trade's row name in the data, a tool's or a moment's mark — under
/// <c>textures/ui/icons/&lt;set&gt;/</c>, and <b>when there is no file, the code glyph that stood there before</b>.
/// </summary>
/// <remarks>
/// <para>
/// ⭐ <b>A name, never a list.</b> <i>"woodcutter's hut"</i> is <c>woodcutters-hut.png</c>, <i>"stone tools"</i> is
/// <c>stone-tools.png</c>, <c>HarvestTrees</c> is <c>harvest-trees.png</c> (<see cref="Slug"/>). A modder's new building
/// needs only a file of its name beside the others; without one it keeps the plain glyph, as today. That is why
/// <see cref="BuildingGlyph"/>, <see cref="TradeGlyph"/>, <see cref="ToolGlyph"/> and <see cref="MomentGlyph"/> stay:
/// they are the fallback, not the look.
/// </para>
/// <para>
/// A state is a second file, <c>&lt;name&gt;-&lt;state&gt;.png</c> — the buildings' <c>-owed</c> (A106). Loaded once a
/// path and kept, the misses too, so a refresh asks a dictionary, not the disk.
/// </para>
/// </remarks>
internal static class UiIcons
{
    /// <summary>An icon's side, logical px (A101: 20).</summary>
    internal const float Side = 20f;

    /// <summary>
    /// A good's icon in the top bar: the chip's own 10 (A111). ⚠️ The bar had 6 px to spare at 75 % (probe <c>bars:</c>);
    /// at 20 it ran 31 px under the right column. Larger there needs room the bar does not have — Joe's call.
    /// </summary>
    internal const float BarSide = 10f;

    /// <summary>A moment's mark, beside body text (<see cref="MomentGlyph.Side"/>).</summary>
    internal const float MomentSide = MomentGlyph.Side;

    internal const string Root = "res://textures/ui/icons/";

    private static readonly Dictionary<string, Texture2D?> Loaded = new();

    /// <summary>A name as a file name: lower case, apostrophes dropped, words joined by hyphens, a capital starting a word.</summary>
    internal static string Slug(string name)
    {
        var slug = new StringBuilder(name.Length + 4);
        for (int i = 0; i < name.Length; i++)
        {
            char c = name[i];
            if (c is '\'' or '’')
            {
                continue;
            }

            bool breaks = char.IsWhiteSpace(c) || c is '_' or '-';
            bool startsAWord = char.IsUpper(c) && i > 0 && char.IsLower(name[i - 1]);
            if ((breaks || startsAWord) && slug.Length > 0 && slug[^1] != '-')
            {
                slug.Append('-');
            }

            if (!breaks)
            {
                slug.Append(char.ToLowerInvariant(c));
            }
        }

        return slug.ToString().TrimEnd('-');
    }

    /// <summary>Where a thing's icon would be.</summary>
    internal static string PathOf(string set, string name, string? state = null) =>
        $"{Root}{set}/{Slug(name)}{(state is null ? string.Empty : "-" + state)}.png";

    /// <summary>The painted icon for this thing in this set, or null when there is none.</summary>
    internal static Texture2D? Find(string set, string name, string? state = null)
    {
        string path = PathOf(set, name, state);
        if (!Loaded.TryGetValue(path, out Texture2D? texture))
        {
            texture = ResourceLoader.Exists(path) ? GD.Load<Texture2D>(path) : null;
            Loaded[path] = texture;
        }

        return texture;
    }

    /// <summary>The painted icon if there is one — with its owed state when the set has them — else the glyph that stood there.</summary>
    internal static Control Or(string set, string name, System.Func<Control> glyph, float side = Side) =>
        Find(set, name) is { } texture ? new UiIcon(texture, Find(set, name, "owed"), side, name) : glyph();

    /// <summary>An empty icon's place, for a line whose thing changes — a person's trade; <see cref="Show"/> fills it.</summary>
    internal static TextureRect Slot(float side = Side) => new()
    {
        ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
        StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
        TextureFilter = CanvasItem.TextureFilterEnum.LinearWithMipmaps,
        CustomMinimumSize = new Vector2(side, side),
        SizeFlagsVertical = Control.SizeFlags.ShrinkBegin,
        MouseFilter = Control.MouseFilterEnum.Ignore,
        Visible = false,
    };

    /// <summary>Put this thing's icon in the slot, or hide the slot when it has none.</summary>
    internal static void Show(TextureRect slot, string set, string? name)
    {
        Texture2D? texture = name is null ? null : Find(set, name);
        slot.Texture = texture;
        slot.Visible = texture is not null;
    }

    /// <summary>Show the owed state on every painted icon this control holds (a strip button's mark). A glyph has none.</summary>
    internal static void MarkOwed(Node holder, bool owed)
    {
        foreach (Node child in holder.GetChildren())
        {
            if (child is UiIcon icon)
            {
                icon.Owed = owed;
            }
        }
    }
}

/// <summary>One painted icon at its logical size, downscaled through its mipmaps; it swaps to its owed file when told.</summary>
public sealed partial class UiIcon : TextureRect
{
    private readonly Texture2D _normal;
    private readonly Texture2D? _owed;

    public UiIcon(Texture2D normal, Texture2D? owed, float side, string of)
    {
        Of = of;
        _normal = normal;
        _owed = owed;
        Texture = normal;
        ExpandMode = ExpandModeEnum.IgnoreSize;
        StretchMode = StretchModeEnum.KeepAspectCentered;
        TextureFilter = TextureFilterEnum.LinearWithMipmaps;
        CustomMinimumSize = new Vector2(side, side);
        SizeFlagsVertical = SizeFlags.ShrinkCenter;
        SizeFlagsHorizontal = SizeFlags.ShrinkCenter;
        MouseFilter = MouseFilterEnum.Ignore;
    }

    private Button? _on;

    /// <summary>The file it shows: the owed one while a gift waits, when the set painted one.</summary>
    public bool Owed
    {
        get => _owed is not null && ReferenceEquals(Texture, _owed);
        set
        {
            Texture = value && _owed is not null ? _owed : _normal;
            if (_on is not null)
            {
                _on.Icon = Texture;
            }
        }
    }

    /// <summary>
    /// Be this button's own icon, centred above its label, at <see cref="UiIcons.Side"/> — the button lays the two out
    /// and sizes itself to hold both. Kept as a hidden child so <see cref="UiIcons.MarkOwed"/> finds it.
    /// </summary>
    internal void ShowOn(Button button)
    {
        _on = button;
        Visible = false;
        button.AddChild(this);
        button.Icon = Texture;
        button.IconAlignment = HorizontalAlignment.Center;
        button.VerticalIconAlignment = VerticalAlignment.Top;
        button.ExpandIcon = false;
        button.TextureFilter = TextureFilterEnum.LinearWithMipmaps;
        button.AddThemeConstantOverride("icon_max_width", (int)CustomMinimumSize.X);
    }

    /// <summary>The name it was found by — for the probe.</summary>
    internal string Of { get; }
}

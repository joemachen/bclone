using Godot;

namespace Bclone.Game;

/// <summary>
/// ⭐ <b>Which band of the deposit atlas each kind of deposit draws from</b> (A60, <c>specs/painted-deposits.md §4</c>, Joe:
/// <i>"with knowledge that we'll create more node types later"</i>) — by the terrain's name, so a new kind is a band
/// painted into <c>textures/nodes/node_sprites.png</c> and one line here, not code. A band is two rows of eight 128-px
/// cells: eight natural drawings, then eight worked. A kind with no line keeps its inked discs.
/// </summary>
[GlobalClass]
public sealed partial class NodeSpriteBands : Resource
{
    internal const string Path = "res://looks/node_sprites.tres";

    /// <summary>A terrain's name (<c>Rock</c>, <c>IronDeposit</c>, …) to its band, counted from the atlas's top.</summary>
    [Export]
    public Godot.Collections.Dictionary Bands { get; set; } = new();

    /// <summary>The bands as they stand in <see cref="Path"/>, or null where the file is missing.</summary>
    internal static NodeSpriteBands? Current => ResourceLoader.Exists(Path) ? GD.Load<NodeSpriteBands>(Path) : null;
}

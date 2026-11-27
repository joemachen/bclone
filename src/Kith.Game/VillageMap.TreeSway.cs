using Godot;

namespace Kith.Game;

/// <remarks>
/// ⭐⭐ THE TREES IN A GENTLE WIND (A299, <c>specs/tree-sway.md</c>; Joe's play note (i): <i>"a gentle wind animation to trees that is a
/// degree or two more intense in the winter. think of how the water animates and the visuals used there"</i>; the art queue's piece 5,
/// at his <i>"just power through all these"</i>). The map's own material (<c>tree_sway.gdshader</c>) leans the painted trees' top
/// corners — marked in their UVs by <see cref="MeshBuilder.SwayMark"/> — on a slow sway, each tree on its own phase; nothing else on
/// the map moves. On the people's clock: the game's speed, capped, still while paused, as the river's flow is. A degree or two more in
/// winter: <see cref="WinterSway"/> times the lean, crossfaded with the season as the atlases are. A uniform a frame, never a re-mesh.
/// </remarks>
public partial class VillageMap
{
    /// <summary>How far a crown's top leans at most, in tiles, in leaf.</summary>
    internal const float SwayTiles = 0.035f;

    /// <summary>Winter's lean over leaf's: the bare boughs in a harder wind (his "a degree or two more").</summary>
    internal const float WinterSway = 1.3f;

    private ShaderMaterial? _swayMaterial;

    /// <summary>Pure: the lean for a season's crossfade — leaf's, winter's, or between as the atlases are.</summary>
    internal static float SwayFor(TreeSet from, float fromAlpha, TreeSet to, float toAlpha)
    {
        float Of(TreeSet set) => set == TreeSet.Winter ? SwayTiles * WinterSway : SwayTiles;
        float total = fromAlpha + toAlpha;
        return total <= 0f ? Of(from) : ((Of(from) * fromAlpha) + (Of(to) * toAlpha)) / total;
    }

    /// <summary>The map's material made once; its clock and its lean set a frame, as the trees are drawn.</summary>
    private void SwayTheTrees(TreeSet from, float fromAlpha, TreeSet to, float toAlpha)
    {
        if (_swayMaterial is null)
        {
            _swayMaterial = new ShaderMaterial { Shader = GD.Load<Shader>("res://shaders/tree_sway.gdshader") };
            Material = _swayMaterial;
        }

        _swayMaterial.SetShaderParameter("sway_seconds", (float)(_peopleClock % 3600.0));
        _swayMaterial.SetShaderParameter("sway_tiles", SwayFor(from, fromAlpha, to, toAlpha));
    }
}

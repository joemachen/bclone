using Godot;

namespace Kith.Game;

public partial class VillageMap
{
    /// <summary>
    /// ⭐ The probe's <c>tree sway:</c> line (A299, <c>tree-sway.md</c>) — printed with the people's lines. The spec's numbers: a lean of
    /// 0.035 tiles in leaf, 1.3 times that in winter; the mark fract(UV.x × 24) = 0.001 on a tree's top corners only.
    /// </summary>
    /// <remarks>⚠️ Nothing is drawn headless (D340): the trees moving are the renders'.</remarks>
    private string TheTreeSwayHolds()
    {
        try
        {
            var faults = new List<string>();

            // ⭐ A tree's top corners carry the mark, its foot's do not — grown and young, mirrored and not.
            foreach (int cell in new[] { 0, 5, MeshBuilder.GrownSprites, MeshBuilder.GrownSprites + 3 })
            {
                foreach (bool mirrored in new[] { false, true })
                {
                    var tree = new MeshBuilder();
                    tree.Sprite(new Vector2(4f, 4f), 1f, cell, mirrored);
                    bool[] want = { true, true, false, true, false, false };   // tl, tr, br, tl, br, bl
                    for (int i = 0; i < 6; i++)
                    {
                        if (MeshBuilder.Sways(tree.UvAt(i)) != want[i])
                        {
                            faults.Add($"cell {cell}{(mirrored ? " mirrored" : "")}: corner {i} sways {!want[i]} (want {want[i]})");
                            break;
                        }
                    }
                }
            }

            // A deposit's cell, a plain texture's corners and an icon's region: none marked.
            var deposit = new MeshBuilder();
            deposit.SpriteFrom(new Vector2(2f, 2f), 0.5f, 0.25f, 0.5f, 0.25f, 0.25f, false);
            bool depositSways = Enumerable.Range(0, 6).Any(i => MeshBuilder.Sways(deposit.UvAt(i)));
            bool plainSways = new[] { Vector2.Zero, Vector2.One, new Vector2(0.5f, 0.25f), new Vector2(37f / 512f, 0f) }.Any(MeshBuilder.Sways);
            if (depositSways || plainSways)
            {
                faults.Add($"a deposit sways {depositSways}, a plain texture's corners sway {plainSways} (want neither)");
            }

            // A degree or two more in winter, crossfaded with the season.
            float leaf = SwayFor(TreeSet.Summer, 1f, TreeSet.Autumn, 0f), bare = SwayFor(TreeSet.Winter, 1f, TreeSet.Summer, 0f),
                turning = SwayFor(TreeSet.Autumn, 0.5f, TreeSet.Winter, 0.5f);
            if (Mathf.Abs(leaf - 0.035f) > 1e-5f || Mathf.Abs(bare - (0.035f * 1.3f)) > 1e-5f || !(turning > leaf && turning < bare))
            {
                faults.Add($"the lean {leaf:F4} in leaf, {bare:F4} in winter, {turning:F4} as winter comes (want 0.035, 0.0455, between)");
            }

            // The shader declares what the map sets.
            Shader shader = GD.Load<Shader>("res://shaders/tree_sway.gdshader");
            var names = shader.GetShaderUniformList().Select(u => u.AsGodotDictionary()["name"].AsString()).ToHashSet();
            if (!names.Contains("sway_seconds") || !names.Contains("sway_tiles"))
            {
                faults.Add($"tree_sway.gdshader declares {string.Join(", ", names)} (want sway_seconds and sway_tiles)");
            }

            return faults.Count == 0
                ? "[widths] tree sway: ✅ a tree's top corners carry the wind's mark and its foot's do not, grown and young, mirrored and not; a deposit and a plain texture never; the lean 0.035 tiles in leaf, 1.3 times in winter, between as it comes; the shader declares its clock and its lean"
                : $"[widths] tree sway: ⛔ {string.Join("; ", faults)}";
        }
        catch (System.Exception e)
        {
            return $"[widths] tree sway: ⛔ threw {e.GetType().Name}: {e.Message}";
        }
    }
}

using Godot;

namespace Kith.Game;

public partial class VillageMap
{
    /// <summary>
    /// ⭐ The probe's <c>animals:</c> line (A298, <c>bushes-and-animals.md</c>) — printed with the people's lines. Every number is the
    /// spec's (grazing under 0.009 tiles a tick along its path, a sixth of a roaming season or so; a bush of three leafy clumps and six berries), never read back from the
    /// code's constants.
    /// </summary>
    /// <remarks>⚠️ Nothing is drawn headless (D340): the cow and the deer themselves are the renders'.</remarks>
    private string TheAnimalsHold()
    {
        try
        {
            var faults = new List<string>();

            // Facing the way the path goes; a standstill keeps the seed's side.
            if (FacingOf(new Vector2(3f, 3f), new Vector2(3.2f, 3f), 7) != 1f || FacingOf(new Vector2(3f, 3f), new Vector2(2.9f, 3.1f), 7) != -1f
                || FacingOf(new Vector2(3f, 3f), new Vector2(3f, 3.2f), 6) != 1f || FacingOf(new Vector2(3f, 3f), new Vector2(3f, 3.2f), 7) != -1f)
            {
                faults.Add("facing: right, left, and a standstill by its seed (want +1, −1, +1, −1)");
            }

            if (!Grazes(0.004f) || Grazes(0.01f))
            {
                faults.Add("grazing at 0.004 tiles a tick and walking at 0.01 (want yes, no)");
            }

            // ⭐ The woods' own roam (unchanged, the systems lane's): some of the time grazing, most of it walking — sampled a season.
            int grazing = 0, n = 0;
            var home = new Vector2(0f, 0f);
            for (double s = 0; s < 2000; s += 5)
            {
                Vector2 now = home + new Vector2((float)Math.Cos((s * 0.012) + 1.7) * 1.4f, (float)Math.Sin(((s * 0.012) + 1.7) * 0.7) * 1.4f);
                Vector2 then = home + new Vector2((float)Math.Cos(((s + 1) * 0.012) + 1.7) * 1.4f, (float)Math.Sin((((s + 1) * 0.012) + 1.7) * 0.7) * 1.4f);
                grazing += Grazes(now.DistanceTo(then)) ? 1 : 0;
                n++;
            }

            float share = grazing / (float)n;
            if (share is < 0.05f or > 0.6f)
            {
                faults.Add($"a roaming deer grazes {share:P0} of a season (want some, not most)");
            }

            // ⭐ A berry patch is a bush: three leafy clumps with their shade and six berries (A86's was three berries).
            var bush = new MeshBuilder();
            BerryPatch(bush, new Vector2(5f, 5f), 12345u, 0.05f, Colors.Red);
            var three = new MeshBuilder();
            three.Disc(new Vector2(5f, 5f), 0.05f, Colors.Red);
            int perDisc = three.VertexCount;
            if (perDisc == 0 || bush.VertexCount != perDisc * 12)
            {
                faults.Add($"a berry patch meshes {bush.VertexCount} vertices (want 12 discs of {perDisc}: three clumps, their shade, six berries)");
            }

            return faults.Count == 0
                ? $"[widths] animals: ✅ a cow and a deer face the way their path goes, graze head down while slow ({share:P0} of a roaming deer's season), walk otherwise; a berry patch a bush of three clumps and six berries"
                : $"[widths] animals: ⛔ {string.Join("; ", faults)}";
        }
        catch (System.Exception e)
        {
            return $"[widths] animals: ⛔ threw {e.GetType().Name}: {e.Message}";
        }
    }
}

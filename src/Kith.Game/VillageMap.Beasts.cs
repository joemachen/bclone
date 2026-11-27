using Godot;

namespace Kith.Game;

/// <remarks>
/// ⭐⭐ THE ANIMALS DRAWN AS ANIMALS (A298, <c>specs/bushes-and-animals.md</c>; the art queue's piece 4, Joe's play note (g): <i>"berry
/// bushes / gathering resources and animals for hunting, animated"</i>; his <i>"just power through all these"</i> — the recommended
/// option taken). The barn's cattle were cream discs and the woods' game brown ones. With the watercolour tick on, each is a small
/// animal standing side-on, as the people do: a cow cream and brown, a deer tawny with a white rump (a hart's antlers on one in three);
/// facing the way its path goes, stepping while it moves, its head down to graze while it is slow. ⛔ <b>Where each one is, and how it
/// wanders, is untouched</b> — the herd's spots and the woods' roam are the systems lane's (D366, livestock); the view asks the same
/// path a moment on for which way it faces and how fast it goes. With the tick off, the discs, to the byte.
/// </remarks>
public partial class VillageMap
{
    /// <summary>Under this many tiles a second along its path an animal grazes, head down; over it, it walks.</summary>
    internal const float GrazeBelow = 0.009f;

    /// <summary>Pure: the way an animal faces, from where its path is now and a moment on: −1 left, +1 right; a standstill keeps
    /// the side its seed gives it.</summary>
    internal static float FacingOf(Vector2 now, Vector2 next, uint seed) =>
        Mathf.Abs(next.X - now.X) > 1e-5f ? Mathf.Sign(next.X - now.X) : ((seed & 1) == 0 ? 1f : -1f);

    /// <summary>Pure: whether an animal going <paramref name="tilesPerUnit"/> along its path is grazing.</summary>
    internal static bool Grazes(float tilesPerUnit) => tilesPerUnit < GrazeBelow;

    private static readonly Color CowHide = new("#e6dcc4"), CowPatch = new("#7a5034"), DeerHide = new("#a8743c"),
        DeerRump = new("#f0e8d8"), BeastInk = new("#2a2018"), Antler = new("#d8c8a8");

    /// <summary>
    /// One animal at <paramref name="feet"/> (screen), side-on: <paramref name="facing"/> −1 or +1, <paramref name="grazing"/> head
    /// down, <paramref name="step"/> the legs' phase while it walks.
    /// </summary>
    private void DrawTheBeast(Vector2 feet, bool deer, float facing, bool grazing, float step, uint seed)
    {
        float u = _pixelsPerTile;
        float len = (deer ? 0.46f : 0.52f) * u;
        float legH = (deer ? 0.16f : 0.12f) * u;
        float rx = len * (deer ? 0.28f : 0.32f), ry = len * (deer ? 0.12f : 0.16f);
        var body = feet + new Vector2(0f, -legH - ry);
        Color hide = AtNight(deer ? DeerHide : CowHide), ink = AtNight(BeastInk with { A = 0.75f });
        float line = Mathf.Max(1f, u * 0.025f);

        // The shadow on the ground.
        DrawColoredPolygon(Ellipse(feet + new Vector2(0f, u * 0.01f), rx * 1.05f, u * 0.035f, 12), AtNight(new Color(0.16f, 0.12f, 0.08f, 0.22f)));

        // The legs: the near and far pair swing against each other while it walks.
        float swing = grazing ? 0f : Mathf.Sin(step * Mathf.Tau) * 0.035f * u;
        foreach ((float x, float s) in new[] { (-0.62f, 1f), (-0.42f, -1f), (0.42f, 1f), (0.62f, -1f) })
        {
            Vector2 top = body + new Vector2(x * rx * facing, ry * 0.6f);
            DrawLine(top, top + new Vector2(swing * s * facing, legH + (ry * 0.4f)), ink, line * (deer ? 0.9f : 1.2f), true);
        }

        // The body, the cow's patches or the deer's white rump, the ink round it.
        Vector2[] torso = Ellipse(body, rx, ry, 16);
        DrawColoredPolygon(torso, hide);
        if (deer)
        {
            DrawColoredPolygon(Ellipse(body + new Vector2(-rx * 0.82f * facing, -ry * 0.1f), ry * 0.55f, ry * 0.6f, 10), AtNight(DeerRump));
        }
        else
        {
            DrawColoredPolygon(Ellipse(body + new Vector2((((seed >> 3) & 3) - 1.5f) * rx * 0.25f * facing, -ry * 0.15f), rx * 0.32f, ry * 0.55f, 10), AtNight(CowPatch));
        }

        DrawPolyline(torso.Append(torso[0]).ToArray(), ink, line, true);

        // The tail behind, the neck and head before: the head down at the grass while it grazes.
        Vector2 rump = body + new Vector2(-rx * facing, -ry * 0.2f);
        DrawLine(rump, rump + new Vector2(-0.04f * u * facing, ry * 1.1f), ink, line, true);
        Vector2 shoulder = body + new Vector2(rx * 0.8f * facing, -ry * 0.3f);
        Vector2 head = grazing
            ? feet + new Vector2((rx + (0.06f * u)) * facing, -0.03f * u)
            : shoulder + new Vector2(0.09f * u * facing, (deer ? -0.16f : -0.06f) * u);
        DrawLine(shoulder, head, hide, (deer ? 0.05f : 0.09f) * u, true);
        Vector2[] skull = Ellipse(head, (deer ? 0.055f : 0.07f) * u, (deer ? 0.035f : 0.05f) * u, 10);
        DrawColoredPolygon(skull, hide);
        DrawPolyline(skull.Append(skull[0]).ToArray(), ink, line, true);
        if (deer && !grazing && seed % 3 == 0)
        {
            // a hart: two branched antlers
            foreach (float side in new[] { -1f, 1f })
            {
                Vector2 b = head + new Vector2(-0.01f * u * facing + (side * 0.012f * u), -0.03f * u);
                Vector2 t = b + new Vector2(side * 0.03f * u, -0.09f * u);
                DrawLine(b, t, AtNight(Antler), line, true);
                DrawLine(b + ((t - b) * 0.5f), b + ((t - b) * 0.5f) + new Vector2(0.03f * u * facing, -0.02f * u), AtNight(Antler), line, true);
            }
        }
    }

    private static Vector2[] Ellipse(Vector2 c, float rx, float ry, int n)
    {
        var points = new Vector2[n];
        for (int i = 0; i < n; i++)
        {
            float a = Mathf.Tau * i / n;
            points[i] = c + new Vector2(rx * Mathf.Cos(a), ry * Mathf.Sin(a));
        }

        return points;
    }
}

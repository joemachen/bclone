using Godot;

namespace Bclone.Game;

/// <summary>
/// ⭐⭐ Discs and bands as triangles, in tile space — <b>so a thing the map draws thousands of
/// is one draw call, not thousands</b> (D366).
/// </summary>
/// <remarks>
/// <para>
/// <b>Joe: *"not sure whats happening with the FPS. its really dropping"* — 22 fps near in over a
/// dense path network.</b> D338 measured the reason once already: a <c>DrawCircle</c> becomes a
/// <c>CommandPolygon</c>, which breaks Godot's 2D batching, so every canopy, boulder, berry and
/// trail disc was its own command every frame — and D338 fixed it for the FAR view only, by baking
/// the foliage into <see cref="ValleyTexture"/>. The near view kept ~5,700 live circles, and
/// D359–D362 laid the trails on top the same way.
/// </para>
/// <para>
/// ⭐ <b>The shape:</b> everything that stands still is built ONCE into an <see cref="ArrayMesh"/>
/// with its vertices in <em>tile</em> coordinates and its colours per vertex, and drawn with a
/// tile→screen <see cref="Transform2D"/> — so zoom and pan are a matrix, never a rebuild, and the
/// mesh is rebuilt only on the generation counter of the state it draws (<c>TerrainGeneration</c>
/// for the scenery, <c>PathWear.Generation</c> for the trails). *`CLAUDE.md`'s rule: nothing
/// derivable incrementally is rebuilt per frame.*
/// </para>
/// <para>
/// ⚠️ A disc is a twelve-segment fan; at the largest zoom (48 px a tile) a canopy's radius is
/// under ten pixels, where twelve sides sag a third of a pixel from a circle. A band along a
/// curve is a strip with averaged normals at each joint — the bends the trails draw turn at most
/// ninety degrees over eight segments, where the miter error is under a percent.
/// </para>
/// </remarks>
internal sealed class MeshBuilder
{
    /// <summary>Sides on a disc. See the class remarks for why twelve is enough.</summary>
    private const int Segments = 12;

    private static readonly Vector2[] Unit = UnitCircle();

    private readonly List<Vector2> _vertices = new();
    private readonly List<Color> _colours = new();

    /// <summary>How many vertices have been laid down since the last <see cref="Clear"/>.</summary>
    public int VertexCount => _vertices.Count;

    public void Clear()
    {
        _vertices.Clear();
        _colours.Clear();
    }

    /// <summary>A filled disc as a fan of triangles.</summary>
    public void Disc(Vector2 centre, float radius, Color colour)
    {
        for (int i = 0; i < Segments; i++)
        {
            Add(centre, colour);
            Add(centre + (Unit[i] * radius), colour);
            Add(centre + (Unit[(i + 1) % Segments] * radius), colour);
        }
    }

    /// <summary>A straight band of the given half-width between two points — <c>DrawLine</c> with a width.</summary>
    public void Band(Vector2 from, Vector2 to, float halfWidth, Color colour)
    {
        Vector2 along = to - from;
        if (along.LengthSquared() < 1e-12f)
        {
            return;
        }

        Vector2 side = new Vector2(-along.Y, along.X).Normalized() * halfWidth;
        Quad(from + side, to + side, to - side, from - side, colour);
    }

    /// <summary>
    /// A band of the given half-width along a polyline — <c>DrawPolyline</c> with a width, as one
    /// strip whose joints share an averaged normal so consecutive quads neither gap nor overlap.
    /// </summary>
    public void Strip(ReadOnlySpan<Vector2> along, float halfWidth, Color colour)
    {
        if (along.Length < 2)
        {
            return;
        }

        Span<Vector2> side = along.Length <= 64 ? stackalloc Vector2[along.Length] : new Vector2[along.Length];
        Vector2 previous = Vector2.Zero;
        for (int i = 0; i < along.Length; i++)
        {
            Vector2 back = i > 0 ? along[i] - along[i - 1] : Vector2.Zero;
            Vector2 forward = i + 1 < along.Length ? along[i + 1] - along[i] : Vector2.Zero;
            Vector2 direction = back.Normalized() + forward.Normalized();

            // A joint between two coincident samples has no direction of its own; it borrows the
            // last one, so a curve that stalls for a sample does not pinch to nothing.
            Vector2 normal = direction.LengthSquared() > 1e-12f
                ? new Vector2(-direction.Y, direction.X).Normalized()
                : previous;
            previous = normal;
            side[i] = normal * halfWidth;
        }

        for (int i = 0; i + 1 < along.Length; i++)
        {
            Quad(along[i] + side[i], along[i + 1] + side[i + 1], along[i + 1] - side[i + 1], along[i] - side[i], colour);
        }
    }

    /// <summary>Add everything laid down so far as one triangle surface of <paramref name="mesh"/>, in 2D.</summary>
    /// <remarks>
    /// ⚠️ A <c>Vector2[]</c> vertex array is what makes the surface a 2D mesh — the canvas draws it
    /// with the item's transform and its vertex colours, no material needed.
    /// </remarks>
    public void AddSurfaceTo(ArrayMesh mesh)
    {
        if (_vertices.Count == 0)
        {
            return;
        }

        var arrays = new Godot.Collections.Array();
        arrays.Resize((int)Mesh.ArrayType.Max);
        arrays[(int)Mesh.ArrayType.Vertex] = _vertices.ToArray();
        arrays[(int)Mesh.ArrayType.Color] = _colours.ToArray();
        mesh.AddSurfaceFromArrays(Mesh.PrimitiveType.Triangles, arrays);
    }

    private void Quad(Vector2 a, Vector2 b, Vector2 c, Vector2 d, Color colour)
    {
        Add(a, colour);
        Add(b, colour);
        Add(c, colour);
        Add(a, colour);
        Add(c, colour);
        Add(d, colour);
    }

    private void Add(Vector2 at, Color colour)
    {
        _vertices.Add(at);
        _colours.Add(colour);
    }

    private static Vector2[] UnitCircle()
    {
        var unit = new Vector2[Segments];
        for (int i = 0; i < Segments; i++)
        {
            double angle = Math.Tau * i / Segments;
            unit[i] = new Vector2((float)Math.Cos(angle), (float)Math.Sin(angle));
        }

        return unit;
    }
}

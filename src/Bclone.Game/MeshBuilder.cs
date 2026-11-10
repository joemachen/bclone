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

    /// <summary>
    /// Texture coordinates — laid down only by <see cref="Sprite"/> (the painted trees, A35). A builder that never lays a
    /// sprite never writes them, so every untextured mesh is the mesh it was, to the byte.
    /// </summary>
    private readonly List<Vector2> _uvs = new();

    /// <summary>How many vertices have been laid down since the last <see cref="Clear"/>.</summary>
    public int VertexCount => _vertices.Count;

    public void Clear()
    {
        _vertices.Clear();
        _colours.Clear();
        _uvs.Clear();
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

    /// <summary>How many points round a lobed crown: enough for seven lobes to read as bumps, not corners.</summary>
    private const int CrownSegments = 32;

    private static readonly Vector2[] CrownUnit = UnitCircle(CrownSegments);

    /// <summary>
    /// ⭐ A tree's crown drawn as a cloud, not a coin (A28): a disc whose radius rises and falls round it, <paramref name="lobes"/>
    /// times, by <paramref name="depth"/> of the radius — the bumps of a canopy seen from above. <paramref name="turn"/> turns
    /// the bumps, so neighbouring crowns differ.
    /// </summary>
    public void Crown(Vector2 centre, float radius, Color colour, int lobes, float turn, float depth)
    {
        for (int i = 0; i < CrownSegments; i++)
        {
            int next = (i + 1) % CrownSegments;
            Add(centre, colour);
            Add(centre + (CrownUnit[i] * CrownRadius(i)), colour);
            Add(centre + (CrownUnit[next] * CrownRadius(next)), colour);
        }

        float CrownRadius(int at)
        {
            float angle = Mathf.Tau * at / CrownSegments;
            return radius * (1f - depth + (depth * Mathf.Abs(Mathf.Sin((angle * lobes * 0.5f) + turn))));
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

    /// <summary>A straight band that narrows from <paramref name="halfFrom"/> to <paramref name="halfTo"/> — a path petering out (D580).</summary>
    public void Taper(Vector2 from, Vector2 to, float halfFrom, float halfTo, Color colour)
    {
        Vector2 along = to - from;
        if (along.LengthSquared() < 1e-12f)
        {
            return;
        }

        Vector2 side = new Vector2(-along.Y, along.X).Normalized();
        Quad(from + (side * halfFrom), to + (side * halfTo), to - (side * halfTo), from - (side * halfFrom), colour);
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

    /// <summary>Triangles already cut — three vertices each — laid down as they are (a traced, filled patch).</summary>
    public void Triangles(ReadOnlySpan<Vector2> triangles, Color colour)
    {
        for (int i = 0; i + 2 < triangles.Length; i += 3)
        {
            Add(triangles[i], colour);
            Add(triangles[i + 1], colour);
            Add(triangles[i + 2], colour);
        }
    }

    /// <summary>
    /// ⭐ One cell of the tree atlas as a square in tile space (the painted trees, <c>tree-sprites.md §5</c>, A35):
    /// centred on <paramref name="centre"/>, <paramref name="half"/> tiles from the middle to each side, white so the
    /// texture is drawn as painted. <paramref name="mirrored"/> lays the cell mirrored across its up-left to down-right
    /// diagonal (a transpose) — the one turn that keeps the painted light where it is (A31).
    /// </summary>
    /// <remarks>⛔ A builder holds sprites or plain fills, never both: <see cref="AddSurfaceTo"/> refuses a mix.</remarks>
    public void Sprite(Vector2 centre, float half, int cell, bool mirrored)
    {
        const float Du = 1f / SpriteColumns, Dv = 1f / SpriteRows;
        float u0 = (cell % SpriteColumns) * Du, v0 = (cell / SpriteColumns) * Dv;
        Vector2 Uv(float s, float t) => mirrored ? new Vector2(u0 + (t * Du), v0 + (s * Dv)) : new Vector2(u0 + (s * Du), v0 + (t * Dv));

        Vector2 tl = centre + new Vector2(-half, -half), tr = centre + new Vector2(half, -half);
        Vector2 br = centre + new Vector2(half, half), bl = centre + new Vector2(-half, half);
        AddUv(tl, Uv(0f, 0f));
        AddUv(tr, Uv(1f, 0f));
        AddUv(br, Uv(1f, 1f));
        AddUv(tl, Uv(0f, 0f));
        AddUv(br, Uv(1f, 1f));
        AddUv(bl, Uv(0f, 1f));
    }

    /// <summary>
    /// The atlas's grid (<c>tree-sprites.md §3</c>): 4 cells across; 5 down since the three more kinds (A36) — two rows of
    /// leafy trees, then a row each of conifers, birches and willows.
    /// </summary>
    internal const int SpriteColumns = 4;

    internal const int SpriteRows = 5;

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
        if (_uvs.Count > 0)
        {
            if (_uvs.Count != _vertices.Count)
            {
                throw new InvalidOperationException($"a mesh of {_vertices.Count} vertices has {_uvs.Count} texture coordinates — sprites and plain fills were mixed in one builder");
            }

            arrays[(int)Mesh.ArrayType.TexUV] = _uvs.ToArray();
        }
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

    private void AddUv(Vector2 at, Vector2 uv)
    {
        Add(at, Colors.White);
        _uvs.Add(uv);
    }

    private static Vector2[] UnitCircle(int segments = Segments)
    {
        var unit = new Vector2[segments];
        for (int i = 0; i < segments; i++)
        {
            double angle = Math.Tau * i / segments;
            unit[i] = new Vector2((float)Math.Cos(angle), (float)Math.Sin(angle));
        }

        return unit;
    }
}

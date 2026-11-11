using Godot;

namespace Bclone.Game;

/// <summary>
/// ⭐ <b>The river's own coordinates</b> (<c>specs/river-drawn.md §3</c>, A44–A45): one row a step down its course —
/// where its middle is, how wide it is, how far it has run — so the shader can lay a mark <i>along</i> a river that
/// wanders, and bend it with the river.
/// </summary>
/// <remarks>
/// <para>
/// A carved river has one run of water per step down its course (<c>MapGenerator.CarveRiver</c>: a west–east river one
/// run a column, a diagonal one run a step of <c>x ± y</c>), so this is a short table and not a field. It is <b>a pure
/// function of the water tiles and the course</b>: the same valley always gives the same table, and the probe can feed it
/// made-up rivers. No <c>SetTerrain</c> makes or unmakes water, so it is built once a world and never in play.
/// ⚠️ <b>Not hashed, not saved</b> (D335): it restates the map.
/// </para>
/// <para>
/// <b>The coordinates</b> are the map's columns and rows with tile <c>i</c>'s centre at <c>i + 0.5</c> — the shader's
/// tile measured from the valley's corner. A step is a column (west–east), a row (north–south), or a diagonal of
/// <c>x + y</c> or <c>x − y</c>; across is measured on the unit vector square to the course, so a diagonal's widths and
/// middles are in tiles on the ground.
/// </para>
/// </remarks>
internal sealed class RiverTable
{
    /// <summary>How many steps either side a row's middle and width are averaged over (§3): a wander is a bend, not a stair.</summary>
    internal const int SmoothSteps = 4;

    private RiverTable(string course, int rows, Vector2 along, Vector2 across, float offset, float[] centre, float[] half, float[] down, float[] lean)
    {
        Course = course;
        Rows = rows;
        Along = along;
        Across = across;
        Offset = offset;
        Centre = centre;
        HalfWidth = half;
        Down = down;
        Lean = lean;
    }

    /// <summary>The course the table was built down: <c>we</c>, <c>ns</c>, <c>nwse</c> or <c>swne</c>.</summary>
    internal string Course { get; }

    /// <summary>One row a step down the course.</summary>
    internal int Rows { get; }

    /// <summary>
    /// What a position (tiles from the valley's corner) is dotted with to give its step down the course, before
    /// <see cref="Offset"/>: <c>(1, 0)</c>, <c>(0, 1)</c>, <c>(1, 1)</c> or <c>(1, −1)</c> — downstream is always the way it grows.
    /// </summary>
    internal Vector2 Along { get; }

    /// <summary>The unit vector square to the course that <i>across</i> is measured on, in tiles.</summary>
    internal Vector2 Across { get; }

    /// <summary>Added to <c>position · Along</c> so a tile's centre lands on its row's texel centre (row + 0.5).</summary>
    internal float Offset { get; }

    /// <summary>Each row's middle, across, in tiles — smoothed.</summary>
    internal float[] Centre { get; }

    /// <summary>Each row's half-width, in tiles on the ground — smoothed.</summary>
    internal float[] HalfWidth { get; }

    /// <summary>How far the river has run to each row along its smoothed middle, in tiles — only ever growing.</summary>
    internal float[] Down { get; }

    /// <summary>
    /// The cosine of each row's bend away from the course: across measured on the course's square is this much longer
    /// than across measured square to the river itself.
    /// </summary>
    internal float[] Lean { get; }

    /// <summary>
    /// The table for <paramref name="water"/> (row-major, <paramref name="width"/> × <paramref name="height"/>) cut down
    /// <paramref name="course"/> — or null where there is no water at all (a river 0 tiles wide).
    /// </summary>
    /// <exception cref="ArgumentException">A course that is not one of the four, or a map of the wrong size.</exception>
    internal static RiverTable? Of(IReadOnlyList<bool> water, int width, int height, string course)
    {
        ArgumentNullException.ThrowIfNull(water);
        if (water.Count != width * height || width <= 0 || height <= 0)
        {
            throw new ArgumentException($"a river table needs {width} × {height} tiles, and was given {water.Count}");
        }

        float root2 = MathF.Sqrt(2f);
        (Vector2 along, Vector2 across, int rows, float step, float spacing) = course switch
        {
            "we" => (new Vector2(1f, 0f), new Vector2(0f, 1f), width, 1f, 1f),
            "ns" => (new Vector2(0f, 1f), new Vector2(1f, 0f), height, 1f, 1f),
            "nwse" => (new Vector2(1f, 1f), new Vector2(1f, -1f) / root2, width + height - 1, 1f / root2, root2),
            "swne" => (new Vector2(1f, -1f), new Vector2(1f, 1f) / root2, width + height - 1, 1f / root2, root2),
            _ => throw new ArgumentException($"no river runs \"{course}\" (want we, ns, nwse or swne)"),
        };

        // A tile's row: its centre dotted with Along, less what lands a centre on (row + 0.5).
        // we, ns: i + 0.5 → offset 0. nwse: (i + 0.5) + (j + 0.5) = row + 1 → −0.5. swne: i − j, row = i − j + h − 1 → h − 0.5.
        float offset = course switch
        {
            "nwse" => -0.5f,
            "swne" => height - 0.5f,
            _ => 0f,
        };

        var sum = new double[rows];
        var count = new int[rows];
        for (int j = 0; j < height; j++)
        {
            for (int i = 0; i < width; i++)
            {
                if (!water[(j * width) + i])
                {
                    continue;
                }

                var centre = new Vector2(i + 0.5f, j + 0.5f);
                int row = (int)MathF.Floor(centre.Dot(along) + offset);
                sum[row] += centre.Dot(across);
                count[row]++;
            }
        }

        // Rows the river does not reach (a diagonal's corners) take the nearest row's that it does.
        int first = Array.FindIndex(count, c => c > 0);
        if (first < 0)
        {
            return null;
        }

        var rawCentre = new float[rows];
        var rawHalf = new float[rows];
        int last = first;
        for (int r = 0; r < rows; r++)
        {
            if (count[r] > 0)
            {
                rawCentre[r] = (float)(sum[r] / count[r]);
                rawHalf[r] = count[r] * spacing / 2f;
                last = r;
            }
            else if (r < first)
            {
                continue;
            }
            else
            {
                rawCentre[r] = rawCentre[last];
                rawHalf[r] = rawHalf[last];
            }
        }

        for (int r = 0; r < first; r++)
        {
            rawCentre[r] = rawCentre[first];
            rawHalf[r] = rawHalf[first];
        }

        float[] centres = Smoothed(rawCentre);
        float[] halves = Smoothed(rawHalf);
        var down = new float[rows];
        var lean = new float[rows];
        for (int r = 0; r < rows; r++)
        {
            if (r > 0)
            {
                float rise = centres[r] - centres[r - 1];
                down[r] = down[r - 1] + MathF.Sqrt((step * step) + (rise * rise));
            }

            float slope = (centres[Math.Min(rows - 1, r + 1)] - centres[Math.Max(0, r - 1)]) / (2f * step);
            lean[r] = 1f / MathF.Sqrt(1f + (slope * slope));
        }

        return new RiverTable(course, rows, along, across, offset, centres, halves, down, lean);
    }

    /// <summary>A box average over ±<see cref="SmoothSteps"/> rows, the ends held.</summary>
    private static float[] Smoothed(float[] raw)
    {
        var smoothed = new float[raw.Length];
        for (int r = 0; r < raw.Length; r++)
        {
            double total = 0;
            for (int o = -SmoothSteps; o <= SmoothSteps; o++)
            {
                total += raw[Math.Clamp(r + o, 0, raw.Length - 1)];
            }

            smoothed[r] = (float)(total / ((2 * SmoothSteps) + 1));
        }

        return smoothed;
    }

    /// <summary>
    /// The table as the shader reads it: one RGBA float texel a row — middle, half-width, distance down, lean — sampled
    /// linearly between rows.
    /// </summary>
    internal ImageTexture ToTexture()
    {
        var bytes = new byte[Rows * 16];
        for (int r = 0; r < Rows; r++)
        {
            BitConverter.TryWriteBytes(bytes.AsSpan((r * 16) + 0, 4), Centre[r]);
            BitConverter.TryWriteBytes(bytes.AsSpan((r * 16) + 4, 4), HalfWidth[r]);
            BitConverter.TryWriteBytes(bytes.AsSpan((r * 16) + 8, 4), Down[r]);
            BitConverter.TryWriteBytes(bytes.AsSpan((r * 16) + 12, 4), Lean[r]);
        }

        return ImageTexture.CreateFromImage(Image.CreateFromData(Rows, 1, false, Image.Format.Rgbaf, bytes));
    }
}

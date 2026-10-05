using System.Globalization;
using System.Text.Json.Nodes;
using Bclone.Sim.Core;
using Bclone.Sim.World;

namespace Bclone.Sim.Persistence;

/// <summary>
/// A save that cannot be read — and the place in it that said so (`specs/save-load.md §7`).
/// </summary>
/// <remarks>
/// <b>Thrown only while reading a save, and caught at one door</b> (<see cref="SaveFile.Open"/>), which
/// turns it into a sentence for the player and a line in the audit log. A load either builds the whole
/// world or throws this — there is no half-loaded village (§7).
/// </remarks>
public sealed class SaveFormatException : Exception
{
    public SaveFormatException(string message)
        : base(message)
    {
    }

    public SaveFormatException(string message, Exception inner)
        : base(message, inner)
    {
    }
}

/// <summary>
/// A sound save holding something today's data does not have — a good, a trade, a technique a modder
/// removed (`save-load.md §7`). Not damage: the file is fine and is not copied aside.
/// </summary>
public sealed class SaveDataException : Exception
{
    public SaveDataException(string what, string message)
        : base(message)
    {
        What = what;
    }

    /// <summary>What the save holds that the data lacks, in the player's words — <c>"good 12"</c>, <c>"“Smokehouse”"</c>.</summary>
    public string What { get; }
}

/// <summary>
/// One object of a save being read, with where it is — so a missing key says <em>which</em> one
/// (`villagers[3].hunger`), not merely that something was wrong.
/// </summary>
/// <remarks>
/// ⛔ <b>Every read is strict</b>: a key that is absent or the wrong type throws
/// <see cref="SaveFormatException"/>. A village restored from a guess is the half-load §7 refuses.
/// </remarks>
internal sealed class SaveReader
{
    private readonly JsonObject _object;

    public SaveReader(JsonObject json, string path, int goods)
    {
        _object = json;
        Path = path;
        GoodsCount = goods;
    }

    /// <summary>Where this object sits in the save, for the refusal's audit line.</summary>
    public string Path { get; }

    /// <summary>How many goods today's data has — a saved good past it is one the data no longer has.</summary>
    public int GoodsCount { get; }

    /// <summary>
    /// A good, by its id. ⚠️ Never by its enum name: a modder's good has an id and no name
    /// (<see cref="MaterialCost"/>), so the number is the only spelling every good has.
    /// </summary>
    public Goods Good(string key)
    {
        int id = Int(key);
        return id >= 0 && id < GoodsCount
            ? (Goods)id
            : throw new SaveDataException($"good {id}", $"{Path}.{key}: good {id}, which this game's data no longer has.");
    }

    public bool Has(string key) => _object.ContainsKey(key);

    /// <summary>Whether <paramref name="key"/> is here and is not <c>null</c>.</summary>
    public bool HasValue(string key) => _object.TryGetPropertyValue(key, out JsonNode? node) && node is not null;

    /// <summary>Every key of this object, in the order it was written.</summary>
    public IEnumerable<string> Keys => _object.Select(pair => pair.Key);

    public JsonNode Node(string key) =>
        _object.TryGetPropertyValue(key, out JsonNode? node) && node is not null
            ? node
            : throw new SaveFormatException($"{Path}.{key}: missing.");

    public int Int(string key) => Value<int>(key);

    public bool Bool(string key) => Value<bool>(key);

    public string String(string key) => Value<string>(key);

    public int? NullableInt(string key) =>
        _object.TryGetPropertyValue(key, out JsonNode? node) && node is not null ? Int(key) : null;

    public string? NullableString(string key) =>
        _object.TryGetPropertyValue(key, out JsonNode? node) && node is not null ? String(key) : null;

    public long Long(string key) => unchecked((long)ULong(key));

    public ulong ULong(string key)
    {
        string text = String(key);
        return ulong.TryParse(text, NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture, out ulong value)
            ? value
            : throw new SaveFormatException($"{Path}.{key}: \"{text}\" is not a 64-bit number in hex.");
    }

    public ulong? NullableULong(string key) =>
        _object.TryGetPropertyValue(key, out JsonNode? node) && node is not null ? ULong(key) : null;

    public Fixed Fixed(string key) => Core.Fixed.FromRawBits(Long(key));

    public Angle Angle(string key) => Core.Angle.FromRaw(checked((ushort)Int(key)));

    public Point Point(string key)
    {
        SaveReader at = Object(key);
        return new Point(at.Fixed("x"), at.Fixed("y"));
    }

    public Point? NullablePoint(string key) =>
        _object.TryGetPropertyValue(key, out JsonNode? node) && node is not null ? Point(key) : null;

    public GridPos GridPos(string key)
    {
        SaveReader at = Object(key);
        return new GridPos(at.Int("x"), at.Int("y"));
    }

    public GridPos? NullableGridPos(string key) =>
        _object.TryGetPropertyValue(key, out JsonNode? node) && node is not null ? GridPos(key) : null;

    public T Enum<T>(string key)
        where T : struct, System.Enum
    {
        string text = String(key);
        return System.Enum.TryParse(text, ignoreCase: false, out T value) && System.Enum.IsDefined(value)
            ? value
            : throw new SaveDataException($"“{text}”", $"{Path}.{key}: \"{text}\" is not a {typeof(T).Name} this build knows.");
    }

    public T? NullableEnum<T>(string key)
        where T : struct, System.Enum =>
        _object.TryGetPropertyValue(key, out JsonNode? node) && node is not null ? Enum<T>(key) : null;

    public SaveReader Object(string key) =>
        Node(key) is JsonObject json
            ? new SaveReader(json, $"{Path}.{key}", GoodsCount)
            : throw new SaveFormatException($"{Path}.{key}: not an object.");

    public SaveReader? NullableObject(string key) =>
        _object.TryGetPropertyValue(key, out JsonNode? node) && node is not null ? Object(key) : null;

    /// <summary>Each object of an array, in order, each knowing its own place.</summary>
    public IEnumerable<SaveReader> Objects(string key)
    {
        JsonArray array = Array(key);
        for (int i = 0; i < array.Count; i++)
        {
            yield return array[i] is JsonObject json
                ? new SaveReader(json, $"{Path}.{key}[{i}]", GoodsCount)
                : throw new SaveFormatException($"{Path}.{key}[{i}]: not an object.");
        }
    }

    public List<int> Ints(string key)
    {
        JsonArray array = Array(key);
        var list = new List<int>(array.Count);
        for (int i = 0; i < array.Count; i++)
        {
            list.Add(Element<int>(array[i], $"{key}[{i}]"));
        }

        return list;
    }

    /// <summary>A list of <c>[a, b]</c> pairs, in order.</summary>
    public List<(int, int)> Pairs(string key)
    {
        JsonArray array = Array(key);
        var list = new List<(int, int)>(array.Count);
        for (int i = 0; i < array.Count; i++)
        {
            if (array[i] is not JsonArray { Count: 2 } pair)
            {
                throw new SaveFormatException($"{Path}.{key}[{i}]: not a pair.");
            }

            list.Add((Element<int>(pair[0], $"{key}[{i}][0]"), Element<int>(pair[1], $"{key}[{i}][1]")));
        }

        return list;
    }

    public List<GridPos> GridPositions(string key)
    {
        List<int> flat = Ints(key);
        if (flat.Count % 2 != 0)
        {
            throw new SaveFormatException($"{Path}.{key}: an odd count of coordinates.");
        }

        var list = new List<GridPos>(flat.Count / 2);
        for (int i = 0; i < flat.Count; i += 2)
        {
            list.Add(new GridPos(flat[i], flat[i + 1]));
        }

        return list;
    }

    public byte[] Bytes(string key, int length)
    {
        byte[] bytes;
        try
        {
            bytes = Convert.FromBase64String(String(key));
        }
        catch (FormatException ex)
        {
            throw new SaveFormatException($"{Path}.{key}: not base64.", ex);
        }

        return bytes.Length == length
            ? bytes
            : throw new SaveFormatException($"{Path}.{key}: {bytes.Length} bytes where {length} were expected.");
    }

    /// <summary>A bool array packed eight to a byte (<see cref="SaveWriter.Bools"/>), read into <paramref name="into"/>.</summary>
    public void BoolsInto(string key, bool[] into)
    {
        byte[] bytes = Bytes(key, (into.Length + 7) / 8);
        for (int i = 0; i < into.Length; i++)
        {
            into[i] = (bytes[i >> 3] & (1 << (i & 7))) != 0;
        }
    }

    public void BytesInto(string key, byte[] into) => Bytes(key, into.Length).CopyTo(into, 0);

    public void IntsInto(string key, int[] into)
    {
        byte[] bytes = Bytes(key, into.Length * 4);
        for (int i = 0; i < into.Length; i++)
        {
            into[i] = System.Buffers.Binary.BinaryPrimitives.ReadInt32LittleEndian(bytes.AsSpan(i * 4));
        }
    }

    public void UShortsInto(string key, ushort[] into)
    {
        byte[] bytes = Bytes(key, into.Length * 2);
        for (int i = 0; i < into.Length; i++)
        {
            into[i] = System.Buffers.Binary.BinaryPrimitives.ReadUInt16LittleEndian(bytes.AsSpan(i * 2));
        }
    }

    public JsonArray Array(string key) =>
        Node(key) as JsonArray ?? throw new SaveFormatException($"{Path}.{key}: not an array.");

    private T Value<T>(string key) => Element<T>(Node(key), key);

    private T Element<T>(JsonNode? node, string key)
    {
        try
        {
            return node is JsonValue value && value.TryGetValue(out T? result) && result is not null
                ? result
                : throw new SaveFormatException($"{Path}.{key}: not a {typeof(T).Name}.");
        }
        catch (Exception ex) when (ex is FormatException or InvalidOperationException)
        {
            throw new SaveFormatException($"{Path}.{key}: not a {typeof(T).Name}.", ex);
        }
    }
}

/// <summary>
/// The other half of <see cref="SaveReader"/>: how each kind of value is spelled in a save
/// (`save-load.md §6`), in one place so the two cannot drift.
/// </summary>
/// <remarks>
/// ⛔ <b>No floats, and no JSON number wider than an <c>int</c></b>: most JSON readers hold a number as a
/// double, so a <c>long</c>, a <c>ulong</c> and a <see cref="Core.Fixed"/>'s raw bits go in as hex text.
/// Bulk arrays go in as base64 of their bytes, little-endian.
/// </remarks>
internal static class SaveWriter
{
    public static JsonNode Hex(ulong value) => JsonValue.Create(value.ToString("x16", CultureInfo.InvariantCulture));

    public static JsonNode Hex(long value) => Hex(unchecked((ulong)value));

    public static JsonNode? Hex(ulong? value) => value is ulong v ? Hex(v) : null;

    public static JsonNode Fixed(Fixed value) => Hex(value.RawBits);

    public static JsonNode Angle(Angle value) => JsonValue.Create((int)value.Raw);

    public static JsonObject Point(Point value) => new() { ["x"] = Fixed(value.X), ["y"] = Fixed(value.Y) };

    public static JsonObject? Point(Point? value) => value is Point p ? Point(p) : null;

    public static JsonObject GridPos(GridPos value) => new() { ["x"] = value.X, ["y"] = value.Y };

    public static JsonObject? GridPos(GridPos? value) => value is GridPos p ? GridPos(p) : null;

    /// <summary>A good, by its id — see <see cref="SaveReader.Good"/>.</summary>
    public static JsonNode Good(Goods value) => JsonValue.Create((int)value);

    public static JsonNode Enum<T>(T value)
        where T : struct, System.Enum => JsonValue.Create(value.ToString());

    public static JsonNode? Enum<T>(T? value)
        where T : struct, System.Enum => value is T v ? Enum(v) : null;

    public static JsonArray Ints(IEnumerable<int> values)
    {
        var array = new JsonArray();
        foreach (int value in values)
        {
            array.Add(value);
        }

        return array;
    }

    /// <summary>Positions as a flat <c>[x, y, x, y, …]</c> — a fence or a yard is dozens of them.</summary>
    public static JsonArray GridPositions(IEnumerable<GridPos> values)
    {
        var array = new JsonArray();
        foreach (GridPos value in values)
        {
            array.Add(value.X);
            array.Add(value.Y);
        }

        return array;
    }

    public static JsonNode Bytes(byte[] bytes) => JsonValue.Create(Convert.ToBase64String(bytes));

    public static JsonNode Bools(IReadOnlyList<bool> values)
    {
        var bytes = new byte[(values.Count + 7) / 8];
        for (int i = 0; i < values.Count; i++)
        {
            if (values[i])
            {
                bytes[i >> 3] |= (byte)(1 << (i & 7));
            }
        }

        return Bytes(bytes);
    }

    public static JsonNode PackedInts(IReadOnlyList<int> values)
    {
        var bytes = new byte[values.Count * 4];
        for (int i = 0; i < values.Count; i++)
        {
            System.Buffers.Binary.BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(i * 4), values[i]);
        }

        return Bytes(bytes);
    }

    public static JsonNode UShorts(IReadOnlyList<ushort> values)
    {
        var bytes = new byte[values.Count * 2];
        for (int i = 0; i < values.Count; i++)
        {
            System.Buffers.Binary.BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(i * 2), values[i]);
        }

        return Bytes(bytes);
    }
}

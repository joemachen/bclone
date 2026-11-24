using System.IO.Compression;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Kith.Sim.Config;
using Kith.Sim.Core;
using Kith.Sim.Logging;

namespace Kith.Sim.Persistence;

/// <summary>
/// What opening a save came to: a village, or a sentence for the player and a line for the audit log
/// (`specs/save-load.md §7`). Never both, and never a half-loaded village.
/// </summary>
/// <param name="Loop">The loaded village, or <c>null</c> when it was refused.</param>
/// <param name="Header">The save's header, when it could be read at all.</param>
/// <param name="Refusal">The player's sentence (§7's table), or <c>null</c> on success.</param>
/// <param name="Detail">What exactly was wrong — the audit log's, not the player's.</param>
public sealed record SaveOpened(SimLoop? Loop, SaveHeader? Header, string? Refusal, string? Detail)
{
    /// <summary>
    /// The save's <c>"view"</c> section, as it was written, or null — the view's own facts about how the player has looked
    /// at the village (the bar's <i>new</i> marks, `hud.md §8.3`, D880). ⛔ The sim never reads it and it is never hashed:
    /// what the player has looked at is not a fact about the village (D335's line). A save without one opens with none.
    /// </summary>
    public JsonObject? View { get; init; }
}

/// <summary>
/// A save on disk: gzip'd JSON, written whole or not at all, and read through one door that turns every
/// way it can go wrong into words (`specs/save-load.md §6–§7`).
/// </summary>
/// <remarks>
/// The pattern is <see cref="PlayerSettingsFile"/>'s: atomic through a <c>.tmp</c>, and a file that cannot
/// be read is copied to <c>.bad</c> before anything else can overwrite it.
/// </remarks>
public static class SaveFile
{
    /// <summary>What every save is called with.</summary>
    public const string Extension = ".save";

    /// <summary>What a damaged save is copied to, beside itself.</summary>
    public const string BrokenSuffix = ".bad";

    /// <summary>What a save is written to before it is moved over the real file.</summary>
    public const string PartSuffix = ".tmp";

    /// <summary>The autosaves, newest first: <c>autosave-1.save</c> … (§7).</summary>
    public const string AutosaveStem = "autosave-";

    private static readonly JsonSerializerOptions Written = new()
    {
        WriteIndented = true,
        TypeInfoResolver = JsonSerializerOptions.Default.TypeInfoResolver,
    };

    /// <summary>The document as the text a save holds — what the byte-for-byte guard compares (§9.3).</summary>
    public static string TextOf(JsonObject document)
    {
        ArgumentNullException.ThrowIfNull(document);
        return document.ToJsonString(Written);
    }

    /// <summary>Write <paramref name="document"/> to <paramref name="path"/> whole or not at all.</summary>
    /// <exception cref="IOException">The disk said no; the caller says so and carries on (§7).</exception>
    /// <exception cref="UnauthorizedAccessException">The folder is not ours to write; likewise.</exception>
    public static void Write(string path, JsonObject document)
    {
        ArgumentNullException.ThrowIfNull(path);
        ArgumentNullException.ThrowIfNull(document);

        string? folder = Path.GetDirectoryName(Path.GetFullPath(path));
        if (!string.IsNullOrEmpty(folder))
        {
            Directory.CreateDirectory(folder);
        }

        // ⭐ ATOMIC: a crash part way leaves the old save, never half of a new one.
        string part = path + PartSuffix;
        using (FileStream file = File.Create(part))
        using (var zipped = new GZipStream(file, CompressionLevel.Optimal))
        {
            byte[] text = Encoding.UTF8.GetBytes(TextOf(document));
            zipped.Write(text, 0, text.Length);
        }

        File.Move(part, path, overwrite: true);
    }

    /// <summary>
    /// An autosave into <paramref name="folder"/>: the newest becomes <c>autosave-1</c> and the older ones
    /// move down, the oldest past <paramref name="keep"/> dropped (Joe, D508: three).
    /// </summary>
    /// <remarks>
    /// ⚠️ The new save is written to its <c>.tmp</c> FIRST, then the old ones are moved down, then it is
    /// moved in — so a write the disk refuses leaves every older autosave where it was.
    /// </remarks>
    public static string WriteAutosave(string folder, JsonObject document, int keep = 3)
    {
        ArgumentNullException.ThrowIfNull(folder);
        ArgumentOutOfRangeException.ThrowIfLessThan(keep, 1);

        string newest = AutosavePath(folder, 1);
        string part = newest + ".new";
        Write(part, document);

        string oldest = AutosavePath(folder, keep);
        if (File.Exists(oldest))
        {
            File.Delete(oldest);
        }

        for (int n = keep - 1; n >= 1; n--)
        {
            string from = AutosavePath(folder, n);
            if (File.Exists(from))
            {
                File.Move(from, AutosavePath(folder, n + 1), overwrite: true);
            }
        }

        File.Move(part, newest, overwrite: true);
        return newest;
    }

    /// <summary>Where the <paramref name="n"/>th newest autosave in <paramref name="folder"/> lives.</summary>
    public static string AutosavePath(string folder, int n) => Path.Combine(folder, $"{AutosaveStem}{n}{Extension}");

    /// <summary>
    /// How much of a save, inflated, the load list reads for its header (D805): the header is written first and is a
    /// few hundred bytes, and every one of Joe's 439 saves reached <c>"world"</c> inside 16 KB.
    /// </summary>
    public const int HeadBytes = 4096;

    /// <summary>The header of the save at <paramref name="path"/>, for the load list — or the sentence saying why not.</summary>
    /// <remarks>
    /// ⭐ <b>THE HEAD OF THE FILE, NEVER THE VILLAGE</b> (D805, `title-and-pause.md §5.2`). Reading Joe's 439 saves
    /// whole to list them froze his pause screen for 830 ms; their heads take 40 ms. A head that does not make a
    /// header is read whole, as before, so every refusal is the one the list always gave. ⚠️ A save cut short
    /// <i>after</i> its header now lists, and <see cref="Open"/> refuses it when it is picked.
    /// </remarks>
    public static SaveOpened ReadHeader(string path)
    {
        ArgumentNullException.ThrowIfNull(path);
        if (TheHeaderFromTheHead(path) is SaveHeader fromTheHead)
        {
            return Listed(path, fromTheHead);
        }

        (JsonObject? document, SaveOpened? refused) = ReadDocument(path, keepBroken: false);
        if (document is null)
        {
            return refused!;
        }

        try
        {
            return Listed(path, SaveGame.HeaderOf(document));
        }
        catch (SaveFormatException ex)
        {
            return new SaveOpened(null, null, Damaged(null), $"{path}: {ex.Message}");
        }
    }

    private static SaveOpened Listed(string path, SaveHeader header) =>
        header.Format == SaveGame.Format
            ? new SaveOpened(null, header, null, null)
            : new SaveOpened(null, header, OtherFormat(header.Build), $"{path}: format {header.Format}, where this build reads {SaveGame.Format}.");

    /// <summary>
    /// The header made from the top-level members written before <c>"world"</c>, read from the first
    /// <see cref="HeadBytes"/> of the file inflated — or <c>null</c>, and the caller reads the whole file, when the head
    /// does not reach <c>"world"</c>, will not read, or does not make a header. Never throws for anything in the file.
    /// </summary>
    private static SaveHeader? TheHeaderFromTheHead(string path)
    {
        byte[] head = new byte[HeadBytes];
        int length = 0;
        try
        {
            using FileStream file = File.OpenRead(path);
            using var zipped = new GZipStream(file, CompressionMode.Decompress);
            int read;
            while (length < head.Length && (read = zipped.Read(head, length, head.Length - length)) > 0)
            {
                length += read;
            }
        }
        catch (Exception ex) when (ex is IOException or InvalidDataException or UnauthorizedAccessException)
        {
            // ⚠️ Not swallowed: the whole-file read meets the same fault and says it, in today's words.
            return null;
        }

        var reader = new Utf8JsonReader(head.AsSpan(0, length), isFinalBlock: length < head.Length, state: default);
        var members = new JsonObject();
        try
        {
            if (!reader.Read() || reader.TokenType != JsonTokenType.StartObject)
            {
                return null;
            }

            while (reader.Read() && reader.TokenType == JsonTokenType.PropertyName)
            {
                if (reader.ValueTextEquals("world"))
                {
                    return SaveGame.HeaderOf(members);
                }

                // A member cut off by the end of the head throws here, and the whole file is read instead.
                string name = reader.GetString()!;
                members.Add(name, JsonNode.Parse(ref reader));
            }
        }
        catch (Exception ex) when (ex is JsonException or ArgumentException or SaveFormatException)
        {
            // ⚠️ Not swallowed either: a key written twice, a value that is not JSON or a header missing a key is the
            // whole-file read's to refuse, and it does, in the words the list has always used.
            return null;
        }

        return null;
    }

    /// <summary>
    /// ⭐ THE ONE DOOR A SAVE COMES IN BY. The village at <paramref name="path"/>, built whole on today's
    /// <paramref name="data"/> — or a refusal in words, with a damaged file kept as <c>.bad</c>. Never throws
    /// for anything in the file.
    /// </summary>
    public static SaveOpened Open(string path, SimConfig data, ISimLogger? logger = null)
    {
        ArgumentNullException.ThrowIfNull(path);
        ArgumentNullException.ThrowIfNull(data);

        (JsonObject? document, SaveOpened? refused) = ReadDocument(path, keepBroken: true);
        if (document is null)
        {
            return refused!;
        }

        SaveHeader? header = null;
        try
        {
            header = SaveGame.HeaderOf(document);
            if (header.Format != SaveGame.Format)
            {
                return new SaveOpened(null, header, OtherFormat(header.Build), $"{path}: format {header.Format}, where this build reads {SaveGame.Format}.");
            }

            SimConfig config = SaveGame.ConfigFor(data, header);
            SimLoop loop = SaveGame.Load(document, config, logger);
            return new SaveOpened(loop, header, null, null) { View = document["view"]?.DeepClone() as JsonObject };
        }
        catch (SaveDataException ex)
        {
            return new SaveOpened(null, header, $"This save holds {ex.What}, which this game no longer has, so it can't be opened.", $"{path}: {ex.Message}");
        }
        catch (Exception ex) when (ex is SaveFormatException or SimConfigException)
        {
            string? kept = KeepTheBrokenFile(path);
            return new SaveOpened(null, header, Damaged(kept), $"{path}: {ex.Message}");
        }
    }

    private static (JsonObject? Document, SaveOpened? Refused) ReadDocument(string path, bool keepBroken)
    {
        string text;
        try
        {
            using FileStream file = File.OpenRead(path);
            using var zipped = new GZipStream(file, CompressionMode.Decompress);
            using var reader = new StreamReader(zipped, Encoding.UTF8);
            text = reader.ReadToEnd();
        }
        catch (Exception ex) when (ex is FileNotFoundException or DirectoryNotFoundException)
        {
            return (null, new SaveOpened(null, null, "That save is not there any more.", $"{path}: {ex.Message}"));
        }
        catch (Exception ex) when (ex is InvalidDataException or EndOfStreamException)
        {
            // ⚠️ Before the IOException arm, which both of these are: not gzip, or cut short, is damage —
            // not a locked file — and is kept as `.bad` like any other.
            return (null, BrokenAt(path, keepBroken, ex.Message));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return (null, new SaveOpened(null, null, $"This save can't be read ({ex.Message}).", $"{path}: {ex.Message}"));
        }

        try
        {
            JsonObject? document = JsonNode.Parse(text) as JsonObject;

            // ⚠️ A key written twice throws only when the object's members are first read — so read
            // them here, inside the catch (`PlayerSettingsFile`'s lesson, D505).
            if (document is not null)
            {
                _ = document.Count;
                return (document, null);
            }
        }
        catch (Exception ex) when (ex is JsonException or ArgumentException)
        {
            return (null, BrokenAt(path, keepBroken, ex.Message));
        }

        return (null, BrokenAt(path, keepBroken, "not a JSON object"));
    }

    private static SaveOpened BrokenAt(string path, bool keepBroken, string why) =>
        new(null, null, Damaged(keepBroken ? KeepTheBrokenFile(path) : null), $"{path}: {why}.");

    private static string OtherFormat(string build) => $"Saved by build {build}, whose saves this build cannot read.";

    private static string Damaged(string? kept) =>
        kept is null
            ? "This save can't be read — it's damaged."
            : $"This save can't be read — it's damaged. A copy was kept as {Path.GetFileName(kept)}.";

    /// <summary>The file's bytes, copied beside it as <c>.bad</c> — or <c>null</c> if even that failed.</summary>
    private static string? KeepTheBrokenFile(string path)
    {
        string broken = path + BrokenSuffix;
        try
        {
            File.Copy(path, broken, overwrite: true);
            return broken;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }
}

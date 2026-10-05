using System.Globalization;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;
using Bclone.Sim.Logging;

namespace Bclone.Sim.Config;

/// <summary>Where the player dragged a window: its side, and whole logical pixels in from that side and down from the top.</summary>
/// <param name="Side"><see cref="PlayerSettings.LeftSide"/> or <see cref="PlayerSettings.RightSide"/>.</param>
/// <param name="X">From the left edge to the window's drawn left edge, or from its drawn right edge to the right edge.</param>
/// <param name="Y">From the top edge to the window's drawn top.</param>
public sealed record WindowPlace(string Side, int X, int Y);

/// <summary>What the player chose for one window. <c>null</c> is "as the game opens it".</summary>
/// <param name="Shown">The window's Settings tick.</param>
/// <param name="Open">Unrolled (true) or rolled up to its title (false).</param>
/// <param name="Place">Where the player dragged it — only ever written by a drag (`settings-persistence.md §4`).</param>
public sealed record WindowPrefs(bool? Shown, bool? Open, WindowPlace? Place);

/// <summary>One thing wrong with a settings file, in a sentence, at the level it should be logged.</summary>
public sealed record PlayerSettingsProblem(LogLevel Level, string Sentence);

/// <summary>
/// ⭐ The player's preferences — <b>how they see and drive the game, never what the village is</b>
/// (D504, `specs/settings-persistence.md`).
/// </summary>
/// <remarks>
/// <para>
/// ⛔ <b>The sim never reads this and it never enters the hash.</b> It lives in <c>Bclone.Sim.Config</c>
/// beside <see cref="NewGame"/> for the same reason that does — it is a shell rule with no engine in it,
/// so it can be tested — not because the village has any business with it. <i>Share the work out</i> is
/// hashed village state and is deliberately absent (§3): it belongs to save/load.
/// </para>
/// <para>
/// Every field is optional; <c>null</c> or an absent key is the view's own default, so a toggle or a
/// window added later is remembered the day it is written. Whole numbers only — the sim library's
/// public API carries no floats (<c>FloatBanTests</c>).
/// </para>
/// </remarks>
public sealed record PlayerSettings
{
    /// <summary>The file format this build writes.</summary>
    public const int CurrentVersion = 1;

    /// <summary>The smallest the UI-size dial goes.</summary>
    public const int SmallestUiScalePercent = 55;

    /// <summary>The largest the UI-size dial goes.</summary>
    public const int LargestUiScalePercent = 115;

    /// <summary>The UI size before anybody turns the dial (Joe, 2026-09-06).</summary>
    public const int DefaultUiScalePercent = 75;

    /// <summary>One press of the dial.</summary>
    public const int UiScaleStep = 5;

    /// <summary>A window docked on the left, and measured from it.</summary>
    public const string LeftSide = "left";

    /// <summary>A window docked on the right, and measured from it.</summary>
    public const string RightSide = "right";

    /// <summary>What the Routes button cycles through, in its order.</summary>
    public static IReadOnlyList<string> RoutesLevels { get; } = new[] { "off", "selected", "all" };

    /// <summary>The UI size, a whole percent on the dial's steps.</summary>
    public int? UiScalePercent { get; init; }

    /// <summary>One of <see cref="RoutesLevels"/>.</summary>
    public string? Routes { get; init; }

    /// <summary>The view's on/off switches by id — the Paths overlay and every <i>On the map</i> tick.</summary>
    public IReadOnlyDictionary<string, bool> Toggles { get; init; } = new Dictionary<string, bool>(StringComparer.Ordinal);

    /// <summary>The windows by title.</summary>
    public IReadOnlyDictionary<string, WindowPrefs> Windows { get; init; } = new Dictionary<string, WindowPrefs>(StringComparer.Ordinal);

    /// <summary>The new-game screen's rows from the last valley founded, by row id. Never the seed or the name.</summary>
    public IReadOnlyDictionary<string, string> NewGameRows { get; init; } = new Dictionary<string, string>(StringComparer.Ordinal);

    /// <summary>
    /// The file's text: keys sorted, absent values left out — so a diff or a hand edit reads cleanly,
    /// and two records that say the same thing are the same string.
    /// </summary>
    public string ToJson()
    {
        var doc = new JsonObject { ["version"] = CurrentVersion };

        if (UiScalePercent is int scale)
        {
            doc["ui_scale_percent"] = scale;
        }

        if (Routes is not null)
        {
            doc["routes"] = Routes;
        }

        if (Toggles.Count > 0)
        {
            var toggles = new JsonObject();
            foreach (string id in Toggles.Keys.Order(StringComparer.Ordinal))
            {
                toggles[id] = Toggles[id];
            }

            doc["toggles"] = toggles;
        }

        if (Windows.Count > 0)
        {
            var windows = new JsonObject();
            foreach (string title in Windows.Keys.Order(StringComparer.Ordinal))
            {
                WindowPrefs prefs = Windows[title];
                var window = new JsonObject();
                if (prefs.Open is bool open)
                {
                    window["open"] = open;
                }

                if (prefs.Shown is bool shown)
                {
                    window["shown"] = shown;
                }

                if (prefs.Place is WindowPlace place)
                {
                    window["side"] = place.Side;
                    window["x"] = place.X;
                    window["y"] = place.Y;
                }

                windows[title] = window;
            }

            doc["windows"] = windows;
        }

        if (NewGameRows.Count > 0)
        {
            var rows = new JsonObject();
            foreach (string id in NewGameRows.Keys.Order(StringComparer.Ordinal))
            {
                rows[id] = NewGameRows[id];
            }

            doc["new_game"] = rows;
        }

        return doc.ToJsonString(Written);
    }

    /// <summary>Indented, and a window called <i>What's here</i> written as that rather than as <c>'</c>.</summary>
    private static readonly JsonSerializerOptions Written = new()
    {
        WriteIndented = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    /// <summary>A UI size read from anywhere, brought onto the dial: clamped, and rounded to the nearest step.</summary>
    public static int OnTheDial(int percent)
    {
        int clamped = Math.Clamp(percent, SmallestUiScalePercent, LargestUiScalePercent);
        int steps = ((clamped - SmallestUiScalePercent) + (UiScaleStep / 2)) / UiScaleStep;
        return Math.Min(LargestUiScalePercent, SmallestUiScalePercent + (steps * UiScaleStep));
    }
}

/// <summary>
/// Reading and writing <c>settings.json</c> (`settings-persistence.md §5–6`): <b>a bad file never stops
/// the game and is never thrown away</b>, and every problem comes back as a sentence for the log.
/// </summary>
public static class PlayerSettingsFile
{
    /// <summary>What an unreadable file is copied to before anything can write over it.</summary>
    public const string BrokenSuffix = ".bad";

    /// <summary>What a save is written to before it is moved over the real file.</summary>
    public const string PartSuffix = ".tmp";

    private static readonly JsonDocumentOptions DocumentOptions = new()
    {
        CommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
    };

    /// <summary>
    /// The player's settings at <paramref name="path"/>, or an empty record — never a throw.
    /// </summary>
    /// <remarks>
    /// No file is a first launch and says nothing. A file that is not a JSON object is copied to
    /// <c>…json.bad</c> and read as nothing, with an error. Anything else wrong is one key at its
    /// default and a warning; the rest of the file still counts.
    /// </remarks>
    public static PlayerSettings Load(string path, ICollection<PlayerSettingsProblem> problems)
    {
        ArgumentNullException.ThrowIfNull(path);
        ArgumentNullException.ThrowIfNull(problems);

        if (!File.Exists(path))
        {
            return new PlayerSettings();
        }

        string text;
        try
        {
            text = File.ReadAllText(path);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            problems.Add(new(LogLevel.Error, $"The settings at {path} could not be read ({ex.Message}); every setting is at its default."));
            return new PlayerSettings();
        }

        JsonObject? doc = null;
        string? why = null;
        try
        {
            doc = JsonNode.Parse(text, null, DocumentOptions) as JsonObject;

            // ⚠️ An object's members are read lazily, and a key written twice throws only then — so
            // it is read here, inside the catch, rather than wherever the first lookup happens to be.
            if (doc is null)
            {
                why = "it is not a JSON object";
            }
            else
            {
                _ = doc.Count;
            }
        }
        catch (Exception ex) when (ex is JsonException or ArgumentException)
        {
            doc = null;
            why = ex.Message;
        }

        if (doc is null)
        {
            string kept = KeepTheBrokenFile(path, text, problems);
            problems.Add(new(LogLevel.Error, $"The settings at {path} could not be read ({why}); every setting is at its default{kept}."));
            return new PlayerSettings();
        }

        return Read(doc, path, problems);
    }

    /// <summary>Write <paramref name="settings"/> to <paramref name="path"/> whole or not at all.</summary>
    /// <exception cref="IOException">The disk said no; the caller logs it and carries on.</exception>
    /// <exception cref="UnauthorizedAccessException">The folder is not ours to write; likewise.</exception>
    public static void Save(string path, PlayerSettings settings)
    {
        ArgumentNullException.ThrowIfNull(path);
        ArgumentNullException.ThrowIfNull(settings);

        string? folder = Path.GetDirectoryName(Path.GetFullPath(path));
        if (!string.IsNullOrEmpty(folder))
        {
            Directory.CreateDirectory(folder);
        }

        // ⭐ ATOMIC: a crash between these two lines leaves the old file, never half of a new one.
        string part = path + PartSuffix;
        File.WriteAllText(part, settings.ToJson());
        File.Move(part, path, overwrite: true);
    }

    private static string KeepTheBrokenFile(string path, string text, ICollection<PlayerSettingsProblem> problems)
    {
        string broken = path + BrokenSuffix;
        try
        {
            File.WriteAllText(broken, text);
            return $"; the file is kept as {broken}";
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            problems.Add(new(LogLevel.Error, $"The unreadable settings could not be kept as {broken} ({ex.Message})."));
            return string.Empty;
        }
    }

    private static readonly HashSet<string> KnownKeys = new(StringComparer.Ordinal)
    {
        "version", "ui_scale_percent", "routes", "toggles", "windows", "new_game",
    };

    private static readonly HashSet<string> KnownWindowKeys = new(StringComparer.Ordinal)
    {
        "shown", "open", "side", "x", "y",
    };

    private static PlayerSettings Read(JsonObject doc, string path, ICollection<PlayerSettingsProblem> problems)
    {
        void Warn(string sentence) => problems.Add(new(LogLevel.Warn, $"Settings ({path}): {sentence}"));

        var unknown = doc.Select(pair => pair.Key).Where(key => !KnownKeys.Contains(key)).ToList();

        if (doc["version"] is JsonNode versionNode
            && TryInt(versionNode, out int version)
            && version > PlayerSettings.CurrentVersion)
        {
            Warn($"written by a newer build (version {version}); reading what this one knows.");
        }

        int? scale = null;
        if (doc["ui_scale_percent"] is JsonNode scaleNode)
        {
            if (TryInt(scaleNode, out int read))
            {
                scale = PlayerSettings.OnTheDial(read);
                if (scale != read)
                {
                    Warn($"ui_scale_percent {read} is off the dial; using {scale}.");
                }
            }
            else
            {
                Warn($"ui_scale_percent should be a whole number, not {scaleNode.ToJsonString()}; using the default.");
            }
        }

        string? routes = null;
        if (doc["routes"] is JsonNode routesNode)
        {
            if (TryString(routesNode, out string read) && PlayerSettings.RoutesLevels.Contains(read, StringComparer.Ordinal))
            {
                routes = read;
            }
            else
            {
                Warn($"routes should be one of {string.Join(", ", PlayerSettings.RoutesLevels)}, not {routesNode.ToJsonString()}; using the default.");
            }
        }

        var toggles = new Dictionary<string, bool>(StringComparer.Ordinal);
        foreach ((string id, JsonNode? value) in ObjectAt(doc, "toggles", Warn))
        {
            if (TryBool(value, out bool on))
            {
                toggles[id] = on;
            }
            else
            {
                Warn($"toggles.{id} should be true or false, not {Shown(value)}; using the default.");
            }
        }

        var windows = new Dictionary<string, WindowPrefs>(StringComparer.Ordinal);
        foreach ((string title, JsonNode? value) in ObjectAt(doc, "windows", Warn))
        {
            if (value is not JsonObject window)
            {
                Warn($"windows.{title} should be an object, not {Shown(value)}; it is at its default.");
                continue;
            }

            unknown.AddRange(window.Select(pair => pair.Key).Where(key => !KnownWindowKeys.Contains(key)).Select(key => $"windows.{title}.{key}"));
            windows[title] = new WindowPrefs(
                BoolAt(window, "shown", $"windows.{title}", Warn),
                BoolAt(window, "open", $"windows.{title}", Warn),
                PlaceAt(window, $"windows.{title}", Warn));
        }

        var rows = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach ((string id, JsonNode? value) in ObjectAt(doc, "new_game", Warn))
        {
            if (TryString(value, out string text))
            {
                rows[id] = text;
            }
            else if (value is not null && TryInt(value, out int number))
            {
                rows[id] = number.ToString(CultureInfo.InvariantCulture);
            }
            else
            {
                Warn($"new_game.{id} should be a value, not {Shown(value)}; the row is at its default.");
            }
        }

        if (unknown.Count > 0)
        {
            Warn($"{string.Join(", ", unknown)} {(unknown.Count == 1 ? "is" : "are")} not a setting this build knows; ignored.");
        }

        return new PlayerSettings
        {
            UiScalePercent = scale,
            Routes = routes,
            Toggles = toggles,
            Windows = windows,
            NewGameRows = rows,
        };
    }

    private static IEnumerable<KeyValuePair<string, JsonNode?>> ObjectAt(JsonObject doc, string key, Action<string> warn)
    {
        JsonNode? node = doc[key];
        if (node is null)
        {
            return Array.Empty<KeyValuePair<string, JsonNode?>>();
        }

        if (node is JsonObject inner)
        {
            return inner.ToList();
        }

        warn($"{key} should be an object, not {node.ToJsonString()}; every one is at its default.");
        return Array.Empty<KeyValuePair<string, JsonNode?>>();
    }

    private static bool? BoolAt(JsonObject window, string key, string where, Action<string> warn)
    {
        if (window[key] is not JsonNode node)
        {
            return null;
        }

        if (TryBool(node, out bool value))
        {
            return value;
        }

        warn($"{where}.{key} should be true or false, not {node.ToJsonString()}; using the default.");
        return null;
    }

    private static WindowPlace? PlaceAt(JsonObject window, string where, Action<string> warn)
    {
        JsonNode? side = window["side"];
        JsonNode? x = window["x"];
        JsonNode? y = window["y"];
        if (side is null && x is null && y is null)
        {
            return null;
        }

        if (side is not null && TryString(side, out string s) && (s == PlayerSettings.LeftSide || s == PlayerSettings.RightSide)
            && x is not null && TryInt(x, out int left)
            && y is not null && TryInt(y, out int top))
        {
            return new WindowPlace(s, left, top);
        }

        warn($"{where} needs side (left or right), x and y as whole numbers to be placed; it opens where the game puts it.");
        return null;
    }

    private static string Shown(JsonNode? node) => node?.ToJsonString() ?? "null";

    private static bool TryInt(JsonNode node, out int value)
    {
        value = 0;
        return node is JsonValue v && v.GetValueKind() == JsonValueKind.Number && v.TryGetValue(out value);
    }

    private static bool TryBool(JsonNode? node, out bool value)
    {
        value = false;
        return node is JsonValue v && v.GetValueKind() is JsonValueKind.True or JsonValueKind.False && v.TryGetValue(out value);
    }

    private static bool TryString(JsonNode? node, out string value)
    {
        value = string.Empty;
        return node is JsonValue v && v.GetValueKind() == JsonValueKind.String && v.TryGetValue(out value!);
    }
}

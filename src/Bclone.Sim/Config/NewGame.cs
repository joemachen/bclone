using System.Globalization;
using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

namespace Bclone.Sim.Config;

/// <summary>
/// One row of the new-game screen: the config keys it overrides, and a range or its levels (D477,
/// `new-game-screen.md §5`).
/// </summary>
/// <remarks>
/// Three kinds. <c>range</c> sets one key to the number chosen. <c>scale</c> sets several keys to the
/// file's values times the percentage chosen (100 = as shipped). <c>levels</c> sets each level's keys
/// to that level's values.
/// </remarks>
public sealed record NewGameRow
{
    /// <summary>The row's name in a share code: lower-case letters and digits.</summary>
    [JsonPropertyName("id")]
    public string Id { get; init; } = string.Empty;

    /// <summary>What the screen calls the row.</summary>
    [JsonPropertyName("label")]
    public string Label { get; init; } = string.Empty;

    /// <summary><c>range</c>, <c>scale</c> or <c>levels</c>.</summary>
    [JsonPropertyName("kind")]
    public string Kind { get; init; } = NewGame.RangeKind;

    /// <summary>The config keys a <c>range</c> (one) or a <c>scale</c> (one or more) sets.</summary>
    [JsonPropertyName("keys")]
    public IReadOnlyList<string> Keys { get; init; } = Array.Empty<string>();

    /// <summary>The least a <c>range</c> or <c>scale</c> may be set to.</summary>
    [JsonPropertyName("min")]
    public int Min { get; init; }

    /// <summary>The most a <c>range</c> or <c>scale</c> may be set to.</summary>
    [JsonPropertyName("max")]
    public int Max { get; init; }

    /// <summary>The step between a <c>range</c>'s or <c>scale</c>'s values, counted from <see cref="Min"/>.</summary>
    [JsonPropertyName("step")]
    public int Step { get; init; } = 1;

    /// <summary>What follows the number on screen — <c>"%"</c>, <c>" tiles"</c>, or nothing.</summary>
    [JsonPropertyName("unit")]
    public string Unit { get; init; } = string.Empty;

    /// <summary>A <c>levels</c> row's choices, in the order the screen offers them.</summary>
    [JsonPropertyName("levels")]
    public IReadOnlyList<NewGameLevel> Levels { get; init; } = Array.Empty<NewGameLevel>();
}

/// <summary>One choice of a <c>levels</c> row, and the config values it sets.</summary>
public sealed record NewGameLevel
{
    /// <summary>The level's name in a share code: lower-case letters and digits.</summary>
    [JsonPropertyName("value")]
    public string Value { get; init; } = string.Empty;

    /// <summary>What the screen calls the level.</summary>
    [JsonPropertyName("label")]
    public string Label { get; init; } = string.Empty;

    /// <summary>The config keys this level sets, and to what.</summary>
    [JsonPropertyName("set")]
    public IReadOnlyDictionary<string, JsonElement> Set { get; init; } = new Dictionary<string, JsonElement>();
}

/// <summary>
/// What the new-game screen says: the seed as typed, the name, and a value per row by row id.
/// </summary>
public sealed record NewGameSettings(
    string SeedText, string? VillageName, IReadOnlyDictionary<string, string> Values);

/// <summary>
/// The new-game screen's rules, without the screen (D477, `new-game-screen.md`): each row's default,
/// applying the settings to a config, and the share code.
/// </summary>
/// <remarks>
/// <para>
/// <b>⭐ One mechanism for every row.</b> A row's keys are overridden on the config serialised to
/// json, and the result is loaded through <see cref="SimConfigLoader.Parse"/> — the same
/// <see cref="SimConfig.Validate"/> as any file. So a modder's row and a shipped row are one thing,
/// and a value the config would refuse is refused here in the config's own words.
/// </para>
/// <para>
/// ⛔ <b>A row's default is the CONFIG's value, never a number in this file</b> — so the screen at its
/// defaults founds exactly the valley the config file names (guarded, `§8.2`).
/// </para>
/// </remarks>
public static class NewGame
{
    /// <summary>A row that sets one key to the number chosen.</summary>
    public const string RangeKind = "range";

    /// <summary>A row that scales several keys together by the percentage chosen; 100 is the file's values.</summary>
    public const string ScaleKind = "scale";

    /// <summary>A row of named choices, each setting keys to values.</summary>
    public const string LevelsKind = "levels";

    /// <summary>A <c>levels</c> row's value when the config matches none of its levels: it sets nothing.</summary>
    public const string AsConfigured = "custom";

    /// <summary>The longest name a village may be given.</summary>
    public const int LongestVillageName = 24;

    /// <summary>The shipped rows — the code's defaults, replaced wholesale by a json's list.</summary>
    /// <remarks>
    /// ⚠️ <b>The ranges and levels here are candidates until `new-game-screen.md §7` measures
    /// them</b>; that table, not this file, is where each end is argued.
    /// </remarks>
    public static IReadOnlyList<NewGameRow> DefaultRows() => new[]
    {
        new NewGameRow
        {
            Id = "river", Label = "River width", Kind = RangeKind,
            Keys = new[] { "river_width_tiles" }, Min = 0, Max = 6, Step = 1, Unit = " tiles",
        },
        new NewGameRow
        {
            Id = "flow", Label = "River runs", Kind = LevelsKind,
            Levels = new[]
            {
                Level("we", "West to east", ("river_course", "\"we\"")),
                Level("ns", "North to south", ("river_course", "\"ns\"")),
                Level("nwse", "North-west to south-east", ("river_course", "\"nwse\"")),
                Level("swne", "South-west to north-east", ("river_course", "\"swne\"")),
                Level("any", "Any way (the seed's)", ("river_course", "\"any\"")),
            },
        },
        new NewGameRow
        {
            Id = "woods", Label = "Forest cover", Kind = RangeKind,
            Keys = new[] { "forest_coverage_percent" }, Min = 20, Max = 50, Step = 5, Unit = "%",
        },
        new NewGameRow
        {
            Id = "stone", Label = "Stone", Kind = LevelsKind,
            Levels = new[]
            {
                Level("sparse", "Sparse", ("stone_seam_count", "4"), ("extra_stone_seams", "4")),
                Level("usual", "Usual", ("stone_seam_count", "4"), ("extra_stone_seams", "8")),
                Level("rich", "Rich", ("stone_seam_count", "4"), ("extra_stone_seams", "12")),
            },
        },
        new NewGameRow
        {
            Id = "iron", Label = "Iron", Kind = LevelsKind,
            Levels = new[]
            {
                Level("sparse", "Sparse", ("iron_seam_count", "2"), ("extra_iron_seams", "0")),
                Level("usual", "Usual", ("iron_seam_count", "2"), ("extra_iron_seams", "2")),
                Level("rich", "Rich", ("iron_seam_count", "2"), ("extra_iron_seams", "4")),
            },
        },
        new NewGameRow
        {
            Id = "scatter", Label = "Seam scatter", Kind = ScaleKind,
            Keys = new[] { "seam_angle_scatter_percent", "seam_reach_scatter_percent", "seam_size_scatter_percent" },
            Min = 0, Max = 100, Step = 10, Unit = "%",
        },
    };

    private static NewGameLevel Level(string value, string label, params (string Key, string Json)[] set)
    {
        var values = new Dictionary<string, JsonElement>(StringComparer.Ordinal);
        foreach ((string key, string json) in set)
        {
            values[key] = JsonDocument.Parse(json).RootElement.Clone();
        }

        return new NewGameLevel { Value = value, Label = label, Set = values };
    }

    /// <summary>Refuse rows the screen could not offer, or whose keys the config does not have.</summary>
    public static void ValidateRows(IReadOnlyList<NewGameRow>? rows)
    {
        if (rows is null)
        {
            throw new SimConfigException("new_game_options must be a list (it may be empty).");
        }

        var ids = new HashSet<string>(StringComparer.Ordinal);
        foreach (NewGameRow row in rows)
        {
            string where = $"new_game_options row \"{row.Id}\"";
            if (!IsCodeWord(row.Id))
            {
                throw new SimConfigException($"{where}: an id is lower-case letters and digits only.");
            }

            if (!ids.Add(row.Id))
            {
                throw new SimConfigException($"{where} appears twice.");
            }

            if (string.IsNullOrWhiteSpace(row.Label))
            {
                throw new SimConfigException($"{where} has no label.");
            }

            switch (row.Kind)
            {
                case RangeKind or ScaleKind:
                    if (row.Keys.Count == 0 || (row.Kind == RangeKind && row.Keys.Count != 1))
                    {
                        throw new SimConfigException(
                            $"{where}: a {row.Kind} row sets {(row.Kind == RangeKind ? "exactly one key" : "at least one key")}.");
                    }

                    if (row.Step <= 0 || row.Min > row.Max || (row.Max - row.Min) % row.Step != 0)
                    {
                        throw new SimConfigException(
                            $"{where}: min {row.Min}, max {row.Max} and step {row.Step} do not make a range.");
                    }

                    foreach (string key in row.Keys)
                    {
                        RequireAKey(key, where);
                    }

                    break;

                case LevelsKind:
                    if (row.Levels.Count == 0)
                    {
                        throw new SimConfigException($"{where}: a levels row needs at least one level.");
                    }

                    var values = new HashSet<string>(StringComparer.Ordinal);
                    foreach (NewGameLevel level in row.Levels)
                    {
                        if (!IsCodeWord(level.Value) || level.Value == AsConfigured || !values.Add(level.Value))
                        {
                            throw new SimConfigException(
                                $"{where}: level \"{level.Value}\" must be a unique lower-case word, and not \"{AsConfigured}\".");
                        }

                        if (level.Set.Count == 0)
                        {
                            throw new SimConfigException($"{where}: level \"{level.Value}\" sets nothing.");
                        }

                        foreach (string key in level.Set.Keys)
                        {
                            RequireAKey(key, where);
                        }
                    }

                    break;

                default:
                    throw new SimConfigException(
                        $"{where}: kind is {RangeKind}, {ScaleKind} or {LevelsKind} (got \"{row.Kind}\").");
            }
        }
    }

    /// <summary>
    /// ⛔ A row whose key the config does not have would do nothing at all — the serialiser ignores a
    /// key it does not know — so it is refused here rather than offered as a slider that moves nothing.
    /// </summary>
    private static void RequireAKey(string key, string where)
    {
        if (!ConfigKeys.Contains(key))
        {
            throw new SimConfigException($"{where} sets \"{key}\", which is not a config key.");
        }
    }

    private static readonly HashSet<string> ConfigKeys = typeof(SimConfig)
        .GetProperties(BindingFlags.Public | BindingFlags.Instance)
        .Select(p => p.GetCustomAttribute<JsonPropertyNameAttribute>()?.Name)
        .Where(name => name is not null)
        .Select(name => name!)
        .ToHashSet(StringComparer.Ordinal);

    private static bool IsCodeWord(string text) =>
        text.Length > 0 && text.All(c => c is (>= 'a' and <= 'z') or (>= '0' and <= '9'));

    /// <summary>Every row at the value <paramref name="config"/> has, with <paramref name="seedText"/> and no name.</summary>
    public static NewGameSettings Defaults(SimConfig config, string seedText)
    {
        ArgumentNullException.ThrowIfNull(config);
        JsonObject doc = Serialise(config);

        var values = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (NewGameRow row in config.NewGameOptions)
        {
            values[row.Id] = DefaultValue(row, doc);
        }

        return new NewGameSettings(seedText, null, values);
    }

    private static string DefaultValue(NewGameRow row, JsonObject doc)
    {
        switch (row.Kind)
        {
            case RangeKind:
                return ((int)doc[row.Keys[0]]!).ToString(CultureInfo.InvariantCulture);
            case ScaleKind:
                return "100";
            default:
                foreach (NewGameLevel level in row.Levels)
                {
                    if (level.Set.All(pair => JsonNode.DeepEquals(doc[pair.Key], JsonNode.Parse(pair.Value.GetRawText()))))
                    {
                        return level.Value;
                    }
                }

                return AsConfigured;
        }
    }

    /// <summary>
    /// The config the settings found a village on: every row's keys overridden, the seed and the name
    /// set, loaded and validated as any config is.
    /// </summary>
    /// <exception cref="SimConfigException">A seed or a value the rules refuse, in a sentence.</exception>
    public static SimConfig Apply(SimConfig config, NewGameSettings settings)
    {
        ArgumentNullException.ThrowIfNull(config);
        ArgumentNullException.ThrowIfNull(settings);

        if (!SeedText.TryToSeed(settings.SeedText, out ulong seed, out string? refusal))
        {
            throw new SimConfigException(refusal!);
        }

        JsonObject doc = Serialise(config);
        JsonObject file = Serialise(config);

        foreach (NewGameRow row in config.NewGameOptions)
        {
            string value = settings.Values.TryGetValue(row.Id, out string? chosen) ? chosen : DefaultValue(row, file);
            if (!IsAllowed(row, value, out string? why))
            {
                throw new SimConfigException(why!);
            }

            switch (row.Kind)
            {
                case RangeKind:
                    doc[row.Keys[0]] = int.Parse(value, CultureInfo.InvariantCulture);
                    break;
                case ScaleKind:
                    int percent = int.Parse(value, CultureInfo.InvariantCulture);
                    foreach (string key in row.Keys)
                    {
                        int shipped = (int)file[key]!;
                        doc[key] = ((shipped * percent) + 50) / 100;
                    }

                    break;
                default:
                    NewGameLevel? level = row.Levels.FirstOrDefault(l => l.Value == value);
                    if (level is not null)
                    {
                        foreach ((string key, JsonElement json) in level.Set)
                        {
                            doc[key] = JsonNode.Parse(json.GetRawText());
                        }
                    }

                    break;
            }
        }

        doc["seed"] = seed;
        string? name = settings.VillageName?.Trim();
        doc["village_name"] = string.IsNullOrEmpty(name) ? null : name;

        return SimConfigLoader.Parse(doc.ToJsonString(), "the new-game screen");
    }

    /// <summary>Whether <paramref name="value"/> is one the row offers; if not, the sentence that says so.</summary>
    public static bool IsAllowed(NewGameRow row, string value, out string? refusal)
    {
        ArgumentNullException.ThrowIfNull(row);
        refusal = null;

        if (row.Kind is RangeKind or ScaleKind)
        {
            if (!int.TryParse(value, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out int number))
            {
                refusal = $"\"{row.Id}={value}\" — {row.Label.ToLowerInvariant()} is a number.";
                return false;
            }

            if (number < row.Min || number > row.Max || (number - row.Min) % row.Step != 0)
            {
                refusal = $"\"{row.Id}={value}\" — {row.Label.ToLowerInvariant()} goes from {row.Min} to "
                    + $"{row.Max}{(row.Step == 1 ? string.Empty : $" in steps of {row.Step}")}.";
                return false;
            }

            return true;
        }

        if (value == AsConfigured || row.Levels.Any(l => l.Value == value))
        {
            return true;
        }

        refusal = $"\"{row.Id}={value}\" — {row.Label.ToLowerInvariant()} is one of "
            + $"{string.Join(", ", row.Levels.Select(l => l.Value))}.";
        return false;
    }

    /// <summary>
    /// The one string that names the valley: the seed, <c>#</c>, then <b>every</b> row as
    /// <c>id=value</c> in the rows' order — so a code survives a later build moving a default.
    /// </summary>
    public static string ShareCode(SimConfig config, NewGameSettings settings)
    {
        ArgumentNullException.ThrowIfNull(config);
        ArgumentNullException.ThrowIfNull(settings);

        JsonObject file = Serialise(config);
        var code = new StringBuilder(SeedText.Normalise(settings.SeedText));
        string separator = SeedText.SettingsMark.ToString();
        foreach (NewGameRow row in config.NewGameOptions)
        {
            string value = settings.Values.TryGetValue(row.Id, out string? chosen) ? chosen : DefaultValue(row, file);
            code.Append(separator).Append(row.Id).Append('=').Append(value);
            separator = ",";
        }

        return code.ToString();
    }

    /// <summary>
    /// Read what the player typed or pasted: a bare seed keeps <paramref name="current"/>'s rows; a
    /// share code sets them. Anything the rules refuse leaves <paramref name="current"/> as it was and
    /// says why.
    /// </summary>
    /// <remarks>
    /// A row a code does not mention keeps its value from <paramref name="current"/> — so a code from a
    /// build before a row existed still reads.
    /// </remarks>
    public static bool TryRead(
        SimConfig config, string text, NewGameSettings current, out NewGameSettings read, out string? refusal)
    {
        ArgumentNullException.ThrowIfNull(config);
        ArgumentNullException.ThrowIfNull(current);
        read = current;

        text ??= string.Empty;
        int mark = text.IndexOf(SeedText.SettingsMark, StringComparison.Ordinal);
        string seedPart = mark < 0 ? text : text[..mark];

        if (!SeedText.TryToSeed(seedPart, out _, out refusal))
        {
            return false;
        }

        var values = new Dictionary<string, string>(current.Values, StringComparer.Ordinal);
        if (mark >= 0)
        {
            foreach (string part in text[(mark + 1)..].Split(',', StringSplitOptions.TrimEntries))
            {
                if (part.Length == 0)
                {
                    continue;
                }

                int equals = part.IndexOf('=', StringComparison.Ordinal);
                if (equals < 0)
                {
                    refusal = $"\"{part}\" — a setting is written id=value.";
                    return false;
                }

                string id = part[..equals].Trim().ToLowerInvariant();
                string value = part[(equals + 1)..].Trim().ToLowerInvariant();
                NewGameRow? row = config.NewGameOptions.FirstOrDefault(r => r.Id == id);
                if (row is null)
                {
                    refusal = $"\"{part}\" — this game has no setting called \"{id}\".";
                    return false;
                }

                if (!IsAllowed(row, value, out refusal))
                {
                    return false;
                }

                values[id] = value;
            }
        }

        read = current with { SeedText = seedPart.Trim(), Values = values };
        refusal = null;
        return true;
    }

    private static JsonObject Serialise(SimConfig config) =>
        JsonSerializer.SerializeToNode(config, SimConfigLoader.SerializerOptions)!.AsObject();
}

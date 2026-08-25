using System.Text.Json;
using System.Text.Json.Serialization;

namespace Bclone.Sim.Config;

/// <summary>
/// Reads <see cref="SimConfig"/> from JSON.
/// </summary>
/// <remarks>
/// Comments and trailing commas are permitted so content files can carry
/// explanation for modders (DESIGN.md §3 — moddability as a first principle)
/// without pulling in a third-party parser.
/// </remarks>
public static class SimConfigLoader
{
    private static readonly JsonSerializerOptions Options = new()
    {
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
        PropertyNameCaseInsensitive = true,

        // ⭐ So a modder writes `"stored_by": ["Shed", "Cart"]` rather than a list of integers
        // they would have to look up (D210, `goods-catalog.md`). Safe to add globally and that
        // was checked rather than assumed: the only enum this config has ever deserialized is
        // `SkillRow.GrownBy`, which carries its own copy of this converter and already expects
        // strings — so nothing that parses today parses differently.
        Converters = { new JsonStringEnumConverter() },
    };

    /// <summary>Load and validate config from a file.</summary>
    /// <exception cref="SimConfigException">
    /// If the file is missing, unparseable, or contains out-of-range values.
    /// Always thrown with the offending path in the message — a config error
    /// should never require a debugger to diagnose.
    /// </exception>
    public static SimConfig LoadFromFile(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        string fullPath = Path.GetFullPath(path);

        if (!File.Exists(fullPath))
        {
            throw new SimConfigException($"Sim config not found at '{fullPath}'.");
        }

        string json;
        try
        {
            json = File.ReadAllText(fullPath);
        }
        catch (IOException ex)
        {
            throw new SimConfigException($"Could not read sim config at '{fullPath}': {ex.Message}", ex);
        }

        return Parse(json, fullPath);
    }

    /// <summary>Parse and validate config from a JSON string.</summary>
    /// <param name="json">The document text.</param>
    /// <param name="sourceName">Shown in error messages so failures stay traceable.</param>
    public static SimConfig Parse(string json, string sourceName = "<string>")
    {
        SimConfig? config;
        try
        {
            config = JsonSerializer.Deserialize<SimConfig>(json, Options);
        }
        catch (JsonException ex)
        {
            throw new SimConfigException($"Malformed sim config in '{sourceName}': {ex.Message}", ex);
        }

        if (config is null)
        {
            throw new SimConfigException($"Sim config in '{sourceName}' deserialized to null.");
        }

        try
        {
            config.Validate();
        }
        catch (SimConfigException ex)
        {
            throw new SimConfigException($"Invalid sim config in '{sourceName}': {ex.Message}", ex);
        }

        return config;
    }
}

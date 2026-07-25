using System;
using System.IO;
using Bclone.Sim.Config;

namespace Bclone.Game;

/// <summary>
/// Finds <c>data/sim.config.json</c> on disk.
/// </summary>
/// <remarks>
/// The config lives outside the Godot project on purpose — it is content, and
/// content is meant to be editable by modders without opening the engine
/// (DESIGN.md §3). That means the game has to go looking for it rather than
/// loading it from <c>res://</c>.
/// </remarks>
public static class ConfigLocator
{
    private const string RelativePath = "data/sim.config.json";

    /// <summary>
    /// Walk up from the running project looking for the repo's data directory.
    /// Returns null if it cannot be found.
    /// </summary>
    public static string? Locate()
    {
        // ProjectSettings.GlobalizePath gives the real path of res:// when running
        // from the editor or a non-packed build.
        string start = Godot.ProjectSettings.GlobalizePath("res://");
        if (string.IsNullOrEmpty(start))
        {
            start = AppContext.BaseDirectory;
        }

        var directory = new DirectoryInfo(start);
        while (directory is not null)
        {
            string candidate = Path.Combine(directory.FullName, RelativePath);
            if (File.Exists(candidate))
            {
                return candidate;
            }

            directory = directory.Parent;
        }

        return null;
    }

    /// <summary>
    /// Load config from disk, or fall back to built-in defaults.
    /// </summary>
    /// <param name="sourceDescription">
    /// Where the config actually came from, so the UI can say so rather than
    /// leaving the player guessing which numbers are in play.
    /// </param>
    public static SimConfig LoadOrDefault(out string sourceDescription)
    {
        string? path = Locate();

        if (path is null)
        {
            sourceDescription = "built-in defaults (data/sim.config.json not found)";
            return new SimConfig();
        }

        try
        {
            SimConfig config = SimConfigLoader.LoadFromFile(path);
            sourceDescription = path;
            return config;
        }
        catch (SimConfigException ex)
        {
            // Never swallow (METHODOLOGY.md §4). A broken config should be obvious
            // on screen, not a silent fallback to different numbers.
            Godot.GD.PushError($"[bclone] Failed to load sim config from '{path}': {ex.Message}");
            sourceDescription = $"built-in defaults (error loading {Path.GetFileName(path)})";
            return new SimConfig();
        }
    }
}

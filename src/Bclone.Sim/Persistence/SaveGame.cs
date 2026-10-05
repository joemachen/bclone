using System.Text.Json.Nodes;
using Bclone.Sim.Config;
using Bclone.Sim.Core;
using Bclone.Sim.Logging;

namespace Bclone.Sim.Persistence;

/// <summary>
/// What the load list shows about a save without building its village (`specs/save-load.md §4`).
/// </summary>
/// <param name="Format">The save format it was written in — a different one is refused (§7).</param>
/// <param name="Build">The <c>VERSION</c> that wrote it, said on the list and in a refusal.</param>
/// <param name="SavedAt">The real date and time, written by the view. The sim never reads it.</param>
/// <param name="ShareCode">The seed and every new-game row (<see cref="NewGame.ShareCode"/>).</param>
/// <param name="VillageName">The name the player gave, or <c>null</c> for the seed's own.</param>
/// <param name="Tick">The tick it was saved at — the list turns it into a year and a season.</param>
public sealed record SaveHeader(int Format, string Build, string SavedAt, string ShareCode, string? VillageName, ulong Tick);

/// <summary>
/// A village as a save: the header and the world, as one JSON document (`save-load.md §4`, §6).
/// </summary>
/// <remarks>
/// <para>
/// ⭐ <b>A snapshot, not a replay</b> (Joe, D507). A replay save is only as good as the sim it was
/// recorded against: every change that moves a golden would turn every old save into a different
/// village without a word. A snapshot is the village's state, so a balance change carries an old
/// village on under the new rules.
/// </para>
/// <para>
/// ⭐ <b>The config is not saved; it is rebuilt</b> from the share code over today's data
/// (<see cref="ConfigFor"/>), exactly as a founding builds it. Nothing is generated on load — the map,
/// the people and the buildings are all the file's.
/// </para>
/// </remarks>
public static class SaveGame
{
    /// <summary>
    /// The save format this build writes and reads. ⛔ Bump it when the shape of a save changes; a save
    /// of another format is refused in words, and there are no migrations before v1 (Joe, D507).
    /// </summary>
    public const int Format = 1;

    /// <summary>The whole of <paramref name="loop"/>'s village and its header, as a document.</summary>
    /// <exception cref="InvalidOperationException">The village faulted — there is nothing sound to save (§7).</exception>
    public static JsonObject Capture(SimLoop loop, string build, string savedAt, string shareCode)
    {
        ArgumentNullException.ThrowIfNull(loop);
        if (loop.Fault is not null)
        {
            throw new InvalidOperationException(
                $"The village stopped on an error at tick {loop.Fault.Tick}, so it cannot be saved.", loop.Fault);
        }

        return new JsonObject
        {
            ["format"] = Format,
            ["build"] = build,
            ["saved_at"] = savedAt,
            ["share_code"] = shareCode,
            ["village_name"] = loop.World.Config.VillageName,
            ["tick"] = SaveWriter.Hex(loop.World.Tick),
            ["world"] = loop.World.CaptureTheVillage(),
        };
    }

    /// <summary>The header alone — what the load list needs, and what decides whether to read on.</summary>
    /// <exception cref="SaveFormatException">The document is not a save.</exception>
    public static SaveHeader HeaderOf(JsonObject document)
    {
        ArgumentNullException.ThrowIfNull(document);
        var save = new SaveReader(document, "save", 0);
        return new SaveHeader(
            save.Int("format"),
            save.String("build"),
            save.String("saved_at"),
            save.String("share_code"),
            save.NullableString("village_name"),
            save.ULong("tick"));
    }

    /// <summary>
    /// The config a save's village runs on: today's <paramref name="data"/> with the save's new-game rows,
    /// seed and name — the founding's own door (<see cref="NewGame.Apply"/>).
    /// </summary>
    /// <exception cref="SaveDataException">A row the save chose is one today's data no longer allows.</exception>
    public static SimConfig ConfigFor(SimConfig data, SaveHeader header)
    {
        ArgumentNullException.ThrowIfNull(data);
        ArgumentNullException.ThrowIfNull(header);

        NewGameSettings defaults = NewGame.Defaults(data, string.Empty);
        if (!NewGame.TryRead(data, header.ShareCode, defaults, out NewGameSettings chosen, out string? refusal))
        {
            throw new SaveDataException("a new-game setting", $"The save's share code \"{header.ShareCode}\": {refusal}");
        }

        return NewGame.Apply(data, chosen with { VillageName = header.VillageName });
    }

    /// <summary>
    /// What the load list calls the village: the name the player gave it, or the seed's own — the same rule
    /// as <see cref="SimWorld.Name"/>, without building the world to ask.
    /// </summary>
    public static string VillageNameOf(SaveHeader header, SimConfig data)
    {
        ArgumentNullException.ThrowIfNull(header);
        ArgumentNullException.ThrowIfNull(data);

        if (header.VillageName?.Trim() is { Length: > 0 } typed)
        {
            return typed;
        }

        int mark = header.ShareCode.IndexOf(SeedText.SettingsMark, StringComparison.Ordinal);
        return SeedText.TryToSeed(mark < 0 ? header.ShareCode : header.ShareCode[..mark], out ulong seed, out _)
            && data.TownNames.Count > 0
            ? data.TownNames[(int)(seed % (ulong)data.TownNames.Count)]
            : "A village";
    }

    /// <summary>
    /// The village <paramref name="document"/> holds, built whole and driven by the founding's own systems
    /// in their order (D5) — or an exception and no village at all (§7: never half-loaded).
    /// </summary>
    /// <exception cref="SaveFormatException">The save is not one this build wrote.</exception>
    /// <exception cref="SaveDataException">It holds something <paramref name="config"/>'s data does not have.</exception>
    public static SimLoop Load(JsonObject document, SimConfig config, ISimLogger? logger = null)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(config);

        SaveHeader header = HeaderOf(document);
        if (header.Format != Format)
        {
            throw new SaveFormatException($"Format {header.Format}, where this build reads {Format}.");
        }

        if (document["world"] is not JsonObject village)
        {
            throw new SaveFormatException("save.world: missing.");
        }

        SimWorld world = SimWorld.Restore(config, village, logger);
        return new SimLoop(world, SimFactory.CreatePhase0Systems());
    }
}

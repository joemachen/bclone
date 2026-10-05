using System.Globalization;
using System.Text;
using Bclone.Sim.Config;
using Bclone.Sim.Determinism;
using Bclone.Sim.Logging;
using Bclone.Sim.Persistence;
using Bclone.Sim.World;
using Godot;

namespace Bclone.Game;

/// <summary>
/// ⭐ Save and load, the view's half (D507–D508, `specs/save-load.md §7–§8`): <b>the village keeps</b>.
/// </summary>
/// <remarks>
/// <para>
/// The sim does the saving (<see cref="SaveGame"/>, <see cref="SaveFile"/>); this file decides <em>when</em>
/// and <em>where</em>: an autosave at every Spring, Day 1 and on quit, three kept (Joe, D508); a named save
/// from Settings; and the new-game screen's <i>Continue</i> and <i>Load…</i> until the title screen exists.
/// </para>
/// <para>
/// ⛔ <b>The probe never touches the player's saves</b> — the same rule as their settings
/// (`settings-persistence.md §7`): under it the folder is <c>null</c>, nothing is listed, nothing is
/// autosaved, and the <c>save:</c> line works in a scratch folder it deletes.
/// </para>
/// </remarks>
public partial class Main
{
    /// <summary>How many autosaves a village keeps (Joe, D508). ⏸️ The player chooses the cadence in a later full settings screen.</summary>
    private const int AutosavesKept = 3;

    /// <summary>This village's folder of saves, or <c>null</c> under the probe — then nothing is saved.</summary>
    private string? _saveFolder;

    /// <summary>Where every village's saves live: <c>user://saves</c>, beside the settings file.</summary>
    private static string SavesRoot => ProjectSettings.GlobalizePath("user://saves");

    /// <summary>Every save on disk, newest first, as the new-game screen lists them — none under the probe.</summary>
    private static List<NewGameScreen.SaveListing> TheSavesOnDisk(SimConfig data)
    {
        var listed = new List<NewGameScreen.SaveListing>();
        if (TheProbeIsRunning || !System.IO.Directory.Exists(SavesRoot))
        {
            return listed;
        }

        IEnumerable<string> files = System.IO.Directory
            .EnumerateFiles(SavesRoot, "*" + SaveFile.Extension, System.IO.SearchOption.AllDirectories)
            .OrderByDescending(System.IO.File.GetLastWriteTimeUtc);
        foreach (string path in files)
        {
            SaveOpened read = SaveFile.ReadHeader(path);
            string stem = System.IO.Path.GetFileNameWithoutExtension(path);
            string line = read.Header is SaveHeader header
                ? $"{SaveGame.VillageNameOf(header, data)} — {SimClock.FromTick(header.Tick, data).SeasonAndYear()} · {stem} · {header.Build}"
                : $"{System.IO.Path.GetFileName(System.IO.Path.GetDirectoryName(path))} · {stem}";
            listed.Add(new NewGameScreen.SaveListing(path, line, read.Refusal));
            if (read.Detail is not null)
            {
                GD.Print($"[bclone] {read.Detail}");
            }
        }

        return listed;
    }

    /// <summary>
    /// ⭐ THE ONE DOOR A VILLAGE COMES IN BY FROM DISK. Opened whole on today's data, or refused in words on
    /// the new-game screen, which stays (§7: never a half-loaded village).
    /// </summary>
    private void OpenTheVillage(SimConfig data, string path)
    {
        CompositeLogSink sinks = OpenTheLogs();
        SaveOpened opened = SaveFile.Open(path, data, sinks);
        if (opened.Loop is null)
        {
            // ⚠️ Never swallowed (METHODOLOGY §4): the sentence on the screen, the detail in the audit file.
            _audit.Log(0UL, LogLevel.Error, "shell", $"{opened.Refusal} ({opened.Detail})");
            GD.PushError($"[bclone] {opened.Refusal} ({opened.Detail})");
            _newGame?.RefuseTheLoad(opened.Refusal ?? "That save could not be opened.");
            return;
        }

        _loop = opened.Loop;
        _shareCode = opened.Header!.ShareCode;
        CloseTheNewGameScreen();
        _loop.World.Log(LogLevel.Info, "shell", $"Opened {path}, saved by build {opened.Header.Build} at {opened.Header.SavedAt}: {_shareCode}.");
        TellTheLogAboutTheSettings();
        StartTheVillage(sinks);
    }

    /// <summary>This village's folder: its name and its seed, so two Ashfords are two folders (§7).</summary>
    private void BeginSaving() =>
        _saveFolder = TheProbeIsRunning ? null : System.IO.Path.Combine(SavesRoot, FolderFor(_loop.World.Name, _shareCode));

    /// <summary>A folder name from the village's name and its seed's words — lower case, letters and digits and dashes.</summary>
    internal static string FolderFor(string villageName, string shareCode)
    {
        int mark = shareCode.IndexOf(SeedText.SettingsMark, StringComparison.Ordinal);
        string seed = mark < 0 ? shareCode : shareCode[..mark];
        return $"{Slug(villageName)}-{Slug(seed)}".Trim('-');
    }

    private static string Slug(string text)
    {
        var slug = new StringBuilder();
        foreach (char c in text.Trim().ToLowerInvariant())
        {
            slug.Append(char.IsAsciiLetterOrDigit(c) ? c : '-');
        }

        string once = slug.ToString();
        while (once.Contains("--", StringComparison.Ordinal))
        {
            once = once.Replace("--", "-", StringComparison.Ordinal);
        }

        return once.Trim('-');
    }

    /// <summary>The village as a save, stamped with this build and the real time — the view's, never the sim's.</summary>
    private System.Text.Json.Nodes.JsonObject CaptureTheVillage() =>
        SaveGame.Capture(_loop, BuildVersion, DateTime.Now.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture), _shareCode);

    /// <summary>
    /// Step <paramref name="ticks"/>, stopping at the turn of a year to autosave — <b>after the tick that turns
    /// it, before the next</b> (§7), however many ticks a frame carries at 10×.
    /// </summary>
    private void StepAndAutosave(int ticks)
    {
        int perYear = _loop.World.Config.TicksPerYear;
        while (ticks > 0)
        {
            int toTheTurn = perYear - (int)(_loop.World.Tick % (ulong)perYear);
            int now = Math.Min(ticks, toTheTurn);
            _loop.Step(now);
            ticks -= now;
            if (_loop.World.Tick % (ulong)perYear == 0)
            {
                Autosave($"Spring, Year {_loop.World.Clock.Year}");
            }
        }
    }

    /// <summary>An autosave, rotated (§7). A disk that says no is said in the log; the older autosaves stand.</summary>
    private void Autosave(string when)
    {
        if (_saveFolder is null || _halted)
        {
            return;
        }

        try
        {
            string written = SaveFile.WriteAutosave(_saveFolder, CaptureTheVillage(), AutosavesKept);
            _loop.World.Log(LogLevel.Info, "shell", $"Autosaved ({when}) to {written}.");
        }
        catch (Exception ex) when (ex is System.IO.IOException or UnauthorizedAccessException)
        {
            CouldNotSave(ex);
        }
    }

    /// <summary>
    /// A named save from Settings — or, on a village that stopped on an error, the refusal (§7): there is
    /// nothing sound to save, and the last autosave is the way back.
    /// </summary>
    private void SaveUnderAName(string name)
    {
        if (_halted)
        {
            SimClock last = SimClock.FromTick(_loop.World.Tick - (_loop.World.Tick % (ulong)_loop.World.Config.TicksPerYear), _loop.World.Config);
            SayInTheLog($"The village stopped on an error, so it can't be saved. The last autosave is from Year {last.Year}.");
            return;
        }

        if (_saveFolder is null)
        {
            SayInTheLog("Nothing is saved while the probe runs.");
            return;
        }

        string stem = Slug(name);
        if (stem.Length == 0)
        {
            stem = $"year-{_loop.World.Clock.Year}";
        }

        string path = System.IO.Path.Combine(_saveFolder, stem + SaveFile.Extension);
        try
        {
            SaveFile.Write(path, CaptureTheVillage());
            _loop.World.Log(LogLevel.Info, "shell", $"Saved as {stem} — {path}.");
        }
        catch (Exception ex) when (ex is System.IO.IOException or UnauthorizedAccessException)
        {
            CouldNotSave(ex);
        }
    }

    private void CouldNotSave(Exception ex)
    {
        string sentence = $"The village could not be saved ({ex.Message}). The last save still stands.";
        SayInTheLog(sentence);
        _audit.Log(_loop.World.Tick, LogLevel.Error, "shell", $"{sentence} {ex}");
        GD.PushError($"[bclone] {sentence}");
    }

    /// <summary>⭐ Quitting writes the autosave (§7) — a village is never lost to the window's ✕.</summary>
    public override void _Notification(int what)
    {
        if (what == NotificationWMCloseRequest && _newGame is null && _loop is not null)
        {
            Autosave("on quit");
        }
    }

    /// <summary>
    /// The Settings rows under <i>How the village runs</i>: a name and <i>Save</i>, until the pause screen
    /// exists (Joe, D508).
    /// </summary>
    private void AddTheSaveRow(VBoxContainer body)
    {
        var row = new HBoxContainer();
        var name = new LineEdit
        {
            PlaceholderText = $"a name, or year-{_loop.World.Clock.Year}",
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
        };
        name.AddThemeFontSizeOverride("font_size", 12);
        row.AddChild(name);
        var save = new Button { Text = "Save", TooltipText = "Save the village under this name" };
        save.AddThemeFontSizeOverride("font_size", 12);
        save.Pressed += () => SaveUnderAName(name.Text);
        row.AddChild(save);
        body.AddChild(row);
        body.AddChild(Caption($"It also saves itself every spring and when you quit — the last {AutosavesKept} are kept."));
    }

    // ---------------------------------------------------------------
    //  The probe's `save:` line (§9)
    // ---------------------------------------------------------------

    /// <summary>
    /// ⭐ The village saved to a scratch folder, opened again through the game's own door, and compared:
    /// the hash, the tick, and the list line that would offer it. Never the player's folder (§9).
    /// </summary>
    private string TheVillageSavesAndLoads()
    {
        string scratch = ProjectSettings.GlobalizePath("user://probe-saves");
        try
        {
            string path = System.IO.Path.Combine(scratch, "probe" + SaveFile.Extension);
            SaveFile.Write(path, CaptureTheVillage());
            SaveOpened opened = SaveFile.Open(path, _loop.World.Config);
            if (opened.Loop is null)
            {
                return $"[widths] save: ❌ the probe's own save was refused — {opened.Refusal} ({opened.Detail})";
            }

            ulong live = StateHash.Compute(_loop.World);
            ulong loaded = StateHash.Compute(opened.Loop.World);
            SaveOpened header = SaveFile.ReadHeader(path);
            long bytes = new System.IO.FileInfo(path).Length;
            bool ok = live == loaded && opened.Loop.World.Tick == _loop.World.Tick && header.Header?.Build == BuildVersion;
            return ok
                ? $"[widths] save: ✅ tick {_loop.World.Tick:N0} saved ({bytes / 1024} KB) and opened again, hash {loaded}; the list reads it as built by {BuildVersion}"
                : $"[widths] save: ❌ saved hash {live} tick {_loop.World.Tick}, opened hash {loaded} tick {opened.Loop.World.Tick}, build {header.Header?.Build}";
        }
        finally
        {
            if (System.IO.Directory.Exists(scratch))
            {
                System.IO.Directory.Delete(scratch, recursive: true);
            }
        }
    }
}

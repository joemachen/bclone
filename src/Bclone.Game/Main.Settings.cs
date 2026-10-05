using Bclone.Sim.Config;
using Bclone.Sim.Logging;
using Godot;

namespace Bclone.Game;

/// <summary>
/// ⭐ Settings persistence (D504, `specs/settings-persistence.md`): <b>what the player set stays set</b>
/// — the UI size, Routes, Paths and every <i>On the map</i> tick, each window's tick, fold and dragged
/// place, and the new-game screen's rows.
/// </summary>
/// <remarks>
/// <para>
/// ⛔ <b>A preference, never the village's state</b> (§3): nothing here is read by the sim or hashed.
/// <i>Share the work out</i> is hashed and is deliberately not here — it travels with save/load.
/// </para>
/// <para>
/// ⛔ <b>The probe never reads the player's file</b> (§7): it asserts the defaults, and Joe's own UI size
/// would move <c>bar height</c> on his machine and nobody else's.
/// </para>
/// </remarks>
public partial class Main
{
    /// <summary>The player's settings file, or <c>null</c> under the probe — then nothing is read or written.</summary>
    private string? _settingsPath;

    /// <summary>The settings as last read or written — the base every capture starts from, so a key this build does not draw is kept.</summary>
    private PlayerSettings _settings = new();

    /// <summary>What was wrong with the file, held until the village's log exists to say so.</summary>
    private readonly List<PlayerSettingsProblem> _settingsProblems = new();

    /// <summary>Every window with a title — the ones the player can fold, drag and (bar Settings) switch off.</summary>
    private readonly List<TitledPanel> _titled = new();

    /// <summary>Where the player dragged each window. ⭐ Written only by a drag or the file — never by the code that moves windows (D340).</summary>
    private readonly Dictionary<PanelContainer, WindowPlace> _playerPlaces = new();

    /// <summary>Places that wait for their window to be shown with a size, because a hidden panel has none (`KeepWindowsOnScreen`).</summary>
    private readonly HashSet<PanelContainer> _placeWhenShown = new();

    /// <summary>Something the player set has changed; written once, at the end of the frame.</summary>
    private bool _settingsDirty;

    /// <summary>Settings are being applied: the ticks they set are not the player changing anything.</summary>
    private bool _applyingSettings;

    /// <summary>The village is built and its settings applied — before this, building the panels is not a change.</summary>
    private bool _settingsLive;

    /// <summary>The UI size as the dial has it, a whole percent — <see cref="_uiScale"/> is this over a hundred.</summary>
    private int _uiScalePercent = PlayerSettings.DefaultUiScalePercent;

    private Label _scaleReading = null!;

    /// <summary>A window with a title: its fold, the side it starts on, and what it is called in the file.</summary>
    private sealed record TitledPanel(string Title, PanelContainer Panel, Button Header, bool Right);

    /// <summary>The id the Paths overlay is remembered by, beside the map's ticks.</summary>
    private const string PathsToggle = "paths";

    private static bool TheProbeIsRunning => System.Environment.GetEnvironmentVariable("BCLONE_PROBE_WIDTHS") is not null;

    /// <summary>Read the player's settings before anything is built — the UI size and the new-game rows are needed first.</summary>
    private void ReadTheSettings()
    {
        _settingsPath = TheProbeIsRunning ? null : ProjectSettings.GlobalizePath("user://settings.json");
        if (_settingsPath is not null)
        {
            _settings = PlayerSettingsFile.Load(_settingsPath, _settingsProblems);
        }

        foreach (PlayerSettingsProblem problem in _settingsProblems)
        {
            if (problem.Level >= LogLevel.Error)
            {
                GD.PushError($"[bclone] {problem.Sentence}");
            }
            else
            {
                GD.PushWarning($"[bclone] {problem.Sentence}");
            }
        }

        SetUiScale(_settings.UiScalePercent ?? PlayerSettings.DefaultUiScalePercent);
    }

    /// <summary>Into the audit log, once it exists: where the settings came from, and anything wrong with them.</summary>
    private void TellTheLogAboutTheSettings()
    {
        _loop.World.Log(LogLevel.Info, "shell", _settingsPath is null
            ? "Settings: not read or written (the probe runs on the defaults)."
            : $"Settings read from {_settingsPath}.");

        foreach (PlayerSettingsProblem problem in _settingsProblems)
        {
            _loop.World.Log(problem.Level, "shell", problem.Sentence);
        }

        _settingsProblems.Clear();
    }

    /// <summary>The new-game screen opens on these next time: the rows of the valley just founded, never its seed or name.</summary>
    private void RememberTheValley(IReadOnlyDictionary<string, string> rows)
    {
        _settings = _settings with { NewGameRows = new Dictionary<string, string>(rows, StringComparer.Ordinal) };
        WriteTheSettings(_settings);
    }

    /// <summary>The dial, whatever moved it: brought onto its steps, drawn, and said beside it.</summary>
    private void SetUiScale(int percent)
    {
        _uiScalePercent = PlayerSettings.OnTheDial(percent);
        _uiScale = _uiScalePercent / 100f;
        if (_scaleReading is not null)
        {
            _scaleReading.Text = $"{_uiScalePercent}%";
        }
    }

    /// <summary>
    /// Put <paramref name="settings"/> on screen. Absent ticks keep what the panels were built with; the
    /// scale, Routes and Paths go to their defaults.
    /// </summary>
    /// <remarks>
    /// ⭐ <b>Every tick is set through <c>ButtonPressed</c></b>, so its own handler moves the map or the
    /// fold — the tick and the thing it controls cannot come apart here (D380). ⚠️ A place is not
    /// applied here: it waits for <see cref="PlaceWhatThePlayerPlaced"/>, because a window has no
    /// settled size until it has been laid out while shown.
    /// </remarks>
    private void ApplySettings(PlayerSettings settings)
    {
        _applyingSettings = true;
        try
        {
            SetUiScale(settings.UiScalePercent ?? PlayerSettings.DefaultUiScalePercent);

            int routes = settings.Routes is string level
                ? Math.Max(0, IndexOf(PlayerSettings.RoutesLevels, level))
                : (int)MapDetail.Selected;
            _detail = (MapDetail)routes;
            ShowTheDetail();

            _map.ShowWear(settings.Toggles.TryGetValue(PathsToggle, out bool paths) && paths);
            RefreshWearButton();

            for (int i = 0; i < _mapToggles.Count; i++)
            {
                (string key, _, CheckBox box, _) = _mapToggles[i];
                if (settings.Toggles.TryGetValue(key, out bool on))
                {
                    box.ButtonPressed = on;
                }
            }

            _playerPlaces.Clear();
            _placeWhenShown.Clear();
            for (int i = 0; i < _titled.Count; i++)
            {
                TitledPanel titled = _titled[i];
                if (!settings.Windows.TryGetValue(titled.Title, out WindowPrefs? prefs))
                {
                    continue;
                }

                if (prefs.Shown is bool shown && WindowOf(titled.Panel) is ShellWindow window && !GatedByTheVillage(titled.Panel))
                {
                    window.Wanted = shown;

                    // What's here also answers to the selection, which `Refresh` reads every frame.
                    if (titled.Panel != _whatsHerePanel)
                    {
                        titled.Panel.Visible = shown;
                    }
                }

                if (prefs.Open is bool open)
                {
                    titled.Header.ButtonPressed = open;
                }

                if (prefs.Place is WindowPlace place)
                {
                    _playerPlaces[titled.Panel] = place;
                    _placeWhenShown.Add(titled.Panel);
                }
            }

            ThePlacedAreTheDragged();
        }
        finally
        {
            _applyingSettings = false;
        }
    }

    /// <summary>
    /// ⚠️ The Tree is the village's to show: it opens on the first unlock (B2), so a remembered tick
    /// cannot show it before then — that would be the spoiler B7 removed.
    /// </summary>
    private bool GatedByTheVillage(PanelContainer panel) => panel == _treePanel && !_loop.World.ShownTheTechTree;

    /// <summary>
    /// A window with a remembered place is one the player dragged: Settings stops centring itself and
    /// <i>What's here</i> stops opening by its tile. Asked again after the launch arrangement, which
    /// clears <see cref="_whatsHereWasDragged"/>.
    /// </summary>
    private void ThePlacedAreTheDragged()
    {
        _settingsWasDragged |= _playerPlaces.ContainsKey(_settingsPanel);
        _whatsHereWasDragged |= _playerPlaces.ContainsKey(_whatsHerePanel);
    }

    /// <summary>Put each remembered window where the player left it, on the first frame it is shown with a size.</summary>
    /// <remarks>Walks only the waiting list, which is empty after the first frames of a village.</remarks>
    private void PlaceWhatThePlayerPlaced()
    {
        if (_placeWhenShown.Count == 0)
        {
            return;
        }

        foreach (PanelContainer panel in _placeWhenShown.ToList())
        {
            if (!panel.Visible || panel.Size.Y <= 0f)
            {
                continue;
            }

            PutWhereThePlayerPut(panel, _playerPlaces[panel]);
            _placeWhenShown.Remove(panel);
        }
    }

    /// <summary>
    /// A place back to a drawn corner — measured from the window's own side, so a right-hand window stays
    /// on the right in a wider game (§4) — and moved there through <see cref="MovePanel"/>, which clamps.
    /// </summary>
    private void PutWhereThePlayerPut(PanelContainer panel, WindowPlace place)
    {
        float wide = panel.Size.X * _uiScale;
        var wanted = new Vector2(
            place.Side == PlayerSettings.RightSide ? Size.X - place.X - wide : place.X,
            place.Y);

        MovePanel(panel, wanted - DrawnTopLeft(panel));
    }

    /// <summary>Where a window is drawn, as the file says it: whole pixels in from its own side and down from the top.</summary>
    private WindowPlace PlaceOf(TitledPanel titled)
    {
        Vector2 corner = DrawnTopLeft(titled.Panel);
        float wide = titled.Panel.Size.X * _uiScale;

        return titled.Right
            ? new WindowPlace(PlayerSettings.RightSide, Mathf.RoundToInt(Size.X - corner.X - wide), Mathf.RoundToInt(corner.Y))
            : new WindowPlace(PlayerSettings.LeftSide, Mathf.RoundToInt(corner.X), Mathf.RoundToInt(corner.Y));
    }

    /// <summary>⭐ The end of a drag — the gesture, and the only thing that writes a place (D340).</summary>
    private void ThePlayerPlaced(PanelContainer panel)
    {
        TitledPanel? titled = _titled.Find(t => t.Panel == panel);

        // A card is dragged by the same grip and is not a window: it belongs to its selection.
        if (titled is null)
        {
            return;
        }

        _playerPlaces[panel] = PlaceOf(titled);
        _placeWhenShown.Remove(panel);
        SettingsChanged();
    }

    /// <summary>Settings → <i>Reset window positions</i>: the default arrangement, and every remembered place forgotten.</summary>
    private void ResetWindowPositions()
    {
        _playerPlaces.Clear();
        _placeWhenShown.Clear();
        _settingsWasDragged = false;
        ArrangeDefaults();
        SettingsChanged();
    }

    /// <summary>What is on screen now, as the file will say it.</summary>
    private PlayerSettings CaptureSettings()
    {
        var toggles = new Dictionary<string, bool>(_settings.Toggles, StringComparer.Ordinal);
        for (int i = 0; i < _mapToggles.Count; i++)
        {
            (string key, _, CheckBox box, _) = _mapToggles[i];
            toggles[key] = box.ButtonPressed;
        }

        toggles[PathsToggle] = _map.WearShown;

        var windows = new Dictionary<string, WindowPrefs>(_settings.Windows, StringComparer.Ordinal);
        for (int i = 0; i < _titled.Count; i++)
        {
            TitledPanel titled = _titled[i];
            windows[titled.Title] = new WindowPrefs(
                WindowOf(titled.Panel)?.Wanted,
                titled.Header.ButtonPressed,
                _playerPlaces.TryGetValue(titled.Panel, out WindowPlace? place) ? place : null);
        }

        return _settings with
        {
            UiScalePercent = _uiScalePercent,
            Routes = PlayerSettings.RoutesLevels[(int)_detail],
            Toggles = toggles,
            Windows = windows,
        };
    }

    /// <summary>The player changed something: written at the end of the frame, once however many ticks moved.</summary>
    private void SettingsChanged()
    {
        _settingsDirty |= _settingsLive && !_applyingSettings;
    }

    /// <summary>Called every frame; writes only when something changed.</summary>
    private void WriteTheSettingsIfChanged()
    {
        if (!_settingsDirty)
        {
            return;
        }

        _settingsDirty = false;
        _settings = CaptureSettings();
        WriteTheSettings(_settings);
    }

    /// <summary>To disk, whole or not at all; a refusal is said and the game carries on (§6).</summary>
    private void WriteTheSettings(PlayerSettings settings)
    {
        if (_settingsPath is null)
        {
            return;
        }

        try
        {
            PlayerSettingsFile.Save(_settingsPath, settings);
        }
        catch (Exception ex) when (ex is System.IO.IOException or UnauthorizedAccessException)
        {
            // Never swallowed (METHODOLOGY §4): said twice, and a preference is not worth halting a village for.
            string sentence = $"The settings could not be written to {_settingsPath} ({ex.Message}); they hold for this session only.";
            GD.PushError($"[bclone] {sentence}");
            _loop.World.Log(LogLevel.Warn, "shell", sentence);
        }
    }

    private static int IndexOf(IReadOnlyList<string> list, string value)
    {
        for (int i = 0; i < list.Count; i++)
        {
            if (list[i] == value)
            {
                return i;
            }
        }

        return -1;
    }

    /// <summary>
    /// ⭐ Every setting goes out and comes back as it went — <b>a probe line</b> (§8, view guards 1–2).
    /// </summary>
    /// <remarks>
    /// <para>
    /// Posed twice. First everything but places: every tick flipped, the dial at 100, Routes on all,
    /// every window the player can switch flipped and every titled window rolled up — through a scratch
    /// file, applied, captured back. Then places: every window on screen moved, on both sides, at 90 %,
    /// and read back off where it is actually drawn, to the pixel. <b>What this catches is a tick or a
    /// window that apply or capture forgets</b>, and place arithmetic that does not invert.
    /// </para>
    /// <para>
    /// Finally the founding's own capture is applied and must come back, and the windows are arranged
    /// again — the probe ends where it started.
    /// </para>
    /// </remarks>
    private string TheSettingsComeBackAsTheyWent()
    {
        var faults = new List<string>();
        if (_settingsPath is not null)
        {
            faults.Add($"the probe read the player's settings ({_settingsPath})");
        }

        PlayerSettings founding = CaptureSettings();

        // ⛔⛔ THE POSE COMES FROM THE REGISTRIES, NEVER FROM THE CAPTURE (D419's trap, paid again in
        // D505). The first draft flipped `founding.Toggles` — so a tick the capture forgot was missing
        // from the pose too, and the round trip agreed with itself: that mutant scored ZERO. The truth
        // is what the screen has (`_mapToggles`, `_titled`); the claim is what the capture says.
        foreach ((string key, _, _, _) in _mapToggles)
        {
            if (!founding.Toggles.ContainsKey(key))
            {
                faults.Add($"the capture forgets the {key} tick");
            }
        }

        if (!founding.Toggles.ContainsKey(PathsToggle))
        {
            faults.Add($"the capture forgets the {PathsToggle} overlay");
        }

        foreach (TitledPanel titled in _titled)
        {
            if (!founding.Windows.ContainsKey(titled.Title))
            {
                faults.Add($"the capture forgets the {titled.Title} window");
            }
        }

        // ---- everything but places ----
        var toggles = new Dictionary<string, bool>(StringComparer.Ordinal) { [PathsToggle] = !_map.WearShown };
        foreach ((string key, _, CheckBox box, _) in _mapToggles)
        {
            toggles[key] = !box.ButtonPressed;
        }

        var windows = new Dictionary<string, WindowPrefs>(StringComparer.Ordinal);
        foreach (TitledPanel titled in _titled)
        {
            bool? shown = WindowOf(titled.Panel) is ShellWindow window
                ? (GatedByTheVillage(titled.Panel) ? window.Wanted : !window.Wanted)
                : null;
            windows[titled.Title] = new WindowPrefs(shown, false, null);
        }

        PlayerSettings posed = founding with
        {
            UiScalePercent = 100,
            Routes = PlayerSettings.RoutesLevels[^1],
            Toggles = toggles,
            Windows = windows,
        };

        string scratch = ProjectSettings.GlobalizePath("user://probe-settings.json");
        var problems = new List<PlayerSettingsProblem>();
        PlayerSettingsFile.Save(scratch, posed);
        PlayerSettings read = PlayerSettingsFile.Load(scratch, problems);
        System.IO.File.Delete(scratch);

        faults.AddRange(problems.Select(p => $"the scratch file said: {p.Sentence}"));
        Compare("the file", posed, read, faults);

        ApplySettings(read);
        Compare("on screen", posed, CaptureSettings(), faults);

        // ---- places, on both sides, read back off the screen ----
        ApplySettings(founding);
        var placed = new Dictionary<string, WindowPrefs>(founding.Windows, StringComparer.Ordinal);
        var onScreen = _titled.Where(t => t.Panel.Visible && t.Panel.Size.Y > 0f).ToList();
        for (int i = 0; i < onScreen.Count; i++)
        {
            TitledPanel titled = onScreen[i];
            var place = new WindowPlace(titled.Right ? PlayerSettings.RightSide : PlayerSettings.LeftSide, 40 + (i * 10), 120 + (i * 20));
            placed[titled.Title] = placed.TryGetValue(titled.Title, out WindowPrefs? was)
                ? was with { Place = place }
                : new WindowPrefs(null, null, place);
        }

        // ⚠️ At 90, not 100: at 100 a drawn width IS the laid-out width, and a place that forgot the
        // scale would come back right anyway. Measured: that mutant scored zero at 100.
        PlayerSettings places = founding with { UiScalePercent = 90, Windows = placed };
        ApplySettings(places);
        PlaceWhatThePlayerPlaced();
        Compare("placed", places, CaptureSettings(), faults);

        foreach (TitledPanel titled in onScreen)
        {
            WindowPlace wanted = places.Windows[titled.Title].Place!;
            WindowPlace drawn = PlaceOf(titled);
            if (drawn.Side != wanted.Side || Math.Abs(drawn.X - wanted.X) > 1 || Math.Abs(drawn.Y - wanted.Y) > 1)
            {
                faults.Add($"{titled.Title} was put at {wanted} and is drawn at {drawn}");
            }
        }

        if (_placeWhenShown.Count > 0)
        {
            faults.Add($"{_placeWhenShown.Count} shown window(s) still waiting to be placed");
        }

        // ---- and back ----
        ApplySettings(founding);
        Compare("restored", founding, CaptureSettings(), faults);
        ArrangeDefaults();

        return faults.Count == 0
            ? $"[widths] settings: ✅ the player's file not read; {toggles.Count} toggles, {windows.Count} windows, the dial and "
                + $"Routes through a file and back; {onScreen.Count} windows placed on both sides and drawn where they were put"
            : $"[widths] settings: ⛔ {string.Join("; ", faults)}";

        static void Compare(string where, PlayerSettings wanted, PlayerSettings got, List<string> faults)
        {
            string[] want = wanted.ToJson().Split('\n');
            string[] have = got.ToJson().Split('\n');
            if (want.SequenceEqual(have))
            {
                return;
            }

            // Named by the object the first difference is in, so *"open": false* says which window.
            string first = $"{want.Length} lines ≠ {have.Length}";
            string within = string.Empty;
            for (int i = 0; i < Math.Min(want.Length, have.Length); i++)
            {
                if (want[i] != have[i])
                {
                    first = $"{within}{want[i].Trim()} ≠ {have[i].Trim()}";
                    break;
                }

                if (want[i].TrimEnd().EndsWith('{'))
                {
                    within = want[i].Trim().TrimEnd('{').Trim() + " ";
                }
            }

            faults.Add($"{where}: {first}");
        }
    }
}

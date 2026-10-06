using Bclone.Sim.World;
using Godot;

namespace Bclone.Game;

/// <summary>
/// ⭐ The pause screen's wiring (D516, `specs/title-and-pause.md §5`): Esc's third step, the speed it keeps, and
/// the rows — each one a door that already exists in <c>Main.Saves.cs</c>.
/// </summary>
public partial class Main
{
    /// <summary>The pause screen while it is up; <c>null</c> otherwise. Built fresh each time, so Load… lists today's saves.</summary>
    private PauseScreen? _pause;

    /// <summary>The speed the pause screen found, given back on <i>Resume</i> — 4× comes back 4×, a Space-pause comes back paused.</summary>
    private double _speedBeforeThePause;

    private bool ThePauseIsUp => _pause is not null;

    /// <summary>For the probe: the pause screen's Save button.</summary>
    private Button? _pauseSave;

    /// <summary>For the probe: the line under Save that says what happened (D518).</summary>
    private Label? _pauseSaid;

    /// <summary>Stop the village and put the pause screen over it.</summary>
    private void OpenThePause()
    {
        _speedBeforeThePause = _driver.SpeedMultiplier;
        SetSpeed(0.0);

        _pause = new PauseScreen();
        AddChild(_pause);
        _pause.DrawAt(_uiScale);
        _pause.Where.Text = $"{_loop.World.Name} — {_loop.World.Clock}";

        // ⚠️ On a village stopped by an error, nothing is saved (D364, `save-load.md §7`): the quits say so.
        string? notSaved = null;
        if (_halted)
        {
            SimClock last = SimClock.FromTick(
                _loop.World.Tick - (_loop.World.Tick % (ulong)_loop.World.Config.TicksPerYear), _loop.World.Config);
            notSaved = $"The village stopped on an error and will not be saved — the last autosave is from Year {last.Year}.";
        }

        _pause.AddButton("Resume", () => CloseThePause(resume: true));
        _pause.AddGap();
        var load = new TitleScreen.LoadMenu(TheSavesOnDisk(_loop.World.Config), LoadFromThePause);
        (_pauseSave, _pauseSaid) = AddTheSaveRow(_pause.Body, () => load.Fill(TheSavesOnDisk(_loop.World.Config)));
        _pause.Body.AddChild(load.Button);
        _pause.AddButton("Settings", OpenSettingsFromThePause);
        _pause.AddGap();
        _pause.AddButton("Quit to title", QuitToTheTitle, notSaved ?? "Saves the village first, then the title.");
        _pause.AddButton("Quit to desktop", QuitToTheDesktop, notSaved ?? "Saves the village first, then closes the game.");
    }

    /// <summary>Take the pause screen down — giving back the speed it found, or leaving the village paused.</summary>
    private void CloseThePause(bool resume)
    {
        if (_pause is null)
        {
            return;
        }

        RemoveChild(_pause);
        _pause.QueueFree();
        _pause = null;
        if (resume)
        {
            SetSpeed(_speedBeforeThePause);
        }
    }

    /// <summary>
    /// ⚠️ <i>Settings</i> leaves the village paused (§5): the player went to Settings, not back to the village.
    /// Space or a speed key resumes, as always.
    /// </summary>
    private void OpenSettingsFromThePause()
    {
        CloseThePause(resume: false);
        if (!_settingsPanel.Visible)
        {
            ToggleSettings();
        }
    }

    /// <summary>
    /// The keyboard while the pause screen is up: <b>Esc resumes, and nothing else does anything</b> — no speed keys,
    /// no Tab, no brush. A key that reached the village behind the dim would be a change the player cannot see.
    /// </summary>
    private void KeyOnThePause(Key key)
    {
        if (key == Key.Escape)
        {
            CloseThePause(resume: true);
        }
    }

    // ---------------------------------------------------------------
    //  The probe's `pause:` line (`title-and-pause.md §8`)
    // ---------------------------------------------------------------

    /// <summary>
    /// ⭐ Esc's third step, through the game's own key path: with nothing in hand and <i>What's here</i> closed, Esc
    /// opens the pause screen and the village stops; Esc again closes it and gives back the speed it found (posed at
    /// 4×). And <i>Save</i> has moved: on the pause screen, not in Settings. Never presses Save, Load or a quit.
    /// </summary>
    private string ThePauseOpensAndCloses()
    {
        var faults = new List<string>();
        double speedBefore = _driver.SpeedMultiplier;
        if (_map.IsPlacing)
        {
            _map.PutTheToolDown();
        }

        if (_whatsHerePanel.Visible)
        {
            CloseTheWindow(_whatsHerePanel);
        }

        SetSpeed(4.0);
        PressAKey(Key.Escape);
        if (!ThePauseIsUp)
        {
            faults.Add("Esc with nothing open did not open the pause screen");
        }
        else
        {
            if (!_driver.IsPaused)
            {
                faults.Add($"the village runs at {_driver.SpeedMultiplier}x behind the pause screen");
            }

            if (!HasASaveButton(_pause!.Body))
            {
                faults.Add("the pause screen has no Save");
            }

            // ⭐ D518: pressing Save says what happened, on the pause screen. Safe under the probe — nothing is
            // saved, and that refusal is the sentence the line must show.
            _pauseSave?.EmitSignal(BaseButton.SignalName.Pressed);
            if (_pauseSaid is not { Visible: true } said || said.Text != "Nothing is saved while the probe runs.")
            {
                faults.Add($"Save said nothing on the pause screen (\"{_pauseSaid?.Text}\")");
            }

            PressAKey(Key.Key4);
            if (!_driver.IsPaused)
            {
                faults.Add("a speed key reached the village behind the pause screen");
            }

            PressAKey(Key.Escape);
            if (ThePauseIsUp)
            {
                faults.Add("Esc did not close the pause screen");
            }
            else if (_driver.SpeedMultiplier != 4.0)
            {
                faults.Add($"Resume gave back {_driver.SpeedMultiplier}x, not the 4x it found");
            }
        }

        if (HasASaveButton(_settingsPanel))
        {
            faults.Add("Settings still has a Save button");
        }

        SetSpeed(speedBefore);
        return faults.Count == 0
            ? "[widths] pause: ✅ Esc with nothing open pauses under the dim, a speed key does nothing, Esc resumes at the 4x it found; Save is on the pause screen, says what it did, and is not in Settings"
            : $"[widths] pause: ❌ {string.Join("; ", faults)}";

        void PressAKey(Key key) => _UnhandledKeyInput(new InputEventKey { Keycode = key, Pressed = true });
    }

    private static bool HasASaveButton(Node under)
    {
        foreach (Node child in under.GetChildren())
        {
            if ((child is Button { Text: "Save" }) || HasASaveButton(child))
            {
                return true;
            }
        }

        return false;
    }
}

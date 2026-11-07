using Bclone.Sim.Core;
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
    /// ⭐ Esc's order, through the game's own key path: a moment up takes Esc first — it goes and the pause does
    /// not come; then, with nothing in hand and <i>What's here</i> closed, Esc opens the pause screen and the village
    /// stops; Esc again closes it and gives back the speed it found (posed at 4×). And <i>Save</i> has moved: on the
    /// pause screen, not in Settings. Never presses Save, Load or a quit.
    /// </summary>
    /// <remarks>
    /// ⛔ <b>THE MOMENT IS POSED, NOT LEFT TO THE RUN (D585).</b> The late <c>Refresh</c> shows whatever waiting moment
    /// the twelve-year run raised — from D567 on, the re-founders of the probe's own emptied valley — and this line
    /// read ❌ for seventeen commits because its Esc dismissed that moment, correctly, instead of pausing. A pose
    /// that depends on what the run happened to raise is a check that depends on the seed.
    /// </remarks>
    private string ThePauseOpensAndCloses()
    {
        var faults = new List<string>();
        SimWorld world = _loop.World;
        double speedBefore = _driver.SpeedMultiplier;

        // Every waiting moment the run raised goes by first, as a player pressing *Go on* would — there can be more
        // than one queued (the re-founders' came twice) — so the pose below is the only moment there is.
        for (int i = 0; i < 100 && _momentPanel is { Visible: true }; i++)
        {
            DismissTheMoment();
            ShowAnyMoment(world);
        }

        var posed = new Moment { Title = "Posed for the pause", Body = "posed by the probe", WaitsToBeDismissed = true };
        world.Moments.Add(posed);
        ShowAnyMoment(world);
        if (_momentPanel is not { Visible: true } || _momentTitle.Text != posed.Title)
        {
            faults.Add($"the posed moment did not show ({WhatIsOpen()})");
        }
        else
        {
            PressAKey(Key.Escape);
            if (_momentPanel.Visible)
            {
                faults.Add("Esc over a moment did not dismiss it");
            }

            if (ThePauseIsUp)
            {
                faults.Add("Esc paused over a moment instead of dismissing it");
                CloseThePause(resume: false);
            }
        }

        world.Moments.Remove(posed);
        _celebrated.Remove(posed.LogLine);

        if (_map.IsPlacing)
        {
            _map.PutTheToolDown();
        }

        if (_whatsHerePanel.Visible)
        {
            CloseTheWindow(_whatsHerePanel);
        }

        SetSpeed(4.0);
        string open = WhatIsOpen();
        PressAKey(Key.Escape);
        if (!ThePauseIsUp)
        {
            faults.Add($"Esc did not open the pause screen ({open})");
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
            PressAKey(Key.Key5);
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
            ? "[widths] pause: ✅ Esc over a moment dismisses it and does not pause; with nothing open Esc pauses under the dim, a speed key does nothing, Esc resumes at the 4x it found; Save is on the pause screen, says what it did, and is not in Settings"
            : $"[widths] pause: ❌ {string.Join("; ", faults)}";

        void PressAKey(Key key) => _UnhandledKeyInput(new InputEventKey { Keycode = key, Pressed = true });
    }

    /// <summary>
    /// ⭐ Space pauses on the PRESS and the village stays paused through the RELEASE — a probe line (D70x, Joe:
    /// <i>"space for pause should be pressed, not held"</i>); and 5 is 20x (<i>"20x = 5"</i>).
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>⛔ THE CAUSE WAS A FOCUS LEAK, NOT THE KEY HANDLER.</b> A Godot <see cref="Button"/> defaults to
    /// <c>FocusMode.All</c>, so the speed button the player last clicked kept keyboard focus; Godot maps Space to
    /// <c>ui_accept</c>, and a focused <see cref="BaseButton"/> fires <c>Pressed</c> on the key's release. The press
    /// reached <c>_UnhandledKeyInput</c> and paused; the release pressed "1x" again. Every bar button now refuses focus
    /// (<c>RefuseKeyboardFocus</c>), and this line holds it there.
    /// </para>
    /// <para>
    /// Three clauses. <b>The rule:</b> no <see cref="BaseButton"/> under the bar takes focus. <b>The key:</b> 5 reads
    /// 20x. <b>The symptom:</b> a synthetic left click on 1x through <see cref="Viewport.PushInput"/>, then a Space press
    /// and release the same way — the village must be paused after the press and still paused after the release, and
    /// nothing may hold focus afterwards. ⚠️ The third is only as good as headless GUI routing: if the click never
    /// reaches 1x the line says so in words rather than claiming the release was tested, and Joe's hand check stands
    /// (click 1x, press and release Space, the village must stay paused).
    /// </para>
    /// </remarks>
    private string SpaceIsPressedNotHeld()
    {
        if (_controlBar is null)
        {
            return "[widths] space: ⛔ the control bar is not built";
        }

        var faults = new List<string>();

        // The rule.
        var focusable = new List<string>();
        CollectFocusable(_controlBar, focusable);
        if (focusable.Count > 0)
        {
            faults.Add($"{focusable.Count} bar button(s) take keyboard focus: {string.Join(", ", focusable)}");
        }

        double speedBefore = _driver.SpeedMultiplier;
        string open = WhatIsOpen();
        if (open != "nothing was open")
        {
            faults.Add($"the keys would not reach the village ({open})");
        }

        // The key.
        SetSpeed(1.0);
        _UnhandledKeyInput(new InputEventKey { Keycode = Key.Key5, Pressed = true });
        if (_driver.SpeedMultiplier != 20.0)
        {
            faults.Add($"5 set {_driver.SpeedMultiplier}x, not 20x");
        }

        // The symptom, through the viewport so the GUI's own routing — focus included — decides who gets the key.
        Viewport viewport = GetViewport();
        viewport.GuiReleaseFocus();
        string release;
        Button? oneX = FindButton(_controlBar, "1x");
        if (oneX is null)
        {
            faults.Add("no 1x button on the bar");
            release = "no 1x to click";
        }
        else
        {
            SetSpeed(4.0);
            Vector2 at = oneX.GetGlobalRect().GetCenter();
            foreach (bool pressed in new[] { true, false })
            {
                viewport.PushInput(
                    new InputEventMouseButton { ButtonIndex = MouseButton.Left, Pressed = pressed, Position = at, GlobalPosition = at },
                    inLocalCoords: true);
            }

            if (_driver.SpeedMultiplier != 1.0)
            {
                release = $"⚠️ a synthetic click at {at.X:F0},{at.Y:F0} did not reach 1x headless (still {_driver.SpeedMultiplier}x) — "
                    + "the release path is Joe's hand check";
            }
            else
            {
                viewport.PushInput(new InputEventKey { Keycode = Key.Space, PhysicalKeycode = Key.Space, Pressed = true });
                bool pausedOnPress = _driver.IsPaused;
                if (!pausedOnPress)
                {
                    faults.Add($"Space's press did not pause the village after a click on 1x ({_driver.SpeedMultiplier}x)");
                }

                viewport.PushInput(new InputEventKey { Keycode = Key.Space, PhysicalKeycode = Key.Space, Pressed = false });
                if (pausedOnPress && !_driver.IsPaused)
                {
                    faults.Add($"Space's release resumed the village at {_driver.SpeedMultiplier}x — the clicked button fired on ui_accept's release");
                }

                release = "clicked 1x, Space's press paused, its release left it paused";
            }

            if (viewport.GuiGetFocusOwner() is Control owner)
            {
                faults.Add($"{owner.GetType().Name} \"{(owner as Button)?.Text}\" holds keyboard focus after the click");
            }
        }

        SetSpeed(speedBefore);
        return faults.Count == 0
            ? $"[widths] space: ✅ no bar button takes keyboard focus; 5 is 20x; {release}"
            : $"[widths] space: ❌ {string.Join("; ", faults)}";

        static void CollectFocusable(Node under, List<string> into)
        {
            foreach (Node child in under.GetChildren())
            {
                if (child is BaseButton { FocusMode: not Control.FocusModeEnum.None } button)
                {
                    into.Add($"\"{(button as Button)?.Text ?? button.Name}\"");
                }

                CollectFocusable(child, into);
            }
        }

        static Button? FindButton(Node under, string text)
        {
            foreach (Node child in under.GetChildren())
            {
                if (child is Button button && button.Text == text)
                {
                    return button;
                }

                if (FindButton(child, text) is Button found)
                {
                    return found;
                }
            }

            return null;
        }
    }

    /// <summary>
    /// For the probe's faults: everything Esc would deal with before it pauses, by name — so a ❌ says which door
    /// was in the way rather than only that the pause did not come.
    /// </summary>
    private string WhatIsOpen()
    {
        var open = new List<string>();
        if (_momentPanel is { Visible: true })
        {
            open.Add($"a moment was up: \"{_momentTitle.Text}\"");
        }

        if (_map.IsPlacing)
        {
            open.Add("a tool was in hand");
        }

        if (_whatsHerePanel.Visible)
        {
            open.Add("What's here was open");
        }

        if (_title is not null || _newGame is not null)
        {
            open.Add("the title or new-game screen was up");
        }

        return open.Count == 0 ? "nothing was open" : string.Join(", ", open);
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

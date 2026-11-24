using Kith.Sim.Config;
using Godot;

namespace Kith.Game;

/// <summary>
/// ⭐ What the probe's <c>title:</c> line asks of the parchment panel (A180, <c>title-screen-art.md §2</c>) — named from the spec,
/// never read back from the screen's own lists. The test project cannot reach Godot code, so this is where the save / no-save
/// rule is checked (Joe's brief).
/// </summary>
public sealed partial class TitleScreen
{
    /// <summary>What a green line says it checked.</summary>
    internal const string WhatTheTitleShowed =
        "the four seasons behind, covering the window, the village right of the panel at 16:10, 16:9 and 21:9; a parchment sheet docked left, Kith in the blackletter, the items in the heading face; with no save Continue and Load… "
        + "hidden and New village the red primary with the keyboard; over a save Continue the primary and New village plain; "
        + "↓ walks every item shown and wraps; every word at 4.5 : 1 or more; it fits 1280 × 720; Settings lists the player's "
        + "ticks as last left, a tick goes to the file, Back returns; Kith's seal centred above the name, its wax clear of it "
        + "and inside the sheet, hidden where the paper cannot hold it; the primary a slab of the seal's wax in its six states, "
        + "its focus the slab's edge inked heavier and clear of the word";

    /// <summary>The settings this title reads and writes (the probe poses a second title with them).</summary>
    internal SimConfig Config => _config;

    /// <summary>The checks on this title — the probe's, which has no save on disk.</summary>
    internal List<string> WhatTheTitleHolds()
    {
        var faults = new List<string>();

        // The sheet, docked left and full height.
        Rect2 band = Band.GetGlobalRect();
        if (!ReferenceEquals(Band.GetThemeStylebox("panel"), UiLook.Panel()) || band.Position.X > 0.5f || Mathf.Abs(band.Size.Y - Size.Y) > 0.5f)
        {
            faults.Add($"the panel is not the parchment sheet docked left full height ({band})");
        }

        // The faces.
        if (UiLook.Blackletter is { } blackletter && !ReferenceEquals(TitleLabel.GetThemeFont("font"), blackletter))
        {
            faults.Add("Kith is not in the blackletter");
        }

        foreach (Button item in new Button[] { ContinueButton, NewVillageButton, LoadButton, SettingsButton, QuitButton })
        {
            string face = item.GetThemeFont("font")?.ResourcePath ?? "none";
            if (!face.Contains("Metamorphous", StringComparison.Ordinal))
            {
                faults.Add($"{item.Text} is in {face} (want Metamorphous)");
            }
        }

        // No save: Continue and Load… hidden, New village the primary, enabled, with the keyboard.
        if (ContinueButton.Visible || LoadButton.Visible)
        {
            faults.Add($"with no save Continue is {(ContinueButton.Visible ? "shown" : "hidden")} and Load… {(LoadButton.Visible ? "shown" : "hidden")} (want both hidden)");
        }

        if (PrimaryItem != NewVillageButton || NewVillageButton.Disabled || NewVillageButton.ThemeTypeVariation != UiLook.TitlePrimary || !NewVillageButton.HasFocus())
        {
            faults.Add($"with no save the primary is {PrimaryItem.Text} ({PrimaryItem.ThemeTypeVariation}), focused {NewVillageButton.HasFocus()} (want New village, red, focused)");
        }

        faults.AddRange(TheArrowsWalk(this));
        faults.AddRange(TheWordsRead(this));

        // 1280 × 720: what the menu needs fits in 720 high, and the panel takes under 40 % of 1280.
        float needs = _menu.GetCombinedMinimumSize().Y + Band.GetThemeStylebox("panel").GetMinimumSize().Y;
        float wide = Mathf.Max(1280f * PanelShare, Band.GetCombinedMinimumSize().X);
        if (needs > 720f || wide > 1280f * 0.4f)
        {
            faults.Add($"at 1280 × 720 the menu needs {needs:F0} high and the panel {wide:F0} wide (want ≤ 720, ≤ 512)");
        }

        faults.AddRange(TheSettingsWork());
        faults.AddRange(TheSeasonsBehind());
        faults.AddRange(TheSealAbove());
        faults.AddRange(TheWaxButton());
        return faults;
    }

    /// <summary>
    /// The four seasons behind (A190, §4): the committed picture at 3840 × 2160, covering the window centred, and the village
    /// landing right of the panel and inside the window at 16:10, 16:9 and 21:9.
    /// </summary>
    private IEnumerable<string> TheSeasonsBehind()
    {
        Texture2D? picture = Backdrop.Texture;
        if (picture?.ResourcePath != BackdropPath || picture.GetWidth() != 3840 || picture.GetHeight() != 2160)
        {
            yield return $"the backdrop is {picture?.ResourcePath ?? "nothing"} at {picture?.GetWidth()} × {picture?.GetHeight()} (want {BackdropPath}, 3840 × 2160)";
            yield break;
        }

        if (Backdrop.StretchMode != TextureRect.StretchModeEnum.KeepAspectCovered)
        {
            yield return $"the backdrop is laid {Backdrop.StretchMode} (want covering the window, centred)";
        }

        foreach (Vector2 window in new[] { new Vector2(1280, 800), new Vector2(1920, 1080), new Vector2(2560, 1080) })
        {
            Vector2 at = WhereTheVillageLands(new Vector2(3840, 2160), window);
            float panel = Mathf.Max(window.X * PanelShare, Band.GetCombinedMinimumSize().X);
            if (at.X < panel + (window.X * 0.05f) || at.X > window.X * 0.9f || at.Y < window.Y * 0.2f || at.Y > window.Y * 0.8f)
            {
                yield return $"at {window.X:0} × {window.Y:0} the village lands at {at.X:0}, {at.Y:0} (want right of the {panel:0} px panel, inside the window)";
            }
        }
    }

    /// <summary>
    /// Kith's seal above the name (A223, `logo.md §7`): the logo's picture, shown at the probe's window, its wax centred on the
    /// name, ending above the name's capitals and below the sheet's top margin; and hidden, never cut, where there is no room.
    /// </summary>
    private IEnumerable<string> TheSealAbove()
    {
        Texture2D? picture = Seal.Texture;
        if (picture?.ResourcePath != SealPath || picture.GetWidth() != 512 || picture.GetHeight() != 512)
        {
            yield return $"the seal is {picture?.ResourcePath ?? "nothing"} at {picture?.GetWidth()} × {picture?.GetHeight()} (want {SealPath}, 512 × 512)";
            yield break;
        }

        if (!Seal.IsVisibleInTree())
        {
            yield return $"the seal is hidden at {Size.X:0} × {Size.Y:0} (the name's top {TitleLabel.Position.Y:0} px down the column)";
            yield break;
        }

        Rect2 box = Seal.GetGlobalRect(), name = TitleLabel.GetGlobalRect();
        Rect2 inside = Band.GetGlobalRect().Grow(-Band.GetThemeStylebox("panel").GetMargin(Side.Top));
        float waxTop = box.Position.Y + (SealWaxTop * box.Size.Y / SealBox), waxFoot = box.Position.Y + (SealWaxBottom * box.Size.Y / SealBox);
        if (Mathf.Abs(box.GetCenter().X - name.GetCenter().X) > 1f || Mathf.Abs(box.Size.X - SealBox) > 0.5f)
        {
            yield return $"the seal's box is {box} (want {SealBox} square, centred on the name at {name.GetCenter().X:0})";
        }

        float caps = name.Position.Y + NameCapTop;
        if (waxFoot > caps - 12f || waxFoot < caps - 30f || waxTop < inside.Position.Y)
        {
            yield return $"the seal's wax runs {waxTop:0}–{waxFoot:0} px (want below the sheet's margin at {inside.Position.Y:0}, and 12–30 px above the name's capitals at {caps:0})";
        }

        if (!SealFits(SealBox) || SealFits(SealBox - SealFoot - SealWaxTop))
        {
            yield return "a name with no paper above it would still wear the seal, or one with room would not";
        }
    }

    /// <summary>
    /// The primary as wax (A224, `logo.md §8`): every state a painted patch of its own file at the master size with mipmaps,
    /// reaching past the box; focus the edge alone — clear where the word sits, so it never covers it.
    /// </summary>
    private IEnumerable<string> TheWaxButton()
    {
        foreach ((string state, string file) in new[]
        {
            ("normal", "normal"), ("hover", "hover"), ("pressed", "pressed"), ("hover_pressed", "pressed"), ("disabled", "disabled"), ("focus", "focus"),
        })
        {
            string want = ParchmentLook.WaxButton.GetFile() + file + ".png";
            if (PrimaryItem.GetThemeStylebox(state) is not PaintedPatch { Texture: { } wax } patch || wax.ResourcePath.GetFile() != want)
            {
                yield return $"the primary's {state} is not {want}";
                continue;
            }

            if (wax.GetWidth() != 1064 || wax.GetHeight() != 248 || wax.GetImage() is not { } image || !image.HasMipmaps())
            {
                yield return $"{want} is {wax.GetWidth()} × {wax.GetHeight()} (want 1064 × 248, with mipmaps)";
                continue;
            }

            if (patch.Outset != new Vector4(ParchmentLook.WaxOutset, ParchmentLook.WaxOutset, ParchmentLook.WaxOutset, ParchmentLook.WaxOutset))
            {
                yield return $"the primary's {state} wax reaches {patch.Outset} past its box (want {ParchmentLook.WaxOutset} each side)";
            }

            if (file == "focus")
            {
                Image ring = image.IsCompressed() ? (Image)image.Duplicate() : image;
                if (ring.IsCompressed())
                {
                    ring.Decompress();
                }

                float most = 0f;
                for (int y = 85; y < 165; y += 4)
                {
                    for (int x = 150; x < 914; x += 8)
                    {
                        most = Mathf.Max(most, ring.GetPixel(x, y).A);
                    }
                }

                if (most > 0.02f)
                {
                    yield return $"the focus painting covers the word (alpha {most:F2} where it sits)";
                }
            }
        }
    }

    /// <summary>
    /// Pure of the screen: the wax behind the primary's word — the colour at the lightest tenth of the painted field where the
    /// word sits (master px 150–914 across, 85–165 down), so a word that reads there reads on all of it but its glints.
    /// </summary>
    internal static Color WaxGround(string file)
    {
        Image image = GD.Load<Texture2D>("res://textures/ui/" + ParchmentLook.WaxButton + file + ".png").GetImage();
        if (image.IsCompressed())
        {
            image = (Image)image.Duplicate();
            image.Decompress();
        }

        var field = new List<(float Lum, Color Colour)>();
        for (int y = 85; y < 165; y += 2)
        {
            for (int x = 150; x < 914; x += 2)
            {
                Color c = image.GetPixel(x, y);
                field.Add(((0.2126f * c.R) + (0.7152f * c.G) + (0.0722f * c.B), c));
            }
        }

        field.Sort((a, b) => a.Lum.CompareTo(b.Lum));
        return field[(int)(field.Count * 0.9f)].Colour with { A = 1f };
    }

    /// <summary>The checks on a title posed over a save, already in the tree.</summary>
    internal static List<string> WhatASavedTitleShows(TitleScreen posed)
    {
        var faults = new List<string>();
        if (!posed.ContinueButton.Visible || !posed.LoadButton.Visible || posed.PrimaryItem != posed.ContinueButton
            || posed.ContinueButton.ThemeTypeVariation != UiLook.TitlePrimary || posed.NewVillageButton.ThemeTypeVariation != UiLook.TitleItem)
        {
            faults.Add($"over a save Continue is {(posed.ContinueButton.Visible ? "shown" : "hidden")}, Load… {(posed.LoadButton.Visible ? "shown" : "hidden")}, "
                + $"the primary {posed.PrimaryItem.Text}, New village {posed.NewVillageButton.ThemeTypeVariation} (want Continue shown and red, New village plain)");
        }

        faults.AddRange(TheArrowsWalk(posed));

        // Its Settings shows the player's value, which the probe set away from the default before posing it.
        (string Key, CheckBox Box) grid = posed.SettingsTicks.FirstOrDefault(t => t.Key == "grid");
        PlayerTick? row = posed._doors.Ticks.FirstOrDefault(t => t.Key == "grid");
        if (grid.Box is null || row is null || grid.Box.ButtonPressed == row.Default || grid.Box.ButtonPressed != posed._doors.Value("grid"))
        {
            faults.Add($"over a save the grid's tick shows {grid.Box?.ButtonPressed}, the player's file {posed._doors.Value("grid")}, its default {row?.Default} (want the file's)");
        }

        return faults;
    }

    /// <summary>↓ from the primary visits every shown item, in order, and comes back round to it; ↑ the other way.</summary>
    private static IEnumerable<string> TheArrowsWalk(TitleScreen title)
    {
        var down = new List<string>();
        Control? at = title.PrimaryItem;
        for (int i = 0; i <= title.ShownItems.Count && at is not null; i++)
        {
            down.Add((at as Button)?.Text ?? at.Name);
            at = at.FindValidFocusNeighbor(Side.Bottom);
        }

        string[] shown = title.ShownItems.Select(b => b.Text).ToArray();
        int from = Array.IndexOf(shown, title.PrimaryItem.Text);
        string[] want = Enumerable.Range(0, shown.Length + 1).Select(k => shown[(from + k) % shown.Length]).ToArray();
        if (!down.SequenceEqual(want) || title.ShownItems.Any(b => b.FocusMode != FocusModeEnum.All))
        {
            yield return $"↓ walks {string.Join(" → ", down)} (want {string.Join(" → ", want)}, every item focusable)";
        }

        Control? up = title.PrimaryItem.FindValidFocusNeighbor(Side.Top);
        if (up != title.ShownItems[(from + shown.Length - 1) % shown.Length])
        {
            yield return $"↑ from {title.PrimaryItem.Text} goes to {(up as Button)?.Text ?? "nothing"}";
        }
    }

    /// <summary>
    /// Every word shown at 4.5 : 1 or more against what is behind it: the parchment and its darkest mottle (the <c>ui contrast:</c>
    /// line's paper), or the rubric under the primary's words.
    /// </summary>
    private static IEnumerable<string> TheWordsRead(TitleScreen title)
    {
        static double Lum(Color c)
        {
            static double Linear(double v) => v <= 0.03928 ? v / 12.92 : Math.Pow((v + 0.055) / 1.055, 2.4);
            return (0.2126 * Linear(c.R)) + (0.7152 * Linear(c.G)) + (0.0722 * Linear(c.B));
        }

        static double Ratio(Color a, Color b) => (Math.Max(Lum(a), Lum(b)) + 0.05) / (Math.Min(Lum(a), Lum(b)) + 0.05);

        Color sheet = ParchmentLook.Parchment;
        Color mottle = new(sheet.R - (10f / 255f), sheet.G - (10f / 255f), sheet.B - (10f / 255f));
        var words = new List<(string What, Color Ink, Color Ground)>
        {
            ("Kith", title.TitleLabel.GetThemeColor("font_color"), mottle),
            ("the version", title.VersionLabel.GetThemeColor("font_color"), mottle),
        };
        foreach (Button item in title.ShownItems)
        {
            if (item == title.PrimaryItem)
            {
                // On the wax (A224): the lightest tenth of the painted field where the word sits, in every state it is read in.
                foreach ((string file, string colour) in new[] { ("normal", "font_color"), ("hover", "font_hover_color"), ("pressed", "font_pressed_color") })
                {
                    words.Add(($"{item.Text} on the {file} wax", item.GetThemeColor(colour), WaxGround(file)));
                }

                continue;
            }

            foreach (string state in new[] { "font_color", "font_hover_color", "font_focus_color" })
            {
                words.Add(($"{item.Text} ({state})", item.GetThemeColor(state), mottle));
            }
        }

        foreach ((string what, Color ink, Color ground) in words)
        {
            double ratio = Math.Min(Ratio(ink, ground), ground == mottle ? Ratio(ink, sheet) : double.MaxValue);
            if (ratio < 4.5)
            {
                yield return $"{what} reads at {ratio:F2} : 1";
            }
        }
    }

    /// <summary>
    /// Settings: the panel turns to it; its ticks are the table's, in order, as last left (the probe's: the defaults); a tick
    /// pressed goes through the door to the file; Back returns to the menu.
    /// </summary>
    private IEnumerable<string> TheSettingsWork()
    {
        SettingsButton.EmitSignal(BaseButton.SignalName.Pressed);
        if (!SettingsPage.Visible || _menu.Visible)
        {
            yield return "Settings does not turn the panel to the settings";
        }

        string[] keys = SettingsTicks.Select(t => t.Key).ToArray();
        string[] want = _doors.Ticks.Select(t => t.Key).ToArray();
        if (!keys.SequenceEqual(want) || want.Length == 0)
        {
            yield return $"Settings lists {string.Join(", ", keys)} (want the table's {want.Length} ticks in order)";
        }

        foreach ((string key, CheckBox box) in SettingsTicks)
        {
            if (box.ButtonPressed != _doors.Value(key))
            {
                yield return $"the {key} tick shows {box.ButtonPressed}, the player's file says {_doors.Value(key)}";
            }
        }

        (string Key, CheckBox Box) grid = SettingsTicks.FirstOrDefault(t => t.Key == "grid");
        if (grid.Box is not null)
        {
            bool was = grid.Box.ButtonPressed;
            grid.Box.ButtonPressed = !was;
            bool went = _doors.Value("grid") == !was;
            grid.Box.ButtonPressed = was;
            if (!went || _doors.Value("grid") != was)
            {
                yield return "a tick pressed on the title does not reach the player's settings";
            }
        }

        if (_scaleReading.Text != $"{_doors.UiScale()}%")
        {
            yield return $"the UI size reads {_scaleReading.Text} (want {_doors.UiScale()}%)";
        }

        SettingsBack.EmitSignal(BaseButton.SignalName.Pressed);
        if (SettingsPage.Visible || !_menu.Visible)
        {
            yield return "Back does not return to the menu";
        }

        PrimaryItem.GrabFocus();
    }
}

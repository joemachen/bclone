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
        "the four seasons behind with no village's picture, covering the window, the village right of the panel at 16:10, 16:9 and 21:9, painted by two shaders with every uniform theirs; over saves the newest village's picture that opens, never a refused save's, the four seasons where the picture will not read; a village's picture centred so its heart sits at 0.64, 0.50; a parchment sheet docked left, Kith in the blackletter, the items in the heading face; with no save Continue and Load… "
        + "hidden and New village the red primary with the keyboard; over a save Continue the primary and New village plain; "
        + "↓ walks every item shown and wraps; every word at 4.5 : 1 or more; it fits 1280 × 720; Settings lists the player's "
        + "ticks as last left, a tick goes to the file, Back returns; Kith's seal centred above the name, its wax clear of it "
        + "and inside the sheet, hidden where the paper cannot hold it; the primary red words larger than the rest and no box, the keyboard's "
        + "painted stroke under each word and as long as it, thick under the primary; Found filled red with a pen line inside" + WhatTheLiftHeld;

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
        faults.AddRange(ThePaintOver(this));
        faults.AddRange(TheVillagesPicture());
        faults.AddRange(TheLiftHolds());
        faults.AddRange(TheSealAbove());
        faults.AddRange(TheRedWord(this));
        faults.AddRange(TheFoundButton());
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

        foreach ((string what, Vector2 of, Vector2 size) in new[]
        {
            ("the four seasons' village", VillageInPicture, new Vector2(3840, 2160)),
            ("a village's heart", TitleSnapshot.HeartInPicture, (Vector2)TitleSnapshot.PictureSize),
        })
        {
            foreach (Vector2 window in new[] { new Vector2(1280, 800), new Vector2(1920, 1080), new Vector2(2560, 1080) })
            {
                Vector2 at = WhereTheVillageLands(of, size, window);
                float panel = Mathf.Max(window.X * PanelShare, Band.GetCombinedMinimumSize().X);
                if (at.X < panel + (window.X * 0.05f) || at.X > window.X * 0.9f || at.Y < window.Y * 0.2f || at.Y > window.Y * 0.8f)
                {
                    yield return $"at {window.X:0} × {window.Y:0} {what} lands at {at.X:0}, {at.Y:0} (want right of the {panel:0} px panel, inside the window)";
                }
            }
        }
    }

    /// <summary>
    /// The watercolour over whatever is behind (A233–A234, `title-snapshot.md §5b`): the washes drawn once at the picture's size, the
    /// paint on the backdrop at full strength, each shader declaring every uniform it is handed — a shader that did not compile
    /// declares none (art trap 6: a material holds whatever it is told).
    /// </summary>
    private static IEnumerable<string> ThePaintOver(TitleScreen title)
    {
        Texture2D? picture = title.Backdrop.Texture;
        if (title.Backdrop.Material is not ShaderMaterial paint || title.Washes is not { } washes
            || (washes.GetChildCount() > 0 ? washes.GetChild(0) : null) is not TextureRect { Material: ShaderMaterial wash } drawn || picture is null)
        {
            yield return "the picture behind the title is not painted (no paint on the backdrop, or no washes)";
            yield break;
        }

        foreach ((string what, ShaderMaterial material, string path, string[] wants) in new[]
        {
            ("the washes", wash, "title_watercolour_wash.gdshader", new[] { "picture", "finish" }),
            ("the paint", paint, "title_watercolour.gdshader", new[] { "picture", "washes", "marks", "finish", "paper", "strength" }),
        })
        {
            string[] declared = material.Shader.GetShaderUniformList().Select(u => u.AsGodotDictionary()["name"].AsString()).ToArray();
            if (material.Shader.ResourcePath.GetFile() != path || wants.Any(u => !declared.Contains(u)))
            {
                yield return $"{what} are {material.Shader.ResourcePath.GetFile()} declaring {string.Join(", ", declared)} (want {path} declaring {string.Join(", ", wants)})";
            }

            foreach (string uniform in wants.Where(u => u != "strength" && material.GetShaderParameter(u).AsGodotObject() is null))
            {
                yield return $"{what} are handed no {uniform}";
            }
        }

        if (!ReferenceEquals(wash.GetShaderParameter("picture").AsGodotObject(), picture) || !ReferenceEquals(paint.GetShaderParameter("picture").AsGodotObject(), picture)
            || (Vector2)washes.Size != picture.GetSize() || drawn.Size != picture.GetSize() || paint.GetShaderParameter("strength").AsSingle() != 1f
            || washes.RenderTargetUpdateMode == SubViewport.UpdateMode.Always)
        {
            yield return $"the paint is not over the picture shown: washes {washes.Size} of a {picture.GetSize()} picture, drawn {drawn.Size}, {washes.RenderTargetUpdateMode}, strength {paint.GetShaderParameter("strength")}";
        }
    }

    /// <summary>
    /// Which picture a title shows over saves (A234, `title-snapshot.md §2, §8`), on a scratch folder it deletes: the newest village
    /// that has one and opens; a refused save's and a founding's passed by; one that is not a picture, the four seasons — and a
    /// picture's centre, from its heart.
    /// </summary>
    private IEnumerable<string> TheVillagesPicture()
    {
        string scratch = System.IO.Path.GetFullPath(ProjectSettings.GlobalizePath("user://probe-title"));
        try
        {
            SaveListing At(string village, string? refusal = null) =>
                new(System.IO.Path.Combine(scratch, village, "autosave-1.save"), village, refusal);

            void Put(string village, byte[] bytes)
            {
                System.IO.Directory.CreateDirectory(System.IO.Path.Combine(scratch, village));
                System.IO.File.WriteAllBytes(System.IO.Path.Combine(scratch, village, TitleSnapshot.FileName), bytes);
            }

            using Image small = Image.CreateEmpty(64, 36, false, Image.Format.Rgb8);
            byte[] jpg = small.SaveJpgToBuffer();
            Put("refused", jpg);
            Put("lived-in", jpg);
            Put("older", jpg);
            Put("broken", new byte[] { 1, 2, 3, 4 });

            SaveListing[] saves = { At("founding"), At("refused", "from another age"), At("lived-in"), At("lived-in"), At("older") };
            string want = System.IO.Path.Combine(scratch, "lived-in", TitleSnapshot.FileName);
            if (PictureOf(saves) != want)
            {
                yield return $"over a founding, a refused save and two villages the title picks {PictureOf(saves) ?? "nothing"} (want {want})";
            }

            foreach (string fault in Posed(saves, want, new Vector2(64, 36)))
            {
                yield return fault;
            }

            foreach (string fault in Posed(new[] { At("broken") }, null, new Vector2(3840, 2160)))
            {
                yield return $"over a picture that is not one: {fault}";
            }
        }
        finally
        {
            if (System.IO.Directory.Exists(scratch))
            {
                System.IO.Directory.Delete(scratch, recursive: true);
            }
        }

        Vector2 heart = new(12f, -7f);
        Vector2 at = (((heart - TitleSnapshot.CentreFor(heart)) * TitleSnapshot.PixelsPerTile) + ((Vector2)TitleSnapshot.PictureSize / 2f)) / TitleSnapshot.PictureSize;
        if (at.DistanceTo(new Vector2(0.64f, 0.50f)) > 1f / 2560f)
        {
            yield return $"a village's heart is drawn at {at} of its picture (want 0.64, 0.50)";
        }

        if (TitleSnapshot.HeartOf(Kith.Sim.Core.SimWorld.Create(_config)) is Vector2 founding)
        {
            yield return $"a founding with no home has a heart at {founding} (want none, so no picture)";
        }
    }

    /// <summary>A title over these saves, in the tree for the asking.</summary>
    private IEnumerable<string> Posed(SaveListing[] saves, string? picture, Vector2 size)
    {
        var posed = new TitleScreen(_config, saves, _build, _doors);
        AddChild(posed);
        var faults = new List<string>();
        if (posed.VillagePicture != picture || posed.Backdrop.Texture?.GetSize() != size)
        {
            faults.Add($"the title shows {posed.VillagePicture ?? "no village"} at {posed.Backdrop.Texture?.GetSize()} (want {picture ?? "no village"}, {size})");
        }

        faults.AddRange(ThePaintOver(posed));
        RemoveChild(posed);
        posed.QueueFree();
        PrimaryItem.GrabFocus();
        return faults;
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
    /// The primary as red words (A242, `logo.md §9`): larger than every other item, red at rest and no box behind it; and the
    /// keyboard's stroke a painted one under every item, as long as its word and inside the item, thickest under the primary.
    /// </summary>
    private static IEnumerable<string> TheRedWord(TitleScreen title)
    {
        Button primary = title.PrimaryItem;
        if (!primary.GetThemeColor("font_color").IsEqualApprox(ParchmentLook.RubricInk) || primary.GetThemeStylebox("normal") is not StyleBoxFlat { DrawCenter: false })
        {
            yield return $"{primary.Text} rests in {primary.GetThemeColor("font_color").ToHtml(false)} on {primary.GetThemeStylebox("normal")} (want red words, no box)";
        }

        foreach (Button item in title.ShownItems.Append(title.SettingsBack))
        {
            bool first = item == primary;
            int size = item.GetThemeFontSize("font_size");
            if (!first && (size >= primary.GetThemeFontSize("font_size") || item.GetThemeColor("font_color").IsEqualApprox(ParchmentLook.RubricInk)))
            {
                yield return $"{item.Text} is written as large or as red as the primary";
            }

            if (item.GetThemeStylebox("focus") is not PaintedPatch { Texture: { } swash } stroke || swash.ResourcePath.GetFile() != "underline.png")
            {
                yield return $"the keyboard's mark on {item.Text} is not the painted stroke";
                continue;
            }

            // The settings page's Back is laid out only when the page is shown.
            if (!item.IsVisibleInTree())
            {
                continue;
            }

            float word = item.GetThemeFont("font").GetStringSize(item.Text, HorizontalAlignment.Left, -1, size).X;
            float length = item.Size.X + stroke.Outset.X + stroke.Outset.Z;
            if (Mathf.Abs(length - word - StrokePastTheWord) > 1f || length > item.Size.X || stroke.Band != (first ? 9f : 5f))
            {
                yield return $"the stroke under {item.Text} is {length:0} × {stroke.Band:0} (want {word + StrokePastTheWord:0} × {(first ? 9 : 5)})";
            }
        }
    }

    /// <summary>
    /// The new valley's <i>Found</i> (A242): the look's red box with a pen line inside it in every state, its words at 4.5 : 1 or
    /// more on the fill, and the keyboard on it a line alone.
    /// </summary>
    private IEnumerable<string> TheFoundButton()
    {
        foreach ((string state, string colour) in new[] { ("normal", "font_color"), ("hover", "font_hover_color"), ("pressed", "font_pressed_color") })
        {
            if (GetThemeStylebox(state, UiLook.PrimaryButton) is not PenFrame { Box: { } box, Line.A: > 0.5f })
            {
                yield return $"Found's {state} is not the red box with the pen line";
                continue;
            }

            double ratio = Ratio(GetThemeColor(colour, UiLook.PrimaryButton), box.BgColor);
            if (ratio < 4.5)
            {
                yield return $"Found's words read at {ratio:F2} : 1 {state}";
            }
        }

        if (GetThemeStylebox("focus", UiLook.PrimaryButton) is not PenFrame { Box: null })
        {
            yield return "the keyboard on Found is not a line alone";
        }
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
        faults.AddRange(TheRedWord(posed));

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

    /// <summary>The contrast between two colours, as WCAG counts it.</summary>
    private static double Ratio(Color a, Color b)
    {
        static double Lum(Color c)
        {
            static double Linear(double v) => v <= 0.03928 ? v / 12.92 : Math.Pow((v + 0.055) / 1.055, 2.4);
            return (0.2126 * Linear(c.R)) + (0.7152 * Linear(c.G)) + (0.0722 * Linear(c.B));
        }

        return (Math.Max(Lum(a), Lum(b)) + 0.05) / (Math.Min(Lum(a), Lum(b)) + 0.05);
    }

    /// <summary>
    /// Every word shown at 4.5 : 1 or more against what is behind it: the parchment and its darkest mottle (the <c>ui contrast:</c>
    /// line's paper).
    /// </summary>
    private static IEnumerable<string> TheWordsRead(TitleScreen title)
    {
        Color sheet = ParchmentLook.Parchment;
        Color mottle = new(sheet.R - (10f / 255f), sheet.G - (10f / 255f), sheet.B - (10f / 255f));
        var words = new List<(string What, Color Ink, Color Ground)>
        {
            ("Kith", title.TitleLabel.GetThemeColor("font_color"), mottle),
            ("the version", title.VersionLabel.GetThemeColor("font_color"), mottle),
        };
        foreach (Button item in title.ShownItems)
        {
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

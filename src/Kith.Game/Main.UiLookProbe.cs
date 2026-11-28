using Kith.Sim.Logging;
using Kith.Sim.World;
using Godot;

namespace Kith.Game;

/// <remarks>The probe's <c>ui theme:</c> line (<c>ui-art.md §9.3</c> step 1, A110).</remarks>
public partial class Main
{
    /// <summary>
    /// ⭐ THE SEAM (<c>ui-art.md §4</c>): with no theme naming an entry, every role and skin is the slate that stood at its
    /// site before (the literals below are those sites' own, copied once); a theme that names an entry is obeyed, and one
    /// that does not leaves the rest slate; a card on the screen wears the look's own kept skin, so the card code asks the
    /// look and makes none of its own. The project's theme is put back after.
    /// </summary>
    private string TheLookIsOneDoor()
    {
        var faults = new List<string>();
        void Same(string what, Color got, Color want)
        {
            if (!got.IsEqualApprox(want))
            {
                faults.Add($"{what} is {got} not {want}");
            }
        }

        bool projectNamesALook = UiLook.WearsALook;

        // ---- in a worn look that inks its words, a tint is the label's font colour, not a multiply (A113) ----
        if (UiLook.TintsByFont)
        {
            var inked = new Label();
            UiLook.Tint(inked, UiLook.Stopped);
            if (!inked.GetThemeColor("font_color").IsEqualApprox(UiLook.Stopped) || inked.Modulate != Colors.White)
            {
                faults.Add("the worn look's tint is not the label's ink");
            }

            inked.Free();
        }

        UiLook.Use(null);
        try
        {
            // ---- the slate, as it stood at each site ----
            Same("text", UiLook.Text, Colors.White);
            Same("faint", UiLook.Faint, new Color(1, 1, 1, 0.55f));
            Same("soft", UiLook.Soft, new Color(1, 1, 1, 0.7f));
            Same("hint", UiLook.Hint, new Color(1, 1, 1, 0.65f));
            Same("dim", UiLook.Dim, new Color(1, 1, 1, 0.45f));
            Same("ghost", UiLook.Ghost, new Color(1, 1, 1, 0.35f));
            Same("strong", UiLook.Strong, new Color(1, 1, 1, 0.8f));
            Same("working", UiLook.Working, new Color("#7fbf7a"));
            Same("stopped", UiLook.Stopped, new Color("#ffc759"));
            Same("selected edge", UiLook.SelectedEdge, new Color("#ffd76a"));
            Same("card edge", UiLook.CardEdge, new Color("#3a3f47"));
            Same("owed", UiLook.Owed, new Color(1f, 0.85f, 0.4f));
            Same("held", UiLook.Held, new Color(1f, 0.85f, 0.4f));
            Same("refused", UiLook.Refused, new Color(1f, 0.55f, 0.45f));
            Same("saved", UiLook.Saved, new Color(0.62f, 0.86f, 0.58f));
            Same("known", UiLook.Known, new Color(0.62f, 0.86f, 0.58f));
            Same("tree edge", UiLook.TreeEdge, new Color(1f, 1f, 1f, 0.35f));
            Same("empty", UiLook.Empty, new Color(1, 1, 1, 0.10f));
            Same("stamp", UiLook.Stamp, new Color("#7f8c94"));
            Same("founding mark", UiLook.FoundingMark, new Color(0.98f, 0.78f, 0.46f));
            Same("pause shade", UiLook.PauseShade, new Color(0f, 0f, 0f, 0.55f));
            Same("log death", UiLook.Log(LogCategory.Death), new Color(0.85f, 0.40f, 0.40f));
            Same("log life", UiLook.Log(LogCategory.Life), new Color(0.55f, 0.80f, 0.55f));
            Same("log discovery", UiLook.Log(LogCategory.Discovery), new Color(0.91f, 0.77f, 0.41f));
            Same("log warning", UiLook.Log(LogCategory.Warning), new Color(1f, 0.78f, 0.35f));
            Same("log building", UiLook.Log(LogCategory.Building), new Color(0.62f, 0.72f, 0.85f));
            Same("log season", UiLook.Log(LogCategory.Season), new Color(0.60f, 0.62f, 0.60f));
            Same("log ordinary", UiLook.Log(LogCategory.Ordinary), new Color(0.88f, 0.88f, 0.86f));

            if (UiLook.Panel(0.94f) is not StyleBoxFlat panel
                || !panel.BgColor.IsEqualApprox(new Color(0.08f, 0.09f, 0.10f, 0.94f))
                || !panel.BorderColor.IsEqualApprox(new Color(0.58f, 0.53f, 0.40f, 0.55f))
                || panel.BorderWidthTop != 1 || panel.CornerRadiusTopLeft != 3 || !Mathf.IsEqualApprox(panel.ContentMarginLeft, 6f))
            {
                faults.Add("a slate panel is not today's");
            }

            if (UiLook.Card(true, PersonCardMargin) is not StyleBoxFlat card
                || !card.BgColor.IsEqualApprox(new Color(0.08f, 0.09f, 0.10f, 0.96f))
                || !card.BorderColor.IsEqualApprox(new Color("#ffd76a")) || card.BorderWidthLeft != 1
                || !Mathf.IsEqualApprox(card.ContentMarginTop, PersonCardMargin))
            {
                faults.Add("a selected person's slate card is not today's");
            }

            if (UiLook.Pill(met: false) is not StyleBoxFlat pill
                || !pill.BgColor.IsEqualApprox(new Color("#ffc759") with { A = 0.18f }) || pill.CornerRadiusTopLeft != 9
                || !Mathf.IsEqualApprox(pill.ContentMarginLeft, 8f))
            {
                faults.Add("an unmet slate pill is not today's");
            }

            if (UiLook.Bar(fill: true) is not StyleBoxFlat fill || !fill.BgColor.IsEqualApprox(new Color("#7fbf7a"))
                || UiLook.Bar(fill: false) is not StyleBoxFlat track || !track.BgColor.IsEqualApprox(new Color(1, 1, 1, 0.10f)))
            {
                faults.Add("a slate bar is not today's");
            }

            // ---- a card on the screen wears the look's own skin ----
            if (_loop.World.TheCart is StoreBuilding cart)
            {
                OpenCard(new CardSubject(CardKind.Store, cart.Id));
                Card open = _selectedCard!;
                if (!ReferenceEquals(open.Panel.GetThemeStylebox("panel"), UiLook.Card(true)))
                {
                    faults.Add("the open card wears a skin the look did not make");
                }

                CloseCard(open);
            }
            else
            {
                faults.Add("no cart to open a card on");
            }

            // ---- a theme that names an entry is obeyed; what it does not name stays slate ----
            var theme = new Theme();
            var odd = new Color(0.9f, 0.1f, 0.8f);
            var box = new StyleBoxFlat { BgColor = odd, ContentMarginLeft = 4, ContentMarginRight = 4, ContentMarginTop = 4, ContentMarginBottom = 4 };
            theme.SetColor("stopped", UiLook.Type, odd);
            theme.SetStylebox("card", UiLook.Type, box);
            UiLook.Use(theme);
            Same("a theme's stopped", UiLook.Stopped, odd);
            Same("working, which the theme does not name", UiLook.Working, new Color("#7fbf7a"));
            if (!ReferenceEquals(UiLook.Card(false), box))
            {
                faults.Add("a theme's card is not used");
            }

            if (UiLook.Card(false, 12) is not { } roomy || ReferenceEquals(roomy, box) || !Mathf.IsEqualApprox(roomy.ContentMarginLeft, 10f))
            {
                faults.Add("a theme's card is not given a person's extra room on top of its own (4 + 6)");
            }

            if (UiLook.Card(true) is not StyleBoxFlat slateSelected || !slateSelected.BorderColor.IsEqualApprox(new Color("#ffd76a")))
            {
                faults.Add("a selected card the theme does not name is not slate");
            }

            var label = new Label();
            UiLook.Tint(label, UiLook.Stopped);
            Same("a tint read back", UiLook.TintOf(label), odd);
            label.Free();
        }
        finally
        {
            UiLook.PutBack();
            RefreshCards(_loop.World);
        }

        return faults.Count == 0
            ? $"[widths] ui theme: ✅ {(projectNamesALook ? "a look is worn; under no theme" : "no look named — slate")}: 28 roles and 4 skins are "
              + "the slate their sites had; an open card wears the look's kept skin; a theme's named entries are obeyed and the "
              + "rest stay slate; a tint reads back"
            : $"[widths] ui theme: ⛔ {string.Join("; ", faults)}";
    }

    /// <summary>
    /// ⭐ RED FOR WARNINGS, LAPIS FOR SELECTED, HELD AND ON (<c>hud.md §10</c>, step 1; A207): the art lane's five controls
    /// (A216–A217) load at their master size with mipmaps; no pressed, held, selected or ticked style in the look is rubric —
    /// each is lapis; every warning role still is rubric; and a card on the screen wears the painted pin, lapis when pinned.
    /// </summary>
    private string TheRubricMeansWrongAndLapisMeansOn()
    {
        var faults = new List<string>();
        const string controls = "res://textures/ui/controls/";
        foreach ((string file, int w, int h) in new[]
        {
            ("underline-lapis", 256, 12), ("underline", 256, 12), ("checkbox-ticked-lapis", 56, 56),
            ("checkbox-ticked-lapis-disabled", 56, 56), ("pin-pinned-lapis", 56, 56), ("pill-warn", 96, 64),
        })
        {
            Texture2D? texture = ResourceLoader.Exists(controls + file + ".png") ? GD.Load<Texture2D>(controls + file + ".png") : null;
            if (texture is null)
            {
                faults.Add($"{file}.png does not load");
            }
            else if (texture.GetWidth() != w || texture.GetHeight() != h)
            {
                faults.Add($"{file}.png is {texture.GetWidth()}x{texture.GetHeight()}, not its master {w}x{h}");
            }
            else if (texture.GetImage() is not { } image || !image.HasMipmaps())
            {
                faults.Add($"{file}.png has no mipmaps");
            }
        }

        static string FileOf(Texture2D? texture) =>
            (texture is SizedTexture sized ? sized.Source : texture)?.ResourcePath.GetFile() ?? "nothing";

        static bool Red(Color c) => c.IsEqualApprox(ParchmentLook.RubricInk) || c.IsEqualApprox(ParchmentLook.Rubric);

        Theme look = ParchmentLook.Build();
        string t = UiLook.Type;

        // ⭐ A314 (`sickbed-art.md §4`): the health pill's third look — the amber label, its own painting, its words in ochre; and W6's
        // three faces (§7) at their 128 master with mipmaps, waiting for piece 1 to wear them.
        string warnFile = look.HasStylebox("pill_warn", t) && look.GetStylebox("pill_warn", t) is PaintedPatch warn ? FileOf(warn.Texture) : "nothing";
        if (warnFile != "pill-warn.png" || !look.GetColor("warned", t).IsEqualApprox(ParchmentLook.OchreInk))
        {
            faults.Add($"the warned pill wears {warnFile} with words {look.GetColor("warned", t).ToHtml(false)} (want pill-warn.png, ochre)");
        }

        foreach (string face in new[] { "plain-diet", "run-down", "sick" })
        {
            string path = $"res://textures/ui/icons/health/{face}.png";
            Texture2D? texture = ResourceLoader.Exists(path) ? GD.Load<Texture2D>(path) : null;
            if (texture is null || texture.GetWidth() != 128 || texture.GetHeight() != 128 || texture.GetImage() is not { } img || !img.HasMipmaps())
            {
                faults.Add($"the {face} face is {(texture is null ? "missing" : $"{texture.GetWidth()}x{texture.GetHeight()} or unmipmapped")} (want 128x128, mipmapped)");
            }
        }
        foreach ((string role, Color want) in new[] { ("held", ParchmentLook.Lapis), ("selected_edge", ParchmentLook.LapisFill) })
        {
            if (!look.GetColor(role, t).IsEqualApprox(want))
            {
                faults.Add($"{role} is {look.GetColor(role, t).ToHtml(false)}, not lapis");
            }
        }

        foreach (string type in new[] { "Button", "CheckBox", "CheckButton", "OptionButton", "MenuButton" })
        {
            foreach (string colour in new[] { "font_pressed_color", "font_hover_pressed_color" })
            {
                if (!look.GetColor(colour, type).IsEqualApprox(ParchmentLook.Lapis))
                {
                    faults.Add($"a pressed {type}'s {colour} is {look.GetColor(colour, type).ToHtml(false)}, not lapis");
                }
            }
        }

        foreach (string state in new[] { "pressed", "hover_pressed" })
        {
            string swash = look.GetStylebox(state, "Button") is PaintedPatch held ? FileOf(held.Texture) : "no painted swash";
            if (swash != "underline-lapis.png")
            {
                faults.Add($"a {state} button's swash is {swash}");
            }
        }

        foreach (string type in new[] { "CheckBox", "CheckButton" })
        {
            foreach ((string icon, string want) in new[] { ("checked", "checkbox-ticked-lapis.png"), ("checked_disabled", "checkbox-ticked-lapis-disabled.png") })
            {
                if (FileOf(look.GetIcon(icon, type)) != want)
                {
                    faults.Add($"a {type}'s {icon} seal is {FileOf(look.GetIcon(icon, type))}");
                }
            }
        }

        if (look.GetIcon("grabber_highlight", "HSlider")?.GetImage() is not { } grip || Red(grip.GetPixel(9, 9) with { A = 1f }))
        {
            faults.Add("a held slider's grip is rubric");
        }

        foreach (string role in new[] { "stopped", "refused", "log_death" })
        {
            if (!look.GetColor(role, t).IsEqualApprox(ParchmentLook.RubricInk))
            {
                faults.Add($"the warning {role} is not rubric");
            }
        }

        // ---- a card on the screen wears the painted pin, its head lapis when pinned ----
        if (_loop.World.TheCart is not StoreBuilding cart)
        {
            faults.Add("no cart to open a card on");
        }
        else if (UiLook.WearsALook)
        {
            OpenCard(new CardSubject(CardKind.Store, cart.Id));
            Card open = _selectedCard!;
            string rest = FileOf(open.Pin.Icon);
            open.Pin.ButtonPressed = true;
            string pinned = FileOf(open.Pin.Icon);
            open.Pin.ButtonPressed = false;
            string after = FileOf(open.Pin.Icon);
            if (rest != "pin.png" || pinned != "pin-pinned-lapis.png" || after != "pin.png" || open.Pin.Text.Length != 0)
            {
                faults.Add($"a card's pin wears {rest}, pinned {pinned}, unpinned again {after}, text '{open.Pin.Text}' (want pin.png, pin-pinned-lapis.png, pin.png, none)");
            }

            CloseCard(open);
        }

        return faults.Count == 0
            ? "[widths] ui colours: ✅ the five lapis and rubric controls and the amber pill load at their masters with mipmaps; the warned "
              + "pill wears its own painting, its words ochre; W6's three faces at 128, mipmapped; held, selected, every "
              + "pressed font and swash and the ticked seal are lapis; stopped, refused and deaths stay rubric; a card's pin is painted, lapis when pinned"
            : $"[widths] ui colours: ⛔ {string.Join("; ", faults)}";
    }

    /// <summary>
    /// ⭐ EVERY WORD THE PARCHMENT LOOK COLOURS READS ON IT (Non-Negotiable §1.1, <c>ui-assets.md §1.3</c>): each text role's
    /// ink against the sheet and against its darkest mottle (−10 a channel), 4.5 : 1 at least (WCAG's sum, sRGB).
    /// </summary>
    private static string TheWordsReadOnParchment()
    {
        static double Lum(Color c)
        {
            static double Linear(double v) => v <= 0.03928 ? v / 12.92 : System.Math.Pow((v + 0.055) / 1.055, 2.4);
            return (0.2126 * Linear(c.R)) + (0.7152 * Linear(c.G)) + (0.0722 * Linear(c.B));
        }

        static double Ratio(Color a, Color b)
        {
            double la = Lum(a);
            double lb = Lum(b);
            return (System.Math.Max(la, lb) + 0.05) / (System.Math.Min(la, lb) + 0.05);
        }

        Color sheet = ParchmentLook.Parchment;
        Color mottle = new(sheet.R - (10f / 255f), sheet.G - (10f / 255f), sheet.B - (10f / 255f));
        var faults = new List<string>();
        double worst = double.MaxValue;
        string worstRole = string.Empty;
        foreach ((string role, Color colour) in ParchmentLook.Words)
        {
            double ratio = System.Math.Min(Ratio(colour, sheet), Ratio(colour, mottle));
            if (ratio < worst)
            {
                worst = ratio;
                worstRole = role;
            }

            if (ratio < 4.5)
            {
                faults.Add($"{role} reads at {ratio:F2}:1");
            }
        }

        // ---- and on the paper as painted where the words begin: each frame's inner edge, read off its master (A116) ----
        // ⚠️ The flat colour above cannot see an edge vignette; A116's first inked sheets greyed the paper under the outermost
        // words to 3.2 : 1, and this line was green.
        Theme look = ParchmentLook.Build();
        double edgeWorst = double.MaxValue;
        string edgeWhere = string.Empty;
        foreach (string box in new[] { "panel", "card", "card_selected", "strip", "top_box" })
        {
            if (look.GetStylebox(box, UiLook.Type) is not PaintedPatch patch || patch.Texture?.GetImage() is not { } image)
            {
                faults.Add($"no painted {box} to read");
                continue;
            }

            if (image.IsCompressed())
            {
                image.Decompress();
            }

            int w = image.GetWidth();
            int h = image.GetHeight();
            float s = 1f / patch.Scale;
            int l = (int)((patch.ContentMarginLeft + patch.Outset.X) * s);
            int t = (int)((patch.ContentMarginTop + patch.Outset.Y) * s);
            int r = w - 1 - (int)((patch.ContentMarginRight + patch.Outset.Z) * s);
            int b = h - 1 - (int)((patch.ContentMarginBottom + patch.Outset.W) * s);
            foreach ((int x, int y) in new[] { (l, h / 3), (l, h / 2), (l, 2 * h / 3), (r, h / 3), (r, h / 2), (r, 2 * h / 3), (w / 3, t), (w / 2, t), (w / 2, b), (2 * w / 3, b) })
            {
                Color paper = image.GetPixel(Mathf.Clamp(x, 0, w - 1), Mathf.Clamp(y, 0, h - 1));
                foreach ((string role, Color colour) in ParchmentLook.Words)
                {
                    double ratio = Ratio(colour, paper with { A = 1f });
                    if (ratio < edgeWorst)
                    {
                        edgeWorst = ratio;
                        edgeWhere = $"{role} at the {box}'s inner edge ({x}, {y})";
                    }
                }
            }
        }

        if (edgeWorst < 4.5)
        {
            faults.Add($"{edgeWhere} reads at {edgeWorst:F2}:1 on the paper as painted");
        }

        return faults.Count == 0
            ? $"[widths] ui contrast: ✅ {ParchmentLook.Words.Length} text roles on the parchment and its darkest mottle, the faintest {worstRole} at {worst:F2}:1; "
              + $"on the paper as painted at every frame's inner edge, the faintest {edgeWorst:F2}:1 (4.5 wanted)"
            : $"[widths] ui contrast: ⛔ {string.Join("; ", faults)}";
    }
}

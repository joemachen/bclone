using Godot;

namespace Kith.Game;

/// <summary>
/// The parchment look (A101–A106; its scrolls removed, A114), as a theme (<c>ui-art.md §9.3</c> step 3, A113): the painted chrome and controls in
/// <c>textures/ui/</c>, the manuscript palette (<c>ui-assets.md §1.4</c>), and text in ink — worn by
/// <see cref="LookLoader"/> before any window is built.
/// </summary>
/// <remarks>
/// <para>
/// <b>Two halves, as <c>ui-art.md §4</c> found.</b> The <see cref="UiLook.Type"/> entries answer the roles the game's
/// own code asks for (a card, a pill, the stopped colour); the default control types (Button, CheckBox, PopupMenu, the
/// scrollbars, the roster's Tree, the log's RichTextLabel) restyle what the game never touched.
/// </para>
/// <para>
/// ⭐ <b>Every text colour here is measured against the parchment</b> (probe <c>ui contrast:</c>, 4.5 : 1 at least,
/// Non-Negotiable §1.1): the palette's gold leaf is 2.1 : 1 on its own sheet, so an owed gift's words are a darkened gold,
/// and the leaf stays in the painting.
/// </para>
/// </remarks>
internal static class ParchmentLook
{
    private const string Ui = "res://textures/ui/";

    // ---- the palette (ui-assets.md §1.4), and the darkened inks the words need ----
    internal static readonly Color Parchment = new("#ead3a9");
    internal static readonly Color Ink = new("#2a2018");
    internal static readonly Color InkMuted = new("#503f2c"); // #5c4833 read 4.06 on A117's weathered paper
    internal static readonly Color InkSoft = new("#4a3a2a");
    // ⚠️ The text inks are measured on the paper as painted, its darkest mottle and every frame's inner edge, at 5 : 1 or
    // more (ui contrast:, A116): on the flat colour alone they passed at 4.5 and read 3.2 under A116's first inked edges.
    internal static readonly Color InkDim = new("#503f30"); // #6b5640 4.30 (A113), #644f3c 4.65 (A116), #5c4837 4.05 on the weathered paper (A117)
    internal static readonly Color Rubric = new("#9b2d1f");
    internal static readonly Color Verdigris = new("#2f5a28");
    internal static readonly Color RubricInk = new("#7b2318"); // the rubric as words: #9b2d1f 4.55 (A116), #8b281b 4.08 on the weathered paper (A117)
    internal static readonly Color VerdigrisInk = new("#264a21"); // the verdigris as words: #2f5a28 4.85 (A116), #2c5526 4.05 (A117)
    internal static readonly Color Lapis = new("#2a3f7b"); // the lapis as words (the log's buildings): #30488c read 4.06 on the weathered paper (A117)
    internal static readonly Color GoldInk = new("#553e0c"); // #6e5010 4.50 (A116), #61470e 4.09 on the weathered paper (A117)
    internal static readonly Color OchreInk = new("#62370c"); // #7a4510 4.72 (A116), #703f0e 4.10 (A117)

    /// <summary>The text roles and their colours, for the contrast probe — every one a word read on parchment.</summary>
    internal static readonly (string Role, Color Colour)[] Words =
    {
        ("text", Ink), ("text_faint", InkMuted), ("text_soft", InkSoft), ("text_hint", InkMuted), ("text_dim", InkDim),
        ("text_strong", Ink), ("working", VerdigrisInk), ("stopped", RubricInk), ("owed", GoldInk), ("held", RubricInk),
        ("refused", RubricInk), ("saved", VerdigrisInk), ("known", VerdigrisInk), ("stamp", InkDim), ("log_death", RubricInk),
        ("log_life", VerdigrisInk), ("log_discovery", GoldInk), ("log_warning", OchreInk), ("log_building", Lapis),
        ("log_season", InkMuted), ("log_ordinary", Ink),
    };

    private static Texture2D Tex(string path) => GD.Load<Texture2D>(Ui + path);

    /// <summary>The theme.</summary>
    internal static Theme Build()
    {
        var theme = new Theme();
        string t = UiLook.Type;

        // ---- the roles the game's code asks for ----
        theme.SetConstant("tints_by_font", t, 1);
        theme.SetConstant("grows_by_frame", t, 1); // Joe, A113: "A, frames add width"
        foreach ((string role, Color colour) in Words)
        {
            theme.SetColor(role, t, colour);
        }

        theme.SetColor("text_ghost", t, InkDim with { A = 0.7f });
        theme.SetColor("selected_edge", t, Rubric);
        theme.SetColor("card_edge", t, InkMuted);
        theme.SetColor("tree_edge", t, Ink with { A = 0.35f });
        theme.SetColor("empty", t, Ink with { A = 0.12f });
        theme.SetColor("founding_mark", t, Rubric);
        theme.SetColor("pause_shade", t, new Color(0.10f, 0.07f, 0.04f, 0.45f));
        theme.SetColor("fade", t, new Color(1f, 1f, 1f, 0.5f));

        // A torn, lightly scorched sheet — no roll (A114, Joe: "keep the parchment and remove the scrolls"). Text clears the scorch.
        PaintedPatch window = PaintedPatch.Make(Tex("chrome/sheet.png"), 0.5f, new Vector4(28, 28, 32, 30), new Vector4(15, 15, 20, 18));
        theme.SetStylebox("panel", t, window);
        theme.SetStylebox("card", t, PaintedPatch.Make(Tex("chrome/sheet.png"), 0.5f, new Vector4(28, 28, 32, 30), new Vector4(28, 15, 20, 18)));

        // Selected: the illuminated bar down the left (≈9 logical) — the same room inside, so a card does not jump when picked.
        theme.SetStylebox(
            "card_selected", t, PaintedPatch.Make(Tex("chrome/sheet-selected.png"), 0.5f, new Vector4(56, 28, 32, 30), new Vector4(28, 15, 20, 18)));

        // The control bar: a long torn sheet across the screen — no rolls at its ends (A114).
        theme.SetStylebox("strip", t, PaintedPatch.Make(Tex("chrome/band.png"), 0.5f, new Vector4(32, 20, 34, 26), new Vector4(15, 12, 20, 20)));

        // The top bar's boxes: a torn slip — slate's own 6 px inside, because that bar is full (A111); the paper reaches a few
        // px past the box instead (A116), so the outermost words never sit on the ink line, and the bar does not move.
        // A124: its own long slip, painted near the size it is drawn — the 240 × 120 slip was stretched ~9 × across Joe's
        // 1920 at 75 %, and every grain and stain in it with it. The small slip stays for popups, menus and tooltips.
        PaintedPatch topBox = PaintedPatch.Make(Tex("chrome/slip-long.png"), 0.5f, new Vector4(20, 16, 20, 16), new Vector4(6, 6, 6, 6));
        topBox.Outset = new Vector4(4, 4, 7, 7);
        theme.SetStylebox("top_box", t, topBox);

        theme.SetStylebox("pill_met", t, PaintedPatch.Make(Tex("controls/pill.png"), 0.25f, new Vector4(28, 0, 28, 0), new Vector4(8, 1, 8, 1)));
        theme.SetStylebox("pill_unmet", t, PaintedPatch.Make(Tex("controls/pill-unmet.png"), 0.25f, new Vector4(28, 0, 28, 0), new Vector4(8, 1, 8, 1)));
        theme.SetStylebox("bar_track", t, PaintedPatch.Make(Tex("controls/bar-track.png"), 0.25f, new Vector4(8, 0, 8, 0), Vector4.Zero));
        theme.SetStylebox("bar_fill", t, PaintedPatch.Make(Tex("controls/bar-fill.png"), 0.25f, new Vector4(8, 0, 8, 0), Vector4.Zero));
        theme.SetStylebox("pause_sheet", t, PaintedPatch.Make(Tex("chrome/sheet-large.png"), 0.5f, new Vector4(60, 60, 60, 60), new Vector4(24, 22, 26, 22)));

        // The tech tree's cards (ui-assets.md §5): known in a colour wash, in sight inked only, fogged a grey silhouette.
        foreach ((string state, string file) in new[] { ("known", "known"), ("insight", "locked"), ("fogged", "fogged") })
        {
            theme.SetStylebox("tech_" + state, t, PaintedPatch.Make(Tex("screens/tech-card-" + file + ".png"), 0.5f, new Vector4(24, 24, 24, 24), new Vector4(8, 3, 8, 3)));
        }

        // Joe's pair (A120): Grenze Gotisch for every word, Metamorphous for the headers — window titles, card titles, a
        // card's section heads (UiLook.Header marks them). Both SIL OFL, their licences beside them in fonts/.
        // (A119's Archivo Narrow stays in fonts/ArchivoNarrow/, unused.)
        theme.DefaultFont = GD.Load<FontFile>("res://fonts/GrenzeGotisch/GrenzeGotisch-VariableFont_wght.ttf");
        Font header = GD.Load<FontFile>("res://fonts/Metamorphous/Metamorphous-Regular.ttf");
        theme.SetConstant("card_room", t, 20); // the widest own name in Metamorphous fits the card's head (D467)
        foreach ((string variation, string type) in new[] { (UiLook.HeaderLabel, "Label"), (UiLook.HeaderButton, "Button") })
        {
            theme.SetTypeVariation(variation, type);
            theme.SetFont("font", variation, header);
        }

        // The roster's column titles (Name, Age, Trade) are headers too (A121, Joe: "Yes, headers").
        theme.SetFont("title_button_font", "Tree", header);

        DefaultTypes(theme);
        return theme;
    }

    /// <summary>What a theme alone restyles: the controls the game makes and never skins itself.</summary>
    private static void DefaultTypes(Theme theme)
    {
        PaintedPatch window = (PaintedPatch)theme.GetStylebox("panel", UiLook.Type);
        PaintedPatch slip = PaintedPatch.Make(Tex("chrome/slip.png"), 0.5f, new Vector4(20, 16, 20, 16), new Vector4(10, 8, 10, 8));

        foreach (string type in new[] { "Label", "LineEdit", "SpinBox", "TooltipLabel" })
        {
            theme.SetColor("font_color", type, Ink);
        }

        theme.SetColor("default_color", "RichTextLabel", Ink);
        theme.SetStylebox("panel", "PanelContainer", window);
        theme.SetStylebox("panel", "Panel", window);
        theme.SetStylebox("panel", "PopupPanel", slip);
        theme.SetStylebox("panel", "PopupMenu", slip);
        theme.SetStylebox("panel", "TooltipPanel", slip);

        // ---- buttons: ink words on the sheet; a faint wash under the pointer; a rubric swash under a held one ----
        var flat = new StyleBoxEmpty { ContentMarginLeft = 6, ContentMarginRight = 6, ContentMarginTop = 4, ContentMarginBottom = 4 };
        var hover = new StyleBoxFlat { BgColor = Ink with { A = 0.07f }, ContentMarginLeft = 6, ContentMarginRight = 6, ContentMarginTop = 4, ContentMarginBottom = 4 };
        hover.SetCornerRadiusAll(3);
        PaintedPatch held = PaintedPatch.Make(Tex("controls/underline.png"), 0.25f, new Vector4(24, 0, 24, 0), new Vector4(6, 4, 6, 4));
        held.Band = 3f;
        foreach (string type in new[] { "Button", "CheckBox", "CheckButton", "OptionButton", "MenuButton" })
        {
            theme.SetStylebox("normal", type, flat);
            theme.SetStylebox("hover", type, hover);
            theme.SetStylebox("pressed", type, type == "Button" ? held : flat);
            theme.SetStylebox("hover_pressed", type, type == "Button" ? held : hover);
            theme.SetStylebox("disabled", type, flat);
            theme.SetStylebox("focus", type, new StyleBoxEmpty());
            theme.SetColor("font_color", type, Ink);
            theme.SetColor("font_hover_color", type, Ink);
            theme.SetColor("font_pressed_color", type, RubricInk);
            theme.SetColor("font_hover_pressed_color", type, RubricInk);
            theme.SetColor("font_focus_color", type, Ink);
            theme.SetColor("font_disabled_color", type, InkMuted with { A = 0.6f });
            theme.SetColor("icon_normal_color", type, Colors.White);
        }

        // ---- the checkbox: an inked ring; a red wax seal when ticked (A102) ----
        Texture2D Box(string name) => SizedTexture.Of(Tex("controls/" + name + ".png"), 14, 14);
        foreach (string type in new[] { "CheckBox", "CheckButton" })
        {
            theme.SetIcon("unchecked", type, Box("checkbox"));
            theme.SetIcon("checked", type, Box("checkbox-ticked"));
            theme.SetIcon("unchecked_disabled", type, Box("checkbox-disabled"));
            theme.SetIcon("checked_disabled", type, Box("checkbox-ticked-disabled"));
        }

        theme.SetIcon("arrow", "OptionButton", SizedTexture.Of(Tex("controls/dropdown-arrow.png"), 10, 6));
        var field = new StyleBoxFlat { BgColor = new Color("#f3e6c8"), BorderColor = Ink with { A = 0.4f } };
        field.SetBorderWidthAll(1);
        field.SetCornerRadiusAll(2);
        field.SetContentMarginAll(4);
        theme.SetStylebox("normal", "OptionButton", field);
        theme.SetStylebox("normal", "LineEdit", field);
        var fieldFocus = (StyleBoxFlat)field.Duplicate();
        fieldFocus.BorderColor = Ink with { A = 0.75f };
        theme.SetStylebox("focus", "LineEdit", fieldFocus);
        theme.SetColor("caret_color", "LineEdit", Ink);
        theme.SetColor("selection_color", "LineEdit", Ink with { A = 0.2f });

        // ---- menus ----
        theme.SetColor("font_color", "PopupMenu", Ink);
        theme.SetColor("font_hover_color", "PopupMenu", Ink);
        theme.SetColor("font_disabled_color", "PopupMenu", InkMuted with { A = 0.6f });
        theme.SetStylebox("hover", "PopupMenu", hover);

        // ---- the scrollbars: a faint inked groove, a short wound roll ----
        theme.SetStylebox("scroll", "VScrollBar", PaintedPatch.Make(Tex("controls/scroll-track.png"), 0.25f, new Vector4(0, 12, 0, 12), new Vector4(3, 3, 3, 3)));
        theme.SetStylebox("grabber", "VScrollBar", PaintedPatch.Make(Tex("controls/scroll-grabber.png"), 0.25f, new Vector4(0, 12, 0, 12), new Vector4(3, 3, 3, 3)));
        theme.SetStylebox("grabber_highlight", "VScrollBar", PaintedPatch.Make(Tex("controls/scroll-grabber-hover.png"), 0.25f, new Vector4(0, 12, 0, 12), new Vector4(3, 3, 3, 3)));
        theme.SetStylebox("grabber_pressed", "VScrollBar", PaintedPatch.Make(Tex("controls/scroll-grabber-held.png"), 0.25f, new Vector4(0, 12, 0, 12), new Vector4(3, 3, 3, 3)));

        // ---- the rules ----
        theme.SetStylebox("separator", "HSeparator", PaintedPatch.Make(Tex("controls/rule-h.png"), 0.25f, new Vector4(16, 0, 16, 0), new Vector4(0, 1, 0, 1)));
        theme.SetStylebox("separator", "VSeparator", PaintedPatch.Make(Tex("controls/rule-v.png"), 0.25f, new Vector4(0, 16, 0, 16), new Vector4(1, 0, 1, 0)));

        // ---- the roster ----
        theme.SetStylebox("panel", "Tree", new StyleBoxEmpty());
        theme.SetColor("font_color", "Tree", Ink);
        theme.SetColor("font_selected_color", "Tree", Ink);
        theme.SetColor("title_button_color", "Tree", InkMuted);
        theme.SetColor("guide_color", "Tree", Ink with { A = 0.15f });
        var picked = new StyleBoxFlat { BgColor = Ink with { A = 0.10f } };
        theme.SetStylebox("selected", "Tree", picked);
        theme.SetStylebox("selected_focus", "Tree", picked);
        theme.SetStylebox("cursor", "Tree", new StyleBoxEmpty());
        theme.SetStylebox("cursor_unfocused", "Tree", new StyleBoxEmpty());
        theme.SetStylebox("title_button_normal", "Tree", new StyleBoxEmpty());
        theme.SetStylebox("title_button_hover", "Tree", hover);
        theme.SetStylebox("title_button_pressed", "Tree", new StyleBoxEmpty());
    }
}

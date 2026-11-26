using Kith.Sim.Logging;
using Godot;

namespace Kith.Game;

/// <summary>
/// The look's one door (<c>ui-art.md §4</c>, §9.3 step 1, A110): every skin and colour the interface draws with — the
/// panels, the cards, the pills and bars, the text's roles, the states' colours, the log's — <b>asked for here by
/// name</b>, and answered from the project theme's <see cref="Type"/> entries when the theme names them, or with
/// today's slate when it does not.
/// </summary>
/// <remarks>
/// <para>
/// ⭐ <b>No theme naming an entry means the screen is what it was, to the byte</b> — every fallback below is the literal
/// that stood at its site before the seam (<c>ui theme:</c> holds them). A look is then a theme file and textures,
/// the art lane's alone; <c>Main.cs</c> and <c>Main.Cards.cs</c> only ever ask for a role.
/// </para>
/// <para>
/// <b>Text is tinted through <see cref="Tint"/></b>, never by setting <c>Modulate</c> at the site: on slate the
/// text is white and a tint is a multiply, which a parchment look's ink cannot take (amber over ink is still ink).
/// One helper is the one place that changes when a look says how it tints. <see cref="TintOf"/> reads it back for
/// the two sites that keep a state in the colour.
/// </para>
/// <para>
/// Skins are made once a theme and kept (CLAUDE.md's rule — a card is re-skinned every refresh). <see cref="Use"/>
/// changes the theme, and the kept ones are let go with it.
/// </para>
/// </remarks>
internal static class UiLook
{
    /// <summary>The theme type every entry is looked up under.</summary>
    internal const string Type = "KithLook";

    private static Theme? _theme;
    private static Theme? _worn;
    private static bool _read;
    private static readonly Dictionary<string, StyleBox> Kept = new();

    /// <summary>The room inside a slate panel, logical px.</summary>
    private const int SlateInside = 6;

    /// <summary>
    /// The theme the look reads: the one <see cref="Wear"/> put on (<see cref="LookLoader"/>, at start), else the project's
    /// (<c>[gui] theme/custom</c>), unless <see cref="Use"/> says otherwise for a while.
    /// </summary>
    private static Theme? Theme
    {
        get
        {
            if (!_read)
            {
                _theme = _worn ?? ThemeDB.GetProjectTheme();
                _read = true;
            }

            return _theme;
        }
    }

    /// <summary>The look the game wears — put on once, at start, before any window is built.</summary>
    internal static void Wear(Theme? theme)
    {
        _worn = theme;
        PutBack();
    }

    /// <summary>Whether the game wears a look other than slate — for the probe.</summary>
    internal static bool WearsALook => _worn is not null || ThemeDB.GetProjectTheme() is not null;

    /// <summary>Read from this theme for a while (null: today's slate everywhere) — the probe's; <see cref="PutBack"/> ends it.</summary>
    internal static void Use(Theme? theme)
    {
        _theme = theme;
        _read = true;
        Kept.Clear();
    }

    /// <summary>The look the game wears again.</summary>
    internal static void PutBack()
    {
        _read = false;
        Kept.Clear();
    }

    private static Color Named(string name, Color slate) =>
        Theme is { } theme && theme.HasColor(name, Type) ? theme.GetColor(name, Type) : slate;

    private static StyleBox? NamedBox(string name) =>
        Theme is { } theme && theme.HasStylebox(name, Type) ? theme.GetStylebox(name, Type) : null;

    // ---------------------------------------------------------------
    //  The text's roles
    // ---------------------------------------------------------------

    /// <summary>The theme type variations a look gives its header face (A120): a label's, a button's.</summary>
    internal const string HeaderLabel = "HeaderLabel";

    internal const string HeaderButton = "HeaderButton";

    /// <summary>The theme type variation of a screen's one primary button — the new valley's <i>Found</i> (A147).</summary>
    internal const string PrimaryButton = "PrimaryButton";

    /// <summary>The title screen's items (A180): the heading face, the rubric under the pointer and the keyboard.</summary>
    internal const string TitleItem = "TitleItem";

    /// <summary>The title screen's one primary item (A180; A242: <see cref="TitleItem"/> larger and always in red).</summary>
    internal const string TitlePrimary = "TitlePrimary";

    /// <summary>The face words are read in on the new-valley screen (A147, Joe's Cardo); null on slate.</summary>
    internal static Font? ReadingFace => NamedFont("reading_font");

    /// <summary>The blackletter for the new-valley screen's title and headings (A147); null on slate.</summary>
    internal static Font? Blackletter => NamedFont("blackletter");

    private static Font? NamedFont(string name) =>
        Theme is { } theme && theme.HasFont(name, Type) ? theme.GetFont(name, Type) : null;

    /// <summary>
    /// ⭐ An amount in lining figures (<c>hud.md §12</c>, D881; call 7): set in the reading face, Cardo, whose 0 is a 0 —
    /// Grenze Gotisch's old-style 0 reads as <i>o</i>. For amounts on their own (the strip, <c>more ▾</c>, the stock
    /// limits, the professions' numbers, the build queue's columns, the dev bar); figures inside a sentence stay in the
    /// body face. On slate the mark names nothing and the label keeps its face.
    /// </summary>
    internal static void Figures(Control control)
    {
        if (ReadingFace is Font cardo)
        {
            control.AddThemeFontOverride("font", cardo);
        }
    }

    /// <summary>
    /// A painted control the look names — a card's pin, normal, under the pointer and pinned (<c>hud.md §8.3</c>, A217) — or
    /// null on slate, where the control keeps the glyph it had.
    /// </summary>
    internal static Texture2D? NamedIcon(string name) =>
        Theme is { } theme && theme.HasIcon(name, Type) ? theme.GetIcon(name, Type) : null;

    /// <summary>
    /// Mark this label or button as a header — a window's title, a card's, a card's section head — so a look that names a
    /// header face sets it in that (A120, Joe's Metamorphous). On slate the mark names nothing and the text is as it was.
    /// </summary>
    internal static void Header(Control control) =>
        control.ThemeTypeVariation = control is Button ? HeaderButton : HeaderLabel;

    /// <summary>Mark every button and label directly in this row as a header — the control bar's main row (A120).</summary>
    internal static void HeadersIn(Node row)
    {
        foreach (Node child in row.GetChildren())
        {
            if (child is Button or Label)
            {
                Header((Control)child);
            }
        }
    }

    /// <summary>Text as it is — the untinted state a tint goes back to.</summary>
    internal static Color Text => Named("text", Colors.White);

    /// <summary>Quiet text: a label, a unit, a hint (<c>Muted</c>, 55 %).</summary>
    internal static Color Faint => Named("text_faint", new Color(1, 1, 1, 0.55f));

    /// <summary>A secondary line that still has to be read: a tool's uses, a coat, wear (70 %).</summary>
    internal static Color Soft => Named("text_soft", new Color(1, 1, 1, 0.7f));

    /// <summary>A screen's under-line: the new game's notes, the pause screen's where (65 %).</summary>
    internal static Color Hint => Named("text_hint", new Color(1, 1, 1, 0.65f));

    /// <summary>What is there but not for now: a log filter off, the frame counter, a tech card not reached (45 %).</summary>
    internal static Color Dim => Named("text_dim", new Color(1, 1, 1, 0.45f));

    /// <summary>A handle's dots (35 %).</summary>
    internal static Color Ghost => Named("text_ghost", new Color(1, 1, 1, 0.35f));

    /// <summary>The tech tree's names (80 %).</summary>
    internal static Color Strong => Named("text_strong", new Color(1, 1, 1, 0.8f));

    // ---------------------------------------------------------------
    //  The states
    // ---------------------------------------------------------------

    /// <summary>Working, met, fine — the card's light and the bars' fill.</summary>
    internal static Color Working => Named("working", new Color("#7fbf7a"));

    /// <summary>Stopped, unmet, wanting — the amber that says something needs a look.</summary>
    internal static Color Stopped => Named("stopped", new Color("#ffc759"));

    /// <summary>The edge of the card being worked with.</summary>
    internal static Color SelectedEdge => Named("selected_edge", new Color("#ffd76a"));

    /// <summary>The edge of every other card.</summary>
    internal static Color CardEdge => Named("card_edge", new Color("#3a3f47"));

    /// <summary>A gift owed — the free library, smithy or town hall, and an untried building, in the bar.</summary>
    internal static Color Owed => Named("owed", new Color(1f, 0.85f, 0.4f));

    /// <summary>The bar's tab that is held open.</summary>
    internal static Color Held => Named("held", new Color(1f, 0.85f, 0.4f));

    /// <summary>New, until clicked (hud.md §8.3, D880): the gold word; slate's owed gold.</summary>
    internal static Color New => Named("new", new Color(1f, 0.85f, 0.4f));

    /// <summary>The gold seal on a new thing's icon (A257), or null on slate.</summary>
    internal static Texture2D? NewSeal => Theme is { } theme && theme.HasIcon("new_seal", Type) ? theme.GetIcon("new_seal", Type) : null;

    /// <summary>A refusal or a failure said in words: a save that failed, a valley that will not found.</summary>
    internal static Color Refused => Named("refused", new Color(1f, 0.55f, 0.45f));

    /// <summary>A save that worked.</summary>
    internal static Color Saved => Named("saved", new Color(0.62f, 0.86f, 0.58f));

    /// <summary>A tech the village knows.</summary>
    internal static Color Known => Named("known", new Color(0.62f, 0.86f, 0.58f));

    /// <summary>The tech tree's lines between cards.</summary>
    internal static Color TreeEdge => Named("tree_edge", new Color(1f, 1f, 1f, 0.35f));

    /// <summary>An empty cell or a bar's unfilled track.</summary>
    internal static Color Empty => Named("empty", new Color(1, 1, 1, 0.10f));

    /// <summary>The log's day stamp.</summary>
    internal static Color Stamp => Named("stamp", new Color("#7f8c94"));

    /// <summary>The new-game preview's mark where the village will be founded.</summary>
    internal static Color FoundingMark => Named("founding_mark", new Color(0.98f, 0.78f, 0.46f));

    /// <summary>The screen behind the pause sheet.</summary>
    internal static Color PauseShade => Named("pause_shade", new Color(0f, 0f, 0f, 0.55f));

    /// <summary>What a log category reads as (Joe: <i>"deaths, important events in colors, general info in white"</i>).</summary>
    internal static Color Log(LogCategory category) => category switch
    {
        LogCategory.Death => Named("log_death", new Color(0.85f, 0.40f, 0.40f)),
        LogCategory.Life => Named("log_life", new Color(0.55f, 0.80f, 0.55f)),
        LogCategory.Discovery => Named("log_discovery", new Color(0.91f, 0.77f, 0.41f)),
        LogCategory.Warning => Named("log_warning", new Color(1f, 0.78f, 0.35f)),
        LogCategory.Building => Named("log_building", new Color(0.62f, 0.72f, 0.85f)),
        LogCategory.Season => Named("log_season", new Color(0.60f, 0.62f, 0.60f)),
        _ => Named("log_ordinary", new Color(0.88f, 0.88f, 0.86f)),
    };

    // ---------------------------------------------------------------
    //  Tinting text
    // ---------------------------------------------------------------

    /// <summary>
    /// Whether the look colours text by its font colour (ink on parchment) rather than by multiplying white text
    /// (slate) — the theme's <c>tints_by_font</c> constant.
    /// </summary>
    internal static bool TintsByFont =>
        Theme is { } theme && theme.HasConstant("tints_by_font", Type) && theme.GetConstant("tints_by_font", Type) != 0;

    /// <summary>
    /// Give this text (or control) a role's colour — the one way a site tints. On slate a multiply of the white text; on a
    /// look that <see cref="TintsByFont"/>, a label's or a button's font colour, since a multiply cannot lighten ink or
    /// turn it amber.
    /// </summary>
    internal static void Tint(CanvasItem item, Color colour)
    {
        if (!TintsByFont)
        {
            item.Modulate = colour;
            return;
        }

        switch (item)
        {
            case Label label:
                label.AddThemeColorOverride("font_color", colour);
                break;
            case Button button:
                button.AddThemeColorOverride("font_color", colour);
                button.AddThemeColorOverride("font_pressed_color", colour);
                button.AddThemeColorOverride("font_hover_pressed_color", colour);
                button.AddThemeColorOverride("font_focus_color", colour);
                break;
            case RichTextLabel text:
                text.AddThemeColorOverride("default_color", colour);
                break;
            default:
                item.Modulate = colour;
                break;
        }
    }

    /// <summary>The colour <see cref="Tint"/> last gave it.</summary>
    internal static Color TintOf(CanvasItem item) => !TintsByFont ? item.Modulate : item switch
    {
        Label or Button => ((Control)item).GetThemeColor("font_color"),
        RichTextLabel text => text.GetThemeColor("default_color"),
        _ => item.Modulate,
    };

    /// <summary>A whole thing faded, not tinted: the tech tree's card not yet seen (45 % on slate, as it was).</summary>
    internal static Color Fade => Named("fade", new Color(1f, 1f, 1f, 0.45f));

    // ---------------------------------------------------------------
    //  The skins
    // ---------------------------------------------------------------

    /// <summary>
    /// A window's sheet. <paramref name="alpha"/> is how much of the map shows through on slate (the bar 0.94, a
    /// spanning panel 0.72); a theme's <c>panel</c> is used as it is.
    /// </summary>
    internal static StyleBox Panel(float alpha = 1f)
    {
        if (NamedBox("panel") is { } named)
        {
            return named;
        }

        return SlatePanel(alpha);
    }

    /// <summary>
    /// A bar across the screen — the control bar, the top bar's boxes (A113): a scroll on its side in a look that paints
    /// one, the same slate as a window where it does not.
    /// </summary>
    internal static StyleBox Strip(float alpha = 1f) => NamedBox("strip") ?? Panel(alpha);

    /// <summary>
    /// One of the top bar's boxes (the goods, the villagers). Its own role because that bar is full — 6 px to spare at
    /// 75 % (A111) — so a look gives it a frame no wider than slate's.
    /// </summary>
    internal static StyleBox TopBox(float alpha = 1f) => NamedBox("top_box") ?? Panel(alpha);

    /// <summary>
    /// How much wider than slate a window (<paramref name="card"/> false) or a card is in this look, so the words inside
    /// keep slate's room: the frame's sides less slate's 6 + 6. Zero on slate, and zero in a look whose theme does not set
    /// <c>grows_by_frame</c> — then the frame's room comes out of the words instead (Joe's call, <c>ui-art.md §9.8</c>).
    /// </summary>
    internal static float FrameWidth(bool card)
    {
        if (Theme is not { } theme || !theme.HasConstant("grows_by_frame", Type) || theme.GetConstant("grows_by_frame", Type) == 0)
        {
            return 0f;
        }

        StyleBox skin = card ? Card(false) : Panel();
        // A card may also want room for its look's title face: card_room (A120, Metamorphous, a broad face, set a
        // person's widest own name 18 px past the card).
        float titles = card && theme.HasConstant("card_room", Type) ? theme.GetConstant("card_room", Type) : 0f;
        return Mathf.Max(0f, skin.ContentMarginLeft + skin.ContentMarginRight - (2 * SlateInside)) + titles;
    }

    /// <summary>
    /// A tech tree card in its state — <c>known</c>, <c>insight</c>, <c>fogged</c> — when the look paints one; null on slate,
    /// where the card keeps the theme's default panel as it always has.
    /// </summary>
    internal static StyleBox? TechCard(string state) => NamedBox("tech_" + state);

    private static StyleBoxFlat SlatePanel(float alpha)
    {
        var skin = new StyleBoxFlat
        {
            BgColor = new Color(0.08f, 0.09f, 0.10f, alpha),
            BorderColor = new Color(0.58f, 0.53f, 0.40f, 0.55f),
        };

        skin.SetBorderWidthAll(1);
        skin.SetContentMarginAll(SlateInside);
        skin.SetCornerRadiusAll(3);
        return skin;
    }

    /// <summary>
    /// A card's frame, selected or not; <paramref name="margin"/> above zero gives it that much room inside (a
    /// person's, D432). Kept: a card is re-skinned on every refresh.
    /// </summary>
    internal static StyleBox Card(bool selected, int margin = 0)
    {
        string key = $"card:{selected}:{margin}";
        if (Kept.TryGetValue(key, out StyleBox? kept))
        {
            return kept;
        }

        StyleBox skin;
        if (NamedBox(selected ? "card_selected" : "card") is { } named)
        {
            skin = named;
            if (margin > SlateInside)
            {
                // A painted card's room is its chrome's (clear of the roll and the bar); a person's gets the same extra
                // on top of it that slate gives (12 where slate's is 6), not 12 in place of it.
                float extra = margin - SlateInside;
                skin = (StyleBox)named.Duplicate();
                skin.ContentMarginLeft = named.ContentMarginLeft + extra;
                skin.ContentMarginRight = named.ContentMarginRight + extra;
                skin.ContentMarginTop = named.ContentMarginTop + extra;
                skin.ContentMarginBottom = named.ContentMarginBottom + extra;
            }
        }
        else
        {
            var flat = SlatePanel(0.96f);
            flat.BorderWidthTop = flat.BorderWidthBottom = flat.BorderWidthLeft = flat.BorderWidthRight = 1;
            flat.BorderColor = selected ? SelectedEdge : CardEdge;
            if (margin > 0)
            {
                flat.SetContentMarginAll(margin);
            }

            skin = flat;
        }

        Kept[key] = skin;
        return skin;
    }

    /// <summary>A need's pill — <i>Fed</i>, <i>Warm</i>, <i>Housed</i> — met or not.</summary>
    internal static StyleBox Pill(bool met)
    {
        string key = met ? "pill_met" : "pill_unmet";
        if (Kept.TryGetValue(key, out StyleBox? kept))
        {
            return kept;
        }

        StyleBox skin = NamedBox(key) ?? FlatPill(met ? Working : Stopped);
        Kept[key] = skin;
        return skin;
    }

    private static StyleBoxFlat FlatPill(Color colour)
    {
        var skin = new StyleBoxFlat
        {
            BgColor = colour with { A = 0.18f },
            BorderColor = colour with { A = 0.7f },
        };

        skin.SetBorderWidthAll(1);
        skin.SetCornerRadiusAll(9);
        skin.ContentMarginLeft = skin.ContentMarginRight = 8;
        skin.ContentMarginTop = skin.ContentMarginBottom = 1;
        return skin;
    }

    /// <summary>A thin bar's track (<paramref name="fill"/> false) or its fill, in the working green.</summary>
    internal static StyleBox Bar(bool fill)
    {
        string key = fill ? "bar_fill" : "bar_track";
        if (Kept.TryGetValue(key, out StyleBox? kept))
        {
            return kept;
        }

        StyleBox? skin = NamedBox(key);
        if (skin is null)
        {
            var flat = new StyleBoxFlat { BgColor = fill ? Working : Empty };
            flat.SetCornerRadiusAll(3);
            flat.SetContentMarginAll(0);
            skin = flat;
        }

        Kept[key] = skin;
        return skin;
    }

    /// <summary>The floor behind the title screen and the new-game screen.</summary>
    internal static StyleBox Backdrop() =>
        NamedBox("backdrop") ?? new StyleBoxFlat { BgColor = new Color(0.07f, 0.08f, 0.08f) };

    /// <summary>The band down the left of the title screen, so the words read over any valley.</summary>
    internal static StyleBox TitleBand() =>
        NamedBox("title_band") ?? new StyleBoxFlat
        {
            BgColor = new Color(0.05f, 0.06f, 0.06f, 0.82f),
            ContentMarginLeft = 40,
            ContentMarginRight = 40,
        };

    /// <summary>The pause screen's sheet.</summary>
    internal static StyleBox PauseSheet() =>
        NamedBox("pause_sheet") ?? new StyleBoxFlat
        {
            BgColor = new Color(0.09f, 0.10f, 0.10f, 0.97f),
            ContentMarginLeft = 24,
            ContentMarginRight = 24,
            ContentMarginTop = 20,
            ContentMarginBottom = 20,
            CornerRadiusTopLeft = 6,
            CornerRadiusTopRight = 6,
            CornerRadiusBottomLeft = 6,
            CornerRadiusBottomRight = 6,
        };
}

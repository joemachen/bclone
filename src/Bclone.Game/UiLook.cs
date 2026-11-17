using Bclone.Sim.Logging;
using Godot;

namespace Bclone.Game;

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
    internal const string Type = "BcloneLook";

    private static Theme? _theme;
    private static bool _read;
    private static readonly Dictionary<string, StyleBox> Kept = new();

    /// <summary>The theme the look reads — the project's (<c>[gui] theme/custom</c>) unless <see cref="Use"/> said otherwise.</summary>
    private static Theme? Theme
    {
        get
        {
            if (!_read)
            {
                _theme = ThemeDB.GetProjectTheme();
                _read = true;
            }

            return _theme;
        }
    }

    /// <summary>Read from this theme from now on (null: today's slate everywhere) — the probe's, and a look switched in play.</summary>
    internal static void Use(Theme? theme)
    {
        _theme = theme;
        _read = true;
        Kept.Clear();
    }

    /// <summary>The project's theme again.</summary>
    internal static void UseTheProjectTheme()
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

    /// <summary>Give this text (or control) a role's colour — the one way a site tints.</summary>
    internal static void Tint(CanvasItem item, Color colour) => item.Modulate = colour;

    /// <summary>The colour <see cref="Tint"/> last gave it.</summary>
    internal static Color TintOf(CanvasItem item) => item.Modulate;

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

        var skin = new StyleBoxFlat
        {
            BgColor = new Color(0.08f, 0.09f, 0.10f, alpha),
            BorderColor = new Color(0.58f, 0.53f, 0.40f, 0.55f),
        };

        skin.SetBorderWidthAll(1);
        skin.SetContentMarginAll(6);
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
            if (margin > 0)
            {
                skin = (StyleBox)named.Duplicate();
                skin.SetContentMarginAll(margin);
            }
        }
        else
        {
            var flat = (StyleBoxFlat)Panel(0.96f);
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

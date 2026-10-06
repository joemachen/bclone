using Godot;

namespace Bclone.Game;

/// <summary>
/// ⭐ The pause screen — Esc, once nothing nearer is open (D516, `specs/title-and-pause.md §5`).
/// </summary>
/// <remarks>
/// <para>
/// <b>Lays out; Main acts.</b> Every row is a callback Main hands in, so the saving, the loading and the ways
/// out stay in <c>Main.Saves.cs</c> beside the autosave they all go through — the title's shape.
/// </para>
/// <para>
/// A full-window dim that stops the mouse, so the map takes no click while it is up (the map reads
/// <c>_GuiInput</c>, and a control on top that stops the mouse is never under it). The panel in the middle is
/// drawn at the player's UI size, as every other window is.
/// </para>
/// </remarks>
public sealed partial class PauseScreen : Control
{
    private PanelContainer _panel = null!;
    private float _scale = 1f;

    /// <summary>Built by Main, which fills it row by row (<c>Main.Pause.cs</c>).</summary>
    public PauseScreen()
    {
    }

    /// <summary>For the probe: the panel's body, where the Save row sits.</summary>
    internal VBoxContainer Body { get; private set; } = null!;

    /// <summary>The line under <i>Paused</i>: the village and its calendar.</summary>
    internal Label Where { get; private set; } = null!;

    public override void _Ready()
    {
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        MouseFilter = MouseFilterEnum.Stop;

        var dim = new ColorRect { Color = new Color(0f, 0f, 0f, 0.55f), MouseFilter = MouseFilterEnum.Ignore };
        dim.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        AddChild(dim);

        _panel = new PanelContainer { CustomMinimumSize = new Vector2(320, 0) };
        _panel.AddThemeStyleboxOverride("panel", new StyleBoxFlat
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
        });
        AddChild(_panel);

        Body = new VBoxContainer();
        Body.AddThemeConstantOverride("separation", 8);
        _panel.AddChild(Body);

        var heading = new Label { Text = "Paused", HorizontalAlignment = HorizontalAlignment.Center };
        heading.AddThemeFontSizeOverride("font_size", 26);
        Body.AddChild(heading);

        Where = new Label { HorizontalAlignment = HorizontalAlignment.Center, Modulate = new Color(1, 1, 1, 0.65f) };
        Where.AddThemeFontSizeOverride("font_size", 13);
        Body.AddChild(Where);
        Body.AddChild(new Control { CustomMinimumSize = new Vector2(0, 6) });
    }

    /// <summary>A full-width button in the panel.</summary>
    internal Button AddButton(string text, Action pressed, string? tooltip = null)
    {
        var button = new Button { Text = text, CustomMinimumSize = new Vector2(0, 34), TooltipText = tooltip ?? string.Empty };
        button.AddThemeFontSizeOverride("font_size", 16);
        button.Pressed += pressed;
        Body.AddChild(button);
        return button;
    }

    /// <summary>A gap between groups of rows.</summary>
    internal void AddGap() => Body.AddChild(new Control { CustomMinimumSize = new Vector2(0, 6) });

    /// <summary>Drawn at the player's UI size, centred — set each time it opens, since the dial may have moved.</summary>
    internal void DrawAt(float scale)
    {
        _scale = scale;
        _panel.Scale = new Vector2(scale, scale);
    }

    public override void _Process(double delta)
    {
        if (!Visible)
        {
            return;
        }

        // Centred by hand: a container lays out at scale 1, and the panel is drawn at the UI size.
        Vector2 drawn = _panel.Size * _scale;
        _panel.Position = ((Size - drawn) / 2f).Floor();
    }
}

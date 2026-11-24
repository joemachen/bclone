using Godot;

namespace Kith.Game;

/// <summary>
/// ⭐ The paint lifts into the game (A236, `title-snapshot.md §6a`; Joe: <i>"when the player clicks continue, can we transition the
/// background image smoothly to the playable version?"</i>): the panel slides away, the save opens behind the still picture, the
/// paint dries off it, and the live map — opened on the picture's own frame — takes over, and the panels
/// fade in. ⭐ <b>The view stays the snapshot's</b> (A237, Joe: <i>"dont zoom into the village. leave the view as is in the snapshot"</i>).
/// </summary>
/// <remarks>
/// <para>
/// <b>A slow load is a held picture, never a frozen animation</b>: the save opens on a frame when nothing moves, and nothing moves
/// again until the map has drawn itself. While it is held, <see cref="LoadingWords"/> says why (A237).
/// </para>
/// <para>
/// <b>Where the picture is not of the save opened</b> (<see cref="PictureIsOf"/>) the paint still lifts, then the picture fades to
/// the village on its usual framing.
/// </para>
/// </remarks>
public sealed partial class TitleScreen
{
    /// <summary>
    /// Each part's length: the panel away, the paint off, the picture to the map, the panels in — twice A236's (A237, Joe: <i>"slow
    /// down the transition by about half - i want the user to see it"</i>).
    /// </summary>
    internal const double SlideSeconds = 0.6, LiftSeconds = 2.2, FadeSeconds = 0.6, PanelsSeconds = 1.8;

    /// <summary>How long the words take to come and go.</summary>
    private const double WordsSeconds = 0.4;

    /// <summary>What is said over the still picture while the save opens (A237, Joe: <i>"maybe 'painting village' or something"</i>).</summary>
    internal const string LoadingWords = "Painting the village…";

    /// <summary>The longest one frame counts for: a frame that hitched must not skip what it held up.</summary>
    private const double LongestStep = 1d / 30d;

    /// <summary>The map is drawn at least this often before the paint lifts, and waited for no longer than the most.</summary>
    private const int FewestSettling = 4, MostSettling = 150;

    /// <summary>Where the lift has got to.</summary>
    internal enum LiftStep
    {
        /// <summary>The title, as it opens.</summary>
        None,

        /// <summary>The panel sliding off to the left.</summary>
        Sliding,

        /// <summary>The panel gone; the save is opened on the next frame.</summary>
        Asking,

        /// <summary>Over the opened village, until it has drawn itself.</summary>
        Settling,

        /// <summary>The paint coming off the picture.</summary>
        Lifting,

        /// <summary>The picture going from over the map.</summary>
        Fading,

        /// <summary>The panels fading in; the player has the game.</summary>
        Arriving,

        /// <summary>Over.</summary>
        Done,
    }

    private string _toOpen = string.Empty;
    private double _into;
    private int _settled;
    private Control? _shield;
    private VillageMap? _under;
    private IReadOnlyList<CanvasItem> _panels = Array.Empty<CanvasItem>();
    private (Vector2 Centre, float Ppt)? _frame;
    private PanelContainer? _words;

    /// <summary>For the probe: how far the lift has got.</summary>
    internal LiftStep Lift { get; private set; }

    /// <summary>For the probe: how far the loading words show, 0 with none up.</summary>
    internal float WordsShown => _words is { Visible: true } ? _words.Modulate.A : 0f;

    /// <summary>For the probe: the paint's strength now.</summary>
    internal float Strength => _backdrop.Material is ShaderMaterial paint ? paint.GetShaderParameter("strength").AsSingle() : 0f;

    /// <summary>
    /// Pure but for the two files' times: is <paramref name="picture"/> the picture of the save at <paramref name="opened"/>? It is
    /// when that is its folder's newest save and the picture was written after it — a picture that failed, or a save made since,
    /// leaves an older one behind.
    /// </summary>
    internal static bool PictureIsOf(string? picture, string opened, IReadOnlyList<SaveListing> saves)
    {
        string? folder = System.IO.Path.GetDirectoryName(opened);
        return picture is not null && System.IO.Path.GetDirectoryName(picture) == folder
            && saves.FirstOrDefault(s => System.IO.Path.GetDirectoryName(s.Path) == folder)?.Path == opened
            && System.IO.File.Exists(opened)
            && System.IO.File.GetLastWriteTimeUtc(picture) >= System.IO.File.GetLastWriteTimeUtc(opened);
    }

    /// <summary>
    /// Pure: the camera that shows the village as its picture does, in a window the picture covers (<c>KeepAspectCovered</c>): the
    /// picture's own middle, at its zoom times the cover's scale.
    /// </summary>
    internal static (Vector2 Centre, float Ppt) FrameOf(Vector2 heart, Vector2 window)
    {
        var picture = (Vector2)TitleSnapshot.PictureSize;
        return (TitleSnapshot.CentreFor(heart), TitleSnapshot.PixelsPerTile * Mathf.Max(window.X / picture.X, window.Y / picture.Y));
    }

    private static float Smooth(float t) => t * t * (3f - (2f * t));

    /// <summary><i>Continue</i> and <i>Load…</i>: the panel slides away first, so the load holds a still picture.</summary>
    private void SlideAwayThenOpen(string path)
    {
        if (Lift != LiftStep.None)
        {
            return;
        }

        _toOpen = path;
        GetViewport().GuiReleaseFocus();
        _shield = new Control { MouseFilter = MouseFilterEnum.Stop };
        _shield.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        _shield.GuiInput += e =>
        {
            if (e is InputEventMouseButton { Pressed: true })
            {
                Hurry();
            }
        };
        AddChild(_shield);
        PutUpTheWords();
        Go(LiftStep.Sliding);
    }

    /// <summary>The loading words on a slip of the HUD's parchment, low in the middle, over the picture and clear of the village.</summary>
    private void PutUpTheWords()
    {
        var label = new Label { Text = LoadingWords, ThemeTypeVariation = UiLook.HeaderLabel };
        label.AddThemeFontSizeOverride("font_size", 22);
        _words = new PanelContainer
        {
            AnchorLeft = 0.5f,
            AnchorRight = 0.5f,
            AnchorTop = 0.9f,
            AnchorBottom = 0.9f,
            GrowHorizontal = GrowDirection.Both,
            GrowVertical = GrowDirection.Both,
            MouseFilter = MouseFilterEnum.Ignore,
        };
        _words.AddThemeStyleboxOverride("panel", UiLook.Strip());
        var margin = new MarginContainer();
        margin.AddThemeConstantOverride("margin_left", 18);
        margin.AddThemeConstantOverride("margin_right", 18);
        margin.AddThemeConstantOverride("margin_top", 4);
        margin.AddThemeConstantOverride("margin_bottom", 4);
        margin.AddChild(label);
        _words.AddChild(margin);
        AddChild(_words);
        SetWords(0f);
    }

    private void SetWords(float alpha)
    {
        if (_words is not null)
        {
            _words.Modulate = new Color(1f, 1f, 1f, alpha);
            _words.Visible = alpha > 0f;
        }
    }

    /// <summary>The save would not open: the panel comes back, with the reason on it.</summary>
    private void BackToTheTitle()
    {
        _shield?.QueueFree();
        _shield = null;
        _words?.QueueFree();
        _words = null;
        Lift = LiftStep.None;
        Band.Visible = true;
        Band.Modulate = Colors.White;
        PlaceThePanel();
        PrimaryItem.GrabFocus();
    }

    /// <summary>
    /// The village is open under this title: <paramref name="hud"/> is hidden until the picture has gone, and the map, when the picture
    /// is of the save opened, is put on the picture's frame before it first draws, from the village's <paramref name="heart"/>.
    /// </summary>
    internal void LiftOver(VillageMap map, Vector2? heart, string opened, IReadOnlyList<CanvasItem> hud)
    {
        _under = map;
        _panels = hud;
        Show(0f);
        Band.Visible = false;
        if (PictureIsOf(VillagePicture, opened, Saves) && heart is Vector2 at)
        {
            _frame = FrameOf(at, Size);
            map.LookAt(_frame.Value.Centre, _frame.Value.Ppt);
        }

        Go(LiftStep.Settling);
    }

    public override void _Process(double delta) => StepTheLift(delta);

    /// <summary>One frame of the lift (the probe steps it too).</summary>
    internal void StepTheLift(double delta)
    {
        if (Lift is LiftStep.None or LiftStep.Done)
        {
            return;
        }

        bool hitched = delta > 0.1;
        _into += Math.Min(delta, LongestStep);
        if (_frame is { } held && _under is not null && Lift is LiftStep.Settling or LiftStep.Lifting or LiftStep.Fading)
        {
            _under.LookAt(held.Centre, held.Ppt); // WASD is polled: the map stays on the picture's frame until it is seen
        }

        switch (Lift)
        {
            case LiftStep.Sliding:
                float away = Smooth(Part(SlideSeconds));
                float width = Band.Size.X;
                Band.OffsetLeft = -width * away;
                Band.OffsetRight = width - (width * away);
                Band.Modulate = new Color(1f, 1f, 1f, 1f - away);
                SetWords((float)Math.Clamp((_into - SlideSeconds + WordsSeconds) / WordsSeconds, 0d, 1d));
                if (Part(SlideSeconds) >= 1f)
                {
                    Band.Visible = false;
                    Go(LiftStep.Asking);
                }

                break;

            case LiftStep.Asking:
                LoadAsked?.Invoke(_toOpen); // Main answers with LiftOver, or with RefuseTheLoad
                break;

            case LiftStep.Settling:
                _settled++;
                if ((_settled >= FewestSettling && !hitched && _under!.EveryoneIsPainted) || _settled >= MostSettling)
                {
                    Go(LiftStep.Lifting);
                }

                break;

            case LiftStep.Lifting:
                SetStrength(1f - Smooth(Part(LiftSeconds)));
                SetWords(1f - Part(WordsSeconds));
                if (Part(LiftSeconds) >= 1f)
                {
                    _floor.Visible = false; // the picture covers it, and it would hold the map back
                    Go(LiftStep.Fading);
                }

                break;

            case LiftStep.Fading:
                Modulate = new Color(1f, 1f, 1f, 1f - Part(FadeSeconds));
                if (Part(FadeSeconds) >= 1f)
                {
                    HandTheGameOver();
                }

                break;

            case LiftStep.Arriving:
                Show(Part(PanelsSeconds));
                if (Part(PanelsSeconds) >= 1f)
                {
                    Finish();
                }

                break;
        }
    }

    /// <summary>A key or a click while the paint lifts: straight to the game.</summary>
    private void Hurry()
    {
        if (Lift is not (LiftStep.Lifting or LiftStep.Fading))
        {
            return;
        }

        HandTheGameOver();
        Finish();
    }

    /// <summary>The picture is gone: the mouse, the keys and the camera are the game's, the view where the picture had it.</summary>
    private void HandTheGameOver()
    {
        Visible = false;
        MouseFilter = MouseFilterEnum.Ignore;
        if (_shield is not null)
        {
            _shield.MouseFilter = MouseFilterEnum.Ignore;
        }

        Go(LiftStep.Arriving);
    }

    private void Finish()
    {
        Show(1f);
        Lift = LiftStep.Done;
        GetParent()?.RemoveChild(this);
        QueueFree();
    }

    private void Show(float alpha)
    {
        foreach (CanvasItem panel in _panels)
        {
            if (IsInstanceValid(panel))
            {
                panel.Modulate = new Color(panel.Modulate, alpha);
            }
        }
    }

    private void SetStrength(float strength) => (_backdrop.Material as ShaderMaterial)?.SetShaderParameter("strength", strength);

    private void Go(LiftStep step)
    {
        Lift = step;
        _into = 0d;
    }

    private float Part(double of) => (float)Math.Min(1d, _into / of);
}

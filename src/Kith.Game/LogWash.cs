using Godot;

namespace Kith.Game;

/// <remarks>
/// ⭐ A LINE OF THE VILLAGE LOG WASHED GOLD, AND FADING (Joe's play note 9, D898: <i>"when kin shows up via letter and the game
/// pauses, animate/highlight the line in the village log temporarily"</i>). It is drawn behind the log's own text, a child of it
/// clipped to it and shown behind it, so the text, the colours and the scroll are untouched and the line can be scrolled away
/// and back while it lasts. ⛔ Real seconds, never the sim's: it fades the same paused, at 1× and at 10×, and it is the view's own —
/// not in the village, not in a save.
/// </remarks>
public sealed partial class LogWash : Control
{
    /// <summary>How long a line stays washed, seconds — Joe's <i>"temporarily"</i>, about six.</summary>
    internal const double Lasts = 6.0;

    /// <summary>Full for this long, and fading over the rest.</summary>
    internal const double Holds = 2.0;

    /// <summary>The palette's gold leaf at the wash's strongest: words on it stay well past 4.5 : 1.</summary>
    private static readonly Color Leaf = new("#c4983e") { A = 0.5f };

    private readonly RichTextLabel _log;

    /// <summary>Each washed line: the entry it is, the paragraph it is drawn as, and the seconds it has left.</summary>
    private readonly List<(int Entry, int Paragraph, double Left)> _lines = new();

    public LogWash(RichTextLabel log)
    {
        _log = log;
        ShowBehindParent = true;
        MouseFilter = MouseFilterEnum.Ignore;
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        SetProcess(false);
    }

    /// <summary>The washed entries, the paragraph each is drawn as, and how strong — for the probe.</summary>
    internal IReadOnlyList<(int Entry, int Paragraph, float Strength)> Washed =>
        _lines.Select(l => (l.Entry, l.Paragraph, StrengthAt(l.Left))).ToList();

    /// <summary>How strong a wash with this many seconds left is: 1, then down to 0.</summary>
    internal static float StrengthAt(double left) => (float)Math.Clamp(left / (Lasts - Holds), 0.0, 1.0);

    /// <summary>Wash this entry's line from now.</summary>
    internal void Begin(int entry, int paragraph)
    {
        _lines.RemoveAll(l => l.Entry == entry);
        _lines.Add((entry, paragraph, Lasts));
        SetProcess(true);
        QueueRedraw();
    }

    /// <summary>The log was cleared and is being written again: where an entry still washed is drawn now.</summary>
    internal void MovedTo(int entry, int paragraph)
    {
        int at = _lines.FindIndex(l => l.Entry == entry);
        if (at >= 0)
        {
            _lines[at] = (entry, paragraph, _lines[at].Left);
            QueueRedraw();
        }
    }

    /// <summary>The log was cleared: nothing is drawn until each entry is written again.</summary>
    internal void Cleared()
    {
        for (int i = 0; i < _lines.Count; i++)
        {
            _lines[i] = (_lines[i].Entry, -1, _lines[i].Left);
        }

        QueueRedraw();
    }

    /// <summary>Seconds passed: the washes fade, and one that is done is dropped.</summary>
    internal void Pass(double seconds)
    {
        for (int i = _lines.Count - 1; i >= 0; i--)
        {
            (int entry, int paragraph, double left) = _lines[i];
            left -= seconds;
            if (left <= 0.0)
            {
                _lines.RemoveAt(i);
            }
            else
            {
                _lines[i] = (entry, paragraph, left);
            }
        }

        if (_lines.Count == 0)
        {
            SetProcess(false);
        }

        QueueRedraw();
    }

    public override void _Process(double delta) => Pass(delta);

    /// <summary>Where a paragraph's lines stand inside the log, scroll included; empty once it is scrolled out, cleared or trimmed.</summary>
    internal Rect2 RectOf(int paragraph)
    {
        if (paragraph < 0 || paragraph >= _log.GetParagraphCount())
        {
            return default;
        }

        StyleBox inside = _log.GetThemeStylebox("normal");
        float top = _log.GetParagraphOffset(paragraph);
        float bottom = paragraph + 1 < _log.GetParagraphCount() ? _log.GetParagraphOffset(paragraph + 1) : _log.GetContentHeight();
        float scrolled = (float)_log.GetVScrollBar().Value;
        return new Rect2(0f, inside.GetMargin(Side.Top) + top - scrolled, _log.Size.X, bottom - top);
    }

    public override void _Draw()
    {
        foreach ((int _, int paragraph, double left) in _lines)
        {
            Rect2 rect = RectOf(paragraph);
            if (rect.Size.Y > 0f && rect.End.Y > 0f && rect.Position.Y < _log.Size.Y)
            {
                DrawRect(rect.Intersection(new Rect2(Vector2.Zero, _log.Size)), Leaf with { A = Leaf.A * StrengthAt(left) });
            }
        }
    }
}

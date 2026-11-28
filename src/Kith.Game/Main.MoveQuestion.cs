using Kith.Sim.Core;
using Kith.Sim.World;
using Godot;

namespace Kith.Game;

/// <summary>
/// ⭐ The move's question (`move-revamp.md §2.2`, D947; Joe: <i>"build in 'are you sure' safeguard prompts before moving a
/// building"</i>) — a small box beside the outline, <i>Move it</i> / <i>Not here</i>, and the card's <i>Call off the move</i>.
/// </summary>
/// <remarks>
/// ⭐ <b>The game keeps running while it is up</b> (call 1): it is a question about one click, not a moment — the pause is
/// D232's, for gifts. The map owns the state (<see cref="VillageMap.ConfirmTheMove"/>, <see cref="VillageMap.LetGoFirst"/>);
/// this only shows it and carries the two answers back.
/// </remarks>
public partial class Main
{
    /// <summary>The question's box; hidden until a move is clicked onto good ground.</summary>
    private PanelContainer _moveBox = null!;

    private Label _moveTitle = null!;
    private Label _moveBody = null!;

    /// <summary>Between the outline and the box, on screen.</summary>
    private const float MoveBoxGap = 36f;

    private void BuildTheMoveQuestion()
    {
        var panel = new PanelContainer { Visible = false, CustomMinimumSize = new Vector2(300f, 0f) };

        var box = new VBoxContainer();
        box.AddThemeConstantOverride("separation", 8);

        _moveTitle = Heading(string.Empty);
        box.AddChild(_moveTitle);

        _moveBody = Wrapped(Body(string.Empty));
        _moveBody.CustomMinimumSize = new Vector2(300f, 0f);
        box.AddChild(_moveBody);

        var answers = new HBoxContainer();
        answers.AddThemeConstantOverride("separation", 10);
        var moveIt = new Button { Text = "Move it", TooltipText = "Enter" };
        moveIt.Pressed += () => _map.ConfirmTheMove();
        answers.AddChild(moveIt);
        var notHere = new Button { Text = "Not here", TooltipText = "Esc or right-click — the building stays in your hand" };
        notHere.Pressed += () => _map.LetGoFirst();
        answers.AddChild(notHere);
        box.AddChild(answers);

        panel.AddChild(box);
        AddChild(panel);
        _moveBox = panel;

        _map.MoveQuestionChanged += ShowTheMoveQuestion;
    }

    /// <summary>Show the question beside the outline, inside the window — or hide it.</summary>
    private void ShowTheMoveQuestion(VillageMap.MoveQuestion? asked)
    {
        if (asked is null)
        {
            _moveBox.Visible = false;
            return;
        }

        _moveTitle.Text = asked.Title;
        _moveBody.Text = asked.Body;
        _moveBox.Visible = true;
        _moveBox.ResetSize();

        // To the right of the outline, its middle level with it; to the left when the right has no room, and never off
        // the window.
        Vector2 size = _moveBox.Size;
        Vector2 window = GetViewportRect().Size;
        Vector2 near = asked.Near - GetGlobalTransformWithCanvas().Origin;
        float x = near.X + MoveBoxGap + size.X <= window.X ? near.X + MoveBoxGap : near.X - MoveBoxGap - size.X;
        float y = near.Y - (size.Y / 2f);
        _moveBox.Position = new Vector2(Mathf.Clamp(x, 8f, Mathf.Max(8f, window.X - size.X - 8f)), Mathf.Clamp(y, 8f, Mathf.Max(8f, window.Y - size.Y - 8f)));
    }

    /// <summary>Whether the move's question is showing — for the probe.</summary>
    internal bool TheMoveQuestionShows => _moveBox.Visible;

    /// <summary>
    /// ⭐ A building on the move says so on its card, with the button that calls it off (call 11) — cheaper to find than the
    /// outline Demolish is pointed at. Hidden when it is not being moved.
    /// </summary>
    private static void MovingLine(Card card, SimWorld world, GridPos tile)
    {
        Workplace? site = world.MoveUnderWay(tile);
        card.MoveRow.Visible = site is not null;
        card.MovingFrom = site is null ? null : tile;
        if (site?.Construction is ConstructionSite plan)
        {
            int owed = System.Math.Max(1, plan.Recipe.WorkTicks);
            card.MoveNote.Text = $"Being moved — {System.Math.Min(100, plan.WorkDone * 100 / owed)}% of the work done.";
        }
    }
}

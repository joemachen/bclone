using Bclone.Sim.Core;
using Bclone.Sim.World;
using Godot;

namespace Bclone.Game;

/// <summary>
/// ⭐ The tech-tree map (`tech-tree-map.md`, D440) — <b>what the village may yet learn, and what it
/// takes</b>, read off the sim's own gates.
/// </summary>
/// <remarks>
/// <para>
/// Nodes are cards in columns by depth (how many nodes a node hangs from), each in the row of its
/// first parent, roots in the order the data lists them; the <c>requires</c> edges are lines between
/// them. A known node is lit; one in sight shows its name, its sentence and a bar of the gate's own
/// number; a fogged one is a grey silhouette with no name.
/// </para>
/// <para>
/// ⛔ <b>Built when asked, never per frame</b> (CLAUDE.md): <see cref="Show(SimWorld)"/> rebuilds the cards from
/// <see cref="TechTree.StateOf"/>; the window calls it when it opens and once a season while open.
/// ⛔ Nothing here unlocks anything — there is no button on a node (§2.7).
/// </para>
/// </remarks>
public partial class TechTreeView : Control
{
    /// <summary>A card's width — wide enough for a name and a two-line sentence.</summary>
    public const float CardWidth = 176f;

    /// <summary>A card's height.</summary>
    public const float CardHeight = 160f;

    private const float Gap = 28f;
    private const float Margin = 8f;

    private static readonly Color Lit = new(0.62f, 0.86f, 0.58f);
    private static readonly Color Edge = new(1f, 1f, 1f, 0.35f);

    private readonly List<(TechNodeRow Node, PanelContainer Card, Vector2 At)> _cards = new();

    /// <summary>The labels on the cards, for the probe's trimming check.</summary>
    public IEnumerable<Label> Labels() => _cards.SelectMany(c => Every<Label>(c.Card));

    /// <summary>Rebuild every card from the village's state now.</summary>
    public void Show(SimWorld world) => Show(world, node => TechTree.StateOf(world, node));

    /// <summary>
    /// Rebuild every card from a given state — the probe's door, posing the fullest cards without
    /// writing the sim's state (the view may not).
    /// </summary>
    public void Show(SimWorld world, Func<TechNodeRow, (TechState State, int Progress, int Of)> stateOf)
    {
        ArgumentNullException.ThrowIfNull(world);
        ArgumentNullException.ThrowIfNull(stateOf);

        foreach ((TechNodeRow _, PanelContainer card, Vector2 _) in _cards)
        {
            card.QueueFree();
        }

        _cards.Clear();
        IReadOnlyList<TechNodeRow> nodes = world.Config.TechNodeRows;
        var rowOf = new Dictionary<string, int>(StringComparer.Ordinal);
        var used = new HashSet<(int, int)>();
        int roots = 0;
        int deepest = 0;
        int lowest = 0;

        foreach (TechNodeRow node in nodes.OrderBy(n => TechTree.DepthOf(nodes, n)))
        {
            int depth = TechTree.DepthOf(nodes, node);
            int row = depth == 0 || node.Requires.Count == 0 || !rowOf.TryGetValue(node.Requires[0], out int parentRow)
                ? roots++
                : parentRow;
            while (!used.Add((depth, row)))
            {
                row++;
            }

            rowOf[node.Id] = row;
            deepest = Math.Max(deepest, depth);
            lowest = Math.Max(lowest, row);

            var at = new Vector2(Margin + (depth * (CardWidth + Gap)), Margin + (row * (CardHeight + (Gap / 2f))));
            PanelContainer card = CardFor(node, stateOf(node));
            card.Position = at;
            card.Size = new Vector2(CardWidth, CardHeight);
            AddChild(card);
            _cards.Add((node, card, at));
        }

        CustomMinimumSize = new Vector2(
            (Margin * 2f) + ((deepest + 1) * CardWidth) + (deepest * Gap),
            (Margin * 2f) + ((lowest + 1) * CardHeight) + (lowest * (Gap / 2f)));
        QueueRedraw();
    }

    /// <summary>The lines between a node and what it hangs from.</summary>
    public override void _Draw()
    {
        foreach ((TechNodeRow node, PanelContainer _, Vector2 at) in _cards)
        {
            foreach (string parent in node.Requires)
            {
                foreach ((TechNodeRow other, PanelContainer _, Vector2 from) in _cards)
                {
                    if (other.Id == parent)
                    {
                        DrawLine(
                            from + new Vector2(CardWidth, CardHeight / 2f),
                            at + new Vector2(0f, CardHeight / 2f),
                            Edge,
                            2f);
                    }
                }
            }
        }
    }

    private static PanelContainer CardFor(TechNodeRow node, (TechState State, int Progress, int Of) at)
    {
        (TechState state, int progress, int of) = at;

        var card = new PanelContainer { CustomMinimumSize = new Vector2(CardWidth, CardHeight) };
        var box = new VBoxContainer();
        box.AddThemeConstantOverride("separation", 4);
        card.AddChild(box);

        if (state == TechState.Fogged)
        {
            card.Modulate = new Color(1f, 1f, 1f, 0.45f);
            box.AddChild(Line("?", 15, bold: true));
            box.AddChild(Line("Not yet seen.", 12));
            return card;
        }

        Label name = Line(node.Name, 15, bold: true);
        if (state == TechState.Known)
        {
            name.Modulate = Lit;
        }

        box.AddChild(name);
        box.AddChild(Line(node.Says, 12));

        if (state == TechState.Known)
        {
            box.AddChild(Line(node.Unlocks is null ? "Known." : "Known — it is in the build menu.", 12));
        }
        else if (node.Condition == TechCondition.NotYet)
        {
            box.AddChild(Line("Nobody in this valley knows how yet.", 12));
        }
        else
        {
            var bar = new ProgressBar
            {
                MaxValue = Math.Max(1, of),
                Value = Math.Min(progress, of),
                ShowPercentage = false,
                CustomMinimumSize = new Vector2(0f, 6f),
            };
            box.AddChild(bar);
            box.AddChild(Line(HowFar(node.Condition, progress, of), 12));
        }

        return card;
    }

    /// <summary>The gate's number in words.</summary>
    private static string HowFar(TechCondition condition, int progress, int of) => condition switch
    {
        TechCondition.StoneDug => $"{progress} of {of} stone dug by hand",
        TechCondition.IronDug => $"{progress} of {of} iron dug by hand",
        TechCondition.KeptGranaryYears => $"{progress} of {of} years of a kept granary",
        TechCondition.FoundersGone => $"{progress} of {of} founders gone",
        _ => string.Empty,
    };

    private static Label Line(string text, int size, bool bold = false)
    {
        var label = new Label
        {
            Text = text,
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
            TextOverrunBehavior = TextServer.OverrunBehavior.NoTrimming,
            CustomMinimumSize = new Vector2(CardWidth - 16f, 0f),
        };
        label.AddThemeFontSizeOverride("font_size", size);
        if (!bold)
        {
            label.Modulate = new Color(1f, 1f, 1f, 0.8f);
        }

        return label;
    }

    private static IEnumerable<T> Every<T>(Node node)
        where T : Node
    {
        if (node is T found)
        {
            yield return found;
        }

        foreach (Node child in node.GetChildren())
        {
            foreach (T inner in Every<T>(child))
            {
                yield return inner;
            }
        }
    }
}

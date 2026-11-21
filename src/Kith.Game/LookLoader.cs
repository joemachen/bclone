using Godot;

namespace Kith.Game;

/// <summary>
/// Puts the look on before any window is built (<c>ui-art.md §9.3</c> step 3, A113): an autoload
/// (<c>project.godot [autoload]</c>), so it enters the tree before the main scene does. The parchment theme becomes the
/// root window's — what every control and popup inherits — and the look's (<see cref="UiLook.Wear"/>), which is what the
/// game's own panels, cards and text colours ask.
/// </summary>
/// <remarks><c>KITH_LOOK=slate</c> starts in today's slate instead — for a before-and-after render, nothing else.</remarks>
public partial class LookLoader : Node
{
    public override void _EnterTree()
    {
        if (OS.GetEnvironment("KITH_LOOK") == "slate")
        {
            return;
        }

        Theme theme = ParchmentLook.Build();
        GetTree().Root.Theme = theme;
        UiLook.Wear(theme);
    }
}

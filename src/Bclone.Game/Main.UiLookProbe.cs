using Bclone.Sim.Logging;
using Bclone.Sim.World;
using Godot;

namespace Bclone.Game;

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

        bool projectNamesALook = ThemeDB.GetProjectTheme() is { } project && project.GetColorList(UiLook.Type).Length > 0;
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
            var box = new StyleBoxFlat { BgColor = odd };
            theme.SetColor("stopped", UiLook.Type, odd);
            theme.SetStylebox("card", UiLook.Type, box);
            UiLook.Use(theme);
            Same("a theme's stopped", UiLook.Stopped, odd);
            Same("working, which the theme does not name", UiLook.Working, new Color("#7fbf7a"));
            if (!ReferenceEquals(UiLook.Card(false), box))
            {
                faults.Add("a theme's card is not used");
            }

            if (UiLook.Card(false, 12) is not { } roomy || ReferenceEquals(roomy, box) || !Mathf.IsEqualApprox(roomy.ContentMarginLeft, 12f))
            {
                faults.Add("a theme's card is not given a person's room");
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
            UiLook.UseTheProjectTheme();
            RefreshCards(_loop.World);
        }

        return faults.Count == 0
            ? $"[widths] ui theme: ✅ {(projectNamesALook ? "the project names a look" : "no look named — slate")}: 28 roles and 4 skins are "
              + "the slate their sites had; an open card wears the look's kept skin; a theme's named entries are obeyed and the "
              + "rest stay slate; a tint reads back"
            : $"[widths] ui theme: ⛔ {string.Join("; ", faults)}";
    }
}

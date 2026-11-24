using System.Globalization;
using System.Text.Json.Nodes;
using Kith.Sim.World;
using Godot;

namespace Kith.Game;

/// <remarks>
/// ⭐ NEW, UNTIL CLICKED (<c>hud.md §8.3</c>, D880; Joe, A210: <i>"highlight newly unlocked categories and buildings until
/// they are clicked on"</i>; call 5): a category or a building that becomes earned is marked — its word in gold ink with a
/// gold-leaf swash under it (A216's <c>underline-gold</c>). A category clears when its flyout opens, a building when it is
/// picked. ⛔ <b>Not the owed medallion</b> (A106), which keeps its meaning — a free one waiting. ⛔ <b>The view's, never the
/// sim's</b>: what the player has looked at is not a fact about the village (D335), so the seen set rides in the save's
/// <c>"view"</c> section, untouched by the sim and never hashed; a save without one (every save before D880) and a new
/// village count everything already earned as seen, so neither lights the whole bar.
/// </remarks>
public partial class Main
{
    private readonly HashSet<string> _seen = new(StringComparer.Ordinal);

    /// <summary>The save's view section a load hands in before the bar is built — null for a new village or an older save.</summary>
    private JsonObject? _viewToRestore;

    private bool _seenStarted;

    private static string SeenKey(BuildCategory category) => "category:" + category;

    private static string SeenKey(BuildingKind kind) => "building:" + ((int)kind).ToString(CultureInfo.InvariantCulture);

    /// <summary>When the bar is built: the save's seen set, or everything earned now — so a load never lights the whole bar.</summary>
    private void StartTheSeen(JsonObject? view)
    {
        _seen.Clear();
        if (view?["seen"] is JsonArray seen)
        {
            foreach (JsonNode? key in seen)
            {
                if (key?.GetValue<string>() is string text)
                {
                    _seen.Add(text);
                }
            }
        }
        else
        {
            SeeEverythingEarned();
        }

        _seenStarted = true;
        MarkWhatIsNew();
    }

    private void SeeEverythingEarned()
    {
        foreach ((BuildCategory category, Button _) in _categoryButtons)
        {
            if (AnythingEarnedIn(category))
            {
                _seen.Add(SeenKey(category));
            }
        }

        foreach ((BuildTab _, BuildCategory _, Button _, BuildingKind? kind, ToolMark? _) in _strip)
        {
            if (kind is BuildingKind k && EarnedYet(k))
            {
                _seen.Add(SeenKey(k));
            }
        }
    }

    private bool IsNew(BuildCategory category) => _seenStarted && AnythingEarnedIn(category) && !_seen.Contains(SeenKey(category));

    private bool IsNew(BuildingKind kind) => _seenStarted && EarnedYet(kind) && !_seen.Contains(SeenKey(kind));

    /// <summary>Seen: a category's flyout opened, or a building picked — its gold goes.</summary>
    private void Seen(string key)
    {
        if (_seen.Add(key))
        {
            MarkWhatIsNew();
        }
    }

    /// <summary>Gold on what is new, after the hand's tint (<c>RelightTheStrip</c> ends here), so the two cannot fight.</summary>
    private void MarkWhatIsNew()
    {
        foreach ((BuildCategory category, Button button) in _categoryButtons)
        {
            WearNew(button, IsNew(category));
        }

        foreach ((BuildTab _, BuildCategory _, Button button, BuildingKind? kind, ToolMark? _) in _strip)
        {
            if (kind is BuildingKind k)
            {
                WearNew(button, IsNew(k));
            }
        }
    }

    /// <summary>The word in gold, the gold swash under it — or neither.</summary>
    private static void WearNew(Button button, bool isNew)
    {
        if (isNew)
        {
            button.AddThemeColorOverride("font_color", UiLook.New);
            button.AddThemeColorOverride("font_hover_color", UiLook.New);
            if (UiLook.NewSwash is StyleBox swash)
            {
                button.AddThemeStyleboxOverride("normal", swash);
                button.AddThemeStyleboxOverride("hover", swash);
            }

            button.SetMeta(NewMeta, true);
            return;
        }

        if (button.HasMeta(NewMeta))
        {
            button.RemoveThemeColorOverride("font_hover_color");
            button.RemoveThemeStyleboxOverride("normal");
            button.RemoveThemeStyleboxOverride("hover");
            if (button.GetThemeColor("font_color").IsEqualApprox(UiLook.New))
            {
                button.RemoveThemeColorOverride("font_color");
            }

            button.RemoveMeta(NewMeta);
        }
    }

    private const string NewMeta = "kith_new";

    /// <summary>The save's view section: the seen set, sorted so two saves of one village read the same.</summary>
    private JsonObject TheViewSection() =>
        new() { ["seen"] = new JsonArray(_seen.Order(StringComparer.Ordinal).Select(key => (JsonNode?)JsonValue.Create(key)).ToArray()) };

    /// <summary>
    /// ⭐ <c>new:</c> (<c>hud.md §8.3</c>, step 7) — nothing is new at the founding; a library posed learned lights Knowledge and
    /// the Library gold with the gold swash; opening Knowledge clears it and leaves the Library gold; picking the Library clears
    /// it; the seen set goes out in the save's view section and comes back the same; and a save without one sees everything.
    /// </summary>
    private string WhatIsNewIsGoldUntilClicked()
    {
        var faults = new List<string>();
        Button knowledge = _categoryButtons.Find(c => c.Category == BuildCategory.Knowledge).Button;
        Button? library = _libraryButton;
        bool Gold(Button? b) => b is not null && b.GetThemeColor("font_color").IsEqualApprox(UiLook.New)
            && (UiLook.NewSwash is null || ReferenceEquals(b.GetThemeStylebox("normal"), UiLook.NewSwash));

        int newAtTheFounding = _categoryButtons.Count(c => Gold(c.Button)) + _strip.Count(e => Gold(e.Button));
        if (newAtTheFounding > 0)
        {
            faults.Add($"{newAtTheFounding} marked new at the founding");
        }

        var seenWas = new HashSet<string>(_seen, StringComparer.Ordinal);
        bool literacyWas = _literacy;
        try
        {
            _literacy = true;
            RefreshTheStrip();
            if (!Gold(knowledge) || !Gold(library))
            {
                faults.Add($"a library learned: Knowledge gold {Gold(knowledge)}, the Library gold {Gold(library)}");
            }

            JsonObject before = TheViewSection();
            ToggleTheFlyout(BuildTab.Build, BuildCategory.Knowledge);
            bool knowledgeCleared = !Gold(knowledge) && Gold(library);
            library?.EmitSignal(BaseButton.SignalName.Pressed);
            bool libraryCleared = !Gold(library);
            _map.PutTheToolDown();
            CloseTheFlyout();
            if (!knowledgeCleared || !libraryCleared)
            {
                faults.Add($"opening Knowledge cleared it and kept the Library {knowledgeCleared}; picking the Library cleared it {libraryCleared}");
            }

            // A reload keeps it: the section out, the set emptied, the section back.
            JsonObject after = TheViewSection();

            // And it is what a save of the village writes (the view's `CaptureTheVillage`), beside "world".
            if (CaptureTheVillage()["view"]?.ToJsonString() != after.ToJsonString())
            {
                faults.Add("a save of the village does not carry the seen set in its view section");
            }

            StartTheSeen(JsonNode.Parse(after.ToJsonString()) as JsonObject);
            bool kept = TheViewSection().ToJsonString() == after.ToJsonString() && !Gold(knowledge) && !Gold(library);
            StartTheSeen(JsonNode.Parse(before.ToJsonString()) as JsonObject);
            bool keptNew = Gold(knowledge) && Gold(library);
            StartTheSeen(null);
            bool olderSeesAll = !Gold(knowledge) && !Gold(library);
            if (!kept || !keptNew || !olderSeesAll)
            {
                faults.Add($"through the save: seen kept {kept}, new kept {keptNew}; a save without the section sees everything {olderSeesAll}");
            }
        }
        finally
        {
            _literacy = literacyWas;
            _seen.Clear();
            _seen.UnionWith(seenWas);
            RefreshTheStrip();
        }

        return faults.Count == 0
            ? "[widths] new: ✅ nothing new at the founding; a library learned lights Knowledge and the Library in gold with the gold swash; "
              + "opening Knowledge clears it, picking the Library clears it; the seen set through the save's view section comes back as it went, "
              + "and a save without one sees everything earned"
            : $"[widths] new: ⛔ {string.Join("; ", faults)}";
    }
}

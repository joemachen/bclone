using System.Globalization;
using System.Text.Json.Nodes;
using Kith.Sim.Core;
using Kith.Sim.World;
using Godot;

namespace Kith.Game;

/// <remarks>
/// ⭐ NEW, UNTIL CLICKED (<c>hud.md §8.3</c>, D880; Joe, A210: <i>"highlight newly unlocked categories and buildings until
/// they are clicked on"</i>; call 5): a category or a building that becomes earned is marked — its word in the rubric's red and ⭐ <b>the
/// title menu's red stroke under it</b> (A272, <c>new-mark-red.md</c>; Joe, of the gold seal A257 wore: <i>"Gold badge on new items
/// doesnt read … or use the red underline that is used on main menu"</i>, then <i>"2"</i>). A category clears when its flyout opens, a building when it is
/// picked. ⭐ <b>A category is new while a building inside it is</b> (Joe's play note 2, D894: <i>"new items main categories should
/// also highlight"</i>) — new, and not seen in its flyout since: it asked whether the category had <i>ever</i> been opened, so
/// the quarry lit and Resources, opened on the first day, did not. The cottage and the manor light Homes the same way.
/// ⛔ <b>Not the owed medallion</b> (A106), which keeps its meaning — a free one waiting. ⛔ <b>The view's, never the
/// sim's</b>: what the player has looked at is not a fact about the village (D335), so the seen set rides in the save's
/// <c>"view"</c> section, untouched by the sim and never hashed; a save without one (every save before D880) and a new
/// village count everything already earned as seen, so neither lights the whole bar.
/// </remarks>
public partial class Main
{
    /// <summary>The buildings picked at least once since they were learned — their gold is gone for good.</summary>
    private readonly HashSet<string> _seen = new(StringComparer.Ordinal);

    /// <summary>The buildings whose category's flyout has been opened since they were learned: they no longer light the category.</summary>
    private readonly HashSet<string> _shown = new(StringComparer.Ordinal);

    /// <summary>The save's view section a load hands in before the bar is built — null for a new village or an older save.</summary>
    private JsonObject? _viewToRestore;

    private bool _seenStarted;

    private static string SeenKey(BuildingKind kind) => "building:" + ((int)kind).ToString(CultureInfo.InvariantCulture);

    /// <summary>When the bar is built: the save's seen set, or everything earned now — so a load never lights the whole bar.</summary>
    private void StartTheSeen(JsonObject? view)
    {
        _seen.Clear();
        _shown.Clear();
        if (view?["seen"] is JsonArray seen)
        {
            Keep(seen, _seen);
            if (view["shown"] is JsonArray shown)
            {
                Keep(shown, _shown);
            }
            else
            {
                // A section from D880 kept no house's mark: the ones already learned are old news.
                foreach ((BuildingKind kind, Button _) in _homesAs)
                {
                    if (KnownTo(_loop.World, kind))
                    {
                        _seen.Add(SeenKey(kind));
                    }
                }
            }
        }
        else
        {
            SeeEverythingEarned(_loop.World);
        }

        // ⚠️ The buildings' keys only: D880's section kept categories by name too, and they mean nothing now.
        static void Keep(JsonArray from, HashSet<string> into)
        {
            foreach (JsonNode? key in from)
            {
                if (key?.GetValue<string>() is string text && text.StartsWith("building:", StringComparison.Ordinal))
                {
                    into.Add(text);
                }
            }
        }

        _seenStarted = true;
        MarkWhatIsNew();
    }

    private void SeeEverythingEarned(SimWorld world)
    {
        foreach ((BuildCategory _, BuildingKind kind, Button _) in WhatCanBeNew())
        {
            if (KnownTo(world, kind))
            {
                _seen.Add(SeenKey(kind));
            }
        }
    }

    /// <summary>
    /// Whether the village knows this building — ⛔ the sim's gate and not only the bar's latched flags (D894): the bar is built,
    /// and the seen set taken, before the first refresh sets those flags, so a village loaded from a save without the section
    /// counted only the founding's twelve as seen and everything it had learned lit up (his Millbrook's autosave says so).
    /// </summary>
    /// <remarks>⚠️ The library and the hall are gifts, not gated: <c>IsUnlocked</c> is true of both from the founding.</remarks>
    private bool KnownTo(SimWorld world, BuildingKind kind) => EarnedYet(kind) || kind switch
    {
        BuildingKind.Library => world.HasLiteracy,
        BuildingKind.TownHall => world.SaidTheFoundersAreGone,
        _ => world.IsUnlocked(kind),
    };

    /// <summary>Everything a flyout offers that can be new: its buildings, and Homes' houses to build as.</summary>
    private IEnumerable<(BuildCategory Category, BuildingKind Kind, Button Button)> WhatCanBeNew()
    {
        foreach ((BuildTab _, BuildCategory category, Button button, BuildingKind? kind, ToolMark? _) in _strip)
        {
            if (kind is BuildingKind k)
            {
                yield return (category, k, button);
            }
        }

        foreach ((BuildingKind kind, Button button) in _homesAs)
        {
            yield return (BuildCategory.Homes, kind, button);
        }
    }

    /// <summary>A category is new while a building inside it is new and has not been seen in its flyout.</summary>
    private bool IsNew(BuildCategory category)
    {
        foreach ((BuildCategory of, BuildingKind kind, Button _) in WhatCanBeNew())
        {
            if (of == category && IsNew(kind) && !_shown.Contains(SeenKey(kind)))
            {
                return true;
            }
        }

        return false;
    }

    private bool IsNew(BuildingKind kind) => _seenStarted && EarnedYet(kind) && !_seen.Contains(SeenKey(kind));

    /// <summary>The open flyout shows its buildings, so they no longer light their category — one learned while it is open too.</summary>
    private void ShowWhatTheFlyoutHolds()
    {
        if (!_flyoutOpen || _tab != BuildTab.Build)
        {
            return;
        }

        foreach ((BuildCategory of, BuildingKind kind, Button _) in WhatCanBeNew())
        {
            if ((_filter is null || of == _filter) && IsNew(kind))
            {
                _shown.Add(SeenKey(kind));
            }
        }
    }

    /// <summary>Seen: a building picked — its gold goes.</summary>
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
        ShowWhatTheFlyoutHolds();
        foreach ((BuildCategory category, Button button) in _categoryButtons)
        {
            WearNew(button, IsNew(category));
        }

        foreach ((BuildCategory _, BuildingKind kind, Button button) in WhatCanBeNew())
        {
            WearNew(button, IsNew(kind));
        }
    }

    /// <summary>The word in red and the red stroke under it — or neither (A272).</summary>
    private static void WearNew(Button button, bool isNew)
    {
        if (isNew)
        {
            button.AddThemeColorOverride("font_color", UiLook.New);
            button.AddThemeColorOverride("font_hover_color", UiLook.New);
            if (UiLook.NewStroke is StyleBox stroke)
            {
                // Under the pointer too: the hover's wash gives way to the stroke, as the swash D880 wore did.
                button.AddThemeStyleboxOverride("normal", stroke);
                button.AddThemeStyleboxOverride("hover", stroke);
            }

            button.SetMeta(NewMeta, true);
            return;
        }

        if (button.HasMeta(NewMeta))
        {
            button.RemoveThemeColorOverride("font_hover_color");
            if (button.GetThemeColor("font_color").IsEqualApprox(UiLook.New))
            {
                button.RemoveThemeColorOverride("font_color");
            }

            foreach (string state in new[] { "normal", "hover" })
            {
                if (StrokeOn(button, state))
                {
                    button.RemoveThemeStyleboxOverride(state);
                }
            }

            button.RemoveMeta(NewMeta);
        }
    }

    /// <summary>Whether a button wears the new mark's stroke in this state.</summary>
    private static bool StrokeOn(Button button, string state) =>
        UiLook.NewStroke is { } stroke && button.HasThemeStyleboxOverride(state) && ReferenceEquals(button.GetThemeStylebox(state), stroke);

    private const string NewMeta = "kith_new";

    /// <summary>The save's view section: what was picked and what was shown, sorted so two saves of one village read the same.</summary>
    private JsonObject TheViewSection()
    {
        static JsonArray Sorted(HashSet<string> keys) =>
            new(keys.Order(StringComparer.Ordinal).Select(key => (JsonNode?)JsonValue.Create(key)).ToArray());
        return new JsonObject { ["seen"] = Sorted(_seen), ["shown"] = Sorted(_shown) };
    }

    /// <summary>
    /// ⭐ <c>new:</c> (<c>hud.md §8.3</c>, step 7) — nothing is new at the founding; a library posed learned lights Knowledge and
    /// the Library red with the red stroke under each (A272); opening Knowledge clears it and leaves the Library gold; picking the Library clears
    /// it; the seen set goes out in the save's view section and comes back the same; and a save without one sees everything.
    /// ⭐ And a category opened long ago lights again for what is learned in it since (D894).
    /// </summary>
    private string WhatIsNewIsGoldUntilClicked()
    {
        var faults = new List<string>();
        Button knowledge = _categoryButtons.Find(c => c.Category == BuildCategory.Knowledge).Button;
        Button? library = _libraryButton;
        // New: the red word and the stroke, at rest and under the pointer; plain: neither — a stroke left on a cleared button is a
        // fault too. (Named Gold for the mark D880 first wore; the probe line's name is Main.cs's.)
        bool Gold(Button? b) => b is not null && b.GetThemeColor("font_color").IsEqualApprox(UiLook.New) && StrokeOn(b, "normal") && StrokeOn(b, "hover");
        bool Plain(Button? b) => b is not null && !b.GetThemeColor("font_color").IsEqualApprox(UiLook.New) && !StrokeOn(b, "normal") && !StrokeOn(b, "hover");

        int newAtTheFounding = _categoryButtons.Count(c => !Plain(c.Button)) + _strip.Count(e => !Plain(e.Button));
        if (newAtTheFounding > 0)
        {
            faults.Add($"{newAtTheFounding} marked new at the founding");
        }

        var seenWas = new HashSet<string>(_seen, StringComparer.Ordinal);
        var shownWas = new HashSet<string>(_shown, StringComparer.Ordinal);
        (bool quarryWas, bool mineWas, bool cottageWas) = (_quarryKnown, _mineKnown, _cottageKnown);
        bool literacyWas = _literacy;
        try
        {
            _literacy = true;
            RefreshTheStrip();
            if (!Gold(knowledge) || !Gold(library))
            {
                faults.Add($"a library learned: Knowledge marked {Gold(knowledge)}, the Library marked {Gold(library)}");
            }

            // The mark itself: the word in the rubric's red, and the title menu's painted swash in a 4 px band at the button's foot —
            // not the held button's lapis one, and no seal left over from A257.
            foreach (Button b in new[] { knowledge, library! })
            {
                string swash = b.GetThemeStylebox("normal") is PaintedPatch { Texture: { } painted } p
                    ? $"{painted.ResourcePath.GetFile()} at {p.Band:F0} px" : "no painted swash";
                if (!UiLook.New.IsEqualApprox(ParchmentLook.RubricInk) || swash != $"underline.png at {ParchmentLook.NewStrokeBand:F0} px"
                    || b.GetNodeOrNull("NewSeal") is not null)
                {
                    faults.Add($"{b.Text} wears {UiLook.New.ToHtml(false)} words, {swash}{(b.GetNodeOrNull("NewSeal") is null ? "" : ", and a seal")}: "
                        + "not the rubric's red over the title's red stroke");
                }
            }

            JsonObject before = TheViewSection();
            ToggleTheFlyout(BuildTab.Build, BuildCategory.Knowledge);
            bool knowledgeCleared = Plain(knowledge) && Gold(library);
            library?.EmitSignal(BaseButton.SignalName.Pressed);
            bool libraryCleared = Plain(library);
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
            bool kept = TheViewSection().ToJsonString() == after.ToJsonString() && Plain(knowledge) && Plain(library);
            StartTheSeen(JsonNode.Parse(before.ToJsonString()) as JsonObject);
            bool keptNew = Gold(knowledge) && Gold(library);
            StartTheSeen(null);
            bool olderSeesAll = Plain(knowledge) && Plain(library);
            if (!kept || !keptNew || !olderSeesAll)
            {
                faults.Add($"through the save: seen kept {kept}, new kept {keptNew}; a save without the section sees everything {olderSeesAll}");
            }

            // ⭐ His screenshot (play note 2, D894): Resources opened at the founding, the quarry learned after — both light.
            // Opening Resources clears it and keeps the quarry; the mine learned later lights it again; picked, both go.
            Button resources = _categoryButtons.Find(c => c.Category == BuildCategory.Resources).Button;
            ToggleTheFlyout(BuildTab.Build, BuildCategory.Resources);
            CloseTheFlyout();
            _quarryKnown = true;
            RefreshTheStrip();
            bool quarryLights = Gold(resources) && Gold(_quarryButton);
            ToggleTheFlyout(BuildTab.Build, BuildCategory.Resources);
            CloseTheFlyout();
            bool openingClears = Plain(resources) && Gold(_quarryButton);
            JsonObject opened = TheViewSection();
            StartTheSeen(JsonNode.Parse(opened.ToJsonString()) as JsonObject);
            bool openedKept = Plain(resources) && Gold(_quarryButton);
            _mineKnown = true;
            RefreshTheStrip();
            bool mineLightsAgain = Gold(resources) && Gold(_mineButton) && Gold(_quarryButton);
            _quarryButton?.EmitSignal(BaseButton.SignalName.Pressed);
            _mineButton?.EmitSignal(BaseButton.SignalName.Pressed);
            _map.PutTheToolDown();
            bool picked = Plain(resources) && Plain(_mineButton) && Plain(_quarryButton);
            if (!quarryLights || !openingClears || !openedKept || !mineLightsAgain || !picked)
            {
                faults.Add($"the quarry learned lights it and Resources {quarryLights}; opening Resources clears it and keeps the quarry {openingClears}, through a save {openedKept}; "
                    + $"the mine lights Resources again {mineLightsAgain}; both picked, nothing is marked {picked}");
            }

            // ⭐ The cottage lights Homes (b, his word); chosen, it goes. A section from D880 — no "shown" — calls it old news.
            Button homes = _categoryButtons.Find(c => c.Category == BuildCategory.Homes).Button;
            Button cottages = _homesAs.Find(h => h.Kind == BuildingKind.Cottage).Button;
            JsonObject beforeTheCottage = TheViewSection();
            beforeTheCottage.Remove("shown");
            _cottageKnown = true;
            RefreshTheStrip();
            bool cottageLights = Gold(homes) && Gold(cottages);
            StartTheSeen(JsonNode.Parse(beforeTheCottage.ToJsonString()) as JsonObject);
            bool oldNews = Plain(homes) && Plain(cottages);
            _seen.Remove(SeenKey(BuildingKind.Cottage));
            RefreshTheStrip();
            cottages.EmitSignal(BaseButton.SignalName.Pressed); // chosen — refused by the sim here, and seen all the same
            bool chosen = Plain(homes) && Plain(cottages);
            if (!cottageLights || !oldNews || !chosen)
            {
                faults.Add($"the cottage learned lights Homes and Cottages {cottageLights}; an older section calls it old news {oldNews}; chosen, neither is marked {chosen}");
            }

            // ⭐ The sim's gate, not the flags the first refresh has yet to set: a world that knows the bridge from its founding.
            _seen.Clear();
            SeeEverythingEarned(SimFactory.CreatePhase0(_loop.World.Config with { BridgeUnlockPopulation = 0 }, new Kith.Sim.Logging.InMemoryLogSink()).World);
            if (EarnedYet(BuildingKind.Bridge) || !_seen.Contains(SeenKey(BuildingKind.Bridge)) || _seen.Contains(SeenKey(BuildingKind.TownHall)))
            {
                faults.Add($"a village that knows the bridge before its flags are set: flag {EarnedYet(BuildingKind.Bridge)} (the pose wants it unset), counted as seen {_seen.Contains(SeenKey(BuildingKind.Bridge))}");
            }
        }
        finally
        {
            _literacy = literacyWas;
            (_quarryKnown, _mineKnown, _cottageKnown) = (quarryWas, mineWas, cottageWas);
            _seen.Clear();
            _seen.UnionWith(seenWas);
            _shown.Clear();
            _shown.UnionWith(shownWas);
            RefreshTheStrip();
        }

        return faults.Count == 0
            ? "[widths] new: ✅ nothing new at the founding; a library learned lights Knowledge and the Library in red with the title menu's red stroke under each, and no seal; "
              + "opening Knowledge clears it, picking the Library clears it; the seen set through the save's view section comes back as it went, "
              + "and a save without one sees everything earned; the quarry lights Resources though it was opened before, opening it clears it "
              + "(through a save too), the mine lights it again; the cottage lights Homes, and a D880 section calls it old news; what a village "
              + "knows is asked of the sim, before the bar's flags are set"
            : $"[widths] new: ⛔ {string.Join("; ", faults)}";
    }
}

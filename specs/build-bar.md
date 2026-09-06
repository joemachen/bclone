# Spec: The build bar — one strip, three tabs, and a menu that reads the catalogue

> Status: **▶️ IN PROGRESS, 2026-09-06** — spec written first, code follows. Nothing here has
> shipped yet; when it does, this line says so and the §7 entry number goes beside it.
> · Owner: Joe + Claude Code
> Format per `METHODOLOGY.md §2`. Implements Joe's UI mockup, second half. Resolves
> `buildings-catalog.md §8.2` (D223's deferral) and closes `content-inventory.md` finding 5's
> reachability hole.

---

## 1. Goal

**One strip at the bottom of the screen holding everything the player does to the valley**, with
**BUILD / REMOVAL / HARVEST** tabs, a category filter inside BUILD, the speed controls, and the
panel toggles — replacing the three separate rows that live in `Main.BuildControlPanel`,
`Main.BuildBuildMenu` and `Main.BuildHarvestMenu` today.

And underneath it, the thing that makes the strip possible at all: **the build menu reads the
buildings catalogue** instead of being ten hand-written buttons.

---

## 2. Which pillars / non-negotiables this serves

- **§1.1 Legibility, hardest.** Three menus that overlap and a mode you cannot see is the
  opposite of legible. **A lit tab is the game telling you what is in your hand** — today
  `VillageMap` knows and cannot say, which is why nothing highlights.
  ⛔ **This is also the constraint on the icons:** an icon-only strip is a memory test. Every
  button keeps its word under its mark.
- **§1.2 Meditative pace.** A bar you hunt through is a click-farm in a different medium. The
  filter exists so that forty-five buildings are still *browsed* rather than *searched*.
- **§1.6 Traceable over clever.** Seven `Begin*` methods each hand-clearing eight fields is the
  clever-by-accident shape: it works until the eighth brush forgets one. **It already has** —
  see §5.
- **⛔ D26 — no image assets.** Icons are drawn from polygons and rects, as `TradeGlyph` draws
  a trade. Emoji are rejected for the same reason the goods table rejects them: a glyph *"is at
  the mercy of whatever the default font happens to cover"*.
- **D103 — a feature the player cannot reach does not exist.** That is the rule this spec is
  mostly about honouring at scale.

---

## 3. The four calls (Joe, 2026-09-06)

1. **Three tabs, absorbing the strays.** There are more brushes than tabs, and rather than grow
   the tab row they are placed by what the player is *doing*:
   - **BUILD** — every placeable building, plus **Paint land** (the housing brush) and **Move**.
   - **REMOVAL** — **Demolish**, **Take back** (unpainting homes), **Empty**.
   - **HARVEST** — **Trees / Stone / Iron / All**, plus **Unmark**.
   ⚠️ **Move and Empty were not named in his answer and are this spec's reading of it**: Move is
   a placement act, Empty takes goods out of a store. Flagged rather than assumed silently.
2. **Catalogue-driven now.** ⛔ **The category stays out of the sim** — `buildings-catalog.md
   §8.1` settled that it is *"a column the sim does not want … it is presentation"*, and this
   spec does not reopen it. The mapping lives in the view.
3. **ALL is a category filter inside BUILD**, not a fourth tab: ALL / Works / Food / Resources /
   Storage & trade / Knowledge / Civic / Homes / Other.
4. **One bar.** It replaces the control bar rather than sitting beside it — speed, tabs, strip
   and panel toggles are one piece of furniture.

---

## 4. Data model

### 4.1 `MapTool` — what is in the player's hand

New, in the **view only**. `VillageMap` holds one value describing the tool, and the bar reads it:

```
enum MapTool { None, Building, Demolishing, PaintingHomes, ErasingHomes,
               Harvesting, Unmarking, PaintingGround, ErasingGround, Moving, Emptying }
```

- `Tool` — the current tool, publicly readable, privately set.
- `PendingBuilding` / `PendingHarvest` — which building or which harvest mode, so the strip can
  light the **specific** button and not just the tab.
- `ToolChanged` — an event raised wherever the tool changes. **`Announce()` is the seam**: every
  `Begin*` already calls it, so no new call sites are introduced.
- **`SetTool` is the only writer of the underlying fields.** Each `Begin*` keeps its signature
  and its doc comment and delegates.

⚠️ **`PaintingGround` / `ErasingGround` are on this enum but not on the bar.** The work-ground
brush belongs to a *building* and is reached from that building's panel (D86, D93: *"a control
that is always there but never says WHAT it acts on is the thing you hunt for"*). It needs a
value here only so the bar can unlight every tab while it is in hand.

### 4.2 The category — a view-side table

`BuildCategory { Works, Food, Resources, Storage, Knowledge, Civic, Homes, Other }`, with
`CategoryOf(BuildingKind)` mirroring the groups `BuildBuildMenu` writes by hand today and
returning **`Other` for anything past the built-ins.**

⭐ That is exactly the cheap option `buildings-catalog.md §8.2` recorded — *"built-ins keep
hand-placed ordering, and anything with an id above the built-ins appears automatically in an
'Other' group"* — arriving **inside** the catalogue-driven version rather than instead of it.
A modder's building gets an icon, a group and a place in the strip on the day it has a row.

### 4.3 Gating — a predicate, not two held references

Today `Main` holds `_libraryCategory` and `_civicCategory` and flips their `Visible`. That does
not survive a catalogue walk. It becomes `VisibleWhen(BuildingKind, SimWorld)`, defaulting to
always, with two entries:

- **Library** — `world.HasLiteracy`. ⭐ *"the library is in the UI from the beginning —
  shouldn't it show up once gifted?"* (Joe). The button sitting there for eighteen years was the
  gift being spoiled before it arrived.
- **Town hall** — `world.SaidTheFoundersAreGone` (D252).

⛔ **Both keep their new-gift highlight.** It is named behaviour (D103, D252), not decoration:
the tint means *"this is the gift"* and clears when the free one is spent.

---

## 5. Edge cases & failure modes

### 5.1 ⛔ Three live bugs the collapse fixes, found while writing this spec

The `Begin*` methods do **not** all clear the same fields. `BeginBuilding`, `BeginDemolishing`,
`BeginMoving` and `BeginEmptying` leave `_groundFor` and `_harvestMode` alone. `Announce()` tests
`_groundFor` **first**. So:

1. **Paint ground for a hut, then press Demolish** → the message reads *"Drag to give ground to
   forester's hut — its people work what you paint."* A sentence about the wrong tool, over a
   tool that is not that one. **This is the exact fault D93 already fixed once, arriving from the
   other direction.**
2. **Paint ground, then right-click to cancel** → the cancel path calls `BeginBuilding(null)`,
   which leaves `_groundFor` set, so the game answers a cancel with *"Right-click to stop."*
3. **⛔ The worst: demolish the hut mid-stroke.** `PaintAround` sets `_groundFor = 0` and
   `continue`s — so every remaining tile in that stroke falls through to the residential arm and
   **paints housing land the player never asked for.** The comment above it claims it *"stops
   rather than half-painting"*; it does not. It abandons the tool and keeps the brush.

All three are one cause — **eight fields that must agree, cleared in seven places** — and one
`SetTool` is the fix. (3) additionally has to leave the stroke rather than `continue`.

### 5.2 The bar must never clip

⛔ **`HFlowContainer` everywhere, never `HBoxContainer`.** D242: an `HBoxContainer` has no way to
fail gracefully — *"Pause"* was clipped to *"use"* on the left edge and *"Cancel"* sat half off
the right — and the strip **grows by a whole category mid-play** when literacy lands and again
when the last founder dies. A flow container puts the overflow on a second row.

⚠️ **And the bar is widened before it is scaled** (D305). Its unscaled width is `window / scale`,
because scaling a wrapping bar to four fifths would leave it four fifths as *wide*, wrap more
rows, and eat **more** of the valley than it saved.

### 5.3 A hidden control measures as nothing

`ProbeTheControlBar` already un-hides the Knowledge and Civic groups before measuring and puts
them back after. **With tabs, two thirds of the strip is hidden at any moment**, so the probe
must walk every tab and every chip and report the widest. Without that it measures the BUILD tab,
reports a bar that fits, and ships the *"correct at startup, wrong later"* fault D242 exists
because of.

### 5.4 What is deliberately not on the bar

- **The house.** `BuildingKind.Home` has a catalogue row and is placed by the land brush, never
  by a button (D42, D102). It is the one row the walk skips.
- **The work-ground brush** — §4.1.
- **The staffing control** — it moved to the building panel and stays there (D93).

### 5.5 `VillageMap` still compares a building by kind

`_building == BuildingKind.Market` gates the market's service-area preview. **That stays.** It is
a *preview* in the view, not a rule in the sim, and it is noted here so it is not mistaken for
the hole this spec closes.

---

## 6. How it is tested

⚠️ **The view has no automated verification of any kind** (D11, D160). That is a known, accepted
hole. What exists instead:

1. **`dotnet build src/Bclone.Game/Bclone.Game.csproj`** — the only build that compiles the view.
   ⛔ A green root `dotnet build` says nothing about it (`METHODOLOGY §116`).
2. **`BCLONE_PROBE_WIDTHS=1 "$GODOT" --headless --path src/Bclone.Game`** — the only thing that
   catches a view throw. A `ProfessionName` crash once fired **3,609 times** while the suite was
   green. Extended per §5.3.
3. **`dotnet test`** — **927 / 0 / 2 of 929, unchanged.** Every file this spec touches is in
   `Bclone.Game`, which is not in the solution. **Any test movement means the change leaked.**
4. **Joe's eyes**, which is the actual test.

---

## 7. Definition of Done

1. This spec current, and its status line honest.
2. `VillageMap.Tool` readable, `ToolChanged` raised, and **`SetTool` the only writer** of the
   brush fields — §5.1's three bugs gone with it.
3. The build strip walks the catalogue; **a row added to `data/sim.config.json` gets a button
   with no code change.**
4. Tabs and chips light from `Tool`, and right-click unlights everything.
5. Library and town hall still appear when earned, still highlighted while they are the gift.
6. The probe walks every tab; the widest fits the window.
7. Suite unmoved: 927 / 0 / 2 of 929.
8. `DESIGN.md` §6 + §7 updated; `buildings-catalog.md §8.2` resolved rather than deleted.

---

## 8. Open

1. **Move and Empty's tabs** — §3.1. This spec's reading, not Joe's word.
2. **Icon-and-word, or icon-and-tooltip?** This spec commits to the word, on §1.1. If forty-five
   buildings make the strip too tall even filtered, the word is what gives — and that is a
   legibility trade, so it is Joe's, not a tuning decision.
3. **Does a modded building deserve a drawn glyph?** It gets the drab square today, which is what
   `TradeGlyph` and `GoodsPalette` both give an unrecognised thing. A `glyph` column on the row
   would be the sim carrying the view's vocabulary — the same objection §8.1 made to the
   category — so the answer is probably no, and it is written here so it is not re-derived.

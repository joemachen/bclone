# The title screen, painted — a parchment menu, and the valley in four seasons behind it (A178, A179)

> **Status: ✅ PART 1 MERGED INTO `main` (A186, `1678471`, his *"it all looks good. merge it."*); built A180 — Joe: *"All as recommended - and add a 'settings' option to the title
> screen"*; ⏸️ HIS PLAY OF PART 1 (§9), then part 2 once he has played his save to maturity.** Specced and part 1 mocked A179.
> Art lane (CLAUDE.md
> *Two tracks*), piece (l) of the art list, Joe's *"After the bridge"* and then *"start the title screen"*; claimed A178. His
> amended brief is `D:\Projects\brushes\title-menu\brief.md` — this spec follows it, and §8 lists every place it departs.
> View only: nothing in the sim, `data/` or `tests/` changes. Part 1's mock is drawn by the game itself from a copy of his
> Hollowmere save, and from none: `D:\Projects\brushes\title-menu\title_panel_mock.jpg`.

## 1. What Joe asked, and what is wrong today

Two problems with the title (`TitleScreen.cs`, D516–D517, `title-and-pause.md §3`):

- **The menu text is nearly invisible.** The band down the left is a dark scrim (`UiLook.TitleBand`, black at 82 %) and,
  under the parchment theme, its words are ink — dark on dark. Rendered today: *Kith*, the version and the four items are
  all but unreadable (the mock's top row).
- **The background says nothing about the game.** It is the newest save's valley baked flat by `ValleyTexture` — terrain
  only: no buildings, fields, people, trees or season. On his Hollowmere at year 83 it is green blobs and a blue river.

His brief answers each in turn: **part 1** a parchment menu panel (stop for his play), **part 2** a re-runnable tool that
captures one committed save of his in four seasons from the real renderer, **part 3** those four composited into one
picture behind the panel.

## 2. Part 1 — the menu panel

**The look** (mocked; drawn by the prototype the mock was rendered from, `TitleScreen.cs.mock.txt` beside the mock):

- **A parchment panel docked to the left**, full height, in place of the scrim: the same painted sheet, torn edge and ink as
  the game's windows and the new-valley screen (`UiLook.Panel()`, `chrome/sheet.png`). 360 logical px at 1280 wide, and
  28 % of a wider window as the new-valley panel's (`PanelShare`), never narrower than its contents.
- **The title, *Kith*, large in the blackletter** (Grenze Gotisch, `UiLook.Blackletter`, 96 px) in ink, a short rule of
  rubric under it, about a third of the way down.
- **The items in the heading face, Metamorphous**, 22 px, ink, centred: **Continue · New village · Load… · Quit** — their
  behaviour exactly as today (`LoadAsked`, `NewVillageAsked`, `QuitAsked`, the `LoadMenu` popup, the refusal sentence under
  them in rubric ink).
- **The primary item is the one red button** — the new-valley screen's *Found a new village* (`UiLook.PrimaryButton`: filled
  rubric, paper words): **Continue when a save opens, otherwise New village**.
- **With no save, Continue is not shown** (⏸️ call 1), and nor is *Load…*, which has nothing to list (today it is greyed, at
  well under 4.5 : 1). The first-run screen is *Kith*, **New village**, Quit.
- **The states, all in the rubric:** hover a faint rubric wash with rubric words; keyboard focus a rubric underline with rubric
  words; pressed the theme's rubric swash; the primary's focus an ink ring round the red button.
- **Keyboard:** the primary item has focus when the screen opens; ↑ ↓ move through the shown items, Enter presses.
  ⚠️ **Found by the mock:** two presses of ↓ from *Continue* landed on *Quit* — Godot's `MenuButton` is not focusable by
  default, so *Load…* was skipped. The build sets its focus mode, and the probe walks the order.
- **The version** small and quiet at the bottom of the panel (13 px, `InkDim`).
- **Nothing sits on the background**: every word is on the panel, ink on parchment, each at least 4.5 : 1 — measured by the
  probe against the panel's paper as the `ui contrast:` line measures every other word (`ParchmentLook.Words`).
- **From 1280 × 720 up.** The panel's contents fit 720 high (they need about 520); nothing scrolls.

**Kept:** the backdrop (today's flat valley) until part 3 replaces it — part 1 is judged on today's background, as the brief
asks; the slate theme's look (with the parchment theme off, `UiLook`'s named roles fall back, as every screen's do).

**Code:** `TitleScreen.BuildTheBand` rebuilt as the panel; its items' styles as named roles in `ParchmentLook` (a
`TitleItem` variation: the rubric hover, focus and pressed) so the title asks the theme rather than painting itself; the
primary picked by one pure function, `PrimaryOf(saves)`, which the probe asks; the `LoadMenu` button made focusable for the
title (the pause screen's too — the same class — which is a fix there as well).

**Probe** — the `title:` line (`Main.ProbeTheTitle`, `Main.cs`, the systems lane's file: ⏸️ call 2) grows from today's four
checks to: the panel is the parchment sheet and docked left; *Kith* in the blackletter and the items in Metamorphous; with
no save, Continue and Load… hidden and New village the primary, focused, enabled; **a second title posed with a save listing**
(no disk read: a listing whose path does not open falls back to a rolled valley, as today) — Continue shown, the primary,
focused, New village plain; ↓ from the primary visits every shown item in order and wraps; every word's contrast against the
panel ≥ 4.5 : 1; at 1280 × 720 the panel's contents inside its height and the panel narrower than 40 % of the window. Red-
checked, the reds counted (D326). The test project cannot reach Godot code, so there is no xunit test — the brief says so.

## 2a. Settings on the title (A180 — Joe: *"add a 'settings' option to the title screen"*)

His two answers, asked when the item was added: *"Player's settings"*, and *"Yes, edit them"* (`Main.cs`, `Main.Settings.cs`).

- **An item, *Settings*, between *Load…* and *Quit*.** It turns the panel to the settings — the same sheet, a page in it —
  and *Back* (or Esc) turns it back, the keyboard on *Settings* again.
- **What it holds — the player's own, never the village's** (`settings-persistence.md §3`'s line): **The window** — the UI
  size (− 75 % +, steps of five) and *full screen*; **On the map** — every tick the village's Settings has there; **Alerts** —
  *go back to my speed after an alert*. Each as the player last left it. **Not here**, because an open village owns them: the
  window list and *Reset window positions*, *Routes* and *Paths* (overlays of a map that is not open), *How the village runs*
  (hashed, the village's), *About this run*.
- **One table** (`Main.PlayerTicks`): every tick's key, words, default and heading. The village's Settings makes each tick from
  its row (`Main.Tick(key)`) and the title lists the same rows — so the two can never offer different words or defaults. The
  `full screen:` probe line checks every tick the village's Settings holds is a row, and every row a tick.
- **Kept at once.** With no village open nothing else writes the file, so a tick or a step of the dial is written as it moves
  (`TitleTickChanged`, `TitleUiScaleMoved`); the village reads it when it opens, as it always has (`ApplySettings`). *"Kept for
  the next village you open."* under the list says so. *Full screen* applies at once.
- ⚠️ **Full screen now applies from the title** (a departure, §8): it used to wait for a village's Settings to be built, so the
  title opened in a window and the game went full screen at the founding.

## 3. Part 2 — the capture tool

**One command, no manual steps, the same picture each run** — `tools\title-capture\capture.bat` (and `capture.sh` for a cloud
session), which runs Godot on `tools/title-capture/capture.gd` against a **committed save** and writes the four captures and
the composite (part 3).

- **The village is a committed save Joe plays to maturity** (⏸️ call 3), not a scripted build: an unattended village dies by
  design (D110, D143) and the only code that plays an opening lives in the test project (D160's reason for deleting the
  screenshot hook). Copied to `tools/title-capture/village.save` (his are 29–40 KB). **A save that no longer opens fails the
  tool loudly** — *"the title's village no longer opens (…the save's refusal…): open it in the game and save it again"* — and
  writes nothing; it never draws something else.
- **What the village should hold:** every building type his village has, fields, roads and worn paths, people at work, the
  river. His two oldest Hollowmeres, read today: **honey-harrow, year 84** — 82 people, all three house kinds (17 manors, a
  cottage, a cabin), 17 trades, a market, barn, trading post, library, town hall, two wells, a bridge; no school. **rook-hare,
  year 85** — 94 people, 22 cottages and no other house kind, the same trades. ⭐ honey-harrow, with a school built in.
- **The frozen world:** the save opens paused; the sim is never stepped between the four captures. People stay where they
  stand, mid-stride and mid-swing.
- **A render-only season override** — one view field, `VillageMap.SeasonShown` (null in play), read by the single place the
  view asks the season (`TheYearAt(...)`'s callers) and set only by the tool. ⚠️ **Never sim state:** the tool asserts the
  world's hash is the same before the first capture and after the last, and fails if it moved. **What follows the forced
  season** (⏸️ call 4):

  | Follows the season shown | Stays as the save holds it |
  |---|---|
  | the grass (`grass_season`) | buildings, roofs, yards, stock |
  | the trees and the rock and ore sprites (their season atlases) | paths and their wear |
  | **the fields**: young in spring, growing in summer, ripe in autumn, stubble in winter, drawn from the season and not the tile's stage (`CropStageAt`) | people — where they are and what they do (a farmer reaping in the winter band is the cost of seams that line up) |
  | **the snow lying** (none, none, none, full) | the river, the bridge, the hills |
  | chimney smoke (winter only, as `HearthBurns` says) | the falling snow is **off** in every capture — flakes are weather, not a season |

- **The renderer is the game's**: the map in a 3840 × 2160 SubViewport, the watercolour on, no HUD, panels, debug readout,
  selection or rings. Headless Godot draws nothing, so the tool opens a real window for the few seconds it runs.
- **The framing:** close enough that buildings and people read — the zoom where a house is ~40 px wide at 4K; the village's
  centre (the founding site's centroid of what stands) in the middle of the area right of the panel.
- **⛔ The blocker** (his): if the four seasons do not look different enough to tell apart at a glance, I stop and show him the
  four before building the composite.
- **Not in the suite**, ever: the tool is run by hand, and the probe never starts it.

## 4. Part 3 — the composite (the same command writes it)

- **Four bands, spring · summer · autumn · winter, left to right**, splitting in equal widths only the area right of the
  panel; plain spring under the panel.
- **The seams tilted 12–15° from vertical**, chosen per save so no seam runs within 20° of the river's own heading where it
  crosses. **Both kinds rendered for him to pick** (his brief): a thin ink line, and a narrow soft blend.
- **Alignment is the capture's test:** the four are the same world from the same camera, so a building or road crossing a
  seam continues unbroken; the tool checks the four captures agree pixel for pixel wherever nothing seasonal is drawn, and
  fails loudly if not.
- **The asset:** `src/Kith.Game/textures/ui/title/valley_seasons.jpg`, 3840 × 2160, committed with its `.import`
  (a JPG of ~2–4 MB; a PNG would be ~20). The panel's share of the width is fixed when it is written (28 %), so the bands
  line up with the panel at 16:9.
- **On screen:** scaled to cover the window (`KeepAspectCovered`), anchored on the village centre so it stays in view at
  every aspect from 16:10 to 21:9 (the probe checks the centre's point lands right of the panel at 16:10, 16:9 and 21:9).
- **⚠️ The trade-off he was told:** the title stops showing his newest village behind it — D517's deliberate touch — and
  shows the committed one instead. The rolled valley for a player with no saves goes too.

## 5. Order of work

1. ⏸️ His calls (§7). 2. **Part 1** built, the four checks, the renders — ⏸️ **his play**. 3. He plays his save to maturity
and names it. 4. **Part 2**: the override, the tool, four captures — the blocker check, shown to him. 5. **Part 3**: the
composite with both seams — ⏸️ his pick. 6. On screen, the four checks — ⏸️ his play, then the merge at his word.

## 6. Not in this piece

The pause screen's look (its sheet already parchment); the new-valley screen; music or motion on the title; a title that
changes with the real season.

## 7. ✅ Joe's calls — *"All as recommended"* (A180, 2026-11-22), and Settings added (§2a)

1. **With no save, Continue and Load… are not shown** — the first-run menu is *New village* (the red button) and *Quit*.
   ⭐ **Hidden.** *Or:* both shown greyed (the mock's last picture) — but greyed words cannot meet 4.5 : 1.
2. **His OK for the files:** `TitleScreen.cs` (the screen), `ParchmentLook.cs` / `UiLook.cs` (its styles, art's), and
   **`Main.cs`'s `title:` probe** (the systems lane's file, a few lines in `ProbeTheTitle`). ⭐ **Yes.** Part 2 adds the
   season override in the map's art-lane files (`VillageMap.Watercolour.cs`, `.Meshes.cs`, `.Smoke.cs`, `.Snowfall.cs`) and
   a new `tools/title-capture/` folder.
3. **The village behind the title:** ⭐ **his Hollowmere *honey-harrow*, year 84**, with a school built in, saved under a name
   he tells me. *Or:* rook-hare, year 85 (more people, cottages only). *Or:* another village he would rather be seen.
4. **What follows the forced season** — §3's table. ⭐ **As the table says.**
5. **The panel at 360 px of a 1280 window, 28 % of a wider one** (the new-valley panel's share). ⭐ **Yes.**
6. **The composite on screen in place of his newest village** (D517's touch lost). ⭐ **Yes** — it is his brief; asked only
   so the loss is chosen, not found.

## 8. Where this departs from his brief, and why

- **Load… hidden with no save**, as well as Continue (call 1): greyed text fails his own 4.5 : 1, and there is nothing to load.
- **`MenuButton` made focusable** — not in the brief; the mock found the arrow keys skipping *Load…*.
- **The tool's script lives in `tools/`, its output in `textures/ui/title/`**: the brief names neither; `tools/` keeps it out of
  the exported game and out of the suite.
- **The composite is a JPG**, not a PNG: ~2–4 MB against ~20 for a picture that is painted already.
- **Falling snow off in every capture** — the brief speaks of snow on the ground only.
- **The tool checks the world's hash and the captures' agreement** — not asked for; they are what make "same world, same
  camera" a fact rather than a hope.
- **Full screen applies from the title** (§2a), not from a village's founding — Settings on the title needs it to, and the
  game had been opening in a window and jumping to full screen a screen later.
- **Settings' long list scrolls** inside the panel at 720 high (the new-valley panel's way); the menu itself never does.

## 9. 🔨 Part 1 built (A180, 2026-11-22)

- **`TitleScreen.cs`**: the panel as §2 says, two pages in one sheet (the menu, Settings); `NewestThatOpens` decides
  *Continue*; the arrow keys linked to wrap (`LinkTheItems`); the width on every resize, never per frame (`PlaceThePanel`).
  `TitleScreen.Probe.cs`: what the probe asks of it. **`LoadMenu`'s button focusable** — the pause screen's too.
- **`ParchmentLook.cs` / `UiLook.cs`**: two roles, `TitleItem` (the heading face at 22, a rubric wash under the pointer, a rubric
  underline and rubric words with the keyboard) and `TitlePrimary` (the new valley's red button in the heading face, an ink
  ring when it has the keyboard). On slate the items fall back to plain buttons.
- **`Main.cs`**: the title gets its settings doors and applies full screen; `ProbeTheTitle` asks the title's checks of itself and
  of a title posed over a save (its grid tick set away from the default first); the fifteen Settings ticks made from the table;
  the `full screen:` line's table guard. **`Main.Settings.cs`**: `PlayerTicks`, `Tick`, `TickValue`, the title's two writers;
  `WriteTheSettings` says a refusal to the village's log only when there is a village.
- **Probe** — `title:` now reads, green: *a parchment sheet docked left, Kith in the blackletter, the items in the heading face;
  with no save Continue and Load… hidden and New village the red primary with the keyboard; over a save Continue the primary and
  New village plain; ↓ walks every item shown and wraps; every word at 4.5 : 1 or more; it fits 1280 × 720; Settings lists the
  player's ticks as last left, a tick goes to the file, Back returns.* **Red-check: 19 mutants, 17 red, two zeros written down** — *Kith* losing its blackletter override (the theme's default
  face *is* the blackletter, so nothing the probe can see changes) and full screen not applied on the title (headless, the
  display takes no mode; Joe's eyes). One mutant (a Settings page missing its first group) first killed the probe with no line
  — unscored, never a red — so the title's checks are now in a `try` and it re-ran red by its throw (`redcheck180.py`,
  `red180.log`, `red180b.log` in `D:\Projects\brushes\title-menu\`).
- **Renders** of the build from a copy of his Hollowmere save and from none, with the keyboard moved and Settings open:
  `D:\Projects\brushes\title-menu\built\`.
- **The four checks:** suite 2206 / 0 / 5 of 2211 in 20m16 (no sim, data or test file changed); view build 0 warnings;
  probe 178 lines, 140 ✅, 0 ⛔, bar 183, `title:` ✅, `full screen:` ✅ with the table's guard, tile centres ✅, new game ✅,
  `done.`; goldens 457, none moved.
- ⚠️ **Joe's own settings file says full screen is off**, so on his machine the title now opens in a window, as his village
  already did — before, the title opened windowed whatever the file said and the founding applied it.
- **What to play** (`.\run.bat` from the art worktree): the title reads, ink on parchment; ↑ ↓ Enter work it, *Load…*
  included; *Settings* turns the panel, a tick changed there is the village's when one opens; *Back* and Esc return. With no
  save (a fresh `APPDATA`, or rename the saves folder) only *New village*, *Settings* and *Quit*. Sheet of the build's renders:
  `D:\Projects\brushes\title-menu\title_part1_built.jpg`.

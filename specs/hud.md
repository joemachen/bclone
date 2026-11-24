# Spec: The HUD — give the map back

> **Status: ✍️ SPECCED AND MOCKED ON `art` (A207–A215, 2026-11-23); NOTHING BUILT. ✅ JOE'S NINE CALLS ALL AS RECOMMENDED,
> AND THE WINDOWS' BUTTONS A COLUMN (A215: *"stack the window buttons as a narrow column"*, *"agree on the 9 calls"*).**
> ✅ **The art lane's controls painted and in on `art` (A216, Joe: *"Yes, paint them"*; the pinned pin too, A217: *"Yes, paint
> it"*), not wired** (§8.3).
> ✅ Merged into `main` at his word (A218, *"Merge art"*). 🔨 **Being built on `slice/hud` (D873, 2026-11-23), §14's
> steps in order** — the line below each step in §14 says what is built. The build
> is its own **systems slice** (`slice/library-borrowing`, which it waited on, merged as D871): every bar, window and panel below is drawn in
> `Main.cs` and its partials, which are the systems lane's (HANDOFF's art table; D842). The art lane's share is two
> repainted controls (§8.3) and the mocks. Owner: Joe + Claude Code. Format per `METHODOLOGY.md §2`.
>
> **Input:** Joe's outside review, `Kith HUD and information architecture proposal.md` and its `.jpg`, untracked in the main
> checkout's root and never committed. It was written from two screenshots; this spec is what survived reading it against
> the code and the log, and Joe's calls on the conflicts (A207, quoted there in full).
>
> **Mocks** (outside git, `D:\Projects\brushes\ui\hud\`): `hud_before_after.jpg` (today against at rest), `hud_at_rest.png`,
> `hud_flyout.png` (Food open), `hud_details.png` (the food figure hovered, a newly learned category and building, a
> brush in hand). Built on the game's own capture of a new village at 2560 × 1440 and the default size
> (`render_hud.gd`, `run_hud.sh`, a fresh `APPDATA` so the settings are the defaults); the minimap and the log are the real
> windows cut from that capture, everything else is the game's painted sheets, icons and fonts (`mock_hud.py`).

---

## 1. Goal

**Give the map back without hiding anything a decision needs.** On a new village today the HUD covers **40 %** of a
2560 × 1440 screen at the default size — measured, not guessed: the pixels that change when `H` hides the HUD
(`mock_hud.py`; the proposal's eyeball 40 % was right). Most of it says zero: nine of ten goods, three of five
headcounts, eight Professions rows at 0 / 0, three greyed categories. **At rest, as specced here, it covers about 15 %**
(15.1 % in the mock, 16.0 % with the dev bar a debug build adds).

## 2. Which pillars and non-negotiables this serves

- **§1.1 Legibility, hardest — and it cuts both ways.** A zero is information only when it is a surprise. Nine zeros on
  Day 1 are noise that hides the one number that matters. ⛔ **But nothing leaves *reach*:** every good the village
  knows stays in `more ▾` with its zero (D502 still hides what it does not know), every window is one click on the
  windows' column, every hotkey is in its button's tooltip. *Hidden from the front, never from the player.*
- **§1.2 Meditative pace.** Fewer windows open is less to manage; flyouts open on a click, never on a hover.
- **§1.6 Traceable over clever.** The food figure is one existing sim number (`FoodTheVillageHolds`) and one division by
  the village's own eating, both in its tooltip — never a forecast.
- **D103, a feature the player cannot reach does not exist** — the windows' column is how a closed window stays reachable.

## 3. What is there today (read off `art` at `58f3dc3`)

| Region | Built at | What it holds |
|---|---|---|
| Top bars | `BuildTopBars` `Main.cs:4674`; boxes `BuildResourcesBox` 4711, `BuildVillagersBox` 4894 | Goods in two fixed rows (`BarRows` 4703: forage, wheat, fish, meat / logs, firewood, stone, iron, stone tools), zeros shown, `more ▾` for the rest (4811). Villagers, adults, children, students, elders; *name · date · households*; the unhoused count (faint until there is nowhere to build, 3453, D568/D571) and the nomads tick (4944, D572) |
| Frame readout | `_frameCounter` `Main.cs:9245`, debug builds only (9240) | fps and frame timings, two lines under the top bar |
| The control bar | `BuildControlPanel` 6081 | Row 1: Pause, 1×–20×, PAUSED, Skip 1y / 10y (`AddTheSkipControls` 9294, `OS.IsDebugBuild` only), Centre on village, Settings, (Records, Lore), BUILD / REMOVAL / HARVEST, Cancel. Row 2: *Show:* All … Other (greyed when empty, D396), the tab's note, *Brush: square*. Row 3: the strip (`BuildTheStrip` 8289). Row 4: the hotkey line |
| Windows | `Floating` 6627 / `Dress` 6908; defaults `ArrangeDefaults` 6519 | Open on a new game: **The village** (roster), **Professions** (D484), **The valley**, **Village log**. Closed: Stock limits, Build queue, Lore (opens on the first unlock), Settings. What's here on selection. Shown, fold and place saved (D504) |
| UI size | `PlayerSettings.cs:47–56` (in `Kith.Sim`) | 55–115 %, step 5, default 75; applied as `Control.Scale` per floater (6317); saved `"ui_scale_percent"` with `"version": 1` |
| Colours | `ParchmentLook.cs:46` | `held` and `stopped` are both `RubricInk`; every pressed button's font and swash is rubric (277–290); a ticked box is a red wax seal (296–303) |
| Numbers | `Amount()` 4981 → `Body()` 9423 | Grenze Gotisch, old-style figures — its 0 reads *o* |

**The probe today:** `bar height:` checks the bar is one height across every tab and filter (spread ≤ 1 px) — ⚠️ **the
number 183 is written only in `CLAUDE.md`; no code asserts it.** `filters:` wants Knowledge, Civic and Other greyed at the
founding. `bars:` poses every cell at `+12,345`. `windows:` wants Professions shown and unfolded (D484).

## 4. What is settled

| Call | Who | What it changes | Entry |
|---|---|---|---|
| The bar may hide a good: a good at zero leaves the front | Joe (*"the bar can hide goods. lets proceed and we can iterate if i hate it"*) | D378's *"the bar never hides a good"* | A208 |
| A food total on the strip again: food in storage, not the larders | Joe (*"yes, do a food total again"*) | D410 (2), *"no total anywhere"* | A209 |
| Categories with nothing earned are hidden; a newly learned category or building is marked until clicked | Joe (*"hide them. and highlight newly unlocked categories and buildings until they are clicked on"*) | D396 (5), *"disabled, never hidden"* | A210 |
| The roster starts closed; the log and Professions stay on screen | Joe (*"Close the roster by default"*, *"Keep the village log properly visible, keep professions on screen somehow"*) | the roster's open default; the proposal's *nothing open* refused | A211 |
| 20×, Skip 1y and Skip 10y in their own floating dev bar | Joe | D254's 20× leaves release builds | A212 |
| What is 75 % today is 100 % | Joe (*"what I use as 75% should be 100% size going forward"*) | D305's numbers | A213 |
| The nomads switch stays on the strip | Joe (*"keep nomads toggle in top bar"*) | nothing — D572 stands | A207 |
| The unhoused count stays calm | Joe (*"its not super important"*) | nothing — D568/D571 stand | A207 |
| Speed beside the date; tools apart from buildings; hotkeys into tooltips; brush shape only while a brush is held; categories as flyouts; red for warnings, lapis for selected and on; a food runway (firewood later); the log never a ticker; the founding line; lining figures | the coordinator's view, *"i agree with you mostly"* | §5–§11 | A207 |

## 5. At rest

| Region | Where | Holds | Shown |
|---|---|---|---|
| **The strip** | top left, one row | the season's icon, the village's name, the date · Pause and the speeds · food and runway, then each good the stores hold, `more ▾` · villagers, households, the unhoused · the nomads switch | always |
| **Dev bar** | floating, right of the strip by default | 20×, Skip 1y, Skip 10y; the frame readout | **debug builds only** |
| **The valley** | top right, as today | the minimap | open by default |
| **Village log** | right, under the minimap, as today (its 190 px list, its seven filters) | the stories | open by default |
| **Professions** | top left, under the strip | able adults and labourers, then a row for each trade with a workplace (§9.2) | open by default |
| **The windows' column** | bottom-left corner, one word a line | Centre · People · Professions · Stores · Build queue · Lore · Log · Valley · Settings (Records once the hall stands) | always |
| **The tool bar** | bottom centre | the categories with anything earned, then Paint land, Move, Remove ▴, Harvest ▴ | always |
| **A flyout** | above the tool bar, over its button | one category's buildings, or Remove's / Harvest's tools | while open |
| **Tool options** | beside the tool bar | Brush: square · round, and *Esc* to put the tool down | while a tool is in hand |

## 6. The strip

One `slip-long` sheet, one row (`hud_at_rest.png`). Its order, left to right:

1. **The season's icon, the name (Metamorphous), the date** — *Stonebridge · Day 1, Spring, Year 1*. Households move to §6.4.
2. **Time.** *Pause* — reading **Paused**, held in lapis, while paused, so the PAUSED label goes — then **1× 2× 4× 10×**, the
   chosen speed held in lapis (it is what play resumes at). 20× is the dev bar's (§7). Keys 1–4 as today; key 5 is the
   dev bar's too.
3. **Food** (§6.1), then **each good the stores hold**, in `BarRows`' order with the food goods taken out (§6.2), then `more ▾`.
4. **People:** *4 villagers · 2 households*; *children*, *students* and *elders* only when there are any; *adults* only when
   another kind is shown (alone it restates *villagers*); then the unhoused count exactly as D568/D571 (faint until there is
   nowhere left to build, then ink with the tooltip).
5. **The nomads switch** (D572), at the strip's right end: the seal and *nomads may come*, words in ink, the seal lapis (§10).

The strip's goods and headcounts come and go, so its width moves. ⛔ **The strip never runs under the minimap**: `bars:`
poses every good and every headcount at `+12,345` (as today) and the strip must end left of the right column at 1280
logical wide and 100 % (§12). *(Read "never wraps and never runs under" until D877:)* ⭐ **It wraps a whole group onto a
second line only when one line would reach the right column** (Joe, D877: *"Wrap to two lines"*) — D876 measured a busy
village's one line at ≈ 2,060 logical px against 1,238 of room at 16:10 (≈ 1,336 measured against the right column's
drawn edge); a new village is one line. The left column starts below where two lines would end, so a wrap covers nothing.

### 6.1 The food figure

**The number is `SimWorld.FoodTheVillageHolds()`** (`SimWorld.cs:1116`): every store that holds food — the cart,
granaries, warehouses, piles, markets — plus the food waiting in the workplaces and armfuls. **Larders are excluded by
construction** (its own remark, 951–955: *"a household's larder is food already distributed"*). That is Joe's
*"all food in storage (excluding food in larders)"* to the letter, and it is his own definition from D394: *"what is in
storage, in transit to storage and in markets — what is available to villagers outside of their home storage"*. It is
also what the birth gate, the quota and the food stock limit read, so **the strip and the sim cannot disagree.**
`FoodInGranaries()` was the other candidate; it misses the huts' buffers and is named for a building it no longer means.

**The runway:** *lasts N seasons* beside it, muted. `seasons = ⌊ food × 4 ÷ (adults × AdultFoodPerYear + children ×
ChildFoodPerYear) ⌋` (`VillageEconomy.cs:75, 79`; a child as the meal charges them). Over 8 seasons it reads in years
(*lasts 3 years*), over 10 years *lasts over 10 years*. **It cannot jitter:** it is computed from the headcount and the
stores, not a measured rate, so it moves only when food or a person does. ⚠️ **It is "if nothing more comes in"** — no
harvest, no foraging — and its tooltip says so; a forecast would be the black box §1.1 refuses.

*At the founding:* 1,200 ÷ (4 × 168 a year) = 1.79 years → **lasts 7 seasons**.

**Its tooltip** (`hud_details.png`): the total and what it counts in words; *on the shelves* (`FoodInGranaries`) and
*waiting in the huts* (`FoodWaitingInHuts`) when both are non-zero; each kind of food the village holds; *in the
larders* (`FoodInLarders`), named and **not counted**; and the runway's sum in words.

**When it warns** is call 3 (§13). Until Joe answers it never turns red: D410 removed the strip's shortage amber at his
word and this spec does not put it back unasked.

### 6.2 Which goods are on the front

- **Food goods fold into the total** — every `Edible` good (forage, fish, meat, wheat, barley, bread). They are in the food
  figure's tooltip and in `more ▾`. *(Call 4.)*
- **Every other good shows while the stores hold any of it** — or while the village is short of it by the sim's own
  reckoning (the amber `ShowShortfall` already gives firewood, logs, tools and handcarts), or while its met stock limit
  shows the ⚠ (D409). ⛔ **A shortage never hides**: firewood at 0 in a village that wants firewood stays on the front, amber.
- **`more ▾` keeps every good the village knows, zeros included** (D502's rule for what it does not know is unchanged).

## 7. The dev bar

A small floating sheet holding **20×**, **Skip 1y**, **Skip 10y** and the **frame readout** (the fps line under today's
top bar). **Built only when `OS.IsDebugBuild()`**, the gate `AddTheSkipControls` already uses (D254) — a release build
has no dev bar, no 20× and no key 5. Draggable, its place saved like any window's (D504); not on the windows' column and not
in Settings.

## 8. The tool bar

### 8.1 What it holds

One `band` sheet at the bottom centre, one row of buttons, **each an icon over its word** (`build-bar.md §2`: an
icon-only strip is a memory test):

- **The categories in today's order** — Works, Food, Resources, Storage & trade, Knowledge, Civic, Homes, Other — each shown
  only when something in it is earned (`AnythingEarnedIn`, today's grey-out rule made a hide, A210). **All goes**: with
  flyouts it would be the whole catalogue in one row, the flat list this replaces. A category's icon is its first building's
  painted icon *(call 9)*.
- **The tools:** **Paint land**, **Move**, **Remove ▴** (Demolish, Take back, Empty, Destroy) and **Harvest ▴** (Trees, Stone,
  Iron, All, Unmark). **The BUILD / REMOVAL / HARVEST tabs go**: BUILD is the categories, the other two are the tools'
  flyouts. Their notes (*"taking a building back is a builder's job…"*) become the flyouts' tooltips.
- **Homes' flyout** holds the well, the *Build homes as* choices (Cabins …, D721) and *Rebuild older homes*. Paint land is a
  tool, not a building, so it sits with the tools.

**Gone from the bar:** the speeds (§6), Skip (§7), *Centre on village* and *Settings* (the windows' column, §9.3), Cancel (§8.4),
the *Show:* row and the hotkey line (§8.4).

### 8.2 Flyouts

- **A click opens one** above its button, touching the bar; the button is held in lapis. It closes on picking from it,
  clicking the button again, Esc, right-click, or clicking the map. One open at a time. **Never on hover** (§1.2).
- A flyout is its own floater, **not part of the bar's height** — so the bar is one height in every state and its pin is
  simple (§12).
- It wraps to a second row before it leaves the screen (`HFlowContainer`, D242), and sits inside the window however near
  the edge its button is.

### 8.3 New, until clicked (A210)

When a category or a building becomes earned, it is marked **new**: its word in gold ink (`#553e0c`, the palette's gold
word) with a **gold-leaf swash** under it (`#c4983e`). A category clears when its flyout is opened, a building when it is
picked. *(Call 5.)*

- **Not the owed medallion.** The medallion on a building's icon (A106; D103/D252) means *a free one is waiting* and keeps
  its meaning; *new* is a different fact.
- **Remembered per village, in the save, never hashed** — what the player has looked at is not a fact about the village
  (D335's line for derived state). A village loaded from an older save treats everything already earned as seen, so a load
  never lights the whole bar.
- ✅ **Painted by the art lane (A216)**, in `textures/ui/controls/`, not wired: `underline-lapis.png` and `underline-gold.png`
  (the swash of `ui-assets.md §3`, one stroke in three inks) and `checkbox-ticked-lapis.png` / `-lapis-disabled.png` (the
  seal in lapis wax, §10), and `pin-pinned-lapis.png` (a pinned card's pin, A217). **New names, not repaints of the red files**, so the game does not go half blue before the slice:
  step 1 points `ParchmentLook` at them, and gives a card's pin the painted pins (today it is still the `⌖` glyph,
  `Main.Cards.cs:413`, pressed in the theme's rubric — no painted pin is worn anywhere yet), and may then delete `checkbox-ticked*.png`,
  `pin-pinned.png` and `underline.png` if nothing else wears them. ✅ **Wired at step 1 (D874)**, and `ui colours:` checks each of the five loads at its master size with mipmaps.
  ⚠️ The main checkout had never imported them — the probe read *"does not load"* until `--import` ran (HANDOFF's trap).

### 8.4 Cancel, the brush shape and the hotkeys

- **Cancel becomes Esc and right-click** (right-click already cancels building, demolish, move and empty; on a brush it
  takes back, D327). While a tool is in hand, **its options** sit beside the bar: the brush shape (*square · round*, still
  key B) for a brush, and an **Esc** button for every tool — so putting a tool down stays something the player can see.
- **The hotkey line goes into tooltips**: each button's tooltip ends with its key (*Pause — space*, *1× — 1*). The keys
  that have no button (WASD, the wheel, R, Tab, Home, C, H) go into a new *Keys* fold in Settings, one line each.

## 9. Windows

### 9.1 What opens on a new village

**Open:** The valley, Village log, Professions. **Closed:** the roster, Stock limits, Build queue, Settings; Lore as today
(it opens itself at the first unlock); What's here as today (on a selection). The proposal's *nothing open* is refused at
Joe's word: the log is where the game's stories are told, and Professions answers a new village's first question (D484).

⚠️ **A saved `"shown"` beats a default** (D504), and every settings file written since D504 holds the roster shown —
Joe's included. *(Call 6: recommended, the settings file's move to version 2 (§11) resets every window's shown state to
these defaults once, keeping places and folds.)*

### 9.2 Professions, kept on screen (call 1)

Recommended **B**: the window as today, top left under the strip, with **a row for each trade that has a workplace
standing or being built, or anyone working it** — a zero row only means something once there is a seat. With none, it is
*Able adults · Labourers* and one line, *"A trade shows here once its building is raised or being built."*
(`hud_at_rest.png`). Its 360 px scroll (D842) is unchanged. **A** is today's table, eight rows at 0 / 0; **C** is the window
folded to its title by default.

### 9.3 The windows' column (A215)

The bottom-left corner, one `sheet` of words (Metamorphous), **one a line**: **Centre, People, Professions, Stores,
Build queue, Lore, Log, Valley, Settings**, plus *Records* once the hall stands. About 137 × 262 logical px.

⚠️ **Why a column, not a row** (Joe, A215, after the first mocks put the row under the tool bar's left end): side by side
the row (643 logical px) and the bar need 1,351 logical px on Day 1 and 1,528 with every category learned — wider than a
16:10 window (1,280) always and a 16:9 one (1,422) once the village has learned enough. The column takes the same map area
as the row and only the corner, so the bar centres freely: with every category learned it starts 67 px clear of the
column at 1,280 wide and 138 px at 16:9. The `hud overlap:` guard (§14) holds that.

Each word shows or hides its window. Each shows or hides its window; a shown one is held in
lapis. **It replaces Settings' window ticks** (D306) — Settings keeps display and game settings only. *Centre* is today's
*Centre on village*. Lore and Records appear when earned, as their buttons do today. *(Call 8: People for "The village",
Stores for "Stock limits".)*

### 9.4 The log

Unchanged: right column, under the minimap, its list 190 logical px, its seven filters. Their ticked seals go lapis with
§10; their words keep their categories' colours (*Deaths* stays rubric). *(Call 2 asks only whether it should sit
elsewhere.)*

## 10. Red, lapis and gold

| Ink | Means | Where |
|---|---|---|
| **Rubric** `#7b2318` (words), `#9b2d1f` | **something is wrong**: a warning, a stop, a refusal, a death | `stopped`, `refused`, `log_death`, ⚠, `ShowShortfall`, the cards' stopped lines — unchanged |
| **Lapis** `#2a3f7b` (words), `#30488c` | **selected, held, on, chosen** | `held` (`ParchmentLook.cs:46`), a pinned card's pin (`pin-pinned-lapis.png`, A217), every pressed button's font and swash (277–290), `selected_edge` (69), `UiLook.Held` (`Main.cs:8782`), the ticked seal |
| **Gold** `#553e0c` (words), gold leaf `#c4983e` | **new** (§8.3), and owed (the medallion) | — |
| **Verdigris** `#264a21` | working, met | unchanged |

- Lapis words already pass 5 : 1 on the painted paper's darkest mottle (A117); the gold word ink is the palette's own.
- **The title and new-game screens keep their one rubric button** (*Continue*, *Found a new village*): a title page's
  rubric, not a state in play *(call 7)*. `sheet-selected.png`'s illuminated bar (gold, lapis, a rubric pinstripe) is
  decoration, unchanged.

## 11. UI size — today's 75 % is 100 %

- **Every drawn size stays what it is at Joe's setting today.** The dial's number changes: `Control.Scale` becomes
  `percent / 100 × 0.75` (one named constant), so the new 100 % draws exactly what 75 % draws now. Every logical
  measure (the 1280 × 800 layout, the probe's widths, the bar's pinned height) is unchanged by this step.
- **The dial: 75–150 %, step 5, default 100** (*call 6*) — today's 55–115 becomes 73–153; the ends round to 75 and 150,
  a hair larger and smaller. `ui-assets.md §1.1`'s table is relabelled (Joe at 1920 × 1080: 100 % ≈ 1.0 px a logical px).
- **The settings file moves to `"version": 2`.** A version-1 file's `"ui_scale_percent"` is read as `× 4 / 3`, rounded to
  the dial's step and clamped (75 → 100, 55 → 75, 115 → 150), and written back as version 2. ⚠️ `PlayerSettings` lives in
  `Kith.Sim` (`Config/PlayerSettings.cs`), so **this step runs the suite** and gets its own tests (a v1 75 reads as 100; a
  v2 100 reads as 100; the ends clamp).

## 12. Numbers in lining figures

**Amounts are set in Cardo** (lining figures; already in `fonts/`, the new-game screen's reading face): the strip's
figures, the dev bar, `more ▾`, Stock limits, Professions' counts, the build queue's columns, the food tooltip. The numbers
on the bar are their own labels today (`Amount()`), so the change is a font on those labels, not a merged font file.
**Figures inside a sentence** (a card's lines, the log) **stay in Grenze Gotisch** — read in their words they are not
mistaken *(call 7's second half)*.

## 13. ✅ Joe's calls — ALL AS RECOMMENDED (A215, 2026-11-23: *"agree on the 9 calls"*)

Kept as they were put to him; each recommendation below is now the spec.

1. **Professions "on screen somehow."** *Recommended:* it stays open top left, but shows only trades that have a building
   (or a building going up); on Day 1 that's just *Able adults 4 · Labourers 4* and a line saying trades appear as their
   buildings go up. *Or:* exactly as today (eight rows of 0 / 0); *or* folded to its title bar until you open it.
2. **Where the log sits.** *Recommended:* where it is now, right side under the minimap, same size — it's already where you
   look for news. *Or:* bottom left, wider and shorter, beside the windows' column.
3. **When the food figure turns red.** *Recommended:* when the food in store won't last until next spring if nothing more
   came in — in spring that's under four seasons, in autumn under two. *Or:* under one year, any time of year; *or*
   never (D410's choice, kept). It also ends the tooltip's sentence: *"…won't see the village through the winter."*
4. **The four food counters.** *Recommended:* the one food figure replaces forage, wheat, fish and meat on the strip; each
   kind is in the figure's hover and in `more ▾`. *Or:* the total plus each kind the stores hold beside it.
5. **What "newly unlocked" looks like.** *Recommended:* the word in gold with a gold swash under it until you click it — a
   category clears when you open it, a building when you pick it — and remembered in the save so a reload doesn't
   re-light it. *Or:* a small gold dot beside the word.
6. **The size dial and your settings file.** *Recommended:* the dial runs 75–150 % (your 75 % reads 100 %), and the first
   load of the new build resets which windows are open to the new defaults once — otherwise your saved settings would
   keep the roster open. Window places and folds are kept.
7. **Where red and the new numbers stop.** *Recommended:* red leaves every "selected" and "on" in play — the ticked seals
   repainted in blue wax — but the title screen keeps its one red button; and the Cardo numbers go on amounts (bars,
   tables, counts), not on numbers inside sentences.
8. **Window names on the new row.** *Recommended:* *People* for "The village" and *Stores* for "Stock limits" (shorter on
   the row; the window titles follow). *Or:* keep the old names.
9. **Category pictures.** *Recommended:* each category borrows its first building's icon for now (the builder's hut for
   Works, the forager's hut for Food…). *Or:* the art lane paints eight category icons first.

## 14. Build order — `slice/hud`, small commits, each with its probe line red-checked (D326)

Cut from `main` after `slice/library-borrowing` merges. The art lane's files (§8.3) merge into `main` first.

| Step | What | Probe / tests | Moves the suite? |
|---|---|---|---|
| 0 | Joe's calls answered here; the slice claimed | — | no |
| 1 | **Red and lapis** — `ParchmentLook` roles, the pressed fonts and swash, `UiLook.Held`, the lapis seal and swashes wired (A216–A217's five files) | `ui colours:` A216–A217's five files load at their master size with mipmaps; no pressed / held / ticked style is rubric; every warning role still is | no |
| | ✅ **Built (D874).** `held` lapis (`#2a3f7b`), `selected_edge` lapis (`#30488c`, new `ParchmentLook.LapisFill`), every pressed font and the held swash (`underline-lapis`), the ticked seal (`checkbox-ticked-lapis`), a held slider's grip; a card's pin wears the painted pushpin (`UiLook.NamedIcon`, slate keeps ⌖), its head lapis when pinned. The title screen's rubric kept (call 7). The red `underline.png`, `checkbox-ticked*.png`, `pin-pinned.png` are left unworn, not deleted — the art lane's files to delete. `ui colours:` red-checked: 6 mutants, 6 reds | | |
| 2 | **UI size** — `× 0.75`, the dial 75–150, settings v2 and its migration (and the windows' one-time reset, call 6) | `ui scale:` a v1 75 reads 100 and draws at 0.75; tests in `PlayerSettingsTests` | **yes** (`Kith.Sim`) |
| | ✅ **Built (D875).** `PlayerSettings` version 2: the dial 75–150, default 100, `DrawnPercentAtTheDialsHundred` 75 (the view's `DrawnScale`); a version-1 size ×4/3 (`FromVersionOne`), its windows' *shown* forgotten once, `MigratedFrom` set and the view writes it back at once. No version is this build's. 6 new tests (11 cases), 2 retuned; `ui scale:` red-checked 4 mutants / 4 reds, the tests 2 Sim mutants / 7 reds. No drawn size moved: every other probe line as step 1's | | |
| 3 | **Windows** — the defaults (§9.1), the windows' column (§9.3), Settings' ticks gone | `windows:` the valley, log and Professions shown, the roster, Stock limits, Build queue hidden; every titled window has a button in the column | no |
| | ✅ **Built (D876).** `Main.WindowsColumn.cs`: the column at the bottom left, one Metamorphous word a line in §9.3's order, a window's word a toggle held in lapis while it is shown (`ShellWindow.Toggle`, Settings' tick until now; `Refresh` keeps it reading `Wanted`, so a ✕ lets it go); Centre, Settings, Records and Lore left the control bar for it. The roster starts shut; **People** and **Stores** are the windows' titles now (call 8), `FormerTitles` keeping what *The village* and *Stock limits* remembered. ⚠️ Until step 6 centres the bar, the column stands on it (`PlaceTheWindowsColumn`). What's here has no word (a bare right-click opens it). The probe opens the roster for its own lines and puts back what it found. `windows:` rewritten; red-checked 6 mutants / 6 reds (one first scored zero — the pose had shut the roster before the line looked) | | |
| 4 | **The strip** — one row; time beside the date, PAUSED gone; the dev bar (§7) | `bars:` one row at `+12,345` everywhere, ends left of the right column; `dev bar:` present in a debug build with 20× and both skips, and the strip holds no 20× | no |
| | ✅ **Built (D877)**, one row made *one line, two when crowded* at Joe's word. `Main.Strip.cs`: one slip, its groups (name and time · goods · people · the nomads switch) in a flow whose width is one line's or the room to the right column (`FitTheStrip`); the season's painted icon, the name in Metamorphous, the date; Pause (reading *Paused*, held) and 1×–10×, the speed play resumes at held — Space and Pause resume at it; households beside the people; STOPPED alone left of the speed label. The dev bar (`BuildTheDevBar`): 20×, the skips and the frame readout, debug builds only, key 5 with it; titled *Dev* for its place, not a window. ⏭️ At step 4 the strip still holds the food goods and the zeros (three lines); *one line at the founding, two at most* is step 5's clause. `bars:` rewritten, `dev bar:` new; 7 mutants / 7 reds | | |
| 5 | **Hide zeros, the food figure, Professions' rows** (§6.1–6.2, §9.2) | `bars:` a good at 0 is hidden, at 1 shown, at 0 and short shown; `food:` equals `FoodTheVillageHolds()` and is unmoved by 500 food posed in a larder; `runway:` 7 seasons at the founding | no |
| | ✅ **Built (D878).** `Main.Food.cs`: the food figure first in the strip's goods (*1,200 food · lasts 7 seasons*), its tooltip as §6.1, the warnings' colour when it won't last to next spring (call 3). Forage, wheat, fish and meat off the front (call 4); every other good hidden at zero unless short (its amber) or stopped (its ⚠); `more ▾` lists every good, the front's too; children, students, elders shown when there are any, adults beside them, the unhoused when there are any. Professions: a row for a learned trade with a workplace standing or being built (sites are workplaces), cached on the workplaces' count and `BuildingGeneration`; with none, the table goes and the line shows. ⚠️ **A real founding is two lines at 1,280** (1,414 logical on one line against 1,336): the sim marks logs and firewood short at zero there and a shortage never hides — one line at 16:9 (Joe's 1920 × 1080). Its builder has a seat, so Day 1 shows the builder's row, not the line. The lines: `bars:` (one line at 1,422, two at most), `zeros:` (new — at the founding and twelve years in), `food:`, `runway:`, `trades:` rewritten; `panels:` asks the strip to stay in its room, no longer to keep one width. 8 mutants / 8 reds — two only after a second look (two arithmetic mutants masked each other; the `more ▾` clause read a list the stock limits share) | | |
| 6 | **The tool bar and flyouts** (§8.1–8.2, §8.4) — the tabs, the *Show:* row, Cancel and the hotkey line gone | `bar height:` **asserts the new pinned number** (not only the spread); `flyouts:` every category's and both tools' flyouts open inside the window; `categories:` Knowledge, Civic and Other hidden at the founding, Works and Food shown; **`hud overlap:` no two floaters meet** (the strip, the dev bar, the valley, the log, Professions, the column, the bar, an open flyout, tool options — a flyout touching its own bar is the one exception), posed with every category earned, at 1,280 × 800 and 1,422 × 800 — red-checked with the column laid out as the first mocks' row | no |
| | ✅ **Built (D879).** `Main.ToolBar.cs`: the band centred at the bottom, one row — the categories with anything earned (each its first building's icon over its word, call 9), Paint land, Move, Remove ▴, Harvest ▴; the flyout a floater of its own over its button, on the bar's top, kept inside the window and clear of the windows' column, wrapping before the window's edge; closed by a pick, its button, Esc (first, before a tool), a click off it or a right-click. Tool options (the brush's shape for a brush, Esc for every tool) to the bar's right while a tool is in hand. The tabs, the *Show:* row, Cancel and the hint line gone; Settings lists *Keys*. `ProbeTheControlBar` rewritten: `categories:`, `flyouts:`, `bar height:` **104** asserted in every state; `hud overlap:` new — 93 px between the bar and the column at 1,280, 189 at 1,422. 8 mutants / 8 reds, the column laid out as the first mocks' row among them | | |
| 7 | **New, until clicked** (§8.3), saved | `new:` a library posed learned lights Knowledge and the Library gold; opening clears the category, picking clears the building; a reload keeps it | **maybe** — if the seen set goes in the save's sim-side format, the save tests |
| | ✅ **Built (D880).** `Main.NewMarks.cs`: a category or building earned and not yet seen wears the gold word (`ParchmentLook`'s new `new` role, `GoldInk`) and the gold-leaf swash (`new_swash`, `underline-gold`); opening a category's flyout clears it, picking a building clears it; marked last in every relight, so the hand's tint cannot wipe it (the library's, the smithy's and the hall's per-frame owed tint leave a new mark alone unless owed). The seen set is the save's `"view"` section — the view's, after `"world"`, never hashed; `SaveOpened.View` hands it back (`Kith.Sim`'s one change, a test in `SaveLoadTests`); a new village and a save without it count everything earned as seen. `new:` new; 6 mutants / 6 reds (two only apart) | | |
| 8 | **Lining figures** (§12) | `numerals:` the strip's amounts and the Stock limits table are Cardo | no |
| | ✅ **Built (D881).** `UiLook.Figures`: the reading face (Cardo) on an amount — `Amount()` (the strip's goods, headcounts and food figure, `more ▾`), every Stock limits field, Professions' asked numbers, the build queue's materials and work columns, the dev bar's readout. The date, the runway, the log and the cards stay in Grenze Gotisch. ⚠️ A tooltip's face is the theme's, one for every tooltip, so the food figure's tooltip stays in the body face. The strip's width did not move (its cells are fixed). `numerals:` new, 65 amounts; 4 mutants / 4 reds (one only after the clause looked inside the professions' stepper) | | |
| 9 | **The founding line** — `SimWorld.Create` narrates what the stores hold (*"4 exiles arrive in 2 households with a cart: 1,200 food and 20 stone tools. Spring, Year 1."*); *"no food stored"* only when there is none | the suite; `Phase0SimTests.cs:463` pins the village of one's line — keep it if its world holds no food, re-pin it if it does | **yes** |
| — | **CLAUDE.md** — `bar height` 183 replaced by step 6's measured number, its history kept as the line keeps it now | — | no |
| | ✅ **Done with step 6 (D879): 104.** | | |

**Later, not in this slice:** a firewood runway (after Joe has lived with the food one; winter fuel per home is
`WinterFirewoodOf`); the minimap's layers button (the proposal's finding 10 — today's *On the map* ticks stay in Settings).

## 15. Refused or deferred from the proposal, and why

- **No window open by default** — refused, Joe (§9.1).
- **A one-line log ticker** — refused: the log is where the stories are told (`DESIGN.md §1.4`); a ticker drops all but the last line.
- **"4 unhoused" the loudest thing on screen** — refused, Joe: it is a calm count until there is nowhere to build (D568/D571).
- **People and Professions merged as tabs; Stores merging `more ▾` with Stock limits** — not taken now: each is a bigger
  move than the screen it saves. Revisit after Joe has played this.
- **Skip 1y / 10y as player features** — they are debug tools (D254) and go to the dev bar.

## 16. Definition of done

1. Every step of §14 built with its probe line, each guard red-checked and its reds counted (D326).
2. At rest on a new village at 2560 × 1440 and 100 %, the HUD covers no more than 16 % of the screen, measured as here.
3. The four checks, every step; the suite where §14 says it moves.
4. This spec's status line true; `build-bar.md`, `ui-assets.md §1.1`, `settings-persistence.md` and `CLAUDE.md`'s bar height
   updated in the same commits.
5. Joe plays it.

# Spec: The cards — one building or person, five parts, and nothing else

**Decisions:** D376 (this document), D377, D378, D379, D380. Neighbours: D80, D104, D113, D147, D169, D311, D350, D367, D372.
**Status:** ✅ **Slice 1 BUILT (2026-09-15, D376), the controls folded onto the card the same day (D377), and slice 2 — the two top bars — BUILT the same day (D378): the Overview panel is gone; Joe's four notes on the lot are D379, his next four D380.** D380 unplayed. Owner: Joe + Claude Code.

---

## 1. Why this exists

Joe, 2026-09-12, with a Foundation screenshot: *"presently our inspector windows/details windows
are such a mess of stacked sentences i dont even know what to read. Remove text where you can."*
His answers, given the same day and confirmed on the mockup (2026-09-15):

- **A card per building**: title, one status line (*Working fine* / the one reason it is not), a
  workers row with **−** and **+**, two or three numbers, a picture — *"and every extra line is a bug."*
- **Several open at once, each pinnable and draggable**: click a building → its card; the pin keeps
  it when you click elsewhere; an unpinned card is replaced by the next click.
- On the mockup: *(1)* the picture is **the building's real map drawing scaled up**, and the
  building can be **renamed** to whatever the player types; *(2)* a home's card **lists its people**,
  scrolling past four; *(3)* a **person's card is the same shape**.
- The resources panel becomes a **top bar in two rows**, villagers a top bar beside it (slice 2).

⚠️ **The insides stay ours.** Foundation's card is a good *shape* for showing the derived, legible
causes this game runs on (§1.1 legibility); it is not a reason to change what is shown.

## 2. The shape — `Main.Cards.cs`

A card is a floating `PanelContainer`, **268 logical pixels wide and never wider** (D367's rule;
the title clips with an ellipsis, the status wraps), built once and rewritten every refresh:

| Part | What it says | Where it comes from |
|---|---|---|
| **Title** | the name; ✎ (**a building's card only** — not a person's, not a home's, D380) opens a `LineEdit` in place, Enter or focus-out commits; ⌖ pins; ✕ closes | `Name` (§3) |
| **Status** | one sentence; a green light for *working*, amber and the sentence for the one reason it is not | `SimWorld.IdleNote` (workplace); full / emptying / *N of cap used* (store); the larder's share against `restock_emergency_percent` and cold (home); `Villager.WorkNote` else `DescribeState` (person) |
| **Workers** | *N / seats* with − and + — the same number the Professions panel edits from the other end (D109) | `SimWorld.SetStaffing` |
| **Three numbers** | workplace: held / capacity, tiles of ground, seats · site: logs, work, sites queued · home: food/target, firewood/target, people · person: age, trade, household. ⛔ **A store has no three numbers since D393** — it has the storage list below | — |
| **Storage list** (stores, D393) | one row per good the store can hold, in catalogue order: chip · name · amount (*—* and dimmed when none) · **✓/✕ take toggle on the row** (`PlayerAllows`, D389). Every row adds up to the status line's *N of M used*. Joe, at a warehouse reading *714 used* over three numbers summing to 660 — the iron and tools were the missing 54: *"add a new line for each item that can go in a warehouse/granary/etc."*, with Foundation's warehouse card as the model. The *Takes:* row under Settings went with it — a good is described in one place (D139) | `StoreBuilding.CanEverHold`, `PlayerAllows`, `ToggleSelectedAccepts` |
| **People** (homes) | *Name, age — trade* per living member, four rows tall, scrolling past four | `Household.MemberIds` |
| **Picture** | `BuildingPortrait`: the footprint quad in the map's own colour, turned as it is turned, with its ring when the map would draw one; a caption — the workers, or the reason for the job | `VillageMap.FootprintQuadAt`, `VillageMap.ColourOf` |

**Drawn at the UI scale, like every panel** (D379 — Joe: *"the scale is larger than the rest of the
UI panels"*; a card is added straight to the scene and was never among the floaters `FitFloaters`
scales). The grip shows the move cursor, as the panels' grips do. **While a rename box has focus the
camera holds still** — the WASD pan is polled every frame, not read from key events, so
`VillageMap._Process` asks whether a `LineEdit` owns the focus before it moves (Joe: *"typing wsad as
part of a building name moves the game camera too"*).

**Open, pin, replace.** `OpenCard(subject)`: an existing card for the subject is selected; else
the one unpinned card is retargeted; else a new card is made beside the left column, stepped down
per open card. The selected card has the yellow edge. A card whose subject is gone (a demolished
building, a death) closes itself on refresh.

**The controls are on the card, under `Settings ▸`** (D377 — Joe, at a stockpile with its card
and the docked panel both open: *"shouldn't all of this be in the same panel? why 2 panels for one
structure?"*). Folded by default so the five parts stay what you read; open, it holds every
control the docked panel used to: a store's *Stocking: Open / Closed / Emptying* (D389 — one
three-state control; Emptying turns itself back to Open when the last armful leaves), *When full: Marker* and (a market's) *Keeps
up to:* (*Takes:* moved onto the storage rows, D393); a workplace's idle marker, ground brush and felling mode, a site's build queue; a
villager's *Kept on:* and the trades they have learned. Every row wraps (`HFlowContainer`), so the
card stays 268 wide with all of them open. Each control selects its card first and then calls the
same "selected" handler the docked panel called — one rule, not two. **The docked panel is
*What's here*, for what has no card yet** (bare ground, the library, the town hall) and hides the
moment the selection has a card. Its ✕ clears the selection — it is about what you clicked, like a
card — and it returns on the next bare-ground click; every other panel's ✕ does what unticking it in
Settings does, and the tick reads the window's state every frame (D380). ✅ **Since D390 (Joe's play
notes, 2026-09-18): a bare right-click opens *What's here* for the tile under the point** (resolved
as a left-click resolves it, so a building opens its card), a second right-click on the same tile
closes it, and **Esc closes it** when nothing is in hand (a tool in hand goes down first — D327's
cancel keeps its place); the brush's right-click take-back and a tool's right-click cancel are
unchanged. And **a heap on the tile is listed**: *"On the ground: 40 logs — still to be carried in."*

⛔ **Not in the card:** a fourth number, a second sentence, a control that belongs to the Settings
panel. If a card wants more, the building is asking for a second card, not a longer one. ⚠️ **A
store's storage list is not a fourth number** — it *replaces* the three (D393, Joe's call): a
store's whole job is what it holds, and three of five was a card that lied.

## 3. Renaming — `SimWorld.Rename`

⚠️ **The view offers ✎ on a store's and a workplace's card only** (D380, Joe: *"only buildings should be
renamable (excluding homes)"*); the sim's household rename below stays as machinery, guarded and
hashed sparsely, offered by nothing. `Workplace`, `StoreBuilding` and `Household` carry `Name` (what the village calls it), `BornAs` (the
place name it was founded with) and `GivenName` (the player's, or null). `SimWorld.Rename(thing,
text)` trims, refuses past `NameLengthLimit` (40) with the reason, hands the born name back on a
blank, and logs *"X is called Y now"*. **Hashed sparsely**: a given name mixes its length and every
character; a village where nobody renamed anything hashes as it did before renaming existed
(`MixGivenName`). Guards: `RenameTests`.

## 4. How it is tested

- **Probe `cards:`** — a card of each of the four kinds opens at the founding; all 268 wide; an
  unpinned card is replaced by the next click; a pinned one stays; a card stays where it is put; a
  40-letter name or a three-line status does not widen a card's minimum; **with every Settings
  fold open and its rows at their longest the minimum is still 268; a store shows one panel, bare
  ground reads in *What's here*** (D377). Red-checked: the replace rule off (*"a second click left
  2 cards open"*), the title clip off (*"widens a card's minimum to 604"*), the rows as an `HBox`
  (*"with its settings open a card's minimum is 609"*), the hide rule off (*"a store showed two
  panels"*).
- **`RenameTests`** — given, blank, too long; the hash sees a given name and not a born one.
- A windowed `BCLONE_SHOT` of four cards (handoff trap 39) looked at once; the shot hook is not in the tree.

## 4a. Where the food is — one number on the bar, three places in the popup (D394)

Joe, playing D391 at year 37: *"Help me understand where all of the food is? There are 5 homes,
the largest of which has 475 food. Is it in the hunter's lodge? … all of these different versions
of the 'truth' of how much food there is can be very confusing."* The bar read **245** (the
shelves alone), the popup *in homes and huts +4,336* (larders and a 2,103-meat lodge lumped), a
farm's card *"it has 3092"* (`FoodTheVillageHolds`: shelves + huts, what the limit reads).
Three definitions on one screen.

**The rule: the number on the bar is the number the rules read.** The bar's *food* is
`FoodTheVillageHolds` — the shelves (granaries and markets) and the huts (producers' buffers,
armfuls in transit) — the same number the limit, the farm, the hunts and the birth gate read.
⚠️ The bar's own comment had said so since D378; the code read the shelves. **The larders are
outside it** and stay outside it — Joe: *"I don't want what is in the home larders to count
against the limit. The limit should be what is in storage, in transit to storage and in markets
— what is available to villagers outside of their home storage."* That was already the sim's
rule (D161, D362); now it is the screen's.

The `more ▾` popup names the three places, always present: **food on the shelves · waiting in
the huts · in the larders** (`FoodInGranaries`, `FoodWaitingInHuts`, `FoodInLarders` — the
first two partition the village's food, the third is shown and never decided on). A quoted total
says where it is: *"you asked the village to keep 2000 food and it has 3092 — 245 on the shelves
and 2,847 still in the huts."* And a producer's card whose buffer is half full or more reads
amber: *"Working — 2,103 waiting to be carried in, 53 armfuls"* (`BufferIsSwollen`,
`ArmfulsWaitingIn`) — a hunt brings 900 in fifteen ticks and a carry takes forty, so a lodge fills
twenty times faster than it empties, and until D394 nothing on screen said so. Guard:
`TheVillagesFoodIsTheShelvesAndTheHutsAndTheSentenceSaysWhere`.

## 5. Slice 2 — the two top bars, and the Overview panel goes (✅ built, D378)

The Overview — a dozen rows in a window at the top-left — is **two boxes on one bar** along the
map's top edge, where the eye already is, and everything it held that is not a number the player
glances at went to Settings. `Main.cs`, `BuildTopBars`.

| Box | What it says | Where it comes from |
|---|---|---|
| **Resources** | two rows: **food** *(the umbrella, first)*, produce, wheat, fish, meat / logs, firewood, stone, tools — a chip, a number, a name; then `more ▾` | `FoodInGranaries` for food; `InStores(g)` per good; `BarRows` fixes the eight in Joe's order, **every other catalogue good is behind `more`** in catalogue order (iron, leather today; a modded good lands there the day it exists — D210's rule kept) |
| **`more ▾`** | a popup: the leftover goods, then the two permanent rows *in homes and huts* `+N`/`—` and *on the ground* `+N`/`—` (its tooltip the per-good reason, D134) | `TotalFood − FoodInGranaries`; `OnTheGround(g)` summed, `WhyItIsOnTheGround` |
| **Villagers** | `N villagers · N adults · N children · N elders · N laborers` (dots in the map's own villager colours) over `Fernhollow · Day 3, Summer, Year 17 · 2 households` | `Population`, the life-stage count, `Laborers`, `Name`, `Clock`, `LivingHouseholds` |

**Amber on a number = the village is short of it, by the sim's own reckoning** — the predicates that
staff the trades, so the bar and the Professions panel cannot disagree: food while
`TheVillageWantsMoreFood()`, firewood while `LabourQuota.WoodcuttersWanted > 0`, logs while
`LabourQuota.ForestersWanted > 0`; the tooltip says the number behind it. Nothing else has a demand
function today, so nothing else goes amber — ⛔ a stock limit is not one: a limit is a ceiling, and
holding less than a ceiling is not a shortage. A founding village opens with food and logs amber
and firewood not (no home yet, so the woodcutter quota wants nothing), which is honest.

**Rules that bind it.** The bar has no title — not foldable, not draggable, not in Settings'
window list, exactly like the control bar; `h` still hides it. **Fixed width and height by
construction:** every number is an `Amount()` cell (D367; 48 px for a good, 34 for a headcount),
the names are static, and `more ▾` is a popup rather than a fold — a fold would grow the bar and
push the roster under it (measured: 58 → 140 logical px tall with the rows laid on the bar). The
left column, a new card and the passing banner start below it (`TopOfTheLeftColumn`); the right
column does not move — the minimap stays top-right. The tick left the bar for the seed line in
Settings (*About this run*: build · seed · tick · config · log), where the *Not here yet* roadmap
fold went too.

⚠️ **Measured at the 1280-logical layout:** the bar is 1069×58 logical, ends at 816 px at the
default 75 % scale against a right column beginning at 966 — it clears until about 89 %; past
that it runs under the minimap, and the minimap is the one that is draggable.

**How it is tested.** The probe's `panels:` line prints the bar (`panel top`) on both passes and
⛔ if its minimum moved between the founding and twelve years in; the **`bars:`** line poses every
cell — the eighteen on the bar and in the popup — at `+12,345` and the clock line at a four-digit
year with two-digit households, and ⛔ if the bar or the popup moved, or if the bar's end crosses the
right column. Red-checked: one cell built without the trio → *"⛔ posing every cell at +12,345 moves
the bar 791x58 → 1149x58"* and `panels:` red on the second pass. **One zero, recorded:** the rows on
the bar instead of the popup scores nothing on either line, because `Amount()` cells are stable
whatever they sit in — that check is the 58 → 140 measurement above, not a guard. Two windowed
`BCLONE_SHOT`s (handoff trap 39) looked at: the founding with `more ▾` open, and a card twelve
years in; the hook is not in the tree.

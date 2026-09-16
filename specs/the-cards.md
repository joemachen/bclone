# Spec: The cards — one building or person, five parts, and nothing else

**Decisions:** D376 (this document). Neighbours: D80, D104, D113, D147, D169, D311, D350, D367, D372.
**Status:** ✅ **Slice 1 BUILT (2026-09-15, D376) and the controls folded onto the card the same day (D377): one structure, one panel.** ⏸️ Slice 2 (the two top bars) not started. Owner: Joe + Claude Code.

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
| **Title** | the name; ✎ opens a `LineEdit` in place, Enter or focus-out commits; ⌖ pins; ✕ closes | `Name` (§3) |
| **Status** | one sentence; a green light for *working*, amber and the sentence for the one reason it is not | `SimWorld.IdleNote` (workplace); full / emptying / *N of cap used* (store); the larder's share against `restock_emergency_percent` and cold (home); `Villager.WorkNote` else `DescribeState` (person) |
| **Workers** | *N / seats* with − and + — the same number the Professions panel edits from the other end (D109) | `SimWorld.SetStaffing` |
| **Three numbers** | store: the three biggest heaps · workplace: held / capacity, tiles of ground, seats · site: logs, work, sites queued · home: food/target, firewood/target, people · person: age, trade, household | — |
| **People** (homes) | *Name, age — trade* per living member, four rows tall, scrolling past four | `Household.MemberIds` |
| **Picture** | `BuildingPortrait`: the footprint quad in the map's own colour, turned as it is turned, with its ring when the map would draw one; a caption — the workers, or the reason for the job | `VillageMap.FootprintQuadAt`, `VillageMap.ColourOf` |

**Open, pin, replace.** `OpenCard(subject)`: an existing card for the subject is selected; else
the one unpinned card is retargeted; else a new card is made beside the left column, stepped down
per open card. The selected card has the yellow edge. A card whose subject is gone (a demolished
building, a death) closes itself on refresh.

**The controls are on the card, under `Settings ▸`** (D377 — Joe, at a stockpile with its card
and the docked panel both open: *"shouldn't all of this be in the same panel? why 2 panels for one
structure?"*). Folded by default so the five parts stay what you read; open, it holds every
control the docked panel used to: a store's *When full: Marker*, *Takes:* and (a market's) *Keeps
up to:*; a workplace's idle marker, ground brush and felling mode, a site's build queue; a
villager's *Kept on:* and the trades they have learned. Every row wraps (`HFlowContainer`), so the
card stays 268 wide with all of them open. Each control selects its card first and then calls the
same "selected" handler the docked panel called — one rule, not two. **The docked panel is
*What's here*, for what has no card yet** (bare ground, the library, the town hall) and hides the
moment the selection has a card.

⛔ **Not in the card:** a fourth number, a second sentence, a control that belongs to the Settings
panel. If a card wants more, the building is asking for a second card, not a longer one.

## 3. Renaming — `SimWorld.Rename`

`Workplace`, `StoreBuilding` and `Household` carry `Name` (what the village calls it), `BornAs` (the
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

## 5. Slice 2 — the two top bars (not started)

Resources in **two rows** — *food, produce, wheat, fish, meat* / *logs, firewood, stone, tools* —
with *more ▾* for iron, leather, *in homes and huts*, *on the ground*; amber on a number the
village is short of; **villagers beside it**: total, adults, children, elders, laborers, and the
clock. Then the Overview panel goes. The probe's `panels:` line measures the bars like any panel.

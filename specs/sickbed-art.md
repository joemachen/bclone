# W6's pictures, piece 1 — a person sick in bed, and the card's health pill (A309)

> **Status: ✍️ MOCKED (A310, 2026-11-27), ⏸️ FOUR CALLS JOE'S; NOTHING BUILT.** Claimed A309. Art lane (CLAUDE.md *Two tracks*),
> branch `art`. W6's ask of the lane (`health.md §11`, D774), piece 1 only: the herbalist, herbs, remedy and the infirmary
> (pieces 2 and 3) wait for their own specs. Mocks: `D:/Projects/brushes/sickbed/` (`sickbed_pick.jpg`; `compose.py` lays the
> beds on the real render, `harness/` paints the figure with the game's own `PersonPainter`, `mock_card.py` and `card.sh` render
> the real card from a scratch copy of the game).

## 1. What is there today, and what piece 1 brings

- **Nothing of health is in the sim yet.** W6 piece 1 (`health.md §5`, §9) adds *run down* and **sick in bed**: about once a
  year a run-down person goes home and lies there five days, eats, does no work, then gets up still run down; nobody dies of it.
  Its stand-ins (§11): the house roof (a person at home is not drawn — they fade at the door, `indoors-and-smoke.md §7`) and a
  lying figure from the dead-under-a-cloth pose without the cloth. **No lying figure exists in `PersonPainter`** yet.
- **The card** (`Main.Cards.cs`, the systems lane's): five need pills, *Fed · Warm · Housed · Water · Content*, each in one of two
  painted skins (`ParchmentLook`: `pill_met`, a green ribbon; `pill_unmet`, a red label). Piece 1 adds a sixth: **Well / Plain
  diet / Run down / Sick** (§5.4). With six the row wraps to a second line at the card's width.

## 2. The mock

On a copy of his honey-harrow Hollowmere (it opens on `main` since D933): Vorith the smith at his cabin, at the game's closest
zoom, the copy changed only so he is resting indoors; the figure painted by `PersonPainter` at the map's figure scale.

- **A — at home, seen through the roof.** Over the bed the roof is washed back in a soft oval (the floor's warm paper, a faint
  ring), and inside it the bed side-on, as people are drawn: a low wooden frame, a pillow, him lying on it under a blue wool
  cover with the sheet turned down. True to the sim (he is home), unchanged by snow, and **the bedside piece 2 needs** — the
  herbalist kneeling *at* a bed is this window with a second figure in it; the infirmary is the same window, several beds.
- **B — lying by the door.** A straw pallet on the ground beside the step, the same figure under the same cover. Plain and
  legible, with nothing over the roof; but it puts a sick man outdoors in every season (snow included), where the sim has him
  in his bed.
- **The pill, two ways, on his real card:** **1** as the other needs — *Well* green, the other three the painted red; **2** the
  same, but ***Plain diet* in an amber ribbon** (the red label's painting, hue-turned to amber): it does nothing yet (§5.3), and
  in red it would look like *Cold*, which harms. Red then means *it is biting*, amber *you have a year*.

## 3. Calls — ⏸️ Joe's

1. **The bed:** ⭐ **A** (through the roof) or B (by the door).
2. **The pill:** 1 (two looks) or ⭐ **2** (amber for *Plain diet*).
3. **His OK for the files outside the lane's table:** for the bed, `VillageMap.Indoors.cs` (the sick are indoors, but drawn),
   `VillageMap.cs` (the home painter's hook for the window, and the click test, so clicking the bed opens the card), and a new
   `VillageMap.Sickbed.cs`; `PersonPainter.cs` gains a lying pose. For pill 2: a new painted `pill-warn.png` and one theme line in
   `ParchmentLook.cs` with `UiLook.Pill`'s third look — **`Main.Cards.cs` is not touched by the art lane**: piece 1's own pill
   asks `UiLook` for the warning look on *Plain diet* (the seam, to be agreed with the systems lane before either side builds it).
4. **When:** the pill's amber skin can be built now (nothing waits on the sim); the bed is built **after W6 piece 1 merges**, on
   its *sick in bed* state — until then there is nobody to draw.

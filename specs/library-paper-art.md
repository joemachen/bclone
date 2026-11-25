# The paper in the library walker's hand (A245)

> **Status: 🔨 BUILT ON `art` (A246, 2026-11-24), his four calls *"All as recommended"*; ⏸️ HIS EYES, then the merge at his
> word.** *(Read "specced and mocked, nothing built" until A246.)* Art lane (CLAUDE.md *Two
> tracks*), branch `art`. View only: nothing in `src/Kith.Sim`, `data/` or `tests/` changes. Mock:
> `D:\Projects\brushes\library-borrowing\paper\paper_mock.jpg` — the game itself, run from a scratch copy on copies of Joe's
> Elderwood, so every figure on it is what the game would draw.

## 1. What was asked

Joe, via the coordinator, 2026-11-24: *"proceed with both in new sessions"*. The request is `library-borrowing.md §8` and
HANDOFF's ⏸️ *ART LANE — THE LIBRARY'S BORROWING (D868)*: **paper in hand** — a villager walking home from the library carrying
a letter or a few sheets (the cask's precedent, A203–A204); take the stand-in out when the figure carries its own.

## 2. What the map drew until A246

`VillageMap.DrawThePaper` (D868) lays a tilted sheet with two lines of ink on the map beside the walker — always at the left of
the picture, whichever way they walk, not in a hand, on top of everything. It does not move with the step, and its ink is wrong:
**the paper is blank on the walk** — the letter is written at their own door (`library-borrowing.md §5.1`).

## 3. Who carries it — measured, not assumed

`D:\Projects\brushes\library-borrowing\paper\measure\` stepped copies of eight of Joe's saves six years on (letters on):
3,150 person-ticks with paper in hand.

| | |
|---|---|
| with a load, a cart | **0** — fetched empty-handed, and home next |
| with a pail | **cannot be**: any state but the water's drops the pail (`CarryTheWater`) |
| in a cloak | 2,376 (75 %) — a hand the cloak covers must be drawn over it |
| elders | 1,240 (39 %) — they hold a stick, in the other hand |
| children | 0 (`CanWork`) |
| `TravelingHome` | 2,365 (75 %) |
| `AtTheLibrary`, standing at its door | 667 (21 %) — §6 |
| `SeekingShelter` (arms hugged in) | 70 (2 %) |
| fetching a coat, fetching from a store | 48 (2 %) |
| seasons | winter 945, fall 759, spring 727, summer 719 |

So it is seen most in a cloak, often on an elder, in every season, and from every side (the walk home goes any way).

## 4. The three looks mocked

Drawn by the painter in the figure, as the cask is: **blank sheets**, a little brighter than the linen (`#faf4e4`; the wash
takes a fill down ~15, art trap 88), inked like everything else, the same hand from behind, mirrored when they walk left.

- **A — sheets held to the chest**, one forearm across. Reads from the front and the side; **walking away nothing shows**, as
  the cask, so half the time you could not tell who is bringing paper. On a linen apron it is white on white.
- **B — a few sheets in the hand at their side** — two sheets, a little fanned, the hand on top. ⭐ *Recommended.* Seen from the
  front, the back and both sides, over the gown or the cloak; clear of an elder's stick; nearest to what the stand-in says now.
- **C — a roll tied with red thread** in the hand. Seen from every side, and the red is pretty up close, but at play size it
  reads as a candle or a rolling pin, and a tied roll says *letter already written*.

Common to all three: the hand with the paper does not swing with the step (the other does); hugging in against the cold, the
sheets are clutched to the chest under the arm.

## 5. Code (✅ as built, A246)

| File | Change | Lane |
|---|---|---|
| `PersonPainter.cs` | `FigureKey.Paper`; `Who.Paper`; the paper drawn after the load; the paper's hand held still | ⛔ call 2 |
| `VillageMap.People.cs` | `PersonKeyOf` takes `paper` and passes `villager.CarryingPaper` (two lines added, two changed, ~93, ~122, ~141); **out:** the stand-in's call (~321–326), `LetterPaper` and `DrawThePaper` (~365–395) | ⛔ call 2 |
| `VillageMap.PeopleProbe.cs` | the probe line hooked on (one line, art trap 73) | ⛔ call 2 |
| `VillageMap.PaperProbe.cs` (new) | the probe line (§8) | art |
| `VillageMap.Indoors.cs`, its probe | only if call 3 is yes (§6) | ⛔ call 3 |

**One thing the build added:** the sim hands the paper over as the walker reaches the library's tile, while the picture is still
gliding up to the door, so the key asks `ShowsPaper(carryingPaper, state)` — paper, and not `AtTheLibrary`. Nobody walks *in* with
it. With both arms full (never seen, §3) none is drawn.

A figure is painted once per `FigureKey` and kept, as now: the paper adds one more picture per pose for whoever carries it, let go
when unseen. No new texture, so no import. The dots (watercolour off) draw no paper today and still will not.

## 6. Found on the way: a paper-taker stands on the library's roof

`AtTheLibrary` (`library_sit_ticks` 2, about 2.7 s at 1×) is not among `VillageMap.Indoors`'s states, so whoever is taking paper
is drawn **standing on the doorstep, over the roof**, for the whole sit — where the school's, the tavern's and every store's
callers step in. A fifth of the paper's time on the map is this. One state added to `Indoors` would put them inside.

✅ **Built (A246):** `Indoors` takes `AtTheLibrary` as it takes the school; rendered on the same copy of his Elderwood, the taker is
no longer on the roof (`paper_built.jpg`).

## 7. Joe's calls — ✅ *"All as recommended"* (A246)

1. **The look: A, B or C.** ⭐ *B — a few sheets in the hand.*
2. **His OK for three files outside the lane's table** — `PersonPainter.cs`, `VillageMap.People.cs`, `VillageMap.PeopleProbe.cs`,
   the lines in §5. The systems lane's open slice (W4) is at its spec and has touched none of them. ⭐ *Yes.*
3. **Step inside the library to take paper**, as at the school and the tavern (one line in `VillageMap.Indoors.cs` and its probe
   case). Not asked for; §6. ⭐ *Yes.*
4. **Blank paper** — no ink lines, since the letter is written at home. ⭐ *Yes.*

## 8. Probe

A new line, `paper in hand:`, on `painted villagers:` (art trap 73): paper pixels in the hand on an adult, a cloaked adult and an
elder, front, back and both sides, both beats; none on the same figure without paper; the elder's stick still there; clutched
when cold; the walker's key carries `Paper` exactly when `CarryingPaper` outside the library; the stand-in gone. ✅ Built, with the
arm held still from step to step; `indoors and smoke:` asks the library. **Red-checked: 17 mutants, 17 caught** — 14 at first; the
three that swung the arm off the paper scored zero against a count of paper pixels (the sheet does not move with the arm), so the
guard compares the arm's own pixels now.

## 9. Not in this piece

The library's own drawing; the pail (a stand-in still, `water-need.md §4`); anything in the dots.

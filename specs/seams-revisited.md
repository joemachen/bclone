# Spec: Stone and iron seams, revisited — volume, positioning, frequency, size, shape

**Decisions:** D542 (the census and the questions), D543 (Joe's answers; built). **Status:** 🔨 **built 2026-10-10 on
`slice/seams-revisited` (D543), sim only; NOT played, NOT merged.** §4 is the design as built, §5 the measurement, §6
the guards and their red-checks.

---

## 1. Why this exists

Joe: *"revisit iron and stone node volume and positioning and frequency and size and shape."*

The seams were last reshaped by D475 (*"stone and iron nodes look planned and symmetrical"* → found, not placed)
and grown by D434/D436 for the quarry (more seams, every iron seam ≥ 50). Today's generator is
`MapGenerator.SeamsOf` (where and how big) and `PaintOutcrop` (the wobbling outline, grown until a seam holds its
minimum). The rules live in `data/sim.config.json` (`stone_seam_*`, `iron_seam_*`, `extra_*_seams`,
`seam_*_scatter_percent`, `iron_seam_min_iron`) and the goods rows (`yield_per_tile`: stone 12, iron 8).

**What volume means now** — worth saying before anyone tunes it: a **quarry or mine face never runs out**
(`quarry.md §3.4`). A seam's volume is only what **laborers dig by hand** before the village learns to quarry or
mine (200 stone, the smith's first iron tool), what the **smithy gift** reads (50 iron), and what is **cleared for
good** when the player paints the destroy or harvest brush over it. So "more volume" changes the early game, not the
quarry's output.

---

## 2. The census — what the generator makes today (64 shipped valleys, scratch `ZzSeamCensus`, deleted)

A *seam* below is a 4-connected run of one kind of tile, so a seam the river cut in two counts twice.

| | Stone | Iron |
|---|---|---|
| Seams a valley | median 12 (11–16) | median 4 (4–7) |
| **Across the river — unreachable** | **234 of 789 (30 %)** | **102 of 274 (37 %)** |
| Tiles a seam | median 11 (p10 5, p90 17, max 35) | median 12 (p10 7, p90 16, max 19) |
| Goods a seam | median 132 (12 a tile) | median 96 (8 a tile) |
| Goods a valley, all seams | median 1,668 (1,344–2,016) | median 384 (272–488) |
| Tiles from the founding, any seam | median 26 (6–43) | median 36 (21–51) |
| **Nearest seam** to the founding | median 14 (6–19) | median 30 (21–39) |
| Shape: tiles ÷ bounding box | median 76 % | median 75 % |
| Shape: long side ÷ short side | median 1.0, p90 1.5 | median 1.0, p90 1.33 |
| Tiny pieces (1–2 tiles) | 53 of 789 | 16 of 274 |

**Reading it:**
- **Frequency and volume are generous for stone, lean for iron**, as designed (*"stone near, iron far"*): a valley holds
  ~1,700 stone and ~380 iron before any quarry or mine.
- **A third of every kind is across the river.** Seams are placed on rings round the founding with no regard to the
  water, so the river takes about a third. They are visible, can never be reached, and since D540 a quarry beside
  them is refused.
- **Every seam is a round blob**: width ≈ height, three-quarters of its box filled. That is D475's wobbled outline on
  a round size. Nothing is long, thin, branching or ridge-like.
- **Tiny pieces** (1–2 tiles) are mostly seams the river cut, and a few blobs whose wobble pinched off a corner.

---

## 3. Joe's questions — what should each one be?

Each is a direction to pick before a spec is written. Nothing here is recommended yet beyond what the census shows.

1. **Volume** — more stone or iron in a seam (a bigger `yield_per_tile`, or bigger seams), less, or as is? Remember it
   only matters before the quarry and the mine, and for the smithy's 50.
2. **Positioning** — keep *stone near, iron far* (nearest stone ~14 tiles, nearest iron ~30)? And **should seams avoid
   the far bank** (or at least the generator count only reachable ones), so a third of the ore is not out of reach?
3. **Frequency** — about 12 stone and 4 iron seams a valley: more, fewer, or more variety between valleys (a
   stone-poor valley, an iron-rich one)?
4. **Size** — most seams are 6–20 tiles. Fewer, bigger seams? A wider spread from small to large?
5. **Shape** — today every seam is a rounded blob. What should they look like: **long veins or ridges** (a stripe a
   tile or two wide running across the ground), **clusters** of small outcrops, **irregular** blobs with arms, or a
   mix by kind (stone as broad outcrops, iron as narrow veins)?

## 3a. Joe's answers (2026-10-10, D543)

1. **Volume: as is** — stone 12 and iron 8 a tile; a typical seam holds what it did. (A valley's total roughly doubles,
   because it holds about twice the seams.)
2. **Positioning: stone as is; iron a little closer** to the founding.
3. **Frequency: "more variety, more frequency across the whole valley"** — about **2×** on average, valleys differing.
4. **Size: "wider range".**
5. **Shape: "all of the above, more variety".**
6. **Seams across the river: keep them** — *"we'll eventually add bridges to the game."*
7. **Only the near iron seams must hold 50**; scattered ones may be small.

⚠️ **Whatever changes, the valley must stay livable**, and the guards that say so are already written:
`quarry.md §6.1` (three reachable stone seams in every valley), `SeamsTests`, every iron seam ≥ 50, and D420's harness
for survival. Moving the generator reshuffles every valley, so the goldens move once, in its own commit.

---

## 4. The design, as built (D543)

All in `MapGenerator` (`SeamsOf`, `ScatteredSeamsOf`, `PaintSeamAt`), every draw on the existing StoneSeams / IronSeams
stage streams (D473): water is unchanged in every valley, and the woodland draws as before. Integer throughout.

- **Near seams** — `SeamsOf`, the rings, keep every guarantee they had: stone 4 at 14 + 8 at 21; iron 4 at
  **`iron_seam_ring_tiles` 22** (was 26). Each draws a **shape and heading with its position**, so a ring's seams are the
  same whatever rings come after (drawn after all of them, the inner four changed shape when the outer eight were
  added, and `TheQuarrysSeamsMovedNoForest` caught it). Near shapes are solid only: stone a **blob or arms**, iron a
  **blob** (arms grown to 50 iron ran past what a player reads as one seam; a wide river left one holding 24 within
  eight tiles of its centre). Near iron still grows until it holds `iron_seam_min_iron` 50.
- **Valley-wide seams** — `ScatteredSeamsOf`, drawn after the rings on the same stage. One candidate per cell of a
  **`seam_scatter_cell_tiles` 12** grid over the whole map (a partial shuffle picks the cells, a jitter places the seam
  inside one), so they spread and reach the far bank. **How many**: `scattered_stone_seams` 12 / `scattered_iron_seams`
  4, swung ±**`seam_count_variety_percent` 50** by one draw per kind per valley. **Kept off the founding**: stone
  `stone_seam_clear_of_founding_tiles` 10, iron `iron_seam_clear_of_founding_tiles` **18** (at 10 one valley had iron 7
  tiles out — the doorstep, not "a little closer"). **Size**: `scattered_seam_tiles_min` 1 to `…_max` 40, a uniform
  draw **squared**, so pebbles are common and big outcrops rare. No iron minimum.
- **Shapes** — `stone_seam_shapes` `{ blob 40, vein 15, cluster 25, arms 20 }`, `iron_seam_shapes`
  `{ blob 25, vein 40, cluster 15, arms 20 }`: a **blob** (D475's wobbling outline), a **vein** (a 1–2-wide stripe along
  a heading, bending a tile now and then by hash), a **cluster** (3–5 small blobs round the centre), **arms** (a small
  blob and 2–3 one-wide veins). Every part paints open grass only and each tile is counted once (`Ground.Held`).
  Where a cluster's parts or an arm's heading fall is **hashed** from the centre, never drawn.
- **The new-game rows** scale the valley-wide counts too: stone *sparse / moderate / rich* 6 / 12 / 20, iron 1 / 4 / 7.

## 5. Measured (64 shipped valleys for the shape of things; D420's 100 for survival)

| | Before | After |
|---|---|---|
| Stone seams a valley | 12 (11–16) | **18–30, mean 23.4** |
| Iron seams a valley | 4 (4–7) | **5–10, mean 7.9** |
| Valley-wide seams beyond 30 tiles | — | stone 549 of 727, iron 197 of 250; every quarter 15 %+ |
| Valley-wide seam size | — | **1–44 tiles**, p10 2, median 9, p90 31 |
| Shapes (stone / iron, all seams) | blobs only | blob 781 / 322, vein 121 / 98, cluster 174 / 35, arms 419 / 51 |
| Elongation, (longest span)² ÷ tiles ×100 | — | vein median **803**, blob median 119 |
| Nearest iron to the founding | median 30 | **median 24**, 14–31 |
| Woodland the valley-wide seams take | — | 0.5–1.4 % (four seeds) |
| Shipped terrain (seeds 12345 / 2 / 42) | stone 134 / 127 / 121, iron 40 / 59 / 45 | stone 214 / 231 / 185, iron 88 / 69 / 69; water identical |
| **Survival, 100 valleys × 50 years** (back to back) | alive 620, dead 19, lost foundings 13 | **alive 612, dead 21, lost foundings 13** |

## 6. Guards (`SeamVarietyTests`, 9; `SeamsTests` re-posed) and red-checks

New: twice the seams and a spread between valleys; the valley-wide seams reach the whole valley; none within the
founding's clearance (iron's further than stone's); the nearest iron 21–26 tiles (median) and never under 10; sizes
from pebbles to outcrops; every shape for each kind (near seams only the solid ones); a vein long and a blob round;
the valley-wide seams moved no forest out of place.

Re-posed: `TheSeamsMoveNoOtherStagesDraws` and `TheQuarrysSeamsMovedNoForest` switch the valley-wide seams off (drawn after
the rings, they move when the rings do); the latter varies **stone extras only** — since D475 going from two iron seams
to four re-spaces the whole ring, so a vacated iron tile may grow trees, which the four seeds never happened to show
until the ring came in to 22.

**Red-checks: 11 mutants, 20 reds, no zeros** — no valley-wide seams (5); no richness swing (1); the clearance ignored
after the jitter (1); one size for every seam (2); every seam a blob (2); a vein painted as a disc (1); near iron with
arms (2); iron ring back to 26 (1); iron as near as stone (2); and the two house guards below (2, 1).
`TheScatteredSeamsMovedNoForest` (a draw-order guard) was not red-checked.

**What else the reshuffle moved** (each with its reason in the test):
- **Ten goldens** — the map hash, three terrain prints and counts, two farm, two skill, two stock-limit — and the new-game
  stats' counts.
- **Seed 7's fixture valley now freezes its founders in Year 1**: the warm start's forester was given 72 wooded tiles all
  across the river (`SimWorld.GiveItTheWoodAroundIt` never asks whether the wood is reachable — D110's mistake, latent,
  fixture-only: a real game starts cold). `ALogLimitAbove…` and `NoStepEverCrossesAWall` moved to seed 11; **the bug is on
  file, not fixed here.**
- **Two house-facing guards** (`HousesFaceThePathInFrontOfThem`, `AHouseWithNoPathNearFacesTheVillage`) had passed with the
  bare square lying about due west of the village; reshuffled, it lay south-east, the nearest house stood on the paint's
  corner, and a diagonal facing swings a house's second tile off the paint (`TilesClippedOff`). They now **state** the
  village centre due west of their square (`DueWestOf`) instead of finding it.

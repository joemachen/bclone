# Spec: Stone and iron seams, revisited — volume, positioning, frequency, size, shape

**Decision:** D542 (Joe, 2026-10-10). **Status:** ✍️ **a census and Joe's questions only, on `slice/seams-revisited`;
NOTHING DESIGNED OR BUILT.** The direction is Joe's to set (§3) before anything is specified.

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

⚠️ **Whatever changes, the valley must stay livable**, and the guards that say so are already written:
`quarry.md §6.1` (three reachable stone seams in every valley), `SeamsTests`, every iron seam ≥ 50, and D420's harness
for survival. Moving the generator reshuffles every valley, so the goldens move once, in its own commit.

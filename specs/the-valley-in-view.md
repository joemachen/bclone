# Spec: The valley in view — terrain that reads as a place, not as storage

**Decisions:** D332, D333, D334, D337. Rendering only; follows `gridless.md §10.2`.
**Status:** ✅ **REBUILT AS A FIELD (2026-09-10, D342).** D337's trees and traced shoreline are **superseded**: the valley is now baked into one texture as the level set of a continuous field, so the river is an organic meander with a shallow bank, the deposits are irregular bodies, and a wood's foliage is texture rather than ~5,740 `DrawCircle` a frame. **`DrawTheShoreline` is deleted.** ⚠️ **The forest CLUMPS are still Manhattan diamonds** — §4 always said the renderer could not hide that, and it cannot; it is `PaintForest` and it is Joe's. **1073 passing, 0 failing, 2 skipped of 1075 — no golden moved.** ⚠️ **Unplayed by Joe as of this line.**

---

## 1. Goal

**Joe:** *"why are forests grid-shaped? is that yet to come? more 'natural' treelines and
waterlines and shapes?"*

⛔ **The sim being tile-indexed is his own closed decision** (`gridless.md §10.2`, *"not to be
re-litigated"*) — terrain heightmaps and spatial partitioning are gridded in essentially every
gridless game. **What nobody had ever revisited is that the tiles are DRAWN as tiles.**

The audit that answered him found the surprise: ⛔⛔ **nothing draws a tree.** A wood is a flat
`ForestColour` rectangle, and `DrawTheWoods` draws only animals and berries. *That is most of why a
forest reads as a block.*

## 2. Which pillar it serves, and what it must not touch

- **§1.1 legibility.** A wood should look like a wood, so that *"why is my forester idle?"* is
  answered by looking at the map.
- ⛔ **Nothing here is sim state.** No golden can move for it, and if one does that is a defect.
- ⛔ **No second source of truth.** Every mark is derived from `TerrainAt` at draw time and from a
  stateless hash of the tile's coordinates — **nothing is stored**, so nothing can drift out of step
  with the terrain the way a cached scatter would.

## 3. The two changes

### 3.1 Trees, and the reason they are the answer rather than smoothing

**Scattered at sub-tile positions, deterministic from `Scramble(x, y)`** — the same machinery that
already places animals and berry patches, which is *already* sub-tile and already stateless.

⭐⭐ **THE TREES OVERHANG THEIR TILE, AND THAT IS THE WHOLE TRICK.** A canopy near the edge of a
woodland tile spills onto the grass beside it, so **the treeline becomes ragged for the reason real
treelines are ragged** — not because a curve was fitted to it. *A smoothed outline would have made
the forest edge a different wrong shape; this makes it not a shape at all.*

- Saplings get fewer and smaller marks, so the forester's replanting is finally visible. ⚠️ D221
  gave saplings a colour and a sentence and noted they had neither before; **they still had no
  texture**, so a replanted acre looked like flat ground of a slightly different green.
- ⚠️ **Zoom-gated.** Below about ten pixels a tile a canopy is sub-pixel, and drawing thousands of
  them would be the per-frame full-map walk `Minimap`'s comment records this project being bitten by
  twice.

### 3.2 A shoreline the river can be seen to have

The water is drawn by the generic terrain loop — one flat square per tile, no bank, no edge. **The
contour tracer built in D332 already does exactly what is needed**, so the shoreline is that tracer
run over the water tiles and drawn as one smooth line.

⭐ **Cached on `SimWorld.TerrainGeneration`**, the counter `Minimap` already uses to decide when to
re-bake — terrain changes only when somebody clears ground, so the trace is paid then rather than
per frame.

## 4. What is deliberately NOT in this slice

- **Filling the smoothed contour.** A concave polygon with holes is a real problem and the *edge*
  carries most of the benefit. Named so it is not mistaken for an oversight.
- **Soil.** It re-quantises an already-smooth bilinear field (`MakeSoilRegional`) back into per-tile
  alpha squares and could be resampled at pixel resolution with zero sim change — but it is **off by
  default**, so it is the least visible thing on the list.
- ⛔⛔ **The forest clumps themselves — AND §4 CALLED THIS RIGHT A SLICE EARLY.** `PaintForest`
  drops Manhattan **diamonds** of tiles. This spec said *"smoothing the render cannot hide that"*, and
  **D342 proved it by trying**: the field's edge jitter can move a boundary about a tile, which on a
  nine-tile diamond is a nibble. ⭐ *The river, which is two tiles wide, is transformed by the same
  machinery — the difference is entirely the ratio of the jitter to the feature.* **It is a generator
  question, it moves every golden, and it is Joe's.**

## 5. How it is tested

The view has no test project (D11, D160), so the guards live in the width probe as D332's tracer
self-check does:

- Trees **overhang**: canopies reach past their own tile, measured by the **branches** rather than
  the trunk. `[widths] trees:` reports how many of the sampled canopies cross the line.
- The tree scatter is **deterministic — and that is enforced by a type rather than a check.**
  `CanopyOn` is a `static` taking a tile and an index, so it cannot reach the tick or the frame
  alpha at all. ⚠️ *The runtime guard that preceded it scored zero and is recorded in §6.*
- ⛔⛔ **THE SHORELINE HAD NO CHECK OF ITS OWN AND THAT REASONING WAS WRONG (D338).** This spec
  argued that the tracer's self-check already covered it — *"a second copy of the same assertion
  would only test the copy"* — and the argument was sound about **the tracer** and silent about
  **the seam.** The tracer was correct throughout; the call site misread its units and **drew the
  bank half a tile off the water.** ⭐ `ATracedOutlineLandsOnItsOwnRectangle` checks the thing that
  was actually untested: *a traced outline's bounding box must equal the rectangle the fill draws for
  the same cell*, posed at one cell per tile and at four. **Red-checked twice, two reds.**
- ⚠️ **Watch the frame cost.** The visible window is already walked six times a frame; trees are a
  seventh pass and the shoreline must not become one.

## 6. Definition of Done — ✅ MET (2026-09-09), except the last

| # | Item | State |
|---|---|---|
| 1 | A wood reads as a wood, its edge ragged rather than square | ✅ **8 of 17 sampled canopies overhang**, furthest 0.73 tiles |
| 2 | Saplings are visibly a young wood | ✅ fewer, smaller, lighter marks |
| 3 | The river has a bank | ✅ the D332 tracer over the water tiles |
| 4 | Zoom-gated, cached, no new per-frame full-map walk | ✅ trees above 10px/tile; the shore cached on `TerrainGeneration` |
| 5 | **No golden moves.** Probe green, bar height 161 | ✅ |
| 6 | Joe plays it | ✅ *"the trees look pretty cool"* — and the riverbank was half a tile off (D338) |

⛔⛔ **THE DETERMINISM GUARD SCORED ZERO AND WAS REPLACED BY A TYPE, NOT A BETTER TEST.** The probe
asserted the scatter was deterministic by computing it twice and comparing — and **that could never
have failed**, because both passes ran in one call at one tick. A seed drifting frame to frame,
making a wood shimmer as the camera moved, would have sailed through it. ⭐ **So `CanopyOn` is a
`static` taking a tile and an index**: it *cannot* reach `_world.Tick` or `_alpha`, and the compiler
refuses the bug instead of a test looking for it. *Determinism stopped being asserted and became
impossible.*

⚠️ **And the overhang guard failed on its first run for the right reason and the wrong measurement.**
It reported *"nothing overhangs"* while measuring canopy **centres** — a canopy centred at 0.45 with
a radius of 0.20 has already crossed the tile edge at 0.5. **It measures the branches now.** The
spread also genuinely was too timid, so both halves were real.

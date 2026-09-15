# Spec: The valley in view — terrain that reads as a place, not as storage

**Decisions:** D332, D333, D334, D337. Rendering only; follows `gridless.md §10.2`.
**Status:** ✅ **REBUILT AS A FIELD (2026-09-10, D342).** D337's trees and traced shoreline are **superseded**: the valley is now baked into one texture as the level set of a continuous field, so the river is an organic meander with a shallow bank, the deposits are irregular bodies, and a wood's foliage is texture rather than ~5,740 `DrawCircle` a frame. **`DrawTheShoreline` is deleted.** ⚠️ **The forest CLUMPS are still Manhattan diamonds** — §4 always said the renderer could not hide that, and it cannot; it is `PaintForest` and it is Joe's. **1073 passing, 0 failing, 2 skipped of 1075 — no golden moved.** ✅ Played by Joe through D343–D362. ⭐ **D366 (2026-09-12): THE NEAR VIEW IS MESHES TOO.** Joe: *"not sure whats happening with the FPS. its really dropping"* — 22 fps near in. D342 baked the FAR view and left every canopy, sapling, boulder, ore lump and berry patch above `TreeZoomFloor` as a live `DrawCircle` a frame (~5,740 — D338's own number, never fixed for the near view), and D358–D362 laid the trails on top the same way. Now they are `ArrayMesh` fans in tile space, a mesh per 8×8 chunk rebuilt only where the terrain changed (`VillageMap.Meshes.cs`, `MeshBuilder`), drawn with one tile→screen transform; `_Draw` is instrumented per pass on the debug line. Measured at the founding view, 48 px/tile: **the tree pass 5.7 ms → 0.0, the frame's draw 6.7 ms → 0.3**. `[widths] scenery:` reports the chunks, vertices, the first build (~26 ms) and one chunk's rebuild (~0.3 ms, what a fell costs), and that a felled tile takes its fans with it. **1121 passing, 0 failing, 2 skipped of 1123 — no golden moved. Unplayed by Joe as of this line.**

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
  twice. *(D338 restated the gate in units the zoom can reach — `TreeZoomFloor`, 24 px/tile — and
  gave the far view its foliage from the bake.)*
- ⛔⛔ **AND ABOVE THE GATE THEY ARE A MESH, NOT CIRCLES (D366).** A `DrawCircle` is a
  `CommandPolygon` that breaks Godot's 2D batching, so the near view issued ~5,740 unbatchable
  commands a frame and Joe read 22 fps. Every canopy, sapling, boulder, ore lump and berry patch is
  a twelve-segment fan in **tile space** with a vertex colour, in one `ArrayMesh` per 8×8-tile chunk
  (`VillageMap.Meshes.cs`, `MeshBuilder`), drawn with a tile→screen `Transform2D` — zoom and pan are
  a matrix, never a rebuild. A chunk is rebuilt only when a tile in it changed: a shadow copy of the
  terrain is diffed on `TerrainGeneration`, the `ValleyTexture` shape, so a fell costs one chunk
  (~0.3 ms) and a season of saplings maturing costs the chunks it touched. Berries are their own
  mesh per chunk because the player can switch them off; **the animals stay live** — they roam every
  frame and there are a few dozen. *`CLAUDE.md`'s rule, applied to the drawing: nothing derivable
  incrementally is rebuilt per frame.*

### 3.2 A shoreline the river can be seen to have

The water is drawn by the generic terrain loop — one flat square per tile, no bank, no edge. **The
contour tracer built in D332 already does exactly what is needed**, so the shoreline is that tracer
run over the water tiles and drawn as one smooth line.

⭐ **Cached on `SimWorld.TerrainGeneration`**, the counter `Minimap` already uses to decide when to
re-bake — terrain changes only when somebody clears ground, so the trace is paid then rather than
per frame.

### 3.3 Boulders on the seams (D347)

The same trick as §3.1, for stone and iron: lumps scattered from `Scramble` over the tile's
coordinates, overhanging the tile so an outcrop has a ragged edge. **Depletion is the seam eroding
tile by tile** — a dug tile is grass and stops having lumps — with the digger's leftover heap as
the trace until it is hauled (D84: no scar). The generated diamond is left as it is; the lumps hide
it, and changing it changes how much ore a valley holds.

### 3.4 Furrows, shoots and stalks (D349)

The fields: furrows on bare ground, sparse shoots when sown, dense stalks in the wheat colour when
ripe. **Not overhanging** — a field's edge is a fence line and the bake keeps worked ground square
by intent; the probe asserts every mark stays inside its tile.

## 4. What is deliberately NOT in this slice

- ~~**Filling the smoothed contour.** A concave polygon with holes is a real problem and the *edge*
  carries most of the benefit.~~ ✅ **DONE (D345), and it was not a polygon problem after all:** Delaunay
  over the loop points, keeping the triangles whose centroid sits on painted ground. Holes and
  concavity fall out of the same rule.
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
- ⭐ **And the frame cost is a number now (D366).** `VillageMap.LastFrame` times each pass of
  `_Draw` — trees, trails, fields, zones, rest — smoothed, on the debug line beside the fps. The
  instrument came first and measured the tree pass at **5.7 ms of a 6.7 ms frame** at the founding
  view before the mesh, and **0.0 of 0.3** after; anything that puts a per-element command back into
  a frame shows up there.
- `[widths] scenery:` — the chunks, the vertex counts, the whole-valley build once, the busiest
  chunk's rebuild alone, and **a felled tile rebuilds exactly one chunk and takes its fans with it**
  (red-checked: with the diff's dirty mark deleted it reads *"rebuilt 0 chunks … 5940 → 5940 →
  5940"*). ⚠️ What no probe checks is the picture: that the transform lands the mesh where
  `ToScreen` lands a point. That was checked by eye once (two screenshots, before and after,
  indistinguishable) and `tile centres:` guards `ToScreen` itself.

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

---

## 7. The rings on the buildings — a legend (D374, Joe: *"what do the coloured squares that sometimes show up on buildings mean?"*)

Every ring is a derived reading, never state; each has a control or a cause the inspector names.

| Ring | Means | Where it is set |
|---|---|---|
| **Orange** on a store | the store is full | `When full: Marker` on the store's inspector turns it off per building (D140) |
| **Light blue** on a workplace | idle — nobody working it, or nothing to do; the inspector's idle line says which | `Marker` on the workplace's inspector (D270/D271) |
| **Blue** on the library | full — the shelves hold what they can | — |
| **Yellow** outline | selected | the click |
| **Green** rings on homes | the homes a market placed here would be nearest for — the ghost's reach while placing (D201) | the placement ghost |

⏸️ **The cards (`handoff.md` item 3) carry this legend on the card itself when they land**; until
then it lives here.

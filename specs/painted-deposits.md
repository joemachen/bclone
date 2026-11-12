# Spec: Painted deposits — the stone and iron nodes, and room for the kinds to come

**Status:** ✍️ **SPECCED (A59, 2026-11-11) — Joe's *"the art for the stone and iron nodes (with knowledge that we'll create
more node types later)"*; ⏸️ FOUR CALLS HIS (§6). NOTHING BUILT.** Art lane, branch `art`. It follows the painted trees
(`tree-sprites.md`): the same hand, the same light, the same atlas-and-mesh way of drawing, so a seam reads as part of the
same painting as the wood beside it.

---

## 1. What this is, and what it is not

A **deposit** (Joe's *node*) is a seam tile: `Terrain.Rock` (stone) or `Terrain.IronDeposit` (iron). Today each one draws
**2–4 flat discs** (`Lumps`, `VillageMap.Meshes.cs`): stone `#cdc6b8`, iron `#5c3424`, 0.12–0.26 tiles across, shaded a
little each, placed by `LumpOn` (a `static` of the tile, D337). A **worked face** (a quarry or mine has claimed the tile,
`quarry.md §3.4`, `iron-mine.md §3.6`) draws one lump fewer, each cut to 0.6. The discs are drawn the same with the
watercolour on or off. Under them, the seam's ground is already painted (`ground-textures.md`, A12–A21).

This spec gives the lumps the trees' treatment: **painted rocks seen from straight above**, ink-outlined, lit from the upper
left, casting a soft shadow down-right, drawn from an atlas. Behind the watercolour tick (on by default since A58); with
it off the discs stay, byte for byte.

**Not in this spec:** the seam's ground (painted, A12–A21); the quarry and mine buildings, their floors and palisades;
the goods heaps; new deposit kinds themselves (clay, coal, copper, salt…), which are the systems lane's when they come.
This spec only leaves room for them (§4).

## 2. What the sim says, and must keep saying

- **A deposit is a tile, and its look is a pure function of the tile** (D337): how many rocks, where, which drawing,
  mirrored or not, all from `Scramble` of the tile. No tick, clock or RNG.
- **Worked is a fact the map already knows** (`_faceShadow`, re-read when the faces move): a claimed tile shows its
  worked drawings at once, and a face given up shows the natural ones again.
- **Nothing here is hashed or saved** (D335): it restates the map.

## 3. The look

- **Stone:** pale grey boulders, rounded and cracked, a lit upper-left face, a cool shadow side, an ink outline and one or
  two crack lines, a little lichen on some. A tile is a small cluster (call 1).
- **Iron:** the same rock family, darker and more angular, with **rust streaks and ochre stains** that say *ore* at a
  glance (call 2).
- **Worked** (call 3): stone is **cut blocks**, squared tops and a pale chipped face; iron is **broken rock and dark spoil**.
  Fewer pieces than the natural tile, so a face reads as worked from any zoom.
- **Winter** (call 4): snow lying on the rocks' upper-left, as on the conifers (A41).
- **Light and ink as the trees:** light from the upper left, ink `#3a2d20`, a see-through shadow down-right. **Never
  turned:** mirrored across the light's diagonal for a second face (A31).

## 4. The atlas, and room for more kinds

- **`textures/nodes/node_sprites.png`**: **128-px cells, eight across** (the young trees' cell, `tree-sprites.md §10`), at
  128 px a tile. **Each kind takes one 256-px band: eight natural drawings, then eight worked.** Stone is band 0 and iron
  band 1: 1024 × 512 for the two.
- **A new kind is a new band and one line.** The band a terrain draws from is named in a small look file,
  `looks/node_sprites.tres` (`Rock → 0`, `IronDeposit → 1`), not in code. A modder who adds a kind paints a band and adds a
  line; a kind with no line keeps today's discs.
- **Seasons:** a winter atlas, `node_sprites_winter.png`, cell for cell (call 4), crossfaded on the trees' clock
  (`TreeSeasonAt`), so the rocks whiten as the woods do.
- **Painted by script in Photoshop** (`D:\Projects\brushes\nodes\build_node_sprites.jsx`), as the trees were, and
  **measured before anything is built on it:**
  - **legibility** (`world-legibility.md §9`): the mid-tone at L\* ≥ 50 and ink under 20 %, so a villager's dot reads
    against the rocks. ⚠️ Today's iron disc is L\* about 27, the dark that a villager's dot is meant to own. So painted
    iron must read as iron by its **rust hue**, not by being dark;
  - **the hue family:** stone in the grey of `#cdc6b8`, iron in a rust family near `#9a5a3c`, so the minimap still reads
    them;
  - **the alpha rule:** opaque or clear, except the shadow (A33).

## 5. How the game draws them

- **Where and how many:** `LumpOn`'s places, as today (call 1 may change the count); a drawing and a face from the
  tile's hash; size × 0.85–1.10.
- **A textured mesh per chunk**, as the trees (`SceneryChunk`), drawn where the lumps are drawn now: under the buildings,
  over the ground. The discs leave the standing mesh while the tick is on.
- **Cost:** six vertices a rock against a disc's 36, one atlas of about 2 MB with mipmaps; GPU measured at 4K, A/B, before
  it is called done.
- **Behind the tick:** off, `Lumps` and `WorkedFace` draw the discs to the vertex.

## 6. Joe's calls (A59), in plain words, each with a recommendation

1. **How many rocks a tile.** ⭐ **Recommended: two or three bigger ones, each a third to half a tile across.** A seam
   reads as a rocky outcrop, as the trees went fewer and bigger. Or keep **today's 2–4 small ones**, more like scattered
   stones.
2. **What says *iron*.** ⭐ **Recommended: grey-brown rock streaked with rust and ochre stains**, the colour of ore, light
   enough that villagers still stand out against it. Or **dark red-brown rocks** like today's, which are the darkest thing
   on the map, about as dark as a villager's dot.
3. **A worked face.** ⭐ **Recommended: painted.** Stone as cut blocks with squared tops; iron as broken rock and dark
   spoil, fewer pieces than the natural tile. Or keep **today's cut-down rocks**, the natural drawings shrunk.
4. **Winter.** ⭐ **Recommended: snow on the rocks**, a white rim on their upper-left, as the firs got. A bare grey seam
   in a white valley would be the summer-trees problem again. Or **unchanged all year**.

## 7. Verification

- **Tick off:** windowed renders at four zooms byte-identical to the build before.
- **The probe:** the atlas imported lossless and mipmapped at its size; the look file's bands; a seam tile drawing its
  kind's natural cells and a claimed tile its worked cells; a kind with no band keeps its discs; with the tick off, the
  discs to the vertex. Red-checked, the reds counted (D326).
- **The limits (§4):** measured, with the numbers to Joe if any drawing falls outside.
- **GPU:** `measure4k`, A/B, paired runs (HANDOFF art trap 29).
- **The four checks** in `CLAUDE.md`; Joe's eyes; merged only at his word.

# Building roofs — the village's buildings painted, seen straight down (A85)

> **Status: ✅ MERGED INTO `main` AND PUSHED (A88, 2026-11-15; built on `art`, A85), PLAYED (A89: *"cart and yards look good"*).** Behind the watercolour tick;
> with it off the map is byte-identical to the build before. Art lane (CLAUDE.md *Two tracks*). Nothing in the sim,
> `data/` or `tests/` changes.

> ⭐ **A86 (2026-11-15) builds on this:** the roof now stands on part of the ground with a yard beside it, the pen draws the material and hatches the shade, and the textures carry the wash (`building-yards.md`). §2–§3 here still hold for the roof itself; the colour is mixed 12 % toward the paper, and §5's runs now put fills and pen lines in one mesh.

## 1. What and why

The buildings were the least finished thing on the map: every one a flat rectangle in its kind's colour with an ink edge
(`art-inventory.md §5`). Joe asked for them in their own art-lane session and picked **look A** from three mocks painted
over his own villages (`D:\Projects\brushes\buildings\buildings_options.jpg`, `buildings_catalogue.jpg`; A85):

- **A — roofs, painted by material** (chosen). From straight above a building is its roof. ✅
- B — the roof inside a strip of timber-framed wall. Two or three pixels of wall at play zoom; reads as a frame.
- C — a floor plan cut away, as concept sheet 03. Roofless, it reads as ruins; kinds blur (41 close pairs of 253, §4).

**Fixed by earlier calls:** drawn straight down (A31) — the concept sheets give the style, never the angle (A80); every
kind still reads at a glance as its kind (§1.1 of `DESIGN.md`, the palette of D637); buildings stay under the painted
canopy, sites dashed through it (A36, A53).

## 2. The look

Each finished building is drawn, in its own turned frame, as:

1. **A shadow** on the ground toward the lower right (the valley's light is from the upper left): the eaves' rectangle
   moved (0.13, 0.17) tiles on screen × the building's *lift* (1 for most, 1.3 the barn, 1.4 the manor, 1.5 the town
   hall), three nested washes (±0.05 tile) in `RoofShadow`, 0.11 each.
2. **A door step** outside the eave on the wall the sim's door opens through (`doors.md §12.2`; the same wall and slide as
   `DoorQuadAt`), 0.30 × 0.12 tile, stone, a fine ink edge; faint when the door is shut.
3. **The roof**: the drawn footprint (0.8 of the extent; manor 0.92, town hall 0.95) plus **0.04 tile of eave** all round.
   - **Gable** when the long side is at least 1.45 × the short (2 × 1 houses and huts, the barn, the town hall): two faces
     meeting at a ridge down the long middle. **Hip** otherwise (2 × 2 and 1 × 1): four faces — two trapezoids and two
     triangles, which is the nearest-eave partition of the rectangle.
   - **Each face is lit by the way it faces:** `1 + 0.17 × (its outward direction on screen · toward the light)`, the
     light toward the upper left. Opposite faces cancel, so the roof's mean is its colour. **A building turned to any
     1/64 is still lit from the upper left** — the game lights the faces, so a turn never carries painted light round
     with it (`art-inventory.md` B4's open question, answered for buildings).
   - **Painted in its material**, a texture laid in the face's own frame (along the eave, up from it) and multiplied by
     the kind's colour, so a material carries pattern and the kind carries colour (§3).
   - The eave inked all round (2 px, `Ink`; dashed when fallen in, D556), the ridge and the hips a fine pen, the ridge a
     faint darker band.
4. **Chimneys** on the ridge: the cabin, cottage, smithy, bakery, farmhouse, workshop, tailor and — for its stove's smoke
   (A139, `indoors-and-smoke.md`) — the school one; the manor two.
   Stone, a dark flue, an ink edge, a small shadow on the roof.
5. **Its own extras:** the manor's upper storey (a smaller gable over the main roof, a step lighter); the town hall's bell
   turret (a small hip in clay tile on the ridge).

**Shapes that are not a roof** (they say what they are by shape):

| Kind | Drawn as |
|---|---|
| Market | Trodden ground in its colour mixed with earth, three striped stalls on it (its colour and cream) |
| Stockpile | Trodden ground, a post at each corner; the goods it holds drawn on it as today |
| Mill | A windmill from above: a round body, a cap, four lattice sails turned with the building |
| Well | A ring of stones round dark water |

**Kept as they were:** construction and demolition sites (A53's dashes included), a house's new walls rising during a
rebuild, bridges (the founders' cart is drawn since A87, `building-yards.md §3.4`), the stock chips on a store, the full-store, idle and library rings, the
card's picture, the minimap, the bar's glyphs. **With the tick off nothing changes.**

## 3. Materials (`src/Bclone.Game/textures/buildings/roof_<material>.png`)

Seven tone maps, 384 × 128 (x along the eave −1.5 … 1.5 tiles, y up the slope 0 … 1 tile, 128 px a tile), lossless and
mipmapped, drawn `LinearWithMipmaps`. Each holds a material's pattern — courses, strands, tiles — and the watercolour:
pooled darker at the eave, a blotchy wash, the paper's tooth and granulation. **Its mean tone is 0.8** and the game
multiplies by the kind's colour ÷ 0.8, so a roof's mean colour is its kind's colour. A building's seed mirrors its
material along the eave, so neighbours differ. Made by `D:\Projects\brushes\buildings\make_roof_textures.py` (outside the
repo, as the trees' Photoshop script is) from the chosen mock's own pattern code; a painter may replace any of them by
hand to the same size and mean.

| Material | Kinds |
|---|---|
| Thatch | cabin, granary, farmhouse |
| Wooden shingle | cottage, warehouse, builder's hut, woodcutter, hunter's lodge, quarry, workshop; any trade not listed |
| Slate | manor, smithy, library, trading post |
| Clay tile | iron mine, bakery, tailor, barn |
| Boards | fishing hut |
| Turf | forager, forester |
| Stone slab | town hall |

**The colours are today's** (`ColourOf`, `TradeGlyph.ColourOf`, the stores'), weathered as before (D560), with two
exceptions on the houses, whose *walls* carried their colour: the cabin's roof is its own `HomeColour` thatch; **the
cottage's roof is weathered-oak shingle `#938d82`** (not its walls' stone grey — a slate roof there sat ΔE 3.8 from the
smithy); **the manor's is its dark slate `#4a535c`**. The card and the minimap keep the wall colours.

## 4. Legibility, measured on the mock

Each kind's mean colour over its footprint (CIELAB), Hollowmere two zooms out, 23 kinds, pairs closer than ΔE 10 of
253: **today 14, A 17**, B 18, C 41. Today's own worst pairs — builder's hut / farmhouse 1.1 and builder / market 2.0
(all three fall through to `MarketColour`, `art-inventory.md §5.2`), mill / town hall 2.0 — are *better* separated in A
(worst: market / stockpile 3.5), because material and shape carry what colour did not. ⚠️ The three that share the
market's orange still share it; giving them colours of their own is a palette change, his call, not made here.

## 5. Code

`VillageMap.Roofs.cs`: `RoofOf(kind)` (the table above), `RoofDrawing(...)` — a pure function returning the pieces a
building is drawn with (polygons, their texture coordinates and colours, lines), which the map draws and the probe
reads, so they cannot disagree — and `DrawPaintedBuilding`, which draws them only with the tick on. The six building
draws in `VillageMap.cs` call it and fall back to the rectangle when it says no.

**⛔ Nothing is laid per frame (CLAUDE.md).** A building's pieces are laid once, in tile space, into meshes kept in
`_roofCache` (keyed by where it stands) with the `RoofSignature` they were laid from — its look, place, size, turn, colour,
fallen-in, door and seed. While the signature holds, a frame only hands the meshes over with its one tile-to-screen
transform (`CanvasItemAddMesh`, the trees' way). The pieces become *runs* in their drawing order: consecutive fills one
mesh, consecutive faces of one material one mesh, consecutive pen lines one mesh — the lines laid for the current zoom
(their width is in pixels: a core `width − 1` px and a pixel of feather each side, as an antialiased line is) and laid
again only when the zoom changes. Entries for buildings no longer drawn are dropped when `BuildingGeneration` moves.
Neither the cache nor its counter is hashed or saved (D335).

**Measured** (4K SubViewport, Hollowmere's west end at the opening zoom, tick on, this build / the build before / this /
before in one sitting): `_Draw` 9.96 / 10.63 / 10.07 / 10.69 ms — *cheaper* than the rectangles; GPU 4.75 / 5.16 / 4.77
/ 4.91 ms, no dearer. The first build, drawing every piece every frame, cost +5.3 ms of `_Draw`; with the fills and faces
meshed and the lines still drawn a call a line, +1.9 ms (2.7 ms of it the lines).

## 6. Probe: `painted buildings:`

Named from this spec, not from the code's own lists (art traps 17, 25): the seven textures load at 384 × 128, mipmapped,
drawn with mipmaps, each mean tone 0.8 ± 0.03; every kind in §3's table draws in its material; a 2 × 1 is a gable along
its long side, a 2 × 2 and a 1 × 1 a hip, the faces tiling the eaves' rectangle (0.04 beyond the drawn footprint) with
no gap or overlap; at sixteen turns the face toward the upper left is the lightest and its opposite the darkest, and the
area-weighted mean light is 1; a door's step lies outside the eave on the door's own wall; the chimneys counted per kind;
the turret on the town hall alone; the mill's four sails, the well's water, the market's three stalls; the cache keeps a
building asked twice and lays it again when its colour changes; the runs carry every polygon fanned and every pen line in
order, the lines laid for the zoom, a face's vertex colour its piece's × 0.8; with the tick off nothing is painted.
⚠️ Draw-time only, so a render's evidence: the texture's look, the stripes, the shadows' softness, a draw site that ignored
the tick, the lines laid again when the zoom moves.

## 6a. 🔨 The windmill's sails turn while it is worked (A145)

Joe, relayed by the systems lane, 2026-11-21: *"windmill definitely needs to animate, but only when it is being worked"*.

- The sails are a run of their own (`TurnsWithTheSails`: cloth, lattice, edge, spar), laid with the roof, between its shadow and
  its body, and drawn through `SailsTurned(hub, angle)`; the body and cap stay still over them. A zoom re-lays them still
  turning (`RelaidForTheZoom`).
- **Worked** = a miller `WorkingABatch` at the mill this tick, noted once a tick (`NoteTheFires`, as the forge's fire).
- They gather way over 2.5 s and come to rest over 4 s, about a turn in seven seconds at speed, on the people's clock (game speed
  capped 1.5×, still when paused). View only.
- ⚠️ No save of his has a finished mill; filmed on a scratch hack (reverted): `D:\Projects\brushes\buildings\mill\mill_turning.gif`.
- Probe (`indoors and smoke:`) and red-check: A145 in DESIGN §7 — 11 mutants, 9 red; two zeros written down (a mill noted as
  worked — unseen on a living mill; the turn drawn — the film).

## 7. Not done

Roofs in snow (`art-inventory.md` §5.1's gap) — the snow lies on the ground only; burning and burnt; per-kind ruins (a
fallen-in roof is still today's dashed edge); construction stages; the card's picture and the bar's glyphs in the new
look; the school (on `slice/school`) draws as an unlisted trade, shingle, until it is listed.

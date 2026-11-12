# Spec: Painted crops — wheat first, in a shape every crop after it can take

**Status:** ✍️ **SPECCED (A59, 2026-11-11) — Joe's *"the wheat farm/crop - wheat will be the default available crop, but
more crops will be unlockable, so wheat won't be the only crop image we need to create - i just want to see it now"*;
✅ HIS FOUR CALLS ANSWERED, ALL AS RECOMMENDED (A60): ROWS EAST–WEST VALLEY-WIDE, FIVE STAGES, UNDER THE SNOW, A SOFT WASH AT THE EDGE. 🔨 BUILT (A61, §8). ⭐ APPROVED (A62, *"approved."*); NOT MERGED.** Art lane, branch `art`.

---

## 1. What this is, and what it is not

A farm's **field** is the ground its player painted, traced and filled along the paint's own curve (D342, D352,
`DrawWorkedGround`). It is split by stage, and each stage is a flat fill: ploughed `#b39874` (`Terrain.Field`), sown
`#a8b07a` (`Sown`), ripe `#d6b55c` (`Ripe`). Over the fill, drawn per tile and only on painted quarters (D351, D360), are
**three furrow lines** (ploughed), **5–7 short green shoots** (sown) or **10–12 golden stalks** (ripe), from 24 px a tile
up. The fills are drawn over the watercolour layer, so **in winter a field stays brown in a white valley**.

This spec paints the field: **a seamless watercolour texture per stage of the crop, laid world-anchored inside the
field's traced outline**, so it follows a round field's curve exactly as the fill does today, reads at every zoom, and
changes through the year. Wheat first; **every crop is a folder of the same stages** (§4), so a crop unlocked later is
painted, not coded.

**Not in this spec:** orchards (`crops-and-orchards.md §8`, deferred); blight (B11, no spec); the farmhouse and its store;
the other crops' paintings, which come with the crops. A trail worn through a field (D512) stays drawn over the crop.

## 2. What the sim says, and must keep saying

- **The calendar** (`crops-and-orchards.md §5`): spring `Field` → `Sown`; summer `Sown`, growing; autumn `Sown` → `Ripe` →
  reaped, back to `Field`; winter `Field`, stubble. A ripe field nobody reaps rots over winter (§5.1).
- **A sown tile carries a crop id** (`GeneratedMap.CropAt`, a byte; 0 bare), and the crop rows are data
  (`SimConfig.Crops`: `{ id, name, yields }`, D348). Wheat is id 1. The view picks the crop's paintings by its **name**.
- **The look is a function of the tile, its stage, its crop and the view's year clock** (`TheYearAt`, the clock the grass,
  snow and trees already share). No tick, no RNG; nothing hashed or saved.

## 3. The stages (call 2)

| stage | when (sim stage + the year) | wheat looks like |
|---|---|---|
| **ploughed** | `Field`, spring, before it is sown | dark turned earth in furrow lines |
| **young** | `Sown`, early: from sowing to mid-summer | fine rows of fresh green shoots, earth between |
| **growing** | `Sown`, mid-summer on | tall green rows, closing over the earth |
| **ripe** | `Ripe` | golden wheat, ears catching the light, rows still read |
| **stubble** | `Field`, from reaping through winter | pale cut stalks in rows on dry earth |

**Young turns to growing on the year clock**, as the grass turns: a crossfade over the same days. A field sown late still
shows young until the clock moves. **Winter** (call 3): stubble under the valley's snow. **A field rotting** (ripe, winter
come) is ripe greying to stubble as the snow comes; the log and the farm's ring say so (§5.1), and the picture only
agrees.

## 4. The paintings, and room for every crop

- **`textures/crops/<crop name>/`**: one seamless tile per stage, `ploughed.png`, `young.png`, `growing.png`, `ripe.png`,
  `stubble.png`. Each is 1024 × 1024 at **128 px a tile**, so a pattern repeats every 8 tiles. Painted to tile without a
  seam, as the ground textures were (`ground-textures.md`, A12).
- **A crop is a folder.** The view reads the crop's `name` from the config row and looks for `textures/crops/<name>/`. A
  crop with no folder (or a missing stage) keeps today's fill and stalks, so an unlocked crop never draws as nothing.
  **`ploughed` and `stubble` are shared** by default (`textures/crops/_ground/`), since turned earth is turned earth; a crop
  may paint its own.
- **Rows** (call 1): painted into each stage's texture, so the rows of every field line up the way the texture is laid.
- **Painted by script in Photoshop** (`D:\Projects\brushes\crops\build_crop_stages.jsx`, wheat first) and **measured
  before it is built on:** each stage seamless (its edges compared); its mean in the stage's hue family (`FieldColour`,
  `SownColour`, `RipeColour`, so the minimap still reads a field's stage); legibility: the mid-tone L\* ≥ 50 with dark marks
  under 20 %, so a farmer's dot reads in the wheat.

## 5. How the game draws them

- **The traced fill, textured:** `DrawWorkedGround` draws each stage's triangles with UVs from their world position (128
  px a tile, the texture repeating) and the stage's painting, in place of the flat colour. One draw a stage a crop; the
  triangles are the ones already cached (`_fieldFills`), never rebuilt a frame (D338).
- **While a stage turns** (young to growing), that stage is drawn twice, the outgoing painting then the incoming at the
  turn's share, as the trees cross seasons.
- **The per-tile furrows, shoots and stalks** are not drawn while the paintings are on (the painting has them), so a
  field costs fewer draws, not more.
- **Snow over the fields** (call 3): the field is drawn under the valley's snow pass, or given the snow's own share from
  the shader's year uniform. How is settled by measuring, not decided here.
- **Behind the watercolour tick** (on by default since A58). Off: today's fills, furrows, shoots and stalks, byte for byte.
- **Cost:** measured at 4K with a valley of fields, A/B, before it is called done.

## 6. Joe's calls (A59), in plain words, each with a recommendation

1. **Which way the rows run.** ⭐ **Recommended: one way across the whole valley** (east–west), so every field's rows are
   parallel. It reads as one farmed landscape, and it is simple and exact on any field shape. Or **each field its own
   way**, along its longest side, which is nicer on a long field but means working each field's direction out and a
   separate draw a field.
2. **How many stages.** ⭐ **Recommended: five** (§3): ploughed, young green, tall green, ripe gold, stubble, so a field
   visibly grows through the summer and shows it has been reaped. Or **three**, as today: ploughed, green, ripe.
3. **Fields in winter.** ⭐ **Recommended: under the snow**, like the rest of the valley, stubble showing through where
   the snow is thin. Today a field stays a brown patch in the white. Or **bare earth all winter**.
4. **A field's edge.** ⭐ **Recommended: no ink line, a soft darker wash at the edge** (pigment pooling, as A28 gave the
   ground). It matches *"the ground isn't outlined"* (A28), and the fence or hedge stays the farm's own. Or **a thin pen
   line round each field**, like the river bank's, so fields read as drawn shapes.

## 7. Verification

- **Tick off:** windowed renders at four zooms byte-identical to the build before, with a field of each stage on the map.
- **The probe:** each stage's painting imported lossless, mipmapped and repeating; a field of each stage drawing its
  stage's painting with world-anchored UVs (a tile's corner at 128 px steps); a crop with no folder keeping the fill and
  stalks; the year clock's young-to-growing turn at named moments; off, the fills and stalks as today. Red-checked, the
  reds counted (D326).
- **The paintings measured** (§4), the numbers to Joe if any falls outside.
- **Renders** of a farm through the year: spring, mid-summer, autumn ripe, after reaping, winter.
- **GPU:** `measure4k`, A/B, paired runs.
- **The four checks** in `CLAUDE.md`; Joe's eyes; merged only at his word.

## 8. As built (A61, 2026-11-11)

- **Painted in Python, not Photoshop** (`D:\Projects\brushes\crops\build_crop_stages.py`, seed 61): a tile must not show
  a seam, and Photoshop's Clouds and Add Noise are not periodic. Every noise field is filtered white noise in Fourier
  space and every mark wraps, so the paintings tile by construction. Measured: across, the step over each wrap is
  0.92–1.06× a typical neighbour step. Down, the wrap falls on a crop row's middle, so it is compared with the other row
  middles, and each matches its own (ploughed 0.97 against 0.97, young 1.53 against 1.46, growing 0.93 against 0.97, ripe
  0.83 against 0.87, stubble 1.37 against 1.37). The painted files:
  - `textures/crops/_ground/ploughed.png`, `stubble.png` and `snow.png` (shared);
  - `textures/crops/wheat/young.png`, `growing.png` and `ripe.png`.

  Each is 1024², 128 px a tile, with rows east–west four to a tile, imported lossless and mipmapped.
- **Measured** (§4): L\* p50 59–73, dark marks at most 15 %. The means sit in their fills' hue family, except **young**:
  its mean (89°) is between ploughed (77°) and sown (114°), because a young field is mostly earth with shoots in it,
  which is what it should look like. The first young and stubble were too faint at the closest zoom, so the shoots and
  stalks were made stronger. The fields' snow is white with a soft periodic alpha averaging 0.78. The first, at 0.63, made a
  winter field look like camouflage beside the valley's smooth white.
- **In the game:**
  - The farm's tracing splits sown and ripe ground **by crop** (`CropAt`), so a field of two crops is two sets. With one
    crop the sets are the stage's, cell for cell.
  - Each fill keeps its texture coordinates (`CropUvOf`, eight tiles a painting) and indices beside its triangles, built
    once with them.
  - `DrawWorkedGround` lays each Field, Sown or Ripe fill as its painting with `CanvasItemAddTriangleArray`, the next
    stage over it at the turn's share (`CropStageAt`: ploughed in spring and summer, stubble from autumn, crossfading as
    the season turns; young in spring, growing after; ripe).
  - Then come each field's edge as two soft dark polylines (pooling, no ink line) and, at the year's snow share, the
    snow painting over every field fill.
  - The per-tile furrows, shoots and stalks are not drawn while the paintings are on.
  - A crop's painting is `textures/crops/<row name>/<stage>.png`, falling back to `_ground`. A crop with neither keeps
    its flat fill.
- **The probe:** `painted crops:` checks:
  - the seven paintings asked for, each its file at 1024², mipmapped, repeating (wheat's ploughed falls back to
    `_ground`);
  - a crop with no row finding no young painting;
  - the year at nine named moments;
  - the mapping (eight tiles a painting);
  - on with the tick and off without it.

**Verified (A61):**
- **Watercolour off:** a demo farm was raised by a temporary hook (marked, stocked, worked and completed through the
  sim's own `Complete`, reached by reflection; the hook was removed before the commit). With it on the map, renders at
  three zooms are byte-identical to the build before. The opening zoom's off shot was taken before the script switched
  off, so it is on since A58.
- **Through a year at 20×, with the demo farm:**
  - spring shows ploughed, young and ripe strips;
  - summer turns young to tall green;
  - autumn ripens the sown strip and turns the ploughed to stubble;
  - winter lays the snow, stubble and gold showing faintly through it.
- **GPU:** not measured; Photoshop was holding the card in its slow state (A60). A field is at most two triangle draws,
  a snow draw and its edge, against the flat look's fill plus a line or 5–12 stalks per tile.
- **Red-check:** 8 mutants, 6 red (bare ground ploughed all year; sown ground never young; a painting every four tiles; the paintings not repeating; the crop's own folder never read; the crops never painted). **Two zeros, written down:** the fields drawn flat with the tick on, and no snow on the fields. Both are draw-time only (`_Draw`, never headless), and the year's renders are their evidence. The harness crashed once, on a Windows file lock mid-restore. It left C3's mutant in place, which was found by its anchor count and reversed by its own text; the harness now retries a locked write.

# Spec: The hills' drawn edge — an inked lip, a lit side, a shaded bank

**Status:** ✍️ **SPECCED (A66, 2026-11-13). Joe's two calls answered on a mock (*"B: lip, light and shade"*, *"Only steep
banks"*). 🔨 BUILT (A66, §7). ⏸️ HIS EYES.** *(Scope, from Joe through the main session, 2026-11-13: "exact style" means the watercolour grass and ground, plus this edge; trees and buildings stay top-down as A31 set, and the panels are later.)* Art lane, branch `art`. It is the first piece of the map *toward Joe's concept* (A65). It is also
`terrain-shader.md §7` piece 5 (relief), which A57 parked until the sim had real heights. It has them now: `main`,
D735–D742, `specs/heights.md`.

---

## 1. Why

Joe's concept (`D:\Projects\brushes\concept\joe-concept-2026-11-13.jpg`, outside git) puts the village on a raised
terrace whose edge is **drawn**: an ink lip along the top, the land lit on one side and shaded on the other. His verdict
on the heights as built (`hilly-as-built-2026-11-13.webp`): *"still just looks like a shadow, not a hill."* The stand-in
(`VillageMap.Relief.cs`, D740–D741) is a soft wash of light and shade by height, grouped into steps, and a wash alone
reads as a shadow. The edge of the rise is what makes land read as raised.

**What he has already turned down on the systems lane, and this spec keeps out:**
- contour lines across the land (D739: *"oh, no i hate that"*);
- a fainter wash (D737–D738: *"i dont see hills at all"*);
- a stronger one (*"it just looks like dark spots"*).

## 2. Joe's calls (A65–A66)

| # | The question | His answer |
|---|---|---|
| 1 | The concept draws buildings and trees at an angle, which A31 ruled out. Which wins? | ***"Straight down"***. Everything stays drawn from above, and the concept is matched in everything else. |
| 2 | The parchment interface: when? | ***"After the map"***. |
| 3 | On a mock (`D:\Projects\brushes\hills\hill_edges_mock.png`): A the wash, B the lip with light and shade, C B with hachures | ***"B: lip, light and shade"*** |
| 4 | Which slopes get the lip? | ***"Only steep banks"***. Gentle ground is lit and shaded only. The lips mark the climbs that slow the walk. |

## 3. What it draws

- **Light and shade on every hill.** The ground's slope is taken from the sim's heights at each pixel and lit from the
  map's upper left (the trees' light, A31). Facing the light is washed lighter; facing away, darker. Flat ground is
  untouched.
- **A darker bank.** Steep ground, the sim's own *steep* class (`slope_steep_at`: 8 levels of 256 a tile), is washed a
  little darker again, most where it faces away from the light.
- **One inked lip, along the top of each steep bank.** That is where the ground turns from not-steep to steep going
  downhill, so it is drawn on the uphill edge of the steep band only, never at its foot. It uses the river bank's own
  pen (`valley_pen`, A28/A45): the same ink, pressure and lifts, a little thinner. Nothing is drawn over water, and
  nothing on gentle ground.
- **The riverbank is not a cliff.** The sim counts steepness land to land (D737), so the drop from a bank to the water
  is not a slope. The view does the same: the heights it draws from have every water tile filled with the land's level
  beside it.

## 4. How

- **The heights as a texture.** A float texture of the valley's tile heights, water filled from the land around it, is
  built once per world. The heights never change in play (`heights.md §3`). Not hashed, not saved (D335). The shader
  reads it between tile centres and takes the slope, in levels per tile, from a tile's width either side.
- **In `valley_terrain.gdshader`**, after the ground's colour and before the bank's stroke and the paper's grain. The
  lip's distance in screen pixels is taken from the slope field's derivatives, as the bank's is from the water's
  weight, so it stays the same width at every zoom.
- **The values in `looks/valley_watercolour.tres`:** light, shade, bank, lip strength and width, the slope that counts
  as a full tilt. The steep threshold is not a look value: it is read from the sim's config (`slope_steep_at`), so the
  lip and the slow ground are the same ground.
- **Behind the watercolour tick, as every piece is.** With it on, the stand-in wash is not drawn (this replaces it).
  With it off, the map is the systems lane's, byte for byte, the stand-in included.
- **A flat valley** (every save from before heights) draws no hills, in either look.

## 5. Not in this piece

- hachures (his B, not C);
- contour lines;
- buildings standing on slopes;
- the terraced *flat tops* themselves (the sim makes the ground; this only draws it);
- the rest of the concept: ground colours, the trees' ink, paths and the plaza, and, after the map, the interface.

## 6. Verification

- **Tick off:** windowed renders at four zooms byte-identical to the build before.
- **The probe (`hill edges:`):**
  - the heights texture matches the map, with water filled from the land;
  - the uniforms are the look file's, and the steep threshold is the config's;
  - the stand-in is skipped with the tick on and drawn with it off;
  - a flat valley turns the hills off.
- **Red-check:** the reds counted (D326), any zero written down.
- **Renders** of the founding valley (Rolling by default) at four zooms, and of a Hilly valley, beside the stand-in.
- **GPU** at 4K, A/B, with Photoshop closed (A60's lesson).
- **The four checks** in `CLAUDE.md`; Joe's eyes; merged only at his word.

## 7. As built (A66, 2026-11-13)

- **The field** (`VillageMap.Watercolour.cs`, `HillField`) is baked once a world at **four texels a tile**: R the height,
  with water filled from the nearest land (`HillHeights`, outward from the banks); G **1 where the sim calls a tile steep**
  (`SteepnessAtIndex ≥ slope_steep_at`) and 0 elsewhere. Each is upsampled between tile centres and softened by two box
  passes half a tile across, as an `Rgf` texture.
- **In the shader:**
  - the height's slope (a tile across, levels a tile) lights the ground from the upper left, with 8 levels a tile drawn
    as a 45° tilt (`HillFullTiltLevels`);
  - the steep mask darkens the bank;
  - the lip is the mask's halfway line on the uphill side, drawn with the bank's pen.
- **Three first tries failed on the founding valley, all measured or rendered:**
  - Steepness worked out per pixel from a tile-sized texture: the lip drew blocks at the widest zoom.
  - The lip's distance from screen derivatives, which work in 2 × 2 blocks: squares where the line hovered over a patch.
  - The steepness itself against 8: a Rolling valley's banks are only just steep, so the crossing was faint and
    uncertain. The mask goes 0 to 1 across a bank's edge however steep the bank, and a lone steep tile softens below half
    and draws nothing.
- **The values** (`looks/valley_watercolour.tres`): full tilt 8, light 0.55, shade 0.55, bank 0.22, lip 0.8 at 0.8 of the
  bank's pen.
- **The stand-in wash** (`VillageMap.Relief.cs`) is drawn only with the watercolour off.

**Verified (A66):**
- **Watercolour off:** windowed renders at four zooms byte-identical to the build before (`982ad5f`), the stand-in included.
- **The founding valley** (Rolling: 9,190 land tiles raised, highest 163 of 256, 654 steep): light and shade across the
  slopes, and an inked lip with a shaded bank below it where a hill's steep edge is (the widest zoom).
- **The probe (`hill edges:`):**
  - the field's size (four texels a tile, `Rgf`);
  - a made-up terrace (flat far from the step, rising through it, the mask over half at the step, and spread by the
    softening);
  - the steep mask matching the sim's class (over 0.9 inside a steep patch, 0 two tiles clear of any);
  - water filled from the land, and a flat valley drawing none;
  - the look's values handed over.

  113 ✅ 0 ⛔.
- **GPU at 4K, the 3070, A B A B against the build before:** the card was in its slow state all sitting (the flat map at
  0.72 ms against about 0.27 normally). Paired, the hills add 0.2–0.46 ms there, 4–9 % of the watercolour's cost, so about
  0.1–0.2 ms at normal clocks. The whole switch is then about 2 ms, against A23's 3.5 ms.
- **Red-check:** 8 mutants, 5 red (the steep mask from the gentle class; the water left at 0, a cliff at every bank; the field not softened, caught only after a softening check was added; the hills never switched on; two texels a tile). **Three zeros, written down, all draw-time** (`_Draw` and the shader never run headless): the stand-in drawn under the watercolour too, the lip on both edges of a bank, and the light from the lower right. The renders are their evidence.

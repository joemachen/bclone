# Spec: The green ground — the hills told by the grass itself

**Status:** ✍️ **SPECCED (A68, 2026-11-13)** at Joe's pick on a mock (*"C"*, and *"Yep!"* to deeper trees).
🔨 **BUILT on `art` (A68, §7).** ⭐ **APPROVED BY JOE in his own game (A69, 2026-11-14: *"Looks good"*).** ⭐ **MERGED INTO `main` AT HIS WORD (A70, 2026-11-14: *"Merge"*, `917f505`), not pushed.** Art lane. It replaces the *look* of
`hill-edges.md` (A66, refused: A67) and keeps that spec's field and its lip. Neighbours: `terrain-shader.md` (the
valley's shader), `grass-look.md` (D632, the approved grass), `heights.md` (D735–D742, the sim's heights),
`ground-textures.md §3` (how dark the ground may go), `tree-sprites.md` (the crowns).

---

## 1. Why

Joe refused four ways of drawing a hill, and all four darkened the parchment toward grey: a soft wash by height
(D738, D740, D741: *"looks like a shadow"*, *"dark spots"*), contour lines every N levels (D739: *"i hate that"*), and
A66's shaded slopes (A67: *"the big dark areas just look like cloud shadows. its grey, not green maybe?"*).
A grey patch on a tan sheet is a shadow lying on flat ground.

His new reference (`D:\Projects\brushes\concept\joe-reference-2026-11-13.jpg`, outside git) has no grey in its land.
The ground is green almost everywhere, and the *green* carries the height: deep in the hollows and by the water, a
yellow-green on the crests. The pen marks only where the land breaks. His words: *"i meant reworking the watercolor
grass/ground so it looks more like the attached (new) reference image's grass/ground - maybe those need to be linked
to height/depth/hills in some way?"*

## 2. Joe's calls (A67–A68)

| # | The question | His answer |
|---|---|---|
| 1 | Three mocks on his real valley (`brushes\hills\a67_options_close.jpg`): A colour only, B colour and the pen, C the brush follows the hill | ***"C"*** |
| 2 | On green ground today's crowns are paler than the grass. Deepen them about a fifth (a tint, no repaint)? | ***"Yep!"*** |
| 3 | This replaces the approved grass's parchment base (D632) | asked for by him (§1); the approved masks stay, over the green |

⛔ **Stays out:** anything multiplied toward grey; contours every N levels; buildings or trees at an angle (A31); the
parchment panels (A65: after the map).

## 3. What it draws

1. **Green by height.** The grass's own ground is no longer the parchment. It is a wash that runs from a deep green at
   the river's level to a yellow-green on the highest ground, the same green for the same height in every valley (so a
   Rolling valley's tops are greener than a Hilly one's). The parchment stays where the reference has it: beyond the
   valley's edge.
2. **Crests bloom, hollows sink.** Ground standing above its surroundings (a crest, a shoulder) turns toward the
   crest's yellow; ground below them (a hollow, a bank's foot) toward the deep green. This is the shape of the land at
   a hill's own scale, and it does not depend on a light.
3. **A slope turns in hue, never toward grey.** Facing the light (upper left, the trees' light, A31) a slope turns
   warmer and lighter; facing away, cooler and deeper. Steep ground is the deepest green on the sheet.
4. **The pen, only where the land breaks.** A66's lip stays: one inked line along the top of each steep bank. New:
   **hachures**, short pen strokes from the lip down the bank, long and short by turns, tapering. Steep banks only
   (the sim's own class, `slope_steep_at`), so the pen marks the climbs that slow the walk. Nothing on gentle ground.
5. **The brush follows the hill.** The grass's brushwork (faint streaks in the wash) and its **tuft marks** (short
   paired pen dashes scattered over the meadows) run along the contours wherever the land slopes, and along the
   painter's own wandering hand where it is flat. So the texture of the painting describes the form.
6. **The seasons.** The green is the summer's. Each season lays its own tint over it (spring fresher, autumn golden,
   winter dulled), turning as the grass's season turns; the snow lies over it as before.
7. **The other kinds.** The woods' floor, the seams and the young wood keep their painted tiles and take a share of
   the same turn of hue, so a hill reads under a wood too. The river is untouched.
8. **Deeper trees.** With the watercolour on, the painted crowns are drawn through a tint about a fifth deeper, so the
   woods are the darkest thing on the land, as on the reference. Not the winter set: its snow stays white.
9. **A flat valley** (every save from before heights) is the same green at one level, with the brush on the painter's
   hand, the tuft marks, and no pen.

## 4. How

- **The field** (`HillField`, once a world, four texels a tile, never hashed or saved, D335): R the height, G the
  softened steep mask (both A66's), and new **B the crest**: the heights blurred at 1.2 tiles less the heights blurred
  at 3.5, plus three tenths of the same at 3 and 8 tiles, in sixteenths of a level (a level is a sixteenth of a
  tile's rise for drawing). Computed at tile size and upsampled as the others are.
- **The hachures** cannot be traced per pixel, so they are **baked once a world** beside the field
  (`HillHachures`, 8 texels a tile, four bytes). ⚠️ Not a distance, as first written here: a thin line's distance,
  read between texels, never reaches zero on the line, so the pen would be drawn broken or fat. Each texel names the
  **one stroke nearest it** (where it starts, its direction, its length), and the shader works the distance out, so
  the pen is as thin on screen at every zoom as the bank's own. One stroke per third of a tile of lip, seeded on the
  lip (the steep mask's halfway line, on its uphill side), walked downhill a twelfth of a tile at a time until its
  length is spent, the ground stops being steep, or water is met; long (1.25 tiles) and short (0.6) by a hash of its
  cell; drawn tapering to under a third of its width, with a slight bow. Every choice is a hash of the place: no
  random source, the same strokes every time (the lane's rule, D632).
- **The shader** (`valley_terrain.gdshader`): the green replaces `ground_srgb` in `terrain_ground` for the grass's
  kind; the approved grass masks multiply over it as before; the brush is one streak noise read at four turns and
  mixed by the contour's direction; the tuft marks are one hashed mark a cell, turned to the same direction; the
  hachures are read from the bake with the bank's own pen. The pen lies over the snow, as the bank's does; the tuft
  marks under it.
- **The values** in `looks/valley_watercolour.tres` (`Green*`, `Hill*`, `Brush*`, `Tuft*`, `Hachure*`, `TreeTint`).
  A66's `HillLight`, `HillShade` and `HillBank` are deleted with the shading they drove.
- **Behind the watercolour tick, as every piece is.** Off, the map is the systems lane's, byte for byte.
- ⚠️ **Not this piece:** the minimap, the new-game preview and the title backdrop draw from the bake
  (`ValleyTexture`), which is still the parchment's. They will disagree with the green valley until a later piece.

## 5. Limits

- **How dark the ground may go** (`ground-textures.md §3`): the villagers' dots and the map's marks are L\* ≤ 29 and
  must read at 3 : 1, which holds down to about L\* 58 and is refused under L\* 50. The reference's deepest greens are
  about L\* 47, so the game's are lifted: **measured on a render, the grass's darkest 1 % stays at L\* 55 or above**
  in summer, and the mean is written down.
- **Cost:** measured paired, tick on, before and after, at 4K (`measure4k.gd`), and written down.
- **Determinism:** no time, no frame count, no unseeded random; every noise a hash of a world cell.

## 6. What proves it

- The probe's `hill edges:` line stays, and a new `green ground:` line asks: the field's third channel on a made-up ridge
  (positive on the crest, negative at its foot, zero far away); the hachure bake on a made-up bank (strokes below the
  lip, none above it, none on flat ground, the same bytes twice); the look's values handed over; a flat valley
  drawing no pen; the trees' tint on with the tick and white without.
- Watercolour off byte-identical at four zooms against the build before.
- Renders of the founding valley (Rolling) and a Hilly one beside the mock and the reference.
- Red-check: every new guard, the reds counted, every zero written down.

## 7. As built

**A68, 2026-11-13.** `valley_terrain.gdshader` (the green, the brush, the tuft marks, the hachures; A66's light, shade
and bank removed, its lip kept), `VillageMap.Watercolour.cs` (`HillCrest`, `HillHachures`, the field now three floats a
texel, the values handed over, the probe's `green ground:` line), `VillageMap.Meshes.cs` (the crowns' tint),
`ValleyWatercolourLook.cs` with `looks/valley_watercolour.tres` (36 new values, three deleted), `Main.cs` (the probe's
print). Nothing in `Bclone.Sim`, `tests/` or `data/`.

**What changed from the mock Joe picked, and why:**
- **Lighter than the mock.** The mock's hollows were about L\* 47 (the reference's own), under §5's floor. The palette
  was lifted twice on renders until the valley floor measured **L\* 59.7 at the median** (Rolling, the opening zoom;
  the villagers' dots at about 3.1 : 1) and, beside a Hilly bank, a median of 58.2 with the darkest 1 % at **54.3** —
  ⚠️ a hair under the 55 aimed for, over the 50 refused. So the game is a fresher, lighter green than the mock and the
  reference. Deeper would need the dots to change (a light rim), which is not this piece.
- **The woods' floor is the land's green, a little deeper** (`GreenWood`), carrying its painted tile's light and dark.
  The first render left it the pale sage it was, and every wood stood in a pale clearing.
- **The approved grass's patches lie over the green at 0.4 of their strength** (`GreenGrassShare`): at full strength
  they were hard-edged discs on the green. D632's masks and values are untouched.

**Verified.** Watercolour off byte-identical to the build before at four zooms (four different shots). Probe 114 ✅
0 ⛔, `bar height` 151, `tile centres` and `new game` ✅, `done.`; no `SHADER ERROR`. View build 0 warnings. `tests`,
`src/Bclone.Sim` and `data` identical to `main` (no golden moved; 418 lines). Renders: `D:/Projects/brushes/hills/a68_in_game.jpg`.
**Four checks:** suite 2018 passed / 0 failed / 5 skipped in 18 m 40 s (at Joe's word, *"Run it, commit"*); view build 0 warnings; the probe as above, run again after the suite; the goldens' 418 lines identical before and after.

**Cost** (RTX 3070, 4K, `measure4k.gd`, tick on, this build then the build before then this build again, one sitting):
4.25 and 3.99 ms against 3.17 at the opening zoom (**about +0.8 to +1.1 ms**), 3.60 and 3.63 against 3.69 at the other
(no difference outside the noise: the build before's rounds ran 3.32 to 4.23). Tick off 0.72 in all three.

**Red-check:** 14 mutants, **12 red**: the crest upside down; the crest not carried into the field; the strokes walked
uphill; seeds on both edges of a bank; strokes not stopped at water ⚠️; a stroke's length from a random source; the
bake at four texels a tile; the winter crowns tinted; the crowns tinted with the tick off; a value not handed over; the
cool colour a grey; the steep test dropped from the seeds ⚠️. ⚠️ **The two marked scored zero first**, and the guards
were at fault: the made-up bank's water lay a tile clear of every stroke, and the starts were only asked to lie
somewhere on the bank. Both poses were tightened, and both are red. **Two zeros, written down, both draw-time** (the
shader never runs headless): a slope multiplied toward black, A66's way; the tuft marks ignoring the hill. The renders
are their evidence.

**Not done, and known:**
- The minimap, the new-game preview and the title backdrop are still the parchment's (§4).
- Autumn's and winter's tints are first values: only spring was rendered (trap 27).
- Paths, fields and zone washes were chosen against parchment and have not been looked at on the green.
- The brush's streaks and the tuft marks are first values, for his eyes.

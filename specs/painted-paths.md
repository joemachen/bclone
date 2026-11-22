# Spec: Painted paths — the worn tracks painted into the green ground

**Status:** ✍️ **SPECCED (A72, 2026-11-14)** at Joe's pick on a mock (*"B, painted track"*). 🔨 **BUILT on `art` (A72, §7)**; played by Joe (*"looks really good"*) and
the pen lightened at his word (A73, §7); ⭐ **MERGED INTO `main` AT HIS WORD (A74, 2026-11-14: *"Yes, merge it"*,
`2d84c53`)** and **PUSHED** (A75, `origin/main` = `b88e37a`). 🔨 **The trampled trail (§3.5) BUILT on `art` (A82)** at Joe's
*"Build it"* on a mock after his concept sheet 08; played (*"Looks good"*) and ✅ **merged into `main` at his word (A83, merge
`2b41cb5`)** and **pushed** (A84, `origin/main` = `a1faeb2`). 🔨 **A track across a crop field (§3.7) BUILT on `art` (A126,
2026-11-19)** at Joe's *"the paths on/in crop fields need to be given the proper styling like the rest of the paths in the game"*
(2026-11-16), the three map files at his *"Yes, edit them"*; ⭐ **merged into `main` at his word (A127, *"Looks right, merge"*, merge `a9dded1`), and pushed (A128, `origin/main` = `5fdc3c7`).** 🔨 **A track wears in and grows back over half a season instead of stepping at the turn — `path-crossfade.md`, built (A169) and merged into `main` at his word (A171, `2b222ae`).** Art lane. View only: where a path runs, how it wears and what it costs are the
sim's (`desire-paths.md`, `path-shapes.md`) and nothing here touches them. Neighbours: `green-ground.md` (the ground the
paths lie on), `terrain-shader.md` (the valley's shader), `art-inventory.md §9` (the paths' row).

---

## 1. Why

The paths were the one thing on the green ground still drawn as the parchment map drew them: flat brown ribbons,
`#8c7457` at 60 % and `#8a7056` at 75 % (D358), which on the green read as mud. In both of Joe's references a track is
pale dirt with a pen line down each side; in his concept the village's lanes are cobbled.

## 2. Joe's call (A72)

Three mocks on his own Hollowmere (Hilly, Spring, Year 85, rendered in the game from a copy of his save, the paths
painted from the game's own network): **A** an inked lane (the mesh recoloured, a pen line each side, flat); **B** a
painted track (in the ground, the dirt mottled like watercolour, the grass yellowed where feet stray, the river bank's
pen down each side); **C** B with the packed ground cobbled, as the concept. Sheet: `D:\Projects\brushes\paths\paths_options.jpg`.
His answer: ***"B, painted track"***.

⛔ **Why not C, written down so it is not re-proposed by accident:** in a grown village nearly every trail tile is packed
(223 of 236 in the mock), so C cobbles the whole network though the village laid no stone. `DESIGN.md §2.6` keeps
dirt → gravel → cobble for **paving**, the player's upgrade, which is not built. Cobbles now would tell the player
something that has not happened (§1.1) and leave paving no look of its own. C's cobbles are paving's art when it comes.

## 3. What it draws (with the watercolour tick on)

1. **The track is painted into the ground**, on the land under the fields, trees, buildings and everything standing,
   along the same lanes the mesh draws (`TrailNetwork`, D581–D607): the same centre lines, the same widths (worn half
   0.14 of a tile, packed 0.17), the same tapered dead ends and the same wider junctions. *Where* a path is drawn does
   not change at all; only how it looks.
2. **The dirt:** a pale sand wash, a little deeper on packed ground (`PathWorn`, `PathPacked`), mottled like a
   watercolour wash and finely granulated, the pigment pooling a touch darker just inside its edge. Its edge wanders a
   little, so the two sides of a lane are never ruled parallel.
3. **The grass gives way:** a patchy halo of drier, yellowed grass a little beyond the edge, where feet spill off the
   track — stronger beside packed ground.
4. **Ruts:** two faint darker streaks along a packed track (carts run there since D587).
5. **The pen:** the river bank's ink (`InkColour`), one line on each side, swelling and thinning with the pen's
   pressure and lifting now and then, constant on screen at every zoom. On packed ground only since A82 (§3.5:
   `PathPenWornShare` 0); faded out when the valley is small on screen (a track a few pixels wide would be all pen).
5a. ⭐ **The trampled trail (A82)** — Joe's concept sheet 08 (`art/concept art/08. Path and road tiers.jpg`) as a progression
   the sim's two wear grades step through (`desire-paths.md`: grass → worn → packed): **lightly worn ground is tier II, a
   faint trampled trail** — the grass flattened and yellowed toward the halo's colour rather than bare (`PathTrail` x), the
   earth showing only in broken flecks (`PathTrail` y–w) along two foot-lines (`PathTrailFeet`), no rim and no pen; **packed
   ground is tier III, the worn footpath**, as A72 drew it (bare dirt, rim, ruts, pen). A young village's paths are mostly
   lightly worn and so mostly faint — the routes barely walked look it; an old one's are mostly packed (Joe's year-1 village:
   94 lightly worn segments to 17 packed; Hollowmere, year 85: 52 to 1,026). **Not in this step:** tier IV (a rutted cart
   track needs the sim to say which lanes carts use — a systems ask) and tier V (cobbles wait for paving, `DESIGN.md §2.6`).
6. **Winter:** the snow lies thinner on a track (trodden), so the network still reads, and the pen lies over the snow
   as the bank's does.
7. ⭐ **Across a field (A126)** the crop is cut where a track runs and **the valley's own painted track shows through** —
   one track, one shader, the same on a field and off it. Until A126 a lane across a field was D512's mesh laid over the
   crop in the track's sand at 85 %: one flat colour and a ruled edge, the *"pale smooth band"* in Joe's Year 35 and 38
   screenshots of Thornfield. Now: **packed**, the crop is cleared to the track's wandered edge and a little past it, to
   where the pen's ink is still full, so the pen shows and the wheat meets it; **lightly worn**, the crop is thinned
   (`FieldCut` x, 0.7 of it taken), a trampled lane through the wheat with the valley's trodden grass and flecks showing,
   as tier II reads on the grass. The crop's cut edge pools a little darker in the fields' own edge colour (`FieldCutPool`),
   as a field's outer edge does (A61). In winter the field's snow is cut the same way, so the track's thinner snow shows.
8. **Tick off:** the mesh, and the field overlay over the crop, exactly as before — byte for byte.

⚠️ **The before/after line** (`ShowBeforeAfter`) shows the plain bake on its left side; with the paths in the ground, that
side shows no paths. A comparison tool for the ground, so accepted and written down.

## 4. How

- **The path field** (`PathField`, `VillageMap.Watercolour.cs`): a texture over the whole map at **8 texels a tile**,
  RGB8, never hashed or saved (D335). Each texel holds, for the lane feature whose edge is nearest: R the **signed
  distance to the track's edge** (tiles, −0.25 to +0.6, inside negative), G the **half-width** there (thousandths of a
  tile, for the ruts), B **packed** (0 or 255). Built by walking every lane segment, single-point lane and junction disc
  over the texels within reach of it and keeping the one with the least signed distance — so a junction's wider disc
  wins where it is wider, exactly as the mesh's disc covers its lanes. A straight lane's distance is linear across a
  texel, so the shader's linear filter reads the edge exactly; the pen's distance in pixels is the signed distance times
  the zoom.
- **Rebuilt only when the network is** — once a collection, when the paths' generation or the walls move, never a frame
  (CLAUDE.md, D338). Keyed on the network object itself: every build makes a new one.
- **The shader** (`valley_terrain.gdshader`): the halo and the dirt after the green ground's brush and tuft marks (the
  track covers the marks), the snow thinned over it, the pen with the other inks over the snow and under the paper grain.
  All noise world-anchored hashes, as everywhere in the valley.
- **The values** in `looks/valley_watercolour.tres` (`Path*`, `FieldCut*`). **With the tick on the trail mesh is not drawn**,
  and neither is the field-lane overlay (D512) while the fields are on their layer (A126).
- **The fields' layer (A126):** a `canvas_item` material shades everything its item draws (HANDOFF art trap 19), so the
  crop cannot be cut on the map's own canvas item. With the painted crops, the fields — every fill as its crop's painting,
  the edges' wash and the snow — are drawn on their own layer, a `Control` behind the map (`ShowBehindParent`), added after
  the valley's layer, under `valley_fields.gdshader`. With the tick on the fields were already the first thing the map drew
  (the trails are in the ground), so the order of everything else is unchanged. Laid from the same cached fills
  (`_fieldFills`, `_fieldEdges`) whenever the map draws; nothing derived anew. ⛔ **The cut is the track's own edge to the
  pixel:** the shader reads the valley's path field texture and computes the tile, the signed distance and the wander
  (`at_value_noise(tile / 0.9, 130u)`) line for line as `valley_terrain.gdshader` does — change one and change the other.

## 5. Limits

- **The villagers' dots** (L\* ≤ 29) on the sand (L\* about 77): about 5 : 1, better than on the grass.
- **Cost:** measured paired at 4K (`measure4k.gd`), tick on, before and after, and written down.
- **Determinism:** nothing in the sim reads it; no time, no unseeded random.

## 6. What proves it

- The probe's `painted paths:` line: on a made-up network — a straight packed lane, a worn lane with a dead end and a
  junction — the field says inside on the lane's middle and outside a tile off it, packed on the packed lane and not on
  the worn, the dead end narrower than the lane, the junction wider; the same bytes twice; an empty network bakes
  nothing; and on the probe's own valley, with the tick on, the field built for the network on screen, the values handed
  to the shader, and the mesh not drawn.
- Watercolour off byte-identical at four zooms against the build before.
- Renders of Joe's Hollowmere beside the mock he picked.
- Red-check: every new guard, the reds counted, every zero written down.

## 7. As built

**A72, 2026-11-14.** `VillageMap.PaintedPaths.cs` (new: `PathField`, the bake and its hand-over, `PathsArePainted`, the probe's
`painted paths:` line), `valley_terrain.gdshader` (the halo, the dirt, the ruts, the snow thinned, the pen), `VillageMap.cs`
(`DrawTrails` hands the field over and draws no mesh with the paths painted; the field overlay re-laid when that changes),
`VillageMap.Meshes.cs` (the field overlay in the track's sand), `VillageMap.Watercolour.cs` (the look handed over),
`ValleyWatercolourLook.cs` with `looks/valley_watercolour.tres` (15 `Path*` values), `Main.cs` (the probe's print).
Nothing in `Kith.Sim`, `tests/` or `data/`.

**As the mock, with two differences:** the pen is the bank's own (`valley_pen`: pressure, lifts, feathered edge, constant on
screen) where the mock drew a plain line, and it fades out below 14 px a tile. The field is 960 × 640 for the config's
120 × 80 map (1.8 MB) and bakes in about 2 ms.

**Verified.** Watercolour off byte-identical to the build before at four zooms on Joe's Hollowmere (Spring, Year 85, a copy
of his save; the debug readout's two lines masked, trap 29; the build before rendered twice, identical to itself, and the
four zooms different from each other, trap 25). Renders with the tick on, spring and winter, beside the mock he picked.
Probe: `painted paths:` ✅, 117 ✅ 0 ⛔, `bar height` 151, `tile centres` and `new game` ✅, `done.`. View build 0 warnings.
**Four checks:** suite 2024 passed / 0 failed / 5 skipped in 15m59; view build 0 warnings; the probe as above; goldens 418
lines, `src/Kith.Sim`, `data` and `tests` identical to `main` (no golden moved).

**Cost** (RTX 3070, 4K SubViewport, Hollowmere opened from the save copy, tick on, this build / the build before / this
build, one sitting, three rounds each): 5.27 / 5.34 / 5.35 ms at the opening zoom, 5.26 / 5.35 / 5.57 at the other — **no
difference outside the noise**: the painted track replaces the mesh at about its price.

**Red-check:** 14 mutants, **14 red**, every one with a ⛔ line (none a crash): packed lanes at the worn width; no taper;
junctions not baked; the nearest centre line winning instead of the nearest edge; every texel packed; texels half a texel
off; the distance stored without its offset; the half-width not stored; painted without the tick; the field never switched
on; a look value not handed over; the worn pen as strong as the packed; the look switched off; the field never baked.
**Zeros, written down, all draw-time** (the shader never runs headless): the halo, the mottle and granulation, the rim, the
ruts, the edge's wander, the pen's fade, and the snow thinned on a track. The renders are their evidence.

**Not done, and known:**
- The before/after line's left side draws no paths with the tick on (§3).
- Every `Path*` value is a first value, for Joe's eyes (the pen lightened once, A73 below).
- The minimap, the new-game preview and the title backdrop are still the parchment's (`green-ground.md §4`).

**A73, 2026-11-14 — the pen lightened.** Joe, playing A72: *"looks really good, but like you said it does look a bit
heavy/ragged. lighten up a tough?"* `PathPenStrength` 0.85 → 0.55, `PathPenWidthShare` 0.65 → 0.5, `PathPenFadePxPerTile`
14 → 18. The look file only; probe green, view build 0 warnings; A72's suite stands.

**A82, 2026-11-14 — the trampled trail (§3.5).** Joe added his concept art (A80–A81) and said sheet 08 bears on the path
look; mocked in-game on his year-1 village and Hollowmere (`D:\Projects\brushes\paths\paths_progression.jpg`), then
*"Build it"*. Changed: `valley_terrain.gdshader` (lightly worn ground the trail; two uniforms), `VillageMap.PaintedPaths.cs`
(the hand-over; the probe's value and look checks), `VillageMap.Watercolour.cs` (the uniform list), `ValleyWatercolourLook.cs`
with `looks/valley_watercolour.tres` (`PathTrail`, `PathTrailFeet`; `PathPenWornShare` 0.5 → 0). Nothing in `Kith.Sim`,
`tests/` or `data/`. **Verified.** Renders on his year-1 village and Hollowmere, before and built (`D:\Projects\brushes\paths\paths_trail_built.jpg`); tick off byte-identical to A77 at four zooms, spring and winter (readout masked); probe 118 ✅ 0 ⛔, `painted paths:` ✅, bar 151, tile centres and new game ✅, `done.`; view build 0 warnings; suite 2024 / 0 / 5 in 16m18; goldens 418 lines; sim, data, tests identical to `main`. **Red-check:** 7 mutants, 6 red of 6 guards (the trail and the feet not handed over, the feet undeclared — red because the shader then fails to compile — the grass turned all the way, the flecks' band backwards, a foot-line off the lane); one draw-time zero written down (the trail drawn as bare dirt again) — the renders are its evidence. **Cost** not measured: one noise read and two exponentials a path pixel.

**A126, 2026-11-19 — a track across a crop field (§3.7).** Joe, 2026-11-16: *"the paths on/in crop fields need to be given
the proper styling like the rest of the paths in the game"*; 2026-11-19: *"art lane should start with the crop-field paths"*.
The fix needed three map files from the systems lane's column of HANDOFF's table; asked first, and Joe answered through the
coordinator: *"Yes, edit them"* — `VillageMap.cs` (`DrawWorkedGround` only), `VillageMap.Watercolour.cs`, `VillageMap.Meshes.cs`
(untouched in the end: the overlay is still built, for the tick off, and only not drawn over the fields' layer, which is a
`DrawWorkedGround` line). Changed: `valley_fields.gdshader` (new), `VillageMap.Watercolour.cs` (the fields' layer, its hand-over,
the probe's half), `VillageMap.cs` (`DrawWorkedGround`: the fields to their layer, the overlay not drawn over it),
`ValleyWatercolourLook.cs` with `looks/valley_watercolour.tres` (`FieldCut` 0.7 / 1, `FieldCutPool` 0.22 / 0.06 tiles).
Nothing in `Kith.Sim`, `tests/` or `data/`.

**Verified.** Renders on Joe's Thornfield (a copy of his Year 38 autosave, the village of his screenshots), spring and winter,
both builds from the same save copy (`D:\Projects\brushes\paths\fields\field_paths_before_after.jpg`); the lab's made-up field first (`D:\Projects\brushes\paths\fields\lab_cut.png`). Watercolour off byte-identical to the
build before at three zooms (only the debug readout's timing line differs, trap 29; the zooms differ from each other, trap 25).
Probe 129 ✅ 0 ⛔ (`watercolour:` carries the fields' half), bar 183, tile centres and new game ✅, `done.`; view build 0
warnings; suite 2119 passed / 0 failed / 5 skipped in 16m58 (renders overlapped part of it); goldens 438 lines before and after,
`src/Kith.Sim`, `data` and `tests` identical to `main`. **Red-check:** 9 mutants, **7 red of 7 guards**, each on its own ⛔
(the layer not behind the map; added before the valley's; the shader not compiling; the cut not the look's; the look's cut put
back to the old band's 0.85; the path field not handed over; the layer left showing with the tick off). **Two draw-time zeros,
written down:** the flat overlay drawn over the cut again, and a worn track cut as hard as a packed one — the renders are their
evidence. ⚠️ **Found in the renders, fixed before commit:** cut out to the end of the pen's fade (half its width and a pixel),
a pale line of the ground showed between a packed track's pen and the wheat; the cut stops where the pen's ink is still full
(a quarter of its width) — HANDOFF art trap 57.

**Not done, and known:** the cut values are first values, for Joe's eyes; the before/after line's left side cuts nothing (no
painted track there to show); GPU cost not measured (one texture read and one noise a field pixel near a track, and the fields
drawn as before).

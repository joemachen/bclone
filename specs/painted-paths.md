# Spec: Painted paths — the worn tracks painted into the green ground

**Status:** ✍️ **SPECCED (A72, 2026-11-14)** at Joe's pick on a mock (*"B, painted track"*). 🔨 **BUILT on `art` (A72, §7)**,
⏸️ for his eyes in his own game; not merged. Art lane. View only: where a path runs, how it wears and what it costs are the
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
   pressure and lifting now and then, constant on screen at every zoom. Full on packed ground, faint and more broken on
   lightly worn ground — so worn and packed still read apart (`desire-paths.md`: grass → worn dirt → packed trail).
   Faded out when the valley is small on screen (a track a few pixels wide would be all pen).
6. **Winter:** the snow lies thinner on a track (trodden), so the network still reads, and the pen lies over the snow
   as the bank's does.
7. **Across a field** the lane is still the mesh laid over the crop (D512), in the track's sand with the tick on.
8. **Tick off:** the mesh, exactly as before — byte for byte.

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
- **The values** in `looks/valley_watercolour.tres` (`Path*`). **With the tick on the trail mesh is not drawn**; the
  field-lane overlay (D512) is laid in `PathWorn` / `PathPacked`.

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
Nothing in `Bclone.Sim`, `tests/` or `data/`.

**As the mock, with two differences:** the pen is the bank's own (`valley_pen`: pressure, lifts, feathered edge, constant on
screen) where the mock drew a plain line, and it fades out below 14 px a tile. The field is 960 × 640 for the config's
120 × 80 map (1.8 MB) and bakes in about 2 ms.

**Verified.** Watercolour off byte-identical to the build before at four zooms on Joe's Hollowmere (Spring, Year 85, a copy
of his save; the debug readout's two lines masked, trap 29; the build before rendered twice, identical to itself, and the
four zooms different from each other, trap 25). Renders with the tick on, spring and winter, beside the mock he picked.
Probe: `painted paths:` ✅, 117 ✅ 0 ⛔, `bar height` 151, `tile centres` and `new game` ✅, `done.`. View build 0 warnings.
**Four checks:** suite 2024 passed / 0 failed / 5 skipped in 15m59; view build 0 warnings; the probe as above; goldens 418
lines, `src/Bclone.Sim`, `data` and `tests` identical to `main` (no golden moved).

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
- The pen at the closest zoom is heavy and ragged; its values (`PathPen*`) and every other `Path*` value are first values,
  for Joe's eyes.
- The minimap, the new-game preview and the title backdrop are still the parchment's (`green-ground.md §4`).

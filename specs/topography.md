# A topography layer, off by default (A265–A266)

**Status: ✅ built on `art` (A267), ⏸️ Joe to play it. Not merged.** Art lane, branch `art`.

Joe's play note 3 (2026-11-24): *"a topography layer that is off by default, but can be toggled on in settings like the grid
is? or can the grid be turned into a topographic map?"*

## 1. What is there

- **The sim has real heights** (D735–D742): 256 levels a tile, smoothed, from the seed; any slope slows the walk; steep ground
  refuses a building (D814).
- **The everyday look tells them by the grass's own colour** (A68, the green ground: *"Looks good"*). Four darker washes were
  refused as shadows (D738–D741, A66–A67), and **contour lines were refused as the everyday look** — *"oh, no i hate that"*
  (D740). This is not that: it is a layer he switches on to read the land, and off again.
- **The valley's shader already holds the heights** (`heights_map`, A66: R the height, G the steep mask, B crest and hollow),
  baked once a valley. A layer drawn from it costs no new baking and nothing per frame on the CPU.

## 2. The mocks (2026-11-25)

Scratch copy `D:\Projects\brushes\topography\` (`mock.py` patches the valley shader behind a `topo_mode` uniform; `render.gd`,
`run.sh`, `sheets.py`). Copies of his saves; the HUD hidden.

- `topo_1_thornfield.jpg` — Thornfield (frost-mossy), heights to 255, his hilliest; `topo_2_elderwood.jpg` — Elderwood
  (thistle-copse), Year 40; `topo_3_millbrook.jpg` — Millbrook; `topo_4_elderwood_close.jpg` — the zoom he builds at;
  `topo_5_under_the_woods.jpg` — where it is drawn.
- **A — contour lines:** a thin sepia line every 10 levels, every 5th heavier; where the lines would crowd into a smear (a
  steep bank, zoomed out) the plain ones fade and the hachures tell the slope.
- **B — height bands:** the land washed in bands of height (low green → straw → ochre), a line where each meets the next.
- **C — the grid as a map:** the grid on, each tile tinted by its own height band — his *"or can the grid be turned into a
  topographic map?"*.
- **In all three, steep ground is hatched** (short strokes down the slope) — the ground nothing can be built on.
- Found mocking: a village founded where the sim sets it — low and level (D736) — shows few lines close in; that is the land
  telling the truth, not the layer failing.

## 3. Where it is drawn

On the ground, as the grid is: under the woods, the buildings and the people, over the water nothing. A wood hides the lines
under it (`topo_5`); the *draw the trees* tick shows them. Drawing it over the woods would draw it over the buildings and the
people too (the canopy is over them, A36), so it is not mocked.

## 4. Joe's calls

1. **The look — A, B or C.** Recommended **A**: it is what a topographic map is, the lines crowding on steep ground say
   "steep" without a legend, and it changes no colour (B tints the whole valley; C covers it in tiles).
2. **Its own tick, or the grid's.** Recommended **its own tick**, *"show the land's height"*, off by default, under *On the
   map* beside the grid: the grid answers "where are the tiles?" (placement), the layer "where are the hills?"; with A or B the
   two are separate things. C is the grid's look changed, so C would ride on the grid's tick.
3. **On the ground, under the woods** (as mocked). Recommended yes.
4. **The files outside the lane's table** (the systems lane's): `Main.Settings.cs` — one row in the player's ticks table;
   `Main.cs` — the tick built beside the grid's (about five lines) and the probe's line in its list. The rest is the art
   lane's: the valley shader, `VillageMap.Watercolour.cs` (the uniform) and a new `VillageMap.Topography.cs`.
   ⚠️ It is drawn by the watercolour layer, so with *paint the valley in watercolour* off the layer has nothing to draw on
   (the tick says so in its tooltip).

**Answered (A267, 2026-11-26): *"Yes, as recommended"*** — A, contour lines; its own tick, off by default, beside the grid;
on the ground, under the woods; his OK for `Main.Settings.cs` and `Main.cs`.

## 5. Build (A267)

- **The shader** (`shaders/valley_terrain.gdshader`): `topo_on` (off) and the contours' own numbers as uniform defaults —
  `topo_step` 10 levels, `topo_index` 5 (every 5th heavier, 1.8× as wide), `topo_ink` sepia (0.45, 0.29, 0.16),
  `topo_width_px` 1.3, `topo_strength` .75, `topo_hatch` 1. Drawn after the ink, before the paper's grain: a line's
  distance in levels over the levels a pixel spans, so a line is the same width at every zoom; plain lines fade where they
  would be closer than 4–9 px, index lines under 2–4 px; the hachures on the steep mask (`hill.g`), along the contour,
  dashed down the slope. Only where the hills are baked (`hills_on`) and only over land (`weights.a`).
- **The map** (`VillageMap.Topography.cs`): `ShowTopography(bool)` sets the uniform; `TopographyShown`. A layer built
  after the tick was pressed takes it from `_topography` (`VillageMap.Watercolour.cs`, where the material is made);
  `topo_on` is in the uniforms the watercolour line checks the shader declares.
- **The tick** (`Main.Settings.cs`, `Main.cs`): the row `topography` — *"show the land's height"*, off, *On the map* —
  built beside the grid's, its tooltip saying it is drawn on the watercolour valley; in the view ticks, so the settings
  line's round trip carries it.
- **The probe's `topography:` line**: the row off by default, the tick and the map agreeing, the tick pressed on and off
  moving the map and the shader's `topo_on` with it.

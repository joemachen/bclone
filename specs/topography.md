# A topography layer, off by default (A265–A266)

**Status: ✍️ mocked, ⏸️ Joe's calls (§4). Nothing built.** Art lane, branch `art`.

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

## 5. Build (after his calls)

Not started. Probe line `topography:` (off by default; on, the shader's uniform follows the tick; the setting round-trips),
red-checked.

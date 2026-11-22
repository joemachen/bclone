# Spec: The paths' crossfade — a track wears in, and grows back, rather than stepping

**Status:** ✍️ **SPECCED AND MOCKED (A168, 2026-11-21).** ✅ **His four calls answered (A169, 2026-11-22: *"All as
recommended"*) — C by patches, half a season, the same pace both ways, no fade on a load.** 🔨 **BUILT on `art` (A169, §7)**, the
two map files at his *"Yes, edit them"*; played, and ✅ **merged into `main` at his word (A171, *"Looks good, merge it"*, merge `2b222ae`); not pushed.** Art piece (a) of his play
notes of 2026-11-20 (*"paths change too abruptly"*), moved ahead of the bridge at his word on 2026-11-21: *"move path
transition/cross-fade up to the next item and start on that"*. Art lane, **view only**: the sim's wear, its classes, the
routes and the cost field (`desire-paths.md`) are read, never written — no golden can move. Neighbours: `painted-paths.md`
(what a track looks like at each grade), `path-shapes.md` and `TrailNetwork` (where the lanes run).

---

## 1. What happens today

- **The sim** treads wear onto every tile a walker crosses, every tick. **But a tile's grade — grass, lightly worn, packed —
  only changes at a season's turn** (`PathWearSystem`, every 120 ticks): the wear fades by 4, and the class is re-read with
  its hysteresis (`desire-paths.md §3.2–§3.4`). `PathWear.Generation` moves at every turn.
- **The view** re-reads the grades when that generation moves, or when a fence goes up or down (`CollectTheTrailsIfTheyMoved`),
  traces the network of lanes again (`TrailNetwork`, D581), and the painted paths bake a new path field from it
  (`HandThePathsOver`, ~2 ms) — once a turn, never a frame.
- **What the eye sees:** in a single frame at the season's turn, every track that stepped changes at once. A new trampled
  trail appears whole; a trail that packed turns to bare dirt with its pen in one go; a track falling out of use vanishes or
  loses its dirt. And because the network is **re-traced** from the new grades, a lane can also **move** — in the mock's
  village (Larkspur, year 1, the turn to summer) one lane from the field gate disappears while a straighter one appears beside
  it, all in one frame. At Joe's usual 4× that is one jump every 40 seconds; at 1× every 160.

## 2. What the crossfade does

When a new network arrives, the valley **blends from the field it was painting to the new one over a stretch of game time**,
instead of switching.

- **What is blended is the path field, not two pictures** (`shaders/path_fade.gdshaderinc`): its signed distance to the
  track's edge, its half-width and its *packed* share are mixed between the old field and the new, and the track is painted
  once from the mix. So **a new lane widens out from its own line**, its yellowed halo first and then the trodden ground; **a
  lane falling out of use narrows back into its line and is gone**; **worn to packed**, the trodden grass turns to bare dirt and
  the pen comes in; packed to worn the other way.
- **By patches (recommended, call 1 C):** the moment each place changes is offset by a soft world-anchored noise (patches about
  a tile and a half across), so the change soaks in unevenly along a lane, as a wash does — never the whole lane at one pace.
  Every place still ends exactly on the new field.
- **In game time**, from the tick the network changed: pausing holds a fade where it is, and the game speed scales it with
  everything else. **Half a season** recommended (call 2): 60 ticks — 80 s at 1×, 20 s at 4×, 4 s at 20×. A fade always ends
  before the next turn.
- **Both ways alike** (call 3): a track fading out of use blends at the same pace as one wearing in.
- **No fade when the paths have not changed** (a turn whose new field is byte for byte the old: nothing is uploaded, and a fade
  already running runs on), **on loading a save or founding** (the first field is drawn as it is), or **with the watercolour
  off** (the parchment's trail mesh steps as before, byte for byte). **Switched back on**, the field is drawn as it is too
  (A169): it was not kept up while the tick was off, so a fade from it would wear in paths that changed seasons ago.
- **Where the numbers live:** `PathFadeTicks` (60) and `PathFadePatch` (0.6) in `looks/valley_watercolour.tres`
  (`ValleyWatercolourLook`); 0 ticks steps as before.
- **The crop's cut moves with the track** (`painted-paths.md §3.7`): `valley_fields.gdshader` includes the same blend, so a
  track across a field wears in with the cut beside it, to the pixel.
- **A fence mid-fade** (the network rebuilt again inside the 60 ticks): what was on screen is baked into the "from" field
  once, at that moment, and a new fade starts from there — so nothing jumps back. ⚠️ The patches are not in that bake (it is
  the even blend), so a place mid-patch can shift a little then. Rare and small; written down.

## 3. What it costs

- **Outside a fade: nothing.** The shader reads the second field only while the fade runs (a uniform branch, the same for
  every pixel).
- **During a fade:** one more texture read a pixel, and a noise only where a track is near in the old field or the new — past
  the field's reach in both (most of the valley) the include returns at once (A169). **Measured at 4K (A169), the two shaders
  swapped on one material inside one run** (runs do not compare, traps 8 and 22): with no fade running, +0.01 ms against the
  build before's shader (floors 3.805 → 3.818 ms); a fade held at half, +0.02–0.06 ms.
- **Memory:** a second path texture, 960 × 640 RGB8 (1.8 MB of video memory), and the two fields' bytes kept on the CPU
  (3.7 MB).
- **CPU, once a turn:** the bake as today (~2 ms), plus comparing the new field with the old (1.8 MB) to skip a fade that
  would change nothing. **No copy in the usual case:** the two textures swap roles — the one that showed the old field
  becomes the one faded from. Mid-fade only, one byte blend of the two fields (~2 ms).
- **CPU, a frame, while a fade runs:** two numbers to two materials from the game clock — the season's and snow's pattern.
  ⛔ Nothing derived is rebuilt per frame (CLAUDE.md): the fade's state (the two fields, the tick it began) is kept where the
  network changes, and a frame only reads the clock.

## 4. Determinism and the sim

Nothing in `Kith.Sim`, `tests/` or `data/` changes. The fade reads `world.Tick` and the view's interpolation, as the snow and
the trees' seasons do; the sim reads nothing of it; it is never hashed or saved. **No golden moves** — checked by the goldens
grep and `git diff` of `src/Kith.Sim`, `data`, `tests` against `main`.

## 5. The mock (A168)

Rendered by the game's own shader from a scratch prototype (uncommitted; `D:\Projects\brushes\paths\crossfade\prototype.patch`)
on a copy of Joe's `larkspur-heron-flax-99/year-2.save` (year 1, eight ticks before the turn to summer): run at 20× to the
turn, frozen, the fade then shot at thirteen fixed steps. **`D:\Projects\brushes\paths\crossfade\path_crossfade_mock.gif`** —
four panels: today (the jump), **A** a plain dissolve (the two end frames mixed), **B** wears in evenly, **C** wears in by
patches; `path_crossfade_stills.jpg` — before, B and C halfway, after, at full size. ⚠️ The GIF runs a fade in about three
seconds; in the game it would take twenty at 4×.

**A is shown and not recommended:** a dissolve is a double exposure — the old lane and the new both half-there as ghosts of
themselves, where B and C read as ground being trodden or growing back.

## 6. Joe's calls — ✅ answered, all as recommended (A169, 2026-11-22: *"All as recommended"*)

1. **The look:** A dissolve, B wears in evenly, or **C wears in by patches (recommended)** — ✅ **C**, patchiness 0.6
   (`PathFadePatch`).
2. **How long a step takes:** a quarter season (10 s at 4×), **half a season (recommended, 20 s at 4×)**, or a whole season
   (40 s at 4×; the picture is then always still catching up with the last turn) — ✅ **half a season, 60 ticks**
   (`PathFadeTicks`).
3. **Backwards:** **the same pace both ways (recommended)**, or grass growing back over a dead lane twice as slowly as a new
   one wears in — ✅ **the same pace** (one blend; nothing to tell the two apart).
4. **A save opened or a village founded:** **no fade, the paths as they are (recommended)** — ✅ **no fade**; nor when the
   watercolour tick comes back on (§2: the field is not kept up while it is off, so it would fade from a stale one).

## 7. What proves it (A169, built)

- **The probe's `painted paths:` line** (`ThePathFadeHolds`), on the probe's valley with the tick on, two made-up fields handed
  over in turn: the look's fade 60 ticks (the spec's number, not the file's — trap 25) inside the season, patchiness inside
  0–1; a new world (a load) drawn as it is; the same bytes again — no fade, nothing uploaded; a change — the texture that
  showed the old field is the one faded from and the new is in the other (the swap, no copy), both on the valley's layer and
  the fields', `path_fade` 0 at the look's patchiness; through the frame's own call, ½ halfway, the same again at the same
  game time (a pause holds it), 1 at the end; a change mid-fade — the "from" field baked once as the blend shown there; the
  tick off and on again — the valley's own field drawn as it is. **`watercolour:`** — both shaders declare the fade's three
  uniforms (the map's lists now 159 and 16; a shader that does not compile declares none, trap 6).
- **Red-check: 12 of 14 red.** Red: no swap; a new world fades; an unchanged field fades; the clock at half speed; no mid-fade
  bake; the fields not handed the fade; the patchiness not handed; a frame never advancing the fade; the tick off keeping the
  stale field; the look's 30 ticks; each shader without the include. **⚠️ Two zeros, both draw-time and expected:** the
  terrain shader reading `path_map` without the blend, and the include's blend returning the new field — the probe never
  draws (D340); the renders below are their check.
- **Byte-identical to the build before** (f5c494c) on Joe's Larkspur year-2 save, paused, four zooms (each different from the
  others): watercolour off, and on with no fade running — every changed pixel inside the debug readout's timings line.
- **Renders through a turn** (Larkspur, eight ticks before summer, 20×, Godot's movie mode at 20 fps so every frame is the
  same game time): the lane to the field gate thins away by patches while the straighter one wears in, over the 60 ticks —
  `D:\Projects\brushes\paths\crossfade\built\path_crossfade_built.gif` and `path_crossfade_built_stills.jpg`.
- **GPU at 4K** (§3): +0.01 ms outside a fade, +0.02–0.06 ms during one.

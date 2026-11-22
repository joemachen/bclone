# Spec: The paths' crossfade — a track wears in, and grows back, rather than stepping

**Status:** ✍️ **SPECCED AND MOCKED (A168, 2026-11-21) — ⏸️ FOUR CALLS JOE'S (§6); NOTHING BUILT.** Art piece (a) of his play
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
- **No fade when the paths have not changed** (a turn whose new field is byte for byte the old: nothing runs), **on loading a
  save or founding** (the first field is drawn as it is), or **with the watercolour off** (the parchment's trail mesh steps
  as before, byte for byte).
- **The crop's cut moves with the track** (`painted-paths.md §3.7`): `valley_fields.gdshader` includes the same blend, so a
  track across a field wears in with the cut beside it, to the pixel.
- **A fence mid-fade** (the network rebuilt again inside the 60 ticks): what was on screen is baked into the "from" field
  once, at that moment, and a new fade starts from there — so nothing jumps back. ⚠️ The patches are not in that bake (it is
  the even blend), so a place mid-patch can shift a little then. Rare and small; written down.

## 3. What it costs

- **Outside a fade: nothing.** The shader reads the second field only while the fade runs (a uniform branch, the same for
  every pixel).
- **During a fade:** one more texture read and one noise a pixel where the paths are painted, for the fade's 60 ticks.
  *(To be measured at 4K paired against the build before, as every painted piece is.)*
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

## 6. Joe's calls

1. **The look:** A dissolve, B wears in evenly, or **C wears in by patches (recommended)**.
2. **How long a step takes:** a quarter season (10 s at 4×), **half a season (recommended, 20 s at 4×)**, or a whole season
   (40 s at 4×; the picture is then always still catching up with the last turn).
3. **Backwards:** **the same pace both ways (recommended)**, or grass growing back over a dead lane twice as slowly as a new
   one wears in.
4. **A save opened or a village founded:** **no fade, the paths as they are (recommended)**.

## 7. What will prove it (when built)

- The probe's `painted paths:` line grows: two made-up networks handed over in turn with the tick on — the "from" texture is
  the old field and the new texture the new (the two swapped, no copy); the fade 0 at the change, ½ halfway, 1 after, held by a
  pause; an unchanged network starts no fade; a new world (a load) starts none; a change mid-fade bakes what was shown; both
  layers declare and hold the fade's uniforms (a shader that does not compile declares none, trap 6).
- Watercolour off byte-identical to the build before at four zooms; on, with no fade running, byte-identical too.
- Renders on Joe's save through a turn, as the mock.
- GPU cost at 4K during a fade and outside one, paired with the build before.
- Red-check every guard; count the reds; write down every zero (the patches' noise and the shader's blend are draw-time).

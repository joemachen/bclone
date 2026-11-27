# The trees' extras: the hand-back and the ground's fade (A300)

> **Status: ✅ BUILT (A300, 2026-11-26), played and merged into `main` at Joe's word (A302, *"Played it, merge"*, `1ce7364`); pushed at his word (A303, *"Push main"*) — at Joe's *"just power through all these, im going to approve them all"*
> (A295): the recommended option taken without a mock.** Art lane, branch `art`; the art queue's piece 6 ("tree extras"). View only. **The ground's fade is built; the crowns' hand-back was
> tried, filmed, measured worse and taken out (§2).**
> ⚠️ **One file outside the lane's table:** `ValleyTexture.cs` gains two read-only accessors and an upload counter (§3) — his review.

## 1. What was asked

The art queue's piece 6, *tree extras*: what the trees' motion (A197, `tree-motion.md`) left switching at once, measured and
written down there as *"what still switches at once"*:

1. **Which crown is on top where two overlap, at a growing's end.** The motion draws over the canopy, the mesh lays trees tile by
   tile; the frame a grown tree goes back into the mesh, a neighbour from a later tile that overlaps it goes back on top — one
   patch of about 30 × 40 px at the closest zoom, 500–700 changed pixels a row.
2. **The ground under the tile.** The valley's painted floor turns from young-wood ground to woods floor (or woods floor to grass)
   in the frame the sim changes the tile — the largest change in the film of a growing (4,600–5,900 px of a 560 × 360 crop).

## 2. The hand-back (1) — ⛔ tried, measured worse, taken out

Tried: a growing's last frame (the mesh's own trees to the vertex, `tree-motion.md §7`) kept drawn over the mesh for half a second
after its trees went back, fading out, so the mesh's overlap order would come in as a crossfade. **Filmed and measured, it made
that frame worse:** the crowns' painted rims and shadows are soft (semi-transparent), and a frame laid over the mesh's own copy of
the same trees doubles their ink — the frame of the hand-back changed **1,161–2,275 px** of the crop where the swap it was meant to
soften had changed **335–541** (`D:\Projects\brushes\trees\extras\handback_diff.png`, the rims lit up around every crown of
the row). Taken out; the motion files are as A197 left them.

**What would work:** a real crossfade of the two orders needs the trees of that tile drawn to an offscreen layer (a
`SubViewport` or a canvas group) and that layer faded — a picture, not a second copy of the trees. A bigger piece than the
swap's half-second patch is worth; **his call if he notices it in play.** The red-check's two mutants for it (M1 no hand-back,
M2 never retired) were red, and go with it.

## 3. The ground's fade (2)

As the paths' field fades (A169, `path-crossfade.md`): **the layer keeps the mix as it was before the change beside the new
one, and blends between them over `GroundFadeTicks` (2.25 ticks — three seconds at 1×, a growing's own length) of game time,
eased.** A pause holds it; the game's speed scales it, as the changes come faster.

- **Kept where the mix changes, never a frame** (CLAUDE.md): the bake's own upload counter (`ValleyTexture.WeightsUploads`) says
  when; then the "from" textures are uploaded once and the bytes kept. A frame only sets `ground_fade`.
- **A change mid-fade** bakes what was on screen into the "from" side once (a byte lerp at the eased fade), so nothing jumps.
- **A new bake** (a load, a founding, the watercolour switched on again after changes while it was off) **snaps**: no fade from
  a stale floor.
- **Shader** (`shaders/terrain_lab.gdshaderinc`, `valley_terrain.gdshader`): `ground_map_from`, `ground_map_shares_from`,
  `ground_fade`; `terrain_weights` and the young/iron shares mix from → now while `ground_fade` < 1, and read only the new
  otherwise. The lab never sets them (fade 1).
- **⚠️ Outside the lane's table — `ValleyTexture.cs`:** `WeightsBytes`, `SharesBytes` (read-only views of what was uploaded) and
  `WeightsUploads` (a counter). Without them the old floor could only be had by reading the texture back from the GPU, a stall
  at every change. Nothing in the bake changes.
- **Not faded:** the plain map (watercolour off) and the before/after split's bake — both as before.

## 4. Checks

- Probe `ground fade:` (`VillageMap.GroundFadeProbe.cs`): the shader declares its three; the curve 0, ½, 1 at 0, 1.125, 2.25
  ticks; nothing changed, nothing fades; a fell fades from the floor as it was (byte for byte) to the new; a change mid-fade bakes
  what showed, once; on again after a change while off, no fade. It prints what a start cost.
- red-checked, 6 mutants: 5 red, 1 zero written down — the hand-back gone (M1) and never retired (M2), both red on `tree motion:` and now taken out with it; the mid-fade bake off (M3), the snap after the watercolour is off and on removed (M4), the fade twice as long (M5), each red on `ground fade:`; **the shader's blend removed (M6) scored zero** — nothing is drawn headless, and the film is its check. Restored by reverse replace, confirmed by grep (`D:\Projects\brushes\trees\extras\red300.log`).
- **Four checks:** suite skipped — no sim, data or test file differs from `a65eff0` (W4's suite, 2363 / 0 / 5, D916); view build 0 warnings; probe 217 lines, 182 ✅, 0 ⛔, `ground fade:` and `tree motion:` ✅, bar height 110, tile centres ✅, new game ✅, `done.`; goldens identical; a fade's start costs about 16 ms, once per change of ground.
- **Film:** A197's growing clip refilmed (movie mode, 20 fps, Oakhaven year 28, 10 tiles a row 5 s apart, `D:\Projects\brushes\trees\extras\`): at each row's turn the floor's one-frame jump fell from 3,123–3,995 changed pixels to 407–549, spread over the next three seconds (`measure300.log`, `ground_fade_before_after.gif`, `ground_fade_frames.jpg`).

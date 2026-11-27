# The trees in a gentle wind (A299)

> **Status: ✅ BUILT (A299, 2026-11-26), played and merged into `main` at Joe's word (A302, *"Played it, merge"*, `1ce7364`); pushed at his word (A303, *"Push main"*) — at Joe's *"just power through all these, im going to approve them all"*
> (A295): the recommended option taken without a mock.** Art lane, branch `art`; the art queue's piece 5. View only. Film:
> `D:\Projects\brushes\animals\sway_built.gif`, `sway_check.jpg` (two frames and what differs: only the crowns).

## 1. What was asked

Joe's play note (i), 2026-11-20 (A137): *"a gentle wind animation to trees that is a degree or two more intense in the winter. think of
how the water animates and the visuals used there."*

## 2. How

The painted trees are cached meshes, one a chunk, built once on the terrain counter and drawn on the map's own canvas item after
the buildings and the people (the canopy over them) and before the overlays. A sway needs moving vertices; re-meshing a frame is
out (CLAUDE.md), and a layer of their own cannot sit between the buildings and the overlays. So:

- **The map's own material**, `shaders/tree_sway.gdshader` (canvas, vertex only): every draw on the map passes through untouched but
  vertices carrying **the wind's mark** — `fract(UV.x × 24) = 0.001`, a 4e-5 nudge of UV.x (`MeshBuilder.SwayMark`), a hair of the
  atlas, never seen. Those lean `sway_tiles` on a slow two-part sway, each tree on its own phase from where it stands.
- **Only the trees' top corners carry it** (`MeshBuilder.Sprite` → `SpriteFrom(…, sways: true)`); the foot stays put, so a trunk
  holds and the crown leans. A deposit's cell never carries it. The trees' motion (A197) lays its falling and growing trees with the
  same mark, so a fall still starts as the mesh's own tree to the vertex. Anything else that happened to carry the mark would move
  a few hundredths of a pixel (its units are pixels; the trees' are tiles).
- **The clock** is the people's (`_peopleClock`): the game's speed capped at 1.5×, still while paused — as the river's flow.
- **A degree or two more in winter:** the lean is 0.035 tiles in leaf and `WinterSway` (1.3) times that bare, crossfaded with the
  season as the atlases are (`SwayFor`). A uniform a frame, never a re-mesh (D338).

## 3. Checks

Probe `tree sway:` (`VillageMap.TreeSwayProbe.cs`): a tree's top corners marked and its foot's not, grown and young, mirrored and
not; a deposit and plain textures never; the lean in leaf, in winter and between; the shader declares its two uniforms. `painted
trees:` reads a cell with the mark taken off its top corners; `tree motion:` holds (the motion's trees carry the mark).

red-checked, 4 mutants, 4 red (no mark on the tops — and `tree motion:` red too; no more lean in winter; the shader's lean renamed; the motion's trees unmarked — on `tree motion:`). **Four checks:** suite skipped — no sim, data or test file differs from `a65eff0` (W4's suite, 2363 / 0 / 5, D916); view build 0 warnings; probe 216 lines, 180 ✅, 0 ⛔, `tree sway:`, `painted trees:` and `tree motion:` ✅, bar height 110, tile centres ✅, new game ✅, `done.`; goldens identical.

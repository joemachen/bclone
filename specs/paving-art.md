# Spec: Paving's pictures — gravel and cobble (W9, art lane)

> **Status: ✍️ MOCKED (A335, 2026-11-28; the unused road's ageing A336), nothing built; ⏸️ six calls Joe's (§4); ✅ the unlocks his (A336, §3.1).** Claimed A334 at Joe's *"W9 paving"* (to
> the coordinator, asked what the art lane should mock ahead while the herbalist's poses wait on `slice/w6-fever`). W9's
> mechanic is **not specced or built** (`systems-complete.md` W9; `DESIGN.md §2.6`): everything here is the look, made ahead,
> with the seams the systems lane will need set out as **its** calls (§3), not decided. Art lane. Neighbours:
> `painted-paths.md` (the worn track this paves over; A72 kept cobbles for paving, §2 there), `desire-paths.md` (wear, the cost
> table paving will extend, §4.4 there), `path-crossfade.md`, `path-shapes.md`.

---

## 1. What exists, and what paving adds

- **Today** (`painted-paths.md`): a path is painted into the ground from the trail network — lightly worn ground a trampled trail,
  packed ground a footpath of pale dirt with a pen down each side, thinner snow on it in winter. The view reads one grade per tile
  (`VillageMap.TrailGradeAt`: 0 unworn, 1 worn, 2 packed), builds lanes (`TrailNetwork.Build`) and bakes the path field
  (`VillageMap.PathField`: R the edge distance, G the half-width, B packed) for `valley_terrain.gdshader`.
- **`DESIGN.md §2.6`:** the player upgrades a path the village has worn in, dirt → gravel → cobblestone, spending stone; a
  paved path stops fading. `systems-complete.md` W9: the player paints over a worn path to pave it, first gravel, then cobble;
  auto-pave later.
- ⛔ **A72 refused cobbles for today's tracks** because they would say the village laid stone when it had not
  (`painted-paths.md §2`): *"C's cobbles are paving's art when it comes."* This is that.

## 2. The mock (A335)

Rendered **in the game** — a scratch copy (`D:\Projects\brushes\paving\game`, never in the repo) whose path shader draws two
stone grades and a stretch being paved, from a second texture marking which tiles are gravel, cobble or ordered — on a copy of
Joe's **Hollowmere (honey-harrow)**, at the closest zoom (60 px a tile) and three clicks out, summer (season 1.5) and winter
(3.6). The square's ring cobbled, two lanes off it gravelled, one path half paved. Sheet:
`D:\Projects\brushes\paving\paving_pick.jpg`. Scripts: `shader_patch.py` (the shader), `VillageMap.MockPave.cs` (the marking
texture and a network dump), `render.gd`, `run.sh`, `compose.py`.

- **Only packed tracks are paved** in the mock, **at the width they wore to** (the packed half-width, 0.17 of a tile): the
  stone goes where the feet went. The pen down each side stays, as on every packed track.
- **Far out** (below ~34 px a tile) the stones fade to the cobbles' average grey and the grit to its wash, so nothing shimmers.

## 3. The seams — the systems lane's calls, for W9's slice (not decided here)

What the view needs, however W9 is built; each is the slice's to name.

| # | The question | What the view does with the answer |
|---|---|---|
| S1 | **How a paved tile is stored.** One grade table extended (0 unworn, 1 worn, 2 packed, **3 gravel, 4 cobble** — `desire-paths.md §4.4`: *"Paving will add classes to the same table, not a second one"*), or a paving layer of its own beside wear (none / gravel / cobble). | Reads the laid grade per tile where it reads wear today (`TrailGradeAt`, `VillageMap.cs`); a paved tile counts as **at least packed** for the network, whatever its wear, so a fossilised road never fades off the map. |
| S2 | **Whether a tile ordered but not yet laid is visible** — a per-tile *planned* mark with its target grade, cleared as each tile is laid. | §4 call 4 A (staked out) needs it; B does not. |
| S3 | **Where the stone waits**: a site or delivery point the paving draws from (as a construction site's materials do), or none. | The heap of stone at the work's front is drawn there; with none, no heap. |
| S4 | **Which tiles can be paved**: packed only, or any worn tile. | A lightly worn tile is drawn as a narrow trail; paved, it is drawn at the packed width. |
| S5 | **The change counter.** Paving a stretch and laying each tile move `PathWear.Generation` (or a counter beside it the view also watches). | The path field is rebaked once per change, never per frame (`CLAUDE.md`; `painted-paths.md §4`). |

| S6 | **How used a paved tile still is.** Recommended: wear keeps counting and fading under the stone — paving stops it **removing** the path, not stop it being measured — so the view ages a paved tile whose wear has faded (§4 call 6), and a road walked again comes back clean. No new data. | The ageing: grass in the joints, moss, the grit greening, the edges softened — never gone. |
| S7 | **Taking paving up**: whether the player can lift a stretch (some stone back), as a deliberate act. Recommended: yes, never on its own. | The stretch goes back to the worn track it was, by the crossfade (`path-crossfade.md`). |
| S8 | **Building over it**: whether a paved tile can be built on, or must be lifted first. Recommended: lifted first. | Nothing new: a lifted tile is a track again. |

The **grade names** the view will key on, whatever S1 says: *gravel*, *cobble*, and *planned* (S2).

### 3.1 Joe's answers on W9's design (A336) — for the slice, written here so they are not lost

Joe, asked how paving fits paths that change with the village: *"yes, i love all of this"* — the fossil rule as `DESIGN.md §2.6` has it (a paved road stays, and the cap on its discount means people still wear new dirt tracks beside it when it no longer goes where they go), the unused road ageing (§4 call 6), S6–S8 asked of the slice. Then on the unlock: ✅ **Joe: *"gravel unlocked once a quarry exists and cobble once the village has a master builder." approved*** — gravel is the quarry's chippings; laying cobbles is what a master builder knows (`DESIGN.md §2.7`, knowledge in a person: with no master builder left the village can still gravel, and the cobbles already laid stay), which also gives the builder's skill its first use (`skills-for-every-trade.md §6`). ⏸️ **Who lays it — recommended, not yet answered: the builders**, a paving order being a site along the path, its stone hauled to it like any site's (which is S3's answer: the heap is the site's stone).

## 4. The calls — Joe's

1. **Gravel** — ⭐ **A fine pale grit**, a step up from the dirt and a step down from the cobbles; or **B pebbles**, darker and
   coarser, which sit close to the cobbles (from across the map the two stone grades blur).
2. **Cobble** — ⭐ **A cobbles to the edge**, the pen down each side; or **B kerbed**, a row of larger darker stones along each
   side (subtle at the closest zoom, gone further out). Winter either way: **snow in the joints, the stones showing** — the
   network still reads, and a cobbled street reads as cobbled.
3. **Where one grade meets the next** — ⭐ **A clean**: the stones end in a line where the paving stops (dirt to gravel the same);
   or **B feathered**: stones and grit stray across the join, ragged by hand. A says exactly where the player paved to.
4. **Being paved** — ⭐ **A staked out**: the ordered stretch marked with pale string and pegs down both sides, a heap of stone at
   the work's front, each tile turning to stone as it is laid — as a building site shows its outline before it is built; or
   **B unmarked**: only the heap and a few strayed stones (needs neither S2 nor anything on the waiting stretch).
5. **The files, when built** (his OK): the art lane's `valley_terrain.gdshader`, `VillageMap.PaintedPaths.cs`,
   `ValleyWatercolourLook.cs` and `looks/valley_watercolour.tres`, a probe line; and in the systems lane's column,
   **`VillageMap.cs`'s trail grade read** (`TrailGradeAt` and the network's build) for S1–S2 (and S6's wear on paved tiles).
6. **A paved road nobody walks any more** (A336, sheet `D:\Projects\brushes\paving\paving_ageing.jpg`) — ⭐ **A it ages**:
   grass in the joints, then moss on some stones and the gravel greening in patches, the edges softened — at its oldest still
   plainly a road, half in the turf; or **B it stays as laid**, forever.

## 5. The build (after Joe's picks and W9's code)

As the mock, in the repo's own terms: the grade per tile (S1) and planned (S2) into the network's points (as `Grades` today),
a fourth channel in the path field (or a second field) carrying the laid grade and planned; the shader's gravel, cobble, seam,
stakes and heap with their values in the look file; tick off byte for byte as before; a probe line (`paving:`) on a made-up
network — a gravel lane, a cobbled lane, a planned stretch — red-checked; renders on Joe's save beside this mock. Nothing in
`Kith.Sim`, `tests/` or `data/` from the art lane.

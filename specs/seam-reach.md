# Spec: A quarry or a mine stands near its seam

**Decision:** D539 (Joe, 2026-10-09). **Status:** ✍️ **specced on `slice/seam-reach`, NOT built** — calls in §6.

---

## 1. Why this exists

Joe: *"quarries and mines should need to be within a certain distance from a stone or iron node or else the
building can't be placed. same way fishing hut is for water."*

Today nothing ties a quarry or a mine to rock. `CanBuildAt` asks neither, and `CanPaintWorkGround` refuses only
the wrong *kind* of ground (`FaceOf(workplace)`: *"A quarry works rock — this is grass."*), never the *distance*.
So a quarry can stand in a meadow and paint a seam across the valley, and its quarriers walk the whole way for
every stint. The building reads as decoration, not as the place the stone comes out of.

**Pillars:** §1.1 *legibility* (the building stands where the work is, so the map explains the walk), §2.5
*terrain dictates viability* (the ground decides where a trade can be, as the river decides the fishery).

---

## 2. What already exists to copy

| Building | Rule | Field | Refusal |
|---|---|---|---|
| fishing hut | must **touch** water | `BuildingRow.MustTouch` | *"A fishing hut has to stand against water, and nothing beside that tile is water."* |
| hunter's lodge | a tree within a **reach** | `BuildingRow.HuntingRadius` (12), `SimWorld.ForestTilesWithin` (a diamond) | *"A hunter's lodge needs woods to hunt, and there is not a tree within 12 tiles of there."* |

Joe said *"within a certain distance"*, which is the lodge's shape (a reach), not the fishery's (a touch). The
seam is already named on the **job** row: `works_face` is `Rock` for the quarrier and `IronDeposit` for the miner
(D449). So the building only needs a number.

---

## 3. The rule

- **A new building-row field, `face_reach`** (tiles; 0 = no rule). The quarry and the mine set it; nothing else.
  The ground it asks for is the face its trade works (`JobsCatalog.WorksFace` of the row's trade), so a modded face
  trade gets the rule by setting one number.
- **`CanBuildAt` refuses** a quarry or a mine with no tile of its face within `face_reach` of the building's tile
  (the lodge's diamond, `|dx| + |dy| ≤ reach`), **counting only face tiles the village can walk to**
  (`TravelCost.CanReach`), the D110/D111 lesson: a seam across the river is not near, it is unreachable, and the
  sentence says which.
- **The order and the words**, after the reachability check, beside the fishery and lodge rules:
  - none at all within reach: *"A quarry has to stand near rock, and there is none within 4 tiles of there."*
  - only across water: *"The rock near there is across the water — the quarriers could not reach it."*
- **Moving a building** (`relocate`) goes through `CanBuildAt`, so the rule holds there too.
- **A building already standing is never refused after the fact**: a save from before this slice keeps a quarry in
  a meadow working as before.
- **The ghost says it before the click**: the view already shows `CanBuildAt`'s sentence for a refused ghost (the
  fishery's and the lodge's work this way), so nothing new is drawn.
- **Cost:** asked at placement and as the ghost moves, at most `(2r+1)²/2` tiles, a reach check only on a face
  tile. Never per tick.

---

## 4. Guards (TDD, red first, red-checked and counted)

1. A quarry beside rock is allowed; the same quarry moved past `face_reach` from every rock is refused, with the
   sentence naming rock and the reach.
2. The same for a mine and iron (and a mine beside stone, with no iron near, is refused: the face is the trade's).
3. Rock only across the river is refused with the *across the water* sentence.
4. A building with `face_reach` 0 (every other row) is untouched: the fishery and the lodge rules still decide theirs.
5. The shipped rows carry the reach (`QuarryReach`, `MineReach` pins), and a modded face row gets the rule from data.
6. Existing quarry and mine tests still find a place (they search outward from the seam: `SomewhereAQuarryFits`).
7. No golden moves (no golden village marks a quarry or a mine); `git diff` says so.

---

## 5. Measured before typed

Over the 64 shipped seeds `quarry.md §6.1` used (scratch `ZzSeamReach`, deleted): every reachable seam, and
whether any tile within the reach of it passes `CanBuildAt` for its building, at founding.

| Reach | Stone seams with a legal quarry site | Iron seams with a legal mine site |
|---|---|---|
| 1 | 555 of 555 | 172 of 172 |
| 2, 3, 4, 6, 8 | 555 of 555 | 172 of 172 |

**No reach strands a seam**, even one tile: seams lie in open ground with room beside them. So Q1 is a call
about feel, not survival. ⚠️ Measured on an empty valley; a seam later hemmed in by the player's own buildings
can lose its sites, and the refusal then says so like any other.

---

## 6. Joe's calls

- **Q1 — How far?** Recommended **4 tiles** for both: a quarry at the edge of its rock, the face a few steps from
  the door. 2 is nearly the fishery's *touch*; 8 lets it stand back in the meadow.
- **Q2 — Should a quarry's painted faces also have to be within that reach?** Recommended **yes**. Otherwise the
  rule is a formality: stand beside one stray rock, then paint the big seam across the valley, which is exactly
  the walk the rule exists to stop. *"No"* keeps painting as it is and limits only where the building goes.
- **Q3 — Seams across the river don't count** (recommended, D110). Confirm or overrule.

---

## 7. Definition of Done

- [ ] Joe's calls answered and written into §3.
- [ ] §5 measured; the number typed into `data/sim.config.json` with the measurement in its comment.
- [ ] Guards §4 written red first; red-checks counted, zeros written down.
- [ ] The four checks; no golden moved.
- [ ] Played by Joe: placing a quarry and a mine near and far from their seams, reading the ghost's sentence.
- [ ] `quarry.md`, `iron-mine.md`, `DESIGN.md` §6/§7 and `HANDOFF.md` in the same commit; this status line made true.

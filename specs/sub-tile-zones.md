# Spec: Sub-tile zones — the player paints finer than the ground is stored

**Decisions:** D42, D86, D87, D332, D333, D335. Follows `gridless.md` and `brush.md`.
**Status:** ✅ **BOTH SLICES BUILT.** **Slice 1** (2026-09-09, D335) — `SubTile`, `ZoneMap` storing sixteen sub-tiles to a tile with incremental summaries, and the hash. **Slice 2** (2026-09-09, D336) — the brush paints quarter-tiles, the wash and the outline are drawn from them, and a five-tile round brush now fills **79% of its box** where whole tiles gave **84%**. ⭐ *That five points is the difference between a circle and "haha this is a circle????"* **1063 passing, 0 failing, 2 skipped of 1065; six goldens moved in slice 1 and none in slice 2.** ✅ **Played by Joe (2026-09-10/11)** — and what he found at the EDGE is §3.1 (D350, 2026-09-11): homes and fields hung half a tile out of the paint, and a taken-back field stayed ploughed for ever. **1082 passing, 0 failing, 2 skipped of 1084 — no golden moved.**

---

## 1. Goal

**Joe, looking at a 5×5 round brush:** *"haha this is a circle????"*

He was right and the evidence is arithmetic rather than opinion. At five tiles across there are
exactly three shapes a square grid can hold — a 13-tile diamond, a 21-tile square-with-corners-bitten,
and the full 25-tile square. **None of them is a circle.** The information is not there:

```
   <= 4          <= 6          <= 8
   ..#..         .###.        #####
   .###.         #####        #####
   #####         #####        #####
   .###.         #####        #####
   ..#..         .###.        #####
   13 tiles      21 tiles     25 tiles
```

⛔ **This is NOT the smoothing** — D333 fixed that, and D334 stopped it eating real corners. A disc
only starts reading as a disc at about nine tiles across, which is far bigger than anyone wants a
default brush to be.

**So the paint gets a finer grid than the ground.** Zones are stored at **four sub-tiles per tile
per axis — sixteen per tile** — while terrain, the travel-cost field and every derived economy
number stay exactly where they are.

## 2. Which pillar it serves, and the one it must not touch

- **§1.1 legibility.** A painted region is a decision the player made; it should look like the shape
  they drew rather than like the storage format.
- ⛔ **§3 determinism is why the sub-tiles are real state and not a rendering trick.** A mask kept
  only for drawing would be a view that draws what the sim does not believe — `gridless.md §7.2`'s
  refusal, and D80 at another scale. **The sub-tiles are hashed.**
- ⛔⛔ **THE ECONOMY MUST NOT MOVE.** Nineteen config entries are stated in tiles and all of them
  hang off `gatherer_hut_ring_tiles` through `VillageEconomy.MaxHomeToWorkTiles`. **D122 froze
  nineteen people when that chain moved by one tile.** *This slice is not allowed to be a
  re-balance, and the rule in §4 is what keeps it from becoming one.*

## 3. The rule, in one sentence

> **A tile counts as painted when at least half of its sixteen sub-tiles are.**

⭐ **One sentence, all three layers, and every existing tile-level question keeps its meaning.**
*"Is this tile residential?"*, *"how many tiles has this farm?"*, *"which ground is marked for
clearing?"* — all still answered in tiles, all still the same numbers for any village that paints
whole tiles, which is every village that exists today.

### 3.1 What the rule does NOT say — the edge (D350, 2026-09-11)

⛔⛔ **Paint is quarter-tiles; a home, a plough and a felling are whole-tile acts. Anything tile-shaped
that hangs off quarter-tile paint sticks out of it by up to half a tile unless a second rule says
otherwise** — and until D350 none did. Joe, with four screenshots from one sitting: *"the houses are
building outside of the painted area"*, *"the farm field is built outside of its painted area"*,
*"'removing' farm land leaves a field that can't be removed"*, *"the painted area needs to be
accurate for the user across all use cases."* The rule above was chosen so the economy's tile counts
held; nothing then said what the **edge** should look like. It is stated per layer now:

| Layer | The whole-tile act | The edge rule |
|---|---|---|
| **Housing** | a home is sited | **`ChooseSite` takes only a tile painted in FULL (16 of 16).** The half rule still decides `IsResidential` and `ResidentialTiles` for everyone else. The brush's ragged rim is a margin nobody builds on, and a house never overhangs the border. |
| **A farm's ground** | the plough, the sowing, the reaping | **The farm's paint is laid in WHOLE tiles** — the brush takes a tile when at least half of it is under the stroke (`BrushStroke.TilesMostlyUnder`, D335's rule applied at paint time), the border is traced **unrounded**, and the furrows fill exactly what the line encloses. *A ploughed field is man-made and reads as man-made precisely because its edges are straight* (D342). ⭐ **Joe chose this over clipping the drawn field to the quarters** (2026-09-11). Belt and braces in the sim: `Plough` runs only once the tile is **held** (`ZoneMap.Holds`, ≥ 8 of 16), never on the first quarter. |
| **A forester's ground** | felling, planting | unchanged — the half rule, drawn as the curve. A treeline is ragged and a tree's canopy overhangs its tile anyway (D337). |
| **Harvest marks** | felling, quarrying | unchanged — the half rule, drawn as the curve. ⚠️ **Except the mark D100 paints under a newly marked building**, which is not drawn as a zone at all: the site is drawn in the clearing's orange while its ground is busy, and the mark is retired when the building stands (D344). |

⭐ **And taking a farm's ground back UN-PLOUGHS it.** When a tile stops being held — the brush's
take-back or the farmhouse's demolition — `Field`/`Sown`/`Ripe` → `Grass` and the crop id is cleared
(`SimWorld.AfterTakingGround`): D84's *no scar* rule for a dug seam, applied to a field. A standing
crop given up is counted per stroke and said once. Before D350 nothing had ever turned a field back,
so the brush left furrows on ground nobody owned, for ever.

## 4. Data model

| Held | Resolution | Hashed? |
|---|---|---|
| `_residential`, `_harvest`, `_workGround` | **sub-tile** (16 per tile) | ✅ **yes — this is the state** |
| per-tile painted **count** (0–16) per layer | tile | ❌ derived, incremental |
| per-tile **summary** (`count >= 8`) per layer | tile | ❌ derived |
| per-tile work-ground **owner** | tile | ❌ derived |

- **`SubTile`** — its own type, not a `GridPos` in different units. ⛔ *One number answering two
  questions reports the wrong one* (D322); a distinct type makes the compiler enumerate every call
  site, which is the method D38's singleton seam used and the reason it worked.
- **The summaries are maintained incrementally**, never recomputed on read. `IsHarvest(tile)` is
  called in hot scans, and folding sixteen sub-tiles per call would be a sixteen-fold cost in a path
  D179 already had to rescue once. ⚠️ **The suite's own wall-clock is the instrument here.**
- **One owner per tile stays the rule, one level down.** A sub-tile may only be given to the owner
  the tile already has, or to nobody. *Two huts sharing a tile would have two crews felling the same
  trees and the village could not say whose a stump was* — `ZoneMap`'s own argument, unchanged.

## 5. What the sim sees, and what the brush sees

⭐ **This is the whole shape of the change and it is why ~175 call sites mostly do not move.**

| Asked by | Resolution |
|---|---|
| The **brush** (`Set*`) and the **renderer** and the **hash** | sub-tile |
| **Everything else** — `IsResidential`, `IsHarvest`, `WorkGroundOwner`, `WorkGroundTiles`, `HarvestTiles`, the painted-ground iterators, the farm, the clearing scan | **tiles, unchanged** |
| **`ChooseSite`** (D350) | tiles — but a **whole** painted tile (16 of 16), see §3.1 |
| **The farm's brush** (D350) | lays whole tiles: `BrushStroke.TilesMostlyUnder`, see §3.1 |

⛔ **A villager is never sent to clear a quarter of a tile.** Work is tile-shaped because the ground
is tile-shaped; only the *decision about where* got finer.

## 6. Edge cases & failure modes

- ⛔ **The goldens move once** (D152), because the hash mixes sub-tile indices and there are sixteen
  times as many. ⚠️ **Proved the D211 way first**: restore the tile-level mix, re-run, and every
  golden must come back **byte-identical** — the village does not change, the fingerprint's shape
  does. *If one does not come back, that is a defect to explain, not a number to re-take.*
- ⚠️ **Founding paints whole tiles** (`starting_residential_radius`), and must keep doing so, or the
  cold start changes for a reason nobody asked for.
- ⚠️ **Half is a boundary and boundaries are where the bugs are.** Exactly eight sub-tiles painted
  counts as painted; the guard poses seven, eight and nine.
- ⚠️ **The brush's size stays stated in TILES.** *"5×5"* keeps meaning five tiles across, and the
  wheel steps in quarter-tiles — otherwise every number the player has learned changes meaning.

## 7. How it is tested

- The half rule at its boundary, per layer, and that a whole-tile paint is identical to what it was.
- **The economy numbers are unchanged for a whole-tile village** — `WorkGroundTiles`, `HarvestTiles`
  and `ResidentialTiles` all still count tiles.
- One owner per tile, refused at the sub-tile door.
- **The edge (D350):** a quarter of farm paint does not plough, eight quarters do (`FarmTests`); taking
  the paint back or demolishing the farm turns the field to grass and clears the crop; a home is sited
  only on a tile painted in full (`SubTileZoneTests`); the farm's stroke is whole tiles at half
  coverage, in a stated order (`BrushShapeTests`). ⚠️ *The order guard scored zero on its red check
  and says so — a `Dictionary` with no removals happens to enumerate in insertion order.*
- ⛔ **Red-check every guard and count the reds** (D326), and **watch the suite's wall-clock** —
  sixteen times the zone array is the kind of change that shows up there first (D329, D331).

## 8. Definition of Done

### Slice 1 — ✅ MET (2026-09-09)

| # | Item | State |
|---|---|---|
| 1 | `SubTile` exists; `ZoneMap` stores sub-tiles with incremental tile summaries | ✅ |
| 2 | Every tile-level API keeps its signature **and its meaning** | ✅ ~175 call sites unchanged |
| 3 | The hash mixes sub-tiles; **goldens move once** | ✅ six moved, one commit, one reason |
| 4 | The brush still paints whole tiles — **no behaviour change** | ✅ |
| 5 | Suite green, wall-clock not worse | ✅ 1063 / 0 / 2 of 1065, **1m50s** |

⭐⭐ **THE PROOF CAME IN THE NATURAL ORDER, WHICH IS BETTER THAN D211's RECONSTRUCTION.** The
substrate landed **first, with the hash still mixing tiles, and the whole suite green** — so the
village provably did not change when the storage did. Only then did the mix move to sub-tiles and
the six numbers with it. *There was nothing to reconstruct: the "before" was a real run.*

⚠️ **Two defects the guards found while being written**, both the same shape — a number answering a
question it was not asked:
- `ReleaseWorkGround` returned *tiles it had any paint on* while the sentence says *"the N tiles it
  **kept**"*. **It over-reported to the player by however many corners they had clipped.**
- Its early-out asked `_tilesByOwner.ContainsKey`, which only knows about tiles a hut **held** — so
  a hut whose ground was all quarter-painted **walked away leaving its paint behind**, still drawn,
  still hashed, owned by a building that no longer existed.

### Slice 2 — ✅ MET (2026-09-09)

| # | Item | State |
|---|---|---|
| 6 | The brush paints in quarter-tiles; a small round brush is genuinely round | ✅ **79% of its box against a disc's π/4**; whole tiles gave 84% |
| 7 | The wash and the outline are drawn from the sub-tiles | ✅ one set of quarter-tiles, so they cannot disagree at the edge |
| 8 | **No golden moves** — view and input only | ✅ |
| 9 | The size is still stated in **tiles** | ✅ *"5.25 tiles round"*, and the wheel steps by a quarter |
| 10 | Joe plays it | ⏸️ owed |

⛔ **`FloatBanTests` reddened and was right.** `AcrossInTiles` returned a `float` from the sim's
public API to make a sentence — D2 bans that, and the guard did not care that the number was only
going into words. ⭐ *It was right on the substance too: turning quarter-tiles into "5.25 tiles" is
presentation, and presentation is the view's job.* The sim counts sub-tiles now; `VillageMap` says
the sentence.

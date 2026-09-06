# Spec: Gridless — free placement, real facings, and paths that bend

> Status: ▶️ **OPTION C CHOSEN BY JOE (2026-09-06). SLICE 1 IS BUILT AND GREEN — `Fixed` (Q32.32)
> and its guards, no behaviour, no golden moved.** Slices 2–4 are not started.
> Every number below was measured against the code on 2026-09-06, not read out of `DESIGN.md`.
> · Owner: Joe + Claude Code
>
> **⭐ His answer, and the reasoning he gave for it:** *"Things stop being locked to squares.
> In game development and engine architecture, 'gridless' describes gameplay space and entity
> interaction, not how terrain data or engine memory is organized under the hood."* Terrain
> heightmaps and spatial partitioning stay gridded; **continuous placement, free rotation and
> agent-driven navigation are what change.** That closes §10.2 as well: terrain stays tiled under
> the hood **on purpose**, permanently, and it is not a compromise to be revisited.
>
> ⚠️ **One translation, and it is not a disagreement.** He described continuous coordinates as
> *floating-point* `(x, y, z)`, which is the standard formulation everywhere except here: this sim
> bans floats in state (D2), because determinism is *same seed ⇒ byte-identical state* and float
> rounding breaks it. **Q32.32 gives the same continuous behaviour with exact reproducibility.**
> The game is also 2D, and pathing reuses the one shared cost field rather than growing a second
> NavMesh (`CLAUDE.md`).
> Format per `METHODOLOGY.md §2`. Implements `DESIGN.md §4`'s *"two directions Joe set on
> 2026-08-16"*, direction 1.

> **⭐ Joe, 2026-08-16:** *"ultimately i want this game to be 'gridless' in that buildings can be
> placed facing any direction, there isn't a grid map, paths aren't straight lines unless
> appropriate."*
>
> **▶️ Joe, 2026-09-06:** *"let's do gridless after the UI work is complete"* — then, the UI work
> done: ***"lets go!"***

---

## 1. ⛔ THE ONE THING THAT MUST BE SETTLED FIRST — AND IT IS NOT MINE TO SETTLE

`DESIGN.md §4` names it and then stops, correctly: *"the first question is whether the sim goes
continuous or stays discrete under a continuous **presentation** — those are very different costs
and only the first is what Joe described."*

**That question is §7 of this document and it is the gate.** Everything else here is the audit
that makes answering it possible. **I have a recommendation (§7.4) and it is not option A or B.**

---

## 2. ⭐⭐ WHAT THE AUDIT ACTUALLY FOUND — five facts, and three of them change the plan

### 2.1 ✅ The sim has NO floating point in it at all. D2 is genuinely held.

Fifteen `float`/`double` occurrences exist in `Bclone.Sim`, and **every one is in
`FixedTimestepDriver` or `TargetTicksPerSecond`** — wall-clock pacing, deliberately outside sim
state. **Sim logic is integer end to end.**

⭐ *This is better news than the design doc assumes.* There is no float creep to clean up first;
the question is purely what to introduce.

### 2.2 ⛔⛔ THERE IS NO `Fixed` TYPE. IT EXISTS IN THE DESIGN AND NOWHERE ELSE.

`DESIGN.md §4` says *"Fixed-point `Fixed` (Q32.32) **already exists in the design** for exactly
this kind of need — that is the door, and it was left open deliberately."* **That sentence is
true as written and is very easy to read as "the type is in the codebase."** It is not: the only
match for `fixed` in `src/Bclone.Sim` is `FixedTimestepDriver`, which is the tick loop and
unrelated.

⚠️ **So "the door was left open" means a decision was recorded, not that work was banked.**
Whoever takes this builds Q32.32 from nothing, with its own guards. *Budget it.*

### 2.3 ⛔⛔ A BUILDING IS ONE TILE, WITH NO EXTENT AND NO ROTATION — SO "FACING" IS NEW STATE

`Rotation`, `Facing` and `Orientation` return **zero hits** across the entire sim. A building is a
single `Position` (`GridPos`), and there is no footprint, no width, no height, no angle.

⭐⭐ **This inverts the framing.** Gridless is usually imagined as *removing* a constraint. Half of
what Joe asked for — *"buildings can be placed facing any direction"* — is **adding a property
buildings have never had**, and it is additive work that does not depend on removing the grid at
all. *A building could gain an extent and a facing tomorrow, on the grid, and it would already
look like the thing he described.*

### 2.4 The grid's actual footprint in the sim

| Surface | Size | What it does |
|---|---|---|
| `GridPos(int X, int Y)` | **253 references** | the coordinate, everywhere |
| `GeneratedMap` | 509 lines | terrain, soil, crops, saplings as **linear-indexed tile arrays** |
| `TravelCostField` | 168 lines | `Dictionary<GridPos, TerrainCostField>`, `BaseTileCost = 10` |
| `ZoneMap` | 374 lines | residential / work / harvest paint, three tile layers |
| `StateHash` | 714 lines | `MixMap` walks the tile arrays **by index**; ~12 sites mix `(uint)X, (uint)Y` |
| Economy | 19 tile-keyed config entries | `MaxHomeToWorkTiles`, `WorkGroundTilesPerWorker`, `FieldTilesOneFarmerKeeps`, the gatherer's ring |
| `VillageMap` (view) | 39 tile references | drawing and the brush |

### 2.5 ⭐ `BaseTileCost = 10` is already a fixed-point foothold

Travel costs are integers **scaled by ten** — a tile is 10, not 1. *The cost field is already
doing arithmetic in fractional units without calling it that*, which means finer movement
granularity does not need the cost model rewritten, only re-scaled.

---

## 3. Which pillars this serves, and the one it endangers

- **§2.6 Desire-path roads is the pillar that GAINS.** A path that bends where people actually
  walk is the whole idea, and a grid forces it into staircases. This is the strongest argument
  for doing it at all.
- **§1.1 Legibility** cuts **both ways** and that is the crux of §7. A valley that looks like a
  place rather than a spreadsheet is more legible. **A view that draws something the sim does not
  believe is less legible**, and this project has a name for that failure: D80's *two panels, one
  screen, and the overview was wrong.*
- **⛔ §3 Determinism is what makes this hard rather than merely large.** *Same seed + same inputs
  ⇒ identical state* is the contract. Integer-only state is how it is currently guaranteed
  (D2). **Anything continuous must be fixed-point, never float**, and must enter the hash in a
  stated order.

---

## 4. What "gridless" actually means, unbundled

Joe's sentence contains **three separate asks**, and the audit says they have very different
costs. *Unbundling them is the most useful thing this document does.*

| # | The ask | Depends on removing the grid? | Cost |
|---|---|---|---|
| **A** | *"buildings can be placed facing any direction"* | **No** — §2.3, buildings have no facing today either way | **Low.** Additive: an extent and an angle. |
| **B** | *"paths aren't straight lines unless appropriate"* | **No** — the cost field can stay tiled while agents walk real lines between waypoints | **Medium.** Sub-tile positions + string-pulling. |
| **C** | *"there isn't a grid map"* | **Yes, by definition** | **Very high.** Terrain, soil, zones, hash, economy, goldens. |

⭐⭐ **A and B are what the player SEES. C is what the data structure IS.** A valley delivering A
and B reads as gridless to anyone playing it, and C is invisible except through A and B.

---

## 5. Data model, if it goes ahead

- **`Fixed` (Q32.32)** — a `readonly record struct` over `long`, integer-only ops, no `float`
  anywhere in it, its own determinism guards. **Written from nothing** (§2.2).
- **`Point`** — `Fixed X, Fixed Y`, replacing `GridPos` for *things that move and things that are
  placed*. `GridPos` **survives as the index into terrain**, which is what §7.4 turns on.
- **`Facing`** — a fixed-point angle, or a 16-step compass. ⚠️ *Sixteen steps is enough to look
  free and is exactly hashable; a continuous angle is neither.* Recommend the compass.
- **`Extent`** — a building's footprint in fixed-point units. Required by A, and **it is the piece
  that makes placement collision real** rather than "is this tile taken".
- **Hash:** positions mix as their **RAW** fixed-point bits, all sixty-four, in a stated order —
  `StateHash.MixFixed`.
  ⛔⛔ **THIS BULLET USED TO SAY "QUANTISED" AND THAT WOULD HAVE BEEN A DETERMINISM BUG.** The word
  came from D303, which is right *about a different hash*: the map generator uses a hash as a
  **pseudo-random source**, where quantising is the whole point because nearby queries must land in
  one bucket so terrain is stable under a small movement. **`StateHash` is the opposite kind of
  thing — a fingerprint** — and its entire contract is that any differing state byte differs the
  hash. A quantising mixer would let two worlds whose villagers stand a fraction of a tile apart
  hash *identically*, so **the determinism suite would go green across a genuine divergence.**
  *Two jobs, two functions, never one.* Guarded by
  `FixedTests.TheHashDistinguishesValuesThatDifferOnlyInTheirFraction`.

---

## 6. Edge cases and failure modes — named before they are stepped in

- **⛔⛔ THE GOLDENS ALL MOVE, AND THEY MOVE ONCE.** D152's rule: goldens go last, one commit, one
  stated reason. **This is the largest golden move the project will ever make**, and the
  byte-identical trick used in D211 (*restore the old arrangement, prove nothing else changed*)
  **is not available** if coordinates change type.
- **⚠️ The economy is stated in TILES in nineteen config keys.** Re-deriving them in distance
  units is not arithmetic, it is a re-balance — and D122 *"froze nineteen people the last time
  that chain moved, by one tile."* **This is the single biggest risk in option C** and the reason
  §7.4 exists.
- **⚠️ Pathing must not regress into two cost systems.** `CLAUDE.md`: *one shared cost field for
  pathfinding and labor catchment. Do not build two competing travel-cost systems.* A continuous
  navmesh beside the tile field would be exactly that.
- **⚠️ A brush that paints regions is a different tool from one that paints tiles.** `ZoneMap` is
  three tile layers and the brush is square (D221). Free-form zones are their own slice.
- **⛔ Determinism first, not last.** The determinism test stays green throughout or the slice is
  wrong. A fixed-point regression is a P0 (`CLAUDE.md`).

---

## 7. ⛔ THE DECISION — three options, with what each actually buys

### 7.1 Option A — continuous sim (what Joe literally described)

Positions, buildings and paths all become fixed-point. The grid is gone from the model.

- ✅ Delivers all three asks, honestly, with no gap between what is drawn and what is believed.
- ⛔ **Touches every row of §2.4**, re-derives the economy, moves every golden, and rebuilds
  pathfinding. **This is the re-founding `DESIGN.md §4` warns about**, and it is a *v2-shaped*
  amount of work — plausibly larger than Phases 3 and 4 combined.

### 7.2 Option B — discrete sim, continuous presentation (cheapest)

The sim keeps the grid; the view draws buildings rotated and paths smoothed.

- ✅ Nearly free. Determinism and every golden survive untouched.
- ⛔⛔ **I do not recommend it, and the reason is a Non-Negotiable.** If the sim still snaps a
  building to a tile, placement is *still gridded* and only the picture lies. **A view that draws
  a building at 30° while the sim believes it is an axis-aligned tile is D80 at architectural
  scale** — and §1.6 is *traceable over clever*. *A facade is the one thing this project has
  consistently refused.*

### 7.3 Option C — **the grid becomes an index, not a constraint** ⭐ RECOMMENDED

**Keep the tile grid as the terrain and cost substrate. Make positions, footprints and facings
fixed-point.**

- Terrain, soil, crops, saplings and `TravelCostField` stay tile-indexed — so **§2.4's three
  biggest rows do not move**, and the nineteen economy keys keep meaning what they mean.
- Buildings gain an `Extent` and a `Facing` and are placed at a `Point`. **Collision becomes
  geometry rather than tile occupancy.**
- Villagers hold a `Point` and walk **real lines between waypoints**, taking the route from the
  existing tile cost field and then string-pulling it straight. **That is ask B, and it is where
  desire paths become possible.**

**Why this is the honest one rather than the convenient one:** the sim genuinely believes the
building is where it is drawn and at the angle it is drawn — **there is no facade** — while the
question *"how expensive is this ground to cross?"* stays answered by the one shared cost field
`CLAUDE.md` insists on. The grid stops being *where things are* and becomes *what the ground is
like*, which is the only job it was ever actually good at.

⚠️ **What C does not deliver:** terrain itself stays square, so a coastline or a forest edge is
still tiled at the finest zoom. **That is a real limit and it should be shown to Joe rather than
argued away** — it may be exactly what he means by *"there isn't a grid map."*

### 7.4 ⛔ THE QUESTION FOR JOE

**Does *"there isn't a grid map"* mean the terrain itself must stop being square (A), or that
things stop being locked to squares (C)?**

*I read his sentence — placement, facing, paths — as three statements about **objects and
movement**, none about terrain. But that is an inference about intent, and it is the single most
expensive inference in this project.*

---

## 8. Slices, if C is chosen

1. **`Fixed` (Q32.32) and its guards.** No behaviour. Determinism test extended to fixed-point
   arithmetic. *Nothing else starts until this is green.*
2. **Buildings gain `Extent` and `Facing`.** Placement collision becomes geometry. **Goldens move
   once, here, with a stated reason.** The view draws the angle.
3. **Villagers hold a `Point`.** Movement interpolates in fixed-point; the cost field is untouched.
4. **String-pulled paths**, and then desire paths (§2.6) become writable for the first time.

⚠️ **Each slice ships playable** (`DESIGN.md §4`). ⛔ **Slice 1 is not a spike** — if Q32.32 is not
provably deterministic the whole direction is wrong and it is worth learning in week one.

---

## 9. Definition of Done

### Slice 1 — ✅ MET (2026-09-06)

| # | Item | State |
|---|---|---|
| 1 | `Fixed` (Q32.32) exists, integer-only, **no `float`/`double` in its API at all** | ✅ `src/Bclone.Sim/Core/Fixed.cs` |
| 2 | Rounding is one stated rule, asserted on **both sides of zero** | ✅ floor; −1.5 → −2 |
| 3 | Overflow fails loudly, with both operands in the message | ✅ throws; anti-vacuity guard that fitting arithmetic does not |
| 4 | Determinism extended to fixed-point arithmetic | ✅ scripted replay folded through `MixFixed`, pinned |
| 5 | **No golden moved** | ✅ *stated as a `git diff` over the five golden files, not as "the tests passed"* |
| 6 | Every guard red-checked, **and the reds counted** | ✅ 5 of 6 red; **one scored zero and is recorded** (§10.3) |
| 7 | Suite green | ✅ 954 / 0 / 2 of 956 (was 928) |

⛔ **Slices 2–4 each owe their own DoD, and slice 2 owes the one-commit golden move** (D152) —
buildings gaining an extent and a facing is the change that moves them, with one stated reason.

---

## 10. Open

1. ~~**§7.4 — A or C.**~~ ✅ **CLOSED 2026-09-06: C.** ⚠️ And it turned out **slice 1 was never
   actually gated by it** — `Fixed` is required identically under A and C, so the one slice that
   could have started before the answer was the one the spec said could not. *Recorded so the next
   dependency claim gets checked rather than inherited.*
2. ~~**Does terrain stay tiled forever under C?**~~ ✅ **CLOSED 2026-09-06 by Joe, and it is a
   deliberate architecture rather than a compromise:** terrain heightmaps and spatial partitioning
   are gridded in essentially every gridless game. **Not to be re-litigated.**
3. ⛔ **The locale guard scored ZERO on its red check and is kept anyway — knowingly.**
   `FixedTests.TheLocaleCannotChangeWhatAFixedLooksLike` stays green when every
   `InvariantCulture` in `Fixed.ToString` is swapped for `CurrentCulture`, because the
   implementation is culture-proof *by construction*: it formats two integers with no specifier and
   joins them with a literal `'.'`. **It guards nothing today.** It is kept as a ratchet — it fires
   the day somebody reformats the fraction through a `decimal` or an `"F6"` — and the zero is
   written down so nobody reads it as evidence the risk was faced.
4. **`Fixed` has no `Sqrt`, deliberately.** Nothing calls it until real distances arrive; it lands
   in the slice that needs it, with the guard that needs it.
5. **What the game should DO when a tick throws** — `Fixed` overflow joins
   `TravelCostField.TicksForCost`, `DeterministicRandom.NextUInt(0)` and `SimConfig` validation as
   an in-tick throw. **There is no error boundary in `SimLoop`.** Not slice 1's question, and it
   applies to all four.

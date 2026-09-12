# Spec: Desire paths — the ground remembers where people walk, and a worn path is cheaper

**Decisions:** D41 (one shared cost field), D179 (the breadth-first sweep — *"that day"*), D353 (Phase
4.5 item 3), D356 (legs, clock A), D357 (Joe's rule: worn cheaper, built cheaper still), **D358 (this
slice).** Follows `gridless.md §8–§9` and `pathfinding-and-water.md`.
**Status:** ✅ **BUILT, SIM AND VIEW, IN ONE COMMIT (2026-09-11, D358); ⚠️ UNPLAYED BY JOE.**
`PathWear`, `PathWearSystem`, the priced cost field (`TerrainCostField.Refill`, Dial's algorithm),
cost-based leg ticks, the hash, six `path_*` keys, `DesirePathTests` (9); trails on the map, the
*Paths* overlay (P), an inspector sentence, a probe line. Paving (§2.6's *upgrade an emerged path*)
is Phase 5/6 and not in this spec beyond the cost table it will need.
· Owner: Joe + Claude Code · Format per `METHODOLOGY.md §2`.

> **⭐ Joe, 2026-09-11 (D357):** *"paths should be cheaper / should increase speed — and upgraded
> paths (stone, brick, something else) should increase speed even more."*

---

## 1. Goal

`DESIGN.md §2.6` in one line: **the player paves what the village already proved matters.** The
sim has to prove it first, and it could not until gridless slices 3 and 4 (D354, D356) put people
at points walking straight lines — before that a trail would have been worn along a staircase
nobody was meant to see. This slice is the *proving* half: every step treads the tile under it,
wear fades with the seasons, and past a threshold the tile is **cheaper to cross**, so routes
converge onto it and the loop that makes a footpath closes. Paving — the player's half — comes
later and reads the same table (§4.4).

Two of §2.6's sentences are the acceptance test, measured, not asserted: **a lone forager shouldn't
scar the map; daily housing↔granary churn should.**

## 2. Which pillar it serves, and the one it must not touch

- **Serves §2.6** directly and **§2.2 smart labour** through the *critical integration*: catchment
  and movement read **one** cost field (`CLAUDE.md`: *"Do not build two competing travel-cost
  systems"*), so a store on a worn lane genuinely reaches further — the feature, not a bug.
- **Serves §1.1 legibility** through the trails on the map and the log line — *"the village has
  its first path"* — and the inspector's sentence on a trodden tile.
- **Must not touch the walk's clock by accident.** D356 pinned the clock (`FirstGatherAtPace1 =
  20`, `FirstGatherAtPace3 = 41`, `TheValleyWalksOnTheSameClockAsBefore`). This slice moves the
  clock **deliberately and only on worn ground** — on grass every number is what it was, and the
  two Phase 0 pins still hold because one villager never wears a path (§3.2). The valley pin moves
  and is re-taken with its reason in the test.
- **Must not become the D179 regression.** Wear reaches the routes at a season turn, never per
  tick, and only when some tile's *price* changed (§3.4). A first draft that rebuilt every flow
  field every season took the suite from three minutes past ten.

## 3. The rules

### 3.1 Tread
Every tick a travelling villager moves (`BehaviorSystem.Travel`, after `WalkTo`), the tile under
their new position gains `path_wear_per_step` (1). It is the tile **under the straight line**
(`Villager.Tile`, the floor of the `Point`), not the route tile the staircase would have used —
so a trail is worn where people actually walk. Wear saturates at `ushort.MaxValue`; it never wraps
(a wrap would turn the main street into fresh grass identically on both machines — the D317
overflow argument).

### 3.2 Decay
At every season boundary (`PathWearSystem`, after `CropSystem`), every tile fades by
`path_wear_decay_per_season` (4), floored at 0. **Measured before the number was picked:** in the
shipped valley a busy tile is trodden 10–18 times a season, the median trodden tile ~5, and the
lone Phase 0 walker's route 1 a season. So a 5-a-season lane wears through (§3.3) in about six
seasons, a 15-a-season doorstep packs in a couple of years, and the lone forager's ground never
holds wear across a season — which is §2.6's *no paths* rule, satisfied by arithmetic.

### 3.3 Price
A tile's **entry cost** — what it costs to step onto it — is one of three classes:

| class | condition | cost | key |
|---|---|---|---|
| grass | wear < `path_worn_at` (12) | 10 = `BaseTileCost` | — |
| worn | 12 ≤ wear < `path_packed_at` (40) | 9 | `path_worn_tile_cost` |
| packed | wear ≥ 40 | 8 | `path_packed_tile_cost` |

Validation: `1 ≤ packed ≤ worn ≤ BaseTileCost` and `worn_at < packed_at`. **The discount is the
lock-in cap §2.6 asks for:** at 8 a packed detour a quarter longer than the straight walk is still
slower (`AWornLaneIsCheaperAndTheRouteTakesIt` — down one, along five packed, up one = 60 against
50 on grass: the detour does not pay). Built paths later take lower numbers in the same table.

**Hysteresis.** A tile that has become a path keeps its class until its wear falls a *decay's
worth* below the threshold (`PriceClassOf(wear, previous, band = decay)`). Measured before it
existed: the shipped valley re-priced in 26 of its first 40 seasons because well-used tiles sat
within one decay of a threshold and flapped across it; every re-price is ~a hundred flow fields.

### 3.4 When the routes learn
Wear is handed to the routes **only at a season turn, and only once a year** — spring
(`Decay(amount, reprice: spring)`). `PathWear.Generation` moves every sweep (the view's trails
redraw on it); `PathWear.RoutesGeneration` moves only when a re-pricing sweep finds a tile whose
class changed. `TravelCostField.FieldTo` compares `RoutesGeneration` to the generation its fields
were built at, and on a difference computes the entry-cost table once and **refills each cached
field in place on its next ask** (`TerrainCostField.Refill`) — same arrays, new numbers. Nothing
is allocated; nothing is rebuilt that nobody asks for.

Why once a year and not four: pricing every season cost the suite half again on top of
hysteresis, and the fiction is right — the picture follows every season, but a footpath firms into
a way people *rely on* over a year.

### 3.5 The leg's ticks follow the ground
`PlanLeg` charges a leg `cost[from] − cost[waypoint]` (the field's own answer along the route it
chose) in whole steps of `BaseTileCost`, rounded to nearest, never below one. On grass that is
exactly the route's step count — clock A untouched, D356's pins hold. On a worn path it is fewer:
**this is the only way a road can make a walk faster**, and the reason Joe wanted roads at all
(`AWornPathCostsFewerTicksInTheField`). If either end is unreachable the leg is charged its route
steps, as before.

## 4. Data model

### 4.1 `PathWear` (sim state, hashed)
`ushort[] _wear` in map order; `byte[] _priceClass` (the class as of the last hand-over — a
derived index, **never hashed**, D335); `Generation`, `RoutesGeneration`, `TroddenTiles` (derived,
never hashed). `Tread`, `Decay(amount, reprice)`, `At`, `TilesAtLeast`, `PositionOf`, `PriceAt`.
Owned by `SimWorld.Paths`, constructed after `Zones`. `SimWorld.AFirstPathHasWorn` is the once-only
narration flag and **is** hashed (it gates a log line, which is world state).

### 4.2 The hash
Sparse, the `ZoneMap` idiom: for each non-zero tile mix `(uint)index` then the wear, so a valley
nobody has walked hashes as it did. `ThePathsAreHashed` guards it.

### 4.3 The priced field
`TerrainCostField.Build(map, destination, baseTileCost, byte[]? entryCost)` and
`Refill(map, baseTileCost, entryCost, Scratch)`. With `entryCost == null` it is D179's sweep over a
reused queue. With a table it is **Dial's algorithm** — Dijkstra with a ring of `baseTileCost + 1`
buckets — because every edge is a small integer ≤ `baseTileCost`, so settling in cost order is a
walk round the ring: O(n + total cost), within a whisker of the sweep and provably the same
answer. A `PriorityQueue` version was measured first and cost the suite a minute. Stale entries
(a tile whose cost improved after it was queued) are skipped by `cost[current] != settling`.
`OnUniformGroundDijkstraIsTheSweep` guards byte-for-byte agreement on uniform ground.

`Scratch` (queue, ring, passability table) is **one per `TravelCostField`, never shared across
worlds** — the suite runs worlds in parallel and a shared buffer is a determinism hazard
(`CLAUDE.md`). `Forget()` drops it with the fields.

### 4.4 Config (`data/sim.config.json`)
`path_wear_per_step` 1 · `path_wear_decay_per_season` 4 · `path_worn_at` 12 · `path_packed_at` 40 ·
`path_worn_tile_cost` 9 · `path_packed_tile_cost` 8. The comment in the file carries the
measurement the numbers came from. Paving will add classes to the same table, not a second one.

## 5. What the sim sees, and what the view sees

The sim sees cost — `TravelCostField.Cost`, `StepToward`, `RouteFrom`, `TicksBetween` — and
nothing about wear directly except `Tread` and the seasonal system. The view (§8 slice 2) reads
`SimWorld.Paths.Tiles` and `Generation` and draws; it never writes.

## 6. Edge cases & failure modes

- **No paths** (§2.6): guarded by `DailyChurnWearsAPathAndALoneForagerDoesNot` — twenty years of
  the village fixture wear 15 tiles through (10 packed); twenty years of Phase 0's one villager wear
  none.
- **Lock-in** (§2.6): the cap in §3.3, guarded in `AWornLaneIsCheaperAndTheRouteTakesIt`.
- **A footstep rebuilds a field:** guarded by `WearReachesTheRoutesOnlyWhenTheSeasonTurns` — a
  row trodden hard mid-season still routes at grass prices, and the cached field count does not move.
- **Wear on water:** cannot happen — nobody stands on water (`NobodyEverStandsOnWater`), and
  passability is the terrain's regardless of wear.
- **An entry cost outside 1..base:** `Refill` throws — the ring cannot hold it, and a silent clamp
  would be a wrong field.
- **Synchrony (D28):** shared paths pull people onto the same tiles. Measured by outcome, not by
  count: six shipped seeds over fifty years carry **139 people against 118** and more food on five
  of six, twenty fixture years the same 12 people with **6.8% less time walking**. ⚠️ That is a
  balance shift in the direction Joe asked for, and the number clock B is measured against.

## 7. How it is tested

`tests/Bclone.Sim.Tests/DesirePathTests.cs` — the eight named above. Red checks recorded in D358.
The clock: `VillagerPointTests` keeps `FirstGatherAtPace1 = 20` and `FirstGatherAtPace3 = 41`
unchanged (one villager never wears a path); `TheValleyWalksOnTheSameClockAsBefore` is re-pinned
with the reason that worn ground is now faster — **the first deliberate clock change since Phase 2.**

## 8. Definition of Done

### Slice 1 — the sim ✅ MET (2026-09-11, D358)
- [x] Tread per step on the tile under the line; seasonal decay; three price classes with hysteresis.
- [x] One cost field, priced: Dial's algorithm, refilled in place, only on a re-pricing spring.
- [x] Leg ticks are cost-based; clock A untouched on grass.
- [x] Hashed sparsely; `AFirstPathHasWorn` narrated once.
- [x] Six config keys, validated, measured before chosen.
- [x] `DesirePathTests` green; goldens re-taken with the reason (the walk is faster on worn ground).
- [x] Suite wall-clock accounted for (D358 records the number).

### Slice 2 — the view ✅ MET (2026-09-11, D358, same commit)
- [x] Trails drawn on the ground from `Paths.Tiles`: worn as earth showing through, packed darker,
  gridless in look (a disc a tile, joined to worn neighbours by a band), collected on `Generation`.
- [x] A *Paths* overlay (button beside *Ground*, key P) washing every trodden tile by wear.
- [x] An inspector sentence for trodden, worn and packed ground (`DescribeBareGround`).
- [x] Outcomes before/after: six shipped seeds × fifty years, twenty fixture years (§6).
- [x] Probe line `trails:` — every drawn trail tile is worn in the sim.
- [ ] ⚠️ **Joe plays it.** The look of the trails is his call — width, colour, whether packed should
  read darker or lighter — and none of it is measured by anything above.

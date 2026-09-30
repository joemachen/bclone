# Spec: The quarry — painted rock that never runs out, and a valley with more of it

**Decisions:** D434 (Joe's calls on this spec, 2026-09-29), D395 (Joe: *"it should be painted land
like the forester's hut"*; more stone and iron on the map; iron nodes ≥ 50; the smithy a gift after
50 iron; the village starts with stone tools). Neighbours: D84 (**a deposit is finite, a building is
not** — `mutable-terrain.md §5.2`), D86 (the forester's hut: ground belongs to a building and is
priced in workers), D67 (seams, never a roll), D213–D215 (stone is spent on buildings and comes
from nowhere but the brush), D237 (a site waiting on a material says so), D344 (a worldgen change
can be draw-neutral — hash, don't draw), D347 (a seam has boulders; depletion is the seam getting
smaller), D385 (every load to a store), D430 (a tool: 34 % off ticks, 25 % on yield).
**Status:** 🔨 **§3.1 BUILT (D436, part 1 of six): the seams.** The rest specced, not built
(2026-09-29, `slice/quarry`). Every number is measured and
called by Joe (§1, §6); §9 holds what is left. The smithy gift and stone-versus-iron tools ride in the same
slice and are specified in `tools-and-the-smith.md §9`. Owner: Joe + Claude Code.

---

## 1. Why this exists

Joe, 2026-09-19 (D395), after a QA pass at 2×: **the quarry is next, spec first** — *"it should be
painted land like the forester's hut"*; more stone and iron on the map; iron nodes ≥ 50, so that
the first node cleared unlocks the smithy gift. It has been on the list since D84 (*"quarry workers
can harvest stone from a quarry (infinite)"*) and D249 (*"stone is quarried, carried, stored,
limited and spent on buildings, while the quarry and the mason do not exist"*).

**Joe's calls on this spec (2026-09-29, D434), asked before a line was written:**

1. **Rock only, and it never runs out.** A quarry's ground is painted over a stone seam; a
   quarried rock tile stays rock. So a seam is a future quarry site, and clearing it by hand
   spends that site.
2. **The quarry only.** The iron mine is the next slice; iron stays finite and dug by hand.
3. **Unlocked by stone dug.** The quarry appears in the build menu once the village has dug
   `quarry_unlock_stone` stone by hand — one *ever dug* counter, which the smithy gift shares
   for iron.
4. **More stone and iron are placed by hash, with no new draws.** Every seed keeps its river, its
   founding site, its soil and its woods; the goldens move once, for this reason only. Per-stage
   seeds stay on the shell roadmap (D344).
5. **In the slice:** iron nodes ≥ 50 and the counter; the smithy gift after 50 iron; stone versus
   iron tools (`tools-and-the-smith.md §9`). **Out:** the mason's yard (it gates the stone
   cottage, D206) and the iron mine.
6. **Nobody is stranded (§0.1).** Seams are sized and placed so that every valley keeps rock
   within reach after its first seams are cleared, measured (§6), and the harvest brush warns
   when clearing would take the last rock near the village.

**And the numbers, put to him with §6's tables (the same day):**

7. **Seams:** eight more stone seams and two more iron seams — every valley measured has an iron
   seam of 50 in reach. ⚠️ *Picked as "the four diagonals and a second ring"; built as the second
   ring's cardinals and diagonals, because the first ring's diagonals sit at (7, 7) in the
   village's plots (§3.1) — the count is his, the placement was the suite's.*
8. **The unlock at 100 stone dug by hand** — about eight seam tiles, most of one seam.
9. **The pace: four-tick digs of ten stone**, a stint of four to an armful (about 100 stone per
   hundred ticks worked against a laborer's 43); two seats; six faces a quarrier.
10. **The word is *quarrier*.**
11. **Stone is too cheap and the prices rise** — told that a village's core costs about 100 stone
    and one seam holds about 156, he chose *"raise building stone costs"* over leaving them.
    Measured before typed (§6.3); **×3** by his call.

What the code had before this spec, traced rather than remembered:

| Thing | State |
|---|---|
| Seams | `MapGenerator.PaintSeams`: 4 stone seams at ring 14 (radius-2 diamonds, 13 tiles, 12 stone a tile), 2 iron at ring 26 (radius 1, 5 tiles, 8 iron a tile); 2 draws a seam, the second-last stage before the woodland |
| Iron a seam | **at most 40 — no seam in 64 valleys reaches 50** (§6.1); seed 42 holds 4 iron tiles in the whole valley |
| Clearing | laborers only, under the harvest brush: `NearestHarvest` → `Clearing` → `Harvest`, which sets the tile to grass (D84) |
| Painted ground | forester and farm only: `KeepsWorkGround` is a `JobKind` switch; `CanPaintWorkGround` takes any land; each trade's picker decides what it works |
| A counter of stone or iron dug | **none** — `LogsEverFelled` and `ToolsEverForged` are the only *ever* counters |
| The quarry | a roadmap row on the professions panel (*"Stonecutter — nothing quarries it"*) |
| What spends stone | buildings (3 a hut … 40 the town hall); nothing else |

## 2. Which pillars this serves, and what it must not touch

- **§2.3 — expansion pressure with a remedy.** Deposits run out; the quarry is what you place
  *"when you are tired of moving"* (D84). Rock-only makes the choice spatial: a seam cleared by
  hand is a quarry site spent.
- **§0.1 — the pressure and its remedy shipping close together** (the forester's hut is the model
  §0.1 names). The unlock arrives out of the thing it answers: clearing stone.
- **§2.7 — unlock by doing.** *"The village has dug enough stone to know how to cut it"* is a
  practice gate, legible as a count the player can watch climb.
- **§1.1 — legible:** a refused paint says why in words; a quarry face looks worked; the unlock
  names what it waited for.
- ⛔ **One cost field** — the quarry's walk is the forester's walk; nothing new prices a route.
- ⛔ **No new draws** — §3.1. Draw order is the seed contract (D344, D392).
- ⛔ **Nothing rebuilt per tick** (CLAUDE.md): the quarry's tiles are read from `ZoneMap`'s owner
  index, never by scanning the map; *"is there rock near the village?"* is answered at paint time,
  not each tick.
- ⛔ **Nothing reaches `VillageEconomy`'s survival floor.** Stone has no floor (it is not food or
  fuel); the quarry is supply above it.

## 3. The rules

### 3.1 More stone and iron, placed by hash

- **The first four stone seams and two iron seams are drawn exactly as today** — the same draws,
  in the same order, at the same slots.
- **Seams beyond them take their jitter from a hash, not the stream:** `SeamJitter(valley, kind,
  index)`, where `valley` is the generator's stream state *read, not advanced*, at the start of
  step 6 (so it is per seed, and it is a value already decided). Their slots continue
  `RingSlot`'s order, **never nearer the village than the ring itself** (`MapGenerator.SeamSlots`):
  for stone, the second ring's four cardinals (21) and four diagonals (14, 14). ⛔ *Specced as the
  first ring's diagonals, and the suite said no:* `RingSlot` halves a diagonal, so those sit at
  (7, 7) — inside the founding's house plots — and eight housing guards lost their ground, rock
  lay under the first building sites, and five food and timber guards went red. Moved out a ring,
  every behavioural guard is green; told to Joe (§1, call 7).
  `stone_seam_count` / `iron_seam_count` keep meaning *drawn* seams;
  `extra_stone_seams` / `extra_iron_seams` are the hashed ones.
- **An iron seam grows until it holds `iron_seam_min_iron`** (50): painted at
  `iron_seam_radius_tiles`, then one ring wider at a time while `tiles × yield_per_tile` falls
  short (water and the map's edge clip a diamond). Radius costs no draws.
- **Over grass only, as today.** Woodland is painted after the seams and still never over them.
- ⭐ **The woodland reads the same draws it read before**, so every forest clump sits where it
  sat; only tiles that became rock are not forest. A guard says so (§7).
- ⚠️ **D435, found building this:** the generator passes its stream by value, so today a draw in
  `PaintSeams` would not have moved the woodland either, and every drawn seam shares one jitter.
  The hash is still the right shape — it stays right once per-stage seeds fix the stream — and it
  gives each new seam its own offset, which the drawn four do not have.

### 3.2 The ever-dug counters

`SimWorld.StoneEverDug` and `IronEverDug`, incremented in `Harvest` by the yield of a deposit tile
(the whole tile's yield, carried or left on the ground — the ground heap is still stone the
village dug). **Hashed, sparsely** (zero mixes nothing), beside `LogsEverFelled`. A quarry's digs
do **not** count toward `StoneEverDug` — it is *"dug by hand"*, the practice the unlock rewards,
and it never needs to count again once the quarry is unlocked.

### 3.3 The unlock

The quarry is **hidden from the build bar and refused by `Mark`** until `StoneEverDug ≥
quarry_unlock_stone`. One rule in the sim (`SimWorld.IsUnlocked(BuildingKind)`), read by the
view's `EarnedYet` — the library and town hall move onto it as well, so there is one gate, not
a view switch. When it unlocks, the village log says so once (*"The village has dug 100 stone by
hand. Somebody has worked out how to cut a quarry."*) and the button carries the new-building
highlight. **It is an unlock, not a gift:** its materials are paid.

### 3.4 A quarry's ground is rock

- `BuildingKind.Quarry` and `JobKind.Quarrier` (Joe's word), appended, never
  renumbered. `KeepsWorkGround` includes it.
- **`CanPaintWorkGround` refuses a tile that is not `Rock` for a quarry**, in words: *"A quarry
  works rock — this is grass."* Painted sub-tiles follow the tile's terrain (D352: a tile is held
  when any quarter is).
- **A quarry face never runs out.** Digging takes stone off the tile **without `SetTerrain`**.
- **Laborers never clear a tile a quarry holds.** `NearestHarvest` skips it, and painting the
  harvest brush over a quarry's ground is refused: *"That rock is the quarry's."* Taking the
  ground back (the card's *Take back*) returns it to a seam laborers may clear.
- **The look:** a quarry face draws as worked rock — the seam's boulders cut down and a pale
  floor — derived from *rock + held by a quarry*, with no new state (D347's `LumpOn` pattern).

### 3.5 The work: a stint at the face, carried to a store

A quarrier walks to the nearest face on their quarry's ground (the `NextGroundToWork` shape:
lowest travel cost, map order breaks ties — never an `Rng` draw), digs `quarry_dig_ticks` for
`stone_per_dig` through `BeginWork` and `YieldFor` (the tool counted: §3.7), and digs again where
they stand until their arms hold `carry_capacity` or the stone limit is met — **the woodcutter's
stint shape** (D384), up to `digs_per_stint`. Then they carry it to the nearest store accepting
stone (D385); what will not fit goes on the ground beside that store (D96). ⭐ Measured as a
**trip**, not an action (trap 134): §6.2.

### 3.6 Area, seats and demand

- **Tiles a quarrier keeps:** `quarry_tiles_per_worker` — a face never empties, so a quarrier
  needs a few tiles to stand at, not the forester's 24. `WorkGroundAllowanceFor` and the
  overstretched warning read it (a quarry branch in `OverstretchedNote`: *"…enough for 6. The
  rest will stand idle."*).
- **Seats:** `quarry_capacity`, player-staffed like every trade (D109).
- **Demand:** `LabourQuota.QuarriersWanted` — every seat while the stone limit is not met *and*
  either a site waits on stone no store holds (`waitedOn`'s test, D237) or stores hold less than
  the limit; zeroed while food comes first. The job row's `limited_by: Stone` makes a met limit
  stop the work (D139), through the general `StoppedByItsOwnLimit`.
- **Stone goes amber** on the bar when a quarry exists and quarriers are wanted but not seated —
  D378's rule (*amber is the trade's quota*), now that stone has a trade.

### 3.7 The quarrier uses a tool

`uses_tool: true` on the row. A tool takes 34 % off `quarry_dig_ticks` and adds 25 % to
`stone_per_dig` (D430), so a stone tool and an iron tool differ here as they do everywhere
(`tools-and-the-smith.md §9`).

### 3.8 Nobody is stranded

- **Enough rock, measured** (§6.1): with the hashed seams, every valley measured keeps at least
  three reachable stone seams, and rock within the reach threshold after the nearest two are
  cleared.
- **The brush warns** once a stroke when the harvest paint would take the last unpainted rock
  within `stone_reach_warn_tiles` of the founding site: *"This is the last rock near the
  village. A quarry can only be cut into rock."* — a warning, not a refusal (D86's pattern: a
  player who clears it anyway has decided).

## 4. Data

`data/sim.config.json`, each with its reason beside it; the numbers in bold are Joe's (§1):

| Key | Default | What |
|---|---|---|
| `extra_stone_seams` | **8** | hashed stone seams: the second ring's four cardinals and four diagonals |
| `extra_iron_seams` | **2** | hashed iron seams at ring 26, south and north |
| `iron_seam_min_iron` | 50 | an iron seam grows until it holds this (D395) |
| `quarry_unlock_stone` | **100** | stone dug by hand before the quarry appears |
| `quarry_logs` / `quarry_stone` | **25 / 0** | a quarry is timber sheds and a crane at the face |
| `quarry_work_ticks` | 40 | as the huts |
| `quarry_capacity` | **2** | seats |
| `quarry_tiles_per_worker` | **6** | faces one quarrier keeps |
| `quarry_dig_ticks` | **4** | a laborer's clearing |
| `stone_per_dig` | **10** | a stint of four fills an armful |
| `digs_per_stint` | 4 | as `splits_per_stint` |
| `stone_reach_warn_tiles` | 20 | the last-rock warning's reach |
| every building's `*_stone` | **×3** | §6.3's table — data AND the C# defaults |

Validated at load: counts ≥ 0, capacities and yields above zero.

## 5. Failure modes designed against

- **A balance change hiding in a worldgen change** (D344's warning): §3.1 adds no draws; §6.1
  says exactly how much rock and iron each valley gains, and the woodland guard proves the forests
  did not move.
- **The quarry skipped straight to** (D84's warning): the unlock is stone dug by hand.
- **The quarry that eats its own seam:** a face never runs out and laborers never clear it (§3.4).
- **A valley with no rock left for a quarry** (§0.1): §3.8.
- **Two travel-cost systems:** none — the quarry's walk is the one cost field's.
- **A met stone limit that does not stop the work** (D139): `limited_by`.
- **A hut idle for a reason nobody says:** the quarry's card uses the idle-note switch —
  *"Nothing to cut — the village has the stone it wants"*, *"No rock painted for this quarry"*.

## 6. Measured before typed

### 6.1 The valley — 64 shipped seeds (scratch harness `ZzSeams`, never committed)

Reachable = walkable from the founding site (no bridges yet, so a seam on the far bank is
unreachable). Steps are tiles walked.

| Arm | Reachable stone tiles (min / p10 / median) | Nearest stone (median steps) | 3rd-nearest stone seam (median) | Valleys with a reachable iron seam ≥ 50 | Reachable iron tiles (median) |
|---|---|---|---|---|---|
| today | 13 / 28 / 39 | 12 | 16 (some valleys have no third) | **0 of 64** | 10 |
| iron grown to ≥ 50 | 13 / 28 / 39 | 12 | 16 | 63 of 64 | 26 |
| + 4 diagonal stone | 26 / 54 / 88 | 10 | 12 | 63 of 64 | 26 |
| + a second stone ring + 2 more iron | 40 / 81 / 127 | 10 | 12 (max 18) | 64 of 64 (≥ 1 in every valley; median 3) | 39 |
| **✅ BUILT (D436): the second ring's cardinals + diagonals, 2 more iron, iron grown** | **53 / 84 / 106** | **12 — as today** | 16 (max 24) | **64 of 64** (median 3) | 38 |

The arms above were painted by the harness after the fact; the last row is the real generator. The
built placement keeps the nearest stone where it always was (12 steps) — the first ring's
diagonals, which the arms used, sat at 10.

### 6.2 The trip

1 tick a tile; the nearest seam sits about 12 tiles from the founding's stores (§6.1).

| | ticks a trip | stone a trip | stone per 100 ticks worked |
|---|---|---|---|
| A laborer clearing a tile (dig 4, one tile a trip) | 12 + 4 + 12 = 28 | 12 | 43 |
| A quarrier, 4 digs of 10 (an armful of 40) | 12 + 16 + 12 = 40 | 40 | 100 |
| — with a tool (3-tick digs, 12 a dig, armful 40) | 12 + 12 + 12 ≈ 36 | 40 | 111 |

*(To be re-read on the built rig — trap 136: count digs, not ticks ÷ dig ticks.)*

### 6.3 What stone is for — and why its prices triple

Stone is spent on buildings only. At the prices before this spec a village's core — two granaries
(20), two warehouses (16), a market (10), six huts (18), a lodge (12), a smithy (12), two wells
(10) — was **about 100 stone**, and **one stone seam holds about 156**: hand-clearing covered a
whole game, and a quarry would have been a milestone with nothing to do. Told so, Joe chose to
raise the prices over leaving them, and **×3** over ×2 or only the big buildings (2026-09-29):

| Building | Stone before | After (×3) |
|---|---|---|
| woodcutter's, forager's, forester's huts, farmhouse, fishing hut | 3 | 9 |
| well | 5 | 15 |
| warehouse (and longhouse) | 8 | 24 |
| granary, market | 10 | 30 |
| hunter's lodge, smithy, library | 12 | 36 |
| town hall | 40 | 120 |

The core is now about **300 stone — two seams by hand**, which is where a quarry earns its keep. The
library, the town hall and the first smithy are gifts, so their price is what a *second* one costs.

**Measured** (D420's 55 fifty-year villages, the harness painting twelve more rock tiles when it
marks the granary and warehouse — the unattended village otherwise paints four):

| Prices | Alive | Starved | Villages with a site unfinished at year 50 |
|---|---|---|---|
| before | 453 | 120 | 7 |
| ×2 | 461 | 124 | 6 |
| **×3** | **454** | **108** | **6** |

⚠️ Within the noise, and **what it cannot see:** the unattended villages build a granary, a
warehouse and huts, not a market, a lodge or a smithy, so this proves the rise is not dangerous and
says nothing about how it feels. That is Joe's play. The houses cost no stone and do not change.

## 7. How it is tested — `tests/Bclone.Sim.Tests/QuarryTests.cs`, `SeamsTests.cs`

- `TheWoodlandReadsTheSameDrawsItDidBefore` — every clump centre, over twelve seeds, is where it was
  (red: the hashed seams drawing their jitter).
- `EveryIronSeamHoldsFifty` — over the seed arm, every iron seam ≥ `iron_seam_min_iron`.
- `EveryValleyKeepsRockInReach` — ≥ 3 reachable stone seams in every valley of the arm.
- `StoneDugByHandIsCounted` / `AQuarrysDigsAreNotHandDug`.
- `NoQuarryBeforeTheVillageHasDugTheStone` — `Mark` refuses; after the count, it accepts.
- `AQuarryPaintsRockAndRefusesGrassInWords`.
- `AQuarriedTileIsStillRock` — after many digs.
- `ALaborerNeverClearsAQuarrysFace`.
- `AMetStoneLimitStopsTheQuarriers`.
- `AQuarrierDigsAStintAndCarriesItToAStore` — the rig of §6.2, counting digs.
- `TheLastRockNearTheVillageIsWarnedAbout`.

Every guard red-checked and its reds counted (D326); a zero is written here.

## 8. Definition of Done

1. This spec current, its status line true.
2. The guards above green and red-checked; the determinism test green; goldens moved once, for §3.1's
   stated reason, and once for `tools-and-the-smith.md §9`.
3. The 55-village arm read against `main`.
4. The view builds with no new warnings; the probe green, bar height 161.
5. Joe plays it: a quarry appears once stone is dug, takes rock and refuses grass in words, and its
   faces look worked and never shrink.
6. `DESIGN.md` §4/§6/§7, `mutable-terrain.md §5.2`'s quarry row, `seeded-map-generation.md` and
   `HANDOFF.md` updated.

## 9. Open, and Joe's to call

- **How the ×3 prices feel** (§6.3) — the unattended arm cannot see a market, a lodge or a smithy
  waiting on stone. Joe's play is the measurement.
- **Iron tools' numbers** (`tools-and-the-smith.md §9.4`) — 250 uses, 50 % / 35 % is his order of
  magnitude and an upper bound; re-measured with the gift in place before it is typed for good.
- **The iron mine** — the next slice (D434). Until it exists iron is finite: a valley's reachable
  iron is 12–39 tiles, 96–312 iron, ~48 iron tools.
- **The mason's yard** — out (D434); it gates the stone cottage (D206).
- **D384's open item** (builders idle on a site whose stone nobody cuts) — the quarry is one answer
  to it, not the only one; unchanged here.

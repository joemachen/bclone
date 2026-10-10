# Spec: The iron mine — painted iron that never runs out, and faces that look worked

**Decisions:** D448 (Joe's order: the iron mine next), D449 (Joe's four calls on this spec,
2026-10-01, asked before a line was written). Neighbours: D84 (**a deposit is finite, a building is
not**), D434–D438 (`quarry.md` — this slice is its iron twin and copies its shape on purpose), D444
and D446 (`tools-and-the-smith.md §9` — the smithy gift and iron tools, which this unlock reads),
D281 (every trade walks in states of its own), D326 (red-check every guard), D385 (every load to a
store), D430 (a tool: off ticks and on yield), D442/D443 (a learned unlock stops the village; the
tech-tree map).
**Status:** ✅ **BUILT (D450), PLAYED BY JOE AND MERGED (D451, 2026-10-02).** §4's numbers were put to
Joe with §6 and set by him (D449); the built rig reads 45 iron per 100 ticks between deliveries (§6).
Every guard in §7 red-checked; no golden moved. Owner: Joe + Claude Code.

---

## 1. Why this exists

The quarry spec (D434) left iron finite on purpose: *"The iron mine is the next slice; iron stays
finite and dug by hand."* A valley holds 96–312 iron in reach (`quarry.md §9`) — about 48 iron
tools — and an iron tool takes 4 iron. Once the smithy forges iron, iron is the one material the
village can run out of with no remedy. §0.1: *the pressure and its remedy ship close together.*

**Joe's calls (2026-10-01, D449), asked before a line was written:**

1. **Painted ground over an iron seam, and it never runs out** — the quarry's shape exactly. A seam
   cleared by hand is a mine site spent.
2. **Unlocked when the smithy forges its first iron tool** — dig iron → the smithy gift → the smith
   works iron → the village learns to mine. Practice, legible as a chain on the tech-tree map, and it
   spends about one seam by hand (the smithy's 50), leaving the rest as mine sites.
3. **Slower than the quarry** — about 40–50 iron per 100 ticks worked against the quarrier's ~100
   stone. Iron is rarer and worth more; the walk (iron sits at ring 26, stone at 14) is already
   part of the price.
4. **The worked-face look ships in this slice, for quarry faces and mine faces both** — `quarry.md
   §3.4`'s deferred look. One view change serves both.

What the code has before this spec, traced:

| Thing | State |
|---|---|
| Iron seams | 2 drawn + 2 hashed at ring 26 (**22 since D543**, and valley-wide iron seams of any size on top — `seams-revisited.md`), the near ones each grown to ≥ 50 iron (8 a tile, ~7 tiles); `Terrain.IronDeposit` |
| Digging iron | laborers only, under the harvest brush (`Harvest` → grass, `IronEverDug +=`) |
| The tech tree | a horizon node `"mine"` — *"The iron mine"*, `NotYet`, requires `"smithy"`, unlocks nothing (`TechTree.DefaultNodes`) |
| Iron tools forged | `ToolsEverForged` — stone and iron together, **not hashed**; no per-kind count |
| The professions panel | a not-hired row *"Miner — iron is on the map; nothing digs it"* |
| Quarry faces | drawn as the seam's boulders under the quarry's outline (D438 deferred the look) |

## 2. Which pillars this serves, and what it must not touch

- **§2.3 / §0.1** — iron stops being a dead end once the village has worked it.
- **§2.7 unlock by doing** — *"the smith has worked iron, so the village knows good ore"*: the
  tree's third rung (stone dug → iron dug → iron forged), each one a count the player can watch.
- **§1.1 legible** — a refused paint says why; a mine face looks worked; the unlock names what it
  waited for.
- ⛔ **One cost field** — the miner's walk is the quarrier's walk.
- ⛔ **No worldgen change** — no new draws, no new seams; every map is byte-identical.
- ⛔ **Nothing rebuilt per tick or per frame** (CLAUDE.md): faces come from `ZoneMap`'s owner index;
  the view re-derives which tiles are faces only when `Zones.Edits`, `TerrainGeneration` or
  `BuildingGeneration` moves (the `TraceTheZonesIfTheyMoved` shape).
- ⛔ **Nothing reaches the survival floor** — iron has none.

## 3. The rules

### 3.1 One face code path, two trades

The quarry's work is generalised rather than copied: **a face trade is a job row that names the
terrain it works** — `works_face` on `JobRow` (`Rock` for the quarrier, `IronDeposit` for the
miner). The good is `TerrainRules.Yields(face)`; the pace is the trade's own keys (§4).
`IsQuarryFace` becomes `IsFace(tile)` — the tile's terrain equals its owner's `works_face` — and
everything that read it (the harvest skip, the brush refusal, `NextFaceToQuarry`) reads that.
⭐ **The quarry must play exactly as before** — every quarry guard green and unmoved is the check.

- `BuildingKind.Mine` (18) and `JobKind.Miner` (10), appended, never renumbered. The word is
  *miner*; the building *iron mine*. `VillagerState.TravelingToMine` / `Mining` of its own (D281:
  a shared walk state misclassifies the errand), an `ErrandKind` arm, `IsOnAWorkErrand`.
- `KeepsWorkGround` includes it; `TilesOneWorkerKeeps` reads `mine_tiles_per_worker`.
- **`CanPaintWorkGround` refuses ground that is not the trade's face**, in words: *"An iron mine
  works an iron seam — this is rock."*
- **A face never runs out**: a dig takes iron without `SetTerrain`, and never counts toward
  `IronEverDug` (that counter is *dug by hand*).
- **Laborers never clear a mine's face**; the harvest brush refuses it: *"That iron is the mine's —
  it is dug there, never cleared."* Taking the ground back returns it to a seam.

### 3.2 The work

As the quarrier's (`quarry.md §3.5`): walk to the nearest face of the mine's ground (lowest travel
cost, map order breaks ties — never an `Rng` draw), dig `mine_dig_ticks` for `iron_per_dig`
through `BeginWork` / `YieldFor` (the tool counted), dig again where they stand while the next dig
fits `carry_capacity`, the stint is under `mine_digs_per_stint` and the iron limit is not met; then
carry it to the nearest store accepting iron (what will not fit goes on the ground beside it).
`uses_tool: true`; `limited_by: Iron`. No skill row (the quarrier has none either — `skills-catalog`
rows for the face trades are the open item beside the fisher's and hunter's).

### 3.3 Seats and demand

- `mine_capacity` seats, player-staffed (D109).
- `LabourQuota.MinersWanted` — every seat of every standing mine that has iron painted, while the
  iron limit is not met; zeroed while food comes first. **Taken after the quarrier** (`KindsInOrder`:
  woodcutter, smith, quarrier, **miner**, marketer, builder), in the quota and the allocator alike.
- `WhyTheMineIsIdle` (the idle-note switch): *"Nothing to dig — the village has the iron it
  wants"*, *"No iron painted for it — give it ground on an iron seam."*

### 3.4 The unlock — the smith's first iron tool

- **`SimWorld.IronToolsEverForged`**, incremented where the forge completes when the good made is
  `Goods.IronTools` (`BehaviorSystem`'s `Forging` arm, beside `ToolsEverForged`). **Hashed sparsely**
  (zero mixes nothing) beside `IronEverDug` — it now decides something, so it is state.
- `WhyNotYet(Mine)` while `IronToolsEverForged < mine_unlock_iron_tools` (1): *"Nobody knows how to
  sink a mine yet — the smith has not worked iron. Set a smithy to forge iron tools."* `Mark`
  refuses with it; the view's `EarnedYet` reads `IsUnlocked`, the quarry button's shape.
- **The tree's `"mine"` node becomes real:** `Condition = IronToolsForged` (a new `TechCondition`),
  `Unlocks = Mine`, still requiring `"smithy"`. `ProgressOf` → `(IronToolsEverForged,
  mine_unlock_iron_tools)`; `HowFar` → *"0 of 1 iron tool forged"*.
- **When it unlocks, the village stops for it** (D442): one moment, waiting to be dismissed — *"The
  village learned to mine. The smith has worked iron; the village knows good ore when it sees it. An
  iron mine can be dug into an iron seam."* — raised on the edge, through `LearnedByDoing`. **An
  unlock, not a gift:** its materials are paid. The bar button is lit gold until the first mine is
  marked.

### 3.5 Nobody is stranded

The last-rock warning generalises: painting the harvest brush over **the last iron the village can
reach** warns — *"This is the last iron the village can reach. Cleared, it is gone for good — and an
iron mine can only be dug into an iron seam."* — a warning, not a refusal (D86). The same per-stroke
reach check, keyed on the tile's terrain; never per tick.

### 3.6 The worked face (the view — quarry and mine)

- **A face** — a seam tile held by its face trade — draws **worked**: the seam's lumps cut down
  (fewer and lower, from the same stateless hash, so nothing flickers) on a **pale floor**
  (spoil-coloured: stone faces paler grey, iron faces rust-pale). The rest of the seam is unchanged.
  ⭐ **The floor is the face workplace's paint, traced and filled along its curve — the farm's field
  shape (D352), drawn under the lumps** (D461). It was a square per face: at 0.46 of a half-tile a grid
  (Joe: *"it shows the grid underneath"*, D459), at 0.5 a staircase round a round brush stroke (*"looks
  like steps"*). Now it is the workplace's sub-tile cells on its own seam, in `TraceTheZonesIfTheyMoved`
  on the same counters, so it meets the work-ground outline exactly.
- **The cut lumps are built in `VillageMap.Meshes`' chunk cache**, not per frame: a face set (`bool[]` per tile) is
  re-derived from the face workplaces' `Zones.WorkGroundOf` lists **only when `Zones.Edits`,
  `TerrainGeneration` or `BuildingGeneration` moves**, diffed against the last one, and the chunks
  whose tiles changed are rebuilt — exactly as a felled tile rebuilds one chunk today.
- **The far view** (below `TreeZoomFloor`) shows the floor too since D461 — a traced fill draws at every
  zoom, as a field does; the lumps are near view only.
- **Probe:** a `faces:` line — mark a quarry face and assert exactly one chunk rebuilds and the
  tile's lumps are the cut-down set (one 36-vertex lump fewer since D461); release it and assert it
  reverts. ⚠️ The traced floor is not in the chunk and cannot be seen headless (D340).

### 3.7 The rest of the view

The bar button (Resources group, beside the quarry), the gold "new" tint, `TradeGlyph` /
`BuildingGlyph` for the miner and the mine (a pick over ore; a headframe over ore), the
professions panel's not-hired *Miner* row deleted (the trade now has a building), the map's
`ColourOf(JobKind)` given miner — and quarrier and smith, which silently fall to the market's colour
today — arms, and `Describe(JobKind)`. ⚠️ **Iron is not on the resources bar** — a miner's output is
invisible except on store cards; ✅ **added (Joe, D449)** — the probe's `bars:` line caught the bar
running 7px under the right column, so the cells sit 7 apart, not 9 (bar height stays 161).

## 4. Data

`data/sim.config.json`, each with its reason beside it, and the C# defaults:

| Key | Default | What |
|---|---|---|
| `mine_unlock_iron_tools` | **1** | iron tools forged before the mine appears (Joe, D449) |
| `mine_logs` / `mine_stone` | 30 / 15 | shoring timbers and a stone-lined shaft head |
| `mine_work_ticks` | 40 | as the quarry |
| `mine_capacity` | 2 | as the quarry |
| `mine_tiles_per_worker` | 3 | an iron seam is ~7 tiles: two miners hold most of one |
| `mine_dig_ticks` | **6** | §6 |
| `iron_per_dig` | **5** | §6 |
| `mine_digs_per_stint` | **8** | eight digs fill an armful of 40 |

Validated at load: capacities, tiles, ticks, per-dig and per-stint above zero; the unlock ≥ 0; a
`works_face` names a deposit terrain that yields a good.

## 5. Failure modes designed against

- **The quarry changed by being generalised** — every quarry guard unmoved; the rig's 99 stone
  per 100 ticks re-read.
- **A mine skipped straight to** — the unlock is an iron tool forged, which needs the smithy, which
  needs 50 iron dug by hand.
- **The mine that eats its own seam** — a face never runs out and laborers never clear it.
- **A valley with no iron left for a mine** — §3.5's warning; the smithy's unlock spends ~one seam
  of four.
- **An unlock nobody can see coming** — the tree shows *"0 of 1 iron tool forged"* under the smithy
  from the moment the smithy is known; the refusal sentence says to set the forge to iron.
- **A smithy left on stone for ever** — the mine never unlocks; the tree and the refusal say why.
  That is the player's choice, legibly.
- **A view cache rebuilt per frame** — §3.6 keys it on three counters; the probe asserts one chunk.
- **The modded fixtures' "free" ids** — `ModdedJobTests` / `ModdedBuildingTests` use `(JobKind)10`
  as *an id the enum cannot name* (trap D348, D278): the boatman moves to 11, the catalogues gain
  the mine's row, the counts move by one.

## 6. Measured before typed — the trip

1 tick a tile; iron seams sit at ring 26, so ~26 tiles from the founding's stores each way.

| | ticks a trip | iron a trip | iron per 100 ticks worked |
|---|---|---|---|
| A laborer clearing an iron tile (dig 4, one tile a trip) | 26 + 4 + 26 = 56 | 8 | 14 |
| **A miner, 8 digs of 5 at 6 ticks (an armful of 40)** | 26 + 48 + 26 = 100 | 40 | **40** |
| — with an iron tool (4-tick digs of 6; 6 digs fill 36) | 26 + 24 + 26 = 76 | 36 | **47** |
| *A quarrier, for comparison (stone, ring 14)* | 12 + 16 + 12 = 40 | 40 | *100* |

Inside Joe's 40–50. An iron tool (4 iron) costs about ten ticks of a tooled miner's work.

**Re-read on the built rig (D450):** the fixture's nearest iron is 24 tiles from the warehouse (cost
260). With the founders' stone tool in hand a miner delivers **36 iron a trip, 108 iron in 240 ticks
between the first delivery and the last — 45 per 100 ticks**, against §6's 47 with a tool.
`AMineDigsIronAndItsFacesStayIron` prints **31** over its two seasons because it counts the first
walk out and the unfinished last trip; that guard asserts the face, not the rate. *(Counted as
deliveries into the store — trap 136.)*

## 7. How it is tested — `tests/Bclone.Sim.Tests/MineTests.cs`

Red-checked (D326) — thirteen sim mutants run against `MineTests`, `QuarryTests` and
`TechTreeTests`, the reds counted; **every mutant scored at least one**:

| Break | Reds |
|---|---|
| no mine gate | 3 — `NoMine…`, `TheFirstIronTool…`, `TheTreesMineNode…` |
| stone tools counted as iron | 1 — `TheFirstIronToolTeachesTheVillageToMineOnce` |
| a moment on every iron tool | 1 — the same |
| the counter unhashed | 1 — `IronToolsForgedAreInTheFingerprint` |
| any ground but water takes a face's paint | 2 — the mine's and the quarry's paint guards |
| no `ErrandKind` arm for the miner | 1 — `AMineDigsIronAndItsFacesStayIron` |
| a dig spends the face | 2 — the mine's and the quarry's rigs |
| laborers do not skip faces / the brush marks them | 2 / 2 — `ALaborerNeverClears…` (mine, quarry) |
| want nought miners | 1 — `MinersAreWantedWhileTheVillageWantsIron` |
| a met limit ignored | 2 — the mine's and the quarry's limit guards |
| the last-seam warning on rock only | 1 — `TheLastIronTheVillageCanReachIsWarnedAbout` |
| the tree's node left `NotYet` | 1 — `TheTreesMineNodeReadsTheMinesGate` |

⚠️ Five mutants first failed to **compile** (CS0162 unreachable code, IDE0060 an unused parameter)
and so scored nothing; re-posed as conditions that never hold, all five went red. *A mutant that
does not build is not a red check* — read the build line, not only the count.
**View:** the `faces:` probe line, with the face diff broken (`faces[i] && !faces[i]`): *"working
one rock tile rebuilt 0 chunks … a face is not following its owner"* — red.

**Goldens:** none should move — no map change, a village that never forges iron tools hashes as
before (the counter is sparse). Any golden that moves is a bug in this slice.

## 8. Definition of Done

1. This spec current, its status line true.
2. The guards green and red-checked, reds counted; determinism green; **no golden moved** (`git diff`).
3. The rig re-read against §6; the numbers put to Joe.
4. View builds with no new warnings; probe green — bar height 161, tile centres ✅, `faces:` ✅.
5. Joe plays it: forge an iron tool, the village stops to learn mining, a mine takes iron and refuses
   rock in words, its faces — and the quarry's — look worked and never shrink.
6. `DESIGN.md` §4/§6/§7, `quarry.md §3.4` (the look, built), `tools-and-the-smith.md`,
   `tech-tree-map.md`, `HANDOFF.md` updated.

## 9. Open, and Joe's to call

- **The numbers in §4** — put to him with §6 before they are typed.
- ~~**Iron on the resources bar** (§3.7).~~ ✅ Joe: *yes* (D449). Second row: logs, firewood, stone,
  **iron**, stone tools; the bar's cell gap 9 → 7 so it still clears the right column at 75 %.
- **Skill rows for the face trades** — beside the fisher's, hunter's and smith's (D391's open item).
- ~~**The far view's faces** — baked into the valley texture, later.~~ ✅ The traced floor draws far too (D461).
- **The smelter** — `tech-tree.md §9.4`; iron goes to the smith raw until then.

# Spec: Bridges — wooden, then stone

**Decision:** D40 (2026-07-27: *"water is impassable until the village learns to bridge it — a technology, then a
building. Wood bridges first, stone upgrades later"*), D546 (Joe's calls, 2026-10-10), D547 (measured), D548 (built),
D549 (placed by drag, at any angle — Joe, after playing it).
**Status:** ✅ **built, played and merged (D546–D551)** — sim and view; Joe played both the first placement (D548) and the
drag (D549: *"everything is good except let bridges be longer than 8 tiles. 12 max."*, D550). `BridgeTests` (16) green and red-checked — 25 mutants, 37 reds, no zeros (§11); the probe's `bridge:` line
red-checked (3 of 3). No golden moved. Slice 3 of
`seeded-map-generation.md §11`; the *"Slice 3 — bridges — is a separate spec"* that `pathfinding-and-water.md §10`
promised.

---

## 1. Why this exists

Joe, 2026-10-10: *"I want to add bridges to the game (basic/wooden -> stone) so villagers can cross water."*

Water has been impassable since D41, and the river has been **enforced scenery** ever since
(`pathfinding-and-water.md §12.3`): the founding chooses the biggest land mass, so nothing the village needs is
ever across it. D543 then put stone and iron across the whole valley, *"seams across the river kept — we'll
eventually add bridges"*. **Measured (§8.1): across the river lies 37–42 % of every valley's land, woods, rock and
iron, in every river course.** It is the largest piece of the map the player cannot use, and a bridge is the one
decision that opens it.

**Pillars.** D40 named four and they still hold: §2.5 *terrain dictates viability* (the river is a wall until you
answer it); §2.7 *the tech tree attaches to something you can point at* (a village learns to bridge, then learns
stone); §2.6 *desire paths get a genuine funnel* (every crossing runs through one deck); and placement (D38) whose
value the player can see before committing. §1.1: *"they live on the wrong side of the water"* stays a sentence the
game can say, and now *"…and the bridge is how they get home"* is one too.

---

## 2. Joe's calls (D546, 2026-10-10)

| # | Question | Joe's answer |
|---|---|---|
| 1 | What unlocks the wooden bridge? | **Village size** (SCALE — `tech-tree.md §9.6`'s own row) |
| 2 | What does stone give over wood? | **A quicker deck, and it lasts where wood wears** (same spans) — rebuilt in place |
| 3 | What unlocks stone? | **Crossings walked** over wooden bridges (DOING) |
| 4 | Can people cross while wood is rebuilt in stone? | **Yes — it stays open** |
| 5 | Wear needs building decay, which does not exist | **Bridges first, wear later** — with Phase 5's maintenance slice (✍️ specced D552, `building-condition.md §4.4`: a failing wooden deck's leg takes 150 %, a ruined deck closes, stone is a row that never wears) |
| 6 | When wear lands, an unrepaired wooden deck… | **Slows, then closes** — warned and drawn worn well ahead; never closes with anybody on it |

⚠️ Call 1 resolves a contradiction between two docs: `tech-tree.md` said SCALE and `buildings-plan.md` said *"by
knowledge"*. Both now say SCALE.

**Mine, stated so Joe can overrule:**
- **One tile wide.** D40's funnel: *every crossing runs through one tile*.
- ~~**Quarter turns only**~~ and ~~**the player clicks on the water; the sim stretches the deck bank to bank**~~ —
  ⛔ **overruled by Joe in play (D549)**: *"i hate it"*. See the table below and §4.

### 2.1 Joe's calls on placement (D549, 2026-10-10, after playing D548)

| # | Question | Joe's answer |
|---|---|---|
| 7 | How is a bridge placed? | **Press on the tile where it begins, drag to where it ends, release — a straight line in ANY direction** |
| 8 | Pressed or released back from the water's edge? | **Exactly as dragged** — both ends on dry land, priced per tile of its whole length |
| 9 | How long may it be? | ~~**8 tiles end to end**~~ → **12** (D550, after playing it: *"let bridges be longer than 8 tiles. 12 max."*) |
| 10 | Rebuilding a wooden bridge in stone? | **A plain click on it with the stone bridge in hand** — a drag draws a new stone one |

Settled without asking: the ends follow the Snap setting (tile centres, or free); R does nothing to a bridge; Esc or a
right-click lets go of a drag.

---

## 3. What a bridge is

> ⭐ **A deck over the water — not a new kind of ground, and not an obstacle.**

- **The tiles stay `Terrain.Water`.** The fishery still touches water, the valley still draws its river, the
  hash's terrain bytes do not move, and the generator's `StepsToWater` is untouched. Three things were possible
  (a terrain value, a building footprint, an overlay); only the overlay changes nothing it does not mean to.
  - A terrain value would re-bake the valley, move the terrain bytes and make every `== Terrain.Water` check in the
    sim a question about bridges.
  - A building footprint is closed to walkers since D383 (`Block`), and a bridge is the one structure whose whole
    point is to be walked across.
- **A finished bridge is a `Bridge`**, its own kind of standing thing, like a well (`World/Well.cs`): two ends (the
  **abutments**, land tiles on either bank), the water between them (the **deck**), a material (wood or stone), a
  name, the tick it was raised and how many crossings it has carried.
- **The deck index**: one byte per tile — what a step onto its deck costs, 0 where there is none — rebuilt from the
  bridges when the standing generation moves, never per tick (CLAUDE.md), **never hashed** (D335: it restates the
  bridges, it is not a second fact). ⭐ *As built:* **a deck change is a standing change** — finishing, rebuilding or
  pulling down a bridge calls `StandingChanged`, so the cost field forgets its routes and the wall-off sweep re-runs
  exactly as they do when a hut goes up. The `DeckGeneration` this line first proposed would have been a second
  counter every caller had to remember; the cost field asks `IObstacles.DeckOn` and nothing else.
- **Walkable = dry ground, or a deck.** One rule (`SimWorld.IsWalkable`), read by everything that asks *can a
  villager stand here?*. Everything that asks *what ground is this?* still sees water (§6).

### 3.1 Walking a deck

- A deck tile costs **`bridge_wood_tile_cost` 10** (grass) for wood and **`bridge_stone_tile_cost` 8** (packed
  ground) for stone, **fixed**. The deck takes **no path wear** and draws no trail: a bridge is already a road. ⭐ **Since D555 the wooden bridge itself wears** (`building-condition.md §4.4`): failing, its deck is walked at 150 % of the ticks (the route unchanged — the byte is already the dial's maximum); a ruin closes it, never with anybody on it and never when it is the only way somewhere; the builders mend it from the near bank for a quarter of its logs. Stone never wears.
  Both stay inside Dial's 1..10 (`TerrainCostField.Refill` throws otherwise).
- **Crossings**: a walker whose step goes from land onto a deck has crossed. Counted on the bridge (the card) and
  in the village's `BridgeCrossings` (the unlock, §5.2). Hashed once non-zero, so it is zero in every golden today.

---

## 4. Placing one — press, drag, release (Joe, D549)

> ⛔ **REWRITTEN BY D549.** The first build clicked on the water, stretched the deck bank to bank along a facing and
> turned it by quarters with R. Joe played it: *"the bridge works as expected, but i want to change how it is placed.
> presently it is a straight line and only 'R' changes the orientation. i hate it. user should click and hold on one
> tile where they want the bridge to begin - then drag where they want the bridge to end, and release. it should
> work as a straight line in ANY direction, wherever the ending is placed."*

**`SimWorld.BridgeLineFrom(kind, Point from, Point to)` is the one rule**, read by `CanBridge` (the ghost's verdict
while dragging), `MarkBridge` (the release) and the probe, so the ghost can never show a bridge the sim would not build.
The two points are where the press and the release land, snapped to tile centres or free as the Snap setting says.

1. **A click — press and release on one tile** — is not a bridge. With the stone bridge in hand over a standing wooden
   one, it **rebuilds that bridge in stone**, same line (Joe, D549 call 3). Otherwise: *"Press on one bank and drag to
   the other."*
2. **Both ends on dry land**: *"A bridge begins and ends on dry land."*
3. **Water somewhere under the line**: *"A bridge has to cross water — drag from one bank to the other."*
4. **At most `bridge_max_length_tiles` 12 end to end** (Joe: 8 in D549, **12 in D550** after playing it): *"That is N
   tiles end to end — a bridge can be 12 at most."* Every valley has a square-across crossing of 4 water tiles or fewer
   (D547), a bridge of 5 at most, and an angle only adds choices — so the cap is how far a bridge may run, never whether
   a valley can be crossed.
5. **Nothing under the line** — not a building, a yard, the lane in front of a gate, or another bridge or bridge site
   — each said with its name (*"It would run through the Ashfords' yard."*). Land under the middle is otherwise
   allowed: it is walkable already. Trees and rock refuse nothing; a bridge never asks for clearing.
6. **The near end is the one the village walks to more cheaply**, whichever way it was dragged, and the far end need not
   be reachable at all; neither reachable is *"There is no route to there from the village."*
7. Nothing else may be built or sited on a bridge's ground while it stands or is planned (`SomethingStandsAt` answers
   true there, and `CanBuildAt` says *"A bridge lands there — it would close the crossing."*). ⚠️ Painting is not
   refused: a field, a forest or a quarry face is still walkable ground, so it cannot close the crossing.

**Any angle, because the tiles are the line's** (D549): a bridge covers `LineOfSight.TilesCrossed` of its segment — the
exact grid walk that at a grid corner lists *both* tiles beside it — so its tiles always join edge to edge and the
4-connected cost field walks a deck at 23° as well as at 0°. That was D412's trap, and the reason for the quarter turns
it replaces. The deck is the water among those tiles; `LineOfSight.Clear` asks about the same tiles, so villagers walk a
deck at an angle in a straight line.

**Priced per tile of length**, end to end and rounded up (Joe, D549 call 1: *exactly as dragged*), so a long land
approach costs more and the narrows are still worth looking for. `Bridge.LengthOf` is exact in integers (D2).

| | per tile of length | a bridge 5 tiles long | compare |
|---|---|---|---|
| wooden bridge | **8 logs, 10 ticks** | 40 logs, 50 ticks | a hut: 25 logs + 9 stone, 40 ticks |
| stone bridge | **12 stone + 4 logs, 15 ticks** | 60 stone + 20 logs, 75 ticks | a smithy: 25 logs + 36 stone, 40 ticks |

*(Starting numbers, written down so they can be argued with; nothing in the survival floor depends on them.)*

---

## 5. Building, unlocking and rebuilding

### 5.1 The site

A bridge is raised by builders from the builder's hut like anything else (D108, D110). **The site stands on the
near abutment** — a one-tile site on dry ground, where builders deliver and work — and carries the planned span
(`ConstructionSite.BridgeTo`). ⛔ **Nobody stands on the water to build it**, and the deck opens only when the bridge
is finished: a crossing half-built is a crossing not yet there. The site reserves its far abutment and deck (§4.7).
- ⭐ *As built:* **a bridge's site is not an obstacle** (`StandingShapes` and `SomethingOverlaps` skip it). Every other
  site closes its tiles (D383); this one stands on the bank it is built from, and closing it would close the crossing
  while a wooden deck is rebuilt in stone — the exact thing Joe's call 4 forbids.
- ⭐ *As built:* **a bridge's site never waits on clearing** (`FirstTileStandingUnder`): trees and rock are walkable, so
  nobody is ever asked to clear an abutment, and a wooded bank would otherwise hold its builders for ever.

### 5.2 Unlocks

Both are latched, saved, and hashed only once they are set, so no golden moves until a village earns them.

- **The wooden bridge: the village has held `bridge_unlock_population` 10 people.** ⭐ *As built, not a latch:*
  `SimWorld.MostEverAlive` is **derived from the villagers' own arrivals and deaths** (a founder from the first tick,
  everybody else from birth), so it is neither saved nor hashed and a village that shrinks back cannot forget — the
  history it is read from is already state. Recomputed only when somebody is added. A stop (*"The village learned to
  bridge water"*), `WhyNotYet` while locked (*"Nobody here has bridged water yet — the village has held N people, and
  learns it once it has held 10."*). Tech node `bridging` (SCALE).
  - ⭐ **When a band's arrival is what teaches it, the nomads' moment says it and the bridge raises none (D576).**
    Joe played it and got two stops at once — the bridge's and the nomads'. The number stays **10** (he asked for 15;
    measured first, §8.2a, 15 would not have parted them and would have put the bridge off a decade or more). `Settle`
    returns `LearnedToBridgeJustNow` and `Knock` adds a second paragraph to its body, marked with the bridge
    (D577, Joe: *"break it into 2 paragraphs … a bridge icon and a nomad icon"*) and crediting the bridge to the
    band: *"On the long road here they learned to lay timber across running water. The village can build wooden
    bridges now, and the far bank is within reach."* A birth that teaches it still raises *"The village learned to
    bridge water"*, now marked with the bridge too. Moments are not hashed: no golden moved.
  - ⚠️ **Its stop is `RaiseMoment`, not `LearnedByDoing`**: it is learned by the village's size, not by anybody's
    hands, so it does not introduce the tech-tree map — the library's and the town hall's rule (D440). Through
    `LearnedByDoing` it set the hashed `ShownTheTechTree` in every village that reached ten first and **moved four
    goldens for a popup**; with it switched off they were byte-identical, which is how the cause was proven.
- **The stone bridge: `stone_bridge_unlock_crossings` 150 crossings walked over the bridges** (§8.3). Tech node
  `stone-arch` (DOING, `LearnedByDoing` — it *is* learned with the feet), hangs from `bridging`. *"Feet have crossed
  the village's bridges N times, and somebody has worked out how to set a span in stone."* A crossing is a step from
  land onto a deck (`BehaviorSystem.Travel`), so one walk across is one crossing however long the span.

### 5.3 Rebuilding in stone

- **Stone is a second row** (`BuildingKind.StoneBridge`), not a mechanism: the house ladder's shape (`Building.cs`,
  *"three rows … and no new mechanism"*) and Phase 7's (*rows, not enums*).
- It may be dragged fresh like a wooden one, or **clicked on a standing wooden bridge** (D549), which raises a site
  carrying `RebuildsBridge` (the relocation's shape, `MovingFrom`). **The wooden deck carries traffic until the stone one
  is finished** (Joe, call 4); then the deck's byte flips to stone and the routes re-price. The wood is not
  refunded — it is what the stone was set on.

### 5.4 Pulling one down

Instant, like a well, with the usual share of materials back — but **refused in words** when:
- somebody is on the deck: *"Somebody is on the bridge."*
- it would cut off a standing building or home from the village (the wall-off sweep, `WhatThisWouldWallOff`'s
  question asked with the deck removed): *"Pulling it down would cut off the Ashfords' house across the water."*

---

## 6. What changes meaning — every water and passability check, classified

| Where | Asks | Becomes |
|---|---|---|
| `TerrainCostField.Scratch.Block` | can a villager stand here? | **walkable** (deck reads true) |
| `TerrainCostField.Build` (static, no obstacles) | — | unchanged: the pure field, no world |
| `LineOfSight.Clear` / `ClearOfWalls` | can a straight leg cross here? | **walkable** |
| `SweepTheFreeGround`, `TheFreeGroundAfter` (wall-off) | is this ground the village can walk? | **walkable** |
| `GateOpensAt` (lane in front of a gate) | can the lane be walked? | **walkable** |
| `CanBuildAt` anchor / covered tiles in water | can a building stand here? | unchanged (a deck is water) |
| residential and work-ground paint | can this be painted? | unchanged, and an abutment refuses too |
| starter zone, candidate searches, `Household` siting | can a house stand here? | unchanged, and an abutment refuses too |
| fishery `MustTouch` | is water beside it? | unchanged — a fishery may stand by a bridge |
| `SetTerrain`'s `routeAffecting` | did passability change? | unchanged (terrain under a deck is never edited) |
| `MapGenerator` (founding, woodland, components) | the generated valley | unchanged — no bridges at generation |

---

## 7. Saved, hashed, drawn, told

- **Save**: `"bridges"` (each bridge's two end points, material, name, raised-at, crossings) and `"bridge_crossings"`.
  A site's far end rides on its `ConstructionSite`. **`SaveGame.Format` 2 → 3 (D548) → 4 (D549**, when the ends became
  points — a save from the branch's first build is refused rather than misread). No migrations before v1. The deck index
  is rebuilt on load.
- **Hash**: the bridges sparsely (none → today's hash), their ends as `Fixed` raw bits; the crossings once set.
- **The view** (D549): with a bridge in hand a mark shows where it would begin; **press on a bank, drag, release** — the
  ghost is the line from the press to the cursor, coloured by the sim's verdict, and its sentence is the sim's reason.
  R, Shift+R and the middle-drag do nothing to a bridge. A deck is drawn over the baked river along its line — planks
  for wood, joints for stone — and a site as the planned line filling from its builders' end.
  The button sits on the Works shelf, hidden until learned, lit gold when it is. A bridge card: material, span, the
  crossings this year and in all, and on wood *"The village learns stone after N crossings (k so far)."* The river's
  ground sentence loses *"Nobody can cross it"*. A probe line `bridge:`.

---

## 8. Measured (D547)

### 8.1 What lies across the river — `tools/harness/ZzBridges.cs`, `Valley`, 100 valleys × 4 courses, shipped config

The founding's land is a flood fill from the founding site; "across" is every other land tile.

| course | land across (median, p10–p90) | woods across | rock across | iron across | narrowest crossing | crossings ≤ 4 / ≤ 6 (median) |
|---|---|---|---|---|---|---|
| we | 37 % (22–48) | 36 % | 34 % | 35 % | 3 | 122 / 126 |
| ns | 37 % (27–48) | 38 % | 29 % | 31 % | 3 | 82 / 84 |
| nwse | 42 % (30–49) | 41 % | 42 % | 39 % | 2 | 69 / 119 |
| swne | 42 % (30–49) | 40 % | 39 % | 41 % | 2 | 72 / 120 |

- **Every valley of the 400 has a crossing of 4 or fewer.** A straight river is 3–4 tiles across (`river_width_tiles`
  3 + a wander of 0–1); a diagonal one crossed square-on is 2–6. The longest straight runs (up to 46) are runs
  *along* the river, which the cap of 6 refuses.

### 8.2 When a village holds 10 — `ZzBridges.Growth` and Joe's own saves

| | first year at 10 people | at 12 | at 15 |
|---|---|---|---|
| Joe's saves (three played villages, 22–59 years) | **11, 12, 11** | 27, 27, — | 31, 33, — |
| harness `every` (13 valleys, unattended) | 9–14 (10 of 13) | 20–26 | 28–33 |
| harness `shipped` (30 valleys, unattended) | 8–13 (5 of 30) | 24–27 | 29–31 |

- **10 lands at Year 11–12 in play**, the D546 target window (years 8–12). 12 would be Year 27: the second
  generation's children, a long wait. ⚠️ A harness is not a player (D447); Joe's saves are the number, the harness
  the floor under it.

### 8.2a With nomads on — `ZzBridgeKnock`, the same 43 villages, fifty years (D576)

The first year each village held N people, and whether that tick was a knock (a band's arrival carried it over):

| | reached 10 | on a knock | reached 15 | on a knock | first year at 15 |
|---|---|---|---|---|---|
| `shipped` (30, unattended) | 25 | **24** | 16 | **16** | 17–45, median ~25 |
| `every` (13) | 11 | 6 | 11 | **11** | 13–23 |

- **A band adds about four people in one tick, so whatever the number, a band is what crosses it** — 27 of 27 at 15.
  Raising the number moves the double stop later; it does not part it. Hence one stop (§5.2), not a bigger number.
- 14 of 30 shipped valleys never hold 15 in fifty years. ⚠️ Unattended, and a harness is not a player (D447) — Joe's
  pre-nomad saves held 15 at Years 31–33 (§8.2).

### 8.3 Crossings a year — `ZzBridges.Crossings`, 43 villages (30 shipped, 13 `every`), Years 5–35

A played opening, a wooden bridge raised free at Year 5 at the crossing nearest the founding, a forager's hut marked on
the far bank, and an 8×8 patch beyond it painted for homes.

- **Median 23 crossings a year** (the quiet villages 12, the busy 60); 36 of 43 crossed at all (the rest never staffed
  the hut — a village of four).
- ⚠️ **Without the hut, every village crossed zero times in thirty years**: the unattended chooser never sites a home
  across while near-side paint is left. A bridge nothing needs is an idle bridge, not a measurement.
- **`stone_bridge_unlock_crossings` = 150**: about six to seven years of one bridge in real use (2–3 for a busy one,
  about 12 for a quiet one). The placeholder it replaced, 2,000, would have been ninety years.

---

## 9. Edge cases and failure modes

- **Somebody on undecked water** — the regression the whole of D41 exists to prevent. Asserted every tick in a long
  run with a bridge built and pulled down.
- **A route through a site** — a half-built bridge must not route anybody, and a builder must not step onto water.
- **A deck pulled down under a walker** — refused (§5.4).
- **A village stranded by its own demolition** — refused (§5.4).
- **A house sited on an abutment** — closes the crossing it stands on. Refused (§4.7).
- **Stale routes** — the deck changes only through `DeckGeneration`, the way a wall does; no caller has to remember
  to forget.
- **Determinism** — the index is derived from the hashed bridges in a stated order; the crossings counter moves on
  the sim tick only.
- **The suite's clock** — a deck change drops every flow field, but only when a bridge is finished, rebuilt or
  pulled down: a few times a run, not a tick.

---

## 10. Testing (TDD; every guard red-checked and its reds counted, D326)

`BridgeTests`, posed on a shipped valley with the river crossing it:
1. **Nothing changes without a bridge** — costs across the valley byte for byte as today (the anti-vacuity twin).
2. A finished wooden bridge makes the far bank reachable, and the cost across falls from unreachable to the walk.
3. Nobody ever stands on undecked water — every tick of a run in which a bridge is built, walked and pulled down.
4. The drag's rule (D549): a line at any angle is edge-joined, through an exact corner too; lengths round up exactly;
   an end on water, no water crossed, over the cap and a click on bare ground are each refused in words; dragged
   either way the near end is the village's; a building or another bridge under the line is refused.
5. A site routes nobody across, and its builders work from the bank.
6. Other buildings and homes refuse an abutment.
7. The unlock latches at 10 and stays latched when the village shrinks; `WhyNotYet` says so before.
8. Crossings are counted; stone unlocks at its number.
9. A click with stone on a wooden bridge rebuilds it, open throughout, and the crossing then costs less; a stone drag
   elsewhere is a new bridge. One walk across a bridge at an angle is one crossing, walked straight along it.
10. Demolition is refused with a walker on the deck, and when it would strand a home.
11. Save → load → hash == live, and a year on matches a village that never stopped.

---

## 11. What has landed

- ✍️ This spec and its measurements (D546, D547).
- 🔨 **Built (D548), sim and view, on `slice/bridges`:** `BuildingKind.Bridge` 21 and `StoneBridge` 22 as rows
  (`spans_water`, `deck_tile_cost`, `rebuilds`); `World/Bridge.cs`; `Core/SimWorld.Bridges.cs` (the span rule, the deck
  index, the site, the rebuild, the pulling-down, the two unlocks, the crossings); the cost field, the leg planner and
  the wall-off sweeps read `IsWalkable`; the hash, the save (format 3) and the tech tree. The view: the button on the
  Works shelf, the ghost bank to bank, R a quarter, decks drawn over the river, a bridge site drawn along its span, a
  bridge card, the river's sentence, the demolish tool, and the probe's `bridge:` line.
- **Guards** — `BridgeTests` (16). Red-checked (D326): 15 mutants, 14 caught, **21 reds**:

  | mutant | reds |
  |---|---|
  | the cost field ignores decks | 7 |
  | a crossing counted on every deck step | 1 |
  | the route prices ignore decks (stone no quicker) | 1 |
  | a bridge's site is an obstacle | 2 |
  | a bridge's ends are not "something stands" | 1 |
  | `CanBuildAt` ignores a bridge's ends | 1 |
  | the walker-on-the-deck refusal gone | 1 |
  | the stranding refusal gone | 1 |
  | the peak is today's population, not history | 1 |
  | the size stop through `LearnedByDoing` | 1 |
  | a bridge's kind not hashed | 1 |
  | bridges not saved | 1 |
  | the deck wears | 1 |
  | a bridge's site waits on a wooded bank | 1 |
  | **the leg planner ignores decks** | **0** |

  ⚠️ **The zero, written down:** a deck is a straight one-wide line, so a pulled string along it and a walk tile by tile
  are the same walk — no guard can tell them apart, and none needs to. The rule stays so the routing and the leg read
  one rule (D404's lesson for walls).
- 🔨 **Re-placed by drag (D549)**, sim and view, on `slice/bridges`: `Bridge` holds two `Point`s and derives its tiles
  (`TilesUnder`) and length (`LengthOf`); `BridgeLineFrom` / `CanBridge` / `MarkBridge` replace the span rule;
  `ConstructionSite.BridgeTo` is a point; save format 4; `bridge_max_span_tiles` 6 → `bridge_max_length_tiles` 8; the
  view's press / drag / release (`VillageMap.PressABridgeAt` / `DragABridgeTo` / `ReleaseABridgeAt`), the ghost as the
  line, R inert for bridges. **Guards** — `BridgeTests` (16, rewritten around points; two angled poses). Red-checked:
  **25 mutants, all caught, 37 reds** — the fifteen above re-run (22 reds; **the zero is gone**: on an angled deck a
  tile-by-tile walk strays off the line and `OneWalkAcrossIsOneStraightCrossing…` sees it) and ten new:

  | mutant (D549) | reds |
  |---|---|
  | the line's tiles joined at corners only | 1 |
  | length rounded down | 1 |
  | a click never rebuilds | 2 |
  | an end on water allowed | 1 |
  | no water needed | 1 |
  | no length cap | 1 |
  | nothing checked under the line | 1 |
  | the near end is always the press | 5 |
  | the deck laid on land too | 1 |
  | priced by water, not length | 1 |

  The probe's `bridge:` line, 3 of 3 (the drag does not follow the cursor; R turns a bridge; putting the tool down
  leaves a drag). ⚠️ **Its first form was vacuous twice over** and the probe itself showed it: at the founding the
  village has not learned bridges, so the ghost's verdict and the sim's were *both the lock sentence* — equal, and
  proving nothing about the drag. It now asks the drag's own rule of the sim (`BridgeLineFrom`) as well.
- ⚠️ **Not posed:** a span with neither end reachable (the shipped valley has one river, so every crossing has the
  village on one side). It is `CanBuildAt`'s shared first refusal, *"There is no route to there from the village."*
- **Found while building:** `CanBuildAt` let a well be marked on a bridge site's bank tile — the general placement path
  never asked about bridge ends (`NothingStandsOnABridgesEnd` caught it); and a bridge site on a wooded bank would have
  waited for ever on clearing nobody was asked to do (`ABridgeOnAWoodedBankIsNotHeldForClearing`).

# Spec: Every building has a door — walkers go in and out through it

**Decision:** D619 (this document; Joe's D607 call *"spurs now, real doors later (a spec of their own)"*, and his
2026-09-19 note, D395). Neighbours: D383 (buildings are obstacles — entered from the cheapest side), D388 (the lane
picks a house's door), D404–D406 (fences as walls, one gate), D605/D607 (doorsteps and spurs, drawn), D614 (one piece
of ground stays one piece). **Status:** 📝 **SPECCED (D619); ✅ Joe's five calls answered (D620); 🔨 BUILT, ✅ PLAYED BY JOE (*"it looks good"*) AND MERGED TO `main` (D626), pushed:** commit 1 the
door derived and said (D621), then **one behaviour commit (D622–D625, §11)** — doorsteps kept clear, walks and catchment
through the door, the facings nobody chose, the door priced into the economy; a farmhouse, a barn and the cart open on
every side (Joe, D624–D625). Goldens re-taken once. **🔨 The view (§9 commit 5, §12) BUILT on `slice/doors-view` (D657),
unplayed, not merged:** the door drawn on every doored building and the ghost, the ghost starting toward the nearest store,
the moved-door sentence on the cards and on marking, spurs landing on doors. Owner: Joe + Claude Code.

---

## 1. Why this exists

Joe, 2026-09-19 (D395): *"every building gets a door tile"* — only homes have one; *"I thought I observed villagers
entering homes via non-door walls."* He was right: the door is drawn and never walked. Then, playing trade (D607), with
lanes ending beside buildings instead of at them: **spurs now, real doors later** — every building a door on its front,
walkers in and out through it, and the paths wearing to the doors.

Today a building is a block you can reach from **any** side (D383): the flow field opens the destination's whole
footprint, a walker climbs in wherever is cheapest, and leaves by whichever outside neighbour is cheapest. So a lane
worn to a granary arrives at whatever face the village happens to be on, the view has to *guess* where a spur lands
(`path-shapes.md §4.G`: *"on the side the worn ground reaches, until doors say which side is the door"*), and a
house's door notch faces a lane nobody need use.

**Pillars:** §1.1 *legibility* (a building is entered where it is drawn to be entered, so the paths explain
themselves); §1.6 *traceable over clever* (one rule — the front — not a cheapest-side search the player cannot see).

---

## 2. What exists today (read 2026-10-24)

| Piece | Where | What it does |
|---|---|---|
| Facing | `Footprint.Facing` (`World/Footprint.cs:65`); saved and hashed on every building kind | Turns the footprint. **Changes nothing about walking.** Any angle (1/64-turn steps), not quarter turns. |
| Who sets it | houses: `Household.ChooseSite` faces the lane (D388/D411); everything else: the player's R on the ghost (`Game/Main.cs:1964`); founding buildings: `Angle.Zero` | |
| The house's door | `PlotShape.Door` (`World/PlotShape.cs:151`): centre + (0, −1) turned by the facing — **derived, never stored**; read only by `TilesClippedOff`, which refuses a plot whose door tile is off the map, water or built on | The one door the sim knows, and only at siting. |
| Reaching a building | `TravelCostField.FieldTo` (`World/TravelCostField.cs:422–443`) opens the destination's whole footprint for that one field | Entered from any wall-free side. |
| Leaving | `StepOff` / `CostLeaving` (`:222–268`): the cheapest outside neighbour of any footprint tile | Left by any side. |
| The last leg | `BehaviorSystem.PlanLeg` (`:4050–4152`) may cross the footprint left and the footprint entered; ends on `PlaceToStand` (the building's `Position`) | Arrival is unchanged by this spec. |
| Catchment | `LabourAllocator.CostBetween` (`:1281`) = `TravelCost.Cost(HomePlaceOf(v), workplace.Tile)` | The same field, so the same any-side rule. |
| Wall-off | `CanBuildAt` → `WhatThisWouldWallOff` (`SimWorld.cs:12740`) → `HasAReachedNeighbour` (`:13305`): **any** tile of a shape, **any** of its four wall-free sides; `TheVillagesPiece` (D614) | *"Every home keeps a reachable door tile"* (DESIGN.md §4) is this any-side sweep. No door is checked. |
| Fences | `ZoneMap.FenceEdges` (`:833–888`) never walls a house's own edges | A house is entered from its own yard too. |
| Wear | `PathWear`, sim state, hashed; trodden in `BehaviorSystem.Travel` | Paths wear wherever walkers actually go, so doors will wear lanes with no view work. |
| The drawing | `TrailNetwork.Doorsteps` / spurs (D605, D607), `KeepTheBusierSides` | View only. Guesses the door from the worn side. |

⚠️ **Name collision:** the sim already says *doorstep* for `HomePoint`, the house's centre (`SimWorld.cs:13979`,
`AVillagerAtHomeStandsOnTheDoorstep`). In this spec **the doorstep is the tile outside the door**; the build renames the
old comment's word, never the other way round.

---

## 3. Measured before specced — the premise (`tools/harness/ZzDoors.cs`, 2026-10-24)

For every standing building on Joe's five newest villages (copies of each `autosave-1.save`, format 8): the tile in
front of its facing by the house's own rule (centre + (0, −(H/2 + ½)) turned), whether that tile is walkable, empty,
unwalled from the building and reached from the village, and the walk to it against today's walk to the building.
The market, trading post and barn are both a workplace and a store, so each was counted once.

| village | buildings | front open | water | built on | fence | unreached | **another side open** | never turned |
|---|---|---|---|---|---|---|---|---|
| oakhaven-hazel-pebble-34 (Y59) | 43 | 41 | 1 | 1 | 0 | 0 | 2 of 2 | 20 |
| ashbourne-rye-rush-18 | 46 | 40 | 3 | 0 | 2 | 1 | 6 of 6 | 19 |
| millbrook-ash-willow-36 | 38 | 34 | 2 | 2 | 0 | 0 | 4 of 4 | 14 |
| oakhaven-ash-otter-53 | 36 | 33 | 1 | 2 | 0 | 0 | 3 of 3 | 15 |
| elderwood-sparrow-brook-12 (Y64) | 33 | 29 | 2 | 2 | 0 | 0 | 4 of 4 | 23 |
| **all five** | **196** | **177 (90 %)** | **9** | **7** | **2** | **1** | **19 of 19** | **91 (46 %)** |

What it says:

1. **A front onto water is the fishing hut's and the trading post's normal pose**, not an accident: 4 fishing huts
   and both trading posts face their water, as a player turns a dock. Three granaries face the river too.
2. **Built on: 7** — a warehouse, a market, a bakery, a smithy and two piles with something on the tile in front of them.
   Nothing today stops a building going up on another's front, because nothing has a front.
3. **Fence: 2, both wells**, their fronts against a yard's wall. **Unreached: 1 barn.**
4. **Every blocked front has another side open** (19 of 19), so an old save never strands a building if the door may
   move to an open side (§4.3). A rule that says *the front or nothing* would wall in 10 % of his villages on load.
5. **Nearly half the buildings were never turned** (they face north, `Angle.Zero`). Most players will not press R for a
   door they cannot see, so where the ghost points the door by default is a real call (§6, call 4).
6. **The walk barely changes on average:** from the village's first home to the front tile, 20,923 → 20,536 cost
   units (−1.8 %) over the 177. From outside a building, its front is no further than its nearest side on average.
   This is a coarse number from one point, not a survival claim. §5 is the survival claim.
7. 16 of 196 are turned off a quarter so far that the front tile sits diagonal to the door tile. The cost field steps
   in four directions, so the door's edge must be a real tile edge (§4.2).

---

## 4. The rule (the starred lines are Joe's calls, answered in D620 — §6)

### 4.1 Which buildings have a door ⭐ (call 1)

**Every roofed building:** homes, every workplace, every store (granary, warehouse, market), the library, the town
hall, and every construction and demolition site (it takes the door of the building it raises; the doorstep is kept
clear from the marking). **No door:** a **well** (drawn from all round; D604 already keeps a ring of public ground
round it), a **stockpile** (a heap, not a building), a **bridge** (walked across, not into). They keep D383's any-side
rule, unchanged.

⭐ **Built (D624–D625): a farmhouse and a barn have no door either** (Joe: *"they open onto their fields on every side,
like a well or a pile"*) — priced through one door a farmer kept one ring of field, five tiles not thirteen (§11.3);
**and nor has the cart** — a wagon, not a roofed building; with a door it closed the best ground beside the founding
site and a played opening froze (§11.4).

### 4.2 Where the door is

On the building's **front**: the edge its facing points away from, in the footprint's own frame the −y edge
(`PlotShape.cs:124`'s *"the door on the −y edge"*, which the house already uses).

- **The door tile** is, of the building's tiles whose next tile frontward is not its own, the one nearest the middle
  of the turned front edge (centre + (0, −H/2) turned); a tie goes to the tile that middle point falls in (the later in
  `CoveredTiles`' order). **The doorstep** is its four-neighbour across the front. *(Built, D621 — the first draft said
  "the tile under centre + (0, −(H/2 − ½))", which need not be a front tile once a building is turned off the quarter.)*
  For a building turned a quarter that is the tile half a tile past the front's middle; for a house it is exactly
  `PlotShape.Door` (guarded, `AHousesDoorstepIsItsPlotsDoor`), so a house's door does not move. For a house turned
  off the quarter, `PlotShape.Door` can be the diagonal tile; the siting check (`TilesClippedOff`) moves onto this
  rule's doorstep **in commit 2** (it changes which plots are refused, so it is not commit 1's), and then the two never
  disagree.
- **The front's direction is quantised to the nearest of the four tile directions** for the sim (a facing exactly
  between two turns back — anticlockwise, to the lower angle; `SimWorld.FrontOf`). The view draws the door on the turned wall where it really is. §3's 16 diagonal cases are why.
- **Derived, never stored, never hashed** (D335): it restates the footprint, the facing and what stands round it.
  Remembered per footprint until `StandingGeneration`, the walls' or the terrain's generation moves (`SimWorld.DoorAt`),
  never rebuilt per ask (Joe's rule; `ADoorIsWorkedOutOncePerChangeNotPerAsk`).
  **No save format change.**

### 4.3 A front that cannot open ⭐ (call 2)

If the front's doorstep is off the map, not walkable, has something standing on it, is walled from the door tile, or
is not ground the village reaches today (the wall-off sweep's own free ground), **the door slides along the front to
the nearest edge tile that can open, and failing that turns to the first side that can, in the order back → right →
left**, each side again from its middle outward. It is still derived. Back before the sides because a dock faces its
water and its door is landward (§3.1). ⭐ *Built (D621):* because a door slides before it turns, **a building is shut
exactly when no free tile the village reaches touches it across an unwalled edge** — D383's own test — so a building
the village reaches always has a door it can use (`ABuildingIsShutExactlyWhenNothingReachesIt`). A shut door stands on
the front's middle.

**The card's sentence** (`SimWorld.SayWhereTheDoorIs`) names the side by the compass, because the player reads a map,
not a building's left and right: *"Its door is on its north side — water lies in front of it."* The reasons: *the
valley ends* / *water lies* / *{the name} stands* in front of it, *a fence runs across its front*, *nobody can reach
the ground in front of it*; shut, *"No side of it opens onto ground the village can reach."* Nothing is said for a
door on its front, slid or not.

Once §4.5 holds, a new building's door only moves when the ground under its doorstep changes. Old saves, the river and
the founding site are the cases left.

### 4.4 Walking in and out — through the one cost field

- **A building's travel goal is its doorstep.** `FieldTo` no longer opens the destination's footprint for a doored
  building: the field is seeded at the doorstep, and **every footprint stays closed in every field.** The walk's
  **last leg** goes doorstep → the standing place (`Position`), across the door edge. **Arrival is unchanged**
  (`PlaceToStand`), so `AVillagerStandsOnTheBuildingTheyArriveAt` and the standing pins hold their meaning.
- **Leaving:** the first leg goes standing place → doorstep. `StepOff` and `CostLeaving` answer "the doorstep" for a
  doored building.
- **One cost field still** (§2.6, CLAUDE.md). ⛔ No wall bits are added round footprints, and no second travel-cost
  system is built. Making the footprint's other edges walls would mean writing building edges into `ZoneMap`'s fence
  mask, a second meaning for one index. *Settled without asking (engineering): the goal moves, the field does not.*
- **Catchment walks door to door:** `CostBetween` = cost(home's doorstep → work's doorstep). The two inside legs are
  the same for every candidate, so they drop out of the comparison. `VillageEconomy`'s distance budgets
  (`ObstacleDetourTiles = 1`, `PlotLaneTiles`) are re-measured, not re-typed (§5).
- **A house's yard:** it is still entered only through its gate, and the house only through its door, which opens onto
  the lane (D388). The yard side of the house stops being a way in. `FenceEdges` keeps not walling the house's own
  edges, because the closed footprint is now the wall.

### 4.5 Keeping doorsteps clear

- **Nothing may stand on a doorstep.** `CanBuildAt` refuses a footprint over any standing building's (or marked
  site's) doorstep: *"That would block the granary's door."* This is asked with the per-tile checks, before the sweep,
  as the gate's lane tile already is (`SimWorld.cs:8731`). The same for a bridge's ends and a pasture's fence.
- **A building's own doorstep must be open when it is placed.** The ghost shows the door (§4.7). With §4.3 this is a
  refusal only when **no** side can open, which is today's *"could not leave it"* already.
- **Wall-off asks the door, not any side.** `HasAReachedNeighbour` for a doored shape asks whether its doorstep is
  reached; `TheVillagesPiece` counts a doored shape as touching a piece through its doorstep only. Wells, piles and
  bridges keep the any-side question.
- **The site chooser** (`ChooseSite`, `DetourOfAHouseAt`, `TheDailyWalks`) prices walks door to door, so a house is
  not sited where its door would face away from the village. `TilesClippedOff` already refuses a blocked door tile.

### 4.6 Founding buildings ⭐ (call 3)

Founding buildings stand at `Angle.Zero` today: every door would face north. **Proposed: each faces the founding site,
quantised to a quarter turn** (ties to N, E, S, W), derived once at the founding and then saved as its facing like any
other building. Fixture-only in practice (`FoundingBuildings` is the established warm start), but the fixture's goldens
move with it.

### 4.7 The view

- **Every doored building draws its door** (houses already draw the notch, `VillageMap.cs:6989–7012`), on the wall
  where §4.2 and §4.3 put it.
- **The ghost shows the door and its doorstep** before the click. R turns it, as today. If the front is blocked, the
  ghost shows the door where §4.3 will put it, with the sentence.
- **The ghost's starting facing** ⭐ (call 4, D620): the ghost starts with its door toward the village's nearest
  store, derived from where the ghost is; R still turns it, and a turned ghost keeps its turn for the next one.
- **Spurs land on the door.** `TrailNetwork.Doorsteps` aims at the door's doorstep instead of the worn side.
  `KeepTheBusierSides` is measured again before it is touched: rings round single buildings should mostly stop being
  worn, because walks now converge on one face. Nothing is removed until the probe's villages say it is dead.
- Built once per standing, wall or path generation, never per frame.

### 4.8 Saves and walks in flight

Nothing new is saved: the door is derived (§4.2), and a loaded village's doors are worked out on load. A villager loaded
mid-walk keeps their `leg_*` fields. The next leg is planned under the door rule; a leg already aimed into a footprint
by a side finishes as saved. *Settled without asking:* no migration, in line with the save/load spec's *no migrations
before v1*. ⚠️ A loaded village is no longer byte-identical going forward to the same village on the old build. That is
true of every sim change and is not a save break.

---

## 5. Measured before typed — the survival claim

Doors move every golden, the Phase 0 pins and catchment, so **the build starts with a measurement, not a guard.**
Shape copied from D611/D617 (`tools/harness/ZzLimits.cs` with `ZZ_LABEL`, **two worktrees: the parent and the change**):

| Arm | Seeds | Why |
|---|---|---|
| played (`ZzBase`'s opening, granary and warehouse at Year 3) | 200–299 (100) | The shipped game as a harness plays it. |
| established (`ShippedConfig.Established()`) | 1–36 | Founding buildings and a market. Call 3 lands here. |
| every (a lodge, a fishery and a farm raised at once) | 1–36 | The only arm with a fishing hut, the case §3 found facing its water. |
| **Joe's five saves**, stepped 20 years | his | Real villages with real facings. A harness never turns a building (D447). |

**Read per arm:** alive, starved, froze, dead valleys, per-seed t; **ticks walking as a share of all ticks**; mean
ticks of a trip to work; houses that fail to site per year (the unhoused count); placements refused for a doorstep;
buildings whose door is not on their front (§4.3).

**The bar:** survival within noise on every arm (|t| < 2 per seed), and **every death the change adds traced death by
death** before it becomes a design question (D611's habit). A harness loss is paired with Joe's own saves before it is
put to him (`harness-losses-are-not-design-problems`). If walking rises past noise, the answer is to re-measure the
distance budgets (§4.4), not to soften the rule.

---

## 6. Joe's calls — answered (D620, 2026-10-24): *"agree with your recommendations for the five calls"*

**All five as recommended:** (1) roofed buildings and their sites, not wells, piles or bridges; (2) a blocked front
moves the door to the first side that can open, said on the card; (3) founding buildings face the founding site, to a
quarter turn; (4) the ghost starts with its door toward the nearest store; (5) water buildings get no rule of their
own — their door is landward by (2)'s order. The options below are kept as they were asked.

1. **Which buildings have a door?** *Recommended:* every roofed building and its site. Wells, stockpiles and bridges
   have none and keep today's any-side entry. *Or:* literally every building, wells and piles included (2 of §3's 19
   blocked fronts were wells against a fence).
2. **A front that cannot open:** *Recommended:* the door moves to the first side that can, front → back → right → left,
   said on the card (§4.3). Every blocked front in his villages has one (19 of 19). *Or:* refuse at placement until
   turned, and for old saves fall back to today's any-side rule for that building only (two rules where there was one).
3. **Founding buildings:** *Recommended:* face the founding site, to a quarter turn (§4.6). *Or:* leave them north
   (the established fixture's doors all face one way).
4. **The ghost's starting facing:** 46 % of his buildings were never turned. *Recommended:* the ghost starts with its
   door toward the village's nearest store, derived from where the ghost is (houses already face their lane); R
   still turns it, and a turned ghost keeps its turn for the next one. *Or:* as today (north until turned), with the door
   now drawn so the player can see it.
5. **Fishing huts and trading posts:** with call 2's order, their door is landward (the back) because their front
   faces the water. *Recommended:* keep that. *Or:* give water buildings their own rule (door opposite the water
   always).

*Settled without asking, his to overrule:* the goal moves to the doorstep and the field does not change (§4.4); the
sim's door is quantised to the four tile directions while the view draws the real wall (§4.2); no save format change
(§4.8); a site takes its building's door (§4.1); livestock in a barn's pasture are untouched (beasts are not walkers
through doors; the gate stays theirs).

---

## 7. Guards (TDD, red first, every one red-checked and counted — D326)

1. `EveryWalkIntoADooredBuildingCrossesItsDoor` — a fixture village over years: every step from outside onto a doored
   footprint goes doorstep → door tile, and nothing else.
2. `NobodyLeavesADooredBuildingButByItsDoor` — the mirror.
3. `AWellAndAPileAreReachedFromAnySide` — the exemptions hold (call 1).
4. `NothingIsBuiltOnADoorstep` — `CanBuildAt` refuses, in the words of §4.5, for a building, a site, a bridge's end and
   a pasture's fence.
5. `ABlockedFrontOpensTheFirstSideThatCan` — posed from §3's cases: a fishing hut facing its water (the back), and a
   granary with a warehouse on its front (call 2's order, side by side).
6. `TheWallOffSweepAsksTheDoor` — a building reachable only by a side that is not its door is walled off and named.
   And D614's seed-18 guard re-posed through doors.
7. `CatchmentIsPricedDoorToDoor` — `CostBetween` agrees with the walk actually taken.
8. `ADoorIsDerivedAndNeverSaved` — save/load round trip identical. `SaveLoadTests.EveryFieldIsSavedOrNamed` names
   the index.
9. `TheDoorIndexIsNotRebuiltPerTick` — the count of rebuilds follows `StandingGeneration` and `WallGeneration`.
10. **Determinism stays green**, before and after, and the goldens move **once, in one commit, each proven by its parent**.
11. Probe: `doors:` — the ghost's door and doorstep drawn where the sim says, a turned ghost, a blocked front showing
    its moved door; the spur landing on the door. Red-checked.

---

## 8. What moves

- **Every sim golden** (`FarmGoldenTests`, `StockLimitTests`, `SkillTests`, the walk pin, the valley pin) and **the
  Phase 0 pins** (`VillagerPointTests.FirstGatherAtPace1` 39 / `FirstGatherAtPace3` 95,
  `Phase0SimTests.GoldenReplay_ASeededLifeIsReproducible`). Re-taken once, in the walking commit, each with its parent
  run beside it. `GoldenMapHash` and the per-seed map hashes **must not move**: doors are not terrain.
- Guards that pose an entry by a side: `ObstacleTests`, `FencesAsWallsTests` (house entered from its yard),
  `OrganicHousingTests`, `DesirePathTests`' cost pins. Each one is traced before it is re-posed, never re-typed to green.

---

## 9. Build order — one commit each, measured where it moves behaviour

⭐ **As built (Joe, D623): commits 2, 3 and 4's sim changes folded into one behaviour commit**, measured once against
`main`, goldens re-taken once — commit 2 alone moved 19 of 30 foundings, because the founding buildings faced north
until commit 4 turned them (§11.1). Commit 5, the view, is still its own and still to come.

1. **The door, derived** — `DoorOf`, the index, the sentence for a moved door as a sim answer. No behaviour change, no
   golden. ✅ **Built (D621).** *The sentence reaches the cards in commit 5:* every card kind writes its own rows, so
   putting it on them is view work with probe checks of its own, and it would be the only view change in this commit.
2. **Doorsteps kept clear** — `CanBuildAt`'s refusal and wall-off asking the door. New refusals only. Measure whether
   any golden village is refused a placement it used to get.
3. **Walking and catchment through the door** — **the goldens move here, once.** §5's measurement is taken on this commit
   against its parent before it is kept.
4. **Founding facing and the ghost's starting facing** (calls 3 and 4). Measured on its own: it moves the established
   arm again.
5. **The view** — the door drawn on every building, the ghost's door, the moved-door sentence on the cards, spurs to
   doors, `KeepTheBusierSides` re-measured.
   View only.

Each on `slice/doors`, unplayed until Joe plays it; merging is his word.

---

## 10. Definition of Done

- ✅ §6 answered and written in here, §4's starred lines rewritten to the answers (D620).
- §5's four arms measured on commit 3 against its parent and written in §5 with the numbers. Every added death traced.
- §7's guards red first, then green, each red-checked with the count written down (zeros kept and named).
- The four checks (CLAUDE.md): suite with determinism, the view build read, the probe (bar height 151, `done.`), the
  goldens grep (`git diff` shows exactly the goldens commit 3 re-took, and no map hash).
- `DESIGN.md §7` entries for each commit, §6 and §4 current; `path-shapes.md §4.G` told that doors now say which side;
  `buildings-as-obstacles.md §2` amended (*through the door, not any side*); this status line true.
- Joe plays it.

---

## 11. ✅ BUILT — the behaviour commit (D622–D625, 2026-10-25), unplayed

### 11.1 What it does

- **Doorsteps kept clear (§4.5).** `CanBuildAt` refuses a footprint on a door in use's doorstep, by name (*"That is in front of
  granary 2's door."*); the wall-off check refuses a proposal that covers, fences across or pockets a door in use, though
  another side of the building stays open (`WhatDoorThisWouldShut`); the site chooser keeps a house's own doorstep clear
  (`PlotShape.Doorstep`, shaped with the plot and remembered with it).
- **Walks and catchment through the door (§4.4).** The field to a doored building walls every edge of its footprint but the
  door's, for that field only (put up on the shared scratch for the fill, kept on the field for its descent,
  `TerrainCostField.DoorWalls`); leaving is by the doorstep (`StepOff`); the leg and the way out read the same rule
  (`LineOfSight.ADoorBars`, `ExitToward`); a step off a doorless building (a pile, a well) is not let into a doored one across
  its wall. One cost field; no wall written into the village. The catchment asks the same field, so it is door to door.
- **The facings nobody chose (§4.6–§4.7).** Founding buildings face the founding site; a building marked without a facing
  faces the nearest store (the ghost's starting facing, asked of the sim). ⚠️ *Settled without asking:* a building that is
  not square turns only a half, north or south, so every founding and marked building covers the tiles it always did.
- **The door in the economy (`VillageEconomy.DoorTiles` = 2).** On every leg the economy prices to or from a building;
  `gather_yield` 123 → 135 (the floor 120 → 132, the margin kept). Measured (`tools/harness/ZzDoorWalk.cs`, twenty villages):
  one way in 1.41–1.75 tiles, a leg between buildings 1.26–2.38, never shorter.

### 11.2 Joe's calls along the way (D622–D625)

- **Fold** commits 2–4 (D623): commit 2 alone changed 19 of 30 foundings, because the founding buildings faced north until
  commit 4 — measured with commit 2 on its own.
- **Accept the cost** as the price of doors, like fences (D406), then **price the door into the derivations** so they stay
  honest (D623).
- **A farmhouse and a barn have no door** (D624); **the farm derivation exactly as it was** (D624): a farm's armfuls go into
  the granary by its door, so a farm beside a granary is warned and capped — the nearest one can stand is 6 ticks, against a
  haul budget of 4, and the warning's cap reads 8 tiles of 13. *The warning now fires on every farm (D42's nag), recorded.*
- **D388 kept** (D625): houses face their lane whatever the walk; the house-siting and herd guards recorded at their measured
  price.
- **Accept the cost on his saves** (D625), §11.5.

### 11.3 The farm, measured before it was decided

Priced through one farmhouse door, a reaped tile's haul cost 4.63 tiles each way with the field painted round the farmhouse,
2.79 with 25 tiles in front of the door, 2.73 with the derivation's own 13 in front — and at three, rounded up, the ring
derivation kept a farmer one ring, five tiles, worth a gatherer's year only at more than an armful a tile (95 against the
armful's 80 and the buffer's 100). Hence D624. Counted tile by tile, with one store visit a haul, a farmer would keep 11
(crop 67 unchanged) — offered, declined.

### 11.4 Bugs the change surfaced, fixed in it

- **A door opened into a pocket beside the founding site.** The free-ground sweep is seeded beside the founding site (D383),
  so a sealed pocket there read as reached; a town hall stood on the site faced its door into one, and 1 tile of 493 was
  reached from the site. Doors read the village's own piece of the ground (D614's rule, `VillageGroundToday`).
- **An emergency sent a villager whose arms were full** — D372's bug, the rest of it: a marketer on the stall, the household
  out of firewood, sent to the stall they stood on, collecting forage there every tick — 274 in their arms. Whatever is carried
  is delivered first.
- **The re-founders' cart food was never counted in** (`FoodEverBroughtIn`), seen once doors cost a fixture its founders.
- **Hay was put out at the pasture's top row**, wherever that lay; now the tile nearest the barn.
- **A tend begun in summer's last ticks ran into autumn**; one that cannot finish is not begun.
- **The cart had a door** (D625): every placement in front of it refused, the played opening's woodcutter's hut never raised,
  four founders frozen.
- **Two cache keys** cost the suite minutes: the doors were keyed on the terrain's counter (a crop turning moved it; only
  water stops a walker), and on the standing counter a site-chooser trial house moves twice a candidate
  (`SettledStandingGeneration`).

### 11.5 Measured against `main` (four arms, two worktrees, `ZzLimits` + `ZzDoorSaves`)

| arm | `main` → doors | per seed |
|---|---|---|
| played, 100 valleys, 50 years | alive 1,308 → 1,334, starved 652 → 579 | t +0.57 |
| established, 36 | alive 543 → 568 | t +0.73 |
| every, 36 | alive 1,077 → 1,149 | t +1.60 |
| Joe's five saves, 20 years | 330 → 280 | — |

His villages, by name: ashbourne 87 → 74, elderwood 60 → 54, millbrook 48 → 25 (21 starved), oakhaven-ash-otter 51 → 51,
oakhaven-hazel 84 → 76. Traced on millbrook: 16 % less food over the twenty years — fish −26 %, wheat −27 %, forage +16 % —
the fishers walking 16 % more; no bug found (a fisher fishes at the hut). They were built before doors: houses facing their
lanes, half their buildings never turned. **Accepted by Joe as measured.**

### 11.6 Guards recorded at their measured price (Joe: "record … as measured costs")

| guard | before doors | with doors |
|---|---|---|
| a farm beside a granary is not warned → renamed `AFarmBesideAGranaryIsWarnedOfTheDoorItHaulsThrough` | not warned | warned at 6 ticks, cap 8 |
| houses along a bend turn with it | ≥ 6 facings of 12 | 5 |
| houses face the path in front of them | half off the quarters | 1 of 6 |
| houses gather round a well | 2 tiles nearer, 2 more within eight | 1.4 nearer, 1 more |
| a path wears to the well | a worn tile | wear 20 against 0, no worn tile |
| a herdsman feeds the herd through winter | 6 of 6 | 2 of 6 |
| the founding distance budget | every seed ≤ 2 × budget | seed 11 named, at 23 |
| the shipped village asked to build | marked ≥ unmarked | 105 against 106 |
| the fuel chain, deaths of old age | over half | 88 of 204 (43 %) |
| Phase 0, first gather at pace 3 | tick 95 | the founder starves at 139 (−1) |
| a farm's harvest falls off with distance (D58) | far arm 10 ticks: fewer tiles | 10 ticks meets the door's cap (76 = 76); re-posed at 12 (53 against 76) |

### 11.7 Not built

Commit 5, the view: the door drawn on every building and the ghost, the moved-door sentence on the cards
(`SayWhereTheDoorIs`), spurs landing on doors (`path-shapes.md §4.G`), `KeepTheBusierSides` re-measured. *(Built in §12,
D657 — all but the re-measurement, which §12.5 leaves for a played village.)*

---

## 12. Commit 5 — the view, as it will be built (D657, 2026-10-30, Joe: *"Push, start doors"*)

📝 **Specced here before any code; 🔨 BUILT on `slice/doors-view` (D657), unplayed.** §4.7 is the rule and Joe's calls (§6) are answered; this is
how each part lands, what it settles without asking, and the guards. **View only, plus one public sim question** — the
ghost has to ask the door of a building that does not stand yet.

### 12.1 The sim's one addition

`SimWorld.DoorOfAProposal(BuildingKind, Footprint)` — the door a building of this kind would have on this footprint,
standing here today; null for a kind with no door (§4.1). It is `DoorAt` behind the kind's rule, so it is remembered per
footprint like every door (§4.2) — a ghost that sits still asks a dictionary, not the walls. No behaviour changes, no
golden moves.

### 12.2 The door, drawn on every building that has one

- **Where:** a notch on the middle of the wall the door opens through — the front, or the side §4.3 moved it to. The sim
  quantises the door to a compass way; the view finds the wall by turning the building's own frame a quarter for each
  step the door is round from the front (`Faces − Front`), so the notch sits on **the turned wall where it really is**.
  ⭐ *Found while building:* a wide front **slides** its door along before it turns (§4.3), so the notch follows the door
  tile along the wall — but only when that tile is more than half a tile off the middle. Half a tile is an even wall's
  ordinary door (a house's, which has always drawn centred), so a house still draws exactly as it did.
- **What:** `DoorColour`, 0.28 of a tile across and 0.18 deep, the size the house's notch has always been — so a house
  draws exactly as it did when its door is on its front, and moves when the front is blocked. **No new colour**
  (`tools/legibility/check.py` stays at its count).
- **On:** homes, every doored workplace, store, library and the town hall — finished or still a site (a site takes the
  door of what it raises, §4.1), in the site's own edge colour so it reads as part of the drawing still being made.
- ⚠️ *Settled without asking:* a **shut** door (§4.3: nothing reaches any side) draws on the front at a quarter strength —
  a door nobody can use, said in words on the card.

### 12.3 The ghost

- **Its door and doorstep** (§4.7): the notch in the ghost's own fine / warned / refused colour, and a dot on the
  doorstep — a dot and not a square, because Joe never wants to see a tile drawn (D345).
- **Its starting facing** (Joe's call 4): until R or a middle-drag turns it, the ghost faces the nearest store
  (`FacingTheNearestStore`, the sim's own answer, asked as the cursor moves). Once turned, the turn is kept for every
  placement until a different building is taken from the bar. ⚠️ *Settled without asking:* a building that is not
  square starts north or south only — the sim's rule (§11.1), so the ghost covers the tiles it always did.
- **The sentence:** a click that marks a building whose door moved says so — *"Marked out. Its door is on its north
  side — water lies in front of it."* — in place of *"The village will raise it when it can spare the hands."* ⚠️ A
  placement that also carries a warning keeps the warning; the door is on the ghost and the card.

### 12.4 The card

A line under the condition (`Card.Door`), muted like it, written by every doored building's card from
`SayWhereTheDoorIs(DoorOf(…))` — homes, workplaces and their sites, stores, the library, the town hall. Hidden when the
door is on its front, which is nearly always.

### 12.5 Spurs to doors

`TrailNetwork.Build` learns each building's door (its tile and doorstep, by the building's number). For a doored
building, `Doorsteps` looks only at the doorstep: it is **reached** when a lane touches the door's wall there, and a spur
is drawn to the doorstep, along worn ground only, as before (§1.1, D581). A building with no door — a well, a pile, a farm,
the barn, the cart — keeps today's any-side rule. **`KeepTheBusierSides` is not touched**: §4.7 asks that it be
re-measured *before it is removed*, and nothing is removed here; whether its rings round single buildings have gone quiet
since walks began converging on doors (D622) is a played village's question, and is left for one.

### 12.6 Guards — the probe is the view's only instrument (D11, D160)

| Line | Asks | Red-check |
|---|---|---|
| `[widths] doors:` | Twelve years in, every doored building's notch has its centre within 0.8 of a tile of the sim's door tile and opens within 46° of the door's way; a posed hut against water draws its door landward. | The notch drawn always on the front |
| `[widths] ghost door:` | An unturned ghost faces the nearest store, and still does after the cursor moves; a turned one keeps its turn across a move; its drawn door is the door the sim gives the building once marked there. | The starting facing never applied; the turn not kept |
| `[widths] card door:` | A building whose front is blocked says so on its card, in the sim's sentence; one on its front says nothing. | The line never shown |
| `[widths] trails:` (one more case) | A building with its door on one side and a lane passing the other gets its spur to the door, not to the lane's side. | The door ignored by `Doorsteps` |
| `DoorTests` (suite) | `DoorOfAProposal` is the door the building has once marked there, and null for a well and a farmhouse. | Always null |

⚠️ **Known zero, written down now:** a draw site that forgets to call the door draw is invisible to `doors:` — the line
checks the notch each building *would* draw, from the same footprint the draw uses, not the frame's pixels.

**✅ Built (D657) — 6 reds over 6 mutants, every one read:** `DoorTests` 1 (the proposal always null — *"Value of type
'Nullable<Door>' does not have a value"*); `doors:` 1 (the notch always on the front — the posed hut's door *"opening
(0, -1) where the sim's opens South"*); `ghost door:` 2 (the starting facing never applied — *"faces 0, where the
nearest store is at 16384"*; the turn not kept — *"faces 16384, not the 50176 it was turned to"*); `card door:` 1 (the
line never shown — *"says 'nothing' where the sim says 'Its door is on its south side — water lies in front of it.'"*);
`trails:` 1 (the door ignored — *"spurred 0 times at its door"*). The four view mutants ran together, one per line, and
each line went red alone. ⚠️ **One mutant was thrown away:** the first `trails:` break threw a null reference inside
the trace instead of ignoring the door, and the probe stopped with Godot still running — a crash is not a red. Rewritten
to ignore the door quietly, it scored its red.
⚠️ **Thin coverage, said plainly:** twelve years into the probe's unattended valley only one standing building has a
door (no houses — nobody paints ground); the posed hut against the water is what carries `doors:`'s moved door, and the
card's line was read on a posed site, marked and taken back. Joe's own villages are the real test (§12.7).

### 12.7 Definition of Done

The guards red first and counted; the four checks; `path-shapes.md §4.G` and `buildings-as-obstacles.md §2` told; this
status line true; Joe plays it — a hut turned to face water, a building against a fence, a ghost picked up beside a
granary.

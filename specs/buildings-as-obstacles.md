# Spec: Buildings are obstacles — villagers go round, not through

**Decisions:** D383 (this document). Neighbours: D40/D41 (water is impassable, one cost field), D111 (unreachable is refused), D356 (string-pulled legs), D358 (wear reaches the routes through the one field), D363 (the food ladder's floor), D382 (footprints).
**Status:** ✅ **built (2026-09-16, D383)** — suite 1167 passing, 0 failing, 2 skipped of 1169; **unplayed by Joe as of this line.** Owner: Joe + Claude Code.

---

## 1. Why this exists

Joe, 2026-09-16, with a screenshot of a villager's trail running straight through a warehouse:
*"villagers should go around buildings, not through them."* `DESIGN.md §4` deferred exactly this
when the string-pulled walk landed (D356): *"buildings do not become obstacles here — that changes
routes and therefore the economy, and is its own slice before organic housing's reachable-door
rule."* Now that a granary is 2×2 (D382) a walk through one is a walk through a wall.

## 2. The rule

**A tile a building stands on cannot be walked through — only to.** Through the ONE shared cost
field (§2.6, CLAUDE.md), exactly as water: a standing tile is impassable to every flow field
except the field whose destination that building is, where its whole footprint is open. Homes,
stores, workplaces, libraries, the hall, the cart, construction and demolition sites — everything
`StandingShapes()` enumerates.

- **Leaving:** a villager standing on a building (at home, on the hut they cleared) steps off it
  to the cheapest outside neighbour of its footprint; `Cost` and `StepToward` from a standing
  tile answer that way. A leg's line of sight may cross the origin's and the target's footprints
  and nothing else.
- **Reaching — against today, not against perfection.** `CanBuildAt` (so the ghost says it before
  the click, and `Mark` refuses it) sweeps the free ground from the founding site twice, as it is
  and as it would be with the footprint closed, and refuses a building that takes the last
  reached free tile from beside the founding site (*"the village could not leave it"*), from
  beside any standing shape (*"That would wall off X"*), or that would itself have none. ⚠️ A
  shape nothing reaches *today* — seed 42's forager's hut on a spit — is not something a
  proposal walls off; the first cut asked for absolute reachability and refused every building
  in that valley. The sweep is asked **last**, after every cheaper refusal: a guard's scan of the
  bank asks `CanBuildAt` of thousands of tiles. The *before* sweep is cached per standing
  generation. `ChooseSite` and `WhereTheTreesAre` (the founding's huts) ask the same question.
- **Changing:** every place a footprint appears, moves or goes bumps `StandingGeneration`; the
  cost field forgets every flow field when it changes (a building is placed a few times a year;
  the fields rebuild on demand, D358's shape). The occupancy index (`StandsOn`, the footprint
  covering a tile) is rebuilt from `StandingShapes()` on that counter — a derived index, never
  hashed (D335) — and `SomethingStandsAt` reads it instead of walking every building.
- **Stranded:** a villager with no route home from where they stand goes idle with the note
  *"cannot get home from here"* rather than asking the way home again every tick (the first cut
  recursed `GoHome ↔ Travel` to a stack overflow).

## 3. The site-chooser: not on the road

Buildings are obstacles, so a house on the way to the granary costs every haul, every day —
`ChooseSite` put the founders' second house on the straight line from the hut to the cart at
tick 4, the haul went 64 → 81 round it, and a village that used to ride out its lean twentieth
year starved. Two cheap guesses failed first: the tiles of the one route the field returned
(through uniform woodland there are a dozen routes of that cost — the fixture's fourth house
went on the one tile beside the warehouse and the haul went 58 → 72), then the corridor of every
route of that cost (it penalised the whole middle of the village for seven converging hauls and
sent the founders' second house into a pocket 147 tiles from their work).

**What is built is measured, not guessed.** `SimWorld.TheDailyWalks()` prices the village's
walks as they are — each workplace to its nearest store, walked by everyone seated there, and
each home to *every* workplace (the seats change hands every season; a house that only spared
the walks of the moment closed the founders' way to the hut the year their seats went to the
new couple). `ChooseSite` scores every painted site by its own walks, sorts them, and stands
each in turn for a moment (`DetourOfAHouseAt`: a trial shape in the standing index, the fields
forgotten and rebuilt against it, every walk priced again), adding what the walks lengthen by
— tile for tile, a penalty and not a refusal. ⚠️ **Every site that could still win, not the
best few:** a detour is never negative, so the trials stop at the first site whose own score is
no better than the best total so far — but the fixture's whole road scored 8 and the first tile
off it 10, and a shortlist of six was six road tiles. A dozen fields per trial, a handful of
trials per house the village ever builds.

## 4. The founding layout leaves lanes

The D382 layout was a ring of buildings round the founding site — the warehouse and the
woodcutter's hut to its west, the builder's hut and granary to its north, the market to its
east — and the founders' homes went into the pockets: a twelve-tile walk to a hut eight tiles
away, and the fixture village (peak 19 at D381, 16 at D382) died out. Since D383 every founding
building has free ground on all four sides and the founding site keeps four free neighbours:

```
        x=-4 -3 -2 -1  0  1  2  3
y=-2     .   .  W  W  .  G  G  .      warehouse (−1,−1) · granary (2,−1)
y=-1     .   .  W  W  .  G  G  .
y= 0     .   .  .  .  F  .  .  .      F = the founding site
y= 1     .   C  C  .  .  .  .  .      woodcutter's hut (−2, 1), unmoved
y= 2     .   .  .  .  .  .  M  M      market (3, 3)
y= 3     .   .  .  B  B  .  M  M      builder's hut (0, 3)
```

⛔ Two offsets are economy inputs and were chosen to keep their sums: the woodcutter's hut feeds
`FirewoodRoundTripTicks` (unmoved), the warehouse feeds `CutRoundTripTicks` ((−1,−1) is the same
ten tiles from the budget's worst home as (−2,0) was). `TheFoundingLayoutLeavesLanes` guards it.

## 5. The economy carries a tile for going round

The budget every trip derivation rests on — `MaxHomeToWorkTiles`, the ring — was a straight
line through whatever stood in the way. Measured on the fixture with the lanes kept clear: the
haul to the granary went 80 → 88 and the commutes 64 → 72, a tile a leg, and a village derived
to feed itself on the straight line made a tenth fewer trips and starved at the margin Joe set
(D363). **`VillageEconomy.ObstacleDetourTiles = 1`, stated**, added to every leg the economy
prices (`WalkBudgetTiles` for the forage, planting and field commutes; the two real legs of the
cut trip; the firewood trip) — so no kind of work is quietly cheaper than another, and *not* in
`MaxHomeToWorkTiles` itself, which the ring's wooded fraction and the farm's radii read as a
radius. The consequences, in the order the derivation runs: `gather_yield` 87 → 90 and
`firewood_per_split` 50 → 53 in `data/sim.config.json` (each guarded as "at least the derived").

## 6. What it cost, measured

Twelve fixture seeds × fifty years, population alive at fifty / peak / ever starved:

| | alive | peak | starved |
|---|---|---|---|
| D381 (before footprints) | 151 | 204 | 38 |
| D382 (footprints, walk-through) | 153 | 204 | 40 |
| obstacles alone | 112 | — | — |
| + the one-route road penalty | 133 | — | — |
| + the trial detour, lanes, old economy | 146 | 183 | 33 |
| **D383 as built** (+ the detour tile) | **154** | **202** | **36** |

The fixture (seed 12345, 150 years): peak 16 → **17**, 11 → 10 starved, 27 → 30 of old age. The
first two years are leaner (the paths are unworn and the founders' homes stand off the road:
63 gathering trips in the first 2,000 ticks against 80) and three fixture-premise guards step
three years instead of two for it. Suite 2m41 → **3m07** (+2.5 % CPU; the critical-path seed
survey grew 18 s — every building event now forgets every flow field).

## 7. How it is tested

- `ObstacleTests`: a route round a stockpile is longer than the straight line and touches none
  of its tiles (⛔ red with the field ignoring obstacles); a villager at home reaches work; the
  last free tile beside a stockpile is refused at the fourth `Mark` (*"wall off"*, red with the
  sweep off); the occupancy index agrees with `StandingShapes()` after fifty fixture years with
  houses raised and pulled down; the founding layout leaves lanes (red with the market at its
  old (2,1)); seed 42 founds and every workplace is reachable (red with the wall-off question out
  of `WhereTheTreesAre`).
- The existing guards: `EveryVillageCanReachItsOwnBuildings`, the clock pins (re-pinned, not for
  the clock: 11/32 → 14/39, 80 → 63 trips), desire paths, the growth guards (peak 17 ≥ 15).
- A windowed shot of the founding's trails after twelve years, looked at once.

## 8. Definition of Done

Rule in the one field; leaving, reaching and stranding handled; the index and its counter; the
site-chooser's trial; the lanes; the detour tile and the two shipped numbers it moves; guards
red-checked; the measurement above; `DESIGN.md §4` Phase 4.5's deferral closed; handoff. ✅

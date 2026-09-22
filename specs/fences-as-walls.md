# fences-as-walls.md — a fence is a wall the cost field routes around

**Status:** ✍️ **SPEC WRITTEN, NOT STARTED** (2026-09-20, D400). Joe's yes, 2026-09-18, ordered
after the professions and again on 2026-09-20 from his D396 play notes: *"villagers are definitely
walking through other villager's yards — look at all of the packed trail within the yards."*
Owner: Joe + Claude Code. Phase 5.

**Measured before a line was written, six shipped seeds × fifty years:** of 502,090 steps taken in
the valley, **81,116 — one in six (16.2 %) — are inside somebody else's fenced yard**, and a
further 24.3 % inside the walker's own. The trails in his screenshot are not a drawing artefact;
they are traffic. At year 50 those six valleys hold 33 plots, **168 fenced tiles and 314 fence
edges** — about five tiles and nine and a half edges a plot.

---

## 1. Goal

A fence stops being paint that people walk over and becomes **a wall**: the one shared cost field
(§2.6) routes around it, a leg's straight line may not cross it, and every yard has **one gate**,
on the lane the house faces. Through-traffic stops; lanes carry it instead, which is what lanes
are for and what makes the desire paths (D358) draw the village's actual streets.

⛔ **This is not a new brush, a new building or a new decision.** The player paints; the sim sites
the house (D386) and fences the yard the day it is marked (D388). What changes is what the fence
*means* to a walk.

## 2. Which pillars, and what it must not touch

- **§1.1 legibility.** *Why is Agnes walking the long way round?* — because the Ashfords' fence is
  there, and you can see it. A fence you can see and walk through is the invisible-rule failure
  §1.1 forbids, from the other direction.
- **§2.6 desire paths.** Lanes stop being a convention the site-chooser observes and become the
  only way through a neighbourhood, so the trails wear where streets belong.
- **§1.3 people, not spreadsheets.** *"They cut through the Ashfords' garden"* stops being true,
  and the village reads as a village.
- ⛔ **One cost field** (CLAUDE.md). The wall is a term *in* `TravelCostField`, never a second
  pathing system. Every flow field, `LineOfSight`, `CanReach` and the site-chooser's trial walks
  read it because they all read that field.
- ⛔ **Determinism.** The mask is derived from `Household.FencedTiles` and the home's facing —
  ⛔ **never hashed** (D335: a derived index restates the state, it is not a second fact).
- ⛔ **Nothing derivable incrementally may be rebuilt per tick** (Joe's rule). The mask is
  maintained where plots are claimed and released, beside the layer that already is.

## 3. The rules

### 3.1 A fence runs on the edges of the plot, and a wall is an edge

> **A fence edge is any edge between a fenced tile and a tile outside that household's fence.
> Crossing a fence edge is impossible, in either direction, for everybody — including the
> household that owns it, except at its gate.**

The fence is already a fact: `Household.FencedTiles` (D388), fixed the day the house was marked,
refunded with the house, inherited by a couple who take over a dead family's home (D381). The wall
is that list's **outline**, computed once per change rather than per tick.

⚠️ **The house's own two tiles are inside the fence and are already impassable** (a building is an
obstacle, D383) — the field whose destination is a building opens its footprint, which is how
anybody gets indoors. So the wall this spec adds is the yard's, not the house's.

### 3.2 One gate, on the lane

> **Each plot has exactly one gate: the edge between the yard tile nearest the door and the lane
> row the house faces. It is open to everybody.**

⭐ **One, not four**, because the point is that a yard is not a thoroughfare; and **on the lane**
because that is the side the household already walks out to (`Household.HomeFacing`, D388's lane
rule). A gate that faced the back would put a household's daily walk through its own kitchen
garden and out the far side — a shortcut for the neighbours by another name.

⚠️ **It is open to everybody rather than to the owner**, because a cost field prices ground, not
people. A per-household field is a second pathing system (§2 forbids it) and would cost one field
per family. *A gate anybody may walk through, into a yard with nothing on the far side, is not a
shortcut* — which is the measurement this slice must make (§6).

### 3.3 The wall-off refusal grows a second sentence

`SimWorld.CanBuildAt` already refuses a building that would cut anything off (D383) by sweeping
the free ground before and after. **A newly fenced plot is now capable of the same thing**, so
the same sweep runs when a *house* is marked, and the refusal reads:

> *"That would fence in the Ashfords — nobody could reach their door."*

⛔ **The site-chooser must not propose one either.** `Household.ChooseSite` already prices the
village's daily walks round each candidate (D383); a candidate whose fence would wall anything off
is refused there, before the player ever sees it, exactly as an obstacle is.

### 3.4 What the view already does, and what it must do

The fence is drawn (D386/D388). Two things follow from it becoming a wall:

- the **gate** is drawn as a gap in the outline — the one place the line stops — so the rule is
  on the screen before it is felt;
- a leg's straight line may not cross a fence edge, so `LineOfSight.Clear` reads the mask. Without
  it a villager routes round the fence and then *draws* through it (D356's string-pulled leg).

## 4. The data model

| | where | maintained |
|---|---|---|
| The fence itself | `Household.FencedTiles` (hashed) | `FencedTilesFor` at the marking (D388) |
| The plot layer, per tile | `ZoneMap._plot` / `_plotByOwner` | `ClaimPlot` / `ReleasePlot` — **the two write sites** |
| ⭐ **The wall layer, per tile** | `ZoneMap._walls`, a `byte` a tile: N/E/S/W bits — **every wall on that edge, whatever put it there** (§10) | the plot's two write sites today, plus a generation counter |
| The gate | one cleared bit in the mask | computed with the mask, from `HomeFacing` |

- **`ZoneMap.WallsOn(GridPos) → byte`** and a `WallGeneration` counter, the shape
  `_groundByOwner` / `Edits` already uses.
- **`IObstacles` gains `byte WallsOn(GridPos tile)`** — the existing seam between `SimWorld` and
  the field, which already carries `Generation`, `StandsOn` and `FootprintCovering`.
- **`TerrainCostField.Scratch` gains `byte[] Walls`**, filled in `Block()` beside `Passable`; the
  Dial `Relax` and the flat `Sweep` refuse a step whose direction bit is set. One array read a
  relax — the same cost as the passability check beside it.
- **Symmetry is a construction rule, not an assertion**: writing a wall on tile A's east edge
  writes tile B's west edge in the same statement, so the two can never disagree.
- ⭐ **The layer is a union, not a plot outline** (§10). `ZoneMap.Wall(edge, on)` is what the plot
  writes through; the field asks *"is there a wall here?"* and never *"whose?"*

## 5. Edge cases and failure modes

| | what happens |
|---|---|
| A household's own yard | Walled like anybody else's, gate open. Nothing in the sim needs to enter a yard today; the kitchen garden (D357) is the first thing that will, and it arrives with a worker who comes through the gate. |
| A plot on the valley's edge | Fine: a wall refuses a step, it does not create ground. |
| Two plots back to back | Two walls on one edge; the mask is per tile per side, so both are set and neither can be crossed. |
| A plot whose lane is itself fenced by a neighbour | The refusal in §3.3 is what prevents it being built. ⚠️ **The measurement in §6 must count how often the site-chooser is refused for this reason** — a chooser that cannot place a house is a village that stops growing. |
| A fence built round a standing building | Cannot happen: `FencedTilesFor` already clips what a building covers. |
| A dead household | `ReleasePlot` drops the mask with the layer, in the same call. |
| Demolition | The fence comes down with the house (D388), so the mask goes with the plot. |

⛔⛔ **The failure that ends a village, named before it happens: a neighbourhood that packs plots
until somebody's door is unreachable.** D383's sweep is the guard, and §6 measures how often it
fires. If it fires often, the answer is the **site-chooser's score** (lanes weigh more), not a
softer wall.

## 6. Measure before merging — what must be known

Twelve fixture seeds and the shipped seeds, fifty years, before and after:

1. **The traffic this exists to stop:** steps inside somebody else's yard, now **16.2 %** of all
   steps. It must go to **zero**; anything else means a hole in the mask.
2. **What it costs a walk:** ticks per gathering trip, the `VillagerPointTests` pins, and the
   village's total walking time (D358 measured 6.8 % either way and that was a feature).
3. **What it costs a village:** alive / peak / starved, against D399's numbers
   (shipped 122 / 159 / 30; fixture 77 / 139 / 59).
4. **How often a house cannot be sited** for the new refusal, and whether any village stops
   building houses.
5. **The suite's clock** (4m02 at D399): every flow field now reads one more array.

⭐ **The trails are the picture, and the picture is the spec (D352):** the `trails:` probe line and
a look at a year-30 neighbourhood say whether the lanes now carry what the yards used to.

## 7. How it is tested

- `AFenceIsAWall` — two plots side by side, a walk from one to the other: the route goes round the
  outline and never crosses an edge. Red with the mask ignored.
- `AGateIsTheOneWayIn` — the yard is reachable, and only through the gate's edge.
- `ALegNeverCrossesAFence` — `LineOfSight.Clear` refuses a straight line across an edge, so the
  drawing and the routing agree (D356's leg).
- `AHouseThatWouldFenceSomebodyInIsRefusedByName` — the D383 sweep, extended, in words.
- `TheSiteChooserNeverProposesAPlotThatWallsAnythingOff`.
- `TheWallMaskIsNotHashed` — two villages identical but for a rebuilt mask hash alike (D335).
- `TheWallsAreMaintainedNotRebuilt` — the generation moves only on a claim or a release, never on
  a tick (Joe's rule, CLAUDE.md).
- The conservation and determinism guards stay green; **goldens move once**, for one stated reason.

## 8. Definition of Done

1. This spec current, with §6's numbers filled in from a run.
2. Guards above green; each new one red-checked and the reds counted.
3. Determinism green; goldens moved once with the reason beside them.
4. The gate drawn, and the probe's picture looked at.
5. `DESIGN.md §6` + a §7 entry; `organic-housing.md §3.5` pointed here; `handoff.md` traps.
6. Joe plays it and says the yards read as yards.

## 9. Open, and Joe's to call

- **The gate's width and where exactly it sits** — the tile nearest the door is the proposal; a
  yard whose door tile is the house itself may want the gate beside it.
- **Whether a fence is ever crossable** — a gate is the answer here; a *stile* (a fence that costs
  extra rather than refusing) is the alternative and is deliberately not proposed: *a wall you can
  pay to cross is a wall the player cannot read off the screen.*
- **What happens when the kitchen garden lands** (D357): a gardener walks through the gate, and
  that is the first time anybody needs to.

---

## 10. ⭐ What the player-built fence will need, and what this slice does for it (Joe, 2026-09-20)

> Joe: *"eventually we are going to want to add 'fence' as a thing the user can build themselves,
> along with gate placement — for example, a fence around a crop field to prevent people from
> walking through (and for aesthetics). It sounds like this work is valuable as foundation for the
> user-placed fences as well?"* **It is, and here is the honest split**, written down now because
> one line of it changes a decision above while that decision is still free.

**What a player-built fence inherits from this slice, whole:**

- the **edge mask in the one cost field** and the relax that reads it (§4) — the expensive,
  risky half, and it does not care who put a wall there;
- **`LineOfSight`** honouring edges, so a drawn leg and a routed leg agree (D356);
- the **wall-off refusal** (§3.3) — which matters *more* for a player fence, because a brush can
  draw a line across the whole valley where a plot can only ever fence its own five tiles;
- the **gate**, as a cleared bit in the same mask.

**What it does NOT inherit, and must not:**

| | the yard's fence (this slice) | the player's fence (later) |
|---|---|---|
| Where the wall comes from | **derived** from `Household.FencedTiles` + the facing | **its own fact** — the edges the player marked |
| Hashed? | ⛔ never (D335 — it restates the plot) | ✅ always — a player decision, like a stock limit or the paint |
| Built? | with the house, on its recipe (D388) | its own site, its own logs, its own demolition |
| Drawn by | the plot's outline | **an edge tool the view does not have** — today's brush paints tiles and quarter-tiles (D327/D352); marking an *edge* is a new interaction, and per-edge gate placement is another |

**⭐ THE ONE DECISION THIS CHANGES, TAKEN NOW WHILE IT IS FREE:** `ZoneMap`'s wall layer is
**every wall on that edge, whatever put it there** — not "the plot outlines' mask". Same array,
same cost, one method (`Wall(edge, on)`) that the plot writes through today and a player fence
writes through later. The field asks *is there a wall here?* and never *whose?* ⛔ Retrofitting a
second source into a layer shaped for one would mean touching the cost field and every golden a
second time, which is the whole reason this paragraph exists.

⚠️ **The hashing rule that falls out of it, stated before it can be got wrong:** hash the
*sources* that are facts — the player's fences — and never the layer itself, which restates them
(D335). A village whose walls are all derived hashes exactly as it does today.

**⭐ The crop field is the good stress test, and it is half-built already:** work ground is painted
per workplace and `ZoneMap` already keeps a per-owner layer for it (`_groundByOwner`), so a field's
outline can contribute walls exactly as a plot's does — and the farmer's gate falls out of the same
question this slice already has to answer, *can the owner still reach their own ground?* ⛔ Not in
this slice, and not implied by it: a field fence is the player's to place, and placing it is the
edge tool above.

---

# Spec: Forests, gathering, and the end of the distance fences

**Decisions:** this document, and Joe's three calls of 2026-08-07 recorded in §1.
Neighbours: D18, D19, D24, D40, D42, D43, D58, D84, D86, D87, D88, D107, D111.
Supersedes in part: `building-placement.md §12.5(3)`, `§12.6`; `professions.md §6.1`, `§6.2`;
`DESIGN.md §5`'s open "7-tile bound".
**Status:** ✅ **BUILT AND MERGED TO `main`** as step C (2026-08-15 on `phase/2-wood-fuel-and-tools`, merged with Phase 2 on 2026-08-22; *that branch no longer exists — corrected 2026-08-28*)
(D112–D130, then D142–D158). Written before the code (METHODOLOGY §2), and it stayed marked
*"specced, not built"* for a week after it shipped — see D159.
**§10 (planting waits for spring):** 🔨 **BUILT on `slice/forester-winter` (D728, 2026-11-12), unplayed, unmerged.**

> ⭐ **What the build added that this document did not predict.** The distance fences came down
> (D120: the catchment was never binding — the furthest commute anybody holds is three tiles),
> `ChooseSite` moved onto the shared cost field (D121), harvest paint became a *standing*
> instruction with regrowth rather than a one-off order (D126, D127), and buildings are numbered
> rather than named by bearing (D124). **The last of those met the first in D157**: a marked
> building's footprint is painted for clearing (D100), regrowing coppice is always nearer, and
> nearest-first clearing therefore never reached the footprint — so a gatherer's hut sited in
> real woodland, which is the whole point of this spec, could never be built. Clearing defers to
> the build queue now. **The re-measured opening is 4 → 22 people over forty years.**

---

## 1. Why this exists

Joe, 2026-08-07, after playing a manually-staffed village to year 30:

> *"I want to go with no forest, no food. There should be generated forests on the map
> naturally, just like stone, iron, water… lots of them, actually. So the gatherer's hut can be
> placed/built as soon as the materials are ready and villagers can begin collecting food early.
> The gatherer's hut should have a maximum gatherable area in a ring and workers cannot gather
> outside that ring — the number of trees/forest in the circle has a relation to the volume of
> food gathered. Less trees = less food available to gather. But also foresters can plant
> trees/forests in a painted area — this will allow the user to sculpt their forests to their
> own desires. The user can paint trees to get laborers to cut down forests until foresters are
> available to provide automation/consistency of log delivery."*

And, separately: **get rid of the ring and the distance restrictions.**

**Three calls, recorded so they are not re-litigated:**

1. **Forests first; D109's manual staffing is parked** on `wip/d109-manual-staffing`.
2. **Planting ships ungated.** The managed-forestry tech node gets new content later.
3. **Catchment is deleted entirely** — no hard bound on home-to-work.

### 1.1 What it is actually for

- **Food stops being a fact of the map and becomes a decision.** Six berry patches the generator
  drops on a ring are the last placeholder left in the economy.
- **Timber and food compete for the same trees.** The harvest brush stops being free money. That
  is §2.3's *"every escalating problem should be back-traceable to something the player did"*
  arriving out of a system built for another reason — the sign it is the right shape.
- **It dissolves D109's staffing problem.** One-number-per-building could not put gatherers at the
  right berry patch because nobody chose the patches. A hut the player sited has no such problem.

---

## 2. Which pillars this serves

- **§2.3 systemic escalating pressure.** Forest exhaustion stops being about timber alone and
  starts being about *food*, which is the pressure that actually regulates the village.
- **§2.5 environment with teeth.** Terrain dictates viability: where the woods are decides where
  the village can live.
- **§1.1 legibility.** Every number here is visible on the map — the ring, the trees in it, the
  bald patch you made.
- **§0.1 the niche.** *Challenge in the planning, never in the punishment.* Over-clearing must be
  a visible, expensive, recoverable mistake — never an invisible one.

---

## 3. ⭐ The shape

| Part | Today | After |
|---|---|---|
| Where food comes from | 6 generator-placed forage sites on a ring | A **gatherer's hut** the player places |
| How much a trip yields | one global `gather_yield: 46` | base yield × **wooded fraction of the hut's ring** |
| Where forest is | 2 clumps of radius 3, on a ring | **Clumps across the whole valley**, a stated coverage |
| Who may work where | hard **catchment** cutoff (10 tiles) | no cutoff; **cost-first sort** is the only steering |
| Where a home may go | hard refusal past 7 tiles from work | **a budget, warned about** (D43's pattern) |
| Putting trees back | natural regrowth, planting gated behind tech | **foresters plant, ungated** |

### 3.1 ⭐ The density rule, argued rather than picked

**Linear in the wooded fraction of the ring, with no floor.**

```
yieldPerGather = baseYield × woodedTilesInRing ÷ tilesInRing      (integer, D2)
```

**Why linear, and why no floor.** *"Half the trees, half the food"* is a sentence a player can
hold in their head while deciding whether to fell the wood beside their hut, which is the whole
point of the mechanic (§1.6 — traceable over clever). A floor would be kinder and would make
*"no forest, no food"* untrue, which is the rule Joe asked for by name. A curve would be more
tunable and would stop the player being able to predict the consequence of one brush stroke.

**Zero trees yields zero food, and that is the design.** The safety is that it is *visible*
(§7.1), not that it is softened.

### 3.2 ⭐ What the economy is derived against now

`RequiredGatherYield` currently solves *yield = need ÷ (trips × vigour)* against the **worst walk**,
and `RoundTripTicks` gets that walk from `MaxHomeToWorkTiles` = forage-ring + 2 × jitter = **7**.
The forage ring is going away, so the anchor must be replaced — **it cannot simply be deleted**,
because widening the worst walk to the map diagonal makes `TripsPerYear` round to zero and the
economy has *no solution at all* (the finding already recorded in `DESIGN.md §5`).

**The replacement, and it is a better one:**

```
MaxHomeToWorkTiles(config)  =  gatherer_hut_ring_tiles
WalkBudgetTiles(config)     =  MaxHomeToWorkTiles(config) + ObstacleDetourTiles   (= ring + 1, D383)
```

⚠️ **The trip derivations price `WalkBudgetTiles`, not the ring** (D383): buildings are obstacles
and the walk goes round them — measured a tile a leg on the fixture — so every leg the economy
prices carries one tile for it (`buildings-as-obstacles.md §5`). The ring itself is unchanged:
it is a radius, and the wooded fraction and the farm's radii read it as one.

Three things recommend it:

- **It is a number the player can see on the map.** The ring the hut draws is also the distance
  the economy assumes people live within. Today's 7 is an artefact of where a generator dropped
  a berry patch, and no player could ever learn it.
- **The stated target barely changes:** *one gatherer at a **fully wooded** hut, at minimum
  vigour, feeds themselves and `RequiredDependants` children.* Only the two words in bold are new.
  ⚠️ **`RequiredDependants` is ONE since D363 (2026-09-12), by Joe's call** — *"foraging gives too
  much food now. drop it 40%"* — and `gather_yield` is 87 (was 145). Foraging feeds a couple, not a
  family; fishing, hunting and farming feed the rest, and a village that will not diversify shrinks
  (twelve shipped seeds × fifty years: 294 people → 171).
- **It stops being a fence and becomes a budget.** `Household.ChooseSite` no longer *refuses*
  ground beyond it — it still **scores** `toWork + toStore` and picks the nearest, which is what
  actually shapes a village. Building beyond the budget is allowed, warned about, and genuinely
  costs food, because the villager really does walk further and really does make fewer trips.

**That is D58's settled mechanism arriving at last:** distance stops being a restriction and
becomes a consequence.

### 3.3 Gatherers work the ring near the hut

**Until D384: a gatherer's job position was the hut, and density scaled the yield** — they did not
walk to individual trees. It satisfied Joe's rule exactly (nothing outside the ring contributes
anything) while reusing the forager behaviour whole, and this section said *"walking to individual
tiles stays on the board as a later change if the hut reads flat; it is a behaviour change, not a
model change."*

✅ **Since D384 (`trades-visibly-work.md §3`) the hut read flat and the change was made:** a trip
gathers at a wooded tile within `gather_walk_tiles` (3) of the hut, picked by a hash of the
villager and their trip count, never by the `Rng`. **The model is unchanged** — the yield is still
the ring's density (`GatherYieldAt`), the drawn ring is still the catchment — and the walk into
the ring is priced in `RoundTripTicks`, which re-derived `gather_yield` 90 → 112. **And since D385
the load goes to a store, never home** (`storage-and-distribution.md §14.10`, Joe's call (b)): a
forager's family shops from the granary like every other, and food in a larder is not the
village's.

---

## 4. Data model

| Thing | Where | Notes |
|---|---|---|
| `BuildingKind.GathererHut = 7` | `Construction.cs` | **Append, never renumber** — hashed by position |
| `JobKind.Forager` | unchanged | **Reused, not added to.** A second kind means a second quota arm, plural, behaviour branch and a rule to stop the village staffing both — D96's argument for renaming `Logger` |
| `gatherer_hut_ring_tiles` | config | **Content**, like `work_ground_tiles_per_worker` — a fact about the building |
| `forest_coverage_percent` | config | **The target**; clump count is derived from it and map area (D16) |
| Wooded-tile count per hut | cached on the workplace | ⚠️ see §7.2 |
| Growth rate | derived | from a stated target, never picked (`building-placement.md §12.8`) |

**Deleted, over the course of the work:** `forage_site_count`, `forage_site_ring_tiles`,
`forage_site_capacity`, `forager_catchment_tiles`, `tree_stand_count`, `tree_stand_ring_tiles`,
`Workplace.CatchmentRadius`, `FoodSource`, `MapGenerator.CanonicalForageSites`,
`VillageEconomy.NearestForageDistance`. **Deleted rather than zeroed**, on D98's rule that a
number which is always zero is a lie waiting to be found.

---

## 5. Slices

Each leaves the suite green and is measured against the cold start's five ticks.

1. **Forests are generated across the valley.** `PaintForest` is already the right primitive and
   the *"clusters, never scatter"* argument at `MapGenerator.cs:49` stands; what changes is what
   drives it. Tree stands stay for now, or foresters have nowhere to work.
2. **The gatherer's hut**, with its ring and the density rule. ✅ **Done.** `BuildingKind
   .GathererHut = 7`, a real price in logs and work, seats derived as the ring priced in workers
   (D86's rule reused — a ring of 8 gives 7 seats), and yield linear in the wooded fraction with
   no floor. **Measured: 83 wooded tiles of 145 is a trip worth 26; fell 41 of them and it is
   worth 13** — half the trees, half the food, to the integer. A bald ring yields zero.
   **All three goldens unmoved**, because forage sites still exist and nothing places a hut
   unless the player does — which is exactly what `professions.md §9.1` asks of a new profession.
3. **The fences come down and the economy is re-derived.** Catchment deleted; `MaxHomeToWorkTiles`
   re-based on the ring and demoted to a budget; **the commute becomes readable** (§7.1).
4. **The forester's hut, and planting ungated.** ✅ **Done**, except that **tree stands retire in
   slice 5, not here** — the hut lands beside them so that no golden moves, which is the pattern
   slices 1 and 2 established and which held again. D86's work ground reached the player at last:
   painted per workplace, priced in workers, with the overstretched warning it has computed since
   C3c. Planting costs 3× felling (content), and the consequence is derived —
   `YearsToRewoodOnesGround`, ~2 years for the ground one pair of hands keeps. **Planting carries
   nothing home**, which is why it is a mode rather than a second job. Measured: 53 owned tiles
   go 53 → 33 wooded in three years of felling, and 68 bare tiles go 0 → 22 in five of planting.
   - **⭐ THE TOGGLE IS FELLING, NOT PLANTING (Joe, D146).** Painting ground for a hut *is* the
     instruction to keep it wooded, so planting is unconditional *(save in winter, when the ground is frozen —
     §10, D728)* and what the player decides is
     whether timber comes out. `WorkMode.PlantOnly` is a wood that is resting; `FellAndPlant`
     is the default. **A met Logs limit is the same instruction at another distance** — felling
     off for a while — and `SimWorld.MayFell` is the single place that answers both, so the
     tile-picker, the action's duration and its outcome cannot disagree. A capped hut
     **replants** until its painted ground is wooded again and only then becomes spare hands
     (Joe's ordering); the demand that keeps it staffed is `ForesterSeatsWithGroundToPlant`,
     because a staffing number is a ceiling and not a summons. ⚠️ **It counts grass only**
     (`SimWorld.IsGroundToPlant`, the one predicate the errand and `Plant` read too — D452): it
     used to count any tile that was not woodland, so a hut whose ground was saplings or a seam
     held hands for planting nobody could do (found D434; `RockAndSaplingsAreNotGroundToPlant`). **Fell-only is deleted** — that
     is what the harvest brush is for (D87), and a forester's ground is a managed wood.
   - **⚠️ PLANTING IS AN ERRAND, NOT A MODE, AND THREE PLACES HAVE TO AGREE ABOUT THAT
     (D137, corrected by D142).** A forester **tends**: trees first while any stand on their
     ground, bare tiles when none do. So the mode alone never decides what is happening —
     **the mode says whether replanting is allowed, the tile says which of the two errands
     this is**, and both halves are needed every time. D137 changed the tile-picker and the
     outcome and missed **the action's duration**, which went on asking the mode: from that
     commit until D142, *every fell in the village was charged `PlantTicks`* — 12 ticks
     against a `cut_ticks` of 4, invisibly, because the villager still walked to a tree and
     still came home with logs. It cost `TheVillageSurvivesWithTheMarketSwitchedOff`. One
     predicate (`BehaviorSystem.IsPlantingErrand`) answers the question now, and both guards
     were checked against the broken code before being believed.
5. **Forage sites retire.** *This is the slice where "no forest, no food" actually arrives*, so it
   ships only after §7.1's sentences and slice 4's planting are both in.
6. **D109 lands on top**, cherry-picked back from `wip/d109-manual-staffing`.

---

## 6. What this overturns, and what survives

**Overturned (Joe's call 2):** `building-placement.md §12.5(3)` — *"planting is gated behind the
managed-forestry unlock"* — and `professions.md §6.2`. Planting ships from the start.

**§12.6's interlock changes shape rather than breaking.** It read: *you may only fell what you
paint → over-clearing is a visible mistake → **natural regrowth** makes it survivable → planting,
once earned, makes it recoverable on purpose.* With planting ungated, **planting is the recovery
and regrowth is optional**. The loop still closes; it closes faster and asks more of the player.
D88's argument for promoting regrowth above planting was entirely about planting being gated, and
it evaporates with the gate.

**⚠️ The cost, named:** §2.7's tech tree names *"log a forest for two generations → managed
forestry"* as its **headline example of unlock-by-doing**, and planting is what that node
unlocked. §2.7 is otherwise unbuilt, so the tree currently has **no designed content left**. The
node survives with different content — faster growth, larger rings, or planting off your own
ground — and **that is a debt this document is creating deliberately**, not an oversight.

**Survives untouched:** §12.1's pattern (the player paints intent; the village acts when it has a
reason to), §12.5(1) (unpainted forest is never cut), and D87's laborer harvest — which is exactly
Joe's *"the user can paint trees to get laborers to cut down forests until foresters are
available."* That already works and needs nothing.

---

## 7. Failure modes to design against

### 7.1 ⭐ The silent commute, and the silent bald ring

**The two ways this design can fail unfairly, and they are the same failure.** Deleting catchment
means a villager can take a job whose walk eats their working year; clearing a hut's ring means
gatherers bring back almost nothing. **In both cases the village thins out slowly with nothing on
screen saying why**, which is §1.1 failing and the one uncozy state §0.1 rules out.

**So the sentences ship in the same slice as the mechanic, not after it:**

- *"The gatherers' ring holds 41 wooded tiles of 60 — they bring back about two-thirds of a full
  trip."* On the hut's panel, always.
- *"Elias walks 19 tiles to the north hut; most of his working year goes on the road."* On the
  villager, when the commute is a large share of the year.
- A village-level alert when a hut's ring falls below the density its own workers need.

**This is not polish. It is the thing that makes the mechanic fair**, exactly as D93 ruled about a
stalled construction site.

### 7.2 ⚠️ A per-tick ring scan

Counting wooded tiles in a ring is O(R²), and it would be asked per gatherer per trip. **D87 has
already taught this suite what that costs** — the run went from four minutes to over ten until a
village that had painted nothing paid one integer compare.

**Cache the count on the workplace and invalidate through the one door.** `SimWorld.SetTerrain` is
the single place terrain changes (D85) and already carries `TravelCostField.Forget()`; the density
cache hangs off the same hook. **Do not hook `Harvest`** — that is identical today and wrong the
day anything else clears ground, which is the reason the door exists.

### 7.3 An economy with no solution

`RequiredGatherYield` searches for a yield that meets the target. If the anchor is too large the
search fails and the config is broken — loudly, at start-up, as `RequiredFirewoodPerSplit` already
does. **Better a throw at start-up than a village that dies in year forty.**

### 7.4 Goldens moving for two reasons at once

All three goldens move more than once across this work. **Each slice re-takes deliberately, with
its own one-sentence reason and the old values kept beside it.** A golden move that cannot be
explained in one sentence means the slice is wrong, not the golden.

### 7.5 A profession whose demand is discontinuous

`professions.md §8`'s standing trap. A gatherer must have something to do on an ordinary Tuesday —
which they do, since a hut's ring regrows and is worked continuously.

---

## 8. How this is tested

1. **The cold-start five ticks**, before and after every slice, on the shipped config against a
   winter at t360. Currently **t121 / t130 / t173 / t241 / t251**.
   `ColdStartTests.JoesOpeningSurvivesOnTheShippedConfig` is the gate and runs *first*.
2. **The twelve-seed × 200-year arm.** The guard that catches a change helping eleven valleys and
   killing one — it has now done so twice (D103, D110).
3. **Density is asserted, not assumed:** a hut in solid forest yields the base; a hut with half its
   ring cleared yields about half; a hut with a bald ring yields nothing and **says so**.
4. **Anti-vacuity throughout (D7):** clearing a ring must change a number, or the guard is
   measuring a constant.
5. **Eyes on it after slices 1 and 2** — the forest and the ring are the two things only eyes can
   check (D11). *This said to use `BCLONE_SCREENSHOT`; that hook is deleted (D160), so it means
   playing the game and looking.*
6. **A determinism guard on the density cache**: two runs of one seed that clear the same tiles
   must hash identically, or the cache has become a second source of truth.

---

## 9. Definition of Done

1. This document current, and each slice's decision in `DESIGN.md §7`.
2. Full suite green after every slice; all three goldens re-taken deliberately with old values
   kept beside them.
3. The cold start's five ticks measured before and after each slice, and reported.
4. `DESIGN.md §5`'s "7-tile bound" open decision **struck through in place** as resolved.
5. `professions.md §6.1`, `§6.2`, `§7` and `building-placement.md §12.5`, `§12.6` reconciled.
6. Joe's QA pass: place a gatherer's hut, fell half its ring, and **read the consequence off the
   screen** before the granary empties.

---

## 10. Planting waits for spring (D728)

**Status:** 🔨 **BUILT on `slice/forester-winter` (D728, 2026-11-12), unplayed, unmerged.** Measured before and after.

Joe, 2026-11-11, playing the art: *"should a forester plant trees in the winter? seems weird. am i wrong?"* He is
not. Real planting is done in the dormant season, but not in frozen ground, and this game's winters are white.
`SimWorld.Plant` asked only whether the ground was grass. `SeasonRules` gated sowing (spring) and gathering (not
winter), but nothing gated planting. He took the recommendation: *no planting in winter* (`"Push and start it"`).

### 10.1 The rule

1. **`SeasonRules.IsPlanting(season)`, true in every season but winter**, beside `IsSowing` and `IsReaping`, so
   the calendar's rules stay with the calendar.
2. **Felling goes on all winter.** Unchanged: timber is fellable year-round, and winter is when wood is cut. A
   forester with trees standing on their ground fells them in winter, as before.
3. **`SimWorld.Plant` refuses in winter.** It is the one door a forester's sapling goes in by, so this is the
   guarantee: whatever sends a forester to bare ground, no sapling goes in between the first and last day of
   winter.
4. **`NextGroundToWork` sends nobody to bare ground in winter.** A forester with nothing to fell is a **spare
   hand**: tidying, helping clear, or home. This is the farmer's winter (`crops-and-orchards.md §5`: *"when there
   is nothing to farm, the marginal hand is worth more at the wood"*).
5. **The ground is asked again on arrival** (the field's rule, `ArriveInTheField`). A planting walk that set out
   in autumn and arrives in winter does not start its stint. The forester decides again.
6. **The demand falls to zero in winter.** `ForesterSeatsWithGroundToPlant` is zero in winter, as
   `FarmerSeatsWithGroundToWork` is. It only staffs a hut whose log limit is met (D146), so in winter that hut's
   hands go to other work and come back in spring.
7. **Said out loud (§7.1).** The forester's note and the hut's idle line say *"the ground is frozen — planting
   waits for spring"* when it is winter and the hut has bare ground it would otherwise plant. Without that line,
   a forester standing idle beside bare ground is the silent stall §1.1 forbids.

### 10.2 What it costs — the economy had already assumed it

⭐ **`VillageEconomy.YearsToRewoodOnesGround` already budgets planting on three-quarters of the year** (the
gatherable year, `TicksPerYear × 3/4`). The derivation behind §5's *"~2 years for the ground one pair of hands
keeps"* already assumed no winter planting. The behaviour planted all four seasons and never matched it. So no
number in `data/` changes, and the derived one is unmoved.

**Measured before and after, in one sitting** (`tools/harness/ZzForesterWinter.cs`). The runs used copies of
Joe's own saves, ten years on. 24 of 94 folders' newest saves load at today's format, and 8 of them have a
forester's hut with painted ground. The *before* arm is `main`'s code in a scratch worktree; the *after* arm is
this slice. A sapling counts as planted when grass becomes a *young* sapling (D220), so the sweep's own seeding
is counted apart. Each step is counted in the season its clock read when it began: ⚠️ the first after-run read
the season after the step and counted autumn's last-tick saplings as winter's.

| village (year at load, people) | ground | planted sp / su / fa / **wi**, before → after | wooded/young/bare at +10y, before → after | people at +10y |
|---|---|---|---|---|
| Hollowmere (Y84, 70) | 126 | 10/15/8/**10** = 43 → 20/11/7/**0** = 38 | 114/12/0 → 115/8/3 | 77 → 77 |
| Oakhaven (Y28, 24) | 115 | 7/6/8/**6** = 27 → 9/7/5/**0** = 21 | 113/2/0 → 113/2/0 | 41 → 41 |
| Stonebridge (Y23, 18) | 109 | 6/8/7/**5** = 26 → 8/11/9/**0** = 28 | 105/2/2 → 106/1/2 | 31 → 29 |
| Willowdale, elder-yarrow (Y22, 16) | 187 | 1/1/1/**0** = 3 → 1/1/1/**0** = 3 | 181/4/0 → 181/4/0 | 27 → 27 |
| Willowdale, hazel-kestrel (Y14, 12) | 145 | 4/3/5/**3** = 15 → 7/4/4/**0** = 15 | 143/2/0 → 143/2/0 | 17 → 17 |
| Fernhollow (Y4, 4) | 107 | 0/2/12/**0** = 14 → the same | 107/0/0 → 107/0/0 | 10 → 10 |
| Ashbourne (Y1, a founding) | 328 | 21/72/83/**42** = 218 → 19/81/83/**0** = 183 | 196/86/46 → 177/70/81 | 3 → 5 |
| Larkspur (Y1, a founding) | 239 | 12/46/63/**35** = 156 → 13/55/62/**0** = 130 | 168/48/23 → 142/49/48 | 2 → 4 |

- **Before, 101 of 502 saplings (20 %) went in in winter. After, none did, and 432 went in: 14 % fewer.**
- **The six established villages plant 7 % fewer (128 → 119).** Their ground is wooded to within a tile or two,
  so planting there is bounded by bare tiles, not by days. The winter's saplings go in in spring instead
  (Hollowmere's spring 10 → 20), and the woods end the same to within four tiles. **Over twenty years, 307 → 283
  (−8 %)**, Hollowmere's spring 19 → 41.
- **The two foundings plant 16 % fewer (374 → 313)**, and their ground is 25–35 tiles barer after ten years. They
  have hundreds of bare tiles, so planting is bounded by days, and winter's fifth is real. ⚠️ Both are a harness
  playing a Year-1 village unattended, and it freezes in both arms (cold 14 → 12, 10 → 8). That is the harness
  playing badly, not a design loss. Their planting counts are the measurement; their survival is not.
- **Felling rose slightly** where planting fell (Hollowmere 1134 → 1151 logs, Oakhaven 510 → 528, Stonebridge
  531 → 546). A forester who is not planting in winter has more time for the trees. In the foundings it fell
  (165 → 150, 120 → 105), because fewer saplings had grown into trees. **Winter at the trees**, in forester
  hand-ticks: Hollowmere 248 → 106, with 0 → 45 spent clearing and tidying instead. The spare hand is visible.
- **Survival: none of it is this change.** Ten years: the same in five of six established villages; Stonebridge
  ends 31 → 29 with 2 starved. Twenty years: Stonebridge starves in **both** arms (14 → 22) and Fernhollow
  freezes in both, so those are the harness playing an unattended village badly. The other four end at
  73 → 78, 54 → 55, 41 → 40 and 28 → 25: noise.
- **D666's bar is untouched.** A forester feeds the woodcutter by felling, and felling did not fall.

### 10.3 Tests

- `PlantingIsEverySeasonButWinter`: the rule, four seasons.
- `AForesterPlantsNothingInWinterAndPlantsAgainInSpring`: a hut with only bare ground, its forester kept on. It
  plants in autumn (anti-vacuity), plants nothing from the first tick of winter to the last (counted by the
  young bit, so the sweep's seeding cannot pass for planting), says the ground is frozen on the forester's note
  and on the hut's card, and plants again in spring. **Three things had to be true for the spring half to mean
  anything, and each was found by the guard returning 0 under the old rule too:**
  - ground that outlasts a year (radius 8, 143 tiles; at radius 5 all 53 were planted by winter's end);
  - felling off (`PlantOnly`), or the first spring's saplings, trees by the next spring, are felled first;
  - a winter's firewood in every hearth, or the fixture village, with its only forester resting a wood, freezes
    before spring.
- `AForesterFellsInWinter`: wooded ground, felling on. Logs come off it in winter, so the rule touched
  planting only.
- `NoPlantingSeatsAreWantedInWinter`: `ForesterSeatsWithGroundToPlant` is the hut's places in autumn and zero
  in winter.
- `PlantRefusesInWinter`: the door itself.
- **Red-check (D326): 8 mutants, 7 red.**
  - The rule always true: 4 guards fail.
  - `Plant`'s season check removed: `PlantRefusesInWinter` fails.
  - The tile-picker's winter check removed: **the test host dies of a stack overflow**. The arrival check
    refuses the tile and decides again, and the picker hands the same tile straight back. The two must agree;
    in the shipped code they cannot disagree.
  - The seat count's winter zero removed: `NoPlantingSeatsAreWantedInWinter` fails.
  - The card's line or the forester's note removed: the winter guard fails on its `frozen` assertions.
  - `Plant`'s check and the arrival check removed together: `PlantRefusesInWinter` fails.
  - ⚠️ **The zero, kept:** the arrival check removed alone. No planting walk crossed into winter in these
    fixtures, and `Plant` would refuse it anyway. The arrival check saves a forester a stint for nothing; no
    guard sees it.
- **Goldens: three moved, re-taken with the old values beside them** (`PhaseFiveGoldenTests`' two, `SkillTests`' shipped
  fifty years). Each is proven by ablation: with `IsPlanting` true in every season, all three old constants pass.
  The fixture's fifty years did not move.

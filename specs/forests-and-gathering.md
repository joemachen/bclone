# Spec: Forests, gathering, and the end of the distance fences

**Decisions:** this document, and Joe's three calls of 2026-08-07 recorded in §1.
Neighbours: D18, D19, D24, D40, D42, D43, D58, D84, D86, D87, D88, D107, D111.
Supersedes in part: `building-placement.md §12.5(3)`, `§12.6`; `professions.md §6.1`, `§6.2`;
`DESIGN.md §5`'s open "7-tile bound".
**Status:** ⚠️ **Specced, not built.** Written before the code (METHODOLOGY §2).

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
```

Three things recommend it:

- **It is a number the player can see on the map.** The ring the hut draws is also the distance
  the economy assumes people live within. Today's 7 is an artefact of where a generator dropped
  a berry patch, and no player could ever learn it.
- **The stated target barely changes:** *one gatherer at a **fully wooded** hut, at minimum
  vigour, feeds themselves and `RequiredDependants` children.* Only the two words in bold are new.
- **It stops being a fence and becomes a budget.** `Household.ChooseSite` no longer *refuses*
  ground beyond it — it still **scores** `toWork + toStore` and picks the nearest, which is what
  actually shapes a village. Building beyond the budget is allowed, warned about, and genuinely
  costs food, because the villager really does walk further and really does make fewer trips.

**That is D58's settled mechanism arriving at last:** distance stops being a restriction and
becomes a consequence.

### 3.3 Gatherers work at the hut

**Recommended: a gatherer's job position is the hut, and density scales the yield** — they do not
walk to individual trees.

It satisfies Joe's rule exactly (nothing outside the ring contributes anything) while reusing the
existing forager behaviour whole, so the slice stays small. The drawn ring and the panel sentence
carry the legibility. **Walking to individual tiles stays on the board** as a later change if the
hut reads flat; it is a behaviour change, not a model change, so it can be made without touching
any of this.

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
5. **Run the game after slices 1 and 2** — the forest and the ring are the two things only eyes
   can check (D11). ~~`BCLONE_SCREENSHOT` + `BCLONE_SCREENSHOT_YEARS`~~ — the automated hook is
   gone (D117); this is Joe playing it and reporting what he sees.
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

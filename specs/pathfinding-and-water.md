# Spec: Pathfinding, and water you have to go round

> Status: **built 2026-07-28 — see §12** · Owner: Joe + Claude Code
> Format per `METHODOLOGY.md §2`. Implements **D40**; slice 2 of `specs/seeded-map-generation.md §11`.

---

## 1. Goal

Make the generated river mean something: **water is impassable**, so a route has to go round it, and every system that asks "how far is that?" gets an answer that accounts for the terrain.

Today `TravelCostField.Cost` is Manhattan distance and `GridPos.StepToward` walks in a straight line. Neither knows terrain exists, so a villager strolls across the river and the sim agrees it was the shortest way.

---

## 2. Which pillars this serves

- **§2.5 Environment with teeth** — this is the pillar. Terrain *dictating viability* rather than decorating it. Until this lands, the valley is a picture.
- **§2.6 Desire-path roads** — the trample field layers onto exactly this cost field. A crossing is the first place traffic will genuinely concentrate, which is what desire paths need to be interesting.
- **§2.2 Smart labour** — catchment is measured in travel cost, so a household across the river from the berry patch is *actually* cut off rather than nominally near.
- **§2.7 Knowledge-based tech tree** — bridges are the unlock this creates a reason for (D40).
- **Non-negotiable 1** — a player must be able to see why a family is struggling: "they are on the wrong side of the water" is a legible answer, and only becomes sayable once it is true.

---

## 3. The hard problem: this field decides who eats, and it is asked constantly

`TravelCostField` is read by labour catchment (D15, D23), market errands (D36), household placement (D18), and the economy's distance budget. It is the busiest question in the sim, and it is asked in the hot path — the annual labour pass is N villagers × M workplaces, and a marketer re-plans whenever they finish a leg.

A path search per call would be the obvious implementation and the wrong one.

### 3.1 The property that makes this cheap

**Every query in the sim has a building at one end.** Verified against every call site:

| Caller | Query | Building end |
|---|---|---|
| `LabourAllocator.CostBetween` | home → workplace | both |
| `BehaviorSystem.NearestStoreHolding` / `NearestStoreAccepting` | position → store | store |
| `BehaviorSystem.PlanMarketErrand` | position → store, or position → home | both |
| `Household.ChooseSite` | candidate tile → forage site, → granary | site/granary |
| `BehaviorSystem.Travel` | position → target (always a building) | target |

So: **one Dijkstra flow field per building**, computed over the static terrain. Every cost query becomes an array lookup, and — the part that matters for movement — the field also gives the *direction* of the next step, by walking downhill. No per-villager path storage, no re-planning, no path invalidation.

### 3.2 Cost

A 120×80 valley is 9,600 tiles. Fields needed: one per workplace (~9), per store (3), per home (~25 at steady state). Call it 40 fields × 9,600 ints = **~1.5 MB**, rebuilt only when a building appears or disappears — which is rare, and never mid-tick.

This also keeps the D2 integer rule: Dijkstra over integer tile costs, no floats anywhere.

---

## 4. Data model

```
TerrainCostField                       (one per building, cached on the world)
    Destination : GridPos
    Cost        : int[width * height]  // Unreachable = int.MaxValue
    StepFrom(GridPos) -> GridPos       // one step downhill toward Destination

TravelCostField                        (unchanged as the public face)
    Cost(from, to)        -> looks up the field for `to`, else falls back
    TicksBetween(from, to)
    IsWithinCatchment(...)
```

`TravelCostField` stays the single source of truth for travel cost (§2.6's non-negotiable). What changes is what it consults.

**Unreachable is a real answer**, not a large number. `int.MaxValue` propagating through arithmetic is a bug waiting to happen, so the API should say `TryCost` / return a sentinel that callers must handle — catchment already asks "is this within reach?", which is the right shape.

---

## 5. What has to be re-derived, not patched

`VillageEconomy` budgets the worst home-to-work walk (`MaxHomeToWorkTiles`). **A path round water is longer than a straight line**, so that budget is now wrong, and this is exactly the D16 mistake if it gets patched instead of re-derived.

Two options, and the second is recommended for the same reason it was in map generation:

1. Derive the economy from the *actual* worst path per seed — makes the economy a property of the seed again.
2. **Have the generator guarantee a bound**: no home site may be more than `MaxHomeToWorkTiles` of *path* from work, and `Household.ChooseSite` already enforces exactly that — it just needs to ask the new field instead of Manhattan distance. The budget then holds by construction, as it does today.

**Recommended: (2).** It is the same trick, it keeps one economy for all seeds, and `ChooseSite` is already the enforcement point.

---

## 6. What the generator now owes

Until bridges exist (D40, slice 3), **the generator must not cut the village off from its work.** Today it only guarantees the founding site is not *in* the river. That is no longer enough: a river between the settlement and every forage site is a dead valley.

The guarantee: **from the founding site, at least `forage_site_count`-minus-a-margin sites and at least one tree stand must be reachable on land.** Checked by flood fill at generation time, redrawn if it fails — the bounded retry §3 of the map spec already anticipated.

This is also where the measured case from `seeded-map-generation.md §12.3` lands: on some seeds the river runs straight through the settlement.

---

## 7. Failure modes to design against

- **A village cut in half.** The main one. Covered by §6, and by a property test over many seeds.
- **Someone walking on water.** The regression this whole slice exists to prevent — asserted directly, every tick, not inferred.
- **A villager stuck against a wall.** Straight-line stepping into an obstacle repeats forever. Walking downhill on a flow field cannot get stuck, because a finite cost always has a lower neighbour — but the *unreachable* case must be handled explicitly rather than by walking into the bank.
- **Stale fields.** A field cached for a building that has moved or gone is the "code reading state from where it used to live" shape this project keeps meeting. Fields are rebuilt when the building set changes, and that must be the only way they change.
- **The economy silently getting harder.** Longer real paths with an unchanged budget starves the village slowly — the exact failure map generation just produced. Covered by §5.
- **Determinism.** Dijkstra with ties broken by scan order is deterministic; ties broken by a priority queue's internal ordering may not be. The tie-break must be explicit.

---

## 8. Testing

- **Nobody ever stands on water** — every tick, over a long run.
- **A cost across the river is longer than the straight line**, and the anti-vacuity twin: with no river, costs match Manhattan distance exactly (so a broken field cannot pass by returning Manhattan everywhere).
- **Unreachable is reported as unreachable**, not as a big number.
- **Every seed leaves the village connected to its work** (§6), over many seeds.
- **The village still holds a stable size for 300 years**, economy re-derived.
- **Determinism** — same seed, identical paths, identical costs, over 300 years.
- **Fields are rebuilt when a building appears** — a home built after the fields were computed is reachable.
- **Performance is not pathological**: the 300-year acceptance run must not slow by more than a small factor. Stated as a test-suite duration guard rather than a benchmark, since a per-call search would show up as minutes.

---

## 9. Definition of Done

Standard DoD (`METHODOLOGY.md §3`), plus:

> **Water is impassable and every route respects it, no seed strands a village from its work, the economy is re-derived for real paths rather than patched, and a player can look at a household on the far bank and understand why it is struggling.**

---

## 10. Sequencing

1. **`TerrainCostField`** — Dijkstra, flow fields, unreachable as a first-class answer. Pure, tested on its own, nothing wired up.
2. **`TravelCostField` consults it**, and movement walks the field. Water becomes impassable in one step, because cost and movement disagreeing is worse than either being wrong.
3. **The generator's connectivity guarantee**, and the economy re-derived against real paths.

Slice 3 — bridges — is a separate spec and needs the tech tree and placement first.

---

## 11. Open questions (for Joe)

1. **Should a villager already mid-journey when the world changes re-plan?** With flow fields this is free — they read the field each step, so they adapt automatically. Noted rather than asked: the answer is yes and it costs nothing.
2. **Does a river tile ever have a cost, rather than being impassable?** A ford would be a cheap way to make some crossings possible without bridges, and it is one line (water costs 10× rather than infinity). **Recommended: no fords for now** — it would blunt exactly the pressure D40 creates, and "you cannot cross until you learn to bridge" is the cleaner rule to hang a technology on.

---

## 12. What happened (built 2026-07-28)

**Water is impassable and 305 tests are green.** The design held: flow fields per building, every query an array lookup, movement walking the same field so cost and route can never disagree.

### 12.1 The bug it flushed out — the same shape as always

§6 said the generator owed the village a valley it could live in, and the first implementation read that as *"the founding site must not be in the river"*. It was not enough, in two ways, and seed 1 demonstrated both at once:

- **The village's own buildings were placed at fixed offsets with no terrain check.** The shed and the woodcutter's hut both came down *in the water*. No logs could be stored, no firewood made, and all four founders froze in the first winter — **peak population zero**. Nothing in the log said "your shed is in the river"; it said they were cold.
- **Founding homes were still dropped on a spiral**, so they could land in the river too, and a family standing on water cannot take a step.

**Dry is not the same as reachable**, and that is the sharper half. A building on the far bank is exactly as useless as one under water and looks perfectly fine on the map. Placement now asks *"can the village walk here from the founding site?"* — one flow field from the origin, reused for every candidate — and the founding site itself is chosen as the land mass holding the most work.

Founding homes now go through `Household.ChooseSite`, the same rule every later home uses. **One rule for the whole village rather than one for the founders and another for their children**, which also means the founders can no longer be handed a start their descendants would never choose.

### 12.2 Two test assumptions this invalidated, both worth recording

- **"A one-tile catchment reaches nobody"** was true only while homes were spiralled without regard to work. Now a home can legitimately sit beside a patch. The test was rewritten to assert the stronger invariant it was really about: *nobody ever holds a job outside their catchment*.
- **`CouplesLeaveHomeWithFoodToStartOn` was checking the wrong field.** It asserted `LifetimeGathered > 0` a century in — but a dowry moves through `Stockpile.Receive`, which deliberately does *not* touch that counter, because goods changing hands are not production. So it was really asserting "has foraged at some point since", and it failed the first time a household happened to be founded near the end of the run. It now checks the larder **at the tick of formation**, which is the only moment the claim is about.

### 12.3 The river is impassable and it does not yet matter — and that is structural

Joe, looking at a run: *"the villagers have no need to walk around the river at this point. nothing they need is on the other side."* Correct, and it is not a quirk of that seed — **I built it that way without noticing.**

Three things push the village away from the water, and they compound:

1. **Forage sites are drawn on a small ring** around the world origin (`forage_site_ring_tiles` = 5), so all the work is clustered in one place.
2. **The founding site is chosen as the land mass holding the most work** (§12.1) — which, by construction, is the side of the river with nearly everything on it.
3. **Homes are then placed near that work** (`ChooseSite`), so the settlement contracts further inward.

The result is a village that always forms in the middle of its own resources, with the river out at the periphery. **Water is impassable, correctly and provably, and nothing ever needs to cross it.** D40's whole argument — *"a river you must go round until you can afford not to is the map arguing with you"* — is not happening. The river is scenery again; it is just *enforced* scenery now.

**This is not fixed by making the terrain harsher.** The connectivity guarantee in §6 exists so that no seed is unplayable, and it is doing its job. The problem is that the guarantee and the pressure are the same knob: any valley where the river genuinely divides the resources is a valley the generator currently refuses to found a village in.

**What would actually create the tension** — recorded so it is not rediscovered:

- **Spread the resources wider than the settlement**, so a village *starts* self-sufficient but has to reach across the water to grow. That is the shape the pressure wants: not "you are cut off", which is unplayable, but "the good land is over there".
- **Which means the founding-site rule should ask for enough work, not the most work.** "Most" always picks the safe side; "enough to start" allows a valley where expansion means crossing.
- Both of these are **worth doing at the same time as bridges** (D40 slice 3), because a river you must cross with nothing to cross it with is just a wall. Until then, the honest description of what shipped is: *water is impassable, the machinery is right, and the map does not yet use it.*

### 12.4 Cost

The suite went from ~25s to ~2m. Most of that is the new property tests (60-year runs across 20 seeds), not the fields themselves. Worth watching rather than acting on; if it grows again, the first thing to try is fewer seeds in the sweeps rather than a cleverer search.

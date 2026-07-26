# Spec: Village Labour Allocation

> Status: **draft — awaiting Joe's review before implementation** · Owner: Joe + Claude Code
> Format per `METHODOLOGY.md §2`. Written *after* three failed improvised attempts (see §3) — that is the reason it exists.

---

## 1. Goal

Decide **who works where** across the whole village, in one pass, deterministically, and in a way that can be explained to the player one villager at a time.

This replaces the current per-workplace logic in `LabourSystem`, which walks workplaces in id order and fills each from the nearest available villager. That works for one workplace and fails for several.

---

## 2. Which pillars / non-negotiables this serves

- **§2.2 Smart labour** — the pillar. It is the mechanism the phase exists to prove, and it is currently the thing blocking catchment from constraining anything.
- **Non-negotiable 1: Legibility.** Every assignment must still yield one sentence naming the place, the distance, and the villager who did not get it. An allocator whose output cannot be explained is worse than a simpler rule that can.
- **Non-negotiable 2: Meditative pace.** More workplaces must not mean more player decisions. The player never assigns anyone.
- **Architecture: determinism.** N villagers × M workplaces is the largest ordering surface in the sim so far.

---

## 3. Why the obvious approaches failed

Recorded because all three looked correct when written, and each was caught only by a test.

| Attempt | What happened |
|---|---|
| **Fixed generous demand** (`forager_demand: 200`) | Every villager went to the lowest-id workplace. Catchment never bound; the second job kind was decorative. |
| **Demand = population ÷ dependants-supported** | Mathematically sufficient and still fatal. Food is stored **per household** while labour is assigned **village-wide**, so a household with no forager produced nothing and waited on seasonal sharing. |
| **Demand duplicated onto every forage site** | Four sites had room for four villages. The sites absorbed everyone, nobody reached the tree stand, timber stopped. |
| **Demand split evenly across sites** | Worse. It *forces* villagers to distant sites rather than letting proximity sort them — the near patch takes one person and the next villager is sent across the valley, to starve beside a patch they were not permitted to work. |

**The diagnosis:** labour demand is a **global allocation problem** being solved with **local, per-workplace rules**. Every attempt above tried to encode a village-wide constraint into a single workplace's `LabourDemand` field, and there is no value of that field that expresses "the village needs twelve foragers, spread across whichever sites are nearest to whoever is free".

---

## 4. The design

**Separate the two questions the old code conflated:**

1. **How much of each kind of work does the village need?** — a village-level quota.
2. **Who does it, and where?** — proximity, resolved by a global matching pass.

`Workplace.LabourDemand` becomes a **local capacity** (how many can physically work this site at once) and stops carrying village-level meaning. The constraint moves to a quota per `JobKind`.

### 4a. Quotas

Derived, not tuned — the same discipline as `VillageEconomy` (decision D16):

```
foragersNeeded  = ceil(mouths / VillageEconomy.RequiredDependants)   // hands to feed everyone
woodcuttersWanted = remaining able hands, capped by total stand capacity
```

Foragers first: **a village short of hands feeds itself before it builds.** That priority is the whole policy, and it is one sentence, which is the test of whether it is legible.

### 4b. The matching pass

Single-pass greedy over an explicitly sorted candidate list. Runs seasonally, not per tick.

```
candidates = [ (villager, workplace, cost) for every able unassigned villager
                                           × every workplace within catchment ]

sort candidates by (cost ASC, villagerId ASC, workplaceId ASC)

for each candidate in order:
    skip if villager already assigned
    skip if workplace at local capacity
    skip if that JobKind's quota is exhausted
    assign, and record the reason
```

**Why greedy-nearest-first rather than optimal:** a global optimum (min total travel) would need Hungarian-style matching, and its output cannot be explained one villager at a time — "you work here because it minimised a village-wide sum" fails non-negotiable 1. Greedy-nearest is explainable in a sentence and is what a person would actually do.

**Why cost-first rather than villager-first:** iterating villagers in id order lets villager #1 claim a distant site before villager #9 — who lives beside it — gets a look in. Sorting by cost means the shortest commutes are claimed first, which is both better and easier to justify.

### 4c. Shedding

When a quota shrinks (population falls, or a stand fills), release the **furthest-travelling** worker first, not the highest id. Longest commute is the weakest claim, and it is a reason that can be stated.

---

## 5. Determinism

The largest ordering surface in the sim so far, so the rules are explicit:

- The candidate list is built by iterating villagers in id order, then workplaces in id order, and is then **sorted by an explicit total ordering** — `(cost, villagerId, workplaceId)`. No two candidates can compare equal, so no tie is left to the sort's stability.
- Quotas are integers derived from counts, never floats.
- Single-pass, single-threaded.
- No `Dictionary`/`HashSet` iteration anywhere in the pass.

---

## 6. Legibility

Unchanged in spirit, richer in content. Every assignment records:

> *Otto took work at the western thicket — 4 tiles from home. The berry patch was nearer (2) but already had its hands. Bess was equally close to the thicket; the tie went to the elder claim.*

And every refusal:

> *No work: the village has enough foragers for 31 mouths, and the tree stand is full.*

A villager with no work must always be able to say **which constraint** excluded them — quota, capacity, or catchment. That is the difference between a village that reads as inscrutable and one that reads as full.

---

## 7. Testing

- **Nobody is sent past a nearer opening.** The failure mode of the even-split attempt, asserted directly.
- **Quotas are respected**: total foragers never exceeds what the village needs fed.
- **Local capacity is respected**: no site exceeds its own `LabourDemand`.
- **Foragers are staffed before woodcutters** when hands are short.
- **Catchment still binds**: no assignment outside a workplace's radius.
- **Shedding takes the furthest first** — asserted, since "highest id" is the tempting shortcut.
- **Determinism**: same seed ⇒ identical assignments and identical reason strings, over a full village and 150 years.
- **Every villager can name the constraint that excluded them.**
- **The village survives with catchment genuinely binding** — the thing none of the three previous attempts achieved, and the real acceptance test.

## 8. Definition of Done

Standard DoD (`METHODOLOGY.md §3`), plus: **`forager_catchment_tiles` can be lowered to a value that visibly binds — outlying households restricted to their nearest site — and the village still sustains itself for 150 years.** Until that holds, the allocator has not solved the problem it exists to solve.

---

## 9. Open questions (for Joe)

1. **Should a villager prefer their household's needs over the village's?** Right now food is per-household but labour is village-wide, and that mismatch has already caused one starvation bug (§3, attempt 2). Options: leave it and let the sharing policy cover it; or bias assignment so each household keeps at least one forager. *(Recommendation: bias it — one forager per household as a floor before the general pass. It matches how the food is actually stored, and "someone in every house brings food home" is a sentence.)*
2. **Should distance to work be visible on the map as a line?** Cheap, and would make a misallocation obvious at a glance rather than requiring a click. *(Recommendation: yes, for the selected villager only — a line from home to work. Every villager would be spaghetti.)*

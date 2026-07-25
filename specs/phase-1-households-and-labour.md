# Spec: Phase 1 — Households & Smart Labour

> Status: **draft — awaiting Joe's review before implementation** · Owner: Joe + Claude Code
> Format per `METHODOLOGY.md §2`. Living doc — update it if reality diverges.

---

## 1. Goal

Go from **one villager** to **a village**: several people, living in households, who take work themselves.

The pillar this exists to prove is `DESIGN.md §2.2`: **no manual building assignment, ever.** The player never slots N workers into a building. Villagers take jobs by proximity, skill, and household, driven by policy. The thing being deleted is the Banished pattern of assigning a worker and teleporting their brain into a workplace.

If Phase 0 proved *a life*, Phase 1 has to prove *a division of labour* — and keep it as readable as one villager was.

---

## 2. Which pillars / non-negotiables this serves

- **§2.2 Smart labour** — the pillar. Labour demand plus catchment radius, not worker slots.
- **Non-negotiable 1: Legibility.** The hard part. With one villager, "why is she doing that" was obvious. With twelve, it is not. **The player must be able to click any villager and get a straight answer for why they took this job over the alternatives.** This is the phase's real deliverable, exactly as the life log was Phase 0's.
- **Non-negotiable 4: Stories from people.** Households make villagers relational rather than fungible — a death is now someone's parent.
- **Non-negotiable 5: Generational time.** Birth, childhood, and inheritance of a home turn "aging and dying" into an actual generational loop.
- **Non-negotiable 2: Meditative pace.** More people must not mean more clicking. Population growth should *reduce* per-person attention, not multiply it.

---

## 3. Scope

### In scope
- **Multiple villagers** (start ~4, grow to a few dozen) with the Phase 0 life model intact: hunger, vigour, ageing, both death paths.
- **Households**: a home building with residents; couples; **birth**; children who belong to a household.
- **Childhood** — finally. Age-gated capability: children cannot do adult work and depend on the household's food. This was deferred from Phase 0 (D13) precisely because it needed households.
- **Workplaces** with a **labour demand** (how much work is wanted) and a **catchment radius** (how far is reasonable to travel).
- **Job assignment by policy**, computed each tick from proximity, capability, and household — never by player click.
- **The shared travel-cost field** (`DESIGN.md §2.6` integration note) — a single source of truth for "how far is that", used by both catchment and, later, desire paths. **Build it once, here.**
- **A "why did you take this job" inspector** — the legibility deliverable.
- Extending the life log to a **village log** without drowning it (Phase 0's lesson: 600 receipts is not a story).

### Out of scope — explicitly deferred
- **Skill growth and apprenticeship** (§2.1) → Phase 4. Villagers have *capability* (can/cannot do a job by age and health) but skill does not yet grow with time-on-task.
- **Desire-path roads** (§2.6) → Phase 3. The shared cost field is built here; the trample/decay layer on top of it is not.
- **Real pathfinding.** Travel stays abstract — cost is a function of distance over the shared field. A* arrives with roads.
- **Tech tree** (§2.7), **systemic pressures** (§2.3), **trade and region** (§2.4), **biomes** (§2.5) → later phases.
- **Save/load** → still deferred; determinism keeps it cheap.

If any deferred item starts feeling necessary, **stop and flag it** — that is the signal the phase is over-scoped, and it was right in Phase 0.

---

## 4. The two hard problems

Worth naming before designing, because everything else is bookkeeping.

### 4a. Legibility at N villagers
One villager's behaviour was self-evident. Twelve villagers making simultaneous decisions is exactly the opaque-simulation failure mode `DESIGN.md §2.2` warns about.

**Approach:** the job-assignment decision must produce a *reason*, not just a result. Every assignment records the candidates it considered and why the winner won, in plain language:

> *Otto took Woodcutting at the north stand. Nearest able worker (4 tiles). Bess was closer (2) but is feeding an infant.*

Same discipline as the Phase 0 behaviour system: a ranked top-down list of reasons a person can read, never a weighted score nobody can explain. **If a decision cannot be explained in one sentence, the design is wrong.**

### 4b. Determinism with many agents
Phase 0 had one villager and no ordering questions. N villagers competing for M jobs introduces every classic determinism trap: iteration order, tie-breaking, and "who picks first".

**Rules:**
- Villagers are stored in a **stable, ordered array** and always iterated in id order. Never a `Dictionary`/`HashSet` — the banned-API notes already flag iteration order as the hazard the analyzer cannot catch.
- **Every tie is broken explicitly and deterministically** (by villager id, then job id). A tie resolved by "whichever came first" is a desync waiting to happen.
- Job assignment is a **single-pass, single-threaded** algorithm over sorted collections.
- The determinism test extends to a full village: same seed, N villagers, 50 years ⇒ identical state and log.

---

## 5. Data model (sketch — refine during implementation)

```
Villager  (extends Phase 0)
    HouseholdId    : int
    LifeStage      : enum { Child, Adult, Elder }   // derived from age
    CurrentJob     : JobId?                          // null = idle/resting
    JobReason      : string                          // why they hold it — for the inspector

Household
    Id             : int
    HomePosition   : GridPos
    MemberIds      : int[]                           // ordered, stable
    Stockpile                                        // food is per-household, not global

Workplace
    Id             : int
    Kind           : from data file
    Position       : GridPos
    LabourDemand   : int                             // how many worker-ticks wanted
    CatchmentRadius: int                             // in shared-cost units, not tiles

TravelCostField
    Cost(from, to) : int                             // THE shared source of truth (§2.6)
```

**Note:** food moves from a single global stockpile to **per-household** stores. That is what makes one household starving while another thrives possible — and it is the seed of the inequality stories the design wants.

---

## 6. Tick order (extends Phase 0's)

1. Clock
2. Ageing (vigour; now also life-stage transitions)
3. Needs (hunger, per villager in id order)
4. **Households** — births, deaths reshaping membership, food sharing within a home
5. **Labour assignment** — recompute job assignments from policy
6. Behaviour — each villager acts on their assigned job, in id order
7. Mortality
8. Narration

Ordering is part of the determinism contract (D5). Labour assignment runs **before** behaviour so that everyone acts on a consistent view of the world within a tick.

---

## 7. Testing

Everything from Phase 0 stays green, plus:

- **Determinism at scale:** same seed, full village, 50 years ⇒ identical hash and log. Extends the P0 suite.
- **No manual assignment exists:** an architectural test asserting no public API lets a caller assign a villager to a workplace. The pillar should be impossible to violate, not merely discouraged.
- **Tie-breaking is stable:** two equally-suited villagers, identical distance ⇒ the same one wins every run.
- **Catchment:** a villager does not cross the map for one log (the named failure mode in §2.2).
- **Children cannot take adult work**; households feed their dependents.
- **Every assignment carries a reason string** — non-empty, and naming the runner-up where there was one.
- **Legibility scenario:** a scripted village where a specific villager takes a surprising job, asserting the recorded reason actually explains it.

## 8. Definition of Done

Standard DoD (`METHODOLOGY.md §3`), plus the phase's own gate:

> **Success test:** watching twelve villagers is still *legible*. The player can click any one of them and understand why they are doing what they are doing, and the village's division of labour makes sense without the player having assigned anybody. If it reads as an ant farm — busy, opaque, and unaccountable — the phase has failed regardless of green tests.

---

## 9. Open questions (for Joe)

1. **Starting population and growth.** Begin with ~4 adults (two couples) and let it grow, or a wider founding group? Growth rate matters for pace — a village that doubles every decade outruns legibility fast.
2. **Household food vs. village food.** Per-household stores create real inequality stories, but also a fairness problem: does a starving household get help automatically, or does the player watch it fail? *(Recommendation: per-household stores with an explicit, visible sharing policy — inequality you can see and act on is a story; inequality you cannot is just cruelty.)*
3. **How many job kinds to start with?** *(Recommendation: exactly two — forager and woodcutter. Two is the minimum that makes "which job" a real decision, and it keeps the first labour system small enough to debug.)*
4. **Does the player set labour policy at all in Phase 1**, or does it stay fully automatic until there is something meaningful to tune? *(Recommendation: fully automatic first. Add policy levers once we can see what actually needs steering.)*

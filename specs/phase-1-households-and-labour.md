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

## 8b. ✅ Resolved — household formation (2026-07-25)

Grown, unpaired adults now pair off **across** households and found new homes, carrying a dowry from both parents' larders. The village went from 2 households / 10 people ever born to **8 households / 27 ever born**.

Design notes worth keeping:
- **Matching is fully ordered** — candidates visited in villager-id order, each taking the lowest-id eligible partner. No scoring, no randomness. A matching problem is exactly where a tie resolved by iteration order becomes a desync (§4b), and it means "why those two?" has a one-sentence answer.
- **Partners must come from different households** — the closest thing to an incest rule this model can express, since everyone in a house is a parent or a sibling.
- **Only a *couple* has children**, not any two fertile adults. Otherwise siblings breed in the parental home and the village grows without ever forming a household, hiding the problem this solves.
- **The dowry is not flavour.** A new household starting on an empty larder gets wiped out by its first winter before anyone has foraged anything — a death with no decision behind it, which the legibility non-negotiable rules out.

### ✅ Resolved — the economy is now derived, and the village sustains itself

**Before:** peak population ~18, extinct by year 91, every run.
**After:** population 8 → 171 over 180 years, food stores climbing throughout.

The fix was to stop tuning and state a target instead. `VillageEconomy` says it in one line:

> **A single adult at their weakest — minimum vigour, no partner — must be able to feed themselves and two children.**

That is not an arbitrary number: it is the widowed-parent case the diagnostics showed was killing nearly every household. `gather_yield` and `stockpile_target` are now *computed* from it rather than guessed, and tests assert the config still meets the target — so a future change to hunger, travel, or vigour that breaks it fails the build rather than the village.

**The bug this exposed was invisible and lethal.** New homes were placed in an ever-lengthening line, so the ninth household sat nineteen tiles from the berry patch against the first household's five — a round trip three times as long on the same working hours. Those families could not feed themselves and the village died of its own sprawl. Two changes: homes now cluster in a square spiral, and the economy budgets for the **furthest** home a village of `economy_horizon_households` will build, not the first.

That is the catchment problem from `DESIGN.md §2.2` arriving early, before the labour system exists to name it. **Distance to work is not flavour — it is whether you eat.** Good sign for the pillar; the labour system now has a real constraint to solve rather than a cosmetic one.

### ✅ Partly resolved — winter now bites; catchment still needs more workplaces

Raising the economy target from "one frail adult supports two dependants" to **three** bought the slack that pressure eats into. The third dependant is not a mouth to feed — it is margin. Re-running the pressure matrix:

| catchment | winter buffer | before target=3 | after |
|---|---|---|---|
| 9 tiles | 150% | dies | **dies** |
| 9 tiles | 260% | dies | **dies** |
| 20 tiles | 150% | dies | **survives** |
| 12 tiles | 180% | — | **survives** |

That separates the two problems cleanly, which is the useful part:

- **Winter pressure was a slack problem.** Fixed. The buffer is down from 260% to 180% and shipped, so a winter now takes a real bite out of the stores rather than scratching them.
- **Catchment at 9 tiles is not a slack problem at all.** No amount of margin helps, because an outlying household simply has *nothing within reach to work*. A binding catchment needs somewhere else to work — which is answer (1), multiple food sources, arriving at the same conclusion from a third direction now.

**Shipped: catchment 12 tiles, winter buffer 180%.** Both pressures are on and the village lives. Catchment at 12 is only just binding against a village spanning ten tiles, so it is honest to say it constrains lightly rather than properly — that waits for more workplaces.

<details>
<summary>The finding this resolved (kept for the reasoning)</summary>

### The village survives only because both pressure systems are inert

Found by drawing the map, not by testing. Two things were visible immediately and neither was caught by 228 green tests:

**Catchment does nothing.** `forager_catchment_tiles` is **40** while the whole village spans about ten tiles. The "nobody walks across the map for one log" guard — the centre of §2.2 — is effectively infinite. `NobodyWalksAcrossTheMapForOneLog` passes only because it overrides the radius to 6; at shipped values the pillar's central rule is decorative.

**Winter does not bite.** Six villagers sitting on 507 food; winter took the stores from 440 to a scratch. The economy derivation was sound but its target too generous — solving for "a frail widowed parent survives" produced a village that is never hungry.

**And here is the part that matters.** Turning either one on kills the village, *independently*:

| catchment | winter buffer | 150-year village |
|---|---|---|
| 9 | 150% | dies |
| 9 | 260% | dies |
| 20 | 150% | dies |

So the settlement is not robust-and-unpressured, it is **fragile and unpressured**. It survives precisely because nothing is pushing on it. The derived economy sits at break-even by construction — one frail adult supports exactly themselves plus two — so there is no slack for a longer walk or a thinner larder.

**This is a design problem, not a tuning one, and it should not be tuned.** Two candidate answers:

1. **Multiple food sources.** A binding catchment is only survivable if a distant household has something nearby to work. This is the same conclusion the "one food source does not scale" limit reached from the other direction, which is a good sign it is the real answer.
2. **Raise the economy target above break-even** — state that a frail adult must support themselves plus *three*, buying slack for pressure to eat into.

Probably both: (1) so catchment can bind, (2) so winter can bite.

**Reverted to catchment 40 / buffer 260 for now**, because a working village with the pressure switched off is more useful than a dead one with it on — and because guessing at values here would repeat exactly the tune-by-iteration mistake the derived economy was built to end.

</details>

### ⚠ Known limit — one food source does not scale forever

Past roughly 100 households the village outgrows its single berry patch: the outer ring is beyond the derived horizon and the population oscillates again. That is expected and is exactly what multiple workplaces plus catchment are for. Not a blocker for the labour system — it *is* the labour system's problem.

<details>
<summary>Earlier framing (kept for the reasoning)</summary>

### The village boom-busts (improved, not solved)

Measured rather than guessed at. A diagnostic run over 126 years gave the causes in order:

**1. Starvation, not old age.** 17 of 27 deaths were starvation. The earlier reading that "nobody starves" was true *before* household formation and stopped being true after it.

**2. Widowed parents.** When one parent dies, the survivor feeds the children alone on declining vigour. One worker cannot support a house that two built. This is a genuinely good story, but it was happening to almost everyone.

**3. Food existed while people starved.** Village stores sat at 170–290 while households died. The food was in the wrong houses.

Three fixes, each aimed at a measured cause:
- **Food sharing between households** — the policy D14 promised alongside per-household stores and I had not built. Givers keep `sharing_keep_percent` of their own target first, so generosity can never push a giver into need.
- **Sharing runs seasonally, not annually.** A household empties inside one winter; a yearly check arrived months after the funerals.
- **A real winter buffer.** `stockpile_target` 60 → 110 per person. At 60 a household stopped foraging at barely 1.5 winters' worth, so the village never built a buffer and any shock was fatal. Also `gather_yield` 34 and `max_household_size` 4 — two adults raising three children had *no* margin at all.

**Result: better, not fixed.** Peak population 10 → 18, and the village now builds a real surplus (1364 food at its peak against 425 before). But it still collapses, now around year 66: nine children born in one cluster, then a crash.

**Next hypothesis, untested:** this is a *synchronisation* problem, not a supply one. Couples form in waves, so their children arrive in waves, so a whole cohort of dependants hits at once and a whole cohort of workers ages out at once. Worth checking whether staggering `birth_interval_years` per household, or making birth depend on a rolling food trend rather than an instantaneous threshold, damps the oscillation.

**Recommendation: do not build the labour system on top of this yet.** A labour system measured against a village that boom-busts will be tuned to the wrong problem.

</details>

<details>
<summary>Earlier framing of this finding</summary>

### The village does not yet sustain itself

It grows and spreads, but at year 126 the population is **zero**. Growth outruns the food supply eventually and the settlement dies out. This needs diagnosis before the labour system goes in — a labour system for a village that cannot survive is measuring the wrong thing.

Worth checking first, in order: whether new households are formed faster than they can be fed; whether `max_household_size` plus `birth_food_threshold` allow a stable population at all; and whether foraging capacity per adult simply caps out below replacement.

</details>

<details>
<summary>Original finding (kept for the reasoning)</summary>

## Grown children never leave home

Births work, and the village grows from 4 to 10 people over its first decade. Then it stops dead and dies out with its last generation.

**The cause is not famine.** Every single death across 126 years is old age; nobody starves. That matters, because "the village collapsed" looks like a food problem and tuning it as one would have sent the whole economy the wrong way. The actual cause: a child stays in the household it was born into for life, so once every house hits `max_household_size` there is nowhere to put another child and births stop permanently. Household count never moves off its founding value.

**The missing piece is household formation** — a grown adult leaving to found a new home, presumably pairing with someone from another household. That is the mechanism that turns "4 adults growing outward" into an actual village, and it is squarely inside this phase's scope.

Two things it will need care on:
- **Determinism.** Pairing is a matching problem across households, and every tie needs an explicit, ordered rule (§4b).
- **Legibility.** "Why did Bess move out and marry into the Fletchers?" has to have a one-sentence answer, exactly like job assignment does.

Pinned by `GrownChildrenCannotYetFoundNewHouseholds`, which asserts the current broken behaviour so it cannot be quietly forgotten. **Invert that test when the feature lands.**

</details>

Also fixed on the way here: the stockpile target was a flat number tuned for Phase 0's single villager, which left a four-person household permanently one bad season from empty. It now scales per member. And children eat a smaller portion than adults (`child_food_share_percent`), because at full adult rations two workers cannot feed a household of four.

---

## 9. Resolved (Joe, 2026-07-25)

1. **Starting population:** **~4 adults, growing outward.** Two couples founding a settlement. Growth is watched carefully against legibility — a village that doubles every decade outruns readability fast, and readability is this phase's deliverable.
2. **Food:** **per-household stores with a visible sharing policy.** One household can starve while another thrives; the player can see it and act on it. Inequality you can see is a story, inequality you cannot is just cruelty.
   - **Forward-looking:** distribution eventually becomes *diegetic* rather than a policy slider — a **manned market or food stall** that redistributes food and goods evenly within its catchment. That is the right long-term shape because it makes distribution a *building someone works at*, which is exactly the §2.2 pattern. Recorded as D14; **not built in Phase 1.** The Phase 1 sharing policy is the placeholder it eventually replaces.
3. **Job kinds:** exactly **two — forager and woodcutter.** The minimum that makes "which job" a real decision, and small enough to debug.
4. **Labour policy:** **fully automatic** in Phase 1. Levers get added once we can see what actually needs steering.

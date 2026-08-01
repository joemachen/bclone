# Spec: Village Labour Allocation

> Status: **✅ implemented.** Note the cadence in §8.1 is stale: it documents a yearly reshuffle, and the shipped value has been **three years** since D46. · Owner: Joe + Claude Code
> Format per `METHODOLOGY.md §2`. Written *after* three failed improvised attempts (see §3) — that is the reason it exists.
> **Updated after implementation.** Four things in the original draft turned out to be wrong when measured; each is marked **[revised]** below with what happened. The spec is a living document (`METHODOLOGY.md §2`), and the point of writing it first was to make these findings legible rather than to be right the first time.

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

Derived, not tuned — the same discipline as `VillageEconomy` (decision D16).

Foragers first: **a village short of hands feeds itself before it builds.** That priority is the whole policy, and it is one sentence, which is the test of whether it is legible.

**[revised]** The draft wrote it as:

```
foragersNeeded    = ceil(mouths / VillageEconomy.RequiredDependants)   // a CEILING on foragers
woodcuttersWanted = remaining able hands, capped by total stand capacity
```

Both lines were wrong, in opposite directions, and both were caught by running the village rather than by reading the code.

**The forager line was a ceiling; it needed to be a floor.** A ceiling leaves able adults idle, and food is stored *per household* (D14) — so an idle adult is not a spare resource, they are a household producing nothing and living on its neighbours' charity. That is attempt #2 in the table above: "mathematically sufficient and still fatal". So the number is now the **minimum** staffed before anyone is spared for timber, and everyone left over forages.

**The woodcutter line was wildly over-provisioned.** "Every hand food does not need" put *two of four* founding adults on the tree stand — and both of them were the whole of household one, which had no food stored and no forager in it. One woodcutter produces enough timber for several houses a year; wood is simply much cheaper than food, and the quota had no way of knowing that until it was asked **what the wood is for**. So timber is now derived the same way foraging is — from demand:

```
foragersToFeedEveryone = ceil(mouths / VillageEconomy.RequiredDependants)   // a FLOOR
housesWanted           = ceil(couples waiting for a home / 2) + 1           // +1 = keep a woodpile
woodcuttersWanted      = ceil((housesWanted * woodPerHouse - stored) / woodOneCutterBringsPerYear)
                         capped by spare hands and by stand capacity,
                         and zero while the village is short of food AND there is food to gather
foragers               = every remaining hand
```

Three details in that are load-bearing, and each cost a run to find:

- **`+ 1`, the woodpile.** Cutting only what the couples in front of you need turns timber from a job into an errand: a hand goes to the stand at the new year, cuts thirty logs by midspring, and is taken off again. Keeping enough for the *next* home as well means somebody is usually at the stand — and it is what any village that has been through a winter would actually do. Without it the village grew at roughly half the rate.
- **The food gate lifts in winter.** Pulling woodcutters back onto the berries when the larder dips is not caution in winter, it is waste: there is nothing out there to pick, so those hands simply stood idle. Trees do not stop in winter, and this is the rule that lets the village act on it (D17).
- **The food gate is measured village-wide, not per household.** "Is any household below its sharing floor?" is true almost all the time — households dip below and are topped back up every season, by design — so gating on it meant no timber was ever cut, no houses were built, no new households formed, and the village aged out and died *without a single villager starving*. Every death was old age, which is exactly what a village that stops having children looks like.

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

**[revised] One pass per job kind, the kind the village needs fewest of first.** A single global pass over every workplace at once — as written above — has a bug the draft did not anticipate. A village of ten hands wants nine foragers and one woodcutter. Sorted purely by cost, every villager near the tree stand takes an even nearer berry patch first, and the single timber job falls to whoever is left at the end — who is by construction the most remote person in the village, and often cannot reach the stand at all. So the job went unfilled, no timber was cut, and the settlement aged out. It failed hardest exactly when catchment was tight, which is to say exactly where the pillar is supposed to work.

Filling the scarce work first hands it to whoever genuinely lives nearest, and it costs food nothing: the quota has *already* decided that timber only gets hands the village can spare from eating. It is also the more explainable of the two — *"Elias cuts timber because he lives nearest the stand"* beats *"Elias cuts timber because he was the last one left."*

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

All of these live in `tests/Bclone.Sim.Tests/LabourAllocationTests.cs` and are green.

- **Nobody is sent past a nearer opening.** The failure mode of the even-split attempt, asserted directly.
- **Quotas are respected**: the village never spares more hands for timber than it has beyond feeding everyone. **[revised]** — the draft said "total foragers never exceeds what the village needs fed", which stopped being the rule when the forager quota became a floor. The quota's real bite is on the timber side.
- **Local capacity is respected**: no site exceeds its own `Capacity`. **[revised]** — `Workplace.LabourDemand` was renamed `Capacity`, because leaving the old name on a field whose meaning had changed is precisely how §3 happened.
- **Foragers are staffed before woodcutters** when hands are short — and **nobody cuts timber the village has no use for**, which is the same rule from the other end.
- **Catchment still binds**: no assignment outside a workplace's radius.
- **Shedding takes the furthest first** — asserted, since "highest id" is the tempting shortcut.
- **Determinism**: same seed ⇒ identical assignments and identical reason strings, over a full village and 150 years.
- **Every villager can name the constraint that excluded them** — plus one test per constraint (catchment, capacity), so the three sentences cannot silently collapse into one.
- **A reshuffle run twice in a row changes nothing** — D20 requires the pass be re-runnable from scratch; a from-scratch run that did not reproduce itself would churn jobs for no reason.
- **A job change says what it changed from**, and the village narrates the reshuffle it just did.
- **Every home the village will build has a forage site within reach** — a layout guard asserted against the map rather than a run, so moving a site fails the build instead of failing the village a century later.
- **The village survives with catchment genuinely binding** — the thing none of the three previous attempts achieved, and the real acceptance test.

## 8. Definition of Done

Standard DoD (`METHODOLOGY.md §3`), plus: **`forager_catchment_tiles` can be lowered to a value that visibly binds — outlying households restricted to their nearest site — and the village still sustains itself for 150 years.**

**Met.** Shipped at **10 tiles, down from 12**, and no home reaches every workplace at that radius. Measured over 150 years from the founding four: **24 alive in 34 households**.

Getting there needed one thing the draft did not foresee. The original forage-site layout put every extra site out at the edges of the map, which left every home near the middle of the village competing for the one original berry patch — so tightening catchment did not restrict outlying households, it left *central* ones idle beside a full patch, and they starved. The sites are now a ring at roughly the width of the settlement plus two further out. That is D19's argument applied one level down: it is not enough to have several food sources, they have to be *spread the way the homes are*.

**What is still fragile, and it is not the allocator.** Below ten tiles the village survives but one or two homes on the placement spiral end up with nothing in reach at all, and those households are doomed from the day they are built. The fix is not a labour rule — it is that homes are currently placed on a fixed spiral with no knowledge of where the work is. Seeded map generation (D18) is where that gets solved properly, and the layout guard test above is what will catch it if it regresses first.

---

## 9. Open questions (for Joe)

1. **Resolved (Joe, 2026-07-26): no forced forager per household.** Instead, the *Banished* pattern — **the village reshuffles periodically, drifting workers toward jobs near where they live.**

   This is better than the floor I proposed, for a reason worth stating: a hard "one forager per house" rule is a constraint the player would have to be told about, whereas a reshuffle is a *behaviour they can watch happen*. It also handles the case the floor does not — a household whose forager dies, or who moves house, gets corrected by the next reshuffle rather than needing a special rule.

   **Implication for the design:** the allocator must be **re-runnable from scratch**, not incremental. Each reshuffle discards existing assignments and re-runs the matching pass, so improved proximity is found naturally rather than requiring anyone to notice it. Cost-first ordering (§4b) already does the work — a villager who moved closer to a site will out-rank a distant incumbent on the next pass.

   **Two things to get right:**
   - **Reshuffle cadence.** Every season is probably too often (jobs would churn and the reason strings would go stale); once a year is likely right, and it should be a config value.
   - **Churn must be legible.** A villager whose job changes needs a reason saying so — *"moved to the western thicket, 2 tiles from home, closer than the berry patch at 6"* — or the player sees people inexplicably swapping jobs. **A reshuffle that cannot explain itself is worse than no reshuffle.**

   **Built as specified**, with one addition the draft did not call for: **a seasonal pass that only fills vacancies.** The annual reshuffle is what moves people *closer*; the seasonal pass is what stops someone sitting idle beside an opening in the meantime. It is needed because food is stored per household — a child coming of age, a forager dying, or a couple building a house all leave a household with nobody working, and a household cannot wait until next spring. It never moves anyone who already has a job, so the reason they were given for holding it stays true until the next reshuffle.

   Cadence is `labour_reshuffle_years`, default 1.
2. **Should distance to work be visible on the map as a line?** Cheap, and would make a misallocation obvious at a glance rather than requiring a click. *(Recommendation: yes, for the selected villager only — a line from home to work. Every villager would be spaghetti.)*

   **Still open — Joe's call.** Not built. The reason string on the selected villager already names the place and the distance, so this is a legibility upgrade rather than a gap.

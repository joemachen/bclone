# Spec: Wood as Fuel — logs, firewood, and the first processing chain

> Status: **agreed — Joe's answers folded in 2026-07-26** · Owner: Joe + Claude Code
> Format per `METHODOLOGY.md §2`. Implements decision **D17**, extended by **D29**.

---

## 1. Goal

Give wood a second job, so that **"forage or cut timber?" is a genuinely contested decision** rather than a cosmetic one.

Right now it is barely contested. Houses are wood's only consumer, so a village with a full woodpile correctly staffs nobody at the tree stand — the measured symptom recorded against D17. Timber is an errand the village runs a few times a generation, not a livelihood anyone holds, and §2.2's "trees do not stop in winter" advantage has almost nothing to bite on.

Joe's shape for it introduces **two resources and a conversion between them**:

```
tree stand ──logger──▶  LOGS  ──woodcutter──▶  FIREWOOD  ──▶ burned in winter
                          │                        │
                          └──▶ buildings           └──▶ shared between households,
                                                        later sold at the trading post
                                                        and distributed by the market
```

**This is the project's first secondary processing chain**, and that matters more than the fuel does. `DESIGN.md §2.2` calls processing structural rather than content — *"processing is where the tech tree (§2.7) attaches to daily life"* — and every workplace so far has been a pure producer. A workplace that **consumes an input to make an output** is a new thing, and getting its shape right here is the point of doing it on something as simple as firewood.

---

## 2. Which pillars / non-negotiables this serves

- **§2.3 Systemic escalating pressure** — the main one. Fuel scales with *how many houses the player let the village build*, so the pressure traces back to their own choices rather than to a dice roll. A village that sprawls pays for it every winter.
- **§2.5 Environment with teeth** — winter currently removes an income (foraging stops). With fuel it also charges a bill, which is the difference between a season that is inconvenient and one that is dangerous.
- **§2.2 Smart labour** — a second standing demand for the allocator to trade against food all year, *and* the first workplace whose demand depends on another workplace's output.
- **§2.1 / §2.7** — the processing step is where skill and "unlock by doing" will eventually attach.
- **Non-negotiable 1: Legibility.** The hard constraint, below.

---

## 3. The deliberate reversal, and the constraint it carries

Phase 0 explicitly ruled this out (`specs/phase-0-vertical-slice.md §3`):

> *Warmth/cold as a separate stat → deferred. Winter's danger in Phase 0 is **food scarcity only**, one survival axis. Do not add a second overlapping death system.*

That was right then and is being reversed on purpose. What changed: there are **households** to heat (a lone villager heating a lone hut is just a second hunger bar), and there is a **labour system** for fuel to compete inside. The reasoning that has *not* expired is the legibility half:

> **A death must never be ambiguous between cold and hunger. The log has to name which one killed someone.**

That is the constraint this spec is built around, and it is the thing most likely to go wrong. Two overlapping death systems is exactly how a survival game becomes unreadable — the player sees people dying, cannot tell why, and stops being able to act.

**The rule:** cause of death is decided by *which counter crossed its threshold*, never by which system happened to run first in the tick order. Where both are advanced at the moment of death, the death line names the one that killed them **and** reports the other, so the player is never left inferring:

> *Bess died of cold — Winter, Year 12, aged 38. The Cooper household burned its last firewood six days ago. She was hungry too, but not starving (60%).*

---

## 4. The resource model

| | Produced by | Stored | Spent on |
|---|---|---|---|
| **Logs** | a **logger** at a tree stand | per household, drawn village-wide | buildings; the woodcutter's input |
| **Firewood** | a **woodcutter** at a woodcutter's hut, from logs | per household, **shared like food** | burned each winter tick |

**Why logs are drawn village-wide but firewood is held per household.** Building already draws timber from the whole village (D25) — a house is raised communally, and pooling was the fix for logs piling up where they could not be spent. Firewood is different: it is *consumed at home*, so it has to be held at home, or "this family froze while their neighbour was warm" becomes unexpressible. That asymmetry is deliberate and mirrors food (D14).

**Firewood is shared between households** (Joe's call), on the same seasonal cadence and the same keep-your-own-floor rule as food. Cold is not made harsher than hunger; it is made *parallel* to it, which keeps one mental model rather than two.

### Naming

The person at the tree stand becomes a **logger**; the **woodcutter** is the one who makes firewood, per Joe. That follows *Banished* and it is worth flagging that it cuts against everyday usage — colloquially a "woodcutter" fells trees. Renaming the existing `JobKind.Woodcutter` to `Logger` and giving the new job the `Woodcutter` name is the change; the alternative is confusion every time either word is used in the log.

---

## 5. The conversion workplace

The genuinely new mechanic. A woodcutter's hut differs from every workplace so far in that **it can be idle for want of an input**, not just for want of a worker.

- It consumes `logs_per_firewood` logs and produces `firewood_per_batch`, taking `convert_ticks`.
- Logs are drawn **village-wide**, in household-id order, exactly as building already does.
- **A woodcutter with no logs to cut must say so.** This is a new refusal reason and it belongs with the others: a villager standing idle at a hut is only legible if the game says *"no logs to work — the village has none felled"*. Without it the player sees a manned building doing nothing.
- Which in turn means the **labour quota becomes two-stage**: loggers are wanted because woodcutters need logs, and woodcutters are wanted because homes need heating. Demand propagates back down the chain. That is the shape every future processing chain will use, so it is worth building deliberately rather than special-casing firewood.

---

## 6. Fuel

### 6a. Consumption

**Per household, at a flat rate, in winter only.**

- **Per household, not per member.** A house costs the same to heat whether two people live in it or five. This is the interesting choice: it makes *sprawl* expensive rather than *population* expensive, so a village that spreads into many small homes pays more than one that stays dense — a pressure that traces directly back to a player decision (§2.3). It also sharpens the widowed-parent case the food economy already centres on: the same heating bill, half the hands.
- **Winter only**, for legibility. Fuel that trickles all year is a background tax; fuel demanded exactly when foraging stops is a season with teeth. Config-driven so shoulder seasons can be switched on later without a redesign.

### 6b. Freezing

Mirrors starvation, deliberately — the shape is already proven and already legible:

| Hunger | Cold |
|---|---|
| `Hunger` rises each tick | `TicksCold` rises each winter tick the household cannot pay |
| resets on eating | resets when the household has firewood again |
| `TicksAtMaxHunger` ≥ `starvation_ticks` → death | `TicksCold` ≥ `freezing_ticks` → death |
| `CauseOfDeath.Starvation` | `CauseOfDeath.Cold` |

**Warned, not surprised.** The village log announces a household burning its last firewood *when it happens*, not when someone dies of it — the same principle §2.7 states for knowledge at risk: a foreseeable loss must be visible and actionable, or it reads as unfair.

### 6c. Demand must be derived, not tuned

**This is the structural half of the spec, and skipping it would repeat a saga.** `VillageEconomy` exists because the food economy was tuned by iteration across two sittings and still boom-busted (D16). Fuel is the same class of problem and gets the same treatment: state the target, derive the numbers, assert them in tests.

> **A village must be able to fell and cut enough firewood to heat every home through winter *and* raise the buildings it needs, while still feeding itself.**

From which: firewood burned per household per winter → village winter firewood need → woodcutters required → logs required → loggers required. Two derived quotas rather than one, chained. `cut_yield`, `convert_ticks` and the hut's capacity become derived values asserted by tests, exactly as `gather_yield` is.

**Predicted cleanup:** the `+1` woodpile reserve in `WoodcuttersWanted` was added because timber demand was lumpy, and D17 already flags it for re-examination once demand is continuous. Winter fuel makes it continuous. Expect the reserve to shrink or disappear; if it does not, that is a finding worth recording rather than a value to keep.

---

## 7. Deferred, deliberately

- **Tools** → until there is a workshop to make them at. *(Joe's call, confirming the recommendation.)* A tool materialising out of a household's woodpile according to a policy is the same abstraction as the food-stall slider D14 exists to replace and the worker-slot §2.2 exists to delete — a third costume on a pattern this project keeps removing. When they land, they hang off the **villager**, not the household, so §2.1 can later say "a skilled worker with a good tool" without a migration.
- **Firewood at the trading post** (§2.4) → whenever trade lands. Recorded now because it is a reason to keep firewood a first-class resource rather than a household counter.
- **Firewood distributed by the market** (D14) → with the market. The seasonal sharing policy in this spec is the *same placeholder* the food sharing is, and should be deleted by the same building.

---

## 8. Failure modes to design against

- **Ambiguous death.** Named above; the whole of §3.
- **Double jeopardy.** Phase 0's actual fear: two overlapping death systems make the village unsurvivable. Guarded by deriving fuel demand rather than guessing it, and by an acceptance test that the village still lives 150 years with fuel switched on.
- **The chain starves in the middle.** Loggers staffed, woodcutters not — or the reverse — and the village freezes with a yard full of logs. This is the failure mode unique to processing, and the two-stage quota exists to prevent it.
- **Timber crowds out food.** If fuel demand is large, the allocator sends everyone to the woods and the village starves with a full woodpile. The quota's existing "feed everyone before anyone builds" floor should prevent this, but it has never been tested against a *standing* demand, only a lumpy one.
- **A sprawl death spiral.** More houses → more fuel → fewer foragers → starvation → fewer hands. Traceable to the player's choices, which makes it good pressure rather than bad — but it must be *survivable if they respond*, not fatal by the time it is visible.
- **Winter becomes the only story.** If both death systems fire every winter, every villager's life ends the same way and the generational arc flattens — the failure D12 was written to avoid.

---

## 9. Testing

- **The village survives 150 years with fuel on** — the acceptance test.
- **Both quotas are derived**, asserted against the shipped config the way `gather_yield` is.
- **A household that runs out of firewood in winter actually loses people** — anti-vacuity, per D7: a pressure system that cannot kill is decoration.
- **Every cold death is recorded as `CauseOfDeath.Cold`**, and no death is ever left `None`.
- **A death advanced on both counters still names one cause**, and reports the other.
- **The log names the household running out of firewood**, at the time it runs out.
- **Firewood is consumed in winter and not otherwise.**
- **Firewood is shared** between households on the same terms as food.
- **A woodcutter with no logs states that as their reason**, rather than standing silently idle.
- **The chain does not starve in the middle** — loggers and woodcutters are both staffed when both are needed.
- **Foragers are never starved out by timber demand** — the quota floor holds against a standing demand.
- **Determinism** — same seed, identical deaths and identical death lines, over a full village and 150 years.
- **A clean 150-year playthrough still logs no warnings or errors** (`CleanPlaythroughTests`).

## 10. Definition of Done

Standard DoD (`METHODOLOGY.md §3`), plus:

> **Fuel is on by default, the village survives 150 years with it on, and a death by cold reads clearly enough in the log that the player can say what went wrong and what they should have done.** Until a cold death is as legible as a starvation death, this has made the game less readable, not more.

---

## 11. Sequencing note

This is materially larger than the fuel-only draft it replaces: a resource split, a new job kind, the first consuming workplace, a two-stage quota, and a sharing policy. Building it in one commit would be a giant diff of exactly the kind `METHODOLOGY.md §7` warns against, so it lands in slices, each green before the next:

1. **Split the resource.** `Wood` → `Logs` + `Firewood` in the stockpile, hash, and building costs. No behaviour change; firewood simply always zero.
2. **The chain.** `JobKind.Logger` at the tree stand, `JobKind.Woodcutter` at a hut converting logs → firewood, with the no-input refusal reason.
3. **The two-stage quota**, derived, with the economy assertions.
4. **Burning and freezing**, with the cause-of-death rule and the warning line.
5. **Sharing**, on the food cadence.

The acceptance test only becomes meaningful at (4); until then the fuel is made and never spent.

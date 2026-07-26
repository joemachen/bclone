# Spec: Wood as Fuel, and as Tools

> Status: **draft — awaiting Joe's review before implementation** · Owner: Joe + Claude Code
> Format per `METHODOLOGY.md §2`. Implements decision **D17**.

---

## 1. Goal

Give wood two more jobs, so that **"forage or cut timber?" is a genuinely contested decision** rather than a cosmetic one.

Right now it is barely contested at all. Houses are wood's only consumer, so a village with a full woodpile correctly staffs nobody at the tree stand — the measured symptom recorded against D17. Timber is an errand the village runs a few times a generation, not a livelihood anyone holds, and §2.2's "trees do not stop in winter" advantage has almost nothing to bite on.

Two new consumers:

1. **Winter fuel.** A home has to be heated. A household that runs out of firewood in winter loses people.
2. **Tools.** A tool raises what a day's work brings home, wears out, and has to be replaced.

---

## 2. Which pillars / non-negotiables this serves

- **§2.3 Systemic escalating pressure** — the main one. Fuel scales with *how many houses the player let the village build*, so the pressure is traceable to their own choices rather than to a dice roll. A village that sprawls pays for it every winter.
- **§2.5 Environment with teeth** — winter currently removes an income (foraging stops). With fuel it also charges a bill, which is the difference between a season that is inconvenient and one that is dangerous.
- **§2.2 Smart labour** — gives the labour allocator a second standing demand to trade against food, all year round.
- **§2.1 Villagers as agents** — tools are where skill will eventually attach (a better worker with a better tool), so the data model should not make that awkward.
- **Non-negotiable 1: Legibility.** The hard constraint, below.

---

## 3. The deliberate reversal, and the constraint it carries

Phase 0 explicitly ruled this out (`specs/phase-0-vertical-slice.md §3`):

> *Warmth/cold as a separate stat → deferred. Winter's danger in Phase 0 is **food scarcity only**, one survival axis. Do not add a second overlapping death system.*

That was right then and is being reversed on purpose. What changed: there are **households** to heat (a lone villager heating a lone hut is just a second hunger bar), and there is a **labour system** for fuel to compete inside. The reasoning that has *not* expired is the legibility half:

> **A death must never be ambiguous between cold and hunger. The log has to name which one killed someone.**

That is the constraint this spec is built around, and it is the thing most likely to go wrong. Two overlapping death systems is exactly how a survival game becomes unreadable — the player sees people dying, cannot tell why, and stops being able to act.

**The rule:** cause of death is decided by *which counter crossed its threshold*, never by which system happened to run first in the tick order. Where both are advanced at the moment of death, the death line names the one that killed them **and** reports the other, so the player is never left inferring:

> *Bess died of cold — Winter, Year 12, aged 38. The Cooper household burned its last log six days ago. She was hungry too, but not starving (60%).*

---

## 4. Fuel

### 4a. Consumption

**Per household, at a flat rate, in winter only.**

- **Per household, not per member.** A house costs the same to heat whether two people live in it or five. This is the interesting choice: it makes *sprawl* expensive rather than *population* expensive, so a village that spreads into many small homes pays more than one that stays dense — a pressure that traces directly back to a player decision (§2.3). It also sharpens the widowed-parent case the food economy already centres on: the same heating bill, half the hands.
- **Winter only**, for legibility. Fuel that trickles all year is a background tax; fuel that is demanded exactly when foraging stops is a season with teeth. Config-driven so shoulder seasons can be switched on later without a redesign.

### 4b. Freezing

Mirrors starvation, deliberately — the shape is already proven and already legible:

| Hunger | Cold |
|---|---|
| `Hunger` rises each tick | `TicksCold` rises each winter tick the household cannot pay |
| resets on eating | resets when the household has fuel again |
| `TicksAtMaxHunger` ≥ `starvation_ticks` → death | `TicksCold` ≥ `freezing_ticks` → death |
| `CauseOfDeath.Starvation` | `CauseOfDeath.Cold` |

**Warned, not surprised.** The village log announces a household burning its last log *when it happens*, not when someone dies of it — the same principle §2.7 states for knowledge at risk: a foreseeable loss must be visible and actionable, or it reads as unfair.

### 4c. Demand must be derived, not tuned

**This is the structural half of the spec, and skipping it would repeat a saga.** `VillageEconomy` exists because the food economy was tuned by iteration across two sittings and still boom-busted (D16). Fuel is the same class of problem and gets the same treatment: state the target, derive the numbers, assert them in tests.

The stated target:

> **A village must be able to cut enough wood to heat every home through winter *and* build the homes it needs, while still feeding itself.**

From which: wood burned per household per winter, village-wide winter fuel need, and therefore the woodcutters required — which feeds `LabourQuota.WoodcuttersWanted` as a second term alongside houses. `cut_yield` and `tree_stand_capacity` become derived values asserted by tests, exactly as `gather_yield` is.

**Predicted cleanup:** the `+1` woodpile reserve in `WoodcuttersWanted` was added because timber demand was lumpy, and D17 already flags it to be re-examined once demand is continuous. Winter fuel makes it continuous. Expect the reserve to shrink or disappear; if it does not, that is a finding worth recording rather than a value to keep.

---

## 5. Tools — and a question about where they come from

A tool raises what a trip brings home, costs wood, and wears out. Mechanically that is small.

**The design problem is who makes them.** A tool that materialises out of a household's woodpile according to a policy is precisely the pattern this project keeps deleting — it is the food-stall argument (D14) and the no-manual-assignment argument (§2.2) in a third costume. The diegetic version is a **workshop someone works at**, which is D19's territory.

So this spec's recommendation is to **build fuel now and hold tools until there is somewhere to make them**, rather than ship a policy we already know we will delete. Raised as open question 2 rather than decided.

If tools do land, the data model should hang them off the **villager**, not the household, so §2.1 can later say "a skilled worker with a good tool" without a migration.

---

## 6. Failure modes to design against

- **Ambiguous death.** Named above; the whole of §3.
- **Double jeopardy.** Phase 0's actual fear: two overlapping death systems make the village unsurvivable. Guarded by deriving fuel demand rather than guessing it, and by an acceptance test that the village still lives 150 years with fuel switched on.
- **Timber crowds out food.** If fuel demand is large, the allocator sends everyone to the tree stand and the village starves with a full woodpile. The quota's existing "feed everyone before anyone builds" floor should already prevent this — but it has never been tested against a *standing* timber demand, only a lumpy one.
- **A sprawl death spiral.** More houses → more fuel → fewer foragers → starvation → fewer hands. Traceable to the player's choices, which makes it good pressure rather than bad — but it must be *survivable if they respond*, not fatal by the time it is visible.
- **Winter becomes the only story.** If both death systems fire every winter, every villager's life ends the same way and the generational arc flattens — the failure D12 was written to avoid.

---

## 7. Testing

- **The village survives 150 years with fuel on** — the acceptance test.
- **Fuel demand is derived**, asserted against the shipped config the way `gather_yield` is.
- **A household that runs out of firewood in winter actually loses people** — anti-vacuity, per D7: a pressure system that cannot kill is decoration.
- **Every cold death is recorded as `CauseOfDeath.Cold`** and no death is ever left `None`.
- **The log names the household running out of fuel**, at the time it runs out.
- **Fuel is consumed in winter and not otherwise.**
- **Foragers are never starved out by timber demand** — the quota floor holds against a standing demand.
- **Determinism** — same seed, identical deaths and identical death lines, over a full village and 150 years.
- **A clean 150-year playthrough still logs no warnings or errors** (`CleanPlaythroughTests` already covers this; fuel must not break it).

## 8. Definition of Done

Standard DoD (`METHODOLOGY.md §3`), plus:

> **Fuel is on by default, the village survives 150 years with it on, and a death by cold reads clearly enough in the log that the player can say what went wrong and what they should have done.** Until a cold death is as legible as a starvation death, this has made the game less readable, not more.

---

## 9. Open questions (for Joe)

1. **Fuel per household, or per person?** *(Recommendation: per household.* A house costs the same to heat whether two or five live in it, which makes sprawl the thing that costs — a pressure that traces back to a player decision, rather than one that just punishes population.*)*
2. **Tools now, or when there is a workshop to make them at?** *(Recommendation: wait.* Tools appearing from a household's woodpile by policy is the same abstraction as the food-stall slider D14 exists to replace. Fuel alone already fixes the measured problem — timber having nothing to do. Tools would be a second system landing in the same slice, and the project's own record on two-systems-at-once is poor.*)*
3. **Winter only, or shoulder seasons too?** *(Recommendation: winter only for now, config-driven.* Fuel demanded exactly when foraging stops is a season with teeth; fuel trickling all year is a background tax nobody notices.*)*
4. **Should a cold death be survivable by moving in with neighbours?** A household with no fuel next door to one with plenty is the same shape as the food-sharing placeholder (D14) — and the same eventual answer, presumably. Worth deciding whether firewood shares between households in Phase 2, or whether cold is deliberately *harsher* than hunger because that is what makes it a distinct pressure. *(No recommendation — this is a feel question, and it is the one that decides whether fuel is a real threat or a second hunger bar.)*

# Spec: Storage and Distribution — goods live in buildings

> Status: **agreed — Joe's answers folded in 2026-07-27** · Owner: Joe + Claude Code
> Format per `METHODOLOGY.md §2`. Implements decisions **D30** and **D32**; delivers the building half of **D14**.

**Settled by Joe:** refilling a larder is a **fetch** (§3), and food gets its own building — a **granary** — separate from the shed that holds manufacturing materials (§4).

---

## 1. Goal

Move goods out of households and into **buildings**: a small buffer at the workshop that made them, a general storage shed for bulk materials, a marketplace near the homes, and a working larder in each home.

Three things make this structural rather than tidying:

1. **It deletes the two least honest pieces of code in the sim.** Food shares between households seasonally and firewood daily, both by a rule the world enforces from nowhere. D14 named that a placeholder the day it was written. This is the building that replaces it.
2. **It fixes by design a bug found twice.** Logs piled up in the logger's house where nobody could spend them and no home was ever built (D25). Firewood piled up in the woodcutter's house and the other founding household froze beside it (D29). Both were patched locally. A shed is the answer to both, and to the third one nobody has hit yet.
3. **It gives desire paths something to be about.** §2.6 needs traffic that means something before trample values can. Hauling between a stand, a shed and a market *is* that traffic — the daily housing↔granary churn the pillar explicitly asks for, rather than a lone forager scarring the map.

---

## 2. Which pillars / non-negotiables this serves

- **§2.2 Smart labour** — D14's core claim: *"distribution is a job, not a slider. A market or food stall is manned like any other workplace."* This is where that stops being a promise.
- **§2.6 Desire-path roads** — the traffic generator. Roads cannot emerge from a village where goods teleport.
- **§2.3 Systemic pressure** — storage capacity is a real constraint that the player's own layout decisions make better or worse.
- **Non-negotiable 1: Legibility.** A player must be able to point at a full shed beside a hungry family and understand why. Which is the hard part, below.

---

## 3. The hard problem: eating must not require a journey

Phase 0 settled this and it constrains everything here (**D10**):

> *A round trip to the food source is longer than the gap between meals, so finish-your-action-first made the villager starve mid-gather beside a full store. A survival game may kill you for bad decisions, never for a scheduling artifact.*

So **a villager must always be able to eat where they are standing at home, instantly.** Any design where meals are taken from a shed across the village re-creates that failure. Homes therefore keep a **working larder**, and the question is only how it gets refilled.

Two ways to refill it, and this is the main open question:

| | How | Cost |
|---|---|---|
| **Fetch** *(recommended)* | A household whose larder is low sends someone to the nearest store holding what they need. The market exists to make that trip short. | A trip, by the household that wants the goods. Degrades gracefully: no market simply means a longer walk. |
| **Deliver** | A manned market or hauler carries goods out to homes in its catchment. | A job. But an **unmanned market means nobody eats** — a cliff, not a gradient, and one the founding village falls off immediately. |

**Recommendation: fetch.** It is what *Banished* does, it keeps the market *valuable* rather than *mandatory*, and it never produces a village that starves because one job went unstaffed. The market still earns its keep — it is the difference between a two-tile errand and a twelve-tile one, which at §2.6's shared cost field is exactly the sort of thing worth paving a road to.

---

## 4. The data model

A `Store` becomes a thing a **building** has, rather than a thing a household has.

```
Store
    Capacity   : int                 // per store; what makes storage a constraint
    Food, Logs, Firewood, ...        // the same goods Stockpile already tracks

Building            (new — a workplace, a home, a shed and a market are all one)
    Id, Name, Position
    Kind        : Home | ForageSite | TreeStand | WoodcutterHut | StorageShed | Market
    Store       : Store
    Accepts     : which goods it will hold
```

`Workplace` and `Household` both become buildings with stores. That is a real refactor and it is the point: **the sim currently has no concept of a place that holds things**, which is why every goods bug so far has been "the right stuff in the wrong house".

**Who holds what:**

| Building | Holds | Why |
|---|---|---|
| Home | a working larder: food, firewood | D10 — meals and the hearth must be instant |
| Woodcutter's hut | a little firewood | Joe's call: a buffer at the place of production, not the whole stock |
| Tree stand | a few logs | same shape; the felled pile beside the stumps |
| **Granary** | food, and only food | Joe's call (D32) |
| **Storage shed** | materials — logs, firewood, stone, lumber, cloth | the general store, and the overflow every producer runs to |
| Market | food and firewood, near the homes | shortens the fetch; D14's building |

### Why food gets its own building

Joe's call, and it resolves a tension this spec was going to have to face (§11.3 as drafted). One undifferentiated pile would have quietly deleted the inequality D14 exists to create — *"one family starving beside a thriving neighbour"* is the story per-household food was introduced for, and a single village-wide store makes it unexpressible.

Two buildings keeps it, and **changes what inequality is made of, for the better.** It stops being about whose larder it is — an accident of which house a forager was born in — and becomes about **distance and hands**: a household far from the granary, or one with nobody spare to send, eats worse than its neighbours. That is spatial, watchable, and it ties straight into catchment (§2.2) and desire paths (§2.6) rather than sitting off to one side. A story about a family on the wrong end of the valley is a better story than one about a family with the wrong surname.

It is also the honest division. A granary and a woodpile are different buildings in every village that ever existed, for the obvious reason: food spoils, rots, and gets eaten by things, and timber does not.

**Capacity is what makes this a system rather than bookkeeping.** A full shed means a producer has somewhere to stop, which is a pressure the player answers by building another one — and it is the first thing in the game that a *placement* decision could improve, which matters for when placement lands.

---

## 5. How goods move

Every movement is a **trip somebody makes**. There is no teleporting, and no policy that moves goods from nowhere.

- **Producing.** A forager, logger or woodcutter finishes a batch and carries it to the nearest store that will take it — their own workplace buffer first, then the shed, then the market. Today they carry it home, which is the bug.
- **Fetching.** A household below its larder target sends an idle member — or a member on their way home — to the nearest store holding what they need. Home, then market, then shed, in ascending travel cost.
- **Stocking the market.** The market's worker moves goods from the shed to the market. That is the whole job, and it is what makes the market a *workplace* rather than a rule.

**All three read the same travel-cost field** (§2.6), so a worn path shortens all of them at once and no system needs to know roads exist.

---

## 6. What this deletes

- `HouseholdSystem.ShareFood` — the seasonal policy.
- `HearthSystem.ShareFirewood` — the daily policy.
- `SimWorld.TryTakeLogsFromTheVillage` — the "draw logs from the whole village" special case, which is a shed in disguise.
- `HouseholdSystem.TryTakeBuildingTimber`'s village-wide sweep (D25) — likewise.

Four workarounds, all of which exist because there was nowhere to put things. **If the implementation does not delete all four, it has not replaced the placeholder, it has joined it.**

---

## 7. Failure modes to design against

- **Starving beside a full shed.** The legibility disaster. A villager who cannot get food must say which of the three — no store in reach, no stock in it, or nobody to fetch — is stopping them, exactly as the labour refusals do.
- **Re-creating D10.** Any path where a meal requires a journey is wrong, however elegant.
- **The unmanned-market cliff.** Why fetch is recommended over deliver.
- **Hauling eating the labour budget.** Fetching is unpaid work that competes with foraging for the same hours. The food economy is derived from trips per year (`VillageEconomy`), and adding a fetch leg to the round trip changes that derivation. **This must be re-derived, not patched** — it is exactly the D16 mistake otherwise.
- **A full shed silently stopping production.** A logger who cannot deposit must say so, like the woodcutter with no logs (D29).

---

## 8. Testing

- **Nobody ever starves with food in a store within reach** — the acceptance test, and the inverse of the failure that motivates this.
- **A villager who cannot get goods names which constraint stopped them.**
- **Meals are still instant at home** — D10 regression guard, asserted directly.
- **Goods conservation:** total goods in the world only changes by production and consumption, never by movement. The lifetime-counter bug (fuel spec §0.4) is exactly what this catches.
- **Capacity is respected** — no store exceeds its own.
- **The market shortens fetch trips** — a manned market measurably reduces total travel versus none, or it is decoration.
- **Determinism** — same seed, identical stores and identical trips, over 150 years.
- **The village still holds a stable size** (D31), with the food economy **re-derived** for the new round trip.
- **All four deleted workarounds stay deleted** — a reflection or grep test, in the spirit of the no-assignment-API test (D15).

## 9. Definition of Done

Standard DoD (`METHODOLOGY.md §3`), plus:

> **The four placeholder workarounds are gone, the village holds a stable size for 150 years with goods moving only by trips people make, and a player can point at a hungry household and a full shed and get a straight answer about why the two have not met.**

---

## 10. Sequencing

Bigger than the fuel chain, and that one taught the lesson: slices, each green before the next.

1. ~~**`Store` as a thing buildings have.**~~ ✅ Done — households and workplaces both got one; no behaviour change.
2. ~~**The storage shed**, and producers depositing to it.~~ ✅ Done — both village-wide sweeps deleted.
3. ~~**Fetching**, with the larder target and the refusal reasons.~~ ✅ Done — both sharing policies deleted; food economy re-derived.
5. **Capacity** as a binding constraint. ← **taken next, ahead of the market (Joe, 2026-07-27)**
4. **The market**, manned, stocking itself from the shed.

**Why 5 before 4.** The flows are proven, which was the stated precondition, and the open question that matters more is the shape of the population curve (§12). Joe's reading: a flat line means growth stopping at what the buildings support, instead of overshooting them and falling back. Capacity is the only brake in this spec that could do that, so it gets measured first. The market shortens fetch trips, which is valuable but does not regulate anything.

---

## 11. Open questions (for Joe)

1. **Fetch or deliver?** ✅ **Resolved (Joe, 2026-07-27): fetch.** A household below its larder target sends someone to the nearest store holding what they need; the market exists to make that trip short rather than to make it possible. Keeps the market valuable rather than mandatory, and never starves a village because one job went unstaffed.
2. **Where do the shed, granary and market come from?** There is still no building placement — the player has no agency at all — so for now they exist from the founding, like the tree stand. Found the village with a granary and a shed; the market arrives as a later slice. All three become placeable when placement lands, **and that is the moment this system starts paying the player back**: where you put the granary is the first decision in the game that storage makes interesting.
3. **Should one store hold everything?** ✅ **Resolved (Joe, 2026-07-27): no — a granary for food, a shed for materials.** See §4. Better than the middle option this spec was going to propose, because it keeps D14's inequality *and* improves what it is made of.

### Still open, raised by the answers

5. **What is granary capacity derived *from*?** It cannot be picked, per D16. See §12.3 — it is derived from the population a granary can carry through winter, which makes it a **stated population ceiling** rather than a number. That is a real design commitment and it is worth Joe reading it as one: *how big can my village get* becomes *how much granary have you built*.

4. **Does food in a granary spoil?** A granary that keeps food perfectly forever is a bank, and a village with a bank has solved winter permanently — which would undo most of what §2.5 is for. Spoilage is the obvious counterweight and it is also the reason granaries are a *building* rather than a heap. **Not proposed for this spec** — it is a Phase 2 environment question, not a storage one — but it is worth naming now, because if it lands later it changes what the food economy is derived against, and that derivation should not have to be redone twice.

---

## 12. What the population curve actually is (measured 2026-07-27, before slice 5)

Measured rather than guessed, because the standing lesson is that guessing the cause has been wrong every time. Shipped village config, seed as configured, 150 years.

### 12.1 The curve

| Year | 40 | 60 | 80 | 90 | **105** | 120 | 135 | 150 |
|---|---|---|---|---|---|---|---|---|
| Population | 20 | 32 | 54 | 63 | **68** | 64 | 41 | 23 |
| Births / 5y | 3 | 7 | 10 | 10 | **15** | 6 | 3 | 0 |
| Deaths / 5y | 0 | 1 | 2 | 3 | 10 | 7 | 11 | 9 |
| Children alive | 11 | 15 | 22 | 26 | 25 | 17 | 11 | **3** |

**It is a demographic wave, not a resource failure.** At year 150 the village is down to 23 people and is holding **1,247 food in the granary and 1,723 in the homes** — roughly 130 per head against a target of 67. It is dying rich. Deaths peak (17 per 5 years) exactly **20 years after** births peak, and the survivors are old: 20 adults to 3 children.

### 12.2 Why — and it is a control problem, not an economy one

Births are gated on a **threshold**: the granary must hold 80% of `stockpile_target × population`. Measured against that gate, the granary sits between 82% and 105% for almost the whole run and dips below only three times in 150 years (75%, 78%, 73% — years 105, 115, 135).

So the gate works, and it is the thing that stops growth. The problem is its *shape*:

- It is **bang-bang** — fully open or fully shut, nothing in between.
- The village's response to it lags by **~15 years**, the time from a birth to a working adult.
- Lives are **40–50 years**, so a cohort born together dies together.

A threshold controller with a 15-year lag on a 45-year system oscillates. It cannot do anything else. The village grows unchecked for 65 years because the gate is open the whole time, hits the gate at 68, and then the cohort that unchecked growth produced ages out all at once.

**This is a shape the project has already met once.** The commit that stabilised fuel says it outright: *"the fuel quota was a thermostat that switched on after the house was cold. Including the annual burn makes it proportional."* Same bug, different system.

### 12.3 What capacity does about it, and what it does not

Capacity does **not** address the lag, and it is not the cause of the wave. What it does is change *when* the brake engages. `TargetFoodForTheGranary()` grows linearly with population and is unbounded; a granary with a finite capacity `C` can never satisfy it above

> **population ceiling = C ÷ (stockpile_target × birth_food_percent)**

so the brake stops being a gate the village passes through for 65 years and becomes a **ceiling it arrives at and stays under**. That should flatten the curve, and for the reason Joe gave: growth stops at what the buildings support instead of overshooting them first.

**Derivation (D16 — stated target, not a number):** *a granary holds the food its village needs to get through one winter, with the same margin the household stockpile target carries.* Capacity follows from the population served; the ceiling above follows from capacity. Neither is typed in.

**The honest risk, recorded before building it:** this makes granary capacity the village's population ceiling, which is a hard stop and there is no placement yet, so the player cannot answer it by building a second granary. Until placement lands the answer is a config value, which means **the pressure is real but the response to it is not** — the same gap `RequiredWoodcutterSeats` already documents. If measurement shows the ceiling is a cliff rather than a settling point, that is a finding, not a tuning problem.

**If capacity flattens the curve, the wave is still there and still worth fixing** — a proportional birth gate is the real answer, and it belongs with D28's re-derivation rather than here.

# Spec: Storage and Distribution — goods live in buildings

> Status: **✅ complete — all five slices built, D30 closed** · Owner: Joe + Claude Code
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
5. ~~**Capacity** as a binding constraint.~~ ✅ Done — taken ahead of the market on Joe's call. See §13; it flattens the curve, and it flushed out D34.
4. ~~**The market**, manned.~~ ✅ Done — and it does more than this line said: it delivers, and it unsticks stranded goods. See §14.

**All five slices are done. D30 is closed.**

**Why 5 before 4.** The flows are proven, which was the stated precondition, and the open question that matters more is the shape of the population curve (§12). Joe's reading: a flat line means growth stopping at what the buildings support, instead of overshooting them and falling back. Capacity is the only brake in this spec that could do that, so it gets measured first. The market shortens fetch trips, which is valuable but does not regulate anything.

---

## 11. Open questions (for Joe)

1. **Fetch or deliver?** ✅ **Resolved (Joe, 2026-07-27): fetch.** A household below its larder target sends someone to the nearest store holding what they need; the market exists to make that trip short rather than to make it possible. Keeps the market valuable rather than mandatory, and never starves a village because one job went unstaffed.
2. **Where do the shed, granary and market come from?** There is still no building placement — the player has no agency at all — so for now they exist from the founding, like the tree stand. Found the village with a granary and a shed; the market arrives as a later slice. All three become placeable when placement lands, **and that is the moment this system starts paying the player back**: where you put the granary is the first decision in the game that storage makes interesting.
3. **Should one store hold everything?** ✅ **Resolved (Joe, 2026-07-27): no — a granary for food, a shed for materials.** See §4. Better than the middle option this spec was going to propose, because it keeps D14's inequality *and* improves what it is made of.

### Resolved by the answers

5. **What is granary capacity derived *from*?** ✅ **Resolved — see §12.3 and D33.** It cannot be picked, per D16. It is derived from the population a granary can carry through winter, which makes it a **stated population ceiling** rather than a number: *how big can my village get* becomes *how much granary have you built*.

4. **Does food in a granary spoil?** ✅ **Resolved (Joe, 2026-07-27): no. Cut from the plan — see D37.** *"It's not fun."*
   - Spoilage is a tax that arrives as a number going down for no decision the player took. It punishes the well-run town exactly as hard as the badly-run one, makes the granary feel like a leaking bucket rather than an achievement, and adds a chore to a game whose second non-negotiable is *reduce babysitting*. It fails §1.2 and §1.1 together.
   - **The danger it was proposed against is real and is already handled.** A granary that keeps food forever is a bank, and a village with a bank has permanently solved winter. But **capacity (slice 5) bounds the granary**, so there is no unlimited bank available: the village cannot stockpile its way out of winter, because the building will not hold it. The pressure survives and now comes from *how much you have built*, which is the more legible source and the one the player can act on.
   - **Consequence to respect:** granary capacity is now the only thing standing between the village and an infinite winter buffer. It should not be quietly relaxed to fix an unrelated squeeze.

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

---

## 14. The market (slice 4) — Joe's answers, 2026-07-27

The last slice of D30, and the building half of D14. Joe settled the three things this spec had left vague.

### 14.1 What Joe asked for

1. **More than one worker.** The market is a workplace with a real capacity, not a one-person post.
2. **It solves stranded goods.** Firewood in a dead family's home, a larder nobody is left to eat from — the market is what unsticks them. This is the answer to the last thing D34 left behind.
3. **A marketer's trip works in both directions.** *"Take things to a home and pick up food from a granary on the trip back if the distances make sense."*

### 14.2 The rule for "if the distances make sense"

Stated rather than tuned, and it needs no threshold: **a marketer never walks empty-handed.** Every leg is chosen cost-first from wherever they are standing right now — the same principle the labour allocator already uses (D15, D23), reading the same travel-cost field (§2.6).

So the loop is not *market → home → market*. It is:

> pick up what somebody needs → carry it to them → from **there**, pick up whatever most needs moving next → carry that.

"Pick up food from the granary on the way back" falls out of this rather than being a special case: after delivering to a home near the granary, the granary is simply the cheapest next stop. No magic number decides whether a detour is worth it, because there is no detour — there is only the next-cheapest useful leg. That also makes it explainable in one sentence, which is the §2.2 test.

### 14.3 Both directions

A marketer moves goods two ways, and the second is what solves stranded goods:

- **Out:** from a store to a household below its target — the delivery.
- **In:** from a household holding more than it needs, or with nobody left living in it, back to a store.

The "in" direction is the whole of Joe's point 2. A house whose family has died is not a special case in the code; it is simply a household whose need is zero and whose store is not.

### 14.4 What must NOT change

**Fetching stays exactly as it is.** §3 rejected delivery-instead-of-fetch because an unmanned market means nobody eats — *"a cliff, not a gradient, and one the founding village falls off immediately."* That argument still holds. Delivery is **additive**: a market with nobody in it means no deliveries and no unsticking, never a household that cannot eat. The founding village has no marketer and must be entirely unaffected.

This is the acceptance test for the slice, and it is a stronger claim than "the market works": **switch the market off and the village must survive exactly as it does today.**

### 14.5 Shape

- `JobKind.Marketer`, `market_capacity` in config — more than one seat, per Joe.
- The market is **both** a `StoreBuilding` (a third `StoreKind`, accepting food and firewood, near the homes) **and** a `Workplace` at the same position. Those are separate types today; merging them into the spec's §4 single `Building` is the right end state but not this slice's job. Recorded as a known seam.
- Households fetch from the market as well as the granary and shed, nearest-first — which is what makes a stocked market shorten the trip rather than just move it.

### 14.7 What it measured (built 2026-07-27)

| | with a market | without | change |
|---|---|---|---|
| Goods stranded in empty homes (goods-years, 200y) | **1,618** | 81,846 | **−98%** |
| Household fetch-steps (100y) | 24,310 | 25,909 | −6% |

**Joe's second requirement is decisively met**: stranded goods essentially stop existing. The fetch-shortening is real but modest, and honestly so — the market is a couple of tiles from the granary in the shipped layout, so there is not much walk to save. **That number is a placement question, not a market question**, and it is exactly what §11.2 predicted would become interesting when the player can put the building somewhere.

**Two things this got wrong on the way, both recorded because they were expensive:**

1. **Recovering "surplus" from living households wrecked the village.** The obvious generalisation of "collect goods a household does not need" was to take anything above target. But a home is above target every time its forager walks in the door, so marketers stripped families the moment they got ahead and the families fetched it straight back — pure churn, and the granary stopped filling, so the birth gate never opened and the settlement died out at **five people**. Recovery is now *only* from houses with nobody living in them. **A trader moves what nobody is using, not what somebody has just earned.**
2. **A marketer who re-decides mid-walk never arrives.** Legs are chosen cost-first from where the villager is standing (§14.2), so re-planning every tick makes the answer change under them with every step — they shuttled between two sources forever and completed nothing. It showed up as stranded goods getting *worse* with a market than without. A destination has to outlive the walk to it.

### 14.6 How it is tested

- **The village survives with the market switched off**, identically to today (§14.4).
- **A dead family's larder does not stay stranded** — the case D34 left behind.
- **The market shortens fetch trips**: total household travel measurably falls against a run with no marketer, or the building is decoration (spec §8).
- **A marketer never walks an empty leg** — asserted directly, since it is the whole of §14.2.
- **Goods conservation** across every new movement, and **determinism** over 300 years.

### 14.8 ⭐⭐ THE MARKET IS STOCKED, AND UNTIL NOW NOTHING EVER PUT ANYTHING IN IT (Joe, 2026-08-23)

> *"The marketer should fill the market's stores from the granary/storage shed and then
> distribute to houses from there. Once houses are full, or the market's stores are empty of a
> needed good, the marketer should replenish the market from the granary/storage shed."*

**⛔ THE SPEC ALREADY ASSUMED THIS AND NOTHING IMPLEMENTED IT.** §14.5's last bullet says
*"households fetch from the market as well as the granary and shed, nearest-first — **which is
what makes a stocked market shorten the trip rather than just move it**"* — and **the market has
never held stock.** Its store exists, is sized (`market_stock_per_household ×
economy_horizon_households`) and is described in config as *"a short trip, not a second
granary"*; the marketer collects at the granary and walks straight past it to the house.

**⭐ That is D185's shape for the third time: the behaviour existed and the demand did not.** The
market is a valid *source* — `NearestStoreHolding` has always included it — so the moment
anything stocks it, households start fetching from it with no other change at all.

#### The rule

**A fourth errand, offered on cost like the other three (§14.2): *the market is short of a good a
bigger store has.*** No threshold, no detour logic — one more useful thing to carry.

- **Source: the nearest store holding it that is not the market**, so there is no self-loop.
- **Target: the market's own capacity**, which is already derived and already sized as a short
  trip rather than a second store. Nothing new is typed.
- **⚠️ Offered only when no household needs anything** — *"once houses are full"*, Joe's own
  trigger, and it is also the safe reading. **D79's rule is that need outranks convenience**: a
  village must never starve with a full granary and an empty larder, and routing a hungry
  household's delivery through the market would make that household wait for two legs instead of
  one.
- **⚠️ SO THE SECOND HALF OF JOE'S SENTENCE IS DELIBERATELY NOT IMPLEMENTED LITERALLY, AND HE
  SHOULD KNOW IT.** *"…or the market's stores are empty of a needed good"* would send the
  marketer to refill the market **before** serving the house that is waiting. The value is
  almost all in the other direction anyway: **households fetch for themselves constantly (§3),
  and marketer delivery is only the top-up** — so stocking the market in slack time is what
  shortens the walks that actually dominate.

#### ⭐ Why this makes siting a decision, which is the point

**Joe: *"that's the point. The user has to put thought into positioning."*** A market next to the
granary is now pure overhead — two legs where one would do. A market **among the homes** turns
one long marketer trip into many short household fetches. **The building finally has a reason to
be somewhere**, which is the same lesson D194 landed on the farm: *put the granary near the
fields, and the market near the homes.*

#### ⛔ And storage is separate from distribution (D199)

> Joe: *"only the marketer moves items to the market. Ordinary haulers dump in generic storage
> (stockpile, shed, warehouse, etc)… I just don't want it to be a dumping ground — I want to
> separate the actual storage buildings from the market (distribution building)."*

**`StoreForTheLoad`'s kind-blind fallback was making the market the overflow store.** A producer
takes their load to the nearest store of the right kind and, failing that, to *anything that will
take it* — and the market takes food and firewood. Measured over thirty years it sat **600 above**
what the village's homes need, **none of it carried there on purpose**. That is the very thing
`market_stock_per_household`'s own config comment forbids.

**`StoreBuilding.IsStorage` names the distinction on the building** — everything but the market —
so a warehouse is storage the day it exists. **A marketer is unaffected**, which is what keeps
household overflow arriving there: §14.3's *"in"* direction is a trader's leg too. *The rule is
about who is carrying, not about what is carried.*

**Measured after: the worst overfill falls from 600 to 36–40**, which is the household overflow
that belongs there.

#### What must still hold

- **⛔ §14.4 is unchanged and is the acceptance test**: switch the market off and the village
  survives exactly as it does today. Stocking is additive.
- **The market must not become a second granary.** Its capacity is the guard, and it is derived.
- **Goods conservation** across the new leg.

#### ⚠️ And the honest failure mode to measure for

**This adds a leg.** If household travel does not measurably fall, the market has become a
detour and the slice is wrong — which is §8's standing test for the market and the one this must
be held to. *Measure household walking, not marketer walking.*

---

## 13. What actually happened (measured after building it, 2026-07-27)

§12 was written before the code and got half of it right. Recorded honestly, because the half it got wrong is the more useful half.

### 13.1 Right: capacity flattens the curve, and by the predicted mechanism

Population band after the founding decades, over 200 years:

| `granary_feeds_people` | band | spread |
|---|---|---|
| unbounded (pre-slice-5) | 24–86 | 62 |
| 60 | 49–63 | 14 |
| **30 (shipped)** | **24–35** | **11** |

Growth stops at what the buildings support instead of overshooting them. That is exactly Joe's reading, and it is now asserted rather than observed (`CapacityIsWhatHoldsThePopulationFlat`).

### 13.2 Wrong: the wave was not a control problem

§12 diagnosed a bang-bang birth gate with a 15-year lag and concluded that a **proportional** gate was the real fix. That diagnosis was wrong, and it was wrong in an instructive way: it is a plausible mechanism, it predicts the observed shape, and it is the same mechanism that genuinely did break the fuel quota. It was reasoning from a pattern rather than from a measurement.

The measurement, when it was finally taken — per household, per year, *which condition refused this birth* — said something else entirely. **The dead were never removed from their household's member list, and the birth check read the list's length as "how many live here".** A household that had seen seven people pass through it could never have another child. Households ratcheted one way into sterility, and every village died out around year 180 regardless of food, fuel or storage. See D34.

With that one line fixed, the threshold birth gate is fine. No proportional control was needed. The village holds a flat band for 300 years.

### 13.3 The lesson worth keeping

**The 150-year window was the reason this survived two phases.** The collapse completed at about year 180; at year 150 the village was at twenty-three and falling, which reads as the tail of a wave rather than the middle of an extinction. The acceptance test asserted "never dropped below the founding four" and that was *true*, right up until it wasn't.

An assertion about a window is not an assertion about a system. Every long-run test here should be asked what it would do if the run continued.

And the standing rule earned another entry: **measure, do not pattern-match.** §12's story was coherent, cited a real precedent in this codebase, and was wrong. The measurement took twenty minutes.

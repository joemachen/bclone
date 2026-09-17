# Spec: Storage and Distribution — goods live in buildings

> Status: **✅ complete — all five slices built, D30 closed** · ✅ **§14.9, the market as a shop (Joe, 2026-09-13), is BUILT (D372, 2026-09-15) — unplayed** · Owner: Joe + Claude Code
> Format per `METHODOLOGY.md §2`. Implements decisions **D30** and **D32**; delivers the building half of **D14**.

**Settled by Joe:** refilling a larder is a **fetch** (§3), and food gets its own building — a **granary** — separate from the warehouse that holds manufacturing materials (§4).

---

## 1. Goal

Move goods out of households and into **buildings**: a small buffer at the workshop that made them, a general warehouse for bulk materials, a marketplace near the homes, and a working larder in each home.

Three things make this structural rather than tidying:

1. **It deletes the two least honest pieces of code in the sim.** Food shares between households seasonally and firewood daily, both by a rule the world enforces from nowhere. D14 named that a placeholder the day it was written. This is the building that replaces it.
2. **It fixes by design a bug found twice.** Logs piled up in the logger's house where nobody could spend them and no home was ever built (D25). Firewood piled up in the woodcutter's house and the other founding household froze beside it (D29). Both were patched locally. A warehouse is the answer to both, and to the third one nobody has hit yet.
3. **It gives desire paths something to be about.** §2.6 needs traffic that means something before trample values can. Hauling between a stand, a warehouse and a market *is* that traffic — the daily housing↔granary churn the pillar explicitly asks for, rather than a lone forager scarring the map.

---

## 2. Which pillars / non-negotiables this serves

- **§2.2 Smart labour** — D14's core claim: *"distribution is a job, not a slider. A market or food stall is manned like any other workplace."* This is where that stops being a promise.
- **§2.6 Desire-path roads** — the traffic generator. Roads cannot emerge from a village where goods teleport.
- **§2.3 Systemic pressure** — storage capacity is a real constraint that the player's own layout decisions make better or worse.
- **Non-negotiable 1: Legibility.** A player must be able to point at a full warehouse beside a hungry family and understand why. Which is the hard part, below.

---

## 3. The hard problem: eating must not require a journey

Phase 0 settled this and it constrains everything here (**D10**):

> *A round trip to the food source is longer than the gap between meals, so finish-your-action-first made the villager starve mid-gather beside a full store. A survival game may kill you for bad decisions, never for a scheduling artifact.*

So **a villager must always be able to eat where they are standing at home, instantly.** Any design where meals are taken from a warehouse across the village re-creates that failure. Homes therefore keep a **working larder**, and the question is only how it gets refilled.

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

Building            (new — a workplace, a home, a warehouse and a market are all one)
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
| **Storage warehouse** | materials — logs, firewood, stone, lumber, cloth | the general store, and the overflow every producer runs to |
| Market | food and firewood, near the homes | shortens the fetch; D14's building |

### Why food gets its own building

Joe's call, and it resolves a tension this spec was going to have to face (§11.3 as drafted). One undifferentiated pile would have quietly deleted the inequality D14 exists to create — *"one family starving beside a thriving neighbour"* is the story per-household food was introduced for, and a single village-wide store makes it unexpressible.

Two buildings keeps it, and **changes what inequality is made of, for the better.** It stops being about whose larder it is — an accident of which house a forager was born in — and becomes about **distance and hands**: a household far from the granary, or one with nobody spare to send, eats worse than its neighbours. That is spatial, watchable, and it ties straight into catchment (§2.2) and desire paths (§2.6) rather than sitting off to one side. A story about a family on the wrong end of the valley is a better story than one about a family with the wrong surname.

It is also the honest division. A granary and a woodpile are different buildings in every village that ever existed, for the obvious reason: food spoils, rots, and gets eaten by things, and timber does not.

**Capacity is what makes this a system rather than bookkeeping.** A full warehouse means a producer has somewhere to stop, which is a pressure the player answers by building another one — and it is the first thing in the game that a *placement* decision could improve, which matters for when placement lands.

---

## 5. How goods move

Every movement is a **trip somebody makes**. There is no teleporting, and no policy that moves goods from nowhere.

- **Producing.** A forager, logger or woodcutter finishes a batch and carries it to the nearest store that will take it — their own workplace buffer first, then the warehouse, then the market. Today they carry it home, which is the bug. ✅ **As written since D385 (2026-09-16, §14.10):** for eleven months a forager carried the load *home* while their own larder was below target, and the fisher's and hunter's overflow did the same; every load goes to a store now.
- **Fetching.** A household below its larder target sends an idle member — or a member on their way home — to the nearest store holding what they need. Home, then market, then warehouse, in ascending travel cost.
- **Stocking the market.** The market's worker moves goods from the warehouse to the market. That is the whole job, and it is what makes the market a *workplace* rather than a rule.

**All three read the same travel-cost field** (§2.6), so a worn path shortens all of them at once and no system needs to know roads exist.

---

## 6. What this deletes

- `HouseholdSystem.ShareFood` — the seasonal policy.
- `HearthSystem.ShareFirewood` — the daily policy.
- `SimWorld.TryTakeLogsFromTheVillage` — the "draw logs from the whole village" special case, which is a warehouse in disguise.
- `HouseholdSystem.TryTakeBuildingTimber`'s village-wide sweep (D25) — likewise.

Four workarounds, all of which exist because there was nowhere to put things. **If the implementation does not delete all four, it has not replaced the placeholder, it has joined it.**

---

## 7. Failure modes to design against

- **Starving beside a full warehouse.** The legibility disaster. A villager who cannot get food must say which of the three — no store in reach, no stock in it, or nobody to fetch — is stopping them, exactly as the labour refusals do.
- **Re-creating D10.** Any path where a meal requires a journey is wrong, however elegant.
- **The unmanned-market cliff.** Why fetch is recommended over deliver.
- **Hauling eating the labour budget.** Fetching is unpaid work that competes with foraging for the same hours. The food economy is derived from trips per year (`VillageEconomy`), and adding a fetch leg to the round trip changes that derivation. **This must be re-derived, not patched** — it is exactly the D16 mistake otherwise.
- **A full warehouse silently stopping production.** A logger who cannot deposit must say so, like the woodcutter with no logs (D29).
- **⛔⛔ Fuel crowding food out of a shared roof (D361).** A cart full of firewood starved thirteen people with nobody idle and nothing wrong: laborers tidying the painted wood hauled firewood into the founding cart until it held 1,591 of 1,650, the foragers read *"every store that takes food is full"*, and food went 915 → 0. **A store that holds both food and other goods keeps half its room for food** — `StoreBuilding.RoomFor` shows a non-food load only the space above what food is still owed, `HasRoomFor` is what a hauler asks when choosing where to put a load down, and `Put` clamps to the same number (one door, D142). `Accepts` is deliberately NOT room-aware — it also answers *"does this store hold this kind?"* for fetching. Granaries and warehouses share no roof and are untouched. Guard: `StorageTests.AMixedStoreKeepsHalfItsRoomForFood`.
- **⛔⛔ Food in a buffer is food nobody can eat until somebody carries it (D362).** Joe's village: seven hunts filled the lodge; `FoodTheVillageHolds` counted it (D161, rightly); every food producer's Wants went to 0; the only carrier was one marketer at forty an armful, because `BufferWorthClearing` cleared a buffer only when it was *nearly full* — after the first eight hundred it was not; fifteen people rested for two years while the granaries drained and thirteen starved beside 1,780 meat. Three rules now: *(a)* **a buffer holding an armful of food is worth clearing whenever a STORAGE building has room** (`BufferWorthClearing` → `SomewhereToPut`; D370 — the market is a counter, D199); *(b)* ~~**the spare hands carry it** — `TryClearABuffer`, a laborer's fallback~~ **⛔ REVERSED BY JOE (D370):** *"so many villagers ran there to empty it. that is strange! it should only be 1) the fisherman (when its full) and 2) the marketer (when they have nothing more pressing)."* One cast of fish is seven armfuls, there was no claim, and every idle job-holder ran. **The buffer is the producer's** — hunter, fisher, farmer carry an armful out of their own hut when it cannot take another load (`TryClearOwnBuffer`, every armful once the food is enough) — **and the marketer's**, last in `PlanMarketErrand` after stocking, never a hut somebody is already walking to (`SomebodyIsClearing`, a read of the errand tile — no new state). Laborers do not carry buffers. The lodge guard still holds on the marketer's leg alone; a stood-down hunter keeping a seat to drain the lodge was built and measured (zero on the guard, zero on six played openings) and not kept. *(c)* **"is there enough?" and "is there room?" are two questions asked of two numbers** (`TheVillageWantsMoreFood`): enough is asked of everything the village holds, buffers included; room is asked of the shelves — the first draft asked both of one number and a hut holding 560 fish beside an emptied granary read as *"every store that takes food is full"*. **And "room" is one predicate (D370):** `SomewhereToPut` / `NearestStorageWithRoomFor` — storage only — asked by the buffer rule, the heap fetch (`NearestGroundStack`), `RoomLeftForFood` and the farmer's haul alike; four finders had four answers and the disagreement was Joe's *"villagers constantly bounce back and forth between their home and the granary"* (a heap fetched because the market had room, carried to the granary because only storage may take it, set down at its full door, fetched again). Guards: `HuntingTests.AVillageDoesNotStarveBesideAFullLodge`, `FishingTests.OnlyTheFisherAndAMarketerEverClearAFishingHut`, `AFisherDrainsTheHutWhenItCannotTakeACast`, `FarmTests.ABufferIsWorthClearingWhenItHoldsAnArmfulAndStorageHasRoom`, `GoodsOnTheGroundTests.AFullGranaryNeverSendsAnyoneBackAndForth`.

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

> **The four placeholder workarounds are gone, the village holds a stable size for 150 years with goods moving only by trips people make, and a player can point at a hungry household and a full warehouse and get a straight answer about why the two have not met.**

---

## 10. Sequencing

Bigger than the fuel chain, and that one taught the lesson: slices, each green before the next.

1. ~~**`Store` as a thing buildings have.**~~ ✅ Done — households and workplaces both got one; no behaviour change.
2. ~~**The warehouse**, and producers depositing to it.~~ ✅ Done — both village-wide sweeps deleted.
3. ~~**Fetching**, with the larder target and the refusal reasons.~~ ✅ Done — both sharing policies deleted; food economy re-derived.
5. ~~**Capacity** as a binding constraint.~~ ✅ Done — taken ahead of the market on Joe's call. See §13; it flattens the curve, and it flushed out D34.
4. ~~**The market**, manned.~~ ✅ Done — and it does more than this line said: it delivers, and it unsticks stranded goods. See §14.

**All five slices are done. D30 is closed.**

**Why 5 before 4.** The flows are proven, which was the stated precondition, and the open question that matters more is the shape of the population curve (§12). Joe's reading: a flat line means growth stopping at what the buildings support, instead of overshooting them and falling back. Capacity is the only brake in this spec that could do that, so it gets measured first. The market shortens fetch trips, which is valuable but does not regulate anything.

---

## 11. Open questions (for Joe)

1. **Fetch or deliver?** ✅ **Resolved (Joe, 2026-07-27): fetch.** A household below its larder target sends someone to the nearest store holding what they need; the market exists to make that trip short rather than to make it possible. Keeps the market valuable rather than mandatory, and never starves a village because one job went unstaffed.
2. **Where do the warehouse, granary and market come from?** There is still no building placement — the player has no agency at all — so for now they exist from the founding, like the tree stand. Found the village with a granary and a warehouse; the market arrives as a later slice. All three become placeable when placement lands, **and that is the moment this system starts paying the player back**: where you put the granary is the first decision in the game that storage makes interesting.
3. **Should one store hold everything?** ✅ **Resolved (Joe, 2026-07-27): no — a granary for food, a warehouse for materials.** See §4. Better than the middle option this spec was going to propose, because it keeps D14's inequality *and* improves what it is made of.

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

- ~~**Out:** from a store to a household below its target — the delivery.~~ **⛔ Deleted in D372 (§14.9) — no leg carries anything to a house.** The household walks to the counter.
- **In:** from a household with nobody left living in it, back to a store.

The "in" direction is the whole of Joe's point 2. A house whose family has died is not a special case in the code; it is simply a household whose need is zero and whose store is not.

### 14.4 What must NOT change

**Fetching stays exactly as it is.** §3 rejected delivery-instead-of-fetch because an unmanned market means nobody eats — *"a cliff, not a gradient, and one the founding village falls off immediately."* That argument still holds. Delivery is **additive**: a market with nobody in it means no deliveries and no unsticking, never a household that cannot eat. The founding village has no marketer and must be entirely unaffected.

This is the acceptance test for the slice, and it is a stronger claim than "the market works": **switch the market off and the village must survive exactly as it does today.**

⚠️ *"Survive"* is asked in D143's currency since D385 (§14.10): an unattended village is supposed to die out, so the guard asks that the village without a market **grows from its founders, most of its dead die of old age, and nobody freezes** over 150 years — the same bar as `TheVillageSustainsItselfAcrossGenerations` — not that anybody is alive at year 300.

### 14.5 Shape

- `JobKind.Marketer`, `market_capacity` in config — more than one seat, per Joe.
- The market is **both** a `StoreBuilding` (a third `StoreKind`, accepting food and firewood, near the homes) **and** a `Workplace` at the same position. Those are separate types today; merging them into the spec's §4 single `Building` is the right end state but not this slice's job. Recorded as a known seam.
- Households fetch from the market as well as the granary and warehouse, ~~nearest-first~~ **market-first since D372 (§14.9)**: the nearest market holding the good, and a storehouse only if no market is reachable or none holds it.

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

> *"The marketer should fill the market's stores from the granary/warehouse and then
> distribute to houses from there. Once houses are full, or the market's stores are empty of a
> needed good, the marketer should replenish the market from the granary/warehouse."*

**⛔ THE SPEC ALREADY ASSUMED THIS AND NOTHING IMPLEMENTED IT.** §14.5's last bullet says
*"households fetch from the market as well as the granary and warehouse, nearest-first — **which is
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

- **Source: the nearest storehouse holding it** — never a counter (D372; it was *"any store that is
  not this market"*, which let one market feed another).
- **Target: the counter's limit** — the player's per-good number for that market (§14.9.3) or, until
  they type, the derived `MarketStockWanted`, sized as a short trip rather than a second store.
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

#### ⭐⭐ And the player can see what a market here would serve (D201)

> Joe: *"the market should have a radius that is showing when placing it that demonstrates the
> radius the marketer will go to fetch/drop off/restock… its service area. Before placing."*

**⛔ THERE IS NO RADIUS, AND DRAWING ONE WOULD BE DRAWING A LIE.** A marketer picks the cheapest
errand from wherever they are standing (§14.2) and households fetch from whatever store is
nearest (§3) — **nothing in the model refuses a distance.** A ring would also rebuild the
catchment fence **D120 deleted**, which is the one thing this project has already paid to remove.

**⭐ The truthful answer is a count: the homes this would be the CLOSEST food store for** — exactly
the set whose walk it shortens. **It is not circular**, because it depends on where the granary
and every other store already are, and *that is Joe's own point about positioning made visible*:
a market beside the granary reads **"0 homes"**, because the granary was already nearer.

- **Strictly nearer**, so a tie goes to the store that already exists — matching the granary's
  walk has not shortened anybody's errand.
- **Occupied homes only** — a market sited to serve families that no longer exist is sited on a
  ghost.
- **Shown at placement, and the homes are ringed on the map.** The number says *how many*; the
  rings say *which way to move it*, which is the difference between a stat and a decision.

#### ⛔ And storage is separate from distribution (D199)

> Joe: *"only the marketer moves items to the market. Ordinary haulers dump in generic storage
> (stockpile, warehouse, warehouse, etc)… I just don't want it to be a dumping ground — I want to
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

### 14.9 ✅ THE MARKET IS A SHOP — Joe's redesign (2026-09-13), built (D372, 2026-09-15)

> Joe, playing D366: *"the marketer. they seems to constantly be going back to and forth to homes.
> lets change their behavior. the marketer should gather resources from storage buildings
> (granaries, stockpiles, warehouses, etc) so villagers have a central place to pick up what they
> need for their home larders. villagers go to the market to grab their food/fuel (fire wood,
> coal)/etc. markets have their own individual item storage limit (i.e., the user sets the limit
> for how much firewood is stored at a given market, how much wheat is stored, how many tools are
> stored, how many clothes, etc.). marketers, when they have nothing more important to do, should
> clear resource buildings like farms, hunting lodges, fishing huts, etc of their stored resource
> and take it to their respective storage building (warehouse, granary, stockpile, etc)."*
>
> Asked whether to spec it with home deliveries removed: *"Yes, spec it, deliveries go — but also
> adjust the time/trigger for villagers to fetch from the market. it should be when larder items
> get to 50% of their total maximum, not as soon as it is below 99%."*

**Status: built 2026-09-15 (D372), unplayed.** Everything below is in, plus one rule Joe added
after the spec was written (§14.9.7); the per-market limits' control is a row on the market's
inspector until the cards (`handoff.md` item 3) carry it. Read D14, D36, D161, D171, D199, D358(a),
D362, D370 and D372 before touching it.

**⚠️ What the spec below got wrong, kept for the record:** *"households already shop"* was true
only in the sense that the market was *a* source — nearest-first, with the granary beside the
homes, it never won a trip (§14.9.7). And *"the marketer already stocks the counter"* was true
with the leg offered last and **never counted by the labour quota**, so a village with a bare
counter and content households staffed nobody to fill it (D185's shape, a fourth time).

#### 14.9.1 What already exists (do not build it twice)

- **Households already shop.** `PlanFetch` (`BehaviorSystem`) sends an adult or elder to the
  nearest store *holding* the good — the market included, because the market `Accepts` food and
  firewood (§14.5's last bullet). Children go on the emergency restock (`TryEmergencyRestock`,
  20 %). A household with no able adult is reached by that and by the marketer's dead-larder
  collection (§14.3 *in*).
- **The marketer already stocks the counter from storage** — `OfferMarketRestock` /
  `LoadForTheMarket`, §14.8 — capped by `MarketStockWanted` (a derived `40 × occupied homes`,
  D358(a) applies it at the counter too). It is offered LAST today.
- **The marketer already clears producer buffers when nothing is more pressing** (D370): last in
  `PlanMarketErrand`, storage only, never a hut somebody else is walking to.

#### 14.9.2 What changes — two things

1. **No home deliveries.** The *out* leg of §14.3 — `Consider(...)` offering a household below
   target, `DeliveringToHome`, `HandOverAtHome` — is deleted. The marketer's day is: stock the
   counter from storage up to each good's limit (§14.9.3); then, with nothing more pressing,
   clear a producer's buffer to storage; then rest. **The dead-larder collection (§14.3 *in*)
   stays**: a house with nobody living in it and a larder full of food is still the marketer's to
   empty (D36's promise that no larder is stranded). ⛔ This reverses the half of D36 that reads
   *"the market delivers"*; §14.4's *"switch the market off and the village survives"* is
   unchanged and stays the acceptance test — a market is convenience, never lives.
2. **A household fetches at half a larder, not at the first dip (Joe).** `PlanFetch` fires today
   whenever `held < target` and the shortfall clears `WorthTheTrip` (25 % of an armful or of the
   target, `fetch_worth_this_share_percent`, D166) — early and often. New rule: a household goes
   for a good when `held ≤ target ⁄ 2`, and brings the larder back to `target` (an armful at a
   time; the trip is planned again while `held < target`, so a target above an armful is two
   trips back to back, not one trip a day). The emergency restock at 20 % stays as the floor
   beneath it. **One dial:** `fetch_below_share_percent` (50) replaces the reading of
   `fetch_worth_this_share_percent` as a trigger; the latter stays as the *"is the trip worth
   it"* bar it was written as (D166's anti-jitter).

#### 14.9.3 Per-market limits — the player's number, per good, per market

The market's stock cap stops being derived (`MarketStockWanted`) and becomes **a limit the
player sets on THAT market, per good** — the same shape as the village stock limits
(`StockLimits`, D62) one building down: *"keep up to 200 firewood and 400 wheat at market 1."*

- **State:** a `Stockpile`-shaped table of `int?` per good on the market's `StoreBuilding`
  (`MarketLimits`), **hashed** in `StateHash` beside the store's contents — it is a player input
  that changes behaviour, so it is a fact about the village (D335's rule cuts the other way for
  derived indexes only). Absent (`null`) means *the derived number* (`MarketStockWanted` stays as
  the default), so a fresh market behaves exactly as today until the player types.
- **Read by:** `OfferMarketRestock` / `NearestCounterWithRoomFor` (the marketer stocks to the
  limit, never past it — D358(a)'s cap generalised), and the market card.
- **Control:** a per-good row on the market's inspector card (the cards slice, `handoff.md` item
  3) — *not* a new panel. Until the card exists the limit is settable only in tests and through
  `SimWorld.SetMarketLimit(market, goods, int?)` (the `SetStockLimit` shape: a `PlacementVerdict`
  with a warning when the limit exceeds the market's capacity). ⚠️ Goods a market does not
  `Accept` cannot be limited; the row does not appear.

#### 14.9.4 What it must not become

- **Not a second granary.** A market holds what its limits say and the marketer keeps it there;
  producers never haul to it (D199, unchanged — `IsStorage` is still the gate in `StoreForTheLoad`
  and in D370's `SomewhereToPut`).
- **Not a delivery service by another name.** No leg carries goods to a house. If a household
  cannot reach a store or the market (all across water, or empty), the household's own fetch
  fails as it does today and the village log says so; the marketer does not step in.
- **Not a reason to walk further.** §8's standing test: *measure household walking, not
  marketer walking.* If household trips per year do not fall with a stocked market beside the
  homes, the market is a detour and the slice is wrong.

#### 14.9.5 How it is tested

- **`MarketTests.TheVillageSurvivesWithTheMarketSwitchedOff`** (300 years) — unchanged, the
  acceptance test. *(Re-posed by D385 to D143's shape, §14.10 — 150 years, peak ≥ 15, old age
  the majority death, nobody frozen.)*
- **`TheMarketKeepsLardersFromRunningDry` re-aimed:** the market keeps larders from running dry
  by being *near and stocked*, not by delivering — pose a stocked market beside the homes and a
  granary far away; assert the dry-larder rate (D363's bar, ≤ 1 per 10,000) and that no villager
  is ever in `DeliveringToHome`.
- **`AHouseholdFetchesAtHalfALarder`:** a larder at 60 % of target sends nobody; at 50 % somebody
  goes and comes back with the larder at target. Red today (a fetch fires at the first dip).
- **`AMarketIsStockedToItsOwnLimit`:** a market limited to 200 firewood beside a warehouse of 1,000
  holds 200 ± an armful for a year; raise the limit, it climbs; set it to 0, the marketer stops
  stocking it and never empties it (a limit is a ceiling, not an order to clear — D62's shape).
- **`ADeadFamilysLarderDoesNotStayStranded`** — unchanged.
- **The marketer's day, in order:** `MarketRestockTests` re-aimed so the restock leg is FIRST
  (it was last), the buffer leg second, and nothing else exists.
- **Measured before it merges:** twelve shipped seeds × fifty years and six played openings
  (D370's harness: 43 people, 22 starved on the played six), **household fetch trips per
  household-year** before and after (expect roughly half — Joe's rule is fewer, fuller trips),
  and marketer walking. The population must not fall; if it does, the fetch trigger is the first
  suspect (a larder at 50 % with a long walk to a far granary is the founding's shape).

#### 14.9.6 Definition of Done

1. ✅ The two changes in §14.9.2 built, the limits in §14.9.3 as hashed state with the derived
   default; the control is a `Keeps up to:` row on the market's inspector until the cards carry it.
2. ✅ The tests in §14.9.5 green (`MarketShopTests`); the 300-year market-off guard untouched *(until D385, §14.10)*.
3. ✅ The measurements in §14.9.5 quoted in D372, with the trip count.
4. ✅ D14, D36, D171 annotated in `DESIGN.md §7`; this section's status line true.

#### 14.9.7 ✅ Built — what is actually in (D372, 2026-09-15)

- **The market comes first (Joe, 2026-09-15, after the spec):** *"villagers should be going to the
  market and the marketer replenishes their market from the granary. the villagers should only go
  to the granary if there is no market nearby or the market doesnt have any food."*
  `BehaviorSystem.NearestShopHolding`: the nearest **market** holding the good; a storehouse only
  if no market is reachable or none holds it. *"Nearby"* is *reachable* — there is no radius to
  tune (D201's reasoning). The emergency restock at 20 % (`TryEmergencyRestock`) still goes to the
  nearest store of any kind — an emergency is the walk that matters most. Guard:
  `AHouseholdFetchesFromTheMarketBeforeTheGranary` (a granary 2 ticks away, a stocked market 11:
  the market; emptied: the granary). Red-checked.
- **Half a larder, then back to target:** `fetch_below_share_percent` (50) in `SimConfig`, validated
  between `restock_emergency_percent` and 100. **One bit of state per good per household —
  `Household.ToppingUpFood` / `ToppingUpFirewood`, hashed** — set when the trigger fires, cleared
  at target, so the trips come back to back (a family of two wanting 190 with an armful of 40
  makes three trips, not one and a hover at three-quarters). `WorthTheTrip` (D166) still bounds the
  last small armful. Guard: `AHouseholdFetchesAtHalfALarder` — 60 % sends nobody, 50 % somebody,
  and with nobody gathering the larder reaches 191 of 154 in a season against **117 without the
  flag** (the flag scored ZERO until the foragers were held off: their own take carried the larder
  past target and passed the guard for free). `TheTriggerIsTheDialAndNotAConstant` reads the dial.
- **No home deliveries:** `VillagerState.DeliveringToHome`, `HandOverAtHome`, the *out* leg of
  `PlanMarketErrand`, `NearerWorkplaceStore` (D161's *"a farm counts if it happens to be nearer"*
  fed only the delivery), `Villager.ErrandHouseholdId` and `MarketErrand`'s household fields are
  **deleted, not switched off**. `LoadForTheRound` derives what a load is for from where the
  marketer stands: a storehouse is the counter's load, a workplace is a buffer, a house is a
  stranded larder.
- **The marketer's day, in order:** the counter (`OfferMarketRestock`, sourced from **storage only**
  — `NearestStorageHolding`), then a dead family's larder, then a buffer (D370), then rest.
  `LabourQuota.MarketersWanted` counts the same three (`SimWorld.CounterWantsStocking`,
  the dead larders, `BufferWorthClearing`) and **no longer counts a household below target**.
  Guards: `TheMarketerStocksTheCounterBeforeClearingABuffer`, `ABareCounterIsAnErrand` (the
  quota's counter arm scored two reds — its own guard and `AMarketIsStockedToItsOwnLimit`, because
  with the arm gone nobody is staffed to stock anything).
- **Per-market limits:** `StoreBuilding.Limits` (a `StockLimits`, lazily built, hashed sparsely
  beside `AllowedGoods` — null and zero diverge), `SimWorld.SetMarketLimit(market, goods, int?)`
  (refuses a non-market and a good a market never holds; warns past capacity),
  `SimWorld.MarketStockLimit(market, goods)` = the limit or the derived default — read by the
  restock offer, the load, `NearestCounterWithRoomFor` and the quota through one predicate,
  `SimWorld.CounterShortOf`. Guards: `AMarketIsStockedToItsOwnLimit` (200 → peaks 200; 400 → 400;
  0 → holds 400, never emptied), `ALimitIsBoundedByWhatTheCounterCanHold`,
  `TheHashCoversALimitAndAToppingUp`. Control: `Keeps up to:` on the market's inspector — a spin
  per good the market holds, showing the derived number until typed, `clear` handing it back;
  set without its signal on refresh or every frame would write the default back as a limit.
- **⛔ A scrap at the counter is not a stocked counter.** *"Holds the good"* for a market means
  holds a trip's worth (the shortfall, up to an armful); a storehouse counts with one unit. Written
  as *any at all*, the three fish the last fetch left behind captured every trip, and a village on
  the edge starved beside a granary (the no-seam founding read 11 against 28). A scrap is still the
  **last** resort when nothing else holds any — the scrap rule alone read eight guards red for a
  village whose only food was thirty fish at the counter. `NearestShopHolding`: a market stocked for
  the trip, else a storehouse with any, else a market with a scrap. Guarded in
  `AHouseholdFetchesFromTheMarketBeforeTheGranary`, red-checked.
- **An idle marketer takes spare work** (`TryTidyGround`, `TryHelpWithHarvest`), as a stood-down
  forager does: the counter is an errand as long as households draw from it, so a village with one
  spare hand keeps a marketer for good — and that hand used to be the laborer who fetched a heap
  of logs off the ground (`AGoodTheShedRefusesStillReachesAStoreThatWillHaveIt` read ten logs a
  year beside a pile with room).
- **Re-aimed:** `AMarketerNeverWalksAnEmptyLeg` watches `StockingTheMarket`; `SkillTests`'
  work table and `ColdStartTests.IsWorking` name it in the delivery's place; the walk pins
  (`VillagerPointTests`) lost the ten-tick fetch that used to open every run.
- **Measured (D372):** eighteen played openings, fifty years: **161 → 145 people, 55 → 72
  starved**, household fetch trips **2.23 → 1.55** per household-year (−30 %); the fixture village
  a hundred years with a market: **80 % of loads taken at the counter** (was 5 %), 12 people at
  year 100 against 11; the no-seam founding over six seeds **38 → 24, against a control that grew
  29 → 43**. ⚠️ **No single rule ablated restores the 161** — the trigger at 80 reads 145 too
  (56 starved), one-fetcher off 138, the top-up off 128, the carrying guard off 148 (the old
  number stood partly on the emergency bounce); the starved count follows the trigger. **In a
  village living on the edge the safety buffer now sits in the granary, where the household that
  fetches first eats it** — filed for Joe and closed by him the same day: *"leave it"* (D32's inequality is the game).

### 14.10 ✅ EVERY LOAD TO A STORE, NEVER HOME — Joe's call (b), 2026-09-16 (D385)

Joe: *"let's evaluate if foragers should carry it home when their larder is below target or
straight to storage no matter what."* Three candidates were written up — *(a)* today's rule, home
while the larder is below target; *(b)* every load to a store; *(c)* home only below the fetch line
(§14.9's 50 %) — and he chose **(b)**: *"Go with (b)."*

**The rule.** A forager's gather, a fisher's overflow and a hunter's overflow all go to the nearest
store with room (`HaulingToStore`, `HaulOrSetDown` when there is none) — the farmer's rule since the
farm, and §5's *Producing* line as it was written in July. Nothing goes home in a producer's arms;
the producer's household shops like every other, at half a larder (§14.9). **Why:** *(1)* D32's
ruling that inequality is made of *distance and hands* and never of *"whose larder it is, which is
an accident of which house a forager was born in"* — and home-first was exactly that: over twelve
seeds and fifty years 34 starved, **not one in a household holding a forager's seat**. *(2)* Food
in a larder is invisible to every count the village runs on — the birth gate, the food quota, the
market's stocking and the food limit all read the stores (`AHouseholdLarderIsNotVillageFood`) — so
two foragers keeping their own families full read as a hungry village beside an empty granary.
*(3)* One rule for every producer.

**Two rules the pooling exposed, and one measurement that kept a third.**
- **The spare hand goes first.** A household's fetch is planned by whichever member decides first,
  and with the forager's family shopping too a farmer in autumn went for the armful as readily as
  the daughter with no seat — the farm guards read 58–70 % reaped with the farmer fetching. A
  job-holder leaves the trip to a living, working-age housemate without a seat when there is one
  (`SomebodySpareCouldFetch`); nobody spare, and the job-holder goes.
- **Already home with nothing to do is a rest spell.** A trade branch that finds no work says
  `GoHome`, and a villager already at their door arrived in the same tick as a spell-less
  `Resting`, re-asked the next tick, and flickered — hidden while foragers fed their own larders
  and rarely ran out of work; `SomebodyWhoHoldsAJobRestsInSpellsToo` read 0 % in a spell under (b).
  `GoHome` at home is `rest_ticks` of rest.
- **A family's short larder stays a reason to forage.** The food trades work while the larder is
  short OR the village wants more. Read as one reason (the village's want, the larders' shortfall
  folded in) the foragers stopped at the target, the fetches drew it down as fast as they filled
  it, and the gate barely opened: **155 / 207 / 44** against **244 / 270 / 24**. Kept as two.

**⭐ The finding: the granary's headcount now opens the birth gate, and §12's wave is back.** The
food that used to sit in larders sits in the stores, which is where the gate reads
(`FoodTheVillageHolds() ≥ TargetFoodForTheGranary() × birth_food_percent`), so the fixture village
grows further and then runs in **booms and famines**: the granary (2,500) fills, the gate opens
past what two forager seats feed, and a famine follows, because nobody unattended sites the second
hut the headcount is asking for. §12.3's ceiling — `C ÷ (stockpile_target × birth_food_percent)`,
26 at 100 % — is where it arrives. **`birth_food_percent` 60 → 100, re-based, not re-tuned:** at 60
the fixture bred to 24 and starved 28 against 27 of old age; at 100, by D155's criterion (growth
arrives, starvation a minority of deaths), it peaks at 22 with 18 starved against 31 of old age.
Two gate formulas that subtract what the larders are owed were built and measured and neither
moved the fifty-year village a soul (259 / 278 / 12 either way); the bar did. `PopulationCeiling`
moves by the same arithmetic. ⚠️ *A proportional (production-aware) birth gate is still §12.3's
"real answer" and still not built — Joe played the boom-bust 2026-09-17: *"it all feels fine"*, so it reads as pressure (D155) and the gate stays filed, not scheduled.*

**Measured, twelve fixture seeds × fifty years (alive / peak / starved):** D384 **161 / 210 / 34**
→ (b) alone **244 / 270 / 24** → (b) with the bar at 100 **259 / 278 / 12**. The fixture 150 years:
peak 22, 18 starved, 31 of old age, 16 alive.

**Guards.** `TradesVisiblyWorkTests.AForagerTakesEveryLoadToAStore` (red with home-first: a load
walked home). **`TheVillageSurvivesWithTheMarketSwitchedOff` re-posed to D143's shape** — it read
*never below the founders through year 300*, which held only while larders hid the food; with the
stall the fixture rides the troughs out to year 300 with fourteen alive and without it ages out at
165, and D143 rules that an unattended village *should*. It asks now what
`TheVillageSustainsItselfAcrossGenerations` asks: 150 years, peak ≥ 15 from four founders, old age
the majority death, nobody frozen — measured **24 / 21 starved / 27 of old age** without a stall
against 22 / 18 / 31 with one. Some fifteen fixture-premise guards re-posed because they counted food
where the old rule put it (the winter drain reads larder + granaries; the food limit tracks trips
in flight by name; the farm grounds start each spring with nothing in the stores and one hand,
because the granary now feeds the fetches that used to come from the larder; the lone walker's
worn tiles; the no-seam bar; the valley pin). Six goldens moved once (the seam pair, the fifty-year
pair, the two skill hashes).

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

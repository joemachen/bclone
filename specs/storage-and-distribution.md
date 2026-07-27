# Spec: Storage and Distribution — goods live in buildings

> Status: **draft — awaiting Joe's review before implementation** · Owner: Joe + Claude Code
> Format per `METHODOLOGY.md §2`. Implements decision **D30**; delivers the building half of **D14**.

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
| Tree stand | a little logs | same shape; the felled pile beside the stumps |
| Storage shed | bulk everything — stone, logs, lumber, cloth, firewood | the general store, and the overflow every producer runs to |
| Market | food and firewood, near the homes | shortens the fetch; D14's building |

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

1. **`Store` as a thing buildings have.** Households and workplaces both get one; no behaviour change.
2. **The storage shed**, and producers depositing to it instead of carrying goods home. Deletes the two village-wide sweeps.
3. **Fetching**, with the larder target and the refusal reasons. Deletes both sharing policies. **Re-derive the food economy here.**
4. **The market**, manned, stocking itself from the shed.
5. **Capacity** as a binding constraint, once the flows are proven.

---

## 11. Open questions (for Joe)

1. **Fetch or deliver?** *(Recommendation: fetch — see §3. It keeps the market valuable rather than mandatory and never starves a village because one job went unstaffed.)*
2. **Where do the shed and market come from?** There is still no building placement — the player has no agency at all — so for now they must exist from the founding, like the tree stand. *(Recommendation: found the village with one shed; the market arrives as a second slice. Both become placeable when placement lands, and that is when this system starts paying the player back.)*
3. **Should the shed hold food?** Letting it means a village can bank a real surplus and survive a bad year, which is good. It also means the per-household inequality D14 exists to create gets softened — one big pile is one big pile. *(No recommendation. This is the one that decides whether "one family starving beside a thriving neighbour" survives contact with storage, and it is a design question about what stories the game wants, not an engineering one.)*

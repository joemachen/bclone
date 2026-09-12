# Spec: Fishing — the food that never runs out, and the first thing that had to touch water

> Status: **✅ BUILT AND SHIPPED (D282–D290, 2026-09-03).**
> ⚠️⚠️ **THIS SPEC WAS WRITTEN AFTER THE CODE, ON 2026-09-06, AT JOE'S REQUEST.** It is a
> **reconstruction from `DESIGN.md §7` (D277–D290), the config comments and `FishingTests`** —
> not the document the work was built against, because there wasn't one. Every number here was
> read back out of the shipped code or the decision that set it; **nothing in it is a proposal.**
> · Owner: Joe + Claude Code
> Format per `METHODOLOGY.md §2`.

> ⛔ **WHY IT IS LATE, RECORDED RATHER THAN TIDIED AWAY.** `METHODOLOGY §2` says no non-trivial
> system gets built without a short spec written **before** the code. Fishing is a trade, a
> building, a good, a job and four config keys — it is not trivial, and it shipped across nine
> decisions with **only a status line in `food-catalog.md`** to its name. Hunting, built days
> later, got `specs/hunting.md` up front and **that spec caught a trap before it was stepped in**
> (§4's reuse of the forest-exhaustion machinery, which fells the tree). *Fishing had no such
> document and paid for it in D283, D285 and D286 instead* — three separate discoveries that fish
> was food nobody could eat, fetch, or measure. **The cost of the missing spec is legible in the
> log; that is the argument for the next one.**

---

## 1. Goal

**A food source that never depletes, seats the most workers of any food building, and is worth
leaving foraging for** — paid for with distance, because the river is at the edge of the valley.

Joe, 2026-09-02, setting all of it in one sentence:

> *"fishing provides a consistent source of food that does not run out — up to 4 seats. A step up
> from foraging in terms of food per worker. **Foraging is bottom of the totem pole.**"*

---

## 2. Which pillars / non-negotiables this serves

- **§1.1 Legibility.** A fishery is the one food source whose yield is **not** a function of
  anything the player has done to the map. Foraging and hunting both read standing woodland
  (D297); farming reads soil and distance. **Fishing reads nothing** — it is the flat, dependable
  floor a village can plan against, and that is a legible thing to be.
- **§0's core loop — "maintaining the production pipeline while scaling the village".** A food
  source that does not run out is what lets the pipeline *scale* rather than merely *survive*.
- **§0.1's cozy/challenging lens.** The fishery is **cozy** — reliable, unexhaustible, four
  seats. The **challenge** is that the river is 2.5% of the valley and always at the periphery,
  so the food you can most rely on is the food you must walk furthest for.
- **⛔ What it deliberately does NOT serve: pressure.** There is no depletion here and there must
  not be. D297 settled that standing woodland is the only thing that moves a yield in this game;
  **water is not woodland and a fishery thins nothing** (`AFisheryCompetesWithNothingAndThinsNothing`).

---

## 3. Data model — every number, and the decision that set it

All keys live in `data/sim.config.json`; defaults are in `SimConfig`.

| Key | Value | Set by |
|---|---|---|
| `fishing_hut_seats` | **4** | Joe, 2026-09-02 — the largest seat count of any food building |
| `fish_yield` | **400** | D358 (from 300, by D288's method — a forager walks worn paths and got quicker, a fisher did not); D288 (from 100; Joe asked for ~2.5×, **overshot to 300 on purpose**) |
| `fish_ticks` | **10** | D282 (from 3 — the longest action in the game) |
| `fishing_hut_store_cap` | **1200** | D358 (from 900, with the yield — still **three casts**); D290 (from 300) |
| `fishing_hut_logs` | 25 | with the hut |
| `fishing_hut_stone` | 3 | with the hut |
| `fishing_hut_work_ticks` | 40 | with the hut |

**⭐ The seat count is the rank of a food source.** Forager 2, farm 2, hunter 3, **fisher 4** —
the ordering is the design, and the fishery sits at the top of it.

**⛔⛔ `fish_yield` AND `fishing_hut_store_cap` ARE COUPLED, AND THE GUARD SAYS SO.** D288 raised
the yield to 300 against a 300 buffer, so **a catch filled the hut exactly**: it held one cast,
the fisher hauled after every single one, and the marketer had nothing to come for. Both numbers
still read as the ones Joe asked for while the feature they existed to add had switched itself
off. **`TheHutHoldsMoreThanOneCast` is a ratio, not a number**, because a capacity is only
meaningful in casts. *This is D50's shape a second time — capacities that do not move when yields
do.* **If you move either number, read that guard first.**

---

## 4. The building

- **⭐ It must touch water, and that is a fourth kind of placement rule.** `CanBuildAt` until the
  fishing hut was either an impossibility (water, occupied, off-map, unreachable) or a
  warn-and-allow. *"It must touch water"* is neither — it is a **requirement on a buildable
  tile**, and `Construction.cs` records it as such.
- **⚠️ `pathfinding-and-water.md §12` is why this costs something:** the village always forms
  *away* from the water, so the fishery is the building the player must deliberately walk out to.
  **That distance is the balance** against four seats and an inexhaustible yield.
- **⭐ It is a row, never a `kind == BuildingKind.FishingHut` check** (`Building.cs:109`). D222
  made a building a row; the fishery must not be the thing that reintroduces a switch.

---

## 5. The work

A cast is **10 ticks** and brings back **400 fish** (D358; 300 before desire paths) into the hut's local store (cap **1200**, three casts).
The fisher hauls to a granary when the hut fills; a **marketer runs the buffer dry** in between
(`AMarketerRunsTheFisheryBufferDry`).

**⭐⭐ A LONGER CAST WAS A PURE PACING CHANGE, AND ONLY MEASUREMENT COULD SAY SO (D282).** Joe:
*"he does reach the hut and fishes, but catches fish too quickly."* The plan called raising
`fish_ticks` *"a yield change wearing a pacing change's clothes"* — **and the plan was wrong.**
Measured over a year: a fisher landed **800 fish at three ticks and 800 at ten**, while time spent
casting went **26 ticks → 94**. The reason is that a fisher gets **about eight casts a year
either way**; the year goes on walking, eating and sleeping, and *the cast was never the limiter.*

⭐ It is also the first action long enough for mastery to express more than two tiers —
`WorkTicksFor`: *"a three-tick action can only become two."*

**⚠️ Winter does not recall a fisher.** The river does not freeze in this game and the trade is
worked in all four seasons (`WinterDoesNotRecallAFisher`).

---

## 6. ⛔⛔ The failure mode this system actually had: FOOD NOBODY COULD EAT

**This is the section that would have existed had the spec been written first, and it is the
reason the spec is worth writing even now.**

D277 made *"what counts as food?"* one question with one answer (`GoodsCatalog.EdibleGoods`), and
the **totals** all learned it. **Three whole capabilities did not**, and each was found separately,
by Joe, from the panel:

1. **The mouth (D283).** `TryEat` still asked for `Goods.Food` by name, so a granary full of fish
   read as full to the birth gate and the food limit **while every villager standing in it
   starved.** Measured: population **3 → 0, five dead.**
2. **The hands (D285).** Joe: *"do the villagers actually eat the fish? I don't see any fish in
   any home larders."* `PlanFetch` asked `household.Stockpile.Food < floor` (a larder full of fish
   read as empty), then `NearestStoreHolding(…, Goods.Food)` (a granary holding 2,000 fish was not
   a source), and the larder deposit put down food and firewood **by name**. **Nothing errored;
   the errand simply never fired.** Measured on a fish-only village: not one fish reached a larder,
   population **4 → 3**; after, 160 in the larders and **4 → 6**.
3. **The cart.** The marketer's hardcoded two-good array, an
   `if (goods == Goods.Food) … else Goods.Firewood` inside its own loop, and the buffer pickup in
   `LoadForTheRound` — **whose own comment had predicted it**: *"a trader walks to the farm, finds
   nothing they know how to pick up, and goes home empty-handed for ever."*

**⭐⭐ THE LESSON IS ABOUT THE SHAPE OF A CAPABILITY CONVERSION, NOT ABOUT FISH.** Converting every
*reader* of a total and missing the one *consumer* leaves an economy that **counts a good it
cannot use**, and every symptom points at starvation rather than at the catalogue. *Five
farm-shaped assumptions blocked fish; the count is the point.*

**⭐ The cure is one door, used everywhere:** `SimWorld.FoodIn(Stockpile)`,
`SimWorld.TakeAMealFrom(Stockpile, int)`, `MoveFood`, `NearestStoreHoldingFood` — in catalogue
order, so a village that has only ever seen one food is byte-identical and **no golden moved.**
D298–D301 finished the job by deleting `Stockpile.Food` outright rather than renaming it: *the
friendly accessor **is** the trap.*

---

## 7. ⭐⭐ How "is it worth it?" is measured — and three ways it cannot be

**⛔ YOU CANNOT MEASURE "FOOD PER WORKER" IN A VILLAGE THAT ALREADY HAS ENOUGH (D286). Three
attempts proved it:**

1. **Annual throughput is demand-limited and NON-MONOTONIC.** Work is gated on the village still
   wanting food, so **a more productive fisher works less**: `fish_yield` 130 gave **910 fish a
   year** and 170 gave **510**.
2. **Switching to a rate only moved the distortion.** At a yield of 300 the fisher worked **37
   ticks in the entire year** and scored 1621 per hundred — a number about *saturation*, not
   about fishing.
3. **Holding demand open for both sides** — a stockpile target nobody can reach — finally gives a
   stable, monotonic reading. **This is the only valid instrument, and any future measurement of
   this trade must use it.**

**⛔⛔ A PER-LOAD COMPARISON IS NOT AN ECONOMY (D284).** The retired guard
`FishingIsAStepUpFromForaging` compared `fish_yield` (100) against `GatherYieldAt` (77) and
concluded fishing won. **That assumes both jobs get the same number of loads a year, and they do
not** — a fisher gets ~8 casts, a forager ~7 trips, against a `TripsPerYear` ceiling of 17 that
neither comes near. Per *hour worked* the real reading was **311 against 721** — *fishing was
losing by more than half while its guard was green.* **Loads are not hours, and only hours answer
"food per worker."**

**⚠️ AND THE FIRST REPLACEMENT WAS BLIND TOO**, which only a red check exposed: halving
`fish_yield` also impoverishes the village, so the foragers make fewer trips and **the ratio
survives** — 400 against 308, passing, when the untouched reference was 539. ⭐ **The forager
baseline now comes from a second village with no fishery in it**
(`AFisherOutEarnsAForagerPerTickWorked`).

**✅ Where it landed (D288):** `fish_yield` **300** reads **830 per hundred ticks worked against a
forager's 721** — about **1.15×**. Joe asked for ~2.5×; **2.5× (yield 250) lands at 691, just
*under* parity**, so following the letter would have satisfied the instruction and failed its
stated purpose. **300 is the first round number clear of the noise**, and the overshoot is
deliberate and recorded.

---

## 8. How it is tested

`tests/Bclone.Sim.Tests/FishingTests.cs`:

| Guard | What it holds |
|---|---|
| `AFishingHutHasToStandAgainstTheWater` | the fourth placement rule |
| `AHutAcrossTheRiverIsRefusedForTheRouteNotTheWater` | ⭐ touching water is **not** enough — it must be *reachable*, and the two failures are distinguished |
| `TheVillageAsksForFishersAndPostsOne` | the trade is demanded and staffed |
| `AFisherAtTheHutCatchesFish` | the cast produces |
| `AFisherWalksTheWholeWayToADistantHut` | the periphery cost is real |
| `WinterDoesNotRecallAFisher` | worked in all four seasons |
| `TheCatchGoesIntoTheHutsOwnStore` | the catch lands in the buffer, not the arms |
| `AMarketerRunsTheFisheryBufferDry` | the buffer is a buffer |
| `AFisherOutEarnsAForagerPerTickWorked` | **per hour, against a fishery-free baseline** |
| `AHouseholdFetchesFishHomeAndLivesOnIt` | D285's hands — the errand fires |
| `AFisheryCompetesWithNothingAndThinsNothing` | no depletion, ever |
| `TheHutHoldsMoreThanOneCast` | **the ratio, not the number** |

⚠️ **`WinterDoesNotRecallAFisher` was reddened twice by buffer changes** (D288, D290) because it
watched the fisher's **arms** — with a big enough buffer the whole cast goes into the hut and the
man carries nothing. *A guard that can be reddened by a buffer being big enough was never testing
the season.* It watches the season now.

---

## 9. Definition of Done — assessed retroactively

| # | Item | State |
|---|---|---|
| 1 | Hut places, requires water, costs logs + stone | ✅ |
| 2 | Four seats, staffed by the allocator | ✅ |
| 3 | Fish is a good in the catalogue, edible end to end | ✅ (D283, D285 — *after* the fact) |
| 4 | Buffer + marketer round trip | ✅ (D290) |
| 5 | Worth more per hour than foraging, measured with demand held open | ✅ 830 vs 721 (D288) |
| 6 | Suite green, goldens unmoved | ✅ no golden moved |
| 7 | **Spec written before the code** | ⛔ **NOT MET — this document is the retrofit** |
| 8 | Manual QA walk against a checklist | ⛔ **never performed** |

---

## 10. Open

1. **Fish subtypes (trout, etc.)** — Joe deferred these *"until the panels can group them"*
   (queued behind the build bar and the materials/ingredients categories). `food-catalog.md`
   carries the tier list.
2. **⚠️ No test asserts the D288 balance**, and that is deliberate (D286): closing the gap meant
   ~2.5× the yield — *a balance change to a number Joe has played and approved, and a test must
   not make that decision by asserting it, nor launder it as a bug fix.* The guard measures and
   reports; **the number is Joe's.**
3. **The river does not freeze.** Never raised, never designed, recorded here so it is a decision
   rather than an oversight the next session "fixes".

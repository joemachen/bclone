# Spec: Goods on the ground, and a pile that costs the clearing

**Decisions:** **D96**, and D90 step 4 which has been waiting on it. Implemented as **D97**
(ground stacks), **D98** (the instant pile) and **D99** (the cart). Neighbours: D76, D80, D83,
D89, D95. **Status:** ✅ **all three steps shipped** (suite is 857 green now, not the 496 this line froze at). ⚠️ *The view REPORTS ground goods since 2026-08-27 — the overview says "+12 on the ground" and names the real reason — but `VillageMap` still draws no heaps. Corrected 2026-08-28.* Awaiting Joe's QA pass
(§10.6) and the view, which draws none of it yet.

---

## 1. Goal

Three changes, in one order, and **the order is the whole design** (D96):

1. **A villager can set a load down on the ground** when no store will take it, and anybody
   can pick it up again. No decay.
2. **A storage pile becomes instant** — but only on ground already clear of trees, stone and
   iron. Its cost becomes **the clearing**.
3. **The cart stops accepting logs** (D90 step 4), which is what 1 and 2 exist to make safe.

Doing 3 first was tried and reverted (D95): a pile is a construction site, so between marking
one and it standing there is a window where the cart refuses logs and no pile exists — and a
forester has nowhere on earth to put a load. **0 homes, nothing built at all.** 1 removes the
"nowhere on earth"; 2 removes the window.

---

## 2. Which pillars this serves

- **§1.1 legibility.** A load that vanishes because the warehouse was full is the untraceable
  outcome this project keeps promising not to ship. Today it genuinely vanishes — see §3.
- **§0.1, the cozy half.** *"The village is full"* stops being a state that only resolves by
  somebody standing still holding an armful (D80's crash, patched from the top).
- **§2.2, and D66's missing hauling.** Picking a load up is an errand that needs no
  construction site to exist, so the laborer finally has the second half of their work.
- **§1.6, traceable over clever.** Supply-invisibility is **structural**, not a filter: a
  ground stack is not a store, so it cannot be counted by anything that counts stores.

---

## 3. What was measured first

**There is a conservation leak today, and it is exactly the hole ground stacks fill.**
`BehaviorSystem.ArriveAt`'s `HaulingToStore` branch does:

```csharp
Stockpile store = StoreForTheLoad(world, villager).Store;
store.Add(Goods.Food, villager.CarriedFood);      // returns what FITTED
...
villager.CarriedFood = 0;                          // ...and the rest is gone
```

`Stockpile.Add` clamps to `FreeSpace` and **returns how much it actually took**; its own
remarks say *"the return value is the whole point and it must not be ignored… callers deposit
what fits and keep the rest"*. This caller ignores it. `RaiseTheBuilding` one file over gets
it right — *"anything the site could not take stays in their arms and goes back to a warehouse on
the next errand — never dropped, per the conservation rule."*

So D80's fix — walk to the store that *would* take it and hold the load until there is room —
was written into `StoreForTheLoad` and then undone at the destination. **Setting the load
down is the honest end of that walk**, and it turns a silent leak into a heap the player can
see and send somebody to fetch.

### 3.1 ⭐ Measured, and it is large

Fifty years of an established village, both configs:

| | fixture | shipped |
|---|---|---|
| Ticks (of 24,000) with some store full | 5,623 | 6,119 |
| Goods that ended up on the ground once the leak was closed | **22,330** | **17,494** |
| …of which food, heaped at the granary's own doorstep | 22,317 | 17,451 |

**Seventeen thousand food went out of the world at a full granary's door**, and nothing ever
read as wrong because the leak only ever made totals *fall*. A forager whose own larder was
topped up while they walked hauls the surplus to the granary; the granary is full; the load
evaporates; repeat, for fifty years.

**And closing it is a balance change, which is the honest thing to record about it:**

| fixture, year 50 | before | after |
|---|---|---|
| firewood ever cut | 6,654 | 6,548 |
| food in stores | 2,164 | **3,640** |
| population | 29 | **36** |
| frozen / starved | 0 / 0 | 0 / 0 |

Production did not move. **The village is 24% bigger because it keeps 40% more food**, which
is the birth gate doing exactly what it is supposed to with food that now exists. The
per-capita fuel stock is therefore thinner at the winter trough, and nobody freezes in either.
**That is Joe's number to know**, not a number to tune around.

### 3.2 One ordering change was required with it

`StoreForTheLoad` preferred *the right kind of building* over *a building with room*:

```csharp
NearestStore(wanted, !IsFull) ?? FirstOfKind(wanted) ?? NearestStoreAccepting(load, !IsFull)
```

`FirstOfKind` returns a store of the right kind **whatever its state**, so a forager holding
berries with the granary full was sent to the full granary while the market stood at 68 of
800. That was merely wasteful while the load then evaporated. **With goods able to be set
down it is a loop**: put the food down at the granary door, pick it up again as a spare hand,
walk nowhere, put it down again, forever.

So room now outranks kind, and `FirstOfKind` moves to the arm below — which is the case D48
wrote it for, a granary across the water and somebody who must still be given somewhere to
walk. **D33's preference survives intact**: a granary that *can* take the load is still
chosen first, so the population ceiling still does not depend on where anybody was standing.

---

## 4. The design — ground stacks

### 4.1 A ground stack is not a store, and that is the restraint

**Supply-invisible is a consequence of the type, not a rule anybody has to remember.**

| | |
|---|---|
| What it is | `GroundStack`: a `Goods`, an amount, a `GridPos`. |
| Where it lives | `SimWorld.GroundStacks`, a list of its own — **not** `StoreBuildings`. |
| Counted by `TotalFood`, `LogsInWarehouses`, `FirewoodInWarehouses`, the quota, the birth gate? | **No.** Those all walk `StoreBuildings`. A stack is not in that list, so no reader had to be taught to skip it. |
| Capacity | **None.** It is a heap on the ground. |
| Decay | **None** (D96, Joe: spoilage is not available and was refused once already, D37). |
| Shelter | **None.** It is not a building. |

**Why this is the right shape rather than a fifth `StoreKind`.** A `StoreKind.Ground` would be
found by `NearestStoreAccepting`, summed by `TotalAccepting`, and counted as room by
`FoodTheVillageHasRoomFor` — so **every one of those would need a new "…except the ground"
clause**, which is D76's seam wearing a new costume and the sixth instalment of the bug that
has already cost this project five. Supply-invisibility asked as a question is a rule five
readers can forget; supply-invisibility as a *different list* is one nothing can.

### 4.2 Setting down is last-resort-only

**It never happens because it is convenient.** There is exactly one trigger:

> The load has nowhere to go — either no store in the village accepts this good at all, or
> the one they walked to had no room left when they arrived.

Concretely, `StoreForTheLoad` stops throwing and returns `StoreBuilding?`. Its callers then
have a null branch, and the null branch is *put it down where you are standing*:

| Call site | Where the load lands |
|---|---|
| `CompleteAction` — `Cutting`, `Clearing` | At the stump. Nowhere to carry it to, so it stays where it was cut. |
| `ActOne` — `HaulingToStore` re-plan | Wherever they had got to. |
| `ArriveAt` — `HaulingToStore` | At the store's door: **what fitted goes in, the remainder goes down.** This is §3's leak, closed. |

**The throw stays for the case it was written for.** `StoreForTheLoad`'s
`InvalidOperationException` said *"every village must have somewhere to put things"* — after
this change that is no longer an invariant, so the throw goes and the sentence becomes a
`DEBUG` line naming the good and the place. Never swallowed (METHODOLOGY §4), but not fatal:
a village whose stores are all full is a village with a **problem**, not a broken world —
D80's own words, applied one level further down.

### 4.3 Picking up is its own errand, because it has to be

**D96 names this as a gift rather than a cost.** Supply-invisible goods cannot be pulled by
*"the village wants more food"* — that question reads stores, and the ground is not a store.
So the errand is stated from the other end: *"there is a load lying about; take it to a
store."*

- **New `VillagerState.TidyingGround`.** Appended to the enum, so nothing renumbers.
- **`TryTidyGround`, positioned directly above `TryHelpWithHarvest` in `Decide`** — below
  every job branch, above clearing, above resting. Anybody who reaches it has already declined
  their own work this tick, which is D87's rule and is why neither needs a quota, a job kind
  or a rule about who is allowed.
- **Above clearing on purpose:** a load already won is worth more than a load not yet taken,
  and tidying before felling is what stops a painted valley producing heaps faster than it
  produces order.
- **⭐ Only fires when a store will actually take the load.** Without that condition a
  villager picks up a heap beside a full warehouse and walks it back to the same full warehouse forever.
  With it, a village whose stores are full simply leaves its heaps alone until there is room —
  which is the self-correcting behaviour D96 predicted, and it needs no rule telling anybody to.
- **The tile is remembered on `ErrandX/ErrandY`**, like clearing and for the same reason: the
  nearest heap is judged from where somebody is standing, so re-deciding mid-walk lets them
  shuttle between two heaps forever.
- **Never a child** (`CanWork`), matching `TryHelpWithHarvest`.

### 4.4 Performance: the early-out is not an optimisation

`NearestHarvest` cost the suite six minutes before it learned to return null on an unpainted
valley (D87). `NearestGroundStack` is asked by every idle able adult every tick, so it takes
the same guard in the same shape: **no stacks, no scan.** Unlike the harvest layer this is a
list rather than a grid, so the guard is `GroundStacks.Count == 0` and the scan is over the
heaps rather than over 9,600 tiles — cheaper than the thing that nearly cost us the suite, and
guarded anyway.

### 4.5 Hashing

**Sparse and countless, exactly like the harvest layer** (D87): each stack mixes its position,
its good and its amount; **a village with no stacks mixes nothing at all.** A count mixed
unconditionally would put a fresh zero into every established village and move both goldens
for a feature nobody used — the mistake the residential layer made and the harvest layer
deliberately did not repeat.

---

## 5. The design — the instant pile

### 5.1 The pile's cost is the clearing

Joe (D96): *"If there are resources, they must first be cleared and then the stockpile can be
instant."*

> **⚠️ CORRECTED BY JOE (D100). The first version of this refused the mark, and that read the
> rule backwards.** *"I want laborers to auto-remove the resources if a building is placed on a
> resource — the user can if they choose to, but shouldn't have to."* **The clearing is still
> what a pile costs; it is a price the village pays rather than an errand the player is sent
> on.** §5.3 has the corrected design.

- ~~**`CanBuildAt(Pile, …)` refuses ground that still has something standing on it.**~~
  **Withdrawn** — see §5.3.
- **On clear ground, `Mark(Pile, …)` raises it there and then.** No `ConstructionSite`, no
  `Workplace`, no builder. The pile *is* what the sentence says it is: cleared ground with
  goods stacked on it.
- **`pile_work_ticks` goes** — config key, `SimConfig` property and all. D96: *"a number that
  is always zero is a lie waiting to be found."* `BuildingRecipe.For(Pile)` becomes
  `(0 logs, 0 ticks)` and is kept only because `Demolish` reads a recipe to work out the refund.

**Only the pile takes this rule.** A granary marked in a wood is a separate question and is
not opened here; the pile is the building whose *entire* cost this becomes.

### 5.3 ⭐ The village clears the ground; the player does not have to (D100)

**Joe's rule, and it is about every building rather than about the pile:** *"laborers
auto-remove the resources if a building is placed on a resource."*

| Marked on | What happens |
|---|---|
| Clear ground | Pile stands the same tick. Every other kind gets its site, as today. |
| Ground with trees / stone / iron | **The tile is painted for harvest**, and the laborers who already clear painted ground (D87) come and take it. |

- **No new machinery.** The brush, the errand and the deposit rule all exist. What the mark
  adds is the *intent*, which is `building-placement.md §12.1`'s pattern exactly: the player
  paints intent, and the village acts on it when it has a reason to.
- **A pile then WAITS** — held on `SimWorld.PilesWaitingOnTheGround`, which is player intent
  and therefore hashed (sparsely, so a village that marks none mixes nothing). It goes up from
  `SetTerrain`, **the one door terrain changes through** (D85), so it fires whoever did the
  clearing — a laborer working the paint, or the player doing it by hand.
- **⚠️ Only the pile waits, and the asymmetry is deliberate.** A pile *is* the ground it stands
  on; a granary is a building that happens to be there. Making every site wait for a clearing
  would insert a hop into the cold start, and D93 measured an inserted hop as the thing that
  kills winter 1.
- **Deliberately not a `ConstructionSite`.** A site needs a builder to tick it, and the whole
  point of an instant pile is that it needs nobody — a site would put the builder dependency
  back and re-open the window D95 died in. A waiting list is what this actually is.

#### ⭐ 5.3a A blocked footprint is cleared before any coppice (D157)

**"No new machinery" above was right about the parts and wrong about the order, and it cost a
year's play to find out.** The brush, the errand and the deposit rule did all exist — but D127
then turned harvest paint into a *standing* instruction whose wood grows back, and
`NearestHarvest` picks the nearest painted tile. Between a village and a footprint eight tiles
out there is always nearer coppice, and it always regrows before the laborers run out of it. So
the tile this table promises will be cleared **is never the nearest tile, and never gets
cleared at all.**

Measured on the shipped opening: a gatherer's hut marked in real woodland is still standing on
`Forest` after **forty years**, while the panel says *"Waiting: the ground it stands on is
still being cleared"* — true, and never going to finish. All four founders freeze in winter 1.

**The rule:** a laborer looking for painted ground takes a tile a marked building is waiting on
before any other painted tile.

- **Free buildings first, in marking order.** A pile and a builder's hut cost nothing but the
  ground, they are not in the build queue, and until the pile stands there is nowhere to put a
  felled log — so clearing anything else first makes timber the village cannot see (D95).
- **Then construction sites, in the build queue's own order** — rank, then id, exactly as
  `BuildQueue` sorts (Joe, D157). *Clearing defers to building.* The first cut of this took the
  nearest blocked footprint, which is a second ordering over one list, and `NextToBuild`'s own
  comment names two orderings that must agree as the shape of half the bugs here. A site the
  player moves up the list moves its clearing with it, which is what the list is for.
- **The paint is still required, so this is priority and not scope.** `Mark` and `MarkHome` put
  it on; a player who takes it off is telling the village something. The set of clearable tiles
  is identical — only the order moves.
- **Reachability is asked of the villager, not the village.** A laborer who cannot walk to the
  head of the queue falls through to work they can reach rather than standing still.
- **Walked from the buildings, not from the paint.** Asking *"is a building waiting here?"*
  per painted tile would be the whole workplace list × hundreds of tiles × every idle adult ×
  every tick — the cost ruin `NearestHarvest`'s own comment records. A village has a handful of
  unraised buildings; that is the short list.

### 5.2 The bug next door, found while reading `Demolish`

```csharp
BuildingKind kind = building.Kind switch {
    StoreKind.Granary => BuildingKind.Granary,
    StoreKind.Warehouse    => BuildingKind.Warehouse,
    _                 => BuildingKind.Market,   // ← a pile, and the cart, refund like a market
};
```

**Pulling down a pile pays back half a market's logs — 17 logs out of a building that cost
nothing.** So does demolishing the cart. It is a free-timber press, it is adjacent to the
change this slice makes to a pile's cost, and it is fixed here rather than noted: the two
buildings the player did not pay for refund nothing.

---

## 6. The design — the cart stops accepting logs

`StoreKind.Cart` accepts everything except `Goods.Logs`. It is *"what you arrived in"*: your
food and your tools, and timber is the one thing that plausibly will not fit (D90).

- **`cart_logs` is deleted**, not zeroed. The shipped file is already at 0 (D95); leaving the
  key would mean the fixture's default of 10 quietly loaded logs into a cart that refuses
  them, since `Stockpile.Receive` knows nothing about `Accepts`. §5.1's rule about numbers
  that are always zero applies to this one too.
- **`StoreKind.Cart`'s "it accepts everything" doc comment is rewritten in the same commit**
  (D90's own condition), or it joins the list of comments describing a world that has moved on.

**What this fixes, and it is measured already.** `ColdStartTests.PlayTheOpening` records four
forty-year arms, of which one is a disaster: *no pile + harvest painted* goes **4 → 6 → 2**,
against 8 or 9 for the other three. The cause is written down there — *"the cart fills with
timber (677 logs of its 1,200 by year five) and the food it crowds out never arrives: 164 food
against 400+ in every other arm"*, dying with **zero starved and zero frozen**, which D89
called a legibility failure before a balance one. **A cart that cannot hold logs cannot be
strangled by them.**

---

## 7. Failure modes to design against

- **The ground becoming a free warehouse.** Both of D96's restraints, together. Supply-invisible
  means a village living off its heaps never grows; last-resort-only means it never chooses to.
- **Shuttling.** A villager picking up a heap beside a full store and carrying it back to the
  same full store. §4.3's condition, and it gets a test.
- **A sixth instalment of D76.** Any new reader that asks *"how much has the village got?"* and
  is taught about the ground is this bug. Stacks are not stores; the answer is no.
- **The suite going from four minutes to ten.** §4.4.
- **A cold start that cannot begin.** D95's failure. Step 3 must not land before steps 1 and 2,
  and `JoesOpeningSurvivesOnTheShippedConfig` is the gate — run first, not last.
- **Goods leaking.** The opposite direction and the one that is hardest to see, since totals
  only ever fall. §3 is the existing case; a conservation guard is the answer.

---

## 8. How it is tested

Against **both** `VillageFixtures.Village` and the shipped config, per METHODOLOGY §3.

**Ground stacks**

1. **Determinism green**, and a stack changes the hash — a heap is sim state.
2. **A load with nowhere to go is set down, not destroyed.** Conservation across a village
   whose stores are full: total goods before == in stores + in arms + on the ground.
3. **⭐ Somebody comes and gets it.** A heap, a store with room, and a spare hand: the heap
   reaches the store.
4. **Nobody comes when there is no room.** The heap stays put and nobody shuttles.
5. **A heap is not supply.** `TotalFood`, `LogsInWarehouses`, `FirewoodInWarehouses` and
   `FoodTheVillageHasRoomFor` are unchanged by a hundred logs lying in a field.
6. **Setting down is last resort.** A village with room never puts anything on the ground —
   asserted over a played run, not by inspection.
7. **A village that has dropped nothing hashes as it did.** Sparse hashing, §4.5.

**The instant pile**

8. **A pile on clear ground stands the tick it is marked** — no site, no builder.
9. **A pile on a forest tile is accepted and the tile is painted for harvest** (D100), and
   **the village clears it unasked** — a played half-year with nothing else done leaves the
   pile standing on grass.
10. **It goes up from `SetTerrain`**, so the player clearing it by hand works identically.
    **And every building asks for its ground to be cleared, not only the pile** — but only the
    pile waits.
11. **`pile_work_ticks` is gone from config and from the shipped file.**
12. **Demolishing a pile or a cart refunds nothing** (§5.2).

**The cart**

13. **The cart refuses logs**, and a forester with a full cart still gets rid of a load —
    to a store if one will take it, to the ground if not.
14. **⭐ The arm that killed a village no longer strangles it:** Joe's opening *with trees
    painted and no store placed* holds **940 food at five years**, where D89 measured 164.
15. **⭐ And the village SAYS it has nowhere to keep timber** — once, at t16 of a founding with
    no pile, and never when a pile stands. **This is not polish**: goods on the ground are
    supply-invisible, so without it a hut reports *"no logs here to split"* beside four hundred
    logs, which is D89's silent strangling in a new costume and the failure §1.1 forbids.
16. **`JoesOpeningSurvivesOnTheShippedConfig` and the whole cold-start file stay green** —
    the gate. **`PlayTheOpening` places a pile again**, because D90's rule now binds: you
    cannot take timber until you have somewhere to put it, and a warehouse cannot stand in for a
    pile because a warehouse costs 30 logs and is a construction site.

**Everything**

17. **All three goldens accounted for.** ✅ Step 1 moved both fifty-year hashes — the leak in
    §3.1, re-taken in the same commit with the old values kept beside them. **Steps 2 and 3
    moved nothing**, which is the right answer: a village that marks no pile and has no cart
    is untouched by either.

---

## 9. What must be measured, and before which step

- ~~**Before step 1:** does an established fifty-year village ever fill a store?~~ ✅ **Done,
  and it does** — §3.1. Both goldens move, and the reason is written into `StockLimitTests`
  beside them.
- ✅ **Before step 3, and taken on both sides of it:** D93's five ticks, shipped config, the
  guard's own village.

  | | D93 (green code) | after steps 1–2 | after step 3 |
  |---|---|---|---|
  | builder funded | t120 | t120 | t120 |
  | logs delivered | t129 | t123 | t128 |
  | hut standing | t172 | t166 | t170 |
  | hut staffed | t240 | t240 | t240 |
  | **first firewood** | **t300** | **t245** | **t248** |
  | slack before winter (t360) | 60 | 115 | **112** |

  **The cart change costs about three ticks and the slice as a whole buys fifty.** D93's
  finding was that *any* perturbation of who works when killed the opening; this one does not,
  and the reason is that the two preconditions went in first.

- ⚠️ **And one thing the measuring turned up that is not fixed here.** A probe that sited the
  pile at (-3, -3) survives comfortably; the same opening with the pile at (-1, -2) **killed
  all four founders**. That is D93's *"balanced so finely that any perturbation kills it"*
  standing unchanged, now visible in *placement* rather than in labour. It is the strongest
  remaining argument for widening the opening's slack deliberately — `specs/cold-start.md §7.2`,
  and Joe's call.

---

## 10. Definition of Done

1. This spec current.
2. The sixteen guards in §8 green; full suite green.
3. Determinism green; every golden either unmoved or deliberately re-taken with its reason.
4. `DESIGN.md` §6 and §7 updated in the same commit as the behaviour.
5. No new warnings or errors in a clean playthrough.
6. **Joe plays it**: place a pile in a wood and be told why not; clear it and place it; watch a
   heap appear and somebody come and get it.

# Spec: Storage piles, and asking for a store by what it holds

**Decisions:** D64, D70–D76. **Slice C1** of the Banished opening.
**Status:** ✅ **BUILT.** A store is asked for by what it holds rather than by which building it
is (D76 — the seam that recurred five times), and a pile stands the tick it is marked on clear
ground (D98). D90 made the pile load-bearing again by stopping the cart taking logs. Guarded by
`InstantPileTests` and `PluralStoresTests`. Marked *"in progress"* long after it finished; see
D159.

---

## 1. Goal

Two halves of one fix:

1. **A store is asked for by what it can hold, not by which building it is.**
2. **A storage pile** — the first thing the player places, costing nothing but cleared
   ground, holding anything.

---

## 2. Why, and the evidence

Joe's fourth cold start built the woodcutter's hut and everyone still froze. The panel said
**"Holding: nothing — no logs here to split."** `BehaviorSystem` fetches the hut's logs from
`NearestStore(hut, StoreKind.Shed, …)` — **sheds only** — and he had marked a hut but no
shed, so every log sat in the cart where the hut could not see it.

**That is the fourth site of one bug.** `TryTakeBuildingTimber` (D72), `StoreForTheLoad`
(D72) and the builder's material fetch (D75) were the first three, each patched by adding
`or StoreKind.Cart`. D75 wrote down that there would be a fourth. A census found **23
decision points switching on `StoreKind` and 14 hard-coded kinds passed to finders.**

> **A fifth patch is not a fix, it is the next instalment.** The seam is the question being
> asked. *"Where is the nearest shed?"* is a question about buildings; *"where can I put
> these logs?"* is a question about goods, and only the second survives a new kind of store.

**It is not a config problem.** `VillageFixtures.Village` and `data/sim.config.json` match on
every economy number, derived ones included. The test passes because it marks a shed first
and Joe did not.

---

## 3. Which pillars this serves

- **§1.1 legibility.** *"No logs here to split"* was true and useless — the logs were forty
  tiles away in a cart nobody would fetch from. The village could not explain itself because
  it could not see its own goods.
- **§2.2**, and D64's shape: the player places the pile, the sim decides who hauls to it.
- **§0's core loop.** The opening becomes *place somewhere to put things → paint → build*,
  which is a pipeline the player builds rather than a puzzle they lose to.

---

## 4. Asking by capability

`BehaviorSystem.NearestStoreAccepting` already dispatches on `StoreBuilding.Accepts(Goods)`
rather than on `Kind` — **it is the one finder that got this right**, and the work is to
widen it rather than to teach 23 switches about a new kind.

- `SimWorld` gains `NearestStoreAccepting(GridPos from, Goods goods, Func<StoreBuilding,bool>
  usable)` beside `NearestStore`.
- The hard-coded call sites become questions about goods:
  `BehaviorSystem` 348 (drop a load), 402 (feed the hut), 522/526 (build), 1633 (put back);
  `HouseholdSystem` 170/255 (timber for a house); `Household` 145 (`ChooseSite`'s food store).
- **Each is a decision, not a rename.** `SimWorld.cs:113-126` records the last time the
  singular store accessors were deleted, and that the call sites each needed a *decision*.

### 4.1b The capacity side, and the third instalment (D81)

D79 widened the **totals** — `FirewoodInSheds`, `LogsInSheds` — after a cold start with a
cart full of firewood reported having none. **The capacity side was never looked at**, and it
had the same bug:

```
FoodTheGranaryHasRoomFor = min(target, GranaryCapacity())   // granaries, BY KIND
        compared against  FoodInGranaries()                 // any store that takes food, BY CAPABILITY
```

**One comparison asking two different questions.** A village with a pile and a cart and no
granary scored **zero room**, so *"does the village want more food?"* answered **no,
forever** — measured at **0.0% of gatherable ticks**, against 99.9% once asked by capability.
That is D80's *"one couple stays home"*: with the village-wide reason to work dead, a
household's own larder is the only reason left, and the family that fills theirs first stops
working for good.

So the rule generalises past finders and past totals: **anything that switches on
`StoreKind` to decide what the village has, or has room for, is this bug.** `NearestStoreAccepting`
was the finder; `TotalAccepting` is the total; `FoodTheVillageHasRoomFor` is the room.

**One reader stays keyed to `Granary` on purpose:** `GranaryCapacity()`, because *how big may
this village get?* is answered **per granary** by design (D33, D39) and "build another one" is
the intended reply. Two questions, two readers — which is the same distinction §4.1 draws
below, from the other end.

### 4.1 What must NOT change

`LabourQuota.WoodcuttersWanted` counts **firewood in sheds**, deliberately (D29): a pile in
somebody's house is not supply because no errand reaches it. **A storage pile is reachable
supply and therefore counts**; a household larder still does not. That distinction is the
whole of D29 and must survive this slice — the guard is that the village must not freeze
with a full pile, which is D29's original failure in a new costume.

---

## 5. The pile

| | |
|---|---|
| Costs | **Nothing.** `BuildingRecipe(Logs: 0, WorkTicks: n)` — `ConstructionSite.Work()` already short-circuits on `HasMaterials`, trivially true at zero logs. |
| Needs | Clear, reachable, buildable ground — `CanBuildAt`'s existing rules. |
| Holds | **Anything**, like the cart. |
| Capacity | **Derived** (`VillageEconomy.PileCapacity`), never typed in. Its *size*, not its rules, is what stops it being the granary. |
| Shape | **One tile for now.** Drag-sizing is slice C4 and needs a footprint concept the game does not have. |
| Shelter | **None.** `ShelterAt` knows only about homes. A pile is outdoors. |

---

## 6. Failure modes

- **A fifth sheds-only site.** The point of §4. Any new call that names a `StoreKind` to
  decide where goods go is this bug again.
- **The pile becoming the granary.** It accepts everything, so only its capacity restrains
  it. Derived and small.
- **D29 in a new costume:** a village freezing beside a full pile because the fuel demand
  still only counts sheds. §4.1, and it gets a test.
- **Tuning the cart to make winter survivable.** Refused three times already (D72, D73, D75)
  and refused again: the dials are stated in `specs/cold-start.md §7.2`.

---

## 7. How it is tested

1. **Determinism green**; the pile is a `StoreBuilding` and hashes like one.
2. **⭐ Joe's play survives winter 1 — on `ShippedConfig.Load()`, not the fixture.** Place a
   pile, paint residential, mark a woodcutter, **mark no shed**. This is the exact sequence
   that killed his village and the reason the slice exists.
3. **The hut is fed from a pile.** Directly: logs in a pile, a hut, and firewood comes out.
4. **A village with only a pile still freezes if the fuel chain is broken** — the D29 guard,
   so §4.1's distinction is proven rather than assumed.
5. **Doing nothing still kills** (`ColdStartTests`), unchanged.
6. **The established village is untouched** — full suite green, both golden hashes unmoved.
   A pile nobody places must change nothing.
7. **A village with a pile and no granary has somewhere to put food** (D81) —
   `AVillageWithAPileAndNoGranaryStillWantsFood`, and its behavioural half
   `TheOpeningFillsItsStores`: five years of play used to put **zero** food into a store.
8. **The opening outlives its founders** — `AVillageGivenOnlyAPileOutlivesItsFounders`. The
   guards above all stopped at five years, and a village that never has a child looks
   perfectly healthy for twenty.
9. **Neither founding household rests while the other works** —
   `NeitherFoundingHouseholdRestsWhileTheOtherWorks`, at a factor of two, because a residual
   gap from job-kind duty cycles is an open design question and not this bug.

---

## 8. Definition of Done

1. This spec current.
2. The six guards in §7 green; full suite green.
3. Determinism test green, goldens unmoved.
4. Joe plays the opening he described and it works.
5. `DESIGN.md` §6 and §7 updated.

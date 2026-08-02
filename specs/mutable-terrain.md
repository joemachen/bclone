# Spec: Mutable terrain — the foundation the harvest brush stands on

**Decisions:** D18, D40, D41, D42, D84. **Slice C3a** — the first of C3, and the only part of
it that is unblocked. **Status:** specced, in progress.

---

## 1. Goal

**Terrain can change during a run, and every system that cached an answer about it finds out.**

Nothing player-facing ships here. This is the floor that `building-placement.md §12`'s harvest
brush, the planting brush, bridges (D40) and paved roads all stand on, and D41 predicted it by
name a phase early:

> *"Cached with no invalidation protocol, deliberately, because `GeneratedMap` is immutable…
> When terrain becomes mutable — a felled stand, a paved road, a bridge — that stops being
> true and the cache needs a way to be dropped."*

**It ships on its own because it is the one part of C3 that no open question touches** (§5).

---

## 2. Which pillar this serves

- **§2.3 — resource radii exhausting and forcing expansion.** A forest that recedes where it
  was felled is that pillar's only real machinery, and it cannot exist while terrain is frozen.
- **§1.1 legibility, applied to the codebase.** A cache that is right *by luck* is the same
  class of thing as a guard that passes for the wrong reason (D78). The rule gets stated.
- **Determinism.** Terrain is sim state the moment it can change; two runs given the same
  decisions must produce the same valley.

---

## 3. What was measured first, and it cut the slice in half

Both of the C3 plan's stated costs were checked against the code before anything was written,
and **one of them does not exist.**

| The plan says | Measured |
|---|---|
| *"`StateHash.MixMap` moves from once-at-start to incremental, and the map golden must be re-taken deliberately."* | **Not needed.** `MixMap` already walks the **live** `map.Tiles` array on every `Compute` — `StateHash.cs:51`. Mutable terrain is correctly hashed today, and no golden moves until terrain actually changes in a run. The comment on `SimWorld.Map` claiming the hash *"covers it once"* was describing the contents never changing, not the hashing being one-shot. |
| *"The flow-field cache needs an invalidation path before the first mutable tile ships."* | **Real.** `TravelCostField._fields` is a `Dictionary<GridPos, TerrainCostField>` with no invalidation at all (`TravelCostField.cs:36-44`). |

**So this slice is one mechanism, not two** — and the half that was skipped was skipped because
it was checked, not because it was forgotten.

---

## 4. The design

### 4.1 Terrain changes through one door

`GeneratedMap` gains a single mutator. Everything else about it stays read-only, so *"who
changed this tile?"* has one answer.

- Returns **whether it changed anything**, in the house style (`ZoneMap.SetResidential`).
- Refuses an out-of-bounds tile the way every other reader does, rather than throwing.

### 4.2 The cache is dropped when **passability** changes, not when terrain changes

This is the load-bearing decision and it is deliberately *not* the simpler rule.

- **Dropping on any change is too expensive.** Each cached field is a full Dijkstra over the
  whole valley, one per building. A logger fells every `cut_ticks`, so "drop everything on any
  terrain change" would rebuild every field in the village several times a year.
- **Dropping on none is what we have, and it is right by luck.** `TerrainCostField` treats only
  `Water` as impassable and charges `Grass` and `Forest` the same, so felling a stand genuinely
  does not move any route — today. §12.3 says this in as many words: *"travel costs survive —
  but only by luck, and planting or bridges will not be so kind."*
- **So the rule is stated in terms of what actually matters:** a change is *route-affecting* if
  the tile's passability differs before and after. Felling forest is not; a bridge over water
  is; rock, if it is ever impassable, is.

**Both branches get a test**, because a rule with an untested branch is the luck again.

### 4.3 Passability is asked of the terrain, not listed at the call site

`Terrain.Water` is named in two places today (`TerrainCostField.cs:109, 153`). A third naming
of it here would be the `StoreKind` seam in a new costume (D76, five instalments) — so
passability becomes a question the terrain answers, and the two existing sites ask it too.

---

## 5. What this slice does NOT do, and why

- **No new terrain kinds.** `Rock` and `IronDeposit` wait on **D84**, which is open: the C3
  plan makes stone spatial and exhaustible while `buildings-plan.md §2.2` makes it finite in
  *effort*. **The buildings plan is loose guidance rather than prescriptive (Joe), so this is a
  conflict for him to settle rather than a rule being broken** — but settling it is exactly
  what must happen first, because adding the enum value picks the answer by accident.
- **No harvest brush.** Already specced (`building-placement.md §12`) and blocked on §5.1
  below.
- **No regrowth clock.** §12.8 requires it be *derived* from a stated target — *a valley
  cleared by a village of thirty comes back in about a generation* — and that is the harvest
  brush's slice, not this one.

### 5.1 ⚠️ The open question the harvest brush hits, recorded here so it is not rediscovered

**`building-placement.md §12.5` says unpainted forest is never cut. The cold start now has
nothing painted.**

Those two were settled six days apart and have never been true at the same time. §12.7 saw the
problem and recommended founding the village with a starter harvest zone already painted —
but **D70's cold start deliberately deleted the auto-painted residential zone** so that the
player's first act is a decision, and Joe has since played and validated that opening.

So the harvest brush cannot ship on §12.5's rule without either reversing D70's founding or
giving the founders a fellable stand some other way. **Joe's call, and it wants answering
before the brush is built rather than during.**

---

## 6. Failure modes to design against

- **A stale route.** The whole point. A villager walking a path the cost field no longer
  believes in is the worst kind of bug here: silent, rare, and it looks like a pathing quirk.
- **A cache dropped every tick.** The other side. If felling invalidates, the village pays a
  full Dijkstra per building per fell — which is why §4.2 asks about passability.
- **Terrain drifting out of the hash.** It does not, and §3 is why; a guard says so rather
  than the comment.
- **A second passability list.** §4.3.

---

## 7. How it is tested

1. **Determinism green**, unchanged.
2. **A changed tile changes the hash** — terrain is sim state, provably.
3. **Same seed, same edits ⇒ same hash.** Mutation must not be a way round the seed contract.
4. **A route-affecting change drops the cache**, and the next query gives a *different* answer:
   wall a destination off and the cost must rise or become unreachable.
5. **A non-route-affecting change does not drop it** — felling forest leaves every cost
   identical, which is the performance claim, asserted rather than hoped.
6. **Out-of-bounds is refused, not thrown.**
7. **Both goldens unmoved.** Nothing in a run changes terrain yet, so a village that never
   fells must hash exactly as it did.

---

## 8. Definition of Done

1. This spec current.
2. The seven guards in §7 green; full suite green.
3. Determinism green, both goldens unmoved.
4. `DESIGN.md` §6 and §7 updated.
5. D84 and §5.1 stated as open, with recommendations, for Joe.

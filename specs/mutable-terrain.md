# Spec: Mutable terrain — the foundation the harvest brush stands on

**Decisions:** D18, D40, D41, D42, D84, D85. **Slice C3a** — the first of C3.
**Status:** ✅ **BUILT** (D85). Terrain changes during a run and every system that cached an
answer about it finds out, through `SetTerrain` as the one door — which is what D100's
clear-the-ground-for-a-building rule was later hung off, and what D157's clearing priority
depends on. Guarded by `MutableTerrainTests`. Marked *"in progress"* long after it finished; see
D159.

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

### 5.2 ✅ What is finite and what is not — Joe's rule (D84)

**A *deposit* is finite. A *building* is not.** The axis is **found versus placed**, not surface
versus subsurface: a deposit is a quantity the generator put in a place and you take it until
it is gone; a building is a livelihood the player sited, and it does not run out. Loose rock and
a quarry both yield stone and are not the same object.

| Source | Kind | Finite? | Worked by |
|---|---|---|---|
| Forest tile | deposit | **Yes** — and regrown, by a forester (D86) | laborer / forester |
| Surface stone deposit | deposit | **Yes** | laborer |
| Iron deposit | deposit | **Yes** | laborer |
| Gem deposit | deposit | **Yes** (D67: visible seams, never a roll) | laborer |
| Quarry | building | **No** | quarry worker |
| Iron mine | building | **No** | miner |
| Berries, mushrooms, herbs | gatherer site | **No** | gatherer |

**It is case by case on purpose** and the table is the record, so a new resource is a row
somebody has to fill in rather than a default it inherits silently.

**Why this shape is right, in one line each:**

- **A spent deposit leaves no scar** — the tile goes back to ordinary ground — which answers
  `buildings-plan.md §2.2`'s stated objection to depletion. That objection was only ever true
  of depleting a *building*, and nothing does.
- **§2.3 gets its expansion pressure free.** Deposits run out, so the village moves. The
  building is what you place when you are tired of moving.
- **⚠️ Watch:** with quarries and mines infinite, all of §2.3's pressure rests on deposits and
  trees. The quarry must therefore sit far enough up the tree that clearing deposits is
  genuinely the early game.

### 5.1 ✅ The harvest-brush conflict — resolved by D86, and the brush moved

**Superseded: `building-placement.md §12` makes harvest a global zone layer. It belongs to a
building instead.**

The conflict was real: §12.5 says *unpainted forest is never cut*, and D70's cold start
deliberately paints nothing, so a founding would fell no timber, make no firewood and freeze.
§12.7 proposed auto-painting a starter zone, which reverses D70's whole point.

**Joe's answer removes the conflict rather than picking a side (D86): tree stands become
forester's huts, and the painted area belongs to the hut.** The player places a hut on day one
exactly as they already place a woodcutter's hut, and paints its ground. **No auto-paint and no
exception to D70** — the timber chain is simply one more thing you site.

It also fixes what §12 left open: the labour allocator is built around **workplaces with a
catchment** (D21–D25), and a global harvest zone contains no workplace, so *"who fells here,
and do they live near it?"* had no answer. **Area is priced in workers** — more foresters, more
ground, to a limit — and the village **warns** when the paint outruns the hands.

### 5.4 What comes next, in order

**C3b — zones learn to belong to a building.** `ZoneMap` is one `bool[] _residential` today.
D86 needs a painted area **keyed by the hut that owns it**, not merely a second global layer —
which is a bigger change than "add a layer" and is the reason it is its own slice. Residential
stays global; it belongs to the village, not to a building.

**C3c — the forester's hut.** Placed like a woodcutter's hut, staffed like one, and its ground
painted. Felling comes off *its* tiles, the forest recedes where it was cut, and the village
warns when the paint outruns the hands. **Tree stands retire here.**

**C3d — laborers, and deposits.** Surface stone and iron as finite deposits per §5.2, cleared
by the laborers D66 could find no work for.

**Not in C3 at all — planting.** It is gated behind managed forestry (§12.5, Joe), and §2.7 is
unbuilt. The hut can fell before it can sow, and that is the right order: the harvest is what
*creates* the pressure the forestry node answers.

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

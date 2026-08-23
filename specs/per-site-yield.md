# Spec: Per-site yield — ground that is worth going to

**Decisions:** **D58** (the mechanism, settled — per-site yield, not work-in-place), D178 (this
slice's scope and shape), D16 (numbers are derived, not picked), D2 (integer-only sim state),
D67 (**seams, not scatter** — the argument this spec leans on hardest), D112 (per-site yield for
the gatherer, already shipped), D120 (the fence came down), D171 (the farm's measured distance
bug), D152 (goldens last).
Neighbours: `crops-and-orchards.md` (the farm this changes), `seeded-map-generation.md` (the draw
order, which is the seed contract), `forests-and-gathering.md §3.2` (where the bound became a
budget), `environment-and-seasons.md` (soil depletion's eventual home).
**Status:** ✅ **BUILT and merged to `main`** (D178).
Soil is regional and read by the farm; the sowing cap asks each farm's own haul; the player can
see the ground. **641 passing, 0 failing, 2 skipped of 643**, all four goldens re-taken.
Proved by `PerSiteYieldTests` and `FarmTests.AFarmsHarvestFallsOffWithDistanceFromItsStore`.

> **⚠️ This status line is load-bearing. Update it the day the slice merges** — D159 found five
> specs claiming "not started" for systems that had shipped.

---

## 1. ⭐ What this actually is, because the queue was wrong about its size

`DESIGN.md §4` calls this *"the biggest payoff on the board and the largest re-derivation."*
**An audit of the code says otherwise: over half of D58 has already shipped under other names.**

| D58's parts | State |
|---|---|
| Retire the 7-tile bound **as a fence** | ✅ **Done (D120).** `Household.ChooseSite` uses it as a score term and a search radius. *"A home beyond it is a family the village feeds less well, not one it refuses to house."* |
| Per-site yield for **gathering** | ✅ **Done (D112).** `SimWorld.GatherYieldAt` is `gather_yield × wooded tiles ÷ ring tiles`. A hut in thick wood genuinely pays more. |
| Per-site yield for **farming** | ⛔ **Not done.** `FieldTilesOneFarmerKeeps` is one number for every farm in the valley. |
| **"Distant sites pay better"** | ⛔ **Not done, and it is the important half.** Nothing in the sim rewards distance; `VillageEconomy` does not read travel cost at all. |

**D58's own warning is what makes the last row the point:**

> *It is two halves and both are budgeted: distance costs, **and** distant sites pay better.
> Every forage site currently sits on a ring with the work in the middle, so per-site yield on
> its own is **a tax on sprawl that buys nothing** — inequality with no reason to accept it. The
> design is **the frontier homestead beside a rich patch: isolated, and eating well for it.**
> That is D32's interesting inequality.*

**So this slice is not a re-derivation. It is: make soil real, and give the farm per-site yield
on both axes.** Gathering is left alone — it already works.

---

## 2. Which pillars

- **§2.3 Systemic escalating pressure** — *"resource radii exhausting and forcing expansion."*
  Ground that differs is what makes expansion a decision rather than a chore.
- **§2.2 Smart labour** — D32's *interesting inequality*: a household that settles far genuinely
  eats differently, and now it can eat **better** as well as worse.
- **§1.1 Legibility** — ⛔ **soil the player cannot see is an invisible multiplier**, which this
  project has rejected three times (D37's spoilage, the seasonal yield curve,
  `skills-catalog.md §1.1`). §7 is not decoration.
- **§0.1 The niche** — *"the challenge is in the planning, never in the punishment."* Good ground
  you can see and choose to walk to is planning. Good ground you cannot see is a lottery.

---

## 3. ⭐⭐ Soil becomes regions, not noise — and D67 is why

**Soil already exists and is the right axis.** `MapGenerator` step 5 fills `soil[]` with
`rng.NextInt(soil_quality_min, soil_quality_max + 1)` per tile — **generated, hashed, in the seed
contract, and read by nothing**, laid down early *"so that when soil depletion lands it does not
have to change the DRAW ORDER."* That foresight is what makes this slice cheap.

**⛔ But per-tile uniform noise is the wrong shape, and D67 already said so about ore:**

> *SEAMS, NOT SCATTER … you can see a seam, so going after it is a **decision** rather than a
> lottery. **Scattered ore would be texture.***

**Per-tile soil noise is texture.** A field is thirteen tiles; over thirteen uniform samples every
site in the valley averages the same. **Uniform noise gives per-tile variance and zero per-site
variance, which is precisely the one thing this slice needs.** Soil must be *regional* — good
ground and poor ground you can point at.

### 3.1 ⭐ How, without moving one byte of any seed's layout

**Keep the draw count identical and reshape afterwards.** Step 5 goes on drawing exactly
`width × height` values from the seeded stream; a **deterministic** pass then turns that noise
into regions, consuming **no draws**.

> **⛔⭐⭐ AND THE FIRST ALGORITHM THIS SPEC PROPOSED WAS WRONG — the probe said so before a line
> shipped, which is the entire argument for METHODOLOGY §3.** The draft said *smooth the noise*.
> **Smoothing makes site-to-site variance worse**, because averaging noise regresses everything
> toward the mean: it destroys amplitude rather than creating structure. Measured on the shipped
> valley, spread across 104 candidate 13-tile fields:
>
> | | p90 ÷ p10 | best ÷ worst |
> |---|---|---|
> | raw per-tile noise | 134% | 181% |
> | smoothed ×2 | 123% | 142% |
> | smoothed ×8 | **113%** | 130% |
> | **value noise, lattice 8** | **200%** | **308%** |
>
> *An hour of probe against a slice built on a mechanism that reduced the number it existed to
> raise.*

**The algorithm is value noise: sample the already-drawn array on a coarse lattice and interpolate
between the samples.** Lattice points keep the full drawn amplitude — which is exactly what
smoothing throws away — and the interpolation supplies the structure.

- **`soil_region_scale: 8`**, measured. Finer (4) averages out across a 13-tile field; coarser
  (24) leaves too few distinct regions. **8 is a couple of fields across — a region you can see
  and walk to**, which is D67's *seam* rather than its *scatter*.
- **Integer bilinear interpolation** (D2). One divide per tile, no floats.
- Measured at lattice 8: **p10 82, median 124, p90 164** — a genuine two-to-one between poor and
  good ground.

**Why this matters more than it sounds.** Draw order is the seed contract
(`seeded-map-generation.md §1`). Clumping soil by drawing region centres would consume a different
number of values, which shifts **step 6 — stone and iron — for every seed ever written down**, and
D91 took explicit care to append those precisely so nothing moved. **With the count preserved, the
RNG state entering step 6 is identical**, so:

- ✅ Terrain, river, woodland, forage sites, the founding site, stone and iron: **byte-identical**.
- ⚠️ **The soil bytes change**, and `StateHash.MixMap` mixes them — so **the map golden moves, and
  nothing else does.** One golden, one stated reason (D152).

**Integer-only** (D2): the smoothing is an integer box average over the neighbours, applied a
stated number of passes (`soil_smoothing_passes`, data). No floats anywhere near it.

### 3.2 ⭐ The founders settle where they can survive, not where the ground is best

**Half of this is already true and nobody wrote it down.** `ChooseFoundingSite` is **step 4** and
soil is **step 5** — so the founding site is chosen *"the biggest piece of walkable ground"* with
**no knowledge of soil whatsoever.**

> **⛔⭐ AND THE PROBE KILLED THE INFERENCE DRAWN FROM THAT.** This spec first argued the founding
> ground was therefore *"already safe-not-rich by construction"*. **It is not.** Chosen without
> knowledge of soil means the founding ground's percentile is **uniformly random**, and across
> eight seeds at lattice 8 it came out:
>
> | seed | 12345 | 1 | 2 | 3 | 7 | 11 | 42 | 99 |
> |---|---|---|---|---|---|---|---|---|
> | founding-ground percentile | 22% | 11% | **99%** | **93%** | **83%** | 34% | 11% | **91%** |
>
> **Half the seeds hand the founders ground in the top fifteen per cent** — seed 2 puts the best
> field in the valley on their doorstep. **That deletes D58's frontier payoff outright** in half
> of all games, which is precisely the failure this slice exists to prevent.
>
> ✅ **The good news from the same run: the best ground is always worth going to.** Across all
> eight seeds it sits **12 to 50 tiles out**, at **177–193** against a valley median of 99–134.
> *The frontier homestead beside a rich patch is there in every seed* — it just has to stop being
> outshone by the doorstep.

**So damping is required rather than optional**, and it is a stated rule inside a small radius of
the founding site.

**⭐ The rule is a CAP, not a reduction, and the cap is the reference itself** (§4.1): soil within
`founding_ordinary_radius` of the founding site is **capped at the valley mean**.

- **It can only ever take away, never add** — ground that is already poor is untouched, so this
  cannot quietly make a hard seed easier.
- **⭐⭐ And it gives the cold start a property worth having: the founders' fields yield *at most*
  exactly `crop_yield_per_tile`** — the locked number, which §4.1 defines as the yield on average
  ground. **The opening can therefore never be *better* than today's**, which is the direction
  that matters, since today's opening is the one `cold-start.md` measured and Joe played.
- **It is diegetic, which is the test this project applies:** *the exiles settled where they could
  live through the first winter, not where the ground was best.* `tech-tree.md §3b` is right that
  the best game rules are also true ones.
- **It is a local cap, not a global gradient** — soil does not "improve with distance", which
  would read as a game rule rather than a place.
- **It costs no draws and no draw-order change**, because the founding site is already known when
  step 5 runs.
- ⚠️ **What it does NOT guarantee is that the opening is unchanged.** A seed whose founding ground
  was *below* the mean now farms below-reference ground, and the opening gets harder there.
  **That is why §9.5 re-measures the cold start from a run rather than asserting it** — and if the
  measurement says so, the cap gains a floor. **Decided by measurement, not here.**

---

## 4. ⭐ The farm gets per-site yield on two axes

**Both of D58's halves, one axis each.**

### 4.1 Soil — how good the ground is (*distant sites can pay better*)

**⛔ `crop_yield_per_tile` is on Joe's locked list and this must not re-derive it.** So soil is a
**multiplier around** the locked value, never a replacement:

```
crop = crop_yield_per_tile × (soil ÷ reference soil) × vigour ÷ 100
```

- **The reference is the valley's mean soil**, derived from `(soil_quality_min + soil_quality_max)
  ÷ 2`, not typed. **A field on average ground yields exactly what it yields today** — so the
  locked 67 is untouched and *acquires a precise meaning it never had*: **67 is the yield on
  average ground.**
- **This is `skills-catalog.md §3.2`'s shape one system over**, and for the same reason: a
  multiplier that averages to one leaves every derived number standing, where a multiplier above
  one silently inflates the whole economy.
- **Good ground pays more; poor ground pays less.** That is the decision.

### 4.2 Distance — how far the harvest has to go (*distance costs*)

**This is D171's measured bug and it is a per-site problem wearing a farm's clothes.**
`FieldTilesOneFarmerKeeps` charges *"a round trip to the steading"* in its own words, and is one
number for every farm — so a farm ten ticks from its store **sows what a farm next door could
reap** and rots the difference. Measured, ten years each:

| farm → granary | brought in |
|---|---|
| next door | 93–96% |
| 6 ticks | 52% |
| **10 ticks** | **46%** |
| 22 ticks | 25% |

**The fix is that the sowing cap asks *this* farm's real haul**, not the average farm's. The
derivation keeps its meaning — *what a well-sited farm manages* — and a poorly-sited one commits
less ground rather than committing the same ground and rotting it.

**⭐ And it makes the rot line honest, which is the whole of D167's argument.** D167 made rot mean
*you over-painted* or *you lost a farmer*; **distance is a third cause the game cannot currently
say**, and a rot line the player cannot act on is the weather D167 spent a decision deleting.

⛔ **`farm_store_cap` is not the lever and must not be touched** — measured at nought to seven
points across one armful to thirteen (D171).

---

## 5. Legibility — the player must be able to see the ground

**A soil multiplier the player cannot see is exactly the invisible number §1.1 refuses.** If this
ships without §5, it is a lottery rather than a decision, and D67's argument applies to it.

- **⭐ A soil overlay on the map**, in the same family as the work-ground and harvest washes that
  already exist (D118). Off by default, one control — the player asks *where is the good ground?*
  and the valley answers.
- **On the farm's panel, a sentence rather than a number** (D147's rule, and the vocabulary
  `crops-and-orchards.md` already uses): *"Rich ground — a tile here is worth about a fifth more
  than average."* / *"Thin ground — about a fifth less."*
- **On the brush**, once per stroke (D42): painting a field on poor ground says so.
- ⚠️ **A soil *number* on the panel is the failure mode**, not the fix. `proficiency 73` was
  rejected in `skills-catalog.md §7` for the same reason.

---

## 6. Determinism and the state hash

- **Integer only** (D2), including the smoothing.
- **Draw count preserved** (§3.1), so every layout is byte-identical and **only the map golden
  moves**.
- **Soil is already hashed** — `StateHash.MixMap` walks `map.Soil`, so nothing new enters the
  hash and no sparse-hash trick is needed.
- ⚠️ **Two village goldens will move as well**, because a farm's yield changes: any village that
  farms has a different history. **Both, once, last, one stated reason** (D152). The 50-year
  goldens place no farmhouse (D162), so they should *not* move — **and if they do, that is a
  finding, not a nuisance.**

---

## 7. What is deliberately NOT here

- **⛔ Gathering is untouched.** It already has per-site yield (D112) and is not broken. Re-opening
  `gather_yield` reopens `MaxHomeToWorkTiles` and the fuel budget together, **and D122 froze
  nineteen people the last time that chain moved — by one tile.**
- **⛔ Soil depletion** (§2.3, `crops-and-orchards.md §7`) stays deferred. **This slice makes soil
  *matter*; depletion makes it *change*.** Two systems, and the second is uninteresting until the
  first exists.
- **⛔ No new refusal.** Poor ground is warned about and never forbidden (D43, D86). Farming bad
  ground on purpose is a decision the player is allowed to make.
- **⛔ `crop_yield_per_tile` and `farm_store_cap` are not re-derived.**

---

## 8. Testing

- **Determinism** — same seed, 200 years, identical state. P0.
- **⭐⭐ The layout does not move** (§3.1) — terrain, river, woodland, forage sites, founding site,
  stone and iron are **byte-identical across the change** for a sample of seeds. **This is the
  guard that says the draw order survived**, and it is the one that would catch the smoothing
  being implemented with an extra draw in it.
- **⭐ Soil is regional, not noise** — the variance *between* well-separated sites is materially
  larger than it is under uniform noise. *Without this the smoothing could do nothing and every
  other guard would still pass.*
- **⭐ Average ground yields exactly today's number** (§4.1) — the locked `crop_yield_per_tile`
  still means what it meant, so nothing downstream moved.
- **⭐⭐ Rich ground beats poor ground, measured over a year** — not at the predicate. D144's rule:
  *a rule tested where it is decided and never where it is used is a rule nobody has tested.*
- **⭐⭐ A distant farm no longer sows what it cannot reap** (§4.2) — the brought-in percentage at
  ten ticks rises materially from the measured **46%**, and **the rot falls**. Assert the year,
  not the step (D167).
- **⭐ The founding stays survivable** (§3.2) — `cold-start.md`'s five ticks re-measured from a
  run, and the twelve-seed arm green. **Damped is not ruined.**
- **Shipped config, not only the fixture** (METHODOLOGY §3).
- **Every guard checked red and counted.**

---

## 8a. ✅ What it measured, once built

| | before | after |
|---|---|---|
| Site-to-site soil spread (p90÷p10) | 134% (per-tile noise) | **182–198%** |
| Founding ground percentile | 99th / 93rd / 91st / 83rd in 4 of 8 seeds | **capped at the reference, every seed** |
| A farm 10 ticks from its store, brought in | **46%** | **96%** |
| …tiles it reaps in ten years | — | **59, against a near farm's 144** |

**Both halves of D58, in the last two rows.** The distant farm stops rotting what it sowed **and
still feeds fewer people** — *distance costs, and distant sites pay better.* A fix that made the
far farm equal to the near one would have deleted the decision.

⚠️ **`AFarmsHarvestFallsOffWithDistanceFromItsStore` had to be re-based**, and went red for the
best possible reason: it was written to *characterise* the bug and said so in as many words. **A
guard that outlives the rule it was written for looks exactly like a regression** (D150).

---

## 9. Definition of Done

1. This spec current and its status line true.
2. **Probe first** (METHODOLOGY §3): measure soil's per-site spread before and after smoothing, and
   the founding-neighbourhood damping, **before any yield reads it.** If regions do not actually
   produce site-to-site variance, the rest of the slice is pointless and better known early.
3. Guards above written and **checked red and counted**.
4. **The layout-unmoved guard green**, which is what licenses the map golden to move alone.
5. `cold-start.md` re-measured from a run.
6. The player can *see* the ground (§5) — *a feature the player cannot reach does not exist* (D103).
7. Goldens re-taken **last**, one commit, one stated reason each (D152).
8. `DESIGN.md §4`, §5 and §6 updated — including **correcting the queue entry's claim about this
   slice's size**, which is how this spec started.

---

## 10. Open

- **✅ ANSWERED BY THE PROBE (§9.2, run before anything was built):** the region algorithm is
  **value noise at lattice 8**, not smoothing — measured **p90/p10 200%** against smoothing's
  113%, which was *worse than doing nothing*. And **damping is required**, because founding
  ground came out at the 99th, 93rd, 91st and 83rd percentile in four of eight seeds.
- **⚠️ Is a two-to-one spread too much?** p10 82 against p90 164 means a tile worth **46 against
  92** on a reference of 67. That is a big swing, and it is the number most likely to want
  tuning after Joe plays it. **The lever is `soil_quality_min`/`max`, not the algorithm.**
- **Does the founding cap need a floor** (§3.2)? Only the cold-start measurement can say.
- **Does the forester want soil too?** Trees on good ground growing back faster is the obvious
  extension and is **deliberately out of scope** here — `forests-and-gathering.md`'s regrowth is
  where it would live.
